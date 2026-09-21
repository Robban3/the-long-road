namespace TheVeil.Sim
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

        /// <summary>
        /// What each step adds to the price of the next.
        ///
        /// <b>So that a troop is finished across the campaign, not across a chapter.</b>
        /// At six per cent a player who put every gold piece into one troop had it
        /// finished in sixty levels - six chapters of a thousand-level game, and then
        /// nothing left on it to want. The rule is that finishing a troop takes at least
        /// nine hundred levels even for a player who buys nothing else.
        ///
        /// Twenty-two per cent does it, worked out with the game's own rounding rather
        /// than guessed: a track comes to about 53 000 gold, a troop with two tracks to
        /// 965 levels of <see cref="ReferenceSquad.GoldPerLevel"/>, and one with three -
        /// the bow, the scout - to about 1 450. The first steps barely move: 30, 35, 45,
        /// 55, 65 against 30, 35, 40, 45, 50. The early game is the game it was; what
        /// changed is that the end of a track is far away, and the last step costs close
        /// to ninety levels' gold on its own.
        /// </summary>
        public const float PriceGrowth = 1.22f;
        public const float Falloff = BoonTable.Falloff;

        /// <summary>
        /// Cheaper to start than the general boons, because there are twenty of these
        /// tracks and nobody is going to fill them all: a full one costs about 53 000
        /// gold (see <see cref="PriceGrowth"/>), and finishing even one troop is the work
        /// of most of a campaign. What that buys is a reason to deepen the three or four
        /// troops you actually field, which ties the shop to the formation rather than to
        /// a shopping list.
        /// </summary>
        public const int BasePrice = 30;

        /// <summary>
        /// What a finished track is worth, reached in thirty even steps.
        ///
        /// <b>The meta layer's ceilings, and they stay low on purpose.</b> docs/economy.md
        /// §3: the combat layer has to dominate the meta layer, or a player in chapter
        /// forty drives over chapter five without making a decision and a thousand levels
        /// collapse into a grind. Gold raises the floor; the silver spent in the level
        /// decides it. These were raised once, to +150 % damage, to widen the gap between
        /// a player who invests and one who does not - and that turned the rule upside
        /// down, so the gap is found in the field layer instead (TroopUpgrades), which is
        /// where the design puts it.
        /// </summary>
        public const float WeaponCap = 0.60f;
        public const float ArmourHealthCap = 0.50f;
        public const float ArmourReductionCap = 0.15f;
        public const float RangeCap = 0.25f;

        /// <summary>
        /// Metres of sight the scout's special track is worth when finished: 34 to about 54
        /// at thirty steps. Metres rather than a share because sight is what the scout is,
        /// and "20 m further" is the sentence a player can weigh against its price.
        /// </summary>
        public const float SightCap = 22f;

        public static readonly UpgradeTrack[] Tracks =
        {
            UpgradeTrack.Weapon, UpgradeTrack.Armour, UpgradeTrack.Special
        };

        /// <summary>
        /// Whether this track does anything for this troop, and so may be sold. The special
        /// track is reach for a bow or a staff, sight for the scout, and nothing for
        /// anybody else.
        /// </summary>
        public static bool Sells(TroopKind kind, UpgradeTrack track)
        {
            if (track != UpgradeTrack.Special) return true;

            return TroopTable.HasRangedSpecial(kind) || TroopTable.Scouts(kind);
        }

        public static int MaxLevel(TroopKind kind, UpgradeTrack track)
            => Sells(kind, track) ? Steps : 0;

        public static int Price(TroopKind kind, UpgradeTrack track, int owned)
        {
            if (!Sells(kind, track) || owned < 0) return 0;
            if (owned >= Steps) return 0;

            return BoonTable.Rounded(BasePrice, PriceGrowth, owned);
        }

        /// <summary>
        /// How far along the track this many steps is, from nothing to one: evenly, a
        /// thirtieth a step.
        ///
        /// <b>Not front-loaded any more, and that is the point of it.</b> This used the
        /// general boons' curve, where each step closes 7.5 % of what is left - so the
        /// first three steps, which one won level pays for, were a fifth of everything the
        /// track would ever give, and every step after cost more and gave less. A player
        /// was most of the way to finished on a troop almost as soon as they started, and
        /// the rest of the track was a long walk for very little. It should be the other
        /// way round: hard to get far, and every step worth the same as the last.
        ///
        /// The first step costs thirty gold and each costs twenty-two per cent more than the
        /// last (see <see cref="PriceGrowth"/>), so one level buys the first three or four
        /// steps of a track and every step after is further away than the one before.
        /// Finishing a troop is a campaign's work, not a level's.
        /// </summary>
        public static float Share(int level)
        {
            if (level <= 0) return 0f;
            if (level > Steps) level = Steps;

            return level / (float)Steps;
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

        /// <summary>Metres of sight bought for the scout. Nought for anybody else.</summary>
        public float Sight(TroopKind kind)
            => TroopTable.Scouts(kind) ? TroopBoonTable.SightCap * Share(kind, UpgradeTrack.Special) : 0f;

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
