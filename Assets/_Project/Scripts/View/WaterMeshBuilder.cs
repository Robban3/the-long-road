using TheVeil.Sim;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheVeil.View
{
    /// <summary>
    /// Builds the water as one continuous surface instead of a plane per tile.
    ///
    /// **The blue plates, finally.** Every river on this map was a crowd of six-metre
    /// square planes, one per four-metre tile, each laid flat at its own tile's bed
    /// height and turned a random quarter. Three things follow from that and all three
    /// were on screen: the planes overhang their tiles by a metre onto the bank, so the
    /// waterline is a row of straight blue edges lying on the grass; neighbouring tiles
    /// sit at different bed heights, so the sheet steps; and an opaque unlit blue quad
    /// over a green field is a blue plate however it is lit.
    ///
    /// A surface has to be one surface. The corners here are shared between tiles, so
    /// the sheet is continuous by construction and cannot step or seam; each corner sits
    /// at the *lowest* bed that meets there, so the water never climbs onto a bank; and
    /// the material is transparent, so the bed shows through in the shallows and the
    /// thing reads as water with a bottom rather than as paint.
    ///
    /// One mesh and one draw call, in place of several hundred.
    /// </summary>
    public static class WaterMeshBuilder
    {
        /// <summary>
        /// How far the surface sits above the bed of the shallowest tile it touches.
        ///
        /// Small: a ford is water a cart can be driven through, so the crossings have to
        /// stay visibly shallow. What stops the bed poking through is the corner rule,
        /// not this.
        /// </summary>
        public const float Depth = 0.35f;

        /// <summary>
        /// The thinnest the water may ever be, in metres.
        ///
        /// A floor under the levelled surface, so a bank whose bed happens to sit above
        /// its neighbourhood does not leave the river showing dry ground where the map
        /// says water. Ten centimetres: visible, and not a puddle standing on a hill.
        /// </summary>
        public const float Film = 0.1f;

        /// <summary>How far either way the bed is averaged to find the water's level, in tiles.</summary>
        // Two. Enough to flatten a channel one to three tiles wide, short enough that the
        // river still follows the valley it runs down.
        const int Levelling = 2;

        /// <summary>
        /// The height the water stands at here: the bed, levelled.
        ///
        /// <b>This is what gives the river a depth at all.</b> The surface used to be the
        /// lowest bed of the four tiles at a corner plus Depth, which makes it a
        /// thirty-five centimetre film draped over the bottom — the same thickness in the
        /// middle of the channel as at the edge, following every bump underneath. Water
        /// does the opposite: the surface is level and the *depth* is what varies.
        ///
        /// Measured on levels 1 and 5, the bed falls 1.2 to 1.4 m across the channel and
        /// only 0.3 m per row along it. Averaging over two tiles either way flattens the
        /// cross-section while leaving the fall along the river alone, so the water lies
        /// level from bank to bank and still runs downhill.
        ///
        /// Wet tiles only. Averaging the meadow in would lift the surface onto the grass,
        /// which is the artefact this whole builder was written to remove.
        /// </summary>
        static float Bedding(TileGrid grid, bool[] wetMask, int cornerX, int cornerY,
                             float tileSize, float heightScale)
        {
            float sum = 0f;
            int counted = 0;

            for (int dy = -Levelling; dy <= Levelling - 1; dy++)
            {
                for (int dx = -Levelling; dx <= Levelling - 1; dx++)
                {
                    int tx = cornerX + dx, ty = cornerY + dy;
                    if (!grid.InBounds(tx, ty)) continue;
                    if (!wetMask[grid.ToIndex(tx, ty)]) continue;

                    sum += grid.SurfaceElevation((tx + 0.5f) * tileSize, (ty + 0.5f) * tileSize)
                         * heightScale;
                    counted++;
                }
            }

            return counted == 0 ? 0f : sum / counted;
        }

        /// <summary>
        /// How deep the water is, packed into the vertex colour for the shader.
        ///
        /// <b>Not the depth buffer.</b> URP can hand a shader the depth of whatever is
        /// behind it and that is how water is usually done, but it costs a pass on a
        /// phone — and it is unnecessary here, because this builder already knows where
        /// the bed is. Carrying the number on the vertex is exact where a screen-space
        /// read is an approximation, and free.
        ///
        /// Normalised against DeepEnough so the shader works in 0..1 and the metres stay
        /// here, where they mean something.
        ///
        /// <b>The channel contract, for a bought material.</b> The depth goes in red,
        /// green and blue alike, and alpha is left at one. Writing it three times was
        /// incidental — it is one number and any channel would have done — but it is what
        /// makes this mesh usable by a water package without touching the mesh: the
        /// stylised water assets read foam and transparency weights off a vertex colour
        /// channel, and whichever one a given package picks, it finds the depth there.
        ///
        /// What is not guaranteed is the *polarity*. Here one means deep. A package that
        /// reads the channel as "how much foam" or "how transparent" wants the opposite,
        /// and the fix is to write 1 - t here instead. That cannot be settled from
        /// outside the editor, so it is written down rather than guessed at.
        /// </summary>
        static List<Color> Deeps(List<float> depths)
        {
            var packed = new List<Color>(depths.Count);

            foreach (float depth in depths)
            {
                float t = Mathf.Clamp01(depth / DeepEnough);
                packed.Add(new Color(t, t, t, 1f));
            }

            return packed;
        }

        /// <summary>
        /// The depth at which water counts as fully deep, in metres.
        ///
        /// Measured rather than chosen, and it came out less than half of what was
        /// expected. Levelling the surface was supposed to leave getting on for two
        /// metres in the middle of the channel; what the generator actually digs, once
        /// only the wet tiles are averaged, is a median of 0.54 m out in the open water
        /// against 0.13 m at the outermost corners, topping out around 0.9 m. The bigger
        /// cross-section the plan expected was measured across the *banks* as well, and
        /// the banks are not water.
        ///
        /// So this is 0.8 m, near the deepest the map has. Set to the metre and a half
        /// that was guessed and the deep colour would simply never be reached: the river
        /// would come out uniformly shallow and green, which is the fault this was
        /// written to fix, only in the other direction.
        /// </summary>
        public const float DeepEnough = 0.8f;

        static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        static readonly int ShallowColourId = Shader.PropertyToID("_ShallowColor");

        /// <summary>
        /// The colour out in the channel: blue, dark, and nearly opaque.
        ///
        /// Darker and less see-through than the one colour the water used to have,
        /// because it is no longer the only one. A single tint has to stand for the whole
        /// river and ends up a compromise between water you can see the gravel through
        /// and water you cannot see into at all; with a shallow colour beside it this one
        /// is free to be what deep water actually looks like from above.
        /// </summary>
        public static readonly Color Surface = new Color(0.13f, 0.28f, 0.42f, 0.88f);

        /// <summary>
        /// And the colour at the waterline: green from the bed showing through, and
        /// transparent enough that it does.
        /// </summary>
        public static readonly Color Shallows = new Color(0.30f, 0.47f, 0.40f, 0.42f);

        /// <summary>
        /// And the marsh's own two, which are nothing like the river's.
        ///
        /// Taken from the nature pack's Water_Swamp_01 rather than invented, so a level
        /// looks the same whether the swamp material is in the inspector slot or the
        /// project's own shader is standing in for it. Dark olive going to a peaty
        /// yellow-green: standing water over rotting leaves does not reflect the sky the
        /// way a river does, and the single strongest thing separating a bog from a pond
        /// is that it is not blue.
        /// </summary>
        public static readonly Color MarshSurface = new Color(0.051f, 0.081f, 0.030f, 0.67f);
        public static readonly Color MarshShallows = new Color(0.132f, 0.176f, 0.031f, 0.40f);

        /// <summary>
        /// Whether this terrain is under water. Fords included: a ford is a shallow
        /// place in a river, not a hole in it, and leaving them dry cut every river into
        /// pieces with a green stripe where the crossing is.
        /// </summary>
        public static bool Wet(TerrainType terrain)
            => terrain == TerrainType.Water || terrain == TerrainType.Ford;

        /// <summary>
        /// How deep a marsh pool stands, in metres.
        ///
        /// Half the river's, and the shallowness is the whole point. A marsh is ground
        /// the caravan drives across at rather more than twice the cost of open plains,
        /// and it must keep looking like ground: a player reads blue as a thing to go
        /// round, and pools deep enough to argue with would be a lie about where the
        /// route can go. Ankle deep, with the bed showing through.
        /// </summary>
        public const float PoolDepth = 0.18f;

        /// <summary>
        /// How far below its surroundings a marsh tile has to lie to hold water, in metres.
        ///
        /// <b>Measured, because the alternative is a lake.</b> Marsh is about a tenth of
        /// the map and it arrives in a handful of large patches — six of them on levels 1
        /// and 5, the biggest 186 tiles. Sheeting all of that is not a marsh, it is an
        /// inland sea across ground the caravan is meant to drive through.
        ///
        /// So only the hollows fill, which is what water does. At this threshold that is
        /// 18 to 24 per cent of the marsh in 19 to 42 separate pools, the median one a
        /// tile or two across and the largest six to twelve. Halving it to 0.1 m puts the
        /// biggest pool on level 5 at forty tiles, which reads as a lake again; doubling
        /// it to 0.5 leaves single tiles that look like a bug rather than a bog.
        /// </summary>
        public const float PoolDrop = 0.2f;

        /// <summary>How far around a marsh tile the ground is compared, in tiles.</summary>
        const int PoolRing = 2;

        /// <summary>
        /// The marsh tiles low enough to hold standing water.
        ///
        /// Compared against the marsh around them rather than against sea level: a fen on
        /// a hillside is still a fen, and its pools sit in its own dips, not at the bottom
        /// of the map. Dry ground in the ring is not counted for the same reason the
        /// river's Bedding does not count the meadow — the question is where this marsh
        /// dips, not whether it is lower than the hill beside it.
        /// </summary>
        static bool[] Hollows(TileGrid grid, float tileSize, float heightScale)
        {
            var pools = new bool[grid.Width * grid.Height];

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid[grid.ToIndex(x, y)] != TerrainType.Marsh) continue;

                    float here = grid.SurfaceElevation((x + 0.5f) * tileSize,
                                                       (y + 0.5f) * tileSize) * heightScale;
                    float sum = 0f;
                    int counted = 0;

                    for (int dy = -PoolRing; dy <= PoolRing; dy++)
                    {
                        for (int dx = -PoolRing; dx <= PoolRing; dx++)
                        {
                            int tx = x + dx, ty = y + dy;
                            if (!grid.InBounds(tx, ty)) continue;
                            if (grid[grid.ToIndex(tx, ty)] != TerrainType.Marsh) continue;

                            sum += grid.SurfaceElevation((tx + 0.5f) * tileSize,
                                                         (ty + 0.5f) * tileSize) * heightScale;
                            counted++;
                        }
                    }

                    // Too small a sample to say anything about a neighbourhood. A lone
                    // marsh tile in a meadow is not a pool, it is a patch of soft ground.
                    if (counted < 3) continue;

                    pools[grid.ToIndex(x, y)] = here <= sum / counted - PoolDrop;
                }
            }

            return pools;
        }

        /// <summary>
        /// The standing water in a marsh, or null where none of it is low enough.
        ///
        /// A separate mesh from the river rather than another submesh of it, because the
        /// two are different water: this one is shallower, it is drawn with the swamp
        /// material, and its corners must not be shared with the river's — a pool beside
        /// a bank would otherwise drag the river's surface down to its own level.
        /// </summary>
        public static Mesh Pools(TileGrid grid, float tileSize, float heightScale)
        {
            if (grid == null) return null;

            var mesh = Build(grid, Hollows(grid, tileSize, heightScale), PoolDepth,
                             tileSize, heightScale, shelve: false);
            if (mesh != null) mesh.name = "Marsh water";
            return mesh;
        }

        /// <summary>
        /// Builds the sheet, or null when the map has no water.
        ///
        /// <paramref name="heightScale"/> is the same metres-of-relief the ground mesh
        /// was built with. At zero — the flat planning map — the surface comes out flat
        /// too, which is right.
        /// </summary>
        public static Mesh Build(TileGrid grid, float tileSize, float heightScale)
        {
            if (grid == null) return null;

            var wet = new bool[grid.Width * grid.Height];
            for (int i = 0; i < wet.Length; i++) wet[i] = Wet(grid[i]);

            return Build(grid, wet, Depth, tileSize, heightScale, shelve: true);
        }

        /// <summary>
        /// One sheet over whichever tiles the mask marks, standing <paramref name="depth"/>
        /// above the ground levelled under them.
        ///
        /// A mask rather than a terrain test, so the same machinery lays the river and the
        /// marsh pools. Everything that made the river read as water — corners shared so
        /// the sheet cannot seam, the bank drawn in and wandered so it is not a staircase,
        /// the depth carried on the vertex — is the same problem for a pool, and solving
        /// it twice is how the two drift apart.
        ///
        /// <paramref name="shelve"/> fades the depth to nothing at the waterline, which is
        /// right for a channel with banks and wrong for a puddle: a pool a tile or two
        /// across is all edge, and fading it would leave it entirely foam.
        /// </summary>
        static Mesh Build(TileGrid grid, bool[] wet, float depth, float tileSize,
                          float heightScale, bool shelve)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var depths = new List<float>();

            // One vertex per shared corner, made on demand. The key is the corner's grid
            // coordinate, which is what makes neighbouring tiles agree.
            var corners = new Dictionary<int, int>();
            int stride = grid.Width + 1;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!wet[grid.ToIndex(x, y)]) continue;

                    int a = Corner(grid, wet, corners, vertices, uvs, depths, x, y, tileSize, heightScale, depth, shelve, stride);
                    int b = Corner(grid, wet, corners, vertices, uvs, depths, x + 1, y, tileSize, heightScale, depth, shelve, stride);
                    int c = Corner(grid, wet, corners, vertices, uvs, depths, x + 1, y + 1, tileSize, heightScale, depth, shelve, stride);
                    int d = Corner(grid, wet, corners, vertices, uvs, depths, x, y + 1, tileSize, heightScale, depth, shelve, stride);

                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                }
            }

            if (triangles.Count == 0) return null;

            var mesh = new Mesh { name = "Water" };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(Deeps(depths));
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            // Tangents, which our own shader does not read and every bought one does.
            //
            // Nothing here uses a normal map: the ripples are arithmetic. But a water
            // package's whole look is its normal maps, and a mesh without tangents does
            // not fail loudly when one is applied to it — the lighting simply comes out
            // wrong, in a way that reads as a broken shader rather than as a missing
            // line. One call, paid once at level build, and the mesh is ready for a
            // material nobody has bought yet. See Material.
            mesh.RecalculateTangents();

            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// How far a corner is drawn in toward the water, as a share of a tile, by how
        /// many of the four tiles meeting there are wet.
        ///
        /// **This is what takes the staircase off the waterline.** A river drawn on a
        /// four-metre grid is a run of squares, and a bank that steps in four-metre
        /// right angles reads as pixel art however good the material is. Nothing about
        /// the grid can be helped — the simulation thinks in tiles and must — but the
        /// *surface* need not be drawn on the tile boundary.
        ///
        /// So every corner is pulled toward the middle of the water that meets it, by an
        /// amount that depends on how surrounded it is. A corner with all four tiles wet
        /// is in open water and does not move. One with a single wet tile is the outside
        /// of a right angle and moves furthest, which cuts the corner off. One with two
        /// is on a straight bank and moves a little, which softens the edge without
        /// narrowing the channel much. Three is the inside of a bend and barely moves.
        ///
        /// The corners are shared between tiles, so both sides of every edge move
        /// together and the sheet stays one continuous surface — it is the same
        /// guarantee the heights already rely on.
        /// </summary>
        static readonly float[] Inset = { 0f, 0.55f, 0.25f, 0.12f, 0f };

        /// <summary>How far a corner may wander, as a share of a tile. An eighth: half a metre.</summary>
        const float Wobble = 0.125f;

        /// <summary>
        /// How much of its depth a corner keeps, by how many of its four tiles are wet.
        ///
        /// <b>The waterline is where the depth is zero, and the mesh has no vertex
        /// there.</b> The outermost ring of corners sits inside the last wet tile, and
        /// the bed under it is already a hand's width down — measured, 0.13 m — so the
        /// sheet stops in a small vertical lip of water rather than running out onto the
        /// bank. The colour and the foam both key off the depth, and with the shallowest
        /// reading on the whole river a sixth of the way to full depth, the entire
        /// shallow end of the gradient had nowhere to happen: the foam came out a single
        /// pixel wide.
        ///
        /// This is not a depth the mesh could carry, because dropping those corners onto
        /// the bed would drain any river one tile wide — every corner of one touches
        /// land. So the *surface* stays where it is, level, and only the depth written
        /// for the shader is faded out toward the bank. It is the truthful number
        /// anyway: somewhere between that last vertex and the grass the water is nothing
        /// deep, and this is where the mesh says the shore is.
        ///
        /// The same signal <see cref="Inset"/> reads, for the same reason — how
        /// surrounded a corner is by water is what tells the middle of a channel from
        /// its edge.
        /// </summary>
        static readonly float[] Shelving = { 0f, 0f, 0.45f, 0.8f, 1f };

        /// <summary>A deterministic offset in -1..1 for a grid corner. The same hash Tint uses.</summary>
        static float Wander(int cx, int cy, int salt)
        {
            unchecked
            {
                uint h = (uint)((cx * 73856093) ^ (cy * 19349663) ^ salt);
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 32767.5f - 1f;
            }
        }

        /// <summary>
        /// One shared corner.
        ///
        /// Its height is the lowest bed of the wet tiles meeting there. Lowest rather
        /// than averaged, because the bank is what the surface must not climb: average a
        /// riverside corner with the meadow beside it and the waterline rides up the
        /// grass, which is the artefact this whole builder exists to remove.
        ///
        /// Its position is the grid corner drawn in toward that same water — see
        /// <see cref="Inset"/>.
        /// </summary>
        static int Corner(TileGrid grid, bool[] wetMask, Dictionary<int, int> corners,
                          List<Vector3> vertices, List<Vector2> uvs, List<float> depths,
                          int x, int y, float tileSize, float heightScale, float depth,
                          bool shelve, int stride)
        {
            int key = y * stride + x;
            if (corners.TryGetValue(key, out int found)) return found;

            float lowest = float.MaxValue;

            // Where the water that meets this corner lies, in tiles, so the corner knows
            // which way to move as well as how far.
            float towardX = 0f, towardZ = 0f;
            int wet = 0;

            for (int dy = -1; dy <= 0; dy++)
            {
                for (int dx = -1; dx <= 0; dx++)
                {
                    int tx = x + dx, ty = y + dy;
                    if (!grid.InBounds(tx, ty)) continue;
                    if (!wetMask[grid.ToIndex(tx, ty)]) continue;

                    float bed = grid.SurfaceElevation((tx + 0.5f) * tileSize, (ty + 0.5f) * tileSize)
                              * heightScale;

                    if (bed < lowest) lowest = bed;

                    towardX += dx + 0.5f;
                    towardZ += dy + 0.5f;
                    wet++;
                }
            }

            if (lowest == float.MaxValue) lowest = 0f;

            // A channel gets a levelled surface and a puddle gets a film, and the two
            // are not a preference — a river's bed is carved and a marsh's is not.
            //
            // <b>Levelling a puddle floats it.</b> Bedding averages tile-centre bed
            // samples over the wet tiles, and a pool one or two tiles across has almost
            // none: measured on levels 1 and 5, the sheet ended up 0.84 m above the fen
            // at 63% of its corners and buried under it at the other 37%, all at once.
            // The ground mesh's own corner height is the only number that cannot do
            // that, because it is where the ground actually is.
            float surface = shelve
                ? Mathf.Max(Bedding(grid, wetMask, x, y, tileSize, heightScale) + depth,
                            lowest + Film)
                : TerrainMeshBuilder.CornerHeight(grid, x, y, heightScale) + depth;

            float px = x * tileSize, pz = y * tileSize;

            if (wet > 0)
            {
                float pull = Inset[wet];
                px += towardX / wet * pull * tileSize;
                pz += towardZ / wet * pull * tileSize;

                // And a wander on top, so the bank is not a ruled line.
                //
                // The inset knocks the corners off a staircase but it cannot make a
                // straight run of river crooked, and a river drawn on a four-metre grid
                // has long straight runs. This moves each corner half a metre or so of
                // its own, which is a shoreline rather than an edge.
                //
                // Deterministic from the corner, never from a clock: a seed is a level,
                // and a bank that reshaped itself on every load would be a worse fault
                // than a straight one. Corners are shared, so both sides of every edge
                // move together and the sheet stays continuous.
                px += Wander(x, y, 0x9E37) * Wobble * tileSize;
                pz += Wander(x, y, 0x85EB) * Wobble * tileSize;
            }

            int index = vertices.Count;
            vertices.Add(new Vector3(px, surface, pz));

            // How deep the water is here, carried on the vertex so the shader can colour
            // and foam by it without reading a depth buffer — see Deeps. Faded out at
            // the waterline, which the mesh has no vertex on — see Shelving.
            // A pool is the same depth all over: it is a film, and its thickness is what
            // it is. Only a channel has a shore-to-middle gradient to describe.
            depths.Add(shelve ? Mathf.Max(0f, surface - lowest) * Shelving[wet] : depth);

            // UVs stay on the grid rather than following the moved vertex, so the ripple
            // the material puts on the surface does not stretch where the bank is cut.
            uvs.Add(new Vector2(x * 0.5f, y * 0.5f));
            corners[key] = index;

            return index;
        }

        /// <summary>
        /// The water material: whatever was put in the inspector, else the project's own
        /// shader, else URP Lit turned transparent in code.
        ///
        /// Transparency on the Lit shader is four properties and a keyword rather than
        /// one flag, and setting the colour's alpha alone does nothing at all — which is
        /// how an opaque sheet went out looking like paint over the river.
        /// </summary>
        public static Material Material(Material chosen = null)
            => Material(chosen, Surface, Shallows);

        /// <summary>
        /// The marsh pools' material: the swamp water from the pack, or our own tinted
        /// like it. Same construction as the river's — only the two colours differ.
        /// </summary>
        public static Material PoolMaterial(Material chosen = null)
            => Material(chosen, MarshSurface, MarshShallows);

        static Material Material(Material chosen, Color deep, Color shallow)
        {
            // A material somebody dropped in the inspector wins over everything below.
            //
            // <b>A slot rather than a shader name, and that is deliberate.</b> The obvious
            // way to take a bought water package is another Shader.Find with its name in
            // it — and a shader found by name at runtime is stripped from a player build
            // unless it is also listed in GraphicsSettings' always-included shaders. That
            // fails in the one place it is expensive to find out: it works in the editor
            // and comes out magenta on the phone. This project has already been caught by
            // it three times.
            //
            // A material a scene refers to is never stripped, because the build can see
            // the reference. So the slot removes the failure rather than moving it, and
            // it needs no name from a package nobody here can open.
            //
            // Instanced rather than used directly: the mesh is built per level and the
            // asset in the project folder should not pick up whatever a run does to it.
            if (chosen != null) return new Material(chosen) { name = "Water" };

            // The project's own water otherwise: waves and drifting ripples, all of it on
            // the GPU — see Shaders/Water.shader. Standing water reads as a painted floor
            // however good its colour is, and stock Lit has nothing on it that can move.
            var moving = Shader.Find("TheVeil/Water");
            if (moving != null)
            {
                var river = new Material(moving) { name = "Water" };
                river.SetColor(BaseColourId, deep);
                river.SetColor(ShallowColourId, shallow);
                river.renderQueue = (int)RenderQueue.Transparent;
                return river;
            }

            // And stock Lit when it is missing, which is still water, just still.
            Debug.LogWarning("[The Veil] Shader 'TheVeil/Water' not found, so the river "
                           + "will not move. Run The Veil > Set Up Project.");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            var water = new Material(shader) { name = "Water" };

            water.SetFloat("_Surface", 1f);                 // transparent
            water.SetFloat("_Blend", 0f);                   // alpha blend
            water.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            water.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            water.SetFloat("_ZWrite", 0f);
            water.SetFloat("_Smoothness", 0.85f);
            water.SetFloat("_Metallic", 0.1f);
            water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            water.DisableKeyword("_ALPHATEST_ON");
            water.renderQueue = (int)RenderQueue.Transparent;

            // One colour has to stand for the whole river here, so it is neither of the
            // two: Lit has no vertex-colour depth to blend them with, and the deep tint
            // on its own would put an opaque navy sheet over the fords.
            water.SetColor(BaseColourId, Color.Lerp(shallow, deep, 0.6f));

            return water;
        }
    }
}
