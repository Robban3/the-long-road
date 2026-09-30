using System.Collections.Generic;
using System.Text;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Chooses which set of props each level wears: `The Veil &gt; Choose The Dressing`.
    ///
    /// <b>The last thing about a level that nobody chose.</b> The map is chosen from a
    /// hundred and sixty candidates, the enemies' strength is calibrated level by level, and
    /// both are settled on a bare map because that is all a level is when it is generated.
    /// The props are hung on it afterwards by the view, and they are worth difficulty: a
    /// solid prop shoves troops and enemies out of position while they fight, so who reaches
    /// whom is partly decided by where rock happens to stand. BareReport measured it at as
    /// much as a fifth of a point either way, with 6-6 playing at 0.60 against a target of
    /// 0.34 - and the same map dressed seven other ways came in at 0.34 and 0.35. The
    /// shipped dressing was simply the outlier.
    ///
    /// So it is chosen like everything else, and the choice is unusually cheap: the map does
    /// not move, so the bare judgement does not move, the climb does not move, and no test
    /// can see any of it. Only the fighting changes.
    ///
    /// Run after Build Level Catalogue, which needs the attempts written before a level can
    /// be built. Deliberately not chained onto the end of it: the builder holds a half-built
    /// catalogue in memory and a level built from that is not the level that will ship.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.DressingChoice.Run
    /// </summary>
    public static class DressingChoice
    {
        const string Path = "Assets/_Project/Resources/LevelCatalogue.txt";

        /// <summary>
        /// How far off its target a level may play before it is dressed again.
        ///
        /// Six hundredths, which is well inside the curve's own tolerance of a tenth. Set at
        /// the tolerance this would leave the worst levels exactly at the edge of what the
        /// suite allows; set much tighter it would re-dress fifty levels to chase what the
        /// bare judgement itself carries - the chosen maps sit a hundredth or two off their
        /// targets to begin with, and no dressing can mend that.
        /// </summary>
        const float Slack = 0.06f;

        /// <summary>
        /// How many dressings a level is offered.
        ///
        /// Eight was enough for all three of the levels the probe tried: five of 6-6's seven
        /// alternatives landed on the target and six of 6-1's. A level that cannot be mended
        /// in eight is a level whose ground is the problem, and the report says so rather
        /// than searching all night.
        /// </summary>
        const int Tries = 8;

        [UnityEditor.MenuItem("The Veil/Choose The Dressing")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Dressing] PlayLevel has no LevelRunner."); return; }

            var sheet = new StringBuilder();
            sheet.AppendLine("[Dressing] which set of props each level wears");

            var chosen = new Dictionary<(int, int), int>();
            int dressed = 0, stubborn = 0;

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    float target = DifficultyCurve.Target(chapter, level);

                    int best = 0;
                    float bestOff = float.MaxValue, bestPlayed = 0f;

                    for (int dressing = 0; dressing < Tries; dressing++)
                    {
                        LevelCatalogue.Dress(chapter, level, dressing);

                        float played = Played(runner, chapter, level);
                        float off = Mathf.Abs(played - target);

                        if (off < bestOff) { bestOff = off; best = dressing; bestPlayed = played; }

                        // The dressing as it falls out, where it is good enough. A level that
                        // plays its target should not be re-dressed for a hundredth: the one
                        // the seed gives is the one the rest of the project has looked at,
                        // photographed and judged by eye.
                        if (off <= Slack) break;
                    }

                    LevelCatalogue.Dress(chapter, level, best);
                    chosen[(chapter, level)] = best;

                    if (best != 0) dressed++;
                    if (bestOff > Slack) stubborn++;

                    sheet.AppendLine($"[Dressing] {chapter,3}-{level,-3} dressing {best} "
                                     + $"plays {bestPlayed:0.00} against {target:0.00}"
                                     + (bestOff > Slack ? "  - no dressing mends this one" : ""));
                }
            }

            Patch(chosen);

            sheet.AppendLine($"[Dressing] {dressed} level(s) re-dressed, {stubborn} still off by "
                             + $"more than {Slack:0.00}");
            sheet.AppendLine($"[Dressing] written to {Path}");
            Debug.Log(sheet.ToString());
        }

        /// <summary>The level as it will be played: built, with its props read in as obstacles.</summary>
        static float Played(LevelRunner runner, int chapter, int level)
        {
            var root = SmokeTest.Build(runner, chapter, level, out var map);

            float played = BareReport.Difficulty(map, LevelMaps.Recipe(chapter, level),
                                                 ReferenceSquad.LevelsCleared(chapter, level),
                                                 root, runner, true, out _);

            Object.DestroyImmediate(root);
            return played;
        }

        /// <summary>
        /// Writes the chosen dressings into the table, and changes nothing else in it.
        ///
        /// Patched rather than rewritten, because rewriting means searching for the maps
        /// again - an hour - and the maps have not changed. A row is left exactly as it was
        /// unless this pass chose a dressing for it.
        /// </summary>
        static void Patch(Dictionary<(int, int), int> chosen)
        {
            var lines = new List<string>(System.IO.File.ReadAllLines(Path));

            for (int i = 0; i < lines.Count; i++)
            {
                var parts = lines[i].Trim().Split(' ');
                if (parts.Length < 4 || parts.Length > 5) continue;

                if (!int.TryParse(parts[0], out int chapter) || !int.TryParse(parts[1], out int level))
                    continue;

                if (!chosen.TryGetValue((chapter, level), out int dressing)) continue;

                lines[i] = $"{parts[0]} {parts[1]} {parts[2]} {parts[3]} {dressing}";
            }

            System.IO.File.WriteAllLines(Path, lines);
            UnityEditor.AssetDatabase.ImportAsset(Path);
        }
    }
}
