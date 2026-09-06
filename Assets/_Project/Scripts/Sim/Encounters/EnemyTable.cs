namespace TheVeil.Sim
{
    /// <summary>
    /// Enemy types for the forest chapter (docs/GDD.md §7.1). Values are persisted in
    /// save data and analytics, so entries may be appended but never renumbered.
    /// </summary>
    public enum EnemyKind : byte
    {
        Wolf = 0,
        Bandit = 1,
        BanditArcher = 2
    }

    /// <summary>
    /// Stats per enemy group. Enemies are placed and fought as groups, matching how
    /// the player's own troops work — one wolf is not a threat, a pack is.
    /// </summary>
    public static class EnemyTable
    {
        // Indexed by (int)EnemyKind.
        static readonly int[] _groupSize = { 5, 4, 3 };
        static readonly float[] _hpPerModel = { 60f, 100f, 60f };
        static readonly float[] _dps = { 14f, 20f, 18f };
        static readonly float[] _speed = { 3.5f, 2.0f, 1.8f };
        static readonly float[] _attackRange = { 2.0f, 2.0f, 18f };

        /// <summary>Range at which the group wakes and attacks the caravan.</summary>
        static readonly float[] _detectRadius = { 20f, 16f, 22f };

        /// <summary>
        /// Threat cost against the level's enemy budget. Not derived from stats: a
        /// wolf pack and an archer band can be equally costly to fight while having
        /// very different numbers, and the budget is about difficulty, not arithmetic.
        /// </summary>
        static readonly int[] _points = { 5, 8, 7 };

        static readonly int[] _silverPerKill = { 3, 6, 5 };

        public static int GroupSize(EnemyKind k) => _groupSize[(int)k];
        public static float HpPerModel(EnemyKind k) => _hpPerModel[(int)k];
        public static float GroupHp(EnemyKind k) => _hpPerModel[(int)k] * _groupSize[(int)k];
        public static float Dps(EnemyKind k) => _dps[(int)k];
        public static float Speed(EnemyKind k) => _speed[(int)k];
        public static float AttackRange(EnemyKind k) => _attackRange[(int)k];
        public static float DetectRadius(EnemyKind k) => _detectRadius[(int)k];
        public static int Points(EnemyKind k) => _points[(int)k];
        public static int SilverPerKill(EnemyKind k) => _silverPerKill[(int)k];

        /// <summary>Silver a fully destroyed group yields.</summary>
        public static int GroupSilver(EnemyKind k) => _silverPerKill[(int)k] * _groupSize[(int)k];

        public static readonly EnemyKind[] All = { EnemyKind.Wolf, EnemyKind.Bandit, EnemyKind.BanditArcher };
    }

    public enum TrapKind : byte
    {
        Pit = 0,
        Log = 1
    }

    /// <summary>
    /// Traps are the other half of the route trade-off: a stretch thick with traps
    /// carries fewer enemies, so the marsh is survivable for a weak army that brings
    /// an engineer (docs/GDD.md §7.2).
    /// </summary>
    public static class TrapTable
    {
        static readonly float[] _damage = { 80f, 120f };
        static readonly float[] _revealRadius = { 6f, 8f };
        static readonly int[] _points = { 2, 3 };
        static readonly int[] _disarmSilver = { 8, 8 };

        public static float Damage(TrapKind k) => _damage[(int)k];

        /// <summary>How close the caravan must come before the trap becomes visible.</summary>
        public static float RevealRadius(TrapKind k) => _revealRadius[(int)k];

        public static int Points(TrapKind k) => _points[(int)k];

        /// <summary>Silver for disarming rather than triggering — the engineer's income.</summary>
        public static int DisarmSilver(TrapKind k) => _disarmSilver[(int)k];

        public static readonly TrapKind[] All = { TrapKind.Pit, TrapKind.Log };
    }
}
