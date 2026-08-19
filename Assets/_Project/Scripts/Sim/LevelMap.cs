using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>
    /// A generated level: terrain, the caravan's entry and exit points, and the
    /// corridors that prove the route choice is real.
    /// Produced by Arna.Gen, consumed by both the simulation and the view.
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

        public LevelMap(TileGrid grid, int seed, int startX, int startY, int goalX, int goalY,
                        float fastestRouteCost, IReadOnlyList<Corridor> corridors,
                        bool choiceValidated, int attempts)
        {
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
