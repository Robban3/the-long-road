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

            // And the courtyard, from where the player actually watches it: the game
            // camera is forty-odd metres up at a slant, not a hundred straight down, and
            // a crate a metre and a quarter tall is a pixel from one and a thing from the
            // other. Every yard that has been reported empty was reported off the wrong
            // camera.
            Shoot(box.center + new Vector3(0f, 34f, -34f), box.center,
                  System.IO.Path.Combine(shots, "mockup-yard.png"));

            // Then from just inside the gateway, looking up the yard at the hall — which
            // is the one view the layout is actually composed for.
            Shoot(new Vector3(box.center.x, box.min.y + 6f, box.min.z + 13f),
                  new Vector3(box.center.x, box.min.y + 3f, box.max.z),
                  System.IO.Path.Combine(shots, "mockup-inside.png"));

            // And the back, which nothing had ever photographed — which is how a timber
            // gallery ended up sitting on the one wall panel the keep was later built
            // into, and stayed there for a pass reading as a plank left on the parapet.
            Shoot(box.center + new Vector3(0f, box.size.y * 0.25f, span * 1.15f), box.center,
                  System.IO.Path.Combine(shots, "mockup-back.png"));

            // And the joint itself, close. Judging where the timber meets the stone from a
            // picture of the whole castle is judging a two-metre detail at forty-six
            // metres, which is how it was called right three times and was wrong.
            var joint = new Vector3(box.center.x, box.max.y - 3f, box.min.z);

            Shoot(joint + new Vector3(6f, 1f, -9f), joint,
                  System.IO.Path.Combine(shots, "mockup-joint.png"));

            // The banner and the paving under each of the pack's four palettes, side by
            // side, because the plan hangs red cloth and the atlas this game has always
            // used draws it blue. Which of the four is the red one is not a thing to
            // reason about: the UVs point where they point, and the only way to know is
            // to put the same prefab under each and look.
            Swatches(shots);

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sun);

            Debug.Log($"[Mockup] pictures in {shots}");
        }

        /// <summary>
        /// One banner and one paving flag under each of the pack's palettes.
        ///
        /// PolygonKnights ships four texture atlases and a dark variant of each, and
        /// everything this game builds has used the first one since the day it was wired
        /// up. Nothing was wrong with that until the plan asked for a colour the first
        /// atlas does not have.
        /// </summary>
        static void Swatches(string shots)
        {
            const string mats = "Assets/Synty/PolygonKnights/Materials";
            const string props = "Assets/Synty/PolygonKnights/Prefabs/Props";

            var sample = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{props}/SM_Prop_Banner_01.prefab");

            if (sample == null) { Debug.Log("[Mockup] no banner to swatch"); return; }

            var stage = new GameObject("Swatches");
            var names = new[] { "PolyKnights_01", "PolyKnights_02",
                                "PolyKnights_03", "PolyKnights_04" };

            float step = 0f;

            foreach (string name in names)
            {
                var skin = AssetDatabase.LoadAssetAtPath<Material>($"{mats}/{name}.mat");
                if (skin == null) continue;

                var piece = Object.Instantiate(sample, stage.transform);
                piece.transform.position = new Vector3(step, 0f, 0f);

                foreach (var r in piece.GetComponentsInChildren<Renderer>())
                {
                    var swap = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < swap.Length; i++) swap[i] = skin;
                    r.sharedMaterials = swap;
                }

                step += 2.5f;
            }

            var box = ModelScaling.Measure(stage);

            Shoot(box.center + new Vector3(0f, 0f, -Mathf.Max(box.size.x, 4f) * 1.2f),
                  box.center, System.IO.Path.Combine(shots, "mockup-livery.png"));

            Debug.Log($"[Mockup] palettes left to right: {string.Join(", ", names)}");
            Object.DestroyImmediate(stage);
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
