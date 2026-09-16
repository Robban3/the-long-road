using System.Text;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The castle on the goal, measured and photographed on its own:
    /// `The Veil > Castle Report`.
    ///
    /// Written after playing 1-10, where the champion turned the goal into the place the
    /// player is looking and the castle turned out to be a wall of grey slabs the caravan
    /// drives straight through. Both of those were known and both were traded away on a
    /// premise that has just stopped being true — PlaceCastle says it outright: *"From
    /// four hundred metres up nobody sees the column clip a course of stone."* Somebody
    /// does now.
    ///
    /// Measured rather than eyeballed, because there are two different faults that look
    /// identical from the game camera: a castle built out of pieces that are too big, and
    /// a castle of the right pieces scaled up to meet a height it was never meant to
    /// reach. The first wants different pieces and the second wants a different number.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.CastleReport.Run
    /// </summary>
    public static class CastleReport
    {
        [MenuItem("The Veil/Castle Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Castle] no LevelRunner in the scene"); return; }

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            var sheet = new StringBuilder();
            sheet.AppendLine("[Castle] the castle on the goal, per chapter");

            for (int chapter = 1; chapter <= 3; chapter++)
            {
                int level = Campaign.LevelsPerChapter;
                var root = SmokeTest.Build(runner, chapter, level, out var map);

                var castle = Find(root.transform, "Castle");
                if (castle == null)
                {
                    sheet.AppendLine($"[Castle] {chapter}-{level}: nothing was built on the goal");
                    Object.DestroyImmediate(root);
                    continue;
                }

                var box = ModelScaling.Measure(castle.gameObject);
                var scale = castle.lossyScale;

                sheet.AppendLine($"[Castle] {chapter}-{level}: {box.size.x:0.0} x {box.size.y:0.0} "
                                 + $"x {box.size.z:0.0} m, scaled x{scale.x:0.00}, "
                                 + $"{castle.childCount} pieces, foot at y {box.min.y:0.0}");

                // Every piece on its own terms, so a wall that is twelve metres tall is
                // told apart from a castle that is twelve metres tall.
                foreach (Transform piece in castle)
                {
                    var pb = ModelScaling.Measure(piece.gameObject);
                    sheet.AppendLine($"[Castle]     {piece.name}: {pb.size.x:0.0} x {pb.size.y:0.0} "
                                     + $"x {pb.size.z:0.0} m");
                }

                // From the side, at the height a wall is, and from above so the gateway
                // can be seen — the caravan has to fit through it and the column is
                // sixteen metres wide.
                float span = Mathf.Max(box.size.x, box.size.z);

                Shoot(box.center + new Vector3(0f, box.size.y * 0.4f, -span * 1.5f), box.center,
                      System.IO.Path.Combine(shots, $"castle-{chapter}-side.png"));

                Shoot(box.center + new Vector3(0f, span * 1.3f, -0.01f), box.center,
                      System.IO.Path.Combine(shots, $"castle-{chapter}-above.png"));

                // And the yard from where the player actually watches: the game camera is
                // forty-odd metres up at a slant, not a hundred straight down, and a crate
                // a metre and a quarter tall is a pixel from one and a thing from the other.
                Shoot(box.center + new Vector3(0f, 34f, -34f), box.center,
                      System.IO.Path.Combine(shots, $"castle-{chapter}-yard.png"));

                Object.DestroyImmediate(root);
            }

            sheet.AppendLine($"[Castle] pictures in {shots}");
            Debug.Log(sheet.ToString());
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Castle camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.42f, 0.5f);

            var rt = new RenderTexture(1400, 900, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1400, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0);
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
