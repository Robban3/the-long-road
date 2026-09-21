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

            // How many of this chapter's fast roads have killed the escort the curve assumes.
            // See Best: about half of them should.
            int fastKills = 0;


            LevelCatalogue.ClearTuning();

            for (int chapter = 1; chapter <= LevelCatalogue.Chapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    // From nothing, every time. Reading the old catalogue here would let a
                    // stale answer copy itself into the new one, which is the one way a
                    // table like this goes wrong and stays wrong.
                    LevelMap map;
                    if (level == 1) fastKills = 0;

                    if (chapter <= BuiltChapters)
                    {
                        // A kill wanted when the chapter is behind half: none on the first
                        // level, one by the second, two by the fourth - so the first level of a
                        // chapter has a fast road that hurts and does not end the run, and
                        // after that it ends it about every other time.
                        bool wantKill = fastKills < level / 2;

                        // Aimed at what the level has to reach: its target, or the level
                        // before it if that came out higher. Calibrated to the target alone,
                        // the typical map sat below a floor the levels before had lifted, and
                        // 3-9 found no map at or over it: 40 per cent after 53.
                        float aim = Math.Max(DifficultyCurve.Target(chapter, level), before);

                        float factor = Calibrate(chapter, level, aim, out float typical);
                        LevelCatalogue.Tune(chapter, level, factor);

                        map = Best(chapter, level, wantKill, before, aim, out float difficulty, out bool killed, out string how);
                        if (killed) fastKills++;

                        how += $", strength x{factor:0.000} (typical map {typical:P0})";
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

                    if (chapter <= BuiltChapters)
                        table.AppendLine($"{chapter} {level} {attempt} "
                                         + LevelCatalogue.Factor(chapter, level)
                                             .ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                    else
                        table.AppendLine($"{chapter} {level} {attempt}");
                    walked++;
                    if (attempt > 0) searched++;

                    if (attempt > 0)
                        sheet.AppendLine($"[Catalogue] {chapter}-{level}: attempt {attempt}");
                }
            }

            System.IO.File.WriteAllText(Path, table.ToString());
            AssetDatabase.ImportAsset(Path);

            LevelCatalogue.ClearTuning();
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
        /// player down every road), then the fast road hardest, then no easier than the
        /// level before, then the fast road ending the ordinary escort's run on the levels
        /// it is meant to (about half of them), then nearest the target.
        ///
        /// <b>Why the half is chosen here and not left to fall out.</b> Left alone it fell
        /// out one of ten in the first chapter and eight of ten in the second: nearest the
        /// target on average, the first chapter's easy settings kept the fast road
        /// survivable and the second's made it lethal, and neither is a road that fools
        /// anybody. The road that looks easiest should end a run often enough to be feared
        /// and seldom enough to be tried.
        /// </summary>
        /// <summary>How many of a level's maps the calibration measures the typical one over.</summary>
        const int CalibrationMaps = 24;

        /// <summary>
        /// The strength factor at which a level's typical map lands on its target.
        ///
        /// The typical map, not the chosen one: the median over the first maps the level
        /// generates, measured on the safe and long roads the way DifficultyCurve measures.
        /// Where the typical map is, the search finds plenty of maps near the target and
        /// can keep every other rule as well; where it was far from it, the target could
        /// only be met by an outlier, and often was not.
        ///
        /// Strength does not change a map - the placer spends points, not strength - so
        /// the maps are made once and only the runs are repeated, halving the range each
        /// time. Six halvings of a range from half to twice put the factor within about a
        /// sixtieth.
        /// </summary>
        static float Calibrate(int chapter, int level, float aim, out float typical)
        {
            var recipe = ChapterRecipe.For(chapter).ForLevel(level);
            float baseStrength = recipe.EnemyStrength;
            int seed = DeterministicRandom.SeedFor(chapter, level);
            int cleared = ReferenceSquad.LevelsCleared(chapter, level);

            var maps = new System.Collections.Generic.List<LevelMap>();
            for (int attempt = 0; attempt < 80 && maps.Count < CalibrationMaps; attempt++)
            {
                var map = TerrainGenerator.Generate(recipe, seed, null, attempt);
                if (map != null && map.Accepted) maps.Add(map);
            }

            float Median(float factor)
            {
                var levels = new System.Collections.Generic.List<float>();

                foreach (var map in maps)
                {
                    float left = 0f;
                    int roads = 0;

                    foreach (var corridor in map.Corridors)
                    {
                        if (corridor.Kind == CorridorKind.Fast) continue;

                        var run = new LevelRun(map, corridor.Tiles,
                                               ReferenceSquad.For(recipe, cleared, 0, ReferenceSquad.SpentOnTroops),
                                               baseStrength * factor) { Shops = true };

                        if (run.RunToCompletion() == RunOutcome.Arrived) left += LevelMaps.EscortLeft(run);
                        roads++;
                    }

                    levels.Add(roads > 0 ? 1f - left / roads : 1f);
                }

                levels.Sort();
                return levels.Count > 0 ? levels[levels.Count / 2] : 0f;
            }

            // <b>No floor under the strength.</b> The first build held each level's enemies
            // to at least the strength of the level before, and the town undid it: 1-8's
            // walls leave nothing to make harder but the enemies, it needed them half as
            // strong again to reach its target, and every level after it inherited that -
            // 1-10 and 3-10 came out with no map a prepared player could win. What has to
            // rise level by level is how hard the level is, and that is what the curve holds.
            // The strength is the dial that gets each level there, and a level built easy
            // needs it turned further than its neighbours.
            float low = 0.5f;
            float high = 2f;

            if (maps.Count == 0) { typical = 0f; return 1f; }

            typical = Median(low);
            if (typical >= aim) return low;

            for (int step = 0; step < 6; step++)
            {
                float middle = (float)Math.Sqrt(low * high);
                if (Median(middle) < aim) low = middle;
                else high = middle;
            }

            float factor = (float)Math.Sqrt(low * high);
            typical = Median(factor);
            return factor;
        }

        /// <summary>
        /// How many attempts of a built level are looked at.
        ///
        /// A hundred and sixty rather than the forty-eight a search at load time was allowed.
        /// Each level is asked four things at once - every hard rule, the fast road hardest,
        /// the fast road killing or sparing as the chapter needs, and no easier than the
        /// level before while nearest its target - and among forty-eight maps there was
        /// often no map that answered all of them: levels landed a fifth off their target.
        /// The catalogue is built once; the cost is half an hour of an editor, not a
        /// player's loading screen.
        /// </summary>
        const int BuiltAttempts = 160;

        static LevelMap Best(int chapter, int level, bool wantKill, float floor, float aim, out float difficulty,
                             out bool killed, out string how)
        {
            var recipe = LevelMaps.Recipe(chapter, level);
            int seed = DeterministicRandom.SeedFor(chapter, level);
            float target = DifficultyCurve.Target(chapter, level);

            LevelMap best = null;
            float bestScore = float.MaxValue;
            difficulty = 1f;
            killed = false;
            how = "no candidate passed the hard rules";

            for (int attempt = 0; attempt < BuiltAttempts; attempt++)
            {
                var map = TerrainGenerator.Generate(recipe, seed, null, attempt);
                if (map == null || !map.Accepted) continue;

                var judged = LevelMaps.Judge(map, chapter, level, recipe.RoutesOwed);
                if (!judged.Hard) continue;

                float score = Math.Abs(judged.Difficulty - aim);
                if (!judged.Treacherous) score += 10f;

                // No easier than the level before: the rule itself. Weighed above the fast
                // road's quota, which asks for about half and can give a level either way,
                // and far below the fast road being the hardest at all.
                //
                // The floor never above the level's own target and tolerance, though. A
                // level that overshot would otherwise lift every level after it: the third
                // chapter climbed from 53 per cent at 3-3 to 84 at 3-10 that way, each level
                // held to the one before rather than to its place on the curve.
                float held = Math.Min(floor, aim + DifficultyCurve.Tolerance);
                if (judged.Difficulty < held) score += 0.3f + (held - judged.Difficulty);

                // And no harder than the tolerance allows, on the same footing. Without it a
                // level with no map near its target and the right fast road took one a dozen
                // points over rather than break the fast road's quota - which is how 3-3 got
                // to 53 against 41, and the chapter after it.
                float over = judged.Difficulty - (aim + DifficultyCurve.Tolerance);
                if (over > 0f) score += 0.3f + over;

                // The fast road ends the ordinary escort's run about every other level: a
                // strong preference, weighed above a few points of difficulty. See the note
                // where it is asked for.
                if (judged.FastLost != wantKill) score += 0.2f;

                if (score >= bestScore) continue;

                bestScore = score;
                best = map;
                difficulty = judged.Difficulty;
                killed = judged.FastLost;
                how = (judged.Treacherous ? "" : "fast road NOT the hardest, ")
                      + (judged.FastLost == wantKill ? "" : (wantKill ? "fast road wanted a kill, " : "fast road wanted to spare, "))
                      + (judged.FastLost ? "fast kills, " : "fast spares, ")
                      + $"attempt {attempt}";
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
