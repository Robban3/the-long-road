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
        static readonly string[] IdleNames = { "Idle", "Idle_Neutral", "Idle_2" };
        static readonly string[] WalkNames = { "Walk", "Run", "Gallop" };
        static readonly string[] AttackNames = { "Sword", "Sword_Slash", "Attack", "Attack_Kick", "Punch", "Punch_Right" };
        static readonly string[] DeathNames = { "Death" };

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

            "Assets/Quaternius/ModularMen/Adventurer.fbx",
            "Assets/Quaternius/ModularMen/Farmer.fbx",
            "Assets/Quaternius/PiratePack/Characters_Captain_Barbarossa.fbx",
            "Assets/Quaternius/PiratePack/Characters_Henry.fbx"
        };

        [MenuItem("Arna/Build Animator Controllers")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(OutputDir))
                AssetDatabase.CreateFolder("Assets/_Project", "Animation");

            int built = 0;
            foreach (var model in Models)
                if (Build(model) != null) built++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Arna] Built {built} animator controllers in {OutputDir}.");
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

            if (idle == null)
            {
                Debug.LogWarning($"[Arna] No idle clip in {modelPath}");
                return null;
            }

            string name = Path.GetFileNameWithoutExtension(modelPath);
            string path = $"{OutputDir}/{name}.controller";

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
            machine.defaultState = idleState;

            if (walk != null)
            {
                var walkState = machine.AddState("Walk");
                walkState.motion = walk;

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
        static AnimationClip Match(List<AnimationClip> clips, string[] wanted)
        {
            foreach (var suffix in wanted)
            {
                foreach (var clip in clips)
                {
                    int bar = clip.name.LastIndexOf('|');
                    string bare = bar >= 0 ? clip.name.Substring(bar + 1) : clip.name;
                    if (bare == suffix) return clip;
                }
            }
            return null;
        }
    }
}
