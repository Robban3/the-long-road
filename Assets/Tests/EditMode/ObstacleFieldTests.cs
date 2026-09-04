using TheVail.Sim;
using NUnit.Framework;

namespace TheVail.Tests
{
    public class ObstacleFieldTests
    {
        [Test]
        public void AnEmptyFieldLeavesEveryPositionAlone()
        {
            // The headless runs and every test in this project walk an empty country, so
            // this is the case that must cost nothing and change nothing.
            var field = new ObstacleField();
            var wanted = new Vec2(12f, 34f);

            Assert.AreEqual(wanted, field.Clear(wanted, 1.1f));
            Assert.IsFalse(field.Blocked(wanted));
        }

        [Test]
        public void APositionInsideATrunkIsPushedOutOfIt()
        {
            var field = new ObstacleField();
            field.Add(20f, 20f, 1f);

            var pushed = field.Clear(new Vec2(20.3f, 20f), clearance: 0.5f);

            Assert.IsFalse(field.Blocked(pushed, 0.49f), "the troop was still inside the tree");
            Assert.AreEqual(21.5f, pushed.X, 0.001f, "pushed out along the wrong bearing");
            Assert.AreEqual(20f, pushed.Y, 0.001f);
        }

        [Test]
        public void APositionOnTheCentreStillHasSomewhereToGo()
        {
            var field = new ObstacleField();
            field.Add(8f, 8f, 1.5f);

            var pushed = field.Clear(new Vec2(8f, 8f), clearance: 0f);

            Assert.IsFalse(field.Blocked(pushed), "standing on the trunk's own centre trapped the group");
        }

        [Test]
        public void ClearGroundIsLeftWhereItWas()
        {
            var field = new ObstacleField();
            field.Add(4f, 4f, 1f);

            var wanted = new Vec2(30f, 30f);

            Assert.AreEqual(wanted, field.Clear(wanted, 1.1f));
        }

        [Test]
        public void APropWiderThanABucketStillBlocks()
        {
            // Discs are bucketed by tile and a ruin is wider than one, so a lookup that
            // only read the bucket a disc's centre fell in would walk through anything
            // big. Every bucket a disc reaches holds it.
            var field = new ObstacleField();
            field.Add(20f, 20f, 5f);

            Assert.IsTrue(field.Blocked(new Vec2(24f, 20f)), "the far side of a wide ruin was open ground");
        }

        [Test]
        public void ARadiusIsCappedRatherThanTrusted()
        {
            var field = new ObstacleField();
            field.Add(30f, 30f, 400f);

            Assert.IsFalse(field.Blocked(new Vec2(30f + ObstacleField.MaxRadius + 1f, 30f)),
                           "one badly measured prop put a hole in the map");
        }

        [Test]
        public void TheEscortTakesItsPostsRoundWhatIsStandingThere()
        {
            // The whole point, end to end: a squad given a field walks round a trunk on
            // its post, and the same squad without one walks through it.
            var grid = new TileGrid(24, 24);
            for (int i = 0; i < grid.TileCount; i++) grid[i] = TerrainType.Plains;

            var squad = new Squad(12);
            squad.TryPlace(FormationSlot.Van, TroopKind.Spearmen);

            var centre = new Vec2(40f, 40f);
            var heading = new Vec2(1f, 0f);

            squad.UpdatePositions(centre, heading);
            var open = squad[FormationSlot.Van].Position;

            var field = new ObstacleField();
            field.Add(open.X, open.Y, 1f);
            squad.Obstacles = field;

            squad.UpdatePositions(centre, heading);
            var moved = squad[FormationSlot.Van].Position;

            Assert.AreNotEqual(open, moved, "the group stood in the tree that was on its post");
            Assert.IsFalse(field.Blocked(moved, Squad.TroopRadius - 0.01f),
                           "the group moved but was still inside the tree");
        }
    }
}
