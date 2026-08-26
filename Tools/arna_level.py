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

# --- Route drawing ------------------------------------------------------------------
#
# The route is the player's, not the generator's (docs/GDD.md §3.3). They put down up
# to six waypoints and each leg is solved with terrain-weighted A*, so a roughly drawn
# line becomes a path a caravan master would actually have taken. The gap between what
# is drawn and what is walked is the whole reason the mechanic feels good rather than
# fiddly — and it is also where the one thing that can go wrong lives.

MAX_WAYPOINTS = 6

# How much longer than the crow's line a leg may run before it is flagged.
#
# Draw across a river away from its fords and nothing stops: A* goes around, and the
# caravan takes a detour nobody asked for. §3.3 says a detour must not arrive as a
# surprise, so the leg is marked and the preview says so before the run starts.
DETOUR_THRESHOLD = 1.4


@dataclass
class RouteLeg:
    """One stretch, from the last point the player put down to the next."""

    from_tile: int
    to_tile: int
    tile_count: int = 0
    travel_cost: float = 0.0

    # Where this leg's tiles sit in `RouteResult.tiles`, inclusive. §3.3 asks for the
    # failed leg drawn red and the detour leg drawn differently, which needs the leg's
    # own stretch of the line and not just its number.
    first: int = 0
    last: int = -1
    walked: float = 0.0          # tiles of ground, a diagonal counting √2
    straight_line: float = 0.0   # tiles as the crow flies
    failed: bool = False
    ford_tile: int = -1

    @property
    def detour(self) -> float:
        return 1.0 if self.straight_line <= 0.0 else self.walked / self.straight_line

    @property
    def is_detour(self) -> bool:
        return self.detour > DETOUR_THRESHOLD


@dataclass
class RouteResult:
    """What the map can tell the player about the line they drew, before they commit."""

    tiles: List[int] = field(default_factory=list)
    legs: List[RouteLeg] = field(default_factory=list)
    crossings: List[int] = field(default_factory=list)
    travel_cost: float = 0.0
    tiles_by_terrain: List[int] = field(default_factory=lambda: [0] * 8)

    # Mean ambush weight along the route, read off the terrain and nothing else. It
    # must not consult the encounter layout: what is actually out there is bought with
    # the eagle or paid for in blood, and a risk number that knew would hand it over
    # for free (§3.4). It says "this is ambush country", never "there are four of them
    # behind that ridge".
    ambush_exposure: float = 0.0

    valid: bool = False
    failed_leg: int = -1

    @property
    def detour_legs(self) -> int:
        return sum(1 for leg in self.legs if leg.is_detour)

    def share_of(self, terrain: int) -> float:
        return 0.0 if not self.tiles else self.tiles_by_terrain[terrain] / len(self.tiles)

    def estimated_seconds(self, tiles_per_second: float = 2.0) -> float:
        return 0.0 if tiles_per_second <= 0 else self.travel_cost / tiles_per_second


def can_place_waypoint(grid: TileGrid, tile: int, placed: Sequence[int] = ()) -> bool:
    """Whether a tap puts a waypoint down (mirrors `RoutePlanner.TryAddWaypoint`).

    A tap on deep water does nothing rather than snapping somewhere the player did not
    choose: a route that quietly moves the point you put down is a route you did not
    draw, which is the one thing this mechanic cannot afford.
    """
    if len(placed) >= MAX_WAYPOINTS:
        return False
    if not 0 <= tile < grid.tile_count:
        return False
    if not grid.is_passable(*grid.to_coords(tile)):
        return False
    return tile not in placed


def solve_route(grid: TileGrid, start: int, goal: int,
                waypoints: Sequence[int] = ()) -> RouteResult:
    """Stitches start → waypoints → goal into one route, and reads it back.

    Always returns a result; check `valid` before letting the player start.
    """
    result = RouteResult()
    pathfinder = GridPathfinder(grid)

    sx, sy = grid.to_coords(start)
    fx, fy = sx, sy

    for leg_index, waypoint in enumerate(list(waypoints) + [goal]):
        tx, ty = grid.to_coords(waypoint)
        leg = RouteLeg(grid.to_index(fx, fy), waypoint,
                       straight_line=math.hypot(tx - fx, ty - fy))

        tiles, cost = pathfinder.find_path(fx, fy, tx, ty)
        if tiles is None:
            leg.failed = True
            result.legs.append(leg)
            result.failed_leg = leg_index
            return result

        leg.tile_count = len(tiles)
        leg.travel_cost = cost
        leg.walked = _walked(grid, tiles)

        for tile in tiles:
            if int(grid.tiles[tile]) != FORD:
                continue
            if leg.ford_tile < 0:
                leg.ford_tile = tile
            if tile not in result.crossings:
                result.crossings.append(tile)

        # The seam belongs to both legs on screen even though it is counted once.
        leg.first = max(len(result.tiles) - (0 if not result.tiles else 1), 0)
        result.tiles.extend(tiles if not result.tiles else tiles[1:])
        leg.last = len(result.tiles) - 1

        result.legs.append(leg)
        result.travel_cost += cost
        fx, fy = tx, ty

    ambush = 0.0
    for tile in result.tiles:
        terrain = int(grid.tiles[tile])
        result.tiles_by_terrain[terrain] += 1
        ambush += AMBUSH[terrain]

    result.ambush_exposure = ambush / len(result.tiles) if result.tiles else 0.0
    result.valid = True
    return result


