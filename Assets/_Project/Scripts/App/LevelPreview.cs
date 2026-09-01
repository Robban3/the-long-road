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
        /// Lays the grey over everything the eagle has not flown over (docs/GDD.md §3.6).
        ///
        /// With no flight this mutes the whole map, which is not a bug: that is what the
        /// plan looks like before the ability is bought. Turn it off to use this scene
        /// the way it was originally built — as a harness for judging what the generator
        /// produced, where an overlay is in the way.
        /// </summary>
        public bool ShowOverlay = true;

        /// <summary>
        /// Colours for the two things the plan marks (docs/GDD.md §3.4, §3.5).
        ///
        /// Red for a group the eagle found, because that is the one fact on this screen
        /// worth interrupting the picture for. Near-black for a circling flock: crows are
        /// a hint and not an answer, and a hint that shouts is an answer.
        /// </summary>
        public Color EnemyMarker = new Color(0.90f, 0.20f, 0.16f, 0.92f);
        public Color CrowMarker = new Color(0.10f, 0.10f, 0.12f, 0.85f);

        /// <summary>
        /// Draws the signs that say what each built thing is (see <see cref="MapSymbols"/>).
        ///
        /// On, because without them a house, a farm, a ruin and a watchtower are four
        /// brown smudges. Off to look at the country the generator made without the map
        /// furniture on top of it, which is what this scene was originally for.
        /// </summary>
        public bool ShowSymbols = true;

        /// <summary>
        /// Marker sizes in metres.
        ///
        /// The map is 256 m across and read from seventy up, so these are chosen against
        /// the picture rather than against the world: a group's mark is about six metres
        /// wide, which is a fifth of a wagon's length and perfectly legible from there,
        /// and would be absurd standing on the ground.
        /// </summary>
        public float EnemyMarkerRadius = 2.9f;
        public float CrowMarkerRadius = 1.1f;

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

        Transform _markers;

        /// <summary>
        /// Held so it can be destroyed. A mesh built in code is not owned by the object
        /// that draws it, and this component rebuilds on every inspector keystroke — one
        /// abandoned mesh per character typed, for the lifetime of the editor session.
        /// </summary>
        Mesh _markerMesh;

        Transform _symbols;
        Mesh _symbolMesh;

        /// <summary>What the decorator built and where, so each can be given a sign.</summary>
        List<Landmark> _landmarks;

        /// <summary>
        /// The fog, and what it takes to lift it a piece at a time.
        ///
        /// `_lit` is the ground's colours as the terrain builder made them and `_shown`
        /// is what is on the mesh right now — the fog is the difference between the two,
        /// and lifting it over a tile is four colours copied from one into the other.
        /// Keeping the lit copy is the whole trick: a mute is not reversible, so there
        /// has to be something to reverse *to*.
        /// </summary>
        Color[] _lit;
        Color[] _shown;
        Mesh _overlay;

        /// <summary>
        /// Every prop's renderers, filed under the tile the prop stands on.
        ///
        /// Renderers rather than transforms, and gathered once while the fog goes on.
        /// A reveal repaints a tile's neighbourhood, a neighbourhood is twenty-five
        /// tiles, and dozens of tiles come due in a frame — walking each prop's
        /// hierarchy again for every one of those is a few thousand searches and a few
        /// thousand allocations per frame, on the editor thread, in the scene that was
        /// already the slow one.
        /// </summary>
        Dictionary<int, List<Renderer>> _propsByTile;

        /// <summary>
        /// Metres flown, per tile, at the moment the bird is nearest to it — or -1 for
        /// ground she never reaches.
        ///
        /// Taken from the flight the simulation worked out rather than recomputed, so
        /// what the map ends up showing is exactly what the ability grants. Nearest
        /// point rather than first sighting: a tile off to one side should come out of
        /// the fog as she draws level with it, not when the edge of her sight first
        /// clips it.
        /// </summary>
        float[] _revealAt;

        readonly HashSet<int> _revealed = new HashSet<int>();

        /// <summary>
        /// How clear each ground *corner* is, from 0 for fog to 1 for fully seen.
        ///
        /// A tile used to be one or the other, which drew the flight as a stencil: a
        /// chewed hard edge in the shape of the tiles the sim happened to mark. Knowledge
        /// does not stop at a four-metre boundary. It thins out, so the edge does too.
        ///
        /// Held per corner and not per tile, and that is what makes the thinning visible.
        /// Feathered over the tiles it still came out in four-metre squares of one flat
        /// value each — the map is 64 tiles across and drawn from four hundred metres
        /// back, so a tile is about sixteen screen pixels, and the fog's edge was a
        /// staircase of them. The ground's own colours are already carried on the corners
        /// (<c>TerrainMeshBuilder.CornerColor</c>) and interpolated smoothly across each
        /// quad; putting the fog on the same corners lets it ride the same interpolation
        /// instead of fighting it.
        ///
        /// Indexed <c>y * (Width + 1) + x</c>: one more corner than tile in each
        /// direction.
        /// </summary>
        float[] _clarity;

        /// <summary>Corners across the map, which is one more than tiles.</summary>
        int _cornersWide;

        /// <summary>Reused rather than allocated per prop per frame.</summary>
        // Built on first use rather than here. A MonoBehaviour's fields are initialised
        // while the object is being deserialised, off the main thread, and anything that
        // reaches into the engine there throws — a MaterialPropertyBlock does. The
        // exception is raised against the component's constructor, so it reads as a
        // problem with the class rather than with one line of it, and the component never
        // starts at all: the whole preview scene comes up empty for a property block.
        MaterialPropertyBlock _block;

        MaterialPropertyBlock Block => _block ?? (_block = new MaterialPropertyBlock());

        /// <summary>Whether the bird has been round the whole flight at least once.</summary>
        bool _lapped;

        /// <summary>How many enemy marks are drawn, so the marks are rebuilt only on a change.</summary>
        int _marked;

        /// <summary>Held for the marker rebuilds a progressive reveal asks for.</summary>
        LevelMap _map;

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

        /// <summary>The level being shown, for whoever has to draw a route across it.</summary>
        public LevelMap Map => _map;
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

            FlyEagle(Elapsed(Time.deltaTime));
        }

        /// <summary>Wall-clock reading at the last tick, outside play mode.</summary>
        double _ticked;

        /// <summary>
        /// The most one frame is allowed to move her, in seconds.
        ///
        /// A tenth of a second. The bird travels at 40 m/s, so a gap of that length is
        /// already four metres of sky, and advancing her by a gap of thirty seconds —
        /// which is what an editor coming back from a recompile hands over — would
        /// teleport her rather than move her.
        ///
        /// A ceiling rather than a rejection. Rejecting long frames looked reasonable
        /// and was not: this scene is heavy enough that most frames are long, so almost
        /// every one was thrown away and the flight barely advanced at all.
        /// </summary>
        const double MaxTick = 0.1;

        /// <summary>
        /// How much time really passed since the last tick.
        ///
        /// `Time.deltaTime` means nothing outside play mode — there is no game loop for
        /// it to measure, and what it hands back is whatever the last one left there. The
        /// bird was being advanced by a number unrelated to the time she had had, which
        /// is exactly the shape of the complaint: motion that lags and lurches while the
        /// editor itself is keeping up perfectly well.
        ///
        /// `EditorApplication.timeSinceStartup` is a real clock and is the one thing in
        /// the editor that can be trusted to say how long anything took.
        /// </summary>
        float Elapsed(float deltaTime)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                double now = UnityEditor.EditorApplication.timeSinceStartup;
                double since = now - _ticked;
                _ticked = now;

                // Clamped, not dropped, and the difference is the whole of whether this
                // works. The preview scene carries six thousand props and the editor
                // redraws all of it every tick, so a frame here can easily take longer
                // than the cap — and dropping those meant dropping nearly all of them.
                // The bird crawled, the fog crept forward on the occasional fast frame,
                // and the map came out green in patches.
                //
                // Clamping slows playback on a slow machine instead of stalling it. The
                // first tick still has nothing to measure from.
                if (since <= 0d) return 0f;

                return (float)(since > MaxTick ? MaxTick : since);
            }
