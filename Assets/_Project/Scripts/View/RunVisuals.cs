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
        readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        readonly Dictionary<Transform, Animator> _animators = new Dictionary<Transform, Animator>();

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
                cart.localPosition = Vector3.zero;
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
                _wagons[i].SetPositionAndRotation(
                    new Vector3(position.X, GroundAt(position), position.Y), facing);
            }

            // Troops march at the caravan's pace, so the column animates as one: when
            // the fen slows the wagons the escort trudges too.
            float pace = run.Caravan.CurrentSpeed;
            bool fighting = run.Combat != null && run.Combat.InContact;

            foreach (var pair in _troops)
            {
                var group = pair.Key;
                pair.Value.gameObject.SetActive(group.Alive);
                if (!group.Alive) continue;

                pair.Value.SetPositionAndRotation(
                    new Vector3(group.Position.X, GroundAt(group.Position), group.Position.Y), facing);

                Animate(pair.Value, fighting ? 0f : pace, fighting, false);
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
                marker.position = new Vector3(enemy.Position.X, GroundAt(enemy.Position), enemy.Position.Y);

                // Face the caravan, which is what the group is coming for.
                var toCaravan = run.Caravan.LeadPosition - enemy.Position;
                if (toCaravan.X * toCaravan.X + toCaravan.Y * toCaravan.Y > 0.01f)
                    marker.rotation = Quaternion.LookRotation(
                        new Vector3(toCaravan.X, 0f, toCaravan.Y), Vector3.up);

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
                    marker.position = new Vector3(trap.Position.X, GroundAt(trap.Position), trap.Position.Y);
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
            marker.position = position;
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
        public void BuildCaches(IReadOnlyList<SilverCache> caches, TileGrid grid)
        {
            foreach (var cache in caches)
            {
                var position = Vec2.FromTile(grid, cache.Tile);
                var marker = Spawn(Library.SilverCache, PrimitiveType.Cube, "SilverCache", CacheColor, 1.8f);
                marker.position = new Vector3(position.X, GroundAt(position), position.Y);
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
            var marker = Spawn(model.Prefab, fallback, name, color, targetHeight);

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
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.Euler(model.WeaponRotation);

            float length = model.WeaponLength > 0f ? model.WeaponLength : 0.8f;
            var bounds = ModelScaling.Measure(weapon);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest > 0.0001f) weapon.transform.localScale *= length / longest;
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
                        float targetHeight, Transform parent = null)
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
                return primitive.transform;
            }

            var instance = Object.Instantiate(prefab, host);
            instance.name = name;
            ModelScaling.Fit(instance, targetHeight, instance.transform.position.y);
            return instance.transform;
        }

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
