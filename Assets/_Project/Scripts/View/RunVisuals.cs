using System.Collections.Generic;
using Arna.Sim;
using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// Draws whatever the simulation is doing.
    ///
    /// Every marker goes through <see cref="Spawn"/>, which falls back to a coloured
    /// primitive when no model is supplied. That fallback is why the entire simulation
    /// could be built and judged before any art existed, and why a missing pack
    /// degrades the picture instead of breaking the game.
    /// </summary>
    public sealed class RunVisuals
    {
        public VisualLibrary Library = new VisualLibrary();

        readonly Transform _root;
        readonly TileGrid _grid;
        readonly float _heightScale;
        readonly List<Transform> _wagons = new List<Transform>();

        /// <summary>One per wagon, in the same order, turning that wagon's wheels.</summary>
        readonly List<WagonWheels> _wheels = new List<WagonWheels>();

        /// <summary>
        /// Where each wagon stood last frame, or nothing on the frame it was built.
        ///
        /// The wheels are turned by distance covered, and distance covered is the one
        /// thing the simulation does not hand over: it gives positions. So it is
        /// measured here, from one frame to the next, which also means a wagon halted
        /// in a fight has wheels that are genuinely still rather than idling.
        /// </summary>
        readonly List<Vector3?> _wagonWere = new List<Vector3?>();

        /// <summary>Every horse in harness, across all the wagons, for animating.</summary>
        readonly List<Transform> _draught = new List<Transform>();
        /// <summary>
        /// One entry per group, and inside it one figure per model the group can field.
        ///
        /// A group is a pooled health bar in the simulation and was a single figure on
        /// the screen, which is where "a wolf pack attacked" turned into one wolf
        /// standing on a hillside. The list is built to the group's full complement and
        /// never resized: figures beyond the survivors are switched off, so a model
        /// keeps its place in the formation as the group is whittled down.
        /// </summary>
        /// <summary>One reach ring per troop that has a reach worth drawing. See DrawReach.</summary>
        readonly Dictionary<TroopGroup, RangeRing> _rings =
            new Dictionary<TroopGroup, RangeRing>();
        readonly Dictionary<TroopGroup, float> _reload = new Dictionary<TroopGroup, float>();
        Volley _volley;
        Material _ringMaterial;

        readonly Dictionary<TroopGroup, List<Transform>> _troops =
            new Dictionary<TroopGroup, List<Transform>>();

        readonly Dictionary<TrackedEnemy, List<Transform>> _enemies =
            new Dictionary<TrackedEnemy, List<Transform>>();
        readonly Dictionary<TrackedTrap, Transform> _traps = new Dictionary<TrackedTrap, Transform>();
        readonly Dictionary<WildAnimal, Transform> _wildlife = new Dictionary<WildAnimal, Transform>();
        readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        readonly Dictionary<Transform, Animator> _animators = new Dictionary<Transform, Animator>();

        /// <summary>Models already complained about, so a warning is said once and not per figure.</summary>
        readonly HashSet<string> _warned = new HashSet<string>();

        /// <summary>How far each model's origin sits above its own feet, after scaling.</summary>
        readonly Dictionary<Transform, float> _standing = new Dictionary<Transform, float>();

        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int AttackParam = Animator.StringToHash("Attack");
        static readonly int DeadParam = Animator.StringToHash("Dead");

        /// <summary>
        /// Drives one actor's animator from what the simulation says it is doing.
        /// Speed comes from the simulation rather than from measured movement, so it
        /// is correct in a headless capture where there are no frames to measure with.
        /// </summary>
        void Animate(Transform marker, float speed, bool attacking, bool dead)
        {
            if (marker == null || !_animators.TryGetValue(marker, out var animator)) return;
            if (animator == null) return;

            animator.SetFloat(SpeedParam, speed);
            animator.SetBool(AttackParam, attacking);
            animator.SetBool(DeadParam, dead);
        }

        public RunVisuals(Transform root, TileGrid grid = null, float heightScale = 0f)
        {
            _root = root;
            _grid = grid;
            _heightScale = heightScale;
        }

        /// <summary>
        /// Ground height under a world position. Everything that moves is placed on
        /// the terrain rather than on the plane it used to be flat on, or the caravan
        /// drives through the hills instead of over them.
        /// </summary>
        /// <summary>
        /// The height anything standing here should be at — the ground, or the bridge
        /// over it.
        ///
        /// The caravan drove *under* the bridge, twice, because the ground is what
        /// everything here takes its height from and a bridge is not the ground. Levelling
        /// the ford fixed the crossing sitting in the bottom of the channel and did not
        /// fix this: an arch still rises over a level bank, and the column went along the
        /// bank and through the vault.
        ///
        /// So the bridges are asked. They are found once, when the world is built, and
        /// each one answers with a ray rather than with a rule, because nothing here
        /// knows what the model looks like.
        /// </summary>
        float GroundAt(Vec2 position)
        {
            if (_grid == null || _heightScale <= 0f) return 0f;

            float ground = _grid.SurfaceElevation(position.X, position.Y) * _heightScale;
            if (_bridges == null) return ground;

            foreach (var bridge in _bridges)
            {
                if (bridge == null) continue;
                if (bridge.Height(position.X, position.Y, ground, out float deck) && deck > ground)
                    return deck;
            }

            return ground;
        }

        BridgeDeck[] _bridges;

        /// <summary>
        /// Finds the crossings once. Called after the decorator has built the world, so
        /// the bridges exist; scanning per frame would be the same answer at a cost.
        /// </summary>
        public void FindBridges(Transform props)
        {
            _bridges = props == null
                ? System.Array.Empty<BridgeDeck>()
                : props.GetComponentsInChildren<BridgeDeck>(true);

            if (_bridges.Length > 0)
                Debug.Log($"[Arna] {_bridges.Length} bridge(s) found; the column rides over them.");
        }

        /// <summary>
        /// Tells the run what it has to walk round.
        ///
        /// Called once, after the decorator has finished. Every prop the decorator judged
        /// solid carries a <see cref="Solid"/> with the radius it actually blocks, and
        /// they go into the run's obstacle field as discs — see
        /// <see cref="Arna.Sim.ObstacleField"/> for why the radius is the trunk and not
        /// the crown.
        ///
        /// This is the join the game never had. The scenery has always been over here and
        /// the walking over there, so the column drove through trees because nothing that
        /// moved it had ever been told they were standing.
        /// </summary>
        public void FindObstacles(Transform props, LevelRun run)
        {
            if (props == null || run?.Obstacles == null) return;

            var solids = props.GetComponentsInChildren<Solid>(true);
            run.Obstacles.Clear();

            foreach (var solid in solids)
                run.Obstacles.Add(solid.Centre.x, solid.Centre.y, solid.Radius);

            Debug.Log($"[Arna] {run.Obstacles.Count} solid prop(s); the escort goes round them.");
        }

        static readonly Color SupplyColor = new Color(0.85f, 0.72f, 0.42f);
        static readonly Color WarColor = new Color(0.72f, 0.45f, 0.35f);
        static readonly Color TreasureColor = new Color(0.95f, 0.82f, 0.30f);
        static readonly Color TroopColor = new Color(0.35f, 0.75f, 0.95f);
        static readonly Color EnemyAsleepColor = new Color(0.62f, 0.32f, 0.55f);
        static readonly Color EnemyAwakeColor = new Color(0.95f, 0.25f, 0.20f);
        static readonly Color TrapColor = new Color(0.95f, 0.55f, 0.15f);
        static readonly Color CacheColor = new Color(0.95f, 0.85f, 0.35f);

        public void Build(LevelRun run)
        {
            foreach (var wagon in run.Caravan.Wagons)
            {
                var cart = BuildWagon(wagon.Kind);
                _wagons.Add(cart);
                _wagonWere.Add(null);
                _wheels.Add(WagonWheels.Fit(cart));
            }

            // Said once, and worth saying: if the pack ships its carts as one welded
            // mesh there are no wheel parts to turn, and the wagons go on sliding with
            // nothing in the console to say why.
            if (_wagons.Count > 0)
                Debug.Log($"[Arna] {_wagons.Count} wagons, {_draught.Count} horses in harness "
                          + $"({_draught.Count / _wagons.Count} each), "
                          + $"{_wheels[0].Count} wheels on the first.");

            CheckSpacing();

            if (_wagons.Count > 0 && _wheels[0].Count == 0)
                Debug.LogWarning("[Arna] No wheels found under the wagon models, so they will "
                                 + $"slide rather than roll. Parts: {WagonWheels.Parts(_wagons[0])}");

            if (run.Squad == null) return;

            foreach (var group in run.Squad.Slots)
            {
                if (group == null) continue;

                int models = TroopTable.Models(group.Kind);
                var figures = new List<Transform>(models);

                for (int i = 0; i < models; i++)
                    figures.Add(SpawnActor(Library.For(group.Kind), PrimitiveType.Capsule,
                        $"Troop_{group.Slot}_{group.Kind}_{i}", TroopColor,
                        VisualLibrary.HeightOf(group.Kind)));

                _troops[group] = figures;
                _reload[group] = 0f;
            }

            _volley = new Volley(_root, Library.Arrow);

            ReportCast(run);
        }

        /// <summary>
        /// Names the model every post is actually drawing, at runtime, once per level.
        ///
        /// This exists because "the old units are still in the game" has now been said
        /// four times and there was no way to tell which of three things it meant: a
        /// scene whose serialized library still holds the old models, an army prefab that
        /// failed to load, or a fallback quietly standing in. `Models` is a serialized
        /// field on a scene component, so an editor menu that rewires it only rewires the
        /// scene that happens to be open — and the running game is the only thing that
        /// knows what it is really holding.
        ///
        /// A name is not an opinion. `MC_ManAtArms_01` and `Knight` are different words.
        /// </summary>
        void ReportCast(LevelRun run)
        {
            if (run.Squad == null) return;

            var cast = new List<string>();

            foreach (var group in run.Squad.Slots)
            {
                if (group == null) continue;

                var model = Library.For(group.Kind);

                cast.Add($"{group.Slot} {group.Kind} = "
                         + (model.HasModel ? model.Prefab.name : "NO MODEL, drawing a capsule"));
            }

            var enemies = new List<string>();
            foreach (var kind in EnemyTable.All)
            {
                var model = Library.For(kind);
                enemies.Add($"{kind} = " + (model.HasModel ? model.Prefab.name : "capsule"));
            }

            Debug.Log($"[Arna] The cast on screen — {string.Join("; ", cast)}. "
                      + $"Enemies: {string.Join("; ", enemies)}.");
        }

        /// <summary>
        /// Composes a wagon from a crate and four wheels.
        ///
        /// None of the packs contain a cart, so it is assembled from parts we do have.
        /// Built from the same crate model the world is dressed with, it matches by
        /// construction rather than by luck.
        /// </summary>
        Transform BuildWagon(WagonKind kind)
        {
            var color = kind == WagonKind.Treasure ? TreasureColor
                      : kind == WagonKind.War ? WarColor
                      : SupplyColor;

            var wagon = new GameObject($"Wagon_{kind}").transform;
            wagon.SetParent(_root, false);

            // A purpose-built cart needs none of the improvised parts: no separate
            // wheels, no barrel standing in for a load. The composed version below is
            // the fallback for when the model is missing.
            var model = Library.WagonFor(kind);
            if (model != null)
            {
                var cart = Spawn(model, PrimitiveType.Cube, "Cart", color, VisualLibrary.WagonHeight, wagon);

                // Zero would throw away the lift that stood the cart on its wheels
                // rather than on its axle. The wagon itself is what gets moved about,
                // so the correction has to live in the cart underneath it.
                cart.localPosition = new Vector3(0f, Standing(cart, Vector3.zero).y, 0f);
                Harness(wagon, cart, kind);
                return wagon;
            }

            var body = Spawn(Library.WagonBody, PrimitiveType.Cube, "Body", color, 2.2f, wagon);
            body.localPosition = new Vector3(0f, 1.2f, 0f);

            if (Library.WagonCargo != null)
            {
                // Cargo rides on the wagon, so it has to read as a load rather than as
                // a second vehicle: barely a third of the body's height.
                var cargo = Spawn(Library.WagonCargo, PrimitiveType.Cube, "Cargo", color, 0.9f, wagon);
                cargo.localPosition = new Vector3(0f, 2.4f, -0.4f);
            }

            for (int i = 0; i < 4; i++)
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(wheel.GetComponent<Collider>());
                wheel.name = $"Wheel{i}";
                wheel.transform.SetParent(wagon, false);
                wheel.transform.localScale = new Vector3(1.1f, 0.16f, 1.1f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localPosition = new Vector3(
                    i < 2 ? -1.3f : 1.3f, 0.6f, i % 2 == 0 ? -1.4f : 1.4f);
                Tint(wheel.transform, new Color(0.28f, 0.20f, 0.14f));
            }

            Harness(wagon, body, kind);
            return wagon;
        }

        static readonly Color HorseColor = new Color(0.45f, 0.32f, 0.22f);

        /// <summary>
        /// How much clear air is left between the two horses of a team, in metres.
        ///
        /// A quarter of a metre, and the rest of the spacing is measured off the horse
        /// rather than guessed at. It was a flat 0.75 m from the wagon's centre line to
        /// each animal, which is only correct for a horse exactly 1.5 m wide: any wider
        /// and the pair interpenetrate and read as one animal with too many legs, which
        /// is precisely how a team of two comes to look like a team of one.
        ///
        /// Horses in harness stand close — the traces hold them there — so the gap is
        /// small on purpose. It is not the spacing that was wrong, it is having a
        /// spacing at all instead of a clearance.
        /// </summary>
        const float HorseGap = 0.25f;

        /// <summary>
        /// <summary>
        /// Puts a pair of horses in front of a cart.
        ///
        /// A wagon that moves with nothing pulling it is a wagon that moves by itself,
        /// which the eye reads long before it works out what is missing. Two abreast
        /// rather than one: three hundred and fifty silver is a load, and a single
        /// animal in front of a six-metre covered wagon looks like it is being punished.
        ///
        /// Parented to the wagon, so they follow it round every turn the road makes
        /// without any of this having to run again — and go out with it when it burns.
        ///
        /// The cart is taken to face +Z, which is what everything else in this file
        /// assumes: <see cref="Sync"/> turns the wagon to <c>LookRotation(heading)</c>
        /// and no yaw correction is applied to the model. If a pack ever ships one
        /// facing another way the horses will stand at its side, which is at least a
        /// visible kind of wrong rather than a silent one.
        /// </summary>
        /// <summary>
        /// Per wagon, how far its team's noses reach ahead of its centre, and how far
        /// its own cart extends behind it.
        ///
        /// Kept per wagon rather than as one figure because the carts are not the same
        /// length — a supply wagon is an open bed with barrels roped along it and a
        /// covered wagon is a hooped canvas, and one uniform spacing is only as good as
        /// the worst pair it has to hold apart. Which pair that is, is a fact about the
        /// models, so it is measured and printed rather than assumed.
        /// </summary>
        readonly List<float> _reach = new List<float>();
        readonly List<float> _rear = new List<float>();

        void Harness(Transform wagon, Transform cart, WagonKind kind)
        {
            // The draught horse, not the cavalry: the army pack's cavalry comes with a
            // rider on it, and hitching that to a wagon puts a knight in the traces.
            var team = Library.Draught.HasModel ? Library.Draught : Library.Mounted;
            if (!team.HasModel) return;

            // Per kind, because the carts are not built alike: one measures out to the
            // end of a modelled drawbar and another to the edge of its bed. See
            // VisualLibrary.HitchSupply.
            float hitch = Library.HitchFor(kind);

            // Measured off the cart rather than assumed from its height: the packs put a
            // hay cart and a covered wagon at the same 3.2 m and they are not remotely
            // the same length.
            var box = ModelScaling.Measure(cart.gameObject);

            float front = box.max.z - wagon.position.z;
            _rear.Add(wagon.position.z - box.min.z);

            // Printed per wagon, because this is the one measurement in the caravan that
            // comes from a model rather than from a table, and three models disagree
            // about it.
            Debug.Log($"[Arna] {kind} wagon: {cart.name} reaches {front:0.0} m forward and "
                      + $"{_rear[_rear.Count - 1]:0.0} m back from its centre; hitch {hitch:0.00} m, "
                      + $"so the team's tails stand at {front + hitch:0.0} m.");

            for (int i = 0; i < 2; i++)
            {
                var horse = SpawnActor(team, PrimitiveType.Capsule,
                                       $"Horse_{i}", HorseColor,
                                       VisualLibrary.DraughtHorseHeight, parent: wagon);

                // Width is measured and length is stated, and the difference is not an
                // inconsistency. Width across a horse barely changes between poses;
                // length does — the bounds hold every clip in the file, gallop included
                // — so the measured figure put the team a horse-length too far forward.
                var size = ModelScaling.Measure(horse.gameObject).size;
                float spread = size.x * 0.5f + HorseGap;

                horse.localRotation = Quaternion.Euler(0f, team.YawOffset, 0f);
                horse.localPosition = new Vector3(
                    i == 0 ? -spread : spread,
                    Standing(horse, Vector3.zero).y,
                    front + hitch + VisualLibrary.DraughtHorseLength * 0.5f);

                _draught.Add(horse);

                if (i == 0)
                    _reach.Add(horse.localPosition.z + VisualLibrary.DraughtHorseLength * 0.5f);
            }
        }

        /// <summary>
        /// Checks the caravan's spacing against the vehicles actually in it.
        ///
        /// <see cref="Caravan.WagonSpacing"/> is one number in the simulation and the
        /// carts it has to hold apart are three different models, so the arithmetic
        /// behind that number can only be finished here. A pair is tight when the
        /// following wagon's horses reach further forward than the gap leaves them: the
        /// team ends up inside the cart in front, which is invisible rather than obviously
        /// broken — the animals do not vanish, they are simply drawn behind planking.
        ///
        /// Printed every time and warned about when it fails, because the failure looks
        /// like "that wagon only has one horse" and not like a spacing problem at all.
        /// </summary>
        void CheckSpacing()
        {
            if (_reach.Count == 0 || _rear.Count == 0) return;

            float worst = 0f;
            int pair = 0;

            // Pair i is the gap between wagon i in front and wagon i + 1 behind it.
            for (int i = 0; i + 1 < _wagons.Count; i++)
            {
                if (i >= _rear.Count || i + 1 >= _reach.Count) break;

                float needed = _rear[i] + _reach[i + 1];
                if (needed <= worst) continue;

                worst = needed;
                pair = i;
            }

            string detail = $"the tightest pair is wagon {pair + 1} to {pair + 2}, which needs "
                            + $"{worst:0.0} m; the caravan uses {Caravan.WagonSpacing:0.0}";

            if (worst > Caravan.WagonSpacing)
            {
                Debug.LogWarning($"[Arna] The wagons are too close together: {detail}. The "
                                 + "following team is drawn inside the cart in front of it, "
                                 + "which looks like a missing horse. Raise Caravan.WagonSpacing.");
                return;
            }

            Debug.Log($"[Arna] Wagon spacing: {detail}.");
        }

        public void Sync(LevelRun run)
        {
            var heading = run.Caravan.Heading;
            var road = new Vector3(heading.X, 0f, heading.Y);

            for (int i = 0; i < _wagons.Count; i++)
            {
                var wagon = run.Caravan.Wagons[i];
                _wagons[i].gameObject.SetActive(!wagon.Destroyed);
                if (wagon.Destroyed) continue;

                var position = run.Caravan.WagonPosition(i);
                var here = new Vector3(position.X, GroundAt(position), position.Y);

                // Its own tangent, not the lead's. The positions were always right — they
                // trail along the path rather than sitting at a straight-line offset — so
                // handing every wagon the front one's rotation put the rear of the column
                // on curved ground pointing the wrong way, and it crabbed round every
                // bend. See Caravan.WagonHeading.
                var along = run.Caravan.WagonHeading(i);
                var mine = Quaternion.LookRotation(new Vector3(along.X, 0f, along.Y), Vector3.up);

                Place(_wagons[i], here, mine);

                // Across the ground rather than through it. Including the climb would
                // add the terrain sampler's own jitter to the roll, and on a slope of
                // any sane grade the difference is under a twentieth.
                var was = _wagonWere[i];
                float rolled = was.HasValue
                    ? new Vector2(here.x - was.Value.x, here.z - was.Value.z).magnitude
                    : 0f;

                _wagonWere[i] = here;
                _wheels[i].Roll(rolled);
            }

            // Troops march at the caravan's pace, so the column animates as one: when
            // the fen slows the wagons the escort trudges too.
            float pace = run.Caravan.CurrentSpeed;

            // And so do the horses, which is the same argument: a team standing still
            // in the traces while the wagon behind it moves is worse than no team.
            foreach (var horse in _draught) Animate(horse, pace, false, false);

            foreach (var pair in _troops)
            {
                var group = pair.Key;
                var figures = pair.Value;

                // Turned to its own opponent, and swinging only when it has one.
                //
                // Both used to be squad-wide: everyone faced the way the road went and
                // everyone attacked the moment anybody was in contact. Six figures
                // striking the air in the direction of travel while one wolf worried
                // the rear is not a fight, it is a formation having a seizure.
                //
                // Facing follows what the group is watching rather than what it is
                // hitting, which are not the same thing: a pack is in sight and running
                // at you for a second or two before anyone can reach it, and a rank
                // that keeps its back turned through that reads as a bug even though
                // the arithmetic underneath is correct.
                var watched = group.Watching;
                var forward = watched != null
                    ? Toward(group.Position, watched.Position, road)
                    : road;

                var look = Facing(forward, Library.For(group.Kind).YawOffset);
                int alive = group.Alive ? group.ModelsAlive : 0;

                for (int i = 0; i < figures.Count; i++)
                {
                    bool standing = i < alive;

                    // The same for our own: a man who is killed lies where he fell. See
                    // the note in SyncEnemies.
                    if (!standing)
                    {
                        Animate(figures[i], 0f, false, true);
                        continue;
                    }

                    var offset = Formation.Line(i, figures.Count, forward.x, forward.z);
                    var spot = new Vec2(group.Position.X + offset.X, group.Position.Y + offset.Y);

                    // Each figure stands on its own ground rather than the group's. Over
                    // four metres of hillside the difference is most of a man's height,
                    // and a rank levelled to one sample has half of it buried.
                    Place(figures[i], new Vector3(spot.X, GroundAt(spot), spot.Y), look);
                    Animate(figures[i], group.Engaged ? 0f : pace, group.Engaged, false);
                }
            }

            DrawReach(run);

            SyncEnemies(run);
            SyncTraps(run);
        }

        /// <summary>
        /// Draws each shooting troop's own reach, as a ring round that troop.
        ///
        /// <b>One per troop, not one round the caravan.</b> It was one — the longest
        /// reach in the column, centred on the middle of the wagons — and that is a
        /// circle drawn round something that does not own it. The longest reach in the
        /// column is almost always the archers' 22 m, so what the player saw was the
        /// archers' number in the caravan's place: a ring that did not move when the
        /// bows moved, did not shrink when the bows walked into a wood, and said nothing
        /// at all about any of the other posts.
        ///
        /// Before that it was six rings, one under every group, and they were collapsed
        /// into that one because six circles a few metres apart overlap into a knot of
        /// arcs. <b>That was the right complaint about the wrong thing.</b> What makes
        /// the knot is the hand weapons — spear 2.5, cavalry 2.2, sword and shield 1.8 —
        /// six near-identical small circles round men standing close together, drawn for
        /// a number nobody has a decision to make about. Take those away and what is
        /// left is 22, 18, 12, 12 and 8 metres, which do not sit on top of each other.
        ///
        /// So the cut is by <see cref="ShootingRange"/> rather than by count, and it is
        /// the same threshold <see cref="Advance"/> uses to decide who looses arrows
        /// instead of swinging. The ring and the arrows then answer the same question,
        /// which is the point: everything that shoots is drawn, and it is drawn where it
        /// stands.
        ///
        /// The radius is asked of the fighting rather than worked out again here (see
        /// <see cref="CombatSystem.Reach"/>), so it moves for every reason the reach
        /// itself moves: a range upgrade bought in the smithy widens it on the next
        /// frame, and walking into a wood shrinks an archer's by two fifths — which is
        /// the terrain rule made visible, having lived its whole life as a number in a
        /// table.
        /// </summary>
        void DrawReach(LevelRun run)
        {
            if (!ShowReach || run.Squad == null || run.Combat == null) { HideRings(); return; }

            var terrain = run.Caravan.CurrentTerrain;

            foreach (var group in run.Squad.Slots)
            {
                if (group == null) continue;

                // Dead groups and hand-weapon groups both get nothing, and for the same
                // reason: a ring is only worth the space it takes when it tells the
                // player something they can act on.
                if (!group.Alive || TroopTable.Range(group.Kind) < ShootingRange)
                {
                    if (_rings.TryGetValue(group, out var idle)) idle.Hide();
                    continue;
                }

                float reach = run.Combat.Reach(group, terrain);

                if (reach <= 0f)
                {
                    if (_rings.TryGetValue(group, out var empty)) empty.Hide();
                    continue;
                }

                // Brighter with something in it, and now per troop rather than per
                // column. A ring is a statement about what that post could hit; a ring
                // with a target inside it is a statement about what it is hitting, and
                // the two should not look the same. The one ring used to light the moment
                // anybody anywhere in the column was fighting, which is the wrong answer
                // to "what are these men busy with".
                var colour = ReachColour(group.Kind);
                colour.a = group.Target != null ? ReachLit : ReachIdle;

                Ring(group).Draw(group.Position, reach, colour, at => GroundAt(at));
            }
        }

        /// <summary>The ring belonging to one troop, made the first time it is asked for.</summary>
        // Lazily rather than beside the figures in Build, so a column of six swordsmen
        // creates no ring meshes at all.
        RangeRing Ring(TroopGroup group)
        {
            if (_rings.TryGetValue(group, out var ring)) return ring;

            ring = new RangeRing(_root, $"Reach_{group.Slot}_{group.Kind}", RingMaterial());
            _rings[group] = ring;

            return ring;
        }

        void HideRings()
        {
            foreach (var ring in _rings.Values) ring.Hide();
        }

        /// <summary>Whether the reach ring is drawn at all.</summary>
        public bool ShowReach = true;

        public const float ReachIdle = 0.34f;
        public const float ReachLit = 0.72f;

        /// <summary>
        /// What colour one troop's ring is drawn in.
        ///
        /// Pale rather than saturated, all of them. These are laid over grass the player
        /// also has to read, and a strong colour on the ground would win an argument the
        /// terrain needs to win.
        ///
        /// Different per troop, because two rings can overlap and a pair of identical
        /// pale circles crossing is worse than either alone. This is what the custom
        /// shader is for — see <see cref="RangeRing.Material"/>: URP's Unlit ignores
        /// vertex colours, so every ring would otherwise come out the same regardless of
        /// what is written into the mesh.
        ///
        /// The priest is green and is not a mistake. His reach is a healing radius, not
        /// a threat one — 0 damage, 15 health a second to whoever inside it is worst hurt
        /// — and drawing that in the same warm tone as a bow would say the wrong thing
        /// about the one post that does not attack.
        /// </summary>
        public static Color ReachColour(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Mage: return new Color(0.82f, 0.72f, 1f);
                case TroopKind.Scout: return new Color(0.72f, 0.90f, 1f);
                case TroopKind.Priest: return new Color(0.72f, 1f, 0.78f);
                case TroopKind.Engineer: return new Color(1f, 0.80f, 0.62f);
                default: return new Color(1f, 0.94f, 0.68f);
            }
        }

        Material RingMaterial()
        {
            if (_ringMaterial == null) _ringMaterial = RangeRing.Material();
            return _ringMaterial;
        }

        /// <summary>
        /// Shortest reach at which a troop is shooting rather than swinging.
        ///
        /// Eight metres, which sorts the table: the bow is 22, the staff 18, the scout
        /// and the priest 12 and the engineer's crossbow 8, while every hand weapon is
        /// under three. Nothing here is a judgement about weapons — it is the reach
        /// column, read for what it already says.
        ///
        /// It decides two things now, not one: who looses arrows in <see cref="Advance"/>,
        /// and who gets a reach ring in <see cref="DrawReach"/>. That they are the same
        /// threshold is deliberate — a troop drawing a circle it never shoots inside of
        /// would be a worse lie than no circle.
        /// </summary>
        public const float ShootingRange = 8f;

        /// <summary>Seconds between shafts from one group.</summary>
        public const float Reload = 0.55f;

        /// <summary>
        /// Fires the bows and moves what is already in the air.
        ///
        /// Separate from <see cref="Sync"/> because it is the one piece of the view that
        /// is about elapsed time rather than about the state of the run, and because the
        /// headless capture steps the simulation without any time passing at all — arrows
        /// hanging in mid-air across a screenshot would be worse than none.
        /// </summary>
        public void Advance(LevelRun run, float deltaTime)
        {
            if (_volley == null || run?.Squad == null) return;

            _volley.Advance(deltaTime);

            var terrain = run.Caravan.CurrentTerrain;

            foreach (var group in run.Squad.Slots)
            {
                if (group == null || !group.Alive) continue;
                if (!_reload.TryGetValue(group, out float since)) continue;

                var target = group.Target;

                // Nothing to shoot at: the clock is left run down rather than reset, so
                // the first shaft at a pack breaking cover goes out at once instead of
                // half a second after it is already being fought.
                if (target == null || TroopTable.Range(group.Kind) < ShootingRange)
                {
                    _reload[group] = Reload;
                    continue;
                }

                since += deltaTime;
                if (since < Reload) { _reload[group] = since; continue; }

                _reload[group] = 0f;

                var to = new Vector3(target.Position.X, GroundAt(target.Position) + Volley.ToHeight,
                                     target.Position.Y);

                Volleys(group, to);
            }
        }

        /// <summary>
        /// One shaft from every archer still standing, rather than one from the group.
        ///
        /// A group is a pooled health bar in the simulation and three men on the screen
        /// (TroopTable.Models), and it used to loose a single arrow from the group's
        /// centre point — so three archers drew together and one arrow left, from a spot
        /// between them where nobody was standing. A volley is what a rank of bows looks
        /// like, and there was already a list of exactly who is in it.
        ///
        /// The dead are switched off rather than removed from the list (see Build), so
        /// activeSelf is the survivor test and a whittled-down group thins its volley
        /// without any extra bookkeeping.
        /// </summary>
        void Volleys(TroopGroup group, Vector3 to)
        {
            if (!_troops.TryGetValue(group, out var figures) || figures == null)
            {
                var alone = new Vector3(group.Position.X,
                                        GroundAt(group.Position) + Volley.FromHeight,
                                        group.Position.Y);
                _volley.Loose(alone, to);
                return;
            }

            int shot = 0;

            for (int i = 0; i < figures.Count; i++)
            {
                var figure = figures[i];
                if (figure == null || !figure.gameObject.activeSelf) continue;

                var at = figure.position;
                var from = new Vector3(at.x, at.y + Volley.FromHeight, at.z);

                // Fanned by a hand's width at the far end, spread from the shooter's
                // index rather than at random: three arrows sent at one point arrive as
                // one arrow, and a headless capture should draw the same picture twice.
                float spread = (i - (figures.Count - 1) * 0.5f) * Volley.Fan;
                var across = Vector3.Cross(Vector3.up, (to - from).normalized) * spread;

                _volley.Loose(from, to + across);
                shot++;
            }

            if (shot == 0)
                _volley.Loose(new Vector3(group.Position.X,
                                          GroundAt(group.Position) + Volley.FromHeight,
                                          group.Position.Y), to);
        }

        /// <summary>
        /// Enemies appear only once revealed. This is the fog of war made visible, and
        /// the moment a group fades in ahead of the column is the design working.
        /// </summary>
        void SyncEnemies(LevelRun run)
        {
            foreach (var enemy in run.Detection.Enemies)
            {
                bool defeated = run.Combat != null && run.Combat.IsDefeated(enemy);
                var model = Library.For(enemy.Kind);

                // Never seen: nothing is drawn. Wiped out: the bodies stay.
                //
                // A group that has been beaten used to be switched off wholesale, so a
                // fight ended with the ground it was fought on completely empty. What a
                // player is owed after winning is the evidence of it.
                if (!enemy.Revealed)
                {
                    if (_enemies.TryGetValue(enemy, out var hidden))
                        foreach (var figure in hidden) figure.gameObject.SetActive(false);
                    continue;
                }

                if (defeated)
                {
                    if (_enemies.TryGetValue(enemy, out var fallen))
                        foreach (var figure in fallen) Animate(figure, 0f, false, true);
                    continue;
                }

                if (!_enemies.TryGetValue(enemy, out var pack))
                {
                    float height = enemy.Kind == EnemyKind.Wolf
                        ? VisualLibrary.WolfHeight
                        : VisualLibrary.EnemyHeight;

                    // One figure per animal or man the group is made of. A wolf pack is
                    // five wolves on the table and was one wolf on the screen, which is
                    // not a rendering shortcut so much as a different game: the player
                    // was being told a pack had found him and shown a stray dog.
                    int size = EnemyTable.GroupSize(enemy.Kind);
                    pack = new List<Transform>(size);

                    for (int i = 0; i < size; i++)
                    {
                        var figure = SpawnActor(model, PrimitiveType.Sphere,
                            $"Enemy_{enemy.Kind}_{i}", EnemyAwakeColor, height);

                        // Once, at spawn. A property block set every frame on every
                        // figure of every group is a per-frame cost for a colour that
                        // never changes.
                        if (model.HasModel) Faction(figure);

                        pack.Add(figure);
                    }

                    _enemies[enemy] = pack;
                }

                // Turned toward the troop it is actually fighting, not toward the head
                // of the column. A pack that has swung round to maul the rear guard used
                // to stand side-on to it and stare at the wagons.
                var quarry = enemy.Engaging;
                var focus = quarry != null ? quarry.Position : run.Caravan.LeadPosition;
                var forward = Toward(enemy.Position, focus, Vector3.forward);
                var look = Facing(forward, model.YawOffset);

                // Whether it is biting or still closing is what the combat step decided
                // this tick, not what the view can guess from a distance and an assumed
                // slack — those two disagreed, and animals bit the air a metre out.
                float speed = enemy.Awake && !enemy.Striking
                    ? EnemyTable.Speed(enemy.Kind) * TileGrid.TileSize
                    : 0f;

                int alive = run.Combat != null ? run.Combat.ModelsAlive(enemy) : pack.Count;

                // A wedge while it runs and a ring once it arrives, and the switch is
                // the whole difference between a pack and a queue. A wedge is one animal
                // deep at the point: five wolves in one means the lead reaches the troop
                // and four wait their turn a metre and a half behind. On screen that is
                // the thing the player was told is a pack, attacking one at a time.
                for (int i = 0; i < pack.Count; i++)
                {
                    bool standing = i < alive;

                    // Left where it fell, playing its death, rather than deleted.
                    //
                    // A wolf that vanishes the instant its share of the pooled health
                    // runs out reads as a rendering glitch, not as a kill — and the
                    // animator has had a Death state built for it since the controllers
                    // were generated, with nothing ever asking for it. The figure keeps
                    // its last position because nothing places it again: `alive` only
                    // falls, so index i is dead for good once it passes it, and a body
                    // cannot come back to life on a later frame.
                    if (!standing)
                    {
                        Animate(pack[i], 0f, false, true);
                        continue;
                    }

                    var offset = enemy.Striking
                        ? Formation.Ring(i, alive, forward.x, forward.z)
                        : Formation.Wedge(i, forward.x, forward.z);

                    var spot = new Vec2(enemy.Position.X + offset.X, enemy.Position.Y + offset.Y);

                    // The whole pack faces one way, and that way is the caravan.
                    //
                    // Twice wrong before this. First each animal faced the centre of its
                    // own ring — a point one radius ahead of the group's marker, which is
                    // the quarry only if the quarry happens to be standing exactly there.
                    // Then each faced the quarry itself, which is worse than it sounds:
                    // the arc is two and a half metres across and the thing it surrounds
                    // is a stride away, so the animals at the ends of it turned sharply
                    // inward and the arc splayed like a hand of cards.
                    //
                    // A pack closing on something is a row of heads pointing the same
                    // way. One bearing, taken from the group's own marker to what it is
                    // attacking, and every animal in it uses that — which is also what
                    // reads as *toward the caravan* from any camera angle, because that
                    // is where the thing being attacked is standing.
                    var turn = look;

                    Place(pack[i], new Vector3(spot.X, GroundAt(spot), spot.Y), turn);
                    Animate(pack[i], speed, enemy.Striking, false);

                    // Colour only survives on primitives; a model keeps its own materials.
                    if (!model.HasModel)
                        Tint(pack[i], enemy.Awake ? EnemyAwakeColor : EnemyAsleepColor);
                }
            }
        }

        /// <summary>
        /// The direction from one point to another, falling back when they coincide.
        ///
        /// A look rotation built from a zero vector is a Unity warning and a figure
        /// snapped to north, which is how a whole pack ends up facing the same wrong way
        /// the instant it arrives on top of what it is attacking.
        /// </summary>
        static Vector3 Toward(Vec2 from, Vec2 to, Vector3 fallback)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            if (dx * dx + dy * dy < 0.01f) return fallback;
            return new Vector3(dx, 0f, dy);
        }

        void SyncTraps(LevelRun run)
        {
            foreach (var trap in run.Traps.Traps)
            {
                bool show = trap.Revealed && !trap.Triggered && !trap.Disarmed;

                if (!show)
                {
                    if (_traps.TryGetValue(trap, out var hidden)) hidden.gameObject.SetActive(false);
                    continue;
                }

                if (!_traps.TryGetValue(trap, out var marker))
                {
                    marker = Spawn(Library.TrapMarker, PrimitiveType.Cylinder,
                        $"Trap_{trap.Kind}", TrapColor, 1.4f);
                    Place(marker, new Vector3(trap.Position.X, GroundAt(trap.Position), trap.Position.Y));
                    _traps[trap] = marker;
                }

                marker.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Stands one actor up outside a run, for a reference shot of the cast.
        ///
        /// Goes through the same spawn path the level does — same height fitting, same
        /// animator, same weapon in the same hand — so the line-up is evidence about
        /// the game rather than a separate picture that happens to use the same models.
        /// </summary>
        public Transform ShowActor(ActorModel model, string name, float targetHeight,
                                   Vector3 position, float speed = 0f, bool byWidth = false)
        {
            var marker = SpawnActor(model, PrimitiveType.Capsule, name, TroopColor,
                                    targetHeight, byWidth);
            Place(marker, position);
            Animate(marker, speed, false, false);
            return marker;
        }

        /// <summary>
        /// Advances every animator by hand.
        ///
        /// Unity drives animators itself during play, but not in an editor session, so
        /// a headless capture would otherwise render everything in bind pose — which
        /// looks exactly like the animation setup being broken.
        /// </summary>
        public void AdvanceAnimators(float deltaTime)
        {
            foreach (var animator in _animators.Values)
                if (animator != null) animator.Update(deltaTime);
        }

        /// <summary>Places the silver caches, which never move once the level begins.</summary>
        /// <summary>
        /// Puts a flock of crows over each piece of ground <see cref="CrowSignal"/> chose.
        ///
        /// Instantiated and placed, and nothing more. The pack's controller flies the
        /// birds itself, and this assembly could not configure it even if it wanted to:
        /// the pack ships no assembly definition, so its scripts compile into
        /// Assembly-CSharp, which an asmdef assembly cannot reference. Count, radius and
        /// altitude live on the prefab — 3, 10 m and 22 m, measured in §4 of the status
        /// notes.
        ///
        /// Truthful and lying flocks are built identically. That is the design and not
        /// an oversight: a signal you can tell is false is not a false positive.
        /// </summary>
        /// <summary>
        /// Spawns the deer, foxes and boar (docs/GDD.md §3.5).
        ///
        /// Actors, because they are animated and walk — the same path the wolf takes,
        /// which is also why they get the pack's own controllers. Unlike the enemies
        /// they are never hidden and never revealed: an animal the player cannot see
        /// until it is spotted would be a threat with the serial numbers filed off, and
        /// the whole point of these is that they are not one.
        /// </summary>
        public void BuildWildlife(IReadOnlyList<WildAnimal> animals)
        {
            if (animals == null) return;

            // Named one by one, because "no animals", "one pack missing" and "animals
            // too small to see" look identical from the outside and are fixed in three
            // different places. The old warning fired only when *all four* were missing,
            // which is the one case that was never the problem.
            var missing = new List<string>();
            if (Library.Fox.Prefab == null) missing.Add("fox");
            if (Library.DeerFemale.Prefab == null) missing.Add("doe");
            if (Library.DeerMale.Prefab == null) missing.Add("stag");
            if (Library.Boar.Prefab == null) missing.Add("boar");

            if (missing.Count > 0)
                Debug.LogWarning($"[Arna] No model for: {string.Join(", ", missing)}. Those are "
                                 + "drawn as coloured capsules. Run Arna > Refresh Scene Assets.");

            foreach (var animal in animals)
            {
                var marker = SpawnActor(Library.For(animal.Kind), PrimitiveType.Capsule,
                                        $"Wild_{animal.Kind}", WildlifeColor,
                                        VisualLibrary.HeightOf(animal.Kind));
                _wildlife[animal] = marker;
                Place(marker, new Vector3(animal.Position.X, GroundAt(animal.Position),
                                          animal.Position.Y),
                      Facing(new Vector3(animal.Heading.X, 0f, animal.Heading.Y),
                             Library.For(animal.Kind).YawOffset));
            }

            int fox = 0, doe = 0, stag = 0, boar = 0;
            foreach (var animal in animals)
                switch (animal.Kind)
                {
                    case WildlifeKind.Fox: fox++; break;
                    case WildlifeKind.DeerFemale: doe++; break;
                    case WildlifeKind.DeerMale: stag++; break;
                    default: boar++; break;
                }

            // The heights too, because the reason they could not be seen was arithmetic
            // rather than absence: a fox stood 0.45 m in grass fitted to 0.70.
            Debug.Log($"[Arna] {animals.Count} wild animals: {fox} fox at "
                      + $"{VisualLibrary.HeightOf(WildlifeKind.Fox):0.00} m, {doe} does, "
                      + $"{stag} stags at {VisualLibrary.HeightOf(WildlifeKind.DeerMale):0.00} m, "
                      + $"{boar} boar. The grass is {TerrainDecorator.CoverHeight:0.00} m.");
        }

        /// <summary>
        /// Moves them, and faces a fleeing one the way it is running.
        ///
        /// A grazing animal keeps whatever facing it had. Turning it toward a home it is
        /// only drifting back to would have every deer on the level pointing at the same
        /// spot, which reads as a formation rather than as animals.
        /// </summary>
        public void SyncWildlife(IReadOnlyList<WildAnimal> animals)
        {
            if (animals == null) return;

            foreach (var animal in animals)
            {
                if (!_wildlife.TryGetValue(animal, out var marker) || marker == null) continue;

                var position = new Vector3(animal.Position.X, GroundAt(animal.Position),
                                           animal.Position.Y);

                // Turned whether it is fleeing or grazing. Only fleeing was turned
                // before, which left every grazing animal on the level pointing the same
                // way — a field of deer in parade order.
                Place(marker, position,
                      Facing(new Vector3(animal.Heading.X, 0f, animal.Heading.Y),
                             Library.For(animal.Kind).YawOffset));

                Animate(marker, animal.IsFleeing ? Wildlife.FleeSpeed : 0f, false, false);
            }
        }

        /// <summary>
        /// Multiplies a figure's colour, which is how a side is marked on models that
        /// share one palette texture.
        ///
        /// <see cref="Tint"/> cannot do this: it assigns a material, which is right for
        /// a primitive and wrong for a model that came with its own. A property block
        /// leaves the material alone and multiplies what it draws.
        /// </summary>
        void Faction(Transform figure)
        {
            if (Library.EnemyFaction != null)
            {
                Repaint(figure, Library.EnemyFaction);
                return;
            }

            var block = new MaterialPropertyBlock();

            foreach (var renderer in figure.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColor, VisualLibrary.EnemyTint);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// Swaps a figure's faction material for another, and touches nothing else.
        ///
        /// Only slots already holding one are replaced. A character carries several
        /// materials — body, weapon, shield — and the pack keeps weapons on their own;
        /// repainting every slot hands the bandits red swords.
        /// </summary>
        static void Repaint(Transform figure, Material faction)
        {
            foreach (var renderer in figure.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    if (!slots[i].name.StartsWith(VisualLibrary.FactionPrefix)) continue;

                    slots[i] = faction;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = slots;
            }
        }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        static readonly Color WildlifeColor = new Color(0.58f, 0.46f, 0.30f);

        /// <summary>
        /// Points a model along a direction, correcting for which way its own nose
        /// happens to face. See <see cref="ActorModel.YawOffset"/> for why that varies.
        /// </summary>
        static Quaternion Facing(Vector3 direction, float yawOffset)
            => Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);

        public void BuildCrowFlocks(IReadOnlyList<CrowFlock> flocks, TileGrid grid)
        {
            if (flocks == null) return;

            if (Library.CrowFlockPrefab == null)
            {
                Debug.LogWarning("[Arna] No crow flock prefab — the scene predates it, or the "
                                 + "path into Unluck Software is wrong. Run Set Up Play Scene.");
                return;
            }

            foreach (var flock in flocks)
            {
                var position = Vec2.FromTile(grid, flock.Tile);

                var instance = Object.Instantiate(Library.CrowFlockPrefab, _root);
                instance.name = $"Crows_{flock.Tile}";
                instance.transform.position =
                    new Vector3(position.X, GroundAt(position) + CrowAltitude, position.Y);
            }

            Debug.Log($"[Arna] {flocks.Count} crow flocks placed.");
        }

        /// <summary>
        /// Metres above the ground the flock turns.
        ///
        /// Measured rather than chosen. At 14 m the birds sat in the spruce tops and a
        /// near-black crow against dark forest at eighty metres is invisible; at 34 m
        /// they were above a camera that looks 35° down and left the frame entirely.
        /// </summary>
        public const float CrowAltitude = 22f;

        public void BuildCaches(IReadOnlyList<SilverCache> caches, TileGrid grid)
        {
            foreach (var cache in caches)
            {
                var position = Vec2.FromTile(grid, cache.Tile);
                var marker = Spawn(Library.SilverCache, PrimitiveType.Cube, "SilverCache", CacheColor, 1.8f);
                Place(marker, new Vector3(position.X, GroundAt(position), position.Y));
            }
        }

        /// <summary>
        /// Instantiates a model, normalises it to a target height and stands it on the
        /// ground.
        ///
        /// The packs are authored at wildly different scales — a wolf, a knight and a
        /// crate do not arrive in the same units — so everything is measured and
        /// rescaled on the way in rather than tuned prefab by prefab.
        /// </summary>
        /// <summary>
        /// Spawns something that moves and fights, wiring up its animator.
        ///
        /// The controller belongs to the model rather than to the role, because every
        /// rig in the packs is Generic: clips are bound to one skeleton and cannot be
        /// shared across models.
        /// </summary>
        Transform SpawnActor(ActorModel model, PrimitiveType fallback, string name, Color color,
                             float targetHeight, bool byWidth = false, Transform parent = null)
        {
            var marker = Spawn(model.Prefab, fallback, name, color, targetHeight, parent,
                               model.Hide, model.Unsized, byWidth);

            if (model.Prefab == null || model.Animator == null) return marker;

            // Explicit == rather than ?? on purpose. Unity overloads equality to report
            // missing and destroyed objects as null, but null-coalescing bypasses that
            // overload and hands back an object that throws the moment it is used.
            var animator = marker.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                animator = marker.gameObject.AddComponent<Animator>();

                // Every rig in these packs is Generic, and a Generic rig binds its clips
                // through an avatar: without one the animator runs, reports a state and
                // a normalised time, and moves nothing at all. Taken off the model's own
                // Animator, which is where the importer put it.
                var rig = model.Prefab.GetComponentInChildren<Animator>();
                if (rig != null) animator.avatar = rig.avatar;
            }

            animator.runtimeAnimatorController = model.Animator;

            // The skeleton, when the clips came from another file. See ActorModel.Rig.
            if (animator.avatar == null && model.Rig != null) animator.avatar = model.Rig;

            // Once per model, and hedged, because it was neither.
            //
            // A Generic rig does not always need an avatar: Unity will bind a generic
            // clip by transform path when the clip and the model come from the same
            // file, which is exactly the case for every animal in this project. The
            // wolf has been animating perfectly all along and this fired on every one
            // of the five in every pack, every time a pack spawned. A warning that
            // cries wolf about a wolf is worse than no warning.
            //
            // It still earns its place for the case it was written for — a *retargeted*
            // clip, which needs an avatar and silently does nothing without one.
            //
            // That was the comment, and the condition below did not say it: it fired on
            // any model without an avatar, so the wolf, the fox, both deer and the boar
            // all warned on every run of a level. The model now carries whether its clips
            // are its own, because nothing about the prefab can be asked that question.
            if (model.Borrowed && animator.avatar == null && _warned.Add(model.Prefab.name))
                Debug.LogWarning($"[Arna] {model.Prefab.name} has no avatar. That is fine for a "
                                 + "Generic rig playing clips out of its own file, and fatal "
                                 + "for one playing clips retargeted from another — if it holds "
                                 + "its bind pose, this is why.");

            // Culling would freeze actors the camera is not looking at, and the
            // simulation keeps moving them regardless.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            // Bound and stepped once here, because an animator that has never been
            // bound evaluates nothing outside play mode: a headless capture came back
            // showing every model in the rest pose its file was saved in, which is a
            // picture of the art rather than of the game. Play mode does this itself,
            // so this costs one evaluation at spawn and changes nothing there.
            animator.Rebind();
            animator.Update(0f);

            _animators[marker] = animator;
            Arm(marker, model);
            return marker;
        }

        /// <summary>
        /// Puts a weapon in the actor's right hand.
        ///
        /// Parented to the hand bone rather than to the model, so it follows the
        /// animation instead of hovering beside a swinging arm. The bone is found by
        /// name because the rigs are Generic — there is no humanoid avatar to ask.
        /// </summary>
        void Arm(Transform marker, ActorModel model)
        {
            if (model.Weapon == null) return;

            if (AlreadyArmed(marker))
            {
                Debug.Log($"[Arna] {marker.name} carries a weapon in its own rig; {model.Weapon.name} skipped.");
                return;
            }

            var hand = FindHandBone(marker);
            if (hand == null)
            {
                // Silent failure here looks identical to a model that simply has no
                // weapon, which is exactly the confusion this reports away.
                Debug.LogWarning($"[Arna] No hand bone on {marker.name}; {model.Weapon.name} not fitted.");
                return;
            }

            Debug.Log($"[Arna] {marker.name}: {model.Weapon.name} fitted to bone '{hand.name}'.");

            var weapon = Object.Instantiate(model.Weapon, hand);
            weapon.name = "Weapon";
            weapon.transform.position = Grip(hand);
            weapon.transform.localRotation = Quaternion.Euler(model.WeaponRotation);

            float length = model.WeaponLength > 0f ? model.WeaponLength : 0.8f;
            var bounds = ModelScaling.Measure(weapon);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest > 0.0001f) weapon.transform.localScale *= length / longest;
        }

        /// <summary>
        /// Where a hand actually holds something: the middle of its knuckles.
        ///
        /// Half of these rigs put a wrist between the forearm and the fingers and half
        /// hang the fingers straight off the forearm, so the bone a weapon is parented
        /// to is sometimes a wrist and sometimes an elbow. At the bone's own origin
        /// that is the difference between a bow in the hand and a bow inside the
        /// archer's ribs. The knuckles are in the same place on both.
        /// </summary>
        static Vector3 Grip(Transform hand)
        {
            var sum = Vector3.zero;
            int count = 0;

            foreach (Transform child in hand)
                if (IsKnuckle(child.name)) { sum += child.position; count++; }

            return count > 0 ? sum / count : hand.position;
        }

        /// <summary>
        /// The bone a weapon belongs on: whichever one the fingers grow from.
        ///
        /// Looking for a bone named "hand" found nothing, because these rigs have none.
        /// A knight runs Shoulder.R → UpperArm.R → LowerArm.R → Index1.R, with the
        /// fingers hanging straight off the forearm; the modular men add a Wrist.R in
        /// between. Naming differs, anatomy does not, so the rule is anatomical. It is
        /// also where the pack's own artists hung the knight's sword.
        /// </summary>
        static Transform FindHandBone(Transform root)
        {
            Transform best = null;

            foreach (var bone in root.GetComponentsInChildren<Transform>())
            {
                if (!IsKnuckle(bone.name) || bone.parent == null) continue;

                // Prefer the right hand; fall back to the left rather than nothing.
                if (IsRightSide(bone.name)) return bone.parent;
                best ??= bone.parent;
            }

            return best;
        }

        /// <summary>
        /// Whether the rig already holds something — the knight ships with a sword
        /// modelled into his left hand, and giving him a second one would show.
        /// </summary>
        static bool AlreadyArmed(Transform root)
        {
            // Body meshes hang off the model root, not off a bone, so only a held
            // object can have an arm bone above it.
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                for (var bone = renderer.transform.parent; bone != null && bone != root; bone = bone.parent)
                    if (IsArmBone(bone.name)) return true;

            return false;
        }

        static bool IsKnuckle(string name)
        {
            string n = name.ToLowerInvariant();
            return n.StartsWith("index1") || n.StartsWith("middle1") || n.StartsWith("ring1") ||
                   n.StartsWith("pinky1") || n.StartsWith("thumb1");
        }

        /// <summary>
        /// Matched from the start of the name, not anywhere in it: every bone in these
        /// rigs sits under "CharacterArmature", which contains "arm".
        /// </summary>
        static bool IsArmBone(string name)
        {
            string n = name.ToLowerInvariant();
            return n.StartsWith("wrist") || n.StartsWith("hand") ||
                   n.StartsWith("lowerarm") || n.StartsWith("forearm");
        }

        static bool IsRightSide(string name)
        {
            string n = name.ToLowerInvariant();
            return n.EndsWith(".r") || n.EndsWith("_r") || n.Contains("right") || n.Contains("_r_");
        }

        /// <param name="byWidth">
        /// Scale to <paramref name="targetHeight"/> metres <i>across</i> rather than
        /// tall. For anything wider than it is tall, height is the wrong handle: an
        /// eagle with its wings out is 13.4 units span against 5.3 of vertical, most of
        /// which is wing dihedral rather than bird, so fitting it by height gives a
        /// wingspan decided by how far the wings happen to be cocked in the bind pose.
        /// </param>
        Transform Spawn(GameObject prefab, PrimitiveType fallback, string name, Color color,
                        float targetHeight, Transform parent = null, string[] hide = null,
                        string[] unsized = null, bool byWidth = false)
        {
            var host = parent != null ? parent : _root;

            if (prefab == null)
            {
                var primitive = GameObject.CreatePrimitive(fallback);
                Object.DestroyImmediate(primitive.GetComponent<Collider>());
                primitive.name = name;
                primitive.transform.SetParent(host, false);
                primitive.transform.localScale = Vector3.one * (targetHeight * 0.5f);
                Tint(primitive.transform, color);

                // A primitive's origin is its middle, so half of it is the ground.
                _standing[primitive.transform] = targetHeight * 0.5f;
                return primitive.transform;
            }

            var instance = Object.Instantiate(prefab, host);
            instance.name = name;

            // Before measuring, not after. A stowaway mesh lying thirty units below
            // the character drags the bounds down with it, and the figure gets scaled
            // and stood on the ground by a body that is not going to be drawn.
            Hide(instance.transform, hide);

            // Switched off across the measurement and back on after it, so a held
            // weapon is drawn at the size the character gives it rather than being
            // what decides that size.
            var held = Switch(instance.transform, unsized, false);

            float ground = instance.transform.position.y;

            if (byWidth) ModelScaling.FitToFootprint(instance, targetHeight, ground);
            else ModelScaling.Fit(instance, targetHeight, ground);

            _standing[instance.transform] = instance.transform.position.y - ground;

            foreach (var mesh in held) mesh.SetActive(true);

            return instance.transform;
        }

        /// <summary>
        /// Stands a marker at a place on the ground, feet first rather than origin
        /// first.
        ///
        /// Nothing says a model's origin is at the sole of its boot, and the packs
        /// disagree: most are within a few centimetres, the knight's sits a third of a
        /// metre up. <see cref="ModelScaling.Fit"/> works that offset out when the
        /// model is spawned, and every place that moved a marker afterwards threw it
        /// away by assigning a position outright. Keeping it here is what makes the
        /// correction survive the first frame.
        /// </summary>
        void Place(Transform marker, Vector3 groundPosition)
        {
            marker.position = Standing(marker, groundPosition);
        }

        void Place(Transform marker, Vector3 groundPosition, Quaternion facing)
        {
            marker.SetPositionAndRotation(Standing(marker, groundPosition), facing);
        }

        Vector3 Standing(Transform marker, Vector3 groundPosition)
        {
            if (_standing.TryGetValue(marker, out float lift)) groundPosition.y += lift;
            return groundPosition;
        }

        /// <summary>
        /// Switches the named meshes on or off and reports which ones it touched.
        ///
        /// Matched from the start of the name so "Henry.002" is still Henry: the
        /// importer appends a suffix when a name collides, and a rule that missed
        /// because of one would fail silently.
        /// </summary>
        static List<GameObject> Switch(Transform root, string[] names, bool on)
        {
            var touched = new List<GameObject>();
            if (names == null || names.Length == 0) return touched;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var name in names)
                    if (renderer.name.StartsWith(name, System.StringComparison.OrdinalIgnoreCase) &&
                        renderer.gameObject.activeSelf != on)
                    {
                        renderer.gameObject.SetActive(on);
                        touched.Add(renderer.gameObject);
                    }

            return touched;
        }

        /// <summary>Switches off the meshes a file carries that are not this character.</summary>
        static void Hide(Transform root, string[] names) => Switch(root, names, false);

        void Tint(Transform target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;

            if (!_materials.TryGetValue(color, out var material))
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                material.SetColor("_BaseColor", color);
                material.color = color;
                _materials[color] = material;
            }

            renderer.sharedMaterial = material;
        }
    }
}
