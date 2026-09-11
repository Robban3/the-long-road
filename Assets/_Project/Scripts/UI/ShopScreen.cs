using TheVeil.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace TheVeil.UI
{
    /// <summary>
    /// Where the gold goes: the caravan's own gear on one tab, the troops on the other.
    ///
    /// Every item is a number the fighting already reads, with two exceptions built for
    /// the shop — field repair and the treasure lashings. That constraint is the design:
    /// a purchase that reaches the run through a road already built and already tested
    /// cannot break the combat on its way in.
    ///
    /// Thirty steps rather than five on everything continuous, because the *effect* is
    /// what is capped (see <see cref="BoonTable"/>) — so the steps can be many and small
    /// without putting the balance at the mercy of whoever grinds hardest.
    /// </summary>
    public static class ShopScreen
    {
        /// <summary>
        /// Four shelves, because eleven items and twenty troop tracks on one list is a
        /// wall rather than a shop. Each is one question: how tough is the caravan, who
        /// is guarding it, what does silver do, and how far can we see.
        /// </summary>
        enum Tab { Caravan, Troops, Silver, Scouting }

        static Tab _tab = Tab.Caravan;
        static TroopKind _troop = TroopKind.Spearmen;

        public static void Build(MenuShell shell, RectTransform root)
        {
            var campaign = Session.Campaign;

            Header(shell, root, campaign);
            Tabs(shell, root);

            var content = Board(root);

            if (_tab == Tab.Troops) Troops(shell, campaign, content);
            else Gear(shell, campaign, content);
        }

        static void Header(MenuShell shell, RectTransform root, Campaign campaign)
        {
            var back = Widgets.Chip("Back", root, Theme.Chevron, shell.ShowMain);
            back.image.rectTransform.Place(new Vector2(0f, 1f),
                new Vector2(Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            var ribbon = Widgets.Ribbon("Ribbon", root, Loc.T("Shop"));
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -Widgets.Margin),
                       new Vector2(520f, 100f));

            var gold = Widgets.Counter("Gold", root, Theme.CoinIcon, Theme.Coin,
                                       Loc.F("{0} gold", campaign.Gold), null, 300f);
            gold.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(300f, 76f));

#if UNITY_EDITOR
            // A purse for trying the shop without playing twenty levels first. Editor
            // only, and it says so on its face: a debug affordance that can reach a
            // player is not a debug affordance, it is a bug with a label on it.
            var grant = Widgets.Plate("Grant", root, "+500 (TEST)", ButtonRole.Secondary, () =>
            {
                campaign.Earn(500);
                Session.Save();
                shell.Show(Build);
            });

            grant.image.rectTransform.Place(new Vector2(1f, 1f),
                new Vector2(-Widgets.Margin, -160f), new Vector2(230f, 76f));
#endif
        }

        static void Tabs(MenuShell shell, RectTransform root)
        {
            Chooser(shell, root, -312f, Loc.T("CARAVAN"), Tab.Caravan);
            Chooser(shell, root, -104f, Loc.T("TROOPS"), Tab.Troops);
            Chooser(shell, root, 104f, Loc.T("SILVER"), Tab.Silver);
            Chooser(shell, root, 312f, Loc.T("SCOUTING"), Tab.Scouting);
        }

        static void Chooser(MenuShell shell, RectTransform root, float x, string text, Tab tab)
        {
            bool here = _tab == tab;

            var button = Widgets.Plate("Tab" + tab, root, text,
                here ? ButtonRole.Primary : ButtonRole.Secondary,
                () => { _tab = tab; shell.Show(Build); });

            button.image.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(x, -262f),
                                             new Vector2(200f, 92f));
        }

        /// <summary>
        /// The scrolling board the cards sit on.
        ///
        /// Eleven items on one tab and twenty troop tracks on the other; neither fits a
        /// screen. Built the way the roadmap builds its own — viewport, content and an
        /// invisible sheet to catch the drag, because a ScrollRect only sees a drag that
        /// lands on a graphic.
        /// </summary>
        static RectTransform Board(RectTransform root)
        {
            var frame = Widgets.Node("Board", root);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(Widgets.Margin, 130f);
            frame.offsetMax = new Vector2(-Widgets.Margin, -374f);

            var scroll = frame.gameObject.AddComponent<ScrollRect>();

            var viewport = Widgets.Node("Viewport", frame);
            viewport.Fill();
            viewport.gameObject.AddComponent<RectMask2D>();

            var catcher = Widgets.Panel("Catcher", viewport, Theme.Flat, new Color(0f, 0f, 0f, 0.002f));
            catcher.type = Image.Type.Simple;
            catcher.rectTransform.Fill();

            var content = Widgets.Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 40f;

            return content;
        }

        // ---- the caravan's own gear -------------------------------------------------

        static void Gear(MenuShell shell, Campaign campaign, RectTransform content)
        {
            float y = 0f;

            foreach (var boon in BoonTable.All)
            {
                if (Shelf(boon) != _tab) continue;

                int owned = campaign.BoonLevel(boon);
                int max = BoonTable.MaxLevel(boon);

                Card(content, ref y, Name(boon), What(boon), owned, max,
                     Now(boon, owned), Next(boon, owned),
                     campaign.PriceOf(boon), campaign.Gold,
                     () =>
                     {
                         if (!campaign.TryBuy(boon, out _)) return;

                         Session.Save();
                         shell.Show(Build);
                     });
            }

            content.sizeDelta = new Vector2(0f, -y + 20f);
        }

        /// <summary>Which shelf a thing belongs on.</summary>
        static Tab Shelf(Boon boon)
        {
            switch (boon)
            {
                case Boon.Hardened:
                case Boon.Repair:
                case Boon.Lashings:
                    return Tab.Caravan;

                // The escort's two whole-number boons sit with the troops, because that
                // is the question they answer: how many of them are there and where do
                // they stand.
                case Boon.Muster:
                case Boon.Outriders:
                    return Tab.Troops;

                case Boon.Watch:
                case Boon.Tracking:
                    return Tab.Scouting;

                default:
                    return Tab.Silver;
            }
        }

        // ---- the troops --------------------------------------------------------------

        static void Troops(MenuShell shell, Campaign campaign, RectTransform content)
        {
            float y = 0f;

            // How many there are and where they stand, before which of them is best at
            // what.
            foreach (var boon in BoonTable.All)
            {
                if (Shelf(boon) != Tab.Troops) continue;

                int held = campaign.BoonLevel(boon);

                Card(content, ref y, Name(boon), What(boon), held, BoonTable.MaxLevel(boon),
                     Now(boon, held), Next(boon, held),
                     campaign.PriceOf(boon), campaign.Gold,
                     () =>
                     {
                         if (!campaign.TryBuy(boon, out _)) return;

                         Session.Save();
                         shell.Show(Build);
                     });
            }

            // Which troop, then its tracks. Twenty tracks laid out flat is a wall; nine
            // names and three cards is a choice followed by a choice.
            var heading = Widgets.Label("Heading", content, Loc.T("PERMANENT TROOP LEVELS"),
                                        Widgets.SmallSize - 4, Theme.Gold);
            heading.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y - 10f),
                                        new Vector2(Widgets.SafeWidth, 40f));
            y -= 60f;

            // Three chips a row, as many rows as there are troops — thirteen now, which
            // is five rows where nine fitted in three.
            int rows = (TroopTable.All.Length + 2) / 3;

            var picker = Widgets.Node("Picker", content);
            picker.Place(new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(Widgets.SafeWidth, rows * 66f));

            float x = 0f, row = 0f;
            foreach (var kind in TroopTable.All)
            {
                bool here = kind == _troop;
                var chosen = kind;

                var chip = Widgets.Plate("Pick" + kind, picker, Names.TroopShort(kind),
                    here ? ButtonRole.Primary : ButtonRole.Secondary,
                    () => { _troop = chosen; shell.Show(Build); });

                chip.image.rectTransform.Place(new Vector2(0f, 1f), new Vector2(x, row),
                                               new Vector2(268f, 58f));

                x += 278f;
                if (x + 268f > Widgets.SafeWidth) { x = 0f; row -= 66f; }
            }

            y -= rows * 66f + 12f;

            foreach (var track in TroopBoonTable.Tracks)
            {
                if (!TroopBoonTable.Sells(_troop, track)) continue;

                int owned = campaign.TroopLevel(_troop, track);
                var chosen = track;

                Card(content, ref y, TrackName(_troop, track), TrackWhat(_troop, track),
                     owned, TroopBoonTable.Steps,
                     TrackNow(_troop, track, owned), TrackNext(_troop, track, owned),
                     campaign.PriceOf(_troop, track), campaign.Gold,
                     () =>
                     {
                         if (!campaign.TryBuy(_troop, chosen, out _)) return;

                         Session.Save();
                         shell.Show(Build);
                     });
            }

            if (!TroopBoonTable.Sells(_troop, UpgradeTrack.Special))
            {
                var note = Widgets.Label("NoRange", content,
                    Loc.F("{0} have no reach to buy — only bows, crossbows and staves shoot from afar.",
                          Names.Troop(_troop)),
                    Widgets.SmallSize - 6, Theme.Dim);

                note.Wrap();
                note.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y - 10f),
                                         new Vector2(Widgets.SafeWidth - 60f, 60f));
                y -= 80f;
            }

            content.sizeDelta = new Vector2(0f, -y + 20f);
        }

        // ---- one card ----------------------------------------------------------------

        /// <summary>
        /// One thing you can buy: what it is, what it does, how far you have taken it,
        /// what you have now, what the next step adds, and the price.
        ///
        /// The bar replaces the row of pips the shop had. Five pips read fine; thirty do
        /// not, and a bar says "you are a third of the way" at a glance where thirty dots
        /// say only "there are a lot of these".
        /// </summary>
        static void Card(RectTransform content, ref float y, string name, string what,
                         int owned, int max, string now, string next,
                         int price, int gold, System.Action buy)
        {
            bool finished = owned >= max;
            bool affordable = !finished && gold >= price;

            var plate = Widgets.Panel("Card" + name, content, Theme.Frame,
                finished ? Theme.Secondary : new Color(0.16f, 0.14f, 0.12f));

            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y),
                                      new Vector2(Widgets.SafeWidth, 230f));

            var title = Widgets.Label("Name", plate.transform, name, Widgets.BodySize - 2,
                                      Theme.Parchment, TextAnchor.MiddleLeft);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(28f, -14f),
                                      new Vector2(500f, 44f));

            var body = Widgets.Label("What", plate.transform, what, Widgets.SmallSize - 6,
                                     Theme.Muted, TextAnchor.UpperLeft);
            body.Wrap();
            body.rectTransform.Place(new Vector2(0f, 1f), new Vector2(28f, -58f),
                                     new Vector2(500f, 78f));

            Bar(plate.transform, owned, max);

            var reading = Widgets.Label("Now", plate.transform,
                finished ? Loc.F("now {0}", now) : Loc.F("now {0}   ·   next step {1}", now, next),
                Widgets.SmallSize - 8, finished ? Theme.Gold : Theme.Dim, TextAnchor.MiddleLeft);
            reading.rectTransform.Place(new Vector2(0f, 0f), new Vector2(28f, 22f),
                                        new Vector2(520f, 34f));

            if (finished)
            {
                var done = Widgets.Label("Done", plate.transform, Loc.T("FULLY BUILT"),
                                         Widgets.SmallSize - 4, Theme.Gold);
                done.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-40f, -10f),
                                         new Vector2(250f, 60f));
                y -= 246f;
                return;
            }

            var button = Widgets.Plate("Buy", plate.transform, Loc.F("{0} GOLD", price),
                                       affordable ? ButtonRole.Primary : ButtonRole.Disabled,
                                       () => buy());

            button.image.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-28f, -6f),
                                             new Vector2(240f, 92f));

            y -= 246f;
        }

        /// <summary>How far along the track this is, as a bar and a count.</summary>
        static void Bar(Transform plate, int owned, int max)
        {
            var track = Widgets.Panel("Track", plate, Theme.Flat, new Color(0f, 0f, 0f, 0.55f));
            track.type = Image.Type.Simple;
            track.rectTransform.Place(new Vector2(0f, 0f), new Vector2(28f, 66f),
                                      new Vector2(400f, 16f));

            var fill = Widgets.Panel("Fill", track.transform, Theme.Flat, Theme.BrightGold);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = max > 0 ? owned / (float)max : 0f;
            fill.rectTransform.Fill(2f, 2f, 2f, 2f);

            var count = Widgets.Label("Count", plate, Loc.F("step {0} / {1}", owned, max),
                                      Widgets.SmallSize - 8, Theme.Muted, TextAnchor.MiddleLeft);
            count.rectTransform.Place(new Vector2(0f, 0f), new Vector2(442f, 74f),
                                      new Vector2(200f, 30f));
        }

        // ---- what everything is called and does ---------------------------------------

        static string Name(Boon boon)
        {
            switch (boon)
            {
                case Boon.Purse: return Loc.T("Trading purse");
                case Boon.Muster: return Loc.T("Muster");
                case Boon.Hardened: return Loc.T("Hardened wagons");
                case Boon.Smithy: return Loc.T("Field smithy");
                case Boon.Outriders: return Loc.T("Outriders");
                case Boon.Trade: return Loc.T("Merchantry");
                case Boon.Watch: return Loc.T("Vigilance");
                case Boon.Tracking: return Loc.T("Tracking");
                case Boon.Exchange: return Loc.T("Exchange office");
                case Boon.Repair: return Loc.T("Field repair");
                default: return Loc.T("Lashings");
            }
        }

        static string What(Boon boon)
        {
            switch (boon)
            {
                case Boon.Purse:
                    return Loc.T("Silver in the purse as soon as the mission starts, so the first upgrade can be bought before the fight instead of after it.");
                case Boon.Muster:
                    return Loc.T("More points to build the escort with. A point is a whole thing, hence few and costly steps.");
                case Boon.Hardened:
                    return Loc.T("The wagons take more punishment before they break. If all three break, the mission is lost.");
                case Boon.Smithy:
                    return Loc.T("Cheaper to upgrade the troops in the middle of a mission.");
                case Boon.Outriders:
                    return Loc.T("A post in the line opens earlier than the chapter would otherwise give it.");
                case Boon.Trade:
                    return Loc.T("More silver for every enemy group brought down, all mission long.");
                case Boon.Watch:
                    return Loc.T("The caravan sees further, so enemies are revealed earlier — and only what has been revealed can be shot at.");
                case Boon.Tracking:
                    return Loc.T("Traps are spotted further ahead, giving the engineer time to disarm them before the wheels get there.");
                case Boon.Exchange:
                    return Loc.T("A better rate when leftover silver is changed into gold after the mission. Spending it in the field is still better.");
                case Boon.Repair:
                    return Loc.T("The wagons are mended while the column rolls — but not while it fights. A quiet stretch becomes worth something.");
                default:
                    return Loc.T("The treasure wagon takes less damage. Its state decides the gold you bring home, so it is as much a raise in pay.");
            }
        }

        static string Now(Boon boon, int level) => Reading(boon, BoonTable.Effect(boon, level));

        static string Next(Boon boon, int level)
        {
            float step = BoonTable.Effect(boon, level + 1) - BoonTable.Effect(boon, level);
            return "+" + Reading(boon, step);
        }

        /// <summary>The effect in the unit it is actually measured in.</summary>
        static string Reading(Boon boon, float value)
        {
            switch (boon)
            {
                case Boon.Purse: return Loc.F("{0:F0} silver", value);
                case Boon.Muster: return Loc.F("{0:F0} points", value);
                case Boon.Outriders: return Loc.F("{0:F0} posts", value);
                case Boon.Watch:
                case Boon.Tracking: return Loc.F("{0:F1} m", value);
                case Boon.Exchange: return Loc.F("{0:F1} silver/gold", value);
                case Boon.Repair: return Loc.F("{0:F2} hp/s", value);
                default: return Loc.F("{0:F1} %", value * 100f);
            }
        }

        // The special track is reach for a bow or a staff and sight for the scout, so its
        // words depend on whose it is.

        static string TrackName(TroopKind kind, UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Weapon: return Loc.T("Weapon");
                case UpgradeTrack.Armour: return Loc.T("Armour");
                default: return TroopTable.Scouts(kind) ? Loc.T("Sight") : Loc.T("Reach");
            }
        }

        static string TrackWhat(TroopKind kind, UpgradeTrack track)
        {
            if (track == UpgradeTrack.Special && TroopTable.Scouts(kind))
                return Loc.T("The scout sees further, so enemies are spotted even earlier. Applies to every mission, on top of what you buy with silver in the field.");

            switch (track)
            {
                case UpgradeTrack.Weapon:
                    return Loc.T("More damage. Applies to every mission, on top of what you buy with silver in the field.");
                case UpgradeTrack.Armour:
                    return Loc.T("More health and a larger share of every blow turned aside. The troops march out with the extra health from the start.");
                default:
                    return Loc.T("Longer range. Remember that nothing can be shot before it has been revealed — reach and sight go together.");
            }
        }

        static string TrackNow(TroopKind kind, UpgradeTrack track, int level)
            => TrackReading(kind, track, TroopBoonTable.Share(level));

        static string TrackNext(TroopKind kind, UpgradeTrack track, int level)
            => "+" + TrackReading(kind, track,
                                  TroopBoonTable.Share(level + 1) - TroopBoonTable.Share(level));

        static string TrackReading(TroopKind kind, UpgradeTrack track, float share)
        {
            if (track == UpgradeTrack.Special && TroopTable.Scouts(kind))
                return Loc.F("{0:F1} m sight", share * TroopBoonTable.SightCap);

            switch (track)
            {
                case UpgradeTrack.Weapon:
                    return Loc.F("{0:F1} % damage", share * TroopBoonTable.WeaponCap * 100f);
                case UpgradeTrack.Armour:
                    return Loc.F("{0:F1} % health", share * TroopBoonTable.ArmourHealthCap * 100f);
                default:
                    return Loc.F("{0:F1} % reach", share * TroopBoonTable.RangeCap * 100f);
            }
        }
    }
}
