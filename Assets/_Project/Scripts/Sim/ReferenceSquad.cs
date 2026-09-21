using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// The escort the difficulty curve assumes a player is fielding, at any point in the
    /// campaign.
    ///
    /// <b>This assumption already existed; it just could not be run.</b>
    /// ChapterRecipe.For sets <c>EscortStrength = StrengthAtEndOf(chapter - 1)</c> — the
    /// generator pictures a player who has grown with the chapters behind them, and every
    /// road it judges survivable is judged against that picture. But the only escort
    /// anything actually played was a fixed six written into a test for chapter two:
    /// spears, bows, a crossbow and a sword, at no weapon level, with nothing bought.
    ///
    /// Measured against that, chapter three's tenth level had no road that could be won —
    /// and it should not have. A player arriving at chapter three with chapter two's
    /// troops, unupgraded, is a player who has not been playing the game the levels are
    /// balanced around: the whole of the shop, the whole of the smithy, and the troops
    /// earned by levels cleared all exist to be spent. Judging a chapter by an escort
    /// that ignores them measures the wrong player and then blames the level.
    ///
    /// So the assumption is written down here, once, and the report and the tests both
    /// field it. When a chapter stops being winnable, this is the other place to look:
    /// either the road got harder than the climb, or the escort this describes is no
    /// longer the escort a player would really have.
    /// </summary>
    public static class ReferenceSquad
    {
        /// <summary>
        /// Levels behind the player when they start this one, which is what opens the
        /// heavier troops. See TroopTable.LevelsToUnlock.
        /// </summary>
        public static int LevelsCleared(int chapter, int level)
            => (chapter - 1) * Campaign.LevelsPerChapter + (level - 1);

        /// <summary>
        /// How far the smithy has been taken, in weapon and armour levels.
        ///
        /// <b>Behind the enemy's climb on purpose.</b> A chapter per level, capped — so a
        /// player in chapter three is two levels into a track that goes to five. The
        /// enemies are at double strength by then, and the escort is not: that gap is the
        /// difficulty, and closing it here would make every gate below pass by describing
        /// a player who had bought everything.
        /// </summary>
        public static int Smithy(int chapter)
        {
            int level = chapter - 1;
            return level < 0 ? 0 : (level > RunEconomy.MaxTrackLevel ? RunEconomy.MaxTrackLevel : level);
        }

        /// <summary>
        /// The line a player of this standing would take out: the heaviest horse they have
        /// earned at the front, shot behind it, a priest once there is room, and spears to
        /// fill whatever posts are left.
        ///
        /// Ordered by what the points buy rather than by taste, and deliberately not
        /// optimal — a real player's line is a compromise between holding, shooting and
        /// mending, and a gate passed only by the single best composition in the game is a
        /// gate that says nothing about the game.
        /// </summary>
        public static Squad For(int chapter, int level, Boons boons = null)
        {
            var recipe = ChapterRecipe.For(chapter).ForLevel(level);
            return For(recipe, LevelsCleared(chapter, level), Smithy(chapter), boons);
        }

        /// <summary>
        /// Gold a cleared level pays, on average.
        ///
        /// <b>Measured off LevelRun.GoldEarned rather than chosen.</b> Forty for arriving,
        /// fifteen a wagon for the three that arrive with it, and up to sixty of loot
        /// scaled by what is left of the treasure cart — so a clean run pays about a
        /// hundred and forty-five and a mauled one about ninety. A hundred and ten is a run
        /// that lost a wagon and half its cargo, which is what most of them are.
        /// </summary>
        public const int GoldPerLevel = 110;

        /// <summary>
        /// What share of that gold goes on the smithy rather than on boons.
        ///
        /// Half. The shop sells both and a player buys both; splitting it evenly is a guess
        /// and is written down as one. What it is not is the old assumption, which was that
        /// none of it was spent at all.
        /// </summary>
        public const float SpentOnTroops = 0.5f;

        /// <summary>
        /// The permanent levels a player would have bought by here, and the thing this
        /// whole file was missing.
        ///
        /// <b>ReferenceSquad never shopped.</b> It set WeaponLevel and ArmourLevel — the
        /// fields a run's own silver raises and resets afterwards, capped at five — and
        /// left School at nothing. So the escort every level is balanced against owned not
        /// one of the thirty permanent steps the shop sells on each of its tracks, on any
        /// troop, in any chapter. The curve has been judged against a player who never
        /// spent a gold piece.
        ///
        /// That is most of why the chapters sit on a knife edge: a two-tile change to a
        /// road took 1-10 from two winnable roads to none, and a champion moved four metres
        /// took 3-10 from one to none. Nothing had any slack because the player being
        /// measured had no growth in them.
        ///
        /// Worked out from the income rather than picked: levels cleared times what a run
        /// pays, halved, and then spent buying every sold track up a level at a time until
        /// the gold runs out — which is how a player spends, and what makes the answer fall
        /// out of the prices instead of out of an opinion.
        /// </summary>
        public static TroopBoons School(int cleared) => School(cleared, SpentOnTroops);

        /// <summary>The permanent levels bought with <paramref name="share"/> of the gold.</summary>
        public static TroopBoons School(int cleared, float share)
            => School(cleared, share, Wanted(cleared));

        /// <summary>
        /// The permanent levels bought with <paramref name="share"/> of the gold, on the
        /// troops of <paramref name="line"/> - a player invests in what they take out.
        /// </summary>
        public static TroopBoons School(int cleared, float share, TroopKind[] line)
        {
            var school = new TroopBoons();
            if (cleared <= 0) return school;

            // <b>Only what is actually fielded.</b>
            //
            // The first version of this bought every sold track on every troop in the game
            // — thirteen kinds, about thirty tracks, nine hundred gold a round — and
            // reported that a player is on permanent level one after twenty-nine levels.
            // That is not the economy, it is the model: nobody upgrades a knight they have
            // never taken out. The line is six posts of four kinds, which is nine tracks
            // and two hundred and seventy gold a round, and the answer changes by a factor
            // of three.
            //
            // Read off Wanted, so the thing being paid for is the thing being fielded and
            // the two cannot drift apart.
            var fielded = new List<TroopKind>();
            foreach (var kind in line)
                if (!fielded.Contains(kind)) fielded.Add(kind);

            if (!fielded.Contains(TroopKind.Spearmen)) fielded.Add(TroopKind.Spearmen);

            int purse = (int)(cleared * GoldPerLevel * share);

            for (int level = 0; level < TroopBoonTable.Steps; level++)
            {
                int round = 0;

                foreach (var kind in fielded)
                    foreach (var track in TroopBoonTable.Tracks)
                        if (TroopBoonTable.Sells(kind, track))
                            round += TroopBoonTable.Price(kind, track, level);

                if (round <= 0 || round > purse) break;

                purse -= round;

                foreach (var kind in fielded)
                    foreach (var track in TroopBoonTable.Tracks)
                        if (TroopBoonTable.Sells(kind, track))
                            school.Set(kind, track, level + 1);
            }

            return school;
        }

        public static Squad For(LevelRecipe recipe, int cleared, int smithy, Boons boons = null)
            => For(recipe, cleared, smithy, SpentOnTroops, boons);

        public static Squad For(LevelRecipe recipe, int cleared, int smithy, float schoolShare,
                                Boons boons = null)
            => For(recipe, cleared, smithy, schoolShare, Wanted(cleared), boons);

        public static Squad For(LevelRecipe recipe, int cleared, int smithy, float schoolShare,
                                TroopKind[] line, Boons boons = null)
        {
            int points = recipe.SquadBudget + (boons?.ExtraSquadPoints ?? 0);
            int posts = recipe.Posts + (boons?.ExtraPosts ?? 0);

            // <b>With what it has bought, which it never used to have.</b> Squad.School is
            // the permanent side of the smithy and was left empty here, so every gate in
            // the game measured a player who had never been to the shop. See School.
            var squad = new Squad(points, posts) { School = School(cleared, schoolShare, line) };

            foreach (var kind in line)
                squad.TryPlace(kind);

            // Whatever points and posts are left over go to spears, which is what a player
            // with four points spare and an empty post does.
            while (squad.TryPlace(TroopKind.Spearmen)) { }

            foreach (var group in squad.Slots)
            {
                if (group == null) continue;

                group.WeaponLevel = smithy;
                group.ArmourLevel = smithy;

                // Raised after the levels are set, or a group starts the run already
                // wounded by exactly the armour it just bought.
                group.Hp = group.EffectiveMaxHp;
            }

            return squad;
        }

        /// <summary>
        /// Share of the gold the player who has invested in their troops puts into them:
        /// all of it, and none on boons. What "upgraded far enough" means when a level is
        /// promised to be winnable by anybody who has - see LevelMaps.EveryRoadWinnable.
        /// </summary>
        public const float AllOnTroops = 1f;

        /// <summary>
        /// A run of this level down one road, played the way it is played: the escort
        /// walks in with nothing from the field smithy and buys it with the silver the
        /// fighting pays, as it pays it. See FieldSmith for why this replaced handing the
        /// squad its smithy levels at the start.
        ///
        /// <paramref name="schoolShare"/> is how much of the gold went on permanent troop
        /// upgrades between levels: <see cref="SpentOnTroops"/> for the player the curve
        /// assumes, <see cref="AllOnTroops"/> for the one who put everything into the line.
        /// </summary>
        public static LevelRun Play(LevelMap map, IReadOnlyList<int> route, LevelRecipe recipe,
                                    int cleared, float schoolShare = SpentOnTroops)
        {
            var squad = For(recipe, cleared, 0, schoolShare);
            return new LevelRun(map, route, squad, recipe.EnemyStrength) { Shops = true };
        }

        /// <summary>
        /// Whether a prepared player gets down this road: the right troops for it, all
        /// their gold put into those troops, and the run's silver spent at the field
        /// smithy as it comes in.
        ///
        /// <b>"The right troops" is a choice, so it is made.</b> The line the curve
        /// assumes is one compromise between holding, shooting and mending, and a road
        /// built to wear an escort down may want a different one. A player who reads the
        /// road takes a wall of shields and a priest down a gauntlet, and horse down a
        /// road where one heavy fight has to be ended quickly. So the lines in
        /// <see cref="Lines"/> are all tried, each with the school bought for its own
        /// troops, and the road counts as won if any of them wins it.
        /// </summary>
        public static bool Prepared(LevelMap map, IReadOnlyList<int> route, LevelRecipe recipe,
                                    int cleared, float enemyScale = 1f)
            => PreparedLine(map, route, recipe, cleared, enemyScale) >= 0;

        /// <summary>Which of <see cref="Lines"/> gets down the road first, or -1 for none.</summary>
        public static int PreparedLine(LevelMap map, IReadOnlyList<int> route, LevelRecipe recipe,
                                       int cleared, float enemyScale = 1f)
        {
            var lines = Lines(cleared);

            // Deep first: the prepared player is the one who has thought about the forge.
            // Wide as well, because on a road of many small fights spreading the armour
            // can be the right answer, and a player who reads the road knows that too.
            foreach (bool deep in new[] { true, false })
                for (int i = 0; i < lines.Length; i++)
                {
                    var squad = For(recipe, cleared, 0, AllOnTroops, lines[i]);
                    var run = new LevelRun(map, route, squad, recipe.EnemyStrength * enemyScale)
                    {
                        Shops = true,
                        ShopsDeep = deep
                    };

                    if (run.RunToCompletion() == RunOutcome.Arrived) return i;
                }

            return -1;
        }

        /// <summary>
        /// The lines a player who reads the road chooses between, in the order they are
        /// tried, each filled from the front until the points run out and topped up with
        /// spears.
        ///
        /// The curve's own line first; then a wall - a shieldbearer to take the blows and
        /// a priest behind it, for a road of many fights with no breath between; then
        /// weight - the heaviest horse the player has earned and swords beside it, for a
        /// road of few fights that have to be finished; then shot behind a shield, for
        /// open ground where the enemy can be thinned before it arrives.
        /// </summary>
        public static TroopKind[][] Lines(int cleared)
        {
            var shot = Best(cleared, TroopKind.Crossbowmen, TroopKind.Archers);
            var horse = Best(cleared, TroopKind.Knights, TroopKind.NobleCavalry,
                             TroopKind.HeavyCavalry, TroopKind.Cavalry);

            return new[]
            {
                Wanted(cleared),
                new[] { TroopKind.Shieldbearer, TroopKind.Priest, shot, shot,
                        TroopKind.Spearmen, TroopKind.Spearmen },
                new[] { horse, TroopKind.Swordsmen, shot, TroopKind.Swordsmen,
                        TroopKind.Spearmen, TroopKind.Spearmen },
                new[] { TroopKind.Shieldbearer, shot, shot, shot,
                        TroopKind.Priest, TroopKind.Spearmen }
            };
        }

        /// <summary>
        /// The line, in the order the points are spent on it.
        ///
        /// <b>The line the tests have always fielded, and that is the point.</b> Two
        /// attempts to improve on it both came out worse. Leading with the shieldbearer
        /// and the priest lost roads on eleven levels of thirty — twelve damage a second
        /// and none at all, out of a budget running from twelve to twenty-six, is most of
        /// a line that cannot end a fight. Leading with the heaviest horse was worse still
        /// and for a duller reason: a knight is ten points of a twenty-six point budget,
        /// and the two spearmen it displaces are 960 health and double damage against
        /// exactly the thing a chapter's champion is.
        ///
        /// So the composition is left alone and the <i>growth</i> is what this models:
        /// the crossbow once it is earned, and the smithy behind it. That is also what
        /// makes it a fair reading of the gates — it is the escort they have been using,
        /// carried forward the way a player carries one, rather than a different escort
        /// that happens to pass.
        /// </summary>
        static TroopKind[] Wanted(int cleared)
        {
            var shot = Best(cleared, TroopKind.Crossbowmen, TroopKind.Archers);

            return new[]
            {
                TroopKind.Spearmen, TroopKind.Spearmen, shot, shot,
                TroopKind.Swordsmen, TroopKind.Spearmen
            };
        }

        /// <summary>The first of these the player has earned.</summary>
        static TroopKind Best(int cleared, params TroopKind[] ladder)
        {
            foreach (var kind in ladder)
                if (cleared >= TroopTable.LevelsToUnlock(kind)) return kind;

            return ladder[ladder.Length - 1];
        }
    }
}
