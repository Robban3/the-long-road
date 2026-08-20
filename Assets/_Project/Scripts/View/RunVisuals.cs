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
        readonly List<Transform> _wagons = new List<Transform>();
        readonly Dictionary<TroopGroup, Transform> _troops = new Dictionary<TroopGroup, Transform>();
        readonly Dictionary<TrackedEnemy, Transform> _enemies = new Dictionary<TrackedEnemy, Transform>();
        readonly Dictionary<TrackedTrap, Transform> _traps = new Dictionary<TrackedTrap, Transform>();
        readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        public RunVisuals(Transform root)
        {
            _root = root;
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

                var marker = Spawn(Library.For(group.Kind), PrimitiveType.Capsule,
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

            var body = Spawn(Library.WagonBody, PrimitiveType.Cube, "Body", color, 2.6f, wagon);
            body.localPosition = new Vector3(0f, 1.4f, 0f);

            if (Library.WagonCargo != null)
            {
                var cargo = Spawn(Library.WagonCargo, PrimitiveType.Cube, "Cargo", color, 1.6f, wagon);
                cargo.localPosition = new Vector3(0f, 2.8f, -0.6f);
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
                _wagons[i].SetPositionAndRotation(new Vector3(position.X, 0f, position.Y), facing);
            }

            foreach (var pair in _troops)
            {
                var group = pair.Key;
                pair.Value.gameObject.SetActive(group.Alive);
                if (!group.Alive) continue;

                pair.Value.SetPositionAndRotation(
                    new Vector3(group.Position.X, 0f, group.Position.Y), facing);
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
                    marker = Spawn(Library.For(enemy.Kind), PrimitiveType.Sphere,
                        $"Enemy_{enemy.Kind}", EnemyAwakeColor, VisualLibrary.EnemyHeight);
                    _enemies[enemy] = marker;
                }

                marker.gameObject.SetActive(true);
                marker.position = new Vector3(enemy.Position.X, 0f, enemy.Position.Y);

                // Face the caravan, which is what the group is coming for.
                var toCaravan = run.Caravan.LeadPosition - enemy.Position;
                if (toCaravan.X * toCaravan.X + toCaravan.Y * toCaravan.Y > 0.01f)
                    marker.rotation = Quaternion.LookRotation(
                        new Vector3(toCaravan.X, 0f, toCaravan.Y), Vector3.up);

                // Colour only survives on primitives; a model keeps its own materials.
                if (Library.For(enemy.Kind) == null)
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
                    marker.position = new Vector3(trap.Position.X, 0f, trap.Position.Y);
                    _traps[trap] = marker;
                }

                marker.gameObject.SetActive(true);
            }
        }

        /// <summary>Places the silver caches, which never move once the level begins.</summary>
        public void BuildCaches(IReadOnlyList<SilverCache> caches, TileGrid grid)
        {
            foreach (var cache in caches)
            {
                var position = Vec2.FromTile(grid, cache.Tile);
                var marker = Spawn(Library.SilverCache, PrimitiveType.Cube, "SilverCache", CacheColor, 1.8f);
                marker.position = new Vector3(position.X, 0f, position.Y);
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

            var bounds = MeasureBounds(instance);
            if (bounds.size.y > 0.001f)
            {
                float scale = targetHeight / bounds.size.y;
                instance.transform.localScale = Vector3.one * scale;
            }

            // Drop it so its feet, not its origin, rest on the ground.
            var scaled = MeasureBounds(instance);
            float lift = instance.transform.position.y - scaled.min.y;
            instance.transform.localPosition += new Vector3(0f, lift, 0f);

            return instance.transform;
        }

        static Bounds MeasureBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
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
