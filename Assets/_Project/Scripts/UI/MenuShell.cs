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

            // The shop, when the result screen asked for it on the way out. Read once and
            // cleared, so pressing Back lands on the front page like any other visit.
            if (Session.OpenShop)
            {
                Session.OpenShop = false;
                ShowShop();
            }
            else if (StartOnRoadmap) ShowRoadmap();
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
            // Naming no painting means *keep the one that is up*, and that is the whole
            // point of the default.
            //
            // Every plain Show(Build) in this project is a screen rebuilding itself in
            // place — a shop tab changing, a boon bought, a troop picked. Defaulting
            // those to the front page's castle meant the shop painted the smithy on the
            // way in and threw it away the moment you touched a tab, which is exactly
            // what "switching tab goes back to the old picture" is. A screen that wants a
            // particular painting says so when it is opened; a rebuild says nothing and
            // keeps what it had.
            Repaint(backdrop ?? _painted ?? Backdrops.Menu);

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

            // The sky goes down first every time, painting or no painting. A painting is
            // held to a phone-shaped column (see Backdrops.Paint), so on a window wider
            // than a phone there is canvas either side of it, and the game's own night
            // sky is a better thing to find there than black.
            Sky(_backdrop);

            if (Backdrops.Paint(backdrop, _backdrop, 0.45f) != null) return;

            bool substituted = backdrop != Backdrops.Menu
                               && Backdrops.Paint(Backdrops.Menu, _backdrop, 0.45f) != null;

            Missing(backdrop, substituted);
        }

        /// <summary>
        /// Says on the screen itself which painting is missing.
        ///
        /// The console has said this for three rounds and the answer kept not arriving,
        /// because a fallback that looks deliberate is indistinguishable from a fallback
        /// that is broken — "the shop still shows the castle" is exactly what a missing
        /// shop painting looks like *and* exactly what a bug in the swapping would look
        /// like. Putting the sentence where the person is looking ends that.
        ///
        /// Editor only. A player must never see a filename.
        /// </summary>
        void Missing(string backdrop, bool substituted)
        {
#if UNITY_EDITOR
            var note = Widgets.Label("Missing", _backdrop,
                $"{backdrop}.png hittades inte — {Backdrops.Inventory()}",
                Widgets.SmallSize - 8, new Color(1f, 0.85f, 0.55f, 0.85f));

            note.Wrap();
            note.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                                     new Vector2(Widgets.SafeWidth, 64f));
#endif
        }

        public void ShowMain() => Show(MainMenuScreen.Build, Backdrops.Menu);
        public void ShowRoadmap() => Show(RoadmapScreen.Build, Backdrops.Menu);

        /// <summary>A screen for a section that is designed but not yet built.</summary>
        public void ShowStub(string title, string explanation, string backdrop = null)
            => Show((shell, root) => StubScreen.Build(shell, root, title, explanation),
                    backdrop ?? Backdrops.Menu);

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
            Show(TroopScreen.Build, Backdrops.Menu);
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