def _walked(grid: TileGrid, tiles: Sequence[int]) -> float:
    """Tiles of ground covered, a diagonal counting √2.

    Distance and not travel cost, deliberately. The detour warning is about the route
    going somewhere the player did not draw, and travel cost would confuse that with
    the route going somewhere slow — a leg through marsh is expensive without being a
    surprise, and a leg the long way round a river is a surprise even on good ground.
    """
    total = 0.0
    for previous, tile in zip(tiles, tiles[1:]):
        px, py = grid.to_coords(previous)
        x, y = grid.to_coords(tile)
        total += SQRT2 if px != x and py != y else 1.0
    return total


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

# What the repair loop actually aims at, which is one more than the promise.
#
# The loop can only measure the routes it sampled, and a player draws whatever they
# like. Repaired to exactly the promise, the sampled routes all met five and the ones
# nobody sampled met four — measured over sixty fresh routes on six levels, every one
# came in a group short. Aimed one above, the same measurement returns five and six.
# The margin is what the sampling costs; it is not slack.
REPAIR_TARGET = MIN_ENCOUNTERS + 1

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

# Donors the loop may try per repair it keeps. A rejected move costs an attempt and
# leaves the layout as it was, so without this the cap would be spent on candidates
# rather than on repairs.
REPAIR_ATTEMPTS = 4

