"""Renders The Long Road's two views to PNG without Unity.

The engine's own captures — Arna.Editor.ArnaSetup.CaptureLevelPreview and
CapturePlayScene — need a Unity install and a machine with a GPU. This draws the same
two pictures from the same generated level: a software rasteriser with a z-buffer, a
shadow map and the ground shader's own lighting arithmetic, over the level that
Tools/arna_level.py generates from a seed.

    python3 render_screens.py --chapter 1 --level 5 --out ../docs/screenshots

What is faithful, and what is not:

  * The level is. Terrain, elevation, rivers and fords, the three corridors, and every
    enemy, trap and cache come out of the port of the generator, which reproduces the
    figures docs/status.md records for 1-5.

  * The camera, the sun and the ground shading are. Angles, field of view, orthographic
    size, light directions and colours, the half-lambert term, the trilight ambient and
    the linear fog are all read off ArnaSetup.cs and TerrainGround.shader.

  * The models are not. Every FBX in the repository is a Git LFS pointer and the packs
    are not fetched here, so trees, wagons, troops and buildings are drawn as
    procedural stand-ins at the sizes TerrainDecorator gives them — a pine is a cone of
    the right height in the right place, not the pine the player sees. The ground's
    detail texture is a Git LFS pointer too, so its grain is value noise at the
    shader's two tiling scales rather than the photograph.

So: the country is the real one, the dressing is a sketch of it.
"""

from __future__ import annotations

import argparse
import math
import os
from dataclasses import dataclass
from typing import List, Optional, Sequence, Tuple

import numpy as np
from PIL import Image

import arna_level as A


# --- Palettes, read off TerrainPalette.cs ----------------------------------------

MAP_COLORS = np.array([
    [0.72, 0.62, 0.42],   # Road
    [0.58, 0.72, 0.38],   # Plains
    [0.20, 0.40, 0.23],   # Forest
    [0.36, 0.38, 0.24],   # Marsh
    [0.48, 0.68, 0.70],   # Ford
    [0.58, 0.55, 0.52],   # MountainPass
    [0.16, 0.31, 0.52],   # Water
    [0.28, 0.26, 0.26],   # Cliff
])

GROUND_COLORS = np.array([
    [0.50, 0.43, 0.33],   # Road
    [0.42, 0.50, 0.29],   # Plains
    [0.30, 0.38, 0.24],   # Forest
    [0.34, 0.37, 0.27],   # Marsh
    [0.40, 0.46, 0.42],   # Ford
    [0.46, 0.44, 0.40],   # MountainPass
    [0.13, 0.24, 0.34],   # Water
    [0.33, 0.31, 0.29],   # Cliff
])

START_COLOR = np.array([0.35, 0.95, 0.45])
GOAL_COLOR = np.array([0.98, 0.82, 0.25])
ROUTE_FAST = np.array([0.98, 0.34, 0.30])
ROUTE_SAFE = np.array([0.40, 0.85, 0.98])
ROUTE_ODD = np.array([0.98, 0.72, 0.24])
ROUTE_OPACITY = 0.72
ROUTE_WIDTH = 2.2
ROUTE_LIFT = 0.35

SKY_COLOR = np.array([0.62, 0.75, 0.85])
PLAN_BACKGROUND = np.array([0.10, 0.11, 0.10])

AMBIENT_SKY = np.array([0.36, 0.42, 0.50])
AMBIENT_EQUATOR = np.array([0.33, 0.36, 0.36])
AMBIENT_GROUND = np.array([0.21, 0.22, 0.17])

WATER_DEPTH = 0.2

# Stand-in colours for the models that are not fetched here.
WOOD = np.array([0.40, 0.28, 0.18])
WOOD_PALE = np.array([0.55, 0.44, 0.30])
PINE_GREEN = np.array([0.14, 0.28, 0.18])
LEAF_GREEN = np.array([0.24, 0.42, 0.21])
DEAD_WOOD = np.array([0.44, 0.39, 0.31])
STONE = np.array([0.46, 0.45, 0.42])
MOUNTAIN_STONE = np.array([0.40, 0.39, 0.38])
GRASS = np.array([0.34, 0.50, 0.24])
PLASTER = np.array([0.74, 0.68, 0.56])
ROOF = np.array([0.42, 0.26, 0.20])
WHEAT = np.array([0.78, 0.66, 0.30])
CANVAS = np.array([0.84, 0.80, 0.70])

WAGON_COLORS = {
    A.SUPPLY: np.array([0.85, 0.72, 0.42]),
    A.WAR: np.array([0.72, 0.45, 0.35]),
    A.TREASURE: np.array([0.95, 0.82, 0.30]),
}

TROOP_STEEL = np.array([0.55, 0.58, 0.63])
TROOP_TABARD = np.array([0.30, 0.45, 0.70])
ENEMY_RED = np.array([0.72, 0.24, 0.20])
WOLF_GREY = np.array([0.42, 0.40, 0.40])
CACHE_GOLD = np.array([0.95, 0.85, 0.35])


# --- Camera ----------------------------------------------------------------------

@dataclass
class Camera:
    position: np.ndarray
    forward: np.ndarray
    right: np.ndarray
    up: np.ndarray
    width: int
    height: int
    orthographic: bool
    fov: float = 50.0
    ortho_size: float = 128.0
    near: float = 0.5

    @staticmethod
    def _basis(forward: np.ndarray, world_up: np.ndarray):
        f = forward / np.linalg.norm(forward)
        if abs(float(np.dot(f, world_up))) > 0.999:
            world_up = np.array([0.0, 0.0, 1.0])
        r = np.cross(world_up, f)
        r /= np.linalg.norm(r)
        u = np.cross(f, r)
        return f, r, u

    @classmethod
    def perspective(cls, position, target, fov, width, height, near=0.5):
        f, r, u = cls._basis(np.asarray(target, float) - np.asarray(position, float),
                             np.array([0.0, 1.0, 0.0]))
        return cls(np.asarray(position, float), f, r, u, width, height, False,
                   fov=fov, near=near)

    @classmethod
    def orthographic_view(cls, position, direction, size, width, height, near=0.0):
        f, r, u = cls._basis(np.asarray(direction, float), np.array([0.0, 1.0, 0.0]))
        return cls(np.asarray(position, float), f, r, u, width, height, True,
                   ortho_size=size, near=near)

    def to_view(self, points: np.ndarray) -> np.ndarray:
        """World positions to camera space: x right, y up, z forward."""
        d = points - self.position
        return np.stack([d @ self.right, d @ self.up, d @ self.forward], axis=1)

    def to_screen(self, view: np.ndarray) -> np.ndarray:
        aspect = self.width / self.height
        if self.orthographic:
            ndc_x = view[:, 0] / (self.ortho_size * aspect)
            ndc_y = view[:, 1] / self.ortho_size
        else:
            tan_half = math.tan(math.radians(self.fov) * 0.5)
            z = np.maximum(view[:, 2], 1e-6)
            ndc_x = view[:, 0] / (z * tan_half * aspect)
            ndc_y = view[:, 1] / (z * tan_half)

        return np.stack([(ndc_x + 1.0) * 0.5 * self.width,
                         (1.0 - ndc_y) * 0.5 * self.height], axis=1)


def euler_forward(pitch_deg: float, yaw_deg: float) -> np.ndarray:
    """Unity's Quaternion.Euler(pitch, yaw, 0) applied to forward, in Unity's handedness."""
    pitch = math.radians(pitch_deg)
    yaw = math.radians(yaw_deg)
    v = np.array([0.0, -math.sin(pitch), math.cos(pitch)])
    cos_y, sin_y = math.cos(yaw), math.sin(yaw)
    return np.array([v[0] * cos_y + v[2] * sin_y, v[1], -v[0] * sin_y + v[2] * cos_y])


# --- Mesh accumulation -----------------------------------------------------------

class Mesh:
    """Vertices, triangles and per-vertex colour and normal, gathered before rasterising."""

    def __init__(self):
        self.vertices: List[np.ndarray] = []
        self.normals: List[np.ndarray] = []
        self.colors: List[np.ndarray] = []
        self.material: List[np.ndarray] = []
        self.triangles: List[np.ndarray] = []
        self._count = 0

    def add(self, vertices, triangles, colors, normals=None, material=0.0):
        vertices = np.asarray(vertices, float)
        triangles = np.asarray(triangles, int)
        colors = np.asarray(colors, float)
        if colors.ndim == 1:
            colors = np.tile(colors, (len(vertices), 1))
        if normals is None:
            normals = _vertex_normals(vertices, triangles)

        self.vertices.append(vertices)
        self.normals.append(np.asarray(normals, float))
        self.colors.append(colors)
        self.material.append(np.full(len(vertices), material, float))
        self.triangles.append(triangles + self._count)
        self._count += len(vertices)

    def finish(self):
        if not self.vertices:
            empty = np.zeros((0, 3))
            return empty, np.zeros((0, 3), int), empty, empty, np.zeros(0)
        return (np.concatenate(self.vertices), np.concatenate(self.triangles),
                np.concatenate(self.colors), np.concatenate(self.normals),
                np.concatenate(self.material))


