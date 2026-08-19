using System.Collections.Generic;

namespace Arna.Sim
{
    public enum CorridorKind
    {
        /// <summary>Pure shortest travel time. Usually the most dangerous.</summary>
        Fast = 0,
        /// <summary>Avoids ambush-prone terrain even at a cost in time.</summary>
        Safe = 1,
        /// <summary>Forced away from the other two — the marsh slog or the mountain pass.</summary>
        Odd = 2
    }

    public sealed class Corridor
    {
        public CorridorKind Kind;
        public readonly List<int> Tiles = new List<int>();

        /// <summary>Travel cost in tile-crossings; divide by caravan tiles/second for time.</summary>
        public float TravelCost;

        /// <summary>Mean ambush weight along the route — the generator's proxy for danger.</summary>
        public float AmbushExposure;
    }

    /// <summary>
    /// Finds the three routes a level must offer and measures whether they are
    /// meaningfully different (docs/content-pipeline.md §3, step 4).
    ///
    /// This is the quality gate for procedural generation. A map where the fast,
    /// safe and odd routes are near-identical is a map where the route drawing —
    /// the central decision of the whole game — does not matter, and the level
    /// collapses into an army-picking screen. Such seeds get rejected.
    /// </summary>
    public static class CorridorFinder
    {
        /// <summary>How hard the cautious route avoids ambush terrain.</summary>
        const float SafetyWeight = 2.4f;

        /// <summary>Ambush weight treated as "neutral"; terrain below this is free.</summary>
        const float NeutralAmbush = 0.9f;

        /// <summary>
        /// How far from the goal-ward middle of the map the odd route's anchor may
        /// be sought. Anchors near the start or the goal barely change the route.
        /// </summary>
        const float AnchorBandLow = 0.28f;
        const float AnchorBandHigh = 0.72f;

        /// <summary>
        /// Finds up to three corridors. Returns fewer only when the goal is
        /// unreachable, which the caller must treat as a rejected seed.
        /// </summary>
        public static List<Corridor> Find(TileGrid grid, int startX, int startY, int goalX, int goalY)
        {
            var result = new List<Corridor>(3);
            var pathfinder = new GridPathfinder(grid);
            var buffer = new List<int>();

            // 1. Fastest, no surcharges.
            if (!pathfinder.TryFindPath(startX, startY, goalX, goalY, buffer, out float fastCost))
                return result;
            result.Add(Build(CorridorKind.Fast, grid, buffer, fastCost));

            // 2. Cautious: ambush-prone terrain costs extra, so the search prefers
            //    open ground and roads it can see along.
            var safetyCost = new float[grid.TileCount];
            for (int i = 0; i < safetyCost.Length; i++)
            {
                float ambush = TerrainTable.AmbushWeight(grid[i]);
                safetyCost[i] = ambush > NeutralAmbush ? (ambush - NeutralAmbush) * SafetyWeight : 0f;
            }

            if (pathfinder.TryFindPath(startX, startY, goalX, goalY, buffer, out _, safetyCost))
            {
                float trueCost = MeasureTravelCost(grid, buffer);
                result.Add(Build(CorridorKind.Safe, grid, buffer, trueCost));
            }

            // 3. Odd: routed through the point furthest from everything found so far.
            //
            //    Surcharging the used tiles was the obvious approach and it does not
            //    work: A* still returns a near-optimal path, so the "alternative" is
            //    the fast route nudged a few tiles sideways and all three corridors
            //    end up within a few percent of the same travel time. Forcing the
            //    route through a distant anchor is what produces a genuine third
            //    option — the marsh slog or the mountain detour — with a real cost in
            //    time to weigh against its lower danger.
            int anchor = FindDetourAnchor(grid, result, startX, startY, goalX, goalY);
            if (anchor >= 0)
            {
                grid.ToCoords(anchor, out int ax, out int ay);
                var oddTiles = new List<int>();

                if (pathfinder.TryFindPath(startX, startY, ax, ay, buffer, out _))
                {
                    oddTiles.AddRange(buffer);
                    if (pathfinder.TryFindPath(ax, ay, goalX, goalY, buffer, out _))
                    {
                        // Skip the anchor itself; it is already the last tile added.
                        for (int i = 1; i < buffer.Count; i++) oddTiles.Add(buffer[i]);
                        result.Add(Build(CorridorKind.Odd, grid, oddTiles, MeasureTravelCost(grid, oddTiles)));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Finds the passable tile in the middle band of the map that lies furthest
        /// from any corridor found so far, measured as walking distance rather than
        /// straight-line distance — a tile across a river is far away even when it
        /// looks close.
        /// </summary>
        static int FindDetourAnchor(TileGrid grid, List<Corridor> found,
                                    int startX, int startY, int goalX, int goalY)
        {
            var distance = new int[grid.TileCount];
            for (int i = 0; i < distance.Length; i++) distance[i] = -1;

            var queue = new Queue<int>();
            foreach (var corridor in found)
                foreach (int tile in corridor.Tiles)
                    if (distance[tile] != 0) { distance[tile] = 0; queue.Enqueue(tile); }

            if (queue.Count == 0) return -1;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                grid.ToCoords(current, out int cx, out int cy);

                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!grid.IsPassable(nx, ny)) continue;

                    int neighbour = grid.ToIndex(nx, ny);
                    if (distance[neighbour] >= 0) continue;

                    distance[neighbour] = distance[current] + 1;
                    queue.Enqueue(neighbour);
                }
            }

            int lowX = (int)(grid.Width * AnchorBandLow);
            int highX = (int)(grid.Width * AnchorBandHigh);

            int best = -1, bestDistance = 0;
            for (int x = lowX; x <= highX && x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    int index = grid.ToIndex(x, y);
                    if (distance[index] <= bestDistance) continue;

                    bestDistance = distance[index];
                    best = index;
                }
            }

            // A trivial detour is worse than none: it produces a third corridor that
            // is really just the first one again.
            return bestDistance >= 4 ? best : -1;
        }

