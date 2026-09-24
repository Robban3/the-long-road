using System.Collections.Generic;
using System.Linq;
using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// What stands on the ground and does not stop anybody: `The Veil > Solid Report`.
    ///
    /// <b>Because "the troops walk through the rocks" is not one bug.</b> Three separate
    /// things can put a prop in somebody's way: the prop may carry no <see cref="Solid"/>
    /// at all, because the pass that placed it never marked it; it may carry one that is
    /// far too small for it, because the disc is sized off its thinnest side; or it may be
    /// standing in the caravan's lane, which is supposed to have been cleared of anything
    /// a wagon cannot roll over. Each has a different fix, and hunting them one screenshot
    /// at a time is how a session goes by.
    ///
    /// So the levels are built and measured. Every prop tall enough to matter is asked
    /// whether anything about it is solid, how much of its own footprint that covers, and
    /// whether it is standing on the drawn route. What comes out is a list of model names,
    /// which is what a fix is written against.
    ///
    /// Trees are expected in the leaky list: a spruce blocks its trunk and not its crown,
    /// which is deliberate (see <see cref="Solid"/>). Everything else in it is a fault.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.SolidReport.Run
    /// </summary>
    public static class SolidReport
    {
        [MenuItem("The Veil/Solid Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Solid] no LevelRunner in the scene"); return; }

            var open = new Dictionary<string, Tally>();
            var leaky = new Dictionary<string, Tally>();
            var inTheRoad = new Dictionary<string, Tally>();

            for (int chapter = 1; chapter <= DifficultyCurve.BuiltChapters; chapter++)
            {
                // The first level and the last: the last is the one with the castle on it,
                // and the castle is half of what this report was written for.
                foreach (int level in new[] { 1, Campaign.LevelsPerChapter })
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);
                    Measure(map, $"{chapter}-{level}", root, open, leaky, inTheRoad);
                    Object.DestroyImmediate(root);
                }
            }

            Report("nothing solid about them", open);
            Report("solid in the middle only", leaky);
            Report("standing in the caravan's lane", inTheRoad);

            Debug.Log("[Solid] done");
        }

        /// <summary>One model, and the worst of what was seen of it.</summary>
        sealed class Tally
        {
            public int Count;
            public float Wide;
            public float Tall;
            public float Reach;
            public string Where = "";
        }

        static void Measure(LevelMap map, string where, GameObject root,
                            Dictionary<string, Tally> open,
                            Dictionary<string, Tally> leaky,
                            Dictionary<string, Tally> inTheRoad)
        {
            var props = root.transform.Find("Props");
            if (props == null) return;

            var grid = map.Grid;
            float span = grid.Width * TileGrid.TileSize;
            var lane = new HashSet<int>(LevelPreview.Travelled(map));

            foreach (Transform prop in props)
            {
                if (prop.GetComponentInChildren<MeshRenderer>() == null) continue;

                // The water, the ground beyond the boundary and everything on the skyline:
                // none of it is walked through because none of it is walked on.
                if (Scenery(prop.name)) continue;

                var bounds = ModelScaling.Measure(prop.gameObject);
                if (bounds.size.y < TerrainDecorator.SolidHeight) continue;

                // Off the map is the apron and the backdrop, whatever they are called.
                if (bounds.center.x < 0f || bounds.center.x > span) continue;
                if (bounds.center.z < 0f || bounds.center.z > span) continue;

                float wide = Mathf.Max(bounds.extents.x, bounds.extents.z);
                var centre = new Vector2(bounds.center.x, bounds.center.z);

                var discs = prop.GetComponentsInChildren<Solid>(true);
                float reach = discs.Length == 0
                    ? 0f
                    : discs.Max(d => Vector2.Distance(d.Centre, centre) + d.Radius);

                if (discs.Length == 0) Note(open, prop.name, wide, bounds.size.y, reach, where);
                else if (reach < wide * Covered) Note(leaky, prop.name, wide, bounds.size.y, reach, where);

                // In the lane, asked of what a wheel would actually hit. A crown over the
                // road is wanted and a trunk in it is not, so the discs are what is
                // measured - and where there are none, the body, because a prop nobody
                // marked is hit by the whole of itself.
                if (bounds.size.y >= TerrainDecorator.DriveClearance && Blocking(grid, lane, discs, bounds))
                    Note(inTheRoad, prop.name, wide, bounds.size.y, reach, where);
            }
        }

        /// <summary>How much of its own footprint a prop has to hold to count as solid.</summary>
        // Two thirds. A disc is drawn round the middle and a model is never a cylinder, so
        // demanding the whole footprint would flag everything; two thirds separates a rock
        // that stops you from a rock with a metre of open ground inside it.
        const float Covered = 0.66f;

        /// <summary>Whether what is solid about this prop stands on the drawn route.</summary>
        static bool Blocking(TileGrid grid, HashSet<int> lane, Solid[] discs, Bounds bounds)
        {
            if (discs.Length == 0) return Underfoot(grid, lane, bounds);

            // A tile counts as held when its middle is inside the disc, which is the rule
            // the decorator places by (ForEachTileUnder). Measured as a box round the disc
            // instead, this reported four hundred props the decorator had already refused -
            // a disc two metres across has corners at 2.8 - and a report that disagrees
            // with the code it is checking is worse than no report.
            foreach (var disc in discs)
                for (int tile = 0; tile < grid.TileCount; tile++)
                {
                    if (!lane.Contains(tile)) continue;

                    var middle = Vec2.FromTile(grid, tile);
                    if (Vector2.Distance(new Vector2(middle.X, middle.Y), disc.Centre) <= disc.Radius)
                        return true;
                }

            return false;
        }

        /// <summary>Whether any tile this prop reaches into is on the drawn route.</summary>
        static bool Underfoot(TileGrid grid, HashSet<int> lane, Bounds bounds)
        {
            int from = Mathf.FloorToInt(bounds.min.x / TileGrid.TileSize);
            int to = Mathf.FloorToInt(bounds.max.x / TileGrid.TileSize);
            int low = Mathf.FloorToInt(bounds.min.z / TileGrid.TileSize);
            int high = Mathf.FloorToInt(bounds.max.z / TileGrid.TileSize);

            for (int x = from; x <= to; x++)
            for (int y = low; y <= high; y++)
            {
                if (x < 0 || y < 0 || x >= grid.Width || y >= grid.Height) continue;
                if (lane.Contains(y * grid.Width + x)) return true;
            }

            return false;
        }

        /// <summary>What is not on the playing field, by the names the decorator gives it.</summary>
        static bool Scenery(string name) =>
            name.StartsWith("Water") || name.StartsWith("Pool") || name.StartsWith("Fall")
            || name.StartsWith("Backdrop") || name.StartsWith("Horizon") || name.StartsWith("Apron")
            || name.StartsWith("Sky") || name.StartsWith("Ground");

        static void Note(Dictionary<string, Tally> into, string name,
                         float wide, float tall, float reach, string where)
        {
            if (!into.TryGetValue(name, out var tally))
                into[name] = tally = new Tally { Reach = float.MaxValue };

            tally.Count++;
            if (wide > tally.Wide) { tally.Wide = wide; tally.Tall = tall; tally.Where = where; }
            if (reach < tally.Reach) tally.Reach = reach;
        }

        static void Report(string what, Dictionary<string, Tally> found)
        {
            Debug.Log($"[Solid] --- {what}: {found.Count} models, "
                      + $"{found.Values.Sum(t => t.Count)} standing ---");

            foreach (var one in found.OrderByDescending(p => p.Value.Wide).Take(40))
                Debug.Log($"[Solid] {one.Key} x{one.Value.Count}: "
                          + $"{one.Value.Wide * 2f:0.0} m across, {one.Value.Tall:0.0} m tall, "
                          + $"solid out to {one.Value.Reach:0.0} m (widest on {one.Value.Where})");
        }
    }
}
