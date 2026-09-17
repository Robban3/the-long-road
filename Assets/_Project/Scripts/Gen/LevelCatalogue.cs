using System.Collections.Generic;
using TheVeil.Sim;

namespace TheVeil.Gen
{
    /// <summary>
    /// Which attempt each level ships, worked out once and written down.
    ///
    /// <b>The generator now asks a question it cannot afford to ask twice.</b> A candidate
    /// map is accepted only when the escort the difficulty curve assumes can get down one
    /// of its roads, and answering that means driving a caravan the length of the level.
    /// Some levels take a long search — 1-10 wanted thirty-four attempts — so opening a
    /// chapter meant a hundred simulated runs before a single frame was drawn. That is
    /// affordable once on a machine here and not at all on a player's device.
    ///
    /// Everything below the seed is deterministic, though, and attempt numbers are small.
    /// So the search is done once by an editor tool and its answer is three numbers a line:
    /// chapter, level, attempt. Reading it, TerrainGenerator goes straight to the map the
    /// search arrived at and asks nothing.
    ///
    /// <b>The signature is the dangerous part and it is written in the file for that
    /// reason.</b> A recorded attempt is only the right answer while the rules that judged
    /// it are the same rules: change an enemy's strength, a price, a cap or the reference
    /// escort, and attempt thirty-four may quietly stop being winnable while the catalogue
    /// still points at it. That is exactly the class of fault this codebase has spent a day
    /// chasing — two answers to one question, with nothing to say they had drifted apart.
    ///
    /// So the numbers that feed the judgement are folded into a signature, the catalogue
    /// carries it, and a catalogue whose signature no longer matches is ignored rather than
    /// trusted. It is written out in full rather than hashed, so a person can read what it
    /// was built against instead of taking a number on faith.
    /// </summary>
    public static class LevelCatalogue
    {
        /// <summary>Where the table lives, under Resources so a build carries it.</summary>
        public const string Asset = "LevelCatalogue";

        static Dictionary<(int Chapter, int Level), int> _shipped;

        /// <summary>
        /// Why the last table was refused, or null where none has been offered or it was
        /// taken. For the loader to report — this assembly is engine-free and has nothing
        /// to log to.
        /// </summary>
        public static string Refused { get; private set; }

        /// <summary>
        /// The attempt this level ships, or -1 where the catalogue cannot answer — an
        /// unbuilt table, a level that was never walked, or a signature that has moved.
        /// </summary>
        public static int Shipped(int chapter, int level)
        {
            if (_shipped == null) return -1;
            return _shipped.TryGetValue((chapter, level), out int attempt) ? attempt : -1;
        }

        /// <summary>
        /// Takes a table, or refuses it. Engine-free: the text comes from whoever can read
        /// a file — see App.LevelCatalogueLoader.
        /// </summary>
        public static void Load(string text)
        {
            Forget();
            if (string.IsNullOrEmpty(text)) return;

            var table = new Dictionary<(int, int), int>();
            string signature = null;

            foreach (string line in text.Split((char)10))
            {
                string row = line.Trim();
                if (row.Length == 0 || row.StartsWith("#")) continue;

                if (row.StartsWith("signature "))
                {
                    signature = row.Substring("signature ".Length).Trim();
                    continue;
                }

                var parts = row.Split(' ');
                if (parts.Length != 3) continue;

                if (int.TryParse(parts[0], out int chapter)
                    && int.TryParse(parts[1], out int level)
                    && int.TryParse(parts[2], out int attempt))
                    table[(chapter, level)] = attempt;
            }

            // <b>Ignored rather than trusted.</b> A table built against other rules is
            // worse than no table: no table costs a search, and a wrong one ships a level
            // nobody can win with nothing anywhere saying so.
            if (signature != Signature())
            {
                Refused = "the level catalogue was built against other rules; rebuild it "
                          + "with The Veil > Build Level Catalogue";
                return;
            }

            _shipped = table;
        }

