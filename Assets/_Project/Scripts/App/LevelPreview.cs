using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using Arna.View;
using UnityEngine;

namespace Arna.App
{
    /// <summary>
    /// Generates a level from its seed and draws the terrain overview with the three
    /// corridors the generator found.
    ///
    /// This is the development harness for the generator, not the shipping game view:
    /// change chapter and level in the inspector and the map rebuilds live. Being able
    /// to eyeball 20 levels in a minute is what makes a procedural generator tunable
    /// at all — the speckled terrain and dead-straight routes of the first version
    /// passed every unit test and were obviously wrong the moment they were rendered.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LevelPreview : MonoBehaviour
    {
        [Header("Level")]
        [Min(1)] public int Chapter = 1;
        [Min(1)] public int Level = 1;
        public bool ShowCorridors = true;

        [Header("World")]
        /// <summary>
        /// Metres between the lowest and highest ground, as in the play view.
        ///
        /// The map used to be drawn flat, on the argument that a plan is a diagram and
        /// shading would muddy the boundaries between terrain types. Read from directly
        /// above, a flat grid of colours is exactly as informative as it sounds and
        /// looks nothing like the country the player is about to cross. Relief, light
        /// and the same trees, rocks and buildings turn the plan into a picture of the
        /// level — which is the only way it can honestly promise what the level is.
        /// </summary>
        public float HeightScale = 22f;

        /// <summary>
        /// Denser than the play view. A map is read at a glance from far above, where
        /// scattered individual trees disappear; a forest has to look like a forest at
        /// map scale or the player cannot tell it from a meadow.
        /// </summary>
        public float DensityScale = 2.2f;

        [Min(0)] public int MaxProps = 2600;

        public BiomeDecor Decor = new BiomeDecor();

        [Header("Routes")]
        /// <summary>Flat unlit vertex colour. A drawn line is not lit by the sun.</summary>
        public Material RouteMaterial;

        /// <summary>
        /// Metres across. Narrow enough that the ground shows either side of it —
        /// a route the player cannot see the terrain under is not a route they can
        /// judge.
        /// </summary>
        public float RouteWidth = 2.2f;

        /// <summary>
        /// How solid the drawn routes are. Opaque, three of them crossing the whole map
        /// read as a transit diagram and the country underneath stops mattering — but
        /// the country underneath is what the player is choosing between.
        /// </summary>
        [Range(0f, 1f)] public float RouteOpacity = 0.72f;

        [Header("Generated (read-only)")]
        [SerializeField] int _seed;
        [SerializeField] int _attempts;
        [SerializeField] bool _choiceValidated;
        [SerializeField] float _fastCost;
        [SerializeField] float _safeCost;
        [SerializeField] float _oddCost;
        [SerializeField] float _maxOverlap;
        [SerializeField] Vector2Int _start;
        [SerializeField] Vector2Int _goal;

        bool _dirty = true;
        Mesh _mesh;
        Transform _props;
        Transform _routes;

        public int Seed => _seed;
        public int Attempts => _attempts;
        public bool ChoiceValidated => _choiceValidated;
        public float FastestRouteCost => _fastCost;
        public float MaxOverlap => _maxOverlap;

        void OnEnable() => _dirty = true;
        void OnValidate() => _dirty = true;

        void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            _seed = DeterministicRandom.SeedFor(Chapter, Level);

            var map = TerrainGenerator.Generate(new LevelRecipe(), _seed);

            _start = new Vector2Int(map.StartX, map.StartY);
            _goal = new Vector2Int(map.GoalX, map.GoalY);
            _attempts = map.Attempts;
            _choiceValidated = map.ChoiceValidated;
            _fastCost = map.CorridorOf(CorridorKind.Fast)?.TravelCost ?? 0f;
            _safeCost = map.CorridorOf(CorridorKind.Safe)?.TravelCost ?? 0f;
            _oddCost = map.CorridorOf(CorridorKind.Odd)?.TravelCost ?? 0f;
            _maxOverlap = WorstOverlap(map.Corridors);

            // No painted corridors any more — they are drawn as ribbons over the
            // ground instead. Start and goal stay painted: those are two single tiles,
            // and a marker is meant to be a patch.
            var mesh = TerrainMeshBuilder.Build(
                map.Grid, TileGrid.TileSize, null, map.StartIndex, map.GoalIndex, HeightScale);

            GetComponent<MeshFilter>().sharedMesh = mesh;

            BuildProps(map);
            BuildRoutes(map);

            // ExecuteAlways rebuilds on every inspector change, so the previous mesh
            // has to go or the editor leaks one per keystroke.
            if (_mesh != null && _mesh != mesh)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }
            _mesh = mesh;
        }

        /// <summary>
        /// Dresses the map with the same models the level is built from.
        ///
        /// The corridors are kept clear of props. That is what makes the plan legible:
        /// three routes drawn on the ground disappear under a closed canopy, and the
        /// one thing the player came to this view to do is compare them.
        /// </summary>
        void BuildProps(LevelMap map)
        {
            if (_props != null)
            {
                if (Application.isPlaying) Destroy(_props.gameObject);
                else DestroyImmediate(_props.gameObject);
            }

            if (Decor == null || Decor.IsEmpty) return;

            _props = new GameObject("Props").transform;
            _props.SetParent(transform, false);

            int placed = TerrainDecorator.Decorate(_props, map.Grid, map.Seed, Decor,
                keepClear: CorridorTiles(map), heightScale: HeightScale,
                maxProps: MaxProps, densityScale: DensityScale);

            // Worth printing: a prop that is placed but too small and a prop that was
            // never placed look identical on a map read from seventy metres up.
            Debug.Log($"[Arna] Plan {Chapter}-{Level}: {placed} props on {map.Grid.TileCount} tiles.");
        }

        /// <summary>
        /// Lays the three corridors over the ground as ribbons.
        ///
        /// Drawn worst-alternative first so the fastest route stays on top wherever
        /// they coincide — where two routes share ground, the fact worth showing is
        /// that the alternative is not an alternative there.
        /// </summary>
        void BuildRoutes(LevelMap map)
        {
            if (_routes != null)
            {
                if (Application.isPlaying) Destroy(_routes.gameObject);
                else DestroyImmediate(_routes.gameObject);
                _routes = null;
            }

            if (!ShowCorridors || map.Corridors == null || RouteMaterial == null) return;

            _routes = new GameObject("Routes").transform;
            _routes.SetParent(transform, false);

            AddRoute(map.CorridorOf(CorridorKind.Odd), TerrainPalette.RouteOdd, map.Grid);
            AddRoute(map.CorridorOf(CorridorKind.Safe), TerrainPalette.RouteSafe, map.Grid);
            AddRoute(map.CorridorOf(CorridorKind.Fast), TerrainPalette.RouteFast, map.Grid);
        }

        void AddRoute(Corridor corridor, Color color, TileGrid grid)
        {
            if (corridor == null) return;

            color.a = RouteOpacity;

            var mesh = RouteRibbonBuilder.Build(grid, corridor.Tiles, color, HeightScale, RouteWidth);
            if (mesh == null) return;

            var go = new GameObject($"Route_{corridor.Kind}");
            go.transform.SetParent(_routes, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = RouteMaterial;

            // A drawn line neither casts nor catches shadow. It is not in the world.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        HashSet<int> CorridorTiles(LevelMap map)
        {
            var tiles = new HashSet<int>();
            if (!ShowCorridors || map.Corridors == null) return tiles;

            foreach (var corridor in map.Corridors)
            {
                if (corridor?.Tiles == null) continue;
                foreach (int tile in corridor.Tiles) tiles.Add(tile);
            }

            return tiles;
        }

        static float WorstOverlap(IReadOnlyList<Corridor> corridors)
        {
            if (corridors == null || corridors.Count < 2) return 0f;

            float worst = 0f;
            for (int a = 0; a < corridors.Count; a++)
                for (int b = a + 1; b < corridors.Count; b++)
                    worst = Mathf.Max(worst, CorridorFinder.Overlap(corridors[a], corridors[b]));
            return worst;
        }

        void OnDisable()
        {
            if (_routes != null)
            {
                if (Application.isPlaying) Destroy(_routes.gameObject);
                else DestroyImmediate(_routes.gameObject);
                _routes = null;
            }

            if (_props != null)
            {
                if (Application.isPlaying) Destroy(_props.gameObject);
                else DestroyImmediate(_props.gameObject);
                _props = null;
            }

            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}
