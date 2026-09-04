using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TheVail.View
{
    /// <summary>
    /// Turns a wagon's wheels by the distance the wagon has covered.
    ///
    /// Nothing drives the caravan physically. The simulation assigns transforms, so
    /// there is no contact, no torque and nothing that would make a wheel turn on its
    /// own — which is why they slid. The spin is arithmetic instead, taken from the
    /// only quantity that is actually true: how far the wagon moved since the last
    /// frame. Driving it off time or off the caravan's nominal speed would drift out
    /// of step the first time the fen slows the column, or a wagon stops to fight.
    ///
    /// Nothing here knows what a wheel looks like. The axle is not read off the model:
    /// it is the wagon's own right, which is <c>cross(up, forward)</c> and therefore
    /// the axis a wheel rolling forward turns about. The radius is half the wheel's
    /// height in the world, because a wheel standing on the ground is as tall as it is
    /// wide — a measurement that survives however the part was exported: rotated,
    /// mirrored, scaled, pivot anywhere at all, including at the wagon's origin rather
    /// than at the hub.
    /// </summary>
    public sealed class WagonWheels
    {
        /// <summary>
        /// One wheel, and everything needed to put it back where it started.
        ///
        /// The rest pose is kept and restored on every turn rather than the rotation
        /// being applied incrementally. An incremental spin accumulates whatever the
        /// pivot is offset by, and a wheel whose pivot is not its hub walks out of its
        /// arch over a few hundred frames.
        /// </summary>
        struct Wheel
        {
            public Transform Transform;

            /// <summary>The hub, in the wagon's space, so it follows the wagon about.</summary>
            public Vector3 Hub;

            public Vector3 RestPosition;
            public Quaternion RestRotation;
            public float Radius;
        }

        readonly Transform _wagon;
        readonly List<Wheel> _wheels = new List<Wheel>();
        float _travelled;

        WagonWheels(Transform wagon) { _wagon = wagon; }

        /// <summary>How many wheels were found, which is the thing worth logging.</summary>
        public int Count => _wheels.Count;

        /// <summary>
        /// A wheel is any part with "wheel" in its name — the packs are consistent
        /// about it (<c>SM_Supply_Wagon_Wheel_1</c>, <c>SM_Covered_Wagon_Wheel_V2</c>),
        /// and the improvised cart names its cylinders the same way, so both roll.
        /// </summary>
        const string WheelMark = "wheel";

        /// <summary>
        /// Too small to be a wheel and too big to be one. A hub cap or a bolt picked up
        /// by the name match would spin at a radius that turns it into a blur; a whole
        /// wagon caught by a badly named parent would barely creep.
        /// </summary>
        const float MinRadius = 0.08f;
        const float MaxRadius = 4f;

        public static WagonWheels Fit(Transform wagon)
        {
            var wheels = new WagonWheels(wagon);
            if (wagon == null) return wheels;

            foreach (var part in wagon.GetComponentsInChildren<Transform>(true))
            {
                if (part == wagon) continue;
                if (part.name.IndexOf(WheelMark, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // A wheel inside a wheel would be turned twice — once by its parent
                // carrying it round, once by itself — at twice the speed.
                if (Inside(part, wagon, wheels)) continue;

                if (!Measure(part, out var hub, out float radius)) continue;
                if (radius < MinRadius || radius > MaxRadius) continue;

                wheels._wheels.Add(new Wheel
                {
                    Transform = part,
                    Hub = wagon.InverseTransformPoint(hub),
                    RestPosition = part.localPosition,
                    RestRotation = part.localRotation,
                    Radius = radius,
                });
            }

            return wheels;
        }

        static bool Inside(Transform part, Transform wagon, WagonWheels found)
        {
            for (var parent = part.parent; parent != null && parent != wagon; parent = parent.parent)
                foreach (var wheel in found._wheels)
                    if (wheel.Transform == parent) return true;

            return false;
        }

        /// <summary>
        /// The hub and the radius, from what is drawn rather than from the transform.
        ///
        /// World bounds on purpose: they already carry the scaling the model was fitted
        /// with, and the height of an upright wheel is its diameter whichever local
        /// axis the mesh happens to call its axle.
        /// </summary>
        static bool Measure(Transform part, out Vector3 hub, out float radius)
        {
            hub = Vector3.zero;
            radius = 0f;

            var renderers = part.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            hub = bounds.center;
            radius = bounds.size.y * 0.5f;
            return true;
        }

        /// <summary>
        /// Advances every wheel by <paramref name="metres"/> of ground covered.
        ///
        /// Each wheel is turned by its own radius, so the small wheels at the front of
        /// a cart turn faster than the tall ones behind them, which is both correct and
        /// the detail that makes the rolling read as rolling.
        /// </summary>
        public void Roll(float metres)
        {
            if (_wheels.Count == 0 || _wagon == null) return;

            _travelled += metres;

            // Wrapped, or a long level accumulates a number whose smallest float step
            // is bigger than one frame's turn, and the wheels quietly stop.
            if (_travelled > 1000f) _travelled -= 1000f;

            var axle = _wagon.right;

            foreach (var wheel in _wheels)
            {
                if (wheel.Transform == null) continue;

                wheel.Transform.localPosition = wheel.RestPosition;
                wheel.Transform.localRotation = wheel.RestRotation;

                float turn = Mathf.Repeat(_travelled / wheel.Radius * Mathf.Rad2Deg, 360f);
                wheel.Transform.RotateAround(_wagon.TransformPoint(wheel.Hub), axle, turn);
            }
        }

        /// <summary>The part names under a wagon, for when none of them was a wheel.</summary>
        public static string Parts(Transform wagon)
        {
            var names = new StringBuilder();

            foreach (var part in wagon.GetComponentsInChildren<Transform>(true))
            {
                if (part == wagon) continue;
                if (names.Length > 0) names.Append(", ");
                names.Append(part.name);
                if (names.Length > 400) { names.Append(" ..."); break; }
            }

            return names.Length == 0 ? "nothing" : names.ToString();
        }
    }
}