# Landing spots offered per donor, emptiest first.
REPAIR_TARGETS = 3

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

    # False when the repair loop could not bring the worst sampled route up to
    # MIN_ENCOUNTERS. The generator reads this and rolls the level again.
    encounters_validated: bool = False

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

    One rule keeps the loop honest, and it had none at first: **a move that does not
    help is undone, and the group that made it is not asked again until something
    else has changed.**

    Hill-climbing needs to know which way is up. The score is the worst route's tally
    first, then how many routes are stuck at that tally — so a repair that lifts one
    route at another's expense is rejected rather than kept.

    Without it the loop livelocked. The group just moved is the idlest group on the
    next pass, because it went somewhere only one route reaches, so it was picked
    again — and again. Traced over forty passes on 2-5 the same band of raiders moved
    forty times while the worst route stayed pinned at four. All twelve repairs were
    being spent walking one group in a circle.

    A group whose move *was* kept may move again: the score strictly rises on every
    accepted move and is bounded above, so that cannot cycle.
    """
    routes = sample_routes(grid, band, corridors, rng, level_start, level_goal)
    layout.sampled_routes = len(routes)
    if not routes:
        return

    rejected = set()

    def score():
        """Worst route first, then how many routes share that worst. Higher is better."""
        met_by = [len(met_groups(grid, route, layout)) for route in routes]
        fewest = min(met_by)
        return fewest, -met_by.count(fewest), met_by.index(fewest)

    fewest, tied, worst = score()

    # A rejection costs an attempt but not a repair, so the cap still means what it
    # says: twelve groups moved, not twelve things tried.
    for _ in range(MAX_REPAIRS * REPAIR_ATTEMPTS):
        layout.min_encounters = fewest
        if fewest >= REPAIR_TARGET or layout.repairs >= MAX_REPAIRS:
            break

        donor = _idlest_group(grid, routes, layout, rejected)
        if donor < 0:
            break

        targets = _emptiest_stretches(grid, routes[worst], band, occupied)
        if not targets:
            break

        before = layout.enemies[donor]
        kept = None

        for target in targets:
            occupied.discard(before.tile)
            layout.enemies[donor] = EnemySpawn(target, before.kind, REPAIR)
            occupied.add(target)
            _assign_territories(grid, layout)

            after = score()
            if after[:2] > (fewest, tied):
                kept = after
                break

            occupied.discard(target)
            layout.enemies[donor] = before
            occupied.add(before.tile)
            _assign_territories(grid, layout)

        if kept is None:
            rejected.add(donor)
            continue

        layout.repairs += 1
        fewest, tied, worst = kept
        rejected.clear()          # the ground moved; a group that could not help may now

    layout.min_encounters = fewest
    # Against the target, not the promise. A level that reaches five on the routes
    # the placer sampled has no margin left for the ones it did not, and re-rolling
    # costs generation time where shipping it costs a level with no game in it.
    layout.encounters_validated = fewest >= REPAIR_TARGET
    _tally_silver(layout, recipe)
    _top_up_silver(grid, routes, recipe, layout, occupied)


def _idlest_group(grid, routes, layout, rejected=()) -> int:
    """The placed group that fewest sampled routes come near.

    Ford guards never move — a guard is the one placement no crossing can avoid, and
    spending it elsewhere gives that back. Nor does a group whose last move was
    rejected, until an accepted move changes the ground under the question.
    """
    best, fewest = -1, None

    for index, spawn in enumerate(layout.enemies):
        if spawn.origin == GUARD or index in rejected:
            continue

        met = 0
        for route in routes:
            if _met_on_route(grid, route, [spawn.tile], radii=[spawn.territory]):
                met += 1

        if fewest is None or met < fewest:
            fewest = met
            best = index

    return best


def _emptiest_stretches(grid: TileGrid, route: Sequence[int], band: ThreatBand,
                        occupied, count: int = REPAIR_TARGETS) -> List[int]:
    """Tiles on a route furthest from anything already placed, emptiest first.

    More than one, because the emptiest tile is a guess and not an answer. It is the
    stretch of road nothing else watches, which is usually where a group is worth
    most — but a group put there can cost another route more than it gains this one,
    and then the loop wants a second candidate rather than a different donor. Offering
    only the best tile was what left 1-10 giving up after a single repair.
    """
    scored = []

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

        scored.append((nearest, tile))

    scored.sort(key=lambda pair: (-pair[0], pair[1]))

    # Spread the candidates out. The three emptiest tiles on a route are usually
    # neighbours, and three tries at the same stretch of road is one try.
    chosen: List[int] = []
    for _, tile in scored:
        if _spaced_enough(tile, grid, chosen, GROUP_SPACING_TILES):
            chosen.append(tile)
        if len(chosen) >= count:
            break

    return chosen


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

    @property
    def encounters_validated(self) -> bool:
        """Whether every route the placer sampled met enough to be a level."""
        return self.encounters.encounters_validated

    def corridor_of(self, kind: int) -> Optional[Corridor]:
        for corridor in self.corridors:
            if corridor.kind == kind:
                return corridor
        return None


def generate(recipe: LevelRecipe, seed: int) -> LevelMap:
    """Turns a recipe plus a seed into playable terrain."""
    attempts = max(1, recipe.max_generation_attempts)

    best: Optional[LevelMap] = None
    best_rank = (False, False, -1.0)

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

        # Both, or roll again. The placer repairs what it can and says so when it
        # could not, and a level where any drawn line meets almost nothing is not one
        # to ship — it is one to re-roll, which costs nothing but generation time.
        #
        # Retrying on `valid` alone was the old rule, and it aged badly: it measures
        # whether the three corridors the generator found differ from each other, and
        # the player stopped choosing between them when they were given a pen. The
        # promise that replaced it was not a criterion at all.
        if valid and encounters.encounters_validated:
            return level

        rank = (encounters.encounters_validated, valid, _spread_of(corridors))
        if rank > best_rank:
            best_rank = rank
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


# --- The eagle -------------------------------------------------------------------

# Metres per second. A gliding bird covers ground fast enough that seven seconds is a
# sweep across the map rather than a stroll over one corner of it.
EAGLE_SPEED = 40.0

# Seconds aloft. The ability is bought per level, so this is the whole of it. Longer
# than the first pass, to buy back some of the ground the narrower trail gives up.
EAGLE_SECONDS = 10.0

# Metres either side of the flight the eagle can see down into. A narrow trail is worth
# more than a wide one at the same coverage: it wanders further, so what it uncovers is
# spread across the map instead of being one broad stripe through the middle.
EAGLE_SIGHT = 20.0

# How finely the flight is walked when marking what it saw. Half a tile keeps the
# trail continuous without sampling more than the reveal radius needs.
EAGLE_STEP = TILE_SIZE * 0.5


@dataclass
class ScoutFlight:
    """Where the eagle went and what it found.

    Deterministic from the level seed. That is not a detail: a flight rolled fresh on
    every press would let a player restart the level until the eagle happened to sweep
    the ground they cared about, and an ability you can re-roll for free is not a
    decision, it is a slot machine. Same level, same flight — the randomness is in the
    map, not in the retry.
    """

    path: List[tuple]           # world positions, metres
    revealed_tiles: set         # tiles the overlay is lifted from
    revealed_enemies: List[int] # indices into EncounterLayout.enemies
    seconds: float

    @property
    def coverage(self) -> float:
        return len(self.revealed_tiles)


# Inland points the flight bends through. Two gave a single sweep across the map; six
# give a bird that wanders — doubles back, cuts a corner, leaves a trail worth reading
# rather than a stripe.
EAGLE_WAYPOINTS = 6


def _flight_path(grid: TileGrid, rng: DeterministicRandom, seconds: float) -> List[tuple]:
    """A wandering curve that enters at an edge and then goes where it likes.

    The first version flew edge to edge through two control points, which always came
    out as one broad diagonal — the same picture on every level with the angle changed.
    A bird quartering ground does not do that. Six points, each free to be anywhere on
    the map, give a line that turns back on itself and covers scattered country.
    """
    extent = grid.width * TILE_SIZE

    def edge_point(edge: int) -> tuple:
        along = rng.range_float(0.15, 0.85) * extent
        if edge == 0:
            return (0.0, along)
        if edge == 1:
            return (extent, along)
        if edge == 2:
            return (along, 0.0)
        return (along, extent)

    # Each turn is taken from where the bird already is, at a random heading and a
    # third of the map away. Drawing six independent points anywhere on the map looked
    # like wandering but was not: the curve through them doubled back on one quarter and
    # left the other three untouched. A step from the last point is how something
    # quartering ground actually moves — it covers, rather than revisits.
    points = [edge_point(rng.range_int(0, 4))]
    for _ in range(EAGLE_WAYPOINTS):
        previous = points[-1]
        for _ in range(8):
            angle = rng.range_float(0.0, 2.0 * math.pi)
            reach = rng.range_float(0.28, 0.52) * extent
            candidate = (previous[0] + math.cos(angle) * reach,
                         previous[1] + math.sin(angle) * reach)
            if 0.05 * extent <= candidate[0] <= 0.95 * extent \
                    and 0.05 * extent <= candidate[1] <= 0.95 * extent:
                points.append(candidate)
                break
        else:
            points.append((rng.range_float(0.2, 0.8) * extent,
                           rng.range_float(0.2, 0.8) * extent))

    budget = EAGLE_SPEED * seconds
    path = []
    travelled = 0.0
    previous = points[0]
    path.append(previous)

    padded = [points[0]] + points + [points[-1]]
    for segment in range(len(points) - 1):
        p0, p1, p2, p3 = padded[segment:segment + 4]
        steps = 64
        for i in range(1, steps + 1):
            t = i / steps
            point = _catmull_rom(p0, p1, p2, p3, t)

            # Keep the bird over the map. A Catmull-Rom through points near the edge
            # overshoots outside it, and a trail that leaves the map is a trail the
            # player paid for and cannot use.
            point = (min(max(point[0], 0.0), extent), min(max(point[1], 0.0), extent))

            step = math.dist(previous, point)
            if travelled + step > budget:
                return path
            travelled += step
            path.append(point)
            previous = point

    return path


def _catmull_rom(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return tuple(
        0.5 * ((2 * p1[i])
               + (-p0[i] + p2[i]) * t
               + (2 * p0[i] - 5 * p1[i] + 4 * p2[i] - p3[i]) * t2
               + (-p0[i] + 3 * p1[i] - 3 * p2[i] + p3[i]) * t3)
        for i in range(2)
    )


def fly_the_eagle(level: LevelMap, seconds: float = EAGLE_SECONDS,
                  sight: float = EAGLE_SIGHT, flight: int = 0) -> ScoutFlight:
    """Flies the scouting ability over a level and reports what it uncovered.

    The eagle is bought before the route is drawn, and this is why: what it found is
    still on the map when the player picks up the pen. Bought for the run it would be
    a reveal buff; bought for the planning it is information that becomes a decision.
    """
    grid = level.grid
    rng = DeterministicRandom(level.seed ^ (0x3A91 + flight * 7919))

    path = _flight_path(grid, rng, seconds)
    revealed = set()

    radius_tiles = sight / TILE_SIZE
    span = int(math.ceil(radius_tiles))
    limit = radius_tiles * radius_tiles

    for x, y in path:
        cx, cy = int(x / TILE_SIZE), int(y / TILE_SIZE)
        for ty in range(cy - span, cy + span + 1):
            for tx in range(cx - span, cx + span + 1):
                if not grid.in_bounds(tx, ty):
                    continue
                if (tx - x / TILE_SIZE) ** 2 + (ty - y / TILE_SIZE) ** 2 > limit:
                    continue
                revealed.add(grid.to_index(tx, ty))

    # A group is found if the eagle passed over where it stands. Its territory does not
    # help it hide and does not help it be seen: the bird looks down at the ground.
    found = [index for index, spawn in enumerate(level.encounters.enemies)
             if spawn.tile in revealed]

    return ScoutFlight(path, revealed, found, seconds)


# --- Scenery ---------------------------------------------------------------------
#
# The decorator's placement, reproduced so the same trees stand in the same places.
# Model sets are counted rather than loaded — see the module docstring.

# How far a scattered prop may vary from its table size. Mirrors
# Arna.View.TerrainDecorator: a quarter either way for rocks, grass and buildings, and
# far more for trees. A quarter gave a stand of spruces between 6.8 and 10.6 m — a hedge,
# evenly clipped — while in the reference picture the smallest conifer is about a third
# the height of the largest. 0.55 to 1.7 against a pine's 8.5 m is 4.7 m to 14.5 m.
JITTER_LOW, JITTER_HIGH = 0.8, 1.25
TREE_JITTER_LOW, TREE_JITTER_HIGH = 0.55, 1.7

# Dead trees are the exception, and a render said so before anything else did. They
# start at 9 m — the tallest entry in the table, because a bare trunk has to read from
# map height — and they are drawn as a pole a tenth as wide. At 1.7 that is a
# fifteen-metre spike, and a ridge of them reads as a power line rather than as a fen.
# Small snags yes, giants no.
DEAD_JITTER_LOW, DEAD_JITTER_HIGH = 0.5, 1.15

TREE_KINDS = ("trees", "pines", "birch")

TREE_HEIGHT = 7.0
PINE_HEIGHT = 8.5
ROCK_HEIGHT = 2.2
BOULDER_WIDTH = 5.5
BUSH_HEIGHT = 1.9
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
    "pines": 8, "trees": 10, "birch": 5, "dead": 8, "bushes": 8,
    "rocks": 10, "boulders": 7, "cover": 21, "mountains": 3, "timber": 7, "ruins": 6,
    # Empty in the engine too: neither Synty pack has a medieval building. They come
    # back with POLYGON Knights. Kept in the table so the shapes stay symmetrical.
    "houses": 0, "farms": 0, "towers": 0,
}

COVER_DENSITY = {FOREST: 2.4, PLAINS: 1.7, MARSH: 2.0, MOUNTAIN_PASS: 0.5, ROAD: 0.15}

# Forest at 0.45 rather than the old 0.28. Measured on 1-5, 1812 forest tiles: 0.28 gives
# 489 trees at a median 4.5 m to the nearest neighbour, 0.45 gives 796 at 4.1 m, 0.62
# gives 1088 at 3.6 m. A spruce crown is 0.62 of its height across, so a base-size pine's
# crown is 5.3 m: at 4.1 m the crowns already overlap, which is what the reference shows
# and 4.5 m did not.
#
# 0.62 was tried and rejected on the evidence — the render showed two wagons and one
# troop through a gap and the rest of the column gone, which is the old 0.55 failure
# exactly. Overlapping crowns were the goal and 0.45 reaches them.
DENSITY = {FOREST: 0.45, MOUNTAIN_PASS: 0.18, PLAINS: 0.03, MARSH: 0.06, ROAD: 0.01}

# How much ground a prop actually stands on, as a share of the size it is given.
#
# Reserving one tile per prop was the old rule and it was wrong by a factor of four for
# the worst case. A mountain is `size * 1.2` across and `size` runs to 25 m, so it is a
# thirty-metre cone standing on one four-metre tile — every tree within fifteen metres
# was placed inside it, and the mountainside came out with spruces growing out of the
# rock. What the eye sees is not one prop overlapping another; it is the world not being
# solid.
#
# The numbers are the widest horizontal extent each kind is drawn at, halved. They
# belong here rather than in the renderer because placement is what has to respect
# them; the drawing merely has to stay inside.
PROP_FOOTPRINT = {
    "mountains": 0.60,   # cone at size * 1.2
    "houses": 0.58,      # roof at size * 1.15
    "boulders": 0.55,    # measured across already, so most of its width is footprint
    "rocks": 0.50,
    "ruins": 0.45,
    "trees": 0.43,       # canopy at size * 0.85
    "birch": 0.30,       # a thinner crown than the round broadleaf
    "pines": 0.31,       # widest whorl at size * 0.62
    "bushes": 0.45,
    "towers": 0.25,
    "timber": 0.20,
    "dead": 0.12,
    "farms": 0.50,
    "cover": 0.0,        # grass. Anything may stand in grass.
}

# Kinds placed before everything else, because they are the ones big enough to swallow
# what is already there. Order matters and did not used to: the scatter walks tiles in
# index order, so a mountain reaching tile 500 could not un-place the pine put on tile
# 450 twenty tiles earlier.
BULKY_KINDS = ("mountains",)

# What claims exclusive ground, and what merely stands on it.
#
# Stone and masonry claim it: nothing may grow out of a rock. Growing things do not,
# because a wood is things touching. The rule used to be expressed as a size — anything
# past a tile's width had to find its whole footprint clear — and that worked only for
# as long as nothing green could reach a tile's width. The moment spruces were given
# their real range a 14-metre one had a 4.5-metre canopy, crossed the threshold, and the
# checker started reading two touching crowns as a tree growing out of a rock.
CANOPY_KINDS = ("trees", "pines", "birch", "dead", "bushes", "cover", "patches")


def prop_footprint(kind: str, size: float) -> float:
    """Radius in metres that nothing else may stand inside."""
    return size * PROP_FOOTPRINT.get(kind, 0.35)


# Above this radius a prop must find its whole footprint clear before it is placed,
# not merely the tile it was aimed at. Below it, overlap is what a forest looks like:
# spruce canopies touch, and forcing a tile of air around every tree would give an
# orchard.
FOOTPRINT_CHECK_METRES = TILE_SIZE


def _footprint_clear(grid: TileGrid, occupied: set, x: float, z: float,
                     radius: float) -> bool:
    """Whether a prop of this size can stand here without something already inside it.

    Checking only the centre tile is what let a mountain land eight metres from a
    watchtower and swallow it: the tower had reserved its own ground, but the mountain
    only ever asked about the one tile under its middle.
    """
    span = int(radius / TILE_SIZE) + 1
    cx, cz = int(x / TILE_SIZE), int(z / TILE_SIZE)
    limit = radius * radius

    for ty in range(cz - span, cz + span + 1):
        for tx in range(cx - span, cx + span + 1):
            if not grid.in_bounds(tx, ty):
                continue
            dx = (tx + 0.5) * TILE_SIZE - x
            dz = (ty + 0.5) * TILE_SIZE - z
            if dx * dx + dz * dz <= limit and grid.to_index(tx, ty) in occupied:
                return False

    return True


def _reserve(grid: TileGrid, occupied: set, x: float, z: float, radius: float) -> None:
    """Marks every tile the prop's own body covers, not just the one it was placed on."""
    if radius <= 0.0:
        return

    span = int(radius / TILE_SIZE) + 1
    cx, cz = int(x / TILE_SIZE), int(z / TILE_SIZE)
    limit = radius * radius

    for ty in range(cz - span, cz + span + 1):
        for tx in range(cx - span, cx + span + 1):
            if not grid.in_bounds(tx, ty):
                continue
            dx = (tx + 0.5) * TILE_SIZE - x
            dz = (ty + 0.5) * TILE_SIZE - z
            if dx * dx + dz * dz <= limit:
                occupied.add(grid.to_index(tx, ty))


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


