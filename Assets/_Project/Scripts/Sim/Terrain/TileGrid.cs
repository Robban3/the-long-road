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
            => InBounds(x, y) && TerrainTable.IsPassable(this[x, y]);

        public bool IsPassable(int index) => TerrainTable.IsPassable(_tiles[index]);

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
