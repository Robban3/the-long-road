"""A port of Arna.Sim and Arna.Gen to Python, so a level can be generated without Unity.

The engine is a Windows GUI binary and the capture methods in Arna.Editor.ArnaSetup
need it. That makes every picture of the game — the planning map above all — something
only one machine can produce. The generator itself needs none of Unity: it is plain
arithmetic over a seed, and this file is that arithmetic, transcribed class for class
from Assets/_Project/Scripts so the two can be compared line by line.

What it gives back is the same LevelMap the engine builds: terrain, elevation, the
three corridors, and every enemy, trap and silver cache the placer put on them.
Tools/render_screens.py draws it.

Two things needed care, and both are load-bearing rather than pedantry:

  * The pathfinder accumulates in single precision, as C# float does. Python floats
    are doubles, and the difference is not academic here: the cautious route is found
    over a grid thick with equal-cost tiles, so a rounding difference of one part in
    ten million decides which of two identical-cost paths A* keeps. In double
    precision level 1-5 came out with 67 % corridor overlap; rounded to single, 59 %
    and a fastest route of 94.4 — the figures recorded in docs/status.md, which is the
    external evidence that this file lands on the same maps the engine builds.

  * Scenery models are counted, not loaded. The decorator's random stream advances by
    one draw per model choice, so the count of each prop set has to match the pack
    exactly or every prop after the first lands somewhere else. The counts here come
    from LoadForestDecor in ArnaSetup.cs.
"""

from __future__ import annotations

import math
import struct
from dataclasses import dataclass, field
from typing import List, Optional, Sequence

import numpy as np


# --- DeterministicRandom ---------------------------------------------------------

MASK32 = 0xFFFFFFFF
MASK64 = 0xFFFFFFFFFFFFFFFF


class DeterministicRandom:
    """Deterministic xorshift128, seeded through a SplitMix64 avalanche."""

    __slots__ = ("_x", "_y", "_z", "_w")

    def __init__(self, seed: int):
        s = seed & MASK32
        state = [0, 0, 0, 0]
        for i in range(4):
            s = (s + 0x9E3779B97F4A7C15) & MASK64
            z = s
            z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
            z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK64
            state[i] = ((z ^ (z >> 31)) >> 32) & MASK32

        self._x, self._y, self._z, self._w = state
        if (self._x | self._y | self._z | self._w) == 0:
            self._w = 0x6D2B79F5

    def next_uint(self) -> int:
        t = (self._x ^ ((self._x << 11) & MASK32)) & MASK32
        self._x = self._y
        self._y = self._z
        self._z = self._w
        self._w = (self._w ^ (self._w >> 19) ^ t ^ (t >> 8)) & MASK32
        return self._w

    def range_int(self, min_inclusive: int, max_exclusive: int) -> int:
        if max_exclusive <= min_inclusive:
            return min_inclusive
        span = max_exclusive - min_inclusive
        threshold = 0x100000000 % span
        while True:
            r = self.next_uint()
            if r >= threshold:
                return min_inclusive + (r % span)

    def value01(self) -> float:
        return (self.next_uint() >> 8) * (1.0 / 16777216.0)

    def range_float(self, min_inclusive: float, max_exclusive: float) -> float:
        return min_inclusive + self.value01() * (max_exclusive - min_inclusive)

    def chance(self, probability: float) -> bool:
        return self.value01() < probability

    def shuffle(self, items: List) -> None:
        for i in range(len(items) - 1, 0, -1):
            j = self.range_int(0, i + 1)
            items[i], items[j] = items[j], items[i]

    @staticmethod
    def seed_for(chapter: int, level: int) -> int:
        return chapter * 1000 + level


# --- ValueNoise ------------------------------------------------------------------


def _hash01(x: np.ndarray, y: np.ndarray, seed: int) -> np.ndarray:
    """Deterministic hash of a lattice point to [0,1), vectorised over a grid.

    Every term is masked back to 32 bits as it is formed. Left to grow, the seed term
    alone overflows int64 and numpy either wraps or refuses the operand.
    """
    seed_term = ((seed & MASK32) * 2246822519) & MASK32
    xt = (x * 374761393) & MASK32
    yt = (y * 668265263) & MASK32

    h = (xt + yt + seed_term) & MASK32
    h = ((h ^ (h >> 13)) * 1274126177) & MASK32
    h = h ^ (h >> 16)
    return ((h >> 8) * np.float32(1.0 / 16777216.0)).astype(np.float32)


def _sample(x: np.ndarray, y: np.ndarray, seed: int) -> np.ndarray:
    x0 = np.floor(x).astype(np.int64)
    y0 = np.floor(y).astype(np.int64)
    sx = (x - x0.astype(np.float32)).astype(np.float32)
    sy = (y - y0.astype(np.float32)).astype(np.float32)
    sx = sx * sx * (np.float32(3.0) - np.float32(2.0) * sx)
    sy = sy * sy * (np.float32(3.0) - np.float32(2.0) * sy)

    n00 = _hash01(x0, y0, seed)
    n10 = _hash01(x0 + 1, y0, seed)
    n01 = _hash01(x0, y0 + 1, seed)
    n11 = _hash01(x0 + 1, y0 + 1, seed)

    a = n00 + (n10 - n00) * sx
    b = n01 + (n11 - n01) * sx
    return a + (b - a) * sy


def fbm(x: np.ndarray, y: np.ndarray, seed: int, octaves: int,
        lacunarity: float = 2.0, gain: float = 0.5) -> np.ndarray:
    """Fractal Brownian motion over a grid of sample points."""
    total = np.float32(0.0)
    amplitude = np.float32(1.0)
    acc = np.zeros_like(x, dtype=np.float32)
    fx, fy = x.astype(np.float32), y.astype(np.float32)

    for i in range(octaves):
        acc = acc + _sample(fx, fy, seed + i * 7919) * amplitude
        total = total + amplitude
        amplitude = amplitude * np.float32(gain)
        fx = fx * np.float32(lacunarity)
        fy = fy * np.float32(lacunarity)

    return acc / total if total > 0 else acc


# --- Terrain ---------------------------------------------------------------------

ROAD, PLAINS, FOREST, MARSH, FORD, MOUNTAIN_PASS, WATER, CLIFF = range(8)

TERRAIN_NAMES = ["Road", "Plains", "Forest", "Marsh", "Ford", "MountainPass", "Water", "Cliff"]

SPEED = [1.25, 1.00, 0.70, 0.45, 0.50, 0.60, 0.0, 0.0]
SIGHT = [1.00, 1.30, 0.55, 0.75, 1.10, 0.90, 0.0, 0.0]
AMBUSH = [1.2, 0.8, 1.5, 1.0, 1.3, 0.9, 0.0, 0.0]
TRAP_DENSITY = [0.6, 0.5, 1.0, 2.5, 0.8, 1.4, 0.0, 0.0]
PASSABLE = [True, True, True, True, True, True, False, False]

TRAVEL_COST = [1.0 / s if p else math.inf for s, p in zip(SPEED, PASSABLE)]
MIN_TRAVEL_COST = min(c for c in TRAVEL_COST if math.isfinite(c))

TILE_SIZE = 4.0

# Ordering along the elevation axis, low to high.
ELEVATION_RANK = {WATER: 0, MARSH: 10, FORD: 20, PLAINS: 30, ROAD: 35,
                  FOREST: 40, MOUNTAIN_PASS: 50, CLIFF: 60}


class TileGrid:
    """The level's terrain and height field, as flat row-major arrays."""

    def __init__(self, width: int, height: int, fill: int = PLAINS):
        self.width = width
        self.height = height
        self.tiles = np.full(width * height, fill, dtype=np.uint8)
        self.elevation = np.zeros(width * height, dtype=np.float32)

    @property
    def tile_count(self) -> int:
        return self.width * self.height

    def to_index(self, x: int, y: int) -> int:
        return y * self.width + x

    def to_coords(self, index: int):
        y, x = divmod(index, self.width)
        return x, y

    def in_bounds(self, x: int, y: int) -> bool:
        return 0 <= x < self.width and 0 <= y < self.height

    def at(self, x: int, y: int) -> int:
        return int(self.tiles[y * self.width + x])

    def set(self, x: int, y: int, value: int) -> None:
        self.tiles[y * self.width + x] = value

    def is_passable(self, x: int, y: int) -> bool:
        return self.in_bounds(x, y) and PASSABLE[int(self.tiles[y * self.width + x])]

    def corner_elevation(self, cx: int, cy: int) -> float:
        total = 0.0
        count = 0
        for dy in (-1, 0):
            for dx in (-1, 0):
                x, y = cx + dx, cy + dy
                if not self.in_bounds(x, y):
                    continue
                total += float(self.elevation[y * self.width + x])
                count += 1
        return total / count if count else 0.0

    def surface_elevation(self, world_x: float, world_z: float) -> float:
        """Ground height at a world position, interpolated as the rendered surface is."""
        tx = world_x / TILE_SIZE
        tz = world_z / TILE_SIZE
        x = min(max(int(tx), 0), self.width - 1)
        y = min(max(int(tz), 0), self.height - 1)
        fx = min(max(tx - x, 0.0), 1.0)
        fz = min(max(tz - y, 0.0), 1.0)

        h00 = self.corner_elevation(x, y)
        h10 = self.corner_elevation(x + 1, y)
        h01 = self.corner_elevation(x, y + 1)
        h11 = self.corner_elevation(x + 1, y + 1)

        top = h00 + (h10 - h00) * fx
        bottom = h01 + (h11 - h01) * fx
        return top + (bottom - top) * fz


