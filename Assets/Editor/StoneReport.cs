using System.Text;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The same castle wall in every palette the pack ships: `The Veil > Stone Report`.
    ///
    /// The pack's own artwork shows a warm sandstone castle and ours is grey, and there
    /// are two quite different reasons that could be: the pack has four palette atlases
    /// and we may simply be on the cold one, or the artwork is lit by a low sun and the
    /// stone is the same stone. Those want opposite answers — one is a material swap, the
    /// other is a light — and guessing between them is how a whole pack gets re-skinned to
    /// fix a lighting problem.
    ///
    /// So each atlas is put on the same wall under the same light and photographed. The
    /// cost of being wrong is high here: the atlas is shared with the houses, the town and
    /// the paving, all of which already look right, so swapping it to warm the castle
    /// would warm everything else with it.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.StoneReport.Run
    /// </summary>
    public static class StoneReport
    {
        [MenuItem("The Veil/Stone Report")]
        public static void Run()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            const string dir = "Assets/Synty/PolygonKnights/Materials/";

            string[] palettes =
            {
                "PolyKnights_01", "PolyKnights_02", "PolyKnights_03", "PolyKnights_04",
                "PolyKnights_01_Dark", "PolyKnights_02_Dark",
                "PolyKnights_03_Dark", "PolyKnights_04_Dark"
            };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Wall_Gate_01.prefab");

            if (prefab == null) { Debug.Log("[Stone] no gatehouse to photograph"); return; }

            // A light of its own, low and warm, so that every palette is judged under the
            // same sun rather than under whatever the empty scene happens to have. This is
            // also the half of the question the materials cannot answer: if the warm one
            // turns out to be the light, it will be this that shows it.
            var sun = new GameObject("Stone sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.93f, 0.82f);
            sun.transform.rotation = Quaternion.Euler(28f, 150f, 0f);

            var sheet = new StringBuilder();
            sheet.AppendLine("[Stone] the same gatehouse in every palette the pack ships");

            foreach (string palette in palettes)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(dir + palette + ".mat");
                if (material == null) { sheet.AppendLine($"[Stone] {palette}: not found"); continue; }

                var piece = Object.Instantiate(prefab);

                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }

                var box = ModelScaling.Measure(piece);
                float back = Mathf.Max(box.size.x, box.size.y) * 1.7f;

                Shoot(box.center + new Vector3(0f, box.size.y * 0.2f, -back), box.center,
                      System.IO.Path.Combine(shots, $"stone-{palette}.png"));

                sheet.AppendLine($"[Stone] {palette}: photographed");
                Object.DestroyImmediate(piece);
            }

            Object.DestroyImmediate(sun);

            sheet.AppendLine($"[Stone] pictures in {shots}");
            Debug.Log(sheet.ToString());
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Stone camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.62f, 0.70f);

            var rt = new RenderTexture(800, 800, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(800, 800, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
