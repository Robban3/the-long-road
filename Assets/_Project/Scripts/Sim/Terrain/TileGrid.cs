namespace Arna.Sim
{
    /// <summary>
    /// The level's terrain, as a flat row-major array. Default size is 64x64 tiles
    /// at 4 world metres each (docs/technical-design.md §5).
    ///
    /// Flat rather than jagged: a 64x64 map is touched tens of thousands of times
    /// per pathfinding call, and one contiguous block keeps that in cache.
    /// </summary>
    public sealed class TileGrid
    {
        public const float TileSize = 4f;

        public readonly int Width;
        public readonly int Height;

        readonly TerrainType[] _tiles;
        readonly float[] _elevation;

        /// <summary>
        /// Tiles something solid is standing on: a trunk, a boulder, a cliff face.
        ///
        /// **On the grid rather than beside it, and that is the whole design.** The
        /// props used to be laid over the finished world by the view, which meant the
        /// simulation had never heard of them: routes were drawn straight through
        /// trees, troops walked into boulders, and the only fix available was to sweep
        /// the props out of whichever line the caravan happened to take — which makes
        /// the country a function of the player's choice, and the player chooses after
        /// seeing it.
        ///
        /// Marked here, before the endpoints are placed and long before a corridor is
        /// found, every route the player can be offered is already clear of them and
        /// every consumer of IsPassable gets it for nothing.
        /// </summary>
        readonly System.Collections.Generic.HashSet<int> _obstructed =
            new System.Collections.Generic.HashSet<int>();

        public TileGrid(int width, int height, TerrainType fill = TerrainType.Plains)
        {
            Width = width;
            Height = height;
            _tiles = new TerrainType[width * height];
            _elevation = new float[width * height];
            for (int i = 0; i < _tiles.Length; i++) _tiles[i] = fill;
        }

        /// <summary>
        /// Normalised terrain height, 0 to 1.
        ///
        /// The generator already computes this to decide terrain types and used to
        /// throw it away afterwards. Keeping it is what lets the play view stand the
        /// world up in three dimensions while the planning map stays flat and readable
        /// — one data source, two very different pictures.
        /// </summary>
        public float Elevation(int index) => _elevation[index];

        public float Elevation(int x, int y) => _elevation[y * Width + x];

        public void SetElevation(int index, float value) => _elevation[index] = value;

        /// <summary>
        /// Normalised ground height at a world position, interpolated between tile
        /// corners exactly as the rendered surface is built.
        ///
        /// Anything that stands on the ground must ask this rather than the tile's own
        /// elevation: the surface is built from corner heights averaged across four
        /// tiles, and a tile's centre value is a different number. Using the wrong one
        /// leaves trees hovering and wagons sunk into the hillside.
        /// </summary>
        public float SurfaceElevation(float worldX, float worldZ)
        {
            float tx = worldX / TileSize;
            float tz = worldZ / TileSize;

            int x = (int)tx;
            int y = (int)tz;
            if (x < 0) x = 0; else if (x >= Width) x = Width - 1;
            if (y < 0) y = 0; else if (y >= Height) y = Height - 1;

            float fx = tx - x;
            float fz = tz - y;
            if (fx < 0f) fx = 0f; else if (fx > 1f) fx = 1f;
            if (fz < 0f) fz = 0f; else if (fz > 1f) fz = 1f;

            float h00 = CornerElevation(x, y);
            float h10 = CornerElevation(x + 1, y);
            float h01 = CornerElevation(x, y + 1);
            float h11 = CornerElevation(x + 1, y + 1);

            float top = h00 + (h10 - h00) * fx;
            float bottom = h01 + (h11 - h01) * fx;
            return top + (bottom - top) * fz;
        }

        /// <summary>Height at a tile corner, averaged from the tiles that meet there.</summary>
        public float CornerElevation(int cornerX, int cornerY)
        {
            float sum = 0f;
            int count = 0;

            for (int dy = -1; dy <= 0; dy++)
            {
                for (int dx = -1; dx <= 0; dx++)
                {
                    int x = cornerX + dx;
                    int y = cornerY + dy;
                    if (!InBounds(x, y)) continue;
                    sum += _elevation[y * Width + x];
                    count++;
                }
            }

            return count == 0 ? 0f : sum / count;
        }

        public int TileCount => _tiles.Length;

        public TerrainType this[int x, int y]
        {
            get => _tiles[y * Width + x];
            set => _tiles[y * Width + x] = value;
        }

        public TerrainType this[int index]
        {
            get => _tiles[index];
            set => _tiles[index] = value;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public int ToIndex(int x, int y) => y * Width + x;

        public void ToCoords(int index, out int x, out int y)
        {
            y = index / Width;
            x = index - y * Width;
        }

        public bool IsPassable(int x, int y)
            => InBounds(x, y) && IsPassable(y * Width + x);

        public bool IsPassable(int index)
            => TerrainTable.IsPassable(_tiles[index]) && !_obstructed.Contains(index);

        /// <summary>Stands something solid on a tile. Nothing walks over it afterwards.</summary>
        public void Obstruct(int index)
        {
            if (index >= 0 && index < _tiles.Length) _obstructed.Add(index);
        }

        /// <summary>Takes the solid thing off a tile again.</summary>
        public void Free(int index) => _obstructed.Remove(index);

        public bool IsObstructed(int index) => _obstructed.Contains(index);

        public bool IsObstructed(int x, int y)
            => InBounds(x, y) && _obstructed.Contains(y * Width + x);

        /// <summary>
        /// Nudges a point out of whatever it is standing inside.
        ///
        /// The route is clear of obstacles because no route could be drawn through one,
        /// but not everything follows the route: the flank posts stand six metres out to
        /// either side, and an attacker crosses whatever country lies between it and the
        /// column. Neither of them is pathfinding, and neither of them should be —
        /// a formation that breaks up to walk round a tree is not a formation, and an
        /// A* per wolf per tick is not a phone game.
        ///
        /// So they are pushed rather than routed: a point inside a solid tile leaves by
        /// its nearest edge, which is a displacement of at most half a tile and usually
        /// far less. Against a scattered obstacle field that reads as walking round the
        /// trunk. Against a wall of them it reads as sliding along it, which is also what
        /// it is.
        ///
        /// An exit into another solid tile is not an exit, so the four edges are ranked
        /// and the first that leads somewhere is taken.
        /// </summary>
        public Vec2 SlideOut(Vec2 at)
        {
            int x = (int)System.Math.Floor(at.X / TileSize);
            int y = (int)System.Math.Floor(at.Y / TileSize);

            if (!InBounds(x, y) || !IsObstructed(x, y)) return at;

            const float Clearance = 0.15f;

            float west = at.X - x * TileSize;
            float east = (x + 1) * TileSize - at.X;
            float south = at.Y - y * TileSize;
            float north = (y + 1) * TileSize - at.Y;

            var best = at;
            float shortest = float.MaxValue;

            Consider(west, x - 1, y, new Vec2(x * TileSize - Clearance, at.Y), ref best, ref shortest);
            Consider(east, x + 1, y, new Vec2((x + 1) * TileSize + Clearance, at.Y), ref best, ref shortest);
            Consider(south, x, y - 1, new Vec2(at.X, y * TileSize - Clearance), ref best, ref shortest);
            Consider(north, x, y + 1, new Vec2(at.X, (y + 1) * TileSize + Clearance), ref best, ref shortest);

            // Every edge leads into another solid tile, so the point is inside a thicket
            // rather than against a trunk. Nothing to slide along; the nearest open
            // ground is the answer, and it is worth the jump because the alternative is
            // a priest standing inside a tree for the length of a level.
            return shortest < float.MaxValue ? best : NearestOpen(at, x, y);
        }

        /// <summary>The closest point on the nearest tile that has nothing standing on it.</summary>
        Vec2 NearestOpen(Vec2 at, int fromX, int fromY)
        {
            for (int radius = 1; radius <= 4; radius++)
            {
                var best = at;
                float shortest = float.MaxValue;

                for (int y = fromY - radius; y <= fromY + radius; y++)
                {
                    for (int x = fromX - radius; x <= fromX + radius; x++)
                    {
                        // The ring only: everything inside it failed on an earlier pass.
                        if (System.Math.Abs(x - fromX) != radius &&
                            System.Math.Abs(y - fromY) != radius) continue;

                        if (!InBounds(x, y) || IsObstructed(x, y)) continue;
                        if (!TerrainTable.IsPassable(this[x, y])) continue;

                        var exit = Clamped(at, x, y);
                        float dx = exit.X - at.X, dy = exit.Y - at.Y;
                        float distance = dx * dx + dy * dy;

                        if (distance >= shortest) continue;

                        best = exit;
                        shortest = distance;
                    }
                }

                if (shortest < float.MaxValue) return best;
            }

            return at;
        }

        /// <summary>The point of a tile nearest a place outside it, held off its edges.</summary>
        static Vec2 Clamped(Vec2 at, int x, int y)
        {
            const float Inset = 0.4f;

            float minX = x * TileSize + Inset, maxX = (x + 1) * TileSize - Inset;
            float minY = y * TileSize + Inset, maxY = (y + 1) * TileSize - Inset;

            return new Vec2(
                at.X < minX ? minX : at.X > maxX ? maxX : at.X,
                at.Y < minY ? minY : at.Y > maxY ? maxY : at.Y);
        }

        void Consider(float distance, int intoX, int intoY, Vec2 exit,
                      ref Vec2 best, ref float shortest)
        {
            if (distance >= shortest) return;
            if (!InBounds(intoX, intoY) || IsObstructed(intoX, intoY)) return;

            best = exit;
            shortest = distance;
        }

        /// <summary>Where the solid things are, for whoever has to draw them.</summary>
        public System.Collections.Generic.IReadOnlyCollection<int> Obstructions => _obstructed;

        /// <summary>Fills an axis-aligned rectangle, clipped to the grid.</summary>
        public void FillRect(int x0, int y0, int x1, int y1, TerrainType type)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 >= Width) x1 = Width - 1;
            if (y1 >= Height) y1 = Height - 1;

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    this[x, y] = type;
        }
    }
}
