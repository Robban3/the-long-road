using UnityEngine;

namespace TheVeil.View
{
    /// <summary>
    /// A horse's legs, moved by code.
    ///
    /// The army pack's horses have a skeleton and not one clip to play on it, so the
    /// cavalry glided along the road with its legs locked. What the pack does have is
    /// four armoured horses the player can tell apart at a glance — mail, a nobleman's
    /// trappings, the knights' barding — which Quaternius's walking horse would have
    /// cost. So the legs are swung here instead: a four-beat walk at the caravan's pace,
    /// blending into a gallop as it quickens, the lower leg folding as the hoof comes
    /// forward, and the back, neck and tail following.
    ///
    /// Read off the skeleton rather than the figure: the swing axis is the horse's own
    /// left-to-right, taken from where its shoulders and head are, so a model saved
    /// facing some other way still steps forward and not sideways.
    ///
    /// Runs after the animator (LateUpdate) and sets each bone from its rest pose every
    /// frame, so it neither fights nor accumulates on top of whatever the rider's
    /// controller writes.
    /// </summary>
    public sealed class HorseGait : MonoBehaviour
    {
        /// <summary>Metres a second the horse is covering, set by whoever moves it.</summary>
        public float Speed;

        /// <summary>Metres covered by one full cycle of the legs, at a walk and flat out.</summary>
        public const float WalkStride = 1.8f;
        public const float GallopStride = 3.6f;

        /// <summary>Where the walk has become a gallop, in metres a second.</summary>
        public const float GallopFrom = 3.5f;
        public const float GallopBy = 6.5f;

        const float WalkSwing = 18f, GallopSwing = 38f;
        const float WalkFold = 32f, GallopFold = 58f;
        const float WalkRock = 1.2f, GallopRock = 5f;
        const float WalkNod = 3f, GallopNod = 8f;
        const float TailSway = 6f;

        /// <summary>Below this the horse is standing, not walking very slowly.</summary>
        const float Standing = 0.05f;

        sealed class Joint
        {
            public Transform Bone;
            public Quaternion Rest;

            public Joint(Transform bone)
            {
                Bone = bone;
                Rest = bone.localRotation;
            }

            public void Reset() => Bone.localRotation = Rest;

            /// <summary>Turned about a world axis, on top of the pose it was just reset to.</summary>
            public void Turn(float degrees, Vector3 axis)
                => Bone.rotation = Quaternion.AngleAxis(degrees, axis) * Bone.rotation;
        }

        sealed class Leg
        {
            public Joint Upper, Lower;

            /// <summary>Where in the cycle this leg is, at a walk and at a gallop.</summary>
            public float WalkPhase, GallopPhase;
        }

        Leg[] _legs;
        Joint _back, _neck;
        Joint[] _tail;
        float _cycle;

        /// <summary>
        /// Every bone as the model file left it, which for a mounted figure is a man in
        /// the saddle. Kept for <see cref="Seat"/>, which needs the rider's seat back
        /// after the animator has had it.
        /// </summary>
        readonly System.Collections.Generic.Dictionary<Transform, Pose> _bind =
            new System.Collections.Generic.Dictionary<Transform, Pose>();

        Transform[] _seat;

        /// <summary>The rider's bones that must not walk: his hips and both legs.</summary>
        static readonly HumanBodyBones[] SeatBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
        };

        /// <summary>
        /// Puts a gait on a figure that carries a horse, and nothing on anything else.
        /// Null when the bones are not all there — a walking man, a wolf, Quaternius's
        /// draught horse, which has clips of its own.
        /// </summary>
        public static HorseGait Fit(Transform figure)
        {
            if (figure == null) return null;

            var leftFore = Find(figure, "UpperArmLeft");
            var leftForeLow = Find(figure, "ForearmLeft");
            var rightFore = Find(figure, "UpperArmRight");
            // Spelt so in the pack's file, and the gait is no use a leg short.
            var rightForeLow = Find(figure, "ForarmRight") ?? Find(figure, "ForearmRight");
            var leftHind = Find(figure, "ThighLeftBack");
            var leftHindLow = Find(figure, "ShinLeftBack");
            var rightHind = Find(figure, "ThighRightBack");
            var rightHindLow = Find(figure, "ShinRightBack");

            if (leftFore == null || leftForeLow == null || rightFore == null || rightForeLow == null
                || leftHind == null || leftHindLow == null || rightHind == null || rightHindLow == null)
                return null;

            var gait = figure.gameObject.GetComponent<HorseGait>();
            if (gait == null) gait = figure.gameObject.AddComponent<HorseGait>();

            // A four-beat walk — left hind, left fore, right hind, right fore — and a
            // gallop that pairs the hinds and then the fores, each pair a little apart.
            gait._legs = new[]
            {
                Make(leftHind, leftHindLow, 0.00f, 0.00f),
                Make(leftFore, leftForeLow, 0.25f, 0.55f),
                Make(rightHind, rightHindLow, 0.50f, 0.10f),
                Make(rightFore, rightForeLow, 0.75f, 0.65f)
            };

            var back = Find(figure, "Middle") ?? Find(figure, "Back");
            var neck = Find(figure, "LowerNeck") ?? Find(figure, "Neck");
            gait._back = back != null ? new Joint(back) : null;
            gait._neck = neck != null ? new Joint(neck) : null;

            var tail = new System.Collections.Generic.List<Joint>();
            for (int i = 1; i <= 4; i++)
            {
                var bone = Find(figure, "Tail" + i);
                if (bone != null) tail.Add(new Joint(bone));
            }
            gait._tail = tail.ToArray();

            // Before any animator has run, so this is the saddle and not a stride.
            gait._bind.Clear();
            foreach (var bone in figure.GetComponentsInChildren<Transform>(true))
                gait._bind[bone] = new Pose(bone.localPosition, bone.localRotation);

            return gait;
        }

