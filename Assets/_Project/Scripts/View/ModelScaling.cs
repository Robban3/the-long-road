using UnityEngine;

namespace Arna.View
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
        /// </summary>
        public static void FitToCrossing(GameObject instance, float deck, float span,
                                         float groundY = 0f)
        {
            var bounds = Measure(instance);

            float across = Mathf.Min(bounds.size.x, bounds.size.z);
            float along = Mathf.Max(bounds.size.x, bounds.size.z);
            if (across <= 0.0001f || along <= 0.0001f) return;

            instance.transform.localScale *= Mathf.Max(deck / across, span / along);

            var scaled = Measure(instance);
            instance.transform.position += new Vector3(0f, groundY - scaled.min.y, 0f);
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
