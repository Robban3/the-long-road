using System.Collections.Generic;
using System.Text;
using TheVeil.App;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TheVeil.Editor
{
    /// <summary>
    /// What a character would look like at each far level, at the range that level is
    /// actually drawn: `The Veil &gt; Cast LOD Proof`.
    ///
    /// <b>Because the one question here is not a measurement.</b>
    /// <see cref="MeshDecimator"/> carries the rig now, so <see cref="FarLods"/> could be
    /// let loose on the 36 character prefabs that have no LOD chain at all. The reason not
    /// to do that blind is arithmetic rather than caution. The rung table is written in
    /// screen share, which is the right quantity, but it was calibrated against scenery:
    ///
    ///       rung 1    900 faces   shown below 0,090 of screen height
    ///       rung 2    250 faces   shown below 0,035
    ///       rung 3     80 faces   below that, to the horizon
    ///
    /// A soldier is 1,8 m tall and a meadow tree is 18 m across, so at the camera's own
    /// resting range of 62 m the tree is at 0,311 of screen height and keeps a hand-made
    /// level, and the soldier is at 0,031 - **under the last threshold, so he would be
    /// drawn at eighty faces, seventy-five pixels tall, for the whole of normal play.**
    ///
    /// Whether eighty faces at seventy-five pixels is a soldier or a paper doll is for
    /// somebody to look at. So this writes the pictures and nothing else: no prefab is
    /// touched, no mesh is saved, and the only thing it leaves behind is a folder of PNGs
    /// and a sheet of numbers.
    ///
    /// <b>Drawn the way the game draws it.</b> Each level is photographed at the distance
    /// that level is actually shown at and at the pixel height it actually occupies, then
    /// scaled up for viewing - and the sheet says by how much. The texture work learned
    /// this the hard way: proof crops drawn nearest-neighbour asked the user to judge a
    /// harsher picture than the phone would ever show.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.CastLodProof.Run
    /// </summary>
    public static class CastLodProof
    {
        /// <summary>Where the pictures and the sheet go.</summary>
        const string ProofDir = "Build/CastLod";

        /// <summary>The lens the game ships, as a vertical field of view.</summary>
        const float Fov = 50f;

        /// <summary>The screen this is measured against, in pixels.</summary>
        // A 1080x2400 phone held upright, which is what the budget's own aspect ratios in
        // BudgetReport are taken from. The whole frame is rendered at this size and the
        // figure is then cut out of it, because how many pixels the figure gets is a
        // property of the frame it stands in.
        const int ScreenHigh = 2400;
        const int ScreenWide = 1080;

        /// <summary>How much each picture is blown up so a person can see it.</summary>
        const int Magnify = 4;

        /// <summary>
        /// The characters this is shown for.
        ///
        /// Four and not thirty-six: a levy, a knight, a mounted figure and a champion.
        /// They span the range of what the escort and what stands against it are made of -
        /// a plain man with a spear, a man in plate, something with a horse under it, and
        /// the heaviest single figure in the game. A judgement made on all four holds for
        /// the rest, and a judgement made on one does not.
        /// </summary>
        static readonly string[] Shown =
        {
            "MC_Levy_01",
            "MC_Knight_01",
            "MC_Cavalry_NobleCavalry",
            "TheVeil_Champion_00_Forest",
        };

        [MenuItem("The Veil/Cast LOD Proof")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                                     OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null)
            {
                Debug.LogError("[Proof] PlayLevel has no LevelRunner, so there is no library.");
                return;
            }

            System.IO.Directory.CreateDirectory(ProofDir);

            var sheet = new StringBuilder();
            sheet.AppendLine("[Proof] what a character looks like at each far level, at the "
                             + "range that level is drawn");
            sheet.AppendLine($"[Proof] lens {Fov:0} degrees, a {ScreenHigh} pixel screen, "
                             + $"pictures blown up {Magnify}x so they can be seen");
            sheet.AppendLine($"[Proof] the camera rests at {CameraOrbit.DefaultRange:0} m and "
                             + $"pulls back to {CameraOrbit.MaxRange:0} m");

            var models = Models(runner);
            int written = 0;

            foreach (string want in Shown)
            {
                if (!models.TryGetValue(want, out var prefab))
                {
                    sheet.AppendLine($"[Proof] {want}: not in the library under that name");
                    continue;
                }

                written += Show(prefab, sheet);
            }

            sheet.AppendLine($"[Proof] {written} pictures in {ProofDir}");
            Debug.Log(sheet.ToString());

            EditorSceneManager.CloseScene(scene, false);
        }

        /// <summary>
        /// Every model the cast library points at, by name.
        ///
        /// Read off the library rather than loaded by path, so this shows what the game
        /// actually stands up. A name that is not there is said so in the sheet rather
        /// than quietly skipped.
        /// </summary>
        static Dictionary<string, GameObject> Models(LevelRunner runner)
        {
            var found = new Dictionary<string, GameObject>();

            var props = new HashSet<GameObject>();
            FarLods.Gather(runner.Models, props);

            foreach (var prop in props)
                if (prop != null) found[prop.name] = prop;

            return found;
        }

        /// <summary>
        /// One character, photographed at its own LOD0 and at each rung the far-LOD tool
        /// would give it.
        /// </summary>
        static int Show(GameObject prefab, StringBuilder sheet)
        {
            var stood = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (stood == null) return 0;

            int written = 0;

            try
            {
                float tall = Height(stood);
                if (tall <= 0.001f)
                {
                    sheet.AppendLine($"[Proof] {prefab.name}: nothing to measure its height by");
                    return 0;
                }

                var renderers = new List<Renderer>();
                foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null && renderer.gameObject.activeSelf) renderers.Add(renderer);

                int faces = 0;
                foreach (var renderer in renderers) faces += FarLods.Faces(renderer);

                sheet.AppendLine($"[Proof] {prefab.name}: {tall:0.00} m tall, {faces} faces, "
                                 + $"{renderers.Count} renderer(s)");

                // LOD0 at both camera ranges, which is what the game draws today, since
                // these prefabs have no chain at all.
                foreach (float range in new[] { CameraOrbit.DefaultRange, CameraOrbit.MaxRange })
                {
                    if (Shoot(stood, tall, range, $"{prefab.name}-LOD0-{range:0}m", faces, sheet))
                        written++;
                }

                sheet.AppendLine($"[Proof]   and what the rungs would be:");

                foreach (var rung in FarLods.Rungs)
                {
                    // The distance that rung starts being drawn at, from its own
                    // threshold: share = height / (distance * lens), so the rung takes
                    // over beyond height / (share * lens). The last rung's threshold is
                    // nought and it runs to the horizon, so it is shown at both camera
                    // ranges instead.
                    float from = rung.Below > 0.0001f ? tall / (rung.Below * FarLods.Lens) : 0f;

                    var ranges = from > 0.0001f
                        ? new[] { Mathf.Max(from, CameraOrbit.DefaultRange) }
                        : new[] { CameraOrbit.DefaultRange, CameraOrbit.MaxRange };

                    foreach (float range in ranges)
                    {
                        if (range > CameraOrbit.MaxRange)
                        {
                            sheet.AppendLine($"[Proof]   {rung.Faces,4} faces: starts at "
                                             + $"{from:0.0} m, past the camera's own reach - "
                                             + "never drawn");
                            continue;
                        }

                        // <b>A fresh copy for every picture.</b> The first version stood
                        // one model up and swapped its meshes from rung to rung, putting
                        // the originals back in between and destroying the simplified ones
                        // - and every skinned renderer then refused to draw, eighty times
                        // over, with the same "vertex stride" complaint the decimator had
                        // been cured of. The decimator was fine: a probe cut the same
                        // model to every budget from two thousand faces down to
                        // twenty-four and drew all of them. It was this loop handing a
                        // renderer a mesh and then deleting it under the renderer.
                        var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        if (copy == null) continue;

                        try
                        {
                            var mine = new List<Renderer>();
                            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                                if (renderer != null && renderer.gameObject.activeSelf)
                                    mine.Add(renderer);

                            int made = Thin(mine, faces, rung.Faces);

                            string at = $"{prefab.name}-{rung.Faces}f-{range:0}m";
                            if (Shoot(copy, tall, range, at, made, sheet)) written++;
                        }
                        finally { Object.DestroyImmediate(copy); }
                    }
                }
            }
            finally { Object.DestroyImmediate(stood); }

            return written;
        }

        /// <summary>
        /// Every renderer swapped for a simplified copy of its own mesh, in memory.
        ///
        /// Nothing is written to disk and nothing is reparented - the renderer keeps its
        /// place, its bones and its materials, and only the mesh it points at changes. So
        /// the picture is of this model at this many faces and not of a new object that
        /// happens to look similar.
        /// </summary>
        static int Thin(List<Renderer> renderers, int nearFaces, int budget)
        {
            int made = 0;

            foreach (var renderer in renderers)
            {
                int faces = FarLods.Faces(renderer);
                if (faces <= 0) continue;

                int share = Mathf.Max(FarLods.Least, (int)((long)budget * faces / nearFaces));
                if (share >= faces) { made += faces; continue; }

                var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>();

                var source = skin != null ? skin.sharedMesh
                                          : filter != null ? filter.sharedMesh : null;
                if (source == null) continue;

                var thin = MeshDecimator.Simplify(source, share, out _);
                if (thin == null) continue;

                if (skin != null) skin.sharedMesh = thin;
                else filter.sharedMesh = thin;

                made += thin.triangles.Length / 3;
            }

            return made;
        }

        /// <summary>
        /// The same question asked of the two trees that are now most of the worst frame
        /// in the game: `The Veil &gt; Scenery LOD Proof`.
        ///
        /// <b>Because this one cannot be settled by counting either.</b>
        /// `SM_Env_Tree_Meadow_01` carries a hand-made LOD1 of 12 700 faces which draws
        /// below 0,138 of the screen, and `SM_Env_Tree_Meadow_02` one of 6 464 below
        /// 0,125. The camera stands 62 to 120 m back, so a 14 m tree is between 0,09 and
        /// 0,31 of screen height for the whole of normal play - which is to say those two
        /// levels are what the game actually draws, and in the heaviest frame in the game
        /// they are 59 per cent of it over 42 copies.
        ///
        /// <see cref="FarLods"/> spares them because they are hand-made and above
        /// <c>KeepAbove</c>. Taking them at a quarter of the screen instead - the share at
        /// which that tool already decides a model with no chain may drop to nine hundred
        /// faces - would cut most of those 42 copies by fourteen times. Whether nine
        /// hundred faces at that size is a tree or a stick is for somebody to look at, so
        /// this writes the pictures: the level as it is drawn today beside the candidates,
        /// at the pixel size the phone would give them.
        ///
        /// Headless: unity run . -- -executeMethod TheVeil.Editor.CastLodProof.Scenery
        /// </summary>
        [MenuItem("The Veil/Scenery LOD Proof")]
        public static void Scenery()
        {
            System.IO.Directory.CreateDirectory(ProofDir);

            var sheet = new StringBuilder();
            sheet.AppendLine("[Proof] what the spared hand-made level draws today and what "
                             + "this tool's rungs would draw in its place");
            sheet.AppendLine($"[Proof] lens {Fov:0} degrees, a {ScreenHigh} pixel screen, "
                             + $"pictures blown up {Magnify}x so they can be seen");

            // At the phone's own lod bias, because which level the engine would pick is
            // the whole reason a picture is taken at a distance at all - and the editor
            // opens on Ultra at 2,00 against Android's 0,70.
            int wasQuality = BudgetReport.Phone(sheet, "Proof");

            int written = 0;

            // <b>At the heights the dressing actually stands them at, which is not the
            // prefab's own.</b> `TerrainDecorator` fits every tree to PineHeight times a
            // jitter between 0,55 and 1,7, so a meadow tree in the country is anywhere
            // from 4,7 m to 14,5 m tall and its share of the screen varies threefold with
            // it. Photographing the prefab at its authored 14,9 m was the second wrong
            // answer this instrument gave: a middling copy is already on this tool's own
            // rung at every range the camera can stand, and only the tall ones draw the
            // hand-made level at all.
            foreach (float tall in new[] { TerrainDecorator.PineHeight,
                                           TerrainDecorator.PineHeight
                                           * TerrainDecorator.TreeJitterHigh })
            {
                foreach (string path in Scenes)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (prefab == null)
                    {
                        sheet.AppendLine($"[Proof] {path}: not there");
                        continue;
                    }

                    written += Standing(prefab, tall, sheet);
                }
            }

            sheet.AppendLine($"[Proof] {written} pictures in {ProofDir}");

            QualitySettings.SetQualityLevel(wasQuality, true);

            Debug.Log(sheet.ToString());
        }

        /// <summary>The scenery this is shown for: the two trees and nothing else.</summary>
        static readonly string[] Scenes =
        {
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Meadow_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Meadow_02.prefab",
        };

        /// <summary>
        /// One scenery model: its own levels as the engine would pick them, then the
        /// candidates for the band this tool would take over.
        ///
        /// <b>One level at a time, which a chained prefab does not do on its own.</b>
        /// An <c>LODGroup</c> leaves every level's renderers enabled and chooses one when
        /// it draws; a prefab stood up in a scene and photographed by a camera of our own
        /// draws all of them at once, one inside the other. So every renderer outside the
        /// level being judged is switched off first - the same fact that made a count of
        /// skinned meshes read three times too high twice before.
        /// </summary>
        static int Standing(GameObject prefab, float stand, StringBuilder sheet)
        {
            var reading = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (reading == null) return 0;

            int written = 0;

            try
            {
                ModelScaling.Fit(reading, stand);

                float tall = Height(reading);
                var group = reading.GetComponent<LODGroup>();

                if (group == null || tall <= 0.001f)
                {
                    sheet.AppendLine($"[Proof] {prefab.name}: no chain to read");
                    return 0;
                }

                var levels = group.GetLODs();

                // <b>The engine picks a level on the widest axis, not on the height, and
                // the first version of this asked the height.</b> A meadow tree is 15 m
                // tall and 21 m across, so the two answers are half an octave apart - and
                // the sheet confidently photographed LOD2 of the second tree where the
                // frame listing in `CastReport`, which is the engine's own choice, says
                // LOD1 is what draws at that range. Checked against that known answer
                // before any picture was looked at. The height is still what decides how
                // many pixels the thing covers, so both are read and each is used for its
                // own question.
                float wide = Spread(reading);

                sheet.AppendLine($"[Proof] {prefab.name} stood at {tall:0.00} m tall, "
                                 + $"{wide:0.00} m across its widest axis, "
                                 + $"{levels.Length} levels");

                // Where the camera can actually be, and the distance a quarter of the
                // screen falls at - the three distances the judgement is about.
                float quarter = wide * QualitySettings.lodBias / (0.25f * FarLods.Lens);

                var ranges = new List<float> { CameraOrbit.DefaultRange };
                if (quarter > CameraOrbit.DefaultRange && quarter < CameraOrbit.MaxRange)
                    ranges.Add(quarter);
                ranges.Add(CameraOrbit.MaxRange);

                for (int lod = 0; lod < levels.Length; lod++)
                {
                    int faces = Isolated(reading, levels, lod);
                    if (faces <= 0) continue;

                    float below = levels[lod].screenRelativeTransitionHeight;

                    foreach (float range in ranges)
                    {
                        // Only where that level is the one the engine would pick, so no
                        // picture here is of something the player never sees.
                        float share = wide * QualitySettings.lodBias
                                      / (range * FarLods.Lens);

                        float over = lod > 0
                            ? levels[lod - 1].screenRelativeTransitionHeight : 1f;

                        if (share > over || share <= below) continue;

                        string at = $"{prefab.name}-LOD{lod}-{faces}f-{range:0}m";
                        if (Shoot(reading, tall, range, at, faces, sheet)) written++;
                    }
                }

                // And the candidates, cut from the level the tool would cut from, shown at
                // the same distances. 900 is the first rung; 2500 is the obvious thing to
                // reach for if 900 will not do, and asking the question without an answer
                // to compare against is how a picture gets judged against nothing.
                foreach (int budget in new[] { 2500, 900 })
                {
                    foreach (float range in ranges)
                    {
                        if (range < quarter - 0.5f) continue;

                        var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        if (copy == null) continue;

                        try
                        {
                            ModelScaling.Fit(copy, stand);

                            var chain = copy.GetComponent<LODGroup>();
                            var theirs = chain != null ? chain.GetLODs() : null;
                            if (theirs == null || theirs.Length == 0) continue;

                            int from = Isolated(copy, theirs, 0);
                            if (from <= 0) continue;

                            var live = new List<Renderer>();
                            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                                if (renderer != null && renderer.enabled
                                    && renderer.gameObject.activeInHierarchy)
                                    live.Add(renderer);

                            // A control picture of the same isolated level before anything
                            // is cut, because the first run of this wrote two empty skies
                            // and an empty sky does not say whether the budget drew
                            // nothing or the instrument pointed at nothing.
                            if (budget == 2500)
                            {
                                string whole = $"{prefab.name}-cutfrom-{from}f-{range:0}m";
                                if (Shoot(copy, tall, range, whole, from, sheet)) written++;
                            }

                            int made = Thin(live, from, budget);

                            var box = new Bounds(copy.transform.position, Vector3.zero);
                            foreach (var renderer in live)
                                if (renderer != null) box.Encapsulate(renderer.bounds);

                            sheet.AppendLine($"[Proof]   cut {live.Count} renderer(s) from "
                                             + $"{from} faces to {made}, bounds {box.size.x:0.0}"
                                             + $" x {box.size.y:0.0} x {box.size.z:0.0} m");

                            string at = $"{prefab.name}-rung{budget}-{made}f-{range:0}m";
                            if (Shoot(copy, tall, range, at, made, sheet)) written++;
                        }
                        finally { Object.DestroyImmediate(copy); }
                    }
                }
            }
            finally { Object.DestroyImmediate(reading); }

            return written;
        }

        /// <summary>
        /// Everything but one level switched off, and that level's face count returned.
        ///
        /// <b>The renderer is switched off, not its object, and the group is switched off
        /// before either - between them they wrote five empty skies.</b> This pack hangs a
        /// tree's LOD0 renderers under the object that carries its LOD1 one, so
        /// deactivating the LOD1 object took LOD0's down the hierarchy with it. And an
        /// <c>LODGroup</c> owns the <c>enabled</c> flag of every renderer it lists: it
        /// turns the level it has chosen on and the rest off, so an instrument that sets
        /// those flags itself is overruled by the thing it is trying to photograph. The
        /// group is disabled first, and only then does a renderer's own flag mean
        /// anything. A control picture of the same level with nothing cut is what told
        /// "the budget drew nothing" from "the instrument pointed at nothing" - without
        /// it the reading would have been that nine hundred faces is a blank sky.
        /// </summary>
        static int Isolated(GameObject stood, LOD[] levels, int lod)
        {
            foreach (var group in stood.GetComponentsInChildren<LODGroup>(true))
                if (group != null) group.enabled = false;

            var wanted = new HashSet<Renderer>();

            if (levels[lod].renderers != null)
                foreach (var renderer in levels[lod].renderers)
                    if (renderer != null) wanted.Add(renderer);

            int faces = 0;

            foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;

                bool keep = wanted.Contains(renderer);
                renderer.enabled = keep;

                if (keep) faces += FarLods.Faces(renderer);
            }

            return faces;
        }

        /// <summary>
        /// The widest axis of everything it has, which is the size an LOD group compares
        /// against its thresholds.
        /// </summary>
        static float Spread(GameObject stood)
        {
            bool any = false;
            var box = new Bounds();

            foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy) continue;

                if (!any) { box = renderer.bounds; any = true; }
                else box.Encapsulate(renderer.bounds);
            }

            if (!any) return 0f;

            var size = box.size;
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }

        /// <summary>How tall the thing is, over every renderer it has.</summary>
        static float Height(GameObject stood)
        {
            bool any = false;
            var box = new Bounds();

            foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy) continue;

                if (!any) { box = renderer.bounds; any = true; }
                else box.Encapsulate(renderer.bounds);
            }

            return any ? box.size.y : 0f;
        }

        /// <summary>
        /// One picture, at the pixel size the thing really occupies on a phone, blown up.
        ///
        /// <b>The size is the whole point.</b> A character rendered into a large image and
        /// looked at closely is a question nobody is ever asked. The figure's share of
        /// screen height at this distance decides how many pixels tall it is, and the
        /// picture is that many pixels and then magnified - so what is being judged is the
        /// detail the phone would have, not the detail the mesh has.
        /// </summary>
        static bool Shoot(GameObject stood, float tall, float range, string name,
                          int faces, StringBuilder sheet)
        {
            float share = tall / (range * FarLods.Lens);
            int high = Mathf.Max(8, Mathf.RoundToInt(share * ScreenHigh));

            var box = new Bounds(stood.transform.position, Vector3.zero);
            bool any = false;

            foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy) continue;
                if (!any) { box = renderer.bounds; any = true; }
                else box.Encapsulate(renderer.bounds);
            }

            var eye = new GameObject("Proof camera");

            try
            {
                var camera = eye.AddComponent<Camera>();

                camera.fieldOfView = Fov;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = range * 4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);

                // Straight at it from the front, at the real distance, with the figure
                // centred. A raised camera would be the game's own angle and would also
                // foreshorten the thing being judged.
                camera.transform.position = box.center + new Vector3(0f, 0f, -range);
                camera.transform.LookAt(box.center);

                // <b>The whole phone frame is rendered and then cropped, and the first
                // version of this did not.</b> It made the picture as many pixels as the
                // figure occupies - eighty-one - and then rendered the camera's entire
                // fifty-eight metre field of view into those eighty-one pixels, so the
                // figure came out three pixels tall in an empty sky. The pixel size of the
                // figure is a property of the frame it sits in, not of the image file.
                var rt = new RenderTexture(ScreenWide, ScreenHigh, 24);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                var whole = new Texture2D(ScreenWide, ScreenHigh, TextureFormat.RGB24, false);
                whole.ReadPixels(new Rect(0, 0, ScreenWide, ScreenHigh), 0, 0);
                whole.Apply();
                RenderTexture.active = null;

                // <b>Where the figure is in the frame, asked while the frame is still what
                // the camera is pointed at.</b> WorldToScreenPoint answers in the camera's
                // own pixel rect, and that is the render texture's size only while the
                // render texture is attached. Clearing it first and asking afterwards -
                // which is what this did - gets an answer in the resolution of whatever
                // display the editor thinks it has, so the crop lands somewhere else
                // entirely and comes back a patch of empty sky out of a picture that has
                // the figure in it. Every proof image was blank twice over for two
                // different reasons, and this was the second.
                var rect = OnScreen(camera, box);

                // The whole phone frame at one to one as well, unmagnified. The crops
                // below are blown up so the pixels can be counted, and a blown-up crop
                // reads as bad graphics to anyone who has not been told what it is. This
                // is the picture that says how big the figure really is.
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(ProofDir, name + "-frame.png"), whole.EncodeToPNG());

                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);

                var small = Cropped(whole, rect, out int cropHigh);
                Object.DestroyImmediate(whole);

                high = cropHigh;

                var big = Blown(small, Magnify);
                string path = System.IO.Path.Combine(ProofDir, name + ".png");
                System.IO.File.WriteAllBytes(path, big.EncodeToPNG());

                Object.DestroyImmediate(small);
                Object.DestroyImmediate(big);

                sheet.AppendLine($"[Proof]   {name,-44} {faces,6} faces, "
                                 + $"{high,4} px tall at {range:0} m "
                                 + $"(share {share:0.000})");

                return true;
            }
            finally { Object.DestroyImmediate(eye); }
        }


        /// <summary>
        /// Where the model's bounding box lands in the frame, in that frame's own pixels.
        ///
        /// The crop is then as many pixels tall as the figure is - which is the number the
        /// sheet prints and the number the whole question is about.
        /// </summary>
        static Rect OnScreen(Camera camera, Bounds box)
        {
            float left = float.MaxValue, right = float.MinValue;
            float down = float.MaxValue, up = float.MinValue;

            // All eight corners, because a box projects to a hexagon and taking the centre
            // and the extents would be wrong the moment the camera is not square on.
            for (int i = 0; i < 8; i++)
            {
                var corner = box.center + Vector3.Scale(box.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1,
                                (i & 2) == 0 ? -1 : 1,
                                (i & 4) == 0 ? -1 : 1));

                var at = camera.WorldToScreenPoint(corner);

                if (at.x < left) left = at.x;
                if (at.x > right) right = at.x;
                if (at.y < down) down = at.y;
                if (at.y > up) up = at.y;
            }

            return Rect.MinMaxRect(left, down, right, up);
        }

        /// <summary>
        /// The figure cut out of the phone frame, with a little air around it.
        /// </summary>
        static Texture2D Cropped(Texture2D whole, Rect where, out int high)
        {
            float left = where.xMin, right = where.xMax;
            float down = where.yMin, up = where.yMax;

            // A quarter of the figure's height of air on every side, so it is clear that
            // the whole of it is in the picture and nothing has been cut off.
            float air = Mathf.Max(4f, (up - down) * 0.25f);

            int x0 = Mathf.Clamp(Mathf.FloorToInt(left - air), 0, whole.width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(right + air), 0, whole.width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(down - air), 0, whole.height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(up + air), 0, whole.height - 1);

            int wide = Mathf.Max(1, x1 - x0 + 1);
            int tall = Mathf.Max(1, y1 - y0 + 1);

            high = Mathf.Max(1, Mathf.RoundToInt(up - down));

            var cut = new Texture2D(wide, tall, TextureFormat.RGB24, false);
            cut.SetPixels(whole.GetPixels(x0, y0, wide, tall));
            cut.Apply();

            return cut;
        }

        /// <summary>
        /// The picture at several times the size, in blocks.
        ///
        /// <b>Blocks on purpose here, which is the opposite of the texture proofs.</b>
        /// There the question was what a smaller texture looks like when the phone samples
        /// it smoothly, so smoothing was the honest draw. Here the picture already is what
        /// the phone would put on the glass, pixel for pixel, and the magnification exists
        /// only so a person can see those pixels. Smoothing them would invent detail the
        /// phone does not have.
        /// </summary>
        static Texture2D Blown(Texture2D small, int by)
        {
            int wide = small.width * by, high = small.height * by;
            var big = new Texture2D(wide, high, TextureFormat.RGB24, false);

            var from = small.GetPixels32();
            var to = new Color32[wide * high];

            for (int y = 0; y < high; y++)
                for (int x = 0; x < wide; x++)
                    to[y * wide + x] = from[(y / by) * small.width + (x / by)];

            big.SetPixels32(to);
            big.Apply();
            return big;
        }
    }
}
