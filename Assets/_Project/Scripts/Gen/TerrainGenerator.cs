using System;
using System.Collections.Generic;
using Arna.Sim;

namespace Arna.Gen
{
    /// <summary>
    /// Turns a recipe plus a seed into playable terrain
    /// (docs/content-pipeline.md §3, steps 1–4).
    ///
    /// Everything here is deterministic: the same inputs must produce a byte-identical
    /// map on every device, because that is the only thing that makes 1000 levels
    /// shippable without distributing 1000 level files.
    /// </summary>
    public static class TerrainGenerator
    {
        /// <summary>
        /// Ordering along the elevation axis. The noise field is a height map, so
        /// mapping low values to water and high values to mountains is what makes
        /// the result read as landscape rather than as static.
        /// </summary>
        static int ElevationRank(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Water: return 0;
                case TerrainType.Marsh: return 10;
                case TerrainType.Ford: return 20;
                case TerrainType.Plains: return 30;
                case TerrainType.Road: return 35;
                case TerrainType.Forest: return 40;
                case TerrainType.MountainPass: return 50;
                case TerrainType.Cliff: return 60;
                default: return 100;
            }
        }

        const int EdgeBand = 3;
        const int PairAttempts = 48;

        public static LevelMap Generate(LevelRecipe recipe, int seed)
        {
            int attempts = Math.Max(1, recipe.MaxGenerationAttempts);

            LevelMap best = null;
            bool bestKept = false, bestValid = false;
            int bestPassable = -1;
            float bestSpread = -1f;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                // Derive an independent stream per attempt while keeping the level's
                // public identity — its seed — unchanged.
                var rng = new DeterministicRandom(seed + attempt * 7919);

                var grid = BuildTerrain(recipe, rng);
                CarveRivers(grid, recipe, rng);

                if (!TryPlaceEndpoints(grid, recipe, rng, out int sx, out int sy, out int gx, out int gy))
                    continue;

                var corridors = CorridorFinder.Find(grid, sx, sy, gx, gy);
                if (corridors.Count == 0) continue;

                bool valid = CorridorFinder.IsMeaningfulChoice(corridors);

                // Encounters need the endpoints as well as the corridors now: the
                // distribution is derived from the ground a route can be drawn through,
                // and the corridors are only the first three samples of that.
                var encounters = EncounterPlacer.Place(grid, corridors, recipe, rng,
                                                       grid.ToIndex(sx, sy), grid.ToIndex(gx, gy));

                var map = new LevelMap(grid, seed, sx, sy, gx, gy,
                                       corridors[0].TravelCost, corridors, valid, attempt + 1, encounters);

                // Both, or roll again. The placer repairs what it can and says so when
                // it could not, and a level where any drawn line meets almost nothing is
                // not one to ship — it is one to re-roll, which costs generation time
                // and nothing else.
                //
                // Retrying on `valid` alone was the old rule, and it aged badly. It
                // measures whether the three corridors the generator found differ from
                // each other, and the player stopped choosing between them when they
                // were given a pen. The promise that replaced it — MinEncounters — was
                // not a criterion at all, so the generator would re-roll twelve times
                // for a property nobody reads and accept a level that broke the one the
                // whole mechanic rests on.
                // And whether the level can be got through, which nothing here has ever
                // asked.
                //
                // The chapter promises a way through every level and two outside the
                // escalation band, and that promise lived only in a test: the generator
                // re-rolled for corridor quality and for silver and accepted whatever
                // difficulty fell out. It survived on luck. Widening the corridors —
                // which fixed three levels of ten offering the same road twice — spent
                // that luck, and 1-5 came out with all three routes fatal.
                //
                // Simulating a run per attempt is far too slow to do at load time, but
                // the arithmetic is not: the danger a route carries is the points of the
                // groups that route meets, and the measurement is unambiguous. Over
                // chapter 1, routes that arrive carry thirty to forty-five points and
                // routes that end with the caravan destroyed carry fifty to seventy,
                // with enemy strength scaling both. See SurvivableDanger.
                int passable = PassableRoutes(grid, corridors, encounters, recipe);

                // Two promises, ranked apart rather than folded together. Conflated into
                // one flag, a level with no encounters worth the name could outrank one
                // whose encounters were right — and 1-1 shipped without them.
                bool kept = encounters.EncountersValidated;
                bool passes = passable >= recipe.RoutesOwed;

                if (valid && kept && passes) return map;

                // Keep the least-bad candidate: promise first, then a meaningful
                // choice, then the corridors that differ most.
                float spread = SpreadOf(corridors);
                if (Better(kept, valid, passable, spread,
                           bestKept, bestValid, bestPassable, bestSpread))
                {
                    bestKept = kept;
                    bestValid = valid;
                    bestPassable = passable;
                    bestSpread = spread;
                    best = map;
                }
            }

