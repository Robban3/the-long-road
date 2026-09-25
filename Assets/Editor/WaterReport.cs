using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// What the water on each level is actually made of: `The Veil > Water Report`.
    ///
    /// <b>Because "is the water moving?" is not a question a screenshot can answer.</b>
    /// A still picture of a river looks the same whether it flows or not, and the material
    /// a level ends up with is decided three deep - the country's own look, then the
    /// scene's default, then the builder's fallback - so reading the code is no better.
    /// This builds a level and reads the material off the mesh that was laid.
    ///
    /// Two of them per level, because they are two different things: what runs in the
    /// channel and what stands in the hollows. See BiomeLook.PoolWater.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.WaterReport.Run
    /// </summary>
    public static class WaterReport
    {
        [MenuItem("The Veil/Water Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Water] no LevelRunner in the scene"); return; }

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                var root = SmokeTest.Build(runner, chapter, 1, out _);
                var props = root.transform.Find("Props");

                int wet = 0;
                var map = LevelMaps.For(chapter, 1);
                for (int t = 0; t < map.Grid.TileCount; t++)
                    if (map.Grid[t] == TerrainType.Water || map.Grid[t] == TerrainType.Ford) wet++;

                Debug.Log($"[Water] {Biomes.Of(chapter)} ({chapter}-1), {wet} wet tile(s): "
                          + $"river {Wearing(props, "Water")}, pools {Wearing(props, "Marsh water")}");

                Object.DestroyImmediate(root);
            }

            Debug.Log("[Water] done");
        }

        /// <summary>The material on one of the water meshes, or that there is no such mesh.</summary>
        // Scanned rather than asked for by name: the sheets are two children among eight
        // thousand, and "none on this level" is the answer this report was written to
        // catch. It was the answer on all six countries, which is how the swept river was
        // found. See WaterSheet.
        static string Wearing(Transform props, string name)
        {
            Transform sheet = null;

            if (props != null)
                foreach (Transform child in props)
                    if (child.name == name && child.GetComponent<WaterSheet>() != null)
                    { sheet = child; break; }

            if (sheet == null) return "none on this level";

            var renderer = sheet.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return "no material";

            var material = renderer.sharedMaterial;

            // The speed is the whole question. Every water shader in the packs keeps it
            // under a name of its own, so the material is asked for the ones that exist.
            string speed = "";

            foreach (string named in new[] { "_WaveSpeed", "_FlowSpeed", "_Water_Speed",
                                             "_Distortion_Speed", "_Normal_Pan_Speed" })
                if (material.HasProperty(named))
                    speed += $" {named}={material.GetFloat(named):0.###}";

            // The shader and not the material's name. Every one of these is instanced at
            // build time and renamed "Water" (WaterMeshBuilder.Material), so the name says
            // nothing; the shader says which pack's water it is.
            return material.shader.name + (speed.Length == 0 ? " (no speed of its own)" : speed);
        }
    }
}
