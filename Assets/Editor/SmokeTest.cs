using System.Collections.Generic;
using System.Linq;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.UI;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Builds every level that has been made and looks at it: `The Veil > Smoke Test`.
    ///
    /// The tests in Assets/Tests judge the map — that a road exists, that the water can
    /// be crossed, that the fights are survivable. None of them judge what the level
    /// looks like when it is standing up, and every appearance fault this project has
    /// shipped was found by a person looking at a screen and saying so: trees on their
    /// roots, a bridge in open water, branches hanging in the air. This builds the world
    /// the way LevelRunner builds it, measures what is standing in it, and renders a
    /// sheet per chapter so the looking can be done in one pass instead of thirty.
    ///
    /// Deliberately not a unit test. It wants a scene, prefabs, materials and a camera,
    /// it takes minutes rather than seconds, and its output is a picture and a list of
    /// suspects — which is a thing to read, not a thing to assert. What it finds that is
    /// worth keeping becomes a test in Assets/Tests, as the seating rule did.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.SmokeTest.Run
    /// </summary>
    public static class SmokeTest
    {
        /// <summary>
        /// Chapters with content: the ones the catalogue has built. The rest are generated
        /// against nothing but the roads until they are built, and look it.
        ///
        /// It was pinned at three, and stayed three while the fourth and fifth were built,
        /// so nothing looked at the plains or the farmland at all.
        /// </summary>
        const int Chapters = TheVeil.Sim.DifficultyCurve.BuiltChapters;

        /// <summary>
        /// How far a prop's lowest point may sit above the ground before it is floating.
        ///
        /// Not zero. A model's bounding box is square and its foot is not, so anything
        /// tilted lifts a corner honestly; and the ground under a prop is sampled at its
        /// pivot while the box reaches out to the sides, which on a slope is a real
        /// difference and not a fault. Half a metre is under the eye's threshold at the
        /// game camera's distance and well over both of those.
        /// </summary>
        const float FloatTolerance = 0.5f;

        /// <summary>Where the sheets are written.</summary>
        static string Shots => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");

        [MenuItem("The Veil/Smoke Test")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                                     OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null)
            {
                Debug.LogError("[Smoke] PlayLevel has no LevelRunner, so there is nothing to build.");
                return;
            }

            System.IO.Directory.CreateDirectory(Shots);

            var faults = new List<string>();

            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                var sheet = new Texture2D(ShotWidth * 5, ShotHeight * 2, TextureFormat.RGB24, false);

                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var shot = Level(runner, chapter, level, faults);

                    int column = (level - 1) % 5;
                    int row = (level - 1) / 5;

                    // Row 0 at the top of the sheet, which is the bottom in texture space.
                    sheet.SetPixels(column * ShotWidth, (1 - row) * ShotHeight,
                                    ShotWidth, ShotHeight, shot.GetPixels());
                    Object.DestroyImmediate(shot);
                }

                sheet.Apply();
                string path = System.IO.Path.Combine(Shots, $"chapter{chapter}.png");
                System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);

                Debug.Log($"[Smoke] Chapter {chapter} sheet: {path}");
            }

            if (faults.Count == 0) Debug.Log("[Smoke] Nothing found standing in the air or buried.");
            else foreach (string fault in faults) Debug.Log("[Smoke] " + fault);

            EditorSceneManager.CloseScene(scene, false);
        }

        const int ShotWidth = 520;
        const int ShotHeight = 340;

        /// <summary>
        /// Stands one level up in the open scene, exactly as LevelRunner stands it up.
        ///
        /// Shared rather than copied, because the whole worth of this tool is that what
        /// it measures and photographs is the world the player drives through. A second
        /// build with its own arguments would drift from the first the day either one is
        /// changed, and then the tool is reporting on a world nobody plays.
        /// </summary>
        internal static GameObject Build(LevelRunner runner, int chapter, int level, out LevelMap map)
        {
            map = LevelMaps.For(chapter, level);
            var biome = Biomes.Of(chapter);
            var look = runner.LookFor(biome);
            var decor = look != null && look.Dressed ? look.Decor : runner.Decor;

            var root = new GameObject($"Smoke {chapter}-{level}");

            // The ground, exactly as the run builds it.
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root.transform, false);
            // The three roads painted into the ground, as LevelRunner paints them. Passed
            // null here, the pictures showed a country with no roads in it while the game
            // had them - an instrument that does not show what ships is worse than none.
            var tracks = new System.Collections.Generic.List<TerrainMeshBuilder.RouteOverlay>();
            if (map.Corridors != null)
                foreach (var road in map.Corridors)
                    if (road?.Tiles != null)
                        tracks.Add(new TerrainMeshBuilder.RouteOverlay(road.Tiles, TerrainPalette.Track));

            var mesh = TerrainMeshBuilder.Build(map.Grid, TileGrid.TileSize, tracks, -1, -1,
                                                runner.HeightScale, TerrainMeshBuilder.SkirtWidth, biome,
                                                LevelMaps.Recipe(chapter, level).Town
                                                    ? Towns.Layout(map.Grid.Width, map.Grid.Height,
                                                                   map.Seed, map.StartY)
                                                    : Towns.None);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial =
                runner.GetComponent<MeshRenderer>() != null
                    ? runner.GetComponent<MeshRenderer>().sharedMaterial
                    : null;

            var props = new GameObject("Props");
            props.transform.SetParent(root.transform, false);

            // The same call the run makes, minus the parts that need a squad on the road:
            // the drive line is the planned route rather than the column's own sweep.
            TerrainDecorator.Decorate(props.transform, map.Grid, map.Seed, decor,
                keepClear: null, heightScale: runner.HeightScale, maxProps: runner.MaxProps,
                waterMaterial: look != null && look.Water != null ? look.Water : runner.WaterMaterial,
                marshWaterMaterial: look != null && look.Water != null ? look.Water : runner.MarshWaterMaterial,
                apronOpenings: new[] { map.StartIndex, map.GoalIndex },
                ruinSites: TrapSigns.Sites(map),
                driveLine: LevelPreview.Travelled(map),
                campSites: CampSignal.Tiles(map), driveMargin: 0,
                travelled: LevelPreview.Travelled(map),
                goalTile: level >= Campaign.LevelsPerChapter ? map.GoalIndex : -1,
                landmarkScale: runner.LandmarkScale,
                densityScale: look != null ? look.Density : 1f,
                village: Settlements.Site(map, chapter, level),
                settled: Settlements.Settled(biome),
                town: LevelMaps.Recipe(chapter, level).Town
                          ? Towns.Layout(map.Grid.Width, map.Grid.Height, map.Seed, map.StartY)
                          : Towns.None,

                // Where the champion waits, so the castle stands on his side of
                // the goal. See Strongholds.Site.
                guard: Champions.Post(map));

            return root;
        }

        /// <summary>Builds one level, measures what stands in it, and returns its picture.</summary>
        static Texture2D Level(LevelRunner runner, int chapter, int level, List<string> faults)
        {
            var root = Build(runner, chapter, level, out var map);
            var look = runner.LookFor(Biomes.Of(chapter));

            Measure(map, root.transform.Find("Props"), runner.HeightScale, chapter, level, faults);

            var shot = Shoot(map, runner, look, Settlements.Site(map, chapter, level), chapter, level);

            Object.DestroyImmediate(root);
            return shot;
        }

        /// <summary>
        /// Every prop against the ground under it, asked of the same surface the
        /// decorator seats against (TileGrid.SurfaceElevation).
        ///
        /// Props outside the grid are skipped rather than judged: the skyline stands
        /// three hundred metres off the map and the apron lies past its edge, and the
        /// surface clamps to the border for both, so the number that comes back for them
        /// is not about them.
        /// </summary>
        static void Measure(LevelMap map, Transform props, float heightScale,
                            int chapter, int level, List<string> faults)
        {
            float edgeX = map.Grid.Width * TileGrid.TileSize;
            float edgeZ = map.Grid.Height * TileGrid.TileSize;

            var floating = new Dictionary<string, (int Count, float Worst)>();
            var sunk = new Dictionary<string, int>();
            int counted = 0;

            foreach (var renderer in props.GetComponentsInChildren<MeshRenderer>(false))
            {
                // The water surfaces are the ground of their own tiles, not things standing
                // on it, and a bridge is built to stand clear of the water it crosses.
                string name = renderer.gameObject.name;
                if (name.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.Contains("Pools") || name.Contains("Bridge")) continue;

                // And two more that are meant to be exactly what this would call a fault.
                //
                // <b>A banner hangs and paving is buried.</b> The castle's colours are hung
                // from the parapet and fall down the face of the wall, so they are four to
                // seven metres clear of the ground by construction; the bailey is laid as
                // earth with flagstone over five parts in six of it, so most of the earth
                // is under stone and out of sight. Both were reported by this the day they
                // were built, four and eleven times a level, and both are correct.
                //
                // Written down rather than tolerated, because a check that cries about
                // things that are right is a check nobody reads — which is the whole
                // reason the bridge exemption above exists.
                bool hangs = name.Contains("Banner");

                // A tree's branches, which are their own meshes in the meadow pack and hang
                // above the ground by construction - that is what a branch does. The trunk
                // they belong to is measured like anything else, and it is the trunk that
                // says whether the tree is standing on the ground. Reported as a fault, 104
                // of them on 5-10 alone, and not one was wrong.
                if (name.Contains("_Branches_") || name.Contains("_Leaves_LOD")) continue;

                // The building kit is assembled part on part, so a chimney is eight metres
                // clear of the ground and right to be: it is standing on a roof. Measuring
                // the parts against the ground asks the wrong question of them — the first
                // run reported nine of these and not one was a fault. What would be worth
                // catching is a part resting on nothing, and that is a question about the
                // kit's own assembly rather than about the ground.
                if (name.StartsWith("SM_Bld_")) continue;

                // And the pieces of a rock mass, for the same reason: a tor is built in
                // courses, and asking whether its second course rests on the ground is the
                // wrong question - it rests on its first. See TerrainDecorator.Tor.
                if (name.StartsWith(TerrainDecorator.TorPieceName)) continue;

                var at = renderer.transform.position;
                if (at.x < 0f || at.z < 0f || at.x > edgeX || at.z > edgeZ) continue;

                counted++;

                float ground = map.Grid.SurfaceElevation(at.x, at.z) * heightScale;
                float gap = renderer.bounds.min.y - ground;
                float height = renderer.bounds.size.y;

                if (gap > FloatTolerance)
                {
                    if (hangs) continue;

                    floating.TryGetValue(name, out var seen);
                    floating[name] = (seen.Count + 1, Mathf.Max(seen.Worst, gap));
                }
                else if (height > 0.2f && gap < -height)
                {
                    sunk.TryGetValue(name, out int buried);
                    sunk[name] = buried + 1;
                }
            }

            Solidity(map, props, chapter, level, faults);

            foreach (var pair in floating.OrderByDescending(p => p.Value.Worst).Take(4))
                faults.Add($"{chapter}-{level}: {pair.Key} hangs in the air, "
                           + $"{pair.Value.Count} of them, worst {pair.Value.Worst:0.0} m clear of the ground");

            foreach (var pair in sunk.OrderByDescending(p => p.Value).Take(3))
                faults.Add($"{chapter}-{level}: {pair.Key} is buried out of sight, {pair.Value} of them");

            // And the map's own promises, so a level that reads well and plays wrong is
            // still caught here rather than in front of an audience.
            int crossings = Crossings.Count(map.Grid);
            var recipe = LevelMaps.Recipe(chapter, level);
            if (crossings < recipe.CrossingsOwed)
                faults.Add($"{chapter}-{level}: only {crossings} way(s) over the water");

            // The same question the game asks, with the roads in it. Without them this
            // asked for a bridge over any crossing while the decorator asks for one over
            // a crossing somebody drives to, so the tool could report a fault about a
            // bridge that is not the bridge the level builds.
            int bridge = TerrainDecorator.BridgeTile(map.Grid, map.Seed,
                                                     LevelPreview.Travelled(map));

            if (bridge >= 0 && !Crossings.Spans(map.Grid, bridge))
                faults.Add($"{chapter}-{level}: the bridge stands in open water");
            else if (bridge < 0 && recipe.CrossingsOwed > 0)
                faults.Add($"{chapter}-{level}: no bridge, and the level owes "
                           + $"{recipe.CrossingsOwed} way(s) over its water");

            int built = 0;
            foreach (var renderer in props.GetComponentsInChildren<MeshRenderer>(false))
                if (renderer.gameObject.name.StartsWith("SM_Bld_")) built++;

            Debug.Log($"[Smoke] {chapter}-{level}: {counted} props, {crossings} crossings, "
                      + $"{built} building part(s).");
        }

        /// <summary>
        /// One picture of the level from the caravan's own height, looking along the road.
        ///
        /// Not from above. The plan view is drawn from up there and it is exactly the
        /// angle that hides this kind of fault: a tree standing on its roots reads as a
        /// tree from overhead, and only a camera down where the player's eye is shows the
        /// gap under it.
        /// </summary>
        /// <summary>
        /// Two things about the country that the pictures cannot show.
        ///
        /// <b>Whether what stands on the ground stops anybody, and whether it stands in the
        /// road.</b> Both were reported from play rather than seen here - the escort walking
        /// through the rock past a bridge, the column driving through a boulder - because a
        /// prop that is not solid looks exactly like one that is. Measured instead: every
        /// prop tall enough to be worth walking round is asked whether anything about it is
        /// solid, and every solid disc is asked whether it is standing on the route.
        ///
        /// The bridge and the water are the exceptions and they are named: a bridge is
        /// meant to be driven over and a river is not walked round.
        /// </summary>
        static void Solidity(LevelMap map, Transform props, int chapter, int level,
                             List<string> faults)
        {
            if (props == null) return;

            var lane = new HashSet<int>(LevelPreview.Travelled(map));
            float span = map.Grid.Width * TileGrid.TileSize;
            float deep = map.Grid.Height * TileGrid.TileSize;

            var open = new List<string>();
            var barring = new List<string>();

            foreach (Transform prop in props)
            {
                if (prop.GetComponentInChildren<MeshRenderer>() == null) continue;
                if (Driven(prop.name)) continue;

                var bounds = ModelScaling.Measure(prop.gameObject);
                if (bounds.size.y < TerrainDecorator.SolidHeight) continue;

                // The apron and the skyline stand outside the playing field, and nobody
                // walks out there to bump into them.
                if (bounds.center.x < 0f || bounds.center.x > span) continue;
                if (bounds.center.z < 0f || bounds.center.z > deep) continue;

                var discs = prop.GetComponentsInChildren<Solid>(true);
                if (discs.Length == 0) { open.Add(prop.name); continue; }

                // The castle stands on the goal and the road runs in at its gate, which is
                // the level: the caravan is driving to the keep, so the keep is in its
                // lane by construction. Everything else in this list is a fault.
                if (prop.name == "Castle") continue;

                foreach (var disc in discs)
                {
                    bool hit = false;

                    foreach (int tile in lane)
                    {
                        var middle = Vec2.FromTile(map.Grid, tile);
                        if (Vector2.Distance(new Vector2(middle.X, middle.Y), disc.Centre) <= disc.Radius)
                        { hit = true; break; }
                    }

                    if (hit) { barring.Add(prop.name); break; }
                }
            }

            if (open.Count > 0)
                faults.Add($"{chapter}-{level}: {open.Count} prop(s) tall enough to walk round "
                           + $"with nothing solid about them, {Named(open)}");

            if (barring.Count > 0)
                faults.Add($"{chapter}-{level}: {barring.Count} solid prop(s) standing in the "
                           + $"caravan's lane, {Named(barring)}");
        }

        /// <summary>The first few names in a list, because a count is not a lead.</summary>
        static string Named(List<string> found)
            => string.Join(", ", found.Distinct().Take(3));

        /// <summary>What is driven over or waded through rather than walked round.</summary>
        // Water by any of the names the decorator gives it - "Water", "Marsh water",
        // "Pool" - which is why this asks for the word rather than the prefix.
        static bool Driven(string name) =>
            name.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.StartsWith("Pool") || name.StartsWith("Fall") || name.Contains("Bridge");

        /// <summary>
        /// The nearest spot on the camera's own line of sight with nothing standing in it.
        ///
        /// Backed off away from what it is looking at, and lifted as it goes, so the shot
        /// keeps its angle: what it is trying to show is the country round the middle of the
        /// level, and a metre or two further out changes nothing about that. Gives up after
        /// a few steps and returns where it started, which is no worse than before.
        /// </summary>
        static Vector3 Clear(Vector3 eye, Vector3 at)
        {
            var back = (eye - at).normalized;

            for (int step = 0; step <= ClearSteps; step++)
            {
                var tried = eye + back * (step * ClearStep) + Vector3.up * (step * ClearLift);
                if (Sees(tried, at)) return tried;
            }

            return eye;
        }

        /// <summary>
        /// Whether the camera can see what it is pointed at from here.
        ///
        /// <b>Standing clear is not the same as seeing.</b> Moved only until nothing had the
        /// camera inside it, three levels of ten still came back as one rock filling the
        /// frame: the mass was a stride in front of it. So the line itself is walked, a few
        /// metres at a time, and a spot only counts when the whole of it is open.
        /// </summary>
        static bool Sees(Vector3 eye, Vector3 at)
        {
            float away = Vector3.Distance(eye, at);
            var along = (at - eye).normalized;

            for (float step = 0f; step < away * SeeShare; step += SeeStep)
                if (Inside(eye + along * step)) return false;

            return true;
        }

        /// <summary>How far along the line of sight is checked: the near two thirds of it.</summary>
        const float SeeShare = 0.66f;

        /// <summary>How finely it is walked, in metres.</summary>
        const float SeeStep = 3f;

        /// <summary>Whether anything standing in the scene has this point inside it.</summary>
        static bool Inside(Vector3 point)
        {
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var box = renderer.bounds;

                // Ground and water are meant to be under the camera, not round it.
                if (renderer.name == "Ground" || renderer.name.StartsWith("Water")) continue;

                box.Expand(ClearRoom);
                if (box.Contains(point)) return true;
            }

            return false;
        }

        /// <summary>How far back the camera steps at a time, in metres.</summary>
        const float ClearStep = 12f;

        /// <summary>And how much higher, so the shot keeps looking down on the country.</summary>
        const float ClearLift = 6f;

        /// <summary>How many steps it may take before it gives up.</summary>
        const int ClearSteps = 6;

        /// <summary>How much room the camera wants round it, in metres.</summary>
        const float ClearRoom = 2f;

        static Texture2D Shoot(LevelMap map, LevelRunner runner, BiomeLook look, int village,
                               int chapter, int level)
        {
            map.Grid.ToCoords(map.StartIndex, out int sx, out int sy);
            map.Grid.ToCoords(map.GoalIndex, out int gx, out int gy);

            var from = new Vector3(sx * TileGrid.TileSize, 0f, sy * TileGrid.TileSize);
            var to = new Vector3(gx * TileGrid.TileSize, 0f, gy * TileGrid.TileSize);

            // A third of the way along the road, which is country the caravan drives
            // through rather than the start line it forms up on — unless the level has a
            // village, which is the thing on it most worth looking at and the only thing
            // on it that can be wrong in a way the ground cannot.
            var at = Vector3.Lerp(from, to, 0.33f);

            if (village >= 0)
            {
                map.Grid.ToCoords(village, out int vx, out int vy);
                at = new Vector3(vx * TileGrid.TileSize, 0f, vy * TileGrid.TileSize);
            }

            // And the town, which is the largest thing on any map and the one most worth
            // looking at on the level it stands on.
            var walls = LevelMaps.Recipe(chapter, level).Town
                ? Towns.Layout(map.Grid.Width, map.Grid.Height, map.Seed, map.StartY)
                : Towns.None;

            if (walls.Any)
                at = new Vector3((walls.West + walls.East) * 0.5f * TileGrid.TileSize, 0f,
                                 (walls.North + walls.South) * 0.5f * TileGrid.TileSize);

            at.y = map.Grid.SurfaceElevation(at.x, at.z) * runner.HeightScale;

            var go = new GameObject("Smoke camera");
            var camera = go.AddComponent<Camera>();

            // Further back and higher for a village: the whole place has to fit, and what
            // is being judged is whether it reads as a settlement rather than as houses.
            var eye = at + (walls.Any ? new Vector3(-135f, 100f, -135f)
                         : village >= 0 ? new Vector3(-38f, 28f, -38f)
                         : new Vector3(-26f, 17f, -26f));
            // <b>And out of whatever is standing there.</b> The camera is put at a fixed
            // offset from the level's middle, and once the mountains grew rock masses
            // twenty-five metres high it stood inside one on three levels of ten: the
            // chapter sheet came back with two cells of dark stone and one of a tree. It
            // backs off along its own line of sight until nothing is in the way of it.
            camera.transform.position = Clear(eye, at + Vector3.up * 3f);
            camera.transform.LookAt(at + Vector3.up * 3f);
            camera.fieldOfView = 50f;
            camera.farClipPlane = 900f;

            bool fog = look != null && look.Fog;
            RenderSettings.fog = fog;
            if (fog)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = look.FogColor;
                RenderSettings.fogDensity = look.FogDensity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = look.SkyColor;
            }

            var rt = new RenderTexture(ShotWidth, ShotHeight, 24);
            camera.targetTexture = rt;

            // Twice, and the first thrown away: the first render of a freshly built level
            // comes back with half its surfaces blown out. See GroundPhotos.
            camera.Render();
            camera.Render();

            RenderTexture.active = rt;
            var shot = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);

            return shot;
        }
    }
}
