using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// The grey the planning map lies under until the eagle lifts it (docs/GDD.md §3.6).
    ///
    /// It takes the <b>colour</b> out and leaves the geometry, and that is the design
    /// rather than an economy. The terrain is what the player plans against — where the
    /// forest is, where the ground opens, where the river runs — so hiding it would
    /// remove the decision instead of the certainty. What is bought with the eagle is
    /// knowing that a stretch of country is *as it appears*, and the difference between
    /// a muted hillside and a lit one is exactly that.
    ///
    /// The numbers are the ones the map render settled: a hillside at 0.72 of the mix
    /// came out a haze rather than a layer, and the eagle's trail barely showed against
    /// the land it had scouted.
    /// </summary>
    public static class PlanningOverlay
    {
        /// <summary>The grey everything is pushed toward. Faintly warm, and faintly green.</summary>
        public static readonly Color Grey = new Color(0.46f, 0.47f, 0.45f);

        /// <summary>How much of the muted colour replaces the original.</summary>
        public const float Mix = 0.88f;

        /// <summary>How far the muted version is taken down as well as across.</summary>
        public const float Darken = 0.88f;

        /// <summary>
        /// How much of its own colour a prop keeps under the overlay.
        ///
        /// Props are darkened rather than desaturated, and that is a compromise worth
        /// naming. The ground is a mesh this project builds, so its vertex colours can be
        /// pushed all the way to luminance; a tree is a Synty prefab with a Synty
        /// material, and the only handle a material property block offers is a colour
        /// that <i>multiplies</i> the atlas. Multiplication can darken a green tree and
        /// cannot take the green out of it.
        ///
        /// So under the overlay the wood goes to a third of its light and stays green. It
        /// reads as country in shadow, which is close enough to country not yet looked at
        /// — and the honest fix, a shader that desaturates, is a bigger job than the
        /// difference is worth today.
        /// </summary>
        public const float PropLight = 0.34f;

        /// <summary>
        /// Mutes one colour: to its own brightness, tinted toward the overlay grey, taken
        /// down, and mixed back over the original.
        /// </summary>
        public static Color Mute(Color colour)
        {
            // The same weights the map render uses, and the same ones a television used:
            // the eye takes most of its brightness from green and almost none from blue,
            // so an unweighted average turns a green hillside into a pale one.
            float luminance = colour.r * 0.299f + colour.g * 0.587f + colour.b * 0.114f;

            // Scaled by the grey's own mean, so muting cannot change how bright the
            // picture is overall — only where its colour went.
            float mean = (Grey.r + Grey.g + Grey.b) / 3f;

            var muted = new Color(
                luminance * Grey.r / mean * Darken,
                luminance * Grey.g / mean * Darken,
                luminance * Grey.b / mean * Darken,
                colour.a);

            return Color.Lerp(colour, muted, Mix);
        }
    }
}
