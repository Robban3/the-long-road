using System.Collections.Generic;
using System.Reflection;
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
    /// Takes the cast's last rung off the skinned-mesh line: `The Veil &gt; Bake Cast Poses`.
    ///
    /// <b>Because the far-LOD pass left a two-dozen-face mesh being skinned at three
    /// hundred metres.</b> `Cast Report` was asked who the hundred and sixty-nine skinned
    /// meshes in the worst frame are, and the answer was that a hundred and fifty-three of
    /// them are on their chain's last rung, carrying between twenty-three and forty-six
    /// faces each, standing between seventy-four and three hundred and ninety metres out.
    /// The decimator took the triangles off those figures and left every one of them a
    /// SkinnedMeshRenderer - and the skinned-mesh budget counts renderers, not triangles.
    /// A skinning dispatch for twenty-four faces is the most expensive triangle in the
    /// game.
    ///
    /// So the last rung stops being skinned. Nothing about its geometry changes: this
    /// takes the mesh the chain already draws there, in the pose the game would be drawing
    /// it in, and hangs it on a plain MeshRenderer. The figure stops moving its legs at
    /// the range where it is forty pixels tall and twenty-four faces wide, and in exchange
    /// it stops being skinned - and starts instancing, which a skinned renderer never
    /// does, so the draw-call line gets the same saving for free.
    ///
    /// <b>The pose comes from the game's own animator, not from the file.</b>
    /// `Rig Probe &gt; Rest Poses` was written to ask whether the pose on disk could simply
    /// be frozen, and the answer was no in the clearest possible terms: every human in the
    /// army pack stands 1,56 m wide and 1,90 m tall on disk, which is a rig in a cross,
    /// and the cross survives the decimator - a man-at-arms' last rung is still 1,15 m
    /// across at ninety-six faces. So each model is stood up, given the Animator and the
    /// controller <see cref="RunVisuals"/> would give it, walked for
    /// <see cref="Settle"/> seconds, and baked where it stands.
    ///
    /// <b>And what the pose did is measured, because an animator that moves nothing looks
    /// exactly like one that worked.</b> A Generic rig with no avatar runs, reports a
    /// state and a normalised time, and holds its bind pose - the project has met that
    /// failure before and wrote a warning about it. Here it would silently freeze a
    /// hundred and fifty figures in a cross. So every baked mesh is compared against the
    /// mesh it was baked from, vertex by vertex in the same space, and a rung the pose did
    /// not move is left skinned and named in the sheet.
    ///
    /// Run after <see cref="FarLods.Build"/>, which is what builds the rungs this bakes.
    /// Running it twice is a no-op: the second run finds nothing skinned on a last rung.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.CastPoses.Bake
    /// </summary>
    public static class CastPoses
    {
        /// <summary>Where the posed meshes are written, one asset per model.</summary>
        public const string Dir = "Assets/_Project/Models/Poses";

        /// <summary>Seconds of walk before the snapshot is taken.</summary>
        // Long enough to be out of any entry transition and into the cycle proper, and
        // chosen rather than left at nought because the first frame of a walk is very
        // often the same cross the file was saved in.
        const float Settle = 1.4f;

        /// <summary>The step the animator is driven at, in seconds.</summary>
        const float Step = 1f / 60f;

        /// <summary>
        /// The Speed parameter the animators are given, in metres a second.
        ///
        /// Below AnimatorBuilder's RunFrom, so this is the walk and not the run: the
        /// column walks, and a frozen sprint under a marching column would read worse
        /// than a frozen stride.
        /// </summary>
        const float Walking = 2f;

        /// <summary>
        /// How far a vertex must have moved for the pose to count as having happened, as
        /// a share of the mesh's own size.
        /// </summary>
        // One part in fifty. A walk moves a hand through most of a metre; anything this
        // small is an animator that bound nothing.
        const float Moved = 0.02f;

        [MenuItem("The Veil/Bake Cast Poses")]
        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                                     OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null)
            {
                Debug.LogError("[Pose] PlayLevel has no LevelRunner, so there is no model "
                               + "library to read.");
                return;
            }

            var library = runner.Models;
            if (library == null)
            {
                Debug.LogError("[Pose] the LevelRunner carries no model library.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/_Project/Models", "Poses");

            var sheet = new StringBuilder();
            sheet.AppendLine("[Pose] the cast's last rungs, baked in the walk the game "
                             + "would be playing and hung on plain MeshRenderers");
            sheet.AppendLine($"[Pose] settled {Settle:0.0} s at Speed {Walking:0.0}, and a "
                             + $"rung whose vertices moved less than {Moved * 100f:0}% of the "
                             + "mesh's own size is left skinned");

            int models = 0, froze = 0, rungs = 0, still = 0, boneless = 0, missing = 0;

            foreach (var model in Cast(library))
            {
                var prefab = model.Prefab;
                if (prefab == null) continue;

                string path = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(path)) continue;

                if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0) continue;

                models++;

                var posed = Pose(model, sheet, out int wasStill, out int wasBoneless);

                still += wasStill;
                boneless += wasBoneless;

                if (posed == null || posed.Count == 0)
                {
                    missing++;
                    continue;
                }

                int swapped = Swap(path, prefab.name, posed, sheet);

                if (swapped > 0) { froze++; rungs += swapped; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sheet.AppendLine($"[Pose] {models} skinned models in the library, {froze} of them "
                             + $"with a last rung frozen, {rungs} renderers taken off the "
                             + "skinned line");
            sheet.AppendLine($"[Pose] {still} rungs left skinned because the pose moved them "
                             + $"less than {Moved * 100f:0}%, {boneless} frozen with no "
                             + $"skeleton to pose at all, {missing} models with nothing to bake");

            Debug.Log(sheet.ToString());

            EditorSceneManager.CloseScene(scene, false);
        }

        /// <summary>
        /// Every model the library carries, single fields and arrays alike.
        ///
        /// Read by reflection rather than from a list, because the library names its
        /// models one field at a time - Spearmen, Swordsmen, Archers, Champions - and a
        /// table written out here would be missing whichever one is added next.
        /// </summary>
        static IEnumerable<ActorModel> Cast(VisualLibrary library)
        {
            var found = new List<ActorModel>();

            foreach (var field in typeof(VisualLibrary).GetFields(BindingFlags.Public
                                                                  | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(ActorModel))
                {
                    found.Add((ActorModel)field.GetValue(library));
                    continue;
                }

                if (field.FieldType != typeof(ActorModel[])) continue;

                var many = (ActorModel[])field.GetValue(library);
                if (many == null) continue;

                foreach (var one in many) found.Add(one);
            }

            return found;
        }

        /// <summary>
        /// Stands a model up, walks it, and bakes the last rung of every chain it carries.
        ///
        /// Keyed by the renderer's path inside the prefab, which is what the prefab edit
        /// afterwards matches on - a name alone is not unique and an index is not stable.
        /// </summary>
        static Dictionary<string, Mesh> Pose(ActorModel model, StringBuilder sheet,
                                             out int still, out int boneless)
        {
            still = boneless = 0;

            var stood = (GameObject)PrefabUtility.InstantiatePrefab(model.Prefab);
            if (stood == null) return null;

            try
            {
                Walk(stood, model);

                // <b>The model's own size, measured off what it draws up close.</b> This
                // is what a baked rung is held against, and the two obvious references
                // were both wrong: a mesh's own bounds are in whatever space the pack
                // authored it in, and a skinned renderer's world bounds are its authored
                // animation box put through its root bone - which for a pack imported at
                // a hundredth is not the figure. The near renderers' world boxes are the
                // one reading in this that every other tool in the project agrees with: a
                // deer is 1,02 m and a man-at-arms is 1,94 m, and they say so.
                float span = Span(stood);

                var posed = new Dictionary<string, Mesh>();

                foreach (var group in stood.GetComponentsInChildren<LODGroup>(true))
                {
                    var levels = group.GetLODs();
                    if (levels.Length < 2) continue;

                    var last = levels[levels.Length - 1];
                    if (last.renderers == null) continue;

                    foreach (var renderer in last.renderers)
                    {
                        if (!(renderer is SkinnedMeshRenderer skin)) continue;
                        if (skin.sharedMesh == null) continue;

                        string key = Path(stood.transform, skin.transform);

                        // No skeleton at all, which the tent is: a SkinnedMeshRenderer
                        // whose mesh carries no bindposes and whose bones array is empty.
                        // There is no pose to take and nothing a bake would return, and
                        // the mesh is already in the renderer's own space - so it is
                        // frozen as it stands, which is what it has always looked like.
                        if (skin.bones == null || skin.bones.Length == 0
                            || skin.sharedMesh.bindposes.Length == 0)
                        {
                            posed[key] = Copy(skin.sharedMesh, key);
                            boneless++;
                            continue;
                        }

                        var baked = Bake(skin, span, out float scaled);

                        if (baked == null)
                        {
                            // Neither bake came back the size of the mesh it was taken
                            // from, so where the vertices would land is not known - and a
                            // mesh in the wrong space is not a worse-looking figure, it is
                            // a figure somewhere else or a figure the size of a county.
                            sheet.AppendLine($"[Pose]   {model.Prefab.name,-34} {key,-40} "
                                             + $"left skinned, the bake came back "
                                             + $"{scaled:0.000} of the mesh's own size");

                            still++;
                            continue;
                        }

                        float shifted = Shifted(skin.sharedMesh, baked);

                        // <b>Nought means two opposite things and they must be told
                        // apart.</b> A longbow or a crossbow is a mesh bound rigidly to
                        // one bone - the hand that holds it - and its own vertices
                        // therefore never move relative to each other whatever the figure
                        // does. The rung sits inside that bone's subtree, so a plain mesh
                        // hung there follows the hand exactly as the skinning did: nought
                        // movement is a proof that freezing it is identical, not a warning
                        // that the pose failed. The same nought on a mesh spread over a
                        // skeleton - every nested horse in the cavalry prefabs, whose
                        // rider's controller does not drive it - means the animator bound
                        // nothing and the bake is the bind pose.
                        if (shifted < Moved && Rigid(skin.sharedMesh))
                        {
                            baked.name = key;
                            posed[key] = baked;
                            continue;
                        }

                        if (shifted < Moved)
                        {
                            // Left skinned on purpose. An animator that bound nothing
                            // reports a state and a normalised time and moves no bone, and
                            // freezing that is freezing the cross the file was saved in.
                            sheet.AppendLine($"[Pose]   {model.Prefab.name,-34} {key,-40} "
                                             + $"left skinned, the pose moved it "
                                             + $"{shifted * 100f:0.0}%");

                            Object.DestroyImmediate(baked);
                            still++;
                            continue;
                        }

                        baked.name = key;
                        posed[key] = baked;
                    }
                }

                return posed;
            }
            finally { Object.DestroyImmediate(stood); }
        }

        /// <summary>
        /// Gives a stood-up model the Animator the game would give it and walks it.
        ///
        /// The same three steps <see cref="RunVisuals"/> takes, in the same order and for
        /// the same reasons: the avatar off the model's own file because the army pack's
        /// characters are prefabs assembled from meshes and the avatar belongs to the
        /// mesh's file, and Rebind before Update because an animator that has never been
        /// bound evaluates nothing outside play mode.
        /// </summary>
        static void Walk(GameObject stood, ActorModel model)
        {
            if (model.Animator == null) return;

            var animator = stood.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                animator = stood.AddComponent<Animator>();

                var rig = model.Prefab.GetComponentInChildren<Animator>();
                if (rig != null) animator.avatar = rig.avatar;
            }

            animator.runtimeAnimatorController = model.Animator;
            if (animator.avatar == null && model.Rig != null) animator.avatar = model.Rig;

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            animator.Rebind();
            animator.Update(0f);

            animator.SetFloat("Speed", Walking);

            for (float at = 0f; at < Settle; at += Step) animator.Update(Step);
        }

        /// <summary>
        /// A posed snapshot of a skinned renderer, in the space its own GameObject sits in.
        ///
        /// <b>Which of BakeMesh's two spaces that is cannot be reasoned out, so the swap
        /// is rehearsed instead.</b> The flag is documented as "use the renderer's scale",
        /// and the packs in this project disagree about where the scale lives: the army
        /// characters stand at one, and the forest animals are imported at a hundredth
        /// with their meshes authored a hundred times over. Two attempts to settle it by
        /// argument both came out wrong, and the second one shipped a deer whose last
        /// rung drew **one pixel** at sixty-two metres where it had drawn forty-three.
        ///
        /// Comparing the baked mesh against the mesh it came from does not catch that
        /// either: in mesh space the two are the same size, and the error is in what the
        /// transform does to them afterwards. So this does the whole swap on a throwaway
        /// object beside the renderer - same parent, same local place - and reads the
        /// world box the engine would actually draw. That box must be in the same country
        /// as the skinned renderer's own, which is the figure's animation box and so is
        /// generous rather than exact: hence a wide window rather than a tight one. A
        /// bake that lands outside it is thrown away, and if neither does, the caller is
        /// told and the rung stays skinned.
        /// </summary>
        static Mesh Bake(SkinnedMeshRenderer skin, float span, out float scaled)
        {
            float want = Mathf.Max(0.0001f, span);

            scaled = 0f;
            Mesh kept = null;

            foreach (bool useScale in new[] { false, true })
            {
                var baked = new Mesh();
                skin.BakeMesh(baked, useScale);

                if (baked.vertexCount == 0) { Object.DestroyImmediate(baked); continue; }

                float ratio = Hung(skin, baked) / want;

                // Kept for the message when both are refused: the first reading is the
                // more informative one, and a nought would say nothing at all.
                if (scaled <= 0f) scaled = ratio;

                // <b>A thirtieth to one and a half, and the low end is a measured gap
                // rather than a principle.</b> A last rung is honestly a fraction of the
                // figure - a man-at-arms' is 0,95 m across a figure of 1,94, a knight's
                // helmet piece a sixteenth of a mounted model - so the floor cannot be
                // near one. Run against the whole cast, the genuine small pieces land
                // between 0,061 and 0,116 and the two that are in the wrong space land at
                // 0,013, which is a clear fourfold gap with nothing in it. The floor sits
                // in that gap. The high end is barely over one because nothing cut out of
                // a model can be bigger than the model.
                if (ratio < 0.03f || ratio > 1.5f) { Object.DestroyImmediate(baked); continue; }

                scaled = ratio;
                kept = baked;
                break;
            }

            return kept;
        }

        /// <summary>
        /// How big the model draws up close, taken off its near renderers' world boxes.
        /// </summary>
        static float Span(GameObject stood)
        {
            var box = new Bounds();
            bool any = false;

            foreach (var renderer in stood.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("Far LOD ")) continue;

                if (!any) { box = renderer.bounds; any = true; }
                else box.Encapsulate(renderer.bounds);
            }

            return any ? box.size.magnitude : 0f;
        }

        /// <summary>
        /// The world size a mesh would draw at if it were hung where the swap will hang
        /// it: a plain renderer on a throwaway object at the skinned renderer's own place.
        /// </summary>
        static float Hung(SkinnedMeshRenderer skin, Mesh mesh)
        {
            var tried = new GameObject("Pose Rehearsal");

            try
            {
                tried.transform.SetParent(skin.transform.parent, false);
                tried.transform.localPosition = skin.transform.localPosition;
                tried.transform.localRotation = skin.transform.localRotation;
                tried.transform.localScale = skin.transform.localScale;

                tried.AddComponent<MeshFilter>().sharedMesh = mesh;

                return tried.AddComponent<MeshRenderer>().bounds.size.magnitude;
            }
            finally { Object.DestroyImmediate(tried); }
        }

        /// <summary>
        /// How far the bake moved the mesh, as a share of the mesh's own size.
        ///
        /// Vertex by vertex in the same space and against the same vertex, which is the
        /// only comparison that tells a pose from a bind pose: a box would not catch a
        /// figure whose arms came down and whose legs went the same distance apart.
        /// </summary>
        static float Shifted(Mesh from, Mesh baked)
        {
            var was = from.vertices;
            var now = baked.vertices;

            int count = Mathf.Min(was.Length, now.Length);
            if (count == 0) return 0f;

            float size = Mathf.Max(0.0001f, from.bounds.size.magnitude);
            float worst = 0f;

            for (int i = 0; i < count; i++)
            {
                float away = (now[i] - was[i]).magnitude;
                if (away > worst) worst = away;
            }

            return worst / size;
        }

        /// <summary>
        /// Whether a mesh hangs off a single bone, so that no pose can deform it.
        ///
        /// Asked of the weights and not of the bones array: the array is the whole
        /// skeleton on every renderer of a model, and a weapon's mesh references one
        /// entry of it.
        /// </summary>
        static bool Rigid(Mesh mesh)
        {
            var weights = mesh.GetAllBoneWeights();
            if (weights.Length == 0) return true;

            int only = -1;

            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i].weight <= 0.001f) continue;

                if (only < 0) only = weights[i].boneIndex;
                else if (weights[i].boneIndex != only) return false;
            }

            return true;
        }

        /// <summary>A standalone copy of a mesh, for a renderer there is no pose to take of.</summary>
        static Mesh Copy(Mesh from, string name)
        {
            var copy = Object.Instantiate(from);
            copy.name = name;
            return copy;
        }

        /// <summary>
        /// Swaps a prefab's last-rung skinned renderers for plain ones holding the posed
        /// meshes, and writes the meshes beside it.
        ///
        /// The GameObject is kept and only its renderer is replaced, because that object
        /// is where the pass that built the rung put it - at the source renderer's own
        /// local place, under the source renderer's own parent - and the baked mesh is in
        /// exactly that space.
        /// </summary>
        static int Swap(string path, string name, Dictionary<string, Mesh> posed,
                        StringBuilder sheet)
        {
            // Before the delete below, and not only in the caller. A second run with no
            // rungs to bake would otherwise delete the asset holding the meshes the
            // prefab is already drawing, and the prefab would keep a null mesh.
            if (posed.Count == 0) return 0;

            string at = $"{Dir}/{name}_Pose.asset";

            // Written whole rather than edited, because the set of rungs can change
            // between runs and a stale sub-asset is a mesh nothing draws.
            if (AssetDatabase.LoadAssetAtPath<Mesh>(at) != null) AssetDatabase.DeleteAsset(at);

            bool first = true;

            foreach (var pair in posed)
            {
                if (first) { AssetDatabase.CreateAsset(pair.Value, at); first = false; }
                else AssetDatabase.AddObjectToAsset(pair.Value, at);
            }

            if (first) return 0;

            AssetDatabase.SaveAssets();

            var contents = PrefabUtility.LoadPrefabContents(path);
            int swapped = 0;

            try
            {
                foreach (var group in contents.GetComponentsInChildren<LODGroup>(true))
                {
                    var levels = group.GetLODs();
                    if (levels.Length < 2) continue;

                    var last = levels[levels.Length - 1];
                    if (last.renderers == null) continue;

                    var kept = new List<Renderer>();
                    bool changed = false;

                    foreach (var renderer in last.renderers)
                    {
                        if (renderer == null) continue;

                        if (!(renderer is SkinnedMeshRenderer skin))
                        {
                            kept.Add(renderer);
                            continue;
                        }

                        string key = Path(contents.transform, skin.transform);

                        if (!posed.TryGetValue(key, out var mesh) || mesh == null)
                        {
                            kept.Add(renderer);
                            continue;
                        }

                        var stood = skin.gameObject;
                        var paints = skin.sharedMaterials;
                        var shadows = skin.shadowCastingMode;
                        bool takes = skin.receiveShadows;

                        Object.DestroyImmediate(skin);

                        stood.AddComponent<MeshFilter>().sharedMesh = mesh;

                        var plain = stood.AddComponent<MeshRenderer>();
                        plain.sharedMaterials = paints;
                        plain.shadowCastingMode = shadows;
                        plain.receiveShadows = takes;

                        kept.Add(plain);
                        swapped++;
                        changed = true;
                    }

                    if (!changed) continue;

                    last.renderers = kept.ToArray();
                    levels[levels.Length - 1] = last;
                    group.SetLODs(levels);
                }

                if (swapped > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);

                    sheet.AppendLine($"[Pose] {name,-40} {swapped,2} renderers frozen, "
                                     + $"{posed.Count,2} meshes written");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            return swapped;
        }

        /// <summary>A transform's path below a root, which is unique where a name is not.</summary>
        static string Path(Transform root, Transform of)
        {
            var parts = new List<string>();

            for (var up = of; up != null && up != root; up = up.parent) parts.Add(up.name);

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
