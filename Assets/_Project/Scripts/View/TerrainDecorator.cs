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
        public PropSet Trees = new PropSet();
        public PropSet Pines = new PropSet();
        public PropSet DeadTrees = new PropSet();
        public PropSet Rocks = new PropSet();
        public PropSet Mountains = new PropSet();

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

        public bool IsEmpty =>
            !Has(Trees) && !Has(Pines) && !Has(DeadTrees) && !Has(Rocks) && !Has(Mountains) &&
            !Has(GroundCover) && !Has(Houses) && !Has(Farms) && !Has(Watchtowers) && !Has(Timber);

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
        public const float MountainHeight = 20f;
        public const float DeadTreeHeight = 9f;

        /// <summary>Landmark sizes. Buildings are measured by height, ground works by width.</summary>
        public const float HouseHeight = 6f;
        public const float WatchtowerHeight = 11f;
        public const float FarmWidth = 9f;
        public const float TimberWidth = 3f;
        public const float RuinWidth = 5f;

        /// <summary>
        /// How many landmarks a map may carry. A hard cap rather than density alone,
        /// because density on a road that happens to run the length of the map produces
        /// a ribbon development, and the point of a landmark is that there are few.
        /// </summary>
        public const int MaxLandmarks = 18;

        /// <summary>Height of a grass tuft or a fern, in metres.</summary>
        public const float CoverHeight = 0.7f;

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
            { TerrainType.Road, 0.15f }
        };

        /// <summary>
        /// Props per tile.
        ///
        /// Forest was tuned down to 0.28 from a first attempt at 0.55, which put a
        /// thousand nine-metre trees on a 256-metre map and closed the canopy over the
        /// caravan entirely — the world has to be looked through, not just at. It is
        /// back up at 0.62, and two things changed underneath that number so the old
        /// objection no longer holds: trees now run from 4.7 m to 14.5 m rather than all
        /// standing at nine, and canopy no longer reserves ground, so the small ones
        /// fill in between the big ones instead of pushing them apart.
        ///
        /// Measured on 1-5, where the forest is 1812 tiles: 0.28 gives 489 trees at a
        /// median 4.5 m to the nearest neighbour, 0.45 gives 796 at 4.1 m, 0.62 gives
        /// 1088 at 3.6 m. A spruce crown is 0.62 of its height across, so at 3.6 m the
        /// crowns overlap — which is what the reference picture shows and what 4.5 m did
        /// not.
        ///
        /// <b>Not yet checked in a render.</b> Two were made and both were worthless: the
        /// script that set the density for them matched `COVER_DENSITY` instead of
        /// `DENSITY`, so they varied the grass and left the trees at 0.28. The counts and
        /// spacings above come from the module directly and stand; whether the caravan
        /// still reads against 1088 trees is open, and the first thing to look at.
        ///
        /// <b>This is the triangle budget's largest single line.</b> Twice the trees is
        /// twice the geometry, against the 250k limit in docs/technical-design.md. They
        /// share one atlas material so the draw calls batch; the triangles do not. Worth
        /// a look at the Stats window before it is called done.
        /// </summary>
        static readonly Dictionary<TerrainType, float> Density = new Dictionary<TerrainType, float>
        {
            { TerrainType.Forest, 0.62f },
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
                                   IReadOnlyCollection<int> ruinSites = null)
        {
            if (decor == null || decor.IsEmpty) return 0;

            var rng = new DeterministicRandom(seed ^ 0x5EED10);
            var clear = keepClear == null ? null : new HashSet<int>(keepClear);
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

                    if (Scatter(parent, grid, passRng, choice, i, heightScale, spread: 1.4f, occupied))
                        placed++;
                }
            }

            placed += PlaceGroundCover(parent, grid, rng, decor, clear, occupied,
                                       heightScale, densityScale);
            placed += PlaceShoreline(parent, grid, rng, decor, occupied, heightScale, densityScale);
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
            if (!decor.Rocks.Any) return 0;

            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MaxShoreStones; i++)
            {
                if (grid[i] == TerrainType.Water) continue;
                if (occupied.Contains(i)) continue;

                grid.ToCoords(i, out int x, out int y);
                if (!NextToWater(grid, x, y)) continue;

                int stones = 3 + rng.Range(0, 4);
                for (int s = 0; s < stones && placed < MaxShoreStones; s++)
                {
                    var choice = new Choice(decor.Rocks, Any(decor.Rocks, rng), ShoreStoneSize,
                                            byWidth: true);

                    Scatter(parent, grid, rng, choice, i, heightScale, spread: 2.0f);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>Whether any of the four neighbouring tiles is water.</summary>
        static bool NextToWater(TileGrid grid, int x, int y)
        {
            return IsWater(grid, x - 1, y) || IsWater(grid, x + 1, y)
                || IsWater(grid, x, y - 1) || IsWater(grid, x, y + 1);
        }

        static bool IsWater(TileGrid grid, int x, int y) =>
            grid.InBounds(x, y) && grid[x, y] == TerrainType.Water;

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

                for (int t = 0; t < tufts && placed < MaxGroundCover; t++)
                {
                    var choice = new Choice(decor.GroundCover, Any(decor.GroundCover, rng),
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
                            HashSet<int> occupied = null)
        {
            var position = Vec2.FromTile(grid, tile);
            float x = position.X + rng.Range(-spread, spread);
            float z = position.Y + rng.Range(-spread, spread);

            // Sampled the way the mesh is built, at the prop's own position. Using the
            // tile's own elevation instead leaves trees hovering above the ground or
            // buried in it, because the rendered surface is interpolated between corners
            // and a tile centre is a different number entirely.
            float groundY = grid.SurfaceElevation(x, z) * heightScale;

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
        /// Whether this is one of the props big enough to swallow what is already there.
        ///
        /// Mountains and nothing else, for now. Everything else is within a tile or two
        /// of the ground it was placed on, and the ordering only matters for the thing
        /// that is not.
        /// </summary>
        static bool IsBulky(TerrainType terrain, Choice choice)
            => terrain == TerrainType.MountainPass && choice.Size >= MountainHeight * 0.9f;

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

        static Choice Pick(BiomeDecor decor, TerrainType terrain, DeterministicRandom rng)
        {
            switch (terrain)
            {
                case TerrainType.Forest:
                    // Pines dominate, broadleaf mixed in so the canopy is not uniform.
                    if (decor.Pines.Any && (rng.Chance(0.62f) || !decor.Trees.Any))
                        return Tree(decor.Pines, rng, PineHeight);

                    return Tree(decor.Trees, rng, TreeHeight);

                case TerrainType.MountainPass:
                    if (decor.Mountains.Any && rng.Chance(0.30f))
                        return From(decor.Mountains, rng, MountainHeight);

                    return From(decor.Rocks, rng, RockHeight);

                // Dead trees belong to the marsh. They are the pack's most legible
                // model at a glance from above, and standing water killing the trees
                // is the thing a marsh looks like.
                case TerrainType.Marsh:
                    if (decor.DeadTrees.Any && rng.Chance(0.55f))
                        return Tree(decor.DeadTrees, rng, DeadTreeHeight,
                                    DeadJitterLow, DeadJitterHigh);

                    return From(decor.Rocks, rng, RockHeight);

                case TerrainType.Plains:
                case TerrainType.Road:
                    if (decor.Rocks.Any && rng.Chance(0.6f))
                        return From(decor.Rocks, rng, RockHeight);

                    return Tree(decor.Trees, rng, TreeHeight);

                default:
                    return default;
            }
        }

        static Choice From(PropSet set, DeterministicRandom rng, float size,
                           float low = JitterLow, float high = JitterHigh) =>
            set != null && set.Any
                ? new Choice(set, Any(set, rng), size, false, low, high)
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
