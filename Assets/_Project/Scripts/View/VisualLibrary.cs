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

        /// <summary>
        /// Meshes inside the file that are not this character, switched off on spawn.
        ///
        /// Some of the packs ship more than one figure per file — the pirate
        /// captain's carries a second man called Ernest, who stood beside every
        /// bandit in the game — and some hand their character a prop we do not want,
        /// like the lute the archer was holding instead of a bow. Named here rather
        /// than guessed at, because no rule tells a stowaway from a sword.
        /// </summary>
        public string[] Hide;

        /// <summary>
        /// Meshes that belong to the character but must not decide how big it is.
        ///
        /// A model is scaled by the height of everything in it, and the knight ships
        /// holding a two-hander whose point hangs past his boots. Measured with it he
        /// came out a head shorter than the troops he stands beside — the sword was
        /// eating a tenth of him. Drawn, not counted.
        /// </summary>
        public string[] Unsized;

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

        [Header("Wildlife")]
        /// <summary>
        /// Deer, foxes and boar (docs/GDD.md §3.5). They cannot be fought, so they are
        /// not in <see cref="For"/> with the enemies — nothing ever looks one up by
        /// <see cref="EnemyKind"/>, and putting them there would invite it.
        /// </summary>
        public ActorModel Fox;
        public ActorModel DeerFemale;
        public ActorModel DeerMale;
        public ActorModel Boar;

        public ActorModel For(WildlifeKind kind)
        {
            switch (kind)
            {
                case WildlifeKind.Fox: return Fox;
                case WildlifeKind.DeerFemale: return DeerFemale;
                case WildlifeKind.DeerMale: return DeerMale;
                default: return Boar;
            }
        }

        /// <summary>
        /// Shoulder heights, like the wolf and for the same reason: an animal measured
        /// to the ear comes out a head too tall, and a deer as tall as a knight reads as
        /// wrong long before anyone works out why.
        /// </summary>
        public static float HeightOf(WildlifeKind kind)
        {
            switch (kind)
            {
                case WildlifeKind.Fox: return 0.45f;
                case WildlifeKind.DeerFemale: return 1.1f;
                case WildlifeKind.DeerMale: return 1.35f;
                default: return 0.85f;
            }
        }

        [Header("Signals")]
        /// <summary>
        /// The circling crows of docs/GDD.md §3.5, as a flock prefab.
        ///
        /// One prefab per flock rather than three birds placed by hand, because the
        /// pack's own controller already flies them: it spawns the birds and turns them
        /// about its own transform. Nothing here configures it, and nothing here can —
        /// the pack's scripts have no assembly definition, so they compile into
        /// Assembly-CSharp, and an asmdef assembly like this one cannot reference that.
        /// The flock's count, radius and altitude are therefore set on the prefab. The
        /// numbers this design measured are 3 birds, a 10 m ring, 22 m up (§4).
        /// </summary>
        public GameObject CrowFlockPrefab;

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
