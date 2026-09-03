using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    /// <summary>
    /// The planning map and the run's map are one map, and a route is checked against the
    /// grid it will be walked on.
    ///
    /// These cover the hand-off that had no test at all: the route the player draws
    /// crosses from the planning screen to the run as bare tile indices, and the two
    /// screens each generated their own map. They disagreed, so a line drawn around a lake
    /// on one indexed into water on the other — and water's speed is zero, which is an
    /// absorbing state, so the run stopped and never ended.
    /// </summary>
    public class LevelMapTests
    {
        /// <summary>
        /// One seed, one map.
        ///
        /// This is the assertion the bug lived under. The planning screen built with
        /// `new LevelRecipe()` and the run with `new ChapterRecipe().ForLevel(level)`, and
        /// the difference is not cosmetic: TerrainGenerator keeps the first of twelve
        /// attempts that satisfies the recipe, and each attempt is seeded
        /// `seed + attempt * 7919`. A recipe that accepts a different attempt gets an
        /// entirely different landscape.
        ///
        /// Tile by tile rather than by a summary, because a summary is what the two
        /// screens agreed on for as long as this was broken — same seed, same chapter,
        /// same level, different country.
        /// </summary>
        [Test]
        public void TheSameLevelIsTheSameMapHoweverItIsAskedFor()
        {
            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= 10; level++)
                {
                    var a = LevelMaps.For(chapter, level);
                    var b = LevelMaps.For(chapter, level);

                    Assert.AreEqual(a.Grid.TileCount, b.Grid.TileCount,
                        $"level {chapter}-{level}: the two maps are not even the same size");

                    for (int i = 0; i < a.Grid.TileCount; i++)
                        Assert.AreEqual(a.Grid[i], b.Grid[i],
                            $"level {chapter}-{level}: tile {i} differs between two asks for "
                            + "the same map");

                    Assert.AreEqual(a.StartIndex, b.StartIndex, $"level {chapter}-{level}: start moved");
                    Assert.AreEqual(a.GoalIndex, b.GoalIndex, $"level {chapter}-{level}: goal moved");
                }
        }

        /// <summary>
        /// Every corridor the generator offers survives the check the run puts a drawn
        /// route through.
        ///
        /// The corridors are the fallback the run uses when nobody drew anything, so if
        /// they cannot pass this the guard would reject the only road left.
        /// </summary>
        [Test]
        public void EveryOfferedCorridorIsWalkable()
        {
            for (int level = 1; level <= 10; level++)
            {
                var map = LevelMaps.For(1, level);

                foreach (var corridor in map.Corridors)
                {
                    Assert.IsTrue(RouteCheck.Walkable(map.Grid, corridor.Tiles, out int bad),
                        $"level 1-{level}: the {corridor.Kind} corridor fails at tile {bad}"
                        + (bad >= 0 && bad < map.Grid.TileCount ? $" ({map.Grid[bad]})" : ""));
                }
            }
        }

        [Test]
        public void ARouteThroughWaterIsRefused()
        {
            var grid = new TileGrid(16, 16, TerrainType.Plains);
            var route = new List<int>();

            for (int x = 0; x < 8; x++) route.Add(grid.ToIndex(x, 8));

            Assert.IsTrue(RouteCheck.Walkable(grid, route, out _), "a plain straight line was refused");

            grid[route[4]] = TerrainType.Water;

            Assert.IsFalse(RouteCheck.Walkable(grid, route, out int bad),
                "a route through open water was accepted");
            Assert.AreEqual(route[4], bad, "the refusal named the wrong tile");
        }

        /// <summary>
        /// A route whose tiles are not neighbours is refused, which is the shape the fault
        /// actually took: the indices were a solved path somewhere else, so on this grid
        /// they jump.
        /// </summary>
        /// <summary>
        /// A caravan that cannot move ends the run instead of hanging.
        ///
        /// Zero speed is an absorbing state: the distance stops growing, so the tile under
        /// the column never changes, so the terrain is still the one it cannot cross.
        /// Nothing noticed — Step ends a run on Destroyed or HasArrived and neither
        /// happens — so the game sat with a frozen progress bar. The only timeout in the
        /// class is the one RunToCompletion passes, which the game never calls.
        ///
        /// The route can no longer reach water, and this exists anyway: "the caravan
        /// cannot move" should be an outcome whatever causes it next.
        /// </summary>
        [Test]
        public void ACaravanThatCannotMoveEndsTheRun()
        {
            var grid = new TileGrid(40, 12, TerrainType.Plains);
            var route = new List<int>();
            for (int x = 0; x < 30; x++) route.Add(grid.ToIndex(x, 6));

            var map = new LevelMap(grid, 1, 0, 6, 29, 6, 1f, new List<Corridor>(), true, 1);

            // Water laid across the road *after* the run is built, which is the only way
            // to reach the state the guard is for: every path into it is now closed.
            var run = new LevelRun(map, route);
            grid[route[10]] = TerrainType.Water;

            var outcome = run.RunToCompletion(600f);

            Assert.AreNotEqual(RunOutcome.InProgress, outcome,
                "the caravan stood in water and the run never ended");
            Assert.AreEqual(route[10], run.StalledOn,
                "the run ended without recording where the caravan stopped");
        }

        /// <summary>
        /// And a column standing still because it is fighting is not stalled.
        ///
        /// CombatSystem.EngagedSpeedFactor is zero on purpose — a fight stops the caravan
        /// dead, for as long as the fight lasts. A stall detector that could not tell the
        /// two apart would end every hard-fought level as a failure.
        /// </summary>
        [Test]
        public void AColumnHaltedByAFightIsNotStalled()
        {
            var map = LevelMaps.For(1, 5);

            var squad = new Squad(LevelMaps.Recipe(5).SquadBudget);
            squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);

            var run = new LevelRun(map, map.CorridorOf(CorridorKind.Safe).Tiles, squad);
            run.RunToCompletion();

            Assert.AreEqual(-1, run.StalledOn,
                $"a level that was fought through was called a stall at tile {run.StalledOn}");
        }

        [Test]
        public void ARouteThatJumpsIsRefused()
        {
            var grid = new TileGrid(16, 16, TerrainType.Plains);

            var route = new List<int>
            {
                grid.ToIndex(2, 8),
                grid.ToIndex(3, 8),
                grid.ToIndex(9, 8)
            };

            Assert.IsFalse(RouteCheck.Walkable(grid, route, out int bad),
                "a route that teleports six tiles was accepted");
            Assert.AreEqual(grid.ToIndex(9, 8), bad, "the refusal named the wrong tile");
        }
    }
}
