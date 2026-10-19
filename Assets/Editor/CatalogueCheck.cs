using TheVeil.Gen;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Whether the shipped catalogue is being used, and whether it is the one on disk:
    /// `The Veil &gt; Catalogue Check`.
    ///
    /// <b>Two questions, and the second cost three runs in one day before it was
    /// asked.</b> A table that no longer matches the rules is refused rather than
    /// trusted, and the only sign of it is every level coming back untuned - that is the
    /// first question and this answered it from the start.
    ///
    /// The second is nastier. Unity imports a changed asset while it is starting, and the
    /// catalogue is read from an InitializeOnLoadMethod, so the first run after the file
    /// changes can load the *previous* text and say nothing. It happened three times in
    /// one day: after a cold reimport, after the table was rebuilt, and after it was
    /// restored from git - and each time the sheet looked like a campaign that had
    /// silently rearranged itself. Ninety-four of a hundred levels judged on other maps,
    /// and the only clue was that the numbers were wrong.
    ///
    /// So this reads the file off the disk itself and holds it against what the game
    /// loaded. They are the same file or they are not, and that is one line rather than
    /// an afternoon.
    /// </summary>
    public static class CatalogueCheck
    {
        [MenuItem("The Veil/Catalogue Check")]
        public static void Run()
        {
            // No Forget() here: nothing reloads the table afterwards, so clearing it
            // first is a check that destroys the thing it is checking. It cost a run.
            Debug.Log($"[Catalogue] refused: {LevelCatalogue.Refused ?? "no"}");
            Debug.Log($"[Catalogue] 1-1 attempt {LevelCatalogue.Shipped(1, 1)}, "
                      + $"factor {LevelCatalogue.Factor(1, 1):0.000}");

            var asset = Resources.Load<TextAsset>(LevelCatalogue.Asset);

            if (asset == null) { Debug.LogWarning("[Catalogue] no asset at all"); return; }

            string onDisk = System.IO.File.ReadAllText(Disk);

            // Line endings are not the question. Git checks the file out with one kind
            // and the builder writes the other, and a table is the same table either way.
            Debug.Log(Same(asset.text, onDisk)
                          ? $"[Catalogue] loaded text matches {Disk}"
                          : "[Catalogue] <b>the loaded table is NOT the file on disk</b> - "
                            + "Unity is holding a stale import. Run this again; the second "
                            + "run after a change reads the new one.");

            Debug.Log($"[Catalogue] first row on disk: {FirstRow(onDisk)}");
        }

        const string Disk = "Assets/_Project/Resources/LevelCatalogue.txt";

        static bool Same(string a, string b)
            => a.Replace("\r", string.Empty) == b.Replace("\r", string.Empty);

        /// <summary>The first row that is not a comment or the signature.</summary>
        static string FirstRow(string table)
        {
            foreach (string line in table.Split('\n'))
            {
                string row = line.Trim();

                if (row.Length == 0 || row[0] == '#' || row.StartsWith("signature")) continue;

                return row;
            }

            return "(none)";
        }
    }
}
