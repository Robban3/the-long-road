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
            var material = EnsureMaterial();

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
            camera.backgroundColor = new Color(0.60f, 0.76f, 0.88f);
            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 900f;

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

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
