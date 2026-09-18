using UnityEngine;

namespace TheVeil.View
{
    /// <summary>
    /// Fits imported models to the world.
    ///
    /// The packs are authored at wildly different scales — a wolf, a knight, a pine
    /// and a crate do not arrive in the same units, and nothing in an FBX says how
    /// tall the thing is meant to be. Measuring each model on instantiation and
    /// rescaling it to a stated height in metres is the only approach that survives
    /// mixing six packs from three different authors.
    ///
    /// Getting this wrong is quiet rather than loud: the first version scattered
    /// trees at their native size, which put one-metre pines on a 256-metre map. They
    /// were placed perfectly and were simply invisible.
    /// </summary>
    public static class ModelScaling
    {
        public static Bounds Measure(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>
        /// Scales an instance to the given height in metres and stands it on
        /// <paramref name="groundY"/> so its feet, not its origin, meet the ground.
        ///
        /// Measured from renderer bounds, which for a rigged character is a box drawn
        /// to hold every clip in the file rather than the figure standing in front of
        /// you. That is close enough for a model whose meshes are all body — and it is
        /// not close enough for one carrying a prop that reaches past its boots, which
        /// is why the knight's own sword mesh is switched off and a scaled one put in
        /// his hand instead. The alternative, baking every skinned mesh at spawn to
        /// measure it exactly, costs a vertex walk per actor to correct a model the
        /// casting can simply describe properly.
        /// </summary>
        public static void Fit(GameObject instance, float targetHeight, float groundY = 0f)
        {
            var bounds = Measure(instance);
            if (bounds.size.y > 0.0001f)
                instance.transform.localScale *= targetHeight / bounds.size.y;

            var scaled = Measure(instance);
            float lift = groundY - scaled.min.y;
            instance.transform.position += new Vector3(0f, lift, 0f);
        }

        /// <summary>
        /// Scales an instance to a height, unless that would make it too wide.
        ///
        /// Fitting by height multiplies the whole model, width included, and the width is
        /// the part nobody was thinking about. A tuft of grass authored fifteen
        /// centimetres tall and a metre across is scaled by four and a half to reach
        /// seven-tenths of a metre, and comes out five and a half metres wide: a green
        /// disc the size of a wagon, of which the map carried fifteen hundred. They were
        /// blamed on the ground patches, on the terrain colour and on a stale scene
        /// before the map was simply asked what it was carrying.
        ///
        /// The smaller of the two demands wins, so a tall thin reed reaches its full
        /// height and a wide low clump stops at its width.
        /// </summary>
        public static void FitWithin(GameObject instance, float maxHeight, float maxWidth,
                                     float groundY = 0f)
        {
            var bounds = Measure(instance);

            float widest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (bounds.size.y <= 0.0001f || widest <= 0.0001f) return;

            instance.transform.localScale *=
                Mathf.Min(maxHeight / bounds.size.y, maxWidth / widest);

            var scaled = Measure(instance);
            instance.transform.position += new Vector3(0f, groundY - scaled.min.y, 0f);
        }

        /// <summary>
        /// Scales a bridge so it is both wide enough to drive over and long enough to
        /// reach the far bank.
        ///
        /// Fitting one to a footprint scales its *longest* side, which for a bridge is
        /// the span — so asking for six metres of bridge gave six metres of length and
        /// whatever the model's proportions then left for the deck, which was under two.
        /// A wagon is two and a half metres wide and the caravan drove along the parapet.
        ///
        /// Both dimensions are asked for and the larger demand wins, so a long thin
        /// bridge is scaled up until its deck is wide enough and a short one until it
        /// reaches across.
        ///
        /// <b>Nothing here caps the height, and it used to.</b> Satisfying a five-metre
        /// deck on a model three metres wide multiplies everything — height included — by
        /// five thirds, and the packs' arched bridges are tall to begin with. What came
        /// out was a monument straddling a four-metre brook with the caravan on its
        /// roadway several metres up. The cap that answered that scaled the model down
        /// uniformly, because the caller yaws the bridge before fitting it and lays Z-up
        /// prefabs down with a quarter turn about X, so a non-uniform localScale here
        /// would squash whichever axis happened to be pointing the wrong way. Uniform
        /// meant the bridge bought its height by giving up its span, and stopped
        /// reaching across.
        ///
        /// A bridge that towers is not too big. It is too high, and height is a
        /// *position*: the model stands on its underside here, and the caller drops it
        /// afterwards until its roadway is level with the bank — see
        /// TerrainDecorator.Bridge and DeckClearance. The footings end up in the channel,
        /// which is where a bridge keeps them.
        /// </summary>
        /// <summary>
        /// How far a model reaches along one horizontal direction, in metres, whatever way
        /// it is turned.
        ///
        /// <b>The bounding box cannot answer this and two methods here were asking it
        /// to.</b> Measure returns a box aligned to the world, which is the model's width
        /// and length only while the model is aligned to the world too. A bridge is laid on
        /// its river's bearing and rivers do not run north: 1-10's lies at seventy-nine
        /// degrees, where the box's depth is mostly the bridge's *length*. Widen divided the
        /// width it wanted by that, and the deck came out at half the nine metres it was
        /// asked for - reported from a playtest as the bridge being too small again, which
        /// is what it was.
        ///
        /// Each mesh's own bounds are in the mesh's own frame, so their corners carried
        /// into the world and laid against the direction give the true reach at any angle.
        /// The corners of a mesh's box rather than every vertex: a touch generous on a
        /// rounded model, exact on a plank one, and a few dozen points rather than
        /// thousands.
        /// </summary>
        public static float ExtentAlong(GameObject instance, Vector3 direction)
        {
            if (instance == null) return 0f;

            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-8f) return 0f;
            direction.Normalize();

            float low = float.MaxValue, high = float.MinValue;

            void Take(Transform frame, Bounds box)
            {
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? box.min.x : box.max.x,
                                             (i & 2) == 0 ? box.min.y : box.max.y,
                                             (i & 4) == 0 ? box.min.z : box.max.z);

                    float along = Vector3.Dot(frame.TransformPoint(corner), direction);
                    if (along < low) low = along;
                    if (along > high) high = along;
                }
            }

            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null) Take(filter.transform, filter.sharedMesh.bounds);

            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (skin.sharedMesh != null) Take(skin.transform, skin.sharedMesh.bounds);

            return high > low ? high - low : 0f;
        }

        public static void FitToCrossing(GameObject instance, float deck, float span,
                                         float groundY = 0f, Vector3 alongWorld = default)
        {
            var bounds = Measure(instance);

            // The box's longer side is the length only for a model laid along an axis.
            // Given the direction the crossing runs, the length is measured along it -
            // see ExtentAlong.
            float across, along;

            if (alongWorld.sqrMagnitude > 1e-8f)
            {
                along = ExtentAlong(instance, alongWorld);
                across = ExtentAlong(instance, new Vector3(alongWorld.z, 0f, -alongWorld.x));
            }
            else
            {
                across = Mathf.Min(bounds.size.x, bounds.size.z);
                along = Mathf.Max(bounds.size.x, bounds.size.z);
            }

            if (across <= 0.0001f || along <= 0.0001f) return;

            instance.transform.localScale *= Mathf.Max(deck / across, span / along);

            var scaled = Measure(instance);
            instance.transform.position += new Vector3(0f, groundY - scaled.min.y, 0f);
        }

        /// <summary>
        /// Widens a model across one horizontal direction without lengthening it.
        ///
        /// <b>The one place a non-uniform scale is right, and it has to pick its axis
        /// carefully.</b> FitToCrossing scales uniformly and takes the larger of the two
        /// demands, so asking for a wider deck asks for a longer bridge: a model running
        /// 4.2 parts long to one across needed twenty-one metres of span before it was
        /// five metres wide, over a crossing that is twelve. Fitting by span alone gets
        /// the length right and leaves the deck too narrow to drive a wagon over, and no
        /// uniform number satisfies both.
        ///
        /// The axis is found rather than assumed. The caller yaws the bridge onto the
        /// ford's bearing and lays a Z-up prefab down with a quarter turn about X, so
        /// which of the model's own axes points across the water depends on both — and
        /// scaling the wrong one squashes the bridge instead of widening it, which is what
        /// the old comment here warned about. Asking the transform to turn the world
        /// direction into its local frame answers it exactly, for any rotation and either
        /// prefab convention.
        /// </summary>
        public static void Widen(GameObject instance, float targetWidth, Vector3 acrossWorld,
                                 float groundY = 0f)
        {
            if (instance == null || targetWidth <= 0.0001f) return;

            // How wide it is now, along that direction, measured the way the model is
            // actually turned.
            //
            // <b>This read the world box's x or z, on the grounds that crossings are cut
            // square to their rivers.</b> The crossings are; the rivers are not. A bridge
            // laid at seventy-nine degrees has a box whose depth is mostly its length, and
            // the deck was divided down by it - see ExtentAlong.
            var across = acrossWorld.normalized;
            float now = ExtentAlong(instance, across);
            if (now <= 0.0001f) return;

            float factor = targetWidth / now;
            if (Mathf.Approximately(factor, 1f)) return;

            // Which of the model's own axes that direction is, in its own frame.
            var local = instance.transform.InverseTransformDirection(across);
            var scale = instance.transform.localScale;

            float ax = Mathf.Abs(local.x), ay = Mathf.Abs(local.y), az = Mathf.Abs(local.z);

            if (ax >= ay && ax >= az) scale.x *= factor;
            else if (ay >= az) scale.y *= factor;
            else scale.z *= factor;

            instance.transform.localScale = scale;

            var widened = Measure(instance);
            instance.transform.position += new Vector3(0f, groundY - widened.min.y, 0f);
        }

        /// <summary>
        /// Scales an instance so its widest horizontal dimension is
        /// <paramref name="targetWidth"/> metres, and stands it on the ground.
        ///
        /// For anything wider than it is tall — a ploughed field, a length of wall, a
        /// stack of logs — height is the wrong handle. Fitting a field two metres high
        /// scales it up by whatever factor its thin profile demands and lays a farm
        /// across a quarter of the map. The trees taught this once already: the grouped
        /// models had to be dropped from the scatter because normalising them by height
        /// stretched them sideways.
        /// </summary>
        public static void FitToFootprint(GameObject instance, float targetWidth, float groundY = 0f)
        {
            var bounds = Measure(instance);
            float widest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (widest > 0.0001f)
                instance.transform.localScale *= targetWidth / widest;

            var scaled = Measure(instance);
            float lift = groundY - scaled.min.y;
            instance.transform.position += new Vector3(0f, lift, 0f);
        }
    }
}
