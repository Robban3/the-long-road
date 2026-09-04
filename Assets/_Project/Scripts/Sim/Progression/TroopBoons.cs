namespace TheVail.Sim
{
    /// <summary>
    /// Permanent levels bought on a troop type with gold, kept between runs.
    ///
    /// These sit <b>beside</b> the field upgrades rather than replacing them. The silver
    /// tracks in the field keep their cap of five and their own multipliers; this is a
    /// second, gentler multiplier on top. Folding the two into one number was the
    /// obvious design and is wrong twice over: it would let gold fill a track the silver
    /// economy exists to make you fight for, and the field multipliers are flat
    /// (+18% damage a level), so twenty levels of anything would be four times the
    /// damage and the end of the game.
    ///
    /// Deliberately not sold for every combination. <see cref="TroopGroup.AttackRange"/>
    /// reads the special level only when <see cref="TroopTable.HasRangedSpecial"/> is
    /// true, so for a swordsman the special track does nothing at all — in the field as
    /// well as here. Selling it would be selling nothing. (That the *field* track has the
    /// same hole for melee troops is a separate finding and not this change's job.)
    /// </summary>
    public static class TroopBoonTable
    {
        public const int Steps = 30;
        public const float PriceGrowth = 1.06f;
        public const float Falloff = BoonTable.Falloff;

        /// <summary>
        /// Cheaper a step than the general boons, because there are twenty of these
        /// tracks and nobody is going to fill them all: a full one costs about 2 400
        /// gold, and twenty of them cost several campaigns. What that buys is a reason to
        /// deepen the three or four troops you actually field, which ties the shop to the
        /// formation rather than to a shopping list.
        /// </summary>
        public const int BasePrice = 30;

        /// <summary>Caps, reached to 90% at thirty steps.</summary>
        public const float WeaponCap = 0.35f;
        public const float ArmourHealthCap = 0.30f;
        public const float ArmourReductionCap = 0.08f;
        public const float RangeCap = 0.25f;

        public static readonly UpgradeTrack[] Tracks =
        {
            UpgradeTrack.Weapon, UpgradeTrack.Armour, UpgradeTrack.Special
        };

        /// <summary>Whether this track does anything for this troop, and so may be sold.</summary>
        public static bool Sells(TroopKind kind, UpgradeTrack track)
        {
            if (track != UpgradeTrack.Special) return true;

            return TroopTable.HasRangedSpecial(kind);
        }

        public static int MaxLevel(TroopKind kind, UpgradeTrack track)
            => Sells(kind, track) ? Steps : 0;

        public static int Price(TroopKind kind, UpgradeTrack track, int owned)
        {
            if (!Sells(kind, track) || owned < 0) return 0;
            if (owned >= Steps) return 0;

            return BoonTable.Rounded(BasePrice, PriceGrowth, owned);
        }

        /// <summary>How far along the curve this many steps is, from nothing to one.</summary>
        public static float Share(int level)
        {
            if (level <= 0) return 0f;
            if (level > Steps) level = Steps;

            float remaining = 1f;
            for (int i = 0; i < level; i++) remaining *= Falloff;

            return 1f - remaining;
        }
    }

    /// <summary>
    /// What a player has bought on each troop type, resolved into multipliers.
    ///
    /// Null everywhere except the running game, like <see cref="Boons"/>: a squad built
    /// by a test or by the headless capture fights with the table's own numbers.
    /// </summary>
    public sealed class TroopBoons
    {
        const int Tracks = 3;

        readonly int[] _levels = new int[TroopTable.All.Length * Tracks];

        static int Key(TroopKind kind, UpgradeTrack track) => (int)kind * Tracks + (int)track;

        public int Level(TroopKind kind, UpgradeTrack track)
        {
            int key = Key(kind, track);
            return key >= 0 && key < _levels.Length ? _levels[key] : 0;
        }

        public void Set(TroopKind kind, UpgradeTrack track, int level)
        {
            int key = Key(kind, track);
            if (key < 0 || key >= _levels.Length) return;

            if (!TroopBoonTable.Sells(kind, track)) return;

            _levels[key] = level < 0 ? 0 : (level > TroopBoonTable.Steps ? TroopBoonTable.Steps : level);
        }

        float Share(TroopKind kind, UpgradeTrack track)
            => TroopBoonTable.Share(Level(kind, track));

        public float Weapon(TroopKind kind)
            => 1f + TroopBoonTable.WeaponCap * Share(kind, UpgradeTrack.Weapon);

        public float ArmourHealth(TroopKind kind)
            => 1f + TroopBoonTable.ArmourHealthCap * Share(kind, UpgradeTrack.Armour);

        public float ArmourReduction(TroopKind kind)
            => TroopBoonTable.ArmourReductionCap * Share(kind, UpgradeTrack.Armour);

        public float Range(TroopKind kind)
            => 1f + TroopBoonTable.RangeCap * Share(kind, UpgradeTrack.Special);

        public bool Any
        {
            get
            {
                foreach (int level in _levels) if (level > 0) return true;
                return false;
            }
        }
    }
}
