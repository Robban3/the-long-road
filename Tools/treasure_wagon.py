"""Builds the treasure wagon and exports it for Unity.

A merchant's wagon rather than a supply cart: a timber frame divided into three
bays with braced side panels, arched lids over the load, spoked wheels and a
driver's bench with steps. All of it is constructed rather than sculpted, which is
what makes it something a script can build properly instead of approximate.

Run headless:
    blender --background --python treasure_wagon.py -- --out <fbx> --preview <png>
"""

import bpy
import math
import sys
import os

WOOD = (0.78, 0.62, 0.24, 1.0)
WOOD_DARK = (0.52, 0.39, 0.14, 1.0)
LID = (0.33, 0.62, 0.28, 1.0)
IRON = (0.30, 0.28, 0.22, 1.0)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials):
        for item in list(block):
            block.remove(item)


def material(name, colour):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = colour
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.85
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


def spoked_wheel(prefix, centre, radius, wood, iron, spokes=8):
    """A rim, a hub and radiating spokes, all lying in the plane across the wagon."""
    parts = [
        cylinder(f"{prefix}Rim", radius, 0.09, centre, wood,
                 rotation=(0, math.pi / 2, 0), sides=14),
        cylinder(f"{prefix}Tyre", radius * 1.04, 0.06, centre, iron,
                 rotation=(0, math.pi / 2, 0), sides=14),
        cylinder(f"{prefix}Hub", radius * 0.26, 0.13, centre, iron,
                 rotation=(0, math.pi / 2, 0), sides=8),
    ]

    # Spokes sit proud of the rim face rather than inside it. Buried in a solid disc
    # they are perfectly correct geometry that nobody can see; standing on the surface
    # they read as the spoked wheel the reference actually shows.
    for face in (-1, 1):
        offset = (centre[0] + face * 0.055, centre[1], centre[2])
        for i in range(spokes):
            angle = i * (math.pi / spokes)
            parts.append(box(f"{prefix}Spoke{face}{i}", (0.04, 0.055, radius * 1.7),
                             offset, dark_of(wood), rotation=(angle, 0, 0)))
    return parts


_SPOKE_MATERIAL = {}


def dark_of(wood):
    """Spokes in a darker shade so they stand out against the rim."""
    if "spoke" not in _SPOKE_MATERIAL:
        _SPOKE_MATERIAL["spoke"] = material("Spoke", WOOD_DARK)
    return _SPOKE_MATERIAL["spoke"]


