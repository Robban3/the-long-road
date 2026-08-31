# Painted backdrops

Drop a painting here and the menu uses it. Nothing else to do — no menu item to run,
no field to wire.

| File | Where it appears |
|---|---|
| `ArnaBackdrop.png` | Behind the front page: the title, PLAY, and the rest |
| `ArnaRoadmap.png`  | Behind the level roadmap, in place of the painted forest |
| `ArnaShop.png`     | Behind the shop |
| `ArnaVictory.png`  | Behind the result screen when the caravan arrived |
| `ArnaDefeat.png`   | Behind it when the caravan was lost |

Every one is optional. Without them the screens fall back to what they draw for
themselves — a gradient sky on the front page, a scattered wood on the roadmap, a plain
dimming behind a result — so nothing is ever waiting on art to be usable.

The import settings are set for you: anything landing in this folder is brought in as a
full-size UI sprite. See Assets/Editor/BackdropImporter.cs.

## Why this folder

`Resources` is normally avoided, and here it is the point. Everything else the scenes
use is a *serialized field*, wired by `Arna → Refresh Scene Assets`, and forgetting to
run that has produced four separate false bug reports in this project: the code changed,
the saved scene did not, and nothing said so. A file loaded by name at run time has no
such trap. Put the file here and it is in the game.

## Shape

The screens are drawn for a tall phone — 1080 × 1920. A painting is fitted to *cover* the
screen, so it is never letterboxed and never squashed; whatever does not fit is cropped
evenly from the edges. A tall portrait image loses least. The menu darkens the lower half
of it so the buttons stay readable over whatever is underneath them.
