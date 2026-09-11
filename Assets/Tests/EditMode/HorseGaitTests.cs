using TheVeil.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Tests
{
    /// <summary>
    /// The four horses of the army pack, stepping. They have a skeleton and no clips, so
    /// the legs are moved by <see cref="HorseGait"/>; these hold it to every horse the
    /// cavalry rides and to nothing else.
    /// </summary>
    public class HorseGaitTests
    {
        const string Dir = "Assets/Stylized_Medieval_Army_Pack/Prefabs - Characters/";

        static readonly string[] Cavalry =
        {
            "MC_Cavalry_LightCavalry", "MC_Cavalry_HeavyCavalry", "MC_Cavalry_NobleCavalry", "MC_Cavalry"
        };

        static GameObject Spawn(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + name + ".prefab");
            Assert.IsNotNull(prefab, $"{name} is not in the army pack");
            return Object.Instantiate(prefab);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        [Test]
        public void EveryHorseTheCavalryRidesTakesTheGait()
        {
            foreach (var name in Cavalry)
            {
                var figure = Spawn(name);
                try
                {
                    Assert.IsNotNull(HorseGait.Fit(figure.transform), $"{name}'s horse has no legs to move");
                }
                finally
                {
                    Object.DestroyImmediate(figure);
                }
            }
        }

        [Test]
        public void AManOnFootTakesNone()
        {
            var figure = Spawn("MC_Levy_03");
            try
            {
                Assert.IsNull(HorseGait.Fit(figure.transform));
                Assert.IsNull(figure.GetComponent<HorseGait>(), "a gait was left on a man with no horse under him");
            }
            finally
            {
                Object.DestroyImmediate(figure);
            }
        }

        [Test]
        public void TheHoovesMoveThroughTheStrideAndStandStillAtRest()
        {
            foreach (var name in Cavalry)
            {
                var figure = Spawn(name);
                try
                {
                    var gait = HorseGait.Fit(figure.transform);
                    var hoof = Find(figure.transform, "HandLeft") ?? Find(figure.transform, "ForearmLeft");
                    Assert.IsNotNull(hoof);

                    gait.Pose(0f, 0f, 0f);
                    var rest = hoof.position;

                    gait.Pose(0.25f, 0f, 1f);
                    var early = hoof.position;

                    gait.Pose(0.75f, 0f, 1f);
                    var late = hoof.position;

                    Assert.Greater(Vector3.Distance(early, late), 0.05f, $"{name}'s foreleg did not move through the stride");

                    // Back to square, exactly: set from rest every time, never added on.
                    gait.Pose(0f, 0f, 0f);
                    Assert.Less(Vector3.Distance(rest, hoof.position), 0.0001f, $"{name} did not come back to standing");
                }
                finally
                {
                    Object.DestroyImmediate(figure);
                }
            }
        }

        [Test]
        public void AFasterPaceIsAGallopAndAWalkIsNot()
        {
            Assert.AreEqual(0f, HorseGait.GallopShare(1.5f));
            Assert.AreEqual(1f, HorseGait.GallopShare(8f));

            float previous = 0f;
            for (float speed = 0f; speed <= 8f; speed += 0.25f)
            {
                float share = HorseGait.GallopShare(speed);
                Assert.GreaterOrEqual(share, previous, $"slower into a gallop at {speed} m/s");
                previous = share;
            }
        }
    }
}
