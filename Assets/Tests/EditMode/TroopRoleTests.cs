using TheVeil.Gen;
using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// What the engineers and the shieldbearers are for, now that each has a job of its
    /// own: the engineers mend the wagons on a quiet road, and the shieldbearers draw
    /// the attack onto themselves.
    /// </summary>
    public class TroopRoleTests
    {
        [Test]
        public void EngineersMendAtTheirStrength()
        {
            var squad = new Squad(30);
            Assert.AreEqual(0f, squad.RepairPerSecond, "an escort with no engineers mends");

            Assert.IsTrue(squad.TryPlace(FormationSlot.Van, TroopKind.Engineer));
            Assert.AreEqual(TroopTable.EngineerRepair, squad.RepairPerSecond, 0.0001f);

            var engineers = squad.Slots[(int)FormationSlot.Van];

            // Cut down to one man: mending, but not at full rate.
            engineers.Hp = TroopTable.HpPerModel(TroopKind.Engineer);
            Assert.Greater(squad.RepairPerSecond, 0f);
            Assert.Less(squad.RepairPerSecond, TroopTable.EngineerRepair);

            // And a dead group mends nothing.
            engineers.Hp = 0f;
            Assert.AreEqual(0f, squad.RepairPerSecond);
        }

        static float MendedInOneStep(TroopKind kind)
        {
            var map = LevelMaps.For(1, 1);
            var squad = new Squad(30);
            Assert.IsTrue(squad.TryPlace(FormationSlot.Van, kind));

            var run = new LevelRun(map, map.Corridors[0].Tiles, squad);
            var wagon = run.Caravan.Wagons[0];
            wagon.Hp = wagon.MaxHp * 0.5f;

            float before = wagon.Hp;
            run.Step();
            return wagon.Hp - before;
        }

        [Test]
        public void EngineersMendTheWagonsWhileTheRoadIsQuiet()
        {
            // The first step of a level: nothing is fighting yet.
            Assert.Greater(MendedInOneStep(TroopKind.Engineer), 0f, "the engineers left a damaged wagon alone");
            Assert.AreEqual(0f, MendedInOneStep(TroopKind.Swordsmen), 0.0001f, "a wagon mended itself with no engineer");
        }

        [Test]
        public void AShieldbearerNearlyAsNearDrawsTheAttack()
        {
            Assert.IsTrue(TroopTable.DrawsAttackers(TroopKind.Shieldbearer));
            Assert.IsFalse(TroopTable.DrawsAttackers(TroopKind.Archers));

            // Nearly as near is near enough; right across the caravan is not.
            Assert.IsTrue(CombatSystem.DrawnTo(4f, 4f));
            Assert.IsTrue(CombatSystem.DrawnTo(4f + CombatSystem.ShieldDraw, 4f));
            Assert.IsFalse(CombatSystem.DrawnTo(4f + CombatSystem.ShieldDraw + 1f, 4f));
        }
    }
}
