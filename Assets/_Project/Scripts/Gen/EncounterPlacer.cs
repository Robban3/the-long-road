using System;
using System.Collections.Generic;
using Arna.Sim;

namespace Arna.Gen
{
    /// <summary>
    /// Distributes enemies, traps and silver across the ground a route can be drawn
    /// through (docs/content-pipeline.md §3, steps 5–6b).
    ///
    /// The old rule spent the budget along the three corridors, in inverse proportion
    /// to their travel time. That was right while those three were the only routes on
    /// offer. The player draws the line now, and a line drawn between the corridors
    /// would have met nothing at all — so threat lives on the whole band instead, and
    /// the rule survives restated per tile: fast ground carries the most, the fen the
    /// least. The quick way is still the dangerous way.
    ///
    /// What placement alone cannot promise is that the player meets anything, and that
    /// promise is the difference between a level and a walk. Three things buy it:
    ///
    /// 1. <b>The fords are guarded.</b> The river runs across the caravan's travel and
    ///    can only be crossed at its fords, so a group on each is a fight no drawn line
    ///    avoids.
    /// 2. <b>Each group watches a stretch.</b> Twelve groups cannot seal fifty tiles of
    ///    width standing on twelve tiles; watching a territory each, they can.
    /// 3. <b>The placer checks its own work.</b> It samples routes a player might draw
    ///    and moves — never adds — a group onto any route that met too little. Adding
    ///    was tried and broke the budget ceiling that the difficulty curve rests on:
    ///    chapter 1 came out between 13 and 71 percent over budget, and §6 of the
    ///    status notes records what happens when that budget lands on less ground.
    ///
    /// Measured over chapter 1 against forty routes the placer had never seen: no route
    /// met fewer than three groups, the average was between five and six, and every
    /// level stayed inside its budget.
    /// </summary>
    public static class EncounterPlacer
    {
        /// <summary>
        /// How far past the fastest crossing a detour may run before it stops being a
        /// route anyone would draw. Wide enough to keep the whole map in play, tight
        /// enough not to spend the budget in the corners.
        /// </summary>
        public const float BandSlack = 1.6f;

        /// <summary>Travel cost kept clear at both ends, so nothing waits in the first strides.</summary>
        public const float SafeEndCost = 8f;

        /// <summary>
        /// The same rule in tiles, across the ground rather than along a path.
        ///
        /// Both are needed. Cost alone lets a group stand four tiles from the start on a
        /// road, because eight of cost is two and a half tiles at ×1.25; distance alone
        /// would let one sit just behind a ridge that takes half a minute to walk round.
        /// </summary>
        public const int SafeEndTiles = 5;

        public const float GroupSpacingTiles = 5f;

        /// <summary>
        /// Tiles between traps. Two rather than three, because a throat has to be laid
        /// *across* — one trap in a five-tile gap is a trap you walk round.
        /// </summary>
        public const float TrapSpacingTiles = 2f;

        /// <summary>
        /// How much detour, as a share of the fastest crossing, still counts as a way
        /// through when measuring how wide the country is at a given depth.
        ///
        /// Eight percent. A tile costing more than that to route through is not another
        /// way past a chokepoint, it is the long way round the level.
        /// </summary>
        public const float ThroatSlack = 0.08f;

        /// <summary>
        /// Slices the crossing is cut into when looking for its narrow points.
        ///
        /// Forty-eight over a route of sixty to a hundred tiles is a slice every tile or
        /// two — fine enough to find a ford, coarse enough that one tile of noise does
        /// not read as a chokepoint.
        /// </summary>
        public const int ThroatSlices = 48;

        /// <summary>
        /// Share of the budget spent on traps before the recipe's density scales it.
        ///
        /// 0.18, down from 0.25, and the reason is a familiar one: **the share was tuned
        /// against a placement that could not spend it.** The old scatter competed for
        /// tiles with everything else in one occupancy set and at three tiles' spacing,
        /// so it routinely ran out of legal ground before it ran out of allowance. Laying
        /// them at the throats, on their own occupancy, at two tiles, spends the lot —
        /// and the same number therefore buys noticeably more trap and less enemy than it
        /// used to. Chapter 2 started shipping levels that could not put five groups on
        /// every drawn route.
        ///
        /// A constant tuned as a product of two things breaks silently when either moves.
        /// This is the third time that has happened here.
        /// </summary>
        public const float TrapBudgetShare = 0.18f;

        /// <summary>
        /// The share of the trap allowance laid at the crossing's narrow points. The
        /// rest is strewn over the band by terrain, as all of it used to be.
        ///
        /// Two thirds, and both parts earn their place.
        ///
        /// **All-scattered was what there was**, and it does not work: a trap has a
        /// three-metre trigger and no territory, so on a band of three thousand tiles it
        /// is scenery. Level 1-8 laid fourteen and a run down its fast corridor revealed
        /// two and fired none.
        ///
        /// **All-at-the-throats works and reads as placed.** Every trap on ground every
        /// route crosses is a level that has been *arranged*, and a player learns within
        /// three levels that the ford is always mined — which turns a hazard into a
        /// checklist. The scattered third is what keeps that from being a rule: some
        /// traps are simply out there, in the marsh and the woods, and the ones you
        /// stumble on are the ones that make you keep a scout.
        /// </summary>
        public const float ThroatShare = 0.65f;

        /// <summary>
        /// Reach of a group with no territory of its own, in tiles. The widest detect
        /// radius in <see cref="EnemyTable"/> is 22 m — five and a half tiles — and four
        /// is the range at which a group will certainly close.
        /// </summary>
        public const float EngageRadiusTiles = 4f;

