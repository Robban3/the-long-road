using System.Collections.Generic;

namespace Arna.Sim
{
    public struct EnemySpawn
    {
        public int Tile;
        public EnemyKind Kind;

        /// <summary>Which corridor this group was budgeted against. Diagnostics only.</summary>
        public CorridorKind Corridor;
    }

    public struct TrapPlacement
    {
        public int Tile;
        public TrapKind Kind;
        public CorridorKind Corridor;
    }

    /// <summary>
    /// A standalone silver source — an abandoned camp, a looted shrine.
    ///
    /// Exists to rescue a corridor whose silver income falls short. The alternative,
    /// adding enemies, would destroy the very thing that makes the cautious route
    /// cautious (docs/content-pipeline.md §3 step 6b).
    /// </summary>
    public struct SilverCache
    {
        public int Tile;
        public int Amount;
        public CorridorKind Corridor;
    }

    /// <summary>
    /// Everything hostile or valuable placed on a level, plus the per-corridor
    /// accounting the generator used to decide it.
    /// </summary>
    public sealed class EncounterLayout
    {
        public readonly List<EnemySpawn> Enemies = new List<EnemySpawn>();
        public readonly List<TrapPlacement> Traps = new List<TrapPlacement>();
        public readonly List<SilverCache> SilverCaches = new List<SilverCache>();

        /// <summary>Threat points spent per corridor, indexed by (int)CorridorKind.</summary>
        public readonly int[] PointsByCorridor = new int[3];

        /// <summary>Silver obtainable per corridor if everything on it is dealt with.</summary>
        public readonly int[] SilverByCorridor = new int[3];

        /// <summary>False when a corridor could not be brought up to the silver floor.</summary>
        public bool SilverValidated;

        public int TotalPoints
        {
            get
            {
                int sum = 0;
                foreach (int p in PointsByCorridor) sum += p;
                return sum;
            }
        }

        /// <summary>
        /// Danger per tile travelled. This is the number that has to come out lower
        /// for the slow route than the fast one, or the whole speed-versus-safety
        /// trade-off is a fiction.
        /// </summary>
        public float ThreatDensity(CorridorKind kind, int corridorTiles)
            => corridorTiles <= 0 ? 0f : (float)PointsByCorridor[(int)kind] / corridorTiles;
    }
}
