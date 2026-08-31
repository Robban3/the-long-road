using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    /// <summary>
    /// The shape of the escort: six posts in the line that open as a chapter goes on, and
    /// one out in front that is the scout's alone.
    /// </summary>
    public class FormationPostTests
    {
        [Test]
        public void TheScoutTakesHerOwnPostAndNotOneOfTheSix()
        {
            // She does not stand in the formation — she walks ahead of the van wherever
            // she is placed — so a post in the line was a licence to exist rather than a
            // position, and it made her compete for a corner with somebody who would have
            // stood in it.
            var squad = new Squad(20);

            Assert.IsTrue(squad.TryPlace(FormationSlot.Scouting, TroopKind.Scout));
            Assert.IsFalse(squad.TryPlace(FormationSlot.LeftVan, TroopKind.Scout),
                "a scout took a post in the line");

            for (int i = 0; i < TroopTable.LinePosts; i++)
                Assert.IsTrue(squad.TryPlace(TroopTable.Line[i], TroopKind.Spearmen),
                    $"the scout cost the line its {TroopTable.Line[i]} post");
        }

        [Test]
        public void NothingButAScoutMayStandOutInFront()
        {
            var squad = new Squad(20);

            Assert.IsFalse(squad.TryPlace(FormationSlot.Scouting, TroopKind.Shieldbearer),
                "a shieldbearer was sent out to scout");
            Assert.IsFalse(squad.TryPlace(FormationSlot.Scouting, TroopKind.Archers));
        }

        [Test]
        public void AClosedPostRefusesEverything()
        {
            var squad = new Squad(20, posts: 3);

            for (int i = 0; i < 3; i++)
                Assert.IsTrue(squad.Open(TroopTable.Line[i]), $"{TroopTable.Line[i]} should be open");

            for (int i = 3; i < TroopTable.LinePosts; i++)
            {
                Assert.IsFalse(squad.Open(TroopTable.Line[i]));
                Assert.IsFalse(squad.TryPlace(TroopTable.Line[i], TroopKind.Spearmen),
                    $"{TroopTable.Line[i]} took a troop while closed");
            }

            // And the scouting post is never one of them.
            Assert.IsTrue(squad.Open(FormationSlot.Scouting));
            Assert.IsTrue(squad.TryPlace(FormationSlot.Scouting, TroopKind.Scout));
        }

        [Test]
        public void ThePostsOpenAsTheChapterGoesOn()
        {
            var chapter = new ChapterRecipe();

            int first = chapter.ForLevel(1).Posts;
            int last = chapter.ForLevel(chapter.LevelsPerChapter).Posts;

            Assert.Less(first, last, "the line never grew");
            Assert.AreEqual(TroopTable.LinePosts, last, "the line never reaches six");
            Assert.GreaterOrEqual(first, 1);

            int previous = 0;
            for (int level = 1; level <= chapter.LevelsPerChapter; level++)
            {
                int posts = chapter.ForLevel(level).Posts;
                Assert.GreaterOrEqual(posts, previous, "the line lost a post as the chapter went on");
                previous = posts;
            }
        }

        [Test]
        public void TheOpeningPostsAreTheEndsOfTheColumn()
        {
            // A flank guard with nobody on point is a formation with a hole in the one
            // place everything arrives from.
            Assert.AreEqual(FormationSlot.Van, TroopTable.Line[0]);
            Assert.AreEqual(FormationSlot.Rear, TroopTable.Line[1]);
        }

        [Test]
        public void APostIsFoundForATroopThatIsNotToldWhereToStand()
        {
            var squad = new Squad(12, posts: 2);

            Assert.IsTrue(squad.TryPlace(TroopKind.Spearmen));
            Assert.IsTrue(squad.TryPlace(TroopKind.Swordsmen));
            Assert.IsFalse(squad.TryPlace(TroopKind.Swordsmen), "a third troop found a post in a line of two");

            Assert.IsTrue(squad.TryPlace(TroopKind.Scout), "the scouting post was counted as part of the line");
        }

        [Test]
        public void TheBudgetStillBindsBeforeThePostsDo()
        {
            // Twelve points and three posts at the start of a chapter: the points run out
            // first, which is the trade-off the budget exists to force.
            var recipe = new ChapterRecipe().ForLevel(1);
            var squad = new Squad(recipe.SquadBudget, recipe.Posts);

            int placed = 0;
            while (squad.TryPlace(TroopKind.Swordsmen)) placed++;

            Assert.Less(placed, TroopTable.LinePosts);
            Assert.LessOrEqual(squad.PointsSpent, recipe.SquadBudget);
        }
    }
}
