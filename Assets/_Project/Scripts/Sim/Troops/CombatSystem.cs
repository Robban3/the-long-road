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
        /// <summary>
        /// Caravan speed while anything is in contact. Zero: the column halts and the
        /// escort forms up.
        ///
        /// It used to be a half, and the wagons rolled on through the fight. Stopping is
        /// the truer picture and the more legible one — a fight becomes an event rather
        /// than a stretch of slower road — but on its own it would have taken the third
        /// star with it: six fights at four and a half seconds is twenty-seven seconds
        /// of standing still against a par slack of fourteen to nineteen.
        ///
        /// So the clock par is measured against stops too. See
        /// <see cref="LevelRun.TravelSeconds"/>: the fight costs blood, the route costs
        /// time, and the third star goes on asking both questions instead of one twice.
        /// </summary>
        public const float EngagedSpeedFactor = 0f;

        /// <summary>How close an enemy must be to a troop before it stops to fight.</summary>
        public const float EngagementSlack = 1.5f;

        /// <summary>
        /// How close a fight has to be before the column halts for it.
        ///
        /// Halting for anything at all was very nearly fatal. An archer band stops at
        /// its own eighteen metres of reach and shoots; the column stopped too, and
        /// since the supply wagon only patches the escort up between fights, neither
        /// side could disengage. Level 1-5 ended with the caravan destroyed at seven
        /// percent of the route after two hundred and twenty-eight seconds, of which
        /// three were spent moving — and the archers who did it finished on full
        /// health, having been stood a comfortable thirteen metres away the whole time.
        ///
        /// Five metres, measured from the troop rather than from the wagons: a melee
        /// attacker halts at its two metres plus the engagement slack, so anything
        /// actually in contact is inside it with room to spare, and an archer at
        /// nineteen and a half is nowhere near. Which is the rule the picture wanted
        /// anyway — you form up when wolves are on you, and you keep the wagons rolling
        /// when arrows are coming out of a treeline.
        /// </summary>
        public const float HaltRadius = 5f;

        /// <summary>
        /// How far a troop will turn to look at something coming for it.
        ///
        /// Facing used to follow reach, so a spearman with two and a half metres of it
        /// stood watching the road while a pack crossed the last twenty metres, and
        /// turned only once the wolves were on top of him. Turning is not striking: a
        /// group faces a threat long before it can touch it.
        ///
        /// Twenty-four metres, because that is just past the widest detect radius on the
        /// table — the archer band's twenty-two. Anything that has woken and is coming
        /// for the column is inside it by the time the turn matters, and anything
        /// further off is a marked position across the field that the whole escort has
        /// no business rotating toward.
        /// </summary>
        public const float WatchRadius = 24f;

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

        /// <summary>True while any enemy is in contact, at any range.</summary>
        public bool InContact { get; private set; }

        /// <summary>
        /// True while something is close enough to stop the column (<see cref="HaltRadius"/>).
        ///
        /// Narrower than <see cref="InContact"/> on purpose: an exchange of arrows is a
        /// fight but not a reason to park the wagons, and treating the two as one thing
        /// is what turned every ranged encounter into a siege neither side could leave.
        /// </summary>
        public bool Halted { get; private set; }

        public int EnemiesEngaged { get; private set; }

        public float HealthOf(TrackedEnemy enemy)
        {
            if (_health.TryGetValue(enemy, out float hp)) return hp;
            return EnemyTable.GroupHp(enemy.Kind) * EnemyStrength;
        }

        public bool IsDefeated(TrackedEnemy enemy) => HealthOf(enemy) <= 0f;

        /// <summary>
        /// How many of the group's animals or men are still standing.
        ///
        /// Read off the pooled health the same way <see cref="TroopGroup.ModelsAlive"/>
        /// is, so a pack that has lost two fifths of its health is drawn three strong.
        /// Rounded up, so the last sliver of a pool is still a live wolf rather than
        /// an empty patch of grass that keeps biting.
        /// </summary>
        public int ModelsAlive(TrackedEnemy enemy)
        {
            int max = EnemyTable.GroupSize(enemy.Kind);

            float perModel = EnemyTable.HpPerModel(enemy.Kind) * EnemyStrength;
            if (perModel <= 0f) return max;

            int models = (int)System.Math.Ceiling(HealthOf(enemy) / perModel);
            if (models < 0) models = 0;
            return models > max ? max : models;
        }

        public void Step(float deltaTime)
        {
            DefeatedThisStep.Clear();
            InContact = false;
            Halted = false;
            EnemiesEngaged = 0;

            var terrain = _caravan.CurrentTerrain;

            MoveAndStrike(deltaTime, terrain);
            TroopsReturnFire(deltaTime, terrain);
            Heal(deltaTime);

            _caravan.SpeedModifier = Halted ? EngagedSpeedFactor : 1f;
        }

        /// <summary>Awake enemies close on the caravan and hit whatever intercepts them.</summary>
        void MoveAndStrike(float deltaTime, TerrainType terrain)
        {
            foreach (var enemy in _detection.Enemies)
            {
                if (!enemy.Awake || IsDefeated(enemy))
                {
                    enemy.Engaging = null;
                    enemy.Striking = false;
                    continue;
                }

                var target = NearestTroopInReach(enemy);
                enemy.Engaging = target;
                enemy.Striking = target != null;

                if (target != null)
                {
                    InContact = true;
                    EnemiesEngaged++;

                    if (Vec2.DistanceSquared(enemy.Position, target.Position) <= HaltRadius * HaltRadius)
                        Halted = true;

                    float damage = EnemyTable.Dps(enemy.Kind) * EnemyStrength * HealthFraction(enemy) * deltaTime;
                    target.TakeDamage(damage);

                    // Any loss while this group is engaged forfeits its clean-kill bonus.
                    if (target.ModelsLost > 0) _bloodied.Add(enemy);
                    continue;
                }

                // Attackers close on the escort, not on the wagons.
                //
                // Making them run at the caravan's centre meant they slipped straight
                // between the six posts — the troops have barely two metres of reach and
                // stand six metres out — so an escorted caravan was mauled exactly as
                // hard as an unescorted one and the whole formation layer did nothing.
                var guard = NearestLivingTroop(enemy);
                if (guard != null)
                {
                    enemy.Engaging = guard;
                    Advance(enemy, guard.Position, deltaTime, terrain);
                    continue;
                }

                // The nearest cart, not the first one.
                //
                // Everything closed on the lead wagon and struck the first undestroyed
                // one in the list, which is the same wagon: an attacker that came out of
                // the trees beside the third cart walked the length of the column to hit
                // the front of it, and the rear of a caravan was the safest place on the
                // map. A column is thirty metres long and the thing nearest a raider is
                // whatever he is standing next to.
                Wagon wagon = null; Vec2 wagonAt = _caravan.LeadPosition;
                foreach (var c in _caravan.Wagons) { if (!c.Destroyed) { wagon = c; break; } }
                if (wagon == null) continue;

                float reachToCaravan = EnemyTable.AttackRange(enemy.Kind) + EngagementSlack;
                if (Vec2.DistanceSquared(enemy.Position, wagonAt) <= reachToCaravan * reachToCaravan)
                {
                    InContact = true;
                    EnemiesEngaged++;
                    enemy.Striking = true;

                    // Nothing is left to intercept it, so closeness to the wagons is
                    // what counts.
                    if (Vec2.DistanceSquared(enemy.Position, wagonAt) <= HaltRadius * HaltRadius)
                        Halted = true;

                    StrikeCaravan(enemy, wagon, deltaTime);
                    continue;
                }

                Advance(enemy, wagonAt, deltaTime, terrain);
            }
        }

        /// <summary>Nearest living troop, at any distance. Null when the escort is gone.</summary>
        TroopGroup NearestLivingTroop(TrackedEnemy enemy)
        {
            TroopGroup found = null;
            float best = float.MaxValue;

            foreach (var group in _squad.Slots)
            {
                if (group == null || !group.Alive) continue;

                float distance = Vec2.DistanceSquared(enemy.Position, group.Position);
                if (distance >= best) continue;

                best = distance;
                found = group;
            }
            return found;
        }

        void Advance(TrackedEnemy enemy, Vec2 goal, float deltaTime, TerrainType terrain)
        {
            var toCaravan = goal - enemy.Position;
            float distance = Vec2.Distance(Vec2.Zero, toCaravan);
            if (distance < 0.0001f) return;

            float speed = EnemyTable.Speed(enemy.Kind) * TileGrid.TileSize * TerrainTable.Speed(terrain);
            float step = speed * deltaTime;
            if (step > distance) step = distance;

            enemy.Position = new Vec2(
                enemy.Position.X + toCaravan.X / distance * step,
                enemy.Position.Y + toCaravan.Y / distance * step);
        }

        /// <summary>The nearest cart still standing, and where it is. Null when all are gone.</summary>
        Wagon NearestWagon(TrackedEnemy enemy, out Vec2 at)
        {
            Wagon found = null;
            at = _caravan.LeadPosition;
            float best = float.MaxValue;

            for (int i = 0; i < _caravan.Wagons.Count; i++)
            {
                var wagon = _caravan.Wagons[i];
                if (wagon.Destroyed) continue;

                var position = _caravan.WagonPosition(i);
                float distance = Vec2.DistanceSquared(enemy.Position, position);
                if (distance >= best) continue;

                best = distance;
                found = wagon;
                at = position;
            }

            return found;
        }

        void StrikeCaravan(TrackedEnemy enemy, Wagon nearest, float deltaTime)
        {
            float damage = EnemyTable.Dps(enemy.Kind) * EnemyStrength * HealthFraction(enemy) * deltaTime;

            // Bandits go for the treasure; everything else hits whatever it reached.
            var wagon = enemy.Kind == EnemyKind.Bandit
                ? _caravan[WagonKind.Treasure]
                : null;

            if (wagon == null || wagon.Destroyed) wagon = nearest;

            wagon?.ApplyDamage(damage);
        }

        /// <summary>Troops strike the nearest revealed enemy they can both see and reach.</summary>
        void TroopsReturnFire(float deltaTime, TerrainType terrain)
        {
            float squadSight = _squad.BestSight;

            foreach (var group in _squad.Slots)
            {
                if (group == null || !group.Alive) continue;

                // Cleared up front, so a group that finds nothing this step stops
                // swinging rather than keeping last step's opponent for ever.
                group.Target = null;

                // Worked out before the damage check, so a priest turns to face the pack
                // that is about to reach her even though she will never hit it.
                group.Threat = NearestThreat(group);

                float dps = group.DamageAgainst(EnemyKind.Wolf, terrain);
                if (dps <= 0f) continue;

                // The same slack the enemy closes to. Without it an attacker halting at
                // its own reach plus slack stood 3.5 metres away while a swordsman with
                // 1.8 metres of reach swung at nothing — the escort died without ever
                // landing a blow, which read as combat being far too lethal rather than
                // as the melee ranges simply not meeting.
                float reach = TroopUpgrades.UsableRange(group.AttackRange(terrain), squadSight)
                              + EngagementSlack;

                // A formation fights what is fighting it. The bows fight everything.
                //
                // Every group used to strike the nearest thing in reach, which made the
                // six posts a single weapon that happened to be drawn in six places:
                // where the player put the swordsmen changed nothing, because whatever
                // came at the left flank was shot at by the right one too. Now a
                // man-at-arms swings at what has closed on *him* — so a pack that comes
                // out of the trees on the left meets the left flank and only the left
                // flank, and the choice of who stands where is the choice it looks like.
                //
                // Archers and the mage are the exception, and are the reason the rule
                // reads as tactics rather than as a handicap: a bow does not need to be
                // attacked to be useful, so they answer anything inside their radius
                // wherever it is on the column. That is what buying reach buys.
                var target = NearestEnemyInReach(group, reach);
                group.Target = target;
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

            ForgetTheDead();
        }

        /// <summary>
        /// Drops every reference to a group that died during this step.
        ///
        /// The two halves of a step disagree about the order things happen in: attackers
        /// pick their quarry before the escort swings, so a pack destroyed by the return
        /// fire was still on record as chasing a spearman, and every troop that had
        /// turned toward it went on facing a patch of empty grass. Harmless to the
        /// arithmetic and very visible on the screen, which is the kind of bug the
        /// simulation is supposed to keep out of the view rather than hand to it.
        /// </summary>
        void ForgetTheDead()
        {
            foreach (var group in _squad.Slots)
            {
                if (group == null) continue;

                if (group.Target != null && IsDefeated(group.Target)) group.Target = null;
                if (group.Threat != null && IsDefeated(group.Threat)) group.Threat = null;
            }

            foreach (var enemy in _detection.Enemies)
            {
                // Both directions: a group destroyed by the return fire, and a troop
                // killed by the group's own blow earlier in the same step. The second
                // one left an attacker on record as closing on a post that no longer
                // had anybody standing at it.
                if (IsDefeated(enemy) || (enemy.Engaging != null && !enemy.Engaging.Alive))
                {
                    enemy.Engaging = null;
                    enemy.Striking = false;
                }
            }
        }

        /// <summary>Healing the supply wagon provides to the whole escort between fights.</summary>
        public const float SupplyHealPerSecond = 8f;

        void Heal(float deltaTime)
        {
            // The supply wagon patches the escort up between fights (docs/GDD.md §5).
            //
            // "Between fights" now means while the column is rolling, not while nothing
            // at all is in contact. That is the same rule it was written as — the two
            // were one thing until halting was narrowed to melee — and it matters:
            // without it a column under long-range fire could neither disengage nor
            // recover. Restoring it took chapter 1 from twenty survivable routes out of
            // thirty to twenty-four.
            //
            // Without this, damage to troops was permanent for the length of a level and
            // simply accumulated: the escort held comfortably through 1-4, was down to a
            // fifth by 1-5 and was wiped outright from 1-7 on. It also meant losing the
            // supply wagon cost the player nothing whatsoever, which is not what a wagon
            // whose entire purpose is healing ought to be worth.
            var supply = _caravan[WagonKind.Supply];
            if (!Halted && supply != null && !supply.Destroyed)
            {
                foreach (var group in _squad.Slots)
                    group?.Heal(SupplyHealPerSecond * deltaTime);
            }

            foreach (var healer in _squad.Slots)
            {
                if (healer == null || !healer.Alive) continue;

                float rate = TroopTable.HealPerSecond(healer.Kind);
                if (rate <= 0f) continue;

                // Out of contact a priest works three times as fast (docs/GDD.md §4.3).
                if (!Halted) rate *= 3f;

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

        /// <summary>
        /// The troop this enemy has closed on, if it has closed on one.
        ///
        /// The enemy's own reach with no slack added, and that is the whole point. The
        /// slack exists so the two sides' ranges overlap, and adding it to both put them
        /// back out of touch by a hair: a wolf halted at two metres plus one and a half,
        /// while a swordsman's one metre eighty plus the same one and a half reaches
        /// three point three. Two tenths of a metre short, every time, so the wolf bit a
        /// swordsman who could not bite back — which on screen is a figure swinging at
        /// air, and in the numbers is a squad in contact with nothing to fight.
        /// </summary>
        TroopGroup NearestTroopInReach(TrackedEnemy enemy)
        {
            float reach = EnemyTable.AttackRange(enemy.Kind);
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

        /// <summary>
        /// The nearest woken, visible enemy inside <see cref="WatchRadius"/>.
        ///
        /// Awake and not merely revealed: a pack the scout has spotted but that has not
        /// noticed the column is something to sneak past, and having the whole escort
        /// swivel to stare at it would give away that the player had seen it.
        /// </summary>
        TrackedEnemy NearestThreat(TroopGroup group)
        {
            float best = WatchRadius * WatchRadius;
            TrackedEnemy found = null;

            foreach (var enemy in _detection.Enemies)
            {
                if (!enemy.Revealed || !enemy.Awake || IsDefeated(enemy)) continue;

                float distance = Vec2.DistanceSquared(enemy.Position, group.Position);
                if (distance > best) continue;

                best = distance;
                found = enemy;
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
