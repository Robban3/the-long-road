using System;
using System.Collections.Generic;
using Arna.Sim;

namespace Arna.Gen
{
    /// <summary>
    /// Distributes enemies, traps and silver across a level's corridors
    /// (docs/content-pipeline.md §3, steps 5–6b).
    ///
    /// The governing rule is that threat is allocated in inverse proportion to travel
    /// time: the quick way through carries the most enemies, the long way round the
    /// fewest. That single formula is what turns the route drawing from a doodle into
    /// a decision — without it the fast route would be strictly better and nobody
    /// would ever weigh anything.
    /// </summary>
    public static class EncounterPlacer
    {
        /// <summary>Tiles at either end left clear so the player is not ambushed before the caravan moves.</summary>
        const int SafeEndTiles = 6;

        /// <summary>Minimum tiles between two enemy groups, so encounters arrive one at a time.</summary>
        const int GroupSpacing = 5;

        /// <summary>Per-tile trap chance at density 1.0 before terrain multipliers.</summary>
        const float BaseTrapChance = 0.035f;

        public static EncounterLayout Place(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                            LevelRecipe recipe, DeterministicRandom rng)
        {
            var layout = new EncounterLayout();
            if (corridors == null || corridors.Count == 0) return layout;

            // Tiles already carrying something, shared across corridors: where two
            // routes overlap the danger is shared, not doubled.
            var occupied = new HashSet<int>();

            float inverseSum = 0f;
            foreach (var c in corridors)
                if (c.TravelCost > 0f) inverseSum += 1f / c.TravelCost;
            if (inverseSum <= 0f) return layout;

            foreach (var corridor in corridors)
            {
                if (corridor.TravelCost <= 0f) continue;

                float share = (1f / corridor.TravelCost) / inverseSum;
                int points = (int)Math.Round(recipe.EnemyBudget * share);

                points -= PlaceTraps(grid, corridor, recipe, rng, layout, occupied, points);
                PlaceEnemies(grid, corridor, recipe, rng, layout, occupied, points);
            }

            TallySilver(layout, recipe);
            TopUpSilver(grid, corridors, recipe, rng, layout, occupied);
            return layout;
        }

        /// <summary>
        /// Scatters traps along a corridor and returns the threat points they consumed.
        /// Marsh draws far more than open ground, so a route through the fen is
        /// automatically trap-heavy and — because these points are deducted — thinner
        /// on enemies. That is the trade the design asks for, expressed as arithmetic
        /// rather than as a special case.
        /// </summary>
        static int PlaceTraps(TileGrid grid, Corridor corridor, LevelRecipe recipe,
                              DeterministicRandom rng, EncounterLayout layout,
                              HashSet<int> occupied, int availablePoints)
        {
            int spent = 0;
            var tiles = corridor.Tiles;

            for (int i = SafeEndTiles; i < tiles.Count - SafeEndTiles; i++)
            {
                int tile = tiles[i];
                if (occupied.Contains(tile)) continue;

                float chance = BaseTrapChance * recipe.TrapDensity * TerrainTable.TrapDensity(grid[tile]);
                if (!rng.Chance(chance)) continue;

                var kind = rng.Chance(0.6f) ? TrapKind.Pit : TrapKind.Log;
                int cost = TrapTable.Points(kind);
                if (spent + cost > availablePoints) break;

                layout.Traps.Add(new TrapPlacement { Tile = tile, Kind = kind, Corridor = corridor.Kind });
                layout.PointsByCorridor[(int)corridor.Kind] += cost;
                occupied.Add(tile);
                spent += cost;
            }

            return spent;
        }

        static void PlaceEnemies(TileGrid grid, Corridor corridor, LevelRecipe recipe,
                                 DeterministicRandom rng, EncounterLayout layout,
                                 HashSet<int> occupied, int points)
        {
            if (points <= 0) return;

            var candidates = BuildWeightedCandidates(grid, corridor, occupied, rng);
            int budget = points;
            int index = 0;

            while (budget > 0 && index < candidates.Count)
            {
                int tile = candidates[index++];
                if (occupied.Contains(tile)) continue;
                if (TooCloseToExistingGroup(corridor, tile, occupied)) continue;

                var kind = PickAffordable(recipe.EnemyPool, rng, budget);
                if (kind == null) break;

                layout.Enemies.Add(new EnemySpawn { Tile = tile, Kind = kind.Value, Corridor = corridor.Kind });
                layout.PointsByCorridor[(int)corridor.Kind] += EnemyTable.Points(kind.Value);
                occupied.Add(tile);
                budget -= EnemyTable.Points(kind.Value);
            }
        }

