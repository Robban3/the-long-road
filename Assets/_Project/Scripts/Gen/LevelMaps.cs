using System;
using System.Collections.Generic;
using TheVeil.Sim;

namespace TheVeil.Gen
{
    /// <summary>
    /// The one place a level's map comes from.
    ///
    /// <b>There were two, and they disagreed.</b> The planning screen generated with
    /// <c>new LevelRecipe()</c> and the run with <c>new ChapterRecipe().ForLevel(level)</c>
    /// — the same seed through two different recipes. That is not a small difference:
    /// <see cref="TerrainGenerator.Generate"/> tries up to twelve terrain fields and keeps
    /// the first that satisfies the recipe's demands, and each attempt is seeded
    /// <c>seed + attempt * 7919</c>. Two recipes that accept different attempt numbers
    /// therefore produce two <i>unrelated landscapes</i> — different lakes, in different
    /// places.
    ///
    /// The route crosses between them as bare tile indices (App.ChosenRoute), and nothing
    /// on either side carries the map's identity. So a line drawn carefully around a lake
    /// on the planning map indexed into the run's own terrain, and could land in open
    /// water — where <c>TerrainTable.Speed(Water)</c> is zero, which is an absorbing
    /// state: the caravan stops, the tile under it never changes, and the run neither
    /// wins nor loses nor ends.
    ///
    /// The demands that decide which attempt wins are ordinary content settings —
    /// MinRouteTiles, EnemyBudget, TrapDensity, EnemyPool, EnemyStrength, RoutesOwed — so
    /// this could be broken again by tuning a number, and silently. It was: raising
    /// LevelRecipe.EnemyBudget from 100 to 120 while ChapterRecipe.EnemyBudgetStart stayed
    /// at 100 pushed level 1's two recipes apart, and the corridor and placer work moved
    /// the accept conditions themselves.
    ///
    /// Hence one function rather than a convention. A convention is what was already
    /// there.
    /// </summary>
    public static class LevelMaps
    {
        /// <summary>The map for one level, as both the planning screen and the run see it.</summary>
        /// <summary>
        /// The maps already worked out, because working one out is no longer cheap.
        ///
        /// <b>This regenerated on every call and that was affordable while generation was
        /// arithmetic.</b> It is not any more: a candidate is now driven down all three of
        /// its roads before it is accepted, and the planning map, the run, every report
        /// and a good part of the test suite all ask for the same level over and over.
        ///
        /// Keyed by chapter and level, which is the whole of a level's identity — the seed
        /// is derived from them.
        /// </summary>
        static readonly Dictionary<(int Chapter, int Level), LevelMap> _built
            = new Dictionary<(int, int), LevelMap>();

        public static LevelMap For(int chapter, int level)
        {
            if (_built.TryGetValue((chapter, level), out var known)) return known;

            // <b>Straight to the attempt the catalogue names, where it has one.</b>
            //
            // The search below asks whether a caravan can be got down a candidate, which
            // means driving one — and 1-10 wanted thirty-four candidates before it found
            // its map. Paying that on a player's device every time they open a chapter is
            // not a trade anybody would make. The search is done once by an editor tool and
            // its answer written down; see LevelCatalogue, and the signature that stops a
            // written answer being trusted after the rules it was written under have moved.
            int shipped = LevelCatalogue.Shipped(chapter, level);

            var recipe = Recipe(chapter, level);

            var map = TerrainGenerator.Generate(recipe,
                                                DeterministicRandom.SeedFor(chapter, level),
                                                candidate => chapter > DifficultyCurve.BuiltChapters
                                                    ? (RoadsThrough(candidate, chapter, level, recipe.RoutesOwed)
                                                       >= recipe.RoutesOwed ? TerrainGenerator.Accepted : 0)
                                                    : Gate(candidate, chapter, level, recipe.RoutesOwed),
                                                shipped);

            // The ground a castle stands on, levelled — after the generator has finished
            // with the map and can no longer see it. See Strongholds.Flatten for why a
            // courtyard cannot be both flat and above grade on a slope, and for why this
            // changes no decision: the two things that read elevation outside the
            // generator pick village and town sites, and neither is ever on the level a
            // keep is on.
            //
            // Here rather than in either caller, because this is the one door the planning
            // map and the run both come through. A level flattened for one and not the
            // other would be two different countries, which is the fault this whole file
            // exists to prevent.
            Strongholds.Flatten(map, level);

            _built[(chapter, level)] = map;
            return map;
        }