# --- Wildlife (docs/GDD.md §3.5) ---------------------------------------------------
#
# Mirrors Arna.Sim.Wildlife. Deer, foxes and boar graze over the level and scatter when
# the caravan closes or a fight starts. They cannot be killed and are worth nothing: the
# moment an animal can be felled it becomes a resource, and a player who stops the
# caravan to hunt deer is playing a different game.

FOX, DEER_FEMALE, DEER_MALE, BOAR = 0, 1, 2, 3
WILDLIFE_NAMES = ["Fox", "DeerFemale", "DeerMale", "Boar"]

WILDLIFE_COUNT = 34

# Chansen att en skogsruta accepteras alls. Se Arna.Sim.Wildlife.ForestShare: att räkna
# djur mätte fel sak — hälften av de synliga stod under ett lövverk som döljer dem helt
# för en kamera 35° ovanför, och ett djur ingen ser är inte glest utan frånvarande.
WILDLIFE_FOREST_SHARE = 0.35
WILDLIFE_SPOOK_RADIUS = 26.0
WILDLIFE_BATTLE_RADIUS = 55.0
WILDLIFE_FLEE_SECONDS = 4.5
WILDLIFE_FLEE_SPEED = 11.0
WILDLIFE_GRAZE_RADIUS = 6.0


