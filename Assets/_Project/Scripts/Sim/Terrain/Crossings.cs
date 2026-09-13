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
