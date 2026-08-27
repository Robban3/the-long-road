using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class CaravanTests
    {
        static List<int> StraightRoute(TileGrid grid, int y, int length)
        {
            var route = new List<int>();
            for (int x = 0; x < length; x++) route.Add(grid.ToIndex(x, y));
            return route;
        }

        /// <summary>
        /// The sweep covers every piece of ground the column and its escort touch.
        ///
        /// The decorator kept the *corridor* clear of anything a wagon could not roll
        /// over, and the corridor is not where the caravan goes. It is not where the
        /// caravan starts — the run-up is forty metres behind the start line and belongs
        /// to no tile on the route. It is not where the flanks walk — six metres out on
        /// either side. And between two tile centres a diagonal crosses tiles the
        /// staircase does not name. Trees stood in all three, and the escort walked
        /// through them.
        ///
        /// So this walks the run and checks the ground, rather than checking the rule.
        /// </summary>
        [Test]
        public void TheSweepCoversWhereTheEscortActuallyWalks()
        {
            var grid = new TileGrid(48, 48);
            var caravan = new Caravan(grid, StraightRoute(grid, 24, 40));
            var squad = new Squad(18);

            squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
            squad.TryPlace(FormationSlot.LeftRear, TroopKind.Priest);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);

            var swept = caravan.Sweep(TerrainDecoratorDriveHalfWidth);

            // What the decorator used to be given: the route's own tiles, widened by
            // one. Kept here so this test fails for the reason it was written, rather
            // than passing because a sweep of everything is trivially a superset.
            var corridor = new HashSet<int>();
            foreach (int tile in StraightRoute(grid, 24, 40))
            {
                grid.ToCoords(tile, out int cx, out int cy);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (grid.InBounds(cx + dx, cy + dy))
                            corridor.Add(grid.ToIndex(cx + dx, cy + dy));
            }

            int missed = 0, outsideTheCorridor = 0;

            for (int step = 0; step < 4000 && !caravan.HasArrived; step++)
            {
                caravan.Tick(0.05f);
                squad.UpdatePositions(caravan);

                foreach (var group in squad.Slots)
                {
                    if (group == null) continue;

                    int tile = TileOf(grid, group.Position);
                    if (tile < 0) continue;

                    if (!swept.Contains(tile)) missed++;
                    if (!corridor.Contains(tile)) outsideTheCorridor++;
                }

                for (int i = 0; i < caravan.Wagons.Count; i++)
                {
                    int tile = TileOf(grid, caravan.WagonPosition(i));
                    if (tile >= 0 && !swept.Contains(tile)) missed++;
                }
            }

            Assert.AreEqual(0, missed,
                $"{missed} post-or-wagon positions stood on ground the sweep never claimed");

            Assert.Greater(outsideTheCorridor, 0,
                "the escort never left the corridor, so this test could not have caught "
                + "the thing it was written for");
        }

        /// <summary>The decorator's half-width, repeated here because Sim cannot see View.</summary>
        const float TerrainDecoratorDriveHalfWidth = 8f;

        static int TileOf(TileGrid grid, Vec2 at)
        {
            int x = (int)System.Math.Floor(at.X / TileGrid.TileSize);
            int y = (int)System.Math.Floor(at.Y / TileGrid.TileSize);
            return grid.InBounds(x, y) ? grid.ToIndex(x, y) : -1;
        }

        [Test]
        public void TheColumnIsAlreadyStrungOutWhenTheLevelBegins()
        {
            // Every wagon used to start on the start tile, stacked, because trailing
            // positions were clamped to the head of the route. The third one did not
            // appear until the first two had driven thirty metres out from under it —
            // a caravan assembling itself out of one point, in the first four seconds
            // of the game.
            var grid = new TileGrid(30, 10);
            var caravan = new Caravan(grid, StraightRoute(grid, 5, 30));

            var lead = caravan.WagonPosition(0);
            var second = caravan.WagonPosition(1);
            var third = caravan.WagonPosition(2);

            Assert.That(Vec2.Distance(lead, second), Is.EqualTo(Caravan.WagonSpacing).Within(0.5f),
                "the second wagon does not trail the first by a wagon's spacing");
            Assert.That(Vec2.Distance(second, third), Is.EqualTo(Caravan.WagonSpacing).Within(0.5f),
                "the third wagon does not trail the second by a wagon's spacing");

            // Behind the start line, not ahead of it: the run-up is ground to stand on
            // and not journey, so nobody has been moved along the route to make room.
            Assert.Less(third.X, lead.X, "the column is not behind its own lead");
            Assert.AreEqual(Vec2.FromTile(grid, grid.ToIndex(0, 5)), lead,
                "the lead wagon does not start on the start tile");
        }

        [Test]
        public void TheColumnCentreSitsBetweenTheLeadWagonAndTheLast()
        {
            // Two distance origins live in Caravan and they are a run-up apart: what the
            // game reports counts from the start line, what positions are measured along
            // counts from behind it. Mixing them aimed the camera fifty-five metres
            // behind the caravan, which looks like the camera lagging rather than like a
            // unit error — so the arithmetic lives in Caravan and this holds it.
            var grid = new TileGrid(40, 10);
            var caravan = new Caravan(grid, StraightRoute(grid, 5, 40));

            for (int i = 0; i < 200; i++) caravan.Tick(0.05f);

            var lead = caravan.WagonPosition(0);
            var last = caravan.WagonPosition(2);
            var centre = caravan.ColumnCentre;

            Assert.That(Vec2.Distance(centre, lead), Is.EqualTo(Vec2.Distance(centre, last))
                                                       .Within(0.5f),
                "the column's centre is not equally far from either end of the column");

            Assert.Less(Vec2.Distance(centre, lead), Vec2.Distance(lead, last),
                "the column's centre is not inside the column");
        }

        [Test]
        public void TheRunUpIsGroundToStandOnRatherThanJourney()
        {
            var grid = new TileGrid(30, 10);
            var route = StraightRoute(grid, 5, 30);
            var caravan = new Caravan(grid, route);

            // 29 steps of one tile: the road the caravan is asked to travel, with the
            // run-up excluded from every number the game reports.
            float road = 29f * TileGrid.TileSize;

            Assert.That(caravan.TotalDistance, Is.EqualTo(road).Within(0.01f),
                "the run-up was counted as part of the journey");
            Assert.That(caravan.DistanceTravelled, Is.EqualTo(0f).Within(0.01f));
            Assert.That(caravan.Progress, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void TheCaravanStartsAtTheRouteHeadAndReachesTheEnd()
        {
            var grid = new TileGrid(30, 10);
            var caravan = new Caravan(grid, StraightRoute(grid, 5, 30));

            Assert.AreEqual(Vec2.FromTile(grid, grid.ToIndex(0, 5)), caravan.LeadPosition);
            Assert.IsFalse(caravan.HasArrived);

            for (int i = 0; i < 2000 && !caravan.HasArrived; i++) caravan.Tick(0.05f);

            Assert.IsTrue(caravan.HasArrived, "the caravan never reached the goal");
            Assert.That(caravan.Progress, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void TerrainSetsThePace()
        {
            // The whole reason route drawing matters minute to minute: the player
            // watches the caravan bog down in the fen they chose.
            var road = new TileGrid(30, 10, TerrainType.Road);
            var marsh = new TileGrid(30, 10, TerrainType.Marsh);

            var onRoad = new Caravan(road, StraightRoute(road, 5, 30));
            var inMarsh = new Caravan(marsh, StraightRoute(marsh, 5, 30));

            for (int i = 0; i < 100; i++) { onRoad.Tick(0.05f); inMarsh.Tick(0.05f); }

            Assert.Greater(onRoad.DistanceTravelled, inMarsh.DistanceTravelled * 2.5f,
                $"road {onRoad.DistanceTravelled:F0} m against marsh {inMarsh.DistanceTravelled:F0} m — " +
                "not the difference the terrain table promises");
        }

        [Test]
        public void HaltingStopsTheColumn()
        {
            var grid = new TileGrid(30, 10);
            var caravan = new Caravan(grid, StraightRoute(grid, 5, 30));

            caravan.SpeedModifier = 0f;
            for (int i = 0; i < 40; i++) caravan.Tick(0.05f);

            Assert.AreEqual(0f, caravan.DistanceTravelled, "the Halt order did not stop the caravan");
        }

        [Test]
        public void WagonsTrailAlongThePathNotThroughTheCorner()
        {
            // A column that cuts a bend reads as a bug. Following the path keeps it
            // looking like a caravan on a road.
            var grid = new TileGrid(30, 30);
            var route = new List<int>();
            for (int x = 0; x < 15; x++) route.Add(grid.ToIndex(x, 5));
            for (int y = 6; y < 20; y++) route.Add(grid.ToIndex(14, y));

            var caravan = new Caravan(grid, route);
            for (int i = 0; i < 300; i++) caravan.Tick(0.05f);

            // Once round the corner, the rear wagon must still be on the first leg.
            var lead = caravan.WagonPosition(0);
            var rear = caravan.WagonPosition(2);

            float straightLine = Vec2.Distance(lead, rear);
            Assert.Less(straightLine, Caravan.WagonSpacing * 2f + 1f,
                "the rear wagon drifted off the path");
            Assert.AreNotEqual(lead, rear, "the wagons are stacked on top of each other");
        }

        [Test]
        public void WagonsStartHealthyAndTakeDamageDownToZero()
        {
            var grid = new TileGrid(10, 10);
            var caravan = new Caravan(grid, StraightRoute(grid, 5, 10));
            var treasure = caravan[WagonKind.Treasure];

            Assert.AreEqual(1f, treasure.HpFraction);
            Assert.AreEqual(1f, caravan.LootFraction);

            treasure.ApplyDamage(treasure.MaxHp * 0.5f);
            Assert.That(caravan.LootFraction, Is.EqualTo(0.5f).Within(0.001f),
                "loot does not follow the treasure wagon's condition");

            float overkill = treasure.ApplyDamage(99999f);
            Assert.AreEqual(0f, treasure.Hp, "a wagon went below zero");
            Assert.Less(overkill, 99999f, "overkill damage was reported as dealt");
            Assert.IsTrue(treasure.Destroyed);
        }

        [Test]
        public void LosingOneWagonDoesNotEndTheRun()
        {
            // Partial failure is the design's replay hook: you arrive, but worse.
            var grid = new TileGrid(10, 10);
            var caravan = new Caravan(grid, StraightRoute(grid, 5, 10));

            caravan[WagonKind.Treasure].ApplyDamage(99999f);
            Assert.IsFalse(caravan.Destroyed, "losing the treasure wagon ended the run");

            foreach (var wagon in caravan.Wagons) wagon.ApplyDamage(99999f);
            Assert.IsTrue(caravan.Destroyed);
        }

        [Test]
        public void ADestroyedCaravanStopsMoving()
        {
            var grid = new TileGrid(30, 10);
            var caravan = new Caravan(grid, StraightRoute(grid, 5, 30));
            foreach (var wagon in caravan.Wagons) wagon.ApplyDamage(99999f);

            for (int i = 0; i < 40; i++) caravan.Tick(0.05f);
            Assert.AreEqual(0f, caravan.DistanceTravelled);
        }

        [Test]
        public void ARealGeneratedLevelCanBeDrivenEndToEnd()
        {
            var chapter = new ChapterRecipe();
            var map = TerrainGenerator.Generate(chapter.ForLevel(3), DeterministicRandom.SeedFor(1, 3));
            var fast = map.CorridorOf(CorridorKind.Fast);

            var caravan = new Caravan(map.Grid, fast.Tiles);
            Assert.Greater(caravan.TotalDistance, 0f);

            int steps = 0;
            while (!caravan.HasArrived && steps < 20000) { caravan.Tick(0.05f); steps++; }

            Assert.IsTrue(caravan.HasArrived, "a generated level's fast route could not be driven");

            float seconds = steps * 0.05f;
            Assert.That(seconds, Is.InRange(20f, 240f),
                $"the run took {seconds:F0} s; the design targets 90 to 180");
        }
    }
}
