using TheVail.Sim;
using UnityEngine;

namespace TheVail.UI
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

        const string SaveKey = "thevail.campaign.v1";

        /// <summary>
        /// Where the campaign was kept when the world was called Arna.
        ///
        /// A PlayerPrefs key is a contract with a device, not a name in the source, and
        /// renaming one is silent: the game reads an empty string, decides this is a new
        /// player and starts at chapter one, with every star and every purchase still
        /// sitting on disk under a key nobody asks for any more. No error, no warning,
        /// nothing on screen but a fresh save.
        ///
        /// So the old key is read once, written across, and removed. Kept as a constant
        /// rather than inlined because it is the reason <see cref="Carried"/> exists, and
        /// a bare string there would look like a typo worth tidying away.
        /// </summary>
        const string OldSaveKey = "arna.campaign.v1";

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
                if (_campaign == null) _campaign = Campaign.Load(Carried());
                return _campaign;
            }
        }

        /// <summary>
        /// The saved campaign, from wherever it is, moving it if it is in the old place.
        ///
        /// The new key wins whenever it holds anything, so a player who has saved once
        /// since the rename never touches the old one again. Only a device that has the
        /// old and not the new is migrated, and it is migrated on the read rather than on
        /// some startup hook, because the read is the one thing that is guaranteed to
        /// happen before the save can matter.
        /// </summary>
        static string Carried()
        {
            string current = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(current)) return current;

            string old = PlayerPrefs.GetString(OldSaveKey, "");
            if (string.IsNullOrEmpty(old)) return "";

            PlayerPrefs.SetString(SaveKey, old);
            PlayerPrefs.DeleteKey(OldSaveKey);
            PlayerPrefs.Save();

            Debug.Log("[The Vail] Campaign carried over from the old save key.");
            return old;
        }

        /// <summary>
        /// The escort the player put together, by formation slot, carried into the run.
        ///
        /// Null in a slot means an empty post. Index 6 is the scouting post, which only
        /// a scout may hold (see FormationSlot.Scouting).
        /// </summary>
        public static readonly TroopKind?[] Escort = new TroopKind?[TroopTable.LinePosts + 1];

        /// <summary>Whether anything was chosen. An empty escort is a caravan travelling alone.</summary>
        public static bool HasEscort
        {
            get
            {
                foreach (var kind in Escort) if (kind.HasValue) return true;
                return false;
            }
        }

        public static void ClearEscort()
        {
            for (int i = 0; i < Escort.Length; i++) Escort[i] = null;
        }

        /// <summary>
        /// Fills the escort from a squad the troop screen has been building.
        ///
        /// Kept as plain kinds rather than as the Squad itself, because the run builds its
        /// own with that level's budget and posts — the screen's copy is a choice, not the
        /// thing that fights.
        /// </summary>
        public static void SetEscort(Squad squad)
        {
            ClearEscort();
            if (squad == null) return;

            for (int i = 0; i < squad.Slots.Count && i < Escort.Length; i++)
                if (squad.Slots[i] != null) Escort[i] = squad.Slots[i].Kind;
        }

        /// <summary>
        /// Open the shop instead of the front page when the menu scene next loads.
        ///
        /// Read once and cleared. The shop is a screen in the menu scene and the result
        /// screen is in the run, so the only way from one to the other is a scene load —
        /// and a scene load lands wherever the menu decides to start. This is the note it
        /// leaves for itself.
        /// </summary>
        public static bool OpenShop { get; set; }

        /// <summary>
        /// Scout flights bought for the level being played, this attempt.
        ///
        /// Not saved and not part of the campaign: it is spent on one crossing, and
        /// leaving a level throws it away. Cleared in <see cref="Choose"/>, which is the
        /// one call every route into a level goes through.
        /// </summary>
        public static int ScoutFlights { get; private set; }

        /// <summary>Books one flight. The gold has already been taken by the caller.</summary>
        public static void BuyScoutFlight() => ScoutFlights++;

        public static void Choose(int chapter, int level)
        {
            // Always, because Choose *is* entering a level. What was bought for the last
            // crossing does not carry to the next, and re-entering the same one is a
            // fresh attempt for the same reason: that bird flew that ground and is down.
            //
            // Unconditional rather than only-when-it-changed, which was written first and
            // is harder to be sure of: the purchases happen in the planning scene, after
            // this, so there is no window where clearing could throw one away.
            ScoutFlights = 0;

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

            // The old one too. A wipe that leaves it behind would be undone by the next
            // read, which migrates it straight back in.
            PlayerPrefs.DeleteKey(OldSaveKey);
            PlayerPrefs.Save();
            Forget();
        }
    }
}