def _vertex_normals(vertices: np.ndarray, triangles: np.ndarray) -> np.ndarray:
    normals = np.zeros_like(vertices)
    a = vertices[triangles[:, 0]]
    b = vertices[triangles[:, 1]]
    c = vertices[triangles[:, 2]]
    face = np.cross(b - a, c - a)
    for i in range(3):
        np.add.at(normals, triangles[:, i], face)
    lengths = np.linalg.norm(normals, axis=1, keepdims=True)
    lengths[lengths == 0] = 1.0
    return normals / lengths


# --- Rasteriser ------------------------------------------------------------------

class Frame:
    """Depth and G-buffer for one pass: albedo, normal, world position, material id."""

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height
        self.depth = np.full((height, width), np.inf)
        self.albedo = np.zeros((height, width, 3))
        self.normal = np.zeros((height, width, 3))
        self.world = np.zeros((height, width, 3))
        self.material = np.full((height, width), -1.0)
        self.covered = np.zeros((height, width), bool)


def rasterise(frame: Frame, camera: Camera, vertices, triangles, colors, normals,
              material, depth_only: bool = False) -> None:
    """Scanline rasterisation with a z-buffer and perspective-correct interpolation."""
    if len(triangles) == 0:
        return

    view = camera.to_view(vertices)
    near = max(camera.near, 1e-4)

    attrs = np.concatenate([colors, normals, vertices, material[:, None]], axis=1)

    tri_view = view[triangles]           # (T, 3, 3)
    tri_attr = attrs[triangles]          # (T, 3, K)

    if camera.orthographic:
        keep = np.ones(len(triangles), bool)
    else:
        keep = (tri_view[:, :, 2] > near).any(axis=1)
    tri_view = tri_view[keep]
    tri_attr = tri_attr[keep]

    for i in range(len(tri_view)):
        v = tri_view[i]
        a = tri_attr[i]

        if not camera.orthographic and (v[:, 2] <= near).any():
            clipped = _clip_near(v, a, near)
            if clipped is None:
                continue
            for poly_v, poly_a in clipped:
                _raster_one(frame, camera, poly_v, poly_a, depth_only)
        else:
            _raster_one(frame, camera, v, a, depth_only)


def _clip_near(v: np.ndarray, a: np.ndarray, near: float):
    """Clips one triangle against the near plane, returning zero, one or two triangles."""
    inside = [i for i in range(3) if v[i, 2] > near]
    outside = [i for i in range(3) if v[i, 2] <= near]
    if not inside:
        return None

    def cut(i, o):
        t = (near - v[o, 2]) / (v[i, 2] - v[o, 2])
        return v[o] + (v[i] - v[o]) * t, a[o] + (a[i] - a[o]) * t

    if len(inside) == 1:
        i = inside[0]
        v1, a1 = cut(i, outside[0])
        v2, a2 = cut(i, outside[1])
        return [(np.stack([v[i], v1, v2]), np.stack([a[i], a1, a2]))]

    i, j = inside
    o = outside[0]
    v1, a1 = cut(i, o)
    v2, a2 = cut(j, o)
    return [(np.stack([v[i], v1, v[j]]), np.stack([a[i], a1, a[j]])),
            (np.stack([v1, v2, v[j]]), np.stack([a1, a2, a[j]]))]


def _raster_one(frame: Frame, camera: Camera, v: np.ndarray, a: np.ndarray,
                depth_only: bool) -> None:
    screen = camera.to_screen(v)
    z = v[:, 2]

    min_x = int(math.floor(screen[:, 0].min()))
    max_x = int(math.ceil(screen[:, 0].max()))
    min_y = int(math.floor(screen[:, 1].min()))
    max_y = int(math.ceil(screen[:, 1].max()))

    min_x = max(min_x, 0)
    min_y = max(min_y, 0)
    max_x = min(max_x, frame.width - 1)
    max_y = min(max_y, frame.height - 1)
    if min_x > max_x or min_y > max_y:
        return

    x0, y0 = screen[0]
    x1, y1 = screen[1]
    x2, y2 = screen[2]

    area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
    if abs(area) < 1e-9:
        return

    xs = np.arange(min_x, max_x + 1) + 0.5
    ys = np.arange(min_y, max_y + 1) + 0.5
    px, py = np.meshgrid(xs, ys)

    w0 = ((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py)) / area
    w1 = ((x2 - px) * (y0 - py) - (x0 - px) * (y2 - py)) / area
    w2 = 1.0 - w0 - w1

    inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
    if not inside.any():
        return

    if camera.orthographic:
        depth = w0 * z[0] + w1 * z[1] + w2 * z[2]
        bary = np.stack([w0, w1, w2], axis=-1)
    else:
        # Perspective correct: interpolate 1/z, then recover the attributes through it.
        inv_z = np.stack([1.0 / z[0], 1.0 / z[1], 1.0 / z[2]])
        denom = w0 * inv_z[0] + w1 * inv_z[1] + w2 * inv_z[2]
        denom = np.where(np.abs(denom) < 1e-12, 1e-12, denom)
        depth = 1.0 / denom
        bary = np.stack([w0 * inv_z[0], w1 * inv_z[1], w2 * inv_z[2]], axis=-1) \
            / denom[..., None]

    window = frame.depth[min_y:max_y + 1, min_x:max_x + 1]
    hit = inside & (depth < window) & (depth > 0)
    if not hit.any():
        return

    window[hit] = depth[hit]
    if depth_only:
        return

    values = bary[hit] @ a                     # (n, K)
    ys_hit, xs_hit = np.nonzero(hit)
    ys_hit += min_y
    xs_hit += min_x

    frame.albedo[ys_hit, xs_hit] = values[:, 0:3]
    frame.normal[ys_hit, xs_hit] = values[:, 3:6]
    frame.world[ys_hit, xs_hit] = values[:, 6:9]
    frame.material[ys_hit, xs_hit] = values[:, 9]
    frame.covered[ys_hit, xs_hit] = True


# --- Shading ---------------------------------------------------------------------

def ambient(normals: np.ndarray) -> np.ndarray:
    """Unity's trilight ambient: sky above, ground below, equator across the middle."""
    up = normals[..., 1:2]
    upper = AMBIENT_EQUATOR + (AMBIENT_SKY - AMBIENT_EQUATOR) * np.clip(up, 0.0, 1.0)
    lower = AMBIENT_EQUATOR + (AMBIENT_GROUND - AMBIENT_EQUATOR) * np.clip(-up, 0.0, 1.0)
    return np.where(up >= 0.0, upper, lower)


def _value_noise(x: np.ndarray, y: np.ndarray, seed: int) -> np.ndarray:
    """Stands in for the ground's detail photograph, which is a Git LFS pointer here."""
    xi = np.floor(x).astype(np.int64)
    yi = np.floor(y).astype(np.int64)
    fx = x - xi
    fy = y - yi
    fx = fx * fx * (3.0 - 2.0 * fx)
    fy = fy * fy * (3.0 - 2.0 * fy)

    def h(ix, iy):
        n = (ix * 374761393 + iy * 668265263 + seed * 2246822519) & 0xFFFFFFFF
        n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
        n = n ^ (n >> 16)
        return (n >> 8) * (1.0 / 16777216.0)

    a = h(xi, yi) + (h(xi + 1, yi) - h(xi, yi)) * fx
    b = h(xi, yi + 1) + (h(xi + 1, yi + 1) - h(xi, yi + 1)) * fx
    return a + (b - a) * fy


def ground_detail(world: np.ndarray) -> np.ndarray:
    """TerrainGround.shader's brightness modulation: two tilings, around 1.0."""
    u = world[..., 0]
    v = world[..., 2]
    # Narrowed around the midpoint: the shader samples a photograph of forest floor,
    # whose green channel clusters near mid grey, while plain value noise runs the whole
    # range and mottles the ground into camouflage at the shader's own strengths.
    fine = 0.5 + (_value_noise(u / 6.0, v / 6.0, 7717) - 0.5) * 0.45
    macro = 0.5 + (_value_noise(u / 41.0, v / 41.0, 4483) - 0.5) * 0.55
    return 1.0 + (fine - 0.5) * 0.55 * 2.0 + (macro - 0.5) * 0.35 * 2.0


