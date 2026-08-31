using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arna.UI
{
    /// <summary>
    /// The painted backdrops the screens stand on.
    ///
    /// Loaded by name at run time from a Resources folder rather than wired into the
    /// scenes as serialized fields, and that is a deliberate choice against this
    /// project's own habit. Everything else the scenes use — the models, the scenery, the
    /// materials — is a serialized field that `Arna → Refresh Scene Assets` fills in, and
    /// forgetting to run it has produced four separate false bug reports: the code
    /// changed, the saved scene did not, and nothing said so. A painting dropped in a
    /// folder and found by name has no such trap. Put the file there and it is in the
    /// game.
    ///
    /// Every one is optional. A screen with no painting draws what it drew before — a
    /// gradient sky on the front page, a scattered wood on the roadmap — so the interface
    /// is never waiting on art to be usable.
    /// </summary>
    public static class Backdrops
    {
        public const string Menu = "ArnaBackdrop";
        public const string Roadmap = "ArnaRoadmap";
        public const string Shop = "ArnaShop";
        public const string Victory = "ArnaVictory";
        public const string Defeat = "ArnaDefeat";

        /// <summary>Where the paintings live, for the editor's forgiving search and for saying so.</summary>
        public const string Folder = "Assets/_Project/Art/Resources";

        static readonly Dictionary<string, Sprite> _found = new Dictionary<string, Sprite>();

        /// <summary>
        /// The painting of that name, or null.
        ///
        /// Tried as a sprite first and as a plain texture second, because which of the two
        /// a PNG becomes is decided by its import settings and nobody should have to know
        /// that to add a picture to their own game. A texture is cut into a sprite here.
        /// </summary>
        public static Sprite Find(string name)
        {
            if (_found.TryGetValue(name, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(name);

            if (sprite == null) sprite = FromTexture(Resources.Load<Texture2D>(name));

#if UNITY_EDITOR
            // Nothing under that exact name. In the editor, look again without caring
            // about case, spaces, underscores or a trailing "1" — which is what a file
            // copied twice is called.
            //
            // Forgiving here and strict in a build, deliberately. A player's phone should
            // not be scanning folders, and by the time there is a build the file has a
            // name that works. This is for the half hour where somebody is trying to get
            // a painting into their own game and the only feedback is a screen that looks
            // exactly the same either way.
            if (sprite == null) sprite = Search(name);
#endif

            // **A miss is never cached, and that was the bug.**
            //
            // Statics survive leaving play mode when Unity's Enter Play Mode Options have
            // the domain reload switched off, which is the usual setting for fast
            // iteration. So: run the game once before the file exists, cache the null,
            // add the file, run again — and the dictionary answers null for the rest of
            // the editor session. It never looks again, and the console line saying the
            // file is missing never prints either, because that only ran on the first
            // lookup. The shop showed the castle for an hour with nothing on screen or in
            // the console to say why.
            //
            // A failed lookup is one Resources.Load returning null. There is nothing
            // there worth protecting with a cache, and the price of caching it is that
            // hour.
            if (sprite != null)
            {
                _found[name] = sprite;

                Debug.Log($"[Arna] Backdrop '{name}' found, {sprite.rect.width:0}×"
                          + $"{sprite.rect.height:0} px.");
            }
            else
            {
                Debug.Log($"[Arna] No backdrop '{name}'. Put {name}.png in "
                          + "Assets/_Project/Art/Resources to use one — the screen draws "
                          + "its own until then.");
            }

            return sprite;
        }

        /// <summary>
        /// Forgets what was found, so a painting replaced on disk is picked up.
        ///
        /// Run when play starts, which is the one moment a person might have swapped a
        /// file since the last look. Without the domain reload the sprites found in an
        /// earlier session are still cached and still point at the old texture.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Forget() => _found.Clear();

        static Sprite FromTexture(Texture2D texture)
        {
            if (texture == null) return null;

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                                 new Vector2(0.5f, 0.5f), Pixels.PixelsPerUnit);
        }

#if UNITY_EDITOR
        /// <summary>Letters and digits only, lowercased. "Arna Backdrop 1" and "arnabackdrop" meet here.</summary>
        static string Plain(string name)
        {
            var text = new System.Text.StringBuilder(name.Length);

            foreach (char c in name)
                if (char.IsLetterOrDigit(c)) text.Append(char.ToLowerInvariant(c));

            return text.ToString();
        }

        static Sprite Search(string name)
        {
            if (!System.IO.Directory.Exists(Folder)) return null;

            string wanted = Plain(name);
            string trimmed = wanted.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

            var listed = new List<string>();

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);

                listed.Add(file);

                string plain = Plain(file);
                if (plain != wanted && plain.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9') != trimmed)
                    continue;

                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path)
                             ?? FromTexture(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path));

                if (sprite == null) continue;

                Debug.LogWarning($"[Arna] Using '{file}' for the '{name}' backdrop. Rename the "
                                 + $"file to {name}.png and it will load in a build too — "
                                 + "this loose match is editor-only.");
                return sprite;
            }

            if (listed.Count > 0)
                Debug.Log($"[Arna] {Folder} holds: {string.Join(", ", listed)}. "
                          + $"None of them is '{name}'.");
            else
                Debug.Log($"[Arna] {Folder} holds no images at all.");

            return null;
        }
#endif

        public static bool Has(string name) => Find(name) != null;

        /// <summary>
        /// Lays a painting behind a screen, cropped to cover it.
        ///
        /// Cover and not fit: a painting letterboxed inside a phone screen is a postcard
        /// on a wall, and one stretched to the screen's shape is a painting somebody sat
        /// on. Whatever does not fit is cropped evenly from the edges, which is why the
        /// paintings are tall — a portrait image on a portrait screen loses least.
        ///
        /// Returns null when there is no such painting, so a caller can fall back to
        /// whatever it drew before by checking for it.
        /// </summary>
        public static Image Paint(string name, Transform parent, float darken = 0.4f)
        {
            var sprite = Find(name);
            if (sprite == null) return null;

            var frame = Widgets.Node("Backdrop", parent);
            frame.Fill();

            var image = Widgets.Panel("Painting", frame, sprite, Color.white);
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.preserveAspect = false;

            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

            image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = Vector2.zero;

            if (darken > 0f) Shade(frame, darken);

            return image;
        }

        /// <summary>
        /// Darkens the painting from the bottom up.
        ///
        /// The buttons are in the lower half of every one of these screens and the
        /// paintings are busiest there — a road, a workbench, a burning field. Without
        /// this the text sits on whatever the brush happened to leave underneath it, which
        /// is legible in some places and not in others, which is worse than either.
        /// </summary>
        static void Shade(Transform frame, float darken)
        {
            var shade = Widgets.Panel("Shade", frame,
                Pixels.Gradient(new Color(0f, 0f, 0f, 0f),
                                new Color(0.02f, 0.02f, 0.03f, Mathf.Clamp01(darken)),
                                "ArnaShade"));

            shade.type = Image.Type.Sliced;
            shade.raycastTarget = false;
            shade.rectTransform.Fill();
        }
    }
}
