using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class RoutePlannerTests
    {
        static TileGrid Plains(int w = 20, int h = 20) => new TileGrid(w, h);

        [Test]
        public void RouteWithNoWaypointsIsTheDirectPath()
        {
            var planner = new RoutePlanner(Plains());
            var route = planner.Solve(0, 0, 19, 0);

            Assert.IsTrue(route.IsValid);
            Assert.AreEqual(20, route.Tiles.Count);
            Assert.That(route.TravelCost, Is.EqualTo(19f).Within(0.001f));
        }

        [Test]
        public void WaypointsBendTheRoute()
        {
            var grid = Plains();
            var planner = new RoutePlanner(grid);
            planner.TryAddWaypoint(10, 15);

            var route = planner.Solve(0, 0, 19, 0);

            Assert.IsTrue(route.IsValid);
            CollectionAssert.Contains(route.Tiles, grid.ToIndex(10, 15), "route ignored the waypoint");
            Assert.Greater(route.TravelCost, 19f, "detour should cost more than the direct line");
        }

        [Test]
        public void SeamsAreNotCountedTwice()
        {
            // Each leg's first tile is the previous leg's last. Counting it twice
            // would inflate the distance and terrain readouts the player plans with.
            var grid = Plains();
            var planner = new RoutePlanner(grid);
            planner.TryAddWaypoint(5, 0);
            planner.TryAddWaypoint(10, 0);
            planner.TryAddWaypoint(15, 0);

            var route = planner.Solve(0, 0, 19, 0);

            Assert.IsTrue(route.IsValid);
            Assert.AreEqual(20, route.Tiles.Count, "seam tiles were duplicated");
            CollectionAssert.AllItemsAreUnique(route.Tiles);
        }

        [Test]
        public void RouteIsContiguous()
        {
            var grid = Plains(30, 30);
            grid.FillRect(12, 0, 12, 22, TerrainType.Water);

            var planner = new RoutePlanner(grid);
            planner.TryAddWaypoint(6, 25);
            planner.TryAddWaypoint(20, 25);

            var route = planner.Solve(0, 0, 29, 5);
            Assert.IsTrue(route.IsValid);

            for (int i = 1; i < route.Tiles.Count; i++)
            {
                grid.ToCoords(route.Tiles[i - 1], out int px, out int py);
                grid.ToCoords(route.Tiles[i], out int cx, out int cy);
                Assert.LessOrEqual(System.Math.Abs(cx - px), 1);
                Assert.LessOrEqual(System.Math.Abs(cy - py), 1);
            }
        }

        [Test]
        public void TerrainBreakdownSumsToRouteLength()
        {
            var grid = Plains(30, 10);
            grid.FillRect(0, 0, 29, 0, TerrainType.Road);
            grid.FillRect(0, 5, 29, 5, TerrainType.Marsh);

            var planner = new RoutePlanner(grid);
            planner.TryAddWaypoint(15, 5);
            var route = planner.Solve(0, 0, 29, 0);

            int sum = 0;
            foreach (int c in route.TilesByTerrain) sum += c;
            Assert.AreEqual(route.Tiles.Count, sum);

            Assert.Greater(route.ShareOf(TerrainType.Marsh), 0f, "route through the marsh reports no marsh");
            Assert.That(route.ShareOf(TerrainType.Marsh), Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void MarshRouteTakesLongerThanRoadRoute()
        {
            // The core planning trade-off: the readout must show the player that a
            // marsh detour costs real time (docs/GDD.md §3.3).
            var grid = Plains(30, 10);
            grid.FillRect(0, 0, 29, 0, TerrainType.Road);
            grid.FillRect(0, 5, 29, 5, TerrainType.Marsh);
            grid.FillRect(0, 1, 29, 4, TerrainType.Water);
            grid.FillRect(0, 6, 29, 9, TerrainType.Water);
            grid[0, 1] = TerrainType.Plains;
            grid.FillRect(0, 1, 0, 5, TerrainType.Plains);
            grid.FillRect(29, 1, 29, 5, TerrainType.Plains);

            var viaRoad = new RoutePlanner(grid);
            viaRoad.TryAddWaypoint(15, 0);
            float roadTime = viaRoad.Solve(0, 0, 29, 0).EstimatedSeconds();

            var viaMarsh = new RoutePlanner(grid);
            viaMarsh.TryAddWaypoint(15, 5);
            float marshTime = viaMarsh.Solve(0, 0, 29, 0).EstimatedSeconds();

            Assert.Greater(marshTime, roadTime * 1.5f,
                $"marsh route ({marshTime:F1}s) should cost far more than the road ({roadTime:F1}s)");
        }

        [Test]
        public void ImpassableWaypointsAreRejected()
        {
            var grid = Plains();
            grid[5, 5] = TerrainType.Water;

            var planner = new RoutePlanner(grid);
            Assert.IsFalse(planner.TryAddWaypoint(5, 5), "a tap on deep water became a waypoint");
            Assert.AreEqual(0, planner.WaypointCount);
        }

        [Test]
        public void DuplicateWaypointsAreRejected()
        {
            var planner = new RoutePlanner(Plains());
            Assert.IsTrue(planner.TryAddWaypoint(4, 4));
            Assert.IsFalse(planner.TryAddWaypoint(4, 4));
            Assert.AreEqual(1, planner.WaypointCount);
        }

        [Test]
        public void WaypointLimitIsEnforced()
        {
            var planner = new RoutePlanner(Plains(), 6);
            for (int i = 0; i < 6; i++) Assert.IsTrue(planner.TryAddWaypoint(i + 1, 3), $"waypoint {i} refused");

            Assert.IsTrue(planner.IsFull);
            Assert.IsFalse(planner.TryAddWaypoint(10, 10), "accepted a seventh waypoint");
        }

        [Test]
        public void UnreachableLegIsReportedNotThrown()
        {
            var grid = Plains();
            grid.FillRect(10, 0, 10, 19, TerrainType.Cliff);

            var planner = new RoutePlanner(grid);
            planner.TryAddWaypoint(15, 10);

            var route = planner.Solve(0, 0, 5, 5);

            Assert.IsFalse(route.IsValid);
            Assert.AreEqual(0, route.FailedLeg, "wrong leg blamed for the blockage");
        }

        [Test]
        public void RemoveAndClearWork()
        {
            var planner = new RoutePlanner(Plains());
            planner.TryAddWaypoint(2, 2);
            planner.TryAddWaypoint(3, 3);

            Assert.IsTrue(planner.RemoveLast());
            Assert.AreEqual(1, planner.WaypointCount);

            planner.Clear();
            Assert.AreEqual(0, planner.WaypointCount);
            Assert.IsFalse(planner.RemoveLast());
        }

        [Test]
        public void MovingAWaypointChangesTheRoute()
        {
            var grid = Plains();
            var planner = new RoutePlanner(grid);
            planner.TryAddWaypoint(10, 2);

            float near = planner.Solve(0, 0, 19, 0).TravelCost;
            Assert.IsTrue(planner.MoveWaypoint(0, 10, 18));
            float far = planner.Solve(0, 0, 19, 0).TravelCost;

            Assert.Greater(far, near, "dragging the waypoint further out did not lengthen the route");
        }

        [Test]
        public void ReusedResultObjectDoesNotAccumulate()
        {
            var planner = new RoutePlanner(Plains());
            planner.TryAddWaypoint(10, 10);

            var reused = new RouteResult();
            planner.Solve(0, 0, 19, 19, reused);
            int firstCount = reused.Tiles.Count;
            float firstCost = reused.TravelCost;

            planner.Solve(0, 0, 19, 19, reused);

            Assert.AreEqual(firstCount, reused.Tiles.Count, "route grew when the buffer was reused");
            Assert.That(reused.TravelCost, Is.EqualTo(firstCost).Within(0.001f));
        }
    }
}