def build_wagon():
    wood = material("Wood", WOOD)
    dark = material("WoodDark", WOOD_DARK)
    lid = material("Lid", LID)
    iron = material("Iron", IRON)

    parts = []

    half_w = 0.62
    bay_edges = (-1.02, -0.34, 0.34, 1.02)   # three bays between four posts

    # Floor and the sills the frame stands on.
    parts.append(box("Bed", (1.24, 2.12, 0.12), (0, 0, 0.80), dark))
    for x in (-half_w + 0.06, half_w - 0.06):
        parts.append(box("Sill", (0.10, 2.12, 0.10), (x, 0, 0.72), dark))

    # Corner and divider posts, then the rails that tie them together.
    for y in bay_edges:
        for x in (-half_w, half_w):
            parts.append(box("Post", (0.10, 0.10, 0.66), (x, y, 1.19), dark))

    for x in (-half_w, half_w):
        parts.append(box("RailTop", (0.09, 2.12, 0.09), (x, 0, 1.50), dark))
        parts.append(box("RailBottom", (0.09, 2.12, 0.09), (x, 0, 0.90), dark))

    parts.append(box("EndFront", (1.24, 0.09, 0.62), (0, -1.02, 1.19), wood))
    parts.append(box("EndBack", (1.24, 0.09, 0.62), (0, 1.02, 1.19), wood))

    # Side panels with an X-brace in each bay. The braces are what make the frame
    # read as carpentry rather than as a plain crate.
    for i in range(3):
        centre_y = (bay_edges[i] + bay_edges[i + 1]) / 2
        span = bay_edges[i + 1] - bay_edges[i]
        diagonal = math.sqrt(span ** 2 + 0.56 ** 2)
        tilt = math.atan2(0.56, span)

        for x in (-half_w, half_w):
            parts.append(box("Panel", (0.05, span - 0.06, 0.56), (x, centre_y, 1.19), wood))
            parts.append(box("BraceA", (0.06, diagonal, 0.07), (x, centre_y, 1.19),
                             dark, rotation=(tilt, 0, 0)))
            parts.append(box("BraceB", (0.06, diagonal, 0.07), (x, centre_y, 1.19),
                             dark, rotation=(-tilt, 0, 0)))

        # A low arched lid over each bay, flattened to the wagon's full width. Left as
        # a plain cylinder its radius has to match the width, which produced a green
        # drum swallowing the frame it was supposed to sit on.
        cover = cylinder(f"Lid{i}", 0.34, span - 0.05, (0, centre_y, 1.44), lid,
                         rotation=(math.pi / 2, 0, 0), sides=10)
        cover.scale[0] *= 1.85
        parts.append(cover)

    # Driver's bench and the steps up to it.
    parts.append(box("Bench", (1.10, 0.36, 0.10), (0, -1.30, 1.06), dark))
    parts.append(box("BenchBack", (1.10, 0.08, 0.30), (0, -1.13, 1.25), dark))
    parts.append(box("Step1", (0.34, 0.10, 0.07), (-0.70, -1.24, 0.80), dark))
    parts.append(box("Step2", (0.34, 0.10, 0.07), (-0.70, -1.38, 0.56), dark))

    for side, x in (("L", -0.70), ("R", 0.70)):
        parts += spoked_wheel(f"Rear{side}", (x, 0.74, 0.50), 0.50, wood, iron)
        parts += spoked_wheel(f"Front{side}", (x, -1.02, 0.40), 0.40, wood, iron)

    parts.append(cylinder("AxleRear", 0.05, 1.44, (0, 0.74, 0.50), iron,
                          rotation=(0, math.pi / 2, 0), sides=6))
    parts.append(cylinder("AxleFront", 0.05, 1.44, (0, -1.02, 0.40), iron,
                          rotation=(0, math.pi / 2, 0), sides=6))

    parts.append(box("Drawbar", (0.10, 0.90, 0.10), (0, -1.72, 0.42), dark))
    parts.append(box("Yoke", (0.70, 0.09, 0.09), (0, -2.10, 0.42), dark))

    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    wagon = bpy.context.active_object
    wagon.name = "WagonTreasure"
    bpy.ops.object.shade_flat()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return wagon


def setup_preview():
    bpy.ops.object.camera_add(location=(4.4, -4.8, 3.0),
                              rotation=(math.radians(68), 0, math.radians(43)))
    bpy.context.scene.camera = bpy.context.active_object

    bpy.ops.object.light_add(type="SUN", location=(4, -4, 8))
    sun = bpy.context.active_object
    sun.data.energy = 2.4
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))

    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue

    scene.render.resolution_x = 1100
    scene.render.resolution_y = 800
    scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.62, 0.76, 0.88, 1.0)


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
        print(f"[treasure] preview -> {preview}")

    out = arg("--out")
    if out:
        os.makedirs(os.path.dirname(out), exist_ok=True)
        bpy.ops.object.select_all(action="DESELECT")
        bpy.data.objects["WagonTreasure"].select_set(True)
        bpy.ops.export_scene.fbx(
            filepath=out,
            use_selection=True,
            apply_unit_scale=True,
            axis_forward="-Z",
            axis_up="Y",
            mesh_smooth_type="FACE",
            bake_space_transform=True,
        )
        print(f"[treasure] fbx -> {out}")


main()
