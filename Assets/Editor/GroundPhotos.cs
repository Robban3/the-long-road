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
                var road = map.Corridors[0].Tiles;
                int at = road[road.Count / 3];
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
                RenderSettings.fog = look != null && look.Fog;
                if (RenderSettings.fog)
                {
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = look.FogColor;
                    RenderSettings.fogDensity = look.FogDensity;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = look.SkyColor;
                }

                var texture = new RenderTexture(1600, 900, 24);
                camera.targetTexture = texture;
                camera.Render();

                RenderTexture.active = texture;
                var shot = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                // What is standing in the shot, nearest first: a picture shows something is
                // wrong and this says what it is.
                var near = new List<(float Away, string Name, Vector3 Size)>();
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var box = renderer.bounds;
                    float away = Vector3.Distance(camera.transform.position, box.center);
                    if (away < 12f && box.size.magnitude > 0.35f)
                        near.Add((away, renderer.transform.root == null ? renderer.name : Root(renderer.transform), box.size));
                }

                near.Sort((a, b) => a.Away.CompareTo(b.Away));
                for (int i = 0; i < near.Count && i < 18; i++)
                    Debug.Log($"[Ground] near {near[i].Away:0} m: {near[i].Name} "
                              + $"{near[i].Size.x:0.0}x{near[i].Size.y:0.0}x{near[i].Size.z:0.0} m");

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

                    var close = new GameObject("Fall").AddComponent<Camera>();
                    close.transform.position = new Vector3(brink.X + 26f, top + 9f, brink.Y - 26f);
                    close.transform.LookAt(new Vector3(brink.X, top - 3f, brink.Y));
                    close.fieldOfView = 50f;
                    close.farClipPlane = 3000f;
                    close.clearFlags = camera.clearFlags;
                    close.backgroundColor = camera.backgroundColor;

                    var near2 = new RenderTexture(1400, 900, 24);
                    close.targetTexture = near2;
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
