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

WOOD = (0.46, 0.30, 0.17, 1.0)
WOOD_LIGHT = (0.56, 0.38, 0.22, 1.0)
WOOD_DARK = (0.30, 0.19, 0.11, 1.0)
CANVAS = (0.84, 0.80, 0.70, 1.0)
CANVAS_SHADE = (0.62, 0.58, 0.50, 1.0)
INTERIOR = (0.13, 0.10, 0.08, 1.0)
IRON = (0.20, 0.20, 0.22, 1.0)

# Running gear. The rear wheels are markedly larger than the front, which is both
# how these carts were built and what stops the silhouette reading as a box on four
# identical discs.
REAR_RADIUS = 0.60
FRONT_RADIUS = 0.42
REAR_AXLE_Y = 0.86
FRONT_AXLE_Y = -1.02
TRACK = 0.62          # half the distance between the wheels
BED_TOP = 0.96
BED_LENGTH = 2.80
BED_HALF_WIDTH = 0.60
SIDE_HEIGHT = 0.44
HOOP_RADIUS = 0.54


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
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
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
    wood = material("Wood", WOOD)
    light = material("WoodLight", WOOD_LIGHT)
    dark = material("WoodDark", WOOD_DARK)
    canvas = material("Canvas", CANVAS)
    shade = material("CanvasShade", CANVAS_SHADE)
    inside = material("Interior", INTERIOR)
    iron = material("Iron", IRON)

    parts = []

    # --- bed --------------------------------------------------------------------
    parts.append(box("Bed", (BED_HALF_WIDTH * 2, BED_LENGTH, 0.09), (0, 0, BED_TOP), wood))

    # Floor boards, so the bed is planking rather than a slab.
    for i in range(5):
        x = -0.45 + i * 0.225
        parts.append(box("Floorboard", (0.17, BED_LENGTH - 0.04, 0.03),
                         (x, 0, BED_TOP + 0.05), light))

    # --- sides ------------------------------------------------------------------
    side_mid = BED_TOP + 0.05 + SIDE_HEIGHT / 2
    for x in (-BED_HALF_WIDTH, BED_HALF_WIDTH):
        outward = 0.03 if x > 0 else -0.03

        # Two plank courses with a gap between them: one flat face reads as a crate.
        for dz in (-SIDE_HEIGHT / 4 - 0.01, SIDE_HEIGHT / 4 + 0.01):
            parts.append(box("SidePlank", (0.06, BED_LENGTH - 0.06, SIDE_HEIGHT / 2 - 0.03),
                             (x, 0, side_mid + dz), wood))

        # Uprights and iron straps.
        for y in (-1.16, -0.58, 0.0, 0.58, 1.16):
            parts.append(box("Stake", (0.05, 0.07, SIDE_HEIGHT + 0.06),
                             (x + outward, y, side_mid), dark))
        for y in (-0.87, 0.29, 1.16):
            parts.append(box("Strap", (0.03, 0.05, SIDE_HEIGHT + 0.02),
                             (x + outward * 1.8, y, side_mid), iron))

    parts.append(box("Tailboard", (BED_HALF_WIDTH * 2, 0.06, SIDE_HEIGHT),
                     (0, BED_LENGTH / 2 - 0.03, side_mid), wood))
    parts.append(box("Headboard", (BED_HALF_WIDTH * 2, 0.06, SIDE_HEIGHT + 0.16),
                     (0, -BED_LENGTH / 2 + 0.03, side_mid + 0.08), wood))

    # --- hood -------------------------------------------------------------------
    # A shallow arch, wider than it is tall, and narrower than the bed so the plank
    # sides stay in the silhouette. A full half-circle as wide as the wagon makes the
    # hood the whole shape and the result reads as a barrel on a frame.
    hood_top = side_mid + SIDE_HEIGHT / 2 - 0.02
    hood_flatten = 0.60

    def flattened(obj):
        obj.scale[1] *= hood_flatten
        return obj

    for y in (-0.52, -0.08, 0.36, 0.80, 1.24):
        parts.append(flattened(ring("Hoop", HOOP_RADIUS, (0, y, hood_top), dark, thickness=0.032)))

    # The canvas spans the middle hoops only, leaving the bow open at both ends.
    # Capped, each end presented a flat white disc the size of the whole opening and
    # the wagon read as a barrel however well the rest was built.
    parts.append(flattened(
        cylinder("Canvas", HOOP_RADIUS - 0.02, 1.20, (0, 0.36, hood_top), canvas,
                 rotation=(math.pi / 2, 0, 0), sides=16)))

    # Dark discs behind each opening, so you look into shade rather than at nothing.
    for y in (-0.30, 1.02):
        parts.append(flattened(
            cylinder("Interior", HOOP_RADIUS - 0.06, 0.03, (0, y, hood_top), inside,
                     rotation=(math.pi / 2, 0, 0), sides=16)))

    # --- driver's seat ----------------------------------------------------------
    # The seat sits on the headboard rather than hovering ahead of it.
    seat_z = side_mid + SIDE_HEIGHT / 2 + 0.14
    parts.append(box("Seat", (1.02, 0.38, 0.10), (0, -BED_LENGTH / 2 + 0.22, seat_z), dark))
    parts.append(box("SeatBack", (1.02, 0.07, 0.26),
                     (0, -BED_LENGTH / 2 + 0.40, seat_z + 0.18), dark))
    parts.append(box("Footboard", (0.94, 0.20, 0.06),
                     (0, -BED_LENGTH / 2 - 0.06, BED_TOP + 0.16), dark))

    # --- running gear -----------------------------------------------------------
    parts.append(box("BolsterRear", (TRACK * 2 + 0.16, 0.16, 0.11),
                     (0, REAR_AXLE_Y, REAR_RADIUS + 0.10), dark))
    parts.append(box("BolsterFront", (TRACK * 2 + 0.12, 0.16, 0.11),
                     (0, FRONT_AXLE_Y, FRONT_RADIUS + 0.10), dark))

    parts.append(cylinder("AxleRear", 0.05, TRACK * 2 + 0.08, (0, REAR_AXLE_Y, REAR_RADIUS),
                          iron, rotation=(0, math.pi / 2, 0), sides=8))
    parts.append(cylinder("AxleFront", 0.05, TRACK * 2 + 0.04, (0, FRONT_AXLE_Y, FRONT_RADIUS),
                          iron, rotation=(0, math.pi / 2, 0), sides=8))

    # The reach ties the two axles together and slopes with them.
    reach_length = REAR_AXLE_Y - FRONT_AXLE_Y
    reach_tilt = math.atan2(REAR_RADIUS - FRONT_RADIUS, reach_length)
    parts.append(box("Reach", (0.13, reach_length + 0.2, 0.09),
                     (0, (REAR_AXLE_Y + FRONT_AXLE_Y) / 2,
                      (REAR_RADIUS + FRONT_RADIUS) / 2 + 0.10),
                     dark, rotation=(-reach_tilt, 0, 0)))

    # Braces from the front bolster up to the bed.
    for x in (-0.30, 0.30):
        parts.append(box("Hound", (0.07, 0.60, 0.07),
                         (x, FRONT_AXLE_Y + 0.28, FRONT_RADIUS + 0.30), dark,
                         rotation=(math.radians(38), 0, 0)))

    parts += wheel("Rear", REAR_AXLE_Y, REAR_RADIUS, wood, dark, iron)
    parts += wheel("Front", FRONT_AXLE_Y, FRONT_RADIUS, wood, dark, iron)

    # --- draught ----------------------------------------------------------------
    parts.append(box("Pole", (0.10, 1.05, 0.09),
                     (0, FRONT_AXLE_Y - 0.55, FRONT_RADIUS + 0.02), dark))
    parts.append(box("Swingletree", (0.72, 0.08, 0.08),
                     (0, FRONT_AXLE_Y - 1.02, FRONT_RADIUS + 0.02), dark))

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
