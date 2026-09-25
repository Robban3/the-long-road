using UnityEngine;

namespace TheVeil.View
{
    /// <summary>
    /// Says that this mesh is the water itself, and not something standing in it.
    ///
    /// <b>Because the bridge sweep ate every river in the game.</b> The water is laid as
    /// one sheet over every wet tile on the map, which is the only way it reads as a river
    /// rather than as blue plates lying on the grass — and <c>SweepTheBridges</c> clears
    /// the roadway by measuring every child of the map against the deck's outline and
    /// taking anything that overlaps it. A sheet that covers the map overlaps every bridge
    /// there has ever been, so it was destroyed on every level that had one, which is all
    /// of them. What was left was the blue the ground is painted: a river that is a colour
    /// on a hillside, with no surface, no depth at the banks and nothing that moves.
    ///
    /// It went unnoticed for so long precisely because the ground underneath is painted
    /// water-blue: every picture of the game still showed a river where a river belongs.
    /// Measured instead - three hundred and seventy-three wet tiles on 1-1, a builder
    /// returning five hundred vertices, and nought sheets standing when the decorating was
    /// done - it took one run to find.
    ///
    /// A component rather than a name, because the sweep and the laying are four thousand
    /// lines apart and a string in two places is a rename away from this happening again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterSheet : MonoBehaviour
    {
    }
}
