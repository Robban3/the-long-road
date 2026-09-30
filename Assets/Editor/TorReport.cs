using System.Collections.Generic;
using System.Text;
using TheVeil.App;
using TheVeil.Sim;
using TheVeil.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Whether a mass of rock is standing on anything: `The Veil &gt; Tor Report`.
    ///
    /// <b>Written because the one check that would have caught this is obliged to skip
    /// it.</b> The smoke test asks every prop on the map whether it clears the ground
    /// under it, and it skips anything named Tor_ — rightly, and with the reason written
    /// down: a tor is built in courses and every course above the first stands on the one
    /// below rather than on the ground. A check that cried about those would cry about
    /// nine pieces in ten.
    ///
    /// So no instrument has ever been able to say whether a tor is bedded, and a tor is
    /// the largest thing the decorator builds: twenty to thirty metres of rock, ten to a
    /// level, on two of the ten countries. The fault was found in a photograph of a
    /// waterfall taken to settle a different question.
    ///
    /// The question this asks is the one the exemption is hiding, and it is not "is there
    /// ground under it" — for most pieces there should not be. It is <b>is there anything
    /// under it</b>: ground, or another piece of the same mass whose top reaches its
    /// bottom. A piece with neither is hanging in the air, whatever course it is in.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.TorReport.Run
    /// </summary>
    public static class TorReport
    {
        /// <summary>
        /// How much of a piece may be out over nothing before it is worth saying so.
        ///
        /// Half. A boulder resting on a shelf with a third of itself past the edge is what
        /// rock on a hillside looks like; one that is mostly out over air is a thing the
        /// eye reads as floating whatever is holding the other end of it, and from a low
        /// camera across a slope that is the only thing it reads as.
        /// </summary>
        const float Juts = 0.5f;

        /// <summary>How many points across a piece's footprint the ground is asked at.</summary>
        // Five by five. The pieces are two to eight metres across, so the samples are
        // half a metre to two metres apart, which is finer than the ground is.
        const int Samples = 5;

        /// <summary>How far a piece may hang before it is worth saying so, in metres.</summary>
        // Half a metre. Rock is laid rough and a hand's daylight under one edge of a
        // boulder is how rock lies; half a metre is a gap a person could put an arm into.
        const float Hangs = 0.5f;

        /// <summary>
        /// How far a piece's own box may miss the piece below and still count as carried.
        ///
        /// Boxes are axis-aligned and rock is not, so two pieces that touch along a
        /// diagonal face have boxes that overlap by very little. A metre of slack either
        /// way, which is less than a quarter of the smallest piece a tor is built from.
        /// </summary>
        const float Slack = 1f;

        [MenuItem("The Veil/Tor Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Tor] no LevelRunner in the scene"); return; }

            var sheet = new StringBuilder();
            sheet.AppendLine("[Tor] pieces of rock with nothing under them");

            int levels = 0, hanging = 0, juts = 0;
            float worst = 0f, reach = 0f;
            string where = "nowhere", out_ = "nowhere";

            foreach (int chapter in DifficultyCurve.Dressed)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);
                    var pieces = Pieces(root.transform);

                    if (pieces.Count == 0) { Object.DestroyImmediate(root); continue; }

                    levels++;

                    int loose = 0, jutting = 0;
                    float deepest = 0f, furthest = 0f;
                    Vector3 at = Vector3.zero, over = Vector3.zero;

                    foreach (var piece in pieces)
                    {
                        float gap = Hanging(piece, pieces, map, runner.HeightScale);

                        if (gap > Hangs)
                        {
                            loose++;

                            if (gap > deepest)
                            {
                                deepest = gap;
                                at = piece.Box.center;
                            }
                        }

                        // And how much of it is out over nothing, which is a different
                        // question with a different answer: a piece can be standing on its
                        // own middle and still reach half its width past anything at all.
                        float air = Jutting(piece, pieces, map, runner.HeightScale,
                                            out float drop);

                        if (air < Juts) continue;

                        jutting++;
                        if (drop <= furthest) continue;

                        furthest = drop;
                        over = piece.Box.center;
                    }

                    hanging += loose;
                    juts += jutting;

                    if (deepest > worst)
                    {
                        worst = deepest;
                        where = $"{chapter}-{level} at {at.x:0}, {at.z:0}";
                    }

                    if (furthest > reach)
                    {
                        reach = furthest;
                        out_ = $"{chapter}-{level} at {over.x:0}, {over.z:0}";
                    }

                    sheet.AppendLine($"[Tor] {chapter}-{level}: {pieces.Count} piece(s), "
                                     + (loose == 0
                                        ? "all carried"
                                        : $"{loose} hanging, worst {deepest:0.0} m clear")
                                     + (jutting == 0
                                        ? ", none overhanging"
                                        : $", {jutting} overhanging, worst {furthest:0.0} m"));

                    Object.DestroyImmediate(root);
                }
            }

            sheet.AppendLine($"[Tor] {hanging} piece(s) hanging over {levels} level(s) with rock on "
                             + $"them; worst {worst:0.0} m, {where}");
            sheet.AppendLine($"[Tor] {juts} piece(s) more than {Juts:P0} out over nothing; "
                             + $"worst {reach:0.0} m of daylight, {out_}");

            Debug.Log(sheet.ToString());
        }

        /// <summary>
        /// Everything on one level that is off the ground, exemptions ignored:
        /// `The Veil &gt; Off The Ground`.
        ///
        /// <b>For when a picture and an instrument disagree.</b> The smoke test skips whole
        /// classes of prop by name - a tor's pieces, a shop sign, a banner, the sky, the
        /// water - and every one of those exemptions is right and was paid for. What none
        /// of them can do is answer "then what is that thing hanging over the hillside in
        /// the photograph", because the answer is by construction something the check is
        /// not allowed to mention.
        ///
        /// So this asks the same question of everything, says what it finds by name and
        /// position, and lets a person decide. It is not a check and nothing should be
        /// made to satisfy it: most of what it lists is meant to be where it is.
        /// </summary>
        [MenuItem("The Veil/Off The Ground")]
        public static void OffTheGround()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/PlayLevel.unity", OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.Log("[Air] no LevelRunner in the scene"); return; }

            var root = SmokeTest.Build(runner, Chapter, Level, out var map);
            var found = new List<(string Name, float Gap, Vector3 At, float Size)>();

            Sweep(root.transform, map, runner.HeightScale, found);

            found.Sort((a, b) => b.Gap.CompareTo(a.Gap));

            var sheet = new StringBuilder();
            sheet.AppendLine($"[Air] {Chapter}-{Level}: what is off the ground, tallest gap first");

            for (int i = 0; i < found.Count && i < Listed; i++)
                sheet.AppendLine($"[Air] {found[i].Name,-44} {found[i].Gap,6:0.0} m clear, "
                                 + $"{found[i].Size:0.0} m tall, at {found[i].At.x:0}, {found[i].At.z:0}");

            sheet.AppendLine($"[Air] {found.Count} thing(s) over {Clear:0.0} m clear of the ground.");

            // <b>And a picture of the worst of them, which is the whole point.</b> A list
            // saying a rock is twenty-one metres off the ground does not say whether it is
            // the top of a mass that is twenty-one metres tall or a rock in the sky, and
            // those want opposite answers. Shot level with the thing and from far enough
            // out to see what is under it.
            if (found.Count > 0)
            {
                string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilFalls");
                System.IO.Directory.CreateDirectory(shots);

                var worst = found[0];
                float away = worst.Gap + 30f;

                Shoot(worst.At + new Vector3(away, worst.Gap * 0.4f, -away), worst.At,
                      System.IO.Path.Combine(shots, $"air-{Chapter}-{Level}.png"));

                sheet.AppendLine("[Air] picture of the worst: "
                                 + System.IO.Path.Combine(shots, $"air-{Chapter}-{Level}.png"));
                Debug.Log(sheet.ToString());
                Object.DestroyImmediate(root);
                return;
            }

            Debug.Log(sheet.ToString());
            Object.DestroyImmediate(root);
        }

        /// <summary>The level the drill-down looks at, and how much of it it prints.</summary>
        const int Chapter = 6, Level = 1, Listed = 25;

        /// <summary>How far off the ground a thing has to be to be worth listing, in metres.</summary>
        const float Clear = 2f;

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Air camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.70f, 0.78f);
            camera.farClipPlane = 2000f;

            var texture = new RenderTexture(1100, 1100, 24);
            camera.targetTexture = texture;

            // Twice, the first thrown away. See GroundPhotos.
            camera.Render();
            camera.Render();

            RenderTexture.active = texture;
            var shot = new Texture2D(1100, 1100, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 1100, 1100), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);

            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
        }

        static void Sweep(Transform at, LevelMap map, float heightScale,
                          List<(string, float, Vector3, float)> found)
        {
            foreach (Transform child in at)
            {
                var box = ModelScaling.Measure(child.gameObject);

                if (box.size.y > 0.5f)
                {
                    // The kinder of the two witnesses, as the smoke test asks it: a thing on
                    // a slope is over one of them and not the other.
                    float ground = Mathf.Max(
                        map.Grid.SurfaceElevation(box.center.x, box.center.z) * heightScale,
                        map.Grid.SurfaceElevation(child.position.x, child.position.z) * heightScale);

                    float gap = box.min.y - ground;

                    if (gap > Clear)
                    {
                        found.Add((child.name, gap, box.center, box.size.y));
                        continue;
                    }
                }

                Sweep(child, map, heightScale, found);
            }
        }

        readonly struct Piece
        {
            public readonly Bounds Box;

            public Piece(Bounds box) => Box = box;
        }

        static List<Piece> Pieces(Transform at)
        {
            var found = new List<Piece>();
            Walk(at, found);
            return found;
        }

        static void Walk(Transform at, List<Piece> found)
        {
            foreach (Transform child in at)
            {
                if (child.name.StartsWith(TerrainDecorator.TorPieceName))
                {
                    var box = ModelScaling.Measure(child.gameObject);
                    if (box.size.y > 0.01f) found.Add(new Piece(box));
                    continue;
                }

                Walk(child, found);
            }
        }

        /// <summary>
        /// How much of a piece's footprint has nothing under it, and how far the emptiest
        /// point of it is above whatever is down there.
        ///
        /// <b>A different question from whether it is carried.</b> A piece is carried if
        /// anything at all holds it up anywhere; it overhangs if most of it does not. The
        /// first is about whether the decorator put it somewhere impossible and the second
        /// is about whether a player looking across a hillside sees rock floating - and a
        /// mass can be perfectly carried, every piece standing on the one below, and still
        /// lean its whole shoulder out over a valley.
        /// </summary>
        static float Jutting(Piece piece, List<Piece> all, LevelMap map, float heightScale,
                             out float drop)
        {
            drop = 0f;

            int empty = 0, asked = 0;

            for (int ix = 0; ix < Samples; ix++)
                for (int iz = 0; iz < Samples; iz++)
                {
                    float x = Mathf.Lerp(piece.Box.min.x, piece.Box.max.x,
                                         (ix + 0.5f) / Samples);
                    float z = Mathf.Lerp(piece.Box.min.z, piece.Box.max.z,
                                         (iz + 0.5f) / Samples);

                    asked++;

                    float under = map.Grid.SurfaceElevation(x, z) * heightScale;

                    // <b>Rock at this level here, not rock whose top stops below me.</b>
                    // The first version of this asked for a piece whose top was under the
                    // sample and no higher than it, which is the test for something the
                    // piece is resting on - and it is the wrong test for whether there is
                    // anything there. A piece standing low among taller neighbours has rock
                    // all round it at its own height and none of it stops below its foot,
                    // so every one of those neighbours was rejected and the ground twenty
                    // metres down was taken as the answer: a third of every mass in the
                    // mountains reported as leaning over air, with 27 m under the worst of
                    // it, and every one of them bedded in the middle of a rock pile.
                    //
                    // Measuring the wrong thing and then changing the world until the
                    // number improves is the expensive way to be wrong. What is being asked
                    // is whether there is stone under this point at all.
                    foreach (var other in all)
                    {
                        if (other.Box == piece.Box) continue;
                        if (other.Box.min.y > piece.Box.min.y + Slack) continue;
                        if (other.Box.max.y < piece.Box.min.y - Slack) continue;
                        if (x < other.Box.min.x || x > other.Box.max.x) continue;
                        if (z < other.Box.min.z || z > other.Box.max.z) continue;

                        under = piece.Box.min.y;
                        break;
                    }

                    float gap = piece.Box.min.y - under;
                    if (gap <= Hangs) continue;

                    empty++;
                    if (gap > drop) drop = gap;
                }

            return asked == 0 ? 0f : empty / (float)asked;
        }

        /// <summary>
        /// How far a piece hangs clear of everything that could be holding it up.
        ///
        /// The ground is sampled the way the smoke test samples it, under the pivot and
        /// under the body, and the kinder of the two wins: a piece on a slope is over one
        /// of them and not the other, and being over one is not hanging.
        /// </summary>
        static float Hanging(Piece piece, List<Piece> all, LevelMap map, float heightScale)
        {
            float ground = Mathf.Max(
                map.Grid.SurfaceElevation(piece.Box.center.x, piece.Box.center.z) * heightScale,
                map.Grid.SurfaceElevation(piece.Box.min.x, piece.Box.min.z) * heightScale);

            float gap = piece.Box.min.y - ground;
            if (gap <= Hangs) return 0f;

            // And whatever else is under it. A piece is carried by anything whose top
            // reaches its bottom and whose footprint is under it.
            foreach (var other in all)
            {
                if (other.Box == piece.Box) continue;
                if (other.Box.max.y < piece.Box.min.y - Slack) continue;
                if (other.Box.min.y >= piece.Box.min.y) continue;

                if (other.Box.max.x < piece.Box.min.x - Slack) continue;
                if (other.Box.min.x > piece.Box.max.x + Slack) continue;
                if (other.Box.max.z < piece.Box.min.z - Slack) continue;
                if (other.Box.min.z > piece.Box.max.z + Slack) continue;

                return 0f;
            }

            return gap;
        }
    }
}
