using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// The achievements: what they count, that a reward is paid once, and that the count
    /// survives the save.
    /// </summary>
    public class AchievementTests
    {
        static RunTally Won(int groups = 0, int traps = 0, int wagonsLost = 0, int gold = 0)
            => new RunTally { Arrived = true, GroupsBeaten = groups, TrapsDisarmed = traps,
                              WagonsLost = wagonsLost, Gold = gold };

        [Test]
        public void RunsAddUpAcrossTheCampaign()
        {
            var campaign = new Campaign();

            campaign.Count(Won(groups: 4, traps: 2, gold: 120));
            campaign.Count(Won(groups: 3, traps: 1, wagonsLost: 1, gold: 80));
            campaign.Count(new RunTally { Arrived = false, GroupsBeaten = 2 });

            Assert.AreEqual(9, campaign.TallyOf(Tally.GroupsBeaten));
            Assert.AreEqual(3, campaign.TallyOf(Tally.TrapsDisarmed));
            Assert.AreEqual(200, campaign.TallyOf(Tally.GoldEarned));
            Assert.AreEqual(1, campaign.TallyOf(Tally.FlawlessArrivals),
                "a run that lost a wagon, or never arrived, was counted as flawless");
        }

        [Test]
        public void LevelsAreCountedFromTheStarsAndNotAgain()
        {
            // The stars already say which levels are cleared; a second count of the same
            // thing is a count that can disagree with the first.
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 3, gold: 0);
            campaign.Record(1, 2, stars: 1, gold: 0);
            campaign.Record(1, 2, stars: 2, gold: 0);

            Assert.AreEqual(1, campaign.Progress(Achievement.FirstRoad));
            Assert.AreEqual(2, campaign.Progress(Achievement.TenRoads));
            Assert.AreEqual(1, campaign.Progress(Achievement.FirstThreeStars));
            Assert.IsTrue(campaign.Achieved(Achievement.FirstThreeStars));
            Assert.IsFalse(campaign.Achieved(Achievement.ChapterOne));
        }

        [Test]
        public void ARewardIsPaidOnceAndOnlyWhenEarned()
        {
            var campaign = new Campaign();

            Assert.IsFalse(campaign.TryClaim(Achievement.FirstRoad), "paid before it was earned");

            campaign.Record(1, 1, stars: 1, gold: 0);
            int before = campaign.Gold;

            Assert.IsTrue(campaign.AnythingToClaim);
            Assert.IsTrue(campaign.TryClaim(Achievement.FirstRoad));
            Assert.AreEqual(before + AchievementTable.Gold(Achievement.FirstRoad), campaign.Gold);

            Assert.IsFalse(campaign.TryClaim(Achievement.FirstRoad), "paid twice");
            Assert.IsTrue(campaign.Claimed(Achievement.FirstRoad));
        }

        [Test]
        public void AnAchievementsOwnGoldDoesNotCountTowardTheGoldAchievements()
        {
            // Or the gold ones would pay themselves: collect a reward, earn "gold", collect
            // the next.
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 1, gold: 0);
            campaign.TryClaim(Achievement.FirstRoad);

            Assert.AreEqual(0, campaign.TallyOf(Tally.GoldEarned));
        }

        [Test]
        public void GemsAreEarnedHere()
        {
            // The one source of gems in the game. If no achievement paid any, the shop's
            // premium currency would still be something nobody can have.
            int total = 0;
            foreach (var a in AchievementTable.All) total += AchievementTable.Gems(a);

            Assert.Greater(total, 0);
        }

        [Test]
        public void TheCountAndTheClaimsSurviveTheSave()
        {
            var campaign = new Campaign();
            campaign.Record(1, 1, stars: 3, gold: 50);
            campaign.Count(Won(groups: 7, traps: 2, gold: 50));
            campaign.TryClaim(Achievement.FirstRoad);

            var loaded = Campaign.Load(campaign.Save());

            Assert.AreEqual(7, loaded.TallyOf(Tally.GroupsBeaten));
            Assert.AreEqual(2, loaded.TallyOf(Tally.TrapsDisarmed));
            Assert.AreEqual(1, loaded.TallyOf(Tally.FlawlessArrivals));
            Assert.IsTrue(loaded.Claimed(Achievement.FirstRoad));
            Assert.IsFalse(loaded.Claimed(Achievement.FirstThreeStars));
        }

        [Test]
        public void ASaveFromBeforeAchievementsStillLoads()
        {
            var loaded = Campaign.Load("3|340|5|1.1.3,1.2.2|0.2|");

            Assert.AreEqual(340, loaded.Gold);
            Assert.AreEqual(3, loaded.Stars(1, 1));
            Assert.AreEqual(0, loaded.TallyOf(Tally.GroupsBeaten));
            Assert.IsTrue(loaded.Achieved(Achievement.FirstRoad), "levels cleared before achievements existed did not count");
            Assert.IsFalse(loaded.Claimed(Achievement.FirstRoad));
        }
    }
}