def tile_centre(grid: TileGrid, tile: int):
    x, y = grid.to_coords(tile)
    return ((x + 0.5) * TILE_SIZE, (y + 0.5) * TILE_SIZE)


# --- Pathfinding -----------------------------------------------------------------

SQRT2 = 1.41421356
_DX = (1, -1, 0, 0, 1, 1, -1, -1)
_DY = (0, 0, 1, -1, 1, -1, 1, -1)

_pack, _unpack = struct.pack, struct.unpack


def f32(value: float) -> float:
    """Rounds a double to what a C# float would hold. See the module docstring."""
    return _unpack("f", _pack("f", value))[0]


_SQRT2_F = f32(SQRT2)
_SQRT2_MINUS_ONE = f32(_SQRT2_F - 1.0)
_MIN_TRAVEL_COST_F = f32(MIN_TRAVEL_COST)


class _MinHeap:
    """Binary min-heap with lazy deletion, matching the C# implementation's ordering."""

    __slots__ = ("items", "keys", "count")

    def __init__(self):
        self.items: List[int] = []
        self.keys: List[float] = []
        self.count = 0

    def clear(self) -> None:
        self.items.clear()
        self.keys.clear()
        self.count = 0

    def push(self, item: int, key: float) -> None:
        self.items.append(item)
        self.keys.append(key)
        i = self.count
        self.count += 1
        items, keys = self.items, self.keys
        while i > 0:
            parent = (i - 1) >> 1
            if keys[parent] <= keys[i]:
                break
            items[parent], items[i] = items[i], items[parent]
            keys[parent], keys[i] = keys[i], keys[parent]
            i = parent

    def pop(self) -> int:
        items, keys = self.items, self.keys
        result = items[0]
        self.count -= 1
        last = self.count
        if last > 0:
            items[0] = items[last]
            keys[0] = keys[last]
            i = 0
            while True:
                left = 2 * i + 1
                if left >= self.count:
                    break
                right = left + 1
                smallest = right if (right < self.count and keys[right] < keys[left]) else left
                if keys[i] <= keys[smallest]:
                    break
                items[i], items[smallest] = items[smallest], items[i]
                keys[i], keys[smallest] = keys[smallest], keys[i]
                i = smallest
        items.pop()
        keys.pop()
        return result


class GridPathfinder:
    """Terrain-weighted A* over a TileGrid, minimising travel time rather than distance."""

    def __init__(self, grid: TileGrid):
        self.grid = grid
        n = grid.tile_count
        self._g = [0.0] * n
        self._came_from = [-1] * n
        self._seen = [0] * n
        self._closed = [0] * n
        self._open = _MinHeap()
        self._generation = 0

        # Plain lists rather than the grid's arrays: this loop runs tens of thousands
        # of times per level and numpy scalar indexing dominates the cost.
        self._passable = [PASSABLE[t] for t in grid.tiles.tolist()]
        self._cost = [f32(TRAVEL_COST[t]) for t in grid.tiles.tolist()]

    def find_path(self, sx: int, sy: int, gx: int, gy: int,
                  extra_cost: Optional[Sequence[float]] = None):
        """Returns (tiles, travel_time) or (None, 0.0) when no route exists."""
        grid = self.grid
        if not grid.is_passable(sx, sy) or not grid.is_passable(gx, gy):
            return None, 0.0

        start = grid.to_index(sx, sy)
        goal = grid.to_index(gx, gy)
        if start == goal:
            return [start], 0.0

        self._generation += 1
        generation = self._generation
        self._open.clear()

        g, came_from, seen, closed = self._g, self._came_from, self._seen, self._closed
        passable, cost = self._passable, self._cost
        width, height = grid.width, grid.height
        push = self._open.push

        g[start] = 0.0
        came_from[start] = -1
        seen[start] = generation
        push(start, _heuristic(sx, sy, gx, gy))

        while self._open.count > 0:
            current = self._open.pop()
            if closed[current] == generation:
                continue
            closed[current] = generation

            if current == goal:
                path = []
                node = goal
                while node != -1:
                    path.append(node)
                    node = came_from[node]
                path.reverse()
                return path, g[goal]

            cy, cx = divmod(current, width)

            g_current = g[current]

            for d in range(8):
                dx, dy = _DX[d], _DY[d]
                nx = cx + dx
                ny = cy + dy
                if nx < 0 or ny < 0 or nx >= width or ny >= height:
                    continue

                neighbour = ny * width + nx
                if not passable[neighbour]:
                    continue

                diagonal = d >= 4
                if diagonal:
                    # Refuse to squeeze diagonally between two blocked tiles.
                    if not (0 <= cx + dx < width and passable[cy * width + cx + dx]):
                        continue
                    if not (0 <= cy + dy < height and passable[(cy + dy) * width + cx]):
                        continue

                if closed[neighbour] == generation:
                    continue

                step = cost[neighbour]
                if diagonal:
                    step = f32(step * _SQRT2_F)
                if extra_cost is not None:
                    step = f32(step + extra_cost[neighbour])

                tentative = f32(g_current + step)

                if seen[neighbour] != generation or tentative < g[neighbour]:
                    seen[neighbour] = generation
                    g[neighbour] = tentative
                    came_from[neighbour] = current
                    push(neighbour, f32(tentative + _heuristic(nx, ny, gx, gy)))

        return None, 0.0


def _heuristic(x: int, y: int, goal_x: int, goal_y: int) -> float:
    """Octile distance scaled by the cheapest possible tile, rounded as C# float would."""
    dx = abs(x - goal_x)
    dy = abs(y - goal_y)
    hi = max(dx, dy)
    lo = min(dx, dy)
    return f32(f32(hi + f32(_SQRT2_MINUS_ONE * lo)) * _MIN_TRAVEL_COST_F)


# --- Corridors -------------------------------------------------------------------

FAST, SAFE, ODD = 0, 1, 2
CORRIDOR_NAMES = ["Fast", "Safe", "Odd"]

SAFETY_WEIGHT = 2.4
NEUTRAL_AMBUSH = 0.9
ANCHOR_BAND_LOW = 0.28
ANCHOR_BAND_HIGH = 0.72


@dataclass
class Corridor:
    kind: int
    tiles: List[int]
    travel_cost: float
    ambush_exposure: float


def _build_corridor(kind: int, grid: TileGrid, tiles: List[int], travel_cost: float) -> Corridor:
    ambush = sum(AMBUSH[grid.tiles[t]] for t in tiles)
    exposure = ambush / len(tiles) if tiles else 0.0
    return Corridor(kind, list(tiles), travel_cost, exposure)


def _measure_travel_cost(grid: TileGrid, tiles: List[int]) -> float:
    cost = 0.0
    width = grid.width
    for i in range(1, len(tiles)):
        py, px = divmod(tiles[i - 1], width)
        cy, cx = divmod(tiles[i], width)
        step = f32(TRAVEL_COST[grid.tiles[tiles[i]]])
        if px != cx and py != cy:
            step = f32(step * _SQRT2_F)
        cost = f32(cost + step)
    return cost


def find_corridors(grid: TileGrid, sx: int, sy: int, gx: int, gy: int) -> List[Corridor]:
    """The fast, cautious and odd routes a level must offer."""
    result: List[Corridor] = []
    pathfinder = GridPathfinder(grid)

    path, fast_cost = pathfinder.find_path(sx, sy, gx, gy)
    if path is None:
        return result
    result.append(_build_corridor(FAST, grid, path, fast_cost))

    safety_cost = [0.0] * grid.tile_count
    for i in range(grid.tile_count):
        ambush = AMBUSH[grid.tiles[i]]
        safety_cost[i] = (f32(f32(f32(ambush) - f32(NEUTRAL_AMBUSH)) * f32(SAFETY_WEIGHT))
                          if ambush > NEUTRAL_AMBUSH else 0.0)

    path, _ = pathfinder.find_path(sx, sy, gx, gy, safety_cost)
    if path is not None:
        result.append(_build_corridor(SAFE, grid, path, _measure_travel_cost(grid, path)))

    anchor = _find_detour_anchor(grid, result)
    if anchor >= 0:
        ax, ay = grid.to_coords(anchor)
        first, _ = pathfinder.find_path(sx, sy, ax, ay)
        if first is not None:
            second, _ = pathfinder.find_path(ax, ay, gx, gy)
            if second is not None:
                odd_tiles = list(first) + list(second[1:])
                result.append(_build_corridor(ODD, grid, odd_tiles,
                                              _measure_travel_cost(grid, odd_tiles)))

    return result


