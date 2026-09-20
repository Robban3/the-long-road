using System;
using System.Collections.Generic;
using TheVeil.Sim;

namespace TheVeil.Gen
{
    /// <summary>
    /// Distributes enemies, traps and silver across the ground a route can be drawn
    /// through (docs/content-pipeline.md §3, steps 5–6b).
    ///
    /// The old rule spent the budget along the three corridors, in inverse proportion
    /// to their travel time. That was right while those three were the only routes on
    /// offer. The player draws the line now, and a line drawn between the corridors
    /// would have met nothing at all — so threat lives on the whole band instead, and
    /// the rule survives restated per tile: cover first, then speed. The wood carries
    /// the most and the road close behind it; the fen and the pass carry least, because
    /// the long slow way round has to be the quiet one for the detour to be a decision
    /// rather than a doodle. Taking it costs time, and time is the third star.
    ///
    /// What placement alone cannot promise is that the player meets anything, and that
    /// promise is the difference between a level and a walk. Three things buy it:
    ///
    /// 1. <b>The fords are guarded.</b> The river runs across the caravan's travel and
    ///    can only be crossed at its fords, so a group on each is a fight no drawn line
    ///    avoids.
    /// 2. <b>Each group watches a stretch.</b> Twelve groups cannot seal fifty tiles of
    ///    width standing on twelve tiles; watching a territory each, they can.
    /// 3. <b>The placer checks its own work.</b> It samples routes a player might draw
    ///    and moves — never adds — a group onto any route that met too little. Adding
    ///    was tried and broke the budget ceiling that the difficulty curve rests on:
    ///    chapter 1 came out between 13 and 71 percent over budget, and §6 of the
    ///    status notes records what happens when that budget lands on less ground.
    ///
    /// Measured over chapter 1 against forty routes the placer had never seen: no route
    /// met fewer than three groups, the average was between five and six, and every
    /// level stayed inside its budget.
    /// </summary>
    public static class EncounterPlacer
    {
        /// <summary>
        /// How far past the fastest crossing a detour may run before it stops being a
        /// route anyone would draw. Wide enough to keep the whole map in play, tight
        /// enough not to spend the budget in the corners.
        /// </summary>
        public const float BandSlack = 1.6f;

        /// <summary>Travel cost kept clear at both ends, so nothing waits in the first strides.</summary>
        public const float SafeEndCost = 8f;

        /// <summary>
        /// The same rule in tiles, across the ground rather than along a path.
        ///
        /// Both are needed. Cost alone lets a group stand four tiles from the start on a
        /// road, because eight of cost is two and a half tiles at ×1.25; distance alone
        /// would let one sit just behind a ridge that takes half a minute to walk round.
        /// </summary>
        public const int SafeEndTiles = 5;

        /// <summary>
        /// The same rule again, plus the ground a group watches from where it stands.
        ///
        /// <b>Keeping a group five tiles off the start does not keep its arrows off
        /// it.</b> Territory was added to the placer after this rule was written - a group
        /// holds a stretch of country and wakes at its edge, and the smallest stretch any
        /// group holds is <see cref="TerritoryMinTiles"/>, six. So a bandit archer parked
        /// six tiles from the start line is legal by the old rule and is shooting at the
        /// caravan before it has moved.
        ///
        /// Found on 1-5, and it does not end the level with a fight, it ends it with
        /// nothing: the column halts in the first stride, never re-forms, and the stall
        /// watch closes the run at two seconds with three troops still standing, nothing
        /// killed and nothing earned. Three combat tests caught it, all of them saying "a
        /// full level of fighting earned nothing", and the fault had been reachable since
        /// the day territory was invented.
        ///
        /// Eleven tiles - five, plus the six a group watches at its tightest.
        /// </summary>
        public const int SafeEndReachTiles = SafeEndTiles + (int)TerritoryMinTiles;

        public const float GroupSpacingTiles = 5f;

        /// <summary>
        /// Tiles between traps. Two rather than three, because a throat has to be laid
        /// *across* — one trap in a five-tile gap is a trap you walk round.
        /// </summary>
        public const float TrapSpacingTiles = 2f;

        /// <summary>
        /// How much detour, as a share of the fastest crossing, still counts as a way
        /// through when measuring how wide the country is at a given depth.
        ///
        /// Eight percent. A tile costing more than that to route through is not another
        /// way past a chokepoint, it is the long way round the level.
        /// </summary>
        public const float ThroatSlack = 0.08f;

        /// <summary>
        /// The widest throat, in tiles, still worth laying a line of traps across.
        ///
        /// Five. At <see cref="TrapSpacingTiles"/> of two that is three traps, which is
        /// what a level's whole throat allowance runs to — so five is both the widest gap
        /// the budget can close and the widest one worth trying on. Anything broader gets
        /// a single trap on its best tile and the allowance moves on.
        /// </summary>
        public const int Closeable = 5;

        /// <summary>
        /// Slices the crossing is cut into when looking for its narrow points.
        ///
        /// Forty-eight over a route of sixty to a hundred tiles is a slice every tile or
        /// two — fine enough to find a ford, coarse enough that one tile of noise does
        /// not read as a chokepoint.
        /// </summary>
        public const int ThroatSlices = 48;

        /// <summary>
        /// Share of the budget spent on traps before the recipe's density scales it.
        ///
        /// 0.18, down from 0.25, and the reason is a familiar one: **the share was tuned
        /// against a placement that could not spend it.** The old scatter competed for
        /// tiles with everything else in one occupancy set and at three tiles' spacing,
        /// so it routinely ran out of legal ground before it ran out of allowance. Laying
        /// them at the throats, on their own occupancy, at two tiles, spends the lot —
        /// and the same number therefore buys noticeably more trap and less enemy than it
        /// used to. Chapter 2 started shipping levels that could not put five groups on
        /// every drawn route.
        ///
        /// A constant tuned as a product of two things breaks silently when either moves.
        /// This is the third time that has happened here.
        ///
        /// <b>0.09, half of that, and this time for what it looks like.</b> Every trap has
        /// its heap of bones beside it now, drawn on the planning map as a skull, and at
        /// eighteen per cent chapter one ran from three traps on its first level to twelve
        /// on its last - five of them at one crossing on 1-10. Seen on the plan, that is
        /// not a warning, it is wallpaper: a player cannot weigh twelve skulls, and a
        /// skull that does not make somebody stop and think is not doing its job. Halved,
        /// the chapter runs two to six.
        ///
        /// The points do not go away. What the traps no longer spend is spent on the
        /// groups, so a level is as dangerous as it was; less of the danger is hidden in
        /// the ground and more of it is walking about where the crows can see it.
        /// </summary>
        public const float TrapBudgetShare = 0.09f;

        /// <summary>
        /// The fewest traps a level may have, whatever its share of the budget buys.
        ///
        /// Two. Halving the share took the first level of the game down to one, and one
        /// skull on a map teaches nothing: a player who sees a single heap of bones and
        /// drives past it has learned that bones are scenery. Two is the least that makes
        /// it a pattern - here, and there, and both times something was waiting - which is
        /// what the first levels of a chapter are for.
        ///
        /// Paid for from the level's budget like any other trap, so the level is no
        /// harder for it; a little less of the danger is standing about and a little more
        /// is in the ground.
        /// </summary>
        public const int MinTraps = 2;

        /// <summary>
        /// The share of the trap allowance laid at the crossing's narrow points. The
        /// rest is strewn over the band by terrain, as all of it used to be.
        ///
        /// Two thirds, and both parts earn their place.
        ///
        /// **All-scattered was what there was**, and it does not work: a trap has a
        /// three-metre trigger and no territory, so on a band of three thousand tiles it
        /// is scenery. Level 1-8 laid fourteen and a run down its fast corridor revealed
        /// two and fired none.
        ///
        /// **All-at-the-throats works and reads as placed.** Every trap on ground every
        /// route crosses is a level that has been *arranged*, and a player learns within
        /// three levels that the ford is always mined — which turns a hazard into a
        /// checklist. The scattered third is what keeps that from being a rule: some
        /// traps are simply out there, in the marsh and the woods, and the ones you
        /// stumble on are the ones that make you keep a scout.
        /// </summary>
        public const float ThroatShare = 0.65f;

        /// <summary>
        /// Reach of a group with no territory of its own, in tiles. The widest detect
        /// radius in <see cref="EnemyTable"/> is 22 m — five and a half tiles — and four
        /// is the range at which a group will certainly close.
        /// </summary>
        public const float EngageRadiusTiles = 4f;

        public const float TerritoryMinTiles = 6f;
        public const float TerritoryMaxTiles = 13f;

        /// <summary>
        /// The least any route a player draws may meet, whichever way they go.
        ///
        /// <b>Two, and it used to be five.</b> Five on every route was a floor applied to
        /// all three roads alike, with a repair loop that moved groups onto whichever one
        /// met fewest - so the placer spent its effort making the three roads the same.
        /// Measured over chapter one it succeeded: every road of every level met six to
        /// eight groups for thirty to fifty points, and the only thing left that told them
        /// apart was travel time. The long way round 1-1 took a hundred and seventy-two
        /// against seventy-three and met exactly as much. That is not a choice, it is a
        /// worse option.
        ///
        /// docs/GDD.md says the opposite in as many words. Section 1: the core tension is
        /// speed against safety. Section 6.2: the dangerous route pays - more enemies,
        /// more silver, a stronger army at the end and broken wagons; the safe route
        /// arrives whole and poor. And the consequence it draws for the generator is that
        /// the silver *per corridor* has to be validated, which only means anything if the
        /// corridors differ.
        ///
        /// So the roads are allowed to differ, and this is no longer a target, it is a
        /// floor: whatever line is drawn, something is on it. What each road owes on top
        /// of that is <see cref="RoadShare"/>.
        /// </summary>
        public const int MinEncounters = 2;

        /// <summary>
        /// The share of a level's threat each road carries, by <see cref="CorridorKind"/>.
        ///
        /// Half on the quick road, a third on the middle one, a fifth on the long way
        /// round. The quick road is dangerous because it is *predictable* - it is where a
        /// caravan is expected to be, which is what the terrain table already says about
        /// roads and why the GDD notes that bandits patrol them. Nobody lies in wait in a
        /// fen for three hours, so the slog is quiet, and quiet is what the slog is for.
        ///
        /// <b>Allocated by ground rather than by route, which is what the old rule could
        /// not do.</b> The budget was once spent along the three corridors in inverse
        /// proportion to their travel time, and that was dropped for a real reason: the
        /// player draws their own line, and a line drawn between two corridors would have
        /// met nothing at all. The replacement put threat on the whole band and restated
        /// the rule per tile as "cover first, then speed" - but cover means woods, woods
        /// are slow, and the quick road is the one that avoids them. So the restatement
        /// inverted the rule it was meant to preserve, and the measurement above is what
        /// that looked like.
        ///
        /// Every tile of the band belongs to whichever road is nearest it, and each road
        /// spends its share on its own ground. A line drawn between two roads crosses
        /// tiles belonging to both and meets a mixture of the two - so nothing is empty,
        /// and the roads still differ.
        /// </summary>
        public static readonly float[] RoadShare = { 0.50f, 0.30f, 0.20f };

        /// <summary>
        /// How far off its shares a level may be and still be left alone.
        ///
        /// A tenth, summed over the three roads, which is about three points of share
        /// each. Closer than that is chasing a number the player cannot feel, and every
        /// step costs a group moved out of the cover the placer chose for it.
        /// </summary>
        public const float ShareTolerance = 0.10f;

        /// <summary>
        /// How many groups each road buys with its share, by <see cref="CorridorKind"/>.
        ///
        /// Thirteen between them, which is where a hundred levels measured say the
        /// placement holds - every level that failed its promise had ten or fewer, and
        /// eighty-eight of the ninety-three that kept it had eleven or more.
        ///
        /// <b>The split is not the shares.</b> Seven groups out of half the budget is a
        /// seventh of it each; two groups out of a fifth is a tenth each. So the long road
        /// is not simply quieter, it is quieter and worse: fewer fights, and the ones it
        /// has are heavier than anything on the quick road. A worn squad takes the long
        /// way for the number of fights, not for their size.
        /// </summary>
        public static readonly int[] RoadGroups = { 7, 4, 2 };

        /// <summary>
        /// How far from its own road a group may drift before it stops counting as that
        /// road's, in tiles.
        ///
        /// <b>Owning a tile is not the same as being met on it, and the difference was
        /// most of the allocation.</b> Measured on 1-1: the quick road had eleven groups
        /// laid on its own country and a caravan down it ran into six, while the long way
        /// round had two laid on its country and ran into six - three times what it was
        /// given - because it is two and a half times longer and wanders through everybody
        /// else's ground on the way.
        ///
        /// The ownership is a partition of the whole map, so a tile twenty tiles off the
        /// quick road still belongs to it, and a group put there is one the quick road
        /// never passes. Five tiles - twenty metres - is about the width of country a
        /// drawn line actually sweeps, so a group within it is one that road will meet.
        ///
        /// A falloff rather than a cutoff, because the ground between the roads has to
        /// carry something: a line drawn down the middle of nowhere should still find
        /// somebody, which is the whole reason threat was taken off the corridors in the
        /// first place.
        /// </summary>
        public const float RoadReach = 5f;

