using System.Collections.Generic;

namespace Arna.Sim
{
    public enum RunOutcome : byte
    {
        InProgress = 0,
        Arrived = 1,
        CaravanLost = 2
    }

    /// <summary>
    /// One playthrough of one level: the caravan moving, threats being noticed, traps
    /// going off, troops fighting, silver accumulating.
    ///
    /// Runs on a fixed 20 Hz step with no UnityEngine anywhere in reach. That is what
    /// makes the whole thing deterministic, replayable from a seed and a list of
    /// inputs, testable without opening the editor, and fast-forwardable simply by
    /// taking more steps per frame (docs/technical-design.md §2).
    /// </summary>
    public sealed class LevelRun
    {
        public const float StepSeconds = 0.05f;

        /// <summary>Sight of the caravan's own driver, with no scout along.</summary>
        public const float CaravanSight = 12f;

        /// <summary>Multiplier on par time for the third star (docs/GDD.md §8.4).</summary>
        public const float ParTimeFactor = 1.35f;

        readonly List<Watcher> _watchers = new List<Watcher>();
        float _accumulator;

        public LevelRun(LevelMap map, IReadOnlyList<int> route, Squad squad = null, float enemyStrength = 1f)
        {
            Caravan = new Caravan(map.Grid, route);
            Squad = squad;
            Detection = new DetectionSystem(map.Grid, map.Encounters.Enemies);
            Traps = new TrapField(map.Grid, map.Encounters.Traps);
            Economy = new RunEconomy();

            // Combat runs whether or not anyone came along to fight it.
            //
            // Making it conditional on a squad meant an unescorted caravan was never
            // attacked at all — enemies woke, ran up and did nothing — so travelling
            // alone was strictly safer than bringing troops. That inverted the entire
            // premise: the escort exists because the road is dangerous without one.
            Combat = new CombatSystem(map.Grid, Caravan, squad ?? new Squad(0), Detection, enemyStrength);

            ParSeconds = map.FastestRouteCost / Caravan.BaseTilesPerSecond * ParTimeFactor;

            Obstacles = new ObstacleField();
            Combat.Obstacles = Obstacles;
            if (Squad != null) Squad.Obstacles = Obstacles;
        }

        /// <summary>
        /// What is standing on the ground, once the view has said.
        ///
        /// Empty until <c>RunVisuals</c> fills it, and a run with nothing in it behaves
        /// exactly as this game always has: every test here and every headless run walks
        /// an empty country. That is what keeps a scenery change out of the balance.
        /// </summary>
        public ObstacleField Obstacles { get; }

        public Caravan Caravan { get; }
        public Squad Squad { get; }
        public DetectionSystem Detection { get; }
        public TrapField Traps { get; }
        public RunEconomy Economy { get; }

        public CombatSystem Combat { get; }

        public float ElapsedSeconds { get; private set; }

        /// <summary>
        /// Seconds the caravan spent travelling, with the fighting taken out.
        ///
        /// This is what par is compared against, and the two were never the same thing.
        /// `ParSeconds` is derived from the *route's* cost — how far and over what
        /// ground — so measuring it against a clock that also counts standing still in
        /// a fight compares unlike quantities. It only went unnoticed while a fight
        /// merely slowed the column instead of halting it.
        ///
        /// What it keeps intact is the third star asking two separate questions. Time
        /// is the route you drew; blood is the fights you took. Let combat spend both
        /// and they collapse into one, and the choice between the fast way and the safe
        /// way stops being a choice.
        /// </summary>
        public float TravelSeconds { get; private set; }

        /// <summary>Seconds spent halted with something in contact.</summary>
        public float FightingSeconds => ElapsedSeconds - TravelSeconds;
        public float ParSeconds { get; }

        /// <summary>Extra trap-spotting range, on top of whatever the squad provides.</summary>
        public float TrapSight { get; set; }

        /// <summary>Sight floor. The squad's best scout is used when it sees further.</summary>
        public float LookoutSight { get; set; } = CaravanSight;

        public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;

        public float EffectiveSight
        {
            get
            {
                float squadSight = Squad?.BestSight ?? 0f;
                return squadSight > LookoutSight ? squadSight : LookoutSight;
            }
        }

