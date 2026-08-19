using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>
    /// The result of stitching the player's waypoints into one caravan route.
    /// </summary>
    public sealed class RouteResult
    {
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

        internal void Reset()
        {
            Tiles.Clear();
            TravelCost = 0f;
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

                if (!_pathfinder.TryFindPath(fromX, fromY, toX, toY, _legBuffer, out float legCost))
                {
                    result.FailedLeg = leg;
                    return result;
                }

                // Drop the first tile of every leg after the first: it is the previous
                // leg's last tile, and counting it twice would inflate both the
                // distance readout and the terrain breakdown the player plans against.
                int startAt = result.Tiles.Count == 0 ? 0 : 1;
                for (int i = startAt; i < _legBuffer.Count; i++) result.Tiles.Add(_legBuffer[i]);

                result.TravelCost += legCost;
                fromX = toX;
                fromY = toY;
            }

            foreach (int tile in result.Tiles)
                result.TilesByTerrain[(int)_grid[tile]]++;

            result.IsValid = true;
            return result;
        }
    }
}