        /// <summary>Forgets what was read, for the tool that writes a new one.</summary>
        public static void Forget()
        {
            _shipped = null;
            Refused = null;
        }

        /// <summary>
        /// What the catalogue was built against, in full.
        ///
        /// Every number here is one the winnability judgement depends on. Adding a rule
        /// that judgement reads means adding it here, and forgetting to is how a catalogue
        /// goes quietly stale — so it is spelled out rather than hashed, and a stale one
        /// can be seen rather than deduced.
        /// </summary>
        public static string Signature()
        {
            // <b>Invariant, or the table is refused on a machine with another decimal
            // mark.</b> String interpolation formats with the current culture, so this
            // comes out "spent=0,50" here and "spent=0.50" in England — two signatures for
            // one set of rules, and a catalogue silently thrown away on somebody else.s
            // machine. The same class of fault as everything else this file guards
            // against, arriving through the one door nobody watches.
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            var parts = new List<string>
            {
                $"gold={ReferenceSquad.GoldPerLevel}",
                "spent=" + ReferenceSquad.SpentOnTroops.ToString("0.00", culture),
                $"steps={TroopBoonTable.Steps}",
                $"price={TroopBoonTable.BasePrice}",
                "growth=" + TroopBoonTable.PriceGrowth.ToString("0.000", culture),
                "weapon=" + TroopBoonTable.WeaponCap.ToString("0.00", culture),
                "armour=" + TroopBoonTable.ArmourHealthCap.ToString("0.00", culture),
                "reduce=" + TroopBoonTable.ArmourReductionCap.ToString("0.00", culture),
                $"smithy={RunEconomy.MaxTrackLevel}",
                "terrain=" + Landscape()
            };

            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                var recipe = ChapterRecipe.For(chapter).ForLevel(Campaign.LevelsPerChapter);

                parts.Add($"c{chapter}="
                          + recipe.EnemyStrength.ToString("0.00", culture)
                          + $"/{recipe.EnemyBudget}/{recipe.SquadBudget}"
                          + $"/{recipe.Posts}/{recipe.GoalBudget}");
            }

            return string.Join(" ", parts);
        }

        static string _landscape;

        /// <summary>
        /// A fingerprint of the country the generator builds, so a catalogue cannot
        /// outlive the landscape it was searched through.
        ///
        /// <b>Everything else in the signature is a number somebody types, and this is
        /// the one thing that is not.</b> A recorded attempt is an index into a search,
        /// and the search runs over whatever TerrainGenerator produces: change how a
        /// river is cut or where a road is allowed to begin, and attempt thirty-four is a
        /// different map with the same number on it. No amount of listing constants
        /// catches that, because no constant moved - the code did.
        ///
        /// So one map is actually built and its ground is hashed. Attempt zero of 1-1,
        /// with no winnability check asked for, which is a terrain field and a corridor
        /// search and nothing expensive. Any change to the shape of the world moves this
        /// number, the catalogue is refused rather than trusted, and the worst case is a
        /// rebuild rather than a level nobody can win.
        ///
        /// Worked out once per process. It is the same answer every time it is asked.
        /// </summary>
        static string Landscape()
        {
            if (_landscape != null) return _landscape;

            var map = TerrainGenerator.Generate(ChapterRecipe.For(1).ForLevel(1),
                                                DeterministicRandom.SeedFor(1, 1), null, 0);

            unchecked
            {
                uint hash = 2166136261u;

                for (int i = 0; i < map.Grid.TileCount; i++)
                {
                    hash ^= (byte)map.Grid[i];
                    hash *= 16777619u;
                }

                foreach (int end in new[] { map.StartIndex, map.GoalIndex })
                {
                    hash ^= (uint)end;
                    hash *= 16777619u;
                }

                return _landscape = hash.ToString("x8");
            }
        }

        /// <summary>How many chapters the signature covers. The rest are the same rules.</summary>
        public const int Chapters = 10;
    }
}
