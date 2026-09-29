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

            int levels = 0, hanging = 0;
            float worst = 0f;
            string where = "nowhere";

            foreach (int chapter in DifficultyCurve.Dressed)
            {
                for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
                {
                    var root = SmokeTest.Build(runner, chapter, level, out var map);
                    var pieces = Pieces(root.transform);

                    if (pieces.Count == 0) { Object.DestroyImmediate(root); continue; }

                    levels++;

                    int loose = 0;
                    float deepest = 0f;
                    Vector3 at = Vector3.zero;

                    foreach (var piece in pieces)
                    {
                        float gap = Hanging(piece, pieces, map, runner.HeightScale);
                        if (gap <= Hangs) continue;

                        loose++;
                        if (gap <= deepest) continue;

                        deepest = gap;
                        at = piece.Box.center;
                    }

                    hanging += loose;

                    if (deepest > worst)
                    {
                        worst = deepest;
                        where = $"{chapter}-{level} at {at.x:0}, {at.z:0}";
                    }

                    sheet.AppendLine($"[Tor] {chapter}-{level}: {pieces.Count} piece(s), "
                                     + (loose == 0
                                        ? "all of them carried"
                                        : $"{loose} hanging, worst {deepest:0.0} m clear"));

                    Object.DestroyImmediate(root);
                }
            }

            sheet.AppendLine($"[Tor] {hanging} piece(s) hanging over {levels} level(s) with rock on "
                             + $"them; worst {worst:0.0} m, {where}");

            Debug.Log(sheet.ToString());
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
