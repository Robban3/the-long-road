using System.Collections.Generic;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using NUnit.Framework;

namespace TheVeil.Tests
{
    /// <summary>
    /// The rules the campaign was built to, checked on the maps it ships.
    ///
    /// Agreed with the user on 2026-09-21 and 2026-09-22, and each one cost a day when it
    /// slipped unnoticed:
    /// - every level is harder than the one before, across chapter boundaries too;
    /// - the fast road looks easiest and is the hardest road on every level;
    /// - it kills the ordinary escort about every other level, chapter one included;
    /// - every road of every level can be won with the right troops, upgraded enough;
    /// - the curve spans all thousand levels and never runs out of room.
    ///
    /// The catalogue builder aims for all of this; these say whether it got there, on the
    /// maps a player is actually given (LevelMaps.For), not on maps generated here.
    /// </summary>
    public class CampaignRulesTests
    {
        static Dictionary<(int, int), LevelMaps.Judgement> _judged;

        /// <summary>Every built level, measured once and shared by the tests below.</summary>
        static Dictionary<(int, int), LevelMaps.Judgement> Judged()
        {
            if (_judged != null) return _judged;

            if (LevelCatalogue.Shipped(1, 1) < 0) LevelCatalogueLoader.Load();

            _judged = new Dictionary<(int, int), LevelMaps.Judgement>();
            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);
                    var recipe = LevelMaps.Recipe(chapter, level);
                    _judged[(chapter, level)] = LevelMaps.Judge(map, chapter, level, recipe.RoutesOwed);
                }

