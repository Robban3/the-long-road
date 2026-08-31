using Arna.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arna.View
{
    /// <summary>
    /// The bright circle on the ground that says how far a troop group can hit.
    ///
    /// It is the one number in the fighting the player is asked to spend silver on and
    /// has never been shown. Reach decides which of six posts answers a pack coming out
    /// of the trees, whether the bows can help the rearguard, and what the range track
    /// buys — and until now all of it happened inside the simulation while the screen
    /// showed six groups of men swinging at things for no visible reason.
    ///
    /// Drawn as a ring of triangles whose vertices are each sampled against the ground,
    /// rather than as a flat disc laid on top of it. That distinction has been learned
    /// expensively here: a flat quad on undulating terrain is the artefact behind the
    /// grass discs, the water planes and the dirt patches, and a fifteen-metre circle
    /// spans enough hillside to sink into it at one side and float at the other. A ring
    /// that follows the ground reads as painted on it.
    /// </summary>
    public sealed class RangeRing
    {
        /// <summary>
        /// Points round the circle. Seventy-two is five degrees a step, which is smooth
        /// at any radius the game uses and is 144 vertices — nothing, next to a tree.
        /// </summary>
        public const int Segments = 72;

        /// <summary>How wide the band is, in metres. A line, not a disc.</summary>
        public const float Width = 0.5f;

        /// <summary>
        /// How far above the ground the band floats.
        ///
        /// Small enough to read as painted on and large enough to survive the difference
        /// between the sampled height field and the rendered mesh, which is a few
        /// centimetres on a steep tile.
        /// </summary>
        public const float Lift = 0.14f;

        readonly GameObject _host;
        readonly MeshRenderer _renderer;
        readonly Mesh _mesh;
        readonly Vector3[] _vertices = new Vector3[Segments * 2];
        readonly Color[] _colours = new Color[Segments * 2];

        Vec2 _centre;
        float _radius;
        Color _colour;
        bool _drawn;

        public RangeRing(Transform parent, string name, Material material)
        {
            _host = new GameObject(name);
            _host.transform.SetParent(parent, false);

            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();

            var triangles = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                int inner = i * 2, outer = inner + 1;
                int nextInner = ((i + 1) % Segments) * 2, nextOuter = nextInner + 1;

                triangles[i * 6 + 0] = inner;
                triangles[i * 6 + 1] = outer;
                triangles[i * 6 + 2] = nextOuter;
                triangles[i * 6 + 3] = inner;
                triangles[i * 6 + 4] = nextOuter;
                triangles[i * 6 + 5] = nextInner;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(triangles, 0);

            _host.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = _host.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public void Hide()
        {
            _host.SetActive(false);
            _drawn = false;
        }

        /// <summary>
        /// Whether anything about the ring has moved enough to be worth rebuilding it.
        ///
        /// Each rebuild samples the ground under seventy-two points, and the ground
        /// sampler asks every bridge on the map whether it is standing over one. That is
        /// cheap and it is not free, and a column at four metres a second does not need
        /// it sixty times a second.
        /// </summary>
        bool Moved(Vec2 centre, float radius, Color colour)
        {
            if (!_drawn) return true;

            float dx = centre.X - _centre.X, dz = centre.Y - _centre.Y;

            return dx * dx + dz * dz > 0.0225f
                || Mathf.Abs(radius - _radius) > 0.05f
                || Mathf.Abs(colour.a - _colour.a) > 0.01f
                || colour.r != _colour.r || colour.g != _colour.g || colour.b != _colour.b;
        }

        /// <summary>
        /// Lays the ring round a point at a given radius, following the ground.
        ///
        /// The vertices are world positions and the object never moves, which saves
        /// transforming a hundred and forty-four points twice; the mesh is rewritten
        /// instead, and only when something about it has changed.
        /// </summary>
        public void Draw(Vec2 centre, float radius, Color colour, System.Func<Vec2, float> ground)
        {
            if (radius <= 0.05f) { Hide(); return; }

            _host.SetActive(true);

            if (!Moved(centre, radius, colour)) return;

            _centre = centre;
            _radius = radius;
            _colour = colour;
            _drawn = true;

            float inner = radius - Width * 0.5f;
            float outer = radius + Width * 0.5f;
            if (inner < 0.05f) inner = 0.05f;

            for (int i = 0; i < Segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / Segments);
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

                var on = new Vec2(centre.X + cos * radius, centre.Y + sin * radius);
                float y = ground(on) + Lift;

                _vertices[i * 2] = new Vector3(centre.X + cos * inner, y, centre.Y + sin * inner);
                _vertices[i * 2 + 1] = new Vector3(centre.X + cos * outer, y, centre.Y + sin * outer);

                _colours[i * 2] = colour;
                _colours[i * 2 + 1] = colour;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colours);
            _mesh.RecalculateBounds();
        }

        /// <summary>
        /// The material every ring shares: unlit, transparent, and vertex-coloured.
        ///
        /// Its own shader rather than URP's Unlit, for a reason that would otherwise cost
        /// an afternoon: <b>URP's Unlit shader ignores vertex colours.</b> Writing colours
        /// into the mesh does nothing at all there, and six rings that should differ would
        /// all come out white. Arna/RangeRing returns the vertex colour and nothing else,
        /// so every group's ring can have its own tint while they stay one material.
        ///
        /// Unlit because a mark on the ground is not a thing the sun shines on: lit, a
        /// ring on the shaded side of a hill would go out.
        /// </summary>
        public static Material Material()
        {
            var shader = Shader.Find("Arna/RangeRing");

            if (shader != null) return new Material(shader) { name = "RangeRing" };

            // The fallback is honest rather than pretty: without the shader the rings are
            // one flat colour, because URP's Unlit will not read the mesh's.
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return null;

            var material = new Material(unlit) { name = "RangeRing (no shader)" };

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;

            Debug.LogWarning("[Arna] Shader 'Arna/RangeRing' not found — the reach rings "
                             + "will all be the same colour.");
            return material;
        }
    }
}
