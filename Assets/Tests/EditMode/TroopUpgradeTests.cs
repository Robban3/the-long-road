using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class TroopUpgradeTests
    {
        const float ArcherBaseRange = 22f;
        const float ArcherBaseSight = 18f;
        const float ScoutBaseSight = 34f;

        [Test]
        public void RangeGrowsButWithDiminishingReturns()
        {
            float previousGain = float.MaxValue;

            for (int level = 1; level <= TroopUpgrades.MaxLevel; level++)
            {
                float gain = TroopUpgrades.RangeMultiplier(level) - TroopUpgrades.RangeMultiplier(level - 1);
                Assert.Greater(gain, 0f, $"level {level} added no range");
                Assert.Less(gain, previousGain, $"level {level} gave as much as the level before");
                previousGain = gain;
            }
        }

        [Test]
        public void MaximumRangeNeverOutrunsWhatCanBeSeen()
        {
            // The guard that keeps the fog of war meaningful. If a fully upgraded
            // archer outranged every sight radius in the game, the player could clear
            // ambushes before they ever woke and the whole hidden-threat design would
            // quietly stop mattering.
            float maxRange = TroopUpgrades.EffectiveRange(ArcherBaseRange, TroopUpgrades.MaxLevel);

            Assert.Less(maxRange, ScoutBaseSight,
                $"a maxed archer reaches {maxRange:F1} m, beyond the scout's {ScoutBaseSight} m sight");
            Assert.Greater(maxRange, ArcherBaseRange * 1.3f, "the track is not worth its price");
        }

        [Test]
        public void RangeIsWorthBuyingWithoutAScout()
        {
            // The rule this replaces clamped reach to the squad's sight, which meant an
            // archer with no scout got nothing at all for five levels of range. What the
            // scout buys is revelation — CombatSystem only ever fires on a Revealed
            // enemy — and that is a different lever from how far the arrow goes.
            float bought = TroopUpgrades.EffectiveRange(ArcherBaseRange, 3);

            Assert.Greater(bought, ArcherBaseRange, "the range track bought no range");
            Assert.Greater(bought, ArcherBaseSight,
                "range that stops at the archer's own sight is range nobody can spend on");
        }

        [Test]
        public void RangeCostsMoreThanEveryOtherTrack()
        {
            for (int level = 0; level < TroopUpgrades.MaxLevel; level++)
            {
                int ordinary = RunEconomy.UpgradeCost(level);
                int ranged = RunEconomy.UpgradeCost(level, TroopUpgrades.RangeCostMultiplier);

                Assert.Greater(ranged, ordinary,
                    $"level {level + 1} of the range track is no dearer than a normal upgrade");
            }
        }

        [Test]
        public void AGoodRunAffordsTwoOrThreeLevelsOfRangeNotFive()
        {
            // Priced so it stays an investment. Cheap enough and it would be the only
            // correct purchase for an archer; free at the top and the fog stops
            // mattering by the third minute.
            var economy = new RunEconomy();
            for (int i = 0; i < 10; i++) economy.AwardGroupKill(EnemyKind.Bandit);

            int level = 0;
            while (economy.TryUpgrade(level, TroopUpgrades.RangeCostMultiplier, out _)) level++;

            Assert.That(level, Is.InRange(1, 3),
                $"a typical run bought {level} levels of range; two to three is the target");
        }

        [Test]
        public void TakingTheDangerousRouteBuysOneMoreLevel()
        {
            // The reward loop closing on itself: more enemies means more silver means
            // reach the cautious player cannot afford.
            int LevelsAffordable(int groups, bool marauder)
            {
                var economy = new RunEconomy();
                for (int i = 0; i < groups; i++)
                {
                    if (i == 2 && marauder) economy.TryBuyMarauder();
                    economy.AwardGroupKill(EnemyKind.Bandit);
                }

                int level = 0;
                while (economy.TryUpgrade(level, TroopUpgrades.RangeCostMultiplier, out _)) level++;
                return level;
            }

            int cautious = LevelsAffordable(6, false);
            int bold = LevelsAffordable(12, true);

            Assert.Greater(bold, cautious,
                $"the bold route bought {bold} levels against the cautious route's {cautious}");
        }

        [Test]
        public void FlatTracksStayFlat()
        {
            // Weapons and armour rise linearly; only range diminishes, because only
            // range threatens to break the detection design.
            for (int level = 1; level <= TroopUpgrades.MaxLevel; level++)
            {
                float gain = TroopUpgrades.WeaponMultiplier(level) - TroopUpgrades.WeaponMultiplier(level - 1);
                Assert.That(gain, Is.EqualTo(TroopUpgrades.WeaponPerLevel).Within(0.0001f));
            }

            Assert.That(TroopUpgrades.ArmourDamageReduction(TroopUpgrades.MaxLevel),
                Is.EqualTo(TroopUpgrades.ArmourReductionPerLevel * TroopUpgrades.MaxLevel).Within(0.0001f));
        }

        [Test]
        public void LevelsAreClampedAtBothEnds()
        {
            Assert.AreEqual(1f, TroopUpgrades.RangeMultiplier(0));
            Assert.AreEqual(1f, TroopUpgrades.RangeMultiplier(-3));
            Assert.AreEqual(TroopUpgrades.RangeMultiplier(TroopUpgrades.MaxLevel),
                            TroopUpgrades.RangeMultiplier(99));
        }

        [Test]
        public void OnlyRangedSpecialsCarryThePremium()
        {
            Assert.AreEqual(1f, TroopUpgrades.CostMultiplier(UpgradeTrack.Weapon, isRangedSpecial: true));
            Assert.AreEqual(1f, TroopUpgrades.CostMultiplier(UpgradeTrack.Special, isRangedSpecial: false));
            Assert.AreEqual(TroopUpgrades.RangeCostMultiplier,
                TroopUpgrades.CostMultiplier(UpgradeTrack.Special, isRangedSpecial: true));
        }
    }
}
