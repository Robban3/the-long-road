using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TheVail.UI
{
    /// <summary>
    /// The few pieces every screen in this game is built from: a canvas, a framed panel,
    /// a label, a button, an icon and a row.
    ///
    /// Screens are assembled in code (see <see cref="Theme"/>), so this is the vocabulary
    /// that keeps them short. Anything a screen does more than once lives here instead,
    /// which is also what keeps the mock-up's proportions consistent between screens:
    /// there is one button height in the game and it is <see cref="ButtonHeight"/>.
    /// </summary>
    public static class Widgets
    {
        /// <summary>The design resolution. A tall phone, which is what the game is for.</summary>
        public static readonly Vector2 Reference = new Vector2(1080f, 1920f);

        /// <summary>
        /// The widest anything may be laid out, in design units.
        ///
        /// Narrower than the 1080 the design is drawn at, because the scaler below is
        /// driven by height: on a 20:9 phone the design is 1920 tall and only 864 wide,
        /// so a panel built at 1000 would have its edges off both sides of the screen.
        /// Everything is kept inside this and centred, and the wider the screen the more
        /// room there simply is either side.
        /// </summary>
        public const float SafeWidth = 840f;

        public const float ButtonHeight = 116f;
        public const float Margin = 48f;

        public const int TitleSize = 92;
        public const int HeadingSize = 46;
        public const int BodySize = 34;
        public const int SmallSize = 27;

        /// <summary>
        /// Builds a full-screen canvas, and the event system if the scene has none.
        ///
        /// Overlay rather than camera-space: these screens are drawn on top of whatever
        /// the game is doing and must not be sorted against the world, dimmed by fog or
        /// caught by the post-processing stack.
        /// </summary>
        public static Canvas Screen(string name, Transform parent = null, int order = 0)
        {
            var host = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) host.transform.SetParent(parent, false);

            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;

            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // Driven by height, not by a blend of the two, and this was a real bug rather
            // than a preference.
            //
            // At 0.5 the scale is the geometric mean of the two ratios, which on a 16:9
            // landscape Game view comes out at exactly 1: the canvas is then 1920 design
            // units wide and *1080 tall*, against a layout drawn for 1920 of height. Half
            // the screen was below the bottom edge, and everything anchored to that edge
            // came up the screen into the middle of everything anchored to the top. The
            // reported symptom was buttons lying on top of each other and a menu too big
            // to see, which is exactly what that produces.
            //
            // At 1 the design's full height always maps to the screen's, whatever shape
            // it is, so nothing is ever lost off the bottom. What varies instead is how
            // much width there is, and that is what SafeWidth is for.
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = Pixels.PixelsPerUnit;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem));

                // The project runs the new input system, whose module is a different type
                // from the old one. Added by name so this assembly does not have to
                // reference the input package for one component.
                var module = System.Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

                if (module != null) events.AddComponent(module);
                else events.AddComponent<StandaloneInputModule>();
            }

            return canvas;
        }

        /// <summary>An empty rect, for grouping and for laying children out against.</summary>
        public static RectTransform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);

            return (RectTransform)node.transform;
        }

        /// <summary>Fills the parent, inset by the given margins.</summary>
        public static RectTransform Fill(this RectTransform rect, float left = 0f, float top = 0f,
                                         float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);

            return rect;
        }

        /// <summary>
        /// Places a rect of a fixed size at a point, both given in the anchor's frame.
        /// </summary>
        public static RectTransform Place(this RectTransform rect, Vector2 anchor,
                                          Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            return rect;
        }

        /// <summary>A horizontal band: full width inside the margins, at a height from the top.</summary>
        public static RectTransform Band(this RectTransform rect, float fromTop, float height,
                                         float margin = Margin)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(margin, 0f);
            rect.offsetMax = new Vector2(-margin, 0f);
            rect.anchoredPosition = new Vector2(0f, -fromTop);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);

            return rect;
        }

        public static Image Panel(string name, Transform parent, Sprite sprite = null,
                                  Color? tint = null)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image))
                .GetComponent<Image>();
            image.transform.SetParent(parent, false);

            image.sprite = sprite != null ? sprite : Theme.Frame;
            image.type = image.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            image.color = tint ?? Color.white;

            return image;
        }

        public static Image Icon(string name, Transform parent, Sprite sprite, Color tint, float size)
        {
            var image = Panel(name, parent, sprite, tint);
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);

            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size,
                                 Color colour, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);

            label.font = Theme.Font;
            label.fontSize = size;
            label.color = colour;
            label.text = text;
            label.alignment = align;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return label;
        }

        /// <summary>
        /// Makes a label wrap inside its rect instead of running off the end of it.
        ///
        /// A method rather than a line of `label.horizontalOverflow = …` at each call
        /// site, and the reason is not tidiness. HorizontalWrapMode lives in UnityEngine
        /// and *not* in UnityEngine.UI, which is where anybody writing UI code reaches for
        /// it — a mistake that has now broken this project's build twice, and each time
        /// took the whole TheVail menu off the menu bar with it, because a broken UI assembly
        /// takes the editor assembly that references it. It is written here once and
        /// nowhere else, so there is one place left to get it wrong.
        /// </summary>
        public static Text Wrap(this Text label)
        {
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            return label;
        }

        /// <summary>
        /// A button in the mock-up's shape: framed plate, optional icon on the left, and
        /// a centred label.
        ///
        /// Disabled is a role rather than a flag so it reads the same in a layout as it
        /// does on screen — a locked entry is a different-looking thing, not a button
        /// that happens to be off.
        /// </summary>
        public static Button Plate(string name, Transform parent, string text, ButtonRole role,
                                   UnityAction clicked, Sprite icon = null)
        {
            var image = Panel(name, parent, Theme.Frame, Theme.Fill(role));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.15f, 1.12f, 1.05f, 1f);
            colours.pressedColor = new Color(0.78f, 0.76f, 0.72f, 1f);
            colours.selectedColor = Color.white;
            colours.disabledColor = new Color(0.65f, 0.63f, 0.6f, 1f);
            colours.fadeDuration = 0.06f;
            button.colors = colours;

            if (icon != null)
            {
                var glyph = Icon("Icon", image.transform, icon, Theme.BrightGold, 52f);
                glyph.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(34f, 0f),
                                          new Vector2(52f, 52f));
                glyph.rectTransform.pivot = new Vector2(0f, 0.5f);
            }

            var label = Label("Text", image.transform, text, BodySize, Theme.Ident(role));
            label.rectTransform.Fill(icon != null ? 96f : 24f, 0f, 24f, 0f);

            if (role == ButtonRole.Disabled)
            {
                button.interactable = false;
                return button;
            }

            if (clicked != null) button.onClick.AddListener(clicked);
            return button;
        }

        /// <summary>A small square button — back, pause, settings, close.</summary>
        public static Button Chip(string name, Transform parent, Sprite icon, UnityAction clicked,
                                  float size = 96f, Color? glyphTint = null)
        {
            var image = Panel(name, parent, Theme.Frame, Theme.Secondary);
            image.rectTransform.sizeDelta = new Vector2(size, size);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (clicked != null) button.onClick.AddListener(clicked);

            var glyph = Icon("Glyph", image.transform, icon, glyphTint ?? Theme.Parchment, size * 0.5f);
            glyph.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero,
                                      new Vector2(size * 0.5f, size * 0.5f));

            return button;
        }

        /// <summary>The red ribbon heading, as on LEVEL SELECT and VICTORY.</summary>
        public static Text Ribbon(string name, Transform parent, string text)
        {
            var banner = Panel(name, parent, Theme.Banner, Color.white);
            var label = Label("Text", banner.transform, text.ToUpperInvariant(), HeadingSize,
                              Theme.Parchment);
            label.rectTransform.Fill(40f, 0f, 40f, 0f);

            return label;
        }

        /// <summary>
        /// A counter chip: icon, number, and an optional plus button — the top bar's
        /// gold and gems.
        /// </summary>
        public static Text Counter(string name, Transform parent, Sprite icon, Color tint,
                                   string value, UnityAction plus = null, float width = 230f)
        {
            var chip = Panel(name, parent, Theme.SoftFrame, new Color(1f, 1f, 1f, 0.9f));
            chip.rectTransform.sizeDelta = new Vector2(width, 72f);

            var glyph = Icon("Glyph", chip.transform, icon, tint, 46f);
            glyph.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(46f, 46f));

            var label = Label("Value", chip.transform, value, BodySize, Theme.Parchment,
                              TextAnchor.MiddleLeft);
            label.rectTransform.Fill(70f, 0f, plus != null ? 74f : 14f, 0f);

            if (plus != null)
            {
                var add = Plate("Plus", chip.transform, "+", ButtonRole.Secondary, plus);
                add.image.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-8f, 0f),
                                              new Vector2(56f, 56f));
            }

            return label;
        }

        /// <summary>A row of stars, filled up to <paramref name="earned"/>.</summary>
        public static RectTransform Stars(string name, Transform parent, int earned, int of,
                                          float size, float gap = 6f)
        {
            var row = Node(name, parent);
            float step = size + gap;
            float start = -(of - 1) * step * 0.5f;

            for (int i = 0; i < of; i++)
            {
                bool won = i < earned;
                var star = Icon("Star" + i, row, Theme.Star,
                                won ? Theme.BrightGold : new Color(0f, 0f, 0f, 0.45f), size);

                // The middle star sits a little proud, as on the mock-up's medallions.
                float lift = of == 3 && i == 1 ? size * 0.18f : 0f;
                star.rectTransform.Place(new Vector2(0.5f, 0.5f),
                                         new Vector2(start + i * step, lift),
                                         new Vector2(size, size));
            }

            return row;
        }

        /// <summary>A dimming sheet that also swallows taps meant for what is behind it.</summary>
        public static Image Scrim(string name, Transform parent, float opacity = 0.78f)
        {
            var image = Panel(name, parent, Theme.Flat, new Color(0f, 0f, 0f, opacity));
            image.type = Image.Type.Simple;
            image.rectTransform.Fill();

            return image;
        }
    }
}
