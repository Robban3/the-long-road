using System;
using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>Birds circling a piece of ground, and whether anything is under them.</summary>
    public struct CrowFlock
    {
        public int Tile;

        /// <summary>
        /// False for a flock standing over nothing.
        ///
        /// Nothing in the world distinguishes one from the other, and nothing should:
        /// a signal you can tell is false is not a false positive, it is a second
        /// signal that says "ignore me".
        /// </summary>
        public bool Truthful;
    }

    /// <summary>
    /// Circling crows — the strongest of the soft signals (docs/GDD.md §3.5).
    ///
    /// Route drawing made this more important than it was. It is read before the line
    /// is drawn rather than during the run, so it is one of the few things that shapes
    /// the decision itself rather than reacting to it.
    ///
    /// What a flock says is deliberately vague: a group somewhere near, never a group
    /// on that tile. Same rule the ruin follows, for the same reason — a signal must be
    /// information and not the answer sheet.
    /// </summary>
    public static class CrowSignal
    {
        /// <summary>
        /// Tiles a flock stands for.
        ///
        /// §3.5 said twenty, and twenty says nothing. With sixteen groups on a
        /// sixty-four tile map, 96 % of the ground already has a group within twenty
        /// tiles — so "there is one within twenty" was true almost everywhere by
        /// accident, and a player who ignored the crows would have been right just as
        /// often. Measured over nine levels:
        ///
        ///     radius   3     4     5     6     8    10    12    15    20
        ///     covered 12%   20%   30%   39%   56%   71%   79%   89%   96%
        ///
        /// Six is where the signal starts being one. Random ground has a group within
        /// six tiles 39 % of the time and a flock says 80 % — a real update, which is
        /// the whole test of whether something is worth reading. It is also about the
        /// size of a group's own territory, so "crows over that wood" means "you would
        /// be inside somebody's reach around there" rather than a coincidence.
        /// </summary>
        public const int HintTiles = 6;

        /// <summary>
        /// Never nearer than this to *any* group, so a flock cannot double as a marker.
        ///
        /// Vaguer than the ruin's three tiles from a trap field is precise, which is the
        /// right order: a trap you walk onto punishes harder than a group you can see
        /// coming.
        /// </summary>
        public const int MinTiles = 3;

        /// <summary>
        /// How many of a level's groups are announced, drawn once per level between these.
        ///
        /// <b>Once per level and the same for every road, and both halves of that are
        /// load-bearing.</b> It was a coin for each group, at a half. Not every group, and
        /// that part is right: if every group had a flock, counting flocks would count
        /// groups and the level's order of battle would be free. But a coin per group is
        /// noise between the roads as well as within the level - the quick road carries
        /// seven groups and the long way round two, and a run of coins can leave the quick
        /// road with two flocks and the slog with two. Measured over the chapters that
        /// exist, the road with the most crows was the most dangerous road on two levels
        /// in three.
        ///
        /// So each level draws how much of its country the crows will give away, and every
        /// road gives that much. Read against one another the roads come out in the order
        /// their danger is in; read on its own, a count of flocks is a count of nothing,
        /// because the share it was taken at is different on every level.
        /// </summary>
        public const float ShareLow = 0.3f;
        public const float ShareHigh = 0.8f;

        /// <summary>
        /// Share of flocks standing over nothing. False positives are the point: a
        /// signal that is always right is not a signal, it is a map.
        /// </summary>
        public const float FalseShare = 0.20f;

        const int PlacementAttempts = 12;
        const int FalseAttempts = 24;

        /// <summary>Where the crows circle. Deterministic from the level seed.</summary>
        public static List<CrowFlock> Place(LevelMap map)
        {
            var flocks = new List<CrowFlock>();
            if (map?.Encounters == null) return flocks;

            var grid = map.Grid;
            var groups = map.Encounters.Enemies;
            var rng = new DeterministicRandom(map.Seed ^ 0x0C0F);
            var taken = new HashSet<int>();

            float share = rng.Range(ShareLow, ShareHigh);

            foreach (var spawn in Announced(map, groups, share, rng))
            {
                grid.ToCoords(spawn.Tile, out int gx, out int gy);

                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    float angle = rng.Range(0f, (float)(2.0 * Math.PI));
                    // Cast, and not for tidiness: both constants are int, so without it
                    // the int overload is chosen and the reach snaps to 3, 4 or 5 —
                    // a different distribution from the one measured.
                    float reach = rng.Range((float)MinTiles, (float)HintTiles);

                    int nx = (int)Math.Round(gx + Math.Cos(angle) * reach);
                    int ny = (int)Math.Round(gy + Math.Sin(angle) * reach);

                    if (!grid.InBounds(nx, ny)) continue;

                    int tile = grid.ToIndex(nx, ny);
                    if (!Free(grid, groups, taken, tile)) continue;

                    // Check the claim after rounding, not before. `reach` is drawn below
                    // the radius but snapping to a whole tile can push it past —
                    // measured at 6.1 to 6.3 tiles against a claim of 6, which is a
                    // flock quietly lying.
                    if (Distance(nx, ny, gx, gy) > HintTiles) continue;

                    taken.Add(tile);
                    flocks.Add(new CrowFlock { Tile = tile, Truthful = true });
                    break;
                }
            }

            // Then the lies, placed where nothing is within the radius they claim — a
            // false flock has to be genuinely false rather than accidentally right.
            //
            // <b>Shared out between the roads, and not left to find their own ground.</b>
            // A lie can only stand where no group is within six tiles, and on a level whose
            // danger is dealt out by road that ground is almost all beside the quiet roads.
            // So the lies, left to land anywhere, landed on the safe roads, and the noise
            // meant to keep the crows honest made the safe road look like the dangerous
            // one. Each road is offered its turn in order; a road with no room for a lie -
            // the busy one, usually - passes its turn to open country beside none of them.
            int wanted = (int)Math.Round(flocks.Count * FalseShare / (1f - FalseShare));
            var roads = map.Corridors;

            for (int i = 0; i < wanted; i++)
            {
                IReadOnlyList<int> beside = roads != null && roads.Count > 0
                    ? roads[i % roads.Count].Tiles
                    : null;

                if (!Lie(grid, groups, taken, rng, flocks, beside))
                    Lie(grid, groups, taken, rng, flocks, null);
            }

            return flocks;
        }

        /// <summary>
        /// The groups that get a flock: the same share of each road's groups, and which of
        /// them is chosen at random.
        ///
        /// A group belongs to the road it is nearest. Groups by no road at all are counted
        /// as a road of their own, so the open country is announced at the same share as
        /// everything else.
        /// </summary>
        static List<EnemySpawn> Announced(LevelMap map, List<EnemySpawn> groups, float share,
                                          DeterministicRandom rng)
        {
            var roads = map.Corridors;
            int count = roads == null ? 0 : roads.Count;

            var byRoad = new List<List<EnemySpawn>>();
            for (int i = 0; i <= count; i++) byRoad.Add(new List<EnemySpawn>());

            foreach (var spawn in groups)
                byRoad[NearestRoad(map, spawn.Tile)].Add(spawn);

            var chosen = new List<EnemySpawn>();

            foreach (var road in byRoad)
            {
                rng.Shuffle(road);

                int announce = (int)Math.Round(road.Count * share);
                for (int i = 0; i < announce && i < road.Count; i++) chosen.Add(road[i]);
            }

            return chosen;
        }

        /// <summary>The road a tile is nearest, or the last index for "none of them".</summary>
        static int NearestRoad(LevelMap map, int tile)
        {
            var roads = map.Corridors;
            if (roads == null || roads.Count == 0) return 0;

            map.Grid.ToCoords(tile, out int x, out int y);

            int best = roads.Count;
            float nearest = HintTiles * 2f;

            for (int r = 0; r < roads.Count; r++)
            {
                foreach (int step in roads[r].Tiles)
                {
                    map.Grid.ToCoords(step, out int sx, out int sy);
                    float d = Distance(sx, sy, x, y);
                    if (d >= nearest) continue;

                    nearest = d;
                    best = r;
                }
            }

            return best;
        }

        /// <summary>
        /// One false flock, beside the given road where it will go, or anywhere at all
        /// when the road is null. False means nothing within the radius the flock claims.
        /// </summary>
        static bool Lie(TileGrid grid, List<EnemySpawn> groups, HashSet<int> taken,
                        DeterministicRandom rng, List<CrowFlock> flocks, IReadOnlyList<int> beside)
        {
            for (int attempt = 0; attempt < FalseAttempts; attempt++)
            {
                int tile;

                if (beside != null && beside.Count > 0)
                {
                    grid.ToCoords(beside[rng.Range(0, beside.Count)], out int rx, out int ry);
                    int nx = rx + rng.Range(-HintTiles, HintTiles + 1);
                    int ny = ry + rng.Range(-HintTiles, HintTiles + 1);
                    if (!grid.InBounds(nx, ny)) continue;
                    tile = grid.ToIndex(nx, ny);
                }
                else
                {
                    tile = rng.Range(0, grid.TileCount);
                }

                if (!Free(grid, groups, taken, tile)) continue;

                grid.ToCoords(tile, out int x, out int y);
                if (NearestGroup(grid, groups, x, y) <= HintTiles) continue;

                taken.Add(tile);
                flocks.Add(new CrowFlock { Tile = tile, Truthful = false });
                return true;
            }

            return false;
        }

        static bool Free(TileGrid grid, List<EnemySpawn> groups, HashSet<int> taken, int tile)
        {
            if (taken.Contains(tile)) return false;

            grid.ToCoords(tile, out int x, out int y);
            if (!grid.IsPassable(x, y) || grid[tile] == TerrainType.Ford) return false;

            // Distance from every group, not only the one this flock belongs to.
            // Checking its own let a flock land on a different group's tile — measured
            // at zero tiles away, which is a marker and not a hint.
            return NearestGroup(grid, groups, x, y) >= MinTiles;
        }

        static float NearestGroup(TileGrid grid, List<EnemySpawn> groups, int x, int y)
        {
            float nearest = float.PositiveInfinity;

            foreach (var spawn in groups)
            {
                grid.ToCoords(spawn.Tile, out int gx, out int gy);
                float distance = Distance(gx, gy, x, y);
                if (distance < nearest) nearest = distance;
            }

            return nearest;
        }

        static float Distance(int ax, int ay, int bx, int by)
        {
            float dx = ax - bx, dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
