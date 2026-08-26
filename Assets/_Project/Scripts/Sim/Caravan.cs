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

        /// <summary>
        /// Metres between wagons along the path.
        ///
        /// 15, and the number comes from what is hitched to the front of each one.
        ///
        /// Measured from a wagon's own centre: a team's noses reach ahead of the wagon
        /// they pull by half a cart plus the draught pole plus a horse, while the cart
        /// behind them extends half its own length backwards. Anything less than the sum
        /// puts one wagon's horses inside the wagon in front — and that failure is
        /// invisible rather than obviously broken, because the animals do not vanish,
        /// they are simply drawn behind planking. At the original 8 m they were three
        /// metres in, which reads as one horse per wagon rather than two.
        ///
        /// **The three carts are not the same length**, so one spacing is only as good
        /// as the worst pair it has to hold apart, and which pair that is, is a fact
        /// about the models rather than about this file. 12.5 was worked out for a
        /// covered wagon and was still tight at the head of the column, where the longer
        /// supply wagon is. 15 carries that, and a horse whose measured length runs long
        /// because a rigged model's bounds cover every clip it can play — a galloping
        /// pose included.
        ///
        /// It is not left as arithmetic: `RunVisuals.CheckSpacing` measures each pair
        /// against the models actually loaded, prints the tightest, and warns with the
        /// figure needed when this constant is short of it.
        /// </summary>
        public const float WagonSpacing = 15f;

        /// <summary>
        /// Metres of road behind the start line, for the column to form up on.
        ///
        /// Without it every wagon begins on the start tile, stacked, and the third one
        /// is not visible until the first two have driven thirty metres out from under
        /// it. A caravan that assembles itself out of one point in the first four
        /// seconds is the first thing a player sees of this game.
        ///
        /// 40: the rearmost of three wagons trails 2 × <see cref="WagonSpacing"/> = 30 m,
        /// and its own team stands about 8 m further back again, so 38 puts the last
        /// horse's nose on the run-up and 40 leaves it a little air. The lead wagon still
        /// starts exactly on the start tile and still finishes on the goal: the run-up is
        /// ground for the tail to stand on, not journey. Everything the game measures —
        /// <see cref="TotalDistance"/>, <see cref="Progress"/>, <see cref="HasArrived"/> —
        /// counts from the start line, which is why they take their origin from it.
        ///
        /// The start is chosen from the leftmost three columns of the map, so this road
        /// is off the map by construction. `TerrainMeshBuilder`'s skirt is what puts
        /// ground under it.
        /// </summary>
        public const float RunUp = 40f;

        const float SupplyHp = 400f;
        const float TreasureHp = 350f;
        const float WarHp = 450f;

        readonly TileGrid _grid;
        readonly int[] _tiles;
        readonly Vec2[] _points;
        readonly float[] _cumulative;
        readonly Wagon[] _wagons;

        float _distance;

        /// <summary>Distance along the path at which the route proper begins.</summary>
        readonly float _origin;

        public Caravan(TileGrid grid, IReadOnlyList<int> route)
        {
            _grid = grid;

            int count = route.Count;

            // One synthetic point behind the start, and only when there is a route to
            // stand behind. A straight run-up needs no more than one: the segment from
            // it to the start tile is interpolated like any other.
            int lead = count > 0 ? 1 : 0;

            _tiles = new int[count + lead];
            _points = new Vec2[count + lead];
            _cumulative = new float[count + lead];

            if (lead == 1)
            {
                var first = Vec2.FromTile(grid, route[0]);

                // Backwards along the road's own first step, so the column arrives on
                // the line it is about to travel rather than joining it from the side.
                var next = count > 1 ? Vec2.FromTile(grid, route[1]) : new Vec2(first.X + 1f, first.Y);

                float step = Vec2.Distance(first, next);

                float backX = step < 0.0001f ? -1f : (first.X - next.X) / step;
                float backY = step < 0.0001f ? 0f : (first.Y - next.Y) / step;

                // The tile is the start's. Nothing reads terrain out here — the lead
                // never stands on it — and a tile index has to be something.
                _tiles[0] = route[0];
                _points[0] = new Vec2(first.X + backX * RunUp, first.Y + backY * RunUp);
                _cumulative[0] = 0f;
            }

            for (int i = 0; i < count; i++)
            {
                int at = i + lead;

                _tiles[at] = route[i];
                _points[at] = Vec2.FromTile(grid, route[i]);
                _cumulative[at] = at == 0
                    ? 0f
                    : _cumulative[at - 1] + Vec2.Distance(_points[at - 1], _points[at]);
            }

            _origin = lead == 1 ? _cumulative[1] : 0f;
            _distance = _origin;

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

        /// <summary>
        /// The whole path including the run-up, which is what positions are measured
        /// along. Everything the game reports counts from the start line instead.
        /// </summary>
        float PathLength => _cumulative.Length == 0 ? 0f : _cumulative[_cumulative.Length - 1];

        public float TotalDistance => PathLength - _origin;
        public float DistanceTravelled => _distance - _origin;
        public float Progress => TotalDistance <= 0f ? 1f : (_distance - _origin) / TotalDistance;
        public bool HasArrived => _distance >= PathLength;

        public Vec2 LeadPosition => PositionAt(_distance);

        /// <summary>
        /// Unit vector along the route. The formation rotates with it, so the van is
        /// always the front whichever way the road happens to run.
        /// </summary>
        public Vec2 Heading
        {
            get
            {
                if (_points.Length < 2) return new Vec2(1f, 0f);

                var ahead = PositionAt(_distance + 1f);
                var behind = PositionAt(_distance - 1f > 0f ? _distance - 1f : 0f);
                var delta = ahead - behind;

                float length = Vec2.Distance(Vec2.Zero, delta);
                return length < 0.0001f ? new Vec2(1f, 0f) : new Vec2(delta.X / length, delta.Y / length);
            }
        }

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
            if (_distance > PathLength) _distance = PathLength;
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
            if (distanceAlong >= PathLength) return _points[_points.Length - 1];

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
            if (distanceAlong >= PathLength) return _tiles[_tiles.Length - 1];
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
