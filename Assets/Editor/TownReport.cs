using System.Linq;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// What the town level actually offers: `The Veil > Town Report`.
    ///
    /// The town was built to a shape — one way past it outside the walls, one gate in,
    /// two lanes within — and none of that is true because it was intended. It is true
    /// only if the ways through come out that way once the walls are impassable ground,
    /// and the one way to know is to ask the same corridor finder the game asks.
    ///
    /// It also reports the danger on each route, because the shape was asked for with a
    /// difficulty order attached: the way round outside hardest, the two lanes inside
    /// easier. The enemy budget is handed out in inverse proportion to travel cost, so
    /// the fast road gets the enemies — and the inside is fast ground. This says which
    /// way it actually fell rather than which way it was meant to.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.TownReport.Run
    /// </summary>
    public static class TownReport
    {
        [MenuItem("The Veil/Town Report")]
        public static void Run()
        {
            int chapter = Towns.Chapter, level = Towns.Level;

            var map = LevelMaps.For(chapter, level);
            var recipe = LevelMaps.Recipe(chapter, level);


            if (!recipe.Town)
            {
                Debug.Log($"[Town] {chapter}-{level} is not a town level.");
                return;
            }

            var plan = Towns.Layout(map.Grid.Width, map.Grid.Height, map.Seed, map.StartY);

            Debug.Log($"[Town] {chapter}-{level}: walls x {plan.West}..{plan.East}, "
                      + $"y {plan.North}..{plan.South}, gates on row {plan.GateRow}. "
                      + $"Map {map.Grid.Width}x{map.Grid.Height}, seed {map.Seed}.");

            map.Grid.ToCoords(map.StartIndex, out int sx, out int sy);
            map.Grid.ToCoords(map.GoalIndex, out int gx, out int gy);
            Debug.Log($"[Town] start {sx},{sy} goal {gx},{gy}.");

            // The walls are what they claim to be: impassable, gates excepted.
            int walled = 0, leaks = 0;

            for (int y = plan.North; y <= plan.South; y++)
                for (int x = plan.West; x <= plan.East; x++)
                {
                    if (!plan.IsWall(x, y)) continue;

                    walled++;
                    if (map.Grid.IsPassable(x, y)) leaks++;
                }

            Debug.Log($"[Town] {walled} wall tile(s), {leaks} of them walkable "
                      + $"(should be 0). Gates walkable: "
                      + $"west {map.Grid.IsPassable(plan.West, plan.GateRow)}, "
                      + $"east {map.Grid.IsPassable(plan.East, plan.GateRow)}.");

            // What the ground outside the west gate is made of, on the gates. own row.
            var westward = new System.Text.StringBuilder("[Town] row " + plan.GateRow + " west of the wall: ");
            for (int x = 0; x <= plan.West; x++)
                westward.Append(x + ":" + map.Grid[map.Grid.ToIndex(x, plan.GateRow)] + " ");
            Debug.Log(westward.ToString());

            var band = new System.Text.StringBuilder("[Town] passable rows in the west band: ");
            for (int y = 0; y < map.Grid.Height; y++)
                if (map.Grid.IsPassable(0, y) || map.Grid.IsPassable(1, y) || map.Grid.IsPassable(2, y))
                    band.Append(y + " ");
            Debug.Log(band.ToString());

            // And the ways through, counted the way the game counts them.
            foreach (var corridor in map.Corridors)
            {
                int inside = corridor.Tiles.Count(t =>
                {
                    map.Grid.ToCoords(t, out int x, out int y);
                    return plan.Holds(x, y);
                });

                bool throughGate = corridor.Tiles.Contains(plan.WestGate(map.Grid))
                                || corridor.Tiles.Contains(plan.EastGate(map.Grid));

                // Which lane, if it is inside at all: north of the block or south of it.
                int north = corridor.Tiles.Count(t =>
                {
                    map.Grid.ToCoords(t, out int x, out int y);
                    return plan.Holds(x, y) && y < plan.GateRow;
                });

                int south = corridor.Tiles.Count(t =>
                {
                    map.Grid.ToCoords(t, out int x, out int y);
                    return plan.Holds(x, y) && y > plan.GateRow;
                });

                Debug.Log($"[Town] {corridor.Kind}: {corridor.Tiles.Count} tiles, "
                          + $"cost {corridor.TravelCost:0.0}, ambush {corridor.AmbushExposure:0.00}, "
                          + $"{inside} tile(s) inside the walls ({north} north lane, {south} south lane), "
                          + $"through a gate: {throughGate}.");
            }

            // The danger each route actually meets. Counted within a few tiles of the
            // line rather than on it: a group watches the ground around it, and one
            // standing beside the road meets the caravan exactly as surely as one
            // standing in it. Counted on the line alone, the road through the town came
            // back as nought groups, which was a measuring fault and not a quiet level.
            const int Reach = 3;

            foreach (var corridor in map.Corridors)
            {
                int points = 0, groups = 0;

                foreach (var spawn in map.Encounters.Enemies)
                {
                    map.Grid.ToCoords(spawn.Tile, out int ex, out int ey);

                    bool met = corridor.Tiles.Any(t =>
                    {
                        map.Grid.ToCoords(t, out int x, out int y);
                        return System.Math.Abs(x - ex) <= Reach && System.Math.Abs(y - ey) <= Reach;
                    });

                    if (!met) continue;

                    groups++;
                    points += EnemyTable.Points(spawn.Kind);
                }

                Debug.Log($"[Town] {corridor.Kind}: {groups} group(s) within {Reach} tiles, {points} point(s).");
            }

            // And what it looks like. Three views, because a town is the one thing on a
            // map that has an inside: the road coming up to the gate, the street between
            // the walls, and the whole circuit from above.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<TheVeil.App.LevelRunner>();
            if (runner == null) return;

            var root = SmokeTest.Build(runner, chapter, level, out map);

            // What is actually standing in it, counted rather than looked for in a picture.
            int carts = 0, hay = 0, wells = 0, barrels = 0, crates = 0, houses = 0, walls = 0;

            foreach (var piece in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                string name = piece.gameObject.name;

                if (name.Contains("CartHay")) hay++;
                else if (name.Contains("Cart")) carts++;
                else if (name.Contains("Well")) wells++;
                else if (name.Contains("Barrel")) barrels++;
                else if (name.Contains("Crate")) crates++;
                else if (name.Contains("Castle_Wall")) walls++;
                else if (name.StartsWith("SM_Bld_House")) houses++;
            }

            Debug.Log($"[Town] standing in it: {houses} house piece(s), {walls} wall length(s), "
                      + $"{carts} cart(s), {hay} hay load(s), {wells} well(s), "
                      + $"{barrels} barrel(s), {crates} crate(s).");

            // And whether any tree is planted in a house, which is a question about the
            // trunk and not about the crown.
            //
            // Measured the wrong way first: tree box against house box, in all three axes.
            // That flags a tree standing beside a house with its branches over the roof,
            // which is what a tree beside a house does. What is wrong is a trunk inside
            // the walls, so the trunk is what is asked about.
            var houseBoxes = new System.Collections.Generic.List<Bounds>();
            var trunks = new System.Collections.Generic.List<Vector3>();

            foreach (var piece in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                string name = piece.gameObject.name;

                if (name.Contains("Tree_") || name.Contains("Willow")) trunks.Add(piece.transform.position);
                else if (name.StartsWith("SM_Bld_House")) houseBoxes.Add(piece.bounds);
            }

            int through = 0;

            foreach (var trunk in trunks)
                foreach (var box in houseBoxes)
                {
                    if (trunk.x <= box.min.x + 0.4f || trunk.x >= box.max.x - 0.4f) continue;
                    if (trunk.z <= box.min.z + 0.4f || trunk.z >= box.max.z - 0.4f) continue;

                    through++;
                    break;
                }

            Debug.Log($"[Town] {trunks.Count} tree(s), {through} of them planted inside a house.");


            // And how far the buildings lean over ground somebody may drive on.
            //
            // A house stands on impassable ground, so no route crosses its tile — and the
            // house is wider than its tile. Measured at seven and a half metres on a
            // four-metre grid, which is nearly two metres of gable over the street on each
            // side, and the caravan drove through the walls on every way but the middle of
            // the main street. This counts the ones that reach a walkable tile at all.
            int leaning = 0, built = 0;

            foreach (var piece in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!piece.gameObject.name.StartsWith("SM_Bld_House")) continue;

                built++;

                // Shrunk by half a metre before asking, because a front wall standing
                // exactly on the plot line touches the street tile without being in it,
                // and that is where a town house is supposed to stand.
                var box = piece.bounds;
                box.Expand(-1f);

                int x0 = Mathf.FloorToInt(box.min.x / TileGrid.TileSize);
                int x1 = Mathf.FloorToInt(box.max.x / TileGrid.TileSize);
                int z0 = Mathf.FloorToInt(box.min.z / TileGrid.TileSize);
                int z1 = Mathf.FloorToInt(box.max.z / TileGrid.TileSize);

                bool reaches = false;

                for (int ty = z0; ty <= z1 && !reaches; ty++)
                    for (int tx = x0; tx <= x1 && !reaches; tx++)
                        if (map.Grid.InBounds(tx, ty) && map.Grid.IsPassable(tx, ty)) reaches = true;

                if (reaches) leaning++;
            }

            Debug.Log($"[Town] {built} house(s), {leaning} of them reaching over walkable ground.");

            float tile = TileGrid.TileSize;
            float middleX = (plan.West + plan.East) * 0.5f * tile;
            float middleZ = (plan.North + plan.South) * 0.5f * tile;
            float row = plan.GateRow * tile;
            float ground = map.Grid.SurfaceElevation(middleX, middleZ) * runner.HeightScale;

            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(dir);

            // Up to the west gate, from the road, at the height a wagon driver sits.
            Shoot(runner, new Vector3(plan.West * tile - 46f, ground + 11f, row - 20f),
                  new Vector3(plan.West * tile, ground + 5f, row),
                  System.IO.Path.Combine(dir, "town-gate.png"));

            // Down the street inside, from just within the west gate.
            Shoot(runner, new Vector3(plan.West * tile + 10f, ground + 9f, row - 2f),
                  new Vector3(plan.East * tile, ground + 4f, row),
                  System.IO.Path.Combine(dir, "town-street.png"));

            // And the whole circuit, from the south so the map is behind it. High enough
            // for all of it: the town is the level now, a quarter of a kilometre across.
            Shoot(runner, new Vector3(middleX - 50f, ground + 200f, middleZ + 235f),
                  new Vector3(middleX, ground, middleZ),
                  System.IO.Path.Combine(dir, "town-above.png"));

            Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, false);

            Debug.Log($"[Town] pictures in {dir}");
        }

        /// <summary>One view of the town, taken with the run's own camera settings.</summary>
        static void Shoot(TheVeil.App.LevelRunner runner, Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Town camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 52f;
            camera.farClipPlane = 1400f;

            // No fog. It is left on in the scene by whichever country was built last, and
            // the fen's fog over a forest town two hundred metres up washed the whole
            // picture to a pale grey — which looked like the town being badly lit rather
            // than like a camera carrying somebody else's weather.
            bool was = RenderSettings.fog;
            RenderSettings.fog = false;

            var rt = new RenderTexture(1200, 760, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1200, 760, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1200, 760), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            RenderSettings.fog = was;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}

