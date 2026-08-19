using Arna.Sim;
using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// Colours for the planning overview. The player must be able to tell terrain
    /// apart at a glance on a phone screen, so neighbouring types are separated by
    /// brightness as well as hue — colour alone fails for the ~8 % of players with
    /// red-green colour vision deficiency.
    /// </summary>
    public static class TerrainPalette
    {
        static readonly Color[] Colors =
        {
            new Color(0.72f, 0.62f, 0.42f), // Road          — packed earth
            new Color(0.58f, 0.72f, 0.38f), // Plains        — open grass
            new Color(0.20f, 0.40f, 0.23f), // Forest        — dark canopy
            new Color(0.36f, 0.38f, 0.24f), // Marsh         — murky olive
            new Color(0.48f, 0.68f, 0.70f), // Ford          — shallow crossing
            new Color(0.58f, 0.55f, 0.52f), // MountainPass  — bare rock
            new Color(0.16f, 0.31f, 0.52f), // Water         — impassable deep
            new Color(0.28f, 0.26f, 0.26f)  // Cliff         — impassable stone
        };

        public static readonly Color Start = new Color(0.35f, 0.95f, 0.45f);
        public static readonly Color Goal = new Color(0.98f, 0.82f, 0.25f);

        /// <summary>The three corridors, kept far apart in hue so overlap is obvious.</summary>
        public static readonly Color RouteFast = new Color(0.98f, 0.34f, 0.30f);
        public static readonly Color RouteSafe = new Color(0.40f, 0.85f, 0.98f);
        public static readonly Color RouteOdd = new Color(0.98f, 0.72f, 0.24f);

        public static Color Of(TerrainType t) => Colors[(int)t];
    }
}
