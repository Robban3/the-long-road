namespace Arna.Sim
{
    /// <summary>What gold buys between levels, and keeps.</summary>
    public enum Boon : byte
    {
        /// <summary>Silver in the purse at the start of every run.</summary>
        Purse = 0,

        /// <summary>Points to spend on the escort.</summary>
        Muster = 1,

        /// <summary>Stouter carts.</summary>
        Wainwright = 2,

        /// <summary>Cheaper upgrades in the field.</summary>
        Smithy = 3,

        /// <summary>A post of the line opened ahead of the chapter's own curve.</summary>
        Outriders = 4
    }

    /// <summary>
    /// The shop's stock: five things gold can buy, each of them a number the fighting
    /// already reads.
    ///
    /// That constraint is the whole design of this table. Silver, squad points, wagon
    /// health, upgrade prices and the number of open posts are all levers the simulation
    /// has consumed since long before there was a shop — so every purchase here reaches
    /// the run through a road that is already built and already tested, rather than
    /// through a new special case that has to be threaded past the combat.
    ///
    /// Prices rise steeply. Gold comes in at forty to a hundred a level, so a first level
    /// of something is one or two runs and the last is a chapter — which is what makes
    /// the choice of *which* to buy a choice at all.
    /// </summary>
    public static class BoonTable
    {
        public static readonly Boon[] All =
        {
            Boon.Purse, Boon.Muster, Boon.Wainwright, Boon.Smithy, Boon.Outriders
        };

        // Indexed by (int)Boon.
        static readonly int[] _maxLevel = { 5, 4, 5, 4, 2 };
        static readonly int[] _firstPrice = { 120, 200, 150, 180, 400 };

        /// <summary>What each level of a boon costs on top of the one before it.</summary>
        const float PriceGrowth = 1.7f;

        public static int MaxLevel(Boon boon) => _maxLevel[(int)boon];

        /// <summary>
        /// Gold for the next level, or zero when there is no next level.
        ///
        /// Geometric rather than flat, at 1.7 a step. Flat pricing makes the last level
        /// of a five-step track the same decision as the first, and it is not: by then
        /// the player has four of them and is choosing between deepening something that
        /// works and opening something that does not exist yet.
        /// </summary>
        public static int Price(Boon boon, int owned)
        {
            if (owned < 0) owned = 0;
            if (owned >= MaxLevel(boon)) return 0;

            float price = _firstPrice[(int)boon];
            for (int i = 0; i < owned; i++) price *= PriceGrowth;

            // To the nearest ten, because a price of 583 gold reads as arithmetic and a
            // price of 580 reads as a price.
            return (int)(price / 10f + 0.5f) * 10;
        }

        /// <summary>Silver in the purse at the start of a run, per level of Purse.</summary>
        public const int SilverPerLevel = 40;

        /// <summary>Escort points per level of Muster.</summary>
        public const int PointsPerLevel = 1;

        /// <summary>Wagon health added per level of Wainwright, as a share.</summary>
        public const float HealthPerLevel = 0.06f;

        /// <summary>Field-upgrade discount per level of Smithy, as a share.</summary>
        public const float DiscountPerLevel = 0.05f;

        /// <summary>Posts of the line opened per level of Outriders.</summary>
        public const int PostsPerLevel = 1;
    }

    /// <summary>
    /// The boons a player owns, resolved into the numbers a run needs.
    ///
    /// A plain carrier rather than a lookup into the campaign, so the run never has to
    /// know that a campaign exists: the headless tests, the capture and a level opened
    /// straight from the editor all pass nothing and get the game as it has always been.
    /// </summary>
    public sealed class Boons
    {
        readonly int[] _levels = new int[BoonTable.All.Length];

        public int Level(Boon boon) => _levels[(int)boon];

        public void Set(Boon boon, int level)
        {
            int max = BoonTable.MaxLevel(boon);
            _levels[(int)boon] = level < 0 ? 0 : (level > max ? max : level);
        }

        public int StartingSilver => Level(Boon.Purse) * BoonTable.SilverPerLevel;
        public int ExtraSquadPoints => Level(Boon.Muster) * BoonTable.PointsPerLevel;
        public int ExtraPosts => Level(Boon.Outriders) * BoonTable.PostsPerLevel;

        /// <summary>Multiplier on wagon health. One when nothing has been bought.</summary>
        public float WagonHealth => 1f + Level(Boon.Wainwright) * BoonTable.HealthPerLevel;

        /// <summary>
        /// Multiplier on what a field upgrade costs. Floored well above zero: a track the
        /// player can fill for nothing is not an economy.
        /// </summary>
        public float UpgradeCost
        {
            get
            {
                float cost = 1f - Level(Boon.Smithy) * BoonTable.DiscountPerLevel;
                return cost < 0.5f ? 0.5f : cost;
            }
        }

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
