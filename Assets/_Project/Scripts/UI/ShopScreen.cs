using Arna.Sim;
using UnityEngine;

namespace Arna.UI
{
    /// <summary>
    /// Where the gold goes.
    ///
    /// Five things, and every one of them is a number the fighting already reads: silver
    /// in the purse when a run starts, points to spend on the escort, stouter carts,
    /// cheaper upgrades in the field, and a post of the line opened ahead of the
    /// chapter's own curve. That constraint is the design and not a shortcut — a shop
    /// selling effects the simulation has never heard of is a shop that has to be
    /// threaded past the combat afterwards, and this one reaches the run through roads
    /// that were built and tested long before it existed.
    ///
    /// Bought is bought: the levels live in the campaign beside the stars and go into the
    /// same save, so gold spent here is spent for good and shows up in the next level
    /// whatever it is.
    /// </summary>
    public static class ShopScreen
    {
        public static void Build(MenuShell shell, RectTransform root)
        {
            Backdrops.Paint(Backdrops.Shop, root, 0.62f);

            var campaign = Session.Campaign;

            Header(shell, root, campaign);

            float y = -300f;
            foreach (var boon in BoonTable.All) Row(shell, root, campaign, boon, ref y);

            Footer(root, campaign);
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
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(300f, 76f));

#if UNITY_EDITOR
            // A purse for trying the shop without playing four levels first. Editor only,
            // and it says so on the button: a debug affordance that can reach a player is
            // not a debug affordance, it is a bug with a label on it.
            var grant = Widgets.Plate("Grant", root, "+500 (TEST)", ButtonRole.Secondary, () =>
            {
                campaign.Earn(500);
                Session.Save();
                shell.Show(Build);
            });

            grant.image.rectTransform.Place(new Vector2(1f, 1f),
                new Vector2(-Widgets.Margin, -170f), new Vector2(230f, 76f));
#endif
        }

        /// <summary>One item: what it is, what it does, how far it has been taken, and the price.</summary>
        static void Row(MenuShell shell, RectTransform root, Campaign campaign, Boon boon,
                        ref float y)
        {
            int owned = campaign.BoonLevel(boon);
            int max = BoonTable.MaxLevel(boon);
            int price = campaign.PriceOf(boon);

            bool finished = owned >= max;
            bool affordable = !finished && campaign.Gold >= price;

            var plate = Widgets.Panel($"Boon{boon}", root, Theme.Frame,
                                      finished ? Theme.Secondary : new Color(0.16f, 0.14f, 0.12f));
            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y),
                                      new Vector2(Widgets.SafeWidth, 210f));

            var name = Widgets.Label("Name", plate.transform, Name(boon), Widgets.BodySize - 2,
                                     Theme.Parchment, TextAnchor.MiddleLeft);
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(28f, -18f),
                                     new Vector2(520f, 48f));

            var what = Widgets.Label("What", plate.transform, What(boon, owned),
                                     Widgets.SmallSize - 6, Theme.Muted, TextAnchor.UpperLeft);
            what.Wrap();
            what.rectTransform.Place(new Vector2(0f, 1f), new Vector2(28f, -70f),
                                     new Vector2(500f, 80f));

            Pips(plate.transform, owned, max);

            if (finished)
            {
                var done = Widgets.Label("Done", plate.transform, "FULLT UTBYGGT",
                                         Widgets.SmallSize - 4, Theme.Gold);
                done.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-40f, -18f),
                                         new Vector2(250f, 60f));
                y -= 226f;
                return;
            }

            var buy = Widgets.Plate("Buy", plate.transform, price + " GULD",
                                    affordable ? ButtonRole.Primary : ButtonRole.Disabled,
                                    () =>
                                    {
                                        if (!campaign.TryBuy(boon, out _)) return;

                                        Session.Save();
                                        shell.Show(Build);
                                    });

            buy.image.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-28f, -14f),
                                          new Vector2(250f, 92f));

            y -= 226f;
        }

        /// <summary>Levels as a row of pips, so how far this has been taken reads at a glance.</summary>
        static void Pips(Transform plate, int owned, int max)
        {
            var row = Widgets.Node("Pips", plate);
            row.Place(new Vector2(0f, 0f), new Vector2(28f, 22f), new Vector2(400f, 30f));

            for (int i = 0; i < max; i++)
            {
                var pip = Widgets.Panel("Pip" + i, row, Theme.Flat,
                    i < owned ? Theme.BrightGold : new Color(1f, 1f, 1f, 0.14f));

                pip.type = UnityEngine.UI.Image.Type.Simple;
                pip.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(i * 38f, 0f),
                                        new Vector2(30f, 10f));
            }
        }

        static void Footer(RectTransform root, Campaign campaign)
        {
            var note = Widgets.Label("Note", root,
                campaign.Gold > 0
                    ? "Guld tjänas på att komma fram. Det du köper här behåller du."
                    : "Du har inget guld ännu. Klara en nivå så börjar det trilla in.",
                Widgets.SmallSize - 6, Theme.Dim);

            note.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, Widgets.Margin),
                                     new Vector2(Widgets.SafeWidth, 44f));
        }

        static string Name(Boon boon)
        {
            switch (boon)
            {
                case Boon.Purse: return "Handelskassa";
                case Boon.Muster: return "Värvning";
                case Boon.Wainwright: return "Vagnmakare";
                case Boon.Smithy: return "Fältsmedja";
                default: return "Förridare";
            }
        }

        /// <summary>
        /// What it does, with the numbers in it — the one it gives now and the one the
        /// next level adds. A shop that says "improves your caravan" is a shop nobody can
        /// choose in.
        /// </summary>
        static string What(Boon boon, int owned)
        {
            switch (boon)
            {
                case Boon.Purse:
                    return $"Börja varje uppdrag med silver i kassan.\nNu: "
                           + $"{owned * BoonTable.SilverPerLevel} silver  ·  nästa nivå: +{BoonTable.SilverPerLevel}";

                case Boon.Muster:
                    return $"Fler poäng att sätta ihop eskorten för.\nNu: +{owned * BoonTable.PointsPerLevel} "
                           + $"poäng  ·  nästa nivå: +{BoonTable.PointsPerLevel}";

                case Boon.Wainwright:
                    return $"Tåligare vagnar.\nNu: +{owned * BoonTable.HealthPerLevel * 100f:F0} % "
                           + $"·  nästa nivå: +{BoonTable.HealthPerLevel * 100f:F0} %";

                case Boon.Smithy:
                    return $"Billigare uppgraderingar ute i fält.\nNu: −{owned * BoonTable.DiscountPerLevel * 100f:F0} % "
                           + $"·  nästa nivå: −{BoonTable.DiscountPerLevel * 100f:F0} %";

                default:
                    return $"En post i ledet öppnas tidigare än kapitlet annars ger den.\n"
                           + $"Nu: +{owned * BoonTable.PostsPerLevel}  ·  nästa nivå: +{BoonTable.PostsPerLevel}";
            }
        }
    }
}