def _find_detour_anchor(grid: TileGrid, found: List[Corridor]) -> int:
    """The passable tile in the middle band lying furthest, on foot, from every route so far."""
    distance = [-1] * grid.tile_count
    queue: List[int] = []

    for corridor in found:
        for tile in corridor.tiles:
            if distance[tile] != 0:
                distance[tile] = 0
                queue.append(tile)

    if not queue:
        return -1

    head = 0
    width = grid.width
    while head < len(queue):
        current = queue[head]
        head += 1
        cy, cx = divmod(current, width)

        for d in range(4):
            nx = cx + (1 if d == 0 else -1 if d == 1 else 0)
            ny = cy + (1 if d == 2 else -1 if d == 3 else 0)
            if not grid.is_passable(nx, ny):
                continue
            neighbour = ny * width + nx
            if distance[neighbour] >= 0:
                continue
            distance[neighbour] = distance[current] + 1
            queue.append(neighbour)

    low_x = int(grid.width * ANCHOR_BAND_LOW)
    high_x = int(grid.width * ANCHOR_BAND_HIGH)

    best, best_distance = -1, 0
    for x in range(low_x, min(high_x, grid.width - 1) + 1):
        for y in range(grid.height):
            index = y * width + x
            if distance[index] <= best_distance:
                continue
            best_distance = distance[index]
            best = index

    return best if best_distance >= 4 else -1


def overlap(a: Corridor, b: Corridor) -> float:
    """Jaccard overlap of two routes' tiles."""
    if not a.tiles or not b.tiles:
        return 0.0
    set_a = set(a.tiles)
    shared = sum(1 for tile in b.tiles if tile in set_a)
    union = len(a.tiles) + len(b.tiles) - shared
    return shared / union if union > 0 else 0.0


def worst_overlap(corridors: Sequence[Corridor]) -> float:
    worst = 0.0
    for i in range(len(corridors)):
        for j in range(i + 1, len(corridors)):
            worst = max(worst, overlap(corridors[i], corridors[j]))
    return worst


def is_meaningful_choice(corridors: Sequence[Corridor], max_overlap: float = 0.62,
                         min_time_spread: float = 0.12, min_danger_spread: float = 0.08) -> bool:
    if corridors is None or len(corridors) < 3:
        return False

    any_distinct = False
    for a in range(len(corridors)):
        if any_distinct:
            break
        for b in range(a + 1, len(corridors)):
            if overlap(corridors[a], corridors[b]) <= max_overlap:
                any_distinct = True
                break

    if not any_distinct:
        return False

    fastest = min(c.travel_cost for c in corridors)
    slowest = max(c.travel_cost for c in corridors)
    safest = min(c.ambush_exposure for c in corridors)
    rashest = max(c.ambush_exposure for c in corridors)

    if fastest <= 0.0 or safest <= 0.0:
        return False

    return ((slowest - fastest) / fastest >= min_time_spread
            and (rashest - safest) / safest >= min_danger_spread)


# --- Enemies, traps, silver ------------------------------------------------------

WOLF, BANDIT, BANDIT_ARCHER = 0, 1, 2
ENEMY_NAMES = ["Wolf", "Bandit", "BanditArcher"]
ENEMY_ALL = [WOLF, BANDIT, BANDIT_ARCHER]
ENEMY_POINTS = [5, 8, 7]
ENEMY_GROUP_SIZE = [5, 4, 3]
ENEMY_SILVER_PER_KILL = [3, 6, 5]

PIT, LOG = 0, 1
TRAP_NAMES = ["Pit", "Log"]
TRAP_POINTS = [2, 3]
TRAP_DISARM_SILVER = [8, 8]

SAFE_END_TILES = 6
BASE_TRAP_CHANCE = 0.035

# --- Placement over the whole band -------------------------------------------------
#
# The player draws the route now, so threat can no longer live on three corridors. It
# lives on every tile a sane crossing could use, and these are the numbers that decide
# which tiles those are and how thickly they are covered.

# How far past the fastest crossing a detour may run before it stops being a route
# anyone would draw. 1.6 keeps the whole width of a 64-tile map in play without
# spending the budget in the corners.
BAND_SLACK = 1.6

# Travel cost kept clear at either end, so nothing is waiting in the first strides.
SAFE_END_COST = 8.0

GROUP_SPACING_TILES = 5.0
TRAP_SPACING_TILES = 3.0

# How close a group has to be to the drawn line to wake and reach the caravan. The
# widest detect radius in EnemyTable is 22 m, which is five and a half tiles; four is
# the distance at which it will certainly close.
ENGAGE_RADIUS_TILES = 4.0

# The promise: no drawn route meets fewer than this many groups.
MIN_ENCOUNTERS = 5

# A group watches the country around it out to half the distance to its nearest
# neighbour, clamped. Placement alone cannot keep this promise: four encounters along
# a freely drawn line across a sixty-four tile map would need about twenty-eight
# groups to seal the band, and the budget buys twelve. Territory is what closes that
# gap without doubling the enemy count — a band of raiders watches a stretch of road
# rather than standing on one tile of it.
TERRITORY_MIN = 6.0
TERRITORY_MAX = 13.0

# Share of the threat budget spent on traps rather than on enemies, before the
# recipe's own trap density scales it. Traps are the other half of the route
# trade-off, not a tax on top of it.
TRAP_BUDGET_SHARE = 0.25

SAMPLE_ROUTES = 32
MAX_REPAIRS = 12

# Why a thing is where it is. Diagnostics only, but the distinction is the design:
# a guard is a promise, a scattered group is a probability, a repair is the placer
# admitting the probability was not enough.
GUARD, SCATTERED, REPAIR = 0, 1, 2
ORIGIN_NAMES = ["Guard", "Scattered", "Repair"]


@dataclass
class EnemySpawn:
    tile: int
    kind: int
    origin: int

    # Tiles of country this group watches. Cross it and they come for you — which is
    # how twelve groups cover a map fifty tiles wide without standing shoulder to
    # shoulder across it. See TERRITORY_MIN/MAX and docs/GDD.md §7.
    territory: float = 0.0


@dataclass
class TrapPlacement:
    tile: int
    kind: int
    origin: int


@dataclass
class SilverCache:
    tile: int
    amount: int
    origin: int


@dataclass
class EncounterLayout:
    """Everything hostile or valuable on a level, and what the placer proved about it."""

    enemies: List[EnemySpawn] = field(default_factory=list)
    traps: List[TrapPlacement] = field(default_factory=list)
    caches: List[SilverCache] = field(default_factory=list)

    total_silver: int = 0
    silver_validated: bool = False

    # What the placer measured about its own output. `min_encounters` is the number
    # the whole design rests on: the fewest groups any sampled route ran into.
    band_tiles: int = 0
    ford_guards: int = 0
    repairs: int = 0
    sampled_routes: int = 0
    min_encounters: int = 0

    @property
    def total_points(self) -> int:
        points = sum(ENEMY_POINTS[s.kind] for s in self.enemies)
        return points + sum(TRAP_POINTS[t.kind] for t in self.traps)


def _travel_field(grid: TileGrid, x: int, y: int) -> List[float]:
    """Cheapest travel cost from one tile to every other, over the same eight
    neighbours and the same costs the pathfinder uses.

    Two of these — one from the start, one from the goal — are what let placement
    reason about every crossing of the map at once instead of about three of them.
    """
    n = grid.tile_count
    dist = [math.inf] * n
    passable = [PASSABLE[t] for t in grid.tiles.tolist()]
    cost = [f32(TRAVEL_COST[t]) for t in grid.tiles.tolist()]
    width, height = grid.width, grid.height

    source = y * width + x
    if not passable[source]:
        return dist

    dist[source] = 0.0
    heap = _MinHeap()
    heap.push(source, 0.0)
    settled = [False] * n

    while heap.count > 0:
        current = heap.pop()
        if settled[current]:
            continue
        settled[current] = True
        cy, cx = divmod(current, width)
        base = dist[current]

        for d in range(8):
            dx, dy = _DX[d], _DY[d]
            nx, ny = cx + dx, cy + dy
            if nx < 0 or ny < 0 or nx >= width or ny >= height:
                continue
            neighbour = ny * width + nx
            if not passable[neighbour]:
                continue
            if d >= 4:
                if not passable[cy * width + cx + dx]:
                    continue
                if not passable[(cy + dy) * width + cx]:
                    continue

            step = cost[neighbour]
            if d >= 4:
                step = f32(step * _SQRT2_F)
            candidate = f32(base + step)
            if candidate < dist[neighbour]:
                dist[neighbour] = candidate
                heap.push(neighbour, candidate)

    return dist


