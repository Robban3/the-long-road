using TheVeil.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheVeil.View
{
    /// <summary>
    /// The two ruts one wagon cuts behind it in the snow.
    ///
    /// A TrailRenderer per side, for the reason Volley gives for its arrows: it already
    /// knows how to lay a ribbon along a path that is only known a frame at a time. Turned
    /// to face the sky rather than the camera, so the ribbon lies on the ground like a
    /// rut instead of standing up like a streak.
    ///
    /// Not hung on the wagon. A trail under the wagon goes out with it, and a wagon lost
    /// to raiders would take its own tracks away — the one place a player would look
    /// for them. These belong to the scene and are only moved by the wagon; when it is
    /// gone they stop, and stay.
    /// </summary>
    public sealed class SnowTracks
    {
        /// <summary>
        /// Wider than a wheel: a rut is pushed out at the sides, and from 55 m up a
        /// wheel's own width comes out as a hairline.
        /// </summary>
        const float Width = 0.4f;

        /// <summary>Above the ground, or the ribbon and the terrain fight over the pixels.</summary>
        const float Lift = 0.06f;

        /// <summary>A point every 40 cm: smooth on a bend, a few hundred on a whole level.</summary>
        const float Spacing = 0.4f;

        /// <summary>
        /// Longer than any level. Tracks do not fill in while the caravan is still in
        /// sight of them, and a rut fading out behind the last wagon would read as the
        /// wagon floating.
        /// </summary>
        const float Seconds = 3600f;

        /// <summary>For a wagon whose wheels could not be found: about a cart's width.</summary>
        const float FallbackHalfTrack = 0.75f;

        /// <summary>
        /// Snow in the shade of its own walls: bluer and darker than the field, and see-
        /// through, so the ground's grain carries on inside it.
        ///
        /// Darker than it sounds. The field is lit to near white by the ground shader's
        /// grain, and 0.50 / 0.55 alpha disappeared into it; 0.30 at 0.8 read as rails.
        /// </summary>
        static readonly Color RutColor = new Color(0.34f, 0.38f, 0.46f, 0.7f);

        /// <summary>A trail aligned to its transform faces along +Z; this points +Z at the sky.</summary>
        static readonly Quaternion FaceUp = Quaternion.Euler(-90f, 0f, 0f);

        readonly TrailRenderer _left;
        readonly TrailRenderer _right;
        readonly float _halfTrack;
        readonly float _rearZ;

        /// <summary>Whether the ruts have been put under the wagon yet. See Follow.</summary>
        bool _laid;

        public SnowTracks(Transform parent, string name, WagonWheels wheels, Material material)
        {
            if (!wheels.Track(out _halfTrack, out _rearZ)) _halfTrack = FallbackHalfTrack;

            _left = Rut(parent, name + " rut L", material);
            _right = Rut(parent, name + " rut R", material);
        }

        /// <summary>
        /// Moves both ruts to under the wagon's rear wheels, on the ground.
        ///
        /// The ground is asked at each rut rather than taken from the wagon, because on a
        /// hillside the two wheels stand at different heights and one ribbon would hang in
        /// the air while the other went under.
        /// </summary>
        public void Follow(Transform wagon, RunVisuals world)
        {
            Put(_left, wagon.TransformPoint(new Vector3(-_halfTrack, 0f, _rearZ)), world);
            Put(_right, wagon.TransformPoint(new Vector3(_halfTrack, 0f, _rearZ)), world);

            // Built at the scene's origin, so the first move would draw a rut from there
            // to the wagon straight across the map.
            if (_laid) return;
            _left.Clear();
            _right.Clear();
            _laid = true;
        }

        /// <summary>The wagon is gone: what it has cut stays, and nothing more is added.</summary>
        public void Stop()
        {
            _left.emitting = false;
            _right.emitting = false;
        }

        static void Put(TrailRenderer rut, Vector3 point, RunVisuals world)
        {
            point.y = world.GroundAt(new Vec2(point.x, point.z)) + Lift;
            rut.transform.position = point;
        }

        static TrailRenderer Rut(Transform parent, string name, Material material)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.rotation = FaceUp;

            var rut = holder.gameObject.AddComponent<TrailRenderer>();
            rut.sharedMaterial = material;
            rut.time = Seconds;
            rut.widthMultiplier = Width;
            rut.minVertexDistance = Spacing;
            rut.alignment = LineAlignment.TransformZ;
            rut.numCapVertices = 0;
            rut.autodestruct = false;
            rut.shadowCastingMode = ShadowCastingMode.Off;
            rut.receiveShadows = false;

            var colour = new Gradient();
            colour.SetKeys(
                new[] { new GradientColorKey(RutColor, 0f), new GradientColorKey(RutColor, 1f) },
                new[] { new GradientAlphaKey(RutColor.a, 0f), new GradientAlphaKey(RutColor.a, 1f) });
            rut.colorGradient = colour;

            return rut;
        }
    }
}
