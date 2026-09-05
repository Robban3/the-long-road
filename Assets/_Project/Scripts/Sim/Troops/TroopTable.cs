namespace TheVeil.Sim
{
    /// <summary>
    /// The nine troop types (docs/GDD.md §4.3). Values are persisted in save data, so
    /// entries may be appended but never renumbered.
    /// </summary>
    public enum TroopKind : byte
    {
        Spearmen = 0,
        Swordsmen = 1,
        Archers = 2,
        Cavalry = 3,
        Mage = 4,
        Scout = 5,
        Shieldbearer = 6,
        Priest = 7,
        Engineer = 8
    }

    /// <summary>
    /// The six posts around the caravan (docs/GDD.md §4.2), clockwise from the front.
    /// The van and the rear take most of the damage; the flanks are where reach earns
    /// its keep.
    /// </summary>
    /// <summary>
    /// Where a troop stands. Six posts round the column, and one out in front.
    ///
    /// <see cref="Scouting"/> is not one of the six and is deliberately outside them.
    /// The scout does not stand in the formation any more — she walks ahead of the van
    /// wherever she is placed — so a post in the line was a licence to exist rather than
    /// a position, and it made her compete for a corner with a shieldbearer who would
    /// actually have stood in it. It is hers alone: nothing else may be put there, and
    /// she may not be put anywhere else.
    /// </summary>
    public enum FormationSlot : byte
    {
        Van = 0,
        RightVan = 1,
        RightRear = 2,
        Rear = 3,
        LeftRear = 4,
        LeftVan = 5,
        Scouting = 6
    }

    /// <summary>
    /// Troop statistics.
    ///
    /// The governing principle is that no troop is best everywhere. Cavalry rules the
    /// open plain and is nearly useless in a fen; archers reach across a field and are
    /// blind among trees; the scout barely fights at all but makes everyone else
    /// effective. That is what forces the army choice and the route choice to be made
    /// together rather than one after the other.
    /// </summary>
    public static class TroopTable
    {
        // Indexed by (int)TroopKind.
        static readonly int[] _cost = { 3, 3, 4, 5, 6, 2, 4, 5, 4 };
        static readonly int[] _models = { 4, 4, 3, 3, 1, 2, 3, 1, 2 };
        static readonly float[] _hpPerModel = { 120f, 150f, 70f, 180f, 90f, 60f, 220f, 80f, 90f };
        static readonly float[] _dps = { 18f, 26f, 22f, 34f, 40f, 10f, 12f, 0f, 8f };
        static readonly float[] _range = { 2.5f, 1.8f, 22f, 2.2f, 18f, 12f, 1.8f, 12f, 8f };
        static readonly float[] _sight = { 12f, 12f, 18f, 16f, 14f, 34f, 12f, 12f, 14f };

        /// <summary>Fraction of incoming damage ignored. The shieldbearer's whole purpose.</summary>
        static readonly float[] _damageReduction = { 0f, 0f, 0f, 0f, 0f, 0f, 0.40f, 0f, 0f };

        /// <summary>Healing per second applied to the most wounded troop in reach.</summary>
        static readonly float[] _healPerSecond = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 15f, 0f };

        /// <summary>Extra range at which this troop notices traps.</summary>
        static readonly float[] _trapSight = { 0f, 0f, 0f, 0f, 0f, 10f, 0f, 0f, 8f };

        public static int Cost(TroopKind k) => _cost[(int)k];
        public static int Models(TroopKind k) => _models[(int)k];
        public static float HpPerModel(TroopKind k) => _hpPerModel[(int)k];
        public static float GroupHp(TroopKind k) => _hpPerModel[(int)k] * _models[(int)k];
        public static float Dps(TroopKind k) => _dps[(int)k];
        public static float Range(TroopKind k) => _range[(int)k];
        public static float Sight(TroopKind k) => _sight[(int)k];
        public static float DamageReduction(TroopKind k) => _damageReduction[(int)k];
        public static float HealPerSecond(TroopKind k) => _healPerSecond[(int)k];
        public static float TrapSight(TroopKind k) => _trapSight[(int)k];

        /// <summary>True for troops whose special upgrade buys reach, and is priced accordingly.</summary>
        public static bool HasRangedSpecial(TroopKind k) => k == TroopKind.Archers || k == TroopKind.Mage;

        public static bool CanDisarmTraps(TroopKind k) => k == TroopKind.Engineer;

        /// <summary>
        /// Troops that belong out in front rather than in the line, and which therefore
        /// take the scouting post instead of one of the six.
        /// </summary>
        public static bool Scouts(TroopKind k) => k == TroopKind.Scout;

        /// <summary>The six posts of the line, in the order they are unlocked.</summary>
        public static readonly FormationSlot[] Line =
        {
            // Front and back first: a small escort covers the ends of the column, and a
            // flank guard with nobody on point is a formation with a hole in the one
            // place everything arrives from.
            FormationSlot.Van, FormationSlot.Rear,
            FormationSlot.RightVan, FormationSlot.LeftVan,
            FormationSlot.RightRear, FormationSlot.LeftRear
        };

        /// <summary>How many posts the line has when every one of them is open.</summary>
        public const int LinePosts = 6;

        public static readonly TroopKind[] All =
        {
            TroopKind.Spearmen, TroopKind.Swordsmen, TroopKind.Archers, TroopKind.Cavalry,
            TroopKind.Mage, TroopKind.Scout, TroopKind.Shieldbearer, TroopKind.Priest,
            TroopKind.Engineer
        };

        /// <summary>
        /// Damage multiplier for fighting in a given terrain.
        ///
        /// Cavalry needs room to build a charge and gets neither in woodland nor in a
        /// bog; archers need a clear line and lose most of it among trees. These are
        /// the numbers that make a route through the forest a decision about which
        /// troops you brought, not merely a slower road.
        /// </summary>
        public static float TerrainDamageMultiplier(TroopKind kind, TerrainType terrain)
        {
            switch (kind)
            {
                case TroopKind.Cavalry:
                    if (terrain == TerrainType.Forest) return 0.5f;
                    if (terrain == TerrainType.Marsh) return 0.3f;
                    if (terrain == TerrainType.Plains) return 1.25f;
                    return 1f;

                case TroopKind.Archers:
                    if (terrain == TerrainType.Forest) return 0.7f;
                    return 1f;

                default:
                    return 1f;
            }
        }

        /// <summary>Reach multiplier for terrain. Dense cover shortens a bowshot badly.</summary>
        public static float TerrainRangeMultiplier(TroopKind kind, TerrainType terrain)
        {
            if (kind != TroopKind.Archers && kind != TroopKind.Mage) return 1f;
            return terrain == TerrainType.Forest ? 0.6f : 1f;
        }

        /// <summary>Anti-cavalry bonus. Spearmen exist to punish a charge.</summary>
        public static float DamageMultiplierAgainst(TroopKind kind, EnemyKind target)
        {
            if (kind == TroopKind.Spearmen && target == EnemyKind.Wolf) return 2f;
            return 1f;
        }
    }
}