@dataclass
class WildAnimal:
    kind: int
    home: tuple
    position: tuple


def wildlife_sites(level: LevelMap) -> List[WildAnimal]:
    """Where the animals graze. Deterministic, and the same choice the engine makes."""
    grid = level.grid
    rng = DeterministicRandom(level.seed ^ 0x1EAF)
    animals: List[WildAnimal] = []

    groups = [grid.to_coords(spawn.tile) for spawn in level.encounters.enemies]

    for _ in range(WILDLIFE_COUNT * 40):
        if len(animals) >= WILDLIFE_COUNT:
            break

        tile = rng.range_int(0, grid.tile_count)
        x, y = grid.to_coords(tile)

        if not grid.is_passable(x, y):
            continue
        if int(grid.tiles[tile]) == FORD:
            continue
        if tile in (level.start_index, level.goal_index):
            continue
        # A fox grazing inside a bandit camp is a joke, and worse, a tell: an animal
        # where no animal would be marks the group as surely as a flag would.
        if any((gx - x) ** 2 + (gy - y) ** 2 <= 25 for gx, gy in groups):
            continue
        if int(grid.tiles[tile]) == FOREST and not rng.chance(WILDLIFE_FOREST_SHARE):
            continue

        home = tile_centre(grid, tile)
        animals.append(WildAnimal(_pick_wildlife(int(grid.tiles[tile]), rng), home, home))

    return animals


