using System;

namespace TheVeil.Sim
{
    /// <summary>
    /// How hard each level of the campaign is meant to be, as one number: the share of
    /// the escort the player the curve assumes loses across the level's three roads, with
    /// a road they do not get down counting as the whole escort.
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
    /// The shape: a quarter of the escort at the first level, rising quickly through the
    /// first chapters and more slowly after, towards four fifths by the end of a
    /// thousand levels. Steeper at the start because that is where a player is learning
    /// fastest; flattening because a curve that kept its first slope would be past
    /// anybody's reach by the tenth chapter.
    /// </summary>
    public static class DifficultyCurve
    {
        /// <summary>
        /// The chapters that exist and are held to this curve. The ones after are generated
        /// against the roads the escort gets down and nothing more, until they are built -
        /// see CatalogueBuilder.
        /// </summary>
        public const int BuiltChapters = 3;

        /// <summary>What the first level costs the escort, on average over its roads.</summary>
        public const float First = 0.25f;

        /// <summary>What the curve approaches and never reaches.</summary>
        public const float Ceiling = 0.80f;

        /// <summary>
        /// How many levels it takes to cover about two thirds of the rise.
        ///
        /// Thirty. At forty-five the target for the second chapter sat at 36 to 44 per cent,
        /// and the easiest map the generator could find for 2-1 that kept every other rule
        /// cost 49 - the curve rose more slowly than the game does, and every level from
        /// there was a compromise. At thirty it runs 25 to 39 through the first chapter, 40
        /// to 51 through the second and 52 to 59 through the third.
        /// </summary>
        public const float Pace = 30f;

        /// <summary>
        /// How far a level may sit from its target and still be kept.
        ///
        /// A tenth either way: tighter and the search runs out of maps before it finds
        /// one, looser and two neighbouring levels can swap places.
        /// </summary>
        public const float Tolerance = 0.10f;

        /// <summary>The target for a level, by its place in the whole campaign.</summary>
        public static float Target(int chapter, int level)
        {
            int index = (chapter - 1) * Campaign.LevelsPerChapter + (level - 1);
            if (index < 0) index = 0;

            return First + (Ceiling - First) * (1f - (float)Math.Exp(-index / Pace));
        }
    }
}
