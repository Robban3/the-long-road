using System.Text;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// What the scenery is worth, in difficulty: `The Veil &gt; Bare Report`.
    ///
    /// <b>Two numbers that were never put next to each other.</b> The catalogue calibrates
    /// a level and chooses its map with <see cref="LevelMaps.Judge"/>, which reads a bare
    /// map: the ground, the water and the groups on it. Nothing is standing on it, because
    /// nothing can be - the props are built by the view, long after the map is settled.
    /// MarginReport plays the level as built, rock and walls and all, and the two answers
    /// were five hundredths apart over chapter six while the catalogue was satisfied.
    ///
    /// So each level is played twice, on the same map with the same squad down the same
    /// roads: once with the built world's obstacles read in, and once without them. The
    /// difference is what the scenery is worth, and nothing has ever measured it.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.BareReport.Run
    /// </summary>
    public static class BareReport
    {
        [UnityEditor.MenuItem("The Veil/Bare Report")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Bare] PlayLevel has no LevelRunner."); return; }

            var sheet = new StringBuilder();
            sheet.AppendLine("[Bare] each level played twice: with the built world, and on the bare map");
            sheet.AppendLine("[Bare]  level   built    bare   scenery   target    secs    bare    longer");

            for (int chapter = First; chapter <= Last; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);
                    var recipe = LevelMaps.Recipe(chapter, level);
                    int cleared = ReferenceSquad.LevelsCleared(chapter, level);

                    float built = Difficulty(map, recipe, cleared, root, runner, true, out float slow);
                    float bare = Difficulty(map, recipe, cleared, root, runner, false, out float quick);

                    // <b>And how long the road took, both ways.</b> A solid prop is not a
                    // fight: the escort goes round it. So if the scenery is worth a fifth of
                    // a point the road must be longer for it, and if it is not then the
                    // difference is coming from somewhere else and the whole diagnosis is
                    // wrong. Cheaper than reading the geometry back and it answers the same
                    // question.
                    sheet.AppendLine($"[Bare] {chapter,3}-{level,-3} {built,7:0.00} {bare,7:0.00} "
                                     + $"{built - bare,9:+0.00;-0.00; 0.00} "
                                     + $"{DifficultyCurve.Target(chapter, level),8:0.00} "
                                     + $"{slow,7:0} {quick,7:0} "
                                     + $"{(quick > 0f ? slow / quick : 1f),9:0.00}");

                    Object.DestroyImmediate(root);
                }
            }

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// The chapters looked at: all the ones the curve is tuned for.
        ///
        /// It was written for six and seven, the pair that changed places, and widened as
        /// soon as their answer came back - the scenery moved those twenty levels by a fifth
        /// of a point in both directions, and twenty levels cannot say whether that is the
        /// desert or the whole road. Ten minutes of an editor against a question that size
        /// is not a trade worth thinking about.
        /// </summary>
        const int First = 1;
        static int Last => DifficultyCurve.BuiltChapters;

        /// <summary>
        /// The share of the escort lost on the roads that are not the fast one, which is
        /// the curve's own arithmetic. See MarginReport, which measures it the same way.
        ///
        /// Public because the dressing pass asks the same question, and there has to be one
        /// answer to it: a dressing chosen against one measurement and reported against
        /// another is two different levels.
        /// </summary>
        public static float Difficulty(LevelMap map, LevelRecipe recipe, int cleared,
                                GameObject root, LevelRunner runner, bool scenery,
                                out float seconds)
        {
            float left = 0f;
            int roads = 0;
            seconds = 0f;

            foreach (var corridor in map.Corridors)
            {
                if (corridor.Kind == CorridorKind.Fast) continue;

                var started = ReferenceSquad.Play(map, corridor.Tiles, recipe, cleared,
                                                  ReferenceSquad.SpentOnTroops);

                if (scenery)
                {
                    var markers = new GameObject("Column");
                    markers.transform.SetParent(root.transform, false);

                    var visuals = new RunVisuals(markers.transform, map.Grid, runner.HeightScale)
                    {
                        Library = runner.Models
                    };

                    visuals.FindBridges(root.transform);
                    visuals.FindObstacles(root.transform, started);

                    Object.DestroyImmediate(markers);
                }

                if (started.RunToCompletion() == RunOutcome.Arrived) left += LevelMaps.EscortLeft(started);
                seconds += started.TravelSeconds;
                roads++;
            }

            return roads > 0 ? 1f - left / roads : 1f;
        }
    }
}
