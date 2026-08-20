using Arna.Sim;
using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// A model and the controller that animates it.
    ///
    /// The two travel together because every rig in the packs is Generic rather than
    /// Humanoid: a clip belongs to one skeleton and cannot drive another. A knight's
    /// walk will not move a wolf, so each model brings its own controller or none.
    /// </summary>
    [System.Serializable]
    public struct ActorModel
    {
        public GameObject Prefab;
        public RuntimeAnimatorController Animator;

        /// <summary>
        /// Weapon fitted into the right hand. The character packs ship weapons as
        /// separate models rather than attached, so a knight straight out of the box
        /// walks into battle empty-handed.
        /// </summary>
        public GameObject Weapon;

        /// <summary>Metres from wrist to grip, and how the weapon sits in the hand.</summary>
        public float WeaponLength;
        public Vector3 WeaponRotation;

        public bool HasModel => Prefab != null;
    }

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
        public ActorModel Melee;
        public ActorModel Ranged;
        public ActorModel Support;
        public ActorModel Mounted;

        [Header("Enemies")]
        public ActorModel Wolf;
        public ActorModel Bandit;
        public ActorModel BanditArcher;

        [Header("Caravan")]
        /// <summary>Complete carts. When set, the improvised crate-and-wheels build is skipped.</summary>
        public GameObject Wagon;

        /// <summary>The merchant's wagon, distinct so the loot is identifiable on sight.</summary>
        public GameObject WagonTreasure;

        public GameObject WagonBody;
        public GameObject WagonCargo;

        /// <summary>
        /// The three wagons look different on purpose: a player who can tell at a glance
        /// which one the bandits are converging on can do something about it.
        /// </summary>
        public GameObject WagonFor(WagonKind kind)
        {
            if (kind == WagonKind.Treasure && WagonTreasure != null) return WagonTreasure;
            return Wagon;
        }

        [Header("World")]
        public GameObject SilverCache;
        public GameObject TrapMarker;

        public ActorModel For(TroopKind kind)
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

        public ActorModel For(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Wolf: return Wolf;
                case EnemyKind.BanditArcher: return BanditArcher;
                default: return Bandit;
            }
        }

        /// <summary>
        /// Rough height of a model in metres, used to scale it to the world.
        /// A tile is four metres across for reference.
        /// </summary>
        /// <summary>
        /// Heights in metres. A person is a person and a wagon is a wagon: fitting both
        /// to the same figure made the caravan look like a toy set, because a knight
        /// standing as tall as the cart he guards reads as wrong long before anyone
        /// works out why.
        /// </summary>
        public const float TroopHeight = 1.85f;
        public const float EnemyHeight = 1.8f;

        /// <summary>
        /// Lower than the others because a wolf is measured at the shoulder, not the
        /// ear. The model is 3.0 tall and 5.5 long in its own units, so fitting it to a
        /// person's height produced a four-metre wolf.
        /// </summary>
        public const float WolfHeight = 0.95f;

        /// <summary>Height of a wagon to the top of its hood.</summary>
        public const float WagonHeight = 2.5f;
    }
}
