using TheVeil.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace TheVeil.UI
{
    /// <summary>
    /// The achievements: what the road has asked so far, how far along each one is, and
    /// the reward waiting on the ones that are done.
    ///
    /// Collected by a press rather than paid on the spot. A reward that arrives in the
    /// middle of a result screen is a number that changed while the player was reading
    /// something else; one that waits here with its button lit is a reason to come and
    /// look, and the moment the gold lands is a moment the player chose.
    /// </summary>
    public static class AchievementsScreen
    {
        public static void Build(MenuShell shell, RectTransform root)
        {
            var campaign = Session.Campaign;

            var back = Widgets.Chip("Back", root, Theme.Chevron, shell.ShowMain);
            back.image.rectTransform.Place(new Vector2(0f, 1f),
                new Vector2(Widgets.Margin, -Widgets.Margin), new Vector2(96f, 96f));

            var ribbon = Widgets.Ribbon("Ribbon", root, Loc.T("Achievements"));
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -Widgets.Margin), new Vector2(520f, 100f));

            int done = 0;
            foreach (var a in AchievementTable.All) if (campaign.Achieved(a)) done++;

            var count = Widgets.Label("Count", root,
                Loc.F("{0} of {1} done", done, AchievementTable.All.Length), Widgets.SmallSize, Theme.Muted);
            count.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -170f),
                                      new Vector2(Widgets.SafeWidth, 44f));

            var content = Board(root);
            float y = 0f;

            foreach (var a in AchievementTable.All)
                Row(shell, campaign, content, a, ref y);

            content.sizeDelta = new Vector2(0f, -y + 20f);
        }

        static void Row(MenuShell shell, Campaign campaign, RectTransform content, Achievement a, ref float y)
        {
            bool achieved = campaign.Achieved(a);
            bool claimed = campaign.Claimed(a);
            int target = AchievementTable.Target(a);
            int progress = campaign.Progress(a);

            var plate = Widgets.Panel("Achievement" + a, content, Theme.Frame,
                claimed ? Theme.Secondary : new Color(0.16f, 0.14f, 0.12f));
            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(Widgets.SafeWidth, 170f));

            var title = Widgets.Label("Name", plate.transform, Title(a), Widgets.BodySize - 4,
                                      achieved ? Theme.Parchment : Theme.Muted, TextAnchor.MiddleLeft);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(28f, -14f), new Vector2(520f, 44f));

            var what = Widgets.Label("What", plate.transform, What(a), Widgets.SmallSize - 6,
                                     Theme.Muted, TextAnchor.UpperLeft);
            what.Wrap();
            what.rectTransform.Place(new Vector2(0f, 1f), new Vector2(28f, -58f), new Vector2(520f, 50f));

            // How far along, as a bar and a count, like the shop's tracks.
            var track = Widgets.Panel("Track", plate.transform, Theme.Flat, new Color(0f, 0f, 0f, 0.55f));
            track.type = Image.Type.Simple;
            track.rectTransform.Place(new Vector2(0f, 0f), new Vector2(28f, 26f), new Vector2(380f, 16f));

            var fill = Widgets.Panel("Fill", track.transform, Theme.Flat, Theme.BrightGold);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = target > 0 ? progress / (float)target : 0f;
            fill.rectTransform.Fill(2f, 2f, 2f, 2f);

            var tally = Widgets.Label("Tally", plate.transform, Loc.F("{0} / {1}", progress, target),
                                      Widgets.SmallSize - 8, Theme.Muted, TextAnchor.MiddleLeft);
            tally.rectTransform.Place(new Vector2(0f, 0f), new Vector2(420f, 20f), new Vector2(160f, 30f));

            string reward = Reward(a);

            if (claimed)
            {
                var got = Widgets.Label("Claimed", plate.transform, Loc.T("COLLECTED"), Widgets.SmallSize - 4, Theme.Gold);
                got.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(230f, 60f));
            }
            else
            {
                var button = Widgets.Plate("Claim", plate.transform, reward,
                    achieved ? ButtonRole.Primary : ButtonRole.Disabled,
                    () =>
                    {
                        if (!campaign.TryClaim(a)) return;

                        Session.Save();
                        shell.Show(Build);
                    });

                button.image.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-28f, 0f),
                                                 new Vector2(230f, 92f));
                button.GetComponentInChildren<Text>().fontSize = Widgets.SmallSize - 4;
            }

            y -= 186f;
        }

        /// <summary>The reward as it reads on the button: gold, gems, or both.</summary>
        static string Reward(Achievement a)
        {
            int gold = AchievementTable.Gold(a);
            int gems = AchievementTable.Gems(a);

            if (gold > 0 && gems > 0) return Loc.F("{0} GOLD + {1} GEMS", gold, gems);
            if (gems > 0) return Loc.F("{0} GEMS", gems);
            return Loc.F("{0} GOLD", gold);
        }

        static string Title(Achievement a)
        {
            switch (a)
            {
                case Achievement.FirstRoad: return Loc.T("First Road");
                case Achievement.TenRoads: return Loc.T("Seasoned Guide");
                case Achievement.TwentyFiveRoads: return Loc.T("Road Warden");
                case Achievement.FirstThreeStars: return Loc.T("Clean Crossing");
                case Achievement.TenThreeStars: return Loc.T("Master of the Road");
                case Achievement.ChapterOne: return Loc.T("The Borderlands Crossed");
                case Achievement.ChapterTwo: return Loc.T("The King's Road Crossed");
                case Achievement.GroupsTwentyFive: return Loc.T("Blooded");
                case Achievement.GroupsHundred: return Loc.T("Hundred Fights");
                case Achievement.GroupsTwoFifty: return Loc.T("Terror of the Wilds");
                case Achievement.TrapsTen: return Loc.T("Sapper");
                case Achievement.TrapsFifty: return Loc.T("Master Sapper");
                case Achievement.FlawlessOne: return Loc.T("Not a Scratch");
                case Achievement.FlawlessTen: return Loc.T("Unbroken");
                case Achievement.GoldThousand: return Loc.T("Merchant");
                default: return Loc.T("Merchant Prince");
            }
        }

        static string What(Achievement a)
        {
            int n = AchievementTable.Target(a);

            switch (AchievementTable.MeasureOf(a))
            {
                case Measure.LevelsCleared: return Loc.F("Clear {0} levels.", n);
                case Measure.LevelsAtThreeStars: return Loc.F("Earn three stars on {0} levels.", n);
                case Measure.ChaptersCleared: return Loc.F("Clear every level in {0} chapters.", n);
                case Measure.GroupsBeaten: return Loc.F("Defeat {0} enemy groups.", n);
                case Measure.TrapsDisarmed: return Loc.F("Disarm {0} traps.", n);
                case Measure.FlawlessArrivals: return Loc.F("Arrive {0} times without losing a wagon.", n);
                default: return Loc.F("Earn {0} gold from the road.", n);
            }
        }

        /// <summary>The scrolling list, built the way the shop builds its own.</summary>
        static RectTransform Board(RectTransform root)
        {
            var frame = Widgets.Node("Board", root);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(Widgets.Margin, 60f);
            frame.offsetMax = new Vector2(-Widgets.Margin, -240f);

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
    }
}
