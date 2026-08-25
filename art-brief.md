# Art brief — "Rescued Kitten"

Date: 25 August 2026
For an artist or for batch generation with manual curation — either works.

**Verbatim prompts for each asset are in `art-prompts.md`:** the palette in
hex values, shared positive and negative blocks, a prompt for each of the
thirty items, for the six cat silhouettes, for each of the twelve rooms, and
for the small stuff. This file answers "what and why," that one answers "what
to generate it with."

The game concept is in `cat-shelter-mvp.md`, tasks and acceptance are in
`cat-shelter-tasks.md`, the breakdown of generation tools is in
`knowledge/artgen/01-art-pipeline.md`.

---

## 1. What kind of game, in three paragraphs

A clutter-clearing puzzle for iPhone. The player pulls items out of a pile of
junk and arranges them on a shelf with nine slots; three matching items
disappear. The pile sits in layers, and **an item's appearance is invisible
until you've reached it** — under the clutter it looks like a blank tile.

They don't clear it just to clear it. An abandoned house holds a kitten, and
with each cleared room it visibly gets better. The house has twelve rooms,
each with one to four piles of clutter; a level is clearing one pile, meaning
a room is cleared in parts.

On the first screen the player photographs their real cat. The game reads the
coloring and assembles a kitten from ready-made parts so it resembles theirs.
**This is her cat** — that's the whole point, and all the art works toward
making it recognizable and making its improvement visible.

Audience: women 30–55, playing 10–20 minutes in breaks between tasks.

---

## 2. Three requirements that outrank style

If something has to be sacrificed, sacrifice the beauty of a single item, not this.

**First. The cat must be recognizably a cat, not a made-up creature.**
Proportions close to real ones, eyes a bit larger than life. Not a cartoon
critter with human expressions, not a "kawaii" blob. The player compares it to
their own cat lying next to them on the couch.

**Second. The "before and after" difference reads in a 200×400 screenshot in
half a second.** Dirt is muted grey-brown. Clean is light and warm. This is
half the power of the ad creative, and the creative decides whether an install
gets bought. If the "was — became" pair needs studying, the pair doesn't work.

**Third. Visual unity outranks the beauty of any single item.** Mismatched
styles are the first sign of a homemade game. Thirty equally good items beat
five excellent ones and twenty-five mismatched ones.

---

## 3. Style canon

### Works

- rounded outlines, no sharp corners;
- thick soft outline;
- volume via soft diffuse light from upper-left;
- warm muted palette: cream, peach, mint, light wood;
- light like morning in a room.

### Doesn't work

- pixel art — men 25–40 like it, our audience reads it as unfinished;
- darkness and high contrast;
- sharp flat outlines;
- oversweetness: pink with glitter reads as a game for eight-year-olds, and
  adults are the ones paying;
- acid colors.

### Generation prompt

Also serves as the verbal style description for an artist:

> soft 3D cartoon look, top-down view at a 30-degree angle, rounded shapes
> with no sharp corners, thick soft outline, volume via soft diffuse light
> from upper-left, warm muted palette — cream, peach, mint, light wood, no
> acid tones or high contrast, clean flat-color background, subject centered
> in frame

The whole set is generated **with one prompt in one pass**, not as needed.
Style drifts precisely when items are made in batches on different days.

### Reference set for comparison

Royal Match, Gossip Harbor, Merge Mansion, Travel Town, Homescapes.
Screenshots are taken from App Store pages, 6–8 shots per game, and collected
into one folder before work begins (task 0.2).

---

## 4. Technical requirements, common to everything

Basis — `knowledge/artgen/01-art-pipeline.md`, section "Preparing sprites for
Unity".

