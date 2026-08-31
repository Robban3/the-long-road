using UnityEngine;

namespace Arna.UI
{
    /// <summary>
    /// Paints the menu's sprites into textures at run time (see <see cref="Theme"/> for
    /// why the menu has no imported art).
    ///
    /// Everything is drawn through <see cref="Shape"/>: a function from a point to how
    /// much of it is inside the shape. Sampling that at four points per pixel is enough
    /// anti-aliasing that a circle looks like a circle at menu size, and it means a
    /// star, a heart and a padlock are each a few lines of geometry rather than a PNG
    /// somebody has to keep.
    /// </summary>
    public static class Pixels
    {
        /// <summary>Coverage of the pixel at (x, y), 0 outside and 1 inside.</summary>
        public delegate float Shape(float x, float y);

        /// <summary>
        /// Canvas reference is 100 pixels per unit, so sprites made at 100 draw their
        /// nine-slice borders at exactly the pixel widths they were painted at.
        /// </summary>
        public const float PixelsPerUnit = 100f;

        static Texture2D Canvas(int width, int height, string name, bool tiling = false)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            texture.SetPixels32(pixels);

            return texture;
        }

        /// <summary>Lays one shape over whatever is already in the texture.</summary>
        static void Draw(Texture2D texture, Shape shape, Color colour)
        {
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float coverage = 0.25f * (shape(x + 0.25f, y + 0.25f) + shape(x + 0.75f, y + 0.25f)
                                            + shape(x + 0.25f, y + 0.75f) + shape(x + 0.75f, y + 0.75f));
                    if (coverage <= 0f) continue;

                    float alpha = colour.a * Mathf.Clamp01(coverage);
                    var under = texture.GetPixel(x, y);
                    var over = new Color(
                        colour.r * alpha + under.r * under.a * (1f - alpha),
                        colour.g * alpha + under.g * under.a * (1f - alpha),
                        colour.b * alpha + under.b * under.a * (1f - alpha),
                        alpha + under.a * (1f - alpha));

                    if (over.a > 0.0001f)
                    {
                        over.r /= over.a;
                        over.g /= over.a;
                        over.b /= over.a;
                    }

                    texture.SetPixel(x, y, over);
                }
            }
        }

        static Sprite Make(Texture2D texture, Vector4 border)
        {
            texture.Apply(false, false);

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                                       new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                                       SpriteMeshType.FullRect, border);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            return sprite;
        }

        static Shape Rect(float x0, float y0, float x1, float y1)
            => (x, y) => x >= x0 && x < x1 && y >= y0 && y < y1 ? 1f : 0f;

        static Shape Disc(float cx, float cy, float radius)
            => (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                return dx * dx + dy * dy <= radius * radius ? 1f : 0f;
            };

        static Shape Ring(float cx, float cy, float outer, float inner)
            => (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float d2 = dx * dx + dy * dy;
                return d2 <= outer * outer && d2 >= inner * inner ? 1f : 0f;
            };

        /// <summary>A rectangle with rounded corners, given the corner radius.</summary>
        static Shape Rounded(float x0, float y0, float x1, float y1, float radius)
            => (x, y) =>
            {
                if (x < x0 || x >= x1 || y < y0 || y >= y1) return 0f;

                float cx = Mathf.Clamp(x, x0 + radius, x1 - radius);
                float cy = Mathf.Clamp(y, y0 + radius, y1 - radius);
                float dx = x - cx, dy = y - cy;

                return dx * dx + dy * dy <= radius * radius ? 1f : 0f;
            };

        static Shape Polygon(Vector2[] points)
            => (x, y) =>
            {
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    if ((points[i].y > y) == (points[j].y > y)) continue;

                    float cross = (points[j].x - points[i].x) * (y - points[i].y)
                                / (points[j].y - points[i].y) + points[i].x;
                    if (x < cross) inside = !inside;
                }

                return inside ? 1f : 0f;
            };

        /// <summary>4×4 white. Tint it at the Image and it is any flat colour you like.</summary>
        public static Sprite Solid(Color colour, string name)
        {
            var texture = Canvas(4, 4, name);
            Draw(texture, Rect(0, 0, 4, 4), colour);
            return Make(texture, default);
        }

        /// <summary>
        /// The nine-sliced panel: black outline, gold edge, black shadow line, fill.
        ///
        /// Painted at 48 square with a 12-pixel border, so the edge keeps its weight at
        /// any size the layout gives it and only the flat middle is stretched.
        /// </summary>
        public static Sprite Frame(Color fill, Color edge, string name, bool thin = false)
        {
            const int size = 48;
            int border = thin ? 8 : 12;
            var texture = Canvas(size, size, name);

            var outline = new Color(0f, 0f, 0f, 0.85f);
            float e = thin ? 2f : 3f;

            Draw(texture, Rounded(0, 0, size, size, thin ? 4f : 6f), outline);
            Draw(texture, Rounded(2, 2, size - 2, size - 2, thin ? 3f : 5f), edge);
            Draw(texture, Rounded(2 + e, 2 + e, size - 2 - e, size - 2 - e, 3f),
                 new Color(0f, 0f, 0f, 0.7f));
            Draw(texture, Rounded(3 + e, 3 + e, size - 3 - e, size - 3 - e, 2f), fill);

            return Make(texture, new Vector4(border, border, border, border));
        }

        /// <summary>
        /// The chapter and result banner: a red ribbon with notched ends.
        ///
        /// The notch is cut into the border region, which a sliced image never stretches,
        /// so the ribbon keeps its shape at any width.
        /// </summary>
        public static Sprite Banner(string name)
        {
            const int width = 64, height = 40;
            var texture = Canvas(width, height, name);

            Draw(texture, Rect(0, 4, width, height - 4), new Color(0f, 0f, 0f, 0.75f));
            Draw(texture, Rect(0, 6, width, height - 6), Theme.Ribbon);
            Draw(texture, Rect(0, height - 9, width, height - 7),
                 new Color(1f, 1f, 1f, 0.12f));

            // The swallowtail: a wedge taken out of each end.
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int x = 0; x < 10; x++)
            {
                float cut = 10 - x;
                for (int y = 0; y < height; y++)
                {
                    bool outside = Mathf.Abs(y - (height - 1) * 0.5f) > height * 0.5f - cut * 0.9f;
                    if (!outside) continue;

                    texture.SetPixel(x, y, clear);
                    texture.SetPixel(width - 1 - x, y, clear);
                }
            }

            return Make(texture, new Vector4(14, 6, 14, 6));
        }

        /// <summary>The medallion a level number sits on in the roadmap.</summary>
        public static Sprite Medallion(string name)
        {
            const int size = 96;
            float c = size * 0.5f;
            var texture = Canvas(size, size, name);

            Draw(texture, Disc(c, c, c - 1f), new Color(0f, 0f, 0f, 0.9f));
            Draw(texture, Ring(c, c, c - 2f, c - 8f), Theme.Gold);
            Draw(texture, Ring(c, c, c - 3f, c - 5f), Theme.BrightGold);
            Draw(texture, Disc(c, c, c - 8f), new Color32(0x33, 0x25, 0x19, 0xFF));
            Draw(texture, Ring(c, c, c - 9f, c - 11f), new Color(0f, 0f, 0f, 0.5f));

            return Make(texture, default);
        }

        /// <summary>One paving stone of the path that links the levels.</summary>
        public static Sprite Slab(string name)
        {
            const int width = 40, height = 26;
            var texture = Canvas(width, height, name);

            Draw(texture, Rounded(0, 0, width, height, 7f), new Color(0f, 0f, 0f, 0.55f));
            Draw(texture, Rounded(1.5f, 1.5f, width - 1.5f, height - 1.5f, 6f),
                 new Color32(0x7A, 0x76, 0x6C, 0xFF));
            Draw(texture, Rounded(3f, 4f, width - 3f, height - 2.5f, 5f),
                 new Color32(0x99, 0x95, 0x8A, 0xFF));

            return Make(texture, default);
        }

        public static Sprite Star(string name)
        {
            const int size = 48;
            var texture = Canvas(size, size, name);
            var points = new Vector2[10];

            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = (i & 1) == 0 ? size * 0.47f : size * 0.20f;
                points[i] = new Vector2(size * 0.5f + Mathf.Cos(angle) * radius,
                                        size * 0.5f + Mathf.Sin(angle) * radius);
            }

            Draw(texture, Polygon(points), Color.white);
            return Make(texture, default);
        }

        public static Sprite Padlock(string name)
        {
            const int size = 48;
            var texture = Canvas(size, size, name);

            Draw(texture, Ring(size * 0.5f, size * 0.56f, size * 0.30f, size * 0.20f), Color.white);
            Draw(texture, Rect(0, 0, size, size * 0.56f), new Color(0f, 0f, 0f, 0f));
            Draw(texture, Rounded(size * 0.22f, size * 0.14f, size * 0.78f, size * 0.60f, 4f), Color.white);

            return Make(texture, default);
        }

        public static Sprite Coin(string name)
        {
            const int size = 40;
            float c = size * 0.5f;
            var texture = Canvas(size, size, name);

            Draw(texture, Disc(c, c, c - 1f), new Color(0f, 0f, 0f, 0.6f));
            Draw(texture, Disc(c, c, c - 2f), Theme.Coin);
            Draw(texture, Ring(c, c, c - 4f, c - 6f), new Color(0f, 0f, 0f, 0.25f));

            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = (i & 1) == 0 ? size * 0.24f : size * 0.10f;
                points[i] = new Vector2(c + Mathf.Cos(angle) * radius, c + Mathf.Sin(angle) * radius);
            }

            Draw(texture, Polygon(points), new Color(1f, 1f, 1f, 0.75f));
            return Make(texture, default);
        }

        public static Sprite Gem(string name)
        {
            const int size = 40;
            var texture = Canvas(size, size, name);

            var body = new[]
            {
                new Vector2(size * 0.5f, size * 0.94f),
                new Vector2(size * 0.92f, size * 0.60f),
                new Vector2(size * 0.5f, size * 0.06f),
                new Vector2(size * 0.08f, size * 0.60f)
            };

            Draw(texture, Polygon(body), new Color(0f, 0f, 0f, 0.6f));
            Draw(texture, Polygon(new[]
            {
                new Vector2(size * 0.5f, size * 0.86f),
                new Vector2(size * 0.84f, size * 0.60f),
                new Vector2(size * 0.5f, size * 0.14f),
                new Vector2(size * 0.16f, size * 0.60f)
            }), Theme.Gem);

            Draw(texture, Polygon(new[]
            {
                new Vector2(size * 0.5f, size * 0.86f),
                new Vector2(size * 0.5f, size * 0.14f),
                new Vector2(size * 0.16f, size * 0.60f)
            }), new Color(1f, 1f, 1f, 0.22f));

            return Make(texture, default);
        }

        public static Sprite Heart(string name)
        {
            const int size = 40;
            var texture = Canvas(size, size, name);

            Shape heart = (x, y) =>
            {
                float u = (x / size - 0.5f) * 2.3f;
                float v = (0.86f - y / size) * 2.3f;
                float t = u * u + v * v - 1f;

                return t * t * t - u * u * v * v * v <= 0f ? 1f : 0f;
            };

            Draw(texture, heart, Theme.Heart);
            return Make(texture, default);
        }

        public static Sprite Skull(string name)
        {
            const int size = 40;
            var texture = Canvas(size, size, name);

            Draw(texture, Disc(size * 0.5f, size * 0.58f, size * 0.34f), Theme.Bone);
            Draw(texture, Rounded(size * 0.30f, size * 0.14f, size * 0.70f, size * 0.50f, 4f), Theme.Bone);

            var socket = new Color(0f, 0f, 0f, 0.85f);
            Draw(texture, Disc(size * 0.37f, size * 0.60f, size * 0.10f), socket);
            Draw(texture, Disc(size * 0.63f, size * 0.60f, size * 0.10f), socket);
            Draw(texture, Polygon(new[]
            {
                new Vector2(size * 0.5f, size * 0.50f),
                new Vector2(size * 0.56f, size * 0.38f),
                new Vector2(size * 0.44f, size * 0.38f)
            }), socket);
            Draw(texture, Rect(size * 0.45f, size * 0.14f, size * 0.55f, size * 0.26f), socket);

            return Make(texture, default);
        }

        /// <summary>The back arrow. Points left; rotate the Image for the other three.</summary>
        public static Sprite Chevron(string name)
        {
            const int size = 40;
            var texture = Canvas(size, size, name);

            Draw(texture, Polygon(new[]
            {
                new Vector2(size * 0.62f, size * 0.86f),
                new Vector2(size * 0.20f, size * 0.50f),
                new Vector2(size * 0.62f, size * 0.14f),
                new Vector2(size * 0.78f, size * 0.28f),
                new Vector2(size * 0.52f, size * 0.50f),
                new Vector2(size * 0.78f, size * 0.72f)
            }), Color.white);

            return Make(texture, default);
        }

        public static Sprite Gear(string name)
        {
            const int size = 44;
            float c = size * 0.5f;
            var texture = Canvas(size, size, name);

            Draw(texture, Disc(c, c, size * 0.32f), Color.white);

            for (int tooth = 0; tooth < 8; tooth++)
            {
                float angle = tooth * Mathf.PI * 0.25f;
                float tx = c + Mathf.Cos(angle) * size * 0.38f;
                float ty = c + Mathf.Sin(angle) * size * 0.38f;
                Draw(texture, Disc(tx, ty, size * 0.09f), Color.white);
            }

            Draw(texture, Disc(c, c, size * 0.13f), new Color(0f, 0f, 0f, 0f));
            return Make(texture, default);
        }

        /// <summary>
        /// Deterministic value noise, so the ground is the same ground every time it is
        /// painted. The same trick <see cref="Arna.Sim.DeterministicRandom"/> uses, and
        /// for the same reason: a texture that differs between two runs is a texture
        /// nobody can compare a screenshot of.
        /// </summary>
        static float Noise(int x, int y, int seed)
        {
            int h = x * 374761393 + y * 668265263 + seed * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;

            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        /// <summary>
        /// Forest floor: a tiling mottle of greens with darker clumps in it.
        ///
        /// Tiled rather than stretched, so the grain stays the same size however tall the
        /// chapter's board is — a stretched texture on a board twice the height of the
        /// screen is a smear, which is the same mistake as a flat prop on a hillside and
        /// has been made in this project three times already.
        /// </summary>
        public static Sprite Ground(string name)
        {
            const int size = 96;
            var texture = Canvas(size, size, name, tiling: true);

            var dark = new Color32(0x2A, 0x3A, 0x22, 0xFF);
            var mid = new Color32(0x36, 0x49, 0x2A, 0xFF);
            var light = new Color32(0x41, 0x57, 0x31, 0xFF);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Two scales: broad patches of lighter and darker turf, and a fine
                    // grain over the top. One alone reads as either a blur or as static.
                    float broad = Noise(x / 12, y / 12, 7) * 0.6f
                                + Noise(x / 6, y / 6, 19) * 0.4f;
                    float grain = Noise(x, y, 41);

                    var colour = Color.Lerp(dark, mid, Mathf.Clamp01(broad * 1.4f));
                    colour = Color.Lerp(colour, light, grain * 0.18f);

                    texture.SetPixel(x, y, colour);
                }
            }

            return Make(texture, default);
        }

        /// <summary>
        /// A spruce, seen from the side: three skirts of needles over a short trunk.
        ///
        /// Silhouettes rather than drawings. At the size a tree is on this board — about
        /// a hundred and thirty pixels — shape is all that survives, and a shape that
        /// reads as a conifer next to one that reads as a broadleaf is what makes a
        /// scattering of them read as a wood rather than as clip art.
        /// </summary>
        public static Sprite Conifer(string name)
        {
            const int width = 72, height = 112;
            var texture = Canvas(width, height, name);

            var trunk = new Color32(0x3A, 0x2A, 0x1C, 0xFF);
            var needle = new Color32(0x22, 0x33, 0x1E, 0xFF);
            var lit = new Color32(0x33, 0x4A, 0x2A, 0xFF);

            Draw(texture, Rect(width * 0.44f, 0f, width * 0.56f, height * 0.22f), trunk);

            // Three skirts, each narrower and shorter than the one below it.
            for (int tier = 0; tier < 3; tier++)
            {
                float bottom = height * (0.14f + tier * 0.24f);
                float top = height * (0.48f + tier * 0.26f);
                float half = width * (0.5f - tier * 0.11f);

                Draw(texture, Polygon(new[]
                {
                    new Vector2(width * 0.5f, top),
                    new Vector2(width * 0.5f + half, bottom),
                    new Vector2(width * 0.5f - half, bottom)
                }), needle);

                // The light comes from the upper left, as it does everywhere else in this
                // interface. One wedge is enough to stop the silhouette reading as a hole.
                Draw(texture, Polygon(new[]
                {
                    new Vector2(width * 0.5f, top),
                    new Vector2(width * 0.5f, bottom),
                    new Vector2(width * 0.5f - half * 0.72f, bottom)
                }), new Color(lit.r / 255f, lit.g / 255f, lit.b / 255f, 0.55f));
            }

            return Make(texture, default);
        }

        /// <summary>A round-crowned tree, for the minority that punctuates the conifers.</summary>
        public static Sprite Broadleaf(string name)
        {
            const int width = 72, height = 84;
            var texture = Canvas(width, height, name);

            var trunk = new Color32(0x3A, 0x2A, 0x1C, 0xFF);
            var leaf = new Color32(0x2C, 0x40, 0x24, 0xFF);
            var lit = new Color32(0x40, 0x59, 0x30, 0xFF);

            Draw(texture, Rect(width * 0.45f, 0f, width * 0.55f, height * 0.34f), trunk);

            Draw(texture, Disc(width * 0.5f, height * 0.58f, width * 0.34f), leaf);
            Draw(texture, Disc(width * 0.30f, height * 0.48f, width * 0.22f), leaf);
            Draw(texture, Disc(width * 0.70f, height * 0.50f, width * 0.22f), leaf);
            Draw(texture, Disc(width * 0.40f, height * 0.68f, width * 0.20f),
                 new Color(lit.r / 255f, lit.g / 255f, lit.b / 255f, 0.6f));

            return Make(texture, default);
        }

        /// <summary>Undergrowth. What stops a wood being trunks standing in a lawn.</summary>
        public static Sprite Shrub(string name)
        {
            const int width = 56, height = 40;
            var texture = Canvas(width, height, name);

            var leaf = new Color32(0x28, 0x3B, 0x21, 0xFF);

            Draw(texture, Disc(width * 0.32f, height * 0.40f, width * 0.26f), leaf);
            Draw(texture, Disc(width * 0.62f, height * 0.44f, width * 0.30f), leaf);
            Draw(texture, Disc(width * 0.48f, height * 0.62f, width * 0.24f),
                 new Color(0.24f, 0.34f, 0.19f, 0.8f));

            return Make(texture, default);
        }

        /// <summary>A boulder beside the path.</summary>
        public static Sprite Boulder(string name)
        {
            const int width = 56, height = 44;
            var texture = Canvas(width, height, name);

            Draw(texture, Rounded(2f, 2f, width - 2f, height * 0.72f, 12f),
                 new Color32(0x4A, 0x4A, 0x44, 0xFF));
            Draw(texture, Rounded(6f, height * 0.28f, width - 10f, height * 0.78f, 10f),
                 new Color32(0x5E, 0x5E, 0x56, 0xFF));

            return Make(texture, default);
        }

        /// <summary>
        /// Packs several painted sprites into one texture, and hands back sprites cut
        /// from it.
        ///
        /// **A draw-call fix, and a necessary one.** The roadmap scatters something like
        /// three hundred trees, shrubs, boulders and paving stones. UGUI batches adjacent
        /// images only when they share a texture, so as five separate textures that board
        /// costs three hundred draw calls on its own — twice the whole game's budget of
        /// 150 (docs/technical-design.md). Packed together and drawn in one run of the
        /// hierarchy, the same board is one.
        ///
        /// The parts are painted first and copied in afterwards rather than painted into
        /// the atlas directly, so each shape's own code stays a drawing of that shape at
        /// its own origin, with no offset arithmetic threaded through it.
        /// </summary>
        public static Sprite[] Pack(string name, params Sprite[] parts)
        {
            const int pad = 2;

            // Shelf packing, which is enough for a handful of parts of similar height and
            // is about ten lines rather than a hundred.
            int width = 0, height = 0, shelf = 0, x = 0, y = 0;

            foreach (var part in parts)
            {
                int w = (int)part.rect.width + pad;
                int h = (int)part.rect.height + pad;

                if (x + w > 256) { y += shelf; x = 0; shelf = 0; }

                x += w;
                if (h > shelf) shelf = h;
                if (x > width) width = x;
            }

            height = y + shelf;

            var atlas = Canvas(Mathf.NextPowerOfTwo(width), Mathf.NextPowerOfTwo(height), name);
            var cut = new Sprite[parts.Length];

            x = 0; y = 0; shelf = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                int w = (int)part.rect.width;
                int h = (int)part.rect.height;

                if (x + w + pad > 256) { y += shelf; x = 0; shelf = 0; }

                atlas.SetPixels(x, y, w, h, part.texture.GetPixels());

                cut[i] = Sprite.Create(atlas, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f),
                                       PixelsPerUnit, 0, SpriteMeshType.FullRect);
                cut[i].name = part.name;
                cut[i].hideFlags = HideFlags.HideAndDontSave;

                x += w + pad;
                if (h + pad > shelf) shelf = h + pad;

                // The part's own texture has done its job. Left alone they are a handful
                // of small leaks, which is not much and is not nothing.
                var spent = part.texture;
                Object.DestroyImmediate(part, true);
                Object.DestroyImmediate(spent, true);
            }

            atlas.Apply(false, false);
            return cut;
        }

        /// <summary>
        /// The dark closing in at the edges of the board.
        ///
        /// Nine-sliced, so the darkness stays the same thickness whatever size the board
        /// is: stretched as one image, a tall chapter would get a soft haze and a short
        /// one a black frame. The middle slice is fully transparent, so it costs nothing
        /// over the part of the picture the player is actually reading.
        /// </summary>
        public static Sprite Vignette(string name)
        {
            const int size = 96;
            var texture = Canvas(size, size, name);

            var edge = new Color(0.02f, 0.03f, 0.02f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the nearest edge, in border widths. One at the very
                    // edge, nothing by the time it reaches the middle slice.
                    float near = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float t = Mathf.Clamp01(1f - near / 34f);

                    texture.SetPixel(x, y, new Color(edge.r, edge.g, edge.b, t * t * 0.88f));
                }
            }

            return Make(texture, new Vector4(36, 36, 36, 36));
        }

        /// <summary>A vertical two-colour wash, for screen backdrops.</summary>
        public static Sprite Gradient(Color top, Color bottom, string name)
        {
            const int height = 64;
            var texture = Canvas(4, height, name);

            for (int y = 0; y < height; y++)
            {
                var colour = Color.Lerp(bottom, top, y / (height - 1f));
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, colour);
            }

            return Make(texture, new Vector4(0, 2, 0, 2));
        }
    }
}