def shade(frame: Frame, sun_direction: np.ndarray, sun_color: np.ndarray,
          sun_intensity: float, shadow: np.ndarray, shadow_strength: float,
          background: np.ndarray, fog: Optional[Tuple[float, float]],
          camera: Camera) -> np.ndarray:
    """The ground shader's arithmetic, applied to the whole frame at once."""
    normals = frame.normal
    lengths = np.linalg.norm(normals, axis=2, keepdims=True)
    lengths[lengths == 0] = 1.0
    normals = normals / lengths

    to_light = -sun_direction / np.linalg.norm(sun_direction)
    ndotl = np.clip(np.einsum("ijk,k->ij", normals, to_light), 0.0, 1.0)

    is_ground = frame.material < 0.5
    # Half-lambert on the ground, so terrain turned from the sun still reads as terrain;
    # straight lambert on everything standing on it.
    diffuse = np.where(is_ground, ndotl * 0.75 + 0.25, ndotl)

    lit_shadow = 1.0 - (1.0 - shadow) * shadow_strength
    detail = np.where(is_ground, ground_detail(frame.world), 1.0)

    light = (sun_color * sun_intensity)[None, None, :] \
        * (diffuse * lit_shadow)[..., None]
    color = frame.albedo * detail[..., None] * (ambient(normals) + light)

    if fog is not None:
        distance = np.linalg.norm(frame.world - camera.position, axis=2)
        start, end = fog
        factor = np.clip((end - distance) / (end - start), 0.0, 1.0)
        color = color * factor[..., None] + SKY_COLOR[None, None, :] * (1.0 - factor)[..., None]

    return np.where(frame.covered[..., None], color, background[None, None, :])


def cast_shadows(camera_bounds, sun_direction, vertices, triangles, resolution=2048):
    """Renders the scene from the sun into a depth buffer, then hands back a sampler."""
    centre, radius = camera_bounds
    direction = sun_direction / np.linalg.norm(sun_direction)
    position = centre - direction * (radius * 2.0)

    light_camera = Camera.orthographic_view(position, direction, radius,
                                            resolution, resolution)
    frame = Frame(resolution, resolution)

    zeros = np.zeros((len(vertices), 3))
    rasterise(frame, light_camera, vertices, triangles, zeros, zeros,
              np.zeros(len(vertices)), depth_only=True)

    def sample(world: np.ndarray) -> np.ndarray:
        view = light_camera.to_view(world.reshape(-1, 3))
        screen = light_camera.to_screen(view)
        xs = np.clip(screen[:, 0].astype(int), 0, resolution - 1)
        ys = np.clip(screen[:, 1].astype(int), 0, resolution - 1)
        stored = frame.depth[ys, xs]
        # Depth bias in metres, scaled to the map: without it the ground shadows itself
        # in stripes wherever the light texel straddles a slope.
        lit = (view[:, 2] <= stored + radius * 0.004) | ~np.isfinite(stored)
        return lit.reshape(world.shape[:2]).astype(float)

    return sample


# --- Terrain geometry ------------------------------------------------------------

def corner_height(grid: A.TileGrid, cx: int, cy: int, height_scale: float) -> float:
    """Corner height with the riverbed dug in, spread across the tiles that meet there."""
    elevation = grid.corner_elevation(cx, cy) * height_scale

    water = tiles = 0
    for dy in (-1, 0):
        for dx in (-1, 0):
            x, y = cx + dx, cy + dy
            if not grid.in_bounds(x, y):
                continue
            tiles += 1
            if grid.at(x, y) == A.WATER:
                water += 1

    if tiles == 0 or water == 0:
        return elevation
    return elevation - height_scale * WATER_DEPTH * water / tiles


def corner_color(grid: A.TileGrid, cx: int, cy: int) -> np.ndarray:
    """Ground colour at a corner: averaged, except water, which needs a majority."""
    total = np.zeros(3)
    tiles = water = 0
    road = False

    for dy in (-1, 0):
        for dx in (-1, 0):
            x, y = cx + dx, cy + dy
            if not grid.in_bounds(x, y):
                continue
            tiles += 1
            terrain = grid.at(x, y)
            if terrain == A.WATER:
                water += 1
                continue
            if terrain == A.ROAD:
                road = True
            total += GROUND_COLORS[terrain]

    if tiles == 0:
        return np.zeros(3)
    if road:
        return GROUND_COLORS[A.ROAD]
    if water * 2 > tiles:
        return GROUND_COLORS[A.WATER]
    return total / (tiles - water)


def _tint(color: np.ndarray, cx: int, cy: int) -> np.ndarray:
    """A deterministic brightness shift per corner, so the ground is not uniform."""
    h = (cx * 73856093) ^ (cy * 19349663)
    h &= 0xFFFFFFFF
    h ^= h >> 13
    h = (h * 1274126177) & 0xFFFFFFFF
    h ^= h >> 16
    shade = 1.0 + ((h & 0xFFFF) / 65535.0 - 0.5) * 0.11
    return color * shade


def corner_normal(grid: A.TileGrid, cx: int, cy: int, height_scale: float) -> np.ndarray:
    west = corner_height(grid, cx - 1, cy, height_scale)
    east = corner_height(grid, cx + 1, cy, height_scale)
    south = corner_height(grid, cx, cy - 1, height_scale)
    north = corner_height(grid, cx, cy + 1, height_scale)
    n = np.array([(west - east) * 0.5, A.TILE_SIZE, (south - north) * 0.5])
    return n / np.linalg.norm(n)


def build_terrain(mesh: Mesh, level: A.LevelMap, height_scale: float,
                  as_ground: bool, mark_endpoints: bool, faceted: bool = False) -> None:
    """One quad per tile: colours per tile corner, heights and normals shared.

    `faceted` gives each triangle its own normal instead, so the ground breaks into
    hard-edged planes. The engine's mesh does not do this — TerrainMeshBuilder samples
    normals at the corners precisely so light runs smoothly across the tile seams —
    but it is the single largest difference between our picture and the faceted
    low-poly landscapes this game is being compared to, and it costs one flag to see.
    """
    grid = level.grid
    w, h = grid.width, grid.height

    heights = np.array([[corner_height(grid, x, y, height_scale)
                         for x in range(w + 1)] for y in range(h + 1)])
    normals = np.array([[corner_normal(grid, x, y, height_scale)
                         for x in range(w + 1)] for y in range(h + 1)])
    if as_ground:
        colors = np.array([[_tint(corner_color(grid, x, y), x, y)
                            for x in range(w + 1)] for y in range(h + 1)])

    vertices = np.zeros((w * h * 4, 3))
    vcolors = np.zeros((w * h * 4, 3))
    vnormals = np.zeros((w * h * 4, 3))
    triangles = np.zeros((w * h * 2, 3), int)

    for y in range(h):
        for x in range(w):
            i = y * w + x
            v = i * 4
            x0, z0 = x * A.TILE_SIZE, y * A.TILE_SIZE
            x1, z1 = x0 + A.TILE_SIZE, z0 + A.TILE_SIZE

            vertices[v + 0] = (x0, heights[y][x], z0)
            vertices[v + 1] = (x1, heights[y][x + 1], z0)
            vertices[v + 2] = (x1, heights[y + 1][x + 1], z1)
            vertices[v + 3] = (x0, heights[y + 1][x], z1)

            vnormals[v + 0] = normals[y][x]
            vnormals[v + 1] = normals[y][x + 1]
            vnormals[v + 2] = normals[y + 1][x + 1]
            vnormals[v + 3] = normals[y + 1][x]

            if as_ground:
                vcolors[v + 0] = colors[y][x]
                vcolors[v + 1] = colors[y][x + 1]
                vcolors[v + 2] = colors[y + 1][x + 1]
                vcolors[v + 3] = colors[y + 1][x]
            else:
                vcolors[v:v + 4] = MAP_COLORS[grid.tiles[i]]

            if mark_endpoints and i == level.start_index:
                vcolors[v:v + 4] = START_COLOR
            elif mark_endpoints and i == level.goal_index:
                vcolors[v:v + 4] = GOAL_COLOR

            t = i * 2
            triangles[t] = (v + 0, v + 1, v + 2)
            triangles[t + 1] = (v + 0, v + 2, v + 3)

    if faceted:
        # One normal per triangle, so every plane shades on its own.
        corners = vertices[triangles]
        face = np.cross(corners[:, 1] - corners[:, 0], corners[:, 2] - corners[:, 0])
        lengths = np.linalg.norm(face, axis=1, keepdims=True)
        lengths[lengths == 0] = 1.0
        face /= lengths

        flat_vertices = corners.reshape(-1, 3)
        flat_colors = vcolors[triangles].reshape(-1, 3)
        flat_normals = np.repeat(face, 3, axis=0)
        flat_triangles = np.arange(len(flat_vertices)).reshape(-1, 3)
        mesh.add(flat_vertices, flat_triangles, flat_colors, flat_normals, material=0.0)
        return

    mesh.add(vertices, triangles, vcolors, vnormals, material=0.0)


# --- Stand-in models -------------------------------------------------------------

