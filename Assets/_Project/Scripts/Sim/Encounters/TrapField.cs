using System.Collections.Generic;

namespace TheVeil.Sim
{
    public sealed class TrackedTrap
    {
        public int Tile;
        public TrapKind Kind;
        public Vec2 Position;

        /// <summary>Visible to the player. Sticky once seen.</summary>
        public bool Revealed;

        /// <summary>Made safe by an engineer. Never fires afterwards.</summary>
        public bool Disarmed;

        /// <summary>Already went off. Traps fire once.</summary>
        public bool Triggered;

        public bool IsLive => !Disarmed && !Triggered;
    }

    /// <summary>
    /// Traps along the route (docs/GDD.md §7.2).
    ///
    /// A trap the player never saw coming is a tax, not a decision. What makes them
    /// interesting is that three different investments answer them — the scout sees
    /// them further off, the engineer defuses them, the shieldbearer absorbs the
    /// blow — so a trap-heavy route is survivable by an army built for it and brutal
    /// to one that is not.
    ///
    /// Trap points are deducted from the same budget as enemies, which is why the
    /// marshy way round carries fewer ambushes: the danger is traded, not added.
    /// </summary>
    public sealed class TrapField
    {
        /// <summary>How close the caravan must come to set a live trap off.</summary>
        public const float TriggerRadius = 3f;

        /// <summary>Distance at which an engineer can work on a revealed trap.</summary>
        public const float DisarmRadius = 8f;

        readonly List<TrackedTrap> _traps = new List<TrackedTrap>();

        /// <summary>Traps that fired on the last update. Cleared each update.</summary>
        public readonly List<TrackedTrap> TriggeredThisTick = new List<TrackedTrap>();

        /// <summary>Traps that became visible on the last update. Cleared each update.</summary>
        public readonly List<TrackedTrap> RevealedThisTick = new List<TrackedTrap>();

        public TrapField(TileGrid grid, IReadOnlyList<TrapPlacement> placements)
        {
            foreach (var placement in placements)
                _traps.Add(new TrackedTrap
                {
                    Tile = placement.Tile,
                    Kind = placement.Kind,
                    Position = Vec2.FromTile(grid, placement.Tile)
                });
        }

        public IReadOnlyList<TrackedTrap> Traps => _traps;

        public int RevealedCount
        {
            get { int n = 0; foreach (var t in _traps) if (t.Revealed) n++; return n; }
        }

        public int LiveCount
        {
            get { int n = 0; foreach (var t in _traps) if (t.IsLive) n++; return n; }
        }

        /// <summary>Traps that have gone off. The one number that says a trap field worked.</summary>
        public int TriggeredCount
        {
            get { int n = 0; foreach (var t in _traps) if (t.Triggered) n++; return n; }
        }

        public int DisarmedCount
        {
            get { int n = 0; foreach (var t in _traps) if (t.Disarmed) n++; return n; }
        }

        /// <summary>
        /// Reveals traps near the given watchers and fires any the caravan walks onto.
        /// </summary>
        /// <param name="trapSight">
        /// Extra spotting range, from a scout or an engineer. Zero means the trap is
        /// only noticed at its own reveal radius, which is usually too late to do
        /// anything but brace.
        /// </param>
        public void Update(Vec2 caravanPosition, IReadOnlyList<Watcher> watchers, float trapSight = 0f)
        {
            TriggeredThisTick.Clear();
            RevealedThisTick.Clear();

            Reveal(caravanPosition, watchers, trapSight);
            Trigger(caravanPosition);
        }

        void Reveal(Vec2 caravanPosition, IReadOnlyList<Watcher> watchers, float trapSight)
        {
            foreach (var trap in _traps)
            {
                if (trap.Revealed) continue;

                float ownRadius = TrapTable.RevealRadius(trap.Kind);
                float radius = trapSight > ownRadius ? trapSight : ownRadius;

                bool seen = Vec2.DistanceSquared(caravanPosition, trap.Position) <= radius * radius;

                if (!seen && watchers != null)
                {
                    foreach (var watcher in watchers)
                    {
                        if (Vec2.DistanceSquared(watcher.Position, trap.Position) > radius * radius) continue;
                        seen = true;
                        break;
                    }
                }

                if (!seen) continue;

                trap.Revealed = true;
                RevealedThisTick.Add(trap);
            }
        }

        void Trigger(Vec2 caravanPosition)
        {
            foreach (var trap in _traps)
            {
                if (!trap.IsLive) continue;
                if (Vec2.DistanceSquared(caravanPosition, trap.Position) > TriggerRadius * TriggerRadius) continue;

                trap.Triggered = true;
                TriggeredThisTick.Add(trap);
            }
        }

        /// <summary>
        /// Defuses the nearest revealed trap within reach. Returns it, or null.
        ///
        /// Only revealed traps can be worked on — an engineer cannot defuse what
        /// nobody has spotted, which is what ties the engineer to the scout rather
        /// than making either of them sufficient alone.
        /// </summary>
        public TrackedTrap TryDisarmNearest(Vec2 engineerPosition, float radius = DisarmRadius)
        {
            TrackedTrap best = null;
            float bestDistance = radius * radius;

            foreach (var trap in _traps)
            {
                if (!trap.IsLive || !trap.Revealed) continue;

                float distance = Vec2.DistanceSquared(engineerPosition, trap.Position);
                if (distance > bestDistance) continue;

                bestDistance = distance;
                best = trap;
            }

            if (best != null) best.Disarmed = true;
            return best;
        }
    }
}
