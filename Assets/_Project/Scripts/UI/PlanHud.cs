using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arna.UI
{
    /// <summary>
    /// The planning screen's furniture: which level this is, what the drawn road costs,
    /// and the button that walks it.
    ///
    /// The numbers come from the route solver through <see cref="Show"/> rather than
    /// being read here, so this assembly stays clear of the generator and the planner —
    /// and so the panel says exactly what the solver said, with no second opinion about
    /// what a road is worth.
    ///
    /// What it deliberately never shows is what is *out there*. Enemies are bought with
    /// the eagle or paid for in blood (docs/GDD.md §3.4); a risk readout that knew where
    /// the wolves were would hand the level over.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlanHud : MonoBehaviour
    {
        /// <summary>Walks the drawn road. Set by the drawing component.</summary>
        public Action Play;

        /// <summary>Takes the last waypoint back.</summary>
        public Action Undo;

        /// <summary>The level named in the banner. Set before or after the canvas is built.</summary>
        public int Chapter = 1;
        public int Level = 1;

        RectTransform _screen;
        Text _title, _points, _time, _terrain, _cover, _fords, _warning;
        Button _play;

        void Start()
        {
            var canvas = Widgets.Screen("PlanHud", transform);
            _screen = Widgets.Node("Screen", canvas.transform);
            _screen.Fill();

            TopBar();
            Panel();
            Footer();

            SetLevel(Chapter, Level);
        }

        void TopBar()
        {
            var back = Widgets.Chip("Back", _screen, Theme.Chevron,
                () => SceneManager.LoadScene(Session.MenuScene));
            back.image.rectTransform.Place(new Vector2(0f, 1f),
                new Vector2(Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            var plate = Widgets.Panel("Title", _screen, Theme.Banner, Color.white);
            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -Widgets.Margin),
                                      new Vector2(520f, 96f));

            _title = Widgets.Label("Text", plate.transform, "", Widgets.HeadingSize - 6, Theme.Parchment);
            _title.rectTransform.Fill(40f, 0f, 40f, 0f);

            var gold = Widgets.Counter("Gold", _screen, Theme.CoinIcon, Theme.Coin,
                                       Session.Campaign.Gold.ToString(), null, 230f);
            gold.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(1f, 1f), new Vector2(-Widgets.Margin, -Widgets.Margin),
                       new Vector2(230f, 76f));
        }

        /// <summary>The road's own numbers, down the right-hand side.</summary>
        void Panel()
        {
            var panel = Widgets.Panel("Route", _screen, Theme.Frame, Color.white);
            panel.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-Widgets.Margin, -190f),
                                      new Vector2(460f, 470f));

            var heading = Widgets.Ribbon("Ribbon", panel.transform, "Din väg");
            heading.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(360f, 84f));

            float y = -100f;
            _points = Line(panel.transform, ref y);
            _time = Line(panel.transform, ref y);
            _terrain = Line(panel.transform, ref y);
            _cover = Line(panel.transform, ref y);
            _fords = Line(panel.transform, ref y);
            _warning = Line(panel.transform, ref y);
            _warning.color = Theme.Danger;
        }

        static Text Line(Transform panel, ref float y)
        {
            var label = Widgets.Label("Line", panel, "", Widgets.SmallSize, Theme.Muted,
                                      TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(404f, 56f));
            y -= 58f;

            return label;
        }

        void Footer()
        {
            var hint = Widgets.Label("Hint", _screen,
                "Tryck på kartan för att lägga ut vägpunkter. Dra för att flytta.",
                Widgets.SmallSize, Theme.Muted);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 200f),
                                     new Vector2(900f, 44f));

            var undo = Widgets.Plate("Undo", _screen, "ÅNGRA", ButtonRole.Secondary,
                                     () => Undo?.Invoke());
            undo.image.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(-300f, Widgets.Margin),
                                           new Vector2(280f, Widgets.ButtonHeight));

            _play = Widgets.Plate("Play", _screen, "SPELA DENNA VÄG", ButtonRole.Primary,
                                  () => Play?.Invoke());
            _play.image.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(120f, Widgets.Margin),
                                            new Vector2(560f, Widgets.ButtonHeight));
        }

        public void SetLevel(int chapter, int level)
        {
            Chapter = chapter;
            Level = level;

            if (_title != null) _title.text = $"KAPITEL {chapter}  ·  NIVÅ {level}";
        }

        /// <summary>
        /// Fills the panel in from a solved route. Everything is passed in because the
        /// menu assembly does not know what a RouteResult is, and should not.
        /// </summary>
        public void Show(int waypoints, int maxWaypoints, bool valid, int failedLeg,
                         float seconds, float forest, float marsh, float road,
                         float exposure, int fords, int detours)
        {
            if (_points == null) return;

            _points.text = $"Vägpunkter  {waypoints} av {maxWaypoints}";

            if (!valid)
            {
                _time.text = "Ingen framkomlig väg.";
                _terrain.text = $"Etapp {failedLeg + 1} går inte att gå.";
                _cover.text = "";
                _fords.text = "";
                _warning.text = "Flytta punkten till fastare mark.";
            }
            else
            {
                _time.text = $"Restid  {seconds:F0} s";
                _terrain.text = $"Skog {forest:P0}   träsk {marsh:P0}   väg {road:P0}";
                _cover.text = $"Skydd åt ett bakhåll  {exposure:F2}";
                _fords.text = $"Vadställen  {fords}";
                _warning.text = detours > 0 ? $"{detours} etapp(er) går långt runt." : "";
            }

            if (_play == null) return;

            _play.interactable = valid;
            _play.image.color = Theme.Fill(valid ? ButtonRole.Primary : ButtonRole.Disabled);
        }
    }
}
