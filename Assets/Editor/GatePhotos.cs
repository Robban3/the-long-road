using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The gate into the enchanted wood, photographed: `The Veil &gt; Gate Photos`.
    ///
    /// <b>Three levels and nothing else in the game looks like them.</b> The portal stands
    /// on the last level of the country before the wood, the first level of the wood and
    /// its last - see Sim.Portals - and those are the only three places in a thousand
    /// levels where the player walks through something rather than past it. A piece of
    /// content that appears three times is a piece nobody will notice is wrong.
    ///
    /// Two shots of each: one from a standing height looking at the gate, which is how it
    /// will be seen, and one from the camera the game is played at, which is the only angle
    /// the player actually gets. The second is the honest one and the first is the one that
    /// shows what the model is doing.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.GatePhotos.Run
    /// </summary>
    public static class GatePhotos
    {
        [UnityEditor.MenuItem("The Veil/Gate Photos")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Gate] PlayLevel has no LevelRunner."); return; }

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilGate");
            System.IO.Directory.CreateDirectory(shots);

            // Every level that has one, asked of Portals rather than listed here, so a
            // tour that moves the wood moves the photographs with it.
            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters + 2; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    if (Portals.On(chapter, level) == 0) continue;

                    var root = SmokeTest.Build(runner, chapter, level, out var map);
                    var gate = Find(root.transform);

                    if (gate == null)
                    {
                        Debug.Log($"[Gate] {chapter}-{level}: Portals wants a gate here and none was built");
                        Object.DestroyImmediate(root);
                        continue;
                    }

                    var box = TheVeil.View.ModelScaling.Measure(gate.gameObject);

                    Debug.Log($"[Gate] {chapter}-{level} ({Biomes.Of(chapter)}): {gate.name} "
                              + $"{box.size.x:0.0} x {box.size.y:0.0} x {box.size.z:0.0} m "
                              + $"at {box.center.x:0}, {box.center.z:0}");

                    // From in front of it, at the height of somebody about to walk through.
                    // The gate faces the middle of the map, so the camera stands on its
                    // own forward and turns round - the same arithmetic the fall report
                    // learned the hard way, where a camera placed by compass bearing spent
                    // an hour photographing the bank instead of the water.
                    float away = box.size.y + 18f;

                    Shoot(box.center + gate.forward * away + Vector3.up * (box.size.y * 0.2f),
                          box.center + Vector3.up * (box.size.y * 0.35f),
                          System.IO.Path.Combine(shots, $"gate-{chapter}-{level}-near.png"));

                    // And from where the game is played.
                    Shoot(box.center + new Vector3(0f, PlayHeight, -PlayHeight), box.center,
                          System.IO.Path.Combine(shots, $"gate-{chapter}-{level}-play.png"));

                    Object.DestroyImmediate(root);
                }
            }

            Debug.Log($"[Gate] pictures in {shots}");
        }

        /// <summary>The height the game's own camera sits at, in metres.</summary>
        const float PlayHeight = 33f;

        /// <summary>
        /// The gate's tree, which is the piece with a body.
        ///
        /// Asked for by having a size, as the fall report asks for its sheet: the arch and
        /// the light are instantiated at the same spot and one of the three is a particle
        /// effect, which measures nought by nought by nought before it has emitted
        /// anything. A camera pointed at that photographs the sky.
        /// </summary>
        static Transform Find(Transform at)
        {
            foreach (Transform child in at)
            {
                if (child.name.StartsWith("SM_Env_Tree_Portal")
                    && TheVeil.View.ModelScaling.Measure(child.gameObject).size.y > 1f)
                    return child;

                var found = Find(child);
                if (found != null) return found;
            }

            return null;
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Gate camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.14f, 0.26f);
            camera.farClipPlane = 2000f;

            var texture = new RenderTexture(1100, 1100, 24);
            camera.targetTexture = texture;

            // Twice, the first thrown away: the first render of a freshly built level comes
            // back with half its surfaces blown out. See GroundPhotos.
            camera.Render();
            camera.Render();

            RenderTexture.active = texture;
            var shot = new Texture2D(1100, 1100, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 1100, 1100), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);

            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log($"[Gate] {System.IO.Path.GetFileName(path)}");
        }
    }
}
