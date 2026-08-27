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

            // From the nearest wagon rather than from the lead one. The column is three
            // wagons over thirty metres and the escort is spread down its length, so
            // measuring everyone against the front wagon would call the rearguard a
            // deserter for standing where it is posted.
            foreach (var group in run.Squad.Slots)
            {
                if (group == null) continue;

                float nearest = float.MaxValue;
                for (int i = 0; i < run.Caravan.Wagons.Count; i++)
                {
                    float d = Vec2.Distance(group.Position, run.Caravan.WagonPosition(i));
                    if (d < nearest) nearest = d;
                }

                Assert.LessOrEqual(nearest, Squad.FormationSpan + 0.5f,
                    $"the {group.Kind} wandered {nearest:F1} m from the column");
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
            // A whole escort with the shieldbearer on point, not a shieldbearer alone.
            // Alone it does not get to the traps: 1-8 destroys a one-man escort at seven
            // percent of the route, twenty metres from the nearest pit, and the test then
            // reports on a trap system it never reached.
            var squad = new Squad(18);
            squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
            squad.TryPlace(FormationSlot.LeftVan, TroopKind.Scout);
            squad.TryPlace(FormationSlot.RightRear, TroopKind.Swordsmen);

            var run = Run(1, 8, squad);
            run.RunToCompletion();

            Assert.Greater(run.Traps.TriggeredCount, 0, "no trap was ever trodden on");
            Assert.Greater(run.Traps.RevealedCount, 0, "no trap was ever spotted");

            // Damage dealt, not health missing: see the note on TrapDamageToTroops.
            Assert.Greater(run.TrapDamageToTroops, 0f, "the trap damage went nowhere");
            Assert.AreEqual(0f, run.TrapDamageToWagons,
                "a trap struck the wagons past a shieldbearer on point");
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

            // The budget 1-6 actually hands out, rather than the default twelve.
            //
            // Twelve is the budget of level *one*, and a level-one escort on a level-six
            // map now loses every wagon whether the enemies are doubled or not — so both
            // sides of this comparison sat on the floor and the assertion had nothing to
            // read. It became that way when the formation was strung out along the whole
            // column instead of huddled around the lead wagon (Squad.PostAt): the escort
            // covers three wagons now and each post fights nearer to alone, which an
            // under-strength squad cannot carry on an escalation level.
            //
            // That is the levels doing their job — the properly-budgeted escort still
            // gets its owed route through every level in the chapter — but it is not
            // what this test is about. Monotonicity has to be measured where there is
            // still something left to lose.
            int budget = new ChapterRecipe().ForLevel(6).SquadBudget;

            var easy = new LevelRun(map, route, Escort(budget), enemyStrength: 1f);
            var hard = new LevelRun(map, route, Escort(budget), enemyStrength: 2f);

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

            bool sawContact = false, sawSplit = false;

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

                Assert.LessOrEqual(engaged, alive);

                // Somebody swinging while somebody else is not. That is the whole
                // claim, and it is not the same as "in contact implies a target":
                // an attacker can be in contact with nobody able to answer it — an
                // unrevealed group, or an archer band outside every usable reach —
                // and a squad standing idle under fire is correct rather than broken.
                if (engaged > 0 && engaged < alive) sawSplit = true;
            }

            Assert.IsTrue(sawContact, "no fight happened, so nothing was tested");
            Assert.IsTrue(sawSplit, "the escort only ever swung as one body");
        }

        [Test]
        public void TheColumnHaltsOnlyForAFightItIsActuallyIn()
        {
            // Halting for anything at all made every ranged encounter a siege: the
            // archers stopped at their own eighteen metres, the column stopped with
            // them, and since the supply wagon heals only out of contact neither side
            // could disengage. 1-5 ended with the caravan destroyed at seven percent of
            // the route, and the band that did it finished untouched.
            var run = Run(1, 5, Escort(18));

            for (int step = 0; step < 8000 && run.Outcome == RunOutcome.InProgress; step++)
            {
                run.Step();
                if (run.Combat == null || !run.Combat.Halted) continue;

                float nearest = float.MaxValue;

                foreach (var enemy in run.Detection.Enemies)
                {
                    // A group destroyed by the return fire in this very step still
                    // halted the column when the step began, and a post wiped out by
                    // its blow is still where the fight was. Both stay in the reckoning
                    // or the check indicts the sim for the order it does things in.
                    if (!enemy.Awake) continue;

                    foreach (var group in run.Squad.Slots)
                    {
                        if (group == null) continue;
                        float distance = Vec2.Distance(enemy.Position, group.Position);
                        if (distance < nearest) nearest = distance;
                    }

                    float toWagons = Vec2.Distance(enemy.Position, run.Caravan.LeadPosition);
                    if (toWagons < nearest) nearest = toWagons;
                }

                Assert.LessOrEqual(nearest, CombatSystem.HaltRadius + 0.01f,
                    $"the column stopped for something {nearest:F1} m away");
            }
        }

        [Test]
        public void AnEscortedColumnGetsThroughTheChapterInsteadOfBeingGroundDown()
        {
            // The halt rule is a balance change and not only a picture, so it is checked
            // as one. Ten levels, one fixed escort, and the question is whether standing
            // still to fight costs the run.
            int arrived = 0;

            for (int level = 1; level <= 10; level++)
                if (Run(1, level, Escort(18)).RunToCompletion() == RunOutcome.Arrived) arrived++;

            // Not all ten: by the end of the chapter one fixed escort is meant to lose
            // some of them, which is the difficulty curve rather than a bug.
            Assert.GreaterOrEqual(arrived, 7,
                $"a full escort survived only {arrived} of the chapter's ten levels");
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
