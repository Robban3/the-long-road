using System;
using System.Collections.Generic;

namespace Arna.Sim
{
    public enum WildlifeKind : byte
    {
        Fox = 0,
        DeerFemale = 1,
        DeerMale = 2,
        Boar = 3
    }

    /// <summary>
    /// One animal, where it grazes and where it is now.
    ///
    /// It has no health and cannot be fought. That is deliberate: the moment an animal
    /// can be killed it becomes a resource, and a player who stops the caravan to farm
    /// deer is playing a different game than the one docs/GDD.md describes. It exists to
    /// make the country look inhabited and to react when the country stops being calm.
    /// </summary>
    public sealed class WildAnimal
    {
        public WildlifeKind Kind;

        /// <summary>Where it belongs. It drifts around this and returns to it.</summary>
        public Vec2 Home;

        public Vec2 Position;

        /// <summary>Seconds of running left. Zero means it is grazing.</summary>
        public float Fleeing;

        /// <summary>
        /// Which way it is looking.
        ///
        /// Given a value when it is placed rather than left at zero, because an animal
        /// left facing +Z is an animal facing the same way as every other animal on the
        /// level — a field of deer in parade order, which reads as a spawner and not as
        /// wildlife. While it flees this is the direction it bolted in.
        /// </summary>
        public Vec2 Heading;

        public bool IsFleeing => Fleeing > 0f;
    }

    /// <summary>
    /// Deer, foxes and boar that scatter when the caravan comes near or a fight starts
    /// (docs/GDD.md §3.5).
    ///
    /// The point is not decoration, though it is that too. The soft signals ask the
    /// player to read the country, and a country where nothing moves except what is
    /// hunting you teaches the eye that movement means danger. Animals that bolt for
    /// their own reasons put noise into that channel — and noise is what makes reading
    /// it a skill rather than a lookup.
    ///
    /// They are also the cheapest possible tell that something has gone wrong somewhere
    /// you are not looking. Deer breaking from a wood you have not reached yet is not a
    /// mechanic the game has to explain.
    /// </summary>
    public static class Wildlife
    {
        /// <summary>
        /// Animals on a level.
        ///
        /// Measured rather than felt. Over nine levels, walking the fast route end to
        /// end past a 26 m spook radius:
        ///
        ///     animals   scattered per run   in sight within 80 m
        ///        14            2.3                  7.3
        ///        26            3.8                 14.4
        ///        44            6.8                 25.0
        ///
        /// Fourteen was the first guess and it is genuinely sparse — a whole run can go
        /// by with nothing in frame, which for a signal that works by being noticed is
        /// the same as not existing. Twenty-six is about one animal in view at a time.
        /// Forty-four is a zoo, and a country teeming with deer stops saying anything
        /// when some of them bolt.
        /// </summary>
        public const int Count = 26;

        /// <summary>Metres the caravan has to close before an animal bolts.</summary>
        public const float SpookRadius = 26f;

        /// <summary>
        /// Metres from a fight at which animals scatter, whether or not it is near them.
        ///
        /// Wider than the caravan's own radius because a fight is louder than a cart.
        ///
        /// Not because it is somewhere else: every fight in this game happens at the
        /// caravan, since the escort is what the enemies come for. Two radii on one
        /// point, and what the player sees is the ring of startled country widening the
        /// moment blades come out.
        /// </summary>
        public const float BattleRadius = 55f;

        /// <summary>Seconds of running once startled.</summary>
        public const float FleeSeconds = 4.5f;

        public const float FleeSpeed = 11f;

        /// <summary>Metres it wanders from home while calm.</summary>
        public const float GrazeRadius = 6f;

        const float GrazeSpeed = 0.9f;

