using UnityEngine;

namespace Arna.UI
{
    /// <summary>
    /// The screen behind a menu entry that is designed but not built.
    ///
    /// It says which part of the game is missing and, where the work exists somewhere
    /// else already, where it currently lives. That is more useful than a dead button
    /// and considerably more honest than a button that silently does nothing.
    /// </summary>
    public static class StubScreen
    {
        public static void Build(MenuShell shell, RectTransform root, string title, string explanation)
            => Build(shell, root, title, explanation, null);

        public static void Build(MenuShell shell, RectTransform root, string title,
                                 string explanation, string backdrop)
        {
            // A section that is designed and not built can at least look like the game it
            // belongs to. The shop has a painting long before it has anything to sell.
            if (backdrop != null) Backdrops.Paint(backdrop, root, 0.55f);

            var back = Widgets.Chip("Back", root, Theme.Chevron, shell.ShowMain);
            back.image.rectTransform.Place(new Vector2(0f, 1f),
                new Vector2(Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            var panel = Widgets.Panel("Panel", root, Theme.Frame, Color.white);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Widgets.SafeWidth, 620f));

            var ribbon = Widgets.Ribbon("Ribbon", panel.transform, title);
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(Widgets.SafeWidth - 220f, 100f));

            var body = Widgets.Label("Body", panel.transform, explanation, Widgets.BodySize, Theme.Muted);
            body.Wrap();
            body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(Widgets.SafeWidth - 100f, 260f));

            var ok = Widgets.Plate("Back", panel.transform, "TILLBAKA", ButtonRole.Primary, shell.ShowMain);
            ok.image.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 44f),
                                         new Vector2(Widgets.SafeWidth - 320f, Widgets.ButtonHeight));
        }
    }
}
