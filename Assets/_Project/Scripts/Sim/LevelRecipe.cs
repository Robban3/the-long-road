namespace Arna.Sim
{
    /// <summary>One terrain type's share of a level's natural terrain.</summary>
    public struct TerrainShare
    {
        public TerrainType Type;
        public float Share;

        public TerrainShare(TerrainType type, float share)
        {
            Type = type;
            Share = share;
        }
    }

    /// <summary>
    /// The plain-data form of a chapter recipe (docs/content-pipeline.md §2).
    ///
    /// Arna.Data holds the designer-facing ScriptableObject; it converts to this at
    /// load time so the generator never touches the asset database. A level is this
    /// object plus a seed — nothing about it is stored on disk.
    /// </summary>
    public sealed class LevelRecipe
    {
        public int Width = 64;
        public int Height = 64;

        /// <summary>
        /// Natural terrain distribution. Shares are normalised, so they need not sum
        /// to 1. Roads and fords are carved afterwards as linear features rather than
        /// scattered by noise, so recipes normally leave them out of the mix.
        /// </summary>
        public TerrainShare[] TerrainMix =
        {
            new TerrainShare(TerrainType.Forest, 0.45f),
            new TerrainShare(TerrainType.Plains, 0.30f),
            new TerrainShare(TerrainType.Marsh, 0.10f),
            new TerrainShare(TerrainType.MountainPass, 0.08f),
            new TerrainShare(TerrainType.Water, 0.07f)
        };

        /// <summary>
        /// Terrain feature size in tiles. Small values fragment the map into
        /// single-tile speckle the player cannot read as landscape; the whole point
        /// of showing terrain is that "there is a forest, there is a marsh" can be
        /// planned around.
        /// </summary>
        public float NoiseScale = 18f;

        /// <summary>
        /// Kept low deliberately. Extra octaves add detail finer than one tile, which
        /// after quantisation reads as noise rather than terrain.
        /// </summary>
        public int NoiseOctaves = 2;

        /// <summary>Box-blur passes over the height field before terrain is assigned.</summary>
        public int SmoothingPasses = 2;

        /// <summary>
        /// Rivers cut north to south, across the caravan's west-to-east travel, and
        /// are crossable only at their fords. They are the main source of real
        /// chokepoints — scattered lakes are avoided without a thought, a river with
        /// three crossings forces a decision.
        /// </summary>
        public int Rivers = 1;
        public int FordsPerRiver = 3;

        /// <summary>Minimum tiles along the fastest route from start to goal.</summary>
        public int MinRouteTiles = 40;

        /// <summary>
        /// Seeds that fail corridor validation are re-rolled this many times before
        /// the best candidate so far is accepted (docs/content-pipeline.md §3 step 4).
        /// </summary>
        public int MaxGenerationAttempts = 12;

        public int EnemyBudget = 40;
        public int SquadBudget = 12;
        public float TrapDensity = 1f;
        public float SilverMultiplier = 1f;
    }
}
