using System.Collections.Generic;
using System.Text;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Turns the broken wagon into something a level can carry a hundred of.
    ///
    /// <b>It arrives at two and a third million triangles.</b> One metre ninety long,
    /// generated rather than modelled, with a two thousand pixel texture and three more
    /// maps beside it: a hundred and three megabytes for a prop the player sees from
    /// forty-seven metres up. The game is stylised low poly aimed at a phone
    /// (docs/GDD.md §1), every level has traps, and every trap is to have one of these
    /// beside it. Dropped in as it came, one wagon would cost more than every other thing
    /// on the map put together.
    ///
    /// So it is simplified here, once, into a mesh asset the project keeps - and the
    /// original is not kept in Assets, where Unity would reimport a hundred megabytes on
    /// every clean checkout for a file nothing loads.
    ///
    /// <b>Two passes, because one was not enough.</b> Clustering alone - drop the model
    /// into a grid, keep one vertex per cell - was tried first and photographed, and it
    /// had eaten the wagon: no wheel, no plank, a cloud of loose shards. A spoke is
    /// thinner than a cell, so clustering welds it to itself and it stops existing.
    ///
    /// So clustering is now only the cheap first pass, at a grid fine enough that nothing
    /// of the shape is in it, to get two and a third million generated triangles down to
    /// something an honest simplifier can chew through. MeshDecimator does the rest, and
    /// it is the one that knows a wheel from a plank.
    /// </summary>
    public static class WagonImporter
    {
        const string Source = "Assets/ThirdParty/BrokenWagon";
        const string MeshDir = "Assets/_Project/Models/Wreck";
        const string MaterialDir = "Assets/_Project/Materials/Wreck";
        const string PrefabDir = "Assets/_Project/Prefabs/Wreck";

        /// <summary>
        /// Triangles the simplified wagon may have.
        ///
        /// Four thousand. The packs this project is built from run a few hundred to a few
        /// thousand for a prop of this size - the knights' cart is about nine hundred -
        /// so this is generous rather than tight, and it is a five-hundredth of what
        /// arrived.
        /// </summary>
        const int Budget = 4000;

        /// <summary>
        /// The biggest a texture on it may be, in pixels.
        ///
        /// A thousand and twenty-four. The wagon is under two metres and is looked at
        /// from forty-seven, so it covers a few dozen pixels of screen: the two thousand
        /// it came at is four times the memory for detail that never reaches a frame.
        ///
        /// The files in the project are cut to this size as well, not only read at it.
        /// The importer's limit spares the build and the running game; it does not spare
        /// the repository, which would carry fourteen megabytes of pixels nothing can
        /// display. The originals are kept outside the project with the FBX.
        /// </summary>
        const int TextureSize = 1024;

        /// <summary>
        /// Photographs the simplified wagon beside the shape it was cut from.
        ///
        /// Clustering can ruin a model and the triangle count does not say whether it
        /// did. This is the only way to know.
        /// </summary>
        /// <summary>Set from the command line to photograph the untouched import instead.</summary>
        static bool Raw => System.Environment.CommandLine.Contains("-wagonRaw");

        [MenuItem("The Veil/Wagon Photo")]
        public static void Photo()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Raw ? $"{Source}/BrokenWagon.fbx" : $"{PrefabDir}/BrokenWagon.prefab");
            if (prefab == null) { Debug.LogError("[Wagon] nothing to photograph."); return; }

            var stage = new GameObject("Wagon stage");

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, 35f, 0f);
            light.intensity = 1.2f;
            light.transform.SetParent(stage.transform);

            var wagon = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            wagon.transform.SetParent(stage.transform);
            wagon.transform.position = Vector3.zero;

            var box = ModelScaling.Measure(wagon);
            float span = Mathf.Max(box.size.x, box.size.z);

            Shoot(box.center + new Vector3(0f, span * 1.1f, -span * 1.6f), box.center,
                  Raw ? "wagon-raw-side.png" : "wagon-side.png");
            Shoot(box.center + new Vector3(0f, span * 3f, -span * 0.2f), box.center,
                  Raw ? "wagon-raw-above.png" : "wagon-above.png");

            Object.DestroyImmediate(stage);
            Debug.Log("[Wagon] photographed");
        }

        static void Shoot(Vector3 from, Vector3 at, string file)
        {
            var go = new GameObject("Wagon camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.48f, 0.38f);

            const int size = 900;
            var rt = new RenderTexture(size, size, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlaytest", file);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        [MenuItem("The Veil/Build Broken Wagon")]
        public static void Build()
        {
            var said = new StringBuilder();

            string fbx = $"{Source}/BrokenWagon.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);

            if (model == null)
            {
                Debug.LogError($"[Wagon] {fbx} is not in the project. It is kept out of "
                               + "Assets once the mesh has been baked — put it back to rebuild.");
                return;
            }

            Folder(MeshDir);
            Folder(MaterialDir);
            Folder(PrefabDir);

            Shrink($"{Source}/BrokenWagon.png", false);
            Shrink($"{Source}/BrokenWagon_normal.png", true);
            Shrink($"{Source}/BrokenWagon_roughness.png", false);
            Shrink($"{Source}/BrokenWagon_metallic.png", false);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var filter = instance.GetComponentInChildren<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                Object.DestroyImmediate(instance);
                Debug.LogError("[Wagon] no mesh on the model.");
                return;
            }

            var full = filter.sharedMesh;
            said.AppendLine($"[Wagon] arrived with {full.triangles.Length / 3} triangles");

            // <b>In metres, not in whatever the file counted in.</b> The importer leaves
            // the mesh in the model's own units and puts the scale on the transform, so a
            // mesh baked straight out of it came to two centimetres across while the
            // thing on screen was one metre ninety.
            // <b>And standing on its wheels, not lying on its back.</b> The file is Z up,
            // which Blender exports by default and Unity does not read back: the prefab
            // measured 1.89 long, 0.84 tall, 1.57 wide, and a wagon 0.84 m tall and 1.57 m
            // across is a wagon turned a quarter over. Photographed at a trap it was a
            // wagon bed seen from above with its wheels flat under it.
            //
            // A quarter turn about X, baked into the mesh rather than put on the prefab's
            // transform, so anything that measures this model - the decorator's sizing,
            // the sweep that clears ground round a landmark - measures the shape the
            // player sees.
            var upright = Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f))
                          * filter.transform.localToWorldMatrix;

            var mesh = Simplified(full, Budget, said, upright);
            mesh.name = "BrokenWagon";

            AssetDatabase.CreateAsset(mesh, $"{MeshDir}/BrokenWagon.asset");

            var material = Dressed();

            var go = new GameObject("BrokenWagon");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/BrokenWagon.prefab");
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var box = mesh.bounds.size;
            said.AppendLine($"[Wagon] kept {mesh.triangles.Length / 3} triangles, "
                            + $"{mesh.vertexCount} vertices, {box.x:0.00} x {box.y:0.00} "
                            + $"x {box.z:0.00} m");

            Debug.Log(said.ToString());
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlaytest", "wagon.txt"),
                said.ToString());
        }

        /// <summary>
        /// How many triangles the cheap first pass leaves for the careful one.
        ///
        /// A hundred and fifty thousand. Edge collapse weighs every edge in the mesh and
        /// keeps them in order, so two and a third million of them is minutes of work and
        /// a lot of memory for detail that is thrown away in the first second. Clustering
        /// at a tenth of a millimetre removes the generated noise and none of the wagon.
        /// </summary>
        const int Rough = 150_000;

        /// <summary>
        /// The same shape with far fewer triangles: clustered coarse, then collapsed.
        ///
        /// The grid for the first pass is chosen by trying, because how many triangles a
        /// given fineness leaves depends on the model. Each step is a little finer than
        /// the last, and the last one that still fits under Rough is the one handed on.
        /// </summary>
        static Mesh Simplified(Mesh full, int budget, StringBuilder said, Matrix4x4 into)
        {
            var coarse = Cluster(full, 64, into);

            for (int cells = 90; cells <= 2048; cells = (int)(cells * 1.4f))
            {
                var tried = Cluster(full, cells, into);

                if (tried.triangles.Length / 3 > Rough)
                {
                    Object.DestroyImmediate(tried);
                    break;
                }

                Object.DestroyImmediate(coarse);
                coarse = tried;
            }

            said.AppendLine($"[Wagon] rough pass left {coarse.triangles.Length / 3} triangles");

            var mesh = MeshDecimator.Simplify(coarse, budget, out string story);
            said.AppendLine($"[Wagon] {story}");

            Object.DestroyImmediate(coarse);
            return mesh;
        }

        /// <summary>One vertex per occupied cell, averaged, with the triangles remapped.</summary>
        static Mesh Cluster(Mesh full, int cells, Matrix4x4 into)
        {
            var points = full.vertices;
            for (int i = 0; i < points.Length; i++) points[i] = into.MultiplyPoint3x4(points[i]);
            var uvs = full.uv;
            var triangles = full.triangles;

            var box = new Bounds(points.Length > 0 ? points[0] : Vector3.zero, Vector3.zero);
            foreach (var point in points) box.Encapsulate(point);

            float span = Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z));
            float cell = span / cells;
            if (cell <= 0f) cell = 0.01f;

            var slot = new Dictionary<long, int>();
            var where = new List<Vector3>();
            var mapped = new List<Vector2>();
            var counted = new List<int>();
            var index = new int[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                var at = points[i] - box.min;

                // Mixed radix rather than three multiples added up: the old key put a
                // thousand and nine between the y steps, which is fine at twelve cells
                // across and hands two different cells the same number at a thousand.
                long key = ((long)(at.x / cell) * 4096L + (long)(at.y / cell)) * 4096L
                         + (long)(at.z / cell);

                if (!slot.TryGetValue(key, out int found))
                {
                    found = where.Count;
                    slot[key] = found;
                    where.Add(Vector3.zero);
                    mapped.Add(Vector2.zero);
                    counted.Add(0);
                }

                where[found] += points[i];
                if (uvs != null && uvs.Length == points.Length) mapped[found] += uvs[i];
                counted[found]++;
                index[i] = found;
            }

            for (int i = 0; i < where.Count; i++)
            {
                where[i] /= counted[i];
                mapped[i] /= counted[i];
            }

            var kept = new List<int>();
            var seen = new HashSet<long>();

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = index[triangles[i]], b = index[triangles[i + 1]], c = index[triangles[i + 2]];
                if (a == b || b == c || a == c) continue;

                // One triangle per trio of cells. Without this a flat panel of ten
                // thousand triangles across four cells comes out as ten thousand copies
                // of the same two.
                long fingerprint = Fingerprint(a, b, c);
                if (!seen.Add(fingerprint)) continue;

                kept.Add(a); kept.Add(b); kept.Add(c);
            }

            var mesh = new Mesh();
            if (where.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(where);
            mesh.SetUVs(0, mapped);
            mesh.SetTriangles(kept, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>A number that is the same for one trio of cells however it is ordered.</summary>
        static long Fingerprint(int a, int b, int c)
        {
            int low = Mathf.Min(a, Mathf.Min(b, c));
            int high = Mathf.Max(a, Mathf.Max(b, c));
            int middle = a + b + c - low - high;

            return ((long)low * 1_000_003L + middle) * 1_000_003L + high;
        }

        /// <summary>The wagon's material, from the maps that came with it.</summary>
        static Material Dressed()
        {
            string path = $"{MaterialDir}/BrokenWagon.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "BrokenWagon" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", Load("BrokenWagon"));
            material.SetTexture("_MainTex", Load("BrokenWagon"));

            var normal = Load("BrokenWagon_normal");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            // <b>Lifted, because the map has the light baked into it.</b> A generated
            // model's texture is a photograph of the model under its own lighting, shadow
            // and all: this one averages 94, 80, 67 - a wood the colour of wet bark, next
            // to a Synty crate at about 140, 110, 80. Put down unlifted it read as a
            // burnt-out black smudge beside the bones rather than a wagon.
            //
            // A tint rather than a repainted texture, because the shading in the map is
            // the only thing giving a four-thousand-triangle model its planks back.
            material.SetColor("_BaseColor", new Color(1.45f, 1.42f, 1.38f));
            material.SetColor("_Color", new Color(1.45f, 1.42f, 1.38f));

            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);

            return material;
        }

        static Texture2D Load(string name)
            => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Source}/{name}.png");

        /// <summary>Holds a texture down to a size worth loading.</summary>
        static void Shrink(string path, bool normal)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            importer.maxTextureSize = TextureSize;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;

            importer.SaveAndReimport();
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
