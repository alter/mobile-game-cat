# Prompts for generating graphics — "Rescued Kitten"

Date: 25 August 2026
Working companion to `art-brief.md`. That one covers what and why, this one
covers verbatim prompts for each asset, with the negative part and caveats.

---

## 0. How to use this

Each prompt is assembled from three parts:

```
[BASE] + [SUBJECT] + [FRAME]
```

and is always accompanied by a **negative part**. The negative part is shared
across the whole set and doesn't change — it exists so the style doesn't
drift between passes.

Three rules, without which the set falls apart:

1. **The whole set is generated in one pass, with the same seed string value
   and the same model.** Style diverges between sessions, not within one.
2. **The first successful item becomes the reference** for everything after
   it: models that support a reference image get it as input. This outweighs
   any words in the prompt.
3. **What the neural network must not generate** — cat masks and paired room
   states. Why — in sections 4 and 5. They're made by editing a finished
   image, otherwise they won't match.

### English or Russian

Prompts are given in English: every verified image-generation model is
noticeably more reliable in English, and the entire style vocabulary of the
industry is in it. Russian explanations are for humans and don't go into the
prompt.

---

## 1. Palette

Taken from the already-written prototype (`build/playtest/index.html`), so
the art matches what's already built, not the other way around.

| Role | Name in the prompt | Code | Where |
|---|---|---|---|
| background, "clean" | warm cream | `#F4EAD8` | screen background, clean rooms |
| wood | light oak | `#C9A97C` | items in the "wood" group |
| shelf | pale sand | `#E8D9BD` | shelf, backing |
| outline and text | dark walnut | `#4A3B28` | outline for everything |
| cream | soft cream | `#F0E2C6` | items |
| peach | muted peach | `#E8B79A` | items |
| mint | dusty mint | `#A8C9B5` | items |
| dusty blue | dusty blue | `#9DB3C4` | items |
| warm grey | warm grey | `#B0A79B` | items |
| dirt, medium | muddy taupe | `#6B6055` | dirty rooms |
| dirt, dark | dull umber | `#55493D` | dirty rooms, shadows |

Outline rule: **one thickness for the whole set**, about 3% of the frame's
shorter side, color `#4A3B28`, but not black and not harsh — soft, slightly
blurred at the edge.

Light rule: **the source is always upper-left**, the shadow falls to the
lower-right, soft, with no hard edge, at about 25% opacity. No secondary
sources, no rim light, no starburst highlights.

---

## 2. Base blocks

### Positive, goes at the start of every prompt

```
soft 3D cartoon render, three-quarter top-down view at 30 degrees,
rounded shapes with no sharp corners, thick soft dark-walnut outline,
volume from soft diffused light coming from the upper left,
soft shadow falling to the lower right at 25% opacity,
warm muted palette of cream, peach, mint and light oak,
matte surfaces, gentle ambient occlusion where forms meet,
cozy children-book illustration feel but not childish,
clean flat single-colour background, subject centred in frame
```

### Negative, goes into every prompt unchanged

```
pixel art, voxel, low poly, flat vector, line art, sketch, watercolour,
photorealistic, photograph, 3D studio render with reflections,
neon, glow, bloom, lens flare, rim light, hard specular highlights,
high contrast, dark background, black background, dramatic lighting,
saturated colours, acid colours, pink glitter, sparkles, stars,
kawaii, chibi, anime, big shiny anime eyes, human facial expression,
text, letters, numbers, watermark, signature, logo, UI elements,
frame, border, vignette, drop shadow box, gradient background,
multiple objects, cropped subject, subject touching frame edge,
busy background, clutter behind subject, cast shadow on background wall
```

Part of the negative block repeats what the positive block already says. This
is deliberate: generation models follow the negative part more readily.

### Frame, added at the end

For items:

```
single object, centred, occupying 80% of the frame,
10% empty margin on every side, plain #F4EAD8 background
```

For rooms:

```
interior view, vertical composition, camera at standing height,
bottom third of the frame left empty and uncluttered
```

---

## 3. Thirty pile items

### Layout by families and colors

Color and family deliberately **don't coincide**: the five round items are
five different colors. Then any sample of ten kinds is distinguishable along
at least one axis. This isn't decoration: in the prototype two kinds got the
same color, and the board became unreadable.

| No. | File | Family | Color | Item |
|---|---|---|---|---|
| 1 | `prop_yarn` | round | soft cream | ball of yarn |
| 2 | `prop_ball` | round | muted peach | child's ball |
| 3 | `prop_plate` | round | dusty mint | plate |
| 4 | `prop_clock` | round | light oak | alarm clock |
| 5 | `prop_spool` | round | dusty blue | thread spool |
| 6 | `prop_bottle` | tall | muted peach | bottle |
| 7 | `prop_vase` | tall | dusty mint | vase |
| 8 | `prop_lamp` | tall | light oak | table lamp |
| 9 | `prop_jar` | tall | dusty blue | jar with lid |
| 10 | `prop_candle` | tall | warm grey | candle in a holder |
| 11 | `prop_book` | flat | dusty mint | book |
| 12 | `prop_box` | flat | light oak | cardboard box |
| 13 | `prop_tray` | flat | dusty blue | tray |
| 14 | `prop_board` | flat | warm grey | cutting board |
| 15 | `prop_rug` | flat | soft cream | rolled-up rug |
| 16 | `prop_suitcase` | angular | light oak | suitcase |
| 17 | `prop_crate` | angular | dusty blue | wooden crate |
| 18 | `prop_frame` | angular | warm grey | picture frame |
| 19 | `prop_mirror` | angular | soft cream | hand mirror |
| 20 | `prop_casket` | angular | muted peach | trinket box |
| 21 | `prop_keys` | branching | dusty blue | bunch of keys |
| 22 | `prop_scissors` | branching | warm grey | scissors |
| 23 | `prop_hanger` | branching | soft cream | hanger |
| 24 | `prop_fork` | branching | muted peach | fork |
| 25 | `prop_comb` | branching | dusty mint | comb |
| 26 | `prop_pillow` | soft | warm grey | pillow |
| 27 | `prop_cloth` | soft | soft cream | rag |
| 28 | `prop_scarf` | soft | muted peach | scarf |
| 29 | `prop_mitten` | soft | dusty mint | mitten |
| 30 | `prop_sack` | soft | light oak | sack |

