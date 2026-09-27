using System.Collections.Generic;
using System.Text;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// What is left of the caravan when it arrives, chapter by chapter, for three kinds of
    /// player: `The Veil > Margin Report`.
    ///
    /// <b>Because the curve measures the wrong thing.</b> A level's difficulty is a judged
    /// number, and the catalogue is built to make that number climb. What the player feels
    /// is not the number: it is how much of the column is standing at the goal. Those two
    /// have come apart. Measured over every built level and all three roads, the reference
    /// player loses six runs in chapter one and none afterwards, and arrives with more of
    /// the escort in each chapter than in the one before - 2.4 of the line in chapter one
    /// and the whole of it in chapter six.
    ///
    /// Three players are run, differing in the one thing a player chooses between levels:
    /// how much of the gold goes on permanent troop levels rather than on boons. Half is
    /// what the curve assumes, a quarter is a player who bought boons instead, and all of
    /// it is the ceiling. Every one of them starts each level with no smithy and buys at
    /// the field forge as the silver comes in, because that is how a run works.
    ///
    /// The judged column is the curve's own measure - the share of the escort lost on the
    /// roads that are not the fast one, a road not got down counting as the whole of it -
    /// so it can be read straight against the target beside it.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.MarginReport.Run
    /// </summary>
    public static class MarginReport
    {
        /// <summary>How long a run may take before it is called a failure to end.</summary>
        const float Longest = 400f;

        /// <summary>
        /// One kind of player, by the only thing a player chooses between levels: how much
        /// of the gold went on permanent troop levels rather than on boons.
        ///
        /// <b>And nothing else, because nothing else is real.</b> This file used to sort
        /// its players by how far their smithy had been taken, out of
        /// ReferenceSquad.Smithy - and a smithy is bought with a run's own silver during
        /// the run and is gone at the end of it. Those players walked the game from
        /// chapter two with five free upgrade levels nobody can have, and this report said
        /// so in a table: no losses after chapter one, the escort arriving whole. The
        /// curve was never wrong; the instrument was.
        /// </summary>
        readonly struct Player
        {
            public Player(string name, float share)
            {
                Name = name;
                Share = share;
            }

            public string Name { get; }

            /// <summary>What share of their gold went on troops rather than boons.</summary>
            public float Share { get; }
        }

        static readonly Player[] Players =
        {
            new Player("as the curve judges", ReferenceSquad.SpentOnTroops),
            new Player("sparing", 0.25f),
            new Player("all on troops", ReferenceSquad.AllOnTroops)
        };

        /// <summary>What one run came to.</summary>
        struct Outcome
        {
            public bool Arrived;
            public bool Fast;
            public float Escort;
            public float Wagons;
            public float Seconds;
            public float Par;
        }

        [MenuItem("The Veil/Margin Report")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Margin] PlayLevel has no LevelRunner."); return; }

            // Filed by player and chapter.
            var tally = new Dictionary<string, List<Outcome>>();

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);
                    var recipe = LevelMaps.Recipe(chapter, level);

                    foreach (var player in Players)
                    {
                        foreach (var kind in new[] { CorridorKind.Fast, CorridorKind.Safe, CorridorKind.Odd })
                        {
                            var corridor = map.CorridorOf(kind);
                            if (corridor == null) continue;

                            // The player: no smithy at the start and the field forge open,
                            // which is how a run works (FieldSmith). Driven any other way
                            // this measures somebody who cannot exist.
                            var run = ReferenceSquad.Play(
                                map, corridor.Tiles, recipe,
                                ReferenceSquad.LevelsCleared(chapter, level), player.Share);

                            // The rock and the walls, so the escort fights the level it is
                            // standing in rather than an empty field. Nothing is spawned:
                            // this reads the Solid marks off the props already built.
                            var markers = new GameObject("Column");
                            markers.transform.SetParent(root.transform, false);

                            var visuals = new RunVisuals(markers.transform, map.Grid, runner.HeightScale)
                            {
                                Library = runner.Models,
                                Chapter = chapter
                            };

                            visuals.FindBridges(root.transform);
                            visuals.FindObstacles(root.transform, run);

                            float loaded = Load(run);
                            float seconds = 0f;

                            while (run.Outcome == RunOutcome.InProgress && seconds < Longest)
                            {
                                run.Step();
                                seconds += LevelRun.StepSeconds;
                            }

                            var note = new Outcome
                            {
                                Arrived = run.Outcome == RunOutcome.Arrived,
                                Fast = kind == CorridorKind.Fast,
                                // <b>The curve's own arithmetic, not a second opinion.</b>
                                // Measured against the health the line set out with, the
                                // field forge's armour raises the denominator's numerator
                                // and a whole chapter came back with more escort than it
                                // began - a negative difficulty. LevelMaps.EscortLeft
                                // measures against the health it could have had, which is
                                // what Judge compares to the target.
                                Escort = LevelMaps.EscortLeft(run),
                                Wagons = loaded <= 0f ? 1f : Load(run) / loaded,
                                Seconds = seconds,
                                Par = run.ParSeconds
                            };

                            string key = $"{player.Name} {chapter}";
                            if (!tally.TryGetValue(key, out var runs)) tally[key] = runs = new List<Outcome>();
                            runs.Add(note);

                            Object.DestroyImmediate(markers);
                        }
                    }

                    Object.DestroyImmediate(root);
                }

                Debug.Log($"[Margin] chapter {chapter} measured");
            }

            var said = new StringBuilder();
            said.AppendLine("[Margin] what arrives, per chapter, for three kinds of player");
            said.AppendLine("[Margin] escort and load are shares of what set out; "
                            + "pace is seconds over par");
            said.AppendLine();

            foreach (var player in Players)
            {
                said.AppendLine($"[Margin] == {player.Name} ==");
                said.AppendLine("[Margin]  ch  runs  lost   escort left   load left   pace"
                                + "   judged   target");

                for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                {
                    if (!tally.TryGetValue($"{player.Name} {chapter}", out var runs)) continue;

                    int lost = 0;
                    float escort = 0f, load = 0f, pace = 0f;

                    // <b>And the same thing in the curve's own language.</b> LevelMaps.Judge
                    // calls a level's difficulty the share of the escort lost on the roads
                    // that are not the fast one, a road not got down counting as the whole
                    // of it. Measured any other way the two cannot be compared, and the
                    // question here is exactly whether the game delivers what the curve
                    // promised.
                    float steady = 0f;
                    int counted = 0;

                    foreach (var run in runs)
                    {
                        if (!run.Arrived) lost++;
                        escort += run.Escort;
                        load += run.Wagons;
                        pace += run.Seconds / Mathf.Max(run.Par, 1f);

                        if (run.Fast) continue;

                        steady += run.Arrived ? run.Escort : 0f;
                        counted++;
                    }

                    float judged = counted > 0 ? 1f - steady / counted : 1f;

                    float target = 0f;
                    for (int l = 1; l <= Campaign.LevelsPerChapter; l++)
                        target += DifficultyCurve.Target(chapter, l);
                    target /= Campaign.LevelsPerChapter;

                    said.AppendLine($"[Margin] {chapter,3} {runs.Count,5} {lost,5} "
                                    + $"{escort / runs.Count,13:0.00} {load / runs.Count,11:0.00} "
                                    + $"{pace / runs.Count,6:0.00} {judged,8:0.00} {target,8:0.00}");
                }

                said.AppendLine();
            }

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilPlaytest",
                                                 "margin.txt");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, said.ToString());

            Debug.Log(said.ToString());
            Debug.Log("[Margin] " + path);
        }

        /// <summary>And every hit point left in the wagons.</summary>
        static float Load(LevelRun run)
        {
            float total = 0f;
            foreach (var wagon in run.Caravan.Wagons) total += wagon.Hp;
            return total;
        }
    }
}
