using System.Collections.Generic;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class DetectionSystemTests
    {
        static TileGrid Grid(TerrainType fill = TerrainType.Plains) => new TileGrid(20, 20, fill);

        static List<EnemySpawn> OneEnemy(TileGrid grid, int x, int y, EnemyKind kind = EnemyKind.Wolf)
            => new List<EnemySpawn> { new EnemySpawn { Tile = grid.ToIndex(x, y), Kind = kind } };

        static List<Watcher> NoWatchers() => new List<Watcher>();

        [Test]
        public void EnemiesStartHiddenAndAsleep()
        {
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));

            Assert.AreEqual(0, detection.RevealedCount);
            Assert.AreEqual(0, detection.AwakeCount);
        }

        [Test]
        public void TheCaravanWakesEnemiesInsideTheirDetectRadius()
        {
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));
            var enemyPosition = Vec2.FromTile(grid, grid.ToIndex(10, 10));

            // Wolves notice at 20 m. Sit at 30 and nothing stirs.
            detection.Scan(new Vec2(enemyPosition.X + 30f, enemyPosition.Y), NoWatchers());
            Assert.AreEqual(0, detection.AwakeCount, "the pack woke from 30 m away");

            detection.Scan(new Vec2(enemyPosition.X + 15f, enemyPosition.Y), NoWatchers());
            Assert.AreEqual(1, detection.AwakeCount, "the pack slept through the caravan at 15 m");
        }

        [Test]
        public void WakingAlsoReveals()
        {
            // A group charging the caravan is not hidden, whatever the sight radius.
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));

            detection.Scan(Vec2.FromTile(grid, grid.ToIndex(10, 10)), NoWatchers());

            Assert.AreEqual(1, detection.AwakeCount);
            Assert.AreEqual(1, detection.RevealedCount);
            Assert.AreEqual(0, detection.SpottedEarlyCount, "an ambush that landed counted as good scouting");
        }

        [Test]
        public void AScoutSeesTheAmbushBeforeItStirs()
        {
            // The entire reason to spend one of six slots on a troop that barely
            // fights: sight radius beating detect radius buys time to reposition.
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));
            var enemy = Vec2.FromTile(grid, grid.ToIndex(10, 10));

            var caravan = new Vec2(enemy.X + 60f, enemy.Y);
            var scout = new List<Watcher> { new Watcher(new Vec2(enemy.X + 25f, enemy.Y), 34f) };

            detection.Scan(caravan, scout);

            Assert.AreEqual(1, detection.RevealedCount, "the scout missed a pack well inside her sight");
            Assert.AreEqual(0, detection.AwakeCount, "the pack woke although the caravan was 60 m off");
            Assert.AreEqual(1, detection.SpottedEarlyCount, "spotting it early earned nothing");
        }

        [Test]
        public void ShortSightedTroopsWalkIntoTheSameAmbush()
        {
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));
            var enemy = Vec2.FromTile(grid, grid.ToIndex(10, 10));

            // Swordsmen see 12 m. On plains that is 15.6 — not enough at 25 m out.
            var swordsmen = new List<Watcher> { new Watcher(new Vec2(enemy.X + 25f, enemy.Y), 12f) };
            detection.Scan(new Vec2(enemy.X + 60f, enemy.Y), swordsmen);

            Assert.AreEqual(0, detection.RevealedCount, "melee troops spotted an ambush 25 m away");
        }

        [Test]
        public void ForestBlindsTheWatcher()
        {
            // Forest halves sight, which is why a wooded route is dangerous beyond its
            // ambush chance — you cannot see what is coming.
            var open = Grid(TerrainType.Plains);
            var wooded = Grid(TerrainType.Forest);

            var openDetection = new DetectionSystem(open, OneEnemy(open, 10, 10));
            var woodedDetection = new DetectionSystem(wooded, OneEnemy(wooded, 10, 10));

            var enemy = Vec2.FromTile(open, open.ToIndex(10, 10));
            var watcher = new List<Watcher> { new Watcher(new Vec2(enemy.X + 20f, enemy.Y), 18f) };
            var farCaravan = new Vec2(enemy.X + 90f, enemy.Y);

            openDetection.Scan(farCaravan, watcher);
            woodedDetection.Scan(farCaravan, watcher);

            Assert.AreEqual(1, openDetection.RevealedCount, "an archer on open ground missed a group at 20 m");
            Assert.AreEqual(0, woodedDetection.RevealedCount, "the same archer saw just as far inside dense forest");
        }

        [Test]
        public void RevealIsStickyOnceSeen()
        {
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));
            var enemy = Vec2.FromTile(grid, grid.ToIndex(10, 10));

            var scout = new List<Watcher> { new Watcher(new Vec2(enemy.X + 25f, enemy.Y), 34f) };
            detection.Scan(new Vec2(enemy.X + 60f, enemy.Y), scout);
            Assert.AreEqual(1, detection.RevealedCount);

            // Scout walks away; the group must not blink out of existence.
            detection.Scan(new Vec2(enemy.X + 60f, enemy.Y), NoWatchers());
            Assert.AreEqual(1, detection.RevealedCount, "a spotted group vanished when the scout looked away");
        }

        [Test]
        public void TheScoutingBountyIsPaidOnce()
        {
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));
            var enemy = Vec2.FromTile(grid, grid.ToIndex(10, 10));
            var scout = new List<Watcher> { new Watcher(new Vec2(enemy.X + 25f, enemy.Y), 34f) };

            for (int i = 0; i < 5; i++) detection.Scan(new Vec2(enemy.X + 60f, enemy.Y), scout);

            Assert.AreEqual(1, detection.SpottedEarlyCount, "the bounty was paid on every pass");
        }

        [Test]
        public void NewlyRevealedAndWokenAreReportedOnlyOnTheTickTheyHappen()
        {
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));
            var enemy = Vec2.FromTile(grid, grid.ToIndex(10, 10));

            detection.Scan(enemy, NoWatchers());
            Assert.AreEqual(1, detection.RevealedThisTick.Count);
            Assert.AreEqual(1, detection.WokeThisTick.Count);

            detection.Scan(enemy, NoWatchers());
            Assert.IsEmpty(detection.RevealedThisTick, "the same group was reported as new twice");
            Assert.IsEmpty(detection.WokeThisTick);
        }

        [Test]
        public void ScanningIsThrottledToFourTimesASecond()
        {
            var grid = Grid();
            var detection = new DetectionSystem(grid, OneEnemy(grid, 10, 10));
            var enemy = Vec2.FromTile(grid, grid.ToIndex(10, 10));

            // Sixteen frames at 60 fps is 0.267 s: exactly one pass, not sixteen.
            int passes = 0;
            for (int frame = 0; frame < 16; frame++)
                if (detection.Tick(1f / 60f, enemy, NoWatchers())) passes++;

            Assert.AreEqual(1, passes, $"detection ran {passes} times in a quarter of a second");
        }

        [Test]
        public void ManyEnemiesAreHandledWithoutScanningThemAll()
        {
            // The spatial hash exists so this stays cheap once traps and combat share it.
            var grid = new TileGrid(64, 64);
            var spawns = new List<EnemySpawn>();
            for (int x = 2; x < 62; x += 3)
                for (int y = 2; y < 62; y += 3)
                    spawns.Add(new EnemySpawn { Tile = grid.ToIndex(x, y), Kind = EnemyKind.Bandit });

            var detection = new DetectionSystem(grid, spawns);
            var corner = Vec2.FromTile(grid, grid.ToIndex(2, 2));

            detection.Scan(corner, new List<Watcher> { new Watcher(corner, 20f) });

            Assert.Greater(detection.RevealedCount, 0);
            Assert.Less(detection.RevealedCount, spawns.Count / 2,
                "a scan in one corner revealed half the map");
        }
    }
}
