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

        [MenuItem("The Veil/Waterfront Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                         OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Waterfront] PlayLevel has no LevelRunner."); return; }

            var note = new StringBuilder();
            note.AppendLine("[Waterfront] level  wrecks  front  loneliest  nearest  from water  in the way");

            int chapter = Biomes.LastChapter;

            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
            {
                var root = SmokeTest.Build(runner, chapter, level, out var map);

                var houses = new List<Transform>();
                var green = new List<Transform>();
                var planks = new List<Transform>();
                Sort(root.transform, houses, green, planks);

                // The instances only: the pack's hulk is a prefab of named parts and a
                // plain name search counts the parts as well.
                int wrecks = Count(root.transform, "Shipwreck");

                Shoreline(map, runner.HeightScale, level, note);

                if (level == Campaign.LevelsPerChapter && planks.Count > 0)
                {
                    var deck = TheVeil.View.ModelScaling.Measure(planks[0].gameObject);
                    var sheet = Sheet(root.transform, deck.center);

                    // And the sea further out, because a picture taken from over the
                    // water looked through a sheet standing higher than the one in the
                    // harbour.
                    var open = Sheet(root.transform,
                                     deck.center + new Vector3(60f, 0f, 60f));
                    var far = Sheet(root.transform,
                                    deck.center + new Vector3(-60f, 0f, -60f));

                    note.AppendLine($"[Waterfront]   deck top {deck.max.y:0.0}, "
                                    + $"water beside it {(sheet == null ? "?" : sheet.Value.ToString("0.0"))}"
                                    + $", 85 m out {(open == null ? "?" : open.Value.ToString("0.0"))}"
                                    + $" / {(far == null ? "?" : far.Value.ToString("0.0"))}");
                }

                if (planks.Count > 0)
                    note.AppendLine($"[Waterfront]   {level}: {planks.Count} section(s) of "
                                    + $"{planks[0].name}");

                // <b>The front is what the front pass built, and nothing else.</b>
                //
                // This took every building within four tiles of water, on the reasoning
                // that the village is inland and the front is not. It is not sound: the
                // coast's village is built out of the same ten models and lands wherever
                // the settlement site is, which can be a beach. A sheet that reported
                // twelve buildings fifteen metres apart was describing a village, two
                // sheds and a front of nine all at once, and the photograph that showed
                // an empty shoreline was right.
                //
                // The pass names its own work now, so this can ask. A node was tried
                // first and three of the decorator's sweeps measure a direct child of
                // the prop root as one object: the bridge sweep found the box round a
                // hundred and twenty metres of street and destroyed the lot in one call.
                var front = new List<Transform>();
                foreach (var house in houses)
                    if (house.name.StartsWith(TheVeil.View.TerrainDecorator.Front,
                                              System.StringComparison.Ordinal))
                        front.Add(house);

                if (front.Count == 0)
                {
                    note.AppendLine($"[Waterfront] {level,5}  {wrecks,6}  none");
                    Object.DestroyImmediate(root);
                    continue;
                }

                // <b>How far each building is from its nearest neighbour, not from the
                // next one along a walk.</b>
                //
                // The buildings were chained end to end and the step between them
                // called the gap, which reads a town of two rows as one line that keeps
                // doubling back: a shed in the back row behind the last house came out
                // as a thirty-seven metre gap in a street whose widest real gap was
                // sixteen. The pass was then sent looking for a hole that was not
                // there.
                //
                // Nearest neighbour says what the eye asks - is this building standing
                // with the others or on its own - and does not depend on an order
                // nobody recorded.
                var walk = Chain(front);

                float worst = 0f, total = 0f;

                foreach (var house in front)
                {
                    float near = float.MaxValue;

                    foreach (var other in front)
                    {
                        if (other == house) continue;

                        float apart = Flat(other.position - house.position);
                        if (apart < near) near = apart;
                    }

                    if (near == float.MaxValue) continue;

                    total += near;
                    if (near > worst) worst = near;
                }

                // <b>And what is standing between a door and the sea.</b> Within a house
                // of the house, and nearer the water than the house is: that is the
                // ground the quay reserves, measured without knowing how the quay decides
                // it.
                int blocked = 0;
                float inland = 0f;

                foreach (var house in front)
                {
                    float wet = ToTheWater(map, house.position);
                    if (wet > inland) inland = wet;

                    foreach (var plant in green)
                    {
                        if (Flat(plant.position - house.position) > Frontage) continue;
                        if (ToTheWater(map, plant.position) >= wet) continue;

                        blocked++;
                        break;
                    }
                }

                // <b>And how tall each of them is, which is the column that was
                // missing.</b> The front was built with a Choice whose size was zero,
                // and a Choice of size zero is fitted to a height of zero: twelve
                // buildings stood in a line along the water and every one of them had no
                // size. Three sets of photographs of an empty beach were taken before
                // anybody asked the sheet how big the buildings were, because the sheet
                // could not say.
                if (level == Campaign.LevelsPerChapter)
                    foreach (var house in walk)
                        note.AppendLine($"[Waterfront]   {house.name} at "
                                        + $"{house.position.x:0}, {house.position.z:0}, "
                                        + $"{ToTheWater(map, house.position):0.0} m from water, "
                                        + $"{TheVeil.View.ModelScaling.Measure(house.gameObject).size.y:0.0} m tall");

                note.AppendLine($"[Waterfront] {level,5}  {wrecks,6}  {front.Count,5}  {worst,9:0.0}  "
                                + $"{(front.Count > 0 ? total / front.Count : 0f),8:0.0}  "
                                + $"{inland,10:0.0}  {blocked,4} of {front.Count}");

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

                if (Has(name, "Shanty") || Has(name, "Rickety_House")
                    || name.StartsWith(TheVeil.View.TerrainDecorator.Front,
                                       System.StringComparison.Ordinal)) houses.Add(child);
                else if (Has(name, "Dock")) planks.Add(child);
                else if (Has(name, "Tree") || Has(name, "Bush") || Has(name, "Plant")
                         || Has(name, "Palm") || Has(name, "Mangrove")) green.Add(child);

                Sort(child, houses, green, planks);
            }
        }

        /// <summary>How many pieces on the level are called this.</summary>
        static int Count(Transform at, string part)
        {
            int found = 0;

            void Walk(Transform where)
            {
                foreach (Transform child in where)
                {
                    if (Has(child.name, part) && Has(child.name, "(Clone)")) found++;
                    Walk(child);
                }
            }

            Walk(at);
            return found;
        }

        /// <summary>
        /// How high the water stands beside this point.
        ///
        /// The sheet's own box is no use: it covers the rivers as well, which run uphill
        /// away from the sea, so its top is the head of a stream somewhere inland. The
        /// vertex nearest the point is the water at the point.
        /// </summary>
        static float? Sheet(Transform at, Vector3 near)
        {
            foreach (Transform child in at)
            {
                var filter = child.GetComponent<MeshFilter>();

                if (child.name == "Water" && filter != null && filter.sharedMesh != null)
                {
                    float closest = float.MaxValue;
                    float found = 0f;

                    foreach (var corner in filter.sharedMesh.vertices)
                    {
                        var world = child.TransformPoint(corner);

                        float away = (world.x - near.x) * (world.x - near.x)
                                     + (world.z - near.z) * (world.z - near.z);

                        if (away >= closest) continue;

                        closest = away;
                        found = world.y;
                    }

                    return closest < float.MaxValue ? found : (float?)null;
                }

                var deeper = Sheet(child, near);
                if (deeper != null) return deeper;
            }

            return null;
        }

        /// <summary>
        /// How high the land stands where it meets the sea.
        ///
        /// <b>The number that decides whether a flat sea is possible.</b> A sheet at one
        /// height drowns every shore below it, so the question is not how deep the sea is
        /// - it is how much the land varies at the waterline. A metre or two is a sea
        /// level; ten metres is a country that has to be carved before it can have one.
        /// </summary>
        static void Shoreline(LevelMap map, float heightScale, int level,
                              System.Text.StringBuilder note)
        {
            var grid = map.Grid;
            var edge = new List<float>();

            for (int i = 0; i < grid.TileCount; i++)
            {
                if (grid[i] == TerrainType.Water) continue;

                grid.ToCoords(i, out int x, out int y);

                bool wet = false;

                for (int dy = -1; dy <= 1 && !wet; dy++)
                    for (int dx = -1; dx <= 1 && !wet; dx++)
                        if (grid.InBounds(x + dx, y + dy)
                            && grid[grid.ToIndex(x + dx, y + dy)] == TerrainType.Water) wet = true;

                if (wet) edge.Add(grid.Elevation(i) * heightScale);
            }

            if (edge.Count == 0) { note.AppendLine($"[Waterfront]   {level}: no shoreline"); return; }

            edge.Sort();

            note.AppendLine($"[Waterfront]   {level}: shoreline {edge.Count} tiles, "
                            + $"low {edge[0]:0.0}, quarter {edge[edge.Count / 4]:0.0}, "
                            + $"half {edge[edge.Count / 2]:0.0}, "
                            + $"three quarters {edge[edge.Count * 3 / 4]:0.0}, "
                            + $"high {edge[edge.Count - 1]:0.0}");
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
