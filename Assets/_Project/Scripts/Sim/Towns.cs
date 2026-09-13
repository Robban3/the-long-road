namespace TheVeil.Sim
{
    /// <summary>
    /// A walled town, stamped into the map before the ways through it are found.
    ///
    /// <b>A wall has to be ground, not scenery.</b> Everything two metres and taller is
    /// placed so that it does not stand in the caravan's way — that rule is what keeps a
    /// pine out of the road, and it would keep a town wall out of it too. A wall built as
    /// decoration would step aside for whatever line the player drew, which is the exact
    /// opposite of what a wall is for. So the walls are cliff: impassable tiles, laid
    /// down before CorridorFinder runs, and every way through the level has to respect
    /// them because they are the country rather than what is standing on it.
    ///
    /// The shape of the level that falls out of that:
    ///
    ///   * The town stands against the map's northern edge, so there is no way round it
    ///     to the north. One way past it outside the walls, to the south.
    ///   * One gate on the west wall and one on the east, on the same row, so the road
    ///     goes in one side and out the other.
    ///   * A block of building in the middle of the town, which splits the inside into
    ///     two lanes — north of the block and south of it.
    ///
    /// Three ways through, which is what the level owes: one outside and two within. The
    /// generator still validates all of it — a stamp that walled the level shut is a map
    /// with no route, and that map is re-rolled like any other.
    /// </summary>
    public static class Towns
    {
        /// <summary>Keeps the town's dice clear of every other stream drawn from the seed.</summary>
        const int Salt = 0x7017;

        /// <summary>The chapter and level the town stands on.</summary>
        public const int Chapter = 1;
        public const int Level = 8;

        /// <summary>Whether this level is the town.</summary>
        public static bool HasTown(int chapter, int level) => chapter == Chapter && level == Level;

        /// <summary>How wide and deep the walls stand, in tiles.</summary>
        public const int Width = 26;
        public const int Depth = 16;

        /// <summary>
        /// How far the north wall sits from the map's edge, in tiles.
        ///
        /// One. Not nothing, because the edge of the grid is where the apron carries the
        /// ground on past the boundary and a wall laid on the last row would have its
        /// footing outside the world. Not more, because the gap is a way round: two tiles
        /// of open ground north of the wall is a road, and the whole point of standing the
        /// town against the edge is that there is exactly one way past it.
        /// </summary>
        public const int Margin = 1;

        /// <summary>How much of the town's middle is solid building, in tiles.</summary>
        public const int BlockWidth = 14;
        public const int BlockDepth = 4;

        /// <summary>Where a town stands and where its gates are.</summary>
        public readonly struct Plan
        {
            /// <summary>The wall's own tiles, inclusive.</summary>
            public readonly int West, East, North, South;

            /// <summary>The row both gates stand on.</summary>
            public readonly int GateRow;

            public Plan(int west, int east, int north, int south, int gateRow)
            {
                West = west;
                East = east;
                North = north;
                South = south;
                GateRow = gateRow;
            }

            public bool Any => East > West;

            /// <summary>The tile of the gate in the west wall.</summary>
            public int WestGate(TileGrid grid) => grid.ToIndex(West, GateRow);

            /// <summary>The tile of the gate in the east wall.</summary>
            public int EastGate(TileGrid grid) => grid.ToIndex(East, GateRow);

            /// <summary>Whether a tile is part of the wall itself.</summary>
            public bool IsWall(int x, int y)
            {
                if (x < West || x > East || y < North || y > South) return false;

                bool edge = x == West || x == East || y == North || y == South;
                if (!edge) return false;

                // The gates are holes in it.
                return !((x == West || x == East) && y == GateRow);
            }

            /// <summary>Whether a tile is inside the walls, gates included.</summary>
            public bool Holds(int x, int y) => x >= West && x <= East && y >= North && y <= South;
        }

        /// <summary>Nothing, for the levels that have no town.</summary>
        public static readonly Plan None = new Plan(0, 0, 0, 0, 0);

        /// <summary>
        /// Lays the town into the grid and says where it went.
        ///
        /// Called after the rivers are cut and before the endpoints are placed, so the
        /// ways through are found on a map that already has the town in it. Everything it
        /// writes is ordinary terrain: cliff for the walls and the middle block, plains
        /// for the ground inside, so nothing downstream needs to know a town exists to
        /// treat it correctly.
        /// </summary>
        /// <summary>
        /// Where the town stands, without laying a stone of it.
        ///
        /// Split from <see cref="Stamp"/> so that everything which has to know where the
        /// walls are — the decorator that builds them, a test that measures them — can
        /// ask without a grid to write into and get the same answer. It is a function of
        /// the level's seed and nothing else, which is also why the generator re-rolling
        /// a map does not move the town.
        /// </summary>
        public static Plan Layout(int mapWidth, int mapHeight, int seed)
        {
            if (mapWidth < Width + 8 || mapHeight < Depth + 8) return None;

            int north = Margin;
            int south = north + Depth - 1;

            // Somewhere along the map's width, clear of both edges so the start and the
            // goal are never walled in.
            var rng = new DeterministicRandom(seed ^ Salt);
            int west = rng.Range(6, mapWidth - Width - 6);

            return new Plan(west, west + Width - 1, north, south, north + Depth / 2);
        }

        public static Plan Stamp(TileGrid grid, int seed)
        {
            var plan = Layout(grid.Width, grid.Height, seed);
            if (!plan.Any) return None;

            int north = plan.North, south = plan.South;
            int west = plan.West, east = plan.East;
            int gateRow = plan.GateRow;

            // The ground inside first: a town is built on cleared, level ground, and
            // whatever the noise put here — bog, wood, a corner of a lake — is not it.
            for (int y = north; y <= south; y++)
                for (int x = west; x <= east; x++)
                    grid[grid.ToIndex(x, y)] = TerrainType.Plains;

            // Then the walls, which are cliff because cliff is what nothing walks through.
            for (int y = north; y <= south; y++)
                for (int x = west; x <= east; x++)
                    if (plan.IsWall(x, y))
                        grid[grid.ToIndex(x, y)] = TerrainType.Cliff;

            // And the block in the middle, which is what gives the inside two lanes
            // rather than one hall. Centred, and short of both gates so neither opens
            // onto it.
            int blockWest = west + (Width - BlockWidth) / 2;
            int blockNorth = gateRow - BlockDepth / 2;

            for (int y = blockNorth; y < blockNorth + BlockDepth; y++)
                for (int x = blockWest; x < blockWest + BlockWidth; x++)
                    if (y > north && y < south)
                        grid[grid.ToIndex(x, y)] = TerrainType.Cliff;

            // The ground the town stands on is level, or its walls step down a hillside
            // one tile at a time and read as a ruin. Taken from the middle of the site so
            // the whole plot is flattened to the same height.
            float height = grid.Elevation(grid.ToIndex((west + east) / 2, gateRow));

            for (int y = north; y <= south; y++)
                for (int x = west; x <= east; x++)
                    grid.SetElevation(grid.ToIndex(x, y), height);

            return plan;
        }
    }
}
