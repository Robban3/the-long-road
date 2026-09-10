using TheVeil.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace TheVeil.UI
{
    /// <summary>
    /// Where the player puts the escort together, before the road is drawn.
    ///
    /// **The choice the whole game is built on, and it lived in the Inspector.** Which
    /// troops stand where decides what a route through the forest costs: archers lose
    /// two fifths of a bowshot among trees, cavalry half its charge, spearmen kill wolves
    /// twice as fast, and a formation fights what is fighting it — so where the swordsmen
    /// stand decides which flank can answer. Every one of those rules was implemented,
    /// tested and unreachable, because the array that filled the six posts was a
    /// serialized field on a scene component that only the developer could see.
    ///
    /// Drawn as the formation itself rather than as a list. The posts are a shape — van,
    /// flanks, rearguard, and the scout out in front of all of it — and a list of six
    /// dropdowns would throw away the one piece of information the player most needs,
    /// which is where the thing they are placing will be standing.
    ///
    /// It sits between the roadmap and the planning map because both of its numbers are
    /// per level: the budget grows through a chapter, and so does the number of posts.
    /// </summary>
    public static class TroopScreen
    {
        /// <summary>The squad being built. Rebuilt when the level changes, kept while tweaking.</summary>
        static Squad _squad;
        static int _forChapter, _forLevel;

        /// <summary>The post the picker is currently open on, or none.</summary>
        static FormationSlot _picking;
        static bool _open;

        public static void Build(MenuShell shell, RectTransform root)
        {
            var recipe = new ChapterRecipe().ForLevel(Session.Level);

            var boons = Session.Campaign.Boons();

            if (_squad == null || _forChapter != Session.Chapter || _forLevel != Session.Level)
            {
                _squad = new Squad(recipe.SquadBudget + boons.ExtraSquadPoints,
                                   recipe.Posts + boons.ExtraPosts)
                {
                    School = Session.Campaign.TroopBoons()
                };
                _forChapter = Session.Chapter;
                _forLevel = Session.Level;

                Suggest(_squad);
            }

            Header(shell, root, recipe);
            Formation(shell, root);
            Footer(shell, root);

            if (_open) Picker(shell, root);
        }

        /// <summary>
        /// An escort already in the posts, rather than an empty formation and a shrug.
        ///
        /// A player opening this for the first time should see a working column they can
        /// argue with — that is a far better teacher than six empty sockets. The line is
        /// filled front to back in the order the posts open.
        ///
        /// Never the scout. She takes a post somebody with a sword would otherwise hold,
        /// and whether that trade is worth it is the player's to make, not ours.
        /// </summary>
        static void Suggest(Squad squad)
        {
            foreach (var kind in new[]
                     {
                         TroopKind.Spearmen, TroopKind.Swordsmen, TroopKind.Archers,
                         TroopKind.Shieldbearer, TroopKind.Swordsmen, TroopKind.Spearmen
                     })
            {
                if (!squad.TryPlace(kind)) break;
            }
        }

        static void Header(MenuShell shell, RectTransform root, LevelRecipe recipe)
        {
            var back = Widgets.Chip("Back", root, Theme.Chevron, shell.ShowRoadmap);
            back.image.rectTransform.Place(new Vector2(0f, 1f),
                new Vector2(Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            var ribbon = Widgets.Ribbon("Ribbon", root, Loc.T("Escort"));
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -Widgets.Margin),
                       new Vector2(480f, 100f));

            var level = Widgets.Label("Level", root,
                Loc.F("CHAPTER {0}  ·  LEVEL {1}", Session.Chapter, Session.Level), Widgets.SmallSize, Theme.Muted);
            level.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -172f),
                                      new Vector2(Widgets.SafeWidth - 120f, 44f));

            // The two numbers that bind, said plainly. Points run out long before posts
            // do at the start of a chapter, and the other way round at the end of one.
            var purse = Widgets.Counter("Points", root, Theme.Star, Theme.BrightGold,
                Loc.F("{0} of {1} points left", _squad.PointsRemaining, _squad.Budget), null, 460f);
            purse.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(460f, 76f));

            var posts = Widgets.Label("Posts", root,
                recipe.Posts < TroopTable.LinePosts
                    ? Loc.F("{0} of {1} posts open in the line  ·  more open later", _squad.Posts, TroopTable.LinePosts)
                    : Loc.F("{0} of {1} posts open in the line", _squad.Posts, TroopTable.LinePosts),
                Widgets.SmallSize - 4, Theme.Dim);
            posts.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -318f),
                                      new Vector2(Widgets.SafeWidth, 40f));
        }

        // Where each post sits on the diagram, in the same shape as the real formation:
        // the van in front, the flanks either side, the rearguard at the back.
        static Vector2 Spot(FormationSlot slot)
        {
            switch (slot)
            {
                case FormationSlot.Van: return new Vector2(0f, 150f);
                case FormationSlot.RightVan: return new Vector2(268f, 40f);
                case FormationSlot.LeftVan: return new Vector2(-268f, 40f);
                case FormationSlot.RightRear: return new Vector2(268f, -120f);
                case FormationSlot.LeftRear: return new Vector2(-268f, -120f);
                default: return new Vector2(0f, -230f);
            }
        }

        static void Formation(MenuShell shell, RectTransform root)
        {
            var board = Widgets.Panel("Board", root, Theme.Frame, new Color(0.13f, 0.15f, 0.12f, 1f));
            board.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                                      new Vector2(Widgets.SafeWidth, 900f));

            // The column the formation is drawn around, so the posts read as being beside
            // something rather than floating in a grid.
            var road = Widgets.Panel("Column", board.transform, Theme.Flat,
                                     new Color(0.30f, 0.26f, 0.20f, 0.55f));
            road.type = Image.Type.Simple;
            road.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -20f),
                                     new Vector2(120f, 560f));

            var heading = Widgets.Label("Heading", board.transform, Loc.T("▲  DIRECTION OF TRAVEL"),
                                        Widgets.SmallSize - 6, Theme.Dim);
            heading.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                                        new Vector2(400f, 34f));

            foreach (var slot in AllSlots) Post(shell, board.transform, slot);
        }

        static readonly FormationSlot[] AllSlots =
        {
            FormationSlot.Van, FormationSlot.RightVan, FormationSlot.LeftVan,
            FormationSlot.RightRear, FormationSlot.LeftRear, FormationSlot.Rear
        };

        static void Post(MenuShell shell, Transform board, FormationSlot slot)
        {
            bool open = _squad.Open(slot);
            var group = _squad[slot];
            var here = slot;

            var plate = Widgets.Panel($"Post{slot}", board, Theme.Frame,
                open ? (group != null ? Theme.Secondary : new Color(0.16f, 0.14f, 0.12f))
                     : Theme.Disabled);

            plate.rectTransform.Place(new Vector2(0.5f, 0.5f), Spot(slot), new Vector2(268f, 150f));

            if (!open)
            {
                var padlock = Widgets.Icon("Lock", plate.transform, Theme.Padlock, Theme.Dim, 54f);
                padlock.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -18f),
                                            new Vector2(54f, 54f));

                var shut = Widgets.Label("Shut", plate.transform, Loc.T("CLOSED"), Widgets.SmallSize - 6,
                                         Theme.Dim);
                shut.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                                         new Vector2(250f, 36f));
                return;
            }

            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(() =>
            {
                // A post with somebody in it empties; an empty one opens the picker. Two
                // taps to change a post rather than a menu of nine every time.
                if (_squad[here] != null) _squad.Remove(here);
                else { _picking = here; _open = true; }

                shell.Show(Build);
            });

            var name = Widgets.Label("Name", plate.transform, Names.Post(slot), Widgets.SmallSize - 6,
                                     Theme.Dim);
            name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -12f),
                                     new Vector2(250f, 34f));

            if (group == null)
            {
                var empty = Widgets.Label("Empty", plate.transform, "+", Widgets.TitleSize - 30,
                                          Theme.Dim);
                empty.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -10f),
                                          new Vector2(120f, 100f));
                return;
            }

            var troop = Widgets.Label("Troop", plate.transform, Names.Troop(group.Kind), Widgets.BodySize - 4,
                                      Theme.Parchment);
            troop.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 2f),
                                      new Vector2(250f, 48f));

            var cost = Widgets.Label("Cost", plate.transform,
                $"{TroopTable.Cost(group.Kind)} p  ·  {Role(group.Kind)}",
                Widgets.SmallSize - 8, Theme.Muted);
            cost.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                                     new Vector2(250f, 36f));
        }

        /// <summary>The sheet of troops that will fit in the post being filled.</summary>
        static void Picker(MenuShell shell, RectTransform root)
        {
            Widgets.Scrim("Scrim", root, 0.7f);

            // Tall enough for all nine troops above the cancel button, now that the scout
            // is chosen for a post of the line like the other eight.
            var panel = Widgets.Panel("Picker", root, Theme.Frame, Color.white);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Widgets.SafeWidth, 1300f));

            var ribbon = Widgets.Ribbon("Ribbon", panel.transform, Names.Post(_picking));
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(Widgets.SafeWidth - 240f, 100f));

            float y = -130f;

            foreach (var kind in TroopTable.All)
            {
                int cost = TroopTable.Cost(kind);
                bool affordable = cost <= _squad.PointsRemaining;

                // The scout is hired once, in the shop, and comes whenever she is asked
                // after that — one of her, in any post. Shown either way, so a player who
                // has not hired her yet can see she exists and where to get her.
                bool scout = TroopTable.Scouts(kind);
                bool hired = !scout || Session.Campaign.Boons().HasScout;
                bool spare = !scout || !_squad.HasScout;
                bool choosable = affordable && hired && spare;
                var chosen = kind;

                var row = Widgets.Plate($"Pick{kind}", panel.transform,
                    $"{Names.Troop(kind).ToUpperInvariant()}   {cost} p",
                    choosable ? ButtonRole.Secondary : ButtonRole.Disabled,
                    () =>
                    {
                        if (!choosable) return;

                        _squad.TryPlace(_picking, chosen);
                        _open = false;
                        shell.Show(Build);
                    });

                row.image.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y),
                                              new Vector2(Widgets.SafeWidth - 100f, 100f));

                string note = !hired ? Loc.T("bought in the shop") : !spare ? Loc.T("already along") : Role(kind);

                var role = Widgets.Label("Role", row.image.transform, note,
                                         Widgets.SmallSize - 8, Theme.Muted, TextAnchor.MiddleRight);
                role.rectTransform.Fill(24f, 0f, 24f, 0f);

                y -= 112f;
            }

            var close = Widgets.Plate("Close", panel.transform, Loc.T("CANCEL"), ButtonRole.Primary,
                () => { _open = false; shell.Show(Build); });

            close.image.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                                            new Vector2(Widgets.SafeWidth - 240f, Widgets.ButtonHeight));
        }

        static void Footer(MenuShell shell, RectTransform root)
        {
            var clear = Widgets.Plate("Clear", root, Loc.T("CLEAR"), ButtonRole.Secondary, () =>
            {
                _squad = new Squad(_squad.Budget, _squad.Posts);
                shell.Show(Build);
            });

            clear.image.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(-290f, Widgets.Margin),
                                            new Vector2(220f, Widgets.ButtonHeight));

            var go = Widgets.Plate("Go", root, Loc.T("DRAW THE ROAD"), ButtonRole.Primary, () =>
            {
                Session.SetEscort(_squad);
                shell.Draw(Session.Chapter, Session.Level);
            });

            go.image.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(130f, Widgets.Margin),
                                         new Vector2(560f, Widgets.ButtonHeight));
        }

        /// <summary>
        /// What each troop is for, in a handful of words.
        ///
        /// Read off the tables rather than invented: the spearman's double damage against
        /// wolves, the archer's reach and what a wood costs it, the shieldbearer's forty
        /// percent, the engineer's traps. A player choosing between nine troops on a
        /// budget of twelve needs to know what they do, and the numbers are already there.
        /// </summary>
        static string Role(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Spearmen: return Loc.T("double against wolves");
                case TroopKind.Swordsmen: return Loc.T("hardest in close combat");
                case TroopKind.Archers: return Loc.T("22 m reach, worse in forest");
                case TroopKind.Cavalry: return Loc.T("strong on plains, weak in marsh");
                case TroopKind.Mage: return Loc.T("18 m, expensive");
                case TroopKind.Scout: return Loc.T("34 m sight, walks ahead, does not fight");
                case TroopKind.Shieldbearer: return Loc.T("takes 40 % less damage");
                case TroopKind.Priest: return Loc.T("heals the most wounded");
                case TroopKind.Engineer: return Loc.T("disarms traps");
                default: return "";
            }
        }
    }
}
