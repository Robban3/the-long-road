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
        /// What is actually standing at the traps, by name.
        ///
        /// Asked because it is the kind of thing that is easy to believe and hard to know:
        /// new models were wired into the set, the set is drawn from at random, and a
        /// picture of some bones is not proof that they are the new bones.
        /// </summary>
        [MenuItem("The Veil/What Stands At The Traps")]
        public static void AtTheTraps()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[AtTraps] no LevelRunner."); return; }

            var said = new System.Text.StringBuilder();
            said.AppendLine("[AtTraps] every prop within three metres of a trap, by name");

            var tally = new Dictionary<string, int>();

            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);

                    foreach (var trap in map.Encounters.Traps)
                    {
                        var at = Vec2.FromTile(map.Grid, trap.Tile);

                        foreach (var thing in root.GetComponentsInChildren<Transform>(true))
                        {
                            float dx = thing.position.x - at.X, dz = thing.position.z - at.Y;
                            if (dx * dx + dz * dz > 9f) continue;

                            string name = thing.name.Replace("(Clone)", "");
                            if (name.Length == 0 || name == "Props" || name == "Ground") continue;

                            tally.TryGetValue(name, out int seen);
                            tally[name] = seen + 1;
                        }
                    }

                    Object.DestroyImmediate(root);
                }

            var names = new List<string>(tally.Keys);
            names.Sort((a, b) => tally[b].CompareTo(tally[a]));

            foreach (string name in names) said.AppendLine($"[AtTraps] {tally[name],4}  {name}");

            Write("attraps.txt", said);
        }

        /// <summary>What the arid pack's bones import as: size, materials, and a picture.</summary>
        [MenuItem("The Veil/Arid Bones Report")]
        public static void AridBones()
        {
            var said = new System.Text.StringBuilder();
            said.AppendLine("[Arid] the bone models as Unity imported them");

            foreach (string name in new[] { "AD2_BonePile_01", "AD2_BonePile_02", "AD2_Ribcage_01",
                                            "AD2_AnimalSkeleton_01", "AD2_HornedSkull_01" })
            {
                string path = $"Assets/ThirdParty/AridDesertBiomeV2/Models_FBX/{name}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (model == null) { said.AppendLine($"[Arid] {name}: NOT IMPORTED"); continue; }

                var instance = Object.Instantiate(model);
                var box = ModelScaling.Measure(instance);

                var mats = new System.Text.StringBuilder();
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mats.Length > 0) mats.Append(", ");
                        mats.Append(mat == null ? "none" : mat.name);
                    }

                said.AppendLine($"[Arid] {name}: {box.size.x:0.00} x {box.size.y:0.00} x "
                                + $"{box.size.z:0.00} m, materials [{mats}]");

                Object.DestroyImmediate(instance);
            }

            Write("arid.txt", said);
        }

        /// <summary>
        /// The crows on the planning map, where the road is chosen, photographed the way
        /// the player sees them.
        ///
        /// The planning map flies real flocks at CrowScale times life size, and its own
        /// note says the number was guessed and nobody had looked at a render. A signal
        /// that cannot be seen from where the decision is made is not a signal.
        /// </summary>
        [MenuItem("The Veil/Crows On The Plan")]
        public static void CrowsOnThePlan()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/LevelPreview.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var preview = Object.FindAnyObjectByType<LevelPreview>();
            if (preview == null) { Debug.LogError("[Crows] no LevelPreview."); return; }

            preview.Chapter = 1;
            preview.Level = 10;
            preview.Rebuild();

            var map = LevelMaps.For(1, 10);
            var said = new System.Text.StringBuilder();

            // Flown for a few seconds first. The flocks circle when the plan is up, and a
            // still taken the instant they are built shows every bird on the one spot it
            // was spawned on, which is not what anybody looking at the map sees.
            var tick = typeof(LevelPreview).GetMethod("TickCrows",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < 60 && tick != null; i++) tick.Invoke(preview, new object[] { 0.1f });

            var flocks = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Crows_")) flocks.Add(t);

            said.AppendLine($"[Crows] 1-10 plan: {flocks.Count} flock(s), CrowScale {preview.CrowScale}");

            foreach (var flock in flocks)
            {
                var box = ModelScaling.Measure(flock.gameObject);
                said.AppendLine($"[Crows] {flock.name} at {flock.position.x:0},{flock.position.z:0}: "
                                + $"{box.size.x:0.0} x {box.size.z:0.0} m from above");
            }

            Overhead(map, System.IO.Path.Combine(Shots, "crows-plan-1-10.png"));
            Write("crows.txt", said);
        }

        /// <summary>
        /// Whether the country tells the truth about which road is dangerous.
        ///
        /// There is no risk readout any more - the landscape is how a road is judged - so
        /// the signals have to carry it. Per road, what a player can see along it before
        /// setting out (crows within a flock's hint of the road, bones beside it) against
        /// what is actually on it (the enemy points a caravan down it runs into). The
        /// question is whether the road that looks worst is the road that is worst.
        /// </summary>
        [MenuItem("The Veil/Signals Along The Roads")]
        public static void SignalsAlongTheRoads()
        {
            var said = new System.Text.StringBuilder();
            said.AppendLine("[Signs] per road: crows and bones a player can see, against what is there");

            int levels = 0, crowsRight = 0, bonesRight = 0, bothRight = 0;

            for (int chapter = 1; chapter <= 3; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);
                    var flocks = CrowSignal.Place(map);
                    var heaps = TrapSigns.Sites(map) ?? new List<int>();

                    var line = new System.Text.StringBuilder();
                    CorridorKind worst = CorridorKind.Fast, loudest = CorridorKind.Fast, boniest = CorridorKind.Fast;
                    int worstPoints = -1, mostCrows = -1, mostBones = -1, signs = -1;
                    CorridorKind signed = CorridorKind.Fast;

                    foreach (var road in map.Corridors)
                    {
                        int points = 0;
                        foreach (int g in EncounterPlacer.MetGroups(map.Grid, road.Tiles, map.Encounters, roadOnly: true))
                            points += EnemyTable.Points(map.Encounters.Enemies[g].Kind);

                        int crows = 0;
                        foreach (var flock in flocks)
                            if (Within(map, road.Tiles, flock.Tile, CrowSignal.HintTiles)) crows++;

                        int bones = 0;
                        foreach (int heap in heaps)
                            if (Within(map, road.Tiles, heap, 2)) bones++;

                        line.Append($"  {road.Kind} {points}p crows {crows} bones {bones}");

                        if (points > worstPoints) { worstPoints = points; worst = road.Kind; }
                        if (crows > mostCrows) { mostCrows = crows; loudest = road.Kind; }
                        if (bones > mostBones) { mostBones = bones; boniest = road.Kind; }
                        if (crows + bones > signs) { signs = crows + bones; signed = road.Kind; }
                    }

                    levels++;
                    if (loudest == worst) crowsRight++;
                    if (boniest == worst) bonesRight++;
                    if (signed == worst) bothRight++;

                    said.AppendLine($"[Signs] {chapter}-{level}: worst is {worst}, most crows {loudest}, "
                                    + $"most bones {boniest}, most of both {signed}"
                                    + (signed == worst ? "" : "  <--") + " |" + line);
                }
            }

            said.AppendLine($"[Signs] {levels} levels: crows point at the worst road on {crowsRight}, "
                            + $"bones on {bonesRight}, the two together on {bothRight}");
            Write("signals.txt", said);
        }

        /// <summary>Whether a tile is within so many tiles of any tile of a road.</summary>
        static bool Within(LevelMap map, List<int> road, int tile, float tiles)
        {
            map.Grid.ToCoords(tile, out int x, out int y);

            foreach (int step in road)
            {
                map.Grid.ToCoords(step, out int rx, out int ry);
                float dx = rx - x, dy = ry - y;
                if (dx * dx + dy * dy <= tiles * tiles) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether every trap has bones beside it, in the game and on the planning map,
        /// before anything has gone off.
        ///
        /// <b>Reported, and not for the first time: no skeletons at the traps until they
        /// spring, and none on the map.</b> Measured in the world the run stands up and in
        /// the landmarks the planning map draws symbols from, trap by trap - because the
        /// signs were being placed one per neighbourhood, twelve metres off, then pushed off
        /// the road, and "some traps have bones somewhere" was never the promise. Every one
        /// was.
        ///
        /// Within a tile and a half, which is the eight tiles touching the trap and the
        /// width of the heap spread round its centre.
        /// </summary>
        /// <summary>
        /// What water each level has, and whether it gets a bridge: `The Veil > Water And
        /// Bridges`.
        ///
        /// Written to answer one question flatly - how can the town level have a bridge -
        /// because the smoke test said it had one and the town clears its whole grid to
        /// building ground. One of the two was wrong and neither could be believed while
        /// the only thing either reported was a verdict.
        /// </summary>
        /// <summary>
        /// How far apart the fights stand on each road: `The Veil > Spacing Along The
        /// Roads`.
        ///
        /// <b>Because a road can be too hard without having too much on it.</b> Played
        /// through, the fast road arrives on fifteen levels of thirty and the other two
        /// on twenty-nine and twenty-seven. The shares are what we agreed - half the
        /// level's threat on the fast road, a third on the middle, a fifth on the long
        /// one - and half a level's threat is not meant to be a death sentence.
        ///
        /// The suspicion this measures: the fast road is also the shortest, so the same
        /// share of fights is packed into fewer metres and they run into each other. A
        /// squad that meets three groups with a hundred metres between them heals and
        /// re-forms between each; the same three in thirty metres is one fight against
        /// all of them. Nothing in the placer knows the difference - it counts points, and
        /// points do not say how far apart they stand.
        ///
        /// So: where along each road every fight is met, the gaps between them in metres,
        /// and the smallest gap on each. Measured before anything is changed.
        /// </summary>
        [MenuItem("The Veil/Spacing Along The Roads")]
        public static void SpacingAlongTheRoads()
        {
            var said = new System.Text.StringBuilder();
            said.AppendLine("[Spacing] every road of every level: fights, length, and the gaps between them");

            var gapsBy = new Dictionary<CorridorKind, List<float>>();
            var fightsBy = new Dictionary<CorridorKind, List<int>>();
            var lengthBy = new Dictionary<CorridorKind, List<float>>();

            foreach (var kind in new[] { CorridorKind.Fast, CorridorKind.Safe, CorridorKind.Odd })
            {
                gapsBy[kind] = new List<float>();
                fightsBy[kind] = new List<int>();
                lengthBy[kind] = new List<float>();
            }

            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);

                    foreach (var kind in new[] { CorridorKind.Fast, CorridorKind.Safe, CorridorKind.Odd })
                    {
                        var corridor = map.CorridorOf(kind);
                        if (corridor == null) continue;

                        var route = corridor.Tiles;

                        // How far along the road each tile of it is, in metres. Diagonal
                        // steps are longer than straight ones and a road is mostly
                        // diagonal, so counting tiles would understate every gap.
                        var along = new float[route.Count];
                        for (int i = 1; i < route.Count; i++)
                        {
                            map.Grid.ToCoords(route[i - 1], out int px, out int py);
                            map.Grid.ToCoords(route[i], out int cx, out int cy);

                            float step = px != cx && py != cy ? 1.41421f : 1f;
                            along[i] = along[i - 1] + step * TileGrid.TileSize;
                        }

                        float length = route.Count > 0 ? along[route.Count - 1] : 0f;

                        // Where on the road each fight is met: the nearest point of the
                        // road to the group that meets it.
                        var met = EncounterPlacer.MetGroups(map.Grid, route, map.Encounters,
                                                            roadOnly: true);
                        var where = new List<float>();

                        foreach (int index in met)
                        {
                            map.Grid.ToCoords(map.Encounters.Enemies[index].Tile, out int ex, out int ey);

                            float best = float.MaxValue, at = 0f;
                            for (int i = 0; i < route.Count; i++)
                            {
                                map.Grid.ToCoords(route[i], out int rx, out int ry);
                                float dx = rx - ex, dy = ry - ey;
                                float apart = dx * dx + dy * dy;

                                if (apart >= best) continue;
                                best = apart;
                                at = along[i];
                            }

                            where.Add(at);
                        }

                        where.Sort();

                        var gaps = new List<float>();
                        for (int i = 1; i < where.Count; i++) gaps.Add(where[i] - where[i - 1]);

                        gapsBy[kind].AddRange(gaps);
                        fightsBy[kind].Add(where.Count);
                        lengthBy[kind].Add(length);

                        said.AppendLine($"[Spacing] {chapter}-{level} {kind}: {where.Count} fight(s) "
                                        + $"over {length:0} m, gaps "
                                        + (gaps.Count == 0 ? "-" : $"least {Least(gaps):0} m, "
                                           + $"middling {Middle(gaps):0} m"));
                    }
                }

            said.AppendLine();
            foreach (var kind in new[] { CorridorKind.Fast, CorridorKind.Safe, CorridorKind.Odd })
            {
                var gaps = gapsBy[kind];
                int tight = 0;
                foreach (float gap in gaps) if (gap < 40f) tight++;

                said.AppendLine($"[Spacing] {kind}: {Sum(fightsBy[kind]):0} fights over "
                                + $"{Middle(lengthBy[kind]):0} m of road (middling), gap "
                                + $"least {Least(gaps):0} m, middling {Middle(gaps):0} m, "
                                + $"{tight} of {gaps.Count} gaps under 40 m");
            }

            Write("spacing.txt", said);
        }

        static float Least(List<float> numbers)
        {
            float least = float.MaxValue;
            foreach (float number in numbers) least = Mathf.Min(least, number);
            return numbers.Count == 0 ? 0f : least;
        }

        static float Middle(List<float> numbers)
        {
            if (numbers.Count == 0) return 0f;
            var sorted = new List<float>(numbers);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }

        static float Sum(List<int> numbers)
        {
            float total = 0f;
            foreach (int number in numbers) total += number;
            return total;
        }

        [MenuItem("The Veil/Water And Bridges")]
        public static void WaterAndBridges()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Water] PlayLevel has no LevelRunner."); return; }

            var said = new System.Text.StringBuilder();
            said.AppendLine("[Water] what each level has to cross, and what stands over it");

            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);

                    int water = 0, fords = 0;
                    for (int i = 0; i < map.Grid.TileCount; i++)
                    {
                        if (map.Grid[i] == TerrainType.Water) water++;
                        else if (map.Grid[i] == TerrainType.Ford) fords++;
                    }

                    int bridge = TerrainDecorator.BridgeTile(map.Grid, map.Seed,
                                                             LevelPreview.Travelled(map));

                    said.AppendLine($"[Water] {chapter}-{level}: {water} water tile(s), "
                                    + $"{fords} ford tile(s), {TheVeil.Sim.Crossings.Count(map.Grid)} "
                                    + $"crossing(s) with banks, owed "
                                    + $"{LevelMaps.Recipe(chapter, level).CrossingsOwed}, bridge "
                                    + (bridge < 0 ? "none" : $"at tile {bridge}"));
                }

            Write("water.txt", said);
        }

        [MenuItem("The Veil/Bones At The Traps")]
        public static void BonesAtTheTraps()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Bones] PlayLevel has no LevelRunner."); return; }

            var said = new System.Text.StringBuilder();
            said.AppendLine("[Bones] every trap, and the nearest bones in the game and on the map");

            int traps = 0, bare = 0, unmarked = 0;
            float near = TileGrid.TileSize * 1.5f;

            for (int chapter = 1; chapter <= 3; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);

                    var bones = new List<Vector3>();
                    var wagons = new List<Vector3>();

                    foreach (var thing in root.GetComponentsInChildren<Transform>(true))
                    {
                        string name = thing.name;

                        // Counted, because looking for it in a photograph is how a prop
                        // that was never placed gets called a prop that is hard to see.
                        if (name.Contains("BrokenWagon")) wagons.Add(thing.position);

                        if (name.Contains("Skull") || name.Contains("Skeleton")
                            || name.Contains("Bone") || name.Contains("Grave"))
                            bones.Add(thing.position);
                    }

                    // The planning map's own receipt: the landmarks it draws symbols from.
                    var marks = new List<Landmark>();
                    var plan = new GameObject("Plan");
                    TerrainDecorator.Decorate(plan.transform, map.Grid, map.Seed,
                        runner.LookFor(Biomes.Of(chapter)) is var look && look != null && look.Dressed
                            ? look.Decor : runner.Decor,
                        heightScale: runner.HeightScale, maxProps: runner.MaxProps,
                        ruinSites: TrapSigns.Sites(map), horizon: false,
                        keepClear: Corridors(map),
                        campSites: CampSignal.Tiles(map), travelled: LevelPreview.Travelled(map),
                        found: marks,

                        // Everything LevelPreview passes that can take ground first. Left out,
                        // 1-8 read as fully marked on the map while its town - which claims
                        // the whole grid - had quietly kept every heap off it in both.
                        village: Settlements.Site(map, chapter, level),
                        settled: Settlements.Settled(Biomes.Of(chapter)),
                        town: LevelMaps.Recipe(chapter, level).Town
                            ? Towns.Layout(map.Grid.Width, map.Grid.Height, map.Seed, map.StartY)
                            : Towns.None,
                        guard: Champions.Post(map),
                        goalTile: level >= Campaign.LevelsPerChapter ? map.GoalIndex : -1);
                    Object.DestroyImmediate(plan);

                    int here = 0, bareHere = 0, unmarkedHere = 0;

                    foreach (var trap in map.Encounters.Traps)
                    {
                        here++;
                        var at = Vec2.FromTile(map.Grid, trap.Tile);

                        float nearest = float.MaxValue;
                        foreach (var bone in bones)
                        {
                            float dx = bone.x - at.X, dz = bone.z - at.Y;
                            float apart = Mathf.Sqrt(dx * dx + dz * dz);
                            if (apart < nearest) nearest = apart;
                        }

                        if (nearest > near) bareHere++;

                        map.Grid.ToCoords(trap.Tile, out int tx, out int ty);
                        bool symbol = false;

                        foreach (var mark in marks)
                        {
                            if (mark.Kind != LandmarkKind.Bones) continue;
                            map.Grid.ToCoords(mark.Tile, out int mx, out int my);
                            if (Mathf.Abs(mx - tx) <= 1 && Mathf.Abs(my - ty) <= 1) { symbol = true; break; }
                        }

                        if (!symbol) unmarkedHere++;
                    }

                    traps += here;
                    bare += bareHere;
                    unmarked += unmarkedHere;

                    // A picture of the first trap from where the player sits, so the heap
                    // is seen as a player sees it and not only counted.
                    if (map.Encounters.Traps.Count > 0 && (level == 1 || level == 10))
                    {
                        var first = Vec2.FromTile(map.Grid, map.Encounters.Traps[0].Tile);
                        var at = new Vector3(first.X,
                            map.Grid.SurfaceElevation(first.X, first.Y) * runner.HeightScale, first.Y);

                        Camera(at + new Vector3(0f, 47f, -40f), at,
                               System.IO.Path.Combine(Shots, $"trap-{chapter}-{level}.png"));
                        Camera(at + new Vector3(0f, 14f, -12f), at,
                               System.IO.Path.Combine(Shots, $"trap-{chapter}-{level}-close.png"));
                    }

                    // And the bridge's true size, measured in its own frame.
                    var span = Deepest(root.transform, "Bridge");
                    if (span != null)
                    {
                        DeckSize(span.gameObject, out float wide, out float lengthOf);
                        said.AppendLine($"[Deck] {chapter}-{level}: {wide:0.0} m wide, "
                                        + $"{lengthOf:0.0} m long"
                                        + (wide < TerrainDecorator.FordDeck - 0.5f ? "  <-- TOO NARROW" : ""));
                    }

                    // The wreck stands off from the *bones*, and the bones stand off
                    // from the trap - TrapSigns puts them on a neighbouring tile - so a
                    // wreck is allowed both distances. Measured against the trap's own
                    // tile at six metres it read as missing on half the levels while the
                    // photographs showed it lying right there.
                    float wreckReach = near + TerrainDecorator.WreckStandoff;

                    // How near a crossing a heap has to be for its missing wreck to be
                    // the sweep's doing. A deck is 22 m long and the sweep works on
                    // outlines, so a heap half a deck away can still have had a wreck
                    // overlapping it.
                    const float DeckReach = 16f;

                    int wagonless = 0;
                    foreach (var trap in map.Encounters.Traps)
                    {
                        var at = Vec2.FromTile(map.Grid, trap.Tile);
                        bool close = false;

                        foreach (var wagon in wagons)
                        {
                            float dx = wagon.x - at.X, dz = wagon.z - at.Y;
                            if (dx * dx + dz * dz <= wreckReach * wreckReach) { close = true; break; }
                        }

                        if (!close) wagonless++;
                    }

                    // Against the heaps rather than against the traps, because two traps
                    // side by side are given the same sign tile and share one heap - and
                    // one wreck is what belongs beside one heap. A count of wrecks against
                    // a count of traps says a level is short when it is not.
                    var heaps = new HashSet<int>(TrapSigns.Sites(map));

                    // <b>A heap may lose its wreck to a crossing, and only to that.</b>
                    // Traps are laid at the throats, a ford is a throat, and
                    // SweepTheBridges takes anything standing on a deck that is not bones -
                    // which is right, a wagon parked on the bridge is worse than no wagon.
                    // So a wreckless heap beside a crossing is the rule working; one out in
                    // the open is a fault, and only that is marked.
                    int stranded = 0;

                    if (wagons.Count < heaps.Count)
                    {
                        var goalAt = Vec2.FromTile(map.Grid, map.GoalIndex);

                        foreach (int heap in heaps)
                        {
                            var at = Vec2.FromTile(map.Grid, heap);

                            float nearest = float.MaxValue;
                            foreach (var wagon in wagons)
                            {
                                float dx = wagon.x - at.X, dz = wagon.z - at.Y;
                                nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dz * dz));
                            }

                            if (nearest <= TerrainDecorator.WreckStandoff + 1f) continue;

                            float fromGoal = Mathf.Sqrt((at.X - goalAt.X) * (at.X - goalAt.X)
                                                        + (at.Y - goalAt.Y) * (at.Y - goalAt.Y));

                            // And from the nearest crossing, which is the other thing that
                            // sweeps ground clear: traps are laid at the throats, a ford is
                            // a throat, and SweepTheBridges takes anything standing on a
                            // deck that is not bones.
                            float fromDeck = float.MaxValue;
                            foreach (var deck in root.GetComponentsInChildren<BridgeDeck>(true))
                            {
                                float dx = deck.transform.position.x - at.X;
                                float dz = deck.transform.position.z - at.Y;
                                fromDeck = Mathf.Min(fromDeck, Mathf.Sqrt(dx * dx + dz * dz));
                            }

                            bool atCrossing = fromDeck <= DeckReach;
                            if (!atCrossing) stranded++;

                            said.AppendLine($"[Wagon] {chapter}-{level}: heap at tile {heap} "
                                            + $"has no wreck ({fromGoal:0} m from the goal, "
                                            + (fromDeck < float.MaxValue
                                                ? $"{fromDeck:0} m from a crossing)"
                                                : "no crossing on the level)")
                                            + (atCrossing ? " - swept off the deck" : "  <--"));
                        }
                    }

                    said.AppendLine($"[Wagon] {chapter}-{level}: {wagons.Count} wreck(s) for "
                                    + $"{heaps.Count} heap(s) at {here} trap(s), {wagonless} "
                                    + $"trap(s) with none within {wreckReach:0} m"
                                    + (stranded > 0 ? "  <--" : ""));

                    said.AppendLine($"[Bones] {chapter}-{level}: {here} trap(s), "
                                    + $"{bareHere} with no bones within {near:0} m in the game, "
                                    + $"{unmarkedHere} with no bones symbol beside it on the map"
                                    + (bareHere + unmarkedHere > 0 ? "  <--" : ""));

                    Object.DestroyImmediate(root);
                }
            }

            said.AppendLine($"[Bones] {traps} traps: {bare} bare in the game, {unmarked} unmarked on the map");
            Write("bones.txt", said);
        }

        /// <summary>
        /// Every tile of the three roads: what the planning map keeps clear of scenery so
        /// its ribbons can be seen. LevelPreview.CorridorTiles, without the instance.
        /// </summary>
        static HashSet<int> Corridors(LevelMap map)
        {
            var tiles = new HashSet<int>();
            foreach (var corridor in map.Corridors) tiles.UnionWith(corridor.Tiles);
            return tiles;
        }

        /// <summary>
        /// The planning map and the game, photographed from straight above in the same
        /// frame, with where each one's bridge stands.
        ///
        /// <b>Built by their own code, not by a copy of it.</b> The first comparison
        /// decorated both worlds with arguments written out here, and said they agreed on
        /// every level - while a playtest drew a route over the bridge on the map and
        /// found it somewhere else in the game. A tool that restates what the planning map
        /// does can only ever confirm the restatement. This opens LevelPreview.unity and
        /// asks it to Rebuild, which is what the player sees, and puts the run's world
        /// beside it.
        ///
        /// Orthographic and square over the whole grid, so the two pictures overlay
        /// exactly: a bridge in a different place is a different place in the frame.
        /// </summary>
        [MenuItem("The Veil/Map Against Game")]
        public static void MapAgainstGame() => MapAgainstGame(1, 10);

        public static void MapAgainstGame(int chapter, int level)
        {
            var said = new System.Text.StringBuilder();
            said.AppendLine($"[Map] {chapter}-{level}: the planning map against the game");

            // The planning map, by its own hand.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/LevelPreview.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var preview = Object.FindAnyObjectByType<LevelPreview>();
            if (preview == null) { Debug.LogError("[Map] LevelPreview.unity has no LevelPreview."); return; }

            preview.Chapter = chapter;
            preview.Level = level;
            preview.Rebuild();

            var map = LevelMaps.For(chapter, level);

            said.AppendLine("[Map] on the planning map: " + Bridges(map));
            Overhead(map, System.IO.Path.Combine(Shots, $"map-{chapter}-{level}.png"));

            // And the game, the way LevelRunner stands it up - see SmokeTest.Build.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Map] PlayLevel has no LevelRunner."); return; }

            var root = SmokeTest.Build(runner, chapter, level, out map);

            said.AppendLine("[Map] in the game:         " + Bridges(map));
            Overhead(map, System.IO.Path.Combine(Shots, $"game-{chapter}-{level}.png"));

            Object.DestroyImmediate(root);
            Write($"map-{chapter}-{level}.txt", said);
        }

        /// <summary>Every bridge standing in the open scene, by tile, with its bearing.</summary>
        static string Bridges(LevelMap map)
        {
            var decks = Object.FindObjectsByType<BridgeDeck>(FindObjectsSortMode.None);
            if (decks.Length == 0) return "no bridge";

            var said = new System.Text.StringBuilder();

            foreach (var deck in decks)
            {
                var box = ModelScaling.Measure(deck.gameObject);
                int x = (int)(box.center.x / TileGrid.TileSize);
                int y = (int)(box.center.z / TileGrid.TileSize);

                if (said.Length > 0) said.Append("; ");
                said.Append($"tile {x},{y} at {box.center.x:0},{box.center.z:0} m, "
                            + $"{box.size.x:0}x{box.size.z:0} m, yaw {deck.transform.eulerAngles.y:0}");
            }

            return said.ToString();
        }

        /// <summary>Straight down on the whole grid, the same frame every time.</summary>
        static void Overhead(LevelMap map, string path)
        {
            float wide = map.Grid.Width * TileGrid.TileSize;
            float deep = map.Grid.Height * TileGrid.TileSize;
            var middle = new Vector3(wide * 0.5f, 0f, deep * 0.5f);

            var go = new GameObject("Overhead camera");
            var camera = go.AddComponent<UnityEngine.Camera>();

            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(wide, deep) * 0.5f;
            camera.aspect = 1f;
            camera.transform.position = middle + Vector3.up * 400f;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);

            const int size = 1024;

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

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
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

                DeckSize(bridge.gameObject, out float deckWide, out float deckLong);

                said.AppendLine($"[{chapter}-{level}] bridge deck on {under}, {wet:0.0} m from "
                                + $"water, {deckWide:0.0} m wide x {deckLong:0.0} m long"
                                + (deckWide < TerrainDecorator.FordDeck - 0.5f ? "  <-- TOO NARROW" : "")
                                + ", used by "
                                + (uses.Length == 0 ? "NOBODY" : uses.ToString())
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

        /// <summary>
        /// A deck's true width and length, however it is turned.
        ///
        /// The narrowest a rectangle reaches over every heading is its width, and it
        /// reaches its length a quarter turn from there. Swept every two degrees with
        /// ModelScaling.ExtentAlong, which measures in the model's own frame - the world
        /// box around a bridge at seventy-nine degrees is mostly its length in both
        /// directions, which is how a nine-metre deck was being reported as eight.
        /// </summary>
        static void DeckSize(GameObject bridge, out float width, out float length)
        {
            width = float.MaxValue;
            float at = 0f;

            for (float heading = 0f; heading < 180f; heading += 2f)
            {
                float rad = heading * Mathf.Deg2Rad;
                float reach = ModelScaling.ExtentAlong(bridge,
                    new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)));

                if (reach < width) { width = reach; at = heading; }
            }

            float turned = (at + 90f) * Mathf.Deg2Rad;
            length = ModelScaling.ExtentAlong(bridge,
                new Vector3(Mathf.Sin(turned), 0f, Mathf.Cos(turned)));
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
