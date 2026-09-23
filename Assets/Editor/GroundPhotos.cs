using System.Collections.Generic;
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
    /// The country at eye level: `The Veil > Ground Photos`.
    ///
    /// <b>Because the chapter sheets are shot from four hundred metres up.</b> What a
    /// player sees of the ground - the flowers in the grass, the worn path, the stones
    /// across the stream - is invisible from there, and a country judged from above is
    /// judged on its treeline alone. This stands the camera where a person would stand,
    /// on the road, looking along it.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.GroundPhotos.Run -level 4 3
    /// </summary>
    public static class GroundPhotos
    {
        /// <summary>Whether any water lies within two tiles.</summary>
        static bool NearWater(TileGrid grid, int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (!grid.InBounds(x + dx, y + dy)) continue;

                    var terrain = grid[x + dx, y + dy];
                    if (terrain == TerrainType.Water || terrain == TerrainType.Ford) return true;
                }

            return false;
        }

        /// <summary>The first thing under here whose name carries this word, or null.</summary>
        static Transform FindLike(Transform root, string word)
        {
            foreach (var piece in root.GetComponentsInChildren<Transform>(true))
                if (piece.name.Contains(word)) return piece;

            return null;
        }

        /// <summary>The first thing under here with this name, or null.</summary>
        static Transform Find(Transform root, string name)
        {
            foreach (var piece in root.GetComponentsInChildren<Transform>(true))
                if (piece.name == name) return piece;

            return null;
        }

        /// <summary>The name of the prop a renderer belongs to, rather than of its part.</summary>
        static string Root(Transform piece)
        {
            var at = piece;
            while (at.parent != null && at.parent.name != "Props" && at.parent.parent != null) at = at.parent;
            return at.name;
        }

        public static void Run()
        {
            var levels = new List<(int Chapter, int Level)>();
            var args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i + 2 < args.Length; i++)
                if (args[i] == "-level"
                    && int.TryParse(args[i + 1], out int chapter)
                    && int.TryParse(args[i + 2], out int level))
                    levels.Add((chapter, level));

            if (levels.Count == 0) levels.Add((4, 3));

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);
            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Ground] no LevelRunner"); return; }

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            foreach (var (chapter, level) in levels)
            {
                var root = SmokeTest.Build(runner, chapter, level, out var map);
                var grid = map.Grid;

                // A place on the fastest road, a third of the way along it, and the way it
                // is heading: the shot a player would have from the driving seat.
                // A dry piece of road, away from any crossing. Standing a third of the way
                // along, the camera once stood on the bridge itself and photographed the
                // inside of its deck - which came back as a bright yellow wall and looked
                // like a broken material rather than a camera in the wrong place.
                var road = map.Corridors[0].Tiles;
                int at = road[road.Count / 3];

                for (int along = 0; along < road.Count; along++)
                {
                    int tried = road[(road.Count / 3 + along) % road.Count];
                    var under = grid[tried];

                    if (under == TerrainType.Water || under == TerrainType.Ford) continue;

                    grid.ToCoords(tried, out int tx, out int ty);
                    if (NearWater(grid, tx, ty)) continue;

                    at = tried;
                    break;
                }
                int ahead = road[Mathf.Min(road.Count - 1, road.Count / 3 + 6)];

                var here = Vec2.FromTile(grid, at);
                var there = Vec2.FromTile(grid, ahead);
                float y = grid.SurfaceElevation(here.X, here.Y) * runner.HeightScale;

                var camera = new GameObject("Shot").AddComponent<Camera>();
                camera.transform.position = new Vector3(here.X, y + 2.2f, here.Y);
                camera.transform.LookAt(new Vector3(there.X,
                                                    grid.SurfaceElevation(there.X, there.Y) * runner.HeightScale + 1.6f,
                                                    there.Y));
                camera.fieldOfView = 60f;
                camera.farClipPlane = 3000f;

                // The country's own air and sky, as LevelRunner sets them for a run. Without
                // this the shot is taken under the scene's default sky, and the first picture
                // of the plains came back under a midnight blue one.
                var look = runner.LookFor(Biomes.Of(chapter));

                // <b>No fog in the picture.</b> Turned on for the shot, every lit surface in
                // it came back a saturated yellow - the bridge, the wreck, the stone, the
                // bones - while the trees, which are drawn by another shader, stayed right.
                // The same country photographed from above, with the same settings, is
                // correct. So it is the shot and not the country; the fog stays off here
                // until that is understood.
                RenderSettings.fog = false;
                if (look != null)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = look.SkyColor;
                }

                // Rendered in high dynamic range, as the game's own camera does. A plain
                // eight-bit target clips a lit white thing to a flat colour, and the bones
                // at the traps came back as saturated yellow blobs - a fault in the picture,
                // not in the country.
                // <b>Every model at its nearest detail.</b> Rendered by hand from an editor
                // script, Unity picks a model's level of detail from whatever camera last
                // culled the scene - and from close to, half the country came back wearing
                // its billboard: the bridge, the wreck, the stone and the bones as flat
                // yellow cards, while the trees, which are drawn by a shader that fades
                // them, looked right. The same shot from above was correct, which is what
                // said it was the detail and not the dressing.
                foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
                    group.ForceLOD(0);

                // And nothing wearing a colour somebody painted on it at runtime.
                foreach (var painted2 in root.GetComponentsInChildren<Renderer>(true))
                    painted2.SetPropertyBlock(null);

                // Nothing for a shiny surface to reflect but a flat grey. The scene keeps no
                // skybox, and what a smooth material reflects when there is none is whatever
                // the pipeline has lying about - which at eye level, where reflection is at
                // its strongest, painted every lit surface yellow.
                RenderSettings.defaultReflectionMode =
                    UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = null;
                DynamicGI.UpdateEnvironment();

                                // <b>A plain target, and no high dynamic range.</b> Rendered to an HDR
                // texture and read straight back into a PNG, with no tone curve between, the
                // bridge, the wreck, the stone and the bones all came back a blown-out
                // yellow while the trees looked right - and three sessions went into the
                // dressing, the materials, the lighting and the level of detail before the
                // same camera at the same spot, differing only in this, came back correct.
                var texture = new RenderTexture(1600, 900, 24);
                camera.targetTexture = texture;

                // <b>Twice, and the first one thrown away.</b> The first render of a freshly
                // built level comes back with half its surfaces a blown-out yellow - the
                // bridge, the wreck, the stone, the bones - while the trees look right; the
                // second, of the same camera at the same spot, is correct, which is how the
                // shots taken later in this method were always right. Three sessions went
                // into the dressing, the materials, the lighting and the levels of detail
                // before a twin camera at the same spot proved it was the order and not the
                // country.
                camera.Render();
                camera.Render();

                RenderTexture.active = texture;
                var shot = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                // What is standing in the shot, nearest first: a picture shows something is
                // wrong and this says what it is.
                var near = new List<(float Away, string Name, Vector3 Size, string Wearing)>();
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var box = renderer.bounds;
                    float away = Vector3.Distance(camera.transform.position, box.center);
                    if (away >= 25f || box.size.magnitude <= 1.2f) continue;

                    var wearing = renderer.sharedMaterial;
                    string dressed = wearing == null
                        ? "no material"
                        : $"{wearing.name} ({wearing.shader.name})"
                          + (wearing.HasProperty("_BaseColor")
                              ? " " + wearing.GetColor("_BaseColor").ToString("0.00") : "");

                    near.Add((away, Root(renderer.transform), box.size, dressed));
                }

                // The biggest things standing in the level, whatever they are and wherever
                // they stand: a picture shows a white slab, this says its name.
                var largest = new List<(float Size, string Name, string Wearing)>();
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var wearing = renderer.sharedMaterial;
                    largest.Add((renderer.bounds.size.magnitude, Root(renderer.transform),
                                 wearing == null ? "none" : wearing.name + " / " + wearing.shader.name));
                }

                // What a rock mass is actually built of, counted by model.
                var masses = new Dictionary<string, int>();
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string owner = Root(renderer.transform);
                    if (!owner.StartsWith("Tor_")) continue;

                    string wears = renderer.sharedMaterial == null ? "none" : renderer.sharedMaterial.name;
                    string key = owner + " / " + wears;

                    masses.TryGetValue(key, out int seen);
                    masses[key] = seen + 1;
                }

                foreach (var pair in masses)
                    Debug.Log($"[Ground] mass {pair.Key} x{pair.Value}");

                largest.Sort((a, b) => b.Size.CompareTo(a.Size));
                for (int i = 0; i < largest.Count && i < 10; i++)
                    Debug.Log($"[Ground] biggest {largest[i].Size:0} m: {largest[i].Name} {largest[i].Wearing}");

                near.Sort((a, b) => a.Away.CompareTo(b.Away));
                for (int i = 0; i < near.Count && i < 24; i++)
                    Debug.Log($"[Ground] near {near[i].Away:0} m: {near[i].Name} "
                              + $"{near[i].Size.x:0.0}x{near[i].Size.y:0.0}x{near[i].Size.z:0.0} m "
                              + near[i].Wearing);

                string path = System.IO.Path.Combine(shots, $"ground-{chapter}-{level}.png");
                System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
                Debug.Log($"[Ground] {chapter}-{level}: {path}");

                // And the whole level from above, at the angle the reference pictures are
                // drawn at: the roads, the river and the colour of the country in one frame.
                var over = new GameObject("Over").AddComponent<Camera>();
                float span = grid.Width * TileGrid.TileSize;
                over.transform.position = new Vector3(span * 0.5f, span * 0.62f, -span * 0.25f);
                over.transform.LookAt(new Vector3(span * 0.5f, 0f, span * 0.45f));
                over.fieldOfView = 55f;
                over.farClipPlane = 4000f;
                over.clearFlags = camera.clearFlags;
                over.backgroundColor = camera.backgroundColor;

                var wide = new RenderTexture(1800, 1000, 24);
                over.targetTexture = wide;
                over.Render();

                RenderTexture.active = wide;
                var aerial = new Texture2D(1800, 1000, TextureFormat.RGB24, false);
                aerial.ReadPixels(new Rect(0, 0, 1800, 1000), 0, 0);
                aerial.Apply();
                RenderTexture.active = null;

                // The same near view, taken by the far camera moved down: if this one is
                // right and the first is yellow, the fault is in how that camera is made and
                // not in the country it is pointed at.
                var twin = new GameObject("Twin").AddComponent<Camera>();
                twin.transform.position = camera.transform.position;
                twin.transform.rotation = camera.transform.rotation;
                twin.fieldOfView = 55f;
                twin.farClipPlane = 4000f;
                twin.clearFlags = over.clearFlags;
                twin.backgroundColor = over.backgroundColor;

                var twinTexture = new RenderTexture(1600, 900, 24);
                twin.targetTexture = twinTexture;
                twin.Render();

                RenderTexture.active = twinTexture;
                var twinShot = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                twinShot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                twinShot.Apply();
                RenderTexture.active = null;

                System.IO.File.WriteAllBytes(System.IO.Path.Combine(shots, $"twin-{chapter}-{level}.png"),
                                             twinShot.EncodeToPNG());
                Object.DestroyImmediate(twin.gameObject);

                string overPath = System.IO.Path.Combine(shots, $"over-{chapter}-{level}.png");
                System.IO.File.WriteAllBytes(overPath, aerial.EncodeToPNG());
                Debug.Log($"[Ground] {chapter}-{level} from above: {overPath}");

                // And the step the river comes over, close to: the one thing a picture of the
                // whole level cannot show.
                int step = Waterfalls.Step(map);
                if (step >= 0)
                {
                    var brink = Vec2.FromTile(grid, step);
                    float top = grid.SurfaceElevation(brink.X, brink.Y) * runner.HeightScale;

                    // <b>Downstream of the step, looking back up at it.</b> Standing off to
                    // one side and above, the camera ended up in the pool under the shelf and
                    // the picture was a wall of water with no fall in it. The river runs down
                    // the grid, so downstream is the way the rows fall: back from the brink,
                    // at the height of the water it lands in.
                    float under = grid.SurfaceElevation(brink.X, brink.Y - TileGrid.TileSize * 3f)
                                  * runner.HeightScale;

                    var close = new GameObject("Fall").AddComponent<Camera>();
                    // <b>Aimed at the fall itself, not at where the fall should be.</b> The
                    // camera was pointed at the step's own tile and came back with a bush,
                    // a bridge and a wall of water in turn; the decorator names the sheet it
                    // builds, so the picture is taken of that.
                    var water = Find(root.transform, "Waterfall") ?? FindLike(root.transform, "WaterFall");
                    var aim = water != null
                        ? water.position
                        : new Vector3(brink.X, (top + under) * 0.5f, brink.Y);

                    // Above the canopy, looking down at it: a tree is fourteen metres and at
                    // nine the camera stood in one. Twenty-six up and twenty-six back is a
                    // line of sight that clears the wood and still reads the face of the fall.
                    Debug.Log($"[Ground] {chapter}-{level} fall at "
                              + (water != null ? aim.ToString("0.0") : "no sheet built"));

                    // In the channel, just above the water, looking upstream at the face. Over
                    // the canopy the fall was hidden under it; at eye level on the bank the
                    // camera stood in a bush. The one line of sight this country always keeps
                    // open is the water's own.
                    // Standing on the ground it actually finds, rather than at a height
                    // guessed from the fall: downstream the shelf tapers back up, and a camera
                    // put two metres over the pool ended up inside a hillside.
                    var spot = aim + new Vector3(0f, 0f, -15f);
                    float under2 = grid.SurfaceElevation(spot.x, spot.z) * runner.HeightScale;

                    close.transform.position = aim + new Vector3(0f, 3.5f, -46f);
                    close.transform.LookAt(aim);
                    close.fieldOfView = 55f;
                    close.farClipPlane = 3000f;
                    close.clearFlags = camera.clearFlags;
                    close.backgroundColor = camera.backgroundColor;

                    var near2 = new RenderTexture(1400, 900, 24);
                    close.targetTexture = near2;

                    // Twice, as above: the first render of a scene comes back wrong.
                    close.Render();
                    close.Render();

                    RenderTexture.active = near2;
                    var fall = new Texture2D(1400, 900, TextureFormat.RGB24, false);
                    fall.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0);
                    fall.Apply();
                    RenderTexture.active = null;

                    string fallPath = System.IO.Path.Combine(shots, $"fall-{chapter}-{level}.png");
                    System.IO.File.WriteAllBytes(fallPath, fall.EncodeToPNG());
                    Debug.Log($"[Ground] {chapter}-{level} fall: {fallPath}");

                    Object.DestroyImmediate(close.gameObject);
                }

                Object.DestroyImmediate(over.gameObject);
                Object.DestroyImmediate(camera.gameObject);
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
