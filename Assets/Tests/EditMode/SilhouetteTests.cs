using System.Collections.Generic;
using NUnit.Framework;
using TheVeil.Editor;
using UnityEngine;

namespace TheVeil.Tests
{
    /// <summary>
    /// That <see cref="Silhouette"/> answers shapes whose answer is already known.
    ///
    /// <b>This is the check the rule it feeds cannot do without.</b> A number that decides
    /// whether a generated LOD level is kept has to be right about the two cases it exists
    /// for: a simplification that keeps the outline, which must pass, and a cloud of
    /// fragments where a tree used to be, which must not. Neither is a judgement - a box
    /// drawn against itself is one, a box drawn against a quarter of a box is a quarter -
    /// so they can be asserted rather than looked at.
    ///
    /// The tree that started it is not in here on purpose: a test that loads a pack asset
    /// says nothing when the pack changes, and these shapes say what they mean.
    /// </summary>
    public sealed class SilhouetteTests
    {
        /// <summary>A box of the given size, standing at the origin.</summary>
        static Renderer Box(Vector3 size, Vector3 at, List<GameObject> made)
        {
            var stood = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stood.transform.position = at;
            stood.transform.localScale = size;

            made.Add(stood);
            return stood.GetComponent<Renderer>();
        }

        [Test]
        public void A_shape_against_itself_keeps_all_of_its_outline()
        {
            var made = new List<GameObject>();

            try
            {
                var root = new GameObject("root");
                made.Add(root);

                var one = new List<Renderer> { Box(Vector3.one * 2f, Vector3.zero, made) };

                Assert.AreEqual(1f, Silhouette.Kept(one, one, root.transform), 0.001f);
            }
            finally { foreach (var thing in made) Object.DestroyImmediate(thing); }
        }

        [Test]
        public void A_trunk_without_its_canopy_keeps_little_of_it()
        {
            var made = new List<GameObject>();

            try
            {
                var root = new GameObject("root");
                made.Add(root);

                // A tree in the only sense this measurement cares about: something wide on
                // top of something narrow. Keeping the stick and losing the crown is the
                // failure this rule is for, and it has to read as a failure even though
                // every triangle that is left is in exactly the right place.
                var trunk = Box(new Vector3(0.4f, 4f, 0.4f), Vector3.zero, made);
                var crown = Box(new Vector3(6f, 4f, 6f), new Vector3(0f, 4f, 0f), made);

                var whole = new List<Renderer> { trunk, crown };
                var stick = new List<Renderer> { trunk };

                float kept = Silhouette.Kept(stick, whole, root.transform);

                // Nought, and that is the right answer rather than a harsh one: the band
                // the crown stood in is empty now. This asserted "greater than nought" while
                // the measure compared whole outlines, and the band that replaced it is
                // what makes a missing crown read as missing rather than as a tenth off.
                Assert.Less(kept, 0.3f, "a trunk alone should not pass for a tree");
            }
            finally { foreach (var thing in made) Object.DestroyImmediate(thing); }
        }

        [Test]
        public void Scattered_fragments_of_a_shape_do_not_keep_it()
        {
            var made = new List<GameObject>();

            try
            {
                var root = new GameObject("root");
                made.Add(root);

                var solid = Box(Vector3.one * 8f, Vector3.zero, made);
                var whole = new List<Renderer> { solid };

                // What a decimated leaf canopy is: a few of the original's pieces, in the
                // right places, with the rest of the shape gone.
                var confetti = new List<Renderer>
                {
                    Box(Vector3.one * 0.6f, new Vector3(-2f, 2f, 1f), made),
                    Box(Vector3.one * 0.6f, new Vector3(2f, -1f, -1f), made),
                    Box(Vector3.one * 0.6f, new Vector3(0f, 3f, -2f), made),
                };

                float kept = Silhouette.Kept(confetti, whole, root.transform);

                Assert.Less(kept, 0.25f, "fragments should not pass for the whole shape");
            }
            finally { foreach (var thing in made) Object.DestroyImmediate(thing); }
        }

        [Test]
        public void A_crown_that_lost_its_trunk_does_not_pass()
        {
            var made = new List<GameObject>();

            try
            {
                var root = new GameObject("root");
                made.Add(root);

                // The meadow tree, in the only terms this measurement has: a wide crown
                // over a thin column. The crown is nearly all of the outline by area, so
                // comparing whole outlines scored this 0,88 and let it through - and the
                // photograph of the real thing was a canopy floating over nothing. The
                // band the trunk stands in has to answer for itself.
                var trunk = Box(new Vector3(0.5f, 5f, 0.5f), Vector3.zero, made);
                var crown = Box(new Vector3(9f, 5f, 9f), new Vector3(0f, 5f, 0f), made);

                var whole = new List<Renderer> { trunk, crown };
                var crownOnly = new List<Renderer> { crown };

                float kept = Silhouette.Kept(crownOnly, whole, root.transform);

                Assert.Less(kept, 0.2f, "a crown with no trunk is not a tree");
            }
            finally { foreach (var thing in made) Object.DestroyImmediate(thing); }
        }

        [Test]
        public void A_shape_that_lost_only_its_inside_keeps_its_outline()
        {
            var made = new List<GameObject>();

            try
            {
                var root = new GameObject("root");
                made.Add(root);

                // The case that must pass: the simplification a decimator is for. The
                // outer box is what is seen; the inner one is detail no silhouette ever
                // carried, and dropping it is free.
                var outer = Box(Vector3.one * 6f, Vector3.zero, made);
                var inner = Box(Vector3.one * 2f, Vector3.zero, made);

                var whole = new List<Renderer> { outer, inner };
                var cut = new List<Renderer> { outer };

                Assert.AreEqual(1f, Silhouette.Kept(cut, whole, root.transform), 0.02f);
            }
            finally { foreach (var thing in made) Object.DestroyImmediate(thing); }
        }
    }
}