        /// <summary>
        /// Metres of road between two fights the same road runs into.
        ///
        /// <b>Because spacing was being measured as the crow flies.</b>
        /// <see cref="GroupSpacingTiles"/> keeps groups five tiles apart in a straight
        /// line, which is twenty metres - and <see cref="EngageRadiusTiles"/> is four, so
        /// a group wakes at sixteen. Two groups laid at the legal minimum are therefore
        /// inside each other's reach along the road, and what the caravan meets is not
        /// two fights but one fight against both.
        ///
        /// Measured over all thirty levels before this existed: the fast road ran a fight
        /// every 34 m with a middling gap of 20 m, 242 of its 295 gaps under 40 m, and
        /// played through it arrived on 15 levels of 30 against the safe road's 29. The
        /// shares were not the fault - half the threat on the fast road is what was
        /// agreed - the packing was.
        ///
        /// Forty-five metres: two engagements and a little, so a squad that has just
        /// fought is out of contact before the next group notices it. Along the road,
        /// not across the map, because the caravan drives one road and two groups either
        /// side of a hill never meet each other.
        /// </summary>
        public const float RoadGapMetres = 45f;


        /// <summary>
        /// What the repair loop aims at, which is one more than the promise.
        ///
        /// The loop can only measure the routes it sampled, and a player draws whatever
        /// they like. Repaired to exactly the promise, the sampled routes all met five
        /// and the ones nobody sampled met four — measured over sixty fresh routes on
        /// six levels, every one came in a group short. Aimed one above, the same
        /// measurement returns five and six. The margin is what the sampling costs; it
        /// is not slack, and <see cref="EncounterLayout.EncountersValidated"/> still
        /// records the promise rather than the target.
        /// </summary>
        public const int RepairTarget = MinEncounters + 1;

        /// <summary>
        /// Routes drawn to check the placement against.
        ///
        /// Named apart from the method that uses it because C# will not have a constant
        /// and a method sharing a name in one type — which is how this went unbuilt for
        /// a while: nothing here runs without an editor, so the clash only surfaced when
        /// one was opened.
        /// </summary>
        // Sixty-four, up from thirty-two, and the reason is the corridors moving apart.
        //
        // The loop can only repair the routes it sampled, and a player draws whatever
        // they like; the margin in RepairTarget is what covers the difference. That
        // margin was measured when the fast and cautious corridors ran within a few
        // tiles of each other — on some levels along the identical line — so the space
        // of routes a player might draw was narrow and thirty-two samples covered it.
        // Now that the cautious route is pushed off the fast one, the space is far
        // wider, and 1-4 came out reporting the promise kept while a freshly drawn route
        // met three groups of the five owed. Twice the samples closes it; raising the
        // repair target instead does not, which says the problem was seeing rather than
        // fixing.
        public const int RouteSamples = 64;
        public const int MaxRepairs = 12;

        /// <summary>
        /// Donors the loop may try per repair it keeps. A rejected move costs an
        /// attempt and leaves the layout as it was, so without this the cap would be
        /// spent on candidates rather than on repairs.
        /// </summary>
        const int RepairAttempts = 4;

        /// <summary>Landing spots offered per donor, emptiest first.</summary>
        const int RepairTargets = 3;

        public static EncounterLayout Place(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                            LevelRecipe recipe, DeterministicRandom rng,
                                            int startIndex, int goalIndex)
        {
            var layout = new EncounterLayout();
            if (corridors == null || corridors.Count == 0 || startIndex < 0 || goalIndex < 0)
                return layout;

            var band = ThreatBand.Build(grid, startIndex, goalIndex, corridors);
            if (band == null) return layout;

            layout.BandTiles = band.Tiles.Count;

            var occupied = new HashSet<int>();
            int budget = recipe.EnemyBudget;

            budget -= GuardTheFords(grid, band, recipe, rng, layout, occupied, budget);

            // Traps keep their own occupancy, and this is not tidiness.
            //
            // Sharing one set meant a trap reserved ground against *groups* as well, at
            // the group's spacing of five tiles — and now that traps go to the throats,
            // that is a five-tile hole punched in the enemy placement at exactly the
            // tiles every route converges on. Measured straight after the throat change:
            // 1-1 fell to two groups on a drawn route against a promise of five, and the
            // repair loop could not put it back because the ground it wanted was
            // reserved by a pit.
            //
            // A group standing on a trap is the one overlap worth refusing, and traps go
            // down first, so groups still check the trap tiles themselves.
            var mined = new HashSet<int>();

            budget -= LayTraps(grid, band, corridors, recipe, rng, layout, mined, budget);

            // The three roads as rulers, made here so that everything which puts a group
            // down or moves one measures against the same marks. See RoadGapMetres.
            var lines = Rulers(grid, corridors, layout);

            ScatterEnemies(grid, band, corridors, recipe, rng, layout, occupied, mined,
                           budget, lines);

            AssignTerritories(grid, layout);
            TallySilver(layout, recipe);
            VerifyAndRepair(grid, band, corridors, recipe, rng, layout, occupied,
                            startIndex, goalIndex, lines);

            // After everything, so everything above is untouched by it. It moves a trap
            // rather than adding one, so the points and the silver are what they were and
            // nothing needs counting again. See MineARoute.
            MineARoute(grid, corridors, layout, mined, startIndex, goalIndex);

            // Last of all, and every word of that is load-bearing.
            //
            // The stand at the goal must not be able to move the road. It has its own
            // purse (Champions.Purse), its own dice, and now its own place in the order —
            // after the scatter, after the repair pass, after everything the generator
            // reads when it decides whether to keep a map. Placed earlier it changed the
            // ground it stood on: 3-10 came out with 2,644 tiles of different terrain, a
            // fastest road a quarter slower, and a worst drawn route that met two groups
            // instead of six. The escort lost two roads it had been winning, with the
            // champion untouched at the goal — the level under it had been replaced.
            //
            // Chapters one and two came right by taking the goal out of the numbers the
            // generator reads; chapter three only came right when it was taken out of the
            // order as well. The lesson is the cheaper one to write down than to find:
            // anything added to a generated level must be added where it cannot be an
            // input to the generation.
            GuardTheGoal(grid, corridors, recipe, new DeterministicRandom(goalIndex * 31 + grid.Width),
                         layout, occupied, recipe.GoalBudget, goalIndex);

            // Both are pure recounts over the finished lists, so the goal's own territory
            // and its silver are right without anything upstream being asked again. See
            // AssignTerritories for why the posted men are no neighbour to anybody.
            AssignTerritories(grid, layout);
            TallySilver(layout, recipe);

            return layout;
        }

        // --- The band ---------------------------------------------------------------

        /// <summary>
        /// Every tile a sane crossing could pass through, and what it is worth
        /// threatening. Two travel fields — one from the start, one from the goal — are
        /// what let the placer reason about every route at once instead of about three.
        /// </summary>
        sealed class ThreatBand
        {
            public readonly List<int> Tiles = new List<int>();
            public float[] Weight;
            public float[] FromStart;
            public float[] FromGoal;
            public float Fastest;

            /// <summary>
            /// Which road each tile belongs to, as a <see cref="CorridorKind"/>, or -1
            /// where no road reaches it.
            ///
            /// This is what carries <see cref="RoadShare"/> onto the ground. See
            /// <see cref="Roadside"/>.
            /// </summary>
            public int[] Road;

            /// <summary>How far each tile is from the road that owns it, in tiles.</summary>
            public int[] FromRoad;

            public static ThreatBand Build(TileGrid grid, int startIndex, int goalIndex,
                                           IReadOnlyList<Corridor> corridors)
            {
                grid.ToCoords(startIndex, out int sx, out int sy);
                grid.ToCoords(goalIndex, out int gx, out int gy);

                var band = new ThreatBand
                {
                    FromStart = TravelField(grid, sx, sy),
                    FromGoal = TravelField(grid, gx, gy),
                    Weight = new float[grid.TileCount]
                };

                band.Road = Roadside(grid, corridors, out int[] fromRoad);
                band.FromRoad = fromRoad;

                band.Fastest = band.FromStart[goalIndex];
                if (float.IsInfinity(band.Fastest) || band.Fastest <= 0f) return null;

                float limit = band.Fastest * BandSlack;

                for (int i = 0; i < grid.TileCount; i++)
                {
                    float total = band.FromStart[i] + band.FromGoal[i];
                    if (float.IsInfinity(total) || total > limit) continue;
                    if (band.FromStart[i] < SafeEndCost || band.FromGoal[i] < SafeEndCost) continue;

                    // And in a straight line as well as in travel cost. The two are not
                    // the same thing and only the second was checked: eight of cost is
                    // two and a half tiles on a road, so a group could stand four tiles
                    // from the start and satisfy it. That is inside the distance the
                    // rule is written in — being ambushed before the caravan has moved
                    // is not a decision the player could have made differently — and it
                    // held only by luck of ordering until the trap change disturbed it.
                    if (Near(grid, i, startIndex) || Near(grid, i, goalIndex)) continue;

                    if (TerrainTable.Speed(grid[i]) <= 0f) continue;

                    // Threat follows cover *and* speed, and the shape of that is the
                    // whole balance of the level.
                    //
                    // Cover cubed, because the table's own range is too narrow to choose
                    // with: ambush weight runs from 0.8 on the plain to 1.5 in the
                    // forest, so a weighted draw preferred cover by less than two to one
                    // — barely a lean. It went unnoticed while the corridors ran close
                    // together and the ground a route could reach was mostly the same
                    // ground; once they spread across the map the reachable country grew
                    // more varied than the placer's picks, and the enemies ended up in
                    // cover *thinner* than the average the routes crossed. Cubing turns
                    // the same ordering into a six-to-one preference.
                    //
                    // Times the *square root* of speed, because the long way round has
                    // to be the quiet way or there is no decision to make. The player
                    // draws the line; what makes that a choice rather than a doodle is
                    // that the quick crossing is dangerous and the detour is safe but
                    // late — and late is a star, by way of the par time in LevelRun.
                    //
                    // **This is not the multiplication that was tried and reverted.** That
                    // one was `speed * ambush` on the raw table: forest 1.05 against plain
                    // 0.80, a lean of 1.3 to 1, and groups drifted out of the cover the
                    // table sends them to. With cover cubed first the lean stays five to
                    // one and speed only sorts what is left.
                    //
                    // The root rather than speed itself, and the test chose it, not taste.
                    // At full speed the weights come out forest 2.36, road 2.16, ford
                    // 1.10, fen 0.45 — and level 2-3 could no longer put five groups on
                    // every drawn route, which is the one promise this class exists to
                    // keep. Rooted it is forest 2.82, road 1.93, ford 1.55, plain 0.51,
                    // fen 0.67, pass 0.56, and every level keeps its promise. The fen
                    // still loses a third of what it carried and the road still gains, so
                    // the trade is there; it is simply not steep enough to strip the
                    // middle of the map bare.
                    float ambush = TerrainTable.AmbushWeight(grid[i]);

                    band.Tiles.Add(i);
                    band.Weight[i] = MathF.Sqrt(TerrainTable.Speed(grid[i])) * ambush * ambush * ambush;
                }

                // Weighting the flanks up from here was tried and reverted, and the
                // reason is worth keeping.
                //
                // The band is a lens, so its flanks hold a fraction of the tiles its
                // waist does and draw a proportional fraction of the threat — which is
                // why hugging an edge crosses country that is nearly empty. Multiplying
                // the flanks' weight fixes that and costs the promise this whole class
                // exists to keep: the budget is a fixed number of groups, so weight moved
                // out to the sides comes off the waist, and the waist is where every
                // route has to pass. Level 2-5 stopped being able to put five groups on
                // every drawn route at a multiplier of 1.15 — the gentlest one worth
                // trying — and at 1.35.
                //
                // Filling the flanks therefore needs a bigger budget, not a rearranged
                // one, and that is a difficulty change rather than a placement fix.
                return band.Tiles.Count == 0 ? null : band;
            }

            public bool Contains(int tile) => Weight[tile] > 0f;

            /// <summary>
            /// Whether a tile is within <see cref="SafeEndTiles"/> of one of the ends,
            /// measured across the ground rather than along a path.
            /// </summary>
            static bool Near(TileGrid grid, int tile, int end)
            {
                grid.ToCoords(tile, out int x, out int y);
                grid.ToCoords(end, out int ex, out int ey);

                int dx = x - ex;
                int dy = y - ey;

                return dx * dx + dy * dy < SafeEndReachTiles * SafeEndReachTiles;
            }
        }

