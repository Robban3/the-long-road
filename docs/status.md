# Where the project stands

Working notes, kept current so that picking the work up on another machine does not
mean rediscovering what was already settled. The design documents next to this one
say what the game is meant to be; this one says what it currently is.

Last updated after the landscape pass on chapter 1.

---

## 1. State in one paragraph

The simulation is complete and tested: terrain generation, three-corridor route
choice, caravan movement, detection, traps, combat, silver and upgrades, all
deterministic from a seed and all covered by 147 tests. The presentation has just
been through a full pass — the ground is lit and textured, the scenery is textured,
and the planning map is a top-down render of the real world rather than a grid of
coloured squares. What is missing is everything between levels: no camp, no shop,
no UI beyond a debug readout, and no build for either phone.

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

The report methods exist because every one of them settled a question that had
already cost hours of guessing. They are cheap to run and worth running first.

Tests:

    Unity.exe -batchmode -projectPath <project> -runTests -testPlatform EditMode -testResults <xml>

---

## 3. What works

- Deterministic generation: seed 1005 is level 1-5 on every machine, every time.
- Three corridors per level with a validated difference between them.
- The full run loop: movement, terrain speed, detection, traps, combat, silver.
- Ground: lit, shadowed, textured at two tiling scales, colours blended across
  tile corners so the world is continuous rather than tiled.
- Scenery: textured trees, grass, ferns, rocks; stones along every waterline;
  landmarks placed where they would stand rather than scattered.
- Planning map: a top-down orthographic render of the real level, with the three
  corridors drawn over it as ribbons.
- Weapons fitted to the right hand on every troop and enemy that needed one.

---

## 4. Known problems, worst first

**The generator lays no roads.** `TerrainType.Road` exists in the terrain table and
nothing ever writes it. Road is the fastest terrain in the game, so the
speed-against-safety trade-off is missing a pole, and houses and fields are placed
on and beside roads, so neither has ever appeared on a map. See §6 for why the
obvious fix does not work on its own.

**Corridor overlap runs high.** Level 1-5 shares 59% of its tiles between the three
routes. The whole level rests on the routes being meaningfully different.

**The wagons are poor.** Hand-built and they look it. The treasure wagon is a
different colour on purpose — the player is meant to see which cart holds the loot —
but the green is placeholder work.

**Mountains and buildings are untextured.** They come from the flat-coloured RTS
pack and sit beside textured trees. The stylized nature pack has no landforms and no
buildings, so replacing them needs another pack.

**No camp, no shop, no UI.** The economy is implemented and has nowhere to be spent.

**Neither phone can be built for.** Only Windows Standalone support is installed.

---

## 5. What to do next, in order

1. **Replace the wagons.** They are the thing the whole game is about and they are
   the weakest models on screen.
2. **The enemy budget, then roads.** Roads cannot land until the budget is fixed;
   see §6.
3. **The planning map's frame** — border, compass, title. Cheap, and it is much of
   what makes a picture read as a map.
4. **The camp between levels**, so silver and upgrades have somewhere to happen.
5. **Android and iOS build support**, and a build on a real device. The frame rate
   target has never been measured on hardware.

---

## 6. Things already established, so they are not rediscovered

**The enemy budget rewards speed with danger, and does not scale.** Budget is shared
between corridors in inverse proportion to travel cost, so a faster corridor is
given more enemies. Anything that makes a route faster therefore makes it
disproportionately deadlier. Laying roads was tried: corridor overlap on 1-5 fell
from 59% to 42% and the fastest route from 94.4 to 86.6 — both improvements — but
level 1-6 went from winnable to unsurvivable on all three routes, because the larger
budget landed on less ground. The budget formula has to be settled first.

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
