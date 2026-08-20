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

        /// <summary>
        /// True when the pack was exported with Z as the up axis, as Blender does by
        /// default. The scenery pack is; the character and animal packs are not, even
        /// though they come from the same author.
        ///
        /// Measured rather than assumed: Resource_Tree1 arrives 0.72 x 0.45 x 0.93,
        /// so its tallest dimension is Z. Left uncorrected the trees lie on their
        /// sides, and — worse, because it looks like a different bug — the height
        /// normalisation measures their width and scales them wrongly too.
        /// </summary>
        public bool ZUp = true;

        public bool IsEmpty =>
            (Trees == null || Trees.Length == 0) &&
            (Pines == null || Pines.Length == 0) &&
            (Rocks == null || Rocks.Length == 0) &&
            (Mountains == null || Mountains.Length == 0);
    }

    /// <summary>
    /// Scatters props across the terrain so the world reads as a landscape.
    ///
    /// Placement is driven by the level seed, so a level is dressed identically every
    /// time — a map that rearranged its own forest between attempts would undermine
    /// the one promise the whole generator rests on.
    ///
    /// Every prop is rescaled to a stated height in metres on the way in. Left at
    /// their authored size the pines came out roughly a metre tall on a 256-metre
    /// map: placed correctly, and completely invisible.
    /// </summary>
    public static class TerrainDecorator
    {
        /// <summary>Prop heights in metres. A tile is four metres across for reference.</summary>
        public const float TreeHeight = 7f;
        public const float PineHeight = 8.5f;
        public const float RockHeight = 2.2f;
        public const float MountainHeight = 20f;

        /// <summary>
        /// Props per tile. Tuned down from a first attempt at 0.55 in forest, which put
        /// a thousand nine-metre trees on a 256-metre map and closed the canopy over
        /// the caravan entirely — the world has to be looked through, not just at.
        /// </summary>
        static readonly Dictionary<TerrainType, float> Density = new Dictionary<TerrainType, float>
        {
            { TerrainType.Forest, 0.28f },
            { TerrainType.MountainPass, 0.18f },
            { TerrainType.Plains, 0.03f },
            { TerrainType.Marsh, 0.06f },
            { TerrainType.Road, 0.01f }
        };

        /// <param name="keepClear">
        /// Tiles left bare. The planning map passes the route here so the drawn line
        /// stays readable through the trees; the play view passes nothing, because
        /// there is no line to bury and a forest should look like one.
        /// </param>
        public static int Decorate(Transform parent, TileGrid grid, int seed, BiomeDecor decor,
                                   IReadOnlyCollection<int> keepClear = null,
                                   float heightScale = 0f, int maxProps = 600,
                                   float densityScale = 1f)
        {
            if (decor == null || decor.IsEmpty) return 0;

            var rng = new DeterministicRandom(seed ^ 0x5EED10);
            var clear = keepClear == null ? null : new HashSet<int>(keepClear);
            int placed = 0;

            for (int i = 0; i < grid.TileCount && placed < maxProps; i++)
            {
                var terrain = grid[i];
                if (!Density.TryGetValue(terrain, out float density)) continue;
                if (clear != null && clear.Contains(i)) continue;
                if (!rng.Chance(density * densityScale)) continue;

                var prefab = Pick(decor, terrain, rng, out float targetHeight);
                if (prefab == null) continue;

                var position = Vec2.FromTile(grid, i);
                float x = position.X + rng.Range(-1.4f, 1.4f);
                float z = position.Y + rng.Range(-1.4f, 1.4f);

                // Sampled the way the mesh is built, at the prop's own position. Using
                // the tile's own elevation instead leaves trees hovering above the
                // ground or buried in it, because the rendered surface is interpolated
                // between corners and a tile centre is a different number entirely.
                float groundY = grid.SurfaceElevation(x, z) * heightScale;

                var instance = Object.Instantiate(prefab, parent);
                instance.transform.position = new Vector3(x, groundY, z);

                // Stand it up before measuring. Fitting to height only means anything
                // once the model's height is actually along Y.
                instance.transform.rotation = decor.ZUp
                    ? Quaternion.Euler(-90f, rng.Range(0f, 360f), 0f)
                    : Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

                ModelScaling.Fit(instance, targetHeight * rng.Range(0.8f, 1.25f), groundY);
                placed++;
            }

            return placed;
        }

        static GameObject Pick(BiomeDecor decor, TerrainType terrain, DeterministicRandom rng,
                               out float targetHeight)
        {
            switch (terrain)
            {
                case TerrainType.Forest:
                    // Pines dominate, broadleaf mixed in so the canopy is not uniform.
                    if (Has(decor.Pines) && (rng.Chance(0.7f) || !Has(decor.Trees)))
                    {
                        targetHeight = PineHeight;
                        return Any(decor.Pines, rng);
                    }
                    targetHeight = TreeHeight;
                    return Has(decor.Trees) ? Any(decor.Trees, rng) : null;

                case TerrainType.MountainPass:
                    if (Has(decor.Mountains) && rng.Chance(0.30f))
                    {
                        targetHeight = MountainHeight;
                        return Any(decor.Mountains, rng);
                    }
                    targetHeight = RockHeight;
                    return Has(decor.Rocks) ? Any(decor.Rocks, rng) : null;

                case TerrainType.Marsh:
                case TerrainType.Plains:
                case TerrainType.Road:
                    if (Has(decor.Rocks) && rng.Chance(0.6f))
                    {
                        targetHeight = RockHeight;
                        return Any(decor.Rocks, rng);
                    }
                    targetHeight = TreeHeight;
                    return Has(decor.Trees) ? Any(decor.Trees, rng) : null;

                default:
                    targetHeight = TreeHeight;
                    return null;
            }
        }

        static bool Has(GameObject[] set) => set != null && set.Length > 0;

        static GameObject Any(GameObject[] set, DeterministicRandom rng) => set[rng.Range(0, set.Length)];
    }
}
