using System.Collections.Generic;
using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// How far a startled animal actually gets: `The Veil &gt; Wildlife Report`.
    ///
    /// <b>Written to settle a threshold nobody could justify.</b> WildlifeTests asserts
    /// that an animal the caravan spooks ends up more than a set distance from where it
    /// was grazing - the guard is against one that bolts and gets nowhere. The number was
    /// twenty, which is neither of the distances the simulation knows: the caravan
    /// startles at twenty-six and an animal grazes within six. What twenty matched was
    /// how far the first animal of 1-5 could get on whichever map the catalogue happened
    /// to ship, so a rebuilt table moved it - and one did, to 19,8.
    ///
    /// The question that decides whether that is a fault is not answerable from one
    /// animal. If the others on the same ground run forty-odd metres and one runs twenty,
    /// the map has built a pen and the test was right to say so. If most of them run
    /// twenty, that is what this country's shoreline allows and the threshold was only
    /// ever describing one map.
    ///
    /// So every animal is asked, one at a time, each on a freshly populated level so the
    /// others' flight cannot push it along.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.WildlifeReport.Run
    /// </summary>
    public static class WildlifeReport
    {
        /// <summary>The flight the test measures: a step to startle, then sixty more.</summary>
        // Six seconds and a tenth, against a flight that lasts four and a half - so an
        // animal with open ground ahead of it has stopped running by the end and walked
        // a little way back. See Wildlife.FleeSeconds.
        const int Steps = 61;
        const float Tick = 0.1f;

        [MenuItem("The Veil/Wildlife Report")]
        public static void Run()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Wildlife] how far a startled animal gets, in metres");
            sheet.AppendLine($"[Wildlife] a clear run is {Wildlife.FleeSeconds * Wildlife.FleeSpeed:0} m; "
                             + $"it grazes within {Wildlife.GrazeRadius:0} and is startled at "
                             + $"{Wildlife.SpookRadius:0}");

            foreach (int chapter in DifficultyCurve.Dressed)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var ran = Flights(chapter, level);
                    if (ran.Count == 0) continue;

                    ran.Sort();

                    int penned = 0;
                    foreach (float far in ran) if (far < Pen) penned++;

                    sheet.AppendLine($"[Wildlife] {chapter}-{level,-2} {ran.Count,3} animals: "
                                     + $"shortest {ran[0],5:0.0}, half {ran[ran.Count / 2],5:0.0}, "
                                     + $"longest {ran[ran.Count - 1],5:0.0}  "
                                     + $"{penned} under {Pen:0}");
                }
            }

            Debug.Log(sheet.ToString());
        }

        /// <summary>The distance the test would call too short, for counting.</summary>
        // Twenty, which is the number being argued about.
        const float Pen = 20f;

        /// <summary>
        /// Every animal of a level, startled on its own and followed.
        ///
        /// Repopulated for each, because the caravan that startles one startles whatever
        /// else is within its radius, and an animal shoved along by the herd is not the
        /// thing being measured.
        /// </summary>
        static List<float> Flights(int chapter, int level)
        {
            var ran = new List<float>();
            var map = LevelMaps.For(chapter, level);

            int count = Wildlife.Populate(map).Count;

            for (int i = 0; i < count; i++)
            {
                var animals = Wildlife.Populate(map);
                var target = animals[i];

                var home = target.Home;

                for (int step = 0; step < Steps; step++)
                    Wildlife.Step(map.Grid, animals, home, null, Tick);

                float dx = target.Position.X - home.X;
                float dy = target.Position.Y - home.Y;

                ran.Add(Mathf.Sqrt(dx * dx + dy * dy));
            }

            return ran;
        }
    }
}
