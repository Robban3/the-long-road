"""Builds the caravan wagon and exports it for Unity.

No pack in the project contains a cart, which is awkward for a game about escorting
one, so the missing piece is built here. A wagon is carpentry — a bed, plank sides,
hoops, a canvas, a running gear and two pairs of wheels — and carpentry is what a
script can build properly rather than approximate.

Kept low-poly with flat shading and per-material colours, matching how the
Quaternius packs are authored: no textures, no UVs, just coloured faces.

Run headless:
    blender --background --python wagon.py -- --out <fbx> --preview <png>
"""

import bpy
import math
import sys
import os

# Four values far enough apart to survive a 46 metre camera. The first version used
# three browns within a tenth of each other, and at distance the wagon read as one
# tan lump: body, wheels, hoops and seat all the same. Contrast is what gives a
# silhouette its parts back.
WOOD = (0.34, 0.22, 0.13, 1.0)          # body boards
WOOD_LIGHT = (0.52, 0.36, 0.21, 1.0)    # floor and wheels, the parts that catch light
WOOD_DARK = (0.22, 0.15, 0.10, 1.0)     # frame, rails, running gear
CANVAS = (0.86, 0.82, 0.72, 1.0)
CANVAS_SHADE = (0.64, 0.60, 0.52, 1.0)
INTERIOR = (0.10, 0.08, 0.07, 1.0)
IRON = (0.17, 0.17, 0.19, 1.0)

# Running gear. The rear wheels are markedly larger than the front, which is both
# how these carts were built and what stops the silhouette reading as a box on four
# identical discs.
REAR_RADIUS = 0.62
FRONT_RADIUS = 0.46
REAR_AXLE_Y = 0.82
FRONT_AXLE_Y = -0.95
TRACK = 0.64          # half the distance between the wheels
BED_TOP = 0.98        # top of the floor
BED_LENGTH = 2.70
BED_HALF_WIDTH = 0.58
SIDE_HEIGHT = 0.46
HOOP_RADIUS = 0.60

# How far the arch is squashed. A half circle as wide as the wagon puts more canvas
# above the rail than there is body below it, and the whole thing reads as a barrel
# on wheels — which is exactly what the first two attempts looked like. Flattened to
# 0.58 the arch is lower than the body is tall, and the wood keeps the silhouette.
HOOP_FLATTEN = 0.72


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for item in list(block):
            try:
                block.remove(item)
            except (RuntimeError, ReferenceError):
                pass


def material(name, colour):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = colour
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.9
    mat.diffuse_color = colour
    return mat


def box(name, size, location, mat, rotation=(0, 0, 0)):
    # The base cube is one metre on a side, so the scale is the size, not half of it.
    # Halving it — as this did — shrank every board, plank and beam to half its stated
    # dimension while the wheels, built from radii, stayed correct. That is most of why
    # the wagons read as stubby: a full-sized wheel against a half-sized body.
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    obj.data.materials.append(mat)
    return obj


