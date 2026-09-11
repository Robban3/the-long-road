using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>The demo switch: every chapter open for showing the game, and never saved.</summary>
    public class DemoTests
    {
        [Test]
        public void TheDemoOpensTheSnowWithoutAnyStars()
        {
            var campaign = new Campaign();
            Assert.IsFalse(campaign.ChapterOpen(2), "chapter two was open to a new campaign");

            campaign.OpenAll = true;

            Assert.IsTrue(campaign.ChapterOpen(2));
            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                Assert.IsTrue(campaign.Unlocked(2, level), $"2-{level} stayed locked in the demo");
        }

        [Test]
        public void TheDemoIsNotWrittenIntoTheSave()
        {
            // A save made while showing the game must not open the gates for good.
            var campaign = new Campaign { OpenAll = true };
            campaign.Record(1, 1, stars: 1, gold: 10);

            var loaded = Campaign.Load(campaign.Save());

            Assert.IsFalse(loaded.OpenAll);
            Assert.IsFalse(loaded.ChapterOpen(2));
        }
    }
}
