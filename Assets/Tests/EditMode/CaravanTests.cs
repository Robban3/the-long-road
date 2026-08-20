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
