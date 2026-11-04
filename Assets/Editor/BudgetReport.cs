using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace TheVeil.Editor
{
    /// <summary>
    /// Every level of the game held against the performance budget it was designed to:
    /// `The Veil &gt; Budget Report`.
    ///
    /// <b>Because the budget is a design requirement and not one line of it has ever been
    /// measured.</b> docs/technical-design.md §1 sets seven numbers - a hundred and fifty
    /// draw calls, two hundred and fifty thousand visible triangles, forty skinned meshes,
    /// seven hundred megabytes, two seconds to load, a hundred and fifty megabyte package,
    /// sixty frames a second - and says in its first sentence that a thousand levels means
    /// the generator may never produce something that bursts it. The generator has since
    /// produced a thousand levels, forty-four waterfront houses, a fort, a mansion, two
    /// piers, a moored warship and thirty-one animals a level, and nothing has ever asked
    /// it for the count.
    ///
    /// Six of the seven do not need a phone. Five of those do not even need a build: the
    /// level is stood up here with the same call the smoke test uses, which is the same
    /// call the run makes, and then counted. Only the frame rate needs hardware, and
    /// knowing the other six is what says whether the hardware will have a chance.
    ///
    /// <b>Visible, not total.</b> A level's whole triangle count is an upper bound nobody
    /// renders. The budget says visible, so the count is taken through the play camera's
    /// own frustum - the scene's camera, so the field of view is the shipped one - stood
    /// where <see cref="CameraOrbit"/> would stand it, at points along the fast road,
    /// facing the way the column faces. The worst of those points is the level's number,
    /// because a budget is a ceiling and the player drives through all of them.
    ///
    /// Taken twice: once at the camera's resting range and once pulled all the way back,
    /// which is a thing the player may do at any moment and which shows the most ground.
    /// And in landscape as well as portrait, because all four rotations are allowed and
    /// the wide one sees more.
    ///
    /// <b>Draw calls are reported as two numbers.</b> What the hardware is asked for
    /// depends on batching, and batching is not a property of the scene alone. The honest
    /// pair is the count of distinct materials among the visible renderers - what a
    /// renderer-per-material submission costs - and the count of distinct material-and-mesh
    /// pairs, which is the floor GPU instancing can reach, since instances of one prefab
    /// sharing one material collapse to a single call. The truth is between them and the
    /// gap is how much the instancing in §1 is worth.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.BudgetReport.Run
    /// </summary>
    public static class BudgetReport
    {
        // docs/technical-design.md §1, the mid-range column. The low-end floor is in the
        // same table and is not repeated here: a level that clears the target clears the
        // floor, and one that bursts the target is already the finding.
        const int WantedCalls = 150;
        const int WantedTriangles = 250000;
        internal const int WantedSkinned = 40;
        const int WantedMegabytes = 700;
        const float WantedLoad = 2f;

        /// <summary>
        /// How many points along the road the camera is stood at.
        ///
        /// Nine, which is a view every eighth of the way. The level is sampled rather than
        /// walked because standing the camera up and culling against it is the whole cost
        /// of this report, and the thing being looked for is the worst stretch of country
        /// on the map - which is tens of metres wide, not one tile.
        /// </summary>
        const int Stops = 9;

        /// <summary>A phone's two shapes, as aspect ratios. Portrait is the default.</summary>
        internal const float Portrait = 1080f / 2400f;
        internal const float Landscape = 2400f / 1080f;

        [MenuItem("The Veil/Budget Report")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                                     OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null)
            {
                Debug.LogError("[Budget] PlayLevel has no LevelRunner, so there is nothing to build.");
                return;
            }

            // The shipped camera, so the field of view is the one the player looks through
            // rather than a number chosen here. A report that invents its own lens is
            // measuring a game nobody plays.
            var eye = Object.FindAnyObjectByType<Camera>();
            float fov = eye != null ? eye.fieldOfView : 60f;

            var sheet = new StringBuilder();
            sheet.AppendLine("[Budget] every level against docs/technical-design.md section 1");
            sheet.AppendLine($"[Budget] lens {fov:0} degrees, camera at "
                             + $"{CameraOrbit.DefaultRange:0} m and {CameraOrbit.MaxRange:0} m, "
                             + $"{Stops} stops a level, portrait and landscape, worst kept");
            sheet.AppendLine($"[Budget] want calls <= {WantedCalls}, triangles <= {WantedTriangles / 1000}k, "
                             + $"skinned <= {WantedSkinned}, memory <= {WantedMegabytes} MB, "
                             + $"load <= {WantedLoad:0.0} s");

            int wasQuality = Phone(sheet, "Budget");

            var worst = new Worst();
            var census = new Census();

            // The single heaviest frame in the game, and what is in it.
            int heaviest = 0;
            Dictionary<Mesh, long> worstFrame = null;
            string worstFrameAt = "-";

            foreach (int chapter in DifficultyCurve.Dressed)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var clock = Stopwatch.StartNew();
                    var root = SmokeTest.Build(runner, chapter, level, out var map);
                    clock.Stop();

                    float load = clock.ElapsedMilliseconds / 1000f;

                    var standing = Pieces(root);
                    long bytes = Resident(root);
                    var seen = Peak(standing, map, runner.HeightScale, fov, out var frame);

                    if (frame != null && seen.Triangles > heaviest)
                    {
                        heaviest = seen.Triangles;
                        worstFrame = frame;
                        worstFrameAt = $"{chapter}-{level}";
                    }

                    sheet.AppendLine($"[Budget] {chapter}-{level,-2} "
                                     + $"calls {seen.Materials,4} / {seen.Instanced,4}  "
                                     + $"tris {seen.Triangles / 1000f,7:0.0}k  "
                                     + $"skinned {seen.Skinned,3}  "
                                     + $"mem {bytes / 1048576f,6:0.0} MB  "
                                     + $"load {load,5:0.00} s  "
                                     + $"seen {seen.Pieces,5} of {standing.Pieces.Count,6} pieces");

                    worst.Take(chapter, level, seen, bytes, load);
                    census.Take(standing.Pieces);

                    Object.DestroyImmediate(root);
                }
            }

            worst.Write(sheet);
            Frame("Budget", sheet, worstFrame, worstFrameAt, heaviest);
            census.Write(sheet);
            Debug.Log(sheet.ToString());

            QualitySettings.SetQualityLevel(wasQuality, true);
            EditorSceneManager.CloseScene(scene, false);
        }

        /// <summary>
        /// Set the quality level the game actually ships with on the phone, and say so.
        ///
        /// <b>Every triangle figure these reports have ever printed was measured at the
        /// wrong one.</b> The LOD bias is a multiplier on a model's share of the screen,
        /// so it decides which level of every chain is drawn - and this project carries
        /// six quality levels whose biases run from 0,3 to 2,0. The editor sits on Ultra
        /// at 2,0 because that is the Standalone default; Android's default is Medium at
        /// 0,7. Measured at 2,0 a tree looks nearly three times taller than the phone will
        /// ever see it, so it draws a level or two further up its chain, and the report
        /// comes back with the desktop's frame wearing the phone's budget.
        ///
        /// The level is read from the project's own setting rather than named here, because
        /// a number copied into a tool is a number that goes stale the first time somebody
        /// changes the real one.
        /// </summary>
        internal static int Phone(StringBuilder sheet, string tag)
        {
            int was = QualitySettings.GetQualityLevel();
            int want = -1;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");

            if (assets != null && assets.Length > 0 && assets[0] != null)
            {
                var all = new SerializedObject(assets[0]);
                var per = all.FindProperty("m_PerPlatformDefaultQuality");

                if (per != null)
                    for (int i = 0; i < per.arraySize; i++)
                    {
                        var pair = per.GetArrayElementAtIndex(i);
                        var key = pair.FindPropertyRelative("first");
                        var value = pair.FindPropertyRelative("second");

                        if (key == null || value == null) continue;
                        if (key.stringValue != "Android") continue;

                        want = value.intValue;
                        break;
                    }
            }

            if (want < 0 || want >= QualitySettings.names.Length)
            {
                // Loudly, because a report that quietly measured the wrong thing is how
                // this whole question went unasked for weeks.
                sheet.AppendLine($"[{tag}] NO ANDROID DEFAULT QUALITY FOUND - measuring at "
                                 + $"{QualitySettings.names[was]}, bias "
                                 + $"{QualitySettings.lodBias:0.00}, which is whatever the "
                                 + "editor was left on");
                return was;
            }

            QualitySettings.SetQualityLevel(want, true);

            sheet.AppendLine($"[{tag}] measured at {QualitySettings.names[want]}, which is "
                             + $"Android's default quality level, lod bias "
                             + $"{QualitySettings.lodBias:0.00} (the editor was on "
                             + $"{QualitySettings.names[was]})");

            return was;
        }

        /// <summary>
        /// What is in the heaviest frame the game draws, model by model.
        ///
        /// <b>This is the line the fix comes off.</b> Everything above it says the ceiling
        /// is burst and by how much; this says by what. The two cures - cut these meshes
        /// down, or stand fewer of them up - cost very different things, and which one is
        /// right depends on whether the triangles belong to a handful of heavy models or
        /// to the whole country evenly.
        /// </summary>
        internal static void Frame(string tag, StringBuilder sheet, Dictionary<Mesh, long> frame,
                                   string where, int all)
        {
            if (frame == null || all <= 0) return;

            var rows = new List<KeyValuePair<Mesh, long>>(frame);
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));

            sheet.AppendLine($"[{tag}] the heaviest frame in the game is on {where}, "
                             + $"{all / 1000000f:0.00}M triangles over {rows.Count} models:");

            long running = 0;

            for (int i = 0; i < rows.Count && i < 12; i++)
            {
                running += rows[i].Value;

                string name = rows[i].Key != null ? rows[i].Key.name : "(no mesh)";

                sheet.AppendLine($"[{tag}]   {name,-42} {rows[i].Value / 1000f,8:0.0}k  "
                                 + $"{100f * rows[i].Value / all,5:0.0}%  "
                                 + $"running {100f * running / all,5:0.0}%");
            }
        }

        /// <summary>One renderer of a built level, reduced to what the budget asks about.</summary>
        internal struct Piece
        {
            public Bounds Where;
            public int Triangles;
            public bool Skinned;
            public Material Paint;
            public Mesh Shape;

            /// <summary>Which LOD group owns it, or -1 for a piece that has none.</summary>
            public int Group;

            /// <summary>Which level of that group it belongs to.</summary>
            public int Lod;
        }

        /// <summary>
        /// One LOD group of a built level: where it is, how big, and when it drops a level.
        /// </summary>
        internal struct Steps
        {
            public Vector3 At;
            public float Size;

            /// <summary>
            /// The screen-relative height at which each level gives way to the next, as
            /// the group itself carries them. A level is shown while the group is taller
            /// on screen than its own figure, so they run downwards, and below the last
            /// one the group is not drawn at all.
            /// </summary>
            public float[] Until;
        }

        /// <summary>A built level, as this report needs to read it.</summary>
        internal struct Standing
        {
            public List<Piece> Pieces;
            public List<Steps> Groups;
        }

        /// <summary>
        /// Two readings of two separate roots, as one frame.
        ///
        /// <b>Because the country and the cast are stood up by different calls and have
        /// to be counted through one camera.</b> The scenery is built once a level and
        /// never moves; the column, the animals and the birds are rebuilt and re-read as
        /// they walk. Counting them apart and adding the totals would be wrong twice over
        /// - a material a wagon shares with a fence would be charged as two draw calls,
        /// and the instanced floor would be the sum of two floors rather than the floor
        /// of the sum.
        ///
        /// The second reading's LOD groups keep their own numbering by being pushed up
        /// past the first's, so a piece still points at the group that owns it.
        /// </summary>
        internal static Standing Join(Standing first, Standing second)
        {
            var pieces = new List<Piece>(first.Pieces.Count + second.Pieces.Count);
            pieces.AddRange(first.Pieces);

            int shift = first.Groups.Count;

            foreach (var piece in second.Pieces)
            {
                var moved = piece;
                if (moved.Group >= 0) moved.Group += shift;
                pieces.Add(moved);
            }

            var groups = new List<Steps>(first.Groups.Count + second.Groups.Count);
            groups.AddRange(first.Groups);
            groups.AddRange(second.Groups);

            return new Standing { Pieces = pieces, Groups = groups };
        }

        /// <summary>
        /// Every renderer of a built level, and the LOD groups that switch between them.
        ///
        /// Disabled ones are left out, and so are renderers whose mesh is missing - both
        /// cost nothing to draw, and counting them would report a budget burst that the
        /// hardware never sees. A renderer with several submeshes is one piece with the
        /// whole mesh's triangles and its first material, which understates its calls by
        /// the number of extra submeshes; the packs here are one material a mesh, and the
        /// census would say so if that changed.
        ///
        /// <b>And the LOD groups are read, because the second version of this counted
        /// every level of every one of them at once.</b> An LODGroup does not switch
        /// levels by disabling renderers - all of them stay enabled and the engine picks
        /// one when it draws - so asking for the enabled renderers hands back the near
        /// tree, the middle tree and the far tree together. The census said so plainly
        /// once it existed: the same meadow tree appeared three times, LOD0, LOD1 and
        /// LOD2, with six thousand seven hundred and fifty-nine copies each. Three
        /// identical copy counts for one model is not a pack, it is a bug in the reader.
        ///
        /// So each piece is told which group owns it and which of that group's levels it
        /// is, and the camera then picks one level per group the way the engine does. A
        /// piece under no group is always counted.
        ///
        /// Nested groups are resolved by depth - the deepest group that lists a renderer
        /// keeps it - because a landmark can carry a group on its root and its own parts
        /// can carry theirs, and the inner one is the one that decides.
        /// </summary>
        internal static Standing Pieces(GameObject root)
        {
            var pieces = new List<Piece>();
            var groups = new List<Steps>();

            // renderer -> which group and level owns it, with the depth that claimed it
            var owner = new Dictionary<Renderer, (int Group, int Lod, int Depth)>();

            foreach (var group in root.GetComponentsInChildren<LODGroup>(false))
            {
                var levels = group.GetLODs();
                if (levels == null || levels.Length == 0) continue;

                int depth = Depth(group.transform);
                int index = groups.Count;

                var until = new float[levels.Length];
                for (int lod = 0; lod < levels.Length; lod++)
                {
                    until[lod] = levels[lod].screenRelativeTransitionHeight;

                    if (levels[lod].renderers == null) continue;

                    foreach (var renderer in levels[lod].renderers)
                    {
                        if (renderer == null) continue;

                        if (owner.TryGetValue(renderer, out var held) && held.Depth >= depth)
                            continue;

                        owner[renderer] = (index, lod, depth);
                    }
                }

                var scale = group.transform.lossyScale;
                float widest = Mathf.Max(Mathf.Abs(scale.x),
                                         Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));

                groups.Add(new Steps
                {
                    At = group.transform.TransformPoint(group.localReferencePoint),
                    Size = group.size * widest,
                    Until = until,
                });
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                // The flag on that call is about the GameObject, not about the component,
                // so a renderer switched off on a live object comes back through it. The
                // far-LOD pass switches off the pack levels it replaces, and without this
                // they would all still be counted.
                if (!renderer.enabled) continue;

                Mesh shape = null;

                if (renderer is SkinnedMeshRenderer skin) shape = skin.sharedMesh;
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) shape = filter.sharedMesh;
                }

                if (shape == null) continue;

                bool owned = owner.TryGetValue(renderer, out var held2);

                pieces.Add(new Piece
                {
                    Where = renderer.bounds,
                    Triangles = Faces(shape),
                    Skinned = renderer is SkinnedMeshRenderer,
                    Paint = renderer.sharedMaterial,
                    Shape = shape,
                    Group = owned ? held2.Group : -1,
                    Lod = owned ? held2.Lod : 0,
                });
            }

            return new Standing { Pieces = pieces, Groups = groups };
        }

        static int Depth(Transform of)
        {
            int depth = 0;
            for (var up = of; up != null; up = up.parent) depth++;
            return depth;
        }

        /// <summary>
        /// A mesh's triangles, asked of each mesh once and then remembered.
        ///
        /// <b>Cached because the obvious way to write this is quadratic in the pack.</b>
        /// `mesh.triangles` hands back a fresh managed copy of the whole index buffer on
        /// every call, and a level stands up four hundred instances of the same tree - so
        /// the plain version copies that tree's indices four hundred times, per camera
        /// stop, per level. Index counts are read off the submeshes instead, which asks
        /// the buffer for its length rather than its contents.
        /// </summary>
        static readonly Dictionary<Mesh, int> Counted = new Dictionary<Mesh, int>();

        static int Faces(Mesh shape)
        {
            if (Counted.TryGetValue(shape, out int faces)) return faces;

            faces = 0;
            for (int sub = 0; sub < shape.subMeshCount; sub++)
                if (shape.GetTopology(sub) == MeshTopology.Triangles)
                    faces += (int)(shape.GetIndexCount(sub) / 3);

            Counted[shape] = faces;
            return faces;
        }

        /// <summary>
        /// What the level's meshes and textures occupy, counted once each.
        ///
        /// Resident rather than visible: culling a tree does not take its mesh out of
        /// memory, and the budget line is about the device's memory and not about the
        /// frame. Counted per asset, because four hundred instances of one tree are one
        /// mesh - which is the whole reason the number is survivable at all.
        ///
        /// <b>And asked of the whole level, not of the pieces that draw.</b> This took the
        /// drawing list, and the moment the far-LOD pass began switching off the pack
        /// levels it replaces, the memory line fell from 777 MB to 546 and read as fixed.
        /// It was not fixed. A switched-off renderer still holds its MeshFilter, the
        /// MeshFilter still holds the mesh, and the mesh still loads with the prefab - so
        /// those megabytes are exactly where they were and the only thing that had changed
        /// was that this had stopped counting them. **A budget line that improves because
        /// the instrument looked away is worse than one that is simply over.**
        ///
        /// Inactive objects and disabled renderers are therefore included. Making the
        /// saving real means taking the replaced meshes out of the prefabs, which is a
        /// different job from switching them off.
        /// </summary>
        static long Resident(GameObject root)
        {
            var meshes = new HashSet<Mesh>();
            var textures = new HashSet<Texture>();
            long bytes = 0;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh shape = null;

                if (renderer is SkinnedMeshRenderer skin) shape = skin.sharedMesh;
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) shape = filter.sharedMesh;
                }

                if (shape != null && meshes.Add(shape))
                    bytes += Profiler.GetRuntimeMemorySizeLong(shape);

                foreach (var paint in renderer.sharedMaterials)
                {
                    if (paint == null) continue;

                    foreach (string slot in paint.GetTexturePropertyNames())
                    {
                        var map = paint.GetTexture(slot);
                        if (map != null && textures.Add(map))
                            bytes += Profiler.GetRuntimeMemorySizeLong(map);
                    }
                }
            }

            return bytes;
        }

        /// <summary>What one camera position sees.</summary>
        internal struct Seen
        {
            public int Materials;
            public int Instanced;
            public int Triangles;
            public int Skinned;

            /// <summary>
            /// How many renderers were inside the frustum.
            ///
            /// Not a budget line. It is here because it is the one number that says
            /// whether the frustum is pointing at the country at all: a level with twelve
            /// thousand pieces standing in it that reports nine of them visible has a
            /// broken camera, and that is exactly what the first run of this reported
            /// while every budget line came back comfortably green.
            /// </summary>
            public int Pieces;
        }

        /// <summary>
        /// The worst view on the level: every stop, both ranges, both shapes of screen.
        /// </summary>
        static Seen Peak(Standing standing, LevelMap map, float heightScale, float fov,
                         out Dictionary<Mesh, long> frame)
        {
            var peak = new Seen();
            frame = null;

            var road = Road(map);
            if (road == null || road.Count < 2) return peak;

            var ranges = new[] { CameraOrbit.DefaultRange, CameraOrbit.MaxRange };
            var shapes = new[] { Portrait, Landscape };

            // One entry per LOD group, filled fresh at every camera position: which level
            // of that group the engine would draw from there, or -1 for a group the
            // distance has culled outright.
            var showing = new int[standing.Groups.Count];

            foreach (float range in ranges)
            {
                float pitch = CameraOrbit.PitchFor(range);

                foreach (float aspect in shapes)
                {
                    for (int stop = 0; stop < Stops; stop++)
                    {
                        int at = stop * (road.Count - 1) / (Stops - 1);
                        int next = at + 1 < road.Count ? at + 1 : at - 1;

                        var look = Ground(map, road[at], heightScale);
                        var ahead = Ground(map, road[next], heightScale) - look;
                        if (next < at) ahead = -ahead;
                        if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.forward;

                        var from = Eye(look, ahead, range, pitch);
                        var view = View(look, ahead, range, pitch, fov, aspect);

                        Choose(standing.Groups, showing, from, fov);

                        var seen = Through(standing.Pieces, showing, view);

                        // The heaviest single view of the level, kept piece by piece.
                        // <b>Because an average does not say what to fix.</b> The table of
                        // what the game stands up over a hundred levels names the models
                        // that are heavy and common, which is not the same question as
                        // what is in the one frame that bursts the ceiling - a model heavy
                        // enough to matter may also be the one the LOD chain has already
                        // dropped by the time the camera is where the trouble is.
                        if (seen.Triangles > peak.Triangles)
                            frame = Breakdown(standing.Pieces, showing, view);

                        if (seen.Triangles > peak.Triangles) peak.Triangles = seen.Triangles;
                        if (seen.Materials > peak.Materials) peak.Materials = seen.Materials;
                        if (seen.Instanced > peak.Instanced) peak.Instanced = seen.Instanced;
                        if (seen.Skinned > peak.Skinned) peak.Skinned = seen.Skinned;
                        if (seen.Pieces > peak.Pieces) peak.Pieces = seen.Pieces;
                    }
                }
            }

            return peak;
        }

        /// <summary>
        /// The road the column takes, which is the fast one if the level drew any.
        ///
        /// The fast one and not an average of the three, because it is the road the
        /// difficulty curve assumes and the one most levels are driven down. A level with
        /// no corridors at all is reported as nothing seen, which the sheet shows as a row
        /// of zeroes rather than as a level quietly skipped.
        /// </summary>
        static List<int> Road(LevelMap map)
        {
            if (map.Corridors == null) return null;

            foreach (var corridor in map.Corridors)
                if (corridor != null && corridor.Tiles != null && corridor.Tiles.Count >= 2)
                    return corridor.Tiles;

            return null;
        }

        /// <summary>A tile's middle, standing on the ground the run builds.</summary>
        internal static Vector3 Ground(LevelMap map, int tile, float heightScale)
        {
            float x = (tile % map.Grid.Width + 0.5f) * TileGrid.TileSize;
            float z = (tile / map.Grid.Width + 0.5f) * TileGrid.TileSize;

            return new Vector3(x, map.Grid.SurfaceElevation(x, z) * heightScale, z);
        }

        /// <summary>
        /// The frustum of the play camera stood behind a column at a point on the road.
        ///
        /// The offset is <see cref="CameraOrbit.Offset"/>'s arithmetic rather than the
        /// component's, because the component needs a caravan and a transform and this
        /// needs a matrix. Yaw is nought - the camera's own resting yaw - so what is
        /// measured is the view the player is given rather than one they have swung.
        /// </summary>
        internal static Plane[] View(Vector3 look, Vector3 ahead, float range, float pitch,
                            float fov, float aspect)
        {
            // Three metres up, which is the column's own height and what the camera is
            // aimed at rather than the dirt under it.
            var target = look + Vector3.up * 3f;
            var from = Eye(look, ahead, range, pitch);

            var world = Matrix4x4.TRS(from,
                                      Quaternion.LookRotation(target - from, Vector3.up),
                                      Vector3.one);

            // 900 metres, as the smoke camera's far plane: past that the country is
            // skyline, and the skyline is placed by its own edge.
            var lens = Matrix4x4.Perspective(fov, aspect, 0.3f, 900f);

            // <b>And the Z axis flipped, which the first version of this did not do.</b>
            // Unity's view space looks down its own negative Z, so a camera's
            // worldToCameraMatrix is not the inverse of its transform - it is that inverse
            // with Z negated. Without the flip the frustum points out of the back of the
            // camera's head, and what it counts is the empty country behind the column:
            // every level came back at four draw calls and sixteen thousand triangles,
            // comfortably inside a budget it had not looked at. A measurement that clears
            // a ceiling by a factor of forty is a measurement to check, not a result.
            var toCamera = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * world.inverse;

            return GeometryUtility.CalculateFrustumPlanes(lens * toCamera);
        }

        /// <summary>
        /// Where the camera stands, given a point on the road and the way the column faces.
        ///
        /// <see cref="CameraOrbit.Offset"/>'s arithmetic, lifted out because the frustum
        /// needs this point and so does the LOD choice - and a second copy of it would
        /// drift from the first the day the camera's resting place moves.
        /// </summary>
        internal static Vector3 Eye(Vector3 look, Vector3 ahead, float range, float pitch)
        {
            float heading = Mathf.Atan2(ahead.x, ahead.z);
            float angle = heading + Mathf.PI;
            float tilt = pitch * Mathf.Deg2Rad;

            float ground = range * Mathf.Cos(tilt);

            return look + new Vector3(Mathf.Sin(angle) * ground,
                                      range * Mathf.Sin(tilt),
                                      Mathf.Cos(angle) * ground);
        }

        /// <summary>
        /// Which level of each LOD group the engine would draw from where the camera is.
        ///
        /// The engine's own rule: a group's share of the screen is its world size over its
        /// distance, divided by twice the tangent of half the vertical field of view, and
        /// the first level whose transition height that share still clears is the one
        /// drawn. Past the last one the group is not drawn at all, which is what makes an
        /// LOD chain also a cull.
        ///
        /// The quality setting's LOD bias is applied, because it is a multiplier on that
        /// share and the project ships whatever it is set to - measuring with a bias of
        /// one when the game runs at two would report the far tree where the near one is
        /// drawn.
        /// </summary>
        internal static void Choose(List<Steps> groups, int[] showing, Vector3 from, float fov)
            => Choose(groups, showing, null, from, fov);

        /// <summary>
        /// The same, keeping each group's share of the screen rather than throwing it away.
        ///
        /// <b>Because the share is the only thing that says where a rung belongs.</b> The
        /// level a group draws is a yes-or-no answer to a question with a number behind it,
        /// and the number is what decides whether a cheaper level in that band would be
        /// seen. Seventy-nine meadow trees drawing a 12 700-face level says nothing about
        /// where to cut; seventy-nine shares between 0,138 and 0,371 says exactly where.
        /// </summary>
        internal static void Choose(List<Steps> groups, int[] showing, float[] shares,
                                    Vector3 from, float fov)
        {
            float lens = 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float bias = QualitySettings.lodBias;

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];

                float away = Vector3.Distance(from, group.At);
                float share = away > 0.0001f ? group.Size / (away * lens) * bias : float.MaxValue;

                if (shares != null) shares[i] = share;

                showing[i] = -1;

                for (int lod = 0; lod < group.Until.Length; lod++)
                {
                    if (share <= group.Until[lod]) continue;

                    showing[i] = lod;
                    break;
                }
            }
        }

        /// <summary>
        /// For each mesh drawn in one frame, how tall on screen each copy of it stood.
        ///
        /// <b>The question this answers, and the one it does not.</b> Level Costs said the
        /// meadow tree's LOD1 costs 12 700 faces and covers the screen band from 0,371
        /// down to 0,138, and the frame said that level is 29,6 per cent of everything
        /// drawn. Neither says whether a cheap rung put at 0,20 would be seen by anybody,
        /// because that depends on how the copies are spread inside the band - and a
        /// level's own thresholds cannot tell you, since they are the same for a tree
        /// filling a third of the screen and one filling a seventh.
        ///
        /// The shares are the biased ones, as the engine compares them, so they can be
        /// read straight against the thresholds in <see cref="Steps.Until"/>.
        /// </summary>
        internal static Dictionary<Mesh, List<Stood>> Spread(
            List<Piece> pieces, int[] showing, float[] shares, Plane[] view,
            Vector3 from, float fov)
        {
            var spread = new Dictionary<Mesh, List<Stood>>();

            foreach (var piece in pieces)
            {
                if (piece.Group >= 0 && showing[piece.Group] != piece.Lod) continue;
                if (piece.Shape == null) continue;
                if (!GeometryUtility.TestPlanesAABB(view, piece.Where)) continue;

                if (!spread.TryGetValue(piece.Shape, out var found))
                    spread[piece.Shape] = found = new List<Stood>();

                // <b>A piece with no group still has a height on screen.</b> For a fortnight
                // this recorded a sentinel for it instead, on the reasoning that a model
                // with no chain has no share - and that is true of the share the engine
                // compares, because there is no threshold to compare it to. It is not true
                // of the thing the number is for. The whole question about ground cover is
                // how tall a reed stands when there are a thousand of them, and the answer
                // was being thrown away at this line: the twelve heaviest models in the
                // phone's frame were all chainless, so the table had a row for each of them
                // and a number in none.
                //
                // Measured the way the engine would if the model had a group: its own world
                // bounds standing in for the size RecalculateBounds would hand that group.
                found.Add(new Stood
                {
                    Share = piece.Group >= 0 ? shares[piece.Group]
                                             : Share(piece.Where, from, fov),
                    Chained = piece.Group >= 0,
                });
            }

            return spread;
        }

        /// <summary>One drawn copy, as the spread needs to read it.</summary>
        internal struct Stood
        {
            /// <summary>Its height on screen as a fraction, biased as the engine has it.</summary>
            public float Share;

            /// <summary>Whether that share is one a threshold is actually compared against.</summary>
            public bool Chained;
        }

        /// <summary>
        /// How tall a thing of these world bounds stands on screen, as a fraction of the
        /// screen's height, from here.
        ///
        /// The same arithmetic <see cref="Choose"/> does on a group, with the model's own
        /// bounds standing in for <see cref="Steps.Size"/> - which is what an LODGroup's
        /// RecalculateBounds would put there, the widest span of the box its renderers
        /// fill. So a chainless model's figure can be read straight against the thresholds
        /// a chain would be given.
        /// </summary>
        internal static float Share(Bounds where, Vector3 from, float fov)
        {
            float size = Mathf.Max(where.size.x,
                                   Mathf.Max(where.size.y, where.size.z));

            float away = Vector3.Distance(from, where.center);
            if (away < 0.0001f) return float.MaxValue;

            float lens = 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            return size / (away * lens) * QualitySettings.lodBias;
        }

        /// <summary>
        /// Where in its band each heavy model's copies actually stood, model by model.
        ///
        /// Printed as how many copies cleared a set of candidate thresholds, because that
        /// is the arithmetic a rung costs: the copies below a threshold are the ones a
        /// cheaper level would serve, and the ones above it are the ones that would be
        /// asked to look worse.
        /// </summary>
        internal static void Bands(string tag, StringBuilder sheet,
                                   Dictionary<Mesh, List<Stood>> spread,
                                   Dictionary<Mesh, long> frame, int all)
        {
            if (spread == null || frame == null || all <= 0) return;

            // <b>The low end is where this table's own subject lives.</b> The first version
            // stopped at 0,100, which was written for trees: the meadow tree's band runs
            // from 0,371 to 0,138 and the question was where inside it a rung belongs. The
            // heaviest models in the phone's frame are a 136-face reed and a 168-face reed
            // standing a thousand deep, and a reed is a metre tall - at ten metres it is
            // 0,107 of the screen and at a hundred it is 0,011. Every copy of it sat under
            // the first column and the table said nothing at all.
            var marks = new[]
            {
                0.300f, 0.200f, 0.150f, 0.100f, 0.070f,
                0.050f, 0.035f, 0.025f, 0.018f, 0.012f, 0.008f,
            };

            var rows = new List<KeyValuePair<Mesh, long>>(frame);
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));

            sheet.AppendLine($"[{tag}] how tall each copy stood, for the heaviest models in "
                             + $"that frame (lod bias {QualitySettings.lodBias:0.00})");
            sheet.AppendLine($"[{tag}] the columns count the copies standing taller than each "
                             + "share, so a rung put there would serve the rest");

            var header = new StringBuilder($"[{tag}]   {"model",-42} {"copies",6}  {"faces ea",8} ");
            foreach (float mark in marks) header.Append($" >{mark:0.000}");
            sheet.AppendLine(header.ToString());

            int bare = 0, bareFaces = 0;

            for (int i = 0; i < rows.Count && i < 12; i++)
            {
                var mesh = rows[i].Key;
                if (mesh == null || !spread.TryGetValue(mesh, out var seen)) continue;
                if (seen.Count == 0) continue;

                var line = new StringBuilder($"[{tag}]   {mesh.name,-42} {seen.Count,6}  "
                                             + $"{rows[i].Value / seen.Count,8} ");

                int chained = 0;
                foreach (var stood in seen) if (stood.Chained) chained++;

                foreach (float mark in marks)
                {
                    int over = 0;
                    foreach (var stood in seen) if (stood.Share > mark) over++;

                    line.Append($" {over,6}");
                }

                if (chained == 0)
                {
                    bare++;
                    bareFaces += (int)rows[i].Value;

                    line.Append("   no chain, so every one of them draws this");
                }
                else if (chained < seen.Count)
                {
                    line.Append($"   ({seen.Count - chained} of them in no chain)");
                }

                sheet.AppendLine(line.ToString());
            }

            sheet.AppendLine($"[{tag}] {bare} of those twelve hold no chain at all, "
                             + $"{bareFaces / 1000f:0.0}k triangles, "
                             + $"{100f * bareFaces / all:0.0}% of the frame - for those the "
                             + "columns are how tall the copies stand, not a threshold any "
                             + "of them is compared against");
        }

        /// <summary>
        /// What ending small ground cover would take out of this frame, at each share it
        /// might be ended at.
        ///
        /// <b>Because the gate at two hundred faces moved the problem rather than settling
        /// it.</b> <see cref="FarLods"/> thins a model by handing it a cheaper mesh, and a
        /// hundred and thirty-six-face reed has nothing to hand over: the cheapest rung the
        /// tool owns is eighty faces and the floor under a decimation is twenty-four. Two
        /// reeds are thirty per cent of the heaviest frame in the game between them, in two
        /// thousand copies. The only lever left on them is to stop drawing them.
        ///
        /// That is a judgement about how the country looks, and it is not this instrument's
        /// to make. What it can do is price every candidate before one is picked, so the
        /// judgement is made against triangles and metres rather than against a guess - and
        /// the gate it measures is <see cref="FarLods.Cover"/> itself, read from the tool,
        /// so the two cannot drift apart.
        /// </summary>
        internal static void Ending(string tag, StringBuilder sheet, List<Piece> pieces,
                                    int[] showing, Plane[] view, Vector3 from, float fov,
                                    int all)
        {
            if (pieces == null || all <= 0) return;

            var ends = new[] { 0.050f, 0.035f, 0.025f, 0.018f, 0.012f, 0.008f, 0.005f };

            var kinds = new HashSet<Mesh>();
            int copies = 0;
            long faces = 0;
            var went = new long[ends.Length];
            var gone = new int[ends.Length];

            foreach (var piece in pieces)
            {
                if (piece.Group >= 0 && showing[piece.Group] != piece.Lod) continue;
                if (piece.Shape == null) continue;
                if (!GeometryUtility.TestPlanesAABB(view, piece.Where)) continue;

                float size = Mathf.Max(piece.Where.size.x,
                                       Mathf.Max(piece.Where.size.y, piece.Where.size.z));

                if (size > FarLods.Cover) continue;

                kinds.Add(piece.Shape);
                copies++;
                faces += piece.Triangles;

                float share = Share(piece.Where, from, fov);

                for (int i = 0; i < ends.Length; i++)
                    if (share <= ends[i]) { went[i] += piece.Triangles; gone[i]++; }
            }

            sheet.AppendLine($"[{tag}] ground cover in that frame - anything under "
                             + $"{FarLods.Cover:0.0} m across, which is the gate that would "
                             + $"decide it ends: {kinds.Count} models, {copies} copies, "
                             + $"{faces / 1000f:0.0}k triangles, "
                             + $"{100f * faces / all:0.0}% of the frame");

            sheet.AppendLine($"[{tag}] what ending it below each share would take out, and "
                             + "how far a one-metre plant would then be drawn:");

            for (int i = 0; i < ends.Length; i++)
            {
                // <b>With the bias in it.</b> A threshold is compared against a share that
                // has already been multiplied by the quality level's lod bias, so the
                // distance a threshold actually reaches is the plain arithmetic times that
                // bias - and on the phone it is 0,70, which is the difference between
                // reading this table as 214 m and reading it as 150 m. The first version of
                // this line left it out and every metre it printed was 1,43x too far, which
                // is precisely the fault this whole report was rewritten to stop making.
                float reach = QualitySettings.lodBias / (ends[i] * FarLods.Lens);

                sheet.AppendLine($"[{tag}]   below {ends[i]:0.000} ({reach,4:0} m for a 1 m "
                                 + $"plant): {gone[i],6} of {copies} copies go, "
                                 + $"{went[i] / 1000f,7:0.0}k triangles, "
                                 + $"{100f * went[i] / all,5:0.0}% of the frame");
            }
        }

        /// <summary>
        /// The same view again, split by model: which mesh owns how many of the triangles
        /// in this one frame.
        ///
        /// A second walk rather than a collector threaded through the counting one,
        /// because it runs only when a view beats the level's best and the counting walk
        /// runs thirty-six times a level.
        /// </summary>
        internal static Dictionary<Mesh, long> Breakdown(List<Piece> pieces, int[] showing, Plane[] view)
        {
            var owned = new Dictionary<Mesh, long>();

            foreach (var piece in pieces)
            {
                if (piece.Group >= 0 && showing[piece.Group] != piece.Lod) continue;
                if (!GeometryUtility.TestPlanesAABB(view, piece.Where)) continue;

                owned.TryGetValue(piece.Shape, out long had);
                owned[piece.Shape] = had + piece.Triangles;
            }

            return owned;
        }

        /// <summary>What a frustum holds, counted the way the budget asks for it.</summary>
        internal static Seen Through(List<Piece> pieces, int[] showing, Plane[] view)
        {
            var paints = new HashSet<Material>();
            var batches = new HashSet<string>();
            var seen = new Seen();

            foreach (var piece in pieces)
            {
                // The level this piece belongs to has to be the one being drawn. Without
                // this the near tree, the middle tree and the far tree are all counted
                // together, which is how the same meadow tree came back three times with
                // an identical copy count.
                if (piece.Group >= 0 && showing[piece.Group] != piece.Lod) continue;

                if (!GeometryUtility.TestPlanesAABB(view, piece.Where)) continue;

                seen.Triangles += piece.Triangles;
                seen.Pieces++;
                if (piece.Skinned) seen.Skinned++;

                paints.Add(piece.Paint);

                // Keyed by both instance ids, because a material and a mesh together are
                // what one instanced call can draw any number of copies of.
                int paint = piece.Paint != null ? piece.Paint.GetInstanceID() : 0;
                int shape = piece.Shape != null ? piece.Shape.GetInstanceID() : 0;
                batches.Add(paint + ":" + shape);
            }

            seen.Materials = paints.Count;
            seen.Instanced = batches.Count;

            return seen;
        }

        /// <summary>
        /// The worst level on each line of the budget, kept as the sheet is written.
        ///
        /// Each line's own worst, not one level's row: the budget is seven separate
        /// ceilings and the level that sees the most triangles need not be the one that
        /// takes the longest to load. Reporting a single worst level would hide whichever
        /// of the seven it does not happen to be worst at, which is how a ceiling gets
        /// burst in a column nobody read.
        /// </summary>
        sealed class Worst
        {
            int _calls, _instanced, _triangles, _skinned;
            long _bytes;
            float _load;
            string _callsAt = "-", _instancedAt = "-", _trianglesAt = "-";
            string _bytesAt = "-", _loadAt = "-";

            public void Take(int chapter, int level, Seen seen, long bytes, float load)
            {
                string where = $"{chapter}-{level}";

                if (seen.Materials > _calls) { _calls = seen.Materials; _callsAt = where; }
                if (seen.Instanced > _instanced) { _instanced = seen.Instanced; _instancedAt = where; }
                if (seen.Triangles > _triangles) { _triangles = seen.Triangles; _trianglesAt = where; }
                if (seen.Skinned > _skinned) _skinned = seen.Skinned;
                if (bytes > _bytes) { _bytes = bytes; _bytesAt = where; }
                if (load > _load) { _load = load; _loadAt = where; }
            }

            public void Write(StringBuilder sheet)
            {
                sheet.AppendLine("[Budget] worst in the game, line by line:");

                Line(sheet, "draw calls, a material each", _calls, WantedCalls, _callsAt);
                Line(sheet, "draw calls, fully instanced", _instanced, WantedCalls, _instancedAt);
                Line(sheet, "visible triangles", _triangles, WantedTriangles, _trianglesAt);
                // Reported, and not as a verdict. SmokeTest.Build stands up the ground and
                // the scenery and stops - the caravan, the escort and the enemy all need a
                // squad on the road, which is a run and not a build. So this counts the
                // scenery's skinned meshes, and the scenery has none. The useful reading is
                // not "within": it is that the whole allowance of forty is still unspent
                // when the player arrives, and what spends it is the cast. That line needs
                // a run, and the frame rate needs a phone.
                const string Cast = "skinned meshes, scenery only";

                sheet.AppendLine($"[Budget]   {Cast,-30} {_skinned,8} against {WantedSkinned,8}  "
                                 + "not a verdict  (the cast is not in a built level)");
                Line(sheet, "memory, MB", (int)(_bytes / 1048576), WantedMegabytes, _bytesAt);
                Line(sheet, "load, hundredths of a second", (int)(_load * 100),
                     (int)(WantedLoad * 100), _loadAt);

                sheet.AppendLine("[Budget] the frame rate and the package size are not in "
                                 + "here: one needs a phone and the other needs a build");
            }

            static void Line(StringBuilder sheet, string what, int got, int want, string where)
            {
                string verdict = got <= want ? "within" : $"OVER by {(float)got / want:0.0}x";

                sheet.AppendLine($"[Budget]   {what,-30} {got,8} against {want,8}  "
                                 + $"{verdict,-12}  (worst at {where})");
            }
        }

        /// <summary>
        /// Which models the triangles belong to, over the whole game.
        ///
        /// <b>Because a hundred and fifteen million triangles is a number with nowhere to
        /// go.</b> The budget line being burst four hundred and sixty times over says the
        /// scenery is too heavy and says nothing about which part of it, and the two
        /// answers - decimate these eight meshes, or stand fewer of them up - cost very
        /// different things. One of them is a morning with
        /// <see cref="MeshDecimator"/> and the other is a visible thinning of the country.
        ///
        /// Counted over every level rather than over the worst one, because a model that
        /// is merely heavy on 4-7 and also stands in nine chapters is the better thing to
        /// fix. Per mesh, so four hundred copies of one tree are one row with four hundred
        /// in its count - which is what makes the row actionable: halving that mesh pays
        /// four hundred times on that level alone.
        ///
        /// Instances rather than visible instances, since the levels report ninety-nine
        /// per cent of their pieces inside the frustum. A sixty-four tile map is two
        /// hundred and fifty metres across and the camera may stand a hundred and twenty
        /// back, so there is nothing for frustum culling to remove. That is a finding of
        /// its own and it is why these two numbers are nearly the same.
        /// </summary>
        sealed class Census
        {
            struct Tally
            {
                public string Name;
                public int Faces;
                public int Copies;
            }

            readonly Dictionary<Mesh, Tally> _seen = new Dictionary<Mesh, Tally>();

            public void Take(List<Piece> pieces)
            {
                foreach (var piece in pieces)
                {
                    if (piece.Shape == null) continue;

                    // The nearest level only, so a model appears once rather than once per
                    // rung of its LOD chain. What this table answers is which model is
                    // expensive when the player is standing next to it, and the far rungs
                    // of its own chain are not the answer to that.
                    if (piece.Group >= 0 && piece.Lod != 0) continue;

                    if (!_seen.TryGetValue(piece.Shape, out var tally))
                        tally = new Tally { Name = piece.Shape.name, Faces = piece.Triangles };

                    tally.Copies++;
                    _seen[piece.Shape] = tally;
                }
            }

            /// <summary>How many rows are worth reading. Twenty, and the rest is a tail.</summary>
            const int Rows = 20;

            public void Write(StringBuilder sheet)
            {
                var rows = new List<Tally>(_seen.Values);

                // By what the model costs the game, which is its own weight times how
                // often it is stood up - not by its weight alone. The heaviest single
                // mesh in a pack can be a landmark that exists once.
                rows.Sort((a, b) => ((long)b.Faces * b.Copies).CompareTo((long)a.Faces * a.Copies));

                long all = 0;
                foreach (var row in rows) all += (long)row.Faces * row.Copies;

                sheet.AppendLine($"[Budget] where the triangles are: {rows.Count} distinct meshes "
                                 + $"at their nearest level, {all / 1000000f:0.0}M triangles stood "
                                 + "up over all 100 levels - what the game would cost with "
                                 + "every prop in the player's face at once");

                for (int i = 0; i < rows.Count && i < Rows; i++)
                {
                    long cost = (long)rows[i].Faces * rows[i].Copies;

                    sheet.AppendLine($"[Budget]   {rows[i].Name,-42} {rows[i].Faces,7} faces "
                                     + $"x {rows[i].Copies,6} copies = {cost / 1000000f,6:0.0}M  "
                                     + $"{100f * cost / all,5:0.0}% of the game");
                }
            }
        }
    }
}
