using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class WildlifeTests
    {
        static LevelMap Level(int chapter, int level)
            => TerrainGenerator.Generate(new LevelRecipe(),
                                         DeterministicRandom.SeedFor(chapter, level));

        static float Distance(Vec2 a, Vec2 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        [Test]
        public void EveryLevelIsInhabited()
        {
            for (int level = 1; level <= 10; level++)
                Assert.AreEqual(Wildlife.Count, Wildlife.Populate(Level(1, level)).Count,
                    $"level 1-{level}");
        }

        [Test]
        public void NothingGrazesInsideABanditCamp()
        {
            // A fox living in an enemy group is a joke, and worse, a tell: an animal
            // where no animal would be would mark the group as surely as a flag.
            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);

                foreach (var animal in Wildlife.Populate(map))
                    foreach (var spawn in map.Encounters.Enemies)
                        Assert.Greater(Distance(animal.Home, Vec2.FromTile(map.Grid, spawn.Tile)),
                            4f * TileGrid.TileSize,
                            $"level 1-{level}: an animal homed on top of a group");
            }
        }

        [Test]
        public void AnimalsStandOnGroundTheyCouldStandOn()
        {
            var map = Level(2, 3);

            foreach (var animal in Wildlife.Populate(map))
            {
                int tile = map.Grid.ToIndex((int)(animal.Home.X / TileGrid.TileSize),
                                            (int)(animal.Home.Y / TileGrid.TileSize));
                map.Grid.ToCoords(tile, out int x, out int y);

                Assert.IsTrue(map.Grid.IsPassable(x, y));
                Assert.AreNotEqual(TerrainType.Ford, map.Grid[tile],
                    "a ford is the one tile the caravan must use; nothing else may block it");
            }
        }

        [Test]
        public void TheCaravanScattersThem()
        {
            var map = Level(1, 5);
            var animals = Wildlife.Populate(map);
            var target = animals[0];

            Wildlife.Step(animals, target.Home, null, 0.1f);
            Assert.IsTrue(target.IsFleeing);

            for (int i = 0; i < 60; i++) Wildlife.Step(animals, target.Home, null, 0.1f);
            Assert.Greater(Distance(target.Position, target.Home), 20f,
                "it bolted and got nowhere");
        }

        [Test]
        public void AFightScattersThemFurtherAway()
        {
            // The wider of the two radii, and the more useful signal: the caravan is
            // where the player already is, and a fight may not be.
            Assert.Greater(Wildlife.BattleRadius, Wildlife.SpookRadius);

            var map = Level(1, 5);
            var animals = Wildlife.Populate(map);
            var target = animals[1];

            var elsewhere = new Vec2(target.Home.X + 500f, target.Home.Y + 500f);
            var battle = new Vec2(target.Position.X + Wildlife.SpookRadius + 14f, target.Position.Y);

            Wildlife.Step(animals, elsewhere, new List<Vec2> { battle }, 0.1f);
            Assert.IsTrue(target.IsFleeing,
                "a fight beyond the caravan's own radius should still have startled it");
        }

        [Test]
        public void TheyComeBack()
        {
            // An animal frozen where its flight ended reads as a bug, and one that never
            // returns leaves the level emptier every minute of a run.
            var map = Level(1, 5);
            var animals = Wildlife.Populate(map);
            var target = animals[0];

            Wildlife.Step(animals, target.Home, null, 0.1f);
            var elsewhere = new Vec2(target.Home.X + 500f, target.Home.Y + 500f);
            for (int i = 0; i < 1500; i++) Wildlife.Step(animals, elsewhere, null, 0.1f);

            Assert.IsFalse(target.IsFleeing);
            Assert.LessOrEqual(Distance(target.Position, target.Home), Wildlife.GrazeRadius + 0.5f);
        }

        [Test]
        public void TheSameLevelIsInhabitedTheSameWay()
        {
            var first = Wildlife.Populate(Level(3, 7));
            var second = Wildlife.Populate(Level(3, 7));

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Kind, second[i].Kind);
                Assert.AreEqual(first[i].Home.X, second[i].Home.X);
            }
        }
    }
}
