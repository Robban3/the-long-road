using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class CampaignTests
    {
        [Test]
        public void TheFirstLevelIsOpenAndNothingElseIs()
        {
            var campaign = new Campaign();

            Assert.IsTrue(campaign.Unlocked(1, 1));
            Assert.IsFalse(campaign.Unlocked(1, 2), "level 2 was open before level 1 was beaten");
            Assert.IsFalse(campaign.Unlocked(2, 1), "chapter 2 was open on a fresh save");
        }

        [Test]
        public void ClearingALevelOpensTheNextOne()
        {
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 1, gold: 50);

            Assert.IsTrue(campaign.Cleared(1, 1));
            Assert.IsTrue(campaign.Unlocked(1, 2));
            Assert.IsFalse(campaign.Unlocked(1, 3));
            Assert.AreEqual(50, campaign.Gold);
        }

        [Test]
        public void ACearedLevelStaysOpenForever()
        {
            // The whole point of the roadmap: a beaten level is somewhere you can go
            // back to, not a door that shut behind you.
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 2, gold: 40);
            campaign.Record(1, 2, stars: 3, gold: 40);

            Assert.IsTrue(campaign.Unlocked(1, 1));
            Assert.IsTrue(campaign.Unlocked(1, 2));
        }

        [Test]
        public void ReplayingKeepsTheBestScoreAndStillPays()
        {
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 3, gold: 100);

            bool improved = campaign.Record(1, 1, stars: 1, gold: 60);

            Assert.IsFalse(improved, "a worse replay was reported as an improvement");
            Assert.AreEqual(3, campaign.Stars(1, 1), "a worse replay overwrote the best score");
            Assert.AreEqual(160, campaign.Gold, "a replay paid nothing");
        }

        [Test]
        public void ABetterReplayRaisesTheScore()
        {
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 1, gold: 0);

            Assert.IsTrue(campaign.Record(1, 1, stars: 3, gold: 0));
            Assert.AreEqual(3, campaign.Stars(1, 1));
        }

        [Test]
        public void ALostRunClearsNothingButKeepsTheGold()
        {
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 0, gold: 25);

            Assert.IsFalse(campaign.Cleared(1, 1));
            Assert.IsFalse(campaign.Unlocked(1, 2));
            Assert.AreEqual(25, campaign.Gold);
        }

        [Test]
        public void TheNextChapterNeedsStarsNotJustAnEnding()
        {
            // Ten one-star wins finish the chapter and must not open the next one, or
            // the star rating buys nothing and replaying a level is pointless.
            var scraped = new Campaign();
            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                scraped.Record(1, level, stars: 1, gold: 0);

            Assert.AreEqual(Campaign.LevelsPerChapter, scraped.StarsIn(1));
            Assert.IsFalse(scraped.ChapterOpen(2));

            var solid = new Campaign();
            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                solid.Record(1, level, stars: 2, gold: 0);

            Assert.GreaterOrEqual(solid.StarsIn(1), Campaign.StarsToOpenNextChapter);
            Assert.IsTrue(solid.ChapterOpen(2));
            Assert.IsTrue(solid.Unlocked(2, 1));
        }

        [Test]
        public void GoingBackToRaiseAStarCanOpenTheNextChapter()
        {
            var campaign = new Campaign();
            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                campaign.Record(1, level, stars: level == 3 ? 1 : 2, gold: 0);

            Assert.IsFalse(campaign.ChapterOpen(2), "the gate opened one star short");

            campaign.Record(1, 3, stars: 3, gold: 0);

            Assert.IsTrue(campaign.ChapterOpen(2), "replaying for stars did not move the campaign on");
        }

        [Test]
        public void TheRoadmapOpensOnTheFirstUnbeatenLevel()
        {
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 3, gold: 0);
            campaign.Record(1, 2, stars: 2, gold: 0);

            campaign.Furthest(out int chapter, out int level);

            Assert.AreEqual(1, chapter);
            Assert.AreEqual(3, level);
        }

        [Test]
        public void ASaveSurvivesARoundTrip()
        {
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 3, gold: 120);
            campaign.Record(1, 2, stars: 1, gold: 60);
            campaign.Earn(0, gems: 15);

            var loaded = Campaign.Load(campaign.Save());

            Assert.AreEqual(campaign.Gold, loaded.Gold);
            Assert.AreEqual(15, loaded.Gems);
            Assert.AreEqual(3, loaded.Stars(1, 1));
            Assert.AreEqual(1, loaded.Stars(1, 2));
            Assert.AreEqual(0, loaded.Stars(1, 3));
            Assert.IsTrue(loaded.Unlocked(1, 3));
        }

        [Test]
        public void ABrokenSaveStartsAFreshCampaignRatherThanThrowing()
        {
            foreach (var rubbish in new[] { null, "", "nonsense", "2|0|0|1.1.3", "1|a|b" })
            {
                var campaign = Campaign.Load(rubbish);

                Assert.AreEqual(0, campaign.TotalStars);
                Assert.IsTrue(campaign.Unlocked(1, 1));
            }
        }

        [Test]
        public void SpendingRefusesWhatThePurseCannotCover()
        {
            var campaign = new Campaign();
            campaign.Earn(100);

            Assert.IsFalse(campaign.Spend(101));
            Assert.AreEqual(100, campaign.Gold);
            Assert.IsTrue(campaign.Spend(60));
            Assert.AreEqual(40, campaign.Gold);
        }
    }
}
