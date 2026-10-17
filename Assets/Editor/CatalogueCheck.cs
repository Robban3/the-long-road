using TheVeil.Gen;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>Whether the shipped catalogue is being used at all: `The Veil &gt; Catalogue Check`.</summary>
    // A table that no longer matches the rules is refused rather than trusted, and the
    // only sign of it is every level coming back untuned. This says so in one line.
    public static class CatalogueCheck
    {
        [MenuItem("The Veil/Catalogue Check")]
        public static void Run()
        {
            // No Forget() here: nothing reloads the table afterwards, so clearing it
            // first is a check that destroys the thing it is checking. It cost a run.
            int shipped = LevelCatalogue.Shipped(1, 1);
            float factor = LevelCatalogue.Factor(1, 1);

            Debug.Log($"[Catalogue] refused: {LevelCatalogue.Refused ?? "no"}");
            Debug.Log($"[Catalogue] 1-1 attempt {shipped}, factor {factor:0.000}");
            Debug.Log($"[Catalogue] signature now: {LevelCatalogue.Signature()}");
        }
    }
}
