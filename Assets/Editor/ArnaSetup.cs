using System;
using System.Collections.Generic;
using System.IO;
using Arna.App;
using Arna.Sim;
using Arna.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Arna.Editor
{
    /// <summary>
    /// One-shot project setup, runnable from the menu or headless via -executeMethod.
    ///
    /// Scripted rather than clicked so the render pipeline, material and preview
    /// scene can be recreated identically on any machine — a project that only
    /// exists because someone configured it by hand once is a project nobody else
    /// can reproduce.
    /// </summary>
    public static class ArnaSetup
    {
        const string SettingsDir = "Assets/_Project/Settings";
        const string ScenesDir = "Assets/_Project/Scenes";
        const string MaterialsDir = "Assets/_Project/Materials";

        const string RendererPath = SettingsDir + "/ArnaUniversalRenderer.asset";
        const string PipelinePath = SettingsDir + "/ArnaUniversalRenderPipeline.asset";
        const string MaterialPath = MaterialsDir + "/TerrainOverview.mat";

        /// <summary>The play view's ground. Lit, unlike the planning map's flat colour.</summary>
        const string GroundMaterialPath = MaterialsDir + "/TerrainGround.mat";

        /// <summary>The corridors drawn on the plan. Always on top, never lit.</summary>
        const string RouteMaterialPath = MaterialsDir + "/RouteOverlay.mat";
        const string ScenePath = ScenesDir + "/LevelPreview.unity";
        const string PlayScenePath = ScenesDir + "/PlayLevel.unity";

        [MenuItem("Arna/Set Up Project")]
        public static void SetupProject()
        {
            if (!Stopped("Set Up Project")) return;

            EnsureFolders();
            var pipeline = EnsureRenderPipeline();
            EnsureMaterial();

            // The plan uses the lit ground material now, the same one the play view
            // stands on. The flat unlit material is kept for anything that still wants
            // a diagram rather than a picture.
            BuildPreviewScene(EnsureGroundMaterial());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Arna] Setup complete. Pipeline: {pipeline.name}, scene: {ScenePath}");
        }

        /// <summary>
        /// Builds the scene you press Play in. Separate from the preview scene, which
        /// is for judging generator output rather than watching a level unfold.
        /// </summary>
        [MenuItem("Arna/Set Up Play Scene")]
        public static void SetUpPlayScene()
        {
            if (!Stopped("Set Up Play Scene")) return;

            EnsureFolders();
            EnsureRenderPipeline();
            EnsureMaterial();
            var material = EnsureGroundMaterial();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            const float mapExtent = 64 * 4f;
            const float pitchDegrees = 55f;

            var centre = new Vector3(mapExtent * 0.5f, 0f, mapExtent * 0.5f);
            var rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetPositionAndRotation(centre - rotation * Vector3.forward * 400f, rotation);
            var camera = cameraGo.AddComponent<Camera>();

            // Sky rather than void. The world ends at the map edge and something has
            // to be behind it; a flat horizon colour reads as distance, black reads as
            // a bug.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 900f;

            ApplyOutdoorLighting();

            var lightGo = new GameObject("Directional Light");

            // Lower and further round than the map view's light. A sun near the zenith
            // puts every shadow directly under the thing casting it, where it cannot
            // be seen, and a landscape whose shadows are invisible looks like a
            // diagram. At 38 degrees the trees lay shadows across the ground.
            lightGo.transform.rotation = Quaternion.Euler(38f, -52f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;

            // The default bias leaves props hovering on their own shadows; the default
            // normal bias eats the contact point where a trunk meets the ground, which
            // is the one part of the shadow that does the grounding.
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.15f;

            var runnerGo = new GameObject("LevelRunner");
            runnerGo.AddComponent<MeshFilter>();
            runnerGo.AddComponent<MeshRenderer>().sharedMaterial = material;

            var runner = runnerGo.AddComponent<LevelRunner>();
            runner.Decor = LoadForestDecor();
            runner.Models = LoadModels();

            EditorSceneManager.SaveScene(scene, PlayScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(PlayScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log($"[Arna] Play scene ready at {PlayScenePath}. Open it and press Play.");
        }


        /// <summary>
        /// Casts the models against the simulation's roles.
        ///
        /// The packs were not made for this game, so the mapping is by silhouette
        /// rather than by name: an armoured figure reads as the troops who hold the
        /// line, a hooded traveller as the ones who scout and shoot, and a pirate
        /// captain as somebody you would rather not meet on the road. At the camera
        /// distance this game uses, silhouette and colour are all that carry.
        /// </summary>
        static VisualLibrary LoadModels()
        {
            return new VisualLibrary
            {
                // One model per troop kind, chosen for what survives at 47 m up: body
                // shape, helmet outline, and what is held. See VisualLibrary.Spearmen.
                //
                // The pack's four ranks are the axis — peasant, levy, man-at-arms,
                // knight — and they are spent on legibility rather than on lore. The
                // three fighting kinds get the armoured ranks so they read as the line;
                // the three support kinds get the unarmoured ones so they read as the
                // people the line is protecting, which is what they are.
                //
                // These are the pack's ready-to-use prefabs and they come armed, so
                // nothing is fitted into a hand: `Arm` would put a second sword in a
                // fist already holding one.
                Spearmen = Army("MC_ManAtArms_01"),
                Swordsmen = Army("MC_ManAtArms_04"),

                // The widest silhouette in the game, which is the whole of what a
                // shieldbearer is for. A knight in full plate is the only figure in the
                // pack that reads as *broad* from above rather than merely tall.
                Shieldbearer = Army("MC_Knight_01"),

                Archers = Army("MC_Archer_01"),

                // No mage in a medieval army pack, and that is not a gap to paper over
                // with a knight. A robed figure with no helmet is the one silhouette
                // here that is unmistakably not a soldier, and the nobles are the only
                // robed men in it.
                Mage = Army("MC_Noble_01"),
                Priest = Army("MC_Noble_04"),

                // Lightest thing on two legs in the pack. A scout that reads as heavy is
                // a scout the player will not believe outruns anything.
                Scout = Army("MC_Levy_03"),

                // Tools rather than arms. An engineer who disarms traps and repairs
                // wagons should look like a man who works, and the peasants are the only
                // figures here who do.
                Engineer = Army("MC_Peasant_01"),

                // Mounted, and already so: the pack ships the rider on the horse rather
                // than as two things to assemble. It stands still, and that is the least
                // wrong of the things it could do — see the draught horse below.
                Mounted = Army("MC_Cavalry_LightCavalry"),

                // **The old horse, not the pack's.**
                //
                // MC_Horse_Saddle is the better model and it reared up on its hind legs.
                // Everything under Prefabs - Characters was made Humanoid and pointed at
                // the one human controller, and a horse mapped onto a human skeleton
                // playing a human idle stands like a man: hind legs down, forelegs up,
                // the length of the caravan. Unity maps it without complaint — the
                // guessing is by bone name and a horse has a hip and a head like anybody.
                //
                // A horse needs horse clips and there are none in the army pack, so it is
                // Quaternius's horse in the traces: worse-looking, and it walks. When a
                // four-legged clip source turns up the model can go back.
                Draught = Actor("Assets/Quaternius/Animals/Horse.fbx"),

                // Red for the bandits. The pack's own faction material rather than a
                // tint over it — see VisualLibrary.EnemyFaction — and note `Unviersal`,
                // which is how the pack spells it.
                EnemyFaction = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Stylized_Medieval_Army_Pack/Materials/UnviersalColorsRed.mat"),

                // The old three are **not loaded**, and the empty fields are the point.
                //
                // They were left pointing at Quaternius as a safety net, and a safety net
                // under a swap is a way of not noticing the swap failed: every kind whose
                // army prefab did not load quietly fell back to a knight, an adventurer
                // or a farmer, and the old escort walked on as though nothing had changed.
                //
                // Empty, a kind with no model draws a coloured capsule, which is the
                // project's oldest rule about this — a missing pack should degrade the
                // picture visibly rather than substitute something that looks deliberate.
                // The fields stay because a scene saved before the split still holds
                // models in them; nothing puts any there now.

                // From ForestAnimals rather than Quaternius: the pack ships the model
                // and a URP prefab, and the wolf is the only enemy on level 1-1
                // — which is why the Quaternius folder could not simply be deleted with
                // the rest of it. `Horse.fbx` still has to stay: neither new pack has one.
                Wolf = Actor("Assets/ForestAnimals/URP/Wolf/Prefab/Wolf_URP.prefab",
                             animator: "Assets/_Project/Animation/Wolf.controller", yaw: ForestAnimalYaw),

                // Barbarossa already carries his cutlass in the rig, so nothing is
                // fitted to him — but his file also carries a second man, Ernest, who
                // was standing beside every bandit in the game.
                // Bandits out of the same pack as the escort, and told apart two ways.
                //
                // **By rank first.** They are levy — the ragged end of the pack — where
                // the player's line is man-at-arms and knight. That difference is body
                // shape and helmet outline, which is what survives at 47 m; a pirate
                // captain from another artist's pack read as *a different game*, not as
                // a different side.
                //
                // **By colour second.** See VisualLibrary.EnemyTint. Colour alone would
                // not do it — the first thing a moving figure loses against a hillside in
                // shadow is its hue — but colour on top of a silhouette that already
                // differs is what makes the call instant.
                Bandit = Army("MC_Levy_05"),
                BanditArcher = Army("MC_Levy_07"),

                // The wildlife of GDD §3.5.
                //
                // The URP prefab for the materials, but the controller this project
                // builds rather than the pack's: everything here is driven through
                // Speed, Attack and Dead, and the pack's controllers use other names.
                // Run Arna > Build Animator Controllers before Set Up Play Scene.
                Fox = Actor("Assets/ForestAnimals/URP/Fox/Prefab/Fox_URP.prefab",
                            animator: "Assets/_Project/Animation/Fox.controller", yaw: ForestAnimalYaw),
                DeerFemale = Actor("Assets/ForestAnimals/URP/DeerFemale/Prefab/DeerFemale_URP.prefab",
                                   animator: "Assets/_Project/Animation/DeerFemale.controller", yaw: ForestAnimalYaw),
                DeerMale = Actor("Assets/ForestAnimals/URP/DeerMale/Prefabs/DeerMale_URP.prefab",
                                 animator: "Assets/_Project/Animation/DeerMale.controller", yaw: ForestAnimalYaw),
                Boar = Actor("Assets/ForestAnimals/URP/Boar/Prefab/Boar.prefab",
                             animator: "Assets/_Project/Animation/Boar.controller", yaw: ForestAnimalYaw),

                // "Wild Few" of the six flock prefabs: its flock size is three, which
                // is the number this design measured. The others spawn more.
                //
                // Not the baked example, which is what this pointed at first and which
                // is a demo: it carries the pack's whole showcase, feather particles
                // included, and nine of them turned the level into a snowstorm. The
                // lesson is duller than the picture — the comment above said Wild Few
                // while the path said something else, and nobody reads a path.
                //
                // The baked variant is still the one to want for a phone, but it ships
                // only as a single bird and a demo flock, so getting it means building
                // a flock prefab from `Bird Crow Baked` by hand. Worth doing before
                // release, not worth blocking on now.
                CrowFlockPrefab = One("Assets/Unluck Software/Bird Flocks/Bird Flock Crow/"
                                      + "Prefabs/Crow Flock - Wild Few.prefab"),

                // No yaw offset, and that is a decision rather than a default. The
                // folder report suggests 90 or -90 here because the model's long
                // horizontal axis is X — but that heuristic reads the long axis as the
                // body, nose to tail, which is true of everything that walks and false
                // of anything with its wings out. On this bird X is the 13.4-unit
                // wingspan and Z is the 7.8-unit body, so Z is already forward.
                Eagle = Actor(EagleModel,
                              animator: "Assets/_Project/Animation/Eagle_B1.controller"),

                // No general fallback vehicle any more. The improvised wagons that
                // stood here — Wagon.fbx and WagonTreasure.fbx, a crate on wheels made
                // before there were any carts — are deleted with the rest of the old
                // kit. If a role ever goes unfilled, RunVisuals still assembles a cart
                // out of the crate and barrel below, which is the fallback that was
                // always underneath this one.

                // One per role, and three silhouettes that cannot be confused from above.
                //
                // This is what the wagon pack was bought for (docs/status.md §8): the
                // supply, war and treasure wagons being three vehicles rather than one
                // model in three colours. Colour is the first thing a moving object loses
                // against a hillside in shadow, and the player has to be able to tell at
                // a glance which one the bandits are converging on (docs/GDD.md §5).
                //
                // Barrels roped to an open bed; shields down both sides; a canvas hood.
                // The merchant's cart was the other candidate for the treasure and it is
                // a market stall — table cloth, plates, a mug — which reads as somewhere
                // a caravan stops rather than as part of one.
                WagonSupply = One($"{WagonDir}/Supply_Wagon/SM_Supply_Wagon_Full.prefab"),
                WagonWar = One($"{WagonDir}/War_Wagon/SM_War_Wagon_Full.prefab"),
                WagonTreasure = One($"{WagonDir}/Covered_Wagon/SM_Covered_Wagon_Full.prefab"),
                // Off the RTS kit and onto Synty with everything else. These are the
                // improvised wagon — a crate on wheels — and it is a fallback for when
                // no cart model is loaded, so it still has to match the world it stands
                // in. The wagon pack replaces the whole assembly.
                WagonBody = One($"{SyntyGenericDir}/Props/SM_Gen_Prop_Crate_01.prefab"),
                WagonCargo = One($"{SyntyGenericDir}/Props/SM_Gen_Prop_Barrel_Wood_01.prefab"),

                // Off the pirate pack and onto the RPG one. Every pirate model shares
                // a single atlas material per asset, and that atlas is not in this
                // project — so the chest holding the level's silver was rendering as a
                // white box. The RPG chest carries its colours in its materials, the
                // way the swords and bows already do, and reads as gold from the air.
                // Both onto Synty. A cache is a chest somebody hid and a sprung trap is
                // a skull somebody left: two of the few props in this game the player
                // is meant to look *at* rather than past, which makes matching the
                // world they sit in worth more here than anywhere else.
                SilverCache = One($"{SyntyNatureDir}/Props/SM_Prop_Chest_Wood_01.prefab"),
                TrapMarker = One($"{SyntyNatureDir}/Props/SM_Prop_Skull_01.prefab")
            };
        }

        /// <summary>Pairs a model with the controller generated for it, matched by filename.</summary>
        /// <summary>
        /// Degrees the ForestAnimals models need turning so their noses lead.
        ///
        /// They are modelled along X while the code turns an actor by pointing its +Z
        /// at what it is heading for, so without this a wolf charges the caravan
        /// broadside — which is what "the wolf slides sideways" was, and it looked like
        /// a movement bug rather than an import one.
        ///
        /// Ninety rather than minus ninety is a coin toss no file can settle: both put
        /// the body the right way round and only one puts the head at the front. It is
        /// a field on the model, so it can be flipped in the Inspector under
        /// Level Runner > Models while the game runs, which beats reasoning about it.
        /// </summary>
        const float ForestAnimalYaw = 90f;

        /// <summary>
        /// The medieval army pack's characters, which is where the troops come from now.
        ///
        /// Note the spaces in the folder name — they are the pack's, not a typo, and
        /// `AssetDatabase` is fine with them.
        /// </summary>
        const string ArmyDir = "Assets/Stylized_Medieval_Army_Pack/Prefabs - Characters";

        /// <summary>The army pack's scenery: stakes, banners, wreckage, worn ground.</summary>
        const string ArmyProps = "Assets/Stylized_Medieval_Army_Pack/Prefabs - Environment";

        static PropSet ArmyScenery(params string[] names)
            => new PropSet(false, Load(ArmyProps, names));


        /// <summary>
        /// One of the army pack's characters, pointed at the one controller they share.
        ///
        /// Shared because they share a skeleton — 52 characters assembled from 22 meshes
        /// — so a clip that binds to one binds to all of them. Without this `Actor` looks
        /// for a controller named after each prefab and finds nothing, fifty-two times.
        /// </summary>
        static ActorModel Army(string name)
            => Actor($"{ArmyDir}/{name}.prefab", animator: AnimatorBuilder.ArmyController,
                     borrowed: true);

        static ActorModel Actor(string path, string weaponPath = null, float weaponLength = 0f,
                                string[] hide = null, string[] unsized = null,
                                string animator = null, float yaw = 0f, bool borrowed = false)
        {
            var prefab = One(path);
            if (prefab == null) return default;

            // A pack that ships its own controller keeps it. Only the models we built
            // controllers for by hand live under _Project/Animation, and asking for one
            // there for a pack that already has a working animator would rebuild work
            // that has been done.
            string controllerPath = animator
                ?? $"Assets/_Project/Animation/{Path.GetFileNameWithoutExtension(path)}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
                Debug.LogWarning($"[Arna] No animator for {Path.GetFileName(path)} — run Arna > Build Animator Controllers.");

            return new ActorModel
            {
                Prefab = prefab,
                Animator = controller,
                Rig = AvatarFor(prefab),
                Borrowed = borrowed,
                Weapon = weaponPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath),
                WeaponLength = weaponLength,

                // Laid along the hand rather than sticking out of the back of it. The
                // packs disagree on which axis a blade runs down, so this is a fixed
                // correction found by looking rather than a value from the files.
                WeaponRotation = new Vector3(-90f, 0f, 0f),

                YawOffset = yaw,

                Hide = hide,
                Unsized = unsized
            };
        }

        /// <summary>
        /// The avatar a model's clips would be retargeted onto.
        ///
        /// Two places to look, and the second is the one that matters here. A model whose
        /// own Animator carries an avatar has already answered the question. A prefab
        /// assembled from meshes has not: the avatar belongs to the **file the mesh came
        /// from**, which nothing in this project names — so it is found by asking the
        /// mesh which asset it lives in, rather than by keeping a table of prefab-to-FBX
        /// that would be wrong the first time the pack is updated.
        /// </summary>
        static Avatar AvatarFor(GameObject prefab)
        {
            if (prefab == null) return null;

            var own = prefab.GetComponentInChildren<Animator>(true);
            if (own != null && own.avatar != null) return own.avatar;

            foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;

                string path = AssetDatabase.GetAssetPath(skin.sharedMesh);
                if (string.IsNullOrEmpty(path)) continue;

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Avatar avatar) return avatar;
            }

            return null;
        }

        static GameObject One(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) Debug.LogWarning($"[Arna] Model not found, falling back to a primitive: {path}");
            return asset;
        }

        /// <summary>
        /// Collects the scenery models for the forest chapter.
        ///
        /// Wired up by path rather than dragged into the inspector so the scene can be
        /// rebuilt from scratch on any machine. Missing models are skipped silently —
        /// the level still runs, it just runs on bare ground.
        /// </summary>
        static BiomeDecor LoadForestDecor()
        {
            // Two packs, two up axes, measured rather than assumed. The stylized nature
            // models arrive Y-up and already in metres — a pine is 7.3 m tall as
            // exported — while the RTS scenery is Z-up and miniature, at 0.72 x 0.45 x
            // 0.93 for a whole tree. Getting either wrong lays the models on their side
            // and then scales them by their width, which looks like two separate bugs.
            return new BiomeDecor
            {
                // Everything here is Synty. The old kit is gone, not layered under this
                // one: two artists' idea of a spruce standing in the same wood is the
                // seam the whole swap was made to close (docs/status.md §8).
                //
                // Three tree species rather than two. Two read as two species; three
                // read as a wood, and the birch was already in the pack and unused. The
                // pale trunk is the only light vertical line in a forest otherwise made
                // of dark ones.
                Pines = Synty("Trees", "SM_Tree_PolyPine_01", "SM_Tree_PolyPine_02",
                              "SM_Tree_PolyPine_03", "SM_Tree_PolyPine_Sparse_01",
                              "SM_Tree_PolyPine_Sparse_02", "SM_Tree_PolyPine_Sparse_03",
                              "SM_Tree_Pine_01", "SM_Tree_Pine_02"),

                Trees = Synty("Trees", "SM_Tree_Round_01", "SM_Tree_Round_02", "SM_Tree_Round_03",
                              "SM_Tree_Round_04", "SM_Tree_Round_05", "SM_Tree_TallRound_01",
                              "SM_Tree_01", "SM_Tree_02", "SM_Tree_03", "SM_Tree_04"),

                Birch = Synty("Trees", "SM_Tree_Birch_01", "SM_Tree_Birch_02",
                              "SM_Tree_Birch_03", "SM_Tree_Birch_04", "SM_Tree_Birch_Small_01"),

                // The marsh gets the swamp trees as well as the bare dead ones. A fen
                // with nothing but grey sticks in it is a diagram of a fen.
                DeadTrees = Synty("Trees", "SM_Tree_Dead_01", "SM_Tree_Dead_02", "SM_Tree_Dead_03",
                                  "SM_Tree_Pine_Dead_01", "SM_Tree_Generic_Dead_01",
                                  "SM_Tree_Swamp_01", "SM_Tree_Swamp_02", "SM_Tree_Swamp_03",
                                  "SM_Tree_Swamp_Branch_01", "SM_Tree_Swamp_Stump_01"),

                // The layer between the grass and the trees. Without it a forest is
                // trunks standing in a lawn, which is what the old one was.
                Bushes = Synty("Plants", "SM_Plant_Bush_01", "SM_Plant_Bush_02", "SM_Plant_Bush_03",
                               "SM_Plant_Bush_Leaves_01", "SM_Plant_Bush_Leaves_02",
                               "SM_Plant_Bush_Leaves_03", "SM_Plant_Hedge_Bush_01",
                               "SM_Plant_Undergrowth_01"),

                Rocks = Synty("Rocks", "SM_Rock_01", "SM_Rock_02", "SM_Rock_03", "SM_Rock_04",
                              "SM_Rock_Rounded_01", "SM_Rock_Small_01", "SM_Rock_Small_02",
                              "SM_Rock_Pile_01", "SM_Rock_Pile_02", "SM_Rock_Pile_03"),

                // Slabs and clusters, sized across rather than up. A pebble is texture;
                // a boulder is something the eye steers round.
                Boulders = Synty("Rocks", "SM_Rock_Boulder_01", "SM_Rock_Cluster_Large_01",
                                 "SM_Rock_Cluster_Large_02", "SM_Rock_Cluster_Large_03",
                                 "SM_Rock_Cluster_Large_04", "SM_Rock_Cluster_Large_05",
                                 "SM_Rock_Cluster_Large_06"),

                // Grass first and by a wide margin. The floor is grass with things in
                // it, not a flowerbed with grass round the edges — so the five grasses
                // are listed twice and everything showier once.
                //
                // No SM_Plant_PurpleFlower_01, and the reason outlives the pack it was
                // learned on: a violet plant at any weight takes over the middle
                // distance, because violet is the one colour nothing else out there is.
                GroundCover = Synty("Plants",
                                    "SM_Plant_Grass_01", "SM_Plant_Grass_02", "SM_Plant_Grass_03",
                                    "SM_Plant_Grass_04", "SM_Plant_Grass_05",
                                    "SM_Plant_Grass_01", "SM_Plant_Grass_02", "SM_Plant_Grass_03",
                                    "SM_Plant_Grass_04", "SM_Plant_Grass_05",
                                    "SM_Plant_Fern_01", "SM_Plant_Fern_02", "SM_Plant_Fern_03",
                                    "SM_Plant_Fern_Leaves_01", "SM_Plant_Fern_Leaves_02",
                                    "SM_Plant_Flowers_01", "SM_Plant_FlowerPatch_01",
                                    "SM_Plant_Mushrooms_01", "SM_Plant_Mushrooms_02",
                                    "SM_Plant_Mushrooms_03", "SM_Plant_Reeds_01"),

                // The skyline. Both packs' mountains together, because a range is meant
                // to vary along its length and this is the one place mixing them cannot
                // show: they are three hundred metres off, in silhouette, and no two
                // adjacent peaks are ever compared closely. Generic's are literally
                // named Background_Mountain — made for exactly this.
                // PolygonNature's three peaks only. The generic pack's background
                // mountains were the rest of this set and they are out with everything
                // else that is not the nature pack.
                Horizon = Synty("Terrain", "SM_Terrain_Mountain_01",
                                "SM_Terrain_Mountain_02", "SM_Terrain_Mountain_03"),

                MarshPlants = Mixed(
                    Load($"{SyntyNatureDir}/Plants", new[]
                    {
                        "SM_Plant_Reeds_01", "SM_Plant_Reeds_02",
                        "SM_Plant_Reeds_01", "SM_Plant_Reeds_02",
                        "SM_Plant_Fern_01", "SM_Plant_Bush_Leaves_01"
                    }),
                    Load($"{SyntyNatureDir}/Terrain", new[]
                    {
                        "SM_Swamp_Root_01", "SM_Swamp_Root_02",
                        "SM_Terrain_Swamp_Growth_01", "SM_Terrain_Swamp_Growth_02",
                        "SM_Terrain_Swamp_Growth_03"
                    })),

                // Back on the water, and back in their own set. Note the pack's own
                // spelling: two Ls. Loading these by the name they ought to have had is
                // a silent miss, and a silent miss here looks exactly like the decision
                // to leave them out.
                Lilypads = Synty("Plants", "SM_Plant_Lillypad_Small_01",
                                 "SM_Plant_Lillypad_Small_01",
                                 "SM_Plant_Lillypad_Large_01", "SM_Plant_Lillypad_Large_02",
                                 "SM_Plant_Lillypad_Large_03"),

                // Fallen wood on the forest floor, off the nature pack rather than the
                // RTS kit's stacked lumber. A log lying where it fell is woodland; a
                // neat stack is an industry, and there is nobody out here to run one.
                Timber = Synty("Trees", "SM_Tree_Log_01", "SM_Tree_Log_02", "SM_Tree_Stump_01",
                               "SM_Tree_Stump_02", "SM_Tree_Stump_03", "SM_Tree_Stump_04",
                               "SM_Tree_Branch_01"),

                // The trap-field tell, and it is now what the reference picture shows:
                // a wrecked cart, bones, a dropped chest. The old marker was one cart
                // model borrowed from a village pack. Nothing about a wreck should look
                // tidy, so the set is deliberately mixed.
                // The trap tell, and it is finally the thing the reference picture shows:
                // a cart left where it stopped. That reference is a battle map called
                // Övergiven Väg — an abandoned road — and what says so in it is wrecked
                // wagons among the bones, not masonry. Masonry means somebody built
                // here; a cart in a field means somebody died here.
                //
                // Mixed from both packs on purpose. A wreck is the one place tidiness
                // would be wrong, and the carts are the same kind of vehicle the player
                // is escorting — which is the whole of what the signal says.
                // The totem the GDD's §5 table has always asked for and no pack here had.
                // A banner or a row of stakes is a thing somebody drove into the ground,
                // which is what separates an ambush site from an accident.
                Markers = new PropSet(),

                // The surface of the water, which is the one prop here that replaces a
                // tile instead of dressing one. See BiomeDecor.Water.
                // **Nothing, and both of the alternatives were tried.**
                //
                // The nature pack's river plane carries a water shader from another
                // pipeline: URP resolves it to untextured white, so every watercourse
                // became a sheet of paper. Painting it a river blue fixed the white and
                // produced a dark slab with hard tile edges, which is worse in its own
                // way. With no plane the water is the terrain mesh in the palette's own
                // colour — the same material as the ground it runs through, so it reads
                // as part of the map rather than as something laid on it.
                //
                // What it is not is a *surface*. The tile staircase shows again, which is
                // the thing the planes were introduced to hide. That wants a shader
                // rather than a prop.
                Water = new PropSet(),

                // A crossing on the ford tiles. Every corridor tends to use the same
                // ford — it is why the traps go there — and it has never had anything on
                // it but water you could somehow walk through.
                Fords = Synty("Props", "SM_Prop_Bridge_Curved_01"),

                // Rock for the tiles the map calls cliff, which have been impassable and
                // featureless since the generator was written.
                //
                // Rock only. The dirt cliffs are a slab of earth with the roots of what
                // grew on it hanging out of the underside — meant to be set into a drop
                // so that only the earth face shows. On flat ground the whole thing
                // stands proud, roots and all, and a twelve-metre one reads as an
                // enormous tree: the first question asked about it was what kind.
                // Rock for the tiles the map calls cliff. PolygonGeneric's, which is
                // another pack but is stone and belongs outdoors. Not the dirt cliffs:
                // those are a slab of earth with roots hanging out of the underside,
                // meant to be set into a drop, and standing free they read as a tree.
                Cliffs = Mixed(
                    Load($"{SyntyGenericDir}/Environment", new[]
                    {
                        "SM_Gen_Env_Cliff_01", "SM_Gen_Env_Cliff_02", "SM_Gen_Env_Cliff_03",
                        "SM_Gen_Env_Cliff_04"
                    })),

                // What a band of raiders lives in. One per group, not a village.
                Camps = new PropSet(),

                // The one tree whose place is decided by water rather than by biome.
                Willows = Synty("Trees", "SM_Tree_Willow_Small_01", "SM_Tree_Willow_Medium_01",
                                "SM_Tree_Willow_Large_01"),

                // Stone a river put there, rather than stone that happens to be near
                // one — the curved piles are made to follow a waterline.
                // **Nothing, and the empty field is the finding.**
                //
                // SM_MountainSkybox_01 is a bowl, not a band: a ring of peaks around a
                // flat floor, made to be scaled up around a terrain that reaches its
                // edge. This map is a 256 m island, the bowl is 1600 m across, and the
                // floor is laid at y=0 because that is where a backdrop's base belongs —
                // so the island sat in eight hundred metres of flat grey in every
                // direction and read as a sea.
                //
                // Sinking it does not help. The floor is wider than anything that could
                // hide it, so from a camera above the caravan there is always some of it
                // showing below the peaks. The horizon this project wanted is the ring
                // PlaceHorizon already stands at 380 m; this was the belt as well as the
                // braces, and it cost the sky.
                //
                // The pass stays. A backdrop that is a *band* would work in it.

                Shore = Synty("Rocks", "SM_Rock_Pile_01", "SM_Rock_Pile_02", "SM_Rock_Pile_03",
                              "SM_Rock_Pile_04", "SM_Rock_Pile_05",
                              "SM_Rock_Pile_Curved_01", "SM_Rock_Pile_Curved_02"),

                // The wrecked carts and the arrows are out with the rest of the other
                // packs; the bones are PolygonNature's own and stay, which is enough for
                // a trap site to say something happened here.
                Ruins = Mixed(
                    Load($"{SyntyNatureDir}/Props", new[]
                    {
                        // The bones the GDD's §5 table calls for. `_Skull_01` twice, so a
                        // site is as likely to be bones as anything else in the set: the
                        // table names bone piles and totems as *the* trap-field tell, and
                        // one skeleton among six props made it the rarest of them.
                        //
                        // A pile it is not. The pack has one skeleton and one skull, and
                        // the decorator places a single prop per site, so what stands
                        // there is a body or a skull rather than a heap of them. Nor is
                        // there a totem in either pack — the nearest thing is
                        // `_TorchStick_01`, which is a torch. The medieval pack arriving
                        // has archer stakes and banners, which are what a totem wants.
                        "SM_Prop_Skeleton_Ground_01", "SM_Prop_Skull_01", "SM_Prop_Skull_01",
                        "SM_Prop_Chest_Wood_01", "SM_Prop_Grave_03", "SM_Prop_CampFire_01"
                    })),

                // Bare earth, gravel and worn grass, laid flat on the ground.
                //
                // From PolygonGeneric, which is the only pack here with ground surfaces
                // at all — and which was on the deletion list for having a modern name
                // and a folder full of air conditioners. Mixing two Synty packs is
                // allowed here and nowhere else: ground is seen flat and mostly in
                // shadow, not a spruce beside another artist's spruce.
                // The army pack's worn ground goes in with it, on that same argument:
                // its mud and its trodden path are flat pieces seen face-on, which is
                // the one category where two artists' work does not show a seam.
                // Bare earth, and nothing green.
                //
                // The three grass pieces in this folder are five-metre discs of a darker
                // green laid on green: not a patch of anything, a blob, and no number of
                // tufts stood on top of them changes that. What earns its place is the
                // dirt — the reference picture is half trodden ground — and it is dirt
                // wherever it comes from.
                GroundPatches = Mixed(
                    Load($"{SyntyGenericDir}/Environment", new[]
                    {
                        "SM_Gen_Env_Ground_Dirt_01", "SM_Gen_Env_Ground_Dirt_02",
                        "SM_Gen_Env_Ground_Dirt_03", "SM_Gen_Env_Ground_Dirt_04",
                        "SM_Gen_Env_Ground_Dirt_Large_01", "SM_Gen_Env_Ground_Dirt_Large_02",
                        "SM_Gen_Env_Ground_River_Dirt_01",
                        "SM_Gen_Env_Ground_River_Dirt_02", "SM_Gen_Env_Ground_River_Dirt_03"
                    }),
                    Load(ArmyProps, new[] { "Mud_1", "Path" }))

                // Houses, Farms and Watchtowers are left empty, and that is the honest
                // state rather than an oversight. Neither Synty pack has a medieval
                // building: PolygonNature has none at all and PolygonGeneric's are
                // modern city blocks. They come back with POLYGON Knights (§8). An
                // empty set places nothing, so the map is wilderness until then — which
                // is what the reference picture is, and arguably what a road through the
                // provinces should have been all along.
            };
        }

        /// <summary>
        /// POLYGON Nature, which files its content by kind: Trees, Rocks, Plants,
        /// Terrain, Props. The group is a parameter rather than five constants because
        /// the same call has to reach all five and the folder names are the pack's own.
        /// </summary>
        const string SyntyNaturePack = "Assets/Synty/PolygonNature";
        const string SyntyNatureDir = SyntyNaturePack + "/Prefabs";

        /// <summary>
        /// Prefabs from POLYGON Nature, which is Y-up like every Synty pack.
        ///
        /// Authored scale does not matter here and that is worth knowing before anyone
        /// goes measuring: <see cref="TerrainDecorator"/> refits every prop to its own
        /// table — a pine to eight and a half metres, a rock to two point two — so a
        /// pack exported at ten times life size lands at the same height as one exported
        /// at life size. The up axis is the part a pack can get wrong in a way nothing
        /// downstream can correct.
        /// </summary>
        /// <summary>
        /// What the planning map is dressed with: the world's scenery, minus the two
        /// sets that hide the thing the map is for.
        ///
        /// The skyline stands three hundred metres beyond the map's edge, which is exactly
        /// what it is for from inside the world and exactly what is wrong with it on a
        /// map: a ring of peaks around the sheet, drawn outside the ground the player is
        /// reading, with nothing to say about the route. Seen from directly above, a
        /// mountain is a dome with a shadow beside it and no information in either.
        ///
        /// A plain view difference and not a preference: the play camera keeps the range,
        /// because from 46 m back and 32 m up a mountain on the skyline is the thing that
        /// says the country goes on past the level.
        /// </summary>
        static BiomeDecor LoadPlanDecor()
        {
            var decor = LoadForestDecor();

            decor.Horizon = new PropSet();

            return decor;
        }

        static PropSet Synty(string group, params string[] names)
            => new PropSet(false, Load($"{SyntyNatureDir}/{group}", names));

        /// <summary>
        /// PolygonGeneric, which is here for one folder.
        ///
        /// Half the pack is modern — sidewalks, air conditioning, tyre marks — and on
        /// that basis it was very nearly deleted as something that came along for the
        /// ride. Its `Environment` folder is the ground kit: dirt, gravel and worn-grass
        /// surfaces, riverbanks and slopes, none of which PolygonNature has at all.
        /// </summary>
        const string SyntyGenericDir = "Assets/Synty/PolygonGeneric/Prefabs";

        /// <summary>
        /// Medieval Wagons, Carts &amp; Carriages Vol. 1. Ten vehicles, each shipped as
        /// loose parts and one assembled `_Full` prefab; this game wants the assembled
        /// ones and nothing else.
        ///
        /// It ships 2K and 4K PBR textures, which is not the flat-atlas look the rest of
        /// the world is drawn in. Two import settings close most of that gap — textures
        /// down to 1K, smoothness low — and what gives a photoreal asset away beside a
        /// stylized one at forty-six metres is the gloss and the normal map, not the
        /// model. Not yet done.
        /// </summary>
        const string WagonDir = "Assets/3DreaMax Studio/003_MDVL_WagonsCartsCarriages_Vol_1/Prefabs";

        static PropSet Generic(string group, params string[] names)
            => new PropSet(false, Load($"{SyntyGenericDir}/{group}", names));

        /// <summary>
        /// One set drawn from more than one folder.
        ///
        /// Only the skyline uses it. Everywhere else a set comes from one pack on
        /// purpose — two artists' spruces standing side by side is the seam the whole
        /// swap was made to close — but a peak three hundred metres off in silhouette
        /// has no detail left to disagree about, and a range wants variety along its
        /// length more than it wants one author.
        /// </summary>
        static PropSet Mixed(params GameObject[][] groups)
        {
            var all = new System.Collections.Generic.List<GameObject>();
            foreach (var group in groups) all.AddRange(group);
            return new PropSet(false, all.ToArray());
        }

        /// <summary>
        /// Loads scenery by name, preferring a prefab over the raw model.
        ///
        /// It used to look for `.fbx` and nothing else, which was true of every pack in
        /// the project until Synty arrived. Synty ships its content as prefabs and the
        /// FBX beside them carries no materials, so an fbx-only reader would have found
        /// the shape of a forest painted entirely in Unity's default grey — the exact
        /// failure the previous pack swap was made to fix.
        ///
        /// Prefab first, model second, and a warning naming both when neither is there.
        /// The warning matters more than it looks: a missing prop is skipped silently by
        /// everything downstream, so a mistyped name is a bare hillside and no error.
        /// </summary>
        static GameObject[] Load(string folder, string[] names)
        {
            var found = new System.Collections.Generic.List<GameObject>();
            foreach (var name in names)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.prefab")
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.fbx");

                if (asset != null) found.Add(asset);
                else Debug.LogWarning($"[Arna] Scenery model not found: {folder}/{name} (.prefab or .fbx)");
            }
            return found.ToArray();
        }

        static void EnsureFolders()
        {
            foreach (var path in new[] { SettingsDir, ScenesDir, MaterialsDir })
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                var parent = Path.GetDirectoryName(path).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        static UniversalRenderPipelineAsset EnsureRenderPipeline()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (existing == null)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, RendererPath);
                }

                existing = UniversalRenderPipelineAsset.Create(rendererData);
                existing.name = "ArnaUniversalRenderPipeline";
                AssetDatabase.CreateAsset(existing, PipelinePath);
                AssetDatabase.SaveAssets();
            }

            // Mobile profile: no shadow cascades to speak of, modest shadow distance,
            // HDR off. See docs/technical-design.md §1 for the budget these serve.
            existing.supportsHDR = false;
            existing.msaaSampleCount = 2;
            // Far enough to cover the plan view, which looks down from seventy metres
            // and needs the whole canopy to cast. The play view never sees past about
            // thirty, so nothing is lost there.
            existing.shadowDistance = 90f;
            existing.shadowCascadeCount = 1;

            GraphicsSettings.defaultRenderPipeline = existing;
            QualitySettings.renderPipeline = existing;
            return existing;
        }

        /// <summary>Horizon colour. Camera background, fog and sky ambient all use it,
        /// so distant ground dissolves into the sky instead of ending at a hard line.</summary>
        static readonly Color SkyColor = new Color(0.62f, 0.75f, 0.85f);

        /// <summary>
        /// Scene lighting for the play view.
        ///
        /// Unity's defaults give a scene flat white ambient from every direction and no
        /// fog at all. The result is a picture with no air in it: a hill two hundred
        /// metres off is drawn as crisply as the wagon in front of the camera, so the
        /// eye reads the whole thing as one flat plane. These four settings are most of
        /// the difference between "assets placed on a mesh" and "a landscape".
        /// </summary>
        static void ApplyOutdoorLighting()
        {
            // Sky above, bounced green from below. Ambient is what fills the shadows,
            // and shadows filled with grey light look like dust while shadows filled
            // with sky and grass look like shade.
            // Kept deliberately dim. Ambient plus sunlight multiply the ground's own
            // colour, and the first attempt summed to 1.37 — every surface rendered at
            // 137 % of its albedo, which turned a muted olive into neon and left no
            // headroom for a shadow to darken anything into. Lit ground should land
            // near 1.0 so the palette is the colour you actually see.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.42f, 0.50f);

            // The equator band lights vertical surfaces, which is every cliff face and
            // every tree trunk in the scene. Set it well below the sky — as the first
            // attempt did — and a mountain turned away from the sun goes to near black,
            // because ambient is the only light reaching it. It sits close to the sky
            // colour for the same reason a real cliff in shade is grey, not black: most
            // of what it can see is sky.
            RenderSettings.ambientEquatorColor = new Color(0.33f, 0.36f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.21f, 0.22f, 0.17f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;

            // Starts well beyond the caravan so the thing the player is watching is
            // never washed out.
            RenderSettings.fogStartDistance = 70f;

            // The end moved from 320 to 520 when the skyline went in, and the old value
            // is why the skyline did not work the first time. Linear fog reaches full
            // sky colour at its end distance, so 320 meant the world visually stopped
            // there — and a ring of mountains placed at exactly 320 m was rendered as
            // pure sky. The peaks were in the scene, correctly sized and correctly
            // placed, and every pixel of them was the colour of the air.
            //
            // 520 puts a peak at 300 m at about half its own colour: a pale blue
            // silhouette, which is what a mountain twenty minutes' walk away actually
            // looks like. The cost is real and worth stating — the map's own far edge,
            // 250 m off, goes from 28 % of its colour to 60 %, so the middle distance
            // carries less haze than the value tuned for a 300-metre landscape gave it.
            // The landscape is 640 metres deep now. Retuning the air is what adding a
            // horizon costs.
            RenderSettings.fogEndDistance = 520f;

            RenderSettings.skybox = null;
        }

        static Material EnsureRouteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RouteMaterialPath);
            if (material != null) return material;

            var shader = Shader.Find("Arna/RouteOverlay");
            if (shader == null)
                throw new InvalidOperationException("Shader 'Arna/RouteOverlay' not found.");

            material = new Material(shader) { name = "RouteOverlay" };
            AssetDatabase.CreateAsset(material, RouteMaterialPath);
            return material;
        }

        static Material EnsureGroundMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);

            if (material == null)
            {
                var shader = Shader.Find("Arna/TerrainGround");
                if (shader == null)
                    throw new InvalidOperationException("Shader 'Arna/TerrainGround' not found.");

                material = new Material(shader) { name = "TerrainGround" };
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }

            // Set every time rather than only on creation. A material asset keeps the
            // values it was born with, so changing a default in the shader silently
            // fails to reach a project that already has the file — which is every
            // project except a brand new one.
            material.SetFloat("_ShadowStrength", 0.85f);
            material.SetFloat("_AmbientBoost", 1f);

            // Forest floor: leaf litter, twigs and soil. Used as grain rather than as
            // colour, so one texture serves every terrain type — the green of grass and
            // the grey of rock still come from the vertex colours underneath.
            var detail = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Textures/Terrain/forest_floor_Diffuse.jpg");
            if (detail != null) material.SetTexture("_DetailMap", detail);
            else Debug.LogWarning("[Arna] Ground detail texture missing; the ground will be flat colour.");

            material.SetFloat("_DetailTiling", 6f);
            material.SetFloat("_DetailStrength", 0.55f);
            material.SetFloat("_MacroTiling", 41f);
            material.SetFloat("_MacroStrength", 0.35f);
            material.SetFloat("_DebugShadow", 0f);

            // Left behind by an earlier [Toggle] on the diagnostic property, which was
            // the wrong attribute for something with five modes. The keyword does
            // nothing, but a stale keyword in a committed asset invites the next person
            // to go looking for what turns it on.
            material.DisableKeyword("_DEBUGSHADOW_ON");
            EditorUtility.SetDirty(material);

            return material;
        }

        static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            var shader = Shader.Find("Arna/TerrainVertexColor");
            if (shader == null)
                throw new InvalidOperationException("Shader 'Arna/TerrainVertexColor' not found.");

            material = new Material(shader) { name = "TerrainOverview" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static void BuildPreviewScene(Material terrainMaterial)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The map is 64 tiles of 4 m, so 256 m square centred on (128, 0, 128).
            const float mapExtent = 64 * 4f;

            var centre = new Vector3(mapExtent * 0.5f, 0f, mapExtent * 0.5f);

            // Straight down. The old view was pitched 55 degrees, which is a landscape
            // photograph of a map rather than a map: the far half of the country is
            // squashed and the near half is not, so two routes of equal length do not
            // look equal. Read from directly above, distance on screen is distance on
            // the ground everywhere.
            var rotation = Quaternion.Euler(90f, 0f, 0f);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            // Low, despite looking straight down. An orthographic camera frames by its
            // size, not its distance, so height is free to choose — and URP measures
            // shadow range from the camera. Parked at four hundred metres the whole map
            // fell outside the shadow distance and nothing cast at all. Raising that
            // distance instead would have spread the same shadow map over five times
            // the ground and coarsened the play view along with it.
            cameraGo.transform.SetPositionAndRotation(centre + Vector3.up * 70f, rotation);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.11f, 0.10f);

            // Orthographic, so the map has no vanishing point and its edges stay
            // parallel. Perspective made the far edge shrink away, which is exactly
            // wrong for a view whose whole job is comparing routes across the map.
            camera.orthographic = true;
            camera.orthographicSize = mapExtent * 0.5f * 1.02f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 200f;

            ApplyOutdoorLighting();

            // No fog on the plan. Fog reads as distance, and from straight above there
            // is no distance to read — it would only grey out the middle of the map.
            RenderSettings.fog = false;

            var lightGo = new GameObject("Directional Light");

            // Steeper than the play view's sun. Shadows here are there to give the
            // canopy and the cliffs relief, not to stretch across the ground; long
            // shadows seen from above hide as much terrain as they describe.
            lightGo.transform.rotation = Quaternion.Euler(58f, -40f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.97f, 0.91f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;

            var terrainGo = new GameObject("LevelPreview");
            terrainGo.AddComponent<MeshFilter>();
            terrainGo.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;

            var preview = terrainGo.AddComponent<LevelPreview>();
            preview.Decor = LoadPlanDecor();

            // The cast, for the eagle. The plan draws one actor and only one: the bird
            // flying the scouting ability's own flight over the ground it scouts.
            preview.Models = LoadModels();

            preview.RouteMaterial = EnsureRouteMaterial();
            preview.Rebuild();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>
        /// Renders the preview scene to a PNG. Used to check generator output without
        /// opening the editor: -executeMethod Arna.Editor.ArnaSetup.CaptureLevelPreview
        /// -arnaOutput &lt;path&gt; [-arnaChapter N] [-arnaLevel N]
        /// </summary>
        public static void CaptureLevelPreview()
        {
            string output = ArgValue("-arnaOutput") ?? "Logs/level-preview.png";
            int chapter = int.TryParse(ArgValue("-arnaChapter"), out var c) ? c : 1;
            int level = int.TryParse(ArgValue("-arnaLevel"), out var l) ? l : 1;
            int width = int.TryParse(ArgValue("-arnaWidth"), out var w) ? w : 1280;
            int height = int.TryParse(ArgValue("-arnaHeight"), out var h) ? h : 720;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var preview = UnityEngine.Object.FindFirstObjectByType<LevelPreview>();
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (preview == null || camera == null)
                throw new InvalidOperationException("Preview scene is missing its camera or LevelPreview.");

            preview.Chapter = chapter;
            preview.Level = level;
            preview.Rebuild();

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };
            var previousActive = RenderTexture.active;

            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllBytes(output, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);

                Debug.Log($"[Arna] Captured {chapter}-{level} (seed {preview.Seed}, " +
                          $"attempts {preview.Attempts}, valid {preview.ChoiceValidated}, " +
                          $"fastest {preview.FastestRouteCost:F1}, overlap {preview.MaxOverlap:P0}) -> {output}");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Builds a level in the play scene and renders it, without entering play mode.
        ///
        /// -executeMethod Arna.Editor.ArnaSetup.CapturePlayScene -arnaOutput &lt;path&gt;
        /// [-arnaChapter N] [-arnaLevel N] [-arnaSteps N]
        ///
        /// The steps argument advances the simulation before the shot, so the caravan
        /// can be caught mid-journey rather than sitting at the start line.
        /// </summary>
        public static void CapturePlayScene()
        {
            string output = ArgValue("-arnaOutput") ?? "Logs/play.png";
            int chapter = int.TryParse(ArgValue("-arnaChapter"), out var c) ? c : 1;
            int level = int.TryParse(ArgValue("-arnaLevel"), out var l) ? l : 1;
            int steps = int.TryParse(ArgValue("-arnaSteps"), out var s) ? s : 0;
            int width = int.TryParse(ArgValue("-arnaWidth"), out var w) ? w : 1400;
            int height = int.TryParse(ArgValue("-arnaHeight"), out var h) ? h : 1000;

            // Without this the editor compiles shader variants in the background and
            // renders whatever is ready, which for a freshly edited shader is a variant
            // with none of its keywords. That produced a capture showing no shadows at
            // all and sent a morning into diagnosing a shader that was working — the
            // picture was simply taken before the shader was.
            ShaderUtil.allowAsyncCompilation = false;

            EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Single);

            var runner = UnityEngine.Object.FindFirstObjectByType<LevelRunner>();
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (runner == null || camera == null)
                throw new InvalidOperationException("Play scene is missing its camera or LevelRunner.");

            runner.Chapter = chapter;
            runner.Level = level;
            runner.FollowCaravan = true;

            // Lets a capture move in close enough to check whether the actors are
            // actually posed rather than standing in bind pose.
            if (float.TryParse(ArgValue("-arnaCamDistance"), out float distance)) runner.FollowDistance = distance;
            if (float.TryParse(ArgValue("-arnaCamHeight"), out float camHeight)) runner.FollowHeight = camHeight;
            runner.Restart();
            runner.StepTimes(steps);

            // Update() never runs in a headless editor session, so the camera has to be
            // pointed at the column explicitly after the simulation has moved it.
            runner.AimCamera();
            ReportShadowState(runner.GetComponent<MeshRenderer>().sharedMaterial);

            // -arnaLitGround swaps the ground onto Unity's own Lit shader. It settles
            // the one question a picture with no shadows in it cannot answer on its
            // own: whether the fault is in our shader or in the scene around it.
            if (ArgValue("-arnaLitGround") != null)
            {
                var stock = Shader.Find("Universal Render Pipeline/Lit");
                var probe = new Material(stock) { name = "ShadowProbe" };
                probe.SetFloat("_Smoothness", 0f);
                probe.SetColor("_BaseColor", new Color(0.45f, 0.50f, 0.35f));
                runner.GetComponent<MeshRenderer>().sharedMaterial = probe;
                Debug.Log("[Arna] Ground swapped to stock URP Lit for this capture.");
            }

            // Always written, never only when asked. Setting a property on a shared
            // material edits the asset, and Unity saves it on quit — so one diagnostic
            // capture left every later capture drawing its debug output, and the mode
            // silently carried across runs.
            {
                if (!float.TryParse(ArgValue("-arnaDebugShadow"), out float debugShadow)) debugShadow = 0f;

                var ground = runner.GetComponent<MeshRenderer>().sharedMaterial;
                if (ground.HasProperty("_DebugShadow")) ground.SetFloat("_DebugShadow", debugShadow);
                if (debugShadow > 0f)
                    Debug.Log($"[Arna] Ground material {ground.name} drawing debug mode {debugShadow}.");
            }

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var previous = RenderTexture.active;

            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllBytes(output, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);

                var run = runner.Run;
                Debug.Log($"[Arna] Captured {chapter}-{level} after {steps} steps: " +
                          $"{run.ElapsedSeconds:F1}s, {run.Caravan.Progress:P0} along, " +
                          $"{run.Detection.RevealedCount} revealed, {run.Economy.Silver} silver -> {output}");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Renders the cast standing together:
        /// -executeMethod Arna.Editor.ArnaSetup.CaptureCharacters -arnaOutput &lt;path&gt;
        ///
        /// The play capture answers whether a level reads. It cannot answer whether a
        /// knight is holding his sword or wearing it through his forearm, because at
        /// the distance the game watches from he is forty pixels tall. This stands
        /// everybody up close enough to see, through the same spawn path a level uses
        /// — same height fitting, same animator, same hand bone — so a fault visible
        /// here is a fault the player would eventually meet.
        /// </summary>
        public static void CaptureCharacters()
        {
            string output = ArgValue("-arnaOutput") ?? "Logs/characters.png";

            // Wide and short by default. Seven figures side by side span twelve metres
            // and stand under two, so a conventional frame spends most of itself on
            // empty sky and shrinks the thing being looked at to fit.
            int width = int.TryParse(ArgValue("-arnaWidth"), out var w) ? w : 1800;
            int height = int.TryParse(ArgValue("-arnaHeight"), out var h) ? h : 700;

            // Same reason as the play capture: a shot taken while variants are still
            // compiling shows a shader that is not the one under test.
            ShaderUtil.allowAsyncCompilation = false;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ApplyOutdoorLighting();

            // No fog. It is tuned for a landscape three hundred metres deep and this
            // scene is twelve — all it would do here is grey the cast.
            RenderSettings.fog = false;

            var lightGo = new GameObject("Directional Light");

            // The play view's sun, unchanged. A line-up lit for its own convenience
            // would flatter models that look worse in the game.
            lightGo.transform.rotation = Quaternion.Euler(38f, -52f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.15f;

            // The game's own ground, not a stand-in. Its shader takes its colour from
            // vertex colours the terrain generator writes, so the plane is built by
            // hand with those colours on it rather than taken from Unity's primitives,
            // which carry none — a stock Plane comes out white under this material.
            // Worth the dozen lines: a cast lit and shaded by a different material
            // than the game uses is a picture of some other game.
            var mesh = new Mesh { name = "CastGround" };
            const float extent = 40f;
            var grass = TerrainPalette.OfGround(TerrainType.Plains);

            mesh.vertices = new[]
            {
                new Vector3(-extent, 0f, -extent), new Vector3(-extent, 0f, extent),
                new Vector3(extent, 0f, extent), new Vector3(extent, 0f, -extent)
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            // UVs in metres, not in 0..1. The ground shader divides them by a tiling
            // figure expressed as metres per repeat, so a quad with unit UVs would
            // stretch one repeat of the forest floor across the whole eighty metres.
            mesh.uv = new[]
            {
                new Vector2(-extent, -extent), new Vector2(-extent, extent),
                new Vector2(extent, extent), new Vector2(extent, -extent)
            };
            mesh.colors = new[] { grass, grass, grass, grass };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            var ground = new GameObject("Ground");
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = EnsureGroundMaterial();

            // -arnaNoGround takes the floor away. A model standing too low and a model
            // missing its legs look identical from above the ground and nothing else
            // tells them apart; with the floor gone the question answers itself.
            ground.SetActive(ArgValue("-arnaNoGround") == null);

            var models = LoadModels();
            var visuals = new RunVisuals(new GameObject("Cast").transform) { Library = models };

            const float spacing = 2.8f;
            const float rowDepth = 3.6f;

            // -arnaOnly narrows the line-up to whoever matches, so one model can be
            // looked at close instead of at one seventh of the frame. -arnaBindPose
            // leaves the animators alone, which is how a pose that comes from the clip
            // is told apart from one that comes from the model.
            string only = ArgValue("-arnaOnly");
            var troops = Pick(Troops(models), only);
            var enemies = Pick(Enemies(models), only);

            static (string Name, ActorModel Model, float Height)[] Pick(
                (string Name, ActorModel Model, float Height)[] row, string filter)
            {
                if (string.IsNullOrEmpty(filter)) return row;
                return System.Array.FindAll(row, entry =>
                    entry.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // -arnaBindPose spawns the models with no animator at all, so what shows
            // is the shape the file ships with. It is the difference between a model
            // that is wrong in the box and a model our own setup is bending.
            bool still = ArgValue("-arnaBindPose") != null;

            // Troops in front, enemies behind and staggered into the gaps. Squared up
            // in two straight rows, the wolf — the shortest thing in the game — stood
            // entirely behind a knight.
            PlaceRow(troops, 0f);
            PlaceRow(enemies, rowDepth);

            void PlaceRow((string Name, ActorModel Model, float Height)[] row, float z)
            {
                float start = -(row.Length - 1) * spacing * 0.5f;
                for (int i = 0; i < row.Length; i++)
                {
                    var model = row[i].Model;
                    if (still) model.Animator = null;

                    var actor = visuals.ShowActor(model, row[i].Name, row[i].Height,
                                                  new Vector3(start + i * spacing, 0f, z));

                    // Turned a few degrees off square. Dead-on, an arm hides the weapon
                    // against the body, and the weapon is half of what this picture is
                    // for.
                    actor.rotation = Quaternion.Euler(0f, 195f, 0f);
                }
            }

            // Animators do not run in an editor session, so without this the cast
            // stands in bind pose with its arms out — which looks like a broken rig
            // rather than like nobody having pressed play.
            for (int i = 0; i < 30; i++) visuals.AdvanceAnimators(1f / 30f);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;

            // Framed by arithmetic rather than by eye, because the resolution is an
            // argument: a distance tuned by hand for one shape of picture crops the
            // ends off the row the moment somebody asks for a wider one.
            //
            // Fitted to the front row, not to the group's centre. The rows are three
            // and a half metres apart, and a camera placed to fit the average is too
            // close for the row nearest it — the first attempt put the knight's
            // shoulder outside the frame while leaving room to spare behind him.
            float halfSpan = (Mathf.Max(troops.Length, enemies.Length) - 1) * spacing * 0.5f + 1.9f;
            float halfVertical = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * ((float)width / height));

            // Both dimensions, not just the wide one. Narrowed to a single model by
            // -arnaOnly, a distance chosen from the width alone stands two metres away
            // from a person and cuts them off at the chest.
            const float halfFrameHeight = 1.5f;
            float distance = Mathf.Max(halfSpan / Mathf.Tan(halfHorizontal),
                                       halfFrameHeight / Mathf.Tan(halfVertical));

            // Eye level, near enough. Looked down on from above, everybody reads as a
            // game piece; met at their own height, they read as people the size the
            // game means them to be.
            var focus = new Vector3(0f, 1.05f, rowDepth * 0.35f);
            cameraGo.transform.position = new Vector3(0f, 1.9f, -distance);
            cameraGo.transform.LookAt(focus);

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };
            var previous = RenderTexture.active;

            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllBytes(output, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);

                Debug.Log($"[Arna] Captured {troops.Length + enemies.Length} characters " +
                          $"from {distance:F1} m -> {output}");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Prints the real dimensions and pivot of the scenery models.
        ///
        /// Worth having permanently: a model that arrives lying down, or with its
        /// pivot in the middle rather than at its base, cannot be told apart from a
        /// placement bug by looking at the game. Measuring settles it.
        /// </summary>
        /// <summary>
        /// Measures every model in a folder: -arnaModelDir &lt;path under Assets&gt;.
        ///
        /// A new pack has to be measured before it can be used. Nothing in an FBX says
        /// which way is up or how big the thing is meant to be, and the two failures
        /// compound — a model imported lying down is also normalised by its width,
        /// so it comes out the wrong size as well as the wrong way round, and the
        /// second symptom hides the first.
        /// </summary>
        [MenuItem("Arna/Report Folder Dimensions")]
        public static void ReportFolderDimensions()
            => ReportDimensionsOf(ArgValue("-arnaModelDir") ?? SyntyNatureDir);

        /// <summary>
        /// Measures whatever folder is selected in the Project window.
        ///
        /// The same report as above without the command line, and it exists because the
        /// command line is where this keeps failing. A new pack lands in a folder whose
        /// name nobody has typed yet; clicking it is one step, and getting batchmode to
        /// run against a project the editor may or may not be holding is several — with
        /// the failure mode being a log that stops before it says why.
        ///
        /// Select the pack's folder in the Project window, run this, read the Console.
        /// </summary>
        [MenuItem("Arna/Report Selected Folder Dimensions")]
        public static void ReportSelectedFolderDimensions()
        {
            var folders = new List<string>();

            foreach (var selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                    folders.Add(path);
            }

            if (folders.Count == 0)
            {
                Debug.LogWarning("[Arna] Select one or more folders in the Project window first.");
                return;
            }

            foreach (string folder in folders) ReportDimensionsOf(folder);
        }

        static void ReportDimensionsOf(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[Arna] {folder} is not a folder in this project");
                return;
            }

            // Prefabs as well as models. Synty files its content as prefabs with no FBX
            // beside it, so a search for t:Model alone reports an empty folder for the
            // pack that now supplies the entire landscape.
            var guids = AssetDatabase.FindAssets("t:Model t:Prefab", new[] { folder });
            Debug.Log($"[Arna] {folder}: {guids.Length} models and prefabs");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var instance = UnityEngine.Object.Instantiate(prefab);
                var bounds = ModelScaling.Measure(instance);

                // "tallest axis" is the whole point: if it is not Y, the pack was
                // exported Z-up and everything placed from it will lie on its side.
                string tallest = bounds.size.y >= bounds.size.x && bounds.size.y >= bounds.size.z ? "Y"
                               : bounds.size.z >= bounds.size.x ? "Z" : "X";

                // The long horizontal axis. On anything that walks, that is the body —
                // nose to tail — so it says which way the model faces, and a model that
                // faces the wrong way runs sideways at whatever it is chasing. That
                // reads as a movement bug and is an import one.
                //
                // On anything with its wings out it is the span instead, and the advice
                // inverts. The eagle measures 13.4 across against 7.8 nose to tail, and
                // this line told us to yaw it ninety degrees — which would have had the
                // bird fly sideways for exactly the reason the paragraph above warns
                // about. Anything half again wider than it is long is called out rather
                // than guessed at, because a bounding box cannot tell a wingspan from a
                // very long horse.
                float across = Mathf.Max(bounds.size.x, bounds.size.z);
                float lengthwise = Mathf.Min(bounds.size.x, bounds.size.z);

                // The height test is what keeps a horse out of this. A horse is three
                // times longer than it is wide too, but it is also taller than it is
                // wide; a bird with its wings out is flatter than it is long.
                bool winged = lengthwise > 0.0001f && across / lengthwise > 1.5f
                              && bounds.size.y < lengthwise;

                string along = winged
                    ? (bounds.size.x > bounds.size.z
                        ? "X, but that looks like a wingspan — forward is probably Z (YawOffset 0 or 180)"
                        : "Z, but that looks like a wingspan — forward is probably X (YawOffset 90 or -90)")
                    : bounds.size.x > bounds.size.z ? "X (YawOffset 90 or -90)"
                                                    : "Z (YawOffset 0)";

                Debug.Log($"[Arna]   {Path.GetFileNameWithoutExtension(path)}: " +
                          $"{bounds.size.x:F2} x {bounds.size.y:F2} x {bounds.size.z:F2} " +
                          $"tallest={tallest} longest horizontal={along} " +
                          $"baseY={bounds.min.y:F2}");

                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [MenuItem("Arna/Report Model Dimensions")]
        public static void ReportModelDimensions()
        {
            string[] names =
            {
                // The forest as it is actually drawn, which is Synty now. This list
                // pointed at the RTS trees for so long that it went on measuring them
                // after they stopped being in the game.
                $"{SyntyNatureDir}/Trees/SM_Tree_PolyPine_01.prefab",
                $"{SyntyNatureDir}/Trees/SM_Tree_Round_01.prefab",
                $"{SyntyNatureDir}/Rocks/SM_Rock_01.prefab",
                $"{SyntyNatureDir}/Terrain/SM_Terrain_Mountain_01.prefab",
                $"{SyntyNatureDir}/Plants/SM_Plant_Grass_01.prefab",
                $"{SyntyNatureDir}/Trees/SM_Tree_Log_01.prefab",
                "Assets/Quaternius/Knight/Knight.fbx",

                // The ForestAnimals wolf, not the Quaternius one. That folder held
                // thirteen animals of which this game uses a horse, and the wolf in it
                // was superseded the day the animal pack arrived — leaving a report
                // that measured a model nothing draws.
                "Assets/ForestAnimals/Models/Wolf.fbx",

                $"{SyntyNatureDir}/Props/SM_Prop_Chest_Wood_01.prefab",
                $"{SyntyNatureDir}/Props/SM_Prop_Skull_01.prefab"
            };

            foreach (var path in names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Debug.LogWarning($"[Arna] missing {path}"); continue; }

                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                var bounds = ModelScaling.Measure(instance);
                Debug.Log($"[Arna] {Path.GetFileNameWithoutExtension(path),-22} " +
                          $"size {bounds.size.x,7:F2} x {bounds.size.y,7:F2} x {bounds.size.z,7:F2}   " +
                          $"min.y {bounds.min.y,7:F2}   centre.y {bounds.center.y,7:F2}");

                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// The escort, at the height the game gives them — one entry per kind now.
        ///
        /// It listed the three group models nine kinds used to share, which stopped being
        /// the cast the moment they were split. Built off the enum rather than written
        /// out, so a kind added later cannot be forgotten here.
        /// </summary>
        static (string Name, ActorModel Model, float Height)[] Troops(VisualLibrary models)
        {
            var kinds = (TroopKind[])System.Enum.GetValues(typeof(TroopKind));
            var cast = new (string, ActorModel, float)[kinds.Length + 1];

            for (int i = 0; i < kinds.Length; i++)
                cast[i] = ($"{kinds[i]}", models.For(kinds[i]), VisualLibrary.HeightOf(kinds[i]));

            cast[kinds.Length] = ("Draught", models.Draught, VisualLibrary.DraughtHorseHeight);
            return cast;
        }

        /// <summary>What is waiting on the road, at the height the game gives them.</summary>
        static (string Name, ActorModel Model, float Height)[] Enemies(VisualLibrary models) =>
            new (string, ActorModel, float)[]
            {
                ("Wolf", models.Wolf, VisualLibrary.WolfHeight),
                ("Bandit", models.Bandit, VisualLibrary.EnemyHeight),
                ("BanditArcher", models.BanditArcher, VisualLibrary.EnemyHeight)
            };

        /// <summary>
        /// What the game draws by its width rather than its height.
        ///
        /// One entry so far. It is a separate list because the number means something
        /// different — ten metres across, not ten metres tall — and putting a span in a
        /// column headed Height is how a bird ends up the size of a barn.
        /// </summary>
        static (string Name, ActorModel Model, float Width)[] Wide(VisualLibrary models) =>
            new (string, ActorModel, float)[]
            {
                ("Eagle", models.Eagle, VisualLibrary.EagleSpan)
            };

        /// <summary>
        /// Measures every actor where it lands, before and after it is posed.
        ///
        /// A model is scaled and stood on the ground from its bind-pose bounds,
        /// because that is all there is to measure at the moment it is spawned. If a
        /// rig's bind pose does not describe the shape the game will actually draw,
        /// the model ends up buried or hovering, and from the game's own camera —
        /// forty metres up and pitched over — neither is visible. Printing both sets
        /// of numbers turns "the knight looks wrong" into a figure in metres.
        /// </summary>
        [MenuItem("Arna/Report Actor Fit")]
        public static void ReportActorFit()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var models = LoadModels();
            var visuals = new RunVisuals(new GameObject("Cast").transform) { Library = models };

            var cast = new System.Collections.Generic.List<(string Name, Transform Actor)>();
            foreach (var entry in Troops(models))
                cast.Add((entry.Name, visuals.ShowActor(entry.Model, entry.Name, entry.Height, Vector3.zero)));
            foreach (var entry in Enemies(models))
                cast.Add((entry.Name, visuals.ShowActor(entry.Model, entry.Name, entry.Height, Vector3.zero)));

            // Fitted across rather than up. Reported here and not photographed with the
            // rest: the character line-up is framed for people, and a ten-metre bird in
            // it would push the camera back until the knights were specks.
            foreach (var entry in Wide(models))
                cast.Add((entry.Name, visuals.ShowActor(entry.Model, entry.Name, entry.Width,
                                                        Vector3.zero, byWidth: true)));

            // Posed before measured. An actor spawned in an editor session stands in
            // whatever pose the file was saved in until something drives its animator,
            // and the pose is what the numbers below are about.
            for (int i = 0; i < 30; i++) visuals.AdvanceAnimators(1f / 30f);

            foreach (var (name, actor) in cast)
            {
                var box = ModelScaling.Measure(actor.gameObject);
                Debug.Log($"[Arna] {name,-20} {box.size.x,5:F2} wide x {box.size.y,5:F2} tall   " +
                          $"stands at {box.min.y,6:F2}   " +
                          $"{actor.GetComponentsInChildren<Renderer>().Length} meshes");

                foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
                    Debug.Log($"[Arna]     {name}/{renderer.name,-24} " +
                              $"y {renderer.bounds.min.y,6:F2} .. {renderer.bounds.max.y,5:F2}   " +
                              $"width {renderer.bounds.size.x,5:F2}");
            }
        }

        /// <summary>
        /// Lists the animation clips inside each character and creature model.
        ///
        /// Clip names decide the whole animator setup, and they are not consistent
        /// between packs from different years. Reading them beats assuming them.
        /// </summary>
        /// <summary>
        /// Lists every clip in a model, and the rig type that decides whether they can
        /// be shared.
        ///
        /// Reports on whatever is selected in the Project window, falling back to the
        /// models this project built by hand. A hardcoded list was the whole of it
        /// before, which meant the one report that could have explained why a new pack's
        /// animals stood still could not be pointed at them.
        /// </summary>
        [MenuItem("Arna/Report Animation Clips")]
        public static void ReportAnimationClips()
        {
            var selected = new List<string>();

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                    foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { path }))
                        selected.Add(AssetDatabase.GUIDToAssetPath(guid));
                else if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    selected.Add(path);
            }

            string[] models = selected.Count > 0 ? selected.ToArray() : new[]
            {
                "Assets/Quaternius/Knight/Knight.fbx",
                "Assets/Quaternius/Animals/Horse.fbx",
                "Assets/Quaternius/ModularMen/Adventurer.fbx",
                "Assets/Quaternius/ModularMen/Farmer.fbx",
                "Assets/Quaternius/PiratePack/Characters_Captain_Barbarossa.fbx",
                "Assets/Quaternius/PiratePack/Characters_Henry.fbx"
            };

            foreach (var path in models)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                var clips = new System.Collections.Generic.List<string>();

                foreach (var asset in assets)
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                        clips.Add($"{clip.name} ({clip.length:F2}s)");

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                string rig = importer == null ? "?" : importer.animationType.ToString();

                Debug.Log($"[Arna] {Path.GetFileNameWithoutExtension(path)}  rig={rig}  " +
                          $"clips={clips.Count}: {string.Join(", ", clips)}");
            }
        }

        /// <summary>
        /// Dumps a model's transform hierarchy.
        ///
        /// Needed because attaching anything to a rig means finding a bone by name, and
        /// Generic rigs carry no avatar to ask. Guessing at the naming convention cost
        /// a round trip; reading it costs one run.
        /// </summary>
        [MenuItem("Arna/Report Rig Bones")]
        public static void ReportRigBones()
        {
            string[] models =
            {
                "Assets/Quaternius/Knight/Knight.fbx",
                "Assets/Quaternius/ModularMen/Adventurer.fbx"
            };

            foreach (var path in models)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var instance = UnityEngine.Object.Instantiate(prefab);
                var names = new System.Collections.Generic.List<string>();
                foreach (var bone in instance.GetComponentsInChildren<Transform>()) names.Add(bone.name);

                Debug.Log($"[Arna] {Path.GetFileNameWithoutExtension(path)}: {names.Count} transforms");
                Debug.Log($"[Arna]   {string.Join(" | ", names)}");

                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Dumps what the imported models are actually rendered with.
        ///
        /// The packs ship no material assets and the importer claims to place them
        /// externally, so neither the meta files nor the project folder say what a
        /// tree's surface is. Asking the renderer does.
        /// </summary>

        /// <summary>
        /// Gives a colour to the materials whose texture this project does not have.
        ///
        /// The pirate pack paints each model from one shared atlas image, and that
        /// image is not here — the pack arrived as meshes and materials with nothing
        /// for the materials to point at, and <see cref="RestyleModelMaterials"/>
        /// cannot extract what was never embedded. Left alone the two bandits render
        /// at 0.8 grey, which on a sunlit field is white: two ghosts on the road.
        ///
        /// A flat colour is not the texture and does not pretend to be. It is enough
        /// for a figure the player meets at forty metres, where silhouette and tone
        /// are what carry, and it is the whole fix that is available without the
        /// missing file. Scripted rather than clicked so it survives a reimport, which
        /// resets a material edited by hand.
        /// </summary>
        [MenuItem("Arna/Colour Untextured Materials")]
        public static void ColourUntexturedMaterials()
        {
            // Chosen to separate the two on sight, the same way the troops are
            // separated: the captain in dark leather, the archer in a duller red.
            var colours = new (string Path, Color Colour)[]
            {
                ("Assets/Quaternius/PiratePack/Materials/Characters_Captain_Barbarossa_Atlas.mat",
                 new Color(0.24f, 0.19f, 0.16f)),
                ("Assets/Quaternius/PiratePack/Materials/Characters_Henry_Atlas.mat",
                 new Color(0.42f, 0.26f, 0.22f))
            };

            int painted = 0;
            foreach (var (path, colour) in colours)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { Debug.LogWarning($"[Arna] Material not found: {path}"); continue; }

                if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                {
                    Debug.Log($"[Arna] {Path.GetFileNameWithoutExtension(path)} has its texture back; left alone.");
                    continue;
                }

                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
                material.color = colour;
                EditorUtility.SetDirty(material);
                painted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Arna] Coloured {painted} materials that have no texture to draw.");
        }

        /// <summary>
        /// Prints which texture every material in a folder ended up with:
        /// -arnaModelDir &lt;path under Assets&gt;.
        ///
        /// Two different failures look alike from a distance — a material with no
        /// texture renders white, and one holding its own normal map renders violet —
        /// and both are invisible in a material list. The pairing has to be read.
        /// </summary>
        [MenuItem("Arna/Report Material Textures")]
        public static void ReportMaterialTextures()
        {
            // The pack root rather than its prefab folder: Synty keeps materials in a
            // folder of their own beside the prefabs, so the narrower path finds none.
            string folder = ArgValue("-arnaModelDir") ?? SyntyNaturePack;

            var guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            int missing = 0;

            Debug.Log($"[Arna] {folder}: {guids.Length} materials");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;

                var baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                if (baseMap == null) missing++;

                Debug.Log($"[Arna]   {Path.GetFileNameWithoutExtension(path)} -> " +
                          $"{(baseMap == null ? "NONE" : baseMap.name)}");
            }

            Debug.Log($"[Arna] {missing} of {guids.Length} materials have no base map.");
        }

        [MenuItem("Arna/Report Materials")]
        public static void ReportMaterials()
        {
            string[] models =
            {
                $"{SyntyNatureDir}/Trees/SM_Tree_PolyPine_01.prefab",
                $"{SyntyNatureDir}/Trees/SM_Tree_Round_01.prefab",
                $"{SyntyNatureDir}/Terrain/SM_Terrain_Mountain_01.prefab",
                "Assets/Quaternius/Knight/Knight.fbx"
            };

            foreach (var path in models)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Debug.Log($"[Arna] missing {path}"); continue; }

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                {
                    var mesh = renderer.GetComponent<MeshFilter>();
                    string verts = mesh != null && mesh.sharedMesh != null
                        ? $"{mesh.sharedMesh.vertexCount}v/{mesh.sharedMesh.triangles.Length / 3}t" +
                          $" colors={mesh.sharedMesh.colors.Length}"
                        : "-";

                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) { Debug.Log("[Arna]   (null material)"); continue; }

                        string smooth = material.HasProperty("_Smoothness")
                            ? material.GetFloat("_Smoothness").ToString("0.00")
                            : material.HasProperty("_Glossiness")
                                ? material.GetFloat("_Glossiness").ToString("0.00") + "*"
                                : "n/a";
                        string metal = material.HasProperty("_Metallic")
                            ? material.GetFloat("_Metallic").ToString("0.00") : "n/a";
                        string tex = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null
                            ? material.GetTexture("_BaseMap").name : "none";

                        Debug.Log($"[Arna] {Path.GetFileNameWithoutExtension(path)} :: {material.name} " +
                                  $"shader={material.shader.name} smooth={smooth} metal={metal} " +
                                  $"tex={tex} mesh={verts}");
                    }
                }
            }
        }

        /// <summary>
        /// Re-wires the open scene's model and scenery libraries from code, without
        /// rebuilding the scene: `Arna > Refresh Scene Assets`.
        ///
        /// This exists because of a trap that has now produced three false bug reports
        /// in a row — a wagon that would not grow, lilypads that would not shrink, a
        /// forest that stayed the old pack's. `Decor` and `Models` are *serialized
        /// fields on components in the scene*. Pulling code changes `LoadForestDecor`
        /// and `LoadModels`; it does not change what a saved scene already holds, and
        /// nothing says so. The change looks applied in the diff and absent on screen.
        ///
        /// `Setup Project` and `Set Up Play Scene` fix it by building a scene from
        /// scratch, which also throws away whatever was set by hand in the inspector.
        /// This does the same wiring in place, and prints what it wired so "it did not
        /// change" becomes a thing that can be checked rather than believed.
        /// </summary>
        /// <summary>
        /// Whether the editor is out of play mode, said out loud when it is not.
        ///
        /// **This cost a week.** Opening or saving a scene is illegal in play mode, so a
        /// tool that does it throws at its first line and does nothing at all — while the
        /// work it was meant to do goes on being reported as done, round after round.
        /// Grass deleted from the code stayed on the map; models that had been swapped
        /// stayed swapped out. Every one of those looked like a bug in what the tool
        /// writes and was in fact the tool never running.
        ///
        /// Catching the exception would not help: a scene edited in play mode is thrown
        /// away the moment play stops, and a re-import during play is undone with it. So
        /// the answer is to say which button to press, and stop.
        /// </summary>
        static bool Stopped(string what)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

            Debug.LogWarning($"[Arna] Stop play mode first — {what} changed nothing.\n"
                             + "It writes to the saved scenes and assets, and neither can "
                             + "be written while the game is running: anything it did "
                             + "would be discarded the moment you pressed stop. Press "
                             + "Stop, run it again, then press Play.");
            return false;
        }

        [MenuItem("Arna/Refresh Scene Assets")]
        public static void RefreshSceneAssets()
        {
            // **Not while the game is running**, and this cost a week.
            //
            // Opening a scene is illegal in play mode, so this threw at its first line
            // and did nothing at all — while the changes it was meant to make were being
            // reported as done, round after round. Grass that had been deleted from the
            // code stayed on the map; models that had been swapped stayed swapped out.
            // Every one of those looked like a bug in what the tool writes and was in
            // fact the tool never running.
            //
            // It could not have worked even if the exception were caught: a scene edited
            // in play mode is thrown away the moment play stops. So it says so and stops.
            if (!Stopped("Refresh Scene Assets")) return;

            // Both scenes, not whichever is open.
            //
            // This rewired the active scene, and the active scene is not always the one
            // Play runs. Told four times that the old units were still in the game, with
            // the menu item reporting success each time: it was rewiring `LevelPreview`
            // while `PlayLevel` kept the models it was saved with. A tool that fixes the
            // thing you are looking at and leaves the thing you are running is worse than
            // one that does nothing, because it also tells you it worked.
            string open = EditorSceneManager.GetActiveScene().path;

            foreach (string path in new[] { PlayScenePath, ScenePath })
            {
                if (!System.IO.File.Exists(path)) continue;
                if (path == open) continue;

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                RefreshOpenScene();
            }

            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);

            RefreshOpenScene();
        }

        static void RefreshOpenScene()
        {
            int touched = 0;

            // A white bird is a material with no texture in it, and that is fixable from
            // here rather than by asking for another menu item to be run. Done before
            // anything is wired, so what the components pick up is the mended model.
            if (Untextured(LoadModels().Eagle))
            {
                Debug.Log("[Arna] The eagle's materials carry no textures — wiring them now.");
                WireLooseTextures();
            }

            // Every controller, not just the bird's. What a state plays, and whether the
            // clip under it loops, is decided when the controller is generated — so both
            // live in those assets and not in the scene, and a controller generated
            // before the loop rule existed still has clips that play once and stop. Which
            // is a horse that takes one stride and then slides, and a knight who swings
            // once and holds the follow-through for the rest of the level.
            //
            // Cheap on the second run: EnsureLooping reimports only when it actually has
            // something to switch on.
            AnimatorBuilder.BuildAll();

            // And then the borrowing. The army pack has 52 characters and no animation
            // whatsoever, so its rigs and a Quaternius character's are both re-imported
            // as Humanoid and that character's clips are played on their skeletons. It
            // costs nothing on the second run: a rig already Humanoid and mapped is left
            // alone, and one that cannot be mapped is put back rather than left broken.
            AnimatorBuilder.RigForRetargeting();

            foreach (var runner in UnityEngine.Object.FindObjectsByType<LevelRunner>(
                         FindObjectsSortMode.None))
            {
                runner.Models = LoadModels();
                runner.Decor = LoadForestDecor();
                EditorUtility.SetDirty(runner);
                touched++;

                Debug.Log($"[Arna] {runner.name}: wagons at {VisualLibrary.WagonHeight:0.0} m — "
                          + $"{Describe(runner.Models.WagonSupply)}, "
                          + $"{Describe(runner.Models.WagonWar)}, "
                          + $"{Describe(runner.Models.WagonTreasure)}, drawn by "
                          + $"{Describe(runner.Models.Draught.Prefab)} at "
                          + $"{VisualLibrary.DraughtHorseHeight:0.0} m. "
                          + $"{TroopReport(runner.Models)} "
                          + $"{Rig("draught horse", runner.Models.Draught)}. "
                          + $"Marsh plants: {Names(runner.Decor.MarshPlants)}. "
                          + $"Markers: {Names(runner.Decor.Markers)}. "
                          + $"Lilypads: {Names(runner.Decor.Lilypads)} at "
                          + $"{TerrainDecorator.LilypadWidth:0.0} m across.");
            }

            foreach (var preview in UnityEngine.Object.FindObjectsByType<LevelPreview>(
                         FindObjectsSortMode.None))
            {
                preview.Models = LoadModels();
                preview.Decor = LoadPlanDecor();
                preview.Rebuild();
                EditorUtility.SetDirty(preview);
                touched++;

                Debug.Log($"[Arna] {preview.name}: marsh plants {Names(preview.Decor.MarshPlants)}. "
                          + $"Lilypads: {Names(preview.Decor.Lilypads)}. "
                          + $"Skyline: {Names(preview.Decor.Horizon)}. "
                          + $"{Rig("eagle", preview.Models.Eagle)}");
            }

            if (touched == 0)
            {
                Debug.LogWarning("[Arna] No LevelRunner or LevelPreview in the open scene. "
                                 + "Open PlayLevel or LevelPreview first.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            // The scene by name, because that is the thing this can get wrong. It rewires
            // what is open, and what is open is not always what Play runs — a menu item
            // that says "saved the scene" while a different scene holds the old models is
            // a report that agrees with itself and with nothing else.
            Debug.Log($"[Arna] Refreshed {touched} component(s) in {scene.name} "
                      + $"({scene.path}) and saved it. If Play still shows the old models, "
                      + "Play is running a different scene than this one.");
        }

        /// <summary>
        /// One line per troop kind: which model it got, and whether that model can move.
        ///
        /// Nine kinds shared three models until the army pack arrived, and the pack's
        /// characters are prefabs rather than FBXs — clips live in the source file, not
        /// in a prefab, so `Build Animator Controllers` has nothing to read from these
        /// paths. Whether each one animates therefore depends on what the pack put on its
        /// own prefab, which is a thing to look at rather than assume. `SpawnActor`
        /// leaves a prefab's own animator alone when we have no controller for it, so a
        /// pack that ships its own keeps working.
        /// </summary>
        static string TroopReport(VisualLibrary models)
        {
            if (models == null) return "no models";

            var lines = new List<string>();

            foreach (TroopKind kind in System.Enum.GetValues(typeof(TroopKind)))
            {
                var model = models.For(kind);

                string rig = !model.HasModel ? "NO MODEL"
                    : model.Animator != null ? model.Animator.name
                    : Animated(model.Prefab) ? "the pack's own animator"
                    : "NO ANIMATOR — it will hold its bind pose";

                lines.Add($"{kind} {Describe(model.Prefab)} at "
                          + $"{VisualLibrary.HeightOf(kind):0.00} m [{rig}]");
            }

            return "Troops: " + string.Join("; ", lines) + ".";
        }

        /// <summary>Whether a prefab carries an animator with something in it.</summary>
        static bool Animated(GameObject prefab)
        {
            var animator = prefab.GetComponentInChildren<Animator>(true);
            return animator != null && animator.runtimeAnimatorController != null;
        }

        /// <summary>The cgtrader bird. Note the capital F: the archive shipped it that way.</summary>
        const string EagleModel = "Assets/ThirdParty/Eagle/Eagle_B1.Fbx";

        static string Describe(GameObject prefab) => prefab == null ? "—" : prefab.name;

        /// <summary>
        /// Every way an actor can be wrong and still look like all the others, in one
        /// line: textures, controller, each clip's loop flag, and the avatar.
        ///
        /// White is a material with no texture. A glider is a model with no controller.
        /// A model that moves once and stops is a clip that does not loop. A model that
        /// reports a state, a normalised time and no movement at all is a Generic rig
        /// with no avatar. Each of those has been mistaken for the others, so the report
        /// names all four rather than making anyone guess between them.
        /// </summary>
        static string Rig(string what, ActorModel model)
        {
            if (!model.HasModel) return $"{what}: no model";

            Paint(model, out int slots, out int painted);

            if (model.Animator == null)
                return $"{what}: {painted} of {slots} material slot(s) textured, "
                       + "no animator controller — run Build Animator Controllers";

            var clips = new List<string>();
            foreach (var clip in model.Animator.animationClips)
                clips.Add($"{clip.name} {(clip.isLooping ? "loops" : "PLAYS ONCE")}");

            var animator = model.Prefab.GetComponentInChildren<Animator>();
            string avatar = animator == null ? "NO ANIMATOR ON THE MODEL"
                : animator.avatar == null ? "NO AVATAR — a Generic rig cannot bind without one"
                : animator.avatar.isValid ? animator.avatar.name
                : $"{animator.avatar.name} IS INVALID";

            return $"{what}: {painted} of {slots} material slot(s) textured, "
                   + $"{model.Animator.name} [{string.Join(", ", clips)}], avatar {avatar}";
        }

        static bool Untextured(ActorModel eagle)
        {
            if (!eagle.HasModel) return false;

            Paint(eagle, out int slots, out int painted);
            return slots > 0 && painted == 0;
        }

        /// <summary>
        /// How many of a model's material slots have something in them.
        ///
        /// `_BaseMap` before `mainTexture`: URP's Lit shader does not answer to the
        /// built-in name, and a material read through the wrong property reports itself
        /// as blank whatever is actually wired into it.
        /// </summary>
        static void Paint(ActorModel model, out int slots, out int painted)
        {
            slots = 0;
            painted = 0;

            foreach (var renderer in model.Prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    slots++;
                    if (material == null) continue;

                    var texture = material.HasProperty("_BaseMap")
                        ? material.GetTexture("_BaseMap")
                        : material.mainTexture;

                    if (texture != null) painted++;
                }
        }

        /// <summary>What a set actually holds, which is the thing worth printing.</summary>
        static string Names(PropSet set)
        {
            if (set == null || !set.Any) return "none";

            var names = new List<string>();
            foreach (var model in set.Models)
                if (model != null && !names.Contains(model.name)) names.Add(model.name);

            return string.Join(", ", names);
        }

        /// <summary>
        /// Builds a material out of the loose textures lying beside a model, and gives it
        /// to the model: `Arna > Wire Loose Textures`, `-arnaModelDir &lt;path&gt;`.
        ///
        /// <see cref="RestyleModelMaterials"/> cannot do this and it is worth being clear
        /// why. That one <b>extracts</b> textures that are embedded inside an FBX. The
        /// eagle's are not embedded: cgtrader ships the model in one archive and five
        /// PNGs in another, and what arrives in Unity is a mesh whose material slots
        /// point at nothing. A slot pointing at nothing renders pure white, which is
        /// exactly the failure the comment in RestyleModelMaterials warns about — the
        /// same symptom reached from the opposite direction.
        ///
        /// Alpha clipping is switched on when an opacity map is found. Feathers, leaves
        /// and hair are cut out of flat geometry, and without clipping a bird is drawn
        /// with rectangular wings.
        /// </summary>
        [MenuItem("Arna/Wire Loose Textures")]
        public static void WireLooseTextures()
        {
            string folder = ArgValue("-arnaModelDir") ?? "Assets/ThirdParty/Eagle";

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[Arna] {folder} is not a folder in this project.");
                return;
            }

            var textures = new System.Collections.Generic.List<Texture2D>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(
                    AssetDatabase.GUIDToAssetPath(guid)));

            if (textures.Count == 0)
            {
                Debug.LogWarning($"[Arna] No textures in {folder}. Nothing to wire.");
                return;
            }

            // Matched by what the file is called, because that is all there is to go on
            // and every pack names them the same way. Diffuse first and by exclusion:
            // "Eagle_B1_diffuseOriginal" contains neither "normal" nor "height", and a
            // rule looking for the word "diffuse" alone misses half the packs out there.
            var albedo = Pick(textures, "diffuse", "albedo", "basecolor", "base_color", "_d");
            var normal = Pick(textures, "normal", "_n", "nrm");
            var opacity = Pick(textures, "opacity", "alpha", "_a");

            if (albedo == null)
            {
                Debug.LogWarning($"[Arna] No albedo texture found in {folder}. "
                                 + $"Saw: {string.Join(", ", textures.ConvertAll(t => t.name))}");
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");

            var material = new Material(shader) { name = Path.GetFileName(folder) };
            material.SetTexture("_BaseMap", albedo);
            material.SetFloat("_Smoothness", 0.05f);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            if (opacity != null)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = 2450;
            }

            string materialPath = $"{folder}/{material.name}.mat";
            AssetDatabase.CreateAsset(material, materialPath);

            // Remapped on the importer rather than assigned to the instance, so it
            // survives a reimport — which a material dragged onto a prefab does not.
            int wired = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.External;

                foreach (var slot in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(slot is Material old)) continue;

                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(old), material);
                    wired++;
                }

                importer.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Arna] {material.name}: albedo={albedo.name} "
                      + $"normal={normal?.name ?? "—"} opacity={opacity?.name ?? "—"} "
                      + $"→ {wired} material slot(s) remapped. Alpha clip {(opacity != null ? "on" : "off")}.");
        }

        static Texture2D Pick(System.Collections.Generic.List<Texture2D> textures,
                              params string[] wanted)
        {
            foreach (string want in wanted)
                foreach (var texture in textures)
                    if (texture.name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                        return texture;

            return null;
        }

        /// <summary>
        /// Extracts the packs' embedded materials into asset files and takes the gloss
        /// off them.
        ///
        /// Every model arrives at smoothness 0.5 — trees, rock, dirt alike — which puts
        /// a broad specular sheen down the side of a pine and is the whole reason the
        /// forest read as plastic. Bark is not half-glossy. Nothing outdoors is.
        ///
        /// The materials have to be extracted before they can be changed: they ship
        /// inside the FBX as sub-assets, where they are read-only. Extracting also
        /// leaves us a real palette to tune per biome later, which is the harder
        /// reason to do it this way rather than overriding at runtime.
        /// </summary>
        [MenuItem("Arna/Restyle Model Materials")]
        public static void RestyleModelMaterials()
        {
            const float outdoorSmoothness = 0.04f;

            var modelPaths = new System.Collections.Generic.List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Quaternius" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter) modelPaths.Add(path);
            }

            // One folder per pack, one file per model-and-slot. Sharing a folder across
            // packs would collide on the names they all use — "Wood", "Green", "Stone" —
            // and quietly repaint one pack with another's palette.
            foreach (var folder in MaterialFolders(modelPaths))
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), "Materials");
            }

            int extracted = 0, adjusted = 0, textured = 0;

            // Textures first, and outside the batch. A material extracted from a model
            // whose textures are still embedded points at nothing once it is a file of
            // its own, and the model renders pure white — which is exactly what
            // happened to the whole stylized nature pack the first time this ran. The
            // flat-coloured RTS models survived only because they had no textures to
            // lose.
            foreach (var modelPath in modelPaths)
            {
                if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) continue;

                string folder = MaterialFolderFor(modelPath) + "/Textures";
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(MaterialFolderFor(modelPath), "Textures");

                if (importer.ExtractTextures(folder)) textured++;
            }

            AssetDatabase.Refresh();

            // Repair models whose remap points at a material file that is gone. An
            // importer keeps pointing at an extracted material by GUID, so deleting the
            // file leaves the model with a reference to nothing and Unity draws it in
            // magenta. Clearing the remap brings the embedded material back, which is
            // what the extraction below needs to find.
            int repaired = 0, linked = 0;
            foreach (var modelPath in modelPaths)
            {
                if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) continue;

                var broken = new System.Collections.Generic.List<AssetImporter.SourceAssetIdentifier>();
                foreach (var entry in importer.GetExternalObjectMap())
                    if (entry.Value == null) broken.Add(entry.Key);

                if (broken.Count == 0) continue;

                foreach (var key in broken) importer.RemoveRemap(key);
                importer.SaveAndReimport();
                repaired++;
            }

            if (repaired > 0) Debug.Log($"[Arna] Cleared broken material remaps on {repaired} models.");

            int remapped = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var modelPath in modelPaths)
                {
                    string folder = MaterialFolderFor(modelPath);
                    string model = Path.GetFileNameWithoutExtension(modelPath);
                    var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;

                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                    {
                        if (!(sub is Material embedded)) continue;

                        string target = $"{folder}/{model}_{embedded.name}.mat";
                        var existing = AssetDatabase.LoadAssetAtPath<Material>(target);

                        // Point the model at the file we already have instead of
                        // skipping. Skipping was the bug: the material existed, the
                        // model was never told, and it kept rendering magenta.
                        if (existing != null)
                        {
                            if (importer == null) continue;

                            importer.AddRemap(
                                new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name),
                                existing);
                            remapped++;
                            continue;
                        }

                        string error = AssetDatabase.ExtractAsset(embedded, target);
                        if (string.IsNullOrEmpty(error)) extracted++;
                        else Debug.LogWarning($"[Arna] {model}/{embedded.name}: {error}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // The extraction only rewrites the importers' material references once the
            // batch is closed. Reimporting inside it silently undoes the work.
            foreach (var modelPath in modelPaths)
                AssetDatabase.WriteImportSettingsIfDirty(modelPath);

            AssetDatabase.Refresh();

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Quaternius" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null) continue;

                if (LinkBaseMap(material, AssetDatabase.GUIDToAssetPath(guid))) linked++;

                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", outdoorSmoothness);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", outdoorSmoothness);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

                // Ground cover arrives by the thousand — several grass tufts per tile
                // across a 4096-tile map. Without instancing that is a draw call each,
                // and the phone this is meant to run on has a budget of about 150.
                material.enableInstancing = true;

                EditorUtility.SetDirty(material);
                adjusted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Arna] Restyled materials: {textured} models had textures extracted, " +
                      $"{extracted} materials extracted, {remapped} re-linked, " +
                      $"{linked} given a base map, {adjusted} set to smoothness {outdoorSmoothness}.");
        }

        /// <summary>
        /// Prints every setting a shadow has to pass through.
        ///
        /// A missing shadow can come from the pipeline asset, the light, the caster or
        /// the receiving shader, and all four failures look identical in the picture:
        /// no shadow. Printing the chain says which link is open.
        /// </summary>
        static void ReportShadowState(Material ground)
        {
            if (ground != null)
            {
                string values = "";
                foreach (var name in new[] { "_ShadowStrength", "_AmbientBoost", "_DebugShadow" })
                    values += ground.HasProperty(name) ? $"{name}={ground.GetFloat(name):0.00} " : $"{name}=absent ";

                Debug.Log($"[Arna] ground material: {ground.name} shader={ground.shader.name} {values}");
            }

            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            var sun = UnityEngine.Object.FindFirstObjectByType<Light>();

            int casters = 0, total = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                total++;
                if (renderer.shadowCastingMode != ShadowCastingMode.Off) casters++;
            }

            Debug.Log($"[Arna] shadow chain: pipeline={(pipeline == null ? "none" : pipeline.name)} " +
                      $"mainLightShadows={pipeline?.supportsMainLightShadows} " +
                      $"distance={pipeline?.shadowDistance} cascades={pipeline?.shadowCascadeCount} " +
                      $"soft={pipeline?.supportsSoftShadows} | " +
                      $"sun={sun?.type} shadows={sun?.shadows} strength={sun?.shadowStrength} | " +
                      $"casters={casters}/{total} renderers");
        }

        /// <summary>
        /// Gives an extracted material its texture back.
        ///
        /// Extracting textures out of an FBX does not tell the extracted materials
        /// where they went: the importer records material remaps and nothing else, and
        /// the meta file confirms it — externalObjects lists Materials only. The result
        /// is a material with a null base map, which renders pure white and looks for
        /// all the world like a model with no texture at all.
        ///
        /// The link is recoverable because the words survive, even though the spelling
        /// does not. Matching on a shared tail was not enough: the material named
        /// "Leaves_NormalTree" belongs to the texture "Leaves_NormalTree_C", and
        /// "Leaves_Pine" belongs to "Leaf_Pine_C" — singular where the material is
        /// plural. Every leaf in the pack failed on one of those two, which is why the
        /// trunks came out brown and the canopies stayed white.
        ///
        /// So the words are compared rather than the string. A texture matches when
        /// every word in its name appears in the material's, and the texture with the
        /// most words wins — otherwise the bare "Leaves" would claim a pine.
        /// </summary>
        static bool LinkBaseMap(Material material, string materialPath)
        {
            if (!material.HasProperty("_BaseMap")) return false;
            if (material.GetTexture("_BaseMap") != null) return false;

            string folder = Path.GetDirectoryName(materialPath).Replace('\\', '/') + "/Textures";
            if (!AssetDatabase.IsValidFolder(folder)) return false;

            var wanted = Words(Path.GetFileNameWithoutExtension(materialPath));
            Texture2D best = null;
            int bestScore = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);

                // A normal map holds directions, not colour. Used as albedo it renders
                // the flat violet that a normal map looks like when you look at it.
                if (name.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase)) continue;

                var offered = Words(name);
                if (offered.Count == 0 || offered.Count <= bestScore) continue;

                bool complete = true;
                foreach (var word in offered)
                    if (!wanted.Contains(word)) { complete = false; break; }

                if (!complete) continue;

                best = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                bestScore = offered.Count;
            }

            if (best == null) return false;

            material.SetTexture("_BaseMap", best);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", best);

            // A textured material tinted by a colour from the untextured original comes
            // out muddy; white lets the texture speak.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{best.name}_Normal.png");
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            return true;
        }

        /// <summary>
        /// Splits an asset name into comparable words.
        ///
        /// "_C" marks a colour map and says nothing about which model it belongs to, so
        /// it is dropped; "Leaf" and "Leaves" are the same word as far as this pack is
        /// concerned, and the difference between them is the entire reason the pines
        /// went untextured.
        /// </summary>
        static System.Collections.Generic.HashSet<string> Words(string name)
        {
            var words = new System.Collections.Generic.HashSet<string>();

            foreach (var raw in name.Split('_'))
            {
                string word = raw.ToLowerInvariant();
                if (word.Length == 0) continue;
                if (word == "c" || word == "diffuse" || word == "basecolor") continue;
                if (word == "leaf") word = "leaves";

                words.Add(word);
            }

            return words;
        }

        static string MaterialFolderFor(string modelPath) =>
            Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/Materials";

        static System.Collections.Generic.IEnumerable<string> MaterialFolders(
            System.Collections.Generic.IEnumerable<string> modelPaths)
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var path in modelPaths)
            {
                string folder = MaterialFolderFor(path);
                if (seen.Add(folder)) yield return folder;
            }
        }

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
