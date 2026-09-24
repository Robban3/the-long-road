using TheVeil.App;
using TheVeil.Sim;
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
