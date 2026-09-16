using TheVeil.Sim;
using TheVeil.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheVeil.Editor
{
    /// <summary>
    /// Jumps a running game to the first level of a chapter, for showing the seasons
    /// side by side without playing through a chapter to get there.
    ///
    /// It goes in the way the roadmap does (see MenuShell.Draw): choose, then load the
    /// planning map. Restarting the running level in place was the obvious alternative
    /// and is wrong — the run's HUD reads its chapter and level once, in Start, and
    /// would go on naming the level that was left.
    ///
    /// Play mode only. Outside it there is nothing to choose into: entering play mode
    /// reloads the domain, and the Session is back at chapter one before the first
    /// scene has loaded.
    /// </summary>
    public static class DemoMenu
    {
        const string Root = "The Veil/Demo/";

        // One for every country on the tour (Biomes.Order), because a chapter nobody can
        // jump to is a chapter nobody looks at: the winter was built, shipped and only
        // seen because this menu existed, and the marsh would have waited its turn behind
        // twenty levels of forest. They are written out one by one because a MenuItem is
        // an attribute and attributes cannot be looped over.
        [MenuItem(Root + "Chapter 1 — Forest")]
        static void ChapterOne() => Enter(1);

        [MenuItem(Root + "Chapter 2 — Winter")]
        static void ChapterTwo() => Enter(2);

        [MenuItem(Root + "Chapter 3 — Marsh")]
        static void ChapterThree() => Enter(3);

        [MenuItem(Root + "Chapter 4 — Plains")]
        static void ChapterFour() => Enter(4);

        [MenuItem(Root + "Chapter 5 — Farmland")]
        static void ChapterFive() => Enter(5);

        [MenuItem(Root + "Chapter 6 — Mountain")]
        static void ChapterSix() => Enter(6);

        [MenuItem(Root + "Chapter 7 — Coast")]
        static void ChapterSeven() => Enter(7);

        [MenuItem(Root + "Chapter 8 — Desert")]
        static void ChapterEight() => Enter(8);

        [MenuItem(Root + "Chapter 9 — Enchanted")]
        static void ChapterNine() => Enter(9);

        [MenuItem(Root + "Chapter 10 — Dead land")]
        static void ChapterTen() => Enter(10);

        [MenuItem(Root + "Chapter 1 — Forest", true)]
        [MenuItem(Root + "Chapter 2 — Winter", true)]
        [MenuItem(Root + "Chapter 3 — Marsh", true)]
        [MenuItem(Root + "Chapter 4 — Plains", true)]
        [MenuItem(Root + "Chapter 5 — Farmland", true)]
        [MenuItem(Root + "Chapter 6 — Mountain", true)]
        [MenuItem(Root + "Chapter 7 — Coast", true)]
        [MenuItem(Root + "Chapter 8 — Desert", true)]
        [MenuItem(Root + "Chapter 9 — Enchanted", true)]
        [MenuItem(Root + "Chapter 10 — Dead land", true)]
        static bool Playing() => EditorApplication.isPlaying;

        /// <summary>
        /// Straight to the walled town, which is the one level nobody can reach by
        /// starting a chapter: it is the eighth, and the menu above opens chapters at
        /// their first level. Seven levels of forest is a long way to go to look at a
        /// wall.
        /// </summary>
        [MenuItem(Root + "The town (1-8)")]
        static void Town() => Enter(Towns.Chapter, Towns.Level);

        [MenuItem(Root + "The town (1-8)", true)]
        static bool TownPlaying() => EditorApplication.isPlaying;

        /// <summary>
        /// Straight to a chapter's champion, with the escort a player would be holding
        /// when they met him.
        ///
        /// <b>Written because the fight had never once been seen.</b> Two playtests of
        /// 1-10 both ended on the road at a hundred and eighteen seconds with the escort
        /// dead and the champion untouched, and the conclusion drawn from that was that
        /// the champion was unreachable. He is not: the simulated run of the same level
        /// wins on two of its three roads — see ChampionReport.PlayTheChampions.
        ///
        /// What was wrong was the way in. Entering a level without coming through the
        /// escort screen leaves Session.HasEscort false, so LevelRunner falls back to the
        /// formation serialized in the scene — six posts of whatever was last saved
        /// there, at no weapon level, with nothing bought. That is a fair fight for 1-1
        /// and it is not the player who arrives at 1-10, which is the point
        /// ReferenceSquad was written to make and which the tooling then went on
        /// ignoring.
        /// </summary>
        [MenuItem(Root + "The champion (1-10)")]
        static void Champion() => Enter(1, Campaign.LevelsPerChapter, geared: true);

        [MenuItem(Root + "The champion (1-10)", true)]
        static bool ChampionPlaying() => EditorApplication.isPlaying;

        static void Enter(int chapter) => Enter(chapter, 1);

        static void Enter(int chapter, int level, bool geared = false)
        {
            Session.Choose(chapter, level);
            Session.Forget();

            if (geared) Gear(chapter, level);

            SceneManager.LoadScene(Session.PlanScene);
        }

        /// <summary>
        /// Puts the campaign where a player entering this level would be: the line
        /// ReferenceSquad says they would be fielding, and the smithy behind it.
        ///
        /// Both halves are needed and neither is enough. Session.SetEscort carries the
        /// composition — which troops, in which posts — and nothing else; the weapon and
        /// armour levels come from the campaign's own purchases, through
        /// Campaign.TroopBoons. Set one without the other and the caravan goes in with
        /// the right line at no level, which is most of the way to the escort that lost
        /// twice.
        ///
        /// Bought rather than assigned, through the same TryBuy the shop uses, so a
        /// geared demo run is a state the game could actually have reached. The gold is
        /// granted first because the point is to skip the grind, not to model it.
        /// </summary>
        static void Gear(int chapter, int level)
        {
            var squad = ReferenceSquad.For(chapter, level);
            Session.SetEscort(squad);

            int smithy = ReferenceSquad.Smithy(chapter);
            if (smithy <= 0) return;

            Session.Campaign.Earn(GearingGold);

            foreach (var group in squad.Slots)
            {
                if (group == null) continue;

                for (int step = 0; step < smithy; step++)
                {
                    Session.Campaign.TryBuy(group.Kind, UpgradeTrack.Weapon, out _);
                    Session.Campaign.TryBuy(group.Kind, UpgradeTrack.Armour, out _);
                }
            }
        }

        /// <summary>
        /// Gold handed to a geared demo run before it starts buying.
        ///
        /// Generous on purpose and not a balance number: it is spent immediately on a
        /// fixed list and whatever is left over is spendable in the shop, which is the
        /// same freedom a player who had played the chapter would have.
        /// </summary>
        const int GearingGold = 20000;

        /// <summary>
        /// Every troop on one field, marching into bandits and fighting them. See
        /// TheVeil.App.TroopShowcase.
        ///
        /// Built in a fresh scene rather than kept as one: the showcase is its own
        /// component plus the setup's model library, and a scene asset would be one more
        /// copy of that library to go stale every time a troop's model changes. Built
        /// here, it is always the library setup would build now.
        ///
        /// Not play-mode-only, unlike the chapters: it makes its own scene and starts
        /// play itself, and outside play mode is where a scene can be made.
        /// </summary>
        [MenuItem(Root + "Troop Showcase")]
        static void TroopShowcase()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.Log("[The Veil] Stop the game first, then run Troop Showcase — it builds a scene of its own.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var showcase = new GameObject("Troop Showcase").AddComponent<TheVeil.App.TroopShowcase>();
            showcase.Models = TheVeilSetup.LoadModels();

            EditorApplication.isPlaying = true;
        }
    }
}
