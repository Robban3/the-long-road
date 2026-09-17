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

                /// <summary>
        /// How wide and deep the walls stand, in tiles.
        ///
        /// Fifty by forty on a 64 by 64 map: four fifths of it across, and just under
        /// half the level by area. The first town was 26 by 16 — a tenth of the level, a
        /// walled yard the road clipped the corner of.
        ///
        /// <b>The depth is traded against the way round, and it is a real trade.</b> At
        /// forty-eight deep the town covered 58 per cent of the map and left fifteen rows
        /// south of it — enough ground to walk, and so far round that the detour measured
        /// 173 tiles against 76 through the gates. All three corridors then went through
        /// the town, and the level lost the choice it was built to offer. Eight rows
        /// shallower gives the road outside a length worth taking.
        ///
        /// The depth is what it is because of the way round: the town stands against the
        /// north edge, and what is left south of it is the one road past. Take that too
        /// and there is no choice on the level at all.
        /// </summary>
        public const int Width = 50;
        public const int Depth = 40;

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

        /// <summary>One block of building, in tiles, and the street between two of them.</summary>
        public const int BlockWidth = 8;
        public const int BlockDepth = 4;
        public const int StreetWidth = 3;

        /// <summary>How deep the ground just inside a wall is kept open, in tiles.</summary>
        public const int Approach = 4;

        /// <summary>
        /// How wide a gateway is, in tiles either side of the gate row.
        ///
        /// Two either way, so five tiles and twenty metres of opening.
        ///
        /// <b>The column is sixteen metres wide</b> — TerrainDecorator.DriveHalfWidth is
        /// eight and it sweeps that far either side of the line. At three tiles the
        /// gateway was twelve, so the caravan was four metres wider than the hole it was
        /// driving through and its flanks went through the wall on the way out. A gate is
        /// wide enough for what has to pass it or it is a doorway.
        /// </summary>
        public const int GateHalf = 2;

        /// <summary>How far the ground outside the wall is cleared, in tiles.</summary>
        /// <summary>How far apart the alleys between two streets are cut, in tiles.</summary>
        public const int AlleyStep = 9;

        /// <summary>
        /// How wide an alley is cut, in tiles.
        ///
        /// Two, not one. The houses that line it are seven and a half metres across on a
        /// four-metre tile, so each leans nearly two metres over the ground in front of
        /// it — and a one-tile alley with building on both sides was four metres of
        /// ground with three and a half metres of gable hanging into it. The caravan drove
        /// through the walls. Two tiles leaves a lane after the eaves have taken theirs.
        /// </summary>
        public const int AlleyWidth = 2;

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

            /// <summary>Whether a tile is part of the wall itself, and so impassable.</summary>
            public bool IsWall(int x, int y)
            {
                if (!Holds(x, y)) return false;

                // Two rings thick, because the wall is the map's own border and a border
                // one tile wide leaves a lane outside it. A lane outside the wall is a way
                // round, and on this level there is not meant to be one.
                bool edge = x <= West + 1 || x >= East - 1 || y <= North + 1 || y >= South - 1;
                if (!edge) return false;

                return !IsGateway(x, y);
            }

            /// <summary>
            /// The face of the wall: the inner of its two rings, which is what is built.
            ///
            /// The outer ring is impassable ground and nothing stands on it. It is the
            /// thickness of the wall seen from inside, and from outside it is past the
            /// edge of the world.
            /// </summary>
            public bool IsFace(int x, int y)
                => IsWall(x, y)
                   && (x == West + 1 || x == East - 1 || y == North + 1 || y == South - 1);

            /// <summary>The opening a gateway makes, through both rings.</summary>
            public bool IsGateway(int x, int y)
                => (x <= West + 1 || x >= East - 1)
                   && y >= GateRow - GateHalf && y <= GateRow + GateHalf;

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
        /// ask without a grid to write into and get the same answer.
        ///
        /// <paramref name="road"/> is the row the caravan starts on, and the town is laid
        /// around it: the gates go on that row, and the walls are pushed to whichever edge
        /// of the map is nearer so that the one way past them is on the other side. A town
        /// placed without regard to the road is a town the road goes round.
        /// </summary>
        public static Plan Layout(int mapWidth, int mapHeight, int seed, int road)
        {
            if (mapWidth < 24 || mapHeight < 24) return None;

            // The whole map, and its border is the wall.
            //
            // <b>A town that covers most of a level cannot also have a way round it.</b>
            // Measured at fifty by forty: going round cost ten rows north, sixty-four
            // east and ten back down — eighty-four tiles against sixty-four straight
            // through — so every corridor went through the gates however open the ground
            // outside was made. The two wishes were in each other's way, and the answer
            // was to drop one of them: there is no outside now. The level is the town, the
            // map's edge is its wall, and the three ways through are three streets.
            int gateRow = road;
            if (gateRow < Approach + 2) gateRow = Approach + 2;
            if (gateRow > mapHeight - Approach - 3) gateRow = mapHeight - Approach - 3;

            return new Plan(0, mapWidth - 1, 0, mapHeight - 1, gateRow);
        }

        public static Plan Stamp(TileGrid grid, int seed, int road)
        {
            var plan = Layout(grid.Width, grid.Height, seed, road);
            if (!plan.Any) return None;

            int north = plan.North, south = plan.South;
            int west = plan.West, east = plan.East;
            int gateRow = plan.GateRow;

            // Every tile of it is town: cleared, level ground with building on it, and
            // whatever the noise and the rivers left is gone. A river through a walled
            // town would be a fourth way in.
            //
            // The whole grid, because the whole grid is the town - see Layout, which says
            // why the level stopped having an outside. That is also why this level owes no
            // crossings: there is nowhere for water to be. See LevelRecipe.CrossingsOwed
            // and ChapterRecipe, which sets it to nought here.
            for (int i = 0; i < grid.TileCount; i++) grid[i] = TerrainType.Cliff;

            float height = grid.Elevation(grid.ToIndex(grid.Width / 2, gateRow));
            for (int i = 0; i < grid.TileCount; i++) grid.SetElevation(i, height);

            // Three streets from gate to gate, and building everywhere else.
            //
            // Three, because the level owes the player three ways through and a town has
            // to make all of them out of street. They are spread across the depth rather
            // than bunched: the gates open onto the middle one, and the ends of the town
            // carry the two lanes that reach the others.
            int lane = StreetWidth;
            int[] streets =
            {
                north + 2 + Approach,
                gateRow,
                south - 2 - Approach - lane + 1
            };

            // <b>The main street is a road and the other two are not, and that is the
            // whole choice this level has.</b>
            //
            // All three were cut as the same ground, so the three ways through a town were
            // three identical ways through a town: measured, the fast road and the
            // cautious road came out at the same exposure to four decimal places with
            // three per cent between them in time. IsMeaningfulChoice asks for a road that
            // costs blood against a road that costs time, and a level with one kind of
            // ground on it cannot answer, however the corridors are drawn.
            //
            // Every other level answers with terrain and a town has only its streets, so
            // its streets are the terrain. The gate-to-gate street is a road: quick, and
            // exposed, because a road is where a caravan is expected to be - which is
            // exactly what the high street of a town is. The two back streets are laid as
            // open ground, slower by a quarter and less than half as exposed.
            //
            // So the level asks what a town asks. Straight down the high street and out
            // the far gate, or the long way round the back lanes.
            for (int i = 0; i < streets.Length; i++)
                Cut(grid, west + 2, east - 2, streets[i], streets[i] + lane - 1,
                    streets[i] == gateRow ? TerrainType.Road : TerrainType.Plains);

            // And the alleys between them, which is what a town has that a road does not.
            //
            // One tile wide against the streets' three, cut north to south between one
            // street and the next, and offset so that no two line up into a fourth way
            // through. A medieval town is a few streets somebody laid out and a great many
            // gaps between buildings that people wore into shortcuts; the streets carry the
            // caravan and the alleys are what make the place look lived in rather than
            // planned.
            //
            // They are passable, so a player who wants to thread one may. That is the
            // point of them: the three streets are the ways through the level owes, and
            // the alleys are the choices inside those ways.
            for (int pair = 0; pair + 1 < streets.Length; pair++)
            {
                int from = streets[pair] + lane;
                int to = streets[pair + 1] - 1;
                if (to < from) continue;

                for (int x = west + 2 + Approach + AlleyStep / 2 + pair * (AlleyStep / 3);
                     x <= east - 2 - Approach;
                     x += AlleyStep)
                    Cut(grid, x, x + AlleyWidth - 1, from, to);
            }

            // The two ends, joined down the inside of each wall so all three streets are
            // reached from both gates.
            int top = System.Math.Min(streets[0], gateRow);
            int foot = System.Math.Max(streets[2] + lane - 1, gateRow);

            Cut(grid, west + 2, west + 1 + Approach, top, foot);
            Cut(grid, east - 1 - Approach, east - 2, top, foot);

            // And the gateways, through both rings of the wall. Road, because they are
            // the high street's own two ends.
            Cut(grid, west, west + 1, gateRow - GateHalf, gateRow + GateHalf, TerrainType.Road);
            Cut(grid, east - 1, east, gateRow - GateHalf, gateRow + GateHalf, TerrainType.Road);

            // Then the wall over all of it, which puts back anything the cuts took from
            // the border except the gateways themselves.
            for (int y = north; y <= south; y++)
                for (int x = west; x <= east; x++)
                    if (plan.IsWall(x, y))
                        grid[grid.ToIndex(x, y)] = TerrainType.Cliff;

            return plan;
        }

        /// <summary>
        /// Opens a street inside the town, clipped to its walls.
        ///
        /// <b>Street, and the terrain type is the whole of what was wrong with this
        /// level.</b> It laid Plains, and plains is the fastest open ground in the game
        /// and by a long way the safest - so half of 1-8 became the best country on the
        /// map to drive a caravan through. Going through the gates was shorter *and*
        /// quieter than going round, which is not a choice, and the level measured it:
        /// the fast road and the cautious road came out with the same exposure to four
        /// decimal places and three per cent between them in time.
        ///
        /// A street is a road. The table already knows what that means and says why -
        /// quick, and exposed, because a road is where a caravan is expected to be. So
        /// the town is now the fast way and the dangerous way, and the long way round the
        /// south of it is slow and quiet. That is the level the town was built for.
        ///
        /// <b>No water inside the walls, ever.</b> The whole grid is cleared to wall
        /// before this runs, so whatever river the generator carved is gone by the time a
        /// street is laid. A town has no river and no ford, which is why the level owes no
        /// crossings.
        /// </summary>
        static void Cut(TileGrid grid, int x0, int x1, int y0, int y1,
                        TerrainType ground = TerrainType.Plains)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (grid.InBounds(x, y)) grid[grid.ToIndex(x, y)] = ground;
        }
    }
}
