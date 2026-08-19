using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>A troop that can see. Position and sight radius before terrain is applied.</summary>
    public struct Watcher
    {
        public Vec2 Position;
        public float SightRadius;

        public Watcher(Vec2 position, float sightRadius)
        {
            Position = position;
            SightRadius = sightRadius;
        }
    }

    public sealed class TrackedEnemy
    {
        public int Tile;
        public EnemyKind Kind;
        public Vec2 Position;

        /// <summary>The group has noticed the caravan and is attacking.</summary>
        public bool Awake;

        /// <summary>The player can see the group. Sticky — once seen it stays on the map.</summary>
        public bool Revealed;

        /// <summary>Seen before it woke. Earns the scouting bounty and is paid exactly once.</summary>
        public bool SpottedEarly;

        internal bool ScoutingPaid;
    }

    /// <summary>
    /// The fog of war (docs/GDD.md §3.4).
    ///
    /// The whole design rests on two radii being kept apart:
    ///
    /// <b>The enemy's detect radius</b> decides when a group wakes and attacks. It is
    /// measured against the caravan, because that is what the enemy is after.
    ///
    /// <b>The player's sight radius</b> decides when a group is drawn on screen. It is
    /// measured from the troops, and scaled by the terrain they stand in.
    ///
    /// When sight exceeds detection the player sees trouble before it stirs and has
    /// time to reposition. That gap is the entire reason to spend one of six troop
    /// slots on a scout who barely fights — and the reason spotting pays silver, so a
    /// kill-driven economy does not make the scout unaffordable.
    ///
    /// Updated four times a second rather than every frame. Detection needs no more
    /// precision than that and the cost falls by about eighty percent.
    /// </summary>
    public sealed class DetectionSystem
    {
        public const float UpdateInterval = 0.25f;
        const float CellSize = 8f;

        readonly TileGrid _grid;
        readonly List<TrackedEnemy> _enemies = new List<TrackedEnemy>();
        readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

        float _accumulator;

        /// <summary>Enemies that became visible on the last update. Cleared each tick.</summary>
        public readonly List<TrackedEnemy> RevealedThisTick = new List<TrackedEnemy>();

        /// <summary>Enemies that woke on the last update. Cleared each tick.</summary>
        public readonly List<TrackedEnemy> WokeThisTick = new List<TrackedEnemy>();

        public DetectionSystem(TileGrid grid, IReadOnlyList<EnemySpawn> spawns)
        {
            _grid = grid;

            for (int i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                _enemies.Add(new TrackedEnemy
                {
                    Tile = spawn.Tile,
                    Kind = spawn.Kind,
                    Position = Vec2.FromTile(grid, spawn.Tile)
                });
                AddToCell(i, _enemies[i].Position);
            }
        }

        public IReadOnlyList<TrackedEnemy> Enemies => _enemies;

        public int RevealedCount
        {
            get { int n = 0; foreach (var e in _enemies) if (e.Revealed) n++; return n; }
        }

        public int AwakeCount
        {
            get { int n = 0; foreach (var e in _enemies) if (e.Awake) n++; return n; }
        }

        public int SpottedEarlyCount
        {
            get { int n = 0; foreach (var e in _enemies) if (e.SpottedEarly) n++; return n; }
        }

        /// <summary>
        /// Advances the accumulator and runs a detection pass when one is due.
        /// Returns true if a pass ran.
        /// </summary>
        public bool Tick(float deltaTime, Vec2 caravanPosition, IReadOnlyList<Watcher> watchers)
        {
            _accumulator += deltaTime;
            if (_accumulator < UpdateInterval) return false;

            _accumulator -= UpdateInterval;
            Scan(caravanPosition, watchers);
            return true;
        }

        /// <summary>Runs a pass immediately, ignoring the update interval.</summary>
        public void Scan(Vec2 caravanPosition, IReadOnlyList<Watcher> watchers)
        {
            RevealedThisTick.Clear();
            WokeThisTick.Clear();

            RevealAroundWatchers(watchers);
            WakeAroundCaravan(caravanPosition);
        }

        void RevealAroundWatchers(IReadOnlyList<Watcher> watchers)
        {
            if (watchers == null) return;

            for (int w = 0; w < watchers.Count; w++)
            {
                var watcher = watchers[w];
                float radius = EffectiveSight(watcher);
                if (radius <= 0f) continue;

                foreach (int index in Query(watcher.Position, radius))
                {
                    var enemy = _enemies[index];
                    if (enemy.Revealed) continue;
                    if (Vec2.DistanceSquared(watcher.Position, enemy.Position) > radius * radius) continue;

                    enemy.Revealed = true;

                    // Paid once, and only when the group had not already noticed us.
                    if (!enemy.Awake && !enemy.ScoutingPaid)
                    {
                        enemy.SpottedEarly = true;
                        enemy.ScoutingPaid = true;
                    }

                    RevealedThisTick.Add(enemy);
                }
            }
        }

        void WakeAroundCaravan(Vec2 caravanPosition)
        {
            // The largest detect radius in the table bounds how far we need to look.
            float widest = 0f;
            foreach (var kind in EnemyTable.All)
                if (EnemyTable.DetectRadius(kind) > widest) widest = EnemyTable.DetectRadius(kind);

            foreach (int index in Query(caravanPosition, widest))
            {
                var enemy = _enemies[index];
                if (enemy.Awake) continue;

                float radius = EnemyTable.DetectRadius(enemy.Kind);
                if (Vec2.DistanceSquared(caravanPosition, enemy.Position) > radius * radius) continue;

                enemy.Awake = true;

                // Waking also reveals: a group charging the caravan is not hidden.
                if (!enemy.Revealed)
                {
                    enemy.Revealed = true;
                    RevealedThisTick.Add(enemy);
                }

                enemy.ScoutingPaid = true;
                WokeThisTick.Add(enemy);
            }
        }

        /// <summary>
        /// Sight scaled by the terrain the watcher stands in. Dense forest roughly
        /// halves it, open plain extends it — which is why a route through the woods
        /// is dangerous beyond its ambush chance: you simply cannot see.
        /// </summary>
        float EffectiveSight(Watcher watcher)
        {
            int tx = (int)(watcher.Position.X / TileGrid.TileSize);
            int ty = (int)(watcher.Position.Y / TileGrid.TileSize);
            if (!_grid.InBounds(tx, ty)) return watcher.SightRadius;

            return watcher.SightRadius * TerrainTable.Sight(_grid[tx, ty]);
        }

        // --- spatial hash -----------------------------------------------------------

        void AddToCell(int enemyIndex, Vec2 position)
        {
            long key = CellKey(position);
            if (!_cells.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>();
                _cells[key] = bucket;
            }
            bucket.Add(enemyIndex);
        }

        static long CellKey(Vec2 position)
            => CellKey((int)(position.X / CellSize), (int)(position.Y / CellSize));

        static long CellKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        /// <summary>
        /// Enemy indices in the cells overlapping a circle. A superset — callers still
        /// check the real distance. Scanning every enemy against every watcher would
        /// be fine for one level, but the same grid backs the trap and combat queries.
        /// </summary>
        IEnumerable<int> Query(Vec2 centre, float radius)
        {
            int minX = (int)((centre.X - radius) / CellSize);
            int maxX = (int)((centre.X + radius) / CellSize);
            int minY = (int)((centre.Y - radius) / CellSize);
            int maxY = (int)((centre.Y + radius) / CellSize);

            for (int cx = minX; cx <= maxX; cx++)
                for (int cy = minY; cy <= maxY; cy++)
                    if (_cells.TryGetValue(CellKey(cx, cy), out var bucket))
                        foreach (int index in bucket)
                            yield return index;
        }
    }
}
