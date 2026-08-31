using Arna.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Arna.UI
{
    /// <summary>
    /// The level roadmap: ten levels on a winding path, with the stars each one has been
    /// beaten for.
    ///
    /// This is the screen that makes a *campaign* out of what was a demo. Until now a
    /// level was a chapter and level number typed into the Inspector, played once and
    /// forgotten — nothing recorded that it had happened, so there was nowhere to go
    /// back to and no reason to. A beaten level here stays open forever and keeps its
    /// best score, and the next chapter is opened by stars rather than by simply
    /// arriving, so going back to a scrappy one-star win is a real move and not
    /// housekeeping.
    ///
    /// Levels run down the page from 1, which is the way the mock-up reads them.
    /// </summary>
    public static class RoadmapScreen
    {
        const float NodeSize = 160f;
        const float Step = 260f;
        const float Swing = 300f;
        const float TopPad = 120f;
        const float NavHeight = 170f;

        /// <summary>The chapter tab currently open. Sticky across a rebuild.</summary>
        static int _shown;

        public static void Build(MenuShell shell, RectTransform root)
        {
            var campaign = Session.Campaign;

            if (_shown < 1)
            {
                campaign.Furthest(out int chapter, out _);
                _shown = chapter;
            }

            if (!campaign.ChapterOpen(_shown)) _shown = 1;

            TopBar(shell, root, campaign);
            Header(shell, root, campaign);
            Path(shell, root, campaign);
            Nav(shell, root);
        }

        static void TopBar(MenuShell shell, RectTransform root, Campaign campaign)
        {
            var back = Widgets.Chip("Back", root, Theme.Chevron, shell.ShowMain);
            back.image.rectTransform.Place(new Vector2(0f, 1f),
                new Vector2(Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            var purse = Widgets.Node("Purse", root);
            purse.Place(new Vector2(1f, 1f), new Vector2(-Widgets.Margin, -Widgets.Margin),
                        new Vector2(500f, 96f));

            var gold = Widgets.Counter("Gold", purse, Theme.CoinIcon, Theme.Coin,
                campaign.Gold.ToString(), () => shell.ShowStub("Butik", "Inget säljs ännu."), 250f);
            gold.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(1f, 0.5f), new Vector2(-260f, 0f), new Vector2(250f, 72f));

            var gems = Widgets.Counter("Gems", purse, Theme.GemIcon, Theme.Gem,
                campaign.Gems.ToString(), () => shell.ShowStub("Butik", "Inget säljs ännu."), 250f);
            gems.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(250f, 72f));
        }

        static void Header(MenuShell shell, RectTransform root, Campaign campaign)
        {
            var ribbon = Widgets.Ribbon("Ribbon", root, "Välj nivå");
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(720f, 100f));

            // One tab per chapter the player has reached, plus the next one — so the gate
            // that opens it is visible, rather than a chapter that simply is not there.
            int tabs = Mathf.Clamp(campaign.HighestChapter + 1, 2, 4);

            var row = Widgets.Node("Chapters", root);
            row.Place(new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(940f, 96f));

            // Narrower as they multiply, so four tabs still fit the width they have.
            float width = Mathf.Min(360f, (940f - (tabs - 1) * 16f) / tabs);
            float start = -(tabs - 1) * (width + 16f) * 0.5f;

            for (int chapter = 1; chapter <= tabs; chapter++)
            {
                bool open = campaign.ChapterOpen(chapter);
                bool here = chapter == _shown;
                int number = chapter;

                var role = !open ? ButtonRole.Disabled
                         : here ? ButtonRole.Primary : ButtonRole.Secondary;

                var tab = Widgets.Plate("Chapter" + chapter, row, "KAPITEL " + chapter, role,
                    () => { _shown = number; shell.ShowRoadmap(); },
                    open ? null : Theme.Padlock);

                tab.image.rectTransform.Place(new Vector2(0.5f, 0.5f),
                    new Vector2(start + (chapter - 1) * (width + 16f), 0f), new Vector2(width, 92f));
            }

            var name = Widgets.Label("ChapterName", root, ChapterName(_shown), Widgets.SmallSize,
                                     Theme.Muted);
            name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(900f, 48f));

            if (campaign.ChapterOpen(_shown)) return;

            var locked = Widgets.Label("Gate", root,
                $"Behöver {Campaign.StarsToOpenNextChapter} stjärnor i kapitel {_shown - 1}" +
                $" — du har {campaign.StarsIn(_shown - 1)}", Widgets.SmallSize, Theme.Danger);
            locked.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -462f), new Vector2(900f, 48f));
        }

        /// <summary>
        /// Chapter names, until there is a content asset to read them from. The first is
        /// the one the mock-up names; the rest follow the road east.
        /// </summary>
        static string ChapterName(int chapter)
        {
            switch (chapter)
            {
                case 1: return "GRÄNSLANDEN";
                case 2: return "KUNGSVÄGEN";
                case 3: return "DE VÅTA MARKERNA";
                default: return "KAPITEL " + chapter;
            }
        }

        /// <summary>Builds the scrolling path of levels for the open chapter.</summary>
        static void Path(MenuShell shell, RectTransform root, Campaign campaign)
        {
            var frame = Widgets.Node("Levels", root);
            frame.anchorMin = new Vector2(0f, 0f);
            frame.anchorMax = new Vector2(1f, 1f);
            frame.offsetMin = new Vector2(Widgets.Margin, NavHeight + 24f);
            frame.offsetMax = new Vector2(-Widgets.Margin, -500f);

            var board = Widgets.Panel("Board", frame, Theme.Frame,
                                      new Color(0.14f, 0.16f, 0.12f, 1f));
            board.rectTransform.Fill();

            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            var viewport = Widgets.Node("Viewport", frame);
            viewport.Fill(10f, 10f, 10f, 10f);
            viewport.gameObject.AddComponent<RectMask2D>();

            // A ScrollRect only sees a drag that lands on a graphic; an invisible sheet
            // over the viewport is what makes the whole board draggable rather than only
            // the level medallions.
            var catcher = Widgets.Panel("Catcher", viewport, Theme.Flat, new Color(0f, 0f, 0f, 0.002f));
            catcher.type = Image.Type.Simple;
            catcher.rectTransform.Fill();

            var content = Widgets.Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            content.sizeDelta = new Vector2(0f, TopPad * 2f + Step * (Campaign.LevelsPerChapter - 1));

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 40f;

            for (int level = 1; level < Campaign.LevelsPerChapter; level++)
                Stones(content, Spot(level), Spot(level + 1));

            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                Node(shell, content, campaign, level);

            campaign.Furthest(out int _, out int next);
            float scrolled = Mathf.Clamp(TopPad + (next - 2) * Step, 0f,
                                         Mathf.Max(0f, content.sizeDelta.y - frame.rect.height));
            content.anchoredPosition = new Vector2(0f, scrolled);
        }

        /// <summary>Where a level sits on the board, in content coordinates.</summary>
        static Vector2 Spot(int level)
            => new Vector2(Mathf.Sin((level - 1) * 1.15f) * Swing, -(TopPad + (level - 1) * Step));

        /// <summary>Lays the paving stones between two levels.</summary>
        static void Stones(RectTransform content, Vector2 from, Vector2 to)
        {
            const int stones = 5;
            var mid = (from + to) * 0.5f + new Vector2((to.x - from.x) * -0.25f, 0f);

            for (int i = 1; i <= stones; i++)
            {
                float t = i / (stones + 1f);

                // Quadratic through the offset midpoint, so the path bows between the
                // medallions instead of running straight between them.
                var point = Mathf.Pow(1f - t, 2f) * from + 2f * (1f - t) * t * mid + t * t * to;
                var ahead = Mathf.Pow(1f - (t + 0.05f), 2f) * from
                          + 2f * (1f - (t + 0.05f)) * (t + 0.05f) * mid
                          + (t + 0.05f) * (t + 0.05f) * to;

                var slab = Widgets.Icon("Stone", content, Theme.Slab,
                                        new Color(1f, 1f, 1f, 0.55f), 56f);
                slab.rectTransform.Place(new Vector2(0.5f, 1f), point, new Vector2(70f, 46f));

                var step = ahead - point;
                float angle = Mathf.Atan2(step.y, step.x) * Mathf.Rad2Deg;
                slab.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
            }
        }

        /// <summary>One level medallion: number, stars, or a padlock.</summary>
        static void Node(MenuShell shell, RectTransform content, Campaign campaign, int level)
        {
            bool open = campaign.Unlocked(_shown, level);
            int stars = campaign.Stars(_shown, level);
            int chapter = _shown;

            var medallion = Widgets.Panel("Level" + level, content, Theme.Round,
                open ? Color.white : new Color(0.42f, 0.40f, 0.38f, 1f));
            medallion.rectTransform.Place(new Vector2(0.5f, 1f), Spot(level),
                                          new Vector2(NodeSize, NodeSize));

            if (open)
            {
                var button = medallion.gameObject.AddComponent<Button>();
                button.targetGraphic = medallion;
                button.onClick.AddListener(() => shell.Play(chapter, level));

                var number = Widgets.Label("Number", medallion.transform, level.ToString(),
                                           Widgets.HeadingSize + 12, Theme.BrightGold);
                number.rectTransform.Fill();

                var row = Widgets.Stars("Stars", medallion.transform, stars, Campaign.MaxStars, 44f, 4f);
                row.Place(new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(160f, 44f));
            }
            else
            {
                var padlock = Widgets.Icon("Lock", medallion.transform, Theme.Padlock,
                                           new Color(0.75f, 0.72f, 0.68f, 1f), 72f);
                padlock.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            }

            // The level being played next is worth pointing at: on a board of ten
            // medallions the eye needs somewhere to start.
            campaign.Furthest(out int atChapter, out int atLevel);
            if (!open || atChapter != chapter || atLevel != level) return;

            var halo = Widgets.Icon("Halo", medallion.transform, Theme.Round,
                                    new Color(Theme.BrightGold.r, Theme.BrightGold.g, Theme.BrightGold.b, 0.25f),
                                    NodeSize + 34f);
            halo.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero,
                                     new Vector2(NodeSize + 34f, NodeSize + 34f));
            halo.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// The bottom bar from the mock-up. Strid is where the player is; the rest name
        /// the sections the design expects and say so when pressed.
        /// </summary>
        static void Nav(MenuShell shell, RectTransform root)
        {
            var bar = Widgets.Panel("Nav", root, Theme.Frame, Theme.Backdrop);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.offsetMin = new Vector2(0f, 0f);
            bar.rectTransform.offsetMax = new Vector2(0f, 0f);
            bar.rectTransform.sizeDelta = new Vector2(0f, NavHeight);

            Tab(shell, bar.transform, -420f, "BUTIK", Theme.CoinIcon, false, "Inget säljs ännu.");
            Tab(shell, bar.transform, -210f, "TRUPPER", Theme.HeartIcon, false, null,
                shell.ShowTroops);
            Tab(shell, bar.transform, 0f, "STRID", Theme.Star, true, null);
            Tab(shell, bar.transform, 210f, "SMEDJA", Theme.GemIcon, false,
                "Uppgraderingar köps i dag med silver mitt i ett uppdrag.");
            Tab(shell, bar.transform, 420f, "KARTA", Theme.SkullIcon, false,
                "Världskartan över kapitlen är inte byggd.");
        }

        static void Tab(MenuShell shell, Transform bar, float x, string text, Sprite icon,
                        bool here, string explanation,
                        UnityEngine.Events.UnityAction goes = null)
        {
            var slot = Widgets.Panel("Tab" + text, bar, here ? Theme.Frame : Theme.Flat,
                                     here ? Theme.Primary : new Color(0f, 0f, 0f, 0f));
            slot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(x, here ? 12f : 0f),
                                     new Vector2(196f, here ? 168f : 150f));

            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;

            if (goes != null) button.onClick.AddListener(goes);
            else if (!here) button.onClick.AddListener(() => shell.ShowStub(text, explanation));

            var glyph = Widgets.Icon("Glyph", slot.transform, icon,
                                     here ? Theme.BrightGold : Theme.Muted, 64f);
            glyph.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(64f, 64f));

            var label = Widgets.Label("Text", slot.transform, text, Widgets.SmallSize,
                                      here ? Theme.Parchment : Theme.Muted);
            label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(190f, 42f));
        }
    }
}
