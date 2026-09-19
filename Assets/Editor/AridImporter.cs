using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Builds Unity meshes out of the arid pack's FBX files, because Unity will not.
    ///
    /// <b>The pack imports as empty objects.</b> Its files are ASCII FBX written by a
    /// generator rather than exported by a modelling tool - the header says as much - and
    /// Unity's importer reads them without complaint and produces a GameObject with no
    /// mesh on it and no materials. Every one of the thirty-six, measured: nought by
    /// nought by nought metres.
    ///
    /// The geometry is all there in plain text, though: vertices, polygons, a material
    /// index per polygon and three colours with their values written out. So it is read
    /// here and turned into meshes, materials and prefabs the project can use. Not a
    /// general FBX reader and it should not grow into one - it handles exactly what these
    /// files contain, and says so loudly when it meets anything else.
    ///
    /// Normals are worked out from the triangles, because the files carry none. There are
    /// no texture coordinates either, which costs nothing: the materials are flat colours,
    /// which is what the pack itself describes.
    /// </summary>
    public static class AridImporter
    {
        const string Source = "Assets/ThirdParty/AridDesertBiomeV2/Models_FBX";
        const string MeshDir = "Assets/_Project/Models/Arid";
        const string MaterialDir = "Assets/_Project/Materials/Arid";
        const string PrefabDir = "Assets/_Project/Prefabs/Arid";

        [MenuItem("The Veil/Build Arid Models")]
        public static void Build()
        {
            Folder(MeshDir);
            Folder(MaterialDir);
            Folder(PrefabDir);

            var said = new StringBuilder();
            said.AppendLine("[Arid] meshes built from the pack's text");

            int built = 0, skipped = 0;

            foreach (string path in System.IO.Directory.GetFiles(Source, "*.fbx"))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                string text = System.IO.File.ReadAllText(path);

                var mesh = Read(text, name, out var colours, out string why);

                if (mesh == null)
                {
                    said.AppendLine($"[Arid] {name}: {why}");
                    skipped++;
                    continue;
                }

                AssetDatabase.CreateAsset(mesh, $"{MeshDir}/{name}.asset");

                var materials = new Material[colours.Count];
                for (int i = 0; i < colours.Count; i++) materials[i] = Painted(colours[i]);

                var go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;

                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
                Object.DestroyImmediate(go);

                var size = mesh.bounds.size;
                said.AppendLine($"[Arid] {name}: {mesh.vertexCount} vertices, "
                                + $"{mesh.subMeshCount} part(s), {size.x:0.00} x {size.y:0.00} "
                                + $"x {size.z:0.00} m");
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            said.AppendLine($"[Arid] {built} built, {skipped} skipped");
            Debug.Log(said.ToString());

            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlaytest", "arid.txt"),
                said.ToString());
        }

        /// <summary>
        /// One file's mesh, or null with a reason.
        ///
        /// Polygons are a run of vertex indices ended by a negative one - the last index
        /// of a face is written as its bitwise complement - and they are fanned into
        /// triangles from the first corner, which is right for the convex faces these
        /// models are made of. Each polygon carries a material index, so the triangles
        /// are gathered into one submesh per material and the parts keep their colours.
        /// </summary>
        static Mesh Read(string text, string name, out List<Color> colours, out string why)
        {
            colours = new List<Color>();
            why = null;

            var points = Numbers(text, "Vertices");
            var corners = Integers(text, "PolygonVertexIndex");

            if (points == null || corners == null) { why = "no geometry in the file"; return null; }
            if (points.Count % 3 != 0) { why = $"{points.Count} vertex numbers, not a multiple of three"; return null; }

            foreach (Match match in Regex.Matches(text,
                "Material:: *[A-Za-z0-9_]+.*?DiffuseColor\", \"Color\", \"\", \"A\","
                + @"([-\d.eE]+),([-\d.eE]+),([-\d.eE]+)",
                RegexOptions.Singleline))
            {
                colours.Add(new Color(Float(match.Groups[1].Value), Float(match.Groups[2].Value),
                                      Float(match.Groups[3].Value)));
            }

            if (colours.Count == 0) colours.Add(new Color(0.78f, 0.74f, 0.62f));

            var vertices = new List<Vector3>(points.Count / 3);
            for (int i = 0; i + 2 < points.Count; i += 3)
                vertices.Add(new Vector3(points[i], points[i + 1], points[i + 2]));

            // Which material each polygon wears, where the file says. Missing or short,
            // everything goes on the first one rather than nothing being drawn.
            var perPolygon = Integers(text, "Materials");

            var parts = new List<List<int>>();
            for (int i = 0; i < colours.Count; i++) parts.Add(new List<int>());

            var face = new List<int>();
            int polygon = 0;

            foreach (int corner in corners)
            {
                bool last = corner < 0;
                face.Add(last ? ~corner : corner);

                if (!last) continue;

                int slot = perPolygon != null && polygon < perPolygon.Count
                    ? Mathf.Clamp(perPolygon[polygon], 0, colours.Count - 1)
                    : 0;

                for (int i = 1; i + 1 < face.Count; i++)
                {
                    parts[slot].Add(face[0]);
                    parts[slot].Add(face[i]);
                    parts[slot].Add(face[i + 1]);
                }

                face.Clear();
                polygon++;
            }

            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.subMeshCount = parts.Count;
            for (int i = 0; i < parts.Count; i++) mesh.SetTriangles(parts[i], i);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (polygon == 0) { why = "no polygons in the file"; return null; }
            return mesh;
        }

        /// <summary>A material of that colour, shared by every model that asks for it.</summary>
        static Material Painted(Color colour)
        {
            string name = $"Arid_{Mathf.RoundToInt(colour.r * 255):X2}"
                          + $"{Mathf.RoundToInt(colour.g * 255):X2}"
                          + $"{Mathf.RoundToInt(colour.b * 255):X2}";

            string path = $"{MaterialDir}/{name}.mat";
            var found = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (found != null) return found;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };

            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            material.SetFloat("_Smoothness", 0.1f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static List<float> Numbers(string text, string key)
        {
            var body = Body(text, key);
            if (body == null) return null;

            var list = new List<float>();
            foreach (string part in body.Split(',')) list.Add(Float(part));
            return list;
        }

        static List<int> Integers(string text, string key)
        {
            var body = Body(text, key);
            if (body == null) return null;

            var list = new List<int>();
            foreach (string part in body.Split(','))
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                                 out int value)) list.Add(value);

            return list;
        }

        /// <summary>The numbers inside `Key: *n { a: ... }`.</summary>
        static string Body(string text, string key)
        {
            var match = Regex.Match(text, key + @": \*\d+ \{ a: ([^}]*)\}");
            return match.Success ? match.Groups[1].Value : null;
        }

        static float Float(string text)
            => float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                              out float value) ? value : 0f;

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
