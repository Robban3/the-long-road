using System.Collections.Generic;
using Arna.Sim;
using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// Builds the terrain overview mesh: one flat quad per tile, coloured by terrain
    /// type through vertex colours.
    ///
    /// Vertices are not shared between tiles. Sharing would blend neighbouring
    /// colours across the seam and turn crisp terrain boundaries into gradients —
    /// and reading those boundaries is exactly the skill the fog-of-war design asks
    /// of the player (docs/GDD.md §3.4).
    ///
    /// Shading is not bound by that. Normals are sampled from the height field at the
    /// corners, which neighbouring tiles agree on, so light runs smoothly over the
    /// ground while the colours stay crisp. That separation is the point: the player
    /// should see one continuous landscape whose terrain types are still legible.
    ///
    /// A 64x64 grid yields 16 384 vertices, comfortably inside one mesh. The chunked
    /// layout described in docs/technical-design.md §5 only starts paying off once
    /// parts of the map can be culled, which is not the case for an overview that is
    /// always fully visible.
    /// </summary>
    public static class TerrainMeshBuilder
    {
        /// <summary>
        /// How far the ground is carried on past the map's edge, in metres.
        ///
        /// The caravan forms up on <see cref="Arna.Sim.Caravan.RunUp"/> metres of road
        /// behind the start line and the start is always within three tiles of the
        /// map's western edge, so that road is off the map by construction and needs
        /// ground under it. Eight metres more than the run-up, so the apron does not end
        /// exactly where the last wagon stands.
        ///
        /// It is a constant here rather than an argument at the call site because two
        /// things have to agree about it: what draws the ground, and what keeps the
        /// skyline off it (<c>TerrainDecorator.PlaceHorizon</c>). They disagreed once,
        /// and the range ended up standing on the apron.
        /// </summary>
        public const float SkirtWidth = Arna.Sim.Caravan.RunUp + 8f;

        /// <summary>A set of tiles to paint over the terrain, such as one corridor.</summary>
        public struct RouteOverlay
        {
            public IReadOnlyList<int> Tiles;
            public Color Color;

            public RouteOverlay(IReadOnlyList<int> tiles, Color color)
            {
                Tiles = tiles;
                Color = color;
            }
        }

        /// <param name="overlays">
        /// Painted in order, so later entries win where routes share tiles. Pass the
        /// fastest corridor last: where it coincides with another, the interesting
        /// fact is that the alternative is not actually an alternative there.
        /// </param>
        /// <param name="heightScale">
        /// Metres between the lowest and highest ground. Zero gives the flat map the
        /// planning view wants; the play view stands the same data up in three
        /// dimensions.
        /// </param>
        /// <param name="skirt">
        /// Metres of ground carried on past the map's edge. Zero on the planning map,
        /// which is a sheet and ends where its ground ends.
        ///
        /// The world does not. Two things want this. The obvious one is that the caravan
        /// forms up on road *behind* the start line — see <see cref="Arna.Sim.Caravan.RunUp"/>
        /// — and the start is chosen from the leftmost three columns, so that road is off
        /// the map by construction and had nothing under it. The other is that the ground
        /// simply stopped at the boundary with three hundred metres of nothing between it
        /// and the skyline, which reads as the edge of a board.
        ///
        /// It is one quad per border tile rather than more map: the colours and corner
        /// heights of the edge carried outward, flat. Nothing is scattered on it and
        /// nothing walks on it except the tail of the column at the moment the level
        /// begins, so bare ground is all it has to be — 252 quads on a 64×64 map against
        /// the 2 960 tiles that widening the grid by ten would have cost.
        /// </param>
        public static Mesh Build(TileGrid grid, float tileSize, IReadOnlyList<RouteOverlay> overlays = null,
                                 int startIndex = -1, int goalIndex = -1, float heightScale = 0f,
                                 float skirt = 0f)
        {
            int tiles = grid.TileCount;
            var vertices = new Vector3[tiles * 4];
            var normals = new Vector3[tiles * 4];
            var colors = new Color[tiles * 4];

            // Metres, not a 0..1 unwrap. The ground has no natural seam and no reason
            // to be unwrapped, so the coordinate is simply where you are standing; the
            // shader decides how often the detail repeats. Without any UV at all — as
            // this mesh had until now — no texture can be put on the ground however
            // good the texture is.
            var uvs = new Vector2[tiles * 4];
            var triangles = new int[tiles * 6];

            var painted = BuildOverlayLookup(overlays);

            for (int i = 0; i < tiles; i++)
            {
                grid.ToCoords(i, out int x, out int y);

                float x0 = x * tileSize;
                float z0 = y * tileSize;
                float x1 = x0 + tileSize;
                float z1 = z0 + tileSize;

                float h00 = 0f, h10 = 0f, h11 = 0f, h01 = 0f;
                if (heightScale > 0f)
                {
                    // Corner heights are averaged from the tiles meeting there, so the
                    // ground is continuous while the colours stay per-tile and crisp.
                    h00 = CornerHeight(grid, x, y, heightScale);
                    h10 = CornerHeight(grid, x + 1, y, heightScale);
                    h11 = CornerHeight(grid, x + 1, y + 1, heightScale);
                    h01 = CornerHeight(grid, x, y + 1, heightScale);
                }

                int v = i * 4;
                vertices[v + 0] = new Vector3(x0, h00, z0);
                vertices[v + 1] = new Vector3(x1, h10, z0);
                vertices[v + 2] = new Vector3(x1, h11, z1);
                vertices[v + 3] = new Vector3(x0, h01, z1);

                uvs[v + 0] = new Vector2(x0, z0);
                uvs[v + 1] = new Vector2(x1, z0);
                uvs[v + 2] = new Vector2(x1, z1);
                uvs[v + 3] = new Vector2(x0, z1);

                // Normals are sampled at the corners from the same height field the
                // vertices came from, so the surface shades smoothly even though no
                // vertex is shared. One normal per tile — the obvious version — steps
                // in brightness at every tile edge and renders the ground as a
                // chequerboard, which is what it looked like before.
                if (heightScale > 0f)
                {
                    normals[v + 0] = CornerNormal(grid, x, y, tileSize, heightScale);
                    normals[v + 1] = CornerNormal(grid, x + 1, y, tileSize, heightScale);
                    normals[v + 2] = CornerNormal(grid, x + 1, y + 1, tileSize, heightScale);
                    normals[v + 3] = CornerNormal(grid, x, y + 1, tileSize, heightScale);
                }
                else
                {
                    normals[v + 0] = normals[v + 1] = normals[v + 2] = normals[v + 3] = Vector3.up;
                }

                // Relief means this is being drawn as ground rather than as a diagram,
                // whether it ends up under the caravan or under a planning map.
                bool asGround = heightScale > 0f;

                if (asGround)
                {
                    // Corner colours are averaged across the tiles that meet there, the
                    // same trick the heights use. Two quads sharing an edge compute the
                    // same two corner values, so the colour runs continuously between
                    // them and the ground stops looking tiled — while a wide stretch of
                    // one terrain type still comes out its own colour, because all four
                    // tiles in the average agree.
                    //
                    // Grass does not change to forest floor along a straight line four
                    // metres from where the trees start. On the map it must, because
                    // there the line is the information.
                    colors[v + 0] = Tint(CornerColor(grid, x, y), x, y);
                    colors[v + 1] = Tint(CornerColor(grid, x + 1, y), x + 1, y);
                    colors[v + 2] = Tint(CornerColor(grid, x + 1, y + 1), x + 1, y + 1);
                    colors[v + 3] = Tint(CornerColor(grid, x, y + 1), x, y + 1);
                }
                else
                {
                    Color flat = TerrainPalette.Of(grid[i]);
                    colors[v + 0] = colors[v + 1] = colors[v + 2] = colors[v + 3] = flat;
                }

                // Markings last, over whatever ground was drawn underneath. A route is
                // a line the player traced, not a kind of terrain, so it keeps its hard
                // edge even where the ground beneath it blends — and the caller decides
                // whether there are any, which is how the play view gets a world with
                // no map furniture painted across it.
                if (painted != null && painted.TryGetValue(i, out Color route))
                    for (int k = 0; k < 4; k++) colors[v + k] = Color.Lerp(colors[v + k], route, 0.78f);

                if (i == startIndex)
                    colors[v + 0] = colors[v + 1] = colors[v + 2] = colors[v + 3] = TerrainPalette.Start;
                else if (i == goalIndex)
                    colors[v + 0] = colors[v + 1] = colors[v + 2] = colors[v + 3] = TerrainPalette.Goal;

                // Clockwise seen from above, which is front-facing in Unity's
                // left-handed space.
                int t = i * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 0;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh
            {
                name = "ArnaTerrain",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };

            if (skirt > 0f)
            {
                var moreVertices = new List<Vector3>(vertices);
                var moreNormals = new List<Vector3>(normals);
                var moreUvs = new List<Vector2>(uvs);
                var moreColors = new List<Color>(colors);
                var moreTriangles = new List<int>(triangles);

                Skirt(grid, tileSize, heightScale, skirt,
                      moreVertices, moreNormals, moreUvs, moreColors, moreTriangles);

                mesh.SetVertices(moreVertices);
                mesh.SetNormals(moreNormals);
                mesh.SetUVs(0, moreUvs);
                mesh.SetColors(moreColors);
                mesh.SetTriangles(moreTriangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Carries the ground on past the map's edge: one outward quad per border tile,
        /// and one per corner so the four aprons meet.
        ///
        /// Colours and heights come from the same corner functions the tiles use, so the
        /// seam is invisible — the apron is the edge continued rather than a different
        /// piece of ground butted against it. Flat outward on purpose: the height field
        /// has nothing to say beyond its own bounds, and inventing relief out there would
        /// put hills where the level has no idea whether hills belong.
        /// </summary>
        static void Skirt(TileGrid grid, float tileSize, float heightScale, float skirt,
                          List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
                          List<Color> colors, List<int> triangles)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                Apron(grid, tileSize, heightScale, x, 0, 0, -1, skirt,
                      vertices, normals, uvs, colors, triangles);
                Apron(grid, tileSize, heightScale, x, grid.Height - 1, 0, 1, skirt,
                      vertices, normals, uvs, colors, triangles);
            }

            for (int y = 0; y < grid.Height; y++)
            {
                Apron(grid, tileSize, heightScale, 0, y, -1, 0, skirt,
                      vertices, normals, uvs, colors, triangles);
                Apron(grid, tileSize, heightScale, grid.Width - 1, y, 1, 0, skirt,
                      vertices, normals, uvs, colors, triangles);
            }

            // The corners, where two aprons leave a square of nothing between them.
            Corner(grid, tileSize, heightScale, 0, 0, -1, -1, skirt,
                   vertices, normals, uvs, colors, triangles);
            Corner(grid, tileSize, heightScale, grid.Width - 1, 0, 1, -1, skirt,
                   vertices, normals, uvs, colors, triangles);
            Corner(grid, tileSize, heightScale, 0, grid.Height - 1, -1, 1, skirt,
                   vertices, normals, uvs, colors, triangles);
            Corner(grid, tileSize, heightScale, grid.Width - 1, grid.Height - 1, 1, 1, skirt,
                   vertices, normals, uvs, colors, triangles);
        }

        /// <summary>One border tile's outward quad, on the side <paramref name="dx"/>,
        /// <paramref name="dy"/> points at.</summary>
        static void Apron(TileGrid grid, float tileSize, float heightScale, int x, int y,
                          int dx, int dy, float skirt,
                          List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
                          List<Color> colors, List<int> triangles)
        {
            // The two corners of the tile that lie on the map's edge: the edge runs
            // across whichever way the apron points.
            int ax, ay, bx, by;

            if (dx != 0)
            {
                ax = bx = dx > 0 ? x + 1 : x;
                ay = y;
                by = y + 1;
            }
            else
            {
                ay = by = dy > 0 ? y + 1 : y;
                ax = x;
                bx = x + 1;
            }

            var inner0 = Edge(grid, tileSize, heightScale, ax, ay);
            var inner1 = Edge(grid, tileSize, heightScale, bx, by);

            var out1 = Out(inner1, dx, dy, skirt);
            var out0 = Out(inner0, dx, dy, skirt);

            var normal0 = Normal(grid, tileSize, heightScale, ax, ay);
            var normal1 = Normal(grid, tileSize, heightScale, bx, by);

            var colour0 = CornerColour(grid, ax, ay);
            var colour1 = CornerColour(grid, bx, by);

            // Each vertex carries its own normal and colour round the quad, because the
            // winding correction below may reorder them.
            Quad(new[] { inner0, inner1, out1, out0 },
                 new[] { normal0, normal1, Vector3.up, Vector3.up },
                 new[] { colour0, colour1, colour1, colour0 },
                 vertices, normals, uvs, colors, triangles);
        }

        /// <summary>The square of apron diagonally off a map corner.</summary>
        static void Corner(TileGrid grid, float tileSize, float heightScale, int x, int y,
                           int dx, int dy, float skirt,
                           List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
                           List<Color> colors, List<int> triangles)
        {
            int cx = dx > 0 ? x + 1 : x;
            int cy = dy > 0 ? y + 1 : y;

            var at = Edge(grid, tileSize, heightScale, cx, cy);

            var alongX = Out(at, dx, 0, skirt);
            var alongY = Out(at, 0, dy, skirt);
            var across = Out(at, dx, dy, skirt);

            var colour = CornerColour(grid, cx, cy);
            var normal = Normal(grid, tileSize, heightScale, cx, cy);

            Quad(new[] { at, alongY, across, alongX },
                 new[] { normal, Vector3.up, Vector3.up, Vector3.up },
                 new[] { colour, colour, colour, colour },
                 vertices, normals, uvs, colors, triangles);
        }

        static Vector3 Out(Vector3 from, int dx, int dy, float skirt)
            => new Vector3(from.x + dx * skirt, from.y, from.z + dy * skirt);

        static Vector3 Edge(TileGrid grid, float tileSize, float heightScale, int cornerX, int cornerY)
            => new Vector3(cornerX * tileSize,
                           heightScale > 0f ? CornerHeight(grid, cornerX, cornerY, heightScale) : 0f,
                           cornerY * tileSize);

        static Vector3 Normal(TileGrid grid, float tileSize, float heightScale, int cornerX, int cornerY)
            => heightScale > 0f
                ? CornerNormal(grid, cornerX, cornerY, tileSize, heightScale)
                : Vector3.up;

        /// <summary>The ground colour at a corner, tinted the way the tiles are.</summary>
        static Color CornerColour(TileGrid grid, int cornerX, int cornerY)
            => Tint(CornerColor(grid, cornerX, cornerY), cornerX, cornerY);

        /// <summary>
        /// Appends one quad, wound so it faces up whichever way its corners were listed.
        ///
        /// The winding is corrected rather than got right at each call site. There are
        /// six sides and corners to emit and the sense of "round the quad" flips with
        /// the direction each one points, so half of them came out facing down and
        /// invisible — which is a bug that looks like the apron being absent on two
        /// sides of the map and present on the other two. The shoelace area says which
        /// way a polygon is wound; matching the tiles' sign is one comparison.
        /// </summary>
        static void Quad(Vector3[] points, Vector3[] pointNormals, Color[] pointColours,
                         List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
                         List<Color> colors, List<int> triangles)
        {
            float area = 0f;
            for (int i = 0; i < 4; i++)
            {
                var from = points[i];
                var to = points[(i + 1) % 4];
                area += from.x * to.z - to.x * from.z;
            }

            // Wound the other way round: swapping one opposite pair reverses it, and
            // everything that belongs to a vertex travels with it.
            if (area < 0f)
            {
                Swap(points, 1, 3);
                Swap(pointNormals, 1, 3);
                Swap(pointColours, 1, 3);
            }

            int v = vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(points[i]);
                normals.Add(pointNormals[i]);
                colors.Add(pointColours[i]);
                uvs.Add(new Vector2(points[i].x, points[i].z));
            }

            triangles.Add(v + 0); triangles.Add(v + 2); triangles.Add(v + 1);
            triangles.Add(v + 0); triangles.Add(v + 3); triangles.Add(v + 2);
        }

        static void Swap<T>(T[] values, int a, int b)
        {
            var held = values[a];
            values[a] = values[b];
            values[b] = held;
        }

        /// <summary>
        /// Ground normal at a tile corner, from the slope of the height field either
        /// side of it. Central differences rather than the quad's own face normal:
        /// the face normal is constant across a tile and therefore visible as an edge.
        /// </summary>
        static Vector3 CornerNormal(TileGrid grid, int cornerX, int cornerY,
                                    float tileSize, float heightScale)
        {
            float west = CornerHeight(grid, cornerX - 1, cornerY, heightScale);
            float east = CornerHeight(grid, cornerX + 1, cornerY, heightScale);
            float south = CornerHeight(grid, cornerX, cornerY - 1, heightScale);
            float north = CornerHeight(grid, cornerX, cornerY + 1, heightScale);

            return new Vector3((west - east) * 0.5f, tileSize, (south - north) * 0.5f).normalized;
        }

        /// <summary>
        /// Ground colour at a tile corner, averaged over the tiles meeting there —
        /// except at water, which keeps its own colour.
        ///
        /// Blending is what stops the ground looking tiled, and grass really does fade
        /// into forest floor. A shoreline does not: it is a line, in the world and on
        /// the player's plan. Averaged with the grass either side of it a river came
        /// out as a vague darker smear that could as easily have been the shadow of a
        /// hill, and water is impassable — a route drawn across one is not a route.
        ///
        /// Water takes a corner only on a majority, never on a tie. Letting a single
        /// water tile claim the corner widened every river by half a tile in each
        /// direction, which swallowed the fords — and a ford is the one tile of a river
        /// the caravan may cross, so painting it as river hides the crossing and leaves
        /// the wagons apparently driving through the water.
        /// </summary>
        static Color CornerColor(TileGrid grid, int cornerX, int cornerY)
        {
            float r = 0f, g = 0f, b = 0f;
            int tiles = 0, water = 0;
            bool road = false;

            for (int dy = -1; dy <= 0; dy++)
            {
                for (int dx = -1; dx <= 0; dx++)
                {
                    int x = cornerX + dx;
                    int y = cornerY + dy;
                    if (!grid.InBounds(x, y)) continue;

                    tiles++;

                    var terrain = grid[x, y];
                    if (terrain == TerrainType.Water) { water++; continue; }
                    if (terrain == TerrainType.Road) road = true;

                    var c = TerrainPalette.OfGround(terrain);
                    r += c.r; g += c.g; b += c.b;
                }
            }

            if (tiles == 0) return Color.black;

            // A road wins its corners outright, unlike water, which needs a majority.
            // Roads are one tile wide, so no corner is ever surrounded by four of them
            // and the averaging diluted every road to half grass — a road that vanishes
            // into the meadow it crosses. Claiming the corner widens it by half a tile
            // each side, which is exactly what a track needs to stay continuous. Water
            // cannot have the same rule: there it swallowed the fords.
            if (road) return TerrainPalette.OfGround(TerrainType.Road);
            if (water * 2 > tiles) return TerrainPalette.OfGround(TerrainType.Water);

            int land = tiles - water;
            return new Color(r / land, g / land, b / land, 1f);
        }

        /// <summary>How deep a riverbed sits below the ground around it.</summary>
        const float WaterDepth = 0.2f;

        /// <summary>
        /// Ground height at a tile corner, with any riverbed dug into it.
        ///
        /// The depth is spread over the corners in proportion to how much water meets
        /// there, which matters more than it sounds: dropping all four corners of a
        /// water tile by a fixed amount leaves its neighbours where they were, and the
        /// two quads no longer share an edge. The gap between them is a hole, and what
        /// shows through a hole in the ground is the sky — which is exactly what those
        /// pale blue bands along every river turned out to be. Sharing the corner
        /// makes the bank a slope instead of a cliff with a gap in it.
        /// </summary>
        static float CornerHeight(TileGrid grid, int cornerX, int cornerY, float heightScale)
        {
            float elevation = grid.CornerElevation(cornerX, cornerY) * heightScale;

            int water = 0, tiles = 0;
            for (int dy = -1; dy <= 0; dy++)
            {
                for (int dx = -1; dx <= 0; dx++)
                {
                    int x = cornerX + dx;
                    int y = cornerY + dy;
                    if (!grid.InBounds(x, y)) continue;

                    tiles++;
                    if (grid[x, y] == TerrainType.Water) water++;
                }
            }

            if (tiles == 0 || water == 0) return elevation;
            return elevation - heightScale * WaterDepth * water / tiles;
        }

        /// <summary>
        /// A small brightness shift keyed to a grid corner. Deterministic, because a
        /// map that mottled itself differently on each run would break the promise
        /// that a seed is a level.
        /// </summary>
        static Color Tint(Color c, int cornerX, int cornerY)
        {
            unchecked
            {
                int h = cornerX * 73856093 ^ cornerY * 19349663;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;

                float shade = 1f + ((h & 0xFFFF) / 65535f - 0.5f) * 0.11f;
                return new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
            }
        }

        static Dictionary<int, Color> BuildOverlayLookup(IReadOnlyList<RouteOverlay> overlays)
        {
            if (overlays == null || overlays.Count == 0) return null;

            var lookup = new Dictionary<int, Color>();
            foreach (var overlay in overlays)
            {
                if (overlay.Tiles == null) continue;
                for (int i = 0; i < overlay.Tiles.Count; i++)
                    lookup[overlay.Tiles[i]] = overlay.Color;
            }
            return lookup.Count == 0 ? null : lookup;
        }
    }
}