        public float EffectiveTrapSight
        {
            get
            {
                float squadSight = Squad?.BestTrapSight ?? 0f;
                return squadSight > TrapSight ? squadSight : TrapSight;
            }
        }

        /// <summary>Advances real time, running whole simulation steps as they come due.</summary>
        public void Advance(float deltaTime)
        {
            if (Outcome != RunOutcome.InProgress) return;

            _accumulator += deltaTime;
            while (_accumulator >= StepSeconds && Outcome == RunOutcome.InProgress)
            {
                _accumulator -= StepSeconds;
                Step();
            }
        }

        /// <summary>One fixed simulation step.</summary>
        public void Step()
        {
            if (Outcome != RunOutcome.InProgress) return;

            ElapsedSeconds += StepSeconds;
            if (Combat == null || !Combat.Halted) TravelSeconds += StepSeconds;

            Caravan.Tick(StepSeconds);

            // The scout walks out in front while the road is quiet and falls back into
            // the ranks the moment anything is fighting. Set before the posts are worked
            // out, so she is already coming back on the step contact is made rather than
            // one step later, standing alone in front of it.
            if (Squad != null) Squad.Scouting = Combat == null || !Combat.InContact;

            // Around the whole column, not around its first wagon. See Squad.PostAt.
            Squad?.UpdatePositions(Caravan);
            RefreshWatchers();

            if (Detection.Tick(StepSeconds, Caravan.LeadPosition, _watchers))
                PayForSpotting();

            if (Combat != null)
            {
                Combat.Step(StepSeconds);
                PayForKills();
            }

            // The engineer works before the wagons arrive, not after they set it off.
            //
            // Traps.Update both reveals and triggers, and it ran first: a trap first seen
            // at the moment the lead wagon came within its three metres was sprung in the
            // same call, and the engineer — looking afterwards — found nothing live to
            // work on. Ten traps on 1-8, four of them revealed, none ever disarmed.
            // Moved ahead of it, he acts on what was revealed last tick, which is the
            // whole of what a sapper does: clear it before the column reaches it.
            WorkTheEngineer();

            Traps.Update(Caravan.LeadPosition, _watchers, EffectiveTrapSight);
            ApplyTrapDamage();

            if (Caravan.Destroyed) Outcome = RunOutcome.CaravanLost;
            else if (Caravan.HasArrived) Outcome = RunOutcome.Arrived;
        }

        /// <summary>Runs to completion. Used by tests and by headless balancing.</summary>
        public RunOutcome RunToCompletion(float timeoutSeconds = 900f)
        {
            while (Outcome == RunOutcome.InProgress && ElapsedSeconds < timeoutSeconds) Step();
            return Outcome;
        }

        void RefreshWatchers()
        {
            _watchers.Clear();

            if (Squad != null)
            {
                foreach (var group in Squad.Slots)
                    if (group != null && group.Alive)
                        _watchers.Add(new Watcher(group.Position, group.SightRadius));
            }

            _watchers.Add(new Watcher(Caravan.LeadPosition, LookoutSight));
        }

        void PayForSpotting()
        {
            foreach (var enemy in Detection.RevealedThisTick)
                if (enemy.SpottedEarly) Economy.AwardScouting();
        }

        void PayForKills()
        {
            foreach (var defeat in Combat.DefeatedThisStep)
                Economy.AwardGroupKill(defeat.Kind, defeat.Flawless);
        }

        /// <summary>
        /// Traps strike the troop holding the van if there is one, and the lead wagon
        /// otherwise. The shieldbearer's damage reduction applies, which is what makes
        /// putting one on point a real answer to a trapped route (docs/GDD.md §7.2).
        /// </summary>
        void ApplyTrapDamage()
        {
            foreach (var trap in Traps.TriggeredThisTick)
            {
                float damage = TrapTable.Damage(trap.Kind);

                var point = Squad?[FormationSlot.Van];
                if (point != null && point.Alive)
                {
                    point.TakeDamage(damage);
                    TrapDamageToTroops += damage;
                    continue;
                }

                foreach (var wagon in Caravan.Wagons)
                {
                    if (wagon.Destroyed) continue;
                    wagon.ApplyDamage(damage);
                    TrapDamageToWagons += damage;
                    break;
                }
            }
        }

