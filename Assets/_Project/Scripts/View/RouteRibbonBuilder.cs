using System.Collections.Generic;
using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.View
{
    /// <summary>
    /// Draws a route as a ribbon laid over the ground.
    ///
    /// The plan used to paint the corridor's tiles instead. A tile is four metres
    /// across, so a route came out as a four-metre band of flat colour stepping
    /// diagonally across the country — which reads as a coloured region rather than as
    /// a line someone drew, and buries the terrain it crosses. Comparing two routes is
    /// the central decision of the game (docs/GDD.md §3), and it is made by looking at
    /// this picture.
    ///
    /// A ribbon follows the same tiles but is as wide as it needs to be, so the ground
    /// underneath still shows either side of it.
    /// </summary>
    public static class RouteRibbonBuilder
    {
        /// <summary>Metres above the surface. Enough to clear it, not enough to float.</summary>
        public const float Lift = 0.35f;

        public static Mesh Build(TileGrid grid, IReadOnlyList<int> tiles, Color color,
                                 float heightScale, float width)
        {
            if (tiles == null || tiles.Count < 2) return null;

            var centres = new List<Vector3>(tiles.Count);
            foreach (int tile in tiles)
            {
                var position = Vec2.FromTile(grid, tile);

                // Sampled the way the ground mesh is built, so the ribbon follows the
                // hills rather than cutting through them.
                float y = grid.SurfaceElevation(position.X, position.Y) * heightScale + Lift;
                centres.Add(new Vector3(position.X, y, position.Y));
            }

            int count = centres.Count;
            var vertices = new Vector3[count * 2];
            var colors = new Color[count * 2];
            var triangles = new int[(count - 1) * 6];

            for (int i = 0; i < count; i++)
            {
                // Direction from the neighbours rather than from one segment, so the
                // ribbon miters through corners instead of pinching at them.
                var previous = centres[Mathf.Max(i - 1, 0)];
                var next = centres[Mathf.Min(i + 1, count - 1)];

                var forward = new Vector3(next.x - previous.x, 0f, next.z - previous.z);
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();

                var side = new Vector3(forward.z, 0f, -forward.x) * (width * 0.5f);

                vertices[i * 2 + 0] = centres[i] - side;
                vertices[i * 2 + 1] = centres[i] + side;
                colors[i * 2 + 0] = color;
                colors[i * 2 + 1] = color;
            }

            for (int i = 0; i < count - 1; i++)
            {
                int v = i * 2;
                int t = i * 6;

                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "TheVeilRoute" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
