using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using Arna.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arna.App
{
    [System.Serializable]
    public struct SlotAssignment
    {
        public bool Occupied;
        public TroopKind Kind;
    }

    /// <summary>
    /// Press Play and watch a level run.
    ///
    /// Generates the map, hands the chosen corridor to the simulation and draws what
    /// the simulation is doing. This is the first point at which the design can be
    /// judged rather than reasoned about: whether a minute and a half is the right
    /// length, whether the marsh detour feels as slow as the numbers say, whether
    /// seeing a group fade in ahead of the column reads as a warning or a surprise.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LevelRunner : MonoBehaviour
    {
        [Header("Level")]
        [Min(1)] public int Chapter = 1;
        [Min(1)] public int Level = 1;
        public CorridorKind Route = CorridorKind.Fast;

        [Header("Playback")]
        [Range(0.25f, 8f)] public float TimeScale = 1f;

        [Header("World")]
        /// <summary>Metres between the lowest and highest ground. Zero gives the flat planning map.</summary>
        [Range(0f, 40f)] public float HeightScale = 14f;

        [Range(200, 4000)] public int MaxProps = 2200;

        [Header("Camera")]
        public bool FollowCaravan = true;

        /// <summary>
        /// Close enough that a knight reads as a knight. At ninety metres the column
        /// was a few grey specks against nine-metre trees — technically correct and
        /// useless for judging anything.
        /// </summary>
        [Range(15f, 400f)] public float FollowDistance = 46f;

        /// <summary>
        /// High enough to see over the canopy. At twenty-two metres the camera sat
        /// inside the forest and half the frame was the back of a tree.
        /// </summary>
        [Range(8f, 300f)] public float FollowHeight = 32f;

        /// <summary>
        /// Lets the player pinch to zoom and drag to swing the camera round.
        ///
        /// Off gives the fixed view the design was measured from, which is what a
        /// screenshot or a comparison wants. On is what shipping wants: a fixed camera
        /// in a game about reading terrain is a game that decides for you what you are
        /// allowed to look at.
        /// </summary>
        public bool PlayerControlsCamera = true;

        [Range(0.05f, 1f)] public float OrbitSensitivity = 0.25f;

        [Header("Models")]
        public VisualLibrary Models = new VisualLibrary();

        [Header("Scenery")]
        public BiomeDecor Decor = new BiomeDecor();

        [Header("Escort")]
        public SlotAssignment[] Formation =
        {
            new SlotAssignment { Occupied = true, Kind = TroopKind.Spearmen },     // Van
            new SlotAssignment { Occupied = true, Kind = TroopKind.Archers },      // RightVan
            new SlotAssignment { Occupied = false, Kind = TroopKind.Swordsmen },   // RightRear
            new SlotAssignment { Occupied = true, Kind = TroopKind.Swordsmen },    // Rear
            new SlotAssignment { Occupied = false, Kind = TroopKind.Shieldbearer },// LeftRear
            new SlotAssignment { Occupied = true, Kind = TroopKind.Scout }         // LeftVan
        };

        LevelRun _run;
        TileGrid _levelGrid;
        RunVisuals _visuals;
        Transform _markerRoot;
        List<WildAnimal> _wildlife;
        readonly CameraOrbit _orbit = new CameraOrbit();
        float _pinchDistance;
        readonly List<Vec2> _battles = new List<Vec2>();
        Camera _camera;
        Vector3 _cameraOffset;
        Mesh _mesh;

        public LevelRun Run => _run;

        void Start() => Restart();

        [ContextMenu("Restart")]
        public void Restart()
        {
            Cleanup();

            var chapter = new ChapterRecipe();
            var recipe = chapter.ForLevel(Level);
            var map = TerrainGenerator.Generate(recipe, DeterministicRandom.SeedFor(Chapter, Level));

            var corridor = map.CorridorOf(Route) ?? map.Corridors[0];

            var squad = new Squad(recipe.SquadBudget);
            for (int i = 0; i < Formation.Length && i < 6; i++)
                if (Formation[i].Occupied) squad.TryPlace((FormationSlot)i, Formation[i].Kind);

            _run = new LevelRun(map, corridor.Tiles, squad, recipe.EnemyStrength);
            _levelGrid = map.Grid;

            // No map furniture in the play view. The drawn line belongs to the planning
            // map and painting it across the ground here would read as a road that is
            // not there; the start and goal markers likewise came out as coloured
            // patches of grass the caravan happened to be standing on.
            _mesh = TerrainMeshBuilder.Build(map.Grid, TileGrid.TileSize,
                null, -1, -1, HeightScale);
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _markerRoot = new GameObject("Markers").transform;
            _markerRoot.SetParent(transform, false);

            // Nothing is kept *clear* — there is no line to bury, and a forest should
            // look like a forest. But nothing stands in the wagons' way either: the
            // route refuses anything two metres or taller, so the grass, flowers,
            // bushes and loose stones stay where they fall and the boulders and trees
            // do not stand in the road. See TerrainDecorator.DriveClearance.
            TerrainDecorator.Decorate(_markerRoot, map.Grid, map.Seed, Decor,
                keepClear: null, heightScale: HeightScale, maxProps: MaxProps,
                driveLine: corridor.Tiles);

            _visuals = new RunVisuals(_markerRoot, map.Grid, HeightScale) { Library = Models };
            _visuals.Build(_run);
            _visuals.BuildCaches(map.Encounters.SilverCaches, map.Grid);
            _visuals.BuildCrowFlocks(CrowSignal.Place(map), map.Grid);

            _wildlife = Wildlife.Populate(map);
            _visuals.BuildWildlife(_wildlife);
            _visuals.Sync(_run);

            _camera = Camera.main;
            AimCamera();
        }

        /// <summary>
        /// Places the camera behind and above the column.
        ///
        /// The planning map frames the whole 256-metre board, which is right for
        /// comparing routes and hopeless for looking at anything: a knight is ten
        /// pixels tall from up there. The play view trades that overview away for a
        /// distance where models and trees actually read.
        /// </summary>
        public void AimCamera()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || !FollowCaravan) return;

            var heading = _run.Caravan.Heading;

            if (PlayerControlsCamera)
            {
                _orbit.Offset(heading.X, heading.Y, out float ox, out float oy, out float oz);
                _cameraOffset = new Vector3(ox, oy, oz);
            }
            else
            {
                // The fixed view, straight from the inspector fields. Kept because every
                // measurement in the design notes was taken from it, and a screenshot
                // that quietly used a dragged camera would compare nothing to nothing.
                _cameraOffset = new Vector3(-heading.X, 0f, -heading.Y) * FollowDistance
                                + Vector3.up * FollowHeight;
            }

            _camera.orthographic = false;
            _camera.transform.position = CaravanWorldPosition() + _cameraOffset;
            _camera.transform.LookAt(CaravanWorldPosition() + Vector3.up * 4f);
        }

        /// <summary>
        /// Reads the two gestures the camera answers to: pinch to zoom, drag to swing.
        ///
        /// Polled off the devices rather than through an action asset. There is no input
        /// asset in this project yet, and inventing one for two gestures would put a
        /// file between the code and the thing it does for no gain — when the game grows
        /// a real control scheme, this moves into it.
        ///
        /// Both backends are enabled in the project settings, so every device here can
        /// be null on a machine that has none of it. Each is checked.
        /// </summary>
        void ReadCameraInput()
        {
            if (!PlayerControlsCamera) return;

            var touch = Touchscreen.current;
            if (touch != null && touch.touches.Count > 0)
            {
                ReadTouch(touch);
                return;
            }

            // Mouse, so the thing can be worked on in the editor without a phone.
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f) _orbit.Zoom(scroll > 0f ? 0.9f : 1f / 0.9f);

            if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _orbit.Orbit(delta.x * OrbitSensitivity, -delta.y * OrbitSensitivity);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) _orbit.Reset();
        }

        void ReadTouch(Touchscreen touch)
        {
            int down = 0;
            Vector2 first = default, second = default, drag = default;

            foreach (var finger in touch.touches)
            {
                if (!finger.press.isPressed) continue;

                if (down == 0) { first = finger.position.ReadValue(); drag = finger.delta.ReadValue(); }
                else if (down == 1) second = finger.position.ReadValue();
                down++;
            }

            // Two fingers is a pinch and nothing else. Letting it also orbit would make
            // every zoom a small unintended swing, which is the usual reason a mobile
            // camera feels slippery.
            if (down >= 2)
            {
                float spread = Vector2.Distance(first, second);

                if (_pinchDistance > 0f && spread > 0f)
                    _orbit.Zoom(_pinchDistance / spread);

                _pinchDistance = spread;
                return;
            }

            _pinchDistance = 0f;
            if (down == 1)
                _orbit.Orbit(drag.x * OrbitSensitivity, -drag.y * OrbitSensitivity);
        }

        /// <summary>Back to the view the game is balanced for.</summary>
        public void ResetCamera() => _orbit.Reset();

        /// <summary>
        /// Moves the wildlife on, and tells it where the fighting is.
        ///
        /// Every fight in this game happens at the caravan — the escort is what the
        /// enemies come for — so the battle position is the caravan's. The two radii
        /// still differ and still matter: the same spot startles animals out to 55 m
        /// once blades are out and only 26 m while the carts are merely rolling past,
        /// so contact makes a visibly wider ring of the country break cover.
        /// </summary>
        void StepWildlife(float dt)
        {
            if (_wildlife == null) return;

            _battles.Clear();
            if (_run?.Combat != null && _run.Combat.InContact)
                _battles.Add(_run.Caravan.LeadPosition);

            Wildlife.Step(_wildlife, _run.Caravan.LeadPosition, _battles, dt);
        }

        void Update()
        {
            if (_run == null) return;

            if (_run.Outcome == RunOutcome.InProgress)
                _run.Advance(Time.deltaTime * TimeScale);

            ReadCameraInput();
            StepWildlife(Time.deltaTime * TimeScale);
            _visuals.Sync(_run);
            _visuals.SyncWildlife(_wildlife);
            AimCamera();
        }

        Vector3 CaravanWorldPosition()
        {
            var position = _run.Caravan.LeadPosition;
            float ground = _levelGrid != null && HeightScale > 0f
                ? _levelGrid.SurfaceElevation(position.X, position.Y) * HeightScale
                : 0f;

            return new Vector3(position.X, ground, position.Y);
        }

        /// <summary>
        /// Also runs outside play mode, so a level can be built and rendered from a
        /// headless editor session. Destroy is play-mode only, hence the split.
        /// </summary>
        void Cleanup()
        {
            if (_markerRoot != null)
            {
                if (Application.isPlaying) Destroy(_markerRoot.gameObject);
                else DestroyImmediate(_markerRoot.gameObject);
            }

            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }

            _run = null;
        }

        /// <summary>Advances the simulation by whole steps. Used by the headless capture.</summary>
        public void StepTimes(int steps)
        {
            if (_run == null) return;

            for (int i = 0; i < steps && _run.Outcome == RunOutcome.InProgress; i++) _run.Step();
            _visuals.Sync(_run);
            _visuals.SyncWildlife(_wildlife);

            // Animators do not tick outside play mode, so a capture would show bind
            // pose. Advancing a little lands the actors mid-stride instead.
            _visuals.AdvanceAnimators(0.4f);
        }

        void OnDestroy() => Cleanup();

        /// <summary>
        /// A readout rather than a user interface. The real HUD is a later phase; this
        /// exists so the numbers can be watched while the level plays.
        /// </summary>
        void OnGUI()
        {
            if (_run == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            GUILayout.BeginArea(new Rect(14, 14, 380, 320), GUI.skin.box);

            GUILayout.Label($"<b>{Chapter}-{Level}</b>   {Route} route", style);
            // Two clocks, because they answer different questions and the player is
            // scored on the second. Wall-clock is how long the run took; travel time is
            // the route, with the fighting taken out, and that is what par measures.
            GUILayout.Label($"Tid  {_run.ElapsedSeconds:F1} s", style);
            GUILayout.Label($"Restid  {_run.TravelSeconds:F1} s   (par {_run.ParSeconds:F0} s)" +
                            $"   strid {_run.FightingSeconds:F1} s", style);
            GUILayout.Label($"Sträcka  {_run.Caravan.Progress:P0}   i {_run.Caravan.CurrentTerrain}", style);
            GUILayout.Label($"Fart  {_run.Caravan.CurrentSpeed:F1} m/s", style);
            GUILayout.Space(6);

            GUILayout.Label($"Silver  {_run.Economy.Silver}   (tjänat {_run.Economy.TotalEarned})", style);
            GUILayout.Label($"Upptäckta  {_run.Detection.RevealedCount} / {_run.Detection.Enemies.Count}" +
                            $"   vakna {_run.Detection.AwakeCount}   sedda i tid {_run.Detection.SpottedEarlyCount}", style);
            GUILayout.Label($"Fällor  kvar {_run.Traps.LiveCount}   desarmerade {_run.Traps.DisarmedCount}", style);
            GUILayout.Space(6);

            foreach (var wagon in _run.Caravan.Wagons)
                GUILayout.Label($"{wagon.Kind,-9} {wagon.Hp,5:F0} / {wagon.MaxHp:F0}", style);

            if (_run.Outcome != RunOutcome.InProgress)
            {
                GUILayout.Space(6);
                GUILayout.Label($"<b>{_run.Outcome}</b>   {_run.Stars} stjärnor   {_run.GoldEarned()} guld", style);
            }

            GUILayout.EndArea();
        }
    }
}
