using System;
using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Walks every level, finds the map each one ships, and writes it down:
    /// `The Veil &gt; Build Level Catalogue`.
    ///
    /// <b>Run this after changing anything the difficulty depends on.</b> An enemy's
    /// strength, a price, an upgrade cap, the reference escort, the shape of a road — all
    /// of them change which candidate a level accepts, and a catalogue built before the
    /// change points at a map that may no longer be winnable. LevelCatalogue's signature
    /// catches that and refuses the stale table; what it cannot do is write the new one.
    ///
    /// The cost lands here, once, instead of on every player at every chapter: a level
    /// that wants thirty-four attempts drives a hundred simulated caravans to find them.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.CatalogueBuilder.Run
    /// </summary>
    public static class CatalogueBuilder
    {
        /// <summary>Where the table is written. Resources, so a build carries it.</summary>
        const string Path = "Assets/_Project/Resources/LevelCatalogue.txt";

        /// <summary>
        /// The chapters that exist, and are searched against the whole of LevelMaps.Gate.
        ///
        /// The rest of the table is still written - the game can be asked for any level -
        /// but those chapters are searched against the roads the escort gets down and no
        /// more. The difficulty curve and the prepared player cost dozens of simulated
        /// runs per candidate, which over seventy levels nobody can play is hours for
        /// nothing; and their recipes have not been tuned, so holding them to a curve
        /// would only fill the table with compromises. Raise this as a chapter is built.
        /// </summary>
        const int BuiltChapters = DifficultyCurve.BuiltChapters;

        [MenuItem("The Veil/Build Level Catalogue")]
        public static void Run()
        {
            var table = new StringBuilder();

            table.AppendLine("# Which attempt each level ships. Built by "
                             + "The Veil > Build Level Catalogue.");
            table.AppendLine("# Three numbers a line: chapter, level, attempt.");
            table.AppendLine("#");
            table.AppendLine("# The signature is what these answers were judged against. A "
                             + "catalogue whose");
            table.AppendLine("# signature no longer matches the rules is ignored rather "
                             + "than trusted — see");
            table.AppendLine("# Gen.LevelCatalogue, and rebuild rather than edit.");
            table.AppendLine($"signature {LevelCatalogue.Signature()}");
            table.AppendLine();

            // Written before the sweep so a half-finished run leaves a table that is
            // refused rather than one that is half right.
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));

            var sheet = new StringBuilder();
            sheet.AppendLine("[Catalogue] the map each level ships");

            int walked = 0, searched = 0, stuck = 0, settled = 0;
            var beaten = new StringBuilder();

            // How hard the last level turned out, so a level that came out easier than the
            // one before it is named in the sheet.
            float before = 0f;

            for (int chapter = 1; chapter <= LevelCatalogue.Chapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    // From nothing, every time. Reading the old catalogue here would let a
                    // stale answer copy itself into the new one, which is the one way a
                    // table like this goes wrong and stays wrong.
                    LevelMap map;

                    if (chapter <= BuiltChapters)
                    {
                        map = Best(chapter, level, out float difficulty, out string how);
                        sheet.AppendLine($"[Catalogue] {chapter}-{level}: difficulty {difficulty:P0}, "
                                         + $"target {DifficultyCurve.Target(chapter, level):P0} - {how}"
                                         + (difficulty < before ? "  <-- easier than the level before" : ""));
                        before = difficulty;
                    }
                    else
                    {
                        map = Fresh(chapter, level, before);
                    }

                    if (map == null)
                    {
                        sheet.AppendLine($"[Catalogue] {chapter}-{level}: nothing generated");
                        continue;
                    }

                    // Attempts counts from one and the generator's loop from nought.
                    int attempt = map.Attempts - 1;

                    // <b>A search that ran out is not an answer, and it looks exactly like
                    // one from here.</b> Generate keeps the least bad candidate and hands
                    // it back when the ceiling is reached, so a level nobody can get down
                    // arrives as an ordinary row with a high attempt number on it. Written
                    // into the table it becomes the shipped map, and the one gate that
                    // would have caught it is the gate that already gave up.
                    //
                    // So what is about to be written down is asked the question one more
                    // time. It costs one more run on the levels that searched hardest and
                    // it is the difference between a catalogue that records a search and a
                    // catalogue that records a promise.
                    if (!map.Accepted)
                    {
                        beaten.AppendLine($"[Catalogue] {chapter}-{level}: the search ran "
                                          + "out and this is the least bad candidate, found "
                                          + $"at attempt {attempt}");
                        settled++;
                    }

                    if (!LevelMaps.Winnable(map, chapter, level))
                    {
                        beaten.AppendLine($"[Catalogue] {chapter}-{level}: no road the "
                                          + "reference escort can get down");
                        stuck++;
                    }

                    table.AppendLine($"{chapter} {level} {attempt}");
                    walked++;
                    if (attempt > 0) searched++;

                    if (attempt > 0)
                        sheet.AppendLine($"[Catalogue] {chapter}-{level}: attempt {attempt}");
                }
            }

            System.IO.File.WriteAllText(Path, table.ToString());
            AssetDatabase.ImportAsset(Path);

            LevelCatalogue.Forget();

            sheet.AppendLine($"[Catalogue] {walked} levels, {searched} of them past the "
                             + "first attempt");
            sheet.AppendLine($"[Catalogue] written to {Path}");
            sheet.AppendLine($"[Catalogue] signature {LevelCatalogue.Signature()}");

            Debug.Log(sheet.ToString());

            // Loud, and after the table, so the numbers above are there to read beside it.
            // The table is still written: a level that cannot be won is a level whose
            // recipe or whose map wants changing, and refusing to write the catalogue
            // would only take the rest of the game away while that is worked out.
            if (stuck > 0 || settled > 0)
                Debug.LogWarning($"[The Veil] {settled} level(s) ship a compromise and "
                                 + $"{stuck} ship a map the reference escort cannot get "
                                 + $"down:\n{beaten}");
        }

        /// <summary>
        /// One level generated by the full search, with no catalogue in the way.
        ///
        /// LevelMaps.For would read the table this is writing, so it cannot be used here —
        /// the tool would find last week's answer and copy it forward, and a catalogue that
        /// launders its own stale rows is worse than none.
        /// </summary>
        /// <summary>
        /// Every attempt of a built level looked at, and the one kept nearest its place on
        /// the curve.
        ///
        /// <b>Every attempt, not the first that will do.</b> The search stops at the first
        /// candidate that passes, and inside a window a tenth either side of the target the
        /// first to pass sat anywhere in it - so two neighbouring levels could land a fifth
        /// apart, and in either order.
        ///
        /// <b>And no chain.</b> The first version also held each level to at least the
        /// difficulty of the one before, which is the rule written down. It ran away: a
        /// level's difficulty is measured from three runs and comes in lumps, so a level
        /// that overshot lifted the floor for the rest of the chapter and by 2-7 nothing
        /// lay between 59 and 76 per cent - eighteen levels of thirty became compromises
        /// and the third chapter's boss cost 89 per cent. Held to its own target instead,
        /// each level lands within a few points of it, and a level that comes out easier
        /// than the one before is named in the sheet rather than forced.
        ///
        /// In order of preference: every hard rule (the roads the level owes, and a prepared
        /// player down every road), then the fast road hardest, then nearest the target.
        /// </summary>
        static LevelMap Best(int chapter, int level, out float difficulty, out string how)
        {
            var recipe = LevelMaps.Recipe(chapter, level);
            int seed = DeterministicRandom.SeedFor(chapter, level);
            float target = DifficultyCurve.Target(chapter, level);

            LevelMap best = null;
            float bestScore = float.MaxValue;
            difficulty = 1f;
            how = "no candidate passed the hard rules";

            for (int attempt = 0; attempt < recipe.MaxGenerationAttempts; attempt++)
            {
                var map = TerrainGenerator.Generate(recipe, seed, null, attempt);
                if (map == null || !map.Accepted) continue;

                var judged = LevelMaps.Judge(map, chapter, level, recipe.RoutesOwed);
                if (!judged.Hard) continue;

                float score = Math.Abs(judged.Difficulty - target);
                if (!judged.Treacherous) score += 10f;

                if (score >= bestScore) continue;

                bestScore = score;
                best = map;
                difficulty = judged.Difficulty;
                how = (judged.Treacherous ? "" : "fast road NOT the hardest, ") + $"attempt {attempt}";
            }

            return best ?? Fresh(chapter, level, 0f);
        }

        static LevelMap Fresh(int chapter, int level, float floor)
        {
            var recipe = LevelMaps.Recipe(chapter, level);

            if (chapter > BuiltChapters)
                return TerrainGenerator.Generate(recipe,
                                                 DeterministicRandom.SeedFor(chapter, level),
                                                 candidate => LevelMaps.RoadsThrough(candidate, chapter, level,
                                                                                     recipe.RoutesOwed)
                                                              >= recipe.RoutesOwed
                                                     ? TerrainGenerator.Accepted : 0);

            // The gate the game uses, not a part of it. This called RoadsThrough alone, so
            // for a day the catalogue chose maps without asking whether a prepared player
            // could get down every road - the game asked when it loaded the level, and the
            // catalogue had simply been lucky that its choices passed.
            return TerrainGenerator.Generate(recipe,
                                             DeterministicRandom.SeedFor(chapter, level),
                                             candidate => LevelMaps.Gate(candidate, chapter,
                                                                         level, recipe.RoutesOwed, floor));
        }

    }
}
