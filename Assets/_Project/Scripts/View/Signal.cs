using UnityEngine;

namespace TheVeil.View
{
    /// <summary>
    /// Marks a prop that is telling the player something, rather than dressing the map.
    ///
    /// The wrecked cart at a trap site, the bones beside it, the banner driven into the
    /// ground, the raiders' tents: these are the whole of the information the planning
    /// map offers before the eagle flies. Every prop on that map is painted down to a
    /// flat grey until the fog lifts off it — which is right for a tree and exactly
    /// wrong for these, because a signal nobody can see is not a signal. The player was
    /// looking at four thousand grey tiles and being asked to choose a route through
    /// them.
    ///
    /// So the fog leaves these alone. It is not a leak: what they mark is a piece of
    /// ground *somebody else* disturbed, which is knowledge the caravan has by looking
    /// at the country. What the fog is hiding is where the enemies are now.
    /// </summary>
    public sealed class Signal : MonoBehaviour
    {
    }
}
