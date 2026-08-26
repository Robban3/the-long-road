# The Synty packs, as imported

Every prefab in `Assets/Synty`, filed by what it is and what this game does with it.
Taken from the project's own listing rather than from the store page: what a pack
advertises and what its folder contains are not always the same thing, and a name that
does not exist is a bare hillside and no error (see `ArnaSetup.Load`).

Two packs are installed. **Both are needed**, and the second one nearly went in the bin
for having a name that sounds wrong.

---

## PolygonNature — the country

`Assets/Synty/PolygonNature/Prefabs/`

### Trees (`Trees/`)

| Group | Prefabs | Used as |
|---|---|---|
| Poly pine | `SM_Tree_PolyPine_01..03`, `_Sparse_01..03` | **`Pines`** — the faceted conifer of the reference shot |
| Pine | `SM_Tree_Pine_01..02`, `_Large_01..02`, `_Small_01..02`, `_Base_01` | **`Pines`** (01, 02) |
| Round broadleaf | `SM_Tree_Round_01..05`, `_TallRound_01` | **`Trees`** — the minority that punctuates the conifers |
| Generic | `SM_Tree_01..04`, `_Generic_01`, `_Generic_Giant_01`, `_Large_01` | spare broadleaf |
| Birch | `SM_Tree_Birch_01..04`, `_Small_01`, `_Dead_01` | unused — a birch stand is a different biome |
| Willow | `SM_Tree_Willow_Small/Medium/Large_01` | unused — river biome |
| Dead | `SM_Tree_Dead_01..03`, `_Pine_Dead_01`, `_Generic_Dead_01` | **`DeadTrees`** |
| Swamp | `SM_Tree_Swamp_01..04`, `_Branch_01..02`, `_Root_01..02`, `_Stump_01..02` | **`DeadTrees`** (01, 02) |
| Debris | `SM_Tree_Stump_01..04`, `_Log_01..02`, `_Branch_01`, `_Twig_01..05` | candidates for forest-floor litter |
| Vines | `SM_Tree_Vines_01..04` | unused |

### Rocks (`Rocks/`)

| Group | Prefabs | Used as |
|---|---|---|
| Loose rock | `SM_Rock_01..04`, `_Rounded_01`, `_Small_01..02` | **`Rocks`** |
| Boulder | `SM_Rock_Boulder_01` | spare |
| Piles | `SM_Rock_Pile_01..05`, `_Pile_Curved_01..02` | candidates for riverbank |
| Clusters | `SM_Rock_Cluster_Large_01..06` | candidates for the mountain pass |
| Walls / tiles | `SM_Rock_Wall_01..02`, `_Tile_01..03` | unused — modular building blocks |
| Caves | `SM_Rock_CaveEntrance_01..02`, `_CaveInterior_01..02` | unused |

### Plants (`Plants/`)

| Group | Prefabs | Used as |
|---|---|---|
| Grass | `SM_Plant_Grass_01..05` | **`GroundCover`**, listed twice for weight |
| Fern | `SM_Plant_Fern_01..03`, `_Leaves_01..02` | **`GroundCover`** |
| Bush | `SM_Plant_Bush_01..03`, `_Leaves_01..03`, `_Hedge_Bush_01..02` | **`GroundCover`** (01–03) |
| Undergrowth | `SM_Plant_Undergrowth_01` | **`GroundCover`** |
| Flowers | `SM_Plant_Flowers_01`, `_FlowerPatch_01` | **`GroundCover`** (Flowers_01) |
| Mushrooms | `SM_Plant_Mushrooms_01..06` | **`GroundCover`** (01, 02) |
| Reeds | `SM_Plant_Reeds_01..02` | candidates for the water's edge |
| Lilypads | `SM_Plant_Lillypad_Small_01`, `_Large_01..03` | candidates for still water |
| Generic | `SM_Plant_01..07` | unused |
| **Purple flower** | `SM_Plant_PurpleFlower_01` | **deliberately unused** — see below |
| Palm | `SM_Plant_PalmBush_01` | unused — wrong climate |

### Terrain (`Terrain/`)

| Group | Prefabs | Used as |
|---|---|---|
| Mountains | `SM_Terrain_Mountain_01..03` | **`Mountains`** |
| Mountain backdrop | `SM_MountainSkybox_01` | **unused, and the biggest missed opportunity** — see below |
| Ground detail | `SM_Terrain_DustPile_Long_01`, `_Small_01..02`, `_Rubble_Pebbles_01..03` | ground patches |
| Grass edges | `SM_Terrain_GrassEdge_01..04`, `_Roots_01..02` | ground patches |
| Mounds | `SM_Terrain_Ground_Mound_Small_01..02`, `_Large_01..02` | ground patches |
| River | `SM_River_Plane_01`, `_Dip_01..02`, `_WaterFall_01`, `SM_Terrain_RiverSide_01`, `_Corner_01..02` | candidates for the river |
| Swamp | `SM_Swamp_Root_01..02`, `SM_Terrain_Swamp_Growth_01..03` | candidates for the marsh |
| Ice | `SM_Terrain_Ice_01` | unused — a later chapter |

### Props (`Props/`)

