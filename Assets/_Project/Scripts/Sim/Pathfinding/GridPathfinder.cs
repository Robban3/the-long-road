using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>
    /// Terrain-weighted A* over a <see cref="TileGrid"/>.
    ///
    /// This runs between each pair of player waypoints to turn a rough finger-drawn
    /// stroke into a believable route (docs/technical-design.md §6). Because it
    /// minimises <see cref="TerrainTable.TravelCost"/> rather than raw distance, the
    /// path naturally hugs roads and skirts marshes instead of cutting straight
    /// through them — which is what makes five taps read as a route a caravan master
    /// would actually pick.
    ///
    /// One instance is reused across calls; buffers are allocated once and
    /// invalidated with a generation counter rather than cleared, since the route
    /// preview re-solves on every frame the player drags a waypoint.
    /// </summary>
    public sealed class GridPathfinder
    {
        const float Sqrt2 = 1.41421356f;

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        readonly TileGrid _grid;
        readonly float[] _g;
        readonly int[] _cameFrom;
        readonly int[] _seen;
        readonly int[] _closed;
        readonly MinHeap _open;
        int _generation;

        public GridPathfinder(TileGrid grid)
        {
            _grid = grid;
            int n = grid.TileCount;
            _g = new float[n];
            _cameFrom = new int[n];
            _seen = new int[n];
            _closed = new int[n];
            _open = new MinHeap(n);
        }

        /// <summary>
        /// Finds the fastest route between two tiles.
        /// </summary>
        /// <param name="pathOut">Cleared, then filled with tile indices from start to goal inclusive.</param>
        /// <param name="travelTime">Cost in tile-crossings; multiply by the caravan's seconds-per-tile.</param>
        /// <param name="extraCost">
        /// Optional non-negative surcharge per tile, added on top of terrain cost. This is
        /// how the generator asks for routes other than the fastest one: penalise ambush-prone
        /// terrain and you get the cautious route, penalise the tiles already used and you get
        /// a genuinely different third corridor. Values must not be negative or the heuristic
        /// stops being admissible and A* may return a non-optimal path.
        /// </param>
        /// <returns>False if either endpoint is impassable or no route exists.</returns>
        public bool TryFindPath(int startX, int startY, int goalX, int goalY,
                                List<int> pathOut, out float travelTime, float[] extraCost = null)
        {
            pathOut.Clear();
            travelTime = 0f;

            if (!_grid.IsPassable(startX, startY) || !_grid.IsPassable(goalX, goalY))
                return false;

            int start = _grid.ToIndex(startX, startY);
            int goal = _grid.ToIndex(goalX, goalY);

            if (start == goal)
            {
                pathOut.Add(start);
                return true;
            }

            _generation++;
            _open.Clear();

            _g[start] = 0f;
            _cameFrom[start] = -1;
            _seen[start] = _generation;
            _open.Push(start, Heuristic(startX, startY, goalX, goalY));

            while (_open.Count > 0)
            {
                int current = _open.Pop();
                if (_closed[current] == _generation) continue;   // stale duplicate
                _closed[current] = _generation;

                if (current == goal)
                {
                    Reconstruct(goal, pathOut);
                    travelTime = _g[goal];
                    return true;
                }

                _grid.ToCoords(current, out int cx, out int cy);

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX[d];
                    int ny = cy + DY[d];
                    if (!_grid.IsPassable(nx, ny)) continue;

                    bool diagonal = d >= 4;
                    if (diagonal)
                    {
                        // Refuse to squeeze diagonally between two blocked tiles.
                        // Without this a route can slip through the corner where two
                        // cliffs meet — a gap that does not exist in the rendered world.
                        if (!_grid.IsPassable(cx + DX[d], cy) || !_grid.IsPassable(cx, cy + DY[d]))
                            continue;
                    }

                    int neighbour = _grid.ToIndex(nx, ny);
                    if (_closed[neighbour] == _generation) continue;

                    float step = TerrainTable.TravelCost(_grid[neighbour]);
                    if (diagonal) step *= Sqrt2;
                    if (extraCost != null) step += extraCost[neighbour];

                    float tentative = _g[current] + step;

                    if (_seen[neighbour] != _generation || tentative < _g[neighbour])
                    {
                        _seen[neighbour] = _generation;
                        _g[neighbour] = tentative;
                        _cameFrom[neighbour] = current;
                        _open.Push(neighbour, tentative + Heuristic(nx, ny, goalX, goalY));
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Octile distance scaled by the cheapest possible tile. Never overestimates,
        /// so A* still returns a genuinely optimal route.
        /// </summary>
        static float Heuristic(int x, int y, int goalX, int goalY)
        {
            int dx = x > goalX ? x - goalX : goalX - x;
            int dy = y > goalY ? y - goalY : goalY - y;
            int hi = dx > dy ? dx : dy;
            int lo = dx > dy ? dy : dx;
            return (hi + (Sqrt2 - 1f) * lo) * TerrainTable.MinTravelCost;
        }

        void Reconstruct(int goal, List<int> pathOut)
        {
            int node = goal;
            while (node != -1)
            {
                pathOut.Add(node);
                node = _cameFrom[node];
            }
            pathOut.Reverse();
        }

        /// <summary>Binary min-heap keyed on f-score, with lazy deletion.</summary>
        sealed class MinHeap
        {
            int[] _items;
            float[] _keys;
            int _count;

            public MinHeap(int capacity)
            {
                if (capacity < 16) capacity = 16;
                _items = new int[capacity];
                _keys = new float[capacity];
            }

            public int Count => _count;
            public void Clear() => _count = 0;

            public void Push(int item, float key)
            {
                if (_count == _items.Length)
                {
                    System.Array.Resize(ref _items, _count * 2);
                    System.Array.Resize(ref _keys, _count * 2);
                }

                int i = _count++;
                _items[i] = item;
                _keys[i] = key;

                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (_keys[parent] <= _keys[i]) break;
                    Swap(parent, i);
                    i = parent;
                }
            }

            public int Pop()
            {
                int result = _items[0];
                _count--;
                if (_count > 0)
                {
                    _items[0] = _items[_count];
                    _keys[0] = _keys[_count];

                    int i = 0;
                    while (true)
                    {
                        int left = 2 * i + 1;
                        if (left >= _count) break;
                        int right = left + 1;
                        int smallest = (right < _count && _keys[right] < _keys[left]) ? right : left;
                        if (_keys[i] <= _keys[smallest]) break;
                        Swap(i, smallest);
                        i = smallest;
                    }
                }
                return result;
            }

            void Swap(int a, int b)
            {
                (_items[a], _items[b]) = (_items[b], _items[a]);
                (_keys[a], _keys[b]) = (_keys[b], _keys[a]);
            }
        }
    }
}
