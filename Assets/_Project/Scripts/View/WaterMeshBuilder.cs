using Arna.Sim;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arna.View
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

        static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");

        /// <summary>The colour of standing water seen from above, with the depth to see into it.</summary>
        public static readonly Color Surface = new Color(0.16f, 0.34f, 0.46f, 0.80f);

        /// <summary>
        /// Whether this terrain is under water. Fords included: a ford is a shallow
        /// place in a river, not a hole in it, and leaving them dry cut every river into
        /// pieces with a green stripe where the crossing is.
        /// </summary>
        public static bool Wet(TerrainType terrain)
            => terrain == TerrainType.Water || terrain == TerrainType.Ford;

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

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            // One vertex per shared corner, made on demand. The key is the corner's grid
            // coordinate, which is what makes neighbouring tiles agree.
            var corners = new Dictionary<int, int>();
            int stride = grid.Width + 1;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!Wet(grid[grid.ToIndex(x, y)])) continue;

                    int a = Corner(grid, corners, vertices, uvs, x, y, tileSize, heightScale, stride);
                    int b = Corner(grid, corners, vertices, uvs, x + 1, y, tileSize, heightScale, stride);
                    int c = Corner(grid, corners, vertices, uvs, x + 1, y + 1, tileSize, heightScale, stride);
                    int d = Corner(grid, corners, vertices, uvs, x, y + 1, tileSize, heightScale, stride);

                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                }
            }

            if (triangles.Count == 0) return null;

            var mesh = new Mesh { name = "Water" };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// One shared corner.
        ///
        /// Its height is the lowest bed of the wet tiles meeting there. Lowest rather
        /// than averaged, because the bank is what the surface must not climb: average a
        /// riverside corner with the meadow beside it and the waterline rides up the
        /// grass, which is the artefact this whole builder exists to remove.
        /// </summary>
        static int Corner(TileGrid grid, Dictionary<int, int> corners, List<Vector3> vertices,
                          List<Vector2> uvs, int x, int y, float tileSize, float heightScale,
                          int stride)
        {
            int key = y * stride + x;
            if (corners.TryGetValue(key, out int found)) return found;

            float lowest = float.MaxValue;

            for (int dy = -1; dy <= 0; dy++)
            {
                for (int dx = -1; dx <= 0; dx++)
                {
                    int tx = x + dx, ty = y + dy;
                    if (!grid.InBounds(tx, ty)) continue;
                    if (!Wet(grid[grid.ToIndex(tx, ty)])) continue;

                    float bed = grid.SurfaceElevation((tx + 0.5f) * tileSize, (ty + 0.5f) * tileSize)
                              * heightScale;

                    if (bed < lowest) lowest = bed;
                }
            }

            if (lowest == float.MaxValue) lowest = 0f;

            int index = vertices.Count;
            vertices.Add(new Vector3(x * tileSize, lowest + Depth, y * tileSize));
            uvs.Add(new Vector2(x * 0.5f, y * 0.5f));
            corners[key] = index;

            return index;
        }

        /// <summary>
        /// The water material: URP Lit, turned transparent in code.
        ///
        /// Transparency on the Lit shader is four properties and a keyword rather than
        /// one flag, and setting the colour's alpha alone does nothing at all — which is
        /// how an opaque sheet went out looking like paint over the river.
        /// </summary>
        public static Material Material()
        {
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
            water.SetColor(BaseColourId, Surface);

            return water;
        }
    }
}
