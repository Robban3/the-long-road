using System.Collections.Generic;
using System.Text;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEditor;
using UnityEngine;
using TheVeil.View;

namespace TheVeil.Editor
{
    /// <summary>
    /// How square the caravan meets the water, measured: `The Veil > Bridge Report`.
    ///
    /// <b>Written because the same fault has been reported a dozen times and answered
    /// three times by eye.</b> "The caravan comes onto the bridge at an angle" is a
    /// statement about a number nobody had ever produced, so every attempt at it was an
    /// argument about a picture — and two of those attempts moved the bridge, which was
    /// not what was wrong and had to be reverted both times.
    ///
    /// Two numbers per crossing, and they are different faults:
    ///
    ///   * <b>Entry</b> — the angle between the way the route is heading as it puts its
    ///     first tile on the water and the way the ford runs. Zero is square on. This is
    ///     what a wheel on the edge of the deck looks like.
    ///   * <b>Off-row</b> — how far, in tiles, the approach strays from the row the ford
    ///     is on over the run-up behind it. A route can enter square and still have been
    ///     swinging onto that line a tile earlier, which from the ground is a column of
    ///     wagons still coming round the turn while the lead one is already on timber.
    ///
    /// Reported for the drawn route and for the squared one side by side, because
    /// Crossings.Square exists to fix exactly this and the question is how much of it it
    /// actually fixes.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.BridgeReport.Run
    /// </summary>
    public static class BridgeReport
    {
        /// <summary>Chapters measured. Three is the span the other reports cover.</summary>
        const int Chapters = 3;

        /// <summary>Tiles of approach read back from the water. Matches Crossings.RunUp.</summary>
        static int RunUp => Crossings.RunUp;

        [MenuItem("The Veil/Bridge Report")]
        public static void Run()
        {
            var sheet = new StringBuilder();
            sheet.AppendLine("[Bridge] how square the caravan meets the water");
            sheet.AppendLine("[Bridge] level corridor  bridged  drawn entry/off-row  squared entry/off-row");

            float drawnEntry = 0f, squaredEntry = 0f;
            float drawnStray = 0f, squaredStray = 0f;
            float drift = 0f, worstDrift = 0f;
            var spread = new int[Bands.Length + 1];
            int crossings = 0, crooked = 0, torn = 0, relaid = 0;

            for (int chapter = 1; chapter <= Chapters; chapter++)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var map = LevelMaps.For(chapter, level);
                    int bridged = TheVeil.View.TerrainDecorator.BridgeTile(map.Grid, map.Seed);

                    foreach (var corridor in map.Corridors)
                    {
                        var drawn = corridor.Tiles;
                        var squared = Crossings.Square(map.Grid, drawn);

                        // <b>The gate, before any of the numbers below mean anything.</b>
                        //
                        // This rebuilds a stretch of the route rather than nudging it, so
                        // the first question is not how square the crossing is but whether
                        // what came out is still a road: every tile a single step from the
                        // last and every one of them drivable. A crossing taken at an
                        // angle is a fault; a route that steps through a cliff to avoid
                        // one is a bug, and it would be a bug the caravan walks into at
                        // the one moment the camera is on it.
                        if (!RouteCheck.Walkable(map.Grid, squared, out int broken))
                        {
                            sheet.AppendLine($"[Bridge] {chapter}-{level} {corridor.Kind}: "
                                             + $"TORN at tile {broken} "
                                             + $"({(broken >= 0 && broken < map.Grid.TileCount ? map.Grid[broken].ToString() : "off the map")})");
                            torn++;
                        }

                        if (squared.Count != drawn.Count) relaid++;

                        var before = Meetings(map.Grid, drawn, bridged);
                        var after = Meetings(map.Grid, squared, bridged);

                        for (int i = 0; i < before.Count && i < after.Count; i++)
                        {
                            crossings++;

                            drawnEntry += before[i].Entry;
                            squaredEntry += after[i].Entry;
                            drawnStray += before[i].Stray;
                            squaredStray += after[i].Stray;

                            drift += after[i].Drift;
                            worstDrift = Mathf.Max(worstDrift, after[i].Drift);

                            int band = 0;
                            while (band < Bands.Length && after[i].Drift > Bands[band]) band++;
                            spread[band]++;

                            if (after[i].Entry > Square || after[i].Stray > 1f) crooked++;

                            sheet.AppendLine(
                                $"[Bridge] {chapter}-{level,-2} {corridor.Kind,-5} "
                                + $"{(before[i].OnTheBridge ? "  bridge" : "       ")}  "
                                + $"{before[i].Entry,5:0}° {before[i].Stray,4:0.0}      "
                                + $"{after[i].Entry,5:0}° {after[i].Stray,4:0.0}");
                        }
                    }
                }
            }

