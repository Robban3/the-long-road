using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// Whether a list of tiles is a road a caravan can actually be put on.
    ///
    /// The route the player draws is solved on the planning screen and handed to the run
    /// as bare tile indices, with nothing attached that says which map they were solved
    /// against. Every guard on the drawing side is real — RoutePlanner refuses an
    /// impassable waypoint, GridPathfinder refuses an impassable step — and every one of
    /// them runs against the planning screen's grid. Between there and the run the tiles
    /// are just numbers.
    ///
    /// <see cref="TheVeil.Gen.LevelMaps"/> is what makes those two grids the same grid, and
    /// this is the check that says so out loud if they are ever not. Cheap — one pass over
    /// a few hundred tiles, once per run — against a fault whose symptom was a caravan
    /// standing in a lake in a run that could not end.
    ///
    /// Engine-free on purpose: the check belongs with the simulation, the shouting belongs
    /// with the app.
    /// </summary>
    public static class RouteCheck
    {
        /// <summary>
        /// Whether every tile is passable and every step is to a neighbour.
        /// </summary>
        /// <param name="firstBadTile">
        /// The tile that failed, or -1. Named rather than counted, because "the route is
        /// broken" and "the route is broken at tile 2371, which is water" are minutes
        /// apart when somebody has to find out why.
        /// </param>
        public static bool Walkable(TileGrid grid, IReadOnlyList<int> route, out int firstBadTile)
        {
            firstBadTile = -1;

            if (grid == null || route == null || route.Count == 0) return false;

            for (int i = 0; i < route.Count; i++)
            {
                int tile = route[i];

                if (tile < 0 || tile >= grid.TileCount)
                {
                    firstBadTile = tile;
                    return false;
                }

                if (!TerrainTable.IsPassable(grid[tile]))
                {
                    firstBadTile = tile;
                    return false;
                }

                if (i == 0) continue;

                // Eight-connected, matching GridPathfinder: a solved route steps corner to
                // corner as well as edge to edge. A gap wider than that means these tiles
                // were never a path on this grid — which is the shape the fault took, the
                // indices being meaningful somewhere else.
                grid.ToCoords(route[i - 1], out int px, out int py);
                grid.ToCoords(tile, out int x, out int y);

                int dx = x - px, dy = y - py;
                if (dx < 0) dx = -dx;
                if (dy < 0) dy = -dy;

                if (dx > 1 || dy > 1 || (dx == 0 && dy == 0))
                {
                    firstBadTile = tile;
                    return false;
                }
            }

            return true;
        }
    }
}