def _cone(segments: int = 9):
    angles = np.linspace(0, 2 * math.pi, segments, endpoint=False)
    ring = np.stack([np.cos(angles) * 0.5, np.zeros(segments), np.sin(angles) * 0.5], axis=1)
    vertices = np.vstack([ring, [[0.0, 1.0, 0.0]]])
    triangles = [[i, (i + 1) % segments, segments] for i in range(segments)]
    return vertices, np.array(triangles)


def _cylinder(segments: int = 8):
    angles = np.linspace(0, 2 * math.pi, segments, endpoint=False)
    lower = np.stack([np.cos(angles) * 0.5, np.zeros(segments), np.sin(angles) * 0.5], axis=1)
    upper = lower + np.array([0.0, 1.0, 0.0])
    vertices = np.vstack([lower, upper])
    triangles = []
    for i in range(segments):
        j = (i + 1) % segments
        triangles.append([i, j, segments + j])
        triangles.append([i, segments + j, segments + i])
    top = len(vertices)
    vertices = np.vstack([vertices, [[0.0, 1.0, 0.0]]])
    for i in range(segments):
        triangles.append([segments + i, segments + (i + 1) % segments, top])
    return vertices, np.array(triangles)


def _box():
    v = np.array([
        [-0.5, 0.0, -0.5], [0.5, 0.0, -0.5], [0.5, 0.0, 0.5], [-0.5, 0.0, 0.5],
        [-0.5, 1.0, -0.5], [0.5, 1.0, -0.5], [0.5, 1.0, 0.5], [-0.5, 1.0, 0.5],
    ])
    t = np.array([
        [0, 1, 2], [0, 2, 3], [4, 6, 5], [4, 7, 6],
        [0, 4, 5], [0, 5, 1], [1, 5, 6], [1, 6, 2],
        [2, 6, 7], [2, 7, 3], [3, 7, 4], [3, 4, 0],
    ])
    return v, t


def _rock():
    v = np.array([
        [0.0, 1.0, 0.0],
        [0.55, 0.45, 0.15], [0.15, 0.40, 0.55], [-0.5, 0.5, 0.25],
        [-0.3, 0.35, -0.45], [0.3, 0.3, -0.5],
        [0.45, 0.0, 0.1], [0.1, 0.0, 0.5], [-0.45, 0.0, 0.2],
        [-0.3, 0.0, -0.4], [0.25, 0.0, -0.45],
    ])
    upper = [[0, i, 1 + (i % 5)] for i in range(1, 6)]
    upper = [[0, i, 1 + (i % 5)] for i in range(1, 6)]
    side = []
    for i in range(5):
        a, b = 1 + i, 1 + (i + 1) % 5
        c, d = 6 + i, 6 + (i + 1) % 5
        side.append([a, c, d])
        side.append([a, d, b])
    return v, np.array(upper + side)


def _tilt(segments: int = 9):
    """A canvas over hoops: a half cylinder lying along +z, one unit in every dimension."""
    angles = np.linspace(0.0, math.pi, segments)
    arc = np.stack([np.cos(angles) * 0.5, np.sin(angles), np.zeros(segments)], axis=1)
    front = arc + np.array([0.0, 0.0, -0.5])
    back = arc + np.array([0.0, 0.0, 0.5])
    vertices = np.vstack([front, back])

    triangles = []
    for i in range(segments - 1):
        triangles.append([i, segments + i, segments + i + 1])
        triangles.append([i, segments + i + 1, i + 1])
    return vertices, np.array(triangles)


def _quad_cross():
    """Two crossed quads: a tuft of grass seen from any angle."""
    v = np.array([
        [-0.5, 0.0, 0.0], [0.5, 0.0, 0.0], [0.5, 1.0, 0.0], [-0.5, 1.0, 0.0],
        [0.0, 0.0, -0.5], [0.0, 0.0, 0.5], [0.0, 1.0, 0.5], [0.0, 1.0, -0.5],
    ])
    t = np.array([[0, 1, 2], [0, 2, 3], [4, 5, 6], [4, 6, 7]])
    return v, t


def _transform(vertices: np.ndarray, scale: np.ndarray, yaw_deg: float,
               position: np.ndarray) -> np.ndarray:
    angle = math.radians(yaw_deg)
    cos, sin = math.cos(angle), math.sin(angle)
    scaled = vertices * scale
    x = scaled[:, 0] * cos + scaled[:, 2] * sin
    z = -scaled[:, 0] * sin + scaled[:, 2] * cos
    return np.stack([x, scaled[:, 1], z], axis=1) + position


def _add(mesh: Mesh, template, color, scale, yaw, position, material=1.0, normals=None):
    vertices, triangles = template
    placed = _transform(vertices, np.asarray(scale, float), yaw, np.asarray(position, float))
    if normals is not None:
        normals = np.tile(np.asarray(normals, float), (len(placed), 1))
    mesh.add(placed, triangles, color, normals, material=material)


def _outward(template):
    """Reverses winding so the generated normals point out of the solid, not into it."""
    vertices, triangles = template
    return vertices, triangles[:, ::-1].copy()


CONE = _outward(_cone())
CONE_FINE = _outward(_cone(11))
CYLINDER = _outward(_cylinder())
BOX = _box()
ROCK = _outward(_rock())
TUFT = _quad_cross()
TILT = _tilt()


def build_eagle(mesh: Mesh, position, ground_y: float, heading) -> None:
    """The bird at the head of its own trail.

    Drawn from above, which is the only angle this map has, so the silhouette is the
    whole of it: a narrow body, a fanned tail, and two wings swept back from the
    shoulders. The first attempt gave each wing one triangle from the centre outwards
    and the two met into a filled diamond — a paper dart with a white nose. A wing
    needs a root, a leading edge and a trailing edge before it reads as a wing.
    """
    span = EAGLE_SPAN
    yaw = math.degrees(math.atan2(heading[0], heading[1]))
    base = np.array([position[0], ground_y + EAGLE_HEIGHT, position[1]])

    def add(shape, triangles, colour):
        shape = np.asarray(shape, float)
        mesh.add(_transform(shape, np.ones(3), yaw, base), np.asarray(triangles), colour,
                 np.tile([0.0, 1.0, 0.0], (len(shape), 1)), material=1.0)

    half = span * 0.035

    add([[0.0, 0.0, span * 0.30],
         [-half, 0.0, span * 0.12],
         [half, 0.0, span * 0.12],
         [-half, 0.0, -span * 0.16],
         [half, 0.0, -span * 0.16]],
        [[0, 1, 2], [1, 3, 4], [1, 4, 2]], EAGLE_BODY)

    # Fanned tail. A bird from above is mostly wing, and the tail is what says which end
    # you are looking at when the wings are symmetrical.
    add([[-half, 0.0, -span * 0.14],
         [half, 0.0, -span * 0.14],
         [span * 0.09, 0.0, -span * 0.34],
         [-span * 0.09, 0.0, -span * 0.34]],
        [[0, 1, 2], [0, 2, 3]], EAGLE_BODY * 1.15)

    for side in (-1.0, 1.0):
        root_front = span * 0.16
        root_back = -span * 0.10
        tip = side * span * 0.50

        add([[side * half, 0.0, root_front],
             [tip, 0.0, root_front - span * 0.20],
             [tip, 0.0, root_front - span * 0.30],
             [side * half, 0.0, root_back]],
            [[0, 1, 2], [0, 2, 3]], EAGLE_WING)

        # Dark primaries at the tip, which is what stops the wing looking cut from card.
        add([[tip * 0.98, 0.0, root_front - span * 0.20],
             [tip * 1.02, 0.0, root_front - span * 0.26],
             [tip * 0.80, 0.0, root_front - span * 0.31]],
            [[0, 1, 2]], EAGLE_BODY * 0.75)

    add([[0.0, 0.0, span * 0.32],
         [-span * 0.035, 0.0, span * 0.20],
         [span * 0.035, 0.0, span * 0.20]],
        [[0, 1, 2]], EAGLE_HEAD)

