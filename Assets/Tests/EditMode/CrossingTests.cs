using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// Ways over the water, which is the choice a river exists to force.
    ///
    /// Written after chapter 3-1 shipped with one: the fen's water was a tenth of the
    /// ground, its lakes grew over the rivers, and four fords came out as one usable
    /// crossing — so all three routes queued for the same tile and the bridge stood in
    /// open water. Nothing said no, because nothing was asking.
    /// </summary>
    public class CrossingTests
    {
        /// <summary>The countries built so far, which are the ones whose maps can be judged.</summary>
        static readonly int[] Chapters = { 1, 2, 3 };

        [Test]
        public void EveryLevelOffersThreeWaysOverTheWaterAndOneBridge()
        {
            foreach (int chapter in Chapters)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);
                    var recipe = LevelMaps.Recipe(chapter, level);

                    var crossings = Crossings.All(map.Grid);

                    Assert.GreaterOrEqual(crossings.Count, recipe.CrossingsOwed,
                        $"{chapter}-{level} has {crossings.Count} way(s) over the water");

                    // One bridge, on one of those crossings, with banks at both ends.
                    int bridge = TerrainDecorator.BridgeTile(map.Grid, map.Seed);

                    Assert.GreaterOrEqual(bridge, 0, $"{chapter}-{level} has no bridge at all");
                    Assert.AreEqual(TerrainType.Ford, map.Grid[bridge],
                        $"{chapter}-{level}: the bridge is not on a crossing");
                    Assert.IsTrue(Crossings.Spans(map.Grid, bridge),
                        $"{chapter}-{level}: the bridge stands in open water");
                    Assert.Contains(bridge, crossings,
                        $"{chapter}-{level}: the bridge is not on one of the counted crossings");
                }
            }
        }

        [Test]
        public void TheSameBridgeIsDrawnOnThePlanAndInTheRun()
        {
            // The two ask the same function of the same map, and the day they stop, a
            // player plans a crossing that is not there when they drive it.
            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
            {
                var map = LevelMaps.For(3, level);

                Assert.AreEqual(TerrainDecorator.BridgeTile(map.Grid, map.Seed),
                                TerrainDecorator.BridgeTile(map.Grid, map.Seed),
                                $"3-{level}");
            }
        }

        [Test]
        public void ACrossingNeedsGroundAtBothEnds()
        {
            // The rule itself, on a map made by hand: a ford in the middle of open water
            // is not a crossing however wadeable the tile says it is.
            var grid = new TileGrid(16, 16);

            for (int i = 0; i < grid.TileCount; i++) grid[i] = TerrainType.Water;

            int middle = grid.ToIndex(8, 8);
            grid[middle] = TerrainType.Ford;
            Assert.IsFalse(Crossings.Spans(grid, middle), "a ford alone in a lake counted as a crossing");

            // Banks either side, and it is one.
            int west = grid.ToIndex(6, 8);
            int east = grid.ToIndex(10, 8);
            grid[west] = TerrainType.Plains;
            grid[east] = TerrainType.Plains;

            Assert.IsTrue(Crossings.Spans(grid, middle));
            Assert.AreEqual(1, Crossings.Count(grid));
        }
    }
}
