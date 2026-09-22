using System.Collections.Generic;
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

        /// <summary>
        /// What furnishes a courtyard: the well, the lean-tos against the wall, the
        /// tents, the loose gear and the fire by the gate.
        ///
        /// <b>Held on the kit rather than read from the biome's prop sets, because the
        /// castle is assembled at the origin and turned as one thing.</b> Everything
        /// inside the walls has to be a child of the castle and placed in its local
        /// space — which is also what keeps it out of the courtyard sweep, since that
        /// only judges the decor parent's own children.
        /// </summary>
        public PropSet Wells = new PropSet();
        public PropSet Shelters = new PropSet();
        public PropSet Tents = new PropSet();
        public PropSet YardGear = new PropSet();
        public PropSet Braziers = new PropSet();
        public PropSet Stairs = new PropSet();

        /// <summary>The stone the bailey is laid in. See BuildingBuilder.Pave.</summary>
        public PropSet Paving = new PropSet();

        /// <summary>The earth the flagstone is laid over. See BuildingBuilder.Pave.</summary>
        public PropSet Ground = new PropSet();

        /// <summary>
        /// The cloth banners that hang down the face of a curtain wall.
        ///
        /// Not <see cref="Banners"/>, which is the pennant on a tower's staff. The plan
        /// hangs four of these between the towers, and they are the only colour on a
        /// forty-metre run of grey.
        /// </summary>
        public PropSet WallBanners = new PropSet();

        /// <summary>
        /// The colours the banners are painted in.
        ///
        /// <b>PolygonKnights ships four texture atlases and this game had only ever used
        /// the first.</b> Nothing was wrong with that until the plan asked for red cloth:
        /// the banner's UVs point where they point, and under atlas one they come out blue
        /// and purple. Put the same prefab under each of the four and the third is red and
        /// gold — see CastleMockup.Swatches, which is how this was settled rather than
        /// reasoned about.
        ///
        /// Optional. Left empty the banners keep whatever the prefab ships with.
        /// </summary>
        public Material Livery;

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
                                       bool capped = true, int style = -1, bool crowned = true,
                                       bool spired = false)
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
            var cap = drum || spired ? kit.Spires
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
                    // <b>Built big, because a gateway is the one hole in the wall.</b>
                    //
                    // The pack draws its gate panel the size of a curtain panel, and once
                    // the curtain went to two courses the arch in it was a mousehole at
                    // the foot of a fifteen-metre face — an eighth of the wall's height
                    // where the plan has a quarter of it. So the gate panel is built to a
                    // scale of its own: it is the gatehouse, and a gatehouse is heavier
                    // masonry than the wall it interrupts.
                    //
                    // Which also widens it past its bay and into the panels either side,
                    // and that is right: on the plan the gatehouse is a block projecting
                    // out of the wall line, not a panel flush with it.
                    var arch = Gatehouse(host.transform, kit, rng, x, -halfZ,
                                         WallRise(host.transform, kit, style) * GateShare);

                    // And the grille, down. The castle is the champion's and nobody is
                    // invited in: a gateway standing open on a fortress that cannot be
                    // entered is the one detail that would give the whole thing away.
                    if (arch != null && kit.Portcullis.Any)
                    {
                        var grille = Run(host.transform, Any(kit.Portcullis, rng),
                                         kit.Portcullis.ZUp, new Vector3(x, 0f, -halfZ), 0f);

                        // At the arch's scale, or the grille hangs in the middle of an
                        // opening twice its size with daylight all round it.
                        if (grille != null)
                        {
                            grille.transform.localScale = arch.transform.localScale;

                            var bars = ModelScaling.Measure(grille);
                            grille.transform.position += new Vector3(x - bars.center.x,
                                                                     -bars.min.y,
                                                                     -halfZ - bars.center.z);
                        }
                    }

                    // <b>The stone over the gateway.</b>
                    //
                    // A gatehouse is not two towers with a hole between them, which is
                    // what stood here: a pair of drums planted six metres clear of the
                    // wall with the arch behind them. On the plan it is a mass of masonry
                    // built up over the arch — a course higher than the curtain either
                    // side, crenellated across the top, and the towers are at its
                    // shoulders rather than in front of it.
                    if (arch != null)
                    // <b>Crenellations only, no course under them.</b>
                    //
                    // The gate panel is already built to the full height of the curtain,
                    // so a course on top of it stood the gatehouse a whole storey proud of
                    // the wall — a crenellated block rearing up over the gateway that is
                    // on no elevation of the plan. What rises above the wall there is the
                    // donjon, behind. The gatehouse finishes flush and takes the same wall
                    // walk as the curtain either side of it.
                    if (arch != null)
                    {
                        var block = ModelScaling.Measure(arch);

                        Crenel(host.transform, kit, style, rng,
                               new Vector3(x, block.max.y - Seam, -halfZ), 0f, courses: 0);

                        // And the colours, on the gatehouse itself. They used to hang on
                        // the wall panels either side, which the widened gatehouse then
                        // stood in front of — so the two banners the plan hangs at the
                        // gate, the pair anybody walking up to it sees, were behind
                        // masonry.
                        foreach (float off in new[] { -GateBannerAt, GateBannerAt })
                            Hang(host.transform, kit, Any(kit.WallBanners, rng),
                                 kit.WallBanners.ZUp,
                                 new Vector3(x + off * block.size.x,
                                             block.max.y * BannerDrop,
                                             block.min.z - Seam), 0f);
                    }
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
                    Buttress(host.transform, kit, rng,
                        new Vector3(x, 0f, halfZ), 0f);

                    if (i != gateAt && i != gateAt + 1)
                        Buttress(host.transform, kit, rng,
                            new Vector3(x, 0f, -halfZ), 0f);
                }

                for (int i = 1; i < deep; i++)
                {
                    float z = -halfZ + i * span;
                    Buttress(host.transform, kit, rng,
                        new Vector3(-halfX, 0f, z), 0f);
                    Buttress(host.transform, kit, rng,
                        new Vector3(halfX, 0f, z), 0f);
                }
            }

            // One tower to the left of the gate and one to the right, standing clear of
            // the wall on the outside of it — the same tower the corners carry, with the
            // timber gallery on top.
            float gateX = -halfX + (gateAt + 0.5f) * span;

            // In the wall line, not in front of it. The pair used to stand a tower's depth
            // clear of the curtain, which made them two free-standing drums with a gateway
            // somewhere behind — see the gate bay above for what the plan actually draws.
            float stand = -halfZ;

            // <b>Round drums with red conical roofs, which is what the plan draws.</b>
            //
            // These were square shafts under timber hoardings for several passes, built
            // off the pack's own promotional artwork and off instructions given while that
            // was the only reference there was. The plan supersedes both: every tower on
            // it — the four corners and the pair at the gate — is a round drum capped
            // with a red spire, and the timber on it is a gallery along the back wall,
            // not a hat on every tower.
            //
            // Both halves are in the kit and both were already loaded: RoundShafts and
            // Spires. Tower(round: true) has drawn them correctly the whole time; nothing
            // was ever asking it to.
            foreach (float side in new[] { gateX - span * GateFlank, gateX + span * GateFlank })
            {
                Mural(host.transform, kit, rng, GateCourses, new Vector3(side, 0f, stand));
            }

            // <b>Eight towers: four corners, one on each side wall, two at the gate.</b>
            //
            // Counted off the plan rather than reasoned about, after two passes of
            // reasoning about it. It was twelve once — two to a side on all four, which
            // turned the circuit into a palisade of towers — and six for one pass, which
            // left both side walls a blank forty-metre run. Six besides the gate pair is
            // what is drawn.
            //
            // The side ones stand mid-wall, which is also the only place a mural tower is
            // any use: a tower at a corner covers two faces and a tower halfway along
            // covers the ground between them.
            // In the wall line, like every other tower on the plan. These two stood a
            // tower's depth clear of the curtain — the only two on the circuit that did,
            // which read as a pair of turrets parked beside the castle rather than as
            // mural towers belonging to it.
            foreach (float side in new[] { -halfX, halfX })
            {
                Mural(host.transform, kit, rng, CornerCourses, new Vector3(side, 0f, 0f));
            }

            // <b>The donjon, in the middle of the wall the gate faces across.</b>
            //
            // Item two on the plan's key — huvudtorn — and the thing the whole castle is
            // built around: a keep wider and half again taller than the mural towers,
            // standing in the north wall. It was missing entirely, which is why the castle
            // read as a walled yard with turrets rather than as somebody's stronghold.
            //
            // Widened rather than heightened alone. The kit draws one width of shaft, and
            // a keep that is only taller is a chimney — see Block, which was written for
            // exactly this and had been left standing unused once the gate towers stopped
            // needing it.
            var keep = Tower(host.transform, kit, rng, KeepCourses, spired: true,
                             style: rng.Range(0, WindowedShafts));

            if (keep != null)
            {
                keep.transform.localScale = new Vector3(Broad, 1f, Broad);
                keep.transform.localPosition = new Vector3(0f, 0f, halfZ);
            }

            // A tower on each corner, which is what stops the curtain reading as a fence.
            foreach (var corner in new[]
            {
                new Vector3(-halfX, 0f, -halfZ), new Vector3(halfX, 0f, -halfZ),
                new Vector3(-halfX, 0f, halfZ), new Vector3(halfX, 0f, halfZ)
            })
            {
                Mural(host.transform, kit, rng, CornerCourses, corner);
            }

            // The colours down the curtain. Four to a wall on the plan, hung between the
            // towers on the two faces anybody sees — and they are the only thing that is
            // not grey on a forty-metre run of it.
            Colours(host.transform, kit, rng, halfX, halfZ, span, across, gateAt, style);


            Courtyard(host.transform, kit, rng, halfX, halfZ, span, gateX,
                      WallDepth(host.transform, kit, style),
                      WallCourses * WallRise(host.transform, kit, style) - (WallCourses - 1) * Seam);

            return host;
        }

        /// <summary>
        /// Inside the walls, to the plan: ranges of buildings with their backs to the
        /// curtain, a hall on the wall facing the gate, and the middle left open with a
        /// well in it.
        ///
        /// <b>Nothing here is fitted to a size.</b> The castle is put up at the scale the
        /// pack draws it and set down without resizing — see TerrainDecorator.PlaceCastle
        /// — so a house from the same pack is already the right size beside its wall. Every
        /// fault the yard has had came from the other habit: a piece asked to be a certain
        /// number of metres tall, which blows a wide flat thing up by its aspect ratio and
        /// turns a well into a silo.
        ///
        /// So the only measuring done is of what a building came out as, and the only use
        /// made of it is deciding where the next one starts.
        ///
        /// Built as children of the castle, in the castle's own space, before it is turned
        /// and seated. That is what keeps the yard out of the sweep at the end of Decorate,
        /// which walks the decor parent's children and would otherwise clear this too.
        /// </summary>
        static void Courtyard(Transform host, BuildingKit kit, DeterministicRandom rng,
                              float halfX, float halfZ, float span, float gateX, float wall,
                              float walk)
        {
            if (!kit.CanBuildHouse) return;

            float inset = wall * 0.5f;

            // The corners are held clear because a corner tower stands on each of them,
            // and a cottage inside a tower is a cottage nobody can see.
            float clear = CourtyardClear;

            // <b>The ground first, because it is the largest thing in the picture.</b>
            //
            // The yard was whatever the level's ground happened to be, which in a forest
            // chapter is grass — so the castle read as a wall built round a field. On the
            // plan the inner bailey is laid stone, grey and brown, and it is the single
            // change that does most: a paved yard is a yard, and grass is a paddock.
            Pave(host, kit, rng, halfX - inset, halfZ - inset);

            // Then the buildings, and there are few of them and none of them is tall.
            //
            // <b>Two mistakes were made here and both were made by not looking.</b> The
            // yard was filled wall to wall with the town's houses — fourteen of them, some
            // three storeys — and the plan has neither: it has six or seven single-storey
            // sheds ranged against the curtain, with more bare ground than building. A
            // castle bailey is not a village that happens to have a wall round it. What
            // stands in one is what the garrison needs and nothing else.
            Range(host, kit, rng, new Vector3(-halfX + clear, 0f, halfZ - inset),
                  Vector3.right, Vector3.back, 2f * (halfX - clear), 90f, BackRange);

            Range(host, kit, rng, new Vector3(-halfX + inset, 0f, -halfZ + clear),
                  Vector3.forward, Vector3.right, 2f * (halfZ - clear), 0f, SideRange);

            Range(host, kit, rng, new Vector3(halfX - inset, 0f, -halfZ + clear),
                  Vector3.forward, Vector3.left, 2f * (halfZ - clear), 180f, SideRange);

            // <b>And the gate wall, which was bare.</b>
            //
            // The plan has stores against it like every other wall; the reason this one
            // was left empty is that the middle of it is gatehouse and a tower stands
            // either side of that, so what is left is two short stretches out by the
            // corners. Short is not nothing: one building to a stretch, and the corner
            // allowance halved, since a corner tower is narrower than the gate towers the
            // rest of the yard is spaced around.
            // Gear, not buildings. Measured rather than assumed: the gatehouse takes the
            // middle of this wall, a tower stands a panel and a half either side of it and
            // another on each corner, so what is left is two stretches of about five
            // metres — and the narrowest shed in the pack is wider than that. A range was
            // asked for here twice and quietly built nothing both times, which is worse
            // than not asking.
            if (kit.YardGear.Any)
            {
                float flank = gateX + span * GateFlank;

                foreach (float at in new[] { -flank, flank })
                {
                    Run(host, Any(kit.YardGear, rng), kit.YardGear.ZUp,
                        new Vector3(at * GateGearIn, 0f, -halfZ + inset + GearStand),
                        rng.Range(0, 4) * 90f);

                    Run(host, Any(kit.YardGear, rng), kit.YardGear.ZUp,
                        new Vector3(at, 0f, -halfZ + inset + GearStand * 2.2f),
                        rng.Range(0, 4) * 90f);
                }
            }

            // <b>The well, in the middle, and big.</b>
            //
            // Item six on the plan's key, drawn dead centre of the inner bailey and wide
            // enough that two people could stand at it — it is the thing the yard is
            // arranged around. The pack draws its well for a village green, about two
            // metres across, and at that size in a forty-metre yard it was a drain cover
            // somebody had put off to one side.
            //
            // Fitted across rather than to a height, which is the distinction this
            // codebase keeps getting wrong in the other direction: a well is a wide flat
            // thing and fitting one to a height blows it up by its aspect ratio into a silo.
            if (kit.Wells.Any)
            {
                var well = Run(host, Any(kit.Wells, rng), kit.Wells.ZUp,
                               Vector3.zero, rng.Range(0, 4) * 90f);

                if (well != null)
                {
                    var ring = ModelScaling.Measure(well);
                    float wide = Mathf.Max(ring.size.x, ring.size.z);

                    if (wide > 0f) well.transform.localScale *= WellWide / wide;

                    ring = ModelScaling.Measure(well);
                    well.transform.position += new Vector3(-ring.center.x, -ring.min.y,
                                                           -ring.center.z);
                }
            }

            // The stair up to the wall walk — item nine, against the inside of a side wall.
            //
            // <b>Up to the walk, and no wider.</b> At the size the pack draws it the flight
            // stopped a good way under the parapet, which is a stair to nowhere. Scaled
            // evenly until it reached, it would be a flight as wide as it is tall - the
            // aspect-ratio trap that has turned a cobble into a boulder in this file
            // before. So the rise and the run are stretched and the width is not: the
            // flight stays the width it was drawn, climbs at the pitch it was drawn at,
            // and its top lands on the walk (`walk`, measured off the curtain pieces
            // Crenel stacks).
            if (kit.Stairs.Any)
            {
                var at = new Vector3(halfX - inset - StairStand, 0f, -halfZ * 0.35f);
                var stair = Run(host, Any(kit.Stairs, rng), kit.Stairs.ZUp, at, 90f);

                if (stair != null && walk > 0f)
                {
                    var flight = ModelScaling.Measure(stair);

                    if (flight.size.y > 0.01f)
                    {
                        // Its rise and its run, both, so the steps keep the pitch they were
                        // drawn at: stretched in height alone the flight went up at seventy
                        // degrees and was a ladder. The run lies along the wall, where there
                        // is room for it. Which of the model's own axes are up and along
                        // depends on how the pack drew it and how it was turned, so they are
                        // read off its rotation rather than assumed.
                        float stretch = walk / flight.size.y;
                        var turned = stair.transform.localRotation;
                        var scale = stair.transform.localScale;

                        scale[Along(turned, Vector3.up)] *= stretch;
                        scale[Along(turned, Vector3.forward)] *= stretch;
                        stair.transform.localScale = scale;

                        flight = ModelScaling.Measure(stair);
                        stair.transform.position += new Vector3(at.x - flight.center.x,
                                                                -flight.min.y,
                                                                at.z - flight.center.z);
                    }
                }
            }

            // Two striped canopies in the open ground, which is what the plan has standing
            // in the middle of the bailey and the only colour in it.
            if (kit.Tents.Any)
                foreach (var at in new[]
                {
                    new Vector2(halfX * TentAt, -halfZ * TentAt * 0.4f),
                    new Vector2(-halfX * TentAt * 0.55f, -halfZ * TentAt)
                })
                    Run(host, Any(kit.Tents, rng), kit.Tents.ZUp,
                        new Vector3(at.x, 0f, at.y), rng.Range(0, 4) * 90f);

            // A fire either side of the gateway, inside. A shut gate with nobody keeping
            // it is a shut gate; a shut gate with braziers at it is held.
            if (kit.Braziers.Any)
                foreach (float side in new[] { gateX - span * 0.6f, gateX + span * 0.6f })
                    Run(host, Any(kit.Braziers, rng), kit.Braziers.ZUp,
                        new Vector3(side, 0f, -halfZ + inset + BrazierStand), 0f);

            // And the loose gear — a cart, a hay wain, crates. Put where a yard's gear
            // actually ends up, which is against the buildings: a cart is unloaded at a
            // door and a crate is stacked out of the way, and neither is left standing in
            // the middle of the ground everybody has to drive across.
            //
            // This was a row across the open yard for one pass and it read as four things
            // placed by arithmetic, because it was.
            if (!kit.YardGear.Any) return;

            // The lane between the ranges and the open middle. A range is seven or eight
            // metres deep, so gear set against the wall itself would be standing inside a
            // house; this is the strip just in front of their doors.
            float laneX = halfX * GearLane;
            float laneZ = halfZ * GearLane;

            foreach (var spot in new[]
            {
                new Vector2(-laneX, laneZ * 0.7f),
                new Vector2(laneX, laneZ * 0.2f),
                new Vector2(-laneX * 0.85f, -laneZ * 0.6f),
                new Vector2(laneX * 0.9f, -laneZ * 0.8f)
            })
            {
                Run(host, Any(kit.YardGear, rng), kit.YardGear.ZUp,
                    new Vector3(spot.x, 0f, spot.y), rng.Range(0, 4) * 90f);
            }
        }

        /// <summary>
        /// A row of buildings along the inside of one wall, packed end to end until the
        /// run is used up.
        ///
        /// Each is built, measured and then seated: the pack's rooms are not one width,
        /// and stepping by a constant either overlaps them or leaves a gap that reads as a
        /// missing house. <paramref name="along"/> is the direction the row runs and
        /// <paramref name="inward"/> the way the courtyard is, so the back of each
        /// building finishes against the stone.
        /// </summary>
        static void Range(Transform host, BuildingKit kit, DeterministicRandom rng,
                          Vector3 from, Vector3 along, Vector3 inward, float run, float yaw,
                          int most)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);

            var built = new List<GameObject>();
            var boxes = new List<Bounds>();
            float total = 0f;

            // Built first and placed after, because a row that is laid down as it is built
            // can only be packed from one end — and what is left over then shows as a
            // stretch of bare wall at the far corner, which reads as a range that ran out
            // of houses rather than as a castle.
            while (built.Count < most)
            {
                var shed = Shed(host, kit, rng);
                if (shed == null) break;

                shed.transform.localRotation = turn;

                var box = ModelScaling.Measure(shed);
                float width = Across(box, along);

                if (width <= 0f || total + width > run) { Object.DestroyImmediate(shed); break; }

                built.Add(shed);
                boxes.Add(box);
                total += width;
            }

            if (built.Count == 0) return;

            // <b>Spread, not packed.</b> What is left over is shared out between the
            // buildings as well as at the ends, because the plan does not range them
            // shoulder to shoulder: there are two or three to a wall with bare stone
            // showing between them, and a row packed tight against one end reads as a
            // terrace, which is a street and not a bailey.
            float gap = (run - total) / (built.Count + 1);
            float cursor = gap;

            for (int i = 0; i < built.Count; i++)
            {
                float width = Across(boxes[i], along);

                // <b>Backed onto the wall by the ground floor, not by the whole house.</b>
                //
                // A house's bounds are its roof's bounds: the eaves overhang the walls and
                // a chimney hangs off one side of the ridge, and neither is symmetric. So
                // the two side ranges — the same houses, turned through a hundred and
                // eighty degrees — backed onto their walls by different amounts, and on a
                // real level one range sat on the stone while the other stood four metres
                // out in the yard with grass behind it.
                //
                // The ground-floor piece is the building's actual footprint and has no
                // overhang on it, so seating by that puts the wall of the house on the
                // wall of the castle whichever way round it is turned. The roof is left to
                // overhang the parapet, which is what a range built against a curtain does.
                var body = Footing(built[i]);

                float depth = Across(body, inward);
                var off = new Vector3(body.center.x, 0f, body.center.z);

                built[i].transform.localPosition =
                    from + along * (cursor + width * 0.5f) + inward * (depth * 0.5f) - off;

                cursor += width + gap;
            }
        }

        /// <summary>
        /// How far a measured box reaches along one of the ground axes.
        ///
        /// The bounds are world-aligned, so which of x and z is the building's width
        /// depends on which way it was turned. A range down a side wall runs along z and
        /// one along the back runs along x, and reading size.x for both is what made every
        /// second range overlap itself.
        /// </summary>
        static float Across(Bounds box, Vector3 axis)
            => Mathf.Abs(axis.x) * box.size.x + Mathf.Abs(axis.z) * box.size.z;

        /// <summary>
        /// One building of a range: a plank shed, or now and then a single-storey cottage.
        ///
        /// <b>No storeys.</b> The plan has nothing inside the walls taller than one floor,
        /// and what stands there is mostly the pack's lean-to — a plank roof on posts,
        /// open to the yard. Calling <see cref="House"/> here is what filled the bailey
        /// with three-storey town houses on jettied foundations: a house is what a town is
        /// made of, and a bailey is made of sheds.
        /// </summary>
        static GameObject Shed(Transform parent, BuildingKit kit, DeterministicRandom rng)
        {
            var host = new GameObject("Shed");
            host.transform.SetParent(parent, false);

            float top = 0f;

            if (kit.Shelters.Any && rng.Chance(PlankShed))
            {
                Stack(host.transform, Any(kit.Shelters, rng), ref top, kit.Shelters.ZUp);
                return host;
            }

            // A finished cottage: one room, its own roof, and nothing on top of it.
            int style = rng.Range(0, Length(kit.Rooms));
            var room = Stack(host.transform, Pick(kit.Rooms, style), ref top, kit.Rooms.ZUp);

            if (room != null && kit.Chimneys.Any && rng.Chance(HasChimney))
                Chimney(host.transform, kit, rng, room);

            return host;
        }

        /// <summary>
        /// The footprint a building actually stands on, which is not what it measures.
        ///
        /// The ground-floor piece, because a building's bounds are its roof's bounds —
        /// eaves overhang the walls and a chimney hangs off one side of the ridge, neither
        /// of them symmetric. Two ranges of the same sheds turned through a hundred and
        /// eighty degrees backed onto their walls by different amounts because of it, and
        /// on a real level one range sat on the stone while the other stood four metres out
        /// with grass behind it.
        /// </summary>
        static Bounds Footing(GameObject building)
        {
            if (building.transform.childCount == 0) return ModelScaling.Measure(building);

            var box = ModelScaling.Measure(building.transform.GetChild(0).gameObject);
            return box.size == Vector3.zero ? ModelScaling.Measure(building) : box;
        }

        /// <summary>
        /// The bailey, laid in stone.
        ///
        /// <b>The ground was the biggest thing in the picture and nobody had chosen it.</b>
        /// The yard was whatever the level's terrain happened to be, so in a forest chapter
        /// the castle was a wall built round a lawn. On the plan the inner bailey is laid
        /// stone — grey and brown, worn earth with flags through it — and it is what
        /// separates a castle from a stockade.
        ///
        /// Each flag fitted across to the step so the yard is continuous stone rather than
        /// a field of mats with ground showing between them, and turned in quarter turns so
        /// one pattern does not repeat over forty metres. The same treatment the town's
        /// streets get, which is where it was proved.
        /// </summary>
        static void Pave(Transform host, BuildingKit kit, DeterministicRandom rng,
                         float halfX, float halfZ)
        {
            if (!kit.Paving.Any) return;

            int across = Mathf.Max(1, Mathf.RoundToInt(halfX * 2f / Flagstone));
            int deep = Mathf.Max(1, Mathf.RoundToInt(halfZ * 2f / Flagstone));

            float stepX = halfX * 2f / across;
            float stepZ = halfZ * 2f / deep;
            float size = Mathf.Max(stepX, stepZ);

            // <b>Earth first, all of it, and stone on some of it.</b>
            //
            // Both the pack's flag and its cobble are a cold blue-grey and the plan's
            // bailey is warm sandy stone. Mixing the warm piece in with them as a third
            // option gave a chequerboard of grey slabs and tan squares that read as sand
            // pits in a car park — which is what it was, since a flat unfigured square laid
            // beside a figured one is a hole in the pattern.
            //
            // Laying the earth under everything instead solves both at once: there is no
            // chequer because there is only one ground, the warmth comes from underneath,
            // and the stone that shows is stone somebody laid over the worst of the mud.
            // Which is also how a yard like this was actually surfaced.
            for (int i = 0; i < across; i++)
            {
                for (int j = 0; j < deep; j++)
                {
                    var at = new Vector3(-halfX + (i + 0.5f) * stepX, 0f,
                                         -halfZ + (j + 0.5f) * stepZ);

                    if (kit.Ground.Any)
                        Flag(host, Any(kit.Ground, rng), kit.Ground.ZUp, at, size,
                             rng.Range(0, 4) * 90f, PaveLip);

                    if (!kit.Ground.Any || rng.Chance(Flagged))
                        Flag(host, Any(kit.Paving, rng), kit.Paving.ZUp, at, size,
                             rng.Range(0, 4) * 90f, PaveLip + FlagProud);
                }
            }
        }

        /// <summary>
        /// One paving flag, fitted across to a size and laid with its face at the ground.
        ///
        /// Scaled before it is placed, not after: scaling happens about the pivot, and a
        /// piece centred on its spot and then scaled walks off it by however far its pivot
        /// is from its middle.
        /// </summary>
        static void Flag(Transform host, GameObject prefab, bool zUp, Vector3 at,
                         float size, float yaw, float lip)
        {
            if (prefab == null) return;

            var piece = Object.Instantiate(prefab, host);

            piece.transform.localRotation = zUp
                ? Quaternion.Euler(-90f, yaw, 0f) : Quaternion.Euler(0f, yaw, 0f);

            var box = ModelScaling.Measure(piece);
            float wide = Mathf.Max(box.size.x, box.size.z);

            if (wide <= 0f) { Object.DestroyImmediate(piece); return; }

            piece.transform.localScale *= size / wide;

            box = ModelScaling.Measure(piece);

            piece.transform.position += new Vector3(at.x - box.center.x,
                                                    lip - box.max.y,
                                                    at.z - box.center.z);
        }

        /// <summary>
        /// The colours down the curtain.
        ///
        /// Hung on the two faces anybody sees, every other bay, from just under the
        /// crenellations. The plan hangs four to a wall and they are the one thing on it
        /// that is not grey: a forty-metre run of stone with nothing on it reads as a
        /// retaining wall however well it is built.
        /// </summary>
        static void Colours(Transform host, BuildingKit kit, DeterministicRandom rng,
                            float halfX, float halfZ, float span, int across, int gateAt,
                            int style)
        {
            if (!kit.WallBanners.Any) return;

            float rise = WallRise(host, kit, style) * WallCourses;
            if (rise <= 0f) return;

            float face = WallDepth(host, kit, style) * 0.5f;
            float hang = rise * BannerDrop;

            for (int i = 0; i < across; i++)
            {
                if (i % 2 == 1) continue;

                float x = -halfX + (i + 0.5f) * span;

                // Not on the bays touching the gate: the gatehouse hangs its own pair,
                // and a banner on the panel beside it lands a stride away from one of
                // them and reads as four colours crowded round the gateway.
                if (i < gateAt - 1 || i > gateAt + 1)
                    Hang(host, kit, Any(kit.WallBanners, rng), kit.WallBanners.ZUp,
                         new Vector3(x, hang, -halfZ - face), 0f);

                Hang(host, kit, Any(kit.WallBanners, rng), kit.WallBanners.ZUp,
                     new Vector3(x, hang, halfZ + face), 180f);
            }
        }

        /// <summary>A banner hung from a height, against a face.</summary>
        static void Hang(Transform host, BuildingKit kit, GameObject prefab, bool zUp,
                         Vector3 at, float yaw)
        {
            if (prefab == null) return;

            var piece = Object.Instantiate(prefab, host);

            piece.transform.localRotation = zUp
                ? Quaternion.Euler(-90f, yaw, 0f) : Quaternion.Euler(0f, yaw, 0f);

            // Repainted in the castle's colours. See BuildingKit.Livery.
            if (kit.Livery != null)
            {
                foreach (var skin in piece.GetComponentsInChildren<Renderer>())
                {
                    var swap = new Material[skin.sharedMaterials.Length];
                    for (int i = 0; i < swap.Length; i++) swap[i] = kit.Livery;
                    skin.sharedMaterials = swap;
                }
            }

            var box = ModelScaling.Measure(piece);
            if (box.size.y <= 0f) { Object.DestroyImmediate(piece); return; }

            // Sized to the wall it hangs on. The pack draws its banners for a tent pole
            // and at that size they read as playing cards pinned to forty metres of stone
            // — which is what the first pass looked like. A banner on a curtain is most of
            // the height of it. Scaled by its own height, so the cloth keeps its shape.
            piece.transform.localScale *= BannerRise / box.size.y;

            box = ModelScaling.Measure(piece);

            // By its top, not its foot: a banner is hung from the parapet and falls, and
            // seating it on the ground is a flag standing in a flowerbed.
            piece.transform.position += new Vector3(at.x - box.center.x,
                                                    at.y - box.max.y,
                                                    at.z - box.center.z);
        }

        /// <summary>Which of a turned model's own axes lies nearest a direction of its host's.</summary>
        static int Along(Quaternion turned, Vector3 direction)
        {
            int best = 0;
            float most = -1f;

            for (int axis = 0; axis < 3; axis++)
            {
                var own = Vector3.zero;
                own[axis] = 1f;

                float lined = Mathf.Abs(Vector3.Dot(turned * own, direction));
                if (lined <= most) continue;

                most = lined;
                best = axis;
            }

            return best;
        }

        /// <summary>How tall one curtain piece is, measured like its length and thickness.</summary>
        static float WallRise(Transform host, BuildingKit kit, int style)
        {
            var sample = Pick(kit.CurtainWalls, style);
            if (sample == null) return 0f;

            var probe = Object.Instantiate(sample, host);
            probe.transform.localRotation = kit.CurtainWalls.ZUp
                ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;

            float rise = ModelScaling.Measure(probe).size.y;

            if (Application.isPlaying) Object.Destroy(probe);
            else Object.DestroyImmediate(probe);

            return rise;
        }

        /// <summary>Where a banner's head hangs, as a share of the curtain's height.</summary>
        const float BannerDrop = 0.95f;

        /// <summary>How far a banner falls down the wall, in metres.</summary>
        const float BannerRise = 3.6f;

        /// <summary>How far out from the gateway a gatehouse banner hangs, as a share of the block.</summary>
        const float GateBannerAt = 0.3f;

        /// <summary>How far a wall's inner face is held clear of each corner, in metres.</summary>
        const float CourtyardClear = 6f;

        /// <summary>Buildings against the wall the gate faces, and against each side wall.</summary>
        const int BackRange = 4;
        const int SideRange = 3;

        /// <summary>Buildings on each short stretch of the gate wall.</summary>
        /// <summary>How far in from a gate tower the near piece of gear stands.</summary>
        const float GateGearIn = 0.62f;

        /// <summary>How far off the gate wall gear stands, in metres.</summary>
        const float GearStand = 2.6f;

        /// <summary>Where a gate tower stands, in wall panels either side of the gateway.</summary>
        const float GateFlank = 1.5f;

        /// <summary>How often a building in the bailey is the pack's plank lean-to.</summary>
        const float PlankShed = 0.35f;

        /// <summary>How wide one paving flag is laid, in metres, and how proud it sits.</summary>
        const float Flagstone = 7f;
        const float PaveLip = 0.05f;

        /// <summary>
        /// How far a flagstone stands over the earth it is laid in, in metres.
        ///
        /// A centimetre. It was five, which is nothing until you remember the flag is a
        /// slab with a thickness and that thickness is scaled up with its width — so five
        /// centimetres of the slab.s own side stood exposed all the way round every flag,
        /// and from straight above the yard was a field of grey rugs each with its own
        /// shadow. Bedded to a centimetre the side is gone and only the face shows, which
        /// is what a paving stone is.
        ///
        /// Not nought, because two surfaces at the same height flicker against each other.
        /// </summary>
        const float FlagProud = 0.01f;

        /// <summary>Share of the bailey that has stone laid over its earth.</summary>
        const float Flagged = 0.86f;

        /// <summary>Where the well, the tent and the gear stand, as a share of the yard.</summary>
        const float WellWide = 4.6f;

        /// <summary>How far off the wall the stair to the walk stands, in metres.</summary>
        const float StairStand = 1.5f;
        const float TentAt = 0.42f;

        /// <summary>
        /// The lane the gear stands in, as a share of the half-yard: outside the ranges
        /// and inside the open middle.
        /// </summary>
        const float GearLane = 0.58f;

        /// <summary>How far inside the gateway a brazier stands, in metres.</summary>
        const float BrazierStand = 3.5f;

        /// <summary>
        /// How thick a wall piece is, measured the way <see cref="WallLength"/> measures
        /// how long one is: the shorter of the two ground axes.
        /// </summary>
        static float WallDepth(Transform host, BuildingKit kit, int style)
        {
            var sample = Pick(kit.CurtainWalls, style);
            if (sample == null) return 0f;

            var probe = Object.Instantiate(sample, host);
            probe.transform.localRotation = kit.CurtainWalls.ZUp
                ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;

            var bounds = ModelScaling.Measure(probe);

            if (Application.isPlaying) Object.Destroy(probe);
            else Object.DestroyImmediate(probe);

            return Mathf.Min(bounds.size.x, bounds.size.z);
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
        public const int KeepCourses = 7;

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
        public const float CastleSpan = 38f;

        /// <summary>
        /// How much wider than the kit draws it a gate tower or a keep is built.
        ///
        /// The shaft is 2.78 m across and the artwork.s towers are about two and a half
        /// times as tall as they are wide. At 1.9 the tower is 5.3 m across, which puts a
        /// three-course tower at about that ratio and gives the gate a mass rather than a
        /// pair of posts.
        /// </summary>
        public const float Broad = 1.9f;

        /// <summary>The shaft in the set with windows cut down it: SM_Bld_Castle_Tower_04.</summary>
        public const int WindowedShaft = 3;
        public const int SlittedShaft = WindowedShaft;

        /// <summary>
        /// How many of the square shafts have windows in them.
        ///
        /// Three: SM_Bld_Castle_Tower_01 through _03. The fourth is the slitted one, which
        /// is a blank face with loops cut in it and reads as a blockhouse.
        /// </summary>
        public const int WindowedShafts = 3;

        /// <summary>How much stouter than the pack draws it a mural tower is built.</summary>
        public const float TowerGirth = 1.35f;

        /// <summary>
        /// How much of the curtain's height the gatehouse panel is built to.
        ///
        /// Two courses, so the arch reaches the wall head and the gateway is a quarter of
        /// the wall rather than an eighth of it. See the gate bay in Castle.
        /// </summary>
        public const float GateShare = 2f;

        /// <summary>How much of a hoarding is the raking timbers under its floor.</summary>
        public const float StrutShare = 0.4f;

        /// <summary>How far a tower.s crown is let into the shaft below it.</summary>
        public const float CrownSink = 0.33f;

        /// <summary>Courses of shaft in a corner tower, and in the pair flanking the gate.</summary>
        public const int CornerCourses = 5;
        public const int GateCourses = 5;

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
                           Vector3 at, float turn, int courses = WallCourses)
        {
            float top = at.y;

            for (int c = 0; c < courses; c++)
            {
                var wall = Run(host, Pick(kit.CurtainWalls, style), kit.CurtainWalls.ZUp,
                               new Vector3(at.x, c == 0 ? at.y : top - Seam, at.z), turn);
                if (wall == null) return;

                top = ModelScaling.Measure(wall).max.y;
            }

            if (!kit.WallTops.Any) return;

            Run(host, Any(kit.WallTops, rng), kit.WallTops.ZUp,
                new Vector3(at.x, top - Seam, at.z), turn);
        }

        /// <summary>
        /// One tower of the circuit: a windowed square shaft under a red pyramid, set in
        /// the wall line and built a little stouter than the pack draws it.
        ///
        /// <b>The girth is the point.</b> A tower the same thickness as the curtain sits
        /// flush in it and disappears — the wall runs past and there is nothing to see but
        /// a roof. Widened by a third it breaks the wall line on both faces, which is what
        /// a mural tower is for and what every tower on the plan does.
        ///
        /// Widened and not heightened, which is the distinction this file keeps having to
        /// relearn: the kit draws one width of shaft, so the only dial a plain stack has is
        /// height, and turning it up makes a flue.
        /// </summary>
        static void Mural(Transform host, BuildingKit kit, DeterministicRandom rng,
                          int courses, Vector3 at)
        {
            // Style three, which is SM_Bld_Castle_Tower_04: the one shaft in the set with
            // openings cut down it. The other three are blank faces with a string course,
            // and a castle of those is a castle nobody is looking out of.
            var tower = Tower(host, kit, rng, courses, spired: true, style: WindowedShaft);
            if (tower == null) return;

            tower.transform.localScale = new Vector3(TowerGirth, 1f, TowerGirth);
            tower.transform.localPosition = at;
        }

        /// <summary>
        /// The gate panel, built to its own scale.
        ///
        /// The pack draws one gate piece and it is the size of a curtain panel. That was
        /// right while the wall was one course; at two it left the arch at the foot of the
        /// face, an eighth of the wall's height where the plan has a quarter. So it is
        /// scaled to reach the wall head — which widens it past its bay into the panels
        /// either side, and that is what a gatehouse does.
        /// </summary>
        static GameObject Gatehouse(Transform host, BuildingKit kit, DeterministicRandom rng,
                                    float x, float z, float rise)
        {
            var arch = Run(host, Any(kit.Gates, rng), kit.Gates.ZUp, new Vector3(x, 0f, z), 0f);
            if (arch == null) return null;

            var box = ModelScaling.Measure(arch);
            if (box.size.y <= 0f || rise <= 0f) return arch;

            arch.transform.localScale *= rise / box.size.y;

            box = ModelScaling.Measure(arch);
            arch.transform.position += new Vector3(x - box.center.x, -box.min.y,
                                                   z - box.center.z);
            return arch;
        }

        /// <summary>
        /// The buttress at a wall joint, run up the full height of the wall.
        ///
        /// The pack draws its pillar exactly one curtain piece tall, which is the pack
        /// saying the two belong together — so a wall of two courses wants two of them.
        /// One left the buttresses stopping halfway up, which reads as a wall that was
        /// heightened later and never had its bays carried up.
        /// </summary>
        static void Buttress(Transform host, BuildingKit kit, DeterministicRandom rng,
                             Vector3 at, float turn)
        {
            if (!kit.Pillars.Any) return;

            float top = at.y;

            for (int c = 0; c < WallCourses; c++)
            {
                var pier = Run(host, Any(kit.Pillars, rng), kit.Pillars.ZUp,
                               new Vector3(at.x, c == 0 ? at.y : top - Seam, at.z), turn);
                if (pier == null) return;

                top = ModelScaling.Measure(pier).max.y;
            }
        }

        /// <summary>
        /// Courses of curtain in a wall.
        ///
        /// <b>Two, measured off the plan's front elevation rather than eyeballed.</b> The
        /// curtain there stands about a third of the castle's width; at one course it
        /// stood an eighth of it, and no arrangement of towers fixes that — a low wall
        /// round a wide yard is a stockyard however well the towers are placed. One piece
        /// is 5.09 m, which is three men; the drawing's is a wall nobody gets a ladder
        /// over.
        /// </summary>
        public const int WallCourses = 2;

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
