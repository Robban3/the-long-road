using System.Collections.Generic;
using UnityEngine;

namespace Arna.View
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
        /// <summary>Metres a second. Fast enough to read as loosed, slow enough to see.</summary>
        public const float Speed = 45f;

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

        /// <summary>Length a shaft is scaled to, whatever the model measures.</summary>
        public const float ShaftLength = 0.85f;

        sealed class Shaft
        {
            public Transform Holder;
            public Vector3 From, To;
            public float Flight, Flown;
            public bool Live;
        }

        readonly Transform _parent;
        readonly GameObject _model;
        readonly List<Shaft> _shafts = new List<Shaft>();
        readonly int _capacity;

        public Volley(Transform parent, GameObject model, int capacity = 40)
        {
            _parent = parent;
            _model = model;
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

            shaft.Holder.gameObject.SetActive(true);
            shaft.Holder.position = from;
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

            var made = new Shaft { Holder = Build().transform };
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
        GameObject Build()
        {
            var holder = new GameObject("Arrow");
            holder.transform.SetParent(_parent, false);
            holder.SetActive(false);

            if (_model == null)
            {
                // No arrow in the packs: a dart, which at this size and speed is the same
                // picture. Better than no bowshot at all, and it says so in the console.
                var dart = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(dart.GetComponent<Collider>());

                dart.transform.SetParent(holder.transform, false);
                dart.transform.localScale = new Vector3(0.045f, 0.045f, ShaftLength);
                dart.GetComponent<Renderer>().sharedMaterial.color = new Color(0.35f, 0.26f, 0.16f);

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
