using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.View
{
    /// <summary>
    /// How one country looks: what stands on its ground, what its water is made of, and
    /// what falls out of its sky.
    ///
    /// <b>A list of these rather than a field per country.</b> There were two countries
    /// and the winter was an exception written three times over — a second decor set, an
    /// ice material, a snow effect, and a <c>bool winter</c> at each place that read them.
    /// Ten countries down that road is thirty fields and ten comparisons, and every one of
    /// them a place to forget one. A country is now an entry: setup fills in the ones it
    /// has built, and anything without an entry is drawn as the forest, which is the
    /// safe way to be unfinished — a marsh chapter plays over marsh ground under woodland
    /// trees rather than over bare earth.
    /// </summary>
    [System.Serializable]
    public sealed class BiomeLook
    {
        public Biome Biome;

        /// <summary>What stands on the ground here. Empty falls back to the forest's.</summary>
        public BiomeDecor Decor = new BiomeDecor();

        /// <summary>
        /// What the rivers and the pools are made of. Used for both, since a bog freezes
        /// the same way a river does and a fen is the same brown as its own puddles. Null
        /// keeps the summer water, which is the safe way to be wrong: a river that should
        /// be frozen still reads as a river.
        /// </summary>
        public Material Water;

        /// <summary>
        /// What falls out of the sky, hung on the camera. Snow in the winter; null leaves
        /// the sky still, which is most countries.
        /// </summary>
        public GameObject Weather;

        /// <summary>
        /// How thickly this country is grown, against the forest's one.
        ///
        /// A country is not only which models stand on it but how many: a fen is a
        /// thicket of dead trunks and roots where a wood is trees with floor between
        /// them, and the same scatter rate in both makes the fen read as a wood that
        /// lost its leaves. Multiplied into the decorator's own density, so the cap on
        /// how many props a level may carry still holds.
        /// </summary>
        public float Density = 1f;

        /// <summary>
        /// Standing air: fog, for a country that has any. Off leaves the sky clear, which
        /// is every country but the fen so far.
        /// </summary>
        public bool Fog;

        public Color FogColor = new Color(0.55f, 0.60f, 0.58f);

        /// <summary>Exponential-squared density. Small numbers: 0.01 is a haze, 0.05 is a wall.</summary>
        public float FogDensity = 0.012f;

        /// <summary>
        /// What the sky is, in a country that has fog.
        ///
        /// The fog closes the ground and the skybox went on being a bright summer blue
        /// over it, which reads as a clear day with a dirty lens rather than as weather.
        /// A flat sky the colour of the fog is what standing in one actually looks like:
        /// the horizon simply stops. Ignored where <see cref="Fog"/> is off, so every
        /// other country keeps the skybox.
        /// </summary>
        public Color SkyColor = new Color(0.55f, 0.60f, 0.58f);

        /// <summary>Whether anybody has built this country's scenery yet.</summary>
        public bool Dressed => Decor != null && !Decor.IsEmpty;
    }
}
