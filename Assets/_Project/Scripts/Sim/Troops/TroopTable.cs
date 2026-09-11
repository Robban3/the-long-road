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
        Engineer = 8,

        // Appended, never reordered: the save keys permanent troop levels by this number.

        /// <summary>Bows' harder-hitting, dearer cousins. Opened by levels cleared.</summary>
        Crossbowmen = 9,

        /// <summary>
        /// The three mounted tiers above plain cavalry, each dearer, tougher and harder
        /// hitting than the last, and each opened by levels cleared. All of them charge
        /// like cavalry — see <see cref="TroopTable.IsMounted"/>.
        /// </summary>
        HeavyCavalry = 10,
        NobleCavalry = 11,
        Knights = 12
    }

    /// <summary>
    /// The six posts around the caravan (docs/GDD.md §4.2), clockwise from the front.
    /// The van and the rear take most of the damage; the flanks are where reach earns
    /// its keep.
    ///
    /// The scout takes one of them like anybody else. She had a seventh post of her own
    /// for a while, out in front, and that made her free: a troop that costs nothing but
    /// two points is a troop everybody brings. Taking a place in the line is her price —
    /// the corner she holds is a corner nobody with a sword is holding. She still walks
    /// ahead of the van whichever post she is given (see Squad.PostFor).
    /// </summary>
    public enum FormationSlot : byte
    {
        Van = 0,
        RightVan = 1,
        RightRear = 2,
        Rear = 3,
        LeftRear = 4,
        LeftVan = 5
    }

    /// <summary>
    /// Troop statistics.
    ///
    /// The governing principle is that no troop is best everywhere. Cavalry rules the
    /// open plain and is nearly useless in a fen; archers reach across a field and are
    /// blind among trees; the scout does not fight at all but makes everyone else
    /// effective. That is what forces the army choice and the route choice to be made
    /// together rather than one after the other.
    ///
    /// <b>The scout's damage and reach are nought.</b> What she is for is seeing trouble
    /// before it stirs, and that is all she does: a scout with a blade was a weak
    /// swordsman with good eyes, and got used as one. With no reach she draws no ring on
    /// the ground either. She can still be struck, so her armour track still matters.
    /// </summary>
    public static class TroopTable
    {
        // Indexed by (int)TroopKind.
        //                                 spear sword bow  cav  mage scout shld prst eng  xbow heavy noble knight
        static readonly int[] _cost =        { 3,   3,   4,   5,   6,   2,    4,   5,   4,   5,   7,    8,    10 };
        static readonly int[] _models =      { 4,   4,   3,   3,   1,   2,    3,   1,   2,   3,   3,    3,    3 };
        static readonly float[] _hpPerModel = { 120f, 150f, 70f, 180f, 90f, 60f, 220f, 80f, 90f, 80f, 240f, 260f, 320f };
        static readonly float[] _dps =        { 18f, 26f, 22f, 34f, 40f, 0f, 12f, 0f, 8f, 32f, 44f, 52f, 60f };
        static readonly float[] _range =      { 2.5f, 1.8f, 22f, 2.2f, 18f, 0f, 1.8f, 12f, 8f, 20f, 2.2f, 2.2f, 2.4f };
        static readonly float[] _sight =      { 12f, 12f, 18f, 16f, 14f, 34f, 12f, 12f, 14f, 16f, 16f, 16f, 16f };

        /// <summary>
        /// Fraction of incoming damage ignored. The shieldbearer's whole purpose — and the
        /// better half of what a dearer horse buys: mail, then plate, then barding.
        /// </summary>
        static readonly float[] _damageReduction = { 0f, 0f, 0f, 0f, 0f, 0f, 0.40f, 0f, 0f, 0f, 0.15f, 0.20f, 0.25f };

        /// <summary>Healing per second applied to the most wounded troop in reach.</summary>
        static readonly float[] _healPerSecond = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 15f, 0f, 0f, 0f, 0f, 0f };

        /// <summary>Extra range at which this troop notices traps.</summary>
        static readonly float[] _trapSight = { 0f, 0f, 0f, 0f, 0f, 10f, 0f, 0f, 8f, 0f, 0f, 0f, 0f };

        /// <summary>
        /// Levels cleared before a troop may be brought, anywhere in the campaign.
        ///
        /// The newer troops are earned rather than handed out: the crossbow once the
        /// player has some road behind them, and each heavier horse a chapter's worth of
        /// levels after the last. Counted by levels cleared rather than stars, so going
        /// back for a third star is its own reward and not a toll gate.
        /// </summary>
        static readonly int[] _levelsToUnlock = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 10, 15, 20 };

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
        public static int LevelsToUnlock(TroopKind k) => _levelsToUnlock[(int)k];

        /// <summary>True for troops whose special upgrade buys reach, and is priced accordingly.</summary>
        public static bool HasRangedSpecial(TroopKind k)
            => k == TroopKind.Archers || k == TroopKind.Mage || k == TroopKind.Crossbowmen;

        /// <summary>
        /// Troops on horseback. They share the charge and its limits — strong on open
        /// ground, bogged down in a fen, blind to a charge among trees — and the rider's
        /// height; what separates the tiers is the price and what it buys.
        /// </summary>
        public static bool IsMounted(TroopKind k)
            => k == TroopKind.Cavalry || k == TroopKind.HeavyCavalry
               || k == TroopKind.NobleCavalry || k == TroopKind.Knights;

        public static bool CanDisarmTraps(TroopKind k) => k == TroopKind.Engineer;

        /// <summary>
        /// Troops that walk out in front of the column rather than at their post, and of
        /// which an escort may bring only one. Bought once in the shop (Boon.Scout).
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
            TroopKind.Engineer, TroopKind.Crossbowmen,
            TroopKind.HeavyCavalry, TroopKind.NobleCavalry, TroopKind.Knights
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
            // Every horse, whatever it cost: a destrier in plate gets no more room to
            // charge among trees than a hobbler does.
            if (IsMounted(kind))
            {
                if (terrain == TerrainType.Forest) return 0.5f;
                if (terrain == TerrainType.Marsh) return 0.3f;
                if (terrain == TerrainType.Plains) return 1.25f;
                return 1f;
            }

            // A crossbow needs the same clear line a bow does.
            if (kind == TroopKind.Archers || kind == TroopKind.Crossbowmen)
                return terrain == TerrainType.Forest ? 0.7f : 1f;

            return 1f;
        }

        /// <summary>Reach multiplier for terrain. Dense cover shortens a bowshot badly.</summary>
        public static float TerrainRangeMultiplier(TroopKind kind, TerrainType terrain)
        {
            if (kind != TroopKind.Archers && kind != TroopKind.Mage && kind != TroopKind.Crossbowmen)
                return 1f;

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
