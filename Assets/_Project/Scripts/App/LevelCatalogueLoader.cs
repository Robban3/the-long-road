using TheVeil.Gen;
using UnityEngine;

namespace TheVeil.App
{
    /// <summary>
    /// Hands the level catalogue to the generator, once, before anything asks for a map.
    ///
    /// <b>A file reader and nothing else, because Gen is engine-free on purpose.</b> That
    /// is what lets the generator and the simulation be tested without opening Unity, and
    /// it is worth more than the convenience of calling Resources.Load where the table is
    /// parsed. So Gen.LevelCatalogue takes a string and knows nothing about where it came
    /// from, and this is the ten lines that know.
    ///
    /// Runs before the first scene loads, and again when the editor reloads its scripts —
    /// the tests and the reports come in that way and would otherwise search for every map
    /// from scratch.
    /// </summary>
    public static class LevelCatalogueLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            var asset = Resources.Load<TextAsset>(LevelCatalogue.Asset);

            if (asset == null)
            {
                // Not a fault in itself: without a table every level searches for its own
                // map, which is what happened before there was one. Worth saying, because
                // the difference is minutes.
                Debug.Log("[The Veil] No level catalogue, so every level will search for "
                          + "its map. Build one: The Veil > Build Level Catalogue.");
                return;
            }

            LevelCatalogue.Load(asset.text);

            if (LevelCatalogue.Refused != null)
                Debug.LogWarning("[The Veil] " + LevelCatalogue.Refused);
        }

#if UNITY_EDITOR
        /// <summary>
        /// And on a script reload, which is how the editor and every headless report and
        /// test arrive. Without it the catalogue is only read when a game starts, and the
        /// suite pays the full search it exists to avoid.
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void LoadInTheEditor() => Load();
#endif
    }
}