        /// <summary>
        /// What the traps actually did, kept because the health left at the end does not
        /// say.
        ///
        /// Two tests asserted that the troop on point finishes a trapped route hurt, and
        /// both were wrong to. A priest in the column heals between fights, so on a level
        /// the escort wins comfortably the van is back at full health by the goal — six
        /// traps went off on 1-8 and the point troop arrived at 660 of 660. One of those
        /// tests then *passed* for a while because wolves were hurting the troop instead,
        /// which is worse than failing: it was reporting on the trap system and measuring
        /// something else entirely.
        ///
        /// A running total cannot be healed away.
        /// </summary>
        public float TrapDamageToTroops { get; private set; }
        public float TrapDamageToWagons { get; private set; }

        /// <summary>
        /// An engineer defuses revealed traps within reach as the column passes, and is
        /// paid for it — which is how a troop that kills almost nothing stays
        /// affordable in an economy driven by kills.
        /// </summary>
        void WorkTheEngineer()
        {
            if (Squad == null || !Squad.HasEngineer) return;

            foreach (var group in Squad.Slots)
            {
                if (group == null || !group.Alive || !TroopTable.CanDisarmTraps(group.Kind)) continue;

                var disarmed = Traps.TryDisarmNearest(group.Position);
                if (disarmed != null) Economy.AwardTrapDisarm(disarmed.Kind);
            }
        }

        /// <summary>Result rating, 0 if the caravan never arrived (docs/GDD.md §8.4).</summary>
        public int Stars
        {
            get
            {
                if (Outcome != RunOutcome.Arrived) return 0;

                int surviving = 0;
                bool allHealthy = true;
                foreach (var wagon in Caravan.Wagons)
                {
                    if (wagon.Destroyed) continue;
                    surviving++;
                    if (wagon.HpFraction < 0.6f) allHealthy = false;
                }

                if (surviving == 0) return 0;
                if (surviving < Caravan.Wagons.Count) return 1;
                return allHealthy && TravelSeconds <= ParSeconds ? 3 : 2;
            }
        }

        /// <summary>Gold earned, loot scaled by the treasure wagon's condition.</summary>
        public int GoldEarned(int baseReward = 40, int perWagon = 15, int treasureValue = 60)
        {
            if (Outcome != RunOutcome.Arrived) return 0;

            int gold = baseReward;
            foreach (var wagon in Caravan.Wagons)
                if (!wagon.Destroyed) gold += perWagon;

            gold += (int)(treasureValue * Caravan.LootFraction);
            gold += Economy.ConvertLeftoverToGold();
            return gold;
        }

        /// <summary>
        /// Spends silver on one post, mid-level. Returns whether it was bought and what
        /// it cost.
        ///
        /// **The half of the economy that was never joined up.** Everything either side
        /// of this existed and was tested: silver is earned from kills, scouting and
        /// disarmed traps; the tracks are priced 20/32/51/82/131 and each level of them
        /// is read by the combat every step. There was simply no call anywhere that took
        /// silver out of the purse and put a level on a troop. The number went up on the
        /// screen and nothing could ever be done with it — which is why the difficulty
        /// curve read as the game getting harder while the player stood still. It was.
        ///
        /// Mid-level rather than between levels, because silver does not survive the
        /// level (see RunEconomy): spending it is the level's own decision, made while
        /// it is still worth something.
        /// </summary>
        public bool TryUpgrade(FormationSlot slot, UpgradeTrack track, out int cost)
        {
            cost = 0;

            var group = Squad?[slot];
            if (group == null || !group.Alive) return false;
            if (Outcome != RunOutcome.InProgress) return false;

            if (!Economy.TryUpgrade(group.UpgradeLevel(track), group.CostMultiplier(track), out cost))
                return false;

            group.RaiseLevel(track);
            return true;
        }

        /// <summary>What the next level of a track would cost, or zero when it is capped.</summary>
        public int PriceOf(FormationSlot slot, UpgradeTrack track)
        {
            var group = Squad?[slot];
            if (group == null) return 0;

            int level = group.UpgradeLevel(track);
            if (level >= RunEconomy.MaxTrackLevel) return 0;

            return RunEconomy.UpgradeCost(level, group.CostMultiplier(track));
        }
    }
}
