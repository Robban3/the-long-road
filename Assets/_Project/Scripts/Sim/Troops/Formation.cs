namespace Arna.Sim
{
    /// <summary>
    /// Where the individual figures of a group stand relative to the group's own point.
    ///
    /// The simulation fights with pooled health: one <see cref="TroopGroup"/> and one
    /// <see cref="TrackedEnemy"/> are each a single position and a single pool, which is
    /// what keeps twelve groups affordable at 20 Hz on a phone. Nothing about that
    /// requires the group to be <i>drawn</i> as one figure, and drawing it that way was
    /// a mistake with a name: a wolf pack is five wolves, and the screen showed one.
    ///
    /// So the offsets live here, in the simulation, rather than in the view. They are a
    /// pure function of index and heading — no state, no allocation, no engine — which
    /// means the renderer, the map and a test all get the same answer, and a figure
    /// keeps its place in the group from one frame to the next.
    /// </summary>
    public static class Formation
    {
        /// <summary>
        /// Sideways gap between two attackers in a pack, in metres.
        ///
        /// A wolf is about a metre and a fifth long and a half metre across. At one and
        /// a half the pack read as a single animal with too many legs; at two and a
        /// quarter a pack of five spanned nine metres and had its wings standing behind
        /// troops it was not fighting. Between them the five-wolf wedge is seven metres
        /// across, which is a little wider than the six-metre formation radius: the pack
        /// closes around one post rather than engulfing three.
        /// </summary>
        public const float PackSpacing = 1.8f;

        /// <summary>How far each rank of a wedge trails the one in front of it.</summary>
        public const float PackDepth = 1.5f;

        /// <summary>
        /// How far a hunting pack stands from what it is killing, in metres.
        ///
        /// Two and a half. A wolf's reach is 2 m (`EnemyTable.AttackRange`), so this puts
        /// the ring close enough that every animal on it is at the fight rather than
        /// walking toward it, and far enough that they are not standing inside the man
        /// they are biting.
        /// </summary>
        public const float PackRing = 2.5f;

        /// <summary>
        /// How much of a circle a pack closes around its quarry, as a fraction.
        ///
        /// Five sixths, not the whole circle. A pack that surrounds a target perfectly
        /// has one animal directly behind it and reads as a diagram; leaving a sixth
        /// open puts the gap on the far side, which is both what a real pack does — the
        /// quarry is driven, not enclosed — and what lets the player see the fight
        /// happening rather than a wheel of backs.
        /// </summary>
        public const float PackArc = 5f / 6f;

        /// <summary>Sideways gap between two models of a troop group holding one post.</summary>
        public const float LineSpacing = 1.3f;

        /// <summary>
        /// How far every other model of a line stands back from its neighbours.
        ///
        /// A perfectly straight rank of four looks like a parade, which is the opposite
        /// of the thing being drawn. Half a metre is enough to break it and too little
        /// to read as two ranks.
        /// </summary>
        public const float LineStagger = 0.5f;

        /// <summary>
        /// A wedge: the first figure at the point, the rest fanning back in pairs.
        ///
        /// Index zero sits exactly on the group's position, so the group point is the
        /// nose of the pack rather than its middle. That matters because the simulation
        /// halts an attacker at its reach from the troop it is closing on: put the
        /// wedge's centre there and the lead animal is a metre and a half short of the
        /// fight it is supposedly in.
        /// </summary>
        /// <summary>
        /// A pack around its quarry: the members spread over an arc centred on the way
        /// the group is facing.
        ///
        /// **This is the difference between a pack and a queue.** A wedge is right while
        /// the pack is running — the lead animal at the point, the rest fanning back —
        /// and wrong the moment it arrives, because a wedge is one animal deep at the
        /// front. Five wolves in a wedge means one wolf reaches the troop and four wait
        /// their turn a metre and a half behind it, which on screen is a queue: the thing
        /// the player was told is a pack, arriving one at a time.
        ///
        /// Around the quarry, all five are at the fight at once, and the flanks the
        /// wolf is designed to punish (docs/GDD.md §7.1) are being punished by something
        /// visibly standing on them.
        ///
        /// The offsets are relative to the group's own point, which the simulation has
        /// already halted at its reach from the troop — so the ring is drawn around the
        /// pack's nose rather than around the target, and <see cref="PackRing"/> is what
        /// carries it the rest of the way.
        /// </summary>
        public static Vec2 Ring(int index, int count, float forwardX, float forwardY,
                                float radius = PackRing)
        {
            if (count <= 1) return Vec2.Zero;

            // Centred on the heading, so the arc opens away from the quarry rather than
            // starting at an arbitrary compass point and rotating with nothing.
            float heading = (float)System.Math.Atan2(forwardY, forwardX);
            float sweep = PackArc * 2f * (float)System.Math.PI;

            float step = sweep / count;
            float angle = heading - sweep * 0.5f + step * (index + 0.5f);

            // Forward of the group's point, not around it: the pack is closing on
            // something ahead of it, and the ring's centre is that something.
            float centreX = forwardX * radius;
            float centreY = forwardY * radius;

            return new Vec2(centreX + (float)System.Math.Cos(angle) * radius,
                            centreY + (float)System.Math.Sin(angle) * radius);
        }

        public static Vec2 Wedge(int index, float forwardX, float forwardY,
                                 float spacing = PackSpacing, float depth = PackDepth)
        {
            if (index <= 0) return Vec2.Zero;

            int rank = (index + 1) / 2;
            float side = (index & 1) == 1 ? 1f : -1f;

            return Offset(forwardX, forwardY, side * rank * spacing, -rank * depth);
        }

        /// <summary>
        /// A line abreast, centred on the group's position and facing the same way.
        ///
        /// <paramref name="count"/> is the group's full complement rather than its
        /// survivors, so a figure keeps its place as the group is whittled down. The
        /// dead simply stop being drawn and leave a gap where they stood, which is a
        /// more honest picture than four models closing ranks into three.
        /// </summary>
        public static Vec2 Line(int index, int count, float forwardX, float forwardY,
                                float spacing = LineSpacing, float stagger = LineStagger)
        {
            if (count <= 1) return Vec2.Zero;

            float lateral = (index - (count - 1) * 0.5f) * spacing;
            float back = (index & 1) == 1 ? -stagger : 0f;

            return Offset(forwardX, forwardY, lateral, back);
        }

        /// <summary>
        /// Turns a right/forward offset into world metres, given a heading.
        ///
        /// Right of a heading is (heading.Y, -heading.X): the same handedness Unity's
        /// look rotation uses, so a figure placed to the right of the group is on the
        /// right of the picture and not behind the camera.
        /// </summary>
        static Vec2 Offset(float forwardX, float forwardY, float right, float forward)
        {
            float length = Vec2.Distance(Vec2.Zero, new Vec2(forwardX, forwardY));

            // A group with no heading — nothing in sight and standing still — still has
            // to put its models somewhere, and north is as good as any.
            if (length < 0.0001f) { forwardX = 0f; forwardY = 1f; }
            else { forwardX /= length; forwardY /= length; }

            return new Vec2(
                forwardY * right + forwardX * forward,
                -forwardX * right + forwardY * forward);
        }
    }
}
