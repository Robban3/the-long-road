using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    /// <summary>
    /// The reach a troop group actually has, which the view now draws as a circle on the
    /// ground.
    ///
    /// It was worked out inside the strike loop and thrown away every step, so the one
    /// number the player is asked to spend silver on could not be shown, checked or
    /// argued with. These tests are about the number itself; that the ring is that number
    /// is a matter of the view calling this and nothing else.
    /// </summary>
    public class ReachTests
    {
        [Test]
        public void TheScoutWalksAheadOfTheColumn()
        {
            // She had the best eyes in the army and a post in the formation, so what she
            // looked at was the ground the column was already standing on.
            var squad = new Squad(12);
            squad.TryPlace(FormationSlot.Van, TroopKind.Spearmen);
            squad.TryPlace(FormationSlot.Scouting, TroopKind.Scout);

            var run = Run(squad);
            run.Step();

            var scout = run.Squad[FormationSlot.Scouting];
            var van = run.Squad[FormationSlot.Van];
            var lead = run.Caravan.LeadPosition;

            Assert.Greater(Vec2.Distance(scout.Position, lead),
                           Vec2.Distance(van.Position, lead),
                           "the scout was no further out than the van");
        }

        [Test]
        public void TheScoutFallsBackWhenBladesAreOut()
        {
            // Fourteen metres in front of a charge is not scouting. She is the one troop
            // in the game that cannot afford to be the first thing reached.
            var squad = new Squad(12);
            squad.TryPlace(FormationSlot.Scouting, TroopKind.Scout);

            var run = Run(squad);
            run.Step();

            var scout = run.Squad[FormationSlot.Scouting];
            float ahead = Vec2.Distance(scout.Position, run.Caravan.LeadPosition);

            run.Squad.Scouting = false;
            run.Squad.UpdatePositions(run.Caravan);

            float back = Vec2.Distance(scout.Position, run.Caravan.LeadPosition);

            Assert.Less(back, ahead, "the scout stayed out in front with a fight on");
        }

        static Squad WithScout()
        {
            var squad = new Squad(12);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
            squad.TryPlace(FormationSlot.Scouting, TroopKind.Scout);
            return squad;
        }

        static Squad Blind()
        {
            var squad = new Squad(12);
            squad.TryPlace(FormationSlot.RightVan, TroopKind.Archers);
            return squad;
        }

        static LevelRun Run(Squad squad)
        {
            var map = TerrainGenerator.Generate(new ChapterRecipe().ForLevel(1),
                                                DeterministicRandom.SeedFor(1, 1));
            return new LevelRun(map, map.Corridors[0].Tiles, squad);
        }

        [Test]
        public void BuyingRangeWidensTheCircle()
        {
            var run = Run(WithScout());
            var archers = run.Squad[FormationSlot.RightVan];

            float before = run.Combat.Reach(archers, TerrainType.Plains);

            archers.SpecialLevel = 3;
            float after = run.Combat.Reach(archers, TerrainType.Plains);

            Assert.Greater(after, before, "the range track bought no reach");
        }

        [Test]
        public void RangeGrowsWithoutAScout()
        {
            // The reason this test exists: reach used to be clamped to the squad's sight,
            // so an archer group with no scout was stuck at eighteen metres however much
            // range was bought — the upgrade took the silver and the ring on the ground
            // did not move. Sight still decides what may be shot at, through revelation;
            // it no longer decides how far.
            var run = Run(Blind());
            var archers = run.Squad[FormationSlot.RightVan];

            float before = run.Combat.Reach(archers, TerrainType.Plains);

            archers.SpecialLevel = 3;
            float after = run.Combat.Reach(archers, TerrainType.Plains);

            Assert.Greater(after, before, "range bought without a scout bought nothing");
        }

        [Test]
        public void AWoodShortensABowshot()
        {
            var run = Run(WithScout());
            var archers = run.Squad[FormationSlot.RightVan];

            float open = run.Combat.Reach(archers, TerrainType.Plains);
            float wooded = run.Combat.Reach(archers, TerrainType.Forest);

            Assert.Less(wooded, open, "the forest cost the archers nothing");
        }

        [Test]
        public void AHandWeaponHasTheSameReachWhereverItStands()
        {
            var squad = new Squad(12);
            squad.TryPlace(FormationSlot.Rear, TroopKind.Swordsmen);

            var run = Run(squad);
            var swords = run.Squad[FormationSlot.Rear];

            Assert.AreEqual(run.Combat.Reach(swords, TerrainType.Plains),
                            run.Combat.Reach(swords, TerrainType.Forest), 0.001f,
                            "the terrain rule leaked onto a sword");
        }

        [Test]
        public void ReachIsWhatTheFightingUses()
        {
            // Not a tautology now that it is one method: an enemy just inside the reported
            // reach is fought, and one just outside it is not. If the ring and the strike
            // ever disagree, this is where it shows.
            var run = Run(WithScout());
            var archers = run.Squad[FormationSlot.RightVan];

            float reach = run.Combat.Reach(archers, TerrainType.Plains);

            Assert.Greater(reach, 0f);
            Assert.Less(reach, TroopTable.Range(TroopKind.Archers)
                               * TroopUpgrades.RangeMultiplier(TroopUpgrades.MaxLevel)
                               + CombatSystem.EngagementSlack + 0.001f,
                        "reported reach exceeded what an archer could possibly have");
        }
    }
}
