using System.Collections.Generic;
using TheVeil.App;
using TheVeil.Sim;
using TheVeil.View;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheVeil.Tests
{
    /// <summary>
    /// That stumps and trees are dug into the ground rather than stood on top of it.
    ///
    /// A model with a root flare set on the surface stands on the flare like a stool, and
    /// the fen is made of those: swamp trees, stumps, root balls. It went out that way
    /// once because a set decides nothing about depth on its own — the place a prop is
    /// put decides it, and only some places bury anything. Putting roots in the rocks,
    /// which rest on the ground because a stone does, is enough to break it.
    ///
    /// So the rule is checked on what the scenes actually hold: any set with a stump or a
    /// root in it has to be a set the decorator buries, and the fen's trees have to carry
    /// a depth of their own.
    /// </summary>
    public class PropSeatingTests
    {
        static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/PlayLevel.unity",
            "Assets/_Project/Scenes/LevelPreview.unity"
        };

        /// <summary>Sets the decorator seats below the surface wherever it places them.</summary>
        static readonly HashSet<string> AlwaysBuried = new HashSet<string> { "Timber", "DeadTrees" };

        /// <summary>Models drawn with a footing: set on the surface, they stand on it.</summary>
        static bool HasFooting(GameObject model)
        {
            if (model == null) return false;

            string name = model.name;
            return name.Contains("Stump") || name.Contains("Root");
        }

        static IEnumerable<(string Name, PropSet Set)> SetsOf(BiomeDecor decor)
        {
            foreach (var field in typeof(BiomeDecor).GetFields())
            {
                if (field.FieldType != typeof(PropSet)) continue;

                var set = (PropSet)field.GetValue(decor);
                if (set != null && set.Any) yield return (field.Name, set);
            }
        }

        static void Check(BiomeDecor decor, string where)
        {
            foreach (var (name, set) in SetsOf(decor))
            {
                bool footed = false;
                foreach (var model in set.Models) footed |= HasFooting(model);

                if (!footed) continue;

                Assert.IsTrue(AlwaysBuried.Contains(name) || set.Sink > 0f,
                    $"{where}: {name} holds stumps or roots and is laid on the surface — "
                    + "they will stand on their roots. Put them in Timber or the dead trees, "
                    + "which the decorator buries, or give the set a Sink of its own.");
            }
        }

        [Test]
        public void NothingWithRootsIsLeftStandingOnThem()
        {
            foreach (string path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var runner in root.GetComponentsInChildren<LevelRunner>(true))
                        {
                            Check(runner.Decor, $"{scene.name} forest");
                            foreach (var look in runner.Looks)
                                if (look != null && look.Dressed) Check(look.Decor, $"{scene.name} {look.Biome}");
                        }

                        foreach (var preview in root.GetComponentsInChildren<LevelPreview>(true))
                        {
                            Check(preview.Decor, $"{scene.name} forest");
                            foreach (var look in preview.Looks)
                                if (look != null && look.Dressed) Check(look.Decor, $"{scene.name} {look.Biome}");
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void TheFensTreesAreBuriedToTheirFlare()
        {
            // Every tree in that country is drawn with roots spreading from the trunk, so
            // every tree set there carries a depth. A twentieth of a nine-metre trunk is
            // the flare and no more — buried like a stump they stand in holes.
            foreach (string path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var runner in root.GetComponentsInChildren<LevelRunner>(true))
                        {
                            var fen = runner.LookFor(Biome.Marsh);
                            if (fen == null || !fen.Dressed) continue;

                            foreach (var set in new[] { fen.Decor.Pines, fen.Decor.Trees, fen.Decor.Birch })
                            {
                                if (set == null || !set.Any) continue;

                                Assert.Greater(set.Sink, 0f, $"{scene.name}: a fen tree set rests on the surface");
                                Assert.Less(set.Sink, 0.2f, $"{scene.name}: a fen tree set is buried like a stump");
                            }
                        }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
