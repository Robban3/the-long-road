using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class RunEconomyTests
    {
        [Test]
        public void KillsPaySilver()
        {
            var economy = new RunEconomy();
            int awarded = economy.AwardGroupKill(EnemyKind.Bandit);

            Assert.AreEqual(EnemyTable.GroupSilver(EnemyKind.Bandit), awarded);
            Assert.AreEqual(awarded, economy.Silver);
            Assert.AreEqual(awarded, economy.TotalEarned);
        }

        [Test]
        public void FlawlessVictoriesPayMore()
        {
            var plain = new RunEconomy();
            var clean = new RunEconomy();

            int ordinary = plain.AwardGroupKill(EnemyKind.Bandit, flawless: false);
            int flawless = clean.AwardGroupKill(EnemyKind.Bandit, flawless: true);

            Assert.Greater(flawless, ordinary, "a clean victory paid no better than a costly one");
            Assert.AreEqual(1, clean.FlawlessVictories);
            Assert.AreEqual(0, plain.FlawlessVictories);
        }

        [Test]
        public void ScoutingAndDisarmingPayToo()
        {
            // Without these the scout and the engineer are unaffordable in an economy
            // driven by kills, and the information game dies with them.
            var economy = new RunEconomy();

            Assert.Greater(economy.AwardScouting(), 0);
            Assert.Greater(economy.AwardTrapDisarm(TrapKind.Pit), 0);
        }

        [Test]
        public void TheMarauderPaysOffWhenBoughtEarlyAndNotWhenBoughtLate()
        {
            // This is the whole point of the mechanic: it must be a wager on how much
            // of the level remains, not a strictly correct purchase.
            const int groupsInLevel = 12;

            int Simulate(int buyAfterGroups)
            {
                var economy = new RunEconomy();
                for (int i = 0; i < groupsInLevel; i++)
                {
                    if (i == buyAfterGroups) economy.TryBuyMarauder();
                    economy.AwardGroupKill(EnemyKind.Bandit);
                }
                return economy.Silver;
            }

            int never = Simulate(-1);
            int early = Simulate(2);
            int late = Simulate(groupsInLevel - 2);

            Assert.Greater(early, never, "buying the marauder early never paid for itself");
            Assert.Less(late, never, "buying the marauder on the last stretch should be a loss");
        }

        [Test]
        public void TheMarauderCannotBeBoughtTwiceOrWithoutFunds()
        {
            var economy = new RunEconomy();
            Assert.IsFalse(economy.TryBuyMarauder(), "bought the marauder with an empty purse");

            economy.AwardGroupKill(EnemyKind.Bandit);
            economy.AwardGroupKill(EnemyKind.Bandit);
            Assert.IsTrue(economy.TryBuyMarauder());
            Assert.IsFalse(economy.TryBuyMarauder(), "bought the marauder a second time");
            Assert.IsTrue(economy.HasMarauder);
        }

        [Test]
        public void IncomeIsNotRetroactive()
        {
            var economy = new RunEconomy();
            economy.AwardGroupKill(EnemyKind.Bandit);
            economy.AwardGroupKill(EnemyKind.Bandit);
            int before = economy.Silver;

            economy.TryBuyMarauder();
            Assert.AreEqual(before - RunEconomy.MarauderCost, economy.Silver,
                "buying the marauder retroactively topped up earlier kills");
        }

        [Test]
        public void ChapterMultiplierScalesIncome()
        {
            var plain = new RunEconomy(1f);
            var rich = new RunEconomy(1.5f);

            Assert.Greater(rich.AwardGroupKill(EnemyKind.Wolf), plain.AwardGroupKill(EnemyKind.Wolf));
        }

        [Test]
        public void UpgradeCostsRiseSteeply()
        {
            int previous = 0;
            for (int level = 0; level < RunEconomy.MaxTrackLevel; level++)
            {
                int cost = RunEconomy.UpgradeCost(level);
                Assert.Greater(cost, previous, $"level {level + 1} costs no more than the one before");
                previous = cost;
            }

            Assert.AreEqual(int.MaxValue, RunEconomy.UpgradeCost(RunEconomy.MaxTrackLevel),
                "a sixth level should be unreachable");
        }

        [Test]
        public void UpgradingSpendsSilver()
        {
            var economy = new RunEconomy();
            for (int i = 0; i < 4; i++) economy.AwardGroupKill(EnemyKind.Bandit);

            int before = economy.Silver;
            Assert.IsTrue(economy.TryUpgrade(0, out int cost));
            Assert.AreEqual(before - cost, economy.Silver);
            Assert.AreEqual(cost, economy.TotalSpent);
        }

        [Test]
        public void UpgradingFailsWhenBrokeOrMaxed()
        {
            var economy = new RunEconomy();
            Assert.IsFalse(economy.TryUpgrade(0, out _), "upgraded on an empty purse");

            for (int i = 0; i < 40; i++) economy.AwardGroupKill(EnemyKind.Bandit);
            Assert.IsFalse(economy.TryUpgrade(RunEconomy.MaxTrackLevel, out _), "upgraded past the cap");
        }

        [Test]
        public void ATypicalLevelAffordsAboutEightUpgradeLevels()
        {
            // docs/GDD.md §6.4 budgets roughly eight levels spread over six troops:
            // enough to specialise, never enough to max everything.
            var economy = new RunEconomy();
            for (int i = 0; i < 10; i++) economy.AwardGroupKill(EnemyKind.Bandit);
            for (int i = 0; i < 4; i++) economy.AwardTrapDisarm(TrapKind.Pit);

            int bought = 0;
            var trackLevels = new int[6];
            bool progress = true;

            while (progress)
            {
                progress = false;
                for (int track = 0; track < trackLevels.Length; track++)
                {
                    if (!economy.TryUpgrade(trackLevels[track], out _)) continue;
                    trackLevels[track]++;
                    bought++;
                    progress = true;
                }
            }

            Assert.That(bought, Is.InRange(5, 12),
                $"a level's income bought {bought} upgrade levels; the design targets about eight");
        }

        [Test]
        public void LeftoverSilverConvertsPoorlyButNotToNothing()
        {
            var economy = new RunEconomy();
            economy.AwardGroupKill(EnemyKind.Bandit);
            int silver = economy.Silver;

            int gold = economy.ConvertLeftoverToGold();

            Assert.AreEqual(0, economy.Silver, "silver survived the end of the level");
            Assert.Greater(gold, 0, "hoarded silver evaporated entirely");
            Assert.Less(gold, silver, "hoarding paid as well as spending");
        }
    }
}
