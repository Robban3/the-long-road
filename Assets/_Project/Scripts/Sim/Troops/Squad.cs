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

        /// <summary>Whether this group in particular is fighting, not merely the squad.</summary>
        public bool Engaged => Target != null;

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
        /// <summary>Metres from the caravan's centre to a post.</summary>
        public const float FormationRadius = 6f;

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
        public void UpdatePositions(Vec2 centre, Vec2 heading)
        {
            float hx = heading.X, hy = heading.Y;
            float length = (float)Math.Sqrt(hx * hx + hy * hy);
            if (length < 0.0001f) { hx = 1f; hy = 0f; }
            else { hx /= length; hy /= length; }

            for (int i = 0; i < _slots.Length; i++)
            {
                var group = _slots[i];
                if (group == null) continue;

                // Slot 0 is dead ahead, then every 60 degrees clockwise.
                double angle = i * Math.PI / 3.0;
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);

                float ox = hx * cos - hy * sin;
                float oy = hx * sin + hy * cos;

                group.Position = new Vec2(centre.X + ox * FormationRadius, centre.Y + oy * FormationRadius);
            }
        }
    }
}
