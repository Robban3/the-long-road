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
        {
            var back = Widgets.Chip("Back", root, Theme.Chevron, shell.ShowMain);
            back.image.rectTransform.Place(new Vector2(0f, 1f),
                new Vector2(Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            var panel = Widgets.Panel("Panel", root, Theme.Frame, Color.white);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 620f));

            var ribbon = Widgets.Ribbon("Ribbon", panel.transform, title);
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(660f, 100f));

            var body = Widgets.Label("Body", panel.transform, explanation, Widgets.BodySize, Theme.Muted);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(740f, 260f));

            var ok = Widgets.Plate("Back", panel.transform, "TILLBAKA", ButtonRole.Primary, shell.ShowMain);
            ok.image.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 44f),
                                         new Vector2(520f, Widgets.ButtonHeight));
        }
    }
}
