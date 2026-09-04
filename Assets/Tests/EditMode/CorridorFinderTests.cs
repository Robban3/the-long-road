using TheVail.Sim;
using NUnit.Framework;

namespace TheVail.Tests
{
    public class CorridorFinderTests
    {
        static Corridor Route(CorridorKind kind, params int[] tiles)
        {
            var c = new Corridor { Kind = kind };
            c.Tiles.AddRange(tiles);
            return c;
        }

        [Test]
        public void OverlapOfIdenticalRoutesIsOne()
        {
            var a = Route(CorridorKind.Fast, 1, 2, 3, 4);
            var b = Route(CorridorKind.Safe, 1, 2, 3, 4);
            Assert.That(CorridorFinder.Overlap(a, b), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void OverlapOfDisjointRoutesIsZero()
        {
            var a = Route(CorridorKind.Fast, 1, 2, 3);
            var b = Route(CorridorKind.Safe, 4, 5, 6);
            Assert.That(CorridorFinder.Overlap(a, b), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void OverlapOfHalfSharedRoutesIsBetween()
        {
            var a = Route(CorridorKind.Fast, 1, 2, 3, 4);
            var b = Route(CorridorKind.Safe, 3, 4, 5, 6);
            float overlap = CorridorFinder.Overlap(a, b);
            Assert.Greater(overlap, 0.2f);
            Assert.Less(overlap, 0.5f);
        }

        [Test]
        public void SafeRouteTradesTimeForLowerAmbushExposure()
        {
            // A road is both the quickest way through and the one bandits patrol
            // (docs/GDD.md §3.2). The cautious corridor should give up the road.
            var grid = new TileGrid(30, 11);
            grid.FillRect(0, 5, 29, 5, TerrainType.Road);

            var corridors = CorridorFinder.Find(grid, 0, 5, 29, 5);
            Assert.GreaterOrEqual(corridors.Count, 2);

            var fast = corridors[0];
            var safe = corridors[1];

            Assert.AreEqual(CorridorKind.Fast, fast.Kind);
            Assert.AreEqual(CorridorKind.Safe, safe.Kind);
            Assert.Less(safe.AmbushExposure, fast.AmbushExposure,
                "the cautious route is no safer than the fast one");
            Assert.Greater(safe.TravelCost, fast.TravelCost,
                "safety came for free, which means the road was not actually faster");
        }

        [Test]
        public void UniformTerrainOffersNoRealChoice()
        {
            // Featureless ground gives three near-identical routes. Such a seed must
            // be rejected: the route drawing would be decoration, not a decision.
            var grid = new TileGrid(30, 30);
            var corridors = CorridorFinder.Find(grid, 0, 15, 29, 15);

            Assert.IsFalse(CorridorFinder.IsMeaningfulChoice(corridors),
                "featureless terrain was accepted as offering a meaningful choice");
        }

        [Test]
        public void ShortDangerousCrossingVersusLongSafeOneIsARealChoice()
        {
            // The shape the whole design is built on: a near crossing reached through
            // heavy forest, and a far one across open ground. Two fords alone are not
            // enough — if both approaches were equally dangerous the longer one would
            // simply be worse, and nobody would ever weigh them against each other.
            var grid = new TileGrid(40, 40);
            grid.FillRect(20, 0, 20, 39, TerrainType.Water);
            grid.FillRect(20, 14, 20, 15, TerrainType.Ford);   // near, through the forest
            grid.FillRect(20, 38, 20, 39, TerrainType.Ford);   // far, across the plain

            // Forest blankets the northern half on both banks; the south is open.
            grid.FillRect(0, 0, 39, 20, TerrainType.Forest);
            grid.FillRect(0, 21, 39, 39, TerrainType.Plains);
            grid.FillRect(20, 0, 20, 13, TerrainType.Water);
            grid.FillRect(20, 16, 20, 37, TerrainType.Water);
            grid.FillRect(20, 14, 20, 15, TerrainType.Ford);
            grid.FillRect(20, 38, 20, 39, TerrainType.Ford);

            var corridors = CorridorFinder.Find(grid, 0, 12, 39, 12);

            Assert.AreEqual(3, corridors.Count, "not all three corridors were found");

            var fast = corridors[0];
            var safest = corridors[0];
            foreach (var c in corridors)
                if (c.AmbushExposure < safest.AmbushExposure) safest = c;

            Assert.Less(safest.AmbushExposure, fast.AmbushExposure,
                "no corridor is meaningfully safer than the fastest");
            Assert.IsTrue(CorridorFinder.IsMeaningfulChoice(corridors),
                "a short dangerous crossing against a long safe one should be a real choice");
        }

        [Test]
        public void NoCorridorsWhenGoalIsWalledOff()
        {
            var grid = new TileGrid(20, 20);
            grid.FillRect(10, 0, 10, 19, TerrainType.Cliff);

            var corridors = CorridorFinder.Find(grid, 0, 10, 19, 10);

            Assert.IsEmpty(corridors);
            Assert.IsFalse(CorridorFinder.IsMeaningfulChoice(corridors));
        }

        [Test]
        public void FewerThanThreeCorridorsIsNeverMeaningful()
        {
            var only = new[] { Route(CorridorKind.Fast, 1, 2, 3) };
            Assert.IsFalse(CorridorFinder.IsMeaningfulChoice(only));
            Assert.IsFalse(CorridorFinder.IsMeaningfulChoice(null));
        }

        [Test]
        public void ReportedCostIsRealTerrainCostNotSearchCost()
        {
            // The safe corridor is found using surcharges, but the player experiences
            // terrain. A reported cost inflated by the search bias would make the
            // route preview lie.
            var grid = new TileGrid(30, 11);
            grid.FillRect(0, 5, 29, 5, TerrainType.Road);

            var corridors = CorridorFinder.Find(grid, 0, 5, 29, 5);
            var safe = corridors[1];

            var pathfinder = new GridPathfinder(grid);
            var path = new System.Collections.Generic.List<int>();

            float honest = 0f;
            for (int i = 1; i < safe.Tiles.Count; i++)
            {
                grid.ToCoords(safe.Tiles[i - 1], out int px, out int py);
                grid.ToCoords(safe.Tiles[i], out int cx, out int cy);
                float step = TerrainTable.TravelCost(grid[safe.Tiles[i]]);
                if (px != cx && py != cy) step *= 1.41421356f;
                honest += step;
            }

            Assert.That(safe.TravelCost, Is.EqualTo(honest).Within(0.01f));

            // And it must never be cheaper than the true optimum.
            pathfinder.TryFindPath(0, 5, 29, 5, path, out float optimum);
            Assert.GreaterOrEqual(safe.TravelCost, optimum - 0.01f);
        }
    }
}
