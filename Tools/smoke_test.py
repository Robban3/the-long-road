#!/usr/bin/env python3
"""Smoke tests for the generator, run against the Python port in `vail_level.py`.

The Unity EditMode tests are the real suite, but they need an editor and this
environment has none. These cover the same ground from the port, which reproduces
the engine's arithmetic exactly (see the module docstring in `vail_level.py`), so a
failure here is a failure there.

Three kinds of check, and the middle one is the point:

  determinism  a level is a recipe plus a seed and nothing else, so the same pair
               must give the same level however much is generated in between
  promises     the numbers the design leans on — the threat budget, the silver
               floor, and above all MIN_ENCOUNTERS
  leakage      information the player is not meant to have, reaching them anyway

    python3 smoke_test.py            # chapter 1, quick
    python3 smoke_test.py --all      # all five chapters, slow
"""

from __future__ import annotations

import argparse
import math
import sys

import vail_level as A
import render_screens

CHAPTER = A.ChapterRecipe()

failures: list[str] = []


def check(name: str, ok: bool, detail: str = "") -> bool:
    if not ok:
        failures.append(f"{name}: {detail}" if detail else name)
        print(f"  FAIL  {name}  {detail}")
    return ok


def level(chapter: int, number: int) -> A.LevelMap:
    return A.generate(CHAPTER.for_level(number),
                      A.DeterministicRandom.seed_for(chapter, number))


def fingerprint(m: A.LevelMap):
    """Everything about a level that a replay would have to reproduce."""
    g = m.grid
    return (tuple(int(t) for t in g.tiles),
            tuple(round(float(h), 6) for h in g.elevation),
            m.start_index, m.goal_index,
            tuple((e.tile, e.kind, e.origin, round(e.territory, 4)) for e in m.encounters.enemies),
            tuple((t.tile, t.kind) for t in m.encounters.traps),
            tuple((c.tile, c.amount) for c in m.encounters.caches),
            tuple(tuple(c.tiles) for c in m.corridors))


def determinism() -> None:
    print("== determinism ==")
    a, b = level(1, 5), level(1, 5)
    check("same seed, same level", fingerprint(a) == fingerprint(b))

    # The one that catches a module-level RNG or a cached grid: generating something
    # else in between must not move the first level a single tile.
    first = level(1, 5)
    level(7, 3)
    check("no state carried between levels", fingerprint(first) == fingerprint(level(1, 5)))

    f1, f2 = A.fly_the_eagle(a), A.fly_the_eagle(b)
    check("same level, same flight",
          f1.revealed_tiles == f2.revealed_tiles and f1.revealed_enemies == f2.revealed_enemies)

    other = A.fly_the_eagle(a, flight=1)
    shared = len(f1.revealed_tiles & other.revealed_tiles)
    check("a second eagle looks elsewhere", shared < len(f1.revealed_tiles) * 0.8,
          f"{shared} of {len(f1.revealed_tiles)} tiles shared")

    # Scouting is a read. If flying moved anything, a player could scout to change
    # the level rather than to learn it.
    before = fingerprint(a)
    A.fly_the_eagle(a)
    A.fly_the_eagle(a, flight=2)
    check("flying does not mutate the level", fingerprint(a) == before)