        /// <summary>
        /// Orders the corridor's tiles by ambush suitability, with a random tiebreak.
        /// Enemies gather where the ground favours them — the forest, the ford, the
        /// bend in the road — rather than spreading evenly along the route.
        /// </summary>
        static List<int> BuildWeightedCandidates(TileGrid grid, Corridor corridor,
                                                 HashSet<int> occupied, DeterministicRandom rng)
        {
            var tiles = corridor.Tiles;
            var scored = new List<KeyValuePair<float, int>>();

            for (int i = SafeEndTiles; i < tiles.Count - SafeEndTiles; i++)
            {
                int tile = tiles[i];
                if (occupied.Contains(tile)) continue;

                float weight = TerrainTable.AmbushWeight(grid[tile]) * rng.Range(0.6f, 1.4f);
                scored.Add(new KeyValuePair<float, int>(weight, tile));
            }

            scored.Sort((a, b) => b.Key.CompareTo(a.Key));

            var result = new List<int>(scored.Count);
            foreach (var entry in scored) result.Add(entry.Value);
            return result;
        }

        static bool TooCloseToExistingGroup(Corridor corridor, int tile, HashSet<int> occupied)
        {
            int position = corridor.Tiles.IndexOf(tile);
            if (position < 0) return false;

            int from = Math.Max(0, position - GroupSpacing);
            int to = Math.Min(corridor.Tiles.Count - 1, position + GroupSpacing);

            for (int i = from; i <= to; i++)
                if (i != position && occupied.Contains(corridor.Tiles[i])) return true;

            return false;
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

        static void TallySilver(EncounterLayout layout, LevelRecipe recipe)
        {
            float multiplier = recipe.SilverMultiplier <= 0f ? 1f : recipe.SilverMultiplier;

            foreach (var spawn in layout.Enemies)
                layout.SilverByCorridor[(int)spawn.Corridor] +=
                    (int)(EnemyTable.GroupSilver(spawn.Kind) * multiplier);

            foreach (var trap in layout.Traps)
                layout.SilverByCorridor[(int)trap.Corridor] +=
                    (int)(TrapTable.DisarmSilver(trap.Kind) * multiplier);
        }

        /// <summary>
        /// Brings any corridor below the silver floor up to it with a standalone cache.
        ///
        /// A cautious route that yields so little silver the player cannot upgrade
        /// through the level's final fight is a broken level, not a hard choice. The
        /// shortfall is covered with an abandoned camp rather than more enemies —
        /// adding enemies would erase the reason to take the safe route at all.
        /// </summary>
        static void TopUpSilver(TileGrid grid, IReadOnlyList<Corridor> corridors, LevelRecipe recipe,
                                DeterministicRandom rng, EncounterLayout layout, HashSet<int> occupied)
        {
            layout.SilverValidated = true;

            foreach (var corridor in corridors)
            {
                int index = (int)corridor.Kind;
                int shortfall = recipe.MinSilverPerCorridor - layout.SilverByCorridor[index];
                if (shortfall <= 0) continue;

                int tile = FindFreeTile(corridor, occupied);
                if (tile < 0) { layout.SilverValidated = false; continue; }

                layout.SilverCaches.Add(new SilverCache
                {
                    Tile = tile,
                    Amount = shortfall,
                    Corridor = corridor.Kind
                });
                layout.SilverByCorridor[index] += shortfall;
                occupied.Add(tile);
            }
        }

        static int FindFreeTile(Corridor corridor, HashSet<int> occupied)
        {
            var tiles = corridor.Tiles;
            int middle = tiles.Count / 2;

            for (int offset = 0; offset < tiles.Count; offset++)
            {
                foreach (int direction in new[] { 1, -1 })
                {
                    int i = middle + offset * direction;
                    if (i < SafeEndTiles || i >= tiles.Count - SafeEndTiles) continue;
                    if (!occupied.Contains(tiles[i])) return tiles[i];
                }
            }
            return -1;
        }
    }
}
