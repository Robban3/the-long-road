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

            var ribbon = Widgets.Ribbon("Ribbon", root, "Butiken");
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -Widgets.Margin),
                       new Vector2(520f, 100f));

            var gold = Widgets.Counter("Gold", root, Theme.CoinIcon, Theme.Coin,
                                       campaign.Gold + " guld", null, 300f);
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
            Chooser(shell, root, -312f, "KARAVAN", Tab.Caravan);
            Chooser(shell, root, -104f, "TRUPPER", Tab.Troops);
            Chooser(shell, root, 104f, "SILVER", Tab.Silver);
            Chooser(shell, root, 312f, "SPANING", Tab.Scouting);
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
            var heading = Widgets.Label("Heading", content, "PERMANENTA TRUPPNIVÅER",
                                        Widgets.SmallSize - 4, Theme.Gold);
            heading.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y - 10f),
                                        new Vector2(Widgets.SafeWidth, 40f));
            y -= 60f;

            var picker = Widgets.Node("Picker", content);
            picker.Place(new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(Widgets.SafeWidth, 190f));

            float x = 0f, row = 0f;
            foreach (var kind in TroopTable.All)
            {
                bool here = kind == _troop;
                var chosen = kind;

                var chip = Widgets.Plate("Pick" + kind, picker, Short(kind),
                    here ? ButtonRole.Primary : ButtonRole.Secondary,
                    () => { _troop = chosen; shell.Show(Build); });

                chip.image.rectTransform.Place(new Vector2(0f, 1f), new Vector2(x, row),
                                               new Vector2(268f, 58f));

                x += 278f;
                if (x + 268f > Widgets.SafeWidth) { x = 0f; row -= 66f; }
            }

            y -= 210f;

            foreach (var track in TroopBoonTable.Tracks)
            {
                if (!TroopBoonTable.Sells(_troop, track)) continue;

                int owned = campaign.TroopLevel(_troop, track);
                var chosen = track;

                Card(content, ref y, TrackName(track), TrackWhat(track),
                     owned, TroopBoonTable.Steps,
                     TrackNow(track, owned), TrackNext(track, owned),
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
                    $"{Name(_troop)} har ingen räckvidd att köpa — bara bågskyttar och "
                    + "magiker skjuter på avstånd.", Widgets.SmallSize - 6, Theme.Dim);

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
                finished ? $"nu {now}" : $"nu {now}   ·   nästa steg {next}",
                Widgets.SmallSize - 8, finished ? Theme.Gold : Theme.Dim, TextAnchor.MiddleLeft);
            reading.rectTransform.Place(new Vector2(0f, 0f), new Vector2(28f, 22f),
                                        new Vector2(520f, 34f));

            if (finished)
            {
                var done = Widgets.Label("Done", plate.transform, "FULLT UTBYGGT",
                                         Widgets.SmallSize - 4, Theme.Gold);
                done.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-40f, -10f),
                                         new Vector2(250f, 60f));
                y -= 246f;
                return;
            }

            var button = Widgets.Plate("Buy", plate.transform, price + " GULD",
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

            var count = Widgets.Label("Count", plate, $"steg {owned} / {max}",
                                      Widgets.SmallSize - 8, Theme.Muted, TextAnchor.MiddleLeft);
            count.rectTransform.Place(new Vector2(0f, 0f), new Vector2(442f, 74f),
                                      new Vector2(200f, 30f));
        }

        // ---- what everything is called and does ---------------------------------------

        static string Name(Boon boon)
        {
            switch (boon)
            {
                case Boon.Purse: return "Handelskassa";
                case Boon.Muster: return "Värvning";
                case Boon.Hardened: return "Härdade vagnar";
                case Boon.Smithy: return "Fältsmedja";
                case Boon.Outriders: return "Förridare";
                case Boon.Trade: return "Köpmannaskap";
                case Boon.Watch: return "Vaksamhet";
                case Boon.Tracking: return "Spårsinne";
                case Boon.Exchange: return "Växelkontor";
                case Boon.Repair: return "Fältreparation";
                default: return "Lastsäkring";
            }
        }

        static string What(Boon boon)
        {
            switch (boon)
            {
                case Boon.Purse:
                    return "Silver i kassan redan när uppdraget börjar, så första "
                           + "uppgraderingen kan köpas före striden i stället för efter.";
                case Boon.Muster:
                    return "Fler poäng att sätta ihop eskorten för. En poäng är en hel "
                           + "sak, därför få och dyra steg.";
                case Boon.Hardened:
                    return "Vagnarna tål mer stryk innan de går sönder. Går alla tre "
                           + "sönder är uppdraget förlorat.";
                case Boon.Smithy:
                    return "Billigare att uppgradera trupperna mitt i ett uppdrag.";
                case Boon.Outriders:
                    return "En post i ledet öppnas tidigare än kapitlet annars ger den.";
                case Boon.Trade:
                    return "Mer silver för varje fiendegrupp som fälls, hela uppdraget "
                           + "igenom.";
                case Boon.Watch:
                    return "Karavanen ser längre, så fiender avslöjas tidigare — och man "
                           + "kan bara skjuta på det som avslöjats.";
                case Boon.Tracking:
                    return "Fällor upptäcks längre fram, vilket ger ingenjören tid att "
                           + "desarmera dem innan hjulen är där.";
                case Boon.Exchange:
                    return "Bättre kurs när silver som blev över växlas till guld efter "
                           + "uppdraget. Att spendera i fält är fortfarande bättre.";
                case Boon.Repair:
                    return "Vagnarna lagas medan kolonnen rullar — men inte medan det "
                           + "slåss. En lugn sträcka blir värd något.";
                default:
                    return "Skattvagnen tar mindre skada. Dess skick avgör guldet du får "
                           + "ut, så det är en uppgradering av lönen lika mycket.";
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
                case Boon.Purse: return $"{value:F0} silver";
                case Boon.Muster: return $"{value:F0} poäng";
                case Boon.Outriders: return $"{value:F0} poster";
                case Boon.Watch:
                case Boon.Tracking: return $"{value:F1} m";
                case Boon.Exchange: return $"{value:F1} silver/guld";
                case Boon.Repair: return $"{value:F2} hp/s";
                default: return $"{value * 100f:F1} %";
            }
        }

        static string Short(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Spearmen: return "SPJUT";
                case TroopKind.Swordsmen: return "SVÄRD";
                case TroopKind.Archers: return "BÅGE";
                case TroopKind.Cavalry: return "RYTTARE";
                case TroopKind.Mage: return "MAGIKER";
                case TroopKind.Scout: return "SPEJARE";
                case TroopKind.Shieldbearer: return "SKÖLD";
                case TroopKind.Priest: return "PRÄST";
                default: return "INGENJÖR";
            }
        }

        static string Name(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Spearmen: return "Spjutmän";
                case TroopKind.Swordsmen: return "Svärdsmän";
                case TroopKind.Archers: return "Bågskyttar";
                case TroopKind.Cavalry: return "Ryttare";
                case TroopKind.Mage: return "Magiker";
                case TroopKind.Scout: return "Spejare";
                case TroopKind.Shieldbearer: return "Sköldbärare";
                case TroopKind.Priest: return "Präst";
                default: return "Ingenjör";
            }
        }

        static string TrackName(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Weapon: return "Vapen";
                case UpgradeTrack.Armour: return "Rustning";
                default: return "Räckvidd";
            }
        }

        static string TrackWhat(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Weapon:
                    return "Mer skada. Gäller varje uppdrag, ovanpå det du köper med "
                           + "silver ute i fält.";
                case UpgradeTrack.Armour:
                    return "Mer hälsa och en större andel av skadan avvärjd. Trupperna "
                           + "rycker ut med den extra hälsan redan från början.";
                default:
                    return "Längre skotthåll. Kom ihåg att inget kan skjutas innan det "
                           + "avslöjats — räckvidd och sikt hör ihop.";
            }
        }

        static string TrackNow(UpgradeTrack track, int level)
            => TrackReading(track, TroopBoonTable.Share(level));

        static string TrackNext(UpgradeTrack track, int level)
            => "+" + TrackReading(track,
                                  TroopBoonTable.Share(level + 1) - TroopBoonTable.Share(level));

        static string TrackReading(UpgradeTrack track, float share)
        {
            switch (track)
            {
                case UpgradeTrack.Weapon:
                    return $"{share * TroopBoonTable.WeaponCap * 100f:F1} % skada";
                case UpgradeTrack.Armour:
                    return $"{share * TroopBoonTable.ArmourHealthCap * 100f:F1} % hälsa";
                default:
                    return $"{share * TroopBoonTable.RangeCap * 100f:F1} % räckvidd";
            }
        }
    }
}
