using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TheVeil.UI
{
    /// <summary>The languages the interface is written in. Stored by number: append only.</summary>
    public enum Language : byte
    {
        English = 0,
        Swedish = 1
    }

    /// <summary>
    /// Every word the player reads, in the language they read.
    ///
    /// <b>The code is written in English and English is the table's key.</b> A screen asks
    /// for <c>Loc.T("PLAY")</c> and gets "PLAY" or "SPELA"; there are no symbolic keys to
    /// look up and nothing to keep in step with them, and a string with no translation
    /// falls back to perfectly good English rather than to a key name on the screen.
    /// TranslationTests reads every call in the source and fails on any the Swedish table
    /// is missing, so that fallback is a safety net and not a way of shipping half a
    /// translation.
    ///
    /// Whole sentences, never pieces. "{0} of {1} points left" is one entry; built out of
    /// "of" and "points left" it would be unsayable in any language whose word order is
    /// not English's. Numbers go in through <see cref="F"/>, which formats them for the
    /// language as well — "6 %" and "0,5" in Swedish, "6%" and "0.5" in English.
    ///
    /// A table in code rather than Unity's Localization package, because the game has a
    /// couple of hundred short strings and one font, and that package brings string
    /// tables, locales, addressables and an editor window to manage them. Another
    /// language is another dictionary here.
    /// </summary>
    public static class Loc
    {
        const string LanguageKey = "theveil.language";

        static Language? _language;

        /// <summary>
        /// The language on screen: the player's choice if they have made one, and the
        /// device's otherwise — Swedish on a Swedish phone, English everywhere else.
        /// </summary>
        public static Language Language
        {
            get
            {
                if (_language == null)
                {
                    int chosen = PlayerPrefs.GetInt(LanguageKey, -1);
                    _language = chosen >= 0 && chosen <= (int)Language.Swedish
                        ? (Language)chosen
                        : FromSystem(Application.systemLanguage);
                }

                return _language.Value;
            }
            set
            {
                _language = value;
                PlayerPrefs.SetInt(LanguageKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The language a device set to <paramref name="system"/> starts in.</summary>
        public static Language FromSystem(SystemLanguage system)
            => system == SystemLanguage.Swedish ? Language.Swedish : Language.English;

        static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-GB");
        static readonly CultureInfo SwedishCulture = CultureInfo.GetCultureInfo("sv-SE");

        /// <summary>How numbers are written in a language.</summary>
        public static CultureInfo CultureOf(Language language)
            => language == Language.Swedish ? SwedishCulture : EnglishCulture;

        /// <summary>A fixed piece of text, in the current language.</summary>
        public static string T(string english) => Translate(english, Language);

        /// <summary>A sentence with numbers or names in it, in the current language.</summary>
        public static string F(string english, params object[] args)
            => Format(Language, english, args);

        public static string Translate(string english, Language language)
        {
            if (language == Language.Swedish && SwedishTable.TryGetValue(english, out var swedish))
                return swedish;

            return english;
        }

        public static string Format(Language language, string english, params object[] args)
            => string.Format(CultureOf(language), Translate(english, language), args);

        /// <summary>The Swedish table, for the test that holds it to the source.</summary>
        public static IReadOnlyDictionary<string, string> Swedish => SwedishTable;

        static readonly Dictionary<string, string> SwedishTable = new Dictionary<string, string>
        {
            // ---- shared ------------------------------------------------------------
            ["BACK"] = "TILLBAKA",
            ["SHOP"] = "BUTIK",
            ["TROOPS"] = "TRUPPER",
            ["UPGRADE"] = "UPPGRADERA",
            ["CHAPTER {0}"] = "KAPITEL {0}",
            ["CHAPTER {0}  ·  LEVEL {1}"] = "KAPITEL {0}  ·  NIVÅ {1}",

            // ---- front page ----------------------------------------------------------
            ["PLAY"] = "SPELA",
            ["ACHIEVEMENTS"] = "BRAGDER",
            ["SETTINGS"] = "INSTÄLLNINGAR",
            ["Settings"] = "Inställningar",
            ["Achievements"] = "Bragder",

            // ---- settings ------------------------------------------------------------
            ["LANGUAGE"] = "SPRÅK",
            ["SOUND"] = "LJUD",
            ["ON"] = "PÅ",
            ["OFF"] = "AV",
            ["PROGRESS"] = "FRAMSTEG",
            ["RESET PROGRESS"] = "NOLLSTÄLL FRAMSTEG",
            ["TAP AGAIN TO ERASE EVERYTHING"] = "TRYCK IGEN FÖR ATT RADERA ALLT",
            ["Stars, gold and everything bought are gone for good."]
                = "Stjärnor, guld och allt som köpts försvinner för gott.",

            // ---- achievements --------------------------------------------------------
            ["{0} of {1} done"] = "{0} av {1} klara",
            ["{0} / {1}"] = "{0} / {1}",
            ["COLLECTED"] = "HÄMTAD",
            ["{0} GOLD + {1} GEMS"] = "{0} GULD + {1} ÄDELSTENAR",
            ["{0} GEMS"] = "{0} ÄDELSTENAR",
            ["First Road"] = "Första vägen",
            ["Seasoned Guide"] = "Van vägvisare",
            ["Road Warden"] = "Vägväktare",
            ["Clean Crossing"] = "Ren färd",
            ["Master of the Road"] = "Vägens mästare",
            ["The Borderlands Crossed"] = "Gränslanden korsade",
            ["The King's Road Crossed"] = "Kungsvägen korsad",
            ["Blooded"] = "Stridsvan",
            ["Hundred Fights"] = "Hundra strider",
            ["Terror of the Wilds"] = "Vildmarkens skräck",
            ["Sapper"] = "Minör",
            ["Master Sapper"] = "Mästerminör",
            ["Not a Scratch"] = "Inte en skråma",
            ["Unbroken"] = "Obruten",
            ["Merchant"] = "Köpman",
            ["Merchant Prince"] = "Handelsfurste",
            ["Clear {0} levels."] = "Klara {0} nivåer.",
            ["Earn three stars on {0} levels."] = "Ta tre stjärnor på {0} nivåer.",
            ["Clear every level in {0} chapters."] = "Klara alla nivåer i {0} kapitel.",
            ["Defeat {0} enemy groups."] = "Besegra {0} fiendegrupper.",
            ["Disarm {0} traps."] = "Desarmera {0} fällor.",
            ["Arrive {0} times without losing a wagon."] = "Kom fram {0} gånger utan att förlora en vagn.",
            ["Earn {0} gold from the road."] = "Tjäna {0} guld på vägen.",

            // ---- roadmap -------------------------------------------------------------
            ["Choose a level"] = "Välj nivå",
            ["THE BORDERLANDS"] = "GRÄNSLANDEN",
            ["THE KING'S ROAD"] = "KUNGSVÄGEN",
            ["THE WETLANDS"] = "DE VÅTA MARKERNA",
            ["Needs {0} stars in chapter {1} — you have {2}"] = "Behöver {0} stjärnor i kapitel {1} — du har {2}",
            ["BATTLE"] = "STRID",

            // ---- troop screen ----------------------------------------------------------
            ["Escort"] = "Eskort",
            ["{0} of {1} points left"] = "{0} av {1} poäng kvar",
            ["{0} of {1} posts open in the line"] = "{0} av {1} poster öppna i ledet",
            ["{0} of {1} posts open in the line  ·  more open later"]
                = "{0} av {1} poster öppna i ledet  ·  fler öppnas längre fram",
            ["▲  DIRECTION OF TRAVEL"] = "▲  FÄRDRIKTNING",
            ["CLOSED"] = "STÄNGD",
            ["bought in the shop"] = "köps i butiken",
            ["already along"] = "redan med",
            ["CANCEL"] = "AVBRYT",
            ["CLEAR"] = "TÖM",
            ["DRAW THE ROAD"] = "RITA VÄGEN",
            ["double against wolves"] = "dubbelt mot vargar",
            ["hardest in close combat"] = "hårdast i närstrid",
            ["22 m reach, worse in forest"] = "22 m räckvidd, sämre i skog",
            ["strong on plains, weak in marsh"] = "stark på slätt, svag i träsk",
            ["18 m, expensive"] = "18 m, dyr",
            ["34 m sight, walks ahead, does not fight"] = "34 m sikt, går före, slåss inte",
            ["takes 40 % less damage"] = "tar 40 % mindre skada",
            ["heals the most wounded"] = "helar den mest sargade",
            ["disarms traps"] = "desarmerar fällor",

            // ---- names ---------------------------------------------------------------
            ["Spearmen"] = "Spjutmän",
            ["Swordsmen"] = "Svärdsmän",
            ["Archers"] = "Bågskyttar",
            ["Cavalry"] = "Ryttare",
            ["Mage"] = "Magiker",
            ["Scout"] = "Spejare",
            ["Shieldbearers"] = "Sköldbärare",
            ["Priest"] = "Präst",
            ["Engineer"] = "Ingenjör",
            ["SPEAR"] = "SPJUT",
            ["SWORD"] = "SVÄRD",
            ["BOW"] = "BÅGE",
            ["CAVALRY"] = "RYTTARE",
            ["MAGE"] = "MAGIKER",
            ["SCOUT"] = "SPEJARE",
            ["SHIELD"] = "SKÖLD",
            ["PRIEST"] = "PRÄST",
            ["ENGINEER"] = "INGENJÖR",
            ["VAN"] = "FÖRTRUPP",
            ["RIGHT FRONT"] = "HÖGER FRAM",
            ["LEFT FRONT"] = "VÄNSTER FRAM",
            ["RIGHT REAR"] = "HÖGER BAK",
            ["LEFT REAR"] = "VÄNSTER BAK",
            ["REARGUARD"] = "EFTERTRUPP",
            ["road"] = "väg",
            ["plains"] = "slätt",
            ["forest"] = "skog",
            ["marsh"] = "träsk",
            ["ford"] = "vadställe",
            ["mountain pass"] = "bergspass",
            ["water"] = "vatten",
            ["cliff"] = "brant",

            // ---- planning map ----------------------------------------------------------
            ["Your road"] = "Din väg",
            ["Tap the map to place waypoints. Drag to move them."]
                = "Tryck på kartan för att lägga ut vägpunkter. Dra för att flytta.",
            ["UNDO"] = "ÅNGRA",
            ["PLAY THIS ROAD"] = "SPELA DENNA VÄG",
            ["EAGLE-EYE  ·  {0} G"] = "ÖRNÖGA  ·  {0} G",
            ["EAGLE-EYE AGAIN  ·  {0} G"] = "ÖRNÖGA IGEN  ·  {0} G",
            ["Waypoints  {0} of {1}"] = "Vägpunkter  {0} av {1}",
            ["No passable road."] = "Ingen framkomlig väg.",
            ["Leg {0} cannot be walked."] = "Etapp {0} går inte att gå.",
            ["Move the point to firmer ground."] = "Flytta punkten till fastare mark.",
            ["Travel time  {0:F0} s"] = "Restid  {0:F0} s",
            ["Forest {0:P0}   marsh {1:P0}   road {2:P0}"] = "Skog {0:P0}   träsk {1:P0}   väg {2:P0}",
            ["Cover for an ambush  {0:F2}"] = "Skydd åt ett bakhåll  {0:F2}",
            ["Fords  {0}"] = "Vadställen  {0}",
            ["{0} leg(s) go far around."] = "{0} etapp(er) går långt runt.",

            // ---- the run ---------------------------------------------------------------
            ["{0:P0} of the way   ·   {1}   ·   {2:F0} s (par {3:F0} s)"]
                = "{0:P0} av vägen   ·   {1}   ·   {2:F0} s (par {3:F0} s)",
            ["Paused"] = "Paus",
            ["RESUME"] = "FORTSÄTT",
            ["RESTART"] = "BÖRJA OM",
            ["REDRAW THE ROAD"] = "RITA OM VÄGEN",
            ["QUIT"] = "AVSLUTA",
            ["Smithy"] = "Smedjan",
            ["{0} silver"] = "{0} silver",
            ["sight {0:F0} m"] = "sikt {0:F0} m",
            ["reach {0:F0} m"] = "räckvidd {0:F0} m",
            ["WEAPON"] = "VAPEN",
            ["ARMOUR"] = "SKYDD",
            ["REACH"] = "RÄCKV",
            ["SIGHT"] = "SIKT",
            ["FALLEN"] = "STUPAD",
            ["{0:F0} → {1:F0} dmg"] = "{0:F0} → {1:F0} skada",
            ["{0:F0} → {1:F0} hp"] = "{0:F0} → {1:F0} hp",
            ["{0:F0} → {1:F0} m"] = "{0:F0} → {1:F0} m",
            ["{0:F1} → {1:F1} m"] = "{0:F1} → {1:F1} m",
            ["{0:F0} dmg"] = "{0:F0} skada",
            ["{0:F0} hp"] = "{0:F0} hp",
            ["{0:F0} m"] = "{0:F0} m",
            ["Victory"] = "Seger",
            ["Defeat"] = "Nederlag",
            ["The caravan was lost."] = "Karavanen gick förlorad.",
            ["Your best result yet!"] = "Bästa resultatet hittills!",
            ["Cleared — your record stands."] = "Klarat — ditt rekord står kvar.",
            ["GOLD"] = "GULD",
            ["BEATEN"] = "SLAGNA",
            ["WAGONS"] = "VAGNAR",
            ["NEXT LEVEL"] = "NÄSTA NIVÅ",
            ["PLAY AGAIN"] = "SPELA OM",
            ["TRY AGAIN"] = "FÖRSÖK IGEN",
            ["TO THE MAP"] = "TILL KARTAN",

            // ---- shop ----------------------------------------------------------------
            ["Shop"] = "Butiken",
            ["{0} gold"] = "{0} guld",
            ["CARAVAN"] = "KARAVAN",
            ["SILVER"] = "SILVER",
            ["SCOUTING"] = "SPANING",
            ["PERMANENT TROOP LEVELS"] = "PERMANENTA TRUPPNIVÅER",
            ["{0} have no reach to buy — only archers and mages shoot from afar."]
                = "{0} har ingen räckvidd att köpa — bara bågskyttar och magiker skjuter på avstånd.",
            ["now {0}"] = "nu {0}",
            ["now {0}   ·   next step {1}"] = "nu {0}   ·   nästa steg {1}",
            ["FULLY BUILT"] = "FULLT UTBYGGT",
            ["{0} GOLD"] = "{0} GULD",
            ["step {0} / {1}"] = "steg {0} / {1}",
            ["Trading purse"] = "Handelskassa",
            ["Muster"] = "Värvning",
            ["Hardened wagons"] = "Härdade vagnar",
            ["Field smithy"] = "Fältsmedja",
            ["Outriders"] = "Förridare",
            ["Merchantry"] = "Köpmannaskap",
            ["Vigilance"] = "Vaksamhet",
            ["Tracking"] = "Spårsinne",
            ["Exchange office"] = "Växelkontor",
            ["Field repair"] = "Fältreparation",
            ["Lashings"] = "Lastsäkring",
            ["Silver in the purse as soon as the mission starts, so the first upgrade can be bought before the fight instead of after it."]
                = "Silver i kassan redan när uppdraget börjar, så första uppgraderingen kan köpas före striden i stället för efter.",
            ["More points to build the escort with. A point is a whole thing, hence few and costly steps."]
                = "Fler poäng att sätta ihop eskorten för. En poäng är en hel sak, därför få och dyra steg.",
            ["The wagons take more punishment before they break. If all three break, the mission is lost."]
                = "Vagnarna tål mer stryk innan de går sönder. Går alla tre sönder är uppdraget förlorat.",
            ["Cheaper to upgrade the troops in the middle of a mission."]
                = "Billigare att uppgradera trupperna mitt i ett uppdrag.",
            ["A post in the line opens earlier than the chapter would otherwise give it."]
                = "En post i ledet öppnas tidigare än kapitlet annars ger den.",
            ["More silver for every enemy group brought down, all mission long."]
                = "Mer silver för varje fiendegrupp som fälls, hela uppdraget igenom.",
            ["The caravan sees further, so enemies are revealed earlier — and only what has been revealed can be shot at."]
                = "Karavanen ser längre, så fiender avslöjas tidigare — och man kan bara skjuta på det som avslöjats.",
            ["Traps are spotted further ahead, giving the engineer time to disarm them before the wheels get there."]
                = "Fällor upptäcks längre fram, vilket ger ingenjören tid att desarmera dem innan hjulen är där.",
            ["A better rate when leftover silver is changed into gold after the mission. Spending it in the field is still better."]
                = "Bättre kurs när silver som blev över växlas till guld efter uppdraget. Att spendera i fält är fortfarande bättre.",
            ["The wagons are mended while the column rolls — but not while it fights. A quiet stretch becomes worth something."]
                = "Vagnarna lagas medan kolonnen rullar — men inte medan det slåss. En lugn sträcka blir värd något.",
            ["Walks ahead of the column and spots enemies long before they wake. Does not fight, and takes one of the escort's posts. Bought once."]
                = "Går före kolonnen och ser fiender långt innan de vaknar. Slåss inte, och tar en av eskortens platser. Köps en gång.",
            ["The treasure wagon takes less damage. Its state decides the gold you bring home, so it is as much a raise in pay."]
                = "Skattvagnen tar mindre skada. Dess skick avgör guldet du får ut, så det är en uppgradering av lönen lika mycket.",
            ["comes along whenever you like"] = "följer med när du vill",
            ["{0:F0} silver"] = "{0:F0} silver",
            ["{0:F0} points"] = "{0:F0} poäng",
            ["{0:F0} posts"] = "{0:F0} poster",
            ["{0:F1} m"] = "{0:F1} m",
            ["{0:F1} silver/gold"] = "{0:F1} silver/guld",
            ["{0:F2} hp/s"] = "{0:F2} hp/s",
            ["{0:F1} %"] = "{0:F1} %",
            ["hired"] = "anställd",
            ["not hired"] = "inte anställd",
            ["Weapon"] = "Vapen",
            ["Armour"] = "Rustning",
            ["Sight"] = "Sikt",
            ["Reach"] = "Räckvidd",
            ["The scout sees further, so enemies are spotted even earlier. Applies to every mission, on top of what you buy with silver in the field."]
                = "Spejaren ser längre, så fiender upptäcks ännu tidigare. Gäller varje uppdrag, ovanpå det du köper med silver ute i fält.",
            ["More damage. Applies to every mission, on top of what you buy with silver in the field."]
                = "Mer skada. Gäller varje uppdrag, ovanpå det du köper med silver ute i fält.",
            ["More health and a larger share of every blow turned aside. The troops march out with the extra health from the start."]
                = "Mer hälsa och en större andel av skadan avvärjd. Trupperna rycker ut med den extra hälsan redan från början.",
            ["Longer range. Remember that nothing can be shot before it has been revealed — reach and sight go together."]
                = "Längre skotthåll. Kom ihåg att inget kan skjutas innan det avslöjats — räckvidd och sikt hör ihop.",
            ["{0:F1} m sight"] = "{0:F1} m sikt",
            ["{0:F1} % damage"] = "{0:F1} % skada",
            ["{0:F1} % health"] = "{0:F1} % hälsa",
            ["{0:F1} % reach"] = "{0:F1} % räckvidd",
        };
    }
}