### Template

```
[BASE] , <item description>, main colour <color> <code>,
<shape feature>, [FRAME_PROP]
```

The negative part is shared, unchanged.

### Thirty prompts

Only the middle parts are given; substitute the base block, frame, and
negative part from section 2.

| File | Middle part of the prompt |
|---|---|
| `prop_yarn` | `a ball of yarn, soft cream #F0E2C6, loose thread end curling to one side, visible strand grooves` |
| `prop_ball` | `a child's rubber ball, muted peach #E8B79A, one wide painted stripe, slightly scuffed` |
| `prop_plate` | `a ceramic dinner plate seen from above, dusty mint #A8C9B5, plain rim, one small chip` |
| `prop_clock` | `a round alarm clock, light oak #C9A97C body, blank face with no numbers, two small bells on top` |
| `prop_spool` | `a wooden thread spool, dusty blue #9DB3C4 thread, wound unevenly, wide flanges` |
| `prop_bottle` | `a glass bottle, muted peach #E8B79A tint, cork stopper, tall narrow neck` |
| `prop_vase` | `a ceramic vase, dusty mint #A8C9B5, bulbous body narrowing to the top, empty` |
| `prop_lamp` | `a small table lamp, light oak #C9A97C base, fabric shade, switched off` |
| `prop_jar` | `a storage jar with a lid, dusty blue #9DB3C4, straight sides, empty` |
| `prop_candle` | `a candle in a holder, warm grey #B0A79B holder, unlit wick, wax slightly melted` |
| `prop_book` | `a closed hardcover book lying flat, dusty mint #A8C9B5 cover, no title, worn corners` |
| `prop_box` | `a flat cardboard box, light oak #C9A97C, lid slightly ajar, empty` |
| `prop_tray` | `a serving tray, dusty blue #9DB3C4, shallow raised rim, two side handles` |
| `prop_board` | `a wooden cutting board, warm grey #B0A79B, rounded corners, small hanging hole` |
| `prop_rug` | `a rolled-up rug, soft cream #F0E2C6, tied with a band, seen from the side` |
| `prop_suitcase` | `an old suitcase lying flat, light oak #C9A97C, two latches, worn corner caps` |
| `prop_crate` | `a small wooden crate, dusty blue #9DB3C4, visible plank gaps, empty` |
| `prop_frame` | `an empty picture frame, warm grey #B0A79B, plain moulding, no picture inside` |
| `prop_mirror` | `a small hand mirror, soft cream #F0E2C6 handle, oval glass shown as flat pale surface with no reflection` |
| `prop_casket` | `a small trinket box, muted peach #E8B79A, hinged lid closed, tiny clasp` |
| `prop_keys` | `a bunch of three keys on a ring, dusty blue #9DB3C4, keys splayed apart` |
| `prop_scissors` | `a pair of scissors, warm grey #B0A79B handles, blades half open` |
| `prop_hanger` | `a clothes hanger, soft cream #F0E2C6, wide shoulders, hook curving to one side` |
| `prop_fork` | `a table fork, muted peach #E8B79A handle, four tines, lying flat` |
| `prop_comb` | `a hair comb, dusty mint #A8C9B5, wide teeth, one tooth missing` |
| `prop_pillow` | `a small cushion, warm grey #B0A79B, corner tassels, dented in the middle` |
| `prop_cloth` | `a crumpled cleaning cloth, soft cream #F0E2C6, soft folds, no pattern` |
| `prop_scarf` | `a knitted scarf loosely coiled, muted peach #E8B79A, visible knit texture, fringed ends` |
| `prop_mitten` | `a single knitted mitten, dusty mint #A8C9B5, thumb to one side, cuff ribbing` |
| `prop_sack` | `a small cloth sack, light oak #C9A97C, tied at the neck with cord, slumped` |

### Nuances that are easy to miss

**No food and nothing alive.** The house is abandoned, the clutter must be
inanimate and inedible — otherwise the player starts looking for meaning in
the set.

**Not a single item with text.** A book with no title, a clock with no
numbers, a box with no label. Text on a 52-point tile turns to mud, and the
negative part already forbids it — but it must not be in the item description
either.

**Mirror and glass — no reflections.** Glass is drawn as a matte light
surface. A reflection on the tile is unreadable and breaks the unity.

**Wear, but not destruction.** A chip, a scuff, a missing tooth on the comb —
yes. Broken in half, charred, moldy — no: the audience came to tidy up, not
to sort through a dump.

### Acceptance

Shrink any ten to 52 points, put them side by side, show them to an outside
person: does he confidently say these are ten different things. Not "is it
pretty," but "is it distinguishable."
