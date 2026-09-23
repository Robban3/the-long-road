using System.Collections.Generic;
using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>How far a river falls between one tile and the next, measured over the built chapters.</summary>
    public static class FallReport
    {
        public static void Run()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Fall] the steepest step in each level's water, in metres");

            float worst = 0f;
            int steep = 0, levels = 0;

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var grid = LevelMaps.For(chapter, level).Grid;
                    float most = 0f;
                    int drops = 0;

                    for (int y = 0; y < grid.Height; y++)
                        for (int x = 0; x < grid.Width; x++)
                        {
                            int tile = grid.ToIndex(x, y);
                            if (!Wet(grid[tile])) continue;

                            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                if (!grid.InBounds(x + dx, y + dy)) continue;
                                int next = grid.ToIndex(x + dx, y + dy);
                                if (!Wet(grid[next])) continue;

                                float drop = (grid.Elevation(tile) - grid.Elevation(next))
                                             * 14f;
                                if (drop > most) most = drop;
                                if (drop >= 2f) drops++;
                            }
                        }

                    levels++;
                    if (most >= 2f) steep++;
                    if (most > worst) worst = most;
                    sheet.AppendLine($"[Fall] {chapter}-{level}: steepest {most:0.0} m, {drops} steps of two metres or more");
                }

            sheet.AppendLine($"[Fall] {steep} of {levels} levels have a step of two metres or more; worst {worst:0.0} m");
            Debug.Log(sheet.ToString());
        }

        static bool Wet(TerrainType t) => t == TerrainType.Water || t == TerrainType.Ford;
    }
}
