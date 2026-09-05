using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.View
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

        /// <summary>
        /// Colours for the ground in the play view, which is a different job.
        ///
        /// The map palette separates types as far as it can, because on a map colour
        /// is the only thing carrying the information. On the ground that same
        /// separation renders every four-metre tile as a distinct slab and the
        /// landscape comes out tiled. Here the terrain announces itself by what stands
        /// on it — you know it is forest because there are trees — so the ground only
        /// has to be plausible earth, and these values sit close together on purpose.
        /// </summary>
        static readonly Color[] GroundColors =
        {
            new Color(0.50f, 0.43f, 0.33f), // Road          — packed earth
            new Color(0.42f, 0.50f, 0.29f), // Plains        — open grass
            // Close to the plains, and that is the fix rather than the compromise. It
            // was a good deal darker, and a wood standing in open country then read as a
            // dark green stain on the ground with a hard tile edge round it — the "green
            // blob" that survived every prop being taken off the map, because it was
            // never a prop. What says forest is the canopy. The floor under it is the
            // same earth as the field beside it, a shade darker for the shade.
            new Color(0.38f, 0.46f, 0.28f), // Forest        — shaded floor
            new Color(0.34f, 0.37f, 0.27f), // Marsh         — wet ground
            new Color(0.40f, 0.46f, 0.42f), // Ford          — wet gravel
            new Color(0.46f, 0.44f, 0.40f), // MountainPass  — bare rock
            // Darker and bluer than the rest by a wide margin. Everything else on the
            // ground is allowed to blend into its neighbours, but water is impassable:
            // a player who cannot see where the river runs cannot plan a route around
            // it. Averaged against grass at the shore it has to survive the average.
            new Color(0.13f, 0.24f, 0.34f), // Water         — deep, and must read as deep
            new Color(0.33f, 0.31f, 0.29f)  // Cliff         — stone
        };

        public static readonly Color Start = new Color(0.35f, 0.95f, 0.45f);
        public static readonly Color Goal = new Color(0.98f, 0.82f, 0.25f);

        /// <summary>The three corridors, kept far apart in hue so overlap is obvious.</summary>
        public static readonly Color RouteFast = new Color(0.98f, 0.34f, 0.30f);
        public static readonly Color RouteSafe = new Color(0.40f, 0.85f, 0.98f);
        public static readonly Color RouteOdd = new Color(0.98f, 0.72f, 0.24f);

        public static Color Of(TerrainType t) => Colors[(int)t];

        public static Color OfGround(TerrainType t) => GroundColors[(int)t];
    }
}
