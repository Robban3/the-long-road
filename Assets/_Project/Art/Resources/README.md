# Painted backdrops

Drop a painting here and the menu uses it. Nothing else to do — no menu item to run,
no field to wire.

| File | Where it appears |
|---|---|
| `TheVeilBackdrop.png` | Behind the front page: the title, PLAY, and the rest |
| `TheVeilRoadmap.png`  | **The level roadmap itself** — see below |
| `TheVeilRoadmap2.png` | The same for chapter 2, and so on. Falls back to the first |
| `TheVeilShop.png`     | Behind the shop |
| `TheVeilVictory.png`  | Behind the result screen when the caravan arrived |
| `TheVeilDefeat.png`   | Behind it when the caravan was lost |

Every one is optional. Without them the screens fall back to what they draw for
themselves — a gradient sky on the front page, a scattered wood on the roadmap, a plain
dimming behind a result — so nothing is ever waiting on art to be usable.

The import settings are set for you: anything landing in this folder is brought in as a
full-size UI sprite. See Assets/Editor/BackdropImporter.cs.

## Windows hides the extension, and that broke this three times

Explorer hides known file extensions by default. Save a picture you have named
`TheVeilShop.png` into a folder where `.png` is hidden and what lands on disk is
`TheVeilShop.png.png`. Unity strips only the *last* extension, so the resource is called
`TheVeilShop.png`, and the lookup for `TheVeilShop` finds nothing — a screen identical to one
with no painting at all. Three of the first four paintings arrived that way; the fourth
arrived as `TheVeilBackdrop..png` and worked, which is why it looked like the code was
picking favourites.

The folder now straightens this out itself: a file dropped here with a doubled or empty
extension is renamed on import, and the console says what it renamed. See
Assets/Editor/BackdropImporter.cs. Nothing to do — but if you ever wonder why a picture
is not showing, the filename is the first thing to look at, extensions turned on.

## Why this folder

`Resources` is normally avoided, and here it is the point. Everything else the scenes
use is a *serialized field*, wired by `TheVeil → Refresh Scene Assets`, and forgetting to
run that has produced four separate false bug reports in this project: the code changed,
the saved scene did not, and nothing said so. A file loaded by name at run time has no
such trap. Put the file here and it is in the game.

## The roadmap is not a backdrop

`TheVeilRoadmap.png` is different from the others: it *is* the screen, not something
behind it. The board takes the painting's own proportions, the whole picture is shown
rather than cropped, and the ten level medallions are pinned to points on the road in
the painting — first at the bottom, tenth at the top, so you can see where the journey
starts and what it is heading for.

The map is drawn larger than the frame it sits in and scrolled — a medallion is 194
units across with its ring, and ten of them will not fit down a picture fitted to the
board's width without piling on top of each other. At the current zoom a fifth of the
picture's width is cropped at each edge, so a waypoint's `x` has to stay inside roughly
0.21–0.79 to be on screen at all.

Those points are a table in `UI/RoadmapScreen.cs` (`Waypoints`), given as fractions of
the picture so they hold at any size. They belong to one painting: a chapter with its
own picture wants its own table. While `ShowWaypoints` is on, each medallion prints its
pair underneath in the editor, so one that has landed in a river can be read off the
screen and moved in a line rather than guessed at.

With no painting the screen draws the board it drew before — tiled ground, a scattered
wood and a road of paving stones.

## Shape

The screens are drawn for a tall phone — 1080 × 1920. A painting is fitted to *cover* the
screen, so it is never letterboxed and never squashed; whatever does not fit is cropped
evenly from the edges. A tall portrait image loses least. The menu darkens the lower half
of it so the buttons stay readable over whatever is underneath them.
