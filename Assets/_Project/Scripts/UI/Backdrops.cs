using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheVail.UI
{
    /// <summary>
    /// The painted backdrops the screens stand on.
    ///
    /// Loaded by name at run time from a Resources folder rather than wired into the
    /// scenes as serialized fields, and that is a deliberate choice against this
    /// project's own habit. Everything else the scenes use — the models, the scenery, the
    /// materials — is a serialized field that `TheVail → Refresh Scene Assets` fills in, and
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
        public const string Menu = "TheVailBackdrop";
        public const string Roadmap = "TheVailRoadmap";
        public const string Shop = "TheVailShop";
        public const string Victory = "TheVailVictory";
        public const string Defeat = "TheVailDefeat";

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

                Debug.Log($"[The Vail] Backdrop '{name}' found, {sprite.rect.width:0}×"
                          + $"{sprite.rect.height:0} px.");
            }
            else
            {
                Debug.Log($"[The Vail] No backdrop '{name}'. Put {name}.png in "
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
        /// <summary>Letters and digits only, lowercased. "TheVail Backdrop 1" and "thevailbackdrop" meet here.</summary>
        static string Plain(string name)
        {
            var text = new System.Text.StringBuilder(name.Length);

            foreach (char c in name)
                if (char.IsLetterOrDigit(c)) text.Append(char.ToLowerInvariant(c));

            return text.ToString();
        }

        /// <summary>
        /// A file's name with every image extension taken off, not only the last one.
        ///
        /// Windows Explorer hides known extensions by default. Save a file you have called
        /// "TheVailShop.png" into a folder where ".png" is hidden and what lands on disk is
        /// TheVailShop.png.png. Unity strips one extension, names the resource "TheVailShop.png",
        /// and Resources.Load("TheVailShop") finds nothing — which is the whole reason the
        /// shop kept showing the castle. "TheVailBackdrop..png" is the same slip one step
        /// further, and it is the one that happened to survive the match below, which is
        /// why exactly one painting out of four worked and nothing said why.
        ///
        /// Shared with the importer that renames such a file (TheVail.Editor.BackdropImporter)
        /// so the two agree on what the name was meant to be.
        /// </summary>
        public static string Bare(string path)
        {
            string file = System.IO.Path.GetFileName(path);

            while (true)
            {
                string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();

                if (extension != ".png" && extension != ".jpg"
                    && extension != ".jpeg" && extension != ".") break;

                file = file.Substring(0, file.Length - extension.Length);
            }

            return file;
        }

        static Sprite Search(string name)
        {
            string wanted = Plain(name);

            // No folder is not the end of the search — the file may be somewhere else
            // in the project, which is its own thing to say.
            if (!System.IO.Directory.Exists(Folder))
            {
                Debug.Log($"[The Vail] {Folder} does not exist.");
                return Elsewhere(name, wanted);
            }

            string trimmed = wanted.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

            var listed = new List<string>();

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileName(path);
                string bare = Bare(path);

                // The real filename, doubled extension and all — the one thing that
                // settles this in a glance.
                listed.Add(file);

                string plain = Plain(bare);
                if (plain != wanted && plain.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9') != trimmed)
                    continue;

                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path)
                             ?? FromTexture(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path));

                if (sprite == null) continue;

                Debug.LogWarning($"[The Vail] Using '{file}' for the '{name}' backdrop. Rename the "
                                 + $"file to {name}.png and it will load in a build too — "
                                 + "this loose match is editor-only.");
                return sprite;
            }

            if (listed.Count > 0)
                Debug.Log($"[The Vail] {Folder} holds: {string.Join(", ", listed)}. "
                          + $"None of them is '{name}'.");
            else
                Debug.Log($"[The Vail] {Folder} holds no images at all.");

            return Elsewhere(name, wanted);
        }

        /// <summary>
        /// The last place to look: anywhere in the project, by name.
        ///
        /// A painting saved to the wrong folder is the one remaining way to have the file
        /// and not have the picture, and it looks from the screen exactly like the other
        /// ways. Asking the asset database for the name is cheap and targeted — it is a
        /// filename query, not a sweep of two Synty packs — and it runs only when a
        /// backdrop is already missing.
        ///
        /// It reports rather than merely coping. Loading from outside a Resources folder
        /// works in the editor and cannot work in a build, so the console says where the
        /// file is and where it belongs.
        /// </summary>
        static Sprite Elsewhere(string name, string wanted)
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

                if (path.StartsWith(Folder, System.StringComparison.Ordinal)) continue;
                if (Plain(Bare(path)) != wanted) continue;

                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path)
                             ?? FromTexture(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path));

                if (sprite == null) continue;

                Debug.LogWarning($"[The Vail] Found '{name}' at {path}, outside {Folder}. "
                                 + "Using it for now — move the file into that folder and "
                                 + "it will work in a build too.");
                return sprite;
            }

            return null;
        }
#endif

        public static bool Has(string name) => Find(name) != null;

        /// <summary>
        /// What is actually in the backdrop folder, as a sentence.
        ///
        /// Put on the screen beside a missing painting, because after three rounds of
        /// "the picture does not change" the one thing nobody has been able to establish
        /// is what the folder holds. A name read off the screen settles in one look what
        /// a console line has failed to settle three times: whether the file is missing,
        /// or there under a name nothing is looking for.
        /// </summary>
        public static string Inventory()
        {
#if UNITY_EDITOR
            if (!System.IO.Directory.Exists(Folder)) return "mappen finns inte";

            var listed = new List<string>();

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                listed.Add(System.IO.Path.GetFileName(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));

            return listed.Count == 0 ? "mappen är tom" : "mappen innehåller: " + string.Join(", ", listed);
#else
            return string.Empty;
#endif
        }

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

            // A phone-shaped column, not the whole canvas.
            //
            // The scaler is driven by height, so a window wider than a phone is wider in
            // *design units* too — a 16:9 editor Game view is 3413 units across. A tall
            // painting covering that has to be blown up nearly four times before its
            // edges reach, and what is left on screen is a small square out of the middle
            // of it. That is the "the pictures are enormous", and it is a symptom of the
            // window, not of the paintings.
            //
            // Held to the design's own 1080 the painting stays the size it was painted
            // at whatever shape the window is. On a phone the canvas is 1080 units wide
            // at most — 864 on a 20:9 — so the column is at least the screen's width and
            // the painting still covers it edge to edge, which is the case that matters.
            var frame = Widgets.Node("Backdrop", parent);
            frame.anchorMin = new Vector2(0.5f, 0f);
            frame.anchorMax = new Vector2(0.5f, 1f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.offsetMin = new Vector2(-Widgets.Reference.x * 0.5f, 0f);
            frame.offsetMax = new Vector2(Widgets.Reference.x * 0.5f, 0f);

            // The crop has to stop at the column's edge or the painting spills across the
            // sky it is supposed to be standing in front of.
            frame.gameObject.AddComponent<RectMask2D>();

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
                                "TheVailShade"));

            shade.type = Image.Type.Sliced;
            shade.raycastTarget = false;
            shade.rectTransform.Fill();
        }
    }
}
