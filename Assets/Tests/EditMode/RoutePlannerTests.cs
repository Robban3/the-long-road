using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
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

        // --- What the preview tells the player (docs/GDD.md §3.3) -------------------

        [Test]
        public void ADetourAroundAnObstacleIsFlagged()
        {
            // Drawing across a river away from its fords does not stop anything — A*
            // goes around — and the caravan takes a detour nobody asked for. The point
            // of the flag is that it arrives before the run does, not during it.
            var grid = Plains(21, 21);
            for (int y = 0; y < 20; y++) grid[10, y] = TerrainType.Water;   // a wall with one gap

            var planner = new RoutePlanner(grid);
            var route = planner.Solve(0, 0, 20, 0);

            Assert.IsTrue(route.IsValid);
            Assert.AreEqual(1, route.Legs.Count);
            Assert.Greater(route.Legs[0].Detour, RouteResult.DetourThreshold,
                $"the way round the water read {route.Legs[0].Detour:0.00}");
            Assert.AreEqual(1, route.DetourLegs);
        }

        [Test]
        public void AnOrdinaryLegIsNotFlagged()
        {
            // The threshold has to clear the noise floor. A* on eight-connected ground
            // never walks the crow's line, so an ordinary leg already reads above 1.0 —
            // measured over chapter 1 to 3 in the port, 1.05 to 1.19. A threshold inside
            // that spread would warn on every route and mean nothing.
            var route = new RoutePlanner(Plains()).Solve(0, 0, 19, 19);

            Assert.IsTrue(route.IsValid);
            Assert.LessOrEqual(route.Legs[0].Detour, RouteResult.DetourThreshold);
            Assert.AreEqual(0, route.DetourLegs);
        }

        [Test]
        public void TheLegThatCrossesTheRiverNamesItsFord()
        {
            // §3.3: the crossing is where the decision is, so the preview has to be
            // able to point at the one this route uses.
            var grid = Plains(21, 21);
            for (int y = 0; y < 21; y++) grid[10, y] = TerrainType.Water;
            grid[10, 5] = TerrainType.Ford;

            var route = new RoutePlanner(grid).Solve(0, 5, 20, 5);

            Assert.IsTrue(route.IsValid);
            Assert.AreEqual(grid.ToIndex(10, 5), route.Legs[0].FordTile);
            CollectionAssert.AreEqual(new[] { grid.ToIndex(10, 5) }, route.Crossings);
        }

        [Test]
        public void TheRiskReadingComesOffTheTerrainAndNothingElse()
        {
            // The rule the whole information economy rests on: what is out there is
            // bought with the eagle or paid for in blood. A risk number that consulted
            // the encounter layout would hand it over for free.
            var forest = Plains();
            for (int i = 0; i < forest.TileCount; i++) forest[i] = TerrainType.Forest;

            var open = Plains();
            for (int i = 0; i < open.TileCount; i++) open[i] = TerrainType.Plains;

            float dense = new RoutePlanner(forest).Solve(0, 0, 19, 0).AmbushExposure;
            float bare = new RoutePlanner(open).Solve(0, 0, 19, 0).AmbushExposure;

            Assert.That(dense, Is.EqualTo(TerrainTable.AmbushWeight(TerrainType.Forest)).Within(0.001f));
            Assert.That(bare, Is.EqualTo(TerrainTable.AmbushWeight(TerrainType.Plains)).Within(0.001f));
            Assert.Greater(dense, bare, "forest should read as worse country to be ambushed in");
        }

        [Test]
        public void AFailedLegIsRecordedRatherThanDropped()
        {
            // The preview draws the failed leg red and blocks the start, so it needs
            // the leg itself and not only its number.
            var grid = Plains();
            for (int y = 0; y < 20; y++) grid[10, y] = TerrainType.Water;

            var route = new RoutePlanner(grid).Solve(0, 0, 19, 0);

            Assert.IsFalse(route.IsValid);
            Assert.AreEqual(0, route.FailedLeg);
            Assert.AreEqual(1, route.Legs.Count);
            Assert.IsTrue(route.Legs[0].Failed);
        }

        [Test]
        public void EveryLegIsRecordedInOrder()
        {
            var grid = Plains();
            var planner = new RoutePlanner(grid);
            planner.TryAddWaypoint(5, 10);
            planner.TryAddWaypoint(12, 3);

            var route = planner.Solve(0, 0, 19, 19);

            Assert.AreEqual(3, route.Legs.Count);
            Assert.AreEqual(grid.ToIndex(0, 0), route.Legs[0].FromTile);
            Assert.AreEqual(grid.ToIndex(5, 10), route.Legs[0].ToTile);
            Assert.AreEqual(grid.ToIndex(5, 10), route.Legs[1].FromTile);
            Assert.AreEqual(grid.ToIndex(12, 3), route.Legs[1].ToTile);
            Assert.AreEqual(grid.ToIndex(19, 19), route.Legs[2].ToTile);
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
