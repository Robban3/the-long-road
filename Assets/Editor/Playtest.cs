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
    /// <b>Water sometimes comes out white, and it is not the water.</b> On the first run
    /// after a recompile the river and the lakes render as a flat pale sheet; on a later
    /// run of the same code they are blue. It was put down to URP not being stood up
    /// behind a bare Camera.Render - wrongly, since nothing about the pipeline changes
    /// between two runs of one build. It is the shader not being ready the first time it
    /// is asked for.
    ///
    /// So a white river means run it again, and a fault reported off a first run about
    /// how water *looks* is a fault in the timing rather than in the game.
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

        /// <summary>Where the pictures and the sheet go.</summary>
        static string Shots
        {
            get
            {
                string at = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlaytest");
                System.IO.Directory.CreateDirectory(at);
                return at;
            }
        }

        /// <summary>
        /// Writes the sheet to a file beside the pictures as well as to the console.
        ///
        /// <b>The console cannot be read from outside the editor</b>, and the editor is
        /// usually the one place this cannot run - it is open, somebody is playing in it.
        /// A tool whose findings can only be read by the person who did not need them is
        /// half a tool.
        /// </summary>
        static void Write(string name, System.Text.StringBuilder said)
        {
            string path = System.IO.Path.Combine(Shots, name);
            System.IO.File.WriteAllText(path, said.ToString());

            Debug.Log(said + "\n[Playtest] written to " + path);
        }

        [MenuItem("The Veil/Playtest Photos")]
        public static void Run()
        {
            foreach (var road in new[] { CorridorKind.Fast, CorridorKind.Safe, CorridorKind.Odd })
                Shoot(1, 10, road);
        }

        /// <summary>
        /// Whether the bridge the planning map shows is the bridge the run builds.
        ///
        /// <b>Reported from a playtest: the route was drawn over the bridge on the map and
        /// there was no bridge there in the game.</b> The two are decorated by the same
        /// call with the same seed and the same road, so they ought to agree - and "ought
        /// to" has been wrong about something every hour of this. The planning map and the
        /// run are the pair of answers this codebase has had the most trouble keeping
        /// together, and the honest thing is to build both and look.
        ///
        /// Decorated twice per level with the two callers' own arguments, taken from
        /// App.LevelPreview and App.LevelRunner rather than invented here, and the bridge
        /// found in each. A tile apart is the same crossing; anything more is the fault.
        /// </summary>
        [MenuItem("The Veil/Bridge On Both Maps")]
        public static void BothMaps()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Both] PlayLevel has no LevelRunner."); return; }

            var said = new System.Text.StringBuilder();
            said.AppendLine("[Both] the bridge the map shows against the bridge the run builds");

            int apart = 0, missing = 0;

            for (int chapter = 1; chapter <= 3; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);
                    var biome = Biomes.Of(chapter);
                    var look = runner.LookFor(biome);
                    var decor = look != null && look.Dressed ? look.Decor : runner.Decor;

                    // What BridgeTile answers, which is what both callers ask it.
                    int named = TerrainDecorator.BridgeTile(map.Grid, map.Seed,
                                                            LevelPreview.Travelled(map));

                    var plan = Where(Decorated(map, decor, runner, chapter, level, plan: true), map);
                    var road = Where(Decorated(map, decor, runner, chapter, level, plan: false), map);

                    if (plan < 0 || road < 0)
                    {
                        said.AppendLine($"[Both] {chapter}-{level}: BridgeTile says {named}, "
                                        + $"map built {plan}, run built {road}"
                                        + (plan != road ? "  <-- ONE OF THEM HAS NO BRIDGE" : ""));
                        if (plan != road) missing++;
                        continue;
                    }

                    map.Grid.ToCoords(plan, out int px, out int py);
                    map.Grid.ToCoords(road, out int rx, out int ry);

                    int gap = Mathf.Abs(px - rx) + Mathf.Abs(py - ry);
                    if (gap > 1) apart++;

                    // <b>And whether a caravan driving the road goes over the deck or
                    // past it.</b> The route is squared onto the row the middle of its
                    // ford is on (Crossings.Square) and the bridge is laid on the tile
                    // BridgeTile named; if those are different rows, the player draws a
                    // line over the bridge on the map and drives beside it in the game,
                    // which is what a playtest just reported.
                    int off = int.MaxValue;
                    string whose = "nobody";

                    foreach (var corridor in map.Corridors)
                    {
                        var driven = TheVeil.Sim.Crossings.Square(map.Grid, corridor.Tiles);

                        foreach (int tile in driven)
                        {
                            map.Grid.ToCoords(tile, out int dx, out int dy);
                            int away = Mathf.Abs(dx - rx) + Mathf.Abs(dy - ry);

                            if (away >= off) continue;
                            off = away;
                            whose = corridor.Kind.ToString();
                        }
                    }

                    said.AppendLine($"[Both] {chapter}-{level}: map at {px},{py}, "
                                    + $"run at {rx},{ry}, {gap} tile(s) apart; nearest "
                                    + $"driven road is {whose} at {off} tile(s)"
                                    + (gap > 1 ? "  <-- DIFFERENT CROSSINGS" : "")
                                    + (off > 1 ? "  <-- NOBODY DRIVES OVER IT" : ""));
                }
            }

            said.AppendLine($"[Both] {apart} of 30 disagree, {missing} build one bridge only");
            Write("bridges.txt", said);
        }

        /// <summary>
        /// One level decorated the way the planning map does it, or the way the run does.
        ///
        /// The arguments are the two callers' own, copied rather than paraphrased. Where
        /// they differ is the whole point of the comparison, so a shared helper that
        /// smoothed the difference away would answer nothing.
        /// </summary>
        static GameObject Decorated(LevelMap map, BiomeDecor decor, LevelRunner runner,
                                    int chapter, int level, bool plan)
        {
            var root = new GameObject(plan ? "Plan" : "Run");
            var town = LevelMaps.Recipe(chapter, level).Town
                ? Towns.Layout(map.Grid.Width, map.Grid.Height, map.Seed, map.StartY)
                : Towns.None;

            if (plan)
                TerrainDecorator.Decorate(root.transform, map.Grid, map.Seed, decor,
                    keepClear: null, heightScale: runner.HeightScale,
                    maxProps: runner.MaxProps,
                    ruinSites: TrapSigns.Sites(map), horizon: false,
                    campSites: CampSignal.Tiles(map), travelled: LevelPreview.Travelled(map),
                    village: Settlements.Site(map, chapter, level),
                    settled: Settlements.Settled(biome: Biomes.Of(chapter)),
                    town: town,
                    guard: Champions.Post(map),
                    goalTile: level >= Campaign.LevelsPerChapter ? map.GoalIndex : -1);
            else
                TerrainDecorator.Decorate(root.transform, map.Grid, map.Seed, decor,
                    keepClear: null, heightScale: runner.HeightScale,
                    maxProps: runner.MaxProps,
                    apronOpenings: new[] { map.StartIndex, map.GoalIndex },
                    ruinSites: TrapSigns.Sites(map),
                    driveLine: LevelPreview.Travelled(map),
                    campSites: CampSignal.Tiles(map), driveMargin: 0,
                    travelled: LevelPreview.Travelled(map),
                    goalTile: level >= Campaign.LevelsPerChapter ? map.GoalIndex : -1,
                    landmarkScale: runner.LandmarkScale,
                    densityScale: 1f,
                    village: Settlements.Site(map, chapter, level),
                    settled: Settlements.Settled(Biomes.Of(chapter)),
                    town: town,
                    guard: Champions.Post(map));

            return root;
        }

        /// <summary>The tile the built bridge stands on, or -1 where none was built.</summary>
        static int Where(GameObject root, LevelMap map)
        {
            var bridge = Deepest(root.transform, "Bridge");
            int tile = -1;

            if (bridge != null)
            {
                var at = ModelScaling.Measure(bridge.gameObject).center;
                int x = (int)(at.x / TileGrid.TileSize), y = (int)(at.z / TileGrid.TileSize);
                if (map.Grid.InBounds(x, y)) tile = map.Grid.ToIndex(x, y);
            }

            Object.DestroyImmediate(root);
            return tile;
        }

        /// <summary>
        /// Drives every level of the chapters that exist and writes down what happened.
        ///
        /// <b>Thirty levels, three roads each, played rather than counted.</b> Every fault
        /// the last few days have cost came back as a sentence from somebody watching the
        /// screen - the caravan starts in the water, the bridge stands on the grass, the
        /// horses walk through the deck. None of them could be seen in a number, and every
        /// number said the levels were fine.
        ///
        /// A picture at the start, one at every crossing, one wherever the run ends, and
        /// one of the bridge. The sheet beside them says what each road did, so there is
        /// something to read before deciding which pictures are worth opening.
        /// </summary>
        [MenuItem("The Veil/Play Every Level")]
        public static void Everything()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Playtest] PlayLevel has no LevelRunner."); return; }

            var said = new System.Text.StringBuilder();
            said.AppendLine("[Playtest] every level of the chapters that exist, all three roads");

            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                    Play(runner, chapter, level, said);

            Write("playtest.txt", said);
        }

        /// <summary>One level: where its bridge stands, and a run down each of its roads.</summary>
        static void Play(LevelRunner runner, int chapter, int level, System.Text.StringBuilder said)
        {
            var root = SmokeTest.Build(runner, chapter, level, out var map);

            // Where the bridge came to rest, which is the fault a playtest just reported:
            // a bridge standing on grass with the river somewhere else.
            var bridge = Deepest(root.transform, "Bridge");

            if (bridge == null)
            {
                said.AppendLine($"[{chapter}-{level}] no bridge built");
            }
            else
            {
                // <b>The model's own middle, not its transform.</b> Measured from the
                // transform this read nought metres from water on all thirty levels while
                // a playtest was looking at a bridge standing on grass - because the
                // anchor is put on the ford and the deck is wherever the prefab's pivot
                // leaves it. A bridge is where its planking is.
                var box = ModelScaling.Measure(bridge.gameObject);
                var at = box.center;
                var anchor = bridge.position;

                int bx = (int)(at.x / TileGrid.TileSize), by = (int)(at.z / TileGrid.TileSize);

                string under = map.Grid.InBounds(bx, by)
                    ? map.Grid[map.Grid.ToIndex(bx, by)].ToString() : "off the map";

                float wet = ToWater(map, at.x, at.z);

                float drift = Mathf.Sqrt((at.x - anchor.x) * (at.x - anchor.x)
                                         + (at.z - anchor.z) * (at.z - anchor.z));

                // <b>And which way it lies, which is the question a centre cannot
                // answer.</b> A bridge turned a quarter of a circle still has its middle
                // on the ford and runs along the river instead of across it, standing
                // with both ends on the same bank. From above that is a bridge on the
                // grass, which is what a playtest just reported and what measuring the
                // deck's position said was fine.
                //
                // Counted as how much of the deck's own footprint is over water. A
                // crossing lies across its stream, so most of it should be.
                int over = 0, tiles = 0;

                for (float t = -0.5f; t <= 0.5f; t += 0.05f)
                {
                    float sx = at.x + box.size.x * t * (box.size.x > box.size.z ? 1f : 0f);
                    float sz = at.z + box.size.z * t * (box.size.z >= box.size.x ? 1f : 0f);

                    int tx = (int)(sx / TileGrid.TileSize), tz = (int)(sz / TileGrid.TileSize);
                    if (!map.Grid.InBounds(tx, tz)) continue;

                    var ground = map.Grid[map.Grid.ToIndex(tx, tz)];
                    tiles++;
                    if (ground == TerrainType.Ford || ground == TerrainType.Water) over++;
                }

                float across = tiles == 0 ? 0f : (float)over / tiles;

                // <b>And whether anybody crosses there.</b> BridgeTile draws at random
                // from the crossings that have banks and never looks at where the roads
                // go, so a level's one bridge can stand on the ford none of them use.
                var uses = new System.Text.StringBuilder();

                foreach (var road in map.Corridors)
                {
                    bool near = false;

                    foreach (int tile in road.Tiles)
                    {
                        map.Grid.ToCoords(tile, out int rx, out int ry);
                        float dx = (rx + 0.5f) * TileGrid.TileSize - at.x;
                        float dy = (ry + 0.5f) * TileGrid.TileSize - at.z;

                        if (dx * dx + dy * dy <= BridgeReach * BridgeReach) { near = true; break; }
                    }

                    if (!near) continue;

                    if (uses.Length > 0) uses.Append('+');
                    uses.Append(road.Kind);
                }

                said.AppendLine($"[{chapter}-{level}] bridge deck on {under}, {wet:0.0} m from "
                                + $"water, {box.size.x:0}x{box.size.z:0} m, used by "
                                + (uses.Length == 0 ? "NOBODY" : uses.ToString())
                                + (under == "Ford" || under == "Water" ? "" : "  <-- DRY LAND"));

                Camera(at + new Vector3(0f, 55f, -45f), at,
                       System.IO.Path.Combine(Shots, $"{chapter}-{level}-bridge.png"));
            }

            foreach (var road in new[] { CorridorKind.Fast, CorridorKind.Safe, CorridorKind.Odd })
            {
                var corridor = map.CorridorOf(road);
                if (corridor == null) continue;

                var recipe = LevelMaps.Recipe(chapter, level);
                var squad = ReferenceSquad.For(recipe, ReferenceSquad.LevelsCleared(chapter, level),
                                               ReferenceSquad.Smithy(chapter));

                var run = new LevelRun(map, corridor.Tiles, squad, recipe.EnemyStrength);

                var markers = new GameObject($"Column {road}");
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

                Shoot(visuals, run, System.IO.Path.Combine(Shots,
                    $"{chapter}-{level}-{road}-start.png"));

                bool wasWet = false;
                int crossing = 0;
                float seconds = 0f, stalled = 0f, worst = 0f, was = 0f, held = 0f;
                float bled = 0f;
                string state = "", said_state = "";

                while (run.Outcome == RunOutcome.InProgress && seconds < 400f)
                {
                    run.Step();
                    seconds += Step;

                    if (run.Caravan.DistanceTravelled - was < 0.01f)
                    {
                        // <b>Measured from where the standing still began.</b> The first
                        // version of this compared health against the step before and
                        // read the state on the step the stall ended - so it answered
                        // "did anything change in the last twentieth of a second", which
                        // is not the question, and described the moment the column got
                        // going again rather than the hours it did not.
                        if (stalled <= 0f)
                        {
                            held = Health(run);
                            state = run.Combat == null ? "no combat"
                                : $"arrived {run.Caravan.HasArrived}, "
                                  + $"holding {run.HoldingTheGoal}, "
                                  + $"guards {run.Combat.GuardsStillStanding}, "
                                  + $"halted {run.Combat.Halted}, "
                                  + $"speed {run.Caravan.CurrentSpeed:0.0}";
                        }

                        stalled += Step;

                        if (stalled > worst)
                        {
                            worst = stalled;
                            bled = Health(run) - held;
                            said_state = state;
                        }
                    }
                    else { stalled = 0f; was = run.Caravan.DistanceTravelled; }

                    bool wet = Under(map, run.Caravan.WagonPosition(0)) == "Ford";

                    if (wet && !wasWet)
                    {
                        visuals.Sync(run);
                        Shoot(visuals, run, System.IO.Path.Combine(Shots,
                            $"{chapter}-{level}-{road}-ford{crossing}.png"));
                        crossing++;
                    }

                    wasWet = wet;
                }

                visuals.Sync(run);
                Shoot(visuals, run, System.IO.Path.Combine(Shots,
                    $"{chapter}-{level}-{road}-end.png"));

                said.AppendLine($"[{chapter}-{level}] {road}: {run.Outcome} after {seconds:0} s "
                                + $"(par {run.ParSeconds:0}), {run.Caravan.DistanceTravelled:0} m, "
                                + $"{crossing} crossing(s), {Alive(run)} escort, {Wagons(run)} wagons"
                                + (worst > 20f ? $"  <-- STOOD STILL {worst:0} s" : "")
                                + (worst > 20f ? $", {-bled:0} health lost in it [{said_state}]" : "")
                                + (seconds >= 400f ? "  <-- NEVER ENDED" : ""));

                Object.DestroyImmediate(markers);
            }

            Object.DestroyImmediate(root);
        }

        /// <summary>Every hit point on the field, on both sides.</summary>
        static float Health(LevelRun run)
        {
            float total = 0f;

            foreach (var group in run.Squad.Slots) if (group != null) total += group.Hp;
            foreach (var wagon in run.Caravan.Wagons) total += wagon.Hp;

            if (run.Combat != null && run.Detection != null)
                foreach (var enemy in run.Detection.Enemies) total += run.Combat.HealthOf(enemy);

            return total;
        }

        /// <summary>How far a world point is from the nearest wet tile, in metres.</summary>
        static float ToWater(LevelMap map, float x, float z)
        {
            float nearest = float.MaxValue;

            for (int i = 0; i < map.Grid.TileCount; i++)
            {
                if (map.Grid[i] != TerrainType.Water && map.Grid[i] != TerrainType.Ford) continue;

                map.Grid.ToCoords(i, out int wx, out int wy);
                float dx = (wx + 0.5f) * TileGrid.TileSize - x;
                float dy = (wy + 0.5f) * TileGrid.TileSize - z;
                float apart = Mathf.Sqrt(dx * dx + dy * dy);

                if (apart < nearest) nearest = apart;
            }

            return nearest;
        }

        /// <summary>A photograph from a fixed point, for things that do not move.</summary>
        static void Camera(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Playtest camera");
            var camera = go.AddComponent<UnityEngine.Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);

            Capture(camera, path);
            Object.DestroyImmediate(go);
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

        /// <summary>
        /// How near a road has to pass the bridge to be counted as using it, in metres.
        ///
        /// Ten, which is about a bridge's own half-length: a road that comes that close to
        /// the deck is a road that goes over it, and one that does not is crossing its
        /// river somewhere else.
        /// </summary>
        const float BridgeReach = 10f;

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

        /// <summary>
        /// Where every level's bridge ended up, and what is under it.
        ///
        /// <b>Reported from a playtest: the bridge is standing on grass.</b> One crossing
        /// per level gets a bridge and the rest get stone (TerrainDecorator.PlaceFords),
        /// and the tile it goes on is chosen by BridgeTile - crossings with banks first,
        /// and, where a level has none, any ford at all, with a note in its own source
        /// saying that a level reaching that line has already failed the generator's
        /// check. Nothing has ever looked at where the model actually came to rest.
        /// </summary>
        [MenuItem("The Veil/Bridge Placement")]
        public static void Bridges()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Bridge] PlayLevel has no LevelRunner."); return; }

            var said = new System.Text.StringBuilder();
            said.AppendLine("[Bridge] where each level's bridge stands");

            int dry = 0;

            for (int chapter = 1; chapter <= 3; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);
                    var bridge = Deepest(root.transform, "Bridge");

                    if (bridge == null)
                    {
                        int chosen = TerrainDecorator.BridgeTile(map.Grid, map.Seed);
                        said.AppendLine($"[Bridge] {chapter}-{level}: nothing built "
                                        + (chosen < 0 ? "(no ford to build on)" : $"(tile {chosen})"));
                        Object.DestroyImmediate(root);
                        continue;
                    }

                    var at = bridge.position;
                    int x = (int)(at.x / TileGrid.TileSize);
                    int y = (int)(at.z / TileGrid.TileSize);

                    string under = map.Grid.InBounds(x, y)
                        ? map.Grid[map.Grid.ToIndex(x, y)].ToString() : "off the map";

                    // How far the model's own middle is from the nearest wet tile. A
                    // bridge is a thing over water; anything else is a shed.
                    float nearest = float.MaxValue;

                    for (int i = 0; i < map.Grid.TileCount; i++)
                    {
                        if (map.Grid[i] != TerrainType.Water && map.Grid[i] != TerrainType.Ford)
                            continue;

                        map.Grid.ToCoords(i, out int wx, out int wy);
                        float dx = (wx + 0.5f) * TileGrid.TileSize - at.x;
                        float dy = (wy + 0.5f) * TileGrid.TileSize - at.z;
                        float apart = Mathf.Sqrt(dx * dx + dy * dy);

                        if (apart < nearest) nearest = apart;
                    }

                    bool wrong = under != "Ford" && under != "Water";
                    if (wrong) dry++;

                    said.AppendLine($"[Bridge] {chapter}-{level}: on {under}"
                                    + (wrong ? " — ON DRY LAND" : "")
                                    + $", {nearest:0.0} m from water, "
                                    + $"at {at.x:0}, {at.z:0}");

                    Object.DestroyImmediate(root);
                }
            }

            said.AppendLine($"[Bridge] {dry} of 30 stand on dry ground");
            Debug.Log(said.ToString());
        }

        /// <summary>The named transform, searched depth first.</summary>
        static Transform Deepest(Transform root, string name)
        {
            if (root.name.Contains(name)) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = Deepest(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
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
            var camera = go.AddComponent<UnityEngine.Camera>();

            camera.transform.position = at - behind * 40f + Vector3.up * 47f;
            camera.transform.LookAt(at);
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);

            Capture(camera, path);
            Object.DestroyImmediate(go);
        }

        /// <summary>Renders one camera to a PNG.</summary>
        static void Capture(UnityEngine.Camera camera, string path)
        {
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
            rt.Release();
            Object.DestroyImmediate(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
