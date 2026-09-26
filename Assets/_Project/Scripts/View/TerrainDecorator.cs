using System.Collections.Generic;
using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.View
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

        /// <summary>
        /// What share of its own size every model in this set is buried by, when the place
        /// it is put does not say otherwise. Zero rests it on the ground.
        ///
        /// <b>Because the models decide this, not the sites.</b> A sink was something a
        /// placement asked for — the marsh buries its dead trees, a landmark buries its
        /// stumps — which works while a set is the same kind of thing in every country.
        /// The fen's trees are not: they are the pack's swamp trees, drawn with a root
        /// flare spreading out from the trunk, and set on the surface like a pine they
        /// stand on the flare as if on legs. A set that knows its own models have footings
        /// can say so once, here, instead of every site having to know which country it is
        /// dressing.
        /// </summary>
        public float Sink;

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
        /// Wood that is already down: the swamp's fallen branches.
        ///
        /// Split out of <see cref="DeadTrees"/>, which is otherwise trunks that stand.
        /// The pack files both under Trees and the set inherited the filing, but they are
        /// two different props wearing one name — a standing trunk is a vertical line read
        /// from above, and a fallen branch is a shape on the floor. Sharing a set forced
        /// one rule onto both: the height that suits a trunk stretched the branches, and
        /// the only tool that lays a thing down would have felled every trunk to reach
        /// them, because it cannot be told which is which after the fact.
        ///
        /// So they are told apart here instead, where somebody decides it, rather than
        /// derived later from whichever way round a model happens to have been drawn.
        /// </summary>
        public PropSet Deadfall = new PropSet();

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
        /// Flowers, for the beds they grow in rather than for the scatter.
        ///
        /// <b>A flower in the ground cover is one tuft in forty.</b> The cover is sown a
        /// tuft or two to the tile out of a set of grasses, so wildflowers come out as the
        /// odd speck of colour in a green field - which is what a lawn with weeds in it
        /// looks like, and not what a meadow looks like. A meadow has *drifts*: twenty
        /// square metres of one colour, then grass, then another drift. Those are laid as
        /// beds (PlaceFlowerBeds) out of this set.
        /// </summary>
        public PropSet Flowers = new PropSet();

        /// <summary>
        /// Grass by the square metre rather than by the tuft.
        ///
        /// <b>What a meadow is made of, and the pack draws it as mats.</b> The ground
        /// cover is clumps: half-metre tufts sown a couple to the tile, which read from
        /// two metres away and disappear from the height the game is played at - the open
        /// country came out as a flat green sheet with specks on it however many tufts
        /// were thrown at it, because a speck is a speck. The pack also ships grass as
        /// planes, several metres across, and they had never been used at their own size:
        /// one is in the cover list, fitted by height to seven-tenths of a metre, which
        /// shrinks a four-metre mat to a pin.
        ///
        /// Laid by width, about a tile across, one to a tile. It costs one object where
        /// the same coverage in tufts costs thirty.
        /// </summary>
        public PropSet Mats = new PropSet();

        /// <summary>
        /// Swells in the open ground: a hummock with grass over it.
        ///
        /// The map's own relief is the country's shape, and it is smooth at this scale -
        /// a meadow seen from above with nothing standing on it is a flat green sheet
        /// however well it is coloured. These are the pack's ground mounds, which is a
        /// piece of ground rather than a thing standing on it.
        /// </summary>
        public PropSet Mounds = new PropSet();

        /// <summary>
        /// What flies over a meadow on a summer day.
        ///
        /// Butterflies, blown petals, drifting seed. They are particle effects rather than
        /// models: nothing about them is solid, nothing claims ground, and they are the
        /// only thing in the dressing that moves on its own while the plan is being read.
        /// </summary>
        public PropSet Fauna = new PropSet();

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
        /// <summary>
        /// The loose pieces that belong *around* a wreck, never instead of one.
        ///
        /// A cart wheel is not a landmark. Placed on its own and scaled to a landmark's
        /// five metres it becomes a five-metre wheel standing upright in a meadow, which
        /// is what went out — and the lesson is more general than the wheel: a prop that
        /// only reads as part of something has to be placed as part of something. These
        /// are laid flat, small, and only ever beside a wreck that is already there.
        /// </summary>
        /// <summary>
        /// The building kit: the pieces houses, towers and ruins are stacked out of.
        ///
        /// Takes precedence over <see cref="Houses"/> and <see cref="Watchtowers"/> where
        /// it can build the thing, and those stay as the fallback for a pack that ships
        /// whole buildings instead of a kit. See BuildingBuilder.
        /// </summary>
        public BuildingKit Kit = new BuildingKit();

        /// <summary>
        /// What makes a village a place somebody lives rather than a row of houses.
        ///
        /// <b>Houses alone do not read as a village.</b> Five buildings in a field is a
        /// building site; what says people live here is the ground between them being
        /// used — a fence round a plot, a cart standing where it was left, a shed against
        /// a gable, a wheel turning in the river. None of it is a landmark and none of it
        /// is scenery either: it is the difference between somewhere and something.
        ///
        /// Kept as sets of their own rather than folded into Wreckage or Ruins, which is
        /// where the cart and the crate already live. Those two mean *something went
        /// wrong here*, and a village is the opposite claim.
        /// </summary>
        public PropSet Fences = new PropSet();

        /// <summary>A shed, a lean-to: the small buildings that lean on the big ones.</summary>
        public PropSet Sheds = new PropSet();

        /// <summary>A cart left standing, hay, crates — a yard in use.</summary>
        public PropSet Yard = new PropSet();

        /// <summary>
        /// The mill wheel and the frame it turns in, which are two models and one thing.
        ///
        /// Index-matched with <see cref="MillSupports"/>: the wheel goes in the water and
        /// the support stands on the bank, and a wheel without its frame hangs in the
        /// river.
        /// </summary>
        public PropSet Mills = new PropSet();
        public PropSet MillSupports = new PropSet();

        /// <summary>Tied up where the water is deep enough to tie one up.</summary>
        public PropSet Boats = new PropSet();

        /// <summary>
        /// Whether the passes of this country are walled with rock.
        ///
        /// <b>Cliff tiles only ever happen in a town.</b> The generator draws a town's
        /// blocks as cliff and nothing else in the game does, so the Cliffs set was dressed
        /// and almost never used - and the mountains, which are a third bare pass, came out
        /// as open ground with a grey tint. In a country that says yes to this, every pass
        /// tile is a candidate for a rock face, and the roads thread between them.
        /// </summary>
        public bool RockPasses;

        /// <summary>
        /// How tall this country's rock faces stand, against TerrainDecorator.CliffHeight.
        ///
        /// One everywhere but the mountains, where a face is a wall of a ravine and five
        /// metres is a step. At two and a bit it is eleven metres - three draught horses
        /// stacked up - which is what the road threads between.
        /// </summary>
        public float CliffRise = 1f;

        /// <summary>
        /// The sheet of falling water, stood in the step where a river drops. See PlaceFalls.
        /// </summary>
        public PropSet Falls = new PropSet();

        /// <summary>The spray at the foot of one.</summary>
        public PropSet Whitewater = new PropSet();

        /// <summary>
        /// Laid ground: cobble, flag and dressed stone for a town's streets.
        ///
        /// Every other surface in this game is vertex colour on the terrain mesh, which
        /// is right for meadow and forest floor and poor for a city — a street is a made
        /// thing. The pack ships the pieces to make one with and this project had never
        /// loaded them; the town's streets were grey paint over the same ground the
        /// woods stand on.
        /// </summary>
        public PropSet Paving = new PropSet();

        /// <summary>
        /// What a street has that a road does not: a lamp on a post, a brazier, a sign
        /// hung off a gable.
        /// </summary>
        public PropSet Street = new PropSet();

        /// <summary>
        /// A statue on its base, and the plinths a town puts things on.
        ///
        /// One to a town, in the open ground by a gate. A monument is a thing a place
        /// raised once and is known by; two of them in one town is a garden centre.
        /// </summary>
        public PropSet Monuments = new PropSet();

        public PropSet Wreckage = new PropSet();

        /// <summary>
        /// The broken wagon that lies at a trap, whole rather than in pieces.
        ///
        /// <b>Its own set because it is a site, and Wreckage is not.</b> Every model in
        /// Wreckage is fitted to DebrisWidth, 1.3 m, because a wheel, a crate and a
        /// barrel are all about that across and the set is drawn from as a bag of loose
        /// pieces. This is one model, one metre ninety long, and it is the thing the
        /// player is meant to read from the air: put it in that bag and it would be sized
        /// to a barrel and dealt out one time in six.
        /// </summary>
        public PropSet Wrecks = new PropSet();

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
            !Has(Trees) && !Has(Pines) && !Has(Birch) && !Has(DeadTrees) && !Has(Deadfall) &&
            !Has(Bushes) &&
            !Has(Rocks) && !Has(Boulders) && !Has(Horizon) &&
            !Has(GroundCover) && !Has(MarshPlants) && !Has(Lilypads) &&
            !Has(GroundPatches) && !Has(Houses) && !Has(Farms) && !Has(Watchtowers) &&
            !Has(Timber) && !Has(Ruins) && !Has(Wreckage) && !Has(Markers) && !Has(Water) && !Has(Fords) &&
            !Has(Cliffs) && !Has(Camps) && !Has(Willows) && !Has(Shore) && !Has(Backdrop) &&
            (Kit == null || Kit.IsEmpty);

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
        public const float HouseHeight = 7f;

        /// <summary>
        /// What a second storey adds, in metres before the landmark multiplier.
        ///
        /// <b>A house was scaled to one height however many storeys it had.</b>
        /// BuildingBuilder stacks a foundation, a room, sometimes an upper room, and a
        /// roof — and Raise then fitted the whole stack to HouseHeight regardless. So a
        /// cottage and a two-storey house standing beside each other came out the same
        /// height, which means their storeys did not: the cottage got a hall with a
        /// four-metre ceiling and the tall one got two squashed floors. It is why some of
        /// them read as three houses piled up.
        ///
        /// Two metres, which is a floor. Not a multiplier: a storey is a fixed thing, and
        /// scaling by a ratio would make the farm's extra floor shorter than the house's
        /// for no reason anybody could name.
        /// </summary>
        public const float UpperStoreyRise = 2f;

        /// <summary>The height to fit a stacked building to, by what it was stacked from.</summary>
        static float Storeys(float baseHeight, bool twoStorey)
            => twoStorey ? baseHeight + UpperStoreyRise : baseHeight;
        public const float WatchtowerHeight = 8f;

        /// <summary>How far into the ground a building is set, as a share of its size.</summary>
        public const float BuildingSink = 0.12f;
        public const float FarmWidth = 9f;

        /// <summary>
        /// How tall a piece of fallen wood stands, in metres.
        ///
        /// Measured on the height, which is the way round that survives the set. Timber
        /// was fitted to a three-metre footprint, and that is right for a log — the pack
        /// authored one at 3.05 m — but the stumps are narrow and it dragged them up by
        /// their width until SM_Tree_Stump_04, a 0.40 m stump, stood 12 to 30 times its
        /// own size: a 36-metre stump, taller than the watchtower, which is what the
        /// tallest-built report kept complaining about. Fitting on height instead puts a
        /// stump at knee height where it belongs and lets the width cap hold the logs,
        /// which lose about a metre of their authored length and are still logs.
        /// </summary>
        public const float TimberHeight = 1.4f;

        /// <summary>How many times its height a piece of timber may be wide. See TimberHeight.</summary>
        public const float TimberSpread = SpreadLimit;

        /// <summary>
        /// How many times its height a dead tree may be wide.
        ///
        /// Looser than <see cref="SpreadLimit"/>, because a bare trunk keeps its branches
        /// and they are the point — it is the most legible model in the pack from above.
        /// Tighter than a canopy's nothing, because the set is not all trunks: the pack
        /// files the swamp's roots and fallen branches here too, and those are drawn
        /// lying down. Given a nine-metre height and no width to answer to, one reached
        /// twenty-four metres across. Capped, the demand that binds is the width, and the
        /// branch settles at the sprawl it was drawn as instead of a nine-metre tree.
        /// </summary>
        public const float DeadTreeSpread = 0.8f;

        /// <summary>
        /// How far across a fallen branch lies, in metres.
        ///
        /// Measured across rather than up, the way the wreckage is, because that is the
        /// dimension a thing on the floor has. Four metres is a branch off one of the
        /// nine-metre trunks standing over it, which is where these came from.
        /// </summary>
        public const float DeadfallWidth = 4f;

        /// <summary>
        /// What share of a stump's height sits below the ground.
        ///
        /// Two fifths, which is far more than a building's taper because a stump is
        /// mostly root: the pack models the flare where the trunk spreads into the
        /// ground, and that flare is meant to be in the ground. Left on the surface it
        /// reads as legs and the stump appears to be standing on them.
        ///
        /// Settled by looking rather than by arithmetic — the models are low-poly enough
        /// that sampling the mesh for where the flare ends gives a number the eye then
        /// disagrees with. Three depths were rendered from one camera on one level. At a
        /// quarter the roots are still clear of the ground; at eleven twentieths the
        /// stump is cut off and has lost its shape. This is the one in between.
        /// </summary>
        public const float StumpSink = 0.40f;

        /// <summary>
        /// The most of itself a prop may be buried, as a share of its own height.
        ///
        /// Half, and it is a floor under the whole idea rather than a number anybody
        /// chose. A share of a height is only a flare while the height is right; get the
        /// height wrong and the same share is a grave. That is what happened to the
        /// timber — see <see cref="Bury"/> — and the ground it is standing on is another
        /// way in, because a prop on a slope is pushed down by the fall as well.
        ///
        /// Anything asking for more than half of itself is asking to be invisible, and an
        /// invisible prop is drawn, lit and paid for exactly like a visible one.
        /// </summary>
        const float MostOfItself = 0.5f;

        /// <summary>
        /// Seats a prop into the ground it is standing on.
        ///
        /// Called after the model has been fitted, and that is the whole point: the depth
        /// is a share of what the model actually came out as, not of the height it was
        /// asked for. The two agree for a tree, which is fitted by height, and part
        /// company for anything the width cap has shrunk — which is every log, every
        /// fallen branch and every low, broad clump of growth on the map.
        /// </summary>
        static void Bury(GameObject instance, float share, float slope)
        {
            var box = ModelScaling.Measure(instance);
            float height = box.size.y;
            if (height <= 0f) return;

            float depth = height * share + slope;

            // Half of itself at most — unless it is a flat thing, and then all but a
            // finger of it.
            //
            // The cap is there so nothing is swallowed by the ground, and a paving stone
            // laid flush is not swallowed: it is laid. Paving is five to fourteen
            // centimetres thick, so half of it left proud is a kerb across every tile,
            // which is what a street of slabs sitting on the grass looks like.
            bool flat = height < Mathf.Max(box.size.x, box.size.z) * 0.25f;
            float most = flat ? height - 0.02f : height * MostOfItself;

            if (most < 0f) most = 0f;

            instance.transform.position += Vector3.down * (depth > most ? most : depth);
        }

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
        /// <summary>How wide a bridge's deck is, and how far it reaches.</summary>
        // Five metres of deck: a wagon is two and a half and the escort walks beside it,
        // so anything narrower is a plank the caravan straddles. Twelve of span, which is
        // the ford tile plus a bank at each end — a bridge that stops at the waterline is
        // a jetty. See ModelScaling.FitToCrossing for why both numbers are needed.
        /// <summary>
        /// How wide the roadway has to be, in metres.
        ///
        /// <b>Five, and the span no longer follows it.</b> This was cut to 3.5 because
        /// the width was stretching the bridge — FitToCrossing scales uniformly and takes
        /// the larger demand, so a five-metre deck dragged a twelve-metre crossing out to
        /// a twenty-one-metre bridge, which the log said plainly: "span asked 12,0 m and
        /// got 21,0 m". But 3.5 is a wagon and half a metre either side, and a column
        /// crossing on it has its flanks over the water.
        ///
        /// The two are separated now: the model is fitted by the span alone and then
        /// widened on its own axis (see ModelScaling.Widen), so the deck may be as wide as
        /// a wagon needs without the bridge growing into a viaduct. Five metres is two
        /// wagons abreast, which is what the crossing has to look like it could carry.
        /// </summary>
        /// <b>Nine, and the last metre of it is measured rather than judged.</b>
        ///
        /// BridgeReport walks every corridor of thirty levels and reads how far across the
        /// deck the column drifts while it is on the timber. The answer is not a small
        /// angle: eighty-nine of a hundred and sixteen crossings drift under a metre, and
        /// the other twenty-seven step a whole tile sideways — four metres — with nothing
        /// in between. It is a road that either holds the ford's row or does not.
        ///
        /// Carrying the four-metre step needs eleven metres of deck, and these bridges are
        /// fourteen metres long. Fourteen by eleven is a raft. So this covers the
        /// eighty-nine with a metre to spare either side and leaves the twenty-seven
        /// crabbing, which is the honest trade: the alternative was straightening the road
        /// at the crossings, and that moves it past what the player drew round — measured,
        /// it cost chapter one's tenth level both of its winnable roads even when the road
        /// was forbidden to grow by a single tile.
        public const float FordDeck = 9f;
        /// <summary>
        /// The shortest a bridge is ever built, in metres.
        ///
        /// <b>Sixteen, up from twelve, because a bridge that only just spans its water
        /// reads as a plank somebody laid across it.</b> Watched from the ground the deck
        /// wants to be a piece of road that happens to be over a river, not the minimum
        /// timber that reaches both banks — and the crossings carry four to eight metres of
        /// water, so twelve was that minimum almost everywhere.
        /// </summary>
        public const float FordSpan = 16f;

        /// <summary>
        /// How far onto each bank the bridge reaches past the water, in metres.
        ///
        /// <b>Without this the bridge collapsed to its floor.</b> Splitting the width off
        /// the length left FitToCrossing with only the span term, and the span was the
        /// twelve-metre floor on every crossing — so a bridge that had been twenty-one
        /// metres became twelve, and because the fit is uniform its height came down with
        /// it to four parts in seven. What was left was a plank at the water's edge.
        ///
        /// Measured: the crossings carry four to eight metres of water, two tiles at the
        /// median. Three metres of landing either side puts the span at fourteen to
        /// eighteen, which is a bridge that meets dry ground at both ends rather than one
        /// that stops where the river does.
        /// </summary>
        public const float BridgeLanding = 5f;

        /// <summary>How much clear ground the bridge keeps around itself, in metres.</summary>
        public const float BridgeClearance = 6f;

        /// <summary>
        /// How far the roadway sits above the bank it meets, in metres.
        ///
        /// A quarter of a metre — enough that the deck does not fight the ground for the
        /// same pixels, and not enough to be a step.
        ///
        /// <b>This replaces a height cap, and the difference is the whole bug.</b> A
        /// bridge used to be stood on its underside, which for an arched one puts the
        /// footings on the bank and the roadway a storey up: the caravan drove along the
        /// top and the vault below it, mouth open at ground level, read as a tunnel. The
        /// answer then was to cap the height at three metres, and because the cap scales
        /// the model uniformly it bought that by shrinking the bridge to a stub that
        /// crossed nothing — seven metres of span where twelve was asked for, and a deck
        /// narrower than a wagon.
        ///
        /// A bridge is not shorter than its river. It is *sunk*: the footings belong in
        /// the channel and the roadway belongs level with the road it joins. So the deck
        /// is measured after fitting and the whole thing dropped until it sits here — see
        /// <see cref="Bridge"/>. An arch's ends then dip a little under the bank, which is
        /// what an arch does where it meets a road, and no cap is needed at all.
        /// </summary>
        public const float DeckClearance = 0.25f;

        /// <summary>How tall a cliff face stands.</summary>
        // Five metres, down from twelve. A cliff tile is impassable ground on a flat map
        // rather than the lip of a drop, so whatever stands on it stands in the open and
        // is read against what is beside it: twelve metres is five draught horses stacked
        // up, which stops being scenery and becomes a landmark in the middle of a field.
        // Five is a rock a man could not climb, which is all the tile is claiming.
        public const float CliffHeight = 5f;

        /// <summary>
        /// A raiders' tent, sized to be seen rather than to be slept in.
        ///
        /// Four metres, up from 2.6. The honest number was the old one — a tent is a bit
        /// over the height of the man inside it — and it was reported as tiny twice.
        /// Here is the arithmetic behind why: the run multiplies a landmark by
        /// LevelRunner.LandmarkScale (1.6), so 2.6 came out at 4.2 m beside a wagon of
        /// 3.2 (VisualLibrary.WagonHeight). One and three tenths of a wagon is not a
        /// camp, it is a bivouac, and the camp is supposed to be a *place* on the map —
        /// the thing the bandits come out of.
        ///
        /// Four gives 6.4 m in the run, two wagons, which is a tent somebody holds court
        /// in. On the plan map the multiplier is one and the floor takes over, so nothing
        /// changes there.
        ///
        /// Honest proportion lost to legibility deliberately, and it is the same trade
        /// the houses already made at 6 m x 1.6 = 9.6.
        /// </summary>
        public const float CampHeight = 4f;

        /// <summary>
        /// A willow, which is shorter than the spruce it stands among.
        ///
        /// Ten metres against a spruce's fourteen, because a willow leans out over water
        /// rather than up out of a wood, and one drawn to a conifer's height beside a
        /// stream is the only tree on the map you would notice from the map.
        ///
        /// <b>Written as the height it reaches rather than the height it starts at,
        /// because the two were compared as though they were the same thing.</b> The ten
        /// was a table value and the fourteen was a measurement: every tree here is
        /// jittered up to <see cref="TreeJitterHigh"/>, so a spruce entered at 8.5 m
        /// arrives at 14.4 and a willow entered at 10 arrives at 17. The willows came out
        /// the three tallest trees in the wood — 16.5, 16.3 and 15.4 m against every
        /// pine's 14.4 — which is precisely the tree this note was written to prevent.
        ///
        /// So the ten is stated where it can be checked against the fourteen, and the
        /// table value is derived from it. Both numbers now mean the same kind of thing.
        /// </summary>
        public const float WillowTallest = 10f;

        public const float WillowHeight = WillowTallest / TreeJitterHigh;

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
        /// How wide a tuft of ground cover may be, whatever its height comes to.
        ///
        /// One metre. Fitting by height alone multiplies the width with it, and the
        /// nature pack's grass is authored low and broad: five clumps, fifteen hundred
        /// instances, each blown up by four or five to reach seven-tenths of a metre and
        /// arriving five metres across. That is the green blob, and it was blamed on the
        /// ground patches, on the forest's colour and on a stale scene before anybody
        /// asked the map what it was carrying.
        /// </summary>
        public const float CoverWidth = 1f;

        /// <summary>How much wider than tall anything but a tree may end up.</summary>
        // A bush, a reed, a fern, a rock: all of them are fitted by height and all of
        // them carry whatever width that scaling happens to give. Half again is generous
        // for every one of them and stops a low broad model from spreading into a mat.
        // Trees are exempt, because a canopy is exactly the thing this would clip.
        public const float SpreadLimit = 1.5f;

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
        /// The same waist height that decides what the escort walks round, and they are
        /// deliberately one number: what a man has to go round is what a wagon has to go
        /// round, and two rules would mean a boulder the troops avoid standing in a road
        /// the wagons drive straight over.
        ///
        /// It was two metres, which sorted the table cleanly — a rock is 2.2, a boulder
        /// 5.5, a tree 7 to 8.5 — and left the 1.9 m bushes standing in the road, where
        /// they are the thing you can actually see the column pass through.
        /// </summary>
        public const float DriveClearance = SolidHeight;

        /// <summary>
        /// How far either side of the route the ground has to be clear, in tiles.
        ///
        /// One, which is twelve metres of lane against the corridor's four. The corridor
        /// is where the *wagons* go and it was all that was kept clear, so the wagons had
        /// their line and everything walking beside them did not: the flank posts stand
        /// six metres out — a tile and a half — and walked through every trunk and
        /// boulder on the verge. The van and the rearguard stay on the line, so it is the
        /// flanks that set this number.
        ///
        /// Only things a wheel cannot roll over are refused, so the grass, the flowers,
        /// the bushes and the loose stones all still grow in the lane and the country
        /// does not turn into a swept avenue with the caravan in the middle of it.
        /// </summary>
        public const int DriveMarginTiles = 1;

        /// <summary>
        /// How wide the caravan's own sweep is, in metres either side of the path.
        ///
        /// Eight: the flank posts stand at six (Squad.FlankOffset) and the last two are
        /// the soldier's own width and the trunk's. Handed to Caravan.Sweep, which walks
        /// the path — so this covers the run-up and the corners a tile list does not
        /// describe, and a caller that passes a swept lane wants no further margin on
        /// top of it.
        /// </summary>
        public const float DriveHalfWidth = 8f;

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
            // Enough that a forest tile has a tree on it.
            //
            // At 0.45 across two passes, and with two thirds of the forest table being
            // trees rather than bushes or rock, a little over half of them did — which is
            // a wood with gaps you can see the far side through, and it is why the forest
            // had to be argued for in the ground colour. It should not have to be. Three
            // in four now, which is the number that makes the canopy the thing that says
            // forest.
            { TerrainType.Forest, 0.62f },
            { TerrainType.MountainPass, 0.18f },

            // Not bare. Three percent is a lawn with a shrub on it, and the road spends
            // a good deal of its length crossing this: the country looked empty not
            // because the forest was thin but because the route was rarely in it. A
            // meadow has copses, single trees and thickets in it, which is what the plains
            // table is mostly made of — the number was the only thing keeping them off.
            { TerrainType.Plains, 0.11f },

            // <b>A fen is not a lawn, and 0.06 made it the barest ground on the map.</b>
            // Marsh was thinner than plains and second only to the road, so the one
            // terrain whose whole character is standing water, drowned trees and rotting
            // growth came out as empty green with a stump on it. The marsh table is
            // already right — near half of it is dead and swamp trees and another third
            // its own plants — and the density was the only thing keeping them off, the
            // same fault the plains had before it.
            //
            // 0.45, just under the forest's 0.62, because a swamp is dense but its trees
            // are not a closed canopy. On a 64x64 map that is about 400 marsh tiles, so
            // roughly 180 props where there were 24 — and MaxProps still caps the level.
            { TerrainType.Marsh, 0.45f },
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
        /// <summary>
        /// Metres below which a landmark is drawn bigger than it is, or nought for life
        /// size.
        ///
        /// **A floor, not a multiplier**, and that distinction is the design. The plan
        /// map is 256 m across read from four hundred back, where a five-metre skeleton
        /// is a smudge and a six-metre house is another smudge — which is exactly why
        /// this project drew symbols over them in the first place. Multiplying everything
        /// would take the castle to sixty-six metres and a quarter of the map with it.
        /// Raising only what is *under* the floor lifts the bones, the house, the farm and
        /// the totem into legibility and leaves the tower at fifteen and the castle at
        /// twenty-two exactly as they are.
        ///
        /// It is the same argument as VisualLibrary.EagleSpan, which draws a two-metre
        /// bird at ten: over a map at this scale a landmark is a marker that happens to
        /// be shaped like the thing it marks.
        ///
        /// Only the plan sets it. In the run you are standing among these buildings and a
        /// house has to be a house.
        ///
        /// Static because Decorate is the one entry point and sets it on the way in, and
        /// because the alternative is threading a float through eight signatures of a
        /// file this size. Written down rather than hidden: it is state, and state that
        /// is not obvious is the kind that surprises somebody later.
        /// </summary>
        static float _landmarkFloor;

        /// <summary>
        /// What every landmark's own size is multiplied by, or one for life size.
        ///
        /// **A multiplier where the plan uses a floor, and the two are different tools
        /// for different jobs rather than one tool used twice.** On the map everything
        /// built is a marker and all that matters is that it can be made out, so a floor
        /// is right and a tent coming out as tall as a house costs nothing. In the run
        /// you are down among them and relative size is the whole of it: a floor there
        /// would stand a raiders' tent as high as a farmhouse, which is worse than the
        /// problem it fixes.
        ///
        /// It exists because a house at six metres beside a wagon at 3.2 reads as small.
        /// The proportions are honest — a real cottage is about that against a real cart
        /// — but the wagons are what the eye anchors on, and a building barely twice one
        /// of them does not read as a building somebody lives in.
        /// </summary>
        static float _landmarkScale = 1f;

        /// <summary>
        /// The tiles the wagons drive over, while a level is being dressed.
        ///
        /// Held here rather than passed because the passes that need it are the deepest
        /// ones - a single rock in one course of one tor - and threading a parameter down
        /// to them means touching every scatter in the file. The same reasoning as
        /// <see cref="_landmarkScale"/>, which is also a fact about the level rather than
        /// about the prop being placed. Null while the planning map is drawn: it has no
        /// caravan in it.
        /// </summary>
        static HashSet<int> _road;

        public static int Decorate(Transform parent, TileGrid grid, int seed, BiomeDecor decor,
                                   IReadOnlyCollection<int> keepClear = null,
                                   float heightScale = 0f, int maxProps = 600,
                                   float densityScale = 1f,
                                   IReadOnlyCollection<int> ruinSites = null,
                                   bool horizon = true,
                                   IReadOnlyCollection<int> driveLine = null,
                                   IReadOnlyCollection<int> campSites = null,
                                   int driveMargin = DriveMarginTiles,
                                   IReadOnlyCollection<int> travelled = null,
                                   List<Landmark> found = null,
                                   int goalTile = -1,
                                   float minimumLandmark = 0f,
                                   float landmarkScale = 1f,
                                   Material waterMaterial = null,
                                   Material marshWaterMaterial = null,
                                   IReadOnlyCollection<int> apronOpenings = null,
                                   int village = -1,
                                   bool settled = true,
                                   Towns.Plan town = default,

                                   // The tile the chapter.s champion waits on, so the castle
                                   // can be stood on his side of the goal rather than on
                                   // whichever side the loop reached first. See
                                   // Strongholds.Site: he is mechanism and cannot be moved to
                                   // the castle without costing a chapter, so the castle comes
                                   // to him.
                                   int guard = -1)
        {
            // Before the early return below, so a call that decorates nothing still
            // leaves the floor at what this caller asked for rather than at what the
            // last one did.
            _landmarkFloor = minimumLandmark;
            _landmarkScale = landmarkScale <= 0f ? 1f : landmarkScale;

            if (decor == null || decor.IsEmpty) return 0;

            // One stream per stage, each drawn from the seed and nothing else.
            //
            // <b>There was one stream for the whole map, and the planning map and the run
            // each drew a different amount from it.</b> The run lays the skyline and the
            // apron before the fords and the plan does not; the plan scatters its ground
            // cover at a denser scale and so throws more dice. Everything placed after
            // that — which crossing gets the bridge, which model, where the stones and the
            // cliffs and the camps go — came out of a stream standing somewhere else in
            // each, and the bridge the player planned their route over was on another
            // crossing in the game. A stage with its own stream is the same stage in
            // both, whatever else either of them did first.
            DeterministicRandom Stream(int stage) => new DeterministicRandom(seed ^ (0x5EED10 + stage * 0x3C6EF35F));

            var clear = keepClear == null ? null : new HashSet<int>(keepClear);

            // A set, not the list it arrives as. IReadOnlyCollection has no Contains
            // worth the name, and this is asked once per prop on every tile of the map.
            var road = Lane(grid, driveLine, driveMargin);
            _road = road;
            int placed = 0;

            // Landmarks first, and the tiles they take are then off limits to the
            // scatter. Done the other way round a pine grows through the roof of the
            // farmhouse, and the building — the thing the eye was meant to find — is
            // the one that loses.
            var occupied = new HashSet<int>();

            // The castle first of anything, and the order is the point.
            //
            // PlaceLandmarks argues below why the built things take their ground before
            // the scatter — otherwise a pine grows through the farmhouse roof and the
            // building, the thing the eye was meant to find, is the one that loses. A
            // castle is the largest of them by a long way, so it claims first.
            placed += PlaceCastle(parent, grid, Stream(0), decor, occupied, heightScale,
                                  goalTile, travelled, found, guard);

            // The town before any of it, because its walls are the largest built thing
            // on any map and they are not negotiable: the ground they stand on was made
            // impassable before the ways through were found, so nothing else may take it.
            placed += PlaceTown(parent, grid, Stream(11), decor, occupied, heightScale,
                                town, found, road);

            // And the village next, for the same reason and one more: it is the only
            // built thing on the map whose position was decided before the decorator was
            // called (Settlements.Site), so it cannot be moved out of the way of anything
            // that got there first. It goes down while the ground is still empty.
            placed += PlaceVillage(parent, grid, Stream(10), decor, occupied, heightScale,
                                   village, road, found);

            placed += PlaceLandmarks(parent, grid, Stream(1), decor, clear, occupied, heightScale,
                                     ruinSites, road,
                                     travelled == null ? null : new HashSet<int>(travelled),
                                     found, settled);

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

                    // Whether this tile touches water, so a boulder is not dropped into a
                    // crossing — see Pick.
                    grid.ToCoords(i, out int bankX, out int bankY);
                    var choice = Pick(decor, terrain, passRng,
                                      NextToWater(grid, bankX, bankY));
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
                placed += PlaceHorizon(parent, grid, Stream(2), decor);

                // And the ground between the two: the apron the skirt draws outside the
                // grid, which has been bare since it was added. Under the same flag as the
                // skyline because it is the same job — what the map ends in — and the
                // planning view turns both off, where a fringe of trees would only hide
                // the corner of the map somebody is trying to read.
                placed += PlaceApron(parent, grid, Stream(3), decor, heightScale, apronOpenings);
            }

            // Bridges before anything scattered, and they claim the ground they cover.
            //
            // A bridge is twelve metres long and stands on one tile, so it reaches three
            // tiles into its neighbours — and it used to be built last, over ground the
            // shore pass had already strewn with boulders. One of them came up through
            // the deck. Placed first and claiming its whole footprint, the stones go
            // round it.
            placed += PlaceFords(parent, grid, seed, Stream(4), decor, occupied, heightScale,
                                 found, travelled);

            placed += PlacePatches(parent, grid, Stream(12), decor, heightScale,
                                   densityScale, travelled);
            placed += PlaceGroundCover(parent, grid, Stream(5), decor, clear, occupied,
                                       heightScale, densityScale);

            // The grass itself, under everything the cover sows: see BiomeDecor.Mats.
            placed += PlaceMats(parent, grid, Stream(18), decor, occupied, heightScale,
                                densityScale, clear);

            // The meadow's own three, after the floor is sown and before the water goes
            // on: drifts of flowers, hummocks in the open, and something flying over it.
            placed += PlaceFlowerBeds(parent, grid, Stream(15), decor, occupied, heightScale,
                                      densityScale, road);
            placed += PlaceMounds(parent, grid, Stream(16), decor, occupied, heightScale, road);
            placed += PlaceFauna(parent, grid, Stream(17), decor, heightScale, road);
            placed += PlaceShoreline(parent, grid, Stream(6), decor, occupied, heightScale,
                                     densityScale, road);

            // The water goes on last, over everything laid on its bed. Nothing claims
            // ground for it: reeds stand in the shallows and pads float on the surface,
            // and a sheet that reserved its tiles would have cleared both away.
            placed += PlaceWater(parent, grid, heightScale, waterMaterial, marshWaterMaterial);
            placed += PlaceFalls(parent, grid, Stream(13), decor, heightScale, waterMaterial);
            placed += PlaceCliffs(parent, grid, Stream(7), decor, occupied, heightScale, road, town);
            placed += PlaceTors(parent, grid, Stream(14), decor, occupied, heightScale, road);
            placed += PlaceWillows(parent, grid, Stream(8), decor, occupied, heightScale,
                                   densityScale, road);
            placed += PlaceCamps(parent, grid, Stream(9), decor, occupied, heightScale, campSites, road,
                                 found);

            placed -= SweepTheCourtyard(parent);
            placed -= SweepTheBridges(parent);
            placed -= SweepTheBones(parent, grid);

            Census(parent);
            Tallest(parent);

            _road = null;
            return placed;
        }

        /// <summary>
        /// Takes everything out of the castle's courtyard, last of all.
        ///
        /// <b>Last, because nothing else works.</b> Raise reserves the castle's footprint
        /// in <c>occupied</c> and the scatter checks it — but a tree is canopy, and canopy
        /// skips the ground check on purpose so that a wood can close over a track. So
        /// pines grew through the curtain wall and stood about the yard, and no amount of
        /// reserving ground before the fact would have stopped them. The only thing that
        /// answers every case at once is to look at what is standing there when the
        /// decorating is finished and take it away.
        ///
        /// The yard is left bare on purpose and that is not laziness — it is the enemy's
        /// castle, the player never goes in, and everything put in it so far has been
        /// wrong in a different way each time. Empty is the honest state to build from.
        /// </summary>
        static int SweepTheCourtyard(Transform parent)
        {
            if (_castle == null) return 0;

            var yard = ModelScaling.Measure(_castle);

            // The inside only. The wall's own pieces and its towers stand on the rim, so
            // the box is pulled in by a wall's thickness before anything is judged by it.
            yard.Expand(new Vector3(-CourtyardMargin * 2f, 0f, -CourtyardMargin * 2f));

            var doomed = new List<GameObject>();

            foreach (Transform thing in parent)
            {
                if (thing == _castle.transform) continue;

                var at = thing.position;
                if (at.x < yard.min.x || at.x > yard.max.x) continue;
                if (at.z < yard.min.z || at.z > yard.max.z) continue;

                doomed.Add(thing.gameObject);
            }

            foreach (var thing in doomed)
            {
                if (Application.isPlaying) Object.Destroy(thing);
                else Object.DestroyImmediate(thing);
            }

            _castle = null;
            return doomed.Count;
        }

        /// <summary>How far inside the castle's outline the courtyard begins, in metres.</summary>
        const float CourtyardMargin = 4f;

        /// <summary>The tiles a trap's bones went down on, for SweepTheBones.</summary>
        static readonly List<int> _boneSites = new List<int>();

        /// <summary>
        /// The broken wagons laid at those traps, which the sweep must not take.
        ///
        /// Held as the objects rather than tested by name or by height, because both of
        /// those have already failed here: the sweep takes anything over 1.5 m standing
        /// within three metres of a heap, and the wagon is 1.57 m tall and stands 3.2 m
        /// away. It was swept off all hundred and twenty-one traps the moment it was
        /// turned the right way up, and the photographs showed an empty field.
        /// </summary>
        static readonly List<GameObject> _trapWrecks = new List<GameObject>();

        /// <summary>
        /// How tall a thing standing over a heap of bones may be and still leave it seen,
        /// in metres.
        ///
        /// A metre and a half: grass, a stone, a stump stay; a bush, a boulder and every
        /// tree go. A skeleton is under half a metre high and the camera is forty-seven
        /// up, so anything taller than a person between them is a thing it hides behind.
        /// </summary>
        const float OverBones = 1.5f;

        /// <summary>
        /// Clears whatever would hide a trap's bones, last of all, for the reason the
        /// courtyard and the bridges are swept: canopy does not check the ground.
        ///
        /// A heap of bones under a spruce is a heap of bones nobody sees, and a warning
        /// nobody sees is no warning. The heap claims its tile when it goes down, but a
        /// tree is canopy and canopy stands where it likes so a wood can close over a
        /// track - so the ground round every heap is looked at when the decorating is
        /// done, and anything tall standing on it is taken away.
        ///
        /// Never a bridge. Traps are laid at the throats and a ford is a throat, so a
        /// heap can land beside one; the bridge is the thing that is meant to be there.
        ///
        /// And never the trap's own wagon, for the same reason: it was put beside the
        /// heap on purpose, by the code that put the heap down.
        /// </summary>
        static int SweepTheBones(Transform parent, TileGrid grid)
        {
            if (_boneSites.Count == 0) return 0;

            var doomed = new List<GameObject>();
            float reach = BonePileSpread + 0.5f;

            foreach (int site in _boneSites)
            {
                var at = Vec2.FromTile(grid, site);

                foreach (Transform thing in parent)
                {
                    if (thing.GetComponentInChildren<BridgeDeck>() != null) continue;
                    if (_trapWrecks.Contains(thing.gameObject)) continue;

                    // <b>And never the remains themselves.</b> This sweep clears what
                    // stands over a trap's bones so they can be seen, and it judges by
                    // height - which was safe while the bones were a skeleton lying at
                    // 0.41 m. The mountains mark their traps with the alpine pack's
                    // fossil, two and a half metres of spine curled in the stone, and the
                    // sweep took every one of them the moment they went down: the sign
                    // was tall enough to be something hiding itself. Bones do not hide
                    // bones.
                    if (IsBones(thing.gameObject)) continue;

                    // And never the water, for the reason the bridge sweep may not have
                    // it either: it is one mesh over every wet tile, so its box is the
                    // map, it is taller than the heap by the whole relief of the country,
                    // and it lies within reach of every trap there is. Both sweeps took
                    // it, on every level. See WaterSheet.
                    if (thing.GetComponent<WaterSheet>() != null) continue;

                    // Never the castle. It is forty metres of stone standing beside the
                    // goal, and a trap near its wall once took the whole of it down on
                    // 3-10. Its site keeps it off the roads now (Strongholds.Site), and
                    // the traps are on the roads - this is so it can never happen again.
                    if (thing.gameObject == _castle || thing.name == "Castle") continue;

                    var box = ModelScaling.Measure(thing.gameObject);
                    if (box.size.y < OverBones) continue;

                    if (box.max.x < at.X - reach || box.min.x > at.X + reach) continue;
                    if (box.max.z < at.Y - reach || box.min.z > at.Y + reach) continue;

                    if (!doomed.Contains(thing.gameObject)) doomed.Add(thing.gameObject);
                }
            }

            foreach (var thing in doomed)
            {
                if (Application.isPlaying) Object.Destroy(thing);
                else Object.DestroyImmediate(thing);
            }

            _boneSites.Clear();
            _trapWrecks.Clear();
            return doomed.Count;
        }

        /// <summary>
        /// How far in from a bridge's outline the roadway is taken to begin, in metres.
        ///
        /// A metre. The deck is the middle of the model and the outline includes its
        /// railings and its feet, so pulling in a little keeps a boulder that the bank
        /// happens to put against an abutment and takes the things that are actually on
        /// the planking.
        /// </summary>
        const float BridgeMargin = 1f;

        /// <summary>
        /// Takes everything standing on a bridge off it, for the reason the courtyard is
        /// swept: canopy does not check the ground.
        ///
        /// <b>Reported from a playtest of 1-10 as the bridge being in the wrong place.</b>
        /// It is not - measured across all thirty levels of the chapters that exist, every
        /// deck sits on its ford within a tenth of a metre. What was wrong is that a
        /// spruce was growing through the middle of it. A bridge with a tree in it does not
        /// read as a bridge with a tree in it; it reads as a bridge somebody dropped on the
        /// grass, and that is what it was called.
        ///
        /// Same cause as the pines in the castle yard, same cure. Bridge reserves its
        /// ground in <c>occupied</c> and the scatter honours it, but a tree is canopy and
        /// canopy skips the ground check on purpose so a wood can close over a track.
        /// Nothing done before the fact answers that. Looking at what is standing there
        /// when the decorating has finished does.
        ///
        /// <b>By what the thing covers, not by where its pivot is.</b> The first try
        /// asked whether a prop's position fell inside the deck's box, which is the test
        /// the courtyard uses and is wrong here: the spruce on 1-10 has its trunk a metre
        /// off the downstream rail and its crown right across the planking. Canopy closing
        /// over a track is the thing trees are allowed to do, and a bridge is the one
        /// track it must not close over - you cannot see the caravan on it.
        ///
        /// So the two outlines are compared. A trunk on the bank whose branches do not
        /// reach the deck stays, which is the point of a bridge in a wood: the wood comes
        /// down to both banks.
        /// </summary>
        static int SweepTheBridges(Transform parent)
        {
            var decks = parent.GetComponentsInChildren<BridgeDeck>(true);
            if (decks.Length == 0) return 0;

            var doomed = new List<GameObject>();

            foreach (var deck in decks)
            {
                var span = ModelScaling.Measure(deck.gameObject);
                span.Expand(new Vector3(-BridgeMargin * 2f, 0f, -BridgeMargin * 2f));

                foreach (Transform thing in parent)
                {
                    if (thing == deck.transform) continue;
                    if (thing.IsChildOf(deck.transform)) continue;

                    // Except a trap's bones. Traps go to the throats and a ford is a
                    // throat, so a heap can fall on the planking; a skeleton on a bridge
                    // is a warning about the bridge, which is what it is there to be.
                    if (IsBones(thing.gameObject)) continue;

                    // And except the water, which is what the bridge is crossing. The
                    // sheet covers every wet tile on the map, so it overlaps every deck
                    // by construction and was swept off every level in the game. See
                    // WaterSheet.
                    if (thing.GetComponent<WaterSheet>() != null) continue;

                    // And except a trap's wagon, for the reason its bones are spared two
                    // lines up: the two together are the warning. A trap beside a crossing
                    // is the one whose warning matters most - a ford is a throat and that
                    // is where traps go - and the sweep was taking the wagon and leaving a
                    // skull, so chapter five's opening trap was marked by one skull half
                    // on the bridge ramp and nothing else. Photographed, and it read as
                    // nothing at all.
                    if (_trapWrecks.Contains(thing.gameObject)) continue;

                    var box = ModelScaling.Measure(thing.gameObject);

                    // Flattened, because the question is what stands over the roadway and
                    // not what passes above or below it.
                    if (box.max.x < span.min.x || box.min.x > span.max.x) continue;
                    if (box.max.z < span.min.z || box.min.z > span.max.z) continue;

                    if (!doomed.Contains(thing.gameObject)) doomed.Add(thing.gameObject);
                }
            }

            foreach (var thing in doomed)
            {
                if (Application.isPlaying) Object.Destroy(thing);
                else Object.DestroyImmediate(thing);
            }

            return doomed.Count;
        }

        /// <summary>
        /// Says what is actually standing on the map, biggest population first.
        ///
        /// Written after a fourth round of "what are those green things?" answered by
        /// guessing. Three of the guesses were wrong, and each cost a build, a run and a
        /// screenshot to find out. What is on the ground is a fact the decorator knows
        /// the moment it finishes, and the only reason it was ever a question is that
        /// nobody had asked it to say.
        ///
        /// By prefab rather than by set, because the answer wanted is "that shape", and a
        /// shape has a name. Clone suffixes are trimmed so the counts add up.
        /// </summary>
        /// <summary>
        /// Measures everything big that was actually built, and says so.
        ///
        /// <b>Written because guessing at sizes from a screenshot has been wrong three
        /// times.</b> Nothing outside Unity can see these models — every FBX in this
        /// repository is a Git LFS pointer — so a constant can say a tower is eleven
        /// metres while the thing on screen is a slab, and the only way to tell has been
        /// to argue about a picture.
        ///
        /// Height *and* width, because the two faults so far were one of each: a tower
        /// that was not too tall but far too broad, and a tent that reads as tiny for a
        /// reason the arithmetic does not predict. Measured after fitting, so this is what
        /// the player sees rather than what was asked for.
        ///
        /// Only what stands over MeasuredFrom, so the line is readable: nobody has ever
        /// complained about a pebble.
        /// </summary>
        static void Tallest(Transform parent)
        {
            var biggest = new Dictionary<string, Vector2>();

            foreach (Transform child in parent)
            {
                var bounds = ModelScaling.Measure(child.gameObject);
                float high = bounds.size.y;
                if (high < MeasuredFrom) continue;

                string name = child.name;
                int clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
                if (clone >= 0) name = name.Substring(0, clone);

                float wide = Mathf.Max(bounds.size.x, bounds.size.z);

                // The largest of each shape, because the complaint is always about the
                // one that stands out, never about the median.
                if (!biggest.TryGetValue(name, out var seen) || high > seen.x)
                    biggest[name] = new Vector2(high, wide);
            }

            if (biggest.Count == 0) return;

            var ranked = new List<KeyValuePair<string, Vector2>>(biggest);
            ranked.Sort((a, b) => b.Value.x.CompareTo(a.Value.x));

            var lines = new List<string>();
            for (int i = 0; i < ranked.Count && i < 14; i++)
                lines.Add($"{ranked[i].Key} {ranked[i].Value.x:0.0}x{ranked[i].Value.y:0.0} m");

            Debug.Log($"[The Veil] Tallest built (height x width), against a wagon at "
                      + $"{VisualLibrary.WagonHeight:0.0} m: {string.Join(", ", lines)}");
        }

        /// <summary>How tall a thing has to be to be worth reporting, in metres.</summary>
        const float MeasuredFrom = 3f;

        static void Census(Transform parent)
        {
            var counts = new Dictionary<string, int>();

            foreach (Transform child in parent)
            {
                string name = child.name;

                int clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
                if (clone >= 0) name = name.Substring(0, clone);

                counts.TryGetValue(name, out int seen);
                counts[name] = seen + 1;
            }

            if (counts.Count == 0) return;

            var ranked = new List<KeyValuePair<string, int>>(counts);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));

            var top = new List<string>();
            for (int i = 0; i < ranked.Count && i < 12; i++)
                top.Add($"{ranked[i].Key} x{ranked[i].Value}");

            Debug.Log($"[The Veil] On the ground: {string.Join(", ", top)}"
                      + (ranked.Count > 12 ? $", and {ranked.Count - 12} other kind(s)." : "."));
        }

        /// <summary>
        /// Lays the water.
        ///
        /// One mesh over every wet tile — see <see cref="WaterMeshBuilder"/>, which also
        /// records why the plane-per-tile version this replaces could only ever look
        /// like blue plates lying on the grass.
        ///
        /// <c>decor.Water</c> is no longer read. A water prefab is a flat square with a
        /// shader from another pipeline on it, and neither half of that survived contact
        /// with this map.
        ///
        /// <paramref name="waterMaterial"/> is the one thing about the water a scene gets
        /// to decide, and it is there so a bought or downloaded water package can be tried
        /// without a code change — the mesh already carries the depth such a package wants
        /// to read. Null keeps the project's own shader. See WaterMeshBuilder.Material.
        /// </summary>
        static int PlaceWater(Transform parent, TileGrid grid, float heightScale,
                              Material waterMaterial, Material marshWaterMaterial)
        {
            var mesh = WaterMeshBuilder.Build(grid, TileGrid.TileSize, heightScale);
            if (mesh == null) return 0;

            var surface = new GameObject("Water");
            surface.transform.SetParent(parent, false);

            // Water, and not something standing in the road. See WaterSheet: without this
            // the bridge sweep took the river off every level that had a bridge, which is
            // every level, and what was left was the blue the ground is painted.
            surface.AddComponent<WaterSheet>();

            // Terrain, not scenery. The planning fog paints every prop flat grey and
            // files it under the tile its transform sits on — which for one mesh covering
            // the whole map is tile zero, so every river on the plan would go grey
            // together until the corner of the map was revealed. A river is the thing a
            // route is planned around; it is read from the map, like the ground it cuts.
            surface.AddComponent<Signal>();
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WaterMeshBuilder.Material(waterMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return 1 + PlacePools(parent, grid, heightScale, marshWaterMaterial);
        }

        /// <summary>
        /// Lays the standing water in the marshes, where there is any.
        ///
        /// <b>Pools, and never a sheet.</b> Marsh is a tenth of the map and it arrives in
        /// a few large patches — the biggest measured is 186 tiles — and it is *passable*:
        /// the caravan drives through a fen at rather more than twice the cost of open
        /// plains, which is the whole reason the terrain exists. Flooding it would draw an
        /// inland sea across ground the route is meant to cross, and a player reads blue
        /// as a thing to go round. So only the hollows fill, which is also simply what
        /// water does. See WaterMeshBuilder.PoolDrop.
        ///
        /// Nothing here touches the simulation. The tiles stay Marsh, they stay passable
        /// and they cost exactly what they cost — this is paint on ground that was already
        /// wet.
        ///
        /// Its own object rather than another material on the river's, because the two
        /// meshes are separate: a pool must not share a corner with a bank, or it would
        /// drag the river's surface down to its own level.
        /// </summary>
        static int PlacePools(Transform parent, TileGrid grid, float heightScale,
                              Material marshWaterMaterial)
        {
            var mesh = WaterMeshBuilder.Pools(grid, TileGrid.TileSize, heightScale);
            if (mesh == null) return 0;

            var pools = new GameObject("Marsh water");
            pools.transform.SetParent(parent, false);

            // Terrain rather than scenery, for the reason the river is — one mesh over the
            // whole map would otherwise be filed under tile zero and fog as a single prop.
            pools.AddComponent<Signal>();
            pools.AddComponent<WaterSheet>();
            pools.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = pools.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WaterMeshBuilder.PoolMaterial(marshWaterMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return 1;
        }

        /// <summary>
        /// Puts a crossing on the ford tiles, one per crossing rather than one per tile.
        ///
        /// A ford is a terrain type the route planner treats as a chokepoint — every
        /// corridor tends to use the same one, which is why the traps go there — and it
        /// has never had anything on it. What the player sees is water that is somehow
        /// passable, with nothing to say why. A plank bridge says it.
        /// </summary>
        /// <summary>
        /// Falling water where the river drops from one tile to the next.
        ///
        /// <b>Measured before it was built.</b> The rivers of the fifty built levels fall at
        /// most 2.6 m between neighbouring tiles, and only twelve levels have a step of two
        /// metres at all - so there is no cliff in this country for a river to come over,
        /// and a waterfall like the pack's own picture cannot be found by looking. What
        /// there is, is the step: water dropping a man's height over a few metres, which is
        /// a fall you hear before you see. The sheet is stood in the step and scaled to it,
        /// the spray at its foot, and the biggest few on a level are taken so that a river
        /// does not become a staircase.
        /// </summary>
        static int PlaceFalls(Transform parent, TileGrid grid, DeterministicRandom rng,
                              BiomeDecor decor, float heightScale, Material waterMaterial)
        {
            // Either is enough: the rock alone makes the step, and the water that comes
            // over it is the river's own surface, which already falls with the ground.
            if (!decor.Falls.Any && !decor.Cliffs.Any) return 0;

            var steps = new List<(float Drop, int From, int To)>();

            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    int tile = grid.ToIndex(x, y);
                    if (!IsWet(grid[tile])) continue;

                    foreach (var (dx, dy) in Steps)
                    {
                        if (!grid.InBounds(x + dx, y + dy)) continue;

                        int next = grid.ToIndex(x + dx, y + dy);
                        if (!IsWet(grid[next])) continue;

                        float drop = (grid.Elevation(tile) - grid.Elevation(next)) * heightScale;
                        if (drop >= FallLeast) steps.Add((drop, tile, next));
                    }
                }

            steps.Sort((a, b) => b.Drop.CompareTo(a.Drop));

            int placed = 0;
            var taken = new HashSet<int>();

            foreach (var (drop, from, to) in steps)
            {
                if (placed >= MostFalls) break;
                if (!Apart(grid, from, taken, FallsApart)) continue;

                grid.ToCoords(from, out int fx, out int fy);
                grid.ToCoords(to, out int tx, out int ty);

                // In the step itself: halfway between the two tiles, standing on the lower
                // one's water and reaching up to the higher one's.
                var above = Vec2.FromTile(grid, from);
                var below = Vec2.FromTile(grid, to);

                float x = (above.X + below.X) * 0.5f;
                float z = (above.Y + below.Y) * 0.5f;
                float foot = grid.Elevation(to) * heightScale;

                // Out over the brink rather than on it: the sheet hangs off the lip, so the
                // shelf's own water runs up to its head and the pool takes its foot.
                float outward = TileGrid.TileSize * 0.5f;
                x += System.Math.Sign(tx - fx) * outward;
                z += System.Math.Sign(ty - fy) * outward;

                // <b>Hung by its own origin, which is the one thing it was never given.</b>
                // Measured at last: the model runs from y 0 down to y -10, so its pivot is
                // the brink itself and the water hangs below it; and it lies to one side of
                // that pivot, from x -3.75 to 0, so centring it on the channel means moving
                // it half its width back. Placed by its foot, or by its middle, or scaled to
                // the drop and seated on the pool, it ended up flat on the shelf or inside
                // the rock - four attempts, all of them guessing at where its nought was.
                if (decor.Whitewater.Any)
                {
                    var spray = Object.Instantiate(Any(decor.Whitewater, rng), parent);
                    spray.transform.position = new Vector3(x, foot, z);
                }

                // <b>The rock the fall is cut through.</b> A row of stones along the brink
                // was a lip and nothing more; what the reference country has is a mass of
                // rock with the river sawing through the middle of it - walls either side of
                // the water, standing above the shelf and going down to the pool below.
                //
                // So the rock is laid as a block round the step rather than as a line along
                // it: a few tiles either way, denser and taller against the channel, and the
                // wet tiles left open for the water to come through.
                if (decor.Cliffs.Any)
                    for (int ahead = -MassifAlong; ahead <= 0; ahead++)
                        for (int side = -MassifAcross; side <= MassifAcross; side++)
                        {
                            if (side > -CliffGap && side < CliffGap) continue;

                            bool downstream = fy == ty;

                            int cx = fx + (downstream ? ahead : side);
                            int cy = fy + (downstream ? side : ahead);

                            if (!grid.InBounds(cx, cy)) continue;

                            int beside = grid.ToIndex(cx, cy);
                            if (IsWet(grid[beside])) continue;

                            // Thinning outwards, so the mass has a shape rather than an edge.
                            int away = (side < 0 ? -side : side) + (ahead < 0 ? -ahead : ahead);
                            if (away > CliffGap && !rng.Chance(1f - (away - CliffGap) * MassifThins)) continue;

                            var stood = Vec2.FromTile(grid, beside);

                            var rock = Object.Instantiate(Any(decor.Cliffs, rng), parent);
                            rock.transform.rotation = Quaternion.Euler(decor.Cliffs.ZUp ? -90f : 0f,
                                                                       rng.Range(0f, 360f), 0f);

                            // Standing in the step, not buried in the bank: foot in the water
                            // below, head above the brink. Set the other way round - the top
                            // at the height of the ground it stands on - every piece of it was
                            // underground and the water came over a grass edge.
                            float rise = drop + CliffLip + rng.Range(0f, MassifCrown);

                            var face = ModelScaling.Measure(rock);
                            if (face.size.y > 0.01f) rock.transform.localScale *= rise / face.size.y;

                            face = ModelScaling.Measure(rock);
                            rock.transform.position += new Vector3(stood.X - face.center.x,
                                                                    foot - face.min.y,
                                                                    stood.Y - face.center.z);

                            // And taken down again where the ground it stands on is higher
                            // than the rock is tall: the shelf tapers, so a piece on its
                            // shoulder can be buried whole. The smoke test counts those, and
                            // counted three.
                            float ground = grid.SurfaceElevation(stood.X, stood.Y) * heightScale;
                            face = ModelScaling.Measure(rock);

                            if (face.max.y < ground + CliffShows) Unbuild(rock);

                            // And bedded where the ground under it is higher than the pool it
                            // was stood in, so no piece of the fall's rock hangs off a slope.
                            else if (face.min.y > ground)
                                rock.transform.position += new Vector3(0f, ground - face.min.y - CliffShows, 0f);

                            // <b>And solid, which it never was.</b> Every other mass of rock
                            // on the map is marked - the scatter's boulders, the cliffs, the
                            // tors - and this one was instantiated by hand and the marking
                            // forgotten, so the walls of the gorge were rock the escort walked
                            // straight through. Measured across twelve levels: three hundred
                            // and twenty-one props standing with nothing solid about them, and
                            // the largest of them were these, twenty metres across and
                            // thirteen tall. It is the rock lying past the bridge that a
                            // playtest reported the column marching through.
                            Block(rock, canopy: false);
                            if (Barring(grid, _road, rock)) Unbuild(rock);
                        }

                // <b>And the mountain the water comes out of.</b> A lip of rock either side
                // is a step in a field; what the reference country has is a mass of stone
                // with the fall coming down the middle of it. So in a country built of rock,
                // a tor is raised on each bank, two tiles clear of the channel.
                float crowns = 0f;
                int crowned = 0;

                if (decor.RockPasses)
                    foreach (int side in new[] { -1, 1 })
                    {
                        int mx = fx + (fy == ty ? 0 : side * TorFromFall);
                        int my = fy + (fy == ty ? side * TorFromFall : 0);

                        if (!grid.InBounds(mx, my)) continue;

                        int at = grid.ToIndex(mx, my);
                        if (IsWet(grid[at])) continue;

                        placed += Tor(parent, grid, rng, decor, at, heightScale, new HashSet<int>(),
                                      out float crown);

                        crowns += crown;
                        crowned++;
                    }

                // <b>The water comes over the top of the rock, not out from under it.</b>
                // Hung at the brink - the height of the shelf's own water - the fall stood at
                // the foot of a twenty-five metre mass of stone and could not be seen at all.
                // What the reference country has is water coming off the crown of the rock
                // and down its face into the pool, so the sheet hangs from the rock's own top
                // and is cut long enough to reach the water below it.
                if (decor.Falls.Any)
                {
                    var sheet = Object.Instantiate(Any(decor.Falls, rng), parent);

                    float brink = grid.Elevation(from) * heightScale;
                    float head = crowned > 0 ? crowns / crowned - FallBelowCrown : brink;
                    float span = TileGrid.TileSize * Channel(grid, from, tx - fx, ty - fy);

                    sheet.transform.rotation =
                        Quaternion.Euler(0f, Mathf.Atan2(tx - fx, ty - fy) * Mathf.Rad2Deg, 0f);

                    // Its pivot is its own head and it hangs ten metres below that (measured,
                    // FallModelTall), and it lies to one side of that pivot - so it is moved
                    // half its width to bring the water down the middle of the channel.
                    sheet.transform.localScale = new Vector3(span / FallModelWide,
                                                             (head - foot + FallRaise) / FallModelTall,
                                                             1f);

                    sheet.transform.position = new Vector3(x, head, z)
                                               + sheet.transform.right * (span * 0.5f);

                    var falling = sheet.GetComponentInChildren<MeshRenderer>();
                    if (falling != null)
                        falling.sharedMaterial = WaterMeshBuilder.Material(waterMaterial);
                }

                taken.Add(from);
                placed++;
            }

            return placed;
        }

        /// <summary>
        /// One face of falling water, standing in the step between two wet tiles.
        ///
        /// Two quads back to back, because a quad is one-sided and a waterfall is seen from
        /// both banks. Slightly proud of the step on each side, so the river's surface above
        /// and the pool below meet it rather than clip through it.
        /// </summary>
        static void Falling(Transform parent, TileGrid grid, int from, int to, float drop,
                            float heightScale, Material waterMaterial)
        {
            if (waterMaterial == null) return;

            grid.ToCoords(from, out int fx, out int fy);
            grid.ToCoords(to, out int tx, out int ty);

            var above = Vec2.FromTile(grid, from);
            var below = Vec2.FromTile(grid, to);

            float x = (above.X + below.X) * 0.5f;
            float z = (above.Y + below.Y) * 0.5f;
            float head = grid.Elevation(from) * heightScale;

            // How wide the water is here, so the sheet spans the channel and no more.
            float wide = TileGrid.TileSize * Channel(grid, from, tx - fx, ty - fy);

            for (int side = 0; side < 2; side++)
            {
                var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                face.name = "Waterfall";
                Object.DestroyImmediate(face.GetComponent<Collider>());

                face.transform.SetParent(parent, false);
                face.transform.position = new Vector3(x, head - drop * 0.5f + FallRaise, z);

                float turn = Mathf.Atan2(tx - fx, ty - fy) * Mathf.Rad2Deg + side * 180f;
                face.transform.rotation = Quaternion.Euler(0f, turn, 0f);
                face.transform.localScale = new Vector3(wide, drop + FallRaise * 2f, 1f);

                var renderer = face.GetComponent<MeshRenderer>();
                // <b>White water, not river water.</b> The river's material is a caustic
                // pattern sized for a broad surface seen from above; on a six-metre vertical
                // face it came out as a lattice of green stones. What falls over a lip is
                // white and half transparent, and that is what this is.
                renderer.sharedMaterial = FallingWater();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>How many tiles of water lie across the step, measured from one of them.</summary>
        static int Channel(TileGrid grid, int tile, int alongX, int alongY)
        {
            grid.ToCoords(tile, out int x, out int y);

            // Across the flow: if the water runs down the rows, the channel runs along them.
            int stepX = alongY != 0 ? 1 : 0;
            int stepY = alongX != 0 ? 1 : 0;

            int wide = 1;

            for (int way = -1; way <= 1; way += 2)
                for (int step = 1; step <= 6; step++)
                {
                    int nx = x + stepX * step * way, ny = y + stepY * step * way;
                    if (!grid.InBounds(nx, ny) || !IsWet(grid[nx, ny])) break;

                    wide++;
                }

            return wide;
        }

        /// <summary>How far the sheet stands proud of the water above and below it, in metres.</summary>
        const float FallRaise = 0.5f;

        /// <summary>How wide the pack's fall is drawn, in metres. Measured, not guessed.</summary>
        const float FallModelWide = 3.75f;

        /// <summary>And how far it hangs below its own pivot.</summary>
        const float FallModelTall = 10f;

        /// <summary>How far under the rock's crown the water comes over it, in metres.</summary>
        const float FallBelowCrown = 3f;

        static Material _fallingWater;

        /// <summary>The white, part-transparent water of a fall. Built once and shared.</summary>
        static Material FallingWater()
        {
            if (_fallingWater != null) return _fallingWater;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            _fallingWater = new Material(shader) { name = "Falling water" };
            _fallingWater.SetFloat("_Surface", 1f);
            _fallingWater.SetFloat("_Blend", 0f);
            _fallingWater.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _fallingWater.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _fallingWater.SetFloat("_ZWrite", 0f);
            _fallingWater.SetFloat("_Smoothness", 0.7f);
            _fallingWater.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _fallingWater.SetColor("_BaseColor", new Color(0.92f, 0.96f, 0.98f, 0.82f));
            _fallingWater.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return _fallingWater;
        }

        static bool IsWet(TerrainType terrain)
            => terrain == TerrainType.Water || terrain == TerrainType.Ford;

        static readonly (int X, int Y)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>How far a river must drop between two tiles to be worth a sheet of water, in metres.</summary>
        public const float FallLeast = 1.2f;

        /// <summary>How much longer than the drop the sheet is cut, so it sinks into both waters.</summary>
        const float FallSpare = 0.8f;

        /// <summary>How many falls a level may have, largest first.</summary>
        const int MostFalls = 3;

        /// <summary>How far apart two falls must stand, in tiles.</summary>
        const int FallsApart = 6;

        /// <summary>How much of a road tile's length carries a patch of bare ground.</summary>
        const float TrackPatch = 0.4f;

        /// <summary>How far the rock mass reaches across the river, in tiles either side.</summary>
        const int MassifAcross = 5;

        /// <summary>How far it reaches up and down the river, in tiles.</summary>
        const int MassifAlong = 2;

        /// <summary>How fast it thins out from the channel: a fifth of the pieces per tile.</summary>
        const float MassifThins = 0.22f;

        /// <summary>How much a piece may stand above the brink, in metres.</summary>
        const float MassifCrown = 3.5f;

        /// <summary>How wide the gap in the rock is, in tiles either side of the water.</summary>
        const int CliffGap = 2;

        /// <summary>How far the lip stands above the water it drops from, in metres.</summary>
        const float CliffLip = 0.8f;

        /// <summary>How far a piece of the rock must show above its ground to be worth keeping.</summary>
        const float CliffShows = 0.4f;

        /// <summary>How far a cliff piece is bedded into the ground, as a share of its height.</summary>
        const float CliffBed = 0.12f;

        /// <summary>How much of a walled country's passes carries a rock face.</summary>
        // Three pass tiles in four. Photographed over the whole chapter, two in five left
        // half the levels reading as stony meadow with pines on it - the roads have to
        // thread between rock for the country to be what it is called.
        const float PassRock = 0.75f;

        /// <summary>Whether a tile is clear of everything already taken, by this many tiles.</summary>
        static bool Apart(TileGrid grid, int tile, HashSet<int> taken, int tiles)
        {
            grid.ToCoords(tile, out int x, out int y);

            foreach (int other in taken)
            {
                grid.ToCoords(other, out int ox, out int oy);
                if (Mathf.Abs(ox - x) <= tiles && Mathf.Abs(oy - y) <= tiles) return false;
            }

            return true;
        }

        static int PlaceFords(Transform parent, TileGrid grid, int seed, DeterministicRandom rng,
                              BiomeDecor decor, HashSet<int> occupied, float heightScale,
                              List<Landmark> found, IReadOnlyCollection<int> travelled)
        {
            int bridged = BridgeTile(grid, seed, travelled);
            if (bridged < 0) return 0;

            int placed = 0;

            // Its own stream for the model, for the reason BridgeTile gives for the tile.
            if (decor.Fords.Any &&
                Bridge(parent, grid, new DeterministicRandom(seed ^ BridgeModelSalt), decor, bridged,
                       heightScale, occupied, travelled))
            {
                placed++;

                // Written down, so the planning map can put a sign on it. A level has
                // three crossings and one of them has this; the map showed the pale water
                // and the stones at all three and never said which, so the line was drawn
                // blind and the answer arrived in the run.
                Landmark.Note(found, LandmarkKind.Bridge, bridged);
            }

            // And every other crossing is what a ford actually is: stones in shallow
            // water. The gravel bar is already level with the banks — LevelTheCrossings
            // raises it — so the sheet runs thin over it and the bed shows through. What
            // was missing is the reason it reads as a place to cross rather than as
            // river that happens to be paler.
            placed += PlaceSteppingStones(parent, grid, rng, decor, occupied, heightScale);

            return placed;
        }

        /// <summary>
        /// The ford the level's one bridge stands on, or -1 for a level with no ford.
        ///
        /// One bridge on the level, and the dice choose which crossing gets it. Every
        /// crossing used to get one, which made three bridges a level and a built structure
        /// the ordinary case. A bridge is somebody's work: it should be the exception, and
        /// where it happens to stand is worth something in itself — sometimes the crossing
        /// the enemies are watching, sometimes one nobody has reason to go near.
        ///
        /// <b>Asked of the terrain and the seed and nothing else.</b> It used to be drawn
        /// from the stream the whole decoration shares, among crossings that other props
        /// had not already covered — and the planning map and the run lay different
        /// things before the fords, so each put the bridge on a different crossing. The
        /// player drew their route over a bridge that was somewhere else in the game. A
        /// function of the map alone cannot disagree with itself.
        /// </summary>
        public static int BridgeTile(TileGrid grid, int seed,
                                    IReadOnlyCollection<int> travelled = null)
        {
            // Crossings with banks, counted the same way the generator counts them when it
            // decides whether a map may ship — see TheVeil.Sim.Crossings. A bridge is a
            // thing between two banks, and dropped on a ford that a lake has grown over it
            // stands in open water with its ends in the air, which is what 3-1 did.
            var crossings = Crossings.All(grid);

            // <b>And no bridge at all where nothing has banks.</b> This used to fall back
            // to any ford it could find, on the reasoning that a bridge in an awkward
            // place still says "cross here" and that a level reaching that line had
            // already failed the generator's own check.
            //
            // Both halves were wrong. 1-8 is a town, it owes no crossings, it has failed
            // nothing - and its water has no ford with ground on two sides, so the
            // fallback stood a bridge in the middle of open water with its ends in the
            // air. That is the same fault the banks test was written for, arriving by the
            // door beside it, and the smoke test has been reporting it every run.
            //
            // A level that owes crossings and has none is caught where it belongs: the
            // generator's crossing count, and the smoke test's "only N ways over the
            // water". Neither needs a bridge planted in a lake to say so.
            if (crossings.Count == 0) return -1;

            // <b>One that somebody actually crosses.</b> This drew at random from every
            // crossing on the map and never looked at where the roads go, so a level's one
            // bridge could stand on the ford none of them use: measured over the thirty
            // levels of the chapters that exist, six of them did, and most of the rest
            // served one road out of three.
            //
            // A level owes three ways over its water and builds one bridge, so two roads
            // ford and one gets planking - that is the design. Which one gets it should
            // not be the one nobody takes.
            //
            // Narrowed rather than forced: where no crossing is on a road the whole list
            // stands, because a bridge in an awkward place still says "cross here" and a
            // level that has got this far has stranger problems than that.
            if (travelled != null && travelled.Count > 0)
            {
                var used = new List<int>();

                foreach (int crossing in crossings)
                    if (Walked(grid, crossing, travelled)) used.Add(crossing);

                if (used.Count > 0) crossings = used;
            }

            // <b>And one a road can be driven straight onto.</b> A ford at the edge of a lake,
            // or squeezed between two rivers, has no dry ground along its row to line the
            // column up on (Crossings.Square), so the road turns onto it at a right angle -
            // on 3-7 the bridge stood on exactly such a ford, and the wagons swung onto the
            // planks sideways. Narrowed, not forced, for the same reason as above.
            var straight = new List<int>();
            foreach (int crossing in crossings)
                if (Banked(grid, crossing)) straight.Add(crossing);
            if (straight.Count > 0) crossings = straight;

            return crossings[new DeterministicRandom(seed ^ BridgeTileSalt).Range(0, crossings.Count)];
        }

        /// <summary>
        /// Whether a crossing has dry ground along its own row on both banks, enough for
        /// Crossings.Square to lay a straight run-up onto it.
        /// </summary>
        static bool Banked(TileGrid grid, int crossing)
        {
            grid.ToCoords(crossing, out int x, out int y);

            int west = x, east = x;
            while (grid.InBounds(west - 1, y) && grid[grid.ToIndex(west - 1, y)] == TerrainType.Ford) west--;
            while (grid.InBounds(east + 1, y) && grid[grid.ToIndex(east + 1, y)] == TerrainType.Ford) east++;

            return DryRun(grid, west, y, -1) >= Crossings.LeastRunUp
                && DryRun(grid, east, y, 1) >= Crossings.LeastRunUp;
        }

        static int DryRun(TileGrid grid, int x, int y, int dir)
        {
            int count = 0;
            for (int step = 1; step <= Crossings.LeastRunUp; step++)
            {
                int at = x + dir * step;
                if (!grid.InBounds(at, y) || !grid.IsPassable(at, y)) break;
                if (grid[grid.ToIndex(at, y)] == TerrainType.Ford) break;
                count++;
            }
            return count;
        }

        /// <summary>Whether a drawn road passes within a bridge's length of this tile.</summary>
        static bool Walked(TileGrid grid, int tile, IReadOnlyCollection<int> travelled)
        {
            grid.ToCoords(tile, out int x, out int y);

            foreach (int walked in travelled)
            {
                grid.ToCoords(walked, out int wx, out int wy);

                int dx = wx - x, dy = wy - y;
                if (dx * dx + dy * dy <= BridgeReachTiles * BridgeReachTiles) return true;
            }

            return false;
        }

        /// <summary>How near a road must pass a crossing to be said to use it, in tiles.</summary>
        // Two. A ford is several tiles wide and a route takes one row of it, so a road
        // using the crossing beside the one it is drawn through is still using it.
        const int BridgeReachTiles = 2;

        const int BridgeTileSalt = 0x0B21D6E;
        const int BridgeModelSalt = 0x0B21D6F;

        /// <summary>How wide a stepping stone is, in metres.</summary>
        // Knee height on a wagon's wheel. Big enough to break the water, small enough
        // that a line of them reads as a crossing rather than as a dam.
        public const float SteppingStoneSize = 1.1f;

        /// <summary>
        /// Marks the crossings that have no bridge with stone.
        ///
        /// A ford is passable water and it has never looked like anything: the same blue
        /// as the river, a little paler because the bar under it is higher. A player
        /// looking for somewhere to cross had nothing to look *at*.
        ///
        /// Stones, from the shore set the waterline already uses, so the pack's own
        /// river-worn piles do the work rather than generic scatter. Laid on the ford
        /// tiles themselves, which is where somebody putting them there would have laid
        /// them.
        /// </summary>
        static int PlaceSteppingStones(Transform parent, TileGrid grid, DeterministicRandom rng,
                                       BiomeDecor decor, HashSet<int> occupied, float heightScale)
        {
            var stones = decor.Shore.Any ? decor.Shore : decor.Rocks;
            if (!stones.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount; i++)
            {
                if (grid[i] != TerrainType.Ford) continue;

                // The bridge claimed its own tiles on the way in, so this cannot strew
                // stones across a roadway.
                if (occupied.Contains(i)) continue;

                // Four to seven, up from two to four. A ford is crossed on stones and two
                // of them is a pair of rocks in a river; what says "you can walk here" is
                // a line of them, and they are 1.1 m across on a four-metre tile.
                int pile = 4 + rng.Range(0, 4);

                for (int s = 0; s < pile; s++)
                {
                    var choice = new Choice(stones, Any(stones, rng), SteppingStoneSize,
                                            byWidth: true);

                    // With the occupied set, so a stone is not dropped on something that
                    // is already there. It was called without it, which is how one came
                    // down on a tent.
                    if (Scatter(parent, grid, rng, choice, i, heightScale, spread: 1.5f, occupied))
                        placed++;
                }
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
        /// <summary>
        /// The mountains themselves: masses of rock built up out of the pack's pieces,
        /// standing in the country rather than on its skyline.
        ///
        /// <b>Because no pack has a mountain that a caravan can drive past.</b> The skyline
        /// peaks are three hundred metres out and scaled to be looked at, not walked under;
        /// inside the map the tallest stone was a five-metre face. A mountain level made of
        /// those reads as a stony field. So a tor is assembled: a ring of the biggest rock
        /// the packs have, each piece scaled between two and five times, stacked in two or
        /// three courses with the upper ones narrower, and a cap on top. Twenty to thirty
        /// metres of it, which from the road is a thing you go round.
        ///
        /// On the high ground, away from the roads, and only in a country that asks for it
        /// (BiomeDecor.RockPasses) - everywhere else the country is not made of this.
        /// </summary>
        static int PlaceTors(Transform parent, TileGrid grid, DeterministicRandom rng,
                             BiomeDecor decor, HashSet<int> occupied, float heightScale,
                             HashSet<int> road)
        {
            if (!decor.RockPasses || !decor.Cliffs.Any) return 0;

            var stood = new List<int>();
            int placed = 0;

            for (int i = 0; i < grid.TileCount && stood.Count < MostTors; i++)
            {
                if (grid[i] != TerrainType.MountainPass) continue;
                if (road != null && road.Contains(i)) continue;
                if (occupied.Contains(i)) continue;
                if (!rng.Chance(TorChance)) continue;
                if (!Apart(grid, i, stood, TorsApart)) continue;

                grid.ToCoords(i, out int x, out int y);
                if (NearWater(grid, x, y, 3)) continue;

                stood.Add(i);
                placed += Tor(parent, grid, rng, decor, i, heightScale, occupied);
            }

            return placed;
        }

        /// <summary>One mass of rock, built up in courses.</summary>
        static int Tor(Transform parent, TileGrid grid, DeterministicRandom rng, BiomeDecor decor,
                       int tile, float heightScale, HashSet<int> occupied)
            => Tor(parent, grid, rng, decor, tile, heightScale, occupied, out _);

        static int Tor(Transform parent, TileGrid grid, DeterministicRandom rng, BiomeDecor decor,
                       int tile, float heightScale, HashSet<int> occupied, out float crown)
        {
            var middle = Vec2.FromTile(grid, tile);
            float foot = grid.SurfaceElevation(middle.X, middle.Y) * heightScale;

            int pieces = 0;
            int courses = rng.Range(2, 4);
            float standing = 0f;

            for (int course = 0; course < courses; course++)
            {
                // Narrower and shorter as it goes up, so the mass has a shoulder and a cap
                // rather than being a column.
                float spread = TorSpread * (1f - course * 0.3f);
                int round = Mathf.Max(3, 7 - course * 2);
                float tall = TorPiece * (1f - course * 0.22f);

                for (int step = 0; step < round; step++)
                {
                    float turn = step / (float)round * Mathf.PI * 2f + rng.Range(-0.3f, 0.3f);
                    float out_ = spread * rng.Range(0.55f, 1f);

                    var rock = Object.Instantiate(Any(decor.Cliffs, rng), parent);
                    rock.transform.rotation = Quaternion.Euler(decor.Cliffs.ZUp ? -90f : 0f,
                                                               rng.Range(0f, 360f), 0f);

                    // Named, because a tor is built in courses and every course above the
                    // first stands on the one below rather than on the ground - which is
                    // what the smoke test's floating check is written to catch. It skips
                    // these by name, as it already skips the building kit's own parts.
                    rock.name = TorPieceName + rock.name;

                    ModelScaling.Fit(rock, tall * rng.Range(0.8f, 1.25f), 0f);

                    var box = ModelScaling.Measure(rock);
                    float x = middle.X + Mathf.Cos(turn) * out_;
                    float z = middle.Y + Mathf.Sin(turn) * out_;

                    // Each course set into the one below it, so the mass reads as one rock
                    // rather than as a pile of separate ones.
                    float sits = foot + standing - (course == 0 ? 0f : box.size.y * TorSink);

                    rock.transform.position += new Vector3(x - box.center.x,
                                                            sits - box.min.y,
                                                            z - box.center.z);
                    Block(rock, canopy: false);

                    // And the road goes through the mass rather than into it. A tor is
                    // raised on a tile clear of the lane, but it spreads eight metres from
                    // that tile - two tiles - so its own outer course came down in the
                    // road. Dropping the piece rather than moving the tor is what a pass
                    // through rock looks like: the mass opens where the road crosses it.
                    if (Barring(grid, _road, rock)) { Unbuild(rock); continue; }

                    pieces++;
                }

                standing += tall * (1f - TorSink);
            }

            Claim(grid, new Bounds(new Vector3(middle.X, foot, middle.Y),
                                   new Vector3(TorSpread * 2f, standing, TorSpread * 2f)), occupied);

            crown = foot + standing;
            return pieces;
        }

        /// <summary>Whether water lies within so many tiles.</summary>
        static bool NearWater(TileGrid grid, int x, int y, int tiles)
        {
            for (int dy = -tiles; dy <= tiles; dy++)
                for (int dx = -tiles; dx <= tiles; dx++)
                {
                    if (!grid.InBounds(x + dx, y + dy)) continue;

                    var terrain = grid[x + dx, y + dy];
                    if (terrain == TerrainType.Water || terrain == TerrainType.Ford) return true;
                }

            return false;
        }

        /// <summary>What a piece of a rock mass is called. See SmokeTest.</summary>
        public const string TorPieceName = "Tor_";

        /// <summary>How many tors a level may carry.</summary>
        // Ten. At fourteen, with two thirds of the country bare rock under them, the
        // chapter sheet came back with the camera inside a tor on three levels of ten -
        // a country you cannot see across is not a country you can drive through either.
        const int MostTors = 10;

        /// <summary>How likely a free pass tile is to carry one.</summary>
        const float TorChance = 0.1f;

        /// <summary>How far apart two tors stand, in tiles.</summary>
        const float TorsApart = 7f;

        /// <summary>How wide a tor's lowest course is, in metres from its middle.</summary>
        const float TorSpread = 8f;

        /// <summary>How tall one piece of its lowest course stands, in metres.</summary>
        const float TorPiece = 10f;

        /// <summary>How far each course sinks into the one below it, as a share of its height.</summary>
        const float TorSink = 0.35f;

        /// <summary>
        /// How far from the falling water the rock masses beside it stand, in tiles.
        ///
        /// Five. At three their shoulders met over the channel and the fall was walled in:
        /// photographed from downstream, the picture was two rocks and a sliver of water.
        /// </summary>
        const int TorFromFall = 5;

        static int PlaceCliffs(Transform parent, TileGrid grid, DeterministicRandom rng,
                               BiomeDecor decor, HashSet<int> occupied, float heightScale,
                               HashSet<int> road = null, Towns.Plan town = default)
        {
            if (!decor.Cliffs.Any) return 0;

            int placed = 0;
            var stood = new List<int>();

            for (int i = 0; i < grid.TileCount && placed < MaxLandmarks * 3; i++)
            {
                bool walled = decor.RockPasses && grid[i] == TerrainType.MountainPass;
                if (grid[i] != TerrainType.Cliff && !walled) continue;

                // Not every pass tile, or the road is a corridor of stone with no way to see
                // out of it: two in five, which leaves gaps to look through and shoulders to
                // walk round.
                if (walled && !rng.Chance(PassRock)) continue;

                // The town is built of impassable ground too — its walls and the block
                // in its middle — and what stands on those is masonry, not rock. Cliff
                // is the terrain the stamp had to hand; it was never a statement that
                // there is a crag here.
                if (town.Any)
                {
                    grid.ToCoords(i, out int tx, out int ty);
                    if (town.Holds(tx, ty)) continue;
                }

                if (occupied.Contains(i)) continue;
                if (road != null && road.Contains(i)) continue;
                if (!Apart(grid, i, stood, 2f)) continue;
                stood.Add(i);

                // Bedded into the slope rather than set on the surface. A cliff piece is
                // several tiles across and the ground under it falls away, so seated on the
                // height of its middle it hangs by the difference - measured, up to a metre
                // clear on 2-5, 3-9 and 4-2. A tenth of its own height buries that.
                var choice = new Choice(decor.Cliffs, Any(decor.Cliffs, rng),
                                        CliffHeight * decor.CliffRise * rng.Range(0.8f, 1.3f),
                                        byWidth: false, sink: CliffBed);

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
                                float heightScale, float densityScale,
                                HashSet<int> road = null)
        {
            if (!decor.Willows.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MaxLandmarks * 2; i++)
            {
                if (grid[i] == TerrainType.Water || grid[i] == TerrainType.Cliff) continue;
                if (occupied.Contains(i)) continue;
                if (road != null && road.Contains(i)) continue;

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

        /// <summary>How far a camp may be moved to find dry ground, in tiles.</summary>
        const int CampReach = 3;

        /// <summary>
        /// The nearest tile to this one that a tent could stand on, or -1.
        ///
        /// Dry, passable and not a crossing. Searched outward in rings so the camp moves
        /// as little as it can: a band watching a ford should still be at the ford.
        /// </summary>
        static int DryGroundNear(TileGrid grid, int tile)
        {
            grid.ToCoords(tile, out int x, out int y);

            for (int ring = 0; ring <= CampReach; ring++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        // The ring's edge only; the inside was covered by the ring before.
                        if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue;
                        if (!grid.InBounds(x + dx, y + dy)) continue;

                        var terrain = grid[grid.ToIndex(x + dx, y + dy)];
                        if (terrain == TerrainType.Water || terrain == TerrainType.Ford) continue;
                        if (!grid.IsPassable(x + dx, y + dy)) continue;

                        return grid.ToIndex(x + dx, y + dy);
                    }
                }
            }

            return -1;
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
                              IReadOnlyCollection<int> sites, HashSet<int> road = null,
                              List<Landmark> found = null)
        {
            if (sites == null || !decor.Camps.Any) return 0;

            int placed = 0;

            foreach (int tile in sites)
            {
                if (tile < 0 || tile >= grid.TileCount) continue;

                // <b>On the bank, not in the river.</b> The site is where an enemy group
                // holds ground, and a ford is a chokepoint, so that is where the ambushes
                // are put — measured over chapter one, 29 of the 149 groups sit on a
                // crossing, two or three on every level. Nothing here ever asked what the
                // tile was, so their tents were pitched in the water, and the stepping
                // stones were then strewn over the canvas.
                //
                // The ambush stays where it is; only the tent moves, to the nearest dry
                // ground it can be pitched on. A camp beside the crossing the raiders are
                // watching is what the signal was always meant to say.
                // Its own name rather than reusing the loop's, which C# will not let a
                // foreach assign to anyway: the site is where the band is, the pitch is
                // where its tent stands, and they are two different tiles.
                int pitch = DryGroundNear(grid, tile);
                if (pitch < 0) continue;

                if (occupied.Contains(pitch)) continue;
                if (road != null && road.Contains(pitch)) continue;

                var choice = new Choice(decor.Camps, Any(decor.Camps, rng), CampHeight,
                                        byWidth: false);

                // A tent is pitched, not set down: it gets the same seating as a house so
                // its pegged edge meets the ground on a slope.
                if (Scatter(parent, grid, rng, choice, pitch, heightScale, spread: 1.6f, occupied,
                            lift: -Seat(grid, pitch, heightScale, CampHeight), signal: true))
                {
                    Landmark.Note(found, LandmarkKind.Camp, pitch);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// Stands one bridge on a ford, turned across the water and scaled to be driven
        /// over. Placed by hand rather than scattered, because both of those are things
        /// the scatter deliberately randomises.
        /// </summary>
        static bool Bridge(Transform parent, TileGrid grid, DeterministicRandom rng,
                           BiomeDecor decor, int tile, float heightScale, HashSet<int> occupied,
                           IReadOnlyCollection<int> travelled)
        {
            var prefab = Any(decor.Fords, rng);
            if (prefab == null) return false;

            var at = Vec2.FromTile(grid, tile);
            float groundY = grid.SurfaceElevation(at.X, at.Y) * heightScale;

            var instance = Object.Instantiate(prefab, parent);

            instance.transform.position = new Vector3(at.X, groundY, at.Y);

            var upright = decor.Fords.ZUp
                ? Quaternion.Euler(-90f, 0f, 0f)
                : Quaternion.identity;

            instance.transform.rotation = upright;

            // Which way the model is long is measured, not assumed. Whether a bridge
            // prefab is authored running along X or along Z is the artist's business and
            // a rule written from one guess is wrong for the next pack.
            var bounds = ModelScaling.Measure(instance);
            bool longAlongX = bounds.size.x > bounds.size.z;

            // And which way it has to lie is square to its river, along the ford's own run.
            //
            // <b>It used to turn to meet the caravan's drawn lane, and that made it a
            // different bridge on each map.</b> The planning map is where the lane is drawn,
            // so it cannot know the lane before it has been drawn — it laid the bridge
            // square, and the run then turned the same bridge up to thirty degrees to meet
            // the line. The planning map has to show the country exactly as it will be
            // crossed, so the bridge answers to the river alone. The column crabs a few
            // degrees across the deck where the line meets the water at a slant; that is
            // the lesser fault by far.
            float across = Crossing(grid, tile, travelled);

            // The model's own length is turned onto that bearing. A prefab authored
            // along X is already a quarter turn from one authored along Z.
            //
            // Applied *before* the upright rotation rather than after it. A Z-up prefab
            // has already been laid down a quarter turn about X, so its own Y axis points
            // along world -Z — and a yaw multiplied on the right turns about that, which
            // rolls the bridge instead of aiming it.
            instance.transform.rotation =
                Quaternion.Euler(0f, longAlongX ? across - 90f : across, 0f) * upright;

            // Long enough to reach both banks. The crossing is measured rather than
            // assumed at three tiles: fords are cut to the width of their river, and a
            // twelve-metre bridge over a five-tile ford is a jetty from each side.
            float span = Mathf.Max(FordSpan,
                                   (FordWidth(grid, tile, across) + 1) * TileGrid.TileSize
                                   + 2f * BridgeLanding);

            // Fitted by the span alone — a zero deck demand leaves only the length term —
            // and then widened on its own axis. Asking FitToCrossing for both made the two
            // fight: it scales uniformly and takes the larger demand, so a five-metre deck
            // dragged a twelve-metre crossing out to a twenty-one-metre bridge. The length
            // is what the ford measures and the width is what a wagon needs, and they are
            // not the same question.
            // Along the bearing it has just been turned onto, so the span is measured as
            // the bridge's length rather than as the longer side of a box around it.
            float lengthwise = across * Mathf.Deg2Rad;
            ModelScaling.FitToCrossing(instance, 0f, span, groundY,
                                       new Vector3(Mathf.Sin(lengthwise), 0f, Mathf.Cos(lengthwise)));

            // Square to the run the bridge lies along.
            float widthwise = (across + 90f) * Mathf.Deg2Rad;
            ModelScaling.Widen(instance,
                               FordDeck,
                               new Vector3(Mathf.Sin(widthwise), 0f, Mathf.Cos(widthwise)),
                               groundY);

            // Measured rather than described. Nothing here knows where the roadway is
            // inside a bridge model, so the bridge is asked at runtime — see BridgeDeck.
            // <b>Bedded a hand's width into the banks.</b> Fitted to the crossing it sat with
            // its lowest plank exactly on the height of the middle of the ford - and the
            // banks either side stand a little above that, so both ends hung clear of the
            // ground with daylight under them. A bridge rests on its banks.
            instance.transform.position += new Vector3(0f, -BridgeBed, 0f);

            var deck = instance.AddComponent<BridgeDeck>();
            deck.Measure();

            // And now the one thing FitToCrossing cannot do: stand the bridge on its
            // roadway instead of on its feet. BridgeDeck measured the roadway on the way
            // in — down the middle of the span, where the road runs and the railings are
            // not — so the number is already there to be read.
            float before = float.NaN, after = float.NaN;

            // Only an exact measurement may move the bridge. A roadway taken from the
            // model's own boxes is good enough to ride over and not good enough to seat
            // by: dropping the bridge until a guessed deck sat a quarter of a metre above
            // the bank would bury it to the parapet, which is the tunnel this was written
            // to stop happening a third time. Inexact means the bridge stays where
            // FitToCrossing put it and the column is still lifted onto it.
            if (deck.Exact)
            {
                before = deck.Deck - groundY;
                instance.transform.position +=
                    new Vector3(0f, groundY + DeckClearance - deck.Deck, 0f);

                // The colliders travel with the transform, but the footprint and the deck
                // height were both measured where they used to be. Measure again or the
                // bridge answers for the wrong ground at the wrong height.
                deck.Measure();

                after = deck.Deck - groundY;
            }

            var got = ModelScaling.Measure(instance);

            Debug.Log($"[The Veil] Bridge {prefab.name} on tile {tile}: bearing {across:F0}°, "
                    + $"span asked {span:F1} m and got {Mathf.Max(got.size.x, got.size.z):F1} m, "
                    + $"deck {Mathf.Min(got.size.x, got.size.z):F1} m wide, "
                    + $"roadway {before:F1} m above the bank before settling and {after:F1} m after, "
                    + $"from {deck.Meshes} mesh(es) and {deck.Surfaces} collider(s), "
                    + $"{(deck.Exact ? "measured by ray" : "from the model's own boxes")} "
                    + $"at {deck.Deck:F2} m.");

            // Claimed with a margin, so the wood does not close over the crossing.
            //
            // <b>The bridge reserved its own footprint and nothing more, and that was
            // enough until the marshes were filled.</b> Marsh density went from 0.06 to
            // 0.45 — the fen beside a river is now the densest ground on the map after
            // the forest — so trees grew to the deck's edge on both banks and the water
            // under it went out of sight. What is left reads as a bridge standing in a
            // wood.
            //
            // Six metres of clear ground round it: a tile and a half, enough to see the
            // river it crosses and to drive up to it, and far too little to leave a
            // clearing anybody would notice.
            got.Expand(BridgeClearance * 2f);
            Claim(grid, got, occupied);
            return true;
        }

        /// <summary>
        /// Shortest thing that is worth walking round, in metres.
        ///
        /// Waist height. Below it are the grass, the flowers, the lilypads and the loose
        /// stones — things a boot goes over and a wheel rolls across — and making any of
        /// them solid would fill the map with invisible pebbles for the escort to shuffle
        /// around. Above it are trunks, boulders, walls and carts.
        /// </summary>
        public const float SolidHeight = 1.2f;

        /// <summary>
        /// What share of a canopy's width is trunk.
        ///
        /// A tenth. Measured off the pack rather than argued: the spruces run about four
        /// and a half metres of crown over roughly half a metre of stem, and the birches
        /// about the same. It is the difference between a forest you push through and a
        /// forest that is a wall — see Solid.
        /// </summary>
        public const float TrunkShare = 0.1f;

        public const float MinTrunk = 0.3f;
        public const float MaxTrunk = 1.1f;

        /// <summary>
        /// Marks a placed prop as something to walk round, if it is big enough to be one.
        ///
        /// Measured from what is actually standing there, after it has been scaled: the
        /// table size is a request and <see cref="ModelScaling.FitWithin"/> is free to
        /// refuse it.
        /// </summary>
        static void Block(GameObject instance, bool canopy)
        {
            if (instance == null) return;

            var bounds = ModelScaling.Measure(instance);
            if (bounds.size.y < SolidHeight) return;

            float across = Mathf.Max(bounds.extents.x, bounds.extents.z);

            float radius = canopy
                ? Mathf.Clamp(across * 2f * TrunkShare, MinTrunk, MaxTrunk)
                : across * 0.85f;

            var solid = instance.AddComponent<Solid>();
            solid.Radius = radius;
            solid.Centre = new Vector2(bounds.center.x, bounds.center.z);
        }

        /// <summary>Says that a prop is telling the player something. See Signal.</summary>
        static GameObject Mark(GameObject instance)
        {
            if (instance != null && instance.GetComponent<Signal>() == null)
                instance.AddComponent<Signal>();

            return instance;
        }

        /// <summary>Marks every tile a placed thing's footprint reaches into.</summary>
        // A prop scaled well past its own tile is otherwise invisible to every later
        // pass: they ask whether the *centre* tile is taken and strew freely over the
        // rest of it.
        static void Claim(TileGrid grid, Bounds bounds, HashSet<int> occupied)
        {
            if (occupied == null) return;

            int minX = (int)Mathf.Floor(bounds.min.x / TileGrid.TileSize);
            int maxX = (int)Mathf.Floor(bounds.max.x / TileGrid.TileSize);
            int minY = (int)Mathf.Floor(bounds.min.z / TileGrid.TileSize);
            int maxY = (int)Mathf.Floor(bounds.max.z / TileGrid.TileSize);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (grid.InBounds(x, y)) occupied.Add(grid.ToIndex(x, y));
        }

        /// <summary>
        /// The bearing a bridge must lie on to cross the water here, in degrees.
        ///
        /// A ford used to be turned by the same dice as a pine, so half of them lay
        /// *along* the river with both ends in the water. Asking the four neighbours
        /// which axis was wetter fixed the rivers that run square and left the diagonal
        /// ones: a river crossing a tile corner to corner has one wet neighbour on each
        /// axis, the count ties, and the bridge is laid north-south over water flowing
        /// north-east. One of them was, and it was the one that got noticed.
        ///
        /// So the watercourse is measured rather than counted. Every wet tile within two
        /// gives a vector from here, and those vectors are averaged as *lines* rather
        /// than as arrows — a river runs both ways at once, and summing the offsets of a
        /// straight one cancels it to nothing. Doubling the angle before averaging and
        /// halving it after is the standard way round that; the road is then square to
        /// what comes out.
        /// </summary>
        static float Crossing(TileGrid grid, int tile, IReadOnlyCollection<int> travelled = null)
        {
            // The ford run first, because it is not an estimate. A ford is cut as a line
            // of tiles straight across its river (TerrainGenerator carves it out from the
            // river tile along x while the water lasts), so the tiles the map itself calls
            // a crossing *are* the road over the water, and the bridge lies along them.
            //
            // Measured over ten levels: the ford run is 90° on every crossing in chapter
            // one, and asking the water instead answered anywhere between 54° and 120° —
            // and 0°, laying the bridge straight along its own river, when the wet tiles
            // round it cancelled out. Counting ford tiles as water made that worse rather
            // than better: a three-tile ford is three tiles of "water" lying square to
            // the river, so the average leant toward the crossing and the ninety-degree
            // turn that follows put the bridge in the water.
            float ford = Bearing(grid, tile, TerrainType.Ford, 3);
            if (!float.IsNaN(ford)) return ford;

            // <b>And the road only where the ford has no run to read.</b>
            //
            // Asking the road first was tried and reverted on sight: a bridge laid along
            // the road.s bearing instead of the river.s crosses the water at a slant, and
            // what that looks like is a bridge that does not belong to its stream. The
            // ford is cut straight across its river and the deck belongs to the river.
            //
            // A single wet tile has no run to read, so there the road is the whole answer
            // — 1-4.s crossing is exactly that, and before this fallback existed its bridge
            // sat at whatever angle the surrounding water averaged to while the road came
            // at it dead straight.
            float road = Bearing(grid, tile, travelled, 2);
            if (!float.IsNaN(road)) return road;

            float water = Bearing(grid, tile, TerrainType.Water, 2);
            if (!float.IsNaN(water)) return water + 90f;

            return 0f;
        }

        /// <summary>
        /// The bearing of a run of one terrain type around a tile, as a yaw.
        ///
        /// Averaged as *lines* rather than as arrows: a river runs both ways at once and
        /// a ford is crossed in either direction, so summing the offsets of a straight
        /// one cancels it to nothing. Doubling the angle before averaging and halving it
        /// after is the standard way round that. Near tiles count for more than far ones,
        /// which keeps a bend two tiles away from turning the answer.
        ///
        /// NaN when there is nothing to measure, which is a real answer and not a
        /// failure: the caller has a better idea than a made-up bearing.
        /// </summary>
        static float Bearing(TileGrid grid, int tile, TerrainType of, int radius)
        {
            grid.ToCoords(tile, out int x, out int y);

            float sumSin = 0f, sumCos = 0f;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!grid.InBounds(x + dx, y + dy)) continue;
                    if (grid[grid.ToIndex(x + dx, y + dy)] != of) continue;

                    float angle = (float)System.Math.Atan2(dy, dx);
                    float weight = 1f / Mathf.Sqrt(dx * dx + dy * dy);

                    sumSin += weight * (float)System.Math.Sin(angle * 2f);
                    sumCos += weight * (float)System.Math.Cos(angle * 2f);
                }
            }

            if (sumSin * sumSin + sumCos * sumCos < 0.0001f) return float.NaN;

            // Unity's yaw looks up +Z and turns clockwise — the opposite sense to atan2
            // about +X, hence the ninety and the subtraction.
            float bearing = (float)System.Math.Atan2(sumSin, sumCos) * 0.5f;
            return 90f - bearing * 57.29578f;
        }

        /// <summary>
        /// The same measurement taken over a set of tiles rather than a terrain type.
        ///
        /// For asking the road which way it is going. A run of route tiles through a ford
        /// is a line in exactly the sense <see cref="Bearing(TileGrid,int,TerrainType,int)"/>
        /// means: it is crossed in either direction and has no arrow, so it is averaged
        /// the same doubled-angle way.
        /// </summary>
        static float Bearing(TileGrid grid, int tile, IReadOnlyCollection<int> of, int radius)
        {
            if (of == null || of.Count == 0) return float.NaN;

            // Hashed once rather than scanned per tile: the road is a few hundred tiles
            // and this asks about twenty-four of them.
            var road = of as HashSet<int> ?? new HashSet<int>(of);

            grid.ToCoords(tile, out int x, out int y);

            float sumSin = 0f, sumCos = 0f;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!grid.InBounds(x + dx, y + dy)) continue;
                    if (!road.Contains(grid.ToIndex(x + dx, y + dy))) continue;

                    float angle = (float)System.Math.Atan2(dy, dx);
                    float weight = 1f / Mathf.Sqrt(dx * dx + dy * dy);

                    sumSin += weight * (float)System.Math.Sin(angle * 2f);
                    sumCos += weight * (float)System.Math.Cos(angle * 2f);
                }
            }

            if (sumSin * sumSin + sumCos * sumCos < 0.0001f) return float.NaN;

            float bearing = (float)System.Math.Atan2(sumSin, sumCos) * 0.5f;
            return 90f - bearing * 57.29578f;
        }

        /// <summary>How many tiles wide the crossing is, along its own run.</summary>
        static int FordWidth(TileGrid grid, int tile, float bearing)
        {
            grid.ToCoords(tile, out int x, out int y);

            float radians = (90f - bearing) / 57.29578f;
            float dx = (float)System.Math.Cos(radians), dy = (float)System.Math.Sin(radians);

            int width = 1;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                for (int step = 1; step <= 6; step++)
                {
                    int tx = x + Mathf.RoundToInt(dx * step * sign);
                    int ty = y + Mathf.RoundToInt(dy * step * sign);

                    if (!grid.InBounds(tx, ty)) break;

                    // The crossing only. Counting open water too walks off along a bend
                    // or into a lake: measured over three chapters that gave a widest
                    // crossing of thirteen tiles and a fifty-six-metre bridge. The fords
                    // themselves run one to three tiles, every time.
                    if (grid[grid.ToIndex(tx, ty)] != TerrainType.Ford) break;

                    width++;
                }
            }

            return width;
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
                                  float heightScale, float densityScale,
                                  HashSet<int> road = null)
        {
            // The piles if the pack has them, the general stones if not.
            var stones = decor.Shore.Any ? decor.Shore : decor.Rocks;
            if (!stones.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MaxShoreStones; i++)
            {
                if (grid[i] == TerrainType.Water) continue;
                if (occupied.Contains(i)) continue;
                if (road != null && road.Contains(i)) continue;

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
        /// The tiles the caravan and its escort walk over, widened from the wagons' line.
        ///
        /// See <see cref="DriveMarginTiles"/>. Chebyshev rather than Euclidean: a corner
        /// tile is as much in the way as a side one, and a diagonal stretch of route is
        /// drawn as a staircase whose corners are exactly where a prop would sit.
        /// </summary>
        static HashSet<int> Lane(TileGrid grid, IReadOnlyCollection<int> driveLine, int margin)
        {
            if (driveLine == null) return null;
            if (margin <= 0) return new HashSet<int>(driveLine);

            var lane = new HashSet<int>();

            foreach (int tile in driveLine)
            {
                grid.ToCoords(tile, out int x, out int y);

                for (int dy = -margin; dy <= margin; dy++)
                    for (int dx = -margin; dx <= margin; dx++)
                        if (grid.InBounds(x + dx, y + dy)) lane.Add(grid.ToIndex(x + dx, y + dy));
            }

            return lane;
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

        /// <summary>
        /// How many trees stand on the apron, the drawn ground outside the playable grid.
        ///
        /// <b>A count rather than a density, because the apron is large and the triangle
        /// budget is not.</b> The skirt runs TerrainMeshBuilder.SkirtWidth — forty-eight
        /// metres — out from every edge, so on a 64x64 map it is about 58,000 square
        /// metres against the grid's 65,500: nearly as much ground again. Filling it at
        /// the forest's own density would be some 2,300 trees, and the trees are already
        /// the largest single line in the budget at around 800.
        ///
        /// Fourteen hundred, weighted toward the map's edge (see below). Spread evenly
        /// that would be one tree per forty square metres, which is a heath; the weighting
        /// puts half of them inside the first fourteen metres, where it comes to one per
        /// twenty-one against a pine canopy of about sixteen. That is a closed wall of
        /// wood seen from inside the map, thinning honestly behind it.
        ///
        /// <b>It is also the largest thing added to the budget in one go, and it is one
        /// number to turn down.</b> The trees were already the biggest single line at
        /// around 800, so this roughly triples them. Nothing walks out here, so they claim
        /// no ground, block nothing and signal nothing — but triangles are triangles, and
        /// if the frame rate drops on a phone this is the first place to look.
        /// </summary>
        public const int ApronTrees = 1400;

        /// <summary>How much of the apron is stone rather than wood.</summary>
        public const float ApronStone = 0.2f;

        /// <summary>How far an apron boulder is set into the ground, in metres.</summary>
        const float ApronStoneSink = 0.6f;

        /// <summary>
        /// Half the width of the road left through the apron at the start and the goal.
        ///
        /// <b>A corridor and not a clearing.</b> A round hole in the wood reads as a
        /// glade the caravan happens to be standing in; a lane cut straight out through
        /// the trees reads as the road it came in on, which is what it is. The column
        /// forms up on Caravan.RunUp — forty metres — of road behind the start line, and
        /// that road is off the map by construction: it is the reason the skirt exists.
        ///
        /// Eighteen either side, so thirty-six across. The caravan's own swept lane is
        /// eight either side (DriveHalfWidth), so this is that with room to see out of,
        /// and wide enough that a wagon at the back is not brushing trunks while the lead
        /// is already on the map.
        /// </summary>
        public const float ApronCorridorHalf = 18f;

        /// <summary>
        /// Whether a point on the apron lies in the road left open at an opening.
        ///
        /// The lane runs straight out from whichever map edge the opening is nearest,
        /// which for a start three tiles from the western edge is due west. Everything
        /// beyond the opening in that direction, and within half a corridor either side
        /// of it, stays bare.
        /// </summary>
        static bool InCorridor(Vec2 opening, float x, float z, float width, float depth)
        {
            float west = opening.X, east = width - opening.X;
            float south = opening.Y, north = depth - opening.Y;

            float nearest = Mathf.Min(Mathf.Min(west, east), Mathf.Min(south, north));

            if (nearest == west)
                return x <= opening.X && Mathf.Abs(z - opening.Y) < ApronCorridorHalf;

            if (nearest == east)
                return x >= opening.X && Mathf.Abs(z - opening.Y) < ApronCorridorHalf;

            if (nearest == south)
                return z <= opening.Y && Mathf.Abs(x - opening.X) < ApronCorridorHalf;

            return z >= opening.Y && Mathf.Abs(x - opening.X) < ApronCorridorHalf;
        }

        /// <summary>
        /// How far outside the map the wood starts, in metres.
        ///
        /// Nothing here may touch the playing field. A tree placed exactly on the boundary
        /// still leans over it — a pine's crown is about four and a half metres across, so
        /// half of it hangs inside — and the apron is scenery, not terrain. Three metres
        /// clears the widest crown the pack has and is invisible from the map: the wood
        /// still reads as starting at the edge.
        /// </summary>
        public const float ApronInset = 3f;

        /// <summary>
        /// Plants the apron: dense wood on the drawn ground outside the playable map,
        /// open at the start and the goal.
        ///
        /// The skirt was added so the caravan's run-up had ground under it and so the
        /// world did not end at the last tile. It has been bare ever since, which trades
        /// a hard edge for a soft one without giving the eye anything to stop at. A wood
        /// is what stops it, and it costs nothing in play: nothing walks there, so these
        /// trees claim no ground, block nothing and signal nothing.
        ///
        /// Weighted toward the map's edge rather than spread evenly. The near rows are
        /// what anybody sees; the far ones are behind them. Squaring the random depth
        /// puts about half the wood in the first fifteen metres, which reads as a wall
        /// from inside the map and thins honestly toward the horizon.
        /// </summary>
        static int PlaceApron(Transform parent, TileGrid grid, DeterministicRandom rng,
                              BiomeDecor decor, float heightScale,
                              IReadOnlyCollection<int> openings)
        {
            var wood = decor.Pines.Any ? decor.Pines : decor.Trees;
            if (!wood.Any) return 0;

            float skirt = TerrainMeshBuilder.SkirtWidth;
            float width = grid.Width * TileGrid.TileSize;
            float depth = grid.Height * TileGrid.TileSize;

            // Where the apron may not close in. Kept as world points rather than tiles:
            // the clearing has to reach out onto the apron, which has no tiles.
            var clear = new List<Vec2>();
            if (openings != null)
                foreach (int tile in openings)
                    if (tile >= 0 && tile < grid.TileCount) clear.Add(Vec2.FromTile(grid, tile));

            int placed = 0;

            for (int i = 0; i < ApronTrees; i++)
            {
                // A side, then a place along it, then how far out. The sides are weighted
                // by length so a long map is not fringed like a square one.
                bool northSouth = rng.Range(0f, width + depth) < width;

                // Squared, so the wood is thickest against the map and thins outward —
                // and never nearer than ApronInset, so no crown hangs over the playing
                // field. What is drawn out here changes nothing that is played on.
                float t = rng.Range(0f, 1f);
                float out_ = ApronInset + (skirt - ApronInset) * (1f - t) * (1f - t);

                float x, z;

                if (northSouth)
                {
                    x = rng.Range(-skirt, width + skirt);
                    z = rng.Range(0, 2) == 0 ? -out_ : depth + out_;
                }
                else
                {
                    z = rng.Range(-skirt, depth + skirt);
                    x = rng.Range(0, 2) == 0 ? -out_ : width + out_;
                }

                bool onTheRoad = false;
                foreach (var opening in clear)
                    if (InCorridor(opening, x, z, width, depth)) { onTheRoad = true; break; }

                if (onTheRoad) continue;

                // The elevation sampler clamps outside the grid and the skirt is drawn flat
                // at the edge's own height, so the two agree out here by construction.
                float groundY = grid.SurfaceElevation(x, z) * heightScale;

                // <b>Stone among the trees, which is what the edge of this country is.</b>
                // The apron was a hedge of conifers all the way round, and every reference
                // picture of it is a broken rim: pines standing between grey outcrops. One
                // piece in five is stone.
                bool stone = decor.Boulders.Any && rng.Chance(ApronStone);
                var set = stone ? decor.Boulders : wood;

                var instance = Object.Instantiate(Any(set, rng), parent);

                instance.transform.rotation = set.ZUp
                    ? Quaternion.Euler(-90f, rng.Range(0f, 360f), 0f)
                    : Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

                instance.transform.position = new Vector3(x, groundY, z);

                if (stone)
                {
                    var box = ModelScaling.Measure(instance);
                    float wide = Mathf.Max(box.size.x, box.size.z);
                    if (wide > 0.01f)
                        instance.transform.localScale *= BoulderWidth * rng.Range(0.9f, 2.2f) / wide;

                    box = ModelScaling.Measure(instance);
                    instance.transform.position += new Vector3(0f, groundY - box.min.y - ApronStoneSink, 0f);
                }
                else
                {
                    ModelScaling.Fit(instance, PineHeight * rng.Range(TreeJitterLow, TreeJitterHigh),
                                     groundY);
                }

                placed++;
            }

            return placed;
        }

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
        /// <summary>
        /// Worn ground: a track down every road, and bare earth in patches off them.
        ///
        /// <b>This was written and never called.</b> The set, the density table, the width,
        /// the lift off the ground and the cap on the count have all been in this file for
        /// a long time, with a comment saying the road "in this game has so far been a
        /// stripe of a slightly different green" - and it still was, because nothing ever
        /// placed one. Photographed at eye level on 4-3 there was no path anywhere in the
        /// country, which is what a player is looking at while they drive down it.
        ///
        /// The roads first and by name, not by the density table: the three roads are what
        /// the level is about, and a track laid along each of them is the one thing on the
        /// ground that says where they go. Then bare patches off them, by the table.
        ///
        /// Flat pieces, so a tile with any real fall in it is left alone (PatchMaxFall) and
        /// every piece is lifted a finger's width off the ground it lies on (PatchLift).
        /// </summary>
        static int PlacePatches(Transform parent, TileGrid grid, DeterministicRandom rng,
                                BiomeDecor decor, float heightScale, float densityScale,
                                IReadOnlyCollection<int> travelled)
        {
            if (!decor.GroundPatches.Any) return 0;

            int placed = 0;
            var roads = travelled == null ? new HashSet<int>() : new HashSet<int>(travelled);

            foreach (int tile in roads)
            {
                if (placed >= MaxGroundPatches) break;
                if (!Patchable(grid, tile, heightScale)) continue;

                // A piece here and there, not a paving. The road is painted into the ground
                // (TerrainPalette.Track); these are the gravel and the bare earth on it.
                if (!rng.Chance(TrackPatch)) continue;

                var worn = new Choice(decor.GroundPatches, Any(decor.GroundPatches, rng),
                                      PatchWidth, byWidth: true, canopy: true);

                Scatter(parent, grid, rng, worn, tile, heightScale, spread: 0.35f,
                        lift: PatchLift, yaw: Along(grid, tile, roads, rng));
                placed++;
            }

            // And nothing off them. Scattered by the density table it came out as brown
            // discs all over a green meadow - worn ground is worn by something, and what
            // wears it here is the road. The table is kept for a country that wants it.
            return placed;
        }

        /// <summary>Whether a flat piece can be laid on this tile without cutting into it.</summary>
        static bool Patchable(TileGrid grid, int tile, float heightScale)
        {
            if (tile < 0 || tile >= grid.TileCount) return false;

            var terrain = grid[tile];
            if (terrain == TerrainType.Water || terrain == TerrainType.Ford) return false;

            grid.ToCoords(tile, out int x, out int y);
            if (!grid.IsPassable(x, y)) return false;

            return Fall(grid, tile, heightScale) <= PatchMaxFall;
        }

        /// <summary>
        /// The way the road runs through a tile, as a yaw - so a track lies along it rather
        /// than across it. Where the road turns or the tile stands alone, any way will do.
        /// </summary>
        static float Along(TileGrid grid, int tile, HashSet<int> roads, DeterministicRandom rng)
        {
            grid.ToCoords(tile, out int x, out int y);

            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!grid.InBounds(x + dx, y + dy)) continue;
                    if (!roads.Contains(grid.ToIndex(x + dx, y + dy))) continue;

                    return Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                }

            return rng.Range(0f, 360f);
        }

        static int PlaceGroundCover(Transform parent, TileGrid grid, DeterministicRandom rng,
                                    BiomeDecor decor, HashSet<int> clear, HashSet<int> occupied,
                                    float heightScale, float densityScale)
        {
            if (!decor.GroundCover.Any) return 0;

            // <b>Thinned to fit, rather than sown until the budget is spent.</b> The cap
            // is counted while walking the tiles in order, so a map that wants more cover
            // than the cap allows got every tuft it asked for at one end and nothing at
            // all at the other: on the plains, where the table asks 1.7 a tile over four
            // thousand tiles, the far third of the country came out as bare sheet. The
            // demand is measured first and the whole map is sown at whatever share of it
            // fits, so a thin meadow is thin everywhere.
            float wanted = 0f;

            for (int i = 0; i < grid.TileCount; i++)
                if (CoverDensity.TryGetValue(grid[i], out float asked) && !occupied.Contains(i))
                    wanted += asked * densityScale;

            float fits = wanted > MaxGroundCover ? MaxGroundCover / wanted : 1f;

            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MaxGroundCover; i++)
            {
                if (!CoverDensity.TryGetValue(grid[i], out float density)) continue;
                if (occupied.Contains(i)) continue;

                density *= fits;

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

                    Scatter(parent, grid, rng, choice, i, heightScale, spread: 1.9f,
                            maxWidth: choice.ByWidth ? 0f : CoverWidth);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>Drops one model somewhere inside a tile, turned at random.</summary>
        /// <summary>
        /// Lays grass over the open ground, a mat to the tile.
        ///
        /// Canopy, so it claims no ground and nothing is kept off it: this is the floor,
        /// and the flowers, the stones and the trees all stand in it. Off the road, where
        /// the ground is meant to be worn.
        /// </summary>
        static int PlaceMats(Transform parent, TileGrid grid, DeterministicRandom rng,
                             BiomeDecor decor, HashSet<int> occupied, float heightScale,
                             float densityScale, HashSet<int> clear)
        {
            if (!decor.Mats.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MostMats; i++)
            {
                if (!MatDensity.TryGetValue(grid[i], out float chance)) continue;
                if (occupied.Contains(i)) continue;

                // Thinned on the drawn line rather than cleared, as the cover is: a
                // swept lane through a meadow reads as a road that is not there.
                if (clear != null && clear.Contains(i)) chance *= 0.3f;

                if (!rng.Chance(chance * densityScale)) continue;

                var choice = new Choice(decor.Mats, Any(decor.Mats, rng),
                                        MatWidth * rng.Range(0.85f, 1.25f),
                                        byWidth: true, canopy: true);

                if (Scatter(parent, grid, rng, choice, i, heightScale, spread: 1.2f,
                            lift: -MatSink))
                    placed++;
            }

            return placed;
        }

        /// <summary>How likely a tile of each country carries a mat of grass.</summary>
        // The meadow is nearly solid; a wood has a floor of litter and shade with grass in
        // the gaps; a fen has its own plants and wants none of this. The mountain pass is
        // bare rock and is not in the table at all.
        static readonly Dictionary<TerrainType, float> MatDensity = new Dictionary<TerrainType, float>
        {
            { TerrainType.Plains, 0.85f },
            { TerrainType.Forest, 0.35f }
        };

        /// <summary>How many mats a level may carry.</summary>
        // Two thousand, which is half the tiles on a map. They are one object each and
        // cheaper than the tufts they replace.
        const int MostMats = 2000;

        /// <summary>How broad one is, in metres - about a tile.</summary>
        const float MatWidth = 4.5f;

        /// <summary>And how far its base is set into the ground, so its edge does not show.</summary>
        const float MatSink = 0.1f;

        /// <summary>
        /// Drifts of flowers in the open, which is what makes a meadow a meadow.
        ///
        /// Laid as beds rather than sown: a bed is one species over a few tiles, thick
        /// enough that its colour is the ground rather than a speck on it. Off the road,
        /// off ground anything is standing on, and spread out from each other, because
        /// what the reference country shows is three or four drifts in a field and not a
        /// carpet of them.
        /// </summary>
        static int PlaceFlowerBeds(Transform parent, TileGrid grid, DeterministicRandom rng,
                                   BiomeDecor decor, HashSet<int> occupied, float heightScale,
                                   float densityScale, HashSet<int> road)
        {
            if (!decor.Flowers.Any) return 0;

            var beds = new List<int>();
            int placed = 0;

            for (int i = 0; i < grid.TileCount && beds.Count < MostBeds; i++)
            {
                if (grid[i] != TerrainType.Plains) continue;
                if (occupied.Contains(i)) continue;
                if (road != null && road.Contains(i)) continue;
                if (!rng.Chance(BedChance)) continue;
                if (!Apart(grid, i, beds, BedsApart)) continue;

                beds.Add(i);

                // One species to a bed. Flowers grow from seed that fell in one place;
                // a drift of mixed colours reads as a flowerbed somebody planted.
                var flower = Any(decor.Flowers, rng);
                int thick = Mathf.RoundToInt(PerBed * densityScale * rng.Range(0.7f, 1.3f));

                for (int f = 0; f < thick; f++)
                {
                    // Measured across, not up. A flower fitted to its height comes out
                    // half a metre wide and reads as a speck from the camera; the pack's
                    // wildflowers are drawn as patches, and a patch wants to be laid at
                    // the size it was drawn.
                    var choice = new Choice(decor.Flowers, flower,
                                            FlowerWidth * rng.Range(0.8f, 1.3f),
                                            byWidth: true, canopy: true);

                    if (Scatter(parent, grid, rng, choice, i, heightScale, spread: BedSpread))
                        placed++;
                }
            }

            return placed;
        }

        /// <summary>How many drifts of flowers a level may carry.</summary>
        // Eighteen on a map of four thousand tiles, which at four metres to the tile is
        // about one in every forty metres of open country.
        const int MostBeds = 30;

        /// <summary>The chance an open tile is where one starts.</summary>
        const float BedChance = 0.09f;

        /// <summary>How far apart they stand, in tiles.</summary>
        const int BedsApart = 6;

        /// <summary>How many clumps of flowers one bed carries.</summary>
        // Twenty-four. A drift has to be the ground rather than a sprinkle on it: at
        // fourteen the bed read as a few flowers standing in grass, which is what the open
        // field already had.
        const float PerBed = 24f;

        /// <summary>How far they scatter from its middle, in metres.</summary>
        // Three and a half tiles across, which is a drift rather than a bouquet.
        const float BedSpread = 7f;

        /// <summary>How broad one patch of flowers is laid, in metres.</summary>
        const float FlowerWidth = 2.6f;

        /// <summary>
        /// Hummocks in the open ground.
        ///
        /// The country's own relief is smooth at this scale and a meadow with nothing on
        /// it reads as a sheet. These are ground rather than scenery - the pack draws them
        /// as a piece of grassed earth - so they are sunk to their own edge and nothing
        /// walks round them.
        /// </summary>
        static int PlaceMounds(Transform parent, TileGrid grid, DeterministicRandom rng,
                               BiomeDecor decor, HashSet<int> occupied, float heightScale,
                               HashSet<int> road)
        {
            if (!decor.Mounds.Any) return 0;

            var stood = new List<int>();
            int placed = 0;

            for (int i = 0; i < grid.TileCount && stood.Count < MostMounds; i++)
            {
                if (grid[i] != TerrainType.Plains) continue;
                if (occupied.Contains(i)) continue;
                if (road != null && road.Contains(i)) continue;
                if (!rng.Chance(MoundChance)) continue;
                if (!Apart(grid, i, stood, MoundsApart)) continue;

                grid.ToCoords(i, out int x, out int y);
                if (NearWater(grid, x, y, 2)) continue;

                stood.Add(i);

                var choice = new Choice(decor.Mounds, Any(decor.Mounds, rng),
                                        MoundWidth * rng.Range(0.7f, 1.4f),
                                        byWidth: true, sink: MoundSink);

                if (Scatter(parent, grid, rng, choice, i, heightScale, spread: 1f, occupied))
                    placed++;
            }

            return placed;
        }

        /// <summary>How many hummocks a level may carry, and how far apart, in tiles.</summary>
        const int MostMounds = 12;

        const int MoundsApart = 7;

        /// <summary>The chance an open tile carries one.</summary>
        const float MoundChance = 0.06f;

        /// <summary>How broad one is, in metres, and how far it is set into the ground.</summary>
        const float MoundWidth = 9f;

        const float MoundSink = 0.25f;

        /// <summary>
        /// Butterflies over the open ground, and whatever else the country has flying.
        ///
        /// Instantiated rather than scattered: these are particle effects, so they want
        /// no fitting, no width cap, no solid disc and no claim on the ground under them.
        /// Lifted to about chest height, which is where a butterfly is.
        /// </summary>
        static int PlaceFauna(Transform parent, TileGrid grid, DeterministicRandom rng,
                              BiomeDecor decor, float heightScale, HashSet<int> road)
        {
            if (!decor.Fauna.Any) return 0;

            var flying = new List<int>();

            for (int i = 0; i < grid.TileCount && flying.Count < MostFlights; i++)
            {
                if (grid[i] != TerrainType.Plains && grid[i] != TerrainType.Forest) continue;
                if (road != null && road.Contains(i)) continue;
                if (!rng.Chance(FlightChance)) continue;
                if (!Apart(grid, i, flying, FlightsApart)) continue;

                flying.Add(i);

                var at = Vec2.FromTile(grid, i);
                float ground = grid.SurfaceElevation(at.X, at.Y) * heightScale;

                var flight = Object.Instantiate(Any(decor.Fauna, rng), parent);
                flight.transform.position = new Vector3(at.X + rng.Range(-1.5f, 1.5f),
                                                        ground + FlightLift,
                                                        at.Y + rng.Range(-1.5f, 1.5f));
                flight.transform.rotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
            }

            return flying.Count;
        }

        /// <summary>How many flights a level carries, how far apart, and how likely.</summary>
        // Ten. They are the only thing in the country that moves while nothing is
        // happening, and a meadow with a butterfly every twenty metres is an aviary.
        const int MostFlights = 10;

        const int FlightsApart = 8;

        const float FlightChance = 0.05f;

        /// <summary>How high above the grass they fly, in metres.</summary>
        const float FlightLift = 1.1f;

        static bool Scatter(Transform parent, TileGrid grid, DeterministicRandom rng,
                            Choice choice, int tile, float heightScale, float spread,
                            HashSet<int> occupied = null, float lift = 0f, float? yaw = null,
                            float maxWidth = 0f, bool signal = false, bool solid = false)
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
            // A random turn suits a tree and ruins a bridge. Anything whose direction
            // means something says so; everything else keeps the dice.
            float turn = yaw ?? rng.Range(0f, 360f);

            instance.transform.rotation = choice.ZUp
                ? Quaternion.Euler(-90f, turn, 0f)
                : Quaternion.Euler(0f, turn, 0f);

            // Zero would come out of a default Choice and scale the prop to nothing.
            float low = choice.Low > 0f ? choice.Low : JitterLow;
            float high = choice.High > 0f ? choice.High : JitterHigh;

            float size = choice.Size * rng.Range(low, high);

            // A signal is a landmark, so it is sized like one.
            //
            // <b>The camp is the only thing that comes through here with signal set, and
            // it was the only landmark missing both of these.</b> Houses, ruins, towers
            // and the keep are placed by Place, which applies the run's landmark scale and
            // the plan's floor; the camp is scattered instead — it is pitched with a
            // jitter and seated on a slope like a tree — and so it kept its bare 2.6 m
            // while the house beside it was drawn at 9.6. A tent shorter than the wagon
            // parked next to it reads as a toy, and on the plan map it stayed under the
            // floor that exists to make exactly this kind of small landmark legible: the
            // floor's own note lists "a camp at 2.6" among the sizes it lifts, and it has
            // never reached one.
            //
            // Guarded on `signal` rather than applied to the scatter, because the scatter
            // is also every tree, rock and bush on the map. Those are scenery and are
            // sized against the world; a landmark is sized against being *read*.
            if (signal) size = Mathf.Max(size * _landmarkScale, _landmarkFloor);

            // Anything fitted by height gets a width it never asked for, and a model
            // authored low and broad gets a great deal of it: this is how five kinds of
            // grass became fifteen hundred five-metre discs. A tree may be wider than it
            // is tall — that is a canopy — and everything else may not, by much.
            //
            // A set may name its own ratio, and that is checked before canopy rather than
            // after: a set says so precisely when being a canopy is not the whole truth
            // about it. The dead trees are the case. Most of them are bare trunks and want
            // the canopy's freedom, but the pack files the swamp's sprawling roots and
            // fallen branches in with them, and unlimited width let those reach
            // twenty-four metres across on a nine-metre budget.
            float cap = maxWidth > 0f ? maxWidth * rng.Range(low, high)
                      : choice.MaxSpread > 0f ? size * choice.MaxSpread
                      : choice.Canopy ? 0f
                      : size * SpreadLimit;

            // Stood on the ground first, and buried afterwards — see Bury, which is where
            // the depth is decided now. It used to be taken off `size`, which is what the
            // model was *asked* to be rather than what it came out as, and the two are a
            // different number whenever the width cap has had a word: a log is long and
            // low, so it fits its width long before it fits its height and ends up a
            // fraction of the height it was asked for. Forty percent of the asked-for
            // height then put the whole log under the ground. Measured over chapter one,
            // five to eight logs and branches a level were buried out of sight.
            if (choice.ByWidth) ModelScaling.FitToFootprint(instance, size, groundY);
            else if (cap > 0f) ModelScaling.FitWithin(instance, size, cap, groundY);
            else ModelScaling.Fit(instance, size, groundY);

            if (choice.Sink > 0f)
                Bury(instance, choice.Sink, Fall(grid, tile, heightScale) * SlopeSink);

            if (signal) Mark(instance);

            Block(instance, choice.Canopy);

            // And not standing in the caravan's lane, now that it is known how much of
            // this one is solid and where. The cheap test before the scatter asks about
            // the tile and the table's size; this asks the thing itself. See Barring.
            if (Barring(grid, _road, instance))
            {
                Unbuild(instance);
                return false;
            }

            // Canopy neither claims ground nor checks for it. Keeping it out of the
            // reserved set has a second effect worth having: grass and ferns may now
            // grow under a tree, where the tree's own footprint used to keep the floor
            // bare beneath it.
            if (choice.Canopy) return true;

            // Fitted before the ground is checked, because until it is fitted nobody
            // knows how much ground it wants. A big prop that cannot fit is destroyed
            // again rather than left standing through a watchtower.
            // <paramref name="solid"/> is how a caller says "this one always asks". The
            // size exemption below a tile.s width is about foliage — spruce crowns touch,
            // and a tile of air round every tree would give an orchard — and it is wrong
            // for anything standing among buildings. A town tree is six metres tall and
            // two wide, so it slipped under the exemption and was planted through a roof:
            // twenty of them, measured.
            if (!FootprintClear(grid, occupied, x, z, FootprintRadius(instance), always: solid))
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
        /// <summary>
        /// Whether a building, at the size it ended up, has any part of itself in the
        /// caravan's lane.
        ///
        /// <b>This is why the column drove through the watchtower.</b> A landmark was
        /// rejected only when the *tile it stands on* was in the lane — PlaceLandmarks
        /// tests road.Contains(i) and nothing else. Its body was never asked about. So a
        /// tower could stand legally on the first tile outside the swept band, four
        /// metres from the edge, and since Raise scales the model uniformly to its target
        /// height a tall one is several tiles wide. Its wall landed in the road.
        ///
        /// The same footprint the building reserves against other props, asked of the
        /// road as well — FootprintRadius is what Reserve already uses, so a landmark
        /// cannot claim ground it is not standing on and cannot overhang ground the
        /// caravan needs.
        ///
        /// Null road means nobody said where the caravan goes, which is the planning map.
        /// </summary>
        static bool InTheRoad(TileGrid grid, HashSet<int> road, GameObject instance,
                              float x, float z)
        {
            if (road == null || instance == null) return false;

            bool hit = false;
            ForEachTileUnder(grid, x, z, FootprintRadius(instance),
                             tile => { if (road.Contains(tile)) hit = true; });
            return hit;
        }

        /// <summary>
        /// Whether anything solid about this prop reaches into the caravan's lane.
        ///
        /// <b>Asked of the disc and not of the footprint, and that is the whole
        /// distinction.</b> The lane was guarded by one test - the tile a prop was placed
        /// on - and a cliff piece six metres across placed on the first tile outside the
        /// lane has two metres of itself inside it. So the column drove through rock that
        /// was, on paper, standing beside the road. Measured off the body instead
        /// (<see cref="InTheRoad"/>) it goes too far the other way: a spruce is ten metres
        /// of crown over half a metre of trunk, and refusing every tree whose branches
        /// reach the road would strip the verges of exactly the wood that makes a forest
        /// road look like one.
        ///
        /// What a wheel hits is what the escort walks round, which is already written down
        /// as the prop's <see cref="Solid"/> discs. So the crowns stay over the road and
        /// the trunks, boulders, walls and carts come out of it.
        /// </summary>
        static bool Barring(TileGrid grid, HashSet<int> road, GameObject instance)
        {
            if (road == null || instance == null) return false;

            foreach (var solid in instance.GetComponentsInChildren<Solid>(true))
            {
                bool hit = false;
                ForEachTileUnder(grid, solid.Centre.x, solid.Centre.y, solid.Radius,
                                 tile => { if (road.Contains(tile)) hit = true; });

                if (hit) return true;
            }

            return false;
        }

        /// <summary>Takes the ground-claim off a prop, leaving it standing.</summary>
        static void Unsolid(GameObject instance)
        {
            if (instance == null) return;

            foreach (var solid in instance.GetComponentsInChildren<Solid>(true))
            {
                if (Application.isPlaying) Object.Destroy(solid);
                else Object.DestroyImmediate(solid);
            }
        }

        /// <summary>Takes a building down again, at edit time or in play.</summary>
        static void Unbuild(GameObject instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }

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
        ///
        /// <b>Unless it is a building, and that exemption is a bug that shipped.</b> The
        /// reasoning above is about foliage and it was applied to everything: a house
        /// narrower than four metres never asked, so it was raised straight through the
        /// one already standing there. It had reserved its ground — reserving was never
        /// the half that was missing — and the next house simply did not look. Two roofs
        /// growing out of each other in the town street is what it looks like, and it is
        /// what was reported. Masonry always asks; a spruce still does not.
        /// </summary>
        static bool FootprintClear(TileGrid grid, HashSet<int> occupied, float x, float z,
                                   float radius, bool always = false)
        {
            if (occupied == null) return true;
            if (!always && radius <= TileGrid.TileSize) return true;

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
        /// The one building the whole journey is aimed at, standing on the goal.
        ///
        /// Until now the caravan was escorted to a differently coloured tile. The goal is
        /// painted by TerrainMeshBuilder and nothing was ever built on it, because the
        /// decorator was never told where it was — so the road ended at a square of
        /// paint. It ends here instead, which is also what the level roadmap has been
        /// climbing towards: its tenth waypoint is called the castle road.
        ///
        /// Centred *on* the goal rather than beside it, so arriving means going in
        /// through the gate.
        ///
        /// **And not an obstacle, which is the one thing it must not be.** Raise blocks
        /// everything it seats — right for a house, and wrong for the one building that
        /// is a destination. Block laid a solid disc on the goal tile (capped by
        /// ObstacleField.MaxRadius at six metres), RunVisuals.FindObstacles handed it to
        /// the run, and Squad.Advance pushes every troop group out of an obstacle.
        ///
        /// So the escort was shoved out of the castle in the last seconds of every level
        /// while the wagons drove in past them. The caravan itself is unaffected — it
        /// travels a fixed path by arc length and never consults the field, which is why
        /// this is a wrongness to watch rather than a level that cannot be finished.
        ///
        /// Walls that can be driven through is the lesser wrong. From four hundred metres
        /// up nobody sees the column clip a course of stone, and everybody sees an escort
        /// slide off its posts at the gate. The better answer is a solid per wall piece
        /// with the gateway left open, and it is a bigger risk than it looks — the troops'
        /// avoidance would then have to thread an opening, and a gap a little too narrow
        /// bunches them just the same, only less obviously.
        /// </summary>
        static int PlaceCastle(Transform parent, TileGrid grid, DeterministicRandom rng,
                               BiomeDecor decor, HashSet<int> occupied, float heightScale,
                               int goalTile, IReadOnlyCollection<int> travelled,
                               List<Landmark> found, int guard)
        {
            if (goalTile < 0 || goalTile >= grid.TileCount) return 0;
            if (decor.Kit == null || !decor.Kit.CanBuildCastle) return 0;

            var castle = BuildingBuilder.Castle(parent, decor.Kit, rng);
            if (castle == null) return 0;

            // Named, because road now sits where yaw used to and a castle passing its gate
            // angle positionally would hand it in as a set of road tiles.
            //
            // And no road is passed on purpose. The castle stands on the goal because that
            // is what the goal is, and its gate is turned to face the way the caravan
            // arrives — it is the one building the route is supposed to reach.
            // <b>Beside the goal, and not on it.</b>
            //
            // The castle stood on the goal because the goal is what the journey is aimed
            // at — and that made the journey end inside the enemy's fortress, with the
            // caravan driving through his curtain wall to get there. It is *his* castle:
            // the champion holds it, and what the road is for is reaching the place beside
            // it, past him.
            //
            // Which also dissolves the fault that was chased through two rewrites. The
            // walls could not be solid while the route ran through them, so they were left
            // drivable and the column clipped stone at the one moment the camera is on it.
            // Nothing has to be driven through any more, so nothing has to be left open.
            int site = CastleSite(grid, goalTile, travelled, occupied, guard);
            if (site < 0) return 0;

            if (!Raise(grid, site, rng, castle, CastleHeight, heightScale, occupied,
                       yaw: GateYaw(grid, goalTile, travelled), landmark: false, resize: false))
                return 0;

            BedTheYard(castle, grid, site, heightScale);
            WallOff(castle);

            Landmark.Note(found, LandmarkKind.Castle, site);
            _castle = castle;
            return 1;
        }

        /// <summary>
        /// Lays the bailey's stone on the ground it is actually on.
        ///
        /// <b>A castle is built in its own flat plane and set down on country that is
        /// not flat.</b> Everything inside the walls is assembled at the origin — that is
        /// what lets the whole thing be turned and seated as one thing — so the yard comes
        /// out as a level floor at whatever height Raise chose for the building. On a
        /// slope, a quarter of it is then underground: Smoke Test counted twelve pieces
        /// buried on 1-10, eight on 2-10 and eleven on 3-10, and what that looks like is
        /// grass pushing up through a paved courtyard.
        ///
        /// Moved by the difference between the ground under each flag and the ground under
        /// the castle's own tile, rather than seated on the ground outright. The yard is
        /// two layers — earth everywhere with stone over five parts in six of it — and
        /// seating both on the surface would flatten one into the other. A shared offset
        /// keeps the stone above its earth and still lets the floor follow the fall.
        ///
        /// Here rather than in BuildingBuilder because there is no ground to ask there.
        /// The same reason the courtyard is swept at the end of Decorate and not before
        /// it: some questions can only be put once the thing is standing where it stands.
        /// </summary>
        static void BedTheYard(GameObject castle, TileGrid grid, int site, float heightScale)
        {
            // <b>The castle.s own height, not the ground under its tile.</b>
            //
            // Those are not the same number and the difference is the whole fault. Raise
            // sinks a building so its footings bed into the slope rather than perching on
            // it, so the castle.s plane sits below grade by a constant — and a correction
            // measured against the ground under its tile is that same constant on every
            // flag, which cancels and moves nothing. It was measured, changed nothing on
            // 1-10 and made 2-10 worse.
            float floor = castle.transform.position.y;

            foreach (Transform piece in castle.transform)
            {
                // Everything that stands on the yard, and nothing that stands on the wall.
                //
                // The stone was the loud part of this and not the whole of it: crates, a
                // hay wain and a cart wheel went under on 3-10 for the same reason, being
                // seated on the same sunken plane. The masonry stays where Raise put it —
                // its footings are meant to be in the slope — and so do the banners, which
                // hang from the parapet and belong to the wall rather than to the ground.
                //
                // And the stair, which climbs to the wall walk: bedded on the yard it rose or
                // sank against a wall that stays put, and its top missed the walk.
                if (piece.name.Contains("Castle") || piece.name.Contains("Banner")
                    || piece.name.Contains("Stairs") || piece.name == "Tower") continue;

                var stood = piece.position;
                float ground = grid.SurfaceElevation(stood.x, stood.z) * heightScale;

                piece.position = new Vector3(stood.x, stood.y + (ground - floor), stood.z);
            }
        }

        /// <summary>
        /// The castle just built, so the courtyard can be swept once everything is down.
        ///
        /// Held on the class rather than passed along because the sweep has to happen at
        /// the very end of Decorate, after the scatter, and the castle is put up near the
        /// beginning of it.
        /// </summary>
        static GameObject _castle;

        /// <summary>
        /// Makes the castle's stonework solid, a piece at a time, leaving the gateway open.
        ///
        /// <b>This is the answer PlaceCastle's own note asked for, and it took a champion
        /// standing in the gate to make it worth the risk.</b> Raise blocks what it seats
        /// with one disc over the whole building, which on a castle walls the caravan out
        /// of its own courtyard — so the disc was taken straight back off and the walls
        /// were left drivable. The note recorded the trade honestly: *"From four hundred
        /// metres up nobody sees the column clip a course of stone."*
        ///
        /// Somebody does now. The champion holds the goal, so the last thing that happens
        /// on the tenth level of every chapter happens at the gate with the camera on it,
        /// and the column driving through the curtain wall is the first thing anybody
        /// watching notices.
        ///
        /// A disc per piece rather than one over the building. The gateway is a hole in
        /// the wall — BuildingBuilder leaves the pieces out — so there is nothing standing
        /// there to be made solid, and the way in is open without anything having to be
        /// carved out of the block. Each disc is the piece's own half-width, not the
        /// eight-tenths Block uses: a wall is thin and long, and a disc drawn to its
        /// length would seal the courtyard it stands around.
        /// </summary>
        static void WallOff(GameObject castle)
        {
            foreach (var solid in castle.GetComponentsInChildren<Solid>(true))
            {
                if (Application.isPlaying) Object.Destroy(solid);
                else Object.DestroyImmediate(solid);
            }

            foreach (Transform piece in castle.transform)
            {
                var bounds = ModelScaling.Measure(piece.gameObject);
                if (bounds.size.y < SolidHeight) continue;

                Stand(piece.gameObject, bounds);
            }
        }

        /// <summary>
        /// Marks a prop as ground nobody walks through - as a row of discs where it is long.
        ///
        /// <b>One disc is a post, and a curtain wall is not a post.</b> Everything solid in
        /// this game claims a circle about its middle, with the radius taken from its
        /// shorter side so it does not swallow the ground beside it - which is right for a
        /// tree and wrong for anything built in a line. A five-metre wall a metre thick
        /// claimed a metre-wide circle in the middle of itself, so the escort walked in one
        /// end of it and out of the other: reported from a playtest as men cutting through
        /// the castle. A wall, a fence, a stone dyke and a bridge's parapet are laid as a
        /// chain of touching discs down their own length instead.
        /// </summary>
        static void Stand(GameObject piece, Bounds bounds)
        {
            // <b>Except the gateway, which is the one piece that is meant to be walked
            // through.</b> The pack's gate is a wall with an arch in it, and a wall is
            // what a chain of discs down its length makes of it - so the escort would be
            // shut out of the yard it is walking into. It carried a disc before this, in
            // the middle of the opening, which was its own smaller version of the same
            // mistake. The way in is left open.
            if (piece.name.Contains("Gate")) return;

            float wide = Mathf.Max(bounds.size.x, bounds.size.z);
            float thick = Mathf.Min(bounds.size.x, bounds.size.z);
            float radius = Mathf.Max(thick * 0.5f, LeastSolid);

            if (wide <= thick * Stretched)
            {
                var one = piece.GetComponent<Solid>() ?? piece.AddComponent<Solid>();
                one.Radius = radius;
                one.Centre = new Vector2(bounds.center.x, bounds.center.z);
                return;
            }

            // Along its own length, a disc every radius, so the chain has no gap in it.
            bool alongX = bounds.size.x >= bounds.size.z;
            int discs = Mathf.Clamp(Mathf.CeilToInt(wide / radius), 2, MostSolids);

            for (int i = 0; i < discs; i++)
            {
                float along = discs == 1 ? 0.5f : i / (float)(discs - 1);
                float x = alongX ? Mathf.Lerp(bounds.min.x, bounds.max.x, along) : bounds.center.x;
                float z = alongX ? bounds.center.z : Mathf.Lerp(bounds.min.z, bounds.max.z, along);

                var link = new GameObject("Solid").transform;
                link.SetParent(piece.transform, false);

                var solid = link.gameObject.AddComponent<Solid>();
                solid.Radius = radius;
                solid.Centre = new Vector2(x, z);
            }
        }

        /// <summary>How many times its own thickness a prop must be before it counts as long.</summary>
        const float Stretched = 2f;

        /// <summary>The smallest circle a solid prop claims, in metres.</summary>
        const float LeastSolid = 0.45f;

        /// <summary>How many discs one prop may be laid out as.</summary>
        const int MostSolids = 24;

        /// <summary>
        /// Tiles from the goal to the middle of the castle.
        ///
        /// Fourteen. The castle is forty-six metres across — near six tiles to its wall
        /// from its middle — so this stands it about eight tiles clear of the goal: near
        /// enough to loom over the arrival, far enough that the caravan is not parked
        /// against the stonework, and far enough that the champion, who waits five tiles
        /// short of the goal, is in front of it rather than in it.
        /// </summary>
        public const int CastleStandoff = Strongholds.Standoff;

        /// <summary>
        /// Where the enemy's castle stands: off to one side of the goal, across the road.
        ///
        /// Square to the way the caravan comes in, so it is beside the arrival rather than
        /// behind it — a castle straight ahead is a castle the road appears to lead to,
        /// which is the reading this whole change is getting rid of.
        ///
        /// Either side will do and the first that fits is taken, which is not laziness:
        /// the two are mirror images of each other, the choice says nothing to the player,
        /// and trying both is what makes a goal near the edge of the map still get a
        /// castle instead of quietly getting none.
        /// </summary>
        static int CastleSite(TileGrid grid, int goalTile, IReadOnlyCollection<int> travelled,
                              HashSet<int> occupied, int guard)
        {
            // <b>Asked rather than worked out here.</b>
            //
            // This was the only place that knew where the castle went, which is why the
            // generator posted the chapter's champion five tiles back along the fastest
            // road while the castle went up fourteen tiles out to the side: the man who
            // holds it stood in a field with his back to it. The answer lives in
            // Sim.Strongholds now and both callers ask the same one.
            //
            // What stays here is the only part that is about this scene rather than about
            // the map: ground something else has already claimed.
            return Strongholds.Site(grid, goalTile, travelled, occupied, guard);
        }

        /// <summary>How tall the castle stands, in metres. Half again the watchtower.</summary>
        public const float CastleHeight = 15f;

        /// <summary>Tiles around the goal that are looked at to find which way the road comes in.</summary>
        public const int GateLookback = Strongholds.Lookback;

        /// <summary>
        /// Which way to turn the castle so its gate faces the road.
        ///
        /// The gate is built at -Z (see <see cref="BuildingBuilder.Castle"/>), so the
        /// castle is turned until that points at where the caravan is coming from. That
        /// direction is the average of the travelled tiles near the goal — an average
        /// rather than the single nearest one, because one tile of a winding approach
        /// points wherever that tile happens to lie.
        ///
        /// Snapped to a quarter turn, for the reason <see cref="Raise"/> gives about
        /// buildings at eleven degrees. With nothing to go on it faces west, which is
        /// where the caravan starts: the start is chosen from the leftmost columns.
        /// </summary>
        /// <summary>
        /// Which quarter turn puts the gate towards the road. Moved to Sim.Strongholds,
        /// because the generator needs the same answer to know where the castle.s lord
        /// waits — see CastleSite.
        /// </summary>
        static float GateYaw(TileGrid grid, int goalTile, IReadOnlyCollection<int> travelled)
            => Strongholds.GateYaw(grid, goalTile, travelled);

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
                                  float heightScale, IReadOnlyCollection<int> ruinSites,
                                  HashSet<int> road = null, HashSet<int> travelled = null,
                                  List<Landmark> found = null, bool settled = true)
        {
            int placed = 0;

            if (ruinSites != null && decor.Ruins.Any)
            {
                _boneSites.Clear();
                _trapWrecks.Clear();

                foreach (int wanted in ruinSites)
                {
                    // <b>No cap.</b> MaxLandmarks is a budget for scenery and these are not
                    // scenery: every trap has its bones, and a level with more traps than
                    // the budget had its last ones marked by nothing at all.

                    // Moved off the lane rather than dropped on it, and this is the whole
                    // of whether the tell works.
                    //
                    // TrapSigns puts a sign within three tiles of its field and knows to
                    // avoid water, cliff and the trap itself. It cannot avoid the lane:
                    // it runs in the simulation, and at planning time there is no drawn
                    // route to keep off. So the decorator was handed sites that sometimes
                    // sat in the road and answered by discarding them — which selects
                    // against exactly the signs worth having, because a trap laid on the
                    // route is the one whose warning lands on the route too.
                    //
                    // Measured on 1-1: three traps, one of them on the driven line and
                    // the other two 28 and 48 m off it. Two signs, and the one dropped
                    // was the one beside the trap the caravan actually drives onto. What
                    // survived stood 37 m out on ground nobody crosses. The player met no
                    // warning and the map drew no bones, and both came of the same line.
                    // <b>Exactly where TrapSigns put it, beside its trap.</b> This used to
                    // move the site off the road and off the planning map's corridors, and
                    // both moves were wrong. Off the road took the warning away from the
                    // road being driven, so the trap the caravan was about to hit was the
                    // one whose bones had been carried out of its way. And the corridors
                    // are only kept clear on the planning map, so the same trap's bones
                    // stood on one tile in the plan and another in the game.
                    //
                    // Bones are too low to be anything the column walks round (see Block),
                    // so on the road is fine; nothing before this has claimed ground but
                    // the castle, the town and the village, which are the same on both
                    // maps, so a site that is taken is taken on both and skipped on both.
                    int tile = wanted;

                    // Claimed after the wreck stands, not before it is attempted. Place
                    // asks for its whole footprint now, and a tile claimed up front is
                    // ground the site would have found taken by itself — which is only
                    // survivable while a ruin's footprint stays inside one tile. At
                    // RuinWidth and the landmark scale it comes to exactly a tile's four
                    // metres, so the tell has been standing on the boundary of switching
                    // itself off. It should not depend on that number.
                    // <b>Whatever else stands there.</b> A heap of bones is a hand's
                    // height off the ground and covers two metres of it; there is nothing
                    // it has to be kept apart from. Asking it to find its ground free was
                    // how every trap in 1-8's town went unmarked - the town claims the whole
                    // map before anything else is placed, streets and all - and how the one
                    // beside a village's field lost its bones to the field. So the heap goes
                    // down on its own tile and then claims it, so nothing is put on top of
                    // it afterwards.
                    int built = Wreck(parent, grid, tile, rng, decor, heightScale,
                                      new HashSet<int>(), found);
                    if (built == 0) continue;

                    _boneSites.Add(tile);

                    occupied.Add(tile);
                    placed += built;

                    // And a totem beside it, where the pack has one. A wreck says
                    // something happened here; a banner driven into the ground says
                    // somebody *chose* here, which is the difference between an accident
                    // and an ambush and is what the GDD's §5 table is asking for.
                    // Beside the wreck, which is what this has always said and did not do.
                    //
                    // Both went on the centre of the one tile the site occupies, so the
                    // banner stood in front of the bones. It is 4.8 m tall and they are
                    // 0.41 m lying down, and the §5 table names the bones as the tell —
                    // so the weaker signal was hiding the stronger one, and a player who
                    // rode past read a banner in a field.
                    //
                    // The map has always known: MapSymbols pushes a second symbol on one
                    // tile sideways rather than letting it land on the first, and says
                    // why. The world was drawing what the map was careful not to.
                    if (decor.Markers.Any
                        && Mark(Place(parent, grid, tile, rng,
                                      new Choice(decor.Markers, Any(decor.Markers, rng),
                                                 MarkerHeight, byWidth: false),
                                      heightScale, occupied,
                                      sink: Seat(grid, tile, heightScale, MarkerHeight),
                                      standoff: TotemStandoff)) != null)
                        Landmark.Note(found, LandmarkKind.Totem, tile);

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
                                              canopy: true, maxSpread: DeadTreeSpread,
                                              sink: StumpSink);

                        Scatter(parent, grid, rng, dead, tile, heightScale, spread: 2.6f);
                        placed++;
                    }
                }
            }

            for (int i = 0; i < grid.TileCount && placed < MaxLandmarks; i++)
            {
                if (clear != null && clear.Contains(i)) continue;
                if (road != null && road.Contains(i)) continue;
                if (occupied.Contains(i)) continue;

                grid.ToCoords(i, out int x, out int y);

                // Built things first, where the pack came as a kit. A house is a
                // foundation, a room and a roof; a castle tower is a base, a shaft and a
                // top; a ruin is what is left of one with its stone lying around it.
                if (decor.Kit != null
                    && Built(parent, grid, rng, decor, i, heightScale, occupied, travelled,
                             road, found, settled))
                {
                    placed++;
                    continue;
                }

                Choice choice = default;
                var kind = LandmarkKind.House;

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
                        kind = LandmarkKind.Farm;
                        break;

                    case TerrainType.MountainPass when decor.Watchtowers.Any && rng.Chance(0.012f):
                        choice = new Choice(decor.Watchtowers, Any(decor.Watchtowers, rng),
                                            WatchtowerHeight, false);
                        kind = LandmarkKind.Watchtower;
                        break;

                    case TerrainType.Forest when decor.Timber.Any && rng.Chance(0.006f):
                        choice = new Choice(decor.Timber, Any(decor.Timber, rng), TimberHeight,
                                            byWidth: false, maxSpread: TimberSpread);
                        kind = LandmarkKind.Timber;
                        break;
                }

                if (choice.Prefab == null) continue;

                // Buildings are set into the ground rather than stood on it.
                //
                // Everything else here rests its lowest point on the surface, which is
                // right for a tree and wrong for anything with a foundation: the pack's
                // towers taper to a rounded base meant to be buried, so on the ground
                // they read as pieces standing on a lawn. A tenth of their height buries
                // the taper, and on the slope of a pass it also stops the uphill side
                // showing daylight underneath.
                //
                // Nothing is written down until it stands. Place may now refuse the spot
                // — it asks for its whole footprint, the way Raise does — and the tile is
                // claimed and the landmark noted on the way out rather than on the way
                // in, so a refusal does not leave a reservation on empty ground or a
                // landmark on the plan that nobody built. The order used to be the other
                // way because Place could not fail.
                if (Place(parent, grid, i, rng, choice, heightScale, occupied,
                          sink: Seat(grid, i, heightScale, choice.Size)) == null)
                    continue;

                Landmark.Note(found, kind, i);
                occupied.Add(i);
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

            /// <summary>
            /// How many times its own height this prop may be wide. Zero takes the
            /// default: <see cref="SpreadLimit"/>, or no limit at all for a canopy.
            ///
            /// Scatter has always capped a height-fitted prop, which is what keeps a low
            /// broad model from being blown out sideways when the height it is given is
            /// larger than the height it was drawn at. Two holes in that. Place had no
            /// such rule at all, and canopy turns it off — rightly for a spruce, whose
            /// crown is meant to outreach nothing, and disastrously for a set that is
            /// only *mostly* upright. A fallen log is a fifth as tall as it is long; a
            /// stump is three times taller than it is wide. One number cannot size both,
            /// and whichever is chosen the other model is the one that comes out wrong.
            /// Written as a ratio rather than metres so it survives jitter: the cap grows
            /// with the size the prop actually came out at, not the one in the table.
            /// </summary>
            public readonly float MaxSpread;

            /// <summary>
            /// What share of its own size this prop is buried by. Zero rests it on top.
            ///
            /// Resting a model on its lowest point is right for a tree, whose trunk ends
            /// where the bark ends, and wrong for anything the artist gave roots or a
            /// footing to. A stump is modelled with its root flare, and set on the
            /// surface the flare becomes legs: the stump stands on them like a stool
            /// instead of growing out of the ground. Buildings had this from the start
            /// and it is the same problem — see <see cref="Seat"/>, whose reasoning and
            /// slope term apply here unchanged.
            /// </summary>
            public readonly float Sink;

            /// <summary>
            /// Whether this prop is placed at the size it was drawn, unscaled.
            ///
            /// For a set whose models have no dimension in common because each one is
            /// already the size of the real thing. Fitting them makes every member the
            /// size of the number rather than the size of itself: a cart is three metres
            /// across and a skull a quarter of one, and one width for both turned the
            /// skull into an eight-metre boulder of a head.
            ///
            /// It costs nothing here because these props are not what makes their site
            /// legible from map height. The totem beside them is nearly five metres and
            /// the two dead trees are nine, and both were put there for that job.
            /// </summary>
            public readonly bool LifeSize;

            public Choice(PropSet set, GameObject prefab, float size, bool byWidth,
                          float low = JitterLow, float high = JitterHigh, bool canopy = false,
                          float maxSpread = 0f, float sink = 0f, bool lifeSize = false)
            {
                Prefab = prefab;
                ZUp = set != null && set.ZUp;
                Size = size;
                ByWidth = byWidth;
                Low = low;
                High = high;
                Canopy = canopy;
                MaxSpread = maxSpread;
                Sink = sink;
                LifeSize = lifeSize;
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

        /// <summary>
        /// Rests a model on the ground without touching its size.
        ///
        /// The seating half of the fitters, on its own. A prefab's pivot is wherever the
        /// artist left it, so a model dropped at the surface height stands in it or over
        /// it as often as on it, and every fitter ends by measuring the thing and lifting
        /// it back onto its own underside. This does that and stops.
        /// </summary>
        static void Ground(GameObject instance, float groundY)
        {
            var bounds = ModelScaling.Measure(instance);
            instance.transform.position += new Vector3(0f, groundY - bounds.min.y, 0f);
        }

        /// <summary>
        /// Builds the scene at a trap site rather than dropping one prop on it.
        ///
        /// A wreck is a cart that stopped here and the things that came off it: a wheel
        /// on the ground, a spilled crate, bones. One prop cannot say that, and the
        /// version that tried said something worse — the set held a cart wheel among the
        /// carts, so a site could come out as a lone five-metre wheel standing on its rim
        /// in an empty field.
        ///
        /// So the pieces have roles now. The cart or the skeleton is the site; the
        /// wheels and crates are its debris, small, flat on the ground, and never the
        /// thing itself.
        /// </summary>
        static int Wreck(Transform parent, TileGrid grid, int tile, DeterministicRandom rng,
                         BiomeDecor decor, float heightScale, HashSet<int> occupied,
                         List<Landmark> found = null)
        {
            // Drawn once and held, so the site can say which of the two it turned out to
            // be. The same single call in the same place — hoisting it out of the Choice
            // does not touch the order the random numbers come in, and every map stays
            // exactly the map it was.
            // <b>Bones, always, because this is a trap sign and not a ruin.</b>
            //
            // The draw was Any, which takes whatever the set hands back — and the set holds
            // wrecks as well as remains, so about half of every trap on every level was
            // marked by a broken cart. That is not a warning, it is scenery; and because
            // the landmark below is noted from what was drawn, those halves came out on the
            // planning map as Wreck and the bones symbol never appeared for them.
            //
            // LandmarkKind.Bones has said "remains at a trap site" since the day it was
            // written. It was the draw that had never been told.
            var chosen = Bones(decor.Ruins, rng) ?? Any(decor.Ruins, rng);

            // Life size, and not sunk at all.
            //
            // Seat exists for things with a foundation: a share of the model's own size
            // to bury the taper, plus a share of the tile's fall so a building goes into
            // a hillside rather than onto it. Neither applies to remains. At RuinWidth
            // the first term alone is 0.6 m and a skull is 0.25 m tall, so it buried the
            // prop outright — and dropping to the slope term did not save it: the fall
            // across a tile at this height scale put the skeleton 0.53 m under, which is
            // more than the 0.41 m it stands. Both were measured, not reasoned about.
            //
            // What is left is Ground, which rests the model on the surface and nothing
            // else. A body lying on a slope may show a little daylight at one end; a body
            // that is underground shows nothing at all.
            var main = Mark(Place(parent, grid, tile, rng,
                                  new Choice(decor.Ruins, chosen, RuinWidth,
                                             byWidth: true, lifeSize: true),
                                  heightScale, occupied, sink: 0f));
            if (main == null) return 0;

            // <b>And nothing solid where the warning stands on the road.</b> A trap sign
            // is put beside its trap and traps are laid on the roads, so the sign lands in
            // the caravan's lane by design - that is the whole point of it. It was safe
            // while the sign was a skeleton lying at knee height, which Block leaves alone;
            // the arid pack's standing skeletons and the mountains' fossil are tall enough
            // to be marked solid, and a solid thing in the lane is something the wagons
            // drive through. Off the road it keeps its disc and the escort walks round it.
            if (Barring(grid, _road, main)) Unsolid(main);

            Landmark.Note(found, IsBones(chosen) ? LandmarkKind.Bones : LandmarkKind.Wreck, tile);

            int placed = 1;

            // A heap, when what the site turned out to be is remains.
            //
            // The GDD's §5 table asks for bone *piles* and the set could not make one:
            // the pack has a skeleton and two skulls, one prop went down per site, and
            // what stood there was a body or a head. At life size that is a skeleton
            // 0.41 m off the ground, which from the height the game is played at could
            // not be found even knowing where it was — it was looked for and missed.
            //
            // So the answer is more of them rather than a bigger one. Scaling a skeleton
            // up until it reads is how the eight-metre skull happened; a dozen bones over
            // two metres of ground is what the table asked for in the first place, and
            // every piece of it stays the size a bone is.
            if (IsBones(chosen))
            {
                int bones = rng.Range(BonePieces, BonePieces * 2);
                for (int i = 0; i < bones; i++)
                {
                    var piece = Bones(decor.Ruins, rng);
                    if (piece == null) break;

                    var at = Vec2.FromTile(grid, tile);
                    float bx = at.X + rng.Range(-BonePileSpread, BonePileSpread);
                    float bz = at.Y + rng.Range(-BonePileSpread, BonePileSpread);
                    float by = grid.SurfaceElevation(bx, bz) * heightScale;

                    var bone = Object.Instantiate(piece, parent);
                    bone.transform.position = new Vector3(bx, by, bz);
                    bone.transform.rotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

                    Ground(bone, by);

                    // <b>A heap is made of pieces a man could pick up.</b> The draw takes
                    // whatever in the set is bones, and the mountains mark their traps
                    // with a fossil - two and a half metres of spine, eight long. A dozen
                    // of those strewn over two metres of ground is not a heap of bones
                    // beside a wreck; it is a herd of dead animals standing in each other,
                    // which is what the first photograph of 6-1 showed. The sign itself
                    // may be as big as the country wants; what lies around it may not.
                    if (ModelScaling.Measure(bone).size.y > BonePiece)
                    {
                        Unbuild(bone);
                        continue;
                    }
                    Mark(bone);
                    placed++;
                }
            }

            // <b>And the broken wagon, which is what a trap sign was always for.</b>
            //
            // This used to be one to three loose pieces out of Wreckage: a wheel, a
            // crate, three barrels. Looked at from the height the game is played at, a
            // barrel beside some bones is a barrel beside some bones — it was asked
            // outright what the thing at the trap was meant to be, because on the photo
            // it read as a barrel and nothing else.
            //
            // One wagon says the whole sentence instead. Somebody came along this road
            // with a load, and what is left of them is lying here. The bones above are
            // the other half of it and the two together are the warning; the barrels
            // were neither half.
            if (!decor.Wrecks.Any) return placed;

            var wreck = Any(decor.Wrecks, rng);
            if (wreck == null) return placed;

            // Beside the bones rather than on them. Far enough not to stand in the heap,
            // near enough that the eye takes the two as one thing.
            float angle = rng.Range(0f, Mathf.PI * 2f);
            var where = Vec2.FromTile(grid, tile);
            float wx = where.X + Mathf.Cos(angle) * WreckStandoff;
            float wz = where.Y + Mathf.Sin(angle) * WreckStandoff;
            float wy = grid.SurfaceElevation(wx, wz) * heightScale;

            var wagon = Object.Instantiate(wreck, parent);
            wagon.transform.position = new Vector3(wx, wy, wz);

            // The bones lie on the road - that is what the sign is for - and the wagon
            // lies beside them at whichever angle the dice gave, which is as often as not
            // across the lane. Tried round the heap instead until one side of it is out of
            // the way, because the wagon is about to be made solid and the one rule the
            // lane has is that nothing solid stands in it.
            for (int turn = 1; turn <= WreckTries; turn++)
            {
                if (!InTheRoad(grid, _road, wagon, wx, wz)) break;

                angle += Mathf.PI * 2f / WreckTries;
                wx = where.X + Mathf.Cos(angle) * WreckStandoff;
                wz = where.Y + Mathf.Sin(angle) * WreckStandoff;
                wy = grid.SurfaceElevation(wx, wz) * heightScale;
                wagon.transform.position = new Vector3(wx, wy, wz);
            }

            // <b>Tipped onto its side, and at a wagon's size.</b> It went down at the size
            // it was drawn and the way it was drawn, and measured against a man that was
            // 1.90 x 1.57 x 0.85 m standing on its face - a cart for a child, with the loose
            // wheel lying in the air. Photographed in three turns beside a man's height, a
            // quarter turn about its axle is the one that reads as a wagon gone over.
            wagon.transform.rotation = Quaternion.Euler(WreckTip, rng.Range(0f, 360f), 0f);
            ModelScaling.Fit(wagon, WreckHeight, wy);

            Ground(wagon, wy);
            Mark(wagon);

            // <b>Something to walk round, at last.</b> Fifty-nine of these on twelve
            // levels and not one of them solid: a wagon on its side, two and a bit metres
            // of it, and the escort walked through the wreck as if it were grass. Not made
            // solid where every side of the heap is in the lane, which does happen on a
            // road that runs straight past the bones: a wreck the caravan drives through
            // is worse than one the escort does.
            if (!InTheRoad(grid, _road, wagon, wx, wz)) Block(wagon, canopy: false);

            _trapWrecks.Add(wagon);

            return placed + 1;
        }

        /// <summary>How many places round the bones a wreck is tried before it gives up.</summary>
        // Six, which is a turn every sixty degrees. More than that and two of them are the
        // same tile at this standoff.
        const int WreckTries = 6;

        /// <summary>
        /// How far the pieces of a bone pile lie from its middle, in metres.
        ///
        /// <b>Two and a half, which is a tile, and it was 1.3 for a reason that does not
        /// survive being measured.</b> The reason was that bones are where somebody fell
        /// and a heap wider than two metres stops being one thing that happened and
        /// becomes litter. True at eye level. The game is looked at from thirty-odd
        /// metres up at a slant, and photographed from there — see TrapReport.OnALevel —
        /// a 2.6 m scatter of pieces 41 cm tall is not litter or a heap, it is nothing at
        /// all: the only parts of a trap sign a player can actually see are the banner
        /// and the dead trees standing beside it.
        ///
        /// Nothing is scaled. What is wider is the ground it covers, so the patch reads
        /// as disturbed even when no single bone does — the same answer this file already
        /// reached once, that the cure is more of them rather than a bigger one, carried
        /// the rest of the way.
        /// </summary>
        public const float BonePileSpread = 2.5f;

        /// <summary>Pieces in a bone pile, and up to twice that.</summary>
        public const int BonePieces = 8;

        /// <summary>
        /// One bone prop out of a set that is mostly not bones, or null if it has none.
        ///
        /// Drawn rather than filtered, and given up on after a few tries: the ruins are
        /// five bones in nine, so a draw finds one almost always and the loop is cheap
        /// insurance against a set somebody later fills with carts.
        /// </summary>
        static GameObject Bones(PropSet set, DeterministicRandom rng)
        {
            if (set == null || !set.Any) return null;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                var candidate = Any(set, rng);
                if (IsBones(candidate)) return candidate;
            }

            return null;
        }

        /// <summary>
        /// Whether the thing standing at a trap site is remains rather than a wreck.
        ///
        /// By name, and that is worth defending because matching a third party's asset
        /// names usually is not. These are not a third party's choices: the set is
        /// written out prop by prop in this project's own <c>TheVeilSetup</c>, where two
        /// carts stand beside a skeleton, two skulls, a grave and a second skeleton from
        /// the generic pack. What is being read here is a list this repository keeps.
        ///
        /// The distinction earns its place on the map. A cart that stopped here is a
        /// mishap; bones are a killing, and the GDD's §5 table names bone piles as *the*
        /// trap-field tell. Drawing both as a broken cart threw away the more important
        /// of the two — and bones are the likelier draw of the set.
        /// </summary>
        static bool IsBones(GameObject prefab)
        {
            if (prefab == null) return false;

            string name = prefab.name;

            // Ribcage and carcass as well as the obvious four: the arid pack names its
            // heaps AD2_Ribcage_01 and AD2_BonePile_01, and a sign this did not recognise
            // as bones was noted on the planning map as a wreck and never got its heap.
            //
            // And a fossil, which is the mountains' own dead: the alpine pack draws a
            // spine and ribcage curled in the stone. Everything this test governs applies
            // to it - the map marks it as the trap tell, the sweeps leave it alone, the
            // wreck is laid beside it - and none of it would have, because the pack calls
            // bones in rock a fossil.
            return name.Contains("Skull") || name.Contains("Skeleton")
                || name.Contains("Bone") || name.Contains("Grave")
                || name.Contains("Ribcage") || name.Contains("Carcass")
                || name.Contains("Fossil");
        }

        /// <summary>How wide a loose piece of wreckage is, and how far it lies from the cart.</summary>
        /// <summary>
        /// How far the broken wagon lies from the bones at a trap, in metres.
        ///
        /// Three metres two. Not guessed: BonePileSpread throws bones 2.5 m from the
        /// middle and the wagon is 1.9 m long, so anything under three metres parks it
        /// inside the ribcage - which is what the first photograph showed it doing. A
        /// stray bone under a wheel is right; a wagon inside a ribcage is not.
        /// </summary>
        /// <summary>How far a bridge is bedded into its banks, in metres.</summary>
        public const float BridgeBed = 0.35f;

        public const float WreckStandoff = 3.2f;

        /// <summary>
        /// How tall a loose piece of a bone heap may be, in metres.
        ///
        /// Waist height, which is the line everything else in this file uses for what is
        /// walked over rather than round: a skull, a ribcage and a thighbone are all well
        /// under it, and anything over it is a body rather than a piece of one.
        /// </summary>
        const float BonePiece = 1.2f;

        /// <summary>
        /// How far a broken wagon is tipped over, in degrees about its axle.
        ///
        /// <b>Nought, and this is the third answer.</b> It went down as drawn and read as
        /// a cart for a child; it was scaled to a wagon's size and turned a quarter about
        /// its axle, on the reasoning that a wreck lies on its side; and what a quarter
        /// turn actually does to this model is stand it on its end with its wheels in the
        /// air, which is what a playtest reported.
        ///
        /// Measured at last, every quarter turn fitted to the height the decorator asks
        /// for (The Veil > Wreck Model): as drawn it covers 3.1 by 4.9 m of ground, and
        /// turned a quarter about x it covers 1.4 by 1.0 - a column rather than a wreck.
        /// The model is authored lying down with its top up and its loose wheel off, which
        /// is the thing itself. The yaw still turns: a wreck may lie any way round.
        /// </summary>
        const float WreckTip = 0f;

        /// <summary>
        /// How high a wreck lies, in metres.
        ///
        /// Two and a fifth: a wagon of the caravan stands 3.2 m (VisualLibrary.WagonHeight),
        /// and one on its side with a wheel off is about two thirds of that. Beside a man of
        /// 1.8 m it reads as something a man could shelter behind, which is what a wreck at
        /// the roadside is.
        /// </summary>
        public const float WreckHeight = 2.2f;

        public const float DebrisWidth = 1.3f;
        public const float DebrisSpread = 2.6f;

        /// <summary>
        /// How far the totem stands from the wreck it marks, in metres.
        ///
        /// The same reach the debris scatters over, so the banner stands at the edge of
        /// the site rather than outside it or in it. Wide enough to clear the largest
        /// thing it could be standing in front of — the hay cart, at 3.2 m — and near
        /// enough that the two still read as one piece of ground.
        /// </summary>
        public const float TotemStandoff = DebrisSpread;

        /// <summary>
        /// Tips a loose piece onto its side, whichever way it was modelled.
        ///
        /// Debris lies down. Which axis a prefab is thin along is the artist's business —
        /// a wheel may be authored upright in XY or lying in XZ — so it is measured: the
        /// shallowest axis is turned to point up, and the piece is re-seated on the
        /// ground afterwards because rotating about its centre moves its lowest point.
        /// </summary>
        static void LayFlat(GameObject instance, float groundY)
        {
            var size = ModelScaling.Measure(instance).size;

            if (size.y <= size.x && size.y <= size.z) return;   // already lying down

            instance.transform.rotation = size.x < size.z
                ? Quaternion.Euler(0f, 0f, 90f) * instance.transform.rotation
                : Quaternion.Euler(90f, 0f, 0f) * instance.transform.rotation;

            var seated = ModelScaling.Measure(instance);
            instance.transform.position += new Vector3(0f, groundY - seated.min.y, 0f);
        }

        /// <summary>
        /// How far a building is set into the ground here.
        ///
        /// Two parts, and the second is the one that was missing. A share of the model's
        /// own size buries the taper the artist put on its base — that is what
        /// <see cref="BuildingSink"/> is for, and on flat ground it is enough. On a slope
        /// it is not: a tower is seated by its lowest corner, so on a pass with three
        /// metres of fall across the tile the uphill side is left standing a metre and a
        /// half clear of the hill with daylight under it. The fall across the tile is
        /// added, so the building goes into the hill rather than onto it.
        ///
        /// The mountain passes are where the watchtowers go, and they are the steepest
        /// ground on the map. That is not a coincidence — it is why this was reported
        /// twice as towers standing on the grass.
        /// </summary>
        static float Seat(TileGrid grid, int tile, float heightScale, float size)
            => size * BuildingSink + Fall(grid, tile, heightScale) * SlopeSink;

        /// <summary>What share of a tile's own fall a building is sunk by, on top of its taper.</summary>
        public const float SlopeSink = 0.6f;

        /// <summary>
        /// How far a whole cottage is set into the ground, in metres.
        ///
        /// A hand's width, and it is there for one reason: a building laid exactly on the
        /// surface shows a line of daylight under its sill wherever the ground is not
        /// perfectly flat. It is not a share of the model's height, because a cottage has
        /// no taper and no foundation course to bury — see the note in Raise.
        /// </summary>
        public const float CottageSink = 0.12f;

        /// <summary>
        /// Builds whatever this tile has earned out of the kit, or nothing.
        ///
        /// Where they stand is unchanged and the reasons are the old ones: people build
        /// beside roads, towers watch the passes. Ruins are the new one and they go the
        /// other way — out in open country away from the road, because a ruin beside a
        /// living road reads as a building somebody would have repaired.
        /// </summary>
        static bool Built(Transform parent, TileGrid grid, DeterministicRandom rng,
                          BiomeDecor decor, int tile, float heightScale,
                          HashSet<int> occupied, HashSet<int> line,
                          HashSet<int> road = null, List<Landmark> found = null,
                          bool settled = true)
        {
            var kit = decor.Kit;
            var terrain = grid[tile];

            if (terrain == TerrainType.MountainPass && kit.CanBuildTower && rng.Chance(TowerChance))
                return Note(found, LandmarkKind.Watchtower, tile,
                            Raise(grid, tile, rng, BuildingBuilder.Tower(parent, kit, rng),
                                  TowerHeight, heightScale, occupied, road));

            grid.ToCoords(tile, out int x, out int y);

            // Somewhere a building could stand: open ground, near enough to the road the
            // caravan is taking to be *on* it, and flat enough to have been built on.
            // And whether anybody lives in this country at all (Settlements.Settled). A
            // kit house went up in the fen on 3-8 because the fen inherits the forest's
            // building kit and nothing asked whether a bog is somewhere to live. The
            // ruins below are not gated: what is left of a house is exactly what those
            // countries should have.
            bool plot = settled
                        && terrain == TerrainType.Plains
                        && Beside(grid, line, x, y, SettlementReach)
                        && Fall(grid, tile, heightScale) < BuildableFall;

            // The height follows what was actually stacked — see UpperStoreyRise. Asking
            // for the building first and its height second is the whole point: a cottage
            // and a two-storey house are the same call with a different die roll.
            if (plot && kit.CanBuildHouse && rng.Chance(HouseChance))
            {
                var house = BuildingBuilder.House(parent, kit, rng, out int storeys);
                return Note(found, LandmarkKind.House, tile,
                            Raise(grid, tile, rng, house,
                                  Storeys(HouseHeight, storeys > 1), heightScale, occupied, road));
            }

            if (plot && kit.CanBuildHouse && rng.Chance(FarmChance))
            {
                var farm = BuildingBuilder.House(parent, kit, rng, out int storeys);
                return Note(found, LandmarkKind.Farm, tile,
                            Raise(grid, tile, rng, farm,
                                  Storeys(FarmHeight, storeys > 1), heightScale, occupied, road));
            }

            // Ruins go the other way: out in the country, away from the line, because a
            // ruin beside a living road reads as a building somebody would have repaired.
            if ((terrain == TerrainType.Plains || terrain == TerrainType.Forest)
                && !Beside(grid, line, x, y, RuinExclusion)
                && kit.CanBuildRuin && rng.Chance(StoneRuinChance))
                return Note(found, LandmarkKind.Ruin, tile,
                            Raise(grid, tile, rng, BuildingBuilder.Ruin(parent, kit, rng),
                                  StoneRuinHeight, heightScale, occupied, road));

            return false;
        }

        /// <summary>
        /// Writes down a building that actually went up, and passes the answer through.
        ///
        /// Wrapped around <see cref="Raise"/> rather than called before it, because Raise
        /// can refuse — no room, or ground too steep — and a symbol on the map for a
        /// house that was never built is worse than no symbol at all.
        /// </summary>
        static bool Note(List<Landmark> found, LandmarkKind kind, int tile, bool raised)
        {
            if (raised) Landmark.Note(found, kind, tile);
            return raised;
        }

        /// <summary>
        /// Whether the caravan's road passes within <paramref name="radius"/> tiles.
        ///
        /// **This replaces asking the terrain for a road, and that is a bug fix and not a
        /// refactor.** Houses were placed on Road tiles and farms on plains beside them,
        /// and the generator lays no roads — it says so in LevelRecipe, in a comment about
        /// why. Every generated map in this project has exactly zero road tiles, measured
        /// across chapter one, so the whole settlement layer has been correct-looking dead
        /// code for its entire life: not one house or field has ever been placed.
        ///
        /// What is used instead is the generator's own corridors: the natural ways
        /// through this country, which both the planning map and the run can ask for and
        /// which do not move. The line the *player* drew would have been the other
        /// candidate and is wrong for a reason worth stating — redrawing the route would
        /// move the houses, and the country has to exist before anybody decides how to
        /// cross it.
        /// </summary>
        static bool Beside(TileGrid grid, HashSet<int> line, int x, int y, int radius)
        {
            if (line == null || line.Count == 0) return false;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (grid.InBounds(nx, ny) && line.Contains(grid.ToIndex(nx, ny))) return true;
                }
            }

            return false;
        }

        /// <summary>How often a road tile carries a house, a pass a tower, open country a ruin.</summary>
        /// <summary>
        /// Rates per qualifying tile, set from what they add up to rather than from what
        /// they sound like.
        ///
        /// Counted over chapter one: about 480 plains tiles lie within three of a corridor
        /// on a 64×64 map, 1750 sit out in the country beyond them, and 320 are pass. At
        /// two percent each that came to <b>nineteen buildings a level</b> — a town, on a
        /// road that goes through the provinces. Eight parts in a thousand gives three or
        /// four buildings along the way, which is what a day's travel should pass.
        ///
        /// The others were already about right and stay: four towers on the passes, two
        /// or three ruins in the empty country.
        /// </summary>
        public const float HouseChance = 0.008f;
        public const float FarmChance = 0.008f;
        public const float TowerChance = 0.012f;
        public const float StoneRuinChance = 0.0015f;

        /// <summary>
        /// How far from the caravan's road a building may still be said to be on it.
        ///
        /// <b>Five, and it had to move when the lane test arrived.</b> A building is now
        /// refused when its *body* reaches the caravan's swept lane rather than only when
        /// the tile under its middle does — which is what stopped the column driving
        /// through the watchtower. But a house is required to stand near the road, and at
        /// three tiles the two rules were fighting: measured over chapter 1, the plains
        /// tiles that satisfied both fell from 1101 to 373, a third of what there was. At
        /// eight parts in a thousand per tile that is the difference between three or four
        /// buildings on a level and none, and on 1-2 the count went from 44 sites to 13.
        ///
        /// Five tiles restores the supply — 1225 sites, slightly more than before — with
        /// every one of them far enough back that the building stands clear of the lane.
        /// The houses move back rather than away.
        /// </summary>
        public const int SettlementReach = 5;

        /// <summary>
        /// How far from the caravan's line a ruin has to be to be out in the country.
        ///
        /// Held at the three tiles SettlementReach used to be, rather than following it
        /// out to five. The two numbers were one number because they meant the same
        /// thing; they stopped meaning the same thing when the settlement reach was
        /// widened to make room for a house's body, and dragging the ruins out with it
        /// would have thinned them for a reason that has nothing to do with ruins.
        /// </summary>
        public const int RuinExclusion = 3;

        /// <summary>Most a tile may fall across before nobody would have built on it.</summary>
        public const float BuildableFall = 1.6f;

        /// <summary>A farmhouse stands lower and broader than a village house.</summary>
        public const float FarmHeight = 5.5f;

        /// <summary>How tall a built castle tower stands, and a stone ruin.</summary>
        /// <summary>
        /// How tall a tower stands out of the kit, in metres before the multiplier.
        ///
        /// <b>Eleven, which is what the prefab watchtower has always been.</b> Two
        /// constants named the same building and drifted apart: this one was fifteen, and
        /// the run multiplies every landmark by LandmarkScale, so the kit tower went up at
        /// twenty-four metres — seven and a half wagons, two and a half times a house, and
        /// half again a prefab watchtower standing on the next pass. A cathedral, not a
        /// lookout.
        ///
        /// At eleven both towers come out at 17.6 m in the run: above a two-storey house
        /// at 14.4 and well below the castle at 35.2, which is the order they should be in.
        /// </summary>
        public const float TowerHeight = WatchtowerHeight;
        public const float StoneRuinHeight = 4.5f;

        /// <summary>
        /// Puts an assembled building on the ground: turned square, seated into the slope,
        /// scaled to the size the level wants and marked as something to walk round.
        ///
        /// The building arrives already stacked and standing at the origin (see
        /// <see cref="BuildingBuilder"/>), which is why this is a separate step from
        /// <see cref="Place"/>: one instantiates a prefab and the other finishes a thing
        /// that was built out of several.
        /// </summary>
        /// <param name="yaw">
        /// Which way it faces, or -1 to let it fall on a random quarter turn.
        ///
        /// Every building here may point wherever it likes except one: a castle's gate
        /// has to face the road, or the caravan arrives at a wall.
        /// </param>
        /// <summary>
        /// How wide one length of town wall is drawn, in metres.
        ///
        /// One tile, because the wall is made of tiles: each impassable tile the stamp
        /// laid down gets one piece, fitted to the tile's width rather than to a height,
        /// so the run closes instead of coming out as a row of posts with daylight
        /// between them. Fitting a wall by height is what leaves the gaps — the piece is
        /// as long as the model happens to be, and the model was not authored to the
        /// grid.
        /// </summary>
        const float TownWallSpan = TileGrid.TileSize;

        /// <summary>
        /// How tall a town wall stands, in metres.
        ///
        /// Fitted by height rather than to the tile, and the pieces overlap because of
        /// it. Fitted to the tile instead — four metres wide, which is what closes the
        /// run — a curtain piece authored ten metres by six comes out two and a half
        /// metres tall, and a wall a man can see over is a garden wall. Overlapping
        /// stone reads as thickness; a low wall reads as a mistake.
        /// </summary>
        const float TownWallHeight = 7f;

        /// <summary>How tall a corner tower and a gatehouse stand, in metres.</summary>
        const float TownTowerHeight = 10f;
        const float TownGateHeight = 9f;

        /// <summary>
        /// Builds the walled town, if this level has one.
        ///
        /// The walls themselves were laid as ground before any of this ran (Towns.Stamp):
        /// impassable tiles that the corridors and the player's own drawn line have to go
        /// round or through a gate. What happens here is only that they are made visible.
        /// The two cannot disagree, because this reads the same plan the stamp wrote and
        /// puts a stone on every tile the stamp made impassable.
        ///
        /// <b>Which is why the gates are holes rather than arches parked against a wall.</b>
        /// The stamp leaves the gate tiles passable and the wall run short a piece there;
        /// the gatehouse is then stood in the hole. A gate drawn over a solid wall would
        /// be a door nobody can open.
        /// </summary>
        static int PlaceTown(Transform parent, TileGrid grid, DeterministicRandom rng,
                             BiomeDecor decor, HashSet<int> occupied, float heightScale,
                             Towns.Plan town, List<Landmark> found, HashSet<int> road)
        {
            if (!town.Any) return 0;

            var kit = decor.Kit;
            if (kit == null || !kit.CurtainWalls.Any) return 0;

            // One style for the whole circuit. A town wall built of three different
            // stones reads as three towns.
            var stone = Any(kit.CurtainWalls, rng);
            int placed = 0;

            // The corners first, and the order is the whole of it.
            //
            // Towers and wall ran in one pass over the rows, so the north wall was laid
            // before the loop reached the north-east corner — and a wall piece beside a
            // corner has already spoken for that ground, so the tower was turned away.
            // One tower of four stood, which is a castle with three corners.
            if (kit.CanBuildTower)
            {
                foreach (var corner in new[] { (town.West, town.North), (town.East, town.North),
                                               (town.West, town.South), (town.East, town.South) })
                {
                    int tile = grid.ToIndex(corner.Item1, corner.Item2);

                    if (Raise(grid, tile, rng, BuildingBuilder.Tower(parent, kit, rng),
                              TownTowerHeight, heightScale, occupied, null, 0f, landmark: false))
                    {
                        Landmark.Note(found, LandmarkKind.Watchtower, tile);
                        placed++;
                    }
                }
            }

            for (int y = town.North; y <= town.South; y++)
            {
                for (int x = town.West; x <= town.East; x++)
                {
                    // The face of the wall only. Its outer ring is the thickness of the
                    // stone, not a second wall to build.
                    if (!town.IsFace(x, y)) continue;

                    int tile = grid.ToIndex(x, y);

                    // The corners already carry their towers, raised before any of this,
                    // and so do the two tiles flanking each gateway.
                    if ((x <= town.West + 1 || x >= town.East - 1)
                        && (y <= town.North + 1 || y >= town.South - 1)) continue;

                    if ((x <= town.West + 1 || x >= town.East - 1)
                        && System.Math.Abs(y - town.GateRow) == Towns.GateHalf + 1) continue;

                    // The face this piece stands on decides which way it looks. North and
                    // south walls run east to west; the side walls run north to south.
                    float yaw = y == town.North || y == town.South ? 0f : 90f;

                    if (Scatter(parent, grid, rng,
                                new Choice(kit.CurtainWalls, stone, TownWallHeight, byWidth: false,
                                           low: 1f, high: 1f, maxSpread: 3f),
                                tile, heightScale, spread: 0f, occupied, yaw: yaw))
                        placed++;
                }
            }

            // The gateways: a tower on each side and nothing across the opening.
            //
            // <b>There is no model for a gatehouse this wide, and there should not be.</b>
            // The pack's gate is a wall panel with an arch in it, 5,4 m by 5,1 — the same
            // piece as a plain wall with a hole. Stretched across a twenty-metre opening it
            // becomes a triumphal arch twenty-five metres tall; laid in a row across it,
            // the piers between the arches stand in the road. Either way the caravan drives
            // through stone, which is the fault this was meant to fix.
            //
            // The column is sixteen metres wide (TerrainDecorator.DriveHalfWidth), so the
            // opening has to be wider than that and empty. Two towers flanking a gap is
            // what a city gate of that size actually is — the wall stops, the towers say
            // where, and the doors it once had are open.
            if (kit.CanBuildTower)
            {
                foreach (int side in new[] { town.West + 1, town.East - 1 })
                {
                    foreach (int step in new[] { -1, 1 })
                    {
                        int y = town.GateRow + step * (Towns.GateHalf + 1);
                        if (y <= town.North + 1 || y >= town.South - 1) continue;

                        int tile = grid.ToIndex(side, y);

                        if (Raise(grid, tile, rng, BuildingBuilder.Tower(parent, kit, rng),
                                  TownTowerHeight, heightScale, occupied, null, 0f, landmark: false))
                        {
                            Landmark.Note(found, LandmarkKind.Castle, tile);
                            placed++;
                        }
                    }
                }
            }


            placed += PavementOf(parent, grid, rng, decor, occupied, heightScale, town);
            placed += PlaceTownHouses(parent, grid, rng, decor, occupied, heightScale, town, found, road);
            placed += PlaceTownStreets(parent, grid, rng, decor, occupied, heightScale, town, road);

            return placed;
        }

        /// <summary>
        /// Lays the town's streets with stone.
        ///
        /// <b>The ground was painted rather than laid.</b> Every surface in this game is
        /// vertex colour on the terrain mesh, which is right for meadow and forest floor
        /// and poor for a city: a town's street is a made thing, and the pack ships the
        /// pieces to make it with — cobble, flag and dressed stone under Environments,
        /// none of which this project had ever loaded. Painting it grey was the cheap half
        /// of the job.
        ///
        /// Flat, so it goes on the street rather than beside it. Everything else in the
        /// town stands on the block because the caravan may drive any tile of any street;
        /// paving is the exception that proves the rule, because driving over a paving
        /// stone is what a paving stone is for.
        ///
        /// Laid before the scatter, and it claims its tiles, so the streets also stop
        /// growing grass — which they had been doing, in tufts, down the middle of a city.
        /// </summary>
        static int PavementOf(Transform parent, TileGrid grid, DeterministicRandom rng,
                              BiomeDecor decor, HashSet<int> occupied, float heightScale,
                              Towns.Plan town)
        {
            if (!decor.Paving.Any) return 0;

            int laid = 0;

            for (int y = town.North + 1; y < town.South; y++)
            {
                for (int x = town.West + 1; x < town.East; x++)
                {
                    if (!grid.IsPassable(x, y)) continue;

                    int tile = grid.ToIndex(x, y);

                    // Fitted to the tile across, so the street is continuous stone rather
                    // than a row of mats with ground showing between them, and turned in
                    // quarter turns so the pattern does not repeat down the whole street.
                    if (Scatter(parent, grid, rng,
                                // Bedded into the ground rather than laid on it: only the top
                                // of a paving stone is meant to show.
                                new Choice(decor.Paving, Any(decor.Paving, rng),
                                           TileGrid.TileSize, byWidth: true, low: 1f, high: 1f,
                                           sink: 0.9f),
                                tile, heightScale, spread: 0f, occupied,
                                yaw: rng.Range(0, 4) * 90f))
                        laid++;
                }
            }

            return laid;
        }

        /// <summary>
        /// What stands along a town's streets: carts, hay, barrels, and a well at a corner.
        ///
        /// <b>On the block, never on the street.</b> Every street in the town is one of the
        /// three ways through the level and the player draws their own line down it, so
        /// there is no lane of a street that is safe to stand in: the first version put
        /// these in the gutter and the caravan walked through a well. Furniture stands on
        /// the block instead, at its edge, facing the street — impassable ground, where
        /// nothing can ever be in the way.
        ///
        /// Spaced along the street rather than scattered over it. A cart every few strides
        /// is a market; a cart on every tile is a barricade.
        /// </summary>
        static int PlaceTownStreets(Transform parent, TileGrid grid, DeterministicRandom rng,
                                    BiomeDecor decor, HashSet<int> occupied, float heightScale,
                                    Towns.Plan town, HashSet<int> road)
        {
            int placed = 0;

            for (int y = town.North + 2; y < town.South - 1; y++)
            {
                for (int x = town.West + 2; x < town.East - 1; x++)
                {
                    int tile = grid.ToIndex(x, y);

                    // <b>On the block, never on the street.</b> This stood things in the
                    // gutter — a street tile with building on one side — and the caravan
                    // walked straight through a well, because the player draws their own
                    // line and every tile of a street is a tile they may drive. There is
                    // no safe lane on a road somebody else chooses.
                    //
                    // Block is impassable ground, so nothing placed on it can ever be in
                    // anybody's way; standing it at the block's edge puts it against the
                    // street where it is seen. A cart at the kerb rather than in the road.
                    if (grid[tile] != TerrainType.Cliff) continue;
                    if (occupied != null && occupied.Contains(tile)) continue;

                    // And not where the column will pass. A cart at the kerb leans over
                    // the street exactly as a house does.
                    if (road != null && road.Contains(tile)) continue;

                    bool northStreet = grid.InBounds(x, y - 1) && grid.IsPassable(x, y - 1);
                    bool southStreet = grid.InBounds(x, y + 1) && grid.IsPassable(x, y + 1);
                    bool westStreet = grid.InBounds(x - 1, y) && grid.IsPassable(x - 1, y);
                    bool eastStreet = grid.InBounds(x + 1, y) && grid.IsPassable(x + 1, y);

                    if (!northStreet && !southStreet && !westStreet && !eastStreet) continue;

                    if ((x - town.West) % StreetFurniture != 0) continue;
                    if (!rng.Chance(0.72f)) continue;

                    // A well where two ways meet, a lamp or a brazier on the kerb, a tree
                    // in a corner of the block, and a cart or a load of hay everywhere
                    // else. The well is the rarer thing and the one a town is built round,
                    // so it is not on every corner.
                    //
                    // Trees inside the walls on purpose: a town with no green in it reads
                    // as a barracks, and the ones that grow in a town grow in the gaps
                    // between buildings — which is exactly the ground this is walking.
                    float roll = rng.Value01();

                    bool wellHere = roll < 0.10f && decor.Houses.Any;
                    bool lampHere = !wellHere && roll < 0.34f && decor.Street.Any;
                    bool treeHere = !wellHere && !lampHere && roll < 0.56f && decor.Trees.Any;

                    var set = wellHere ? decor.Houses
                            : lampHere ? decor.Street
                            : treeHere ? decor.Trees
                            : rng.Chance(0.66f) && decor.Yard.Any ? decor.Yard
                            : decor.Wreckage;

                    if (set == null || !set.Any) continue;

                    float size = wellHere ? WellHeight
                               : lampHere ? LampHeight
                               : treeHere ? rng.Range(5.5f, 8.5f)
                               : YardHeight;

                    if (Scatter(parent, grid, rng,
                                // Never canopy, not even for the trees. Canopy is the rule that
                                // lets a wood look like a wood — a spruce skips the ground
                                // check so crowns may touch — and in a town it means a tree
                                // planted through a roof. A town tree asks like everything
                                // else here and is refused where a house stands.
                                new Choice(set, Any(set, rng), size, byWidth: false),
                                tile, heightScale, spread: 0.8f, occupied, solid: true))
                        placed++;
                }
            }

            return placed;
        }

        /// <summary>How far apart street furniture is set along a street, in tiles.</summary>
        const int StreetFurniture = 3;

        /// <summary>How tall a lamp post or a brazier stands, in metres.</summary>
        const float LampHeight = 3.4f;

        /// <summary>How tall the town's one monument stands, in metres.</summary>
        const float MonumentHeight = 5.5f;

        /// <summary>
        /// The town inside its walls: building on every block, facing the streets.
        ///
        /// The stamp lays the interior solid and cuts two streets out of it, so what is
        /// left is block — and a block is where a town's buildings are. Rather than name
        /// rows, which went stale the first time the layout changed, this walks the
        /// impassable ground inside the walls and builds on it: a house every third tile,
        /// turned to face the nearest street where there is one to face.
        ///
        /// <b>A town is not a village with more houses in it.</b> The village stands in a
        /// ring round a well with its ground cleared about it; here the ground is spoken
        /// for by the walls, and what makes it a town is that the buildings are shoulder
        /// to shoulder along a street with no gap to see the country through.
        /// </summary>
        static int PlaceTownHouses(Transform parent, TileGrid grid, DeterministicRandom rng,
                                   BiomeDecor decor, HashSet<int> occupied, float heightScale,
                                   Towns.Plan town, List<Landmark> found, HashSet<int> road)
        {
            if (decor.Kit == null || !decor.Kit.CanBuildHouse) return 0;

            int placed = 0;

            for (int y = town.North + 1; y < town.South; y++)
            {
                for (int x = town.West + 1; x < town.East; x++)
                {
                    if (grid[grid.ToIndex(x, y)] != TerrainType.Cliff) continue;

                    // Every third tile each way, and not every one of those: a town laid
                    // out on an exact grid reads as barracks. A house in six is left out,
                    // and the row's own offset shifts along the street, so the frontages
                    // step in and out the way they do when each plot was built by whoever
                    // owned it.
                    int step = 3;
                    int offset = (y * 2) % step;

                    if ((x - town.West + offset) % step != 0 || (y - town.North) % step != 0) continue;
                    if (rng.Chance(0.17f)) continue;

                    // Turned to whichever side has a street on it. A house with its back
                    // to the road is a house nobody uses; one in the middle of a block
                    // takes the turn of the die.
                    bool northStreet = y > town.North && grid.IsPassable(x, y - 1);
                    bool southStreet = !northStreet && y < town.South && grid.IsPassable(x, y + 1);
                    bool westStreet = !northStreet && !southStreet && x > town.West && grid.IsPassable(x - 1, y);
                    bool eastStreet = !northStreet && !southStreet && !westStreet
                                      && x < town.East && grid.IsPassable(x + 1, y);

                    float yaw = northStreet ? 0f
                              : southStreet ? 180f
                              : westStreet ? 270f
                              : eastStreet ? 90f
                              : rng.Range(0, 4) * 90f;

                    var house = BuildingBuilder.House(parent, decor.Kit, rng, out int storeys);

                    // <b>Asked about the road, which it never used to be.</b> A house stands
                    // on impassable ground, so no route crosses its tile — and the column
                    // is wider than its tile. These are seven and a half metres across on
                    // a four-metre tile, so each one leans nearly two metres over the
                    // street it faces, and the caravan drove through the gables on every
                    // way but the middle of the main street. Raise turns down a building
                    // that stands in the road; it was being handed no road to check.
                    // Set back from the street it faces by half its own overhang, so its
                    // front lands on the plot line rather than out in the road.
                    //
                    // Two cases the setback cannot answer, and they are the twenty-two
                    // that were left over when it did. A house with no street against it
                    // at all is in the middle of a block and is not nudged anywhere: its
                    // overhang lands on its neighbours' ground, which is what a terrace
                    // is. A house with street on two sides is a corner plot, and a corner
                    // plot is small — it is set back from one street and cut down to fit
                    // the other.
                    int streets = (northStreet ? 1 : 0) + (southStreet ? 1 : 0)
                                + (westStreet ? 1 : 0) + (eastStreet ? 1 : 0);

                    bool corner = grid.IsPassable(x, y - 1) && grid.IsPassable(x - 1, y)
                               || grid.IsPassable(x, y - 1) && grid.IsPassable(x + 1, y)
                               || grid.IsPassable(x, y + 1) && grid.IsPassable(x - 1, y)
                               || grid.IsPassable(x, y + 1) && grid.IsPassable(x + 1, y);

                    float width = corner ? TileGrid.TileSize * 0.95f : TownHouseWidth;
                    float setback = (width - TileGrid.TileSize) * 0.5f;

                    var nudge = streets == 0 ? Vector3.zero
                              : northStreet ? new Vector3(0f, 0f, setback)
                              : southStreet ? new Vector3(0f, 0f, -setback)
                              : westStreet ? new Vector3(setback, 0f, 0f)
                              : new Vector3(-setback, 0f, 0f);

                    if (!Raise(grid, grid.ToIndex(x, y), rng, house, storeys * StoreyHeight,
                               heightScale, occupied, road, yaw, landmark: false,
                               maxWidth: width, nudge: nudge))

                        continue;

                    Landmark.Note(found, LandmarkKind.House, grid.ToIndex(x, y));
                    placed++;
                }
            }

            // And the monument: one to a town, on the open ground inside a gate where
            // there is room to stand and look at it.
            if (decor.Monuments.Any)
            {
                int at = grid.ToIndex(town.West + 3, town.GateRow + 2);

                if (grid.InBounds(town.West + 3, town.GateRow + 2)
                    && grid[at] == TerrainType.Cliff
                    && Scatter(parent, grid, rng,
                               new Choice(decor.Monuments, Any(decor.Monuments, rng),
                                          MonumentHeight, byWidth: false, low: 1f, high: 1f),
                               at, heightScale, spread: 0f, occupied, solid: true))
                    placed++;
            }

            return placed;

        }

        /// <summary>How many houses stand in a village.</summary>
        const int VillageLow = 12;
        const int VillageHigh = 19;

        /// <summary>
        /// How far the houses stand from the village's middle, in metres.
        ///
        /// A ring rather than a row, and a loose one: the well is the middle of a village
        /// because that is what everybody walks to, and the houses face it. Near enough
        /// that the whole place reads as one settlement from the caravan's height, far
        /// enough that two of them are not one building with two roofs.
        /// </summary>
        const float VillageNear = 14f;
        const float VillageFar = 24f;

        /// <summary>
        /// How far the second row of houses stands out, in metres.
        ///
        /// Two rings rather than one, because one ring of six houses is a hamlet and a
        /// village is a place. Alternating between them as the ring is walked puts the
        /// rows out of step with each other, so the outer houses stand in the gaps of the
        /// inner ones and the place has depth from the road rather than reading as a
        /// circle of buildings around a green.
        /// </summary>
        const float VillageOuter = 37f;

        /// <summary>How tall the well in the middle stands.</summary>
        const float WellHeight = 2.6f;

        /// <summary>
        /// How tall a village house stands, in metres.
        ///
        /// The pack draws its cottages about five metres to the ridge, so this is very
        /// nearly life size and the scaling is very nearly nothing. HouseHeight, which
        /// this replaces here, is seven — and seven was chosen back when a house was
        /// four stacked pieces and the number had to cover all of them. One cottage
        /// stretched to seven metres is a cottage with a two-storey door.
        /// </summary>
        /// <summary>
        /// How tall one storey of a settlement building stands, in metres.
        ///
        /// The house is fitted to this times the number of storeys it came out with, so a
        /// cottage is four metres to the ridge and a three-storey town house is twelve.
        /// Fitting both to one number is what squashes the tall ones and stretches the
        /// small ones into the same barracks.
        /// </summary>
        const float StoreyHeight = 4.2f;

        /// <summary>
        /// How wide a town house is allowed to be, in metres.
        ///
        /// Seven, which is wider than the four-metre plot it stands on and narrower than
        /// the eight and a quarter the general cap allows. The overhang is dealt with by
        /// setting the house back rather than by shrinking it: squeezed inside its plot a
        /// cottage came out under four metres tall and the wells beside it looked bigger
        /// than the houses.
        /// </summary>
        const float TownHouseWidth = 7f;

        /// <summary>The sizes of the small things a village is furnished with, in metres.</summary>
        const float ShedHeight = 3.2f;
        const float YardHeight = 1.9f;
        const float FenceHeight = 1.3f;
        const float MillWheelHeight = 5.5f;
        const float BoatLength = 3.4f;

        /// <summary>How far apart fence posts are set along a run, in metres.</summary>
        const float FenceStep = 2.6f;

        /// <summary>How far from the village a mill will look for water, in tiles.</summary>
        const int MillReach = 10;

        /// <summary>
        /// Builds the village, if this level has one.
        ///
        /// <b>Why a village exists at all as its own thing.</b> Houses were placed by the
        /// same die roll as every other landmark — plains, near a way through, flat, and
        /// then eight chances in a thousand — which put about one house on a level, alone
        /// in a field. Nobody lives alone in a field on a road with bandits on it; that
        /// reads as a house somebody left, and it was not meant to. Settlements picks one
        /// site per level instead, and everything that makes a village is built around it.
        ///
        /// <b>And why it is more than houses.</b> Five buildings round a well is a
        /// building site. What says people live here is the ground between them being
        /// used: a fence round each plot, a cart standing where it was left, a shed
        /// against a gable, and — where the village has water — a mill wheel turning in
        /// it and a boat tied up beside. Every one of those is placed against a house or
        /// against the water rather than scattered, because a prop that only reads as
        /// part of something has to be placed as part of something.
        ///
        /// Houses that will not fit are simply not built. The ring is a wish, not a plan:
        /// a tree, a rock or the road itself may already hold the ground a house wanted,
        /// and Raise turns all three down on its own. A village of four is still a
        /// village; a house standing in the road is not.
        /// </summary>
        static int PlaceVillage(Transform parent, TileGrid grid, DeterministicRandom rng,
                                BiomeDecor decor, HashSet<int> occupied, float heightScale,
                                int site, HashSet<int> road, List<Landmark> found)
        {
            if (site < 0 || site >= grid.TileCount) return 0;
            if (decor.Kit == null || !decor.Kit.CanBuildHouse) return 0;

            var middle = Vec2.FromTile(grid, site);
            int placed = 0;


            // The well first, in the middle, because it is what the houses are turned
            // towards and the one piece whose position is not negotiable.
            //
            // Scattered rather than placed, and that is not a detail. Place is the
            // landmark path and it lifts anything under the plan's legibility floor — a
            // well is 2,6 m, which is under it, so the first village had a well the size
            // of a house standing in the middle of the houses. A well is life-size or it
            // is not a well.
            if (decor.Houses.Any
                && Scatter(parent, grid, rng,
                           new Choice(decor.Houses, Any(decor.Houses, rng), WellHeight,
                                      byWidth: false),
                           site, heightScale, spread: 0f, occupied))
            {
                Landmark.Note(found, LandmarkKind.House, site);
                placed++;
            }

            int houses = rng.Range(VillageLow, VillageHigh);

            // The ring is walked from a random bearing so the gap the road makes through
            // it falls somewhere different on every level.
            float turn = rng.Range(0f, 360f);

            // Where the houses actually landed, which is not where they were wanted: the
            // fences and the yards are strung between the ones that stand.
            var standing = new List<Vector2>();

            for (int i = 0; i < houses; i++)
            {
                float bearing = (turn + i * 360f / houses + rng.Range(-9f, 9f)) * Mathf.Deg2Rad;

                // Every other house in the outer row. See VillageOuter.
                float reach = i % 2 == 1
                    ? rng.Range(VillageFar, VillageOuter)
                    : rng.Range(VillageNear, VillageFar);

                float x = middle.X + Mathf.Cos(bearing) * reach;
                float z = middle.Y + Mathf.Sin(bearing) * reach;

                int tile = Tile(grid, x, z);
                if (tile < 0) continue;

                var terrain = grid[tile];
                if (terrain == TerrainType.Water || terrain == TerrainType.Ford
                    || terrain == TerrainType.Cliff) continue;

                var house = BuildingBuilder.House(parent, decor.Kit, rng, out int storeys);

                // Turned to the well, in quarter turns like every other building: the
                // bearing decides which of the four it gets, so a house on the east side
                // of the ring faces west and the village has a middle rather than a
                // scatter of buildings all facing the same way.
                float yaw = Mathf.Round((bearing * Mathf.Rad2Deg + 180f) / 90f) * 90f;

                if (!Raise(grid, tile, rng, house, storeys * StoreyHeight,
                           heightScale, occupied, road, yaw, landmark: false))
                    continue;

                Landmark.Note(found, LandmarkKind.House, tile);
                placed++;

                var at = Vec2.FromTile(grid, tile);
                standing.Add(new Vector2(at.X, at.Y));

                placed += Yard(parent, grid, rng, decor, occupied, heightScale, road,
                               at, bearing);
            }

            placed += Fences(parent, grid, rng, decor, occupied, heightScale, road,
                             new Vector2(middle.X, middle.Y), standing);

            placed += Mill(parent, grid, rng, decor, occupied, heightScale, road, site, found);

            // And the yard is spoken for last, which keeps the wood out of it.
            //
            // <b>A village does not have a forest growing through it.</b> Each building
            // claims the tiles under itself, which is enough to keep two of them apart
            // and nowhere near enough to make a village: the ground between the houses
            // was left open, so the scatter sowed it like any other meadow and spruces
            // came up on the green, against the doors and around the well. What was
            // built read as houses dropped into a wood rather than as somewhere people
            // had cleared and settled.
            //
            // Last rather than first, and that order is the whole of it: claimed before
            // the houses go up, the yard turns away the houses themselves. Claimed after,
            // it turns away only what comes later, and what comes later is the scatter.
            // Out to the ring and one tile past it, not to Settlements.Yard: the yard is
            // the plot the site was chosen by and it is three tiles, while the houses
            // stand between fifteen and twenty-six metres out. Clearing the yard alone
            // left the middle open and the wood standing between the houses, which is
            // where it shows.
            grid.ToCoords(site, out int yx, out int yy);
            int clearing = Mathf.CeilToInt(VillageOuter / TileGrid.TileSize) + 1;

            for (int dy = -clearing; dy <= clearing; dy++)
                for (int dx = -clearing; dx <= clearing; dx++)
                {
                    int nx = yx + dx, ny = yy + dy;
                    if (grid.InBounds(nx, ny)) occupied?.Add(grid.ToIndex(nx, ny));
                }

            return placed;
        }

        /// <summary>
        /// What stands in a house's yard: a shed against the gable, a cart in front.
        ///
        /// Placed against the house rather than near it. <paramref name="bearing"/> is the
        /// direction from the village's middle out to this house, so the far side of the
        /// house is where the shed goes and the near side, towards the well, is where the
        /// cart stands — which is where a cart would be left, facing the way out.
        /// </summary>
        static int Yard(Transform parent, TileGrid grid, DeterministicRandom rng,
                        BiomeDecor decor, HashSet<int> occupied, float heightScale,
                        HashSet<int> road, Vec2 house, float bearing)
        {
            int placed = 0;

            if (decor.Sheds.Any && rng.Chance(0.55f))
            {
                float side = bearing + Mathf.PI * 0.5f;
                int tile = Tile(grid, house.X + Mathf.Cos(side) * 7f,
                                      house.Y + Mathf.Sin(side) * 7f);

                if (tile >= 0 && Scatter(parent, grid, rng,
                                         new Choice(decor.Sheds, Any(decor.Sheds, rng), ShedHeight,
                                                    byWidth: false),
                                         tile, heightScale, spread: 1f, occupied))
                    placed++;
            }

            if (decor.Yard.Any && rng.Chance(0.7f))
            {
                int tile = Tile(grid, house.X - Mathf.Cos(bearing) * 6f,
                                      house.Y - Mathf.Sin(bearing) * 6f);

                if (tile >= 0 && (road == null || !road.Contains(tile))
                    && Scatter(parent, grid, rng,
                               new Choice(decor.Yard, Any(decor.Yard, rng), YardHeight,
                                          byWidth: false),
                               tile, heightScale, spread: 1.2f, occupied))
                    placed++;
            }

            return placed;
        }

        /// <summary>
        /// Fences from house to house, which is what turns a ring of buildings into
        /// plots.
        ///
        /// Strung along the outside of the ring between neighbours, at a fixed step, and
        /// each post turned to the run it belongs to. Anything the road wants, the road
        /// gets: a fence across the way in is a fence across the way in.
        /// </summary>
        static int Fences(Transform parent, TileGrid grid, DeterministicRandom rng,
                          BiomeDecor decor, HashSet<int> occupied, float heightScale,
                          HashSet<int> road, Vector2 middle, List<Vector2> houses)
        {
            if (!decor.Fences.Any || houses.Count < 2) return 0;

            int placed = 0;

            for (int i = 0; i < houses.Count; i++)
            {
                var from = houses[i];
                var to = houses[(i + 1) % houses.Count];

                // Only between neighbours that are actually neighbours. Two houses on
                // opposite sides of the village are not a plot boundary, they are a line
                // drawn through the middle of the green.
                float span = Vector2.Distance(from, to);
                if (span > 34f) continue;

                // Bowed outwards, away from the well, so the fence runs round the plots
                // rather than cutting the corner across them.
                var mid = (from + to) * 0.5f;
                var out0 = (mid - middle).normalized * 4f;

                float yaw = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

                for (float t = FenceStep * 0.5f; t < span; t += FenceStep)
                {
                    float k = t / span;

                    // A hair of bow: full at the middle of the run, none at its ends.
                    var bow = out0 * Mathf.Sin(k * Mathf.PI);
                    var at = Vector2.Lerp(from, to, k) + bow;

                    int tile = Tile(grid, at.x, at.y);
                    if (tile < 0) continue;
                    if (road != null && road.Contains(tile)) continue;

                    var terrain = grid[tile];
                    if (terrain == TerrainType.Water || terrain == TerrainType.Ford
                        || terrain == TerrainType.Cliff) continue;

                    if (Scatter(parent, grid, rng,
                                new Choice(decor.Fences, Any(decor.Fences, rng), FenceHeight,
                                           byWidth: false, low: 1f, high: 1f),
                                tile, heightScale, spread: 0f, occupied: null, yaw: -yaw))
                        placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// The mill, where the village has water to turn one.
        ///
        /// Three pieces and one thing: the wheel in the water, the frame it turns in on
        /// the bank, and the mill house behind that. Placed together or not at all — a
        /// wheel on its own is a wheel in a river, which is what the cart wheel taught
        /// (see Wreckage). A boat is tied up beside it where there is room.
        /// </summary>
        static int Mill(Transform parent, TileGrid grid, DeterministicRandom rng,
                        BiomeDecor decor, HashSet<int> occupied, float heightScale,
                        HashSet<int> road, int site, List<Landmark> found)
        {
            if (!decor.Mills.Any) return 0;

            grid.ToCoords(site, out int sx, out int sy);

            // The nearest water with dry ground beside it, which is where a mill goes.
            int bank = -1, water = -1, nearest = int.MaxValue;

            for (int dy = -MillReach; dy <= MillReach; dy++)
                for (int dx = -MillReach; dx <= MillReach; dx++)
                {
                    int wx = sx + dx, wy = sy + dy;
                    if (!grid.InBounds(wx, wy)) continue;
                    if (grid[wx, wy] != TerrainType.Water) continue;

                    int reach = dx * dx + dy * dy;
                    if (reach >= nearest) continue;

                    // A bank beside it, towards the village.
                    foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int bx = wx + step.Item1, by = wy + step.Item2;
                        if (!grid.InBounds(bx, by)) continue;

                        var terrain = grid[bx, by];
                        if (terrain == TerrainType.Water || terrain == TerrainType.Ford
                            || terrain == TerrainType.Cliff) continue;

                        nearest = reach;
                        water = grid.ToIndex(wx, wy);
                        bank = grid.ToIndex(bx, by);
                        break;
                    }
                }

            if (bank < 0) return 0;

            grid.ToCoords(water, out int ax, out int ay);
            grid.ToCoords(bank, out int bx2, out int by2);

            // The wheel faces along the bank, which is across the line from bank to water.
            float yaw = Mathf.Atan2(ay - by2, ax - bx2) * Mathf.Rad2Deg;
            int placed = 0;

            if (!Scatter(parent, grid, rng,
                         new Choice(decor.Mills, Any(decor.Mills, rng), MillWheelHeight,
                                    byWidth: false, low: 1f, high: 1f),
                         water, heightScale, spread: 0f, occupied: null, yaw: -yaw))
                return 0;

            placed++;

            if (decor.MillSupports.Any
                && Scatter(parent, grid, rng,
                           new Choice(decor.MillSupports, Any(decor.MillSupports, rng),
                                      MillWheelHeight, byWidth: false, low: 1f, high: 1f),
                           water, heightScale, spread: 0f, occupied: null, yaw: -yaw))
                placed++;

            // And the mill itself on the bank behind the wheel.
            if (decor.Kit != null && decor.Kit.CanBuildHouse)
            {
                var mill = BuildingBuilder.House(parent, decor.Kit, rng, out int storeys);

                if (Raise(grid, bank, rng, mill, storeys * StoreyHeight,
                          heightScale, occupied, road, Mathf.Round(yaw / 90f) * 90f, landmark: false))
                {
                    Landmark.Note(found, LandmarkKind.House, bank);
                    placed++;
                }
            }

            if (decor.Boats.Any
                && Scatter(parent, grid, rng,
                           new Choice(decor.Boats, Any(decor.Boats, rng), BoatLength,
                                      byWidth: true, low: 1f, high: 1f),
                           water, heightScale, spread: 2.4f, occupied: null, yaw: -yaw))
                placed++;

            return placed;
        }

        /// <summary>The tile a world position falls on, or -1 when it is off the map.</summary>
        static int Tile(TileGrid grid, float x, float z)
        {
            int tx = (int)(x / TileGrid.TileSize);
            int ty = (int)(z / TileGrid.TileSize);

            return grid.InBounds(tx, ty) ? grid.ToIndex(tx, ty) : -1;
        }

        static bool Raise(TileGrid grid, int tile, DeterministicRandom rng, GameObject building,
                          float height, float heightScale, HashSet<int> occupied,
                          HashSet<int> road = null, float yaw = -1f, bool landmark = true,
                          float maxWidth = 0f, Vector3 nudge = default, bool resize = true)
        {
            if (building == null) return false;

            // <b>A house in a village is not a landmark, and blowing it up to landmark
            // size is what made the villages look wrong.</b>
            //
            // The scale exists so that a lone building can be picked out from map height,
            // and it is 1.6 with a floor under it. Applied to a village house it gives:
            // seven metres becomes 11.2, and nine with an upper storey becomes 14.4.
            // Measured on 1-6, every house stood between 12.5 and 16.3 m — four to five
            // storeys, beside a wagon 3.2 m tall. At that size a two-storey house reads
            // as wall, roof, wall, roof, and what it looks like is two or three houses
            // stacked on top of each other, which is exactly what it was reported as.
            //
            // A settlement is not something to spot from above. It is something the road
            // goes through, and the wagons are the ruler.
            if (landmark) height = Mathf.Max(height * _landmarkScale, _landmarkFloor);

            var at = Vec2.FromTile(grid, tile);

            float surfaceY = grid.SurfaceElevation(at.X, at.Y) * heightScale;

            // <b>A cottage is not seated like a tower, and seating it like one buries it
            // to the sills.</b>
            //
            // BuildingSink is an eighth of the model's own height, and it is right for
            // what it was written for: a tower or a keep whose artist tapered its base
            // into the ground, and a stacked house whose bottom course was a foundation
            // meant to be half buried. A whole cottage has neither. An eighth of five and
            // a half metres is 0.66 m before the slope is added, which is the ground floor
            // — reported, correctly, as half-buried houses.
            //
            // The slope share stays either way. That is not about the model at all: it is
            // what keeps the uphill side of anything from standing clear of the hill with
            // daylight under it.
            float taper = landmark ? height * BuildingSink : CottageSink;
            float groundY = surfaceY - (taper + Fall(grid, tile, heightScale) * SlopeSink);

            // Quarter turns, as for any building. A house at eleven degrees reads as
            // subsidence, and this one is several pieces deep.
            building.transform.rotation = Quaternion.Euler(
                0f, yaw >= 0f ? yaw : rng.Range(0, 4) * 90f, 0f);
            // <paramref name="nudge"/> moves it off the middle of its tile, which is where
            // everything else here stands. A town house wants it: the plot is four metres
            // and the house is seven, so centred on the tile it leans two metres over the
            // street in front of it — measured, 155 of 249 of them reaching across ground
            // the caravan drives, and the column went through the gables. Set back by the
            // overhang, the front stands on the plot line and the rest leans into the
            // block behind, where only its neighbours care.
            building.transform.position = new Vector3(at.X + nudge.x, groundY, at.Y + nudge.z);

            // Scaled about its own origin, which the builder put on the ground plane —
            // not seated by its lowest point, which is what ModelScaling.Fit does and
            // what every other prop here wants. A ruin has its wall deliberately sunk
            // below that plane, and seating by the lowest point would dig it straight
            // back up. So what is fitted is the height that shows.
            // Against the surface rather than the seated origin, so the height asked for
            // is the height the player sees. Measured from the sunk origin instead, a
            // house on a slope would come out short by however far it was buried.
            var standing = ModelScaling.Measure(building);
            float above = standing.max.y - surfaceY;

            // <b>A width cap, which every other prop in this file has had and buildings
            // never did.</b> Scatter fits its props with ModelScaling.FitWithin and a cap
            // of size * SpreadLimit, precisely because fitting by height hands a model a
            // width nobody asked for. Raise fits by height and nothing else.
            //
            // Which is worse here than anywhere, because what Raise fits is the height
            // that *shows* — the model minus however far Seat buried it. Measured over
            // chapter 1: a mountain pass falls 0.34 m at the median and 1.79 at the worst,
            // so a tower is sunk 1.5 to 2.4 m before it is measured, and the factor that
            // pushes its top back up to eleven metres is between 1.3 and 3.05 depending on
            // how tall the model was to begin with. Every one of those multiplies the
            // width too. That is the tower that is not tall but is enormously broad.
            //
            // The smaller demand wins, as it does in FitWithin: a building may be half
            // again as wide as it is tall and no wider.
            float byHeight = above > 0.0001f ? height / above : 1f;
            float widest = Mathf.Max(standing.size.x, standing.size.z);

            // A caller may name the width instead of taking the general cap, and a town
            // has to. Its buildings stand on a four-metre grid with streets cut out of
            // that same grid, and at the general cap — half again the height, so eight and
            // a quarter metres — each house leant two metres over the street in front of
            // it. Measured: 155 of 249 reached across walkable ground, and the caravan
            // drove through the gables. A narrow, tall house on a narrow plot is also what
            // a town of this age actually looked like.
            float limit = maxWidth > 0f ? maxWidth : height * SpreadLimit;
            float byWidth = widest > 0.0001f ? limit / widest : byHeight;

            // Unless the thing was built at the size it means to be. A castle is assembled
            // from kit pieces that are already drawn to the scale of everything else on the
            // map, and fitting the assembly to a target height scales every piece with it:
            // on 3-10 that was x2.58, curtain walls thirteen metres high, and a goal that
            // read as a stack of slabs. See BuildingBuilder.CastleSpan.
            if (resize) building.transform.localScale *= Mathf.Min(byHeight, byWidth);

            // Scaled before the lane is checked, because until it is scaled nobody knows
            // how much ground it covers — the same order Scatter uses, and for the same
            // reason. A building that will not fit beside the road comes down again
            // rather than being left standing in it.
            if (InTheRoad(grid, road, building, at.X, at.Y))
            {
                Unbuild(building);
                return false;
            }

            // <b>And whether anything is already standing in it.</b> Raise reserved its
            // ground and never asked for it — the only test above it is
            // PlaceLandmarks checking the single tile under the building's middle, which
            // says nothing about a house four metres wide on the tile next door. So two
            // of them could go up in the same place and did, one growing out of the roof
            // of the other.
            //
            // Scatter has asked this since it was written, with this same call. Raise is
            // the one path that skipped it, and it is the path that puts up everything
            // large enough for the overlap to show.
            if (!FootprintClear(grid, occupied, at.X, at.Y, FootprintRadius(building), always: true))
            {
                Unbuild(building);
                return false;
            }

            Block(building, canopy: false);

            // <b>And asked again of the wall it just claimed.</b> The test above measures
            // from the middle of the tile, which is where a building stands unless it was
            // nudged off it, and against the footprint, which is the model's own bounds.
            // What stops a wagon is neither: it is the disc Block has this moment put on
            // the building, drawn about the bounds' centre - and for a house built of
            // several pieces that centre is not the tile's. Two houses in the lane on 1-8,
            // both of them legal by the tile and standing in the road on the ground.
            if (Barring(grid, road, building))
            {
                Unbuild(building);
                return false;
            }

            Reserve(grid, occupied, building, at.X, at.Y);

            return true;
        }

        /// <summary>Stands one landmark on the centre of a tile, sized and seated.</summary>
        static GameObject Place(Transform parent, TileGrid grid, int tile, DeterministicRandom rng,
                                Choice choice, float heightScale, HashSet<int> occupied = null,
                                float sink = 0f, float standoff = 0f)
        {
            var centre = Vec2.FromTile(grid, tile);

            // Stood off the middle of the tile when something is already standing there.
            //
            // The ground is sampled where the model ends up rather than at the centre it
            // was offset from, or a prop pushed onto a slope hangs by the difference.
            var position = centre;
            if (standoff > 0f)
            {
                float bearing = rng.Range(0, 8) * 45f * Mathf.Deg2Rad;
                position = new Vec2(centre.X + Mathf.Cos(bearing) * standoff,
                                    centre.Y + Mathf.Sin(bearing) * standoff);
            }

            float groundY = grid.SurfaceElevation(position.X, position.Y) * heightScale - sink;

            var instance = Object.Instantiate(choice.Prefab, parent);
            instance.transform.position = new Vector3(position.X, groundY, position.Y);

            // Buildings are square to the world in a way trees are not, so they turn in
            // quarters. A house at eleven degrees reads as subsidence.
            float yaw = rng.Range(0, 4) * 90f;
            instance.transform.rotation = choice.ZUp
                ? Quaternion.Euler(-90f, yaw, 0f)
                : Quaternion.Euler(0f, yaw, 0f);

            // Never smaller than the floor. Across or up depending on which way this
            // kind is measured, and either reading of "at least this many metres" is the
            // one that decides whether it can be made out from map height.
            float size = Mathf.Max(choice.Size * _landmarkScale, _landmarkFloor);

            if (choice.LifeSize) Ground(instance, groundY);
            else if (choice.ByWidth) ModelScaling.FitToFootprint(instance, size, groundY);
            else if (choice.MaxSpread > 0f)
                ModelScaling.FitWithin(instance, size, size * choice.MaxSpread, groundY);
            else ModelScaling.Fit(instance, size, groundY);

            // Asked after scaling, because until it is scaled nobody knows how much
            // ground it wants — the same order Scatter and Raise use, and for the same
            // reason.
            //
            // <b>Place reserved its ground and never asked for it.</b> Raise had exactly
            // this hole and it was closed there; this is the other path in, and it was
            // left open. Today it puts up wells and fallen timber, where two in one spot
            // is a small ugliness rather than the house growing out of a roof that made
            // the case over there — but Farms and Watchtowers route through here too, and
            // both are empty only because somebody emptied them. The hole should not be
            // waiting when they are filled again.
            if (!FootprintClear(grid, occupied, position.X, position.Y,
                                FootprintRadius(instance)))
            {
                Unbuild(instance);
                return null;
            }

            Block(instance, choice.Canopy);

            // A farm is nine metres across and a ruin five, so the landmarks need their
            // ground reserving for the same reason the mountain does.
            Reserve(grid, occupied, instance, position.X, position.Y);

            return instance;
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
        static Choice Pick(BiomeDecor decor, TerrainType terrain, DeterministicRandom rng,
                           bool onTheBank = false)
        {
            float roll = rng.Range(0f, 1f);

            // <b>No boulders at the water.</b> A boulder is fitted to BoulderWidth, five
            // and a half metres across, and the rivers here carry four to eight metres of
            // water — two tiles at the median. One of them dropped on the bank sits in the
            // crossing looking like a rock the size of the river, and beside the stepping
            // stones at 1.1 m it reads as though somebody rolled a house into the ford.
            //
            // The tile still gets something; it gets a rock, which is what a riverbank
            // has. Nothing is removed and no other ground changes.
            if (onTheBank && roll >= 0.56f && roll < 0.68f)
                return From(decor.Rocks, rng, RockHeight);

            switch (terrain)
            {
                // Three quarters trees, up from two thirds. The undergrowth and the rock
                // are what a forest floor has *as well*, and they were taking a fifth of
                // the ground the trees were meant to be standing on.
                case TerrainType.Forest:
                    if (roll < 0.50f) return Tree(decor.Pines, rng, PineHeight);
                    if (roll < 0.65f) return Tree(decor.Trees, rng, TreeHeight);
                    if (roll < 0.76f) return Tree(decor.Birch, rng, TreeHeight);
                    if (roll < 0.90f) return From(decor.Bushes, rng, BushHeight);
                    if (roll < 0.97f) return From(decor.Rocks, rng, RockHeight);
                    return From(decor.Timber, rng, TimberHeight, sink: StumpSink);

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
                    if (roll < 0.42f)
                        return Tree(decor.DeadTrees, rng, DeadTreeHeight,
                                    DeadJitterLow, DeadJitterHigh,
                                    sink: StumpSink, maxSpread: DeadTreeSpread);

                    // What came off them. The share is taken out of the trunks' own and
                    // not from anything else, because that is where these models were:
                    // two of the thirteen in the dead set, which is about the four parts
                    // in a hundred they get back here. The marsh is dressed the same
                    // amount as before, in the same things, with two of them now lying
                    // down instead of standing on their ends.
                    //
                    // Measured across and left the way round it was drawn. Nothing here
                    // turns it down, because the pack already did: the branch is 1.8 m
                    // long and 0.76 m tall in its own file. It only ever stood up because
                    // it was being fitted to a nine-metre height, which took three
                    // quarters of a metre of fallen wood and made a nine-metre arch of
                    // it. Sizing it across is the whole fix.
                    if (roll < 0.46f)
                        return From(decor.Deadfall, rng, DeadfallWidth, byWidth: true);

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
                           bool byWidth = false, float maxSpread = 0f, float sink = 0f) =>
            set != null && set.Any
                ? new Choice(set, Any(set, rng), size, byWidth, low, high,
                             maxSpread: maxSpread, sink: sink > 0f ? sink : set.Sink)
                : default;

        /// <summary>A tree: the wide size spread a stand of them wants, and canopy rules.</summary>
        static Choice Tree(PropSet set, DeterministicRandom rng, float size,
                           float low = TreeJitterLow, float high = TreeJitterHigh,
                           float sink = 0f, float maxSpread = 0f) =>
            set != null && set.Any
                ? new Choice(set, Any(set, rng), size, false, low, high, canopy: true,
                             maxSpread: maxSpread, sink: sink > 0f ? sink : set.Sink)
                : default;

        static GameObject Any(PropSet set, DeterministicRandom rng) =>
            set.Models[rng.Range(0, set.Models.Length)];
    }
}
