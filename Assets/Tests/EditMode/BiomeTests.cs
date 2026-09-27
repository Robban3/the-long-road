using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// Which country each chapter is set in. The planning map, the run and the ground
    /// colours all ask <see cref="Biomes.Of"/>, and the day it answers differently for any
    /// of them is the day somebody plans a level in snow and drives it through a forest.
    /// </summary>
    public class BiomeTests
    {
        [Test]
        public void TheRoadOpensInTheForestAndTurnsToWinterInTheSecond()
        {
            Assert.AreEqual(Biome.Forest, Biomes.Of(1));
            Assert.AreEqual(Biome.Winter, Biomes.Of(Biomes.WinterChapter));
            Assert.AreEqual(2, Biomes.WinterChapter, "winter moved off chapter 2 without anyone saying so");
            Assert.AreEqual(Biome.Winter, Biomes.Order[Biomes.WinterChapter - 1],
                            "the winter constant and the tour disagree");
        }

        [Test]
        public void TheThirdChapterIsTheMarsh()
        {
            // Which is what the roadmap has been calling it — "THE WETLANDS" — while
            // playing it as forest.
            Assert.AreEqual(Biome.Marsh, Biomes.Of(3));
            Assert.AreEqual(3, Biomes.FirstChapterOf(Biome.Marsh));
        }

        [Test]
        public void TheTourVisitsEveryInlandCountryAndThenBeginsAgain()
        {
            var seen = new System.Collections.Generic.HashSet<Biome>();
            foreach (var biome in Biomes.Order)
                Assert.IsTrue(seen.Add(biome), $"{biome} appears twice in the tour");

            // The sea is not on the tour, and nothing else is missing from it. The road
            // ends at the coast (Biomes.LastChapter), so a player who met it in chapter
            // seven would be sailing away from somewhere they had already been.
            Assert.IsFalse(seen.Contains(Biome.Coast), "the sea is on the tour");
            Assert.AreEqual(Biomes.All.Length - 1, Biomes.Order.Length,
                            "a country other than the sea is missing from the tour");

            // Every biome the game knows about is somewhere in the campaign.
            foreach (Biome biome in System.Enum.GetValues(typeof(Biome)))
                Assert.Greater(Biomes.FirstChapterOf(biome), 0, $"{biome} is in no chapter at all");

            // And the last ten levels are the sea, whatever the tour would have said.
            Assert.AreEqual(Biome.Coast, Biomes.Of(Biomes.LastChapter));
            Assert.AreEqual(Biomes.LastChapter, Biomes.FirstChapterOf(Biome.Coast));

            for (int chapter = 1; chapter < Biomes.LastChapter; chapter++)
                Assert.AreNotEqual(Biome.Coast, Biomes.Of(chapter),
                                   $"the sea turns up in chapter {chapter} as well");

            // And the second pass starts one country later, so the tour is not the same
            // ten chapters over again.
            int tour = Biomes.Order.Length;
            Assert.AreEqual(Biomes.Order[1], Biomes.Of(tour + 1),
                            "the second pass opens where the first did");
            Assert.AreEqual(Biomes.Order[2], Biomes.Of(tour * 2 + 1),
                            "the third pass opens where the second did");
        }

        [Test]
        public void EveryPassVisitsAllTenCountries()
        {
            for (int pass = 0; pass < 10; pass++)
            {
                var seen = new System.Collections.Generic.HashSet<Biome>();

                for (int i = 1; i <= Biomes.Order.Length; i++)
                {
                    int chapter = pass * Biomes.Order.Length + i;
                    Assert.AreEqual(pass, Biomes.PassOf(chapter), $"chapter {chapter} is in the wrong pass");
                    seen.Add(Biomes.Of(chapter));
                }

                Assert.AreEqual(Biomes.Order.Length, seen.Count, $"pass {pass} misses a country");
            }
        }

        [Test]
        public void EachPassDressesTheCountryDifferently()
        {
            // The first hundred levels are the country as it is; after that it comes back
            // under snow, then burnt, then flooded, and then as it was again.
            // Counted in passes round the tour rather than in tens of chapters, because
            // the tour is nine countries long now that the sea is held back for the end.
            int tour = Biomes.Order.Length;

            Assert.AreEqual(Dressing.Plain, Biomes.DressingOf(1));
            Assert.AreEqual(Dressing.Plain, Biomes.DressingOf(tour));
            Assert.AreEqual(Dressing.Snow, Biomes.DressingOf(tour + 1));
            Assert.AreEqual(Dressing.Burnt, Biomes.DressingOf(tour * 2 + 1));
            Assert.AreEqual(Dressing.Flood, Biomes.DressingOf(tour * 3 + 1));
            Assert.AreEqual(Dressing.Plain, Biomes.DressingOf(tour * 4 + 1));

            // A chapter with no campaign behind it is the opening country, plainly dressed.
            Assert.AreEqual(Dressing.Plain, Biomes.DressingOf(0));
            Assert.AreEqual(Biome.Forest, Biomes.Of(0));
        }

        [Test]
        public void EveryChapterHasACountry()
        {
            for (int chapter = 0; chapter <= 100; chapter++)
                Assert.Greater(Biomes.FirstChapterOf(Biomes.Of(chapter)), 0, $"chapter {chapter}");
        }

        [Test]
        public void TwoChaptersRunningAreNeverTheSameCountry()
        {
            // The reason for the order: close country, then open. No stretch of the road
            // should read as one chapter played twice.
            for (int chapter = 1; chapter < 100; chapter++)
                Assert.AreNotEqual(Biomes.Of(chapter), Biomes.Of(chapter + 1),
                    $"chapters {chapter} and {chapter + 1} are both {Biomes.Of(chapter)}");
        }
    }
}
