using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// One prefab, taken apart and measured: `unity run . -- -executeMethod
    /// TheVeil.Editor.ModelCheck.Run -model &lt;path&gt;`.
    ///
    /// <b>Written because "that one model is wrong" keeps costing a rebuild to find
    /// out.</b> A waterfall hung ten metres below its own pivot, a wagon stood on its end,
    /// a cactus floats a metre off the ground - each was found by photographing a whole
    /// level and then guessing. What decides all three is the same handful of numbers:
    /// where the pivot is, where the renderers are against it, and whether the pieces
    /// agree with each other.
    ///
    /// So this prints them. Every renderer under the prefab, its own bounds, and how far
    /// its lowest point sits above the root - which is the number that makes a prop float
    /// when the placer seats it by its pivot.
    /// </summary>
    public static class ModelCheck
    {
        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] != "-model") continue;

                Look(args[i + 1]);
            }
        }

        static void Look(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) { Debug.Log($"[Model] missing {path}"); return; }

            var stood = (GameObject)PrefabUtility.InstantiatePrefab(model);
            stood.transform.position = Vector3.zero;
            stood.transform.rotation = Quaternion.identity;

            var whole = ModelScaling.Measure(stood);

            Debug.Log($"[Model] {model.name}: {whole.size.x:0.00} x {whole.size.y:0.00} x "
                      + $"{whole.size.z:0.00} m, lowest point {whole.min.y:0.00} above its pivot, "
                      + $"centre {whole.center.x:0.00}, {whole.center.y:0.00}, {whole.center.z:0.00}");

            foreach (var renderer in stood.GetComponentsInChildren<MeshRenderer>(true))
            {
                var box = renderer.bounds;

                Debug.Log($"[Model]   {renderer.name}"
                          + (renderer.gameObject.activeInHierarchy ? "" : " (off)")
                          + $": {box.size.x:0.00} x {box.size.y:0.00} x {box.size.z:0.00} m, "
                          + $"lowest {box.min.y:0.00}, local pos {renderer.transform.localPosition}");
            }

            var group = stood.GetComponent<LODGroup>();
            if (group != null)
                Debug.Log($"[Model]   an LODGroup of {group.GetLODs().Length} levels, "
                          + $"size {group.size:0.00}");

            Object.DestroyImmediate(stood);
        }
    }
}
