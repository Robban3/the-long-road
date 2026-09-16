using System.Collections.Generic;
using System.Text;
using TheVeil.App;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Everything standing on a level, measured and sorted by how tall it is:
    /// `The Veil > Scale Report`.
    ///
    /// <b>Because the same bug keeps coming back wearing a different prop.</b> A wide flat
    /// thing fitted to a height is scaled by its aspect ratio, and this codebase has now
    /// shipped a five-metre cart wheel, an eight-metre skull, a four-metre cobble, a
    /// twelve-metre well and a marquee-sized tent. Every one of them was found by somebody
    /// looking at a screenshot and saying "what is that", which is the most expensive way
    /// to find it and the only way there has been.
    ///
    /// A prop that is three times taller than it is wide and stands above a house is
    /// almost always one of these. That is a thing a machine can look for.
    ///
    /// Trees and the castle are exempt: a pine is supposed to be tall and thin, and a
    /// keep is supposed to be the tallest thing on the field.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.ScaleReport.Run
    /// </summary>
    public static class ScaleReport
    {
        /// <summary>How tall a prop has to be before it is worth a second look, in metres.</summary>
        const float Towering = 8f;

        /// <summary>And how much taller than it is wide, before it is worth reporting.</summary>
        const float Lanky = 2.5f;

        /// <summary>What a pine is allowed to be without anybody worrying.</summary>
        static readonly string[] Excused =
        {
            "Tree", "Pine", "Birch", "Palm", "Castle", "Tower", "Church", "Spire",
            "Banner", "Flag", "Lampost", "Chimney", "Mast"
        };

        [MenuItem("The Veil/Scale Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                         OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Scale] no LevelRunner in the scene"); return; }

            var sheet = new StringBuilder();
            sheet.AppendLine("[Scale] anything built at a size it probably should not be");

            for (int level = 1; level <= 10; level++)
            {
                var root = SmokeTest.Build(runner, 1, level, out _);

                var tall = new List<(string Name, Vector3 Size, Vector3 At)>();
                Sweep(root.transform, tall);

                tall.Sort((a, b) => b.Size.y.CompareTo(a.Size.y));

                foreach (var thing in tall)
                {
                    sheet.AppendLine($"[Scale] 1-{level,-2} {thing.Name,-44} "
                                     + $"{thing.Size.x,5:0.0} x {thing.Size.y,5:0.0} "
                                     + $"x {thing.Size.z,5:0.0} m "
                                     + $"at {thing.At.x:0}, {thing.At.z:0}");
                }

                Object.DestroyImmediate(root);
            }

            Debug.Log(sheet.ToString());
        }

        static void Sweep(Transform at, List<(string, Vector3, Vector3)> found)
        {
            foreach (Transform child in at)
            {
                // Measured whole, then its children looked at separately only if it is not
                // itself a thing: a castle is one object made of two hundred, and reporting
                // every course of its wall would bury the one prop that is wrong.
                var box = ModelScaling.Measure(child.gameObject);

                if (box.size.y >= Towering
                    && box.size.y >= Lanky * Mathf.Max(box.size.x, box.size.z)
                    && !Exempt(child.name))
                {
                    found.Add((child.name, box.size, child.position));
                    continue;
                }

                Sweep(child, found);
            }
        }

        static bool Exempt(string name)
        {
            foreach (string word in Excused)
                if (name.Contains(word)) return true;

            return false;
        }
    }
}
