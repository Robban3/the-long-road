using System.Collections.Generic;

namespace Arna.Sim
{
    /// <summary>
    /// Why something was placed where it was. Diagnostics, but the distinction is the
    /// design: a guard is a promise the player cannot walk around, a scattered group is
    /// a probability, and a repair is the placer admitting the probability was not
    /// enough on some route it sampled.
    /// </summary>
    public enum PlacementOrigin : byte
    {
        Guard = 0,
        Scattered = 1,
        Repair = 2
    }

    public struct EnemySpawn
    {
        public int Tile;
        public EnemyKind Kind;
        public PlacementOrigin Origin;

        /// <summary>
        /// Metres of country this group watches, and the range at which it wakes.
        ///
        /// A group standing on one tile of a sixty-four tile map cannot be relied on to
        /// meet anybody. Placement alone would need roughly twenty-eight groups to seal
        /// the band a route can be drawn through, and the enemy budget buys twelve — so
        /// each of the twelve watches a stretch instead, and whoever crosses that
        /// stretch is intercepted. It is also the truer fiction: raiders hold a piece of
        /// road, they do not queue on a tile.
        /// </summary>
        public float Territory;
    }

    public struct TrapPlacement
    {
        public int Tile;
        public TrapKind Kind;
        public PlacementOrigin Origin;
    }

    /// <summary>
    /// A standalone silver source — an abandoned camp, a looted shrine.
    ///
    /// Exists to rescue a route whose silver income falls short. The alternative,
    /// adding enemies, would destroy the very thing that makes the cautious way
    /// cautious (docs/content-pipeline.md §3 step 6b).
    /// </summary>
    public struct SilverCache
    {
        public int Tile;
        public int Amount;
        public PlacementOrigin Origin;
    }

    /// <summary>
    /// Everything hostile or valuable placed on a level, and what the placer was able
    /// to prove about it.
    ///
    /// The accounting used to be per corridor, because the player chose one of three.
    /// Now the player draws the line, so the numbers that matter are about the worst
    /// route rather than about a named one — above all <see cref="MinEncounters"/>.
    /// </summary>
    public sealed class EncounterLayout
    {
        public readonly List<EnemySpawn> Enemies = new List<EnemySpawn>();
        public readonly List<TrapPlacement> Traps = new List<TrapPlacement>();
        public readonly List<SilverCache> SilverCaches = new List<SilverCache>();

        /// <summary>Silver obtainable on the level if everything on it is dealt with.</summary>
        public int TotalSilver;

        /// <summary>False when a sampled route could not be brought up to the silver floor.</summary>
        public bool SilverValidated;

        /// <summary>
        /// False when the repair loop could not bring the worst sampled route up to
        /// the placer's target (see `EncounterPlacer.RepairTarget` in Arna.Gen, which
        /// this assembly cannot name: Gen depends on Sim and not the reverse).
        ///
        /// The generator reads this and rolls the level again, which is the point of
        /// recording it. A level where a drawn line can meet almost nothing is not one
        /// to ship; it is one to re-roll, and that costs generation time and nothing else.
        /// </summary>
        public bool EncountersValidated;

        /// <summary>Tiles a sane crossing could pass through — the ground placement covers.</summary>
        public int BandTiles;

        /// <summary>Groups placed on fords, which no crossing of the river can avoid.</summary>
        public int FordGuards;

        /// <summary>Groups moved onto a route that had met too little. Budget-neutral.</summary>
        public int Repairs;

        public int SampledRoutes;

        /// <summary>
        /// Fewest groups any sampled route ran into. The promise the whole route-drawing
        /// mechanic rests on: draw what you like, you will still have a game.
        /// </summary>
        public int MinEncounters;

        /// <summary>Threat points spent, enemies and traps together.</summary>
        public int TotalPoints
        {
            get
            {
                int points = 0;
                foreach (var spawn in Enemies) points += EnemyTable.Points(spawn.Kind);
                foreach (var trap in Traps) points += TrapTable.Points(trap.Kind);
                return points;
            }
        }
    }
}
