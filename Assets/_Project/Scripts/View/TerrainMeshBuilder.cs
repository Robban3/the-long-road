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
    /// A 64x64 grid yields 16 384 vertices, comfortably inside one mesh. The chunked
    /// layout described in docs/technical-design.md §5 only starts paying off once
    /// parts of the map can be culled, which is not the case for an overview that is
    /// always fully visible.
    /// </summary>
    public static class TerrainMeshBuilder
    {
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
        /// <summary>Direction the baked relief shading comes from.</summary>
        static readonly Vector3 LightDirection = new Vector3(0.35f, 0.85f, -0.4f).normalized;

        /// <param name="heightScale">
        /// Metres between the lowest and highest ground. Zero gives the flat map the
        /// planning view wants; the play view stands the same data up in three
        /// dimensions.
        /// </param>
        public static Mesh Build(TileGrid grid, float tileSize, IReadOnlyList<RouteOverlay> overlays = null,
                                 int startIndex = -1, int goalIndex = -1, float heightScale = 0f)
        {
            int tiles = grid.TileCount;
            var vertices = new Vector3[tiles * 4];
            var colors = new Color[tiles * 4];
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
                    h00 = grid.CornerElevation(x, y) * heightScale;
                    h10 = grid.CornerElevation(x + 1, y) * heightScale;
                    h11 = grid.CornerElevation(x + 1, y + 1) * heightScale;
                    h01 = grid.CornerElevation(x, y + 1) * heightScale;

                    // Water sits in its bed rather than on top of the ground.
                    if (grid[i] == TerrainType.Water)
                    {
                        float sink = heightScale * 0.12f;
                        h00 -= sink; h10 -= sink; h11 -= sink; h01 -= sink;
                    }
                }

                int v = i * 4;
                vertices[v + 0] = new Vector3(x0, h00, z0);
                vertices[v + 1] = new Vector3(x1, h10, z0);
                vertices[v + 2] = new Vector3(x1, h11, z1);
                vertices[v + 3] = new Vector3(x0, h01, z1);

                Color c = TerrainPalette.Of(grid[i]);
                if (painted != null && painted.TryGetValue(i, out Color overlay))
                    c = Color.Lerp(c, overlay, 0.78f);
                if (i == startIndex) c = TerrainPalette.Start;
                else if (i == goalIndex) c = TerrainPalette.Goal;

                // Relief shading baked into the vertex colours. The terrain shader is
                // unlit — deliberately, because the planning map must not be shaded —
                // so the play view gets its sense of slope from the geometry here
                // instead, at no runtime cost.
                if (heightScale > 0f)
                {
                    var across = new Vector3(tileSize, h10 - h00, 0f);
                    var along = new Vector3(0f, h01 - h00, tileSize);
                    var normal = Vector3.Cross(along, across).normalized;

                    float lambert = Mathf.Clamp01(Vector3.Dot(normal, LightDirection));
                    float shade = 0.72f + 0.28f * lambert;
                    c = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
                }

                colors[v + 0] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c;

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
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
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
