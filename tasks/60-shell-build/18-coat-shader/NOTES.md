# Half of it works, 2026-08-27

`game/Assets/View/CoatBuilder.cs` builds a coloured cat from one greyscale
silhouette. `CoatGridView` renders every coat colour against every state; drop
a `coat.txt` next to the save to see it. The grid was photographed on the
Android emulator and is the evidence for what follows.

## What ships

| step | what it does |
|---|---|
| `Reshape` | narrows the body 15% for state 1, widens 5% for state 3, head untouched |
| `Weather` | dulls the coat, coarsens it, dirties the underside — full for state 1, a trace for state 2 |
| `Tufts` | strands grown along the contour normal, in clumps |
| `Tint` | multiplies the coat colour into the greyscale, so the modelling survives |
| `Outline` | the canon rim the cats were delivered without |

Three states out of three files, six colours, no masks.

## Three things learned the hard way, each from a screenshot

**The textures are not readable and should stay that way.** `GetPixels32` threw
`texture data is either not readable`. Marking them Read/Write keeps a second
copy in memory for the whole run to serve one pass at load, so the pixels come
through a GPU blit into a temporary RenderTexture instead. Works on any
texture whatever its import settings, including files delivered later.

**Do not tint the shadow.** The cats are drawn with a soft semi-transparent
shadow beneath them. Tinting everything with alpha above zero turned it into a
coloured puddle — a yellow smear under a cream cat. Only pixels at alpha > 200
are coat; the rest keeps its own neutral colour, and the outline skips them too
so the shadow does not get a dark ring.

**Lightness cannot find eyes.** Eye colour was applied by tinting the darkest
pixels, and the first grid came back with amber smears under every sleeping
cat: the deep shadow beneath a curled body is darker than an eye. The tint is
removed. Every player's cat has the dark eyes the silhouette was drawn with
until `40-art/04` supplies an eyes mask — which is what that mask is for.

## The masks: computed, not drawn — added later the same day

`CoatMasks` derives all nine from the silhouette. 40-art/04 forbids generating
them, and for a good reason — a model asked for "the same cat but striped"
draws a different cat, and a mask one pixel off its base is worse than none.
Deriving them from the base has neither problem: the base is the input, so
alignment is exact by construction.

| mask | how |
|---|---|
| eyes | a pair of dark blobs at the same height on the head, found not drawn |
| pointed | distance from the body's centre — ears, muzzle, paws, tail are the far parts, which is what pointed means |
| paws | the bottom of the figure |
| face, chest | ellipses placed from the eyes and the head box |
| tuxedo | chest plus paws, which is what a tuxedo is |
| bicolor, calico | value noise, a few large zones or more small ones |
| tabby | near-vertical wavy stripes, denser along the back |

Two of the three cats have their eyes shut, so their eye mask comes out empty —
correct, there is nothing to colour, and it means one mask was needed where the
task budgeted three.

**A drawn file always wins.** `CoatBuilder.MaskOf` looks for
`Art/<base>_<mask>.png` before using the computed one, so the 27 masks can be
drawn one at a time, whenever any single one is worth drawing, and each lands
as an improvement with no code change.

**The weak one is tabby.** Real stripes follow the animal; these follow the
frame. It reads as a striped cat rather than as a photograph of one, and on the
sleeping silhouette the stripes run the wrong way across a curled body. Three
hand-drawn stripe masks would replace it and nothing else has to change.

## Three more defects the grid caught

**Freckles instead of patches.** Bicolor and calico came out as speckle because
the noise scale was inverted — the count is patches across the frame, so fewer
means larger. Bicolor is 4, calico is 7.

**Rectangles instead of anatomy.** Chest and face were axis-aligned boxes and
looked it: a bib and a stripe across the muzzle. Ellipses placed from the eye
positions instead.

**Grey paws.** A white marking multiplied by the greyscale value is not white,
it is grey — the shading has to be compressed rather than applied at full
depth, or every white sock comes out dirty.

## Not done

- **Mask mode for hand-drawn files is written but never exercised**: no drawn
  mask exists to load, so that path has not run once.
- The long-haired fallback logs and works, but there is no long-haired art to
  fall back *from*, so it has only been exercised in the direction that is
  missing.

---

# Looking at the grid properly, 2026-08-27 (evening)

`grid-2026-08-27.png` is now in this directory — the Android emulator at
1080×2340, all six patterns against all three states, ginger, green eyes,
markings chest+paws. VERIFY item 5 asks for the picture rather than the
sentence; here it is. Everything below is read off that picture.

**It contradicts what this note said earlier, and what
`40-art/04-cat-layers` was told on the strength of it.** The claim was that
tabby is the one weak mask. That is not what the grid shows.

| row | what the picture actually shows |
|---|---|
| solid | correct |
| tabby | **works, on all three states** — including the sleeping cat, where the stripes were predicted to run the wrong way |
| bicolor | one large dark patch on the standing cat; on sitting and sleeping, near-indistinguishable from solid |
| calico | a dark blotch on the sitting cat's shoulder, almost nothing on the other two |
| tuxedo | **cannot be judged from this grid at all** — see below |
| pointed | slightly darker extremities on the sitting cat, effectively invisible on the other two |

## The harness was asking the wrong question

Every row was rendered with markings `{chest, paws}`. A tuxedo *is* a white
chest and white paws, so the tuxedo row was being compared against five other
cats already wearing one. Worse, the same white bib and socks sat on top of
every pattern and dominated the cell, so the pattern underneath went unread.

`CoatGridView` now renders the pattern rows with **no markings**, and gives
markings their own row on a solid cat. The picture attached here was taken
before that change and is kept as the record of what was actually seen; a
fresh run is needed before anyone decides which masks to draw.

## What the picture does establish, and what it does not

Established, from this run: six colours are distinct and none is flat (item 1
of VERIFY); procedural mode draws every pattern without throwing or rendering
blank, because no drawn mask exists and this is that path (item 3); the eyes
are green where the eyes are open and correctly absent on the two states whose
eyes are shut.

Not established: item 2 (a white cat with all three markings, judged by
someone other than the author — this grid is ginger with two markings); item 4
in the direction that matters, since there is no long-haired art to fall back
*from*; and mask mode, which has still never loaded a file because no drawn
mask exists.

## Two markings look wrong, and this is the honest finding

On the **sleeping** cat the white chest lands as a circle on the flank — the
cat is curled, and the geometry that places a chest on a standing animal has
nothing to attach to. On the **sitting** cat the white paws spread into a
broad band along the bottom that merges with the tail. On the standing cat
both are right.

So the masks worth drawing, in order, are the markings on the two non-standing
states — not the stripes. `40-art/04-cat-layers` has been corrected.
