using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    /// <summary>
    /// The figures inside a group, and who each of them is looking at.
    ///
    /// These are the numbers behind a complaint that was entirely visual — a wolf pack
    /// drawn as one wolf, standing side-on, with the escort's backs to it — so the
    /// assertions are about metres and headings rather than about pixels. What they
    /// pin down is that the picture is a function of the fight and not a guess made
    /// separately from it.
    /// </summary>
    public class FormationTests
    {
        const float East = 1f;

        // --- the shapes themselves --------------------------------------------------

        [Test]
        public void TheLeadAttackerStandsOnTheGroupsOwnPoint()
        {
            var apex = Formation.Wedge(0, East, 0f);

            // Not the centre of the pack but its nose, because that is the point the
            // combat step halts at its reach from the troop. Centring the wedge there
            // would leave the lead animal short of the fight it is in.
            Assert.AreEqual(0f, apex.X, 1e-4f);
            Assert.AreEqual(0f, apex.Y, 1e-4f);
        }

        [Test]
        public void APackFansOutInPairsBehindItsLeader()
        {
            var left = Formation.Wedge(1, East, 0f);
            var right = Formation.Wedge(2, East, 0f);

            // Mirrored about the heading, and both a rank further back.
            Assert.AreEqual(-left.Y, right.Y, 1e-4f, "the wings were not mirrored");
            Assert.AreEqual(left.X, right.X, 1e-4f, "the wings sat at different depths");
            Assert.Less(left.X, 0f, "the second rank stood ahead of the leader");

            Assert.AreEqual(Formation.PackSpacing, System.Math.Abs(left.Y), 1e-4f);
            Assert.AreEqual(Formation.PackDepth, System.Math.Abs(left.X), 1e-4f);
        }

        [Test]
        public void AWolfPackIsWiderThanOneFormationPostAndNarrowerThanThree()
        {
            float widest = 0f;
            for (int i = 0; i < EnemyTable.GroupSize(EnemyKind.Wolf); i++)
            {
                float lateral = System.Math.Abs(Formation.Wedge(i, East, 0f).Y);
                if (lateral > widest) widest = lateral;
            }

            float span = widest * 2f;

            // Wide enough to read as a pack closing on a post, tight enough that it is
            // fighting one post rather than draped across the whole formation.
            Assert.Greater(span, Squad.FormationRadius,
                $"a five-wolf pack spanned only {span:0.0} m and reads as one animal");
            Assert.Less(span, Squad.FormationRadius * 2f,
                $"a five-wolf pack spanned {span:0.0} m and engulfs three posts");
        }

        [Test]
        public void AWedgeTurnsWithItsHeading()
        {
            var east = Formation.Wedge(1, 1f, 0f);
            var north = Formation.Wedge(1, 0f, 1f);

            // The same figure, the same place in the pack, rotated a quarter turn: the
            // offsets are in the group's frame, not the world's.
            Assert.AreEqual(east.Y, -north.X, 1e-4f);
            Assert.AreEqual(east.X, north.Y, 1e-4f);
        }

        [Test]
        public void ALineIsCentredOnThePostItHolds()
        {
            const int count = 4;
            float sumX = 0f, sumY = 0f;

            for (int i = 0; i < count; i++)
            {
                var offset = Formation.Line(i, count, East, 0f);
                sumX += offset.X;
                sumY += offset.Y;
            }

            // Sideways it balances exactly; the stagger pushes the whole rank slightly
            // back, which is the point of the stagger.
            Assert.AreEqual(0f, sumY, 1e-4f, "the rank was not centred on its post");
            Assert.Less(sumX, 0f, "the stagger pushed the rank forward");
        }

        [Test]
        public void ARankFitsInsideItsOwnPost()
        {
            foreach (var kind in TroopTable.All)
            {
                int count = TroopTable.Models(kind);
                float widest = 0f;

                for (int i = 0; i < count; i++)
                {
                    float lateral = System.Math.Abs(Formation.Line(i, count, East, 0f).Y);
                    if (lateral > widest) widest = lateral;
                }

                // Adjacent posts sit a formation radius apart, so half of one is the
                // most a rank may claim before it starts standing inside its neighbour.
                Assert.LessOrEqual(widest, Squad.FormationRadius * 0.5f,
                    $"{kind} spread {widest * 2f:0.0} m and overlaps the next post");
            }
        }

        [Test]
        public void ADeadModelLeavesItsGapRatherThanClosingRanks()
        {
            // Positions depend on the group's full complement, never on its survivors,
            // so a figure does not shuffle sideways every time a neighbour falls.
            var beforeLoss = Formation.Line(0, 4, East, 0f);
            var afterLoss = Formation.Line(0, 4, East, 0f);

            Assert.AreEqual(beforeLoss.Y, afterLoss.Y, 1e-4f);
            Assert.AreNotEqual(Formation.Line(0, 4, East, 0f).Y,
                               Formation.Line(0, 3, East, 0f).Y,
                               "a shorter line put the first model in the same place");
        }

        [Test]
        public void AGroupWithNoHeadingStillPlacesItsFiguresApart()
        {
            var a = Formation.Wedge(1, 0f, 0f);
            var b = Formation.Wedge(2, 0f, 0f);

            Assert.Greater(Vec2.Distance(a, b), 1f,
                "figures stacked on one spot when nothing was in sight");
        }

        // --- who is looking at whom -------------------------------------------------

        static Squad Escort()
        {
            var squad = new Squad(12);
            squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
            squad.TryPlace(FormationSlot.Scouting, TroopKind.Scout);
            return squad;
        }

        static LevelRun Run()
        {
            var map = TerrainGenerator.Generate(new ChapterRecipe().ForLevel(5),
                                                DeterministicRandom.SeedFor(1, 5));
            return new LevelRun(map, map.CorridorOf(CorridorKind.Fast).Tiles, Escort());
        }

        [Test]
        public void AnAttackerRecordsTheTroopItIsComingFor()
        {
            var run = Run();
            bool everEngaged = false;

            for (int step = 0; step < 4000 && run.Outcome == RunOutcome.InProgress; step++)
            {
                run.Step();

                foreach (var enemy in run.Detection.Enemies)
                {
                    if (!enemy.Awake || run.Combat.IsDefeated(enemy))
                    {
                        Assert.IsNull(enemy.Engaging, "a beaten group was still chasing someone");
                        Assert.IsFalse(enemy.Striking, "a beaten group was still swinging");
                        continue;
                    }

                    if (enemy.Engaging == null) continue;
                    everEngaged = true;

                    Assert.IsTrue(enemy.Engaging.Alive, "an attacker was closing on a dead troop");

                    if (!enemy.Striking) continue;

                    // Striking means in reach of that very troop, which is what lets the
                    // view animate the bite without re-deriving the range itself.
                    float reach = EnemyTable.AttackRange(enemy.Kind) + CombatSystem.EngagementSlack;
                    Assert.LessOrEqual(Vec2.Distance(enemy.Position, enemy.Engaging.Position), reach + 0.01f,
                        "a group was biting something outside its reach");
                }
            }

            Assert.IsTrue(everEngaged, "nothing attacked the caravan all level");
        }

        [Test]
        public void ATroopTurnsTowardDangerBeforeItCanReachIt()
        {
            var run = Run();
            bool everWatchedOutOfReach = false;

            for (int step = 0; step < 4000 && run.Outcome == RunOutcome.InProgress; step++)
            {
                run.Step();

                foreach (var group in run.Squad.Slots)
                {
                    if (group == null || !group.Alive) continue;

                    var threat = group.Threat;
                    if (threat == null) continue;

                    Assert.IsTrue(threat.Awake, "the escort turned to stare at a sleeping ambush");
                    Assert.IsFalse(run.Combat.IsDefeated(threat), "a group was watching a corpse");
                    Assert.LessOrEqual(Vec2.Distance(threat.Position, group.Position),
                                       CombatSystem.WatchRadius + 0.01f);

                    // The whole point: something to face while it is still crossing the
                    // ground, which is exactly the stretch the escort used to spend with
                    // its back turned.
                    if (group.Target == null) everWatchedOutOfReach = true;
                }
            }

            Assert.IsTrue(everWatchedOutOfReach,
                "no troop ever faced a threat it could not yet hit, so nothing changed");
        }

        [Test]
        public void APackIsDrawnAtItsStrengthAndNoMore()
        {
            var run = Run();

            for (int step = 0; step < 4000 && run.Outcome == RunOutcome.InProgress; step++)
            {
                run.Step();

                foreach (var enemy in run.Detection.Enemies)
                {
                    int alive = run.Combat.ModelsAlive(enemy);

                    Assert.GreaterOrEqual(alive, 0);
                    Assert.LessOrEqual(alive, EnemyTable.GroupSize(enemy.Kind),
                        "more animals were drawn than the group has");

                    if (run.Combat.IsDefeated(enemy))
                        Assert.AreEqual(0, alive, "a destroyed group still had animals standing");
                    else
                        Assert.Greater(alive, 0, "a living group had nothing left to draw");
                }
            }
        }
    }
}
