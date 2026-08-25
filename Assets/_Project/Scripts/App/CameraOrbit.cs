using System;

namespace Arna.App
{
    /// <summary>
    /// Where the camera sits relative to the caravan: how far, how high, which side.
    ///
    /// Held as pitch, yaw and range rather than as an offset, because that is what the
    /// player's two gestures actually change — a pinch is range and a drag is the other
    /// two — and because the clamps that matter are on those, not on a vector.
    ///
    /// Deliberately free of UnityEngine so the arithmetic can be run and checked
    /// outside an editor, the way the rest of the simulation is.
    /// </summary>
    public sealed class CameraOrbit
    {
        /// <summary>
        /// The view everything in this game was calibrated against.
        ///
        /// 46 m back and 32 m up is 56 m away at 34.8° down, and every judgement in the
        /// design notes — how big a crow reads, how tall a deer should be, whether a
        /// silhouette survives — was made from here. Moving the camera is the player's
        /// to do; coming back to this is one button, because this is the view the game
        /// is balanced for.
        /// </summary>
        public const float DefaultPitch = 34.8f;
        public const float DefaultRange = 56f;

        /// <summary>
        /// Nearest the camera may come. Closer than this and the caravan fills the frame
        /// with no country around it, which is the one thing this game is about reading.
        /// </summary>
        public const float MinRange = 24f;

        /// <summary>
        /// Furthest. At ninety metres the column was already grey specks against
        /// nine-metre trees; past 120 the play view is a worse copy of the planning map,
        /// and the two screens are meant to answer different questions.
        /// </summary>
        public const float MaxRange = 120f;

        /// <summary>
        /// Flattest angle. Below this the horizon fills the frame and the ground the
        /// caravan is crossing is a sliver.
        /// </summary>
        public const float MinPitch = 12f;

        /// <summary>
        /// Steepest. Not 90: straight down *is* the planning map, and arriving at it by
        /// dragging would hand the player the overview screen without its overlay — and
        /// with it the terrain reading the whole design asks them to earn.
        /// </summary>
        public const float MaxPitch = 68f;

        public float Pitch { get; private set; } = DefaultPitch;
        public float Range { get; private set; } = DefaultRange;

        /// <summary>Degrees around the caravan, measured from directly behind it.</summary>
        public float Yaw { get; private set; }

        public bool IsDefault => Math.Abs(Pitch - DefaultPitch) < 0.05f
                                 && Math.Abs(Range - DefaultRange) < 0.05f
                                 && Math.Abs(Yaw) < 0.05f;

        /// <summary>Multiplies the range. A pinch is a ratio, not a distance.</summary>
        public void Zoom(float factor)
        {
            if (factor <= 0f) return;
            Range = Clamp(Range * factor, MinRange, MaxRange);
        }

        public void Orbit(float yawDegrees, float pitchDegrees)
        {
            Yaw = Wrap(Yaw + yawDegrees);
            Pitch = Clamp(Pitch + pitchDegrees, MinPitch, MaxPitch);
        }

        public void Reset()
        {
            Pitch = DefaultPitch;
            Range = DefaultRange;
            Yaw = 0f;
        }

        /// <summary>
        /// The camera's offset from the caravan, given which way the caravan faces.
        ///
        /// Yaw is relative to the heading rather than to the world, so a view chosen
        /// over the left flank stays over the left flank when the road turns. Absolute
        /// yaw would have the camera swing around the column at every bend, which is
        /// exactly the seasickness this kind of camera is usually blamed for.
        /// </summary>
        public void Offset(float headingX, float headingZ, out float x, out float y, out float z)
        {
            double heading = Math.Atan2(headingX, headingZ);
            double angle = heading + Math.PI + Yaw * Math.PI / 180.0;
            double pitch = Pitch * Math.PI / 180.0;

            float ground = (float)(Range * Math.Cos(pitch));
            x = (float)(Math.Sin(angle) * ground);
            z = (float)(Math.Cos(angle) * ground);
            y = (float)(Range * Math.Sin(pitch));
        }

        static float Clamp(float value, float low, float high)
            => value < low ? low : value > high ? high : value;

        static float Wrap(float degrees)
        {
            while (degrees > 180f) degrees -= 360f;
            while (degrees < -180f) degrees += 360f;
            return degrees;
        }
    }
}
