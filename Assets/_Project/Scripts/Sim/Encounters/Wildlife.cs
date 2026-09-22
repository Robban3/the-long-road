using System;
using System.Collections.Generic;

namespace TheVeil.Sim
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

        /// <summary>
        /// How fast it is actually moving, in metres a second.
        ///
        /// <b>Because the view was guessing and guessing wrong.</b> It animated an animal
        /// at the flight speed while fleeing and at zero otherwise — but "otherwise"
        /// covers an animal walking home at GrazeSpeed, which came out sliding across the
        /// grass with no legs moving. Only the step knows which of the three things an
        /// animal is doing this tick, so only the step can say.
        /// </summary>
        public float Speed;
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
        /// the same as not existing. Forty-four is a zoo, and a country teeming with
        /// deer stops saying anything when some of them bolt.
        ///
        /// Thirty-four, with <see cref="ForestShare"/> keeping most of them out from
        /// under the canopy, puts about fifteen where they can actually be seen.
        /// </summary>
        public const int Count = 44;

        /// <summary>
        /// How many separate sightings the level places, before herds are counted.
        ///
        /// The cap used to be on **animals**, and grouping the deer broke it: a deer draw
        /// places two to four where a fox or a boar places one, so the deer ate the
        /// budget. Measured on a run: 21 does and 8 stags against 2 foxes and 3 boar —
        /// a level of deer with a rumour of anything else, and the terrain that was
        /// supposed to decide the mix decided nothing.
        ///
        /// Counting sightings instead puts the choice back where `Pick` makes it: sixteen
        /// pieces of ground get an animal, and what kind depends on what the ground is.
        /// <see cref="Count"/> stays as the hard ceiling, because a draw call budget does
        /// not care how the animals were chosen.
        /// </summary>
        public const int Sightings = 16;

        /// <summary>
        /// How many animals stand together where the ground allows it.
        ///
        /// Deer are drawn in twos and threes and the boar and the fox are not, which is
        /// both true of them and the thing that makes them visible. Thirty-four animals
        /// over a 256-metre map is one per nineteen hundred square metres: from a camera
        /// that sees perhaps a hundred metres by sixty, that is two or three in frame,
        /// each on its own, each hidden behind the next tree.
        ///
        /// Grouped, the same thirty-four make a dozen sightings instead of thirty-four
        /// misses. A herd is seen where a single deer is not — three animals moving
        /// together read as one event large enough to notice, and the eye is far better
        /// at catching a group than an individual.
        /// </summary>
        public const int HerdMin = 2;
        public const int HerdMax = 4;

        /// <summary>Metres a herd's members stand from the ground the herd was placed on.</summary>
        public const float HerdSpread = 5f;

        /// <summary>
        /// How often a forest tile is accepted at all.
        ///
        /// Counting animals was measuring the wrong thing. At twenty-six placed evenly,
        /// fourteen came within sight of the caravan over a run and only *seven* of
        /// those stood anywhere they could be seen — the rest were under a canopy that
        /// hides a deer completely from a camera thirty-five degrees above it. An animal
        /// nobody can see is not sparse, it is absent.
        ///
        /// A third rather than none, because foxes and boar belong in a wood, and
        /// hiding is a thing animals do. Pushing it lower bought almost nothing:
        ///
        ///     placed   forest share   in sight   of those, in the open
        ///       26         all           14.3            7.6
        ///       26         0.35          15.0           11.6
        ///       34         0.35          19.8           15.4
        ///       34         0.20          19.8           17.2
        /// </summary>
        public const float ForestShare = 0.35f;

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
        public static List<WildAnimal> Populate(LevelMap map, ObstacleField obstacles = null)
        {
            var animals = new List<WildAnimal>();
            if (map?.Grid == null) return animals;

            var grid = map.Grid;
            var rng = new DeterministicRandom(map.Seed ^ 0x1EAF);

            int sightings = 0;

            for (int attempt = 0; attempt < Sightings * 40
                                  && sightings < Sightings && animals.Count < Count; attempt++)
            {
                int tile = rng.Range(0, grid.TileCount);
                grid.ToCoords(tile, out int x, out int y);

                if (!grid.IsPassable(x, y)) continue;
                if (grid[tile] == TerrainType.Ford) continue;
                if (tile == map.StartIndex || tile == map.GoalIndex) continue;
                if (NearAnEnemy(map, x, y)) continue;
                if (grid[tile] == TerrainType.Forest && !rng.Chance(ForestShare)) continue;

                // Out of any trunk the tile's centre happens to be inside. See Body.
                var found = Vec2.FromTile(grid, tile);
                if (obstacles != null)
                {
                    found = obstacles.Clear(found, Body);
                    if (!Standable(grid, found, obstacles)) continue;
                }

                var kind = Pick(grid[tile], rng);

                // Deer keep company. A boar is a boar and a fox hunts alone, which is
                // true of them and also keeps the herd from becoming the only thing on
                // the map.
                int herd = kind == WildlifeKind.DeerFemale || kind == WildlifeKind.DeerMale
                    ? rng.Range(HerdMin, HerdMax + 1)
                    : 1;

                sightings++;

                for (int i = 0; i < herd && animals.Count < Count; i++)
                {
                    // The first stands where the ground was chosen; the rest around it —
                    // and the rest are checked, which the first always was.
                    //
                    // The spread is a few metres either way and a few metres from a
                    // chosen tile is a neighbouring one, which may be water, a cliff, or
                    // off the map altogether. The last of those is not a doe standing in
                    // a river, it is an index out of range: a herd chosen near the edge
                    // put a companion outside the grid and the first thing to ask where
                    // it was standing threw.
                    var home = found;

                    for (int t = 0; i > 0 && t < 8; t++)
                    {
                        var spot = new Vec2(found.X + rng.Range(-HerdSpread, HerdSpread),
                                            found.Y + rng.Range(-HerdSpread, HerdSpread));

                        if (!Standable(grid, spot, obstacles)) continue;

                        // And clear of the enemy, which only the founder ever was.
                        //
                        // The founder is checked in tile coordinates and stands on its
                        // tile's centre, so five tiles between indices really is five
                        // tiles between positions. A companion is offset by up to
                        // HerdSpread on each axis — seven metres on the diagonal — and
                        // was checked for standable ground and nothing else, so it could
                        // walk most of that straight at the camp. Measured: a doe 15.84 m
                        // from a group where the rule promises sixteen.
                        //
                        // In metres here rather than tiles, because a companion's home is
                        // a position and not a tile, and a rule enforced in one unit and
                        // promised in another is the whole of this bug.
                        if (NearAnEnemy(map, spot)) continue;

                        home = spot;
                        break;
                    }

                    float facing = rng.Range(0f, (float)(2.0 * Math.PI));

                    animals.Add(new WildAnimal
                    {
                        // A herd is does and one stag rather than a row of stags.
                        Kind = i == 0 ? kind : Companion(kind, rng),
                        Home = home,
                        Position = home,
                        Heading = new Vec2((float)Math.Sin(facing), (float)Math.Cos(facing))
                    });
                }
            }

            return animals;
        }

        /// <summary>
        /// Moves the animals on. `battles` is where fighting is happening this frame.
        /// </summary>
        public static void Step(TileGrid grid, IReadOnlyList<WildAnimal> animals, Vec2 caravan,
                                IReadOnlyList<Vec2> battles, float dt, ObstacleField obstacles = null)
        {
            if (animals == null || dt <= 0f) return;

            foreach (var animal in animals)
            {
                // One that starts inside a trunk would refuse every step out of it and
                // stand there for the level; pushed clear first, as Squad does its troops.
                if (obstacles != null && obstacles.Blocked(animal.Position, Body))
                    animal.Position = obstacles.Clear(animal.Position, Body);

                if (animal.Fleeing <= 0f && Startled(animal, caravan, battles, out var away))
                {
                    animal.Fleeing = FleeSeconds;
                    animal.Heading = away;
                }

                if (animal.Fleeing > 0f)
                {
                    animal.Fleeing -= dt;
                    animal.Speed = FleeSpeed;
                    Walk(grid, animal, FleeSpeed, dt, obstacles);
                    continue;
                }

                // Calm: drift back towards home rather than standing still. An animal
                // frozen where its flight ended reads as a bug, and one that never
                // returns leaves the level emptier every minute.
                var toHome = new Vec2(animal.Home.X - animal.Position.X,
                                      animal.Home.Y - animal.Position.Y);
                float distance = (float)Math.Sqrt(toHome.X * toHome.X + toHome.Y * toHome.Y);

                if (distance <= GrazeRadius)
                {
                    animal.Speed = 0f;
                    continue;
                }

                // <b>Turned the way it is walking, which this never did.</b> Heading was
                // set when the animal bolted and then left alone, so an animal walking
                // home afterwards faced wherever it had run *from* — moving one way and
                // pointing another, which is the sliding. Nothing else here knows the
                // direction: the view is handed a heading and trusts it.
                animal.Heading = new Vec2(toHome.X / distance, toHome.Y / distance);
                animal.Speed = GrazeSpeed;

                Walk(grid, animal, GrazeSpeed, dt, obstacles);
            }
        }

        /// <summary>
        /// Moves an animal along its heading as far as the ground allows, swerving rather
        /// than stopping when it runs out of it.
        ///
        /// <b>Nothing here had ever looked at the ground.</b> Step advanced a position by
        /// a heading and a speed and that was all of it, so a startled deer bolted along
        /// whatever bearing it happened to be given — over the lake, across the ford the
        /// caravan is queueing for, off the cliff. The rule it needed was already in this
        /// file: <see cref="Standable"/>, written for placing a herd and never asked
        /// again once one was moving.
        ///
        /// Swerving and not stopping, because a frightened animal that hits a shoreline
        /// and freezes reads exactly as wrong as one that runs out into the water. It
        /// turns by thirty-five degrees at a time, either way, and takes the first bearing
        /// with ground on it — which is also what a deer does at a river. Boxed in on
        /// every side it stops, and that at least is a thing an animal does.
        /// </summary>
        static void Walk(TileGrid grid, WildAnimal animal, float speed, float dt, ObstacleField obstacles)
        {
            if (Try(grid, animal, animal.Heading, speed, dt, obstacles)) return;

            foreach (float turn in Swerves)
            {
                if (Try(grid, animal, Turn(animal.Heading, turn), speed, dt, obstacles, keep: true)) return;
                if (Try(grid, animal, Turn(animal.Heading, -turn), speed, dt, obstacles, keep: true)) return;
            }

            animal.Speed = 0f;
        }

        /// <summary>One candidate step. Taken only if it lands on ground.</summary>
        static bool Try(TileGrid grid, WildAnimal animal, Vec2 heading, float speed, float dt,
                        ObstacleField obstacles, bool keep = false)
        {
            var step = new Vec2(animal.Position.X + heading.X * speed * dt,
                                animal.Position.Y + heading.Y * speed * dt);

            if (grid != null && !Standable(grid, step, obstacles)) return false;

            if (keep) animal.Heading = heading;
            animal.Position = step;
            return true;
        }

        /// <summary>A heading turned by a number of degrees.</summary>
        static Vec2 Turn(Vec2 heading, float degrees)
        {
            float radians = degrees * (float)(Math.PI / 180.0);
            float cos = (float)Math.Cos(radians), sin = (float)Math.Sin(radians);

            return new Vec2(heading.X * cos - heading.Y * sin,
                            heading.X * sin + heading.Y * cos);
        }

        /// <summary>
        /// How far an animal will turn off its bearing to find ground, in degrees.
        ///
        /// Tried nearest first and to both sides, so an animal running at a shore veers
        /// along it rather than reversing into whatever it was running from. The last one
        /// is most of the way round: that is the animal cornered against water with the
        /// caravan behind it, and doubling back is better than standing in the shallows.
        /// </summary>
        static readonly float[] Swerves = { 35f, 70f, 105f, 140f, 175f };

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

        /// <summary>
        /// How wide an animal is, in metres from its middle, when it comes to trees.
        ///
        /// <b>Animals walked straight through trees</b>, because nothing here knew where a
        /// tree was: Standable asked the tile and the tile is "forest", which is ground. The
        /// trunks are known - the decorator marks every solid thing it plants and the run
        /// keeps them as an ObstacleField, which the troops have walked round for a long
        /// time. Now the animals are handed the same one.
        /// </summary>
        public const float Body = 0.6f;

        /// <summary>Whether a world position is on ground an animal could stand on.</summary>
        static bool Standable(TileGrid grid, Vec2 at, ObstacleField obstacles = null)
        {
            if (obstacles != null && obstacles.Blocked(at, Body)) return false;

            // Off the west or south edge entirely. Worth saying out loud because the cast
            // below truncates towards zero, so a position at minus one metre lands on tile
            // nought and is waved through — which is ground, just not the ground it is on.
            if (at.X < 0f || at.Y < 0f) return false;

            int x = (int)(at.X / TileGrid.TileSize);
            int y = (int)(at.Y / TileGrid.TileSize);

            if (!grid.InBounds(x, y) || !grid.IsPassable(x, y)) return false;

            return grid[grid.ToIndex(x, y)] != TerrainType.Ford;
        }

        /// <summary>Tiles an animal keeps between itself and any enemy group.</summary>
        public const float ClearOfEnemiesTiles = 5f;

        static bool NearAnEnemy(LevelMap map, int x, int y)
        {
            if (map.Encounters == null) return false;

            foreach (var spawn in map.Encounters.Enemies)
            {
                map.Grid.ToCoords(spawn.Tile, out int gx, out int gy);
                float dx = gx - x, dy = gy - y;
                if (dx * dx + dy * dy <= ClearOfEnemiesTiles * ClearOfEnemiesTiles) return true;
            }

            return false;
        }

        /// <summary>The same rule for a position rather than a tile — see the herd loop.</summary>
        static bool NearAnEnemy(LevelMap map, Vec2 at)
        {
            if (map.Encounters == null) return false;

            float clear = ClearOfEnemiesTiles * TileGrid.TileSize;

            foreach (var spawn in map.Encounters.Enemies)
                if (Within(at, Vec2.FromTile(map.Grid, spawn.Tile), clear)) return true;

            return false;
        }

        /// <summary>Deer in the open, foxes and boar under cover. Roughly true, and it reads.</summary>
        /// <summary>
        /// What stands beside the animal a herd was founded on.
        ///
        /// Does, mostly. A field of stags is a trophy room; one set of antlers among
        /// three hinds is a herd, and it is the antlers that carry from a distance.
        /// </summary>
        static WildlifeKind Companion(WildlifeKind founder, DeterministicRandom rng)
        {
            if (founder != WildlifeKind.DeerFemale && founder != WildlifeKind.DeerMale)
                return founder;

            return rng.Chance(0.8f) ? WildlifeKind.DeerFemale : WildlifeKind.DeerMale;
        }

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
