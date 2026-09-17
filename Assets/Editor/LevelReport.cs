using System.Collections.Generic;
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
    /// Every accept condition can be read off a finished map from out here, now that the
    /// last of them is a measurement rather than a proxy: how many of a level's roads the
    /// reference escort can be got down. So a level that shows every condition holding and
    /// COMPROMISE beside it is a level that fails on the road count, and nothing else.
    /// </summary>
    public static class LevelReport
    {
        [MenuItem("The Veil/Level Report")]
        public static void Run()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Levels] chapter-level: attempt, verdict, and the promises kept");

            int compromised = 0, unwinnable = 0, wet = 0;
            var groups = new List<string>();
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

                    // <b>How many groups the budget bought, against whether the
                    // placement held.</b> The count is what the promise is made of - every
                    // route meets five - and it is decided by a uniform draw over enemy
                    // kinds that cost anything from a few points to sixty. Printed for
                    // every level so the two can be seen together.
                    groups.Add((kept ? "kept " : "SHORT") + $" {chapter}-{level}: "
                               + $"{map.Encounters.Enemies.Count} groups, worst route "
                               + $"{map.Encounters.MinEncounters} | " + PerRoad(map));

                    if (map.Accepted && winnable && !damp) continue;

                    sheet.AppendLine($"[Levels] {chapter}-{level}: attempt {map.Attempts - 1}, "
                                     + (map.Accepted ? "accepted" : "COMPROMISE")
                                     + (winnable ? "" : ", UNWINNABLE")
                                     + $" | choice {Yes(choice)} encounters {Yes(kept)} "
                                     + $"crossings {crossings}/{recipe.CrossingsOwed} "
                                     + $"| {ends}"
                                     + (choice ? "" : " | " + WhyNoChoice(map))
                                     + (kept ? "" : " | " + Starved(map))
                                     + " | " + Ground(map));
                }
            }

            sheet.AppendLine($"[Levels] {compromised} of 100 ship a compromise, "
                             + $"{unwinnable} cannot be won, {wet} begin or end in water");
            sheet.AppendLine($"[Levels] of the compromises: {why[0]} offer no real choice, "
                             + $"{why[1]} have encounters the placer could not validate, "
                             + $"{why[2]} are short of crossings, {why[3]} cannot be won");

            groups.Sort();
            foreach (string line in groups) sheet.AppendLine("[Groups] " + line);

            Debug.Log(sheet.ToString());
        }

        static string Yes(bool held) => held ? "ok" : "NO";

        /// <summary>
        /// What each of the three roads meets, and what it costs in time and exposure.
        ///
        /// The design says the three roads carry different weights of enemy - the quick
        /// comfortable one crawling with them, the hard slog nearly empty - and the placer
        /// says every road must meet at least MinEncounters, with a repair loop that moves
        /// groups onto whichever road meets fewest. Those two cannot both be true, and
        /// this is the number that says which one is winning.
        /// </summary>
        static string PerRoad(LevelMap map)
        {
            var said = new StringBuilder();
            var owner = EncounterPlacer.RoadsideOf(map.Grid, map.Corridors);

            foreach (var corridor in map.Corridors)
            {
                int met = EncounterPlacer.MetGroups(map.Grid, corridor.Tiles, map.Encounters,
                                                    roadOnly: true).Count;
                int points = 0;
                foreach (int group in EncounterPlacer.MetGroups(map.Grid, corridor.Tiles,
                                                                map.Encounters, roadOnly: true))
                    points += EnemyTable.Points(map.Encounters.Enemies[group].Kind);

                if (said.Length > 0) said.Append("  ");
                // What the road was given, beside what a caravan down it actually runs
                // into. The first is the allocation; the second is the allocation after
                // every road has wandered through every other road's country.
                int laid = 0, laidPoints = 0;
                for (int i = 0; i < map.Encounters.Enemies.Count; i++)
                {
                    if (owner == null || owner[map.Encounters.Enemies[i].Tile] != (int)corridor.Kind)
                        continue;

                    laid++;
                    laidPoints += EnemyTable.Points(map.Encounters.Enemies[i].Kind);
                }

                said.Append($"{corridor.Kind} laid {laid}g/{laidPoints}p met {met}g/{points}p "
                            + $"t{corridor.TravelCost:0} x{corridor.AmbushExposure:0.00}");
            }

            return said.ToString();
        }

        /// <summary>
        /// How far short of its promise the placement came, and what it had to work with.
        ///
        /// The promise is that every route a player might draw meets enough to be a level
        /// (EncounterPlacer.MinEncounters), aimed one higher because the loop can only
        /// repair the routes it sampled. When that fails the question is always the same:
        /// too few groups on the map, or enough groups in the wrong places.
        /// </summary>
        static string Starved(LevelMap map)
        {
            int guards = 0, repaired = 0;

            foreach (var spawn in map.Encounters.Enemies)
            {
                if (spawn.Origin == PlacementOrigin.Guard) guards++;
                if (spawn.Origin == PlacementOrigin.Repair) repaired++;
            }

            return $"worst route meets {map.Encounters.MinEncounters}/"
                   + $"{EncounterPlacer.RepairTarget}, {map.Encounters.Enemies.Count} groups "
                   + $"({guards} placed, {repaired} moved), {map.Encounters.Repairs} repairs";
        }

        /// <summary>What the level is made of, as a count per terrain type.</summary>
        static string Ground(LevelMap map)
        {
            var count = new int[8];
            for (int i = 0; i < map.Grid.TileCount; i++) count[(int)map.Grid[i]]++;

            var said = new StringBuilder();
            for (int i = 0; i < count.Length; i++)
            {
                if (count[i] == 0) continue;
                if (said.Length > 0) said.Append(' ');
                said.Append($"{(TerrainType)i} {count[i]}");
            }

            return said.ToString();
        }

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
