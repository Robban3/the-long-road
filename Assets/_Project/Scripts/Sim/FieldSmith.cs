namespace TheVeil.Sim
{
    /// <summary>
    /// A player at the field smithy during a run: whenever the purse will cover it, the
    /// cheapest upgrade on anybody still standing is bought.
    ///
    /// <b>Because the difficulty curve was judged against an escort nobody can field.</b>
    /// ReferenceSquad handed the escort its smithy levels before the run began - level
    /// one on all twelve tracks in chapter two, level two in chapter three. Those levels
    /// are bought with the run's own silver, a run starts with none, and level two on
    /// twelve tracks is 624 of it: more than most levels pay in total. So every gate
    /// that asked "can the player get down this road" was asking it of a player who had
    /// been given, at the first step, what a real one could at best have by the last.
    ///
    /// This buys the way a player does - as the silver comes in, a level at a time,
    /// spread across the line - so the upgrades a run can afford are the upgrades it gets,
    /// and when it gets them is part of the answer. The cheapest first, because that is
    /// the most levels for the money and it spreads them without anybody deciding to;
    /// weapon before armour on a tie, because a fight ended sooner costs less health than
    /// any amount of armour saves.
    ///
    /// Not optimal, on purpose, for the reason ReferenceSquad's line is not: a gate passed
    /// only by the best possible shopping says nothing about the game.
    /// </summary>
    public static class FieldSmith
    {
        static readonly UpgradeTrack[] Tracks = { UpgradeTrack.Weapon, UpgradeTrack.Armour };

        /// <summary>Spends what the purse holds in the run's own style.</summary>
        public static int Spend(LevelRun run)
            => run != null && run.ShopsDeep ? Deep(run) : Wide(run);

        /// <summary>
        /// Shopping as a player who has thought about it: the silver goes deep on the troop
        /// doing the killing, a level at a time up to the cap, then its armour, then the
        /// next troop - and waits for the price rather than spending the change on
        /// somebody else.
        ///
        /// Because the field smithy is where the level is decided (docs/economy.md §3),
        /// how it is spent should matter, and it does: the same silver is a level on six
        /// troops or three levels on the one that ends fights. This is not the best there
        /// is; it is what a player who reads the line would do.
        /// </summary>
        public static int Deep(LevelRun run)
        {
            if (run?.Squad == null) return 0;

            var order = new System.Collections.Generic.List<TroopGroup>();
            foreach (var group in run.Squad.Slots)
                if (group != null && group.Alive) order.Add(group);

            order.Sort((a, b) => TroopTable.Dps(b.Kind).CompareTo(TroopTable.Dps(a.Kind)));

            int bought = 0;

            foreach (var group in order)
                foreach (var track in Tracks)
                {
                    int price = run.PriceOf(group.Slot, track);
                    if (price <= 0) continue;

                    // Waiting for this one rather than buying something cheaper elsewhere
                    // is the whole of the difference.
                    if (price > run.Economy.Silver) return bought;

                    while (price > 0 && price <= run.Economy.Silver
                           && run.TryUpgrade(group.Slot, track, out _))
                    {
                        bought++;
                        price = run.PriceOf(group.Slot, track);
                    }

                    if (price > 0) return bought;
                }

            return bought;
        }

        /// <summary>Spends what the purse holds, cheapest first, and returns how many levels it bought.</summary>
        public static int Wide(LevelRun run)
        {
            if (run?.Squad == null) return 0;

            int bought = 0;

            while (true)
            {
                TroopGroup chosen = null;
                var track = UpgradeTrack.Weapon;
                int cheapest = int.MaxValue;

                foreach (var group in run.Squad.Slots)
                {
                    if (group == null || !group.Alive) continue;

                    foreach (var candidate in Tracks)
                    {
                        int price = run.PriceOf(group.Slot, candidate);
                        if (price <= 0 || price >= cheapest) continue;

                        cheapest = price;
                        chosen = group;
                        track = candidate;
                    }
                }

                if (chosen == null || cheapest > run.Economy.Silver) break;
                if (!run.TryUpgrade(chosen.Slot, track, out _)) break;

                bought++;
            }

            return bought;
        }
    }
}
