using System.Collections.Generic;
using System.Text;

namespace Arna.Sim
{
    /// <summary>
    /// What the player has done so far: which levels are open, how well each one went,
    /// and what is in the purse.
    ///
    /// The game has been able to *play* a level since early on and has never been able
    /// to *have played* one. A run ended, printed its stars, and the next press of Play
    /// started the same level from nothing — so there was no reason to go back to a
    /// two-star level and no way to reach a third one. That is the whole point of the
    /// level roadmap: a chain of levels you have beaten, each still open, each still
    /// holding a score you can improve.
    ///
    /// Stars are the currency of that. A run rates 0–3 (see <see cref="LevelRun.Stars"/>)
    /// and only the best result is kept, so replaying a level can never cost the player
    /// anything — and since the next chapter is opened by stars rather than by simply
    /// reaching the end, going back to a scrappy win is a real way forward and not
    /// merely a tidy-up.
    ///
    /// Deliberately engine-free, like the rest of Arna.Sim: it serialises itself to a
    /// short string and lets the layer above decide where that string lives (PlayerPrefs
    /// today, a cloud save later). That also means the save format can be tested.
    /// </summary>
    public sealed class Campaign
    {
        public const int LevelsPerChapter = 10;
        public const int MaxStars = 3;

        /// <summary>
        /// Stars needed in a chapter before the next one opens, out of its 30.
        ///
        /// Two thirds of a perfect run, which is roughly "clear every level, some of
        /// them well". Set at the level count the chapter can be finished with — 10, one
        /// star each — the gate would be no gate at all: simply arriving ten times opens
        /// the next chapter and stars mean nothing. Set at 30 it would demand a perfect
        /// chapter, and a single level that will not give up its third star locks the
        /// campaign. 20 leaves the player room to be bad at two or three levels and a
        /// reason to go back to them.
        /// </summary>
        public const int StarsToOpenNextChapter = 20;

        /// <summary>Stars per level, keyed by <see cref="Key"/>. Absent means never cleared.</summary>
        readonly Dictionary<int, int> _stars = new Dictionary<int, int>();

        /// <summary>Boons bought in the shop, by level. See <see cref="BoonTable"/>.</summary>
        readonly int[] _boons = new int[BoonTable.All.Length];

        /// <summary>Gold, the between-levels currency. Silver is spent inside a run.</summary>
        public int Gold { get; private set; }

        /// <summary>The hard currency in the mock-up's top bar. Not yet earned anywhere.</summary>
        public int Gems { get; private set; }

        public int HighestChapter { get; private set; } = 1;

        static int Key(int chapter, int level) => chapter * 100 + level;

        /// <summary>Best stars ever scored on this level, 0 if it has never been beaten.</summary>
        public int Stars(int chapter, int level)
            => _stars.TryGetValue(Key(chapter, level), out int stars) ? stars : 0;

        /// <summary>Whether the level has been finished at least once. Cleared levels stay replayable.</summary>
        public bool Cleared(int chapter, int level) => Stars(chapter, level) > 0;

