using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The first level of every built chapter, photographed into the project:
    /// `The Veil > Demo > Photograph Every Chapter`.
    ///
    /// <b>For showing the game rather than for checking it.</b> The smoke test's sheets are
    /// a contact print - ten cells, low camera, whatever the level looks like from one fixed
    /// spot - and they are meant to catch faults. These are one picture per country, taken
    /// from above at the angle the reference art uses, and they live in the project so they
    /// can be looked at from the Project window rather than dug out of a temporary folder.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.DemoPhotos.Run
    /// </summary>
    public static class DemoPhotos
    {
        /// <summary>Where the pictures are kept.</summary>
        const string Folder = "Assets/_Project/Demo";

        [MenuItem("The Veil/Demo/Photograph Every Chapter")]
        public static void Run()
        {
            if (!System.IO.Directory.Exists(Folder)) System.IO.Directory.CreateDirectory(Folder);

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Demo] no LevelRunner in the scene"); return; }

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                var root = SmokeTest.Build(runner, chapter, 1, out var map);
                var grid = map.Grid;

                // Every model at its nearest detail, and nothing wearing a colour somebody
                // painted on it: both of these bit the other photographers here.
                foreach (var group in root.GetComponentsInChildren<LODGroup>(true)) group.ForceLOD(0);
                foreach (var painted in root.GetComponentsInChildren<Renderer>(true))
                    painted.SetPropertyBlock(null);

                var look = runner.LookFor(Biomes.Of(chapter));
                RenderSettings.fog = false;

                float span = grid.Width * TileGrid.TileSize;

                var camera = new GameObject("Demo camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(span * 0.5f, span * 0.58f, -span * 0.22f);
                camera.transform.LookAt(new Vector3(span * 0.5f, 0f, span * 0.45f));
                camera.fieldOfView = 52f;
                camera.farClipPlane = 4000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = look != null ? look.SkyColor : new Color(0.66f, 0.80f, 0.85f);

                var texture = new RenderTexture(1920, 1080, 24);
                camera.targetTexture = texture;

                // Twice, the first thrown away: the first render of a freshly built level
                // comes back with half its surfaces blown out. See GroundPhotos.
                camera.Render();
                camera.Render();

                RenderTexture.active = texture;
                var shot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                string path = $"{Folder}/Chapter{chapter}_{Name(chapter)}.png";
                System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
                Debug.Log($"[Demo] {chapter}-1 ({Name(chapter)}): {path}");

                Object.DestroyImmediate(camera.gameObject);
                if (root != null) Object.DestroyImmediate(root);
            }

            AssetDatabase.Refresh();
        }

        /// <summary>The country a chapter is set in, for the file's name.</summary>
        static string Name(int chapter) => Biomes.Of(chapter).ToString();
    }
}
