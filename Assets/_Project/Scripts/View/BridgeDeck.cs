using UnityEngine;

namespace TheVeil.View
{
    /// <summary>
    /// Marks a bridge and answers how high its deck is at a given place.
    ///
    /// **Because nothing here knows what a bridge looks like.** The models come out of a
    /// pack that is not in this repository, so no rule written here can say where the
    /// deck sits inside one: a plank bridge's roadway is its top, an arched one's rises
    /// in the middle and meets the ground at both ends, and one with parapets has its
    /// top somewhere above the road. Two guesses were made about that and both were
    /// wrong, and the caravan drove under the arch each time.
    ///
    /// So the bridge is measured instead of described. A collider is fitted to whatever
    /// meshes the prefab turns out to have, and the height of the road at any point is
    /// found by dropping a ray onto it — which is exact for a plank, an arch or a
    /// drawbridge, and needs to know nothing about which it is.
    ///
    /// Only bridges get a collider, and the ray is cast at this one rather than at the
    /// scene, so a tree standing beside the crossing cannot answer for it.
    /// </summary>
    public sealed class BridgeDeck : MonoBehaviour
    {
        /// <summary>How far above the ground the ray starts. Taller than any bridge.</summary>
        const float Overhead = 40f;

        Collider[] _surfaces;
        Bounds _footprint;

        /// <summary>What the last Measure found, so a failure can say which step failed.</summary>
        public int Meshes { get; private set; }
        public int Surfaces => _surfaces == null ? 0 : _surfaces.Length;

        /// <summary>
        /// Whether the roadway was found by the ray rather than by the fallback.
        ///
        /// It decides one thing and it matters: only an exact measurement may be used to
        /// *move* the bridge. The caller drops the model until its roadway sits a quarter
        /// of a metre above the bank, and a roadway guessed a metre too high would bury
        /// the bridge to its parapet — which is the tunnel this class was written to stop
        /// happening a third time. A rough answer is good enough to lift the column onto
        /// a bridge that is standing where it was put; it is not good enough to decide
        /// where to put it.
        /// </summary>
        public bool Exact { get; private set; }

        Renderer[] _pieces;

        /// <summary>The roadway's height, measured once. NaN until <see cref="Measure"/> runs.</summary>
        float _deck = float.NaN;

        /// <summary>Fits colliders to the meshes and measures the roadway.</summary>
        public void Measure()
        {
            var filters = GetComponentsInChildren<MeshFilter>(true);
            var surfaces = new System.Collections.Generic.List<Collider>();

            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;

                var collider = filter.gameObject.GetComponent<MeshCollider>();
                if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();

                collider.sharedMesh = filter.sharedMesh;
                surfaces.Add(collider);
            }

            _surfaces = surfaces.ToArray();
            _deck = float.NaN;
            Exact = false;
            Meshes = filters.Length;

            // The footprint from the renderers rather than the colliders, because
            // renderer bounds need no physics, no collider and no read/write flag on the
            // mesh — they are there the moment the object is. Everything below can then
            // fail and the bridge still knows where it stands.
            _pieces = GetComponentsInChildren<Renderer>(true);
            if (_pieces.Length == 0) return;

            _footprint = _pieces[0].bounds;
            for (int i = 1; i < _pieces.Length; i++) _footprint.Encapsulate(_pieces[i].bounds);

            if (_surfaces.Length == 0)
            {
                Fallback();
                return;
            }

            // <b>Without this the ray misses and the bridge is never seated.</b> The
            // colliders are created, the bridge is turned, scaled and moved, and then it
            // is asked where its roadway is — all inside one call. Unity has not synced
            // transforms into the physics scene at that point, because
            // Physics.autoSyncTransforms is off by default in current versions, so every
            // collider is still where it was before any of that happened and a ray cast
            // at the finished bridge passes through nothing.
            //
            // The measurement came back NaN, Height then refused for the rest of the run,
            // GroundAt never lifted the column, and the caravan walked through the bridge
            // at ground level. It printed "roadway NaN m above the bank" every time and
            // nobody had read the line.
            Physics.SyncTransforms();

            _deck = Sample(_footprint.center.x, _footprint.center.z);
            Exact = !float.IsNaN(_deck);

            if (!Exact) Fallback();
        }

