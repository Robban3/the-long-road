using System.Collections.Generic;

namespace TheVail.App
{
    /// <summary>
    /// The route the player drew, carried from the planning map to the run.
    ///
    /// **This is the hand-off the game never had.** RoutePlanner has been able to turn
    /// a handful of tapped waypoints into a route — with its travel time, its ambush
    /// exposure, the fords it crosses and the share of it spent in marsh — since it was
    /// written, and nothing in the game has ever called it. The runner picked its way
    /// through a level from a CorridorKind field in the Inspector, so every level was
    /// played on whichever of the generator's three sample corridors happened to be
    /// selected, and the player's choice of road did not exist.
    ///
    /// Static because it outlives a scene load and nothing else here does. It is a
    /// handful of tile indices and the level they belong to, and the level is checked on
    /// arrival: a route drawn for 1-3 must not be walked on 1-4 because somebody pressed
    /// Play twice.
    /// </summary>
    public static class ChosenRoute
    {
        static readonly List<int> _tiles = new List<int>();

        public static int Chapter { get; private set; }
        public static int Level { get; private set; }

        public static IReadOnlyList<int> Tiles => _tiles;

        /// <summary>Whether a route was drawn for this level and is worth walking.</summary>
        public static bool Waits(int chapter, int level)
            => _tiles.Count > 0 && Chapter == chapter && Level == level;

        public static void Set(int chapter, int level, IReadOnlyList<int> tiles)
        {
            _tiles.Clear();
            Chapter = chapter;
            Level = level;

            if (tiles != null) _tiles.AddRange(tiles);
        }

        public static void Clear() => _tiles.Clear();
    }
}
