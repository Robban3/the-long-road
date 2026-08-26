using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>
    /// The ground that shows a caravan came to grief here: bones, a wrecked cart, a
    /// dropped chest, a cold fire.
    ///
    /// This is the soft signal the design asks for (docs/GDD.md §2, §5): the player is
    /// meant to learn to read the country rather than be told what is in it. So a sign
    /// is placed near a trap field, **never on one** — one per neighbourhood, offset by
    /// a few tiles. Marking the trap itself would hand over the position of the thing
    /// the whole detection system exists to keep hidden, and a risk you can see exactly
    /// is no longer a risk.
    ///
    /// Near enough to be worth noticing, far enough that noticing it tells you to be
    /// careful rather than where to step.
    ///
    /// It lives in the simulation rather than in the view because both views need it and
    /// only one of them had it. The planning map marked its trap fields and **the play
    /// view marked nothing at all** — `LevelRunner` never passed the sites to the
    /// decorator — so the tell existed on the map the player reads before the level and
    /// was absent from the country they then drove through, which is the half where it
    /// was supposed to do its work.
    /// </summary>
    public static class TrapSigns
    {
        /// <summary>Trap fields are grouped into neighbourhoods this many tiles across.</summary>
        public const int ClusterTiles = 6;

        /// <summary>How far from its field a sign may stand, in tiles.</summary>
        public const int Offset = 3;

        public static List<int> Sites(LevelMap map)
        {
            var traps = map?.Encounters?.Traps;
            if (traps == null || traps.Count == 0) return null;

            var tiles = new List<int>();
            foreach (var trap in traps) tiles.Add(trap.Tile);

            return Near(map, tiles, map.Seed ^ 0x2117);
        }

        /// <summary>
        /// Ground beside the given tiles: one site per neighbourhood, offset by a few
        /// tiles, and never on one of them.
        ///
        /// The rule both signals share. "Never on one" is the whole of the tell, and it
        /// was once said and not done: the offset is drawn from [-3, 3] in both axes,
        /// which includes (0, 0), and nothing checked the marked tiles. A sign marked a
        /// trap exactly, on one of nine sites on 1-5.
        /// </summary>
        static List<int> Near(LevelMap map, List<int> marked, int seed)
        {
            var avoid = new HashSet<int>(marked);
            var rng = new DeterministicRandom(seed);
            var neighbourhoods = new HashSet<int>();
            var sites = new List<int>();

            foreach (int tile in marked)
            {
                map.Grid.ToCoords(tile, out int x, out int y);

                // One per neighbourhood. A field of six traps is one thing that happened,
                // not six; a band of raiders sleeps in one camp.
                int cell = (y / ClusterTiles) * map.Grid.Width + x / ClusterTiles;
                if (!neighbourhoods.Add(cell)) continue;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int nx = x + rng.Range(-Offset, Offset + 1);
                    int ny = y + rng.Range(-Offset, Offset + 1);
                    if (!map.Grid.InBounds(nx, ny)) continue;

                    var terrain = map.Grid[nx, ny];
                    if (terrain == TerrainType.Water || terrain == TerrainType.Cliff) continue;

                    int site = map.Grid.ToIndex(nx, ny);
                    if (avoid.Contains(site)) continue;

                    sites.Add(site);
                    break;
                }
            }

            return sites;
        }

    }
}
