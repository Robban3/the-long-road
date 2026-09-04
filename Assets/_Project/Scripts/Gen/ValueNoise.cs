namespace TheVail.Gen
{
    /// <summary>
    /// Deterministic 2D value noise with fBm layering.
    ///
    /// Written by hand rather than taken from Mathematics or Perlin helpers because
    /// the generator must produce byte-identical output on every device and every
    /// Unity version — a level is only a recipe plus a seed, so a noise function
    /// that drifts silently replaces every map in the game.
    /// </summary>
    public static class ValueNoise
    {
        /// <summary>Deterministic hash of a lattice point to [0,1).</summary>
        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h >> 8) * (1f / 16777216f);
            }
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static int Floor(float v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        /// <summary>Single octave, bilinearly interpolated. Returns [0,1).</summary>
        public static float Sample(float x, float y, int seed)
        {
            int x0 = Floor(x);
            int y0 = Floor(y);
            float sx = Smooth(x - x0);
            float sy = Smooth(y - y0);

            float n00 = Hash01(x0, y0, seed);
            float n10 = Hash01(x0 + 1, y0, seed);
            float n01 = Hash01(x0, y0 + 1, seed);
            float n11 = Hash01(x0 + 1, y0 + 1, seed);

            float a = n00 + (n10 - n00) * sx;
            float b = n01 + (n11 - n01) * sx;
            return a + (b - a) * sy;
        }

        /// <summary>
        /// Fractal Brownian motion: several octaves at doubling frequency and halving
        /// amplitude. Normalised back to [0,1). Broad regions come from the first
        /// octave, coastline detail from the last.
        /// </summary>
        public static float Fbm(float x, float y, int seed, int octaves,
                                float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            float fx = x, fy = y;

            for (int i = 0; i < octaves; i++)
            {
                sum += Sample(fx, fy, seed + i * 7919) * amplitude;
                total += amplitude;
                amplitude *= gain;
                fx *= lacunarity;
                fy *= lacunarity;
            }

            return total > 0f ? sum / total : 0f;
        }
    }
}
