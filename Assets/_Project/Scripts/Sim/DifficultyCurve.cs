using System;

namespace TheVeil.Sim
{
    /// <summary>
    /// How hard each level of the campaign is meant to be, as one number: the share of
    /// the escort the player the curve assumes loses on the level's safe and long roads,
    /// a road they do not get down counting as the whole escort.
    ///
    /// <b>Not the fast road.</b> It was all three roads at first, and a fast road that ended
    /// the run counted as a third of the level on its own - so once the fast road was made
    /// to kill the ordinary escort about every other level, the levels zigzagged ten points
    /// up and down with it. The fast road is a gamble with a life on it and is meant to be;
    /// what makes one level harder than the last is felt on the roads a careful player
    /// takes. The fast road answers to rules of its own: the hardest road on every level,
    /// and a killer about every other time (CatalogueBuilder.Best).
    ///
    /// <b>Every level harder than the one before, from the first to the last.</b> The
    /// levels used to be a saw: the enemy budget went back to a hundred at the start of
    /// every chapter after a hundred and forty at its end, and the escort lost two points
    /// and a post, so each chapter's first level was easier than the last level of the one
    /// before. And under the saw the levels jumped about on their own account - measured,
    /// 2-2 cost the escort everything on all three roads while 2-5 let it through with
    /// three quarters left, at the same settings. The settings decide what a level is
    /// likely to be, and the map decides what it is.
    ///
    /// So the curve is written here and the generator is held to it: a map is kept only
    /// when its measured difficulty sits within <see cref="Tolerance"/> of the target for
    /// its place in the campaign (LevelMaps.Gate).
    ///
    /// The shape: a tenth of the escort at the first level and seven tenths at the
    /// thousandth, rising every level in between - fastest at the start, where a player
    /// is learning fastest, and more slowly after, because the campaign is a thousand
    /// levels long and the share of an escort a level can cost stops at all of it.
    /// See <see cref="Bend"/>.
    /// </summary>
    public static class DifficultyCurve
    {
        /// <summary>
        /// The chapters that exist and are held to this curve. The ones after are generated
        /// against the roads the escort gets down and nothing more, until they are built -
        /// see CatalogueBuilder.
        /// </summary>
        public const int BuiltChapters = 4;

        /// <summary>How many levels the curve spans: the whole campaign.</summary>
        public const int Levels = 1000;

        /// <summary>What the first level costs the escort, on its safe and long roads.</summary>
        public const float First = 0.10f;

        /// <summary>What the last level costs it: seven tenths, and a prepared player still through.</summary>
        public const float Last = 0.70f;

        /// <summary>
        /// Where the curve bends, in levels.
        ///
        /// <b>Spread over a thousand levels, because a share of an escort ends at a hundred
        /// per cent.</b> The first version rose about a point a level - right for three
        /// chapters, and out of room by level sixty or seventy: past that no level could be
        /// harder than the last without being impossible, and nine hundred and thirty levels
        /// would have had nowhere to go. A logarithm keeps rising for ever and never runs
        /// out: ten levels in it rises a point and a quarter a level, which a player feels;
        /// by level five hundred a fortieth of a point, which a campaign adds up.
        ///
        /// Ten puts it at 18 per cent at level 10, 28 at level 30, 41 at 100, 50 at 200,
        /// 61 at 500 and 70 at the thousandth.
        /// </summary>
        public const float Bend = 10f;

        /// <summary>
        /// How far a level may sit from its target and still be kept.
        ///
        /// A tenth either way: tighter and the search runs out of maps before it finds
        /// one, looser and two neighbouring levels can swap places.
        /// </summary>
        public const float Tolerance = 0.10f;

        /// <summary>The target for a level, by its place in the whole campaign.</summary>
        public static float Target(int chapter, int level) => At(Index(chapter, level));

        /// <summary>
        /// How much harder this level is meant to be than the one before it - always more
        /// than nothing, from the first level to the thousandth.
        /// </summary>
        public static float Rise(int chapter, int level)
        {
            int index = Index(chapter, level);
            return index <= 0 ? 0f : At(index) - At(index - 1);
        }

        static int Index(int chapter, int level)
        {
            int index = (chapter - 1) * Campaign.LevelsPerChapter + (level - 1);
            return index < 0 ? 0 : index;
        }

        static float At(int index)
        {
            if (index > Levels - 1) index = Levels - 1;

            return First + (Last - First)
                   * (float)(Math.Log(1.0 + index / Bend) / Math.Log(1.0 + (Levels - 1) / Bend));
        }
    }
}