            if (crossings == 0) { Debug.Log("[Bridge] no crossings on any route"); return; }

            sheet.AppendLine($"[Bridge] {crossings} crossings over {Chapters} chapters");
            sheet.AppendLine($"[Bridge] drawn:   entry {drawnEntry / crossings:0.0}° "
                             + $"off-row {drawnStray / crossings:0.00} tiles");
            sheet.AppendLine($"[Bridge] squared: entry {squaredEntry / crossings:0.0}° "
                             + $"off-row {squaredStray / crossings:0.00} tiles");
            sheet.AppendLine($"[Bridge] {crooked} of {crossings} still crooked "
                             + $"(over {Square:0}° or more than a tile off the row)");
            sheet.AppendLine($"[Bridge] {relaid} routes changed length, {torn} torn");
            sheet.AppendLine($"[Bridge] drift across the deck: {drift / crossings:0.0} m average, "
                             + $"{worstDrift:0.0} m worst — deck is "
                             + $"{TheVeil.View.TerrainDecorator.FordDeck:0.0} m wide");

            // <b>The distribution, because an average and a worst case do not size a
            // bridge.</b> Half the deck less half a wagon is how far the column may drift
            // before a wheel is over the edge; what matters is how many crossings are past
            // that line, not how far the very worst one is. Building to the worst case is
            // how a seven-metre deck becomes a viaduct to carry four crossings in a
            // hundred.
            sheet.Append("[Bridge] drift:");
            for (int band = 0; band < Bands.Length; band++)
                sheet.Append($"  under {Bands[band]:0.0} m: {spread[band]}");

            sheet.AppendLine($"  over {Bands[Bands.Length - 1]:0.0} m: {spread[Bands.Length]}");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// The bridge itself, photographed on the level it stands on:
        /// `The Veil > Bridge Photos`.
        ///
        /// <b>Because a deck is a number until somebody looks at it.</b> The measurements
        /// above say how wide the roadway has to be to keep a wheel on it; they cannot say
        /// whether a roadway that wide reads as a bridge or as a viaduct somebody dropped
        /// on a stream. Widening is a view change with no cost to the simulation, which
        /// makes it cheap to try and easy to leave too wide.
        /// </summary>
        [MenuItem("The Veil/Bridge Photos")]
        public static void Photos()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<TheVeil.App.LevelRunner>();
            if (runner == null) { Debug.Log("[Bridge] no LevelRunner in the scene"); return; }

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilSmoke");
            System.IO.Directory.CreateDirectory(shots);

            foreach (int level in new[] { 4, 7 })
            {
                var root = SmokeTest.Build(runner, 1, level, out _);

                var bridge = Find(root.transform, "Bridge");
                if (bridge == null)
                {
                    Debug.Log($"[Bridge] 1-{level}: nothing bridged");
                    Object.DestroyImmediate(root);
                    continue;
                }

                var box = ModelScaling.Measure(bridge.gameObject);
                float span = Mathf.Max(box.size.x, box.size.z);

                Debug.Log($"[Bridge] 1-{level}: {box.size.x:0.0} x {box.size.y:0.0} "
                          + $"x {box.size.z:0.0} m");

                // From the player's own camera height and slant, which is the only angle
                // the width has to look right from.
                Shoot(box.center + new Vector3(0f, span * 0.9f, -span * 0.9f), box.center,
                      System.IO.Path.Combine(shots, $"bridge-{level}-yard.png"));

                // And along the water, where a deck too wide for its stream shows.
                Shoot(box.center + new Vector3(span * 1.4f, span * 0.25f, 0f), box.center,
                      System.IO.Path.Combine(shots, $"bridge-{level}-side.png"));

                Object.DestroyImmediate(root);
            }

