using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arna.UI
{
    /// <summary>
    /// The menu scene: one canvas, one screen at a time.
    ///
    /// The game had no front end at all — Play started a level and quitting was closing
    /// the application — so this is where a session now begins and ends. It holds the
    /// canvas, paints the backdrop, and swaps the screen inside it; every screen is a
    /// method that fills an empty rect, which keeps them independent of each other and
    /// of the order they are reached in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuShell : MonoBehaviour
    {
        /// <summary>Opens straight onto the roadmap, for testing the level list.</summary>
        public bool StartOnRoadmap;

        Canvas _canvas;
        RectTransform _backdrop;
        RectTransform _screen;
        string _painted;

        void Start()
        {
            _canvas = Widgets.Screen("Menu", transform);

            // Its own layer, made before the screen so it is behind it, and rebuilt when
            // the screen changes. It used to be painted once here and never touched
            // again — so the front page's castle stood behind every screen in the game,
            // and a screen with its own painting drew over the castle rather than
            // instead of it.
            _backdrop = Widgets.Node("Backdrop", _canvas.transform);
            _backdrop.Fill();

            _screen = Widgets.Node("Screen", _canvas.transform);
            _screen.Fill();

            if (StartOnRoadmap) ShowRoadmap();
            else ShowMain();
        }

        /// <summary>
        /// The sky behind everything: night blue at the top falling to near-black, with
        /// a warm glow low down where the castle sits in the mock-up's painting.
        ///
        /// A painted backdrop is what the mock-up has and what this will eventually get.
        /// Until there is one, a flat black screen makes the frames and the gold read as
        /// floating rectangles; a gradient gives them a room to be in.
        /// </summary>
        /// <summary>
        /// The sky the menus stand on when there is no painting at all: night blue at the
        /// top falling to near-black, with a warm glow low down where the castle sits in
        /// the mock-up's painting.
        /// </summary>
        static void Sky(Transform parent)
        {

            var sky = Widgets.Panel("Backdrop", parent,
                Pixels.Gradient(new Color32(0x1B, 0x1F, 0x2A, 0xFF),
                                new Color32(0x0B, 0x09, 0x08, 0xFF), "ArnaSky"));
            sky.type = Image.Type.Sliced;
            sky.raycastTarget = false;
            sky.rectTransform.Fill();

            var glow = Widgets.Panel("Glow", parent,
                Pixels.Gradient(new Color(0.55f, 0.36f, 0.16f, 0.28f),
                                new Color(0.55f, 0.36f, 0.16f, 0f), "ArnaGlow"));
            glow.type = Image.Type.Sliced;
            glow.raycastTarget = false;
            glow.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            glow.rectTransform.anchorMax = new Vector2(1f, 0.62f);
            glow.rectTransform.offsetMin = Vector2.zero;
            glow.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Throws the current screen away and builds another in its place, on the
        /// painting that screen asked for.
        /// </summary>
        public void Show(Action<MenuShell, RectTransform> build, string backdrop = null)
        {
            Repaint(backdrop ?? Backdrops.Menu);

            for (int i = _screen.childCount - 1; i >= 0; i--)
                Destroy(_screen.GetChild(i).gameObject);

            build(this, _screen);
        }

        /// <summary>
        /// Puts the named painting behind the screen, or the nearest thing there is.
        ///
        /// Three deep, because a half-finished set of paintings should look like a game
        /// rather than like a bug: the one asked for, then the front page's, then the
        /// gradient sky that is drawn from nothing. A shop with no painting of its own
        /// standing on the castle is a shop that looks unfinished; standing on a black
        /// rectangle it looks broken.
        ///
        /// Skipped entirely when the same painting is already up, so walking in and out
        /// of the shop does not reload and re-crop it every time.
        /// </summary>
        void Repaint(string backdrop)
        {
            if (_painted == backdrop) return;

            _painted = backdrop;

            for (int i = _backdrop.childCount - 1; i >= 0; i--)
                Destroy(_backdrop.GetChild(i).gameObject);

            if (Backdrops.Paint(backdrop, _backdrop, 0.45f) != null) return;

            if (backdrop != Backdrops.Menu
                && Backdrops.Paint(Backdrops.Menu, _backdrop, 0.45f) != null) return;

            Sky(_backdrop);
        }

        public void ShowMain() => Show(MainMenuScreen.Build, Backdrops.Menu);
        public void ShowRoadmap() => Show(RoadmapScreen.Build, Backdrops.Menu);

        /// <summary>A screen for a section that is designed but not yet built.</summary>
        public void ShowStub(string title, string explanation, string backdrop = null)
            => Show((shell, root) => StubScreen.Build(shell, root, title, explanation), backdrop);

        /// <summary>
        /// Starts a level: the escort first, then the road.
        ///
        /// In that order because the two choices are one choice. Which troops you bring
        /// decides what a route costs — archers lose two fifths of a bowshot in a wood,
        /// cavalry half its charge in a bog — so picking the road before the escort is
        /// picking half a plan.
        /// </summary>
        public void Play(int chapter, int level)
        {
            Session.Choose(chapter, level);
            Session.Forget();
            Show(TroopScreen.Build);
        }

        /// <summary>Off to the planning map with the escort that was chosen.</summary>
        public void Draw(int chapter, int level)
        {
            Session.Choose(chapter, level);
            SceneManager.LoadScene(Session.PlanScene);
        }

        public void ShowTroops() => Show(TroopScreen.Build, Backdrops.Menu);
        public void ShowShop() => Show(ShopScreen.Build, Backdrops.Shop);

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
