using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// Says that this prop is something you have to walk round.
    ///
    /// The scenery is placed here in the view and the walking is done over in the
    /// simulation, which has never had any idea what the country it crosses has standing
    /// on it — so the column drove through trunks and boulders because, to the code
    /// moving it, they were not there. This is the note the two sides pass each other:
    /// the decorator marks what is solid and how wide, and the run collects the marks
    /// into an <see cref="Arna.Sim.ObstacleField"/> before the first step is taken.
    ///
    /// The radius is the part that matters, and it is not the prop's own width. A spruce
    /// has four and a half metres of crown over half a metre of trunk, and a wood you
    /// cannot walk into is not a wood — an earlier attempt used the full footprint and
    /// turned the forest into a wall the player had to go round. What blocks is what you
    /// would actually bump into.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Solid : MonoBehaviour
    {
        /// <summary>Metres of clear ground this prop claims about its centre.</summary>
        public float Radius = 0.5f;

        /// <summary>
        /// Where that centre is, in world x and z.
        ///
        /// Recorded rather than read off the transform. A prefab's origin is wherever the
        /// artist left it — often at one corner of the model, and for a few of these packs
        /// several metres from the mesh — so a disc placed at the transform would sit
        /// beside the tree rather than in it.
        /// </summary>
        public Vector2 Centre;
    }
}
