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

        /// <summary>
        /// Rounds the corners off a path that is made of tiles.
        ///
        /// <b>Because a drawn route came out as a staircase and a road does not look like
        /// that.</b> The planner walks a grid, and where two ways cost the same it takes
        /// whichever it reached first: a long diagonal comes out as a clean line, and the
        /// next stretch of the same road comes out as a sawtooth of four-metre steps -
        /// right, down, right, down - because stepping is free. Half the drawn line looked
        /// like a line and half looked like a zip, which is what was reported as the map
        /// going strange when a waypoint goes down.
        ///
        /// Corner cutting, twice: each segment is replaced by its own quarter and
        /// three-quarter points, which rounds every corner and leaves a straight run
        /// straight. The ends are kept exactly, because they are the start and the goal.
        ///
        /// <b>This is the drawing and not the road.</b> The caravan still walks the tiles
        /// the planner chose, and the numbers on the panel are still those tiles. What
        /// moves is at most half a tile of yellow paint, and it moves towards the inside
        /// of a corner the column is going to cut anyway.
        /// </summary>
        static List<Vector3> Smoothed(List<Vector3> centres)
        {
            for (int pass = 0; pass < Passes && centres.Count > 2; pass++)
            {
                var cut = new List<Vector3>(centres.Count * 2) { centres[0] };

                for (int i = 0; i < centres.Count - 1; i++)
                {
                    cut.Add(Vector3.Lerp(centres[i], centres[i + 1], Corner));
                    cut.Add(Vector3.Lerp(centres[i], centres[i + 1], 1f - Corner));
                }

                cut.Add(centres[centres.Count - 1]);
                centres = cut;
            }

            return centres;
        }

        /// <summary>How many times the corners are cut.</summary>
        // Two. One still shows the step, and three starts to pull the line off a bend it
        // is meant to be describing.
        const int Passes = 2;

        /// <summary>How far along each segment the new corner points are taken.</summary>
        const float Corner = 0.25f;

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

            centres = Smoothed(centres);

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
