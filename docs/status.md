# Where the project stands

Working notes, kept current so that picking the work up on another machine does not
mean rediscovering what was already settled. The design documents next to this one
say what the game is meant to be; this one says what it currently is.

Last updated when the three asset packs were settled and the swap planned; §8 holds
it. The landscape pass on chapter 1 is what came before.

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

### Without Unity at all

`Tools/arna_level.py` is `Arna.Sim` and `Arna.Gen` transcribed to Python, and
`Tools/render_screens.py` draws both views from it with its own rasteriser, z-buffer
and shadow map. That is what a machine with no engine on it — a cloud session, a
laptop without the project installed — can still look at:

    cd Tools && python3 render_screens.py --chapter 1 --level 5 --out ../docs/screenshots

It reproduces both numbers this document records for 1-5 — 59 % corridor overlap in
§4 and a fastest route of 94.4 in §6 — which is the check that it is generating the
same levels. Getting there needed its pathfinder to round to single
precision the way C# float does; in double precision 1-5 came out at 67 % overlap
instead of 59 %, because the cautious route is searched over ground thick with
equal-cost tiles and the rounding decides which of two identical paths A* keeps.

What it cannot show is the art: every FBX and texture in the repository is a Git LFS
pointer, so scenery is drawn as procedural stand-ins at the sizes `TerrainDecorator`
gives them. The country is right, the dressing is a sketch. See
`docs/screenshots/README.md`.

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
but the green is placeholder work. Whether the pack swap in §8 fixes this depends on
whether the army pack contains a cart; if it does not, `Tools/wagon.py` stays.

**Mountains and buildings are untextured.** They come from the flat-coloured RTS
pack and sit beside textured trees. The stylized nature pack has no landforms and no
buildings, so replacing them needs another pack. This is the problem the pack swap
in §8 is meant to end.

**No camp, no shop, no UI.** The economy is implemented and has nowhere to be spent.

**Neither phone can be built for.** Only Windows Standalone support is installed.

---

## 5. What to do next, in order

1. **Swap the asset packs**, and measure before wiring — see §8. It settles the
   untextured landforms, the missing buildings and the character roster in one move,
   and it decides what is left of the wagon problem.
2. **Replace the wagons**, unless step 1 brought a cart with it. They are the thing
   the whole game is about and they are the weakest models on screen.
3. **The enemy budget, then roads.** Roads cannot land until the budget is fixed;
   see §6.
4. **The planning map's frame** — border, compass, title. Cheap, and it is much of
   what makes a picture read as a map.
5. **The camp between levels**, so silver and upgrades have somewhere to happen.
6. **Android and iOS build support**, and a build on a real device. The frame rate
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

The swap in §8 changes what this section can promise. Two purchased Asset Store packs
cannot be committed, so the repository stops being a thing that runs after a clone: it
becomes code plus a shopping list. What stays committed stays CC0 — the medieval
village pack is kept for exactly the props §8 lists, and nothing about it changes here. That is a fair trade for art that carries the game,
but it is a decision rather than a side effect, and the consequence is worth stating
plainly — the code can stay public, the game it renders cannot be assembled from the
repository alone.

---

## 8. Swapping the asset packs

Everything under `Assets/Quaternius` is being replaced by three purchased packs:

| Pack | What it supplies |
|---|---|
| Stylized Medieval Army Pack | The cast — every troop and enemy in `VisualLibrary`, and the camp: tents, palisade, banners |
| POLYGON Nature Pack (Synty) | The country — trees, plants, rocks, terrain, dead trees for the marsh |
| POLYGON Knights (Synty) | The built things — castle, houses, church, mountains, cliff, bridge, well, road pieces, an empty hay cart |

Two of the three are from the same POLYGON series, which is the reason for choosing
Knights over cheaper castle packs: the nature pack sets the art direction, and a pack
from the same series matches it by construction rather than by hope. Every alternative
was a bet that two artists' idea of stylized low-poly is the same idea, and that bet is
visible in every frame if it loses.

**The village pack goes too, once one thing is checked.** The earlier plan kept
`Assets/Quaternius/MedievalVillage` because it was the only source of the abandoned
cart that marks a trap field. Knights ships an empty hay cart, which does that job at
least as well — a farm cart left in a field reads as abandoned. Import, put it through
`LevelPreview.RuinSites`, look at it on a map, and only then delete the folder. If it
does not read, keep the village pack and drop this paragraph.

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

**What it does not solve.** Roads and the enemy budget are untouched; no pack lays a
road — though Knights brings modular cobble and stone path pieces, so the art will be
waiting when the budget question is finally settled. The camp, the shop and the UI are
untouched, and so are the phone builds.

The caravan's wagons are their own question. Knights' hay cart is a farm cart, and the
treasure wagon has to read as a treasure wagon, so `Tools/wagon.py` stays unless the
army pack ships a covered cart. Check that at import: it is the difference between §5
step 2 being a day's work and being deleted.

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
