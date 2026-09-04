using System.Collections.Generic;
using TheVail.Sim;
using UnityEngine;

namespace TheVail.View
{
    /// <summary>
    /// Flat coloured discs laid on the planning map: where a group stands, where the
    /// crows are circling.
    ///
    /// A mark on a map is not a thing in the world, and it is not drawn like one. These
    /// carry their colour in their vertices and go through the same flat unlit material
    /// the drawn routes use — no light, no shadow, no falling into the terrain's own
    /// shading. A marker that darkens because it happens to lie on a north slope is a
    /// marker that says something about the slope.
    ///
    /// One mesh for all of them, because a map with twelve groups and eighty crow
    /// flocks on it should not cost ninety draw calls to say so.
    /// </summary>
    public static class MapMarkerBuilder
    {
        public struct Marker
        {
            public int Tile;
            public Color Color;
            public float Radius;

            public Marker(int tile, Color color, float radius)
            {
                Tile = tile;
                Color = color;
                Radius = radius;
            }
        }

        /// <summary>Corners in a disc. Twelve is round enough at map scale and cheap.</summary>
        const int Sides = 12;

        /// <summary>
        /// Metres the disc floats above the ground it marks.
        ///
        /// Enough to clear the grass, which is 0.7 m and stands on the same ground. A
        /// marker half-buried in a tuft is worse than no marker, because the eye reads
        /// the gap rather than the mark.
        /// </summary>
        const float Lift = 0.9f;

        /// <summary>How much darker the rim is than the fill.</summary>
        const float RimShade = 0.45f;

        /// <summary>Rim thickness as a share of the radius.</summary>
        const float RimShare = 0.24f;

        public static Mesh Build(TileGrid grid, IReadOnlyList<Marker> markers, float heightScale)
        {
            if (grid == null || markers == null || markers.Count == 0) return null;

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            foreach (var marker in markers)
            {
                if (marker.Tile < 0 || marker.Tile >= grid.TileCount) continue;

                var centre = Vec2.FromTile(grid, marker.Tile);
                float y = grid.SurfaceElevation(centre.X, centre.Y) * heightScale + Lift;

                var rim = marker.Color * RimShade;
                rim.a = marker.Color.a;

                // Rim first so the fill lands on top of it where they meet. Both are flat
                // and unlit, so the only thing deciding which shows is draw order within
                // the mesh and a hair of height.
                Disc(vertices, colors, triangles, centre, y, marker.Radius, rim);
                Disc(vertices, colors, triangles, centre, y + 0.05f,
                     marker.Radius * (1f - RimShare), marker.Color);
            }

            if (triangles.Count == 0) return null;

            var mesh = new Mesh { name = "MapMarkers" };
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Disc(List<Vector3> vertices, List<Color> colors, List<int> triangles,
                         Vec2 centre, float y, float radius, Color color)
        {
            int hub = vertices.Count;

            vertices.Add(new Vector3(centre.X, y, centre.Y));
            colors.Add(color);

            for (int i = 0; i <= Sides; i++)
            {
                float angle = i / (float)Sides * Mathf.PI * 2f;

                vertices.Add(new Vector3(centre.X + Mathf.Cos(angle) * radius, y,
                                         centre.Y + Mathf.Sin(angle) * radius));
                colors.Add(color);
            }

            for (int i = 0; i < Sides; i++)
            {
                triangles.Add(hub);
                triangles.Add(hub + i + 2);
                triangles.Add(hub + i + 1);
            }
        }
    }
}
