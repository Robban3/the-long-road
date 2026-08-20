using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>An enemy group destroyed this step, and whether it cost the player anything.</summary>
    public struct EnemyDefeat
    {
        public EnemyKind Kind;
        public bool Flawless;
    }

    /// <summary>
    /// Resolves the fighting (docs/GDD.md §4, §7).
    ///
    /// Troops hold their posts and strike whatever comes into reach; enemies close on
    /// the caravan and are intercepted on the way. Everything is pooled health and
    /// damage per second rather than individual models trading blows — at 20 Hz on a
    /// phone, with several groups engaged, anything finer would cost more than it
    /// communicates.
    ///
    /// Two rules carry most of the tactical weight:
    ///
    /// A troop can only strike what has been <b>revealed</b>. Reach is worthless
    /// without vision, which is what stops an upgraded archer from replacing the
    /// scout instead of complementing her.
    ///
    /// A troop will not chase beyond its <b>leash</b>. Without that, the formation
    /// dissolves the moment anything appears and the six posts stop meaning anything.
    /// </summary>
    public sealed class CombatSystem
    {
        /// <summary>Caravan speed while anything is in contact. Picking through a fight is slow.</summary>
        public const float EngagedSpeedFactor = 0.5f;

        /// <summary>How close an enemy must be to a troop before it stops to fight.</summary>
        const float EngagementSlack = 1.5f;

        readonly TileGrid _grid;
        readonly Caravan _caravan;
        readonly Squad _squad;
        readonly DetectionSystem _detection;

        readonly HashSet<TrackedEnemy> _bloodied = new HashSet<TrackedEnemy>();
        readonly Dictionary<TrackedEnemy, float> _health = new Dictionary<TrackedEnemy, float>();

        /// <summary>Groups destroyed on the last step. Cleared each step.</summary>
        public readonly List<EnemyDefeat> DefeatedThisStep = new List<EnemyDefeat>();

        public CombatSystem(TileGrid grid, Caravan caravan, Squad squad,
                            DetectionSystem detection, float enemyStrength = 1f)
        {
            _grid = grid;
            _caravan = caravan;
            _squad = squad;
            _detection = detection;
            EnemyStrength = enemyStrength;
        }

        public float EnemyStrength { get; }

        /// <summary>True while any enemy is in contact, which slows the column.</summary>
        public bool InContact { get; private set; }

        public int EnemiesEngaged { get; private set; }

        public float HealthOf(TrackedEnemy enemy)
        {
            if (_health.TryGetValue(enemy, out float hp)) return hp;
            return EnemyTable.GroupHp(enemy.Kind) * EnemyStrength;
        }

        public bool IsDefeated(TrackedEnemy enemy) => HealthOf(enemy) <= 0f;

        public void Step(float deltaTime)
        {
            DefeatedThisStep.Clear();
            InContact = false;
            EnemiesEngaged = 0;

            var terrain = _caravan.CurrentTerrain;

            MoveAndStrike(deltaTime, terrain);
            TroopsReturnFire(deltaTime, terrain);
            Heal(deltaTime);

            _caravan.SpeedModifier = InContact ? EngagedSpeedFactor : 1f;
        }

        /// <summary>Awake enemies close on the caravan and hit whatever intercepts them.</summary>
        void MoveAndStrike(float deltaTime, TerrainType terrain)
        {
            foreach (var enemy in _detection.Enemies)
            {
                if (!enemy.Awake || IsDefeated(enemy)) continue;

                var target = NearestTroopInReach(enemy);
                if (target != null)
                {
                    InContact = true;
                    EnemiesEngaged++;

                    float damage = EnemyTable.Dps(enemy.Kind) * EnemyStrength * HealthFraction(enemy) * deltaTime;
                    target.TakeDamage(damage);

                    // Any loss while this group is engaged forfeits its clean-kill bonus.
                    if (target.ModelsLost > 0) _bloodied.Add(enemy);
                    continue;
                }

                float reachToCaravan = EnemyTable.AttackRange(enemy.Kind) + EngagementSlack;
                if (Vec2.DistanceSquared(enemy.Position, _caravan.LeadPosition) <= reachToCaravan * reachToCaravan)
                {
                    InContact = true;
                    EnemiesEngaged++;
                    StrikeCaravan(enemy, deltaTime);
                    continue;
                }

                Advance(enemy, deltaTime, terrain);
            }
        }

        void Advance(TrackedEnemy enemy, float deltaTime, TerrainType terrain)
        {
            var toCaravan = _caravan.LeadPosition - enemy.Position;
            float distance = Vec2.Distance(Vec2.Zero, toCaravan);
            if (distance < 0.0001f) return;

            float speed = EnemyTable.Speed(enemy.Kind) * TileGrid.TileSize * TerrainTable.Speed(terrain);
            float step = speed * deltaTime;
            if (step > distance) step = distance;

            enemy.Position = new Vec2(
                enemy.Position.X + toCaravan.X / distance * step,
                enemy.Position.Y + toCaravan.Y / distance * step);
        }

        void StrikeCaravan(TrackedEnemy enemy, float deltaTime)
        {
            float damage = EnemyTable.Dps(enemy.Kind) * EnemyStrength * HealthFraction(enemy) * deltaTime;

            // Bandits go for the treasure; everything else hits whatever is in front.
            var wagon = enemy.Kind == EnemyKind.Bandit
                ? _caravan[WagonKind.Treasure]
                : null;

            if (wagon == null || wagon.Destroyed)
            {
                foreach (var candidate in _caravan.Wagons)
                {
                    if (candidate.Destroyed) continue;
                    wagon = candidate;
                    break;
                }
            }

            wagon?.ApplyDamage(damage);
        }

        /// <summary>Troops strike the nearest revealed enemy they can both see and reach.</summary>
        void TroopsReturnFire(float deltaTime, TerrainType terrain)
        {
            float squadSight = _squad.BestSight;

            foreach (var group in _squad.Slots)
            {
                if (group == null || !group.Alive) continue;

                float dps = group.DamageAgainst(EnemyKind.Wolf, terrain);
                if (dps <= 0f) continue;

                float reach = TroopUpgrades.UsableRange(group.AttackRange(terrain), squadSight);
                var target = NearestEnemyInReach(group, reach);
                if (target == null) continue;

                InContact = true;

                float damage = group.DamageAgainst(target.Kind, terrain) * deltaTime;
                float remaining = HealthOf(target) - damage;
                _health[target] = remaining;

                if (remaining > 0f) continue;

                _health[target] = 0f;
                DefeatedThisStep.Add(new EnemyDefeat
                {
                    Kind = target.Kind,
                    Flawless = !_bloodied.Contains(target)
                });
            }
        }

        void Heal(float deltaTime)
        {
            foreach (var healer in _squad.Slots)
            {
                if (healer == null || !healer.Alive) continue;

                float rate = TroopTable.HealPerSecond(healer.Kind);
                if (rate <= 0f) continue;

                // Out of contact a priest works three times as fast (docs/GDD.md §4.3).
                if (!InContact) rate *= 3f;

                TroopGroup worst = null;
                float worstFraction = 1f;
                foreach (var group in _squad.Slots)
                {
                    if (group == null || !group.Alive) continue;
                    float fraction = group.Hp / group.EffectiveMaxHp;
                    if (fraction >= worstFraction) continue;
                    worstFraction = fraction;
                    worst = group;
                }

                worst?.Heal(rate * deltaTime);
            }
        }

        TroopGroup NearestTroopInReach(TrackedEnemy enemy)
        {
            float reach = EnemyTable.AttackRange(enemy.Kind) + EngagementSlack;
            float best = reach * reach;
            TroopGroup found = null;

            foreach (var group in _squad.Slots)
            {
                if (group == null || !group.Alive) continue;

                float distance = Vec2.DistanceSquared(enemy.Position, group.Position);
                if (distance > best) continue;

                best = distance;
                found = group;
            }
            return found;
        }

        TrackedEnemy NearestEnemyInReach(TroopGroup group, float reach)
        {
            float best = reach * reach;
            float leashSquared = Squad.Leash * Squad.Leash;
            TrackedEnemy found = null;

            foreach (var enemy in _detection.Enemies)
            {
                // Only what has been seen, and only within the leash: a formation that
                // dissolves to chase the first thing it spots is not a formation.
                if (!enemy.Revealed || IsDefeated(enemy)) continue;
                if (Vec2.DistanceSquared(enemy.Position, group.Position) > leashSquared + best) continue;

                float distance = Vec2.DistanceSquared(enemy.Position, group.Position);
                if (distance > best) continue;

                best = distance;
                found = enemy;
            }
            return found;
        }

        /// <summary>A wounded group hits proportionally softer, as troops do.</summary>
        float HealthFraction(TrackedEnemy enemy)
        {
            float full = EnemyTable.GroupHp(enemy.Kind) * EnemyStrength;
            return full <= 0f ? 0f : HealthOf(enemy) / full;
        }
    }
}
