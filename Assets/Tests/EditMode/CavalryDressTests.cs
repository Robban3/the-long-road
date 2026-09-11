using TheVeil.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Tests
{
    /// <summary>
    /// The two horses setup re-dresses out of the army pack's (TheVeilSetup.HeavyCavalryPrefab
    /// and KnightsPrefab): the heavy horse in plate without its cloth, and the knights'
    /// barded horse ridden by Knight 02.
    ///
    /// Asked of a spawned copy, as the game spawns them, and not of the asset: a prefab
    /// asset is in no scene, so nothing in it is active in a hierarchy and every renderer
    /// reads as hidden. The first version of these asked the asset, found nothing
    /// showing at all — and the one test that only looks at what shows passed by having
    /// nothing to look at.
    /// </summary>
    public class CavalryDressTests
    {
        const string Heavy = "Assets/_Project/Prefabs/Cavalry/TheVeil_HeavyCavalry.prefab";
        const string Knights = "Assets/_Project/Prefabs/Cavalry/TheVeil_Knights.prefab";
        const string KnightMeshesFrom = "Assets/Stylized_Medieval_Army_Pack/Prefabs - Characters/MC_Knight_02.prefab";

        static GameObject Spawn(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"{path} is missing — run The Veil > Refresh Scene Assets");
            return Object.Instantiate(prefab);
        }

        static bool Shows(Renderer renderer) => renderer.enabled && renderer.gameObject.activeInHierarchy;

        [Test]
        public void TheHeavyHorseWearsPlateAndNoCloth()
        {
            var heavy = Spawn(Heavy);
            try
            {
                int plate = 0;

                foreach (var renderer in heavy.GetComponentsInChildren<Renderer>(true))
                {
                    if (!Shows(renderer)) continue;

                    Assert.IsFalse(renderer.name.StartsWith("HorseTrapper"), $"the heavy horse still wears {renderer.name}");
                    if (renderer.name.StartsWith("HorseArmor")) plate++;
                }

                Assert.Greater(plate, 0, "the heavy horse lost its plate with its cloth");
            }
            finally
            {
                Object.DestroyImmediate(heavy);
            }
        }

        [Test]
        public void TheKnightsRideInKnightTwosArmour()
        {
            var knights = Spawn(Knights);
            var dresser = Spawn(KnightMeshesFrom);
            try
            {
                var worn = new System.Collections.Generic.HashSet<Mesh>();
                foreach (var skin in knights.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (Shows(skin)) worn.Add(skin.sharedMesh);

                int pieces = 0;
                foreach (var piece in dresser.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!Shows(piece)) continue;
                    pieces++;
                    Assert.IsTrue(worn.Contains(piece.sharedMesh), $"the knight is not wearing Knight 02's {piece.name}");
                }
                Assert.Greater(pieces, 0, "Knight 02 shows nothing to wear");

                // And the trapper stays on: the knights' horse is the barded one.
                bool trapper = false;
                foreach (var renderer in knights.GetComponentsInChildren<Renderer>(true))
                    if (Shows(renderer) && renderer.name.StartsWith("HorseTrapper")) trapper = true;
                Assert.IsTrue(trapper, "the knights' horse has lost its trapper");
            }
            finally
            {
                Object.DestroyImmediate(knights);
                Object.DestroyImmediate(dresser);
            }
        }

        [Test]
        public void TheKnightIsBoundToTheRiderAndNotToTheHorse()
        {
            var knights = Spawn(Knights);
            try
            {
                Transform rider = null;
                foreach (var t in knights.GetComponentsInChildren<Transform>(true))
                    if (t.name == "MC_Knight_01") rider = t;
                Assert.IsNotNull(rider, "no rider on the knights' horse");

                int checkedSkins = 0;
                foreach (var skin in rider.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!Shows(skin)) continue;
                    checkedSkins++;

                    foreach (var bone in skin.bones)
                    {
                        Assert.IsNotNull(bone, $"{skin.name} has a bone missing");
                        Assert.IsTrue(bone.IsChildOf(rider), $"{skin.name} is bound to the horse's {bone.name}");
                    }
                }

                // Something has to have been looked at, or this passes on an empty rider.
                Assert.Greater(checkedSkins, 0, "the knight shows no body to check");
            }
            finally
            {
                Object.DestroyImmediate(knights);
            }
        }

        [Test]
        public void BothRedressedHorsesStillStep()
        {
            foreach (var path in new[] { Heavy, Knights })
            {
                var figure = Spawn(path);
                try
                {
                    Assert.IsNotNull(HorseGait.Fit(figure.transform), $"{path}'s horse has no legs to move");
                }
                finally
                {
                    Object.DestroyImmediate(figure);
                }
            }
        }
    }
}
