using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The pack's waterfall, taken apart and photographed from three sides.
    ///
    /// Four attempts to stand it in a step all failed differently, which is the mark of
    /// not knowing what the thing is. This says: what its pieces are, which way its own
    /// axes run, where its pivot sits, and what it looks like from the front, the side and
    /// above.
    /// </summary>
    public static class FallModel
    {
        public static void Run()
        {
            const string path =
                "Assets/Synty/PolygonNature/Prefabs/Terrain/SM_River_Plane_WaterFall_01.prefab";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.Log("[FallModel] missing " + path); return; }

            var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            piece.transform.position = Vector3.zero;
            piece.transform.rotation = Quaternion.identity;

            var whole = ModelScaling.Measure(piece);
            Debug.Log($"[FallModel] whole: size {whole.size.x:0.00} x {whole.size.y:0.00} x {whole.size.z:0.00} m, "
                      + $"centre {whole.center.x:0.00}, {whole.center.y:0.00}, {whole.center.z:0.00}, "
                      + $"min y {whole.min.y:0.00}");

            foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>(true))
            {
                var box = renderer.bounds;
                var mesh = renderer.GetComponent<MeshFilter>();

                Debug.Log($"[FallModel] part {renderer.name}: "
                          + $"{box.size.x:0.00} x {box.size.y:0.00} x {box.size.z:0.00} m at "
                          + $"{box.center.x:0.00}, {box.center.y:0.00}, {box.center.z:0.00}; "
                          + $"local pos {renderer.transform.localPosition}; "
                          + $"mesh {(mesh == null || mesh.sharedMesh == null ? "none" : mesh.sharedMesh.name)}; "
                          + $"material {(renderer.sharedMaterial == null ? "none" : renderer.sharedMaterial.name)}");
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

            float wide = Mathf.Max(whole.size.x, whole.size.y, whole.size.z) * 1.4f + 4f;

            Shoot(whole.center + new Vector3(0f, 0f, -wide), whole.center, "fall-front");
            Shoot(whole.center + new Vector3(wide, 0f, 0f), whole.center, "fall-side");
            Shoot(whole.center + new Vector3(0.01f, wide, 0f), whole.center, "fall-above");

            Object.DestroyImmediate(sun.gameObject);
            Object.DestroyImmediate(piece);
        }

        static void Shoot(Vector3 from, Vector3 at, string name)
        {
            var camera = new GameObject("Shot").AddComponent<Camera>();
            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.42f, 0.30f);
            camera.fieldOfView = 45f;

            var texture = new RenderTexture(900, 700, 24);
            camera.targetTexture = texture;

            // Twice: the first render of a fresh scene comes back wrong. See GroundPhotos.
            camera.Render();
            camera.Render();

            RenderTexture.active = texture;
            var shot = new Texture2D(900, 700, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 900, 700), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke", name + ".png");
            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log("[FallModel] " + path);

            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
