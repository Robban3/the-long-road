using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The building kit's own pieces, photographed one at a time: `The Veil > Kit Report`.
    ///
    /// Written after four rounds of measuring the wrong thing. A house in the villages
    /// came out looking like two and three houses stacked, roof meeting floor; every
    /// measurement of *pairs* of buildings said they did not touch, which was true and
    /// useless, because the stack is inside one building. The way to see what a stack is
    /// made of is to look at what it is stacked from.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.KitReport.Run
    /// </summary>
    public static class KitReport
    {
        const string Dir = "Assets/Synty/PolygonKnights/Prefabs/Buildings/";

        static readonly string[] Pieces =
        {
            "SM_Bld_House_Foundation_01",
            "SM_Bld_House_Room_01",
            "SM_Bld_House_TopRoomSmall_01",
            "SM_Bld_House_RoomTop_01"
        };

        [MenuItem("The Veil/Kit Report")]
        public static void Run()
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            var stage = new GameObject("Kit stage");
            float x = 0f;

            foreach (string name in Pieces)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + name + ".prefab");
                if (prefab == null)
                {
                    Debug.Log($"[Kit] {name}: not found.");
                    continue;
                }

                var piece = Object.Instantiate(prefab, stage.transform);
                piece.transform.position = new Vector3(x, 0f, 0f);

                var box = ModelScaling.Measure(piece);

                Debug.Log($"[Kit] {name}: {box.size.x:0.0} x {box.size.y:0.0} x {box.size.z:0.0} m, "
                          + $"foot at y {box.min.y:0.00}, top at y {box.max.y:0.0}.");

                x += 12f;
            }

            // And one built house beside them, to be compared against its own parts.
            var runner = Object.FindAnyObjectByType<TheVeil.App.LevelRunner>();
            Shoot(new Vector3(x * 0.5f - 6f, 9f, -34f), new Vector3(x * 0.5f - 6f, 4f, 0f),
                  System.IO.Path.Combine(shots, "kit-pieces.png"));

            Object.DestroyImmediate(stage);
            Debug.Log($"[Kit] picture in {shots}");
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Kit camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.42f, 0.5f);

            var rt = new RenderTexture(1400, 700, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1400, 700, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1400, 700), 0, 0);
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
