namespace TheVeil.Sim
{
    /// <summary>
    /// Running totals kept across the whole campaign, for the achievements to count.
    /// Stored by number: append only.
    ///
    /// Only what the save cannot already answer. Levels cleared, levels at three stars
    /// and chapters finished are all in the stars the campaign keeps, and a second count
    /// of the same thing is a count that can disagree with the first.
    /// </summary>
    public enum Tally : byte
    {
        GroupsBeaten = 0,
        TrapsDisarmed = 1,
        FlawlessArrivals = 2,
        GoldEarned = 3
    }

    /// <summary>What an achievement counts.</summary>
    public enum Measure : byte
    {
        LevelsCleared,
        LevelsAtThreeStars,
        ChaptersCleared,
        GroupsBeaten,
        TrapsDisarmed,
        FlawlessArrivals,
        GoldEarned
    }

    /// <summary>The achievements. Stored by number in the save: append only.</summary>
    public enum Achievement : byte
    {
        FirstRoad = 0,
        TenRoads = 1,
        TwentyFiveRoads = 2,
        FirstThreeStars = 3,
        TenThreeStars = 4,
        ChapterOne = 5,
        ChapterTwo = 6,
        GroupsTwentyFive = 7,
        GroupsHundred = 8,
        GroupsTwoFifty = 9,
        TrapsTen = 10,
        TrapsFifty = 11,
        FlawlessOne = 12,
        FlawlessTen = 13,
        GoldThousand = 14,
        GoldFiveThousand = 15
    }

    /// <summary>
    /// What each achievement asks and what it pays.
    ///
    /// <b>Things the player does anyway, counted.</b> Nothing here asks for a way of
    /// playing the game would not otherwise reward — no "finish a level without an
    /// escort" — because an achievement that pulls against the design is a reason to
    /// play it badly. They are a record of the road, with a purse at each milestone.
    ///
    /// <b>This is where gems come from.</b> The shop has sold nothing for them and nothing
    /// earned them; the economy rule is that gems buy time and never a decision
    /// (docs/economy.md), and a handful for having crossed a chapter is exactly that.
    /// </summary>
    public static class AchievementTable
    {
        public static readonly Achievement[] All =
        {
            Achievement.FirstRoad, Achievement.TenRoads, Achievement.TwentyFiveRoads,
            Achievement.FirstThreeStars, Achievement.TenThreeStars,
            Achievement.ChapterOne, Achievement.ChapterTwo,
            Achievement.GroupsTwentyFive, Achievement.GroupsHundred, Achievement.GroupsTwoFifty,
            Achievement.TrapsTen, Achievement.TrapsFifty,
            Achievement.FlawlessOne, Achievement.FlawlessTen,
            Achievement.GoldThousand, Achievement.GoldFiveThousand
        };

        // All indexed by (int)Achievement.
        static readonly Measure[] _measure =
        {
            Measure.LevelsCleared, Measure.LevelsCleared, Measure.LevelsCleared,
            Measure.LevelsAtThreeStars, Measure.LevelsAtThreeStars,
            Measure.ChaptersCleared, Measure.ChaptersCleared,
            Measure.GroupsBeaten, Measure.GroupsBeaten, Measure.GroupsBeaten,
            Measure.TrapsDisarmed, Measure.TrapsDisarmed,
            Measure.FlawlessArrivals, Measure.FlawlessArrivals,
            Measure.GoldEarned, Measure.GoldEarned
        };

        static readonly int[] _target = { 1, 10, 25, 1, 10, 1, 2, 25, 100, 250, 10, 50, 1, 10, 1000, 5000 };
        static readonly int[] _gold = { 50, 150, 300, 50, 200, 0, 0, 100, 250, 0, 100, 0, 75, 0, 0, 0 };
        static readonly int[] _gems = { 0, 0, 5, 0, 5, 10, 15, 0, 0, 20, 0, 10, 0, 10, 5, 20 };

        public static Measure MeasureOf(Achievement a) => _measure[(int)a];
        public static int Target(Achievement a) => _target[(int)a];
        public static int Gold(Achievement a) => _gold[(int)a];
        public static int Gems(Achievement a) => _gems[(int)a];
    }

    /// <summary>
    /// What one run added to the campaign's totals, taken off the run as it ends.
    ///
    /// A plain value rather than the run itself, so the campaign never has to know what a
    /// LevelRun is — the same line the rest of the campaign keeps.
    /// </summary>
    public struct RunTally
    {
        public bool Arrived;
        public int GroupsBeaten;
        public int TrapsDisarmed;
        public int WagonsLost;
        public int Gold;

        /// <summary>Arrived with every wagon it set out with.</summary>
        public bool Flawless => Arrived && WagonsLost == 0;
    }
}
