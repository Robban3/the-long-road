using Arna.Sim;
using UnityEngine;

namespace Arna.UI
{
    /// <summary>
    /// What the menus and the level share: the campaign, and which level the player is
    /// currently in.
    ///
    /// Static because it has to outlive three scene loads — roadmap, planning map, run —
    /// and because there is exactly one player. The campaign itself is engine-free and
    /// testable (see <see cref="Campaign"/>); this is only the part that knows where the
    /// save lives and which scene is which.
    /// </summary>
    public static class Session
    {
        public const string MenuScene = "MainMenu";
        public const string PlanScene = "LevelPreview";
        public const string PlayScene = "PlayLevel";

        const string SaveKey = "arna.campaign.v1";

        static Campaign _campaign;

        /// <summary>The chapter and level about to be, or currently being, played.</summary>
        public static int Chapter { get; private set; } = 1;
        public static int Level { get; private set; } = 1;

        /// <summary>
        /// How the last run ended, for the result screen and for the roadmap to animate
        /// to. Stars of -1 means no run has finished this session.
        /// </summary>
        public static int LastStars { get; private set; } = -1;
        public static int LastGold { get; private set; }
        public static bool LastWasBest { get; private set; }

        public static Campaign Campaign
        {
            get
            {
                if (_campaign == null) _campaign = Campaign.Load(PlayerPrefs.GetString(SaveKey, ""));
                return _campaign;
            }
        }

        public static void Choose(int chapter, int level)
        {
            Chapter = chapter < 1 ? 1 : chapter;
            Level = level < 1 ? 1 : level;
        }

        public static void Save()
        {
            PlayerPrefs.SetString(SaveKey, Campaign.Save());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Files a finished run and writes the save.
        ///
        /// Called from the result screen rather than the moment the run ends, so that a
        /// player who closes the game mid-victory-screen has still banked it: the run is
        /// over, the stars are earned, and nothing about showing them should be able to
        /// take that back.
        /// </summary>
        public static void Finish(int stars, int gold)
        {
            LastStars = stars;
            LastGold = gold;
            LastWasBest = Campaign.Record(Chapter, Level, stars, gold);

            Save();
        }

        public static void Forget()
        {
            LastStars = -1;
            LastGold = 0;
            LastWasBest = false;
        }

        /// <summary>Whether there is a level after this one that the player may now enter.</summary>
        public static bool HasNext(out int chapter, out int level)
        {
            chapter = Chapter;
            level = Level + 1;

            if (level <= Campaign.LevelsPerChapter) return Campaign.Unlocked(chapter, level);

            chapter = Chapter + 1;
            level = 1;

            return Campaign.ChapterOpen(chapter);
        }

        /// <summary>Wipes the save. Reachable from Settings, and asked twice before it fires.</summary>
        public static void Wipe()
        {
            _campaign = new Campaign();
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Forget();
        }
    }
}
