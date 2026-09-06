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

        public PropSet Wreckage = new PropSet();

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
        public const float HouseHeight = 6f;
        public const float WatchtowerHeight = 11f;

        /// <summary>How far into the ground a building is set, as a share of its size.</summary>
        public const float BuildingSink = 0.12f;
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
        /// <summary>How wide a bridge's deck is, and how far it reaches.</summary>
        // Five metres of deck: a wagon is two and a half and the escort walks beside it,
        // so anything narrower is a plank the caravan straddles. Twelve of span, which is
        // the ford tile plus a bank at each end — a bridge that stops at the waterline is
        // a jetty. See ModelScaling.FitToCrossing for why both numbers are needed.
        public const float FordDeck = 5f;
        public const float FordSpan = 12f;

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
                                   float landmarkScale = 1f)
        {
            // Before the early return below, so a call that decorates nothing still
            // leaves the floor at what this caller asked for rather than at what the
            // last one did.
            _landmarkFloor = minimumLandmark;
            _landmarkScale = landmarkScale <= 0f ? 1f : landmarkScale;

            if (decor == null || decor.IsEmpty) return 0;

            var rng = new DeterministicRandom(seed ^ 0x5EED10);
            var clear = keepClear == null ? null : new HashSet<int>(keepClear);

            // A set, not the list it arrives as. IReadOnlyCollection has no Contains
            // worth the name, and this is asked once per prop on every tile of the map.
            var road = Lane(grid, driveLine, driveMargin);
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
            placed += PlaceCastle(parent, grid, rng, decor, occupied, heightScale,
                                  goalTile, travelled, found);

            placed += PlaceLandmarks(parent, grid, rng, decor, clear, occupied, heightScale,
                                     ruinSites, road,
                                     travelled == null ? null : new HashSet<int>(travelled),
                                     found);

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

            // Bridges before anything scattered, and they claim the ground they cover.
            //
            // A bridge is twelve metres long and stands on one tile, so it reaches three
            // tiles into its neighbours — and it used to be built last, over ground the
            // shore pass had already strewn with boulders. One of them came up through
            // the deck. Placed first and claiming its whole footprint, the stones go
            // round it.
            placed += PlaceFords(parent, grid, rng, decor, occupied, heightScale);

            placed += PlaceGroundCover(parent, grid, rng, decor, clear, occupied,
                                       heightScale, densityScale);
            placed += PlaceShoreline(parent, grid, rng, decor, occupied, heightScale,
                                     densityScale, road);

            // The water goes on last, over everything laid on its bed. Nothing claims
            // ground for it: reeds stand in the shallows and pads float on the surface,
            // and a sheet that reserved its tiles would have cleared both away.
            placed += PlaceWater(parent, grid, heightScale);
            placed += PlaceCliffs(parent, grid, rng, decor, occupied, heightScale, road);
            placed += PlaceWillows(parent, grid, rng, decor, occupied, heightScale,
                                   densityScale, road);
            placed += PlaceCamps(parent, grid, rng, decor, occupied, heightScale, campSites, road,
                                 found);

            Census(parent);

            return placed;
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
        /// </summary>
        static int PlaceWater(Transform parent, TileGrid grid, float heightScale)
        {
            var mesh = WaterMeshBuilder.Build(grid, TileGrid.TileSize, heightScale);
            if (mesh == null) return 0;

            var surface = new GameObject("Water");
            surface.transform.SetParent(parent, false);

            // Terrain, not scenery. The planning fog paints every prop flat grey and
            // files it under the tile its transform sits on — which for one mesh covering
            // the whole map is tile zero, so every river on the plan would go grey
            // together until the corner of the map was revealed. A river is the thing a
            // route is planned around; it is read from the map, like the ground it cuts.
            surface.AddComponent<Signal>();
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WaterMeshBuilder.Material();
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

                if (Bridge(parent, grid, rng, decor, i, heightScale, occupied)) placed++;
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
                               BiomeDecor decor, HashSet<int> occupied, float heightScale,
                               HashSet<int> road = null)
        {
            if (!decor.Cliffs.Any) return 0;

            int placed = 0;
            var stood = new List<int>();

            for (int i = 0; i < grid.TileCount && placed < MaxLandmarks * 3; i++)
            {
                if (grid[i] != TerrainType.Cliff) continue;
                if (occupied.Contains(i)) continue;
                if (road != null && road.Contains(i)) continue;
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
                if (occupied.Contains(tile)) continue;
                if (road != null && road.Contains(tile)) continue;

                var choice = new Choice(decor.Camps, Any(decor.Camps, rng), CampHeight,
                                        byWidth: false);

                // A tent is pitched, not set down: it gets the same seating as a house so
                // its pegged edge meets the ground on a slope.
                if (Scatter(parent, grid, rng, choice, tile, heightScale, spread: 1.6f, occupied,
                            lift: -Seat(grid, tile, heightScale, CampHeight), signal: true))
                {
                    Landmark.Note(found, LandmarkKind.Camp, tile);
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
                           BiomeDecor decor, int tile, float heightScale, HashSet<int> occupied)
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

            // And which way it has to lie is the ford's own bearing: the crossing is a
            // run of tiles cut across the river, and the bridge lies along it.
            float across = Crossing(grid, tile);

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
            float span = Mathf.Max(FordSpan, (FordWidth(grid, tile, across) + 1) * TileGrid.TileSize);

            ModelScaling.FitToCrossing(instance, FordDeck, span, groundY);

            // Measured rather than described. Nothing here knows where the roadway is
            // inside a bridge model, so the bridge is asked at runtime — see BridgeDeck.
            var deck = instance.AddComponent<BridgeDeck>();
            deck.Measure();

            // And now the one thing FitToCrossing cannot do: stand the bridge on its
            // roadway instead of on its feet. BridgeDeck measured the roadway on the way
            // in — down the middle of the span, where the road runs and the railings are
            // not — so the number is already there to be read.
            float before = float.NaN, after = float.NaN;

            if (!float.IsNaN(deck.Deck))
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
                    + $"roadway {before:F1} m above the bank before settling and {after:F1} m after.");

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
        static float Crossing(TileGrid grid, int tile)
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

            // A ford one tile wide has no run to read, so fall back to the water — square
            // to it, and water only.
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

                    Scatter(parent, grid, rng, choice, i, heightScale, spread: 1.9f,
                            maxWidth: choice.ByWidth ? 0f : CoverWidth);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>Drops one model somewhere inside a tile, turned at random.</summary>
        static bool Scatter(Transform parent, TileGrid grid, DeterministicRandom rng,
                            Choice choice, int tile, float heightScale, float spread,
                            HashSet<int> occupied = null, float lift = 0f, float? yaw = null,
                            float maxWidth = 0f, bool signal = false)
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
            float cap = maxWidth > 0f ? maxWidth * rng.Range(low, high)
                      : choice.Canopy ? 0f
                      : size * SpreadLimit;

            if (choice.ByWidth) ModelScaling.FitToFootprint(instance, size, groundY);
            else if (cap > 0f) ModelScaling.FitWithin(instance, size, cap, groundY);
            else ModelScaling.Fit(instance, size, groundY);

            if (signal) Mark(instance);

            Block(instance, choice.Canopy);

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
                               List<Landmark> found)
        {
            if (goalTile < 0 || goalTile >= grid.TileCount) return 0;
            if (decor.Kit == null || !decor.Kit.CanBuildCastle) return 0;

            var castle = BuildingBuilder.Castle(parent, decor.Kit, rng);
            if (castle == null) return 0;

            if (!Raise(grid, goalTile, rng, castle, CastleHeight, heightScale, occupied,
                       GateYaw(grid, goalTile, travelled)))
                return 0;

            // The Solid that Raise just added, taken straight back off. See the note
            // above: on this one building it walls the caravan out of its own gate.
            foreach (var solid in castle.GetComponentsInChildren<Solid>(true))
            {
                if (Application.isPlaying) Object.Destroy(solid);
                else Object.DestroyImmediate(solid);
            }

            Landmark.Note(found, LandmarkKind.Castle, goalTile);
            return 1;
        }

        /// <summary>How tall the castle stands, in metres. Half again the watchtower.</summary>
        public const float CastleHeight = 22f;

        /// <summary>Tiles around the goal that are looked at to find which way the road comes in.</summary>
        public const int GateLookback = 8;

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
        static float GateYaw(TileGrid grid, int goalTile, IReadOnlyCollection<int> travelled)
        {
            float toX = -1f, toZ = 0f;

            if (travelled != null)
            {
                grid.ToCoords(goalTile, out int gx, out int gy);
                float sumX = 0f, sumY = 0f;
                int seen = 0;

                foreach (int tile in travelled)
                {
                    if (tile < 0 || tile >= grid.TileCount) continue;

                    grid.ToCoords(tile, out int x, out int y);
                    int dx = x - gx, dy = y - gy;

                    if (dx * dx + dy * dy > GateLookback * GateLookback) continue;

                    sumX += dx;
                    sumY += dy;
                    seen++;
                }

                if (seen > 0 && (sumX != 0f || sumY != 0f))
                {
                    toX = sumX;
                    toZ = sumY;
                }
            }

            // Whichever axis the road lies along more strongly wins the quarter turn.
            //
            // A yaw of nought leaves the gate pointing down -Z, ninety turns it to -X,
            // a hundred and eighty to +Z and two hundred and seventy to +X. Written the
            // other way round the castle presents its back wall to the road, which is
            // the one thing this function exists to prevent.
            if (Mathf.Abs(toX) >= Mathf.Abs(toZ)) return toX < 0f ? 90f : 270f;
            return toZ < 0f ? 0f : 180f;
        }

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
                                  List<Landmark> found = null)
        {
            int placed = 0;

            if (ruinSites != null && decor.Ruins.Any)
            {
                foreach (int tile in ruinSites)
                {
                    if (placed >= MaxLandmarks) break;
                    if (clear != null && clear.Contains(tile)) continue;
                    if (road != null && road.Contains(tile)) continue;
                    if (!occupied.Add(tile)) continue;

                    placed += Wreck(parent, grid, tile, rng, decor, heightScale, occupied, found);

                    // And a totem beside it, where the pack has one. A wreck says
                    // something happened here; a banner driven into the ground says
                    // somebody *chose* here, which is the difference between an accident
                    // and an ambush and is what the GDD's §5 table is asking for.
                    if (decor.Markers.Any
                        && Mark(Place(parent, grid, tile, rng,
                                      new Choice(decor.Markers, Any(decor.Markers, rng),
                                                 MarkerHeight, byWidth: false),
                                      heightScale, occupied,
                                      sink: Seat(grid, tile, heightScale, MarkerHeight))) != null)
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
                                              canopy: true);

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
                    && Built(parent, grid, rng, decor, i, heightScale, occupied, travelled, found))
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
                        choice = new Choice(decor.Timber, Any(decor.Timber, rng), TimberWidth, true);
                        kind = LandmarkKind.Timber;
                        break;
                }

                if (choice.Prefab == null) continue;

                Landmark.Note(found, kind, i);
                occupied.Add(i);

                // Buildings are set into the ground rather than stood on it.
                //
                // Everything else here rests its lowest point on the surface, which is
                // right for a tree and wrong for anything with a foundation: the pack's
                // towers taper to a rounded base meant to be buried, so on the ground
                // they read as pieces standing on a lawn. A tenth of their height buries
                // the taper, and on the slope of a pass it also stops the uphill side
                // showing daylight underneath.
                Place(parent, grid, i, rng, choice, heightScale, occupied,
                      sink: Seat(grid, i, heightScale, choice.Size));
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
            var chosen = Any(decor.Ruins, rng);

            var main = Mark(Place(parent, grid, tile, rng,
                                  new Choice(decor.Ruins, chosen, RuinWidth,
                                             byWidth: true),
                                  heightScale, occupied,
                                  sink: Seat(grid, tile, heightScale, RuinWidth)));
            if (main == null) return 0;

            Landmark.Note(found, IsBones(chosen) ? LandmarkKind.Bones : LandmarkKind.Wreck, tile);

            int placed = 1;
            if (!decor.Wreckage.Any) return placed;

            int pieces = rng.Range(1, 4);
            for (int i = 0; i < pieces; i++)
            {
                var debris = new Choice(decor.Wreckage, Any(decor.Wreckage, rng),
                                        DebrisWidth, byWidth: true, canopy: true);

                var position = Vec2.FromTile(grid, tile);
                float x = position.X + rng.Range(-DebrisSpread, DebrisSpread);
                float z = position.Y + rng.Range(-DebrisSpread, DebrisSpread);
                float groundY = grid.SurfaceElevation(x, z) * heightScale;

                var instance = Object.Instantiate(debris.Prefab, parent);
                instance.transform.position = new Vector3(x, groundY, z);
                instance.transform.rotation = debris.ZUp
                    ? Quaternion.Euler(-90f, rng.Range(0f, 360f), 0f)
                    : Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

                ModelScaling.FitToFootprint(instance, DebrisWidth, groundY);
                LayFlat(instance, groundY);
                Mark(instance);

                placed++;
            }

            return placed;
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

            return name.Contains("Skull") || name.Contains("Skeleton")
                || name.Contains("Bone") || name.Contains("Grave");
        }

        /// <summary>How wide a loose piece of wreckage is, and how far it lies from the cart.</summary>
        public const float DebrisWidth = 1.3f;
        public const float DebrisSpread = 2.6f;

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
                          List<Landmark> found = null)
        {
            var kit = decor.Kit;
            var terrain = grid[tile];

            if (terrain == TerrainType.MountainPass && kit.CanBuildTower && rng.Chance(TowerChance))
                return Note(found, LandmarkKind.Watchtower, tile,
                            Raise(grid, tile, rng, BuildingBuilder.Tower(parent, kit, rng),
                                  TowerHeight, heightScale, occupied));

            grid.ToCoords(tile, out int x, out int y);

            // Somewhere a building could stand: open ground, near enough to the road the
            // caravan is taking to be *on* it, and flat enough to have been built on.
            bool settled = terrain == TerrainType.Plains
                        && Beside(grid, line, x, y, SettlementReach)
                        && Fall(grid, tile, heightScale) < BuildableFall;

            if (settled && kit.CanBuildHouse && rng.Chance(HouseChance))
                return Note(found, LandmarkKind.House, tile,
                            Raise(grid, tile, rng, BuildingBuilder.House(parent, kit, rng),
                                  HouseHeight, heightScale, occupied));

            if (settled && kit.CanBuildHouse && rng.Chance(FarmChance))
                return Note(found, LandmarkKind.Farm, tile,
                            Raise(grid, tile, rng, BuildingBuilder.House(parent, kit, rng),
                                  FarmHeight, heightScale, occupied));

            // Ruins go the other way: out in the country, away from the line, because a
            // ruin beside a living road reads as a building somebody would have repaired.
            if ((terrain == TerrainType.Plains || terrain == TerrainType.Forest)
                && !Beside(grid, line, x, y, SettlementReach)
                && kit.CanBuildRuin && rng.Chance(StoneRuinChance))
                return Note(found, LandmarkKind.Ruin, tile,
                            Raise(grid, tile, rng, BuildingBuilder.Ruin(parent, kit, rng),
                                  StoneRuinHeight, heightScale, occupied));

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

        /// <summary>How far from the caravan's road a building may still be said to be on it.</summary>
        public const int SettlementReach = 3;

        /// <summary>Most a tile may fall across before nobody would have built on it.</summary>
        public const float BuildableFall = 1.6f;

        /// <summary>A farmhouse stands lower and broader than a village house.</summary>
        public const float FarmHeight = 5.5f;

        /// <summary>How tall a built castle tower stands, and a stone ruin.</summary>
        public const float TowerHeight = 15f;
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
        static bool Raise(TileGrid grid, int tile, DeterministicRandom rng, GameObject building,
                          float height, float heightScale, HashSet<int> occupied,
                          float yaw = -1f)
        {
            if (building == null) return false;

            height = Mathf.Max(height * _landmarkScale, _landmarkFloor);

            var at = Vec2.FromTile(grid, tile);

            float surfaceY = grid.SurfaceElevation(at.X, at.Y) * heightScale;
            float groundY = surfaceY - Seat(grid, tile, heightScale, height);

            // Quarter turns, as for any building. A house at eleven degrees reads as
            // subsidence, and this one is several pieces deep.
            building.transform.rotation = Quaternion.Euler(
                0f, yaw >= 0f ? yaw : rng.Range(0, 4) * 90f, 0f);
            building.transform.position = new Vector3(at.X, groundY, at.Y);

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

            if (above > 0.0001f) building.transform.localScale *= height / above;

            Block(building, canopy: false);
            Reserve(grid, occupied, building, at.X, at.Y);

            return true;
        }

        /// <summary>Stands one landmark on the centre of a tile, sized and seated.</summary>
        static GameObject Place(Transform parent, TileGrid grid, int tile, DeterministicRandom rng,
                                Choice choice, float heightScale, HashSet<int> occupied = null,
                                float sink = 0f)
        {
            var position = Vec2.FromTile(grid, tile);
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

            if (choice.ByWidth) ModelScaling.FitToFootprint(instance, size, groundY);
            else ModelScaling.Fit(instance, size, groundY);

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
        static Choice Pick(BiomeDecor decor, TerrainType terrain, DeterministicRandom rng)
        {
            float roll = rng.Range(0f, 1f);

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
