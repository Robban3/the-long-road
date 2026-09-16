using System.Collections.Generic;
using TheVeil.Gen;
using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
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
            // A range rather than a number, because the count stopped being one when the
            // deer were grouped: a sighting places one animal or a herd of two to four,
            // so how many animals a level ends with depends on how much of it is the kind
            // of ground deer stand on. Sixteen sightings is the thing that is fixed.
            //
            // The floor is what the rule is actually about — a level with a handful of
            // animals on it is a level where hunting is not a decision — and the ceiling
            // is the draw call budget, which is what Wildlife.Count is for.
            for (int level = 1; level <= 10; level++)
            {
                int placed = Wildlife.Populate(Level(1, level)).Count;

                Assert.GreaterOrEqual(placed, Wildlife.Sightings,
                    $"level 1-{level} placed {placed} animals across {Wildlife.Sightings} sightings, "
                    + "which means most of them found nowhere to stand");

                Assert.LessOrEqual(placed, Wildlife.Count, $"level 1-{level}");
            }
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
        public void TheyNeverRunOntoWater()
        {
            // <b>Nothing in the step had ever looked at the ground under it.</b> It
            // advanced a position by a heading and a speed, so a startled deer bolted
            // along whatever bearing it was given — over the lake, across the ford the
            // caravan is queueing for, off the cliff. Reported from a playtest, which is
            // the wrong place to find it: it is true of every animal on every level and
            // always was.
            //
            // Driven rather than staged. A single animal aimed at a single pond proves the
            // case it was written for and nothing else; a caravan walking a diagonal
            // across six levels startles everything on them, from every side, and asks
            // the question of each animal on every tick.
            for (int level = 1; level <= 6; level++)
            {
                var map = Level(1, level);
                var animals = Wildlife.Populate(map);

                for (int tick = 0; tick < 600; tick++)
                {
                    var chase = new Vec2(tick * 1.7f, tick * 1.3f);
                    Wildlife.Step(map.Grid, animals, chase, null, 0.1f);

                    foreach (var animal in animals)
                        Assert.IsTrue(OnGround(map, animal.Position),
                            $"1-{level} tick {tick}: a {animal.Kind} is standing on "
                            + $"{Under(map, animal.Position)} at "
                            + $"{animal.Position.X:0}, {animal.Position.Y:0}");
                }
            }
        }

        /// <summary>Whether a world position is on ground an animal could stand on.</summary>
        static bool OnGround(LevelMap map, Vec2 at)
        {
            if (at.X < 0f || at.Y < 0f) return false;

            int x = (int)(at.X / TileGrid.TileSize);
            int y = (int)(at.Y / TileGrid.TileSize);

            return map.Grid.InBounds(x, y) && map.Grid.IsPassable(x, y)
                && map.Grid[map.Grid.ToIndex(x, y)] != TerrainType.Ford;
        }

        /// <summary>What it is standing on, for the failure message.</summary>
        static string Under(LevelMap map, Vec2 at)
        {
            int x = (int)(at.X / TileGrid.TileSize);
            int y = (int)(at.Y / TileGrid.TileSize);

            if (at.X < 0f || at.Y < 0f || !map.Grid.InBounds(x, y)) return "ground off the map";

            return map.Grid[map.Grid.ToIndex(x, y)].ToString();
        }

        [Test]
        public void TheCaravanScattersThem()
        {
            var map = Level(1, 5);
            var animals = Wildlife.Populate(map);
            var target = animals[0];

            Wildlife.Step(map.Grid, animals, target.Home, null, 0.1f);
            Assert.IsTrue(target.IsFleeing);

            for (int i = 0; i < 60; i++) Wildlife.Step(map.Grid, animals, target.Home, null, 0.1f);
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

            Wildlife.Step(map.Grid, animals, elsewhere, new List<Vec2> { battle }, 0.1f);
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

            Wildlife.Step(map.Grid, animals, target.Home, null, 0.1f);
            var elsewhere = new Vec2(target.Home.X + 500f, target.Home.Y + 500f);
            for (int i = 0; i < 1500; i++) Wildlife.Step(map.Grid, animals, elsewhere, null, 0.1f);

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

        [Test]
        public void MostOfThemStandWhereTheyCanBeSeen()
        {
            // Counting animals measured the wrong thing. Placed evenly, half of the
            // ones a run came near stood under a canopy that hides a deer completely
            // from a camera thirty-five degrees above it, and an animal nobody can see
            // is not sparse, it is absent.
            int forest = 0, total = 0;

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);

                foreach (var animal in Wildlife.Populate(map))
                {
                    int tile = map.Grid.ToIndex((int)(animal.Home.X / TileGrid.TileSize),
                                                (int)(animal.Home.Y / TileGrid.TileSize));
                    total++;
                    if (map.Grid[tile] == TerrainType.Forest) forest++;
                }
            }

            Assert.Greater(total, 0);
            float share = forest / (float)total;
            Assert.Less(share, 0.45f, $"{share:P0} of the wildlife is under the canopy");
            Assert.Greater(share, 0.02f, "none of them are in the woods, which is where foxes live");
        }
    }
}
