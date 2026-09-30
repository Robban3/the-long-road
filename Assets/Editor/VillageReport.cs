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
    /// A close look at every village: `The Veil > Village Report`.
    ///
    /// The chapter sheets the smoke test draws are ten levels wide and a village is
    /// forty metres across in one of them, which is enough to see that something is
    /// standing there and not enough to see whether it is a village. Two houses growing
    /// out of each other went unnoticed on a sheet and were obvious the moment one was
    /// rendered on its own.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.VillageReport.Run
    /// </summary>
    public static class VillageReport
    {
        /// <summary>What BuildingBuilder calls the things it assembles.</summary>
        static readonly System.Collections.Generic.HashSet<string> Hosts =
            new System.Collections.Generic.HashSet<string>
            {
                "House", "Tower", "Ruin", "Castle", "Shed"
            };

        [MenuItem("The Veil/Village Report")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity",
                                                     OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) return;

            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(dir);

            // The built chapters and the coast, which is chapter one hundred and is not
            // one of them. Every instrument here counted to BuiltChapters and so none of
            // them ever looked at the last chapter of the game.
            // Every dressed chapter, from the one place that knows which they are.
            var chapters = DifficultyCurve.Dressed;

            foreach (int chapter in chapters)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    if (!Settlements.HasVillage(chapter, level)) continue;

                    var map = LevelMaps.For(chapter, level);
                    int site = Settlements.Site(map, chapter, level);
                    if (site < 0) continue;

                    var root = SmokeTest.Build(runner, chapter, level, out map);

                    map.Grid.ToCoords(site, out int vx, out int vy);
                    var at = new Vector3(vx * TileGrid.TileSize, 0f, vy * TileGrid.TileSize);
                    at.y = map.Grid.SurfaceElevation(at.x, at.z) * runner.HeightScale;

                    // Low and close, which is the only angle that shows whether the
                    // buildings stand apart. From above they never overlap.
                    Shoot(at + new Vector3(-44f, 22f, -44f), at + Vector3.up * 3f,
                          System.IO.Path.Combine(dir, $"village-{chapter}-{level}.png"));

                    // And the whole place, to see whether it reads as somewhere.
                    Shoot(at + new Vector3(-30f, 58f, -62f), at,
                          System.IO.Path.Combine(dir, $"village-{chapter}-{level}-above.png"));

                    // And whether any two of them are standing in each other, which is a
                    // thing to measure and not to judge from a picture. Every building is
                    // taken as the box its own renderers fill; two boxes that share space
                    // are two buildings sharing space.
                    var solids =
                        new System.Collections.Generic.List<(string Name, string Kind, Bounds Box)>();

                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        var part = renderer.transform;
                        Transform building = null;

                        // Up to the thing the builder made: one of its hosts, or the prop
                        // itself where it was placed whole.
                        //
                        // <b>All five hosts, not just the house.</b> BuildingBuilder makes
                        // a House, a Tower, a Ruin, a Castle and a Shed, and this knew
                        // about one of them - so the other four were never found and the
                        // climb stopped at the topmost SM_Bld_ piece inside them instead.
                        // A tower is a base, a shaft and a top standing one on another by
                        // construction, which is exactly what this report calls two
                        // buildings standing in each other: sixteen pairs on 1-3, twenty-
                        // three on 2-3, every one of them a tower being read course by
                        // course. A check that cries about what it is looking at is the
                        // kind nobody reads, and it was hiding whatever is really stacked.
                        while (part != null)
                        {
                            if (Hosts.Contains(part.name) || part.name.StartsWith("SM_Bld_"))
                                building = part;

                            part = part.parent;
                        }

                        if (building == null) continue;

                        string id = building.GetInstanceID().ToString();
                        int seen = solids.FindIndex(s => s.Name == id);

                        if (seen >= 0)
                        {
                            var box = solids[seen].Box;
                            box.Encapsulate(renderer.bounds);
                            solids[seen] = (solids[seen].Name, solids[seen].Kind, box);
                        }
                        else solids.Add((id, building.name, renderer.bounds));
                    }

                    int clashes = 0;
                    float worst = 0f;

                    for (int a = 0; a < solids.Count; a++)
                        for (int b = a + 1; b < solids.Count; b++)
                        {
                            var one = solids[a].Box;
                            var two = solids[b].Box;

                            // Flat overlap only: two buildings on the same ground, not a
                            // chimney measured against the roof under it.
                            float overlapX = Mathf.Min(one.max.x, two.max.x) - Mathf.Max(one.min.x, two.min.x);
                            float overlapZ = Mathf.Min(one.max.z, two.max.z) - Mathf.Max(one.min.z, two.min.z);

                            if (overlapX <= 0f || overlapZ <= 0f) continue;

                            clashes++;
                            worst = Mathf.Max(worst, Mathf.Min(overlapX, overlapZ));

                            // Stacked is the worse case and a different fault: one
                            // building's floor standing at another's roof, which is what
                            // was reported — two and three high, roof meeting floor.
                            float over = two.min.y - one.max.y;
                            float under = one.min.y - two.max.y;

                            if (System.Math.Abs(over) < 2f || System.Math.Abs(under) < 2f)
                                Debug.Log("[Village] STACKED: one building at y "
                                          + one.min.y.ToString("0.0") + ".." + one.max.y.ToString("0.0")
                                          + ", the other at " + two.min.y.ToString("0.0") + ".."
                                          + two.max.y.ToString("0.0") + ", sharing "
                                          + overlapX.ToString("0.0") + " x " + overlapZ.ToString("0.0")
                                          + " m of ground.");
                        }

                    Debug.Log($"[Village] {chapter}-{level}: {solids.Count} building(s), "
                              + $"{clashes} pair(s) standing in each other, worst {worst:0.0} m of shared ground.");

                    // Whether this village could have a mill at all.
                    //
                    // <b>The census says the winter and the coast never build one, and a
                    // count cannot tell "never asked" from "asked and there was no
                    // water".</b> A mill goes on the nearest water with a dry bank within
                    // ten tiles of the village site (TerrainDecorator.MillReach), and a
                    // village site is chosen for level plains near a road, which has
                    // nothing to do with rivers. So the question is whether the water was
                    // there, and that is a fact about the map rather than about the dice.
                    int wet = 0;

                    for (int dy = -TerrainDecorator.MillReach; dy <= TerrainDecorator.MillReach; dy++)
                        for (int dx = -TerrainDecorator.MillReach; dx <= TerrainDecorator.MillReach; dx++)
                        {
                            int wx = vx + dx, wy = vy + dy;
                            if (!map.Grid.InBounds(wx, wy)) continue;
                            if (map.Grid[wx, wy] == TerrainType.Water) wet++;
                        }

                    bool milled = false;
                    foreach (var piece in root.GetComponentsInChildren<MeshRenderer>(false))
                        if (piece.gameObject.name.Contains("WaterWheel")) { milled = true; break; }

                    Debug.Log($"[Village] {chapter}-{level}: {wet} water tile(s) within "
                              + $"{TerrainDecorator.MillReach} of the site, mill built: {milled}.");

                    // And the furniture, by name and height.
                    //
                    // <b>A picture of a village does not say how big a bucket is.</b> The
                    // yard set was fitted to one height for every model in it, and the
                    // models run from a 0.20 m bucket to a 3.84 m scarecrow - so the bucket
                    // came out nine times its own size and nothing on a sheet or in a
                    // photograph said so, because a large bucket beside a large house looks
                    // like a village. What says so is the number. Anything here taller than
                    // a man is either a scarecrow, a well or a fault.
                    var yard = new System.Collections.Generic.Dictionary<string, float>();

                    foreach (var prop in root.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        if (Vector3.Distance(prop.bounds.center, at) > Settlements.Yard * TileGrid.TileSize * 2f)
                            continue;

                        string name = prop.transform.root == prop.transform
                                    ? prop.name : Top(prop.transform).name;

                        if (!name.StartsWith("SM_Prop_")) continue;

                        float height = prop.bounds.size.y;
                        if (!yard.TryGetValue(name, out float tallest) || height > tallest)
                            yard[name] = height;
                    }

                    foreach (var pair in yard)
                        Debug.Log($"[Village]   {pair.Key}: {pair.Value:0.00} m tall"
                                  + (pair.Value > 2.5f ? "  <-- taller than a man" : ""));

                    // And each building on its own. A house is a foundation, a room,
                    // perhaps an upper room, and a roof — four pieces and about seven
                    // metres. Anything twice that, or carrying two roofs, is one building
                    // that has been stacked rather than two buildings that overlap, and
                    // the pairwise measurement above cannot see it at all.
                    //
                    // <b>Twice the height it is actually built at, which is not seven
                    // metres.</b> A house is asked for at HouseHeight and then multiplied
                    // by the run's landmark scale, because a landmark is sized against
                    // being read from map height and not against a man - so the houses come
                    // out at eleven metres and every village in the game was reported as
                    // having four to seven buildings stacked in it. A report that cries
                    // wolf on every village is a report nobody reads, and this one has been
                    // crying since the landmark scale was put in.
                    float built = TerrainDecorator.HouseHeight * runner.LandmarkScale;

                    foreach (var (id, kind, box) in solids)
                    {
                        if (box.size.y < built * 2f) continue;

                        // <b>Except the two that are meant to be tall.</b> This measures
                        // whether a house has been stacked into a tower, and it began
                        // reporting the castle the moment the report learned to group a
                        // building by its host rather than by its pieces: a keep is 22 m
                        // and a watchtower 15, both on purpose, and both above twice the
                        // height a house is built at. The check was right about houses
                        // before and after; what changed is that it could finally see the
                        // things that are not houses.
                        if (kind == "Castle" || kind == "Tower") continue;

                        Debug.Log($"[Village] TALL: a building {box.size.y:0.0} m high at "
                                  + $"{box.center.x:0},{box.center.z:0}.");
                    }

                    int roofs = 0, rooms = 0;
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        if (renderer.gameObject.name.Contains("RoomTop")) roofs++;
                        else if (renderer.gameObject.name.Contains("House_Room")) rooms++;
                    }

                    Debug.Log($"[Village] {chapter}-{level}: {roofs} roof(s) over {rooms} room(s) "
                              + $"in {solids.Count} building(s).");

                    int parts = 0;
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
                        if (renderer.gameObject.name.StartsWith("SM_Bld_")) parts++;

                    Debug.Log($"[Village] {chapter}-{level}: site {vx},{vy}, {parts} building part(s).");

                    Object.DestroyImmediate(root);
                }
            }

            Debug.Log($"[Village] pictures in {dir}");
            EditorSceneManager.CloseScene(scene, false);
        }

        /// <summary>The outermost object of a placed prop, which is what carries its name.</summary>
        static Transform Top(Transform part)
        {
            while (part.parent != null && part.parent.name != "Props"
                   && !part.parent.name.StartsWith("Level")) part = part.parent;

            return part;
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Village camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 50f;
            camera.farClipPlane = 900f;

            var rt = new RenderTexture(1200, 760, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1200, 760, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1200, 760), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
