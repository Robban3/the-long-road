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
            readonly List<List<int>> _around = new List<List<int>>();
            readonly List<Candidate> _heap = new List<Candidate>();

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

                var slot = new Dictionary<Vector3Int, int>(points.Length);
                var index = new int[points.Length];

                for (int i = 0; i < points.Length; i++)
                {
                    var key = new Vector3Int(Mathf.RoundToInt(points[i].x / Same),
                                             Mathf.RoundToInt(points[i].y / Same),
                                             Mathf.RoundToInt(points[i].z / Same));

                    if (!slot.TryGetValue(key, out int found))
                    {
                        found = _where.Count;
                        slot[key] = found;
                        _where.Add(points[i]);
                        _mapped.Add(textured ? uvs[i] : Vector2.zero);
                        _around.Add(new List<int>(6));
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
                mesh.RecalculateNormals();
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