def _pick_wildlife(terrain: int, rng: DeterministicRandom) -> int:
    """Deer in the open, foxes and boar under cover. Roughly true, and it reads."""
    if terrain == FOREST:
        return FOX if rng.chance(0.45) else BOAR
    if terrain == MARSH:
        return BOAR
    return DEER_FEMALE if rng.chance(0.5) else DEER_MALE


# --- Circling crows (docs/GDD.md §3.5) ---------------------------------------------
#
# The strongest of the soft signals, and route drawing made it more important than it
# was: it is read before the line is drawn rather than during the run, so it is one of
# the few things that shapes the decision itself.
#
# What it says is deliberately vague: a flock means a group somewhere near, never a
# group on that tile. Same rule the ruin follows, for the same reason — a signal must be
# information, not the answer sheet.

# Tiles a flock stands for.
#
# §3.5 said twenty, and twenty says nothing. With sixteen groups on a sixty-four tile
# map, 96 % of the ground already has a group within twenty tiles, so "there is one
# within twenty" is true almost everywhere by accident and a player who ignored the
# crows entirely would be right just as often. Measured over nine levels:
#
#     radius   3     4     5     6     8    10    12    15    20
#     covered 12%   20%   30%   39%   56%   71%   79%   89%   96%
#
# Six is where the signal starts being one. A random piece of ground has a group within
# six tiles 39 % of the time; a flock says 80 % — a real update, which is the whole test
# for whether a signal is worth reading. It is also about the size of a group's own
# territory (TERRITORY_MIN is six), so "crows over that wood" means "you would be inside
# somebody's reach around there", which is the truthful reading rather than a coincidence.
CROW_HINT_TILES = 6

# Never nearer than this to *any* group, so a flock cannot double as a marker for one.
# Vaguer than the ruin's three-tile offset from a trap field is precise, which is the
# right order: a trap you walk onto is more punishing than a group you can see coming.
CROW_MIN_TILES = 3

# Chance a given group is announced at all.
#
# Not all of them, and this is the load-bearing part: if every group had a flock, the
# number of flocks would be the number of groups, and counting them would hand over the
# level's whole order of battle for free.
CROW_PER_GROUP = 0.5

# Share of flocks standing over nothing. §3.5 puts it at 20 %, and false positives are
# the point — a signal that is always right is not a signal, it is a map.
CROW_FALSE_SHARE = 0.20


@dataclass
class CrowFlock:
    """Birds circling a piece of ground, and whether anything is actually under them."""

    tile: int
    truthful: bool