            return best ?? Fallback(recipe, seed, attempts);
        }

        /// <summary>
        /// The danger one route may carry and still be survivable, in enemy points
        /// scaled by the level's enemy strength.
        ///
        /// Forty-two, and the number is a measurement rather than a judgement. Across
        /// chapter 1: routes that arrived carried 45.0, 30.0, 40.0, 39.5, 42.1, 48.6,
        /// 40.3 and 39.2 of it; routes that ended with the caravan destroyed carried
        /// 59.4, 60.3, 81.2 and 58.0.
        ///
        /// That reading put it at fifty-two, and the same measurement moved it to
        /// forty-two the moment the placer's preference for cover was sharpened: the
        /// points a route meets are the same, and a group that meets them from a forest
        /// rather than from a plain is worth more of them. A proxy calibrated against
        /// one version of the placement has to be recalibrated when the placement
        /// changes, which is the cost of not simulating — and simulating a run per
        /// generation attempt is far beyond a load-time budget. Below thirty-eight the
        /// gate stops finding any map it likes and ships the least-bad instead, which is
        /// how a threshold that is too strict fails.
        /// </summary>
        const float SurvivableDanger = 42f;

        // What a level owes comes from the chapter shape now — LevelRecipe.RoutesOwed —
        // rather than from one number here. Two everywhere was the first gate, and it is
        // the wrong shape: 2-10 is the boss of a harder chapter, owes one, and spent
        // twelve attempts failing to find two before shipping the least-bad anyway.

        static int PassableRoutes(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                  EncounterLayout encounters, LevelRecipe recipe)
        {
            int passable = 0;

            foreach (var corridor in corridors)
            {
                float danger = 0f;

                foreach (int met in EncounterPlacer.MetGroups(grid, corridor.Tiles, encounters))
                    danger += EnemyTable.Points(encounters.Enemies[met].Kind);

                if (danger * recipe.EnemyStrength <= SurvivableDanger) passable++;
            }

            return passable;
        }

        /// <summary>Ranks two failed attempts, worst-case promise first.</summary>
        static bool Better(bool kept, bool valid, int passable, float spread,
                           bool bestKept, bool bestValid, int bestPassable, float bestSpread)
        {
            if (kept != bestKept) return kept;

            // Ways through, ahead of whether the corridors differ: a level offering two
            // survivable routes that resemble each other is a worse picture and a better
            // game than one offering three distinct ways to die.
            if (passable != bestPassable) return passable > bestPassable;

            if (valid != bestValid) return valid;
            return spread > bestSpread;
        }

        /// <summary>
        /// How much of a choice a rejected candidate still offers, for ranking one
        /// against another. Higher is better.
        ///
        /// <b>Between the fast road and the safe road, and nothing else.</b> This used to
        /// be slowest-minus-fastest over all three corridors, which the odd route decides
        /// on its own: it is forced away from the other two and is therefore always the
        /// slowest, so the tiebreak was measuring how slow the detour came out while
        /// saying it measured how much the roads differ. A candidate whose fast and safe
        /// routes were the *same road* outranked one where they genuinely parted, as long
        /// as its odd route was slower.
        ///
        /// That is why letting the generator try harder did not help. Measured over
        /// chapter 1, raising the attempt ceiling from 12 to 24 to 48 to 96 moved the
        /// fast-safe overlap around at random — level 10 went 100, 23, 100, 100 percent —
        /// because more attempts only meant a different arbitrary candidate won a ranking
        /// that was indifferent to the thing being ranked.
        ///
        /// Two terms, added rather than ranked, because either one alone is satisfiable
        /// by a road that is simply worse: a parallel line that costs nothing is not a
        /// safer road, and a slower line along the same ground is not a different one.
        /// </summary>
        static float SpreadOf(IReadOnlyList<Corridor> corridors)
        {
            Corridor fast = null, safe = null;

            foreach (var corridor in corridors)
            {
                if (corridor.Kind == CorridorKind.Fast) fast = corridor;
                else if (corridor.Kind == CorridorKind.Safe) safe = corridor;
            }

            if (fast == null || safe == null || fast.TravelCost <= 0f) return 0f;

            float apart = 1f - CorridorFinder.Overlap(fast, safe);
            float slower = (safe.TravelCost - fast.TravelCost) / fast.TravelCost;

            return apart + (slower > 0f ? slower : 0f);
        }

        /// <summary>
        /// Last resort when every attempt failed to place endpoints at all. Produces
        /// a plain crossable map rather than returning null: an ugly level is a bug
        /// report, a null reference is a crash on the player's phone.
        /// </summary>
        static LevelMap Fallback(LevelRecipe recipe, int seed, int attempts)
        {
            var grid = new TileGrid(recipe.Width, recipe.Height, TerrainType.Plains);
            int y = recipe.Height / 2;
            var corridors = CorridorFinder.Find(grid, 0, y, recipe.Width - 1, y);
            float cost = corridors.Count > 0 ? corridors[0].TravelCost : 0f;
            return new LevelMap(grid, seed, 0, y, recipe.Width - 1, y, cost, corridors, false, attempts);
        }

        // --- Step 1: natural terrain ------------------------------------------------

        static TileGrid BuildTerrain(LevelRecipe recipe, DeterministicRandom rng)
        {
            int w = recipe.Width, h = recipe.Height;
            var grid = new TileGrid(w, h);

            // Offsetting into the noise field rather than reseeding the hash keeps
            // neighbouring seeds from producing subtly similar coastlines.
            float ox = rng.Range(0f, 4096f);
            float oy = rng.Range(0f, 4096f);
            int noiseSeed = (int)rng.NextUInt();

            float scale = recipe.NoiseScale <= 0f ? 18f : recipe.NoiseScale;
            var field = new float[w * h];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    field[y * w + x] = ValueNoise.Fbm(
                        ox + x / scale, oy + y / scale, noiseSeed, Math.Max(1, recipe.NoiseOctaves));

            Blur(field, w, h, recipe.SmoothingPasses);
            ApplyMixByQuantile(grid, field, recipe.TerrainMix);

            // Keep the height field rather than discarding it once terrain types are
            // assigned: the play view needs it to stand the world up.
            for (int i = 0; i < field.Length; i++) grid.SetElevation(i, field[i]);

            return grid;
        }

        /// <summary>
        /// Box blur over the height field, before terrain types are assigned.
        ///
        /// Smoothing the field rather than the assigned tiles matters: a majority
        /// filter over terrain types would quietly shift the mix away from what the
        /// recipe asked for, while blurring beforehand leaves the quantile split — and
        /// therefore the requested proportions — exact.
        /// </summary>
        static void Blur(float[] field, int w, int h, int passes)
        {
            if (passes <= 0) return;
            var tmp = new float[field.Length];

            for (int p = 0; p < passes; p++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f;
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= w) continue;
                                sum += field[ny * w + nx];
                                n++;
                            }
                        }
                        tmp[y * w + x] = sum / n;
                    }
                }
                Array.Copy(tmp, field, field.Length);
            }
        }

        /// <summary>
        /// Assigns terrain by splitting the sorted noise values at the cumulative
        /// shares. Thresholding on fixed noise values would leave the actual
        /// proportions at the mercy of the noise distribution — a recipe asking for
        /// 10 % marsh could produce 2 % or 25 %. Cutting on quantiles instead makes
        /// the requested mix come out right whatever the noise happens to look like,
        /// which is what lets a designer tune a chapter by editing numbers.
        /// </summary>
        static void ApplyMixByQuantile(TileGrid grid, float[] field, TerrainShare[] mix)
        {
            int n = field.Length;

            var ordered = new TerrainShare[mix.Length];
            Array.Copy(mix, ordered, mix.Length);
            Array.Sort(ordered, (a, b) => ElevationRank(a.Type).CompareTo(ElevationRank(b.Type)));

            float total = 0f;
            foreach (var s in ordered) total += s.Share > 0f ? s.Share : 0f;
            if (total <= 0f)
            {
                for (int i = 0; i < n; i++) grid[i] = TerrainType.Plains;
                return;
            }

            var sorted = new float[n];
            Array.Copy(field, sorted, n);
            Array.Sort(sorted);

            var cut = new float[ordered.Length];
            float cumulative = 0f;
            for (int i = 0; i < ordered.Length; i++)
            {
                cumulative += (ordered[i].Share > 0f ? ordered[i].Share : 0f) / total;
                int idx = (int)(cumulative * (n - 1));
                if (idx < 0) idx = 0;
                if (idx >= n) idx = n - 1;
                cut[i] = sorted[idx];
            }
            cut[ordered.Length - 1] = float.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                float v = field[i];
                int band = ordered.Length - 1;
                for (int b = 0; b < ordered.Length; b++)
                {
                    if (v <= cut[b]) { band = b; break; }
                }
                grid[i] = ordered[band].Type;
            }
        }

        // --- Step 2: rivers ---------------------------------------------------------

        /// <summary>
        /// Cuts rivers from the north edge to the south edge, across the caravan's
        /// west-to-east travel, and opens a handful of fords.
        ///
        /// This is what turns a permeable map into one with decisions on it. Scattered
        /// lakes get walked around without thought; a river with three crossings makes
        /// the player pick one, and every enemy placement downstream of that choice
        /// suddenly means something.
        /// </summary>
        static void CarveRivers(TileGrid grid, LevelRecipe recipe, DeterministicRandom rng)
        {
            for (int r = 0; r < recipe.Rivers; r++)
            {
                int startX = rng.Range(grid.Width / 6, grid.Width - grid.Width / 6);
                int endX = rng.Range(grid.Width / 6, grid.Width - grid.Width / 6);

                var path = MeanderSouth(grid, startX, endX, rng);
                if (path.Count == 0) continue;

                foreach (int tile in path) grid[tile] = TerrainType.Water;
                PlaceFords(grid, path, Math.Max(1, recipe.FordsPerRiver));
            }

            SinkTheChannel(grid);
            LevelTheCrossings(grid);
        }

        /// <summary>
        /// How far below its banks a river's bed lies, as a share of the height field.
        ///
        /// A tenth, which the run's fourteen metres of relief make about 1.4 m — a stream
        /// you would wade rather than a gorge.
        /// </summary>
        public const float ChannelDepth = 0.1f;

        /// <summary>
        /// Digs the riverbed.
        ///
        /// <b>There was never one.</b> LevelTheCrossings has said since it was written
        /// that "a river is carved by lowering its tiles", and nothing lowered them: a
        /// water tile kept whatever height the noise field gave the meadow beside it, and
        /// WaterMeshBuilder then laid the surface a third of a metre *above* that. So the
        /// river ran along the top of the ground instead of through it — a blue film on a
        /// green field, which is what a plate looks like and is exactly what it was
        /// reported as.
        ///
        /// The channel is cut here, before the crossings are levelled, because a ford is
        /// defined against its banks and must be raised back afterwards. Nothing else in
        /// the pipeline reads elevation before this point.
        ///
        /// <b>The banks slope on their own.</b> Nothing feathers the edge here and
        /// nothing needs to: the rendered surface is interpolated between corners, and a
        /// corner is the average of the four tiles meeting at it
        /// (TileGrid.CornerElevation), so a bankside corner is already half meadow and
        /// half riverbed. Cutting the tile cuts a slope into the bank for free. Feathering
        /// as well would double it and turn every stream into a valley.
        /// </summary>
        static void SinkTheChannel(TileGrid grid)
        {
            for (int i = 0; i < grid.TileCount; i++)
                if (grid[i] == TerrainType.Water)
                    grid.SetElevation(i, grid.Elevation(i) - ChannelDepth);
        }

        /// <summary>
        /// Raises every ford to the height of the banks it joins.
        ///
        /// A river is carved by lowering its tiles, and a ford was carved with the rest
        /// of it: the crossing sat at the bottom of the channel. Everything in the game
        /// takes its height from the ground, so the caravan drove *down into the river*
        /// at the one place it is supposed to get across — and the bridge, standing on
        /// the same ground, arched over the top of it. It looked as though the wagons
        /// were passing through the bridge. They were passing under it.
        ///
        /// A ford is not a hole in the river, it is the shallow place: a bar of gravel
        /// level with the banks, which is why anything can cross there at all. Levelling
        /// it here fixes the picture for the wagons, the horses, the escort and the
        /// bridge at once, because all five read the same number.
        /// </summary>
        static void LevelTheCrossings(TileGrid grid)
        {
            for (int i = 0; i < grid.TileCount; i++)
            {
                if (grid[i] != TerrainType.Ford) continue;

                grid.ToCoords(i, out int x, out int y);

                float sum = 0f;
                int banks = 0;

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (!grid.InBounds(x + dx, y + dy)) continue;

                        int neighbour = grid.ToIndex(x + dx, y + dy);
                        var terrain = grid[neighbour];

                        // Dry ground only. Averaging the river in would put the crossing
                        // back in the water, a fraction higher than before.
                        if (terrain == TerrainType.Water || terrain == TerrainType.Ford) continue;

                        sum += grid.Elevation(neighbour);
                        banks++;
                    }
                }

                if (banks > 0) grid.SetElevation(i, sum / banks);
            }
        }

        /// <summary>
        /// Walks from the north edge to the south edge, drifting toward a target
        /// column with a little wander so the result reads as a river rather than a
        /// ruled line.
        /// </summary>
        static List<int> MeanderSouth(TileGrid grid, int startX, int endX, DeterministicRandom rng)
        {
            var path = new List<int>(grid.Height * 2);
            int x = Math.Max(0, Math.Min(grid.Width - 1, startX));

            for (int y = 0; y < grid.Height; y++)
            {
                path.Add(grid.ToIndex(x, y));

                float progress = grid.Height <= 1 ? 1f : (float)y / (grid.Height - 1);
                int desired = (int)Math.Round(startX + (endX - startX) * progress);

                int drift = 0;
                if (x < desired) drift = 1;
                else if (x > desired) drift = -1;

                // Occasional wander against the drift keeps banks irregular.
                if (rng.Chance(0.28f)) drift = rng.Range(-1, 2);

                int nx = x + drift;
                if (nx < 1) nx = 1;
                if (nx > grid.Width - 2) nx = grid.Width - 2;

                // Widen the bend so diagonal steps cannot slip through the river.
                if (nx != x) path.Add(grid.ToIndex(nx, y));
                x = nx;
            }

            return path;
        }

        /// <summary>
        /// Opens evenly spaced fords. Without them the river is a wall and the level
        /// is unplayable; with too many it stops being a decision at all.
        /// </summary>
        static void PlaceFords(TileGrid grid, List<int> river, int fords)
        {
            if (river.Count == 0) return;

            for (int i = 0; i < fords; i++)
            {
                // Spaced through the interior, never at the very edges where the
                // caravan cannot reach them.
                float t = (i + 1f) / (fords + 1f);
                int at = (int)(t * (river.Count - 1));

                grid[river[at]] = TerrainType.Ford;

                // The neighbours above and below keep the crossing walkable even where
                // the river widened at a bend.
                grid.ToCoords(river[at], out int fx, out int fy);
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = fx + dx;
                    if (grid.InBounds(nx, fy) && grid[nx, fy] == TerrainType.Water)
                        grid[nx, fy] = TerrainType.Ford;
                }
            }
        }

        // --- Step 3: start and goal -------------------------------------------------

        static bool TryPlaceEndpoints(TileGrid grid, LevelRecipe recipe, DeterministicRandom rng,
                                      out int startX, out int startY, out int goalX, out int goalY)
        {
            startX = startY = goalX = goalY = 0;

            var left = CollectBand(grid, 0, Math.Min(EdgeBand, grid.Width) - 1);
            var right = CollectBand(grid, Math.Max(0, grid.Width - EdgeBand), grid.Width - 1);

            // A band can be solid water. Clear one tile rather than reject the seed:
            // rejecting here would bias generation towards maps with dry edges.
            if (left.Count == 0) left.Add(ForceOpen(grid, 0, grid.Height / 2));
            if (right.Count == 0) right.Add(ForceOpen(grid, grid.Width - 1, grid.Height / 2));

            rng.Shuffle(left);
            rng.Shuffle(right);

            var pathfinder = new GridPathfinder(grid);
            var path = new List<int>();

            int bestStart = -1, bestGoal = -1, bestTiles = -1;

            int attempts = Math.Min(PairAttempts, left.Count * right.Count);
            for (int i = 0; i < attempts; i++)
            {
                int s = left[i % left.Count];
                int g = right[(i * 7 + i / left.Count) % right.Count];

                grid.ToCoords(s, out int sx, out int sy);
                grid.ToCoords(g, out int gx, out int gy);

                if (!pathfinder.TryFindPath(sx, sy, gx, gy, path, out _)) continue;

                if (path.Count > bestTiles)
                {
                    bestTiles = path.Count;
                    bestStart = s;
                    bestGoal = g;
                }

                if (path.Count >= recipe.MinRouteTiles) break;
            }

            if (bestStart < 0) return false;

            grid.ToCoords(bestStart, out startX, out startY);
            grid.ToCoords(bestGoal, out goalX, out goalY);
            return true;
        }

        static List<int> CollectBand(TileGrid grid, int xFrom, int xTo)
        {
            var result = new List<int>();
            for (int x = xFrom; x <= xTo; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.IsPassable(x, y)) result.Add(grid.ToIndex(x, y));
            return result;
        }

        static int ForceOpen(TileGrid grid, int x, int y)
        {
            grid[x, y] = TerrainType.Plains;
            return grid.ToIndex(x, y);
        }
    }
}
