# House map: room identities and placement

Evidence: `game/Assets/Resources/Art/map_room_NN_clean.png` (256x256, opened all 12) for identity; `game/Assets/Resources/Art/map_background.png` (928x1664) measured pixel-by-pixel with ImageMagick (`magick ... -trim`, row/column scans) for the house geometry.

## 1. The house geometry (measured, not eyeballed)

House bounding box (given, confirmed by my own `-trim`): **x 6%–93%, y 9%–92%** of the 928x1664 file. All placement below is given as a percentage **local to this box** (0% = box left/top, 100% = box right/bottom), plus the file-relative equivalent in parentheses.

Column scan (`magick map_background.png -crop <w>x1+0+<y> -fuzz 5% -trim`) at the main roof (chimney excluded) gives this taper of usable width vs. height-in-box (`ly`):

| ly (local %) | file y | silhouette width (px) | columns that fit |
|---|---|---|---|
| 0–5% | 152–220 | 0–160 | none (roof apex) |
| 5–15% | 220–360 | 160–450 | 1, narrow |
| 15–33% | 360–600 | 450–640 | 1, comfortable |
| 33–91% | 600–1420 | 640 (constant) | 2 |
| 91–100% | 1420–1532 | 640→0 (rounded base) | shrinking, avoid |

So **the roof only ever supports a single column.** Two columns only become possible at ly≈33% (file y≈600), which is where the walls stop being roof-slope and go vertical. The game's current "2 across x 6 down" uniform grid puts column 2 inside the roof for its top 2–3 rows — there is no width there for it; that is the root bug, independent of which room lands where.

Inside the vertical walls there is also a ~86px painted bevel/shadow border (measured via horizontal color scan at y=900: light frame x144–230, flat dark cavity x230–700, light frame x700–784). Treat it as a soft margin, not a hard edge — it's shading, not a second frame — but keep cell centers inside it: usable cavity ≈ local x 19%–81% within the 2-column band.

## 2. The twelve rooms

01 — entry hall: coat hooks, wall mirror, cushioned bench, door — front-door reception room.
02 — kitchen: stove, range hood, sink, dish shelves.
03 — living room: sofa, coffee table, rug, large window.
04 — bedroom: bed, nightstand, lamp, window.
05 — bedroom (second): bed, round rug, wall shelf — duplicate of 04's type.
06 — study/office: desk, desk lamp, books, chair.
07 — bathroom: clawfoot tub, towel shelf, mirror.
08 — pantry/storage: preserve jars, wicker baskets, step stool.
09 — **attic**: pitched ceiling beams meeting at a point, round dormer window, rocking chair, storage boxes. Unambiguous — this is the sloped-ceiling room the task is about.
10 — balcony/porch: outdoor railing, wicker chair, potted plants, trees visible outside — the one room with an outdoor element.
11 — hallway/corridor: runner rug, doors on both sides, framed pictures, windows — an upstairs corridor, distinct from 01's entry hall.
12 — reading nook: slanted skylight ceiling, built-in bench, bookshelf. **This is a second sloped-ceiling room**, not previously flagged. It belongs under the roof alongside 09.

Confirms the coordinator's two flags: 02 is a kitchen (not attic-shaped, doesn't belong under the roof), 09 is the attic (does belong under the roof, currently row 5 of a 2x6 grid — wrong).

## 3. Where the plan fights the art

- **Column count.** House supports 1 column for roughly the top third (ly 0–33%) and 2 columns for the bottom two-thirds (ly 33–91%). A uniform 2-column grid has no basis in the silhouette above ly≈33%. Compromise: **7 rows, not 6** — 2 single-column rows in the roof (for 09 and 12, the only two sloped-ceiling rooms) + 5 two-column rows in the body (for the other 10 rooms). 2+10=12, no room dropped.
- **Two rooms share one identity slot.** 09 (attic) and 12 (nook) are both sloped-ceiling rooms and both only fit as single-column cells — there is exactly enough single-column room for both, stacked, so neither has to be demoted to a body row it doesn't visually match.
- **04 and 05 are both plain bedrooms.** Nothing in the art distinguishes them (same furniture class, different rug/shelf dressing). They can sit anywhere in the body; I put them in adjoining rows since a real house doesn't scatter bedrooms.
- **10 (balcony) needs an edge.** It's the only room with an outdoor view; give it the right-hand column, not centered, so its plants can bleed toward open air rather than sitting boxed between two indoor rooms.
- **11 (corridor) is not a destination room**, it's a connective space — same category problem as 01 (entry hall) but mid-house. Placed as a landing between the ground floor and the bedrooms, matching its art (doors leading off it).
- **Usable rectangle** for cell placement, in file percentages: **x 6–93%, y 9–92%** (the box itself), with the caveat that the bottom ~8% of that box (ly 91–100%) is the rounded base of the wooden shape and should carry no room center.

## 4. Placement table (7 rows, bottom row = ground floor)

Local coordinates are % within the house box (0,0 = box top-left; 100,100 = box bottom-right). Cell size is a suggested width x height in the same local %.

| Row (bottom→top) | Room | Identity | Col | Local x% (center) | Local y% (center) | Cell w% x h% |
|---|---|---|---|---|---|---|
| 1 | 01 | entry hall | L | 27 | 88 | 32 x 16 |
| 1 | 02 | kitchen | R | 73 | 88 | 32 x 16 |
| 2 | 08 | pantry | L | 27 | 76 | 32 x 16 |
| 2 | 07 | bathroom | R | 73 | 76 | 32 x 16 |
| 3 | 03 | living room | L | 27 | 64 | 32 x 16 |
| 3 | 10 | balcony | R | 73 | 64 | 32 x 16 |
| 4 | 11 | corridor | L | 27 | 52 | 32 x 16 |
| 4 | 05 | bedroom | R | 73 | 52 | 32 x 16 |
| 5 | 06 | study | L | 27 | 40 | 32 x 16 |
| 5 | 04 | bedroom | R | 73 | 40 | 32 x 16 |
| 6 | 12 | nook (sloped) | single | 50 | 26 | 46 x 14 |
| 7 | 09 | attic (sloped) | single | 50 | 13 | 38 x 14 |

File-relative conversion: `file_x% = 6 + 0.87*local_x%`, `file_y% = 9 + 0.83*local_y%`.

## 5. Numbering vs. placement

The game plays rooms 1→12 in asset-number order. The delivered numbering is *almost* a bottom-to-top climb — 01 hall and 02 kitchen are correctly early/low — but breaks down twice: 09 (attic) sits at position 9 of 12, well before 10/11/12, even though it's the highest, most climactic room in the house; and 11 (corridor, a mid-house connector) sits later than 09 despite belonging lower in the climb.

Recommend re-mapping *play order* to the spatial climb above, without renaming the art files:

play order 1→12 = asset 01, 02, 08, 07, 03, 10, 11, 05, 06, 04, 12, 09

i.e. attic (09) becomes the last room, nook (12) second-to-last — the two sloped-ceiling rooms close the climb, matching "hall to attic" rather than the current random walk. This is a data/lookup change (room-index → play-order), not a re-numbering of the PNGs; no C# touched here per the task's constraint.