        /// <summary>
        /// True when the level presents a real decision.
        ///
        /// Three conditions, and all three are needed:
        ///
        /// 1. Some pair of corridors is genuinely different ground, not the same
        ///    route nudged sideways.
        /// 2. Danger differs across the corridors. Time alone is not a decision —
        ///    a longer route that is no safer is simply a worse route, and nobody
        ///    would ever take it.
        /// 3. That safety costs time. Safety for free is not a decision either.
        ///
        /// Requiring *every* pair to differ was the first attempt and it was wrong:
        /// it rejected good levels where the fast and cautious routes share a river
        /// crossing while a third corridor takes a different one entirely. One real
        /// alternative is enough to make the player choose.
        /// </summary>
        public static bool IsMeaningfulChoice(IReadOnlyList<Corridor> corridors,
                                              float maxOverlap = 0.62f,
                                              float minTimeSpread = 0.12f,
                                              float minDangerSpread = 0.08f)
        {
            if (corridors == null || corridors.Count < 3) return false;

            bool anyDistinctPair = false;
            for (int a = 0; a < corridors.Count && !anyDistinctPair; a++)
                for (int b = a + 1; b < corridors.Count; b++)
                    if (Overlap(corridors[a], corridors[b]) <= maxOverlap) { anyDistinctPair = true; break; }

            if (!anyDistinctPair) return false;

            float fastest = float.MaxValue, slowest = 0f;
            float safest = float.MaxValue, rashest = 0f;
            foreach (var c in corridors)
            {
                if (c.TravelCost < fastest) fastest = c.TravelCost;
                if (c.TravelCost > slowest) slowest = c.TravelCost;
                if (c.AmbushExposure < safest) safest = c.AmbushExposure;
                if (c.AmbushExposure > rashest) rashest = c.AmbushExposure;
            }

            if (fastest <= 0f || safest <= 0f) return false;

            float timeSpread = (slowest - fastest) / fastest;
            float dangerSpread = (rashest - safest) / safest;

            return timeSpread >= minTimeSpread && dangerSpread >= minDangerSpread;
        }

        /// <summary>Jaccard overlap of two routes' tiles, 0 (disjoint) to 1 (identical).</summary>
        public static float Overlap(Corridor a, Corridor b)
        {
            if (a.Tiles.Count == 0 || b.Tiles.Count == 0) return 0f;

            var setA = new HashSet<int>(a.Tiles);
            int shared = 0;
            foreach (int tile in b.Tiles)
                if (setA.Contains(tile)) shared++;

            int union = a.Tiles.Count + b.Tiles.Count - shared;
            return union <= 0 ? 0f : (float)shared / union;
        }

        static Corridor Build(CorridorKind kind, TileGrid grid, List<int> tiles, float travelCost)
        {
            var corridor = new Corridor { Kind = kind, TravelCost = travelCost };
            corridor.Tiles.AddRange(tiles);

            float ambush = 0f;
            foreach (int tile in tiles) ambush += TerrainTable.AmbushWeight(grid[tile]);
            corridor.AmbushExposure = tiles.Count > 0 ? ambush / tiles.Count : 0f;

            return corridor;
        }

        /// <summary>
        /// Real travel cost of a route, ignoring whatever surcharges were used to
        /// find it. The player experiences terrain, not the generator's search bias.
        /// </summary>
        static float MeasureTravelCost(TileGrid grid, List<int> tiles)
        {
            float cost = 0f;
            for (int i = 1; i < tiles.Count; i++)
            {
                grid.ToCoords(tiles[i - 1], out int px, out int py);
                grid.ToCoords(tiles[i], out int cx, out int cy);
                float step = TerrainTable.TravelCost(grid[tiles[i]]);
                if (px != cx && py != cy) step *= 1.41421356f;
                cost += step;
            }
            return cost;
        }
    }
}
