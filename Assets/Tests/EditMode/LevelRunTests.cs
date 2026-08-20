using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class LevelRunTests
    {
        static LevelMap Map(int chapter, int level)
            => TerrainGenerator.Generate(new ChapterRecipe().ForLevel(level),
                                         DeterministicRandom.SeedFor(chapter, level));

        static LevelRun Run(int chapter, int level, CorridorKind kind = CorridorKind.Fast)
        {
            var map = Map(chapter, level);
            return new LevelRun(map, map.CorridorOf(kind).Tiles);
        }

        [Test]
        public void ARunReachesTheGoal()
        {
            var run = Run(1, 1);
            Assert.AreEqual(RunOutcome.InProgress, run.Outcome);

            Assert.AreEqual(RunOutcome.Arrived, run.RunToCompletion());
            Assert.Greater(run.ElapsedSeconds, 0f);
        }

        [Test]
        public void EnemiesAreFoundAlongTheWay()
        {
            var run = Run(1, 5);
            run.RunToCompletion();

            Assert.Greater(run.Detection.RevealedCount, 0, "the caravan crossed the map and saw nothing");
            Assert.Greater(run.Detection.AwakeCount, 0, "nothing ever noticed the caravan");
        }

        [Test]
        public void AScoutSeesTroubleBeforeItWakesAndGetsPaidForIt()
        {
            // Same level, same route, one difference: a lookout who can actually see.
            var blindMap = Map(1, 6);
            var sharpMap = Map(1, 6);

            var blind = new LevelRun(blindMap, blindMap.CorridorOf(CorridorKind.Fast).Tiles);
            var sharp = new LevelRun(sharpMap, sharpMap.CorridorOf(CorridorKind.Fast).Tiles)
            {
                LookoutSight = 34f
            };

            blind.RunToCompletion();
            sharp.RunToCompletion();

            Assert.Greater(sharp.Detection.SpottedEarlyCount, blind.Detection.SpottedEarlyCount,
                "the scout spotted no more than the caravan's own driver");
            Assert.Greater(sharp.Economy.TotalEarned, blind.Economy.TotalEarned,
                "spotting groups early earned nothing");
        }

        [Test]
        public void TrapsGoOffAndHurt()
        {
            var run = Run(1, 8);
            float before = 0f;
            foreach (var wagon in run.Caravan.Wagons) before += wagon.Hp;

            run.RunToCompletion();

            if (run.Traps.Traps.Count == 0) Assert.Ignore("this seed placed no traps on the fast route");

            float after = 0f;
            foreach (var wagon in run.Caravan.Wagons) after += wagon.Hp;

            Assert.Less(after, before, "the caravan walked a trapped route without a scratch");
        }

        [Test]
        public void ADisarmedTrapNeverFires()
        {
            var grid = new TileGrid(30, 10);
            var route = new List<int>();
            for (int x = 0; x < 30; x++) route.Add(grid.ToIndex(x, 5));

            var placements = new List<TrapPlacement>
            {
                new TrapPlacement { Tile = grid.ToIndex(15, 5), Kind = TrapKind.Log }
            };

            var field = new TrapField(grid, placements);
            var trapPosition = Vec2.FromTile(grid, grid.ToIndex(15, 5));

            // Spotted from a distance, then defused before the column arrives.
            field.Update(new Vec2(trapPosition.X - 7f, trapPosition.Y), null, trapSight: 12f);
            Assert.AreEqual(1, field.RevealedCount, "the trap was never spotted");

            Assert.IsNotNull(field.TryDisarmNearest(new Vec2(trapPosition.X - 5f, trapPosition.Y)));
            Assert.AreEqual(1, field.DisarmedCount);

            field.Update(trapPosition, null);
            Assert.IsEmpty(field.TriggeredThisTick, "a disarmed trap went off anyway");
        }

        [Test]
        public void AnUnseenTrapCannotBeDisarmed()
        {
            // The engineer depends on the scout: you cannot defuse what nobody spotted.
            var grid = new TileGrid(30, 10);
            var placements = new List<TrapPlacement>
            {
                new TrapPlacement { Tile = grid.ToIndex(15, 5), Kind = TrapKind.Pit }
            };

            var field = new TrapField(grid, placements);
            var trapPosition = Vec2.FromTile(grid, grid.ToIndex(15, 5));

            Assert.IsNull(field.TryDisarmNearest(trapPosition), "an unspotted trap was defused");
        }

        [Test]
        public void ATrapFiresOnlyOnce()
        {
            var grid = new TileGrid(30, 10);
            var placements = new List<TrapPlacement>
            {
                new TrapPlacement { Tile = grid.ToIndex(15, 5), Kind = TrapKind.Pit }
            };

            var field = new TrapField(grid, placements);
            var trapPosition = Vec2.FromTile(grid, grid.ToIndex(15, 5));

            field.Update(trapPosition, null);
            Assert.AreEqual(1, field.TriggeredThisTick.Count);

            field.Update(trapPosition, null);
            Assert.IsEmpty(field.TriggeredThisTick, "the trap fired a second time");
        }

        [Test]
        public void StarsFollowTheCondition()
        {
            var run = Run(1, 1);
            Assert.AreEqual(0, run.Stars, "an unfinished run was rated");

            run.RunToCompletion();
            Assert.Greater(run.Stars, 0, "arriving earned nothing");

            var wrecked = Run(1, 1);
            wrecked.Caravan[WagonKind.Treasure].ApplyDamage(99999f);
            wrecked.RunToCompletion();
            Assert.AreEqual(1, wrecked.Stars, "arriving a wagon short should be worth exactly one star");
        }

        [Test]
        public void LostLootCostsGold()
        {
            var whole = Run(1, 2);
            whole.RunToCompletion();

            var robbed = Run(1, 2);
            robbed.Caravan[WagonKind.Treasure].ApplyDamage(99999f);
            robbed.RunToCompletion();

            Assert.Greater(whole.GoldEarned(), robbed.GoldEarned(),
                "losing the treasure wagon cost nothing");
        }

        [Test]
        public void TheRunIsDeterministic()
        {
            var a = Run(2, 4);
            var b = Run(2, 4);

            a.RunToCompletion();
            b.RunToCompletion();

            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.That(a.ElapsedSeconds, Is.EqualTo(b.ElapsedSeconds).Within(0.0001f));
            Assert.AreEqual(a.Detection.RevealedCount, b.Detection.RevealedCount);
            Assert.AreEqual(a.Economy.TotalEarned, b.Economy.TotalEarned);
        }

        [Test]
        public void FixedStepsAreIndependentOfFrameRate()
        {
            // The point of the fixed timestep: a phone dropping frames must not change
            // the outcome, and fast-forward is just more steps per frame.
            var steady = Run(1, 4);
            var stuttering = Run(1, 4);

            while (steady.Outcome == RunOutcome.InProgress) steady.Advance(1f / 60f);
            while (stuttering.Outcome == RunOutcome.InProgress) stuttering.Advance(1f / 12f);

            Assert.AreEqual(steady.Outcome, stuttering.Outcome);
            Assert.That(stuttering.ElapsedSeconds, Is.EqualTo(steady.ElapsedSeconds).Within(0.2f),
                "frame rate changed how long the level took");
        }

        [Test]
        public void EveryCorridorOfEveryEarlyLevelIsSurvivable()
        {
            // Traps currently strike the wagons directly because troops are not
            // simulated yet, which makes them harsher than designed. Even so, no route
            // should be lethal on its own.
            for (int level = 1; level <= 10; level++)
            {
                var map = Map(1, level);
                foreach (var corridor in map.Corridors)
                {
                    var run = new LevelRun(map, corridor.Tiles);
                    var outcome = run.RunToCompletion();

                    Assert.AreEqual(RunOutcome.Arrived, outcome,
                        $"level 1-{level}: the {corridor.Kind} route destroyed the caravan through traps alone");
                }
            }
        }
    }
}