        /// <summary>
        /// Cheapest travel cost from one tile to every other, over the same eight
        /// neighbours and the same costs the pathfinder uses.
        /// </summary>
        /// <summary>
        /// Which road owns each tile: a breadth-first sweep out from all three at once,
        /// nearest wins.
        ///
        /// Sweeping from every road together rather than one at a time is what makes the
        /// answer a partition - each tile is reached first by exactly one road, and the
        /// boundaries fall halfway between them without anybody working out where halfway
        /// is. The roads are seeded in share order, so a tile two roads reach in the same
        /// number of steps goes to the busier one.
        ///
        /// Steps rather than travel cost, deliberately. The question is which road a line
        /// drawn near here would be following, and that is about how close the line is,
        /// not about how long the ground takes to walk.
        /// </summary>
        /// <summary>Which road owns each tile, for the report that measures this.</summary>
        public static int[] RoadsideOf(TileGrid grid, IReadOnlyList<Corridor> corridors)
            => Roadside(grid, corridors, out _);

        static int[] Roadside(TileGrid grid, IReadOnlyList<Corridor> corridors, out int[] fromRoad)
        {
            var road = new int[grid.TileCount];
            var reach = new int[grid.TileCount];

            for (int i = 0; i < road.Length; i++) { road[i] = -1; reach[i] = int.MaxValue; }

            fromRoad = reach;
            if (corridors == null) return road;

            var queue = new Queue<int>();

            for (int kind = 0; kind < RoadShare.Length; kind++)
                foreach (var corridor in corridors)
                {
                    if ((int)corridor.Kind != kind) continue;

                    foreach (int tile in corridor.Tiles)
                    {
                        if (road[tile] >= 0) continue;

                        road[tile] = kind;
                        reach[tile] = 0;
                        queue.Enqueue(tile);
                    }
                }

            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                grid.ToCoords(at, out int x, out int y);

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!grid.InBounds(nx, ny)) continue;

                    int next = grid.ToIndex(nx, ny);
                    if (road[next] >= 0) continue;
                    if (TerrainTable.Speed(grid[next]) <= 0f) continue;

