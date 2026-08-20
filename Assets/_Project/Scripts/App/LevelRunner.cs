using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using Arna.View;
using UnityEngine;

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
        public bool FollowCaravan;

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
        RunVisuals _visuals;
        Transform _markerRoot;
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

            _mesh = TerrainMeshBuilder.Build(map.Grid, TileGrid.TileSize,
                new[] { new TerrainMeshBuilder.RouteOverlay(corridor.Tiles, TerrainPalette.RouteFast) },
                map.StartIndex, map.GoalIndex);
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _markerRoot = new GameObject("Markers").transform;
            _markerRoot.SetParent(transform, false);

            // The route is left bare so the player can still read the line they drew
            // through whatever the forest is doing around it.
            TerrainDecorator.Decorate(_markerRoot, map.Grid, map.Seed, Decor, corridor.Tiles);

            _visuals = new RunVisuals(_markerRoot) { Library = Models };
            _visuals.Build(_run);
            _visuals.BuildCaches(map.Encounters.SilverCaches, map.Grid);
            _visuals.Sync(_run);

            _camera = Camera.main;
            if (_camera != null)
                _cameraOffset = _camera.transform.position - CaravanWorldPosition();
        }

        void Update()
        {
            if (_run == null) return;

            if (_run.Outcome == RunOutcome.InProgress)
                _run.Advance(Time.deltaTime * TimeScale);

            _visuals.Sync(_run);

            if (FollowCaravan && _camera != null)
                _camera.transform.position = CaravanWorldPosition() + _cameraOffset;
        }

        Vector3 CaravanWorldPosition()
        {
            var position = _run.Caravan.LeadPosition;
            return new Vector3(position.X, 0f, position.Y);
        }

        void Cleanup()
        {
            if (_markerRoot != null) Destroy(_markerRoot.gameObject);
            if (_mesh != null) Destroy(_mesh);
            _run = null;
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
            GUILayout.Label($"Tid  {_run.ElapsedSeconds:F1} s   (par {_run.ParSeconds:F0} s)", style);
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
