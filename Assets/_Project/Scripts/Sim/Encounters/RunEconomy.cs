namespace Arna.Sim
{
    /// <summary>
    /// The silver a player earns and spends during one level (docs/GDD.md §6).
    ///
    /// Silver never survives the level. If it did, a player could walk into a map
    /// already maxed out and the whole upgrade curve — the thing that makes each
    /// level its own small story — would collapse.
    ///
    /// Two things beyond the base drop can raise income, and neither of them is
    /// automatic:
    ///
    /// The <b>marauder</b> is bought, early, at the cost of being weaker right now.
    /// Buying it late is nearly worthless, so it is a genuine wager on what lies
    /// ahead — which pairs with the player being unable to see what lies ahead.
    ///
    /// The <b>flawless bonus</b> is earned by destroying a group without losing a
    /// single model, which rewards putting the right troop in the right position
    /// rather than simply having the bigger numbers.
    ///
    /// What is deliberately absent is any bonus that scales with how upgraded the
    /// army already is. Tying income to power would make one upgrade track the
    /// correct opening on every level, and the choice between weapons, armour and
    /// speciality — the reason the system exists — would stop being a choice.
    /// </summary>
    public sealed class RunEconomy
    {
        public const int MarauderCost = 40;
        public const float MarauderBonus = 0.40f;
        public const float FlawlessBonus = 0.25f;

        /// <summary>Paid when a group is spotted before it wakes — the scout's income.</summary>
        public const int ScoutingBonus = 10;

        /// <summary>Leftover silver converts to gold at this rate: always worse than spending it.</summary>
        public const int SilverPerGold = 4;

        /// <summary>Cost of each successive level within one upgrade track.</summary>
        static readonly int[] TrackCosts = { 20, 32, 51, 82, 131 };
        public const int MaxTrackLevel = 5;

        readonly float _levelMultiplier;

        public RunEconomy(float levelSilverMultiplier = 1f)
        {
            _levelMultiplier = levelSilverMultiplier <= 0f ? 1f : levelSilverMultiplier;
        }

        public int Silver { get; private set; }
        public bool HasMarauder { get; private set; }
        public int TotalEarned { get; private set; }
        public int TotalSpent { get; private set; }
        public int FlawlessVictories { get; private set; }

        /// <summary>Total multiplier currently applied to a non-flawless kill.</summary>
        public float IncomeMultiplier => _levelMultiplier * (HasMarauder ? 1f + MarauderBonus : 1f);

        public int AwardGroupKill(EnemyKind kind, bool flawless = false)
        {
            if (flawless) FlawlessVictories++;
            float bonus = flawless ? 1f + FlawlessBonus : 1f;
            return Award(EnemyTable.GroupSilver(kind) * bonus);
        }

        public int AwardTrapDisarm(TrapKind kind) => Award(TrapTable.DisarmSilver(kind));

        /// <summary>
        /// Paid for seeing a group before it wakes. Without this the scout and the
        /// engineer — who kill almost nothing — would be unaffordable in a
        /// kill-driven economy, and the information game would collapse with them.
        /// </summary>
        public int AwardScouting() => Award(ScoutingBonus);

        public int AwardCache(int amount) => Award(amount);

        int Award(float amount)
        {
            int granted = (int)(amount * IncomeMultiplier);
            if (granted < 0) granted = 0;
            Silver += granted;
            TotalEarned += granted;
            return granted;
        }

        /// <summary>
        /// Buys the marauder. Deliberately not refundable and deliberately flat-priced:
        /// its value comes entirely from how much of the level is left.
        /// </summary>
        public bool TryBuyMarauder()
        {
            if (HasMarauder || Silver < MarauderCost) return false;
            Silver -= MarauderCost;
            TotalSpent += MarauderCost;
            HasMarauder = true;
            return true;
        }

        /// <summary>
        /// Cost of raising a track from <paramref name="currentLevel"/> to the next.
        /// <paramref name="costMultiplier"/> lets a premium track — attack range above
        /// all — be priced above the standard curve.
        /// </summary>
        public static int UpgradeCost(int currentLevel, float costMultiplier = 1f)
        {
            if (currentLevel < 0) currentLevel = 0;
            if (currentLevel >= MaxTrackLevel) return int.MaxValue;

            float multiplier = costMultiplier <= 0f ? 1f : costMultiplier;
            return (int)(TrackCosts[currentLevel] * multiplier);
        }

        public bool CanAfford(int currentLevel, float costMultiplier = 1f)
            => Silver >= UpgradeCost(currentLevel, costMultiplier);

        public bool TryUpgrade(int currentLevel, out int cost) => TryUpgrade(currentLevel, 1f, out cost);

        public bool TryUpgrade(int currentLevel, float costMultiplier, out int cost)
        {
            cost = UpgradeCost(currentLevel, costMultiplier);
            if (currentLevel >= MaxTrackLevel || Silver < cost) return false;

            Silver -= cost;
            TotalSpent += cost;
            return true;
        }

        /// <summary>
        /// Converts what is left at the end of the level. The rate is poor on purpose:
        /// hoarding should never beat spending, but it should not feel like pure loss
        /// either, or players would spend badly just to avoid the waste.
        /// </summary>
        public int ConvertLeftoverToGold()
        {
            int gold = Silver / SilverPerGold;
            Silver = 0;
            return gold;
        }
    }
}
