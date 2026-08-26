using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Arna.Editor
{
    /// <summary>
    /// Generates one animator controller per model.
    ///
    /// Every rig in the packs is Generic rather than Humanoid, so clips cannot be
    /// shared between models — a knight's walk will not drive a wolf. That means one
    /// controller each, which is tedious by hand and trivial to generate: the states
    /// are always the same four, only the clips differ.
    ///
    /// Clip names are matched by suffix because the packs disagree on the prefix
    /// (CharacterArmature vs AnimalArmature) and on what an attack is called — Sword,
    /// Sword_Slash, Attack and Attack_Kick all mean the same thing here.
    /// </summary>
    public static class AnimatorBuilder
    {
        const string OutputDir = "Assets/_Project/Animation";

        /// <summary>Clip suffixes tried in order for each state, first match wins.</summary>
        // "Fly" last in both lists, and in both on purpose. A bird in the air has no
        // idle: standing still and travelling are the same kind of thing, so the eagle's
        // flight clips have to answer for both states or the controller has a hole in it
        // where Speed is zero — and a bird frozen mid-air is worse than no bird.
        //
        // Last, so nothing that owns a real idle can lose it to a flight clip.
        //
        // Which flight clip is not settled here. Both lists end in "Fly", so both match
        // the same one, and the eagle has four of them — a soar and a wingbeat among
        // them. See SortOutTheFlying, which measures them instead of reading their names.
        static readonly string[] IdleNames = { "Idle", "Idle_Neutral", "Idle_2", "Eat", "Graze", "Fly" };
        static readonly string[] WalkNames = { "Walk", "Trot", "Run", "Gallop", "Fly" };
        static readonly string[] AttackNames = { "Sword", "Sword_Slash", "Attack", "Attack_Kick", "Bite", "Punch", "Punch_Right" };
        static readonly string[] DeathNames = { "Death", "Die" };

        static readonly string[] Models =
        {
            "Assets/Quaternius/Knight/Knight.fbx",
            "Assets/Quaternius/Animals/Horse.fbx",

            // ForestAnimals. The pack ships its own controllers and they cannot be used:
            // this project drives every animator through Speed, Attack and Dead, and a
            // controller with other parameter names is a controller nothing can steer.
            // The wolf that came out of that sat in its default state and slid along
            // beside the caravan — which looks exactly like being dragged, because it is.
            //
            // So the clips are taken from the pack and a controller built here instead.
            "Assets/ForestAnimals/Models/Wolf.fbx",
            "Assets/ForestAnimals/Models/Fox.fbx",
            "Assets/ForestAnimals/Models/DeerFemale.fbx",
            "Assets/ForestAnimals/Models/DeerMale.fbx",
            "Assets/ForestAnimals/Models/Boar.fbx",

            // Four flight clips and nothing else — no idle, no walk, no attack. See the
            // clip-name lists above for how a controller gets built out of that, and
            // SortOutTheFlying for which of the four ends up under each state.
            "Assets/ThirdParty/Eagle/Eagle_B1.Fbx",

            "Assets/Quaternius/ModularMen/Adventurer.fbx",
            "Assets/Quaternius/ModularMen/Farmer.fbx",
            "Assets/Quaternius/PiratePack/Characters_Captain_Barbarossa.fbx",
            "Assets/Quaternius/PiratePack/Characters_Henry.fbx"
        };

        /// <summary>The medieval army pack, and the controller its characters share.</summary>
        public const string ArmyPack = "Assets/Stylized_Medieval_Army_Pack";
        public const string ArmyCharacters = ArmyPack + "/Prefabs - Characters";

        /// <summary>
        /// Where the army pack's characters get their animation from.
        ///
        /// **The pack ships none.** `Build Army Animator` searched every asset under it
        /// and found not one clip: 22 FBXs of meshes, 52 prefabs assembled from them, and
        /// nothing that moves. So its characters borrow the knight's, which is what
        /// Humanoid retargeting is for — a clip described in terms of a human skeleton
        /// rather than of bone names plays on any human skeleton, including one from
        /// another artist.
        ///
        /// `Rig For Retargeting` is what makes that possible, and has to be run once.
        /// </summary>
        public const string ArmyController = OutputDir + "/Knight.controller";

        /// <summary>The file the borrowed clips come out of.</summary>
        public const string ClipSource = "Assets/Quaternius/Knight/Knight.fbx";

        /// <summary>
        /// Builds the one controller every character in the army pack uses.
        ///
        /// Separate from `Build Animator Controllers` because it answers a different
        /// question. That one walks a list of models this project knows the animation
        /// lives inside; this one goes looking, because with the army pack nobody knows
        /// yet whether there is any animation at all — its 22 FBXs are meshes and its
        /// characters are prefabs assembled from them.
        ///
        /// Whatever it says is worth reading. Clips found and matched means the troops
        /// move; clips found and unmatched means the names need adding to the lists at
        /// the top of this file; nothing found means the pack ships no animation, and
        /// the way out is Humanoid retargeting off a pack that does.
        /// </summary>
        /// <summary>
        /// Makes the army pack's characters and the knight into Humanoid rigs, so one can
        /// play the other's animation: `Arna > Rig For Retargeting`.
        ///
        /// The army pack has 52 characters and no animation whatsoever. Every other pack
        /// here ships a model and its clips in one file, and this project has always read
        /// them straight out of it — bone name to bone name, which only ever works within
        /// one file. Humanoid is Unity's way round that: the importer maps a skeleton onto
        /// a standard human one, a clip is stored as *what a human did* rather than as
        /// what these particular bones did, and it then plays on any other rig that has
        /// been mapped the same way.
        ///
        /// Two halves, and both are needed. The clips must be re-imported as humanoid,
        /// which is the knight's file; and every rig that is to play them must have an
        /// avatar, which is the army pack's meshes.
        ///
        /// **It can fail, and it fails quietly.** Unity maps bones by guessing from their
        /// names and hierarchy, and a rig it cannot read produces an invalid avatar and
        /// no error. So every avatar is checked afterwards and named — an invalid one has
        /// to be fixed by hand in the importer's Configure screen, and knowing which is
        /// most of that work.
        /// </summary>
        [MenuItem("Arna/Rig For Retargeting")]
        public static void RigForRetargeting()
        {
            var rigs = new List<string> { ClipSource };

            // The files the characters are actually made of, found through the prefabs
            // rather than listed: a table of prefab-to-FBX would be wrong the first time
            // the pack is updated.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ArmyCharacters }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;

                foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null) continue;

                    string path = AssetDatabase.GetAssetPath(skin.sharedMesh);
                    if (!string.IsNullOrEmpty(path) && !rigs.Contains(path)) rigs.Add(path);
                }
            }

            int changed = 0;
            var broken = new List<string>();

            foreach (string path in rigs)
            {
                if (Humanoid(path)) changed++;
                if (!Mapped(path)) broken.Add(Path.GetFileNameWithoutExtension(path));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // The clips are humanoid now, so the controller has to be built from them
            // again: the old one holds the generic versions, which retarget onto nothing.
            Build(ClipSource);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Arna] {rigs.Count} rig(s) checked, {changed} re-imported as Humanoid. "
                      + $"The army pack now plays {ArmyController}.");

            if (broken.Count > 0)
                Debug.LogWarning($"[Arna] Unity could not map these onto a human skeleton: "
                                 + $"{string.Join(", ", broken)}. They will hold their bind pose "
                                 + "until the mapping is corrected by hand — select the file, "
                                 + "Rig > Configure, and fix whatever is red.");
        }

        /// <summary>Re-imports a model as a Humanoid rig. Returns whether it had to.</summary>
        static bool Humanoid(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return false;
            if (importer.animationType == ModelImporterAnimationType.Human) return false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            return true;
        }

        /// <summary>Whether the importer managed to build a usable human avatar.</summary>
        static bool Mapped(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Avatar avatar) return avatar.isValid && avatar.isHuman;

            return false;
        }

        [MenuItem("Arna/Build Army Animator")]
        public static void BuildArmyAnimator()
        {
            if (!AssetDatabase.IsValidFolder(ArmyPack))
            {
                Debug.LogWarning($"[Arna] {ArmyPack} is not in this project.");
                return;
            }

            var controller = BuildFromFolder("Army", ArmyPack);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (controller == null)
            {
                Debug.Log($"[Arna] Nothing to build, which is the answer: the pack has no "
                          + $"animation of its own, so its characters borrow the knight's "
                          + $"through {ArmyController}. Run Arna > Rig For Retargeting once, "
                          + "then Refresh Scene Assets.");
                return;
            }

            Debug.LogWarning($"[Arna] The army pack turned out to have clips after all, and they "
                             + $"are in {OutputDir}/Army.controller. Point "
                             + "AnimatorBuilder.ArmyController at it — borrowing the knight's "
                             + "was only ever the answer to it having none.");
        }

        [MenuItem("Arna/Build Animator Controllers")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(OutputDir))
                AssetDatabase.CreateFolder("Assets/_Project", "Animation");

            int built = 0;
            var missed = new List<string>();

            foreach (var model in Models)
            {
                if (Build(model) != null) built++;
                else missed.Add(Path.GetFileNameWithoutExtension(model));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // A count on its own cannot be read. "Built eleven" is the right answer for
            // eleven models and a silent failure for twelve, and the difference only
            // showed up because somebody happened to know what the list was that day.
            // The per-model warnings were there and scrolled past; a total that names
            // its denominator does not need anyone to go looking.
            if (missed.Count == 0)
            {
                Debug.Log($"[Arna] Built {built} of {Models.Length} animator controllers in {OutputDir}.");
                return;
            }

            Debug.LogWarning($"[Arna] Built {built} of {Models.Length} animator controllers in "
                             + $"{OutputDir}. No controller for: {string.Join(", ", missed)}. "
                             + "The warning above each one says why — a missing file reads as "
                             + "\"No clips\", a file whose clips this project cannot name reads "
                             + "as \"No idle clip\".");
        }

        static string Name(AnimationClip clip) => clip == null ? "—" : clip.name;

        /// <summary>
        /// Builds one controller from every clip in a folder, wherever they live.
        ///
        /// <see cref="Build"/> reads clips out of one FBX, which is right for the packs
        /// that ship a model and its animation in the same file. The medieval army pack
        /// does not: its 22 FBXs are meshes, its characters are prefabs assembled from
        /// them, and whatever animation it has is somewhere else in the folder —
        /// possibly `.anim` assets, possibly sub-assets of a rig file this project never
        /// names. Searching by type finds them either way, and finding nothing is itself
        /// the answer to a question that was otherwise going to cost a round trip.
        ///
        /// One controller for the whole pack rather than one per character. They share a
        /// skeleton — the pack builds 52 characters out of 22 meshes — so a clip that
        /// binds to one binds to all of them, and 52 identical controllers would be 52
        /// assets saying the same thing.
        /// </summary>
        public static AnimatorController BuildFromFolder(string name, string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[Arna] {folder} is not a folder in this project.");
                return null;
            }

            var clips = new List<AnimationClip>();
            var seen = new HashSet<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is AnimationClip clip)) continue;
                    if (clip.name.StartsWith("__preview")) continue;
                    if (!seen.Add($"{path}:{clip.name}")) continue;

                    clips.Add(clip);
                }
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning($"[Arna] No animation clips anywhere under {folder}. The pack "
                                 + "ships none, so its characters will hold their bind pose. "
                                 + "The way out is Humanoid retargeting — set both this pack's "
                                 + "rigs and a pack that does have clips to Humanoid, and Unity "
                                 + "will play one on the other.");
                return null;
            }

            var names = new List<string>();
            foreach (var clip in clips) names.Add(clip.name);

            Debug.Log($"[Arna] {clips.Count} clip(s) under {folder}: {string.Join(", ", names)}");

            return Assemble(name, $"{folder} (folder)", clips);
        }

        public static AnimatorController Build(string modelPath)
        {
            var clips = LoadClips(modelPath);
            if (clips.Count == 0)
            {
                Debug.LogWarning($"[Arna] No clips in {modelPath}");
                return null;
            }

            var idle = Match(clips, IdleNames);
            var walk = Match(clips, WalkNames);
            var attack = Match(clips, AttackNames);
            var death = Match(clips, DeathNames);

            SortOutTheFlying(clips, ref idle, ref walk);

            // A reimport destroys the clip objects loaded above, so everything is chosen
            // again from the new ones.
            if (EnsureLooping(modelPath, idle, walk, attack))
            {
                clips = LoadClips(modelPath);
                if (clips.Count == 0) return null;

                idle = Match(clips, IdleNames);
                walk = Match(clips, WalkNames);
                attack = Match(clips, AttackNames);
                death = Match(clips, DeathNames);

                SortOutTheFlying(clips, ref idle, ref walk);
            }

            ReportFlight(clips, modelPath, idle, walk);

            return Assemble(Path.GetFileNameWithoutExtension(modelPath), modelPath, clips);
        }

        /// <summary>
        /// Turns a heap of clips into the four states this project drives, and writes the
        /// controller.
        ///
        /// Split out of <see cref="Build"/> so the same four states can be assembled from
        /// clips found anywhere — one FBX, or a whole pack folder. Which clip fills which
        /// state is the only decision in here, and it is made the same way either way.
        /// </summary>
        static AnimatorController Assemble(string name, string source, List<AnimationClip> clips)
        {
            var idle = Match(clips, IdleNames);
            var walk = Match(clips, WalkNames);
            var attack = Match(clips, AttackNames);
            var death = Match(clips, DeathNames);

            SortOutTheFlying(clips, ref idle, ref walk);

            if (idle == null)
            {
                Debug.LogWarning($"[Arna] No idle clip in {source}. Nothing here is named "
                                 + "anything this project recognises as standing still — see "
                                 + "IdleNames, which is a list to add to rather than a rule.");
                return null;
            }

            string path = $"{OutputDir}/{name}.controller";

            // Said out loud, because a controller built from the wrong clips and one
            // built from the right ones are the same file from the outside, and the
            // difference only shows up as an animal standing still in a running game.
            Debug.Log($"[Arna] {name}: idle={Name(idle)} walk={Name(walk)} "
                      + $"attack={Name(attack)} death={Name(death)}  ({clips.Count} clips)");

            Loop(idle, walk, attack);

            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // Attack is a bool rather than a trigger: fighting here lasts as long as
            // something is in contact, so the swing should loop rather than fire once.
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;

            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            idleState.speed = IsFlight(idle) ? FlightSpeed : 1f;
            machine.defaultState = idleState;

            if (walk != null)
            {
                var walkState = machine.AddState("Walk");
                walkState.motion = walk;
                walkState.speed = IsFlight(walk) ? FlightSpeed : 1f;

                // Threshold sits well below a walking pace so the column does not
                // shuffle between idle and walk while creeping through a marsh.
                Transition(idleState, walkState, AnimatorConditionMode.Greater, 0.15f, "Speed");
                Transition(walkState, idleState, AnimatorConditionMode.Less, 0.15f, "Speed");
            }

            if (attack != null)
            {
                var attackState = machine.AddState("Attack");
                attackState.motion = attack;

                // Entered from anywhere, so a troop caught mid-stride still turns and
                // fights instead of finishing its walk cycle first.
                var toAttack = machine.AddAnyStateTransition(attackState);
                toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                toAttack.hasExitTime = false;
                toAttack.duration = 0.1f;
                toAttack.canTransitionToSelf = false;

                var fromAttack = attackState.AddTransition(idleState);
                fromAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Attack");
                fromAttack.hasExitTime = false;
                fromAttack.duration = 0.15f;
            }

            if (death != null)
            {
                var deathState = machine.AddState("Death");
                deathState.motion = death;

                // From anywhere: dying interrupts whatever was happening.
                var toDeath = machine.AddAnyStateTransition(deathState);
                toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                toDeath.hasExitTime = false;
                toDeath.duration = 0.1f;
                toDeath.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// How fast a flight clip is played back, and only a flight clip.
        ///
        /// 0.45, and this time from the argument rather than from a cautious nudge. A
        /// fifth off was tried first, was not enough, and was not going to be: the beat
        /// is wrong by a factor, not by a margin.
        ///
        /// The factor is the bird's size. She is drawn at a ten-metre wingspan against a
        /// real eagle's two, because at life size she is a speck over a 256 m map (see
        /// <c>VisualLibrary.EagleSpan</c>). Wingbeat frequency falls roughly with the
        /// square root of length for animals of the same shape, so a bird very nearly
        /// five times over should beat about √5 ≈ 2.2 times slower. 1 ÷ 2.2 = 0.45.
        ///
        /// Which is the useful thing about it: the clip is authored for a two-metre bird
        /// and is being watched on a ten-metre one, so it was never going to look right
        /// at any speed chosen by eye — it looks right at the speed its size asks for.
        ///
        /// Nothing that walks is touched. A troop's stride is tied to how fast the
        /// caravan is actually moving, and slowing the clip would put the feet out of
        /// step with the ground.
        /// </summary>
        public const float FlightSpeed = 0.45f;

        static bool IsFlight(AnimationClip clip)
            => clip != null && Contains(Bare(clip.name), "Fly");

        /// <summary>
        /// Switches loop on for clips that are their own assets.
        ///
        /// <see cref="EnsureLooping"/> goes through the model importer, which is right
        /// for a clip that is a sub-asset of an FBX and impossible for one that is not:
        /// a standalone `.anim` has no importer to ask. Its loop flag lives in the clip's
        /// own settings instead.
        ///
        /// Death is left alone here for the same reason it is there — a looping death is
        /// a corpse that keeps getting up to die again.
        /// </summary>
        static void Loop(params AnimationClip[] clips)
        {
            foreach (var clip in clips)
            {
                if (clip == null || clip.isLooping) continue;

                // Sub-assets of a model are the importer's to change, and it has already
                // had its chance by the time this runs.
                string path = AssetDatabase.GetAssetPath(clip);
                if (AssetImporter.GetAtPath(path) is ModelImporter) continue;

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                EditorUtility.SetDirty(clip);
                Debug.Log($"[Arna] loop switched on for {clip.name}, which was set to play once.");
            }
        }

        /// <summary>
        /// Switches loop on for the clips that are meant to run continuously.
        ///
        /// **Unity imports every clip with Loop Time off.** A cycle that does not loop
        /// plays once and holds its last frame, so a wingbeat lasting a second gives one
        /// beat and then a bird gliding for the rest of the level — which is
        /// indistinguishable from an animator that never ran at all, and cost a round
        /// being mistaken for one.
        ///
        /// Idle, walk and attack only. **Death must never loop**: it is entered from
        /// anywhere on a bool that stays true, and a looping death animation is a corpse
        /// that keeps getting up to die again. Attack loops on purpose — fighting here
        /// lasts as long as something is in contact, so the swing should repeat.
        ///
        /// Written through the importer rather than onto the clip, because a clip is a
        /// sub-asset of the model file: anything set on it directly is regenerated away
        /// the next time the file is imported.
        /// </summary>
        static bool EnsureLooping(string modelPath, params AnimationClip[] wanted)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return false;

            var names = new List<string>();
            foreach (var clip in wanted)
                if (clip != null) { names.Add(clip.name); names.Add(Bare(clip.name)); }

            if (names.Count == 0) return false;

            // defaultClipAnimations is what the file itself declares, and is what an
            // untouched model has: reading clipAnimations alone comes back empty and
            // there is nothing to switch anything on for.
            var settings = importer.clipAnimations;
            if (settings == null || settings.Length == 0) settings = importer.defaultClipAnimations;
            if (settings == null || settings.Length == 0) return false;

            var looped = new List<string>();

            foreach (var setting in settings)
            {
                if (setting.loopTime) continue;
                if (!names.Contains(setting.name) && !names.Contains(Bare(setting.name))) continue;

                setting.loopTime = true;
                looped.Add(setting.name);
            }

            if (looped.Count == 0) return false;

            importer.clipAnimations = settings;
            importer.SaveAndReimport();

            Debug.Log($"[Arna] {Path.GetFileNameWithoutExtension(modelPath)}: loop switched on "
                      + $"for {string.Join(", ", looped)} — they were set to play once.");
            return true;
        }

        /// <summary>
        /// Chooses between flight clips, which the name lists cannot do.
        ///
        /// The eagle ships four of them — `Fly_01` at 6 s, `Fly_02` at 5 s, `Fly_03` at
        /// 8.33 s, `Fly_04` at 1 s — and they are emphatically not interchangeable: some
        /// of a bird's flying is beating and some of it is soaring. Both lists end in
        /// "Fly", both matched `Fly_01`, and `Fly_01` turns out to be a glide. So the
        /// bird was textured, moving, animated, and had wings that did not beat, which
        /// is three fixes deep into a fault that was never any of them.
        ///
        /// Length looks like the tell and is not one: a one-second clip is probably a
        /// single wingbeat, but a six-second one may be six of them. What settles it is
        /// how much the skeleton actually moves per second, which is a thing that can be
        /// measured rather than guessed at — see <see cref="Cadence"/>. The busiest clip
        /// drives the travelling state and the calmest one the idle, which is also the
        /// right answer for a bird: hanging on a thermal is what it does when it is not
        /// going anywhere.
        /// </summary>
        static void SortOutTheFlying(List<AnimationClip> clips,
                                     ref AnimationClip idle, ref AnimationClip walk)
        {
            var flying = clips.FindAll(clip => Contains(Bare(clip.name), "Fly"));
            if (flying.Count < 2) return;

            flying.Sort((a, b) => Cadence(a).CompareTo(Cadence(b)));

            var calmest = flying[0];
            var busiest = flying[flying.Count - 1];

            if (flying.Contains(idle)) idle = calmest;
            if (flying.Contains(walk)) walk = busiest;
        }

        /// <summary>
        /// The measurements behind the choice above, said out loud once.
        ///
        /// Separate from the choosing because the choosing may run twice — a clip that
        /// had to be switched to looping is reimported, which destroys every clip object
        /// and means picking again from the new ones. The numbers are worth one line,
        /// not two identical ones.
        /// </summary>
        static void ReportFlight(List<AnimationClip> clips, string modelPath,
                                 AnimationClip idle, AnimationClip walk)
        {
            var flying = clips.FindAll(clip => Contains(Bare(clip.name), "Fly"));
            if (flying.Count < 2) return;

            var measured = new List<string>();
            foreach (var clip in flying)
                measured.Add($"{Bare(clip.name)} {Cadence(clip):0.00}/s"
                             + (clip.isLooping ? "" : " (plays once!)"));

            Debug.Log($"[Arna] {Path.GetFileNameWithoutExtension(modelPath)} flight clips: "
                      + $"{string.Join(", ", measured)}. Travelling on {Name(walk)}, "
                      + $"idling on {Name(idle)}, both at {FlightSpeed:0.00}x.");
        }

        /// <summary>
        /// How much a clip's skeleton turns per second of it.
        ///
        /// Summed off the rotation curves, because that is what a wingbeat is, and with
        /// the root left out: a soaring bird banking across the sky moves its root more
        /// than a flapping one moves it at all, so counting the root would pick exactly
        /// the wrong clip. The absolute value matters not at all — only the ordering
        /// between clips out of the same file, which is why raw quaternion components
        /// are fine to add up.
        /// </summary>
        static float Cadence(AnimationClip clip)
        {
            if (clip == null || clip.length < 0.01f) return 0f;

            float turned = 0f;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (string.IsNullOrEmpty(binding.path)) continue;
                if (binding.propertyName.IndexOf("Rotation", System.StringComparison.Ordinal) < 0
                    && binding.propertyName.IndexOf("Euler", System.StringComparison.Ordinal) < 0)
                    continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) continue;

                for (int i = 1; i < curve.length; i++)
                    turned += Mathf.Abs(curve[i].value - curve[i - 1].value);
            }

            return turned / clip.length;
        }

        static void Transition(AnimatorState from, AnimatorState to,
                               AnimatorConditionMode mode, float threshold, string parameter)
        {
            var transition = from.AddTransition(to);
            transition.AddCondition(mode, threshold, parameter);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
        }

        static List<AnimationClip> LoadClips(string modelPath)
        {
            var clips = new List<AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    clips.Add(clip);
            return clips;
        }

        /// <summary>
        /// Finds a clip whose name ends with one of the wanted suffixes. Exact matches
        /// win over partial ones, so "Idle" is not beaten by "Idle_HitReact_Left".
        /// </summary>
        /// <summary>
        /// Finds the clip for a state, trying the names in order and, for each, three
        /// increasingly forgiving readings of "matches".
        ///
        /// It used to demand an exact name, which the comment at the top of this file
        /// has always described as matching by suffix — and which was true enough while
        /// every pack here happened to call its clip `Idle`. ForestAnimals calls it
        /// `Wolf_Idle`, the build found no idle, and the animals stood in bind pose
        /// while the simulation walked them about.
        ///
        /// Exact first, so a pack that names a clip plainly still wins it over a longer
        /// name that merely contains the word.
        /// </summary>
        static AnimationClip Match(List<AnimationClip> clips, string[] wanted)
        {
            foreach (var name in wanted)
            {
                var clip = Match(clips, name, Exactly) ?? Match(clips, name, EndsWith)
                           ?? Match(clips, name, Contains);
                if (clip != null) return clip;
            }
            return null;
        }

        static AnimationClip Match(List<AnimationClip> clips, string wanted,
                                   System.Func<string, string, bool> how)
        {
            foreach (var clip in clips)
                if (how(Bare(clip.name), wanted)) return clip;
            return null;
        }

        /// <summary>The part after the last bar: exporters prefix clips with the take's name.</summary>
        static string Bare(string name)
        {
            int bar = name.LastIndexOf('|');
            return bar >= 0 ? name.Substring(bar + 1) : name;
        }

        static bool Exactly(string a, string b)
            => string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);

        static bool EndsWith(string a, string b)
            => a.EndsWith(b, System.StringComparison.OrdinalIgnoreCase);

        static bool Contains(string a, string b)
            => a.IndexOf(b, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