        public const float TerritoryMinTiles = 6f;
        public const float TerritoryMaxTiles = 13f;

        /// <summary>Groups the placer works to put on every sampled route.</summary>
        public const int MinEncounters = 5;

        /// <summary>
        /// What the repair loop aims at, which is one more than the promise.
        ///
        /// The loop can only measure the routes it sampled, and a player draws whatever
        /// they like. Repaired to exactly the promise, the sampled routes all met five
        /// and the ones nobody sampled met four — measured over sixty fresh routes on
        /// six levels, every one came in a group short. Aimed one above, the same
        /// measurement returns five and six. The margin is what the sampling costs; it
        /// is not slack, and <see cref="EncounterLayout.EncountersValidated"/> still
        /// records the promise rather than the target.
        /// </summary>
        public const int RepairTarget = MinEncounters + 1;

        /// <summary>
        /// Routes drawn to check the placement against.
        ///
        /// Named apart from the method that uses it because C# will not have a constant
        /// and a method sharing a name in one type — which is how this went unbuilt for
        /// a while: nothing here runs without an editor, so the clash only surfaced when
        /// one was opened.
        /// </summary>
        // Sixty-four, up from thirty-two, and the reason is the corridors moving apart.
        //
        // The loop can only repair the routes it sampled, and a player draws whatever
        // they like; the margin in RepairTarget is what covers the difference. That
        // margin was measured when the fast and cautious corridors ran within a few
        // tiles of each other — on some levels along the identical line — so the space
        // of routes a player might draw was narrow and thirty-two samples covered it.
        // Now that the cautious route is pushed off the fast one, the space is far
        // wider, and 1-4 came out reporting the promise kept while a freshly drawn route
        // met three groups of the five owed. Twice the samples closes it; raising the
        // repair target instead does not, which says the problem was seeing rather than
        // fixing.
        public const int RouteSamples = 64;
        public const int MaxRepairs = 12;

        /// <summary>
        /// Donors the loop may try per repair it keeps. A rejected move costs an
        /// attempt and leaves the layout as it was, so without this the cap would be
        /// spent on candidates rather than on repairs.
        /// </summary>
        const int RepairAttempts = 4;

        /// <summary>Landing spots offered per donor, emptiest first.</summary>
        const int RepairTargets = 3;

        public static EncounterLayout Place(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                            LevelRecipe recipe, DeterministicRandom rng,
                                            int startIndex, int goalIndex)
        {
            var layout = new EncounterLayout();
            if (corridors == null || corridors.Count == 0 || startIndex < 0 || goalIndex < 0)
                return layout;

            var band = ThreatBand.Build(grid, startIndex, goalIndex);
            if (band == null) return layout;

            layout.BandTiles = band.Tiles.Count;

            var occupied = new HashSet<int>();
            int budget = recipe.EnemyBudget;

            budget -= GuardTheFords(grid, band, recipe, rng, layout, occupied, budget);

            // Traps keep their own occupancy, and this is not tidiness.
            //
            // Sharing one set meant a trap reserved ground against *groups* as well, at
            // the group's spacing of five tiles — and now that traps go to the throats,
            // that is a five-tile hole punched in the enemy placement at exactly the
            // tiles every route converges on. Measured straight after the throat change:
            // 1-1 fell to two groups on a drawn route against a promise of five, and the
            // repair loop could not put it back because the ground it wanted was
            // reserved by a pit.
            //
            // A group standing on a trap is the one overlap worth refusing, and traps go
            // down first, so groups still check the trap tiles themselves.
            var mined = new HashSet<int>();

            budget -= LayTraps(grid, band, corridors, recipe, rng, layout, mined, budget);
            ScatterEnemies(grid, band, recipe, rng, layout, occupied, mined, budget);

            AssignTerritories(grid, layout);
            TallySilver(layout, recipe);
            VerifyAndRepair(grid, band, corridors, recipe, rng, layout, occupied,
                            startIndex, goalIndex);
            return layout;
        }

        // --- The band ---------------------------------------------------------------

        /// <summary>
        /// Every tile a sane crossing could pass through, and what it is worth
        /// threatening. Two travel fields — one from the start, one from the goal — are
        /// what let the placer reason about every route at once instead of about three.
        /// </summary>
        sealed class ThreatBand
        {
            public readonly List<int> Tiles = new List<int>();
            public float[] Weight;
            public float[] FromStart;
            public float[] FromGoal;
            public float Fastest;

