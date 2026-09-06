using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// One stretch of the route, from the last point the player put down to the next.
    ///
    /// Legs are kept separately and not just summed because two of the three things
    /// docs/GDD.md §3.3 asks the preview to say are per-leg: which leg failed, and
    /// which leg went somewhere the player did not mean.
    /// </summary>
    public struct RouteLeg
    {
        public int FromTile;
        public int ToTile;
        public int TileCount;
        public float TravelCost;

        /// <summary>
        /// Where this leg's tiles sit in <see cref="RouteResult.Tiles"/>, as
        /// [First, Last]. §3.3 asks for the failed leg drawn red and the detour leg
        /// drawn differently, which needs the leg's own stretch of the line and not
        /// just its number.
        /// </summary>
        public int First;
        public int Last;

        /// <summary>Tiles walked, counting a diagonal as √2. Distance, not time.</summary>
        public float Walked;

        /// <summary>Tiles as the crow flies between the leg's ends.</summary>
        public float StraightLine;

        /// <summary>Walked over straight-line. 1.0 is a straight leg.</summary>
        public float Detour => StraightLine <= 0f ? 1f : Walked / StraightLine;

        /// <summary>
        /// The leg went appreciably further than the line the player drew.
        ///
        /// Drawing across a river without a ford does not stop anything — A* goes
        /// around — and the caravan takes a detour nobody asked for. §3.3: a detour
        /// must not arrive as a surprise, so the leg is drawn differently and says
        /// *this did not become what you thought*.
        /// </summary>
        public bool IsDetour => Detour > RouteResult.DetourThreshold;

        public bool Failed;

        /// <summary>The first ford this leg crosses, or -1. Where the decision is.</summary>
        public int FordTile;
    }

    /// <summary>
    /// The result of stitching the player's waypoints into one caravan route.
    /// </summary>
    public sealed class RouteResult
    {
        /// <summary>How much longer than the crow's line a leg may run before it is flagged.</summary>
        public const float DetourThreshold = 1.4f;

        /// <summary>Each stretch between the points the player put down, in order.</summary>
        public readonly List<RouteLeg> Legs = new List<RouteLeg>();

        /// <summary>Fords the route crosses, in the order it reaches them.</summary>
        public readonly List<int> Crossings = new List<int>();

        /// <summary>
        /// Mean ambush weight along the route — the risk indicator §3.3 asks for.
        ///
        /// Read off the terrain and nothing else. It must not consult the encounter
        /// layout: what is actually out there is bought with the eagle or paid for in
        /// blood, and a risk number that knew would hand it over for free (§3.4). This
        /// says *this is ambush country*, never *there are four of them behind that
        /// ridge* — and it is the same measure <see cref="Corridor.AmbushExposure"/>
        /// uses, so the generator's own cautious corridor is a fair yardstick for it.
        /// </summary>
        public float AmbushExposure;
        /// <summary>Tile indices from start to goal, contiguous, no duplicates at the seams.</summary>
        public readonly List<int> Tiles = new List<int>();

        /// <summary>Sum of terrain travel costs. Divide by the caravan's tiles/second for time.</summary>
        public float TravelCost;

        /// <summary>Tiles of each terrain type along the route, indexed by (int)TerrainType.</summary>
        public readonly int[] TilesByTerrain = new int[8];

        /// <summary>False when a leg has no route; the preview draws that leg red and blocks start.</summary>
        public bool IsValid;

        /// <summary>Which leg failed, or -1. Leg 0 is start → first waypoint.</summary>
        public int FailedLeg = -1;

        public float EstimatedSeconds(float caravanTilesPerSecond = 2f)
            => caravanTilesPerSecond <= 0f ? 0f : TravelCost / caravanTilesPerSecond;

        /// <summary>Share of the route spent in one terrain type, 0–1.</summary>
        public float ShareOf(TerrainType type)
            => Tiles.Count == 0 ? 0f : (float)TilesByTerrain[(int)type] / Tiles.Count;

        /// <summary>Legs that went further than the player drew. Nothing is wrong; it is a warning.</summary>
        public int DetourLegs
        {
            get
            {
                int count = 0;
                foreach (var leg in Legs) if (leg.IsDetour) count++;
                return count;
            }
        }

        internal void Reset()
        {
            Tiles.Clear();
            Legs.Clear();
            Crossings.Clear();
            TravelCost = 0f;
            AmbushExposure = 0f;
            for (int i = 0; i < TilesByTerrain.Length; i++) TilesByTerrain[i] = 0;
            IsValid = false;
            FailedLeg = -1;
        }
    }

    /// <summary>
    /// Turns the player's five or six taps into a caravan route (docs/GDD.md §3.3).
    ///
    /// The waypoints are intermediate: every route runs start → waypoints → goal.
    /// Each leg is solved with terrain-weighted A*, so a rough finger-drawn stroke
    /// becomes a path that hugs roads and skirts marshes rather than cutting straight
    /// through them. That gap between what the player draws and what the caravan
    /// walks is the whole reason the mechanic feels good instead of fiddly.
    ///
    /// Solving is cheap enough to re-run while a waypoint is being dragged, but only
    /// the two legs touching the moved waypoint actually change — see
    /// docs/technical-design.md §6 for the throttling this enables.
    /// </summary>
    public sealed class RoutePlanner
    {
        public const int DefaultMaxWaypoints = 6;

        readonly TileGrid _grid;
        readonly GridPathfinder _pathfinder;
        readonly List<int> _waypoints = new List<int>();
        readonly List<int> _legBuffer = new List<int>();

        public RoutePlanner(TileGrid grid, int maxWaypoints = DefaultMaxWaypoints)
        {
            _grid = grid;
            _pathfinder = new GridPathfinder(grid);
            MaxWaypoints = maxWaypoints;
        }

        public int MaxWaypoints { get; }
        public IReadOnlyList<int> Waypoints => _waypoints;
        public int WaypointCount => _waypoints.Count;
        public bool IsFull => _waypoints.Count >= MaxWaypoints;

        /// <summary>
        /// Adds a waypoint. Rejects impassable tiles and duplicates — a tap on deep
        /// water should do nothing rather than silently snap somewhere the player did
        /// not choose.
        /// </summary>
        public bool TryAddWaypoint(int x, int y)
        {
            if (IsFull) return false;
            if (!_grid.IsPassable(x, y)) return false;

            int index = _grid.ToIndex(x, y);
            if (_waypoints.Contains(index)) return false;

            _waypoints.Add(index);
            return true;
        }

        public bool MoveWaypoint(int waypointIndex, int x, int y)
        {
            if (waypointIndex < 0 || waypointIndex >= _waypoints.Count) return false;
            if (!_grid.IsPassable(x, y)) return false;

            _waypoints[waypointIndex] = _grid.ToIndex(x, y);
            return true;
        }

        public bool RemoveLast()
        {
            if (_waypoints.Count == 0) return false;
            _waypoints.RemoveAt(_waypoints.Count - 1);
            return true;
        }

        public void Clear() => _waypoints.Clear();

        /// <summary>
        /// Solves start → waypoints → goal. Always produces a result; check
        /// <see cref="RouteResult.IsValid"/> before letting the player start.
        /// </summary>
        public RouteResult Solve(int startX, int startY, int goalX, int goalY, RouteResult into = null)
        {
            var result = into ?? new RouteResult();
            result.Reset();

            int legs = _waypoints.Count + 1;
            int fromX = startX, fromY = startY;

            for (int leg = 0; leg < legs; leg++)
            {
                int toX, toY;
                if (leg < _waypoints.Count) _grid.ToCoords(_waypoints[leg], out toX, out toY);
                else { toX = goalX; toY = goalY; }

                var record = new RouteLeg
                {
                    FromTile = _grid.ToIndex(fromX, fromY),
                    ToTile = _grid.ToIndex(toX, toY),
                    StraightLine = Distance(fromX, fromY, toX, toY),
                    FordTile = -1
                };

                if (!_pathfinder.TryFindPath(fromX, fromY, toX, toY, _legBuffer, out float legCost))
                {
                    record.Failed = true;
                    result.Legs.Add(record);
                    result.FailedLeg = leg;
                    return result;
                }

                record.TileCount = _legBuffer.Count;
                record.TravelCost = legCost;
                record.Walked = Walked(_legBuffer);

                foreach (int tile in _legBuffer)
                {
                    if (_grid[tile] != TerrainType.Ford) continue;
                    if (record.FordTile < 0) record.FordTile = tile;
                    if (!result.Crossings.Contains(tile)) result.Crossings.Add(tile);
                }

                result.Legs.Add(record);

                // Drop the first tile of every leg after the first: it is the previous
                // leg's last tile, and counting it twice would inflate both the
                // distance readout and the terrain breakdown the player plans against.
                // The leg still spans it — the seam belongs to both legs on screen even
                // though it is counted once.
                int startAt = result.Tiles.Count == 0 ? 0 : 1;
                record.First = System.Math.Max(result.Tiles.Count - startAt, 0);
                for (int i = startAt; i < _legBuffer.Count; i++) result.Tiles.Add(_legBuffer[i]);
                record.Last = result.Tiles.Count - 1;

                result.Legs[result.Legs.Count - 1] = record;
                result.TravelCost += legCost;
                fromX = toX;
                fromY = toY;
            }

            float ambush = 0f;
            foreach (int tile in result.Tiles)
            {
                result.TilesByTerrain[(int)_grid[tile]]++;
                ambush += TerrainTable.AmbushWeight(_grid[tile]);
            }

            result.AmbushExposure = result.Tiles.Count == 0 ? 0f : ambush / result.Tiles.Count;
            result.IsValid = true;
            return result;
        }

        /// <summary>Straight-line tiles between two tile coordinates.</summary>
        static float Distance(int fromX, int fromY, int toX, int toY)
        {
            float dx = toX - fromX, dy = toY - fromY;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Tiles walked along a path, a diagonal counting √2.
        ///
        /// Distance and not travel cost, deliberately. The detour warning is about the
        /// route going somewhere the player did not draw, and travel cost would confuse
        /// that with the route going somewhere slow — a leg through marsh is expensive
        /// without being a surprise, and a leg the long way round a river is a surprise
        /// even on good ground.
        /// </summary>
        float Walked(List<int> tiles)
        {
            float total = 0f;

            for (int i = 1; i < tiles.Count; i++)
            {
                _grid.ToCoords(tiles[i - 1], out int px, out int py);
                _grid.ToCoords(tiles[i], out int x, out int y);
                total += px != x && py != y ? Sqrt2 : 1f;
            }

            return total;
        }

        const float Sqrt2 = 1.41421356f;
    }
}
