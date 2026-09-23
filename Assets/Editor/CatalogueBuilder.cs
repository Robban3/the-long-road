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

            // The built levels are chosen together, before anything is written: see Choose.
            LevelCatalogue.ClearTuning();
            var chosen = Choose(sheet);

            for (int chapter = 1; chapter <= LevelCatalogue.Chapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    // From nothing, every time. Reading the old catalogue here would let a
                    // stale answer copy itself into the new one, which is the one way a
                    // table like this goes wrong and stays wrong.
                    LevelMap map = chapter <= BuiltChapters && chosen.TryGetValue((chapter, level), out var pick)
                        ? pick
                        : Fresh(chapter, level, 0f);

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
        internal static float Calibrate(int chapter, int level, float aim, out float typical)
        {
            var recipe = LevelMaps.Uncalibrated(chapter, level);
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
            // A fifth to twice the formula. Half was the floor once, and from level 100 on
            // the calibration sat on it with the level still far too hard: the strength
            // formula is older than the escort's growth and outruns it over a thousand
            // levels. The range has to reach what the level actually needs.
            float low = 0.2f;
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

        /// <summary>
        /// And more where a level's hundred and sixty left too little to choose from.
        ///
        /// The fourth chapter kept as few as nineteen maps of a hundred and sixty - on the
        /// plains a prepared player loses more roads, and a map one of whose roads cannot be
        /// won is no candidate at all. With so few, the climb had nothing to step onto: 4-10
        /// came out at 45 per cent against a curve at 31, and the choice bought its way out
        /// with 3-9 easier than 3-8. So a level keeps being tried until it has enough maps,
        /// and enough of them near its target, or three times the usual number of attempts.
        /// </summary>
        const int MostAttempts = 480;

        /// <summary>Maps a level should have to choose from before the search stops.</summary>
        const int WantedCandidates = 60;

        /// <summary>
        /// Of those, how many should sit near its target with the fast road hardest - and
        /// near reaches further above the target than below it.
        ///
        /// <b>Above, because that is where the climb goes.</b> Every level has to beat the
        /// one before by the curve's rise, so the chosen levels drift a little over the
        /// curve and the next level needs maps above its target, not at it. Counted evenly,
        /// the search stopped with plenty at the target and nothing higher: 3-10 had maps
        /// at 30 and 31 per cent that only turned up after the hundred and sixtieth attempt,
        /// so the choice dropped it to 23, and 4-10 had to jump to 45.
        /// </summary>
        const int WantedNear = 20;

        /// <summary>How far under its target a map still counts as near.</summary>
        const float Near = DifficultyCurve.Tolerance / 2f;

        /// <summary>One attempt of a built level, measured.</summary>
        sealed class Candidate
        {
            public int Attempt;
            public LevelMaps.Judgement Judged;
            public float Cost;
        }

        /// <summary>
        /// The map every built level ships, chosen for all of them at once.
        ///
        /// <b>Together, not one at a time.</b> Chosen level by level, each had to be
        /// harder than the one already picked, and where no map sat just above it the level
        /// jumped - 2-7 from 35 to 41 per cent - and every level after inherited the jump,
        /// until the third chapter ended at 69 against a curve that asked for 47. Nothing
        /// chosen early could make room for what came later.
        ///
        /// So every built level is calibrated to its own target first and every one of its
        /// attempts measured; then one pass over the whole campaign finds the sequence that
        /// rises by at least a point every level and lies nearest the curve overall. A level
        /// may take a slightly harder map than its target if that is what keeps a later one
        /// from having to jump. It is a small problem - thirty levels, a hundred and sixty
        /// maps each - and it is solved exactly.
        ///
        /// What each candidate costs, largest first: failing a hard rule (never chosen), the
        /// fast road not the hardest road, the level before not being beaten by a point, then
        /// the fast road killing or sparing against the chapter's pattern (every second level
        /// kills, the first of a chapter spares), then the distance from the curve.
        /// </summary>
        static System.Collections.Generic.Dictionary<(int, int), LevelMap> Choose(StringBuilder sheet)
        {
            var levels = new System.Collections.Generic.List<(int Chapter, int Level)>();
            for (int chapter = 1; chapter <= BuiltChapters; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                    levels.Add((chapter, level));

            var candidates = new System.Collections.Generic.List<System.Collections.Generic.List<Candidate>>();
            var factors = new float[levels.Count];

            for (int i = 0; i < levels.Count; i++)
            {
                var (chapter, level) = levels[i];
                float target = DifficultyCurve.Target(chapter, level);

                // Rounded to what the catalogue writes, before anything is judged with it.
                // Judged at the exact factor and shipped at the written one, the levels came
                // out a point off here and there - 3-7 easier than 3-6 in the game, though
                // not in the sheet. CampaignRulesTests measures the shipped maps and caught it.
                float factor = (float)Math.Round(Calibrate(chapter, level, target, out float typical), 3);
                LevelCatalogue.Tune(chapter, level, factor);
                factors[i] = factor;

                var recipe = LevelMaps.Recipe(chapter, level);
                int seed = DeterministicRandom.SeedFor(chapter, level);

                var list = new System.Collections.Generic.List<Candidate>();

                int near = 0;

                for (int attempt = 0;
                     attempt < BuiltAttempts
                     || (attempt < MostAttempts && (list.Count < WantedCandidates || near < WantedNear));
                     attempt++)
                {
                    var map = TerrainGenerator.Generate(recipe, seed, null, attempt);
                    if (map == null || !map.Accepted) continue;

                    // Levelled as LevelMaps.For levels it, so the map judged is the map played.
                    Strongholds.Flatten(map, level);

                    var judged = LevelMaps.Judge(map, chapter, level, recipe.RoutesOwed);
                    if (!judged.Hard) continue;

                    // Inside the tolerance, or not at all.
                    //
                    // <b>Because the climb only goes one way.</b> Every level has to beat
                    // the one before, so a level that lands above its target takes every
                    // level after it with it - and a map's difficulty scatters several
                    // points either side of what the strength was calibrated for. Building
                    // the fifth chapter, the chain drifted five points over by 4-10 and
                    // twenty-two by 5-10, because nothing ever brought it back down. Held
                    // to the window, it cannot drift: the window itself rises faster than
                    // the step each level owes.
                    if (Math.Abs(judged.Difficulty - target) > DifficultyCurve.Tolerance) continue;

                    float cost = Math.Abs(judged.Difficulty - target);
                    if (!judged.Treacherous) cost += 10f;

                    list.Add(new Candidate { Attempt = attempt, Judged = judged, Cost = cost });
                    if (judged.Treacherous
                        && judged.Difficulty >= target - Near
                        && judged.Difficulty <= target + DifficultyCurve.Tolerance) near++;
                }

                sheet.AppendLine($"[Catalogue] {chapter}-{level}: {list.Count} candidates at strength "
                                 + $"x{factor:0.000}, typical map {typical:P0} against a target of {target:P0}");

                // Every candidate, so a choice that goes wrong can be followed by hand: its
                // difficulty, n where the fast road is not the hardest, k where it kills.
                var spread = new System.Collections.Generic.List<Candidate>(list);
                spread.Sort((a, b) => a.Judged.Difficulty.CompareTo(b.Judged.Difficulty));
                sheet.AppendLine($"[Catalogue] {chapter}-{level} candidates:"
                                 + string.Concat(spread.ConvertAll(c => $" {c.Judged.Difficulty * 100f:0.0}"
                                                                        + (c.Judged.Treacherous ? "" : "n")
                                                                        + (c.Judged.FastLost ? "k" : ""))));
                candidates.Add(list);
            }

            // The pass over the campaign: best[i][c, k] is the least total cost of any choice
            // of levels up to i that ends with candidate c of level i, having had k fast
            // roads kill the ordinary escort so far in this chapter.
            //
            // <b>The kills are a rule, not a preference.</b> They were a cost - a level whose
            // fast road killed where the pattern wanted it to spare paid a few points of
            // distance from the curve - and the choice simply bought its way out: rebuilt
            // with the fifth chapter, the third chapter came out with no killing fast road
            // at all and the fourth with eight. About half of them, in every chapter, is
            // what the player is promised, so it is carried in the state of the search and a
            // chapter that ends outside FewestKills..MostKills is not a chapter the choice
            // may make.
            int states = Campaign.LevelsPerChapter + 1;

            var best = new System.Collections.Generic.List<float[,]>();
            var from = new System.Collections.Generic.List<int[,]>();

            for (int i = 0; i < levels.Count; i++)
            {
                var list = candidates[i];
                var cost = new float[list.Count, states];
                var back = new int[list.Count, states];

                bool first = levels[i].Level == 1;
                bool last = levels[i].Level == Campaign.LevelsPerChapter;

                for (int c = 0; c < list.Count; c++)
                {
                    int killed = list[c].Judged.FastLost ? 1 : 0;

                    for (int k = 0; k < states; k++)
                    {
                        cost[c, k] = float.MaxValue;
                        back[c, k] = -1;

                        // The count this candidate leaves behind it: reset at a chapter's
                        // first level, carried on otherwise.
                        if (first && k != killed) continue;
                        if (last && (k < FewestKills || k > MostKills)) continue;

                        if (i == 0 || candidates[i - 1].Count == 0)
                        {
                            if (!first) continue;
                            cost[c, k] = list[c].Cost;
                            continue;
                        }

                        float least = float.MaxValue;
                        int leastFrom = -1;
                        var previous = candidates[i - 1];

                        for (int p = 0; p < previous.Count; p++)
                        {
                            int had = first ? 0 : k - killed;
                            if (!first && (had < 0 || had >= states)) continue;

                            float carried = first
                                ? Cheapest(best[i - 1], p, states)
                                : best[i - 1][p, had];

                            if (carried >= float.MaxValue) continue;

                            float need = previous[p].Judged.Difficulty + MinimumStep;
                            float step = list[c].Judged.Difficulty >= need - 0.0001f
                                ? 0f
                                : Dip + (need - list[c].Judged.Difficulty);

                            float total = carried + step;
                            if (total >= least) continue;

                            least = total;
                            leastFrom = p;
                        }

                        if (leastFrom < 0) continue;

                        cost[c, k] = least + list[c].Cost;
                        back[c, k] = leastFrom;
                    }
                }

                best.Add(cost);
                from.Add(back);
            }

            // Back from the cheapest end.
            var picks = new int[levels.Count];
            for (int i = 0; i < picks.Length; i++) picks[i] = -1;

            int end = levels.Count - 1;
            if (candidates[end].Count > 0)
            {
                int at = -1, state = -1;
                float cheapest = float.MaxValue;

                for (int c = 0; c < candidates[end].Count; c++)
                    for (int k = 0; k < states; k++)
                        if (best[end][c, k] < cheapest) { cheapest = best[end][c, k]; at = c; state = k; }

                // <b>Said out loud rather than left as an empty chapter.</b> The kills are a
                // hard rule and a hard rule can be impossible: a chapter whose maps never let
                // the fast road kill four times cannot be chosen at all, and the search would
                // otherwise come back with nothing and no reason.
                if (at < 0)
                    sheet.AppendLine($"[Catalogue] no chain keeps {FewestKills} to {MostKills} "
                                     + "killing fast roads in every chapter - nothing was written");

                for (int i = end; i >= 0 && at >= 0; i--)
                {
                    picks[i] = at;

                    int previous = from[i][at, state];
                    if (levels[i].Level == 1)
                    {
                        // A chapter's first level carries no count into the one before it:
                        // whichever state that level ended in is the one that was cheapest.
                        state = i > 0 ? Ended(best[i - 1], previous, states) : -1;
                    }
                    else
                    {
                        state -= candidates[i][at].Judged.FastLost ? 1 : 0;
                    }

                    at = previous;
                }
            }

            var chosen = new System.Collections.Generic.Dictionary<(int, int), LevelMap>();
            float before = 0f;
            int kills = 0;

            for (int i = 0; i < levels.Count; i++)
            {
                var (chapter, level) = levels[i];
                if (level == 1) kills = 0;

                if (picks[i] < 0)
                {
                    sheet.AppendLine($"[Catalogue] {chapter}-{level}: no candidate kept every hard rule");
                    continue;
                }

                var pick = candidates[i][picks[i]];
                var recipe = LevelMaps.Recipe(chapter, level);
                chosen[(chapter, level)] = TerrainGenerator.Generate(recipe, DeterministicRandom.SeedFor(chapter, level),
                                                                     null, pick.Attempt);

                if (pick.Judged.FastLost) kills++;
                float difficulty = pick.Judged.Difficulty;

                sheet.AppendLine($"[Catalogue] {chapter}-{level}: difficulty {difficulty:P0}, "
                                 + $"target {DifficultyCurve.Target(chapter, level):P0} - "
                                 + (pick.Judged.Treacherous ? "" : "fast road NOT the hardest, ")
                                 + (pick.Judged.FastLost ? "fast kills" : "fast spares")
                                 + $" ({kills} this chapter), strength x{factors[i]:0.000}, attempt {pick.Attempt}"
                                 + (i > 0 && difficulty < before + MinimumStep - 0.0001f ? "  <-- not harder than the level before" : ""));
                before = difficulty;
            }

            return chosen;
        }

        /// <summary>
        /// What breaking the climb costs in the choice: more than any distance from the
        /// curve, so the choice only ever takes it when there is no other way.
        ///
        /// Fifty, and it was one. One is more than any single level's distance from the
        /// curve and less than the sum of a chapter's: building the fifth chapter, the
        /// choice let 4-4 fall from 31 per cent to 25 because holding the climb there cost
        /// the ten levels after it a few points each. Every level harder than the last is a
        /// rule, not a preference, and the levels it would trade it for are all well inside
        /// the tolerance.
        /// </summary>
        const float Dip = 50f;

        /// <summary>How much harder than the level before each level must be: a fifth of a point.</summary>
        public const float MinimumStep = 0.002f;

        /// <summary>The fewest fast roads that may kill in a chapter.</summary>
        const int FewestKills = 4;

        /// <summary>And the most.</summary>
        const int MostKills = 6;

        /// <summary>The cheapest way any choice of the level before ended, over every count.</summary>
        static float Cheapest(float[,] best, int candidate, int states)
        {
            float cheapest = float.MaxValue;
            for (int k = 0; k < states; k++)
                if (best[candidate, k] < cheapest) cheapest = best[candidate, k];

            return cheapest;
        }

        /// <summary>Which count that cheapest way ended in.</summary>
        static int Ended(float[,] best, int candidate, int states)
        {
            int ended = -1;
            float cheapest = float.MaxValue;

            if (candidate < 0) return -1;

            for (int k = 0; k < states; k++)
                if (best[candidate, k] < cheapest) { cheapest = best[candidate, k]; ended = k; }

            return ended;
        }

        /// <summary>
        /// Unused now the kills are a rule: kept for the note it carries.
        ///
        /// What a fast road that kills where the pattern wanted it to spare, or the other
        /// way round, costs in the choice: ten points of distance from the curve.
        ///
        /// It was three, and once the curve was spread over a thousand levels and the
        /// first chapters became gentler, three was too little: the choice took maps a
        /// point or two nearer the curve over maps with the right fast road, and the fast
        /// road killed the ordinary escort on two levels of ten in the first and second
        /// chapters. About half, in every chapter, is the rule; a level a few points off
        /// the curve is the lesser fault.
        ///
        /// And ten was too much, once the fourth chapter was built: 4-10 took a map at 45
        /// per cent against a curve at 31 because it was the one whose fast road killed.
        /// Replayed offline from the build's own candidate lists (every candidate is in
        /// the sheet), five keeps every chapter at three to six kills, every level harder
        /// than the last, and no level more than six points off the curve.
        /// </summary>
        const float FastPattern = 0.05f;

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