            public static ThreatBand Build(TileGrid grid, int startIndex, int goalIndex)
            {
                grid.ToCoords(startIndex, out int sx, out int sy);
                grid.ToCoords(goalIndex, out int gx, out int gy);

                var band = new ThreatBand
                {
                    FromStart = TravelField(grid, sx, sy),
                    FromGoal = TravelField(grid, gx, gy),
                    Weight = new float[grid.TileCount]
                };

                band.Fastest = band.FromStart[goalIndex];
                if (float.IsInfinity(band.Fastest) || band.Fastest <= 0f) return null;

                float limit = band.Fastest * BandSlack;

                for (int i = 0; i < grid.TileCount; i++)
                {
                    float total = band.FromStart[i] + band.FromGoal[i];
                    if (float.IsInfinity(total) || total > limit) continue;
                    if (band.FromStart[i] < SafeEndCost || band.FromGoal[i] < SafeEndCost) continue;

                    // And in a straight line as well as in travel cost. The two are not
                    // the same thing and only the second was checked: eight of cost is
                    // two and a half tiles on a road, so a group could stand four tiles
                    // from the start and satisfy it. That is inside the distance the
                    // rule is written in — being ambushed before the caravan has moved
                    // is not a decision the player could have made differently — and it
                    // held only by luck of ordering until the trap change disturbed it.
                    if (Near(grid, i, startIndex) || Near(grid, i, goalIndex)) continue;

                    if (TerrainTable.Speed(grid[i]) <= 0f) continue;

                    // Threat follows cover, and nothing else. This used to be
                    // `speed * AmbushWeight` — the corridor rule, the quick way is the
                    // dangerous way, restated per tile — and the two factors pull against
                    // each other: docs/GDD.md §3.1 puts its highest ambush weight on the
                    // forest at 1.5, which is also among the slowest ground at ×0.70.
                    // Multiplied by speed, the forest scored 1.05 and the open plain
                    // 0.80, so groups drifted out of the cover the table sends them to.
                    //
                    // Speed against safety is not lost with it. The road carries that on
                    // its own: fastest ground on the map at ×1.25, and second most
                    // dangerous at 1.2. Taking it is still a trade.
                    // Cubed, because the table's own range is too narrow to choose with.
                    //
                    // Ambush weight runs from 0.8 on the plain to 1.5 in the forest, so
                    // a weighted draw preferred cover by less than two to one — barely a
                    // lean. It went unnoticed while the corridors ran close together and
                    // the ground a route could reach was mostly the same ground; once
                    // they spread across the map the reachable country grew more varied
                    // than the placer's picks, and the enemies ended up in cover that was
                    // *thinner* than the average the routes crossed. Cubing turns the
                    // same ordering into a six-to-one preference and nothing else about
                    // the table changes.
                    float ambush = TerrainTable.AmbushWeight(grid[i]);

                    band.Tiles.Add(i);
                    band.Weight[i] = ambush * ambush * ambush;
                }

                return band.Tiles.Count == 0 ? null : band;
            }

            public bool Contains(int tile) => Weight[tile] > 0f;

            /// <summary>
            /// Whether a tile is within <see cref="SafeEndTiles"/> of one of the ends,
            /// measured across the ground rather than along a path.
            /// </summary>
            static bool Near(TileGrid grid, int tile, int end)
            {
                grid.ToCoords(tile, out int x, out int y);
                grid.ToCoords(end, out int ex, out int ey);

                int dx = x - ex;
                int dy = y - ey;

                return dx * dx + dy * dy < SafeEndTiles * SafeEndTiles;
            }
        }

        /// <summary>
        /// Cheapest travel cost from one tile to every other, over the same eight
        /// neighbours and the same costs the pathfinder uses.
        /// </summary>
        static float[] TravelField(TileGrid grid, int x, int y)
        {
            int n = grid.TileCount;
            var distance = new float[n];
            var settled = new bool[n];
            for (int i = 0; i < n; i++) distance[i] = float.PositiveInfinity;

            if (!grid.IsPassable(x, y)) return distance;

            int source = grid.ToIndex(x, y);
            distance[source] = 0f;

            var queue = new SortedSet<(float cost, int tile)> { (0f, source) };

            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);
                if (settled[current.tile]) continue;
                settled[current.tile] = true;

                grid.ToCoords(current.tile, out int cx, out int cy);

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Neighbours.DX[d];
                    int ny = cy + Neighbours.DY[d];
                    if (!grid.IsPassable(nx, ny)) continue;

                    if (d >= 4)
                    {
                        if (!grid.IsPassable(cx + Neighbours.DX[d], cy)) continue;
                        if (!grid.IsPassable(cx, cy + Neighbours.DY[d])) continue;
                    }

                    int neighbour = grid.ToIndex(nx, ny);
                    if (settled[neighbour]) continue;

                    float step = TerrainTable.TravelCost(grid[neighbour]);
                    if (d >= 4) step *= 1.41421356f;

                    float candidate = current.cost + step;
                    if (candidate >= distance[neighbour]) continue;

