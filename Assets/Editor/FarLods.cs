using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TheVeil.App;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Cheap far levels for the scenery, and a chain for the props that have none:
    /// `The Veil &gt; Far LOD Survey` and `The Veil &gt; Build Far LODs`.
    ///
    /// <b>Because the budget is thirty times over and this is where it is lost.</b>
    /// <see cref="BudgetReport"/> measured the heaviest frame in the game and split it by
    /// model. It is not a count of props and it is not the wildflower patches that looked
    /// like the villain: it is trees at distance and rocks with no chain. The meadow tree
    /// is 17,9 m across with transitions at 0,371 / 0,138 / 0,039, which at the shipped
    /// 50 degree lens puts its last level out at 489 m - and a level is 64 tiles, 256 m.
    /// So every tree on the map is always at LOD2 or nearer, distance drops nothing, and
    /// LOD2 is six thousand triangles. These are a desktop pack's levels on a phone's
    /// budget. The rock piles are worse: no LODGroup at all, so they draw their full mesh
    /// from any range, two meshes for eleven per cent of the frame.
    ///
    /// The cure is not a thinner country. It is a chain that finishes inside the country
    /// it is standing in, with a far level cheap enough to be worth reaching.
    ///
    /// <b>The survey runs first and on purpose.</b> The sets are read off the scene, and
    /// what they point at decides what this tool can do at all - a prefab can be given an
    /// LODGroup and an FBX cannot. Writing the builder against a guess about that is how
    /// an afternoon goes missing, so the survey is a separate menu item and it writes
    /// nothing.
    ///
    /// <b>It missed the cast for a fortnight.</b> The first version gathered models from
    /// the PropSet fields of every BiomeDecor and nothing else, so it rebuilt 398 chains
    /// of which not one was a deer, a wolf, a wagon or a knight: the column, the escort,
    /// the enemies, the animals and the crows hang off <see cref="VisualLibrary"/>
    /// instead. <see cref="CastReport"/> then counted them in the frame and they are
    /// 300-750k triangles of it. The library is gathered now too.
    ///
    /// <b>Most of what that adds is skinned, and for a day this tool refused it.</b>
    /// MeshDecimator dropped the rig: a character run through it came back with the right
    /// silhouette and no skeleton, which does not fail loudly - it ships. It carries the
    /// weights, the bindposes and the tangents now, held there by MeshDecimatorTests, and
    /// the refusal is gone. <see cref="CastLodProof"/> is what settled whether the levels
    /// are worth having: at the camera's own resting range a soldier is eighty-two pixels
    /// tall, and a hundred and eighteen faces is indistinguishable from two thousand nine
    /// hundred.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.FarLods.Survey
    ///           unity run . -- -executeMethod TheVeil.Editor.FarLods.Build
    /// </summary>
    public static class FarLods
    {
        const string PlayScene = "Assets/_Project/Scenes/PlayLevel.unity";

        /// <summary>Where the generated far meshes live. Never inside a pack.</summary>
        const string MeshDir = "Assets/_Project/Models/Lod";

        [MenuItem("The Veil/Far LOD Survey")]
        public static void Survey()
        {
            var props = Standing(out string trouble);
            if (props == null)
            {
                Debug.LogError(trouble);
                return;
            }

            var sheet = new StringBuilder();
            sheet.AppendLine($"[Lod] {props.Count} distinct models stand in the scene's sets");

            int prefabs = 0, models = 0, other = 0, chained = 0, bare = 0;
            int rigged = 0, riggedChained = 0;
            long bareFaces = 0, riggedFaces = 0;
            var worst = new List<string>();
            var cast = new List<string>();

            foreach (var prop in props)
            {
                string path = AssetDatabase.GetAssetPath(prop);
                string kind = Kind(path);

                if (kind == "prefab") prefabs++;
                else if (kind == "model") models++;
                else other++;

                var group = prop.GetComponent<LODGroup>();
                int near = Near(prop, group);

                // Counted apart, because this tool cannot touch them and a number it
                // cannot act on mixed in with one it can is how a sheet lies by addition.
                // See Rigged.
                if (Rigged(prop))
                {
                    rigged++;
                    riggedFaces += near;

                    int rungs = group != null ? group.GetLODs().Length : 0;
                    if (rungs > 1) riggedChained++;

                    // <b>And how far the chain reaches, which is the number that decides
                    // whether it does anything.</b> The scenery listing has carried this
                    // from the start and the cast listing did not, which made a fox with
                    // three levels read as a fox that is handled. A chain whose last rung
                    // reaches past the map never switches in play, and lowering the
                    // transition heights of one that does is free - it moves no vertex and
                    // touches no rig, so it is the one lever this tool has over the cast.
                    string chain = "NO CHAIN";

                    if (rungs > 1)
                    {
                        var levels = group.GetLODs();
                        float last = levels[rungs - 1].screenRelativeTransitionHeight;
                        float reach = last > 0.0001f ? World(prop, group) / (last * Lens) : 9999f;

                        chain = $"{rungs} levels, last reaches {reach,6:0} m"
                                + (reach > MapWidth ? " <-- PAST THE MAP" : "");
                    }

                    if (near > Worthwhile)
                        cast.Add($"[Lod]   {prop.name,-38} {near,7} faces near, {chain} ({kind})");
                }

                if (group != null && group.GetLODs().Length > 1)
                {
                    chained++;

                    // How far the chain's last level reaches, in metres, at the shipped
                    // lens. This is the number that says whether the chain does anything:
                    // past the map's own width it cannot.
                    var levels = group.GetLODs();
                    float last = levels[levels.Length - 1].screenRelativeTransitionHeight;
                    float reach = last > 0.0001f ? World(prop, group) / (last * Lens) : 9999f;

                    if (reach > MapWidth && near > Worthwhile)
                        worst.Add($"[Lod]   {prop.name,-38} {near,7} faces near, "
                                  + $"{levels.Length} levels, last reaches {reach,6:0} m "
                                  + $"({kind})");
                }
                else
                {
                    bare++;
                    bareFaces += near;

                    if (near > Worthwhile)
                        worst.Add($"[Lod]   {prop.name,-38} {near,7} faces, NO CHAIN ({kind})");
                }
            }

            sheet.AppendLine($"[Lod] {prefabs} prefabs, {models} imported models, {other} other");
            sheet.AppendLine($"[Lod] {chained} carry an LOD chain, {bare} carry none "
                             + $"({bareFaces / 1000f:0.0}k faces with no chain at all)");
            sheet.AppendLine($"[Lod] a level is {MapWidth:0} m across, so a chain whose last "
                             + "level reaches further than that never switches in play:");

            worst.Sort();
            foreach (string line in worst) sheet.AppendLine(line);

            sheet.AppendLine($"[Lod] {worst.Count} models worth working on "
                             + $"(over {Worthwhile} faces near)");

            sheet.AppendLine($"[Lod] and {rigged} of the models are skinned - the cast - "
                             + $"{riggedFaces / 1000f:0.0}k faces near between them, "
                             + $"{riggedChained} of them carrying a chain. These were off "
                             + "limits until the decimator learned to carry a rig; they are "
                             + "still listed apart, because a chain on something that bends "
                             + "is a different risk from a chain on a rock:");

            cast.Sort();
            foreach (string line in cast) sheet.AppendLine(line);

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// The share of the screen below which no hand-made level may be the one drawn.
        ///
        /// Five and a half per cent, which on a 2400 pixel portrait screen is a hundred
        /// and thirty pixels of height. <b>This is the whole rule, and it is deliberately
        /// about screen share rather than distance.</b> A tree and a mountain range both
        /// want their detail while they are large in the frame, and they are large at
        /// wildly different distances - the meadow tree is 18 m across and the mountain a
        /// hundred and more, so a rule written in metres either strips the mountain or
        /// spares the tree. Screen share is what the LOD system is for and the packs
        /// simply set theirs too generously: the meadow tree's last level sits at 0,01,
        /// which is a reach of 1917 m on a map 256 m wide.
        ///
        /// A level whose threshold is under this never gets to be the cheap one, so it is
        /// switched off and ours takes over below it.
        /// </summary>
        const float KeepAbove = 0.055f;

        /// <summary>
        /// The levels this tool adds under everything else: the share each is shown below,
        /// and how many faces it may spend there.
        ///
        /// Three for a model that arrives with no chain at all, and the last two for one
        /// that has a chain already - because a pack's own LOD0 and LOD1 are hand-made and
        /// worth keeping, and it is only the far end that was written for a desktop.
        ///
        /// <b>Nothing is ever culled.</b> The last entry's share is nought, so the model
        /// is drawn at eighty faces out to the horizon rather than vanishing. Cutting
        /// trees off at two hundred metres would be cheaper and it would be seen: a level
        /// is 256 m across and the camera may stand 120 m back, so the far edge of the
        /// country is in frame.
        /// </summary>
        static readonly (float Below, int Faces)[] Thin =
        {
            (0.090f, 900),
            (0.035f, 250),
            (0.000f, 80),
        };

        /// <summary>
        /// The same table, for an instrument that needs to say what this tool would do
        /// without letting it do it.
        ///
        /// See <see cref="CastLodProof"/>: the rungs are written in screen share and were
        /// calibrated against scenery, and a soldier is a tenth of a tree's size, so what
        /// they mean for the cast is a question worth asking before the pass is run.
        /// </summary>
        internal static (float Below, int Faces)[] Rungs => Thin;

        /// <summary>Where a model with no chain of its own has its first level end.</summary>
        // A quarter of the screen. Below that it goes onto the 900-face level, which is
        // the first rung of Thin.
        const float BareFirst = 0.25f;

        /// <summary>No generated piece is cut below this, however small its share.</summary>
        // Twenty-four faces is a box with a lid. Under that a decimation stops being a
        // simplification of the shape and becomes a different object.
        internal const int Least = 24;

        /// <summary>Twice the tangent of half the shipped camera's field of view.</summary>
        // 50 degrees, as the play camera carries it. A group's share of the screen is its
        // world size over its distance divided by this, which is what an LOD threshold is
        // compared against.
        internal const float Lens = 0.9326f;

        /// <summary>How wide a level is, in metres: 64 tiles at four.</summary>
        const float MapWidth = 256f;

        /// <summary>Below this many faces there is nothing to win by thinning.</summary>
        const int Worthwhile = 600;

        /// <summary>The name every generated level carries, so a second run can find it.</summary>
        const string Mark = "Far LOD ";

        [MenuItem("The Veil/Build Far LODs")]
        public static void Build()
        {
            var props = Standing(out string trouble);
            if (props == null)
            {
                Debug.LogError(trouble);
                return;
            }

            Ensure(MeshDir);

            var sheet = new StringBuilder();
            sheet.AppendLine($"[Lod] building far levels for {props.Count} models");
            sheet.AppendLine($"[Lod] nothing below {KeepAbove:0.000} of screen height keeps a "
                             + "hand-made level; nothing is culled");

            int touched = 0, skipped = 0, failed = 0, rigged = 0;
            long before = 0, after = 0;

            foreach (var prop in props)
            {
                string path = AssetDatabase.GetAssetPath(prop);

                if (Kind(path) != "prefab")
                {
                    skipped++;
                    continue;
                }

                // Counted, not refused. This was a refusal for one day - the decimator
                // dropped the rig, and a character run through it came back with the right
                // silhouette and no skeleton. It carries the weights, the bindposes and
                // the tangents now, and MeshDecimatorTests holds it to that.
                if (Rigged(prop)) rigged++;

                var group = prop.GetComponent<LODGroup>();
                int near = Near(prop, group);

                if (near <= Worthwhile)
                {
                    skipped++;
                    continue;
                }

                string story = Thinned(path, out int wasFar, out int nowFar);

                if (story == null) { skipped++; continue; }
                if (story.Length > 0) { failed++; sheet.AppendLine($"[Lod]   {prop.name}: {story}"); continue; }

                touched++;
                before += wasFar;
                after += nowFar;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sheet.AppendLine($"[Lod] {touched} rebuilt, {skipped} left alone, {failed} refused, "
                             + $"{rigged} of them skinned");
            sheet.AppendLine($"[Lod] the far levels of those models were {before / 1000f:0.0}k "
                             + $"faces and are now {after / 1000f:0.0}k");
            sheet.AppendLine("[Lod] LOD0 is untouched everywhere - what the player sees up close "
                             + "is the same model it was");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// One prefab given cheap far levels, in place.
        ///
        /// Returns null when there was nothing to do, an empty string on success, and the
        /// trouble otherwise - so the caller can tell "already fine" from "would not".
        /// </summary>
        static string Thinned(string path, out int wasFar, out int nowFar)
        {
            wasFar = 0;
            nowFar = 0;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return "the prefab would not open";

            try
            {
                // Anything a previous run added, taken out first, so running this twice
                // does not stack three levels on three levels. Searched over the whole
                // hierarchy rather than the root's own children, because a generated piece
                // is parented beside the renderer it was simplified from - which is what
                // lets it copy that renderer's local place exactly.
                var mine = new List<GameObject>();

                foreach (var piece in root.GetComponentsInChildren<Transform>(true))
                    if (piece != root.transform
                        && piece.name.StartsWith(Mark, System.StringComparison.Ordinal))
                        mine.Add(piece.gameObject);

                foreach (var piece in mine)
                    if (piece != null) Object.DestroyImmediate(piece);

                var group = root.GetComponent<LODGroup>();

                // The renderers that draw the thing when it is close, which is what every
                // generated level is simplified from. A chain's LOD0, or the whole model
                // when it has no chain.
                var near = new List<Renderer>();
                var keep = new List<LOD>();

                if (group != null && group.GetLODs().Length > 0)
                {
                    var levels = group.GetLODs();

                    foreach (var renderer in levels[0].renderers)
                        if (renderer != null) near.Add(renderer);

                    // Every hand-made level that is still the drawn one while the model is
                    // big enough on screen to deserve it. The rest are switched off: they
                    // are the desktop tail of the chain and ours replaces them.
                    for (int lod = 0; lod < levels.Length; lod++)
                    {
                        if (lod == 0 || levels[lod].screenRelativeTransitionHeight > KeepAbove)
                        {
                            keep.Add(levels[lod]);
                            continue;
                        }

                        if (levels[lod].renderers == null) continue;

                        foreach (var renderer in levels[lod].renderers)
                        {
                            if (renderer == null) continue;

                            wasFar += Faces(renderer);
                            Strip(renderer);
                        }
                    }
                }
                else
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        if (renderer != null && renderer.gameObject.activeSelf) near.Add(renderer);

                    // A model with no chain draws all of itself at every range, so all of
                    // it is what the far levels replace.
                    foreach (var renderer in near) wasFar += Faces(renderer);
                }

                if (near.Count == 0) return "nothing draws it up close";

                int nearFaces = 0;
                foreach (var renderer in near) nearFaces += Faces(renderer);
                if (nearFaces <= 0) return "its near level has no triangles";

                // A model that arrived with no chain needs a rung above the cheap ones, so
                // it does not drop from everything to nine hundred faces in one step. One
                // that has a chain already has that rung, hand-made.
                int from = keep.Count > 0 ? 1 : 0;

                if (keep.Count == 0)
                    keep.Add(new LOD
                    {
                        screenRelativeTransitionHeight = BareFirst,
                        fadeTransitionWidth = 0f,
                        renderers = near.ToArray(),
                    });

                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                for (int rung = from; rung < Thin.Length; rung++)
                {
                    var made = Level(near, nearFaces, Thin[rung].Faces,
                                     $"{name}_Far{rung}", $"{Mark}{rung}");

                    if (made == null || made.Length == 0) continue;

                    foreach (var renderer in made) nowFar += Faces(renderer);

                    keep.Add(new LOD
                    {
                        screenRelativeTransitionHeight = Thin[rung].Below,
                        fadeTransitionWidth = 0f,
                        renderers = made,
                    });
                }

                // Strictly decreasing, or the group refuses the chain. A hand-made level
                // kept just above the floor can sit under the first rung we add, and then
                // the whole chain is rejected and the model draws its LOD0 for ever - which
                // would look right in a photograph and be the opposite of the fix.
                var chain = Ordered(keep);

                if (group == null) group = root.AddComponent<LODGroup>();

                group.SetLODs(chain);
                group.RecalculateBounds();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return "";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// A level this tool has replaced, taken out rather than switched off.
        ///
        /// <b>Because switching it off saves nothing that matters and reads as though it
        /// did.</b> The first version set the object inactive, and the memory line
        /// promptly fell from 777 MB to 546 - which was the budget report no longer
        /// counting what was still there. A disabled renderer keeps its MeshFilter, the
        /// MeshFilter keeps the mesh, and the mesh loads with the prefab whatever the
        /// object's active flag says. The only way the megabytes go is for the reference
        /// to go.
        ///
        /// The mesh itself is left in the pack untouched; what is removed is this prefab's
        /// hold on it. A model that still wants it still has it, and the measurement is
        /// what says whether anything was actually freed.
        ///
        /// The object is left standing, empty. It may be a parent, it may carry a collider
        /// or a script, and a tool that deletes whatever looks spare is how a prefab loses
        /// something nobody was measuring.
        /// </summary>
        static void Strip(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();

            Object.DestroyImmediate(renderer);
            if (filter != null) Object.DestroyImmediate(filter);
        }

        /// <summary>
        /// One generated level: every near renderer simplified to its share of a budget,
        /// parented under the prefab root.
        ///
        /// The budget is split between the renderers in proportion to what they already
        /// cost, so a tree spends it mostly on its trunk and a little on its branches
        /// rather than halving both. A renderer already under its share is copied rather
        /// than simplified - there is nothing to take off it, and a decimation that is
        /// asked for more faces than the mesh has would hand back the mesh anyway.
        /// </summary>
        static Renderer[] Level(List<Renderer> near, int nearFaces,
                                int budget, string name, string mark)
        {
            var made = new List<Renderer>();
            int piece = 0;

            foreach (var renderer in near)
            {
                int faces = Faces(renderer);
                if (faces <= 0) continue;

                var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>();

                var source = skin != null ? skin.sharedMesh
                                          : filter != null ? filter.sharedMesh : null;
                if (source == null) continue;

                int share = Mathf.Max(Least, (int)((long)budget * faces / nearFaces));
                if (share >= faces) share = faces;

                Mesh thin;

                if (share >= faces) thin = source;
                else
                {
                    string at = $"{MeshDir}/{name}_{piece}.asset";

                    thin = MeshDecimator.Simplify(source, share, out _);
                    if (thin == null) continue;

                    thin.name = $"{name}_{piece}";

                    var had = AssetDatabase.LoadAssetAtPath<Mesh>(at);
                    if (had != null) AssetDatabase.DeleteAsset(at);

                    AssetDatabase.CreateAsset(thin, at);
                }

                var stood = new GameObject($"{mark}{piece}");

                // Beside the renderer it was simplified from, with that renderer's own
                // local place copied across - so a branch cluster sitting four metres up
                // and turned thirty degrees stays there, whatever the model's own root
                // does. Parenting these to the prefab root and copying world values
                // instead would be right only while every root sat at the origin at scale
                // one, which is a thing that is true until it is not.
                stood.transform.SetParent(renderer.transform.parent, false);
                stood.transform.localPosition = renderer.transform.localPosition;
                stood.transform.localRotation = renderer.transform.localRotation;
                stood.transform.localScale = renderer.transform.localScale;

                Renderer paint;

                if (skin != null)
                {
                    // <b>A skinned level stays skinned, and the bones are the originals.</b>
                    // The mesh carries the weights and the bindposes - MeshDecimator sees
                    // to that - but a SkinnedMeshRenderer also needs the transforms those
                    // weights index into, and they live in this prefab's own skeleton.
                    // Handing over the same array is what makes the far level bend with
                    // the near one rather than stand still beside it.
                    var moved = stood.AddComponent<SkinnedMeshRenderer>();

                    moved.sharedMesh = thin;
                    moved.bones = skin.bones;
                    moved.rootBone = skin.rootBone;
                    moved.quality = skin.quality;
                    moved.updateWhenOffscreen = skin.updateWhenOffscreen;

                    // Copied rather than left to Unity, because a skinned renderer's
                    // bounds are not computed from the mesh: the bones move it, so the box
                    // is authored. An empty one culls the level the moment it is drawn,
                    // which reads exactly like an LOD chain that works.
                    moved.localBounds = skin.localBounds;

                    paint = moved;
                }
                else
                {
                    stood.AddComponent<MeshFilter>().sharedMesh = thin;
                    paint = stood.AddComponent<MeshRenderer>();
                }

                paint.sharedMaterials = renderer.sharedMaterials;

                made.Add(paint);
                piece++;
            }

            return made.Count == 0 ? null : made.ToArray();
        }

        /// <summary>
        /// The chain with its thresholds forced strictly downwards, keeping the order.
        ///
        /// Each level is pushed below the one before it by a hair if it is not already.
        /// Unity will not accept a chain that does not descend, and a refused chain is
        /// silent: the group keeps whatever it had.
        /// </summary>
        static LOD[] Ordered(List<LOD> chain)
        {
            var done = chain.ToArray();

            for (int i = 1; i < done.Length; i++)
            {
                float above = done[i - 1].screenRelativeTransitionHeight;

                if (done[i].screenRelativeTransitionHeight < above) continue;

                done[i].screenRelativeTransitionHeight = Mathf.Max(0f, above * 0.5f);
            }

            return done;
        }

        static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Ensure(parent);

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        /// <summary>
        /// Every model the scene's decor sets can stand up.
        ///
        /// Read by reflection over <see cref="BiomeDecor"/>'s own fields rather than from a
        /// list written here, because there are dozens of sets and the day a new one is
        /// added a hand-written list goes quietly stale - which is the same fault as six
        /// instruments each keeping their own copy of the dressed chapters.
        /// </summary>
        static HashSet<GameObject> Standing(out string trouble)
        {
            trouble = null;

            var scene = EditorSceneManager.OpenScene(PlayScene, OpenSceneMode.Single);
            var runner = Object.FindAnyObjectByType<LevelRunner>();

            if (runner == null)
            {
                trouble = "[Lod] PlayLevel has no LevelRunner, so there are no sets to read.";
                EditorSceneManager.CloseScene(scene, false);
                return null;
            }

            var props = new HashSet<GameObject>();

            Gather(runner.Decor, props);

            if (runner.Looks != null)
                foreach (var look in runner.Looks)
                    if (look != null) Gather(look.Decor, props);

            // And the cast, which is not dressing and was missed entirely the first time.
            Gather(runner.Models, props);

            EditorSceneManager.CloseScene(scene, false);
            return props;
        }

        static void Gather(BiomeDecor decor, HashSet<GameObject> props)
        {
            if (decor == null) return;

            foreach (var field in typeof(BiomeDecor).GetFields(BindingFlags.Public
                                                               | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(PropSet)) continue;

                var set = field.GetValue(decor) as PropSet;
                if (set == null || set.Models == null) continue;

                foreach (var model in set.Models)
                    if (model != null) props.Add(model);
            }
        }

        /// <summary>
        /// The cast's models, which hang off <see cref="VisualLibrary"/> and not off any
        /// prop set.
        ///
        /// <b>Because the first pass of this tool could not see them, and nobody noticed
        /// for a fortnight.</b> It rebuilt 398 chains and not one of them was a deer, a
        /// wolf, a wagon or a knight: it gathers from the PropSet fields of every
        /// BiomeDecor, and the column, the escort, the enemies, the animals and the crows
        /// are not dressing. <see cref="CastReport"/> then measured them in the frame and
        /// found the cast worth 300-750k triangles of it, close to half on the coast.
        ///
        /// Read by reflection for the same reason the prop sets are: the library is forty
        /// fields long and growing, and a list of them written out here would be wrong the
        /// first time one was added. An ActorModel brings its weapon as well as itself,
        /// because a spear is a mesh in a hand and is in the frame whatever else is.
        /// </summary>
        internal static void Gather(VisualLibrary library, HashSet<GameObject> props)
        {
            if (library == null) return;

            foreach (var field in typeof(VisualLibrary).GetFields(BindingFlags.Public
                                                                  | BindingFlags.Instance))
            {
                object held = field.GetValue(library);
                if (held == null) continue;

                // <b>Every check against null here is a typed one, deliberately.</b> A
                // field whose asset has gone missing hands back a GameObject that Unity
                // reports as null through its own operator and that `object held == null`
                // says is perfectly fine, because that comparison is reference equality on
                // a wrapper which still exists. The first version of this gathered one of
                // those and the survey died inside GetComponent.
                if (held is GameObject one)
                {
                    if (one != null) props.Add(one);
                    continue;
                }

                if (held is GameObject[] many)
                {
                    foreach (var model in many) if (model != null) props.Add(model);
                    continue;
                }

                if (held is ActorModel actor) { Gather(actor, props); continue; }

                if (held is ActorModel[] cast)
                    foreach (var model in cast) Gather(model, props);
            }
        }

        /// <summary>One actor: the model it stands up and the weapon it carries.</summary>
        static void Gather(ActorModel actor, HashSet<GameObject> props)
        {
            if (actor.Prefab != null) props.Add(actor.Prefab);
            if (actor.Weapon != null) props.Add(actor.Weapon);
        }

        /// <summary>
        /// Whether a model carries a skinned mesh, which this tool must not touch.
        ///
        /// <b>MeshDecimator carries no bone weights and no bindposes.</b> Running a
        /// skinned character through it would hand back a mesh with the right silhouette
        /// and no rig, which does not fail loudly: it ships, and the knight stands in the
        /// field like a statue while his skeleton walks off without him. So the refusal is
        /// here, in the tool, rather than in a note asking the next person to remember -
        /// and it is printed as a refusal with its reason, because a model quietly left
        /// alone reads as a model that did not need the work.
        /// </summary>
        static bool Rigged(GameObject prop)
        {
            return prop != null
                   && prop.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;
        }

        static string Kind(string path)
        {
            if (string.IsNullOrEmpty(path)) return "none";
            if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) return "prefab";
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return "model";
            if (path.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase)) return "model";
            return "other";
        }

        /// <summary>
        /// The faces a model draws when the player is next to it.
        ///
        /// Its LOD0 if it has a chain, the whole of it if it does not - which is the same
        /// thing, since a model with no chain draws all of itself at every range.
        /// </summary>
        static int Near(GameObject prop, LODGroup group)
        {
            int faces = 0;

            if (group != null)
            {
                var levels = group.GetLODs();
                if (levels.Length > 0 && levels[0].renderers != null)
                {
                    foreach (var renderer in levels[0].renderers)
                        faces += Faces(renderer);

                    return faces;
                }
            }

            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
                faces += Faces(renderer);

            return faces;
        }

        internal static int Faces(Renderer renderer)
        {
            if (renderer == null) return 0;

            Mesh shape = null;

            if (renderer is SkinnedMeshRenderer skin) shape = skin.sharedMesh;
            else
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) shape = filter.sharedMesh;
            }

            if (shape == null) return 0;

            int faces = 0;
            for (int sub = 0; sub < shape.subMeshCount; sub++)
                if (shape.GetTopology(sub) == MeshTopology.Triangles)
                    faces += (int)(shape.GetIndexCount(sub) / 3);

            return faces;
        }

        /// <summary>A group's size in world metres, which is what its thresholds use.</summary>
        static float World(GameObject prop, LODGroup group)
        {
            var scale = prop.transform.lossyScale;
            float widest = Mathf.Max(Mathf.Abs(scale.x),
                                     Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));

            return group.size * widest;
        }
    }
}
