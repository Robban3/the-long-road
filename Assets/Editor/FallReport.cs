using System.Collections.Generic;
using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>How far a river falls between one tile and the next, measured over the built chapters.</summary>
    public static class FallReport
    {
        public static void Run()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Fall] the steepest step in each level's water, in metres");

            float worst = 0f;
            int steep = 0, levels = 0;

            for (int chapter = FallChapter; chapter <= FallChapter; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var grid = LevelMaps.For(chapter, level).Grid;
                    float most = 0f;
                    int drops = 0;

                    for (int y = 0; y < grid.Height; y++)
                        for (int x = 0; x < grid.Width; x++)
                        {
                            int tile = grid.ToIndex(x, y);
                            if (!Wet(grid[tile])) continue;

                            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                if (!grid.InBounds(x + dx, y + dy)) continue;
                                int next = grid.ToIndex(x + dx, y + dy);
                                if (!Wet(grid[next])) continue;

                                float drop = (grid.Elevation(tile) - grid.Elevation(next))
                                             * 14f;
                                if (drop > most) most = drop;
                                if (drop >= 2f) drops++;
                            }
                        }

                    levels++;
                    if (most >= 2f) steep++;
                    if (most > worst) worst = most;
                    sheet.AppendLine($"[Fall] {chapter}-{level}: steepest {most:0.0} m, {drops} steps of two metres or more");
                }

            sheet.AppendLine($"[Fall] {steep} of {levels} levels have a step of two metres or more; worst {worst:0.0} m");
            Debug.Log(sheet.ToString());
        }

        static bool Wet(TerrainType t) => t == TerrainType.Water || t == TerrainType.Ford;

        /// <summary>
        /// The falls themselves, photographed: `The Veil &gt; Fall Photos`.
        ///
        /// <b>This report has measured in metres since it was written and never shown
        /// anything.</b> It says the steepest step in 6-1's water is 13.3 m, and the scale
        /// report says the sheet standing there is 31.1 m long - both true, and neither
        /// answers the question anybody actually has, which is whether the water looks like
        /// it is coming off a rock or like a plane somebody stretched. The numbers disagree
        /// for a good reason (the sheet hangs from the crown of the tor, not from the brink
        /// of the shelf) and a good reason is exactly the kind of thing that hides a fault.
        ///
        /// Two shots of each: one from across the gorge at the height a person would stand,
        /// which is the only angle a waterfall is ever really seen from, and one from the
        /// camera the game is played at, which is the angle the player gets.
        /// </summary>
        [UnityEditor.MenuItem("The Veil/Fall Photos")]
        public static void Photograph()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<TheVeil.App.LevelRunner>();
            if (runner == null) { Debug.Log("[Fall] no LevelRunner in the scene"); return; }

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilFalls");
            System.IO.Directory.CreateDirectory(shots);

            // Every level of the country, not a sample of three. A fall is built out of the
            // ground it lands in and no two levels have the same ground; three of ten told
            // us that 6-1 was wrong and said nothing about the seven that were not looked
            // at, which is the same fault the reports themselves had.
            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
            {
                var root = SmokeTest.Build(runner, FallChapter, level, out _);

                var sheet = Find(root.transform, "Waterfall");
                if (sheet == null)
                {
                    Debug.Log($"[Fall] {FallChapter}-{level}: no fall built");
                    Object.DestroyImmediate(root);
                    continue;
                }

                var box = TheVeil.View.ModelScaling.Measure(sheet.gameObject);

                Debug.Log($"[Fall] {FallChapter}-{level}: {sheet.name} "
                          + $"{box.size.x:0.0} x {box.size.y:0.0} x {box.size.z:0.0} m "
                          + $"at {box.center.x:0}, {box.center.z:0}");

                // <b>From where the water goes, looking back up at it.</b>
                //
                // This stood off at forty-five degrees across the gorge, which is the one
                // angle a fall in a cleft cannot be seen from: the bank is between the
                // camera and the water. It cost an hour - the rock was brought in close to
                // the channel to match the pack's own picture, the photograph came back as
                // a wall of stone with no water in it, and the rock was very nearly moved
                // back out again. The rock was right. The camera was standing on the bank.
                //
                // A waterfall is looked at from downstream. The sheet's own forward is the
                // way the water is going, so the camera goes out along it and turns round.
                //
                // Backed off by a fixed distance and not by the sheet's own height, which
                // is circular: the whole reason for the picture is that the height is the
                // thing in question, and when the fall stopped being thirty-one metres and
                // became thirteen the camera walked in with it and photographed a spruce.
                float away = box.size.y + 34f;

                Shoot(box.center + sheet.forward * away + Vector3.up * (box.size.y * 0.25f),
                      box.center,
                      System.IO.Path.Combine(shots, $"fall-{FallChapter}-{level}-near.png"));

                // And from where the game is played.
                Shoot(box.center + new Vector3(0f, PlayHeight, -PlayHeight), box.center,
                      System.IO.Path.Combine(shots, $"fall-{FallChapter}-{level}-play.png"));

                Object.DestroyImmediate(root);
            }

            Debug.Log($"[Fall] pictures in {shots}");
        }

        /// <summary>
        /// The one country with rock high enough to drop water off. See Run.
        ///
        /// <b>Asked for by country, because chapter numbers move.</b> This was 6 while the
        /// mountain was the sixth chapter; the wheel turned one step and the mountain became
        /// the fifth, which would have left both of this file's jobs - the measurement and
        /// the photographs - pointed at the desert, reporting no falls and no fault.
        /// </summary>
        static int FallChapter => Biomes.FirstChapterOf(Biome.Mountain);

        /// <summary>The height the game's own camera sits at, in metres.</summary>
        const float PlayHeight = 33f;

        /// <summary>
        /// The falling sheet, and not the spray at the foot of it.
        ///
        /// <b>Both are called a waterfall and only one of them has a size.</b> The spray is
        /// FX_Waterfall_Foam_01, a particle effect, and at edit time a particle system has
        /// emitted nothing and measures nought by nought by nought - so the first run of
        /// this pointed a camera at a point and photographed the sky. The sheet is a mesh.
        /// Asked for by having a body rather than by name, because the pack is free to
        /// rename either of them and the difference between the two is not what they are
        /// called.
        /// </summary>
        static Transform Find(Transform at, string name)
        {
            foreach (Transform child in at)
            {
                // Case folded, because the two are not spelled the same: the pack's sheet
                // is SM_River_Plane_WaterFall_01 and its spray is FX_Waterfall_Foam_01.
                if (child.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0
                    && TheVeil.View.ModelScaling.Measure(child.gameObject).size.y > 1f)
                    return child;

                var found = Find(child, name);
                if (found != null) return found;
            }

            return null;
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Fall camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.70f, 0.78f);
            camera.farClipPlane = 2000f;

            var texture = new RenderTexture(1100, 1100, 24);
            camera.targetTexture = texture;

            // Twice, the first thrown away: the first render of a freshly built level comes
            // back with half its surfaces blown out. See GroundPhotos.
            camera.Render();
            camera.Render();

            RenderTexture.active = texture;
            var shot = new Texture2D(1100, 1100, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 1100, 1100), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);

            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log($"[Fall] {System.IO.Path.GetFileName(path)}");
        }
    }
}
