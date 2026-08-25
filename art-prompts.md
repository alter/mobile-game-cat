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

### English only

Prompts are given in English: every verified image-generation model is
noticeably more reliable in English, and the entire style vocabulary of the
industry is in it. Explanatory prose is for humans and never goes into the
prompt.

### What this file deliberately does not contain

**Output size.** No prompt here states a resolution, because the size is a
generation parameter rather than part of the prompt, and duplicating it in two
places guarantees the two eventually disagree. The single source is the table
in section 5 of `art-brief.md`. Generate at least at the size given there — a
larger render downsampled is fine, an upscale is not.

**Format and delivery rules.** PNG, alpha, colour profile, pivot, what gets
baked into the shadow, and why nothing is vector — all in section 4 of
`art-brief.md`. Read it before the first pass, not after.

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

---

## 4. The cat

### The general idea to keep in mind

The player compares this cat **to their own cat, lying next to them on the
couch.** Everything else follows from this: realistic proportions, eyes a
bit larger than life but not saucers; no human facial expressions, no
eyebrows; fur reads as texture, not individual strands.

The cat goes through three states, and the transition between them is **half
the meaning of the game**. The difference must read instantly, but it's the
same animal: not a thin black kitten at the start and a fluffy ginger one at
the end, but one cat that got better.

| State | Rooms | Pose | Fur | Ears, tail | Gaze |
|---|---|---|---|---|---|
| 1 | 1–4 | sitting hunched in a corner | matted, sticking out in tufts, dull | ears flattened, tail wrapped around the paws | looking away, not at the viewer |
| 2 | 5–8 | standing, walking | tidy, not yet glossy | ears up, tail lowered but not tucked | looking at the viewer |
| 3 | 9–12 | lying on the windowsill, paws tucked | sleek, glossy | ears relaxed, tail resting loosely | eyes half closed, content |

### Correction to the previous version

In `cat-shelter-mvp.md`, section 10, it says the cat is assembled from parts —
body, head, tail, paws. **This is wrong for our case.** Parts pay off only
with animation, and the MVP has none, and the pose changes wholesale from
state to state. Splitting into parts would create seams and triple the work.

Correct approach: **a whole silhouette per state**, with coloring applied in
layers on top.

### What the neural network generates and what it doesn't

Generated: **six silhouettes** — three states × two fur lengths.

**Masks are not generated.** The pattern mask and the white-patch mask must
match the base point for point. A neural network will never give you that: it
will redraw the cat from scratch. Masks are made by **editing the finished
silhouette** in an editor — trace the areas, fill white on black. This is
manual work, and it must be budgeted into the schedule.

Same for fur length: if you generate "the same cat but fluffy" as a separate
prompt, you get two different cats. The right way is to generate the
short-haired one, and get the long-haired one by editing the same image or by
generating with a reference.

### Prompts for the six silhouettes

Shared part for all: base block, item frame, negative part — plus additionally
into the negative:

```
collar, clothing, accessories, human hands, background objects,
multiple cats, kitten and adult cat together, cat food, bowl
```

**Important:** the silhouettes are generated **desaturated** — coloring is
applied by code.

| File | Middle part of the prompt |
|---|---|
| `cat_1_short_base` | `a thin young short-haired cat sitting hunched in a corner, desaturated greyscale fur with no colour, matted uneven coat sticking out in tufts, ears flattened back, tail wrapped tightly around the front paws, looking to the side away from the viewer, realistic cat proportions, eyes only slightly larger than life, dull lifeless coat` |
| `cat_2_short_base` | `the same short-haired cat now standing and mid-step, desaturated greyscale fur with no colour, coat tidy but not yet glossy, ears upright, tail lowered but not tucked, looking directly at the viewer, calm and curious, realistic cat proportions` |
| `cat_3_short_base` | `the same short-haired cat lying on a windowsill with paws tucked under, desaturated greyscale fur with no colour, sleek glossy coat, ears relaxed, tail resting loosely alongside the body, eyes half closed and content, realistic cat proportions` |
| `cat_1_long_base` | `identical pose and framing to cat_1_short_base but long-haired, desaturated greyscale fur, matted clumped long coat, visible tangles, ruff around the neck` |
| `cat_2_long_base` | `identical pose and framing to cat_2_short_base but long-haired, desaturated greyscale fur, coat combed out, full tail plume` |
| `cat_3_long_base` | `identical pose and framing to cat_3_short_base but long-haired, desaturated greyscale fur, soft flowing coat, thick tail curled alongside` |

The phrase "identical pose and framing" won't work on its own — for the
long-haired variant you must feed the short-haired version in as a reference
image.

### Layers made by hand

For each of the six silhouettes:

| File | What to draw | Nuance |
|---|---|---|
| `..._pattern_tabby` | stripes across the back and sides, rings on the tail, an "M" on the forehead | stripes narrow toward the belly, don't reach the chest |
| `..._pattern_bicolor` | lower half of the body and paws | uneven border, running along the shoulder and hip |
| `..._pattern_calico` | three to four large uneven patches | patches asymmetric, one must cover an ear |
| `..._pattern_tuxedo` | chest bib, paws, tail tip | bib shaped like a drop, narrowing downward |
| `..._pattern_pointed` | face, ears, paws, tail | edges soft, not sharp |
| `..._mark_chest` | chest patch | oval, offset from center |
| `..._mark_paws` | "socks" on all four paws | different heights, back ones higher |
| `..._mark_face` | face patch | asymmetric, covers one eye or the nose |
| `..._eyes` | irises only, no eyelids or sclera | almond-shaped, vertical pupil |

Masks strictly black and white, edge anti-aliasing no more than two points,
don't extend past the base silhouette.

**Separate nuance about the white cat.** White patches on a white cat aren't
visible. So patch masks are drawn to read via outline and soft shadow, not
just fill: the patch edge gets a slightly darker contour of the same shade.

### Acceptance

Show the six silhouettes to an outside person: **is this one cat or
different ones?** Answer "different" — not accepted, no matter how much
effort went in.

---

## 5. Twelve rooms

### Main nuance: the pair cannot be generated with two prompts

If you generate a "dirty living room" and then a "clean living room," you get
two different living rooms. The window will move, the sofa will change shape,
and the whole power of the "before — after" pair will be lost.

Correct order:

1. Generate the **clean** room — it's harder and sets up the layout.
2. Get the dirty one by **editing the same image**: mute it toward
   grey-brown, add dust, peeling wallpaper, scattered clutter. Or by
   generating with the clean room as a reference and the prompt "the same
   room, neglected."

The clean room is generated first deliberately: spoiling is easier than
tidying.

### Shared part of the prompts

Base block, room frame, negative part — plus additionally into the negative:

```
people, cat, animals, modern electronics, television, computer,
open flame, mould, insects, rubbish bags, broken glass, decay, rot,
text on walls, posters with writing
```

The cat and clutter are overlaid by code, they must not be in the room itself.

### Clean rooms

Middle parts of the prompt; each starts with the base block.

| File | Middle part |
|---|---|
| `room_01_clean` | `a small tidy entrance hall, coat hooks on the wall, a bench with a cushion, a round mirror, morning light through the door glass, warm and welcoming` |
| `room_02_clean` | `a tidy cottage kitchen, open shelves with plain crockery, a kettle on the stove, checked curtain, sunlight on the counter` |
| `room_03_clean` | `a tidy living room, a soft sofa with cushions, a low table, a rug, tall window with light curtains, warm afternoon light` |
| `room_04_clean` | `a tidy bedroom, a made bed with a folded quilt, a bedside table with a lamp, a small window, soft calm light` |
| `room_05_clean` | `a tidy child's room, a low bed, a shelf of toys neatly arranged, a small rug, bright cheerful light` |
| `room_06_clean` | `a tidy study, a wooden desk, a chair, a bookshelf with upright books, a desk lamp, quiet focused light` |
| `room_07_clean` | `a tidy bathroom, a claw-foot tub, folded towels on a rack, a small window with frosted glass, clean bright light` |
| `room_08_clean` | `a tidy pantry, wooden shelves with jars and baskets in rows, a step stool, cool even light` |
| `room_09_clean` | `a tidy attic, sloped ceiling, a round window, a few neatly stacked boxes, a rocking chair, dusty golden light` |
| `room_10_clean` | `a tidy veranda, wicker chair, potted plants, wooden railing, view of green outside, bright open light` |
| `room_11_clean` | `a tidy corridor, a runner rug, framed empty pictures on the wall, doors along one side, soft even light` |
| `room_12_clean` | `a tidy loft room under the roof, a window seat with cushions, low bookshelf, warm evening light through a skylight` |

### Dirty variants

Editing the clean room. Prompt for generation with a reference:

```
[BASE] , the same room, neglected and long abandoned,
overall colour shifted to muddy taupe #6B6055 and dull umber #55493D,
dim grey light instead of warm light, dust in the air,
wallpaper peeling at the seams, cobwebs in the upper corners,
furniture out of place and covered with dust sheets,
scattered clutter on the floor, curtains sagging,
identical camera angle, identical furniture layout, identical window position,
[FRAME_ROOM]
```

The key words here are the last three: **same angle, same furniture, same
window.** Without them the pair drifts apart.

### Nuances

**Abandoned, but not destroyed.** Dust, cobwebs, peeling wallpaper — yes.
Mould, holes in the floor, broken glass, trash bags — no: the audience came
to create coziness, not to clear out a condemned building. This is also in
the negative part.