def promises(chapters: range) -> None:
    print("== promises ==")
    worst_recorded = 99

    for chapter in chapters:
        for number in range(1, 11):
            m = level(chapter, number)
            recipe = CHAPTER.for_level(number)
            grid, layout = m.grid, m.encounters
            tag = f"{chapter}-{number}"

            placed = ([e.tile for e in layout.enemies]
                      + [t.tile for t in layout.traps]
                      + [c.tile for c in layout.caches])

            check(f"{tag} everything on the map",
                  all(0 <= t < grid.tile_count for t in placed))
            check(f"{tag} nothing stacked",
                  len(set(placed)) == len(placed),
                  f"{len(placed) - len(set(placed))} tiles carry two things")
            check(f"{tag} nothing on impassable ground",
                  all(grid.is_passable(*grid.to_coords(t)) for t in placed))
            check(f"{tag} start and goal clear",
                  m.start_index not in placed and m.goal_index not in placed)

            check(f"{tag} within the threat budget",
                  layout.total_points <= recipe.enemy_budget,
                  f"{layout.total_points} of {recipe.enemy_budget}")
            check(f"{tag} enemies from the unlocked pool",
                  all(e.kind in recipe.enemy_pool for e in layout.enemies))
            check(f"{tag} territories clamped",
                  all(A.TERRITORY_MIN - 1e-3 <= e.territory <= A.TERRITORY_MAX + 1e-3
                      for e in layout.enemies))
            check(f"{tag} silver validated", layout.silver_validated)

            # The promise the route-drawing mechanic rests on.
            check(f"{tag} min encounters", layout.min_encounters >= A.MIN_ENCOUNTERS,
                  f"worst sampled route met {layout.min_encounters}, "
                  f"promise is {A.MIN_ENCOUNTERS}")
            worst_recorded = min(worst_recorded, layout.min_encounters)

            flight = A.fly_the_eagle(m)
            extent = grid.width * A.TILE_SIZE
            check(f"{tag} flight stays on the map",
                  all(-A.TILE_SIZE <= p[0] <= extent + A.TILE_SIZE
                      and -A.TILE_SIZE <= p[1] <= extent + A.TILE_SIZE
                      for p in flight.path))
            check(f"{tag} found groups stand on flown ground",
                  all(layout.enemies[i].tile in flight.revealed_tiles
                      for i in flight.revealed_enemies))
            check(f"{tag} one eagle never finds the whole level",
                  len(flight.revealed_enemies) < len(layout.enemies)
                  or len(layout.enemies) < 4,
                  f"{len(flight.revealed_enemies)} of {len(layout.enemies)}")
            covered = len(flight.revealed_tiles) / grid.tile_count
            check(f"{tag} eagle coverage in range", 0.10 <= covered <= 0.45,
                  f"{covered:.0%}")

    print(f"  worst recorded min_encounters: {worst_recorded}")


# What an unseen route is allowed to meet. One below the promise, and that number is
# measured rather than chosen: the placer proves its case over the 32 routes it
# sampled, and a route drawn between them can always come out a group short. Chasing
# that last group by raising the repair target does not work — at 7 the failures move
# to different levels, at 8 generation time triples and levels start failing
# validation outright. Four groups is still a level with a game in it; two was not,
# and two is what this caught.
UNSEEN_FLOOR = A.MIN_ENCOUNTERS - 1


def unseen_routes(chapters: range, count: int = 60) -> None:
    """The recorded guarantee is measured against 32 routes the placer chose. This
    measures it against routes it never saw, which is the case that ships."""
    print("== the promise against routes the placer never sampled ==")

    for chapter in chapters:
        for number in (1, 5, 10):
            m = level(chapter, number)
            band = A.build_band(m.grid, m.start_index, m.goal_index)
            if band is None:
                continue

            rng = A.DeterministicRandom(m.seed ^ 0x5EED1)
            routes = A.sample_routes(m.grid, band, [], rng,
                                     m.start_index, m.goal_index, count=count)
            met = [len(A.met_groups(m.grid, r, m.encounters)) for r in routes]

            check(f"{chapter}-{number} unseen route meets enough",
                  min(met) >= UNSEEN_FLOOR,
                  f"recorded {m.encounters.min_encounters}, "
                  f"worst of {len(routes)} unseen was {min(met)}, "
                  f"mean {sum(met) / len(met):.1f}")


