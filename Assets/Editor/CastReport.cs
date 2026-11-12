using System.Collections.Generic;
using System.Text;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TheVeil.Editor
{
    /// <summary>
    /// The budget counted with the cast in the frame: `The Veil &gt; Cast Report`.
    ///
    /// <b>Because the budget report stands up the country and nothing that walks on
    /// it.</b> It builds a level with <see cref="SmokeTest.Build"/>, which is the ground
    /// and the props - and the column, the escort, the enemies, thirty-one animals and
    /// the crows are not props. They are built by <see cref="RunVisuals"/> from a run,
    /// and a run is what that report does not have. So the three counted lines of §1 are
    /// all scenery-only figures: 0,76M visible triangles, 252 instanced calls, and a
    /// skinned-mesh count of nought against an allowance of forty. <b>Those two numbers
    /// are a reading and not a constant</b>, which is why the sheet now prints the day
    /// they were taken - they were 3,09M and 229 when this was written, and the far LOD
    /// work has moved them four times since.
    ///
    /// A count of nought skinned meshes in a game with a mounted column, a squad and a
    /// herd of deer in it is not a measurement, and the sheet said so in those words -
    /// "skinned meshes, scenery only". This is the run that fills the hole.
    ///
    /// <b>The column moves, so the sampling is in time rather than in space.</b> The
    /// budget report stands the camera at nine points along the road because the country
    /// does not change; here the camera is where the player's camera is, behind the
    /// column, and what is in frame changes as the column walks into a fight and out the
    /// other side with fewer of itself. So the run is stepped and read every few seconds
    /// of it, and the worst reading is the level's number.
    ///
    /// <b>Counted through one camera, not added up.</b> Scenery and cast go through
    /// <see cref="BudgetReport.Join"/> into a single frame before they are counted,
    /// because draw calls do not add: a material a wagon shares with a fence is one call
    /// and not two.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.CastReport.Run
    /// </summary>
    public static class CastReport
    {
        /// <summary>Seconds of run between readings.</summary>
        // Four. A column does about two tiles a second, so this is a reading every forty
        // metres - and, more to the point, several readings inside any one fight, which is
        // where the cast is at its largest and the only part of the run this is about.
        const float Every = 4f;

        /// <summary>The step the simulation is driven at, in seconds.</summary>
        const float Step = LevelRun.StepSeconds;

        /// <summary>How long one run is given before it is called stuck, in seconds.</summary>
        const float Longest = 400f;

        /// <summary>Which levels of each dressed chapter are read.</summary>
        // Three of the ten, and not all ten, because this walks a whole run per road per
        // level rather than standing a camera up nine times. The first, the middle and the
        // last of a chapter is the shape of its difficulty: the escort grows over a
        // chapter and so does what is sent against it, so the last level is where the cast
        // is biggest and the first is the floor.
        static readonly int[] Levels = { 1, 5, 10 };

        [MenuItem("The Veil/Cast Report")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                                     OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null)
            {
                Debug.LogError("[Cast] PlayLevel has no LevelRunner, so there is nothing to build.");
                return;
            }

            var eye = Object.FindAnyObjectByType<Camera>();
            float fov = eye != null ? eye.fieldOfView : 60f;

            var sheet = new StringBuilder();
            sheet.AppendLine("[Cast] the budget with the column, the squad, the enemies, "
                             + "the animals and the crows in the frame");
            sheet.AppendLine($"[Cast] lens {fov:0} degrees, camera at "
                             + $"{CameraOrbit.DefaultRange:0} m and {CameraOrbit.MaxRange:0} m, "
                             + $"a reading every {Every:0} s of run, three roads, worst kept");
            sheet.AppendLine($"[Cast] want skinned <= {BudgetReport.WantedSkinned}; "
                             + "skinned is reported as in frame / standing, and the "
                             + "scenery-only figures to hold the rest against are "
                             + "757 756 triangles and 252 instanced calls, read off "
                             + "The Veil > Budget Report on 2026-11-09");

            // Before anything is counted, because the bias decides which level of
            // every chain the counting sees.
            int wasQuality = BudgetReport.Phone(sheet, "Cast");

            var worst = new Worst();

            int heaviest = 0;
            Dictionary<Mesh, long> worstFrame = null;
            Dictionary<Mesh, List<BudgetReport.Stood>> worstSpread = null;
            string worstEnding = null;

            // Kept apart from the heaviest frame on purpose: the frame worst by calls is
            // not the frame worst by triangles, and reading one of them as the other is
            // how a per-model figure gets compared across two runs that were looking at
            // different country.
            int worstCalls = 0;
            string worstCallsText = null;

            // And the same for the skinned line, kept apart for the same reason: the frame
            // with the most skinned figures in it is not the frame with the most triangles.
            int worstSkinned = 0;
            string worstSkinnedText = null;

            // <b>And the rows asked for by name, because the worst row moves.</b> Run this
            // against the prefabs as they were before the ground cover ending and the frame
            // worst by calls is 4-10-Fast at 319; run it against them as they are and it is
            // 8-10-Odd at 345. Diffing one against the other compares two different pieces
            // of country, which is the fault this report has already made once and caught.
            // So a row can be named on the command line - `-rows 8-1-Fast,8-10-Odd` - and
            // its own call list is kept whatever else is worse.
            var asked = Asked();
            var byRow = new Dictionary<string, string>();
            string worstFrameAt = "-";

            foreach (int chapter in DifficultyCurve.Dressed)
            {
                foreach (int level in Levels)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);

                    // Read once. The country does not move, and reading it is the
                    // expensive half of this.
                    var country = BudgetReport.Pieces(root);

                    foreach (var kind in new[] { CorridorKind.Fast, CorridorKind.Safe,
                                                 CorridorKind.Odd })
                    {
                        var corridor = map.CorridorOf(kind);
                        if (corridor == null) continue;

                        var seen = Walk(runner, root, map, country, chapter, level, kind, fov,
                                        out int standing, out int castTriangles,
                                        out int castPieces, out Dictionary<Mesh, long> frame,
                                        out Dictionary<Mesh, List<BudgetReport.Stood>> spread,
                                        out string ending, out string calls,
                                        out string census);

                        if (seen.Skinned > worstSkinned)
                        {
                            worstSkinned = seen.Skinned;
                            worstSkinnedText = census;
                        }

                        if (seen.Instanced > worstCalls)
                        {
                            worstCalls = seen.Instanced;
                            worstCallsText = calls;
                        }

                        string row = $"{chapter}-{level}-{kind}";
                        if (asked.Contains(row)) byRow[row] = calls;

                        sheet.AppendLine($"[Cast] {chapter}-{level,-2} {kind,-4} "
                                         + $"skinned {seen.Skinned,3} / {standing,3}  "
                                         + $"calls {seen.Materials,4} / {seen.Instanced,4}  "
                                         + $"tris {seen.Triangles / 1000f,7:0.0}k  "
                                         + $"of which cast {castTriangles / 1000f,6:0.0}k "
                                         + $"({(seen.Triangles > 0 ? 100f * castTriangles / seen.Triangles : 0f),4:0.0}%)  "
                                         + $"cast pieces {castPieces,4}");

                        if (frame != null && seen.Triangles > heaviest)
                        {
                            heaviest = seen.Triangles;
                            worstFrame = frame;
                            worstSpread = spread;
                            worstEnding = ending;
                            worstFrameAt = $"{chapter}-{level}-{kind}";
                        }

                        worst.Take(chapter, level, kind, seen, standing, castTriangles);
                    }

                    Object.DestroyImmediate(root);
                }
            }

            worst.Write(sheet);
            BudgetReport.Frame("Cast", sheet, worstFrame, worstFrameAt, heaviest);
            BudgetReport.Bands("Cast", sheet, worstSpread, worstFrame, heaviest);
            if (!string.IsNullOrEmpty(worstSkinnedText)) sheet.Append(worstSkinnedText);
            if (!string.IsNullOrEmpty(worstEnding)) sheet.Append(worstEnding);
            if (!string.IsNullOrEmpty(worstCallsText)) sheet.Append(worstCallsText);

            foreach (var pair in byRow)
            {
                sheet.AppendLine($"[Cast] and the call list for {pair.Key}, asked for by "
                                 + "name so it can be read against another run of the same "
                                 + "row:");

                if (!string.IsNullOrEmpty(pair.Value)) sheet.Append(pair.Value);
            }
            Debug.Log(sheet.ToString());

            QualitySettings.SetQualityLevel(wasQuality, true);
            EditorSceneManager.CloseScene(scene, false);
        }

        /// <summary>
        /// One run down one road, read every few seconds, worst reading kept.
        ///
        /// <b>Sync is called at the readings and not at every step.</b> It is what puts
        /// the models where the simulation says they are, and the simulation is what needs
        /// every step; moving a few hundred transforms eighty times a second of run and
        /// then looking at them twice would be most of this tool's cost for nothing.
        /// </summary>
        /// <summary>
        /// The rows whose call lists were asked for on the command line, as
        /// <c>-rows 8-1-Fast,8-10-Odd</c>.
        ///
        /// Empty when nothing was asked, and then only the frame worst by calls is listed.
        /// A row is named chapter-level-road, exactly as the per-row lines above print it.
        /// </summary>
        static HashSet<string> Asked()
        {
            var want = new HashSet<string>();
            var args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-rows") continue;

                foreach (string one in args[i + 1].Split(','))
                    if (!string.IsNullOrWhiteSpace(one)) want.Add(one.Trim());

                break;
            }

            return want;
        }

        static BudgetReport.Seen Walk(LevelRunner runner, GameObject root, LevelMap map,
                                      BudgetReport.Standing country,
                                      int chapter, int level, CorridorKind kind, float fov,
                                      out int standing, out int castTriangles,
                                      out int castPieces, out Dictionary<Mesh, long> frame,
                                      out Dictionary<Mesh, List<BudgetReport.Stood>> spread,
                                      out string ending, out string calls, out string census)
        {
            var peak = new BudgetReport.Seen();
            standing = castTriangles = castPieces = 0;
            frame = null;
            spread = null;
            ending = null;
            calls = null;
            census = null;

            var corridor = map.CorridorOf(kind);
            var recipe = LevelMaps.Recipe(chapter, level);

            // The player's own squad, as Playtest learned to do it: a reference squad with
            // free equipment reports a game nobody plays.
            var run = ReferenceSquad.Play(map, corridor.Tiles, recipe,
                                          ReferenceSquad.LevelsCleared(chapter, level));

            var markers = new GameObject($"Column {kind}");
            markers.transform.SetParent(root.transform, false);

            // Everything LevelRunner stands up, in the order it stands it up. The caches,
            // the crows and the animals are not decoration this tool added for interest:
            // they are built by the run and they are in the frame the player is looking
            // at, which is the whole of what this report is for.
            var visuals = new RunVisuals(markers.transform, map.Grid, runner.HeightScale)
            {
                Library = runner.Models,
                ShowReach = runner.ShowReach,
                Chapter = chapter,
                TracksInSnow = Biomes.Of(chapter) == Biome.Winter
            };

            visuals.FindBridges(root.transform);
            visuals.FindObstacles(root.transform, run);
            visuals.Build(run);
            visuals.BuildCaches(map.Encounters.SilverCaches, map.Grid);
            visuals.BuildCrowFlocks(CrowSignal.Place(map), map.Grid);

            var animals = Wildlife.Populate(map, run.Obstacles);
            visuals.BuildWildlife(animals);

            var ranges = new[] { CameraOrbit.DefaultRange, CameraOrbit.MaxRange };
            var shapes = new[] { BudgetReport.Portrait, BudgetReport.Landscape };

            float seconds = 0f, read = -1f;

            while (seconds < Longest)
            {
                if (read < 0f || seconds - read >= Every)
                {
                    read = seconds;

                    visuals.Sync(run);
                    visuals.SyncWildlife(animals);

                    var cast = BudgetReport.Pieces(markers);
                    var both = BudgetReport.Join(country, cast);

                    var showing = new int[both.Groups.Count];
                    var castShowing = new int[cast.Groups.Count];

                    // How tall each group stands on screen, kept alongside the level
                    // it picked. Choose works it out either way; throwing it away is
                    // what left the question of where a rung belongs unanswerable.
                    var shares = new float[both.Groups.Count];

                    // The middle of the column where there is one, which is what the
                    // camera is aimed at, and the lead wagon on a column of one.
                    var middle = run.Caravan.WagonPosition(
                        Mathf.Min(1, run.Caravan.Wagons.Count - 1));
                    var look = new Vector3(middle.X, visuals.GroundAt(middle), middle.Y);

                    var heading = run.Caravan.Heading;
                    var ahead = new Vector3(heading.X, 0f, heading.Y);
                    if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.forward;

                    foreach (float range in ranges)
                    {
                        float pitch = CameraOrbit.PitchFor(range);

                        foreach (float aspect in shapes)
                        {
                            var from = BudgetReport.Eye(look, ahead, range, pitch);
                            var view = BudgetReport.View(look, ahead, range, pitch, fov, aspect);

                            BudgetReport.Choose(both.Groups, showing, shares, from, fov);
                            BudgetReport.Choose(cast.Groups, castShowing, from, fov);

                            var seen = BudgetReport.Through(both.Pieces, showing, view);

                            int up = Skinned(cast, castShowing);
                            if (up > standing) standing = up;

                            if (seen.Triangles > peak.Triangles)
                            {
                                // The split, taken only when the frame is a record,
                                // because it is a second walk over the country.
                                var mine = BudgetReport.Through(cast.Pieces, castShowing, view);

                                castTriangles = mine.Triangles;
                                castPieces = mine.Pieces;

                                frame = BudgetReport.Breakdown(both.Pieces, showing, view);
                                spread = BudgetReport.Spread(both.Pieces, showing,
                                                             shares, view, from, fov);

                                // Priced here rather than at the end, because what it
                                // prices is this one frame's pieces and they are gone the
                                // moment the level is torn down. Held as text for the same
                                // reason the split above is held as a dictionary: only the
                                // record frame's copy is wanted.
                                var told = new StringBuilder();

                                BudgetReport.Ending("Cast", told, both.Pieces, showing, view,
                                                    from, fov, seen.Triangles);

                                ending = told.ToString();

                                peak.Triangles = seen.Triangles;
                            }

                            if (seen.Materials > peak.Materials) peak.Materials = seen.Materials;

                            if (seen.Instanced > peak.Instanced)
                            {
                                peak.Instanced = seen.Instanced;

                                // Taken at the record and not at the end, for the same
                                // reason the model split is: these pieces belong to a
                                // level that is torn down as soon as the row is finished.
                                var told = new StringBuilder();

                                // The view is named along with the row, because two runs
                                // search for their own worst view independently and may
                                // land on different ones - the camera is read at two
                                // ranges, two aspects and every four seconds of the run.
                                // If these two lines do not match, the lists below them
                                // are not of the same frame and must not be diffed.
                                BudgetReport.Batches("Cast", told,
                                                     $"{chapter}-{level}-{kind} at "
                                                     + $"{range:0} m, aspect {aspect:0.00}, "
                                                     + $"{seconds:0} s in",
                                                     both.Pieces, showing, view);

                                calls = told.ToString();
                            }
                            if (seen.Pieces > peak.Pieces) peak.Pieces = seen.Pieces;

                            if (seen.Skinned > peak.Skinned)
                            {
                                peak.Skinned = seen.Skinned;

                                // Taken at the record, like the model split and the call
                                // list, because these pieces belong to a level that is
                                // torn down when the row ends.
                                census = Census($"{chapter}-{level}-{kind} at {range:0} m, "
                                                + $"aspect {aspect:0.00}, {seconds:0} s in",
                                                both, showing, view, from);
                            }
                        }
                    }

                }

                if (run.Outcome != RunOutcome.InProgress) break;

                run.Step();
                seconds += Step;
            }

            Object.DestroyImmediate(markers);

            return peak;
        }

        /// <summary>
        /// How many skinned meshes the cast stands up, with the LOD chains resolved and
        /// nothing culled by the frustum.
        ///
        /// <b>Reported twice, like the draw calls, because the engine's two answers are
        /// far apart and neither alone is honest.</b> What is in frame is what a frustum
        /// cull leaves; this is what is behind the camera as well.
        ///
        /// <b>And the first version of this counted the chains instead, which is wrong in
        /// the harsh direction.</b> It asked the hierarchy for every enabled
        /// SkinnedMeshRenderer - and an LOD group leaves all of its levels enabled and
        /// picks one when it draws, so a deer with a three-rung chain was counted three
        /// times. That reported 397 against an allowance of forty, nine point nine times
        /// over, which would have been the worst-breached line in the project and is a
        /// property of the reader rather than of the game. The engine skins what it draws.
        /// </summary>
        static int Skinned(BudgetReport.Standing cast, int[] showing)
        {
            int count = 0;

            foreach (var piece in cast.Pieces)
            {
                if (piece.Group >= 0 && showing[piece.Group] != piece.Lod) continue;
                if (piece.Skinned) count++;
            }

            return count;
        }

        /// <summary>
        /// Who the skinned meshes in one frame are: by mesh, by rung, and by distance.
        ///
        /// <b>Because "169 against 40" names no suspect.</b> The line comes down two ways
        /// and only two: stand fewer of them up, or draw something that is not skinned
        /// where a skinned one is drawn now. Which of those is worth doing depends on
        /// whether the count is forty figures each carrying four renderers or a hundred
        /// and sixty figures each carrying one, and on whether they are at the camera's
        /// nose or out at the edge of the country - and nothing in this project could say
        /// which until this list existed.
        ///
        /// A chain's rung is printed as <c>lod/rungs</c>, and "-" for a renderer that has
        /// no chain at all. The distance is to the near edge of the renderer's bounds,
        /// which is a skinned figure's animation-sized box and so is a metre or two
        /// generous - it is read as a band and not as a measurement.
        /// </summary>
        static string Census(string where, BudgetReport.Standing both, int[] showing,
                             Plane[] view, Vector3 from)
        {
            var rows = new Dictionary<string, Row>();
            int all = 0, chained = 0, bottom = 0;

            foreach (var piece in both.Pieces)
            {
                if (piece.Group >= 0 && showing[piece.Group] != piece.Lod) continue;
                if (!piece.Skinned) continue;
                if (!GeometryUtility.TestPlanesAABB(view, piece.Where)) continue;

                int rungs = piece.Group >= 0 ? both.Groups[piece.Group].Until.Length : 0;
                float away = Mathf.Sqrt(piece.Where.SqrDistance(from));

                all++;
                if (rungs > 0) chained++;
                if (rungs > 0 && piece.Lod == rungs - 1) bottom++;

                string name = piece.Shape != null ? piece.Shape.name : "(no mesh)";
                string key = $"{name}|{piece.Lod}|{rungs}";

                if (!rows.TryGetValue(key, out var row))
                    row = new Row { Name = name, Lod = piece.Lod, Rungs = rungs,
                                    Near = float.MaxValue, Far = 0f };

                row.Count++;
                row.Triangles = piece.Triangles;
                if (away < row.Near) row.Near = away;
                if (away > row.Far) row.Far = away;

                rows[key] = row;
            }

            var sheet = new StringBuilder();

            sheet.AppendLine($"[Cast] the skinned meshes in frame, one by one, on {where}:");
            sheet.AppendLine($"[Cast]   {all} in frame, {chained} of them in a chain, "
                             + $"{bottom} of those on the chain's last rung");

            var sorted = new List<Row>(rows.Values);
            sorted.Sort((a, b) => b.Count != a.Count ? b.Count - a.Count
                                                     : string.Compare(a.Name, b.Name,
                                                                      System.StringComparison.Ordinal));

            foreach (var row in sorted)
                sheet.AppendLine($"[Cast]   {row.Count,3} x {row.Name,-44} "
                                 + $"rung {(row.Rungs > 0 ? $"{row.Lod}/{row.Rungs}" : "-"),-5} "
                                 + $"{row.Triangles,6} faces each  "
                                 + $"{row.Near,5:0} - {row.Far,5:0} m");

            return sheet.ToString();
        }

        struct Row
        {
            public string Name;
            public int Lod, Rungs, Count, Triangles;
            public float Near, Far;
        }

        /// <summary>The worst reading in the game on each line, and where it was taken.</summary>
        sealed class Worst
        {
            int _skinned, _seen, _triangles, _cast, _materials, _instanced;
            string _atSkinned = "-", _atSeen = "-", _atTriangles = "-", _atCast = "-";
            string _atMaterials = "-", _atInstanced = "-";

            public void Take(int chapter, int level, CorridorKind kind,
                             BudgetReport.Seen seen, int standing, int castTriangles)
            {
                string where = $"{chapter}-{level}-{kind}";

                if (standing > _skinned) { _skinned = standing; _atSkinned = where; }
                if (seen.Skinned > _seen) { _seen = seen.Skinned; _atSeen = where; }
                if (seen.Triangles > _triangles) { _triangles = seen.Triangles; _atTriangles = where; }
                if (castTriangles > _cast) { _cast = castTriangles; _atCast = where; }
                if (seen.Materials > _materials) { _materials = seen.Materials; _atMaterials = where; }
                if (seen.Instanced > _instanced) { _instanced = seen.Instanced; _atInstanced = where; }
            }

            public void Write(StringBuilder sheet)
            {
                sheet.AppendLine("[Cast] the worst of all of it:");

                Line(sheet, "skinned meshes standing", _skinned, BudgetReport.WantedSkinned,
                     _atSkinned);
                Line(sheet, "skinned meshes in frame", _seen, BudgetReport.WantedSkinned,
                     _atSeen);
                Line(sheet, "visible triangles", _triangles, 250000, _atTriangles);
                Line(sheet, "draw calls, instanced", _instanced, 150, _atInstanced);
                Line(sheet, "draw calls, a material each", _materials, 150, _atMaterials);

                sheet.AppendLine($"[Cast]   {"the cast's own triangles",-30} {_cast,8} "
                                 + $"at its worst, on {_atCast} - "
                                 + "no line of its own, it is part of the count above");
            }

            static void Line(StringBuilder sheet, string what, int got, int want, string where)
            {
                sheet.AppendLine($"[Cast]   {what,-30} {got,8} against {want,8}  "
                                 + (got <= want ? "within" : $"OVER by {(float)got / want:0.0}x")
                                 + $", worst on {where}");
            }
        }
    }
}
