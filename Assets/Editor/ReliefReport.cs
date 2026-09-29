using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// How high each country stands: `The Veil > Relief Report`.
    ///
    /// <b>Written because the mountains are flat and nothing said so.</b> Chapter six is
    /// a country of passes on paper — two thirds bare rock, the tightest noise in the
    /// game, roads that thread between faces — and photographed from above it is a grey
    /// gravel field with stones lying on it. Every instrument here measures what is
    /// *placed*: props, water, solidity, villages. None of them measures the shape of the
    /// ground the props are placed on, so the one thing wrong with that chapter was the
    /// one thing nothing looked at.
    ///
    /// What it prints is the ground itself, in metres: how far a level rises from its
    /// lowest tile to its highest, and how much it climbs from one tile to the next. The
    /// second number is the one that decides whether a country reads as hilly — a map
    /// that rises thirty metres evenly across two hundred and fifty is a dome, and one
    /// that rises three metres between neighbours is a pass.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.ReliefReport.Run
    /// </summary>
    public static class ReliefReport
    {
        [MenuItem("The Veil/Relief Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Relief] no LevelRunner in the scene"); return; }

            float scale = runner.HeightScale;

            // Every dressed chapter, from the one place that knows which they are.
            var chapters = DifficultyCurve.Dressed;

            foreach (int chapter in chapters)
            {
                float rise = 0f, mean = 0f, steepest = 0f;

                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var grid = LevelMaps.For(chapter, level).Grid;

                    float low = float.MaxValue, high = float.MinValue;
                    double steps = 0;
                    int counted = 0;

                    for (int y = 0; y < grid.Height; y++)
                        for (int x = 0; x < grid.Width; x++)
                        {
                            float here = grid.Elevation(x, y) * scale;
                            if (here < low) low = here;
                            if (here > high) high = here;

                            // East and north only, so each pair of neighbours is measured
                            // once rather than twice.
                            if (x + 1 < grid.Width) Step(grid, scale, here, x + 1, y, ref steps, ref counted, ref steepest);
                            if (y + 1 < grid.Height) Step(grid, scale, here, x, y + 1, ref steps, ref counted, ref steepest);
                        }

                    rise += high - low;
                    if (counted > 0) mean += (float)(steps / counted);
                }

                int levels = Campaign.LevelsPerChapter;

                Debug.Log($"[Relief] {chapter} ({Biomes.Of(chapter)}): rises {rise / levels:0.0} m from"
                          + $" lowest to highest, {mean / levels:0.00} m between neighbouring tiles"
                          + $" on average, steepest single step {steepest:0.0} m.");
            }
        }

        static void Step(TileGrid grid, float scale, float here, int x, int y,
                         ref double steps, ref int counted, ref float steepest)
        {
            float step = Mathf.Abs(grid.Elevation(x, y) * scale - here);

            steps += step;
            counted++;
            if (step > steepest) steepest = step;
        }
    }
}
