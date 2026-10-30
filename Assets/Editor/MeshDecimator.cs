using System.Collections.Generic;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Cuts a mesh down to a triangle budget while keeping the shape it had.
    ///
    /// <b>Written because clustering was not good enough.</b> Dropping a model into a
    /// grid and keeping one vertex per cell is a few lines and it works on solid lumps -
    /// a rock, a bone pile. Put a wagon through it and the wheels go first: a spoke is
    /// thinner than a cell, so both of its sides land in the same cell, and the spoke
    /// becomes nothing. The broken wagon came out of that as a cloud of loose shards with
    /// no wheel, no plank and no wagon in it - photographed and looked at, rather than
    /// trusted because the triangle count had come down.
    ///
    /// This is the standard answer instead: every edge is weighed by how far the surface
    /// would move if it were collapsed, and the cheapest is collapsed, over and over,
    /// until the budget is met. The weighing is Garland and Heckbert's - each vertex
    /// carries the planes of the triangles it stands in, and the cost of putting it
    /// somewhere new is the summed squared distance to those planes. A spoke's two sides
    /// face opposite ways, so merging them is expensive, and it is left alone until there
    /// is nothing cheaper left to do.
    ///
    /// Two things are protected beyond that. An open edge - one with a single triangle on
    /// it, which is every splintered end of a broken wagon - gets a plane of its own
    /// standing across it, so the outline stays where it is. And a collapse that would
    /// turn a triangle inside out is refused however cheap it looks, because that is what
    /// makes a simplified mesh flicker.
    ///
    /// <b>It carries the rig, which it did not until 2026-10-30.</b> A skinned mesh is a
    /// mesh plus a weight per vertex saying which bones move it and a bindpose per bone
    /// saying where that bone stood when it was bound. Drop either and the result has the
    /// right silhouette and no skeleton - and that does not fail loudly, it ships: the
    /// knight stands in the field like a statue while his bones walk off without him. So
    /// the weights ride through the weld and the collapses, and the bindposes are copied
    /// across untouched, because nothing here moves a bone.
    ///
    /// That is what shut the cast out of <see cref="FarLods"/>: 36 character prefabs with
    /// no LOD chain at all, refused by that tool because this one would have wrecked them.
    ///
    /// <b>The welding is bone-aware, and that is the one subtle part.</b> Vertices at the
    /// same position are welded so an edge can be collapsed across a seam the file wrote
    /// as two points - but two vertices at one position driven by different bones are not
    /// one point, because the animation is about to pull them apart. Welding those hands
    /// one side of a shoulder to the other arm. Collapsing across bones afterwards is
    /// ordinary and fine: the vertex that survives keeps its own weights, the same way it
    /// keeps its own UV.
    /// </summary>
    public static class MeshDecimator
    {
        /// <summary>How much harder an open edge is to move than an ordinary one.</summary>
        const double BoundaryWeight = 60.0;

        /// <summary>Positions this close together are the same point, in metres.</summary>
        const float Same = 0.0001f;

        /// <summary>
        /// The mesh at the budget, or fewer triangles if it runs out of edges to collapse.
        ///
        /// The source is left alone. Vertices sitting on top of each other are welded
        /// first, because an edge cannot be collapsed across a seam the file has written
        /// as two separate points, and generated meshes are full of them.
        /// </summary>
        public static Mesh Simplify(Mesh source, int budget, out string story)
            => new Job(source, budget).Run(out story);

        sealed class Job
        {
            readonly List<Vector3> _where = new List<Vector3>();
            readonly List<Vector2> _mapped = new List<Vector2>();
            /// <summary>
            /// Each welded vertex's own bone weights, as many as it had.
            ///
            /// <b>Not a BoneWeight[], which is four bones a vertex whether they are used
            /// or not.</b> The army pack's characters are authored at two, and a mesh
            /// written four wide against a renderer expecting two is refused outright -
            /// "the data for mesh '' does not match the expected mesh data size and vertex
            /// stride" - with no exception and nothing drawn. So the width is the source's
            /// own, carried vertex by vertex.
            /// </summary>
            readonly List<BoneWeight1[]> _driven = new List<BoneWeight1[]>();
            readonly List<List<int>> _around = new List<List<int>>();
            readonly List<Candidate> _heap = new List<Candidate>();

            /// <summary>Whether the source is skinned, and so whether _driven means anything.</summary>
            bool _skinned;

            readonly Mesh _source;
            readonly int _budget;

            int[] _corners;
            bool[] _standing;
            bool[] _gone;
            int[] _stamp;
            double[] _quadrics;
            int _live;

            public Job(Mesh source, int budget)
            {
                _source = source;
                _budget = budget;
            }

            public Mesh Run(out string story)
            {
                Weld();
                Quadrics();
                Seed();

                int refused = 0;

                while (_live > _budget && _heap.Count > 0)
                {
                    var edge = Pop();

                    if (_gone[edge.A] || _gone[edge.B]) continue;
                    if (_stamp[edge.A] != edge.StampA || _stamp[edge.B] != edge.StampB) continue;

                    var target = Best(edge.A, edge.B, out _);

                    if (Flips(edge.A, edge.B, target)) { refused++; continue; }

                    Collapse(edge.A, edge.B, target);
                }

                story = $"collapsed to {_live} triangles, {refused} collapses refused as flips";
                return Built();
            }

            /// <summary>One vertex per position, with the triangles pointed at them.</summary>
            void Weld()
            {
                var points = _source.vertices;
                var uvs = _source.uv;
                bool textured = uvs != null && uvs.Length == points.Length;

                // <b>A mesh carries its skin in one of two shapes and this has to read
                // both.</b> The new one is a count per vertex and a flat run of weights,
                // which is what the importer writes and what lets a vertex have one bone
                // instead of four. The old one is a BoneWeight per vertex, four wide with
                // the unused ones at nought, and it is what any mesh assembled in code
                // through the `boneWeights` property has - including the one in this
                // project's own tests, which is how the gap was found rather than shipped.
                var skin = Skin(points.Length, out int[] starts, out int[] dominant);
                _skinned = skin != null;

                var slot = new Dictionary<(Vector3Int At, int Bone), int>(points.Length);
                var index = new int[points.Length];

                for (int i = 0; i < points.Length; i++)
                {
                    var at = new Vector3Int(Mathf.RoundToInt(points[i].x / Same),
                                            Mathf.RoundToInt(points[i].y / Same),
                                            Mathf.RoundToInt(points[i].z / Same));

                    // <b>The bone the vertex mostly follows is part of what makes it one
                    // point.</b> Welding is for the duplicate vertices a file writes at a
                    // seam so that each side can carry its own normal or UV - those are
                    // one point. Two vertices at the same position driven by different
                    // bones are not: they are about to be pulled apart by the animation,
                    // and merging them hands one side of a shoulder to the other arm.
                    // Collapsing across bones later is fine and ordinary, because the
                    // vertex that survives keeps its own weights; it is the silent weld
                    // that is wrong.
                    //
                    // An unskinned mesh has every bone at nought, so this key is the
                    // position and nothing else and the behaviour is what it was.
                    var key = (at, _skinned ? dominant[i] : 0);

                    if (!slot.TryGetValue(key, out int found))
                    {
                        found = _where.Count;
                        slot[key] = found;
                        _where.Add(points[i]);
                        _mapped.Add(textured ? uvs[i] : Vector2.zero);
                        _around.Add(new List<int>(6));

                        if (_skinned)
                        {
                            int count = starts[i + 1] - starts[i];
                            var mine = new BoneWeight1[count];
                            for (int w = 0; w < count; w++) mine[w] = skin[starts[i] + w];
                            _driven.Add(mine);
                        }
                        else _driven.Add(null);
                    }

                    index[i] = found;
                }

                var triangles = _source.triangles;
                var kept = new List<int>(triangles.Length);

                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = index[triangles[i]], b = index[triangles[i + 1]], c = index[triangles[i + 2]];
                    if (a == b || b == c || a == c) continue;

                    int t = kept.Count / 3;
                    kept.Add(a); kept.Add(b); kept.Add(c);
                    _around[a].Add(t); _around[b].Add(t); _around[c].Add(t);
                }

                _corners = kept.ToArray();
                _live = _corners.Length / 3;

                _standing = new bool[_live];
                for (int i = 0; i < _live; i++) _standing[i] = true;

                _gone = new bool[_where.Count];
                _stamp = new int[_where.Count];
                _quadrics = new double[_where.Count * 10];
            }


            /// <summary>
            /// The source's bone weights, flattened, whichever shape the mesh keeps them
            /// in - or null when there are none.
            ///
            /// <paramref name="starts"/> has one more entry than there are vertices, so a
            /// vertex's slice is the run between its own start and the next, and the last
            /// vertex needs no special case. <paramref name="dominant"/> is the bone each
            /// vertex mostly follows, which is part of what makes two vertices one point
            /// when they are welded.
            /// </summary>
            BoneWeight1[] Skin(int vertices, out int[] starts, out int[] dominant)
            {
                starts = null;
                dominant = null;

                if (vertices <= 0) return null;

                var perVertex = _source.GetBonesPerVertex();
                var flat = _source.GetAllBoneWeights();

                if (perVertex.Length == vertices && flat.Length > 0)
                {
                    starts = new int[vertices + 1];
                    dominant = new int[vertices];

                    for (int i = 0, running = 0; i < vertices; i++)
                    {
                        starts[i] = running;
                        dominant[i] = perVertex[i] > 0 ? flat[running].boneIndex : 0;
                        running += perVertex[i];
                    }

                    starts[vertices] = flat.Length;
                    return flat.ToArray();
                }

                // The four-wide shape. The weights at nought are dropped rather than
                // carried, because carrying them would write every far level four bones
                // wide when the model is authored at one or two - which is both bigger and
                // a layout the renderer may not be expecting.
                var old = _source.boneWeights;
                if (old == null || old.Length != vertices) return null;

                starts = new int[vertices + 1];
                dominant = new int[vertices];

                var kept = new List<BoneWeight1>(vertices * 2);

                for (int i = 0; i < vertices; i++)
                {
                    starts[i] = kept.Count;
                    dominant[i] = old[i].boneIndex0;

                    Keep(kept, old[i].boneIndex0, old[i].weight0);
                    Keep(kept, old[i].boneIndex1, old[i].weight1);
                    Keep(kept, old[i].boneIndex2, old[i].weight2);
                    Keep(kept, old[i].boneIndex3, old[i].weight3);

                    // A vertex driven by nothing would sit at the origin while the rest of
                    // the model walks away, so it is given its first bone outright.
                    if (kept.Count == starts[i])
                        kept.Add(new BoneWeight1 { boneIndex = old[i].boneIndex0, weight = 1f });
                }

                starts[vertices] = kept.Count;
                return kept.ToArray();
            }

            static void Keep(List<BoneWeight1> into, int bone, float weight)
            {
                if (weight > 0.0001f) into.Add(new BoneWeight1 { boneIndex = bone, weight = weight });
            }

            /// <summary>Each vertex given the planes of the triangles it stands in.</summary>
            void Quadrics()
            {
                var open = new Dictionary<long, int>();

                for (int t = 0; t < _live; t++)
                {
                    Vector3 p0 = _where[_corners[t * 3]],
                            p1 = _where[_corners[t * 3 + 1]],
                            p2 = _where[_corners[t * 3 + 2]];

                    var cross = Vector3.Cross(p1 - p0, p2 - p0);
                    float twice = cross.magnitude;

                    for (int c = 0; c < 3; c++)
                    {
                        int a = _corners[t * 3 + c], b = _corners[t * 3 + (c + 1) % 3];
                        long key = Pair(a, b);
                        open[key] = open.TryGetValue(key, out int seen) ? seen + 1 : 1;
                    }

                    if (twice <= 0f) continue;

                    var normal = cross / twice;
                    double weight = twice * 0.5;
                    double d = -Vector3.Dot(normal, p0);

                    for (int c = 0; c < 3; c++)
                        Plane(_corners[t * 3 + c], normal.x, normal.y, normal.z, d, weight);
                }

                // The open edges, a second time round, now that it is known which they
                // are: a wall standing across each one, so the splintered outline of the
                // thing is not planed off as the cheapest edge in the model.
                for (int t = 0; t < _live; t++)
                {
                    Vector3 p0 = _where[_corners[t * 3]],
                            p1 = _where[_corners[t * 3 + 1]],
                            p2 = _where[_corners[t * 3 + 2]];

                    var cross = Vector3.Cross(p1 - p0, p2 - p0);
                    float twice = cross.magnitude;
                    if (twice <= 0f) continue;

                    var face = cross / twice;

                    for (int c = 0; c < 3; c++)
                    {
                        int a = _corners[t * 3 + c], b = _corners[t * 3 + (c + 1) % 3];
                        if (open[Pair(a, b)] != 1) continue;

                        var along = (_where[b] - _where[a]).normalized;
                        var wall = Vector3.Cross(along, face).normalized;
                        if (wall.sqrMagnitude <= 0f) continue;

                        double d = -Vector3.Dot(wall, _where[a]);
                        double weight = BoundaryWeight * twice * 0.5;

                        Plane(a, wall.x, wall.y, wall.z, d, weight);
                        Plane(b, wall.x, wall.y, wall.z, d, weight);
                    }
                }
            }

            void Plane(int vertex, double a, double b, double c, double d, double weight)
            {
                int o = vertex * 10;

                _quadrics[o] += weight * a * a;
                _quadrics[o + 1] += weight * a * b;
                _quadrics[o + 2] += weight * a * c;
                _quadrics[o + 3] += weight * a * d;
                _quadrics[o + 4] += weight * b * b;
                _quadrics[o + 5] += weight * b * c;
                _quadrics[o + 6] += weight * b * d;
                _quadrics[o + 7] += weight * c * c;
                _quadrics[o + 8] += weight * c * d;
                _quadrics[o + 9] += weight * d * d;
            }

            /// <summary>Every edge weighed once, to start with.</summary>
            void Seed()
            {
                var seen = new HashSet<long>();

                for (int t = 0; t < _live; t++)
                    for (int c = 0; c < 3; c++)
                    {
                        int a = _corners[t * 3 + c], b = _corners[t * 3 + (c + 1) % 3];
                        if (seen.Add(Pair(a, b))) Weigh(a, b);
                    }
            }

            void Weigh(int a, int b)
            {
                Best(a, b, out double error);

                Push(new Candidate
                {
                    Error = error, A = a, B = b, StampA = _stamp[a], StampB = _stamp[b]
                });
            }

            /// <summary>
            /// Where the two are best merged, of three places: either end, or the middle.
            ///
            /// The textbook solves for the least-error point instead, which needs the
            /// quadric inverted and a fallback for when it will not invert. Three tries
            /// cost nothing and never have to be caught.
            /// </summary>
            Vector3 Best(int a, int b, out double error)
            {
                var sum = new double[10];
                for (int i = 0; i < 10; i++) sum[i] = _quadrics[a * 10 + i] + _quadrics[b * 10 + i];

                var at = _where[a];
                error = Cost(sum, at);

                var other = _where[b];
                double second = Cost(sum, other);
                if (second < error) { error = second; at = other; }

                var middle = (_where[a] + _where[b]) * 0.5f;
                double third = Cost(sum, middle);
                if (third < error) { error = third; at = middle; }

                return at;
            }

            static double Cost(double[] q, Vector3 p)
            {
                double x = p.x, y = p.y, z = p.z;

                return q[0] * x * x + 2 * q[1] * x * y + 2 * q[2] * x * z + 2 * q[3] * x
                     + q[4] * y * y + 2 * q[5] * y * z + 2 * q[6] * y
                     + q[7] * z * z + 2 * q[8] * z + q[9];
            }

            /// <summary>Whether a triangle that outlives the collapse would face backwards.</summary>
            bool Flips(int a, int b, Vector3 target)
                => Turned(a, a, b, target) || Turned(b, a, b, target);

            bool Turned(int vertex, int a, int b, Vector3 target)
            {
                foreach (int t in _around[vertex])
                {
                    if (!_standing[t]) continue;

                    int i0 = _corners[t * 3], i1 = _corners[t * 3 + 1], i2 = _corners[t * 3 + 2];

                    bool hasA = i0 == a || i1 == a || i2 == a;
                    bool hasB = i0 == b || i1 == b || i2 == b;
                    if (hasA && hasB) continue;

                    Vector3 p0 = Moved(i0, a, b, target),
                            p1 = Moved(i1, a, b, target),
                            p2 = Moved(i2, a, b, target);

                    var was = Vector3.Cross(_where[i1] - _where[i0], _where[i2] - _where[i0]);
                    var now = Vector3.Cross(p1 - p0, p2 - p0);

                    if (Vector3.Dot(was.normalized, now.normalized) < 0.2f) return true;
                }

                return false;
            }

            Vector3 Moved(int vertex, int a, int b, Vector3 target)
                => vertex == a || vertex == b ? target : _where[vertex];

            void Collapse(int a, int b, Vector3 target)
            {
                _where[a] = target;
                for (int i = 0; i < 10; i++) _quadrics[a * 10 + i] += _quadrics[b * 10 + i];

                foreach (int t in _around[b])
                {
                    if (!_standing[t]) continue;

                    for (int c = 0; c < 3; c++)
                        if (_corners[t * 3 + c] == b) _corners[t * 3 + c] = a;

                    int i0 = _corners[t * 3], i1 = _corners[t * 3 + 1], i2 = _corners[t * 3 + 2];

                    if (i0 == i1 || i1 == i2 || i0 == i2)
                    {
                        _standing[t] = false;
                        _live--;
                        continue;
                    }

                    _around[a].Add(t);
                }

                _around[b].Clear();
                _gone[b] = true;
                _stamp[a]++;

                // Everything still touching the merged vertex is worth a different amount
                // now. Bumping its stamp is what makes the entries already in the heap
                // stale, so they are thrown away when they surface rather than acted on.
                var neighbours = new HashSet<int>();
                var kept = new List<int>(_around[a].Count);

                foreach (int t in _around[a])
                {
                    if (!_standing[t]) continue;
                    kept.Add(t);

                    for (int c = 0; c < 3; c++)
                    {
                        int other = _corners[t * 3 + c];
                        if (other != a) neighbours.Add(other);
                    }
                }

                _around[a] = kept;
                foreach (int other in neighbours) Weigh(a, other);
            }

            Mesh Built()
            {
                var index = new int[_where.Count];
                for (int i = 0; i < index.Length; i++) index[i] = -1;

                var where = new List<Vector3>();
                var mapped = new List<Vector2>();
                var driven = new List<BoneWeight1[]>();
                var corners = new List<int>(_live * 3);

                for (int t = 0; t < _standing.Length; t++)
                {
                    if (!_standing[t]) continue;

                    for (int c = 0; c < 3; c++)
                    {
                        int v = _corners[t * 3 + c];

                        if (index[v] < 0)
                        {
                            index[v] = where.Count;
                            where.Add(_where[v]);
                            mapped.Add(_mapped[v]);
                            if (_skinned) driven.Add(_driven[v]);
                        }

                        corners.Add(index[v]);
                    }
                }

                var mesh = new Mesh();
                if (where.Count > 65000)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.SetVertices(where);
                mesh.SetUVs(0, mapped);
                mesh.SetTriangles(corners, 0);

                // <b>The rig, before the normals, because a skinned mesh with no bindposes
                // is not a skinned mesh.</b> Without these two the far level comes back
                // with the right silhouette and no skeleton - which does not fail loudly,
                // it ships, and the knight stands in the field like a statue while his
                // bones walk off without him. The bindposes are the source's unchanged:
                // they say where each bone was when the mesh was bound, and nothing here
                // moves a bone.
                if (_skinned)
                {
                    int total = 0;
                    foreach (var mine in driven) total += mine != null ? mine.Length : 0;

                    var counts = new Unity.Collections.NativeArray<byte>(
                        driven.Count, Unity.Collections.Allocator.Temp);
                    var flat = new Unity.Collections.NativeArray<BoneWeight1>(
                        total, Unity.Collections.Allocator.Temp);

                    try
                    {
                        int at = 0;

                        for (int v = 0; v < driven.Count; v++)
                        {
                            var mine = driven[v];
                            counts[v] = (byte)(mine != null ? mine.Length : 0);

                            if (mine == null) continue;
                            foreach (var one in mine) flat[at++] = one;
                        }

                        mesh.SetBoneWeights(counts, flat);
                    }
                    finally { counts.Dispose(); flat.Dispose(); }

                    mesh.bindposes = _source.bindposes;
                }

                mesh.RecalculateNormals();

                // <b>Tangents, when the source had them, and this is what stopped every
                // character in the game from drawing.</b> The weights and the bindposes
                // were right from the first try; what was missing was a channel that has
                // nothing to do with the rig. Unity's skinning writes position, normal
                // *and* tangent into the skinned vertex buffer, so a skinned mesh without
                // tangents has a stride the backend does not expect, and it refuses the
                // mesh outright: "the data for mesh '' does not match the expected mesh
                // data size and vertex stride". Nothing draws, and no exception is thrown.
                //
                // Asked of the source rather than always done, so an unskinned prop that
                // never had tangents does not start carrying them - that would be four
                // floats a vertex of nothing, on the levels built to be cheap.
                if (_source.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent))
                    mesh.RecalculateTangents();

                mesh.RecalculateBounds();

                return mesh;
            }

            struct Candidate
            {
                public double Error;
                public int A, B, StampA, StampB;
            }

            void Push(Candidate candidate)
            {
                _heap.Add(candidate);

                int i = _heap.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_heap[parent].Error <= _heap[i].Error) break;

                    (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                    i = parent;
                }
            }

            Candidate Pop()
            {
                var top = _heap[0];
                int last = _heap.Count - 1;

                _heap[0] = _heap[last];
                _heap.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1, right = left + 1, least = i;

                    if (left < _heap.Count && _heap[left].Error < _heap[least].Error) least = left;
                    if (right < _heap.Count && _heap[right].Error < _heap[least].Error) least = right;
                    if (least == i) break;

                    (_heap[least], _heap[i]) = (_heap[i], _heap[least]);
                    i = least;
                }

                return top;
            }

            static long Pair(int a, int b)
                => a < b ? (long)a * 4_000_037L + b : (long)b * 4_000_037L + a;
        }
    }
}