def build_prop(mesh: Mesh, prop: A.Prop) -> None:
    """Draws one placed prop as the nearest simple solid at the size it was given."""
    base = np.array([prop.x, prop.ground_y, prop.z])
    size = prop.size

    if prop.kind == "pines":
        trunk = size * 0.22
        _add(mesh, CYLINDER, WOOD, (size * 0.09, trunk, size * 0.09), prop.yaw, base)
        for i, (offset, width, height) in enumerate(
                ((0.15, 0.62, 0.45), (0.42, 0.48, 0.38), (0.66, 0.32, 0.34))):
            _add(mesh, CONE_FINE, PINE_GREEN * (0.85 + 0.1 * i),
                 (size * width, size * height, size * width), prop.yaw,
                 base + np.array([0.0, size * offset, 0.0]))

    elif prop.kind == "trees":
        _add(mesh, CYLINDER, WOOD, (size * 0.11, size * 0.45, size * 0.11), prop.yaw, base)
        _add(mesh, CONE, LEAF_GREEN, (size * 0.85, size * 0.42, size * 0.85), prop.yaw,
             base + np.array([0.0, size * 0.36, 0.0]))
        _add(mesh, CONE, LEAF_GREEN * 0.88, (size * 0.65, size * 0.36, size * 0.65),
             prop.yaw + 40, base + np.array([0.0, size * 0.62, 0.0]))

    elif prop.kind == "dead":
        _add(mesh, CYLINDER, DEAD_WOOD, (size * 0.09, size, size * 0.09), prop.yaw, base)
        for k in range(3):
            _add(mesh, CYLINDER, DEAD_WOOD * 0.92,
                 (size * 0.05, size * 0.30, size * 0.05), prop.yaw + k * 120,
                 base + np.array([size * 0.08, size * (0.55 + 0.12 * k), size * 0.05]))

    elif prop.kind in ("rocks", "shore"):
        height = size if prop.kind == "rocks" else size * 0.5
        color = STONE if prop.kind == "rocks" else STONE * 0.72
        _add(mesh, ROCK, color, (size, height, size), prop.yaw, base)

    elif prop.kind == "mountains":
        _add(mesh, CONE_FINE, MOUNTAIN_STONE, (size * 1.2, size, size * 1.2), prop.yaw, base)

    elif prop.kind == "cover":
        # Foliage cards, lit as though they faced the sky: a grass blade has no side
        # worth shading, and lit by its own geometry half of every tuft goes black.
        _add(mesh, TUFT, GRASS, (size * 1.1, size, size * 1.1), prop.yaw, base,
             normals=(0.0, 1.0, 0.0))

    elif prop.kind == "houses":
        _add(mesh, BOX, PLASTER, (size * 0.9, size * 0.6, size * 0.8), prop.yaw, base)
        _add(mesh, CONE, ROOF, (size * 1.15, size * 0.45, size * 1.05), prop.yaw + 45,
             base + np.array([0.0, size * 0.6, 0.0]))

    elif prop.kind == "farms":
        rows = 5
        for r in range(rows):
            offset = (r / (rows - 1) - 0.5) * size * 0.8
            _add(mesh, BOX, WHEAT * (0.92 + 0.03 * r),
                 (size * 0.12, size * 0.14, size * 0.9), prop.yaw,
                 base + _transform(np.array([[offset, 0.0, 0.0]]), np.ones(3),
                                   prop.yaw, np.zeros(3))[0])

    elif prop.kind == "towers":
        _add(mesh, BOX, PLASTER * 0.85, (size * 0.35, size * 0.85, size * 0.35),
             prop.yaw, base)
        _add(mesh, CONE, ROOF, (size * 0.5, size * 0.25, size * 0.5), prop.yaw,
             base + np.array([0.0, size * 0.85, 0.0]))

    elif prop.kind == "timber":
        for k in range(3):
            _add(mesh, CYLINDER, WOOD_PALE, (size * 0.28, size * 0.9, size * 0.28),
                 prop.yaw + 90, base + np.array([0.0, size * 0.22 * k, size * 0.1 * k]))

    elif prop.kind == "ruins":
        # An abandoned cart: the same kind of cart the player is escorting.
        _add(mesh, BOX, WOOD, (size * 0.55, size * 0.28, size * 0.9), prop.yaw,
             base + np.array([0.0, size * 0.16, 0.0]))
        for dx, dz in ((-0.3, -0.35), (0.3, -0.35), (-0.3, 0.35)):
            _add(mesh, CYLINDER, WOOD * 0.7, (size * 0.28, size * 0.06, size * 0.28),
                 prop.yaw + 90, base + np.array([dx * size, size * 0.12, dz * size]))


def build_wagon(mesh: Mesh, position, ground_y: float, heading, kind: int) -> None:
    """A bed, a canvas over hoops and four wheels — the shape Tools/wagon.py builds.

    The treasure wagon is a different colour on purpose: the player is meant to see at a
    glance which cart holds the loot, because damage to that one costs them the reward.
    """
    yaw = math.degrees(math.atan2(heading[0], heading[1]))
    base = np.array([position[0], ground_y, position[1]])
    color = WAGON_COLORS[kind]
    canvas = color if kind == A.TREASURE else CANVAS

    _add(mesh, BOX, WOOD, (1.7, 0.34, 3.2), yaw, base + np.array([0.0, 0.86, 0.0]))
    _add(mesh, BOX, WOOD * 1.15, (1.85, 0.12, 3.3), yaw, base + np.array([0.0, 1.14, 0.0]))
    _add(mesh, TILT, canvas, (1.85, 1.15, 3.0), yaw, base + np.array([0.0, 1.2, 0.0]))
    # The draught pole, so the cart has a front.
    _add(mesh, BOX, WOOD, (0.14, 0.14, 1.6), yaw,
         base + _transform(np.array([[0.0, 0.0, 2.2]]), np.ones(3), yaw, np.zeros(3))[0]
         + np.array([0.0, 0.7, 0.0]))

    for dx, dz, radius in ((-0.95, -1.15, 0.42), (0.95, -1.15, 0.42),
                           (-0.95, 1.15, 0.60), (0.95, 1.15, 0.60)):
        offset = _transform(np.array([[dx, 0.0, dz]]), np.ones(3), yaw, np.zeros(3))[0]
        _add(mesh, CYLINDER, np.array([0.26, 0.18, 0.12]),
             (radius * 2, 0.18, radius * 2), yaw + 90,
             base + offset + np.array([0.0, radius, 0.0]))


def build_figure(mesh: Mesh, position, ground_y: float, body: np.ndarray,
                 head: np.ndarray, height: float, yaw: float) -> None:
    """A troop or an enemy, at the height VisualLibrary gives the real model."""
    base = np.array([position[0], ground_y, position[1]])
    _add(mesh, CYLINDER, body, (height * 0.30, height * 0.75, height * 0.22), yaw, base)
    _add(mesh, CYLINDER, head, (height * 0.20, height * 0.25, height * 0.20), yaw,
         base + np.array([0.0, height * 0.75, 0.0]))


def build_wolf(mesh: Mesh, position, ground_y: float, height: float, yaw: float) -> None:
    base = np.array([position[0], ground_y, position[1]])
    _add(mesh, BOX, WOLF_GREY, (height * 0.45, height * 0.55, height * 1.3), yaw,
         base + np.array([0.0, height * 0.4, 0.0]))
    for dx, dz in ((-0.15, -0.45), (0.15, -0.45), (-0.15, 0.45), (0.15, 0.45)):
        offset = _transform(np.array([[dx * height, 0.0, dz * height]]), np.ones(3),
                            yaw, np.zeros(3))[0]
        _add(mesh, CYLINDER, WOLF_GREY * 0.8, (height * 0.12, height * 0.42, height * 0.12),
             yaw, base + offset)


# --- Route ribbons ---------------------------------------------------------------

def ribbon(grid: A.TileGrid, tiles: Sequence[int], height_scale: float,
           width: float = ROUTE_WIDTH):
    """The corridor drawn as a ribbon over the ground, mitred through its corners."""
    centres = []
    for tile in tiles:
        x, z = A.tile_centre(grid, tile)
        y = grid.surface_elevation(x, z) * height_scale + ROUTE_LIFT
        centres.append((x, y, z))

    count = len(centres)
    if count < 2:
        return None

    vertices = np.zeros((count * 2, 3))
    for i in range(count):
        previous = centres[max(i - 1, 0)]
        following = centres[min(i + 1, count - 1)]
        fx, fz = following[0] - previous[0], following[2] - previous[2]
        length = math.hypot(fx, fz)
        if length < 0.01:
            fx, fz, length = 0.0, 1.0, 1.0
        fx, fz = fx / length, fz / length
        side = np.array([fz, 0.0, -fx]) * (width * 0.5)

        centre = np.array(centres[i])
        vertices[i * 2 + 0] = centre - side
        vertices[i * 2 + 1] = centre + side

    triangles = []
    for i in range(count - 1):
        v = i * 2
        triangles.append([v + 0, v + 2, v + 1])
        triangles.append([v + 1, v + 2, v + 3])

    return vertices, np.array(triangles)


# --- The two views ---------------------------------------------------------------

def _scene_bounds(level: A.LevelMap, height_scale: float):
    extent = level.grid.width * A.TILE_SIZE
    centre = np.array([extent * 0.5, height_scale * 0.5, extent * 0.5])
    return centre, extent * 0.75


ENEMY_MARKER = np.array([0.86, 0.18, 0.16])

EAGLE_BODY = np.array([0.26, 0.19, 0.13])
EAGLE_WING = np.array([0.38, 0.29, 0.19])
EAGLE_HEAD = np.array([0.90, 0.88, 0.82])