| Parameter | Value |
|---|---|
| Format | PNG, 8-bit per channel, with alpha channel |
| Background | fully transparent, no halo or white fringe |
| Unity texture type | `Sprite (2D and UI)` |
| Filtering | `Bilinear` (not `Point` — we're not doing pixel art) |
| iOS compression | ASTC |
| Pixels Per Unit | **100**, uniform across the whole project |
| Texture side | power of two, width and height may differ |
| Sheet padding | 2–4 px between sprites |
| Color space | sRGB |

A uniform PPU isn't nitpicking: with different values items will be at
different scales in the same scene, and it shows.

### Naming

Lowercase letters, underscores, no spaces or Cyrillic:

```
prop_<name>.png                 pile item
prop_unknown.png                blank tile (item under the clutter)
prop_locked.png                 locked item
cat_<state>_<fur>_base.png      cat silhouette
cat_<state>_<fur>_<layer>.png   cat layer
room_<nn>_dirty.png             room, before
room_<nn>_clean.png             room, after
reward_bowl.png, reward_blanket.png
map_room_<nn>_<state>.png       house map tile
icon_<n>.png                    app icon
```

### How to deliver

One folder per group, containing files per the naming above, plus
`contact-sheet.png` with thumbnails of the whole group on one sheet. The sheet
is needed for checking visual unity: mismatches only show up when everything
is side by side.

Source files (layers) are delivered separately and not put in the repository.

---

## 5. Full list of work

| Group | Files | Size | Priority | Task |
|---|---|---|---|---|
| Pile items | 30 | 256×256 | P0 | 4.1–4.3 |
| "Under the clutter" blank tile | 1 | 256×256 | P0 | new, from 3.9 |
| Locked item | 1 | 256×256 | P0 | new, from 3.11 |
| Cat silhouettes | 6 | 512×512 | P0 | 4.4 |
| Cat layers | see §7 | 512×512 | P0 | 4.5 |
| Rooms, pairs | 24 | 1024×2048 | P1 | 4.7 |
| Reward items | 2 | 256×256 | P1 | 4.8 |
| House map | 12+3 | see §9 | P0 | new, from 6.2.1 |
| App icon | 5 | 1024×1024 | P0 | 4.9 |
| "Before — after" card frame | 1 | 1080×1080 | P2 | new, from 6.14 |

Order of work is not top to bottom, but: first the **pilot** (§11), then
items, then the cat, then the rooms.

---

## 6. Pile items — 30 pieces

### What this is

Junk from an abandoned house that the player clears: dishware, books, tools,
toys, rags. Each item is drawn separately, in a 256×256 frame, centered, with
margins of about 10% of the side.

### The constraint that outranks beauty

The board can have **six to ten different kinds at once**, and the player
identifies them in a fraction of a second on a tile about 52 points. So any
ten items from the set must differ **by silhouette and color patch**, not by
detail.

This has already gone wrong once: in the prototype two kinds got the same
color, and the level became unreadable. So the set is built by families.

**Six silhouette families, five items each:**

| Family | Outline | Examples |
|---|---|---|
| round | ball, disc | ball of yarn, ball, plate, clock, spool |
| tall | vertical | bottle, vase, lamp, jar, candle |
| flat | horizontal | book, box, tray, board, mat |
| angular | sharp edges | suitcase, crate, frame, mirror, box |
| branching | protruding parts | keys, scissors, hanger, fork, comb |
| soft | shapeless | pillow, rag, scarf, mitten, sack |

**Six color groups**, five items each: cream, peach, mint, light wood,
dusty blue, warm grey. All within the muted palette — no acid tones.

Arrange so that **family and color don't coincide**: the five round items must
be five different colors. Then any sample of ten kinds turns out
distinguishable along at least one axis.

### Acceptance

- 30 PNG files, clean alpha, uniform frame size;
- on the contact sheet the set reads as **one matched set**, not a random mix
  (a human looks at it);
- **distinguishability check:** shrink any ten items to 52 points, put them
  side by side — an outside person must confidently say these are ten
  different things.

---

## 7. The cat — the most expensive and riskiest part

This is task 4.4, and on the task list it's marked as the one the performer
can fail completely. Read this section more carefully than the others.

### How the cat is built

There's no finished cat image. There's a **desaturated base and a set of
black-and-white masks**, and the final color is assembled by a shader at
runtime from traits read off the player's photo. That way the same cat is
shown in every state, and only six sets need to be drawn instead of hundreds
of combinations.

Traits that come from the photo:

```
base_color      ginger | grey | black | white | cream | brown
pattern         solid | tabby | bicolor | calico | tuxedo | pointed
fur_length      short | long
eye_color       green | amber | blue
white_markings  chest, paws, face — any subset
```

### What gets drawn

**Six silhouettes:** three states × two fur lengths.

| State | When | How it looks |
|---|---|---|
| 1 | rooms 1–4 | thin, matted fur, ears back, sitting in a corner |
| 2 | rooms 5–8 | tidier, walking around the room, watching the player |
| 3 | rooms 9–12 | well-groomed, playing, sleeping on the windowsill |

**Layers for each silhouette:**

| File | What it is | How it's used |
|---|---|---|
| `..._base.png` | the whole cat, **desaturated**, light and shadow only | tinted with `base_color` |
| `..._pattern_tabby.png` | mask: white where the stripes are | overlaid with a darker shade of the base |
| `..._pattern_bicolor.png` | mask for the second color | same |
| `..._pattern_calico.png` | mask for patches | same |
| `..._pattern_tuxedo.png` | mask for the "shirtfront" | same |
| `..._pattern_pointed.png` | mask for face, paws, tail | same |
| `..._mark_chest.png` | mask for the white chest patch | filled with white |
| `..._mark_paws.png` | mask for white paws | same |
| `..._mark_face.png` | mask for white on the face | same |
| `..._eyes.png` | eyes only | tinted with `eye_color` |

The `solid` pattern needs no mask — that's just the plain base.

Total per silhouette: **1 base + 5 pattern masks + 3 patch masks + eyes = 10
files.** For six silhouettes — 60.

### Mask requirements

- masks strictly black and white, no halftones at the edges except 1–2 points
  of soft anti-aliasing;
- the mask matches the base **point for point**: same frame, same pose, no
  offset;
- the mask doesn't extend past the base silhouette;
- white patches are drawn so they read against any base color — on a white
  cat they're indicated only by outline and shadow.

### Acceptance, deliberately handed to an outside person

**Show the six silhouettes to an outside person and ask: is this one cat or
different ones?** Answer "one" — accepted. Answer "different" — not accepted,
no matter how much effort went in.

The phrasing is chosen deliberately: the performer almost always reports that
it came out consistent. The judge must be someone who doesn't care either way.

Second check: assemble six different trait sets and confirm you get **six
distinguishable cats**, none of them looking broken — no mask misaligned, no
white patch floating in midair.

**Two attempts, then hire an artist.** This is written into the tasks, and
delaying this decision costs more than making it.

---

## 8. Rooms — twelve, but cleared in parts

### What changed from the previous version

Previously a level equaled a room: clear the pile, the room becomes clean. Now
a room has **one to four piles**, and a level clears one of them. The room
lightens gradually: a corner, a wall, a windowsill.

For the art, this is set up so as not to raise the cost of the work:

- **still two backgrounds per room** — dirty and clean;
- **the clutter is the same thirty items**, laid out over the background;
- "a third cleared" is the same background, just with some items already gone
  from it.

So twelve rooms give thirty-seven levels without a single new room drawing.

### Requirements

| Parameter | Value |
|---|---|
| Size | 1024×2048 (portrait, for phone) |
| Format | PNG without alpha (opaque background) |
| Pair | `room_01_dirty.png` and `room_01_clean.png`, exactly the same room from the same viewpoint |
| Clear space | the bottom third of the frame stays reserved for the shelf and pile, don't put anything important there |

Dirty: muted grey-brown, dim light, dust, peeling wallpaper. Clean: the same
room, light and warm, light like morning, no new furniture — **it must be the
same room, just tidied**, not a different one.

Twelve rooms: hallway, kitchen, living room, bedroom, nursery, study,
bathroom, pantry, attic, porch, corridor, loft. Order isn't mandatory, but the
attic and loft go last — they're the coziest.

### Possible 2x savings that must be verified, not assumed

If "dirty" can be produced as the clean background plus a grey-brown muting
filter plus clutter on top, then the second background isn't needed, and the
work drops from twenty-four files to twelve.

**Don't build this into the plan until one room passes the test.** The artist
will likely tell you the real "before" differs by lighting, not a filter, and
is probably right. But it's worth testing on one room — the savings are large.

### Acceptance

Shrink the pair to 200×400, show it to a person for half a second and ask
which one is clean. The answer must be instant. If it takes staring, the pair
doesn't work.

---

## 9. House map

The screen showing all twelve rooms and how much remains. Needed because the
audience's third-strongest motivator is "completeness, an unfinished set is
nagging," and an unfinished house is exactly that.

Drawn as a cutaway of the house or a grid of rooms — artist's choice, the
cutaway is more legible.

| File | What |
|---|---|
| `map_background.png` | the whole house, empty outline |
| `map_room_<nn>_dirty.png` | room tile, not started |
| `map_room_<nn>_partial.png` | started but not finished |
| `map_room_<nn>_clean.png` | closed out |

Three tile states, twelve rooms. The tile is small — 256×256 is enough. Main
requirement: **states are distinguishable from a distance**, so the whole
house reads at a single glance. Not by shade, but visibly: dark, halfway,
light.

---

## 10. Small but mandatory

### Blank tile — item under the clutter

`prop_unknown.png`. An item not yet reached, showing no appearance. This isn't
a "gray square": the blank tile must read as a covered object — a baggy
silhouette, a rag on top, dust. It should be visible that something is there,
but not visible what.

Appears on screen in large numbers, so it must not be visually noisy.

### Locked item

`prop_locked.png`. One of three obstacles: an item that unlocks after several
completed triples. Overlaid on top of a regular item — so it's a
**semi-transparent layer**: wrapping, rope, ice. It needs to let the item
underneath be guessable.

### Reward items

`reward_bowl.png` — a bowl for the fourth room. `reward_blanket.png` — a
blanket for the eighth. Both appear in the room and visibly change the
kitten's behavior. Drawn in the same style as the items, but a bit richer —
this is a gift, not junk.

Important: they **give no gameplay benefit** and must not look like a
power-up. No glows, arrows, or numbers.

### App icon

Five variants, 1024×1024, no transparency and no rounded corners — the system
applies those. The icon decides more than twelve levels do: it's seen before
the install.

All five must feature the cat. Test: survey ten people — **which of the five
would you tap.** Not "which is prettier."

### "Before — after" card frame

`share_frame.png`, 1080×1080. Frame for the image the player shares: room
before on the left, after on the right, cat in the foreground. Space for the
cat's name is **not provided** — it was decided not to put a name on the
public card.

---

## 11. Order of work: pilot first, then everything else

Don't start with thirty items. Start with a pilot that costs a day and
answers the question "will this even work."

**Pilot, tasks 0.3, 0.3.1, 0.3.2:**

1. three items that read as one matched set;
2. **one cat in two states** — visibly worse and visibly better;
3. **one room, dirty and clean.**

Pilot check — by an outside person, on two questions: "is this one cat or two
different ones?" and "which room is clean?" (half a second).

Why this way. Ad creatives for demand testing are shot **before** the game is
written, and you can't shoot them without a cat and a room. If the pilot
doesn't work out, you learn the project's worst news for zero dollars instead
of three hundred and three weeks.

And separately: **everything that goes into the ads must be made by the same
pipeline as the actual game.** An ad promising art the game can't deliver
turns the measured install cost into a useless number — you'd be measuring
demand for a game that won't exist.

Next in descending order of importance: items (30) → cat (6 silhouettes and
layers) → icon → house map → rooms → small stuff.

The icon ranks above the rooms deliberately: it's seen before the install,
the rooms after.

---

## 12. Acceptance summary

| Group | Machine checks | Human checks |
|---|---|---|
| Items | 30 files, alpha, uniform size | set reads as one matched set; ten pieces distinguishable at 52 px |
| Blank tile, locked | format | visible that something's there, not visible what |
| Cat silhouettes | 6 files | **outsider: one cat or different ones?** |
| Cat layers | masks match the base point for point | six trait sets produce six whole cats |
| Rooms | 24 files, size | difference reads at 200×400 in half a second |
| House map | 3 states × 12 | whole house reads at a glance |
| Icon | 5 files 1024×1024 | **survey of ten: which would they tap** |
| Rewards | format | don't look like a power-up |

General acceptance rule across the whole project: **whoever made it doesn't
accept it.** And wherever the judge is an outside person, they can't be
replaced by your own opinion. This is especially true for art: the author sees
the concept, the player sees the picture.

---

## 13. What's deliberately not in this brief

**Animations.** The MVP has none at all, except simple movement of an item to
the shelf — done in code, not by the artist. The cat in three states is three
static poses.

**Clothing and skins for the cat.** Decided not to sell recoloring: the
coloring comes from the player's photo, and that's the one thing the game
exists for. What will be sold is everything **around** the cat — frames,
lighting, beds — but that's a later phase and isn't described in this brief.

**Interface screens.** Buttons, labels, and layout are done in UI Toolkit with
layout tools, not drawings.

**Twelve different piles.** The pile is assembled from the same thirty items
in a random layout — no need to draw it separately.

</content>

