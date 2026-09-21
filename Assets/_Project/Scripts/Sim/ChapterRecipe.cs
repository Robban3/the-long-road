using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// One chapter's difficulty curve. Produces a <see cref="LevelRecipe"/> for each
    /// of its levels by interpolating between a start and an end value
    /// (docs/content-pipeline.md §2).
    ///
    /// Balancing a chapter is therefore editing about a dozen numbers, not building
    /// ten levels by hand. That is the only way a hundred chapters is tractable.
    /// </summary>
    public sealed class ChapterRecipe
    {
        public int LevelsPerChapter = 10;

        /// <summary>
        /// Enemy count across the chapter. Kept inside the measured 100–140 band:
        /// below it the silver floor flattens every route's reward, above it the short
        /// fast corridor saturates and the long way round becomes the richer one.
        /// See LevelRecipe.EnemyBudget.
        /// </summary>
        // Left where it was, and the attempt to raise it is worth recording. Charging the
        // cautious corridor for the fast one's tiles spreads the same budget over three
        // separate roads instead of two-and-a-bit, so a fifth was added to both ends to
        // pay for it — and it broke two things at once: level 6 came out at 147 against
        // the saturation ceiling of 145 (see ChapterProgressionTests), and chapter 1 fell
        // to 19 survivable routes of 30 where it owes 20, with 1-10 down to one way
        // through where it owes two.
        //
        // The curve was already enough. What needed the twenty was LevelRecipe's plain
        // default, which is flat and is what a test or a standalone recipe gets.
        public int EnemyBudgetStart = 100;
        public int EnemyBudgetEnd = 140;

        /// <summary>
        /// Multiplier on enemy health and damage.
        ///
        /// This is the lever that carries difficulty past the point where adding more
        /// enemies stops working. Later chapters get harder by fielding tougher
        /// enemies, not more of them — the map simply has nowhere to put more.
        /// </summary>
        /// <summary>
        /// How much stronger every enemy is than the curve below describes, everywhere.
        ///
        /// <b>Because the forge had nothing to do.</b> Played the way the game is played -
        /// nothing from the field smithy at the start, the run's silver spent on it as it
        /// comes in (FieldSmith) - the escort the curve assumes got down the safe road on
        /// 30 levels of 30, the long road on 29 and the fast road on 29. Upgrading changed
        /// nothing because nothing needed upgrading for.
        ///
        /// Thirty-five per cent, swept rather than felt, against two players: the one the
        /// curve assumes, and a prepared one - the right line for the road, all the gold
        /// in the troops, and the silver spent deep at the field smithy. At 1.35 the first
        /// gets down the safe road on 28 levels of 30, the long one on 29 and the fast one
        /// on 17, and the second gets down every road of every level, which the generator
        /// refuses to ship without (LevelMaps.EveryRoadWinnable).
        ///
        /// It was 1.15 for a day, and that number was measured on a combat that could
        /// deadlock: troops never stepped off their posts, so an enemy standing just
        /// outside a troop's reach held the column until the stall watch called the run
        /// lost. Part of what read as the curve being hard was that. With the troops able
        /// to close on their attacker (CombatSystem.Close), 1.15 let the ordinary escort
        /// down the fast road on 28 levels of 30, and the hardness was swept again.
        ///
        /// On the enemies and not on the escort: the escort is what the player built, and
        /// EscortStrength and the silver multiplier keep describing that player.
        /// </summary>
        public const float EnemyHardness = 1.35f;

        public float EnemyStrengthStart = 1.00f;
        public float EnemyStrengthEnd = 1.35f;

        public float TrapDensityStart = 0.5f;
        public float TrapDensityEnd = 1.4f;

        public int RouteTilesStart = 55;
        public int RouteTilesEnd = 95;

        /// <summary>
        /// The player's own budget grows too, but deliberately more slowly than the
        /// threat. Attrition across the chapter compounds this, so the curve must not
        /// be steep or the last levels become impossible with a worn-down army.
        /// </summary>
        public int SquadBudgetStart = 12;
        public int SquadBudgetEnd = 18;

        /// <summary>
        /// Posts of the line, opening across the chapter.
        ///
        /// Three to six. The formation had all six from the first level and the player
        /// could never fill them: the cheapest six troops in the game cost twenty points
        /// and the budget above runs from twelve to eighteen, so a full line was not hard
        /// to afford but arithmetically impossible. Six sockets of which four can ever be
        /// used reads as a bug; three that become six as the chapter goes on reads as
        /// getting somewhere.
        ///
        /// Paced against the budget rather than against the level number: three posts at
        /// twelve points, six at eighteen, which is about four points a post either end.
        /// </summary>
        public int PostsStart = 3;
        public int PostsEnd = TroopTable.LinePosts;

        /// <summary>
        /// Scales enemy silver drops for the whole chapter. Upgrade costs are fixed,
        /// so without this later chapters would feel poor rather than hard.
        /// </summary>
        public float SilverMultiplier = 1f;

        /// <summary>
        /// The level at which each enemy type starts appearing, indexed by
        /// (int)EnemyKind. Introducing types one at a time is a difficulty lever in
        /// itself: the archer is not merely stronger, it is a problem that melee
        /// cannot answer at all.
        /// </summary>
        /// <summary>
        /// ...and never, for the two the first chapter has no business meeting. A band on
        /// horseback and a captain who takes four troops to put down are not a lesson for
        /// the levels that teach what a wolf is; they ride in from chapter two (see
        /// <see cref="For"/>). An entry past the end of this table reads as level one, so
        /// leaving them out is the same as sending them at the opening caravan.
        /// </summary>
        public int[] EnemyUnlockLevel = { 1, 2, 4, Never, Never };

        /// <summary>How strong the escort is assumed to be. See LevelRecipe.EscortStrength.</summary>
        public float EscortStrength = 1f;

        /// <summary>Which chapter this recipe is for, so a level can know its own place.</summary>
        public int Chapter = 1;

        public TerrainShare[] TerrainMix;
        public int Rivers = 1;
        public int FordsPerRiver = 3;
        public float NoiseScale = 18f;

        /// <summary>Builds the recipe for one level, 1-based.</summary>
        public LevelRecipe ForLevel(int level)
        {
            int span = LevelsPerChapter > 1 ? LevelsPerChapter - 1 : 1;
            int clamped = level < 1 ? 1 : (level > LevelsPerChapter ? LevelsPerChapter : level);
            float t = (clamped - 1) / (float)span;

            var recipe = new LevelRecipe
            {
                EnemyBudget = Lerp(EnemyBudgetStart, EnemyBudgetEnd, t),
                EnemyStrength = (EnemyStrengthStart + (EnemyStrengthEnd - EnemyStrengthStart) * t)
                                * EnemyHardness,
                EscortStrength = EscortStrength,
                TrapDensity = TrapDensityStart + (TrapDensityEnd - TrapDensityStart) * t,
                MinRouteTiles = Lerp(RouteTilesStart, RouteTilesEnd, t),
                SquadBudget = Lerp(SquadBudgetStart, SquadBudgetEnd, t),
                Posts = Lerp(PostsStart, PostsEnd, t),
                SilverMultiplier = SilverMultiplier,
                Rivers = Rivers,
                FordsPerRiver = FordsPerRiver,
                NoiseScale = NoiseScale,
                EnemyPool = PoolForLevel(clamped),

                // Levels 6 to 9 are the escalation band and owe one way through; every
                // other level owes two. See LevelRecipe.RoutesOwed.
                RoutesOwed = clamped >= 6 && clamped <= 9 ? 1 : 2,

                // And the one level with a town on it, whose walls are laid before the
                // ways through are found. See Towns.
                Town = Towns.HasTown(Chapter, clamped),

                // <b>And a town owes no crossings, because a town has no river.</b>
                // Towns.Stamp clears the whole grid - the level is the town and the map's
                // edge is its wall - so whatever the generator carved is gone before a
                // street is laid. The level was still being asked for three ways over the
                // water, could not have one, and was rejected on that every attempt: it
                // shipped as a compromise every single build, and the decorator stood a
                // bridge on a ford with no banks because the fallback had nothing better.
                //
                // The demand was never about this level. It is about a river being a
                // decision, and there is no river here - the decision is the high street
                // against the back lanes.
                CrossingsOwed = Towns.HasTown(Chapter, clamped) ? 0 : new LevelRecipe().CrossingsOwed,

                // And who is waiting at the end of it. See Champions.
                GoalRetinue = Champions.Named(Chapter, clamped) ? Champions.Retinue(Chapter) : 0,
                GoalBlocks = Champions.Named(Chapter, clamped)
            };

            if (TerrainMix != null && TerrainMix.Length > 0) recipe.TerrainMix = TerrainMix;

            // After the pool, which is what the guard is drawn from. Nothing at the goal
            // means nothing to pay for: see Champions.Guards for why that is most levels.
            recipe.GoalBudget = Champions.Guards(Chapter, clamped)
                ? Champions.Purse(recipe.EnemyPool, recipe.GoalBlocks, recipe.GoalRetinue)
                : 0;
            return recipe;
        }

        public EnemyKind[] PoolForLevel(int level)
        {
            var pool = new List<EnemyKind>();
            foreach (var kind in EnemyTable.All)
            {
                int index = (int)kind;
                int unlock = index < EnemyUnlockLevel.Length ? EnemyUnlockLevel[index] : 1;
                if (level >= unlock) pool.Add(kind);
            }

            // A level with nothing in it is worse than one that is slightly too easy.
            if (pool.Count == 0) pool.Add(EnemyKind.Wolf);
            return pool.ToArray();
        }

        static int Lerp(int from, int to, float t) => (int)(from + (to - from) * t + 0.5f);

        /// <summary>What chapter one's enemies gain in strength across its ten levels.</summary>
        public const float StrengthPerChapter = 0.35f;

        /// <summary>
        /// Chapters over which the climb bends, past chapter two. See <see cref="StrengthAtEndOf"/>.
        /// </summary>
        public const float Knee = 3f;

        /// <summary>
        /// The recipe for any chapter: chapter one exactly as it always was, and every
        /// chapter after it starting where the one before ended.
        ///
        /// Built for about a thousand levels, which is what shapes it. Chapter one's rise
        /// kept up for a hundred chapters would put the last enemies at thirty-six times
        /// their first strength, against a player whose every purchase has a cap — the
        /// shop's tracks stop at thirty steps, the line at six posts. So the climb is
        /// chapter one's for two chapters and then bends, and the player's budget grows
        /// until it can field a line of knights and stops there.
        ///
        /// What stays put: the enemy count, which the map has no room to raise (see
        /// ChapterProgressionTests), the route lengths, and the shape of a chapter — an
        /// easier start, the escalation band, the tenth.
        /// </summary>
        public static ChapterRecipe For(int chapter)
        {
            var recipe = new ChapterRecipe { Chapter = chapter < 1 ? 1 : chapter };
            if (chapter <= 1) return recipe;

            recipe.EnemyStrengthStart = StrengthAtEndOf(chapter - 1);
            recipe.EnemyStrengthEnd = StrengthAtEndOf(chapter);

            // The generator pictures a player who has grown with the chapters before; the
            // chapter's own rise is still theirs to meet.
            recipe.EscortStrength = recipe.EnemyStrengthStart;

            // Stronger enemies take longer to put down and the smithy's prices do not
            // move, so the silver follows the strength — at its root, so that it helps
            // without keeping pace.
            recipe.SilverMultiplier = (float)System.Math.Sqrt(recipe.EnemyStrengthStart);

            recipe.TrapDensityStart = 1.0f;
            recipe.TrapDensityEnd = System.Math.Min(TrapCeiling, 1.6f + 0.05f * (chapter - 2));

            // Four points a chapter, six across one, up to a line of knights.
            recipe.SquadBudgetStart = System.Math.Min(SquadCeiling - 6, 12 + 4 * (chapter - 1));
            recipe.SquadBudgetEnd = recipe.SquadBudgetStart + 6;

            recipe.PostsStart = System.Math.Min(TroopTable.LinePosts, 3 + 2 * (chapter - 1));

            // The wolves, the raiders and their archers from the first level: those
            // lessons were chapter one's. The horsemen and the captain are this chapter's
            // own, and they are paced inside it — a band on horseback halfway through,
            // the man who leads it late, where a worn escort meets him.
            recipe.EnemyUnlockLevel = new[] { 1, 1, 1, 4, 7 };

            // The country last, so it has the final word on the ground it is made of.
            // Applied after the climb because some of what it says is a change to what the
            // climb just decided — the fens are trappier than the road behind them,
            // whatever rung of the climb they are on.
            Country(recipe, Biomes.Of(chapter));

            return recipe;
        }

        /// <summary>
        /// Dresses a chapter in its country: the terrain mix, the water across it, the
        /// size of the land forms, and who is out there.
        ///
        /// <b>This is what makes a biome more than a change of models.</b> A marsh with
        /// the forest's ten percent of bog is a wood with puddles; the plains with the
        /// forest's forty-five percent of trees is a wood. The shares are the country,
        /// and the models are what it looks like.
        ///
        /// Every mix is five shares of the same five terrains the default uses, summing to
        /// one, because the generator hands them out by quantile: adding a sixth kind or
        /// summing to something else silently reshapes all of them. Cliffs are not in it —
        /// they are placed as river banks and map edges, not sown across the ground.
        ///
        /// Forest and winter are absent on purpose: they are the defaults, and a country
        /// nobody has built scenery for is drawn as forest anyway (see Biomes.Order), so a
        /// chapter set in a marsh plays over marsh ground under woodland trees until the
        /// marsh is built. Wrong-looking is better than wrong to play.
        /// </summary>
        static void Country(ChapterRecipe recipe, Biome biome)
        {
            switch (biome)
            {
                case Biome.Marsh:
                    // Bog and standing water, and two rivers with few crossings: what the
                    // marsh does to a caravan is take away the choice of where to cross.
                    // <b>Bog rather than open water, and crossings the map can keep.</b>
                    // At a tenth of the ground in open water — the forest's own share —
                    // the lakes grew into the rivers and swallowed their fords: measured
                    // on 3-1, 588 tiles of water against seven of crossing, so the three
                    // routes all queued for the one ford left and the bridge stood in a
                    // lake. Half the water, and two rivers cut three times each so six
                    // crossings are laid where three have to survive. Fewer per river
                    // than the forest still, so the fen is the wettest country on the
                    // road and it is waded rather than bridged, which is what a fen is.
                    recipe.TerrainMix = Mix(0.27f, 0.20f, 0.43f, 0.05f, 0.05f);
                    recipe.Rivers = 2;
                    recipe.FordsPerRiver = 3;
                    Traps(recipe, 1.1f);
                    break;

                case Biome.Plains:
                    // Open ground: the horse's country, and nowhere to hide from a bow.
                    // Broader land forms too, so the openness reads as country rather than
                    // as a missing forest.
                    recipe.TerrainMix = Mix(0.22f, 0.58f, 0.07f, 0.06f, 0.07f);
                    recipe.FordsPerRiver = 4;
                    recipe.NoiseScale = 24f;
                    break;

                case Biome.Farmland:
                    // Country somebody lives in: fields between woodlots, and water people
                    // settled beside.
                    recipe.TerrainMix = Mix(0.28f, 0.50f, 0.07f, 0.05f, 0.10f);
                    break;

                case Biome.Mountain:
                    // Passes and tight land forms, and a road that has to go the long way
                    // round rather than over.
                    recipe.TerrainMix = Mix(0.26f, 0.26f, 0.05f, 0.34f, 0.09f);
                    recipe.FordsPerRiver = 2;
                    recipe.NoiseScale = 13f;
                    recipe.RouteTilesStart += 8;
                    recipe.RouteTilesEnd += 8;
                    break;

                case Biome.Coast:
                    // Water on one hand and salt marsh behind the dunes, cut by two river
                    // mouths.
                    recipe.TerrainMix = Mix(0.25f, 0.33f, 0.15f, 0.05f, 0.22f);
                    recipe.Rivers = 2;
                    break;

                case Biome.Desert:
                    // Sand, rock and one thread of water. No wolves: nothing here hunts in
                    // packs, and the danger is the men who know where the water is.
                    //
                    // <b>And there has to be somewhere for those men to be.</b> This was
                    // eight per cent cover against sixty-six per cent open sand, and six
                    // of the chapter's ten levels could not be validated: the placer sends
                    // threat to cover, so every group in the level sat in that eight per
                    // cent, and a road that missed those few stands met nothing at all.
                    // The generator shipped the least bad map each time.
                    //
                    // Scrub and broken rock rather than more wood - which is what ambush
                    // country in a desert is, and what the chapter is drawn as. Sixteen
                    // and twenty-two against fifty-two of open sand: still the emptiest
                    // country in the game by a wide margin, and no longer so empty that
                    // nobody can wait in it.
                    recipe.TerrainMix = Mix(0.16f, 0.52f, 0.05f, 0.22f, 0.05f);
                    recipe.FordsPerRiver = 2;
                    recipe.NoiseScale = 26f;
                    // No wolves, and horsemen from the start: open sand is their country,
                    // and a band that lives out here lives on horseback.
                    recipe.EnemyUnlockLevel = new[] { Never, 1, 1, 1, 5 };
                    break;

                case Biome.Enchanted:
                    // Deep wood with bog in it, and more of it trapped: the wood does not
                    // want the road. Its beasts meet the caravan first and its people late.
                    recipe.TerrainMix = Mix(0.56f, 0.18f, 0.16f, 0.04f, 0.06f);
                    Traps(recipe, 1.25f);
                    // Beasts first and people late, and no horsemen at all: nothing rides
                    // through a wood this close, which is also what makes it safe to send
                    // the wood's own captain early.
                    recipe.EnemyUnlockLevel = new[] { 1, 4, 4, Never, 5 };
                    break;

                case Biome.Dead:
                    // Ash and bare rock where a country used to be, and the most trapped
                    // ground on the road: everything left here was left to catch somebody.
                    recipe.TerrainMix = Mix(0.30f, 0.38f, 0.05f, 0.17f, 0.10f);
                    recipe.FordsPerRiver = 2;
                    Traps(recipe, 1.4f);
                    break;
            }
        }

        /// <summary>A level whose number no chapter reaches: an enemy kind this country has none of.</summary>
        public const int Never = 999;

        /// <summary>
        /// More trapped ground than the climb asked for, or less, and never past the
        /// ceiling: the scout and the engineer have to be able to keep up (see
        /// <see cref="TrapCeiling"/>).
        /// </summary>
        static void Traps(ChapterRecipe recipe, float factor)
        {
            recipe.TrapDensityStart = System.Math.Min(TrapCeiling, recipe.TrapDensityStart * factor);
            recipe.TrapDensityEnd = System.Math.Min(TrapCeiling, recipe.TrapDensityEnd * factor);
        }

        /// <summary>The five shares, in the order the default declares them.</summary>
        static TerrainShare[] Mix(float forest, float plains, float marsh, float pass, float water)
            => new[]
            {
                new TerrainShare(TerrainType.Forest, forest),
                new TerrainShare(TerrainType.Plains, plains),
                new TerrainShare(TerrainType.Marsh, marsh),
                new TerrainShare(TerrainType.MountainPass, pass),
                new TerrainShare(TerrainType.Water, water)
            };

        /// <summary>
        /// Enemy strength at the end of a chapter; chapter zero is the start of the first.
        ///
        /// Straight for two chapters — 1.35 and 1.70, the rise chapter one already had —
        /// then a logarithm that leaves at the same slope, so there is no step where the
        /// two meet: 2.0 after chapter three, about 3.1 after ten, 4.7 after fifty and 5.4
        /// after a hundred. Always rising, ever more slowly; the last fifty chapters add
        /// about as much as chapters three and four did.
        /// </summary>
        public static float StrengthAtEndOf(int chapter)
        {
            if (chapter <= 0) return 1f;
            if (chapter <= 2) return 1f + StrengthPerChapter * chapter;

            return 1f + StrengthPerChapter * 2f
                 + StrengthPerChapter * Knee * (float)System.Math.Log(1f + (chapter - 2) / Knee);
        }

        /// <summary>Squad points at which the budget stops: six knights, the dearest line there is.</summary>
        public static int SquadCeiling => TroopTable.LinePosts * TroopTable.Cost(TroopKind.Knights);

        /// <summary>Traps at the end of a chapter stop here; the scout and the engineer have to keep up.</summary>
        public const float TrapCeiling = 2.0f;
    }
}