**Bottom third of the frame empty.** That's where the shelf and pile will go.
In the clean room the floor and maybe a corner of the rug can land there;
nothing important — windows, furniture, the cat — should be there.

**Light is the one variable you can't skimp on.** Two-thirds of the
"before — after" difference is made by light, not clutter. The dirty room is
grey-brown and dim, the clean one is warm and bright. If that's not the case,
the 200×400 acceptance check won't pass.

**Room order.** The attic, veranda, and loft go last — they're the coziest,
and they coincide with the cat's third state and the last rewards.

---

## 6. The rest of the assets

### Blank tile — item under the clutter

```
[BASE] , an unidentifiable object hidden under a dust sheet,
warm grey #B0A79B cloth draped over an unknown shape,
soft folds, a little dust, the shape underneath unreadable,
calm and quiet, not spooky, [FRAME_PROP]
```

Additionally into the negative: `ghost, skull, face, eyes, anything
recognisable under the cloth`.

Nuance: there can be as many as thirty of these tiles on screen at once. It
must be **quiet** — minimal detail, even tone, or the board will get noisy.

### Locked item

```
[BASE] , a length of rough twine wound crosswise several times,
warm grey #B0A79B cord, tied in a simple knot at the centre,
rendered as a standalone overlay on transparent background,
nothing underneath, [FRAME_PROP]
```

This is an **overlay on top of a regular item**, so the middle must stay
open — the item underneath should be guessable through the rope. Into the
negative: `chain, padlock, metal, rust, prison bars`. A lock and chain read
as punishment, and ours is a game about care.

### Rewards

```
reward_bowl:
[BASE] , a ceramic cat bowl, dusty mint #A8C9B5,
low and wide, empty, a small paw print painted on the side,
slightly nicer and cleaner than ordinary household objects,
[FRAME_PROP]

reward_blanket:
[BASE] , a small folded blanket for a cat, muted peach #E8B79A,
soft knitted texture, neatly folded in three, one corner turned over,
slightly nicer and cleaner than ordinary household objects,
[FRAME_PROP]
```

Mandatory in the negative: `glow, sparkle, magic aura, rarity border,
star, badge, plus sign, number`. Rewards **must not look like a power-up** —
the moment the bowl starts glowing, care turns into gear math.

### App icon, five variants

One prompt, five passes with a different middle part:

| Variant | Middle part |
|---|---|
| 1 | `close-up of a content cat face, warm cream background, no objects` |
| 2 | `a cat sitting inside a cosy cleaned room seen through a doorway` |
| 3 | `a cat peeking out from behind a cardboard box` |
| 4 | `split composition, dull grey clutter on the left, warm tidy room with a cat on the right` |
| 5 | `a cat curled asleep on a folded blanket, seen from above` |

Different frame: `square composition, fills the entire frame, no margins,
no rounded corners, no transparency`. Rounding is applied by the system.

Acceptance — survey ten people: **which of the five would you tap.** Not
"which is prettier."

### House map

```
map_background:
[BASE] , a cutaway view of a small two-storey house,
twelve empty rooms arranged in a grid inside the walls,
plain interior, roof and outer walls in light oak #C9A97C,
rooms empty and unfurnished, seen straight on, [FRAME_ROOM]
```

Room tiles — three states, distinguishable from a distance: `dirty` dark
grey-brown, `partial` half light with a clear boundary, `clean` warm light.

Nuance: the distinction is made by **lightness, not hue.** The map is viewed
at a glance and as a whole; a color difference doesn't read at that size, a
lightness difference always does.

### "Before — after" card frame

```
[BASE] , a simple square photo frame divided into two equal halves
by a thin vertical line, light oak #C9A97C moulding, both halves empty,
rendered as an overlay on transparent background, [FRAME_PROP]
```

Space for the cat's name is **not provided** — it was decided not to put a
name on the public card.

---

## 7. Order and acceptance

Generate in this order, and accept each step before the next:

1. **Pilot:** three items from different families, one cat in states 1 and 3,
   one room as a pair. This answers the question "will this even work," and
   it's needed before the ad creatives.
2. Thirty items in one pass.
3. Blank tile and locked overlay.
4. Six cat silhouettes, then masks by hand.
5. Five icons.
6. House map.
7. Twelve rooms: all clean ones first, then all dirty ones.
8. Rewards and frame.

Curation after each pass is **manual**. The neural network delivers something
usable about half the time, and you should budget two to three passes per
item.

Three checks that can't be delegated to a machine and can't be delegated to
yourself:

- ten items at 52 points — are they distinguishable (outsider);
- six cat silhouettes — one cat or different ones (outsider);
- a room pair at 200×400 for half a second — which one is clean (outsider).

The author always sees the concept, the player sees the picture.
</content>


