using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    /// <summary>
    /// The placer's promises, checked the way the player will break them: by drawing
    /// routes it never saw.
    ///
    /// Every test that samples routes seeds its own stream from the level seed XOR a
    /// constant, so the routes are deterministic but are not the ones the placer
    /// optimised against. Checking against the placer's own sample would only prove it
    /// can hit a target it chose.
    /// </summary>
    public class EncounterPlacerTests
    {
        const int FreshRoutes = 40;
        const int MinEncounters = EncounterPlacer.MinEncounters;

        /// <summary>
        /// The floor measured against routes the placer never saw.
        ///
        /// One below <see cref="EncounterPlacer.MinEncounters"/>, and measured rather
        /// than chosen. The placer proves its case over the 32 routes it sampled and
        /// repairs to one above the promise so the unsampled ones keep it too, but a
        /// line drawn between the samples can still come out a group short. Raising
        /// the repair target further does not buy it back: at 7 the failures move to
        /// other levels, at 8 generation time triples and levels start failing
        /// validation outright.
        ///
        /// It was 3 while the repair loop livelocked, and levels were shipping at 2.
        /// </summary>
        const int WorstCaseEncounters = MinEncounters - 1;

        static LevelRecipe Recipe() => new LevelRecipe();

        static LevelMap Level(int chapter, int level, LevelRecipe recipe = null)
            => TerrainGenerator.Generate(recipe ?? Recipe(), DeterministicRandom.SeedFor(chapter, level));

        static List<List<int>> FreshSample(LevelMap map)
            => EncounterPlacer.SampleRoutes(map.Grid, map.Corridors,
                                            new DeterministicRandom(map.Seed ^ 0x5A5A),
                                            map.StartIndex, map.GoalIndex, FreshRoutes);

        [Test]
        public void NoDrawnRouteWalksThroughAnEmptyLevel()
        {
            // The promise the whole route-drawing mechanic rests on. If this fails, a
            // player who happens to draw between the groups gets a level with no game
            // in it, and the freedom to draw is what let them.
            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);
                int worst = int.MaxValue;

                foreach (var route in FreshSample(map))
                    worst = System.Math.Min(worst, EncounterPlacer.MetGroups(map.Grid, route, map.Encounters).Count);

                Assert.GreaterOrEqual(worst, WorstCaseEncounters,
                    $"level 1-{level}: some drawn route met only {worst} groups");
            }
        }

        [Test]
        public void TheRepairLoopFinishesWhatItStarts()
        {
            // It used to livelock. The group just moved is the idlest group on the next
            // pass, because it went somewhere only one route reaches, so it was picked
            // again — traced over forty passes on 2-5, the same band of raiders moved
            // forty times while the worst route stayed pinned at four. Every repair in
            // the budget was being spent walking one group in a circle.
            //
            // What it asserts is the outcome rather than the repair count. Saturation
            // was the symptom — every failing level spent all twelve — but a level may
            // legitimately need most of them, and the levels here already use ten. The
            // thing livelock actually prevented is arriving, so that is what is checked.
            for (int level = 1; level <= 10; level++)
            {
                var layout = Level(1, level).Encounters;

                Assert.GreaterOrEqual(layout.MinEncounters, EncounterPlacer.RepairTarget,
                    $"level 1-{level}: repaired to {layout.MinEncounters} in "
                    + $"{layout.Repairs} moves, target is {EncounterPlacer.RepairTarget}");
            }
        }

        [Test]
        public void ALevelThatCannotKeepThePromiseIsRolledAgain()
        {
            // The generator used to re-roll on IsMeaningfulChoice alone — whether the
            // three corridors it found differ from each other, which stopped being a
            // question the moment the player was handed a pen. The promise that
            // replaced it was not a criterion at all, so levels shipped broken.
            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= 10; level++)
                    Assert.IsTrue(Level(chapter, level).Encounters.EncountersValidated,
                        $"level {chapter}-{level} shipped without keeping its promise");
        }

        [Test]
        public void TheBudgetCeilingHolds()
        {
            // Repairs move groups rather than add them for exactly this reason. The
            // first version added, and chapter 1 came out between 13 and 71 percent
            // over budget — which §6 of the status notes records as the thing that
            // turned 1-6 from winnable to unsurvivable.
            var recipe = Recipe();

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level, recipe);
                Assert.LessOrEqual(map.Encounters.TotalPoints, recipe.EnemyBudget,
                    $"level 1-{level} spent {map.Encounters.TotalPoints} of {recipe.EnemyBudget}");
            }
        }

        [Test]
        public void EveryFordIsGuarded()
        {
            // The river crosses the caravan's travel and can only be forded at its
            // crossings, so a guard on each is the one placement no drawn line avoids.
            int levelsWithFords = 0;

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);

                bool hasFord = false;
                for (int i = 0; i < map.Grid.TileCount; i++)
                    if (map.Grid[i] == TerrainType.Ford) { hasFord = true; break; }

                if (!hasFord) continue;
                levelsWithFords++;

                Assert.Greater(map.Encounters.FordGuards, 0,
                    $"level 1-{level} has fords and none of them is watched");

                foreach (var spawn in map.Encounters.Enemies)
                    if (spawn.Origin == PlacementOrigin.Guard)
                        Assert.AreEqual(TerrainType.Ford, map.Grid[spawn.Tile],
                            "a ford guard is standing somewhere other than on its ford");
            }

            Assert.Greater(levelsWithFords, 5, "too few levels had a river to check");
        }

        [Test]
        public void ThreatFollowsFastGround()
        {
            // The corridor rule restated per tile: the quick way is the dangerous way.
            // Enemies should sit on ground that is faster than the map's average, or
            // the trade the whole route choice rests on has quietly inverted.
            int checkedLevels = 0;

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);
                if (map.Encounters.Enemies.Count == 0) continue;

                float occupied = 0f;
                foreach (var spawn in map.Encounters.Enemies)
                    occupied += TerrainTable.Speed(map.Grid[spawn.Tile]);
                occupied /= map.Encounters.Enemies.Count;

                float everywhere = 0f;
                int passable = 0;
                for (int i = 0; i < map.Grid.TileCount; i++)
                {
                    if (!map.Grid.IsPassable(i)) continue;
                    everywhere += TerrainTable.Speed(map.Grid[i]);
                    passable++;
                }
                everywhere /= passable;

                Assert.Greater(occupied, everywhere,
                    $"level 1-{level}: enemies sit on slower ground ({occupied:F2}) " +
                    $"than the map average ({everywhere:F2})");
                checkedLevels++;
            }

            Assert.Greater(checkedLevels, 5);
        }

        [Test]
        public void EveryGroupWatchesAStretchOfCountry()
        {
            var map = Level(1, 3);
            Assert.Greater(map.Encounters.Enemies.Count, 0);

            foreach (var spawn in map.Encounters.Enemies)
            {
                Assert.GreaterOrEqual(spawn.Territory, EncounterPlacer.TerritoryMinTiles);
                Assert.LessOrEqual(spawn.Territory, EncounterPlacer.TerritoryMaxTiles);
            }
        }

        [Test]
        public void ADrawnRouteCanAlwaysEarnTheUpgradeFloor()
        {
            // A route that cannot pay for two upgrades leaves the player at the level's
            // last fight with an army they had no way to improve. That is broken rather
            // than hard, and the caches exist to prevent exactly it.
            var recipe = Recipe();

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level, recipe);
                if (!map.Encounters.SilverValidated) continue;

                foreach (var route in FreshSample(map))
                {
                    int earned = 0;
                    foreach (int index in EncounterPlacer.MetGroups(map.Grid, route, map.Encounters))
                        earned += EnemyTable.GroupSilver(map.Encounters.Enemies[index].Kind);

                    Assert.Greater(earned, 0,
                        $"level 1-{level}: a route earned nothing at all");
                }
            }
        }

        [Test]
        public void NothingWaitsInTheFirstStrides()
        {
            // Being ambushed before the caravan has moved is not a decision the player
            // could have made differently.
            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);
                var start = Vec2.FromTile(map.Grid, map.StartIndex);
                var goal = Vec2.FromTile(map.Grid, map.GoalIndex);

                foreach (var spawn in map.Encounters.Enemies)
                {
                    var position = Vec2.FromTile(map.Grid, spawn.Tile);
                    Assert.Greater(Vec2.Distance(position, start), TileGrid.TileSize * 4f,
                        $"level 1-{level}: a group is waiting on the start tile");
                    Assert.Greater(Vec2.Distance(position, goal), TileGrid.TileSize * 4f,
                        $"level 1-{level}: a group is waiting on the goal tile");
                }
            }
        }

        [Test]
        public void PlacementIsDeterministic()
        {
            // A level is a recipe plus a seed. If placement drifts, the seed stops
            // being the level.
            var a = Level(4, 7);
            var b = Level(4, 7);

            Assert.AreEqual(a.Encounters.Enemies.Count, b.Encounters.Enemies.Count);
            Assert.AreEqual(a.Encounters.Traps.Count, b.Encounters.Traps.Count);
            Assert.AreEqual(a.Encounters.TotalPoints, b.Encounters.TotalPoints);
            Assert.AreEqual(a.Encounters.MinEncounters, b.Encounters.MinEncounters);

            for (int i = 0; i < a.Encounters.Enemies.Count; i++)
            {
                Assert.AreEqual(a.Encounters.Enemies[i].Tile, b.Encounters.Enemies[i].Tile);
                Assert.AreEqual(a.Encounters.Enemies[i].Kind, b.Encounters.Enemies[i].Kind);
                Assert.AreEqual(a.Encounters.Enemies[i].Territory, b.Encounters.Enemies[i].Territory);
            }
        }
    }
}
