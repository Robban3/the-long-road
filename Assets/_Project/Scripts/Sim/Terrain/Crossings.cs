using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// Where a level can be crossed: the fords that have dry ground to land on at both
    /// ends, counted once each however wide they are.
    ///
    /// <b>One answer, used by the two places that must agree.</b> The generator decides
    /// whether a map may ship, and the view decides which crossing gets the bridge. They
    /// counted differently once and it showed on 3-1: the map was accepted with what the
    /// generator called four crossings, three of which a lake had grown over, so every
    /// route queued for the one that was left and the bridge stood in open water with its
    /// ends in the air.
    ///
    /// Engine-free, in Sim, because both callers can see Sim and neither can see the
    /// other.
    /// </summary>
    public static class Crossings
    {
        /// <summary>
        /// How far apart two ford tiles must be to count as separate crossings, in tiles.
        ///
        /// Four. A ford is several tiles wide, and counting each of its tiles as its own
        /// crossing says a level has six ways over a river it has two ways over — and
        /// builds a pier when the bridge is placed on each of them.
        /// </summary>
        public const float Apart = 4f;

        /// <summary>How far a bank may be from the crossing, in tiles.</summary>
        // Three. A crossing is water you can wade; wider than that is a lake with a
        // shallow spot in it, and nothing the caravan can drive over.
        public const int BankReach = 3;

        /// <summary>
        /// Tiles of straight approach held on each bank. See Square.
        ///
        /// Eight, which is thirty-two metres. It was four, and four was reasoned from the
        /// bridge: the deck plus its landings, so the column would be straight by the time
        /// a wheel touched timber. Watched, that is not the thing that looks wrong. The
        /// caravan is a column of wagons twenty-odd metres long, and a lead wagon that is
        /// straight while the three behind it are still coming round the turn reads as the
        /// whole train slewing onto the bridge sideways.
        ///
        /// So the measure is the length of what is crossing, not the length of what is
        /// being crossed. Eight tiles is the column with room to spare, and the turn the
        /// road has to make is made before the first wagon reaches it rather than under it.
        /// </summary>
        public const int RunUp = 8;

        /// <summary>
        /// The same road, driven square at the water.
        ///
        /// <b>The bridge answers to the river and the road did not answer to the
        /// bridge.</b> TerrainDecorator.Bridge lays its deck along the ford — it has to,
        /// because the planning map is drawn before the player draws a line and the two
        /// have to show the same country — and it says so in its own note: *"The column
        /// crabs a few degrees across the deck where the line meets the water at a slant;
        /// that is the lesser fault by far."* That was true while the bridge was something
        /// the column went over on its way somewhere. Watched from the ground it is a
        /// wagon climbing onto a bridge sideways.
        ///
        /// So the road is straightened instead of the bridge turned, which is also what a
        /// driver does: you line the team up before the planks, you do not take a bridge
        /// at an angle. A ford is cut as a run of tiles straight across its river — see
        /// Crossing in the decorator — so driving it square means holding one row of
        /// tiles from the near bank to the far one.
        ///
        /// Everything away from the water is left exactly as it was drawn. This is a
        /// local correction at the crossings and nowhere else: the route the player drew
        /// is theirs, and the only part of it being argued with is the part that would
        /// have put a wheel off the deck.
        /// </summary>
        public static IReadOnlyList<int> Square(TileGrid grid, IReadOnlyList<int> route)
        {
            if (grid == null || route == null || route.Count < 3) return route;

            var straight = new List<int>(route);
            bool changed = false;

            for (int i = 0; i < straight.Count; i++)
            {
                if (grid[straight[i]] != TerrainType.Ford) continue;

                // The whole run of water this step belongs to.
                int from = i;
                while (from > 0 && grid[straight[from - 1]] == TerrainType.Ford) from--;

                int to = i;
                while (to + 1 < straight.Count && grid[straight[to + 1]] == TerrainType.Ford) to++;

                // The row to hold: the one the middle of the crossing is already on, so
                // the correction is shared between the two banks rather than dragging the
                // whole approach to whichever side happened to be entered first.
                grid.ToCoords(straight[(from + to) / 2], out _, out int row);

                for (int t = from; t <= to; t++)
                {
                    grid.ToCoords(straight[t], out int x, out int y);
                    if (y == row) continue;

                    if (!grid.InBounds(x, row) || !grid.IsPassable(x, row)) continue;

                    straight[t] = grid.ToIndex(x, row);
                    changed = true;
                }

                // And a run-up on each bank, so the column is already straight when it
                // reaches the planks instead of turning onto them.
                //
                // <b>Four tiles, not one.</b> A bridge is not the width of its ford: the
                // deck spans the water plus a landing at each end, so it covers several
                // tiles either side of the wet ones. With a single tile of run-up the
                // column was straight for one tile and then turned — on the deck, which
                // is exactly the thing this was written to stop and exactly what it still
                // looked like. Sixteen metres clears any landing the decorator builds.
                for (int b = 1; b <= RunUp; b++)
                {
                    changed |= Aim(grid, straight, from - b, row);
                    changed |= Aim(grid, straight, to + b, row);
                }

                i = to;
            }

            return changed ? straight : route;
        }

        /// <summary>
        /// Pulls one tile of a route onto a row, if that leaves a road somebody could drive.
        ///
        /// <b>The neighbour check is the whole of it.</b> Without it this drags tiles onto
        /// the row from wherever they happen to be, and a tile six rows away lands six rows
        /// from the one before it: the route stops being a walk and becomes a list of
        /// places, with a diagonal leap across whatever lies between. On 1-4 that was a
        /// jump from (27,42) to (28,48) — straight over the water the bridge is there to
        /// cross — and two tiles snapped onto the same square besides.
        ///
        /// So a tile only moves if it still touches both its neighbours afterwards. What
        /// cannot be straightened without tearing the road is left crooked, which is the
        /// right answer: a road that bends is a road, and a road that teleports is not.
        /// </summary>
        static bool Aim(TileGrid grid, List<int> route, int at, int row)
        {
            if (at < 0 || at >= route.Count) return false;

            grid.ToCoords(route[at], out int x, out int y);
            if (y == row) return false;

            if (!grid.InBounds(x, row) || !grid.IsPassable(x, row)) return false;

            int moved = grid.ToIndex(x, row);

            if (!Touches(grid, moved, route, at - 1)) return false;
            if (!Touches(grid, moved, route, at + 1)) return false;

            route[at] = moved;
            return true;
        }

        /// <summary>Whether a tile is a single step from the route's tile at an index.</summary>
        static bool Touches(TileGrid grid, int tile, List<int> route, int at)
        {
            if (at < 0 || at >= route.Count) return true;

            grid.ToCoords(tile, out int x, out int y);
            grid.ToCoords(route[at], out int ox, out int oy);

            return System.Math.Abs(x - ox) <= 1 && System.Math.Abs(y - oy) <= 1;
        }

        /// <summary>Every crossing on this map, one tile per crossing.</summary>
        public static List<int> All(TileGrid grid)
        {
            var found = new List<int>();
            if (grid == null) return found;

            for (int i = 0; i < grid.TileCount; i++)
            {
                if (grid[i] != TerrainType.Ford) continue;
                if (!FarEnough(grid, i, found)) continue;
                if (!Spans(grid, i)) continue;

                found.Add(i);
            }

            return found;
        }

        public static int Count(TileGrid grid) => All(grid).Count;

        /// <summary>
        /// Whether this crossing has ground to land on: dry tiles within
        /// <see cref="BankReach"/> on two opposite sides.
        /// </summary>
        public static bool Spans(TileGrid grid, int tile)
        {
            grid.ToCoords(tile, out int x, out int y);

            return (Bank(grid, x, y, -1, 0) && Bank(grid, x, y, 1, 0))
                || (Bank(grid, x, y, 0, -1) && Bank(grid, x, y, 0, 1));
        }

        static bool Bank(TileGrid grid, int x, int y, int dx, int dy)
        {
            for (int step = 1; step <= BankReach; step++)
            {
                int nx = x + dx * step;
                int ny = y + dy * step;

                if (!grid.InBounds(nx, ny)) return false;

                var terrain = grid[nx, ny];
                if (terrain == TerrainType.Water || terrain == TerrainType.Ford) continue;

                return true;
            }

            return false;
        }

        static bool FarEnough(TileGrid grid, int tile, List<int> found)
        {
            grid.ToCoords(tile, out int x, out int y);

            foreach (int other in found)
            {
                grid.ToCoords(other, out int ox, out int oy);

                float dx = x - ox, dy = y - oy;
                if (dx * dx + dy * dy < Apart * Apart) return false;
            }

            return true;
        }
    }
}
