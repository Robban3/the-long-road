using TheVail.Gen;
using TheVail.Sim;
using NUnit.Framework;

namespace TheVail.Tests
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

            Assert.AreEqual((int)BoonTable.Effect(Boon.Purse, 3), run.Economy.Silver,
                "the trading purse never reached the run");
            Assert.Greater(run.Economy.Silver, 0, "three steps of purse bought no silver at all");
        }

        [Test]
        public void StouterCartsArriveInTheRun()
        {
            var plain = Run(new Squad(12), new Boons());

            var boons = new Boons();
            boons.Set(Boon.Hardened, 5);
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
        public void ADeepTrackHasThirtyStepsAndTinyOnes()
        {
            // Five steps was never a balance decision — it was the only depth that felt
            // safe with a flat curve. With the effect capped instead, the steps can be
            // many and small.
            Assert.AreEqual(BoonTable.DeepSteps, BoonTable.MaxLevel(Boon.Hardened));

            float first = BoonTable.Effect(Boon.Hardened, 1);
            float last = BoonTable.Effect(Boon.Hardened, 30)
                       - BoonTable.Effect(Boon.Hardened, 29);

            Assert.Greater(first, last, "the steps did not shrink");
            Assert.Less(first, BoonTable.Cap(Boon.Hardened) * 0.1f, "the first step was not small");
        }

        [Test]
        public void TheEffectApproachesTheCapAndNeverPassesIt()
        {
            // The whole reason thirty steps are safe. A save from a future build, a bug,
            // or somebody editing the file cannot put the game past this.
            foreach (var boon in BoonTable.All)
            {
                float cap = BoonTable.Cap(boon);

                Assert.LessOrEqual(BoonTable.Effect(boon, BoonTable.MaxLevel(boon)), cap + 0.001f);
                Assert.LessOrEqual(BoonTable.Effect(boon, 1000), cap + 0.001f,
                    $"{boon} passed its cap when asked for a thousand steps");
            }

            Assert.Greater(BoonTable.Effect(Boon.Hardened, 30),
                           BoonTable.Cap(Boon.Hardened) * 0.85f,
                           "thirty steps did not get most of the way there");
        }

        [Test]
        public void TheNewGearReachesTheRun()
        {
            var boons = new Boons();
            boons.Set(Boon.Trade, 30);
            boons.Set(Boon.Watch, 30);
            boons.Set(Boon.Tracking, 30);
            boons.Set(Boon.Exchange, 20);

            var plain = Run(new Squad(12), new Boons());
            var bought = Run(new Squad(12), boons);

            Assert.Greater(bought.LookoutSight, plain.LookoutSight, "the lookout saw no further");
            Assert.Greater(bought.TrapSight, plain.TrapSight, "trap sense reached no further");
            Assert.Greater(boons.SilverIncome, 1f, "silver income was unchanged");
            Assert.Less(boons.SilverPerGold, RunEconomy.SilverPerGold, "the exchange rate was unchanged");
        }

        [Test]
        public void TheExchangeNeverBeatsSpendingInTheField()
        {
            // Spending silver on the road has to stay better than hoarding it, or the
            // whole mid-run economy becomes a savings account.
            var boons = new Boons();
            boons.Set(Boon.Exchange, 1000);

            Assert.GreaterOrEqual(boons.SilverPerGold, 2);
        }

        [Test]
        public void TheWheelwrightMendsOnlyWhenNothingIsFighting()
        {
            var boons = new Boons();
            boons.Set(Boon.Repair, 30);

            var run = Run(new Squad(12), boons);
            var wagon = run.Caravan.Wagons[0];

            wagon.ApplyDamage(wagon.MaxHp * 0.5f);
            float hurt = wagon.Hp;

            for (int i = 0; i < 40 && !run.Combat.InContact; i++) run.Step();

            Assert.Greater(wagon.Hp, hurt, "a damaged cart never mended on a quiet road");
            Assert.LessOrEqual(wagon.Hp, wagon.MaxHp, "repair went past the cart's own health");
        }

        [Test]
        public void TheLashingsSpareTheTreasureAndNothingElse()
        {
            var boons = new Boons();
            boons.Set(Boon.Lashings, 30);

            var run = Run(new Squad(12), boons);

            var treasure = run.Caravan[WagonKind.Treasure];
            var supply = run.Caravan[WagonKind.Supply];

            float toTreasure = treasure.ApplyDamage(100f);
            float toSupply = supply.ApplyDamage(100f);

            Assert.Less(toTreasure, toSupply, "the lashings spared nothing");
            Assert.AreEqual(100f, toSupply, 0.001f, "the lashings leaked onto another cart");
        }

        [Test]
        public void PermanentTroopLevelsReachTheFighting()
        {
            var school = new TroopBoons();
            school.Set(TroopKind.Swordsmen, UpgradeTrack.Weapon, 30);
            school.Set(TroopKind.Swordsmen, UpgradeTrack.Armour, 30);

            var plain = new Squad(12);
            plain.TryPlace(FormationSlot.Van, TroopKind.Swordsmen);

            var raised = new Squad(12) { School = school };
            raised.TryPlace(FormationSlot.Van, TroopKind.Swordsmen);

            var a = plain[FormationSlot.Van];
            var b = raised[FormationSlot.Van];

            Assert.Greater(b.DamageAgainst(EnemyKind.Bandit, TerrainType.Plains),
                           a.DamageAgainst(EnemyKind.Bandit, TerrainType.Plains),
                           "the weapon school bought no damage");

            Assert.Greater(b.EffectiveMaxHp, a.EffectiveMaxHp, "the armour school bought no health");
            Assert.Greater(b.Hp, a.Hp, "the health was bought but not handed over before the fighting");

            // And the armour is felt, not merely counted.
            float plainDealt = a.TakeDamage(100f);
            float raisedDealt = b.TakeDamage(100f);

            Assert.Less(raisedDealt, plainDealt, "the armour school turned nothing aside");
        }

        [Test]
        public void RangeIsSoldOnlyWhereItDoesSomething()
        {
            // AttackRange reads the special level only for troops with a ranged special,
            // so for a swordsman that track does nothing at all — in the field as well as
            // here. Selling it would be selling nothing.
            Assert.IsTrue(TroopBoonTable.Sells(TroopKind.Archers, UpgradeTrack.Special));
            Assert.IsFalse(TroopBoonTable.Sells(TroopKind.Swordsmen, UpgradeTrack.Special));

            Assert.AreEqual(0, TroopBoonTable.Price(TroopKind.Swordsmen, UpgradeTrack.Special, 0));

            var campaign = new Campaign();
            campaign.Earn(100000);

            Assert.IsFalse(campaign.TryBuy(TroopKind.Swordsmen, UpgradeTrack.Special, out _));
            Assert.IsTrue(campaign.TryBuy(TroopKind.Archers, UpgradeTrack.Special, out _));
        }

        [Test]
        public void TheArchersReachGrowsWithTheSchool()
        {
            var school = new TroopBoons();
            school.Set(TroopKind.Archers, UpgradeTrack.Special, 30);

            var plain = new Squad(12);
            plain.TryPlace(FormationSlot.RightVan, TroopKind.Archers);

            var raised = new Squad(12) { School = school };
            raised.TryPlace(FormationSlot.RightVan, TroopKind.Archers);

            Assert.Greater(raised[FormationSlot.RightVan].AttackRange(TerrainType.Plains),
                           plain[FormationSlot.RightVan].AttackRange(TerrainType.Plains));
        }

        [Test]
        public void TheFieldTrackKeepsItsOwnCapOnTopOfTheSchool()
        {
            // The two are separate axes on purpose: gold must not be able to fill the
            // track the silver economy exists to make you fight for.
            var school = new TroopBoons();
            school.Set(TroopKind.Swordsmen, UpgradeTrack.Weapon, 30);

            var squad = new Squad(12) { School = school };
            squad.TryPlace(FormationSlot.Van, TroopKind.Swordsmen);

            Assert.AreEqual(0, squad[FormationSlot.Van].UpgradeLevel(UpgradeTrack.Weapon),
                "gold filled a field track it should not reach");
            Assert.AreEqual(5, RunEconomy.MaxTrackLevel, "the field cap moved");
        }

        [Test]
        public void TroopLevelsSurviveTheSave()
        {
            var campaign = new Campaign();
            campaign.Earn(5000);
            campaign.TryBuy(TroopKind.Archers, UpgradeTrack.Special, out _);
            campaign.TryBuy(TroopKind.Archers, UpgradeTrack.Special, out _);
            campaign.TryBuy(TroopKind.Spearmen, UpgradeTrack.Weapon, out _);
            campaign.TryBuy(Boon.Purse, out _);

            var loaded = Campaign.Load(campaign.Save());

            Assert.AreEqual(2, loaded.TroopLevel(TroopKind.Archers, UpgradeTrack.Special));
            Assert.AreEqual(1, loaded.TroopLevel(TroopKind.Spearmen, UpgradeTrack.Weapon));
            Assert.AreEqual(0, loaded.TroopLevel(TroopKind.Scout, UpgradeTrack.Weapon));
            Assert.AreEqual(1, loaded.BoonLevel(Boon.Purse));
            Assert.AreEqual(campaign.Gold, loaded.Gold);
        }

        [Test]
        public void EveryOlderSaveStillLoads()
        {
            // Version 1 had no boons; version 2 had no troop levels. Both read as
            // campaigns without what they never had.
            var one = Campaign.Load("1|340|0|1.1.3,1.2.2");
            Assert.AreEqual(340, one.Gold);
            Assert.AreEqual(3, one.Stars(1, 1));
            Assert.AreEqual(0, one.BoonLevel(Boon.Purse));

            var two = Campaign.Load("2|500|10|1.1.3|0.2");
            Assert.AreEqual(500, two.Gold);
            Assert.AreEqual(2, two.BoonLevel(Boon.Purse));
            Assert.AreEqual(0, two.TroopLevel(TroopKind.Archers, UpgradeTrack.Special));
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
            Assert.AreEqual(1f, run.Boons.SilverIncome, 0.0001f);
            Assert.AreEqual(0f, run.Boons.RepairPerSecond, 0.0001f);
            Assert.AreEqual(0f, run.Boons.TreasureGuard, 0.0001f);
            Assert.AreEqual(RunEconomy.SilverPerGold, run.Boons.SilverPerGold);
        }
    }
}
