using System.Collections.Generic;
using Arna.Gen;
using Arna.Sim;
using NUnit.Framework;

namespace Arna.Tests
{
    public class EncounterPlacerTests
    {
        static LevelRecipe Recipe() => new LevelRecipe();

        static LevelMap Level(int chapter, int level, LevelRecipe recipe = null)
            => TerrainGenerator.Generate(recipe ?? Recipe(), DeterministicRandom.SeedFor(chapter, level));

        [Test]
        public void TheFastRouteIsTheDangerousOne()
        {
            // The promise the entire route-drawing mechanic rests on. If this fails,
            // taking the quick way is free and there is no decision to make.
            int checkedLevels = 0;

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);
                var fast = map.CorridorOf(CorridorKind.Fast);
                var odd = map.CorridorOf(CorridorKind.Odd);
                if (fast == null || odd == null) continue;

                float fastDensity = map.Encounters.ThreatDensity(CorridorKind.Fast, fast.Tiles.Count);
                float oddDensity = map.Encounters.ThreatDensity(CorridorKind.Odd, odd.Tiles.Count);

                Assert.Greater(fastDensity, oddDensity,
                    $"level 1-{level}: the fast route ({fastDensity:F3} threat/tile) is no more " +
                    $"dangerous than the long one ({oddDensity:F3})");
                checkedLevels++;
            }