        /// <summary>
        /// Puts the rider back in the saddle.
        ///
        /// The riders play the foot soldiers' controller — there is no riding clip in
        /// the pack either — and a walk played on a seated man swings his legs through
        /// the horse's belly and bobs his hips out of the saddle. So after the animator
        /// has posed him, his hips and legs go back to the model's own seat and only
        /// what is above them keeps moving: the lance, the shield, the swing of a blow.
        ///
        /// Found through the humanoid avatar rather than by name, so it holds whatever a
        /// pack calls its thigh; nothing to do for a figure with no human on it.
        /// </summary>
        public void Seat()
        {
            if (_seat == null)
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman) return;

                var seat = new System.Collections.Generic.List<Transform>();
                foreach (var bone in SeatBones)
                {
                    var found = animator.GetBoneTransform(bone);
                    if (found != null && _bind.ContainsKey(found)) seat.Add(found);
                }
                _seat = seat.ToArray();
            }

            foreach (var bone in _seat)
            {
                var pose = _bind[bone];
                bone.localPosition = pose.position;
                bone.localRotation = pose.rotation;
            }
        }

        static Leg Make(Transform upper, Transform lower, float walk, float gallop)
            => new Leg { Upper = new Joint(upper), Lower = new Joint(lower), WalkPhase = walk, GallopPhase = gallop };

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>How far from a walk to a gallop this pace is, nought to one.</summary>
        public static float GallopShare(float speed)
            => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GallopFrom, GallopBy, speed));

        void LateUpdate()
        {
            Seat();
            Advance(Time.deltaTime);
        }

        /// <summary>Moves the legs on by a stretch of time at the current speed.</summary>
        public void Advance(float seconds)
        {
            if (_legs == null) return;

            float gallop = GallopShare(Speed);
            float stride = Mathf.Lerp(WalkStride, GallopStride, gallop);

            _cycle = Mathf.Repeat(_cycle + Speed * seconds / stride, 1f);
            Pose(_cycle, gallop, Speed > Standing ? 1f : 0f);
        }

        /// <summary>
        /// The legs at one point of the cycle, for a pace between a walk (0) and a gallop
        /// (1). Motion 0 is standing square. Public so a still picture can be taken at
        /// any point of the stride without running the game.
        /// </summary>
        public void Pose(float cycle, float gallop, float motion)
        {
            if (_legs == null) return;

            // The horse's own axes, out of its skeleton. Forward from the hindquarters to
            // the forequarters, flattened; the swing axis is its right hand.
            var hind = (_legs[0].Upper.Bone.position + _legs[2].Upper.Bone.position) * 0.5f;
            var fore = (_legs[1].Upper.Bone.position + _legs[3].Upper.Bone.position) * 0.5f;
            var forward = Vector3.ProjectOnPlane(fore - hind, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f) forward = transform.forward;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);

            float swing = Mathf.Lerp(WalkSwing, GallopSwing, gallop) * motion;
            float fold = Mathf.Lerp(WalkFold, GallopFold, gallop) * motion;

            foreach (var leg in _legs)
            {
                leg.Upper.Reset();
                leg.Lower.Reset();

                float phase = (cycle - Mathf.Lerp(leg.WalkPhase, leg.GallopPhase, gallop)) * Mathf.PI * 2f;

                // Positive about the right hand carries the hoof back: the leg sweeps
                // back along the ground and forward through the air, and it is on the way
                // forward (cosine above nought) that the lower leg folds up out of the way.
                leg.Upper.Turn(-Mathf.Sin(phase) * swing, right);
                leg.Lower.Turn(Mathf.Max(0f, Mathf.Cos(phase)) * fold, right);
            }

            // Twice a cycle, as the weight passes from one pair of legs to the other.
            float beat = Mathf.Sin(cycle * Mathf.PI * 4f);

            if (_back != null)
            {
                _back.Reset();
                _back.Turn(beat * Mathf.Lerp(WalkRock, GallopRock, gallop) * motion, right);
            }

            if (_neck != null)
            {
                _neck.Reset();
                _neck.Turn(-beat * Mathf.Lerp(WalkNod, GallopNod, gallop) * motion, right);
            }

            for (int i = 0; i < _tail.Length; i++)
            {
                _tail[i].Reset();
                float sway = Mathf.Sin(cycle * Mathf.PI * 2f - i * 0.6f) * TailSway * motion;
                _tail[i].Turn(sway, Vector3.up);
            }
        }
    }
}
