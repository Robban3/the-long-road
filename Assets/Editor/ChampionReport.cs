using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
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
            SweepTheChapters();
            PlayTheChampions();
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

                Play(sheet, before, bare, "no goal");
                Play(sheet, map, recipe, "champion");
            }

            Debug.Log(sheet.ToString());
        }

        /// <summary>One level played down every corridor it offers.</summary>
        static void Play(StringBuilder sheet, LevelMap map, LevelRecipe recipe, string label)
        {
            foreach (var corridor in map.Corridors)
            {
                var run = new LevelRun(map, corridor.Tiles,
                                       Escort(recipe.SquadBudget, recipe.Posts),
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

        /// <summary>The escort ChapterDifficultyTests fields. Kept identical on purpose.</summary>
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
