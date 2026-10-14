using System.Collections.Generic;
using System.Text;
using TheVeil.App;
using TheVeil.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Whether the harbour has a street: `The Veil &gt; Waterfront Report`.
    ///
    /// <b>Written because a photograph of a bay cannot answer it.</b> The waterfront pass
    /// says out loud how many buildings it raised - nine of ten - and the picture taken
    /// from the jetty shows one of them, in a wood, with the other eight somewhere out of
    /// frame or under a tree. Those are two different faults with two different fixes and
    /// the picture cannot tell them apart.
    ///
    /// So the buildings are counted where they stand: how far each is from the water, how
    /// far from the one before it, and what is growing on the ground in front of it. A
    /// street is buildings in a line, near the water, with nothing between them and it.
    /// Each of those three is a number here.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.WaterfrontReport.Run
    /// </summary>
    public static class WaterfrontReport
    {
        /// <summary>How far in front of a house is looked at for undergrowth, in metres.</summary>
        // Eight, which is the two tiles the quay reserves. Anything standing inside that
        // is standing between a door and the sea.
        const float Frontage = 8f;

        /// <summary>How near the water a building has to be to be part of the front.</summary>
        // Four tiles. The front is built on the dry tile nearest the water; the coast's
        // village is thirty-two metres inland, so there is no argument about which is
        // which.
        const float Shoreline = 16f;

        [MenuItem("The Veil/Waterfront Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                         OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Waterfront] PlayLevel has no LevelRunner."); return; }

            var note = new StringBuilder();
            note.AppendLine("[Waterfront] level  front  gap worst  gap mean  in the way");

            int chapter = Biomes.LastChapter;

            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
            {
                var root = SmokeTest.Build(runner, chapter, level, out var map);

                var houses = new List<Transform>();
                var green = new List<Transform>();
                var planks = new List<Transform>();
                Sort(root.transform, houses, green, planks);

                // <b>The front is the houses that stand on the water.</b> A coast level
                // also has its village, inland, and counting those in would hide a front
                // of three among a hamlet of ten. Sixteen metres is four tiles: the front
                // is built on the dry tile nearest the water and the village was measured
                // at thirty-two.
                var front = new List<Transform>();
                foreach (var house in houses)
                    if (ToTheWater(map, house.position) <= Shoreline) front.Add(house);

                if (front.Count == 0)
                {
                    note.AppendLine($"[Waterfront] {level,5}  none");
                    Object.DestroyImmediate(root);
                    continue;
                }

                // Walked from one end, nearest to nearest, because the order they were
                // built in is not recorded and a street is a street whichever way it is
                // read along.
                var walk = Chain(front);

                float worst = 0f, total = 0f;

                for (int i = 1; i < walk.Count; i++)
                {
                    float gap = Flat(walk[i].position - walk[i - 1].position);
                    total += gap;
                    if (gap > worst) worst = gap;
                }

                // <b>And what is standing between a door and the sea.</b> Within a house
                // of the house, and nearer the water than the house is: that is the
                // ground the quay reserves, measured without knowing how the quay decides
                // it.
                int blocked = 0;

                foreach (var house in front)
                {
                    float wet = ToTheWater(map, house.position);

                    foreach (var plant in green)
                    {
                        if (Flat(plant.position - house.position) > Frontage) continue;
                        if (ToTheWater(map, plant.position) >= wet) continue;

                        blocked++;
                        break;
                    }
                }

                note.AppendLine($"[Waterfront] {level,5}  {front.Count,5}  {worst,9:0.0}  "
                                + $"{(walk.Count > 1 ? total / (walk.Count - 1) : 0f),8:0.0}  "
                                + $"{blocked,4} of {front.Count}");

                Object.DestroyImmediate(root);
            }

            Debug.Log(note.ToString());
        }

        /// <summary>The buildings of the front, and what grows.</summary>
        // By name, because the decorator keeps no register of what it raised. The coast's
        // Houses set is seven shanties and three rickety houses and nothing else on the
        // level is called either.
        static void Sort(Transform at, List<Transform> houses, List<Transform> green,
                         List<Transform> planks)
        {
            foreach (Transform child in at)
            {
                string name = child.name;

                if (Has(name, "Shanty") || Has(name, "Rickety_House")) houses.Add(child);
                else if (Has(name, "Dock")) planks.Add(child);
                else if (Has(name, "Tree") || Has(name, "Bush") || Has(name, "Plant")
                         || Has(name, "Palm") || Has(name, "Mangrove")) green.Add(child);

                Sort(child, houses, green, planks);
            }
        }

        static bool Has(string name, string part)
            => name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The buildings in the order a walk along them visits them.</summary>
        // Nearest-neighbour from one end, which for points strung along a shoreline is
        // the street itself. Started from the one furthest from the middle, because a
        // chain begun in the middle reports half a street.
        static List<Transform> Chain(List<Transform> of)
        {
            var left = new List<Transform>(of);
            var walk = new List<Transform>();

            var middle = Vector3.zero;
            foreach (var one in left) middle += one.position;
            middle /= left.Count;

            int first = 0;
            for (int i = 1; i < left.Count; i++)
                if (Flat(left[i].position - middle) > Flat(left[first].position - middle)) first = i;

            walk.Add(left[first]);
            left.RemoveAt(first);

            while (left.Count > 0)
            {
                int next = 0;
                for (int i = 1; i < left.Count; i++)
                    if (Flat(left[i].position - walk[walk.Count - 1].position)
                        < Flat(left[next].position - walk[walk.Count - 1].position)) next = i;

                walk.Add(left[next]);
                left.RemoveAt(next);
            }

            return walk;
        }

        static float Flat(Vector3 by) { by.y = 0f; return by.magnitude; }

        /// <summary>How far from this point the nearest water is, in metres.</summary>
        static float ToTheWater(LevelMap map, Vector3 at)
        {
            var grid = map.Grid;

            int cx = Mathf.RoundToInt(at.x / TileGrid.TileSize);
            int cy = Mathf.RoundToInt(at.z / TileGrid.TileSize);

            for (int reach = 0; reach <= 8; reach++)
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        if (Mathf.Abs(dx) != reach && Mathf.Abs(dy) != reach) continue;
                        if (!grid.InBounds(cx + dx, cy + dy)) continue;

                        if (grid[grid.ToIndex(cx + dx, cy + dy)] == TerrainType.Water)
                            return reach * TileGrid.TileSize;
                    }

            return 8 * TileGrid.TileSize;
        }
    }
}
