using System.Collections.Generic;

namespace TheVail.Sim
{
    /// <summary>
    /// Deterministic xorshift128 PRNG.
    ///
    /// Every random value in generation and simulation comes from an instance of
    /// this class. UnityEngine.Random is banned in TheVail.Sim and TheVail.Gen: it is
    /// global mutable state, anyone can perturb it from anywhere, and its sequence
    /// is not guaranteed stable across Unity versions. Since a level is stored as
    /// nothing but a recipe plus a seed, a shifted sequence does not just change a
    /// few numbers — it silently replaces all 1000 levels with different ones.
    /// </summary>
    public sealed class DeterministicRandom
    {
        uint _x, _y, _z, _w;

        public DeterministicRandom(int seed)
        {
            // SplitMix64 avalanche on the way in, so that adjacent level seeds
            // (7003 and 7004) yield unrelated streams rather than similar ones.
            // Seeding the state directly from the seed makes neighbouring levels
            // look alike for their first few draws.
            ulong s = (uint)seed;
            _x = NextSplit(ref s);
            _y = NextSplit(ref s);
            _z = NextSplit(ref s);
            _w = NextSplit(ref s);

            // All-zero is a fixed point of xorshift — it would emit zeros forever.
            if ((_x | _y | _z | _w) == 0u) _w = 0x6D2B79F5u;
        }

        static uint NextSplit(ref ulong s)
        {
            s += 0x9E3779B97F4A7C15UL;
            ulong z = s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (uint)((z ^ (z >> 31)) >> 32);
        }

        /// <summary>Uniform over the full range of uint.</summary>
        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            return _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive). Returns min for an empty range.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)((long)maxExclusive - minInclusive);

            // Rejection sampling. Plain modulo biases the low end whenever span
            // does not divide 2^32, and across a million tile placements that bias
            // shows up as maps that systematically favour certain terrain.
            // Discarding the first (2^32 mod span) values leaves an exact multiple.
            uint threshold = (uint)(0x100000000UL % span);
            uint r;
            do { r = NextUInt(); } while (r < threshold);
            return minInclusive + (int)(r % span);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value01() => (NextUInt() >> 8) * (1.0f / 16777216.0f);

        /// <summary>Uniform float in [minInclusive, maxExclusive).</summary>
        public float Range(float minInclusive, float maxExclusive)
            => minInclusive + Value01() * (maxExclusive - minInclusive);

        /// <summary>True with the given probability. Values outside [0,1] clamp naturally.</summary>
        public bool Chance(float probability) => Value01() < probability;

        /// <summary>Picks one element uniformly. Throws on an empty list.</summary>
        public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];

        /// <summary>Fisher-Yates shuffle, in place.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        /// <summary>
        /// Seed for a level, e.g. chapter 7 level 3 becomes 7003. Levels are
        /// identified by this number everywhere — save data, analytics, bug reports.
        /// </summary>
        public static int SeedFor(int chapter, int level) => chapter * 1000 + level;
    }
}
