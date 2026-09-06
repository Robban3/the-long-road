using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.View
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
        /// The skeleton the clips are played on.
        ///
        /// Only needed when the clips came from somewhere else. A Generic rig playing
        /// animation out of its own file binds by transform path and wants nothing here;
        /// a **retargeted** clip is described in terms of a human skeleton rather than of
        /// bone names, and without an avatar to map that onto there is nothing for it to
        /// move. It runs, reports a state and a normalised time, and the model stands
        /// still — which is the failure that looks most like no animation at all.
        ///
        /// Carried on the model rather than read off the prefab because the two are not
        /// the same asset: the army pack's characters are prefabs assembled from meshes,
        /// and the avatar belongs to the mesh's file.
        /// </summary>
        public Avatar Rig;

        /// <summary>
        /// Whether this model plays clips out of somebody else's file.
        ///
        /// The one fact that decides whether a missing avatar matters. It is not
        /// something the prefab can be asked — a Generic rig with no avatar is correct
        /// and complete when the clips are its own, and broken when they are not — so
        /// the side that wired the controller says which case it is.
        /// </summary>
        public bool Borrowed;

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
        /// Degrees to turn the model so its nose points where it is going.
        ///
        /// Nothing in an FBX says which way is forward, and the packs disagree — the
        /// same disagreement as the up axis, and with a worse symptom, because it looks
        /// like a movement bug rather than an import one. A wolf whose model faces +X
        /// while the code turns it to face the caravan runs at the caravan sideways,
        /// crab-wise, which reads as the animation being broken or the thing being
        /// dragged.
        ///
        /// Read it off `The Veil > Report Selected Folder Dimensions`: on a quadruped the
        /// long horizontal axis is the body, nose to tail. Long in Z is 0, long in X is
        /// 90 or -90.
        /// </summary>
        public float YawOffset;

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
        /// <summary>
        /// One model per troop kind, because a post the player cannot read is a post
        /// they cannot use.
        ///
        /// Nine kinds shared three models — melee, ranged, support — so a priest looked
        /// like an engineer and a shieldbearer like a spearman. The whole of docs/GDD.md
        /// §4.2 is that *where you put which troop* decides the level; six posts around a
        /// caravan mean nothing if you cannot tell at a glance what is standing on them.
        ///
        /// The difference has to carry from 47 m up and 40 m back. At that distance a
        /// face is nothing and a tabard is a smudge: what reads is **body shape, helmet
        /// outline and what is held**. So the pack's four social ranks are spent on that
        /// axis — peasant, levy, man-at-arms, knight — rather than on colour.
        /// </summary>
        [Header("Troops")]
        public ActorModel Spearmen;
        public ActorModel Swordsmen;
        public ActorModel Archers;
        public ActorModel Mage;
        public ActorModel Scout;
        public ActorModel Shieldbearer;
        public ActorModel Priest;
        public ActorModel Engineer;
        public ActorModel Mounted;

        /// <summary>
        /// The horse in the traces, which is **not** the cavalry model.
        ///
        /// It used to be: both were `Mounted`, back when that was a bare Quaternius
        /// horse and the cavalry had nobody on it. The army pack ships its cavalry with
        /// the rider already mounted, so pointing the wagons at the same field hitches
        /// six knights to the caravan and has them pull it.
        /// </summary>
        public ActorModel Draught;

        /// <summary>
        /// The three that everything used to share, kept as fallbacks.
        ///
        /// A scene saved before the kinds were split still holds these and holds nothing
        /// in the fields above, and <see cref="For(TroopKind)"/> falls back to them
        /// rather than drawing capsules. `Decor` and `Models` are serialized on the scene
        /// component — pulling code does not change a saved scene — and that has produced
        /// enough false bug reports in this project to be worth one field each.
        /// </summary>
        public ActorModel Melee;
        public ActorModel Ranged;
        public ActorModel Support;

        /// <summary>
        /// What every enemy figure's colour is multiplied by.
        ///
        /// The army pack shades everything off one small palette texture, so multiplying
        /// the base colour tilts the whole figure at once — mail, cloth and leather
        /// together — instead of recolouring one garment. A muted warm crimson rather
        /// than a saturated one: saturated red flattens a palette into a silhouette and
        /// throws away the armour detail the rank distinction is carried by.
        ///
        /// Colour is the **second** signal and never the only one. The bandits are levy
        /// where the escort is man-at-arms and knight, because the first thing a moving
        /// figure loses against a hillside in shadow is its hue (docs/GDD.md §5 makes the
        /// same argument for the three wagons). This is what makes the call instant once
        /// the silhouette has already made it possible.
        ///
        /// A property block rather than a material, so it costs no asset and applies to
        /// whatever the pack ships. When the pack's own faction material sets are wired
        /// this becomes a fallback for anything they do not cover.
        /// </summary>
        public static readonly Color EnemyTint = new Color(0.78f, 0.44f, 0.40f);

        /// <summary>
        /// The faction material an enemy figure is repainted with.
        ///
        /// The army pack ships six of these — black, blue, brown, green, red, yellow —
        /// and they are the pack's own answer to exactly this question: one palette
        /// texture per side, so two armies share every mesh and differ only in which
        /// material draws them. That is better than <see cref="EnemyTint"/> in both
        /// directions: it is the colour the artist authored rather than a multiply over
        /// theirs, and a shared material batches where a per-renderer property block
        /// does not.
        ///
        /// **Only the slots that already hold a faction material are swapped.** A
        /// character carries several — body, weapon, shield — and the pack keeps weapons
        /// on their own `Weapons.mat`. Repainting every slot would hand the bandits red
        /// swords.
        ///
        /// Left null, <see cref="EnemyTint"/> is used instead, so a project without the
        /// pack still tells the sides apart.
        /// </summary>
        public Material EnemyFaction;

        /// <summary>
        /// The faction material the *escort* is repainted with.
        ///
        /// <b>Both sides have to be set, and only one of them was.</b> The bandits were
        /// repainted red and the player's troops were left in whatever the pack's prefabs
        /// happened to ship as — which is a colour nobody here chose and which can perfectly
        /// well be the same red. Two sides in the same livery is the exact failure the
        /// repaint exists to prevent, and leaving one of them to chance is how it happened.
        ///
        /// The pack carries six of these — black, blue, brown, green, red and yellow — so
        /// there is no reason for either side to take what it is given.
        ///
        /// Left null, the escort keeps its shipped materials, which is what a project
        /// without the pack wants: nothing to repaint and nothing to go wrong.
        /// </summary>
        public Material PlayerFaction;

        /// <summary>
        /// What a faction material is called, so one can be recognised in a slot.
        ///
        /// `Unviersal` is the pack's own spelling. Correcting it here would mean matching
        /// nothing, which is the quietest possible failure: every figure would simply
        /// stay the colour it shipped as.
        /// </summary>
        public const string FactionPrefix = "UnviersalColors";

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
        /// <summary>
        /// How tall a wild animal is drawn, in metres.
        ///
        /// **Not life size, and for a reason that can be measured: the grass is 0.7 m.**
        /// `TerrainDecorator.CoverHeight` is what every tuft, fern and reed on the map is
        /// fitted to, and a fox is 0.45 m at the shoulder. It was placed correctly on
        /// thirty-four tiles of a level and was *under the ground cover* — invisible not
        /// as a bug but as arithmetic. A doe at 1.1 m stood a hand above it, from a camera
        /// 46 m back and 32 m up.
        ///
        /// So they are drawn at roughly twice life, the same call the eagle got and for
        /// the same reason: she is a ten-metre bird against a real two, "a marker that
        /// happens to be shaped like a bird". An animal in this game is a resource the
        /// player can choose to take (docs/GDD.md §3.5) and a thing that cannot be seen
        /// is not a choice.
        ///
        /// The ceiling is the man walking past. A troop is 1.85 m, and a stag at 1.85
        /// would read as the largest thing on the map after the wagons — so the stag is
        /// the tallest of these and still stops below a man's head.
        /// </summary>
        public static float HeightOf(WildlifeKind kind)
        {
            switch (kind)
            {
                case WildlifeKind.Fox: return 0.95f;
                case WildlifeKind.DeerFemale: return 1.5f;
                case WildlifeKind.DeerMale: return 1.75f;
                default: return 1.2f;
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

        /// <summary>
        /// The scouting eagle of docs/GDD.md §3.6.
        ///
        /// Flown over the planning map before the pencil comes out, so it belongs to
        /// that screen rather than to the run — and that screen does not exist in Unity
        /// yet. Wired here anyway: an asset that is imported but unreferenced is an
        /// asset nobody has checked, and the two things worth knowing about a model —
        /// what it is authored at and which way its nose points — are cheapest to find
        /// out the day it arrives.
        ///
        /// Fitted by <see cref="EagleSpan"/> rather than by height. See the byWidth
        /// argument on RunVisuals.Spawn for why.
        /// </summary>
        public ActorModel Eagle;

        [Header("Caravan")]
        /// <summary>Complete carts. When set, the improvised crate-and-wheels build is skipped.</summary>
        public GameObject Wagon;

        /// <summary>
        /// One model per wagon, and the comment below used to promise this while the
        /// code delivered two.
        ///
        /// The supply wagon and the war wagon were the same vehicle in different
        /// colours, which is the thing the design specifically says is not enough
        /// (docs/GDD.md §5): a player has to be able to tell at a glance which one the
        /// bandits are converging on, and colour is what a moving object loses first
        /// against a hillside in shadow.
        ///
        /// Any that is left unset falls back to <see cref="Wagon"/>, so a pack with
        /// fewer vehicles than this game has roles still produces a caravan.
        /// </summary>
        /// <summary>
        /// How far in front of each cart's own measured front its team stands, in metres.
        ///
        /// **Three numbers rather than one, because the three carts are not built alike.**
        /// The front is measured off the model, so a cart with a drawbar modelled on it
        /// measures out to the end of that bar — which is where the horses are *already*
        /// meant to stand. Adding a pole to that puts them a pole further on, and the
        /// covered wagon has a bar where the supply wagon has a bed edge. One constant
        /// could only ever be right for one of them.
        ///
        /// Serialized, so they can be dragged in the inspector against the thing on
        /// screen instead of guessed at from here and pushed back and forth. The build
        /// log prints what each one came out as.
        /// </summary>
        [Header("Harness")]
        public float HitchSupply = 0.75f;

        /// <summary>The war wagon's. See <see cref="HitchSupply"/>.</summary>
        public float HitchWar = 0.75f;

        /// <summary>
        /// The covered wagon's, and the smallest of the three: this is the one whose
        /// drawbar is part of the model, so its measured front already reaches most of
        /// the way to the collar.
        /// </summary>
        public float HitchTreasure = 0.1f;

        /// <summary>The hitch distance for one kind of wagon.</summary>
        public float HitchFor(WagonKind kind)
        {
            switch (kind)
            {
                case WagonKind.Treasure: return HitchTreasure;
                case WagonKind.War: return HitchWar;
                default: return HitchSupply;
            }
        }

        public GameObject WagonSupply;

        /// <summary>The merchant's wagon, distinct so the loot is identifiable on sight.</summary>
        public GameObject WagonTreasure;

        /// <summary>The ballista cart. Its silhouette is the reason it is its own model.</summary>
        public GameObject WagonWar;

        public GameObject WagonBody;
        public GameObject WagonCargo;

        /// <summary>
        /// The three wagons look different on purpose: a player who can tell at a glance
        /// which one the bandits are converging on can do something about it.
        /// </summary>
        public GameObject WagonFor(WagonKind kind)
        {
            switch (kind)
            {
                case WagonKind.Treasure: return WagonTreasure != null ? WagonTreasure : Wagon;
                case WagonKind.War: return WagonWar != null ? WagonWar : Wagon;
                default: return WagonSupply != null ? WagonSupply : Wagon;
            }
        }

        [Header("World")]
        public GameObject SilverCache;
        public GameObject TrapMarker;

        /// <summary>
        /// The shaft an archer looses. Optional: without one the bows fire a plain dart,
        /// which at forty-five metres a second is very nearly the same picture.
        /// </summary>
        public GameObject Arrow;

        public ActorModel For(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Spearmen: return Or(Spearmen, Melee);
                case TroopKind.Swordsmen: return Or(Swordsmen, Melee);
                case TroopKind.Shieldbearer: return Or(Shieldbearer, Melee);

                case TroopKind.Archers: return Or(Archers, Ranged);
                case TroopKind.Mage: return Or(Mage, Ranged);

                case TroopKind.Cavalry: return Mounted;

                case TroopKind.Scout: return Or(Scout, Support);
                case TroopKind.Priest: return Or(Priest, Support);
                case TroopKind.Engineer: return Or(Engineer, Support);

                default: return Melee;
            }
        }

        /// <summary>The kind's own model, or the group it used to share, or nothing.</summary>
        static ActorModel Or(ActorModel own, ActorModel group) => own.HasModel ? own : group;

        /// <summary>
        /// How tall a troop kind is drawn, in metres.
        ///
        /// One number for eight of them and a separate one for the cavalry, because the
        /// cavalry model is a **rider already on a horse**. Fitting that to a man's
        /// height gives a man-sized horse with a doll on it: the pack ships the pair
        /// assembled, so the pair is what has to be measured. A draught horse is drawn at
        /// 2.4 m to the top of its head (<see cref="DraughtHorseHeight"/>) and a rider
        /// sits above the withers, so 2.7 puts the two on the same ruler.
        /// </summary>
        public static float HeightOf(TroopKind kind)
            => kind == TroopKind.Cavalry ? CavalryHeight : TroopHeight;

        public const float CavalryHeight = 2.7f;

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

        /// <summary>
        /// Height of a wagon to the top of its hood.
        ///
        /// 3.2 rather than 2.5, and the measure that settles it is the man walking beside
        /// it. A troop is 1.85 m, so at 2.5 a wagon stood 1.35 times a man — which is a
        /// handcart, not a vehicle hauling three hundred and fifty silver. At 3.2 it is
        /// 1.7 times a man, and the column becomes the largest thing on the ground, which
        /// is what the player is meant to be watching.
        ///
        /// It has room to grow into: the wagons trail eight metres apart
        /// (<see cref="TheVeil.Sim.Caravan.WagonSpacing"/>), and a covered wagon fitted to
        /// 3.2 m is about six and a half long.
        /// </summary>
        public const float WagonHeight = 3.2f;

        /// <summary>
        /// A draught horse to the top of its head.
        ///
        /// Taken off the same ruler as the rest: a man is 1.85 m and a heavy horse
        /// stands about 1.7 m at the withers, so head up it reaches roughly 2.4. That
        /// puts it at three quarters of the wagon it pulls — the horse's back below the
        /// cart's rail, its head about level with the hood. Fitted to a man's height
        /// instead, which is what the mounted troop's horse uses, it would come out as a
        /// pony leaning into a vehicle nearly twice its size.
        /// </summary>
        public const float DraughtHorseHeight = 2.4f;

        /// <summary>
        /// How long a draught horse is, nose to tail, at that height.
        ///
        /// Stated rather than measured, and that is the fix rather than a shortcut. A
        /// rigged model's renderer bounds are a box drawn to hold **every clip in the
        /// file** — this horse has a gallop in it, legs at full stretch — so measuring
        /// the instance gave a length half again what the animal standing in front of
        /// you occupies. The team was then placed by that inflated figure and ended up
        /// most of a horse-length too far in front of the cart, with a gap the escort
        /// promptly walked into.
        ///
        /// 2.6 m: a heavy horse is about 2.4 m from chest to rump and the head carries
        /// it a little past that.
        /// </summary>
        public const float DraughtHorseLength = 2.6f;

        /// <summary>
        /// Wingspan in metres, and deliberately not a golden eagle's.
        ///
        /// A real one is a little over two metres across. At two metres over a map
        /// four tiles to the finger it is a speck, and the whole point of the ability
        /// is that the player watches where the bird goes. Ten was chosen on the
        /// planning render, where twenty-one read as a dragon and eleven vanished into
        /// the canopy — the same argument the map's own note makes: the bird is a
        /// marker that happens to be shaped like a bird.
        /// </summary>
        public const float EagleSpan = 10f;
    }
}
