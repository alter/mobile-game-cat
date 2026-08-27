# VERIFY — 40-art/03-cat-silhouettes

Result: **partial** — three of six delivered, and they pass every check that
does not need a person. The two outsider items are untouched and this verifier
is barred from standing in for them.

Delivered 2026-08-27 by the owner, into `game/Assets/Resources/Art/`.

## Item 1 — files, size, greyscale: PASS, measured

| file | size | mode | transparent | mean saturation |
|---|---|---|---|---|
| `cat_1_short_base.png` | 1024×1024 | RGBA | 59% of frame | 0.025 |
| `cat_2_short_base.png` | 1024×1024 | RGBA | 57% | 0.041 |
| `cat_3_short_base.png` | 1024×1024 | RGBA | 61% | 0.021 |

The handful of pixels that measure as saturated are near-black (RGB values of
2–7) — contour and shadow, not colour. There is no hue in the fur.

1024, not the 512 the task asked for until the same day: `art-brief.md` §5 had
already overruled that figure and the task had not caught up. The files match
the corrected size.

## Naming: correct as delivered

The three states landed on the right numbers without renaming — 1 sits hunched
with ears down and gaze away, 2 stands facing the viewer, 3 lies with paws
tucked and eyes closed. That is the table in `art-prompts.md` §4.

## What is missing against the task as written

Three of six: only the short-haired column. The long-haired variants are not
delivered, so a long-haired cat will be rendered short — a deliberate cut, and
`60-shell-build/18-coat-shader` requires that fallback to be logged rather than
silent.

## Found while checking: no outline, and it is fixed in code, not in the art

`art-prompts.md` §1 makes one outline the rule for the whole set — `#4A3B28`,
about 3% of the short side. Measured as the difference between the mean
lightness of a 6px rim and the interior:

| | rim vs interior |
|---|---|
| `prop_vase` | 58.8 |
| `prop_book` | 67.0 |
| `prop_ball` | 62.9 |
| `cat_1_short_base` | 4.9 |
| `cat_2_short_base` | 1.7 |
| `cat_3_short_base` | 10.8 |

The props carry the canon outline; the cats carry none, and beside a prop they
read as a different game. Not sent back for regeneration: the outline is built
from the alpha edge at load time in the coat shader, so it applies to these
three and to anything delivered later, and the source files stay clean.

## Two observations, neither blocking

- The three states differ by **pose and expression, not by body or coat**. The
  brief asked state 1 for matted fur in tufts and a thin cat; what arrived is
  the same well-fed cat looking miserable. Whether "worse to better" still
  reads is exactly what item 3 below asks a person, so it is not settled here.
- The eyes are drawn dark. Until `40-art/04` supplies an eyes mask there is
  nothing for `eye_color` to tint, so every player's cat has dark eyes.

## Items 2 and 3 — answered by the owner, 2026-08-27

**Item 2, one cat or different cats: PASS.** Answer: "да".

**Item 3, do the three read worse-to-better: PASS on the letter, FAIL on the
intent.** Answer, verbatim: "да, но дело в том что коту становится лучше не как
будто он из подвала вылез, а как будто голодного кота покормили".

That distinction is the whole arc. The game is twelve rooms long and its
payment to the player is that the cat was *rescued*; a cat that reads as merely
*fed* pays a smaller reward for the same work. The reason is measurable and was
already noted above before the answer came in: the three files differ by pose
and face while the body and the coat are identical between them. Being fed is a
change of face. Being rescued is a change of coat and frame, and none was drawn.

**Consequence: state 1 needs redrawing; states 2 and 3 stand.** State 1 carries
the whole "before" — matted fur in tufts, a thin frame, a dull coat — and it is
the only one of the three where the brief's own wording was not met. The other
two are the "after" and they are right.

It must be regenerated **from one of the existing three as a reference image**,
not from a fresh prompt, or the result is a different cat and item 2 stops
passing. This is the same rule that governs the long-haired variants
(`art-prompts.md` §4).

Note also that the owner is the sole respondent here and is not an outsider.
For a decision this size that is enough — the arc either reads or it does not,
and it did not. If the redraw is contested later, ask someone who has not seen
the code.

## How to reproduce the numbers

```bash
.venv/bin/python - <<'PY'
from PIL import Image, ImageFilter
import numpy as np, glob, colorsys
A = "game/Assets/Resources/Art"
for f in sorted(glob.glob(f"{A}/cat_*.png")):
    im = Image.open(f).convert("RGBA"); px = im.load(); w, h = im.size
    sats = [colorsys.rgb_to_hsv(*[c/255 for c in px[x, y][:3]])[1]
            for x in range(0, w, 4) for y in range(0, h, 4) if px[x, y][3] > 200]
    print(f, im.size, "mean saturation", round(sum(sats)/len(sats), 3))

def rim(path, edge=6, core=25):
    a = np.array(Image.open(path).convert("RGBA"))
    rgb = a[..., :3].astype(float).mean(axis=2)
    m = Image.fromarray((a[..., 3] > 200).astype(np.uint8) * 255)
    band = (np.array(m) > 128) & ~(np.array(m.filter(ImageFilter.MinFilter(2*edge+1))) > 128)
    inner = np.array(m.filter(ImageFilter.MinFilter(2*core+1))) > 128
    return round(rgb[inner].mean() - rgb[band].mean(), 1)

for n in ("prop_vase", "prop_book", "prop_ball",
          "cat_1_short_base", "cat_2_short_base", "cat_3_short_base"):
    print(n, "rim vs interior:", rim(f"{A}/{n}.png"))
PY
```
