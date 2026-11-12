using System.Collections.Generic;
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


        /// <summary>
        /// Whether every renderer in a rebuilt prefab belongs to a level of its LOD group:
        /// `The Veil &gt; Chain Check`.
        ///
        /// <b>A renderer that no level lists is drawn at every distance.</b> The engine
        /// switches levels by drawing the chosen one, and anything outside the group is
        /// not part of that choice - so an orphan is a mesh the chain cannot put away, and
        /// a count of skinned meshes that went up after a pass meant to bring it down.
        /// </summary>
        [MenuItem("The Veil/Chain Check")]
        public static void Chains()
        {
            var sheet = new System.Text.StringBuilder();
            sheet.AppendLine("[Chain] renderers each prefab has, and how many its LOD group claims");

            int loose = 0, looked = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // <b>Every group in the hierarchy, not just the one on the root.</b> A
                // prefab may hold another prefab that carries its own chain - the noble
                // cavalry is a knight on a horse - and BudgetReport resolves ownership by
                // the deepest group that lists a renderer. A check that reads only the
                // root's group calls those renderers orphans when they are governed
                // perfectly well, which is a tool inventing a defect.
                var groups = prefab.GetComponentsInChildren<LODGroup>(true);
                if (groups.Length == 0) continue;

                var claimed = new System.Collections.Generic.HashSet<Renderer>();

                foreach (var group in groups)
                    foreach (var level in group.GetLODs())
                    {
                        if (level.renderers == null) continue;
                        foreach (var renderer in level.renderers)
                            if (renderer != null) claimed.Add(renderer);
                    }

                int all = 0, orphans = 0;

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !renderer.gameObject.activeSelf) continue;

                    all++;
                    if (!claimed.Contains(renderer)) orphans++;
                }

                looked++;

                if (orphans > 0)
                {
                    loose++;
                    sheet.AppendLine($"[Chain]   {prefab.name,-40} {all,3} renderers, "
                                     + $"{claimed.Count,3} claimed, {orphans,3} ORPHANED");

                    foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer == null || !renderer.gameObject.activeSelf) continue;
                        if (claimed.Contains(renderer)) continue;

                        sheet.AppendLine($"[Chain]       {renderer.name,-36} "
                                         + $"{renderer.GetType().Name}, parent "
                                         + $"{(renderer.transform.parent != null ? renderer.transform.parent.name : "-")}");
                    }
                }
            }

            sheet.AppendLine($"[Chain] {looked} prefabs carry a group; {loose} of them have "
                             + "renderers no level lists");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// How many renderers each level of each chain in the cast holds:
        /// `The Veil &gt; Level Widths`.
        ///
        /// <b>The question the far-LOD pass left open.</b> The pass took 421 823 triangles
        /// off the cast and put the skinned-mesh count up, 160 in frame to 211. A chain
        /// changes which mesh a renderer draws and not how many renderers there are, so a
        /// rise can only mean the rebuilt chains hold more renderers at distance than the
        /// hand-made ones they replaced - and <see cref="FarLods"/> builds every generated
        /// level from LOD0's renderers, one thin renderer each, while stripping any
        /// hand-made level below 0.055 of the screen. If a pack's own far level merged the
        /// model into fewer renderers, that is the trade, in writing.
        ///
        /// It counts skinned renderers separately, because that is the budget line: the
        /// engine skins what it draws, and the count at the bottom of the chain is what a
        /// figure two hundred metres away costs.
        ///
        /// Headless: unity run . -- -executeMethod TheVeil.Editor.RigProbe.Widths
        /// </summary>
        [MenuItem("The Veil/Level Widths")]
        public static void Widths()
        {
            var sheet = new System.Text.StringBuilder();
            sheet.AppendLine("[Width] renderers per level, for every prefab in the project "
                             + "that holds a skinned renderer");
            sheet.AppendLine("[Width] a level marked gen was built by FarLods from LOD0");

            int chains = 0, widened = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0) continue;

                var groups = prefab.GetComponentsInChildren<LODGroup>(true);
                if (groups.Length == 0) continue;

                chains++;
                sheet.AppendLine($"[Width] {prefab.name}");

                foreach (var group in groups)
                {
                    var levels = group.GetLODs();
                    int first = -1, last = -1;

                    for (int lod = 0; lod < levels.Length; lod++)
                    {
                        int all = 0, skins = 0, generated = 0;

                        if (levels[lod].renderers != null)
                            foreach (var renderer in levels[lod].renderers)
                            {
                                if (renderer == null) continue;

                                all++;
                                if (renderer is SkinnedMeshRenderer) skins++;
                                if (renderer.name.StartsWith("Far LOD ")) generated++;
                            }

                        if (lod == 0) first = skins;
                        last = skins;

                        sheet.AppendLine($"[Width]     {group.name,-28} LOD{lod} "
                                         + $"below {levels[lod].screenRelativeTransitionHeight,6:0.000} "
                                         + $"{all,3} renderers, {skins,3} skinned"
                                         + (generated > 0 ? $"  ({generated} gen)" : ""));
                    }

                    // The one comparison that matters: a chain whose bottom rung skins as
                    // many meshes as its top rung saves triangles and no skinning at all.
                    if (levels.Length > 1 && last >= first && first > 0)
                    {
                        widened++;
                        sheet.AppendLine($"[Width]     {group.name,-28} bottom rung skins "
                                         + $"{last} against {first} at the top - NO SAVING");
                    }
                }
            }

            sheet.AppendLine($"[Width] {chains} skinned prefabs carry a chain; {widened} of "
                             + "their groups skin as many meshes at the bottom as at the top");

            // <b>Every prefab, not just the skinned ones, for the one defect that is not a
            // budget line.</b> A level that lists no renderers draws nothing, so the model
            // disappears below that threshold rather than getting cheaper. Six of these
            // were found on nested knights after the cast pass and none existed before it,
            // which is a thing worth counting over the whole project every time.
            int empty = 0, blank = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                foreach (var group in prefab.GetComponentsInChildren<LODGroup>(true))
                {
                    var levels = group.GetLODs();
                    bool any = false;

                    for (int lod = 0; lod < levels.Length; lod++)
                    {
                        int all = 0;

                        if (levels[lod].renderers != null)
                            foreach (var renderer in levels[lod].renderers)
                                if (renderer != null) all++;

                        if (all > 0) continue;

                        empty++;
                        any = true;

                        sheet.AppendLine($"[Width] EMPTY {prefab.name} / {group.name} LOD{lod} "
                                         + $"below {levels[lod].screenRelativeTransitionHeight:0.000}"
                                         + " draws nothing");
                    }

                    if (any) blank++;
                }
            }

            sheet.AppendLine($"[Width] {empty} levels in {blank} groups list no renderers at "
                             + "all, over every prefab in the project");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// What each level of a scenery chain actually costs: `The Veil &gt; Level Costs`.
        ///
        /// <b>Written because the heaviest thing in the game is a hand-made LOD1.</b>
        /// After <see cref="FarLods"/> stopped emptying levels, the top of the frame was
        /// SM_Env_Tree_Meadow_01_LOD1 at 923 400 triangles, 27,3 per cent of the worst
        /// frame on its own - and five times what the same tree's LOD0 cost in that frame.
        /// A level that costs more than the one above it is not a simplification.
        ///
        /// It survives because <see cref="FarLods"/> spares any hand-made level above
        /// 0,055 of the screen, on the reasoning that a pack's own LOD0 and LOD1 are worth
        /// keeping. That reasoning is a rule standing in for a measurement. This is the
        /// measurement: faces per level and the threshold each level covers, so the
        /// question "is this level cheaper than the rung that would replace it" has a
        /// number rather than an assumption behind it.
        ///
        /// <b>Scope, so nobody reads it for more than it says.</b> This counts the asset,
        /// not the frame. A heavy level costs nothing until something stands in its band,
        /// and how many stand there is <see cref="BudgetReport"/>'s question, not this
        /// one. Read the ranking here as what could be expensive, and that report as what
        /// is.
        ///
        /// Headless: unity run . -- -executeMethod TheVeil.Editor.RigProbe.Costs
        /// </summary>
        [MenuItem("The Veil/Level Costs")]
        public static void Costs()
        {
            // FarLods keeps a hand-made level above this share of the screen and cuts its
            // own rungs below it, at these face budgets. Both are copied rather than read
            // because they are private there, so a change to either wants a change here.
            const float KeepAbove = 0.055f;
            var rungs = new[] { 900, 250, 32 };

            var sheet = new System.Text.StringBuilder();
            sheet.AppendLine("[Cost] faces per level, for every prefab that holds a chain "
                             + "and no skinned renderer");
            sheet.AppendLine($"[Cost] a hand-made level above {KeepAbove:0.000} of the screen is "
                             + $"spared; the rungs cut below it cost {rungs[0]}, {rungs[1]} "
                             + $"and {rungs[2]} faces");
            sheet.AppendLine("[Cost] this is the asset and not the frame - a heavy level costs "
                             + "nothing until something stands in its band");

            // Every spared hand-made level, so the end of the sheet can rank them rather
            // than leave the reading to whoever scrolls furthest.
            var spared = new System.Collections.Generic.List<(int Faces, float Below, float Share, string Where)>();

            int chains = 0, levelsSeen = 0, heavy = 0, heavyFaces = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // The skinned chains have their own sheet in Level Widths, and the cast is
                // a different argument: an animal's levels are cheap already.
                if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0) continue;

                var groups = prefab.GetComponentsInChildren<LODGroup>(true);
                if (groups.Length == 0) continue;

                chains++;
                bool said = false;

                foreach (var group in groups)
                {
                    var levels = group.GetLODs();
                    int above = 0;

                    for (int lod = 0; lod < levels.Length; lod++)
                    {
                        int all = 0, faces = 0, generated = 0;

                        if (levels[lod].renderers != null)
                            foreach (var renderer in levels[lod].renderers)
                            {
                                if (renderer == null) continue;

                                all++;
                                faces += FarLods.Faces(renderer);
                                if (renderer.name.StartsWith("Far LOD ")) generated++;
                            }

                        levelsSeen++;

                        bool mine = all > 0 && generated == all;
                        float below = levels[lod].screenRelativeTransitionHeight;

                        // The comparison that says whether a level is a simplification at
                        // all. A ratio near one is the same model drawn again.
                        string drop = above > 0 && lod > 0
                            ? $"{(float)faces / above,6:0.00}x of the one above"
                            : "              ";

                        bool fat = !mine && lod > 0 && below > KeepAbove && faces > rungs[0];

                        if (fat)
                        {
                            heavy++;
                            heavyFaces += faces;
                            spared.Add((faces, below, (float)faces / System.Math.Max(above, 1),
                                        $"{prefab.name} / {group.name} LOD{lod}"));
                        }

                        if (!said) { sheet.AppendLine($"[Cost] {prefab.name}"); said = true; }

                        sheet.AppendLine($"[Cost]     {group.name,-26} LOD{lod} "
                                         + $"below {below,6:0.000} {all,3} renderers "
                                         + $"{faces,8} faces  {drop}"
                                         + (mine ? "  gen" : "     ")
                                         + (fat ? "  SPARED AND HEAVY" : ""));

                        above = faces;
                    }
                }
            }

            spared.Sort((a, b) => b.Faces.CompareTo(a.Faces));

            sheet.AppendLine("[Cost] the heaviest spared hand-made levels, worst first");

            for (int i = 0; i < spared.Count && i < 30; i++)
                sheet.AppendLine($"[Cost]  {i + 1,3}  {spared[i].Faces,8} faces  "
                                 + $"below {spared[i].Below:0.000}  "
                                 + $"{spared[i].Share:0.00}x of the one above  {spared[i].Where}");

            sheet.AppendLine($"[Cost] {chains} unskinned prefabs carry a chain, {levelsSeen} levels "
                             + $"between them");
            sheet.AppendLine($"[Cost] {heavy} hand-made levels are spared although they cost more "
                             + $"than the {rungs[0]}-face rung that would stand there, "
                             + $"{heavyFaces} faces in all");

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

        /// <summary>
        /// What a mesh is actually made of, in pieces that touch: `The Veil &gt; Mesh Shells`.
        ///
        /// <b>Because the decimator's failure on a tree is a fact about the asset and
        /// nobody had counted it.</b> `MeshDecimator` works by collapsing edges, and an
        /// edge can only be collapsed if the triangles around it share it. A Synty canopy
        /// is a few thousand separate leaf cards - two triangles each, touching nothing -
        /// so there is no edge in them to collapse at all, and the only collapsible edges
        /// in the mesh are the trunk's. Asked for any budget the tool therefore eats the
        /// trunk and leaves every leaf where it was. That was read off photographs; this
        /// is the same thing as a number, and it says whether dropping whole cards could
        /// ever reach a budget.
        ///
        /// Welded by position first, the way the decimator welds, because a shell written
        /// as two halves at a seam is one shell to it.
        ///
        /// Headless: unity run . -- -executeMethod TheVeil.Editor.RigProbe.Shells
        /// </summary>
        [MenuItem("The Veil/Mesh Shells")]
        public static void Shells()
        {
            // The two trees this started with, a bush and a fern for the same canopy
            // question at another size, and a cube - whose answer is known, which is how
            // this sheet is checked before anything is read off it.
            string[] looked =
            {
                "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Meadow_01.prefab",
                "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Meadow_02.prefab",
                "Assets/3DreaMax Studio/003_MDVL_WagonsCartsCarriages_Vol_1/Prefabs/Covered_Wagon/SM_Covered_Wagon_Full.prefab",
            };

            var sheet = new StringBuilder();
            sheet.AppendLine("[Shell] what each mesh is made of, in pieces that touch after "
                             + "welding by position");
            sheet.AppendLine("[Shell] an edge can only be collapsed if two triangles share "
                             + "it, so faces in two-triangle shells are faces no decimator "
                             + "can simplify - it can only delete them whole");

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                var mesh = box.GetComponent<MeshFilter>().sharedMesh;
                Count(mesh, "a cube, whose answer is known", sheet);
            }
            finally { Object.DestroyImmediate(box); }

            foreach (string path in looked)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    sheet.AppendLine($"[Shell] {path}: not there");
                    continue;
                }

                var group = prefab.GetComponent<LODGroup>();
                var levels = group != null ? group.GetLODs() : null;
                if (levels == null || levels.Length == 0) continue;

                foreach (var renderer in levels[0].renderers)
                {
                    if (renderer == null) continue;

                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;

                    Count(filter.sharedMesh, $"{prefab.name} / {renderer.name}", sheet);

                    // <b>And what the decimator says it did to it.</b> The covered wagon
                    // comes back from an eighty-face budget at 25 152 faces against a
                    // source of 25 164 and nobody knows why; it is nought per cent loose
                    // cards, so it is not the tree's fault. The tool's own account of the
                    // run is the only thing in the project that can say whether it ran out
                    // of edges, refused every collapse as a flip, or never started.
                    MeshDecimator.Simplify(filter.sharedMesh, 80, out string story);
                    sheet.AppendLine($"[Shell]     asked for 80: {story}");
                }
            }

            Debug.Log(sheet.ToString());
        }

        /// <summary>One mesh's shells, counted and ranked.</summary>
        static void Count(Mesh mesh, string what, StringBuilder sheet)
        {
            const float Same = 0.0001f;

            var points = mesh.vertices;
            var corners = mesh.triangles;

            // Welded by position, so a seam written as two vertices is one point.
            var slot = new Dictionary<Vector3Int, int>(points.Length);
            var index = new int[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                var at = new Vector3Int(Mathf.RoundToInt(points[i].x / Same),
                                        Mathf.RoundToInt(points[i].y / Same),
                                        Mathf.RoundToInt(points[i].z / Same));

                if (!slot.TryGetValue(at, out int found)) { found = slot.Count; slot[at] = found; }
                index[i] = found;
            }

            // Union-find over the welded points, joined by every triangle.
            var owner = new int[slot.Count];
            for (int i = 0; i < owner.Length; i++) owner[i] = i;

            int Root(int i) { while (owner[i] != i) { owner[i] = owner[owner[i]]; i = owner[i]; } return i; }
            void Join(int a, int b) { a = Root(a); b = Root(b); if (a != b) owner[a] = b; }

            for (int t = 0; t + 2 < corners.Length; t += 3)
            {
                Join(index[corners[t]], index[corners[t + 1]]);
                Join(index[corners[t + 1]], index[corners[t + 2]]);
            }

            var size = new Dictionary<int, int>();

            for (int t = 0; t + 2 < corners.Length; t += 3)
            {
                int root = Root(index[corners[t]]);
                size.TryGetValue(root, out int had);
                size[root] = had + 1;
            }

            int faces = corners.Length / 3;
            int tiny = 0, tinyFaces = 0, biggest = 0;

            foreach (var one in size)
            {
                if (one.Value <= 2) { tiny++; tinyFaces += one.Value; }
                if (one.Value > biggest) biggest = one.Value;
            }

            sheet.AppendLine($"[Shell] {what,-52} {faces,7} faces in {size.Count,6} shells; "
                             + $"largest {biggest,7} faces; {tiny,6} shells of two triangles "
                             + $"or fewer, {tinyFaces,7} faces "
                             + $"({(faces > 0 ? 100f * tinyFaces / faces : 0f),5:0.0}% of the "
                             + "mesh a decimator can only delete whole)");
        }

        /// <summary>
        /// What pose the cast prefabs stand in on disk, near rung and far rung both:
        /// `The Veil &gt; Rest Poses`.
        ///
        /// <b>Because a frozen pose is only worth having if the pose is worth freezing.</b>
        /// The far rungs of the cast carry SkinnedMeshRenderers holding two dozen faces
        /// each, and the way to take them off the skinned-mesh line is to bake them in a
        /// pose and hang a plain MeshRenderer instead. That is only safe if the pose on
        /// disk is a pose, and most rigs are authored with the arms straight out.
        ///
        /// <b>Read the width in metres, not the ratio.</b> The first version of this
        /// printed widest-horizontal over height and called anything over 0,8 arms out -
        /// which flagged every deer, wolf, fox, fish and shark in the project, because a
        /// quadruped is as long as it is tall and always was. The ratio measures boxiness.
        /// What catches a rig standing in a cross is the absolute width of a thing about
        /// 1,9 m tall: half a metre is a figure, a metre and a half is a cross.
        ///
        /// <b>And the far rung is measured as well as the near one</b>, because a mesh
        /// decimated to two dozen faces may have lost the arms that made the pose a
        /// problem - an assumption this tool made in a comment for one run before it was
        /// asked to measure it.
        ///
        /// Headless: unity run . -- -executeMethod TheVeil.Editor.RigProbe.Poses
        /// </summary>
        [MenuItem("The Veil/Rest Poses")]
        public static void Poses()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Pose] every prefab with a skinned renderer, baked in the pose "
                             + "it is saved in, box measured");
            sheet.AppendLine("[Pose] near is the chain's top rung, far is its last; width in "
                             + "metres is the reading, and about 0,5 m on a 1,9 m figure is a "
                             + "figure standing");

            int seen = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0) continue;

                var stood = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (stood == null) continue;

                try
                {
                    var near = new List<SkinnedMeshRenderer>();
                    var far = new List<SkinnedMeshRenderer>();

                    foreach (var skin in stood.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (!skin.name.StartsWith("Far LOD ")) near.Add(skin);

                    // The chain's own last rung, asked of the group rather than guessed
                    // from the name: a model may carry generated pieces that no level
                    // lists any more, and those are not what gets drawn.
                    foreach (var group in stood.GetComponentsInChildren<LODGroup>(true))
                    {
                        var levels = group.GetLODs();
                        if (levels.Length == 0) continue;

                        var last = levels[levels.Length - 1];
                        if (last.renderers == null) continue;

                        foreach (var renderer in last.renderers)
                            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                                far.Add(skin);
                    }

                    if (!Box(near, out var nearBox, out int nearFaces)) continue;

                    seen++;

                    bool hasFar = Box(far, out var farBox, out int farFaces);

                    sheet.AppendLine($"[Pose] {prefab.name,-40} "
                                     + $"near {near.Count,2} skinned {nearFaces,6} faces "
                                     + $"{Mathf.Max(nearBox.size.x, nearBox.size.z),5:0.00} wide "
                                     + $"{nearBox.size.y,5:0.00} tall   "
                                     + (hasFar
                                        ? $"far {far.Count,2} skinned {farFaces,6} faces "
                                          + $"{Mathf.Max(farBox.size.x, farBox.size.z),5:0.00} wide "
                                          + $"{farBox.size.y,5:0.00} tall"
                                        : "far  - no skinned last rung"));
                }
                finally { Object.DestroyImmediate(stood); }
            }

            sheet.AppendLine($"[Pose] {seen} prefabs baked");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// The world box of a set of skinned renderers baked in the pose they stand in,
        /// and the faces they hold between them.
        /// </summary>
        static bool Box(List<SkinnedMeshRenderer> skins, out Bounds box, out int faces)
        {
            box = new Bounds();
            faces = 0;

            bool any = false;

            foreach (var skin in skins)
            {
                if (skin.sharedMesh == null) continue;

                var baked = new Mesh();

                try
                {
                    // <b>Baked with the scale, and then turned and not moved.</b> BakeMesh
                    // with useScale applies the renderer world scale itself, so putting
                    // the result through the transform as well multiplies by it twice -
                    // which is how this tool first reported a deer whose far rung was a
                    // box one centimetre across. Baking without the scale and
                    // transforming is not the fix either: these packs are imported at a
                    // hundredth on an ancestor and the figure came out at 0,01 m both
                    // ways. Rotation alone is what is left, and it is all the box needs:
                    // the extents are wanted on the world axes and the position is not
                    // wanted at all.
                    skin.BakeMesh(baked, true);
                    if (baked.vertexCount == 0) continue;

                    faces += baked.triangles.Length / 3;

                    var local = baked.bounds;

                    foreach (var corner in Corners(local))
                    {
                        var at = skin.transform.rotation * corner;

                        if (!any) { box = new Bounds(at, Vector3.zero); any = true; }
                        else box.Encapsulate(at);
                    }
                }
                finally { Object.DestroyImmediate(baked); }
            }

            return any;
        }

        /// <summary>The eight corners of a box, which Bounds does not hand out.</summary>
        static Vector3[] Corners(Bounds box)
        {
            var low = box.min;
            var high = box.max;

            return new[]
            {
                new Vector3(low.x, low.y, low.z), new Vector3(high.x, low.y, low.z),
                new Vector3(low.x, high.y, low.z), new Vector3(high.x, high.y, low.z),
                new Vector3(low.x, low.y, high.z), new Vector3(high.x, low.y, high.z),
                new Vector3(low.x, high.y, high.z), new Vector3(high.x, high.y, high.z),
            };
        }

        /// <summary>
        /// Whether a generated far rung's skinning survived the decimator:
        /// `The Veil &gt; Rung Weights`.
        ///
        /// <b>Written because Rest Poses printed a deer's far rung as a box one centimetre
        /// across.</b> A skinned renderer's bounds are copied from the near one by the pass
        /// that builds it, so a rung whose vertices all collapse onto one bone's origin
        /// reports a deer-sized box to every count in the project and draws a dot. Nothing
        /// that reads bounds can see that, and nothing that reads the mesh un-posed can
        /// either: the mesh asset is the right shape, and it is the weights that are
        /// wrong.
        ///
        /// Headless: unity run . -- -executeMethod TheVeil.Editor.RigProbe.Weights
        /// </summary>
        [MenuItem("The Veil/Rung Weights")]
        public static void Weights()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Weight] every generated skinned far rung: the mesh un-posed, "
                             + "the mesh baked in the rest pose, and what it is bound to");

            int seen = 0, collapsed = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0) continue;

                var stood = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (stood == null) continue;

                try
                {
                    foreach (var skin in stood.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (!skin.name.StartsWith("Far LOD ")) continue;

                        var mesh = skin.sharedMesh;
                        if (mesh == null) continue;

                        seen++;

                        var baked = new Mesh();
                        Vector3 bakedSize;

                        try
                        {
                            skin.BakeMesh(baked, true);
                            bakedSize = baked.vertexCount > 0 ? baked.bounds.size : Vector3.zero;
                        }
                        finally { Object.DestroyImmediate(baked); }

                        var weights = mesh.GetAllBoneWeights();
                        float heaviest = 0f;

                        for (int i = 0; i < weights.Length; i++)
                            if (weights[i].weight > heaviest) heaviest = weights[i].weight;

                        bool bad = bakedSize.magnitude < 0.05f * mesh.bounds.size.magnitude;
                        if (bad) collapsed++;

                        sheet.AppendLine($"[Weight] {prefab.name,-34} {skin.name,-14} "
                                         + $"mesh {mesh.bounds.size.magnitude,6:0.00} m  "
                                         + $"baked {bakedSize.magnitude,6:0.00} m  "
                                         + $"{mesh.bindposes.Length,3} bindposes  "
                                         + $"{weights.Length,5} weights, heaviest "
                                         + $"{heaviest,4:0.00}  "
                                         + $"{skin.bones.Length,3} bones"
                                         + (bad ? "   COLLAPSED" : ""));
                    }
                }
                finally { Object.DestroyImmediate(stood); }
            }

            sheet.AppendLine($"[Weight] {seen} generated skinned far rungs, {collapsed} of "
                             + "them collapsing when posed");

            Debug.Log(sheet.ToString());
        }
    }
}
