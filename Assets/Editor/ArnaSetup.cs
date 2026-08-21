using System;
using System.IO;
using Arna.App;
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
        const string ScenePath = ScenesDir + "/LevelPreview.unity";
        const string PlayScenePath = ScenesDir + "/PlayLevel.unity";

        [MenuItem("Arna/Set Up Project")]
        public static void SetupProject()
        {
            EnsureFolders();
            var pipeline = EnsureRenderPipeline();
            var material = EnsureMaterial();
            BuildPreviewScene(material);

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
                Melee = Actor("Assets/Quaternius/Knight/Knight.fbx",
                              "Assets/Quaternius/RPGItems/Sword.fbx", 0.95f),
                Ranged = Actor("Assets/Quaternius/ModularMen/Adventurer.fbx",
                               "Assets/Quaternius/RPGItems/Bow_Wooden.fbx", 1.05f),
                Support = Actor("Assets/Quaternius/ModularMen/Farmer.fbx",
                                "Assets/Quaternius/RPGItems/Axe_small.fbx", 0.6f),
                Mounted = Actor("Assets/Quaternius/Animals/Horse.fbx"),

                Wolf = Actor("Assets/Quaternius/Animals/Wolf.fbx"),
                Bandit = Actor("Assets/Quaternius/PiratePack/Characters_Captain_Barbarossa.fbx",
                               "Assets/Quaternius/PiratePack/Weapon_Cutlass.fbx", 0.8f),
                BanditArcher = Actor("Assets/Quaternius/PiratePack/Characters_Henry.fbx",
                                     "Assets/Quaternius/PiratePack/Weapon_Dagger.fbx", 0.45f),

                Wagon = One("Assets/_Project/Models/Wagon.fbx"),
                WagonTreasure = One("Assets/_Project/Models/WagonTreasure.fbx"),
                WagonBody = One($"{QuaterniusDir}/Crate.fbx"),
                WagonCargo = One("Assets/Quaternius/PiratePack/Prop_Barrel.fbx"),

                SilverCache = One("Assets/Quaternius/PiratePack/Prop_Chest_Gold.fbx"),
                TrapMarker = One("Assets/Quaternius/RPGItems/Bone.fbx")
            };
        }

        /// <summary>Pairs a model with the controller generated for it, matched by filename.</summary>
        static ActorModel Actor(string path, string weaponPath = null, float weaponLength = 0f)
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
                WeaponRotation = new Vector3(-90f, 0f, 0f)
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
            // Single models only. The "_Group" variants are several trees side by side,
            // so their bounding box is wide and short — normalising that by height
            // stretches them sideways into horizontal logs across the ground.
            return new BiomeDecor
            {
                Trees = Load("Resource_Tree1", "Resource_Tree2"),
                Pines = Load("Resource_PineTree"),
                Rocks = Load("Rock", "Resource_Rock_1", "Resource_Rock_2", "Resource_Rock_3"),
                Mountains = Load("Mountain_Single", "MountainLarge_Single")
            };
        }

        static GameObject[] Load(params string[] names)
        {
            var found = new System.Collections.Generic.List<GameObject>();
            foreach (var name in names)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusDir}/{name}.fbx");
                if (asset != null) found.Add(asset);
                else Debug.LogWarning($"[Arna] Scenery model not found: {name}.fbx");
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
            existing.shadowDistance = 60f;
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
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.31f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.19f, 0.14f);
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
            const float pitchDegrees = 55f;

            var centre = new Vector3(mapExtent * 0.5f, 0f, mapExtent * 0.5f);
            var rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            var cameraPosition = centre - rotation * Vector3.forward * 400f;

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetPositionAndRotation(cameraPosition, rotation);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);

            // Orthographic for the planning overview. Perspective made the near edge
            // of the map overflow the frame while the far edge shrank away, which is
            // exactly wrong for a view whose job is letting the player compare two
            // routes across the whole map. Orthographic also frames it exactly:
            // a plane of depth D pitched by θ occupies D·sin(θ) vertically.
            camera.orthographic = true;
            camera.orthographicSize =
                mapExtent * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad) * 0.5f * 1.08f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 900f;

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            var terrainGo = new GameObject("LevelPreview");
            terrainGo.AddComponent<MeshFilter>();
            terrainGo.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
            terrainGo.AddComponent<LevelPreview>().Rebuild();

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
        /// Prints the real dimensions and pivot of the scenery models.
        ///
        /// Worth having permanently: a model that arrives lying down, or with its
        /// pivot in the middle rather than at its base, cannot be told apart from a
        /// placement bug by looking at the game. Measuring settles it.
        /// </summary>
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

            int extracted = 0, adjusted = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var modelPath in modelPaths)
                {
                    string folder = MaterialFolderFor(modelPath);
                    string model = Path.GetFileNameWithoutExtension(modelPath);

                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                    {
                        if (!(sub is Material embedded)) continue;

                        string target = $"{folder}/{model}_{embedded.name}.mat";
                        if (AssetDatabase.LoadAssetAtPath<Material>(target) != null) continue;

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

                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", outdoorSmoothness);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", outdoorSmoothness);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

                EditorUtility.SetDirty(material);
                adjusted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Arna] Restyled materials: {extracted} extracted, {adjusted} set to " +
                      $"smoothness {outdoorSmoothness}.");
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
