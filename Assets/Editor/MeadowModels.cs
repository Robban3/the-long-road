using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The meadow pack's floor, laid out and photographed: `The Veil > Meadow Models`.
    ///
    /// <b>Written after three rounds of guessing at why a meadow looks like a lawn.</b>
    /// Density was raised, beds were laid, the flowers were fitted by width instead of by
    /// height - and each round cost a rebuild and a photograph of a whole level to find out
    /// that the colour still did not read. The models are ten centimetres of the problem
    /// and a level is two hundred and fifty metres of it.
    ///
    /// So each piece is stood on its own patch of ground at the size the decorator gives
    /// it, measured, and shot from above and from the side. What comes out says which of
    /// them is a plant with a few blooms on it and which is a piece of flowering ground -
    /// which is the whole question, and is not answerable from a prefab's name.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.MeadowModels.Run
    /// </summary>
    public static class MeadowModels
    {
        const string Dir = "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs";

        /// <summary>How wide each piece is laid, in metres: the decorator's own bed width.</summary>
        const float Laid = 2.6f;

        /// <summary>How far apart they stand, in metres.</summary>
        const float Apart = 4f;

        public static void Run()
        {
            string[] names =
            {
                "SM_Env_Flowers_Flat_01", "SM_Env_Flowers_Flat_02", "SM_Env_Flowers_Flat_03",
                "SM_Env_Wildflowers_01", "SM_Env_Wildflowers_02", "SM_Env_Wildflowers_03",
                "SM_Env_Wildflowers_Patch_01", "SM_Env_Wildflowers_Patch_02",
                "SM_Env_Wildflowers_Patch_03", "SM_Env_Sunflower_01",
                "SM_Env_Grass_Short_Plane_01", "SM_Env_Grass_Med_Plane_01",
                "SM_Env_Grass_Tall_Plane_01", "SM_Env_Ground_Mound_Large_01"
            };

            var row = new GameObject("Meadow");

            // Something green to stand them on, so a flat mat is seen against ground
            // rather than against the sky.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(row.transform, false);
            floor.transform.localScale = new Vector3(12f, 1f, 4f);
            floor.transform.position = new Vector3(names.Length * Apart * 0.5f, 0f, 0f);

            var grass = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            grass.SetColor("_BaseColor", new Color(0.44f, 0.58f, 0.33f));
            floor.GetComponent<MeshRenderer>().sharedMaterial = grass;

            float along = 0f;

            foreach (string name in names)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{name}.prefab");
                if (model == null) { Debug.Log($"[Meadow] missing {name}"); continue; }

                var stood = (GameObject)PrefabUtility.InstantiatePrefab(model, row.transform);

                // Fitted across, which is how the beds lay them.
                ModelScaling.FitToFootprint(stood, Laid, 0f);

                var box = ModelScaling.Measure(stood);
                stood.transform.position += new Vector3(along - box.center.x, -box.min.y,
                                                        -box.center.z);

                box = ModelScaling.Measure(stood);
                Debug.Log($"[Meadow] {name}: {box.size.x:0.00} x {box.size.y:0.00} x "
                          + $"{box.size.z:0.00} m laid at {Laid} m across");

                along += Apart;
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, 140f, 0f);

            float middle = (names.Length - 1) * Apart * 0.5f;
            float wide = names.Length * Apart * 0.62f;

            Shoot(new Vector3(middle, wide * 0.9f, -wide * 0.45f),
                  new Vector3(middle, 0f, 0f), "meadow-above");
            Shoot(new Vector3(middle, 2.2f, -wide * 0.7f),
                  new Vector3(middle, 1f, 0f), "meadow-side");

            Object.DestroyImmediate(sun.gameObject);
            Object.DestroyImmediate(row);
        }

        static void Shoot(Vector3 from, Vector3 at, string name)
        {
            var camera = new GameObject("Shot").AddComponent<Camera>();
            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.66f, 0.80f, 0.85f);
            camera.fieldOfView = 45f;

            var texture = new RenderTexture(1800, 700, 24);
            camera.targetTexture = texture;

            // Twice: the first render of a fresh scene comes back wrong. See GroundPhotos.
            camera.Render();
            camera.Render();

            RenderTexture.active = texture;
            var shot = new Texture2D(1800, 700, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 1800, 700), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke",
                                                 name + ".png");
            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log("[Meadow] " + path);

            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
