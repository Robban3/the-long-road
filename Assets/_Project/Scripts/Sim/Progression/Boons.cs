namespace TheVeil.Sim
{
    /// <summary>What gold buys between levels, and keeps.</summary>
    public enum Boon : byte
    {
        /// <summary>Silver in the purse at the start of every run.</summary>
        Purse = 0,

        /// <summary>Points to spend on the escort.</summary>
        Muster = 1,

        /// <summary>Carts that take more punishment before they break.</summary>
        Hardened = 2,

        /// <summary>Cheaper upgrades in the field.</summary>
        Smithy = 3,

        /// <summary>A post of the line opened ahead of the chapter's own curve.</summary>
        Outriders = 4,

        // Appended, never reordered: the save stores a boon by its number, so moving one
        // would silently read somebody's trading purse as their trap sense.

        /// <summary>More silver from every group put down.</summary>
        Trade = 5,

        /// <summary>The caravan's own lookout sees further.</summary>
        Watch = 6,

        /// <summary>Traps are spotted further ahead.</summary>
        Tracking = 7,

        /// <summary>A better rate when leftover silver is changed to gold.</summary>
        Exchange = 8,

        /// <summary>Carts mend themselves while the road is quiet.</summary>
        Repair = 9,

        /// <summary>The treasure cart takes less of what gets through.</summary>
        Lashings = 10
    }

    /// <summary>
    /// The shop's stock: what each thing does, how deep it goes, and what it costs.
    ///
    /// Every one is a number the fighting already reads — silver, squad points, wagon
    /// health, upgrade prices, open posts, sight, trap sight, the exchange rate — with
    /// two exceptions built for this: field repair and the treasure lashings. That
    /// constraint is the design and not a shortcut: a purchase that reaches the run
    /// through a road already built and already tested cannot quietly break the combat
    /// on its way in.
    ///
    /// <b>Thirty steps, not five, and the effect is what is capped.</b> Each step gives a
    /// little less than the one before it:
    ///
    ///     effect(n) = Cap × (1 − <see cref="Falloff"/>ⁿ)
    ///
    /// Step 1 gives 7.5% of the cap, step 15 gives 69%, step 30 gives 90%. The total can
    /// never pass the cap however much is bought — which is precisely what lets there be
    /// thirty steps instead of five without putting the balance at the mercy of whoever
    /// grinds hardest. Five steps was not a balance decision, it was the only depth that
    /// felt safe with a flat curve.
    ///
    /// Prices go the other way, at <see cref="PriceGrowth"/> a step, so a full deep track
    /// costs roughly 2 400–3 200 gold — some twenty completed levels at the measured
    /// ~145 gold each. Everything together costs far more than a ten-chapter campaign
    /// pays out, so what is bought is a choice rather than an order.
    /// </summary>
    public static class BoonTable
    {
        public static readonly Boon[] All =
        {
            Boon.Purse, Boon.Muster, Boon.Hardened, Boon.Smithy, Boon.Outriders,
            Boon.Trade, Boon.Watch, Boon.Tracking, Boon.Exchange, Boon.Repair, Boon.Lashings
        };

        /// <summary>Steps on a continuous track.</summary>
        public const int DeepSteps = 30;

        /// <summary>What each step adds to the price of the next.</summary>
        public const float PriceGrowth = 1.06f;

        /// <summary>
        /// How much of the remaining distance to the cap each step closes.
        ///
        /// 0.925 gives 90% of the cap at thirty steps and about 99% at sixty — so the
        /// track has a real end without a wall, and a step past thirty would be honest
        /// arithmetic that is not worth anybody's gold. That is why the track stops
        /// there rather than running to a hundred: a step that changes nothing is a
        /// counter, not an upgrade.
        /// </summary>
        public const float Falloff = 0.925f;

        // All indexed by (int)Boon.
        static readonly int[] _maxLevel = { 30, 5, 30, 30, 2, 30, 30, 30, 20, 30, 30 };
        static readonly int[] _basePrice = { 40, 220, 50, 60, 400, 50, 60, 40, 60, 70, 50 };

        /// <summary>
        /// What the track is worth when it is finished — the number the curve above
        /// approaches. Shares where the effect is a share; metres, silver or points where
        /// it is a count.
        /// </summary>
        static readonly float[] _cap =
        {
            360f,   // Purse: silver at the start
            5f,     // Muster: squad points
            0.45f,  // Hardened: share of wagon health
            0.40f,  // Smithy: share off a field upgrade
            2f,     // Outriders: posts
            0.45f,  // Trade: share on silver income
            9f,     // Watch: metres of lookout, 12 → 21
            8f,     // Tracking: metres of trap sight
            2f,     // Exchange: silver per gold taken off the rate of 4
            1.8f,   // Repair: wagon hit points a second
            0.35f   // Lashings: share off damage to the treasure cart
        };

        /// <summary>
        /// Whether the track is continuous, and so worth thirty small steps.
        ///
        /// Squad points and formation posts are not. A point of budget and a post in the
        /// line are whole things; dividing either into thirty pieces would give
        /// twenty-nine steps that change nothing visible, which is exactly the lie a row
        /// of pips would be telling. They stay short and dear.
        /// </summary>
        static readonly bool[] _deep =
        {
            true, false, true, true, false, true, true, true, true, true, true
        };

        public static int MaxLevel(Boon boon) => _maxLevel[(int)boon];
        public static bool Deep(Boon boon) => _deep[(int)boon];
        public static float Cap(Boon boon) => _cap[(int)boon];

        /// <summary>Gold for the next step, or zero when the track is finished.</summary>
        public static int Price(Boon boon, int owned)
        {
            if (owned < 0) owned = 0;
            if (owned >= MaxLevel(boon)) return 0;

            return Rounded(_basePrice[(int)boon], PriceGrowth, owned);
        }

        /// <summary>
        /// A price rounded to something a person would say, and always more than the step
        /// before it.
        ///
        /// The second half is the part that had to be built rather than assumed. Rounding
        /// to the nearest ten is right for a price of 583 and wrong for one of 42: six
        /// percent on a base of forty is two and a half gold, which rounds back to forty
        /// — so the first four steps of the cheapest track all cost the same, and a track
        /// whose price does not move is a track whose depth is decoration. Caught by its
        /// own test, which is the only reason it is not in the game.
        ///
        /// So each step is rounded and then, if the rounding flattened it, pushed up by
        /// one increment. That holds for any base and any growth, which matters because
        /// the next person to tune these numbers should not have to rediscover it.
        /// </summary>
        public static int Rounded(float basePrice, float growth, int steps)
        {
            float price = basePrice;
            int previous = 0;

            for (int i = 0; i <= steps; i++)
            {
                int step = price < 100f ? 5 : (price < 500f ? 10 : 50);
                int rounded = (int)(price / step + 0.5f) * step;

                if (rounded <= previous) rounded = previous + step;

                if (i == steps) return rounded;

                previous = rounded;
                price *= growth;
            }

            return previous;
        }

        /// <summary>
        /// What the track is worth at this many steps.
        ///
        /// Deep tracks approach their cap and never reach it, so a caller may pass any
        /// level at all — a save from a future build, a test asking for a thousand — and
        /// get a number the game can survive.
        /// </summary>
        public static float Effect(Boon boon, int level)
        {
            if (level <= 0) return 0f;

            int max = MaxLevel(boon);
            if (level > max) level = max;

            if (!Deep(boon)) return _cap[(int)boon] * level / max;

            float remaining = 1f;
            for (int i = 0; i < level; i++) remaining *= Falloff;

            return _cap[(int)boon] * (1f - remaining);
        }
    }

    /// <summary>
    /// The boons a player owns, resolved into the numbers a run needs.
    ///
    /// A plain carrier rather than a lookup into the campaign, so the run never has to
    /// know a campaign exists: the headless tests, the capture and a level opened
    /// straight from the editor all pass nothing and get the game as it has always been.
    /// </summary>
    public sealed class Boons
    {
        readonly int[] _levels = new int[BoonTable.All.Length];

        public int Level(Boon boon) => _levels[(int)boon];

        public void Set(Boon boon, int level)
        {
            int max = BoonTable.MaxLevel(boon);
            _levels[(int)boon] = level < 0 ? 0 : (level > max ? max : level);
        }

        float Of(Boon boon) => BoonTable.Effect(boon, _levels[(int)boon]);

        public int StartingSilver => (int)Of(Boon.Purse);
        public int ExtraSquadPoints => (int)Of(Boon.Muster);
        public int ExtraPosts => (int)Of(Boon.Outriders);

        /// <summary>Multiplier on wagon health. One when nothing has been bought.</summary>
        public float WagonHealth => 1f + Of(Boon.Hardened);

        /// <summary>Multiplier on silver earned from every source in a run.</summary>
        public float SilverIncome => 1f + Of(Boon.Trade);

        /// <summary>Metres added to the caravan's own lookout, and to its trap sense.</summary>
        public float ExtraSight => Of(Boon.Watch);
        public float ExtraTrapSight => Of(Boon.Tracking);

        /// <summary>Wagon hit points mended a second while nothing is fighting.</summary>
        public float RepairPerSecond => Of(Boon.Repair);

        /// <summary>Share of damage the treasure cart is spared.</summary>
        public float TreasureGuard => Of(Boon.Lashings);

        /// <summary>
        /// Multiplier on what a field upgrade costs. Floored well above zero: a track the
        /// player can fill for nothing is not an economy.
        /// </summary>
        public float UpgradeCost
        {
            get
            {
                float cost = 1f - Of(Boon.Smithy);
                return cost < 0.5f ? 0.5f : cost;
            }
        }

        /// <summary>
        /// Silver needed for one gold when what is left over is changed at the end.
        ///
        /// Poor on purpose and never generous: spending silver in the field has to stay
        /// better than hoarding it, or the whole mid-run economy becomes a savings
        /// account. This only makes the waste hurt less.
        /// </summary>
        public int SilverPerGold
        {
            get
            {
                int rate = (int)(RunEconomy.SilverPerGold - Of(Boon.Exchange) + 0.5f);
                return rate < 2 ? 2 : rate;
            }
        }

        public bool Any
        {
            get
            {
                foreach (int level in _levels) if (level > 0) return true;
                return false;
            }
        }
    }
}
