using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.View
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

        /// <summary>
        /// Round towers and the caps that belong on them, kept apart from the square ones.
        ///
        /// <b>Because Pick indexes a style into each set by remainder, and the sets are
        /// different lengths.</b> Six shafts against three tops means style 1 draws a
        /// square shaft and a round cap, style 2 a square shaft and a conical spire — so
        /// the castle came out with domes on square towers and its spires wherever the
        /// arithmetic happened to land. A round top belongs on a round tower and that is
        /// not something a remainder can be trusted to know.
        /// </summary>
        public PropSet RoundShafts = new PropSet();
        public PropSet RoundTops = new PropSet();

        /// <summary>A banner for a tower to fly. Optional.</summary>
        public PropSet Banners = new PropSet();

        /// <summary>
        /// The timber hoarding that goes over a tower.s battlements.
        ///
        /// <b>The thing the stone towers were missing and the artwork has on every one of
        /// them.</b> A castle in this pack is not bare masonry to the sky: the fighting
        /// top is a wooden gallery built out over the wall, and the pack ships four of
        /// them. Without it a tower ends in a ring of grey teeth and reads as a chess
        /// piece; with it, it reads as a castle somebody is holding.
        /// </summary>
        public PropSet Hoardings = new PropSet();

        /// <summary>
        /// The buttress that stands at a joint between two wall panels.
        ///
        /// Drawn 5.09 m tall, which is the curtain wall to the centimetre — the pack means
        /// it to stand against one. Without them a castle wall is one panel repeated, the
        /// same flat face the whole way round, and what the pack.s own artwork shows is a
        /// wall divided into bays by exactly this.
        /// </summary>
        public PropSet Pillars = new PropSet();

        /// <summary>The grille in the gate, shut. See BuildingBuilder.Castle.</summary>
        public PropSet Portcullis = new PropSet();

        /// <summary>
        /// The crenellated walk that goes on top of a curtain wall.
        ///
        /// Drawn 5.00 m long against the wall.s 5.40, which is the pack saying these two
        /// belong together. Without it the curtain is a plain 5.09 m slab — three men high,
        /// with nothing on top to stand behind.
        /// </summary>
        public PropSet WallTops = new PropSet();

        /// <summary>
        /// The small turret that rises out of a tower.s crown, and the conical roof that
        /// caps a round one.
        ///
        /// This is the stepped silhouette the pack.s own artwork is built on: a broad
        /// shaft, a ring of battlements, and a narrower tower standing inside them. A
        /// tower that stops at its crown is a chimney with a fence round it.
        /// </summary>
        public PropSet Turrets = new PropSet();
        public PropSet Spires = new PropSet();

        public bool CanBuildRoundTower => RoundShafts.Any && RoundTops.Any;

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
            => House(parent, kit, rng, out _);

        /// <summary>
        /// A house of one, two or three storeys, and says how many it came out with.
        ///
        /// <b>The pack's four sets are not four courses of one building.</b> Each was
        /// looked at on its own, square from the side, which is the inspection that should
        /// have been made the first time:
        ///
        ///   * Foundation — a ground floor with a flat, open top. It carries a storey.
        ///   * Room — a finished cottage: stone sill, timber walls, its own red roof.
        ///   * TopRoomSmall — an upper storey with its own roof, jettied, narrow below.
        ///   * RoomTop — an upper storey with its own roof, flat below.
        ///
        /// So there are two assemblies and this builds both: a Room standing alone, or a
        /// Foundation — sometimes two — under a TopRoomSmall or a RoomTop. What there is
        /// not is a stack of all four, which is what was built for months: a foundation, a
        /// whole cottage on top of it, and two more roofed storeys above that. Sixteen and
        /// nine tenths of a metre of it, reported over and over as three houses standing on
        /// each other, which is exactly what it was.
        ///
        /// <paramref name="storeys"/> is for the caller's scaling. A cottage and a
        /// three-storey town house are not the same height and must not be fitted to the
        /// same number, or the tall one is squashed and the small one stretched.
        /// </summary>
        public static GameObject House(Transform parent, BuildingKit kit, DeterministicRandom rng,
                                       out int storeys)
        {
            storeys = 1;
            if (kit == null || !kit.CanBuildHouse) return null;

            var host = new GameObject("House");
            host.transform.SetParent(parent, false);

            // One style, carried across the sets. The pack numbers its foundations, rooms
            // and roofs in parallel, so drawing separately would put a round roof on a
            // square room about six times in seven.
            int style = rng.Range(0, Length(kit.Rooms));
            float top = 0f;

            float roll = rng.Value01();

            // A cottage: one piece, finished, and the commonest thing in any town.
            if (roll < Cottage || !kit.Foundations.Any)
            {
                Stack(host.transform, Pick(kit.Rooms, style), ref top, kit.Rooms.ZUp);
                return host;
            }

            // Otherwise a ground floor, and a second one for the tall ones.
            Stack(host.transform, Pick(kit.Foundations, style), ref top, kit.Foundations.ZUp);
            storeys = 2;

            if (roll > 1f - Tall && kit.Foundations.Any)
            {
                Stack(host.transform, Pick(kit.Foundations, style), ref top, kit.Foundations.ZUp);
                storeys = 3;
            }

            // And the storey that carries the roof. The jettied one where the pack has it,
            // because an upper floor hanging out over the street is the whole look of a
            // town of this age.
            var upper = kit.UpperRooms.Any && rng.Chance(Jettied) ? kit.UpperRooms : kit.Roofs;
            var crown = Stack(host.transform, Pick(upper, style), ref top, upper.ZUp);

            if (crown != null && kit.Chimneys.Any && rng.Chance(HasChimney))
                Chimney(host.transform, kit, rng, crown);

            return host;
        }

        /// <summary>Share of houses that are a single finished cottage.</summary>
        const float Cottage = 0.42f;

        /// <summary>Share of the rest that get a second ground floor under the top storey.</summary>
        const float Tall = 0.3f;

        /// <summary>How often the top storey is the jettied one rather than the plain one.</summary>
        const float Jettied = 0.65f;


        /// <summary>
        /// Which of the kit's shelves this house is drawn from.
        ///
        /// Weighted towards the rooms and roofs, which are the full-sized cottages; the
        /// foundations are squatter and the small upper rooms are smaller still, and a
        /// village of nothing but those reads as a hamlet of sheds.
        /// </summary>
        static PropSet Shelf(BuildingKit kit, DeterministicRandom rng)
        {
            float roll = rng.Value01();

            if (roll < 0.40f && kit.Rooms.Any) return kit.Rooms;
            if (roll < 0.75f && kit.Roofs.Any) return kit.Roofs;
            if (roll < 0.90f && kit.Foundations.Any) return kit.Foundations;

            return kit.UpperRooms.Any ? kit.UpperRooms : kit.Rooms;
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
            => Tower(parent, kit, rng, 1);

        /// <summary>
        /// A tower of however many courses of shaft.
        ///
        /// <b>One course is not a tower.</b> The pack's shaft is about three metres and its
        /// curtain wall is five, so a base, one shaft and a top came out at 5.1 m against a
        /// 5.1 m wall — from above they read as lumps on the corners, and from the ground
        /// the castle had no vertical in it at all. A tower is the thing that tells you
        /// which building is the castle; it has to clear the wall it stands in.
        /// </summary>
        public static GameObject Tower(Transform parent, BuildingKit kit, DeterministicRandom rng,
                                       int courses, bool round = false, bool timber = false,
                                       bool capped = true, int style = -1, bool crowned = true)
        {
            if (kit == null || !kit.CanBuildTower) return null;

            bool drum = round && kit.CanBuildRoundTower;
            var shafts = drum ? kit.RoundShafts : kit.TowerShafts;
            var tops = drum ? kit.RoundTops : kit.TowerTops;

            var host = new GameObject("Tower");
            host.transform.SetParent(parent, false);

            if (style < 0) style = rng.Range(0, Length(shafts));
            float top = 0f;

            if (kit.TowerBases.Any)
                Stack(host.transform, Pick(kit.TowerBases, style), ref top, kit.TowerBases.ZUp);

            for (int i = 0; i < (courses < 1 ? 1 : courses); i++)
                Stack(host.transform, Pick(shafts, style), ref top, shafts.ZUp);

            // The crown, which is a ring of battlements and not a lid — and then the
            // thing that stands inside it. A tower that stops at its crown is a chimney
            // with a fence round it; the pack.s own artwork steps every one of them.
            //
            // Skipped where a timber hoarding is going on instead. The gallery <i>is</i>
            // the fighting top, and a ring of stone battlements under it is a third thing
            // between the wood and the tower: sunk far enough to bury the struts, the
            // crenellations came back out above the gallery floor and read as a loose band
            // of stone hanging in the gap.
            float crown = top;

            if (crowned)
            {
                var ring = Stack(host.transform, Pick(tops, style), ref top, tops.ZUp);

                // Sunk a little into the shaft it stands on.
                //
                // The crown is wider than the shaft — 3.18 m against 2.78 — so seated
                // exactly on top of it the whole underside of that overhang is in view,
                // and a course of stone with daylight under its rim reads as floating
                // however solidly it is sitting there. Dropped by a third of its own
                // height the overhang meets the shaft and it reads as built.
                if (ring != null)
                {
                    float sink = ModelScaling.Measure(ring).size.y * CrownSink;
                    ring.transform.position += Vector3.down * sink;
                    top = ModelScaling.Measure(ring).max.y;
                }
            }


            // Timber only where it was asked for, which is the pair either side of the
            // gate. Put on every tower it turns the whole castle into a timber yard —
            // the artwork hoards the gatehouse and leaves the rest of the wall in stone,
            // because the gate is the part worth defending from overhead.
            var cap = drum ? kit.Spires
                          : (timber && kit.Hoardings.Any ? kit.Hoardings : kit.Turrets);

            // Uncapped when this shaft is one quarter of a block: the block gets a single
            // gallery over the whole of it rather than one per shaft. See Block.
            if (!capped) cap = new PropSet();

            // Seated from the crown the ring started at, not from the top of it, and by
            // its origin rather than its bounds: the turret is drawn with two and a half
            // metres of tenon below it to drop down inside the battlements. See Perch.
            if (cap.Any)
            {
                float socket = crown;
                Perch(host.transform, Any(cap, rng), ref socket, cap.ZUp);
                if (socket > top) top = socket;
            }

            // A banner on the tall ones. The pack ships them and nothing was flying any:
            // a fortress with no colours on it is a ruin somebody still lives in, and the
            // one thing that says this castle is *held* is that somebody hung a flag.
            if (capped && kit.Banners.Any && courses >= CornerCourses)
                Stack(host.transform, Any(kit.Banners, rng), ref top, kit.Banners.ZUp);

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

            // <b>How many pieces, not how big a piece.</b>
            //
            // This used to take two or three a side and let the caller fit the whole thing
            // to fifteen metres tall, which on 3-10 came out as a scale of 2.58 — curtain
            // walls thirteen metres high and fourteen long, four of them, and from the
            // ground it read as a stack of grey slabs rather than as a castle. The kit is
            // already drawn at the scale everything else in the level is at; a wall that
            // has to be made half again as tall as a house is a wall, and one made three
            // times as tall is a cliff.
            //
            // So the pieces keep the size they were drawn at and the castle is made big
            // the way a real one is: by having more of them. See CastleSpan.
            int across = Ring(span);
            int deep = Ring(span);

            float halfX = across * span * 0.5f;
            float halfZ = deep * span * 0.5f;

            // The two side walls, run from the near corner to the far one.
            for (int i = 0; i < deep; i++)
            {
                float z = -halfZ + (i + 0.5f) * span;

                Crenel(host.transform, kit, style, rng, new Vector3(-halfX, 0f, z), 90f);
                Crenel(host.transform, kit, style, rng, new Vector3(halfX, 0f, z), 90f);
            }

            // The back wall, whole. The front wall short of however many pieces the
            // caravan needs to drive between.
            //
            // <b>The gate is shut, because the castle is theirs.</b>
            //
            // This was a hole to drive through, and the hole was the mistake behind the
            // mistake. A castle standing on the goal means the caravan's journey ends
            // inside the enemy's fortress, which is not a thing that happens: the champion
            // holds it, and what the road is for is getting past him to the place beside
            // it. So the castle is closed — see TerrainDecorator.PlaceCastle for where the
            // goal went instead — and the front wall carries a gatehouse rather than a gap.
            //
            // The gatehouse is the pack's own SM_Bld_Castle_Wall_Gate_01, which is a
            // curtain wall with a gate in it. The arch that used to stand here is dry
            // stone 2.4 m across, put where a 14 m wall belonged, and it left a castle
            // with no opening and no gatehouse either — the one piece in the kit that is
            // actually a gate was never loaded.
            int gateAt = across / 2;

            for (int i = 0; i < across; i++)
            {
                float x = -halfX + (i + 0.5f) * span;

                Crenel(host.transform, kit, style, rng, new Vector3(x, 0f, halfZ), 0f);

                if (i == gateAt && kit.Gates.Any)
                {
                    Run(host.transform, Any(kit.Gates, rng), kit.Gates.ZUp,
                        new Vector3(x, 0f, -halfZ), 0f);

                    // And the grille, down. The castle is the champion's and nobody is
                    // invited in: a gateway standing open on a fortress that cannot be
                    // entered is the one detail that would give the whole thing away.
                    if (kit.Portcullis.Any)
                        Run(host.transform, Any(kit.Portcullis, rng), kit.Portcullis.ZUp,
                            new Vector3(x, 0f, -halfZ), 0f);
                }
                else
                {
                    Crenel(host.transform, kit, style, rng, new Vector3(x, 0f, -halfZ), 0f);
                }
            }

            // <b>A buttress at every joint, which is what a castle wall looks like.</b>
            //
            // The curtain was one 5.4 m panel repeated, so from any angle it was the same
            // flat face the whole way round — a wall with no bays in it. The pack draws a
            // pillar exactly the wall's own height for this and it had never been loaded.
            //
            // Interior joints only: the corners and the gate already carry towers, and a
            // buttress inside a tower is a pillar nobody can see holding up nothing.
            if (kit.Pillars.Any)
            {
                for (int i = 1; i < across; i++)
                {
                    float x = -halfX + i * span;
                    Run(host.transform, Any(kit.Pillars, rng), kit.Pillars.ZUp,
                        new Vector3(x, 0f, halfZ), 0f);

                    if (i != gateAt && i != gateAt + 1)
                        Run(host.transform, Any(kit.Pillars, rng), kit.Pillars.ZUp,
                            new Vector3(x, 0f, -halfZ), 0f);
                }

                for (int i = 1; i < deep; i++)
                {
                    float z = -halfZ + i * span;
                    Run(host.transform, Any(kit.Pillars, rng), kit.Pillars.ZUp,
                        new Vector3(-halfX, 0f, z), 0f);
                    Run(host.transform, Any(kit.Pillars, rng), kit.Pillars.ZUp,
                        new Vector3(halfX, 0f, z), 0f);
                }
            }

            // One tower to the left of the gate and one to the right, standing clear of
            // the wall on the outside of it — the same tower the corners carry, with the
            // timber gallery on top.
            float gateX = -halfX + (gateAt + 0.5f) * span;
            float stand = -halfZ - OutsideTheWall(kit);

            foreach (float side in new[] { gateX - span * 0.5f, gateX + span * 0.5f })
            {
                // Style three, which is SM_Bld_Castle_Tower_04 — the one shaft in the set
                // with arrow slits cut in it. The corners draw at random and so happened
                // to show windows while the gate towers came out blank.
                Block(host.transform, kit, rng, GateCourses, new Vector3(side, 0f, stand),
                      timber: true, facing: 180f, width: 1f, style: SlittedShaft);
            }

            // And two more of the same on each of the other three sides.
            //
            // The gate had its pair and the rest of the circuit had nothing but corners,
            // so from anywhere except straight in front the castle was a long blank run of
            // wall. Two to a side puts a tower in view from wherever the road comes in,
            // which is the whole job of a mural tower and the reason they are spaced the
            // way they are on any real one.
            //
            // Each turned to overhang its own wall, since a hoarding built out over the
            // courtyard would be a gallery facing the wrong way.
            float outX = halfX + OutsideTheWall(kit);
            float outZ = halfZ + OutsideTheWall(kit);

            foreach (float at in new[] { -span, span })
            {
                Block(host.transform, kit, rng, GateCourses, new Vector3(at, 0f, outZ),
                      timber: true, facing: 0f, width: 1f, style: SlittedShaft);

                Block(host.transform, kit, rng, GateCourses, new Vector3(-outX, 0f, at),
                      timber: true, facing: 90f, width: 1f, style: SlittedShaft);

                Block(host.transform, kit, rng, GateCourses, new Vector3(outX, 0f, at),
                      timber: true, facing: 270f, width: 1f, style: SlittedShaft);
            }

            // A tower on each corner, which is what stops the curtain reading as a fence.
            foreach (var corner in new[]
            {
                new Vector3(-halfX, 0f, -halfZ), new Vector3(halfX, 0f, -halfZ),
                new Vector3(-halfX, 0f, halfZ), new Vector3(halfX, 0f, halfZ)
            })
            {
                // Three courses at the corners, so the towers clear the curtain by half
                // again and the castle has a skyline. See Tower(.., courses).
                var tower = Tower(host.transform, kit, rng, CornerCourses);
                if (tower != null) tower.transform.localPosition = corner;
            }


            return host;
        }

        /// <summary>
        /// A tower of four shafts standing shoulder to shoulder, as one mass.
        ///
        /// The kit has one width of shaft, so height is the only dial a single stack has —
        /// and turning it up makes the tower thinner, not grander. Four together doubles
        /// the width, which is the only way to get a tower that reads as masonry rather
        /// than as a flue.
        /// </summary>
        static void Block(Transform host, BuildingKit kit, DeterministicRandom rng,
                          int courses, Vector3 at, bool timber = false, float facing = 0f,
                          float width = Broad, int style = -1)
        {
            // <b>One tower widened, not four standing together.</b>
            //
            // The kit draws its shaft at one width, so the first answer to a tower too thin
            // for its height was to stand four of them in a square. That is thicker and it
            // is worse: the seams between them show, the ring of battlements repeats four
            // times across the top, and whatever caps it perches over the join on stilts.
            // It reads as four towers pushed together, because it is.
            //
            // Widening one is the thing the eye accepts — a tower has no detail that a
            // stretch across gives away at this scale, and what comes out is a single
            // mass with a single crown. Height is deliberately not scaled with it: that
            // is the ratio the whole problem was about.
            var shaft = Tower(host, kit, rng, courses, capped: false, style: style);
            if (shaft == null) return;

            shaft.transform.localScale = new Vector3(width, 1f, width);
            shaft.transform.localPosition = at;

            float crown = ModelScaling.Measure(shaft).max.y;

            if (!timber || !kit.Hoardings.Any) return;

            // <b>One gallery over the whole block, not one per shaft.</b> A block is four
            // towers standing together and reads as a single tower, so capping each of
            // them capped the same tower four times — and since the hoardings come in four
            // different sizes and are drawn at random, one tower ended up wearing two of
            // them at different heights. The artwork has one roof per tower.
            var roof = new GameObject("Hoarding");
            roof.transform.SetParent(host, false);

            // Seated on the stone by its own lowest point, not hung from its origin. The
            // hoarding is drawn as a gallery on struts, and perched the way a turret is
            // perched those struts become legs: the gallery stands a clear metre above the
            // battlements with daylight under it.
            float socket = crown;
            var gallery = Stack(roof.transform, Any(kit.Hoardings, rng), ref socket, kit.Hoardings.ZUp);

            // <b>And then sunk, because the struts belong in the stone.</b> A hoarding is a
            // gallery braced out from the wall on raking timbers, and they are drawn as
            // part of the piece — the lower two fifths of it is nothing but legs. Seated by
            // its bounds it stands on those legs with daylight under the floor, and perched
            // by its origin it does the same thing a little higher. What it wants is its
            // floor on the battlements and its timbers buried in the masonry below, which
            // is what a hoarding actually is.
            float sink = gallery == null ? 0f : ModelScaling.Measure(gallery).size.y * StrutShare;

            if (kit.Banners.Any) Stack(roof.transform, Any(kit.Banners, rng), ref socket,
                                       kit.Banners.ZUp);

            // Turned to overhang the outside. A hoarding is a gallery built out from one
            // face — unturned, it hangs over the courtyard and the wall gets its blank
            // back, which is why the first pair read as two brown boxes.
            roof.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
            roof.transform.localPosition = at + Vector3.down * sink;
        }

        /// <summary>How far out of the wall line a gate tower stands, in metres.</summary>
        static float OutsideTheWall(BuildingKit kit)
        {
            if (!kit.CurtainWalls.Any || !kit.TowerShafts.Any) return 2f;

            var wall = Object.Instantiate(kit.CurtainWalls.Models[0]);
            var shaft = Object.Instantiate(kit.TowerShafts.Models[0]);

            var wallBox = ModelScaling.Measure(wall);
            var shaftBox = ModelScaling.Measure(shaft);

            if (Application.isPlaying) { Object.Destroy(wall); Object.Destroy(shaft); }
            else { Object.DestroyImmediate(wall); Object.DestroyImmediate(shaft); }

            // Half the wall's thickness plus half the tower's, so the tower's back face
            // meets the wall's outer face and it stands proud of it rather than in it.
            return Mathf.Min(wallBox.size.x, wallBox.size.z) * 0.5f
                   + Mathf.Max(shaftBox.size.x, shaftBox.size.z) * 0.5f;
        }

        /// <summary>
        /// Half the gap between the four shafts the keep is made of.
        ///
        /// Measured off a shaft rather than picked, so the four stand shoulder to shoulder
        /// whatever the pack is: half a shaft each way puts their faces together and their
        /// crowns overlapping into one mass with a turret at each corner.
        /// </summary>
        static float KeepSpread(BuildingKit kit)
        {
            if (!kit.TowerShafts.Any) return 1.4f;

            var probe = Object.Instantiate(kit.TowerShafts.Models[0]);
            var box = ModelScaling.Measure(probe);

            if (Application.isPlaying) Object.Destroy(probe);
            else Object.DestroyImmediate(probe);

            return Mathf.Max(box.size.x, box.size.z) * 0.5f;
        }

        /// <summary>
        /// Courses of shaft in the keep.
        ///
        /// Six: half again the gate towers, which are themselves half again the corners.
        /// A castle's silhouette is a rising one — curtain, corner, gate, keep — and that
        /// order is the whole of what makes a pile of grey blocks read as a fortress.
        /// </summary>
        public const int KeepCourses = 4;

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
        /// <summary>
        /// How wide the castle is built, in metres, before the ground is considered.
        ///
        /// Forty-four. The caravan drives in through the gate, so the courtyard has to
        /// hold a sixteen-metre column with its escort round it and still look like a
        /// courtyard rather than a corridor. It is also what a castle has to be to read as
        /// one from the game camera against a map whose tiles are four metres.
        /// </summary>
        public const float CastleSpan = 44f;

        /// <summary>
        /// How much wider than the kit draws it a gate tower or a keep is built.
        ///
        /// The shaft is 2.78 m across and the artwork.s towers are about two and a half
        /// times as tall as they are wide. At 1.9 the tower is 5.3 m across, which puts a
        /// three-course tower at about that ratio and gives the gate a mass rather than a
        /// pair of posts.
        /// </summary>
        public const float Broad = 1.9f;

        /// <summary>The shaft in the set that has arrow slits cut in it.</summary>
        public const int SlittedShaft = 3;

        /// <summary>How much of a hoarding is the raking timbers under its floor.</summary>
        public const float StrutShare = 0.4f;

        /// <summary>How far a tower.s crown is let into the shaft below it.</summary>
        public const float CrownSink = 0.33f;

        /// <summary>Courses of shaft in a corner tower, and in the pair flanking the gate.</summary>
        public const int CornerCourses = 2;
        public const int GateCourses = 3;

        /// <summary>Pieces to a side, so the ring comes out about <see cref="CastleSpan"/> wide.</summary>
        static int Ring(float span)
        {
            if (span <= 0f) return 2;

            int pieces = Mathf.RoundToInt(CastleSpan / span);
            return pieces < 3 ? 3 : pieces;
        }

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

        /// <summary>
        /// One length of curtain wall with its battlements on.
        ///
        /// <b>The wall was shipping without its top.</b> SM_Bld_Castle_Wall_01 is a plain
        /// 5.09 m slab and SM_Bld_Castle_Tower_Wall_Top_01 is the crenellated walk that
        /// goes on it — drawn 5.00 m long against the wall's 5.40, which is the pack
        /// saying in the only way it can that these two are one wall. Nothing had ever
        /// loaded the second piece, so every castle in the game was a curtain with a flat
        /// edge: three men high, and nothing on top for anyone to stand behind.
        ///
        /// Together they come to about six and a half metres, which is also the
        /// proportion the pack's own artwork has against its figures.
        /// </summary>
        static void Crenel(Transform host, BuildingKit kit, int style, DeterministicRandom rng,
                           Vector3 at, float turn)
        {
            var wall = Run(host, Pick(kit.CurtainWalls, style), kit.CurtainWalls.ZUp, at, turn);
            if (wall == null || !kit.WallTops.Any) return;

            float top = ModelScaling.Measure(wall).max.y;
            Run(host, Any(kit.WallTops, rng), kit.WallTops.ZUp,
                new Vector3(at.x, top - Seam, at.z), turn);
        }

        /// <summary>
        /// Sets a piece by its own origin rather than by the bottom of its bounds.
        ///
        /// <b>For the pieces the artist drew with a tenon on them.</b> The mini turret is
        /// 5.12 m tall and its lowest point is 2.56 m *below* its origin: two and a half
        /// metres of spike, meant to drop down inside the crown of a tower so that only
        /// the turret shows. Seated the ordinary way — bounds bottom on the course below —
        /// that spike becomes a stilt, and the turret floats two and a half metres over
        /// the battlements it is supposed to be standing in.
        ///
        /// <see cref="Stack"/> is right for everything drawn to butt, which is most of the
        /// kit. This is right for the few drawn to socket, and telling them apart is a
        /// matter of looking at where the origin sits relative to the mesh.
        /// </summary>
        static GameObject Perch(Transform host, GameObject prefab, ref float top, bool zUp)
        {
            if (prefab == null) return null;

            var piece = Object.Instantiate(prefab, host);
            piece.transform.localRotation = zUp ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;

            var bounds = ModelScaling.Measure(piece);
            if (bounds.size == Vector3.zero) return piece;

            // The origin to the course below, and the centring the same as Stack's.
            piece.transform.position += new Vector3(-bounds.center.x, top, -bounds.center.z);

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
