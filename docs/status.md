# Where the project stands

Working notes, kept current so that picking the work up on another machine does not
mean rediscovering what was already settled. The design documents next to this one
say what the game is meant to be; this one says what it currently is.

Last updated when route drawing replaced the three-corridor choice and threat moved
onto the whole crossable band. The four-pack asset swap in §8 is planned but not yet
done; the landscape pass on chapter 1 came before both.

---

## 1. State in one paragraph

The simulation is complete and tested: terrain generation, route drawing, caravan
movement, detection, traps, combat, silver and upgrades, all deterministic from a seed
and all covered by tests. The route is the player's now — they draw it through the
country rather than picking one of three the generator drew, and threat is placed
across the whole crossable band to suit. The presentation has just
been through a full pass — the ground is lit and textured, the scenery is textured,
the cast stands on the ground at the right size, and the planning map is a top-down
render of the real world rather than a grid of coloured squares. What is missing is
everything between levels: no camp, no shop, no UI beyond a debug readout, and no
build for either phone.

---

## 2. Running it without opening the editor

Unity is a GUI binary on Windows, so `&` does not block. Use `Start-Process -Wait`
or the run will overlap itself and fight over the lock file.

Clear `Temp/UnityLockfile` first if a previous batch run crashed, but only when no
Unity process is alive.

    Unity.exe -batchmode -quit -projectPath <project> -logFile <log> -executeMethod <method>

Useful methods, all on `Arna.Editor.ArnaSetup`:

| Method | What it does |
|---|---|
| `SetupProject` | Rebuilds the render pipeline, materials and the planning scene |
| `SetUpPlayScene` | Rebuilds the scene you press Play in |
| `CaptureLevelPreview` | Renders the planning map to a PNG |
| `CapturePlayScene` | Renders the play view; `-arnaSteps` advances the simulation first |
| `RestyleModelMaterials` | Extracts textures and materials from the packs and takes the gloss off |
| `ReportFolderDimensions` | Measures every model in `-arnaModelDir`: size and which axis is up |
| `ReportMaterialTextures` | Prints which texture each material ended up with |
| `ReportRigBones` | Dumps a character's bone hierarchy |
| `CaptureCharacters` | Stands the whole cast in two rows and photographs it |
| `ReportActorFit` | Prints how tall each actor came out and where it stands |
| `ColourUntexturedMaterials` | Paints the materials whose texture this project does not have |

The report methods exist because every one of them settled a question that had
already cost hours of guessing. They are cheap to run and worth running first.

Tests:

    Unity.exe -batchmode -projectPath <project> -runTests -testPlatform EditMode -testResults <xml>

### The half of Unity's compile that can be had here

`typecheck.sh` builds Sim, Gen and the tests — everything that compiles without an engine
— and that leaves **View, App and Editor unchecked**. Those are three quarters of the code
that draws anything, and for this whole project the only compiler that had ever seen them
was the one inside Unity, on another machine, after a push. A method defined twice in
`ArnaSetup` cost a Safe Mode dialog and two round trips to find, and nothing here could
have caught it.

`unitycheck.sh` hands every one of those files to Roslyn with **no references at all**.
Most of what comes back is noise — a thousand *type not found* for `UnityEngine` — but the
errors that do not depend on knowing what a `GameObject` is come back too:

| | |
|---|---|
| CS0111 | a method defined twice |
| CS0101 | a type defined twice |
| CS0128 | a local declared twice |
| CS1002, CS1513, CS1525 | the syntax family |

It runs in four seconds and it is not a substitute for Unity's own compile — anything that
needs to know a type is still invisible. It is the half that can be had before pushing, and
that half is where this class of mistake lives.

### Running the C# without Unity

    apt-get install -y dotnet-sdk-8.0
    ./Tools/csharp/typecheck.sh            # everything
    ./Tools/csharp/typecheck.sh Formation  # one fixture

`Arna.Sim` is compiled without engine references on purpose and `Arna.Gen` only depends
on it, so both build with a plain compiler. The EditMode tests build against the NUnit
stand-in beside that script — and `Runner.cs` reflects over the fixtures and **executes**
them.

**It used to assert nothing**, which caught a test that would not compile and missed
every test that would fail. The first run with real assertions found **seven failures
sitting on `main`**. Four came from one bug — the column halting for anything at all,
including an archer band twenty metres off, so that neither side could disengage and
1-5 ended with the caravan destroyed at seven percent of the route (§8 of the GDD has
the fix). A fifth was arithmetic: an attacker halted at its reach *plus* the engagement
slack while a troop reached its reach plus the same slack, leaving a swordsman two
tenths of a metre short of the wolf biting him — a figure swinging at air, which is
also what it looked like on screen.

Three remain, listed in §4. The lesson is the mono lesson below, one notch further in:
a check that reports success without checking is worse than no check.

**Roslyn rather than mono, and the reason is worth keeping.** Mono's compiler cannot
parse C# local functions, which four of the test files use, and it stops at the first
parse error before typing anything — so a file it could not read masked real errors
everywhere else. A wrong constructor argument in `LevelRunTests` reached a push that
way, after a run that reported nothing, and new tests in `CombatTests` were never
checked at all because that file was one of the skipped four. A tool that quietly
checks less than it appears to is worse than no tool.

**What it still cannot cover: `View`, `App` and `Editor`.** They use UnityEngine, so
only the editor builds them, and every compile error that has reached a push today has
been in one of those three. The most recent was a local named `heading` inside a method
that already had one — legal in most languages, CS0136 in C#.

A probe is the quickest way to turn a failure into a number: add a `[Test]` that
prints rather than asserts, run it by name, and delete it. That is how the 228-second
siege above stopped being a theory — *outcome=CaravanLost elapsed=228s travel=3s
progress=0.07*, with an untouched archer band standing 13.5 m away — and how the fix
was confirmed: *outcome=Arrived elapsed=96s travel=51s progress=1.00*.

Before this existed, the same thing was done by compiling a small `Main` against the
subset a feature reached and running it under `mono`. That is how the wildlife numbers
below were measured. The runner makes it unnecessary.

This matters more than it looks. Two thirds of the game's logic lives in those two
assemblies, and without this there is no way to know whether an edit compiles — let
alone works — until somebody opens the editor. A constant and a method sharing the name `SampleRoutes` sat
in `EncounterPlacer` through several commits for exactly that reason — nothing here
could build it, so nothing caught it. `View`, `App` and `Editor` still need Unity.

### Without Unity at all

`Tools/arna_level.py` is `Arna.Sim` and `Arna.Gen` transcribed to Python, and
`Tools/render_screens.py` draws both views from it with its own rasteriser, z-buffer
and shadow map. That is what a machine with no engine on it — a cloud session, a
laptop without the project installed — can still look at:

    cd Tools && python3 render_screens.py --chapter 1 --level 5 --out ../docs/screenshots

The same port is what the generator is tested against here, since the EditMode suite
needs an editor:

    cd Tools && python3 smoke_test.py --all

It checks determinism, the numbers the design leans on, and information the player is
not meant to have. It found four real bugs the first time it was run; see §4.

It was validated against this document's own recorded numbers for 1-5 — 59 % corridor
overlap and a fastest route of 94.4 — which is how we know it generates the same levels
and not merely similar ones. Getting there needed its pathfinder to round to single
precision the way C# float does; in double precision 1-5 came out at 67 % overlap
instead of 59 %, because the cautious route is searched over ground thick with
equal-cost tiles and the rounding decides which of two identical paths A* keeps.

**Those two numbers have since moved, and on purpose.** The generator now re-rolls a
level whose encounter promise it cannot keep (§4), so 1-5 is no longer decided on the
first attempt but on the sixth, and it is a different level: fastest 77.6, worst-pair
overlap 67 %. The port and the engine were changed together and still agree by
construction, but the cross-check above is now historical — it validated the
transcription at the commit where both were the old code. Re-running it needs an
editor, so anyone who has one should: generate 1-5 in Unity and check it against
`python3 render_screens.py --chapter 1 --level 5`.

What it cannot show is the art: every FBX and texture in the repository is a Git LFS
pointer, so scenery is drawn as procedural stand-ins at the sizes `TerrainDecorator`
gives them. The country is right, the dressing is a sketch. See
`docs/screenshots/README.md`.

---

## 3. What works

- Deterministic generation: seed 1005 is level 1-5 on every machine, every time.
- Three corridors per level with a validated difference between them — used as the
  generator's quality gate and for par time, never shown to the player.
- Threat placed across the whole crossable band, with a guard on every ford and a
  territory around every group, then verified against sampled routes. Measured over
  chapter 1 against forty routes the placer never saw: no route met fewer than three
  groups, the average was five to six, and every level stayed inside its budget.
- Route drawing in the simulation: `RoutePlanner` stitches up to six waypoints into one
  caravan route with terrain-weighted A* per leg, and reports travel cost, terrain
  shares and which leg is impassable.
- The scouting ability: `ScoutingAbility.Fly` sends an eagle wandering over the planning
  map on a curve seeded from the level, and reports the ground it saw and the groups it
  passed over. Ten seconds on a narrow trail lifts 17–25 % of the overlay and finds two
  to five of the twelve groups. Deterministic from the seed, so the flight cannot be
  re-rolled by restarting the level.
- The full run loop: movement, terrain speed, detection, traps, combat, silver.
- Ground: lit, shadowed, textured at two tiling scales, colours blended across
  tile corners so the world is continuous rather than tiled.
- Scenery: textured trees, grass, ferns, rocks; stones along every waterline;
  landmarks placed where they would stand rather than scattered.
- Planning map: a top-down orthographic render of the real level. It still draws the
  three corridors as ribbons, which is now wrong — §5 step 1 replaces them with the one
  route the player draws.
- Weapons fitted to the right hand on every troop and enemy that needed one.

---

## 4. Known problems, worst first

**The suite is green: 194 tests, none failing.** The four that had been red for weeks are
resolved below, and none of the four was an implementation bug — three were design calls
waiting to be made and one was a test measuring the wrong thing.

**Traps are no longer inert, and the fix was placement.** Level 1-8 used to place fourteen
of them; a run down its fast corridor revealed two and triggered none. A trap has a
three-metre trigger and no territory — unlike a group, which comes to you — so scattered
over a band of three thousand tiles it is scenery. Three of the game's own answers had no
question with it: the scout who reveals traps at 10 m, the sapper who disarms, the
shieldbearer who absorbs — "three different answers to the same problem" (GDD §7.2) — and
the marching order in which order 0 walks in first and order 3 crosses last.

They now go where the country is narrow, and narrowness is *measured*. Both travel fields
were already built, so a tile's detour is `FromStart + FromGoal - Fastest`: zero on an
ideal crossing, growing as you go round. Cut the crossing into 48 slices by depth, count
the tiles per slice whose detour is inside 8 %, and that count is how many ways past there
are at that depth. The smallest counts are the fords, the passes and the dry line through
a bog.

Throats alone were not enough, and how they failed is worth keeping: a throat is narrow
ground on the *ideal* crossing, and the three corridors a player is offered are generated
lines that only roughly follow it. Traps landed in the right stretch of country and a tile
off the road — on 1-8 with a lone shieldbearer not one was so much as seen. A three-metre
trigger on a four-metre tile does not forgive being one tile out. So the score ranks by how
many corridors cross a tile first, and everything else decides ties within that rank.

**Two thirds at the throats, one third strewn** (`ThroatShare`). All-scattered is what
there was and it does not fire. All-at-the-throats fires and reads as *arranged*: a player
learns within three levels that the ford is always mined, which turns a hazard into a
checklist. The strewn third is what stops that becoming a rule — and it gets its own share
of the allowance rather than the throats' leftovers, because handing it the remainder makes
the total depend on how much legal ground the throats happened to have. That took 1-7 from
three survivable routes to none between one run and the next with the allowance unchanged.

`TrapBudgetShare` came down from 0.25 to 0.18 with it. The share had been tuned against a
placement that *could not spend it* — the old scatter shared one occupancy set with
everything else at three tiles' spacing and ran out of ground before it ran out of
allowance. Laying them properly spends the lot, so the same number bought far more trap
and less enemy, and chapter 2 began shipping levels that could not put five groups on
every drawn route. A constant tuned as a product breaks silently when either factor moves;
this is the third time that has happened here.

**Threat follows cover, not speed.** `ThreatFollowsFastGround` asserted that enemies sit
on ground faster than the map average and failed on nine levels of ten. The measurement was
right and the rule was wrong. GDD §3.1 is deliberately two-humped — ambush weight runs
forest 1.5, ford 1.3, road 1.2, marsh 1.0, pass 0.9, plains 0.8, while speed runs road
1.25, plains 1.0, forest 0.70, pass 0.60, ford 0.50, marsh 0.45. The two most dangerous
terrains in the table are the forest and the ford and **both are slow**: what draws an
ambush is cover. Threat measured against speed therefore *must* come out below the map
average, and it did, 0.69 against 0.76 — the table working rather than failing.

The band's own weight was `speed * AmbushWeight`, and those factors pull against each
other: multiplied by speed the forest scored 1.05 against the open plain's 0.80, so groups
drifted out of the cover the table sends them to. It is `AmbushWeight` alone now. Speed
against safety is not lost with it — the road carries that by itself, fastest ground on the
map and second most dangerous on it, which `TheRoadIsTheFastestGroundAndAlsoDangerous` now
holds on its own.

Two smaller things fell out of that. The repair loop is the last thing to touch a layout and
sorted its candidates on emptiness alone, so on a level needing several repairs it undid the
scatter's cover-seeking; it weights by cover now. And `SafeEndCost` was only ever checked as
travel cost — eight of it is two and a half tiles on a road — so a group could stand four
tiles from the start and satisfy a rule written in distance. There is a straight-line ring
as well now.