# Metres across. Twenty-one was a hang-glider: wider than the eight-metre spruces it
# flew over, which made the map look small rather than the bird look grand. A real eagle
# spans two, and two is eleven pixels from seventy metres up — nothing. Ten is where it
# sits now: under the canopy it flies over, which is the proportion that was wrong, and
# still a bird rather than a smudge. The pin does the finding, so the bird does not have
# to be big enough to find itself.
EAGLE_SPAN = 10.0

# Metres above the ground it flies. Clear of the canopy so it never vanishes into a
# treetop, low enough that the shadow it throws stays beside it rather than reading as
# a second bird.
EAGLE_HEIGHT = 9.0

# The ring around the bird, drawn in screen space after the world is shaded rather than
# as a disc on the ground. On the ground the canopy ate it — a white crescent behind a
# spruce, which reads as a lighting mistake and not as a marker. In screen space it is
# what it actually is: a pin on a map, at the same size whatever it flies over.
EAGLE_PIN = np.array([0.98, 0.96, 0.88])
EAGLE_PIN_SHADE = np.array([0.10, 0.09, 0.07])
EAGLE_PIN_RADIUS = 0.023   # of image width
EAGLE_PIN_WIDTH = 0.0013   # of image width, so supersampling thins nothing

# The overlay. Not a fog that hides the country — the terrain is what the player reads
# to plan, and hiding it would remove the decision rather than the certainty. It takes
# the colour out and leaves the shape, so unflown ground says "you have not looked here"
# while staying legible.
#
# The mix is how much of that colour goes. At 0.72 the grey was a haze and the eagle's
# trail barely stood out from the country around it; the ability has to be visibly worth
# its gold. Full grey was tried and costs too much: at 0.94 and above the river stops
# being blue outside the trail, and §3.3 asks that water and its crossings be legible
# *before* the route is drawn — that is the whole reason the overlay is see-through.
# 0.88 is the last stop where the water still reads.
OVERLAY_GREY = np.array([0.46, 0.47, 0.45])
OVERLAY_MIX = 0.88
OVERLAY_DARKEN = 0.88


def _apply_overlay(image: np.ndarray, frame: Frame, level: A.LevelMap,
                   revealed: set) -> np.ndarray:
    """Greys out every pixel standing on a tile the eagle never flew over."""
    grid = level.grid
    tx = np.clip((frame.world[..., 0] / A.TILE_SIZE).astype(int), 0, grid.width - 1)
    tz = np.clip((frame.world[..., 2] / A.TILE_SIZE).astype(int), 0, grid.height - 1)

    seen = np.zeros(grid.tile_count, bool)
    if revealed:
        seen[np.fromiter(revealed, int, len(revealed))] = True

    lifted = seen[tz * grid.width + tx].astype(float)

    # Feather the edge. A four-metre tile is eleven pixels at map scale, so a hard mask
    # draws the eagle's trail as a staircase of squares — which says "grid" where it
    # should say "this is as far as the bird could see".
    radius = max(2, int(image.shape[1] * 0.012))
    lifted = _box_blur(lifted, radius)

    luminance = image @ np.array([0.299, 0.587, 0.114])
    muted = (luminance[..., None] * OVERLAY_GREY / OVERLAY_GREY.mean()) * OVERLAY_DARKEN
    blended = image * (1.0 - OVERLAY_MIX) + muted * OVERLAY_MIX

    alpha = (lifted * frame.covered)[..., None]
    return image * alpha + blended * (1.0 - alpha)


# --- Map markers ------------------------------------------------------------------
#
# Everything here is drawn in screen space, over the finished picture, and that is the
# whole point of the section. The planning map is a map: the things a player plans
# against have to keep their size and their place whether they sit on sunlit meadow or
# under spruce, which is exactly what an object in the world cannot do.
#
# It was not always drawn this way and the map was unusable for it. Start and goal were
# one four-metre tile each, tinted on the ground — eleven pixels from seventy metres up,
# and the goal on 1-5 was hidden under a mountain. Fords were terrain colour, so the
# crossings docs/GDD.md §3.3 calls the map's most important information were a slightly
# paler shade of river. A player was being asked to draw a line to a goal they could not
# see, over crossings they could not find.

START_MARKER = np.array([0.42, 0.78, 0.36])
GOAL_MARKER = np.array([0.98, 0.80, 0.22])
FORD_MARKER = np.array([0.76, 0.62, 0.42])
MARKER_EDGE = np.array([0.10, 0.09, 0.07])

# All as a share of image width, so supersampling changes nothing.
ENDPOINT_RADIUS = 0.011
MARKER_EDGE_WIDTH = 0.0026
FORD_THICKNESS = 0.008


def _stamp(image: np.ndarray, distance: np.ndarray, box, fill, edge=MARKER_EDGE,
           edge_width: float = None, fill_alpha: float = 1.0,
           edge_alpha: float = 0.7) -> None:
    """Composites one antialiased shape from its signed distance field, edge outward.

    Every marker is two colours, and not for decoration: a pale mark vanishes on a
    sunlit meadow and a dark one under spruce, so each carries its own contrast with it.
    """
    x0, y0, x1, y1 = box
    if x0 >= x1 or y0 >= y1:
        return

    width = image.shape[1]
    rim = (MARKER_EDGE_WIDTH if edge_width is None else edge_width) * width

    inside = np.clip(0.5 - distance, 0.0, 1.0)
    outline = np.clip(0.5 - (distance - rim), 0.0, 1.0) - inside

    patch = image[y0:y1, x0:x1]
    a = (outline * edge_alpha)[..., None]
    patch = patch * (1.0 - a) + edge * a
    a = (inside * fill_alpha)[..., None]
    patch = patch * (1.0 - a) + fill * a
    image[y0:y1, x0:x1] = patch


def _box_around(image: np.ndarray, points: np.ndarray, margin: float):
    """Pixel bounds enclosing the points plus a margin, clipped to the image."""
    height, width = image.shape[:2]
    low = points.min(axis=0) - margin
    high = points.max(axis=0) + margin
    return (max(int(low[0]), 0), max(int(low[1]), 0),
            min(int(high[0]) + 1, width), min(int(high[1]) + 1, height))


def _grid_of(box):
    x0, y0, x1, y1 = box
    ys, xs = np.mgrid[y0:y1, x0:x1]
    return xs, ys


def _draw_disc(image, camera, position, radius_share, fill) -> None:
    screen = camera.to_screen(camera.to_view(position[None, :]))
    radius = radius_share * image.shape[1]
    box = _box_around(image, screen, radius + MARKER_EDGE_WIDTH * image.shape[1] + 2)
    xs, ys = _grid_of(box)
    _stamp(image, np.hypot(xs - screen[0, 0], ys - screen[0, 1]) - radius, box, fill)


def _draw_ring(image, camera, position, radius_share, width_share, fill,
               edge_alpha=0.55) -> None:
    screen = camera.to_screen(camera.to_view(position[None, :]))
    radius = radius_share * image.shape[1]
    half = width_share * image.shape[1] * 0.5
    box = _box_around(image, screen, radius + half + width_share * image.shape[1] + 2)
    xs, ys = _grid_of(box)
    distance = np.abs(np.hypot(xs - screen[0, 0], ys - screen[0, 1]) - radius) - half
    _stamp(image, distance, box, fill, edge_width=width_share, edge_alpha=edge_alpha)


def _draw_capsule(image, camera, a, b, thickness_share, fill) -> None:
    """A bar with rounded ends between two world points — the bridge over a ford."""
    screen = camera.to_screen(camera.to_view(np.stack([a, b])))
    half = thickness_share * image.shape[1] * 0.5
    box = _box_around(image, screen, half + MARKER_EDGE_WIDTH * image.shape[1] + 2)
    xs, ys = _grid_of(box)

    ax, ay = screen[0]
    bx, by = screen[1]
    dx, dy = bx - ax, by - ay
    length_squared = dx * dx + dy * dy

    if length_squared < 1e-6:
        distance = np.hypot(xs - ax, ys - ay)
    else:
        t = np.clip(((xs - ax) * dx + (ys - ay) * dy) / length_squared, 0.0, 1.0)
        distance = np.hypot(xs - (ax + t * dx), ys - (ay + t * dy))

    _stamp(image, distance - half, box, fill)


