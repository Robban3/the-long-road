namespace TheVeil.Sim
{
    /// <summary>
    /// Where the way into the enchanted wood stands, and the way out of it.
    ///
    /// <b>The one country you do not walk into.</b> Every other border on the road is a
    /// change of ground - the fen dries into plains, the plains rise into passes - and the
    /// enchanted wood is not somewhere the same road reaches. So it is entered through a
    /// portal and left through one: the caravan arrives at a gate standing in the last
    /// country, comes out of a gate in the first level of the wood, and goes out through a
    /// third at the end of it.
    ///
    /// Three places, and all three are asked for by <b>country</b> rather than by chapter
    /// number. The wood is the seventh chapter on the first pass round the tour and the
    /// sixteenth on the second (see <see cref="Biomes.PassOf"/>), so a rule written as
    /// "chapter seven" would put a portal in the desert the second time round and leave the
    /// wood with none. Waterfalls.Cuts learned this before it happened and says so; two
    /// editor reports learned it afterwards and had to be mended.
    /// </summary>
    public static class Portals
    {
        /// <summary>The country the portals lead to and from.</summary>
        public const Biome Country = Biome.Enchanted;

        /// <summary>
        /// Whether this level has a portal standing on it, and on which end of the road.
        ///
        /// Nought for none, 1 at the start, 2 at the goal. A level never has both: the wood
        /// is entered on its first level and left on its last, and those are different
        /// levels in every chapter longer than one.
        /// </summary>
        public static int On(int chapter, int level)
        {
            // Out of the country before: the last level of a chapter whose next chapter is
            // the wood. This is what makes the gate read as a door rather than as scenery -
            // the player walks into it at the end of one chapter and out of it at the
            // beginning of the next.
            if (level == Campaign.LevelsPerChapter && Biomes.Of(chapter + 1) == Country) return AtGoal;

            if (Biomes.Of(chapter) != Country) return 0;

            if (level == 1) return AtStart;
            if (level == Campaign.LevelsPerChapter) return AtGoal;

            return 0;
        }

        public const int AtStart = 1;
        public const int AtGoal = 2;

        /// <summary>
        /// The tile a portal stands on, or -1 where this level has none.
        ///
        /// The start or the goal itself, and not a tile beside it. A gate the caravan walks
        /// past is a piece of scenery; a gate it walks through is the way in.
        ///
        /// On a tenth level the keep stands here too, and the two do not collide:
        /// <see cref="Strongholds.Site"/> puts a castle at a standoff from the goal and
        /// never on it, so the goal tile is free and the yard is beside it. The player
        /// comes through the gate with the castle at their shoulder.
        /// </summary>
        public static int Site(LevelMap map, int chapter, int level)
        {
            if (map == null) return -1;

            switch (On(chapter, level))
            {
                case AtStart: return map.StartIndex;
                case AtGoal: return map.GoalIndex;
                default: return -1;
            }
        }
    }
}
