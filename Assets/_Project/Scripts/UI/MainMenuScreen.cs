using UnityEngine;
using UnityEngine.UI;

namespace TheVeil.UI
{
    /// <summary>
    /// The front page: the title, the five ways in, and the three chips along the bottom.
    ///
    /// Only two of them lead anywhere yet — Spela to the roadmap, Inställningar to the
    /// save. The rest are drawn in full and say so when pressed, which is a deliberate
    /// choice over hiding them: the shape of the menu is part of the design being agreed,
    /// and a screen that shows three buttons today and eight later is a different screen
    /// to judge.
    /// </summary>
    public static class MainMenuScreen
    {
        public static void Build(MenuShell shell, RectTransform root)
        {
            var gear = Widgets.Chip("Settings", root, Theme.Gear,
                () => shell.ShowSettings());
            gear.image.rectTransform.Place(new Vector2(1f, 1f),
                new Vector2(-Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            Title(root);
            Choices(shell, root);
            Chips(shell, root);
        }

        static void Title(RectTransform root)
        {
            var block = Widgets.Node("Title", root);
            block.Place(new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(Widgets.SafeWidth, 340f));

            var above = Widgets.Label("Above", block, "THE", 44, Theme.Muted);
            above.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(Widgets.SafeWidth, 60f));

            var name = Widgets.Label("Name", block, "LONG ROAD", Widgets.TitleSize, Theme.BrightGold);
            name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(Widgets.SafeWidth, 130f));

            var rule = Widgets.Panel("Rule", block, Theme.Flat, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.55f));
            rule.type = Image.Type.Simple;
            rule.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(480f, 3f));

            var under = Widgets.Label("Under", block, "LEGACY OF THE VEIL", 38, Theme.Muted);
            under.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(Widgets.SafeWidth, 56f));
        }

        static void Choices(MenuShell shell, RectTransform root)
        {
            var column = Widgets.Node("Choices", root);
            column.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(Widgets.SafeWidth - 80f, 700f));

            float y = 0f;
            float step = Widgets.ButtonHeight + 24f;

            Entry(column, ref y, step, Loc.T("PLAY"), ButtonRole.Primary, () => shell.ShowRoadmap());

            Entry(column, ref y, step, Loc.T("UPGRADE"), ButtonRole.Secondary,
                  () => shell.ShowStub(Loc.T("Upgrade"),
                      Loc.T("Troops are upgraded with silver in the middle of a mission. This is where the gold between missions is meant to be spent.")));

            Entry(column, ref y, step, Loc.T("SHOP"), ButtonRole.Secondary,
                  shell.ShowShop);

            Entry(column, ref y, step, Loc.T("ACHIEVEMENTS"), ButtonRole.Secondary,
                  () => shell.ShowStub(Loc.T("Achievements"), Loc.T("No achievements have been written yet.")));

            Entry(column, ref y, step, Loc.T("SETTINGS"), ButtonRole.Secondary,
                  () => shell.ShowSettings());
        }

        static void Entry(RectTransform column, ref float y, float step, string text,
                          ButtonRole role, UnityEngine.Events.UnityAction clicked)
        {
            var button = Widgets.Plate(text, column, text, role, clicked);
            button.image.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -y),
                                             new Vector2(Widgets.SafeWidth - 80f, Widgets.ButtonHeight));
            y += step;
        }

        /// <summary>The three chips along the bottom, badge and all.</summary>
        static void Chips(MenuShell shell, RectTransform root)
        {
            var row = Widgets.Node("Chips", root);
            row.Place(new Vector2(0.5f, 0f), new Vector2(0f, Widgets.Margin), new Vector2(Widgets.SafeWidth, 210f));

            Chip(shell, row, -272f, Loc.T("DAILY\nREWARD"), Theme.CoinIcon, "!",
                 Loc.T("Come back tomorrow for gold. Not built yet."));
            Chip(shell, row, 0f, Loc.T("QUESTS"), Theme.Star, "2",
                 Loc.T("Daily quests will give gold and gems. Not built yet."));
            Chip(shell, row, 272f, Loc.T("LEADERBOARD"), Theme.SkullIcon, null,
                 Loc.T("The leaderboard needs a server. Not built yet."));
        }

        static void Chip(MenuShell shell, RectTransform row, float x, string text, Sprite icon,
                         string badge, string explanation)
        {
            var plate = Widgets.Panel(text, row, Theme.Frame, Theme.Secondary);
            plate.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(x, 0f), new Vector2(256f, 200f));

            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(() => shell.ShowStub(text.Replace("\n", " "), explanation));

            var glyph = Widgets.Icon("Glyph", plate.transform, icon, Theme.BrightGold, 76f);
            glyph.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(76f, 76f));

            var label = Widgets.Label("Text", plate.transform, text, Widgets.SmallSize, Theme.Muted);
            label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(240f, 74f));

            if (badge == null) return;

            var dot = Widgets.Panel("Badge", plate.transform, Theme.Round, Theme.Danger);
            dot.rectTransform.Place(new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(56f, 56f));

            var count = Widgets.Label("Count", dot.transform, badge, Widgets.SmallSize, Color.white);
            count.rectTransform.Fill();
        }
    }
}
