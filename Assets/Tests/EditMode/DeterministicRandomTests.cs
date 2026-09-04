using System.Collections.Generic;
using TheVail.Sim;
using NUnit.Framework;

namespace TheVail.Tests
{
    /// <summary>
    /// Determinism is load-bearing: a level is a recipe plus a seed, so if this
    /// sequence ever shifts, every generated level in the game changes with it.
    /// These tests are the tripwire.
    /// </summary>
    public class DeterministicRandomTests
    {
        [Test]
        public void SameSeedProducesIdenticalSequence()
        {
            var a = new DeterministicRandom(7003);
            var b = new DeterministicRandom(7003);

            for (int i = 0; i < 10000; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"diverged at draw {i}");
        }

        [Test]
        public void AdjacentSeedsProduceUnrelatedSequences()
        {
            // Levels 7-3 and 7-4 sit next to each other. If their streams started
            // out similar, consecutive levels in a chapter would look alike.
            var a = new DeterministicRandom(DeterministicRandom.SeedFor(7, 3));
            var b = new DeterministicRandom(DeterministicRandom.SeedFor(7, 4));

            int matches = 0;
            for (int i = 0; i < 64; i++)
                if (a.NextUInt() == b.NextUInt()) matches++;

            Assert.LessOrEqual(matches, 1, "adjacent seeds produced near-identical streams");
        }

        [Test]
        public void SeedForEncodesChapterAndLevel()
        {
            Assert.AreEqual(1001, DeterministicRandom.SeedFor(1, 1));
            Assert.AreEqual(7003, DeterministicRandom.SeedFor(7, 3));
            Assert.AreEqual(100010, DeterministicRandom.SeedFor(100, 10));
        }

        [Test]
        public void IntRangeStaysWithinBounds()
        {
            var r = new DeterministicRandom(1);
            for (int i = 0; i < 100000; i++)
            {
                int v = r.Range(-5, 12);
                Assert.GreaterOrEqual(v, -5);
                Assert.Less(v, 12);
            }
        }

        [Test]
        public void EmptyOrInvertedRangeReturnsMin()
        {
            var r = new DeterministicRandom(1);
            Assert.AreEqual(4, r.Range(4, 4));
            Assert.AreEqual(4, r.Range(4, 1));
        }

        [Test]
        public void SingleValueRangeTerminates()
        {
            // span == 1 makes the rejection threshold zero; a naive implementation
            // can loop forever here.
            var r = new DeterministicRandom(1);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(3, r.Range(3, 4));
        }

        [Test]
        public void IntRangeIsUnbiased()
        {
            const int buckets = 10;
            const int draws = 200000;
            var counts = new int[buckets];
            var r = new DeterministicRandom(42);

            for (int i = 0; i < draws; i++) counts[r.Range(0, buckets)]++;

            const int expected = draws / buckets;
            foreach (int c in counts)
                Assert.That(c, Is.EqualTo(expected).Within(expected * 0.05),
                    "distribution is skewed — modulo bias has crept back in");
        }

        [Test]
        public void Value01StaysInUnitInterval()
        {
            var r = new DeterministicRandom(9);
            for (int i = 0; i < 100000; i++)
            {
                float v = r.Value01();
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
            }
        }

        [Test]
        public void ShuffleIsDeterministicAndPreservesContents()
        {
            var a = new List<int>();
            var b = new List<int>();
            for (int i = 0; i < 50; i++) { a.Add(i); b.Add(i); }

            new DeterministicRandom(123).Shuffle(a);
            new DeterministicRandom(123).Shuffle(b);

            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEquivalent(new List<int>(b), a);
            Assert.AreNotEqual(0, a[0] + a[1] + a[2] - 3, "list appears unshuffled");
        }

        [Test]
        public void HundredRunsOfTheSameSeedHashIdentically()
        {
            // The batch check called for in technical-design.md §3.
            uint Reference()
            {
                var r = new DeterministicRandom(DeterministicRandom.SeedFor(12, 7));
                uint hash = 2166136261u;
                for (int i = 0; i < 5000; i++) hash = (hash ^ r.NextUInt()) * 16777619u;
                return hash;
            }

            uint first = Reference();
            for (int run = 1; run < 100; run++)
                Assert.AreEqual(first, Reference(), $"run {run} diverged");
        }
    }
}
