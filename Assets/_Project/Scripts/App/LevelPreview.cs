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
        /// <summary>
        /// Draws the three corridors the generator found, and clears scenery along
        /// them so the ribbons read.
        ///
        /// Off by default, and that is a design change and not a preference. The
        /// corridors were the level's three answers while the player picked one of
        /// them; now the player draws their own line and the corridors are the
        /// generator's own working — an answer sheet laid over the question. Worse,
        /// the cleared scenery leaks them even with the ribbons hidden: three lanes
        /// through the forest at a third of the surrounding prop density, which the
        /// planning overlay cannot cover because it removes colour and not geometry.
        ///
        /// Turn it on to look at what the generator did. Not to play against it.
        /// </summary>
        public bool ShowCorridors;

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
        ///
        /// 1.38 rather than the 2.2 it was tuned at, and the number moved because what
        /// it multiplies did. The map wants about 0.62 trees per forest tile; that was
        /// 0.28 × 2.2 while the play view ran at 0.28, and when the play view went to
        /// 0.45 this quietly became 0.99 — the map got sixty percent denser than anyone
        /// asked for, and the terrain a player has to read to draw a route went with it.
        /// 0.62 ÷ 0.45 restores the tuned figure.
        /// </summary>
        public float DensityScale = 1.38f;

        [Min(0)] public int MaxProps = 2600;

        public BiomeDecor Decor = new BiomeDecor();

        [Header("Scouting")]
        /// <summary>
        /// Flies the scouting eagle over the plan (docs/GDD.md §3.6).
        ///
        /// The bird was imported, measured, fitted by wingspan and given a controller
        /// built out of its four flight clips, and then nothing drew it — because the
        /// screen it belongs to did not exist in Unity. This *is* that screen: the plan
        /// is a top-down render of the real world, which is exactly what the eagle is
        /// flown over.
        ///
        /// What it does not do yet is lift the overlay. `ScoutFlight` returns the tiles
        /// and the groups it found and this only reads the path, because the plan has no
        /// overlay to lift — that is a separate piece and pretending otherwise would put
        /// a bird over a map it is not actually scouting.
        /// </summary>
        public bool ShowEagle = true;

        /// <summary>
        /// Metres above the ground the bird flies.
        ///
        /// Measured on the plan render, where 14 m put her in the spruce tops and 34 m
        /// took her out of a frame that looks 35° down. The same argument settles it
        /// here and the same number comes out.
        /// </summary>
        public float EagleAltitude = 22f;

        public VisualLibrary Models = new VisualLibrary();

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

        RunVisuals _cast;
        Transform _eagleRoot;
        Transform _eagle;
        ScoutFlight _flight;
        float _flown;

        /// <summary>
        /// Distance along the flight at each point of it, and the total.
        ///
        /// Precomputed because the path is <b>not</b> evenly spaced: `ScoutFlight` samples
        /// a Catmull-Rom at fixed parameter steps, and a curve sampled by parameter is
        /// sampled unevenly by length — tight turns bunch the points up. Stepping it by
        /// index at a constant speed would have the bird dawdle through the corners and
        /// bolt down the straights, which is precisely backwards.
        /// </summary>
        float[] _milestones;
        float _flightLength;

        public int Seed => _seed;
        public int Attempts => _attempts;
        public bool ChoiceValidated => _choiceValidated;
        public float FastestRouteCost => _fastCost;
        public float MaxOverlap => _maxOverlap;

        void OnEnable() => _dirty = true;
        void OnValidate() => _dirty = true;

        void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Rebuild();
            }

            FlyEagle(Time.deltaTime);
        }

        /// <summary>
        /// Moves the bird along the flight the simulation worked out, and loops.
        ///
        /// Driven from Update rather than from a coroutine because this component is
        /// [ExecuteAlways]: the plan is looked at in the editor far more often than it
        /// is played, and a bird that only moves in play mode is a bird nobody sees.
        /// The animator has to be stepped by hand for the same reason — Unity does not
        /// run animators outside play mode, and an unstepped one holds its bind pose,
        /// which for a bird is a glider.
        /// </summary>
        void FlyEagle(float deltaTime)
        {
            if (_eagle == null || _flight == null || _flight.Path.Count < 2) return;
            if (deltaTime <= 0f) return;

            if (_milestones == null || _flightLength <= 0f) return;

            _flown = Mathf.Repeat(_flown + deltaTime * ScoutingAbility.Speed, _flightLength);

            // Walked by distance, not by index. Linear from the start each frame: the
            // path is a couple of hundred points and this runs once, which is cheaper
            // than being clever and impossible to get wrong.
            int at = 0;
            while (at < _milestones.Length - 2 && _milestones[at + 1] < _flown) at++;

            var from = _flight.Path[at];
            var to = _flight.Path[at + 1];

            float leg = _milestones[at + 1] - _milestones[at];
            float t = leg > 0.0001f ? Mathf.Clamp01((_flown - _milestones[at]) / leg) : 0f;

            float x = Mathf.Lerp(from.X, to.X, t);
            float z = Mathf.Lerp(from.Y, to.Y, t);
            float ground = GroundAt(x, z);

            _eagle.position = new Vector3(x, ground + EagleAltitude, z);

            var heading = new Vector3(to.X - from.X, 0f, to.Y - from.Y);
            if (heading.sqrMagnitude > 0.0001f)
                _eagle.rotation = Quaternion.LookRotation(heading, Vector3.up)
                                  * Quaternion.Euler(0f, Models.Eagle.YawOffset, 0f);

            _cast?.AdvanceAnimators(deltaTime);
        }

        /// <summary>Ground height under a world position, in the plan's own relief scale.</summary>
        float GroundAt(float x, float z)
            => _grid == null ? 0f : _grid.SurfaceElevation(x, z) * HeightScale;

        /// <summary>
        /// Spawns the eagle and works out the flight she will fly.
        ///
        /// The flight comes from <see cref="ScoutingAbility.Fly"/> — the same call the
        /// game makes when the ability is bought — so what the plan shows is the flight
        /// the level actually has, seeded off the map. A bird flying a path invented
        /// here would be decoration that contradicts the game.
        ///
        /// Fitted by wingspan rather than by height, like everything wider than it is
        /// tall: most of this model's vertical extent is wing dihedral, so fitting it by
        /// height lets the bind pose decide the wingspan.
        /// </summary>
        void BuildEagle(LevelMap map)
        {
            // The root, not the bird inside it. Destroying only the actor left its empty
            // parent behind, and this component rebuilds on every inspector keystroke —
            // one abandoned GameObject per character typed into the level field.
            if (_eagleRoot != null)
            {
                if (Application.isPlaying) Destroy(_eagleRoot.gameObject);
                else DestroyImmediate(_eagleRoot.gameObject);
            }

            _eagleRoot = null;
            _eagle = null;

            _cast = null;
            _flight = null;
            _milestones = null;
            _flightLength = 0f;
            _flown = 0f;
            _grid = map.Grid;

            if (!ShowEagle) return;

            if (Models == null || !Models.Eagle.HasModel)
            {
                Debug.LogWarning("[Arna] No eagle model — run Arna > Setup Project. "
                                 + "The scene predates the bird, or the path into "
                                 + "Assets/ThirdParty/Eagle is wrong.");
                return;
            }

            _flight = ScoutingAbility.Fly(map);
            if (_flight.Path.Count < 2) return;

            _milestones = new float[_flight.Path.Count];
            for (int i = 1; i < _flight.Path.Count; i++)
                _milestones[i] = _milestones[i - 1]
                                 + Vec2.Distance(_flight.Path[i - 1], _flight.Path[i]);

            _flightLength = _milestones[_milestones.Length - 1];

            _eagleRoot = new GameObject("Eagle").transform;
            _eagleRoot.SetParent(transform, false);

            _cast = new RunVisuals(_eagleRoot);

            var start = _flight.Path[0];
            _eagle = _cast.ShowActor(Models.Eagle, "Eagle", VisualLibrary.EagleSpan,
                                     Vector3.zero, speed: ScoutingAbility.Speed, byWidth: true);

            // Set outright rather than through ShowActor's placement, which stands a
            // model's feet on the ground it is given. That is right for everything that
            // walks and wrong for a bird: the altitude is where the animal is, not where
            // the lowest feather is.
            _eagle.position = new Vector3(start.X, GroundAt(start.X, start.Y) + EagleAltitude,
                                          start.Y);

            Debug.Log($"[Arna] Eagle: {_flight.Seconds:0} s aloft, {_flight.Coverage} tiles, "
                      + $"{_flight.RevealedEnemies.Count} of {map.Encounters.Enemies.Count} groups found.");
        }

        TileGrid _grid;

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
            BuildEagle(map);

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
                maxProps: MaxProps, densityScale: DensityScale,
                ruinSites: RuinSites(map));

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

        /// <summary>Trap fields are grouped into neighbourhoods this many tiles across.</summary>
        const int RuinClusterTiles = 6;

        /// <summary>
        /// Picks the ground that shows a caravan came to grief here.
        ///
        /// This is the soft signal the design asks for (docs/GDD.md §2): the player is
        /// meant to learn to read the country rather than be told what is in it. So a
        /// ruin is placed near a trap field, never on one — one per neighbourhood, and
        /// offset by a few tiles. Marking the trap itself would hand over the position
        /// of something the whole detection system exists to keep hidden, and a risk
        /// the player can see exactly is no longer a risk.
        ///
        /// Placed near enough to be worth noticing, far enough that noticing it tells
        /// you to be careful rather than where to step.
        /// </summary>
        List<int> RuinSites(LevelMap map)
        {
            var traps = map.Encounters?.Traps;
            if (traps == null || traps.Count == 0) return null;

            // "Never on one" is the whole of the tell, and it used to be said and not
            // done: the offset below is drawn from [-3, 3] in both axes, which includes
            // (0, 0), and nothing checked the trap tiles. A ruin marked a trap exactly
            // on one of nine sites on 1-5.
            var mined = new HashSet<int>();
            foreach (var trap in traps) mined.Add(trap.Tile);

            var rng = new DeterministicRandom(map.Seed ^ 0x2117);
            var neighbourhoods = new HashSet<int>();
            var sites = new List<int>();

            foreach (var trap in traps)
            {
                map.Grid.ToCoords(trap.Tile, out int x, out int y);

                // One ruin per neighbourhood. A field of six traps is one thing that
                // happened, not six.
                int cell = (y / RuinClusterTiles) * map.Grid.Width + x / RuinClusterTiles;
                if (!neighbourhoods.Add(cell)) continue;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int nx = x + rng.Range(-3, 4);
                    int ny = y + rng.Range(-3, 4);
                    if (!map.Grid.InBounds(nx, ny)) continue;

                    var terrain = map.Grid[nx, ny];
                    if (terrain == TerrainType.Water || terrain == TerrainType.Cliff) continue;

                    int site = map.Grid.ToIndex(nx, ny);
                    if (mined.Contains(site)) continue;

                    sites.Add(site);
                    break;
                }
            }

            return sites;
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
