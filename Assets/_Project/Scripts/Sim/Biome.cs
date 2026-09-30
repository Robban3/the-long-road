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
        ///
        /// <b>The sea is not on this list, and that is the ending.</b> It used to be, and
        /// the rotation put it on chapters 7, 16, 25, 34, 43, 52, 61, 80, 89 and 98 - so a
        /// player reached the last ten levels having crossed the coast nine times already.
        /// The road ends at the sea and the caravan sails; a sea somebody has been to nine
        /// times is not an ending, it is a commute. So the coast is held back for
        /// <see cref="LastChapter"/> and the tour is the nine countries that are inland.
        /// See <see cref="Of"/>.
        /// </summary>
        /// <summary>
        /// Every country the game has, in the order the enum declares them.
        ///
        /// <b>Not the tour.</b> <see cref="Order"/> is the order chapters are visited in
        /// and the sea is no longer on it; this is one entry per country, for the things
        /// that are built one per country rather than one per chapter - the champions'
        /// faces, the setup's dressing, a test that wants to name them all. Sizing those
        /// by the tour worked only while the two lists happened to be the same, and the
        /// day the sea left the tour the champion table came up one short and threw on the
        /// dead lands.
        /// </summary>
        public static readonly Biome[] All =
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

        /// <b>Turned one step, on the day the last country was dressed.</b> The tour ran
        /// forest, winter, marsh for as long as the forest was the only country with any
        /// scenery in it - the opening had to be the one place that was finished, and
        /// everything after it fell back to the forest's dressing anyway. That reason is
        /// gone: all ten are built, and the first country a player sees no longer has to be
        /// the first one that was made.
        ///
        /// So the wheel turns by one. Winter opens, the forest closes the tour, and every
        /// chapter is a country the player would have met somewhere else before. The
        /// second pass turns it again by itself (see PassOf), as it always did.
        ///
        /// <b>And the ground is sown again with it.</b> Turning the wheel alone puts the
        /// same seed through a different recipe, which is most of a new level - but only
        /// most, and on two stretches of the road not even that. See
        /// <see cref="GroundSeed"/>, which carries the measurements, and LikenessReport,
        /// which took them.
        public static readonly Biome[] Order =
        {
            Biome.Winter,
            Biome.Marsh,
            Biome.Plains,
            Biome.Farmland,
            Biome.Mountain,
            Biome.Desert,
            Biome.Enchanted,
            Biome.Dead,
            Biome.Forest
        };

        /// <summary>
        /// The chapter that is winter.
        ///
        /// <b>The first, since the wheel turned.</b> It was the second while the forest
        /// opened the tour, and the reason given was that the first country after the
        /// forest wants to be reachable in a few levels and judged in play. That is now
        /// truer than ever: it is the first country at all. Kept as a name because the
        /// view, the setup and the tests all had one, and checked against
        /// <see cref="Order"/> by BiomeTests so the two cannot drift apart.
        /// </summary>
        public const int WinterChapter = 1;

        /// <summary>
        /// Which time round the tour this chapter is, counting from nought.
        ///
        /// Ten chapters to a pass. The pass is what makes the second hundred levels
        /// different from the first: the climb in <see cref="ChapterRecipe"/> is further
        /// along, the tour starts one country later, and the country is dressed for
        /// another season (see <see cref="DressingOf"/>).
        /// </summary>
        /// <summary>
        /// The last chapter of the campaign: where the road reaches the sea.
        ///
        /// Worked out from the two numbers that decide it rather than written down a third
        /// time, so a campaign of a different length cannot leave the coast stranded in
        /// the middle of it.
        /// </summary>
        public static int LastChapter => DifficultyCurve.Levels / Campaign.LevelsPerChapter;

        public static int PassOf(int chapter)
            => chapter < 1 ? 0 : (chapter - 1) / Order.Length;

        /// <summary>
        /// How many steps the wheel has been turned by hand, on top of the turn each pass
        /// makes by itself.
        /// </summary>
        public const int Turn = 1;

        /// <summary>A turn's worth of ground, measured in seed. Prime, and far wider than the campaign.</summary>
        // Wider than the campaign on purpose: added to a level's seed it must not land on
        // another level's, or two chapters would be growing their ground from one number.
        const int TurnStride = 524287;

        /// <summary>
        /// The seed a level's ground is grown from, which is not quite the same thing as
        /// the level's own seed.
        ///
        /// A level's seed is its identity - the chapter times a thousand plus the level,
        /// the number that goes in save data and in bug reports - and for every chapter on
        /// the tour it is also the number its ground comes from. That is enough, because
        /// turning the wheel puts the same seed through a different country's recipe, and
        /// a different recipe is most of a new level.
        ///
        /// <b>Turning the wheel was not enough, and it was measured twice before this was
        /// written.</b> The reasoning was that a level's terrain comes from its seed through
        /// its country's recipe, so a new recipe over the same seed is a new level. It is
        /// mostly true and it fails in two places.
        ///
        /// The sea is the first. It is pinned to <see cref="LastChapter"/> because that is
        /// where the road ends, so when the wheel turned, nine countries moved and the sea
        /// stood exactly where it was: same recipe, same seed, the same ten levels tile for
        /// tile. LikenessReport put 100-1 through 100-10 at a hundred per cent alike to what
        /// they had been.
        ///
        /// The second place is worse, because the wheel did reach it. Chapter nine went from
        /// the dead land to the forest - and five of its ten levels came back eighty per cent
        /// the ground they had been, which is the very number LikenessReport calls the point
        /// at which two levels read as the same place. The two countries are built from
        /// nearly the same terrain numbers: the dead land is a forest that died. So the
        /// river, the rock and the roads stayed where they were and only the props changed,
        /// which is one level in two sets of clothes and not two levels.
        ///
        /// So the ground is sown again, everywhere, by the same turn. The identity is
        /// untouched - the number in save data and bug reports is still the chapter and the
        /// level - and what moves is only the field the level is grown in. Everything that
        /// generates a campaign level comes through here: a tool that asks
        /// <see cref="DeterministicRandom.SeedFor"/> directly will search a different
        /// sequence than the one the catalogue was written from, and the map a player gets
        /// will not be the map that was measured.
        /// </summary>
        public static int GroundSeed(int chapter, int level)
            => DeterministicRandom.SeedFor(chapter, level) + Turn * TurnStride;

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
            // The end of the road. The last ten levels are the coast whatever the tour
            // would have said, because that is where the caravan stops being a caravan.
            if (chapter >= LastChapter) return Biome.Coast;

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
            // The sea is off the tour and its chapter is the last one, which is also the
            // only one. Answering nought here would have the roadmap name it after nothing
            // and the setup build it never.
            if (biome == Biome.Coast) return LastChapter;

            for (int i = 0; i < Order.Length; i++)
                if (Order[i] == biome) return i + 1;

            return 0;
        }
    }
}
