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
    /// going off, silver accumulating.
    ///
    /// Runs on a fixed 20 Hz step with no UnityEngine anywhere in reach. That is what
    /// makes the whole thing deterministic, replayable from a seed and a list of
    /// inputs, testable without opening the editor, and fast-forwardable simply by
    /// taking more steps per frame (docs/technical-design.md §2).
    /// </summary>
    public sealed class LevelRun
    {
        public const float StepSeconds = 0.05f;

        /// <summary>Sight of the caravan's own lookout, before any scout is added.</summary>
        public const float CaravanSight = 12f;

        /// <summary>Multiplier on par time for the third star (docs/GDD.md §8.4).</summary>
        public const float ParTimeFactor = 1.35f;

        readonly LevelMap _map;
        readonly List<Watcher> _watchers = new List<Watcher>();

        float _accumulator;

        public LevelRun(LevelMap map, IReadOnlyList<int> route)
        {
            _map = map;
            Caravan = new Caravan(map.Grid, route);
            Detection = new DetectionSystem(map.Grid, map.Encounters.Enemies);
            Traps = new TrapField(map.Grid, map.Encounters.Traps);
            Economy = new RunEconomy();

            ParSeconds = map.FastestRouteCost / Caravan.BaseTilesPerSecond * ParTimeFactor;
        }

        public Caravan Caravan { get; }
        public DetectionSystem Detection { get; }
        public TrapField Traps { get; }
        public RunEconomy Economy { get; }

        public float ElapsedSeconds { get; private set; }
        public float ParSeconds { get; }

        /// <summary>Extra trap-spotting range from a scout or engineer in the column.</summary>
        public float TrapSight { get; set; }

        /// <summary>Sight radius of the caravan's best lookout. Raised by taking a scout.</summary>
        public float LookoutSight { get; set; } = CaravanSight;

        public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;

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
            Caravan.Tick(StepSeconds);

            RefreshWatchers();

            if (Detection.Tick(StepSeconds, Caravan.LeadPosition, _watchers))
                PayForSpotting();

            Traps.Update(Caravan.LeadPosition, _watchers, TrapSight);
            ApplyTrapDamage();

            if (Caravan.Destroyed) Outcome = RunOutcome.CaravanLost;
            else if (Caravan.HasArrived) Outcome = RunOutcome.Arrived;
        }

        /// <summary>Runs to completion. Used by tests and by headless balancing.</summary>
        public RunOutcome RunToCompletion(float timeoutSeconds = 600f)
        {
            while (Outcome == RunOutcome.InProgress && ElapsedSeconds < timeoutSeconds) Step();
            return Outcome;
        }

        void RefreshWatchers()
        {
            _watchers.Clear();
            _watchers.Add(new Watcher(Caravan.LeadPosition, LookoutSight));
        }

        void PayForSpotting()
        {
            foreach (var enemy in Detection.RevealedThisTick)
                if (enemy.SpottedEarly) Economy.AwardScouting();
        }

        /// <summary>
        /// Traps currently strike the lead wagon.
        ///
        /// Placeholder: the design has them hitting the troop walking point, with the
        /// shieldbearer soaking the blow (docs/GDD.md §7.2). Troops are not simulated
        /// yet, so the damage lands on the caravan instead — which makes traps rather
        /// more punishing than they will be.
        /// </summary>
        void ApplyTrapDamage()
        {
            foreach (var trap in Traps.TriggeredThisTick)
            {
                float damage = TrapTable.Damage(trap.Kind);
                foreach (var wagon in Caravan.Wagons)
                {
                    if (wagon.Destroyed) continue;
                    wagon.ApplyDamage(damage);
                    break;
                }
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
                return allHealthy && ElapsedSeconds <= ParSeconds ? 3 : 2;
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
    }
}
