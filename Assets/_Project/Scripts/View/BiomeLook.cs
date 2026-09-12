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

        /// <summary>Whether anybody has built this country's scenery yet.</summary>
        public bool Dressed => Decor != null && !Decor.IsEmpty;
    }
}
