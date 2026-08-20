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

                // Encounters are placed only once the corridors are known, because the
                // whole distribution is derived from their relative travel times.
                var encounters = EncounterPlacer.Place(grid, corridors, recipe, rng);

                var map = new LevelMap(grid, seed, sx, sy, gx, gy,
                                       corridors[0].TravelCost, corridors, valid, attempt + 1, encounters);

                if (valid) return map;

                // Keep the least-bad candidate: the one whose routes differ most.
                float spread = SpreadOf(corridors);
                if (spread > bestSpread)
                {
                    bestSpread = spread;
                    best = map;
                }
            }

            return best ?? Fallback(recipe, seed, attempts);
        }

        static float SpreadOf(IReadOnlyList<Corridor> corridors)
        {
            float fastest = float.MaxValue, slowest = 0f;
            foreach (var c in corridors)
            {
                if (c.TravelCost < fastest) fastest = c.TravelCost;
                if (c.TravelCost > slowest) slowest = c.TravelCost;
            }
            return fastest > 0f ? (slowest - fastest) / fastest : 0f;
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
