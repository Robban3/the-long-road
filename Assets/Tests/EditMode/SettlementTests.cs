using TheVeil.Gen;
using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// That a level promised a village gets one, and that the countries nobody lives in
    /// stay empty.
    ///
    /// Written because the first version quietly failed: the site wanted a seven-by-seven
    /// of flat plains beside a way through, 1-6 had none anywhere on the map, and the
    /// level came out with no village and no complaint. A promise the code makes to the
    /// player — this chapter has somewhere people live — is exactly the kind that has to
    /// be kept by something other than luck.
    /// </summary>
    public class SettlementTests
    {
        /// <summary>The chapters with content, which are the ones whose maps can be judged.</summary>
        static readonly int[] Chapters = { 1, 2, 3 };

        [Test]
        public void EveryLevelPromisedAVillageHasOne()
        {
            foreach (int chapter in Chapters)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);
                    int site = Settlements.Site(map, chapter, level);

                    if (!Settlements.HasVillage(chapter, level))
                    {
                        Assert.AreEqual(-1, site, $"{chapter}-{level} has a village it was never promised");
                        continue;
                    }

                    Assert.GreaterOrEqual(site, 0,
                        $"{chapter}-{level} is a village level and no site could be found");

                    // On the map, and on ground somebody could have built on.
                    Assert.Less(site, map.Grid.TileCount, $"{chapter}-{level}: the site is off the map");

                    var terrain = map.Grid[site];
                    Assert.AreNotEqual(TerrainType.Water, terrain, $"{chapter}-{level}: a village in the water");
                    Assert.AreNotEqual(TerrainType.Ford, terrain, $"{chapter}-{level}: a village on a crossing");
                    Assert.AreNotEqual(TerrainType.Cliff, terrain, $"{chapter}-{level}: a village on a cliff");
                }
            }
        }

        [Test]
        public void NobodyLivesInTheFenTheAshOrTheSand()
        {
            Assert.IsFalse(Settlements.Settled(Biome.Marsh));
            Assert.IsFalse(Settlements.Settled(Biome.Dead));
            Assert.IsFalse(Settlements.Settled(Biome.Desert));
            Assert.IsFalse(Settlements.Settled(Biome.Enchanted));

            Assert.IsTrue(Settlements.Settled(Biome.Forest));
            Assert.IsTrue(Settlements.Settled(Biome.Plains));
            Assert.IsTrue(Settlements.Settled(Biome.Farmland));

            // And the rule the countries are read through: no village on any level of a
            // chapter set in one of them, whichever levels the table names.
            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                Assert.IsFalse(Settlements.HasVillage(Biomes.FirstChapterOf(Biome.Marsh), level),
                               $"the fen has a village on level {level}");
        }

        [Test]
        public void AVillageStandsWithinReachOfAWayThrough()
        {
            // A village nobody passes is scenery nobody sees. The site is chosen near a
            // corridor for that reason, and this is the reason written down.
            foreach (int chapter in Chapters)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    if (!Settlements.HasVillage(chapter, level)) continue;

                    var map = LevelMaps.For(chapter, level);
                    int site = Settlements.Site(map, chapter, level);
                    if (site < 0) continue;

                    map.Grid.ToCoords(site, out int x, out int y);

                    bool near = false;
                    foreach (var corridor in map.Corridors)
                        foreach (int tile in corridor.Tiles)
                        {
                            map.Grid.ToCoords(tile, out int cx, out int cy);
                            if (System.Math.Abs(cx - x) <= Settlements.Reach
                                && System.Math.Abs(cy - y) <= Settlements.Reach) near = true;
                        }

                    Assert.IsTrue(near, $"{chapter}-{level}: the village is off every way through");
                }
            }
        }
    }
}
