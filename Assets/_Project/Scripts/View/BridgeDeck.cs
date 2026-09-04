using UnityEngine;

namespace TheVail.View
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

        /// <summary>Fits colliders to the meshes and remembers the ground it covers.</summary>
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

            if (_surfaces.Length == 0) return;

            _footprint = _surfaces[0].bounds;
            for (int i = 1; i < _surfaces.Length; i++) _footprint.Encapsulate(_surfaces[i].bounds);
        }

        /// <summary>
        /// The height of the roadway above a point, or the ground's own height when the
        /// point is not on the bridge.
        ///
        /// The highest hit wins. A ray down the middle of an arch passes through the
        /// roadway and out through the vault under it, and the underside is the one
        /// answer that would put the caravan back where it started.
        /// </summary>
        public bool Height(float worldX, float worldZ, float groundY, out float deck)
        {
            deck = groundY;
            if (_surfaces == null || _surfaces.Length == 0) return false;

            // Cheap rejection first: most of the map is not a bridge.
            if (worldX < _footprint.min.x || worldX > _footprint.max.x ||
                worldZ < _footprint.min.z || worldZ > _footprint.max.z) return false;

            var ray = new Ray(new Vector3(worldX, groundY + Overhead, worldZ), Vector3.down);
            bool found = false;

            foreach (var surface in _surfaces)
            {
                if (surface == null) continue;
                if (!surface.Raycast(ray, out var hit, Overhead * 2f)) continue;

                if (found && hit.point.y <= deck) continue;

                deck = hit.point.y;
                found = true;
            }

            return found;
        }
    }
}
