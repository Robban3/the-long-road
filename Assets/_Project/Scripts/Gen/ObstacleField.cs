using Arna.Sim;

namespace Arna.Gen
{
    /// <summary>
    /// Stands the solid things on the map before anybody decides where to walk.
    ///
    /// The trees and boulders used to be the view's business: the generator produced
    /// terrain, the corridors were found through it, the encounters were placed on it,
    /// and only then did the decorator strew several hundred models over the finished
    /// article. Nothing downstream of the decorator existed, so nothing downstream of
    /// the decorator could avoid it — the caravan drove through trunks, the escort
    /// walked into boulders, and the only remedy available at that end of the pipeline
    /// was to keep the props off whichever line the caravan happened to take.
    ///
    /// **Which is backwards.** The player picks the route after looking at the country,
    /// so a country arranged around the route is a country arranged around a decision
    /// that has not been made yet — and the two routes not taken stay full of trees the
    /// escort would have walked through.
    ///
    /// So the solid things are decided here, from the level's own seed, before the
    /// endpoints are placed and long before a corridor is drawn. Every route the player
    /// can be offered is clear of them because the pathfinder could not have drawn one
    /// through them. The decorator's job shrinks to standing a model on each of them.
    ///
    /// Nothing is placed on water, on a ford or on a cliff: the first two carry their
    /// own scenery and the third is already impassable, and marking a tile that no
    /// route could use anyway only makes the map look busier than it is.
    /// </summary>
    public static class ObstacleField
    {
        /// <summary>
        /// How much of each kind of ground carries something a wagon cannot pass.
        ///
        /// These are not the decorator's old scatter densities. Those were a look — how
        /// thick the country should read — and could be as high as they liked because
        /// nothing had to walk through it. These are a constraint on every route on the
        /// map, so they are the share of ground that can be given away before the
        /// pathfinder starts having to work around a maze.
        ///
        /// Forest carries most, which is what a forest is; plains carry a scattering of
        /// boulders and lone trees; marsh carries least, because a bog with a boulder
        /// field in it is neither.
        ///
        /// The first numbers tried were more than twice these and the pathfinder took
        /// them without complaint — three corridors on every level of the chapter, first
        /// attempt. What could not take them was the triangle budget: six hundred to
        /// seven hundred obstacles per map, *on top of* the six hundred scattered props,
        /// where the whole scene used to be six hundred.
        ///
        /// **And the second constraint was a surprise worth writing down.** Standing
        /// trees on the map changes which routes the corridor finder draws, which changes
        /// which enemies a route meets, which changes whether a level can be survived —
        /// and that relationship is not monotonic. A forest share of 0.12 leaves every
        /// level of chapter 1 with the ways through it owes; 0.04, 0.08 and 0.18 all
        /// leave 1-5 with one where it owes two. Fewer obstacles is not safer, it is
        /// merely different.
        ///
        /// Which says something uncomfortable about the guarantee rather than about this
        /// number: the generator does not enforce it. It retries for corridor quality
        /// and for silver, and survivability is a property the old seeds happened to
        /// have. Any new feature in the world re-rolls that luck. This number is chosen
        /// to keep the promise on the seeds the test walks, and the promise itself wants
        /// enforcing where it is made.
        /// </summary>
        public const float ForestShare = 0.12f;
        public const float PlainsShare = 0.04f;
        public const float MarshShare = 0.02f;
        public const float PassShare = 0.066f;

        /// <summary>
        /// How much clear ground is left around the start and the goal, in tiles.
        ///
        /// The caravan forms up behind the start line — ten tiles of run-up — and it
        /// forms up in whatever direction the route leaves in, which is not known here.
        /// A ring is the answer that does not need to know.
        /// </summary>
        public const int EndpointClearTiles = 12;

        public static void Grow(TileGrid grid, DeterministicRandom rng)
        {
            for (int i = 0; i < grid.TileCount; i++)
            {
                float share = ShareFor(grid[i]);
                if (share <= 0f) continue;
                if (rng.Chance(share)) grid.Obstruct(i);
            }
        }

        /// <summary>
        /// Clears a circle of ground, for the places a route has to be able to begin.
        ///
        /// Called after the endpoints are known rather than before, because they are
        /// chosen by looking at the terrain and moving them to suit the trees would be
        /// the same inversion this class exists to undo.
        /// </summary>
        public static void Clear(TileGrid grid, int centreX, int centreY, int radiusTiles)
        {
            int squared = radiusTiles * radiusTiles;

            for (int y = centreY - radiusTiles; y <= centreY + radiusTiles; y++)
            {
                for (int x = centreX - radiusTiles; x <= centreX + radiusTiles; x++)
                {
                    if (!grid.InBounds(x, y)) continue;

                    int dx = x - centreX, dy = y - centreY;
                    if (dx * dx + dy * dy > squared) continue;

                    grid.Free(grid.ToIndex(x, y));
                }
            }
        }

        static float ShareFor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Forest: return ForestShare;
                case TerrainType.Plains: return PlainsShare;
                case TerrainType.Marsh: return MarshShare;
                case TerrainType.MountainPass: return PassShare;
                default: return 0f;   // road, water, ford, cliff
            }
        }
    }
}
