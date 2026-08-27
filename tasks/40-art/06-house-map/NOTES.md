## Delivery check, 2026-08-28 — measured against this task's own OUTCOME/VERIFY

**File list: complete.** 37 files present: `map_background.png` plus
`map_room_<nn>_<state>.png` for `nn` 01..12, `state` in
{dirty,partial,clean}. Names match `View/HouseMapView.cs`'s
`LoadNamed("Art/map_background")` and `LoadNamed($"Art/map_room_{roomNo}_{stateName}")`
exactly — that view already loads the real art with no code change, per its
own comments. Live under `game/Assets/Resources/Art/` (required for
`Resources.Load`).

**Measured, not trusted:** all 36 cells are 256×256, 8-bit sRGB, no alpha —
matches VERIFY 1's "cells 256x256" and `MISSING.md`'s recorded figure
exactly. `map_background.png` is 928×1664 (portrait), also matching
`MISSING.md` and the size `HouseMapView.cs` was already rewritten against
(percent-based layout, three columns by four rows, per its own 2026-08-28
comment) — no discrepancy found.

**VERIFY 2 (greyscale order survives per room), all 12 rooms:**

| room | dirty | partial | clean | dark→light? |
|---|---|---|---|---|
| 01 | 33.91 | 47.96 | 62.21 | yes |
| 02 | 31.28 | 43.06 | 55.04 | yes |
| 03 | 38.08 | 53.69 | 69.50 | yes |
| 04 | 34.06 | 49.39 | 64.92 | yes |
| 05 | 35.44 | 50.30 | 65.36 | yes |
| 06 | 30.96 | 42.19 | 53.61 | yes |
| 07 | 30.87 | 48.11 | 65.56 | yes |
| 08 | 31.42 | 39.28 | 47.35 | yes (narrowest spread, 15.93) |
| 09 | 32.19 | 47.64 | 63.28 | yes |
| 10 | 34.79 | 46.96 | 59.31 | yes |
| 11 | 31.36 | 46.35 | 61.54 | yes |
| 12 | 31.23 | 44.02 | 57.01 | yes |

All 12 order dirty < partial < clean strictly, converted to greyscale mean.
Room 08 has the smallest dirty-to-clean spread (15.93 vs a typical ~28-31)
— still correctly ordered, but the one worth a second look if the "distance"
check in VERIFY 3 turns up a weak tile.

**What a machine can't clear:**
- VERIFY 3 ("all 12 rooms' cells laid out together read as mostly
  unfinished / mostly done in one glance, checked by someone other than
  the author") — needs a person by its own wording. To make that judgement
  possible in one sitting, `qa/states-sheet-256.png` lays out all 12 rooms
  × 3 states as one sheet, each cell at its true 256×256 size with a
  room/state label, 3 columns × 12 rows.

Everything a machine can check (file count/names, cell size, background
size, alpha, greyscale ordering) is satisfied. `labels.txt` moved to
`status:review`; `verify:` left as `pending` for the human item above.

Not touched: `ROOM-PLACEMENT.md` (being written elsewhere) and its question
of room order/layout on the cutaway — out of scope here.
