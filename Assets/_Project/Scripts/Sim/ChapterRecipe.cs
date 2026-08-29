using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>
    /// One chapter's difficulty curve. Produces a <see cref="LevelRecipe"/> for each
    /// of its levels by interpolating between a start and an end value
    /// (docs/content-pipeline.md §2).
    ///
    /// Balancing a chapter is therefore editing about a dozen numbers, not building
    /// ten levels by hand. That is the only way a hundred chapters is tractable.
    /// </summary>
    public sealed class ChapterRecipe
    {
        public int LevelsPerChapter = 10;

        /// <summary>
        /// Enemy count across the chapter. Kept inside the measured 100–140 band:
        /// below it the silver floor flattens every route's reward, above it the short
        /// fast corridor saturates and the long way round becomes the richer one.
        /// See LevelRecipe.EnemyBudget.
        /// </summary>
        public int EnemyBudgetStart = 100;
        public int EnemyBudgetEnd = 140;

        /// <summary>
        /// Multiplier on enemy health and damage.
        ///
        /// This is the lever that carries difficulty past the point where adding more
        /// enemies stops working. Later chapters get harder by fielding tougher
        /// enemies, not more of them — the map simply has nowhere to put more.
        /// </summary>
        public float EnemyStrengthStart = 1.00f;
        public float EnemyStrengthEnd = 1.35f;

        public float TrapDensityStart = 0.5f;
        public float TrapDensityEnd = 1.4f;

        public int RouteTilesStart = 55;
        public int RouteTilesEnd = 95;

        /// <summary>
        /// The player's own budget grows too, but deliberately more slowly than the
        /// threat. Attrition across the chapter compounds this, so the curve must not
        /// be steep or the last levels become impossible with a worn-down army.
        /// </summary>
        public int SquadBudgetStart = 12;
        public int SquadBudgetEnd = 18;

        /// <summary>
        /// Scales enemy silver drops for the whole chapter. Upgrade costs are fixed,
        /// so without this later chapters would feel poor rather than hard.
        /// </summary>
        public float SilverMultiplier = 1f;

        /// <summary>
        /// The level at which each enemy type starts appearing, indexed by
        /// (int)EnemyKind. Introducing types one at a time is a difficulty lever in
        /// itself: the archer is not merely stronger, it is a problem that melee
        /// cannot answer at all.
        /// </summary>
        public int[] EnemyUnlockLevel = { 1, 2, 4 };

        public TerrainShare[] TerrainMix;
        public int Rivers = 1;
        public int FordsPerRiver = 3;
        public float NoiseScale = 18f;

        /// <summary>Builds the recipe for one level, 1-based.</summary>
        public LevelRecipe ForLevel(int level)
        {
            int span = LevelsPerChapter > 1 ? LevelsPerChapter - 1 : 1;
            int clamped = level < 1 ? 1 : (level > LevelsPerChapter ? LevelsPerChapter : level);
            float t = (clamped - 1) / (float)span;

            var recipe = new LevelRecipe
            {
                EnemyBudget = Lerp(EnemyBudgetStart, EnemyBudgetEnd, t),
                EnemyStrength = EnemyStrengthStart + (EnemyStrengthEnd - EnemyStrengthStart) * t,
                TrapDensity = TrapDensityStart + (TrapDensityEnd - TrapDensityStart) * t,
                MinRouteTiles = Lerp(RouteTilesStart, RouteTilesEnd, t),
                SquadBudget = Lerp(SquadBudgetStart, SquadBudgetEnd, t),
                SilverMultiplier = SilverMultiplier,
                Rivers = Rivers,
                FordsPerRiver = FordsPerRiver,
                NoiseScale = NoiseScale,
                EnemyPool = PoolForLevel(clamped),

                // Levels 6 to 9 are the escalation band and owe one way through; every
                // other level owes two. See LevelRecipe.RoutesOwed.
                RoutesOwed = clamped >= 6 && clamped <= 9 ? 1 : 2
            };

            if (TerrainMix != null && TerrainMix.Length > 0) recipe.TerrainMix = TerrainMix;
            return recipe;
        }

        public EnemyKind[] PoolForLevel(int level)
        {
            var pool = new List<EnemyKind>();
            foreach (var kind in EnemyTable.All)
            {
                int index = (int)kind;
                int unlock = index < EnemyUnlockLevel.Length ? EnemyUnlockLevel[index] : 1;
                if (level >= unlock) pool.Add(kind);
            }

            // A level with nothing in it is worse than one that is slightly too easy.
            if (pool.Count == 0) pool.Add(EnemyKind.Wolf);
            return pool.ToArray();
        }

        static int Lerp(int from, int to, float t) => (int)(from + (to - from) * t + 0.5f);
    }
}
