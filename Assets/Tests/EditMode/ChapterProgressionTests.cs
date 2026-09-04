using TheVail.Gen;
using TheVail.Sim;
using NUnit.Framework;

namespace TheVail.Tests
{
    public class ChapterProgressionTests
    {
        static ChapterRecipe Chapter() => new ChapterRecipe();

        /// <summary>Enemy count times enemy quality — the two levers that carry difficulty.</summary>
        static float Difficulty(LevelRecipe r) => r.EnemyBudget * r.EnemyStrength;

        [Test]
        public void EveryLevelIsHarderThanTheOneBefore()
        {
            var chapter = Chapter();
            float previous = 0f;

            for (int level = 1; level <= chapter.LevelsPerChapter; level++)
            {
                float difficulty = Difficulty(chapter.ForLevel(level));
                Assert.Greater(difficulty, previous,
                    $"level {level} is no harder than level {level - 1}");
                previous = difficulty;
            }
        }

        [Test]
        public void TheLastLevelIsSubstantiallyHarderThanTheFirst()
        {
            var chapter = Chapter();
            float first = Difficulty(chapter.ForLevel(1));
            float last = Difficulty(chapter.ForLevel(chapter.LevelsPerChapter));

            Assert.Greater(last / first, 1.5f,
                $"the chapter only ramps from {first:F0} to {last:F0} — barely a curve");
        }

        [Test]
        public void EnemyBudgetStaysInsideTheWorkableBand()
        {
            // Below 100 the silver floor flattens every route's reward; above ~140 the
            // fast corridor saturates and the long route becomes the richer one.
            var chapter = Chapter();

            for (int level = 1; level <= chapter.LevelsPerChapter; level++)
            {
                int budget = chapter.ForLevel(level).EnemyBudget;
                Assert.GreaterOrEqual(budget, 95, $"level {level}: budget {budget} is below the workable band");
                Assert.LessOrEqual(budget, 145, $"level {level}: budget {budget} is above the saturation ceiling");
            }
        }

        [Test]
        public void EnemyTypesAreIntroducedOneAtATime()
        {
            var chapter = Chapter();

            var first = chapter.PoolForLevel(1);
            Assert.AreEqual(1, first.Length, "the opening level should teach one enemy, not three");
            Assert.AreEqual(EnemyKind.Wolf, first[0]);

            Assert.Contains(EnemyKind.Bandit, chapter.PoolForLevel(2));
            Assert.IsFalse(System.Array.IndexOf(chapter.PoolForLevel(3), EnemyKind.BanditArcher) >= 0,
                "the archer arrives too early to be a distinct lesson");
            Assert.Contains(EnemyKind.BanditArcher, chapter.PoolForLevel(4));

            // The pool never shrinks.
            int previous = 0;
            for (int level = 1; level <= chapter.LevelsPerChapter; level++)
            {
                int size = chapter.PoolForLevel(level).Length;
                Assert.GreaterOrEqual(size, previous, $"level {level} lost an enemy type");
                previous = size;
            }
        }

        [Test]
        public void ThePlayerGrowsMoreSlowlyThanTheThreat()
        {
            // Attrition already compounds across a chapter, so the squad budget must
            // not keep pace or the last levels become impossible with a worn army.
            var chapter = Chapter();
            var first = chapter.ForLevel(1);
            var last = chapter.ForLevel(chapter.LevelsPerChapter);

            float threatGrowth = Difficulty(last) / Difficulty(first);
            float playerGrowth = (float)last.SquadBudget / first.SquadBudget;

            Assert.Greater(threatGrowth, playerGrowth,
                $"threat grows {threatGrowth:F2}x but the player grows {playerGrowth:F2}x");
        }

        [Test]
        public void LevelsOutsideTheChapterAreClamped()
        {
            var chapter = Chapter();
            Assert.AreEqual(Difficulty(chapter.ForLevel(1)), Difficulty(chapter.ForLevel(0)));
            Assert.AreEqual(Difficulty(chapter.ForLevel(10)), Difficulty(chapter.ForLevel(99)));
        }

        [Test]
        public void GeneratedLevelsActuallyGetMoreDangerous()
        {
            // The recipe curve is only worth anything if it survives generation.
            var chapter = Chapter();

            int earlyThreat = 0, lateThreat = 0;
            for (int level = 1; level <= 3; level++)
                earlyThreat += TerrainGenerator
                    .Generate(chapter.ForLevel(level), DeterministicRandom.SeedFor(1, level))
                    .Encounters.TotalPoints;

            for (int level = 8; level <= 10; level++)
                lateThreat += TerrainGenerator
                    .Generate(chapter.ForLevel(level), DeterministicRandom.SeedFor(1, level))
                    .Encounters.TotalPoints;

            Assert.Greater(lateThreat, earlyThreat,
                $"levels 8-10 carry {lateThreat} threat points against levels 1-3's {earlyThreat}");
        }

        [Test]
        public void EarlyLevelsFieldOnlyWolves()
        {
            var chapter = Chapter();
            var map = TerrainGenerator.Generate(chapter.ForLevel(1), DeterministicRandom.SeedFor(1, 1));

            foreach (var spawn in map.Encounters.Enemies)
                Assert.AreEqual(EnemyKind.Wolf, spawn.Kind,
                    "level 1-1 fielded an enemy the player has not met yet");
        }
    }
}
