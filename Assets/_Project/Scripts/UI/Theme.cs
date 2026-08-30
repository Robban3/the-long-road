using UnityEngine;

namespace Arna.UI
{
    /// <summary>
    /// The look of the menus: colours, and the sprites they are drawn with.
    ///
    /// Every sprite here is painted in code at start-up rather than imported. That is a
    /// deliberate trade and worth stating, because the obvious way to build this screen
    /// is to draw nine-slices in an image editor and wire them up in a prefab.
    ///
    /// This project builds its scenes from <c>ArnaSetup</c> — the terrain, the props,
    /// the caravan and the markers are all made by code that can be read, reviewed and
    /// re-run. A menu made of imported PNGs and a hand-wired prefab is the one part
    /// nobody could review, nobody could regenerate, and which would quietly rot every
    /// time the scene file was touched. Painted frames also recolour for free: the
    /// primary, secondary and disabled buttons in the mock-up are one frame and three
    /// palettes.
    ///
    /// The whole set costs a few hundred kilobytes of texture and about a millisecond,
    /// once, and is created lazily so a scene that shows no menu pays nothing.
    /// </summary>
    public static class Theme
    {
        // The mock-up's palette: near-black leather and tarnished gold, with one warm
        // red reserved for the single action a screen actually wants.
        public static readonly Color Ink        = new Color32(0x14, 0x0F, 0x0B, 0xFF);
        public static readonly Color Backdrop   = new Color32(0x1C, 0x15, 0x0F, 0xFF);
        public static readonly Color PanelFill  = new Color32(0x26, 0x1C, 0x14, 0xFF);
        public static readonly Color PanelEdge  = new Color32(0x8A, 0x6C, 0x3C, 0xFF);
        public static readonly Color Gold       = new Color32(0xC9, 0xA1, 0x55, 0xFF);
        public static readonly Color BrightGold = new Color32(0xE8, 0xC8, 0x7C, 0xFF);

        public static readonly Color Parchment  = new Color32(0xE9, 0xDA, 0xB8, 0xFF);
        public static readonly Color Muted      = new Color32(0xA8, 0x96, 0x77, 0xFF);
        public static readonly Color Dim        = new Color32(0x6B, 0x5E, 0x4C, 0xFF);

        public static readonly Color Primary    = new Color32(0x7C, 0x2B, 0x24, 0xFF);
        public static readonly Color Secondary  = new Color32(0x2E, 0x24, 0x1A, 0xFF);
        public static readonly Color Disabled   = new Color32(0x24, 0x20, 0x1C, 0xFF);
        public static readonly Color Resume     = new Color32(0x3C, 0x55, 0x2A, 0xFF);
        public static readonly Color Restart    = new Color32(0x27, 0x3E, 0x5C, 0xFF);

        public static readonly Color Ribbon     = new Color32(0x8E, 0x2A, 0x22, 0xFF);
        public static readonly Color Coin       = new Color32(0xE0, 0xA9, 0x3B, 0xFF);
        public static readonly Color Gem        = new Color32(0x4F, 0xCF, 0x7A, 0xFF);
        public static readonly Color Heart      = new Color32(0xC8, 0x40, 0x2F, 0xFF);
        public static readonly Color Bone       = new Color32(0xD8, 0xCF, 0xBA, 0xFF);
        public static readonly Color Danger     = new Color32(0xC4, 0x5A, 0x30, 0xFF);

        static Font _font;
        static Sprite _frame, _frameSoft, _round, _slab, _star, _lock, _flat, _banner;
        static Sprite _coin, _gem, _heart, _skull, _chevron, _gear;

        /// <summary>
        /// The built-in font. Legacy <see cref="UnityEngine.UI.Text"/> rather than
        /// TextMeshPro, because TMP needs its essential-resources package imported into
        /// the project before a single label can be drawn, and this menu should work in
        /// a fresh clone with nothing but the repository.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                return _font;
            }
        }

        /// <summary>The standard bordered panel: dark leather inside a double gold edge.</summary>
        public static Sprite Frame => _frame != null ? _frame : _frame = Pixels.Frame(PanelFill, PanelEdge, "ArnaFrame");

        /// <summary>The same frame with a thinner, quieter edge, for the many small chips.</summary>
        public static Sprite SoftFrame => _frameSoft != null ? _frameSoft
            : _frameSoft = Pixels.Frame(PanelFill, new Color(PanelEdge.r, PanelEdge.g, PanelEdge.b, 0.55f), "ArnaFrameSoft", thin: true);

        /// <summary>Untextured white, for tints and dividers.</summary>
        public static Sprite Flat => _flat != null ? _flat : _flat = Pixels.Solid(Color.white, "ArnaFlat");

        /// <summary>The medallion a level number sits in on the roadmap.</summary>
        public static Sprite Round => _round != null ? _round : _round = Pixels.Medallion("ArnaRound");

        /// <summary>One stone of the winding path between levels.</summary>
        public static Sprite Slab => _slab != null ? _slab : _slab = Pixels.Slab("ArnaSlab");

        public static Sprite Star => _star != null ? _star : _star = Pixels.Star("ArnaStar");
        public static Sprite Padlock => _lock != null ? _lock : _lock = Pixels.Padlock("ArnaLock");
        public static Sprite Banner => _banner != null ? _banner : _banner = Pixels.Banner("ArnaBanner");

        public static Sprite CoinIcon => _coin != null ? _coin : _coin = Pixels.Coin("ArnaCoin");
        public static Sprite GemIcon => _gem != null ? _gem : _gem = Pixels.Gem("ArnaGem");
        public static Sprite HeartIcon => _heart != null ? _heart : _heart = Pixels.Heart("ArnaHeart");
        public static Sprite SkullIcon => _skull != null ? _skull : _skull = Pixels.Skull("ArnaSkull");
        public static Sprite Chevron => _chevron != null ? _chevron : _chevron = Pixels.Chevron("ArnaChevron");
        public static Sprite Gear => _gear != null ? _gear : _gear = Pixels.Gear("ArnaGear");

        /// <summary>The fill a button of this role is painted with.</summary>
        public static Color Fill(ButtonRole role)
        {
            switch (role)
            {
                case ButtonRole.Primary: return Primary;
                case ButtonRole.Resume: return Resume;
                case ButtonRole.Restart: return Restart;
                case ButtonRole.Exit: return Primary;
                case ButtonRole.Disabled: return Disabled;
                default: return Secondary;
            }
        }

        public static Color Ident(ButtonRole role)
            => role == ButtonRole.Disabled ? Dim : Parchment;
    }

    public enum ButtonRole
    {
        Primary,
        Secondary,
        Disabled,
        Resume,
        Restart,
        Exit
    }
}
