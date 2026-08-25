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

        /// <summary>
        /// Metres of country this group watches, from <see cref="EnemySpawn.Territory"/>.
        /// Zero falls back to the table's detect radius.
        ///
        /// A group wakes when the caravan enters its territory rather than when it comes
        /// within a fixed sixteen metres. That is what lets twelve groups cover a map a
        /// player may cross anywhere: they hold a stretch of country between them, and
        /// crossing anyone's stretch is a fight. See EncounterPlacer for why placement
        /// alone could not make that promise.
        /// </summary>
        public float Territory;

        /// <summary>The group has noticed the caravan and is attacking.</summary>
        public bool Awake;

        /// <summary>
        /// The troop group this one is closing on, or null when it is asleep, beaten,
        /// or the escort is gone and it is going for the wagons.
        ///
        /// The combat step picks this every tick and used to throw it away, so the view
        /// had to guess: it turned every attacker toward the head of the column. A pack
        /// mauling the rear guard therefore stood side-on to the troops it was biting
        /// and looked at the wagons instead. Kept here, the picture agrees with the
        /// fight.
        /// </summary>
        public TroopGroup Engaging;

        /// <summary>
        /// The group is in contact and swinging, rather than still crossing the ground.
        ///
        /// Recorded rather than re-derived from distance in the view, which had to
        /// guess at the engagement slack and got a different answer than the combat
        /// step did — animals biting a metre before they arrived, or running on the
        /// spot after they had.
        /// </summary>
        public bool Striking;

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
                    Position = Vec2.FromTile(grid, spawn.Tile),
                    Territory = spawn.Territory * TileGrid.TileSize
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

        /// <summary>Range at which a group notices the caravan: its territory, or the table's.</summary>
        public static float WakeRadius(TrackedEnemy enemy)
        {
            float table = EnemyTable.DetectRadius(enemy.Kind);
            return enemy.Territory > table ? enemy.Territory : table;
        }

        void WakeAroundCaravan(Vec2 caravanPosition)
        {
            // The widest wake radius on the map bounds how far we need to look. It is a
            // property of the placed groups now rather than of the table, because a
            // group's territory can reach further than its eyes.
            float widest = 0f;
            foreach (var enemy in _enemies)
                if (WakeRadius(enemy) > widest) widest = WakeRadius(enemy);

            foreach (int index in Query(caravanPosition, widest))
            {
                var enemy = _enemies[index];
                if (enemy.Awake) continue;

                float radius = WakeRadius(enemy);
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
