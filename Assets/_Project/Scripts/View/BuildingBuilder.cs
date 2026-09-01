using Arna.Sim;
using UnityEngine;

namespace Arna.View
{
    /// <summary>
    /// The pieces a building is made of.
    ///
    /// PolygonKnights ships buildings as a kit and not as buildings: seven foundations,
    /// seven ground-floor rooms, seven upper rooms, seven roofs, five chimneys, and the
    /// castle towers as a base, a shaft and a top. Wiring a single piece into the scenery
    /// sets and calling it a house is what produced a lone mini tower standing on a lawn
    /// and, before that, a five-metre cart wheel: <b>a part of a thing is not a small
    /// version of the thing.</b>
    ///
    /// Sets are index-matched where the pack numbers them in parallel — foundation 3,
    /// room 3 and roof 3 belong to the same house — so one draw picks a style and the
    /// pieces agree with each other.
    /// </summary>
    [System.Serializable]
    public sealed class BuildingKit
    {
        public PropSet Foundations = new PropSet();
        public PropSet Rooms = new PropSet();

        /// <summary>Second storeys. Optional: a cottage is a room and a roof.</summary>
        public PropSet UpperRooms = new PropSet();

        public PropSet Roofs = new PropSet();
        public PropSet Chimneys = new PropSet();

        /// <summary>Castle towers, which come apart the same way: base, shaft, top.</summary>
        public PropSet TowerBases = new PropSet();
        public PropSet TowerShafts = new PropSet();
        public PropSet TowerTops = new PropSet();

        /// <summary>Free-standing stonework, for the walls a ruin has left.</summary>
        public PropSet Walls = new PropSet();

        /// <summary>
        /// Castle curtain, kept apart from <see cref="Walls"/>.
        ///
        /// Walls mixes dry stone in with castle courses, which is right for a ruin — what
        /// is left of a building is whatever stone was nearest — and wrong for a curtain.
        /// A castle ringed with field wall is a castle with a garden fence.
        /// </summary>
        public PropSet CurtainWalls = new PropSet();

        /// <summary>The archway a road goes through.</summary>
        public PropSet Gates = new PropSet();

        /// <summary>Fallen stone. What a building leaves behind when it stops being one.</summary>
        public PropSet Rubble = new PropSet();

        public bool CanBuildHouse => Foundations.Any && Rooms.Any && Roofs.Any;
        public bool CanBuildTower => TowerShafts.Any && TowerTops.Any;
        public bool CanBuildRuin => Rooms.Any || Walls.Any;
        public bool CanBuildCastle => CurtainWalls.Any && CanBuildTower;

        public bool IsEmpty => !CanBuildHouse && !CanBuildTower && !CanBuildRuin;
    }

    /// <summary>
    /// Stacks a kit into a building.
    ///
    /// Everything here is measured rather than tabled. Nothing in this repository knows
    /// how tall a Synty foundation is — the models are Git LFS pointers on the machine
    /// this was written on, and a table of heights copied out of Unity would be a second
    /// source of truth that goes stale the first time the pack is updated. So each piece
    /// is instantiated, its renderers are measured, and the next piece is seated on top
    /// of what came out. That is also the only version that works for a kit whose parts
    /// are not all the same size.
    ///
    /// Assembled at the origin, unrotated, and turned and scaled as one thing at the end:
    /// aligning children against a rotated parent is arithmetic nobody needs, and a
    /// building that is scaled after assembly keeps its proportions whatever the level
    /// asks it to be.
    /// </summary>
    public static class BuildingBuilder
    {
        /// <summary>
        /// How far each piece is sunk into the one below, as a share of its own height.
        ///
        /// Two percent. Synty's kit pieces are authored to butt exactly, and exactly is
        /// where a hairline of daylight shows between two of them at some camera angles.
        /// Enough to close that and far too little to see.
        /// </summary>
        public const float Seam = 0.02f;

        /// <summary>Chance a house has a second storey, and a chimney.</summary>
        public const float UpperStorey = 0.45f;
        public const float HasChimney = 0.75f;

