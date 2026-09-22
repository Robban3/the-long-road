using System;
using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// Where the castle at the end of a chapter stands, and which way its gate faces.
    ///
    /// <b>One answer, used by the two places that must agree.</b> This lived in
    /// TerrainDecorator, which is the thing that builds the castle — and so the only
    /// thing that knew where it was. The generator, which decides where the chapter's
    /// champion waits, had no way to ask: it posted him five tiles back along the fastest
    /// road, and the castle went up fourteen tiles out to the side. He is the man who
    /// holds it and he stood with his back to it, in the middle of a field.
    ///
    /// The same shape as <see cref="Crossings"/>, and for the same reason: two callers
    /// that must show the same country, neither of which can see the other, so the answer
    /// goes in Sim where both can reach it. Engine-free for that reason.
    ///
    /// The decorator keeps one thing of its own — it will not put the castle on ground
    /// something else has already claimed — because what is occupied is a fact about the
    /// scene being built and not about the map.
    /// </summary>
    public static class Strongholds
    {
        /// <summary>
        /// How far to the side of the goal the castle stands, in tiles.
        ///
        /// Fourteen. The castle is forty-odd metres across — near six tiles to its wall
        /// from its middle — so this stands it about eight tiles clear of the goal: near
        /// enough to loom over the arrival, far enough that the caravan is not parked
        /// against the stonework.
        /// </summary>
        public const int Standoff = 14;

        /// <summary>How far back down the road the gate looks to work out which way it came.</summary>
        public const int Lookback = 8;

        /// <summary>
        /// The tile the castle stands on, or -1 where neither side of the goal will take
        /// it.
        ///
        /// Out to the side, square to the way the road comes in, so the castle is beside
        /// the arrival rather than on it: the goal is the place the caravan is going and
        /// the castle belongs to the enemy.
        /// </summary>
        public static int Site(TileGrid grid, int goalTile, IReadOnlyCollection<int> travelled,
                               HashSet<int> taken = null, int behind = -1)
        {
            if (grid == null || goalTile < 0 || goalTile >= grid.TileCount) return -1;

            grid.ToCoords(goalTile, out int gx, out int gy);

            // The way in, as the gate works it out, turned a quarter.
            float yaw = GateYaw(grid, goalTile, travelled);
            float rad = yaw * Deg2Rad;

            int dx = Round(Cos(rad));
            int dy = Round(-Sin(rad));

            // <b>Which of the two sides, decided by whoever is standing there.</b>
            //
            // The castle went to whichever side came out of the loop first, so the man who
            // holds it stood with his back to it about half the time — reported from a
            // playtest as a rider who ought to be coming from the castle and was not.
            //
            // Moving him to the castle was tried three times and measured three times, and
            // every version of it cost a chapter: four tiles outside his own gate is forty
            // metres from the goal against a detect radius of twenty-six, so he never woke
            // and a live champion holds the goal — the run could not end at all. Put at the
            // right distance but off the road he was a hundred and twenty-seven metres from
            // the fast corridor. Put on the road nearest his castle he was twenty-seven
            // from the odd one. Being met is what he is for, and a single tile cannot be
            // met from three roads that diverge.
            //
            // So it is the castle that moves. He is mechanism and has to stand where the
            // roads meet; the castle is scenery and can stand wherever it looks right from.
            // Given his tile, the side is the side he is on.
            var order = new[] { 1, -1 };

            if (behind >= 0 && behind < grid.TileCount)
            {
                grid.ToCoords(behind, out int bx, out int by);
                if ((bx - gx) * dx + (by - gy) * dy < 0) order = new[] { -1, 1 };
            }

            // <b>Off the roads.</b> The two sides were taken as they came, and on 3-10 the
            // side that came first had a road running through it: the castle went up across
            // the road, a trap on that road stood inside its walls, and the decorator's
            // sweep round the trap took the whole castle down again - the chapter ended at
            // an empty goal. A side with a road under the castle is passed over for one
            // without, and where neither side is clear the ring round the goal is searched.
            int fallback = -1;

            foreach (int sign in order)
            {
                int x = gx + dx * Standoff * sign;
                int y = gy + dy * Standoff * sign;

                if (!grid.InBounds(x, y)) continue;

                int tile = grid.ToIndex(x, y);
                if (taken != null && taken.Contains(tile)) continue;

                if (OffTheRoads(grid, x, y, travelled)) return tile;
                if (fallback < 0) fallback = tile;
            }

            int ring = Ring(grid, gx, gy, travelled, taken);
            return ring >= 0 ? ring : fallback;
        }

        /// <summary>
        /// The nearest tile to the usual standoff from the goal, in any direction, with no
        /// road under the castle. -1 if there is none. Searched in a fixed order, so the
        /// planning map and the run find the same one.
        /// </summary>
        static int Ring(TileGrid grid, int gx, int gy, IReadOnlyCollection<int> travelled, HashSet<int> taken)
        {
            int best = -1;
            int bestOff = int.MaxValue;
            int reach = Standoff + Bailey;

            for (int y = gy - reach; y <= gy + reach; y++)
            {
                for (int x = gx - reach; x <= gx + reach; x++)
                {
                    if (!grid.InBounds(x, y)) continue;

                    int ddx = x - gx, ddy = y - gy;
                    int distance = (int)Math.Round(Math.Sqrt(ddx * ddx + ddy * ddy));
                    int off = Math.Abs(distance - Standoff);
                    if (off > Bailey / 2 || off >= bestOff) continue;

                    int tile = grid.ToIndex(x, y);
                    if (taken != null && taken.Contains(tile)) continue;
                    if (!grid.IsPassable(x, y) || grid[tile] == TerrainType.Ford) continue;
                    if (!OffTheRoads(grid, x, y, travelled)) continue;

                    best = tile;
                    bestOff = off;
                }
            }

            return best;
        }

        /// <summary>Whether no road passes under a castle standing on this tile.</summary>
        static bool OffTheRoads(TileGrid grid, int cx, int cy, IReadOnlyCollection<int> travelled)
        {
            if (travelled == null || travelled.Count == 0) return true;

            foreach (int tile in travelled)
            {
                grid.ToCoords(tile, out int x, out int y);
                int ddx = x - cx, ddy = y - cy;
                if (ddx * ddx + ddy * ddy <= Bailey * Bailey) return false;
            }

            return true;
        }

        /// <summary>
        /// How much ground the bailey is levelled over, as a radius in tiles.
        ///
        /// Seven. The castle is forty-odd metres across, near six tiles from its middle to
        /// its wall, and one more takes in the towers that stand out of the line.
        /// </summary>
        public const int Bailey = 7;

        /// <summary>
        /// The castle's tile on a finished level, asked exactly as the decorator asks it:
        /// the roads are every corridor and the side is the champion's.
        ///
        /// <b>One question, asked one way.</b> Flatten asked without the champion, and the
        /// decorator with him, so where he stood on the other side of the goal the ground
        /// was levelled on one side and the castle built on the other - 2-10 levelled a
        /// square of empty field and stood its castle on whatever the slope was.
        /// </summary>
        public static int SiteOf(LevelMap map)
        {
            if (map?.Grid == null) return -1;

            var travelled = new HashSet<int>();
            if (map.Corridors != null)
                foreach (var corridor in map.Corridors)
                    foreach (int tile in corridor.Tiles) travelled.Add(tile);

            return Site(map.Grid, map.GoalIndex, travelled, null, Champions.Post(map));
        }

        /// <summary>
        /// Levels the ground a castle stands on, so its courtyard can be flat and above
        /// grade at once.
        ///
        /// <b>Why it has to be the ground and not the floor.</b> A castle is assembled in
        /// its own flat plane and set down on country that is not — so the yard came out
        /// as a level slab at the building's height, and on a slope a quarter of it was
        /// underground: sixteen pieces buried on 1-10, eight on 2-10, eleven on 3-10.
        /// Laying each flag on the ground under it fixed that and bought a new fault,
        /// because flat tiles at different heights do not meet at their edges: the terrain
        /// shows through the seams. There is no arrangement of a flat floor on sloping
        /// ground that is both level and closed. The ground has to give.
        ///
        /// <b>And it can, because nothing decides anything by it here.</b> Elevation is
        /// read by two things outside the generator — Settlements picks a village site by
        /// it and Towns sets its gate row by it — and neither ever meets a castle: villages
        /// stand on the third and sixth levels of a chapter, the town on 1-8, and the keep
        /// only on the tenth. Run after Generate, so every corridor, cost and encounter is
        /// already fixed and cannot see this.
        ///
        /// Called from LevelMaps.For, which is the one door both the planning map and the
        /// run come through — a level flattened for one and not the other would be two
        /// different countries.
        /// </summary>
        public static void Flatten(LevelMap map, int level)
        {
            if (map?.Grid == null || level < Campaign.LevelsPerChapter) return;

            int site = SiteOf(map);
            if (site < 0) return;

            map.Grid.ToCoords(site, out int cx, out int cy);
            float floor = map.Grid.Elevation(site);

            for (int dy = -Bailey; dy <= Bailey; dy++)
            {
                for (int dx = -Bailey; dx <= Bailey; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!map.Grid.InBounds(x, y)) continue;

                    // <b>Not the water, and not the ground that holds it in.</b>
                    //
                    // A river is cut below the banks beside it and the whole look of one
                    // depends on that staying true — TerrainGeneratorTests checks every
                    // bankside tile on every level. Levelling a fifteen-tile square without
                    // asking what was in it put ten of 1-10's bank tiles at or above the
                    // dry ground next to them, which is a stream running along the top of
                    // its own valley.
                    //
                    // So water keeps its bed and the tiles touching it keep their fall. The
                    // yard is nowhere near either: the castle stands on ground the placer
                    // already found dry.
                    if (Wet(map.Grid, x, y)) continue;

                    map.Grid.SetElevation(map.Grid.ToIndex(x, y), floor);
                }
            }
        }

        /// <summary>Whether a tile is water, or stands on the bank of some.</summary>
        static bool Wet(TileGrid grid, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!grid.InBounds(nx, ny)) continue;

                    var terrain = grid[nx, ny];
                    if (terrain == TerrainType.Water || terrain == TerrainType.Ford) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Which quarter turn puts the gate towards the road the caravan arrives on.
        ///
        /// Moved here whole from TerrainDecorator. A yaw of nought leaves the gate
        /// pointing down -Z, ninety turns it to -X, a hundred and eighty to +Z and two
        /// hundred and seventy to +X. Written the other way round the castle presents its
        /// back wall to the road, which is the one thing this exists to prevent.
        /// </summary>
        public static float GateYaw(TileGrid grid, int goalTile, IReadOnlyCollection<int> travelled)
        {
            float toX = -1f, toZ = 0f;

            if (travelled != null)
            {
                grid.ToCoords(goalTile, out int gx, out int gy);
                float sumX = 0f, sumY = 0f;
                int seen = 0;

                foreach (int tile in travelled)
                {
                    if (tile < 0 || tile >= grid.TileCount) continue;

                    grid.ToCoords(tile, out int x, out int y);
                    int dx = x - gx, dy = y - gy;

                    if (dx * dx + dy * dy > Lookback * Lookback) continue;

                    sumX += dx;
                    sumY += dy;
                    seen++;
                }

                if (seen > 0 && (sumX != 0f || sumY != 0f))
                {
                    toX = sumX;
                    toZ = sumY;
                }
            }

            if (Abs(toX) >= Abs(toZ)) return toX < 0f ? 90f : 270f;
            return toZ < 0f ? 0f : 180f;
        }

        // Engine-free, so the arithmetic is System.Math rather than UnityEngine.Mathf.
        const float Deg2Rad = 0.0174532924f;

        static float Abs(float v) => v < 0f ? -v : v;
        static float Cos(float v) => (float)System.Math.Cos(v);
        static float Sin(float v) => (float)System.Math.Sin(v);
        static int Round(float v) => (int)System.Math.Round(v, System.MidpointRounding.AwayFromZero);
    }
}
