using TheVeil.Gen;
using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// The chapters after the first: each one starting where the last ended, the climb
    /// bending so that a thousand levels stay winnable, and chapter one left alone.
    /// </summary>
    public class ChapterDifficultyTests
    {
        /// <summary>About a thousand levels, ten to a chapter.</summary>
        const int Chapters = 100;

        static float Difficulty(LevelRecipe r) => r.EnemyBudget * r.EnemyStrength;

        [Test]
        public void ChapterOneIsExactlyWhatItWas()
        {
            // Its maps are generated from these numbers, and the planning map and the run
            // both have to keep drawing the same ones.
            var plain = new ChapterRecipe();
            var first = ChapterRecipe.For(1);

            for (int level = 1; level <= plain.LevelsPerChapter; level++)
            {
                var a = plain.ForLevel(level);
                var b = first.ForLevel(level);

                Assert.AreEqual(a.EnemyStrength, b.EnemyStrength, $"1-{level} strength");
                Assert.AreEqual(a.EnemyBudget, b.EnemyBudget, $"1-{level} enemies");
                Assert.AreEqual(a.TrapDensity, b.TrapDensity, $"1-{level} traps");
                Assert.AreEqual(a.SquadBudget, b.SquadBudget, $"1-{level} squad");
                Assert.AreEqual(a.Posts, b.Posts, $"1-{level} posts");
                Assert.AreEqual(a.SilverMultiplier, b.SilverMultiplier, $"1-{level} silver");
                Assert.AreEqual(1f, b.EscortStrength, $"1-{level} escort");
                Assert.AreEqual(a.EnemyPool.Length, b.EnemyPool.Length, $"1-{level} pool");
            }
        }

        [Test]
        public void ChapterTwoIsWhatWasAgreed()
        {
            var two = ChapterRecipe.For(2);
            var first = two.ForLevel(1);
            var last = two.ForLevel(two.LevelsPerChapter);

            Assert.AreEqual(1.35f, first.EnemyStrength, 0.001f);
            Assert.AreEqual(1.70f, last.EnemyStrength, 0.001f);
            Assert.AreEqual(1.0f, first.TrapDensity, 0.001f);
            Assert.AreEqual(1.6f, last.TrapDensity, 0.001f);
            Assert.AreEqual(16, first.SquadBudget);
            Assert.AreEqual(22, last.SquadBudget);
            Assert.AreEqual(5, first.Posts);
            Assert.AreEqual(TroopTable.LinePosts, last.Posts);

            // Everything chapter one taught is out from the first level; what chapter two
            // adds of its own — horsemen, a captain — is paced inside it (RaiderTests).
            foreach (var kind in EnemyTable.Common)
                Assert.Contains(kind, first.EnemyPool,
                    "chapter two opened with the wolves-only lesson chapter one already taught");
        }

        [Test]
        public void EachChapterPicksUpWhereTheLastLeftOff()
        {
            for (int chapter = 2; chapter <= Chapters; chapter++)
            {
                float ended = ChapterRecipe.For(chapter - 1).ForLevel(10).EnemyStrength;
                float starts = ChapterRecipe.For(chapter).ForLevel(1).EnemyStrength;

                Assert.AreEqual(ended, starts, 0.0001f,
                    $"chapter {chapter} starts at {starts:F3} where chapter {chapter - 1} ended at {ended:F3}");
            }
        }

        [Test]
        public void TheClimbSlowsButNeverStops()
        {
            float previousStep = float.MaxValue;

            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                float step = ChapterRecipe.StrengthAtEndOf(chapter) - ChapterRecipe.StrengthAtEndOf(chapter - 1);

                Assert.Greater(step, 0f, $"chapter {chapter} is no harder than the one before");
                Assert.LessOrEqual(step, previousStep + 0.0001f, $"chapter {chapter} climbs faster than the one before");
                previousStep = step;
            }

            // A player whose every purchase has a cap is still meant to be able to finish.
            Assert.Less(ChapterRecipe.StrengthAtEndOf(Chapters), 6f,
                "the last chapter's enemies are out of reach of anything the shop sells");
        }

        [Test]
        public void EveryChapterStillRampsAndThePlayerStillTrailsIt()
        {
            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                var recipe = ChapterRecipe.For(chapter);
                float previous = 0f;

                for (int level = 1; level <= recipe.LevelsPerChapter; level++)
                {
                    var at = recipe.ForLevel(level);
                    Assert.Greater(Difficulty(at), previous, $"{chapter}-{level} is no harder than the level before");
                    previous = Difficulty(at);

                    // The map has no room for more; see ChapterProgressionTests.
                    Assert.LessOrEqual(at.EnemyBudget, 145, $"{chapter}-{level} fields more than the map holds");
                    Assert.LessOrEqual(at.TrapDensity, ChapterRecipe.TrapCeiling + 0.0001f);
                }

                var first = recipe.ForLevel(1);
                var last = recipe.ForLevel(recipe.LevelsPerChapter);

                float threat = Difficulty(last) / Difficulty(first);
                float player = (float)last.SquadBudget / first.SquadBudget;

                Assert.Greater(threat, player, $"chapter {chapter}: the squad grows {player:F2}x against a threat of {threat:F2}x");
            }
        }

        [Test]
        public void TheSquadGrowsToALineOfKnightsAndStops()
        {
            int previous = 0;

            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                var recipe = ChapterRecipe.For(chapter);
                int start = recipe.ForLevel(1).SquadBudget;
                int end = recipe.ForLevel(recipe.LevelsPerChapter).SquadBudget;

                Assert.GreaterOrEqual(start, previous - 6, $"chapter {chapter} takes points back");
                Assert.LessOrEqual(end, ChapterRecipe.SquadCeiling, $"chapter {chapter} pays for more than six knights");
                previous = end;
            }

            Assert.AreEqual(ChapterRecipe.SquadCeiling, ChapterRecipe.For(Chapters).ForLevel(10).SquadBudget);
        }

        /// <summary>
        /// An escort a player might field with chapter two's points: spears to hold, bows
        /// and crossbows to shoot, a sword. No boons — by chapter two a player has bought
        /// some, so this is the harder case.
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

        [Test]
        public void ChapterTwoOffersAWayThroughEveryLevel()
        {
            // The promise chapter one keeps (LevelRunTests): every level has at least one
            // road that can be fought through. Played rather than estimated, since the
            // generator's own gate is an estimate calibrated on chapter one.
            var report = new System.Text.StringBuilder();
            int total = 0;

            for (int level = 1; level <= 10; level++)
            {
                var recipe = LevelMaps.Recipe(2, level);
                var map = LevelMaps.For(2, level);

                int survivable = 0;
                foreach (var corridor in map.Corridors)
                {
                    var run = new LevelRun(map, corridor.Tiles, Escort(recipe.SquadBudget, recipe.Posts),
                                           recipe.EnemyStrength);
                    if (run.RunToCompletion() == RunOutcome.Arrived) survivable++;
                }

                report.Append($" 2-{level}:{survivable}");
                total += survivable;

                Assert.GreaterOrEqual(survivable, 1, $"level 2-{level} has no road that can be fought through;{report}");
            }

            UnityEngine.Debug.Log($"[The Veil] Chapter 2 survivable routes:{report} (total {total})");
        }
    }
}
