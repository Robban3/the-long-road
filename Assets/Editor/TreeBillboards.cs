using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TheVeil.Editor
{
    /// <summary>
    /// A tree baked into four pictures and eight triangles: `The Veil &gt; Bake Tree
    /// Billboards`.
    ///
    /// <b>Because the mesh route is finished and this project has the measurements to say
    /// so.</b> `MeshDecimator` cuts by collapsing edges, and an edge needs two triangles
    /// sharing it. A Synty canopy is a few thousand two-triangle leaf cards that touch
    /// nothing - `The Veil &gt; Mesh Shells` counts 9 885 of them in a meadow tree, 89,2
    /// per cent of the mesh - so there is no edge in any of it, and the only collapsible
    /// edges in the model belong to the trunk. Asked for a budget the decimator therefore
    /// eats the trunk and leaves the leaves standing in the air. Two ways of splitting the
    /// budget were tried and photographed; neither put a trunk back. A mesh with 9 885
    /// loose cards cannot be a 900-face tree.
    ///
    /// So the far level stops being a mesh. Four flat blades in a cross, one facing each
    /// way, each carrying a photograph of the tree taken from that side: eight triangles
    /// against twelve thousand seven hundred, and a silhouette that is the tree's own
    /// because it was taken from it.
    ///
    /// <b>What this does not do.</b> It does not light the tree - the picture carries the
    /// light it was baked under, and a billboard that was baked at noon stays at noon
    /// while the mesh beside it does not. The chapters here are lit differently, which is
    /// the thing to measure next and the reason the blades are hung only where the model
    /// is small: at four hundred pixels and below the difference is a few shades across a
    /// shape a finger-width wide. It does not turn to face the camera either. A billboard
    /// that turns needs a script on every copy, and this game's camera stays at one
    /// height and swings around the country - four fixed blades answer that without a
    /// single Update.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.TreeBillboards.Bake
    /// </summary>
    public static class TreeBillboards
    {
        /// <summary>Where the baked pictures and blades are written.</summary>
        const string Dir = "Assets/_Project/Models/Billboards";

        /// <summary>How many pixels each side of the tree is baked at.</summary>
        // Five hundred and twelve, in an atlas of four. The blade is drawn when the model
        // is four hundred pixels tall at most, so this is more than the screen will ever
        // ask of it and leaves room for the mip chain to do the shrinking properly.
        const int Side = 512;

        /// <summary>The four directions a blade is baked and hung for.</summary>
        static readonly Vector3[] Ways =
        {
            Vector3.forward, Vector3.right, Vector3.back, Vector3.left,
        };

        /// <summary>The trees this is done for.</summary>
        // The ones measured: both meadow trees, the two large ones, and the turned
        // variants that share their meshes. Not every tree in the packs - a pine is
        // needles on a connected branch and the decimator does fine by it, which Mesh
        // Shells says and this list obeys.
        static readonly string[] Trees =
        {
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Meadow_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Meadow_02.prefab",

            // The turned variant is its own prefab with its own name, so it needs its own
            // blades - the first build boarded the two above and left
            // `SM_Env_Tree_Meadow_01_Turned` standing in the shape rule's list, which is
            // how this was noticed rather than assumed.
            //
            // <b>`SM_Env_Tree_Meadow_02_Turned` and `_Late` were in this list and are not
            // any more, because nothing in the game points at them.</b> They came back
            // "clean" from the check that no boarded tree was left in the shape rule's
            // list - clean because the pass had never touched them, and it had never
            // touched them because the dressing never asks for them. Two atlases baked for
            // two trees that are never drawn. Absence from a sheet is evidence only when
            // the sheet would have named the thing, and the positive check - blades in the
            // prefab, blades at the end of the chain - is what caught it.
            "Assets/_Project/Prefabs/Plains/SM_Env_Tree_Meadow_01_Turned.prefab",

            // And the enchanted wood's own, which the shape rule had already named: both
            // large trees lose their whole outline at the first rung, and the portal tree
            // and the trunk lose most of it.
            "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/Prefabs/SM_Env_Tree_Large_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/Prefabs/SM_Env_Tree_Large_02.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/Prefabs/SM_Env_Tree_Trunk_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/Prefabs/SM_Env_Tree_Portal_01.prefab",
        };

        [MenuItem("The Veil/Bake Tree Billboards")]
        public static void Bake()
        {
            System.IO.Directory.CreateDirectory(Dir);

            var sheet = new StringBuilder();
            sheet.AppendLine("[Board] a tree baked into four pictures and eight triangles");

            int done = 0;

            foreach (string path in Trees)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    sheet.AppendLine($"[Board] {path}: not there");
                    continue;
                }

                if (Baked(prefab, sheet)) done++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sheet.AppendLine($"[Board] {done} of {Trees.Length} baked into {Dir}");
            Debug.Log(sheet.ToString());
        }

        /// <summary>One tree: four pictures into an atlas, a cross of blades, a material.</summary>
        static bool Baked(GameObject prefab, StringBuilder sheet)
        {
            var stood = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (stood == null) return false;

            var lamp = new GameObject("Bake light");

            // <b>The picture must not carry whatever the editor's scene was left in.</b>
            // The first two bakes came out with red speckles across the shaded side, and
            // then blue ones when the light moved - which is not the model and not the
            // light but the ambient and the sky reflection the shader falls back on where
            // the light does not reach. A baked picture that carries them is a tree with
            // somebody else's weather in it, and it would change the next time this ran.
            var wasAmbient = RenderSettings.ambientMode;
            var wasColour = RenderSettings.ambientLight;
            var wasProbe = RenderSettings.defaultReflectionMode;
            var wasCustom = RenderSettings.customReflectionTexture;

            try
            {
                RenderSettings.ambientMode =
                    UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.47f);
                RenderSettings.defaultReflectionMode =
                    UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = null;
                // The richest level, which is what the tree looks like when it is looked
                // at. Baking the author's far level instead would bake a simplification of
                // a simplification.
                var group = stood.GetComponent<LODGroup>();
                var levels = group != null ? group.GetLODs() : null;
                if (levels == null || levels.Length == 0) return false;

                foreach (var one in group.GetComponentsInChildren<LODGroup>(true))
                    one.enabled = false;

                var wanted = new HashSet<Renderer>();
                foreach (var renderer in levels[0].renderers)
                    if (renderer != null) wanted.Add(renderer);

                foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = wanted.Contains(renderer);

                var box = Around(stood);
                if (box.size.y <= 0.001f) return false;

                // A light of our own, so the picture does not carry whatever the scene was
                // left in. Straight down the view and a little to the side, which is the
                // shape a person reads a tree by.
                var sun = lamp.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1f;

                var atlas = new Texture2D(Side * 2, Side * 2, TextureFormat.RGBA32, false);

                for (int way = 0; way < Ways.Length; way++)
                {
                    // <b>The light turns with the camera, and the first bake proved why.</b>
                    // Stood still, it lit one side of the tree and left the opposite blade
                    // in its own shadow - four pictures of four different afternoons, which
                    // on four blades in a cross is a tree that changes brightness as the
                    // camera swings around it. Hung relative to each view, every blade is
                    // the same tree in the same light.
                    lamp.transform.rotation =
                        Quaternion.LookRotation(Ways[way], Vector3.up)
                        * Quaternion.Euler(50f, -30f, 0f);

                    var shot = Shot(box, Ways[way]);
                    if (shot == null) return false;

                    atlas.SetPixels((way % 2) * Side, (way / 2) * Side, Side, Side,
                                    shot.GetPixels());

                    Object.DestroyImmediate(shot);
                }

                atlas.Apply();

                string stem = $"{Dir}/{prefab.name}_Board";
                System.IO.File.WriteAllBytes($"{stem}.png", atlas.EncodeToPNG());
                Object.DestroyImmediate(atlas);

                AssetDatabase.ImportAsset($"{stem}.png", ImportAssetOptions.ForceUpdate);
                Read($"{stem}.png");

                var blades = Blades(box);
                AssetDatabase.CreateAsset(blades, $"{stem}.asset");

                Painted(stem);

                sheet.AppendLine($"[Board] {prefab.name,-34} {box.size.x:0.0} x "
                                 + $"{box.size.y:0.0} x {box.size.z:0.0} m, "
                                 + $"{blades.triangles.Length / 3} triangles, "
                                 + $"{Side * 2}x{Side * 2} picture");

                return true;
            }
            finally
            {
                RenderSettings.ambientMode = wasAmbient;
                RenderSettings.ambientLight = wasColour;
                RenderSettings.defaultReflectionMode = wasProbe;
                RenderSettings.customReflectionTexture = wasCustom;

                Object.DestroyImmediate(lamp);
                Object.DestroyImmediate(stood);
            }
        }

        /// <summary>The box every renderer that is on stands inside.</summary>
        static Bounds Around(GameObject stood)
        {
            bool any = false;
            var box = new Bounds();

            foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!renderer.gameObject.activeInHierarchy) continue;

                if (!any) { box = renderer.bounds; any = true; }
                else box.Encapsulate(renderer.bounds);
            }

            return any ? box : new Bounds();
        }

        /// <summary>
        /// One side of the tree, orthographic, on nothing.
        ///
        /// <b>Orthographic because the blade is flat.</b> A perspective picture of a tree
        /// carries the convergence of the lens it was taken with, and pasting that onto a
        /// flat quad which is then seen through a different lens is a tree that leans. The
        /// blade is the tree's own size, so the picture has to be too.
        /// </summary>
        static Texture2D Shot(Bounds box, Vector3 way)
        {
            var eye = new GameObject("Bake camera");

            try
            {
                var camera = eye.AddComponent<Camera>();

                float tall = box.size.y;
                float wide = Mathf.Max(box.size.x, box.size.z);
                float span = Mathf.Max(tall, wide);

                camera.orthographic = true;
                camera.orthographicSize = span * 0.5f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = span * 4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

                camera.transform.position = box.center - way * span * 1.5f;
                camera.transform.rotation = Quaternion.LookRotation(way, Vector3.up);

                var rt = new RenderTexture(Side, Side, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                var shot = new Texture2D(Side, Side, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0, 0, Side, Side), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);

                return shot;
            }
            finally { Object.DestroyImmediate(eye); }
        }

        /// <summary>
        /// Four one-sided blades in a cross, each with its own quarter of the atlas.
        ///
        /// One-sided and four of them rather than two double-sided: a double-sided quad
        /// shows the same picture from in front and behind, so a tree's left side appears
        /// on its right. Four costs four more triangles and nothing else.
        /// </summary>
        static Mesh Blades(Bounds box)
        {
            float tall = box.size.y;
            float wide = Mathf.Max(box.size.x, box.size.z);
            float span = Mathf.Max(tall, wide);

            // The picture is square and holds the whole model, so the blade is square at
            // the same size and sits where the model sits inside it.
            float half = span * 0.5f;
            float middle = box.center.y;

            var points = new List<Vector3>();
            var mapped = new List<Vector2>();
            var corners = new List<int>();

            for (int way = 0; way < Ways.Length; way++)
            {
                var ahead = Ways[way];
                var right = Vector3.Cross(Vector3.up, ahead).normalized;

                int first = points.Count;

                points.Add(-right * half + Vector3.up * (middle - half));
                points.Add(right * half + Vector3.up * (middle - half));
                points.Add(right * half + Vector3.up * (middle + half));
                points.Add(-right * half + Vector3.up * (middle + half));

                float u = (way % 2) * 0.5f, v = (way / 2) * 0.5f;

                mapped.Add(new Vector2(u, v));
                mapped.Add(new Vector2(u + 0.5f, v));
                mapped.Add(new Vector2(u + 0.5f, v + 0.5f));
                mapped.Add(new Vector2(u, v + 0.5f));

                // Wound so the face looks back along the direction it was baked from.
                corners.Add(first); corners.Add(first + 2); corners.Add(first + 1);
                corners.Add(first); corners.Add(first + 3); corners.Add(first + 2);
            }

            var mesh = new Mesh { name = "Board" };
            mesh.SetVertices(points);
            mesh.SetUVs(0, mapped);
            mesh.SetTriangles(corners, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// The material the blades are painted with.
        ///
        /// <b>Lit, although the picture already carries light, and that is a choice with a
        /// cost.</b> Unlit would show exactly what was baked and would ignore the chapter
        /// it stands in - a tree in the snow lit like a tree in the meadow, which on a
        /// hillside of them reads immediately. Lit puts the chapter's own sun on top of
        /// the sun that is already in the picture, so the far trees are slightly flatter
        /// and slightly brighter than the near ones. At four hundred pixels and below that
        /// is a few shades; a tree that does not know whether it is night is not. Worth a
        /// photograph in a dark chapter before it is called settled.
        ///
        /// Cut out rather than blended, because a blended leaf needs sorting and a
        /// thousand sorted trees is a different performance problem from the one this is
        /// here to solve.
        /// </summary>
        static void Painted(string stem)
        {
            var picture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{stem}.png");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            var paint = new Material(shader) { name = "Board" };

            paint.SetTexture("_BaseMap", picture);
            paint.SetFloat("_Smoothness", 0f);
            paint.SetFloat("_AlphaClip", 1f);
            paint.SetFloat("_Cutoff", 0.5f);
            paint.EnableKeyword("_ALPHATEST_ON");
            paint.renderQueue = 2450;

            // Two-sided, so a blade seen from behind is the tree and not a hole. The four
            // blades face outwards and the two nearest the camera are what is seen; the
            // far two show their backs through the leaves of the near ones.
            paint.SetFloat("_Cull", 0f);

            AssetDatabase.CreateAsset(paint, $"{stem}.mat");
        }

        /// <summary>The picture imported the way a cut-out wants to be.</summary>
        static void Read(string path)
        {
            var how = AssetImporter.GetAtPath(path) as TextureImporter;
            if (how == null) return;

            how.alphaIsTransparency = true;
            how.alphaSource = TextureImporterAlphaSource.FromInput;
            how.wrapMode = TextureWrapMode.Clamp;
            how.mipmapEnabled = true;
            how.textureCompression = TextureImporterCompression.Compressed;

            how.SaveAndReimport();
        }
    }
}
