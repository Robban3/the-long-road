using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace TheVeil.Editor
{
    /// <summary>
    /// Every texture that ships, and what it weighs: `The Veil &gt; Texture Report`.
    ///
    /// <b>Because three quarters of the package is texture.</b> The first Android build
    /// came out at 386,7 MB against a line of 150, and its own manifest said where the
    /// weight is: 756,2 MB of packed Texture2D against 201,0 MB of mesh and 34 MB of
    /// everything else. Nothing else on that list is worth an afternoon until this is
    /// dealt with.
    ///
    /// And 756 MB of texture for a stylised low-poly game is not a budget problem. It is
    /// an import-settings problem: these are desktop-sized atlases imported for a desktop,
    /// in a project whose own technical design asks for a shared atlas per biome on a
    /// phone. What this report is for is to find out which, before anything is changed -
    /// a flat colour palette at 2048 square looks the same at 256 and a hand-painted
    /// detail map does not, and the difference decides whether this is an afternoon or a
    /// conversation about how the game looks.
    ///
    /// <b>What ships, and not what is in the project.</b> The packs carry demo scenes,
    /// showcase materials and variants this game never stands up, and counting those would
    /// inflate every number here and point at textures no player will ever load. So the
    /// list is the dependencies of the scenes in the build settings, recursively - which
    /// is the same set the build itself packs.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.TextureReport.Run
    /// </summary>
    public static class TextureReport
    {
        /// <summary>How many rows are printed. The tail is summarised instead.</summary>
        const int Rows = 25;

        [MenuItem("The Veil/Texture Report")]
        public static void Run()
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);

            if (scenes.Count == 0)
            {
                Debug.LogError("[Texture] no scenes are enabled, so nothing ships.");
                return;
            }

            var seen = new HashSet<string>();

            foreach (string path in AssetDatabase.GetDependencies(scenes.ToArray(), true))
                if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D))
                    seen.Add(path);

            var sheet = new StringBuilder();
            sheet.AppendLine($"[Texture] {seen.Count} textures are reachable from the "
                             + $"{scenes.Count} scenes the build settings ship");

            var rows = new List<Row>();
            long all = 0;
            long editor = 0;

            var bySize = new Dictionary<int, Count>();
            var byFormat = new Dictionary<string, Count>();
            long overridden = 0;
            int overrides = 0;

            foreach (string path in seen)
            {
                var map = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (map == null) continue;

                long bytes = Weighs(map);
                all += bytes;

                editor += Profiler.GetRuntimeMemorySizeLong(map);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                int ceiling = 0;
                bool phone = false;

                if (importer != null)
                {
                    ceiling = importer.maxTextureSize;

                    var android = importer.GetPlatformTextureSettings("Android");
                    phone = android != null && android.overridden;

                    if (phone) { overrides++; overridden += bytes; }
                }

                rows.Add(new Row
                {
                    Path = path,
                    Wide = map.width,
                    High = map.height,
                    Format = map.format.ToString(),
                    Bytes = bytes,
                    Ceiling = ceiling,
                    Phone = phone,
                });

                Add(bySize, Mathf.Max(map.width, map.height), bytes);
                Add(byFormat, map.format.ToString(), bytes);
            }

            rows.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));

            sheet.AppendLine($"[Texture] {all / 1048576f:0.0} MB in all, counted from "
                             + "dimensions, format and mip chain");
            sheet.AppendLine($"[Texture] (the profiler says {editor / 1048576f:0.0} MB for the "
                             + "same textures, which is the editor's own copy of them - see "
                             + "Weighs)");
            sheet.AppendLine($"[Texture] {overrides} of {rows.Count} carry an Android "
                             + $"override ({overridden / 1048576f:0.0} MB)");

            sheet.AppendLine("[Texture] by longest edge:");
            foreach (var line in Sorted(bySize))
                sheet.AppendLine($"[Texture]   {line.Key,6} px  {line.Value.Bytes / 1048576f,8:0.0} MB  "
                                 + $"{line.Value.Many,5} textures  "
                                 + $"{100f * line.Value.Bytes / all,5:0.0}%");

            sheet.AppendLine("[Texture] by format, which is what says whether they are "
                             + "compressed at all:");
            foreach (var line in Sorted(byFormat))
                sheet.AppendLine($"[Texture]   {line.Key,-22} {line.Value.Bytes / 1048576f,8:0.0} MB  "
                                 + $"{line.Value.Many,5} textures  "
                                 + $"{100f * line.Value.Bytes / all,5:0.0}%");

            sheet.AppendLine($"[Texture] heaviest {Rows}:");

            long head = 0;

            for (int i = 0; i < rows.Count && i < Rows; i++)
            {
                head += rows[i].Bytes;

                sheet.AppendLine($"[Texture]   {Short(rows[i].Path),-54} "
                                 + $"{rows[i].Wide,5}x{rows[i].High,-5} {rows[i].Format,-18} "
                                 + $"{rows[i].Bytes / 1048576f,7:0.0} MB  max {rows[i].Ceiling,5}"
                                 + (rows[i].Phone ? "  android" : ""));
            }

            sheet.AppendLine($"[Texture] those {Mathf.Min(Rows, rows.Count)} are "
                             + $"{100f * head / all:0.0}% of it; the rest is a long tail");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// What a texture weighs on a phone, counted rather than asked for.
        ///
        /// <b>Because the profiler answers a different question.</b>
        /// Profiler.GetRuntimeMemorySizeLong gave every 4096 texture in this project the
        /// same 106,7 MB whatever its format, and 6760,4 MB for the lot - against the
        /// 756,2 MB of packed Texture2D the Android build's own manifest reported. It is
        /// measuring the copy the editor is holding, which is format-blind, so it both
        /// inflates the total by nine times and hands a four-bit texture and an eight-bit
        /// one the same bill. A share of a distorted total is a distorted share, and the
        /// whole point of this report is to say which textures to deal with first.
        ///
        /// So: bits per pixel from the format, block-aligned for the compressed ones, down
        /// the mip chain. Counted this way the total lands within a few per cent of what
        /// the build packed, which is the only cross-check available without a device.
        /// </summary>
        static long Weighs(Texture2D map)
        {
            long bytes = Weighs(map.width, map.height, map.format,
                                Mathf.Max(1, map.mipmapCount));

            return bytes > 0 ? bytes : Profiler.GetRuntimeMemorySizeLong(map);
        }

        /// <summary>
        /// The same arithmetic for a size a texture does not have yet, which is what
        /// <see cref="TextureCeiling"/> needs to price a smaller one. Zero when the format
        /// is not one this knows.
        /// </summary>
        internal static long Weighs(int wide0, int high0, TextureFormat format, int levels)
        {
            int bits = Bits(format, out int block);

            if (bits <= 0) return 0;

            long bytes = 0;
            levels = Mathf.Max(1, levels);

            for (int level = 0; level < levels; level++)
            {
                int wide = Mathf.Max(1, wide0 >> level);
                int high = Mathf.Max(1, high0 >> level);

                if (block > 1)
                {
                    // A compressed level is whole blocks even when the level is smaller
                    // than one, which is why the tail of a mip chain costs more than the
                    // pixel count says.
                    long across = (wide + block - 1) / block;
                    long down = (high + block - 1) / block;
                    bytes += across * down * (block * block * bits / 8);
                }
                else
                {
                    bytes += (long)wide * high * bits / 8;
                }
            }

            return bytes;
        }

        /// <summary>
        /// Bits per pixel for a format, and the block it is stored in (1 when it is not
        /// block-compressed). Zero for a format not listed, which sends the caller back to
        /// the profiler rather than to a guess.
        /// </summary>
        static int Bits(TextureFormat format, out int block)
        {
            block = 4;

            switch (format)
            {
                // Four bits a pixel: an eight-byte block over sixteen pixels.
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC2_RGB:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.DXT1:
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                    return 4;

                // Eight bits a pixel: a sixteen-byte block over the same sixteen pixels,
                // which is what an alpha channel costs.
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                case TextureFormat.ASTC_4x4:
                    return 8;

                case TextureFormat.ASTC_5x5: block = 5; return 8;
                case TextureFormat.ASTC_6x6: block = 6; return 8;
                case TextureFormat.ASTC_8x8: block = 8; return 8;
                case TextureFormat.ASTC_10x10: block = 10; return 8;
                case TextureFormat.ASTC_12x12: block = 12; return 8;
            }

            block = 1;

            switch (format)
            {
                case TextureFormat.Alpha8:
                case TextureFormat.R8: return 8;
                case TextureFormat.RGB565:
                case TextureFormat.RGBA4444:
                case TextureFormat.ARGB4444:
                case TextureFormat.RG16:
                case TextureFormat.RHalf:
                case TextureFormat.R16: return 16;
                case TextureFormat.RGB24: return 24;
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                case TextureFormat.RGHalf:
                case TextureFormat.RFloat: return 32;
                case TextureFormat.RGBAHalf:
                case TextureFormat.RGFloat: return 64;
                case TextureFormat.RGBAFloat: return 128;
            }

            return 0;
        }

        struct Row
        {
            public string Path;
            public int Wide;
            public int High;
            public string Format;
            public long Bytes;
            public int Ceiling;
            public bool Phone;
        }

        struct Count
        {
            public long Bytes;
            public int Many;
        }

        static void Add<T>(Dictionary<T, Count> into, T key, long bytes)
        {
            into.TryGetValue(key, out var had);
            had.Bytes += bytes;
            had.Many++;
            into[key] = had;
        }

        static List<KeyValuePair<T, Count>> Sorted<T>(Dictionary<T, Count> of)
        {
            var rows = new List<KeyValuePair<T, Count>>(of);
            rows.Sort((a, b) => b.Value.Bytes.CompareTo(a.Value.Bytes));
            return rows;
        }

        /// <summary>The path without the Assets prefix, so a row fits on a line.</summary>
        static string Short(string path)
        {
            const string head = "Assets/";

            string said = path.StartsWith(head, System.StringComparison.Ordinal)
                ? path.Substring(head.Length)
                : path;

            return said.Length <= 54 ? said : "..." + said.Substring(said.Length - 51);
        }
    }
}
