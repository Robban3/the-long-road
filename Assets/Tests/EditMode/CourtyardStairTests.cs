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
    /// The stair in the castle's courtyard climbs to the wall walk.
    ///
    /// It stood at the size the pack draws it and stopped well under the parapet - a
    /// stair to nowhere, on the level every chapter ends on. Checked on the kit the game
    /// actually builds with, from the scene.
    /// </summary>
    public class CourtyardStairTests
    {
        /// <summary>How far the stair's top may sit from the walk and still meet it, in metres.</summary>
        const float Meets = 0.5f;

        [Test]
        public void TheStairReachesTheWallWalk()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Additive);
            var built = new List<GameObject>();

            try
            {
                var kits = new List<BuildingKit>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var runner in root.GetComponentsInChildren<LevelRunner>(true))
                    {
                        if (runner.Decor?.Kit != null) kits.Add(runner.Decor.Kit);
                        foreach (var look in runner.Looks)
                            if (look != null && look.Dressed && look.Decor?.Kit != null) kits.Add(look.Decor.Kit);
                    }

                int checkedStairs = 0;

                foreach (var kit in kits)
                {
                    if (!kit.CanBuildCastle || !kit.Stairs.Any || !kit.WallTops.Any) continue;

                    for (int seed = 1; seed <= 3; seed++)
                    {
                        var castle = BuildingBuilder.Castle(null, kit, new DeterministicRandom(seed));
                        if (castle == null) continue;
                        built.Add(castle);

                        float stairTop = float.MinValue, walk = float.MaxValue;

                        foreach (Transform piece in castle.transform)
                        {
                            var bounds = ModelScaling.Measure(piece.gameObject);
                            if (piece.name.Contains("Stairs")) stairTop = Mathf.Max(stairTop, bounds.max.y);
                            else if (IsWallTop(kit, piece.name)) walk = Mathf.Min(walk, bounds.min.y);
                        }

                        if (stairTop == float.MinValue || walk == float.MaxValue) continue;

                        checkedStairs++;
                        Assert.AreEqual(walk, stairTop, Meets,
                                        $"seed {seed}: the stair tops out at {stairTop:0.0} m and the wall walk is at {walk:0.0} m");
                    }
                }

                Assert.Greater(checkedStairs, 0, "no castle with a courtyard stair was built");
            }
            finally
            {
                foreach (var castle in built) Object.DestroyImmediate(castle);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static bool IsWallTop(BuildingKit kit, string name)
        {
            foreach (var model in kit.WallTops.Models)
                if (model != null && name.StartsWith(model.name)) return true;
            return false;
        }
    }
}