def route_drawing(chapters: range) -> None:
    """The line the player draws, and what the map is able to tell them about it."""
    print("== route drawing ==")

    baseline = []

    for chapter in chapters:
        for number in (1, 5, 10):
            m = level(chapter, number)
            grid = m.grid
            tag = f"{chapter}-{number}"

            straight = A.solve_route(grid, m.start_index, m.goal_index)
            check(f"{tag} a route with no waypoints reaches the goal", straight.valid)
            if not straight.valid:
                continue

            check(f"{tag} the route starts at the start and ends at the goal",
                  straight.tiles[0] == m.start_index and straight.tiles[-1] == m.goal_index)
            check(f"{tag} the route is contiguous",
                  all(_adjacent(grid, a, b)
                      for a, b in zip(straight.tiles, straight.tiles[1:])))
            check(f"{tag} the route never crosses impassable ground",
                  all(grid.is_passable(*grid.to_coords(t)) for t in straight.tiles))
            check(f"{tag} no tile is counted twice",
                  len(set(straight.tiles)) == len(straight.tiles))
            check(f"{tag} the terrain tally adds up",
                  sum(straight.tiles_by_terrain) == len(straight.tiles))

            # The risk reading must come off the terrain and nothing else: a number
            # that knew where the groups were would hand over for free what the eagle
            # is sold for.
            check(f"{tag} the risk reading is a terrain average",
                  min(A.AMBUSH[:6]) - 1e-4 <= straight.ambush_exposure <= max(A.AMBUSH) + 1e-4,
                  f"{straight.ambush_exposure:.2f}")

            # Every crossing of the river is a ford, because there is no other way over.
            check(f"{tag} every crossing is at a ford",
                  all(int(grid.tiles[c]) == A.FORD for c in straight.crossings))

            baseline += [leg.detour for leg in straight.legs]

            # A tap on water puts nothing down.
            water = [t for t in range(grid.tile_count)
                     if not grid.is_passable(*grid.to_coords(t))]
            if water:
                check(f"{tag} a tap on impassable ground places no waypoint",
                      not A.can_place_waypoint(grid, water[0]))

    # The detour threshold has to clear the noise floor. A* on eight-connected ground
    # never walks the crow's line, so an undisturbed leg already reads above 1.0; if
    # the threshold sat inside that spread every route would warn and the warning
    # would mean nothing.
    if baseline:
        check("the detour threshold clears an ordinary leg",
              max(baseline) < A.DETOUR_THRESHOLD * 0.95,
              f"an ordinary leg reads up to {max(baseline):.2f} against a threshold "
              f"of {A.DETOUR_THRESHOLD}")
        print(f"  ordinary leg detour: {min(baseline):.2f} to {max(baseline):.2f}, "
              f"threshold {A.DETOUR_THRESHOLD}")


def _adjacent(grid, a: int, b: int) -> bool:
    ax, ay = grid.to_coords(a)
    bx, by = grid.to_coords(b)
    return max(abs(ax - bx), abs(ay - by)) == 1


def crows(chapters: range) -> None:
    """The soft signal, and whether it is a signal at all (docs/GDD.md §3.5)."""
    print("== circling crows ==")

    flocks = truthful = 0
    distances = []
    ratios = []

    for chapter in chapters:
        for number in range(1, 11):
            m = level(chapter, number)
            grid = m.grid
            tag = f"{chapter}-{number}"
            sites = A.crow_sites(m)
            groups = [grid.to_coords(s.tile) for s in m.encounters.enemies]
            if not sites or not groups:
                continue

            flocks += len(sites)
            truthful += sum(1 for f in sites if f.truthful)
            ratios.append(len(sites) / len(groups))

            for flock in sites:
                fx, fy = grid.to_coords(flock.tile)
                nearest = min(math.hypot(gx - fx, gy - fy) for gx, gy in groups)

                check(f"{tag} no flock doubles as a marker",
                      nearest >= A.CROW_MIN_TILES - 1e-6,
                      f"a flock sat {nearest:.1f} tiles from a group")

                if flock.truthful:
                    distances.append(nearest)
                    check(f"{tag} a truthful flock tells the truth",
                          nearest <= A.CROW_HINT_TILES + 1e-6,
                          f"{nearest:.1f} tiles, claim is {A.CROW_HINT_TILES}")
                else:
                    check(f"{tag} a false flock is genuinely false",
                          nearest > A.CROW_HINT_TILES,
                          f"{nearest:.1f} tiles — something was under it after all")

            check(f"{tag} flocks stand on passable ground",
                  all(grid.is_passable(*grid.to_coords(f.tile)) for f in sites))

    if not flocks:
        return

    false_share = 1.0 - truthful / flocks
    check("about a fifth of the flocks are lying",
          abs(false_share - A.CROW_FALSE_SHARE) <= 0.06,
          f"{false_share:.0%} against a design figure of {A.CROW_FALSE_SHARE:.0%}")

    # The load-bearing one. If every group had a flock, counting flocks would count
    # groups, and the level's whole order of battle would be free.
    check("flocks cannot be counted into groups",
          max(ratios) - min(ratios) > 0.3,
          f"the ratio only ranged {min(ratios):.2f} to {max(ratios):.2f}")

    print(f"  {flocks} flocks, {false_share:.0%} false, truthful ones "
          f"{min(distances):.1f}-{max(distances):.1f} tiles from their group, "
          f"flocks per group {min(ratios):.2f}-{max(ratios):.2f}")