                    distance[neighbour] = candidate;
                    queue.Add((candidate, neighbour));
                }
            }

            return distance;
        }

        static class Neighbours
        {
            public static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
            public static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        }

        // --- Placement --------------------------------------------------------------

        /// <summary>
        /// A group on every ford in the band. The river can only be crossed at its
        /// fords, so this is the one placement no drawn route can walk around —
        /// everything else in this file is a probability, and this is the floor.
        /// </summary>
        static int GuardTheFords(TileGrid grid, ThreatBand band, LevelRecipe recipe,
                                 DeterministicRandom rng, EncounterLayout layout,
                                 HashSet<int> occupied, int budget)
        {
            int spent = 0;

            foreach (var crossing in FordCrossings(grid, band))
            {
                if (spent >= budget) break;

                // The middle of the crossing, so the guard stands on the ford rather
                // than at the water's edge where a route can slip past it.
                int tile = crossing[crossing.Count / 2];
                if (occupied.Contains(tile)) continue;

                var kind = PickAffordable(recipe.EnemyPool, rng, budget - spent);
                if (kind == null) break;

                layout.Enemies.Add(new EnemySpawn
                {
                    Tile = tile,
                    Kind = kind.Value,
                    Origin = PlacementOrigin.Guard
                });
                occupied.Add(tile);
                spent += EnemyTable.Points(kind.Value);
                layout.FordGuards++;
            }

            return spent;
        }

        /// <summary>Ford tiles grouped into crossings, one group per place the river can be forded.</summary>
        static List<List<int>> FordCrossings(TileGrid grid, ThreatBand band)
        {
            var crossings = new List<List<int>>();
            var seen = new HashSet<int>();

            foreach (int tile in band.Tiles)
            {
                if (grid[tile] != TerrainType.Ford || seen.Contains(tile)) continue;

                var group = new List<int>();
                var stack = new Stack<int>();
                stack.Push(tile);
                seen.Add(tile);

                while (stack.Count > 0)
                {
                    int current = stack.Pop();
                    group.Add(current);
                    grid.ToCoords(current, out int cx, out int cy);

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (!grid.InBounds(nx, ny)) continue;

                            int neighbour = grid.ToIndex(nx, ny);
                            if (seen.Contains(neighbour)) continue;
                            if (grid[neighbour] != TerrainType.Ford) continue;
                            if (!band.Contains(neighbour)) continue;

                            seen.Add(neighbour);
                            stack.Push(neighbour);
                        }
                    }
                }

                group.Sort();
                crossings.Add(group);
            }

            return crossings;
        }

        /// <summary>
        /// Traps at the crossing's narrow points, not scattered over the country.
        ///
        /// The old version spread them by <see cref="TerrainTable.TrapDensity"/> across
        /// the whole threat band, and a band is three thousand tiles. Level 1-8 laid
        /// fourteen; a run down its fast corridor revealed two and triggered none. A trap
        /// has a three-metre trigger and no territory — unlike a group, which comes to
        /// you — so a trap forty metres off the line the player drew is scenery. Three
        /// tests failed on that and none of them said so in those words, because a trap
        /// that never fires reads as a squad that took no damage.
        ///
        /// It also left three of the game's own answers with no question. The scout
        /// reveals traps at 10 m, the sapper disarms in 2 s, the shield-bearer absorbs
        /// the damage — "three different answers to the same problem" (docs/GDD.md §7.2)
        /// — and the marching order exists so that order 0 walks into traps first and
        /// order 3 crosses a trap field last (§4.2). None of that is reachable content
        /// while traps do not fire.
        ///
        /// So they go where the country is narrow, and narrowness is measured rather
        /// than guessed at. Both travel fields are already built: a tile's *detour* is
        /// `FromStart + FromGoal - Fastest`, which is zero on an ideal crossing and grows
        /// as you go round. Cut the crossing into slices by depth, count the tiles in
        /// each slice whose detour is inside <see cref="ThroatSlack"/>, and that count is
        /// how many ways past there are at that depth. The smallest counts are the fords,
        /// the passes and the dry line through a bog — the places every route has to
        /// share whichever line the player draws.
        ///
        /// Terrain only flavours it. `TrapDensity` picks *which* tile of a throat, and
        /// the geometry picks the throat — which is why the ford's low density in §3.1
        /// no longer keeps traps off the most unavoidable ground on the map.
        /// </summary>
        static int LayTraps(TileGrid grid, ThreatBand band, IReadOnlyList<Corridor> corridors,
                            LevelRecipe recipe, DeterministicRandom rng, EncounterLayout layout,
                            HashSet<int> occupied, int budget)
        {
            int allowance = (int)(budget * TrapBudgetShare * recipe.TrapDensity);
            if (allowance <= 0) return 0;

            int spent = 0;

            foreach (var throat in Throats(grid, band, Crossed(grid, corridors)))
            {
                if (spent >= allowance * ThroatShare) break;

                foreach (int tile in throat.Tiles)
                {
                    if (spent >= allowance * ThroatShare) break;
                    spent += Lay(grid, rng, layout, occupied, tile, allowance - spent);
                }
            }

            // Its own share, not the leftovers. Handing the strew whatever the throats
            // could not spend makes the total depend on how much legal ground the throats
            // happened to have, which is how 1-7 went from three survivable routes to
            // none between one run and the next without the allowance changing at all.
            spent += Strew(grid, band, rng, layout, occupied,
                           (int)(allowance * (1f - ThroatShare)));

            return spent;
        }

        /// <summary>
        /// The other third: traps out in the country, by terrain, the way all of them
        /// used to be placed. See <see cref="ThroatShare"/> for why both halves exist.
        /// </summary>
        static int Strew(TileGrid grid, ThreatBand band, DeterministicRandom rng,
                         EncounterLayout layout, HashSet<int> occupied, int allowance)
        {
            if (allowance <= 0) return 0;

            var scored = new List<KeyValuePair<float, int>>();

            foreach (int tile in band.Tiles)
            {
                if (occupied.Contains(tile)) continue;

                float density = TerrainTable.TrapDensity(grid[tile]);
                if (density <= 0f) continue;

                scored.Add(new KeyValuePair<float, int>(density * rng.Range(0.5f, 1.5f), tile));
            }

            scored.Sort((a, b) => b.Key.CompareTo(a.Key));

            int spent = 0;
            foreach (var entry in scored)
            {
                if (spent >= allowance) break;
                spent += Lay(grid, rng, layout, occupied, entry.Value, allowance - spent);
            }

            return spent;
        }

        /// <summary>
        /// Puts one trap on a tile if it will take one, and returns what it cost.
        ///
        /// Six pits to four log traps. A pit is the cheaper lesson — 80 damage and two
        /// seconds pinned — and the log trap is the one that punishes a bunched column,
        /// five metres of it at 120 (docs/GDD.md §7.2).
        /// </summary>
        static int Lay(TileGrid grid, DeterministicRandom rng, EncounterLayout layout,
                       HashSet<int> occupied, int tile, int left)
        {
            if (occupied.Contains(tile)) return 0;
            if (!SpacedEnough(grid, tile, occupied, TrapSpacingTiles)) return 0;

            var kind = rng.Chance(0.6f) ? TrapKind.Pit : TrapKind.Log;

            int cost = TrapTable.Points(kind);
            if (cost > left) return 0;

            layout.Traps.Add(new TrapPlacement
            {
                Tile = tile,
                Kind = kind,
                Origin = PlacementOrigin.Scattered
            });

            occupied.Add(tile);
            return cost;
        }

        /// <summary>One narrow point of the crossing, and the tiles that make it up.</summary>
        struct Throat
        {
            public int Ways;
            public List<int> Tiles;
        }

        /// <summary>
        /// The crossing's narrow points, narrowest first, with each one's tiles ordered
        /// by how central they are and how well the ground hides a trap.
        ///
        /// The centre first matters: a throat is laid across from the middle outward, so
        /// that a budget which runs out has closed the line anyone would actually walk
        /// rather than decorated its edges.
        /// </summary>
        /// <summary>
        /// How many of the offered corridors pass over or beside each tile.
        ///
        /// The throats alone were not enough, and the way they failed is worth keeping.
        /// A throat is narrow ground on the *ideal* crossing — the one the travel fields
        /// describe — and the three corridors a player is offered are generated lines
        /// that only roughly follow it. So the traps landed in the right stretch of
        /// country and a tile or two off the road: on 1-8 with a lone shieldbearer, not
        /// one trap was so much as *seen*, and with a scout in the squad they were seen
        /// and never trodden on. A three-metre trigger on a four-metre tile does not
        /// forgive being one tile out.
        ///
        /// Counted with a tile of slack, because a route runs corner to corner and a
        /// trap beside the line still catches the column's flank.
        /// </summary>
        static int[] Crossed(TileGrid grid, IReadOnlyList<Corridor> corridors)
        {
            var crossed = new int[grid.TileCount];
            if (corridors == null) return crossed;

            var counted = new HashSet<int>();

            foreach (var corridor in corridors)
            {
                counted.Clear();

                foreach (int tile in corridor.Tiles)
                {
                    grid.ToCoords(tile, out int x, out int y);

                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (!grid.IsPassable(x + dx, y + dy)) continue;
                            counted.Add(grid.ToIndex(x + dx, y + dy));
                        }
                }

                // Once per corridor however many of its tiles touch this one.
                foreach (int tile in counted) crossed[tile]++;
            }

            return crossed;
        }

        static List<Throat> Throats(TileGrid grid, ThreatBand band, int[] crossed)
        {
            var slices = new List<int>[ThroatSlices];
            float slack = band.Fastest * ThroatSlack;

            foreach (int tile in band.Tiles)
            {
                float detour = band.FromStart[tile] + band.FromGoal[tile] - band.Fastest;
                if (detour > slack) continue;

                int slice = (int)(band.FromStart[tile] / band.Fastest * ThroatSlices);
                if (slice < 0) slice = 0;
                if (slice >= ThroatSlices) slice = ThroatSlices - 1;

                (slices[slice] ?? (slices[slice] = new List<int>())).Add(tile);
            }

            var throats = new List<Throat>();

            for (int i = 0; i < ThroatSlices; i++)
            {
                if (slices[i] == null || slices[i].Count == 0) continue;

                var tiles = slices[i];

                // Dead centre of the throat first, and among equals the ground that hides
                // a trap best. TrapDensity is a multiplier on how tempting a tile is, so
                // it is applied to the detour rather than compared against it: bare
                // ground has to be twice as central as a bog to be chosen over it.
                tiles.Sort((a, b) => Score(grid, band, crossed, a)
                                    .CompareTo(Score(grid, band, crossed, b)));

                throats.Add(new Throat { Ways = tiles.Count, Tiles = tiles });
            }

            throats.Sort((a, b) => a.Ways.CompareTo(b.Ways));
            return throats;
        }

        /// <summary>
        /// Lower is a better place for a trap: on the most roads, then central, then on
        /// the ground that hides one best.
        ///
        /// Roads first and by a wide margin. A trap on ground all three corridors cross
        /// is one nobody routes around; a trap on perfect ambush ground that no offered
        /// route touches is scenery, which is what the whole scatter used to be.
        /// </summary>
        static float Score(TileGrid grid, ThreatBand band, int[] crossed, int tile)
        {
            float density = TerrainTable.TrapDensity(grid[tile]);

            // A tile the ground refuses outright stays refused, however central it is.
            if (density <= 0f) return float.MaxValue;

            float detour = band.FromStart[tile] + band.FromGoal[tile] - band.Fastest;

            // A whole rank per corridor: three beats two beats one, before anything else
            // is looked at, and the rest decides ties within a rank.
            return -crossed[tile] * 1000f + (detour + 1f) / density;
        }

        /// <summary>The rest of the budget, over the band, weighted by how fast the ground is.</summary>
        static void ScatterEnemies(TileGrid grid, ThreatBand band, LevelRecipe recipe,
                                   DeterministicRandom rng, EncounterLayout layout,
                                   HashSet<int> occupied, HashSet<int> mined, int budget)
        {
            if (budget <= 0) return;

            var scored = new List<KeyValuePair<float, int>>();
            foreach (int tile in band.Tiles)
            {
                if (occupied.Contains(tile)) continue;
                scored.Add(new KeyValuePair<float, int>(band.Weight[tile] * rng.Range(0.6f, 1.4f), tile));
            }
            scored.Sort((a, b) => b.Key.CompareTo(a.Key));

            foreach (var entry in scored)
            {
                if (budget <= 0) break;

                int tile = entry.Value;
                if (occupied.Contains(tile)) continue;

                // The tile itself, not its neighbourhood: a group has no business
                // standing in a pit, and every business standing beside one.
                if (mined.Contains(tile)) continue;
                if (!SpacedEnough(grid, tile, occupied, GroupSpacingTiles)) continue;

                var kind = PickAffordable(recipe.EnemyPool, rng, budget);
                if (kind == null) break;

                layout.Enemies.Add(new EnemySpawn
                {
                    Tile = tile,
                    Kind = kind.Value,
                    Origin = PlacementOrigin.Scattered
                });
                occupied.Add(tile);
                budget -= EnemyTable.Points(kind.Value);
            }
        }

        /// <summary>Groups arrive one at a time, so nothing is placed on top of anything else.</summary>
        static bool SpacedEnough(TileGrid grid, int tile, IEnumerable<int> occupied, float spacing)
        {
            grid.ToCoords(tile, out int x, out int y);
            float limit = spacing * spacing;

            foreach (int other in occupied)
            {
                grid.ToCoords(other, out int ox, out int oy);
                float dx = ox - x, dy = oy - y;
                if (dx * dx + dy * dy < limit) return false;
            }
            return true;
        }

        static EnemyKind? PickAffordable(EnemyKind[] pool, DeterministicRandom rng, int budget)
        {
            var source = pool != null && pool.Length > 0 ? pool : EnemyTable.All;

            var affordable = new List<EnemyKind>();
            foreach (var kind in source)
                if (EnemyTable.Points(kind) <= budget) affordable.Add(kind);

            if (affordable.Count == 0) return null;
            return affordable[rng.Range(0, affordable.Count)];
        }

        /// <summary>
        /// Half the distance to the nearest other group, clamped. Halved so neighbouring
        /// territories meet rather than overlap, and clamped so a group alone in a
        /// corner does not end up watching a quarter of the map.
        /// </summary>
        static void AssignTerritories(TileGrid grid, EncounterLayout layout)
        {
            for (int i = 0; i < layout.Enemies.Count; i++)
            {
                var spawn = layout.Enemies[i];
                grid.ToCoords(spawn.Tile, out int x, out int y);

                float nearest = float.PositiveInfinity;
                for (int j = 0; j < layout.Enemies.Count; j++)
                {
                    if (j == i) continue;
                    grid.ToCoords(layout.Enemies[j].Tile, out int ox, out int oy);
                    float dx = ox - x, dy = oy - y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distance < nearest) nearest = distance;
                }

                float radius = float.IsInfinity(nearest) ? TerritoryMaxTiles : nearest * 0.5f;
                if (radius < TerritoryMinTiles) radius = TerritoryMinTiles;
                if (radius > TerritoryMaxTiles) radius = TerritoryMaxTiles;

                spawn.Territory = radius;
                layout.Enemies[i] = spawn;
            }
        }

        // --- Verification -----------------------------------------------------------

        /// <summary>
        /// Routes a player might actually draw: the three the generator knows, plus
        /// crossings through random waypoints in the band.
        ///
        /// This is the stand-in for the player, and everything the placer promises is
        /// checked against it. A promise that only holds for the three corridors is
        /// exactly the promise that broke when the player was handed a pen.
        /// </summary>
        public static List<List<int>> SampleRoutes(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                                   DeterministicRandom rng, int startIndex,
                                                   int goalIndex, int count = RouteSamples)
        {
            var band = ThreatBand.Build(grid, startIndex, goalIndex);
            return band == null
                ? new List<List<int>>()
                : SampleRoutes(grid, band, corridors, rng, startIndex, goalIndex, count);
        }

        static List<List<int>> SampleRoutes(TileGrid grid, ThreatBand band,
                                            IReadOnlyList<Corridor> corridors,
                                            DeterministicRandom rng, int startIndex,
                                            int goalIndex, int count)
        {
            var routes = new List<List<int>>();
            foreach (var corridor in corridors)
                if (corridor?.Tiles != null && corridor.Tiles.Count > 0)
                    routes.Add(new List<int>(corridor.Tiles));

            grid.ToCoords(startIndex, out int sx, out int sy);
            grid.ToCoords(goalIndex, out int gx, out int gy);

            var pathfinder = new GridPathfinder(grid);
            var leg = new List<int>();

            int guard = 0;
            while (routes.Count < count && band.Tiles.Count > 0 && guard++ < count * 4)
            {
                int waypoints = 1 + rng.Range(0, 2);
                var tiles = new List<int>();
                int fromX = sx, fromY = sy;
                bool broken = false;

                for (int w = 0; w <= waypoints; w++)
                {
                    int toX, toY;
                    if (w < waypoints)
                    {
                        int pick = band.Tiles[rng.Range(0, band.Tiles.Count)];
                        grid.ToCoords(pick, out toX, out toY);
                    }
                    else
                    {
                        toX = gx;
                        toY = gy;
                    }

                    if (!pathfinder.TryFindPath(fromX, fromY, toX, toY, leg, out _))
                    {
                        broken = true;
                        break;
                    }

                    for (int i = tiles.Count == 0 ? 0 : 1; i < leg.Count; i++) tiles.Add(leg[i]);
                    fromX = toX;
                    fromY = toY;
                }

                if (!broken && tiles.Count > 0) routes.Add(tiles);
            }

            return routes;
        }

        /// <summary>Indices of the enemy groups whose territory a route crosses.</summary>
        public static List<int> MetGroups(TileGrid grid, IReadOnlyList<int> route,
                                          EncounterLayout layout)
        {
            var met = new List<int>();
            var onRoute = new HashSet<int>(route);

            for (int i = 0; i < layout.Enemies.Count; i++)
            {
                var spawn = layout.Enemies[i];
                if (onRoute.Contains(spawn.Tile)) { met.Add(i); continue; }

                float reach = spawn.Territory > 0f ? spawn.Territory : EngageRadiusTiles;
                if (WithinReach(grid, route, spawn.Tile, reach)) met.Add(i);
            }

            return met;
        }

        static bool WithinReach(TileGrid grid, IReadOnlyList<int> route, int tile, float reach)
        {
            grid.ToCoords(tile, out int tx, out int ty);
            float limit = reach * reach;

            for (int i = 0; i < route.Count; i++)
            {
                grid.ToCoords(route[i], out int rx, out int ry);
                float dx = rx - tx, dy = ry - ty;
                if (dx * dx + dy * dy <= limit) return true;
            }
            return false;
        }

        /// <summary>
        /// Samples routes and moves threat onto whichever one met too little.
        ///
        /// A scattered field says nothing about the worst case, and the worst case is
        /// the one that matters: a player who happens to draw between the groups gets a
        /// level with no game in it. Repairs move a group rather than add one, so the
        /// budget the difficulty curve is measured against stays exactly what the recipe
        /// asked for. A group no sampled route ever came near is doing nothing where it
        /// stands, so it is the one that moves.
        /// </summary>
        static void VerifyAndRepair(TileGrid grid, ThreatBand band, IReadOnlyList<Corridor> corridors,
                                    LevelRecipe recipe, DeterministicRandom rng,
                                    EncounterLayout layout, HashSet<int> occupied,
                                    int startIndex, int goalIndex)
        {
            var routes = SampleRoutes(grid, band, corridors, rng, startIndex, goalIndex, RouteSamples);
            layout.SampledRoutes = routes.Count;
            if (routes.Count == 0) return;

            var rejected = new HashSet<int>();
            Score(grid, routes, layout, out int fewest, out int tied, out int worst);

            // A rejection costs an attempt but not a repair, so the cap still means
            // what it says: twelve groups moved, not twelve things tried.
            for (int attempt = 0; attempt < MaxRepairs * RepairAttempts; attempt++)
            {
                layout.MinEncounters = fewest;
                if (worst < 0 || fewest >= RepairTarget || layout.Repairs >= MaxRepairs) break;

                int donor = IdlestGroup(grid, routes, layout, rejected);
                if (donor < 0) break;

                var targets = EmptiestStretches(grid, routes[worst], band, occupied);
                if (targets.Count == 0) break;

                var before = layout.Enemies[donor];
                bool kept = false;

                foreach (int target in targets)
                {
                    var moved = before;
                    occupied.Remove(before.Tile);
                    moved.Tile = target;
                    moved.Origin = PlacementOrigin.Repair;
                    layout.Enemies[donor] = moved;
                    occupied.Add(target);
                    AssignTerritories(grid, layout);

                    Score(grid, routes, layout, out int nowFewest, out int nowTied, out int nowWorst);
                    if (nowFewest > fewest || (nowFewest == fewest && nowTied < tied))
                    {
                        fewest = nowFewest;
                        tied = nowTied;
                        worst = nowWorst;
                        kept = true;
                        break;
                    }

                    occupied.Remove(target);
                    layout.Enemies[donor] = before;
                    occupied.Add(before.Tile);
                    AssignTerritories(grid, layout);
                }

                if (!kept)
                {
                    rejected.Add(donor);
                    continue;
                }

                layout.Repairs++;
                rejected.Clear();   // the ground moved; a group that could not help may now
            }

            layout.MinEncounters = fewest;
            // Against the target, not the promise. A level that reaches five on the
            // routes the placer sampled has no margin left for the ones it did not,
            // and re-rolling costs generation time where shipping it costs a level
            // with no game in it.
            layout.EncountersValidated = fewest >= RepairTarget;

            TallySilver(layout, recipe);
            TopUpSilver(grid, routes, recipe, layout, occupied);
        }

        /// <summary>
        /// How good the layout is, worst route first: the fewest groups any sampled
        /// route meets, then how many routes are stuck at that number.
        ///
        /// The second term is what stops a repair from robbing one route to pay
        /// another. Without a score at all the loop had no idea which way was up, and
        /// kept every move it made.
        /// </summary>
        static void Score(TileGrid grid, List<List<int>> routes, EncounterLayout layout,
                          out int fewest, out int tied, out int worst)
        {
            fewest = int.MaxValue;
            worst = -1;

            for (int i = 0; i < routes.Count; i++)
            {
                int met = MetGroups(grid, routes[i], layout).Count;
                if (met >= fewest) continue;
                fewest = met;
                worst = i;
            }

            tied = 0;
            for (int i = 0; i < routes.Count; i++)
                if (MetGroups(grid, routes[i], layout).Count == fewest) tied++;
        }

        /// <summary>
        /// The placed group fewest sampled routes come near.
        ///
        /// Ford guards never move — a guard is the one placement no crossing can
        /// avoid, and spending it elsewhere gives that back. Nor does a group whose
        /// last move was rejected, until an accepted move changes the ground under
        /// the question.
        ///
        /// That second rule is why the loop terminates. Without it the group just
        /// moved was the idlest group on the next pass, because it went somewhere
        /// only one route reaches, so it was picked again — and again. Traced over
        /// forty passes on 2-5 the same band of raiders moved forty times while the
        /// worst route stayed pinned at four.
        /// </summary>
        static int IdlestGroup(TileGrid grid, List<List<int>> routes, EncounterLayout layout,
                               HashSet<int> rejected)
        {
            int best = -1, fewest = int.MaxValue;

            for (int i = 0; i < layout.Enemies.Count; i++)
            {
                var spawn = layout.Enemies[i];
                if (spawn.Origin == PlacementOrigin.Guard || rejected.Contains(i)) continue;

                float reach = spawn.Territory > 0f ? spawn.Territory : EngageRadiusTiles;
                int met = 0;
                foreach (var route in routes)
                    if (WithinReach(grid, route, spawn.Tile, reach)) met++;

                if (met >= fewest) continue;
                fewest = met;
                best = i;
            }

            return best;
        }

        /// <summary>
        /// Tiles on a route furthest from anything already placed, emptiest first.
        ///
        /// More than one, because the emptiest tile is a guess and not an answer. It
        /// is the stretch of road nothing else watches, which is usually where a group
        /// is worth most — but a group put there can cost another route more than it
        /// gains this one, and then the loop wants a second candidate rather than a
        /// different donor. Offering only the best tile left 1-10 giving up after a
        /// single repair.
        ///
        /// Candidates are spaced apart: the three emptiest tiles on a route are
        /// usually neighbours, and three tries at the same stretch of road is one try.
        /// </summary>
        static List<int> EmptiestStretches(TileGrid grid, IReadOnlyList<int> route, ThreatBand band,
                                           HashSet<int> occupied, int count = RepairTargets)
        {
            var scored = new List<(float Distance, int Tile)>();

            foreach (int tile in route)
            {
                if (occupied.Contains(tile) || !band.Contains(tile)) continue;
                if (band.FromStart[tile] < SafeEndCost || band.FromGoal[tile] < SafeEndCost) continue;

                grid.ToCoords(tile, out int x, out int y);
                float nearest = float.PositiveInfinity;

                foreach (int other in occupied)
                {
                    grid.ToCoords(other, out int ox, out int oy);
                    float dx = ox - x, dy = oy - y;
                    float distance = dx * dx + dy * dy;
                    if (distance < nearest) nearest = distance;
                }

                // Emptiness decides where, and cover decides which of the empty places.
                //
                // The loop used to sort on distance alone, and it is the last thing to
                // touch the layout — so on a level needing several repairs it undid the
                // cover-seeking of the scatter it was correcting, and the level shipped
                // with its groups standing in the open. Weighted rather than sorted after,
                // because a tile that is twice as empty should still win over one that
                // merely hides better.
                scored.Add((nearest * band.Weight[tile], tile));
            }

            scored.Sort((a, b) => a.Distance != b.Distance
                ? b.Distance.CompareTo(a.Distance)
                : a.Tile.CompareTo(b.Tile));

            var chosen = new List<int>();
            foreach (var candidate in scored)
            {
                if (SpacedEnough(grid, candidate.Tile, chosen, GroupSpacingTiles))
                    chosen.Add(candidate.Tile);
                if (chosen.Count >= count) break;
            }

            return chosen;
        }


        static void TallySilver(EncounterLayout layout, LevelRecipe recipe)
        {
            float multiplier = recipe.SilverMultiplier <= 0f ? 1f : recipe.SilverMultiplier;
            int total = 0;

            foreach (var spawn in layout.Enemies)
                total += (int)(EnemyTable.GroupSilver(spawn.Kind) * multiplier);

            foreach (var trap in layout.Traps)
                total += (int)(TrapTable.DisarmSilver(trap.Kind) * multiplier);

            foreach (var cache in layout.SilverCaches)
                total += cache.Amount;

            layout.TotalSilver = total;
        }

        /// <summary>
        /// A cache wherever a sampled route could not earn the floor.
        ///
        /// Same reasoning as the per-corridor top-up it replaces: a route that cannot
        /// pay for two upgrades leaves the player at the level's last fight with an army
        /// they had no way to improve, which is broken rather than hard. Only the unit
        /// of measurement changed, from the corridor to the line the player might draw.
        /// </summary>
        static void TopUpSilver(TileGrid grid, List<List<int>> routes, LevelRecipe recipe,
                                EncounterLayout layout, HashSet<int> occupied)
        {
            layout.SilverValidated = true;
            float multiplier = recipe.SilverMultiplier <= 0f ? 1f : recipe.SilverMultiplier;

            foreach (var route in routes)
            {
                int earned = 0;

                foreach (int index in MetGroups(grid, route, layout))
                    earned += (int)(EnemyTable.GroupSilver(layout.Enemies[index].Kind) * multiplier);

                for (int i = 0; i < layout.Traps.Count; i++)
                    if (WithinReach(grid, route, layout.Traps[i].Tile, EngageRadiusTiles))
                        earned += (int)(TrapTable.DisarmSilver(layout.Traps[i].Kind) * multiplier);

                for (int i = 0; i < layout.SilverCaches.Count; i++)
                    if (WithinReach(grid, route, layout.SilverCaches[i].Tile, EngageRadiusTiles))
                        earned += layout.SilverCaches[i].Amount;

                int shortfall = recipe.MinSilverPerRoute - earned;
                if (shortfall <= 0) continue;

                int tile = FreeTileOn(route, occupied);
                if (tile < 0) { layout.SilverValidated = false; continue; }

                layout.SilverCaches.Add(new SilverCache
                {
                    Tile = tile,
                    Amount = shortfall,
                    Origin = PlacementOrigin.Scattered
                });
                occupied.Add(tile);
            }

            TallySilver(layout, recipe);
        }

        static int FreeTileOn(IReadOnlyList<int> route, HashSet<int> occupied)
        {
            int middle = route.Count / 2;

            for (int offset = 0; offset < route.Count; offset++)
            {
                for (int direction = 1; direction >= -1; direction -= 2)
                {
                    int i = middle + offset * direction;
                    if (i < SafeEndTiles || i >= route.Count - SafeEndTiles) continue;
                    if (!occupied.Contains(route[i])) return route[i];
                }
            }
            return -1;
        }
    }
}
