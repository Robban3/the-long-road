namespace TheVail.Sim
{
    /// <summary>
    /// The balance table from docs/GDD.md §3.2, in code because the pathfinder and
    /// the generator both need it before any Unity object exists.
    ///
    /// These are the shipping defaults. TheVail.Data mirrors them as TerrainTypeDef
    /// assets so designers can tune without recompiling; at load time the assets
    /// overwrite these values through <see cref="Override"/>. Keeping a working
    /// default here means Sim and Gen stay runnable in a plain unit test with no
    /// asset database around.
    /// </summary>
    public static class TerrainTable
    {
        const int Count = 8;

        // Indexed by (int)TerrainType.
        static readonly float[] _speed = { 1.25f, 1.00f, 0.70f, 0.45f, 0.50f, 0.60f, 0f, 0f };
        static readonly float[] _sight = { 1.00f, 1.30f, 0.55f, 0.75f, 1.10f, 0.90f, 0f, 0f };
        // Ambush: road, plains, forest, marsh, ford, pass.
        //
        // Widened from { 1.2, 0.8, 1.5, 1.0, 1.3, 0.9 }, where the safest ground and the
        // most dangerous were under a factor of two apart. A route is a mix of terrain,
        // so its *mean* exposure is pulled toward the middle — and two routes across the
        // same country then differ by a couple of percent whatever ground they take. That
        // is what made "the safe way round" undeliverable: CorridorFinder can push a
        // route off the fast line, and did, but different ground was not safer ground, so
        // the safe corridor came out more exposed than the fast one on 11 of 17 failing
        // seeds.
        //
        // The spread is the mechanic. An ambush needs cover to wait in, so a bare plain
        // should be close to safe and deep forest close to certain; a road is exposed for
        // a different reason — it is where a caravan is expected to be — and a ford is
        // worse still because the column is pinned while it crosses. Plains to forest is
        // now 4.9x where it was 1.9x.
        static readonly float[] _ambush = { 1.1f, 0.45f, 2.2f, 1.0f, 1.4f, 0.7f, 0f, 0f };
        static readonly float[] _trap = { 0.6f, 0.5f, 1.0f, 2.5f, 0.8f, 1.4f, 0f, 0f };
        static readonly bool[] _passable = { true, true, true, true, true, true, false, false };

        /// <summary>Caravan speed multiplier. Zero for impassable terrain.</summary>
        public static float Speed(TerrainType t) => _speed[(int)t];

        /// <summary>Multiplier applied to a unit's sight radius while standing here.</summary>
        public static float Sight(TerrainType t) => _sight[(int)t];

        /// <summary>Relative likelihood the generator places an ambush here.</summary>
        public static float AmbushWeight(TerrainType t) => _ambush[(int)t];

        /// <summary>Multiplier on the recipe's trap density.</summary>
        public static float TrapDensity(TerrainType t) => _trap[(int)t];

        public static bool IsPassable(TerrainType t) => _passable[(int)t];

        /// <summary>
        /// Time cost of crossing one tile, i.e. the inverse of speed. A* minimising
        /// this minimises travel time rather than distance — which is what the route
        /// preview shows the player, so the path they get is the path they were
        /// promised.
        /// </summary>
        public static float TravelCost(TerrainType t)
            => _passable[(int)t] ? 1f / _speed[(int)t] : float.PositiveInfinity;

        /// <summary>Cheapest possible tile cost. The A* heuristic must not exceed this per step to stay admissible.</summary>
        public static float MinTravelCost
        {
            get
            {
                float min = float.PositiveInfinity;
                for (int i = 0; i < Count; i++)
                    if (_passable[i] && 1f / _speed[i] < min) min = 1f / _speed[i];
                return min;
            }
        }

        /// <summary>Applies designer-tuned values from a TerrainTypeDef asset.</summary>
        public static void Override(TerrainType t, float speed, float sight, float ambush, float trap, bool passable)
        {
            int i = (int)t;
            _speed[i] = speed;
            _sight[i] = sight;
            _ambush[i] = ambush;
            _trap[i] = trap;
            _passable[i] = passable;
        }
    }
}
