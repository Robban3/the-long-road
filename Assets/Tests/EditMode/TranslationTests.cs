using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TheVeil.UI;
using NUnit.Framework;
using UnityEngine;

namespace TheVeil.Tests
{
    /// <summary>
    /// Holds the Swedish table to the source.
    ///
    /// The interface is written in English and translated by lookup (see Loc), so a string
    /// with no Swedish entry does not break anything — it simply stays English on a Swedish
    /// phone. That is the right failure for a player and the wrong one for a build: nobody
    /// would notice until somebody Swedish did. So every call is read out of the scripts and
    /// checked here instead.
    /// </summary>
    public class TranslationTests
    {
        static readonly Regex Call = new Regex(@"Loc\.(?:T|F)\(\s*""((?:[^""\\]|\\.)*)""");
        static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[,:][^}]*)?\}");

        static List<string> KeysInSource()
        {
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            var keys = new List<string>();

            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
                foreach (Match match in Call.Matches(File.ReadAllText(file)))
                    keys.Add(Regex.Unescape(match.Groups[1].Value));

            return keys;
        }

        static string[] Slots(string text)
            => Placeholder.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value)
                          .Distinct().OrderBy(s => s).ToArray();

        [Test]
        public void EveryStringOnScreenHasASwedishTranslation()
        {
            var keys = KeysInSource();
            Assert.Greater(keys.Count, 100, "the source scan found almost nothing — is the path right?");

            var missing = keys.Distinct().Where(k => !Loc.Swedish.ContainsKey(k)).ToList();

            Assert.IsEmpty(missing, "untranslated: " + string.Join(" | ", missing));
        }

        [Test]
        public void NoTranslationIsLeftOverFromTextThatIsGone()
        {
            // A table that only grows ends up translating screens that no longer exist,
            // and the next person cannot tell which half is live.
            var used = new HashSet<string>(KeysInSource());
            var stale = Loc.Swedish.Keys.Where(k => !used.Contains(k)).ToList();

            Assert.IsEmpty(stale, "translated but never shown: " + string.Join(" | ", stale));
        }

        [Test]
        public void ATranslationKeepsEveryNumberItWasGiven()
        {
            // "{0} of {1} points left" turned into "{0} poäng kvar" would quietly drop the
            // budget from the screen — or throw, if the translation asked for a {2}.
            foreach (var pair in Loc.Swedish)
                CollectionAssert.AreEqual(Slots(pair.Key), Slots(pair.Value),
                    $"'{pair.Key}' and its translation use different placeholders");
        }

        [Test]
        public void EnglishIsTheKeyAndSwedishIsLookedUp()
        {
            Assert.AreEqual("PLAY", Loc.Translate("PLAY", Language.English));
            Assert.AreEqual("SPELA", Loc.Translate("PLAY", Language.Swedish));

            // Anything without an entry stays readable rather than vanishing.
            Assert.AreEqual("no such line", Loc.Translate("no such line", Language.Swedish));
        }

        [Test]
        public void NumbersAreWrittenTheWayTheLanguageWritesThem()
        {
            Assert.AreEqual("sight 34 m", Loc.Format(Language.English, "sight {0:F0} m", 34f));
            Assert.AreEqual("sikt 34 m", Loc.Format(Language.Swedish, "sight {0:F0} m", 34f));

            Assert.AreEqual("0.5 m", Loc.Format(Language.English, "{0:F1} m", 0.5f));
            Assert.AreEqual("0,5 m", Loc.Format(Language.Swedish, "{0:F1} m", 0.5f));
        }

        [Test]
        public void ASwedishPhoneStartsInSwedishAndEveryOtherInEnglish()
        {
            Assert.AreEqual(Language.Swedish, Loc.FromSystem(SystemLanguage.Swedish));
            Assert.AreEqual(Language.English, Loc.FromSystem(SystemLanguage.English));
            Assert.AreEqual(Language.English, Loc.FromSystem(SystemLanguage.German));
        }
    }
}
