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
        readonly Dictionary<TroopGroup, Transform> _troops = new Dictionary<TroopGroup, Transform>();
        readonly Dictionary<TrackedEnemy, Transform> _enemies = new Dictionary<TrackedEnemy, Transform>();
        readonly Dictionary<TrackedTrap, Transform> _traps = new Dictionary<TrackedTrap, Transform>();
        readonly Dictionary<WildAnimal, Transform> _wildlife = new Dictionary<WildAnimal, Transform>();
        readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        readonly Dictionary<Transform, Animator> _animators = new Dictionary<Transform, Animator>();

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
        float GroundAt(Vec2 position)
        {
            if (_grid == null || _heightScale <= 0f) return 0f;
            return _grid.SurfaceElevation(position.X, position.Y) * _heightScale;
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
                _wagons.Add(BuildWagon(wagon.Kind));

            if (run.Squad == null) return;

            foreach (var group in run.Squad.Slots)
            {
                if (group == null) continue;

                var marker = SpawnActor(Library.For(group.Kind), PrimitiveType.Capsule,
                    $"Troop_{group.Slot}_{group.Kind}", TroopColor, VisualLibrary.TroopHeight);
                _troops[group] = marker;
            }
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

            return wagon;
        }

        public void Sync(LevelRun run)
        {
            var heading = run.Caravan.Heading;
            var facing = Quaternion.LookRotation(new Vector3(heading.X, 0f, heading.Y), Vector3.up);

            for (int i = 0; i < _wagons.Count; i++)
            {
                var wagon = run.Caravan.Wagons[i];
                _wagons[i].gameObject.SetActive(!wagon.Destroyed);
                if (wagon.Destroyed) continue;

                var position = run.Caravan.WagonPosition(i);
                Place(_wagons[i], new Vector3(position.X, GroundAt(position), position.Y), facing);
            }

            // Troops march at the caravan's pace, so the column animates as one: when
            // the fen slows the wagons the escort trudges too.
            float pace = run.Caravan.CurrentSpeed;

            foreach (var pair in _troops)
            {
                var group = pair.Key;
                pair.Value.gameObject.SetActive(group.Alive);
                if (!group.Alive) continue;

                // Turned to its own opponent, and swinging only when it has one.
                //
                // Both used to be squad-wide: everyone faced the way the road went and
                // everyone attacked the moment anybody was in contact. Six figures
                // striking the air in the direction of travel while one wolf worried
                // the rear is not a fight, it is a formation having a seizure.
                var look = group.Engaged
                    ? Aim(group.Position, group.Target.Position, facing)
                    : facing;

                Place(pair.Value, new Vector3(group.Position.X, GroundAt(group.Position), group.Position.Y),
                      look);

                Animate(pair.Value, group.Engaged ? 0f : pace, group.Engaged, false);
            }

            SyncEnemies(run);
            SyncTraps(run);
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

                if (!enemy.Revealed || defeated)
                {
                    if (_enemies.TryGetValue(enemy, out var hidden)) hidden.gameObject.SetActive(false);
                    continue;
                }

                if (!_enemies.TryGetValue(enemy, out var marker))
                {
                    float height = enemy.Kind == EnemyKind.Wolf
                        ? VisualLibrary.WolfHeight
                        : VisualLibrary.EnemyHeight;

                    marker = SpawnActor(Library.For(enemy.Kind), PrimitiveType.Sphere,
                        $"Enemy_{enemy.Kind}", EnemyAwakeColor, height);
                    _enemies[enemy] = marker;
                }

                marker.gameObject.SetActive(true);
                Place(marker, new Vector3(enemy.Position.X, GroundAt(enemy.Position), enemy.Position.Y));

                // Face the caravan, which is what the group is coming for.
                var toCaravan = run.Caravan.LeadPosition - enemy.Position;
                if (toCaravan.X * toCaravan.X + toCaravan.Y * toCaravan.Y > 0.01f)
                    marker.rotation = Facing(new Vector3(toCaravan.X, 0f, toCaravan.Y),
                                             Library.For(enemy.Kind).YawOffset);

                // A group that has closed on the caravan is fighting; one that has woken
                // but is still crossing the ground is running at it.
                float rangeToCaravan = Vec2.Distance(enemy.Position, run.Caravan.LeadPosition);
                bool striking = enemy.Awake && rangeToCaravan < EnemyTable.AttackRange(enemy.Kind) + 6f;
                float speed = enemy.Awake && !striking
                    ? EnemyTable.Speed(enemy.Kind) * TileGrid.TileSize
                    : 0f;

                Animate(marker, speed, striking, false);

                // Colour only survives on primitives; a model keeps its own materials.
                if (!Library.For(enemy.Kind).HasModel)
                    Tint(marker, enemy.Awake ? EnemyAwakeColor : EnemyAsleepColor);
            }
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
                                   Vector3 position, float speed = 0f)
        {
            var marker = SpawnActor(model, PrimitiveType.Capsule, name, TroopColor, targetHeight);
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

            // Said out loud, because "no animals" and "animals with no model" look the
            // same from the outside and are fixed in different places.
            if (Library.Fox.Prefab == null && Library.DeerFemale.Prefab == null
                && Library.DeerMale.Prefab == null && Library.Boar.Prefab == null)
                Debug.LogWarning("[Arna] No wildlife models loaded — the scene predates them. "
                                 + "Run Arna > Build Animator Controllers, then Set Up Play Scene.");

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

            Debug.Log($"[Arna] {animals.Count} wild animals placed.");
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

        static readonly Color WildlifeColor = new Color(0.58f, 0.46f, 0.30f);

        /// <summary>
        /// Turns a figure from one point toward another, keeping its current facing if
        /// the two coincide — a rotation built from a zero vector is a warning and a
        /// figure snapped to north.
        /// </summary>
        static Quaternion Aim(Vec2 from, Vec2 to, Quaternion fallback)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            if (dx * dx + dy * dy < 0.01f) return fallback;

            return Quaternion.LookRotation(new Vector3(dx, 0f, dy), Vector3.up);
        }

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
                             float targetHeight)
        {
            var marker = Spawn(model.Prefab, fallback, name, color, targetHeight, null,
                               model.Hide, model.Unsized);

            if (model.Prefab == null || model.Animator == null) return marker;

            // Explicit == rather than ?? on purpose. Unity overloads equality to report
            // missing and destroyed objects as null, but null-coalescing bypasses that
            // overload and hands back an object that throws the moment it is used.
            var animator = marker.GetComponentInChildren<Animator>();
            if (animator == null) animator = marker.gameObject.AddComponent<Animator>();

            animator.runtimeAnimatorController = model.Animator;

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

        Transform Spawn(GameObject prefab, PrimitiveType fallback, string name, Color color,
                        float targetHeight, Transform parent = null, string[] hide = null,
                        string[] unsized = null)
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
            ModelScaling.Fit(instance, targetHeight, ground);
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
