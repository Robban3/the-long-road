using System;
using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// What is standing in the way, and how to get round it.
    ///
    /// The column has always driven straight through trees and boulders, and the reason
    /// is that nothing in the simulation knew they were there: the scenery is placed by
    /// the view, from the same seed but on the other side of the fence, and the troops
    /// take their positions from arithmetic on the caravan's line — post so many metres
    /// forward, so many to the right — with nothing in between to say the spot is
    /// occupied.
    ///
    /// This is that missing thing, and it is deliberately small. Each solid prop
    /// contributes one disc, and a position that lands inside a disc is pushed out to its
    /// edge. Applied every step, that reads on screen as walking round the tree: the post
    /// slides along the trunk as the column advances and comes back to its proper place
    /// the moment it is past.
    ///
    /// <b>The radius is the trunk, not the crown.</b> An earlier attempt at this made a
    /// tree as wide as its canopy, which is honest and made the forest a wall — the
    /// player was left with a green stripe they could not enter, and it was reverted for
    /// that reason. A wood is a place you walk through, brushing past trunks. Four and a
    /// half metres of spruce crown blocks nothing; the half-metre of trunk under it does.
    /// </summary>
    public sealed class ObstacleField
    {
        readonly struct Disc
        {
            public readonly float X, Z, Radius;

            public Disc(float x, float z, float radius)
            {
                X = x;
                Z = z;
                Radius = radius;
            }
        }

        /// <summary>Discs bucketed by tile, so a lookup reads nine buckets and not the map.</summary>
        readonly Dictionary<int, List<Disc>> _buckets = new Dictionary<int, List<Disc>>();

        /// <summary>Bucket side in metres. A tile, which is the size everything else here uses.</summary>
        public const float Bucket = TileGrid.TileSize;

        /// <summary>
        /// The largest radius that may be added.
        ///
        /// A cap rather than a trust in the caller. One badly measured prop — a horizon
        /// range, a backdrop, a mesh with a stray vertex at the origin — would otherwise
        /// put a fifty-metre hole in the map that the escort walks round for the whole
        /// level, and it would be very hard to see what had happened.
        /// </summary>
        public const float MaxRadius = 6f;

        public int Count { get; private set; }

        public void Add(float x, float z, float radius)
        {
            if (radius <= 0.05f) return;
            if (radius > MaxRadius) radius = MaxRadius;

            var disc = new Disc(x, z, radius);

            // Into every bucket the disc reaches, so a lookup never has to widen its
            // search for a prop bigger than one bucket.
            int minX = (int)Math.Floor((x - radius) / Bucket);
            int maxX = (int)Math.Floor((x + radius) / Bucket);
            int minZ = (int)Math.Floor((z - radius) / Bucket);
            int maxZ = (int)Math.Floor((z + radius) / Bucket);

            for (int bz = minZ; bz <= maxZ; bz++)
            {
                for (int bx = minX; bx <= maxX; bx++)
                {
                    int key = Key(bx, bz);

                    if (!_buckets.TryGetValue(key, out var bucket))
                        _buckets[key] = bucket = new List<Disc>();

                    bucket.Add(disc);
                }
            }

            Count++;
        }

        public void Clear()
        {
            _buckets.Clear();
            Count = 0;
        }

        static int Key(int bx, int bz) => bz * 8192 + bx;

        /// <summary>Whether a body of this radius standing here would be inside something.</summary>
        public bool Blocked(Vec2 at, float clearance = 0f)
        {
            int bx = (int)Math.Floor(at.X / Bucket);
            int bz = (int)Math.Floor(at.Y / Bucket);

            if (!_buckets.TryGetValue(Key(bx, bz), out var bucket)) return false;

            for (int i = 0; i < bucket.Count; i++)
            {
                float dx = at.X - bucket[i].X;
                float dz = at.Y - bucket[i].Z;
                float reach = bucket[i].Radius + clearance;

                if (dx * dx + dz * dz < reach * reach) return true;
            }

            return false;
        }

        /// <summary>
        /// The nearest spot to <paramref name="wanted"/> that is not inside anything.
        ///
        /// Pushed straight out of each disc it overlaps, twice over, because stepping out
        /// of one can step into its neighbour — two passes settles the gap between a pair
        /// of trunks, and a third buys nothing measurable. A position that is exactly on a
        /// disc's centre has no direction to leave by and is nudged east first, which is
        /// arbitrary and has to be *something*.
        /// </summary>
        public Vec2 Clear(Vec2 wanted, float clearance = 0f)
        {
            if (_buckets.Count == 0) return wanted;

            float x = wanted.X, z = wanted.Y;

            for (int pass = 0; pass < 2; pass++)
            {
                int bx = (int)Math.Floor(x / Bucket);
                int bz = (int)Math.Floor(z / Bucket);

                if (!_buckets.TryGetValue(Key(bx, bz), out var bucket)) break;

                bool moved = false;

                for (int i = 0; i < bucket.Count; i++)
                {
                    float dx = x - bucket[i].X;
                    float dz = z - bucket[i].Z;
                    float reach = bucket[i].Radius + clearance;
                    float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                    if (distance >= reach) continue;

                    if (distance < 0.0001f) { dx = 1f; dz = 0f; distance = 1f; }

                    x = bucket[i].X + dx / distance * reach;
                    z = bucket[i].Z + dz / distance * reach;
                    moved = true;
                }

                if (!moved) break;
            }

            return new Vec2(x, z);
        }
    }
}