**How many ways through a level owes you depends on what the level is for.**
`EveryLevelOffersAWayThroughForAnEscortedCaravan` wanted two everywhere and failed on 1-6
and 1-8 — both in the escalation band, with the eight levels outside it passing. GDD §8.1
gives every chapter the same shape: 1 intro, 2-4 variation, 5 twist, **6-9 escalation**, 10
boss. One hard way through late in a chapter is the escalation doing its job; buying two
there costs either a toothless enemy budget on those levels or a squad budget raised across
all ten to mend two. The threshold is two outside the band and one inside it, every level
still owes at least one, and `ChapterOneStillOffersARealRouteChoiceOverall` holds the other
end — twenty survivable routes across the chapter's thirty, or it is a corridor with
scenery either side.

**A test that measures health at the goal is not measuring traps.** Both trap tests
asserted that the troop on point finishes hurt. A priest heals between fights, so on a level
the escort wins comfortably the van arrives at full health however many traps it walked
into — six fired on 1-8 and it came in at 660 of 660. One of those tests *passed* for a
while because wolves were hurting the troop instead, which is worse than failing: it
reported on the trap system while measuring something else. `LevelRun.TrapDamageToTroops`
and `TrapDamageToWagons` are running totals, and a running total cannot be healed away.

The other half of that test was a one-man escort. 1-8 destroys it at seven percent of the
route, twenty metres from the nearest pit, and it then reports on a trap system it never
reached.

**A fight is now most of a level's wall clock.** 1-1 arrives in 110 s of which 71 are
spent halted. Par is measured against `TravelSeconds` so the stars are unaffected, but
two thirds of a level standing still is a thing to look at rather than a number to
accept.

**Props stood inside each other, and it took a screenshot to notice.** The decorator
reserved one tile per prop however big the prop was. A mountain is drawn about `size *
1.2` across and size runs to 25 m, so a thirty-metre rock stood on one four-metre tile
and everything within fifteen metres was placed inside it — spruces growing out of the
rock face. What that reads as is not two props overlapping; it is the world not being
solid, and one such tree undoes a hillside of careful scenery.

Three parts to the fix, and the second was found by the test written for the first:

- Reserve the ground a prop actually covers, not the tile it was aimed at. In Unity
  the radius is read off the instance's own bounds after `ModelScaling` has fitted it,
  which is better than a table: the table knows what was asked for, the bounds know
  what came out.
- Place the bulky things first. The scatter walks tiles in index order, so a mountain
  reaching tile 500 cannot un-place the pine put down on tile 450.
- A big prop must find its **whole footprint** clear, not just its centre tile.
  Otherwise a mountain lands eight metres from a watchtower and swallows it — the tower
  had reserved its ground, but the mountain only asked about the one tile under its
  middle. Only asked above a tile's width: below it, overlap is what a forest looks
  like, and a tile of air around every tree would give an orchard.

`Tools/smoke_test.py` now has a `solid world` section. It exists because a screenshot
caught this and no test did.

**Fixed by the first smoke run, recorded so they are not reintroduced.** Four bugs,
two of them information leaks, all four present in the engine and not only in the port:

- *The repair loop livelocked.* `EncounterPlacer` moves a group onto whichever sampled
  route met too little, and picked the idlest group to move — but the group it just
  moved is the idlest group on the next pass, because it went somewhere only one route
  reaches. Traced over forty passes on 2-5, the same band of raiders moved forty times
  while the worst route stayed pinned at four. All twelve repairs were being spent
  walking one group in a circle, and the `MinEncounters` promise broke on 10 of 50
  levels, down to 2. The loop now scores itself, undoes a move that does not help, and
  offers each donor several landing spots instead of one.
- *The generator never re-rolled on the broken promise.* It re-rolled up to twelve
  times on whether the three corridors differ from each other — which stopped being a
  question when the player was handed a pen — while `MinEncounters` was not a criterion
  at all. It is now, through `EncounterLayout.EncountersValidated`.
- *A ruin could stand on the trap it is meant to only hint at.* The offset is drawn
  from [-3, 3] in both axes, which includes (0, 0), and nothing checked the trap tiles.
  One of nine sites on 1-5 marked a trap exactly — handing over the position the whole
  detection system exists to hide.
- *The three corridors leaked through the scenery.* Props were cleared along them so
  the ribbons would read, which drew them as lanes through the forest at a third of the
  surrounding density. The planning overlay cannot hide that, because it removes colour
  and not geometry — so hiding the ribbons hid nothing. Clearing is now tied to drawing
  the ribbons, and `LevelPreview.ShowCorridors` defaults off.

**The planning map is legible again, in the renderer.** Removing the cleared lanes cost
the map its structure — start and goal were one tinted tile each, and the goal on 1-5
sat under a mountain — so start, goal and every ford are now drawn in screen space over
the finished picture, and the route with them. Nothing in the world can cover them, and
the clearing is gone for good: it existed so a ribbon would not sit under a spruce, and
the ribbon no longer can.

This is `Tools/render_screens.py` only. The same three marks have to be built on the
Unity planning screen when it is written (§5), and the sizes and colours settled here
are the starting point: endpoint discs at 1.1 % of screen width, ford spans at 0.8 %,
each with its own dark rim so it carries contrast onto both meadow and canopy.


**The generator lays no roads.** `TerrainType.Road` exists in the terrain table and
nothing ever writes it. Road is the fastest terrain in the game, so the
speed-against-safety trade-off is missing a pole, and houses and fields are placed
on and beside roads, so neither has ever appeared on a map. See §6 for why the
obvious fix does not work on its own.

**Corridor overlap runs high — and now means something else.** Level 1-5 shares 67 %
of its tiles between its two closest corridors, and five levels in fifty share 100 %.
That last figure is not a broken gate: `IsMeaningfulChoice` asks that *some* pair of
corridors differ, while the overlap figure reports the *worst* pair, so two coinciding
routes and a third that diverges passes — correctly, since the fast way and the safe
way being the same road is a real thing for a map to say. The corridors are no longer shown to anyone,
so this is not a broken level any more; it is a map that affords fewer distinct
crossings than it should, which is what the quality gate is measuring. Worth keeping an
eye on, no longer worth blocking on.

**The wagons are bought, not built.** A wagon pack settles this — see §8 — and the
hand-built ones are gone with the scripts that made them. `Assets/_Project/Models`
still holds `Wagon.fbx` and `WagonTreasure.fbx`, and `VisualLibrary` still points at
them; they stay until the pack's wagons are wired in their place, because deleting
them first leaves the caravan with nothing to draw.

**The pirate pack has no texture in this project.** Every model in it draws from one
shared atlas image and that image was never here — the pack arrived as meshes and
materials pointing at nothing, and `RestyleModelMaterials` cannot extract what was
never embedded. The props that used it have moved to packs that carry their colours
in their materials. The two bandits cannot move: they are the only medieval figures
in reach, so they are painted a flat colour each by `ColourUntexturedMaterials`. A
flat figure is legible at the distance this game is played and plainly unfinished up
close. Finding the atlas, or replacing both models, is the real fix.

**Mountains and buildings are untextured.** They come from the flat-coloured RTS
pack and sit beside textured trees. The stylized nature pack has no landforms and no
buildings, so replacing them needs another pack. This is the problem the pack swap
in §8 is meant to end.

**No camp, no shop, no UI.** The economy is implemented and has nowhere to be spent.

**Neither phone can be built for.** Only Windows Standalone support is installed.

---

## 5. What to do next, in order

1. **The planning screen.** Everything under it is built: `RoutePlanner` solves the
   route, `RouteResult` carries the readout, and placement no longer assumes corridors.
   What is missing is the screen — tap to place a waypoint, one ribbon instead of three,
   travel time and terrain shares, markers on the fords, and a start button locked until
   the route is valid. Until it exists the game's central decision is implemented and
   unreachable.
2. **Swap the asset packs**, and measure before wiring — see §8. It settles the
   untextured landforms, the missing buildings and the character roster in one move,
   and it decides what is left of the wagon problem.
3. **Cast the three wagons** out of the wagon pack — a covered wagon for supply, a
   heavier cart for war, a box or merchant wagon for treasure — and retire
   `Wagon.fbx` and `WagonTreasure.fbx` once `VisualLibrary` points at the new ones.
4. **Roads.** The budget objection is settled: threat is spread over a band by how fast
   the ground is, so a road makes its own tiles more dangerous instead of concentrating
   a corridor's whole share onto a shorter line. Lay them and re-measure.
5. **The planning map's frame** — border, compass, title. Cheap, and it is much of
   what makes a picture read as a map.
6. **The scouting overlay in Unity.** The flight itself is built and measured; what is
   missing is the map under a grey layer that lifts along the trail, the markers on what
   the bird found, and the gold that pays for it. `Tools/render_screens.py --eagle` draws
   exactly what it should look like.
7. **The camp between levels**, so silver and upgrades have somewhere to happen.
8. **Android and iOS build support**, and a build on a real device. The frame rate
   target has never been measured on hardware.

---

## 6. Things already established, so they are not rediscovered

**The enemy budget rewards speed with danger, and does not scale.** Budget is shared
between corridors in inverse proportion to travel cost, so a faster corridor is
given more enemies. Anything that makes a route faster therefore makes it
disproportionately deadlier. Laying roads was tried: corridor overlap on 1-5 fell
from 59% to 42% and the fastest route from 94.4 to 86.6 — both improvements — but
level 1-6 went from winnable to unsurvivable on all three routes, because the larger
budget landed on less ground.

That objection is now spent. Threat is no longer shared between corridors at all — it
is spread over the crossable band by how fast each tile is (§5 of
`content-pipeline.md`), so a road makes its own tiles more dangerous rather than
concentrating a corridor's whole share onto a shorter line. Roads are a scenery and
balance job again, not a blocked one.

**A model's origin is not its feet, and placing it by its origin buries it.** The
fitting pass at spawn works out how far a model's origin sits above its own lowest
point and stands it on the ground; anything that assigns a position afterwards
throws that away. Most packs are within a few centimetres and it never shows. The
knight is a third of a metre out and walked the road buried to the knee. `Place` in
`RunVisuals` reapplies the offset, and everything that moves a marker goes through
it.

