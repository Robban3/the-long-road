using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The castle on its own, built straight out of the kit: `The Veil > Castle Mockup`.
    ///
    /// <b>Written because looking at it was costing more than changing it.</b> Seeing a
    /// castle used to take two Unity runs — Set Up Play Scene to write the kit into the
    /// scene's serialized decor, then Castle Report to open that scene, generate a whole
    /// level and go looking for the building in it — and most of that work is about
    /// everything except the castle.
    ///
    /// This loads the same kit the setup loads, calls the same builder the decorator
    /// calls, and photographs what comes out. No scene, no terrain, no encounters. It is
    /// the difference between trying something and committing to it, and the whole reason
    /// the castle took as many passes as it did is that there was no cheap way to try.
    ///
    /// What it deliberately cannot tell you is whether the castle sits right *on a level*
    /// — the ground it stands on, what grows through it, where the goal is. That is still
    /// Castle Report's job, and this does not replace it.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.CastleMockup.Run
    /// </summary>
    public static class CastleMockup
    {
        [MenuItem("The Veil/Castle Mockup")]
        public static void Run()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            var decor = TheVeilSetup.ForestDecor();
            if (decor == null || decor.Kit == null || !decor.Kit.CanBuildCastle)
            {
                Debug.Log("[Mockup] the forest kit cannot build a castle");
                return;
            }

            var sun = new GameObject("Mockup sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(32f, 140f, 0f);

            var root = new GameObject("Mockup");
            var castle = BuildingBuilder.Castle(root.transform, decor.Kit,
                                                new DeterministicRandom(7));

            if (castle == null) { Debug.Log("[Mockup] nothing was built"); return; }

            var box = ModelScaling.Measure(castle);
            float span = Mathf.Max(box.size.x, box.size.z);

            Debug.Log($"[Mockup] {box.size.x:0.0} x {box.size.y:0.0} x {box.size.z:0.0} m, "
                      + $"{castle.transform.childCount} pieces");

            // The gate square on, which is the face the artwork is drawn from and the one
            // the player meets; then a three-quarter, which is where a silhouette reads;
            // then above, for the plan.
            Shoot(box.center + new Vector3(0f, box.size.y * 0.25f, -span * 1.15f), box.center,
                  System.IO.Path.Combine(shots, "mockup-gate.png"));

            Shoot(box.center + Quaternion.Euler(18f, 35f, 0f) * new Vector3(0f, 0f, -span * 1.5f),
                  box.center, System.IO.Path.Combine(shots, "mockup-corner.png"));

            Shoot(box.center + new Vector3(0f, span * 1.2f, -0.01f), box.center,
                  System.IO.Path.Combine(shots, "mockup-above.png"));

            // And the joint itself, close. Judging where the timber meets the stone from a
            // picture of the whole castle is judging a two-metre detail at forty-six
            // metres, which is how it was called right three times and was wrong.
            var joint = new Vector3(box.center.x, box.max.y - 3f, box.min.z);

            Shoot(joint + new Vector3(6f, 1f, -9f), joint,
                  System.IO.Path.Combine(shots, "mockup-joint.png"));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sun);

            Debug.Log($"[Mockup] pictures in {shots}");
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Mockup camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);

            var rt = new RenderTexture(1400, 900, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1400, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
