using System.Collections.Generic;

namespace Arna.Sim
{
    public enum WagonKind : byte
    {
        /// <summary>Heals troops between fights. Losing it ends healing for the level.</summary>
        Supply = 0,

        /// <summary>Carries the loot. Damage to it costs the player their reward directly.</summary>
        Treasure = 1,

        /// <summary>Ballista support. Losing it ends covering fire.</summary>
        War = 2
    }

    public sealed class Wagon
    {
        public readonly WagonKind Kind;
        public readonly float MaxHp;
        public float Hp;

        public Wagon(WagonKind kind, float maxHp)
        {
            Kind = kind;
            MaxHp = maxHp;
            Hp = maxHp;
        }

        public bool Destroyed => Hp <= 0f;
        public float HpFraction => MaxHp <= 0f ? 0f : Hp / MaxHp;

        /// <summary>Damage that leaves the wagon at zero rather than below it.</summary>
        public float ApplyDamage(float amount)
        {
            if (amount <= 0f || Destroyed) return 0f;
            float dealt = amount < Hp ? amount : Hp;
            Hp -= dealt;
            return dealt;
        }

        public void Repair(float amount)
        {
            if (Destroyed || amount <= 0f) return;
            Hp += amount;
            if (Hp > MaxHp) Hp = MaxHp;
        }
    }

    /// <summary>
    /// Three wagons following the planned route (docs/GDD.md §5).
    ///
    /// Speed comes from the terrain under the lead wagon, which is what makes the
    /// route drawing matter moment to moment rather than only on the summary screen:
    /// the player watches the caravan bog down in the fen they chose.
    ///
    /// Losing a wagon does not end the level. A partial failure — arriving with the
    /// treasure wagon smashed and the loot gone — is far better for replay than a
    /// binary win or lose, and it is where the design puts its strongest optional ad.
    /// </summary>
    public sealed class Caravan
    {
        /// <summary>Tiles per second on terrain with no modifier.</summary>
        public const float BaseTilesPerSecond = 2f;

        /// <summary>Metres between wagons along the path.</summary>
        public const float WagonSpacing = 8f;

        const float SupplyHp = 400f;
        const float TreasureHp = 350f;
        const float WarHp = 450f;

        readonly TileGrid _grid;
        readonly int[] _tiles;
        readonly Vec2[] _points;
        readonly float[] _cumulative;
        readonly Wagon[] _wagons;

        float _distance;

        public Caravan(TileGrid grid, IReadOnlyList<int> route)
        {
            _grid = grid;
            _tiles = new int[route.Count];
            _points = new Vec2[route.Count];
            _cumulative = new float[route.Count];

            for (int i = 0; i < route.Count; i++)
            {
                _tiles[i] = route[i];
                _points[i] = Vec2.FromTile(grid, route[i]);
                _cumulative[i] = i == 0
                    ? 0f
                    : _cumulative[i - 1] + Vec2.Distance(_points[i - 1], _points[i]);
            }

            _wagons = new[]
            {
                new Wagon(WagonKind.War, WarHp),
                new Wagon(WagonKind.Supply, SupplyHp),
                new Wagon(WagonKind.Treasure, TreasureHp)
            };
        }

        public IReadOnlyList<Wagon> Wagons => _wagons;

        /// <summary>Set to zero by the Halt order, which trades ground for a tighter formation.</summary>
        public float SpeedModifier { get; set; } = 1f;

        public float TotalDistance => _cumulative.Length == 0 ? 0f : _cumulative[_cumulative.Length - 1];
        public float DistanceTravelled => _distance;
        public float Progress => TotalDistance <= 0f ? 1f : _distance / TotalDistance;
        public bool HasArrived => _distance >= TotalDistance;

        public Vec2 LeadPosition => PositionAt(_distance);

        /// <summary>All three wagons destroyed. The one true failure state.</summary>
        public bool Destroyed
        {
            get
            {
                foreach (var wagon in _wagons) if (!wagon.Destroyed) return false;
                return true;
            }
        }

        public Wagon this[WagonKind kind]
        {
            get
            {
                foreach (var wagon in _wagons) if (wagon.Kind == kind) return wagon;
                return null;
            }
        }

        /// <summary>Fraction of the loot that survives — the treasure wagon's health.</summary>
        public float LootFraction => this[WagonKind.Treasure].HpFraction;

        public int CurrentTile => TileAt(_distance);

        public TerrainType CurrentTerrain => _grid[CurrentTile];

        /// <summary>Metres per second right now, terrain and orders included.</summary>
        public float CurrentSpeed =>
            BaseTilesPerSecond * TileGrid.TileSize * TerrainTable.Speed(CurrentTerrain) * SpeedModifier;

        public void Tick(float deltaTime)
        {
            if (HasArrived || Destroyed || deltaTime <= 0f) return;

            _distance += CurrentSpeed * deltaTime;
            if (_distance > TotalDistance) _distance = TotalDistance;
        }

        /// <summary>
        /// Position of a wagon, trailing the lead along the path rather than at a
        /// straight-line offset. A column that follows the road round a bend reads as
        /// a caravan; one that cuts the corner reads as a bug.
        /// </summary>
        public Vec2 WagonPosition(int wagonIndex)
        {
            float trail = _distance - wagonIndex * WagonSpacing;
            return PositionAt(trail < 0f ? 0f : trail);
        }

        public Vec2 PositionAt(float distanceAlong)
        {
            if (_points.Length == 0) return Vec2.Zero;
            if (_points.Length == 1 || distanceAlong <= 0f) return _points[0];
            if (distanceAlong >= TotalDistance) return _points[_points.Length - 1];

            int segment = FindSegment(distanceAlong);
            float segmentStart = _cumulative[segment];
            float segmentLength = _cumulative[segment + 1] - segmentStart;
            if (segmentLength <= 0f) return _points[segment];

            float t = (distanceAlong - segmentStart) / segmentLength;
            var from = _points[segment];
            var to = _points[segment + 1];
            return new Vec2(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
        }

        public int TileAt(float distanceAlong)
        {
            if (_tiles.Length == 0) return 0;
            if (distanceAlong <= 0f) return _tiles[0];
            if (distanceAlong >= TotalDistance) return _tiles[_tiles.Length - 1];
            return _tiles[FindSegment(distanceAlong)];
        }

        int FindSegment(float distanceAlong)
        {
            // Binary search: called for every wagon on every step, and routes run to
            // a hundred points.
            int low = 0, high = _cumulative.Length - 1;
            while (low < high - 1)
            {
                int mid = (low + high) / 2;
                if (_cumulative[mid] <= distanceAlong) low = mid; else high = mid;
            }
            return low;
        }
    }
}
