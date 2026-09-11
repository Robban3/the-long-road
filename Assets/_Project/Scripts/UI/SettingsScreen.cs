using UnityEngine;

namespace TheVeil.UI
{
    /// <summary>
    /// The settings: which language, whether there is sound, and starting again.
    ///
    /// Three sections and no more. Graphics options are left out until there is something
    /// to choose between — a quality slider that changes nothing on a phone that runs the
    /// game the same way at every setting is a control that lies.
    /// </summary>
    public static class SettingsScreen
    {
        /// <summary>
        /// Whether the erase button has been pressed once and is waiting for the second
        /// press. Cleared whenever the screen is opened fresh, so a half-finished erase is
        /// never still armed the next time somebody comes in to change the language.
        /// </summary>
        static bool _erasing;

        public static void Open(MenuShell shell)
        {
            _erasing = false;
            shell.Show(Build, Backdrops.Menu);
        }

        public static void Build(MenuShell shell, RectTransform root)
        {
            var back = Widgets.Chip("Back", root, Theme.Chevron, shell.ShowMain);
            back.image.rectTransform.Place(new Vector2(0f, 1f),
                new Vector2(Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            var ribbon = Widgets.Ribbon("Ribbon", root, Loc.T("Settings"));
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -Widgets.Margin),
                       new Vector2(520f, 100f));

            float y = -220f;

            Heading(root, ref y, Loc.T("LANGUAGE"));

            // Each language in its own words, never translated: a player who has landed in
            // a language they cannot read has to be able to find their own on this screen.
            Pair(shell, root, ref y,
                 "ENGLISH", Loc.Language == Language.English, () => Choose(shell, Language.English),
                 "SVENSKA", Loc.Language == Language.Swedish, () => Choose(shell, Language.Swedish));

            Heading(root, ref y, Loc.T("SOUND"));

            Pair(shell, root, ref y,
                 Loc.T("ON"), GameSettings.Sound, () => { GameSettings.Sound = true; shell.Show(Build); },
                 Loc.T("OFF"), !GameSettings.Sound, () => { GameSettings.Sound = false; shell.Show(Build); });

            // Only where somebody is showing the game: the editor and development builds.
            if (GameSettings.DemoAvailable)
            {
                Heading(root, ref y, Loc.T("DEMO: EVERY CHAPTER AND LEVEL OPEN"));

                Pair(shell, root, ref y,
                     Loc.T("ON"), GameSettings.Demo, () => { GameSettings.Demo = true; shell.Show(Build); },
                     Loc.T("OFF"), !GameSettings.Demo, () => { GameSettings.Demo = false; shell.Show(Build); });
            }

            Heading(root, ref y, Loc.T("PROGRESS"));

            // Asked twice, as Session.Wipe promises. The second press is a different button
            // in a different colour saying what is about to happen, rather than the same
            // button pressed again — which is how a double tap erases a campaign.
            var erase = Widgets.Plate("Erase", root,
                _erasing ? Loc.T("TAP AGAIN TO ERASE EVERYTHING") : Loc.T("RESET PROGRESS"),
                _erasing ? ButtonRole.Exit : ButtonRole.Secondary,
                () =>
                {
                    if (!_erasing) { _erasing = true; shell.Show(Build); return; }

                    _erasing = false;
                    Session.Wipe();
                    shell.ShowMain();
                });

            erase.image.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y),
                                            new Vector2(Widgets.SafeWidth - 80f, Widgets.ButtonHeight));
            y -= Widgets.ButtonHeight + 16f;

            var warning = Widgets.Label("Warning", root,
                Loc.T("Stars, gold and everything bought are gone for good."),
                Widgets.SmallSize - 4, _erasing ? Theme.Danger : Theme.Dim);
            warning.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y),
                                        new Vector2(Widgets.SafeWidth, 44f));
        }

        static void Choose(MenuShell shell, Language language)
        {
            Loc.Language = language;
            shell.Show(Build);
        }

        static void Heading(RectTransform root, ref float y, string text)
        {
            var label = Widgets.Label("Heading", root, text, Widgets.SmallSize - 2, Theme.Gold);
            label.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y),
                                      new Vector2(Widgets.SafeWidth, 44f));
            y -= 60f;
        }

        /// <summary>Two buttons side by side, the chosen one lit.</summary>
        static void Pair(MenuShell shell, RectTransform root, ref float y,
                         string left, bool leftChosen, UnityEngine.Events.UnityAction pickLeft,
                         string right, bool rightChosen, UnityEngine.Events.UnityAction pickRight)
        {
            float width = (Widgets.SafeWidth - 80f - 24f) * 0.5f;

            var a = Widgets.Plate(left, root, left,
                                  leftChosen ? ButtonRole.Primary : ButtonRole.Secondary, pickLeft);
            a.image.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(-(width + 24f) * 0.5f, y),
                                        new Vector2(width, Widgets.ButtonHeight));

            var b = Widgets.Plate(right, root, right,
                                  rightChosen ? ButtonRole.Primary : ButtonRole.Secondary, pickRight);
            b.image.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2((width + 24f) * 0.5f, y),
                                        new Vector2(width, Widgets.ButtonHeight));

            y -= Widgets.ButtonHeight + 44f;
        }
    }
}