def crow_sites(level: LevelMap) -> List[CrowFlock]:
    """Where the crows circle. Deterministic from the level seed."""
    grid = level.grid
    groups = level.encounters.enemies
    rng = DeterministicRandom(level.seed ^ 0x0C0F)

    flocks: List[CrowFlock] = []
    taken: set = set()

    positions = [grid.to_coords(spawn.tile) for spawn in groups]

    def nearest_group(x: int, y: int) -> float:
        return min((math.hypot(gx - x, gy - y) for gx, gy in positions), default=math.inf)

    def free(tile: int) -> bool:
        if tile in taken:
            return False
        x, y = grid.to_coords(tile)
        if not grid.is_passable(x, y) or int(grid.tiles[tile]) == FORD:
            return False
        # Distance from every group, not just the one this flock belongs to. Checking
        # only its own let a flock land on a different group's tile — measured at zero
        # tiles away, which is a marker and not a hint.
        return nearest_group(x, y) >= CROW_MIN_TILES

    for spawn in groups:
        if not rng.chance(CROW_PER_GROUP):
            continue

        gx, gy = grid.to_coords(spawn.tile)
        for _ in range(12):
            angle = rng.range_float(0.0, 2.0 * math.pi)
            reach = rng.range_float(CROW_MIN_TILES, CROW_HINT_TILES)
            nx = int(round(gx + math.cos(angle) * reach))
            ny = int(round(gy + math.sin(angle) * reach))

            if not grid.in_bounds(nx, ny):
                continue
            tile = grid.to_index(nx, ny)
            if not free(tile):
                continue

            # Check the claim after rounding, not before. `reach` is drawn below the
            # radius but snapping to a whole tile can push it past — measured at 6.1 to
            # 6.3 tiles against a claim of 6, which is a flock quietly lying.
            if math.hypot(nx - gx, ny - gy) > CROW_HINT_TILES:
                continue

            taken.add(tile)
            flocks.append(CrowFlock(tile, True))
            break

    # Then the lies. Placed where nothing is within the radius they claim, so a false
    # flock is genuinely false rather than accidentally right.
    wanted = int(round(len(flocks) * CROW_FALSE_SHARE / (1.0 - CROW_FALSE_SHARE)))
    for _ in range(wanted):
        for _ in range(24):
            tile = rng.range_int(0, grid.tile_count)
            if not free(tile):
                continue

            if nearest_group(*grid.to_coords(tile)) <= CROW_HINT_TILES:
                continue

            taken.add(tile)
            flocks.append(CrowFlock(tile, False))
            break

    return flocks


def ruin_sites(level: LevelMap) -> List[int]:
    """Ground that shows a caravan came to grief: near a trap field, never on one.

    "Never on one" is the whole of the tell. A ruin says be careful here; a ruin
    standing on the trap says step there instead, and hands over the position the
    detection system exists to keep hidden.

    It used to say so and not do it. The offset is drawn from [-3, 3] in both axes,
    which includes (0, 0), and nothing checked the trap tiles — so a ruin marked a
    trap exactly on 1 of 9 sites on 1-5.
    """
    traps = level.encounters.traps
    if not traps:
        return []

    mined = {trap.tile for trap in traps}
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

            site = level.grid.to_index(nx, ny)
            if site in mined:
                continue

            sites.append(site)
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

    # Two passes over the same ground, and the order is the fix. The scatter walks tiles
    # in index order, so a mountain reaching tile 500 cannot un-place the pine put down
    # on tile 450 twenty tiles earlier — the big thing has to claim its ground first or
    # the small things grow out of it.
    for pass_index in range(2):
        bulky = pass_index == 0
        pass_rng = DeterministicRandom(seed ^ (0x5EED10 + 0x9E37 * pass_index))

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
            if not pass_rng.chance(density * density_scale):
                continue

            choice = _pick(terrain, pass_rng)
            if choice is None:
                continue
            if (choice[0] in BULKY_KINDS) != bulky:
                continue

            if _scatter(props, grid, pass_rng, choice, i, height_scale, spread=1.4,
                        occupied=occupied):
                placed += 1

    _place_ground_cover(props, grid, rng, clear, occupied, height_scale, density_scale)
    _place_shoreline(props, grid, rng, occupied, height_scale)
    return props