        /// <summary>
        /// Whether the escort the difficulty curve assumes can get down any of a
        /// candidate's roads.
        ///
        /// <b>The promise the generator makes and had never once checked.</b> It re-rolls
        /// for corridors that differ, for encounters worth the name, for crossings that
        /// can be waded — and for survivability it used an estimate: the points of the
        /// groups a route meets, against a band measured over chapter one. That estimate
        /// is why every change in this corner has cost a chapter. Straightening the
        /// corridors at their crossings took 2-7 and 1-10 from winnable to not and the
        /// estimate passed them both; the tests found it hours later, which is the wrong
        /// place and the wrong hour.
        ///
        /// So the question is asked properly: field the line ReferenceSquad says a player
        /// would have here, drive each corridor to the end, and ship the map only if one of
        /// them arrives. It is slow — three simulated runs per surviving candidate — and it
        /// is paid once per level, behind the cache above.
        ///
        /// One road, not three. A level where every road is comfortable is a level with no
        /// decision on it; what the chapter promises is that the decision has a right
        /// answer, not that every answer is right.
        /// </summary>
        public static bool Winnable(LevelMap map, int chapter, int level)
            => RoadsThrough(map, chapter, level, 1) >= 1;

        /// <summary>
        /// The whole of what a candidate has to answer, as a grade:
        /// <see cref="TerrainGenerator.Accepted"/> when it answers everything, less when it
        /// does not, and the less the worse.
        ///
        /// Hard, and a candidate failing them grades below any that passes them:
        /// - the escort the curve assumes gets down the roads the level owes;
        /// - a prepared player gets down every road (<see cref="EveryRoadWinnable"/>).
        ///
        /// Soft, and graded by how far off they are, so the search's compromise is the
        /// nearest miss rather than the first one:
        /// - the fast road is the hardest road on the level for the escort the curve
        ///   assumes - the one that looks easiest turns out worst, on every level;
        /// - the level is as hard as its place in the campaign says, within
        ///   <see cref="DifficultyCurve.Tolerance"/> (<see cref="DifficultyCurve"/>).
        /// </summary>
        public static int Gate(LevelMap map, int chapter, int level, int wanted)
            => Gate(map, chapter, level, wanted, 0f);

        /// <param name="floor">
        /// A difficulty the level may not come in under - the level before it, for a
        /// builder that walks the levels in order. Nothing passes one today: tried as a
        /// chain it ran away (see CatalogueBuilder.Best), and it is kept for a builder
        /// that can measure a level finely enough to hold one.
        /// </param>
        public static int Gate(LevelMap map, int chapter, int level, int wanted, float floor)
        {
            var judged = Judge(map, chapter, level, wanted);
            if (!judged.Hard) return judged.Through < wanted ? judged.Through : wanted;

            float target = DifficultyCurve.Target(chapter, level);

            // How far outside the window it lies: no easier than the level before and no
            // further from its target than the tolerance.
            float low = Math.Max(target - DifficultyCurve.Tolerance, floor);
            float high = target + DifficultyCurve.Tolerance;
            float off = judged.Difficulty < low ? low - judged.Difficulty
                      : judged.Difficulty > high ? judged.Difficulty - high
                      : 0f;

            if (judged.Treacherous && off <= 0f) return TerrainGenerator.Accepted;

            int grade = TerrainGenerator.Accepted / 2 - (int)(off * 40f) - (judged.Treacherous ? 0 : 15);
            return Math.Max(wanted + 1, grade);
        }

        /// <summary>What a candidate map is, measured: see <see cref="Judge"/>.</summary>
        public struct Judgement
        {
            /// <summary>Roads the escort the curve assumes got down.</summary>
            public int Through;

            /// <summary>Enough roads for it, and every road for a prepared player.</summary>
            public bool Hard;

            /// <summary>The fast road costs it the most of the three.</summary>
            public bool Treacherous;