        /// <summary>
        /// A house: foundation, room, sometimes an upper room, roof, sometimes a chimney.
        ///
        /// Returned unparented to the ground — the caller seats and scales it — so that
        /// this stays a builder and the decorator stays the thing that knows where
        /// buildings go.
        /// </summary>
        public static GameObject House(Transform parent, BuildingKit kit, DeterministicRandom rng)
        {
            if (kit == null || !kit.CanBuildHouse) return null;

            var host = new GameObject("House");
            host.transform.SetParent(parent, false);

            // One style, carried across the sets. The pack numbers its foundations, rooms
            // and roofs in parallel, so drawing separately would put a round roof on a
            // square room about six times in seven.
            int style = rng.Range(0, Length(kit.Rooms));
            float top = 0f;

            Stack(host.transform, Pick(kit.Foundations, style), ref top, kit.Foundations.ZUp);
            Stack(host.transform, Pick(kit.Rooms, style), ref top, kit.Rooms.ZUp);

            if (kit.UpperRooms.Any && rng.Chance(UpperStorey))
                Stack(host.transform, Pick(kit.UpperRooms, style), ref top, kit.UpperRooms.ZUp);

            var roof = Stack(host.transform, Pick(kit.Roofs, style), ref top, kit.Roofs.ZUp);

            if (roof != null && kit.Chimneys.Any && rng.Chance(HasChimney))
                Chimney(host.transform, kit, rng, roof);

            return host;
        }

        /// <summary>
        /// A castle tower: base, shaft, top.
        ///
        /// The watchtowers on the passes were the pack's two mini towers, which are whole
        /// pieces and were reported twice as standing on the grass. They are whole, and
        /// they are also small and always the same two. A tower built from the castle
        /// pieces is as tall as the pass wants and comes out different each time.
        /// </summary>
        public static GameObject Tower(Transform parent, BuildingKit kit, DeterministicRandom rng)
        {
            if (kit == null || !kit.CanBuildTower) return null;

            var host = new GameObject("Tower");
            host.transform.SetParent(parent, false);

            int style = rng.Range(0, Length(kit.TowerShafts));
            float top = 0f;

            if (kit.TowerBases.Any)
                Stack(host.transform, Pick(kit.TowerBases, style), ref top, kit.TowerBases.ZUp);

            Stack(host.transform, Pick(kit.TowerShafts, style), ref top, kit.TowerShafts.ZUp);
            Stack(host.transform, Pick(kit.TowerTops, style), ref top, kit.TowerTops.ZUp);

            return host;
        }

        /// <summary>
        /// What is left of a building: a foundation, the lower courses of a wall, and its
        /// stone lying around it.
        ///
        /// The room is buried rather than cut down. Scaling a wall to half its height
        /// squashes its windows and its stonework into something that reads as a model
        /// that went wrong; sinking it leaves the courses at their proper proportions and
        /// shows exactly as much of them as a ruin should have left. The rubble is what
        /// says the rest of it came down rather than was never built.
        /// </summary>
        public static GameObject Ruin(Transform parent, BuildingKit kit, DeterministicRandom rng)
        {
            if (kit == null || !kit.CanBuildRuin) return null;

            var host = new GameObject("Ruin");
            host.transform.SetParent(parent, false);

            int style = rng.Range(0, Mathf.Max(1, Length(kit.Rooms)));
            float top = 0f;

            if (kit.Foundations.Any)
                Stack(host.transform, Pick(kit.Foundations, style), ref top, kit.Foundations.ZUp);

            var standing = kit.Rooms.Any ? Pick(kit.Rooms, style) : Pick(kit.Walls, style);
            var wall = Stack(host.transform, standing, ref top, kit.Rooms.Any ? kit.Rooms.ZUp : kit.Walls.ZUp);

            if (wall != null)
            {
                // Down into the ground by half to three quarters of what it stands, so
                // what shows is the bottom of a wall and not a short house.
                var bounds = ModelScaling.Measure(wall);
                wall.transform.position -= new Vector3(0f, bounds.size.y * rng.Range(0.45f, 0.72f), 0f);
            }

            if (!kit.Rubble.Any) return host;

            var footprint = ModelScaling.Measure(host);
            float spread = Mathf.Max(footprint.extents.x, footprint.extents.z) + 1f;

            int stones = rng.Range(2, 5);
            for (int i = 0; i < stones; i++)
            {
                var stone = Object.Instantiate(Any(kit.Rubble, rng), host.transform);

                stone.transform.localPosition = new Vector3(rng.Range(-spread, spread), 0f,
                                                            rng.Range(-spread, spread));
                stone.transform.localRotation = kit.Rubble.ZUp
                    ? Quaternion.Euler(-90f, rng.Range(0f, 360f), 0f)
                    : Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

                var lying = ModelScaling.Measure(stone);
                stone.transform.position -= new Vector3(0f, lying.min.y, 0f);
            }

            return host;
        }

