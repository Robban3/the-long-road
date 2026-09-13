using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// Where people live on the road, and on which levels.
    ///
    /// <b>Houses were placed one at a time and never added up to anywhere.</b> The rule
    /// was a die roll per tile — plains, near a corridor, flat enough, and then eight
    /// chances in a thousand — which comes out at about one house a level, standing alone
    /// in a field. One house is not a settlement; it is a house somebody abandoned. So a
    /// level either has a village on it or it has none, the site is chosen once, and
    /// everything that belongs to a village is built around that one point.
    ///
    /// Which levels: the ones in country somebody would live in. A fen, a desert, ash and
    /// a wood that does not want the road get nothing, and that absence says as much
    /// about those countries as the village says about this one.
    ///
    /// This decides *where*; TerrainDecorator builds what stands there. The same split
    /// the camps and the trap signs use, and for the same reason — the plan map and the
    /// run must agree about what is on the ground, and they only do if one of them is not
    /// deciding it.
    /// </summary>
    public static class Settlements
    {
        /// <summary>Keeps the village's dice clear of every other stream drawn from the seed.</summary>
        const int Salt = 0x5E77;

        /// <summary>
        /// The levels in a chapter that have a village, where the country has any.
        ///
        /// Two, and neither of them at the ends: the first level of a chapter is where a
        /// player learns what the country is, and the tenth is the one with the keep on
        /// it. A village in between is a landmark to travel towards and then leave.
        /// </summary>
        static readonly int[] VillageLevels = { 3, 6 };

        /// <summary>Whether anybody lives in this country at all.</summary>
        public static bool Settled(Biome biome)
        {
            switch (biome)
            {
                // Nobody keeps a house in a bog, on ash, on sand, or in a wood that has
                // its own opinion about visitors.
                case Biome.Marsh:
                case Biome.Dead:
                case Biome.Desert:
                case Biome.Enchanted:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>Whether this level has a village on it.</summary>
        public static bool HasVillage(int chapter, int level)
        {
            if (!Settled(Biomes.Of(chapter))) return false;

            foreach (int at in VillageLevels)
                if (at == level) return true;

            return false;
        }

        /// <summary>How far from a corridor a village may stand, in tiles.</summary>
        public const int Reach = 4;

        /// <summary>How much ground a village takes, as a radius in tiles.</summary>
        public const int Yard = 3;

        /// <summary>
        /// The rise a village will not be built on, in normalised height across its yard.
        ///
        /// Houses are square to the world and do not follow a slope; a row of them across
        /// a hillside reads as subsidence, which is the same fault that turns them a
        /// quarter turn at a time rather than to any angle.
        /// </summary>
        public const float Level = 0.055f;

        /// <summary>
        /// The tile a village stands on, or -1 where the level has none.
        ///
        /// Chosen once from the map's own seed, so the plan map and the run put the
        /// village in the same field. Candidates are open ground within reach of a way
        /// through — a village nobody passes is scenery nobody sees — with a yard of dry,
        /// level ground around them.
        /// </summary>
        public static int Site(LevelMap map, int chapter, int level)
        {
            if (map?.Grid == null || !HasVillage(chapter, level)) return -1;

            var near = Travelled(map);
            if (near.Count == 0) return -1;

            // Asked twice, and the second asking is what makes the village a promise.
            //
            // The first pass wants the field a village ought to have: a yard of level
            // plains three tiles out. Three of the four levels owed one got it and 1-6
            // came back with nothing at all — no seven-by-seven of flat plains anywhere
            // near a way through. A level that is told it has a village and then has none
            // is worse than a village on a smaller plot, so the second pass takes a
            // smaller yard on a steeper slope and settles for open ground rather than
            // insisting on a field.
            int site = Best(map, near, Yard, Level, plainsOnly: true);
            if (site >= 0) return site;

            return Best(map, near, Yard - 1, Level * 1.8f, plainsOnly: false);
        }

        /// <summary>The best site under one set of demands, or -1 where there is none.</summary>
        static int Best(LevelMap map, HashSet<int> near, int yard, float level, bool plainsOnly)
        {
            var grid = map.Grid;
            var candidates = new List<int>();

            for (int i = 0; i < grid.TileCount; i++)
            {
                var terrain = grid[i];
                if (plainsOnly ? terrain != TerrainType.Plains : !Open(terrain)) continue;

                grid.ToCoords(i, out int x, out int y);

                // Off the edge, where a village would be half outside the world.
                if (x < yard + 1 || y < yard + 1
                    || x >= grid.Width - yard - 1 || y >= grid.Height - yard - 1) continue;

                if (!Within(grid, near, x, y, Reach)) continue;
                if (!Yardable(grid, x, y, yard, level)) continue;

                candidates.Add(i);
            }

            if (candidates.Count == 0) return -1;

            return candidates[new DeterministicRandom(map.Seed ^ Salt).Range(0, candidates.Count)];
        }

        /// <summary>Ground somebody could clear and build on, whatever is growing on it.</summary>
        static bool Open(TerrainType terrain)
            => terrain == TerrainType.Plains || terrain == TerrainType.Forest;

        /// <summary>Whether a way through passes within <paramref name="reach"/> tiles.</summary>
        static bool Within(TileGrid grid, HashSet<int> tiles, int x, int y, int reach)
        {
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!grid.InBounds(nx, ny)) continue;
                    if (tiles.Contains(grid.ToIndex(nx, ny))) return true;
                }

            return false;
        }

        /// <summary>Whether the ground around a tile is dry, buildable and level.</summary>
        static bool Yardable(TileGrid grid, int x, int y, int yard, float level)
        {
            float low = float.MaxValue, high = float.MinValue;

            for (int dy = -yard; dy <= yard; dy++)
                for (int dx = -yard; dx <= yard; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!grid.InBounds(nx, ny)) return false;

                    var terrain = grid[nx, ny];

                    // Water and its crossings are not a building plot, and neither is a
                    // cliff or a pass: what is wanted is a field.
                    if (terrain == TerrainType.Water || terrain == TerrainType.Ford
                        || terrain == TerrainType.Cliff || terrain == TerrainType.MountainPass)
                        return false;

                    float height = grid.Elevation(nx, ny);
                    if (height < low) low = height;
                    if (height > high) high = height;
                }

            return high - low <= level;
        }

        /// <summary>Every tile any way through the level crosses.</summary>
        static HashSet<int> Travelled(LevelMap map)
        {
            var tiles = new HashSet<int>();
            if (map.Corridors == null) return tiles;

            foreach (var corridor in map.Corridors)
                foreach (int tile in corridor.Tiles)
                    tiles.Add(tile);

            return tiles;
        }
    }
}
