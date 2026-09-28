using UnityEngine;

namespace TheVeil.View
{
    /// <summary>
    /// A piece that rests on its own ground rather than on its building's.
    ///
    /// <b>A building is one thing and is seated once, which is right for a building.</b>
    /// A wall, a roof and a chimney belong to each other: they are drawn to stack, and
    /// asking each of them separately where the ground is would take a house apart. So an
    /// assembly is measured whole and set down whole, and every piece of it keeps the
    /// height the piece below gave it.
    ///
    /// What that is wrong for is the stone lying five metres from the wall. A ruin
    /// scatters two to four of them out past its own footprint (BuildingBuilder.Ruin), and
    /// they are not part of the structure - they are what fell off it and came to rest on
    /// the hillside. Seated with the ruin they sit on the ruin's plane, so on a slope the
    /// uphill ones go under the ground and the downhill ones stand on air. One went under
    /// on 6-5 and was the last thing the smoke test could still find, once the mountains
    /// were given two and a half times the relief they used to have.
    ///
    /// Marked rather than named, so that nothing which reads prop names - the census, the
    /// smoke test's exemptions, the paint report - has to learn a new spelling for a stone
    /// that is still the same stone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Loose : MonoBehaviour
    {
    }
}
