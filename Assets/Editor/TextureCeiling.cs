using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// How large each texture is allowed to be on a phone, decided by measurement:
    /// `The Veil &gt; Texture Plan` surveys and writes nothing, `The Veil &gt; Fit Textures
    /// to the Phone` writes the Android overrides.
    ///
    /// <b>Because 772 MB of texture has to become about a hundred, and "halve everything"
    /// is not a decision.</b> <see cref="TextureReport"/> found 133 textures at 2048 or
    /// 4096 square carrying 92% of the weight, and not one Android override in the
    /// project. Something has to come down. But a flat colour palette at 4096 square looks
    /// exactly the same at 256 and a hand-painted normal map does not, so a single ceiling
    /// applied to all of them either wastes most of the budget on palettes or ruins the
    /// few textures that were carrying detail. Which is which is a question about the
    /// images, and it can be asked of the images.
    ///
    /// <b>What is measured.</b> Halving an image replaces each group of four pixels with
    /// their average. What that costs is the amount those four disagreed - nothing at all
    /// inside a flat field of colour, a great deal across a painted edge. So each texture
    /// is box-halved repeatedly and the squared disagreement at every step is kept, which
    /// gives the error of representing the whole image at each size, in levels of 0-255.
    ///
    /// Those step errors add, exactly rather than roughly: within one block the parent is
    /// the mean of its children, so the children's deviations sum to zero and the cross
    /// terms between steps vanish. The error of going from full size down to any size is
    /// therefore the sum of the steps between, and one pass down the chain prices every
    /// candidate size at once.
    ///
    /// <b>What is decided.</b> Then it is an allocation and not a rule: every texture
    /// starts at full size, and the one whose next halving costs the least error per
    /// megabyte saved is halved, over and over, until the total fits the budget. The
    /// palettes fall to 128 before a detail map gives up its first halving, because that
    /// is what the arithmetic says, and nothing is cut past
    /// <see cref="MostError"/> however hungry the budget gets - a budget met by destroying
    /// four textures is not met.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.TextureCeiling.Plan
    ///           unity run . -- -executeMethod TheVeil.Editor.TextureCeiling.Fit
    /// </summary>
    public static class TextureCeiling
    {
        /// <summary>
        /// What the textures are allowed to weigh, as this counts them.
        ///
        /// Not a line from the technical design, which only has one for the package as a
        /// whole: 150 MB. This is worked backwards from the build that exists. That one
        /// packed 772 MB of texture, 201 MB of mesh and 34 MB of everything else into a
        /// 386,7 MB package, so the package is about 0,38 of what goes into it. Leaving
        /// the mesh alone, texture has to come to about 155 MB for the package to reach
        /// 150. A hundred, then, because the estimate is an estimate and the mesh is the
        /// next thing to be looked at, not a thing already settled.
        /// </summary>
        const float WantedMegabytes = 100f;

        /// <summary>
        /// The most error any single texture may be made to carry, in levels of 0-255.
        ///
        /// Six, which is two and a half per cent, and chosen off the curve the plan prints
        /// rather than guessed: one buys 117 MB, three buys 214, six buys 437, and twelve -
        /// which is where resolution alone would finally meet the package line - buys 643.
        /// The curve is steep from four to eight and then it is asking for real damage, so
        /// six is the last point on it that is cheap.
        ///
        /// It is still a number about how the game looks, and no instrument here can judge
        /// that, so the worst <see cref="Proofs"/> textures at this setting are written out
        /// side by side to be looked at. Textures that reach the ceiling stop coming down
        /// and the report names them, because a budget that cannot be met without passing
        /// this is a thing to be told about and not worked around quietly.
        /// </summary>
        const float MostError = 6f;

        /// <summary>Where the before-and-after crops are written. Outside the repository.</summary>
        const string ProofDir = "Build/TexturePlan";

        /// <summary>How many of the worst-hit textures are written out to be looked at.</summary>
        const int Proofs = 8;

        /// <summary>The side of the crop that gets written, at one pixel to one pixel.</summary>
        const int Crop = 512;

        /// <summary>No texture is taken below this, whatever its error says.</summary>
        const int Least = 64;

        /// <summary>
        /// The error ceilings the plan prices, so the one that is used is a point on a
        /// curve that was printed and not a number that appeared alone.
        /// </summary>
        static readonly float[] Ceilings = { 1f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 24f, 1000f };

        [MenuItem("The Veil/Texture Plan")]
        public static void Plan() { Work(false); }

        [MenuItem("The Veil/Fit Textures to the Phone")]
        public static void Fit() { Work(true); }

        static void Work(bool write)
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);

            if (scenes.Count == 0)
            {
                Debug.LogError("[Ceiling] no scenes are enabled, so nothing ships.");
                return;
            }

            var paths = new List<string>();
            var seen = new HashSet<string>();

            foreach (string path in AssetDatabase.GetDependencies(scenes.ToArray(), true))
                if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D)
                    && seen.Add(path))
                    paths.Add(path);

            var sheet = new StringBuilder();
            sheet.AppendLine($"[Ceiling] {paths.Count} textures ship; measuring what each "
                             + "one loses when it is halved");

            var fit = new List<Fitted>();
            long full = 0;

            var unread = new List<string>();
            long blind = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Texture Plan", paths[i],
                                                 (float)i / paths.Count);

                if (!Measure(paths[i], out Fitted one))
                {
                    unread.Add(paths[i]);

                    // What the hole in the measurement weighs, so it is a known hole.
                    var missed = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
                    if (missed != null)
                        blind += TextureReport.Weighs(missed.width, missed.height,
                                                      missed.format,
                                                      Mathf.Max(1, missed.mipmapCount));

                    continue;
                }

                fit.Add(one);
                full += one.Bytes[0];
            }

            EditorUtility.ClearProgressBar();

            if (fit.Count == 0)
            {
                Debug.LogError("[Ceiling] not one texture could be read, so there is "
                               + "nothing to decide.");
                return;
            }

            sheet.AppendLine($"[Ceiling] {full / 1048576f:0.0} MB at full size");

            if (unread.Count > 0)
            {
                sheet.AppendLine($"[Ceiling] {unread.Count} could not be read at all "
                                 + $"({blind / 1048576f:0.0} MB), so they are left alone and "
                                 + "this plan does not cover them:");

                for (int i = 0; i < unread.Count && i < 12; i++)
                    sheet.AppendLine($"[Ceiling]   {Short(unread[i])}");

                if (unread.Count > 12)
                    sheet.AppendLine($"[Ceiling]   and {unread.Count - 12} more");
            }

            long wanted = (long)(WantedMegabytes * 1048576f);

            // <b>The curve, not the single answer.</b> The first version of this reported
            // only what it reached at MostError, and what it reached was 557,9 MB against a
            // hundred - which reads as a wall. It is not a wall: of the 176 textures that
            // stopped there, 111 were within one more doubling of the threshold and the
            // middle one wanted 5,2 against a limit of 3,0. So the limit was what was
            // binding, and a limit that is binding is a thing to be chosen out loud rather
            // than tightened or loosened until the total looks the way one wanted.
            sheet.AppendLine("[Ceiling] what each error ceiling buys, worst texture to "
                             + "what it reaches:");

            foreach (float ceiling in Ceilings)
            {
                Spend(fit, full, wanted, ceiling, out long reach, out int took,
                      out bool held);

                sheet.AppendLine($"[Ceiling]   at {ceiling,5:0.0} levels   "
                                 + $"{reach / 1048576f,7:0.0} MB   {took,4} halvings   "
                                 + (reach <= wanted
                                        ? "within the budget"
                                        : held
                                            ? "all it can reach"
                                            : "budget met"));
            }

            Spend(fit, full, wanted, MostError, out long now, out int steps, out bool stuck);

            sheet.AppendLine($"[Ceiling] the plan below is the {MostError:0.0} row: {steps} "
                             + $"halvings to {now / 1048576f:0.0} MB against "
                             + $"{WantedMegabytes:0} MB  "
                             + (now <= wanted ? "within" : "SHORT"));

            if (stuck)
                sheet.AppendLine("[Ceiling] and there it stopped. Whatever the rows above "
                                 + "say, resolution alone does not reach the budget: the "
                                 + "formats and the number of separate atlases are where "
                                 + "the rest of it is.");

            Report(sheet, fit);

            sheet.AppendLine($"[Ceiling] the {Proofs} worst of them, written out to be "
                             + "looked at - original on the left, what the plan leaves on "
                             + "the right, one pixel to one pixel:");

            int shown = Show(fit, sheet);

            if (shown == 0)
                sheet.AppendLine("[Ceiling]   none could be written, which is worth knowing: "
                                 + "the plan then has no picture to argue with.");

            if (!write)
            {
                sheet.AppendLine("[Ceiling] nothing was written. The Veil > Fit Textures to "
                                 + "the Phone writes it.");
                Debug.Log(sheet.ToString());
                return;
            }

            int wrote = Apply(fit);

            sheet.AppendLine($"[Ceiling] {wrote} textures now carry an Android ceiling. "
                             + "Build again and read the package.");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// Spends the budget: every texture back to full size, then the cheapest halving in
        /// the whole project by error per megabyte, over and over, until the total fits or
        /// nothing is left that stays under <paramref name="most"/>.
        ///
        /// Cheapest by error per megabyte is the only comparison that treats a flat mask
        /// and a painted map as the different things they are. It is greedy and so not
        /// provably the best allocation, but each halving is priced against what it saves
        /// at the moment it is taken, and the alternative - one rule for all of them - is
        /// not an allocation at all.
        /// </summary>
        static void Spend(List<Fitted> fit, long full, long wanted, float most,
                          out long now, out int steps, out bool stuck)
        {
            for (int i = 0; i < fit.Count; i++)
            {
                var one = fit[i];
                one.Step = 0;
                fit[i] = one;
            }

            now = full;
            steps = 0;
            stuck = false;

            while (now > wanted)
            {
                int best = -1;
                float cheapest = float.MaxValue;

                for (int i = 0; i < fit.Count; i++)
                {
                    var one = fit[i];
                    int next = one.Step + 1;

                    if (next >= one.Bytes.Count) continue;
                    if (one.Error[next] > most) continue;

                    long saved = one.Bytes[one.Step] - one.Bytes[next];
                    if (saved <= 0) continue;

                    float cost = (one.Error[next] - one.Error[one.Step]) / (saved / 1048576f);

                    if (cost >= cheapest) continue;

                    cheapest = cost;
                    best = i;
                }

                if (best < 0) { stuck = true; return; }

                var chosen = fit[best];
                now -= chosen.Bytes[chosen.Step] - chosen.Bytes[chosen.Step + 1];
                chosen.Step++;
                fit[best] = chosen;
                steps++;
            }
        }

        /// <summary>
        /// Reads a texture's own file and prices every size it could be imported at.
        /// </summary>
        static bool Measure(string path, out Fitted one)
        {
            one = default;

            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (map == null) return false;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;

            if (!Pixels(path, map, out Color32[] pixels, out int wide, out int high))
                return false;

            // The file rather than the imported texture, so the measurement is of the
            // picture the artist made and not of whatever ceiling the import happens to be
            // at today. A ceiling above the source is no ceiling at all, so the chain
            // starts at whichever is smaller.
            int most = Mathf.Min(Mathf.Max(wide, high), importer.maxTextureSize);

            bool mips = importer.mipmapEnabled;

            var error = new List<float> { 0f };
            var bytes = new List<long>();
            var sizes = new List<int>();

            long first = Price(wide, high, most, map.format, mips);
            if (first <= 0) return false;

            bytes.Add(first);
            sizes.Add(most);

            var level = pixels;
            int lw = wide, lh = high;
            float carried = 0f;

            // A source larger than the ceiling it is already imported at has halvings in
            // it that have happened: those are not this plan's to charge for, and counting
            // them would put every later step against the wrong size.
            while (Mathf.Max(lw, lh) > most && lw > 1 && lh > 1)
            {
                Lost(level, lw, lh, out Color32[] already, out int aw, out int ah);
                level = already;
                lw = aw;
                lh = ah;
            }

            while (Mathf.Max(lw, lh) > Least && lw > 1 && lh > 1)
            {
                carried += Lost(level, lw, lh, out Color32[] half, out int hw, out int hh);

                level = half;
                lw = hw;
                lh = hh;

                int size = Mathf.Max(1, most >> error.Count);
                if (size < Least) break;

                long price = Price(wide, high, size, map.format, mips);
                if (price <= 0) break;

                error.Add(Mathf.Sqrt(carried));
                bytes.Add(price);
                sizes.Add(size);
            }

            one = new Fitted
            {
                Path = path,
                Step = 0,
                Error = error,
                Bytes = bytes,
                Sizes = sizes,
            };

            return true;
        }

        /// <summary>
        /// Box-halves a level and returns the squared disagreement it cost, as a mean over
        /// the pixels of the level that was halved - which, because every pixel of the
        /// original inherits its block's value, is also a mean over the original.
        /// </summary>
        static float Lost(Color32[] level, int wide, int high,
                          out Color32[] half, out int hw, out int hh)
        {
            hw = Mathf.Max(1, wide / 2);
            hh = Mathf.Max(1, high / 2);

            half = new Color32[hw * hh];

            double sum = 0d;
            long many = 0;

            for (int y = 0; y < hh; y++)
            {
                int top = y * 2 * wide;
                int bottom = Mathf.Min(y * 2 + 1, high - 1) * wide;

                for (int x = 0; x < hw; x++)
                {
                    int left = x * 2;
                    int right = Mathf.Min(x * 2 + 1, wide - 1);

                    var a = level[top + left];
                    var b = level[top + right];
                    var c = level[bottom + left];
                    var d = level[bottom + right];

                    int r = (a.r + b.r + c.r + d.r) / 4;
                    int g = (a.g + b.g + c.g + d.g) / 4;
                    int bl = (a.b + b.b + c.b + d.b) / 4;
                    int al = (a.a + b.a + c.a + d.a) / 4;

                    half[y * hw + x] = new Color32((byte)r, (byte)g, (byte)bl, (byte)al);

                    sum += Apart(a, r, g, bl, al) + Apart(b, r, g, bl, al)
                           + Apart(c, r, g, bl, al) + Apart(d, r, g, bl, al);

                    many += 4;
                }
            }

            return many > 0 ? (float)(sum / many) : 0f;
        }

        /// <summary>How far one pixel sits from its block's average, squared, over the
        /// four channels - so a colour that shifts and an alpha edge that softens both
        /// count, and neither counts four times.</summary>
        static double Apart(Color32 pixel, int r, int g, int b, int a)
        {
            double dr = pixel.r - r;
            double dg = pixel.g - g;
            double db = pixel.b - b;
            double da = pixel.a - a;

            return (dr * dr + dg * dg + db * db + da * da) * 0.25d;
        }

        /// <summary>What a texture weighs once a ceiling has scaled it.</summary>
        static long Price(int wide, int high, int ceiling, TextureFormat format, bool mips)
        {
            int longest = Mathf.Max(wide, high);
            if (longest <= 0) return 0;

            if (ceiling < longest)
            {
                float shrink = (float)ceiling / longest;
                wide = Mathf.Max(1, Mathf.RoundToInt(wide * shrink));
                high = Mathf.Max(1, Mathf.RoundToInt(high * shrink));
            }

            int levels = 1;
            if (mips)
            {
                int side = Mathf.Max(wide, high);
                while (side > 1) { side >>= 1; levels++; }
            }

            return TextureReport.Weighs(wide, high, format, levels);
        }

        /// <summary>
        /// The texture's own pixels at the size the artist saved it.
        ///
        /// From the file on disk where that can be done: it is readable whatever the import
        /// settings say, and at whatever size the artist saved. Unity reads PNG and JPEG
        /// that way and nothing else, and the first run of this found 39 textures - 113 MB,
        /// a seventh of the weight - that it could not open, which is a measurement with a
        /// hole in it rather than a measurement.
        ///
        /// So for those: the imported texture, copied through the graphics device, which
        /// works whatever the format and whether or not the asset is marked readable. It is
        /// the second-best picture, already capped at the import ceiling and already
        /// through a block compressor, and the compressor's own blocking is high-frequency
        /// detail that was never in the artist's file - so these read slightly worse than
        /// they are, and the error numbers for them are an overstatement rather than a
        /// flattering one.
        /// </summary>
        static bool Pixels(string path, Texture2D map,
                           out Color32[] pixels, out int wide, out int high)
        {
            pixels = null;
            wide = high = 0;

            Texture2D read = null;

            try
            {
                byte[] raw = System.IO.File.ReadAllBytes(path);

                read = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (read.LoadImage(raw, false))
                {
                    wide = read.width;
                    high = read.height;
                    pixels = read.GetPixels32();

                    return pixels != null && pixels.Length == wide * high;
                }
            }
            catch (System.Exception bad)
            {
                Debug.LogWarning($"[Ceiling] {path} could not be opened as a file: "
                                 + bad.Message);
            }
            finally
            {
                if (read != null) Object.DestroyImmediate(read);
            }

            return Copied(map, out pixels, out wide, out high);
        }

        /// <summary>
        /// The imported texture's pixels, taken by drawing it once into a buffer this code
        /// owns. Needs a graphics device, which a batch-mode editor has on Windows but is
        /// not promised anywhere, so a failure here is reported and not fatal.
        /// </summary>
        static bool Copied(Texture2D map, out Color32[] pixels, out int wide, out int high)
        {
            pixels = null;
            wide = map.width;
            high = map.height;

            if (wide <= 0 || high <= 0) return false;

            RenderTexture into = null;
            Texture2D read = null;
            var was = RenderTexture.active;

            try
            {
                into = RenderTexture.GetTemporary(wide, high, 0, RenderTextureFormat.ARGB32,
                                                  RenderTextureReadWrite.Linear);

                Graphics.Blit(map, into);

                RenderTexture.active = into;

                read = new Texture2D(wide, high, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0f, 0f, wide, high), 0, 0);
                read.Apply(false);

                pixels = read.GetPixels32();

                return pixels != null && pixels.Length == wide * high;
            }
            catch (System.Exception bad)
            {
                Debug.LogWarning($"[Ceiling] {map.name} could not be copied either: "
                                 + bad.Message);
                return false;
            }
            finally
            {
                RenderTexture.active = was;
                if (read != null) Object.DestroyImmediate(read);
                if (into != null) RenderTexture.ReleaseTemporary(into);
            }
        }

        static void Report(StringBuilder sheet, List<Fitted> fit)
        {
            var moved = new List<Fitted>();
            int held = 0;

            foreach (var one in fit)
            {
                if (one.Step > 0) moved.Add(one);
                else held++;
            }

            moved.Sort((a, b) => (b.Bytes[0] - b.Bytes[b.Step])
                                 .CompareTo(a.Bytes[0] - a.Bytes[a.Step]));

            sheet.AppendLine($"[Ceiling] {moved.Count} come down, {held} stay where they "
                             + "are; the twenty that give up the most:");

            for (int i = 0; i < moved.Count && i < 20; i++)
            {
                var one = moved[i];

                sheet.AppendLine($"[Ceiling]   {Short(one.Path),-50} "
                                 + $"{one.Sizes[0],5} -> {one.Sizes[one.Step],-5} "
                                 + $"{(one.Bytes[0] - one.Bytes[one.Step]) / 1048576f,6:0.0} MB  "
                                 + $"error {one.Error[one.Step],5:0.00}");
            }

            // Where the error ended up, which is the thing to argue with. A plan whose
            // every texture sits at a hundredth of a level has spent nothing; one with a
            // crowd at the ceiling is a plan that ran out of room.
            int[] bands = new int[5];

            foreach (var one in fit)
            {
                float was = one.Error[one.Step];

                if (was < 0.1f) bands[0]++;
                else if (was < 0.5f) bands[1]++;
                else if (was < 1.5f) bands[2]++;
                else if (was < MostError) bands[3]++;
                else bands[4]++;
            }

            // What it would cost to go one step further than the plan dares, for every
            // texture that stopped. This is the number that says whether MostError is
            // sitting on a cliff or halfway up a slope: a crowd just above it means the
            // threshold is what is binding and is worth arguing about, and a crowd far
            // above it means resolution is simply not where the weight can come from.
            var asking = new List<float>();

            foreach (var one in fit)
            {
                int next = one.Step + 1;
                if (next >= one.Error.Count) continue;

                asking.Add(one.Error[next]);
            }

            asking.Sort();

            if (asking.Count > 0)
            {
                int[] over = new int[4];

                foreach (float was in asking)
                {
                    if (was <= MostError * 2f) over[0]++;
                    else if (was <= MostError * 4f) over[1]++;
                    else if (was <= MostError * 10f) over[2]++;
                    else over[3]++;
                }

                sheet.AppendLine($"[Ceiling] the next halving, for the {asking.Count} that "
                                 + $"stopped:  to {MostError * 2f:0.0}: {over[0]}   "
                                 + $"to {MostError * 4f:0.0}: {over[1]}   "
                                 + $"to {MostError * 10f:0.0}: {over[2]}   "
                                 + $"beyond: {over[3]}      "
                                 + $"(middle one {asking[asking.Count / 2]:0.0})");
            }

            sheet.AppendLine($"[Ceiling] error, in levels of 0-255:  under 0.1: {bands[0]}   "
                             + $"to 0.5: {bands[1]}   to 1.5: {bands[2]}   "
                             + $"to {MostError:0.0}: {bands[3]}   at the ceiling: {bands[4]}");
        }

        /// <summary>
        /// Writes the worst-hit textures out side by side, original against what the plan
        /// would leave, at one pixel to one pixel.
        ///
        /// <b>Because the last question is not a measurement.</b> Everything above this is
        /// arithmetic and can be checked. Whether six levels of error on a bark texture is
        /// invisible or is the moment the forest goes to mush is a thing somebody has to
        /// look at, and the honest way to hand that over is the pixels and not a number
        /// with an adjective attached.
        ///
        /// The crop is not the middle of the image, which in an atlas is often empty. It is
        /// the busiest square in it - the one whose pixels disagree with each other most -
        /// because that is where a halving does its damage and a crop that avoids it would
        /// be a flattering picture rather than a fair one.
        /// </summary>
        static int Show(List<Fitted> fit, StringBuilder sheet)
        {
            var worst = new List<Fitted>();

            foreach (var one in fit)
                if (one.Step > 0 && one.Error[one.Step] > 0.01f) worst.Add(one);

            worst.Sort((a, b) => b.Error[b.Step].CompareTo(a.Error[a.Step]));

            System.IO.Directory.CreateDirectory(ProofDir);

            int wrote = 0;

            for (int i = 0; i < worst.Count && wrote < Proofs; i++)
            {
                var one = worst[i];

                var map = AssetDatabase.LoadAssetAtPath<Texture2D>(one.Path);
                if (map == null) continue;

                if (!Pixels(one.Path, map, out Color32[] pixels, out int wide, out int high))
                    continue;

                // Down to the chosen size and straight back up, which is what the phone
                // will be sampling from once the ceiling is written.
                var level = pixels;
                int lw = wide, lh = high;
                int down = 0;
                int target = one.Sizes[one.Step];

                while (Mathf.Max(lw, lh) > target && lw > 1 && lh > 1)
                {
                    Lost(level, lw, lh, out Color32[] half, out int hw, out int hh);
                    level = half;
                    lw = hw;
                    lh = hh;
                    down++;
                }

                if (down == 0) continue;

                Busiest(pixels, wide, high, out int cx, out int cy);

                string name = System.IO.Path.GetFileNameWithoutExtension(one.Path);
                string at = $"{ProofDir}/{wrote + 1:00} {name} "
                            + $"{one.Sizes[0]} to {target} error "
                            + $"{one.Error[one.Step]:0.0}.png".Replace(',', '.');

                if (!Beside(pixels, wide, high, level, lw, lh, cx, cy, at)) continue;

                sheet.AppendLine($"[Ceiling]   {at}");
                wrote++;
            }

            return wrote;
        }

        /// <summary>
        /// The top-left corner of the <see cref="Crop"/>-sized square whose pixels disagree
        /// with each other most. Found on a coarse grid, which is enough to land on the
        /// busy part of an atlas without walking every position.
        /// </summary>
        static void Busiest(Color32[] pixels, int wide, int high, out int cx, out int cy)
        {
            cx = Mathf.Max(0, (wide - Crop) / 2);
            cy = Mathf.Max(0, (high - Crop) / 2);

            if (wide <= Crop || high <= Crop) { cx = 0; cy = 0; return; }

            double busiest = -1d;
            int stride = Mathf.Max(1, Crop / 2);
            int step = Mathf.Max(1, Crop / 32);

            for (int y = 0; y + Crop <= high; y += stride)
            {
                for (int x = 0; x + Crop <= wide; x += stride)
                {
                    double sum = 0d;
                    double square = 0d;
                    long many = 0;

                    for (int sy = 0; sy < Crop; sy += step)
                    {
                        int row = (y + sy) * wide + x;

                        for (int sx = 0; sx < Crop; sx += step)
                        {
                            var pixel = pixels[row + sx];
                            double grey = pixel.r * 0.299d + pixel.g * 0.587d
                                          + pixel.b * 0.114d;

                            sum += grey;
                            square += grey * grey;
                            many++;
                        }
                    }

                    if (many == 0) continue;

                    double spread = square / many - (sum / many) * (sum / many);

                    if (spread <= busiest) continue;

                    busiest = spread;
                    cx = x;
                    cy = y;
                }
            }
        }

        /// <summary>
        /// One PNG: the original crop on the left, the same crop as the reduced texture
        /// would deliver it on the right, with a line between them.
        ///
        /// <b>Smoothed and not blocked up.</b> The first version drew the right side with
        /// nearest neighbour, on the reasoning that a smooth upscale puts back detail the
        /// texture no longer carries - which is true of the information and false of the
        /// picture. The phone samples that texture bilinearly, so blocks are what the
        /// arithmetic has lost and not what anyone will see, and a crop drawn in blocks
        /// asks somebody to judge a harsher thing than the game will show them. These
        /// images exist to be judged, so they are drawn the way the game draws.
        /// </summary>
        static bool Beside(Color32[] was, int wide, int high,
                           Color32[] now, int lw, int lh,
                           int cx, int cy, string at)
        {
            int side = Mathf.Min(Crop, Mathf.Min(wide, high));
            int across = side * 2 + 4;

            var sheet = new Color32[across * side];
            var line = new Color32(255, 0, 128, 255);

            float shrink = (float)lw / wide;

            for (int y = 0; y < side; y++)
            {
                int row = y * across;
                int from = (cy + y) * wide + cx;

                for (int x = 0; x < side; x++)
                {
                    sheet[row + x] = was[from + x];

                    sheet[row + side + 4 + x] = Smooth(now, lw, lh,
                                                      (cx + x) * shrink - 0.5f,
                                                      (cy + y) * shrink - 0.5f);
                }

                for (int x = 0; x < 4; x++) sheet[row + side + x] = line;
            }

            Texture2D page = null;

            try
            {
                // Rows run bottom-up in a Texture2D and top-down in the arrays above, so
                // the page is built upside down and flipped on the way out.
                var flipped = new Color32[sheet.Length];

                for (int y = 0; y < side; y++)
                    System.Array.Copy(sheet, y * across,
                                      flipped, (side - 1 - y) * across, across);

                page = new Texture2D(across, side, TextureFormat.RGBA32, false);
                page.SetPixels32(flipped);
                page.Apply(false);

                System.IO.File.WriteAllBytes(at, page.EncodeToPNG());
                return true;
            }
            catch (System.Exception bad)
            {
                Debug.LogWarning($"[Ceiling] {at} could not be written: {bad.Message}");
                return false;
            }
            finally
            {
                if (page != null) Object.DestroyImmediate(page);
            }
        }

        /// <summary>One pixel read out of a smaller image the way the hardware reads it.</summary>
        static Color32 Smooth(Color32[] from, int wide, int high, float x, float y)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, wide - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, high - 1);
            int x1 = Mathf.Min(x0 + 1, wide - 1);
            int y1 = Mathf.Min(y0 + 1, high - 1);

            float ax = Mathf.Clamp01(x - Mathf.Floor(x));
            float ay = Mathf.Clamp01(y - Mathf.Floor(y));

            var a = from[y0 * wide + x0];
            var b = from[y0 * wide + x1];
            var c = from[y1 * wide + x0];
            var d = from[y1 * wide + x1];

            return new Color32(
                Mix(a.r, b.r, c.r, d.r, ax, ay),
                Mix(a.g, b.g, c.g, d.g, ax, ay),
                Mix(a.b, b.b, c.b, d.b, ax, ay),
                Mix(a.a, b.a, c.a, d.a, ax, ay));
        }

        static byte Mix(byte a, byte b, byte c, byte d, float ax, float ay)
        {
            float top = a + (b - a) * ax;
            float bottom = c + (d - c) * ax;

            return (byte)Mathf.Clamp(Mathf.RoundToInt(top + (bottom - top) * ay), 0, 255);
        }

        static int Apply(List<Fitted> fit)
        {
            int wrote = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < fit.Count; i++)
                {
                    var one = fit[i];

                    EditorUtility.DisplayProgressBar("Fit Textures", one.Path,
                                                     (float)i / fit.Count);

                    var importer = AssetImporter.GetAtPath(one.Path) as TextureImporter;
                    if (importer == null) continue;

                    var settings = importer.GetPlatformTextureSettings("Android");

                    settings.overridden = true;
                    settings.maxTextureSize = one.Sizes[one.Step];

                    // The format is left to the importer. Resolution is what was
                    // measured here, and choosing a codec by an argument this instrument
                    // never made would be the second time today that a number was quoted
                    // for a question it had not been asked.
                    settings.format = TextureImporterFormat.Automatic;

                    importer.SetPlatformTextureSettings(settings);
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();

                    wrote++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            return wrote;
        }

        struct Fitted
        {
            public string Path;

            /// <summary>How many halvings down this one has been taken.</summary>
            public int Step;

            /// <summary>Error at each step, root mean square, in levels of 0-255.</summary>
            public List<float> Error;

            /// <summary>What it weighs at each step.</summary>
            public List<long> Bytes;

            /// <summary>The ceiling each step means.</summary>
            public List<int> Sizes;
        }

        static string Short(string path)
        {
            const string head = "Assets/";

            string said = path.StartsWith(head, System.StringComparison.Ordinal)
                ? path.Substring(head.Length)
                : path;

            return said.Length <= 50 ? said : "..." + said.Substring(said.Length - 47);
        }
    }
}
