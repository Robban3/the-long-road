using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// Who waits at the end of a road, and how well attended.
    ///
    /// <b>A chapter had no moment it was about.</b> Ten levels of the same enemies
    /// growing stronger, ending on a level whose goal has a castle standing on it —
    /// scenery, and nothing in front of it. Meanwhile the strongest thing in the game,
    /// the captain, was dealt out by the same uniform draw as a wolf and could land
    /// anywhere or nowhere.
    ///
    /// So every level now has something waiting at its goal, and the tenth has the
    /// chapter's own champion: a rider who has to be put down before the caravan can
    /// arrive. The difference between the two is deliberate and it is not strength —
    /// it is that <b>only the champion blocks the road</b>. Every other encounter in
    /// this game can be driven round, which is what the route drawing is for, and a
    /// level that cannot be finished without a fight is a promise the game breaks once
    /// a chapter rather than ten times.
    ///
    /// This decides <i>who</i>; EncounterPlacer.GuardTheGoal decides where, and
    /// VisualLibrary decides what he looks like. The same split the towns and the
    /// villages use, and for the same reason: the plan map and the run have to agree,
    /// and they only do when one of them is not deciding.
    /// </summary>
    public static class Champions
    {
        /// <summary>
        /// Whether this level has anything waiting at its goal.
        ///
        /// <b>The last level of a chapter, and only that one.</b> This began as every
        /// level — a heavy group at every goal to keep the rhythm, with the champion on
        /// the tenth — and the measuring killed it (The Veil > Champion Report, the sweep
        /// over all thirty levels). A group posted at the goal is met by <i>every</i>
        /// route, by construction, so eight points standing there weigh far more than
        /// eight points scattered over the road: 1-3 went from two survivable roads to
        /// one, 3-2 from three to two, and 2-5 — which had exactly one — went to none.
        ///
        /// The promise those levels were breaking is the oldest one in the generator:
        /// every level has at least one road that can be fought through. Buying a rhythm
        /// with it is a bad trade, and the rhythm was never what was asked for. The boss
        /// is what was asked for, and a boss once a chapter is what makes it a boss.
        /// </summary>
        public static bool Guards(int chapter, int level) => Named(chapter, level);

        /// <summary>
        /// The tenth level of a chapter, where the castle stands on the goal.
        ///
        /// The one fight in a chapter the player cannot drive round. Every other encounter
        /// in this game can be routed past — that is the whole of the route drawing — and
        /// a level that cannot be finished without a fight is a promise the game breaks
        /// once a chapter rather than ten times.
        /// </summary>
        public static bool Named(int chapter, int level)
            => level >= Campaign.LevelsPerChapter && level <= Campaign.LevelsPerChapter;

        /// <summary>
        /// What stands at a level's goal.
        ///
        /// The heaviest thing the level is allowed to field, or the chapter's champion on
        /// the level he holds. <b>Drawn from the pool and not from a constant</b>, because
        /// the first draft posted a horseman at every goal and chapter one never unlocks
        /// horsemen — so 1-1, which is meant to be wolves and nothing else, met cavalry at
        /// its goal. What waits at the end of a level has to be something the level was
        /// allowed to contain, or the unlock table is decoration.
        /// </summary>
        public static EnemyKind GuardKind(IReadOnlyList<EnemyKind> pool, bool blocks)
        {
            if (blocks) return EnemyKind.Champion;
            if (pool == null || pool.Count == 0) return EnemyKind.Wolf;

            var worst = pool[0];
            for (int i = 1; i < pool.Count; i++)
                if (EnemyTable.Points(pool[i]) > EnemyTable.Points(worst)) worst = pool[i];

            return worst;
        }

        /// <summary>
        /// What the stand at the goal costs, as a purse of its own.
        ///
        /// <b>Not taken out of the road's budget, and that was tried first.</b> Paying for
        /// the goal fight out of <see cref="LevelRecipe.EnemyBudget"/> left the road
        /// poorer by exactly that much, and the road's budget is not slack: 1-2 fell from
        /// four groups on the worst drawn route to three, against a promise of four, and
        /// the trap tests went quiet because the groups that used to spring them were no
        /// longer bought. Threat at the goal is *additional* content — it is the thing the
        /// chapter is about — so it is bought with additional points and the ceiling is
        /// raised to say so, rather than the road being silently emptied to afford it.
        /// </summary>
        public static int Purse(IReadOnlyList<EnemyKind> pool, bool blocks, int retinue)
        {
            int purse = EnemyTable.Points(GuardKind(pool, blocks));
            if (!blocks) return purse;

            for (int i = 0; i < retinue; i++)
            {
                var companion = CompanionAt(pool, i);
                if (companion.HasValue) purse += EnemyTable.Points(companion.Value);
            }

            return purse;
        }

        /// <summary>
        /// One of the men who ride with the champion, or nothing if the chapter has not
        /// unlocked anybody fit to.
        ///
        /// <b>Ordinary horsemen and bowmen, and never the heaviest thing in the pool.</b>
        /// Drawing them the same way the guard is drawn seemed tidy and gave the champion
        /// of chapter two an escort of two captains — four hundred and seventy-six health
        /// apiece on top of his own, at the one place on the map the caravan cannot go
        /// round. Every road of 2-10 was lost with the champion barely scratched: the
        /// player was being killed by the retinue while the fight the level is named for
        /// had not started.
        ///
        /// Riders first and archers second, alternating, because the choice of which to
        /// answer is the fight. A rider closes and a bowman does not, so a line that turns
        /// to meet the horse is shot in the back and a line that answers the bows is
        /// ridden down — and that is a decision, which is more than another captain is.
        /// </summary>
        static EnemyKind? CompanionAt(IReadOnlyList<EnemyKind> pool, int index)
        {
            var wanted = index % 2 == 0 ? EnemyKind.BanditRider : EnemyKind.BanditArcher;
            if (Has(pool, wanted)) return wanted;

            var other = index % 2 == 0 ? EnemyKind.BanditArcher : EnemyKind.BanditRider;
            if (Has(pool, other)) return other;

            return null;
        }

        static bool Has(IReadOnlyList<EnemyKind> pool, EnemyKind kind)
        {
            if (pool == null) return false;

            for (int i = 0; i < pool.Count; i++)
                if (pool[i] == kind) return true;

            return false;
        }

        /// <summary>The companions, in the order they are posted. See CompanionAt.</summary>
        public static EnemyKind? Companion(IReadOnlyList<EnemyKind> pool, int index)
            => CompanionAt(pool, index);

        /// <summary>
        /// How many ride with the champion.
        ///
        /// One group, and two from the fifth chapter. The companions are ordinary riders
        /// and archers: what makes the fight a fight is having to choose which to answer
        /// first, not another row in the enemy table.
        ///
        /// <b>A group is not a man, and counting them like men is what went wrong.</b>
        /// Two groups reads as "two companions" and is six figures — and every one of
        /// them is multiplied by the chapter's EnemyStrength, which doubles by chapter
        /// three while the escort's points rise by not quite half. Measured on 3-10: the
        /// retinue alone came to eleven hundred health and eighty-eight damage a second,
        /// standing on the ground the caravan has to arrive at. It killed the column in
        /// nine seconds with the champion still at full health — the fight the level is
        /// named for never started, three roads that had been winnable were lost, and the
        /// cause was the two bodyguards rather than the boss.
        ///
        /// The escalation across chapters is already in EnemyStrength. This is company.
        /// </summary>
        public static int Retinue(int chapter)
        {
            if (!Settled(chapter)) return 0;
            return chapter >= 5 ? 2 : 1;
        }

        /// <summary>
        /// Whether a chapter fields a retinue at all.
        ///
        /// Chapter one does not. Its champion is the first thing in the game the player
        /// cannot drive round, and meeting that for the first time with three horsemen
        /// beside it is how a lesson becomes a wall.
        /// </summary>
        static bool Settled(int chapter) => chapter >= 2;
    }
}
