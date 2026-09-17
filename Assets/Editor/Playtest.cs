using System.Collections.Generic;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Drives a caravan down a level and photographs it from the player's own camera.
    ///
    /// <b>Every visual fault this project has cost a day on was found by somebody
    /// playing, and reported as a sentence.</b> The caravan starts in the water. The
    /// horses walk through the bridge. The water does not match itself. Each one was
    /// obvious on screen and invisible to every measurement in the repository, because
    /// the measurements read the simulation and the fault was in what was drawn from it.
    ///
    /// SmokeTest already stands a level up and photographs the country. What it does not
    /// do is put the column on the road and move it, so the things that go wrong *between*
    /// the simulation and the view - a wagon at one height and its team at another, a
    /// route that does not match the map it was drawn on - never appear in a picture.
    /// This runs the level and takes the picture from where the player would be sitting.
    ///
    /// It is not a replacement for playing. Nothing here notices that a fight is dull or
    /// that a road is unfair. It notices that something is in the wrong place, which is
    /// most of what a playtest has been finding.
    ///
    /// <b>And it cannot be trusted about water.</b> Batch mode renders the river and the
    /// lakes as a flat white sheet - checked both ways round, with the surface normals
    /// pointing up and with them recalculated from the mesh, and the picture is identical,
    /// so it is the pipeline and not the mesh. URP is not fully stood up behind a bare
    /// Camera.Render, and the water shader is the one thing here that depends on it. Every
    /// other thing in the frame is where it says it is; the water is the colour of nothing
    /// at all. A fault reported off these pictures about how water *looks* is a fault in
    /// this tool.
    /// </summary>
    public static class Playtest
    {
        /// <summary>Seconds of run between photographs.</summary>
        // Six. A caravan does about two tiles a second, so this is a picture every fifty
        // metres or so - close enough that a crossing cannot happen entirely between two
        // of them, far enough that ten pictures cover a level.
        const float Every = 6f;

        /// <summary>How many photographs one level is worth.</summary>
        const int Frames = 12;

        /// <summary>The step the simulation is driven at, in seconds.</summary>
        // The run's own fixed step, taken from the run rather than written down again:
        // two numbers for one tick is how a tool ends up photographing a game nobody
        // plays.
        const float Step = LevelRun.StepSeconds;

        [MenuItem("The Veil/Playtest Photos")]
        public static void Run()
        {
            foreach (var road in new[] { CorridorKind.Fast, CorridorKind.Safe, CorridorKind.Odd })
                Shoot(1, 10, road);
        }

        /// <summary>
        /// The column at the water, which is where a playtest keeps finding things.
        ///
        /// <b>Twelve pictures six seconds apart do not reach the far end of a level,</b>
        /// and the crossings are usually past where they stop: measured on 1-10, seventy
        /// seconds of run covers about two hundred metres of a three hundred and forty
        /// metre road. So this skips to them - runs the level with no pictures until the
        /// lead wagon is close to a ford, then takes one every second while it crosses.
        ///
        /// Every fault reported off a bridge so far has been a thing that is only wrong
        /// for the few seconds a wheel is on the planking: a team at bank height while
        /// the cart behind it is on the deck, a column coming at the boards from an
        /// angle. There is no use photographing a level every six seconds to find them.
        /// </summary>
        [MenuItem("The Veil/Playtest Crossings")]
        public static void Crossings()
        {
            foreach (var road in new[] { CorridorKind.Fast, CorridorKind.Safe, CorridorKind.Odd })
                AtTheWater(1, 10, road);
        }

        /// <summary>
        /// One level, one road, photographed the length of it.
        ///
        /// The fast road by default because it is the one the design now loads with the
        /// most threat, so it is where the column spends the most time doing something
        /// other than driving in a straight line.
        /// </summary>
        public static void Shoot(int chapter, int level, CorridorKind road)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Playtest] PlayLevel has no LevelRunner."); return; }

            var root = SmokeTest.Build(runner, chapter, level, out var map);

            var corridor = map.CorridorOf(road);
            if (corridor == null) { Debug.LogError($"[Playtest] {chapter}-{level} has no {road} road."); return; }

            var recipe = LevelMaps.Recipe(chapter, level);
            var squad = ReferenceSquad.For(recipe, ReferenceSquad.LevelsCleared(chapter, level),
                                           ReferenceSquad.Smithy(chapter));

            var run = new LevelRun(map, corridor.Tiles, squad, recipe.EnemyStrength);

            // The same visuals the run builds, on the world SmokeTest just stood up. A
            // second set of arguments here would be a second world - see SmokeTest.Build.
            var markers = new GameObject("Column");
            markers.transform.SetParent(root.transform, false);

            var visuals = new RunVisuals(markers.transform, map.Grid, runner.HeightScale)
            {
                Library = runner.Models,
                Chapter = chapter
            };

            visuals.FindBridges(root.transform);
            visuals.FindObstacles(root.transform, run);
            visuals.Build(run);
            visuals.Sync(run);

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlaytest");
            System.IO.Directory.CreateDirectory(shots);

            var said = new System.Text.StringBuilder();
            said.AppendLine($"[Playtest] {chapter}-{level}, the {road} road: "
                            + $"{corridor.Tiles.Count} tiles, par {run.ParSeconds:0} s");

            float elapsed = 0f;

            for (int frame = 0; frame < Frames; frame++)
            {
                var at = run.Caravan.WagonPosition(0);
                string under = Under(map, at);

                said.AppendLine($"[Playtest] {frame * Every:0} s: "
                                + $"{run.Caravan.DistanceTravelled:0} m in, on {under}, "
                                + $"{Alive(run)} of the escort up, {Wagons(run)} wagons");

                Shoot(visuals, run, System.IO.Path.Combine(shots,
                    $"{chapter}-{level}-{road}-{frame:00}.png"));

                for (float t = 0f; t < Every && run.Outcome == RunOutcome.InProgress; t += Step)
                {
                    run.Step();
                    elapsed += Step;
                }

                visuals.Sync(run);

                if (run.Outcome != RunOutcome.InProgress)
                {
                    said.AppendLine($"[Playtest] {elapsed:0} s: {run.Outcome}");
                    Shoot(visuals, run, System.IO.Path.Combine(shots,
                        $"{chapter}-{level}-{road}-end.png"));
                    break;
                }
            }

            said.AppendLine($"[Playtest] pictures in {shots}");
            Debug.Log(said.ToString());

            Object.DestroyImmediate(root);
        }

        /// <summary>How near the water the column is photographed from, in metres.</summary>
        // Thirty out and thirty past, which at the caravan's pace is about fifteen
        // seconds either side of the planking - enough to see it line up, cross, and
        // leave.
        const float Approach = 30f;

        /// <summary>Drives to each crossing on a road and photographs the column over it.</summary>
        public static void AtTheWater(int chapter, int level, CorridorKind road)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Playtest] PlayLevel has no LevelRunner."); return; }

            var root = SmokeTest.Build(runner, chapter, level, out var map);

            var corridor = map.CorridorOf(road);
            if (corridor == null) { Object.DestroyImmediate(root); return; }

            var recipe = LevelMaps.Recipe(chapter, level);
            var squad = ReferenceSquad.For(recipe, ReferenceSquad.LevelsCleared(chapter, level),
                                           ReferenceSquad.Smithy(chapter));

            // The route the run drives, which is the corridor squared at its crossings -
            // see Crossings.Square. Reading the corridor's own tiles would put the water
            // somewhere the caravan never goes.
            var run = new LevelRun(map, corridor.Tiles, squad, recipe.EnemyStrength);

            var fords = new List<float>();
            float along = 0f;

            for (int i = 1; i < run.Route.Count; i++)
            {
                map.Grid.ToCoords(run.Route[i - 1], out int px, out int py);
                map.Grid.ToCoords(run.Route[i], out int x, out int y);

                along += (px != x && py != y ? 1.41421356f : 1f) * TileGrid.TileSize;

                if (map.Grid[run.Route[i]] == TerrainType.Ford
                    && (fords.Count == 0 || along - fords[fords.Count - 1] > 40f))
                    fords.Add(along);
            }

            var said = new System.Text.StringBuilder();
            said.AppendLine($"[Water] {chapter}-{level}, the {road} road: {fords.Count} crossing(s)");

            if (fords.Count == 0)
            {
                Debug.Log(said.ToString());
                Object.DestroyImmediate(root);
                return;
            }

            var markers = new GameObject("Column");
            markers.transform.SetParent(root.transform, false);

            var visuals = new RunVisuals(markers.transform, map.Grid, runner.HeightScale)
            {
                Library = runner.Models,
                Chapter = chapter
            };

            visuals.FindBridges(root.transform);
            visuals.FindObstacles(root.transform, run);
            visuals.Build(run);

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlaytest");
            System.IO.Directory.CreateDirectory(shots);

            int crossing = 0;

            foreach (float at in fords)
            {
                // Up to the near bank without a picture, then one a second across.
                while (run.Outcome == RunOutcome.InProgress
                       && run.Caravan.DistanceTravelled < at - Approach)
                    run.Step();

                if (run.Outcome != RunOutcome.InProgress) break;

                for (int shot = 0; shot < 12; shot++)
                {
                    visuals.Sync(run);

                    var lead = run.Caravan.WagonPosition(0);
                    said.AppendLine($"[Water] crossing {crossing}, {shot} s: "
                                    + $"{run.Caravan.DistanceTravelled - at:+0;-0} m from the water, "
                                    + $"lead wagon on {Under(map, lead)}");

                    Shoot(visuals, run, System.IO.Path.Combine(shots,
                        $"{chapter}-{level}-{road}-ford{crossing}-{shot:00}.png"));

                    for (float t = 0f; t < 1f && run.Outcome == RunOutcome.InProgress; t += Step)
                        run.Step();

                    if (run.Outcome != RunOutcome.InProgress) break;
                }

                crossing++;
                if (run.Outcome != RunOutcome.InProgress) break;
            }

            said.AppendLine($"[Water] pictures in {shots}");
            Debug.Log(said.ToString());

            Object.DestroyImmediate(root);
        }

        /// <summary>What the lead wagon is standing on, which is the fault that started this.</summary>
        static string Under(LevelMap map, Vec2 at)
        {
            int x = (int)(at.X / TileGrid.TileSize);
            int y = (int)(at.Y / TileGrid.TileSize);

            return map.Grid.InBounds(x, y) ? map.Grid[map.Grid.ToIndex(x, y)].ToString() : "off the map";
        }

        static int Alive(LevelRun run)
        {
            int up = 0;
            foreach (var group in run.Squad.Slots) if (group != null && group.Alive) up++;
            return up;
        }

        static int Wagons(LevelRun run)
        {
            int left = 0;
            foreach (var wagon in run.Caravan.Wagons) if (!wagon.Destroyed) left++;
            return left;
        }

        /// <summary>
        /// The player's own view: behind the column and above it, at LevelRunner's own
        /// follow distance and height.
        ///
        /// Those two numbers are where every judgement in the design notes about how big
        /// a thing reads was made from, so a photograph taken from anywhere else is a
        /// photograph of a different game.
        /// </summary>
        static void Shoot(RunVisuals visuals, LevelRun run, string path)
        {
            var middle = run.Caravan.WagonPosition(1);
            var heading = run.Caravan.Heading;

            var at = new Vector3(middle.X, visuals.GroundAt(middle), middle.Y);
            var behind = new Vector3(heading.X, 0f, heading.Y).normalized;

            var go = new GameObject("Playtest camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = at - behind * 40f + Vector3.up * 47f;
            camera.transform.LookAt(at);
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);

            const int width = 1400, height = 900;

            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
