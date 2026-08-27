using System.Collections.Generic;
using Arna.Sim;
using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// A set of interchangeable models, and how the pack they came from was exported.
    ///
    /// The up axis belongs here rather than on the biome, because it is a fact about
    /// the pack and a biome now draws from several. Held on the biome as a single flag
    /// it was right for every model or wrong for every model, and mixing a Y-up nature
    /// pack with a Z-up scenery pack made both answers wrong at once.
    ///
    /// It cannot be detected from the model either. A fern measures 9.05 x 2.69 x 8.49
    /// and a pebble 0.50 x 0.10 x 0.37 — both are widest across, and both are the right
    /// way up. Only the pack knows.
    /// </summary>
    [System.Serializable]
    public sealed class PropSet
    {
        public GameObject[] Models;

        /// <summary>True when the pack was exported with Z up, as Blender does by default.</summary>
        public bool ZUp;

        public bool Any => Models != null && Models.Length > 0;

        public PropSet() { }

        public PropSet(bool zUp, GameObject[] models)
        {
            ZUp = zUp;
            Models = models;
        }
    }

    /// <summary>Models used to dress one biome's terrain.</summary>
    [System.Serializable]
    public sealed class BiomeDecor
    {
        /// <summary>
        /// Conifers. The tree this country is mostly made of.
        ///
        /// Kept as its own set rather than folded in with the broadleaf because the
        /// forest is a spruce forest with other things in it, and a single bag drawn
        /// from evenly is a mixed wood — a different place entirely.
        /// </summary>
        public PropSet Pines = new PropSet();

        /// <summary>Round-crowned broadleaf. The minority that punctuates the conifers.</summary>
        public PropSet Trees = new PropSet();

        /// <summary>
        /// Birch: pale trunks, thin crowns, and the reason there is a third tree set.
        ///
        /// Two species read as two species; three read as a wood. It is the cheapest
        /// variety on this list — the models were already in the pack, unused — and the
        /// pale trunk is the only light vertical line in a forest otherwise made of dark
        /// ones, which is what stops a stand of spruce from reading as a texture.
        /// </summary>
        public PropSet Birch = new PropSet();

        public PropSet DeadTrees = new PropSet();

        /// <summary>
        /// The layer between the grass and the trees.
        ///
        /// Without it a forest is trunks standing in a lawn. Every reference for this
        /// game has a shrub layer at roughly head height — dense enough to hide a fox,
        /// short enough to see a caravan over — and its absence is most of why the old
        /// forest read as a diagram of a forest.
        /// </summary>
        public PropSet Bushes = new PropSet();

        /// <summary>
        /// Reeds, swamp growth and roots: what grows in standing water and at its edge.
        ///
        /// A fen dressed in the same grass and ferns as the meadow is a meadow that
        /// happens to slow you down. Used on marsh tiles and on the ring of tiles around
        /// them, because a bog does not stop at a tile boundary — the ground goes soft
        /// before it goes wet, and that margin is where the reeds are.
        /// </summary>
        public PropSet MarshPlants = new PropSet();

        /// <summary>
        /// Pads on open water, and the one set in the whole decorator that is measured
        /// across rather than up.
        ///
        /// They were pulled out of <see cref="MarshPlants"/> because that set is fitted
        /// by height, and a lilypad has almost none: fitting one to 0.7 m of height
        /// multiplied the model by some thirty times and took the width along with it,
        /// which is where the fen full of green rings came from. Kept as its own set
        /// rather than dropped, because still water with nothing on it reads as a hole
        /// in the map — and fitted across, where a pad's size is a real measurement.
        /// </summary>
        public PropSet Lilypads = new PropSet();

        /// <summary>Loose stone, ankle to waist. Scattered everywhere.</summary>
        public PropSet Rocks = new PropSet();

        /// <summary>
        /// The big grey blocks and clusters, taller than a man.
        ///
        /// Separate from <see cref="Rocks"/> because they do a different job: a pebble
        /// is texture and a boulder is a landmark you steer round. Sized across rather
        /// than up — these are slabs, and fitting a slab by height inflates it.
        /// </summary>
        public PropSet Boulders = new PropSet();

        /// <summary>
        /// The skyline: mountains standing outside the map, seen and never reached.
        ///
        /// The world used to end at the map edge with a flat sky colour behind it, and
        /// that reads as the edge of a board rather than as distance. Both reference
        /// pictures put large pale peaks well beyond the ground being played on, and
        /// what they buy is not scenery — it is the sense that the country continues,
        /// which is the whole premise of a game about a road through it.
        ///
        /// **This is now the only place mountains appear.** They used to stand on the map
        /// as well, in mountain-pass terrain, and a twenty-metre hill on a tile the
        /// caravan has to walk over is a wall in the road: the column drove straight into
        /// one. A pass is the ground *between* the mountains anyway — boulders, scree and
        /// the trees that manage on it — so that is what dresses it, and the range is
        /// here, where it is looked at rather than walked into.
        ///
        /// They are painted flat grey. See <see cref="Skyline"/>.
        /// </summary>
        public PropSet Horizon = new PropSet();

        /// <summary>
        /// Grass, ferns, flowers, mushrooms, pebbles — the small stuff, scattered by
        /// the thousand rather than the dozen.
        ///
        /// It is what separates a landscape from a diagram of one. Bare ground between
        /// the trees reads as unfinished however good the trees are, because real
        /// ground is never bare.
        /// </summary>
        public PropSet GroundCover = new PropSet();

        /// <summary>
        /// Flat patches laid on the ground: bare earth, gravel, worn grass.
        ///
        /// The ground is one shader with a vertex colour per terrain type and a grain
        /// texture over it, which gives an even sheet of green. Every reference for this
        /// game shows the opposite — grass worn through to soil, gravel along the water,
        /// a road that is a band of trodden earth rather than a line of a different
        /// colour. That variation is what makes ground read as ground, and it cannot come
        /// from the shader without a second texture set and a blend map.
        ///
        /// It can come from the pack. These are laid on top, flat, and they cost a few
        /// hundred triangles each — a cheaper answer than a terrain-splat pipeline and a
        /// reversible one.
        /// </summary>
        public PropSet GroundPatches = new PropSet();

        /// <summary>
        /// Landmarks. Unlike the scatter above, these are placed where they make sense
        /// rather than where the dice fall: people build beside roads, watchtowers go
        /// where there is something to watch, timber is cut where the trees are.
        ///
        /// They are also deliberately rare. The design asks the player to learn to read
        /// the world (docs/GDD.md §3.4), and anything scattered everywhere teaches the
        /// eye to skip it — which would blunt the signals that are meant to matter.
        /// </summary>
        public PropSet Houses = new PropSet();
        public PropSet Farms = new PropSet();
        public PropSet Watchtowers = new PropSet();
        public PropSet Timber = new PropSet();

        /// <summary>
        /// Reserved for the trap-field signal in docs/GDD.md §2: a ruin marks ground
        /// where something went wrong before. Nothing places these yet — the caller
        /// supplies the sites, and until traps are wired in there are none. Kept here
        /// so the models are loaded and sized alongside everything else rather than
        /// bolted on later.
        /// </summary>
        public PropSet Ruins = new PropSet();

        /// <summary>
        /// What somebody planted here: a banner, a row of archer stakes.
        ///
        /// The GDD's §5 table names *bone piles and totems* as the trap-field tell and
        /// there has never been a totem in the project — the nearest thing either nature
        /// pack had was a torch on a stick. The army pack's banners and stakes are what
        /// the entry was describing: a thing driven into the ground, which says somebody
        /// chose this piece of it.
        ///
        /// **Measured up, not across**, which is why it is not in <see cref="Ruins"/>.
        /// That set is fitted to five metres of width because a wrecked cart is a wide
        /// low thing; a banner is a tall narrow one, and five metres across it would be a
        /// sail. Same trap as the boulders and the lilypads, one set earlier.
        /// </summary>
        public PropSet Markers = new PropSet();

        /// <summary>
        /// The surface of open water, laid over the tiles that are water.
        ///
        /// **The one set here that replaces a tile with a shape.** Everything else on
        /// this list dresses ground that the mesh already draws; a river is drawn *by*
        /// the mesh, as a band of blue vertex colour, and a band of tiles running at any
        /// angle other than square is a staircase of four-metre squares. Reeds and pads
        /// hide that staircase, which was the fix that could be had without new models.
        /// A water plane is the fix: a surface with its own edge, laid on top, which does
        /// not care where the tile boundaries are.
        /// </summary>
        public PropSet Water = new PropSet();

        /// <summary>Where a route crosses water: a plank bridge, a stepping course.</summary>
        public PropSet Fords = new PropSet();

        /// <summary>
        /// Rock faces for the tiles the map calls cliff.
        ///
        /// `TerrainType.Cliff` has existed since the generator was written and has never
        /// had a single prop on it — it is impassable, so nothing walks there and nothing
        /// was ever put there, and what the player sees is a patch of differently
        /// coloured ground they cannot cross for no visible reason. A cliff should look
        /// like the reason.
        /// </summary>
        public PropSet Cliffs = new PropSet();

        /// <summary>
        /// A tent, a weapon rack, a banner: what an enemy group lives in.
        ///
        /// Groups have a territory in the simulation and stand on bare grass in the view,
        /// so a band of raiders reads as men who happen to be standing there. A camp is
        /// the same soft signal as the wreck at a trap field and the crows overhead — it
        /// says *somebody lives here* in the language the design already speaks
        /// (docs/GDD.md §2), rather than by drawing a marker.
        /// </summary>
        public PropSet Camps = new PropSet();

        /// <summary>Willows, for the ground beside water. Nothing else here belongs there.</summary>
        public PropSet Willows = new PropSet();

        /// <summary>
        /// Stone the water has piled up, for its margins.
        ///
        /// The shoreline was strewn with the general <see cref="Rocks"/> set, which is
        /// loose stone scattered anywhere. The pack has piles and *curved* piles, made to
        /// follow a waterline — the difference between stones that happen to be near a
        /// river and stones a river put there.
        /// </summary>
        public PropSet Shore = new PropSet();

        /// <summary>
        /// One piece of scenery standing behind everything: the far range.
        ///
        /// The skyline is 22 separate peaks — 22 draw calls and a few thousand triangles
        /// for something that is never nearer than 400 m, never seen from the side, and
        /// never moves relative to anything. The pack ships `SM_MountainSkybox_01` for
        /// exactly this: one mesh, one draw call, the whole horizon.
        ///
        /// It does not replace the ring. It stands **behind** it, so the near peaks give
        /// parallax against a backdrop that does not — which is what makes distance read
        /// as distance rather than as a painted wall. The ring can then be thinned, which
        /// is where the draw calls come back.
        /// </summary>
        public PropSet Backdrop = new PropSet();

        public bool IsEmpty =>
            !Has(Trees) && !Has(Pines) && !Has(Birch) && !Has(DeadTrees) && !Has(Bushes) &&
            !Has(Rocks) && !Has(Boulders) && !Has(Horizon) &&
            !Has(GroundCover) && !Has(MarshPlants) && !Has(Lilypads) &&
            !Has(GroundPatches) && !Has(Houses) && !Has(Farms) && !Has(Watchtowers) &&
            !Has(Timber) && !Has(Ruins) && !Has(Markers) && !Has(Water) && !Has(Fords) &&
            !Has(Cliffs) && !Has(Camps) && !Has(Willows) && !Has(Shore) && !Has(Backdrop);

        static bool Has(PropSet set) => set != null && set.Any;
    }

    /// <summary>
    /// Scatters props across the terrain so the world reads as a landscape.
    ///
    /// Placement is driven by the level seed, so a level is dressed identically every
    /// time — a map that rearranged its own forest between attempts would undermine
    /// the one promise the whole generator rests on.
    ///
    /// Every prop is rescaled to a stated height in metres on the way in. Left at
    /// their authored size the pines came out roughly a metre tall on a 256-metre
    /// map: placed correctly, and completely invisible.
    /// </summary>
    public static class TerrainDecorator
    {
        /// <summary>Prop heights in metres. A tile is four metres across for reference.</summary>
        public const float TreeHeight = 7f;
        public const float PineHeight = 8.5f;
        public const float RockHeight = 2.2f;

        /// <summary>
        /// A boulder, measured across rather than up. These are slabs and blocks, wider
        /// than they are tall, and fitting one by height inflates it into a menhir.
        /// </summary>
        public const float BoulderWidth = 5.5f;

        /// <summary>
        /// A shrub, at about the height of the man walking past it. Tall enough to read
        /// as cover from the play camera, short enough that the column shows over it.
        /// </summary>
        public const float BushHeight = 1.9f;

        /// <summary>
        /// The radius the ring would like to stand at, in metres from the map's centre.
        ///
        /// A preference rather than a rule: <c>PlaceHorizon</c> pushes any peak further
        /// out when its own footprint would otherwise reach back over the drawn ground.
        ///
        /// The map is 256 m across, so its corners are 181 m out. At 320 the ring clears
        /// them by well over a hundred metres, which is enough that the peaks read as
        /// distance rather than as a wall around the pitch. The play camera clips at
        /// 900 m and can pull back to 120 from the caravan, so the furthest peak from
        /// the furthest camera is about 520 — comfortably inside.
        ///
        /// <b>None of it is visible at the default camera, and that is geometry rather
        /// than tuning.</b> The play view sits 46 m back and 32 m up: a pitch of 34.8°
        /// with a 50° field, so the frame spans from 9.8° *below* horizontal to 59.8°
        /// below. A horizon is at 0°. Nothing on it can enter that frame at any size or
        /// distance. The skyline is for the player who tilts the camera down toward it —
        /// `CameraOrbit` allows 12°, where the frame reaches 13° above horizontal — and
        /// it is one of the few things the orbit control actually pays out.
        /// </summary>
        public const float HorizonRadius = 380f;

        /// <summary>
        /// Clear air between the furthest drawn ground and the foot of the range.
        ///
        /// Forty metres. It is not a look, it is the difference between country that
        /// continues and a wall at the end of the field.
        /// </summary>
        public const float HorizonClearance = 40f;

        /// <summary>
        /// Peaks in the ring.
        ///
        /// Twenty-two of them at 380 m is one every 108 metres, and each is about 1.2
        /// times its height across — so at <see cref="HorizonHeight"/> they still overlap
        /// and read as a continuous range rather than as a row of separate cones, which
        /// is what a skyline is. The clearance rule pushes some further out than others,
        /// which breaks the ring's evenness on purpose: a range is not a fence.
        /// </summary>
        public const int HorizonCount = 22;

        /// <summary>
        /// How tall a skyline peak is.
        ///
        /// A hundred and five, and getting here took one honest measurement and one
        /// mistake worth recording.
        ///
        /// The height was raised from 130 to 185 because at 130 exactly one peak found a
        /// gap in the canopy — but that was never a height problem. The range was
        /// invisible because the fog ended at 320 m and the ring stood at 320, so every
        /// pixel of it was the colour of the air. Raising the peaks changed nothing and
        /// the raise was left in, which meant a value chosen for one camera and never
        /// checked from another.
        ///
        /// From a camera near the map's corner the nearest peak is only 192 m away, and
        /// at 185 m tall it tops out 37° above the eye: not a horizon, a wall. At 105 it
        /// is 19° from the corner and 11.5° from the middle of the map, which reads as
        /// distance from anywhere the caravan can stand — and still stands well clear of
        /// a treeline that tops out around 3°.
        ///
        /// The jitter stays wide on purpose: a row of identical peaks is a saw blade.
        /// </summary>
        public const float HorizonHeight = 105f;
        public const float HorizonJitterLow = 0.55f;
        public const float HorizonJitterHigh = 1.35f;
        public const float DeadTreeHeight = 9f;

        /// <summary>Landmark sizes. Buildings are measured by height, ground works by width.</summary>
        public const float HouseHeight = 6f;
        public const float WatchtowerHeight = 11f;
        public const float FarmWidth = 9f;
        public const float TimberWidth = 3f;
        public const float RuinWidth = 5f;

        /// <summary>
        /// How tall a planted marker stands.
        ///
        /// Three metres — head and a half above the man walking past it, which is what a
        /// banner is for. Tall enough to be seen over the scrub around a trap field from
        /// a camera 47 m up, short enough not to compete with a fourteen-metre spruce.
        /// </summary>
        public const float MarkerHeight = 3f;

        /// <summary>
        /// How far across a water plane is laid, in metres.
        ///
        /// A tile and a half. The planes overlap on purpose — a surface that stops
        /// exactly at a tile boundary reproduces the staircase it was brought in to
        /// hide, and a river is continuous. Overlapping ones read as one sheet.
        /// </summary>
        public const float WaterWidth = TileGrid.TileSize * 1.5f;

        /// <summary>
        /// How far a water plane sits above the ground under it.
        ///
        /// Twelve centimetres. Level with the bed it z-fights, which is the ugliest
        /// failure in rendering and the most distracting; higher than this and the sheet
        /// visibly floats over its own bank.
        /// </summary>
        public const float WaterLift = 0.12f;

        /// <summary>How wide a crossing is laid across a ford, in metres.</summary>
        public const float FordWidth = 6f;

        /// <summary>How tall a cliff face stands.</summary>
        // Five metres, down from twelve. A cliff tile is impassable ground on a flat map
        // rather than the lip of a drop, so whatever stands on it stands in the open and
        // is read against what is beside it: twelve metres is five draught horses stacked
        // up, which stops being scenery and becomes a landmark in the middle of a field.
        // Five is a rock a man could not climb, which is all the tile is claiming.
        public const float CliffHeight = 5f;

        /// <summary>A tent, at a bit over the height of the man who sleeps in it.</summary>
        public const float CampHeight = 2.6f;

        /// <summary>
        /// A willow, which is shorter than the spruce it stands among.
        ///
        /// Ten metres against a spruce's fourteen, because a willow leans out over water
        /// rather than up out of a wood, and one drawn to a conifer's height beside a
        /// stream is the only tree on the map you would notice from the map.
        /// </summary>
        public const float WillowHeight = 10f;

        /// <summary>How many tiles from water a willow will take root.</summary>
        public const int WillowReach = 2;

        /// <summary>
        /// How many landmarks a map may carry. A hard cap rather than density alone,
        /// because density on a road that happens to run the length of the map produces
        /// a ribbon development, and the point of a landmark is that there are few.
        /// </summary>
        public const int MaxLandmarks = 18;

        /// <summary>
        /// Height of a grass tuft or a fern, in metres.
        ///
        /// <b>Ground cover is fitted by height, so nothing flat may go in it.</b> A
        /// lilypad has almost no height, and fitting one to 0.7 m of it multiplies the
        /// whole model by whatever that takes — the width goes with it, and a fen came
        /// out paved with three-metre discs stacked on each other. Anything flat is
        /// measured across instead: <see cref="BiomeDecor.GroundPatches"/> for what lies
        /// on the ground, <see cref="BiomeDecor.Lilypads"/> for what floats on it.
        /// </summary>
        public const float CoverHeight = 0.7f;

        /// <summary>
        /// How wide a lilypad cluster lies across the water, in metres.
        ///
        /// Measured across on purpose — see <see cref="BiomeDecor.Lilypads"/> for what
        /// measuring one up its height did. A single pad is 20-30 cm and the pack ships
        /// clusters as well as singles, so the number is for the set rather than for a
        /// leaf: 1.2 m with the usual quarter either way gives 0.9 m to 1.5 m, which
        /// puts three or four of them inside one four-metre tile without either
        /// disappearing at camera distance or reading as a raft.
        /// </summary>
        public const float LilypadWidth = 1.2f;

        /// <summary>
        /// The share of a fen tile's cover that comes out a lilypad rather than a reed.
        ///
        /// Only on the water itself, never on the soft margin where the reeds are — a
        /// pad floats, and one lying in the grass beside a bog is the same category of
        /// wrong as a reed growing out of open water.
        /// </summary>
        public const float LilypadShare = 0.22f;

        /// <summary>
        /// How much a scattered prop may vary from its table size.
        ///
        /// A quarter either way for rocks, grass and buildings: enough that the eye does
        /// not catch two identical stones, little enough that a boulder stays a boulder.
        /// </summary>
        public const float JitterLow = 0.8f;
        public const float JitterHigh = 1.25f;

        /// <summary>
        /// Trees vary far more, and the reference picture is why.
        ///
        /// A quarter either way gave a stand of spruces between 6.8 and 10.6 m — a
        /// hedge, evenly clipped. What a forest looks like from the air is saplings
        /// through to giants, and in the pack's own marketing shot the smallest conifer
        /// is about a third the height of the largest. At 0.55 to 1.7 against a pine's
        /// eight and a half metres that is 4.7 m to 14.5 m, which is the same spread.
        ///
        /// Applied to the whole tree family, dead ones included: a fen kills trees of
        /// every size.
        /// </summary>
        public const float TreeJitterLow = 0.55f;
        public const float TreeJitterHigh = 1.7f;

        /// <summary>
        /// Dead trees, which are the exception — and a render said so before anyone did.
        ///
        /// They start at nine metres, the tallest entry in the table, because a bare
        /// trunk has to read from map height. Drawn as a pole a tenth as wide, at 1.7
        /// that is a fifteen-metre spike, and a ridge of them reads as a power line
        /// rather than as a fen. Small snags yes, giants no.
        /// </summary>
        public const float DeadJitterLow = 0.5f;
        public const float DeadJitterHigh = 1.15f;

        /// <summary>
        /// Ground cover is capped separately and much higher. These are a few hundred
        /// triangles each against a tree's few thousand, so the budget that keeps trees
        /// affordable is the wrong budget for grass.
        /// </summary>
        public const int MaxGroundCover = 4000;

        /// <summary>
        /// Width of a ground patch in metres.
        ///
        /// 5.2 rather than 7.5, and the arithmetic says why the first number could not
        /// work. A 7.5 m disc covers about three and a half four-metre tiles, so at the
        /// plains rate of 0.22 a tile it laid patches over 77 % of the ground and on a
        /// road tile, at 0.55 and the plan's density scale, 265 % — every patch on top of
        /// two others. Seen from above that is not worn ground, it is craters on craters.
        ///
        /// At 5.2 a patch covers about one and two thirds tiles, which with the
        /// no-stacking rule below leaves bare earth in pieces rather than in sheets.
        /// </summary>
        public const float PatchWidth = 5.2f;

        /// <summary>
        /// How much fall a tile may have before it is refused a patch, in metres.
        ///
        /// These are flat pieces. Laid across a slope, a flat piece cuts into the hill on
        /// one side and floats off it on the other, and both are worse than the even
        /// green they were meant to break up. A tile is four metres across, so 0.9 m of
        /// fall is a slope of about twelve degrees — enough to catch the valley floors,
        /// the river flats and the passes, and to leave the hillsides alone.
        /// </summary>
        public const float PatchMaxFall = 0.9f;

        /// <summary>
        /// Metres a patch is lifted off the ground it sits on.
        ///
        /// Coplanar surfaces fight for the depth buffer and the result flickers as the
        /// camera moves — the one artefact on this list that a still screenshot will not
        /// show and every player will see.
        /// </summary>
        public const float PatchLift = 0.05f;

        public const int MaxGroundPatches = 420;

        /// <summary>
        /// Patches per tile.
        ///
        /// Heaviest on the road, which in the reference is a band of bare trodden earth
        /// and in this game has so far been a stripe of a slightly different green.
        /// Plains next: open ground is where a bare patch reads. Little in forest, where
        /// the floor is litter and shade and there is not much of it to see, and none in
        /// the mountain pass, which is already bare rock.
        /// </summary>
        static readonly Dictionary<TerrainType, float> PatchDensity = new Dictionary<TerrainType, float>
        {
            { TerrainType.Road, 0.55f },
            { TerrainType.Plains, 0.22f },
            { TerrainType.Marsh, 0.14f },
            { TerrainType.Forest, 0.07f }
        };

        /// <summary>
        /// How tall a prop has to be before the caravan's line refuses it, in metres.
        ///
        /// Two, which sorts the table cleanly: a rock is 2.2 and a boulder 5.5 and a tree
        /// 7 to 8.5, and all of them are things a loaded wagon goes round. A bush is 1.9
        /// and grass is 0.7, and both are things it goes over. Nothing here is a guess
        /// about wheels — it is the same list of sizes the scatter already uses, read for
        /// what it means.
        /// </summary>
        public const float DriveClearance = 2f;

        /// <summary>Stones along the water's edge, measured across rather than up.</summary>
        public const float ShoreStoneSize = 2.2f;
        public const int MaxShoreStones = 1600;

        /// <summary>
        /// Tufts per tile — a rate, not a probability, because more than one belongs on
        /// a four-metre square. Forest floor and marsh are thick with it; a road is
        /// worn bare and mountain rock has nothing to grow in.
        /// </summary>
        static readonly Dictionary<TerrainType, float> CoverDensity = new Dictionary<TerrainType, float>
        {
            { TerrainType.Forest, 2.4f },
            { TerrainType.Plains, 1.7f },
            { TerrainType.Marsh, 2.0f },
            { TerrainType.MountainPass, 0.5f },
            { TerrainType.Road, 0.15f },

            // Open water, and it is the one entry here that is not about dressing the
            // ground. A river is drawn as tiles, so a diagonal one is a staircase of
            // four-metre squares; everywhere else on the map that edge is hidden under
            // the props growing across it, and the water was the one boundary with
            // nothing on it. Pads floating over the seam do for it what the trees do for
            // the forest's edge.
            { TerrainType.Water, 0.45f }
        };

        /// <summary>
        /// Props per tile.
        ///
        /// Forest was tuned down to 0.28 from a first attempt at 0.55, which put a
        /// thousand nine-metre trees on a 256-metre map and closed the canopy over the
        /// caravan entirely — the world has to be looked through, not just at. At 0.45
        /// it is dense and still transparent.
        ///
        /// Measured on 1-5, where the forest is 1812 tiles: 0.28 gives 489 trees at a
        /// median 4.5 m to the nearest neighbour, 0.45 gives 796 at 4.1 m, 0.62 gives
        /// 1088 at 3.6 m. A spruce crown is 0.62 of its height across, so a base-size
        /// pine's crown is 5.3 m: at 4.1 m the crowns already overlap, which is the thing
        /// the reference picture shows and 4.5 m did not.
        ///
        /// <b>0.62 was tried and rejected on the evidence.</b> The argument for it was
        /// that the old objection had expired — trees now run 4.7 m to 14.5 m rather than
        /// all standing at nine, and canopy no longer reserves ground. The render says
        /// otherwise: at 0.62 two wagons and one troop were visible through a gap and the
        /// rest of the column was gone, which is the 0.55 failure exactly. Overlapping
        /// crowns were the goal and 0.45 reaches them; 0.62 buys nothing the picture
        /// wanted and costs the column.
        ///
        /// It is still the triangle budget's largest single line — 796 trees against 489
        /// — and the limit in docs/technical-design.md is 250k. They share one atlas
        /// material so the draw calls batch; the triangles do not.
        /// </summary>
        static readonly Dictionary<TerrainType, float> Density = new Dictionary<TerrainType, float>
        {
            { TerrainType.Forest, 0.45f },
            { TerrainType.MountainPass, 0.18f },
            { TerrainType.Plains, 0.03f },
            { TerrainType.Marsh, 0.06f },
            { TerrainType.Road, 0.01f }
        };

        /// <param name="keepClear">
        /// Tiles left bare. The planning map passes the route here so the drawn line
        /// stays readable through the trees; the play view passes nothing, because
        /// there is no line to bury and a forest should look like one.
        /// </param>
        /// <param name="ruinSites">
        /// Tiles that should carry a ruin. This is the hook for the trap-field signal
        /// in docs/GDD.md §2 — ground where a previous caravan came to grief. Left null
        /// until traps are wired to it, and note when they are that the ruin belongs
        /// near the field rather than on it: a signal is meant to suggest danger, not
        /// mark its exact extent.
        /// </param>
        public static int Decorate(Transform parent, TileGrid grid, int seed, BiomeDecor decor,
                                   IReadOnlyCollection<int> keepClear = null,
                                   float heightScale = 0f, int maxProps = 600,
                                   float densityScale = 1f,
                                   IReadOnlyCollection<int> ruinSites = null,
                                   bool horizon = true,
                                   IReadOnlyCollection<int> driveLine = null,
                                   IReadOnlyCollection<int> campSites = null)
        {
            if (decor == null || decor.IsEmpty) return 0;

            var rng = new DeterministicRandom(seed ^ 0x5EED10);
            var clear = keepClear == null ? null : new HashSet<int>(keepClear);

            // A set, not the list it arrives as. IReadOnlyCollection has no Contains
            // worth the name, and this is asked once per prop on every tile of the map.
            var road = driveLine == null ? null : new HashSet<int>(driveLine);
            int placed = 0;

            // Landmarks first, and the tiles they take are then off limits to the
            // scatter. Done the other way round a pine grows through the roof of the
            // farmhouse, and the building — the thing the eye was meant to find — is
            // the one that loses.
            var occupied = new HashSet<int>();
            placed += PlaceLandmarks(parent, grid, rng, decor, clear, occupied, heightScale, ruinSites);

            // Two passes over the same ground, and the order is half the fix. The scatter
            // walks tiles in index order, so a mountain reaching tile 500 cannot un-place
            // the pine put down on tile 450 twenty tiles earlier — the big thing has to
            // claim its ground first or the small things grow out of it.
            for (int pass = 0; pass < 2; pass++)
            {
                bool bulky = pass == 0;
                var passRng = new DeterministicRandom(seed ^ (0x9E37 * (pass + 1)));

                for (int i = 0; i < grid.TileCount && placed < maxProps; i++)
                {
                    var terrain = grid[i];
                    if (!Density.TryGetValue(terrain, out float density)) continue;
                    if (clear != null && clear.Contains(i)) continue;
                    if (occupied.Contains(i)) continue;
                    if (!passRng.Chance(density * densityScale)) continue;

                    var choice = Pick(decor, terrain, passRng);
                    if (choice.Prefab == null) continue;
                    if (IsBulky(terrain, choice) != bulky) continue;

                    // Nothing the wagons would drive through, on the ground the wagons
                    // drive over.
                    //
                    // Not the same as keeping the line clear, and the difference is the
                    // whole reason this is a second parameter. `keepClear` empties a tile
                    // and thins its grass, which draws the route as a swept lane through
                    // the forest — the thing the corridor version was turned off for.
                    // This refuses only what a wheel cannot roll over: the grass, the
                    // flowers, the bushes and the loose stones all stay, so the ground
                    // still reads as untouched country and the caravan stops passing
                    // through boulders.
                    if (road != null && choice.Size >= DriveClearance && road.Contains(i))
                        continue;

                    if (Scatter(parent, grid, passRng, choice, i, heightScale, spread: 1.4f, occupied))
                        placed++;
                }
            }

            // Off for the plan, and the reason is what a plan is.
            //
            // The skyline stands three hundred metres outside a map two hundred and
            // fifty-six across, and the plan camera looks straight down from far enough
            // up to hold the whole map — so its frustum is wider than the ground, and
            // the ring lands *around* the map in the frame. A row of mountains framing a
            // map is not distance, it is furniture: the plan is a map, and the only
            // thing on it should be the country the route is drawn through.
            if (horizon)
            {
                placed += PlaceBackdrop(parent, grid, decor);
                placed += PlaceHorizon(parent, grid, rng, decor);
            }

            // Patches before cover, so grass grows over the bare earth rather than the
            // bare earth being laid over the grass.
            placed += PlaceGroundPatches(parent, grid, rng, decor, occupied,
                                         heightScale, densityScale);

            placed += PlaceGroundCover(parent, grid, rng, decor, clear, occupied,
                                       heightScale, densityScale);
            placed += PlaceShoreline(parent, grid, rng, decor, occupied, heightScale, densityScale);

            // The water goes on last, over everything laid on its bed. Nothing claims
            // ground for it: reeds stand in the shallows and pads float on the surface,
            // and a sheet that reserved its tiles would have cleared both away.
            placed += PlaceWater(parent, grid, rng, decor, heightScale);
            placed += PlaceFords(parent, grid, rng, decor, occupied, heightScale);
            placed += PlaceCliffs(parent, grid, rng, decor, occupied, heightScale);
            placed += PlaceWillows(parent, grid, rng, decor, occupied, heightScale, densityScale);
            placed += PlaceCamps(parent, grid, rng, decor, occupied, heightScale, campSites);

            return placed;
        }

        /// <summary>
        /// Lays a surface over the tiles that are water.
        ///
        /// See <see cref="BiomeDecor.Water"/> for why this is the only set that replaces
        /// a tile rather than dressing one. The planes are laid a tile and a half across
        /// and overlap, because a sheet that stopped at the tile boundary would reproduce
        /// the staircase it is here to hide.
        ///
        /// Flat, and level with itself rather than with the bed. Water finds its own
        /// level: a plane tilted to follow the ground under it is the one thing that
        /// would give the trick away.
        /// </summary>
        static int PlaceWater(Transform parent, TileGrid grid, DeterministicRandom rng,
                              BiomeDecor decor, float heightScale)
        {
            if (!decor.Water.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount; i++)
            {
                if (grid[i] != TerrainType.Water) continue;

                var at = Vec2.FromTile(grid, i);
                float bed = grid.SurfaceElevation(at.X, at.Y) * heightScale;

                var instance = Object.Instantiate(Any(decor.Water, rng), parent);

                instance.transform.position = new Vector3(at.X, bed + WaterLift, at.Y);

                // Turned in quarter turns only. A water plane is a square with a texture
                // on it, and an odd angle shows its corners against the tile grid it is
                // covering.
                instance.transform.rotation = Quaternion.Euler(0f, rng.Range(0, 4) * 90f, 0f);

                ModelScaling.FitToFootprint(instance, WaterWidth, bed + WaterLift);
                placed++;
            }

            return placed;
        }

        /// <summary>
        /// Puts a crossing on the ford tiles, one per crossing rather than one per tile.
        ///
        /// A ford is a terrain type the route planner treats as a chokepoint — every
        /// corridor tends to use the same one, which is why the traps go there — and it
        /// has never had anything on it. What the player sees is water that is somehow
        /// passable, with nothing to say why. A plank bridge says it.
        /// </summary>
        static int PlaceFords(Transform parent, TileGrid grid, DeterministicRandom rng,
                              BiomeDecor decor, HashSet<int> occupied, float heightScale)
        {
            if (!decor.Fords.Any) return 0;

            int placed = 0;
            var bridges = new List<int>();

            for (int i = 0; i < grid.TileCount; i++)
            {
                if (grid[i] != TerrainType.Ford) continue;
                if (occupied.Contains(i)) continue;

                // One per crossing. A ford is several tiles wide and a bridge on each of
                // them is a pier, not a crossing.
                if (!Apart(grid, i, bridges, 4f)) continue;
                bridges.Add(i);

                var choice = new Choice(decor.Fords, Any(decor.Fords, rng), FordWidth,
                                        byWidth: true);

                if (Scatter(parent, grid, rng, choice, i, heightScale, spread: 0.4f, occupied))
                    placed++;
            }

            return placed;
        }

        /// <summary>
        /// Stands rock on the tiles the map calls cliff.
        ///
        /// They are impassable, so nothing has ever been placed there and nothing walks
        /// there — and the result is a patch of differently coloured ground the player
        /// cannot cross for no visible reason. A cliff should look like the reason it is
        /// one.
        /// </summary>
        static int PlaceCliffs(Transform parent, TileGrid grid, DeterministicRandom rng,
                               BiomeDecor decor, HashSet<int> occupied, float heightScale)
        {
            if (!decor.Cliffs.Any) return 0;

            int placed = 0;
            var stood = new List<int>();

            for (int i = 0; i < grid.TileCount && placed < MaxLandmarks * 3; i++)
            {
                if (grid[i] != TerrainType.Cliff) continue;
                if (occupied.Contains(i)) continue;
                if (!Apart(grid, i, stood, 2f)) continue;
                stood.Add(i);

                var choice = new Choice(decor.Cliffs, Any(decor.Cliffs, rng), CliffHeight,
                                        byWidth: false);

                if (Scatter(parent, grid, rng, choice, i, heightScale, spread: 1.2f, occupied))
                    placed++;
            }

            return placed;
        }

        /// <summary>
        /// Willows on the ground beside water, and nowhere else.
        ///
        /// The scatter puts spruce and oak wherever the terrain table says forest, which
        /// takes no notice of a river running through it. A willow leaning over water is
        /// the one tree whose place is decided by something other than the biome.
        /// </summary>
        static int PlaceWillows(Transform parent, TileGrid grid, DeterministicRandom rng,
                                BiomeDecor decor, HashSet<int> occupied,
                                float heightScale, float densityScale)
        {
            if (!decor.Willows.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MaxLandmarks * 2; i++)
            {
                if (grid[i] == TerrainType.Water || grid[i] == TerrainType.Cliff) continue;
                if (occupied.Contains(i)) continue;

                grid.ToCoords(i, out int x, out int y);
                if (!WithinReachOfWater(grid, x, y, WillowReach)) continue;
                if (!rng.Chance(0.22f * densityScale)) continue;

                var choice = new Choice(decor.Willows, Any(decor.Willows, rng), WillowHeight,
                                        byWidth: false, TreeJitterLow, TreeJitterHigh, canopy: true);

                if (Scatter(parent, grid, rng, choice, i, heightScale, spread: 1.2f, occupied))
                    placed++;
            }

            return placed;
        }

        static bool WithinReachOfWater(TileGrid grid, int x, int y, int reach)
        {
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                    if (IsWater(grid, x + dx, y + dy)) return true;

            return false;
        }

        /// <summary>
        /// Pitches a camp on the ground an enemy group holds.
        ///
        /// One prop per site rather than a cluster, for the same reason the trap fields
        /// get one ruin: a band of raiders is *one* thing that is there, and six tents
        /// would read as a village. See <see cref="BiomeDecor.Camps"/>.
        /// </summary>
        static int PlaceCamps(Transform parent, TileGrid grid, DeterministicRandom rng,
                              BiomeDecor decor, HashSet<int> occupied, float heightScale,
                              IReadOnlyCollection<int> sites)
        {
            if (sites == null || !decor.Camps.Any) return 0;

            int placed = 0;

            foreach (int tile in sites)
            {
                if (tile < 0 || tile >= grid.TileCount) continue;
                if (occupied.Contains(tile)) continue;

                var choice = new Choice(decor.Camps, Any(decor.Camps, rng), CampHeight,
                                        byWidth: false);

                if (Scatter(parent, grid, rng, choice, tile, heightScale, spread: 1.6f, occupied))
                    placed++;
            }

            return placed;
        }

        /// <summary>
        /// Strews stones along the water's edge.
        ///
        /// A river drawn as a band of blue between two banks of grass is a shape on a
        /// map. What makes it read as a river is the debris it leaves at its margins —
        /// the water has been moving stones about for a long time, and the ground says
        /// so. It costs a few hundred pebbles and no new models.
        /// </summary>
        static int PlaceShoreline(Transform parent, TileGrid grid, DeterministicRandom rng,
                                  BiomeDecor decor, HashSet<int> occupied,
                                  float heightScale, float densityScale)
        {
            // The piles if the pack has them, the general stones if not.
            var stones = decor.Shore.Any ? decor.Shore : decor.Rocks;
            if (!stones.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MaxShoreStones; i++)
            {
                if (grid[i] == TerrainType.Water) continue;
                if (occupied.Contains(i)) continue;

                grid.ToCoords(i, out int x, out int y);
                if (!NextToWater(grid, x, y)) continue;

                int pile = 3 + rng.Range(0, 4);
                for (int s = 0; s < pile && placed < MaxShoreStones; s++)
                {
                    var choice = new Choice(stones, Any(stones, rng), ShoreStoneSize,
                                            byWidth: true);

                    Scatter(parent, grid, rng, choice, i, heightScale, spread: 2.0f);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// Whether a tile stands far enough from the ones this pass has already used.
        ///
        /// Measured against the pass's own choices rather than against everything on the
        /// map. A bridge should not be built beside another bridge; it has no quarrel
        /// with a daisy. Spacing against the whole occupancy set would also cost a scan
        /// of some thousands of props per candidate tile, for an answer nobody wanted.
        /// </summary>
        static bool Apart(TileGrid grid, int tile, List<int> taken, float spacing)
        {
            grid.ToCoords(tile, out int x, out int y);
            float limit = spacing * spacing;

            foreach (int other in taken)
            {
                grid.ToCoords(other, out int ox, out int oy);
                float dx = ox - x, dy = oy - y;
                if (dx * dx + dy * dy < limit) return false;
            }

            return true;
        }

        /// <summary>
        /// Whether any tile within one step is water, diagonals included.
        ///
        /// Diagonals matter more here than anywhere else on the map. A watercourse that
        /// runs at any angle other than square is drawn as a staircase of four-metre
        /// tiles, and it is the *corners* of that staircase that read as blocky. A
        /// four-neighbour margin dresses the flats and leaves every corner bare, which
        /// is precisely the wrong half.
        /// </summary>
        static bool NextToWater(TileGrid grid, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && IsWater(grid, x + dx, y + dy)) return true;

            return false;
        }

        static bool IsWater(TileGrid grid, int x, int y) =>
            grid.InBounds(x, y) && grid[x, y] == TerrainType.Water;

        /// <summary>
        /// Whether any tile within one step is marsh, diagonals included.
        ///
        /// Diagonals included on purpose: a four-neighbour margin leaves the corners of
        /// a fen sharp, and the one thing a bog's edge is not is a right angle.
        /// </summary>
        static bool NextToMarsh(TileGrid grid, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (grid.InBounds(x + dx, y + dy) &&
                        grid[x + dx, y + dy] == TerrainType.Marsh) return true;
                }

            return false;
        }

        /// <summary>
        /// Rings the map with mountains that are looked at and never walked on.
        ///
        /// Placed in world space around the map's centre rather than on tiles, because
        /// they are not on the map — there is no ground out there and there is not meant
        /// to be. Their feet sit on the map's base plane, so what shows above the map
        /// edge is peak, which is the only part that has a job.
        ///
        /// Deterministic like everything else: the same seed puts the same range on the
        /// same side of the same level, so a player who learns a level learns its
        /// skyline too, and the shot the screenshots take is the shot they saw.
        /// </summary>
        /// <summary>
        /// The skyline's colour: a pale, cold grey.
        ///
        /// Distant ground is not a smaller copy of near ground. Air between you and it
        /// scatters the light, so it loses its colour and moves toward the sky's — which
        /// is why a range twenty miles off is blue-grey however green its trees are, and
        /// why the pack's grass-covered mountains read as a green wall at the edge of
        /// the field rather than as distance. Painting them out is not a stylisation; it
        /// is the one cue that says how far away they are.
        /// </summary>
        public static readonly Color SkylineGrey = new Color(0.56f, 0.60f, 0.66f);

        static Material _skyline;

        /// <summary>
        /// One flat material for the whole range, made rather than loaded.
        ///
        /// Flat on purpose: at three hundred metres a texture is smaller than a pixel,
        /// so it costs bandwidth to deliver noise. One shared material also means the
        /// twenty-two peaks batch instead of pulling the pack's atlas twenty-two times.
        /// </summary>
        static Material Skyline()
        {
            // Explicit null check rather than ??: a material destroyed by a domain reload
            // reports itself null through Unity's operator and is handed straight back
            // by the coalescing one, which then throws the moment it is assigned.
            if (_skyline != null) return _skyline;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            _skyline = new Material(shader) { name = "Skyline" };
            _skyline.SetColor(BaseColorId, SkylineGrey);
            _skyline.SetFloat("_Smoothness", 0f);

            return _skyline;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Stands one backdrop behind the whole map. See <see cref="BiomeDecor.Backdrop"/>.
        ///
        /// Centred on the map and scaled to swallow it whole, so it surrounds the player
        /// whichever way they look. It is painted the same grey as the peaks in front of
        /// it: two ranges at different distances in the same colour read as one range
        /// receding, where two colours read as two pieces of scenery.
        /// </summary>
        static int PlaceBackdrop(Transform parent, TileGrid grid, BiomeDecor decor)
        {
            if (!decor.Backdrop.Any || decor.Backdrop.Models[0] == null) return 0;

            float centreX = grid.Width * TileGrid.TileSize * 0.5f;
            float centreZ = grid.Height * TileGrid.TileSize * 0.5f;

            var instance = Object.Instantiate(decor.Backdrop.Models[0], parent);

            instance.transform.position = new Vector3(centreX, 0f, centreZ);
            instance.transform.rotation = decor.Backdrop.ZUp
                ? Quaternion.Euler(-90f, 0f, 0f)
                : Quaternion.identity;

            ModelScaling.FitToFootprint(instance, BackdropWidth, 0f);

            var skyline = Skyline();
            if (skyline != null)
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = skyline;

            return 1;
        }

        /// <summary>
        /// How wide the backdrop is laid, in metres.
        ///
        /// Sixteen hundred: four times the radius the peak ring stands at, so it is well
        /// behind every one of them from every point on the map. It is a backdrop, and a
        /// backdrop that anything can get level with is a wall.
        /// </summary>
        public const float BackdropWidth = 1600f;

        static int PlaceHorizon(Transform parent, TileGrid grid, DeterministicRandom rng,
                                BiomeDecor decor)
        {
            if (!decor.Horizon.Any) return 0;

            var skyline = Skyline();

            float centreX = grid.Width * TileGrid.TileSize * 0.5f;
            float centreZ = grid.Height * TileGrid.TileSize * 0.5f;

            // The furthest drawn ground: the map's own corner, carried out by the skirt
            // in both directions at once, which is what makes a corner the far point.
            float ground = Mathf.Sqrt(
                (centreX + TerrainMeshBuilder.SkirtWidth) * (centreX + TerrainMeshBuilder.SkirtWidth) +
                (centreZ + TerrainMeshBuilder.SkirtWidth) * (centreZ + TerrainMeshBuilder.SkirtWidth));

            for (int i = 0; i < HorizonCount; i++)
            {
                // Evenly spaced and then nudged, rather than placed at random angles.
                // Random angles clump, and a clump on a skyline is a gap somewhere else
                // — which reads as the range having been forgotten on one side.
                float angle = (i + rng.Range(-0.3f, 0.3f)) / HorizonCount * Mathf.PI * 2f;
                float wanted = HorizonRadius * rng.Range(0.88f, 1.18f);

                var instance = Object.Instantiate(Any(decor.Horizon, rng), parent);

                instance.transform.rotation = decor.Horizon.ZUp
                    ? Quaternion.Euler(-90f, rng.Range(0f, 360f), 0f)
                    : Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

                ModelScaling.Fit(instance, HorizonHeight * rng.Range(HorizonJitterLow,
                                                                    HorizonJitterHigh), 0f);

                // Placed by its own edge rather than by its centre, and this is the
                // whole of why the caravan kept driving into a mountain.
                //
                // A peak is *fitted by height* and the pack's are much wider than they
                // are tall, so how far its foot sticks out from the point it stands on is
                // a fact about the model and not about the radius chosen here. At 320 m a
                // wide one reached back to within 197 m of the centre — and once the
                // ground grew a skirt, out to 249 m at the corners, the range was standing
                // on the map's own apron with the road running under it.
                //
                // So the radius is a preference and the measurement is the floor: far
                // enough that this peak's own footprint clears the furthest drawn ground.
                // Whatever the pack ships, and whatever the skirt becomes, it holds.
                float radius = Mathf.Max(wanted, ground + FootprintRadius(instance)
                                                 + HorizonClearance);

                // Only x and z. Fit has already stood it on y = 0.
                instance.transform.position = new Vector3(
                    centreX + Mathf.Cos(angle) * radius,
                    instance.transform.position.y,
                    centreZ + Mathf.Sin(angle) * radius);

                if (skyline == null) continue;

                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = skyline;
            }

            return HorizonCount;
        }

        /// <summary>
        /// Lays bare earth, gravel and worn grass over the flatter ground.
        ///
        /// The one rule that matters is the slope test. These are flat pieces from a
        /// pack built for flat modular scenes, and this game's ground is a heightmap;
        /// laid across a hillside a flat piece buries one edge and floats the other. So
        /// a tile is offered a patch only if its four corners are within
        /// <see cref="PatchMaxFall"/> of each other, which keeps them on the valley
        /// floors, the river flats and the road — where the reference pictures put them
        /// anyway, because that is where ground gets walked on.
        /// </summary>
        static int PlaceGroundPatches(Transform parent, TileGrid grid, DeterministicRandom rng,
                                      BiomeDecor decor, HashSet<int> occupied,
                                      float heightScale, float densityScale)
        {
            if (!decor.GroundPatches.Any) return 0;

            int placed = 0;

            // Patches keep their own ground, separately from the props'.
            //
            // Without it they stack, and stacked flat pieces at slightly different
            // heights are the worst artefact on this list: from above they read as
            // craters on craters, and each one makes the next one look deliberate.
            // Sharing `occupied` would have been wrong in both directions — a patch is
            // not something a tree may not grow in, and a tree is not something bare
            // earth may not appear under.
            var patched = new HashSet<int>();

            for (int i = 0; i < grid.TileCount && placed < MaxGroundPatches; i++)
            {
                if (!PatchDensity.TryGetValue(grid[i], out float density)) continue;
                if (occupied != null && occupied.Contains(i)) continue;

                if (!rng.Chance(density * densityScale)) continue;
                if (Fall(grid, i, heightScale) > PatchMaxFall) continue;

                // The whole footprint, not the centre tile. A 5.2 m disc reaches into its
                // neighbours, and checking only the middle lets two patches overlap by
                // most of their area while both believe they are alone.
                if (!PatchGroundFree(grid, patched, i)) continue;

                var choice = new Choice(decor.GroundPatches, Any(decor.GroundPatches, rng),
                                        PatchWidth, byWidth: true, canopy: true);

                if (Scatter(parent, grid, rng, choice, i, heightScale, spread: 1.2f,
                            occupied: null, lift: PatchLift))
                {
                    ReservePatch(grid, patched, i);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>Tiles a patch claims, measured out from the one it stands on.</summary>
        const int PatchReach = 1;

        static bool PatchGroundFree(TileGrid grid, HashSet<int> patched, int tile)
        {
            grid.ToCoords(tile, out int x, out int y);

            for (int dy = -PatchReach; dy <= PatchReach; dy++)
                for (int dx = -PatchReach; dx <= PatchReach; dx++)
                    if (grid.InBounds(x + dx, y + dy) &&
                        patched.Contains(grid.ToIndex(x + dx, y + dy))) return false;

            return true;
        }

        static void ReservePatch(TileGrid grid, HashSet<int> patched, int tile)
        {
            grid.ToCoords(tile, out int x, out int y);

            for (int dy = -PatchReach; dy <= PatchReach; dy++)
                for (int dx = -PatchReach; dx <= PatchReach; dx++)
                    if (grid.InBounds(x + dx, y + dy))
                        patched.Add(grid.ToIndex(x + dx, y + dy));
        }

        /// <summary>How far the ground falls across one tile, corner to corner.</summary>
        static float Fall(TileGrid grid, int tile, float heightScale)
        {
            grid.ToCoords(tile, out int x, out int y);

            float lowest = float.MaxValue, highest = float.MinValue;

            for (int dy = 0; dy <= 1; dy++)
                for (int dx = 0; dx <= 1; dx++)
                {
                    float h = grid.CornerElevation(x + dx, y + dy) * heightScale;
                    if (h < lowest) lowest = h;
                    if (h > highest) highest = h;
                }

            return highest - lowest;
        }

        /// <summary>
        /// Scatters the small stuff — grass, ferns, flowers, pebbles.
        ///
        /// Its own pass with its own budget, because it is numerous in a way nothing
        /// else is: several per tile rather than one per twenty. Sharing the scatter's
        /// prop cap would have let a few thousand grass tufts crowd out every tree on
        /// the map, and the cap exists to protect the frame rate, not to ration grass.
        ///
        /// It also ignores the cleared corridors. Grass does not hide a route the way a
        /// nine-metre pine does, and a route swept bare of even grass looks like a road.
        /// </summary>
        static int PlaceGroundCover(Transform parent, TileGrid grid, DeterministicRandom rng,
                                    BiomeDecor decor, HashSet<int> clear, HashSet<int> occupied,
                                    float heightScale, float densityScale)
        {
            if (!decor.GroundCover.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MaxGroundCover; i++)
            {
                if (!CoverDensity.TryGetValue(grid[i], out float density)) continue;
                if (occupied.Contains(i)) continue;

                // Thinned rather than cleared on a corridor: enough to keep the drawn
                // line legible from above without the line looking swept.
                float scale = clear != null && clear.Contains(i) ? 0.3f : 1f;

                int tufts = Mathf.FloorToInt(density * densityScale * scale);
                if (rng.Chance(density * densityScale * scale - tufts)) tufts++;

                // Reeds in the fen and on its margin, grass everywhere else. A bog does
                // not stop at a tile boundary — the ground goes soft before it goes wet,
                // and that margin is where the reeds are. Without the margin the marsh
                // has a hard edge you could measure with a ruler.
                grid.ToCoords(i, out int cx, out int cy);

                // Open water takes pads and nothing else. A reed standing in the middle
                // of a river is the same category of wrong as a lilypad in the grass.
                bool open = grid[i] == TerrainType.Water;
                if (open && !decor.Lilypads.Any) continue;

                // A riverbank is dressed like a fen's margin rather than like a meadow:
                // the ground beside moving water is soft, and reeds are what say so.
                bool wet = grid[i] == TerrainType.Marsh
                           || NextToMarsh(grid, cx, cy) || NextToWater(grid, cx, cy);

                var set = decor.MarshPlants.Any && wet ? decor.MarshPlants : decor.GroundCover;
                if (!open && !set.Any) continue;

                bool pads = decor.Lilypads.Any && (open || grid[i] == TerrainType.Marsh);

                for (int t = 0; t < tufts && placed < MaxGroundCover; t++)
                {
                    // A pad rather than a reed, and measured across rather than up.
                    var choice = pads && (open || rng.Chance(LilypadShare))
                        ? new Choice(decor.Lilypads, Any(decor.Lilypads, rng),
                                     LilypadWidth, byWidth: true, canopy: true)
                        : new Choice(set, Any(set, rng),
                                     CoverHeight, byWidth: false, canopy: true);

                    Scatter(parent, grid, rng, choice, i, heightScale, spread: 1.9f);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>Drops one model somewhere inside a tile, turned at random.</summary>
        static bool Scatter(Transform parent, TileGrid grid, DeterministicRandom rng,
                            Choice choice, int tile, float heightScale, float spread,
                            HashSet<int> occupied = null, float lift = 0f)
        {
            var position = Vec2.FromTile(grid, tile);
            float x = position.X + rng.Range(-spread, spread);
            float z = position.Y + rng.Range(-spread, spread);

            // Sampled the way the mesh is built, at the prop's own position. Using the
            // tile's own elevation instead leaves trees hovering above the ground or
            // buried in it, because the rendered surface is interpolated between corners
            // and a tile centre is a different number entirely.
            float groundY = grid.SurfaceElevation(x, z) * heightScale + lift;

            var instance = Object.Instantiate(choice.Prefab, parent);
            instance.transform.position = new Vector3(x, groundY, z);

            // Stand it up before measuring. Fitting to height only means anything once
            // the model's height is actually along Y.
            instance.transform.rotation = choice.ZUp
                ? Quaternion.Euler(-90f, rng.Range(0f, 360f), 0f)
                : Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

            // Zero would come out of a default Choice and scale the prop to nothing.
            float low = choice.Low > 0f ? choice.Low : JitterLow;
            float high = choice.High > 0f ? choice.High : JitterHigh;

            float size = choice.Size * rng.Range(low, high);
            if (choice.ByWidth) ModelScaling.FitToFootprint(instance, size, groundY);
            else ModelScaling.Fit(instance, size, groundY);

            // Canopy neither claims ground nor checks for it. Keeping it out of the
            // reserved set has a second effect worth having: grass and ferns may now
            // grow under a tree, where the tree's own footprint used to keep the floor
            // bare beneath it.
            if (choice.Canopy) return true;

            // Fitted before the ground is checked, because until it is fitted nobody
            // knows how much ground it wants. A big prop that cannot fit is destroyed
            // again rather than left standing through a watchtower.
            if (!FootprintClear(grid, occupied, x, z, FootprintRadius(instance)))
            {
                if (Application.isPlaying) Object.Destroy(instance);
                else Object.DestroyImmediate(instance);
                return false;
            }

            Reserve(grid, occupied, instance, x, z);
            return true;
        }

        /// <summary>
        /// Marks every tile the prop's own body covers, and not merely the one it was
        /// placed on.
        ///
        /// One tile per prop was the old rule and it is wrong by a factor of four at the
        /// worst. A mountain is drawn about `size * 1.2` across and size runs to 25 m, so
        /// it is a thirty-metre rock standing on one four-metre tile — everything placed
        /// within fifteen metres went inside it, and the mountainside came out with
        /// spruces growing out of the stone. What the player sees there is not two props
        /// overlapping; it is the world not being solid.
        ///
        /// The radius is read off the instance's own bounds rather than from a table of
        /// sizes, because after <see cref="ModelScaling"/> has fitted it the renderer
        /// knows how big the thing actually came out and a table only knows what was
        /// asked for.
        /// </summary>
        static void Reserve(TileGrid grid, HashSet<int> occupied, GameObject instance,
                            float x, float z)
        {
            float radius = FootprintRadius(instance);
            if (occupied == null || radius <= 0f) return;

            ForEachTileUnder(grid, x, z, radius, tile => occupied.Add(tile));
        }

        /// <summary>
        /// Whether a prop of this size can stand here without something already inside it.
        ///
        /// Checking only the centre tile is what let a mountain land eight metres from a
        /// watchtower and swallow it: the tower had reserved its own ground, but the
        /// mountain only ever asked about the one tile under its middle.
        ///
        /// Asked only of the big props. Below a tile's width, overlap is what a forest
        /// looks like — spruce canopies touch, and a tile of air around every tree would
        /// give an orchard.
        /// </summary>
        static bool FootprintClear(TileGrid grid, HashSet<int> occupied, float x, float z,
                                   float radius)
        {
            if (occupied == null || radius <= TileGrid.TileSize) return true;

            bool clear = true;
            ForEachTileUnder(grid, x, z, radius,
                             tile => { if (occupied.Contains(tile)) clear = false; });
            return clear;
        }

        /// <summary>Ground the prop's own body covers, as tile indices.</summary>
        static void ForEachTileUnder(TileGrid grid, float x, float z, float radius,
                                     System.Action<int> visit)
        {
            int span = Mathf.FloorToInt(radius / TileGrid.TileSize) + 1;
            int cx = Mathf.FloorToInt(x / TileGrid.TileSize);
            int cz = Mathf.FloorToInt(z / TileGrid.TileSize);
            float limit = radius * radius;

            for (int ty = cz - span; ty <= cz + span; ty++)
            {
                for (int tx = cx - span; tx <= cx + span; tx++)
                {
                    if (!grid.InBounds(tx, ty)) continue;

                    float dx = (tx + 0.5f) * TileGrid.TileSize - x;
                    float dz = (ty + 0.5f) * TileGrid.TileSize - z;
                    if (dx * dx + dz * dz <= limit) visit(grid.ToIndex(tx, ty));
                }
            }
        }

        /// <summary>
        /// How much ground the instance actually stands on, read off its own bounds.
        ///
        /// From the bounds rather than from a table of sizes, because after
        /// <see cref="ModelScaling"/> has fitted it the renderer knows how big the thing
        /// came out and a table only knows what was asked for. Height is left out: a pine
        /// is tall and stands on very little.
        /// </summary>
        static float FootprintRadius(GameObject instance)
        {
            if (instance == null) return 0f;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        /// <summary>
        /// Whether this is one of the props big enough to swallow what is already there,
        /// and so has to claim its ground before anything else is scattered near it.
        ///
        /// **Nothing qualifies today.** The mountains that did are off the map and on
        /// the skyline. The two passes stay because the castle and the keep are coming
        /// and they are exactly this: a thing that decides what can stand near it,
        /// rather than a thing that has to fit around what is already there.
        /// </summary>
        const float BulkySize = 14f;

        static bool IsBulky(TerrainType terrain, Choice choice) => choice.Size >= BulkySize;

        /// <summary>
        /// Places the things that were built rather than grown.
        ///
        /// Each kind goes where it would actually stand: houses on roads, fields on the
        /// open ground beside them, watchtowers in the passes, cut timber in the
        /// forest. That costs nothing over scattering them at random and it earns the
        /// player something — a house means a road is near, a watchtower means the pass
        /// is worth guarding. Scenery that can be read is worth more than scenery.
        /// </summary>
        static int PlaceLandmarks(Transform parent, TileGrid grid, DeterministicRandom rng,
                                  BiomeDecor decor, HashSet<int> clear, HashSet<int> occupied,
                                  float heightScale, IReadOnlyCollection<int> ruinSites)
        {
            int placed = 0;

            if (ruinSites != null && decor.Ruins.Any)
            {
                foreach (int tile in ruinSites)
                {
                    if (placed >= MaxLandmarks) break;
                    if (clear != null && clear.Contains(tile)) continue;
                    if (!occupied.Add(tile)) continue;

                    Place(parent, grid, tile, rng,
                          new Choice(decor.Ruins, Any(decor.Ruins, rng), RuinWidth, byWidth: true),
                          heightScale, occupied);
                    placed++;

                    // And a totem beside it, where the pack has one. A wreck says
                    // something happened here; a banner driven into the ground says
                    // somebody *chose* here, which is the difference between an accident
                    // and an ambush and is what the GDD's §5 table is asking for.
                    if (decor.Markers.Any)
                        Place(parent, grid, tile, rng,
                              new Choice(decor.Markers, Any(decor.Markers, rng), MarkerHeight,
                                         byWidth: false),
                              heightScale, occupied);

                    // Dead trees around it. A cart alone is small enough to miss from
                    // map height, and the signal is worthless if it is not noticed;
                    // bare trunks are tall, they read from above, and they say the same
                    // thing the cart does about this piece of ground.
                    if (!decor.DeadTrees.Any) continue;

                    for (int t = 0; t < 2; t++)
                    {
                        var dead = new Choice(decor.DeadTrees, Any(decor.DeadTrees, rng),
                                              DeadTreeHeight, byWidth: false,
                                              low: DeadJitterLow, high: DeadJitterHigh,
                                              canopy: true);

                        Scatter(parent, grid, rng, dead, tile, heightScale, spread: 2.6f);
                        placed++;
                    }
                }
            }

            for (int i = 0; i < grid.TileCount && placed < MaxLandmarks; i++)
            {
                if (clear != null && clear.Contains(i)) continue;
                if (occupied.Contains(i)) continue;

                grid.ToCoords(i, out int x, out int y);

                Choice choice = default;

                switch (grid[i])
                {
                    case TerrainType.Road when decor.Houses.Any && rng.Chance(0.035f):
                        choice = new Choice(decor.Houses, Any(decor.Houses, rng), HouseHeight, false);
                        break;

                    // Fields belong to a farm, and a farm belongs to a road. Scattered
                    // across open country they read as abandoned, which is a signal we
                    // have not earned the right to send yet.
                    case TerrainType.Plains when decor.Farms.Any && NearRoad(grid, x, y, 2) && rng.Chance(0.16f):
                        choice = new Choice(decor.Farms, Any(decor.Farms, rng), FarmWidth, true);
                        break;

                    case TerrainType.MountainPass when decor.Watchtowers.Any && rng.Chance(0.012f):
                        choice = new Choice(decor.Watchtowers, Any(decor.Watchtowers, rng),
                                            WatchtowerHeight, false);
                        break;

                    case TerrainType.Forest when decor.Timber.Any && rng.Chance(0.006f):
                        choice = new Choice(decor.Timber, Any(decor.Timber, rng), TimberWidth, true);
                        break;
                }

                if (choice.Prefab == null) continue;

                occupied.Add(i);
                Place(parent, grid, i, rng, choice, heightScale, occupied);
                placed++;
            }

            return placed;
        }

        /// <summary>
        /// One model picked for one spot, carrying everything placement needs.
        ///
        /// The up axis travels with the choice rather than being read from the biome,
        /// which is what lets a single map mix a Y-up nature pack with a Z-up scenery
        /// pack without either lying on its side.
        /// </summary>
        readonly struct Choice
        {
            public readonly GameObject Prefab;
            public readonly bool ZUp;
            public readonly float Size;
            public readonly bool ByWidth;

            /// <summary>How far this prop may vary from its table size. See TreeJitterLow.</summary>
            public readonly float Low;
            public readonly float High;

            /// <summary>
            /// A growing thing rather than stone or masonry.
            ///
            /// It decides who claims ground. Nothing may grow out of a rock, but a wood
            /// is things touching, so canopy neither reserves ground nor asks for clear
            /// ground. The rule used to be expressed as a size — anything past a tile's
            /// width had to find its whole footprint clear — and that held only while
            /// nothing green could reach a tile's width. Given their real range a
            /// fourteen-metre spruce has a four-and-a-half-metre crown, crossed the
            /// threshold, and the checker began reading two touching crowns as a tree
            /// growing out of a rock.
            /// </summary>
            public readonly bool Canopy;

            public Choice(PropSet set, GameObject prefab, float size, bool byWidth,
                          float low = JitterLow, float high = JitterHigh, bool canopy = false)
            {
                Prefab = prefab;
                ZUp = set != null && set.ZUp;
                Size = size;
                ByWidth = byWidth;
                Low = low;
                High = high;
                Canopy = canopy;
            }
        }

        /// <summary>Whether a road runs within <paramref name="radius"/> tiles.</summary>
        static bool NearRoad(TileGrid grid, int x, int y, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (grid.InBounds(nx, ny) && grid[nx, ny] == TerrainType.Road) return true;
                }
            }

            return false;
        }

        /// <summary>Stands one landmark on the centre of a tile, sized and seated.</summary>
        static void Place(Transform parent, TileGrid grid, int tile, DeterministicRandom rng,
                          Choice choice, float heightScale, HashSet<int> occupied = null)
        {
            var position = Vec2.FromTile(grid, tile);
            float groundY = grid.SurfaceElevation(position.X, position.Y) * heightScale;

            var instance = Object.Instantiate(choice.Prefab, parent);
            instance.transform.position = new Vector3(position.X, groundY, position.Y);

            // Buildings are square to the world in a way trees are not, so they turn in
            // quarters. A house at eleven degrees reads as subsidence.
            float yaw = rng.Range(0, 4) * 90f;
            instance.transform.rotation = choice.ZUp
                ? Quaternion.Euler(-90f, yaw, 0f)
                : Quaternion.Euler(0f, yaw, 0f);

            if (choice.ByWidth) ModelScaling.FitToFootprint(instance, choice.Size, groundY);
            else ModelScaling.Fit(instance, choice.Size, groundY);

            // A farm is nine metres across and a ruin five, so the landmarks need their
            // ground reserving for the same reason the mountain does.
            Reserve(grid, occupied, instance, position.X, position.Y);
        }

        /// <summary>
        /// What grows on one tile of a given terrain.
        ///
        /// A weighted draw rather than a chain of coin flips, because the shares *are*
        /// the design and a chain of flips hides them: the old version was four nested
        /// ifs and the actual proportion of one species to another was something you
        /// worked out with a pencil. Here the numbers are in a column and read down.
        ///
        /// The proportions come from the reference pictures. Forest is a spruce forest
        /// with other things in it — three fifths conifer, broadleaf and birch behind
        /// it — and a fifth of it is the shrub layer, whose absence is most of why the
        /// old forest read as trunks standing in a lawn. Plains are stone and shrub with
        /// the odd tree. The pass is rock and boulder under landform. The marsh is dead
        /// standing timber.
        /// </summary>
        static Choice Pick(BiomeDecor decor, TerrainType terrain, DeterministicRandom rng)
        {
            float roll = rng.Range(0f, 1f);

            switch (terrain)
            {
                case TerrainType.Forest:
                    if (roll < 0.44f) return Tree(decor.Pines, rng, PineHeight);
                    if (roll < 0.58f) return Tree(decor.Trees, rng, TreeHeight);
                    if (roll < 0.68f) return Tree(decor.Birch, rng, TreeHeight);
                    if (roll < 0.88f) return From(decor.Bushes, rng, BushHeight);
                    if (roll < 0.96f) return From(decor.Rocks, rng, RockHeight);
                    return From(decor.Timber, rng, TimberWidth, byWidth: true);

                // No whole mountains. A twenty-metre hill standing on a tile the caravan
                // has to walk over is a wall in the road — the column drove straight into
                // one — and a mountain is not what a pass looks like anyway: a pass is
                // the ground *between* the mountains, which is boulders, scree and the
                // trees that manage on it. The range belongs on the skyline, where
                // Horizon puts it. Its share went to the boulders, which are the thing
                // that reads as high country from inside it.
                case TerrainType.MountainPass:
                    if (roll < 0.40f) return From(decor.Boulders, rng, BoulderWidth, byWidth: true);
                    if (roll < 0.86f) return From(decor.Rocks, rng, RockHeight);
                    return Tree(decor.Pines, rng, PineHeight);

                // Standing water killing the trees is the thing a marsh looks like, and
                // a bare trunk is the most legible model in the pack from above.
                case TerrainType.Marsh:
                    if (roll < 0.46f)
                        return Tree(decor.DeadTrees, rng, DeadTreeHeight,
                                    DeadJitterLow, DeadJitterHigh);

                    // Its own plants, not the meadow's. A fen dressed in the same grass
                    // and ferns as the plains is a meadow that happens to slow you down.
                    if (roll < 0.76f) return From(decor.MarshPlants, rng, BushHeight);
                    if (roll < 0.88f) return From(decor.Bushes, rng, BushHeight);
                    if (roll < 0.96f) return From(decor.Rocks, rng, RockHeight);
                    return Tree(decor.Pines, rng, PineHeight);

                case TerrainType.Plains:
                case TerrainType.Road:
                    if (roll < 0.34f) return From(decor.Rocks, rng, RockHeight);
                    if (roll < 0.56f) return From(decor.Bushes, rng, BushHeight);
                    if (roll < 0.68f) return From(decor.Boulders, rng, BoulderWidth, byWidth: true);
                    if (roll < 0.84f) return Tree(decor.Trees, rng, TreeHeight);
                    if (roll < 0.94f) return Tree(decor.Birch, rng, TreeHeight);
                    return Tree(decor.Pines, rng, PineHeight);

                default:
                    return default;
            }
        }

        /// <summary>
        /// One prop from a set, or nothing when the set is empty.
        ///
        /// Empty is ordinary rather than exceptional: the weighted draw asks for a birch
        /// on a map dressed by a pack that has none, and the honest answer is a bare
        /// tile. Every caller already treats a null prefab as "place nothing".
        /// </summary>
        static Choice From(PropSet set, DeterministicRandom rng, float size,
                           float low = JitterLow, float high = JitterHigh,
                           bool byWidth = false) =>
            set != null && set.Any
                ? new Choice(set, Any(set, rng), size, byWidth, low, high)
                : default;

        /// <summary>A tree: the wide size spread a stand of them wants, and canopy rules.</summary>
        static Choice Tree(PropSet set, DeterministicRandom rng, float size,
                           float low = TreeJitterLow, float high = TreeJitterHigh) =>
            set != null && set.Any
                ? new Choice(set, Any(set, rng), size, false, low, high, canopy: true)
                : default;

        static GameObject Any(PropSet set, DeterministicRandom rng) =>
            set.Models[rng.Range(0, set.Models.Length)];
    }
}
