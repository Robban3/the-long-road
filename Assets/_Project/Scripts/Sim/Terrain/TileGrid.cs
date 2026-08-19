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

        public TileGrid(int width, int height, TerrainType fill = TerrainType.Plains)
        {
            Width = width;
            Height = height;
            _tiles = new TerrainType[width * height];
            for (int i = 0; i < _tiles.Length; i++) _tiles[i] = fill;
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