            return _judged;
        }

        [Test]
        public void TheCatalogueIsTakenForEveryBuiltLevel()
        {
            // A catalogue refused for a stale signature makes every level search for its
            // own map, and the rules below would be checked on maps nobody chose.
            if (LevelCatalogue.Shipped(1, 1) < 0) LevelCatalogueLoader.Load();

            Assert.IsNull(LevelCatalogue.Refused, LevelCatalogue.Refused);

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                    Assert.GreaterOrEqual(LevelCatalogue.Shipped(chapter, level), 0,
                                          $"{chapter}-{level} has no map in the catalogue");
        }

        [Test]
        public void EveryLevelIsHarderThanTheOneBefore()
        {
            var judged = Judged();
            float before = -1f;

            // The whole climb in the message, so a failure shows where it bent.
            var climb = new System.Text.StringBuilder();
            foreach (var pair in judged) climb.Append($" {pair.Key.Item1}-{pair.Key.Item2}:{pair.Value.Difficulty:P1}");

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    float difficulty = judged[(chapter, level)].Difficulty;

                    if (before >= 0f)
                        Assert.GreaterOrEqual(difficulty,
                                              before + DifficultyCurve.Rise(chapter, level) - 0.001f,
                                              $"{chapter}-{level} ({difficulty:P1}) is not harder than the level before ({before:P1});"
                                              + climb);

                    before = difficulty;
                }
        }

        [Test]
        public void EveryLevelSitsNearItsPlaceOnTheCurve()
        {
            var judged = Judged();

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    float target = DifficultyCurve.Target(chapter, level);
                    Assert.AreEqual(target, judged[(chapter, level)].Difficulty, DifficultyCurve.Tolerance,
                                    $"{chapter}-{level} is far from its target of {target:P0}");
                }
        }

        [Test]
        public void TheFastRoadIsTheHardestRoadOnEveryLevel()
        {
            var judged = Judged();

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                    Assert.IsTrue(judged[(chapter, level)].Treacherous,
                                  $"{chapter}-{level}: the fast road is kinder than another road");
        }

        [Test]
        public void TheFastRoadKillsAboutEveryOtherLevelInEveryChapter()
        {
            var judged = Judged();

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                int kills = 0;
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                    if (judged[(chapter, level)].FastLost) kills++;

                // About half: never a chapter where the fast road is simply the safe bet,
                // and never one where it is certain death and so no gamble at all.
                Assert.That(kills, Is.InRange(3, 7), $"chapter {chapter}: the fast road killed on {kills} of 10 levels");
            }
        }

        [Test]
        public void EveryRoadOfEveryLevelCanBeWon()
        {
            var judged = Judged();

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var j = judged[(chapter, level)];
                    Assert.IsTrue(j.Hard, $"{chapter}-{level}: a prepared player cannot get down every road "
                                          + $"(the ordinary escort got down {j.Through})");
                }
        }

        [Test]
        public void EveryChapterEndsAtACastleWithNoRoadThroughIt()
        {
            // On 3-10 a road ran under the castle's site, a trap on it stood inside the
            // walls, and the sweep round the trap took the castle down: the chapter ended
            // at an empty goal and nothing said so.
            if (LevelCatalogue.Shipped(1, 1) < 0) LevelCatalogueLoader.Load();

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                var map = LevelMaps.For(chapter, Campaign.LevelsPerChapter);
                var travelled = new HashSet<int>();
                foreach (var corridor in map.Corridors)
                    foreach (int tile in corridor.Tiles) travelled.Add(tile);

                int site = Strongholds.SiteOf(map);
                Assert.GreaterOrEqual(site, 0, $"{chapter}-10 has nowhere for its castle");

                map.Grid.ToCoords(site, out int cx, out int cy);
                foreach (int tile in travelled)
                {
                    map.Grid.ToCoords(tile, out int x, out int y);
                    int dx = x - cx, dy = y - cy;
                    Assert.Greater(dx * dx + dy * dy, Strongholds.Bailey * Strongholds.Bailey,
                                   $"{chapter}-10: a road passes under the castle at {x},{y}");
                }
            }
        }

        [Test]
        public void TheCastleStandsOnTheGroundThatWasLevelledForIt()
        {
            // The ground is levelled in Sim and the castle raised in the view, and each
            // worked out the site for itself - once with the champion's side and once
            // without. Asked the decorator's way, the site has to be level ground.
            if (LevelCatalogue.Shipped(1, 1) < 0) LevelCatalogueLoader.Load();

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                var map = LevelMaps.For(chapter, Campaign.LevelsPerChapter);
                var travelled = new HashSet<int>();
                foreach (var corridor in map.Corridors)
                    foreach (int tile in corridor.Tiles) travelled.Add(tile);

                // What the decorator asks: the same roads, the champion's tile, nothing
                // yet standing (the castle is the first thing it places).
                int site = Strongholds.Site(map.Grid, map.GoalIndex, travelled, new HashSet<int>(),
                                            Champions.Post(map));
                Assert.AreEqual(Strongholds.SiteOf(map), site, $"{chapter}-10: levelled here, built there");

                map.Grid.ToCoords(site, out int cx, out int cy);
                float floor = map.Grid.Elevation(site);
                int uneven = 0, dry = 0;

                for (int dy = -Strongholds.Bailey + 2; dy <= Strongholds.Bailey - 2; dy++)
                    for (int dx = -Strongholds.Bailey + 2; dx <= Strongholds.Bailey - 2; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (!map.Grid.InBounds(x, y)) continue;
                        if (ByTheWater(map.Grid, x, y)) continue;
                        dry++;
                        if (System.Math.Abs(map.Grid.Elevation(map.Grid.ToIndex(x, y)) - floor) > 0.001f) uneven++;
                    }

                Assert.AreEqual(0, uneven, $"{chapter}-10: {uneven} of {dry} tiles under the castle are not level");
            }
        }

        /// <summary>Water, or touching it: the ground Flatten leaves its fall on purpose, so rivers keep their banks.</summary>
        static bool ByTheWater(TileGrid grid, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!grid.InBounds(x + dx, y + dy)) continue;
                    var terrain = grid[x + dx, y + dy];
                    if (terrain == TerrainType.Water || terrain == TerrainType.Ford) return true;
                }

            return false;
        }

        [Test]
        public void TheCurveRisesOnEveryLevelOfTheThousand()
        {
            int levels = DifficultyCurve.Levels;
            Assert.AreEqual(1000, levels);

            float before = -1f;
            for (int index = 0; index < levels; index++)
            {
                int chapter = index / Campaign.LevelsPerChapter + 1;
                int level = index % Campaign.LevelsPerChapter + 1;
                float target = DifficultyCurve.Target(chapter, level);

                Assert.Greater(target, before, $"level {index + 1} does not rise");
                Assert.LessOrEqual(target, DifficultyCurve.Last + 0.0001f, $"level {index + 1} is past the last");
                if (index > 0) Assert.Greater(DifficultyCurve.Rise(chapter, level), 0f, $"level {index + 1} has no rise");

                before = target;
            }

            Assert.AreEqual(DifficultyCurve.First, DifficultyCurve.Target(1, 1), 0.0001f);
            Assert.AreEqual(DifficultyCurve.Last, before, 0.0001f);
        }
    }
}
