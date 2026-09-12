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
        BanditArcher = 2,

        /// <summary>
        /// Raiders on horseback: fewer, faster, and on the caravan before the escort has
        /// finished turning round.
        /// </summary>
        BanditRider = 3,

        /// <summary>
        /// The man the band follows. One figure, worth four of them, and the reason a
        /// band is somewhere rather than wandering.
        /// </summary>
        BanditLeader = 4
    }

    /// <summary>
    /// Stats per enemy group. Enemies are placed and fought as groups, matching how
    /// the player's own troops work — one wolf is not a threat, a pack is.
    /// </summary>
    public static class EnemyTable
    {
        // Indexed by (int)EnemyKind.
        //
        // The riders and the leader, and what each is for. A band of raiders on foot is
        // met: it waits in cover and the escort forms up. Horsemen are three, hit harder
        // than four men on foot and cross the ground at nearly twice their pace, so what
        // they cost the player is the time to react — which is what cavalry is. The leader
        // is one man with the health of a small band, to be fought rather than swept
        // aside; he is worth killing (40 silver against a raider's 6) because a band
        // without its captain is what the road is for.
        //
        // <b>Their reach and their pace are the balance, not their health.</b> They were
        // first drawn at 4 m/s and waking at 26 m, on the argument that a horseman should
        // be on you before you are ready. Measured, that made chapter 2-5 a level with no
        // road that could be fought through at all: a group that wakes ten metres further
        // out drags neighbouring groups into the same fight, and the survivability gate
        // counts the groups a route meets rather than the ones that end up in the battle.
        // At 3.4 and 20 they are still the quickest thing on the road and the first to
        // notice the caravan, and every road can be fought down again.
        static readonly int[] _groupSize = { 5, 4, 3, 3, 1 };
        static readonly float[] _hpPerModel = { 60f, 100f, 60f, 130f, 280f };
        static readonly float[] _dps = { 14f, 20f, 18f, 26f, 28f };
        static readonly float[] _speed = { 3.5f, 2.0f, 1.8f, 3.4f, 2.2f };
        static readonly float[] _attackRange = { 2.0f, 2.0f, 18f, 2.2f, 2.2f };

        /// <summary>Range at which the group wakes and attacks the caravan.</summary>
        static readonly float[] _detectRadius = { 20f, 16f, 22f, 20f, 18f };

        /// <summary>
        /// Threat cost against the level's enemy budget. Not derived from stats: a
        /// wolf pack and an archer band can be equally costly to fight while having
        /// very different numbers, and the budget is about difficulty, not arithmetic.
        /// </summary>
        // The horsemen and the captain cost more than their health and damage alone would
        // suggest, and that is deliberate: points are what the placer spends and what the
        // survivability gate measures, so a group worth more trouble than its price says
        // gets waved through and then kills the caravan. Measured, three times: at twelve
        // and fourteen chapter 2-6 had no road that could be fought through, at sixteen and
        // twenty it was 2-9, and at fourteen and twenty-four it was 2-10 — the captain
        // kept being priced low enough that the placer could stand a band beside him. At
        // thirty he arrives nearly alone, which is what a man worth thirty points means.
        static readonly int[] _points = { 5, 8, 7, 14, 30 };

        static readonly int[] _silverPerKill = { 3, 6, 5, 9, 40 };

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

        /// <summary>
        /// Whether this one came for the cargo rather than for whoever is in the way.
        ///
        /// The men did and the wolves did not, and that was one comparison against
        /// <c>EnemyKind.Bandit</c> in the fighting — so the horsemen and their captain,
        /// who are on the road for exactly the same reason, went for the nearest cart
        /// instead of the treasure.
        /// </summary>
        public static bool AfterTreasure(EnemyKind k)
            => k == EnemyKind.Bandit || k == EnemyKind.BanditRider || k == EnemyKind.BanditLeader;

        /// <summary>Whether this one is on a horse, which is what a spear is for.</summary>
        public static bool IsMounted(EnemyKind k) => k == EnemyKind.BanditRider;

        public static readonly EnemyKind[] All =
        {
            EnemyKind.Wolf, EnemyKind.Bandit, EnemyKind.BanditArcher,
            EnemyKind.BanditRider, EnemyKind.BanditLeader
        };

        /// <summary>
        /// What a level fields when nobody has said otherwise: the three that were the
        /// whole road for as long as there was one chapter.
        ///
        /// <b>Not <see cref="All"/>, and that is the point.</b> A bare
        /// <c>new LevelRecipe()</c> is what a test, a tool or a scene opened on its own
        /// gets, and it used to mean "everything there is" because everything there was
        /// belonged in chapter one. The moment horsemen and a captain were appended, every
        /// such level started fielding them — which moved the placer's promises and the
        /// route choice on chapter 1-4, measured, and read as the raiders breaking the
        /// generator rather than as a default quietly meaning something new.
        /// </summary>
        public static readonly EnemyKind[] Common =
        {
            EnemyKind.Wolf, EnemyKind.Bandit, EnemyKind.BanditArcher
        };
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