            /// <summary>The share of the escort lost over the roads. See DifficultyCurve.</summary>
            public float Difficulty;

            /// <summary>The fast road ended the run of the escort the curve assumes.</summary>
            public bool FastLost;
        }

        /// <summary>
        /// Plays a candidate with the escort the curve assumes down every road, and with a
        /// prepared player down every road that one lost, and says what it found.
        /// </summary>
        public static Judgement Judge(LevelMap map, int chapter, int level, int wanted)
        {
            var judged = new Judgement { Difficulty = 1f };
            if (map?.Corridors == null || map.Corridors.Count == 0) return judged;

            var recipe = Recipe(chapter, level);
            int cleared = ReferenceSquad.LevelsCleared(chapter, level);

            float total = 0f;
            int counted = 0;
            float fast = -1f, others = float.MaxValue;
            var lost = new List<Corridor>();

            foreach (var corridor in map.Corridors)
            {
                var run = ReferenceSquad.Play(map, corridor.Tiles, recipe, cleared);
                bool arrived = run.RunToCompletion() == RunOutcome.Arrived;
                if (arrived) judged.Through++;
                else lost.Add(corridor);

                float left = arrived ? EscortLeft(run) : 0f;

                if (corridor.Kind == CorridorKind.Fast)
                {
                    fast = left;
                    judged.FastLost = !arrived;
                    continue;
                }

                // The difficulty is the other roads', not the fast one's. See DifficultyCurve.
                total += left;
                counted++;
                if (left < others) others = left;
            }

            judged.Difficulty = counted > 0 ? 1f - total / counted : 1f;

            // Ties allowed: a level where the fast road and another both cost everything
            // is not one where the fast road is kinder.
            judged.Treacherous = fast < 0f || fast <= others + 0.001f;

            if (judged.Through < wanted) return judged;

            // The prepared player only on the roads the ordinary one lost. A road the escort
            // the curve assumes gets down, the prepared one gets down too: it tries that
            // same line, shopping the same way, with at least as much bought between levels.
            // Asking again cost up to twenty-four runs a candidate for no answer, and the
            // search runs through dozens of candidates a level.
            foreach (var corridor in lost)
                if (!ReferenceSquad.Prepared(map, corridor.Tiles, recipe, cleared)) return judged;

            judged.Hard = true;
            return judged;
        }

        /// <summary>
        /// How hard a level is, as DifficultyCurve measures it: the share of the escort the
        /// player the curve assumes loses over the three roads, a road not got down counting
        /// as the whole of it.
        /// </summary>
        public static float Difficulty(LevelMap map, int chapter, int level)
        {
            if (map?.Corridors == null || map.Corridors.Count == 0) return 1f;

            var recipe = Recipe(chapter, level);
            int cleared = ReferenceSquad.LevelsCleared(chapter, level);
            float total = 0f;

            foreach (var corridor in map.Corridors)
            {
                var run = ReferenceSquad.Play(map, corridor.Tiles, recipe, cleared);
                if (run.RunToCompletion() == RunOutcome.Arrived) total += EscortLeft(run);
            }

            return 1f - total / map.Corridors.Count;
        }

        /// <summary>
        /// The share of the escort still standing when a run ends: health left against
        /// health it could have had, the fallen counting nothing.
        /// </summary>
        public static float EscortLeft(LevelRun run)
        {
            if (run?.Squad == null) return 0f;

            float hp = 0f, full = 0f;
            foreach (var group in run.Squad.Slots)
            {
                if (group == null) continue;
                if (group.Alive) hp += group.Hp;
                full += group.EffectiveMaxHp;
            }

            return full > 0f ? hp / full : 0f;
        }