def solid_world(chapters: range) -> None:
    """Nothing stands inside anything else.

    Found by looking at a screenshot, not by a test, which is the reason this exists.
    A mountain is drawn thirty metres across and was placed on one four-metre tile with
    one tile reserved, so every spruce within fifteen metres was put inside the rock.
    What that looks like is not two props overlapping — it is the world not being solid,
    and one such tree undoes a whole hillside of careful scenery.
    """
    print("== solid world ==")

    # Grass and shoreline pebbles are flat and may lie under anything; a tuft of grass
    # at the foot of a boulder is not a fault.
    FLAT = {"cover", "shore"}

    for chapter in chapters:
        for number in (1, 5, 10):
            m = level(chapter, number)
            props = [p for p in A.decorate(m.grid, m.seed, keep_clear=None,
                                           height_scale=14.0, max_props=2200)
                     if p.kind not in FLAT]
            tag = f"{chapter}-{number}"

            worst = None
            for big in props:
                # Stone only. The test used to say "anything with a footprint past four
                # metres", which named the mountains for as long as nothing green could
                # reach four metres — and indicted the forest the moment spruces were
                # given their real size range. What must never happen is a tree growing
                # out of a rock; two crowns touching is what a wood is.
                if big.kind in A.CANOPY_KINDS:
                    continue

                radius = A.prop_footprint(big.kind, big.size)
                if radius < 4.0:      # only things large enough to swallow a tree
                    continue
                for other in props:
                    if other is big:
                        continue
                    gap = math.hypot(other.x - big.x, other.z - big.z)
                    if gap < radius * 0.8:
                        worst = f"a {other.kind} stood {gap:.1f} m inside a {big.kind} " \
                                f"of radius {radius:.1f} m"

            check(f"{tag} nothing grows out of the rock", worst is None, worst or "")


def leakage(chapters: range) -> None:
    """Information the player is not meant to have, reaching them anyway."""
    print("== leakage ==")

    for chapter in chapters:
        for number in (1, 5, 10):
            m = level(chapter, number)
            grid = m.grid
            tag = f"{chapter}-{number}"

            # A ruin is a tell that a trap field is near, deliberately offset so it
            # says "be careful here" and not "step there". On the tile itself it
            # hands over the position the whole detection system exists to hide.
            traps = {t.tile for t in m.encounters.traps}
            sites = A.ruin_sites(m)
            check(f"{tag} no ruin stands on a trap",
                  not (traps & set(sites)),
                  f"{len(traps & set(sites))} of {len(sites)} ruins mark a trap exactly")

            # The three corridors are the generator's own working. Clearing scenery
            # along them draws them as lanes through the forest, and the planning
            # overlay cannot hide that: it takes colour out, not geometry.
            #
            # The invariant is exact, so it is asserted exactly rather than measured:
            # the planning map must decorate identically to the run. Anything else and
            # the forest the player plans against is not the forest they drive through
            # — LevelRunner keeps nothing clear, so neither may the map.
            planning = A.decorate(grid, m.seed,
                                  keep_clear=render_screens.planning_keep_clear(m, False),
                                  height_scale=22.0, max_props=2600, density_scale=2.2,
                                  sites=sites)
            running = A.decorate(grid, m.seed, keep_clear=None, height_scale=22.0,
                                 max_props=2600, density_scale=2.2, sites=sites)
            check(f"{tag} planning map is dressed like the run",
                  planning == running,
                  f"{len(planning)} props on the map against {len(running)} on the run")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--all", action="store_true",
                        help="all five chapters rather than just the first")
    args = parser.parse_args()
    chapters = range(1, 6) if args.all else range(1, 2)

    determinism()
    promises(chapters)
    unseen_routes(chapters)
    route_drawing(chapters)
    crows(chapters)
    solid_world(chapters)
    leakage(chapters)

    print(f"\n{len(failures)} failure(s)")
    for line in failures:
        print(f"  - {line}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
