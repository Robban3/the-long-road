using System.Collections.Generic;
using TheVail.Sim;
using NUnit.Framework;

namespace TheVail.Tests
{
    public class GridPathfinderTests
    {
        static (GridPathfinder, List<int>) Setup(TileGrid grid)
            => (new GridPathfinder(grid), new List<int>());

        [Test]
        public void StraightLineOnUniformPlainsCostsOnePerTile()
        {
            var grid = new TileGrid(11, 3);
            var (pf, path) = Setup(grid);

            Assert.IsTrue(pf.TryFindPath(0, 1, 10, 1, path, out float time));
            Assert.That(time, Is.EqualTo(10f).Within(0.001f));
            Assert.AreEqual(11, path.Count);
        }

        [Test]
        public void PrefersRoadDetourOverDirectPlains()
        {
            // A road running parallel to the direct line is faster despite the two
            // diagonal steps needed to reach it.
            var grid = new TileGrid(11, 3);
            grid.FillRect(0, 0, 10, 0, TerrainType.Road);
            var (pf, path) = Setup(grid);

            Assert.IsTrue(pf.TryFindPath(0, 1, 10, 1, path, out float time));
            Assert.Less(time, 10f, "route ignored the faster road");

            bool usedRoad = false;
            foreach (int t in path) { grid.ToCoords(t, out _, out int y); if (y == 0) usedRoad = true; }
            Assert.IsTrue(usedRoad, "route never touched the road");
        }

        [Test]
        public void AvoidsMarshWhenDetourIsCheaper()
        {
            var grid = new TileGrid(5, 3);
            grid[2, 1] = TerrainType.Marsh;
            var (pf, path) = Setup(grid);

            Assert.IsTrue(pf.TryFindPath(0, 1, 4, 1, path, out _));
            CollectionAssert.DoesNotContain(path, grid.ToIndex(2, 1), "route waded through the marsh");
        }

        [Test]
        public void CrossesMarshWhenThereIsNoWayRound()
        {
            // The marsh is a full wall — the caravan must slog through it, slowly.
            var grid = new TileGrid(5, 3);
            grid.FillRect(2, 0, 2, 2, TerrainType.Marsh);
            var (pf, path) = Setup(grid);

            Assert.IsTrue(pf.TryFindPath(0, 1, 4, 1, path, out float time));
            Assert.Greater(time, 4f, "crossing a marsh wall should cost real time");
        }

        [Test]
        public void RoutesThroughTheGapInAWall()
        {
            var grid = new TileGrid(9, 9);
            grid.FillRect(4, 0, 4, 9, TerrainType.Water);
            grid[4, 7] = TerrainType.Ford;
            var (pf, path) = Setup(grid);

            Assert.IsTrue(pf.TryFindPath(0, 1, 8, 1, path, out _));
            CollectionAssert.Contains(path, grid.ToIndex(4, 7), "route did not use the only ford");
        }

        [Test]
        public void ReturnsFalseWhenFullyWalledOff()
        {
            var grid = new TileGrid(9, 9);
            grid.FillRect(4, 0, 4, 9, TerrainType.Water);
            var (pf, path) = Setup(grid);

            Assert.IsFalse(pf.TryFindPath(0, 1, 8, 1, path, out _));
            Assert.IsEmpty(path);
        }

        [Test]
        public void ReturnsFalseForImpassableEndpoints()
        {
            var grid = new TileGrid(5, 5);
            grid[4, 4] = TerrainType.Cliff;
            var (pf, path) = Setup(grid);

            Assert.IsFalse(pf.TryFindPath(0, 0, 4, 4, path, out _));
            Assert.IsFalse(pf.TryFindPath(4, 4, 0, 0, path, out _));
        }

        [Test]
        public void StartEqualToGoalReturnsSingleTile()
        {
            var grid = new TileGrid(5, 5);
            var (pf, path) = Setup(grid);

            Assert.IsTrue(pf.TryFindPath(2, 2, 2, 2, path, out float time));
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(0f, time);
        }

        [Test]
        public void DiagonalDoesNotSqueezeBetweenBlockedCorners()
        {
            // Both orthogonal neighbours blocked: the diagonal gap is not real.
            var grid = new TileGrid(3, 3);
            grid[1, 0] = TerrainType.Water;
            grid[0, 1] = TerrainType.Water;
            var (pf, path) = Setup(grid);

            Assert.IsFalse(pf.TryFindPath(0, 0, 1, 1, path, out _),
                "route slipped diagonally through a corner that has no opening");
        }

        [Test]
        public void PathIsContiguous()
        {
            var grid = new TileGrid(20, 20);
            grid.FillRect(5, 0, 5, 15, TerrainType.Cliff);
            grid.FillRect(12, 5, 12, 19, TerrainType.Water);
            var (pf, path) = Setup(grid);

            Assert.IsTrue(pf.TryFindPath(0, 0, 19, 19, path, out _));

            for (int i = 1; i < path.Count; i++)
            {
                grid.ToCoords(path[i - 1], out int px, out int py);
                grid.ToCoords(path[i], out int cx, out int cy);
                Assert.LessOrEqual(System.Math.Abs(cx - px), 1);
                Assert.LessOrEqual(System.Math.Abs(cy - py), 1);
                Assert.IsTrue(TerrainTable.IsPassable(grid[path[i]]), "route entered impassable terrain");
            }
        }

        [Test]
        public void RepeatedQueriesOnOneInstanceAreIdentical()
        {
            // The pathfinder reuses its buffers across calls and invalidates them
            // with a generation counter. If that bookkeeping leaks, the second
            // solve differs from the first.
            var grid = new TileGrid(30, 30);
            grid.FillRect(10, 0, 10, 24, TerrainType.Water);
            grid.FillRect(20, 6, 20, 29, TerrainType.Cliff);

            var pf = new GridPathfinder(grid);
            var first = new List<int>();
            var second = new List<int>();

            Assert.IsTrue(pf.TryFindPath(0, 0, 29, 29, first, out float t1));
            pf.TryFindPath(3, 3, 25, 8, new List<int>(), out _);   // unrelated query in between
            Assert.IsTrue(pf.TryFindPath(0, 0, 29, 29, second, out float t2));

            CollectionAssert.AreEqual(first, second);
            Assert.AreEqual(t1, t2);
        }
    }
}
