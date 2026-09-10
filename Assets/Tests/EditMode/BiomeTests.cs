using NUnit.Framework;
using TheVeil.Sim;

namespace TheVeil.Tests
{
    /// <summary>
    /// Which country each chapter is set in.
    ///
    /// Small, and worth having for that reason: the planning map, the run and the setup
    /// all ask <see cref="Biomes.Of"/>, and the day it answers differently for any of them
    /// is the day a route is planned in one country and driven in another.
    /// </summary>
    public class BiomeTests
    {
        [Test]
        public void TheFirstChapterIsTheForest()
        {
            Assert.AreEqual(Biome.Forest, Biomes.Of(1));
        }

        [Test]
        public void TheSecondChapterIsWinter()
        {
            Assert.AreEqual(Biome.Winter, Biomes.Of(Biomes.WinterChapter));
            Assert.AreEqual(2, Biomes.WinterChapter, "winter moved off chapter 2 without anyone saying so");
        }

        [Test]
        public void EveryOtherChapterStaysForestForNow()
        {
            // Until the hundred chapters are shared out, winter is one chapter and the
            // rest are the forest. This goes red on purpose when that changes, so the
            // change is made here rather than discovered.
            for (int chapter = 1; chapter <= 10; chapter++)
            {
                if (chapter == Biomes.WinterChapter) continue;
                Assert.AreEqual(Biome.Forest, Biomes.Of(chapter), $"chapter {chapter}");
            }
        }
    }
}
