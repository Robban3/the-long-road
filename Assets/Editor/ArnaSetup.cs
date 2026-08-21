using System;
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

        const string QuaterniusDir = "Assets/Quaternius/UltimateFantasyRTS";

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
                // The knight arrives holding a two-hander, so nothing is fitted to
                // him — but its point hangs past his boots, and a model is scaled by
                // the height of everything in it. Measured with the sword he came out
                // a head shorter than the troops beside him and stood on its tip.
                Melee = Actor("Assets/Quaternius/Knight/Knight.fbx",
                              unsized: new[] { "Sword" }),
                Ranged = Actor("Assets/Quaternius/ModularMen/Adventurer.fbx",
                               "Assets/Quaternius/RPGItems/Bow_Wooden.fbx", 1.05f),
                Support = Actor("Assets/Quaternius/ModularMen/Farmer.fbx",
                                "Assets/Quaternius/RPGItems/Axe_small.fbx", 0.6f),
                Mounted = Actor("Assets/Quaternius/Animals/Horse.fbx"),

                Wolf = Actor("Assets/Quaternius/Animals/Wolf.fbx"),

                // Barbarossa already carries his cutlass in the rig, so nothing is
                // fitted to him — but his file also carries a second man, Ernest, who
                // was standing beside every bandit in the game.
                Bandit = Actor("Assets/Quaternius/PiratePack/Characters_Captain_Barbarossa.fbx",
                               hide: new[] { "Ernest" }),

                // Henry ships holding a lute. An archer holding a lute is a joke the
                // player has to work out, so it goes and a bow takes its place — which
                // also matches how the player's own archers read at a distance.
                BanditArcher = Actor("Assets/Quaternius/PiratePack/Characters_Henry.fbx",
                                     "Assets/Quaternius/RPGItems/Bow_Wooden.fbx", 1.05f,
                                     hide: new[] { "Weapon_Lute" }),

                Wagon = One("Assets/_Project/Models/Wagon.fbx"),
                WagonTreasure = One("Assets/_Project/Models/WagonTreasure.fbx"),
                WagonBody = One($"{QuaterniusDir}/Crate.fbx"),
                WagonCargo = One($"{QuaterniusDir}/Barrel.fbx"),

                // Off the pirate pack and onto the RPG one. Every pirate model shares
                // a single atlas material per asset, and that atlas is not in this
                // project — so the chest holding the level's silver was rendering as a
                // white box. The RPG chest carries its colours in its materials, the
                // way the swords and bows already do, and reads as gold from the air.
                SilverCache = One("Assets/Quaternius/RPGItems/Chest_Ingots.fbx"),
                TrapMarker = One("Assets/Quaternius/RPGItems/Bone.fbx")
            };
        }

        /// <summary>Pairs a model with the controller generated for it, matched by filename.</summary>
        static ActorModel Actor(string path, string weaponPath = null, float weaponLength = 0f,
                                string[] hide = null, string[] unsized = null)
        {
            var prefab = One(path);
            if (prefab == null) return default;

            string controllerPath = $"Assets/_Project/Animation/{Path.GetFileNameWithoutExtension(path)}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
                Debug.LogWarning($"[Arna] No animator for {Path.GetFileName(path)} — run Arna > Build Animator Controllers.");

            return new ActorModel
            {
                Prefab = prefab,
                Animator = controller,
                Weapon = weaponPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath),
                WeaponLength = weaponLength,

                // Laid along the hand rather than sticking out of the back of it. The
                // packs disagree on which axis a blade runs down, so this is a fixed
                // correction found by looking rather than a value from the files.
                WeaponRotation = new Vector3(-90f, 0f, 0f),

                Hide = hide,
                Unsized = unsized
            };
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
                // Textured, from the Stylized Nature MegaKit. These replace the flat
                // untextured RTS trees, whose materials measured tex=none across the
                // board — which is what made the forest read as plastic long before
                // anything about its shape was at fault.
                // No twisted trees here. Their leaf texture is authored deep red — an
                // autumn or blood tree — and scattered through a temperate forest at
                // one in two they read as a bug rather than as a species. They belong
                // to a chapter that wants them.
                Trees = Nature("CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4",
                               "CommonTree_5"),
                Pines = Nature("Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5"),
                DeadTrees = Nature("DeadTree_1", "DeadTree_2", "DeadTree_3", "DeadTree_4", "DeadTree_5"),
                Rocks = Nature("Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3",
                               "Pebble_Round_1", "Pebble_Round_3", "Pebble_Square_1", "Pebble_Square_4"),

                // Grass first and by a wide margin. The pack shares one leaf atlas
                // across its plants — green, blue, orange, purple and pink leaves in a
                // single image, picked by each model's own UVs — so the showier plants
                // come out genuinely violet. A few are a woodland floor; the big
                // variants at equal weight turned the forest into a flowerbed.
                // Plant_1 and Plant_7 are gone. They are the pack's violet rosettes —
                // the leaf atlas it shares between every plant carries green, blue,
                // orange, purple and pink, and those two models point their UVs at the
                // purple — and at any weight they took over the middle distance. A
                // temperate forest floor is green with flowers in it, not the reverse.
                GroundCover = Nature("Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short",
                                     "Grass_Wispy_Tall", "Grass_Common_Short", "Grass_Common_Tall",
                                     "Grass_Wispy_Short", "Grass_Wispy_Tall",
                                     "Clover_1", "Clover_2", "Fern_1", "Fern_1", "Fern_1",
                                     "Bush_Common", "Bush_Common",
                                     "Flower_3_Single", "Flower_4_Single", "Mushroom_Common"),

                // Mountains stay with the RTS pack: the nature kit has rocks but no
                // landforms, and a ridge on the skyline is a different job from a
                // boulder on the ground.
                Mountains = Rts("Mountain_Single", "MountainLarge_Single"),

                // The pack's buildings come in three levels of development. First age,
                // level one: this is a road through the provinces, not a capital.
                Houses = Rts("Houses_FirstAge_1_Level1", "Houses_FirstAge_2_Level1",
                             "Houses_FirstAge_3_Level1", "TowerHouse_FirstAge",
                             "Windmill_FirstAge"),
                Farms = Rts("Farm_FirstAge_Level1_Wheat", "Farm_FirstAge_Level2_Wheat",
                            "Farm_Dirt_Level1"),
                Watchtowers = Rts("WatchTower_FirstAge_Level1", "WatchTower_FirstAge_Level2"),
                Timber = Rts("Logs", "Crate_Stack1", "Crate_Stack2", "Barrel"),

                // What is left where a caravan came to grief. A wall segment was the
                // first attempt and said the wrong thing entirely — a ruin is masonry,
                // and masonry means somebody built here, not that somebody died here.
                // An abandoned cart is unmistakable, and it is the same kind of cart
                // the player is escorting.
                Ruins = Village("Cart")
            };
        }

        const string NatureDir = "Assets/Quaternius/StylizedNature";
        const string VillageDir = "Assets/Quaternius/MedievalVillage";

        /// <summary>Models from the Z-up RTS scenery pack.</summary>
        static PropSet Rts(params string[] names) => new PropSet(true, Load(QuaterniusDir, names));

        /// <summary>Models from the Y-up stylized nature pack.</summary>
        static PropSet Nature(params string[] names) => new PropSet(false, Load(NatureDir, names));

        /// <summary>
        /// Models from the medieval village pack, which is Y-up — measured, not assumed.
        /// It comes from the same author as the Z-up RTS scenery, and House_1 arrives
        /// 2.14 x 3.39 x 2.66 with its height along Y. Guessing from the author would
        /// have laid every building on its side.
        /// </summary>
        static PropSet Village(params string[] names) =>
            new PropSet(false, Load(VillageDir, names));

        static GameObject[] Load(string folder, string[] names)
        {
            var found = new System.Collections.Generic.List<GameObject>();
            foreach (var name in names)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.fbx");
                if (asset != null) found.Add(asset);
                else Debug.LogWarning($"[Arna] Scenery model not found: {folder}/{name}.fbx");
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
            // never washed out, and ends short of the far clip so the map edge is
            // gone before it can be seen.
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 320f;

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
            preview.Decor = LoadForestDecor();

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
        {
            string folder = ArgValue("-arnaModelDir") ?? "Assets/Quaternius/StylizedNature";

            var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            Debug.Log($"[Arna] {folder}: {guids.Length} models");

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

                Debug.Log($"[Arna]   {Path.GetFileNameWithoutExtension(path)}: " +
                          $"{bounds.size.x:F2} x {bounds.size.y:F2} x {bounds.size.z:F2} " +
                          $"tallest={tallest} baseY={bounds.min.y:F2}");

                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [MenuItem("Arna/Report Model Dimensions")]
        public static void ReportModelDimensions()
        {
            string[] names =
            {
                $"{QuaterniusDir}/Resource_Tree1.fbx",
                $"{QuaterniusDir}/Resource_Tree2.fbx",
                $"{QuaterniusDir}/Resource_PineTree.fbx",
                $"{QuaterniusDir}/Rock.fbx",
                $"{QuaterniusDir}/Mountain_Single.fbx",
                $"{QuaterniusDir}/Crate.fbx",
                "Assets/Quaternius/Knight/Knight.fbx",
                "Assets/Quaternius/Animals/Wolf.fbx",
                "Assets/Quaternius/PiratePack/Prop_Barrel.fbx",
                "Assets/Quaternius/PiratePack/Prop_Chest_Gold.fbx",
                "Assets/Quaternius/RPGItems/Bone.fbx"
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

        /// <summary>The escort, at the height the game gives them.</summary>
        static (string Name, ActorModel Model, float Height)[] Troops(VisualLibrary models) =>
            new (string, ActorModel, float)[]
            {
                ("Melee_Knight", models.Melee, VisualLibrary.TroopHeight),
                ("Ranged_Adventurer", models.Ranged, VisualLibrary.TroopHeight),
                ("Support_Farmer", models.Support, VisualLibrary.TroopHeight),
                ("Mounted_Horse", models.Mounted, VisualLibrary.TroopHeight)
            };

        /// <summary>What is waiting on the road, at the height the game gives them.</summary>
        static (string Name, ActorModel Model, float Height)[] Enemies(VisualLibrary models) =>
            new (string, ActorModel, float)[]
            {
                ("Wolf", models.Wolf, VisualLibrary.WolfHeight),
                ("Bandit", models.Bandit, VisualLibrary.EnemyHeight),
                ("BanditArcher", models.BanditArcher, VisualLibrary.EnemyHeight)
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
        [MenuItem("Arna/Report Animation Clips")]
        public static void ReportAnimationClips()
        {
            string[] models =
            {
                "Assets/Quaternius/Knight/Knight.fbx",
                "Assets/Quaternius/Animals/Wolf.fbx",
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
            string folder = ArgValue("-arnaModelDir") ?? "Assets/Quaternius/StylizedNature";

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
                $"{QuaterniusDir}/Resource_PineTree.fbx",
                $"{QuaterniusDir}/Resource_Tree1.fbx",
                $"{QuaterniusDir}/Mountain_Single.fbx",
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
