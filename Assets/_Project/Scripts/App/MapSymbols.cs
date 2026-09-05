using System.Collections.Generic;
using TheVeil.Sim;
using TheVeil.UI;
using TheVeil.View;
using UnityEngine;

namespace TheVeil.App
{
    /// <summary>
    /// The signs on the planning map that say what a thing is.
    ///
    /// The map is drawn from four hundred metres up, and from there a house and a ruin
    /// are the same brown smudge — you can see that something is built without being
    /// able to tell what. That is not a rendering problem to be solved by drawing the
    /// models better; a real map has never solved it that way. It puts a sign on the
    /// thing.
    ///
    /// So the decorator now says what it stood where (<see cref="Landmark"/>), and this
    /// draws a symbol over each: a gable for a house, a broken wall for a ruin, a
    /// crenellated shaft for a tower, a tent with smoke for a camp, a bird for a flock
    /// of crows. Told apart by silhouette rather than by colour, so they survive being
    /// small.
    ///
    /// One mesh, one material, one draw call — an atlas painted by <see cref="Pixels"/>
    /// and quads cut out of it. It lives in TheVeil.App because that is the one assembly
    /// that can see both the painting (TheVeil.UI) and the map (TheVeil.View).
    /// </summary>
    public static class MapSymbols
    {
        /// <summary>Metres across, on a map that is 256 metres wide.</summary>
        public const float Size = 13f;

        /// <summary>
        /// Metres a symbol's middle floats above the ground it marks.
        ///
        /// Reckoned from the bottom edge rather than the middle, which is what decides
        /// it. The quad is turned to face a camera looking down at fifty-five degrees, so
        /// half its height stands 6.5 × cos 55° ≈ 3.7 m below its middle: at fourteen the
        /// lowest corner is ten metres up, clear of the pines at eight and a half. Any
        /// less and a treetop pokes through the bottom of the sign.
        ///
        /// The height also throws the symbol up-screen by about its own height, which is
        /// what makes it read as a pin stuck in the thing rather than a sticker over it.
        /// </summary>
        public const float Lift = 14f;

        /// <summary>
        /// How far a second sign on the same tile is nudged aside, as a share of a
        /// symbol's half-width — so 1.5 is three quarters of a whole symbol.
        /// </summary>
        public const float Spread = 1.5f;

        static Sprite[] _sheet;
        static Material _material;

        /// <summary>The symbols, in the order <see cref="LandmarkKind"/> names them.</summary>
        static readonly LandmarkKind[] Drawn =
        {
            LandmarkKind.House, LandmarkKind.Farm, LandmarkKind.Watchtower,
            LandmarkKind.Ruin, LandmarkKind.Camp, LandmarkKind.Wreck,
            LandmarkKind.Bones, LandmarkKind.Totem, LandmarkKind.Castle
        };

        /// <summary>Index into the sheet for the crows, which are not a landmark.</summary>
        const int CrowSlot = 9;

        /// <summary>
        /// Paints the sheet once and packs it into one texture.
        ///
        /// Kept in a static rather than rebuilt per map: the pictures do not depend on
        /// the map, and repainting seven anti-aliased sprites every time a level is
        /// opened is work for nothing.
        /// </summary>
        static Sprite[] Sheet()
        {
            if (_sheet != null && _sheet[0] != null) return _sheet;

            // The material holds the old atlas, and the old atlas is what has just been
            // found gone. Dropped together or the symbols come back blank.
            _material = null;

            _sheet = Pixels.Pack("TheVeilMapSymbols",
                Pixels.House("SymbolHouse"),
                Pixels.Farm("SymbolFarm"),
                Pixels.Tower("SymbolTower"),
                Pixels.Ruin("SymbolRuin"),
                Pixels.Camp("SymbolCamp"),
                Pixels.Wreck("SymbolWreck"),
                Pixels.Bones("SymbolBones"),
                Pixels.Totem("SymbolTotem"),
                Pixels.Castle("SymbolCastle"),
                Pixels.Crow("SymbolCrow"));

            return _sheet;
        }

        /// <summary>
        /// The material the symbols are drawn with.
        ///
        /// Found by shader name at run time rather than wired into the scene as a
        /// serialized field, deliberately. A field would have to be filled in by
        /// `TheVeil → Refresh Scene Assets`, and forgetting to run that has produced
        /// several false bug reports in this project: the code changes, the saved scene
        /// does not, and nothing says so.
        /// </summary>
        static Material Paint()
        {
            if (_material != null) return _material;

            var shader = Shader.Find("TheVeil/MapSymbol");

            if (shader == null)
            {
                Debug.LogWarning("[The Veil] No TheVeil/MapSymbol shader — the planning map will "
                                 + "have no symbols on it.");
                return null;
            }

            _material = new Material(shader) { name = "TheVeilMapSymbols", hideFlags = HideFlags.DontSave };
            _material.SetTexture("_BaseMap", Sheet()[0].texture);

            return _material;
        }