        /// <summary>Stars collected in one chapter, out of <see cref="LevelsPerChapter"/> × 3.</summary>
        public int StarsIn(int chapter)
        {
            int total = 0;
            for (int level = 1; level <= LevelsPerChapter; level++) total += Stars(chapter, level);
            return total;
        }

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (var pair in _stars) total += pair.Value;
                return total;
            }
        }

        /// <summary>
        /// Whether the chapter can be entered at all.
        ///
        /// Chapter 1 always; after that, the previous chapter has to have earned its
        /// <see cref="StarsToOpenNextChapter"/>.
        /// </summary>
        public bool ChapterOpen(int chapter)
            => chapter <= 1 || StarsIn(chapter - 1) >= StarsToOpenNextChapter;

        /// <summary>
        /// Whether this level can be played.
        ///
        /// The first level of an open chapter is always available, and after that a
        /// level opens when the one before it has been cleared. A cleared level never
        /// closes again — that is what makes the roadmap a map and not a queue.
        /// </summary>
        public bool Unlocked(int chapter, int level)
        {
            if (level < 1 || level > LevelsPerChapter) return false;
            if (!ChapterOpen(chapter)) return false;

            return level == 1 || Cleared(chapter, level - 1);
        }

        /// <summary>The level the roadmap should open on: the first one not yet beaten.</summary>
        public void Furthest(out int chapter, out int level)
        {
            chapter = 1;
            level = 1;

            for (int c = 1; c <= HighestChapter; c++)
            {
                if (!ChapterOpen(c)) break;

                for (int l = 1; l <= LevelsPerChapter; l++)
                {
                    if (Cleared(c, l)) continue;

                    chapter = c;
                    level = l;
                    return;
                }

                chapter = c;
                level = LevelsPerChapter;
            }
        }

        /// <summary>
        /// Files the result of a run. Gold is always added; stars only ever go up.
        ///
        /// Returns whether this run beat the level's previous best, which is what the
        /// result screen needs in order to say something truthful about a replay: "3
        /// stars" on a level already at 3 is not news, and telling the player they have
        /// improved when they have not is the fastest way to make a score meaningless.
        /// </summary>
        public bool Record(int chapter, int level, int stars, int gold)
        {
            if (gold > 0) Gold += gold;
            if (chapter > HighestChapter && ChapterOpen(chapter)) HighestChapter = chapter;

            if (stars <= 0) return false;
            if (stars > MaxStars) stars = MaxStars;

            int key = Key(chapter, level);
            if (_stars.TryGetValue(key, out int best) && best >= stars) return false;

            _stars[key] = stars;

            // Opening the chapter this run belongs to is the gate's job; the *next*
            // chapter becoming reachable is noticed here so the roadmap has a tab to
            // show without anyone having played there yet.
            if (chapter >= HighestChapter && ChapterOpen(chapter + 1)) HighestChapter = chapter + 1;

            return true;
        }

        public int BoonLevel(Boon boon) => _boons[(int)boon];

        /// <summary>What the next level of this boon costs, or zero when it is finished.</summary>
        public int PriceOf(Boon boon) => BoonTable.Price(boon, _boons[(int)boon]);

        /// <summary>
        /// Buys the next level of a boon, and says what it cost.
        ///
        /// Refused when the boon is finished or the gold is not there, and in both cases
        /// nothing is spent — the caller can offer the button either way and let this
        /// decide, rather than working out the answer twice and disagreeing with itself.
        /// </summary>
        public bool TryBuy(Boon boon, out int cost)
        {
            cost = PriceOf(boon);

            if (cost <= 0 || Gold < cost) { cost = 0; return false; }

            Gold -= cost;
            _boons[(int)boon]++;

            return true;
        }

        /// <summary>What has been bought, in the form a run can use.</summary>
        public Boons Boons()
        {
            var boons = new Boons();
            foreach (var boon in BoonTable.All) boons.Set(boon, _boons[(int)boon]);

            return boons;
        }

        public void Earn(int gold, int gems = 0)
        {
            Gold += gold;
            Gems += gems;
        }

        /// <summary>Returns whether there was enough, and takes it if there was.</summary>
        public bool Spend(int gold)
        {
            if (gold < 0 || Gold < gold) return false;

            Gold -= gold;
            return true;
        }

        public bool SpendGems(int gems)
        {
            if (gems < 0 || Gems < gems) return false;

            Gems -= gems;
            return true;
        }

        /// <summary>
        /// The save string: <c>2|gold|gems|chapter.level.stars,…|boon.level,…</c>
        ///
        /// A line of text rather than JSON because Arna.Sim may not touch the engine and
        /// therefore has no JsonUtility, and because a save this small is easier to read
        /// in a bug report as text than as anything else. The leading number is the format
        /// version: version 1 had no boons and still loads, with none.
        /// </summary>
        public string Save()
        {
            var text = new StringBuilder();
            text.Append('2').Append('|').Append(Gold).Append('|').Append(Gems).Append('|');

            bool first = true;
            for (int chapter = 1; chapter <= HighestChapter; chapter++)
            {
                for (int level = 1; level <= LevelsPerChapter; level++)
                {
                    int stars = Stars(chapter, level);
                    if (stars <= 0) continue;

                    if (!first) text.Append(',');
                    text.Append(chapter).Append('.').Append(level).Append('.').Append(stars);
                    first = false;
                }
            }

            text.Append('|');

            bool leading = true;
            for (int i = 0; i < _boons.Length; i++)
            {
                if (_boons[i] <= 0) continue;

                if (!leading) text.Append(',');
                text.Append(i).Append('.').Append(_boons[i]);
                leading = false;
            }

            return text.ToString();
        }

        /// <summary>
        /// Reads a save back. Anything unreadable gives a fresh campaign rather than an
        /// exception: a corrupt save should cost the player their progress, not their
        /// ability to start the game.
        /// </summary>
        public static Campaign Load(string saved)
        {
            var campaign = new Campaign();
            if (string.IsNullOrEmpty(saved)) return campaign;

            var parts = saved.Split('|');
            if (parts.Length < 4) return campaign;

            // Version 1 is the same save without the boons on the end, and is read as a
            // campaign that has bought nothing — which is exactly what it was.
            if (parts[0] != "1" && parts[0] != "2") return campaign;

            if (int.TryParse(parts[1], out int gold) && gold > 0) campaign.Gold = gold;
            if (int.TryParse(parts[2], out int gems) && gems > 0) campaign.Gems = gems;

            foreach (var entry in parts[3].Split(','))
            {
                if (entry.Length == 0) continue;

                var field = entry.Split('.');
                if (field.Length != 3) continue;

                if (!int.TryParse(field[0], out int chapter) ||
                    !int.TryParse(field[1], out int level) ||
                    !int.TryParse(field[2], out int stars)) continue;

                if (chapter < 1 || level < 1 || level > LevelsPerChapter) continue;
                if (stars < 1) continue;
                if (stars > MaxStars) stars = MaxStars;

                campaign._stars[Key(chapter, level)] = stars;
                if (chapter > campaign.HighestChapter) campaign.HighestChapter = chapter;
            }

            if (parts.Length > 4)
            {
                foreach (var entry in parts[4].Split(','))
                {
                    if (entry.Length == 0) continue;

                    var field = entry.Split('.');
                    if (field.Length != 2) continue;

                    if (!int.TryParse(field[0], out int boon) ||
                        !int.TryParse(field[1], out int level)) continue;

                    if (boon < 0 || boon >= campaign._boons.Length || level < 1) continue;

                    int max = BoonTable.MaxLevel((Boon)boon);
                    campaign._boons[boon] = level > max ? max : level;
                }
            }

            // A save can name a chapter the gate would not open — an older build, a
            // rebalanced gate — and the roadmap still has to show the tab the player's
            // stars are sitting in. Only the chapter *after* the highest played one is
            // decided by the gate here.
            if (campaign.ChapterOpen(campaign.HighestChapter + 1)) campaign.HighestChapter++;

            return campaign;
        }
    }
}
