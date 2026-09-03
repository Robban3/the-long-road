using System;
using Arna.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arna.UI
{
    /// <summary>
    /// What is on screen while a level is being played: the top bar, the pause menu, and
    /// the result.
    ///
    /// The run has always known everything here — wagons, kills, silver, distance, stars
    /// — and printed it in a debug readout in the top-left corner. The numbers are the
    /// same; what is new is that the run now *ends* somewhere. Pressing home or next
    /// files the result with <see cref="Session"/>, which is what fills in the roadmap
    /// and opens the level after this one.
    ///
    /// The runner owns the simulation, so the three things this cannot do itself —
    /// restart the level, and know what the level after it is — are handed in as
    /// callbacks rather than reached for across an assembly boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunHud : MonoBehaviour
    {
        /// <summary>The run being watched. Set by the runner, and again on every restart.</summary>
        public LevelRun Run;

        /// <summary>Replays this level from the top, keeping the road that was drawn.</summary>
        public Action Restart;

        public int Chapter = 1;
        public int Level = 1;

        Canvas _canvas;
        RectTransform _screen;
        RectTransform _compass;
        Camera _camera;
        Text _wagons, _kills, _silver, _progress;
        Image _bar;
        GameObject _sheet;
        bool _paused;
        bool _resultShown;

        void Start()
        {
            _canvas = Widgets.Screen("RunHud", transform);
            _screen = Widgets.Node("Screen", _canvas.transform);
            _screen.Fill();

            TopBar();
            Compass();
            Footer();
        }

        /// <summary>
        /// The rose, under the pause chip, and <b>this one turns.</b>
        ///
        /// On the planning map a compass is an ornament: that camera is Euler(90,0,0) and
        /// never moves, so north is up and stays up. Here it is an instrument. The camera
        /// swings round to sit behind the caravan as the road bends
        /// (<c>LevelRunner.AimCamera</c>) and the player can drag it anywhere they like on
        /// top of that (<c>CameraOrbit</c>), so which way the country is facing is a thing
        /// you genuinely lose track of — and the map you planned on had north up.
        ///
        /// Under the top bar rather than in it, matching where the plan screen puts its
        /// own, so the two screens keep the rose in the same place.
        /// </summary>
        void Compass()
        {
            const float size = 84f;

            var rose = Widgets.Panel("Compass", _screen, Theme.CompassIcon, Theme.Muted);
            var rect = rose.rectTransform;

            // Pivoted at its middle rather than by Widgets.Place, which pins pivot to the
            // anchor — and a rose pivoted at its top-right corner does not spin, it swings
            // off the screen. So the anchor stays top-right and the offset is to the
            // centre: half a rose in from the margin, and clear of the 84-tall top band
            // that starts one margin down.
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-Widgets.Margin - size * 0.5f,
                                                -Widgets.Margin - 84f - 20f - size * 0.5f);

            rose.raycastTarget = false;
            _compass = rect;
        }

        /// <summary>
        /// Turns the rose so its north arrow points where north actually is on screen.
        ///
        /// Read off the camera rather than off CameraOrbit, because the yaw has two
        /// sources — the heading the runner aims from and whatever the player has dragged
        /// — and the camera is where they have already been added together.
        ///
        /// The z-rotation is the camera's yaw, not its negative. A yaw of 90 degrees has
        /// the camera looking east, which puts east at the top of the screen and north to
        /// the left; a UI element rotated +90 about z turns anticlockwise, which is exactly
        /// where the arrow needs to go. Worth one glance in play mode all the same: a
        /// compass pointing the wrong way is worse than none, and a sign is a cheap thing
        /// to get backwards.
        /// </summary>
        void AimCompass()
        {
            if (_compass == null) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            _compass.localRotation = Quaternion.Euler(0f, 0f, _camera.transform.eulerAngles.y);
        }

        void OnDestroy()
        {
            // A level left while paused must not leave the next one frozen.
            if (_paused) Time.timeScale = 1f;
        }

        void Update()
        {
            // Before the guard: the camera swings whether or not there is a run to read,
            // and a rose frozen at north on a turned camera is a lie rather than a gap.
            AimCompass();

            if (Run == null) return;

            Refresh();

            if (!_resultShown && Run.Outcome != RunOutcome.InProgress) ShowResult();
        }

        void TopBar()
        {
            var bar = Widgets.Node("TopBar", _screen);
            bar.Band(Widgets.Margin, 84f);

            _wagons = Widgets.Counter("Wagons", bar, Theme.HeartIcon, Theme.Heart, "3/3", null, 190f);
            _wagons.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(190f, 76f));

            _kills = Widgets.Counter("Kills", bar, Theme.SkullIcon, Theme.Bone, "0/0", null, 190f);
            _kills.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0f, 0.5f), new Vector2(206f, 0f), new Vector2(190f, 76f));

            _silver = Widgets.Counter("Silver", bar, Theme.CoinIcon, Theme.Coin, "0", null, 210f);
            _silver.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0f, 0.5f), new Vector2(412f, 0f), new Vector2(210f, 76f));

            var smithy = Widgets.Chip("Smithy", bar, Theme.GemIcon, Upgrades, 84f,
                                      Theme.BrightGold);
            smithy.image.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-98f, 0f),
                                             new Vector2(84f, 84f));

            var pause = Widgets.Chip("Pause", bar, Theme.Flat, Pause, 84f);
            pause.image.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(0f, 0f),
                                            new Vector2(84f, 84f));

            // Two bars, drawn rather than painted as a sprite: a pause glyph is the one
            // icon simple enough that a shape would be more code than two rectangles.
            var glyph = pause.transform.Find("Glyph");
            if (glyph != null) Destroy(glyph.gameObject);

            for (int i = 0; i < 2; i++)
            {
                var stroke = Widgets.Icon("Bar" + i, pause.transform, Theme.Flat, Theme.Parchment, 10f);
                stroke.rectTransform.Place(new Vector2(0.5f, 0.5f),
                    new Vector2(i == 0 ? -11f : 11f, 0f), new Vector2(12f, 38f));
            }
        }

        /// <summary>
        /// The distance bar along the bottom, where the mock-up puts its wave counter.
        ///
        /// Distance is this game's wave count: it is the thing that runs out, and the
        /// player's whole plan — where to spend silver, when to take the marsh — is
        /// judged against how much road is left.
        /// </summary>
        void Footer()
        {
            var frame = Widgets.Panel("Distance", _screen, Theme.SoftFrame, Color.white);
            frame.rectTransform.anchorMin = new Vector2(0f, 0f);
            frame.rectTransform.anchorMax = new Vector2(1f, 0f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0f);
            frame.rectTransform.offsetMin = new Vector2(Widgets.Margin, Widgets.Margin);
            frame.rectTransform.offsetMax = new Vector2(-Widgets.Margin, 0f);
            frame.rectTransform.sizeDelta = new Vector2(0f, 96f);

            _progress = Widgets.Label("Text", frame.transform, "", Widgets.SmallSize,
                                      Theme.Parchment, TextAnchor.UpperLeft);
            _progress.rectTransform.Place(new Vector2(0f, 1f), new Vector2(20f, -10f),
                                          new Vector2(600f, 36f));

            var track = Widgets.Panel("Track", frame.transform, Theme.Flat, new Color(0f, 0f, 0f, 0.6f));
            track.type = Image.Type.Simple;
            track.rectTransform.anchorMin = new Vector2(0f, 0f);
            track.rectTransform.anchorMax = new Vector2(1f, 0f);
            track.rectTransform.pivot = new Vector2(0.5f, 0f);
            track.rectTransform.offsetMin = new Vector2(20f, 18f);
            track.rectTransform.offsetMax = new Vector2(-20f, 0f);
            track.rectTransform.sizeDelta = new Vector2(0f, 20f);

            _bar = Widgets.Panel("Fill", track.transform, Theme.Flat, Theme.Coin);
            _bar.type = Image.Type.Filled;
            _bar.fillMethod = Image.FillMethod.Horizontal;
            _bar.fillAmount = 0f;
            _bar.rectTransform.Fill(2f, 2f, 2f, 2f);
        }

        void Refresh()
        {
            int standing = 0;
            foreach (var wagon in Run.Caravan.Wagons)
                if (!wagon.Destroyed) standing++;

            _wagons.text = standing + "/" + Run.Caravan.Wagons.Count;

            int beaten = 0;
            foreach (var enemy in Run.Detection.Enemies)
                if (Run.Combat.IsDefeated(enemy)) beaten++;

            _kills.text = beaten + "/" + Run.Detection.Enemies.Count;
            _silver.text = Run.Economy.Silver.ToString();

            _bar.fillAmount = Mathf.Clamp01(Run.Caravan.Progress);
            _progress.text = $"{Run.Caravan.Progress:P0} av vägen   ·   {Ground(Run.Caravan.CurrentTerrain)}" +
                             $"   ·   {Run.TravelSeconds:F0} s (par {Run.ParSeconds:F0} s)";
        }

        /// <summary>Swedish for the ground underfoot. The rest of the interface is.</summary>
        static string Ground(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Road: return "väg";
                case TerrainType.Plains: return "slätt";
                case TerrainType.Forest: return "skog";
                case TerrainType.Marsh: return "träsk";
                case TerrainType.Ford: return "vadställe";
                case TerrainType.MountainPass: return "bergspass";
                case TerrainType.Water: return "vatten";
                case TerrainType.Cliff: return "brant";
                default: return terrain.ToString();
            }
        }

        // ---- pause -------------------------------------------------------------

        public void Pause()
        {
            if (_paused || _resultShown) return;

            _paused = true;
            Time.timeScale = 0f;

            var sheet = Sheet("Paus");
            var panel = (RectTransform)sheet.transform.GetChild(1);

            float y = -150f;
            Row(panel, ref y, "FORTSÄTT", ButtonRole.Resume, Resume);
            Row(panel, ref y, "BÖRJA OM", ButtonRole.Restart, () => { Resume(); Restart?.Invoke(); });
            Row(panel, ref y, "RITA OM VÄGEN", ButtonRole.Secondary, () =>
            {
                Resume();
                SceneManager.LoadScene(Session.PlanScene);
            });
            Row(panel, ref y, "AVSLUTA", ButtonRole.Exit, GoHome);
        }

        public void Resume()
        {
            _paused = false;
            Time.timeScale = 1f;

            if (_sheet != null) Destroy(_sheet);
        }

        // ---- the smithy --------------------------------------------------------

        /// <summary>
        /// Where silver is spent, mid-run.
        ///
        /// This is the same six posts and three tracks the debug readout used to list in
        /// the corner of the screen, which is worth saying plainly: the upgrade economy
        /// was built, priced and read by every step of the fighting long before there was
        /// any way to press it. What it lacked was a button. It has one now, in the shape
        /// the mock-up asks for — a stat panel with the price on the action.
        ///
        /// Rebuilt after each purchase rather than updated in place. Six rows of three is
        /// small enough that the difference is unmeasurable, and a panel that is only
        /// ever built one way cannot drift out of step with what was bought.
        /// </summary>
        public void Upgrades()
        {
            if (_resultShown) return;

            _paused = true;
            Time.timeScale = 0f;

            var sheet = Sheet("Smedjan");
            var panel = (RectTransform)sheet.transform.GetChild(1);

            var purse = Widgets.Counter("Purse", panel, Theme.CoinIcon, Theme.Coin,
                                        Run.Economy.Silver + " silver", null, 320f);
            purse.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(320f, 72f));

            float y = -230f;
            foreach (var group in Run.Squad.Slots)
            {
                if (group == null) continue;

                Post(panel, group, ref y);
            }

            var close = Widgets.Plate("Close", panel, "TILLBAKA", ButtonRole.Primary, Resume);
            close.image.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                                            new Vector2(Widgets.SafeWidth - 220f, Widgets.ButtonHeight));
        }

        void Post(RectTransform panel, TroopGroup group, ref float y)
        {
            var row = Widgets.Panel("Post" + group.Slot, panel, Theme.SoftFrame, Color.white);
            row.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(Widgets.SafeWidth - 60f, 108f));

            var name = Widgets.Label("Name", row.transform, Name(group.Kind), Widgets.SmallSize,
                                     group.Alive ? Theme.Parchment : Theme.Dim, TextAnchor.MiddleLeft);
            name.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(18f, 6f), new Vector2(210f, 46f));

            // The reach in metres, under the name — the same number the ring on the
            // ground is drawn at, so the panel and the picture agree about what this
            // group can hit.
            float reach = Run.Combat.Reach(group, Run.Caravan.CurrentTerrain);

            var span = Widgets.Label("Reach", row.transform, $"räckvidd {reach:F0} m",
                                     Widgets.SmallSize - 8, Theme.Muted, TextAnchor.MiddleLeft);
            span.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(18f, -26f),
                                     new Vector2(210f, 34f));

            Track(row.transform, group, UpgradeTrack.Weapon, "VAPEN", 240f);
            Track(row.transform, group, UpgradeTrack.Armour, "SKYDD", 420f);

            // A bow's special track *is* its reach, and is priced at over half again as
            // much for it. Saying so on the button is the difference between an upgrade
            // the player understands and one they buy last.
            Track(row.transform, group, UpgradeTrack.Special,
                  TroopTable.HasRangedSpecial(group.Kind) ? "RÄCKV" : "SPEC", 600f);

            y -= 118f;
        }

        void Track(Transform row, TroopGroup group, UpgradeTrack track, string label, float x)
        {
            int level = group.UpgradeLevel(track);

            if (level >= RunEconomy.MaxTrackLevel)
            {
                var capped = Widgets.Label(label, row, label + "\n" + level + "/5", Widgets.SmallSize - 6,
                                           Theme.Gold);
                capped.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(170f, 80f));
                return;
            }

            int price = Run.PriceOf(group.Slot, track);
            bool affordable = Run.Economy.Silver >= price && group.Alive;

            var button = Widgets.Plate(label, row, label + " " + level + "\u2192" + (level + 1) + "\n" + price,
                                       affordable ? ButtonRole.Secondary : ButtonRole.Disabled,
                                       () =>
                                       {
                                           if (Run.TryUpgrade(group.Slot, track, out _)) Upgrades();
                                       });

            button.image.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(x, 0f),
                                             new Vector2(170f, 84f));

            var text = button.GetComponentInChildren<Text>();
            text.fontSize = Widgets.SmallSize - 6;
        }

        /// <summary>Swedish names for the posts, so the panel reads like the rest of the game.</summary>
        static string Name(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Spearmen: return "Spjutmän";
                case TroopKind.Swordsmen: return "Svärdsmän";
                case TroopKind.Archers: return "Bågskyttar";
                case TroopKind.Shieldbearer: return "Sköldbärare";
                case TroopKind.Scout: return "Spejare";
                case TroopKind.Engineer: return "Ingenjör";
                case TroopKind.Cavalry: return "Ryttare";
                case TroopKind.Mage: return "Magiker";
                case TroopKind.Priest: return "Präst";
                default: return kind.ToString();
            }
        }

        // ---- result ------------------------------------------------------------

        /// <summary>
        /// Files the run and shows what it was worth.
        ///
        /// Filed here rather than on the button, so a player who closes the game on the
        /// victory screen has still banked the stars they earned.
        /// </summary>
        void ShowResult()
        {
            _resultShown = true;

            bool won = Run.Outcome == RunOutcome.Arrived;
            int stars = Run.Stars;
            int gold = Run.GoldEarned();

            Session.Choose(Chapter, Level);
            Session.Finish(stars, gold);

            var sheet = Sheet(won ? "Seger" : "Nederlag",
                              won ? Backdrops.Victory : Backdrops.Defeat);
            var panel = (RectTransform)sheet.transform.GetChild(1);

            var row = Widgets.Stars("Stars", panel, stars, Campaign.MaxStars, 128f, 18f);
            row.Place(new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(460f, 130f));

            string verdict = !won ? "Karavanen gick förlorad."
                           : Session.LastWasBest ? "Bästa resultatet hittills!"
                           : "Klarat — ditt rekord står kvar.";

            var note = Widgets.Label("Note", panel, verdict, Widgets.SmallSize, Theme.Muted);
            note.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(Widgets.SafeWidth - 140f, 40f));

            Reward(panel, -230f, Theme.CoinIcon, Theme.Coin, gold.ToString(), "GULD");
            Reward(panel, 0f, Theme.SkullIcon, Theme.Bone, Beaten() + "", "SLAGNA");
            Reward(panel, 230f, Theme.HeartIcon, Theme.Heart, Standing() + "", "VAGNAR");

            float y = -640f;
            if (won && Session.HasNext(out int nextChapter, out int nextLevel))
            {
                Row(panel, ref y, "NÄSTA NIVÅ", ButtonRole.Primary, () =>
                {
                    Session.Choose(nextChapter, nextLevel);
                    Session.Forget();
                    SceneManager.LoadScene(Session.PlanScene);
                });
            }

            // Second, right under the primary, because that is where the eye goes next
            // and because this is the moment the gold above it means something. The shop
            // was reachable before only by leaving the flow — result screen, map, front
            // page, shop — so the natural run of presses never passed it and the player
            // was never once asked to spend what they had earned.
            Row(panel, ref y, "UPPGRADERA", ButtonRole.Secondary, () =>
            {
                Session.OpenShop = true;
                GoHome();
            });

            Row(panel, ref y, won ? "SPELA OM" : "FÖRSÖK IGEN", ButtonRole.Secondary, () =>
            {
                if (Restart == null) SceneManager.LoadScene(Session.PlanScene);
                else { Destroy(_sheet); _resultShown = false; Restart(); }
            });

            Row(panel, ref y, "TILL KARTAN", ButtonRole.Secondary, GoHome);
        }

        int Beaten()
        {
            int beaten = 0;
            foreach (var enemy in Run.Detection.Enemies)
                if (Run.Combat.IsDefeated(enemy)) beaten++;

            return beaten;
        }

        int Standing()
        {
            int standing = 0;
            foreach (var wagon in Run.Caravan.Wagons)
                if (!wagon.Destroyed) standing++;

            return standing;
        }

        void GoHome()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(Session.MenuScene);
        }

        /// <summary>A dimmed screen with a framed panel and a ribbon title on it.</summary>
        GameObject Sheet(string title) => Sheet(title, null);

        GameObject Sheet(string title, string backdrop)
        {
            if (_sheet != null) Destroy(_sheet);

            var host = Widgets.Node("Sheet", _screen);
            host.Fill();
            _sheet = host.gameObject;

            // A painting behind the result, when there is one, in place of the plain
            // dimming. Winning a level should not look like pausing one.
            if (backdrop == null || Backdrops.Paint(backdrop, host, 0.5f) == null)
                Widgets.Scrim("Scrim", host);

            var panel = Widgets.Panel("Panel", host, Theme.Frame, Color.white);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Widgets.SafeWidth, 1100f));

            var ribbon = Widgets.Ribbon("Ribbon", panel.transform, title);
            ribbon.transform.parent.GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(Widgets.SafeWidth - 220f, 110f));

            return host.gameObject;
        }

        static void Row(RectTransform panel, ref float y, string text, ButtonRole role, Action clicked)
        {
            var button = Widgets.Plate(text, panel, text, role, () => clicked());
            button.image.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, y),
                                             new Vector2(Widgets.SafeWidth - 220f, Widgets.ButtonHeight));
            y -= Widgets.ButtonHeight + 20f;
        }

        static void Reward(RectTransform panel, float x, Sprite icon, Color tint, string value,
                           string caption)
        {
            var slot = Widgets.Panel("Reward" + caption, panel, Theme.SoftFrame, Color.white);
            slot.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(x, -400f),
                                     new Vector2(200f, 200f));

            var glyph = Widgets.Icon("Glyph", slot.transform, icon, tint, 80f);
            glyph.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(80f, 80f));

            var number = Widgets.Label("Value", slot.transform, value, Widgets.BodySize, Theme.Parchment);
            number.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(190f, 44f));

            var name = Widgets.Label("Caption", slot.transform, caption, Widgets.SmallSize - 4, Theme.Muted);
            name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(190f, 34f));
        }
    }
}
