using System;
using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>A camp on a piece of ground, and whether anyone is still using it.</summary>
    public struct Camp
    {
        public int Tile;

        /// <summary>
        /// False for a camp with nobody near it.
        ///
        /// Nothing in the world distinguishes one from the other, and nothing should. A
        /// signal you can tell is false is not a false positive, it is a second signal
        /// saying "ignore me".
        /// </summary>
        public bool Truthful;
    }

    /// <summary>
    /// A tent, a rack of spears, a banner in the ground — and no promise that anyone is
    /// still there.
    ///
    /// The third soft signal, and the one with the strongest claim: crows are birds that
    /// might be over anything and a bone pile is old, but a standing camp says *men, here,
    /// recently*. That is exactly why it has to be able to lie. A signal that is always
    /// right is not a signal, it is a map — and a camp the player can trust turns route
    /// drawing into route reading.
    ///
    /// **An abandoned camp is better fiction than a lying bird.** The crows lie one time
    /// in five and the story is that birds circle carrion, not soldiers. A camp needs no
    /// story: bands move, and the tents they leave behind are the most ordinary thing in
    /// a country full of bands. So it lies one time in three, which is the most any of
    /// the three signals does, and it is still worth reading — see <see cref="FalseShare"/>.
    /// </summary>
    public static class CampSignal
    {
        /// <summary>
        /// How near its group a real camp is pitched, in tiles.
        ///
        /// Closer than the crows' six, because a camp belongs to the men in a way a bird
        /// does not — it is their tent. Never on them: enemies are drawn only once
        /// revealed, and a tent on an unrevealed group hands over the position the whole
        /// detection system exists to hide.
        /// </summary>
        public const int MinTiles = 2;
        public const int HintTiles = 4;

        /// <summary>
        /// Chance a given group has a camp at all.
        ///
        /// A third, which is less than the crows' half, and the reason is that the two
        /// signals stack. Both marking the same group is not twice the information, it is
        /// one group with two signs on it — and a player who learns to look for the pair
        /// gets certainty back through the side door.
        /// </summary>
        public const float PerGroup = 0.34f;

        /// <summary>
        /// Share of camps with nobody near them.
        ///
        /// A third. The test of a signal is whether it updates what you believed: random
        /// ground has a group within four tiles about a fifth of the time, and a camp
        /// says two thirds. That is still a large update — worth walking around — while
        /// leaving one camp in three a feint, which is enough that the player cannot
        /// treat one as proof and stop scouting.
        /// </summary>
        public const float FalseShare = 1f / 3f;

        const int PlacementAttempts = 12;
        const int FalseAttempts = 24;

        /// <summary>Where the camps stand. Deterministic from the level seed.</summary>
        public static List<Camp> Place(LevelMap map)
        {
            var camps = new List<Camp>();
            if (map?.Encounters == null) return camps;

            var grid = map.Grid;
            var groups = map.Encounters.Enemies;
            var rng = new DeterministicRandom(map.Seed ^ 0x0CA3);
            var taken = new HashSet<int>();

            foreach (var spawn in groups)
            {
                if (!rng.Chance(PerGroup)) continue;

                grid.ToCoords(spawn.Tile, out int gx, out int gy);

                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    float angle = rng.Range(0f, (float)(2.0 * Math.PI));

                    // Cast, and not for tidiness: both constants are int, so without it
                    // the int overload is chosen and the reach snaps to whole tiles — a
                    // different distribution from the one these numbers describe.
                    float reach = rng.Range((float)MinTiles, (float)HintTiles);

                    int nx = (int)Math.Round(gx + Math.Cos(angle) * reach);
                    int ny = (int)Math.Round(gy + Math.Sin(angle) * reach);

                    if (!grid.InBounds(nx, ny)) continue;

                    int tile = grid.ToIndex(nx, ny);
                    if (!Free(grid, groups, taken, tile)) continue;

                    // Checked after rounding, not before: a reach drawn below the radius
                    // can snap past it, which is a camp quietly claiming more than it
                    // should.
                    if (Distance(nx, ny, gx, gy) > HintTiles) continue;

                    taken.Add(tile);
                    camps.Add(new Camp { Tile = tile, Truthful = true });
                    break;
                }
            }

            // Then the empty ones, placed where nothing is within the radius a camp
            // claims — a feint has to be genuinely false rather than accidentally right.
            int wanted = (int)Math.Round(camps.Count * FalseShare / (1f - FalseShare));

            for (int i = 0; i < wanted; i++)
            {
                for (int attempt = 0; attempt < FalseAttempts; attempt++)
                {
                    int tile = rng.Range(0, grid.TileCount);
                    if (!Free(grid, groups, taken, tile)) continue;

                    grid.ToCoords(tile, out int x, out int y);
                    if (NearestGroup(grid, groups, x, y) <= HintTiles) continue;

                    taken.Add(tile);
                    camps.Add(new Camp { Tile = tile, Truthful = false });
                    break;
                }
            }

            return camps;
        }

        /// <summary>The tiles alone, for the decorator, which does not care who is home.</summary>
        public static List<int> Tiles(LevelMap map)
        {
            var tiles = new List<int>();
            foreach (var camp in Place(map)) tiles.Add(camp.Tile);
            return tiles;
        }

        static bool Free(TileGrid grid, List<EnemySpawn> groups, HashSet<int> taken, int tile)
        {
            if (taken.Contains(tile)) return false;

            grid.ToCoords(tile, out int x, out int y);
            if (!grid.IsPassable(x, y)) return false;

            // Nowhere a tent could not stand, and nowhere it would be in the way: a ford
            // is the one piece of ground every route crosses.
            var terrain = grid[tile];
            if (terrain == TerrainType.Ford || terrain == TerrainType.Water) return false;

            // Distance from every group, not only the one this camp belongs to. Checking
            // its own would let a camp land on a different group's tile — which is a
            // marker, not a hint.
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
