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
            var props = Standing(out string trouble, out _);
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
        /// <b>Nothing standing is ever culled.</b> The last entry's share is nought, so the
        /// model is drawn at eighty faces out to the horizon rather than vanishing. Cutting
        /// trees off at two hundred metres would be cheaper and it would be seen: a level
        /// is 256 m across and the camera may stand 120 m back, so the far edge of the
        /// country is in frame.
        ///
        /// Ground cover is the exception and it has its own rule: see
        /// <see cref="CoverEnds"/>. A tree that vanishes is a hole in the skyline; a reed
        /// that vanishes is twelve pixels of ground, and there are two thousand of them.
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

        /// <summary>
        /// Below this many faces a model is left alone.
        ///
        /// <b>It was 600, and that is the wrong question asked of the right number.</b>
        /// Six hundred asks whether a model is big. What costs a frame is a model's total
        /// in it, and the heaviest twelve models in the game's worst frame are 238 to 2 127
        /// faces each standing 40 to 328 deep: 328 copies of a 330-face grass is 108 200
        /// triangles, which is more than any single tree in that frame. Ten of those twelve
        /// were gathered by this tool and silently dropped here.
        ///
        /// Two hundred, because the cheapest rung is eighty faces and a model under about
        /// two hundred has no room left to give. The gate cannot see copy counts - that is
        /// the frame's business and <see cref="CastReport"/>'s - so it is set where a chain
        /// still has something to cut rather than where a model starts looking large.
        /// </summary>
        const int Worthwhile = 200;

        /// <summary>
        /// How wide a thing may be, in metres, and still count as ground cover.
        ///
        /// <b>This is the gate on ending a model rather than thinning it, and it is written
        /// in metres on purpose - the one rule here that is.</b> Every other threshold in
        /// this file is a share of the screen, because detail should go when a thing is
        /// small in the frame and that happens at wildly different distances for a tree and
        /// a mountain. Ending is the opposite question: what may be allowed to vanish is
        /// about what the thing is, not about how it is being looked at. A reed is a reed at
        /// any range.
        ///
        /// Three metres, which is a plant, a small rock, a tussock - and which is well under
        /// a wagon, a hut or a horizon piece. It does double duty: a share is a size over a
        /// distance, so the bigger the model the further off the ending falls, and at three
        /// metres the ending already lands past the width of the map. Nothing near the top
        /// of this gate can actually vanish in play, which is the right shape for a rule
        /// whose failure mode is a bald country.
        /// </summary>
        internal const float Cover = 3f;

        /// <summary>
        /// The share of the screen below which ground cover is not drawn at all.
        ///
        /// <b>This is the one threshold in this file that is a judgement and not a
        /// measurement, so here is everything that was measured before it was picked.</b>
        ///
        /// Five thousandths of the screen's height, which on a 2400-pixel phone held
        /// upright is twelve pixels. At the phone's own lod bias of 0,70 that puts a
        /// one-metre plant out at 150 m, a two-metre bush at 300 m, and anything near the
        /// top of <see cref="Cover"/> past the width of the map, where ending it means
        /// nothing.
        ///
        /// What the frame said. The heaviest frame in the game is in the marsh, and 61 per
        /// cent of it is ground cover: 98 models in 5 699 copies, 671,8k triangles, of
        /// which two reeds at 136 and 168 faces are 30 per cent on their own. None of it
        /// can be thinned - the cheapest rung this tool owns is eighty faces and
        /// <see cref="Least"/> is twenty-four, so a 136-face reed has nothing to hand over.
        /// Ending it is the only lever left.
        ///
        /// What the camera said, and this is the part that decided it. The play camera
        /// stands between 62 and 120 m back from what it looks at, so ground cover is never
        /// near it: not one copy in that frame stood taller than 0,018 of the screen, and
        /// 4 080 of the 5 699 stood below 0,005. The saving is all in the far tail and so
        /// is the risk - there is no threshold that takes only what cannot be seen.
        ///
        /// What the air said. The marsh runs exponential-squared fog at 0,008, which leaves
        /// 8 per cent of a plant at 200 m and 2 per cent at 250 m, so out there this is very
        /// nearly free. <b>But the fog is set per chapter and the threshold is baked into
        /// the prefab</b>, and the clearest chapter is farmland at 0,0007, which hides
        /// nothing at any range inside the map. So the rule cannot lean on the fog, and it
        /// is written in pixels instead: twelve of them, at which a reed's own blades are
        /// under a pixel wide.
        ///
        /// It will be seen in clear weather as a far edge where the cover stops. That is
        /// the trade, it was taken deliberately, and it moves in one place.
        /// </summary>
        const float CoverEnds = 0.005f;

        /// <summary>
        /// The share at which this model ends: twelve pixels, unless twelve pixels would
        /// fall nearer than the camera can stand back.
        ///
        /// <b>Because a share alone ended the grass everywhere.</b> A threshold is a size
        /// over a distance, so the same twelve pixels are a hundred and ninety metres for a
        /// reed and forty-six for a three-decimetre tuft of grass - and the play camera
        /// stands up to 120 m back from what it is looking at, so that tuft stopped being
        /// drawn before the frame even began. Not a far edge: never drawn at all. The first
        /// run of this rule did exactly that to <c>SM_Plant_Grass_01</c> and eight others,
        /// and the sheet reported it as forty-six metres rather than as gone.
        ///
        /// So the edge is pushed out to <see cref="CameraOrbit.MaxRange"/> when the share
        /// would put it nearer. Nothing vanishes closer than the furthest the camera can
        /// ever be from the thing it is aimed at. It only ever lowers the threshold, so a
        /// chain's descent is never put at risk by it.
        /// </summary>
        static float Edge(float span)
        {
            if (span <= 0f) return CoverEnds;

            float floor = span * QualitySettings.lodBias
                          / (CameraOrbit.MaxRange * Lens);

            return Mathf.Min(CoverEnds, floor);
        }

        /// <summary>
        /// How far, in metres, a model of this size is drawn before it ends.
        ///
        /// <b>With the bias in it, which is the whole point of the line.</b> The first
        /// version of this divided size by threshold and lens and stopped, and every
        /// distance it printed was 1,43x too far - the identical fault, made on the same
        /// afternoon, as the one in <see cref="BudgetReport.Ending"/> that this project had
        /// just finished correcting. A threshold is compared against a share that has
        /// already been multiplied by the quality level's lod bias, so the reach has to
        /// carry it too. <see cref="BudgetReport.Phone"/> is what puts the editor on the
        /// phone's quality first, or this reads whatever the desktop happened to be set to.
        /// </summary>
        static float Reach(float span)
        {
            float edge = Edge(span);
            if (edge <= 0.00001f) return 9999f;

            return span * QualitySettings.lodBias / (edge * Lens);
        }

        /// <summary>The name every generated level carries, so a second run can find it.</summary>
        const string Mark = "Far LOD ";

        [MenuItem("The Veil/Build Far LODs")]
        public static void Build()
        {
            var props = Standing(out string trouble, out var cast);
            if (props == null)
            {
                Debug.LogError(trouble);
                return;
            }

            Ensure(MeshDir);

            var sheet = new StringBuilder();
            sheet.AppendLine($"[Lod] building far levels for {props.Count} models");

            // <b>On the phone's quality level, and this one is not only for the printing.</b>
            // The floor under a ground cover edge is a distance - 120 m, the furthest the
            // camera stands back - and turning a distance into a threshold needs the lod
            // bias the player will actually run. The editor opens on Ultra, which is a bias
            // of 2,00 against the phone's 0,70, so a pass run without this writes every
            // edge 2,9x too near and nothing in the asset says so afterwards. See Edge.
            int wasQuality = BudgetReport.Phone(sheet, "Lod");
            sheet.AppendLine($"[Lod] nothing below {KeepAbove:0.000} of screen height keeps a "
                             + $"hand-made level; nothing is culled except ground cover "
                             + $"under {Cover:0.0} m across, which ends below "
                             + $"{CoverEnds:0.000} of screen height and never nearer than "
                             + $"{CameraOrbit.MaxRange:0} m");

            int touched = 0, skipped = 0, failed = 0, rigged = 0, ended = 0, onlyEdge = 0;
            long before = 0, after = 0, endedFaces = 0;

            // Every model that leaves here with a far edge instead of a chain running on to
            // the horizon, and how far it is drawn before it stops. Written out in full
            // rather than tallied, because this is the one rule in the file that can be
            // wrong in a way no instrument will catch - a reed that ends too near is
            // something a person has to see, and a person needs the list to look at.
            var ends = new List<string>();

            // Every model this run leaves alone for being small, by name, so that absence
            // from this sheet means it was never gathered and nothing else.
            var small = new List<string>();

            // Which models this run will give a chain to, settled before any of them is
            // touched. A nested model's renderers are its own business only if a chain is
            // actually coming for them, and a HashSet has no order, so asking mid-run
            // would answer differently depending on which prefab came up first. See
            // Elsewhere.
            var handled = new HashSet<string>();

            foreach (var prop in props)
            {
                string where = AssetDatabase.GetAssetPath(prop);

                if (Kind(where) != "prefab") continue;

                // Thinned or ended, either way it leaves here with a chain - and that is
                // the only thing this set is asked about. A reed nested inside a swamp
                // cluster is the reed's own business now that the reed has an edge of its
                // own, exactly as a knight inside a cavalry prefab is.
                bool thin = Near(prop, prop.GetComponent<LODGroup>()) > Worthwhile;

                if (!thin && !Coverable(prop, cast)) continue;

                handled.Add(where);
            }

            sheet.AppendLine($"[Lod] {handled.Count} of them will be given a chain, so a "
                             + "nested one of those is left to its own");

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
                    float wide = Span(prop);

                    // Too small to thin is not the same as too small to cost anything, and
                    // for a fortnight this gate treated them as the same thing. A model
                    // with nothing to hand a decimator can still be handed a far edge.
                    string why = null;

                    if (Coverable(prop, cast))
                    {
                        string note = Ended(path, out float ours, out float reach, out why);

                        if (note != null && note.Length > 0)
                        {
                            failed++;
                            sheet.AppendLine($"[Lod]   {prop.name}: {note}");
                            continue;
                        }

                        if (note != null)
                        {
                            ended++;
                            onlyEdge++;
                            endedFaces += near;
                            ends.Add($"{prop.name,-38} {near,7} faces, {ours,5:0.0} m across, "
                                     + $"drawn to {reach,5:0} m and then not at all");
                            continue;
                        }
                    }

                    // Named, not just counted, and with the reason where there is one. This
                    // was a bare skipped++ for weeks, and "it is not in the sheet" then
                    // meant either "never gathered" or "gathered and dropped here" with no
                    // way to tell which - so a claim about the castle went into a commit
                    // message half wrong. A silent skip is a silent failure wearing a
                    // counter.
                    skipped++;
                    small.Add($"{prop.name} ({near} faces, {wide:0.0} m across"
                              + (why != null ? $", {why}" : "") + ")");
                    continue;
                }

                string story = Thinned(path, handled, Coverable(prop, cast),
                                       out int wasFar, out int nowFar, out float span);

                if (story == null) { skipped++; continue; }
                if (story.Length > 0) { failed++; sheet.AppendLine($"[Lod]   {prop.name}: {story}"); continue; }

                touched++;
                before += wasFar;
                after += nowFar;

                if (Coverable(prop, cast))
                {
                    ended++;
                    ends.Add($"{prop.name,-38} {near,7} faces, {span,5:0.0} m across, "
                             + $"thinned and ended at {Reach(span),5:0} m");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            small.Sort();

            sheet.AppendLine($"[Lod] {small.Count} were left alone for being under "
                             + $"{Worthwhile} faces:");

            foreach (string one in small) sheet.AppendLine($"[Lod]   small {one}");

            ends.Sort();

            sheet.AppendLine($"[Lod] {ended} are ground cover, under {Cover:0.0} m across, so "
                             + $"they end below {CoverEnds:0.000} of screen height instead of "
                             + "running on to the horizon:");

            foreach (string one in ends) sheet.AppendLine($"[Lod]   ends {one}");

            sheet.AppendLine($"[Lod] {touched} rebuilt, {onlyEdge} given an edge and nothing "
                             + $"else, {skipped} left alone, {failed} refused, {rigged} of "
                             + $"them skinned - and {ended} carry a far edge in all, of which "
                             + $"{onlyEdge} had no mesh to spare "
                             + $"({endedFaces / 1000f:0.0}k faces of cover that could not be "
                             + "thinned at all)");
            sheet.AppendLine($"[Lod] the far levels of those models were {before / 1000f:0.0}k "
                             + $"faces and are now {after / 1000f:0.0}k");
            sheet.AppendLine("[Lod] LOD0 is untouched everywhere - what the player sees up close "
                             + "is the same model it was");

            QualitySettings.SetQualityLevel(wasQuality, true);

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// One prefab given cheap far levels, in place.
        ///
        /// Returns null when there was nothing to do, an empty string on success, and the
        /// trouble otherwise - so the caller can tell "already fine" from "would not".
        /// </summary>
        static string Thinned(string path, HashSet<string> handled, bool ending,
                              out int wasFar, out int nowFar, out float span)
        {
            wasFar = 0;
            nowFar = 0;
            span = 0f;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return "the prefab would not open";

            try
            {
                // First, and before a renderer is stripped or a level added, because this
                // is the model's own size and the point of reading it is to compare it with
                // what arrived rather than with what this method leaves behind.
                span = Span(root);

                // Anything a previous run added, taken out first, so running this twice
                // does not stack three levels on three levels. Searched over the whole
                // hierarchy rather than the root's own children, because a generated piece
                // is parented beside the renderer it was simplified from - which is what
                // lets it copy that renderer's local place exactly.
                //
                // <b>Except a piece that belongs to a nested prefab, which is not ours to
                // delete.</b> Deleting it writes a removed-child override into this
                // prefab, and the nested model's own LOD group still lists it - so its
                // far levels end up listing nothing, and a level that lists nothing draws
                // nothing. That is how the light cavalry and the scout lost their riders
                // below 0,090 of the screen: six empty levels, measured by Level Widths,
                // none of them there before this tool ran over the cast.
                var mine = new List<GameObject>();

                foreach (var piece in root.GetComponentsInChildren<Transform>(true))
                {
                    if (piece == root.transform) continue;
                    if (!piece.name.StartsWith(Mark, System.StringComparison.Ordinal)) continue;
                    if (Theirs(piece.gameObject)) continue;

                    mine.Add(piece.gameObject);
                }

                foreach (var piece in mine)
                    if (piece != null) Object.DestroyImmediate(piece);

                // Every renderer another model in this run owns, because it arrived here
                // inside that model. Its own chain covers it; a second chain over the top
                // of it draws the thing twice at distance and switches it on two rules at
                // once.
                var elsewhere = Elsewhere(root, path, handled);

                var group = root.GetComponent<LODGroup>();

                // The renderers that draw the thing when it is close, which is what the
                // first kept level holds: a chain's LOD0, or the whole model when it has
                // no chain.
                var near = new List<Renderer>();
                var keep = new List<LOD>();
                var doomed = new List<Renderer>();

                // Every original level's live renderers, in chain order, so the cheapest
                // one can be found afterwards whether it is kept or replaced.
                var rungs = new List<List<Renderer>>();

                if (group != null && group.GetLODs().Length > 0)
                {
                    var levels = group.GetLODs();

                    foreach (var renderer in levels[0].renderers)
                        if (renderer != null && !elsewhere.Contains(renderer)) near.Add(renderer);

                    // Every hand-made level that is still the drawn one while the model is
                    // big enough on screen to deserve it. The rest are switched off: they
                    // are the desktop tail of the chain and ours replaces them.
                    for (int lod = 0; lod < levels.Length; lod++)
                    {
                        var trimmed = Without(levels[lod], elsewhere);
                        rungs.Add(Live(trimmed));

                        if (lod == 0 || trimmed.screenRelativeTransitionHeight > KeepAbove)
                        {
                            // <b>Unless it is one of ours from a previous run, emptied by
                            // the cleanup above.</b> The first rung this tool adds sits at
                            // 0,090, which is above KeepAbove, so on a second run it reads
                            // as a hand-made level worth keeping - and its renderers were
                            // destroyed thirty lines ago. It was kept, empty, and the rung
                            // that would have refilled it was skipped because the chain
                            // "already had" a level there. 304 groups in this project held
                            // exactly one such level, all of them at 0,090: measured in the
                            // prefab files, and a level that lists nothing draws nothing.
                            if (lod > 0 && rungs[lod].Count == 0) continue;

                            keep.Add(trimmed);
                            continue;
                        }

                        doomed.AddRange(rungs[lod]);
                    }
                }
                else
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        if (renderer != null && renderer.gameObject.activeSelf
                            && !elsewhere.Contains(renderer)) near.Add(renderer);
                }

                if (near.Count == 0) return "nothing draws it up close";

                int nearFaces = 0;
                foreach (var renderer in near) nearFaces += Faces(renderer);
                if (nearFaces <= 0) return "its near level has no triangles";

                // A model that arrived with no chain draws all of itself at every range, so
                // all of it is what the far levels replace - in the accounting only. Those
                // renderers are its near level now and nothing strips them; `doomed` is for
                // levels that are going away, and it has no near level in it.
                if (group == null || group.GetLODs().Length == 0)
                    foreach (var renderer in near) wasFar += Faces(renderer);
                else
                    foreach (var renderer in doomed) wasFar += Faces(renderer);

                // A model with nothing but a near level needs a rung above the cheap ones,
                // so it does not drop from everything to nine hundred faces in one step.
                // One that keeps a hand-made far level already has that rung.
                //
                // Counted after the empties are dropped, and that matters: a chain this
                // tool wrote has one kept level, its near one, and needs all three rungs
                // back. Asking "does it have a group" instead gave it two and left the
                // 900-face rung as a hole.
                if (keep.Count == 0)
                    keep.Add(new LOD
                    {
                        screenRelativeTransitionHeight = BareFirst,
                        fadeTransitionWidth = 0f,
                        renderers = near.ToArray(),
                    });

                int from = keep.Count > 1 ? 1 : 0;

                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                // <b>The cheapest level the model has is what the cheap ones are cut from,
                // not LOD0 - and cheapest means cheapest, kept or replaced.</b> This was
                // LOD0 for one pass and it cost the skinned-mesh budget: Level Widths, run
                // against the prefabs before and after, found the only three bottom rungs
                // in the project that got wider - Fox_URP 1 skinned renderer to 3, Wolf_URP
                // 1 to 3, Boar 1 to 2 - because the fox ships three renderers at LOD0 and
                // one merged renderer at LOD1, and a generated level keeps one renderer per
                // renderer it was cut from. A pack author merging his own far level is him
                // saying what a fox at two hundred metres is: one silhouette, not three.
                //
                // The boar's merged level sits at 0,010, below KeepAbove, so it is one this
                // tool replaces - and it is still the right thing to cut from. Which is why
                // the new levels are built before anything is stripped.
                var seed = Cheapest(rungs, near);

                int seedFaces = 0;
                foreach (var renderer in seed) seedFaces += Faces(renderer);
                if (seedFaces <= 0) { seed = near; seedFaces = nearFaces; }

                for (int rung = from; rung < Thin.Length; rung++)
                {
                    // A rung that is not cheaper than what it is cut from is not a far
                    // level at all. Level falls back to the source mesh when the budget
                    // does not bite, so asking a 330-face grass for a 900-face rung stands
                    // a second renderer up drawing exactly what the one above it draws -
                    // which was harmless while the gate kept small models out and is not
                    // harmless now that it lets them in.
                    if (Thin[rung].Faces >= seedFaces) continue;

                    var made = Level(seed, seedFaces, Thin[rung].Faces,
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

                // <b>Now, and not before, the levels this tool replaces let go of their
                // meshes.</b> The cheap levels are cut from the cheapest one the model has,
                // and on most packs that is a level we are replacing - so it has to still
                // be there while they are being cut. A model with no chain of its own has
                // its whole self in this list, which is the same thing: what it drew at
                // every range is what the far levels take over.
                foreach (var renderer in doomed)
                    if (renderer != null) Strip(renderer);

                // Strictly decreasing, or the group refuses the chain. A hand-made level
                // kept just above the floor can sit under the first rung we add, and then
                // the whole chain is rejected and the model draws its LOD0 for ever - which
                // would look right in a photograph and be the opposite of the fix.
                var chain = Ordered(keep);

                // <b>And if it is ground cover, the chain ends rather than running on at
                // eighty faces for ever.</b> The size gate and the face gate ask different
                // questions and a model can answer both: a 400-face bush a metre across is
                // thinned like a tree and ended like a reed. Done after Ordered, since what
                // it changes is a threshold Ordered put at nought, and checked against the
                // rung above all the same - this tool has been bitten once by a chain Unity
                // silently refused.
                // <b>Decided by the caller, not by the size read here.</b> Whether a thing
                // may be allowed to vanish is a question about what it is, and this method
                // can only see how big it is. See Coverable.
                if (ending && chain.Length > 0)
                {
                    int last = chain.Length - 1;
                    float above = last > 0 ? chain[last - 1].screenRelativeTransitionHeight
                                           : 1f;
                    float edge = Edge(span);

                    if (edge < above)
                        chain[last].screenRelativeTransitionHeight = edge;
                }

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
        /// Whether a model may be allowed to stop being drawn at all.
        ///
        /// <b>Three conditions, and the first one is the one that was nearly missed.</b>
        /// Size alone very nearly shipped a rule that ends a crow. A crow is thirty
        /// centimetres across, which puts its edge at forty-five metres - and the crows are
        /// how the player reads what is waiting down the road, so a crow that stops being
        /// drawn is not a cheaper frame, it is a lie about the country. A reed and a crow
        /// are the same size and no measurement of size can tell them apart.
        ///
        /// So: it has to be dressing rather than cast, which is a question of which list it
        /// was read off; it has to be small, which is <see cref="Cover"/>; and it must not
        /// bend, because a thing with a skeleton in it is an actor whatever list it came
        /// from and this tool has already learned once what happens when it guesses about
        /// rigs.
        /// </summary>
        static bool Coverable(GameObject prop, HashSet<GameObject> cast)
        {
            if (prop == null) return false;
            if (cast != null && cast.Contains(prop)) return false;
            if (Rigged(prop)) return false;

            float wide = Span(prop);
            return wide > 0f && wide <= Cover;
        }

        /// <summary>
        /// One small model given a chain whose only job is to end: it draws itself as it
        /// is, and below <see cref="CoverEnds"/> it draws nothing.
        ///
        /// <b>For everything the thinning path cannot help.</b> A model under
        /// <see cref="Worthwhile"/> faces has nothing to give a decimator, and that gate
        /// used to mean "left alone" - which on a reed standing a thousand deep in the
        /// marsh means a sixth of the frame that no instrument could do anything about. It
        /// is not given a cheaper mesh. It is given a far edge.
        ///
        /// Returns null when there was nothing to do, an empty string on success, and the
        /// trouble otherwise, like <see cref="Thinned"/>.
        /// </summary>
        static string Ended(string path, out float span, out float reach, out string why)
        {
            span = 0f;
            reach = 0f;
            why = null;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return "the prefab would not open";

            try
            {
                span = Span(root);
                if (span <= 0f) { why = "nothing in it has a mesh"; return null; }
                if (span > Cover) return null;

                reach = Reach(span);

                var live = new List<Renderer>();

                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null && renderer.gameObject.activeSelf
                        && Faces(renderer) > 0) live.Add(renderer);

                if (live.Count == 0) { why = "nothing in it is switched on"; return null; }

                var group = root.GetComponent<LODGroup>();

                // <b>A chain somebody else made gets its last threshold lowered and nothing
                // else.</b> For one pass this refused them outright, on the reasoning that
                // rewriting another author's chain blind is how a model ends up drawing its
                // LOD0 for ever - and that is true of rewriting the levels. It is not true
                // of this: lowering the bottom threshold moves no vertex, touches no
                // renderer and lists nothing differently. It can only make the chain give
                // up sooner, and the one way it could break the chain is by not descending,
                // which is checked here against the level above.
                //
                // The refusal cost 25 models, among them the two ground covers that are
                // 23k triangles of the heaviest frame in the game - small enough that no
                // decimator can help them and chained well enough that this tool would not
                // look at them.
                if (group != null && group.GetLODs().Length > 1)
                {
                    var levels = group.GetLODs();
                    int last = levels.Length - 1;
                    float edge = Edge(span);

                    if (edge >= levels[last - 1].screenRelativeTransitionHeight)
                    {
                        why = $"its chain's last level already begins at "
                              + $"{levels[last - 1].screenRelativeTransitionHeight:0.000}, "
                              + "which is under the edge we would give it";
                        return null;
                    }

                    levels[last].screenRelativeTransitionHeight = edge;

                    group.SetLODs(levels);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    return "";
                }

                if (group == null) group = root.AddComponent<LODGroup>();

                var only = group.GetLODs().Length == 1 ? group.GetLODs()[0]
                                                       : new LOD { renderers = live.ToArray() };

                only.screenRelativeTransitionHeight = Edge(span);
                only.fadeTransitionWidth = 0f;

                if (only.renderers == null || only.renderers.Length == 0)
                    only.renderers = live.ToArray();

                group.SetLODs(new[] { only });
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
                // <b>Unless that renderer is the prefab root, and then under it.</b> A root
                // has no parent; SetParent(null) makes the piece a root object of the
                // preview scene rather than part of the prefab, SaveAsPrefabAsset saves the
                // root's own hierarchy and nothing else, and the piece is dropped on the
                // floor while the level that lists it keeps a null renderer. That is a model
                // which draws its full mesh above a quarter of the screen and nothing at all
                // below 0,090 - it does not get cheaper at distance, it vanishes.
                //
                // 278 single-object props in this project were in exactly that state, read
                // out of the prefab files and not inferred: `renderer: {fileID: 0}` three
                // times over, at 0,090, 0,035 and 0, which are this tool's own thresholds.
                // Under the root at identity is the same place the root's own renderer draws.
                var over = renderer.transform.parent;

                if (over != null)
                {
                    stood.transform.SetParent(over, false);
                    stood.transform.localPosition = renderer.transform.localPosition;
                    stood.transform.localRotation = renderer.transform.localRotation;
                    stood.transform.localScale = renderer.transform.localScale;
                }
                else
                {
                    stood.transform.SetParent(renderer.transform, false);
                    stood.transform.localPosition = Vector3.zero;
                    stood.transform.localRotation = Quaternion.identity;
                    stood.transform.localScale = Vector3.one;
                }

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
        /// <summary>The live renderers of one level, in order.</summary>
        static List<Renderer> Live(LOD level)
        {
            var found = new List<Renderer>();
            if (level.renderers == null) return found;

            foreach (var renderer in level.renderers)
                if (renderer != null) found.Add(renderer);

            return found;
        }

        /// <summary>
        /// The renderers of the cheapest level the model has, or the near ones when it has
        /// only the one.
        ///
        /// A chain is in descending order of screen share, so the last entry that holds
        /// anything is the one the engine draws furthest out - and that is the right thing
        /// to simplify from, whether this tool is keeping that level or replacing it. See
        /// the note at the call.
        /// </summary>
        static List<Renderer> Cheapest(List<List<Renderer>> rungs, List<Renderer> near)
        {
            for (int i = rungs.Count - 1; i >= 0; i--)
                if (rungs[i].Count > 0) return rungs[i];

            return near;
        }

        /// <summary>
        /// A level with somebody else's renderers taken out of it.
        ///
        /// A hand-made level listing a nested model's renderers is the pack author treating
        /// the pair as one object. Once that nested model has a chain of its own, two groups
        /// are switching the same renderer on two rules, and which one wins is not a thing
        /// to rely on. The nested one is nearer the renderer, so it keeps it.
        /// </summary>
        static LOD Without(LOD level, HashSet<Renderer> elsewhere)
        {
            if (level.renderers == null || elsewhere.Count == 0) return level;

            var kept = new List<Renderer>();

            foreach (var renderer in level.renderers)
                if (renderer != null && !elsewhere.Contains(renderer)) kept.Add(renderer);

            level.renderers = kept.ToArray();
            return level;
        }

        /// <summary>
        /// Whether a generated piece arrived inside a nested prefab rather than being added
        /// to this one.
        ///
        /// Part of a prefab instance and not an addition to it means it comes from that
        /// prefab's own asset, which is where it will be rebuilt. Deleting it here only
        /// writes a removed-child override and empties that model's own far levels.
        /// </summary>
        static bool Theirs(GameObject piece)
            => PrefabUtility.IsPartOfPrefabInstance(piece)
               && !PrefabUtility.IsAddedGameObjectOverride(piece);

        /// <summary>
        /// Every renderer in this prefab that belongs to another model the same run will
        /// give a chain to.
        ///
        /// <b>Decided by what the run will do, not by what the hierarchy looks like now.</b>
        /// A set is not ordered, so the knight may be thinned before the cavalry that
        /// carries him or after, and a rule that asks "does this nested model have a group
        /// yet" would give two different answers for the same project. A nested model that
        /// nothing in this run will touch is left in - it has no chain coming, so the
        /// parent's is the only one it will get.
        /// </summary>
        static HashSet<Renderer> Elsewhere(GameObject root, string path, HashSet<string> handled)
        {
            var theirs = new HashSet<Renderer>();
            if (handled == null || handled.Count == 0) return theirs;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(renderer.gameObject)) continue;

                string owner = PrefabUtility
                    .GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);

                if (string.IsNullOrEmpty(owner)) continue;
                if (owner == path) continue;
                if (handled.Contains(owner)) theirs.Add(renderer);
            }

            return theirs;
        }

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
        static HashSet<GameObject> Standing(out string trouble, out HashSet<GameObject> cast)
        {
            trouble = null;
            cast = new HashSet<GameObject>();

            var scene = EditorSceneManager.OpenScene(PlayScene, OpenSceneMode.Single);
            var runner = Object.FindAnyObjectByType<LevelRunner>();

            if (runner == null)
            {
                trouble = "[Lod] PlayLevel has no LevelRunner, so there are no sets to read.";
                EditorSceneManager.CloseScene(scene, false);
                return null;
            }

            // The cast is kept apart as well as counted in, because one rule here needs to
            // know where a model came from and not just how big it is. See Cover: ending a
            // thing because it is small catches a crow, and a crow at thirty centimetres
            // would stop being drawn at forty-five metres. The crows are how the player
            // reads what is waiting on the road. A prop set is dressing and the library is
            // not, and that distinction is the only honest way to tell them apart - a
            // measurement of size cannot, because a reed and a crow are the same size.

            var props = new HashSet<GameObject>();

            Gather(runner.Decor, props);

            if (runner.Looks != null)
                foreach (var look in runner.Looks)
                    if (look != null) Gather(look.Decor, props);

            // And the cast, which is not dressing and was missed entirely the first time.
            Gather(runner.Models, cast);

            foreach (var one in cast) props.Add(one);

            EditorSceneManager.CloseScene(scene, false);
            return props;
        }

        static void Gather(BiomeDecor decor, HashSet<GameObject> props)
            => Harvest(decor, props, new HashSet<object>(), 0);

        /// <summary>
        /// Every <see cref="PropSet"/> anywhere inside a decor, however deep.
        ///
        /// <b>Because reading only the top level missed the castle.</b> This walked the
        /// PropSet fields of BiomeDecor itself and stopped there - and BiomeDecor's `Kit`
        /// is a BuildingKit with thirty prop sets of its own: the walls, the curtain walls,
        /// the tower bases, shafts and tops, the gates, the turrets. The castle's tower and
        /// wall are 2 127 and 1 534 faces each and stood 40 and 54 deep in the heaviest
        /// frame in the game, 167 900 triangles between them, and this tool had never once
        /// been offered them.
        ///
        /// That is the third time here that a tool walked a list the thing it needed was
        /// not on. So it is a walk over the object graph now rather than over one type's
        /// fields, and the day somebody nests another kit it needs no edit.
        ///
        /// Only the project's own classes are followed. Stepping into a Unity type would
        /// be a walk through the engine, and a GameObject is the answer rather than
        /// something to search.
        /// </summary>
        static void Harvest(object holder, HashSet<GameObject> props,
                            HashSet<object> seen, int depth)
        {
            if (holder == null || depth > 4) return;
            if (!Ours(holder.GetType())) return;
            if (!seen.Add(holder)) return;

            foreach (var field in holder.GetType().GetFields(BindingFlags.Public
                                                             | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(PropSet))
                {
                    if (!(field.GetValue(holder) is PropSet set)) continue;
                    if (set.Models == null) continue;

                    foreach (var model in set.Models)
                        if (model != null) props.Add(model);

                    continue;
                }

                var value = field.GetValue(holder);
                if (value == null) continue;

                // A kit, or a list of them. Strings are enumerable too, which is the one
                // trap in asking an unknown field whether it can be walked.
                if (value is string) continue;

                if (value is System.Collections.IEnumerable many)
                {
                    foreach (var item in many) Harvest(item, props, seen, depth + 1);
                    continue;
                }

                Harvest(value, props, seen, depth + 1);
            }
        }

        /// <summary>Whether a type is one of this project's own, and so worth walking into.</summary>
        static bool Ours(System.Type type)
            => type != null && type.IsClass && type != typeof(string)
               && type.Namespace != null
               && type.Namespace.StartsWith("TheVeil", System.StringComparison.Ordinal);

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

        /// <summary>
        /// How wide a model is in world metres, counted the way an LODGroup counts itself.
        ///
        /// The widest span of the box its meshes fill, which is what RecalculateBounds
        /// writes into <c>LODGroup.size</c> and therefore what every threshold in a chain
        /// is measured against. Taken from the meshes rather than from
        /// <c>Renderer.bounds</c>, because a prefab asset is not in a scene and a
        /// renderer's world bounds there are whatever they last happened to be.
        ///
        /// Read before anything is touched, so the figure is the model as it arrived.
        /// </summary>
        static float Span(GameObject prop)
        {
            if (prop == null) return 0f;

            var into = prop.transform.worldToLocalMatrix;
            var box = new Bounds();
            bool any = false;

            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;

                Mesh shape = null;

                if (renderer is SkinnedMeshRenderer skin) shape = skin.sharedMesh;
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) shape = filter.sharedMesh;
                }

                if (shape == null) continue;

                var local = into * renderer.transform.localToWorldMatrix;
                var middle = shape.bounds.center;
                var reach = shape.bounds.extents;

                for (int corner = 0; corner < 8; corner++)
                {
                    var at = middle + new Vector3(
                        (corner & 1) == 0 ? -reach.x : reach.x,
                        (corner & 2) == 0 ? -reach.y : reach.y,
                        (corner & 4) == 0 ? -reach.z : reach.z);

                    var stood = local.MultiplyPoint3x4(at);

                    if (!any) { box = new Bounds(stood, Vector3.zero); any = true; }
                    else box.Encapsulate(stood);
                }
            }

            if (!any) return 0f;

            var scale = prop.transform.lossyScale;
            float widest = Mathf.Max(Mathf.Abs(scale.x),
                                     Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));

            return Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z)) * widest;
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
