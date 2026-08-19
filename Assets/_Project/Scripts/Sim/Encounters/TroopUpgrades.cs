using System;

namespace Arna.Sim
{
    public enum UpgradeTrack : byte
    {
        Weapon = 0,
        Armour = 1,
        Special = 2
    }

    /// <summary>
    /// What each upgrade level buys, and what it costs (docs/GDD.md §6.4).
    /// </summary>
    public static class TroopUpgrades
    {
        public const float WeaponPerLevel = 0.18f;
        public const float ArmourHpPerLevel = 0.15f;
        public const float ArmourReductionPerLevel = 0.04f;

        /// <summary>
        /// Attack range costs more than any other upgrade.
        ///
        /// Range is the strongest statistic in a defence game: every metre is time the
        /// enemy spends being shot without shooting back. Priced like the other tracks
        /// it would be the only correct purchase for archers. At this multiplier a
        /// good run affords two levels, three if the player took the dangerous route
        /// and bought the marauder early — so it stays a real investment rather than a
        /// default.
        /// </summary>
        public const float RangeCostMultiplier = 1.6f;

        /// <summary>
        /// Diminishing gains, unlike the flat tracks. Compounding fifteen percent five
        /// times would double an archer's reach and let it clear every ambush before
        /// the group ever woke, which would quietly delete the fog of war.
        /// </summary>
        static readonly float[] RangeIncrements = { 0.15f, 0.11f, 0.08f, 0.05f, 0.03f };

        public static int MaxLevel => RunEconomy.MaxTrackLevel;

        public static float WeaponMultiplier(int level) => 1f + WeaponPerLevel * Clamp(level);
        public static float ArmourHpMultiplier(int level) => 1f + ArmourHpPerLevel * Clamp(level);
        public static float ArmourDamageReduction(int level) => ArmourReductionPerLevel * Clamp(level);

        /// <summary>Cumulative range multiplier at a given level. Roughly 1.49 at maximum.</summary>
        public static float RangeMultiplier(int level)
        {
            int capped = Clamp(level);
            float multiplier = 1f;
            for (int i = 0; i < capped; i++) multiplier *= 1f + RangeIncrements[i];
            return multiplier;
        }

        public static float EffectiveRange(float baseRange, int rangeLevel)
            => baseRange * RangeMultiplier(rangeLevel);

        /// <summary>
        /// Range a troop can actually use.
        ///
        /// Nothing can be shot before it has been revealed, so sight is a hard ceiling
        /// on reach. This is what stops range upgrades from replacing the scout: buy
        /// range without buying vision and the extra metres are simply wasted. The two
        /// investments are complements, and the information economy survives.
        /// </summary>
        public static float UsableRange(float attackRange, float sightRadius)
            => attackRange < sightRadius ? attackRange : sightRadius;

        /// <summary>Cost multiplier for a track on a given troop.</summary>
        public static float CostMultiplier(UpgradeTrack track, bool isRangedSpecial)
            => track == UpgradeTrack.Special && isRangedSpecial ? RangeCostMultiplier : 1f;

        static int Clamp(int level)
        {
            if (level < 0) return 0;
            return level > RunEconomy.MaxTrackLevel ? RunEconomy.MaxTrackLevel : level;
        }
    }
}
