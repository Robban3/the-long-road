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

**Traps are inert.** Level 1-8 places fourteen of them; a run down its fast corridor
reveals two and triggers none. The trigger radius is three metres and threat now sits
across the whole crossable band, so a trap forty metres off the line the player drew is
scenery. Enemies survived that change because a group has a territory and comes to you;
a trap has neither. Three EditMode tests have been failing on this and none of them said
so in those words, because a trap that never fires reads as a squad that took no damage.
The fix is a placement decision rather than a bug fix — chokepoints a route cannot avoid
(fords, passes, narrow ground) rather than a scatter over open country — and it is worth
making deliberately.

One of those three, `TrapsStrikeTheTroopOnPointRatherThanTheWagons`, was *passing* until
the halt fix, and not for its own reason: it asserts that the troop on point ends the
level hurt, and the point troop was being hurt by wolves rather than by traps. Once the
escort could win 1-8 cleanly the assertion had nothing left to lean on. A test that
passes off the wrong evidence is worth more attention than one that fails.

**Threat sits on slower ground than the map average, on nine levels out of ten.**
Measured over chapter 1: 1-1 places eighteen groups on ground averaging 0.717 against a
map average of 0.761, 1-8 lands at 0.671 against 0.761, and only 1-2 comes out ahead.
`EncounterPlacerTests.ThreatFollowsFastGround` asserts the opposite, one level at a time,
and it is not sample noise at that spread. The design rule it encodes — the quick way is
the dangerous way — and the ambush weighting that draws groups toward cover are pulling
against each other, and which of them should win is a design call, not an implementation
one.

**Chapter 1 has levels with only one survivable corridor.** With the fixed test escort
at each level's own budget and enemy strength, 1-6 and 1-8 come out at one route of
three; the other eight levels give two or three, and every level has at least one. That
is up from the state this was found in, where 1-5 offered none at all and the caravan
died at seven percent of the route. `EveryLevelOffersAWayThroughForAnEscortedCaravan`
wants two, on the argument that a level with one way through is not a route choice. It
is a tuning question — enemy strength, squad budget, or the threshold itself.

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
`RunVisuals` never spawns it. It is wired into `VisualLibrary` and the actor-fit report
so that scale and facing are settled now rather than on the day the planning screen is
built. The opacity map means the feathers are almost certainly cut out of flat
geometry, so the material will need Alpha Clipping in URP; untested until something
renders it.

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
