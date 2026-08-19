using System;

namespace Arna.Sim
{
    /// <summary>
    /// A world position in metres.
    ///
    /// Defined here rather than using UnityEngine.Vector2 because Arna.Sim is
    /// compiled without engine references — that is what keeps the simulation
    /// deterministic and runnable in a plain unit test.
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        /// <summary>Squared distance. Preferred in hot paths — no square root.</summary>
        public static float DistanceSquared(Vec2 a, Vec2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        public static float Distance(Vec2 a, Vec2 b) => (float)Math.Sqrt(DistanceSquared(a, b));

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);

        /// <summary>Centre of a tile, in metres.</summary>
        public static Vec2 FromTile(TileGrid grid, int tileIndex)
        {
            grid.ToCoords(tileIndex, out int x, out int y);
            return new Vec2((x + 0.5f) * TileGrid.TileSize, (y + 0.5f) * TileGrid.TileSize);
        }

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public override string ToString() => $"({X:F1}, {Y:F1})";
    }
}
