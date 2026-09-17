using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// What each level actually ships, and which of the generator's promises it keeps.
    ///
    /// <b>Sixty levels out of a hundred ship a map the generator never accepted, and
    /// nothing anywhere said so.</b> Generate returns the first candidate that satisfies
    /// every condition and, failing that, the best of a bad lot — with an ordinary
    /// attempt number on it. LevelMap.Accepted tells the two apart now; this says *which
    /// condition* the compromises fall on, which is the question that decides whether the
    /// answer is a looser recipe, a longer search, or a fault in the placer.
    ///
    /// Four of the five accept conditions can be read off a finished map from out here.
    /// The fifth — PassableRoutes against RoutesOwed — is private to the generator and
    /// its estimate, and the honest stand-in is the thing that estimate is an estimate of:
    /// whether the reference escort can actually get down a road. Both are printed, per
    /// corridor, so a level that fails only the estimate can be told from one that fails
    /// in earnest.
    /// </summary>
    public static class LevelReport
    {
        [MenuItem("The Veil/Level Report")]
        public static void Run()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Levels] chapter-level: attempt, verdict, and the promises kept");

            int compromised = 0, unwinnable = 0, wet = 0;
            var why = new int[4];

            for (int chapter = 1; chapter <= LevelCatalogue.Chapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var recipe = LevelMaps.Recipe(chapter, level);
                    var map = LevelMaps.For(chapter, level);

                    bool choice = map.ChoiceValidated;
                    bool kept = map.Encounters.EncountersValidated;
                    int crossings = Crossings.Count(map.Grid);
                    bool crossable = crossings >= recipe.CrossingsOwed;
                    bool winnable = LevelMaps.Winnable(map, chapter, level);

                    if (!map.Accepted) compromised++;
                    if (!winnable) unwinnable++;

                    if (!map.Accepted)
                    {
                        if (!choice) why[0]++;
                        if (!kept) why[1]++;
                        if (!crossable) why[2]++;
                        if (!winnable) why[3]++;
                    }

                    // And where the road begins and ends, which is the fault a playtest
                    // found: a caravan standing in the water before it has moved.
                    string ends = Ends(map, out bool damp);
                    if (damp) wet++;

                    if (map.Accepted && winnable && !damp) continue;

                    sheet.AppendLine($"[Levels] {chapter}-{level}: attempt {map.Attempts - 1}, "
                                     + (map.Accepted ? "accepted" : "COMPROMISE")
                                     + (winnable ? "" : ", UNWINNABLE")
                                     + $" | choice {Yes(choice)} encounters {Yes(kept)} "
                                     + $"crossings {crossings}/{recipe.CrossingsOwed} "
                                     + $"| {ends}"
                                     + (choice ? "" : " | " + WhyNoChoice(map)));
                }
            }

            sheet.AppendLine($"[Levels] {compromised} of 100 ship a compromise, "
                             + $"{unwinnable} cannot be won, {wet} begin or end in water");
            sheet.AppendLine($"[Levels] of the compromises: {why[0]} offer no real choice, "
                             + $"{why[1]} have encounters the placer could not validate, "
                             + $"{why[2]} are short of crossings, {why[3]} cannot be won");

            Debug.Log(sheet.ToString());
        }

        static string Yes(bool held) => held ? "ok" : "NO";

        /// <summary>
        /// Which of IsMeaningfulChoice's five conditions the level falls on.
        ///
        /// The dominant failure by a long way and it is reported as one bit, so the answer
        /// could be a threshold, the corridor finder, or the squaring at the crossings -
        /// and there is no telling which from outside. The thresholds are repeated here
        /// from CorridorFinder's own defaults; they are printed beside the measurement so
        /// a drift between the two is visible rather than silent.
        /// </summary>
        static string WhyNoChoice(LevelMap map)
        {
            Corridor fast = null, safe = null;

            foreach (var corridor in map.Corridors)
            {
                if (corridor.Kind == CorridorKind.Fast) fast = corridor;
                if (corridor.Kind == CorridorKind.Safe) safe = corridor;
            }

            if (fast == null || safe == null) return "no fast/safe pair at all";

            float overlap = CorridorFinder.Overlap(fast, safe);

            float time = fast.TravelCost <= 0f ? 0f
                       : (safe.TravelCost - fast.TravelCost) / fast.TravelCost;
            float danger = safe.AmbushExposure <= 0f ? 0f
                         : (fast.AmbushExposure - safe.AmbushExposure) / safe.AmbushExposure;

            var said = new StringBuilder();
            said.Append($"overlap {overlap:0.00}/0.62");
            if (overlap > 0.62f) said.Append(" OVER");

            said.Append($", time {time:0.00}/0.12");
            if (time < 0.12f) said.Append(" UNDER");

            said.Append($", danger {danger:0.00}/0.08");
            if (danger < 0.08f) said.Append(" UNDER");

            return said.ToString();
        }

        /// <summary>The terrain the road starts and ends on, and whether either is wet.</summary>
        static string Ends(LevelMap map, out bool damp)
        {
            var start = map.Grid[map.StartIndex];
            var goal = map.Grid[map.GoalIndex];

            damp = Wet(start) || Wet(goal);
            return $"start {start}, goal {goal}";
        }

        static bool Wet(TerrainType at) => at == TerrainType.Water || at == TerrainType.Ford;
    }
}
