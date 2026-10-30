using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace TheVeil.Editor
{
    /// <summary>
    /// What a real character mesh carries, and what comes back out of the decimator:
    /// `The Veil &gt; Rig Probe`.
    ///
    /// Written because the decimator's own tests passed on a mesh built inside the test
    /// and every skinned renderer in the game then refused to draw: "the data for mesh ''
    /// does not match the expected mesh data size and vertex stride". The tests asserted
    /// the data and not that anything would draw it, which is the difference between a
    /// mesh that has bone weights and a mesh the skinning backend will accept.
    ///
    /// <b>It found two things and was wrong once itself.</b> The layout comparison showed
    /// the simplified mesh had no Tangent channel where the source had one - Unity's
    /// skinning writes position, normal and tangent together, so a mesh without tangents
    /// has a stride the backend does not expect - and that the bone weights were four wide
    /// where the pack authors them two. Then its own render check put the helmet's mesh on
    /// every renderer in the model and reported half a per cent of the frame drawn, which
    /// was the probe and not the decimator. Corrected, it cuts each renderer's own mesh
    /// and sweeps the budget from two thousand faces down to twenty-four, drawing the same
    /// share of the frame at every one of them.
    ///
    /// <b>Kept rather than deleted.</b> It is the only thing here that asks whether a mesh
    /// will actually draw, and the next person to change <see cref="MeshDecimator"/> will
    /// want that question answered in seconds rather than from a folder of PNGs.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.RigProbe.Run
    /// </summary>
    public static class RigProbe
    {
        const string Model = "Assets/Stylized_Medieval_Army_Pack/Prefabs - Characters/MC_Levy_01.prefab";

        [MenuItem("The Veil/Rig Probe")]
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (prefab == null) { Debug.LogError("[Probe] " + Model + " is not there."); return; }

            var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null || skin.sharedMesh == null)
            {
                Debug.LogError("[Probe] no skinned renderer with a mesh.");
                return;
            }

            var source = skin.sharedMesh;
            var sheet = new StringBuilder();

            sheet.AppendLine($"[Probe] {source.name}: {source.vertexCount} vertices, "
                             + $"{source.triangles.Length / 3} faces, "
                             + $"{source.subMeshCount} submesh(es), "
                             + $"{source.bindposes.Length} bindposes, "
                             + $"{skin.bones.Length} bones on the renderer");

            Layout("source", source, sheet);

            var thin = MeshDecimator.Simplify(source, 250, out string story);

            if (thin == null) { sheet.AppendLine("[Probe] the decimator returned nothing"); }
            else
            {
                sheet.AppendLine($"[Probe] thin: {thin.vertexCount} vertices, "
                                 + $"{thin.triangles.Length / 3} faces, "
                                 + $"{thin.bindposes.Length} bindposes, "
                                 + $"boneWeights {thin.boneWeights.Length} ({story})");

                Layout("thin  ", thin, sheet);
            }

            Drawn(prefab, 0, sheet, "untouched");

            // <b>Swept rather than sampled, because the mild cut drew and the harsh one
            // did not.</b> One budget is an anecdote; the sweep says where the line is,
            // and a line is a thing that can be looked for in the code.
            foreach (int budget in new[] { 2000, 1000, 400, 200, 100, 50, 24 })
                Drawn(prefab, budget, sheet, $"each skin cut to {budget} faces");

            Debug.Log(sheet.ToString());
        }

        static void Layout(string what, Mesh mesh, StringBuilder sheet)
        {
            foreach (VertexAttribute attribute in System.Enum.GetValues(typeof(VertexAttribute)))
            {
                if (!mesh.HasVertexAttribute(attribute)) continue;

                sheet.AppendLine($"[Probe]   {what} {attribute,-16} "
                                 + $"{mesh.GetVertexAttributeFormat(attribute),-12} "
                                 + $"x{mesh.GetVertexAttributeDimension(attribute)} "
                                 + $"stream {mesh.GetVertexAttributeStream(attribute)}");
            }

            sheet.AppendLine($"[Probe]   {what} bonesPerVertex total "
                             + $"{mesh.GetBonesPerVertex().Length}, weights "
                             + $"{mesh.GetAllBoneWeights().Length}");

            sheet.AppendLine($"[Probe]   {what} blendShapes {mesh.blendShapeCount}, "
                             + $"vertexBuffers {mesh.vertexBufferCount}, "
                             + $"readable {mesh.isReadable}, "
                             + $"indexFormat {mesh.indexFormat}, "
                             + $"topology0 {(mesh.subMeshCount > 0 ? mesh.GetTopology(0).ToString() : "-")}");
        }

        /// <summary>
        /// Whether a mesh put on a real skinned renderer actually draws anything.
        ///
        /// <b>The question the PNG files were being squinted at to answer.</b> It is a
        /// count, so it is an answer: the fraction of the frame that is not the colour the
        /// camera cleared to. A mesh the skinning backend refuses leaves that at nought
        /// and says so in the log, which is a line nobody reads at the time.
        /// </summary>
        static float Drawn(GameObject prefab, int budget, StringBuilder sheet, string what)
        {
            var stood = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (stood == null) return -1f;

            var eye = new GameObject("Probe camera");

            try
            {
                var skins = stood.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                // <b>Each renderer gets a thin copy of its own mesh.</b> The first version
                // put one mesh - the helmet's - on every skin in the model, so the body
                // was drawing a helmet bound to a different set of bones, and reported
                // half a per cent of the frame. That was the probe being wrong, not the
                // decimator, and it is the same mistake twice in one afternoon: an
                // instrument that answers a question nobody asked.
                int faces = 0;

                if (budget > 0)
                    foreach (var skin in skins)
                    {
                        if (skin.sharedMesh == null) continue;

                        var cut = MeshDecimator.Simplify(skin.sharedMesh, budget, out _);
                        if (cut == null) continue;

                        skin.sharedMesh = cut;
                        faces += cut.triangles.Length / 3;
                    }

                var box = new Bounds(stood.transform.position, Vector3.one);
                bool any = false;

                foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !renderer.gameObject.activeSelf) continue;
                    if (!any) { box = renderer.bounds; any = true; }
                    else box.Encapsulate(renderer.bounds);
                }

                var camera = eye.AddComponent<Camera>();
                var sky = new Color(0.55f, 0.63f, 0.72f);

                camera.fieldOfView = 50f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = sky;
                camera.transform.position = box.center + new Vector3(0f, 0f, -4f);
                camera.transform.LookAt(box.center);

                var rt = new RenderTexture(256, 256, 24);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                var shot = new Texture2D(256, 256, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);

                var pixels = shot.GetPixels32();
                int lit = 0;

                foreach (var pixel in pixels)
                {
                    if (Mathf.Abs(pixel.r - (byte)(sky.r * 255)) > 8
                        || Mathf.Abs(pixel.g - (byte)(sky.g * 255)) > 8
                        || Mathf.Abs(pixel.b - (byte)(sky.b * 255)) > 8) lit++;
                }

                Object.DestroyImmediate(shot);

                float share = (float)lit / pixels.Length;
                sheet.AppendLine($"[Probe] {what,-36}: {share * 100f,5:0.0}% of the frame is not "
                                 + $"sky, {faces} faces across {skins.Length} skin(s)");

                return share;
            }
            finally
            {
                Object.DestroyImmediate(eye);
                Object.DestroyImmediate(stood);
            }
        }
    }
}
