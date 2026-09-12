using TheVeil.Sim;
using TheVeil.View;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// The ground colours per country. Each country the game paints needs a full row of
    /// them — one short and a tile type reads past the end of the table the first time a
    /// level puts that ground in that country.
    /// </summary>
    public class TerrainPaletteTests
    {
        [Test]
        public void EveryPaintedCountryHasAColourForEveryGround()
        {
            foreach (Biome biome in System.Enum.GetValues(typeof(Biome)))
                foreach (TerrainType terrain in System.Enum.GetValues(typeof(TerrainType)))
                {
                    var colour = TerrainPalette.OfGround(terrain, biome);
                    Assert.Greater(colour.a, 0f, $"{biome} {terrain} is transparent ground");
                }
        }

        [Test]
        public void TheFenIsItsOwnGroundAndNotTheForestsWithPuddles()
        {
            foreach (var terrain in new[] { TerrainType.Plains, TerrainType.Forest, TerrainType.Marsh })
            {
                var forest = TerrainPalette.OfGround(terrain, Biome.Forest);
                var marsh = TerrainPalette.OfGround(terrain, Biome.Marsh);

                Assert.AreNotEqual(forest, marsh, $"{terrain} is the same colour in the fen as in the wood");

                // Browner and darker: a bog is peat, and peat is not a lawn.
                Assert.LessOrEqual(marsh.g, forest.g + 0.001f, $"{terrain} is greener in the fen");
            }
        }

        [Test]
        public void WaterStaysFarDarkerThanItsBanksInEveryCountry()
        {
            // The one rule every palette keeps: a river the player cannot see is a river
            // they cannot plan round.
            foreach (Biome biome in System.Enum.GetValues(typeof(Biome)))
            {
                var water = TerrainPalette.OfGround(TerrainType.Water, biome);
                var bank = TerrainPalette.OfGround(TerrainType.Plains, biome);

                float waterLight = water.r + water.g + water.b;
                float bankLight = bank.r + bank.g + bank.b;

                Assert.Less(waterLight, bankLight - 0.2f, $"{biome}: the water does not stand out from its bank");
            }
        }

        [Test]
        public void AnUnpaintedCountryFallsBackToTheForest()
        {
            // Desert ground is not painted yet, and the fallback is what keeps a chapter
            // set there playable rather than blank. The day somebody paints it, this test
            // says so by failing.
            foreach (TerrainType terrain in System.Enum.GetValues(typeof(TerrainType)))
                Assert.AreEqual(TerrainPalette.OfGround(terrain, Biome.Forest),
                                TerrainPalette.OfGround(terrain, Biome.Desert),
                                $"{terrain}");
        }
    }
}