def _pick(terrain: int, rng: DeterministicRandom):
    """Mirrors Arna.View.TerrainDecorator.Pick — a weighted draw, shares in a column.

    The proportions come from the reference pictures. Forest is a spruce forest with
    other things in it, and a fifth of it is the shrub layer whose absence made the old
    one read as trunks standing in a lawn.
    """
    roll = rng.range_float(0.0, 1.0)

    def one(kind, size, by_width=False):
        n = SET_SIZES[kind]
        if n <= 0:
            return None
        return (kind, size, by_width, rng.range_int(0, n))

    if terrain == FOREST:
        if roll < 0.44: return one("pines", PINE_HEIGHT)
        if roll < 0.58: return one("trees", TREE_HEIGHT)
        if roll < 0.68: return one("birch", TREE_HEIGHT)
        if roll < 0.88: return one("bushes", BUSH_HEIGHT)
        if roll < 0.96: return one("rocks", ROCK_HEIGHT)
        return one("timber", TIMBER_WIDTH, True)

    if terrain == MOUNTAIN_PASS:
        if roll < 0.26: return one("mountains", MOUNTAIN_HEIGHT)
        if roll < 0.50: return one("boulders", BOULDER_WIDTH, True)
        if roll < 0.86: return one("rocks", ROCK_HEIGHT)
        return one("pines", PINE_HEIGHT)

    if terrain == MARSH:
        if roll < 0.52: return one("dead", DEAD_TREE_HEIGHT)
        if roll < 0.74: return one("bushes", BUSH_HEIGHT)
        if roll < 0.90: return one("rocks", ROCK_HEIGHT)
        return one("pines", PINE_HEIGHT)

    if terrain in (PLAINS, ROAD):
        if roll < 0.34: return one("rocks", ROCK_HEIGHT)
        if roll < 0.56: return one("bushes", BUSH_HEIGHT)
        if roll < 0.68: return one("boulders", BOULDER_WIDTH, True)
        if roll < 0.84: return one("trees", TREE_HEIGHT)
        if roll < 0.94: return one("birch", TREE_HEIGHT)
        return one("pines", PINE_HEIGHT)

    return None


def _scatter(props, grid, rng, choice, tile, height_scale, spread,
             occupied: Optional[set] = None) -> None:
    kind, size, by_width, _model = choice
    cx, cz = tile_centre(grid, tile)
    x = cx + rng.range_float(-spread, spread)
    z = cz + rng.range_float(-spread, spread)
    ground_y = grid.surface_elevation(x, z) * height_scale

    yaw = rng.range_float(0.0, 360.0)

    if kind in TREE_KINDS:
        low, high = TREE_JITTER_LOW, TREE_JITTER_HIGH
    elif kind == "dead":
        low, high = DEAD_JITTER_LOW, DEAD_JITTER_HIGH
    else:
        low, high = JITTER_LOW, JITTER_HIGH
    scaled = size * rng.range_float(low, high)
    radius = prop_footprint(kind, scaled)

    # Canopy neither claims ground nor checks for it. Keeping it out of the reserved
    # set has a second effect worth having: grass and ferns may now grow under a tree,
    # where before the tree's own footprint kept the floor bare beneath it.
    if occupied is not None and kind not in CANOPY_KINDS:
        if radius > FOOTPRINT_CHECK_METRES and not _footprint_clear(grid, occupied, x, z, radius):
            return False
        _reserve(grid, occupied, x, z, radius)

    props.append(Prop(kind, x, z, ground_y, scaled, yaw, by_width))
    return True


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
            _scatter(props, grid, rng, choice, i, height_scale, spread=1.9,
                     occupied=occupied)
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
            _scatter(props, grid, rng, choice, i, height_scale, spread=2.0,
                     occupied=occupied)
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
            _place(props, grid, tile, rng, choice, height_scale, occupied)
            placed += 1

            # Dead trees around it: a cart alone is too small to read from map height.
            for _ in range(2):
                dead = ("dead", DEAD_TREE_HEIGHT, False, rng.range_int(0, SET_SIZES["dead"]))
                _scatter(props, grid, rng, dead, tile, height_scale, spread=2.6,
                                 occupied=occupied)
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

        # An empty set places nothing, the way an empty PropSet does in the engine.
        # Houses, farms and watchtowers are empty on purpose: neither Synty pack has a
        # medieval building, and they come back with POLYGON Knights.
        def one(kind, size, by_width=False):
            n = SET_SIZES[kind]
            return None if n <= 0 else (kind, size, by_width, rng.range_int(0, n))

        if terrain == ROAD and rng.chance(0.035):
            choice = one("houses", HOUSE_HEIGHT)
        elif terrain == PLAINS and _near_road(grid, x, y, 2) and rng.chance(0.16):
            choice = one("farms", FARM_WIDTH, True)
        elif terrain == MOUNTAIN_PASS and rng.chance(0.012):
            choice = one("towers", WATCHTOWER_HEIGHT)
        elif terrain == FOREST and rng.chance(0.006):
            choice = one("timber", TIMBER_WIDTH, True)

        if choice is None:
            continue

        occupied.add(i)
        _place(props, grid, i, rng, choice, height_scale, occupied)
        placed += 1


def _near_road(grid: TileGrid, x: int, y: int, radius: int) -> bool:
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            nx, ny = x + dx, y + dy
            if grid.in_bounds(nx, ny) and grid.at(nx, ny) == ROAD:
                return True
    return False


def _place(props, grid, tile, rng, choice, height_scale, occupied=None) -> None:
    kind, size, by_width, _model = choice
    cx, cz = tile_centre(grid, tile)
    ground_y = grid.surface_elevation(cx, cz) * height_scale
    yaw = rng.range_int(0, 4) * 90.0
    props.append(Prop(kind, cx, cz, ground_y, size, yaw, by_width))

    # A farm is nine metres across and a ruin five, so the landmarks need their ground
    # reserving for the same reason the mountain does — and they are placed first, so
    # without it the mountain lands on top of them rather than the other way round.
    if occupied is not None:
        _reserve(grid, occupied, cx, cz, prop_footprint(kind, size))
