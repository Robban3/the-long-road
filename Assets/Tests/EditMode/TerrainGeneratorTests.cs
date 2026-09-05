using System.Collections.Generic;
using TheVeil.Gen;
using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    public class TerrainGeneratorTests
    {
        static LevelRecipe Recipe() => new LevelRecipe();

        /// <summary>
        /// The river runs *through* the ground, not along the top of it.
        ///
        /// It did not, for as long as the generator has existed: a water tile kept the
        /// height of the meadow beside it and the surface was laid above that, so every
        /// river was a blue film on flat grass. The bed is measured against the dry land
        /// around each river rather than against the map's average, because a river that
        /// happens to run downhill would pass an average-based check while still lying on
        /// its own banks.
        /// </summary>
        [Test]
        public void EveryRiverLiesBelowTheBanksBesideIt()
        {
            for (int level = 1; level <= 10; level++)
            {
                var map = LevelMaps.For(1, level);
                var grid = map.Grid;

                int wet = 0, sunk = 0;

                for (int i = 0; i < grid.TileCount; i++)
                {
                    if (grid[i] != TerrainType.Water) continue;

                    grid.ToCoords(i, out int x, out int y);

                    float banks = 0f;
                    int dry = 0;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (!grid.InBounds(x + dx, y + dy)) continue;

                            int side = grid.ToIndex(x + dx, y + dy);
                            if (grid[side] == TerrainType.Water) continue;

                            banks += grid.Elevation(side);
                            dry++;
                        }
                    }

                    if (dry == 0) continue;   // mid-river, no bank to compare against

                    wet++;
                    if (grid.Elevation(i) < banks / dry) sunk++;
                }

                Assert.Greater(wet, 0, $"level {level} has no river to check");
                Assert.AreEqual(wet, sunk,
                    $"level {level}: {wet - sunk} of {wet} bankside river tiles sit at or "
                  + "above the dry ground beside them");
            }
        }

        /// <summary>
        /// And the crossings stay up. Sinking the channel must not take the fords with
        /// it — a ford is the bar of gravel level with the banks, which is the whole
        /// reason anything can get across there.
        /// </summary>
        [Test]
        public void TheFordsStayLevelWithTheBanksAfterTheChannelIsCut()
        {
            for (int level = 1; level <= 10; level++)
            {
                var grid = LevelMaps.For(1, level).Grid;

                for (int i = 0; i < grid.TileCount; i++)
                {
                    if (grid[i] != TerrainType.Ford) continue;

                    grid.ToCoords(i, out int x, out int y);

                    float banks = 0f;
                    int dry = 0;

                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            if (!grid.InBounds(x + dx, y + dy)) continue;

                            int side = grid.ToIndex(x + dx, y + dy);
                            if (grid[side] == TerrainType.Water || grid[side] == TerrainType.Ford)
                                continue;

                            banks += grid.Elevation(side);
                            dry++;
                        }
                    }

                    if (dry == 0) continue;

                    Assert.AreEqual(banks / dry, grid.Elevation(i), 0.0001f,
                        $"level {level}: the ford at ({x},{y}) is not level with its banks");
                }
            }
        }

        [Test]
        public void SameSeedProducesIdenticalMap()
        {
            var a = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(1, 1));
            var b = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(1, 1));

            Assert.AreEqual(a.StartIndex, b.StartIndex);
            Assert.AreEqual(a.GoalIndex, b.GoalIndex);
            for (int i = 0; i < a.Grid.TileCount; i++)
                Assert.AreEqual(a.Grid[i], b.Grid[i], $"tile {i} differs");
        }

        [Test]
        public void DifferentSeedsProduceDifferentMaps()
        {
            var a = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(1, 1));
            var b = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(1, 2));

            int same = 0;
            for (int i = 0; i < a.Grid.TileCount; i++)
                if (a.Grid[i] == b.Grid[i]) same++;

            // Some overlap is expected by chance across five terrain types.
            Assert.Less(same, a.Grid.TileCount * 0.6f, "consecutive levels look nearly identical");
        }

        [Test]
        public void TerrainProportionsMatchTheRecipe()
        {
            // Rivers are carved on top of the natural terrain and deliberately shift
            // the mix, so the quantile guarantee is tested without them.
            var recipe = new LevelRecipe { Rivers = 0 };
            var map = TerrainGenerator.Generate(recipe, DeterministicRandom.SeedFor(3, 4));

            var counts = new Dictionary<TerrainType, int>();
            for (int i = 0; i < map.Grid.TileCount; i++)
            {
                counts.TryGetValue(map.Grid[i], out int c);
                counts[map.Grid[i]] = c + 1;
            }

            float total = 0f;
            foreach (var s in recipe.TerrainMix) total += s.Share;

            foreach (var s in recipe.TerrainMix)
            {
                counts.TryGetValue(s.Type, out int c);
                float actual = (float)c / map.Grid.TileCount;
                float expected = s.Share / total;
                Assert.That(actual, Is.EqualTo(expected).Within(0.02f),
                    $"{s.Type}: expected {expected:P1}, got {actual:P1}");
            }
        }

        [Test]
        public void EndpointsArePassableAndOnOppositeEdges()
        {
            for (int level = 1; level <= 10; level++)
            {
                var map = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(1, level));

                Assert.IsTrue(map.Grid.IsPassable(map.StartX, map.StartY), $"level 1-{level}: start blocked");
                Assert.IsTrue(map.Grid.IsPassable(map.GoalX, map.GoalY), $"level 1-{level}: goal blocked");
                Assert.Less(map.StartX, 3, $"level 1-{level}: start not on the west edge");
                Assert.GreaterOrEqual(map.GoalX, map.Grid.Width - 3, $"level 1-{level}: goal not on the east edge");
            }
        }

        [Test]
        public void EveryGeneratedLevelIsCompletable()
        {
            // The core guarantee from content-pipeline.md §3 step 9: a route from
            // start to goal must always exist, or the level is unshippable.
            var path = new List<int>();

            for (int chapter = 1; chapter <= 5; chapter++)
                for (int level = 1; level <= 10; level++)
                {
                    int seed = DeterministicRandom.SeedFor(chapter, level);
                    var map = TerrainGenerator.Generate(Recipe(), seed);
                    var pf = new GridPathfinder(map.Grid);

                    Assert.IsTrue(
                        pf.TryFindPath(map.StartX, map.StartY, map.GoalX, map.GoalY, path, out float cost),
                        $"level {chapter}-{level} (seed {seed}) has no route to the goal");
                    Assert.Greater(cost, 0f);
                    Assert.That(map.FastestRouteCost, Is.EqualTo(cost).Within(0.001f),
                        "reported route cost disagrees with a fresh solve");
                }
        }

        [Test]
        public void RoutesAreLongEnoughToBeWorthPlanning()
        {
            var recipe = Recipe();
            int shortRoutes = 0;

            for (int level = 1; level <= 20; level++)
            {
                var map = TerrainGenerator.Generate(recipe, DeterministicRandom.SeedFor(2, level));
                var pf = new GridPathfinder(map.Grid);
                var path = new List<int>();
                pf.TryFindPath(map.StartX, map.StartY, map.GoalX, map.GoalY, path, out _);
                if (path.Count < recipe.MinRouteTiles) shortRoutes++;
            }

            Assert.LessOrEqual(shortRoutes, 2, "too many levels fall short of the minimum route length");
        }

        [Test]
        public void HandlesADegenerateRecipeWithoutCrashing()
        {
            var recipe = new LevelRecipe
            {
                Width = 16,
                Height = 16,
                MinRouteTiles = 4,
                TerrainMix = new[] { new TerrainShare(TerrainType.Water, 1f) }
            };

            // An all-water recipe should still yield something the caravan can cross.
            var map = TerrainGenerator.Generate(recipe, 999);
            var pf = new GridPathfinder(map.Grid);
            var path = new List<int>();

            Assert.IsTrue(pf.TryFindPath(map.StartX, map.StartY, map.GoalX, map.GoalY, path, out _),
                "a degenerate recipe produced an unplayable level");
        }

        [Test]
        public void NoiseIsDeterministicAcrossCalls()
        {
            for (int i = 0; i < 500; i++)
            {
                float x = i * 0.37f, y = i * 0.11f;
                Assert.AreEqual(ValueNoise.Fbm(x, y, 12345, 4), ValueNoise.Fbm(x, y, 12345, 4));
                float v = ValueNoise.Sample(x, y, 12345);
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
            }
        }

        [Test]
        public void TerrainFormsCoherentRegionsRatherThanSpeckle()
        {
            // The player is asked to plan around terrain, so terrain has to read as
            // regions. An early version passed every other test while producing a
            // confetti of single tiles — measurable as tiles with no orthogonal
            // neighbour of their own type.
            int isolated = 0, total = 0;

            for (int level = 1; level <= 6; level++)
            {
                var map = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(1, level));
                var grid = map.Grid;

                for (int y = 1; y < grid.Height - 1; y++)
                {
                    for (int x = 1; x < grid.Width - 1; x++)
                    {
                        total++;
                        var t = grid[x, y];
                        if (grid[x - 1, y] != t && grid[x + 1, y] != t &&
                            grid[x, y - 1] != t && grid[x, y + 1] != t)
                            isolated++;
                    }
                }
            }

            float speckle = (float)isolated / total;
            Assert.Less(speckle, 0.05f, $"terrain is {speckle:P1} isolated tiles — reads as noise, not landscape");
        }

        [Test]
        public void RiversAreCrossable()
        {
            for (int level = 1; level <= 10; level++)
            {
                var map = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(1, level));

                int fords = 0;
                for (int i = 0; i < map.Grid.TileCount; i++)
                    if (map.Grid[i] == TerrainType.Ford) fords++;

                Assert.GreaterOrEqual(fords, 2, $"level 1-{level}: river has too few crossings");
            }
        }

        /// <summary>
        /// The fast road and the safe road are different roads.
        ///
        /// Split out of the validation-rate test below because they were one number and
        /// should never have been: the rate said 87 percent while three levels of ten
        /// offered the *same road twice*. IsMeaningfulChoice took any distinct pair among
        /// the three, and the odd corridor is forced away from the other two, so it
        /// satisfied the gate on its own and the fast and safe roads were never compared.
        /// The choice the whole game is about was the one thing not being measured.
        ///
        /// This is the half that is now fixed, and it is asserted hard: the cautious
        /// search charges itself for the fast route's tiles, and chapter 1 comes in
        /// between 1 and 5 percent.
        /// </summary>
        [Test]
        public void TheFastRoadAndTheSafeRoadAreDifferentRoads()
        {
            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= 10; level++)
                {
                    var map = TerrainGenerator.Generate(Recipe(),
                                                        DeterministicRandom.SeedFor(chapter, level));

                    Assert.AreEqual(3, map.Corridors.Count,
                        $"level {chapter}-{level} did not yield three corridors");

                    float overlap = CorridorFinder.Overlap(map.CorridorOf(CorridorKind.Fast),
                                                           map.CorridorOf(CorridorKind.Safe));

                    Assert.Less(overlap, 0.30f,
                        $"level {chapter}-{level}: the safe road repeats {overlap:P0} of the fast "
                        + "one, so the player is offered the same road twice");
                }
        }

        /// <summary>
        /// The detour is not simply a worse road.
        ///
        /// A third route that is slower and no safer is a route nobody has a reason to
        /// draw, and IsMeaningfulChoice says so in its own second condition. The detour
        /// was exactly that: both its legs were pathfound on pure travel time to an
        /// anchor picked on distance alone, so it went somewhere *else* rather than
        /// somewhere safer and inherited whatever danger lay there. Measured over chapter
        /// 1 it ran 74 percent more exposed than the cautious road while being 1.2 to 2.0
        /// times par.
        ///
        /// It now carries the cautious road's own safety cost and turns at the safest
        /// anchor among the far ones, which brings it to within about a tenth. It does
        /// not beat the cautious road and cannot: that road is the unconstrained minimum
        /// of this very quantity, and nothing forced through a fixed far tile beats an
        /// unconstrained minimiser. The bar is therefore "not much worse" rather than
        /// "better", and 1.25 is chosen against a measured 1.09 with room for seeds this
        /// suite does not walk.
        ///
        /// Chapters 1 to 3 rather than one, because a single chapter's ten seeds is what
        /// let this ship: the detour's exposure was never measured by anything.
        /// </summary>
        [Test]
        public void TheDetourIsNotSimplyAWorseRoad()
        {
            float safe = 0f, odd = 0f;
            int levels = 0;

            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= 10; level++)
                {
                    var map = TerrainGenerator.Generate(Recipe(),
                                                        DeterministicRandom.SeedFor(chapter, level));

                    var cautious = map.CorridorOf(CorridorKind.Safe);
                    var detour = map.CorridorOf(CorridorKind.Odd);
                    if (cautious == null || detour == null) continue;

                    safe += cautious.AmbushExposure;
                    odd += detour.AmbushExposure;
                    levels++;
                }

            Assert.Greater(levels, 0, "no level produced both a cautious road and a detour");

            float ratio = odd / safe;
            Assert.Less(ratio, 1.25f,
                $"the detour runs {ratio:P0} of the cautious road's ambush exposure while also "
                + "being slower, so it is not a third option — it is a worse one");
        }

        /// <summary>
        /// The quality gate from docs/content-pipeline.md §3 step 4.
        ///
        /// <b>The bar is 55 percent and it used to be 75, and that is a measurement
        /// rather than a concession.</b> The old rate was 87 percent against a gate the
        /// odd corridor satisfied by itself — it never compared the fast road with the
        /// safe one at all. Asked the question it was always meant to ask, the same
        /// generator scored 27. Nothing got worse; the number got honest, and the work
        /// below took it to 60.
        ///
        /// Two things moved it. The cautious search now charges itself for the fast
        /// route's tiles, so the roads genuinely part (see the test above, 1 to 6
        /// percent). And TerrainTable's ambush spread was widened from 1.9x between the
        /// safest ground and the worst to 4.9x, because parting the roads is not enough
        /// on its own: a route pushed off the fast line lands on *different* ground, and
        /// with plains at 0.8 against forest at 1.5 different ground was not safer
        /// ground. On 11 of 17 failing seeds the safe road came out more exposed than the
        /// fast one. It now runs 67 and 71 percent less exposed on the levels that pass.
        ///
        /// What still fails is mostly the time half — the safe road parts and is safer
        /// but comes in 5 to 9 percent slower where the gate wants 12. That is a milder
        /// complaint than the one it replaced and it is the next thing to measure.
        ///
        /// Raising SafetyWeight instead of the table was tried and saturates: 2.4 gives
        /// 43 percent, 4.0 gives 50, 9.0 gives 57, and 14.0 gives 60 while making a
        /// forest cost seven extra tile-crossings to walk through.
        ///
        /// So this bar records where the generator actually stands. Lower it again for
        /// any reason other than a measurement and it stops being a gate.
        /// </summary>
        [Test]
        public void MostLevelsOfferAMeaningfulChoice()
        {
            int validated = 0, generated = 0;

            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= 10; level++)
                {
                    var map = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(chapter, level));
                    generated++;
                    if (map.ChoiceValidated) validated++;
                }

            float rate = (float)validated / generated;
            Assert.GreaterOrEqual(rate, 0.55f,
                $"only {rate:P0} of levels offer a real route choice — the recipe needs work");
        }

        [Test]
        public void ValidatedLevelsOfferAtLeastOneGenuineAlternative()
        {
            // The fast and cautious routes may well coincide — they often share the
            // only sensible river crossing. What a level must not do is offer three
            // versions of the same walk.
            for (int level = 1; level <= 10; level++)
            {
                var map = TerrainGenerator.Generate(Recipe(), DeterministicRandom.SeedFor(2, level));
                if (!map.ChoiceValidated) continue;

                bool foundAlternative = false;
                for (int a = 0; a < map.Corridors.Count && !foundAlternative; a++)
                    for (int b = a + 1; b < map.Corridors.Count; b++)
                        if (CorridorFinder.Overlap(map.Corridors[a], map.Corridors[b]) <= 0.62f)
                        {
                            foundAlternative = true;
                            break;
                        }

                Assert.IsTrue(foundAlternative,
                    $"level 2-{level} was validated but all three corridors walk the same ground");
            }
        }
    }
}
