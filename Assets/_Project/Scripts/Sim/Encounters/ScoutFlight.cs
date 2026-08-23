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
        /// <summary>Metres per second. Fast enough that ten seconds quarters the map.</summary>
        public const float Speed = 40f;

        /// <summary>Seconds aloft. Bought per level, so this is the whole of it.</summary>
        public const float Seconds = 10f;

        /// <summary>
        /// Metres either side of the flight the bird can see down into.
        ///
        /// Narrow, deliberately. A wide trail at the same coverage is one broad stripe
        /// through the middle of the map; a narrow one wanders further for the same
        /// ground, so what it uncovers is spread about. Measured over chapter 1 it lifts
        /// 17–25 % of the overlay and finds two to five of the twelve groups.
        /// </summary>
        public const float Sight = 20f;

        /// <summary>
        /// Turns the flight takes. Two gave one sweep across the map — the same picture
        /// every level with the angle changed. Six give a bird that quarters ground.
        /// </summary>
        const int Waypoints = 6;

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
        /// A wandering curve: in from one edge, then six turns wherever the bird likes.
        /// </summary>
        static void BuildPath(List<Vec2> path, TileGrid grid, DeterministicRandom rng, float seconds)
        {
            float extent = grid.Width * TileGrid.TileSize;

            // Each turn is taken from where the bird already is, at a random heading a
            // third of the map away. Six independent points anywhere on the map looked
            // like wandering and was not: the curve through them doubled back over one
            // quarter and left the other three untouched. A step from the last point is
            // how something quartering ground actually moves — it covers rather than
            // revisits.
            var points = new List<Vec2> { EdgePoint(rng, rng.Range(0, 4), extent) };

            for (int w = 0; w < Waypoints; w++)
            {
                var previous = points[points.Count - 1];
                bool placed = false;

                for (int attempt = 0; attempt < 8 && !placed; attempt++)
                {
                    float angle = rng.Range(0f, (float)(2.0 * Math.PI));
                    float reach = rng.Range(0.28f, 0.52f) * extent;

                    var candidate = new Vec2(previous.X + (float)Math.Cos(angle) * reach,
                                             previous.Y + (float)Math.Sin(angle) * reach);

                    if (candidate.X >= 0.05f * extent && candidate.X <= 0.95f * extent &&
                        candidate.Y >= 0.05f * extent && candidate.Y <= 0.95f * extent)
                    {
                        points.Add(candidate);
                        placed = true;
                    }
                }

                if (!placed)
                    points.Add(new Vec2(rng.Range(0.2f, 0.8f) * extent,
                                        rng.Range(0.2f, 0.8f) * extent));
            }

            float budget = Speed * seconds;
            float travelled = 0f;

            var last = points[0];
            path.Add(last);

            for (int segment = 0; segment < points.Count - 1; segment++)
            {
                var p0 = points[Math.Max(segment - 1, 0)];
                var p1 = points[segment];
                var p2 = points[segment + 1];
                var p3 = points[Math.Min(segment + 2, points.Count - 1)];

                for (int i = 1; i <= StepsPerSegment; i++)
                {
                    var point = CatmullRom(p0, p1, p2, p3, i / (float)StepsPerSegment);

                    // Keep the bird over the map. A Catmull-Rom through points near the
                    // edge overshoots outside it, and a trail that leaves the map is
                    // ground the player paid for and cannot use.
                    point = new Vec2(Math.Min(Math.Max(point.X, 0f), extent),
                                     Math.Min(Math.Max(point.Y, 0f), extent));

                    float step = Vec2.Distance(last, point);
                    if (travelled + step > budget) return;

                    travelled += step;
                    path.Add(point);
                    last = point;
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
