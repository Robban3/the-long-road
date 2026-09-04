using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheVail.View
{
    /// <summary>
    /// Arrows in the air.
    ///
    /// The bows have always worked and have never been visible. An archer group twenty
    /// metres from a pack applies its damage per second to whatever it can reach, and on
    /// screen that was six figures standing still while something died in the distance —
    /// the one troop type whose whole point is that it fights at a distance was the one
    /// with nothing to show for it.
    ///
    /// These are decoration and are meant to be: the arrows carry no damage and hitting
    /// nothing changes nothing. The fighting is continuous and the shafts are a picture
    /// of it, which is the right way round — a projectile that had to connect would put
    /// the balance at the mercy of the frame rate and of whether a wolf happened to walk
    /// out from under one.
    ///
    /// Pooled, because a level fires a few thousand of them and instantiating a
    /// GameObject for each is the kind of thing that shows up on a phone.
    /// </summary>
    public sealed class Volley
    {
        /// <summary>
        /// Metres a second, and deliberately not a bowshot's.
        ///
        /// 28, against a real arrow's fifty to sixty. <b>A real arrow is invisible</b>,
        /// which is the fact the first number missed: the eye follows a moving thing by
        /// how many of its own lengths it crosses in a second, and a 0.75 m shaft at
        /// 50 m/s crosses sixty-seven of them. Nobody sees that in life either — what a
        /// person actually sees at an archery butt is a bow, a pause, and a thud.
        ///
        /// Ten or twelve lengths a second is about where the eye starts tracking rather
        /// than inferring. At <see cref="ShaftLength"/> = 2.6 m that is 26 to 31 m/s, so
        /// 28. Over an archer's twenty-two metres it makes the flight eight tenths of a
        /// second, which is long enough to watch a shaft leave, arc and land.
        ///
        /// It is tied to the length above and should move with it: draw the shaft bigger
        /// and this has to rise, or the volley turns to treacle.
        /// </summary>
        public const float Speed = 28f;

        /// <summary>
        /// How high the shot arcs, as a share of how far it goes.
        ///
        /// An eighth. A flat line between two points reads as a laser; a real bowshot at
        /// twenty metres rises about two, which is what this gives.
        /// </summary>
        public const float Arc = 0.12f;

        /// <summary>How long an arrow that reached its mark stays stuck in the ground.</summary>
        public const float Linger = 0.5f;

        /// <summary>Metres up the bow is held, and where on its target a shot is aimed.</summary>
        public const float FromHeight = 1.35f;
        public const float ToHeight = 0.8f;

        /// <summary>
        /// How long the streak behind a shaft lasts, in seconds, and how wide it starts.
        ///
        /// The size and the speed made a shaft you could see; the streak is what makes it
        /// a *bowshot*. A moving object with nothing behind it has to be caught in the
        /// act, and the eye keeps losing one against grass and trees at this range. A
        /// tail is the thing that is still there a moment after the arrow has gone past,
        /// so the shot registers even when the shaft itself did not.
        ///
        /// Fifteen hundredths of a second, which at <see cref="Speed"/> is about four
        /// metres — a shaft and a half. Long enough to read as a line through the air,
        /// short enough that it is a trace and not a rope: at half a second the volley
        /// turns into a cat's cradle strung between the archers and the wolves, which is
        /// worse than the invisibility it was meant to fix.
        ///
        /// Width tapers to nothing on its own; only the head needs a number, and it is
        /// the shaft's own thickness so the streak leaves the arrow rather than sitting
        /// around it.
        /// </summary>
        public const float TrailSeconds = 0.15f;
        public const float TrailWidth = Thickness;

        /// <summary>
        /// The colour of the streak, head and tail.
        ///
        /// Pale and thin rather than bright. It is drawn over grass, trees and the troops
        /// themselves, and a saturated tail at this width is a scratch on the lens; what
        /// is wanted is the suggestion of disturbed air. Alpha does nearly all the work
        /// and it ends at nothing, so the trace dissolves instead of being cut off.
        /// </summary>
        public static readonly Color TrailHead = new Color(1f, 0.97f, 0.88f, 0.45f);
        public static readonly Color TrailTail = new Color(1f, 0.94f, 0.80f, 0f);

        /// <summary>How far apart a rank's shafts land, per shooter, in metres.</summary>
        // A metre and a half. Enough that three arrows into one pack read as three, and
        // small enough that they are all plainly aimed at the same thing.
        public const float Fan = 1.5f;

        /// <summary>
        /// Length a shaft is drawn at, whatever the model measures.
        ///
        /// <b>2.6 m against a real arrow's 0.75, and the exaggeration is the point.</b>
        /// The bows fired from the day the volley was written and nobody could see it,
        /// which reads exactly like nothing being fired at all — so it was reported as
        /// missing rather than as small, and that is the shape of the mistake.
        ///
        /// It is arithmetic, not taste. The camera sits 40 m back and 47 up
        /// (LevelRunner.FollowDistance / FollowHeight), so it watches from about 62 m. A
        /// life-size shaft there is nine tenths of a degree long and four hundredths of a
        /// degree thick — on a 1080-wide phone, roughly thirty pixels by one and a half,
        /// dark brown against grass, crossing the screen in half a second. There is no
        /// contrast trick that rescues one and a half pixels.
        ///
        /// The same argument the eagle already won, and by the same factor: she is drawn
        /// at a ten-metre wingspan against a real bird's two because at life size she is
        /// a speck over a 256 m map (see VisualLibrary.EagleSpan). Three times over puts
        /// a shaft at about ninety pixels by five, which is an arrow.
        ///
        /// Scale it back towards life if the camera is ever brought in close. This number
        /// is a function of the viewing distance and of nothing else.
        /// </summary>
        public const float ShaftLength = 2.6f;

        sealed class Shaft
        {
            public Transform Holder;
            public TrailRenderer Trail;
            public Vector3 From, To;
            public float Flight, Flown;
            public bool Live;
        }

        /// <summary>How thick the fallback dart is drawn, in metres.</summary>
        // A thirtieth of its length, which is about what a real shaft with its fletching
        // measures. The model, when there is one, keeps its own proportions.
        public const float Thickness = ShaftLength / 30f;

        readonly Transform _parent;
        readonly GameObject _model;
        readonly Material _trail;
        Material _dart;
        static bool _warned;
        readonly List<Shaft> _shafts = new List<Shaft>();
        readonly int _capacity;

        // Sixty-four rather than forty. Each shaft is alive for its flight plus Linger —
        // at the slower speed above that is a second and a bit — and every archer in a
        // rank now looses its own, so five ranged posts can have thirty-odd in the air at
        // once. A full pool drops shafts silently, which reads as a bow that missed a
        // turn.
        /// <param name="trail">
        /// The material every shaft's tail is drawn with. Passed in rather than made here
        /// so it is the *same* material as the reach rings' — they want the identical
        /// shader for identical reasons, and two instances of a shader with no properties
        /// is two draw-call batches where there should be one. Null falls back to making
        /// one, so a caller that has none still gets tails.
        /// </param>
        public Volley(Transform parent, GameObject model, Material trail = null,
                      int capacity = 64)
        {
            _parent = parent;
            _model = model;
            _trail = trail != null ? trail : RangeRing.Material();
            _capacity = capacity;
        }

        public int Flying
        {
            get
            {
                int flying = 0;
                foreach (var shaft in _shafts) if (shaft.Live) flying++;
                return flying;
            }
        }

        /// <summary>Looses one shaft. Ignored, rather than queued, when the pool is full.</summary>
        public void Loose(Vector3 from, Vector3 to)
        {
            var shaft = Free();
            if (shaft == null) return;

            shaft.From = from;
            shaft.To = to;
            shaft.Flown = 0f;
            shaft.Flight = Mathf.Max(0.08f, Vector3.Distance(from, to) / Speed);
            shaft.Live = true;

            // Placed before it is switched on, and cleared after. A pooled trail keeps
            // the points it laid down last time, so a shaft reused across the map draws a
            // streak from where the previous one landed to where this one starts — a
            // white line straight through the caravan, once per reuse.
            shaft.Holder.position = from;
            shaft.Holder.gameObject.SetActive(true);

            if (shaft.Trail != null)
            {
                shaft.Trail.Clear();
                shaft.Trail.emitting = true;
            }
        }

        /// <summary>
        /// Moves every shaft in the air along its arc and turns it to point the way it is
        /// going, so an arrow noses over as it falls.
        /// </summary>
        public void Advance(float deltaTime)
        {
            foreach (var shaft in _shafts)
            {
                if (!shaft.Live) continue;

                shaft.Flown += deltaTime;

                // Past its mark it stays where it landed for a moment rather than winking
                // out mid-air, which is what a volley looks like from behind: shafts in
                // the ground where the last one went.
                if (shaft.Flown >= shaft.Flight + Linger)
                {
                    shaft.Live = false;
                    shaft.Holder.gameObject.SetActive(false);
                    continue;
                }

                // Landed: the shaft stands in the ground and the streak it came in on
                // fades off it over TrailSeconds, rather than hanging there whole.
                if (shaft.Trail != null && shaft.Flown >= shaft.Flight)
                    shaft.Trail.emitting = false;

                float t = Mathf.Clamp01(shaft.Flown / shaft.Flight);

                var at = Along(shaft, t);
                shaft.Holder.position = at;

                // Aimed a hair ahead of itself rather than at its target: an arrow points
                // along its flight, and near the top of the arc that is not the same line.
                var ahead = Along(shaft, Mathf.Min(1f, t + 0.04f));
                var heading = ahead - at;

                if (heading.sqrMagnitude > 0.0001f)
                    shaft.Holder.rotation = Quaternion.LookRotation(heading, Vector3.up);
            }
        }

        static Vector3 Along(Shaft shaft, float t)
        {
            var flat = Vector3.Lerp(shaft.From, shaft.To, t);
            float rise = Vector3.Distance(shaft.From, shaft.To) * Arc * 4f * t * (1f - t);

            return flat + Vector3.up * rise;
        }

        public void Clear()
        {
            foreach (var shaft in _shafts)
            {
                shaft.Live = false;
                if (shaft.Holder != null) shaft.Holder.gameObject.SetActive(false);
            }
        }

        Shaft Free()
        {
            foreach (var shaft in _shafts)
                if (!shaft.Live) return shaft;

            if (_shafts.Count >= _capacity) return null;

            var holder = Build().transform;
            var made = new Shaft { Holder = holder, Trail = Streak(holder) };
            _shafts.Add(made);

            return made;
        }

        /// <summary>
        /// One arrow: a holder whose forward is the flight, with the model turned inside
        /// it to lie along that.
        ///
        /// Which way an arrow prefab points is the artist's business, so it is measured —
        /// the longest axis of the mesh is the shaft — and turned onto the holder's +Z.
        /// A rule written from one pack's arrow would be wrong for the next one's.
        /// </summary>
        /// <summary>
        /// The tail behind one shaft.
        ///
        /// A TrailRenderer rather than anything hand-built: it is the one piece of Unity
        /// that already knows how to lay a ribbon along a path that is only known a frame
        /// at a time, and the arc here is exactly that.
        ///
        /// It shares the reach rings' material (see <see cref="RangeRing.Material"/>),
        /// which is the same shader this wants for the same three reasons — unlit, so a
        /// streak on the shaded side of a hill does not go out; transparent; and
        /// vertex-coloured, which is what carries the gradient. <b>URP's own Unlit
        /// ignores vertex colours</b>, so without that shader the tail comes out one flat
        /// opaque band whatever the gradient says.
        /// </summary>
        TrailRenderer Streak(Transform holder)
        {
            if (_trail == null) return null;

            var trail = holder.gameObject.AddComponent<TrailRenderer>();

            // sharedMaterial, not material. Renderer.material *instantiates a copy* on
            // first touch, so assigning it here would quietly make sixty-four materials
            // out of the one passed in — the opposite of why it is passed in — and break
            // batching for every shaft in the air. RangeRing assigns the same way.
            trail.sharedMaterial = _trail;
            trail.time = TrailSeconds;
            trail.startWidth = TrailWidth;
            trail.endWidth = 0f;
            trail.emitting = false;
            trail.autodestruct = false;

            // Every 20 cm rather than every frame: at 28 m/s a frame is half a metre, so
            // this costs nothing on a fast machine and keeps the ribbon smooth on a slow
            // one, where a per-frame trail would come out as three long facets.
            trail.minVertexDistance = 0.2f;

            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(TrailHead, 0f),
                    new GradientColorKey(TrailTail, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(TrailHead.a, 0f),
                    new GradientAlphaKey(TrailTail.a, 1f)
                });

            trail.colorGradient = gradient;

            return trail;
        }

        GameObject Build()
        {
            var holder = new GameObject("Arrow");
            holder.transform.SetParent(_parent, false);
            holder.SetActive(false);

            if (_model == null)
            {
                // No arrow in the packs: a dart, which at this size and speed is the same
                // picture. Better than no bowshot at all.
                //
                // And said out loud, at last. The comment here has always promised the
                // console would mention it and the console never did, so an unassigned
                // arrow model and a working one were the same silence — which is a bad
                // way to spend an evening wondering why the archers look idle.
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[The Vail] No arrow model on the visual library, so the "
                                     + "volley is firing plain darts. Run The Vail > Set Up "
                                     + "Project — it assigns SM_Prop_Arrow_01, which is in "
                                     + "the project.");
                }

                var dart = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(dart.GetComponent<Collider>());

                dart.transform.SetParent(holder.transform, false);

                // Thickness with the length. A shaft drawn three times life size and left
                // life-size thick is a wire, and a wire is what nobody could see.
                dart.transform.localScale = new Vector3(Thickness, Thickness, ShaftLength);

                var skin = dart.GetComponent<Renderer>();

                // Its own material, made once and shared by every dart.
                //
                // Writing to `sharedMaterial.color` here — which is what this did — sets
                // the colour on the *default material every primitive Unity makes shares*.
                // One brown dart therefore turned every fallback cube, capsule and sphere
                // in the scene brown, which is a wide blast radius for a code path that
                // only runs when an art asset is missing.
                if (_dart == null)
                    _dart = new Material(skin.sharedMaterial)
                    {
                        name = "Dart",
                        color = new Color(0.35f, 0.26f, 0.16f)
                    };

                skin.sharedMaterial = _dart;

                return holder;
            }

            var arrow = Object.Instantiate(_model, holder.transform);
            var bounds = ModelScaling.Measure(arrow);

            // Longest axis onto +Z.
            if (bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z)
                arrow.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            else if (bounds.size.y >= bounds.size.z)
                arrow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest > 0.0001f) arrow.transform.localScale *= ShaftLength / longest;

            // Centred on the holder, so the shaft flies about its middle rather than
            // swinging round whichever end the pivot happens to sit on.
            var turned = ModelScaling.Measure(arrow);
            arrow.transform.position += holder.transform.position - turned.center;

            foreach (var collider in arrow.GetComponentsInChildren<Collider>(true))
                Object.Destroy(collider);

            return holder;
        }
    }
}
