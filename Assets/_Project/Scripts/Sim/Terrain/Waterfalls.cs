using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// The step a river falls over, cut into the ground before anything is drawn.
    ///
    /// <b>Measured first: there was nothing to fall over.</b> Across the fifty built levels
    /// a river drops at most 2.6 m from one tile to the next, and on most of them less than
    /// two - so a waterfall could not be found by looking for one, and a sheet of falling
    /// water stood in a step that small lies down flat on the water instead. The country
    /// had no cliff in it because nothing ever cut one.
    ///
    /// So the upper part of the river's course is lifted onto a shelf: every wet tile above
    /// the chosen step, and the ground either side of them, raised by <see cref="Rise"/>
    /// with a taper out into the fields so it reads as high ground rather than as a wall
    /// dropped on a meadow. The river then comes off the edge of it.
    ///
    /// <b>Elevation only, and that is why this is safe.</b> Nothing in the simulation reads
    /// the height of the ground: a run is tiles, distances and who is standing on them, and
    /// the same map plays exactly the same before and after this. What it changes is what
    /// the eye sees - which is the whole point, and also the reason it is done here in Sim
    /// rather than in the view: the planning map and the run must see one country.
    /// </summary>
    public static class Waterfalls
    {
        /// <summary>How far the shelf stands above the water below it, in grid units.</summary>
        /// <remarks>
        /// The view multiplies elevation by its height scale (14 m at the time of writing),
        /// so 0.45 is a drop of about six metres: high enough to read as a fall from the
        /// camera this game is played at, low enough that the road up onto the shelf is a
        /// climb rather than a cliff.
        /// </remarks>
        public const float Rise = 0.45f;

        /// <summary>How far the shelf reaches either side of the river, in tiles.</summary>
        public const int Reach = 6;

        /// <summary>How many tiles the shelf takes to come back down to the fields.</summary>
        public const int Taper = 5;

        /// <summary>The tile the water comes over, or -1 where this level has no fall.</summary>
        public static int Step(LevelMap map)
        {
            var river = Course(map);
            if (river.Count < Least) return -1;

            // A quarter of the way down the river from its head: far enough in that the
            // shelf has ground under it, far enough from the mouth that the fall is not on
            // the map's edge.
            return river[river.Count / 4];
        }

        /// <summary>
        /// Lifts the ground above the step. Call once, after the map is generated and before
        /// anything reads its elevation.
        /// </summary>
        public static void Carve(LevelMap map)
        {
            if (map?.Grid == null) return;

            var river = Course(map);
            if (river.Count < Least) return;

            int step = river[river.Count / 4];
            var grid = map.Grid;
            grid.ToCoords(step, out _, out int stepRow);

            // Everything above the step, by row: the river runs down the grid, so the rows
            // above the step are the shelf and the rows below it are the country it falls
            // into. Taken by row rather than by walking the water, because the banks have to
            // come up with the river or the water stands in a trench.
            for (int y = 0; y < grid.Height; y++)
            {
                if (y <= stepRow) continue;

                for (int x = 0; x < grid.Width; x++)
                {
                    int tile = grid.ToIndex(x, y);

                    float lift = Rise * Share(grid, x, y, river, stepRow);
                    if (lift <= 0f) continue;

                    grid.SetElevation(tile, grid.Elevation(tile) + lift);
                }
            }
        }

        /// <summary>
        /// How much of the full rise this tile takes: all of it on the shelf, none of it out
        /// in the fields, and a smooth ramp between the two.
        /// </summary>
        static float Share(TileGrid grid, int x, int y, List<int> river, int stepRow)
        {
            // <b>All of it, at once, in the row above the step.</b> It was brought in over
            // five rows so the shelf would have a shoulder, and that spread the drop over
            // five tiles: measured afterwards, 4-1's steepest step was 1.7 m - the same as
            // before the shelf was cut. A fall is a cliff and a cliff has no shoulder; what
            // softens it is the taper sideways, away from the water.
            int across = Across(grid, x, y, river);
            if (across <= Reach) return 1f;

            if (across >= Reach + Taper) return 0f;

            return 1f - (across - Reach) / (float)Taper;
        }

        /// <summary>How far this tile is from the river, in tiles, up to what matters.</summary>
        static int Across(TileGrid grid, int x, int y, List<int> river)
        {
            int nearest = Reach + Taper;

            foreach (int tile in river)
            {
                grid.ToCoords(tile, out int rx, out int ry);
                if (ry - y > nearest || y - ry > nearest) continue;

                int away = rx > x ? rx - x : x - rx;
                if (away < nearest) nearest = away;
            }

            return nearest;
        }

        /// <summary>The river, from its head to its mouth, as tiles.</summary>
        static List<int> Course(LevelMap map)
        {
            var river = new List<int>();
            var grid = map?.Grid;
            if (grid == null) return river;

            for (int y = grid.Height - 1; y >= 0; y--)
                for (int x = 0; x < grid.Width; x++)
                {
                    var terrain = grid[x, y];
                    if (terrain == TerrainType.Water || terrain == TerrainType.Ford)
                        river.Add(grid.ToIndex(x, y));
                }

            return river;
        }

        /// <summary>How many wet tiles a level needs before it is worth cutting a step.</summary>
        const int Least = 30;
    }
}
