using System;
using System.Collections.Generic;
using TheVail.Gen;
using TheVail.Sim;
using NUnit.Framework;

namespace TheVail.Tests
{
    /// <summary>
    /// The soft signal, and whether it is a signal at all (docs/GDD.md §3.5).
    ///
    /// Every number here was measured rather than chosen, and the measurements are in
    /// the doc. What the tests defend is that the signal keeps saying something: a
    /// flock that is right everywhere, or one that can be counted back into an order of
    /// battle, is decoration in the first case and an exploit in the second.
    /// </summary>
    public class CrowSignalTests
    {
        static LevelMap Level(int chapter, int level)
            => TerrainGenerator.Generate(new LevelRecipe(),
                                         DeterministicRandom.SeedFor(chapter, level));

        static float Nearest(LevelMap map, int tile)
        {
            map.Grid.ToCoords(tile, out int x, out int y);
            float nearest = float.PositiveInfinity;

            foreach (var spawn in map.Encounters.Enemies)
            {
                map.Grid.ToCoords(spawn.Tile, out int gx, out int gy);
                float dx = gx - x, dy = gy - y;
                nearest = Math.Min(nearest, (float)Math.Sqrt(dx * dx + dy * dy));
            }

            return nearest;
        }

        [Test]
        public void ATruthfulFlockTellsTheTruth()
        {
            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);

                foreach (var flock in CrowSignal.Place(map))
                {
                    if (!flock.Truthful) continue;

                    Assert.LessOrEqual(Nearest(map, flock.Tile), CrowSignal.HintTiles + 0.001f,
                        $"level 1-{level}: a flock claimed a group within "
                        + $"{CrowSignal.HintTiles} tiles and there was none");
                }
            }
        }

        [Test]
        public void AFalseFlockIsGenuinelyFalse()
        {
            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);

                foreach (var flock in CrowSignal.Place(map))
                {
                    if (flock.Truthful) continue;

                    Assert.Greater(Nearest(map, flock.Tile), CrowSignal.HintTiles,
                        $"level 1-{level}: a flock meant to be lying had something under it");
                }
            }
        }

        [Test]
        public void NoFlockDoublesAsAMarker()
        {
            // The whole point of the signal is that it says "be careful here" and never
            // "there they are". A flock on the group is the second thing.
            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);

                foreach (var flock in CrowSignal.Place(map))
                    Assert.GreaterOrEqual(Nearest(map, flock.Tile), CrowSignal.MinTiles - 0.001f,
                        $"level 1-{level}: a flock sat on top of a group");
            }
        }

        [Test]
        public void AboutAFifthOfTheFlocksAreLying()
        {
            int total = 0, truthful = 0;

            for (int level = 1; level <= 10; level++)
                foreach (var flock in CrowSignal.Place(Level(1, level)))
                {
                    total++;
                    if (flock.Truthful) truthful++;
                }

            Assert.Greater(total, 0);
            float falseShare = 1f - truthful / (float)total;
            Assert.That(falseShare, Is.EqualTo(CrowSignal.FalseShare).Within(0.06f),
                $"{falseShare:P0} of flocks were false");
        }

        [Test]
        public void FlocksCannotBeCountedIntoGroups()
        {
            // The load-bearing one. If every group had a flock, counting flocks would
            // count groups and the level's order of battle would be free.
            float lowest = float.PositiveInfinity, highest = 0f;

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);
                if (map.Encounters.Enemies.Count == 0) continue;

                float ratio = CrowSignal.Place(map).Count / (float)map.Encounters.Enemies.Count;
                lowest = Math.Min(lowest, ratio);
                highest = Math.Max(highest, ratio);
            }

            Assert.Greater(highest - lowest, 0.3f,
                $"the ratio only ranged {lowest:0.00} to {highest:0.00}, which is close "
                + "enough to constant that a player could divide by it");
        }

        [Test]
        public void FlocksStandOnGroundACaravanCouldCross()
        {
            var map = Level(2, 6);

            foreach (var flock in CrowSignal.Place(map))
            {
                map.Grid.ToCoords(flock.Tile, out int x, out int y);
                Assert.IsTrue(map.Grid.IsPassable(x, y));
                Assert.AreNotEqual(TerrainType.Ford, map.Grid[flock.Tile]);
            }
        }

        [Test]
        public void TheSameLevelGetsTheSameFlocks()
        {
            var first = CrowSignal.Place(Level(3, 4));
            var second = CrowSignal.Place(Level(3, 4));

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Tile, second[i].Tile);
                Assert.AreEqual(first[i].Truthful, second[i].Truthful);
            }
        }
    }
}
