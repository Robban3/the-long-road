using System.Text;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The props a trap could be made of, measured and photographed one at a time:
    /// `The Veil > Trap Report`.
    ///
    /// A sprung trap is one of the two or three things in this game the player is meant to
    /// look *at* rather than past, and it has been a skull — one skull, blown up to nine
    /// tenths of a metre and tinted red, which is a skull the size of a sheep and the
    /// colour of nothing.
    ///
    /// Measured before it is placed, because this pack has caught the same mistake twice
    /// already: SM_Env_Path_Cobble_Stone_01 is a single 41 x 31 cm cobble and was being
    /// scaled to four metres, and the first paving went down lying on the grass with its
    /// whole thickness proud of it. A hatch over a pit has exactly those two failure modes
    /// — the wrong size, and sitting on the ground instead of in it — and both are
    /// invisible from above and obvious from the side.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.TrapReport.Run
    /// </summary>
    public static class TrapReport
    {
        [MenuItem("The Veil/Trap Report")]
        public static void Run()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            (string Path, string Name)[] candidates =
            {
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Wall_01.prefab", "wall"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Wood_Battlement_01.prefab", "hoarding"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Tower_01.prefab", "shaft"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Tower_Top_01.prefab", "parapet"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Tower_Mini_01.prefab", "turret"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Tower_Wall_Top_01.prefab", "walltop"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Tower_Base_01.prefab", "towerbase"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Pillar_01.prefab", "pillar"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Wall_Gate_01.prefab", "gatehouse"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Tower_Round_01.prefab", "drum"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Roof_Spire_01.prefab", "spire"),
                ("Assets/Synty/PolygonKnights/Prefabs/Buildings/SM_Bld_Castle_Flag_01.prefab", "banner"),
                ("Assets/Synty/PolygonKnights/Prefabs/Props/SM_Prop_Trapdoor_01.prefab", "trapdoor"),
                ("Assets/Synty/PolygonKnights/Prefabs/Props/SM_Prop_Grate_01.prefab", "grate"),
                ("Assets/Synty/PolygonKnights/Prefabs/Props/SM_Prop_Beam_01.prefab", "beam"),
                ("Assets/Synty/PolygonNature/Prefabs/Props/SM_Prop_Skeleton_Ground_01.prefab", "skeleton"),
                ("Assets/Synty/PolygonNature/Prefabs/Props/SM_Prop_Skull_01.prefab", "skull")
            };

            var sheet = new StringBuilder();
            sheet.AppendLine("[Trap] candidates, at the size the artist drew them");

            foreach (var (path, name) in candidates)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { sheet.AppendLine($"[Trap] {name}: not found at {path}"); continue; }

                var piece = Object.Instantiate(prefab);
                var box = ModelScaling.Measure(piece);

                float across = Mathf.Max(box.size.x, box.size.z);
                bool flat = box.size.y < across * 0.25f;

                sheet.AppendLine($"[Trap] {name}: {box.size.x:0.00} x {box.size.z:0.00} m across, "
                                 + $"{box.size.y:0.00} m thick, foot at y {box.min.y:0.00}"
                                 + (flat ? "  (flat — beds into the ground)" : ""));

                // From the side and a little above, which is the angle that shows whether
                // a thing is lying on the grass or set into it.
                //
                // Square to the long face, not to the end. Offsetting always along x put
                // the camera at the gable of anything that runs along x — the gatehouse
                // came out as a blank slab, because a 5.4 m wall seen end-on is 2.4 m of
                // plain stone. Whichever way the piece is shorter is the way to look at it.
                float back = Mathf.Max(across, box.size.y) * 1.8f;
                var eye = box.size.x >= box.size.z
                    ? box.center + new Vector3(0f, box.size.y * 0.35f, -back)
                    : box.center + new Vector3(back, box.size.y * 0.35f, 0f);

                Shoot(eye, box.center, System.IO.Path.Combine(shots, $"trap-{name}.png"));

                Object.DestroyImmediate(piece);
            }

            sheet.AppendLine($"[Trap] pictures in {shots}");
            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// A trap sign where it actually stands, from where the player actually sits:
        /// `The Veil > Trap Sign On A Level`.
        ///
        /// <b>Every judgement about these bones has been made from the wrong distance.</b>
        /// The pieces above are photographed a metre away, which is how a 41 cm skeleton
        /// gets called "life size, and that is the honest size for a body". The game camera
        /// is forty-six metres up at a slant, and the question that matters is not whether
        /// the skeleton is the right size but whether a player looking at the ground from
        /// there can tell there is one.
        ///
        /// Two shots: what the camera sees, and a close one for what is actually lying
        /// there. The second is the one that says the first is not a rendering fault.
        /// </summary>
        [MenuItem("The Veil/Trap Sign On A Level")]
        public static void OnALevel()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<TheVeil.App.LevelRunner>();
            if (runner == null) { Debug.Log("[Trap] no LevelRunner in the scene"); return; }

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            for (int level = 1; level <= 3; level++)
            {
                var root = SmokeTest.Build(runner, 1, level, out _);

                var remains = Remains(root.transform);
                if (remains == null)
                {
                    Debug.Log($"[Trap] 1-{level}: no remains anywhere on the level");
                    Object.DestroyImmediate(root);
                    continue;
                }

                var box = ModelScaling.Measure(remains.gameObject);

                Debug.Log($"[Trap] 1-{level}: {remains.name} "
                          + $"{box.size.x:0.00} x {box.size.y:0.00} x {box.size.z:0.00} m "
                          + $"at {box.center.x:0}, {box.center.z:0}");

                // The camera the game is played from: forty-six metres up, at a slant.
                Shoot(box.center + new Vector3(0f, PlayHeight, -PlayHeight), box.center,
                      System.IO.Path.Combine(shots, $"trapsign-{level}-played.png"));

                // And close, so a thing that cannot be seen from up there is shown to be
                // there all the same.
                Shoot(box.center + new Vector3(0f, 3f, -4f), box.center,
                      System.IO.Path.Combine(shots, $"trapsign-{level}-close.png"));

                Object.DestroyImmediate(root);
            }

            Debug.Log($"[Trap] pictures in {shots}");
        }

        /// <summary>How high the game's own camera sits, in metres. See RunVisuals.</summary>
        const float PlayHeight = 33f;

        /// <summary>The first bones standing on a level, wherever they ended up.</summary>
        static Transform Remains(Transform at)
        {
            foreach (Transform child in at)
            {
                if (child.name.Contains("Skeleton") || child.name.Contains("Skull")
                    || child.name.Contains("Bone"))
                    return child;

                var found = Remains(child);
                if (found != null) return found;
            }

            return null;
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Trap camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.42f, 0.5f);

            var rt = new RenderTexture(900, 900, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
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
