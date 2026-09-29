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
        // <b>And they are not named in the per-chapter line either.</b> They were: the
        // fault line was suppressed and the summary still printed "2 with nothing built
        // (Signs, Monuments)" for eight countries in a row, which reads as eight faults
        // however carefully the fault lines are filtered. It was read that way, and a
        // morning went on asking why no country in the game raises a monument. Every
        // country but one does not raise a monument because there is one town in the game
        // - chapter one, level eight, Towns.Chapter and Towns.Level - and chapter one
        // raises one, which this report says two lines above in its own thin list.
        //
        // An instrument that cries wolf is worse than no instrument: it is the only kind
        // that costs time rather than saving it. So the quiet ones are counted apart and
        // said to be quiet.
        static readonly HashSet<string> Quiet = new HashSet<string>
        {
            // Only the coast has a sea to put these on, and only its last level a harbour.
            "Jetty", "Ship", "Boats",

            // Only a town has streets to furnish and a plan to build from, and there is
            // one town in the game. See Towns.Chapter.
            //
            // <b>The wreckage is on this list and it was written for something else.</b>
            // Its own note in the setup calls it what a wrecked cart leaves - a wheel, a
            // crate, three barrels - and the trap that was meant to be marked with it draws
            // from the ruins instead (see Wreck, which asks Bones(decor.Ruins)). The only
            // two places that read decor.Wreckage are the town's streets and its market
            // square. So it builds in chapter one and nowhere else, and the reason it does
            // not show as a nought in seven other countries is that its barrels are in
            // their yard sets as well and this report credits a model to every set that
            // holds it. On the coast, whose yard is fishing gear, it showed the truth.
            "Street", "Signs", "Paving", "Monuments", "Wreckage",

            // Only a country with rock passes stands faces on them.
            "Cliffs", "Backdrop",
        };

        [MenuItem("The Veil/Census")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Census] no LevelRunner in the scene"); return; }

            // Every dressed chapter, from the one place that knows which they are.
            var chapters = DifficultyCurve.Dressed;

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
                var quiet = new List<string>();
                var rare = new List<string>();

                foreach (string set in sets)
                {
                    if (standing[set] == 0) (Quiet.Contains(set) ? quiet : never).Add(set);
                    else if (standing[set] <= Campaign.LevelsPerChapter) rare.Add(set + " x" + standing[set]);
                }

                foreach (string set in never)
                {
                    empty++;
                    Debug.Log($"[Census] {chapter} ({biome}): {set} has models loaded and nothing "
                              + "standing on any of its ten levels.");
                }

                thin += rare.Count;

                Debug.Log($"[Census] {chapter} ({biome}): {sets.Count} set(s) filled, "
                          + $"{never.Count} with nothing built"
                          + (never.Count > 0 ? " (" + string.Join(", ", never) + ")" : "")
                          + (quiet.Count > 0 ? $", {quiet.Count} quiet by design" : "")
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
