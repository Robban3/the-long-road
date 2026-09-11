using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// The planning map and the run have to agree on where the bridge is.
    ///
    /// They build the country separately, with different amounts of it — the run has a
    /// skyline and an apron, the plan a denser wood — and the bridge was chosen from the
    /// stream both of them drew from, after all of that. The player drew their route over
    /// a bridge that stood on another crossing in the game.
    /// </summary>
    public class BridgeTests
    {
        [Test]
        public void TheBridgeIsChosenByTheLevelAlone()
        {
            int bridged = 0;

            for (int chapter = 1; chapter <= 2; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = TerrainGenerator.Generate(new ChapterRecipe().ForLevel(level),
                                                        DeterministicRandom.SeedFor(chapter, level));

                    int tile = TerrainDecorator.BridgeTile(map.Grid, map.Seed);
                    if (tile < 0) continue;

                    bridged++;
                    Assert.AreEqual(TerrainType.Ford, map.Grid[tile], $"{chapter}-{level}: the bridge is not on a ford");

                    // Asked again, from nothing but the map, it answers the same — which is
                    // what the plan and the run each do.
                    Assert.AreEqual(tile, TerrainDecorator.BridgeTile(map.Grid, map.Seed));
                }

            Assert.Greater(bridged, 0, "no level in two chapters had a bridge to check");
        }
    }
}
