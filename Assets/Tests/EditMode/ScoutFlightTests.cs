using TheVail.Gen;
using TheVail.Sim;
using NUnit.Framework;

namespace TheVail.Tests
{
    public class ScoutFlightTests
    {
        static LevelMap Level(int chapter, int level)
            => TerrainGenerator.Generate(new LevelRecipe(), DeterministicRandom.SeedFor(chapter, level));

        [Test]
        public void TheEagleUncoversAboutAQuarterOfTheMap()
        {
            // Too little and the ability is not worth its gold; too much and the overlay
            // stops mattering, which takes the terrain reading in §3.4 and §3.5 with it.
            // Measured over chapter 1 in the Python port: 23 to 25 percent.
            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);
                var flight = ScoutingAbility.Fly(map);

                float share = flight.Coverage / (float)map.Grid.TileCount;
                Assert.That(share, Is.InRange(0.12f, 0.40f),
                    $"level 1-{level}: the eagle uncovered {share:P0} of the map");
            }
        }

        [Test]
        public void ItFindsSomeGroupsAndNeverAllOfThem()
        {
            int levelsChecked = 0;

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);
                if (map.Encounters.Enemies.Count < 4) continue;

                var flight = ScoutingAbility.Fly(map);

                Assert.Less(flight.RevealedEnemies.Count, map.Encounters.Enemies.Count,
                    $"level 1-{level}: one flight found the whole level, which leaves nothing to fear");
                levelsChecked++;
            }

            Assert.Greater(levelsChecked, 5);
        }

        [Test]
        public void EveryGroupItFoundStandsOnGroundItFlewOver()
        {
            var map = Level(1, 5);
            var flight = ScoutingAbility.Fly(map);

            foreach (int index in flight.RevealedEnemies)
                Assert.IsTrue(flight.RevealedTiles.Contains(map.Encounters.Enemies[index].Tile),
                    "a group was marked on ground the eagle never flew over");
        }

        [Test]
        public void TheSameLevelFliesTheSameFlight()
        {
            // The reason this matters: a flight rolled fresh on every press would let a
            // player restart the level until the bird swept the ground they cared about.
            var first = ScoutingAbility.Fly(Level(3, 4));
            var second = ScoutingAbility.Fly(Level(3, 4));

            Assert.AreEqual(first.Path.Count, second.Path.Count);
            Assert.AreEqual(first.Coverage, second.Coverage);
            CollectionAssert.AreEqual(first.RevealedEnemies, second.RevealedEnemies);
        }

        [Test]
        public void ASecondEagleLooksSomewhereElse()
        {
            var map = Level(1, 5);
            var first = ScoutingAbility.Fly(map);
            var second = ScoutingAbility.Fly(map, flight: 1);

            int shared = 0;
            foreach (int tile in second.RevealedTiles)
                if (first.RevealedTiles.Contains(tile)) shared++;

            Assert.Less(shared, first.Coverage * 0.8f,
                "the second flight covered nearly the same ground as the first");
        }

        [Test]
        public void TheFlightStaysOnTheMap()
        {
            var map = Level(2, 2);
            float extent = map.Grid.Width * TileGrid.TileSize;

            foreach (var point in ScoutingAbility.Fly(map).Path)
            {
                Assert.That(point.X, Is.InRange(-TileGrid.TileSize, extent + TileGrid.TileSize));
                Assert.That(point.Y, Is.InRange(-TileGrid.TileSize, extent + TileGrid.TileSize));
            }
        }
    }
}
