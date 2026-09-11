using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// The shape of the escort: six posts in the line that open as a chapter goes on, one
    /// of which the scout may take.
    /// </summary>
    public class FormationPostTests
    {
        [Test]
        public void TheScoutTakesOneOfTheSix()
        {
            // She had a seventh post of her own, and a troop that costs a place in the
            // line nobody else wanted was a troop everybody brought. The corner she holds
            // is now a corner nobody with a sword is holding.
            var squad = new Squad(30);

            Assert.IsTrue(squad.TryPlace(FormationSlot.LeftVan, TroopKind.Scout));

            int line = 0;
            for (int i = 0; i < TroopTable.LinePosts; i++)
                if (squad.TryPlace(TroopTable.Line[i], TroopKind.Spearmen)) line++;

            Assert.AreEqual(TroopTable.LinePosts - 1, line, "the scout did not cost the line a post");
        }

        [Test]
        public void AnEscortBringsOneScout()
        {
            // There is one of her: hired once in the shop, not recruited by the dozen.
            var squad = new Squad(30);

            Assert.IsTrue(squad.TryPlace(FormationSlot.Van, TroopKind.Scout));
            Assert.IsFalse(squad.TryPlace(FormationSlot.Rear, TroopKind.Scout), "a second scout came along");
            Assert.IsFalse(squad.TryPlace(TroopKind.Scout));
            Assert.IsTrue(squad.HasScout);
        }

        [Test]
        public void TheScoutIsNeverTheOneATrapStrikes()
        {
            // She holds the van and walks ahead of it clearing what she sees; one she
            // missed lands on the first troop behind her, front to back.
            var squad = new Squad(30);
            squad.TryPlace(FormationSlot.Van, TroopKind.Scout);
            squad.TryPlace(FormationSlot.LeftVan, TroopKind.Shieldbearer);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Spearmen);

            Assert.AreSame(squad[FormationSlot.LeftVan], squad.PointTroop);
        }

        [Test]
        public void OtherwiseTheVanTakesTheTrapAsItAlwaysDid()
        {
            var squad = new Squad(30);
            squad.TryPlace(FormationSlot.Van, TroopKind.Shieldbearer);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Spearmen);

            Assert.AreSame(squad[FormationSlot.Van], squad.PointTroop);

            // An empty van lets it through to the wagons, as before.
            Assert.IsNull(new Squad(30).PointTroop);
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
                Assert.IsFalse(squad.TryPlace(TroopTable.Line[i], TroopKind.Scout),
                    $"{TroopTable.Line[i]} took the scout while closed");
            }
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
            Assert.IsFalse(squad.TryPlace(TroopKind.Scout), "the scout found a post outside the line");
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