        /// <summary>
        /// Scatters animals over passable ground, away from what will kill the mood.
        ///
        /// Not near the road the caravan must take, and not on top of an enemy group:
        /// a fox grazing inside a bandit camp is a joke, and a deer standing on the
        /// start tile is in the way of the first thing the player does.
        /// </summary>
        public static List<WildAnimal> Populate(LevelMap map)
        {
            var animals = new List<WildAnimal>();
            if (map?.Grid == null) return animals;

            var grid = map.Grid;
            var rng = new DeterministicRandom(map.Seed ^ 0x1EAF);

            for (int attempt = 0; attempt < Count * 40 && animals.Count < Count; attempt++)
            {
                int tile = rng.Range(0, grid.TileCount);
                grid.ToCoords(tile, out int x, out int y);

                if (!grid.IsPassable(x, y)) continue;
                if (grid[tile] == TerrainType.Ford) continue;
                if (tile == map.StartIndex || tile == map.GoalIndex) continue;
                if (NearAnEnemy(map, x, y)) continue;

                var home = Vec2.FromTile(grid, tile);
                float facing = rng.Range(0f, (float)(2.0 * Math.PI));

                animals.Add(new WildAnimal
                {
                    Kind = Pick(grid[tile], rng),
                    Home = home,
                    Position = home,
                    Heading = new Vec2((float)Math.Sin(facing), (float)Math.Cos(facing))
                });
            }

            return animals;
        }

        /// <summary>
        /// Moves the animals on. `battles` is where fighting is happening this frame.
        /// </summary>
        public static void Step(IReadOnlyList<WildAnimal> animals, Vec2 caravan,
                                IReadOnlyList<Vec2> battles, float dt)
        {
            if (animals == null || dt <= 0f) return;

            foreach (var animal in animals)
            {
                if (animal.Fleeing <= 0f && Startled(animal, caravan, battles, out var away))
                {
                    animal.Fleeing = FleeSeconds;
                    animal.Heading = away;
                }

                if (animal.Fleeing > 0f)
                {
                    animal.Fleeing -= dt;
                    animal.Position = new Vec2(animal.Position.X + animal.Heading.X * FleeSpeed * dt,
                                               animal.Position.Y + animal.Heading.Y * FleeSpeed * dt);
                    continue;
                }

                // Calm: drift back towards home rather than standing still. An animal
                // frozen where its flight ended reads as a bug, and one that never
                // returns leaves the level emptier every minute.
                var toHome = new Vec2(animal.Home.X - animal.Position.X,
                                      animal.Home.Y - animal.Position.Y);
                float distance = (float)Math.Sqrt(toHome.X * toHome.X + toHome.Y * toHome.Y);
                if (distance <= GrazeRadius) continue;

                animal.Position = new Vec2(animal.Position.X + toHome.X / distance * GrazeSpeed * dt,
                                           animal.Position.Y + toHome.Y / distance * GrazeSpeed * dt);
            }
        }

        static bool Startled(WildAnimal animal, Vec2 caravan, IReadOnlyList<Vec2> battles,
                             out Vec2 away)
        {
            away = animal.Heading;

            if (Within(animal.Position, caravan, SpookRadius))
                return Direction(caravan, animal.Position, out away);

            if (battles != null)
                foreach (var battle in battles)
                    if (Within(animal.Position, battle, BattleRadius))
                        return Direction(battle, animal.Position, out away);

            return false;
        }

        /// <summary>Straight away from whatever startled it. A frightened animal is not clever.</summary>
        static bool Direction(Vec2 from, Vec2 to, out Vec2 away)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            // Standing exactly on it: pick a fixed direction rather than divide by zero.
            away = length < 0.001f ? new Vec2(0f, 1f) : new Vec2(dx / length, dy / length);
            return true;
        }

        static bool Within(Vec2 a, Vec2 b, float radius)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy <= radius * radius;
        }

        static bool NearAnEnemy(LevelMap map, int x, int y)
        {
            if (map.Encounters == null) return false;

            foreach (var spawn in map.Encounters.Enemies)
            {
                map.Grid.ToCoords(spawn.Tile, out int gx, out int gy);
                float dx = gx - x, dy = gy - y;
                if (dx * dx + dy * dy <= 5f * 5f) return true;
            }

            return false;
        }

        /// <summary>Deer in the open, foxes and boar under cover. Roughly true, and it reads.</summary>
        static WildlifeKind Pick(TerrainType terrain, DeterministicRandom rng)
        {
            if (terrain == TerrainType.Forest)
                return rng.Chance(0.45f) ? WildlifeKind.Fox : WildlifeKind.Boar;

            if (terrain == TerrainType.Marsh)
                return WildlifeKind.Boar;

            return rng.Chance(0.5f) ? WildlifeKind.DeerFemale : WildlifeKind.DeerMale;
        }
    }
}