**A model is scaled by everything in its file, props included.** The knight arrives
holding a two-hander whose point hangs past his boots, so measuring him with it
made him a head shorter than the troops beside him. `ActorModel.Unsized` names
meshes that are drawn but not counted. `ActorModel.Hide` is the other half of the
same problem: some files carry a second character (Ernest, in the pirate captain's)
or a prop the game does not want (Henry's lute), and no rule distinguishes a
stowaway from a sword — so they are named.

**A skinned mesh's renderer bounds are an animation-sized box, not the figure.**
The knight's report 1.97 m across for a figure two thirds that wide. Baking the
posed mesh gives the truth, but the baked vertices land in a bone-relative space
that is not the renderer's — measuring through `renderer.transform` reported him as
fifty metres tall and scaled him to a speck. Naming the offending mesh in the
casting is cheaper and clearer than getting that transform right.

**Outside play mode an animator that has never been bound evaluates nothing.** A
headless capture was photographing the pose each file happens to be saved in.
`Rebind()` and one `Update(0)` at spawn fixes it; `AdvanceAnimators` alone does not,
because there is nothing bound for it to advance.

**Unity's first batch run after a script edit compiles and quits without running
the method.** The log ends after the assembly reload with no output and the exit
code is 1. Run the same command again — the second pass is the one that works —
and wait for `Unity.exe` to leave the process list between runs, or the next
invocation finds the project locked and writes no log at all.

**The packs disagree about which way is up.** The RTS scenery is Z-up and miniature
— a whole tree is 0.72 × 0.45 × 0.93. The stylized nature pack and the medieval
village pack are both Y-up and already in metres. Same author, different answers, so
measure with `ReportFolderDimensions` rather than infer. The up axis is a property of
the pack and lives on `PropSet`, not on the biome.

**Widest axis does not mean wrong way up.** A fern measures 9.05 × 2.69 × 8.49 and a
pebble 0.50 × 0.10 × 0.37. Both are correct. Anything wider than it is tall must be
fitted by footprint rather than by height, or it is scaled by its thin dimension and
comes out enormous.

**Extracting materials from an FBX leaves their textures behind.** Extract textures
first, or every model renders pure white. Deleting extracted materials to retry
leaves the importers pointing at files that no longer exist, and then everything
renders magenta.

**Texture names do not match material names.** `Leaves_NormalTree` belongs to
`Leaves_NormalTree_C`, and `Leaves_Pine` to `Leaf_Pine_C` — singular where the
material is plural. Matching on a shared tail fails for every leaf in the pack.
Words are compared instead, and a texture is only accepted when every word in its
name appears in the material's.

**One leaf atlas is shared across the nature pack** — green, blue, orange, purple
and pink in a single image, picked by each model's UVs. Some plants are genuinely
violet. They are not broken and are not deleted; they belong to a biome that wants
them.

**Corner colour blending needs opposite rules for water and roads.** Both are one
tile wide, so neither ever gets a corner surrounded by four of itself, and plain
averaging dilutes both away. Water takes a corner on a majority — letting any single
water tile claim one swallowed the fords, which are the only tiles of a river the
caravan may cross. A road takes its corners outright, because there is nothing for a
road to swallow and half a tile either side is what keeps a track continuous.

**Sinking water without skirts punches holes through the terrain.** The pale blue
bands that used to run along every river were the sky showing through. Depth is
spread across shared corners now, so the banks slope and meet.

**Headless captures must compile shaders synchronously.** A capture taken shortly
after a shader edit renders with a variant that has none of its keywords, which
looks exactly like a shader that does not work.

**Vertical surfaces are lit by the ambient equator colour.** Set it well below the
sky and every cliff face and tree trunk goes to near black, because ambient is the
only light reaching them.

**Anything on the ground must sample `TileGrid.SurfaceElevation`.** The rendered
surface is interpolated between corner heights, and a tile's own elevation is a
different number. Using the wrong one leaves props hovering or buried.

---

## 7. Asset licensing

Everything in the repository is CC0, from Quaternius and Poly Haven. That is
deliberate: the repository could be made public without stripping it.

Asset Store packages may be used commercially but not redistributed, so they cannot
be committed — a purchased pack has to live locally and be ignored by git. Synty was
torn out of the history for exactly this reason.

Two packages offered earlier were repackaged from a piracy site and were not
installed. Assets for tabletop mapmaking are also worth checking carefully: the
largest libraries licence for print and virtual tabletops but explicitly exclude
integration into software.

The swap in §8 changes what this section can promise. Four purchased packs cannot be
committed, so the repository stops being a thing that runs after a clone: it becomes
code plus a shopping list. What stays committed stays CC0 — the medieval
village pack is kept for exactly the props §8 lists, and nothing about it changes here. That is a fair trade for art that carries the game,
but it is a decision rather than a side effect, and the consequence is worth stating
plainly — the code can stay public, the game it renders cannot be assembled from the
repository alone.

---

## 8. Swapping the asset packs

### The nature half is done

POLYGON Nature is in and drawing the whole landscape. What was matched is the pack's
own marketing shot, which the design took as its target: conifers dominating in many
sizes and two greens, a minority of rounded broadleaf, grey faceted rock, and a floor
thick enough that no bare ground shows between the trees.

| | Was | Is |
|---|---|---|
| Broadleaf | `CommonTree_1..5` | `SM_Tree_Round_01..05` |
| Conifers | `Pine_1..5` | `SM_Tree_PolyPine_01..03`, `_Sparse_01..02`, `SM_Tree_Pine_01..02` |
| Dead / marsh | `DeadTree_1..5` | `SM_Tree_Dead_01..03`, `SM_Tree_Pine_Dead_01`, `SM_Tree_Swamp_01..02` |
| Rock | `Rock_Medium_*`, `Pebble_*` | `SM_Rock_01..04`, `_Rounded_01`, `_Small_01..02` |
| Ground cover | Stylized Nature MegaKit | `SM_Plant_Grass_01..05` ×2, `Fern_01..03`, `Bush_01..03`, `Undergrowth_01`, `Flowers_01`, `Mushrooms_01..02` |
| Mountains | **Quaternius RTS** | `SM_Terrain_Mountain_01..03` |

Four things are worth keeping about how it went.

**`Load` only ever looked for `.fbx`.** True of every pack in the project until this one.
Synty ships prefabs and the FBX beside them carries no materials, so an fbx-only reader
would have found the shape of a forest painted in Unity's default grey — the exact
failure the *previous* pack swap was made to fix. It now tries `.prefab` first.

**The mountains came home.** They were borrowed from the RTS kit because the old nature
kit had rocks and no landforms — a borrowed ridge on a borrowed skyline, and the seam
showed. POLYGON Nature has three, pale and snow-capped, which is what the reference puts
on its horizon.

**Two decisions survived the change of pack, because their reasons did.** Grass listed
twice over and everything showier once: the floor in the reference is grass with things
in it, not a flowerbed with grass around the edges. And no `SM_Plant_PurpleFlower_01` —
the last pack taught that a violet plant at any weight takes over the middle distance,
because violet is the one colour nothing else on a hillside is.

**Trees now vary in size far more than anything else.** A quarter either way gave spruces
between 6.8 and 10.6 m — a hedge, evenly clipped. In the reference the smallest conifer
is about a third the height of the largest, so the tree family alone runs 0.55 to 1.7,
which against a pine's 8.5 m is 4.7 m to 14.5 m. Rocks, grass and buildings keep the
quarter.

**The forest is at 0.45 props per tile, up from 0.28.** It was tuned *down* to 0.28 from
a first attempt at 0.55 because a thousand nine-metre trees closed the canopy over the
caravan — and two things have changed underneath that number since. Trees now run 4.7 m
to 14.5 m rather than all standing at nine, and canopy no longer reserves ground, so the
small ones fill in between the big ones instead of pushing them apart. Measured on 1-5,
where the forest is 1812 tiles:

| Density | Trees | Median gap to the nearest tree |
|---|---|---|
| 0.28 | 489 | 4.5 m |
| **0.45** | **796** | **4.1 m** |
| 0.62 | 1088 | 3.6 m |

A spruce crown is 0.62 of its height across, so a base-size pine's crown is 5.3 m: at
4.1 m the crowns already overlap, which is the thing the reference shows and 4.5 m did
not.

**0.62 was tried first and rejected on the evidence.** The argument for it was that the
old objection had expired — trees now run 4.7 m to 14.5 m rather than all standing at
nine, and canopy no longer reserves ground, so the small ones fill in between the big
ones instead of pushing them apart. Plausible, and wrong: the render at 0.62 showed two
wagons and one troop through a gap with the rest of the column gone, which is the 0.55
failure exactly. Overlapping crowns were the whole goal and 0.45 reaches them, so 0.62
buys nothing the picture wanted and costs the column.

**Two renders were wasted getting there**, and the reason is worth keeping: the script
that set the density for them matched `COVER_DENSITY` instead of `DENSITY`, so they
varied the grass and left the trees where they were. Both looked plausibly denser, which
is how they survived being looked at. The counts in the table come from the module
directly and were never affected.

It is still the triangle budget's largest single line — 796 trees against 489 — against
the 250k limit in docs/technical-design.md. The trees share one atlas material so the
draw calls batch; the triangles do not.

`Assets/Quaternius/StylizedNature` is deleted — nothing referenced it once the swap
landed. Two dimension reports were still pointing at it and at the RTS trees, and had
been measuring models the game stopped drawing; both now point at what is actually on
the ground. `Report Selected Folder Dimensions` also searches `t:Prefab` now, or it
reports an empty folder for the pack that supplies the entire landscape.

### The scenery layer was rebuilt, not patched

Everything that dresses the world now comes from Synty, and the old kit is deleted
rather than left underneath. `Assets/Quaternius/UltimateFantasyRTS` (1212 files) and
`MedievalVillage` (396) are gone; what is left of that folder is characters and their
gear — Knight, ModularMen, PiratePack, the cavalry horse, and RPGItems for the bow and
axe — until the army pack arrives.

**Three tree species instead of two, and a shrub layer.** Two species read as two
species; three read as a wood, and the birch was in the pack already and unused. Its
pale trunk is the only light vertical line in a forest otherwise made of dark ones. The
shrub layer matters more: without it a forest is trunks standing in a lawn, which is
what the old one was, and every reference for this game has foliage at about head
height.

**`Pick` is a weighted draw now, not a chain of coin flips.** The shares *are* the
design and the old form hid them — four nested ifs, where working out the proportion of
one species to another took a pencil. They read down a column now:

| | Forest | Pass | Marsh | Plains / road |
|---|---|---|---|---|
| Conifer | 44 % | 14 % | 10 % | 6 % |
| Broadleaf | 14 % | — | — | 16 % |
| Birch | 10 % | — | — | 10 % |
| Shrub | 20 % | — | 22 % | 22 % |
| Rock | 8 % | 36 % | 16 % | 34 % |
| Boulder | — | 24 % | — | 12 % |
| Landform | — | 26 % | — | — |
| Dead timber | — | — | 52 % | — |
| Fallen wood | 4 % | — | — | — |

Boulders are a new set and a separate job from rocks: a pebble is texture, a boulder is
something the eye steers round. Sized across rather than up, because they are slabs and
fitting a slab by height turns it into a menhir.

**Houses, farms and watchtowers are empty, and that is the honest state.** Neither Synty
pack has a medieval building — PolygonNature has none and PolygonGeneric's are modern
city blocks — so an empty set places nothing and the map is wilderness until POLYGON
Knights. Which is what the reference picture is, and arguably what a road through the
provinces should have been all along.

The trap tell changed with it. It was one cart borrowed from the village pack; it is now
a mix of skeleton, dropped chest, grave, dead fire and broken masonry. The reference
shows a wrecked *wagon*, which neither Synty pack has — the wagon pack does, and that is
the obvious next move.

### The skyline, and the fog that hid it

Both reference pictures put large pale peaks well beyond the ground being played on, and
this game had no horizon at all: the world ended at the map edge with a flat sky colour
behind it, which reads as the edge of a board rather than as distance.

`Horizon` is a ring of 22 peaks on a 320 m radius around a 256 m map, 185 m tall with a
wide jitter — a range of identical peaks is a saw blade. Evenly spaced and then nudged
rather than placed at random angles: random angles clump, and a clump on a skyline is a
gap somewhere else, which reads as the range having been forgotten on one side. They are
the only set drawn from both Synty packs, because a silhouette three hundred metres off
has no detail left to disagree about and a range wants variety along its length more
than it wants one author.

**It did not work, and the reason is worth more than the feature.** The peaks were in
the scene, correctly sized and correctly placed, and every pixel of them was the colour
of the air. Linear fog reaches *full* sky colour at its end distance, and the end
distance was 320 m — the exact radius of the ring. The world visually stopped there.

Two attempts were wasted before that was found, and both failed in ways that looked like
the answer: the peaks were raised from 130 m to 185 (no change), then the renderer's
stone colour was darkened away from the sky (no change). Only the second failure was
suspicious enough to go looking, and the fog was in `render_screens.py` mirroring
`ArnaSetup`, which is the whole reason the port exists.

The fog ends at 520 m now. A peak at 300 m keeps about half its own colour — a pale blue
silhouette, which is what a mountain twenty minutes' walk away actually looks like. The
cost is real: the map's own far edge, 250 m off, goes from 28 % of its colour to 60 %, so
the middle distance carries less haze than a value tuned for a 300-metre landscape gave
it. The landscape is 640 metres deep now.

**The range is shoulders with summits standing out of them**, not a row of cones — and
getting there included one change that had to be taken straight back out. Five `Hill`
prefabs went into the mix on the argument that a ring of pure peaks reads as a sawtooth.
The argument was right about shape and wrong about everything else: a hill in that pack
is grass-covered, and a green mass a hundred metres tall on the skyline is not a distant
hill, it is a wall of lawn. From a play camera near the map's corner it filled the frame.
The shoulders come from the broader `Background_Mountain` prefabs instead.

**The height came down from 185 m to 105**, and the reason it was ever 185 is worth
keeping. It was raised from 130 because at 130 exactly one peak found a gap in the
canopy — but that was never a height problem. The range was invisible because the fog
ended at 320 m and the ring stood at 320. Raising the peaks changed nothing, the raise
was left in, and the result was a value chosen against one camera and never checked from
another: from the map's corner the nearest peak is 192 m away, and at 185 m tall it tops
out 37° above the eye. At 105 it is 19° from the corner and 11.5° from the middle of the
map, and still stands well clear of a treeline that tops out around 3°.

The renderer needed the same lesson and got it late. It drew a horizon peak as one
`CONE_FINE` with a smaller cone of snow on top — a party hat, and not what Unity draws,
where the model is an irregular faceted landform. It is a broad rocky shoulder with two
or three off-centre summits now, varied per peak from its own position so no two in the
ring are the same mountain. The snow caps had to be nested properly as well: at 0.40 of
the span they overhung a cone that is only 0.27 of the span wide at that height, so every
summit wore a brim. A cone of radius `span` has radius `span * (1 - y / tall)` at height
`y`; a cap starting at 0.60 of the height may be at most 0.40 of the span across.

**None of it is visible at the default camera, and that is geometry rather than tuning.**
The play view sits 46 m back and 32 m up: 34.8° of pitch with a 50° field, so the frame
spans from 9.8° *below* horizontal to 59.8° below. A horizon is at 0°. Nothing on it can
enter that frame at any size or any distance. The skyline belongs to the player who
tilts the camera down toward it — `CameraOrbit` allows 12°, where the frame reaches 13°
above horizontal — and it is one of the few things the orbit control actually pays out.

### The marsh has its own plants

A fen dressed in the meadow's grass and ferns is a meadow that happens to slow you down.
`MarshPlants` — reeds, lilypads, swamp growth — is drawn on marsh tiles and on the ring
of tiles around them, because a bog does not stop at a tile boundary: the ground goes
soft before it goes wet, and that margin is where the reeds are. Diagonals count in the
margin, since the one thing a bog's edge is not is a right angle. Measured on 1-5: 573
marsh plants against 3435 ordinary tufts.

The marsh scatter changed with it — 30 % marsh plants where it used to be the generic
shrub, and swamp trees and stumps added to the dead-timber set.

### Three things that were flat, and one rule

**Lilypads paved the fen with three-metre discs stacked on each other.** They were in
`MarshPlants`, ground cover is fitted by *height*, and a lilypad has almost none — so
fitting one to 0.7 m of height multiplied the whole model by whatever that took and the
width went with it. From above it read as craters on craters.

They are back, in `BiomeDecor.Lilypads`, which is the one set in the decorator measured
**across**: `LilypadWidth = 1.2 m` with the usual quarter either way, so 0.9 m to 1.5 m.
A single pad is 20–30 cm and the pack ships clusters as well as singles, so the number is
for the set rather than for a leaf; three or four fit inside a four-metre tile. They go
on marsh tiles only, never on the soft margin the reeds have — a pad floats, and one
lying in the grass beside a bog is the same category of wrong as a reed in open water.
`LilypadShare = 0.22` of a fen tile's cover comes out a pad. Note the pack's own spelling:
`SM_Plant_Lillypad_*`, two Ls, and loading them by the name they ought to have had is a
silent miss that looks exactly like the decision to leave them out.

**Ground patches stacked for the same reason from the other direction.** A 7.5 m disc
covers about three and a half four-metre tiles, so at the plains rate of 0.22 a tile they
covered 77 % of the ground, and on a road tile at 0.55 with the plan's density scale,
265 % — every patch on top of two others. They are 5.2 m now and keep their own reserved
ground, checked over the whole footprint rather than the centre tile: two patches
overlapping by most of their area while both believe they are alone is the same bug the
mountains had.

**The eagle was the same rule read from the other end** — most of her vertical extent is
wing dihedral, so fitting her by height let the bind pose decide the wingspan.

The rule, written where it will be read: **ground cover is fitted by height, so nothing
flat may go in it.** Anything flat is measured across instead — `GroundPatches` for what
lies on the ground, `Lilypads` for what floats on it.

### The eagle is white, and it is not the model's fault

`RestyleModelMaterials` **extracts** textures embedded inside an FBX. The eagle's are not
embedded: cgtrader ships the model in one archive and five PNGs in another, so what
arrives in Unity is a mesh whose material slots point at nothing — and a slot pointing at
nothing renders pure white, which is the failure that method's own comment warns about,
reached from the opposite direction.

`Arna > Wire Loose Textures` builds a URP material from the loose files beside a model
and remaps it onto the importer, so it survives a reimport in a way a material dragged
onto a prefab does not. Albedo, normal and opacity are matched by filename. Alpha
clipping goes on when an opacity map is found: feathers, leaves and hair are cut out of
flat geometry, and without clipping a bird has rectangular wings.

It defaults to `Assets/ThirdParty/Eagle` and takes `-arnaModelDir` for anything else,
which the next cgtrader asset will need.

**The wings not beating is a separate fault with a separate cause** — four of them, in
fact, and they look identical from the outside. That is the lesson, more than any of the
four: *animated but not moving* has a handful of unrelated causes and no symptom that
tells them apart, so the report has to name all of them at once. `Refresh Scene Assets`
prints textures, controller, every clip's loop flag and the avatar in one line.

The first: `SpawnActor` attaches no animator when there is no controller to attach, and
said nothing — so a bird holds its bind pose while it moves, which looks like the flight
being wrong. `LevelPreview` now warns, and `Refresh Scene Assets` reports the controller
by name alongside how many of the eagle's material slots have a texture in them. White
and gliding are different faults; guessing between them cost two rounds.

The second: **she was flying on the wrong clip.** The eagle ships four — `Fly_01` 6 s,
`Fly_02` 5 s, `Fly_03` 8.33 s, `Fly_04` 1 s — and they are not interchangeable: some of a
bird's flying is beating and some of it is soaring. Both name lists end in `"Fly"`, both
matched `Fly_01`, and `Fly_01` is a glide. So she was textured, moving, animated, and had
wings that did not beat — three fixes deep into a fault that was none of them.

Length looks like the tell and is not: a one-second clip is probably one wingbeat, but a
six-second one may be six. `AnimatorBuilder.Cadence` measures instead — the sum of every
rotation curve's absolute change, per second of clip, with the root left out, because a
soaring bird banking across the sky moves its root more than a flapping one moves it at
all and counting the root picks exactly the wrong clip. The busiest clip drives the
travelling state, the calmest the idle, which is also right for a bird: hanging on a
thermal is what it does when it is going nowhere. The numbers are logged, so a wrong pick
is visible rather than inferred.

That choice lives in the generated controller asset, not in the scene, so `Refresh Scene
Assets` rebuilds the eagle's controller as well as wiring her textures.

The beat is played back at `FlightSpeed = 0.45`, on flight states only — nothing that
walks is touched, because a troop's stride is tied to how fast the caravan is actually
moving and slowing the clip would put the feet out of step with the ground.

0.45 comes from the bird's size rather than from taste. A fifth off was tried first, was
not enough, and was never going to be: the beat is wrong by a factor, not by a margin.
She is drawn at a ten-metre wingspan against a real eagle's two, because at life size she
is a speck over a 256 m map. Wingbeat frequency falls roughly with the square root of
length for animals of the same shape, so a bird very nearly five times over should beat
about √5 ≈ 2.2 times slower, and 1 ÷ 2.2 = 0.45. The clip was authored for a two-metre
bird and is being watched on a ten-metre one; it was never going to look right at a speed
chosen by eye.

### The fog lifts behind the bird, not before her

The map starts grey — all of it — and only ground the eagle has already flown over comes
out of it. That is the ability, and it was already the *end* state; what was wrong is
that the whole flight's worth of reveal was applied at build time, before she had flown a
metre. The map opened with the answer printed on it and the bird was a decoration
crossing ground that had already told you everything.

`_revealAt` holds, per tile, how far she has flown by the time she is nearest to it —
taken from the flight the simulation worked out, so what the map ends up showing is
exactly what the ability grants. Nearest point rather than first sighting: a tile off to
one side should clear as she draws level with it, not when the edge of her sight first
clips it. A thousand tiles against a couple of hundred path points, once per rebuild.

`RevealLag = 20 m` is half a second at her 40 m/s, and it is the difference between
*trailing* and *travelling with*: ground going clear under the bird reads as the bird
being made of light, ground going clear behind her reads as her having looked at it.

Three details that are each a bug if missed:

- **The lit colours have to be kept.** A mute is not reversible, so `_lit` holds the
  ground as the terrain builder made it and `_shown` holds what is on the mesh; the fog
  is the difference, and lifting it over a tile is four colours copied across.
- **Props are filed by tile** on the way past. A reveal has to find the props on one tile
  out of four thousand and cannot walk six thousand props to do it.
- **The last stretch of the flight would never come due.** `_flown` wraps just short of
  the full length, so `length + lag` is unreachable and the final 20 m stays grey for
  ever. Once she has been round once, everything she flies over is behind her.

Revealed ground stays revealed when the flight loops. A map that re-fogs every twenty
seconds is one you cannot plan on, and the second lap has nothing to add.

**The edge is feathered rather than cut.** A tile used to be seen or unseen, which drew
the flight as a stencil — a chewed hard edge in the shape of the tiles the simulation
happened to mark. Knowledge does not stop at a four-metre boundary; it thins out.
`_clarity` is a value per tile, and `FogFeather = 2.5` tiles carries a revealed tile's
clarity into its neighbours: about ten metres of edge on a map 256 across. Less than that
is a stencil with a soft line drawn on it; much more and the flight stops having a shape,
which is the one thing the player is reading it for.

Clarity only ever rises, and that is what makes it cheap. Each neighbour keeps the best
claim anything has made on it, so a reveal costs a fixed twenty-five tiles rather than a
blur over the whole map every frame.

Props are filed by tile as **renderers**, gathered once while the fog goes on. A reveal
repaints twenty-five tiles and dozens of tiles come due per frame; walking each prop's
hierarchy again for every one of those is thousands of searches and thousands of
allocations per frame, on the editor thread, in the scene that was already the slow one.

The enemy marks follow the same rule and the crows do not. A group is drawn where she
found it; the crows are free and always visible, which is the whole distinction the
design rests on (docs/GDD.md §3.4, §3.6).

### Nine things the packs had and the game did not

All of them were sitting in `docs/synty-inventory.md` marked *unused* or *candidate*.

**Water is now a surface, not a colour.** `SM_River_Plane_01` laid over every water tile,
a tile and a half across so the planes overlap — a sheet that stopped at the tile boundary
would reproduce the staircase it is there to hide. It sits `WaterLift = 0.12 m` above the
bed: level with it, it z-fights; higher, it floats. Flat and level with itself rather than
following the ground, because water finds its own level and a tilted plane is the one thing
that would give the trick away. **This is the only set in the decorator that replaces a tile
rather than dressing one**, and it is the real answer to the blocky watercourses — the reeds
and pads were the fix that could be had without new models.

**The fen is dressed with actual swamp plants.** `SM_Swamp_Root_01..02` and
`SM_Terrain_Swamp_Growth_01..03` were unused in `Terrain/` while the marsh was being dressed
out of the general plant folder with ferns and mushrooms, which grow in a wood.

**Fords have a crossing on them.** `SM_Prop_Bridge_Curved_01`, one per ford rather than one
per tile — a ford is several tiles wide and a bridge on each is a pier. Fords are where the
traps go, because every corridor uses them; they had nothing on them but water you could
somehow walk through.

**Cliffs look like the reason they are impassable.** `TerrainType.Cliff` has existed since
the generator was written and never had a single prop: a patch of differently coloured
ground the player cannot cross, for no visible reason.

**Willows stand beside water and nowhere else.** The scatter puts spruce and oak wherever
the terrain table says forest, which takes no notice of a river running through it. 10 m
against a spruce's 14, because a willow leans out over water rather than up out of a wood.

**The shoreline gets stone a river put there** rather than stone that happens to be near
one — `SM_Rock_Pile_Curved_01..02` are made to follow a waterline.

**Enemy camps.** A tent, a rack, a banner near each group: a band of raiders stood on bare
grass and read as men who happen to be there.

**And one mesh behind the whole horizon.** `SM_MountainSkybox_01` — the inventory has called
it the biggest thing not yet used since the day it was written. The peak ring is 22 draw
calls for something never nearer than 400 m and never seen from the side. The backdrop does
not replace it: it stands **behind** it at 1600 m across, so the near peaks give parallax
against something that does not, which is what makes distance read as distance rather than
as a painted wall. Same grey as the peaks, because two ranges in one colour read as one
range receding.

### The camp is a feint

It is the third soft signal and the one with the strongest claim. Crows are birds that
might be over anything and a bone pile is old, but a standing camp says *men, here,
recently*. **That is exactly why it has to be able to lie** — a camp the player can trust
turns route drawing into route reading.

An abandoned camp is better fiction than a lying bird. The crows lie one time in five and
the story is that birds circle carrion, not soldiers; a camp needs no story at all, because
bands move and the tents they leave are the most ordinary thing in a country full of bands.
So `CampSignal.FalseShare` is **one in three**, the most any of the three signals lies, and
it is still worth reading: random ground has a group within four tiles about a fifth of the
time, and a camp says two thirds. A large update, and one camp in three a feint — enough
that nobody can treat one as proof and stop scouting.

`PerGroup = 0.34` is lower than the crows' half on purpose. **The two signals stack**, and
both marking the same group is not twice the information — it is one group with two signs
on it, and a player who learns to look for the pair gets certainty back through the side
door.

The empty ones are placed where nothing is within the radius a camp claims, because a feint
that happens to have a group behind it is not a feint but a signal that was right by
accident — and it teaches the opposite of the lesson. Two tests hold that: one on the ratio,
one on every empty camp being genuinely empty.

**The camp nearly shipped a third information leak.** The first version pitched the tent on
the group's own tile, on the argument that a camp is what a band lives in. That is wrong in
the way this project has been caught twice: enemies are drawn only once revealed, and a tent
on an unrevealed group hands over the position the detection system exists to hide. It uses
the trap signs' rule now, through the same code — near enough to notice, far enough that
noticing says *be careful* rather than *step here* — which is also truer, since a camp is
where a band sleeps and not where it stands watch. `NoSignalMarksTheThingItIsAboutExactly`
holds both signals at once.

### The water was the one boundary with nothing growing on it

A river is drawn as tiles, so a river running at any angle other than square is a
staircase of four-metre squares. Everywhere else on the map that kind of edge is hidden
under the props growing across it — the forest's boundary is invisible because there are
trees standing on both sides of it — and the water had bare colour on one side and
grass on the other.

Three changes, none of them to the mesh:

- `NextToWater` counts diagonals now. It is the *corners* of a staircase that read as
  blocky, and a four-neighbour margin dresses the flats and leaves every corner bare —
  precisely the wrong half. `NextToMarsh` had learned this already; the water's copy had
  not. It also feeds the shore stones, which get the same benefit.
- **Banks are dressed like a fen's margin**, not like a meadow. Ground beside moving
  water is soft and reeds are what say so.
- **Open water carries lilypads**, and only lilypads: `CoverDensity` has a `Water` entry
  for the first time. A reed standing in the middle of a river is the same category of
  wrong as a lilypad lying in grass. Pads floating over the seam do for the water's edge
  what the trees do for the forest's.

### The skyline is placed by its own edge, not by a radius

The caravan drove into a mountain a second time, and the reason is the same shape as the
first: **a peak is fitted by height and the pack's are much wider than they are tall**,
so how far its foot sticks out from the point it stands on is a fact about the model, not
about the radius the ring was given.

At `HorizonRadius = 320` with the jitter band, the nearest peak stood 282 m out and
reached back to within 197 m of the centre. The map's own corner is 181 m out, so it just
about held — until the ground grew a skirt and reached 249 m at the corners. The range
was then standing on the map's own apron, with the road running underneath it.

`PlaceHorizon` measures each peak's footprint after fitting it and pushes it out to
whichever is greater: the radius it would like, or `furthest ground + its own footprint +
HorizonClearance`. The radius became a preference and the measurement became the floor,
so it holds whatever the pack ships and whatever the skirt becomes later. It also breaks
the ring's evenness, which is a gain: a range is not a fence.

`TerrainMeshBuilder.SkirtWidth` is a constant rather than an argument for the same
reason. Two things have to agree about how far the ground goes — what draws it, and what
keeps the skyline off it — and they disagreed once.

### Mountains are on the skyline and nowhere else

The caravan drove into one. A twenty-metre hill standing on a tile the column has to walk
over is a wall in the road, and no amount of route-clearing fixes it: `driveLine` refuses
props on the line itself, but a mountain placed on a tile *beside* it overhangs the road
anyway.

They are off the map entirely — `BiomeDecor.Mountains` is gone, not emptied. **A pass is
the ground between the mountains**, which is boulders, scree and the trees that manage on
it, so that is what dresses `MountainPass` now; the mountains' 26 % went to the boulders,
which are what reads as high country from inside it. The range lives in `Horizon`, three
hundred metres beyond the map's edge, where it is looked at rather than walked into.

**And it is painted flat grey** (`SkylineGrey`, a pale cold blue-grey). Distant ground is
not a smaller copy of near ground: air between you and it scatters the light, so it loses
its colour and moves toward the sky's. That is why a range twenty miles off is blue-grey
however green its trees are — and why the pack's grass-covered mountains read as a green
wall at the edge of the field rather than as distance. Painting them out is not a
stylisation, it is the one cue that says how far away they are.

One flat material for the whole range, made in code rather than loaded. At three hundred
metres a texture is smaller than a pixel, so it costs bandwidth to deliver noise — and
one shared material means the twenty-two peaks batch instead of pulling the pack's atlas
twenty-two times.

The two-pass scatter stays even though nothing is bulky any more: the castle and the keep
are coming and they are exactly that — a thing that decides what may stand near it rather
than one that fits around what is already there.

### The planning map is dressed differently from the world

`LoadPlanDecor` is `LoadForestDecor` with `Horizon` emptied. Everything else — every
tree, rock, bush, reed and lilypad — is the same set the play view uses, so the map is
the country seen from above rather than a diagram of it.

The skyline comes out because it stands three hundred metres beyond the map's edge, which
is exactly what it is for from inside the world and exactly what is wrong with it on a
map: a ring of peaks around the sheet, drawn outside the ground being read, with nothing
to say about the route.

### The preview kept every generation it had ever built

`_props`, `_routes`, `_markers` and `_eagleRoot` are private fields with no
`[SerializeField]`, so Unity does not serialize them. Opening the scene hands them all
back null while the GameObjects they point at are sitting in it exactly where they were
saved — and the rebuild `OnEnable` asks for then builds a second `Props` beside the first
and leaves it. Every save keeps another generation.

That is the whole of "why is there scenery outside the map and a mountain in the middle
of it": a horizon ring built before the planning view stopped asking for one, orphaned by
a scene load, saved back in by the next thing that saved the scene, and from then on
untouchable by any code change — `horizon: false` cannot remove a prop that no longer
belongs to anything.

`LevelPreview.Clear(name)` removes a build root **by name**, and every stray copy of it,
before each rebuild. Serializing the fields would have mended the reference and not the
strays already in the scene; this mends both. It says how many it cleared, once, so the
next accumulation of this kind is visible rather than inferred from the picture.

The general shape is worth naming: **an editor-time builder must clear by identity in the
scene, not by a reference in the component.** Anything `[ExecuteAlways]` generates lives
longer than the field that made it.

### The editor has no clock in it

`Time.deltaTime` means nothing outside play mode. There is no game loop for it to
measure, and what it returns is whatever the last one left behind. `LevelPreview` was
advancing the bird by that number, which is the exact shape of *motion that lags and
lurches while the editor is keeping up perfectly well* — and it hid behind the wings not
beating, because a gliding bird shows judder far less than a flapping one does.

`Elapsed` reads `EditorApplication.timeSinceStartup` instead, which is a real clock and
the one thing in the editor that can be trusted to say how long something took. A frame
longer than `MaxTick = 0.1 s` is **clamped to it, not thrown away**, and that distinction
cost a round on its own: rejecting long frames looked reasonable, but this scene carries
six thousand props and the editor redraws all of it every tick, so most frames *are*
long. Nearly every one was rejected, the bird crawled, and the fog lifted in patches on
whichever frames happened to be quick. A ceiling slows playback on a slow machine;
a rejection stops it.

Anything else `[ExecuteAlways]` ever animates in the preview has to go through the same
function. This is not specific to the eagle.

The third: **Unity imports every clip with Loop Time off.** A cycle that does not loop
plays once and holds its last frame — so a one-second wingbeat gives one beat and then a
bird gliding for the rest of the level, which is indistinguishable from an animator that
never ran. `AnimatorBuilder.EnsureLooping` switches it on through the *importer*, because
a clip is a sub-asset of the model file and anything written onto it directly is
regenerated away at the next import. Idle, walk and attack only: **death must never
loop** — it is entered from anywhere on a bool that stays true, and a looping death is a
corpse that keeps getting up to die again. A reimport destroys the loaded clip objects,
so everything is chosen again from the new ones afterwards.

The fourth, and the one that was actually watched: **the editor does not run a game
loop.** `[ExecuteAlways]` gets an `Update` when something asks the editor to redraw — a
mouse crossing the scene view, an inspector edit — and nothing asks while you sit and
look at it. So the bird advanced in little jerks whenever the pointer moved and was
frozen the rest of the time, which reads exactly like wings that do not beat.
`FlyEagle` now calls `EditorApplication.QueuePlayerLoopUpdate()` while there is a bird in
the air, and only then — **above** the `deltaTime <= 0` guard rather than below it. Below
it the call could not start anything: outside play mode `Time.deltaTime` is zero until a
loop is already running, so the one frame that could have started the loop returned
before queueing anything.

And a fifth that has not bitten yet but would have: **a Generic rig binds its clips
through an avatar.** Without one the animator runs, reports a state and a normalised
time, and moves nothing. `SpawnActor` copies the avatar off the model's own Animator when
it has to add one, and warns when there is none to copy.

### The trap that produced three false bug reports

`Decor` and `Models` are **serialized fields on components in the scene**. Pulling code
changes `LoadForestDecor` and `LoadModels`; it does not change what a saved scene already
holds, and nothing says so. The change is there in the diff and absent on screen, which
is the most expensive kind of wrong: a wagon that would not grow, lilypads that would not
shrink, a forest that stayed the old pack's.

`Setup Project` and `Set Up Play Scene` fix it by building a scene from scratch — and
throw away anything set by hand in the inspector along with it.

**It rewired the scene that was open, and the scene that is open is not always the one Play
runs.** Told four times that the old units were still in the game, with the menu item
reporting success each time — it was rewiring `LevelPreview` while `PlayLevel` kept the
models it had been saved with. A tool that fixes the thing you are looking at and leaves
the thing you are running is worse than one that does nothing, because it also tells you it
worked. It opens both scenes now, and names the one it saved.

**And the running game says the cast out loud.** "The old units are still in the game" had
three possible meanings — a stale scene, a prefab that failed to load, or a fallback
standing in — and no way to tell them apart from outside. `RunVisuals.ReportCast` names the
prefab every post is actually drawing, once per level, at runtime. A name is not an
opinion: `MC_ManAtArms_01` and `Knight` are different words.

`Arna > Refresh Scene Assets` does the same wiring in place, and prints what it wired:
the wagon height in use, the cart and horse models by name, the marsh plants and lilypads
by name, and the eagle's textures and controller. "It did not change" becomes a thing
that can be checked rather than believed. It also mends the one fault it can — an eagle
whose materials carry no textures gets `Wire Loose Textures` run over it on the spot.

Two of the three reports also needed nothing but a `git pull`. Wagon height is a `const`
read at runtime, so it applies on Play with no rebuild at all; the lilypads live in the
serialized decor and need the refresh. Different requirements, one symptom — which is
exactly why the log matters more than the fix.

### The ground

The terrain is one shader — a vertex colour per type with a grain texture over it — which
gives an even sheet of green. Every reference for this game shows the opposite: grass
worn through to soil, gravel where water meets land, a road that is a band of trodden
earth rather than a line of a different colour. That variation is most of what makes
ground read as ground.

It could come from a second texture set and a blend map. It comes instead from the pack,
as flat pieces laid on top — `GroundPatches` in `BiomeDecor`, a few hundred triangles
each, and reversible.

**They are placed only where the ground is flat.** These are flat pieces from a pack
built for flat modular scenes and this game's ground is a heightmap; laid across a
hillside a flat piece buries one edge and floats the other, which is worse than the even
green it was meant to break up. A tile is offered a patch only when its four corners are
within 0.9 m of each other — about twelve degrees — which keeps them on the valley
floors, the river flats and the road. Which is where the references put them anyway,
because that is where ground gets walked on. They are lifted 5 cm, because coplanar
surfaces fight for the depth buffer and flicker as the camera moves — the one artefact
on this list a still screenshot will not show and every player will see.

Density is heaviest on the road (0.55 per tile), then plains (0.22), marsh (0.14), a
little in forest (0.07) and none in the mountain pass, which is bare rock already.

**They come from PolygonGeneric, which was on the deletion list and should not have
been.** Its name and half its contents are modern — air conditioners, sidewalks, tyre
marks, a robot — and on that basis it looked like something that came along for the
ride. Its `Environment` folder is the only source of ground surfaces in either pack.
Mixing two Synty packs is allowed here and nowhere else: this is ground, seen flat and
mostly in shadow, not a spruce standing next to another artist's spruce.

`docs/synty-inventory.md` lists every prefab in both packs and what each is used for, or
why not.

### The planning map got 60 % denser by accident

`LevelPreview.DensityScale` multiplies the play view's density, because a map is read
from far above where scattered trees vanish. It was tuned to 2.2 against a forest density
of 0.28 — about 0.62 trees per tile on the map. Raising the play view to 0.45 carried
that to 0.99 without anyone choosing it, and the terrain a player has to read in order to
draw a route went under a carpet of crowns. It is 1.38 now: 0.62 ÷ 0.45, the tuned figure
restored.

The general shape of this mistake is worth naming, because it is the second one this
week. A constant tuned as a *product* breaks silently when either factor moves, and
nothing fails — it just looks slightly wrong in a way nobody can point at.

### The escort stands beside the column, not in the traces

The six posts were six points on a six-metre circle around the lead wagon, sixty degrees
apart. That was right when a caravan was one wagon and became wrong without anyone
changing it: once the teams were hitched, slot 0 — dead ahead at six metres — stood
**between the lead horses and the cart they pull**, and the escort walked through the
traces.

The circle is a rectangle now. The van walks at 10 m, clear of a team whose noses reach
about eight; the two flank pairs stand 6 m out to the side, where left and right mean
left and right; the rearguard follows 10 m behind. `Posts` is an explicit table rather
than an angle, because there is nothing regular about the shape a column wants.

**It is tighter than the column is long, on purpose.** Posting the rear pair back beside
the third wagon is the truthful arrangement and it lost 1-6 on every route: six troops
strung over forty-five metres cannot support each other, so a pack that finds the
rearguard fights it two against five while the van is half a level away. A 10 m pair
spacing still lost it; 5 m holds. The escort is a fighting unit before it is a cordon,
and the spacing is what the combat can carry rather than what the column measures.

`FormationSpan` replaces `FormationRadius` in the test that catches a wandering troop, and
that test now measures against the **nearest wagon** rather than the lead one — otherwise
it calls the rearguard a deserter for standing where it is posted.

### The camera looks down at the column, and at the middle of it

Two changes, and the second is the one that matters.

It aims at the column's **middle** rather than at its lead wagon. Three wagons at 15 m
with a team in front of each is better than forty metres from the lead horses' noses to
the last cart's tail, and aiming at the front put the third wagon off the bottom of the
screen. Being able to pinch out and see it is not the same as it being in the shot.

**And the angle rides the zoom.** A pinch used to change range at a fixed pitch, and
34.8° is a low angle — the camera sat about as far behind the column as above it. At the
closest range that put it 20 m behind and 14 m up: level with the wagons, looking at their
backs, the road ahead hidden behind them. Zoom is not a dolly. *What a player means by
"closer" is "let me look at this", and looking at something on the ground means looking
down at it.*

`PitchFor` runs the angle from 62° at `MinRange` to 30° at `MaxRange`, so:

| Range | Pitch | Behind | Above |
|---|---|---|---|
| 24 m | 62° | 11 m | 21 m |
| 62 m (default) | 49° | 40 m | 47 m |
| 120 m | 30° | 104 m | 60 m |

Range is the slant distance, so raising the angle at a fixed range moves the camera closer
to the column *and* higher above it at once — which is exactly the trade being asked for.

**And `Caravan.ColumnCentre` does that arithmetic, not the camera.** There are two distance
origins in `Caravan` and they are a run-up apart: `DistanceTravelled` counts from the start
line because that is what the game reports, while `PositionAt` counts along the whole path
including the 40 m behind it. Subtracting half a column from the first and handing the
result to the second aimed the camera fifty-five metres behind the caravan — which looks
like the camera lagging, not like a unit error. A test holds it now.
A drag still moves the angle: it is kept as a **trim on top of the curve** rather than
replacing it, so a player who has tilted the camera keeps that tilt through a pinch
instead of having it snapped away.

`FollowDistance`/`FollowHeight` are 40 and 47, which is the orbit's resting view expressed
as an offset. The fixed view and the orbit's default have to be the same view: every
judgement in these notes about how big a thing reads was made from one of them.

### The dead lie where they fell

A wolf used to vanish the instant its share of the pooled health ran out, which reads as a
rendering glitch rather than as a kill — and the animator has had a Death state built for
it since the controllers were generated, with nothing ever asking for it.

A figure past the group's live count now plays `Dead` and is left where it is. It keeps
its last position because nothing places it again, and the index is a stable death mark:
`alive` only ever falls, so figure *i* is dead for good once it passes it and a body
cannot come back to life on a later frame. Beaten groups are no longer switched off
wholesale either — what a player is owed after winning a fight is the evidence of it. The
same applies to our own dead.

### Wolves hunt in a ring, not a queue

A wedge is right while a pack is running — lead animal at the point, the rest fanning back
— and wrong the moment it arrives, because **a wedge is one animal deep at the front**.
Five wolves in a wedge means one wolf reaches the troop and four wait their turn a metre
and a half behind it: on screen, the thing the player was told is a pack, attacking one at
a time.

`Formation.Ring` spreads them over five sixths of a circle centred on the way the group
faces, at `PackRing = 2.5 m` — just inside a wolf's 2 m reach, so every animal on the ring
is at the fight rather than walking toward it. Not the whole circle: a pack that encloses
its quarry perfectly has one animal directly behind it and reads as a diagram, where a
sixth left open is both what a real pack does and what lets the player see the fight
instead of a wheel of backs. Each animal turns to face the middle of the ring, or the far
side fights with its back to the quarry.

### The animals were under the grass

`CoverHeight` is 0.7 m — every tuft, fern and reed on the map is fitted to it — and a fox
was drawn at 0.45. It was placed correctly on thirty-four tiles of every level and was
*invisible as arithmetic*. A doe at 1.1 m stood a hand above it, from a camera 46 m back
and 32 m up.

They are drawn at roughly twice life now: fox 0.95, doe 1.5, stag 1.75, boar 1.2 — the
same call the eagle got and for the same reason. An animal here is a resource the player
can choose to take, and a thing that cannot be seen is not a choice. The ceiling is the
man walking past: a troop is 1.85 m and the stag stops below his head.

**The cap counts sightings, not animals.** Grouping the deer broke it the other way: a
deer draw places two to four where a fox or a boar places one, so the deer ate the budget.
Measured on a run — 21 does and 8 stags against 2 foxes and 3 boar, a level of deer with a
rumour of anything else, and the terrain that was supposed to decide the mix deciding
nothing. `Sightings = 16` is what is fixed now; `Count = 44` stays as the ceiling, because
a draw call budget does not care how the animals were chosen.

**And the deer stand in twos and threes.** Thirty-four animals over a 256-metre map is one
per nineteen hundred square metres — two or three in frame, each alone, each behind the
next tree. Grouped, the same thirty-four make a dozen sightings instead of thirty-four
misses: the eye is far better at catching a group than an individual, and a herd is one
event large enough to notice. Does mostly, with the occasional stag, because a field of
stags is a trophy room and it is the antlers that carry from a distance. The boar and the
fox stay solitary, which is true of them and keeps the herd from being the only thing on
the map.

The warning was rewritten with it. It fired only when *all four* models were missing —
the one case that was never the problem — and it names them one at a time now, because
"no animals", "one model missing" and "animals too small to see" look identical from the
outside and are fixed in three different places.

### Nine troop kinds, nine silhouettes

They shared three models — melee, ranged, support — so a priest looked like an engineer
and a shieldbearer like a spearman. The whole of GDD §4.2 is that *where you put which
troop* decides the level, and six posts around a caravan mean nothing if you cannot tell
what is standing on them.

`Stylized_Medieval_Army_Pack` has 52 characters across four social ranks, and the ranks are
the axis worth spending. **The difference has to carry from 47 m up and 40 m back**, where
a face is nothing and a tabard is a smudge: what reads is body shape, helmet outline and
what is held.

| Kind | Model | What it reads as |
|---|---|---|
| Spearmen | `MC_ManAtArms_01` | armoured line |
| Swordsmen | `MC_ManAtArms_04` | armoured line |
| Shieldbearer | `MC_Knight_01` | the widest figure in the pack — full plate is the only one that reads *broad* rather than merely tall |
| Archers | `MC_Archer_01` | the bow's line |
| Mage | `MC_Noble_01` | robed, unhelmeted — the pack has no mage, and a robe is the one silhouette that is unmistakably not a soldier |
| Priest | `MC_Noble_04` | the same, and the point |
| Scout | `MC_Levy_03` | lightest thing on two legs; a heavy scout is one nobody believes outruns anything |
| Engineer | `MC_Peasant_01` | a man who works, which is what he does |
| Cavalry | `MC_Cavalry_LightCavalry` | rider and horse, shipped assembled |

The three fighting kinds take the armoured ranks so they read as the line; the three
support kinds take the unarmoured ones so they read as the people the line is protecting,
which is what they are.

Cavalry gets its own height — `HeightOf(TroopKind)` returns 2.7 m for it against 1.85 for
everyone else — because the pack ships the rider **already on the horse**. Fitting that to
a man's height gives a man-sized horse with a doll on it. 2.7 puts it on the same ruler as
the draught teams at 2.4.

`Melee`, `Ranged` and `Support` stay as fallbacks. A scene saved before the split holds
those and nothing in the new fields, and `Models` is serialized on the scene component —
pulling code does not change a saved scene, and that has produced enough false bug reports
in this project to be worth one field each.

### The army pack has no animation, so it borrows the knight's

`Build Army Animator` searched every asset under the pack and found **not one clip**: 22
FBXs of meshes, 52 prefabs assembled from them, and nothing that moves. That is the answer
the search was built to get, and it rules out the cheap fix.

Every other pack here ships a model and its clips in one file, and this project has always
read them straight out of it — bone name to bone name, which only ever works inside one
file. **Humanoid is Unity's way round that.** The importer maps a skeleton onto a standard
human one; a clip is then stored as *what a human did* rather than as what these particular
bones did, and it plays on any other rig mapped the same way.

`Arna > Rig For Retargeting` does both halves, and both are needed:

- the **clips** are re-imported as humanoid, which is the knight's file, and the controller
  is rebuilt from them — the old one holds the generic versions, which retarget onto nothing
- every **rig** that is to play them gets an avatar, which is the army pack's meshes, found
  by asking each prefab's skinned mesh which file it lives in rather than by keeping a
  prefab-to-FBX table that would be wrong the first time the pack is updated

`ActorModel.Rig` carries the avatar, because the prefab and the file its skeleton comes from
are not the same asset. `SpawnActor` assigns it when the animator has none. **A retargeted
clip without an avatar has nothing to map onto**: the animator runs, reports a state and a
normalised time, and the model stands still — the failure that looks most like no animation
at all.

**It can fail, and it fails quietly.** Unity maps bones by guessing from their names and
hierarchy, and a rig it cannot read produces an invalid avatar and no error. So every avatar
is checked afterwards and the failures are named: an invalid one is fixed by hand in Rig >
Configure, and knowing *which* is most of that work.

**And it can be impossible, which is checked first.** A skinned mesh is a mesh bound to a
skeleton; a character built without one is a statue. No bones, so no avatar, so nothing to
retarget onto — and no importer setting fixes a model that has none. The tool counts how
many of the 52 prefabs carry a skeleton before it changes anything, and says so, because
that number decides whether the rest is worth doing. The pack's own description is careful
about this in hindsight: it calls the *bow* fully rigged and never says the same of the
characters.

### Finding the army pack's animation, or proving it has none

`Build` reads clips out of one FBX, which is right for every pack here that ships a model
and its animation in the same file. The army pack does not: its 22 FBXs are meshes, its 52
characters are prefabs assembled from them, and whatever animation it has is somewhere else
in the folder — possibly `.anim` assets, possibly sub-assets of a rig file this project
never names.

`BuildFromFolder` searches by type instead, so it finds them either way — and **finding
nothing is itself the answer**, to a question that was otherwise going to cost a round trip
and a guess. Three outcomes, and the console says which:

- **clips found and matched** → the troops move, and `Arna > Build Army Animator` has just
  built the controller they share
- **clips found and unmatched** → the names need adding to `IdleNames` and its neighbours,
  which are lists to extend rather than rules
- **nothing found** → the pack ships no animation, and the way out is Humanoid retargeting:
  set both this pack's rigs and a pack that *does* have clips to Humanoid, and Unity will
  play one on the other

One controller for the whole pack rather than one per character, because they share a
skeleton — 52 characters out of 22 meshes — so a clip that binds to one binds to all, and
52 identical controllers would be 52 assets saying the same thing.

`Loop` came with it. `EnsureLooping` goes through the model importer, which is right for a
clip that is a sub-asset of an FBX and impossible for one that is not: a standalone `.anim`
has no importer to ask, and its loop flag lives in the clip's own settings. Death is left
alone in both, for the same reason.

### The bandits come from the same pack, and are told apart twice

**By rank first.** They are levy — the ragged end — where the escort is man-at-arms and
knight. A pirate captain from another artist's pack read as *a different game* rather than
as a different side; two ranks of one pack read as two sides of one war.

**By colour second.** `EnemyTint` multiplies every enemy figure's base colour through a
property block, applied once at spawn. The pack shades everything off one small palette
texture, so multiplying tilts mail, cloth and leather together instead of recolouring one
garment. A muted warm crimson rather than a saturated one: saturated red flattens a palette
into a silhouette and throws away the armour detail the rank distinction is carried by.

Colour is the second signal and never the only one — the first thing a moving figure loses
against a hillside in shadow is its hue, which is the same argument §5 makes for the three
wagons having three shapes rather than three tints.

### The caravan is three vehicles

`3DreaMax Studio/003_MDVL_WagonsCartsCarriages_Vol_1`. Ten vehicles, each shipped as
loose parts and one assembled `_Full` prefab; this game wants the assembled ones.

| Role | Prefab | What it looks like from above |
|---|---|---|
| Supply | `SM_Supply_Wagon_Full` | Barrels roped to an open bed |
| War | `SM_War_Wagon_Full` | Shields down both sides |
| Treasure | `SM_Covered_Wagon_Full` | A canvas hood |

This is what the pack was bought for. `WagonFor` promised three vehicles while the code
had two — supply and war were one model in different colours, and colour is the first
thing a moving object loses against a hillside in shadow. The player has to be able to
tell at a glance which one the bandits are converging on (docs/GDD.md §5), and three
silhouettes do that where three tints do not.

**The treasure wagon is the arguable one.** `SM_Merchant_Cart_Full` was the other
candidate and it is a market stall — table cloth, plates, a mug — which reads as
somewhere a caravan stops rather than as part of one. `SM_Royal_Carriage_Full` looks the
most valuable and is a passenger coach; a caravan hauling loot uses a covered wagon.

The improvised wagons are deleted with the rest of the old kit: `Wagon.fbx` and
`WagonTreasure.fbx` were a crate on wheels made before there were any carts. If a role
ever goes unfilled, `RunVisuals` still assembles one out of a crate and a barrel, which
is the fallback that was always underneath that one.

**The tell was on the map and not in the world.** `LevelPreview` passed its trap-field
sites to the decorator and `LevelRunner` did not, so a player read *something went wrong on
this ground* while drawing the route and then drove through country that said nothing —
which is the half where the signal was meant to do its work. The placement moved to
`Sim.TrapSigns` so both views ask the same question of the same rule.

**What is actually in the set, against what the GDD asks for.** §5 names *bone piles and
totems*. The pack has one skeleton and one skull, and the decorator places a single prop per
site, so what stands there is a body or a skull rather than a heap of either — `_Skull_01`
is weighted twice so bones are at least as likely as the chest, the grave and the cold fire.

**The totem exists now.** `BiomeDecor.Markers` holds the army pack's standing banner and
its archer stakes, and one goes up beside the wreck at every trap field: a wreck says
something *happened* here, a banner driven into the ground says somebody *chose* here, and
the difference between those two is the difference between an accident and an ambush.

It is its own set rather than another entry in `Ruins` because **it is measured up and
`Ruins` is measured across**. That set is fitted to five metres of width, which is right for
a wrecked cart lying on its side and would turn a banner into a sail. Same trap as the
boulders and the lilypads, one set later. `MarkerHeight` is 3 m — a head and a half above
the man walking past, tall enough to clear the scrub around a trap field from 47 m up, short
enough not to compete with a spruce.

The army pack's `Scattered_Arrows` and planks join `Ruins`, and its `Mud_1` and `Path` join
`GroundPatches`. Both on the rule that already let PolygonGeneric's dirt sit beside
PolygonNature's grass: **flat pieces seen face-on and mostly in shadow are the one category
where two artists' work does not show a seam** — and a wreck is the one place where mixed
provenance reads as chaos rather than as a mistake.

**The trap tell is finally what the reference shows.** That reference is a battle map
called *Övergiven Väg*, and what says so in it is wrecked wagons among the bones — not
masonry. Masonry means somebody built here; a cart left in a field means somebody died
here. `Ruins` mixes the pack's hay cart and peasant handcart with Synty's skeleton,
dropped chest, grave and dead fire. The carts are the same kind of vehicle the player is
escorting, which is the whole of what the signal says.

**The wagons stand 3.2 m rather than 2.5**, and the measure that settles it is the man
walking beside one. A troop is 1.85 m, so at 2.5 a wagon was 1.35 times a man — a
handcart, not a vehicle hauling three hundred and fifty silver. At 3.2 it is 1.7 times a
man and the column is the largest thing on the ground, which is what the player is meant
to be watching. There is room: the wagons trail 8 m apart and a covered wagon fitted to
3.2 m is about six and a half long.

**Colliders would not have fixed the caravan driving through boulders.** Nothing drives
the caravan physically — the simulation computes a position and `RunVisuals` assigns it
to a transform, so a collider on a rock has nothing to push against. What fixes it is
not putting the rock there.

`Decorate` takes a `driveLine` now, and it is deliberately *not* `keepClear`. `keepClear`
empties a tile and thins its grass, which draws the route as a swept lane through the
forest — the thing corridor-clearing was turned off for. The drive line refuses only what
a wheel cannot roll over: at or above `DriveClearance`, which is 2 m. That sorts the
existing size table cleanly without a single new guess — a rock is 2.2, a boulder 5.5, a
tree 7 to 8.5, all things a loaded wagon goes round; a bush is 1.9 and grass 0.7, both
things it goes over. The ground still reads as untouched country.

One caveat worth keeping: props are scattered up to 1.4 m from their tile's centre, so
something on a tile *beside* the line can still drift into it. Widening the line to the
neighbouring tiles would catch those and would start to look like a road.

**The wheels turn, and they turn by distance covered.** Same root as the colliders:
nothing drives the caravan physically, so there is no contact, no torque and nothing that
would make a wheel turn on its own. `WagonWheels` measures how far each wagon moved
between one `Sync` and the next and rolls its wheels by that — which means a wagon halted
in a fight has wheels that are genuinely still, and one crawling through the fen has
wheels that crawl. Tying the spin to time, or to the caravan's nominal speed, drifts out
of step the first time the ground slows the column.

Nothing in it knows what a wheel looks like. The axle is not read off the model: it is
the wagon's own right, which is `cross(up, forward)` and therefore the axis a wheel
rolling forward turns about. The radius is half the wheel's height in the world, because
a wheel standing on the ground is as tall as it is wide — a measurement that survives
however the part was exported: rotated, mirrored, scaled, pivot anywhere at all. Parts
are found by having "wheel" in the name, which both packs are consistent about
(`SM_Supply_Wagon_Wheel_1`, `SM_Covered_Wagon_Wheel_V2`) and which the improvised cart
follows too, so both roll. A wheel nested inside another wheel is skipped, or it would
turn twice and at twice the speed. If a pack ever ships a cart as one welded mesh there
is nothing to turn, and the console says so rather than leaving the wagons sliding.

**The column starts strung out, on road behind the start line.** Every wagon used to
begin on the start tile, stacked, because a trailing position was clamped to the head of
the route — so the third wagon did not appear until the first two had driven thirty
metres out from under it. A caravan assembling itself out of a single point is the first
thing a player sees of this game.

`Caravan.RunUp` is 40 m of path added *before* the route: the rearmost of three wagons
trails 2 × 15 = 30 m and its own team stands about 8 m further back, so 38 puts the last
horse's nose on the run-up and 40 leaves it air. The lead still starts exactly on the
start tile and still finishes on the goal — **the run-up is ground to stand on, not
journey.** `TotalDistance`, `DistanceTravelled`, `Progress` and `HasArrived` all take
their origin from the start line, which is why the change moved no test.

The start is chosen from the leftmost three columns of the map, so this road is off the
map by construction. `TerrainMeshBuilder`'s **skirt** puts ground under it: one outward
quad per border tile, colours and corner heights carried on from the edge, flat. 252
quads on a 64×64 map against the 2 960 tiles that widening the grid by ten would have
cost. It fixes a second thing that was never a bug report: the ground used to stop dead
at the boundary with three hundred metres of nothing between it and the skyline, which
reads as the edge of a board.

One trap in it worth keeping written down: **the sense of "round the quad" flips with the
direction each apron points**, so half of them came out wound backwards, facing down and
invisible — a bug that looks like the apron being absent on two sides of the map and
present on the other two. `Quad` corrects the winding from the shoelace area rather than
asking six call sites to get it right, and each vertex's normal and colour travel with it
through that correction.

**The teams stand close to their carts, and each cart says how close.** Three things had
to change and the first two were not enough on their own.

The horse's length is **stated** (2.6 m) rather than measured. A rigged model's renderer
bounds are a box drawn to hold every clip in the file, gallop included, so measuring gave
half again the length the animal standing in front of you occupies. Width is still
measured, because width barely changes between poses.

The pole came down from 2 m, because a pole runs *between* the horses and the traces
attach at the collar — what stands clear behind an animal is the swingletree and little
else.

And then there are **three hitch distances, not one**, because the three carts are not
built alike. The front is measured off the model, so a cart with a drawbar modelled on it
measures out to the end of that bar — which is where the horses already belong. Add a pole
to that and they stand a pole further on. The covered wagon has a bar where the supply
wagon has a bed edge; one constant could only ever be right for one of them.

They are serialized fields (`HitchSupply`, `HitchWar`, `HitchTreasure`), so they can be
dragged in the inspector against the thing on screen rather than guessed at and pushed
back and forth over a round trip. `RunVisuals` prints what each cart measured and where
its team ended up.

**The wagons trail 15 m apart, and the number comes from the horses.** A team's noses
reach ahead of the wagon they pull by half a cart plus the 2 m draught pole plus a horse,
while the cart behind extends half its own length back. Anything less than the sum puts
one wagon's horses inside the wagon in front — and **that failure is invisible rather
than obviously broken**: the animals do not disappear, they are drawn behind planking. At
the original 8 m they were three metres in, which reads as one horse per wagon rather
than two.

12.5 was the first correction, worked out for a covered wagon, and it was still tight at
the head of the column — because **the three carts are not the same length** and the
supply wagon leads. One spacing is only as good as the worst pair it has to hold apart,
and which pair that is, is a fact about the models rather than about `Caravan.cs`.

So it is not left as arithmetic. `RunVisuals.CheckSpacing` measures every pair against
the models actually loaded, prints the tightest, and warns with the figure needed when
the constant is short of it:

```
[Arna] Wagon spacing: the tightest pair is wagon 1 to 2, which needs 13.4 m;
       the caravan uses 15.0.
```

That is the general shape of this whole class of problem, and it has now come up four
times in a row: a number derived from a model has to be *checked against the model*, in
the place where the model is loaded, or it is a guess wearing arithmetic.

**Every wagon has a pair of horses in the traces.** `Quaternius/Animals/Horse.fbx`, the
same model the mounted troop rides, fitted to 2.4 m rather than the troop's 1.85: a heavy
horse stands about 1.7 m at the withers, so head up it reaches roughly 2.4, which is
three quarters of the wagon it pulls. Two abreast rather than one — a single animal in
front of a six-metre covered wagon looks like it is being punished. They stand
`size.x / 2 + 0.25 m` either side of the centre line rather than at a flat offset: a
constant is only right for a horse of exactly one width, and any wider and the pair
interpenetrate and read as one animal with too many legs. They stand 2 m clear
of the cart's front, which is what a draught pole is, and the cart's front is *measured*
rather than assumed from its height: the pack puts a hay cart and a covered wagon at the
same 3.2 m and they are nowhere near the same length. Parented to the wagon, so they
follow every turn the road makes and go out with it when it burns, and animated at the
caravan's pace like the escort. The cart is taken to face +Z, which is what everything
else in `RunVisuals` assumes; a pack that ever ships one facing another way puts its
horses at its side, which is at least a visible kind of wrong.

**Not yet done: the textures.** The pack ships 2K and 4K PBR, which is not the flat-atlas
look the rest of the world is drawn in. Two import settings close most of that gap —
textures down to 1K, smoothness low. What gives a photoreal asset away beside a stylized
one at forty-six metres is the gloss and the normal map, not the model.

### What is still to come

Everything under `Assets/Quaternius` is being replaced by three purchased packs:

| Pack | What it supplies |
|---|---|
| Stylized Medieval Army Pack | The cast — every troop and enemy in `VisualLibrary`, and the camp: tents, palisade, banners |
| ~~POLYGON Nature Pack (Synty)~~ | **Done.** The country — trees, plants, rocks, terrain, dead trees for the marsh |
| POLYGON Knights (Synty) | The built things — castle, houses, church, mountains, cliff, bridge, well, road pieces, an empty hay cart |
| Medieval Wagons, Carts & Carriages Vol. 1 | The caravan itself — ten wagons, which is what the game is about |

Two of the four are from the same POLYGON series, which is the reason for choosing
Knights over cheaper castle packs: the nature pack sets the art direction, and a pack
from the same series matches it by construction rather than by hope. Every alternative
was a bet that two artists' idea of stylized low-poly is the same idea, and that bet is
visible in every frame if it loses.

The wagon pack was bought on a different criterion, and deliberately. Ten wagons in one
purchase is what lets the war, supply and treasure wagons be three different vehicles
rather than one model in three colours — and telling them apart in motion is the whole
reason the treasure wagon is a different colour at all (docs/GDD.md §5). It ships 2K
and 4K PBR textures, which is not the flat-atlas look the rest of the world is drawn
in. Two settings close most of that gap at import: textures down to 1K, smoothness
low. What gives a photoreal asset away beside a stylized one at forty-six metres is the
gloss and the normal map, not the model.

**The village pack goes too, once one thing is checked.** The earlier plan kept
`Assets/Quaternius/MedievalVillage` because it was the only source of the abandoned
cart that marks a trap field. Knights ships an empty hay cart, which does that job at
least as well — a farm cart left in a field reads as abandoned. Import, put it through
`LevelPreview.RuinSites`, look at it on a map, and only then delete the folder. If it
does not read, keep the village pack and drop this paragraph.

**`Assets/Quaternius/Animals` stays, and is not up for review.** "Everything under
Assets/Quaternius" would have taken the wolf with it, and `Wolf.fbx` is the only enemy
on level 1-1 — the unlock table holds bandits back to 1-2 and archers to 1-4, so
deleting it leaves the first level of the game with nothing in it. `Horse.fbx` is the
cavalry mount on the same footing. Both are CC0, both already have animator controllers
matched to their filenames, and the army pack is a pack of soldiers: it will bring
horses and it will not bring wolves. The other ten animals in the folder are unused and
cost nothing to keep.

### The play camera

`Arna.App.CameraOrbit` holds pitch, yaw and range — what the player's two gestures
actually change — and `LevelRunner` polls the devices for them: pinch or scroll to zoom,
one finger or right-drag to swing round, R to reset. It has no UnityEngine dependency,
so the arithmetic is run and checked here rather than in an editor.

Four clamps, and each is a design decision rather than a safety rail:

| | |
|---|---|
| Range 24–120 m | Closer and the caravan fills a frame with no country in it, which is the one thing this game is about reading. Further and the column is grey specks. |
| Pitch 12–68° | **Not 90.** Straight down *is* the planning map, and arriving there by dragging would hand over the overview screen without its overlay — and with it the terrain reading the design asks the player to earn. |
| Yaw relative to the heading | A view chosen over the left flank stays over the left flank when the road turns. Absolute yaw swings the camera round the column at every bend. |
| Default = 46 m back, 32 m up | Verified: `CameraOrbit` at its defaults reproduces exactly the offset every measurement in these notes was taken from. `PlayerControlsCamera = false` pins it there, which is what a screenshot wants. |

### Wildlife

`Arna.Sim.Wildlife` puts 14 animals on a level and scatters them (GDD §3.5). Measured
under mono, not estimated:

| | |
|---|---|
| Animals per level | 34, a spread of fox, boar and both deer, 26 % of them in woodland |
| Closest an animal homes to an enemy group | never within 4 tiles |
| Caravan spook radius | 26 m |
| Battle radius | 55 m — wider on purpose, see the GDD |
| Distance covered by a bolt | about 49 m over 4.5 s |
| Settles back to | within the 6 m grazing radius |
| Met on an average run | 19.4 in sight within 80 m, of which 14.9 in open ground |

**Counting animals was measuring the wrong thing.** Twenty-six placed evenly put 14.3
within sight of a run and only 7.6 of them anywhere they could actually be seen — the
rest stood under a canopy that hides a deer completely from a camera 35° above it, and
an animal nobody can see is not sparse, it is absent. Accepting a forest tile only a
third of the time, at 34 animals, gives 19.4 in sight and 14.9 in the open: twice the
visible wildlife for a third more of it. A third rather than none, because foxes and
boar belong in a wood and hiding is a thing animals do.

The models are `ForestAnimals`: fox, both deer and boar, each loaded from its URP prefab
with the pack's own animator controller. `RunVisuals.BuildWildlife` spawns them and
`SyncWildlife` moves them; a fleeing animal turns the way it is running and a grazing one
keeps whatever facing it had — turning it toward a home it is only drifting back to would
have every deer on the level pointing at the same spot, which reads as a formation.

Shoulder heights, like the wolf: fox 0.45, boar 0.85, doe 1.1, stag 1.35. Measured to the
ear they come out a head too tall, and a deer as tall as a knight reads as wrong long
before anyone works out why.

### The scouting eagle, as imported

`Eagle_B1` from cgtrader, in `Assets/ThirdParty/Eagle`: the FBX plus five textures
(`ao`, `diffuseOriginal`, `height`, `normal`, `Opacity_Final` — no metallic or
smoothness map). Measured rather than assumed:

    Eagle_B1  13.37 x 5.32 x 7.80   baseY=-0.48
    rig=Generic  clips=4: Fly_01 (6.00s) Fly_02 (5.00s) Fly_03 (8.33s) Fly_04 (1.00s)

Three things follow, and the first is the one that nearly went wrong.

**The folder report's yaw advice was wrong here, and the report has been fixed.** It
reads the long horizontal axis as the body, nose to tail, which holds for everything
that walks. On a bird with its wings out the long axis is the *span*: 13.4 across
against 7.8 nose to tail. Taking the advice would have yawed the eagle ninety degrees
and had it fly sideways — the exact symptom the report exists to prevent. It now says
so when a model is half again wider than it is long *and* flatter than it is long, the
second test being what keeps a horse out of it. Forward on this model is Z, so
`YawOffset` is 0; whether the nose is +Z or −Z a bounding box cannot say, and 180 is a
one-line change the first time the bird is drawn.

**It is fitted by wingspan, not by height.** `VisualLibrary.EagleSpan` is 10 m — the
number the planning render settled, where 21 m read as a dragon and 11 m vanished into
the canopy, and deliberately not a golden eagle's two. Height is the wrong handle for
this model: most of its 5.3 vertical is wing dihedral, so fitting by height gives a
wingspan decided by how far the wings happen to be cocked in the bind pose.
`RunVisuals.Spawn` takes a `byWidth` flag for it.

**It has no idle.** All four clips are flight, which is correct — a bird in the air has
no standing still. `AnimatorBuilder` now lists `Fly` last in both the idle and the walk
name lists, so the same loop answers for Speed 0 and Speed 12, and nothing that owns a
real idle can lose it to a flight clip.

The bird belongs to the planning map (GDD §3.6) and **is not drawn during a run** —
`RunVisuals` never spawns it. The opacity map means the feathers are almost certainly
cut out of flat geometry, so the material will need Alpha Clipping in URP; untested
until something renders it.

**She flies now.** `LevelPreview` is the planning map in Unity — a top-down render of
the real world — which is exactly what the eagle is flown over, so that is where she
went. The path is `ScoutingAbility.Fly(map)`, the same call the game makes when the
ability is bought: what the plan shows is the flight the level actually has, seeded off
the map. A bird flying a path invented in the view would be decoration that contradicts
the game.

Two things had to be got right and neither was obvious.

**The flight is walked by distance, not by index.** `ScoutFlight` samples a Catmull-Rom
at fixed *parameter* steps, and a curve sampled by parameter is sampled unevenly by
length — tight turns bunch the points up. Stepping it by index at a constant speed has
the bird dawdle through the corners and bolt down the straights, which is precisely
backwards. `LevelPreview` precomputes the distance to each point and walks that.

**The altitude is where the animal is, not where the lowest feather is.** `ShowActor`
stands a model's feet on the ground it is given, which is right for everything that
walks and wrong for a bird.

It is driven from `Update` and the animator is stepped by hand, because the component is
`[ExecuteAlways]`: the plan is looked at in the editor far more often than it is played,
and a bird that only moves in play mode is a bird nobody sees.

**And the flight now lifts an overlay.** `PlanningOverlay` holds the colour maths — the
same numbers the map render settled — and `LevelPreview.ApplyOverlay` mutes everything
outside `ScoutFlight.RevealedTiles`. With no flight the whole map is muted, which is not
a bug: that is what the plan looks like before the ability is bought. `ShowOverlay` turns
it off for using the scene the way it was first built, as a harness for judging generator
output, where an overlay is in the way.

**Two mechanisms for one effect, because the two things are made differently.** The
ground is a mesh this project builds — four vertices per tile, in tile order, so muting
it needs no lookup from a vertex back to the ground under it — and its colours go all the
way to luminance. A tree is somebody else's prefab with somebody else's material, and the
only handle available without writing a shader is a property block that *multiplies* the
atlas. Multiplication darkens a green tree and cannot take the green out of it, so under
the overlay the wood drops to a third of its light and stays green. It reads as country
in shadow, which is near enough to country not yet looked at. The honest fix is a shader
that desaturates, and it is a bigger job than the difference is worth today.

The overlay takes the colour out and leaves the geometry, which is the design rather than
an economy: the terrain is what the player plans against, so hiding it would remove the
decision instead of the certainty.

**It is greyer than the map render's numbers.** Darken went 0.88 to 0.70 and the prop
light 0.34 to 0.20, and the reason is that the two pictures are muted at different
points. The Python render mutes a *finished image* — ground, trees and all — so one
figure lands on everything equally. Here the ground goes properly to luminance and the
props can only be darkened, so at the render's values the unflown country came out as a
grey field with a green wood standing on it, and the eye read the wood rather than the
grey. What decides the prop figure is not how grey the wood is on its own but whether it
is quieter than the ground it stands on.

**The skyline is off on the plan.** The ring stands 320 m outside a 256 m map and the
plan camera looks straight down from far enough up to hold the whole map, so its frustum
is wider than the ground and the mountains land *around* the map in the frame. A row of
peaks framing a map is not distance, it is furniture. `Decorate` takes a `horizon` flag
and the plan passes false.

### What the plan tells you, and what it makes you pay for

The overlay on its own is only half of it. The point of the ability is that it turns a
worry into a route, and that needs two kinds of mark, drawn under different rules.

**The crows are always there.** `CrowSignal` puts a flock over ground near a group, one
in five of them lying, and the plan marks every flock the same whether it is telling the
truth or not — a signal you can tell is false is not a false positive, it is a second
true one. Free, visible from the first moment, and never an answer: what they buy is a
shortlist of country worth worrying about. The wrecked carts and bone piles the decorator
scatters over trap fields do the same job in the same spirit, as scenery rather than as
marks.

**Groups are marked only where the eagle flew.** That is the whole of the ability: a
quarter of the country turning from *something is out there* into *four of them are
standing here*, which is the difference between a worry and a line drawn around it.
Unflown ground keeps its crows and keeps its silence.

`MapMarkerBuilder` draws both as flat discs through the same unlit material the routes
use — a mark on a map is not a thing in the world, and a marker that darkens because it
lies on a north slope is a marker saying something about the slope. One mesh for all of
them: twelve groups and eighty flocks should not cost ninety draw calls to say so. The
overlay does not touch them, so a red group over muted ground stays red.

### The crow and animal packs, as imported

Measured from the folder listing rather than guessed:

| | Path |
|---|---|
| Flock prefabs | `Assets/Unluck Software/Bird Flocks/Bird Flock Crow/Prefabs/Crow Flock - *.prefab` |
| One bird | `.../Prefabs/Bird/Crow.prefab` |
| **Baked variant** | `.../Baked (performance)/Prefabs/Bird Crow Baked.prefab` |
| Controller script | `Assets/Unluck Software/Bird Flocks/Scripts/FlockController.cs` |
| Wolf | `Assets/ForestAnimals/URP/Wolf/Prefab/Wolf_URP.prefab` |

Four decisions the listing settles:

**Import `Bird Flocks/URP materials - import as needed.unitypackage` first.** The pack
ships built-in-pipeline materials and leaves the URP ones in a package beside them.
This project is URP, so without that step the crows render as whatever the pipeline
does with an unsupported shader, which is not a subtle failure but is an easily
misattributed one.

**Import the URP materials before anything else, or the birds are magenta.** They were,
and the picture was unmistakable: the pack's built-in-pipeline materials find no shader
under URP.

**Use `Crow Flock - Wild Few`, not the baked example.** The example prefab is the
pack's showcase — a large flock plus its feather particle system — and nine of them
turned a level into a magenta snowstorm. The baked variant below is still the right
idea, but it ships only as a single bird and that demo, so having it means building a
flock prefab from `Bird Crow Baked` by hand.

**Take the baked variant, not the skinned one.** `Baked (performance)` swaps the
skinned mesh for a sequence of snapshot meshes (`CrowFlap Snap 1..15`) driven by
`BakedMeshAnimator`. A skinned mesh per bird is a per-frame skinning cost for something
twelve to twenty pixels across, and the budget in §6 is 150 draw calls on a phone.

**`Crow TEX 4K Normal.png` is a four-thousand-pixel normal map on a bird that is
twelve pixels.** Import size down to 128 or drop the map entirely. This is the same
correction the wagon pack needs and for the same reason, only more extreme.

**`Crow Flock - Wild Few` is the prefab to start from.** The flock size measured for
this design is three birds; the other prefabs spawn more.

**The wolf can move to ForestAnimals, the horse cannot.** `ForestAnimals` ships
`Wolf.fbx`, `WolfUnity_Var2.fbx`, bear, boar, two deer and a fox, each with a URP prefab
and an animator controller. It ships **no horse** — so `Assets/Quaternius/Animals` may
lose everything except `Horse.fbx`, which is the cavalry mount. Note the new animals are
photoreal PBR with a fur shader, so they want the same treatment as the wagons: textures
down, smoothness down. It matters more here than for the crows, because a wolf is seen
at 46 m in the play view rather than as a speck.

**Crows are built, in the port and the renderer.** GDD §3.5 wants circling crows as the strongest of
the soft signals — an enemy group within twenty tiles, twenty percent false positives —
and route drawing makes that signal considerably more important, because it is read
before the line is drawn rather than during the run. Nothing in any of the four packs is
a crow.

Placement is done: `Arna.Sim.CrowSignal.Place(map)` returns the flocks, deterministic
from the level seed and mirrored from the port that measured the numbers. What is left
is the prefab. It wants a `PropSet Crows` on `BiomeDecor` and a caller in `RunVisuals`
that turns three of them on a ten-metre ring at 22 m; the planning screen draws the same
flocks as markers.

**They are two things, and an earlier note here got that half wrong.** It said crows
should never be a model, because a bird straight down from seventy metres is a few
pixels. That is true of the planning map and only of it: in the play view the camera is
46 m back and 32 m up, where a one-metre crow is 12 to 20 pixels and the flock's ring is
a hundred across. That wants a model with a flap. So: a marker of three specks on the
map, three low-poly birds in the run, both from the same `crow_sites` so they agree
about where the flock is.

The play view settled two numbers a still picture will not forgive. At 14 m the birds sat
in the spruce tops, and a near-black crow against dark forest at eighty metres is simply
invisible; at 34 m they were above a camera that looks 35° down and left the frame. 22 m
holds. They are painted a dark grey-blue rather than black, which is what eighty metres
of air does to a dark bird anyway.

**The hint radius in GDD §3.5 was wrong and is fixed.** It said a flock means a group
within 20 tiles; with sixteen groups on a 64-tile map, 96 % of the ground already has one
within 20 tiles, so the signal was true almost everywhere by accident. Six tiles covers
39 %, which makes a flock a real update rather than a decoration. `Tools/smoke_test.py`
guards the radius, the 20 % false-positive share, and — the load-bearing one — that
flocks cannot be counted back into groups.

The cart matters more than its size suggests. `RuinSites` places it near a trap field
and never on one, which is the soft signal the design asks for in GDD §2 — ground where
a caravan came to grief before. It is the only thing in the game that warns the player
about danger without giving away its position, and it is one model.

**Measure before wiring anything.** The report methods in §2 exist because guessing
cost hours last time, and a new pack pair is exactly when they pay:

- `ReportFolderDimensions` on each pack folder. It gives the up axis and whether the
  models arrive in metres. Do not assume Synty is Y-up because Synty is usually Y-up —
  the last pair of packs came from one author and disagreed with itself.
- `ReportMaterialTextures` on a handful of models. Synty ships one shared atlas per
  pack; if that holds, the word-matching in `RestyleModelMaterials` has nothing left
  to match and the whole restyle step may reduce to a no-op.
- `ReportRigBones` on one soldier, to find whether the right-hand bone the weapon
  fitting hangs off exists and what it is called here.

**What the swap touches**, nearly all of it in `Assets/Editor/ArnaSetup.cs`:

- The directory constants `QuaterniusDir`, `NatureDir`, `VillageDir`. All three are
  replaced, by one for the army pack and one each for the two POLYGON packs.
- `LoadForestDecor()`: every `Nature(...)`, `Rts(...)` and `Village(...)` name list,
  and the `PropSet` up-axis flag that travels with each one.
- `LoadModels()`: the cast against `VisualLibrary` — melee, ranged, support, mounted,
  wolf, bandit, bandit archer — plus weapon paths and lengths. The army pack is the
  first source that was actually built for these roles, so the mapping stops being by
  silhouette and starts being by name.
- `Assets/_Project/Animation/*.controller`. `AnimatorBuilder` matches a controller to
  a model by filename, and `RunVisuals` drives `Speed`, `Attack` and `Dead`. Every new
  character needs a controller built for it, and if the pack ships no clips at all
  that is its own work item — the models will stand in bind pose, which in a headless
  capture looks exactly like a broken animator.
- The height constants in `TerrainDecorator`. Fitting is by height, so nothing breaks
  mechanically, but the density numbers were tuned against a canopy of a particular
  shape and will want another pass once the new trees are on the map.

**What stops being true.** Two entries in §6 belong to the packs being removed rather
than to the project, and should go with them once the swap is proven: texture names
not matching material names, and the shared leaf atlas that made some plants genuinely
violet. Both are facts about the Quaternius stylized nature pack alone.

The third — the packs disagreeing about which way is up — only narrows. The Z-up RTS
scenery is what made the two answers collide, and it leaves; if everything remaining
is Y-up the disagreement is not live. But `PropSet.ZUp` stays where it is. It exists
because the up axis is a property of a pack rather than of a biome, and that is true
whether or not today's packs happen to agree. Rip it out and the next pack imported
sideways costs the same hours again.

Everything else in §6 survives — the widest-axis rule, `SurfaceElevation`, the ambient
equator, synchronous shader compilation in headless captures — because none of it is
about a particular pack.

**What it does not solve.** The bestiary. Wolves come from the CC0 animal folder that
stays, and anything beyond wolves, bandits and archers needs a source — Synty's SIMPLE
Forest Animals is the cheap one, with the caveat that SIMPLE is a flatter art line than
POLYGON and carries no birds at all. Roads and the enemy budget are untouched; no pack lays a
road — though Knights brings modular cobble and stone path pieces, so the art will be
waiting when the budget question is finally settled. The camp, the shop and the UI are
untouched, and so are the phone builds.

The caravan's wagons are no longer their own question. They come from the wagon pack,
and the three roles want three shapes: a covered wagon for supply, a heavier cart for
war, and a box or merchant wagon for treasure — a strongbox on wheels reads as loot
from further away than a colour does. Knights' empty hay cart stays what it is, the
ruin beside a trap field.

`Tools/wagon.py` and `Tools/treasure_wagon.py` are gone with that decision. They built
a covered cart and a strongbox cart in Blender and were the answer while there was no
better one. One thing from them is worth carrying forward if anything is ever built by
script again: Blender's `primitive_cube_add(size=1)` is already one metre on a side, so
scaling by `size / 2` — which both scripts did — halves every board while wheels built
from radii stay correct. A full-sized wheel against a half-sized body is what the old
models were, and it is not a proportion anyone chose.

**Order of operations.** Import and measure, wire `ArnaSetup`, run
`CaptureLevelPreview` and `CapturePlayScene`, and only then delete the Quaternius
folders — in one commit, so no revision of the project exists with neither set of art.
`MedievalVillage` leaves with them only if the hay cart passed the check above. Add the
imported folders to `.gitignore` as soon as their names are known, before the first
commit that could sweep them in; see §7 for why they cannot be committed and what that
costs.

**One seam remains, and it is the one to watch.** The army pack is not from the
POLYGON series, so the characters come from a different hand than the country they walk
through. That is the easier of the two seams to live with — a soldier is looked at, a
landscape is looked through, and the two rarely share a silhouette — but it is worth a
deliberate look at the first play capture rather than a discovery six months in. The
seam that would have been harder, a differently drawn building standing in a Synty
forest, is what buying Knights instead of a cheaper castle pack avoids.

Last: `Tools/render_screens.py` draws stand-ins shaped like the old packs. If the
pictures it makes are still meant to resemble the game, its prop geometry wants
retuning to the new silhouettes at the same time.
