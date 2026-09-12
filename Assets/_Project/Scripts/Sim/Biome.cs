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
    ///
    /// Appended to and never reordered. A scene keeps a set of scenery per biome and the
    /// number is what it is keyed by, so moving one would dress a marsh in snow.
    /// </summary>
    public enum Biome : byte
    {
        Forest = 0,
        Winter = 1,

        /// <summary>Standing water, reeds, and few places to cross.</summary>
        Marsh = 2,

        /// <summary>Open grass: the horse's country, and nothing to stand behind.</summary>
        Plains = 3,

        /// <summary>Fields, fences and villages — country somebody lives in.</summary>
        Farmland = 4,

        /// <summary>Cliffs and narrow passes, where the road cannot go round.</summary>
        Mountain = 5,

        /// <summary>Dunes, salt grass and wrecks.</summary>
        Coast = 6,

        /// <summary>Sand and heat, where water is worth more than silver.</summary>
        Desert = 7,

        /// <summary>A wood that glows, and does not want a road through it.</summary>
        Enchanted = 8,

        /// <summary>Ash, dead trees, nobody left to trade with. The end of the road.</summary>
        Dead = 9
    }

    /// <summary>
    /// What has been done to a country since the last time the road went through it.
    ///
    /// Not a biome: a marsh in flood is still a marsh, and it keeps the marsh's models,
    /// its terrain mix and its enemies. This is the weather and the wear — which is why
    /// it is a second, smaller list rather than ten more countries.
    ///
    /// Appended to, never reordered, for the reason <see cref="Biome"/> is: a scene keys
    /// its scenery by the number.
    /// </summary>
    public enum Dressing : byte
    {
        /// <summary>The country as it is. Every chapter of the first pass.</summary>
        Plain = 0,

        /// <summary>Under snow, whatever the country is.</summary>
        Snow = 1,

        /// <summary>Burnt: black stems, ash, nothing green.</summary>
        Burnt = 2,

        /// <summary>Under water: pools where the fields were, and fewer ways across.</summary>
        Flood = 3
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
        /// The countries the road passes through, in the order it passes through them.
        ///
        /// <b>A tour rather than a region each.</b> A hundred chapters shared ten to a
        /// biome would spend the first ten in the same forest, and the forest is the one
        /// country the player has seen by level three. So the first ten chapters take the
        /// ten biomes one apiece — the pattern chapter 2 set by being winter — and then
        /// the list begins again, a rung further up <see cref="ChapterRecipe"/>'s climb.
        ///
        /// The order is neither alphabetical nor a matter of taste. The three after the
        /// winter are the ones the project's own art can already dress; the four at the
        /// end are the ones that need a pack bought for them, and they are last so they
        /// can be bought late. It also alternates close country with open — marsh, then
        /// plains; mountain, then coast — so no two chapters running feel like one chapter
        /// played twice.
        ///
        /// A biome whose scenery nobody has built yet falls back to the forest's (see
        /// LevelRunner), so a chapter named for a marsh plays as woodland until the marsh
        /// exists. That is the safe way to be wrong: a country that is not finished looks
        /// like the one that is, rather than like bare ground.
        /// </summary>
        public static readonly Biome[] Order =
        {
            Biome.Forest,
            Biome.Winter,
            Biome.Marsh,
            Biome.Plains,
            Biome.Farmland,
            Biome.Mountain,
            Biome.Coast,
            Biome.Desert,
            Biome.Enchanted,
            Biome.Dead
        };

        /// <summary>
        /// The chapter that is winter.
        ///
        /// Still the second: the first biome after the forest wants to be reachable in a
        /// few levels and judged in play. Kept as a name because the view, the setup and
        /// the tests all had one, and checked against <see cref="Order"/> by
        /// BiomeTests so the two cannot drift apart.
        /// </summary>
        public const int WinterChapter = 2;

        /// <summary>
        /// Which time round the tour this chapter is, counting from nought.
        ///
        /// Ten chapters to a pass. The pass is what makes the second hundred levels
        /// different from the first: the climb in <see cref="ChapterRecipe"/> is further
        /// along, the tour starts one country later, and the country is dressed for
        /// another season (see <see cref="DressingOf"/>).
        /// </summary>
        public static int PassOf(int chapter)
            => chapter < 1 ? 0 : (chapter - 1) / Order.Length;

        public static Biome Of(int chapter)
        {
            // Chapters count from one. Anything below that is a caller with no campaign
            // yet — a scene opened on its own, a test — and the opening forest is the
            // right answer for them too.
            if (chapter < 1) return Order[0];

            // Each pass starts one country later, so the ten are all visited every time
            // round but never in the same order: the first pass goes forest, winter,
            // marsh; the second winter, marsh, plains. Without the shift a player who has
            // seen ten chapters has seen the whole rest of the game in order.
            int index = (chapter - 1 + PassOf(chapter)) % Order.Length;
            return Order[index];
        }

        /// <summary>
        /// How the country is dressed this time round: the season, or what has happened to
        /// it since.
        ///
        /// The cheap half of variety. Ten countries are ten sets of models; a dressing is
        /// a change of materials and a few swaps over one of them — the same plain under
        /// snow, the same wood after a fire — so ten countries and four dressings are
        /// forty chapters that do not look like each other, for the art of ten.
        ///
        /// A dressing nothing has been built for falls back to the country's own look, the
        /// same way a country nothing has been built for falls back to the forest.
        /// </summary>
        public static Dressing DressingOf(int chapter)
            => (Dressing)(PassOf(chapter) % 4);

        /// <summary>
        /// Where in the tour a biome first appears, counting chapters from one; nought for
        /// a biome that is not in it. For the roadmap's chapter names, and for anybody
        /// deciding what to build next.
        /// </summary>
        public static int FirstChapterOf(Biome biome)
        {
            for (int i = 0; i < Order.Length; i++)
                if (Order[i] == biome) return i + 1;

            return 0;
        }
    }
}
