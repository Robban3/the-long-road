using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    /// <summary>
    /// The shop: what gold buys, what it costs, and that the purchase reaches the run.
    ///
    /// That last one is the point of these. Every boon is deliberately a number the
    /// fighting already reads, so what has to be true is that the number arrives — a
    /// shop whose purchases stop at the shop is the same failure as an upgrade economy
    /// with no button, which this project has already shipped once.
    /// </summary>
    public class BoonTests
    {
        static LevelRun Run(Squad squad, Boons boons)
        {
            var recipe = new ChapterRecipe().ForLevel(1);
            var map = TerrainGenerator.Generate(recipe, DeterministicRandom.SeedFor(1, 1));

            return new LevelRun(map, map.Corridors[0].Tiles, squad, recipe.EnemyStrength, boons);
        }

        [Test]
        public void PricesRiseWithEachLevel()
        {
            int first = BoonTable.Price(Boon.Purse, 0);
            int second = BoonTable.Price(Boon.Purse, 1);
            int last = BoonTable.Price(Boon.Purse, BoonTable.MaxLevel(Boon.Purse) - 1);

            Assert.Greater(second, first, "the second level cost no more than the first");
            Assert.Greater(last, second);
            Assert.AreEqual(0, BoonTable.Price(Boon.Purse, BoonTable.MaxLevel(Boon.Purse)),
                "a finished boon still had a price on it");
        }

        [Test]
        public void BuyingSpendsTheGoldAndKeepsTheLevel()
        {
            var campaign = new Campaign();
            campaign.Earn(1000);

            int price = campaign.PriceOf(Boon.Purse);

            Assert.IsTrue(campaign.TryBuy(Boon.Purse, out int cost));
            Assert.AreEqual(price, cost);
            Assert.AreEqual(1000 - price, campaign.Gold);
            Assert.AreEqual(1, campaign.BoonLevel(Boon.Purse));
        }

        [Test]
        public void AnEmptyPurseBuysNothing()
        {
            var campaign = new Campaign();
            campaign.Earn(10);

            Assert.IsFalse(campaign.TryBuy(Boon.Muster, out int cost));
            Assert.AreEqual(0, cost, "a refused purchase reported a cost");
            Assert.AreEqual(10, campaign.Gold, "a refused purchase spent gold anyway");
            Assert.AreEqual(0, campaign.BoonLevel(Boon.Muster));
        }

        [Test]
        public void ABoonCannotBeTakenPastItsLastLevel()
        {
            var campaign = new Campaign();
            campaign.Earn(1000000);

            for (int i = 0; i < BoonTable.MaxLevel(Boon.Outriders); i++)
                Assert.IsTrue(campaign.TryBuy(Boon.Outriders, out _));

            Assert.IsFalse(campaign.TryBuy(Boon.Outriders, out _));
            Assert.AreEqual(BoonTable.MaxLevel(Boon.Outriders), campaign.BoonLevel(Boon.Outriders));
        }

        [Test]
        public void WhatWasBoughtSurvivesTheSave()
        {
            var campaign = new Campaign();
            campaign.Earn(2000);
            campaign.TryBuy(Boon.Purse, out _);
            campaign.TryBuy(Boon.Purse, out _);
            campaign.TryBuy(Boon.Smithy, out _);
            campaign.Record(1, 1, stars: 2, gold: 0);

            var loaded = Campaign.Load(campaign.Save());

            Assert.AreEqual(2, loaded.BoonLevel(Boon.Purse));
            Assert.AreEqual(1, loaded.BoonLevel(Boon.Smithy));
            Assert.AreEqual(0, loaded.BoonLevel(Boon.Muster));
            Assert.AreEqual(campaign.Gold, loaded.Gold);
            Assert.AreEqual(2, loaded.Stars(1, 1), "the stars were lost when boons were added");
        }

        [Test]
        public void AnOlderSaveStillLoads()
        {
            // Version 1 is this same save without the boons on the end. It reads as a
            // campaign that has bought nothing, which is exactly what it was.
            var loaded = Campaign.Load("1|340|0|1.1.3,1.2.2");

            Assert.AreEqual(340, loaded.Gold);
            Assert.AreEqual(3, loaded.Stars(1, 1));
            Assert.AreEqual(0, loaded.BoonLevel(Boon.Purse));
        }

        [Test]
        public void ThePurseArrivesInTheRun()
        {
            var boons = new Boons();
            boons.Set(Boon.Purse, 3);

            var run = Run(new Squad(12), boons);

            Assert.AreEqual(3 * BoonTable.SilverPerLevel, run.Economy.Silver,
                "the trading purse never reached the run");
        }

        [Test]
        public void StouterCartsArriveInTheRun()
        {
            var plain = Run(new Squad(12), new Boons());

            var boons = new Boons();
            boons.Set(Boon.Wainwright, 5);
            var bought = Run(new Squad(12), boons);

            for (int i = 0; i < plain.Caravan.Wagons.Count; i++)
                Assert.Greater(bought.Caravan.Wagons[i].MaxHp, plain.Caravan.Wagons[i].MaxHp,
                    $"the {plain.Caravan.Wagons[i].Kind} wagon was no stouter for it");
        }

        [Test]
        public void TheFieldSmithyMakesUpgradesCheaper()
        {
            var squad = new Squad(12);
            squad.TryPlace(FormationSlot.Van, TroopKind.Swordsmen);

            var plain = Run(squad, new Boons());

            var boons = new Boons();
            boons.Set(Boon.Smithy, 4);

            var cheaper = new Squad(12);
            cheaper.TryPlace(FormationSlot.Van, TroopKind.Swordsmen);
            var bought = Run(cheaper, boons);

            Assert.Less(bought.PriceOf(FormationSlot.Van, UpgradeTrack.Weapon),
                        plain.PriceOf(FormationSlot.Van, UpgradeTrack.Weapon),
                        "the smithy discounted nothing");
        }

        [Test]
        public void ADiscountNeverMakesUpgradesFree()
        {
            // A track the player can fill for nothing is not an economy, so the discount
            // is floored well above zero however much is bought.
            var boons = new Boons();
            boons.Set(Boon.Smithy, BoonTable.MaxLevel(Boon.Smithy));

            Assert.GreaterOrEqual(boons.UpgradeCost, 0.5f);
        }

        [Test]
        public void ARunWithNoBoonsIsTheGameAsBalanced()
        {
            // What keeps a shop purchase out of the balance figures: every test here, the
            // headless capture and a level opened from the editor pass nothing.
            var run = Run(new Squad(12), null);

            Assert.AreEqual(0, run.Economy.Silver);
            Assert.AreEqual(1f, run.Boons.WagonHealth, 0.0001f);
            Assert.AreEqual(1f, run.Boons.UpgradeCost, 0.0001f);
            Assert.AreEqual(0, run.Boons.ExtraSquadPoints);
            Assert.AreEqual(0, run.Boons.ExtraPosts);
        }
    }
}
