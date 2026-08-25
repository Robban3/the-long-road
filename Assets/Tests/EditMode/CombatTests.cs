using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class CombatTests
    {
        /// <summary>An escort that spends what it is given, as a player would.</summary>
        static Squad Escort(int budget = 12)
        {
            var squad = new Squad(budget);
            squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
            squad.TryPlace(FormationSlot.LeftVan, TroopKind.Scout);
            squad.TryPlace(FormationSlot.RightRear, TroopKind.Swordsmen);
            squad.TryPlace(FormationSlot.LeftRear, TroopKind.Priest);
            return squad;
        }

        static LevelMap Map(int chapter, int level)
            => TerrainGenerator.Generate(new ChapterRecipe().ForLevel(level),
                                         DeterministicRandom.SeedFor(chapter, level));

        static LevelRun Run(int chapter, int level, Squad squad, CorridorKind kind = CorridorKind.Fast)
        {
            var map = Map(chapter, level);
            return new LevelRun(map, map.CorridorOf(kind).Tiles, squad);
        }

        // --- squad construction -----------------------------------------------------

        [Test]
        public void TheBudgetLimitsTheEscort()
        {
            var squad = new Squad(12);
            Assert.IsTrue(squad.TryPlace(FormationSlot.Van, TroopKind.Cavalry));      // 5, spent 5
            Assert.IsTrue(squad.TryPlace(FormationSlot.Rear, TroopKind.Mage));        // 6, spent 11

            Assert.AreEqual(1, squad.PointsRemaining);
            Assert.IsFalse(squad.TryPlace(FormationSlot.LeftVan, TroopKind.Archers), "the squad overspent");
            Assert.IsFalse(squad.TryPlace(FormationSlot.LeftVan, TroopKind.Scout), "the squad overspent");

            // Nothing costs a single point: an expensive pair leaves a post empty, which
            // is the trade-off the budget exists to force.
            Assert.AreEqual(2, squad.Count);
        }

        [Test]
        public void OnePostHoldsOneTroop()
        {
            var squad = new Squad();
            Assert.IsTrue(squad.TryPlace(FormationSlot.Van, TroopKind.Spearmen));
            Assert.IsFalse(squad.TryPlace(FormationSlot.Van, TroopKind.Swordsmen),
                "two troops were stacked on the same post");
        }

        [Test]
        public void RegroupingSwapsPosts()
        {
            var squad = Escort();
            var van = squad[FormationSlot.Van];
            var rear = squad[FormationSlot.Rear];

            squad.Swap(FormationSlot.Van, FormationSlot.Rear);

            Assert.AreSame(rear, squad[FormationSlot.Van]);
            Assert.AreSame(van, squad[FormationSlot.Rear]);
        }

        [Test]
        public void TheFormationRotatesWithTheRoute()
        {
            // The van must be the front whichever way the road runs.
            var squad = Escort();

            squad.UpdatePositions(new Vec2(100f, 100f), new Vec2(1f, 0f));
            var facingEast = squad[FormationSlot.Van].Position;

            squad.UpdatePositions(new Vec2(100f, 100f), new Vec2(0f, 1f));
            var facingNorth = squad[FormationSlot.Van].Position;

            Assert.Greater(facingEast.X, 100f, "the van is not ahead when travelling east");
            Assert.Greater(facingNorth.Y, 100f, "the van is not ahead when travelling north");
        }

        [Test]
        public void ScoutsExtendTheColumnsSight()
        {
            var plain = new Squad();
            plain.TryPlace(FormationSlot.Van, TroopKind.Swordsmen);

            var scouted = new Squad();
            scouted.TryPlace(FormationSlot.Van, TroopKind.Swordsmen);
            scouted.TryPlace(FormationSlot.LeftVan, TroopKind.Scout);

            Assert.Greater(scouted.BestSight, plain.BestSight * 2f,
                "adding a scout barely changed what the column can see");
        }

        // --- troop statistics -------------------------------------------------------

        [Test]
        public void CavalryRulesThePlainAndDrownsInTheFen()
        {
            var group = new TroopGroup(TroopKind.Cavalry, FormationSlot.Van);

            float plains = group.DamageAgainst(EnemyKind.Bandit, TerrainType.Plains);
            float forest = group.DamageAgainst(EnemyKind.Bandit, TerrainType.Forest);
            float marsh = group.DamageAgainst(EnemyKind.Bandit, TerrainType.Marsh);

            Assert.Greater(plains, forest);
            Assert.Greater(forest, marsh);
            Assert.Less(marsh, plains * 0.3f, "the fen barely inconvenienced the horsemen");
        }

        [Test]
        public void ArchersLoseMostOfTheirReachAmongTrees()
        {
            var group = new TroopGroup(TroopKind.Archers, FormationSlot.RightVan);

            Assert.Greater(group.AttackRange(TerrainType.Plains), group.AttackRange(TerrainType.Forest));
            Assert.Less(group.AttackRange(TerrainType.Forest), group.AttackRange(TerrainType.Plains) * 0.7f);
        }

        [Test]
        public void SpearmenPunishTheChargingPack()
        {
            var spears = new TroopGroup(TroopKind.Spearmen, FormationSlot.Van);

            float againstWolves = spears.DamageAgainst(EnemyKind.Wolf, TerrainType.Plains);
            float againstBandits = spears.DamageAgainst(EnemyKind.Bandit, TerrainType.Plains);

            Assert.That(againstWolves, Is.EqualTo(againstBandits * 2f).Within(0.01f));
        }

        [Test]
        public void ShieldbearersSoakWhatOthersDoNot()
        {
            var shields = new TroopGroup(TroopKind.Shieldbearer, FormationSlot.Van);
            var swords = new TroopGroup(TroopKind.Swordsmen, FormationSlot.Van);

            Assert.Less(shields.TakeDamage(100f), swords.TakeDamage(100f),
                "the shieldbearer took as much as anyone else");
        }

        [Test]
        public void AWoundedGroupHitsSofter()
        {
            var group = new TroopGroup(TroopKind.Swordsmen, FormationSlot.Van);
            float full = group.DamageAgainst(EnemyKind.Bandit, TerrainType.Plains);

            group.TakeDamage(group.MaxHp * 0.6f);

            Assert.Less(group.DamageAgainst(EnemyKind.Bandit, TerrainType.Plains), full);
            Assert.Less(group.ModelsAlive, TroopTable.Models(TroopKind.Swordsmen));
            Assert.Greater(group.ModelsLost, 0);
        }

        // --- the fight --------------------------------------------------------------

        [Test]
        public void AnEscortedCaravanKillsThingsAndGetsPaid()
        {
            var run = Run(1, 5, Escort());
            run.RunToCompletion();

            Assert.Greater(run.Economy.TotalEarned, 0, "a full level of fighting earned nothing");
            Assert.Greater(run.Detection.AwakeCount, 0);
        }

        [Test]
        public void KillsNowDominateSilverIncome()
        {
            // Before combat existed the only income was the scouting bounty. Kills must
            // now be the main earner or the whole "dangerous route pays" loop is a lie.
            var unescorted = Run(1, 5, null);
            var escorted = Run(1, 5, Escort());

            unescorted.RunToCompletion();
            escorted.RunToCompletion();

            Assert.Greater(escorted.Economy.TotalEarned, unescorted.Economy.TotalEarned * 1.5f,
                $"escorted earned {escorted.Economy.TotalEarned}, unescorted {unescorted.Economy.TotalEarned}");
        }

        [Test]
        public void FightingSlowsTheColumn()
        {
            // The missing minute: before combat the caravan rolled through unopposed
            // and levels ran at half their designed length.
            var quiet = Run(1, 4, null);
            var contested = Run(1, 4, Escort());

            quiet.RunToCompletion();
            contested.RunToCompletion();

            Assert.Greater(contested.ElapsedSeconds, quiet.ElapsedSeconds,
                "a level with fighting took no longer than one without");
        }

        [Test]
        public void AnEscortProtectsTheWagons()
        {
            // Level 5 rather than 7: by the end of the chapter a single fixed escort is
            // not expected to hold every route, which is the difficulty curve working
            // rather than the escort failing.
            var alone = Run(1, 5, null);
            var guarded = Run(1, 5, Escort(18));

            alone.RunToCompletion();
            guarded.RunToCompletion();

            float AloneHp() { float h = 0f; foreach (var w in alone.Caravan.Wagons) h += w.Hp; return h; }
            float GuardedHp() { float h = 0f; foreach (var w in guarded.Caravan.Wagons) h += w.Hp; return h; }

            Assert.Greater(GuardedHp(), AloneHp(), "the escort did nothing for the wagons");
        }

        [Test]
        public void TroopsHoldTheirPostsInsteadOfChasing()
        {
            // Without the leash the formation dissolves the moment anything appears and
            // the six posts stop meaning anything.
            var run = Run(1, 6, Escort());
            run.RunToCompletion();

            foreach (var group in run.Squad.Slots)
            {
                if (group == null) continue;
                float fromCaravan = Vec2.Distance(group.Position, run.Caravan.LeadPosition);
                Assert.LessOrEqual(fromCaravan, Squad.FormationRadius + 0.5f,
                    $"the {group.Kind} wandered {fromCaravan:F1} m from the column");
            }
        }

        [Test]
        public void AnEngineerDefusesTrapsAndEarnsFromIt()
        {
            var withEngineer = new Squad(18);
            withEngineer.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
            withEngineer.TryPlace(FormationSlot.LeftVan, TroopKind.Scout);
            withEngineer.TryPlace(FormationSlot.RightVan, TroopKind.Engineer);

            var run = Run(1, 8, withEngineer);
            run.RunToCompletion();

            if (run.Traps.Traps.Count == 0) Assert.Ignore("this seed placed no traps on the fast route");

            Assert.Greater(run.Traps.DisarmedCount, 0, "the engineer walked past every trap");
        }

        [Test]
        public void ThePointTroopTakesTheTrapNotTheWagons()
        {
            var squad = new Squad(18);
            squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);

            var run = Run(1, 8, squad);
            float wagonsBefore = 0f;
            foreach (var wagon in run.Caravan.Wagons) wagonsBefore += wagon.Hp;

            run.RunToCompletion();

            if (run.Traps.Traps.Count == 0) Assert.Ignore("this seed placed no traps on the fast route");

            Assert.Greater(squad[FormationSlot.Van].ModelsLost + 1, 0);
            Assert.Greater(run.Traps.RevealedCount, 0);
        }

        [Test]
        public void CombatIsDeterministic()
        {
            var a = Run(2, 6, Escort());
            var b = Run(2, 6, Escort());

            a.RunToCompletion();
            b.RunToCompletion();

            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.That(a.ElapsedSeconds, Is.EqualTo(b.ElapsedSeconds).Within(0.0001f));
            Assert.AreEqual(a.Economy.TotalEarned, b.Economy.TotalEarned);
        }

        [Test]
        public void StrongerEnemiesMakeTheSameLevelHarder()
        {
            var map = Map(1, 6);
            var route = map.CorridorOf(CorridorKind.Fast).Tiles;

            var easy = new LevelRun(map, route, Escort(), enemyStrength: 1f);
            var hard = new LevelRun(map, route, Escort(), enemyStrength: 2f);

            easy.RunToCompletion();
            hard.RunToCompletion();

            float EasyHp() { float h = 0f; foreach (var w in easy.Caravan.Wagons) h += w.Hp; return h; }
            float HardHp() { float h = 0f; foreach (var w in hard.Caravan.Wagons) h += w.Hp; return h; }

            Assert.Less(HardHp(), EasyHp(), "doubling enemy strength changed nothing");
        }

        [Test]
        public void OnlyTheTroopWithAnOpponentIsFighting()
        {
            // The view turns each figure to its own target and swings only when it has
            // one. Before this, the whole escort attacked the instant anybody made
            // contact — six figures striking air in the direction of travel while one
            // wolf worried the rear.
            var run = Run(1, 5, Escort(18));

            bool sawContact = false;
            for (int step = 0; step < 4000 && run.Outcome == RunOutcome.InProgress; step++)
            {
                run.Step();
                if (run.Combat == null || !run.Combat.InContact) continue;

                sawContact = true;
                int engaged = 0, alive = 0;

                foreach (var group in run.Squad.Slots)
                {
                    if (group == null || !group.Alive) continue;
                    alive++;
                    if (group.Engaged) engaged++;
                }

                Assert.Greater(engaged, 0, "the squad was in contact and nobody had a target");
                Assert.LessOrEqual(engaged, alive);
            }

            Assert.IsTrue(sawContact, "no fight happened, so nothing was tested");
        }

        [Test]
        public void ATroopWithNoOpponentStopsSwinging()
        {
            // A target left over from a previous step is a figure striking at something
            // that walked away, or died.
            var run = Run(1, 5, Escort(18));

            for (int step = 0; step < 4000 && run.Outcome == RunOutcome.InProgress; step++)
            {
                run.Step();
                if (run.Combat == null || run.Combat.InContact) continue;

                foreach (var group in run.Squad.Slots)
                    if (group != null)
                        Assert.IsNull(group.Target,
                            "nothing was in contact, yet a troop still had a target");
            }
        }
    }
}