@dataclass
class ThreatBand:
    """Every tile a sane crossing could pass through, and what it is worth threatening."""
    tiles: List[int]
    weight: dict
    from_start: List[float]
    from_goal: List[float]
    fastest: float


def build_band(grid: TileGrid, level_start: int, level_goal: int,
               slack: float = BAND_SLACK) -> Optional[ThreatBand]:
    """Tiles where the detour past them stays inside `slack` of the fastest crossing.

    Placing outside the band is budget spent where nobody goes; placing evenly inside
    it is what makes "the player will meet something" a property of the map rather
    than of luck.
    """
    sx, sy = grid.to_coords(level_start)
    gx, gy = grid.to_coords(level_goal)

    from_start = _travel_field(grid, sx, sy)
    from_goal = _travel_field(grid, gx, gy)

    fastest = from_start[level_goal]
    if not math.isfinite(fastest) or fastest <= 0.0:
        return None

    limit = fastest * slack
    tiles = []
    weight = {}

    for i in range(grid.tile_count):
        total = from_start[i] + from_goal[i]
        if not math.isfinite(total) or total > limit:
            continue
        if from_start[i] < SAFE_END_COST or from_goal[i] < SAFE_END_COST:
            continue

        terrain = int(grid.tiles[i])
        speed = SPEED[terrain]
        if speed <= 0.0:
            continue

        # Threat follows speed: fast ground carries the most, the fen the least. It is
        # the corridor rule — the quick way is the dangerous way — restated per tile,
        # which is the only form of it that survives the player drawing their own line.
        tiles.append(i)
        weight[i] = speed * AMBUSH[terrain]

    if not tiles:
        return None
    return ThreatBand(tiles, weight, from_start, from_goal, fastest)


def _ford_crossings(grid: TileGrid, band: ThreatBand) -> List[List[int]]:
    """Ford tiles grouped into crossings, one group per place the river can be forded."""
    in_band = set(band.tiles)
    fords = [i for i in in_band if int(grid.tiles[i]) == FORD]
    crossings = []
    seen = set()

    for tile in sorted(fords):
        if tile in seen:
            continue
        group = []
        stack = [tile]
        seen.add(tile)
        while stack:
            current = stack.pop()
            group.append(current)
            cx, cy = grid.to_coords(current)
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    nx, ny = cx + dx, cy + dy
                    if not grid.in_bounds(nx, ny):
                        continue
                    neighbour = grid.to_index(nx, ny)
                    if neighbour in seen or neighbour not in in_band:
                        continue
                    if int(grid.tiles[neighbour]) != FORD:
                        continue
                    seen.add(neighbour)
                    stack.append(neighbour)
        crossings.append(sorted(group))

    return crossings


def place_encounters(grid: TileGrid, corridors: Sequence[Corridor], recipe: "LevelRecipe",
                     rng: DeterministicRandom, level_start: int = -1,
                     level_goal: int = -1) -> EncounterLayout:
    """Places threat across the whole crossable band, then proves no route slips past it.

    The old rule spent the budget along the three corridors, in inverse proportion to
    their travel time. That worked while those three were the only routes on offer. Now
    the player draws their own line, and a route drawn between the corridors would have
    met nothing at all.

    So: guard the fords, spread the rest over the band by how fast the ground is, then
    sample routes and repair whatever they slip through. The guarantee the design needs
    — you always meet something — comes from the last two steps, not from the dice.
    """
    layout = EncounterLayout()
    if level_start < 0 or level_goal < 0 or not corridors:
        return layout

    band = build_band(grid, level_start, level_goal)
    if band is None:
        return layout

    layout.band_tiles = len(band.tiles)
    occupied = set()
    budget = recipe.enemy_budget

    budget -= _guard_the_fords(grid, band, recipe, rng, layout, occupied, budget)
    budget -= _scatter_traps(grid, band, recipe, rng, layout, occupied, budget)
    _scatter_enemies(grid, band, recipe, rng, layout, occupied, budget)

    _assign_territories(grid, layout)
    _tally_silver(layout, recipe)
    _verify_and_repair(grid, band, corridors, recipe, rng, layout, occupied,
                       level_start, level_goal)
    return layout


def _assign_territories(grid: TileGrid, layout: EncounterLayout) -> None:
    """Half the distance to the nearest other group, clamped.

    Halved so two neighbouring territories meet rather than overlap, and clamped so a
    group alone in a corner does not end up watching a quarter of the map.
    """
    tiles = [spawn.tile for spawn in layout.enemies]

    for index, spawn in enumerate(layout.enemies):
        x, y = grid.to_coords(spawn.tile)
        nearest = math.inf

        for other in range(len(tiles)):
            if other == index:
                continue
            ox, oy = grid.to_coords(tiles[other])
            nearest = min(nearest, math.hypot(ox - x, oy - y))

        radius = TERRITORY_MAX if not math.isfinite(nearest) else nearest * 0.5
        spawn.territory = min(max(radius, TERRITORY_MIN), TERRITORY_MAX)


def _round_half_even(value: float) -> int:
    return int(np.round(np.float32(value)))


def _spaced_enough(tile: int, grid: TileGrid, occupied, spacing: float) -> bool:
    """Groups arrive one at a time, so nothing is placed within a few tiles of another."""
    x, y = grid.to_coords(tile)
    limit = spacing * spacing
    for other in occupied:
        ox, oy = grid.to_coords(other)
        if (ox - x) ** 2 + (oy - y) ** 2 < limit:
            return False
    return True