`SM_Prop_Fence_01..02`, `_StoneWall_01..03`, `_Pillar_01`, `_Pillar_Arch_01`, four broken
and four mossy pillar variants, `_Grave_03`, `_CampFire_01`, `_Chest_Wood_01`,
`_Bridge_Curved_01`, `_RoadSign_01`, `_Skeleton_Ground_01`, `_Skull_01`,
`_TorchStick_01`, `_Brick_01..02`, `_Arrow_01`, `_Cloud_01..03`.

All unused so far. `_Bridge_Curved_01` is the obvious candidate for a ford, and
`_Skeleton_Ground_01` with `_Chest_Wood_01` for the trap-field tell.

### FX (`FX/`)

Butterflies in four colours, fireflies, fire, smoke, snow, rain, blowing dust, blowing
grass, falling leaves in green/orange/pink, sunbeams, water ripple, waterfall foam,
stream particles, glowing dust, flies. All unused. Phone budget decides these, not
taste.

### Misc

`SM_Generic_SkyDome_01`.

---

## PolygonGeneric — the ground kit

`Assets/Synty/PolygonGeneric/Prefabs/`

**This pack was on the deletion list and should not have been.** Its name and half its
contents are modern — air conditioners, sidewalks, tyre marks, business characters,
a robot — and on that basis it looked like something that came along for the ride. Its
`Environment/` folder is the piece this project has been missing: **ground surfaces**.
Nothing in PolygonNature covers them.

### Ground surfaces (`Environment/`) — the reason the pack stays

| Group | Prefabs |
|---|---|
| Dirt patches | `SM_Gen_Env_Ground_Dirt_01..04`, `_Large_01..03` |
| Grass patches | `SM_Gen_Env_Ground_Grass_01..04`, `_Large_01..03` |
| Riverbank dirt | `SM_Gen_Env_Ground_River_Dirt_01..07`, `_Large_01..03` |
| Riverbank grass | `SM_Gen_Env_Ground_River_Grass_01..07`, `_Large_01..03` |
| Edges | `SM_Gen_Env_Ground_Edge_01..05` |
| Slopes | `SM_Gen_Env_Ground_Slope_Dirt_01..02`, `_Grass_01..02` |

### Landform

`SM_Gen_Env_Hill_01..05`, `_Mountain_01..03`, `_Background_Mountain_01..03`,
`_Cliff_01..04`, `_Cliff_Arch_01..02`, `_Cliff_Pillar_01`, `_Dirt_Cliff_01..08`,
`_Stalactite_01..04`.

### Vegetation, overlapping PolygonNature

`SM_Gen_Env_Tree_01..03`, `_Tree_Pine_01..03`, `_Tree_Dead_01..03`, `_Bush_01..04`,
`_Bush_Large_01..04`, `_Bush_Part_01..06`, `_Shrub_01..03`, `_Fern_01..03`,
`_Flowers_01..08`, `_Grass_01..07`, `_Grass_Tall_01..04`, `_Ivy_01..13`,
`_Ivy_Draped_01..03`, `_Vines_01..05`, `_Leaves_01..03`, `_Leaves_Pile_01..02`,
`_Lilypads_01..03`, `_Log_01..02`, `_Root_01..02`, `_Stump_01..03`, `_Twig_01..04`,
`_Mushroom_01..03`, `_Rock_01..10`, `_Rock_Pebbles_01..05`.

**Not mixed with PolygonNature's.** Two Synty packs are closer to each other than to
anything else, but they are still two artists' idea of a spruce, and a forest drawn from
both reads as two forests. The nature pack sets the art direction; this one supplies what
that pack does not have.

### Sky and water

`SM_Gen_Env_Skydome_01`, `_Skyline_01`, `_Cloud_01..03`, `_Cloud_Ring_01`,
`_Water_Plane_01`, `_Water_Dip_01`, `_Waterfall_01..02`.

### Roads

`SM_Gen_Env_Road_01`, `_Half_01..02`, `_Small_01..03`, `_Crossing_01`,
`_Intersection_01..02`, `_Ramp_01..02`, `_Gravel_Straight_01..03`,
`_Gravel_Corner_Large_01..02`, `_Gravel_Corner_Small_01..02`, `_Gravel_End_01`.

Modular road pieces, laid end to end. This game's road is a tile type on a generated
grid that bends wherever the player drew it, so the pieces do not fit it — the gravel
straights are worth a look as scatter along a road tile, not as a road.

### Not for this game

`Base/` (74 modular building pieces), `Building/` (pipes, ladders, background blocks),
`Characters/` (business, jumpsuit, prisoner, robot, space, street), `Props/` (aircon,
cardboard, keypads, manholes, screens), `Weapons/` (axe, pickaxe, spade), `FX/`.
Sidewalks, parking bays and tyre marks under `Environment/` likewise.

---

## Two decisions the inventory settles

**`SM_Plant_PurpleFlower_01` stays out.** The previous pack taught this at some cost: a
violet plant at any weight takes over the middle distance, because violet is the one
colour nothing else on a hillside is. The lesson is about the eye, not about that pack,
so it carries.

**`SM_MountainSkybox_01` is the biggest thing not yet used.** Both reference pictures put
large pale snow-capped mountains on the horizon, well beyond the playfield, and this
project has no horizon at all: the world ends at the map edge with a flat sky colour
behind it. `Mountains` as placed today are props standing *on* the map in mountain-pass
terrain, 20 m tall against a 14.5 m spruce — a hill among trees, not a skyline. The
backdrop is a separate job from the prop and the pack ships the part for it.
