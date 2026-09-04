using System;

namespace TheVail.App
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
        /// The view everything in this game is calibrated against: 62 m away at the
        /// angle <see cref="PitchFor"/> gives that range, which is 41 m back and 46 m up.
        ///
        /// It used to be 56 m at 34.8°, and 34.8° is a low angle — the camera sat almost
        /// as far behind the column as above it. That was survivable while the caravan
        /// was one wagon and stopped being so once it was three with teams: you were
        /// looking at the *back* of the thing you were meant to be reading, and the
        /// country it was crossing was edge-on.
        ///
        /// Range is the slant distance, so raising the angle at a fixed range moves the
        /// camera closer to the column *and* higher above it at the same time, which is
        /// exactly the trade wanted here.
        /// </summary>
        public const float DefaultRange = 62f;

        public static readonly float DefaultPitch = PitchFor(DefaultRange);

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

        /// <summary>
        /// The angle the camera takes at each end of the zoom, and the reason zooming in
        /// stopped putting the player behind the caravan.
        ///
        /// A pinch used to change range at a fixed angle. At 34.8° and the closest range
        /// that put the camera 20 m behind the column and 14 m above it — level with the
        /// wagons, looking at their backs, with the road ahead hidden behind them. Zoom
        /// is not a dolly; **what a player means by "closer" is "let me look at this",
        /// and looking at something on the ground means looking down at it.**
        ///
        /// So the angle rides the range. Close in it is 62°, near enough overhead to see
        /// the whole column and the ground either side of it; far out it is 30°, low and
        /// wide, which is the view that shows where the road is going. A drag still moves
        /// the angle — it is kept as a trim on top of this rather than replacing it, so
        /// a player who has tilted the camera keeps that tilt through a pinch instead of
        /// having it snapped away.
        /// </summary>
        public const float SteepPitch = 62f;
        public const float ShallowPitch = 30f;

        /// <summary>The angle that goes with a range, before the player's own trim.</summary>
        public static float PitchFor(float range)
        {
            float t = (Clamp(range, MinRange, MaxRange) - MinRange) / (MaxRange - MinRange);
            return SteepPitch + (ShallowPitch - SteepPitch) * t;
        }

        /// <summary>How far the player has tilted the camera off the angle its range gives.</summary>
        float _trim;

        public float Pitch => Clamp(PitchFor(Range) + _trim, MinPitch, MaxPitch);

        public float Range { get; private set; } = DefaultRange;

        /// <summary>Degrees around the caravan, measured from directly behind it.</summary>
        public float Yaw { get; private set; }

        public bool IsDefault => Math.Abs(_trim) < 0.05f
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

            // Trimmed against the range's own angle, and clamped by where that angle can
            // still be pushed — so a drag has the same effect wherever the zoom is, and
            // dragging to the limit and pinching out does not leave the camera stuck flat.
            float wanted = Clamp(Pitch + pitchDegrees, MinPitch, MaxPitch);
            _trim = wanted - PitchFor(Range);
        }

        public void Reset()
        {
            _trim = 0f;
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
