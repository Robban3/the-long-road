using System.Collections.Generic;
using System.Text;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Every prefab in a pack, rendered one at a time and laid out on contact sheets:
    /// `The Veil > Pack Report`.
    ///
    /// <b>Written because listing filenames is not looking, and I kept mistaking the one
    /// for the other.</b> The castle was built for months out of four of the pack's pieces
    /// while the gatehouse, the portcullis, the buttress and the turret sat unused — and
    /// each was found only when something went visibly wrong and I went hunting for one
    /// thing. A name in a directory listing tells you nothing: SM_Bld_Castle_Pillar_01 is
    /// a buttress drawn to the wall's exact height, and SM_Bld_Rockwall_Archway_01 is a
    /// 2.4 m garden arch, and nothing about the two names says which is which.
    ///
    /// Each piece is framed on its own and scaled to its cell, so a 23 cm skull and a 14 m
    /// wall are both legible. That is the point: relative size is what the measurements in
    /// the log are for, and shape is what the pictures are for. Asking one image to carry
    /// both is how the paving went down at four metres.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.PackReport.Run
    /// </summary>
    public static class PackReport
    {
        /// <summary>Cells across and down one sheet, and how big a cell is.</summary>
        const int Across = 6;
        const int Down = 5;
        const int Cell = 256;

        [MenuItem("The Veil/Pack Report")]
        public static void Run() => Sheets("Assets/Synty/PolygonKnights/Prefabs", "knights");

        [MenuItem("The Veil/Pack Report (Nature)")]
        public static void Nature() => Sheets("Assets/Synty/PolygonNature/Prefabs", "nature");

        /// <summary>
        /// The alpine pack, in three passes.
        ///
        /// Sheets walks a folder's subfolders, and this pack keeps its pieces one level
        /// higher than the others: the models sit straight in Prefabs, the furniture in
        /// Prefabs/Props and the effects in FX/FX_Prefabs. Pointing the same walk at three
        /// roots covers all of it rather than teaching the walk about this one pack.
        /// </summary>
        [MenuItem("The Veil/Pack Report (Alpine)")]
        public static void Alpine()
        {
            const string pack = "Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain";

            Sheets(pack, "alpine");
            Sheets($"{pack}/Prefabs", "alpine-props");
            Sheets($"{pack}/FX", "alpine-fx");
        }

        static void Sheets(string root, string label)
        {
            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPack");
            System.IO.Directory.CreateDirectory(shots);

            // A sun of its own, so every sheet is lit the same and a piece that looks dark
            // is dark rather than badly placed.
            var sun = new GameObject("Pack sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(35f, 140f, 0f);

            var sheet = new StringBuilder();
            sheet.AppendLine($"[Pack] {label}: every prefab, measured and drawn");

            foreach (string folder in System.IO.Directory.GetDirectories(root))
            {
                string group = System.IO.Path.GetFileName(folder);

                var paths = new List<string>(System.IO.Directory.GetFiles(folder, "*.prefab"));
                paths.Sort(System.StringComparer.Ordinal);
                if (paths.Count == 0) continue;

                sheet.AppendLine($"[Pack] == {group} ({paths.Count}) ==");

                int perSheet = Across * Down;
                int sheets = (paths.Count + perSheet - 1) / perSheet;

                for (int s = 0; s < sheets; s++)
                {
                    var page = new Texture2D(Across * Cell, Down * Cell, TextureFormat.RGB24, false);
                    Fill(page, new Color(0.30f, 0.36f, 0.43f));

                    for (int i = 0; i < perSheet; i++)
                    {
                        int index = s * perSheet + i;
                        if (index >= paths.Count) break;

                        string path = paths[index].Replace('\\', '/');
                        string name = System.IO.Path.GetFileNameWithoutExtension(path);

                        var shot = Draw(path, out var box);
                        if (shot == null) { sheet.AppendLine($"[Pack] {name}: would not load"); continue; }

                        // Row from the top, so the sheet reads the way the log does.
                        int col = i % Across;
                        int row = Down - 1 - i / Across;
                        page.SetPixels(col * Cell, row * Cell, Cell, Cell, shot.GetPixels());
                        Object.DestroyImmediate(shot);

                        sheet.AppendLine($"[Pack] {group}/{s + 1} r{i / Across + 1}c{col + 1}  {name}"
                                         + $"  {box.size.x:0.00} x {box.size.y:0.00} x {box.size.z:0.00} m");
                    }

                    page.Apply();
                    System.IO.File.WriteAllBytes(
                        System.IO.Path.Combine(shots, $"{label}-{group}-{s + 1}.png"),
                        page.EncodeToPNG());

                    Object.DestroyImmediate(page);
                }
            }

            Object.DestroyImmediate(sun);

            sheet.AppendLine($"[Pack] sheets in {shots}");
            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// One piece, framed to fill its cell, seen from three quarters and a little above.
        ///
        /// Three quarters rather than square on, because square on a tower and a wall are
        /// the same rectangle. The angle is what separates a drum from a box, and looking
        /// slightly down is what shows whether a thing has a top.
        /// </summary>
        static Texture2D Draw(string path, out Bounds box)
        {
            box = default;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;

            var piece = Object.Instantiate(prefab);
            box = ModelScaling.Measure(piece);

            float span = Mathf.Max(box.size.x, box.size.y, box.size.z);
            if (span <= 0.0001f) span = 1f;

            var go = new GameObject("Pack camera");
            var camera = go.AddComponent<Camera>();

            var eye = box.center + new Quaternion[] { Quaternion.Euler(22f, 35f, 0f) }[0]
                                   * new Vector3(0f, 0f, -span * 2.1f);

            camera.transform.position = eye;
            camera.transform.LookAt(box.center);
            camera.fieldOfView = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.30f, 0.36f, 0.43f);

            var rt = new RenderTexture(Cell, Cell, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(piece);

            return tex;
        }

        static void Fill(Texture2D texture, Color colour)
        {
            var pixels = new Color[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = colour;
            texture.SetPixels(pixels);
        }
    }
}
