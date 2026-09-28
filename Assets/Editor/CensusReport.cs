using System.Collections.Generic;
using TheVeil.App;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// What every country actually puts on the ground: `The Veil &gt; Census`.
    ///
    /// <b>Two things went missing in one afternoon and neither left a mark.</b> The market
    /// square's well asked for a tile that is the third lane of the high street, and the
    /// town's monument asked for one two rows further into the same road. Both wanted
    /// buildable ground, a road is not buildable, and so neither condition could ever be
    /// met: no well, no monument, no error, nothing in any picture to notice. A thing that
    /// is absent leaves no trace. Both were found by counting what was standing and
    /// reading a nought.
    ///
    /// So this counts, for every level of every built chapter. Each country's decor is a
    /// few dozen PropSets, somebody loaded models into each of them on purpose, and every
    /// one of those models is meant to end up somewhere. A set with models in it and no
    /// instances anywhere in its chapter is the same fault in a different place: work
    /// somebody did that the game never shows.
    ///
    /// It is deliberately a blunt question. It cannot tell a set that is unreachable from
    /// one that is merely rare, and it says so by printing the rare ones too. What it can
    /// do is turn "is anything missing?" - which nobody can answer by looking - into a
    /// list of noughts, which anybody can.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.CensusReport.Run
    /// </summary>
    public static class CensusReport
    {
        /// <summary>Sets that are meant to be empty on most countries, so a nought is no news.</summary>
        static readonly HashSet<string> Quiet = new HashSet<string>
        {
            // Only the coast has a sea to put these on, and only its last level a harbour.
            "Jetty", "Ship", "Boats",

            // Only a town has streets to furnish and a plan to build from.
            "Street", "Signs", "Paving", "Monuments",

            // Only a country with rock passes stands faces on them.
            "Cliffs", "Backdrop",
        };

        [MenuItem("The Veil/Census")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Census] no LevelRunner in the scene"); return; }

            var chapters = new List<int>();
            for (int c = 1; c <= DifficultyCurve.BuiltChapters; c++) chapters.Add(c);
            chapters.Add(Biomes.LastChapter);

            int empty = 0, thin = 0;

            foreach (int chapter in chapters)
            {
                var biome = Biomes.Of(chapter);
                var look = runner.LookFor(biome);
                var decor = look != null ? look.Decor : runner.Decor;
                if (decor == null) continue;

                // Which models belong to which set, and how many of each set were built.
                var owners = new Dictionary<string, List<string>>();
                var sets = new List<string>();

                foreach (var field in typeof(BiomeDecor).GetFields())
                {
                    if (field.FieldType != typeof(PropSet)) continue;

                    var models = (PropSet)field.GetValue(decor);
                    if (models == null || !models.Any) continue;

                    sets.Add(field.Name);

                    foreach (var model in models.Models)
                    {
                        if (model == null) continue;

                        if (!owners.TryGetValue(model.name, out var which))
                            owners[model.name] = which = new List<string>();

                        if (!which.Contains(field.Name)) which.Add(field.Name);
                    }
                }

                var standing = new Dictionary<string, int>();
                foreach (string set in sets) standing[set] = 0;

                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out _);

                    // <b>Every renderer, not every mesh renderer.</b> The second version of
                    // this asked for MeshRenderer and reported that four countries have no
                    // wildlife and that not one waterfall in the game has spray at its
                    // foot. Both are drawn with particles, so neither has a mesh renderer
                    // anywhere in it, and both were being placed exactly as written.
                    //
                    // <b>And up to the prop, not the piece it is made of.</b> A rock is one
                    // renderer named after its prefab and a tree is a trunk and a crown and
                    // two cards, none of which is - so the first version reported that the
                    // farmland has no trees in it and the plains no bushes. The ancestors
                    // are walked until one of them is a model some set owns.
                    foreach (var piece in root.GetComponentsInChildren<Renderer>(true))
                    {
                        for (var up = piece.transform; up != null; up = up.parent)
                        {
                            if (!owners.TryGetValue(Model(up.name), out var which)) continue;

                            foreach (string set in which) standing[set]++;
                            break;
                        }
                    }

                    Object.DestroyImmediate(root);
                }

                var never = new List<string>();
                var rare = new List<string>();

                foreach (string set in sets)
                {
                    if (standing[set] == 0) never.Add(set);
                    else if (standing[set] <= Campaign.LevelsPerChapter) rare.Add(set + " x" + standing[set]);
                }

                foreach (string set in never)
                {
                    if (Quiet.Contains(set)) continue;

                    empty++;
                    Debug.Log($"[Census] {chapter} ({biome}): {set} has models loaded and nothing "
                              + "standing on any of its ten levels.");
                }

                thin += rare.Count;

                Debug.Log($"[Census] {chapter} ({biome}): {sets.Count} set(s) filled, "
                          + $"{never.Count} with nothing built"
                          + (never.Count > 0 ? " (" + string.Join(", ", never) + ")" : "")
                          + (rare.Count > 0 ? ", thin: " + string.Join(", ", rare) : "") + ".");
            }

            Debug.Log($"[Census] {empty} set(s) loaded and never used, {thin} used fewer than "
                      + "once a level.");
        }

        /// <summary>The prefab name behind an instance, without Unity's clone suffix.</summary>
        static string Model(string name)
        {
            int clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
            return clone < 0 ? name : name.Substring(0, clone);
        }
    }
}
