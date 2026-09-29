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

        /// <summary>
        /// The same ground in winter: snow over it, and what shows through.
        ///
        /// Not white, and that is arithmetic rather than taste. The ground shader takes
        /// every bit of its colour from these values and multiplies grain on top — up to
        /// nearly twice as bright in the lightest patches before the light is added — so a
        /// snow entered at 1.0 would burn out to flat white across whole tiles and lose
        /// both the grain and the shading that make it read as a surface. These sit in
        /// the upper-middle, with room above them for the sun.
        ///
        /// The differences between types are kept, just moved up the scale: a road is
        /// trodden snow with earth in it and reads as a track; a pass is wind-scoured and
        /// shows rock; a wood is snow in shade. Water is the one that must still stand
        /// apart, for the reason it does in summer — a river the player cannot see is a
        /// river they cannot plan round — so it is ice, bluer and darker than any snow.
        /// </summary>
        static readonly Color[] WinterGroundColors =
        {
            new Color(0.62f, 0.60f, 0.58f), // Road          — trodden snow, earth in it
            new Color(0.80f, 0.82f, 0.85f), // Plains        — open snow
            new Color(0.74f, 0.77f, 0.80f), // Forest        — snow in shade
            new Color(0.66f, 0.69f, 0.70f), // Marsh         — frozen bog
            new Color(0.60f, 0.66f, 0.71f), // Ford          — ice at the edges
            new Color(0.64f, 0.64f, 0.66f), // MountainPass  — scoured rock and drift
            new Color(0.40f, 0.55f, 0.70f), // Water         — ice, and must read as not-ground
            new Color(0.40f, 0.40f, 0.43f)  // Cliff         — stone the snow will not hold
        };

        /// <summary>
        /// The plains, which are greener than the forest and yellower.
        ///
        /// <b>A meadow is not a clearing.</b> The forest's floor is the shaded earth a
        /// canopy stands on, and the plains were drawn on it - so an open country came out
        /// the colour of woodland with the trees taken away. This is grass in the light:
        /// brighter, and with the yellow in it that summer grass has.
        /// </summary>
        static readonly Color[] PlainsGroundColors =
        {
            new Color(0.56f, 0.47f, 0.33f), // Road          — packed earth, drier
            new Color(0.52f, 0.63f, 0.30f), // Plains        — meadow in the light
            new Color(0.44f, 0.55f, 0.29f), // Forest        — a stand of trees in it
            new Color(0.40f, 0.48f, 0.30f), // Marsh         — a damp hollow
            new Color(0.46f, 0.52f, 0.45f), // Ford          — wet gravel
            new Color(0.56f, 0.53f, 0.46f), // MountainPass  — pale stone
            new Color(0.20f, 0.36f, 0.48f), // Water
            new Color(0.52f, 0.49f, 0.43f)  // Cliff         — the pale bluffs
        };

        /// <summary>The farmland: grass grazed and cropped, and more bare earth in it.</summary>
        static readonly Color[] FarmlandGroundColors =
        {
            new Color(0.54f, 0.45f, 0.32f), // Road
            new Color(0.50f, 0.58f, 0.29f), // Plains        — pasture
            new Color(0.41f, 0.50f, 0.28f), // Forest        — the woodlot
            new Color(0.38f, 0.45f, 0.29f), // Marsh
            new Color(0.44f, 0.50f, 0.43f), // Ford
            new Color(0.50f, 0.48f, 0.43f), // MountainPass
            new Color(0.20f, 0.36f, 0.48f), // Water
            new Color(0.46f, 0.44f, 0.40f)  // Cliff
        };

        /// <summary>
        /// The three roads, painted into the ground the caravan drives over.
        ///
        /// <b>A road is a line, not a scatter.</b> It was laid as separate worn patches and
        /// from above it read as a dotted line of brown blots; every reference picture of
        /// this country has one continuous sandy track winding through the grass. Painted
        /// into the ground mesh it is continuous by construction, and the patches on top of
        /// it are gravel rather than the road itself.
        /// </summary>
        public static readonly Color Track = new Color(0.66f, 0.57f, 0.40f);

        /// <summary>
        /// The mountains: stone, scree and the little grass that holds on between them.
        ///
        /// Everything here is a shade of the rock it stands on. The passes are bare, the
        /// open ground is thin turf over gravel, and what trees there are stand in pockets
        /// of darker soil - so the country reads as height and stone from the air, which is
        /// what it is.
        /// </summary>
        static readonly Color[] MountainGroundColors =
        {
            new Color(0.52f, 0.48f, 0.42f), // Road          — gravel
            new Color(0.48f, 0.50f, 0.38f), // Plains        — thin turf
            new Color(0.38f, 0.42f, 0.32f), // Forest        — pine shade
            new Color(0.40f, 0.44f, 0.38f), // Marsh         — a wet hollow
            new Color(0.46f, 0.50f, 0.48f), // Ford          — wet stone
            new Color(0.56f, 0.54f, 0.50f), // MountainPass  — bare rock
            new Color(0.18f, 0.34f, 0.46f), // Water
            new Color(0.50f, 0.47f, 0.44f)  // Cliff
        };

        public static readonly Color Start = new Color(0.35f, 0.95f, 0.45f);
        public static readonly Color Goal = new Color(0.98f, 0.82f, 0.25f);

        /// <summary>The three corridors, kept far apart in hue so overlap is obvious.</summary>
        public static readonly Color RouteFast = new Color(0.98f, 0.34f, 0.30f);
        public static readonly Color RouteSafe = new Color(0.40f, 0.85f, 0.98f);
        public static readonly Color RouteOdd = new Color(0.98f, 0.72f, 0.24f);

        /// <summary>
        /// The same ground in the fen: browner, wetter, and all of it closer together.
        ///
        /// A bog is not a forest with more marsh tiles in it, and the ground is where that
        /// shows. Everything here is a shade of peat — the dry ground is rank rather than
        /// green, the wood floor is dark and sodden, the track is mud — so the tile types
        /// sit nearer each other than they do in summer, which is what a country with no
        /// dry ground in it looks like from above.
        ///
        /// The water is the exception, as it is in every palette: it stays far darker than
        /// its banks, because a player who cannot see where the water lies cannot plan a
        /// route round it. Green-black rather than blue-black — peat water, not a river.
        /// </summary>
        static readonly Color[] MarshGroundColors =
        {
            // Darker than a first reading suggests, because the ground shader multiplies
            // grain on top — up to nearly twice as bright in the lightest patches. Ground
            // the props are cleared off, which is exactly the line the caravan drives
            // along, is where that shows: at the first values the drive line came out a
            // pale band through a dark country, as if the road were paved.
            new Color(0.20f, 0.18f, 0.13f), // Road          — mud track
            new Color(0.19f, 0.23f, 0.14f), // Plains        — rank wet meadow
            new Color(0.16f, 0.19f, 0.12f), // Forest        — sodden floor
            new Color(0.14f, 0.17f, 0.12f), // Marsh         — peat
            new Color(0.17f, 0.20f, 0.17f), // Ford          — churned crossing
            new Color(0.28f, 0.27f, 0.23f), // MountainPass  — wet rock
            new Color(0.08f, 0.12f, 0.10f), // Water         — peat water, and must read as deep
            new Color(0.19f, 0.19f, 0.17f)  // Cliff         — damp stone
        };

        public static Color Of(TerrainType t) => Colors[(int)t];

        public static Color OfGround(TerrainType t) => GroundColors[(int)t];

        /// <summary>
        /// The ground colour for a tile in the country the chapter is set in.
        ///
        /// A country nobody has painted yet gets the forest's, which is the same fallback
        /// its scenery gets (see BiomeLook): a chapter set in a desert plays over desert
        /// ground in woodland colours until somebody paints the sand.
        /// </summary>
        /// <summary>
        /// The ground inside a town's walls, which is laid rather than grown.
        ///
        /// Two greys and nothing else: the streets a pale, worn cobble and the blocks the
        /// buildings stand on a darker one. The blocks are cliff underneath — that is the
        /// terrain the stamp had to hand for ground nothing walks through — and cliff is
        /// painted the brown of bare rock, so the middle of the town came out as a field
        /// of mud with houses standing in it.
        ///
        /// Not a texture, because the ground has none anywhere else on the map: the whole
        /// surface is vertex colour, and a town that suddenly had a material would be the
        /// one place in the world that did.
        /// </summary>
        public static Color OfTown(TerrainType t)
        {
            switch (t)
            {
                // What the buildings stand on.
                case TerrainType.Cliff: return new Color(0.29f, 0.28f, 0.27f);

                // And the streets between them, worn lighter by everything that uses them.
                default: return new Color(0.58f, 0.57f, 0.54f);
            }
        }

        /// <summary>
        /// The coast: grass burnt pale by salt and wind, and stone bleached with it.
        ///
        /// Drier and yellower than the plains without being the desert - what grows on a
        /// headland is marram and thin turf, and the reference picture is a green that has
        /// had the sun on it all summer.
        /// </summary>
        static readonly Color[] CoastGroundColors =
        {
            new Color(0.62f, 0.55f, 0.40f), // Road          — sand trodden into the track
            new Color(0.53f, 0.60f, 0.33f), // Plains        — thin salt turf
            new Color(0.40f, 0.50f, 0.30f), // Forest        — pine on the headland
            new Color(0.44f, 0.50f, 0.34f), // Marsh         — salt marsh behind the dunes
            new Color(0.58f, 0.57f, 0.47f), // Ford          — wet sand
            new Color(0.62f, 0.60f, 0.53f), // MountainPass  — bleached stone
            new Color(0.10f, 0.32f, 0.46f), // Water         — the sea, under the surface
            new Color(0.60f, 0.57f, 0.50f)  // Cliff         — the pale stacks
        };

        /// <summary>
        /// The sand a shore is made of.
        ///
        /// <b>Not a terrain type, because it is not one.</b> Sand is where the land meets
        /// the water, which is a fact about a tile's neighbours rather than about the
        /// tile; making it a type would put it in the cost table, the encounter tables and
        /// the corridor search, all of which would have to be told that it is ordinary
        /// ground. It is a colour laid over the ground colour within a few tiles of water
        /// - see TerrainMeshBuilder.
        /// </summary>
        public static readonly Color Sand = new Color(0.86f, 0.79f, 0.58f);

        /// <summary>
        /// The shallows: sand seen through a foot of clear water.
        ///
        /// The one colour that makes a sea read as a sea rather than as a large pond. Deep
        /// water is nearly black at this angle and the eye reads a dark shape as a hole;
        /// what says "this is the sea" is the band of pale green-blue along the shore,
        /// where the bottom still shows. It is laid the way the sand is - by how far the
        /// water is from land - so a bay is pale all over and the open water goes dark.
        /// </summary>
        public static readonly Color Shallows = new Color(0.36f, 0.78f, 0.78f);

        /// <summary>
        /// The desert: sand, and the rock the sand came off.
        ///
        /// Pale enough to read as hot and not so pale that the props on it disappear -
        /// everything the arid pack draws is the same family of ochres, so the ground has
        /// to sit under them rather than beside them. What the country is short of is
        /// contrast, so the dry wash and the rock are pushed apart: the road is bleached
        /// dust and the broken ground is a darker red-brown.
        /// </summary>
        static readonly Color[] DesertGroundColors =
        {
            new Color(0.74f, 0.58f, 0.40f), // Road          — dust beaten pale
            new Color(0.71f, 0.52f, 0.33f), // Plains        — open sand, ochre rather than straw
            new Color(0.58f, 0.45f, 0.28f), // Forest        — scrub, which is darker than sand
            new Color(0.56f, 0.44f, 0.30f), // Marsh         — a dry wash with something in it
            new Color(0.62f, 0.52f, 0.38f), // Ford          — wet sand where there is any
            new Color(0.60f, 0.38f, 0.26f), // MountainPass  — broken red rock
            new Color(0.18f, 0.46f, 0.50f), // Water         — an oasis, and the only cool colour
            new Color(0.66f, 0.45f, 0.30f)  // Cliff         — sandstone
        };

        /// <summary>
        /// The dead land: ash over rock, and the one thing on it that is not grey.
        ///
        /// <b>Every other country in this game is a colour.</b> The forest is green, the
        /// winter white, the desert ochre, the coast blue — and a country that used to be
        /// one of those and is not any more cannot be a colour, or it is just another
        /// place. So the whole table is grey: cold ash on the open ground, the ash beaten
        /// darker where the road runs through it, and bare scorched rock where the fire
        /// reached the stone.
        ///
        /// Which leaves the water, and that is the chapter. The recipe gives this country
        /// a tenth of its ground as water (ChapterRecipe, Biome.Dead), and in a burnt land
        /// there is no water: what is in the hollows is sulphur, and it is the only warm
        /// thing anywhere on the map. A player who has walked nine countries has never
        /// seen anything glow.
        ///
        /// The marsh goes with it — a fen here is the ground around a sulphur pool, which
        /// is crusted yellow rather than green — and the ford is the crust you cross on.
        /// </summary>
        static readonly Color[] DeadGroundColors =
        {
            new Color(0.30f, 0.28f, 0.27f), // Road          — ash beaten flat
            new Color(0.38f, 0.36f, 0.34f), // Plains        — cold ash, the colour of the whole country
            new Color(0.29f, 0.27f, 0.25f), // Forest        — burnt woodland, darker for the char
            // <b>Crust, not paint.</b> These were mixed two shades too light and a third
            // too saturated, and the first picture of the country came back with lemon
            // spilled across it: vertex colour is interpolated between tile corners, so a
            // bright tile does not stay on its tile - it bleeds a tile in every direction,
            // and at this saturation the bleed is the thing you see. Dulled until the
            // crust belongs to the ash it lies on and the pool is the only thing that
            // carries the colour.
            new Color(0.40f, 0.35f, 0.21f), // Marsh         — sulphur crust round a pool
            new Color(0.45f, 0.39f, 0.22f), // Ford          — the crust you can cross on
            new Color(0.34f, 0.30f, 0.29f), // MountainPass  — scorched stone
            new Color(0.46f, 0.31f, 0.10f), // Water         — the bed under the sulphur, not the sulphur
            new Color(0.32f, 0.29f, 0.28f)  // Cliff         — bare rock, blackened
        };

        public static Color OfGround(TerrainType t, Biome biome)
        {
            switch (biome)
            {
                case Biome.Winter: return WinterGroundColors[(int)t];
                case Biome.Dead: return DeadGroundColors[(int)t];
                case Biome.Marsh: return MarshGroundColors[(int)t];
                case Biome.Plains: return PlainsGroundColors[(int)t];
                case Biome.Mountain: return MountainGroundColors[(int)t];
                case Biome.Farmland: return FarmlandGroundColors[(int)t];
                case Biome.Coast: return CoastGroundColors[(int)t];
                case Biome.Desert: return DesertGroundColors[(int)t];
                default: return GroundColors[(int)t];
            }
        }
    }
}
