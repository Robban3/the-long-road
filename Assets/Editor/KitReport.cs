using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The building kit's own pieces, photographed one at a time: `The Veil > Kit Report`.
    ///
    /// Written after four rounds of measuring the wrong thing. A house in the villages
    /// came out looking like two and three houses stacked, roof meeting floor; every
    /// measurement of *pairs* of buildings said they did not touch, which was true and
    /// useless, because the stack is inside one building. The way to see what a stack is
    /// made of is to look at what it is stacked from.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.KitReport.Run
    /// </summary>
    public static class KitReport
    {
        const string Dir = "Assets/Synty/PolygonKnights/Prefabs/Buildings/";

        static readonly string[] Pieces =
        {
            "SM_Bld_House_Foundation_01",
            "SM_Bld_House_Room_01",
            "SM_Bld_House_TopRoomSmall_01",
            "SM_Bld_House_RoomTop_01"
        };

        [MenuItem("The Veil/Kit Report")]
        public static void Run()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            var stage = new GameObject("Kit stage");
            float x = 0f;

            foreach (string name in Pieces)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + name + ".prefab");
                if (prefab == null)
                {
                    Debug.Log($"[Kit] {name}: not found.");
                    continue;
                }

                var piece = Object.Instantiate(prefab, stage.transform);
                piece.transform.position = new Vector3(x, 0f, 0f);

                var box = ModelScaling.Measure(piece);

                Debug.Log($"[Kit] {name}: {box.size.x:0.0} x {box.size.y:0.0} x {box.size.z:0.0} m, "
                          + $"foot at y {box.min.y:0.00}, top at y {box.max.y:0.0}.");

                x += 12f;
            }

            // And one built house beside them, to be compared against its own parts.
            var runner = Object.FindAnyObjectByType<TheVeil.App.LevelRunner>();
            Shoot(new Vector3(x * 0.5f - 6f, 9f, -34f), new Vector3(x * 0.5f - 6f, 4f, 0f),
                  System.IO.Path.Combine(shots, "kit-pieces.png"));

            Object.DestroyImmediate(stage);
            Debug.Log($"[Kit] picture in {shots}");
        }

        /// <summary>
        /// Every piece of the house kit, photographed alone and from the side.
        ///
        /// The first version of this stood four of them in a row twelve metres apart and
        /// looked at them from thirty — and at that distance every one of them read as a
        /// finished house with a red roof, which is what it was reported as and what the
        /// stacking was torn out for. A row at a distance is not an inspection. This walks
        /// them one at a time, close, square from the side, with the ground line in shot,
        /// so a flat top that is meant to carry a storey cannot be mistaken for a roof.
        /// </summary>
        [MenuItem("The Veil/Kit Pieces")]
        public static void Inspect()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            foreach (string name in Pieces)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + name + ".prefab");
                if (prefab == null) { Debug.Log($"[Kit] {name}: not found."); continue; }

                var piece = Object.Instantiate(prefab);
                var box = ModelScaling.Measure(piece);

                // Square from the side, at the height of the piece's own middle, close
                // enough that the top edge is unambiguous.
                float reach = Mathf.Max(box.size.x, box.size.z) * 2.2f;

                Shoot(box.center + new Vector3(0f, 0f, -reach),
                      box.center,
                      System.IO.Path.Combine(shots, "kit-" + name + ".png"));

                Debug.Log($"[Kit] {name}: {box.size.x:0.0} x {box.size.y:0.0} x {box.size.z:0.0} m, "
                          + $"foot {box.min.y:0.00}, top {box.max.y:0.0}.");

                Object.DestroyImmediate(piece);
            }

            // And the four of them stacked the way the pack's own names say they go, so
            // the assembly can be judged beside its parts.
            var runner = Object.FindAnyObjectByType<TheVeil.App.LevelRunner>();
            var kit = runner != null ? runner.Decor.Kit : null;

            if (kit != null && kit.CanBuildHouse)
            {
                var house = BuildingBuilder.House(null, kit, new DeterministicRandom(1), out _);

                if (house != null)
                {
                    var box = ModelScaling.Measure(house);
                    float reach = Mathf.Max(box.size.x, box.size.z) * 2.6f;

                    Shoot(box.center + new Vector3(0f, 0f, -reach), box.center,
                          System.IO.Path.Combine(shots, "kit-house.png"));

                    Debug.Log($"[Kit] assembled: {box.size.x:0.0} x {box.size.y:0.0} x {box.size.z:0.0} m.");
                    Object.DestroyImmediate(house);
                }
            }

            Debug.Log($"[Kit] pictures in {shots}");
        }

        /// <summary>
        /// Every paving piece, measured and photographed from above and from the side.
        ///
        /// Written after laying a street with the first three that looked right in a file
        /// listing, which produced a street of slabs sitting on the ground with their whole
        /// thickness proud of it, in the densest cobble the pack has. Both faults are
        /// answered by the same thing: look at each piece, and look at it from the side as
        /// well as from above, because from above a slab that is lying on the grass and one
        /// that is bedded into it are the same picture.
        /// </summary>
        [MenuItem("The Veil/Paving Report")]
        public static void Paving()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            string[] pieces =
            {
                "SM_Env_Path_Cobble_01", "SM_Env_Path_Cobble_02", "SM_Env_Path_Cobble_Stone_01",
                "SM_Env_Path_Stone_01", "SM_Env_Path_Stone_02", "SM_Env_Path_Stone_03",
                "SM_Env_Path_Tile_01", "SM_Env_Path_Tile_02", "SM_Env_Path_Tile_Corner_01",
                "SM_Env_Path_Dirt_01", "SM_Env_Path_Dirt_02", "SM_Env_Path_Dirt_03"
            };

            const string dir = "Assets/Synty/PolygonKnights/Prefabs/Environments/";

            foreach (string name in pieces)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dir + name + ".prefab");
                if (prefab == null) { Debug.Log($"[Paving] {name}: not found."); continue; }

                var piece = Object.Instantiate(prefab);
                var box = ModelScaling.Measure(piece);

                float across = Mathf.Max(box.size.x, box.size.z);

                // Straight down, so the pattern can be judged: how much stone, how much
                // ground between it.
                Shoot(box.center + new Vector3(0f, across * 1.6f, 0.01f), box.center,
                      System.IO.Path.Combine(shots, "paving-" + name + ".png"));

                Debug.Log($"[Paving] {name}: {box.size.x:0.00} x {box.size.z:0.00} m across, "
                          + $"{box.size.y:0.00} m thick, foot at y {box.min.y:0.00}.");

                Object.DestroyImmediate(piece);
            }

            Debug.Log($"[Paving] pictures in {shots}");
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Kit camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.42f, 0.5f);

            var rt = new RenderTexture(1400, 700, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1400, 700, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1400, 700), 0, 0);
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
