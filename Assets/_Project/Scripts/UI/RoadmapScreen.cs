using System.Collections.Generic;
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
                        new Vector2(480f, 96f));

            var gold = Widgets.Counter("Gold", purse, Theme.CoinIcon, Theme.Coin,
                campaign.Gold.ToString(), shell.ShowShop, 250f);
            gold.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(1f, 0.5f), new Vector2(-260f, 0f), new Vector2(250f, 72f));

            var gems = Widgets.Counter("Gems", purse, Theme.GemIcon, Theme.Gem,
                campaign.Gems.ToString(), shell.ShowShop, 250f);
            gems.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(250f, 72f));
        }

        static void Header(MenuShell shell, RectTransform root, Campaign campaign)
        {
            var ribbon = Widgets.Ribbon("Ribbon", root, "Välj nivå");
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(Widgets.SafeWidth - 120f, 100f));

            // One tab per chapter the player has reached, plus the next one — so the gate
            // that opens it is visible, rather than a chapter that simply is not there.
            int tabs = Mathf.Clamp(campaign.HighestChapter + 1, 2, 4);

            var row = Widgets.Node("Chapters", root);
            row.Place(new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(Widgets.SafeWidth, 96f));

            // Narrower as they multiply, so four tabs still fit the width they have.
            float width = Mathf.Min(360f, (Widgets.SafeWidth - (tabs - 1) * 16f) / tabs);
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
            name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(Widgets.SafeWidth, 48f));

            if (campaign.ChapterOpen(_shown)) return;

            var locked = Widgets.Label("Gate", root,
                $"Behöver {Campaign.StarsToOpenNextChapter} stjärnor i kapitel {_shown - 1}" +
                $" — du har {campaign.StarsIn(_shown - 1)}", Widgets.SmallSize, Theme.Danger);
            locked.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -462f), new Vector2(Widgets.SafeWidth, 48f));
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
            // As wide as everything else on this screen and no wider.
            //
            // Stretched to the canvas the board grew with the window, and the painting
            // inside it is fitted width-first — so a wide editor Game view made a map
            // several screens tall out of a picture drawn for a phone. Every other row
            // here is built at SafeWidth and centred; the board is now too, so what the
            // map looks like no longer depends on the shape of the window it is in.
            var frame = Widgets.Node("Levels", root);
            frame.anchorMin = new Vector2(0.5f, 0f);
            frame.anchorMax = new Vector2(0.5f, 1f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.offsetMin = new Vector2(-Widgets.SafeWidth * 0.5f, NavHeight + 24f);
            frame.offsetMax = new Vector2(Widgets.SafeWidth * 0.5f, -500f);

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
            var painting = Painting();

            Debug.Log(painting != null
                ? $"[Arna] Roadmap painting {painting.rect.width:0}×{painting.rect.height:0} px "
                  + $"for chapter {_shown}."
                : $"[Arna] No roadmap painting for chapter {_shown} — drawing the scattered "
                  + "wood instead.");

            if (painting != null)
            {
                // The picture sets the shape of the board rather than being cropped to
                // fit one. Its width is the viewport's — the content is stretched to that
                // — and the fitter works the height out from the width every layout pass,
                // which is the only way to get it right without knowing the width here:
                // a rect's size is not decided until the layout runs, and this is built
                // before it does.
                var sheet = Widgets.Panel("Painting", content, painting, Color.white);
                sheet.type = Image.Type.Simple;
                sheet.raycastTarget = false;
                sheet.rectTransform.Fill();

                var fitter = content.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                fitter.aspectRatio = painting.rect.width / painting.rect.height;
            }
            else
            {
                content.sizeDelta =
                    new Vector2(0f, TopPad * 2f + Step * (Campaign.LevelsPerChapter - 1));
            }

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 40f;

            // Ground, then the wood, then the road over it, then the levels on the road.
            // Drawing order is hierarchy order in UGUI, so this list is the picture from
            // back to front and reads that way.
            //
            // With a painting there is none of it: the ground, the wood and the road are
            // all in the picture, and laying paving stones over a painted road is drawing
            // a road on a road.
            if (painting == null)
            {
                Turf(content);
                Wood(content);

                for (int level = 1; level < Campaign.LevelsPerChapter; level++)
                    Stones(content, Spot(level), Spot(level + 1));
            }

            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                Node(shell, content, campaign, level, painting != null);

            // Over the scrolling board and outside it, so the dark stays at the edges of
            // the frame instead of travelling up the map with the trees.
            var dark = Widgets.Panel("Vignette", frame, Theme.Vignette, Color.white);
            dark.raycastTarget = false;
            dark.rectTransform.Fill();

#if UNITY_EDITOR
            if (painting == null)
            {
                // On the screen and not only in the console, because a fallback that
                // looks deliberate is indistinguishable from one that is broken — which
                // has now cost three rounds on the shop's own painting. Laid after the
                // vignette so the darkening at the edges cannot swallow the one line
                // that explains the screen.
                var note = Widgets.Label("Missing", frame,
                    $"{Backdrops.Roadmap}.png hittades inte — {Backdrops.Inventory()}",
                    Widgets.SmallSize - 8, new Color(1f, 0.85f, 0.55f, 0.85f));

                note.Wrap();
                note.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 12f),
                                         new Vector2(Widgets.SafeWidth, 60f));
            }
