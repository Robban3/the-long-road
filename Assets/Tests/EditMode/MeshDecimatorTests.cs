using NUnit.Framework;
using TheVeil.Editor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheVeil.Tests
{
    /// <summary>
    /// That <see cref="MeshDecimator"/> cuts a mesh down and that a skinned one comes back
    /// still skinned.
    ///
    /// <b>These are the first tests any of the editor tools have had.</b> The test
    /// assembly did not reference TheVeil.Editor at all, which is why three hundred and
    /// eighty-two green tests said nothing about a decimator that silently dropped every
    /// bone weight it was given. A tool that writes assets and has no test is a tool whose
    /// mistakes are found by looking at the game.
    ///
    /// The rig is the thing worth asserting, because losing it is the failure that does
    /// not announce itself: the mesh still has the right shape, the triangle count still
    /// comes down, the asset still saves, and the character stands in the field like a
    /// statue while his skeleton walks off without him.
    /// </summary>
    public sealed class MeshDecimatorTests
    {
        /// <summary>
        /// A strip of quads along X, two bones, the seam in the middle.
        ///
        /// Built rather than loaded, so the test says what it means and does not depend on
        /// a pack being on disk. Every vertex of the left half follows bone 0 and every
        /// vertex of the right half bone 1, with the middle column carrying one vertex of
        /// each at the same position - which is the case the welding has to get right.
        /// </summary>
        static Mesh Strip(int columns, bool skinned)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            var weights = new System.Collections.Generic.List<BoneWeight>();
            var corners = new System.Collections.Generic.List<int>();

            for (int i = 0; i <= columns; i++)
            {
                float x = i;
                int bone = i < columns / 2 ? 0 : 1;

                // The seam: one vertex for each bone at the same place.
                int copies = i == columns / 2 ? 2 : 1;

                for (int c = 0; c < copies; c++)
                {
                    int which = copies == 2 ? c : bone;

                    points.Add(new Vector3(x, 0f, 0f));
                    points.Add(new Vector3(x, 1f, 0f));

                    var weight = new BoneWeight { boneIndex0 = which, weight0 = 1f };
                    weights.Add(weight);
                    weights.Add(weight);
                }
            }

            // Quads between neighbouring columns, taking the first pair of each column.
            var starts = new System.Collections.Generic.List<int>();
            for (int v = 0; v < points.Count; v += 2) starts.Add(v);

            for (int s = 0; s + 1 < starts.Count; s++)
            {
                int a = starts[s], b = starts[s + 1];
                if (Mathf.Approximately(points[a].x, points[b].x)) continue;

                corners.Add(a); corners.Add(a + 1); corners.Add(b);
                corners.Add(b); corners.Add(a + 1); corners.Add(b + 1);
            }

            var mesh = new Mesh();
            mesh.SetVertices(points);
            mesh.SetTriangles(corners, 0);

            if (skinned)
            {
                mesh.boneWeights = weights.ToArray();
                mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.Translate(Vector3.right) };
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        [Test]
        public void CutsAnUnskinnedMeshToTheBudget()
        {
            var source = Strip(40, skinned: false);
            int was = source.triangles.Length / 3;

            var thin = MeshDecimator.Simplify(source, 20, out string story);

            Assert.IsNotNull(thin, "the decimator returned nothing");
            int now = thin.triangles.Length / 3;

            Assert.Less(now, was, $"nothing came off: {was} -> {now} ({story})");
            Assert.LessOrEqual(now, 20, $"over the budget of 20: {now} ({story})");
            Assert.Greater(now, 0, "everything came off");
        }

        [Test]
        public void KeepsTheRigOnASkinnedMesh()
        {
            var source = Strip(40, skinned: true);

            var thin = MeshDecimator.Simplify(source, 20, out string story);

            Assert.IsNotNull(thin, "the decimator returned nothing");

            var weights = thin.boneWeights;

            Assert.AreEqual(thin.vertexCount, weights.Length,
                            $"a weight per vertex is what a skinned mesh is ({story})");
            Assert.AreEqual(source.bindposes.Length, thin.bindposes.Length,
                            "the bindposes did not come across");

            // Every vertex has to be driven by something, or it stays at the origin while
            // the rest of the model walks away.
            foreach (var weight in weights)
            {
                float total = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
                Assert.Greater(total, 0.001f, "a vertex is driven by no bone at all");
            }

            // And both bones have to still be used. A far level that quietly handed the
            // whole strip to bone 0 would pass every assertion above and bend wrongly.
            bool left = false, right = false;

            foreach (var weight in weights)
            {
                if (weight.boneIndex0 == 0) left = true;
                if (weight.boneIndex0 == 1) right = true;
            }

            Assert.IsTrue(left && right, "the simplified mesh follows only one of two bones");
        }

        /// <summary>
        /// <b>The test that was missing, and it cost a whole pass.</b> The two above
        /// assert that the rig data is there, and it was there on the first try - every
        /// weight, every bindpose, on a mesh built inside this file. Then every skinned
        /// renderer in the game refused to draw, because the simplified mesh had no
        /// tangents and Unity's skinning writes position, normal and tangent together: a
        /// stride the backend does not expect is a mesh it declines, silently.
        ///
        /// So the thing to assert is not "has the data I thought about". It is "carries
        /// the same channels the source carried", which is a question that does not
        /// require knowing which channel matters.
        /// </summary>
        [Test]
        public void CarriesEveryChannelTheSourceHad()
        {
            var source = Strip(40, skinned: true);
            source.RecalculateTangents();

            Assert.IsTrue(source.HasVertexAttribute(VertexAttribute.Tangent),
                          "the source for this test has no tangents to lose");

            var thin = MeshDecimator.Simplify(source, 20, out _);
            Assert.IsNotNull(thin);

            foreach (VertexAttribute attribute in System.Enum.GetValues(typeof(VertexAttribute)))
            {
                if (!source.HasVertexAttribute(attribute)) continue;

                // The two skin channels are asserted below instead, because they are
                // allowed to get narrower. See the note there.
                if (attribute == VertexAttribute.BlendWeight) continue;
                if (attribute == VertexAttribute.BlendIndices) continue;

                Assert.IsTrue(thin.HasVertexAttribute(attribute),
                              $"the simplified mesh dropped {attribute}, which the source had");
            }

            // <b>The skin is asserted by what it does, not by which channels carry it.</b>
            // A mesh where every vertex follows exactly one bone has no BlendWeight
            // channel at all: the weight is implicitly one, so Unity stores the index and
            // nothing else. That is a narrower layout and a cheaper one, not a lost rig -
            // and demanding channel-for-channel parity failed on exactly that, which is
            // the test being wrong rather than the decimator.
            Assert.IsTrue(thin.HasVertexAttribute(VertexAttribute.BlendIndices),
                          "the simplified mesh has no bone indices, so nothing moves it");

            Assert.AreEqual(thin.vertexCount, thin.GetBonesPerVertex().Length,
                            "not every vertex is accounted for in the skin");

            Assert.AreEqual(source.bindposes.Length, thin.bindposes.Length,
                            "the bindposes did not come across");
        }

        [Test]
        public void LeavesAnUnskinnedMeshUnskinned()
        {
            var source = Strip(40, skinned: false);

            var thin = MeshDecimator.Simplify(source, 20, out _);

            Assert.IsNotNull(thin);
            Assert.AreEqual(0, thin.boneWeights.Length,
                            "an unskinned mesh came back carrying weights");
        }
    }
}
