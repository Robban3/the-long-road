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

            if (_surfaces.Length == 0) return;

            _footprint = _surfaces[0].bounds;
            for (int i = 1; i < _surfaces.Length; i++) _footprint.Encapsulate(_surfaces[i].bounds);

            _deck = Sample(_footprint.center.x, _footprint.center.z);
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
            if (_surfaces == null || _surfaces.Length == 0 || float.IsNaN(_deck)) return false;

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