        /// <summary>
        /// A castle: four runs of curtain wall, a tower on each corner, a gate in the front.
        ///
        /// The one place on the map where something has always been missing. The caravan
        /// is escorted to the goal, and the goal is a differently coloured tile — the
        /// journey ends at a square of paint. This is what it ends at instead, and it is
        /// what the level roadmap has been climbing towards all along.
        ///
        /// Built facing +Z, so the gate is in the wall at the near end and the caller
        /// turns the whole thing to point that at the road. Assembled at the origin and
        /// unrotated like every other building here, for the reason the file gives at the
        /// top: aligning children inside a rotated parent is arithmetic nobody needs.
        ///
        /// How wide it comes out is not decided here. Two to three wall pieces a side,
        /// and the pieces' own measured width does the rest — nothing in this repository
        /// knows how long a Synty wall is, and a number written down here would be a
        /// second source of truth that goes stale the first time the pack is updated.
        /// </summary>
        public static GameObject Castle(Transform parent, BuildingKit kit, DeterministicRandom rng)
        {
            if (kit == null || !kit.CanBuildCastle) return null;

            var host = new GameObject("Castle");
            host.transform.SetParent(parent, false);

            int style = rng.Range(0, Length(kit.CurtainWalls));

            // Measured off a piece that is then thrown away, because the length of a run
            // has to be known before the run can be centred on the origin — and the only
            // way to know it is to instantiate one and look.
            float span = WallLength(host.transform, kit, style);
            if (span <= 0f) return host;

            int across = rng.Range(2, 4);
            int deep = rng.Range(2, 4);

            float halfX = across * span * 0.5f;
            float halfZ = deep * span * 0.5f;

            // The two side walls, run from the near corner to the far one.
            for (int i = 0; i < deep; i++)
            {
                float z = -halfZ + (i + 0.5f) * span;

                Run(host.transform, Pick(kit.CurtainWalls, style), kit.CurtainWalls.ZUp,
                    new Vector3(-halfX, 0f, z), 90f);
                Run(host.transform, Pick(kit.CurtainWalls, style), kit.CurtainWalls.ZUp,
                    new Vector3(halfX, 0f, z), 90f);
            }

            // The back wall, whole. The front wall with its middle piece left out for the
            // gate — a gateway is a hole in a wall, so the wall has to actually be short
            // a piece rather than have an arch parked in front of it.
            int gateAt = across / 2;

            for (int i = 0; i < across; i++)
            {
                float x = -halfX + (i + 0.5f) * span;

                Run(host.transform, Pick(kit.CurtainWalls, style), kit.CurtainWalls.ZUp,
                    new Vector3(x, 0f, halfZ), 0f);

                if (i == gateAt && kit.Gates.Any)
                    Run(host.transform, Any(kit.Gates, rng), kit.Gates.ZUp,
                        new Vector3(x, 0f, -halfZ), 0f);
                else
                    Run(host.transform, Pick(kit.CurtainWalls, style), kit.CurtainWalls.ZUp,
                        new Vector3(x, 0f, -halfZ), 0f);
            }

            // A tower on each corner, which is what stops the curtain reading as a fence.
            foreach (var corner in new[]
            {
                new Vector3(-halfX, 0f, -halfZ), new Vector3(halfX, 0f, -halfZ),
                new Vector3(-halfX, 0f, halfZ), new Vector3(halfX, 0f, halfZ)
            })
            {
                var tower = Tower(host.transform, kit, rng);
                if (tower != null) tower.transform.localPosition = corner;
            }

            return host;
        }

