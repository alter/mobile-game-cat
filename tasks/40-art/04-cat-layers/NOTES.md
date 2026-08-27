# From 54 files to "draw only what is wrong", 2026-08-27

## What changed

This task used to demand 54 hand-traced masks — 9 per silhouette × 6
silhouettes — and it was P0, because without them the coat shader had nothing
to read. `60-shell-build/18` removed that dependency: `View/CoatMasks.cs`
derives all nine masks from the silhouette itself at load time, and
`CoatBuilder.MaskOf` prefers a drawn file whenever one exists:

```csharp
var drawn = Resources.Load<Texture2D>($"Art/{baseName}_{maskName}");
```

So a drawn mask still wins, one at a time, with no code change — and the game
has coats in the meantime. The task is therefore **P0 → P2**: the hook ("it is
her cat") needs *a* coat, and a coat now exists.

**Flagged for the owner rather than buried:** downgrading a P0 is a real
decision and this file is where to argue with it. The case for keeping P0
would be that a computed coat is not good enough to sell the hook. That is a
judgement to make by looking at the pattern grid on a device, which is what
VERIFY item 4 now asks for.

## Why deriving masks does not break the "no model-generated masks" ban

The ban (art-prompts.md §4, `03-cat-silhouettes/NOTES.md`) exists because a
model asked for "the same cat but striped" draws a *different cat*, and a mask
one pixel off its base is worse than no mask at all. Computing from the base
has neither failure: the base is the input, and every mask is clipped to the
base's own alpha before use — which is this task's VERIFY item 1, satisfied by
construction rather than by inspection.

## How each mask is obtained today

| mask | derivation |
|---|---|
| eyes | the darkest paired blobs in the upper 45% of the figure, matched by height and size |
| pointed | 66th-percentile distance from the body's centre of mass — ears, muzzle, paws, tail |
| paws, chest, face | geometry: the bottom of the figure, and ellipses placed from the eyes |
| tuxedo | chest plus paws, which is what a tuxedo is |
| bicolor, calico | value noise at two scales, 4 and 7 cells |
| tabby | near-vertical wavy stripes, period 7.5% of width, fading back to belly |

Two of the three silhouettes have their eyes shut, so their eye mask comes out
empty — correctly, there is nothing there to colour. One mask needed where this
task budgeted three.

## Which ones are actually wrong — corrected the same evening

The first version of this note named **tabby** as the one weak mask, reasoning
from how the stripes are generated: they follow the frame, not the animal, so
on a curled cat they should cross the body the wrong way.

**Looked at the grid instead of reasoning about it, and that is wrong.** The
picture is `tasks/60-shell-build/18-coat-shader/grid-2026-08-27.png`. Tabby
reads on all three states, the sleeping one included. What is wrong is:

1. **The white chest on the sleeping cat** — a circle on the flank. The
   geometry places a chest where a standing animal keeps one, and a curled cat
   has nothing there.
2. **The white paws on the sitting cat** — a broad band along the bottom that
   merges with the tail. Reads as a puddle, not as socks.
3. **bicolor, calico and pointed** are near-indistinguishable from solid on
   most states. Too faint rather than wrong, which is a weaker case for
   drawing than the two above.

**And the grid could not judge tuxedo at all.** It drew a white chest and
white paws on every row, and a tuxedo is exactly a white chest and white paws
— so the tuxedo row was compared against five other cats wearing one, and the
patterns underneath were dominated by the same bib in every cell.
`CoatGridView` now renders pattern rows with no markings and gives markings
their own row. A fresh run is needed before anything is drawn.

Recorded at length because the mistake is the project's recurring one: a
confident claim about pixels, derived from the algorithm rather than from
looking at the output.
