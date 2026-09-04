using UnityEditor;
using UnityEngine;

namespace TheVail.Editor
{
    /// <summary>
    /// Sets the import settings on anything dropped into the backdrops folder, and
    /// straightens out a doubled filename before it can cost anybody an afternoon.
    ///
    /// A PNG added to a 3D project arrives as a *texture*, not a sprite, and at a
    /// resolution Unity has quietly halved to 2048 — so a painting dropped in and
    /// expected to appear turns up soft, or as the wrong kind of asset, and the reason
    /// is in an inspector nobody thought to open. The loader copes with either kind
    /// (see TheVail.UI.Backdrops), and this makes it moot: a file in that folder is
    /// imported as a full-size UI sprite, without anybody having to know that is a
    /// thing that needs doing.
    ///
    /// Scoped to the one folder on purpose. A rule that reached the whole project would
    /// re-import two Synty packs and every texture in them.
    /// </summary>
    public sealed class BackdropImporter : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Art/Resources/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            // The paintings are tall and are cropped to cover a phone screen, so the
            // height is what is actually seen and there is no point throwing it away.
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        /// <summary>
        /// Takes the doubled extension off a painting that has just arrived.
        ///
        /// Windows Explorer hides known extensions by default. Save a file you have
        /// called "TheVailShop.png" into a folder where ".png" is hidden and what lands on
        /// disk is TheVailShop.png.png. Unity strips one extension, so the resource is named
        /// "TheVailShop.png" and Resources.Load("TheVailShop") returns nothing — a screen that
        /// looks exactly the same as one with no painting at all, with nothing in the
        /// console to tell the two apart. That cost this project an afternoon across four
        /// paintings, three of which were named that way and one of which
        /// ("TheVailBackdrop..png") was named a near miss that happened to still be found.
        ///
        /// So the folder fixes its own filenames. Renaming and not merely coping is the
        /// point: a loose match in the editor leaves the build still broken, while a file
        /// that has been *renamed* is right everywhere from then on.
        /// </summary>
        static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                           string[] moved, string[] movedFrom)
        {
            foreach (string path in imported) Straighten(path);
            foreach (string path in moved) Straighten(path);
        }

        static void Straighten(string path)
        {
            if (!path.StartsWith(Folder, System.StringComparison.Ordinal)) return;

            string extension = System.IO.Path.GetExtension(path);
            string lowered = extension.ToLowerInvariant();

            if (lowered != ".png" && lowered != ".jpg" && lowered != ".jpeg") return;

            string file = System.IO.Path.GetFileName(path);
            string bare = TheVail.UI.Backdrops.Bare(path);
            string wanted = bare + extension;

            if (wanted == file) return;   // Already named the way it reads.

            string target = Folder + wanted;

            if (System.IO.File.Exists(target))
            {
                Debug.LogWarning($"[The Vail] '{file}' has a doubled extension and should be "
                                 + $"'{wanted}', but that file already exists. Delete "
                                 + "whichever of the two is the wrong picture.");
                return;
            }

            string failure = AssetDatabase.RenameAsset(path, bare);

            if (!string.IsNullOrEmpty(failure))
            {
                Debug.LogWarning($"[The Vail] Could not rename '{file}' to '{wanted}': {failure}");
                return;
            }

            Debug.Log($"[The Vail] Renamed '{file}' to '{wanted}'. Windows hides known file "
                      + "extensions, so a picture saved as \"" + bare + "\" lands on disk "
                      + $"as {file} and the game never finds it.");
        }
    }
}
