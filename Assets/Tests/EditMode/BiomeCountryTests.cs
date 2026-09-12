using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// That a country is more than a change of models: the ground a chapter is made of,
    /// the water across it and who is out there all follow the biome
    /// (ChapterRecipe.Country).
    ///
    /// A marsh with the forest's tenth of bog is a wood with puddles, and that is the
    /// failure these are here to catch — one that looks like new scenery and plays like
    /// the last chapter.
    /// </summary>
    public class BiomeCountryTests
    {
        static float ShareOf(LevelRecipe recipe, TerrainType type)
        {
            foreach (var share in recipe.TerrainMix)
                if (share.Type == type) return share.Share;

            return 0f;
        }

        /// <summary>The first chapter set in a country, which is where it is judged.</summary>
        static LevelRecipe Level(Biome biome, int level = 5)
            => ChapterRecipe.For(Biomes.FirstChapterOf(biome)).ForLevel(level);

        [Test]
        public void EveryCountryDealsOutWholeGround()
        {
            foreach (var biome in Biomes.Order)
            {
                var recipe = Level(biome);
                float total = 0f;

                foreach (var share in recipe.TerrainMix)
                {
                    Assert.GreaterOrEqual(share.Share, 0f, $"{biome} has a negative share of {share.Type}");
                    total += share.Share;
                }

                // The generator hands the ground out by quantile, so the shares have to be
                // a whole: summing to something else quietly reshapes all of them.
                Assert.AreEqual(1f, total, 0.001f, $"{biome} shares sum to {total:F2}");
                Assert.Greater(recipe.Rivers, 0, $"{biome} has no river to cross");
                Assert.Greater(recipe.FordsPerRiver, 0, $"{biome} has a river nobody can cross");
            }
        }

        [Test]
        public void TheForestAndTheWinterAreTheGroundTheyAlwaysWere()
        {
            var plain = new LevelRecipe();

            foreach (int chapter in new[] { 1, Biomes.WinterChapter })
            {
                var recipe = ChapterRecipe.For(chapter).ForLevel(5);

                Assert.AreEqual(plain.TerrainMix.Length, recipe.TerrainMix.Length, $"chapter {chapter}");
                foreach (var share in plain.TerrainMix)
                    Assert.AreEqual(share.Share, ShareOf(recipe, share.Type), 0.0001f,
                                    $"chapter {chapter}: {share.Type}");
            }
        }

        [Test]
        public void TheMarshIsMostlyBogAndHardToCross()
        {
            var marsh = Level(Biome.Marsh);
            var forest = Level(Biome.Forest);

            Assert.Greater(ShareOf(marsh, TerrainType.Marsh), 3f * ShareOf(forest, TerrainType.Marsh));
            Assert.Greater(ShareOf(marsh, TerrainType.Marsh), ShareOf(marsh, TerrainType.Forest));

            // Two rivers, two crossings each: the marsh takes away the choice of where.
            Assert.AreEqual(2, marsh.Rivers);
            Assert.Less(marsh.FordsPerRiver, forest.FordsPerRiver);
        }

        [Test]
        public void ThePlainsAreOpenAndTheMountainsAreLong()
        {
            var plains = Level(Biome.Plains);
            Assert.Greater(ShareOf(plains, TerrainType.Plains), 2f * ShareOf(plains, TerrainType.Forest));
            Assert.Greater(plains.FordsPerRiver, Level(Biome.Forest).FordsPerRiver);

            var mountain = Level(Biome.Mountain);
            Assert.Greater(ShareOf(mountain, TerrainType.MountainPass), 4f * ShareOf(plains, TerrainType.MountainPass));
            Assert.Greater(mountain.MinRouteTiles, Level(Biome.Farmland).MinRouteTiles,
                           "a road through the passes is no longer than one across the fields");
        }

        [Test]
        public void NothingHuntsInPacksInTheDesertAndTheWoodKeepsItsPeopleBack()
        {
            var desert = ChapterRecipe.For(Biomes.FirstChapterOf(Biome.Desert));

            for (int level = 1; level <= desert.LevelsPerChapter; level++)
            {
                var pool = desert.PoolForLevel(level);
                Assert.IsFalse(System.Array.IndexOf(pool, EnemyKind.Wolf) >= 0, $"a wolf pack in the desert on level {level}");
                Assert.Contains(EnemyKind.Bandit, pool, $"nobody at all in the desert on level {level}");
            }

            var wood = ChapterRecipe.For(Biomes.FirstChapterOf(Biome.Enchanted));

            Assert.Contains(EnemyKind.Wolf, wood.PoolForLevel(1), "the enchanted wood has no beasts");
            Assert.IsFalse(System.Array.IndexOf(wood.PoolForLevel(1), EnemyKind.Bandit) >= 0,
                           "the wood's people arrive on the first level");
            Assert.Contains(EnemyKind.Bandit, wood.PoolForLevel(4));
        }

        [Test]
        public void TheDeadLandIsTheMostTrappedGroundOnTheRoad()
        {
            var dead = Level(Biome.Dead, level: 10);
            var plains = Level(Biome.Plains, level: 10);

            Assert.Greater(dead.TrapDensity, plains.TrapDensity);

            // And never past what the scout and the engineer can keep up with.
            foreach (var biome in Biomes.Order)
                for (int level = 1; level <= 10; level++)
                    Assert.LessOrEqual(Level(biome, level).TrapDensity, ChapterRecipe.TrapCeiling + 0.0001f,
                                       $"{biome} on level {level}");
        }

        [Test]
        public void EveryCountryStillFieldsAnArmyTheMapCanHold()
        {
            // The climb's own promises, checked once more now that the country has the
            // last word on the recipe: see ChapterProgressionTests.
            foreach (var biome in Biomes.Order)
            {
                var chapter = ChapterRecipe.For(Biomes.FirstChapterOf(biome));

                for (int level = 1; level <= chapter.LevelsPerChapter; level++)
                {
                    var recipe = chapter.ForLevel(level);

                    Assert.GreaterOrEqual(recipe.EnemyBudget, 95, $"{biome} on level {level}");
                    Assert.LessOrEqual(recipe.EnemyBudget, 145, $"{biome} on level {level}");
                    Assert.Greater(recipe.SquadBudget, 0, $"{biome} on level {level}");
                    Assert.Greater(recipe.EnemyPool.Length, 0, $"{biome} on level {level} has nothing in it");
                }
            }
        }
    }
}