def _ford_crossings(grid: A.TileGrid):
    """Ford tiles gathered into crossings, and which way each one runs.

    A ford is set on a river tile and its horizontal neighbours, so the run of tiles is
    the direction a caravan crosses in — which is the direction the bridge is drawn.
    """
    seen = set()
    crossings = []

    for tile in range(grid.tile_count):
        if int(grid.tiles[tile]) != A.FORD or tile in seen:
            continue

        cluster, stack = [], [tile]
        seen.add(tile)

        while stack:
            current = stack.pop()
            cluster.append(current)
            cx, cy = grid.to_coords(current)
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = cx + dx, cy + dy
                if not grid.in_bounds(nx, ny):
                    continue
                neighbour = grid.to_index(nx, ny)
                if neighbour in seen or int(grid.tiles[neighbour]) != A.FORD:
                    continue
                seen.add(neighbour)
                stack.append(neighbour)

        coords = [grid.to_coords(t) for t in cluster]
        xs = [c[0] for c in coords]
        ys = [c[1] for c in coords]

        # The long axis of the cluster is the way across.
        if max(xs) - min(xs) >= max(ys) - min(ys):
            ends = ((min(xs), sum(ys) / len(ys)), (max(xs), sum(ys) / len(ys)))
        else:
            ends = ((sum(xs) / len(xs), min(ys)), (sum(xs) / len(xs), max(ys)))
        crossings.append(ends)

    return crossings


def draw_map_markers(image: np.ndarray, camera: Camera, level: A.LevelMap,
                     height_scale: float) -> np.ndarray:
    """Start, goal and every crossing of the river, none of which the canopy may hide.

    Fords are drawn whether or not the eagle has flown. They are terrain, not something
    the scouting hides — and §3.3 asks that the player see where the river can be
    crossed *before* the line is drawn, because that is where the decision is.
    """
    grid = level.grid

    def at(tile: int, lift: float) -> np.ndarray:
        x, z = A.tile_centre(grid, tile)
        return np.array([x, grid.surface_elevation(x, z) * height_scale + lift, z])

    for (ax, ay), (bx, by) in _ford_crossings(grid):
        a = np.array([(ax + 0.5) * A.TILE_SIZE, 0.0, (ay + 0.5) * A.TILE_SIZE])
        b = np.array([(bx + 0.5) * A.TILE_SIZE, 0.0, (by + 0.5) * A.TILE_SIZE])
        a[1] = grid.surface_elevation(a[0], a[2]) * height_scale
        b[1] = grid.surface_elevation(b[0], b[2]) * height_scale

        # Extended a little past the water so the bridge lands on both banks, the way
        # a built thing does. A mark that stops at the waterline reads as shallow
        # water, which is the exact reading §3.3 says to avoid.
        along = b - a
        a = a - along * 0.35
        b = b + along * 0.35
        _draw_capsule(image, camera, a, b, FORD_THICKNESS, FORD_MARKER)

    _draw_disc(image, camera, at(level.start_index, 1.0), ENDPOINT_RADIUS, START_MARKER)
    _draw_disc(image, camera, at(level.goal_index, 1.0), ENDPOINT_RADIUS, GOAL_MARKER)
    _draw_ring(image, camera, at(level.goal_index, 1.0),
               ENDPOINT_RADIUS * 1.9, MARKER_EDGE_WIDTH, GOAL_MARKER, edge_alpha=0.45)
    return image


def _draw_pin(image: np.ndarray, camera: Camera, position: np.ndarray) -> np.ndarray:
    """The ring around the eagle. Same reasoning as the markers above."""
    _draw_ring(image, camera, position, EAGLE_PIN_RADIUS, EAGLE_PIN_WIDTH, EAGLE_PIN)
    return image


def _box_blur(mask: np.ndarray, radius: int) -> np.ndarray:
    """Two box passes — cheap, and close enough to a gaussian for a soft edge."""
    for _ in range(2):
        padded = np.pad(mask, radius, mode="edge")
        cumulative = np.cumsum(np.cumsum(padded, axis=0), axis=1)
        cumulative = np.pad(cumulative, ((1, 0), (1, 0)))

        size = 2 * radius + 1
        h, w = mask.shape
        total = (cumulative[size:size + h, size:size + w]
                 - cumulative[0:h, size:size + w]
                 - cumulative[size:size + h, 0:w]
                 + cumulative[0:h, 0:w])
        mask = total / (size * size)
    return np.clip(mask, 0.0, 1.0)


def planning_keep_clear(level: A.LevelMap, draw_routes: bool):
    """Tiles the decorator leaves bare on the planning map, which is none of them.

    Clearing existed for one reason — a ribbon under a spruce is not a ribbon — and
    that reason is gone: the route is composited over the finished picture now, so the
    canopy cannot cover it whatever grows there.

    What the clearing did instead was leak. Cleared ground read as lanes through the
    forest at a third of the surrounding prop density, wherever the three corridors ran,
    and the planning overlay cannot hide that because it takes out colour and not
    geometry. Hiding the ribbons hid nothing.

    So: nothing is cleared, even with the corridors drawn, and the planning map is
    dressed exactly as `LevelRunner` dresses the run. The argument is kept as a
    parameter because the question — *should* the ribbons buy themselves clear ground —
    is the one this function exists to answer, and the answer is no.
    """
    return None


def render_plan(level: A.LevelMap, width: int = 1400, height: int = 1400,
                height_scale: float = 22.0, density_scale: float = 2.2,
                max_props: int = 2600, eagle=None, draw_routes: bool = True) -> Image.Image:
    """The planning map: straight down, orthographic, under the scouting overlay.

    With `eagle`, the map is greyed out except along the flight, and the groups the bird
    passed over are marked. Without it everything is grey — which is what a player sees
    on a level they did not spend the ability on.
    """
    grid = level.grid
    extent = grid.width * A.TILE_SIZE

    keep_clear = planning_keep_clear(level, draw_routes)
    bird = None

    mesh = Mesh()
    build_terrain(mesh, level, height_scale, as_ground=True, mark_endpoints=True)
    props = A.decorate(grid, level.seed, keep_clear=keep_clear, height_scale=height_scale,
                       max_props=max_props, density_scale=density_scale,
                       sites=A.ruin_sites(level))
    for prop in props:
        build_prop(mesh, prop)

    # What the eagle found, drawn as markers rather than as models. At seventy metres up
    # a wolf is four pixels; a pin is the honest way to say "something is here", and it
    # says nothing about how many or how strong — which is the rule the design puts on
    # bought information.
    if eagle is not None:
        for index in eagle.revealed_enemies:
            spawn = level.encounters.enemies[index]
            x, z = A.tile_centre(grid, spawn.tile)
            y = grid.surface_elevation(x, z) * height_scale
            _add(mesh, CONE, ENEMY_MARKER, (5.0, -7.0, 5.0), 0.0,
                 np.array([x, y + 9.0, z]), normals=(0.0, 1.0, 0.0))

        if len(eagle.path) >= 2:
            head = eagle.path[-1]
            behind = eagle.path[max(len(eagle.path) - 6, 0)]
            heading = (head[0] - behind[0], head[1] - behind[1])
            length = math.hypot(*heading) or 1.0
            ground = grid.surface_elevation(head[0], head[1]) * height_scale
            build_eagle(mesh, head, ground, (heading[0] / length, heading[1] / length))
            bird = np.array([head[0], ground + EAGLE_HEIGHT, head[1]])

    vertices, triangles, colors, normals, material = mesh.finish()

    camera = Camera.orthographic_view(
        np.array([extent * 0.5, 70.0 + height_scale, extent * 0.5]),
        np.array([0.0, -1.0, 0.0]), extent * 0.5 * 1.02, width, height)

    frame = Frame(width, height)
    rasterise(frame, camera, vertices, triangles, colors, normals, material)

    sun = euler_forward(58.0, -40.0)
    shadow = cast_shadows(_scene_bounds(level, height_scale), sun, vertices, triangles)(
        frame.world)

    image = shade(frame, sun, np.array([1.0, 0.97, 0.91]), 1.05, shadow, 0.55,
                  PLAN_BACKGROUND, None, camera)

    if eagle is not None:
        image = _apply_overlay(image, frame, level, eagle.revealed_tiles)
    else:
        image = _apply_overlay(image, frame, level, set())

    if draw_routes:
        image = _draw_routes(image, level, camera, height_scale, frame)

    # Markers last, so nothing in the world can cover them. The eagle's ring goes on
    # top of the rest: it is the one marker that moves, and a moving marker under a
    # fixed one reads as a glitch.
    image = draw_map_markers(image, camera, level, height_scale)
    if bird is not None:
        image = _draw_pin(image, camera, bird)
    return _to_image(image)


def _draw_routes(image: np.ndarray, level: A.LevelMap, camera: Camera,
                 height_scale: float, frame: Frame) -> np.ndarray:
    """Ribbons over the shaded ground, worst alternative first so the fast route stays on top."""
    order = [(A.ODD, ROUTE_ODD), (A.SAFE, ROUTE_SAFE), (A.FAST, ROUTE_FAST)]

    for kind, color in order:
        corridor = level.corridor_of(kind)
        if corridor is None:
            continue
        built = ribbon(level.grid, corridor.tiles, height_scale)
        if built is None:
            continue

        vertices, triangles = built
        overlay = Frame(camera.width, camera.height)
        rasterise(overlay, camera, vertices, triangles,
                  np.tile(color, (len(vertices), 1)),
                  np.tile([0.0, 1.0, 0.0], (len(vertices), 1)),
                  np.ones(len(vertices)))

        # No depth test. The ribbon is built on the ground surface, so it lands in the
        # right place — but the line is the player's own, not a thing in the world, and
        # a map that hides it behind a spruce is hiding the one mark on it they made.
        # Tested against the depth buffer it vanished under the canopy in three places
        # on 1-5 and never visually reached the goal at all: the last stretch ran
        # behind a mountain.
        mask = overlay.covered
        image[mask] = image[mask] * (1.0 - ROUTE_OPACITY) + color * ROUTE_OPACITY

    return image


