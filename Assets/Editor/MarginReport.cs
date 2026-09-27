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
    /// One player cannot answer whether that is the curve or the model, so three are run.
    /// They differ only in what they bought, which is the only thing a player controls:
    ///
    /// - <b>Prepared</b> is ReferenceSquad as it stands, and is what the catalogue is
    ///   calibrated against: every level cleared once, average gold, half of it on troops,
    ///   the smithy a level per chapter.
    /// - <b>Sparing</b> spent a quarter on troops and let the smithy fall two levels
    ///   behind - a player who bought boons instead, which the shop sells and the model
    ///   assumes away.
    /// - <b>Thorough</b> spent three quarters and keeps the smithy at its cap: the ceiling
    ///   of what buying can do, short of replaying levels for gold.
    ///
    /// If the margin grows for all three, the shape of the threat is what is wrong rather
    /// than the numbers in it - and no amount of moving the multiplier will fix it.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.MarginReport.Run
    /// </summary>
    public static class MarginReport
    {
        /// <summary>How long a run may take before it is called a failure to end.</summary>
        const float Longest = 400f;

        /// <summary>One kind of player, by what they have bought.</summary>
        readonly struct Player
        {
            public Player(string name, float share, int smithyBehind, bool smithyCapped)
            {
                Name = name;
                Share = share;
                SmithyBehind = smithyBehind;
                SmithyCapped = smithyCapped;
            }

            public string Name { get; }

            /// <summary>What share of their gold went on troops rather than boons.</summary>
            public float Share { get; }

            /// <summary>How many levels behind the reference their smithy is.</summary>
            public int SmithyBehind { get; }

            /// <summary>Whether they keep it at the cap instead.</summary>
            public bool SmithyCapped { get; }

            public int Smithy(int chapter)
            {
                if (SmithyCapped) return RunEconomy.MaxTrackLevel;

                int level = ReferenceSquad.Smithy(chapter) - SmithyBehind;
                return level < 0 ? 0 : level;
            }
        }

        static readonly Player[] Players =
        {
            new Player("prepared", ReferenceSquad.SpentOnTroops, 0, false),
            new Player("sparing", 0.25f, 2, false),
            new Player("thorough", 0.75f, 0, true)
        };

        /// <summary>What one run came to.</summary>
        struct Outcome
        {
            public bool Arrived;
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

                            var squad = ReferenceSquad.For(
                                recipe, ReferenceSquad.LevelsCleared(chapter, level),
                                player.Smithy(chapter), player.Share);

                            var run = new LevelRun(map, corridor.Tiles, squad, recipe.EnemyStrength);

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

                            float started = Line(run);
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
                                Escort = started <= 0f ? 1f : Line(run) / started,
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
                said.AppendLine("[Margin]  ch  runs  lost   escort left   load left   pace");

                for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                {
                    if (!tally.TryGetValue($"{player.Name} {chapter}", out var runs)) continue;

                    int lost = 0;
                    float escort = 0f, load = 0f, pace = 0f;

                    foreach (var run in runs)
                    {
                        if (!run.Arrived) lost++;
                        escort += run.Escort;
                        load += run.Wagons;
                        pace += run.Seconds / Mathf.Max(run.Par, 1f);
                    }

                    said.AppendLine($"[Margin] {chapter,3} {runs.Count,5} {lost,5} "
                                    + $"{escort / runs.Count,13:0.00} {load / runs.Count,11:0.00} "
                                    + $"{pace / runs.Count,6:0.00}");
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

        /// <summary>Every hit point standing in the line.</summary>
        static float Line(LevelRun run)
        {
            float total = 0f;
            foreach (var group in run.Squad.Slots) if (group != null) total += group.Hp;
            return total;
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
