using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// An Android package, and what it weighs: `The Veil &gt; Build for Android`.
    ///
    /// <b>Because nothing in this project has ever been built as a player.</b> There was
    /// no build pipeline in it at all. Everything the game is has been judged from inside
    /// the editor, and two of the seven lines in docs/technical-design.md §1 cannot be
    /// reached from there at any effort - the package size needs a build and the frame
    /// rate needs the build on a phone. The other five were measured by
    /// <see cref="BudgetReport"/> and four of them were over.
    ///
    /// IL2CPP and ARM64, which is what ships. Mono builds in a fraction of the time and
    /// would make this quick, and it would also be the wrong answer: the whole purpose of
    /// putting this on a device is to find out how the simulation and the drawing perform
    /// there, and a Mono build performs differently. A measurement taken on a
    /// configuration nobody ships is a number with no one to tell.
    ///
    /// <b>The first run of this is slow and it is not broken.</b> Switching the active
    /// build target to Android reimports every texture in the project at Android
    /// compression, and this project carries a great many packs. After that IL2CPP
    /// compiles the whole of it to C++ and then to native code. Hours, once. The target
    /// stays switched afterwards, so the second build is a build rather than an import.
    ///
    /// Headless: unity run . -- -buildTarget Android \
    ///                         -executeMethod TheVeil.Editor.PhoneBuild.Android
    ///
    /// Passing -buildTarget on the command line matters: without it the editor opens on
    /// whatever platform it was last on, and the switch then happens in the middle of the
    /// run instead of while it is starting.
    /// </summary>
    public static class PhoneBuild
    {
        /// <summary>Where the package lands. Outside the repository, which ignores /Build.</summary>
        const string Out = "Build/Android";

        /// <summary>What §1 allows the package to weigh.</summary>
        const long WantedMegabytes = 150;

        [MenuItem("The Veil/Build for Android")]
        public static void Android()
        {
            var scenes = new List<string>();

            // The scenes the project ships, in the order it ships them, taken from the
            // build settings rather than named here - a list written in a build script is
            // a second source of truth for which scenes exist, and it goes stale the first
            // time one is added.
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);

            if (scenes.Count == 0)
            {
                Debug.LogError("[Build] no scenes are enabled in the build settings.");
                return;
            }

            var sheet = new StringBuilder();
            sheet.AppendLine($"[Build] {scenes.Count} scenes: {string.Join(", ", scenes)}");

            // A package rather than a bundle. An app bundle is what a store wants and it
            // cannot be installed on a phone by hand, which is the only thing this build
            // is for.
            EditorUserBuildSettings.buildAppBundle = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,
                                               ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            sheet.AppendLine($"[Build] {PlayerSettings.applicationIdentifier}, "
                             + $"IL2CPP, {PlayerSettings.Android.targetArchitectures}, "
                             + $"min SDK {PlayerSettings.Android.minSdkVersion}");

            System.IO.Directory.CreateDirectory(Out);

            string at = $"{Out}/thelongroad.apk";

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = at,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            var done = report.summary;

            sheet.AppendLine($"[Build] {done.result} in {done.totalTime}");

            if (done.result != BuildResult.Succeeded)
            {
                sheet.AppendLine($"[Build] {done.totalErrors} errors, {done.totalWarnings} warnings");
                Debug.LogError(sheet.ToString());
                return;
            }

            // <b>The file, not the report's own total.</b> BuildSummary.totalSize counts
            // everything the build wrote, intermediates included, and for this project it
            // said 2152,6 MB while the package beside it was 386,7 - a line that read as
            // fourteen times over a limit it is two and a half times over. The number that
            // matters is the one a phone has to download, and that is the file.
            long bytes = 0;

            var file = new System.IO.FileInfo(at);
            if (file.Exists) bytes = file.Length;

            float megabytes = bytes / 1048576f;

            sheet.AppendLine($"[Build] {at}");

            if (bytes <= 0)
                sheet.AppendLine("[Build] the package is not where it was asked for, so its "
                                 + "size is unknown - the build said it succeeded");
            else
                sheet.AppendLine($"[Build] {megabytes:0.0} MB against {WantedMegabytes} MB  "
                                 + (megabytes <= WantedMegabytes
                                        ? "within"
                                        : $"OVER by {megabytes / WantedMegabytes:0.0}x")
                                 + $"   (the build's own total, {done.totalSize / 1048576f:0.0} MB, "
                                 + "counts what it wrote and not what ships)");

            Weight(sheet, report);

            sheet.AppendLine("[Build] install it with: adb install -r " + at);
            sheet.AppendLine("[Build] the frame rate reads out in the corner of the screen - "
                             + "frames a second, the worst frame of the last second, and the "
                             + "worst since it started. See UI/FrameCounter.");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// What the package is made of, heaviest kind first.
        ///
        /// <b>So that a package over its line says what to take out of it.</b> A total is
        /// the same sort of answer as "thirty times over on triangles" was before the
        /// frame was split by model: true, and with nowhere to go. Textures, meshes,
        /// audio, and the engine's own code are four different conversations.
        /// </summary>
        static void Weight(StringBuilder sheet, BuildReport report)
        {
            var packed = report.packedAssets;
            if (packed == null || packed.Length == 0) return;

            var kinds = new Dictionary<string, long>();
            long all = 0;

            foreach (var file in packed)
            {
                if (file.contents == null) continue;

                foreach (var entry in file.contents)
                {
                    string kind = entry.type != null ? entry.type.Name : "unknown";

                    kinds.TryGetValue(kind, out long had);
                    kinds[kind] = had + (long)entry.packedSize;
                    all += (long)entry.packedSize;
                }
            }

            if (all <= 0) return;

            var rows = new List<KeyValuePair<string, long>>(kinds);
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));

            sheet.AppendLine($"[Build] {all / 1048576f:0.0} MB of packed assets, by kind:");

            for (int i = 0; i < rows.Count && i < 10; i++)
                sheet.AppendLine($"[Build]   {rows[i].Key,-28} {rows[i].Value / 1048576f,8:0.0} MB  "
                                 + $"{100f * rows[i].Value / all,5:0.0}%");
        }
    }
}