            Debug.Log($"[Bridge] pictures in {shots}");
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name.Contains(name)) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Bridge camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);

            var rt = new RenderTexture(1400, 900, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1400, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>The bands the drift is counted into, in metres.</summary>
        static readonly float[] Bands = { 1f, 2f, 3f, 4f };

        /// <summary>How far off square a crossing may be before it reads as crooked.</summary>
        const float Square = 20f;

        struct Meeting
        {
            public float Entry;
            public float Stray;
            public float Drift;
            public bool OnTheBridge;
        }

        /// <summary>
        /// Every place a route puts a wheel on water, and how square it does it.
        /// </summary>
        static List<Meeting> Meetings(TileGrid grid, IReadOnlyList<int> route, int bridged)
        {
            var found = new List<Meeting>();
            if (route == null) return found;

            for (int i = 0; i < route.Count; i++)
            {
                if (grid[route[i]] != TerrainType.Ford) continue;

                // The whole run of water, so the ford's own axis can be read off it
                // rather than guessed from one tile.
                int from = i;
                int to = i;
                while (to + 1 < route.Count && grid[route[to + 1]] == TerrainType.Ford) to++;

                grid.ToCoords(route[from], out int fx, out int fy);
                grid.ToCoords(route[to], out int tx, out int ty);

                // A run of one tile has no axis of its own; take the step onto it as the
                // crossing direction, which is the same thing the decorator falls back to.
                float axis = from == to
                    ? Heading(grid, route, from - 1, from)
                    : Mathf.Atan2(ty - fy, tx - fx) * Mathf.Rad2Deg;

                var meeting = new Meeting
                {
                    Entry = Between(Heading(grid, route, from - 1, from), axis),
                    Stray = Stray(grid, route, from, fy),
                    Drift = Drift(grid, route, from, to, fy),
                    OnTheBridge = Touches(grid, route, from, to, bridged)
                };

                found.Add(meeting);
                i = to;
            }

            return found;
        }

        /// <summary>The bearing of one step of a route, in degrees.</summary>
        static float Heading(TileGrid grid, IReadOnlyList<int> route, int a, int b)
        {
            if (a < 0 || b >= route.Count) return 0f;

            grid.ToCoords(route[a], out int ax, out int ay);
            grid.ToCoords(route[b], out int bx, out int by);

            return Mathf.Atan2(by - ay, bx - ax) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The angle between two bearings, folded to a quarter turn.
        ///
        /// Folded because a crossing driven backwards is still driven square: the fault
        /// being measured is the wheel off the edge of the deck, and that does not care
        /// which bank the column started on.
        /// </summary>
        static float Between(float a, float b)
        {
            float difference = Mathf.Abs(Mathf.DeltaAngle(a, b));
            return difference > 90f ? 180f - difference : difference;
        }

        /// <summary>
        /// How far across the deck the route drifts while it is on it, in metres.
        ///
        /// <b>The number the bridge has to be built to.</b> The wander behind the water is
        /// a statement about the road and cannot be fixed without moving it; this is a
        /// statement about the bridge, and a bridge is only a model. Measured over the
        /// tiles the deck actually covers — the wet run plus the landing at each end,
        /// which is what TerrainDecorator.Bridge spans — because what happens further back
        /// is off the timber and nobody can see it against a parapet that is not there.
        ///
        /// Half-widths, since the drift is either side of the middle: a deck has to be
        /// twice this plus a wagon to keep the wheels on it.
        /// </summary>
        static float Drift(TileGrid grid, IReadOnlyList<int> route, int from, int to, int row)
        {
            // The deck reaches a landing past the water at each end. See
            // TerrainDecorator.BridgeLanding, which is in metres.
            int landing = Mathf.CeilToInt(TheVeil.View.TerrainDecorator.BridgeLanding
                                          / TileGrid.TileSize);

            float worst = 0f;

            for (int at = from - landing; at <= to + landing; at++)
            {
                if (at < 0 || at >= route.Count) continue;

                grid.ToCoords(route[at], out _, out int y);
                worst = Mathf.Max(worst, Mathf.Abs(y - row) * TileGrid.TileSize);
            }

            return worst;
        }

        /// <summary>
        /// How far the approach strays from the ford's row over the run-up behind it.
        /// </summary>
        static float Stray(TileGrid grid, IReadOnlyList<int> route, int from, int row)
        {
            float worst = 0f;

            for (int back = 1; back <= RunUp; back++)
            {
                int at = from - back;
                if (at < 0) break;

                grid.ToCoords(route[at], out _, out int y);
                worst = Mathf.Max(worst, Mathf.Abs(y - row));
            }

            return worst;
        }

        /// <summary>Whether this crossing is the one the bridge was built on.</summary>
        static bool Touches(TileGrid grid, IReadOnlyList<int> route, int from, int to, int bridged)
        {
            if (bridged < 0) return false;

            grid.ToCoords(bridged, out int bx, out int by);

            for (int i = from; i <= to; i++)
            {
                grid.ToCoords(route[i], out int x, out int y);
                if (Mathf.Abs(x - bx) <= 1 && Mathf.Abs(y - by) <= 1) return true;
            }

            return false;
        }
    }
}
