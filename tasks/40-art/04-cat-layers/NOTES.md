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

## The one that is actually wrong

**Tabby.** The stripes follow the frame, not the animal. On the sleeping state
they cross a curled body in the wrong direction. It reads as a striped cat and
not as a photograph of one. That is the case for drawing, and it is three files
(or six with long fur), not 54.

Everything else is either geometry or something nature makes irregular anyway,
where a computed mask and a drawn one are hard to tell apart.
