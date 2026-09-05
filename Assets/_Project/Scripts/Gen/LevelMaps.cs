using TheVeil.Sim;

namespace TheVeil.Gen
{
    /// <summary>
    /// The one place a level's map comes from.
    ///
    /// <b>There were two, and they disagreed.</b> The planning screen generated with
    /// <c>new LevelRecipe()</c> and the run with <c>new ChapterRecipe().ForLevel(level)</c>
    /// — the same seed through two different recipes. That is not a small difference:
    /// <see cref="TerrainGenerator.Generate"/> tries up to twelve terrain fields and keeps
    /// the first that satisfies the recipe's demands, and each attempt is seeded
    /// <c>seed + attempt * 7919</c>. Two recipes that accept different attempt numbers
    /// therefore produce two <i>unrelated landscapes</i> — different lakes, in different
    /// places.
    ///
    /// The route crosses between them as bare tile indices (App.ChosenRoute), and nothing
    /// on either side carries the map's identity. So a line drawn carefully around a lake
    /// on the planning map indexed into the run's own terrain, and could land in open
    /// water — where <c>TerrainTable.Speed(Water)</c> is zero, which is an absorbing
    /// state: the caravan stops, the tile under it never changes, and the run neither
    /// wins nor loses nor ends.
    ///
    /// The demands that decide which attempt wins are ordinary content settings —
    /// MinRouteTiles, EnemyBudget, TrapDensity, EnemyPool, EnemyStrength, RoutesOwed — so
    /// this could be broken again by tuning a number, and silently. It was: raising
    /// LevelRecipe.EnemyBudget from 100 to 120 while ChapterRecipe.EnemyBudgetStart stayed
    /// at 100 pushed level 1's two recipes apart, and the corridor and placer work moved
    /// the accept conditions themselves.
    ///
    /// Hence one function rather than a convention. A convention is what was already
    /// there.
    /// </summary>
    public static class LevelMaps
    {
        /// <summary>The map for one level, as both the planning screen and the run see it.</summary>
        public static LevelMap For(int chapter, int level)
            => TerrainGenerator.Generate(Recipe(level), DeterministicRandom.SeedFor(chapter, level));

        /// <summary>
        /// The recipe that level is built from.
        ///
        /// Exposed because the run needs the same object for what is *in* the level —
        /// enemy strength, the squad's budget and posts — and reading those off a second
        /// recipe is the same class of fault as generating off one.
        /// </summary>
        public static LevelRecipe Recipe(int level) => new ChapterRecipe().ForLevel(level);
    }
}
