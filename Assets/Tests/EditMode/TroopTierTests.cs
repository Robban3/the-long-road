using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// The crossbow and the four horses: what each costs, what the price buys, that every
    /// horse charges like a horse, and that the newer troops are earned.
    /// </summary>
    public class TroopTierTests
    {
        static readonly TroopKind[] Horses =
        {
            TroopKind.Cavalry, TroopKind.HeavyCavalry, TroopKind.NobleCavalry, TroopKind.Knights
        };

        [Test]
        public void ADearerHorseHitsHarderAndTakesMore()
        {
            for (int i = 1; i < Horses.Length; i++)
            {
                var cheaper = Horses[i - 1];
                var dearer = Horses[i];

                Assert.Greater(TroopTable.Cost(dearer), TroopTable.Cost(cheaper), $"{dearer} costs no more than {cheaper}");
                Assert.Greater(TroopTable.Dps(dearer), TroopTable.Dps(cheaper), $"{dearer} hits no harder than {cheaper}");
                Assert.Greater(TroopTable.GroupHp(dearer), TroopTable.GroupHp(cheaper), $"{dearer} is no tougher than {cheaper}");
                Assert.GreaterOrEqual(TroopTable.DamageReduction(dearer), TroopTable.DamageReduction(cheaper));
            }
        }

        [Test]
        public void EveryHorseChargesOnOpenGroundAndBogsDownInAFen()
        {
            foreach (var horse in Horses)
            {
                Assert.IsTrue(TroopTable.IsMounted(horse));
                Assert.Greater(TroopTable.TerrainDamageMultiplier(horse, TerrainType.Plains), 1f, $"{horse} does not charge on the plain");
                Assert.Less(TroopTable.TerrainDamageMultiplier(horse, TerrainType.Marsh), 1f, $"{horse} charges through a fen");
                Assert.Less(TroopTable.TerrainDamageMultiplier(horse, TerrainType.Forest), 1f, $"{horse} charges among trees");
            }

            Assert.IsFalse(TroopTable.IsMounted(TroopKind.Swordsmen));
        }

        [Test]
        public void ACrossbowHitsHarderThanABowAndCostsMore()
        {
            Assert.Greater(TroopTable.Dps(TroopKind.Crossbowmen), TroopTable.Dps(TroopKind.Archers));
            Assert.Greater(TroopTable.Cost(TroopKind.Crossbowmen), TroopTable.Cost(TroopKind.Archers));

            // A shooter, bought reach like a bow, and as blind among trees.
            Assert.IsTrue(TroopTable.HasRangedSpecial(TroopKind.Crossbowmen));
            Assert.Less(TroopTable.TerrainRangeMultiplier(TroopKind.Crossbowmen, TerrainType.Forest), 1f);
            Assert.Less(TroopTable.TerrainDamageMultiplier(TroopKind.Crossbowmen, TerrainType.Forest), 1f);
        }

        [Test]
        public void TheNewerTroopsAreEarnedAlongTheRoad()
        {
            var campaign = new Campaign();

            Assert.IsTrue(campaign.TroopOpen(TroopKind.Archers), "a starting troop was locked");
            Assert.IsFalse(campaign.TroopOpen(TroopKind.Crossbowmen), "the crossbow was open from the start");
            Assert.IsFalse(campaign.TroopOpen(TroopKind.Knights));

            for (int level = 1; level <= TroopTable.LevelsToUnlock(TroopKind.Crossbowmen); level++)
                campaign.Record(1, level, stars: 1, gold: 0);

            Assert.IsTrue(campaign.TroopOpen(TroopKind.Crossbowmen), "the crossbow did not open with the levels");
            Assert.IsFalse(campaign.TroopOpen(TroopKind.HeavyCavalry), "heavy horse opened with the crossbow");

            // Each heavier horse later than the last.
            for (int i = 2; i < Horses.Length; i++)
                Assert.Greater(TroopTable.LevelsToUnlock(Horses[i]), TroopTable.LevelsToUnlock(Horses[i - 1]));

            // And all of it in a demo.
            campaign.OpenAll = true;
            Assert.IsTrue(campaign.TroopOpen(TroopKind.Knights));
        }

        [Test]
        public void EveryTableHasARowForEveryTroop()
        {
            // Thirteen troops across nine parallel tables: one short and a new troop reads
            // past the end of it on the first fight.
            foreach (var kind in TroopTable.All)
            {
                Assert.Greater(TroopTable.Cost(kind), 0, $"{kind} has no cost");
                Assert.Greater(TroopTable.Models(kind), 0, $"{kind} has no models");
                Assert.Greater(TroopTable.HpPerModel(kind), 0f, $"{kind} has no health");
                Assert.GreaterOrEqual(TroopTable.Sight(kind), 0f);
                Assert.GreaterOrEqual(TroopTable.TrapSight(kind), 0f);
                Assert.GreaterOrEqual(TroopTable.HealPerSecond(kind), 0f);
                Assert.GreaterOrEqual(TroopTable.LevelsToUnlock(kind), 0);
            }
        }
    }
}
