using TheVeil.Sim;
using TheVeil.UI;
using TheVeil.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TheVeil.App
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
        PlanHud _hud;
        Camera _camera;

        RoutePlanner _planner;
        RouteResult _route;
        LevelMap _map;

        GameObject _ribbon;
        int _dragging = -1;

        void Awake()
        {
            _preview = GetComponent<LevelPreview>();

            // The level the roadmap sent us to. In the editor the Inspector's own chapter
            // and level still win, because opening this scene directly to look at a
            // particular map is how the generator is worked on.
            if (Application.isPlaying)
            {
                _preview.Chapter = Session.Chapter;
                _preview.Level = Session.Level;
                _preview.Rebuild();
            }

            _hud = gameObject.AddComponent<PlanHud>();
            _hud.Chapter = _preview.Chapter;
            _hud.Level = _preview.Level;
            _hud.Play = PlayDrawn;
            _hud.Undo = () => { _planner?.RemoveLast(); Solve(); };

            // A bought flight has to actually put the bird up, and the eagle is built
            // inside the preview's own rebuild. Rebuild regenerates the map too, which is
            // more work than strictly needed — but the seed has not changed, so it comes
            // back the same map, the drawn route still fits it, and this happens once or
            // twice in a level rather than per frame.
            _hud.Scout = () => _preview.Rebuild();
        }

        /// <summary>Hands the drawn tiles to the run and loads the play scene.</summary>
        void PlayDrawn()
        {
            if (_route == null || !_route.IsValid) return;

            Session.Choose(_preview.Chapter, _preview.Level);
            ChosenRoute.Set(_preview.Chapter, _preview.Level, _route.Tiles);
            SceneManager.LoadScene(PlayScene);
        }

        void Update()
        {
            var map = _preview.Map;
            if (map == null) return;

            if (!ReferenceEquals(map, _map)) Begin(map);

            // A press that lands on the panel is the panel's. Without this the Play
            // button also drops a waypoint under itself, and the road the player walks is
            // not the one they drew.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

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
            if (_planner == null) return;

            _route = _planner.Solve(_map.StartX, _map.StartY, _map.GoalX, _map.GoalY, _route);
            Draw();
            Report();
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
        void Report()
        {
            if (_hud == null || _route == null) return;

            _hud.SetLevel(_preview.Chapter, _preview.Level);
            _hud.Show(_planner.WaypointCount, _planner.MaxWaypoints, _route.IsValid,
                      _route.FailedLeg, _route.EstimatedSeconds(),
                      _route.ShareOf(TerrainType.Forest), _route.ShareOf(TerrainType.Marsh),
                      _route.ShareOf(TerrainType.Road), _route.AmbushExposure,
                      _route.Crossings.Count, _route.DetourLegs);
        }
    }
}
