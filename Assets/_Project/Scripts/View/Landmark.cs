using System.Collections.Generic;

namespace Arna.View
{
    /// <summary>The kinds of built thing the decorator stands on a map.</summary>
    public enum LandmarkKind : byte
    {
        House = 0,
        Farm = 1,
        Watchtower = 2,
        Ruin = 3,
        Camp = 4,
        Wreck = 5,
        Timber = 6,

        /// <summary>Remains at a trap site: a skeleton, a skull, a grave.</summary>
        Bones = 7,

        /// <summary>A banner or a gravestone driven into the ground beside one.</summary>
        Totem = 8
    }

    /// <summary>
    /// One built thing and the tile it stands on.
    ///
    /// A receipt for what <see cref="TerrainDecorator.Decorate"/> put down, and it exists
    /// because the planning map could not tell a house from a ruin. Both are brown from
    /// four hundred metres up, and the map had no way to know which it was looking at:
    /// the decorator returned a count and nothing else, so the knowledge it plainly had
    /// — houses beside the road, ruins out in the country, towers in the passes — was
    /// thrown away the instant it was used.
    ///
    /// Nothing here decides anything. It is a list filled in as the placement it already
    /// does happens, so the map can draw a symbol that says which is which.
    /// </summary>
    public readonly struct Landmark
    {
        public readonly LandmarkKind Kind;
        public readonly int Tile;

        public Landmark(LandmarkKind kind, int tile)
        {
            Kind = kind;
            Tile = tile;
        }

        /// <summary>Adds one, when anybody is listening. Nothing to guard at the call sites.</summary>
        public static void Note(List<Landmark> into, LandmarkKind kind, int tile)
            => into?.Add(new Landmark(kind, tile));
    }
}
