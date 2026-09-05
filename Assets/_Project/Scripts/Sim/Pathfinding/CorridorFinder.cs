using System.Collections.Generic;

namespace TheVeil.Sim
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

        /// <summary>
        /// What the detour avoids ambush terrain with, which is the cautious road's own
        /// weight and not a stronger one.
        ///
        /// <b>Both legs of the detour used to be pathfound with no cost array at all</b>
        /// — pure travel time to a geometric anchor — so it went somewhere *else* rather
        /// than somewhere safer and took whatever danger lay there. Measured over chapter
        /// 1 it ran 74 percent more exposed than the cautious road while also being
        /// slower: dominated on every axis, so there was no state of the game in which it
        /// was the right choice. Fewer stars has to buy something.
        ///
        /// A heavier weight was the obvious guess and the measurement refused it. Sharing
        /// SafetyWeight leaves the detour 9 percent above the cautious road; at 9 and 14
        /// it comes out at 15, worse rather than better, because past a point the search
        /// buys cover by wandering into ground that is slower and no safer. What the
        /// detour lacked was not a stronger preference for safety but any preference at
        /// all.
        /// </summary>
        const float DetourSafetyWeight = SafetyWeight;

        /// <summary>
        /// What a tile already used by another corridor costs the cautious route, and
        /// what a tile beside one costs.
        ///
        /// In tile-crossings, so nine tenths is close to the price of walking an extra
        /// tile: enough to take a parallel line where the ground offers one, and not
        /// enough to send the route round three sides of the map when it does not.
        /// </summary>
        const float TakenSurcharge = 0.9f;
        const float NeighbourSurcharge = 0.45f;

        /// <summary>Adds a cost to a set of tiles and a smaller one to their neighbours.</summary>
        static void Surcharge(TileGrid grid, List<int> tiles, float[] cost,
                              float onTile, float beside)
        {
            foreach (int tile in tiles)
            {
                cost[tile] += onTile;

                grid.ToCoords(tile, out int x, out int y);

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        if (!grid.InBounds(x + dx, y + dy)) continue;

                        cost[grid.ToIndex(x + dx, y + dy)] += beside;
                    }
            }
        }

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
            //
            //    **It avoids the fast route, and that is not free.** Without the
            //    surcharge the cautious route is the fast one whenever the fast one is
            //    already safe, which is not rare: measured across chapter 1 the two came
            //    out at 97 to 100 percent of the same tiles on three levels of ten, and
            //    at 64 to 86 on three more. On those the map offered a straight line, a
            //    second straight line drawn on top of it, and a detour — one real choice
            //    where the game promises three.
            //
            //    It was written, tuned and left uncalled, because turning it on means the
            //    encounter placer spreads the same budget over three times the ground and
            //    on 1-5 that left one survivable way through where the chapter owes two.
            //    That is a balance pass, not a line — so it was made as part of one, with
            //    SurvivableDanger measuring the consequence and the generator re-rolling
            //    on it.
            //
            //    Adjacent tiles carry half the charge. Without that the cautious route
            //    steps one tile aside and runs alongside the fast one, which satisfies
            //    an overlap measure and looks to the player exactly like the same road.
            var safetyCost = new float[grid.TileCount];
            for (int i = 0; i < safetyCost.Length; i++)
            {
                float ambush = TerrainTable.AmbushWeight(grid[i]);
                safetyCost[i] = ambush > NeutralAmbush ? (ambush - NeutralAmbush) * SafetyWeight : 0f;
            }

            // The call the comment above has been describing. Made now, because what held
            // it back was a balance pass and this is inside one.
            Surcharge(grid, result[0].Tiles, safetyCost, TakenSurcharge, NeighbourSurcharge);

            if (pathfinder.TryFindPath(startX, startY, goalX, goalY, buffer, out _, safetyCost))
            {
                float trueCost = MeasureTravelCost(grid, buffer);
                result.Add(Build(CorridorKind.Safe, grid, buffer, trueCost));
            }

            // 3. Odd: routed through the point furthest from everything found so far.
            //
            //    Surcharging the used tiles was the obvious approach and it does not
            //    work on its own: A* still returns a near-optimal path, so the
            //    "alternative" is the fast route nudged a few tiles sideways and all
            //    three corridors end up within a few percent of the same travel time.
            //    Forcing the route through a distant anchor is what produces a genuine
            //    third option — the marsh slog or the mountain detour — with a real cost
            //    in time to weigh against its lower danger.
            //
            //    The anchor was the whole of it for a while, and that is what left the
            //    detour dominated: distinct, slow, and no safer than anything else,
            //    because both its legs were solved on travel time alone. It carries its
            //    own safety field now, weighted far above the cautious road's, so the
            //    lower danger it is supposed to be weighed against actually exists.
            int anchor = FindDetourAnchor(grid, result, startX, startY, goalX, goalY);
            if (anchor >= 0)
            {
                grid.ToCoords(anchor, out int ax, out int ay);
                var oddTiles = new List<int>();

                // Its own cost field, and both legs get it.
                //
                // The anchor makes the detour *distinct*; this is what makes it *safe*,
                // and they are different jobs. Charged for both roads already found, not
                // just the fast one: the cautious road is now genuinely cautious, so two
                // searches both chasing low ambush would otherwise start converging on
                // the same ground — the anchor keeps their middles apart and this keeps
                // their approaches apart.
                var detourCost = new float[grid.TileCount];
                for (int i = 0; i < detourCost.Length; i++)
                {
                    float ambush = TerrainTable.AmbushWeight(grid[i]);
                    detourCost[i] = ambush > NeutralAmbush
                        ? (ambush - NeutralAmbush) * DetourSafetyWeight : 0f;
                }

                foreach (var found in result)
                    Surcharge(grid, found.Tiles, detourCost, TakenSurcharge, NeighbourSurcharge);

                if (pathfinder.TryFindPath(startX, startY, ax, ay, buffer, out _, detourCost))
                {
                    oddTiles.AddRange(buffer);
                    if (pathfinder.TryFindPath(ax, ay, goalX, goalY, buffer, out _, detourCost))
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
        /// Finds the tile the detour is routed through: far from the roads already
        /// found, and on the safest ground among the tiles that are.
        ///
        /// Distance is walked rather than measured straight — a tile across a river is
        /// far away even when it looks close.
        ///
        /// <b>Distance alone is what left the detour dominated, and it is worse than
        /// merely neutral about danger.</b> "Furthest from the other two roads" tends
        /// toward the worst ground on the map, because the other two roads already took
        /// the good ground — the cautious one explicitly. So the anchor was being planted
        /// in exactly the country everything else had avoided, and the detour inherited
        /// it: measured over chapter 1 it ran 22 percent more exposed than the cautious
        /// road while also being slower, which is a road nobody has a reason to take.
        /// IsMeaningfulChoice says as much in its own second condition — a longer route
        /// that is no safer is simply a worse route.
        ///
        /// Giving the detour's own pathfinding a heavy safety cost does not fix it and
        /// the measurement is unambiguous: weights of 2.4, 5, 9 and 14 moved its exposure
        /// 0.878, 0.863, 0.852, 0.849 — three percent across a sixfold change — because a
        /// route forced through a fixed far tile cannot avoid the country that tile sits
        /// in. The anchor is the binding constraint, so the anchor is what has to choose.
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

            int furthest = 0;
            for (int x = lowX; x <= highX && x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    int reach = distance[grid.ToIndex(x, y)];
                    if (reach > furthest) furthest = reach;
                }

            // A trivial detour is worse than none: it produces a third corridor that
            // is really just the first one again.
            if (furthest < 4) return -1;

            // Among everything far enough to still be a detour, the safest ground.
            //
            // Two passes rather than one comparison, because the two quantities are not
            // commensurable and ranking them against each other needs a scale nobody can
            // defend. A threshold can be defended: past AnchorReachShare of the furthest
            // tile the route is a detour by any reading, and which of those tiles it
            // turns at is then free to be decided on danger alone.
            float floor = furthest * AnchorReachShare;
            int best = -1;
            float safest = float.MaxValue;

            for (int x = lowX; x <= highX && x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    int index = grid.ToIndex(x, y);
                    if (distance[index] < floor) continue;

                    float exposure = LocalAmbush(grid, x, y);
                    if (exposure >= safest) continue;

                    safest = exposure;
                    best = index;
                }

            return best;
        }

        /// <summary>
        /// How far out a tile has to be, as a share of the furthest, to count as a
        /// detour at all.
        ///
        /// Seven tenths. High enough that the third road still goes somewhere the other
        /// two do not — the overlap measurements are what watch that — and low enough
        /// that there is a real field of candidates to choose the safest from. At one,
        /// there is exactly one candidate and this whole pass does nothing.
        /// </summary>
        const float AnchorReachShare = 0.7f;

        /// <summary>
        /// Mean ambush weight of the ground around a tile, out to AnchorRadius.
        ///
        /// A neighbourhood rather than the tile itself, because the detour has to *pass
        /// through* here rather than stand on it. One safe tile in the middle of a wood
        /// is a route through a wood.
        /// </summary>
        const int AnchorRadius = 3;

        static float LocalAmbush(TileGrid grid, int x, int y)
        {
            float total = 0f;
            int counted = 0;

            for (int dy = -AnchorRadius; dy <= AnchorRadius; dy++)
                for (int dx = -AnchorRadius; dx <= AnchorRadius; dx++)
                {
                    if (!grid.IsPassable(x + dx, y + dy)) continue;

                    total += TerrainTable.AmbushWeight(grid[grid.ToIndex(x + dx, y + dy)]);
                    counted++;
                }

            // Impassable all round is not somewhere to route through.
            return counted == 0 ? float.MaxValue : total / counted;
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
        /// **One pair, and that is a gate built to pass what the generator produces.**
        /// The stated reason was that good levels were rejected when the fast and
        /// cautious routes shared a river crossing. They were not sharing a crossing:
        /// measured across chapter 1, three levels of ten have those two at 97 to 100
        /// percent of the same tiles, because the cautious search has no reason to leave
        /// a fast route that is already safe. On those levels the player is offered a
        /// road, the same road again, and a detour.
        ///
        /// Requiring every pair is one line here and the surcharge described in Find,
        /// and it works — every pair on every level then comes in under a quarter. What
        /// it costs is measured too: see the note in Find. The two belong together and
        /// they want a balance pass with them.
        /// </summary>
        public static bool IsMeaningfulChoice(IReadOnlyList<Corridor> corridors,
                                              float maxOverlap = 0.62f,
                                              float minTimeSpread = 0.12f,
                                              float minDangerSpread = 0.08f)
        {
            if (corridors == null || corridors.Count < 3) return false;

            // The fast road and the safe road, specifically — not "some pair".
            //
            // **Any-pair was passed by the odd corridor on its own.** The odd one is
            // forced away from the other two and overlaps them by 1 to 34 percent, so the
            // test was satisfied before the fast and safe roads were ever compared — and
            // measured over chapter 1 they came out *identical* on levels 4, 5 and 10 and
            // 77 to 86 percent the same on 3 and 6. The level shipped as a meaningful
            // choice while the choice the game is about did not exist on half of it.
            //
            // The spreads below have the same hole and it is the same corridor filling
            // it: slowest-minus-fastest and rashest-minus-safest are both satisfied by
            // the odd route being slow and exposed, whatever the other two are doing. So
            // they are measured between fast and safe as well.
            var fast = Of(corridors, CorridorKind.Fast);
            var safe = Of(corridors, CorridorKind.Safe);

            if (fast == null || safe == null) return false;
            if (Overlap(fast, safe) > maxOverlap) return false;

            if (fast.TravelCost <= 0f || safe.AmbushExposure <= 0f) return false;

            // The safe way must cost time and the fast way must cost blood. Either one
            // alone is a road that is simply better, and a choice with a right answer is
            // not one.
            if ((safe.TravelCost - fast.TravelCost) / fast.TravelCost < minTimeSpread) return false;
            if ((fast.AmbushExposure - safe.AmbushExposure) / safe.AmbushExposure < minDangerSpread)
                return false;

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

        static Corridor Of(IReadOnlyList<Corridor> corridors, CorridorKind kind)
        {
            foreach (var corridor in corridors) if (corridor.Kind == kind) return corridor;
            return null;
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