#endif

            // Where the player is, as a fraction of the board rather than a pixel offset.
            //
            // A rect has no size until the layout has run, and this is built before it
            // does — so the old arithmetic against frame.rect.height was measuring a
            // rectangle of zero height whenever the screen was built fresh. The
            // ScrollRect's own normalised position needs no measurement: nought is the
            // bottom, one is the top, and it clamps itself.
            campaign.Furthest(out int _, out int next);

            float along = Campaign.LevelsPerChapter > 1
                ? (next - 1) / (float)(Campaign.LevelsPerChapter - 1)
                : 0f;

            // Levels climb the picture, so the start of the chapter is the bottom of it.
            scroll.verticalNormalizedPosition = Mathf.Clamp01(along - 0.15f);
        }

        /// <summary>
        /// Where each level sits on the painted map, as a fraction of the painting: x
        /// from its left edge, y from its top.
        ///
        /// Read off the painting itself, which is the only way a medallion can land on a
        /// road somebody drew rather than near it. Fractions rather than pixels, so the
        /// same table holds however large the picture is drawn — on a tall phone, a short
        /// one, or in a Game view of any shape at all.
        ///
        /// The order runs <b>up</b> the picture: level one at the fortress gate at the
        /// bottom, the tenth at the castle on the skyline. That is the way the painting
        /// reads — you can see where the journey starts and what it is heading for — and
        /// it is the reverse of the abstract stepping-stone path this screen had before.
        ///
        /// To move one: change its pair. The numbers are shown under each medallion in
        /// the editor, so a wrong one can be read off the screen rather than guessed at.
        /// </summary>
        static readonly Vector2[] Waypoints =
        {
            new Vector2(0.508f, 0.845f),   // 1  — outside the fortress gate
            new Vector2(0.487f, 0.762f),   // 2
            new Vector2(0.520f, 0.680f),   // 3
            new Vector2(0.500f, 0.598f),   // 4  — the lower stone bridge
            new Vector2(0.452f, 0.522f),   // 5  — under the falls, by the village
            new Vector2(0.497f, 0.462f),   // 6  — the upper bridge
            new Vector2(0.548f, 0.400f),   // 7  — below the watchtower
            new Vector2(0.532f, 0.318f),   // 8
            new Vector2(0.548f, 0.232f),   // 9
            new Vector2(0.640f, 0.140f)    // 10 — the castle road
        };

        /// <summary>
        /// Shows each medallion's place on the painting, in the editor only.
        ///
        /// On while the waypoints are being fitted to a picture, which is a job of
        /// looking rather than of arithmetic: read the pair under a medallion that has
        /// landed in a river and it can be moved in one line. Off once they are right.
        /// </summary>
        public const bool ShowWaypoints = true;

        /// <summary>
        /// The painting for this chapter, or the shared one.
        ///
        /// Chapter two may have its own — ArnaRoadmap2.png — and falls back to the first
        /// when it has not been painted yet. The waypoints belong to a painting, so a
        /// chapter with its own picture will want its own table with it.
        /// </summary>
        static Sprite Painting()
        {
            if (_shown > 1)
            {
                var own = Backdrops.Find(Backdrops.Roadmap + _shown);
                if (own != null) return own;
            }

            return Backdrops.Find(Backdrops.Roadmap);
        }

        /// <summary>Forest floor under everything, tiled rather than stretched.</summary>
        static void Turf(RectTransform content)
        {
            var turf = Widgets.Panel("Turf", content, Theme.Ground, Color.white);
            turf.type = Image.Type.Tiled;
            turf.raycastTarget = false;
            turf.rectTransform.Fill();
        }

        /// <summary>
        /// The wood the road goes through.
        ///
        /// Scattered from a seed rather than laid out by hand, for the same reason the
        /// levels are generated: a hundred chapters of hand-placed trees is not a thing
        /// anybody is going to do, and a board that is different every time you open it
        /// is a board you cannot recognise. The seed is the chapter, so chapter one's
        /// wood is chapter one's wood every time.
        ///
        /// Nothing grows on the road. Each candidate is measured against the path — the
        /// same curve the paving stones are laid along — and against the level medallions,
        /// and thrown away if it would stand on either. Density rises toward the edges,
        /// which is what closes the view in around the road rather than dotting trees
        /// evenly over a field.
        /// </summary>
        static void Wood(RectTransform content)
        {
            var rng = new DeterministicRandom(_shown * 977 + 5501);
            var road = Road();

            float halfWidth = Widgets.SafeWidth * 0.5f - 30f;
            float height = content.sizeDelta.y;

            // Generated first and sorted before anything is built: a tree lower on the
            // board is nearer the viewer and has to draw over one behind it, and in UGUI
            // that means being added later.
            var standing = new List<(Vector2 at, Sprite sprite, float size)>();

            for (int i = 0; i < Attempts; i++)
            {
                float x = rng.Range(-halfWidth, halfWidth);
                float y = -rng.Range(0f, height);

                // Thicker toward the edges: a tree in the middle of the board has to win
                // a roll it is unlikely to win, and one at the margin is nearly certain.
                float edge = Mathf.Abs(x) / halfWidth;
                if (!rng.Chance(0.25f + edge * edge * 0.75f)) continue;

                var at = new Vector2(x, y);
                if (TooNear(at, road, ClearOfRoad)) continue;

                float roll = rng.Range(0f, 1f);

                Sprite sprite;
                float size;

                if (roll < 0.62f) { sprite = Theme.Conifer; size = rng.Range(120f, 190f); }
                else if (roll < 0.78f) { sprite = Theme.Broadleaf; size = rng.Range(105f, 150f); }
                else if (roll < 0.92f) { sprite = Theme.Shrub; size = rng.Range(44f, 74f); }
                else { sprite = Theme.Boulder; size = rng.Range(40f, 72f); }

                standing.Add((at, sprite, size));
            }

            standing.Sort((a, b) => b.at.y.CompareTo(a.at.y));

            foreach (var (at, sprite, size) in standing)
            {
                float aspect = sprite.rect.height / sprite.rect.width;

                var tree = Widgets.Icon("Tree", content, sprite, Color.white, size);
                tree.rectTransform.Place(new Vector2(0.5f, 1f), at,
                                         new Vector2(size / aspect, size));
            }
        }

        /// <summary>How many places are tried. Most are refused; the road wins every argument.</summary>
        const int Attempts = 420;

        /// <summary>Metres — pixels here — of clear ground either side of the paving.</summary>
        const float ClearOfRoad = 150f;

        /// <summary>The line the road actually takes, sampled for the scatter to avoid.</summary>
        static List<Vector2> Road()
        {
            var points = new List<Vector2>();

            for (int level = 1; level < Campaign.LevelsPerChapter; level++)
            {
                var from = Spot(level);
                var to = Spot(level + 1);
                var mid = Bend(from, to);

                for (int i = 0; i <= 10; i++)
                    points.Add(Curve(from, mid, to, i / 10f));
            }

            points.Add(Spot(Campaign.LevelsPerChapter));
            return points;
        }

        static bool TooNear(Vector2 at, List<Vector2> road, float clearance)
        {
            float squared = clearance * clearance;

            foreach (var point in road)
                if ((at - point).sqrMagnitude < squared) return true;

            return false;
        }

        /// <summary>The offset midpoint that bows the road between two levels.</summary>
        static Vector2 Bend(Vector2 from, Vector2 to)
            => (from + to) * 0.5f + new Vector2((to.x - from.x) * -0.25f, 0f);

        static Vector2 Curve(Vector2 from, Vector2 mid, Vector2 to, float t)
            => Mathf.Pow(1f - t, 2f) * from + 2f * (1f - t) * t * mid + t * t * to;

        /// <summary>Where a level sits on the board, in content coordinates.</summary>
        static Vector2 Spot(int level)
        {
            // Climbing, like the painted board: the first level at the foot of the map
            // and the last at the top. One direction in the game, whichever board is
            // being drawn.
            int from = Campaign.LevelsPerChapter - level;

            return new Vector2(Mathf.Sin(from * 1.15f) * Swing, -(TopPad + from * Step));
        }

        /// <summary>Lays the paving stones between two levels.</summary>
        static void Stones(RectTransform content, Vector2 from, Vector2 to)
        {
            // Enough that they touch. The mock-up's road is a continuous ribbon of
            // flagstones and not a dotted line: five spaced stones between medallions
            // read as stepping stones across a stream, which is a different place.
            const int stones = 9;
            var mid = Bend(from, to);

            for (int i = 1; i <= stones; i++)
            {
                float t = i / (stones + 1f);

                var point = Curve(from, mid, to, t);
                var ahead = Curve(from, mid, to, Mathf.Min(1f, t + 0.05f));

                var slab = Widgets.Icon("Stone", content, Theme.Slab,
                                        new Color(1f, 1f, 1f, 0.9f), 56f);
                slab.rectTransform.Place(new Vector2(0.5f, 1f), point, new Vector2(112f, 74f));

                var step = ahead - point;
                float angle = Mathf.Atan2(step.y, step.x) * Mathf.Rad2Deg;
                slab.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
            }
        }

        /// <summary>One level medallion: number, stars, or a padlock.</summary>
        static void Node(MenuShell shell, RectTransform content, Campaign campaign, int level,
                         bool painted)
        {
            bool open = campaign.Unlocked(_shown, level);
            int stars = campaign.Stars(_shown, level);
            int chapter = _shown;

            var medallion = Widgets.Panel("Level" + level, content, Theme.Round,
                open ? Color.white : new Color(0.42f, 0.40f, 0.38f, 1f));

            if (painted) Pin(medallion.rectTransform, level);
            else medallion.rectTransform.Place(new Vector2(0.5f, 1f), Spot(level),
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

            if (painted && ShowWaypoints) Coordinate(medallion.transform, level);

            var halo = Widgets.Icon("Halo", medallion.transform, Theme.Round,
                                    new Color(Theme.BrightGold.r, Theme.BrightGold.g, Theme.BrightGold.b, 0.25f),
                                    NodeSize + 34f);
            halo.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero,
                                     new Vector2(NodeSize + 34f, NodeSize + 34f));
            halo.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// Pins a medallion to its place on the painting.
        ///
        /// Anchored rather than positioned: both anchors sit at the waypoint's fraction
        /// of the content, so the medallion holds its spot on the road whatever size the
        /// picture ends up being drawn at. A pixel offset would be right on one screen
        /// and wrong on the next, which for a road painted by hand is the whole game.
        /// </summary>
        static void Pin(RectTransform medallion, int level)
        {
            var at = Waypoints[Mathf.Clamp(level - 1, 0, Waypoints.Length - 1)];
            var anchor = new Vector2(at.x, 1f - at.y);

            medallion.anchorMin = anchor;
            medallion.anchorMax = anchor;
            medallion.pivot = new Vector2(0.5f, 0.5f);
            medallion.anchoredPosition = Vector2.zero;
            medallion.sizeDelta = new Vector2(NodeSize, NodeSize);
        }

        /// <summary>The waypoint's own numbers, under the medallion, for fitting them.</summary>
        static void Coordinate(Transform medallion, int level)
        {
#if UNITY_EDITOR
            var at = Waypoints[Mathf.Clamp(level - 1, 0, Waypoints.Length - 1)];

            var label = Widgets.Label("At", medallion, $"{at.x:F3}, {at.y:F3}",
                                      Widgets.SmallSize - 10, new Color(1f, 0.9f, 0.6f, 0.7f));
            label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, -14f),
                                      new Vector2(200f, 28f));
