using Arna.Sim;
using Arna.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arna.App
{
    /// <summary>
    /// Lets the player draw the road, which is the decision this game is about and the
    /// one piece of it that was never built.
    ///
    /// Everything under this has been finished for a long time. RoutePlanner takes
    /// waypoints and solves the walk between them, reporting travel cost, mean ambush
    /// exposure, which fords are crossed, how much of the road is marsh, and which legs
    /// went much further than the player drew. The encounter placer validates its work
    /// against sixty-four *drawn* routes rather than against the three corridors, on the
    /// stated grounds that a player draws whatever they like. The planning map has fog,
    /// a scouting bird and a risk readout.
    ///
    /// And the runner took its road from a CorridorKind field in the Inspector, so what
    /// the player actually got was one of the generator's own samples, the same one
    /// every time. Every level had exactly one way through, because choosing was not
    /// implemented — not because the ground offered nothing.
    ///
    /// The ground offers plenty. Forest carries the highest ambush weight in the table
    /// at 1.5 and the road the second at 1.2; marsh is the slowest going at ×0.45;
    /// traps are laid at the throats where routes are forced together. A short line
    /// straight through the wood really is more dangerous than a long one around it,
    /// and this is what makes that a choice rather than a fact nobody can act on.
    /// </summary>
    [RequireComponent(typeof(LevelPreview))]
    public sealed class RouteDrawing : MonoBehaviour
    {
        /// <summary>How near a tap must land to a waypoint to pick it up, in tiles.</summary>
        public float GrabTiles = 1.6f;

        /// <summary>The scene the drawn route is walked in.</summary>
        public string PlayScene = "PlayLevel";

        public Material RouteMaterial;
        public Color DrawnColour = new Color(0.98f, 0.95f, 0.62f, 0.85f);
        public float DrawnWidth = 2.6f;

        LevelPreview _preview;
        Camera _camera;

        RoutePlanner _planner;
        RouteResult _route;
        LevelMap _map;

        GameObject _ribbon;
        int _dragging = -1;

        void Awake() => _preview = GetComponent<LevelPreview>();

        void Update()
        {
            var map = _preview.Map;
            if (map == null) return;

            if (!ReferenceEquals(map, _map)) Begin(map);

            if (Input.GetMouseButtonDown(1)) { _planner.RemoveLast(); Solve(); return; }
            if (Input.GetMouseButtonDown(0)) Press();
            else if (Input.GetMouseButton(0) && _dragging >= 0) Drag();
            else if (Input.GetMouseButtonUp(0)) _dragging = -1;
        }

        void Begin(LevelMap map)
        {
            _map = map;
            _planner = new RoutePlanner(map.Grid);
            _route = null;
            _dragging = -1;

            Solve();
        }

        /// <summary>
        /// A press either picks up a waypoint that is already there or puts a new one
        /// down. Picking up first, because a player correcting a point they just placed
        /// is far more common than one wanting a second point on top of it — and the
        /// planner refuses duplicates anyway, so the alternative is a tap that does
        /// nothing.
        /// </summary>
        void Press()
        {
            if (!Tile(out int x, out int y)) return;

            _dragging = Nearest(x, y);
            if (_dragging >= 0) return;

            if (_planner.TryAddWaypoint(x, y)) Solve();
        }

        void Drag()
        {
            if (!Tile(out int x, out int y)) return;
            if (_planner.MoveWaypoint(_dragging, x, y)) Solve();
        }

        /// <summary>The waypoint under a tile, or -1. Nearest wins, ties to the later one.</summary>
        int Nearest(int x, int y)
        {
            int found = -1;
            float best = GrabTiles * GrabTiles;

            for (int i = 0; i < _planner.WaypointCount; i++)
            {
                _map.Grid.ToCoords(_planner.Waypoints[i], out int wx, out int wy);

                float dx = wx - x, dy = wy - y;
                float distance = dx * dx + dy * dy;
                if (distance > best) continue;

                best = distance;
                found = i;
            }

            return found;
        }

        /// <summary>
        /// Which tile the pointer is over.
        ///
        /// Against a flat plane rather than the relief. The plan camera looks straight
        /// down and its ground is a low-relief map rather than a landscape, so the error
        /// from ignoring height is a fraction of a tile at the edges of the frame and
        /// nothing at all in the middle — where a raycast against the terrain mesh would
        /// need a collider on six thousand props to be reliable.
        /// </summary>
        bool Tile(out int x, out int y)
        {
            x = y = 0;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _map == null) return false;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;

            float along = -ray.origin.y / ray.direction.y;
            if (along <= 0f) return false;

            var at = ray.origin + ray.direction * along;

            x = Mathf.FloorToInt(at.x / TileGrid.TileSize);
            y = Mathf.FloorToInt(at.z / TileGrid.TileSize);

            return _map.Grid.InBounds(x, y);
        }

        void Solve()
        {
            _route = _planner.Solve(_map.StartX, _map.StartY, _map.GoalX, _map.GoalY, _route);
            Draw();
        }

        void Draw()
        {
            if (_ribbon != null) Destroy(_ribbon);
            if (_route == null || _route.Tiles.Count == 0 || RouteMaterial == null) return;

            // Red where it cannot be walked, which is the one thing the player has to be
            // told before they press Play rather than after.
            var colour = _route.IsValid ? DrawnColour : new Color(0.95f, 0.35f, 0.30f, 0.85f);

            var mesh = RouteRibbonBuilder.Build(_map.Grid, _route.Tiles, colour,
                                                _preview.HeightScale, DrawnWidth);
            if (mesh == null) return;

            _ribbon = new GameObject("DrawnRoute");
            _ribbon.transform.SetParent(transform, false);
            _ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = _ribbon.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = RouteMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// The road's own numbers, and no others.
        ///
        /// Time and terrain and how much cover the country offers — never what is
        /// standing in it. What is out there is bought with the eagle or paid for in
        /// blood (docs/GDD.md §3.4), and a readout that knew would hand it over.
        /// </summary>
        void OnGUI()
        {
            if (_route == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            GUILayout.BeginArea(new Rect(Screen.width - 300, 14, 286, 210), GUI.skin.box);

            GUILayout.Label($"<b>Rutt</b>  {_planner.WaypointCount} av {_planner.MaxWaypoints} punkter",
                            style);
            GUILayout.Label("Vänsterklick lägger ut och flyttar, högerklick ångrar.", style);
            GUILayout.Space(6);

            if (!_route.IsValid)
            {
                GUILayout.Label($"<b>Ingen väg</b> på etapp {_route.FailedLeg + 1}.", style);
            }
            else
            {
                GUILayout.Label($"Restid  {_route.EstimatedSeconds():F0} s", style);
                GUILayout.Label($"Terräng  skog {_route.ShareOf(TerrainType.Forest):P0}   "
                                + $"träsk {_route.ShareOf(TerrainType.Marsh):P0}   "
                                + $"väg {_route.ShareOf(TerrainType.Road):P0}", style);
                GUILayout.Label($"Skydd åt ett bakhåll  {_route.AmbushExposure:F2}", style);
                GUILayout.Label($"Vadställen  {_route.Crossings.Count}", style);

                if (_route.DetourLegs > 0)
                    GUILayout.Label($"{_route.DetourLegs} etapp(er) går långt runt.", style);
            }

            GUILayout.Space(6);

            GUI.enabled = _route.IsValid;
            if (GUILayout.Button("Spela denna väg"))
            {
                ChosenRoute.Set(_preview.Chapter, _preview.Level, _route.Tiles);
                SceneManager.LoadScene(PlayScene);
            }
            GUI.enabled = true;

            GUILayout.EndArea();
        }
    }
}