        /// <summary>
        /// Whether every road on the map can be won by the escort the curve assumes, with
        /// the forge bought out.
        ///
        /// <b>The promise, as the generator's rule rather than as the dice's.</b> The fast
        /// road is meant to be the treacherous one and to kill the ordinary escort more
        /// often than the other two - and every level, on every road, is still meant to be
        /// winnable by a player with the right troops who has upgraded them far enough.
        /// Tuning the difficulty toward the first promise put a wall on 2-9: its fast road
        /// could not be won at any smithy the game sells. Nothing had checked, so nothing
        /// would have stopped it shipping.
        ///
        /// "The right troops, upgraded far enough" is ReferenceSquad.Prepared: the line
        /// chosen for the road, all the gold put into it between levels, and the run's
        /// silver spent at the field smithy as it comes in - the most a player can actually
        /// have, rather than a smithy bought out before the first silver was earned, which
        /// is what this first asked and nobody can field.
        ///
        /// Asked after the cheaper question has passed, because it is three more simulated
        /// runs and most candidates never get this far.
        /// </summary>
        public static bool EveryRoadWinnable(LevelMap map, int chapter, int level)
        {
            if (map?.Corridors == null) return false;

            var recipe = Recipe(chapter, level);
            int cleared = ReferenceSquad.LevelsCleared(chapter, level);

            foreach (var corridor in map.Corridors)
                if (!ReferenceSquad.Prepared(map, corridor.Tiles, recipe, cleared)) return false;

            return true;
        }

        /// <summary>
        /// How many of a level's roads the reference escort can actually be got down,
        /// counted up to <paramref name="wanted"/> and no further.
        ///
        /// <b>The generator had an estimate for this and the estimate was the thing that
        /// was wrong.</b> It summed the points of the groups a route meets and compared
        /// them against a band measured over chapter one, because driving a caravan per
        /// attempt was far too slow to do while a player waited. That reason is gone:
        /// nothing searches at load time any more, LevelCatalogue records the answer and
        /// the game reads it. What the estimate was still costing was plain once it could
        /// be seen - of sixty levels shipping a map the generator would not accept,
        /// thirty-four were rejected by arithmetic calling a road fatal that a driven
        /// caravan arrived down.
        ///
        /// Stops at <paramref name="wanted"/> because that is all any caller asks: the
        /// recipe owes two roads, or one through the escalation band, and the difference
        /// between two and three is a simulated run nobody reads.
        /// </summary>
        public static int RoadsThrough(LevelMap map, int chapter, int level, int wanted)
        {
            if (map?.Corridors == null || map.Corridors.Count == 0) return 0;

            var recipe = Recipe(chapter, level);

            // <b>The quietest road first, because this stops at the first one that works.</b>
            //
            // Asking in the order the finder returns them drives three runs on a level that
            // any of the three would have satisfied, and a run is the expensive thing here:
            // a level that needs thirty-four attempts pays for a hundred of them, and one
            // EditMode test went over three minutes and was killed. Sorted by the danger
            // the finder already measured, the first road asked is the one most likely to
            // answer yes, and the usual case is one run rather than three.
            //
            // A sort and not a guess: AmbushExposure is the generator's own reading of what
            // a route carries, and it is deterministic, so this changes how long the answer
            // takes and never what it is.
            var roads = new List<Corridor>(map.Corridors);
            roads.Sort((a, b) => a.AmbushExposure.CompareTo(b.AmbushExposure));

            int through = 0;

            foreach (var corridor in roads)
            {
                // As the player plays it: nothing from the field smithy at the start, and
                // the run's silver spent on it as it comes in. See FieldSmith.
                var run = ReferenceSquad.Play(map, corridor.Tiles, recipe,
                                              ReferenceSquad.LevelsCleared(chapter, level));
                if (run.RunToCompletion() == RunOutcome.Arrived) through++;

                if (through >= wanted) break;
            }

            return through;
        }

        /// <summary>
        /// The recipe that level is built from.
        ///
        /// Exposed because the run needs the same object for what is *in* the level —
        /// enemy strength, the squad's budget and posts — and reading those off a second
        /// recipe is the same class of fault as generating off one.
        ///
        /// By chapter as well as level. It took the level alone once, and chapter two's
        /// first level was chapter one's first level with snow on it.
        /// </summary>
        public static LevelRecipe Recipe(int chapter, int level)
        {
            var recipe = ChapterRecipe.For(chapter).ForLevel(level);

            // The strength the catalogue calibrated for this level. See LevelCatalogue.Factor.
            recipe.EnemyStrength *= LevelCatalogue.Factor(chapter, level);
            return recipe;
        }
    }
}
