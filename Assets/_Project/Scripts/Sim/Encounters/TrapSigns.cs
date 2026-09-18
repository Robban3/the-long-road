using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// The bones beside every trap: the warning that something is there, without saying
    /// what.
    ///
    /// <b>Around each trap, before it springs, and on the planning map as well.</b> That
    /// is the design and it has been the design from the first day - the landscape is how
    /// a player judges a road, and a skeleton in the grass is the landscape saying that
    /// somebody did not get past here. It was built as something else: one sign per six
    /// tiles of trap field, stood up to three tiles off, then pushed further still if it
    /// landed on a road. Six traps in a throat got one heap twelve metres away, and a trap
    /// on the road the player was driving had its warning moved off that road - so the
    /// bones were where nobody drove, and the ones anybody saw were the ones the view puts
    /// down after a trap has already gone off.
    ///
    /// The old note here argued that bones at the trap give its position away. They give
    /// away that something is there. What it is - a pit, a snare, a fall of logs, or only
    /// an old grave - is still found by driving onto it or sending a scout, which is the
    /// risk the detection system exists to keep.
    ///
    /// Never on the trap tile itself: the trap is still a trap, and the bones are around
    /// it. In the simulation because both views read it, and a warning drawn on the map
    /// and missing in the country - or the other way about - is the fault this class was
    /// first written to close.
    /// </summary>
    public static class TrapSigns
    {
        public static List<int> Sites(LevelMap map)
        {
            var traps = map?.Encounters?.Traps;
            if (traps == null || traps.Count == 0) return null;

            var trapTiles = new HashSet<int>();
            foreach (var trap in traps) trapTiles.Add(trap.Tile);

            var rng = new DeterministicRandom(map.Seed ^ 0x2117);
            var taken = new HashSet<int>();
            var sites = new List<int>();
            var ring = new List<int>(8);

            foreach (var trap in traps)
            {
                map.Grid.ToCoords(trap.Tile, out int x, out int y);
                ring.Clear();

                // The eight tiles touching it. Close enough that the bones are the
                // trap's, not some other thing's; off the tile so the trap is untouched.
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = x + dx, ny = y + dy;
                        if (!map.Grid.InBounds(nx, ny)) continue;

                        var terrain = map.Grid[nx, ny];
                        if (terrain == TerrainType.Water || terrain == TerrainType.Cliff) continue;

                        int tile = map.Grid.ToIndex(nx, ny);
                        if (trapTiles.Contains(tile) || taken.Contains(tile)) continue;

                        ring.Add(tile);
                    }

                if (ring.Count == 0) continue;

                int site = ring[rng.Range(0, ring.Count)];
                taken.Add(site);
                sites.Add(site);
            }

            return sites;
        }
    }
}
