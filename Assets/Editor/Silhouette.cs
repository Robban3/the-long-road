using System.Collections.Generic;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// How much of one set of renderers' outline another set still covers.
    ///
    /// <b>Written because every budget line in this project counts what is drawn and none
    /// of them can see what it looks like.</b> `FarLods` cut a meadow tree to 250 faces,
    /// every sheet reported the saving correctly, and what the game drew past ninety-five
    /// metres was a handful of leaves floating where a tree should be - no trunk, no
    /// canopy, nothing a person would call a tree. A Synty canopy is a few thousand
    /// separate leaf cards and a quadric decimator can only delete whole leaves; it cannot
    /// make a cheaper leaf. The trunk is a small share of the face count, so a budget split
    /// in proportion to faces hands it almost nothing and it collapses to splinters. The
    /// faces came down exactly as asked. The tree left.
    ///
    /// So this is the missing measurement: the thing a photograph told, in a number a pass
    /// can act on. The outline is what survives at distance - at seventeen pixels a tree is
    /// its silhouette and nothing else - so the question is what share of the original's
    /// outline the replacement still fills.
    ///
    /// <b>Rasterised here rather than rendered.</b> A camera needs a scene, and this is
    /// asked in the middle of editing a prefab's contents in a preview scene where a
    /// camera of ours is a fight with the editor and a different answer on every Unity
    /// version. Projecting the triangles onto a small grid and filling them is thirty
    /// lines, runs headless, gives the same answer every time, and can be checked against
    /// a shape whose answer is known - which <see cref="Tests"/> does with a cube.
    ///
    /// It does not look at textures. A leaf card is a quad that is mostly leaf, so filling
    /// the quad is near enough; a model whose outline is painted on by alpha rather than
    /// cut into the mesh would read as covered here and this would not catch it.
    /// </summary>
    public static class Silhouette
    {
        /// <summary>How many pixels across the outline is drawn, for the comparison.</summary>
        // Sixty-four. This asks whether the shape survived, not how it looks on a phone -
        // the pixel size is the camera's business and changes with every copy in the
        // country, while the shape is a property of the mesh. Small enough that a model of
        // 25 000 faces costs nothing to fill, large enough that a trunk is several pixels
        // wide where a leaf is one.
        internal const int Grid = 64;

        /// <summary>
        /// The directions the outline is taken from.
        ///
        /// Four around the model and level with it, because a tree is seen from the side in
        /// this game and never from above. Four and not one, since a decimation can keep a
        /// shape from the front and lose it from the side, and the cheapest of the four is
        /// what the rule is asked about rather than the average.
        ///
        /// <b>Level, although the play camera looks down about fourteen degrees, and that
        /// was not a detail.</b> Tilted, a crown nine metres deep smears two metres down
        /// the picture and covers the band its own trunk stands in - so a tree with no
        /// trunk left scored 0,86 and the test written to catch exactly that went green on
        /// the wrong reasoning. The question here is whether the shape survived, which is
        /// a property of the mesh; how it is looked at is the camera's business and the
        /// camera has its own instrument.
        /// </summary>
        static readonly Vector3[] Eyes =
        {
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
            new Vector3(-1f, 0f, 0f),
        };

        /// <summary>How many bands of height the outline is judged in.</summary>
        // <b>Four, because a trunk is a tenth of a tree's outline by area and all of it by
        // meaning.</b> The first version of this compared whole outlines and answered 0,88
        // for a meadow tree whose trunk had been eaten entirely - a crown floating over
        // nothing, photographed and looked at. A thin upright column is a few per cent of
        // the pixels a crown fills, so no comparison of areas can see it go. Split by
        // height and the band the trunk stands in answers for itself.
        const int Bands = 4;

        /// <summary>
        /// The share of <paramref name="whole"/>'s outline that <paramref name="cut"/>
        /// still fills, in the worst band of height from the worst of four directions,
        /// between 0 and 1.
        ///
        /// Not an area against an area: the pixels counted are those the original fills
        /// and the replacement fills as well, over those the original fills. A replacement
        /// that is bigger somewhere else scores nothing for it, which is what "still a
        /// tree" means and what a ratio of two areas would miss.
        ///
        /// A band holding less than a hundredth of the original's pixels is passed over -
        /// the tip of a spire or a few stray leaves above the crown are not what this is
        /// asking about, and a handful of pixels is a ratio that swings on one of them.
        ///
        /// Returns 1 when there is nothing to compare, because a model with no outline is
        /// not this rule's business and a nought here would refuse everything.
        /// </summary>
        public static float Kept(IList<Renderer> cut, IList<Renderer> whole, Transform root)
        {
            if (cut == null || whole == null || root == null) return 1f;

            // <b>Skinned renderers are snapshotted first, because their meshes are not
            // where their meshes are.</b> A skinned mesh's vertices live in a space the
            // bones undo, so putting them through the renderer's own transform - which is
            // what everything below does - draws the model anywhere but where it is
            // drawn. For a year that meant this measurement returned nought for every
            // model in the cast, and it did no harm only because the rule that read it is
            // consulted for rungs that start large and the cast's rungs do not. The
            // moment a rule read it everywhere, it refused the whole army. Baked, the
            // vertices come back in the renderer's own space, which is exactly the space
            // the rest of this expects.
            return Kept(cut, whole, root, out _);
        }

        /// <summary>
        /// The worst band, and beside it the share of the whole outline that survives.
        ///
        /// <b>Two numbers because they answer two questions and one of them cannot stand
        /// in for the other.</b> The worst band says whether a shape is good enough to be
        /// drawn large: a figure that loses its feet keeps nothing in its bottom quarter
        /// and scores nought, and that is the rule working. It therefore cannot be used to
        /// ask whether a level is there at all, which is a different and blunter question
        /// - and asking it with the band number refused two hundred and seventy models in
        /// this project, nearly all of them good ones. The share over all the bands
        /// together is what answers that: a man-at-arms missing his feet keeps most of
        /// himself, and a deer the decimator pulled into a two-centimetre blob keeps
        /// none of himself.
        /// </summary>
        public static float Kept(IList<Renderer> cut, IList<Renderer> whole, Transform root,
                                 out float covered)
        {
            covered = 1f;

            if (cut == null || whole == null || root == null) return 1f;

            Snapshot(cut);
            Snapshot(whole);

            try
            {
                return Worst(cut, whole, root, out covered);
            }
            finally { Release(); }
        }

        static float Worst(IList<Renderer> cut, IList<Renderer> whole, Transform root,
                           out float covered)
        {
            covered = 1f;

            var frame = Framed(whole, root);
            if (frame.size.magnitude <= 0.0001f) return 1f;

            float worst = 1f;

            for (int eye = 0; eye < Eyes.Length; eye++)
            {
                var mine = Filled(cut, root, frame, eye);
                var theirs = Filled(whole, root, frame, eye);

                int all = 0;
                foreach (bool on in theirs) if (on) all++;
                if (all == 0) continue;

                // The whole outline at once, taken per view and the worst view kept - the
                // same shape of reading as the band below, asked of everything together.
                int held = 0;

                for (int i = 0; i < theirs.Length; i++)
                    if (theirs[i] && mine[i]) held++;

                float over = (float)held / all;
                if (over < covered) covered = over;

                for (int band = 0; band < Bands; band++)
                {
                    int from = band * Grid / Bands, to = (band + 1) * Grid / Bands;
                    int had = 0, kept = 0;

                    for (int y = from; y < to; y++)
                        for (int x = 0; x < Grid; x++)
                        {
                            if (!theirs[y * Grid + x]) continue;

                            had++;
                            if (mine[y * Grid + x]) kept++;
                        }

                    if (had * 100 < all) continue;

                    float share = (float)kept / had;
                    if (share < worst) worst = share;
                }
            }

            return worst;
        }

        /// <summary>The box both sets are drawn inside, taken from the original.</summary>
        static Bounds Framed(IList<Renderer> whole, Transform root)
        {
            bool any = false;
            var box = new Bounds();

            foreach (var renderer in whole)
            {
                var mesh = Mesh(renderer);
                if (mesh == null) continue;

                foreach (var corner in Corners(mesh.bounds))
                {
                    var at = root.InverseTransformPoint(
                        renderer.transform.TransformPoint(corner));

                    if (!any) { box = new Bounds(at, Vector3.zero); any = true; }
                    else box.Encapsulate(at);
                }
            }

            return box;
        }

        static IEnumerable<Vector3> Corners(Bounds box)
        {
            var min = box.min;
            var max = box.max;

            for (int i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? min.x : max.x,
                                         (i & 2) == 0 ? min.y : max.y,
                                         (i & 4) == 0 ? min.z : max.z);
        }

        /// <summary>Which grid squares this set of renderers fills, from one direction.</summary>
        static bool[] Filled(IList<Renderer> set, Transform root, Bounds frame, int eye)
        {
            var grid = new bool[Grid * Grid];

            var ahead = Eyes[eye].normalized;
            var right = Vector3.Cross(Vector3.up, ahead).normalized;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            var up = Vector3.Cross(ahead, right).normalized;

            // The same scale for both sets, from the original's own box, so a pixel means
            // the same thing in both pictures. The longest diagonal is used rather than the
            // width in this direction, because a model seen from two sides would otherwise
            // be drawn at two sizes and the two answers would not be comparable.
            float span = frame.size.magnitude;
            if (span <= 0.0001f) return grid;

            float scale = (Grid - 2) / span;
            var centre = frame.center;

            foreach (var renderer in set)
            {
                var mesh = Mesh(renderer);
                if (mesh == null) continue;

                var points = mesh.vertices;
                var corners = mesh.triangles;
                if (points == null || corners == null || points.Length == 0) continue;

                var flat = new Vector2[points.Length];

                for (int i = 0; i < points.Length; i++)
                {
                    var at = root.InverseTransformPoint(
                        renderer.transform.TransformPoint(points[i])) - centre;

                    flat[i] = new Vector2(Vector3.Dot(at, right) * scale + Grid * 0.5f,
                                          Vector3.Dot(at, up) * scale + Grid * 0.5f);
                }

                for (int t = 0; t + 2 < corners.Length; t += 3)
                    Fill(grid, flat[corners[t]], flat[corners[t + 1]], flat[corners[t + 2]]);
            }

            return grid;
        }

        /// <summary>One triangle painted into the grid, by the half-plane test.</summary>
        static void Fill(bool[] grid, Vector2 a, Vector2 b, Vector2 c)
        {
            int left = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int right = Mathf.Min(Grid - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int low = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int high = Mathf.Min(Grid - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

            if (right < left || high < low) return;

            float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

            // A triangle thinner than a grid square has no inside to test for, and a leaf
            // card seen edge-on is exactly that. Its two long edges are drawn instead, so
            // the thing still marks the squares it crosses rather than vanishing - which
            // would read as a model that lost its outline when it was only turned sideways.
            if (Mathf.Abs(area) < 0.0001f)
            {
                Line(grid, a, b);
                Line(grid, b, c);
                Line(grid, c, a);
                return;
            }

            for (int y = low; y <= high; y++)
                for (int x = left; x <= right; x++)
                {
                    var at = new Vector2(x + 0.5f, y + 0.5f);

                    float w0 = ((b.x - a.x) * (at.y - a.y) - (b.y - a.y) * (at.x - a.x)) / area;
                    float w1 = ((c.x - b.x) * (at.y - b.y) - (c.y - b.y) * (at.x - b.x)) / area;
                    float w2 = ((a.x - c.x) * (at.y - c.y) - (a.y - c.y) * (at.x - c.x)) / area;

                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f) grid[y * Grid + x] = true;
                }
        }

        /// <summary>One edge marked, for a triangle with no area to fill.</summary>
        static void Line(bool[] grid, Vector2 from, Vector2 to)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(from, to)) + 1;

            for (int i = 0; i <= steps; i++)
            {
                var at = Vector2.Lerp(from, to, steps == 0 ? 0f : (float)i / steps);

                int x = Mathf.FloorToInt(at.x), y = Mathf.FloorToInt(at.y);
                if (x >= 0 && x < Grid && y >= 0 && y < Grid) grid[y * Grid + x] = true;
            }
        }

        static Mesh Mesh(Renderer renderer)
        {
            if (renderer == null) return null;

            if (_baked.TryGetValue(renderer, out var snapped)) return snapped;

            if (renderer is SkinnedMeshRenderer skin) return skin.sharedMesh;

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>
        /// The skinned renderers of a set, snapshotted in the pose they stand in.
        ///
        /// Kept for the length of one measurement and thrown away after it. Taken once
        /// rather than inside <see cref="Mesh"/>, which is asked for the same renderer
        /// nine times over - four views of the cut, four of the whole, and the frame.
        /// </summary>
        static void Snapshot(IList<Renderer> set)
        {
            if (set == null) return;

            foreach (var renderer in set)
            {
                if (!(renderer is SkinnedMeshRenderer skin)) continue;
                if (skin.sharedMesh == null || _baked.ContainsKey(renderer)) continue;

                var baked = new UnityEngine.Mesh();

                // Without the scale, because every reading below puts the vertices
                // through the renderer's own transform and that carries it.
                skin.BakeMesh(baked, false);

                if (baked.vertexCount == 0) { Object.DestroyImmediate(baked); continue; }

                _baked[renderer] = baked;
            }
        }

        static void Release()
        {
            foreach (var baked in _baked.Values)
                if (baked != null) Object.DestroyImmediate(baked);

            _baked.Clear();
        }

        /// <summary>The snapshots of one measurement, by the renderer they were taken of.</summary>
        static readonly Dictionary<Renderer, Mesh> _baked = new Dictionary<Renderer, Mesh>();

        /// <summary>Named so the class doc can point at where it is checked.</summary>
        internal const string Tests = "SilhouetteTests";
    }
}