#endif
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

            Tab(shell, bar.transform, -336f, "BUTIK", Theme.CoinIcon, false, null, shell.ShowShop);
            Tab(shell, bar.transform, -168f, "TRUPPER", Theme.HeartIcon, false, null,
                shell.ShowTroops);
            Tab(shell, bar.transform, 0f, "STRID", Theme.Star, true, null);
            Tab(shell, bar.transform, 168f, "SMEDJA", Theme.GemIcon, false,
                "Uppgraderingar köps i dag med silver mitt i ett uppdrag.");
            Tab(shell, bar.transform, 336f, "KARTA", Theme.SkullIcon, false,
                "Världskartan över kapitlen är inte byggd.");
        }

        static void Tab(MenuShell shell, Transform bar, float x, string text, Sprite icon,
                        bool here, string explanation,
                        UnityEngine.Events.UnityAction goes = null)
        {
            var slot = Widgets.Panel("Tab" + text, bar, here ? Theme.Frame : Theme.Flat,
                                     here ? Theme.Primary : new Color(0f, 0f, 0f, 0f));
            slot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(x, here ? 12f : 0f),
                                     new Vector2(160f, here ? 168f : 150f));

            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;

            if (goes != null) button.onClick.AddListener(goes);
            else if (!here) button.onClick.AddListener(() => shell.ShowStub(text, explanation));

            var glyph = Widgets.Icon("Glyph", slot.transform, icon,
                                     here ? Theme.BrightGold : Theme.Muted, 64f);
            glyph.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(64f, 64f));

            var label = Widgets.Label("Text", slot.transform, text, Widgets.SmallSize,
                                      here ? Theme.Parchment : Theme.Muted);
            label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(156f, 42f));
        }
    }
}
