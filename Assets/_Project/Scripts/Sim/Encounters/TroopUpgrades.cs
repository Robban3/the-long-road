using System;

namespace TheVeil.Sim
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

        // UsableRange lived here: reach clamped to the squad's sight radius, on the
        // argument that nothing can be shot before it has been revealed and that buying
        // range should therefore not replace buying the scout.
        //
        // The argument still holds and this was the wrong way to hold it. Revelation is
        // already required — CombatSystem.NearestEnemyInReach skips anything not Revealed
        // — so the clamp was a second lock on a locked door, and it turned the range
        // track into a lie for anyone without a scout: five levels bought on an archer
        // capped at her own eighteen metres of sight changed nothing at all, and the
        // reach ring drawn on the ground sat there refusing to grow while the silver
        // went out of the purse. Reach is now CombatSystem.Reach and sight decides only
        // what may be shot at, which is what it was always meant to decide.

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