        /// <summary>What to draw, and where.</summary>
        public struct Sign
        {
            public int Slot;
            public int Tile;

            public Sign(int slot, int tile)
            {
                Slot = slot;
                Tile = tile;
            }
        }

        /// <summary>The slot in the sheet for a landmark kind, or -1 for one with no sign.</summary>
        public static int SlotOf(LandmarkKind kind)
        {
            for (int i = 0; i < Drawn.Length; i++)
                if (Drawn[i] == kind) return i;

            // Cut timber has no symbol on purpose. It is scenery that says somebody works
            // this wood, not a thing a route is planned around, and a map with a sign on
            // every object on it is a map with no signs on it.
            return -1;
        }

        public static Sign Crows(int tile) => new Sign(CrowSlot, tile);

        /// <summary>
        /// Builds one mesh of camera-facing quads, or null when there is nothing to say.
        /// </summary>
        /// <param name="facing">
        /// The camera the quads are turned towards. The planning camera does not move —
        /// it is set once, four hundred metres back at fifty-five degrees — so this is
        /// resolved when the map is built rather than every frame.
        /// </param>
        public static Mesh Build(TileGrid grid, IReadOnlyList<Sign> signs, float heightScale,
                                 Quaternion facing)
        {
            if (grid == null || signs == null || signs.Count == 0) return null;

            var sheet = Sheet();

            var vertices = new List<Vector3>(signs.Count * 4);
            var uvs = new List<Vector2>(signs.Count * 4);
            var colors = new List<Color>(signs.Count * 4);
            var triangles = new List<int>(signs.Count * 6);

            Vector3 right = facing * Vector3.right * (Size * 0.5f);
            Vector3 up = facing * Vector3.up * (Size * 0.5f);

            // How many signs each tile has already been given.
            //
            // A trap site is bones *and* a totem driven into the ground beside them, and
            // both are recorded on the one tile the site occupies. Left alone the second
            // symbol lands exactly on top of the first and the map shows one sign where
            // there are two — which is the same failure as drawing no sign at all, only
            // harder to notice.
            var crowded = new Dictionary<int, int>();

            foreach (var sign in signs)
            {
                if (sign.Slot < 0 || sign.Slot >= sheet.Length) continue;
                if (sign.Tile < 0 || sign.Tile >= grid.TileCount) continue;

                grid.ToCoords(sign.Tile, out int x, out int y);

                crowded.TryGetValue(sign.Tile, out int already);
                crowded[sign.Tile] = already + 1;

                float wx = (x + 0.5f) * TileGrid.TileSize;
                float wz = (y + 0.5f) * TileGrid.TileSize;
                float wy = grid.SurfaceElevation(wx, wz) * heightScale + Lift;

                var at = new Vector3(wx, wy, wz);

                // Sideways along the screen rather than across the ground, so a pair
                // stands shoulder to shoulder from where the map is read. Three quarters
                // of a symbol apart: touching, plainly two things, and still over the one
                // piece of ground they both belong to.
                if (already > 0) at += right * (Spread * already);
                int v = vertices.Count;

                vertices.Add(at - right - up);
                vertices.Add(at + right - up);
                vertices.Add(at + right + up);
                vertices.Add(at - right + up);

                var rect = sheet[sign.Slot].rect;
                var texture = sheet[sign.Slot].texture;

                float u0 = rect.xMin / texture.width;
                float u1 = rect.xMax / texture.width;
                float v0 = rect.yMin / texture.height;
                float v1 = rect.yMax / texture.height;

                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u1, v1));
                uvs.Add(new Vector2(u0, v1));

                for (int k = 0; k < 4; k++) colors.Add(Color.white);

                triangles.Add(v + 0); triangles.Add(v + 2); triangles.Add(v + 1);
                triangles.Add(v + 0); triangles.Add(v + 3); triangles.Add(v + 2);
            }

            if (vertices.Count == 0) return null;

            var mesh = new Mesh { name = "TheVeilMapSymbols" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>Hangs a built symbol mesh under a parent, ready to draw.</summary>
        public static Transform Show(Mesh mesh, Transform parent)
        {
            var material = Paint();
            if (mesh == null || material == null) return null;

            var go = new GameObject("Symbols");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            // A sign on a map neither casts nor catches shadow. It is not in the world.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go.transform;
        }
    }
}