        /// <summary>
        /// How long one wall piece is, measured rather than assumed.
        ///
        /// Instantiated, measured and destroyed. The alternative is a constant, and a
        /// constant here would be wrong the moment somebody swaps the set — which is
        /// exactly the trap this file was written to avoid.
        ///
        /// The longer of the two ground axes, because a piece may be authored running
        /// along either.
        /// </summary>
        static float WallLength(Transform host, BuildingKit kit, int style)
        {
            var sample = Pick(kit.CurtainWalls, style);
            if (sample == null) return 0f;

            var probe = Object.Instantiate(sample, host);
            probe.transform.localRotation = kit.CurtainWalls.ZUp
                ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;

            var bounds = ModelScaling.Measure(probe);

            if (Application.isPlaying) Object.Destroy(probe);
            else Object.DestroyImmediate(probe);

            return Mathf.Max(bounds.size.x, bounds.size.z);
        }

        /// <summary>
        /// Lays one piece flat on the ground at a spot, turned to face along a wall.
        ///
        /// The horizontal counterpart of <see cref="Stack"/>: same measure-don't-table
        /// rule, same seating of the piece's own lowest point on the ground, but placed
        /// beside its neighbours instead of on top of them.
        /// </summary>
        static GameObject Run(Transform host, GameObject prefab, bool zUp, Vector3 at, float turn)
        {
            if (prefab == null) return null;

            var piece = Object.Instantiate(prefab, host);

            piece.transform.localRotation = zUp
                ? Quaternion.Euler(-90f, turn, 0f)
                : Quaternion.Euler(0f, turn, 0f);

            var bounds = ModelScaling.Measure(piece);
            if (bounds.size == Vector3.zero) return piece;

            // Centred on the spot and standing on the ground, which for a wall means its
            // own lowest point at nought — the caller seats the castle as one thing.
            piece.transform.position += new Vector3(at.x - bounds.center.x,
                                                    at.y - bounds.min.y,
                                                    at.z - bounds.center.z);
            return piece;
        }

        /// <summary>
        /// Puts a chimney through the roof rather than on it.
        ///
        /// Off to one side, and its foot set well down inside the roof, because a chimney
        /// stands on the hearth below and comes out through the tiles. Sat on top of the
        /// ridge it reads as a chimney-shaped object somebody left up there.
        /// </summary>
        static void Chimney(Transform host, BuildingKit kit, DeterministicRandom rng, GameObject roof)
        {
            var above = ModelScaling.Measure(roof);
            var stack = Object.Instantiate(Any(kit.Chimneys, rng), host);

            stack.transform.localRotation = kit.Chimneys.ZUp
                ? Quaternion.Euler(-90f, 0f, 0f)
                : Quaternion.identity;

            var bounds = ModelScaling.Measure(stack);

            float side = rng.Chance(0.5f) ? 1f : -1f;
            float x = above.center.x + side * above.extents.x * rng.Range(0.3f, 0.55f);
            float z = above.center.z + rng.Range(-0.35f, 0.35f) * above.extents.z;
            float y = above.min.y + above.size.y * 0.35f;

            stack.transform.position += new Vector3(x - bounds.center.x, y - bounds.min.y,
                                                    z - bounds.center.z);
        }

        /// <summary>
        /// Seats one piece on top of what is already there, centred on the same spot.
        ///
        /// <paramref name="top"/> comes in as the height to build from and goes out as
        /// the top of what was just placed, so a caller stacks by calling this in order.
        /// </summary>
        static GameObject Stack(Transform host, GameObject prefab, ref float top, bool zUp)
        {
            if (prefab == null) return null;

            var piece = Object.Instantiate(prefab, host);
            piece.transform.localRotation = zUp ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;

            var bounds = ModelScaling.Measure(piece);
            if (bounds.size == Vector3.zero) return piece;

            // Centred on the stack's own axis, which is the host's origin, and seated on
            // the course below with a hair of overlap.
            float lift = top - bounds.size.y * Seam - bounds.min.y;

            piece.transform.position += new Vector3(-bounds.center.x, lift, -bounds.center.z);

            top = ModelScaling.Measure(piece).max.y;
            return piece;
        }

        static int Length(PropSet set) => set != null && set.Any ? set.Models.Length : 1;

        /// <summary>The piece of this set that goes with the chosen style.</summary>
        static GameObject Pick(PropSet set, int style)
        {
            if (set == null || !set.Any) return null;
            return set.Models[style % set.Models.Length];
        }

        static GameObject Any(PropSet set, DeterministicRandom rng)
            => set.Models[rng.Range(0, set.Models.Length)];
    }
}