def _guard_the_fords(grid, band, recipe, rng, layout, occupied, budget) -> int:
    """A group on every ford in the band.

    The river runs across the caravan's travel and can only be crossed at its fords, so
    a guard on each is the one placement that no drawn route can avoid. Everything else
    in this file is a probability; this is the floor.
    """
    spent = 0

    for crossing in _ford_crossings(grid, band):
        if spent >= budget:
            break

        # The middle of the crossing, so the guard sits on the ford rather than at the
        # water's edge where a route can slip around it.
        tile = crossing[len(crossing) // 2]
        if tile in occupied:
            continue

        kind = _pick_affordable(recipe.enemy_pool, rng, budget - spent)
        if kind is None:
            break

        layout.enemies.append(EnemySpawn(tile, kind, GUARD))
        occupied.add(tile)
        spent += ENEMY_POINTS[kind]
        layout.ford_guards += 1

    return spent


def _scatter_traps(grid, band, recipe, rng, layout, occupied, budget) -> int:
    """Traps over the band, thickest where the ground hides them: the marsh.

    Traps take a share of the budget rather than a per-tile chance. The chance was
    written for a corridor of seventy tiles and the band is three thousand, so carried
    over unchanged it laid thirty-five traps and left the enemies nothing to spend —
    the level became a minefield with four guards in it.
    """
    allowance = int(budget * TRAP_BUDGET_SHARE * recipe.trap_density)
    if allowance <= 0:
        return 0

    scored = []
    for tile in sorted(band.tiles):
        if tile in occupied:
            continue
        density = TRAP_DENSITY[int(grid.tiles[tile])]
        if density <= 0.0:
            continue
        scored.append((density * rng.range_float(0.5, 1.5), tile))
    scored.sort(key=lambda entry: entry[0], reverse=True)

    spent = 0
    for _, tile in scored:
        if spent >= allowance:
            break
        if tile in occupied:
            continue
        if not _spaced_enough(tile, grid, occupied, TRAP_SPACING_TILES):
            continue

        kind = PIT if rng.chance(0.6) else LOG
        cost = TRAP_POINTS[kind]
        if spent + cost > allowance:
            continue

        layout.traps.append(TrapPlacement(tile, kind, SCATTERED))
        occupied.add(tile)
        spent += cost

    return spent


def _scatter_enemies(grid, band, recipe, rng, layout, occupied, budget) -> None:
    """The rest of the budget, over the band, weighted by how fast the ground is."""
    if budget <= 0:
        return

    scored = []
    for tile in sorted(band.tiles):
        if tile in occupied:
            continue
        scored.append((band.weight[tile] * rng.range_float(0.6, 1.4), tile))
    scored.sort(key=lambda entry: entry[0], reverse=True)

    for _, tile in scored:
        if budget <= 0:
            break
        if tile in occupied:
            continue
        if not _spaced_enough(tile, grid, occupied, GROUP_SPACING_TILES):
            continue

        kind = _pick_affordable(recipe.enemy_pool, rng, budget)
        if kind is None:
            break

        layout.enemies.append(EnemySpawn(tile, kind, SCATTERED))
        occupied.add(tile)
        budget -= ENEMY_POINTS[kind]


def _pick_affordable(pool, rng, budget) -> Optional[int]:
    source = pool if pool else ENEMY_ALL
    affordable = [kind for kind in source if ENEMY_POINTS[kind] <= budget]
    if not affordable:
        return None
    return affordable[rng.range_int(0, len(affordable))]


def _tally_silver(layout: EncounterLayout, recipe: "LevelRecipe") -> None:
    multiplier = recipe.silver_multiplier if recipe.silver_multiplier > 0.0 else 1.0
    layout.total_silver = 0

    for spawn in layout.enemies:
        group_silver = ENEMY_SILVER_PER_KILL[spawn.kind] * ENEMY_GROUP_SIZE[spawn.kind]
        layout.total_silver += int(group_silver * multiplier)

    for trap in layout.traps:
        layout.total_silver += int(TRAP_DISARM_SILVER[trap.kind] * multiplier)

    for cache in layout.caches:
        layout.total_silver += cache.amount


# --- Verification -----------------------------------------------------------------

def sample_routes(grid: TileGrid, band: ThreatBand, corridors: Sequence[Corridor],
                  rng: DeterministicRandom, level_start: int, level_goal: int,
                  count: int = SAMPLE_ROUTES) -> List[List[int]]:
    """Routes a player might actually draw: the three the generator knows, plus
    crossings through random waypoints in the band.

    This is the stand-in for the player. Everything the placer promises is checked
    against it, and a promise that only holds for the three corridors is exactly the
    promise that broke when the player was given a pen.
    """
    sx, sy = grid.to_coords(level_start)
    gx, gy = grid.to_coords(level_goal)

    pathfinder = GridPathfinder(grid)
    routes = [list(c.tiles) for c in corridors if c.tiles]
    pool = sorted(band.tiles)

    while len(routes) < count and pool:
        waypoints = [pool[rng.range_int(0, len(pool))]
                     for _ in range(1 + rng.range_int(0, 2))]

        tiles: List[int] = []
        fx, fy = sx, sy
        broken = False

        for waypoint in waypoints + [level_goal]:
            wx, wy = grid.to_coords(waypoint)
            leg, _ = pathfinder.find_path(fx, fy, wx, wy)
            if leg is None:
                broken = True
                break
            tiles.extend(leg if not tiles else leg[1:])
            fx, fy = wx, wy

        if not broken and tiles:
            routes.append(tiles)

    return routes


def _met_on_route(grid: TileGrid, route: Sequence[int], tiles: Sequence[int],
                  radius: float = ENGAGE_RADIUS_TILES,
                  radii: Optional[Sequence[float]] = None) -> List[int]:
    """Which of `tiles` a caravan on this route comes close enough to wake.

    `radii` gives each entry its own reach — a group's territory. Without it every
    entry uses the flat `radius`, which is what traps and caches want.
    """
    on_route = set(route)
    met = []

    for index, tile in enumerate(tiles):
        tx, ty = grid.to_coords(tile)
        if tile in on_route:
            met.append(index)
            continue

        reach = radius if radii is None else radii[index]
        limit = reach * reach
        for step in route:
            rx, ry = grid.to_coords(step)
            if (rx - tx) ** 2 + (ry - ty) ** 2 <= limit:
                met.append(index)
                break

    return met


def met_groups(grid: TileGrid, route: Sequence[int], layout: EncounterLayout) -> List[int]:
    """Indices of the enemy groups whose territory this route crosses."""
    return _met_on_route(grid, route, [s.tile for s in layout.enemies],
                         radii=[s.territory for s in layout.enemies])


def _verify_and_repair(grid, band, corridors, recipe, rng, layout, occupied,
                       level_start, level_goal) -> None:
    """Samples routes, and moves threat onto whichever one met too little.

    A scattered field says nothing about the worst case, and the worst case is the one
    that matters: a player who happens to draw between the groups gets a level with no
    game in it. So the placer measures its own output and repairs it.

    Repairs move a group rather than add one. Adding was the first version and it broke
    the ceiling the whole difficulty curve rests on — chapter 1 came out between 13 and
    71 percent over its enemy budget, and §6 of the status notes records what happens
    when that budget lands on less ground than it was measured for. A group no sampled
    route ever came near is doing nothing where it stands, so it is the one that moves.
    """
    routes = sample_routes(grid, band, corridors, rng, level_start, level_goal)
    layout.sampled_routes = len(routes)
    if not routes:
        return

    for _ in range(MAX_REPAIRS):
        met_by = [len(met_groups(grid, route, layout)) for route in routes]

        worst = min(range(len(routes)), key=lambda i: met_by[i])
        layout.min_encounters = met_by[worst]
        if met_by[worst] >= MIN_ENCOUNTERS:
            break

        donor = _idlest_group(grid, routes, layout)
        if donor < 0:
            break

        target = _emptiest_stretch(grid, routes[worst], band, occupied)
        if target < 0:
            break

        occupied.discard(layout.enemies[donor].tile)
        layout.enemies[donor] = EnemySpawn(target, layout.enemies[donor].kind, REPAIR)
        occupied.add(target)
        layout.repairs += 1
        _assign_territories(grid, layout)

    _tally_silver(layout, recipe)
    _top_up_silver(grid, routes, recipe, layout, occupied)


def _idlest_group(grid, routes, layout) -> int:
    """The placed group that fewest sampled routes come near. Ford guards never move."""
    best, fewest = -1, None

    for index, spawn in enumerate(layout.enemies):
        if spawn.origin == GUARD:
            continue

        met = 0
        for route in routes:
            if _met_on_route(grid, route, [spawn.tile], radii=[spawn.territory]):
                met += 1

        if fewest is None or met < fewest:
            fewest = met
            best = index

    return best


def _emptiest_stretch(grid: TileGrid, route: Sequence[int], band: ThreatBand,
                      occupied) -> int:
    """The tile on a route that is furthest from anything already placed."""
    best, best_distance = -1, -1.0

    for tile in route:
        if tile in occupied or tile not in band.weight:
            continue
        if band.from_start[tile] < SAFE_END_COST or band.from_goal[tile] < SAFE_END_COST:
            continue

        x, y = grid.to_coords(tile)
        nearest = math.inf
        for other in occupied:
            ox, oy = grid.to_coords(other)
            nearest = min(nearest, (ox - x) ** 2 + (oy - y) ** 2)

        if nearest > best_distance:
            best_distance = nearest
            best = tile

    return best


def _top_up_silver(grid, routes, recipe, layout, occupied) -> None:
    """A cache wherever a sampled route could not earn the floor.

    Same reasoning as the old per-corridor top-up: a route that cannot pay for two
    upgrades leaves the player at the level's last fight with an army they had no way
    to improve, which is broken rather than hard. Only the unit of measurement changed,
    from the corridor to the route the player might draw.
    """
    layout.silver_validated = True
    multiplier = recipe.silver_multiplier if recipe.silver_multiplier > 0.0 else 1.0

    for route in routes:
        earned = 0
        for index in met_groups(grid, route, layout):
            spawn = layout.enemies[index]
            earned += int(ENEMY_SILVER_PER_KILL[spawn.kind] * ENEMY_GROUP_SIZE[spawn.kind]
                          * multiplier)
        for index in _met_on_route(grid, route, [t.tile for t in layout.traps]):
            earned += int(TRAP_DISARM_SILVER[layout.traps[index].kind] * multiplier)
        for index in _met_on_route(grid, route, [c.tile for c in layout.caches]):
            earned += layout.caches[index].amount

        shortfall = recipe.min_silver_per_corridor - earned
        if shortfall <= 0:
            continue

        tile = _free_tile_on(route, occupied)
        if tile < 0:
            layout.silver_validated = False
            continue

        layout.caches.append(SilverCache(tile, shortfall, SCATTERED))
        occupied.add(tile)

    _tally_silver(layout, recipe)


def _free_tile_on(route: Sequence[int], occupied) -> int:
    middle = len(route) // 2
    for offset in range(len(route)):
        for direction in (1, -1):
            i = middle + offset * direction
            if i < SAFE_END_TILES or i >= len(route) - SAFE_END_TILES:
                continue
            if route[i] not in occupied:
                return route[i]
    return -1


# --- Recipes ---------------------------------------------------------------------

@dataclass
class LevelRecipe:
    width: int = 64
    height: int = 64
    terrain_mix: Sequence = ((FOREST, 0.45), (PLAINS, 0.30), (MARSH, 0.10),
                             (MOUNTAIN_PASS, 0.08), (WATER, 0.07))
    noise_scale: float = 18.0
    noise_octaves: int = 2
    smoothing_passes: int = 2
    rivers: int = 1
    fords_per_river: int = 3
    min_route_tiles: int = 40
    max_generation_attempts: int = 12
    enemy_budget: int = 100
    squad_budget: int = 12
    trap_density: float = 1.0
    silver_multiplier: float = 1.0
    enemy_strength: float = 1.0
    enemy_pool: Sequence[int] = tuple(ENEMY_ALL)
    min_silver_per_corridor: int = 55


@dataclass
class ChapterRecipe:
    """One chapter's difficulty curve, interpolated across its levels."""

    levels_per_chapter: int = 10
    enemy_budget_start: int = 100
    enemy_budget_end: int = 140
    enemy_strength_start: float = 1.00
    enemy_strength_end: float = 1.35
    trap_density_start: float = 0.5
    trap_density_end: float = 1.4
    route_tiles_start: int = 55
    route_tiles_end: int = 95
    squad_budget_start: int = 12
    squad_budget_end: int = 18
    silver_multiplier: float = 1.0
    enemy_unlock_level: Sequence[int] = (1, 2, 4)
    rivers: int = 1
    fords_per_river: int = 3
    noise_scale: float = 18.0

    def for_level(self, level: int) -> LevelRecipe:
        span = self.levels_per_chapter - 1 if self.levels_per_chapter > 1 else 1
        clamped = min(max(level, 1), self.levels_per_chapter)
        t = (clamped - 1) / span

        return LevelRecipe(
            enemy_budget=_lerp_int(self.enemy_budget_start, self.enemy_budget_end, t),
            enemy_strength=self.enemy_strength_start
            + (self.enemy_strength_end - self.enemy_strength_start) * t,
            trap_density=self.trap_density_start
            + (self.trap_density_end - self.trap_density_start) * t,
            min_route_tiles=_lerp_int(self.route_tiles_start, self.route_tiles_end, t),
            squad_budget=_lerp_int(self.squad_budget_start, self.squad_budget_end, t),
            silver_multiplier=self.silver_multiplier,
            rivers=self.rivers,
            fords_per_river=self.fords_per_river,
            noise_scale=self.noise_scale,
            enemy_pool=self.pool_for_level(clamped),
        )

    def pool_for_level(self, level: int) -> Sequence[int]:
        pool = [kind for kind in ENEMY_ALL
                if level >= (self.enemy_unlock_level[kind]
                             if kind < len(self.enemy_unlock_level) else 1)]
        return tuple(pool) if pool else (WOLF,)


def _lerp_int(a: int, b: int, t: float) -> int:
    return int(a + (b - a) * t + 0.5)


# --- The generator ---------------------------------------------------------------

EDGE_BAND = 3
PAIR_ATTEMPTS = 48


@dataclass
class LevelMap:
    grid: TileGrid
    seed: int
    start_x: int
    start_y: int
    goal_x: int
    goal_y: int
    fastest_route_cost: float
    corridors: List[Corridor]
    choice_validated: bool
    attempts: int
    encounters: EncounterLayout

    @property
    def start_index(self) -> int:
        return self.grid.to_index(self.start_x, self.start_y)

    @property
    def goal_index(self) -> int:
        return self.grid.to_index(self.goal_x, self.goal_y)

    def corridor_of(self, kind: int) -> Optional[Corridor]:
        for corridor in self.corridors:
            if corridor.kind == kind:
                return corridor
        return None


def generate(recipe: LevelRecipe, seed: int) -> LevelMap:
    """Turns a recipe plus a seed into playable terrain."""
    attempts = max(1, recipe.max_generation_attempts)

    best: Optional[LevelMap] = None
    best_spread = -1.0

    for attempt in range(attempts):
        rng = DeterministicRandom(seed + attempt * 7919)

        grid = _build_terrain(recipe, rng)
        _carve_rivers(grid, recipe, rng)

        endpoints = _try_place_endpoints(grid, recipe, rng)
        if endpoints is None:
            continue
        sx, sy, gx, gy = endpoints

        corridors = find_corridors(grid, sx, sy, gx, gy)
        if not corridors:
            continue

        valid = is_meaningful_choice(corridors)
        encounters = place_encounters(grid, corridors, recipe, rng,
                                      grid.to_index(sx, sy), grid.to_index(gx, gy))

        level = LevelMap(grid, seed, sx, sy, gx, gy, corridors[0].travel_cost,
                         corridors, valid, attempt + 1, encounters)

        if valid:
            return level

        spread = _spread_of(corridors)
        if spread > best_spread:
            best_spread = spread
            best = level

    return best if best is not None else _fallback(recipe, seed, attempts)


def _spread_of(corridors: Sequence[Corridor]) -> float:
    fastest = min(c.travel_cost for c in corridors)
    slowest = max(c.travel_cost for c in corridors)
    return (slowest - fastest) / fastest if fastest > 0.0 else 0.0


def _fallback(recipe: LevelRecipe, seed: int, attempts: int) -> LevelMap:
    grid = TileGrid(recipe.width, recipe.height, PLAINS)
    y = recipe.height // 2
    corridors = find_corridors(grid, 0, y, recipe.width - 1, y)
    cost = corridors[0].travel_cost if corridors else 0.0
    return LevelMap(grid, seed, 0, y, recipe.width - 1, y, cost, corridors, False,
                    attempts, EncounterLayout())


def _build_terrain(recipe: LevelRecipe, rng: DeterministicRandom) -> TileGrid:
    w, h = recipe.width, recipe.height
    grid = TileGrid(w, h)

    ox = rng.range_float(0.0, 4096.0)
    oy = rng.range_float(0.0, 4096.0)
    noise_seed = rng.next_uint()

    scale = recipe.noise_scale if recipe.noise_scale > 0.0 else 18.0

    xs = np.arange(w, dtype=np.float32)
    ys = np.arange(h, dtype=np.float32)
    sample_x = (np.float32(ox) + xs / np.float32(scale))[None, :].repeat(h, axis=0)
    sample_y = (np.float32(oy) + ys / np.float32(scale))[:, None].repeat(w, axis=1)

    field_2d = fbm(sample_x, sample_y, noise_seed, max(1, recipe.noise_octaves))
    field_2d = _blur(field_2d, recipe.smoothing_passes)

    _apply_mix_by_quantile(grid, field_2d.reshape(-1), recipe.terrain_mix)
    grid.elevation = field_2d.reshape(-1).astype(np.float32).copy()
    return grid


def _blur(field_2d: np.ndarray, passes: int) -> np.ndarray:
    """Box blur over the height field, before terrain types are assigned."""
    if passes <= 0:
        return field_2d

    h, w = field_2d.shape
    for _ in range(passes):
        padded = np.pad(field_2d, 1, mode="constant", constant_values=0.0)
        counts = np.pad(np.ones_like(field_2d), 1, mode="constant", constant_values=0.0)

        total = np.zeros_like(field_2d)
        n = np.zeros_like(field_2d)
        for dy in range(3):
            for dx in range(3):
                total = total + padded[dy:dy + h, dx:dx + w]
                n = n + counts[dy:dy + h, dx:dx + w]
        field_2d = (total / n).astype(np.float32)

    return field_2d


def _apply_mix_by_quantile(grid: TileGrid, field: np.ndarray, mix) -> None:
    """Assigns terrain by splitting the sorted noise values at the cumulative shares."""
    n = field.size
    ordered = sorted(mix, key=lambda entry: ELEVATION_RANK[entry[0]])

    total = sum(share for _, share in ordered if share > 0.0)
    if total <= 0.0:
        grid.tiles[:] = PLAINS
        return

    sorted_values = np.sort(field)

    cuts = []
    cumulative = 0.0
    for _, share in ordered:
        cumulative += (share if share > 0.0 else 0.0) / total
        idx = int(cumulative * (n - 1))
        idx = min(max(idx, 0), n - 1)
        cuts.append(float(sorted_values[idx]))
    cuts[-1] = math.inf

    bands = np.searchsorted(np.array(cuts, dtype=np.float64), field.astype(np.float64),
                            side="left")
    types = np.array([entry[0] for entry in ordered], dtype=np.uint8)
    grid.tiles[:] = types[np.minimum(bands, len(ordered) - 1)]


def _carve_rivers(grid: TileGrid, recipe: LevelRecipe, rng: DeterministicRandom) -> None:
    """Rivers run north to south, across the caravan's travel, crossable at their fords."""
    for _ in range(recipe.rivers):
        start_x = rng.range_int(grid.width // 6, grid.width - grid.width // 6)
        end_x = rng.range_int(grid.width // 6, grid.width - grid.width // 6)

        path = _meander_south(grid, start_x, end_x, rng)
        if not path:
            continue

        for tile in path:
            grid.tiles[tile] = WATER
        _place_fords(grid, path, max(1, recipe.fords_per_river))


def _meander_south(grid: TileGrid, start_x: int, end_x: int,
                   rng: DeterministicRandom) -> List[int]:
    path: List[int] = []
    x = min(max(start_x, 0), grid.width - 1)

    for y in range(grid.height):
        path.append(grid.to_index(x, y))

        progress = 1.0 if grid.height <= 1 else y / (grid.height - 1)
        desired = _round_half_even(start_x + (end_x - start_x) * progress)

        drift = 0
        if x < desired:
            drift = 1
        elif x > desired:
            drift = -1

        if rng.chance(0.28):
            drift = rng.range_int(-1, 2)

        nx = x + drift
        nx = max(1, min(nx, grid.width - 2))

        if nx != x:
            path.append(grid.to_index(nx, y))
        x = nx

    return path


def _place_fords(grid: TileGrid, river: List[int], fords: int) -> None:
    if not river:
        return

    for i in range(fords):
        t = (i + 1.0) / (fords + 1.0)
        at = int(t * (len(river) - 1))

        grid.tiles[river[at]] = FORD

        fx, fy = grid.to_coords(river[at])
        for dx in (-1, 0, 1):
            nx = fx + dx
            if grid.in_bounds(nx, fy) and grid.at(nx, fy) == WATER:
                grid.set(nx, fy, FORD)


def _try_place_endpoints(grid: TileGrid, recipe: LevelRecipe, rng: DeterministicRandom):
    left = _collect_band(grid, 0, min(EDGE_BAND, grid.width) - 1)
    right = _collect_band(grid, max(0, grid.width - EDGE_BAND), grid.width - 1)

    if not left:
        left.append(_force_open(grid, 0, grid.height // 2))
    if not right:
        right.append(_force_open(grid, grid.width - 1, grid.height // 2))

    rng.shuffle(left)
    rng.shuffle(right)

    pathfinder = GridPathfinder(grid)
    best_start, best_goal, best_tiles = -1, -1, -1

    attempts = min(PAIR_ATTEMPTS, len(left) * len(right))
    for i in range(attempts):
        s = left[i % len(left)]
        g = right[(i * 7 + i // len(left)) % len(right)]

        sx, sy = grid.to_coords(s)
        gx, gy = grid.to_coords(g)

        path, _ = pathfinder.find_path(sx, sy, gx, gy)
        if path is None:
            continue

        if len(path) > best_tiles:
            best_tiles = len(path)
            best_start, best_goal = s, g

        if len(path) >= recipe.min_route_tiles:
            break

    if best_start < 0:
        return None

    sx, sy = grid.to_coords(best_start)
    gx, gy = grid.to_coords(best_goal)
    return sx, sy, gx, gy


def _collect_band(grid: TileGrid, x_from: int, x_to: int) -> List[int]:
    result = []
    for x in range(x_from, x_to + 1):
        for y in range(grid.height):
            if grid.is_passable(x, y):
                result.append(grid.to_index(x, y))
    return result


def _force_open(grid: TileGrid, x: int, y: int) -> int:
    grid.set(x, y, PLAINS)
    return grid.to_index(x, y)


# --- The caravan -----------------------------------------------------------------

BASE_TILES_PER_SECOND = 2.0
WAGON_SPACING = 8.0
FORMATION_RADIUS = 6.0

SUPPLY, TREASURE, WAR = 0, 1, 2
WAGON_ORDER = [WAR, SUPPLY, TREASURE]
WAGON_NAMES = ["Supply", "Treasure", "War"]

VAN, RIGHT_VAN, RIGHT_REAR, REAR, LEFT_REAR, LEFT_VAN = range(6)
SPEARMEN, ARCHERS, SWORDSMEN, SHIELDBEARER, SCOUT, CAVALRY = 0, 1, 2, 3, 4, 5

# The default escort in LevelRunner: van, right van, rear and left van occupied.
DEFAULT_FORMATION = {VAN: SPEARMEN, RIGHT_VAN: ARCHERS, REAR: SWORDSMEN, LEFT_VAN: SCOUT}


class Caravan:
    """Three wagons following the planned route, with the escort in formation around them."""

    def __init__(self, grid: TileGrid, route: Sequence[int]):
        self.grid = grid
        self.tiles = list(route)
        self.points = [tile_centre(grid, tile) for tile in route]
        self.cumulative = [0.0] * len(route)
        for i in range(1, len(route)):
            px, py = self.points[i - 1]
            cx, cy = self.points[i]
            self.cumulative[i] = self.cumulative[i - 1] + math.hypot(cx - px, cy - py)
        self.distance = 0.0

    @property
    def total_distance(self) -> float:
        return self.cumulative[-1] if self.cumulative else 0.0

    @property
    def progress(self) -> float:
        return 1.0 if self.total_distance <= 0 else self.distance / self.total_distance

    def position_at(self, distance_along: float):
        if not self.points:
            return (0.0, 0.0)
        if len(self.points) == 1 or distance_along <= 0.0:
            return self.points[0]
        if distance_along >= self.total_distance:
            return self.points[-1]

        segment = self._find_segment(distance_along)
        start = self.cumulative[segment]
        length = self.cumulative[segment + 1] - start
        if length <= 0.0:
            return self.points[segment]

        t = (distance_along - start) / length
        fx, fy = self.points[segment]
        tx, ty = self.points[segment + 1]
        return (fx + (tx - fx) * t, fy + (ty - fy) * t)

    def tile_at(self, distance_along: float) -> int:
        if not self.tiles:
            return 0
        if distance_along <= 0.0:
            return self.tiles[0]
        if distance_along >= self.total_distance:
            return self.tiles[-1]
        return self.tiles[self._find_segment(distance_along)]

    def _find_segment(self, distance_along: float) -> int:
        low, high = 0, len(self.cumulative) - 1
        while low < high - 1:
            mid = (low + high) // 2
            if self.cumulative[mid] <= distance_along:
                low = mid
            else:
                high = mid
        return low

    @property
    def lead_position(self):
        return self.position_at(self.distance)

    @property
    def heading(self):
        if len(self.points) < 2:
            return (1.0, 0.0)
        ax, ay = self.position_at(self.distance + 1.0)
        bx, by = self.position_at(max(self.distance - 1.0, 0.0))
        dx, dy = ax - bx, ay - by
        length = math.hypot(dx, dy)
        return (1.0, 0.0) if length < 0.0001 else (dx / length, dy / length)

    @property
    def current_terrain(self) -> int:
        return int(self.grid.tiles[self.tile_at(self.distance)])

    @property
    def current_speed(self) -> float:
        return BASE_TILES_PER_SECOND * TILE_SIZE * SPEED[self.current_terrain]

    def wagon_position(self, index: int):
        trail = self.distance - index * WAGON_SPACING
        return self.position_at(max(trail, 0.0))

    def formation_positions(self, formation=None):
        """Slot 0 is dead ahead, then every 60 degrees clockwise about the lead wagon."""
        formation = DEFAULT_FORMATION if formation is None else formation
        hx, hy = self.heading
        cx, cy = self.lead_position

        placed = {}
        for slot, kind in formation.items():
            angle = slot * math.pi / 3.0
            cos, sin = math.cos(angle), math.sin(angle)
            ox = hx * cos - hy * sin
            oy = hx * sin + hy * cos
            placed[slot] = (kind, (cx + ox * FORMATION_RADIUS, cy + oy * FORMATION_RADIUS))
        return placed

    def advance_to(self, progress: float) -> None:
        self.distance = max(0.0, min(progress, 1.0)) * self.total_distance

    def elapsed_seconds_to(self, distance_along: float) -> float:
        """Time the caravan needs to reach a point, integrating terrain speed along the way."""
        seconds = 0.0
        travelled = 0.0
        for i in range(1, len(self.points)):
            px, py = self.points[i - 1]
            cx, cy = self.points[i]
            step = math.hypot(cx - px, cy - py)
            if travelled + step > distance_along:
                step = distance_along - travelled
            speed = BASE_TILES_PER_SECOND * TILE_SIZE * SPEED[int(self.grid.tiles[self.tiles[i - 1]])]
            if speed > 0.0:
                seconds += step / speed
            travelled += step
            if travelled >= distance_along:
                break
        return seconds


# --- Scenery ---------------------------------------------------------------------
#
# The decorator's placement, reproduced so the same trees stand in the same places.
# Model sets are counted rather than loaded — see the module docstring.

TREE_HEIGHT = 7.0
PINE_HEIGHT = 8.5
ROCK_HEIGHT = 2.2
MOUNTAIN_HEIGHT = 20.0
DEAD_TREE_HEIGHT = 9.0
HOUSE_HEIGHT = 6.0
WATCHTOWER_HEIGHT = 11.0
FARM_WIDTH = 9.0
TIMBER_WIDTH = 3.0
RUIN_WIDTH = 5.0
COVER_HEIGHT = 0.7
SHORE_STONE_SIZE = 2.2

MAX_LANDMARKS = 18
MAX_GROUND_COVER = 4000
MAX_SHORE_STONES = 1600
RUIN_CLUSTER_TILES = 6

# Model counts from LoadForestDecor in Assets/Editor/ArnaSetup.cs.
SET_SIZES = {
    "trees": 5, "pines": 5, "dead": 5, "rocks": 7, "cover": 18,
    "mountains": 2, "houses": 5, "farms": 3, "towers": 2, "timber": 4, "ruins": 1,
}

COVER_DENSITY = {FOREST: 2.4, PLAINS: 1.7, MARSH: 2.0, MOUNTAIN_PASS: 0.5, ROAD: 0.15}
DENSITY = {FOREST: 0.28, MOUNTAIN_PASS: 0.18, PLAINS: 0.03, MARSH: 0.06, ROAD: 0.01}


@dataclass
class Prop:
    """One placed piece of scenery: what it is, where it stands and how big it came out."""
    kind: str
    x: float
    z: float
    ground_y: float
    size: float
    yaw: float
    by_width: bool


def ruin_sites(level: LevelMap) -> List[int]:
    """Ground that shows a caravan came to grief: near a trap field, never on one."""
    traps = level.encounters.traps
    if not traps:
        return []

    rng = DeterministicRandom(level.seed ^ 0x2117)
    neighbourhoods = set()
    sites: List[int] = []

    for trap in traps:
        x, y = level.grid.to_coords(trap.tile)
        cell = (y // RUIN_CLUSTER_TILES) * level.grid.width + x // RUIN_CLUSTER_TILES
        if cell in neighbourhoods:
            continue
        neighbourhoods.add(cell)

        for _ in range(10):
            nx = x + rng.range_int(-3, 4)
            ny = y + rng.range_int(-3, 4)
            if not level.grid.in_bounds(nx, ny):
                continue
            terrain = level.grid.at(nx, ny)
            if terrain in (WATER, CLIFF):
                continue
            sites.append(level.grid.to_index(nx, ny))
            break

    return sites


def decorate(grid: TileGrid, seed: int, keep_clear=None, height_scale: float = 0.0,
             max_props: int = 600, density_scale: float = 1.0,
             sites: Optional[Sequence[int]] = None) -> List[Prop]:
    """Scatters scenery across the terrain, seeded so a level is dressed identically."""
    rng = DeterministicRandom(seed ^ 0x5EED10)
    clear = set(keep_clear) if keep_clear else None
    props: List[Prop] = []
    occupied = set()

    _place_landmarks(props, grid, rng, clear, occupied, height_scale, sites)
    placed = len(props)

    for i in range(grid.tile_count):
        if placed >= max_props:
            break
        terrain = int(grid.tiles[i])
        density = DENSITY.get(terrain)
        if density is None:
            continue
        if clear is not None and i in clear:
            continue
        if i in occupied:
            continue
        if not rng.chance(density * density_scale):
            continue

        choice = _pick(terrain, rng)
        if choice is None:
            continue

        _scatter(props, grid, rng, choice, i, height_scale, spread=1.4)
        placed += 1

    _place_ground_cover(props, grid, rng, clear, occupied, height_scale, density_scale)
    _place_shoreline(props, grid, rng, occupied, height_scale)
    return props


def _pick(terrain: int, rng: DeterministicRandom):
    if terrain == FOREST:
        if rng.chance(0.62):
            return ("pines", PINE_HEIGHT, False, rng.range_int(0, SET_SIZES["pines"]))
        return ("trees", TREE_HEIGHT, False, rng.range_int(0, SET_SIZES["trees"]))

    if terrain == MOUNTAIN_PASS:
        if rng.chance(0.30):
            return ("mountains", MOUNTAIN_HEIGHT, False, rng.range_int(0, SET_SIZES["mountains"]))
        return ("rocks", ROCK_HEIGHT, False, rng.range_int(0, SET_SIZES["rocks"]))

    if terrain == MARSH:
        if rng.chance(0.55):
            return ("dead", DEAD_TREE_HEIGHT, False, rng.range_int(0, SET_SIZES["dead"]))
        return ("rocks", ROCK_HEIGHT, False, rng.range_int(0, SET_SIZES["rocks"]))

    if terrain in (PLAINS, ROAD):
        if rng.chance(0.6):
            return ("rocks", ROCK_HEIGHT, False, rng.range_int(0, SET_SIZES["rocks"]))
        return ("trees", TREE_HEIGHT, False, rng.range_int(0, SET_SIZES["trees"]))

    return None


def _scatter(props, grid, rng, choice, tile, height_scale, spread) -> None:
    kind, size, by_width, _model = choice
    cx, cz = tile_centre(grid, tile)
    x = cx + rng.range_float(-spread, spread)
    z = cz + rng.range_float(-spread, spread)
    ground_y = grid.surface_elevation(x, z) * height_scale

    yaw = rng.range_float(0.0, 360.0)
    scaled = size * rng.range_float(0.8, 1.25)
    props.append(Prop(kind, x, z, ground_y, scaled, yaw, by_width))


def _place_ground_cover(props, grid, rng, clear, occupied, height_scale, density_scale) -> int:
    placed = 0

    for i in range(grid.tile_count):
        if placed >= MAX_GROUND_COVER:
            break
        density = COVER_DENSITY.get(int(grid.tiles[i]))
        if density is None:
            continue
        if i in occupied:
            continue

        scale = 0.3 if (clear is not None and i in clear) else 1.0
        rate = density * density_scale * scale
        tufts = math.floor(rate)
        if rng.chance(rate - tufts):
            tufts += 1

        for _ in range(tufts):
            if placed >= MAX_GROUND_COVER:
                break
            choice = ("cover", COVER_HEIGHT, False, rng.range_int(0, SET_SIZES["cover"]))
            _scatter(props, grid, rng, choice, i, height_scale, spread=1.9)
            placed += 1

    return placed


def _place_shoreline(props, grid, rng, occupied, height_scale) -> int:
    """Stones at the water's edge — what makes a band of blue read as a river."""
    placed = 0

    for i in range(grid.tile_count):
        if placed >= MAX_SHORE_STONES:
            break
        if int(grid.tiles[i]) == WATER or i in occupied:
            continue

        x, y = grid.to_coords(i)
        if not _next_to_water(grid, x, y):
            continue

        stones = 3 + rng.range_int(0, 4)
        for _ in range(stones):
            if placed >= MAX_SHORE_STONES:
                break
            choice = ("shore", SHORE_STONE_SIZE, True, rng.range_int(0, SET_SIZES["rocks"]))
            _scatter(props, grid, rng, choice, i, height_scale, spread=2.0)
            placed += 1

    return placed


def _next_to_water(grid: TileGrid, x: int, y: int) -> bool:
    for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if grid.in_bounds(nx, ny) and grid.at(nx, ny) == WATER:
            return True
    return False


def _place_landmarks(props, grid, rng, clear, occupied, height_scale, sites) -> None:
    placed = 0

    if sites:
        for tile in sites:
            if placed >= MAX_LANDMARKS:
                break
            if clear is not None and tile in clear:
                continue
            if tile in occupied:
                continue
            occupied.add(tile)

            choice = ("ruins", RUIN_WIDTH, True, rng.range_int(0, SET_SIZES["ruins"]))
            _place(props, grid, tile, rng, choice, height_scale)
            placed += 1

            # Dead trees around it: a cart alone is too small to read from map height.
            for _ in range(2):
                dead = ("dead", DEAD_TREE_HEIGHT, False, rng.range_int(0, SET_SIZES["dead"]))
                _scatter(props, grid, rng, dead, tile, height_scale, spread=2.6)
                placed += 1

    for i in range(grid.tile_count):
        if placed >= MAX_LANDMARKS:
            break
        if clear is not None and i in clear:
            continue
        if i in occupied:
            continue

        x, y = grid.to_coords(i)
        terrain = int(grid.tiles[i])
        choice = None

        if terrain == ROAD and rng.chance(0.035):
            choice = ("houses", HOUSE_HEIGHT, False, rng.range_int(0, SET_SIZES["houses"]))
        elif terrain == PLAINS and _near_road(grid, x, y, 2) and rng.chance(0.16):
            choice = ("farms", FARM_WIDTH, True, rng.range_int(0, SET_SIZES["farms"]))
        elif terrain == MOUNTAIN_PASS and rng.chance(0.012):
            choice = ("towers", WATCHTOWER_HEIGHT, False, rng.range_int(0, SET_SIZES["towers"]))
        elif terrain == FOREST and rng.chance(0.006):
            choice = ("timber", TIMBER_WIDTH, True, rng.range_int(0, SET_SIZES["timber"]))

        if choice is None:
            continue

        occupied.add(i)
        _place(props, grid, i, rng, choice, height_scale)
        placed += 1


def _near_road(grid: TileGrid, x: int, y: int, radius: int) -> bool:
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            nx, ny = x + dx, y + dy
            if grid.in_bounds(nx, ny) and grid.at(nx, ny) == ROAD:
                return True
    return False


def _place(props, grid, tile, rng, choice, height_scale) -> None:
    kind, size, by_width, _model = choice
    cx, cz = tile_centre(grid, tile)
    ground_y = grid.surface_elevation(cx, cz) * height_scale
    yaw = rng.range_int(0, 4) * 90.0
    props.append(Prop(kind, cx, cz, ground_y, size, yaw, by_width))
