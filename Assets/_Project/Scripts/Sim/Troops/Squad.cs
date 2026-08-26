using System;
using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>
    /// One troop group holding one post around the caravan.
    ///
    /// Health is pooled across the group and models die off it. Groups rather than
    /// individuals because they render far cheaper, read better on a phone screen,
    /// and because one wolf is not a threat while a pack is.
    /// </summary>
    public sealed class TroopGroup
    {
        public readonly TroopKind Kind;
        public readonly FormationSlot Slot;
        public readonly float MaxHp;

        public float Hp;
        public Vec2 Position;

        public int WeaponLevel;
        public int ArmourLevel;
        public int SpecialLevel;

        /// <summary>Models lost this level. Drives the flawless-victory bonus.</summary>
        public int ModelsLost { get; private set; }

        /// <summary>
        /// What this group is fighting right now, or null.
        ///
        /// The combat step already worked this out and threw it away, and the view was
        /// left with one fact for the whole escort: somebody is in contact. So every
        /// troop swung at once, most of them at nothing, all of them still facing the
        /// way the road went. A fight that reads as a fight needs each figure turned to
        /// its own opponent and swinging only when it has one.
        /// </summary>
        public TrackedEnemy Target;

        /// <summary>
        /// The nearest thing coming for this group, whether or not it can be hit yet.
        ///
        /// Separate from <see cref="Target"/> because turning and striking are different
        /// questions with different answers. A spearman reaches two and a half metres
        /// and a pack crosses twenty before it gets there; tying the turn to the reach
        /// left the escort watching the road until the wolves were already on it.
        /// </summary>
        public TrackedEnemy Threat;

        /// <summary>Whether this group in particular is fighting, not merely the squad.</summary>
        public bool Engaged => Target != null;

        /// <summary>
        /// What this group should be looking at: what it is hitting, or failing that
        /// what is coming for it. Null means nothing is, and it faces the road.
        /// </summary>
        public TrackedEnemy Watching => Target ?? Threat;

        public TroopGroup(TroopKind kind, FormationSlot slot)
        {
            Kind = kind;
            Slot = slot;
            MaxHp = TroopTable.GroupHp(kind);
            Hp = MaxHp;
        }

        public bool Alive => Hp > 0f;

        public int ModelsAlive
        {
            get
            {
                if (Hp <= 0f) return 0;
                int models = (int)Math.Ceiling(Hp / TroopTable.HpPerModel(Kind));
                int max = TroopTable.Models(Kind);
                return models > max ? max : models;
            }
        }

        public float EffectiveMaxHp => MaxHp * TroopUpgrades.ArmourHpMultiplier(ArmourLevel);

        /// <summary>Damage per second, upgrades and terrain included.</summary>
        public float DamageAgainst(EnemyKind target, TerrainType terrain)
        {
            float baseDps = TroopTable.Dps(Kind) * (ModelsAlive / (float)TroopTable.Models(Kind));
            return baseDps
                   * TroopUpgrades.WeaponMultiplier(WeaponLevel)
                   * TroopTable.TerrainDamageMultiplier(Kind, terrain)
                   * TroopTable.DamageMultiplierAgainst(Kind, target);
        }

        /// <summary>
        /// Reach in this terrain, before sight is taken into account. Range upgrades
        /// only apply to troops whose speciality is reach.
        /// </summary>
        public float AttackRange(TerrainType terrain)
        {
            float range = TroopTable.Range(Kind);
            if (TroopTable.HasRangedSpecial(Kind))
                range = TroopUpgrades.EffectiveRange(range, SpecialLevel);
            return range * TroopTable.TerrainRangeMultiplier(Kind, terrain);
        }

        public float SightRadius
        {
            get
            {
                float sight = TroopTable.Sight(Kind);
                if (Kind == TroopKind.Scout) sight += 6f * SpecialLevel;
                return sight;
            }
        }

        /// <summary>Applies damage after the group's own reduction and armour upgrades.</summary>
        public float TakeDamage(float amount)
        {
            if (amount <= 0f || !Alive) return 0f;

            float reduction = TroopTable.DamageReduction(Kind)
                              + TroopUpgrades.ArmourDamageReduction(ArmourLevel);
            if (reduction > 0.8f) reduction = 0.8f;

            int modelsBefore = ModelsAlive;
            float dealt = amount * (1f - reduction);
            if (dealt > Hp) dealt = Hp;

            Hp -= dealt;
            ModelsLost += modelsBefore - ModelsAlive;
            return dealt;
        }

        public void Heal(float amount)
        {
            if (!Alive || amount <= 0f) return;
            Hp += amount;
            float cap = EffectiveMaxHp;
            if (Hp > cap) Hp = cap;
        }

        public int UpgradeLevel(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Weapon: return WeaponLevel;
                case UpgradeTrack.Armour: return ArmourLevel;
                default: return SpecialLevel;
            }
        }

        public void RaiseLevel(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Weapon: WeaponLevel++; break;
                case UpgradeTrack.Armour: ArmourLevel++; break;
                default: SpecialLevel++; break;
            }
        }

        /// <summary>Cost multiplier for a track on this troop. Reach is dearer.</summary>
        public float CostMultiplier(UpgradeTrack track)
            => TroopUpgrades.CostMultiplier(track, TroopTable.HasRangedSpecial(Kind));
    }

    /// <summary>
    /// The six posts around the caravan and who holds them (docs/GDD.md §4.2).
    ///
    /// The point of six slots is that placement matters: the van meets everything
    /// head-on and walks into the traps, the rear meets whatever comes from behind,
    /// and the flanks are where a bow has room to work.
    /// </summary>
    public sealed class Squad
    {
        /// <summary>
        /// Metres from the caravan's centre to a post, and the yardstick the pack
        /// formations are measured against.
        /// </summary>
        public const float FormationRadius = 6f;

        /// <summary>
        /// How far to the side of the column a flank post stands.
        ///
        /// Six. A cart is about two and a half metres across, so this leaves nearly five
        /// metres of clear ground between a troop and the wheel beside it — a fight on
        /// the flank is a fight beside the caravan rather than inside it.
        /// </summary>
        public const float FlankOffset = 6f;

        /// <summary>
        /// How far ahead of the lead wagon the van walks.
        ///
        /// Ten metres, and the number is the horses. The lead team's noses reach about
        /// eight metres in front of the cart it pulls, and the van used to stand at six —
        /// **between the horses and the wagon**, which is where a driver would least like
        /// to find his own escort. Ten clears the animals with a little air and no more:
        /// twelve was tried and cost the escort 1-6 on every route, because a point that
        /// far out meets each group alone.
        /// </summary>
        public const float VanLead = 10f;

        /// <summary>How far behind the last cart's tail the rearguard walks.</summary>
        public const float RearTrail = 5f;

        /// <summary>
        /// The furthest any post stands from the nearest wagon.
        ///
        /// Not a setting — the largest of the offsets below, kept as one number so that
        /// "a troop wandered off" can be asserted against something that follows the
        /// formation when the formation changes.
        /// </summary>
        public const float FormationSpan = VanLead + 1f;

        /// <summary>How far a troop will chase before returning to its post.</summary>
        public const float Leash = 10f;

        readonly TroopGroup[] _slots = new TroopGroup[6];

        public int Budget { get; }

        public Squad(int budget = 12)
        {
            Budget = budget;
        }

        public IReadOnlyList<TroopGroup> Slots => _slots;

        public int PointsSpent
        {
            get
            {
                int total = 0;
                foreach (var group in _slots) if (group != null) total += TroopTable.Cost(group.Kind);
                return total;
            }
        }

        public int PointsRemaining => Budget - PointsSpent;

        public int Count
        {
            get { int n = 0; foreach (var g in _slots) if (g != null) n++; return n; }
        }

        public bool AnyAlive
        {
            get { foreach (var g in _slots) if (g != null && g.Alive) return true; return false; }
        }

        public TroopGroup this[FormationSlot slot] => _slots[(int)slot];

        /// <summary>Places a troop if the post is free and the budget allows.</summary>
        public bool TryPlace(FormationSlot slot, TroopKind kind)
        {
            if (_slots[(int)slot] != null) return false;
            if (TroopTable.Cost(kind) > PointsRemaining) return false;

            _slots[(int)slot] = new TroopGroup(kind, slot);
            return true;
        }

        public bool Remove(FormationSlot slot)
        {
            if (_slots[(int)slot] == null) return false;
            _slots[(int)slot] = null;
            return true;
        }

        /// <summary>Swaps two posts. The Regroup order, which costs three seconds in play.</summary>
        public void Swap(FormationSlot a, FormationSlot b)
        {
            var temp = _slots[(int)a];
            _slots[(int)a] = _slots[(int)b];
            _slots[(int)b] = temp;
        }

        /// <summary>Widest sight in the column. What the fog of war is measured against.</summary>
        public float BestSight
        {
            get
            {
                float best = 0f;
                foreach (var group in _slots)
                    if (group != null && group.Alive && group.SightRadius > best) best = group.SightRadius;
                return best;
            }
        }

        /// <summary>Best trap-spotting range in the column.</summary>
        public float BestTrapSight
        {
            get
            {
                float best = 0f;
                foreach (var group in _slots)
                {
                    if (group == null || !group.Alive) continue;
                    float sight = TroopTable.TrapSight(group.Kind);
                    if (sight > best) best = sight;
                }
                return best;
            }
        }

        public bool HasEngineer
        {
            get
            {
                foreach (var group in _slots)
                    if (group != null && group.Alive && TroopTable.CanDisarmTraps(group.Kind)) return true;
                return false;
            }
        }

        /// <summary>
        /// Moves every post to its place around the caravan, rotated to face the way
        /// the column is travelling — so the van is always the front, whichever
        /// direction the route happens to run.
        /// </summary>
        /// <summary>
        /// Where each post stands, along the column and to the side of it.
        ///
        /// Indexed by <see cref="FormationSlot"/>: Van, RightVan, RightRear, Rear,
        /// LeftRear, LeftVan. X is metres along the direction of travel, Y is metres to
        /// the right of it.
        ///
        /// **The posts used to be six points on a six-metre circle**, evenly spaced at
        /// sixty degrees. That was right when a caravan was one wagon and became wrong
        /// without anyone changing it: slot 0, dead ahead at six metres, ended up
        /// standing **between the lead horses and the cart they pull** once the teams
        /// were hitched, and the escort walked through the traces.
        ///
        /// The circle is a rectangle now. The van walks ahead of the horses, the two
        /// flank pairs stand out to the side where left and right mean left and right,
        /// and the rearguard follows behind. It is still a tight arrangement — see
        /// <see cref="PairSpacing"/> for why it does not stretch to the column's real
        /// length.
        /// </summary>
        /// <summary>
        /// How far apart the van pair and the rear pair stand along the column.
        ///
        /// Five metres, which is deliberately far less than the thirty the column spans.
        ///
        /// Posting the rear pair back beside the third wagon is the truthful arrangement
        /// and it lost 1-6 on **every** route: six troops strung over forty-five metres
        /// cannot support each other, so a pack that finds the rearguard fights it two
        /// against five while the van is half a level away. Ten still lost it. **The
        /// escort is a fighting unit before it is a cordon**, and the spacing is what the
        /// combat can carry rather than what the column measures.
        ///
        /// What this arrangement does buy is the thing that was actually wrong: nobody
        /// stands between a wagon and the horses pulling it, and left and right mean left
        /// and right.
        /// </summary>
        const float PairSpacing = 5f;

        static readonly Vec2[] Posts =
        {
            new Vec2(VanLead, 0f),                            // Van
            new Vec2(0f, FlankOffset),                        // RightVan
            new Vec2(-PairSpacing, FlankOffset),              // RightRear
            new Vec2(-PairSpacing - RearTrail, 0f),           // Rear
            new Vec2(-PairSpacing, -FlankOffset),             // LeftRear
            new Vec2(0f, -FlankOffset)                        // LeftVan
        };

        public void UpdatePositions(Vec2 centre, Vec2 heading)
        {
            float hx = heading.X, hy = heading.Y;
            float length = (float)Math.Sqrt(hx * hx + hy * hy);
            if (length < 0.0001f) { hx = 1f; hy = 0f; }
            else { hx /= length; hy /= length; }

            // Right of the heading. Checked against the case that matters rather than
            // reasoned about: facing +Z, right must come out +X — (0,1) gives (1,0).
            float rx = hy, ry = -hx;

            for (int i = 0; i < _slots.Length; i++)
            {
                var group = _slots[i];
                if (group == null) continue;

                var post = Posts[i];

                group.Position = new Vec2(
                    centre.X + hx * post.X + rx * post.Y,
                    centre.Y + hy * post.X + ry * post.Y);
            }
        }
    }
}
