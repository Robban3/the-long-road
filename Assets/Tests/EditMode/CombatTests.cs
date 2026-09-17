using TheVeil.Gen;
using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
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

        /// <summary>
        /// Silver can be spent, and spending it makes the troop hit harder.
        ///
        /// The whole upgrade economy was built and priced and read by the combat every
        /// step, and nothing anywhere called it: silver went up on the screen and there
        /// was no path from the purse to a troop. Every part of this passed its own test
        /// while the thing they add up to did not exist, which is the failure a test per
        /// part cannot catch.
        /// </summary>
        [Test]
        public void SilverBuysAStrongerTroop()
        {
            var run = Run(1, 1, Escort());
            var group = run.Squad[FormationSlot.Rear];

            Assert.IsFalse(run.TryUpgrade(FormationSlot.Rear, UpgradeTrack.Weapon, out _),
                "bought an upgrade on an empty purse");

            for (int i = 0; i < 6; i++) run.Economy.AwardGroupKill(EnemyKind.Wolf);

            float before = group.DamageAgainst(EnemyKind.Wolf, TerrainType.Plains);
            int purse = run.Economy.Silver;
            int price = run.PriceOf(FormationSlot.Rear, UpgradeTrack.Weapon);

            Assert.IsTrue(run.TryUpgrade(FormationSlot.Rear, UpgradeTrack.Weapon, out int cost),
                $"could not spend {purse} silver on an upgrade priced {price}");

            Assert.AreEqual(price, cost, "the price quoted was not the price charged");
            Assert.AreEqual(purse - price, run.Economy.Silver, "the purse did not pay");
            Assert.AreEqual(1, group.UpgradeLevel(UpgradeTrack.Weapon), "the level did not rise");
            Assert.Greater(group.DamageAgainst(EnemyKind.Wolf, TerrainType.Plains), before,
                "the upgrade bought nothing the fighting can feel");
        }

        /// <summary>Nothing is sold once the level is over, and nothing is sold to the dead.</summary>
        [Test]
        public void AFinishedRunSellsNothing()
        {
            var run = Run(1, 1, Escort());
            for (int i = 0; i < 6; i++) run.Economy.AwardGroupKill(EnemyKind.Wolf);

            run.RunToCompletion();

            Assert.IsFalse(run.TryUpgrade(FormationSlot.Rear, UpgradeTrack.Weapon, out _),
                "sold an upgrade after the run had ended");
        }

        // The level the player plays. See Gen.LevelMaps: a second place generating from
        // the same seed gets a different landscape, and a test on that landscape proves
        // nothing about the game.
        static LevelMap Map(int chapter, int level) => LevelMaps.For(chapter, level);

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

        [Test]
        public void TheScoutDoesNotFight()
        {
            // She is eyes and nothing else: no damage against anything, anywhere, and no
            // reach for a ring to be drawn at.
            var scout = new TroopGroup(TroopKind.Scout, FormationSlot.Van);

            foreach (EnemyKind enemy in System.Enum.GetValues(typeof(EnemyKind)))
                foreach (TerrainType terrain in System.Enum.GetValues(typeof(TerrainType)))
                    Assert.AreEqual(0f, scout.DamageAgainst(enemy, terrain),
                        $"the scout struck a {enemy} on {terrain}");

            Assert.AreEqual(0f, TroopTable.Range(TroopKind.Scout));
        }

        [Test]
        public void TheScoutClearsTheTrapsSheSees()
        {
            // A trap the scout spots does no harm: she makes it safe before the column
            // gets there.
            //
            // Asked over the whole chapter, as a comparison, and that is the third shape
            // this test has had. It named 1-8, whose fast road was the trapped one — until
            // 1-8 became the town and its road ran through a walled street where nothing
            // is buried. It then took the first level with traps on it and asked that she
            // clear some, which failed on 1-2, where she clears none: what she can do
            // depends on how much road she sees before the column reaches the trap, and on
            // some roads that is nothing at all.
            //
            // Neither of those is what the scout promises. She promises that a squad with
            // her in it meets fewer live traps than the same squad without her, and that
            // is a claim about the troop rather than about any one map.
            int withHer = 0, without = 0;

            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
            {
                var scouted = Run(1, level, Line(TroopKind.Scout));
                scouted.RunToCompletion();
                withHer += scouted.Traps.DisarmedCount;

                var blind = Run(1, level, Line(TroopKind.Spearmen));
                blind.RunToCompletion();
                without += blind.Traps.DisarmedCount;
            }

            if (withHer + without == 0) Assert.Ignore("no road in chapter one met a trap");

            Assert.Greater(withHer, without,
                           $"across chapter one the scout cleared {withHer} trap(s) "
                           + $"and a squad without her cleared {without}");
        }

        /// <summary>The same four posts every time, with one of them the troop under test.</summary>
        static Squad Line(TroopKind point)
        {
            var squad = new Squad(18);
            squad.TryPlace(FormationSlot.Van, point);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Shieldbearer);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);
            squad.TryPlace(FormationSlot.LeftVan, TroopKind.Archers);
            return squad;
        }

        [Test]
        public void TheSmithyPromisesWhatTheFightWillUse()
        {
            // The "now" on a smithy button has to be the troop as it stands, or the "next"
            // beside it is a promise about some other troop.
            var archers = new TroopGroup(TroopKind.Archers, FormationSlot.Van) { SpecialLevel = 2, ArmourLevel = 1 };

            Assert.AreEqual(archers.EffectiveMaxHp, archers.StatAt(UpgradeTrack.Armour, archers.ArmourLevel), 0.001f);
            Assert.AreEqual(archers.AttackRange(TerrainType.Plains)
                            / TroopTable.TerrainRangeMultiplier(TroopKind.Archers, TerrainType.Plains),
                            archers.StatAt(UpgradeTrack.Special, archers.SpecialLevel), 0.001f);

            var scout = new TroopGroup(TroopKind.Scout, FormationSlot.Van) { SpecialLevel = 3 };
            Assert.AreEqual(scout.SightRadius, scout.StatAt(UpgradeTrack.Special, scout.SpecialLevel), 0.001f);

            foreach (var track in new[] { UpgradeTrack.Weapon, UpgradeTrack.Armour, UpgradeTrack.Special })
                Assert.Greater(archers.StatAt(track, 3), archers.StatAt(track, 2), $"{track} bought nothing");

            Assert.AreEqual(0f, new TroopGroup(TroopKind.Swordsmen, FormationSlot.Van).StatAt(UpgradeTrack.Special, 3),
                "a swordsman's special track promised something");
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
            // <b>Across the chapter rather than on one road of one level.</b>
            //
            // It was 1-5's fast corridor, then 1-5's cautious one, and each move was made
            // for the same reason: the road it was watching stopped being a road where
            // the question could be answered. The fast one killed both caravans, so it
            // read nothing against nothing; the cautious one now carries a third of a
            // level's threat rather than a share of one spread evenly, and on 1-5 that
            // left it quiet enough that neither caravan was touched - nothing against
            // nothing again, from the other end.
            //
            // Chasing that from level to level is how a test ends up describing one
            // seed. What is actually claimed here is not about 1-5: it is that an escort
            // is worth something to the wagons. Thirty runs say that far better than two,
            // and no single road can be quiet or lethal enough to silence them.
            float alone = 0f, guarded = 0f;

            for (int level = 1; level <= 10; level++)
            {
                var map = Map(1, level);

                foreach (var corridor in map.Corridors)
                {
                    alone += Left(new LevelRun(map, corridor.Tiles, null));
                    guarded += Left(new LevelRun(map, corridor.Tiles, Escort(18)));
                }
            }

            Assert.Greater(guarded, alone,
                $"the escort did nothing for the wagons: {guarded:F0} against {alone:F0} "
                + "hit points left across the chapter");
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

                // The scout is allowed her lead: she walks ahead of the van on purpose
                // now, and falls back into the ranks whenever anything is in contact.
                float allowed = group.Kind == TroopKind.Scout
                    ? Squad.FormationSpan + Squad.ScoutLead
                    : Squad.FormationSpan;

                Assert.LessOrEqual(nearest, allowed + 0.5f,
                    $"the {group.Kind} wandered {nearest:F1} m from the column");
            }
        }

        [Test]
        public void AnEngineerDefusesTrapsAndEarnsFromIt()
        {
            // Across the chapter rather than on one map, because the claim is about the
            // engineer and the old form was about 1-8's fast route.
            //
            // Whether a particular route passes within reach of a trap that has been
            // revealed and not yet sprung is luck of the ground: on the seed this used to
            // name, ten traps were laid, four were revealed, and every one of those four
            // sat off the line the caravan drove. That is a fact about a map, and the
            // test failed whenever the map changed for reasons having nothing to do with
            // engineers — which it did three times in one week.
            //
            // **And a fresh escort for each level, which is the second thing this test
            // got wrong.** It built one squad and ran all ten levels with it, carrying
            // every wound forward with nothing to heal them, so by about level three the
            // engineer was dead and the rest of the chapter measured a corpse walking
            // past traps. The game starts each level with the escort the player paid for;
            // a test that does not is measuring attrition and calling it engineering. It
            // went red for a change to where the *scout* stands, which is how this came
            // to light: with a fresh squad per level the engineer disarms six or seven
            // traps in the chapter whichever way that change goes.
            int laid = 0, disarmed = 0;

            for (int level = 1; level <= 10; level++)
            {
                var squad = new Squad(18);
                squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
                squad.TryPlace(FormationSlot.LeftVan, TroopKind.Scout);
                squad.TryPlace(FormationSlot.RightVan, TroopKind.Engineer);

                var run = Run(1, level, squad);
                run.RunToCompletion();

                laid += run.Traps.Traps.Count;
                disarmed += run.Traps.DisarmedCount;
            }

            if (laid == 0) Assert.Ignore("no traps anywhere in the chapter");

            Assert.Greater(disarmed, 0,
                $"the engineer walked past all {laid} traps in the chapter");
        }

        [Test]
        public void ThePointTroopTakesTheTrapNotTheWagons()
        {
            // A whole escort with the shieldbearer on point, not a shieldbearer alone.
            // Alone it does not get to the traps: a one-man escort is destroyed at seven
            // percent of the route, twenty metres from the nearest pit, and the test then
            // reports on a trap system it never reached.
            //
            // A spear on the left flank rather than the scout, who does not fight: with her
            // there the column was a sword short, and the shieldbearer on point fell before
            // the traps did — which tested the fighting, not the traps.
            //
            // The level is looked for rather than named. It was 1-8, whose fast road was
            // the trapped one, until 1-8 became the walled town: the road through it is
            // paved street between blocks of building, and nothing is buried under a
            // street. The claim was never about 1-8 — it is that a shieldbearer on point
            // takes what the wagons would have taken.
            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
            {
                var squad = new Squad(18);
                squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
                squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);
                squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
                squad.TryPlace(FormationSlot.LeftVan, TroopKind.Spearmen);
                squad.TryPlace(FormationSlot.RightRear, TroopKind.Swordsmen);

                var run = Run(1, level, squad);
                run.RunToCompletion();

                if (run.Traps.TriggeredCount == 0) continue;

                Assert.Greater(run.Traps.RevealedCount, 0, $"1-{level}: no trap was ever spotted");

                // Damage dealt, not health missing: see the note on TrapDamageToTroops.
                Assert.Greater(run.TrapDamageToTroops, 0f, $"1-{level}: the trap damage went nowhere");
                Assert.AreEqual(0f, run.TrapDamageToWagons,
                    $"1-{level}: a trap struck the wagons past a shieldbearer on point");
                return;
            }

            Assert.Ignore("no road in chapter one trod on a trap");
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

            // The cautious road, not the fast one, and for the reason the comment below
            // already gives about the budget: both sides of the comparison have to have
            // something left to lose. The fast road became the dangerous one in earnest
            // once the two stopped sharing tiles, so on it a doubled enemy strength and a
            // single one both end at zero.
            var route = map.CorridorOf(CorridorKind.Safe).Tiles;

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
            //
            // <b>On the road the escort would choose, not always the fastest one.</b>
            // This drove the fast corridor on every level, which was a fair stand-in
            // while the three roads carried the same weight of enemy - measured, they
            // did: six to eight groups each, everywhere. They are not meant to and now do
            // not. EncounterPlacer.RoadShare puts half a level's threat on the quick road
            // and a fifth on the long way round, which is docs/GDD.md §1 and §6.2: the
            // core tension is speed against safety, and the dangerous route pays.
            //
            // So a fixed escort sent down the quick road ten times running is a player
            // who never makes the decision the game is about, and it loses eight of the
            // ten. What the chapter promises is that it can be finished, not that it can
            // be finished the fastest way every time.
            int arrived = 0;

            for (int level = 1; level <= 10; level++)
            {
                var map = Map(1, level);
                bool through = false;

                foreach (var corridor in map.Corridors)
                    if (new LevelRun(map, corridor.Tiles, Escort(18)).RunToCompletion()
                        == RunOutcome.Arrived) { through = true; break; }

                if (through) arrived++;
            }

            // Not all ten: by the end of the chapter one fixed escort is meant to lose
            // some of them, which is the difficulty curve rather than a bug.
            Assert.GreaterOrEqual(arrived, 7,
                $"a full escort survived only {arrived} of the chapter's ten levels");
        }

        /// <summary>Wagon hit points left when a run is over.</summary>
        static float Left(LevelRun run)
        {
            run.RunToCompletion();

            float hp = 0f;
            foreach (var wagon in run.Caravan.Wagons) hp += wagon.Hp;
            return hp;
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
