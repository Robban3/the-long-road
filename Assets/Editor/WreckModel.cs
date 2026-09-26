using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The broken wagon in every quarter turn, measured and photographed:
    /// `The Veil > Wreck Model`.
    ///
    /// <b>Because this is the third time the wreck has been turned by argument.</b> It
    /// went down as the artist drew it and read as a cart for a child; it was turned a
    /// quarter about its axle and read as a wagon gone over; and now it stands on its end
    /// with its wheels in the air. Each turn was chosen from a description of the last
    /// one, and a description of a rotation is not a rotation.
    ///
    /// So all six are laid in a row on a strip of ground with a man's height beside them
    /// and their boxes printed. A wagon lying down is broad and low - four or five metres
    /// across its length and about a metre and a half up. One standing on its end is the
    /// other way round, and the numbers say which is which before anybody looks.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.WreckModel.Run
    /// </summary>
    public static class WreckModel
    {
        const string Wreck =
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/Props/SM_Prop_Wagon_Broken_01.prefab";

        /// <summary>How far apart the turns stand, in metres.</summary>
        const float Apart = 7f;

        public static void Run()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Wreck);
            if (model == null) { Debug.Log("[Wreck] missing " + Wreck); return; }

            var row = new GameObject("Wrecks");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(row.transform, false);
            floor.transform.localScale = new Vector3(8f, 1f, 3f);
            floor.transform.position = new Vector3(Apart * 2.5f, 0f, 0f);
            floor.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit"));

            // The turns worth trying: none, a quarter and a half about the axle, and the
            // same three about the other horizontal axis. Yaw is left out - it spins the
            // wagon on the spot and changes nothing about which way is up.
            var turns = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(90f, 0f, 0f), new Vector3(180f, 0f, 0f),
                new Vector3(0f, 0f, 90f), new Vector3(0f, 0f, 180f), new Vector3(270f, 0f, 0f)
            };

            float along = 0f;

            foreach (var turn in turns)
            {
                var stood = (GameObject)PrefabUtility.InstantiatePrefab(model, row.transform);
                stood.transform.rotation = Quaternion.Euler(turn);

                // Fitted to the same height the decorator asks for, so what is measured is
                // what stands on the map rather than what is in the project folder.
                ModelScaling.Fit(stood, TerrainDecorator.WreckHeight, 0f);

                var box = ModelScaling.Measure(stood);
                stood.transform.position += new Vector3(along - box.center.x, -box.min.y,
                                                        -box.center.z);

                box = ModelScaling.Measure(stood);
                Debug.Log($"[Wreck] {turn.x:0} about x, {turn.z:0} about z: "
                          + $"{box.size.x:0.00} long x {box.size.y:0.00} tall x {box.size.z:0.00} deep");

                along += Apart;
            }

            // A man for scale, at the end of the row.
            var man = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            man.transform.SetParent(row.transform, false);
            man.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            man.transform.position = new Vector3(along, 0.9f, 0f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45f, 145f, 0f);

            float middle = (turns.Length - 1) * Apart * 0.5f;
            Shoot(new Vector3(middle, 6f, -26f), new Vector3(middle, 1f, 0f), "wreck-turns");

            Object.DestroyImmediate(sun.gameObject);
            Object.DestroyImmediate(row);
        }

        static void Shoot(Vector3 from, Vector3 at, string name)
        {
            var camera = new GameObject("Shot").AddComponent<Camera>();
            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.72f);
            camera.fieldOfView = 55f;

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
            Debug.Log("[Wreck] " + path);

            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