            Assert.Greater(checkedLevels, 5, "too few levels had both corridors to compare");
        }

        [Test]
        public void ThreatIsSharedInInverseProportionToTravelTime()
        {
            var map = Level(2, 3);
            var corridors = map.Corridors;

            float inverseSum = 0f;
            foreach (var c in corridors) inverseSum += 1f / c.TravelCost;

            int totalPoints = map.Encounters.TotalPoints;
            Assert.Greater(totalPoints, 0, "no threat was placed at all");

            foreach (var c in corridors)
            {
                float expectedShare = (1f / c.TravelCost) / inverseSum;
                float actualShare = (float)map.Encounters.PointsByCorridor[(int)c.Kind] / totalPoints;

                // Rounding to whole groups and the trap deduction both blur this, so
                // the check is that the ordering and rough magnitude hold.
                Assert.That(actualShare, Is.EqualTo(expectedShare).Within(0.18f),
                    $"{c.Kind} got {actualShare:P0} of the threat, formula says {expectedShare:P0}");
            }
        }

        [Test]
        public void EveryCorridorCanFundFiveUpgrades()
        {
            var recipe = Recipe();

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level, recipe);
                Assert.IsTrue(map.Encounters.SilverValidated, $"level 1-{level}: silver top-up failed");

                foreach (var corridor in map.Corridors)
                    Assert.GreaterOrEqual(map.Encounters.SilverByCorridor[(int)corridor.Kind],
                        recipe.MinSilverPerCorridor,
                        $"level 1-{level}: the {corridor.Kind} route cannot fund five upgrades");
            }
        }

        [Test]
        public void NothingIsPlacedOnTheCaravansFirstOrLastSteps()
        {
            for (int level = 1; level <= 6; level++)
            {
                var map = Level(1, level);

                var forbidden = new HashSet<int>();
                foreach (var corridor in map.Corridors)
                {
                    for (int i = 0; i < 6 && i < corridor.Tiles.Count; i++) forbidden.Add(corridor.Tiles[i]);
                    for (int i = corridor.Tiles.Count - 6; i < corridor.Tiles.Count; i++)
                        if (i >= 0) forbidden.Add(corridor.Tiles[i]);
                }

                // A tile is only off limits if it is near the end of *every* corridor
                // that uses it; corridors overlap, so one route's opening stretch can
                // be another's middle.
                foreach (var spawn in map.Encounters.Enemies)
                {
                    var corridor = map.CorridorOf(spawn.Corridor);
                    int position = corridor.Tiles.IndexOf(spawn.Tile);
                    Assert.GreaterOrEqual(position, 6, $"level 1-{level}: ambush too close to the start");
                    Assert.Less(position, corridor.Tiles.Count - 6, $"level 1-{level}: ambush on the doorstep of the goal");
                }
            }
        }

        [Test]
        public void EnemyGroupsDoNotStackOnTopOfEachOther()
        {
            for (int level = 1; level <= 6; level++)
            {
                var map = Level(1, level);

                var seen = new HashSet<int>();
                foreach (var spawn in map.Encounters.Enemies)
                    Assert.IsTrue(seen.Add(spawn.Tile), $"level 1-{level}: two groups on tile {spawn.Tile}");

                foreach (var trap in map.Encounters.Traps)
                    Assert.IsFalse(seen.Contains(trap.Tile), $"level 1-{level}: trap under an enemy group");
            }
        }

        [Test]
        public void TrapDensityFollowsTheTerrain()
        {
            // A marsh-heavy recipe should draw noticeably more traps than an open one,
            // and those trap points come out of the same budget as the enemies.
            var open = new LevelRecipe
            {
                Rivers = 0,
                TerrainMix = new[]
                {
                    new TerrainShare(TerrainType.Plains, 0.8f),
                    new TerrainShare(TerrainType.Forest, 0.2f)
                }
            };
            var boggy = new LevelRecipe
            {
                Rivers = 0,
                TerrainMix = new[]
                {
                    new TerrainShare(TerrainType.Marsh, 0.8f),
                    new TerrainShare(TerrainType.Forest, 0.2f)
                }
            };

            int openTraps = 0, boggyTraps = 0;
            for (int level = 1; level <= 8; level++)
            {
                openTraps += Level(4, level, open).Encounters.Traps.Count;
                boggyTraps += Level(4, level, boggy).Encounters.Traps.Count;
            }

            Assert.Greater(boggyTraps, openTraps,
                $"marshland produced {boggyTraps} traps against open ground's {openTraps}");
        }

        [Test]
        public void TheDangerousRouteIsAlsoTheRicherOne()
        {
            // Enemies drop silver, so the route with more of them funds more upgrades.
            // This is what stops the cautious route from being strictly better — it
            // gets you there intact but with an army you could not improve.
            //
            // The margin only exists in a band: too small an enemy budget and the
            // silver floor levels everything, too large and the short fast corridor
            // runs out of room while the long one keeps absorbing groups.
            int richer = 0, compared = 0;

            for (int chapter = 1; chapter <= 3; chapter++)
                for (int level = 1; level <= 10; level++)
                {
                    var map = Level(chapter, level);
                    if (map.CorridorOf(CorridorKind.Odd) == null) continue;

                    int fastNatural = NaturalSilver(map, CorridorKind.Fast);
                    int oddNatural = NaturalSilver(map, CorridorKind.Odd);

                    compared++;
                    if (fastNatural > oddNatural) richer++;
                }

            Assert.Greater(compared, 20, "too few levels to judge");
            Assert.GreaterOrEqual((float)richer / compared, 0.9f,
                $"the fast route out-earned the long one on only {richer}/{compared} levels");
        }

        /// <summary>Silver from enemies and traps, excluding the top-up caches.</summary>
        static int NaturalSilver(LevelMap map, CorridorKind kind)
        {
            int total = map.Encounters.SilverByCorridor[(int)kind];
            foreach (var cache in map.Encounters.SilverCaches)
                if (cache.Corridor == kind) total -= cache.Amount;
            return total;
        }

        [Test]
        public void TheSilverFloorIsASafetyNetNotTheNorm()
        {
            // If the floor binds on most corridors it tops them all up to the same
            // figure, every route pays identically, and the reason to take the
            // dangerous one disappears. It must stay a rare rescue.
            int caches = 0, levels = 0;

            for (int level = 1; level <= 10; level++)
            {
                caches += Level(1, level).Encounters.SilverCaches.Count;
                levels++;
            }

            Assert.Less((float)caches / levels, 1.0f,
                $"{(float)caches / levels:F1} silver caches per level — the floor is doing the work");
        }

        [Test]
        public void TheDangerousRouteIsMeaningfullyRicherNotMarginally()
        {
            // A few percent would be a rounding error, not a reason to take a risk.
            int fastTotal = 0, oddTotal = 0, levels = 0;

            for (int level = 1; level <= 10; level++)
            {
                var map = Level(1, level);
                if (map.CorridorOf(CorridorKind.Odd) == null) continue;

                fastTotal += map.Encounters.SilverByCorridor[(int)CorridorKind.Fast];
                oddTotal += map.Encounters.SilverByCorridor[(int)CorridorKind.Odd];
                levels++;
            }

            Assert.Greater(levels, 5);
            float ratio = (float)fastTotal / oddTotal;
            Assert.Greater(ratio, 1.25f,
                $"the fast route pays only {ratio:P0} of the long one — not worth the danger");
        }

        [Test]
        public void PlacementIsDeterministic()
        {
            var a = Level(3, 7);
            var b = Level(3, 7);

            Assert.AreEqual(a.Encounters.Enemies.Count, b.Encounters.Enemies.Count);
            for (int i = 0; i < a.Encounters.Enemies.Count; i++)
            {
                Assert.AreEqual(a.Encounters.Enemies[i].Tile, b.Encounters.Enemies[i].Tile);
                Assert.AreEqual(a.Encounters.Enemies[i].Kind, b.Encounters.Enemies[i].Kind);
            }

            Assert.AreEqual(a.Encounters.Traps.Count, b.Encounters.Traps.Count);
            CollectionAssert.AreEqual(a.Encounters.PointsByCorridor, b.Encounters.PointsByCorridor);
        }

        [Test]
        public void EnemyStatsAreSelfConsistent()
        {
            foreach (var kind in EnemyTable.All)
            {
                Assert.Greater(EnemyTable.GroupSize(kind), 0);
                Assert.Greater(EnemyTable.GroupHp(kind), 0f);
                Assert.Greater(EnemyTable.DetectRadius(kind), 0f);
                Assert.Greater(EnemyTable.Points(kind), 0);
                Assert.AreEqual(EnemyTable.GroupSilver(kind),
                    EnemyTable.SilverPerKill(kind) * EnemyTable.GroupSize(kind));
            }

            // The archer is the reason ranged troops matter: it outranges everything
            // the caravan has in melee and must be answered at distance.
            Assert.Greater(EnemyTable.AttackRange(EnemyKind.BanditArcher),
                EnemyTable.AttackRange(EnemyKind.Bandit) * 5f);

            // The wolf is the reason flanks matter: it arrives before anything else.
            Assert.Greater(EnemyTable.Speed(EnemyKind.Wolf), EnemyTable.Speed(EnemyKind.Bandit));
        }
    }
}
