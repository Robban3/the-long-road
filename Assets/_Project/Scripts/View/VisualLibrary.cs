using Arna.Sim;
using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// Which model stands in for each thing the simulation moves.
    ///
    /// Everything is optional. A missing entry falls back to a coloured primitive, so
    /// the game runs on a machine with no art at all — which is what let the whole
    /// simulation be built and judged before a single model existed.
    /// </summary>
    [System.Serializable]
    public sealed class VisualLibrary
    {
        [Header("Troops")]
        public GameObject Melee;
        public GameObject Ranged;
        public GameObject Support;
        public GameObject Mounted;

        [Header("Enemies")]
        public GameObject Wolf;
        public GameObject Bandit;
        public GameObject BanditArcher;

        [Header("Caravan")]
        public GameObject WagonBody;
        public GameObject WagonCargo;

        [Header("World")]
        public GameObject SilverCache;
        public GameObject TrapMarker;

        public GameObject For(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Archers:
                case TroopKind.Mage:
                    return Ranged;

                case TroopKind.Cavalry:
                    return Mounted;

                case TroopKind.Scout:
                case TroopKind.Priest:
                case TroopKind.Engineer:
                    return Support;

                default:
                    return Melee;
            }
        }

        public GameObject For(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Wolf: return Wolf;
                case EnemyKind.BanditArcher: return BanditArcher;
                default: return Bandit;
            }
        }

        /// <summary>
        /// Rough height of a model in metres, used to scale it to the map.
        ///
        /// The packs are authored at wildly different sizes — a wolf and a knight do
        /// not arrive the same height — so everything is normalised on the way in
        /// rather than hand-tweaked per prefab.
        /// </summary>
        public const float TroopHeight = 3.5f;
        public const float EnemyHeight = 3.0f;
    }
}
