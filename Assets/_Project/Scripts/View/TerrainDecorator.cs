using System.Collections.Generic;
using Arna.Sim;
using UnityEngine;

namespace Arna.View
{
    /// <summary>Models used to dress one biome's terrain.</summary>
    [System.Serializable]
    public sealed class BiomeDecor
    {
        public GameObject[] Trees;
        public GameObject[] Pines;
        public GameObject[] Rocks;
        public GameObject[] Mountains;

        public bool IsEmpty =>
            (Trees == null || Trees.Length == 0) &&
            (Pines == null || Pines.Length == 0) &&
            (Rocks == null || Rocks.Length == 0) &&
            (Mountains == null || Mountains.Length == 0);
    }

    /// <summary>
    /// Scatters props across the terrain so the map reads as a landscape rather than a
    /// coloured grid.
    ///
    /// Placement is driven by the level seed, so the same level is dressed identically
    /// every time — a map that rearranges its own forest between attempts would
    /// undermine the one promise the whole generator rests on.
    ///
    /// Only a fraction of eligible tiles get a prop. A tree on every forest tile would
    /// be eighteen hundred objects on a 64x64 map and would bury the route the player
    /// is trying to read.
    /// </summary>
    public static class TerrainDecorator
    {
        /// <summary>Hard ceiling on props, so a dense biome cannot bury the frame rate.</summary>
        public const int MaxProps = 600;

        static readonly Dictionary<TerrainType, float> Density = new Dictionary<TerrainType, float>
        {
            { TerrainType.Forest, 0.30f },
            { TerrainType.MountainPass, 0.28f },
            { TerrainType.Plains, 0.04f },
            { TerrainType.Marsh, 0.06f },
            { TerrainType.Road, 0.01f }
        };

        public static int Decorate(Transform parent, TileGrid grid, int seed, BiomeDecor decor,
                                   IReadOnlyCollection<int> keepClear = null)
        {
            if (decor == null || decor.IsEmpty) return 0;

            var rng = new DeterministicRandom(seed ^ 0x5EED10);
            var clear = keepClear == null ? null : new HashSet<int>(keepClear);
            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < MaxProps; i++)
            {
                var terrain = grid[i];
                if (!Density.TryGetValue(terrain, out float density)) continue;
                if (clear != null && clear.Contains(i)) continue;
                if (!rng.Chance(density)) continue;

                var prefab = Pick(decor, terrain, rng);
                if (prefab == null) continue;

                var position = Vec2.FromTile(grid, i);

                // Jitter inside the tile and spin freely, or the scatter reads as a grid,
                // which is exactly what the props are meant to hide.
                float jitterX = rng.Range(-1.4f, 1.4f);
                float jitterY = rng.Range(-1.4f, 1.4f);

                var instance = Object.Instantiate(prefab, parent);
                instance.transform.position = new Vector3(position.X + jitterX, 0f, position.Y + jitterY);
                instance.transform.rotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

                float scale = ScaleFor(terrain) * rng.Range(0.85f, 1.2f);
                instance.transform.localScale = Vector3.one * scale;

                placed++;
            }

            return placed;
        }

        static GameObject Pick(BiomeDecor decor, TerrainType terrain, DeterministicRandom rng)
        {
            switch (terrain)
            {
                case TerrainType.Forest:
                    // Pines dominate, with broadleaf mixed in so the canopy is not uniform.
                    if (Has(decor.Pines) && (rng.Chance(0.7f) || !Has(decor.Trees))) return Any(decor.Pines, rng);
                    return Has(decor.Trees) ? Any(decor.Trees, rng) : null;

                case TerrainType.MountainPass:
                    if (Has(decor.Mountains) && rng.Chance(0.35f)) return Any(decor.Mountains, rng);
                    return Has(decor.Rocks) ? Any(decor.Rocks, rng) : null;

                case TerrainType.Marsh:
                case TerrainType.Plains:
                case TerrainType.Road:
                    if (Has(decor.Rocks) && rng.Chance(0.6f)) return Any(decor.Rocks, rng);
                    return Has(decor.Trees) ? Any(decor.Trees, rng) : null;

                default:
                    return null;
            }
        }

        static float ScaleFor(TerrainType terrain)
            => terrain == TerrainType.MountainPass ? 1.6f : 1.1f;

        static bool Has(GameObject[] set) => set != null && set.Length > 0;

        static GameObject Any(GameObject[] set, DeterministicRandom rng) => set[rng.Range(0, set.Length)];
    }
}
