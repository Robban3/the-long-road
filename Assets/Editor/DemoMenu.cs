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

        [MenuItem(Root + "Chapter 1")]
        static void ChapterOne() => Enter(1);

        [MenuItem(Root + "Chapter 2")]
        static void ChapterTwo() => Enter(2);

        [MenuItem(Root + "Chapter 1", true)]
        [MenuItem(Root + "Chapter 2", true)]
        static bool Playing() => EditorApplication.isPlaying;

        static void Enter(int chapter)
        {
            Session.Choose(chapter, 1);
            Session.Forget();
            SceneManager.LoadScene(Session.PlanScene);
        }

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