                    road[next] = road[at];
                    reach[next] = reach[at] + 1;
                    queue.Enqueue(next);
                }
            }

            return road;
        }

        static float[] TravelField(TileGrid grid, int x, int y)
        {
            int n = grid.TileCount;
            var distance = new float[n];
            var settled = new bool[n];
            for (int i = 0; i < n; i++) distance[i] = float.PositiveInfinity;

            if (!grid.IsPassable(x, y)) return distance;

            int source = grid.ToIndex(x, y);
            distance[source] = 0f;

            var queue = new SortedSet<(float cost, int tile)> { (0f, source) };

            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);
                if (settled[current.tile]) continue;
                settled[current.tile] = true;

                grid.ToCoords(current.tile, out int cx, out int cy);

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Neighbours.DX[d];
                    int ny = cy + Neighbours.DY[d];
                    if (!grid.IsPassable(nx, ny)) continue;

                    if (d >= 4)
                    {
                        if (!grid.IsPassable(cx + Neighbours.DX[d], cy)) continue;
                        if (!grid.IsPassable(cx, cy + Neighbours.DY[d])) continue;
                    }

                    int neighbour = grid.ToIndex(nx, ny);
                    if (settled[neighbour]) continue;

                    float step = TerrainTable.TravelCost(grid[neighbour]);
                    if (d >= 4) step *= 1.41421356f;

                    float candidate = current.cost + step;
                    if (candidate >= distance[neighbour]) continue;

                    distance[neighbour] = candidate;
                    queue.Add((candidate, neighbour));
                }
            }

            return distance;
        }

        static class Neighbours
        {
            public static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
            public static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        }

        // --- Placement --------------------------------------------------------------

        /// <summary>
        /// A group on every ford in the band. The river can only be crossed at its
        /// fords, so this is the one placement no drawn route can walk around —
        /// everything else in this file is a probability, and this is the floor.
        /// </summary>
        static int GuardTheFords(TileGrid grid, ThreatBand band, LevelRecipe recipe,
                                 DeterministicRandom rng, EncounterLayout layout,
                                 HashSet<int> occupied, int budget)
        {
            int spent = 0;

            foreach (var crossing in FordCrossings(grid, band))
            {
                if (spent >= budget) break;

                // The middle of the crossing, so the guard stands on the ford rather
                // than at the water's edge where a route can slip past it.
                int tile = crossing[crossing.Count / 2];
                if (occupied.Contains(tile)) continue;

                // A guard is bought outright. The reserve that keeps a road's share
                // buying enough groups belongs to that road's own pass - see Sow - and a
                // ford guard is not on a road, it is on the water every road crosses.
                var kind = PickAffordable(recipe.EnemyPool, rng, budget - spent, 0);
                if (kind == null) break;

                layout.Enemies.Add(new EnemySpawn
                {
                    Tile = tile,
                    Kind = kind.Value,
                    Origin = PlacementOrigin.Guard
                });
                occupied.Add(tile);
                spent += EnemyTable.Points(kind.Value);
                layout.FordGuards++;
            }

            return spent;
        }

        /// <summary>
        /// How far short of the goal the guard stands, in tiles, measured across the
        /// ground rather than along a path.
        ///
        /// <see cref="SafeEndTiles"/>, and the same number on purpose. The first draft
        /// stood him four tiles off on the reasoning that the band's rule was worth
        /// breaking once for the sake of the fight being at the goal — and that was a
        /// special case bought for nothing. At five he is outside the ring the band keeps
        /// clear, so there is no exception to write down and no test to weaken, and he is
        /// still twenty metres from the goal on the road in: well inside his own detect
        /// radius from any direction a route can arrive from, which is what "arriving
        /// means meeting him" actually requires.
        ///
        /// The <see cref="SafeEndCost"/> half of the band's rule he is still inside —
        /// five tiles of road is about four of travel cost against a floor of eight. That
        /// is the whole of the exception, it applies to the goal end only, and it is the
        /// point: the ground by the goal is quiet so that the one thing standing on it is
        /// the thing the player sees.
        /// </summary>
        const int GoalStandoff = SafeEndTiles;

        /// <summary>
        /// How far from the guard his retinue may be posted, in tiles.
        ///
        /// Two, and it was three. At three they are spread over five tiles of road with a
        /// territory and a twenty-metre notice each, which does not read as a bodyguard —
        /// it reads as a picket, and it fought like one: on 3-10 the retinue killed the
        /// caravan short of the goal with the champion still at full health and the fight
        /// the level is named for never begun. Close enough to be his, so the player meets
        /// the whole thing at once and the choice of which to answer first is a choice
        /// made in one fight rather than three.
        /// </summary>
        const int RetinueReach = 2;

        /// <summary>
        /// Stands the last fight of the road at the goal.
        ///
        /// Every level ends on something. Nine times out of ten it is the heaviest thing
        /// the level is allowed to field, which the player may still drive round; on the
        /// tenth it is the chapter's champion, who cannot be driven round and is the only
        /// thing in the game of which that is true.
        ///
        /// <b>The kind comes from the level's own pool and not from a constant.</b> The
        /// first draft posted a BanditRider at every goal, and chapter one does not unlock
        /// riders at all — so 1-1, which is meant to be wolves and nothing else, met
        /// horsemen at its goal. What waits at the end of a level has to be something the
        /// level was allowed to contain, or the unlock table is decoration.
        ///
        /// Paid out of the same budget as everything else, and taken before the scatter
        /// spends it, so a level with a champion carries less on the road behind him
        /// rather than more danger altogether.
        /// </summary>
        static int GuardTheGoal(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                LevelRecipe recipe, DeterministicRandom rng,
                                EncounterLayout layout, HashSet<int> occupied,
                                int budget, int goalIndex)
        {
            var kind = Champions.GuardKind(recipe.EnemyPool, recipe.GoalBlocks);
            if (EnemyTable.Points(kind) > budget) return 0;

            // The ring the band keeps clear, claimed before anything is posted in it, so
            // that the rule holds for the retinue as well as for the man it rides with.
            // The scatter loses nothing by this: the band excludes these tiles already.
            grid.ToCoords(goalIndex, out int ringX, out int ringY);

            for (int dy = -GoalStandoff; dy <= GoalStandoff; dy++)
                for (int dx = -GoalStandoff; dx <= GoalStandoff; dx++)
                {
                    if (ClearOf(dx, dy, GoalStandoff)) continue;

                    int nx = ringX + dx, ny = ringY + dy;
                    if (grid.InBounds(nx, ny)) occupied.Add(grid.ToIndex(nx, ny));
                }

            int post = GoalPost(grid, corridors, goalIndex, occupied);
            if (post < 0) return 0;

            layout.Enemies.Add(new EnemySpawn
            {
                Tile = post,
                Kind = kind,
                Origin = PlacementOrigin.Goal
            });

            occupied.Add(post);
            int spent = EnemyTable.Points(kind);
            layout.GoalGuards++;

            // And the men who ride with him: horsemen and bowmen, never more captains.
            // See Champions.CompanionAt for what drawing them like the guard cost.
            grid.ToCoords(post, out int px, out int py);

            for (int i = 0; i < recipe.GoalRetinue; i++)
            {
                var drawn = Champions.Companion(recipe.EnemyPool, i);
                if (!drawn.HasValue) break;

                var companion = drawn.Value;
                if (spent + EnemyTable.Points(companion) > budget) break;

                int beside = FreeNear(grid, px, py, 1, RetinueReach, occupied, rng);
                if (beside < 0) break;

                layout.Enemies.Add(new EnemySpawn
                {
                    Tile = beside,
                    Kind = companion,
                    Origin = PlacementOrigin.Goal
                });

                occupied.Add(beside);
                spent += EnemyTable.Points(companion);
                layout.GoalGuards++;
            }

            return spent;
        }

        /// <summary>
        /// The tile the guard stands on: on the fastest road in, a few tiles short of the
        /// goal, and failing that any open ground at that remove.
        /// </summary>
        static int GoalPost(TileGrid grid, IReadOnlyList<Corridor> corridors, int goalIndex,
                            HashSet<int> occupied)
        {
            grid.ToCoords(goalIndex, out int gx, out int gy);

            // The fast road, walked back from the goal. A guard on the road the player is
            // likeliest to take is a guard the player meets.
            //
            // Straight-line distance, not steps taken. Manhattan counts a diagonal as
            // two, so five of it is three and a half tiles across the ground — which put
            // a group eleven metres from the goal while satisfying a rule written in
            // sixteen. The band's own note about cost and distance is the same mistake
            // one measure along; this is that note applied here.
            foreach (var corridor in corridors)
            {
                if (corridor.Kind != CorridorKind.Fast) continue;

                for (int i = corridor.Tiles.Count - 1; i >= 0; i--)
                {
                    int tile = corridor.Tiles[i];
                    if (occupied.Contains(tile) || !grid.IsPassable(tile)) continue;

                    grid.ToCoords(tile, out int x, out int y);
                    if (!ClearOf(x - gx, y - gy, GoalStandoff)) continue;

                    return tile;
                }
            }

            // No fast corridor to walk back, so the nearest open ground at the right remove.
            return FreeNear(grid, gx, gy, GoalStandoff, GoalStandoff + 2, occupied, null);
        }

        /// <summary>Whether an offset is at least this many tiles across the ground.</summary>
        static bool ClearOf(int dx, int dy, int tiles) => dx * dx + dy * dy >= tiles * tiles;

        /// <summary>Open, unclaimed ground at a given remove from a point.</summary>
        static int FreeNear(TileGrid grid, int x, int y, int nearest, int furthest,
                            HashSet<int> occupied, DeterministicRandom rng)
        {
            var found = new List<int>();

            for (int dy = -furthest; dy <= furthest; dy++)
            {
                for (int dx = -furthest; dx <= furthest; dx++)
                {
                    if (!ClearOf(dx, dy, nearest)) continue;

                    int nx = x + dx, ny = y + dy;
                    if (!grid.InBounds(nx, ny) || !grid.IsPassable(nx, ny)) continue;

                    int tile = grid.ToIndex(nx, ny);
                    if (occupied.Contains(tile)) continue;

                    found.Add(tile);
                }
            }

            if (found.Count == 0) return -1;
            return rng == null ? found[found.Count / 2] : found[rng.Range(0, found.Count)];
        }

        /// <summary>Ford tiles grouped into crossings, one group per place the river can be forded.</summary>
        static List<List<int>> FordCrossings(TileGrid grid, ThreatBand band)
        {
            var crossings = new List<List<int>>();
            var seen = new HashSet<int>();

            foreach (int tile in band.Tiles)
            {
                if (grid[tile] != TerrainType.Ford || seen.Contains(tile)) continue;

                var group = new List<int>();
                var stack = new Stack<int>();
                stack.Push(tile);
                seen.Add(tile);

                while (stack.Count > 0)
                {
                    int current = stack.Pop();
                    group.Add(current);
                    grid.ToCoords(current, out int cx, out int cy);

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (!grid.InBounds(nx, ny)) continue;

                            int neighbour = grid.ToIndex(nx, ny);
                            if (seen.Contains(neighbour)) continue;
                            if (grid[neighbour] != TerrainType.Ford) continue;
                            if (!band.Contains(neighbour)) continue;

                            seen.Add(neighbour);
                            stack.Push(neighbour);
                        }
                    }
                }

                group.Sort();
                crossings.Add(group);
            }

            return crossings;
        }

        /// <summary>
        /// Traps at the crossing's narrow points, not scattered over the country.
        ///
        /// The old version spread them by <see cref="TerrainTable.TrapDensity"/> across
        /// the whole threat band, and a band is three thousand tiles. Level 1-8 laid
        /// fourteen; a run down its fast corridor revealed two and triggered none. A trap
        /// has a three-metre trigger and no territory — unlike a group, which comes to
        /// you — so a trap forty metres off the line the player drew is scenery. Three
        /// tests failed on that and none of them said so in those words, because a trap
        /// that never fires reads as a squad that took no damage.
        ///
        /// It also left three of the game's own answers with no question. The scout
        /// reveals traps at 10 m, the sapper disarms in 2 s, the shield-bearer absorbs
        /// the damage — "three different answers to the same problem" (docs/GDD.md §7.2)
        /// — and the marching order exists so that order 0 walks into traps first and
        /// order 3 crosses a trap field last (§4.2). None of that is reachable content
        /// while traps do not fire.
        ///
        /// So they go where the country is narrow, and narrowness is measured rather
        /// than guessed at. Both travel fields are already built: a tile's *detour* is
        /// `FromStart + FromGoal - Fastest`, which is zero on an ideal crossing and grows
        /// as you go round. Cut the crossing into slices by depth, count the tiles in
        /// each slice whose detour is inside <see cref="ThroatSlack"/>, and that count is
        /// how many ways past there are at that depth. The smallest counts are the fords,
        /// the passes and the dry line through a bog — the places every route has to
        /// share whichever line the player draws.
        ///
        /// Terrain only flavours it. `TrapDensity` picks *which* tile of a throat, and
        /// the geometry picks the throat — which is why the ford's low density in §3.1
        /// no longer keeps traps off the most unavoidable ground on the map.
        /// </summary>
        static int LayTraps(TileGrid grid, ThreatBand band, IReadOnlyList<Corridor> corridors,
                            LevelRecipe recipe, DeterministicRandom rng, EncounterLayout layout,
                            HashSet<int> occupied, int budget)
        {
            int allowance = (int)(budget * TrapBudgetShare * recipe.TrapDensity);
            if (allowance <= 0) return 0;

            int spent = 0;

            foreach (var throat in Throats(grid, band, Crossed(grid, corridors)))
            {
                if (spent >= allowance * ThroatShare) break;

                // Lay across a gap that can be closed; mine the road in a gap that cannot.
                //
                // The inner loop used to walk every tile of the throat, and laying across
                // is right — "one trap in a five-tile gap is a trap you walk round". But
                // it is only right when the gap is small enough to actually close. A
                // twelve-tile throat takes the whole allowance and still leaves eight
                // ways through, and the tiles are visited best-first, so the one tile the
                // corridors cross is mined and then three or four more are spent beside
                // it, on ground nothing drives over.
                //
                // Measured over chapter 1: the throats laid 3 to 8 traps a level and one
                // to four of them came within firing distance of any offered route. A
                // run down one corridor sprang one or two of a dozen.
                //
                // So a wide throat gets one trap — the first tile, which Score has
                // already put on the most-travelled ground — and the rest of the
                // allowance goes to the next throat. Four mined roads beat one gap
                // half-closed.
                int laid = 0;
                int allowed = throat.Ways <= Closeable ? int.MaxValue : 1;

                foreach (int tile in throat.Tiles)
                {
                    if (laid >= allowed) break;
                    if (spent >= allowance * ThroatShare) break;

                    int cost = Lay(grid, rng, layout, occupied, tile, allowance - spent,
                                   PlacementOrigin.Guard);

                    spent += cost;
                    if (cost > 0) laid++;
                }
            }

            // Its own share, not the leftovers. Handing the strew whatever the throats
            // could not spend makes the total depend on how much legal ground the throats
            // happened to have, which is how 1-7 went from three survivable routes to
            // none between one run and the next without the allowance changing at all.
            spent += Strew(grid, band, rng, layout, occupied,
                           (int)(allowance * (1f - ThroatShare)));

            // And up to the floor, where the share did not reach it. Allowed as much as
            // the dearest trap costs for each one missing, so the top-up can always afford
            // what it is asked for and never buys more than the floor.
            int missing = MinTraps - layout.Traps.Count;
            if (missing > 0)
                spent += Strew(grid, band, rng, layout, occupied,
                               missing * TrapTable.MostPoints, MinTraps);

            return spent;
        }

        /// <summary>
        /// Where along a route its trap may be laid, as a share of the journey.
        ///
        /// The middle stretch. At the start the column has not settled into the route it
        /// was drawn and the player is still looking at the map; at the very end a trap
        /// is a toll on a level already won. Between a fifth and four fifths is ground
        /// the caravan is committed to and still has somewhere to go afterwards.
        /// </summary>
        public const float RouteTrapFrom = 0.2f;
        public const float RouteTrapTo = 0.8f;

        /// <summary>Mixed into a route trap's seed so it draws from a stream of its own.</summary>
        const int RouteMineSalt = 0x5A1D;

        /// <summary>
        /// Moves one trap onto the line of one route, and says whether it did.
        ///
        /// <b>Why it exists.</b> Everything else here aims at ground <i>around</i> where a
        /// caravan goes rather than at the line it drives: the throats are where corridors
        /// converge, and the strewn third is open country. Measured on 1-1, the three
        /// traps sat 0, 28 and 48 m from the route actually driven, and the level was
        /// played to the end without one of them firing. The nearest was not near enough:
        /// a trap fires inside <see cref="TrapField.TriggerRadius"/>, three metres, and
        /// the swathe a caravan covers is eight either side — so a trap can sit squarely
        /// in the lane and still be five metres too far to go off. Not a trap avoided; a
        /// trap missed while driving over its tile.
        ///
        /// <b>One, not one per route.</b> Three trapped routes is not a choice between
        /// them, it is a toll, and the player would learn to stop reading the ground
        /// because reading it changes nothing. Which route gets it is drawn rather than
        /// fixed — always mining the fast one would teach the lesson once and then be a
        /// checklist, the objection <see cref="ThroatShare"/> already makes about fords.
        ///
        /// <b>Moved, not added — and moved last.</b> The first version laid a new trap at
        /// the head of <see cref="LayTraps"/>, drawing from the shared generator and
        /// spending from the shared allowance, so every trap, group and repair after it
        /// moved on every map; and since which of up to twelve terrain attempts a level
        /// keeps depends on where its groups stand, it could swap the landscape too. 1-5's
        /// fast route stopped meeting anything and two tests went red. The second added
        /// it after everything, outside the budget, and 1-2 came out 123 points of 120 —
        /// the ceiling that exists because repairs which once <i>added</i> put chapter 1
        /// as much as 71 percent over.
        ///
        /// Moving one of the traps already placed keeps the count and the cost exactly as
        /// they were, so the ceiling cannot be crossed by construction, and doing it after
        /// everything else leaves every draw upstream where it was. The one moved is the
        /// trap farthest from any route — the one most likely to be sitting in the woods
        /// where nothing will ever drive — and a level that already has a trap on a
        /// route's line is left exactly as it was.
        /// </summary>
        static bool MineARoute(TileGrid grid, IReadOnlyList<Corridor> corridors,
                               EncounterLayout layout, HashSet<int> mined,
                               int startIndex, int goalIndex)
        {
            if (corridors == null || corridors.Count == 0 || layout.Traps.Count == 0) return false;

            var onRoutes = new HashSet<int>();
            foreach (var corridor in corridors)
                if (corridor?.Tiles != null) onRoutes.UnionWith(corridor.Tiles);

            // Already true of this level, so nothing about it changes.
            foreach (var trap in layout.Traps)
                if (onRoutes.Contains(trap.Tile)) return false;

            int victim = FarthestFromRoutes(grid, layout.Traps, onRoutes);

            // Seeded from where the level starts and ends, which the map already fixes,
            // so the choice is as deterministic as the rest and touches nobody's draws.
            var rng = new DeterministicRandom(
                unchecked(startIndex * 73856093 ^ goalIndex * 19349663 ^ RouteMineSalt));

            var route = corridors[rng.Range(0, corridors.Count)];
            if (route?.Tiles == null || route.Tiles.Count < 3) return false;

            int from = Math.Max(1, Math.Min(route.Tiles.Count - 2,
                                            (int)(route.Tiles.Count * RouteTrapFrom)));
            int to = Math.Max(from + 1, Math.Min(route.Tiles.Count - 1,
                                                 (int)(route.Tiles.Count * RouteTrapTo)));

            // A group already stands where it stands; a trap under it is the one overlap
            // the placer refuses, and it is refused here too.
            var standing = new HashSet<int>();
            foreach (var enemy in layout.Enemies) standing.Add(enemy.Tile);

            // The spacing every trap keeps from every other, kept against all of them
            // but the one being moved.
            var others = new List<int>();
            for (int i = 0; i < layout.Traps.Count; i++)
                if (i != victim) others.Add(layout.Traps[i].Tile);

            // Walked outward from one drawn point rather than retried at random, so a
            // route whose middle is crowded still gets its trap somewhere sensible
            // instead of losing it to ten unlucky draws.
            int wanted = rng.Range(from, to);

            for (int step = 0; step < to - from; step++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (step == 0 && side > 0) break;   // the drawn point itself, once

                    int at = wanted + step * side;
                    if (at < from || at >= to) continue;

                    int tile = route.Tiles[at];
                    if (standing.Contains(tile)) continue;
                    if (!SpacedEnough(grid, tile, others, TrapSpacingTiles)) continue;

                    // Same kind, so the same cost and the same disarm silver: the level's
                    // totals are exactly what they were.
                    var moved = layout.Traps[victim];
                    mined.Remove(moved.Tile);
                    moved.Tile = tile;
                    moved.Origin = PlacementOrigin.OnRoute;
                    layout.Traps[victim] = moved;
                    mined.Add(tile);
                    return true;
                }
            }

            return false;
        }

        /// <summary>The index of the trap whose nearest route tile is farthest away.</summary>
        static int FarthestFromRoutes(TileGrid grid, List<TrapPlacement> traps,
                                      HashSet<int> onRoutes)
        {
            int worst = 0;
            float worstDistance = -1f;

            for (int i = 0; i < traps.Count; i++)
            {
                grid.ToCoords(traps[i].Tile, out int tx, out int ty);
                float nearest = float.MaxValue;

                foreach (int tile in onRoutes)
                {
                    grid.ToCoords(tile, out int rx, out int ry);
                    float dx = rx - tx, dy = ry - ty;
                    float squared = dx * dx + dy * dy;
                    if (squared < nearest) nearest = squared;
                }

                // Strictly greater, so a tie keeps the earlier trap and the choice is fixed.
                if (nearest > worstDistance)
                {
                    worstDistance = nearest;
                    worst = i;
                }
            }

            return worst;
        }

        /// <summary>
        /// The other third: traps out in the country, by terrain, the way all of them
        /// used to be placed. See <see cref="ThroatShare"/> for why both halves exist.
        /// </summary>
        static int Strew(TileGrid grid, ThreatBand band, DeterministicRandom rng,
                         EncounterLayout layout, HashSet<int> occupied, int allowance,
                         int upTo = int.MaxValue)
        {
            if (allowance <= 0) return 0;

            var scored = new List<KeyValuePair<float, int>>();

            foreach (int tile in band.Tiles)
            {
                if (occupied.Contains(tile)) continue;

                float density = TerrainTable.TrapDensity(grid[tile]);
                if (density <= 0f) continue;

                scored.Add(new KeyValuePair<float, int>(density * rng.Range(0.5f, 1.5f), tile));
            }

            scored.Sort((a, b) => b.Key.CompareTo(a.Key));

            int spent = 0;
            foreach (var entry in scored)
            {
                if (spent >= allowance) break;
                if (layout.Traps.Count >= upTo) break;
                spent += Lay(grid, rng, layout, occupied, entry.Value, allowance - spent);
            }

            return spent;
        }

        /// <summary>
        /// Puts one trap on a tile if it will take one, and returns what it cost.
        ///
        /// Six pits to four log traps. A pit is the cheaper lesson — 80 damage and two
        /// seconds pinned — and the log trap is the one that punishes a bunched column,
        /// five metres of it at 120 (docs/GDD.md §7.2).
        /// </summary>
        // The origin is passed rather than assumed. It was hard-coded to Scattered for
        // both callers, so every trap in the game reported itself as strewn however it
        // was placed — and "how many of these were laid at a throat" is exactly the
        // question anybody debugging trap placement asks first.
        static int Lay(TileGrid grid, DeterministicRandom rng, EncounterLayout layout,
                       HashSet<int> occupied, int tile, int left,
                       PlacementOrigin origin = PlacementOrigin.Scattered)
        {
            if (occupied.Contains(tile)) return 0;
            if (!SpacedEnough(grid, tile, occupied, TrapSpacingTiles)) return 0;

            var kind = rng.Chance(0.6f) ? TrapKind.Pit : TrapKind.Log;

            int cost = TrapTable.Points(kind);
            if (cost > left) return 0;

            layout.Traps.Add(new TrapPlacement
            {
                Tile = tile,
                Kind = kind,
                Origin = origin
            });

            occupied.Add(tile);
            return cost;
        }

        /// <summary>One narrow point of the crossing, and the tiles that make it up.</summary>
        struct Throat
        {
            public int Ways;

            /// <summary>The most road any one tile of this throat carries. See Roads.</summary>
            public float Roads;

            public List<int> Tiles;
        }

        /// <summary>
        /// The crossing's narrow points, narrowest first, with each one's tiles ordered
        /// by how central they are and how well the ground hides a trap.
        ///
        /// The centre first matters: a throat is laid across from the middle outward, so
        /// that a budget which runs out has closed the line anyone would actually walk
        /// rather than decorated its edges.
        /// </summary>
        /// <summary>
        /// How many of the offered corridors pass over or beside each tile.
        ///
        /// The throats alone were not enough, and the way they failed is worth keeping.
        /// A throat is narrow ground on the *ideal* crossing — the one the travel fields
        /// describe — and the three corridors a player is offered are generated lines
        /// that only roughly follow it. So the traps landed in the right stretch of
        /// country and a tile or two off the road: on 1-8 with a lone shieldbearer, not
        /// one trap was so much as *seen*, and with a scout in the squad they were seen
        /// and never trodden on. A three-metre trigger on a four-metre tile does not
        /// forgive being one tile out.
        ///
        /// Counted with a tile of slack, because a route runs corner to corner and a
        /// trap beside the line still catches the column's flank.
        ///
        /// <b>But beside is not on, and counting them the same is what put three quarters
        /// of every level's traps out of reach.</b> The slack was added to fix exactly the
        /// failure it then caused: a tile that is merely diagonally adjacent to a route
        /// tile is up to 5.7 m from the line the lead wagon actually traces, and the
        /// trigger is three. Scored identically to a tile *on* the road, such a tile won
        /// whenever it was a shade more central or a shade boggier — and the tiebreak
        /// looks at nothing else. Measured over chapter 1 against all three corridors at
        /// once, 10 to 43 percent of traps came within firing distance of any of them,
        /// and a run down one route sprang one or two of a dozen.
        ///
        /// So the slack stays and stops being free: a tile on a corridor counts for that
        /// corridor in full, a tile beside one counts <see cref="Aside"/>. On the road
        /// still beats beside it however good the ground is, which is what the rank in
        /// <see cref="Score"/> was always meant to say.
        /// </summary>
        static float[] Crossed(TileGrid grid, IReadOnlyList<Corridor> corridors)
        {
            var crossed = new float[grid.TileCount];
            if (corridors == null) return crossed;

            var on = new HashSet<int>();
            var beside = new HashSet<int>();

            foreach (var corridor in corridors)
            {
                on.Clear();
                beside.Clear();

                foreach (int tile in corridor.Tiles)
                {
                    on.Add(tile);

                    grid.ToCoords(tile, out int x, out int y);

                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (!grid.IsPassable(x + dx, y + dy)) continue;
                            beside.Add(grid.ToIndex(x + dx, y + dy));
                        }
                }

                // Once per corridor however many of its tiles touch this one, and at the
                // higher of the two weights where it is both on this corridor and beside
                // another part of it.
                foreach (int tile in on) crossed[tile] += 1f;
                foreach (int tile in beside) if (!on.Contains(tile)) crossed[tile] += Aside;
            }

            return crossed;
        }

        /// <summary>What a tile beside a corridor is worth against one on it.</summary>
        // A quarter, so four near-misses do not add up to one hit. The number only has to
        // be small enough that Score's per-corridor rank can never be won by adjacency,
        // and large enough that a tile beside all three corridors still beats one beside
        // none.
        public const float Aside = 0.25f;

        static List<Throat> Throats(TileGrid grid, ThreatBand band, float[] crossed)
        {
            var slices = new List<int>[ThroatSlices];
            float slack = band.Fastest * ThroatSlack;

            foreach (int tile in band.Tiles)
            {
                float detour = band.FromStart[tile] + band.FromGoal[tile] - band.Fastest;

                // Near the ideal crossing, **or** on a road somebody is actually offered.
                //
                // This is where the traps were being lost, and the comment on Crossed
                // half-names it without following it through: a throat is narrow ground
                // on the *ideal* crossing, and the three corridors only roughly follow
                // that. ThroatSlack keeps tiles within eight percent of the fastest
                // crossing — and the safe corridor on 1-1 takes 50 seconds against the
                // fast one's 39, the odd one 77. Two of the three roads a player may draw
                // lie wholly outside the filter, so their tiles were dropped here, before
                // Score ever saw them, and no amount of ranking inside a throat could put
                // a trap back on them.
                //
                // Measured over chapter 1: one to three traps per level landed on a
                // corridor tile, and a run sprang one or two of a dozen. The rest were
                // laid on ground no offered route passes within firing distance of.
                //
                // A tile a player will drive over is a throat tile by definition,
                // whatever it costs to route through — that is the whole of what these
                // are for.
                if (detour > slack && crossed[tile] < 1f) continue;

                int slice = (int)(band.FromStart[tile] / band.Fastest * ThroatSlices);
                if (slice < 0) slice = 0;
                if (slice >= ThroatSlices) slice = ThroatSlices - 1;

                (slices[slice] ?? (slices[slice] = new List<int>())).Add(tile);
            }

            var throats = new List<Throat>();

            for (int i = 0; i < ThroatSlices; i++)
            {
                if (slices[i] == null || slices[i].Count == 0) continue;

                var tiles = slices[i];

                // Dead centre of the throat first, and among equals the ground that hides
                // a trap best. TrapDensity is a multiplier on how tempting a tile is, so
                // it is applied to the detour rather than compared against it: bare
                // ground has to be twice as central as a bog to be chosen over it.
                tiles.Sort((a, b) => Score(grid, band, crossed, a)
                                    .CompareTo(Score(grid, band, crossed, b)));

                float roads = 0f;
                foreach (int tile in tiles) if (crossed[tile] > roads) roads = crossed[tile];

                throats.Add(new Throat { Ways = tiles.Count, Roads = roads, Tiles = tiles });
            }

            // Road first, narrowness second.
            //
            // Narrowest-first alone is what the allowance was being spent on, and it
            // answers the wrong question. The narrowest point of the country is only
            // worth mining if somebody drives through it, and the narrowest points are
            // frequently a gap in a ridge that no offered corridor goes near — so the
            // budget ran out three throats in, having closed three gaps nobody walks.
            //
            // Ordering by road carried spends it on the throats the offered routes
            // actually cross, and narrowness still decides between two that carry the
            // same: given two roads, mine the one with fewer ways round it.
            throats.Sort((a, b) =>
            {
                int byRoad = b.Roads.CompareTo(a.Roads);
                return byRoad != 0 ? byRoad : a.Ways.CompareTo(b.Ways);
            });

            return throats;
        }

        /// <summary>
        /// Lower is a better place for a trap: on the most roads, then central, then on
        /// the ground that hides one best.
        ///
        /// Roads first and by a wide margin. A trap on ground all three corridors cross
        /// is one nobody routes around; a trap on perfect ambush ground that no offered
        /// route touches is scenery, which is what the whole scatter used to be.
        /// </summary>
        static float Score(TileGrid grid, ThreatBand band, float[] crossed, int tile)
        {
            float density = TerrainTable.TrapDensity(grid[tile]);

            // A tile the ground refuses outright stays refused, however central it is.
            if (density <= 0f) return float.MaxValue;

            float detour = band.FromStart[tile] + band.FromGoal[tile] - band.Fastest;

            // A whole rank per corridor: three beats two beats one, before anything else
            // is looked at, and the rest decides ties within a rank.
            return -crossed[tile] * 1000f + (detour + 1f) / density;
        }

        /// <summary>The rest of the budget, over the band, weighted by how fast the ground is.</summary>
        /// <summary>
        /// Spends the level's threat, one road at a time.
        ///
        /// <b>Three passes, not one, and that is the whole of what tells the roads
        /// apart.</b> It was a single sweep over the band ordered by cover, which put the
        /// threat where the *woods* are - and the woods are slow ground, which is the
        /// ground the quick road avoids. So the quickest way through a level came out no
        /// more dangerous than the slowest, and measured over chapter one every road of
        /// every level met six to eight groups for thirty to fifty points. The long way
        /// round 1-1 took a hundred and seventy-two against seventy-three and met exactly
        /// as much, which is not a choice.
        ///
        /// Each road now spends its own share of the budget (<see cref="RoadShare"/>) on
        /// its own ground (ThreatBand.Road), buying its own number of groups
        /// (<see cref="RoadGroups"/>). Inside a road's ground cover still decides which
        /// tile, so a group still waits where a group would wait and the planning map's
        /// ambush reading still means something.
        ///
        /// Anything left over at the end goes on whatever ground is still free. A road
        /// whose country is too small or too crowded to take its whole share should not
        /// hand the difference back.
        /// </summary>
        static void ScatterEnemies(TileGrid grid, ThreatBand band, IReadOnlyList<Corridor> corridors,
                                   LevelRecipe recipe,
                                   DeterministicRandom rng, EncounterLayout layout,
                                   HashSet<int> occupied, HashSet<int> mined, int budget,
                                   RoadLine[] lines)
        {
            if (budget <= 0) return;

            int left = budget;
            var share = Shares(corridors);

            for (int road = 0; road < RoadShare.Length; road++)
            {
                int purse = (int)(budget * share[road]);
                if (purse > left) purse = left;

                left -= purse - Sow(grid, band, recipe, rng, layout, occupied, mined,
                                    purse, road, RoadGroups[road], lines);
            }

            // The remainder, anywhere it will go. Ground belonging to no road included:
            // a player may draw a line out there and should not find it empty.
            //
            // Held to the same gap as the rest. This pass is where a road's leftover
            // money goes, and spent without the rule it would put back exactly the
            // pile-ups the rule was written to stop.
            if (left > 0) left = Sow(grid, band, recipe, rng, layout, occupied, mined,
                                     left, -1, 0, lines);

            // And whatever still will not fit goes into the groups already standing.
            //
            // <b>Because the gap rule takes ground away, and the budget is the level.</b>
            // Spacing the groups along the road at forty-five metres cost a third of
            // them - 317 fights on the fast road became 218 - and the points they would
            // have cost were simply never spent. A level whose threat budget goes unspent
            // is not a better balanced level, it is an easier one, and that is not what
            // was asked for.
            //
            // So the money that has nowhere to stand is spent on who is standing: a group
            // is traded up for a dearer kind out of the same pool. Fewer fights, each of
            // them worth more, which is the shape the long way round was always meant to
            // have and is now what a crowded road gets too.
            if (left > 0) left = Enrich(recipe, rng, layout, left);
        }

        /// <summary>
        /// <see cref="RoadShare"/> per tile of road rather than per road, and then
        /// normalised back to the whole budget.
        ///
        /// <b>A share spread over a longer road is a share a caravan meets more of.</b>
        /// What a route runs into is how thickly the ground is sown times how far the
        /// route goes, and the long way round goes two and a half times as far: measured
        /// on 1-1 with a flat fifth of the budget, it was laid three groups and met seven.
        /// The allocation was right and the arithmetic under it was not.
        ///
        /// So each road's purse is divided by how much longer it is than the quickest,
        /// which leaves the *density* in proportion to the share and therefore what a
        /// caravan meets in proportion to it too. That is docs/GDD.md's own rule - the
        /// budget shared out in inverse proportion to a corridor's travel time - applied
        /// to the ground instead of to the route, which is what lets a hand-drawn line
        /// between two roads still find somebody.
        /// </summary>
        static float[] Shares(IReadOnlyList<Corridor> corridors)
        {
            var share = new float[RoadShare.Length];
            System.Array.Copy(RoadShare, share, share.Length);

            if (corridors == null) return share;

            float quickest = float.MaxValue;
            foreach (var corridor in corridors)
                if (corridor.TravelCost > 0f && corridor.TravelCost < quickest)
                    quickest = corridor.TravelCost;

            if (float.IsInfinity(quickest) || quickest <= 0f) return share;

            float total = 0f;

            foreach (var corridor in corridors)
            {
                int kind = (int)corridor.Kind;
                if (kind < 0 || kind >= share.Length || corridor.TravelCost <= 0f) continue;

                share[kind] *= quickest / corridor.TravelCost;
            }

            foreach (float part in share) total += part;
            if (total <= 0f) return share;

            for (int i = 0; i < share.Length; i++) share[i] /= total;
            return share;
        }

        /// <summary>
        /// Puts one road's share on one road's ground, and gives back what it could not
        /// spend.
        /// </summary>
        /// <param name="road">
        /// The <see cref="CorridorKind"/> whose ground to use, or -1 for any ground at all.
        /// </param>
        /// <param name="wanted">
        /// How many groups this share is meant to buy. It holds back enough of the share
        /// to afford them; once they are bought the rest goes on strength. At -1 for the
        /// road it is nought, which spends freely - by then the counts are met.
        /// </param>
        static int Sow(TileGrid grid, ThreatBand band, LevelRecipe recipe,
                       DeterministicRandom rng, EncounterLayout layout,
                       HashSet<int> occupied, HashSet<int> mined,
                       int budget, int road, int wanted, RoadLine[] lines)
        {
            if (budget <= 0) return 0;

            var scored = new List<KeyValuePair<float, int>>();
            foreach (int tile in band.Tiles)
            {
                if (occupied.Contains(tile)) continue;
                if (road >= 0 && band.Road[tile] != road) continue;

                // Near its own road, falling away with distance. See RoadReach.
                float near = road < 0 ? 1f
                    : RoadReach / (RoadReach + band.FromRoad[tile]);

                scored.Add(new KeyValuePair<float, int>(
                    band.Weight[tile] * near * rng.Range(0.6f, 1.4f), tile));
            }
            scored.Sort((a, b) => b.Key.CompareTo(a.Key));

            int placed = 0;

            foreach (var entry in scored)
            {
                if (budget <= 0) break;

                int tile = entry.Value;
                if (occupied.Contains(tile)) continue;

                // The tile itself, not its neighbourhood: a group has no business
                // standing in a pit, and every business standing beside one.
                if (mined.Contains(tile)) continue;
                if (!SpacedEnough(grid, tile, occupied, GroupSpacingTiles)) continue;

                // And far enough along every road that reaches it. See RoadGapMetres:
                // the straight-line rule above allows two groups twenty metres apart,
                // which is inside the distance at which each of them wakes.
                if (!Room(lines, tile)) continue;

                var kind = PickAffordable(recipe.EnemyPool, rng, budget, wanted - placed);
                if (kind == null) break;

                layout.Enemies.Add(new EnemySpawn
                {
                    Tile = tile,
                    Kind = kind.Value,
                    Origin = PlacementOrigin.Scattered
                });
                occupied.Add(tile);
                Mark(lines, tile);
                budget -= EnemyTable.Points(kind.Value);
                placed++;
            }

            return budget;
        }

        /// <summary>
        /// One road, as a ruler: how far along it each tile beside it lies, and where the
        /// fights already are.
        ///
        /// Worked out once per road and read per candidate tile, rather than walking the
        /// route for every tile the scatter considers. The placer runs up to forty-eight
        /// times per level while the generator looks for a map it will keep, so the
        /// difference is minutes.
        /// </summary>
        sealed class RoadLine
        {
            /// <summary>Metres along the road, or -1 for ground the road does not reach.</summary>
            float[] _at;

            readonly List<float> _taken = new List<float>();

            public static RoadLine Of(TileGrid grid, IReadOnlyList<int> route)
            {
                if (route == null || route.Count == 0) return null;

                var line = new RoadLine { _at = new float[grid.TileCount] };
                var apart = new float[grid.TileCount];

                for (int i = 0; i < grid.TileCount; i++) { line._at[i] = -1f; apart[i] = float.MaxValue; }

                // Diagonal steps are longer than straight ones and a road is mostly
                // diagonal, so counting tiles would understate every gap by a third.
                float along = 0f;

                // <b>As far as a group is met from, not as far as it engages.</b> This
                // reached EngageRadiusTiles, four, and the rule it feeds did nothing for
                // more than half the pile-ups it was written to stop: a group is met by a
                // road when the road comes within its *territory*, which is six tiles at
                // the smallest and thirteen at the largest. Everything sitting further out
                // than four had no place on the ruler at all and was waved through.
                //
                // The smallest territory rather than the largest, because a group that far
                // out is only sometimes met and this should not empty ground that a road
                // may never touch.
                int reach = (int)Math.Ceiling((double)TerritoryMinTiles);

                for (int i = 0; i < route.Count; i++)
                {
                    if (i > 0)
                    {
                        grid.ToCoords(route[i - 1], out int px, out int py);
                        grid.ToCoords(route[i], out int cx, out int cy);
                        along += (px != cx && py != cy ? 1.41421f : 1f) * TileGrid.TileSize;
                    }

                    grid.ToCoords(route[i], out int rx, out int ry);

                    for (int dy = -reach; dy <= reach; dy++)
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            int x = rx + dx, y = ry + dy;
                            if (!grid.InBounds(x, y)) continue;

                            float away = dx * dx + dy * dy;
                            if (away > TerritoryMinTiles * TerritoryMinTiles) continue;

                            int tile = grid.ToIndex(x, y);
                            if (away >= apart[tile]) continue;

                            apart[tile] = away;
                            line._at[tile] = along;
                        }
                }

                return line;
            }

            /// <summary>Whether a group here would be far enough from the ones already met.</summary>
            public bool Room(int tile)
            {
                float at = _at[tile];
                if (at < 0f) return true;

                foreach (float other in _taken)
                    if (Math.Abs(other - at) < RoadGapMetres) return false;

                return true;
            }

            public void Add(int tile)
            {
                if (_at[tile] >= 0f) _taken.Add(_at[tile]);
            }

            /// <summary>Forgets a group that has moved away from here.</summary>
            public void Remove(int tile)
            {
                if (_at[tile] < 0f) return;
                _taken.Remove(_at[tile]);
            }
        }

        /// <summary>
        /// The three roads as rulers, with whatever is already placed marked on them.
        ///
        /// Seeded from the layout rather than started empty, because the ford guards and
        /// anything else posted before the scatter are fights the road runs into too, and
        /// a scattered group laid beside a ford guard is the same pile-up by another
        /// name. What stands at the goal is left out: every road meets it by
        /// construction, and it is the end of the road rather than an encounter on it.
        /// </summary>
        static RoadLine[] Rulers(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                 EncounterLayout layout)
        {
            if (corridors == null) return new RoadLine[0];

            var lines = new RoadLine[corridors.Count];

            for (int i = 0; i < corridors.Count; i++)
            {
                lines[i] = RoadLine.Of(grid, corridors[i].Tiles);
                if (lines[i] == null) continue;

                foreach (var spawn in layout.Enemies)
                {
                    if (spawn.Origin == PlacementOrigin.Goal) continue;
                    lines[i].Add(spawn.Tile);
                }
            }

            return lines;
        }

        static bool Room(RoadLine[] lines, int tile)
        {
            foreach (var line in lines)
                if (line != null && !line.Room(tile)) return false;

            return true;
        }

        static void Mark(RoadLine[] lines, int tile)
        {
            foreach (var line in lines)
                if (line != null) line.Add(tile);
        }

        /// <summary>A group that has moved: off the ruler where it was, onto it where it is.</summary>
        static void Shift(RoadLine[] lines, int from, int to)
        {
            foreach (var line in lines)
            {
                if (line == null) continue;
                line.Remove(from);
                line.Add(to);
            }
        }

        /// <summary>
        /// The same targets, with the ones that leave a gap tried first.
        ///
        /// <b>Preferred, not required.</b> The repair loop exists to keep the promise the
        /// whole route-drawing mechanic rests on - draw what you like and you will still
        /// have a game - and a level that cannot keep it is re-rolled. Spacing is worth a
        /// lot and it is not worth that, so a crowded tile is still tried when no spaced
        /// one will do the job.
        ///
        /// It is worth a great deal in practice all the same: measured after the scatter
        /// learned the rule, 130 of the 351 remaining pile-ups had a repaired group on one
        /// side of them.
        /// </summary>
        static List<int> Spread(RoadLine[] lines, List<int> targets)
        {
            var roomy = new List<int>();
            var rest = new List<int>();

            foreach (int target in targets)
            {
                if (Room(lines, target)) roomy.Add(target);
                else rest.Add(target);
            }

            roomy.AddRange(rest);
            return roomy;
        }

        /// <summary>
        /// Trades groups up for dearer kinds until the money runs out or nothing can be
        /// traded, and returns what is left.
        ///
        /// <b>One step at a time, over and over, not the dearest kind that fits.</b> The
        /// first try took each group as far up the pool as the money allowed, and three
        /// combat tests fell over: a plain twelve-point escort met a bandit leader on the
        /// first fight of 1-5, died without killing anything, and the level earned nothing
        /// at all. Trading up to the next dearer kind and sweeping again spends exactly
        /// the same money and spreads it, so a road with room for ten groups gets ten
        /// harder fights rather than four impossible ones.
        ///
        /// Guards and the stand at the goal are left alone - they are placed promises with
        /// their own purses, and making the ford guard quietly dearer would move the
        /// level's difficulty somewhere the design did not put it.
        /// </summary>
        static int Enrich(LevelRecipe recipe, DeterministicRandom rng, EncounterLayout layout,
                          int left)
        {
            var pool = recipe.EnemyPool != null && recipe.EnemyPool.Length > 0
                ? recipe.EnemyPool : EnemyTable.All;

            bool traded = true;

            while (left > 0 && traded)
            {
                traded = false;

                // A fresh order every sweep, so the same few groups are not fattened
                // while the rest of the road stays as it was.
                var order = new List<int>();
                for (int i = 0; i < layout.Enemies.Count; i++) order.Add(i);

                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = rng.Range(0, i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }

                foreach (int i in order)
                {
                    var spawn = layout.Enemies[i];
                    if (spawn.Origin != PlacementOrigin.Scattered
                        && spawn.Origin != PlacementOrigin.Repair) continue;

                    int was = EnemyTable.Points(spawn.Kind);

                    EnemyKind? best = null;
                    int next = int.MaxValue;

                    foreach (var kind in pool)
                    {
                        int points = EnemyTable.Points(kind);
                        if (points <= was || points >= next || points - was > left) continue;

                        next = points;
                        best = kind;
                    }

                    if (best == null) continue;

                    left -= next - was;
                    spawn.Kind = best.Value;
                    layout.Enemies[i] = spawn;
                    traded = true;

                    if (left <= 0) break;
                }
            }

            return left;
        }

        /// <summary>Groups arrive one at a time, so nothing is placed on top of anything else.</summary>
        static bool SpacedEnough(TileGrid grid, int tile, IEnumerable<int> occupied, float spacing)
        {
            grid.ToCoords(tile, out int x, out int y);
            float limit = spacing * spacing;

            foreach (int other in occupied)
            {
                grid.ToCoords(other, out int ox, out int oy);
                float dx = ox - x, dy = oy - y;
                if (dx * dx + dy * dy < limit) return false;
            }
            return true;
        }

        /// <summary>
        /// One group, drawn from what the budget can afford - and holding back enough of
        /// it to buy the groups still owed.
        ///
        /// <paramref name="owed"/> is how many more groups this road's share still
        /// wants. See Sow. While that is more than one, this may only spend what would leave
        /// the rest buyable at the cheapest price in the pool; once the count is met it
        /// spends freely, which is where the strength goes. A pool whose cheapest kind
        /// costs more than the reserve allows falls back to affording anything at all,
        /// because a group placed is worth more than a count kept.
        /// </summary>
        static EnemyKind? PickAffordable(EnemyKind[] pool, DeterministicRandom rng, int budget,
                                         int owed)
        {
            var source = pool != null && pool.Length > 0 ? pool : EnemyTable.All;

            int cheapest = int.MaxValue;
            foreach (var kind in source)
            {
                int points = EnemyTable.Points(kind);
                if (points < cheapest) cheapest = points;
            }

            int spend = owed > 1 ? budget - (owed - 1) * cheapest : budget;
            if (spend < cheapest) spend = cheapest;

            var affordable = new List<EnemyKind>();
            foreach (var kind in source)
            {
                int points = EnemyTable.Points(kind);
                if (points <= budget && points <= spend) affordable.Add(kind);
            }

            if (affordable.Count == 0)
                foreach (var kind in source)
                    if (EnemyTable.Points(kind) <= budget) affordable.Add(kind);

            if (affordable.Count == 0) return null;
            return affordable[rng.Range(0, affordable.Count)];
        }

        /// <summary>
        /// Half the distance to the nearest other group, clamped. Halved so neighbouring
        /// territories meet rather than overlap, and clamped so a group alone in a
        /// corner does not end up watching a quarter of the map.
        /// </summary>
        static void AssignTerritories(TileGrid grid, EncounterLayout layout)
        {
            for (int i = 0; i < layout.Enemies.Count; i++)
            {
                var spawn = layout.Enemies[i];

                // The stand at the goal holds a gate, not a stretch of country, so it
                // keeps its own eyes: territory stays zero and TrackedEnemy falls back to
                // the table's detect radius.
                //
                // Given a territory it was given the *wrong* one. A group's territory is
                // half the distance to its nearest neighbour, and the nearest neighbour of
                // a man posted five tiles from the goal is whatever the scatter happened
                // to leave nearby — so the champion, whose twenty-six metres of notice is
                // the whole reason he cannot be crept past, was watching six. He sat at
                // full health while the caravan was killed twenty metres away by his own
                // bodyguard, and on a road where the bodyguard lost, the run simply never
                // ended: nothing could arrive and nothing could wake to stop it.
                if (spawn.Origin == PlacementOrigin.Goal) continue;

                grid.ToCoords(spawn.Tile, out int x, out int y);

                float nearest = float.PositiveInfinity;
                for (int j = 0; j < layout.Enemies.Count; j++)
                {
                    if (j == i) continue;

                    // The stand at the goal holds no country and takes none from anybody.
                    // A group's territory is half the distance to its nearest neighbour,
                    // so a champion posted near the goal quietly halved the reach of every
                    // group on the road beside him — which changed what routes met, which
                    // changed which map the generator kept.
                    if (layout.Enemies[j].Origin == PlacementOrigin.Goal) continue;

                    grid.ToCoords(layout.Enemies[j].Tile, out int ox, out int oy);
                    float dx = ox - x, dy = oy - y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distance < nearest) nearest = distance;
                }

                // <b>Half the distance to the neighbour is a ceiling, not a size.</b>
                //
                // It was the size, and that quietly cancelled the thing the placer had
                // just spent the budget deciding. Threat is dealt out by road now - half
                // on the quick way, a fifth on the long way round - so the quick road is
                // crowded and the slog is sparse. Under the old rule the crowded groups
                // came out at the six-tile floor and the lonely ones at the thirteen-tile
                // ceiling, which is four times the ground watched by each. Two groups
                // watching thirteen tiles sweep up as much as seven watching six, and the
                // measurement said so: the quick road carried the most threat on only
                // forty-two levels of a hundred, and the three roads averaged 37, 37 and
                // 33 points met. The shares were being placed and then undone.
                //
                // A territory is how far those men watch, which is a fact about them. The
                // neighbour rule stays as what stops two of them watching the same ground.
                float own = EnemyTable.DetectRadius(spawn.Kind) / TileGrid.TileSize;
                float apart = float.IsInfinity(nearest) ? TerritoryMaxTiles : nearest * 0.5f;

                float radius = own < apart ? own : apart;
                if (radius < TerritoryMinTiles) radius = TerritoryMinTiles;
                if (radius > TerritoryMaxTiles) radius = TerritoryMaxTiles;

                spawn.Territory = radius;
                layout.Enemies[i] = spawn;
            }
        }

        // --- Verification -----------------------------------------------------------

        /// <summary>
        /// Routes a player might actually draw: the three the generator knows, plus
        /// crossings through random waypoints in the band.
        ///
        /// This is the stand-in for the player, and everything the placer promises is
        /// checked against it. A promise that only holds for the three corridors is
        /// exactly the promise that broke when the player was handed a pen.
        /// </summary>
        public static List<List<int>> SampleRoutes(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                                   DeterministicRandom rng, int startIndex,
                                                   int goalIndex, int count = RouteSamples)
        {
            var band = ThreatBand.Build(grid, startIndex, goalIndex, corridors);
            return band == null
                ? new List<List<int>>()
                : SampleRoutes(grid, band, corridors, rng, startIndex, goalIndex, count);
        }

        /// <summary>
        /// How near an edge of the band a tile has to be to count as its fringe, as a
        /// share of the band's own width across the caravan's travel.
        ///
        /// A fifth off each side. Less and the fringe is a line rather than a flank, and
        /// the routes drawn through it are the middle ones again; much more and it stops
        /// being an edge at all and the samples say nothing new.
        /// </summary>
        public const float FringeShare = 0.2f;

        /// <summary>
        /// How wide the band has to be across a column before that column has flanks at
        /// all, in tiles.
        ///
        /// Ten, so a fifth off each side is two tiles of real flank rather than a
        /// rounding. Below it the column is part of the neck at one end of the band or
        /// the other, where every crossing goes anyway and there is no long way round to
        /// reward.
        /// </summary>
        public const int MinFlankSpan = 10;


        /// <summary>
        /// The band's two flanks: the tiles a route would pass through to go the long way
        /// round rather than straight across.
        ///
        /// Taken per column, not over the map as a whole, because the band is a lens: its
        /// top edge near the start is well south of its top edge at the waist. A single
        /// horizontal cut would call half the waist "fringe" and miss the flanks entirely
        /// at both ends.
        /// </summary>
        static List<int> Fringe(TileGrid grid, ThreatBand band)
        {
            var lowest = new Dictionary<int, int>();
            var highest = new Dictionary<int, int>();

            foreach (int tile in band.Tiles)
            {
                grid.ToCoords(tile, out int x, out int y);

                if (!lowest.TryGetValue(x, out int low) || y < low) lowest[x] = y;
                if (!highest.TryGetValue(x, out int high) || y > high) highest[x] = y;
            }

            var fringe = new List<int>();

            foreach (int tile in band.Tiles)
            {
                grid.ToCoords(tile, out int x, out int y);

                int low = lowest[x];
                int span = highest[x] - low;

                // Pinched columns have no flanks, and skipping them is the whole
                // correctness of this.
                //
                // The band closes to a point at the start and at the goal. Two tiles
                // across, every tile in the column is within a fifth of an end, so the
                // naive test called the entire neck "fringe" — and the neck is where
                // every route already goes. Measured, that handed the top strip of the
                // map six times its share of the threat off a 1.8 multiplier, which is
                // what a bonus landing on the ends rather than the sides looks like.
                if (span < MinFlankSpan) continue;

                float across = (y - low) / (float)span;
                if (across <= FringeShare || across >= 1f - FringeShare) fringe.Add(tile);
            }

            return fringe;
        }

        static List<List<int>> SampleRoutes(TileGrid grid, ThreatBand band,
                                            IReadOnlyList<Corridor> corridors,
                                            DeterministicRandom rng, int startIndex,
                                            int goalIndex, int count)
        {
            var routes = new List<List<int>>();
            foreach (var corridor in corridors)
                if (corridor?.Tiles != null && corridor.Tiles.Count > 0)
                    routes.Add(new List<int>(corridor.Tiles));

            grid.ToCoords(startIndex, out int sx, out int sy);
            grid.ToCoords(goalIndex, out int gx, out int gy);

            var fringe = Fringe(grid, band);

            var pathfinder = new GridPathfinder(grid);
            var leg = new List<int>();

            int guard = 0;
            while (routes.Count < count && band.Tiles.Count > 0 && guard++ < count * 4)
            {
                int waypoints = 1 + rng.Range(0, 2);

                // Every fourth sample is sent out to an edge, and the rest are drawn as
                // before.
                //
                // A uniform draw from the band is not a uniform draw over the map. The
                // band is lens-shaped — wide across the middle, pinched at both ends —
                // so a waypoint picked out of it lands in the middle nearly every time,
                // and a route through a middle waypoint is a middle route. All sixty-four
                // samples came out of the same waist of the lens.
                //
                // That is why the edges are bare. This loop is the placer's own conscience
                // — it moves a group onto whichever sampled route met too little — and it
                // can only repair the routes it is shown. It was never shown one that
                // hugged an edge, so it never noticed that hugging an edge met nothing.
                // Measured over chapter 1: six groups in the top eighth of the map and two
                // in the bottom, against fifteen to twenty-five across the middle.
                var pool = fringe.Count > 0 && routes.Count % 4 == 3 ? fringe : band.Tiles;

                var tiles = new List<int>();
                int fromX = sx, fromY = sy;
                bool broken = false;

                for (int w = 0; w <= waypoints; w++)
                {
                    int toX, toY;
                    if (w < waypoints)
                    {
                        int pick = pool[rng.Range(0, pool.Count)];
                        grid.ToCoords(pick, out toX, out toY);
                    }
                    else
                    {
                        toX = gx;
                        toY = gy;
                    }

                    if (!pathfinder.TryFindPath(fromX, fromY, toX, toY, leg, out _))
                    {
                        broken = true;
                        break;
                    }

                    for (int i = tiles.Count == 0 ? 0 : 1; i < leg.Count; i++) tiles.Add(leg[i]);
                    fromX = toX;
                    fromY = toY;
                }

                if (!broken && tiles.Count > 0) routes.Add(tiles);
            }

            return routes;
        }

        /// <summary>Indices of the enemy groups whose territory a route crosses.</summary>
        public static List<int> MetGroups(TileGrid grid, IReadOnlyList<int> route,
                                          EncounterLayout layout, bool roadOnly = false)
        {
            var met = new List<int>();
            var onRoute = new HashSet<int>(route);

            for (int i = 0; i < layout.Enemies.Count; i++)
            {
                var spawn = layout.Enemies[i];

                // The stand at the goal is not what this promise is about.
                //
                // MinEncounters asks whether a *drawn* route meets enough to be a level —
                // draw what you like, you will still have a game. Something posted at the
                // goal is met by every route by construction, so counting it adds one to
                // every score and tells the placer a road is livelier than it is. It also
                // changed which generated map was kept, because the generator re-rolls on
                // this number: adding a champion to 3-10 reshuffled the terrain and lost
                // the escort two roads it had been winning, with the champion untouched.
                if (roadOnly && spawn.Origin == PlacementOrigin.Goal) continue;

                if (onRoute.Contains(spawn.Tile)) { met.Add(i); continue; }

                float reach = spawn.Territory > 0f ? spawn.Territory : EngageRadiusTiles;
                if (WithinReach(grid, route, spawn.Tile, reach)) met.Add(i);
            }

            return met;
        }

        static bool WithinReach(TileGrid grid, IReadOnlyList<int> route, int tile, float reach)
        {
            grid.ToCoords(tile, out int tx, out int ty);
            float limit = reach * reach;

            for (int i = 0; i < route.Count; i++)
            {
                grid.ToCoords(route[i], out int rx, out int ry);
                float dx = rx - tx, dy = ry - ty;
                if (dx * dx + dy * dy <= limit) return true;
            }
            return false;
        }

        /// <summary>
        /// Samples routes and moves threat onto whichever one met too little.
        ///
        /// A scattered field says nothing about the worst case, and the worst case is
        /// the one that matters: a player who happens to draw between the groups gets a
        /// level with no game in it. Repairs move a group rather than add one, so the
        /// budget the difficulty curve is measured against stays exactly what the recipe
        /// asked for. A group no sampled route ever came near is doing nothing where it
        /// stands, so it is the one that moves.
        /// </summary>
        static void VerifyAndRepair(TileGrid grid, ThreatBand band, IReadOnlyList<Corridor> corridors,
                                    LevelRecipe recipe, DeterministicRandom rng,
                                    EncounterLayout layout, HashSet<int> occupied,
                                    int startIndex, int goalIndex, RoadLine[] lines)
        {
            var routes = SampleRoutes(grid, band, corridors, rng, startIndex, goalIndex, RouteSamples);
            layout.SampledRoutes = routes.Count;
            if (routes.Count == 0) return;

            var rejected = new HashSet<int>();
            Score(grid, routes, layout, out int fewest, out int tied, out int worst);

            // A rejection costs an attempt but not a repair, so the cap still means
            // what it says: twelve groups moved, not twelve things tried.
            for (int attempt = 0; attempt < MaxRepairs * RepairAttempts; attempt++)
            {
                layout.MinEncounters = fewest;
                if (worst < 0 || fewest >= RepairTarget || layout.Repairs >= MaxRepairs) break;

                int donor = IdlestGroup(grid, routes, layout, rejected);
                if (donor < 0) break;

                var targets = Spread(lines, EmptiestStretches(grid, routes[worst], band, occupied));
                if (targets.Count == 0) break;

                var before = layout.Enemies[donor];
                bool kept = false;

                foreach (int target in targets)
                {
                    var moved = before;
                    occupied.Remove(before.Tile);
                    moved.Tile = target;
                    moved.Origin = PlacementOrigin.Repair;
                    layout.Enemies[donor] = moved;
                    occupied.Add(target);
                    AssignTerritories(grid, layout);

                    Score(grid, routes, layout, out int nowFewest, out int nowTied, out int nowWorst);
                    if (nowFewest > fewest || (nowFewest == fewest && nowTied < tied))
                    {
                        fewest = nowFewest;
                        tied = nowTied;
                        worst = nowWorst;
                        kept = true;
                        Shift(lines, before.Tile, target);
                        break;
                    }

                    occupied.Remove(target);
                    layout.Enemies[donor] = before;
                    occupied.Add(before.Tile);
                    AssignTerritories(grid, layout);
                }

                if (!kept)
                {
                    rejected.Add(donor);
                    continue;
                }

                layout.Repairs++;
                rejected.Clear();   // the ground moved; a group that could not help may now
            }

            layout.MinEncounters = fewest;
            // Against the target, not the promise. A level that reaches five on the
            // routes the placer sampled has no margin left for the ones it did not,
            // and re-rolling costs generation time where shipping it costs a level
            // with no game in it.
            layout.EncountersValidated = fewest >= RepairTarget;

            // And then the part the floor knows nothing about: which road carries how
            // much. See Balance.
            Balance(grid, band, corridors, routes, layout, occupied, lines);

            TallySilver(layout, recipe);
            TopUpSilver(grid, routes, recipe, layout, occupied);
        }

        /// <summary>
        /// How good the layout is, worst route first: the fewest groups any sampled
        /// route meets, then how many routes are stuck at that number.
        ///
        /// The second term is what stops a repair from robbing one route to pay
        /// another. Without a score at all the loop had no idea which way was up, and
        /// kept every move it made.
        /// </summary>
        /// <summary>
        /// How far the three roads are from carrying the shares they owe, as the total
        /// of each road's error. Nought is exact; anything is possible up to two.
        ///
        /// Measured in points met rather than groups met, because a road's danger is what
        /// is on it and not how many piles it comes in - which is the whole of why the
        /// long way round has fewer and heavier ones.
        /// </summary>
        static float ShareError(TileGrid grid, IReadOnlyList<Corridor> corridors,
                                EncounterLayout layout, float[] met)
        {
            for (int i = 0; i < met.Length; i++) met[i] = 0f;

            float total = 0f;

            foreach (var corridor in corridors)
            {
                int kind = (int)corridor.Kind;
                if (kind < 0 || kind >= met.Length) continue;

                foreach (int group in MetGroups(grid, corridor.Tiles, layout, roadOnly: true))
                {
                    float points = EnemyTable.Points(layout.Enemies[group].Kind);
                    met[kind] += points;
                    total += points;
                }
            }

            if (total <= 0f) return float.MaxValue;

            float error = 0f;
            for (int i = 0; i < met.Length; i++)
                error += Math.Abs(met[i] / total - RoadShare[i]);

            return error;
        }

        /// <summary>
        /// Moves groups between the roads until each carries roughly its share.
        ///
        /// <b>Placement gets most of the way and cannot get all of it.</b> The three roads
        /// leave from one tile and arrive at another, so whatever stands near either end
        /// is met by all three; and the long way round crosses the other two on its way
        /// out and back, picking up their groups as it goes. Sown by road at a density
        /// scaled for length, the shares came out 38/34/26 against the 50/30/20 they are
        /// meant to be - better than the 37/37/33 of a single sweep over the band, and
        /// still not the thing promised.
        ///
        /// So the last of it is measured and corrected, which is the only way anything in
        /// this file has ever been made to hold. A group met by the road carrying too much
        /// is moved to ground beside the road carrying too little, the whole arrangement
        /// is scored again, and the move is kept only if the error fell. Nothing is added
        /// and nothing is taken away, so the level's budget and its silver are exactly
        /// what they were.
        ///
        /// The floor comes first and is not traded against this: a move that leaves a
        /// drawn route with less than <see cref="MinEncounters"/> on it is refused however
        /// much it helps the shares. A road nobody meets anything on is a worse level than
        /// one whose roads are a little too alike.
        /// </summary>
        static void Balance(TileGrid grid, ThreatBand band, IReadOnlyList<Corridor> corridors,
                            List<List<int>> routes, EncounterLayout layout,
                            HashSet<int> occupied, RoadLine[] lines)
        {
            if (corridors == null || corridors.Count < 2) return;

            var met = new float[RoadShare.Length];
            float error = ShareError(grid, corridors, layout, met);
            if (float.IsInfinity(error) || error == float.MaxValue) return;

            for (int attempt = 0; attempt < MaxRepairs * RepairAttempts; attempt++)
            {
                if (error <= ShareTolerance) break;

                Corridor over = null, under = null;
                float most = 0f, least = 0f;
                float total = 0f;
                foreach (float part in met) total += part;
                if (total <= 0f) break;

                foreach (var corridor in corridors)
                {
                    int kind = (int)corridor.Kind;
                    if (kind < 0 || kind >= met.Length) continue;

                    float off = met[kind] / total - RoadShare[kind];
                    if (off > most) { most = off; over = corridor; }
                    if (off < least) { least = off; under = corridor; }
                }

                if (over == null || under == null) break;

                // A group the crowded road meets and the starved one does not, so moving
                // it can only help both ends of the trade.
                var spare = MetGroups(grid, over.Tiles, layout, roadOnly: true);
                var keep = new HashSet<int>(MetGroups(grid, under.Tiles, layout, roadOnly: true));

                var targets = Spread(lines, EmptiestStretches(grid, under.Tiles, band, occupied));
                if (targets.Count == 0) break;

                bool moved = false;

                foreach (int donor in spare)
                {
                    if (keep.Contains(donor)) continue;
                    if (layout.Enemies[donor].Origin == PlacementOrigin.Goal) continue;
                    if (layout.Enemies[donor].Origin == PlacementOrigin.Guard) continue;

                    var before = layout.Enemies[donor];

                    foreach (int target in targets)
                    {
                        var shifted = before;
                        occupied.Remove(before.Tile);
                        shifted.Tile = target;
                        shifted.Origin = PlacementOrigin.Repair;
                        layout.Enemies[donor] = shifted;
                        occupied.Add(target);
                        AssignTerritories(grid, layout);

                        Score(grid, routes, layout, out int fewest, out _, out _);
                        float now = ShareError(grid, corridors, layout, met);

                        if (fewest >= MinEncounters && now < error)
                        {
                            error = now;
                            layout.Repairs++;
                            layout.MinEncounters = fewest;
                            moved = true;
                            Shift(lines, before.Tile, target);
                            break;
                        }

                        occupied.Remove(target);
                        layout.Enemies[donor] = before;
                        occupied.Add(before.Tile);
                        AssignTerritories(grid, layout);
                    }

                    if (moved) break;
                }

                if (!moved) break;

                ShareError(grid, corridors, layout, met);
            }

            layout.RoadShareError = error;
        }

        static void Score(TileGrid grid, List<List<int>> routes, EncounterLayout layout,
                          out int fewest, out int tied, out int worst)
        {
            fewest = int.MaxValue;
            worst = -1;

            for (int i = 0; i < routes.Count; i++)
            {
                int met = MetGroups(grid, routes[i], layout, roadOnly: true).Count;
                if (met >= fewest) continue;
                fewest = met;
                worst = i;
            }

            tied = 0;
            for (int i = 0; i < routes.Count; i++)
                if (MetGroups(grid, routes[i], layout, roadOnly: true).Count == fewest) tied++;
        }

        /// <summary>
        /// The placed group fewest sampled routes come near.
        ///
        /// Ford guards never move — a guard is the one placement no crossing can
        /// avoid, and spending it elsewhere gives that back. Nor does a group whose
        /// last move was rejected, until an accepted move changes the ground under
        /// the question.
        ///
        /// That second rule is why the loop terminates. Without it the group just
        /// moved was the idlest group on the next pass, because it went somewhere
        /// only one route reaches, so it was picked again — and again. Traced over
        /// forty passes on 2-5 the same band of raiders moved forty times while the
        /// worst route stayed pinned at four.
        /// </summary>
        static int IdlestGroup(TileGrid grid, List<List<int>> routes, EncounterLayout layout,
                               HashSet<int> rejected)
        {
            int best = -1, fewest = int.MaxValue;

            for (int i = 0; i < layout.Enemies.Count; i++)
            {
                var spawn = layout.Enemies[i];
                if (spawn.Origin == PlacementOrigin.Guard || spawn.Origin == PlacementOrigin.Goal
                    || rejected.Contains(i)) continue;

                float reach = spawn.Territory > 0f ? spawn.Territory : EngageRadiusTiles;
                int met = 0;
                foreach (var route in routes)
                    if (WithinReach(grid, route, spawn.Tile, reach)) met++;

                if (met >= fewest) continue;
                fewest = met;
                best = i;
            }

            return best;
        }

        /// <summary>
        /// Tiles on a route furthest from anything already placed, emptiest first.
        ///
        /// More than one, because the emptiest tile is a guess and not an answer. It
        /// is the stretch of road nothing else watches, which is usually where a group
        /// is worth most — but a group put there can cost another route more than it
        /// gains this one, and then the loop wants a second candidate rather than a
        /// different donor. Offering only the best tile left 1-10 giving up after a
        /// single repair.
        ///
        /// Candidates are spaced apart: the three emptiest tiles on a route are
        /// usually neighbours, and three tries at the same stretch of road is one try.
        /// </summary>
        static List<int> EmptiestStretches(TileGrid grid, IReadOnlyList<int> route, ThreatBand band,
                                           HashSet<int> occupied, int count = RepairTargets)
        {
            var scored = new List<(float Distance, int Tile)>();

            foreach (int tile in route)
            {
                if (occupied.Contains(tile) || !band.Contains(tile)) continue;
                if (band.FromStart[tile] < SafeEndCost || band.FromGoal[tile] < SafeEndCost) continue;

                grid.ToCoords(tile, out int x, out int y);
                float nearest = float.PositiveInfinity;

                foreach (int other in occupied)
                {
                    grid.ToCoords(other, out int ox, out int oy);
                    float dx = ox - x, dy = oy - y;
                    float distance = dx * dx + dy * dy;
                    if (distance < nearest) nearest = distance;
                }

                // Emptiness decides where, and cover decides which of the empty places.
                //
                // The loop used to sort on distance alone, and it is the last thing to
                // touch the layout — so on a level needing several repairs it undid the
                // cover-seeking of the scatter it was correcting, and the level shipped
                // with its groups standing in the open. Weighted rather than sorted after,
                // because a tile that is twice as empty should still win over one that
                // merely hides better.
                scored.Add((nearest * band.Weight[tile], tile));
            }

            scored.Sort((a, b) => a.Distance != b.Distance
                ? b.Distance.CompareTo(a.Distance)
                : a.Tile.CompareTo(b.Tile));

            var chosen = new List<int>();
            foreach (var candidate in scored)
            {
                if (SpacedEnough(grid, candidate.Tile, chosen, GroupSpacingTiles))
                    chosen.Add(candidate.Tile);
                if (chosen.Count >= count) break;
            }

            return chosen;
        }


        static void TallySilver(EncounterLayout layout, LevelRecipe recipe)
        {
            float multiplier = recipe.SilverMultiplier <= 0f ? 1f : recipe.SilverMultiplier;
            int total = 0;

            foreach (var spawn in layout.Enemies)
                total += (int)(EnemyTable.GroupSilver(spawn.Kind) * multiplier);

            foreach (var trap in layout.Traps)
                total += (int)(TrapTable.DisarmSilver(trap.Kind) * multiplier);

            foreach (var cache in layout.SilverCaches)
                total += cache.Amount;

            layout.TotalSilver = total;
        }

        /// <summary>
        /// A cache wherever a sampled route could not earn the floor.
        ///
        /// Same reasoning as the per-corridor top-up it replaces: a route that cannot
        /// pay for two upgrades leaves the player at the level's last fight with an army
        /// they had no way to improve, which is broken rather than hard. Only the unit
        /// of measurement changed, from the corridor to the line the player might draw.
        /// </summary>
        static void TopUpSilver(TileGrid grid, List<List<int>> routes, LevelRecipe recipe,
                                EncounterLayout layout, HashSet<int> occupied)
        {
            layout.SilverValidated = true;
            float multiplier = recipe.SilverMultiplier <= 0f ? 1f : recipe.SilverMultiplier;

            foreach (var route in routes)
            {
                int earned = 0;

                foreach (int index in MetGroups(grid, route, layout, roadOnly: true))
                    earned += (int)(EnemyTable.GroupSilver(layout.Enemies[index].Kind) * multiplier);

                for (int i = 0; i < layout.Traps.Count; i++)
                    if (WithinReach(grid, route, layout.Traps[i].Tile, EngageRadiusTiles))
                        earned += (int)(TrapTable.DisarmSilver(layout.Traps[i].Kind) * multiplier);

                for (int i = 0; i < layout.SilverCaches.Count; i++)
                    if (WithinReach(grid, route, layout.SilverCaches[i].Tile, EngageRadiusTiles))
                        earned += layout.SilverCaches[i].Amount;

                int shortfall = recipe.MinSilverPerRoute - earned;
                if (shortfall <= 0) continue;

                int tile = FreeTileOn(route, occupied);
                if (tile < 0) { layout.SilverValidated = false; continue; }

                layout.SilverCaches.Add(new SilverCache
                {
                    Tile = tile,
                    Amount = shortfall,
                    Origin = PlacementOrigin.Scattered
                });
                occupied.Add(tile);
            }

            TallySilver(layout, recipe);
        }

        static int FreeTileOn(IReadOnlyList<int> route, HashSet<int> occupied)
        {
            int middle = route.Count / 2;

            for (int offset = 0; offset < route.Count; offset++)
            {
                for (int direction = 1; direction >= -1; direction -= 2)
                {
                    int i = middle + offset * direction;
                    if (i < SafeEndTiles || i >= route.Count - SafeEndTiles) continue;
                    if (!occupied.Contains(route[i])) return route[i];
                }
            }
            return -1;
        }
    }
}
