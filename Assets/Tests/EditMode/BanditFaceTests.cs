using TheVeil.View;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// How a band of raiders is dressed: mostly peasants, now and then a deserter, and the
    /// same faces every time the level is played.
    /// </summary>
    public class BanditFaceTests
    {
        [Test]
        public void AboutOneRaiderInFourIsADeserter()
        {
            int deserters = 0, figures = 0;

            for (int group = 0; group < 400; group++)
                for (int figure = 0; figure < 6; figure++)
                {
                    figures++;
                    if (VisualLibrary.Mix(group, figure) % 4 == 0) deserters++;
                }

            float share = deserters / (float)figures;
            Assert.That(share, Is.InRange(0.18f, 0.32f), $"deserters were {share:P0} of the raiders");
        }

        [Test]
        public void ABandWearsTheSameFacesEveryTime()
        {
            for (int figure = 0; figure < 6; figure++)
                Assert.AreEqual(VisualLibrary.Mix(1234, figure), VisualLibrary.Mix(1234, figure));

            // And not one face for the whole band.
            bool differs = false;
            for (int figure = 1; figure < 6; figure++)
                differs |= VisualLibrary.Mix(1234, figure) % 3 != VisualLibrary.Mix(1234, 0) % 3;

            Assert.IsTrue(differs, "every raider in the band got the same face");
        }
    }
}