def render_play(level: A.LevelMap, corridor: A.Corridor, progress: float = 0.45,
                width: int = 1400, height: int = 1000, height_scale: float = 14.0,
                max_props: int = 2200, follow_distance: float = 46.0,
                follow_height: float = 32.0, fov: float = 50.0, faceted: bool = False):
    """The play view: behind and above the column, at the distance models actually read.

    The three camera numbers are the ones on `LevelRunner`, and they are worth trying
    from here before touching the scene: distance and height together decide how far
    the camera looks down, and that angle decides whether the world reads as landscape
    or as a map with trees on it.
    """
    grid = level.grid

    caravan = A.Caravan(grid, corridor.tiles)
    caravan.advance_to(progress)
    lead = caravan.lead_position
    heading = caravan.heading

    mesh = Mesh()
    build_terrain(mesh, level, height_scale, as_ground=True, mark_endpoints=False,
                  faceted=faceted)

    for prop in A.decorate(grid, level.seed, keep_clear=None, height_scale=height_scale,
                           max_props=max_props):
        build_prop(mesh, prop)

    ground = lambda p: grid.surface_elevation(p[0], p[1]) * height_scale

    for index, kind in enumerate(A.WAGON_ORDER):
        position = caravan.wagon_position(index)
        build_wagon(mesh, position, ground(position), heading, kind)

    facing = math.degrees(math.atan2(heading[0], heading[1]))
    for slot, (troop, position) in caravan.formation_positions().items():
        build_figure(mesh, position, ground(position), TROOP_TABARD, TROOP_STEEL,
                     1.85, facing)

    # Everything the generator put near this route, drawn where it stands. Groups are
    # placed across the whole crossable band now rather than along three corridors, so
    # the filter is distance from the caravan rather than which corridor they belong to.
    # The game reveals them only once detection finds them; this is the level, not a
    # frame of a run, so what is here is what is there.
    for spawn in level.encounters.enemies:
        position = A.tile_centre(grid, spawn.tile)
        if math.dist(position, lead) > 220:
            continue
        if spawn.kind == A.WOLF:
            build_wolf(mesh, position, ground(position), 0.95, facing + 180)
        else:
            build_figure(mesh, position, ground(position), ENEMY_RED, ENEMY_RED * 0.8,
                         1.8, facing + 180)

    for cache in level.encounters.caches:
        position = A.tile_centre(grid, cache.tile)
        if math.dist(position, lead) > 220:
            continue
        _add(mesh, BOX, CACHE_GOLD, (1.4, 1.0, 1.0), facing,
             np.array([position[0], ground(position), position[1]]))

    vertices, triangles, colors, normals, material = mesh.finish()

    target = np.array([lead[0], ground(lead), lead[1]])
    position = target + np.array([-heading[0] * follow_distance, follow_height,
                                 -heading[1] * follow_distance])
    camera = Camera.perspective(position, target + np.array([0.0, 4.0, 0.0]), fov,
                                width, height)

    frame = Frame(width, height)
    rasterise(frame, camera, vertices, triangles, colors, normals, material)

    sun = euler_forward(38.0, -52.0)
    shadow = cast_shadows(_scene_bounds(level, height_scale), sun, vertices, triangles)(
        frame.world)

    image = shade(frame, sun, np.array([1.0, 0.96, 0.88]), 1.0, shadow, 0.7,
                  SKY_COLOR, (70.0, 320.0), camera)
    return _to_image(image), caravan


def _to_image(buffer: np.ndarray) -> Image.Image:
    """Straight to eight bits, with no transfer curve applied.

    The project renders in Gamma colour space — ProjectSettings has m_ActiveColorSpace: 0
    — so what a shader writes is what the framebuffer holds. Converting linear to sRGB
    here would wash every colour out by exactly the amount Unity does not.
    """
    return Image.fromarray((np.clip(buffer, 0.0, 1.0) * 255).astype(np.uint8))


# --- Command line ----------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--chapter", type=int, default=1)
    parser.add_argument("--level", type=int, default=1)
    parser.add_argument("--out", default="screens")
    parser.add_argument("--view", choices=("plan", "play", "both"), default="both")
    parser.add_argument("--route", choices=("fast", "safe", "odd"), default="fast")
    parser.add_argument("--progress", type=float, default=0.45,
                        help="How far along the route the caravan has come, 0 to 1.")
    parser.add_argument("--width", type=int, default=1400)
    parser.add_argument("--follow-distance", type=float, default=46.0,
                        help="Metres behind the column, as on LevelRunner.")
    parser.add_argument("--follow-height", type=float, default=32.0,
                        help="Metres above it. With distance, this sets the look-down angle.")
    parser.add_argument("--fov", type=float, default=50.0,
                        help="Vertical field of view. Lower is a longer lens.")
    parser.add_argument("--eagle", action="store_true",
                        help="Fly the scouting ability and lift the overlay along its trail.")
    parser.add_argument("--no-overlay", action="store_true",
                        help="Draw the map with no scouting overlay at all.")
    parser.add_argument("--faceted", action="store_true",
                        help="Flat-shade the ground, one normal per triangle.")
    parser.add_argument("--height-scale", type=float, default=14.0,
                        help="Metres between the lowest and highest ground in the play view.")
    parser.add_argument("--suffix", default="",
                        help="Appended to the play view's filename, for comparisons.")
    parser.add_argument("--supersample", type=int, default=2,
                        help="Render at this multiple and scale down, for clean edges.")
    args = parser.parse_args()

    os.makedirs(args.out, exist_ok=True)
    seed = A.DeterministicRandom.seed_for(args.chapter, args.level)
    level = A.generate(A.LevelRecipe(), seed)

    print(f"[Arna] {args.chapter}-{args.level} (seed {seed}): "
          f"fastest {level.fastest_route_cost:.1f}, "
          f"overlap {A.worst_overlap(level.corridors):.0%}, "
          f"{len(level.encounters.enemies)} enemy groups, "
          f"{len(level.encounters.traps)} traps, attempt {level.attempts}")

    scale = max(1, args.supersample)

    if args.view in ("plan", "both"):
        size = args.width * scale

        flight = None
        if args.eagle:
            flight = A.fly_the_eagle(level)
            covered = 100.0 * flight.coverage / level.grid.tile_count
            print(f"[Arna] eagle: {flight.seconds:.0f} s aloft, {covered:.0f}% of the map, "
                  f"{len(flight.revealed_enemies)} of {len(level.encounters.enemies)} groups found")
        elif args.no_overlay:
            flight = A.ScoutFlight([], set(range(level.grid.tile_count)), [], 0.0)

        # The corridors are the generator's own measurement and the player never sees
        # them. Under the scouting overlay they would be a third answer sheet laid over
        # the two the player is allowed.
        image = render_plan(level, size, size, eagle=flight,
                            draw_routes=not (args.eagle or args.no_overlay))
        if scale > 1:
            image = image.resize((args.width, args.width), Image.LANCZOS)
        path = os.path.join(args.out, f"plan-{args.chapter}-{args.level}.png")
        image.save(path)
        print(f"[Arna] wrote {path}")

    if args.view in ("play", "both"):
        kind = {"fast": A.FAST, "safe": A.SAFE, "odd": A.ODD}[args.route]
        corridor = level.corridor_of(kind) or level.corridors[0]
        width = args.width * scale
        height = int(width * (1000 / 1400))
        image, caravan = render_play(level, corridor, args.progress, width, height,
                                     height_scale=args.height_scale,
                                     follow_distance=args.follow_distance,
                                     follow_height=args.follow_height, fov=args.fov,
                                     faceted=args.faceted)
        if scale > 1:
            image = image.resize((args.width, int(args.width * (1000 / 1400))), Image.LANCZOS)
        path = os.path.join(args.out,
                            f"play-{args.chapter}-{args.level}-{args.route}{args.suffix}.png")
        image.save(path)
        angle = math.degrees(math.atan2(args.follow_height, args.follow_distance))
        print(f"[Arna] wrote {path} — {caravan.progress:.0%} along the {args.route} route, "
              f"in {A.TERRAIN_NAMES[caravan.current_terrain]}, "
              f"camera {angle:.0f}° down at {args.fov:.0f}° fov")


if __name__ == "__main__":
    main()
