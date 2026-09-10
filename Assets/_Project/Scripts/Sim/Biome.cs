namespace TheVeil.Sim
{
    /// <summary>
    /// The country a chapter is set in.
    ///
    /// Not a terrain type. <see cref="TerrainType"/> is per tile, and a level is already a
    /// mix of them — road, plains, forest, marsh, pass — so the variety inside one country
    /// is taken care of. A biome is the country itself: what the ground is made of, what
    /// grows on it and what stands on it. Forest was the only one there was, and it was
    /// never named because nothing had to tell it apart from anything.
    ///
    /// Named Biome rather than Environment on purpose: <c>System.Environment</c> is in
    /// scope in every file with <c>using System;</c>, and a type of the same name would
    /// make the one line that needs it the one that does not compile.
    /// </summary>
    public enum Biome : byte
    {
        Forest = 0,
        Winter = 1
    }

    /// <summary>
    /// Which biome each chapter is set in.
    ///
    /// One function rather than a field on each view, for the reason
    /// <c>TheVeil.Gen.LevelMaps</c> exists: the planning map and the run each generated
    /// their own level from their own recipe once, and a route drawn on one landed in a
    /// lake on the other. A player who plans a level in snow must play it in snow, and a
    /// test that asks what chapter 2 is must get the answer both of them get.
    ///
    /// Deliberately engine-free, like the rest of TheVeil.Sim, so that answer can be
    /// tested without a scene.
    /// </summary>
    public static class Biomes
    {
        /// <summary>
        /// The chapter that is winter.
        ///
        /// The second, so the first biome after the forest can be reached in a few levels
        /// and judged in play before any more are built on the same machinery. How the
        /// hundred chapters are shared out between biomes comes later, and belongs here
        /// when it does.
        /// </summary>
        public const int WinterChapter = 2;

        public static Biome Of(int chapter) => chapter == WinterChapter ? Biome.Winter : Biome.Forest;
    }
}
