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

            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(name);

                if (texture != null)
                    sprite = Sprite.Create(texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), Pixels.PixelsPerUnit);
            }

            _found[name] = sprite;

            // Said once per painting, because the alternative is what happened with the
            // menu scene: a file that was never found and a file that was never looked
            // for produce exactly the same screen, and no way to tell them apart.
            if (sprite != null)
                Debug.Log($"[Arna] Backdrop '{name}' found, {sprite.rect.width:0}×"
                          + $"{sprite.rect.height:0} px.");
            else
                Debug.Log($"[Arna] No backdrop '{name}'. Put {name}.png in "
                          + "Assets/_Project/Art/Resources to use one — the screen draws "
                          + "its own until then.");

            return sprite;
        }

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