#endif
            return deltaTime;
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

            // Down, and staying down. Above the editor's redraw request on purpose: a
            // landed bird should stop asking the editor to draw another frame, or looking
            // at a finished plan spins the loop for nothing.
            if (_lapped) return;

#if UNITY_EDITOR
            // Queued before the delta is looked at, and that ordering is the whole of it.
            //
            // The editor does not run a game loop. [ExecuteAlways] gets an Update when
            // something asks the editor to redraw — a mouse crossing the scene view, an
            // inspector edit — and nothing asks while you sit and look at it. Asking for
            // the next tick keeps it going.
            //
            // Below the delta check it could not: outside play mode Time.deltaTime is
            // zero until a loop is running, so the one frame that could have started the
            // loop returned before queueing anything and the bird stayed where it was.
            if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif

            if (deltaTime <= 0f) return;
            if (_milestones == null || _flightLength <= 0f) return;

            // Clamped at the end of the flight, not wrapped round to the start.
            //
            // Mathf.Repeat sent her back to the beginning the moment she reached the end,
            // so she flew the same circuit over and over. The ability is one flight: she
            // is aloft for her seconds, she looks at what she looks at, and she is done.
            // A bird that sets off again has nothing left to find — the fog she would
            // lift is already lifted — so the second lap is a decoration that contradicts
            // what the plan is telling you.
            _flown = Mathf.Min(_flown + deltaTime * ScoutingAbility.Speed, _flightLength);

            if (_flown >= _flightLength && !_lapped)
            {
                _lapped = true;

                // The one moment worth a line: from here the fog should be off every
                // tile the flight covers, and "should" is a thing that can be checked.
                Debug.Log($"[Arna] The eagle has flown the whole flight. Coverage "
                          + $"{_flight.Coverage} tiles of {_grid.TileCount}; the fog is off "
                          + $"{_revealed.Count} so far and lifts off the rest this frame.");
            }

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

            Reveal(_flown);
        }

        /// <summary>
        /// Marks what the player is allowed to know (docs/GDD.md §3.4, §3.6).
        ///
        /// Two different kinds of knowledge, and the difference between them is most of
        /// the design.
        ///
        /// <b>The crows are always drawn.</b> They cost nothing, they are visible from
        /// the first moment, and one in five is lying — so what they buy is a shortlist
        /// of ground worth worrying about, never an answer. The wrecks and bone piles
        /// the decorator scatters over trap fields do the same job in the same spirit;
        /// they are already there, as scenery.
        ///
        /// <b>Groups are drawn only where the eagle flew.</b> That is what the ability
        /// is: a quarter of the country turned from *something is out there* into *four
        /// of them are standing here*, which is the difference between a worry and a
        /// route drawn around it. Unflown ground keeps its crows and keeps its silence.
        /// </summary>
        void BuildMarkers(LevelMap map)
        {
            Clear("Markers");
            _markers = null;

            if (_markerMesh != null)
            {
                if (Application.isPlaying) Destroy(_markerMesh);
                else DestroyImmediate(_markerMesh);

                _markerMesh = null;
            }

            if (RouteMaterial == null) return;

            var marks = new List<MapMarkerBuilder.Marker>();

            // The crows are no longer a disc here — they are a symbol, in BuildSymbols
            // below. A near-black mark two metres across on a dark forest, seen from four
            // hundred metres up, was invisible; the intent behind it was right and the
            // execution deleted the signal. What carries "hint, not fact" now is the
            // shape: a hollow ring with a bird in it against the enemy's filled red disc.

            // Only where she has already been. RevealedEnemies is what the whole flight
            // finds; _revealed is what it has found so far, and the difference between
            // them is the difference between a map with the answer printed on it and a
            // map you watch being drawn.
            _marked = 0;

            if (_flight != null)
                foreach (int index in _flight.RevealedEnemies)
                {
                    int tile = map.Encounters.Enemies[index].Tile;
                    if (!_revealed.Contains(tile)) continue;

                    marks.Add(new MapMarkerBuilder.Marker(tile, EnemyMarker, EnemyMarkerRadius));
                    _marked++;
                }

            _markerMesh = MapMarkerBuilder.Build(map.Grid, marks, HeightScale);
            if (_markerMesh == null) return;

            var go = new GameObject("Markers");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _markerMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = RouteMaterial;

            // A mark on a map neither casts nor catches shadow. It is not in the world.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _markers = go.transform;
        }

        /// <summary>
        /// Puts a readable sign on every built thing and every flock of crows.
        ///
        /// A house and a ruin are the same brown smudge from map height, and the map had
        /// no way to tell them apart because the decorator returned a count and threw the
        /// rest away. It reports now, so this can draw a gable over one and a broken wall
        /// over the other. See <see cref="MapSymbols"/>.
        ///
        /// Not fogged. What is *built* on this country is knowledge had by looking at it
        /// — the same argument that already exempts a wrecked cart and a raiders' tent
        /// from the overlay in <see cref="ApplyOverlay"/>. What the fog is for is hiding
        /// where the enemies are now.
        /// </summary>
        void BuildSymbols(LevelMap map)
        {
            Clear("Symbols");
            _symbols = null;

            if (_symbolMesh != null)
            {
                if (Application.isPlaying) Destroy(_symbolMesh);
                else DestroyImmediate(_symbolMesh);

                _symbolMesh = null;
            }

            if (!ShowSymbols) return;

            var signs = new List<MapSymbols.Sign>();

            if (_landmarks != null)
                foreach (var landmark in _landmarks)
                {
                    int slot = MapSymbols.SlotOf(landmark.Kind);
                    if (slot >= 0) signs.Add(new MapSymbols.Sign(slot, landmark.Tile));
                }

            // Truthful and lying flocks are marked identically, and that is the design
            // rather than an oversight: a signal you can tell is false is not a false
            // positive, it is a second true one.
            foreach (var flock in CrowSignal.Place(map))
                signs.Add(MapSymbols.Crows(flock.Tile));

            var facing = Camera.main != null
                ? Camera.main.transform.rotation
                : Quaternion.Euler(55f, 0f, 0f);

            _symbolMesh = MapSymbols.Build(map.Grid, signs, HeightScale, facing);
            _symbols = MapSymbols.Show(_symbolMesh, transform);

            Debug.Log($"[Arna] Plan {Chapter}-{Level}: {signs.Count} map symbols "
                      + $"({_landmarks?.Count ?? 0} landmarks reported).");
        }

        /// <summary>
        /// Mutes the ground and the scenery over every tile the bird did not reach.
        ///
        /// Two different mechanisms for the same effect, because the two things are made
        /// differently. The ground is a mesh this project builds — four vertices per
        /// tile, in tile order — so its colours can be pushed all the way to luminance.
        /// A tree is somebody else's prefab with somebody else's material, and the only
        /// handle available without writing a shader is a property block that multiplies
        /// the atlas, which darkens and cannot desaturate. See
        /// <see cref="PlanningOverlay.PropLight"/>.
        /// </summary>
        void ApplyOverlay(Mesh mesh, LevelMap map)
        {
            _lit = null;
            _shown = null;
            _overlay = null;
            _propsByTile = null;
            _clarity = null;

            if (!ShowOverlay || mesh == null) return;

            _overlay = mesh;

            var colours = mesh.colors;

            // Four vertices per tile and tiles in order, which is what makes this cheap:
            // no lookup from a vertex back to the ground under it.
            _cornersWide = map.Grid.Width + 1;
            _clarity = new float[_cornersWide * (map.Grid.Height + 1)];

            if (colours != null && colours.Length == map.Grid.TileCount * 4)
            {
                _lit = colours;
                _shown = new Color[colours.Length];

                for (int v = 0; v < colours.Length; v++)
                    _shown[v] = PlanningOverlay.Mute(colours[v]);

                mesh.SetColors(_shown);
            }

            if (_props == null) return;

            // Filed by tile on the way past, because a reveal has to find the props on
            // one tile out of four thousand and cannot walk six thousand props to do it.
            _propsByTile = new Dictionary<int, List<Renderer>>();

            var shade = new Color(PlanningOverlay.PropLight, PlanningOverlay.PropLight,
                                  PlanningOverlay.PropLight, 1f);

            foreach (Transform prop in _props)
            {
                // The signals keep their colours and are not filed for the reveal.
                //
                // A wrecked cart, a bone pile, a banner, a raiders' tent: these are the
                // whole of what the planning map offers before the bird flies, and they
                // were painted down to the same flat grey as every tree on the map. The
                // player was looking at four thousand grey tiles and being asked to pick
                // a route through them. What they mark is ground somebody else disturbed,
                // which is knowledge had by looking at the country; what the fog is for
                // is hiding where the enemies are now.
                if (prop.GetComponent<Signal>() != null) continue;

                var at = prop.position;
                int x = Mathf.FloorToInt(at.x / TileGrid.TileSize);
                int y = Mathf.FloorToInt(at.z / TileGrid.TileSize);

                List<Renderer> standing = null;

                if (map.Grid.InBounds(x, y))
                {
                    int tile = map.Grid.ToIndex(x, y);

                    if (!_propsByTile.TryGetValue(tile, out standing))
                        _propsByTile[tile] = standing = new List<Renderer>();
                }

                foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
                {
                    standing?.Add(renderer);

                    renderer.GetPropertyBlock(Block);
                    Block.SetColor(BaseColor, shade);
                    renderer.SetPropertyBlock(Block);
                }
            }
        }

        /// <summary>
        /// How far behind the bird the fog lifts, in metres.
        ///
        /// Half a second at her 40 m/s. The reveal is meant to trail her rather than
        /// travel with her: ground going clear under the bird reads as the bird being
        /// made of light, where ground going clear behind her reads as her having looked
        /// at it — which is what the ability actually is.
        /// </summary>
        const float RevealLag = 20f;

        /// <summary>
        /// Lifts the fog off everything the bird has flown past.
        ///
        /// The map starts grey — all of it — and only ground she has been over comes
        /// out of it (docs/GDD.md §3.4). That was already the end state; what was wrong
        /// is that the whole flight's worth of it was applied before she had flown a
        /// metre, so the map opened with the answer on it and the bird was a decoration
        /// crossing ground that had already told you everything.
        ///
        /// Revealed ground stays revealed once she has passed. A map that re-fogs behind
        /// the bird is one you cannot plan on.
        /// </summary>
        void Reveal(float flown)
        {
            if (_revealAt == null || _map == null) return;

            // The lag means the last stretch of the flight would never come due on its
            // own: _flown stops at the full length and never reaches length + lag. Once
            // she has landed, everything she flew over is behind her.
            float reached = _lapped ? _flightLength + RevealLag : flown;

            bool ground = false;

            for (int tile = 0; tile < _revealAt.Length; tile++)
            {
                if (_revealAt[tile] < 0f || _revealAt[tile] + RevealLag > reached) continue;
                if (!_revealed.Add(tile)) continue;

                ground |= Clarify(tile);
            }

            if (!ground) return;

            if (_overlay != null && _shown != null) _overlay.SetColors(_shown);

            // A group is drawn where she found it, so the marks have to follow the fog.
            // Rebuilt only when the count moves, which is a handful of times a flight.
            //
            // Counted exactly the way BuildMarkers counts, or the two could disagree by
            // one for ever and rebuild the marker mesh on every single frame.
            int found = 0;

            if (_flight != null)
                foreach (int index in _flight.RevealedEnemies)
                    if (_revealed.Contains(_map.Encounters.Enemies[index].Tile)) found++;

            if (found != _marked) BuildMarkers(_map);
        }

        /// <summary>
        /// How far a revealed tile's clarity carries into the fog around it, in tiles.
        ///
        /// Two and a half, so the edge is about ten metres wide on a map that is 256
        /// across. Less than that and it is still a stencil with a soft line drawn on it;
        /// much more and the flight stops having a shape at all, which is the one thing
        /// the player is reading it for.
        /// </summary>
        const float FogFeather = 2.5f;

        /// <summary>
        /// Carries one newly seen tile's clarity out into its neighbours, and repaints
        /// whatever that brightens.
        ///
        /// Clarity only ever rises, which is what makes this cheap: each neighbour keeps
        /// the best claim anything has made on it, so the work is a fixed twenty-five
        /// tiles per reveal rather than a blur over the whole map every frame.
        /// </summary>
        bool Clarify(int tile)
        {
            if (_clarity == null || _map == null) return false;

            var grid = _map.Grid;
            grid.ToCoords(tile, out int cx, out int cy);

            int reach = Mathf.CeilToInt(FogFeather);
            bool changed = false;

            // Corner offsets run one further than tile offsets, because tile (tx, ty) is
            // bounded by corners (tx, ty) through (tx + 1, ty + 1).
            //
            // Distance is measured from the tile's *edge*, not its middle: nought on the
            // tile's own four corners and one per tile outward from there. Measured from
            // the middle instead — the obvious version, and the one written first — the
            // nearest corner is 0.707 tiles away, so ground the bird flew directly over
            // would have topped out at 72% clear and the map would never have come fully
            // out of the fog at all.
            for (int dy = -reach; dy <= reach + 1; dy++)
                for (int dx = -reach; dx <= reach + 1; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || y < 0 || x > grid.Width || y > grid.Height) continue;

                    float ox = Mathf.Max(0f, Mathf.Abs(dx - 0.5f) - 0.5f);
                    float oy = Mathf.Max(0f, Mathf.Abs(dy - 0.5f) - 0.5f);

                    float clarity = 1f - Mathf.Sqrt(ox * ox + oy * oy) / FogFeather;
                    if (clarity <= 0f) continue;

                    int corner = y * _cornersWide + x;
                    if (clarity <= _clarity[corner]) continue;

                    _clarity[corner] = clarity;
                    changed = true;
                }

            if (!changed) return false;

            // Every tile whose corners could have moved, repainted once. Painting from
            // inside the corner loop instead would redraw each tile up to four times, and
            // three of those would be reading corners that had not been raised yet.
            //
            // One tile further back than the corner loop reached, because the tile at
            // -reach - 1 has its far corner at -reach and would otherwise be left holding
            // a corner that had just brightened under it.
            for (int dy = -reach - 1; dy <= reach; dy++)
                for (int dx = -reach - 1; dx <= reach; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (!grid.InBounds(x, y)) continue;

                    Paint(grid.ToIndex(x, y));
                }

            return true;
        }

        /// <summary>Puts one tile's ground and scenery at the clarity its corners have earned.</summary>
        void Paint(int tile)
        {
            _map.Grid.ToCoords(tile, out int x, out int y);

            // The four corners in the order TerrainMeshBuilder lays its vertices down:
            // near-left, near-right, far-right, far-left.
            float c00 = Clarity(x, y);
            float c10 = Clarity(x + 1, y);
            float c11 = Clarity(x + 1, y + 1);
            float c01 = Clarity(x, y + 1);

            if (_shown != null && _lit != null)
            {
                int v = tile * 4;
                Shade(v + 0, c00);
                Shade(v + 1, c10);
                Shade(v + 2, c11);
                Shade(v + 3, c01);
            }

            // One tile's worth for the scenery standing on it. A tree has one colour
            // however finely the ground under it is graded, so it takes the average of
            // the four corners rather than picking one of them.
            float clarity = (c00 + c10 + c11 + c01) * 0.25f;

            if (_propsByTile == null) return;
            if (!_propsByTile.TryGetValue(tile, out var standing)) return;

            // Cleared rather than set at full clarity, so a prop ends up with the colour
            // its own material gives it instead of one multiplied by very nearly white.
            bool clear = clarity >= 0.999f;

            float light = Mathf.Lerp(PlanningOverlay.PropLight, 1f, clarity);
            var shade = new Color(light, light, light, 1f);

            foreach (var renderer in standing)
            {
                if (renderer == null) continue;

                if (clear)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }

                renderer.GetPropertyBlock(Block);
                Block.SetColor(BaseColor, shade);
                renderer.SetPropertyBlock(Block);
            }
        }

        /// <summary>One ground corner's clarity, or nought where there is no such corner.</summary>
        float Clarity(int cornerX, int cornerY)
        {
            if (_clarity == null) return 0f;
            if (cornerX < 0 || cornerY < 0) return 0f;
            if (cornerX >= _cornersWide) return 0f;

            int index = cornerY * _cornersWide + cornerX;
            return index < _clarity.Length ? _clarity[index] : 0f;
        }

        /// <summary>Sets one ground vertex between its fogged and its lit colour.</summary>
        void Shade(int vertex, float clarity)
            => _shown[vertex] = Color.Lerp(PlanningOverlay.Mute(_lit[vertex]), _lit[vertex], clarity);

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

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
            Clear("Eagle");
            _eagleRoot = null;
            _eagle = null;

            _cast = null;
            _flight = null;
            _milestones = null;
            _revealAt = null;
            _flightLength = 0f;
            _flown = 0f;
            _grid = map.Grid;
            _map = map;

            // Cleared here rather than in ApplyOverlay, because this runs before the
            // marks are built and they ask what has been revealed.
            _revealed.Clear();
            _marked = 0;
            _lapped = false;

            if (!ShowEagle) return;

            if (Models == null || !Models.Eagle.HasModel)
            {
                Debug.LogWarning("[Arna] No eagle model — run Arna > Setup Project. "
                                 + "The scene predates the bird, or the path into "
                                 + "Assets/ThirdParty/Eagle is wrong.");
                return;
            }

            // Said out loud, because a bird with no controller is a bird that glides:
            // SpawnActor attaches no animator when there is none to attach, and the
            // result is a model holding its bind pose while it moves across the map.
            // Which looks like the flight being wrong rather than the wings being absent.
            if (Models.Eagle.Animator == null)
                Debug.LogWarning("[Arna] The eagle has no animator controller, so her wings "
                                 + "will not beat. Run Arna > Build Animator Controllers and "
                                 + "check the summary says 12 of 12 — if it names Eagle_B1, "
                                 + "the warning above it says why.");

            _flight = ScoutingAbility.Fly(map);
            if (_flight.Path.Count < 2) return;

            _milestones = new float[_flight.Path.Count];
            for (int i = 1; i < _flight.Path.Count; i++)
                _milestones[i] = _milestones[i - 1]
                                 + Vec2.Distance(_flight.Path[i - 1], _flight.Path[i]);

            _flightLength = _milestones[_milestones.Length - 1];

            // A thousand tiles against a couple of hundred path points, once per rebuild.
            // Cheap enough to do plainly, and plainly is worth more here than clever:
            // every tile the flight reveals, filed under how far she has flown by the
            // time she is closest to it.
            _revealAt = new float[map.Grid.TileCount];
            for (int i = 0; i < _revealAt.Length; i++) _revealAt[i] = -1f;

            foreach (int tile in _flight.RevealedTiles)
            {
                var at = Vec2.FromTile(map.Grid, tile);

                float nearest = float.MaxValue;
                float when = 0f;

                for (int i = 0; i < _flight.Path.Count; i++)
                {
                    float gap = Vec2.Distance(_flight.Path[i], at);
                    if (gap >= nearest) continue;

                    nearest = gap;
                    when = _milestones[i];
                }

                _revealAt[tile] = when;
            }

            // With the fog switched off there is nothing to lift, so nothing should be
            // waiting on the bird either: everything she finds is marked from the start.
            if (!ShowOverlay)
                foreach (int tile in _flight.RevealedTiles) _revealed.Add(tile);

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

            // After the eagle, because what it marks is what she found.
            BuildMarkers(map);

            // After the props, because it draws signs for what they turned out to be.
            BuildSymbols(map);

            // Last, because it reads what the eagle found and repaints what the other
            // three built.
            ApplyOverlay(mesh, map);

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

        /// <summary>
        /// Removes a build root by name, and every stray copy of it.
        ///
        /// By name rather than by reference, because the references cannot be trusted:
        /// `_props`, `_routes`, `_markers` and `_eagleRoot` are private fields with no
        /// [SerializeField] on them, so Unity does not serialize them. Opening the scene
        /// hands them all back null while the GameObjects they pointed at are sitting in
        /// it exactly where they were saved — and the rebuild that `OnEnable` asks for
        /// then builds a second `Props` beside the first and leaves it there.
        ///
        /// It compounds, and it compounds invisibly: every save keeps every generation
        /// the scene has ever built. That is where the mountain in the middle of the map
        /// came from, and where the scenery beyond the map's edge came from — a horizon
        /// ring built before the planning view stopped asking for one, orphaned by a
        /// scene load and then saved back into the scene by the next thing that saved it.
        ///
        /// Serializing the fields would mend the reference and not the strays already
        /// there. This mends both, on every rebuild, for good.
        /// </summary>
        int Clear(string name)
        {
            int removed = 0;

            // Backwards: destroying a child immediately reindexes the ones after it.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != name) continue;

                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);

                removed++;
            }

            return removed;
        }

        void BuildProps(LevelMap map)
        {
            // Asked before the clearing, not after. DestroyImmediate makes a reference
            // read as null the moment it runs, so counting afterwards would report the
            // root this rebuild legitimately replaced as one more stray every time.
            bool had = _props != null;

            int strays = Clear("Props") - (had ? 1 : 0);
            _props = null;

            if (strays > 0)
                Debug.LogWarning($"[Arna] {strays} abandoned prop root(s) cleared out of the "
                                 + "preview. They were built by an earlier run, orphaned by a "
                                 + "scene load and saved back into the scene — see "
                                 + "LevelPreview.Clear.");

            if (Decor == null || Decor.IsEmpty) return;

            _props = new GameObject("Props").transform;
            _props.SetParent(transform, false);

            // The receipt for what was built, so the map can put a sign on each of them.
            // Nothing about the placement changes; this is the decorator saying out loud
            // what it was already deciding.
            _landmarks = new List<Landmark>();

            int placed = TerrainDecorator.Decorate(_props, map.Grid, map.Seed, Decor,
                keepClear: CorridorTiles(map), heightScale: HeightScale,
                maxProps: MaxProps, densityScale: DensityScale,
                ruinSites: TrapSigns.Sites(map), horizon: false,
                campSites: CampSignal.Tiles(map), travelled: Travelled(map),
                found: _landmarks, goalTile: map.GoalIndex);

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
            Clear("Routes");
            _routes = null;

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

        /// <summary>
        /// Every tile any corridor crosses — the country's natural ways through, which is
        /// where people build. Unlike <see cref="CorridorTiles"/> this is not gated on
        /// ShowCorridors: the corridors stay hidden, the houses standing along them do not.
        /// </summary>
        internal static HashSet<int> Travelled(LevelMap map)
        {
            var tiles = new HashSet<int>();
            if (map?.Corridors == null) return tiles;

            foreach (var corridor in map.Corridors)
                foreach (int tile in corridor.Tiles)
                    tiles.Add(tile);

            return tiles;
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

            if (_symbols != null)
            {
                if (Application.isPlaying) Destroy(_symbols.gameObject);
                else DestroyImmediate(_symbols.gameObject);
                _symbols = null;
            }

            if (_symbolMesh != null)
            {
                if (Application.isPlaying) Destroy(_symbolMesh);
                else DestroyImmediate(_symbolMesh);
                _symbolMesh = null;
            }

            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}
