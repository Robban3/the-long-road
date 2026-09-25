using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The planning map itself, photographed level by level: `The Veil > Plan Photos`.
    ///
    /// <b>Every other photographer here looks at the ground and none of them looks at the
    /// map.</b> The smoke test builds the play scene, the demo pictures are taken from the
    /// air, the ground photos stand at eye level - so a fault that lives in the plan view
    /// only, which is where the player spends half the game, is invisible to all of them
    /// and is reported from play instead. This takes the plan exactly as the player sees
    /// it: the scene's own camera, the scene's own framing, nothing helped.
    ///
    /// Flown for a few seconds before the shutter, because the flocks circle and a still
    /// taken the instant they are built has every bird sitting on its spawn.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.PlanPhotos.Run
    /// </summary>
    public static class PlanPhotos
    {
        /// <summary>How wide and tall the pictures come out.</summary>
        const int Wide = 1200;

        const int Tall = 1200;

        /// <summary>Seconds of circling before the picture is taken.</summary>
        const float Flown = 6f;

        [MenuItem("The Veil/Plan Photos")]
        public static void Run()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlan");
            System.IO.Directory.CreateDirectory(shots);

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/LevelPreview.unity",
                                         OpenSceneMode.Single);

            var preview = Object.FindAnyObjectByType<LevelPreview>();
            if (preview == null) { Debug.LogError("[Plan] no LevelPreview in the scene."); return; }

            var tick = typeof(LevelPreview).GetMethod("TickCrows",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // The scout, driven by hand for the same reason the crows are: nothing moves
            // outside play mode unless something steps it. Two pictures of every level,
            // because the map has two states and both of them have to be right - grey
            // before she goes up, and the country she found once she is down.
            var fly = typeof(LevelPreview).GetMethod("FlyEagle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                // The first level and the tenth: the tenth is the one with the castle on
                // the goal, and a plan of it has the most on it of any level in a chapter.
                foreach (int level in new[] { 1, Campaign.LevelsPerChapter })
                {
                    preview.Chapter = chapter;
                    preview.Level = level;
                    preview.Rebuild();

                    for (int i = 0; i < Mathf.RoundToInt(Flown / 0.1f) && tick != null; i++)
                        tick.Invoke(preview, new object[] { 0.1f });

                    Shoot(preview, shots, $"plan-{chapter}-{level}-fogged");

                    // Aloft for a minute of level time, which is six times the flight: she
                    // lands long before that and the reveal catches up behind her.
                    for (int i = 0; i < Aloft && fly != null; i++)
                        fly.Invoke(preview, new object[] { 0.1f });

                    Shoot(preview, shots, $"plan-{chapter}-{level}");
                }
            }

            Debug.Log("[Plan] done");
        }

        /// <summary>
        /// The plan with a route drawn on it: `The Veil > Plan Photos (Drawn)`.
        ///
        /// <b>Because the map the player looks at has a line on it, and none of the
        /// pictures did.</b> Everything photographed so far is the map before anybody has
        /// touched it. What was reported is what happens when a waypoint goes down - and
        /// the ribbon is built from the route the planner returns, over ground that is
        /// half revealed, so it is the one part of the plan that cannot be judged from a
        /// picture of the map standing still.
        ///
        /// Drawn here exactly as RouteDrawing draws it: the same planner, the same
        /// builder, the same material off the scene's own component.
        ///
        /// Headless: unity run . -- -executeMethod TheVeil.Editor.PlanPhotos.Drawn
        /// </summary>
        [MenuItem("The Veil/Plan Photos (Drawn)")]
        public static void Drawn()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlan");
            System.IO.Directory.CreateDirectory(shots);

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/LevelPreview.unity",
                                         OpenSceneMode.Single);

            var preview = Object.FindAnyObjectByType<LevelPreview>();
            var drawing = Object.FindAnyObjectByType<RouteDrawing>();
            if (preview == null || drawing == null)
            {
                Debug.LogError("[Plan] the scene has no LevelPreview or no RouteDrawing.");
                return;
            }

            var fly = typeof(LevelPreview).GetMethod("FlyEagle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            foreach (var (chapter, level) in new[] { (1, 1), (4, 1), (6, 1) })
            {
                preview.Chapter = chapter;
                preview.Level = level;
                preview.Rebuild();

                for (int i = 0; i < Aloft && fly != null; i++)
                    fly.Invoke(preview, new object[] { 0.1f });

                var map = LevelMaps.For(chapter, level);
                var planner = new RoutePlanner(map.Grid);

                // Two waypoints, off the straight line either side, which is what a player
                // does first: one leg out and one back.
                map.Grid.ToCoords(map.StartIndex, out int sx, out int sy);
                map.Grid.ToCoords(map.GoalIndex, out int gx, out int gy);

                int midY = (sy + gy) / 2;
                planner.TryAddWaypoint(Mathf.Clamp(sx + 12, 1, map.Grid.Width - 2),
                                       Mathf.Clamp(midY - 8, 1, map.Grid.Height - 2),
                                       map.StartIndex, map.GoalIndex);
                planner.TryAddWaypoint(Mathf.Clamp(gx - 12, 1, map.Grid.Width - 2),
                                       Mathf.Clamp(midY + 8, 1, map.Grid.Height - 2),
                                       map.StartIndex, map.GoalIndex);

                var route = planner.Solve(map.StartX, map.StartY, map.GoalX, map.GoalY);

                Debug.Log($"[Plan] {chapter}-{level} drawn: {planner.WaypointCount} waypoint(s), "
                          + $"{route.Tiles.Count} tiles, valid {route.IsValid}");

                var mesh = RouteRibbonBuilder.Build(map.Grid, route.Tiles, drawing.DrawnColour,
                                                    preview.HeightScale, drawing.DrawnWidth);

                var ribbon = new GameObject("DrawnRoute");
                ribbon.transform.SetParent(preview.transform, false);
                ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = ribbon.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = drawing.RouteMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Shoot(preview, shots, $"drawn-{chapter}-{level}");

                Object.DestroyImmediate(ribbon);
            }

            Debug.Log("[Plan] done");
        }

        /// <summary>
        /// The plan photographed after each click, as a player builds a route up.
        ///
        /// <b>A still of a finished route cannot show what a click does to it.</b> What
        /// was reported is the map going strange when a waypoint goes down, and the two
        /// candidates look identical once the line is drawn: a path that takes a silly
        /// shape between the points, and a point that is inserted into the wrong leg so
        /// the road doubles back on itself. Photographed one click at a time, they do not
        /// look alike at all.
        ///
        /// The clicks are laid along the straight line from the start to the goal and
        /// then off it, which is how a route is actually drawn: out towards the cover,
        /// back towards the road.
        ///
        /// Headless: unity run . -- -executeMethod TheVeil.Editor.PlanPhotos.Clicks
        /// </summary>
        [MenuItem("The Veil/Plan Photos (Clicks)")]
        public static void Clicks()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlan");
            System.IO.Directory.CreateDirectory(shots);

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/LevelPreview.unity",
                                         OpenSceneMode.Single);

            var preview = Object.FindAnyObjectByType<LevelPreview>();
            var drawing = Object.FindAnyObjectByType<RouteDrawing>();
            if (preview == null || drawing == null)
            {
                Debug.LogError("[Plan] the scene has no LevelPreview or no RouteDrawing.");
                return;
            }

            var fly = typeof(LevelPreview).GetMethod("FlyEagle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            const int chapter = 1;
            const int level = 1;

            preview.Chapter = chapter;
            preview.Level = level;
            preview.Rebuild();

            for (int i = 0; i < Aloft && fly != null; i++) fly.Invoke(preview, new object[] { 0.1f });

            var map = LevelMaps.For(chapter, level);
            var planner = new RoutePlanner(map.Grid);

            map.Grid.ToCoords(map.StartIndex, out int sx, out int sy);
            map.Grid.ToCoords(map.GoalIndex, out int gx, out int gy);

            // Four taps: a quarter of the way along and pulled aside, then half, then
            // three quarters, then one back on the line between the first two - which is
            // the tap that tests which leg a point lands in.
            var taps = new[]
            {
                (0.25f, -6), (0.50f, 6), (0.75f, -4), (0.375f, 0)
            };

            int shot = 0;

            foreach (var (along, aside) in taps)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(sx, gx, along)) + aside,
                                    1, map.Grid.Width - 2);
                int y = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(sy, gy, along)),
                                    1, map.Grid.Height - 2);

                bool took = planner.TryAddWaypoint(x, y, map.StartIndex, map.GoalIndex);
                var route = planner.Solve(map.StartX, map.StartY, map.GoalX, map.GoalY);

                Debug.Log($"[Plan] click {++shot} at {x},{y}: {(took ? "taken" : "refused")}, "
                          + $"{planner.WaypointCount} waypoint(s), {route.Tiles.Count} tiles, "
                          + $"valid {route.IsValid}, order "
                          + string.Join(" ", System.Linq.Enumerable.Select(planner.Waypoints,
                                w => { map.Grid.ToCoords(w, out int wx, out int wy); return $"{wx},{wy}"; })));

                var ribbon = new GameObject("DrawnRoute");
                ribbon.transform.SetParent(preview.transform, false);
                ribbon.AddComponent<MeshFilter>().sharedMesh =
                    RouteRibbonBuilder.Build(map.Grid, route.Tiles, drawing.DrawnColour,
                                             preview.HeightScale, drawing.DrawnWidth);

                var renderer = ribbon.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = drawing.RouteMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Shoot(preview, shots, $"click-{shot}");

                Object.DestroyImmediate(ribbon);
            }

            Debug.Log("[Plan] done");
        }

        /// <summary>How many tenths of a second the scout is flown for.</summary>
        const int Aloft = 600;

        /// <summary>One picture of the plan as it stands, through the scene's own camera.</summary>
        static void Shoot(LevelPreview preview, string shots, string name)
        {
            var camera = Camera.main;
            if (camera == null) { Debug.LogError("[Plan] the scene has no main camera."); return; }

            var texture = new RenderTexture(Wide, Tall, 24);
            var was = camera.targetTexture;
            camera.targetTexture = texture;

            // Twice, the first thrown away: the first render of a freshly built level
            // comes back with half its surfaces blown out. See GroundPhotos.
            camera.Render();
            camera.Render();

            RenderTexture.active = texture;
            var shot = new Texture2D(Wide, Tall, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Wide, Tall), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            camera.targetTexture = was;

            string path = System.IO.Path.Combine(shots, name + ".png");
            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log($"[Plan] {name}: {path}");
        }

    }
}
