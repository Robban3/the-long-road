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
    /// What every country is painted with: `The Veil &gt; Paint Report`.
    ///
    /// <b>Written because a snowed rock stood in a summer meadow for months.</b> Two of
    /// the nature pack's rock clusters ship wearing the pack's snow material instead of
    /// its stone atlas. It was found in the mountains, repainted there, and left raw in
    /// the forest - which is also the plains, the farmland and everything else that starts
    /// from the forest's decor. What it looked like on a chapter sheet was a model that
    /// had lost its texture, and the only way it was ever found was somebody noticing a
    /// white shape in a photograph and going after it.
    ///
    /// A photograph is a bad instrument for this. A prop wearing the wrong material is
    /// still a prop: it is the right size, it stands on the ground, it goes round the
    /// road, and every other check in this folder passes it. What gives it away is the
    /// name of what it wears - a pack names its alternates after what they are, and a
    /// country wearing another country's season is either a mistake or a decision.
    ///
    /// <b>That one question, and no others.</b> The first version also asked which model
    /// was the odd one out in its own set - which is how the snowed rock would have been
    /// caught if it had had no name - and it asked that a hundred and sixty-nine times in
    /// one run, because a Synty tree wears one material on its trunk and another on its
    /// leaves and is therefore the odd one out of every set it is in. Nothing was found in
    /// any of them. A check that asks a hundred and sixty-nine questions is a check that
    /// gets read once.
    ///
    /// Where a country has a good reason the reason is written down in
    /// <see cref="Allowed"/> rather than tolerated silently - the same rule the smoke test
    /// keeps for the things that hang and the things that are buried on purpose.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.PaintReport.Run
    /// </summary>
    public static class PaintReport
    {
        /// <summary>
        /// Season words in a material's name, and the country each belongs to.
        ///
        /// Crude on purpose. A pack names its alternates after what they are - Snow, Ice,
        /// Swamp, Arid - and a country wearing a word that is not its own is either a
        /// mistake or a decision somebody should have to look at once.
        /// </summary>
        static readonly (string Word, Biome Belongs)[] Seasons =
        {
            ("snow", Biome.Winter),
            ("ice", Biome.Winter),
            ("frost", Biome.Winter),
            ("swamp", Biome.Marsh),
            ("desert", Biome.Desert),
            ("arid", Biome.Desert),
        };

        /// <summary>
        /// Where another country's paint is the right paint, and why.
        ///
        /// Every one of these was looked at in a picture before it was written down. A
        /// report that asks the same four questions every run and is answered the same way
        /// every run is a report nobody reads by the third run.
        /// </summary>
        static readonly (string Set, string Word, string Why)[] Allowed =
        {
            ("Ruins", "arid",
             "bone is bleached in any country, and the dry pack is where the bones are"),

            ("MarshPlants", "swamp",
             "growth standing at a waterline, and every country has a waterline"),

            ("Horizon", "snow",
             "a snow-capped range at the back of the world, which is what a far mountain "
             + "looks like from anywhere"),

            ("GroundCover", "desert",
             "the coast's dune scrub and succulents, taken from the dry pack on purpose - "
             + "see LoadCoastDecor"),

            ("DeadTrees", "desert",
             "bleached deadwood on a headland, taken from the dry pack for the same reason"),

            ("Bushes", "desert",
             "the dry pack's brambles, which are what grows on a dune"),

            ("Rocks", "desert",
             "loose stone above the tideline, bleached, at its own size"),
        };

        [MenuItem("The Veil/Paint Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Paint] no LevelRunner in the scene"); return; }

            int asked = 0, excused = 0;

            foreach (var biome in Biomes.All)
            {
                var look = runner.LookFor(biome);

                // The forest has no look of its own: it is what the runner is dressed as
                // when nothing overrides it, so its decor is the runner's own.
                var decor = look != null ? look.Decor : runner.Decor;
                if (decor == null) continue;

                var sets = new List<(string Name, PropSet Models)>();

                foreach (var field in typeof(BiomeDecor).GetFields())
                    if (field.FieldType == typeof(PropSet))
                        sets.Add((field.Name, (PropSet)field.GetValue(decor)));

                if (decor.Kit != null)
                    foreach (var field in typeof(BuildingKit).GetFields())
                        if (field.FieldType == typeof(PropSet))
                            sets.Add(("Kit." + field.Name, (PropSet)field.GetValue(decor.Kit)));

                foreach (var (name, models) in sets)
                    Look(biome, name, models, ref asked, ref excused);
            }

            Debug.Log($"[Paint] {asked} question(s), and {excused} answered already.");
        }

        /// <summary>One set of one country: who wears what, and who does not fit.</summary>
        static void Look(Biome biome, string set, PropSet models, ref int asked, ref int excused)
        {
            if (models == null || !models.Any) return;

            // Material name -> the models of this set wearing it.
            var worn = new Dictionary<string, List<string>>();
            int counted = 0;

            foreach (var model in models.Models)
            {
                if (model == null) continue;

                counted++;

                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;

                        if (!worn.TryGetValue(material.name, out var wearers))
                            worn[material.name] = wearers = new List<string>();

                        if (!wearers.Contains(model.name)) wearers.Add(model.name);
                    }
            }

            if (counted == 0) return;

            foreach (var pair in worn)
            {
                string season = Season(pair.Key, biome);
                if (season == null) continue;

                if (Excuse(set, pair.Key) != null) { excused++; continue; }

                asked++;

                Debug.Log($"[Paint] {biome}.{set}: {pair.Key} on {pair.Value.Count} of {counted}"
                          + $" - {season}: " + string.Join(", ", pair.Value));
            }
        }

        /// <summary>Why this material does not belong in this country, or null.</summary>
        static string Season(string material, Biome biome)
        {
            string lower = material.ToLowerInvariant();

            foreach (var (word, belongs) in Seasons)
            {
                if (lower.IndexOf(word, System.StringComparison.Ordinal) < 0) continue;

                return belongs == biome ? null : $"{word} belongs to {belongs}";
            }

            return null;
        }

        /// <summary>The written-down reason this one is right, or null.</summary>
        static string Excuse(string set, string material)
        {
            string lower = material.ToLowerInvariant();

            foreach (var (which, word, why) in Allowed)
                if (which == set && lower.IndexOf(word, System.StringComparison.Ordinal) >= 0)
                    return why;

            return null;
        }
    }
}
