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


def render_plan(level: A.LevelMap, width: int = 1400, height: int = 1400,
                height_scale: float = 22.0, density_scale: float = 2.2,
                max_props: int = 2600) -> Image.Image:
    """The planning map: straight down, orthographic, the three corridors drawn over it."""
    grid = level.grid
    extent = grid.width * A.TILE_SIZE

    keep_clear = {tile for corridor in level.corridors for tile in corridor.tiles}

    mesh = Mesh()
    build_terrain(mesh, level, height_scale, as_ground=True, mark_endpoints=True)
    props = A.decorate(grid, level.seed, keep_clear=keep_clear, height_scale=height_scale,
                       max_props=max_props, density_scale=density_scale,
                       sites=A.ruin_sites(level))
    for prop in props:
        build_prop(mesh, prop)

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

    image = _draw_routes(image, level, camera, height_scale, frame)
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

        mask = overlay.covered & (overlay.depth <= frame.depth + 1.0)
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

    # Everything the generator put on this corridor, drawn where it stands. The game
    # reveals enemies only once detection finds them; this is the level, not a frame of
    # a run, so what is here is what is there.
    for spawn in level.encounters.enemies:
        if spawn.corridor != corridor.kind:
            continue
        position = A.tile_centre(grid, spawn.tile)
        if math.dist(position, lead) > 220:
            continue
        if spawn.kind == A.WOLF:
            build_wolf(mesh, position, ground(position), 0.95, facing + 180)
        else:
            build_figure(mesh, position, ground(position), ENEMY_RED, ENEMY_RED * 0.8,
                         1.8, facing + 180)

    for cache in level.encounters.caches:
        if cache.corridor != corridor.kind:
            continue
        position = A.tile_centre(grid, cache.tile)
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
        image = render_plan(level, size, size)
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
