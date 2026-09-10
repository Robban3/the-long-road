using UnityEngine;
using UnityEngine.UI;

namespace TheVeil.UI
{
    /// <summary>
    /// The front page: the title and the four ways in — play, the shop, the achievements
    /// and the settings.
    ///
    /// There were eight, and five of them led to a page saying they were not built: an
    /// upgrade screen that was the shop again, a daily reward, quests and a leaderboard.
    /// Drawing them in full was right while the shape of the menu was being agreed; in
    /// front of anybody playing, a button that goes nowhere reads as broken. They come
    /// back when there is something behind them — StubScreen is still there for that.
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

            Entry(column, ref y, step, Loc.T("SHOP"), ButtonRole.Secondary,
                  shell.ShowShop);

            var feats = Entry(column, ref y, step, Loc.T("ACHIEVEMENTS"), ButtonRole.Secondary,
                              shell.ShowAchievements);

            // A reward waiting to be collected is worth a mark on the way in, or it waits
            // for a player who has no reason to look.
            if (Session.Campaign.AnythingToClaim) Badge(feats.transform);

            Entry(column, ref y, step, Loc.T("SETTINGS"), ButtonRole.Secondary,
                  () => shell.ShowSettings());
        }

        static Button Entry(RectTransform column, ref float y, float step, string text,
                            ButtonRole role, UnityEngine.Events.UnityAction clicked)
        {
            var button = Widgets.Plate(text, column, text, role, clicked);
            button.image.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -y),
                                             new Vector2(Widgets.SafeWidth - 80f, Widgets.ButtonHeight));
            y += step;
            return button;
        }

        /// <summary>A red mark on a button: something behind it is waiting for the player.</summary>
        static void Badge(Transform on)
        {
            var dot = Widgets.Panel("Badge", on, Theme.Round, Theme.Danger);
            dot.rectTransform.Place(new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(48f, 48f));
            dot.raycastTarget = false;

            var mark = Widgets.Label("Mark", dot.transform, "!", Widgets.SmallSize, Color.white);
            mark.rectTransform.Fill();
            mark.raycastTarget = false;
        }

    }
}
