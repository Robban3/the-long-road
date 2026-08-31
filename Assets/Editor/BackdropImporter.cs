using UnityEditor;

namespace Arna.Editor
{
    /// <summary>
    /// Sets the import settings on anything dropped into the backdrops folder.
    ///
    /// A PNG added to a 3D project arrives as a *texture*, not a sprite, and at a
    /// resolution Unity has quietly halved to 2048 — so a painting dropped in and
    /// expected to appear turns up soft, or as the wrong kind of asset, and the reason
    /// is in an inspector nobody thought to open. The loader copes with either kind
    /// (see Arna.UI.Backdrops), and this makes it moot: a file in that folder is
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
    }
}
