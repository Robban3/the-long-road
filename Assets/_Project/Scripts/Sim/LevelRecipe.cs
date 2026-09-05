namespace TheVeil.Sim
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
    /// TheVeil.Data holds the designer-facing ScriptableObject; it converts to this at
    /// load time so the generator never touches the asset database. A level is this
    /// object plus a seed — nothing about it is stored on disk.
    /// </summary>
    public sealed class LevelRecipe
    {
        public int Width = 64;
        public int Height = 64;

        /// <summary>
        /// How many posts of the line are open. The scouting post is always open and is
        /// not one of these. See <see cref="Squad.Posts"/>.
        /// </summary>
        public int Posts = TroopTable.LinePosts;

        /// <summary>
        /// Natural terrain distribution. Shares are normalised, so they need not sum
        /// to 1. Roads and fords are carved afterwards as linear features rather than
        /// scattered by noise, so recipes normally leave them out of the mix.
        /// </summary>
        /// <summary>
        /// How many survivable ways through this level owes the player.
        ///
        /// The chapter shape from docs/GDD.md §8.1: two everywhere, one through the
        /// escalation band, where a worn squad and a level that expects them to have
        /// learned the terrain make a single hard way through the point. It lived only
        /// in a test until the generator was given the job of keeping it.
        /// </summary>
        public int RoutesOwed = 2;

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

        // No roads yet, and their absence is load-bearing rather than an oversight.
        // Road is the fastest terrain in the game (docs/GDD.md §3.1) and settlements
        // are placed on and beside roads, so without them the speed-against-safety
        // trade-off is missing a pole and no house or field has ever appeared on a map.
        //
        // Laying them is easy — the pathfinder's cheapest line across the country is
        // what a trade road is — but it cannot land on its own. The enemy budget is
        // shared out in inverse proportion to a corridor's travel cost, so a faster
        // corridor is given more enemies; a road makes the fast corridor both quicker
        // and shorter, and that same budget lands denser. Level 1-6 went from winnable
        // to unsurvivable on all three routes. Roads need the budget formula revisited
        // first, which is a balance change and not a scenery one.

        /// <summary>Minimum tiles along the fastest route from start to goal.</summary>
        public int MinRouteTiles = 40;

        /// <summary>
        /// Seeds that fail corridor validation are re-rolled this many times before
        /// the best candidate so far is accepted (docs/content-pipeline.md §3 step 4).
        /// </summary>
        public int MaxGenerationAttempts = 12;

        /// <summary>
        /// Threat points spread across the corridors.
        ///
        /// Measured behaviour on a 64x64 map: usable from about 80, ideal between 100
        /// and 120. Above roughly 140 the fast corridor saturates — it is short, and
        /// group spacing caps how many encounters fit — so the surplus lands on the
        /// longer routes and the slow way round ends up the richer one, exactly
        /// backwards. At 200 the fast route is the better payday on only 37 % of
        /// levels.
        ///
        /// The consequence for progression: later chapters cannot get harder by
        /// adding enemies. Past the ceiling, difficulty has to come from tougher
        /// enemy types and tighter encounter spacing instead.
        /// </summary>
        // 120, up from 100, and the twenty is the price of the roads parting.
        //
        // The placer owes five groups on every route a player might draw. It used to meet
        // that while the fast and safe corridors ran over the same tiles on half the
        // chapter — the same enemies answered for both. Now that CorridorFinder charges
        // the cautious search for the fast route's ground, the budget is spread over
        // three genuinely separate lines, and at 100 the promise broke on 2-10.
        //
        // Measured across chapters 1 to 3: 100 keeps it on 29 of 30 levels, 120 on all
        // thirty, and so do 140, 160 and 190. Twenty is what it costs; the rest would be
        // a difficulty decision wearing a bug fix's clothes.
        public int EnemyBudget = 120;

        public int SquadBudget = 12;
        public float TrapDensity = 1f;
        public float SilverMultiplier = 1f;

        /// <summary>
        /// Multiplier on enemy health and damage. Threat points count enemies; this
        /// scales how dangerous each one is, which is how difficulty keeps rising
        /// after the map runs out of room for more groups.
        /// </summary>
        public float EnemyStrength = 1f;

        /// <summary>
        /// Which enemy types may appear. Restricting early levels to wolves and
        /// introducing archers later is a difficulty lever in its own right — the
        /// archer is not a stronger wolf, it is a problem melee cannot solve.
        /// </summary>
        public EnemyKind[] EnemyPool = EnemyTable.All;

        /// <summary>
        /// Silver a route must be able to yield — two upgrade levels for one troop
        /// (20+32). Below that the player reaches the level's last fight with an army
        /// they had no way to improve at all, which is broken rather than hard.
        ///
        /// Deliberately low. An earlier value of 105 was set to "three upgrades" and
        /// turned out to bind almost everywhere, topping every route up to exactly the
        /// same figure — which erased the reward for taking the dangerous line, the very
        /// thing the silver economy exists to create. The floor is a safety net for the
        /// broken case, not a guarantee of a comfortable income.
        ///
        /// Measured per sampled route since the player started drawing their own; it
        /// used to be per corridor, back when there were only three of them.
        /// </summary>
        public int MinSilverPerRoute = 55;
    }
}
