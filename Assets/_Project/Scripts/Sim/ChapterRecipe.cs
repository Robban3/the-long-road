using System.Collections.Generic;

namespace TheVeil.Sim
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
        // Left where it was, and the attempt to raise it is worth recording. Charging the
        // cautious corridor for the fast one's tiles spreads the same budget over three
        // separate roads instead of two-and-a-bit, so a fifth was added to both ends to
        // pay for it — and it broke two things at once: level 6 came out at 147 against
        // the saturation ceiling of 145 (see ChapterProgressionTests), and chapter 1 fell
        // to 19 survivable routes of 30 where it owes 20, with 1-10 down to one way
        // through where it owes two.
        //
        // The curve was already enough. What needed the twenty was LevelRecipe's plain
        // default, which is flat and is what a test or a standalone recipe gets.
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
        /// Posts of the line, opening across the chapter.
        ///
        /// Three to six. The formation had all six from the first level and the player
        /// could never fill them: the cheapest six troops in the game cost twenty points
        /// and the budget above runs from twelve to eighteen, so a full line was not hard
        /// to afford but arithmetically impossible. Six sockets of which four can ever be
        /// used reads as a bug; three that become six as the chapter goes on reads as
        /// getting somewhere.
        ///
        /// Paced against the budget rather than against the level number: three posts at
        /// twelve points, six at eighteen, which is about four points a post either end.
        /// </summary>
        public int PostsStart = 3;
        public int PostsEnd = TroopTable.LinePosts;

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

        /// <summary>How strong the escort is assumed to be. See LevelRecipe.EscortStrength.</summary>
        public float EscortStrength = 1f;

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
                EscortStrength = EscortStrength,
                TrapDensity = TrapDensityStart + (TrapDensityEnd - TrapDensityStart) * t,
                MinRouteTiles = Lerp(RouteTilesStart, RouteTilesEnd, t),
                SquadBudget = Lerp(SquadBudgetStart, SquadBudgetEnd, t),
                Posts = Lerp(PostsStart, PostsEnd, t),
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

        /// <summary>What chapter one's enemies gain in strength across its ten levels.</summary>
        public const float StrengthPerChapter = 0.35f;

        /// <summary>
        /// Chapters over which the climb bends, past chapter two. See <see cref="StrengthAtEndOf"/>.
        /// </summary>
        public const float Knee = 3f;

        /// <summary>
        /// The recipe for any chapter: chapter one exactly as it always was, and every
        /// chapter after it starting where the one before ended.
        ///
        /// Built for about a thousand levels, which is what shapes it. Chapter one's rise
        /// kept up for a hundred chapters would put the last enemies at thirty-six times
        /// their first strength, against a player whose every purchase has a cap — the
        /// shop's tracks stop at thirty steps, the line at six posts. So the climb is
        /// chapter one's for two chapters and then bends, and the player's budget grows
        /// until it can field a line of knights and stops there.
        ///
        /// What stays put: the enemy count, which the map has no room to raise (see
        /// ChapterProgressionTests), the route lengths, and the shape of a chapter — an
        /// easier start, the escalation band, the tenth.
        /// </summary>
        public static ChapterRecipe For(int chapter)
        {
            var recipe = new ChapterRecipe();
            if (chapter <= 1) return recipe;

            recipe.EnemyStrengthStart = StrengthAtEndOf(chapter - 1);
            recipe.EnemyStrengthEnd = StrengthAtEndOf(chapter);

            // The generator pictures a player who has grown with the chapters before; the
            // chapter's own rise is still theirs to meet.
            recipe.EscortStrength = recipe.EnemyStrengthStart;

            // Stronger enemies take longer to put down and the smithy's prices do not
            // move, so the silver follows the strength — at its root, so that it helps
            // without keeping pace.
            recipe.SilverMultiplier = (float)System.Math.Sqrt(recipe.EnemyStrengthStart);

            recipe.TrapDensityStart = 1.0f;
            recipe.TrapDensityEnd = System.Math.Min(TrapCeiling, 1.6f + 0.05f * (chapter - 2));

            // Four points a chapter, six across one, up to a line of knights.
            recipe.SquadBudgetStart = System.Math.Min(SquadCeiling - 6, 12 + 4 * (chapter - 1));
            recipe.SquadBudgetEnd = recipe.SquadBudgetStart + 6;

            recipe.PostsStart = System.Math.Min(TroopTable.LinePosts, 3 + 2 * (chapter - 1));

            // Every kind from the first level: the lessons were chapter one's. An empty
            // table opens them all, as PoolForLevel reads a missing entry as level one.
            recipe.EnemyUnlockLevel = System.Array.Empty<int>();

            return recipe;
        }

        /// <summary>
        /// Enemy strength at the end of a chapter; chapter zero is the start of the first.
        ///
        /// Straight for two chapters — 1.35 and 1.70, the rise chapter one already had —
        /// then a logarithm that leaves at the same slope, so there is no step where the
        /// two meet: 2.0 after chapter three, about 3.1 after ten, 4.7 after fifty and 5.4
        /// after a hundred. Always rising, ever more slowly; the last fifty chapters add
        /// about as much as chapters three and four did.
        /// </summary>
        public static float StrengthAtEndOf(int chapter)
        {
            if (chapter <= 0) return 1f;
            if (chapter <= 2) return 1f + StrengthPerChapter * chapter;

            return 1f + StrengthPerChapter * 2f
                 + StrengthPerChapter * Knee * (float)System.Math.Log(1f + (chapter - 2) / Knee);
        }

        /// <summary>Squad points at which the budget stops: six knights, the dearest line there is.</summary>
        public static int SquadCeiling => TroopTable.LinePosts * TroopTable.Cost(TroopKind.Knights);

        /// <summary>Traps at the end of a chapter stop here; the scout and the engineer have to keep up.</summary>
        public const float TrapCeiling = 2.0f;
    }
}
