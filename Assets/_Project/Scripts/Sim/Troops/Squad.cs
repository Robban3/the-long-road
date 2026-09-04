using System;
using System.Collections.Generic;

namespace TheVail.Sim
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

        /// <summary>
        /// What gold has bought on this troop type, permanently.
        ///
        /// Beside the levels above rather than added to them: the field tracks keep their
        /// cap of five and their flat multipliers, and this is a second and gentler one
        /// on top. See TroopBoonTable for why folding the two together would have ended
        /// the silver economy and the game with it.
        /// </summary>
        public TroopBoons School { get; }

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

        /// <param name="school">
        /// What gold has bought on this troop type, or null for the table's own numbers.
        ///
        /// Taken here rather than set afterwards because the health it buys has to be in
        /// the group *before* the first blow lands. Set later, permanent armour would
        /// only raise the ceiling a priest may heal to — a group would still march out at
        /// its base health, and an upgrade the player paid gold for would do nothing all
        /// level unless somebody healed them.
        /// </param>
        public TroopGroup(TroopKind kind, FormationSlot slot, TroopBoons school = null)
        {
            Kind = kind;
            Slot = slot;
            School = school ?? new TroopBoons();
            MaxHp = TroopTable.GroupHp(kind);
            Hp = EffectiveMaxHp;
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

        public float EffectiveMaxHp =>
            MaxHp * TroopUpgrades.ArmourHpMultiplier(ArmourLevel) * School.ArmourHealth(Kind);

        /// <summary>Damage per second, upgrades and terrain included.</summary>
        public float DamageAgainst(EnemyKind target, TerrainType terrain)
        {
            float baseDps = TroopTable.Dps(Kind) * (ModelsAlive / (float)TroopTable.Models(Kind));
            return baseDps
                   * TroopUpgrades.WeaponMultiplier(WeaponLevel)
                   * School.Weapon(Kind)
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
                range = TroopUpgrades.EffectiveRange(range, SpecialLevel) * School.Range(Kind);

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
                              + TroopUpgrades.ArmourDamageReduction(ArmourLevel)
                              + School.ArmourReduction(Kind);
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

        /// <summary>
        /// How far ahead of the van the scout walks.
        ///
        /// Fourteen metres, so about twenty-four ahead of the lead wagon, and the number
        /// is a compromise between the two things that can go wrong. Too short and this
        /// changes nothing: her thirty-four metres of sight already reached that far from
        /// the formation, which is exactly the complaint — a scout who does not scout is a
        /// sight statistic with a name on it. Too long and she meets each group alone,
        /// which is the failure recorded under <see cref="VanLead"/>: the van at twelve
        /// metres instead of ten lost 1-6 on every route for that reason, and the scout
        /// has two models at sixty hit points where the van has four at a hundred and
        /// eighty.
        ///
        /// She is also called in the moment blades are out — see <see cref="Scouting"/> —
        /// so what this buys is warning before contact and nothing during it.
        /// </summary>
        public const float ScoutLead = 14f;

        /// <summary>
        /// Whether the scout is out ahead of the column or back in the ranks.
        ///
        /// Set false while the column is in contact. A scout standing fourteen metres in
        /// front of a charge is not scouting, she is the first thing it reaches — and she
        /// is the one troop in the game that cannot afford to be.
        /// </summary>
        public bool Scouting = true;

        /// <summary>
        /// Whether the scout ranges ahead at all.
        ///
        /// Off puts her back in the ranks with everybody else, which is what she did
        /// before, and exists so the two can be run against each other over a whole
        /// chapter rather than argued about. A change to where a troop stands is a
        /// balance change, and this project has a rule about those: measure it.
        /// </summary>
        public bool ScoutsAhead = true;

        readonly TroopGroup[] _slots = new TroopGroup[TroopTable.LinePosts + 1];

        public int Budget { get; }

        /// <summary>
        /// How many of the six posts of the line are open.
        ///
        /// The formation used to be six posts from the first level, and the player could
        /// never pay for six: the cheapest six troops in the game cost twenty and the
        /// budget runs from twelve to eighteen across a chapter, so a full line was not
        /// merely hard to afford, it was arithmetically impossible. What the player saw
        /// was six sockets of which three or four could ever be filled — which reads as
        /// something broken rather than as a choice.
        ///
        /// So the line grows instead, from three posts to six across the chapter, roughly
        /// in step with the budget that has to fill them. An empty post is now an empty
        /// post because you spent your points elsewhere.
        ///
        /// The scouting post is not one of these and is always open. She costs two, she
        /// stands outside the line, and she is the cheapest thing in the game to bring.
        /// </summary>
        public int Posts { get; }

        /// <summary>
        /// The permanent troop levels this escort was raised with. Empty by default, so a
        /// squad built by a test or by the headless capture fights with the table's own
        /// numbers.
        /// </summary>
        public TroopBoons School { get; set; } = new TroopBoons();

        public Squad(int budget = 12, int posts = TroopTable.LinePosts)
        {
            Budget = budget;
            Posts = posts < 1 ? 1 : (posts > TroopTable.LinePosts ? TroopTable.LinePosts : posts);
        }

        /// <summary>Whether this post is open at this point in the campaign.</summary>
        public bool Open(FormationSlot slot)
        {
            if (slot == FormationSlot.Scouting) return true;

            for (int i = 0; i < Posts && i < TroopTable.Line.Length; i++)
                if (TroopTable.Line[i] == slot) return true;

            return false;
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

        /// <summary>
        /// Places a troop if the post is open and free, the kind belongs there, and the
        /// budget allows.
        ///
        /// A scout goes in the scouting post and nowhere else, and nothing else goes in
        /// it. Two rules rather than one, because both halves have been wrong: she used
        /// to take a place in the line she does not stand in, and letting anything else
        /// take the post out in front would put a shieldbearer fourteen metres ahead of
        /// the van with the sight of a man looking at his own boots.
        /// </summary>
        public bool TryPlace(FormationSlot slot, TroopKind kind)
        {
            if (!Open(slot)) return false;
            if (_slots[(int)slot] != null) return false;
            if (TroopTable.Scouts(kind) != (slot == FormationSlot.Scouting)) return false;
            if (TroopTable.Cost(kind) > PointsRemaining) return false;

            _slots[(int)slot] = new TroopGroup(kind, slot, School);
            return true;
        }

        /// <summary>The post a troop of this kind would go in, given what is free.</summary>
        public bool TryPlace(TroopKind kind)
        {
            if (TroopTable.Scouts(kind)) return TryPlace(FormationSlot.Scouting, kind);

            for (int i = 0; i < Posts && i < TroopTable.Line.Length; i++)
                if (_slots[(int)TroopTable.Line[i]] == null)
                    return TryPlace(TroopTable.Line[i], kind);

            return false;
        }

        public bool Remove(FormationSlot slot)
        {
            if (_slots[(int)slot] == null) return false;
            _slots[(int)slot] = null;
            return true;
        }

        /// <summary>
        /// Swaps two posts. The Regroup order, which costs three seconds in play.
        ///
        /// Refused between the line and the scouting post: that is not a regroup, it is
        /// sending the scout to hold a corner and a swordsman out to scout.
        /// </summary>
        public bool CanSwap(FormationSlot a, FormationSlot b)
            => Open(a) && Open(b)
               && (a == FormationSlot.Scouting) == (b == FormationSlot.Scouting);

        public void Swap(FormationSlot a, FormationSlot b)
        {
            if (!CanSwap(a, b)) return;

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
        /// and the rearguard follows behind.
        ///
        /// **And it stretches to the column's real length, which once lost the game.**
        /// The pairs stood five metres apart for a while, deliberately far less than the
        /// thirty the column spans, because posting the rear pair back beside the third
        /// wagon lost 1-6 on *every* route: six troops strung over forty-five metres
        /// cannot support each other, so a pack that found the rearguard fought it two
        /// against five while the van was half a level away. Ten still lost it. The
        /// escort was a fighting unit before it was a cordon.
        ///
        /// That reasoning is gone, and not because the arithmetic changed. Mutual support
        /// is what the tight formation bought, and a troop only strikes what is attacking
        /// *it* now — see CombatSystem.TroopsReturnFire — so the neighbour who used to
        /// come to the rescue no longer does at any spacing. What the tight formation
        /// cost was two wagons of three walking unguarded while everything converged on
        /// the first, which is what a player watching the rear cart go untouched
        /// actually sees. Given the choice between a cordon that does not support itself
        /// and a huddle that does not cover the wagons, cover the wagons.
        /// </summary>

        /// <summary>
        /// Where each post stands, given how long the column is.
        ///
        /// The offsets used to be measured from the lead wagon, and with three carts
        /// fifteen metres apart that put the whole escort around the first one: two
        /// wagons of the three walked unguarded, and since attackers close on the nearest
        /// troop they all converged on the front of the column. It looked like the rear
        /// wagons could not be attacked. They could — nothing was ever near them.
        ///
        /// So the van walks ahead of the first cart, the rearguard behind the last, and
        /// the flanks stand beside the column at a quarter of its length either side of
        /// the middle, which puts a guard within reach of every wagon there is.
        /// </summary>
        /// <summary>
        /// Where this group stands: its formation post, or out in front if it is the scout.
        ///
        /// By kind and not by slot, which is the whole change. The posts are a formation —
        /// van, flanks, rear — and the scout was given one of them like anybody else, so
        /// what she did with the best eyes in the army was look at the same ground the
        /// column was already standing on. Worse, put in the rear slot she watched the
        /// road behind a caravan that was travelling forwards.
        ///
        /// Waking is measured from the caravan and not from the watchers (see
        /// DetectionSystem.Scan), so a scout out in front reveals without waking. That is
        /// not a loophole, it is the information economy the design rests on: what she
        /// buys is knowing what is ahead before you are committed to it.
        /// </summary>
        Vec2 PostFor(TroopGroup group, int slot, float half)
        {
            if (group != null && group.Kind == TroopKind.Scout && Scouting && ScoutsAhead)
                return new Vec2(half + VanLead + ScoutLead, 0f);

            return PostAt(slot, half);
        }

        static Vec2 PostAt(int slot, float half)
        {
            switch (slot)
            {
                case 0: return new Vec2(half + VanLead, 0f);              // Van
                case 1: return new Vec2(half * 0.5f, FlankOffset);        // RightVan
                case 2: return new Vec2(-half * 0.5f, FlankOffset);       // RightRear
                case 3: return new Vec2(-half - RearTrail, 0f);           // Rear
                case 4: return new Vec2(-half * 0.5f, -FlankOffset);      // LeftVan's mirror
                case 5: return new Vec2(half * 0.5f, -FlankOffset);       // LeftVan

                // The scouting post, when she is not scouting. Called back into the ranks
                // she takes the left of the van, which is where she used to be posted
                // before she was given a place of her own.
                default: return new Vec2(half * 0.5f, -FlankOffset);
            }
        }

        /// <summary>
        /// Puts every post where the column is, following the road rather than a ruler.
        ///
        /// The van walks twenty-five metres ahead of the column's centre and the road
        /// bends inside that distance. Measured along a straight heading the van ends up
        /// off the verge on every corner — fourteen metres from the nearest wagon where
        /// it should be ten — and the formation swings wide through every turn like a
        /// trailer. Walking the path for the along-column part and using the heading only
        /// for the step out to the side keeps the escort on the road.
        /// </summary>
        /// <summary>
        /// What the escort has to walk round, when the view has told the run what it put
        /// on the ground. Null on a headless run, and then posts are taken as drawn.
        /// </summary>
        public ObstacleField Obstacles;

        /// <summary>
        /// How much room a troop group needs. Half the four-metre tile: a group is a
        /// handful of men, not a point, and letting them stand with a shoulder inside a
        /// trunk is the artefact this is here to remove.
        /// </summary>
        public const float TroopRadius = 1.1f;

        public void UpdatePositions(Caravan caravan)
        {
            if (caravan == null) return;

            float centre = caravan.LeadDistance - caravan.ColumnHalfLength;
            var heading = caravan.Heading;

            float hx = heading.X, hy = heading.Y;
            float length = (float)Math.Sqrt(hx * hx + hy * hy);
            if (length < 0.0001f) { hx = 1f; hy = 0f; }
            else { hx /= length; hy /= length; }

            float rx = hy, ry = -hx;

            for (int i = 0; i < _slots.Length; i++)
            {
                var group = _slots[i];
                if (group == null) continue;

                var post = PostFor(group, i, caravan.ColumnHalfLength);
                var anchor = caravan.PositionAt(centre + post.X);

                var wanted = new Vec2(anchor.X + rx * post.Y, anchor.Y + ry * post.Y);

                // Round what is standing there rather than through it. The post itself is
                // unchanged — the group is put at the nearest clear spot to it, and drops
                // back onto the post as soon as the tree is behind them.
                group.Position = Obstacles == null ? wanted : Obstacles.Clear(wanted, TroopRadius);
            }
        }

        /// <param name="half">
        /// Half the column's length. Zero puts every post around a single point, which is
        /// what the formation was before the wagons were strung out.
        /// </param>
        public void UpdatePositions(Vec2 centre, Vec2 heading, float half = 0f)
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

                var post = PostFor(group, i, half);

                var wanted = new Vec2(
                    centre.X + hx * post.X + rx * post.Y,
                    centre.Y + hy * post.X + ry * post.Y);

                group.Position = Obstacles == null ? wanted : Obstacles.Clear(wanted, TroopRadius);
            }
        }
    }
}
