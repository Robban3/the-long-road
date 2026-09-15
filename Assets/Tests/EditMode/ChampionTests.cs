using System.Collections.Generic;
using TheVeil.Gen;
using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// That a chapter ends on its champion and nothing else does, that he is the only
    /// thing in the game placed rather than dealt, that he can be beaten, and that the
    /// stand he makes is not charged to the player twice.
    ///
    /// The last two are the ones worth writing down. A fight the caravan cannot drive
    /// round is new: the two clocks it could quietly spend — the stall timer and the
    /// third star's travel time — both belong to the road rather than to the battle, and
    /// a champion nobody can get past walls the player out of the next chapter with
    /// nothing else in the suite noticing.
    /// </summary>
    public class ChampionTests
    {
        /// <summary>The chapters with content, which are the ones whose maps can be judged.</summary>
        static readonly int[] Chapters = { 1, 2, 3 };

        [Test]
        public void EveryChapterEndsOnAChampionAndNothingElseDoes()
        {
            foreach (int chapter in Chapters)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);
                    bool last = level == Campaign.LevelsPerChapter;

                    Assert.AreEqual(last, map.Encounters.GoalGuards > 0,
                        last
                            ? $"{chapter}-{level} is the end of a chapter and has nobody at its goal"
                            : $"{chapter}-{level} has a guard at its goal; only the tenth should");
                }
            }
        }

        [Test]
        public void TheGuardStandsWithinReachOfTheGoal()
        {
            foreach (int chapter in Chapters)
            {
                int level = Campaign.LevelsPerChapter;
                var map = LevelMaps.For(chapter, level);
                map.Grid.ToCoords(map.GoalIndex, out int gx, out int gy);

                int nearest = int.MaxValue;

                foreach (var spawn in map.Encounters.Enemies)
                {
                    if (spawn.Origin != PlacementOrigin.Goal) continue;

                    map.Grid.ToCoords(spawn.Tile, out int x, out int y);
                    int away = System.Math.Abs(x - gx) + System.Math.Abs(y - gy);
                    if (away < nearest) nearest = away;
                }

                // Near enough that arriving means meeting him, and no nearer than the
                // ring the band keeps clear. Manhattan here, so the window is wider
                // than the straight-line five the placer works in.
                Assert.LessOrEqual(nearest, 10,
                    $"{chapter}-{level}: the posted guard is {nearest} tiles from the goal");
                Assert.GreaterOrEqual(nearest, 4,
                    $"{chapter}-{level}: the posted guard is standing on top of the goal");
            }
        }

        [Test]
        public void TheLastLevelOfAChapterIsHeldByAChampion()
        {
            foreach (int chapter in Chapters)
            {
                var map = LevelMaps.For(chapter, Campaign.LevelsPerChapter);
                int champions = 0;

                foreach (var spawn in map.Encounters.Enemies)
                    if (spawn.Kind == EnemyKind.Champion) champions++;

                Assert.AreEqual(1, champions,
                    $"{chapter}-{Campaign.LevelsPerChapter} should be held by exactly one champion");
            }
        }

        [Test]
        public void NoOtherLevelHasAChampionOnIt()
        {
            foreach (int chapter in Chapters)
            {
                for (int level = 1; level < Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);

                    foreach (var spawn in map.Encounters.Enemies)
                        Assert.AreNotEqual(EnemyKind.Champion, spawn.Kind,
                            $"{chapter}-{level} has a champion on it and is not the end of a chapter");
                }
            }
        }

        /// <summary>
        /// The champion is placed, never dealt. If the scatter can produce one, the
        /// hardest fight in the game turns up at random on the road, which is the
        /// opposite of what it is for.
        /// </summary>
        [Test]
        public void AChampionIsNeverScattered()
        {
            foreach (int chapter in Chapters)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);

                    foreach (var spawn in map.Encounters.Enemies)
                    {
                        if (spawn.Kind != EnemyKind.Champion) continue;

                        Assert.AreEqual(PlacementOrigin.Goal, spawn.Origin,
                            $"{chapter}-{level}: a champion arrived by {spawn.Origin}");
                    }
                }
            }
        }

        /// <summary>
        /// The budget ceiling is what the whole difficulty curve rests on. The stand at
        /// the goal is bought with a second pocket rather than out of the road's — see
        /// Champions.Purse for what happened when it was a slice — and the ceiling has to
        /// hold across both.
        /// </summary>
        [Test]
        public void TheStandAtTheGoalIsPaidForOutOfItsOwnPurse()
        {
            foreach (int chapter in Chapters)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var recipe = LevelMaps.Recipe(chapter, level);
                    var map = LevelMaps.For(chapter, level);

                    int purse = recipe.EnemyBudget + recipe.GoalBudget;

                    Assert.LessOrEqual(map.Encounters.TotalPoints, purse,
                        $"{chapter}-{level} spends more than it was given");

                    // And the road's own share is untouched by it, which is the whole
                    // point of the split: 1-2 owes four groups on the worst drawn route
                    // and cannot pay for them out of a purse the goal has been at.
                    int posted = 0;
                    foreach (var spawn in map.Encounters.Enemies)
                        if (spawn.Origin == PlacementOrigin.Goal)
                            posted += EnemyTable.Points(spawn.Kind);

                    Assert.LessOrEqual(map.Encounters.TotalPoints - posted, recipe.EnemyBudget,
                        $"{chapter}-{level}: the road overspent once the goal was paid for");
                }
            }
        }

        /// <summary>
        /// <b>Every chapter can be got out of.</b>
        ///
        /// The champion is the only thing in the game that must be beaten rather than
        /// driven round, so he is also the only thing that can wall a player out of the
        /// next chapter entirely. Nothing else in the suite would notice: the chapter-two
        /// gate stops at chapter two, and chapter three had no gate at all — 3-10 went
        /// from three survivable roads to none and the suite stayed green.
        ///
        /// Played with ReferenceSquad, which is the escort the difficulty curve already
        /// assumes (ChapterRecipe sets EscortStrength from it) rather than the fixed six
        /// of chapter two. Measuring a chapter-three champion against chapter two's
        /// unupgraded line measures the wrong player and then blames the level: the
        /// troops earned by levels cleared and the smithy behind them are the whole of
        /// what the chapters are balanced around.
        /// </summary>
        [Test]
        public void EveryChampionCanBeBeatenByTheEscortTheChapterAssumes()
        {
            foreach (int chapter in Chapters)
            {
                int level = Campaign.LevelsPerChapter;
                var recipe = LevelMaps.Recipe(chapter, level);
                var map = LevelMaps.For(chapter, level);

                int beaten = 0;
                foreach (var corridor in map.Corridors)
                {
                    var run = new LevelRun(map, corridor.Tiles,
                                           ReferenceSquad.For(chapter, level),
                                           recipe.EnemyStrength);

                    if (run.RunToCompletion() == RunOutcome.Arrived) beaten++;
                }

                Assert.GreaterOrEqual(beaten, 1,
                    $"{chapter}-{level}: no road gets past the champion, so chapter "
                    + $"{chapter + 1} cannot be reached");
            }
        }

        /// <summary>
        /// A run cannot be called finished while the champion is up. Everything else in
        /// the game can be driven past; this is the one thing that cannot, once a
        /// chapter.
        /// </summary>
        [Test]
        public void TheRunDoesNotEndWhileTheChampionStands()
        {
            var map = LevelMaps.For(1, Campaign.LevelsPerChapter);
            var run = new LevelRun(map, Fastest(map));

            // Nobody along: the caravan cannot kill him, so if the run ever ends it ends
            // for the wrong reason.
            for (int i = 0; i < 20000 && run.Outcome == RunOutcome.InProgress; i++) run.Step();

            if (run.Outcome == RunOutcome.CaravanLost) Assert.Pass("the champion took the caravan");

            Assert.AreEqual(RunOutcome.InProgress, run.Outcome,
                "the caravan arrived with the champion still standing");
            Assert.IsTrue(run.HoldingTheGoal, "the run is not holding the goal");
        }

        /// <summary>
        /// Par is measured against the road, and the stand at the goal is not road. See
        /// LevelRun.TravelSeconds — the same reason fighting was taken out of it.
        /// </summary>
        [Test]
        public void HoldingTheGoalDoesNotSpendTheThirdStar()
        {
            var map = LevelMaps.For(1, Campaign.LevelsPerChapter);
            var run = new LevelRun(map, Fastest(map));

            for (int i = 0; i < 20000 && !run.HoldingTheGoal
                                      && run.Outcome == RunOutcome.InProgress; i++) run.Step();

            if (!run.HoldingTheGoal) Assert.Ignore("the caravan never reached the stand");

            float travelled = run.TravelSeconds;
            for (int i = 0; i < 200; i++) run.Step();

            Assert.AreEqual(travelled, run.TravelSeconds, 0.001f,
                "the clock kept running while the caravan was held at the goal");
        }

        static IReadOnlyList<int> Fastest(LevelMap map)
            => map.CorridorOf(CorridorKind.Fast).Tiles;
    }
}
