using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// What stands at the end of every road, and whether it can be beaten:
    /// `The Veil > Champion Report`.
    ///
    /// Written because the champion is the first thing in this game a player cannot drive
    /// round, and a fight that cannot be avoided has to be a fight that can be won. The
    /// suite can tell me that chapter two has a level with no survivable road; only this
    /// can tell me whether that is the champion's health, his damage, the retinue, or the
    /// escort simply running out of clock while it whittles him down — and those four
    /// want four different answers.
    ///
    /// Every appearance fault this project has paid for was found by looking at one thing
    /// at a time. This is that habit applied to a number instead of a model.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.ChampionReport.Run
    /// </summary>
    public static class ChampionReport
    {
        /// <summary>The chapters with content, which are the ones worth measuring.</summary>
        const int Chapters = 3;

        [MenuItem("The Veil/Champion Report")]
        public static void Run()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Champion] chapter-level  guard         away  retinue  road/goal  points");

            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var recipe = LevelMaps.Recipe(chapter, level);
                    var map = LevelMaps.For(chapter, level);

                    map.Grid.ToCoords(map.GoalIndex, out int gx, out int gy);

                    var kind = EnemyKind.Wolf;
                    int posted = 0, away = -1;

                    foreach (var spawn in map.Encounters.Enemies)
                    {
                        if (spawn.Origin != PlacementOrigin.Goal) continue;

                        posted++;
                        map.Grid.ToCoords(spawn.Tile, out int x, out int y);
                        int gap = Mathf.RoundToInt(Mathf.Sqrt((x - gx) * (x - gx) + (y - gy) * (y - gy)));

                        // The guard himself is the heavy one; the rest are his retinue.
                        if (away < 0 || EnemyTable.Points(spawn.Kind) > EnemyTable.Points(kind))
                        {
                            kind = spawn.Kind;
                            away = gap;
                        }
                    }

                    sheet.AppendLine($"[Champion] {chapter}-{level,-2}           {kind,-12}  {away,4}  "
                                     + $"{posted - 1,7}  {recipe.EnemyBudget,4}/{recipe.GoalBudget,-4}  "
                                     + $"{map.Encounters.TotalPoints,6}");
                }
            }

            Debug.Log(sheet.ToString());
            PhotographTheChampions();
            SweepTheChapters();
            PlayTheChampions();
        }

        /// <summary>
        /// What each of the pack's mounts is actually made of.
        ///
        /// The re-dressing finds the seated rider by name, and that name was read off the
        /// heavy cavalry once and hard-coded. Putting a champion on a different horse
        /// means knowing what the rider is called there, and guessing wrong does not fail
        /// — it binds a man to a horse's neck. So it is looked up.
        /// </summary>
        [MenuItem("The Veil/Report Cavalry Riders")]
        public static void Mounts()
        {
            string[] mounts =
            {
                "MC_Cavalry", "MC_Cavalry_HeavyCavalry", "MC_Cavalry_LightCavalry",
                "MC_Cavalry_NobleCavalry", "MC_Cavalry_Scout"
            };

            var sheet = new StringBuilder();

            foreach (string name in mounts)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Stylized_Medieval_Army_Pack/Prefabs - Characters/{name}.prefab");

                if (prefab == null) { sheet.AppendLine($"[Mounts] {name}: not found"); continue; }

                var instance = Object.Instantiate(prefab);
                sheet.AppendLine($"[Mounts] {name}:");

                foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (child == instance.transform) continue;
                    if (child.parent != instance.transform) continue;

                    int skins = child.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                    sheet.AppendLine($"[Mounts]     {child.name}  ({skins} skinned meshes)");
                }

                Object.DestroyImmediate(instance);
            }

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// Each chapter's champion built and photographed on his own, from the side.
        ///
        /// <b>From the side, and one at a time, because that is the only way this project
        /// has ever found a fault in how something looks.</b> Three complete houses stacked
        /// on one another read as a house from above and were obvious the moment one was
        /// drawn side-on; a street of paving read as a street from above and was a row of
        /// slabs lying on the grass. A re-dressed rider has two failure modes that a plan
        /// view hides completely — a man standing beside his horse instead of sitting on
        /// it, and a man half inside it — and both are one glance away from the side.
        ///
        /// Ten pictures side by side is also the only way to answer the question the
        /// champions exist for: is the champion of chapter four visibly not the champion
        /// of chapter three?
        /// </summary>
        static void PhotographTheChampions()
        {
            var library = TheVeilSetup.LoadModels();

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            var sheet = new StringBuilder();
            sheet.AppendLine("[Faces] the champion of each chapter, measured and photographed");

            for (int chapter = 1; chapter <= 10; chapter++)
            {
                var face = library.ChampionFor(chapter);
                if (!face.HasModel)
                {
                    sheet.AppendLine($"[Faces] chapter {chapter}: nothing to draw");
                    continue;
                }

                var figure = Object.Instantiate(face.Prefab);
                var box = ModelScaling.Measure(figure);

                // In the house he actually rides for. Without this the sheet shows ten
                // men in whatever the pack shipped, which is not what any of them look
                // like in the game — the livery is applied at spawn.
                Wear(figure, library.ChampionLivery(chapter));

                // Side on, at the height of the saddle, far enough back to hold a horse.
                float span = Mathf.Max(box.size.x, box.size.z);
                var eye = box.center + new Vector3(span * 2.2f, box.size.y * 0.15f, 0f);

                Shoot(eye, box.center,
                      System.IO.Path.Combine(shots, $"champion-{chapter:00}.png"));

                // And the man alone, close. The horse is most of the silhouette, so at
                // full length a fault in the rider is a dozen pixels — which is how a
                // second head went unnoticed in the first sheet of these.
                var head = new Vector3(box.center.x, box.max.y - box.size.y * 0.22f, box.center.z);

                Shoot(head + new Vector3(box.size.y * 0.75f, 0f, 0f), head,
                      System.IO.Path.Combine(shots, $"champion-{chapter:00}-rider.png"));

                sheet.AppendLine($"[Faces] chapter {chapter}: {face.Prefab.name}, "
                                 + $"{box.size.x:0.00} x {box.size.y:0.00} x {box.size.z:0.00} m, "
                                 + $"foot at y {box.min.y:0.00}");

                Object.DestroyImmediate(figure);
            }

            // And one country in every house it will ever ride for, which is the other
            // half of the answer: ten countries is ten champions, and ten champions in a
            // campaign of a hundred chapters is the same man nine more times. The pass is
            // what stops that.
            for (int pass = 0; pass < 5; pass++)
            {
                var face = library.ChampionFor(1);
                if (!face.HasModel) break;

                var figure = Object.Instantiate(face.Prefab);
                Wear(figure, library.ChampionLivery(1 + pass * 10));

                var box = ModelScaling.Measure(figure);
                float span = Mathf.Max(box.size.x, box.size.z);

                Shoot(box.center + new Vector3(span * 2.2f, box.size.y * 0.15f, 0f), box.center,
                      System.IO.Path.Combine(shots, $"champion-forest-pass-{pass + 1}.png"));

                Object.DestroyImmediate(figure);
            }

            sheet.AppendLine($"[Faces] pictures in {shots}");
            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// Puts a champion in a house's colours, the way RunVisuals.Repaint does at spawn.
        ///
        /// Only the slots already holding a faction material, which is the pack's own
        /// convention and the reason this is a swap rather than a tint: repainting every
        /// slot would hand him a red lance and a red horse's eye.
        /// </summary>
        static void Wear(GameObject figure, Material livery)
        {
            if (livery == null) return;

            foreach (var renderer in figure.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    if (!slots[i].name.StartsWith(VisualLibrary.FactionPrefix)) continue;

                    slots[i] = livery;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = slots;
            }
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Champion camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.42f, 0.5f);

            var rt = new RenderTexture(900, 900, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// Every level of every chapter with content, played down all three roads, with
        /// and without the stand at its goal.
        ///
        /// The two gates in the suite say only that some level has no way through. This
        /// says which levels lost a road and which were already down to one, and those
        /// are different problems: a level that goes from three roads to two is the guard
        /// at its goal doing its job, and a level that goes from one to none is the guard
        /// taking the last road the level had.
        /// </summary>
        static void SweepTheChapters()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Sweep] roads survivable, without the goal / with it, per escort");

            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var recipe = LevelMaps.Recipe(chapter, level);
                    var map = LevelMaps.For(chapter, level);

                    var bare = LevelMaps.Recipe(chapter, level);
                    bare.GoalBudget = 0;
                    bare.GoalBlocks = false;
                    bare.GoalRetinue = 0;

                    var before = TerrainGenerator.Generate(bare, DeterministicRandom.SeedFor(chapter, level));

                    int owed = level >= 6 && level <= 9 ? 1 : 2;

                    sheet.AppendLine($"[Sweep] {chapter}-{level,-2} owes {owed}   "
                                     + $"period {Roads(before, bare, chapter, level, false)}/"
                                     + $"{Roads(map, recipe, chapter, level, false)}   "
                                     + $"upgraded {Roads(before, bare, chapter, level, true)}/"
                                     + $"{Roads(map, recipe, chapter, level, true)}   "
                                     + $"goal {recipe.GoalBudget,3} pts");
                }
            }

            Debug.Log(sheet.ToString());
        }

        /// <summary>How many of a level's three roads a caravan gets down alive.</summary>
        static int Roads(LevelMap map, LevelRecipe recipe, int chapter, int level, bool upgraded)
        {
            int survivable = 0;

            foreach (var corridor in map.Corridors)
            {
                var squad = upgraded
                    ? ReferenceSquad.For(recipe, ReferenceSquad.LevelsCleared(chapter, level),
                                         ReferenceSquad.Smithy(chapter))
                    : Escort(recipe.SquadBudget, recipe.Posts);

                var run = new LevelRun(map, corridor.Tiles, squad, recipe.EnemyStrength);
                if (run.RunToCompletion() == RunOutcome.Arrived) survivable++;
            }

            return survivable;
        }

        /// <summary>
        /// The levels a champion holds, played on every corridor with the escort the
        /// level's own points buy.
        ///
        /// The same escort ChapterDifficultyTests fields, and deliberately so: when that
        /// gate fails this has to be measuring the same thing it is, or tuning against
        /// this would be tuning against a different game.
        /// </summary>
        static void PlayTheChampions()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Champion] the last level of each chapter, every road:");

            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                int level = Campaign.LevelsPerChapter;
                var recipe = LevelMaps.Recipe(chapter, level);
                var map = LevelMaps.For(chapter, level);

                float hp = EnemyTable.HpPerModel(EnemyKind.Champion) * recipe.EnemyStrength;
                float dps = EnemyTable.Dps(EnemyKind.Champion) * recipe.EnemyStrength;

                sheet.AppendLine($"[Champion] {chapter}-{level}: champion {hp:0} hp, {dps:0} dps "
                                 + $"(x{recipe.EnemyStrength:0.00}), retinue {recipe.GoalRetinue}, "
                                 + $"squad {recipe.SquadBudget} over {recipe.Posts} posts");

                // The same level with nothing at its goal, which is what it was before
                // there were champions.
                //
                // Without this the report cannot tell the two failures apart, and they
                // want opposite answers: a road that was already unwinnable is not made
                // winnable by making the champion cheaper, and tuning him down to fix it
                // would quietly delete the boss to paper over a level.
                var bare = LevelMaps.Recipe(chapter, level);
                bare.GoalBudget = 0;
                bare.GoalBlocks = false;
                bare.GoalRetinue = 0;

                var before = TerrainGenerator.Generate(bare, DeterministicRandom.SeedFor(chapter, level));

                // The road has to be bit for bit what it was before there were champions,
                // or none of the numbers below mean what they say: a level whose terrain
                // moved is not the same level being made harder, it is a different level.
                int differs = 0;
                for (int i = 0; i < before.Grid.TileCount && i < map.Grid.TileCount; i++)
                    if (before.Grid[i] != map.Grid[i]) differs++;

                sheet.AppendLine($"[Champion]   terrain: {differs} tiles differ, attempts "
                                 + $"{before.Attempts} vs {map.Attempts}, fastest "
                                 + $"{before.FastestRouteCost:0.0} vs {map.FastestRouteCost:0.0}, "
                                 + $"groups {before.Encounters.Enemies.Count} vs "
                                 + $"{map.Encounters.Enemies.Count}, worst route "
                                 + $"{before.Encounters.MinEncounters} vs {map.Encounters.MinEncounters}");

                Play(sheet, before, bare, "no goal", chapter, level);
                Play(sheet, map, recipe, "champion", chapter, level);
            }

            Debug.Log(sheet.ToString());
        }

        /// <summary>One level played down every corridor it offers.</summary>
        static void Play(StringBuilder sheet, LevelMap map, LevelRecipe recipe, string label,
                         int chapter, int level)
        {
            foreach (var corridor in map.Corridors)
            {
                // <b>The escort the difficulty curve assumes, not a fixed six.</b>
                //
                // This fielded Escort below — spears, bows, a crossbow and a sword at no
                // weapon level, with nothing bought — which is the line written into a
                // test for chapter two and carried forward ever since. Measured against
                // it, chapter three.s tenth level had no road that could be won and the
                // row *without* a champion on it lost too: the report was saying the
                // road was unwinnable when what it had shown is that this escort cannot
                // win it, which is a different sentence and wants a different answer.
                //
                // ReferenceSquad is where that assumption is written down, and the sweep
                // above has fielded it since it was written. This did not.
                var run = new LevelRun(map, corridor.Tiles,
                                       ReferenceSquad.For(recipe,
                                           ReferenceSquad.LevelsCleared(chapter, level),
                                           ReferenceSquad.Smithy(chapter)),
                                       recipe.EnemyStrength);

                var outcome = run.RunToCompletion();

                float wagons = 0f;
                foreach (var wagon in run.Caravan.Wagons) wagons += wagon.Hp;

                // What is left of the man, so a loss reads as "nearly" or "not remotely"
                // rather than just as a loss.
                float left = 0f;
                string state = "";
                foreach (var enemy in run.Detection.Enemies)
                {
                    if (enemy.Kind != EnemyKind.Champion) continue;

                    left = run.Combat.HealthOf(enemy);

                    float dx = enemy.Position.X - run.Caravan.LeadPosition.X;
                    float dy = enemy.Position.Y - run.Caravan.LeadPosition.Y;

                    state = $" [{(enemy.Awake ? "awake" : "asleep")}"
                            + $"{(enemy.Revealed ? "" : ", unseen")}"
                            + $", {Mathf.Sqrt(dx * dx + dy * dy):0} m off"
                            + $", territory {enemy.Territory:0}]";
                }

                // Whether anybody was left to fight him, which is the difference between
                // a boss that is too strong and a road that spent the escort before the
                // boss was reached.
                int standing = 0;
                if (run.Squad != null)
                {
                    foreach (var group in run.Squad.Slots)
                        if (group != null && group.ModelsAlive > 0) standing++;
                }

                sheet.AppendLine($"[Champion]   {label,-9} {corridor.Kind,-5} {outcome,-12} "
                                 + $"{run.ElapsedSeconds,6:0}s travel {run.TravelSeconds,5:0}s "
                                 + $"par {run.ParSeconds,5:0}s  wagons {wagons,6:0}  "
                                 + $"champion left {left,6:0}{state} troops {standing}"
                                 + (run.HoldingTheGoal ? "  (held at the goal)" : ""));
            }
        }

        /// <summary>
        /// The escort ChapterDifficultyTests fields. Kept identical on purpose.
        ///
        /// Used only by the sweep, which shows the upgraded line against this one to say
        /// how much of a chapter.s difficulty is the climb the player is expected to make.
        /// The played report fielded it too and should not have: this is the escort a
        /// player would have had chapters ago, and judging a chapter.s last level by it
        /// measures the wrong player and then blames the level.
        /// </summary>
        static Squad Escort(int budget, int posts)
        {
            var squad = new Squad(budget, posts);
            squad.TryPlace(FormationSlot.Van, TroopKind.Spearmen);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
            squad.TryPlace(FormationSlot.LeftVan, TroopKind.Crossbowmen);
            squad.TryPlace(FormationSlot.RightRear, TroopKind.Swordsmen);
            squad.TryPlace(FormationSlot.LeftRear, TroopKind.Spearmen);
            return squad;
        }
    }
}
