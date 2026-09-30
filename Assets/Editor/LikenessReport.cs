using System.Collections.Generic;
using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// How alike two levels are, tile for tile: `The Veil &gt; Likeness Report`.
    ///
    /// <b>Written before the tour was shifted, so there would be a before.</b> A level's
    /// shape is its terrain - where the wood is, where the water runs, where the rock
    /// stands - and two levels that share that shape are the same level whatever is
    /// standing on them. Nothing in this project has ever been able to say whether two
    /// levels are alike, so every claim about variety has been somebody looking at two
    /// pictures.
    ///
    /// The number is the share of tiles that hold the same kind of ground in both. It is
    /// not zero for two unrelated maps and cannot be: a recipe that asks for half forest
    /// gives two maps that agree on about half their forest by chance alone, so the floor
    /// is the base rate of the recipe and not nought. What matters is the distance from
    /// that floor - two maps at 95 % are the same map, two at 45 % are two maps.
    ///
    /// Two questions, which are different:
    ///
    ///   * <b>Against the level before it.</b> Does 3-4 look like 3-3? A chapter is ten
    ///     levels of one country and they should be ten places in it.
    ///   * <b>Against what it used to be.</b> Written to a file by `Likeness Baseline`
    ///     before a change and read back after, so "the levels are new" is a measurement
    ///     rather than a hope.
    ///
    /// And the closest pair anywhere in the campaign, because the worst twin is rarely
    /// the one you happen to compare.
    ///
    /// <b>One level has a floor of its own and it is high: 1-8, the town.</b> Towns.Stamp
    /// makes the whole map building and cuts three streets through it, so about six tiles
    /// in seven are wall whatever the seed says and the streets are the only thing there is
    /// to move. It measures 86 per cent against its own former self and cannot go much
    /// below that without the town ceasing to be a town. Written down here because it was
    /// read as a fault once, and the hour that cost is the hour this paragraph saves.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.LikenessReport.Run
    /// </summary>
    public static class LikenessReport
    {
        /// <summary>Where the before-picture is kept. Outside Assets: it is not content.</summary>
        static string BaselinePath =>
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilLikeness.txt");

        /// <summary>How alike two levels may be before it is worth saying so.</summary>
        // Four fifths. Two maps from one recipe agree on somewhere near half their ground
        // by chance; four fifths is well past anything chance produces and is the point at
        // which two levels start reading as the same place.
        const float Alike = 0.80f;

        /// <summary>How alike a level may be to its own former self before it has not changed.</summary>
        // Nineteen tiles in twenty. Not a hundred per cent: a level whose ground is the
        // same everywhere but one tile has not been regenerated, it has been nudged, and
        // the point of the line is to catch a level that stood still rather than to catch
        // an exact string match.
        const float Unmoved = 0.95f;

        [MenuItem("The Veil/Likeness Baseline")]
        public static void Baseline()
        {
            var lines = new List<string>();

            foreach (var (chapter, level, ground) in Everything())
                lines.Add($"{chapter} {level} {ground}");

            System.IO.File.WriteAllLines(BaselinePath, lines);
            Debug.Log($"[Likeness] {lines.Count} level(s) written to {BaselinePath}");
        }

        [MenuItem("The Veil/Likeness Report")]
        public static void Run()
        {
            var levels = new List<(int Chapter, int Level, string Ground)>(Everything());

            var sheet = new StringBuilder();
            sheet.AppendLine("[Likeness] share of tiles two levels hold the same kind of ground on");

            // <b>Against the level before it, inside its own chapter.</b>
            float worstRun = 0f;
            string runAt = "nowhere";

            foreach (var chapter in ByChapter(levels))
            {
                float most = 0f, total = 0f;
                int pairs = 0;

                for (int i = 1; i < chapter.Count; i++)
                {
                    float like = Likeness(chapter[i - 1].Ground, chapter[i].Ground);

                    total += like;
                    pairs++;

                    if (like <= most) continue;

                    most = like;
                    if (like > worstRun)
                    {
                        worstRun = like;
                        runAt = $"{chapter[i].Chapter}-{chapter[i].Level} against "
                                + $"{chapter[i - 1].Chapter}-{chapter[i - 1].Level}";
                    }
                }

                if (pairs == 0) continue;

                sheet.AppendLine($"[Likeness] {chapter[0].Chapter} ({Biomes.Of(chapter[0].Chapter)}): "
                                 + $"each level against the one before it, {total / pairs:P0} alike "
                                 + $"on average, closest pair {most:P0}");
            }

            // <b>And the closest pair anywhere.</b> The worst twin is rarely the one you
            // happened to compare: a level can be a stranger to its neighbour and the
            // double of one three chapters away.
            float twins = 0f;
            string twinsAt = "nowhere";

            for (int a = 0; a < levels.Count; a++)
                for (int b = a + 1; b < levels.Count; b++)
                {
                    float like = Likeness(levels[a].Ground, levels[b].Ground);
                    if (like <= twins) continue;

                    twins = like;
                    twinsAt = $"{levels[a].Chapter}-{levels[a].Level} and "
                              + $"{levels[b].Chapter}-{levels[b].Level}";
                }

            sheet.AppendLine($"[Likeness] closest two levels in the campaign: {twinsAt}, {twins:P0} alike");
            sheet.AppendLine($"[Likeness] closest level to the one before it: {runAt}, {worstRun:P0}");

            // <b>And against what each level used to be.</b>
            if (System.IO.File.Exists(BaselinePath))
            {
                var was = new Dictionary<string, string>();

                foreach (string line in System.IO.File.ReadAllLines(BaselinePath))
                {
                    var parts = line.Split(' ');
                    if (parts.Length == 3) was[parts[0] + " " + parts[1]] = parts[2];
                }

                float worst = 0f, sum = 0f;
                int counted = 0;
                string at = "nowhere";

                // <b>Every level that stood still, and not just the worst one.</b> The first
                // run of this reported "closest 1-8 at 100 %" and was read as "one level is
                // unchanged" - it says no such thing. A single worst case hides a set: when
                // 1-8 was mended the line came back as "closest 100-1 at 100 %", and the sea
                // is ten levels, not one. A report that names one member of a group teaches
                // you to mend them one at a time.
                var stood = new List<string>();
                var held = new List<(string At, float Like)>();

                foreach (var (chapter, level, ground) in levels)
                {
                    if (!was.TryGetValue($"{chapter} {level}", out string before)) continue;

                    float like = Likeness(before, ground);

                    sum += like;
                    counted++;

                    if (like >= Unmoved) stood.Add($"{chapter}-{level} {like:P0}");
                    held.Add(($"{chapter}-{level}", like));

                    if (like <= worst) continue;

                    worst = like;
                    at = $"{chapter}-{level}";
                }

                if (counted > 0)
                    sheet.AppendLine($"[Likeness] against the levels as they were: "
                                     + $"{sum / counted:P0} alike on average over {counted}, "
                                     + $"closest {at} at {worst:P0}");

                sheet.AppendLine(stood.Count == 0
                    ? $"[Likeness] no level is {Unmoved:P0} or more the level it was."
                    : $"[Likeness] {stood.Count} level(s) barely moved: {string.Join(", ", stood)}");

                // <b>And the tail, because a threshold answers the wrong question.</b> The
                // line above says whether anything stood still, which is the promise; it
                // cannot say whether the one level nearest the line is alone out there or
                // the first of twenty. So the ten most alike are printed whatever they
                // measure, and the shape of the tail is read off them.
                held.Sort((a, b) => b.Like.CompareTo(a.Like));

                var tail = new List<string>();
                for (int i = 0; i < held.Count && i < 10; i++)
                    tail.Add($"{held[i].At} {held[i].Like:P0}");

                sheet.AppendLine($"[Likeness] most alike to what they were: {string.Join(", ", tail)}");
            }
            else sheet.AppendLine("[Likeness] no baseline written, so no before to compare with");

            sheet.AppendLine(twins >= Alike || worstRun >= Alike
                ? $"[Likeness] something is over {Alike:P0} and wants looking at."
                : $"[Likeness] nothing is over {Alike:P0} alike.");

            Debug.Log(sheet.ToString());
        }

        /// <summary>Every dressed level's ground, as one character a tile.</summary>
        static IEnumerable<(int Chapter, int Level, string Ground)> Everything()
        {
            foreach (int chapter in DifficultyCurve.Dressed)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var grid = LevelMaps.For(chapter, level).Grid;
                    var ground = new StringBuilder(grid.TileCount);

                    for (int i = 0; i < grid.TileCount; i++)
                        ground.Append((char)('0' + (int)grid[i]));

                    yield return (chapter, level, ground.ToString());
                }
        }

        static List<List<(int Chapter, int Level, string Ground)>> ByChapter(
            List<(int Chapter, int Level, string Ground)> levels)
        {
            var grouped = new List<List<(int, int, string)>>();

            foreach (var level in levels)
            {
                if (grouped.Count == 0 || grouped[grouped.Count - 1][0].Item1 != level.Chapter)
                    grouped.Add(new List<(int, int, string)>());

                grouped[grouped.Count - 1].Add(level);
            }

            return grouped;
        }

        /// <summary>The share of tiles two levels hold the same kind of ground on.</summary>
        static float Likeness(string one, string two)
        {
            int length = Mathf.Min(one.Length, two.Length);
            if (length == 0) return 0f;

            int same = 0;
            for (int i = 0; i < length; i++)
                if (one[i] == two[i]) same++;

            return same / (float)length;
        }
    }
}