def cylinder(name, radius, depth, location, mat, rotation=(0, 0, 0), sides=12):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=sides, radius=radius, depth=depth, location=location, rotation=rotation
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def ring(name, radius, location, mat, thickness=0.035, rotation=(math.pi / 2, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        location=location, rotation=rotation,
        major_radius=radius, minor_radius=thickness,
        major_segments=16, minor_segments=5,
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def wheel(prefix, y, radius, wood, dark, iron, spokes=10):
    """An open wheel: felloe, iron tyre, hub and spokes with daylight between them.

    A solid disc with spokes drawn on its face is cheaper and reads as nothing in
    particular — a cartwheel is recognised by its gaps as much as by its spokes.
    """
    parts = []
    for x in (-TRACK, TRACK):
        centre = (x, y, radius)
        parts += [
            ring(f"{prefix}Felloe{x:.0f}", radius * 0.94, centre, wood, thickness=0.06),
            ring(f"{prefix}Tyre{x:.0f}", radius, centre, iron, thickness=0.025),
            cylinder(f"{prefix}Hub{x:.0f}", radius * 0.15, 0.19, centre, dark,
                     rotation=(0, math.pi / 2, 0), sides=8),
            cylinder(f"{prefix}HubRing{x:.0f}", radius * 0.185, 0.05, centre, iron,
                     rotation=(0, math.pi / 2, 0), sides=8),
        ]

        # Each bar spans the diameter, so half as many bars as spokes.
        for i in range(spokes // 2):
            parts.append(box(f"{prefix}Spoke{x:.0f}_{i}",
                             (0.055, 0.05, radius * 1.86), centre, wood,
                             rotation=(i * math.pi / (spokes // 2), 0, 0)))
    return parts


def build_wagon():
    """A covered cart: body, canvas over hoops, seat, running gear, draught.

    Built from the outside in, because that is the order the eye reads it at the
    distance this game is played at. The silhouette is a long dark body with a pale
    arch over it and two pairs of wheels; everything else is detail that only pays off
    when the camera comes closer.
    """
    wood = material("Wood", WOOD)
    light = material("WoodLight", WOOD_LIGHT)
    dark = material("WoodDark", WOOD_DARK)
    canvas = material("Canvas", CANVAS)
    shade = material("CanvasShade", CANVAS_SHADE)
    inside = material("Interior", INTERIOR)
    iron = material("Iron", IRON)

    parts = []

    # --- body -------------------------------------------------------------------
    # Solid boards, not a rail with daylight behind it. The first version built the
    # sides from two thin plank courses with a gap between them, and from any angle
    # you looked straight through the wagon at the floor — which made the canvas look
    # like a barrel balanced on an open frame rather than the roof of a body.
    parts.append(box("Floor", (BED_HALF_WIDTH * 2, BED_LENGTH, 0.08),
                     (0, 0, BED_TOP - 0.04), light))

    for x in (-BED_HALF_WIDTH + 0.09, BED_HALF_WIDTH - 0.09):
        parts.append(box("Sill", (0.12, BED_LENGTH + 0.05, 0.13), (x, 0, BED_TOP - 0.13), dark))

    side_mid = BED_TOP + SIDE_HEIGHT / 2
    rail_top = BED_TOP + SIDE_HEIGHT + 0.08

    for x in (-BED_HALF_WIDTH, BED_HALF_WIDTH):
        outward = 0.045 if x > 0 else -0.045

        parts.append(box("SideBoard", (0.07, BED_LENGTH, SIDE_HEIGHT), (x, 0, side_mid), wood))
        parts.append(box("Rail", (0.13, BED_LENGTH + 0.04, 0.09),
                         (x, 0, BED_TOP + SIDE_HEIGHT + 0.045), dark))

        # A shadow line along the middle of the boards: one plain face reads as a crate.
        parts.append(box("Seam", (0.02, BED_LENGTH - 0.02, 0.035), (x + outward * 0.6, 0, side_mid),
                         dark))

        for y in (-1.12, -0.56, 0.0, 0.56, 1.12):
            parts.append(box("Stake", (0.06, 0.08, SIDE_HEIGHT + 0.05), (x + outward, y, side_mid),
                             dark))
        for y in (-0.84, 0.28, 1.12):
            parts.append(box("Strap", (0.035, 0.055, SIDE_HEIGHT + 0.01),
                             (x + outward * 1.7, y, side_mid), iron))

    parts.append(box("Tailboard", (BED_HALF_WIDTH * 2, 0.07, SIDE_HEIGHT),
                     (0, BED_LENGTH / 2 - 0.035, side_mid), wood))
    parts.append(box("Headboard", (BED_HALF_WIDTH * 2, 0.07, SIDE_HEIGHT + 0.10),
                     (0, -BED_LENGTH / 2 + 0.035, side_mid + 0.05), wood))

    # --- canvas over hoops --------------------------------------------------------
    # The canvas fills the hoops instead of sitting inside a cage of them. Drawn a
    # sixth smaller, as it was, the bows stood out around it like ribs around a
    # sausage and both ends showed a dark disc the size of the whole opening.
    def flattened(obj):
        obj.scale[1] *= HOOP_FLATTEN
        return obj

    hoop_ys = (-1.02, -0.51, 0.0, 0.51, 1.02)
    canvas_length = hoop_ys[-1] - hoop_ys[0] + 0.16

    parts.append(flattened(
        cylinder("Canvas", HOOP_RADIUS - 0.045, canvas_length, (0, 0, rail_top), canvas,
                 rotation=(math.pi / 2, 0, 0), sides=18)))

    for y in hoop_ys:
        parts.append(flattened(ring("Hoop", HOOP_RADIUS, (0, y, rail_top), dark, thickness=0.034)))

    # A rope along each side where the canvas is lashed to the rail.
    for x in (-HOOP_RADIUS + 0.10, HOOP_RADIUS - 0.10):
        parts.append(box("Lashing", (0.05, canvas_length - 0.05, 0.05),
                         (x, 0, rail_top + 0.06), shade))

    # The back is open; the front is not. One opening is a door, two is a tunnel, and
    # a tunnel through a wagon shows the ground on the far side.
    parts.append(flattened(
        cylinder("Opening", HOOP_RADIUS - 0.16, 0.05,
                 (0, hoop_ys[-1] + 0.09, rail_top), inside,
                 rotation=(math.pi / 2, 0, 0), sides=16)))

    # --- driver's seat ------------------------------------------------------------
    seat_z = BED_TOP + SIDE_HEIGHT + 0.16
    parts.append(box("Seat", (1.06, 0.40, 0.11), (0, -BED_LENGTH / 2 + 0.24, seat_z), dark))
    parts.append(box("SeatBack", (1.06, 0.08, 0.28),
                     (0, -BED_LENGTH / 2 + 0.43, seat_z + 0.19), dark))
    parts.append(box("Footboard", (0.96, 0.22, 0.07),
                     (0, -BED_LENGTH / 2 - 0.08, BED_TOP + 0.14), dark))

    # --- running gear -------------------------------------------------------------
    parts.append(box("BolsterRear", (TRACK * 2 + 0.18, 0.18, 0.12),
                     (0, REAR_AXLE_Y, REAR_RADIUS + 0.11), dark))
    parts.append(box("BolsterFront", (TRACK * 2 + 0.14, 0.18, 0.12),
                     (0, FRONT_AXLE_Y, FRONT_RADIUS + 0.11), dark))

    parts.append(cylinder("AxleRear", 0.055, TRACK * 2 + 0.06, (0, REAR_AXLE_Y, REAR_RADIUS),
                          iron, rotation=(0, math.pi / 2, 0), sides=8))
    parts.append(cylinder("AxleFront", 0.055, TRACK * 2 + 0.02, (0, FRONT_AXLE_Y, FRONT_RADIUS),
                          iron, rotation=(0, math.pi / 2, 0), sides=8))

    # Pillars from each bolster up to the sills, so the body stands on the gear
    # instead of hovering over it. Their absence is what made the wheels look like
    # they had been parked next to the wagon rather than under it.
    for y, radius in ((REAR_AXLE_Y, REAR_RADIUS), (FRONT_AXLE_Y, FRONT_RADIUS)):
        top = BED_TOP - 0.19
        height = top - (radius + 0.17)
        for x in (-0.42, 0.42):
            parts.append(box("Pillar", (0.11, 0.13, max(height, 0.06)),
                             (x, y, radius + 0.17 + max(height, 0.06) / 2), dark))

    reach_length = REAR_AXLE_Y - FRONT_AXLE_Y
    reach_tilt = math.atan2(REAR_RADIUS - FRONT_RADIUS, reach_length)
    parts.append(box("Reach", (0.14, reach_length + 0.24, 0.10),
                     (0, (REAR_AXLE_Y + FRONT_AXLE_Y) / 2,
                      (REAR_RADIUS + FRONT_RADIUS) / 2 + 0.11),
                     dark, rotation=(-reach_tilt, 0, 0)))

    parts += wheel("Rear", REAR_AXLE_Y, REAR_RADIUS, light, dark, iron)
    parts += wheel("Front", FRONT_AXLE_Y, FRONT_RADIUS, light, dark, iron)

    # --- draught ------------------------------------------------------------------
    # The pole starts at the front bolster and the hounds brace it from there. Drawn
    # floating ahead of the wagon with nothing joining the two, as it was, it read as
    # a plank lying on the ground.
    pole_z = FRONT_RADIUS + 0.05
    parts.append(box("Pole", (0.11, 1.30, 0.10), (0, FRONT_AXLE_Y - 0.62, pole_z), dark))

    for x in (-0.26, 0.26):
        parts.append(box("Hound", (0.08, 0.72, 0.08),
                         (x * 0.6, FRONT_AXLE_Y - 0.26, pole_z + 0.05), dark,
                         rotation=(0, 0, math.radians(11) * (1 if x > 0 else -1))))

    parts.append(box("Swingletree", (0.78, 0.09, 0.09),
                     (0, FRONT_AXLE_Y - 1.22, pole_z), dark))
    parts.append(cylinder("Ferrule", 0.065, 0.10, (0, FRONT_AXLE_Y - 1.22, pole_z), iron,
                          rotation=(math.pi / 2, 0, 0), sides=8))

    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    wagon = bpy.context.active_object
    wagon.name = "Wagon"

    # Flat shading: visible facets are the point of the style.
    bpy.ops.object.shade_flat()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return wagon


def setup_preview():
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = "Ground"
    ground.data.materials.append(material("Ground", (0.42, 0.52, 0.30, 1.0)))

    bpy.ops.object.camera_add(location=(4.6, -5.0, 2.6),
                              rotation=(math.radians(74), 0, math.radians(43)))
    bpy.context.scene.camera = bpy.context.active_object

    bpy.ops.object.light_add(type="SUN", location=(5, -4, 9))
    key = bpy.context.active_object
    key.data.energy = 3.2
    key.data.angle = math.radians(8)
    key.rotation_euler = (math.radians(52), 0, math.radians(35))

    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue

    scene.render.resolution_x = 1200
    scene.render.resolution_y = 850
    scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.60, 0.74, 0.88, 1.0)
        bg.inputs[1].default_value = 0.6


def arg(name, default=None):
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    return argv[argv.index(name) + 1] if name in argv else default


def main():
    clear_scene()
    build_wagon()

    preview = arg("--preview")
    if preview:
        setup_preview()
        bpy.context.scene.render.filepath = preview
        bpy.ops.render.render(write_still=True)
        print(f"[wagon] preview -> {preview}")

    out = arg("--out")
    if out:
        os.makedirs(os.path.dirname(out), exist_ok=True)
        bpy.ops.object.select_all(action="DESELECT")
        bpy.data.objects["Wagon"].select_set(True)

        # Unity is Y-up; Blender is Z-up. Baking the conversion here means the model
        # arrives standing, unlike the scenery packs which need correcting at runtime.
        bpy.ops.export_scene.fbx(
            filepath=out,
            use_selection=True,
            apply_unit_scale=True,
            axis_forward="-Z",
            axis_up="Y",
            mesh_smooth_type="FACE",
            bake_space_transform=True,
        )
        print(f"[wagon] fbx -> {out}")


main()
