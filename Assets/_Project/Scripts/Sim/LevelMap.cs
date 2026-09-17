using System.Collections.Generic;

namespace TheVeil.Sim
{
    /// <summary>
    /// A generated level: terrain, the caravan's entry and exit points, and the
    /// corridors that prove the route choice is real.
    /// Produced by TheVeil.Gen, consumed by both the simulation and the view.
    /// </summary>
    public sealed class LevelMap
    {
        public readonly TileGrid Grid;
        public readonly int StartX, StartY;
        public readonly int GoalX, GoalY;

        /// <summary>The seed this map was generated from. Levels are identified by it everywhere.</summary>
        public readonly int Seed;

        /// <summary>Travel cost of the fastest possible route, used to derive par time.</summary>
        public readonly float FastestRouteCost;

        /// <summary>Fast, safe and odd routes through this map.</summary>
        public readonly IReadOnlyList<Corridor> Corridors;

        /// <summary>
        /// False when no seed in the retry budget produced three meaningfully
        /// different routes and the best candidate was accepted anyway. Levels
        /// shipping with this false should be visible in generator reports — they
        /// are the ones where the player's route choice does not matter.
        /// </summary>
        public readonly bool ChoiceValidated;

        /// <summary>How many seeds were tried. High values point at a bad recipe.</summary>
        public readonly int Attempts;

        /// <summary>Enemies, traps and silver placed across the corridors.</summary>
        public readonly EncounterLayout Encounters;

        public LevelMap(TileGrid grid, int seed, int startX, int startY, int goalX, int goalY,
                        float fastestRouteCost, IReadOnlyList<Corridor> corridors,
                        bool choiceValidated, int attempts, EncounterLayout encounters = null)
        {
            Encounters = encounters ?? new EncounterLayout();
            Grid = grid;
            Seed = seed;
            StartX = startX;
            StartY = startY;
            GoalX = goalX;
            GoalY = goalY;
            FastestRouteCost = fastestRouteCost;
            Corridors = corridors;
            ChoiceValidated = choiceValidated;
            Attempts = attempts;
        }

        /// <summary>
        /// Whether the generator accepted this map, or ran out of attempts and kept the
        /// least bad one it had seen.
        ///
        /// <b>The two came back indistinguishable, and the difference is the whole of
        /// what the generator promises.</b> Generate returns the first candidate that
        /// satisfies every condition, and when none of them does it returns the best of a
        /// bad lot - with an ordinary <see cref="Attempts"/> on it, which is the attempt
        /// that candidate happened to be found at and not how hard the search tried. So a
        /// level that exhausted ninety-six attempts and shipped a compromise looked from
        /// outside exactly like a level that was answered on the fourth, and the tool that
        /// writes the level catalogue wrote both down the same way.
        ///
        /// Set by the generator on the way out and read by that tool. Nothing in a run
        /// reads it: by then the map is the map.
        /// </summary>
        public bool Accepted { get; set; }

        public int StartIndex => Grid.ToIndex(StartX, StartY);
        public int GoalIndex => Grid.ToIndex(GoalX, GoalY);

        public Corridor CorridorOf(CorridorKind kind)
        {
            if (Corridors == null) return null;
            foreach (var c in Corridors)
                if (c.Kind == kind) return c;
            return null;
        }
    }
}
