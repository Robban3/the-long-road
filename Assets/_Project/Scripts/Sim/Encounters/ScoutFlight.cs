using System;
using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>
    /// Where the eagle went and what it found (docs/GDD.md §3.6).
    ///
    /// Deterministic from the level seed, and that is not a detail. A flight rolled
    /// fresh on every press would let a player restart the level until the bird
    /// happened to sweep the ground they cared about, and an ability you can re-roll
    /// for free is not a decision, it is a slot machine. Same level, same flight — the
    /// randomness lives in the map, not in the retry.
    /// </summary>
    public sealed class ScoutFlight
    {
        /// <summary>World positions along the flight, in metres.</summary>
        public readonly List<Vec2> Path = new List<Vec2>();

        /// <summary>Tiles the planning overlay is lifted from.</summary>
        public readonly HashSet<int> RevealedTiles = new HashSet<int>();

        /// <summary>Indices into <see cref="EncounterLayout.Enemies"/> the bird passed over.</summary>
        public readonly List<int> RevealedEnemies = new List<int>();

        public float Seconds;

        public int Coverage => RevealedTiles.Count;
    }

    /// <summary>
    /// The scouting ability: an eagle flown over the planning map before the route is
    /// drawn (docs/GDD.md §3.6).
    ///
    /// The map is under a grey overlay to begin with — see-through, because the terrain
    /// is what the player plans against and hiding it would remove the decision rather
    /// than the certainty. What the eagle flies over comes back to full colour, and any
    /// group it passed over is marked.
    ///
    /// It must be spent before the pen comes out. Bought for the run it would be a
    /// reveal buff; bought for the planning it is information that becomes a decision —
    /// which is the whole of why it exists (README, decision 1: information is a
    /// resource).
    ///
    /// The flight is not aimed. The player buys a look at a quarter of the country and
    /// does not choose which quarter, so the ability is a wager on the map rather than
    /// a way to confirm what they already suspect.
    /// </summary>
    public static class ScoutingAbility
    {
        /// <summary>Metres per second. Fast enough that seven seconds crosses the map.</summary>
        public const float Speed = 40f;

        /// <summary>Seconds aloft. Bought per level, so this is the whole of it.</summary>
        public const float Seconds = 7f;

        /// <summary>
        /// Metres either side of the flight the bird can see down into. Wide enough to be
        /// worth buying, narrow enough that most of the map stays under the overlay:
        /// measured over chapter 1 it uncovers 23–25 % of the ground and finds three to
        /// five of the twelve groups.
        /// </summary>
        public const float Sight = 32f;

        /// <summary>Samples per segment of the flight curve.</summary>
        const int StepsPerSegment = 64;

        public static ScoutFlight Fly(LevelMap map, float seconds = Seconds,
                                      float sight = Sight, int flight = 0)
        {
            var result = new ScoutFlight { Seconds = seconds };
            if (map == null) return result;

            var grid = map.Grid;
            var rng = new DeterministicRandom(map.Seed ^ (0x3A91 + flight * 7919));

            BuildPath(result.Path, grid, rng, seconds);
            MarkSeen(result, grid, sight);

            // A group is found if the bird passed over where it stands. Its territory
            // neither helps it hide nor helps it be seen: the eagle looks at the ground.
            for (int i = 0; i < map.Encounters.Enemies.Count; i++)
                if (result.RevealedTiles.Contains(map.Encounters.Enemies[i].Tile))
                    result.RevealedEnemies.Add(i);

            return result;
        }

        /// <summary>
        /// A curve entering at one edge and leaving by another, bent by two inland
        /// points. Two rather than one: a single bend always bulges the same way and
        /// reads as a machine sweeping the map, where two give the wandering line a bird
        /// actually flies.
        /// </summary>
        static void BuildPath(List<Vec2> path, TileGrid grid, DeterministicRandom rng, float seconds)
        {
            float extent = grid.Width * TileGrid.TileSize;

            int entryEdge = rng.Range(0, 4);
            int exitEdge = (entryEdge + 1 + rng.Range(0, 3)) % 4;

            var points = new List<Vec2>
            {
                EdgePoint(rng, entryEdge, extent),
                new Vec2(rng.Range(0.2f, 0.8f) * extent, rng.Range(0.2f, 0.8f) * extent),
                new Vec2(rng.Range(0.2f, 0.8f) * extent, rng.Range(0.2f, 0.8f) * extent),
                EdgePoint(rng, exitEdge, extent)
            };

            float budget = Speed * seconds;
            float travelled = 0f;

            var previous = points[0];
            path.Add(previous);

            for (int segment = 0; segment < points.Count - 1; segment++)
            {
                var p0 = points[Math.Max(segment - 1, 0)];
                var p1 = points[segment];
                var p2 = points[segment + 1];
                var p3 = points[Math.Min(segment + 2, points.Count - 1)];

                for (int i = 1; i <= StepsPerSegment; i++)
                {
                    var point = CatmullRom(p0, p1, p2, p3, i / (float)StepsPerSegment);
                    float step = Vec2.Distance(previous, point);

                    if (travelled + step > budget) return;

                    travelled += step;
                    path.Add(point);
                    previous = point;
                }
            }
        }

        static Vec2 EdgePoint(DeterministicRandom rng, int edge, float extent)
        {
            float along = rng.Range(0.15f, 0.85f) * extent;
            switch (edge)
            {
                case 0: return new Vec2(0f, along);
                case 1: return new Vec2(extent, along);
                case 2: return new Vec2(along, 0f);
                default: return new Vec2(along, extent);
            }
        }

        static Vec2 CatmullRom(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float x = 0.5f * (2f * p1.X
                              + (-p0.X + p2.X) * t
                              + (2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * t2
                              + (-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * t3);

            float y = 0.5f * (2f * p1.Y
                              + (-p0.Y + p2.Y) * t
                              + (2f * p0.Y - 5f * p1.Y + 4f * p2.Y - p3.Y) * t2
                              + (-p0.Y + 3f * p1.Y - 3f * p2.Y + p3.Y) * t3);

            return new Vec2(x, y);
        }

        static void MarkSeen(ScoutFlight flight, TileGrid grid, float sight)
        {
            float radiusTiles = sight / TileGrid.TileSize;
            int span = (int)Math.Ceiling(radiusTiles);
            float limit = radiusTiles * radiusTiles;

            foreach (var position in flight.Path)
            {
                float fx = position.X / TileGrid.TileSize;
                float fy = position.Y / TileGrid.TileSize;
                int cx = (int)fx;
                int cy = (int)fy;

                for (int y = cy - span; y <= cy + span; y++)
                {
                    for (int x = cx - span; x <= cx + span; x++)
                    {
                        if (!grid.InBounds(x, y)) continue;

                        float dx = x - fx;
                        float dy = y - fy;
                        if (dx * dx + dy * dy > limit) continue;

                        flight.RevealedTiles.Add(grid.ToIndex(x, y));
                    }
                }
            }
        }
    }
}