        /// <summary>
        /// The roadway from the pieces' own boxes, when the ray could not find it.
        ///
        /// <b>Because a bridge the column walks through is worse than a bridge it rides
        /// half a metre high.</b> The ray is exact and the ray is also fragile: it needs a
        /// MeshCollider, which needs a mesh, which on an imported model needs Read/Write
        /// enabled, and it needs the physics scene to have been told where any of it is.
        /// Any one of those missing gave NaN, and NaN meant Height refused, GroundAt never
        /// lifted anything, and the caravan crossed at ground level through the deck.
        ///
        /// A renderer's bounds need none of that. The highest box standing over the middle
        /// of the span is the roadway or the parapet above it — never the vault
        /// underneath, which is the one answer that would put the column back where it
        /// started. On a bridge built of several pieces the rails run along the edges and
        /// their boxes do not cover the centre line, so this lands on the deck itself; on
        /// a bridge that is one mesh it lands on the top of whatever that mesh is, which
        /// is high rather than wrong.
        ///
        /// Marked inexact, so the caller lifts the column but does not move the bridge.
        /// </summary>
        void Fallback()
        {
            float x = _footprint.center.x, z = _footprint.center.z;
            float top = float.NaN;

            foreach (var piece in _pieces)
            {
                if (piece == null) continue;

                var box = piece.bounds;
                if (x < box.min.x || x > box.max.x || z < box.min.z || z > box.max.z) continue;

                if (float.IsNaN(top) || box.max.y > top) top = box.max.y;
            }

            // Nothing over the middle at all: one flat piece off to the side, or a prefab
            // with no renderers under it. Take the whole thing's top and say so.
            _deck = float.IsNaN(top) ? _footprint.max.y : top;

            Debug.LogWarning($"[The Veil] {name}: the ray found no roadway "
                           + $"({Meshes} mesh(es), {Surfaces} collider(s)), so the deck is "
                           + $"taken from the model's own boxes at {_deck:0.00} m. The "
                           + "column rides over it; the bridge is left where it stands.");
        }

        /// <summary>
        /// The highest thing the bridge has directly above a point, or NaN for nothing.
        ///
        /// The highest hit rather than the first: a ray down the middle of an arch passes
        /// through the roadway and out through the vault under it, and the underside is
        /// the one answer that would put the caravan back where it started.
        ///
        /// Used once, from <see cref="Measure"/>, and never per frame — see
        /// <see cref="Height"/> for why.
        /// </summary>
        float Sample(float worldX, float worldZ)
        {
            var ray = new Ray(new Vector3(worldX, _footprint.max.y + Overhead, worldZ), Vector3.down);
            float top = float.NaN;

            foreach (var surface in _surfaces)
            {
                if (surface == null) continue;
                if (!surface.Raycast(ray, out var hit, Overhead * 4f)) continue;

                if (float.IsNaN(top) || hit.point.y > top) top = hit.point.y;
            }

            return top;
        }

        /// <summary>
        /// The height of the roadway above a point, or the ground's own height when the
        /// point is not on the bridge.
        ///
        /// <b>One height for the whole bridge, and that is the fix.</b> This used to cast
        /// a fresh ray at whatever point it was asked about and take the highest thing it
        /// hit. On a plank bridge with railings and posts the highest thing over a point
        /// near the edge is the *railing*, not the roadway — so anything walking along the
        /// bridge had its height jump between deck and rail several times a second. Both
        /// the troops and their reach rings read their height from here, which is exactly
        /// why the two blinked in step, and it is what put the wagons up in the air on the
        /// crossing.
        ///
        /// A roadway is flat enough over twelve metres to be one number. It is measured
        /// once, down the middle of the span where the road runs and the railings are not,
        /// and every point inside the footprint gets that number. A constant cannot
        /// oscillate, so the flicker has nowhere to come from — and the railings, posts and
        /// vault are simply never asked.
        ///
        /// What is left is a step at the edge of the footprint, from deck to ground. That
        /// is TerrainDecorator.DeckClearance, a quarter of a metre, and invisible.
        /// </summary>
        public bool Height(float worldX, float worldZ, float groundY, out float deck)
        {
            deck = groundY;
            if (float.IsNaN(_deck)) return false;

            // Cheap rejection first: most of the map is not a bridge.
            if (worldX < _footprint.min.x || worldX > _footprint.max.x ||
                worldZ < _footprint.min.z || worldZ > _footprint.max.z) return false;

            deck = _deck;
            return true;
        }

        /// <summary>The roadway's height, or NaN when nothing has been measured.</summary>
        public float Deck => _deck;
    }
}
