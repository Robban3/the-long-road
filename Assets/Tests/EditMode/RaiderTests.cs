using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// The two raiders added behind the peasants: a band on horseback, and the man the
    /// band follows.
    /// </summary>
    public class RaiderTests
    {
        [Test]
        public void HorsemenAreFewerFasterAndHarderThanMenOnFoot()
        {
            Assert.Less(EnemyTable.GroupSize(EnemyKind.BanditRider), EnemyTable.GroupSize(EnemyKind.Bandit));
            // Half again as quick as a man on foot, and the quickest thing on the road
            // bar a wolf pack. Not twice: see the note on _speed in EnemyTable, where
            // four metres a second made a chapter-two level impassable.
            Assert.Greater(EnemyTable.Speed(EnemyKind.BanditRider), 1.5f * EnemyTable.Speed(EnemyKind.Bandit),
                           "a horseman is barely quicker than a man walking");
            Assert.Greater(EnemyTable.Dps(EnemyKind.BanditRider), EnemyTable.Dps(EnemyKind.Bandit));

            // Tougher man for man, not band for band: there are three of them against
            // four, so the band as a whole is no harder to kill — it is harder to catch.
            Assert.Greater(EnemyTable.HpPerModel(EnemyKind.BanditRider), EnemyTable.HpPerModel(EnemyKind.Bandit));

            // Seen coming from further off, which is the only warning the escort gets.
            Assert.Greater(EnemyTable.DetectRadius(EnemyKind.BanditRider), EnemyTable.DetectRadius(EnemyKind.Bandit));
            Assert.IsTrue(EnemyTable.IsMounted(EnemyKind.BanditRider));
            Assert.IsFalse(EnemyTable.IsMounted(EnemyKind.Bandit));
        }

        [Test]
        public void TheLeaderIsOneManWorthAWholeBand()
        {
            Assert.AreEqual(1, EnemyTable.GroupSize(EnemyKind.BanditLeader), "a leader arrived with a band of leaders");

            // One man who takes as long to put down as three of his raiders.
            Assert.Greater(EnemyTable.HpPerModel(EnemyKind.BanditLeader), 2.5f * EnemyTable.HpPerModel(EnemyKind.Bandit));
            Assert.Greater(EnemyTable.Dps(EnemyKind.BanditLeader), EnemyTable.Dps(EnemyKind.BanditRider));

            // Worth killing: he carries what the band has taken.
            Assert.Greater(EnemyTable.GroupSilver(EnemyKind.BanditLeader), EnemyTable.GroupSilver(EnemyKind.Bandit));
            Assert.Greater(EnemyTable.Points(EnemyKind.BanditLeader), EnemyTable.Points(EnemyKind.Bandit));
        }

        [Test]
        public void EveryRaiderComesForTheTreasureAndTheWolvesDoNot()
        {
            Assert.IsTrue(EnemyTable.AfterTreasure(EnemyKind.Bandit));
            Assert.IsTrue(EnemyTable.AfterTreasure(EnemyKind.BanditRider));
            Assert.IsTrue(EnemyTable.AfterTreasure(EnemyKind.BanditLeader));
            Assert.IsFalse(EnemyTable.AfterTreasure(EnemyKind.Wolf), "a wolf came for the silver");
        }

        [Test]
        public void ASpearIsWorthTwiceAsMuchAgainstAnythingThatCharges()
        {
            Assert.AreEqual(2f, TroopTable.DamageMultiplierAgainst(TroopKind.Spearmen, EnemyKind.Wolf));
            Assert.AreEqual(2f, TroopTable.DamageMultiplierAgainst(TroopKind.Spearmen, EnemyKind.BanditRider));
            Assert.AreEqual(1f, TroopTable.DamageMultiplierAgainst(TroopKind.Spearmen, EnemyKind.Bandit));
            Assert.AreEqual(1f, TroopTable.DamageMultiplierAgainst(TroopKind.Swordsmen, EnemyKind.BanditRider));
        }

        [Test]
        public void EveryTableHasARowForEveryEnemy()
        {
            // Five kinds across eight parallel tables: one short and a new enemy reads
            // past the end of it the first time a level fields one.
            foreach (var kind in EnemyTable.All)
            {
                Assert.Greater(EnemyTable.GroupSize(kind), 0, $"{kind} has no group");
                Assert.Greater(EnemyTable.HpPerModel(kind), 0f, $"{kind} has no health");
                Assert.Greater(EnemyTable.Dps(kind), 0f, $"{kind} does no damage");
                Assert.Greater(EnemyTable.Speed(kind), 0f, $"{kind} cannot move");
                Assert.Greater(EnemyTable.AttackRange(kind), 0f, $"{kind} cannot reach anything");
                Assert.Greater(EnemyTable.DetectRadius(kind), 0f, $"{kind} never wakes");
                Assert.Greater(EnemyTable.Points(kind), 0, $"{kind} is free against the budget");
                Assert.Greater(EnemyTable.SilverPerKill(kind), 0, $"{kind} is worth nothing");
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(EnemyKind)).Length, EnemyTable.All.Length,
                            "an enemy kind is not in EnemyTable.All, so no level will ever field it");
        }

        [Test]
        public void NeitherRidesIntoTheOpeningChapter()
        {
            var first = ChapterRecipe.For(1);

            for (int level = 1; level <= first.LevelsPerChapter; level++)
            {
                var pool = first.PoolForLevel(level);
                Assert.IsFalse(System.Array.IndexOf(pool, EnemyKind.BanditRider) >= 0, $"horsemen on 1-{level}");
                Assert.IsFalse(System.Array.IndexOf(pool, EnemyKind.BanditLeader) >= 0, $"a captain on 1-{level}");
            }
        }

        [Test]
        public void TheyArePacedThroughTheChaptersAfterTheFirst()
        {
            var second = ChapterRecipe.For(2);

            Assert.IsFalse(System.Array.IndexOf(second.PoolForLevel(1), EnemyKind.BanditRider) >= 0,
                           "horsemen on the first level of a chapter");
            Assert.Contains(EnemyKind.BanditRider, second.PoolForLevel(4));

            Assert.IsFalse(System.Array.IndexOf(second.PoolForLevel(6), EnemyKind.BanditLeader) >= 0,
                           "the captain arrives before the escort is worn");
            Assert.Contains(EnemyKind.BanditLeader, second.PoolForLevel(7));

            // And the pool never shrinks as a chapter goes on.
            int previous = 0;
            for (int level = 1; level <= second.LevelsPerChapter; level++)
            {
                int size = second.PoolForLevel(level).Length;
                Assert.GreaterOrEqual(size, previous, $"level {level} lost an enemy kind");
                previous = size;
            }
        }
    }
}
