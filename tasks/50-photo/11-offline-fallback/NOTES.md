# Built and measured, 2026-08-26 — and the honest number is 63%

When the Worker cannot be reached, `CatColour` reads the base colour off the
cropped photo on device and everything else takes a default:
`pattern=solid, fur_length=short, eye_color=green, white_markings=[]`.

Pattern is forced, never guessed. Apple's on-device classifier has 1303
categories and not one of them is a coat pattern
(`knowledge/ios/06-on-device-coat-traits.md`), and no licensed model for it
exists. The task calls this "capped by physics, not effort", which is right.

## How well the colour is read: 17 of 27

Measured against `ground-truth.txt` — the 27 accepted crops, labelled by eye,
written down first so the estimator could be scored instead of tuned by feel.

| approach | score |
|---|---|
| frame average, invented palette anchors | 13/27 (48%) |
| **k-means**, invented anchors | **11/27 (41%)** |
| frame average, anchors measured from the labels | 15/27 (56%) |
| frame average, measured anchors + physical white/cream | **17/27 (63%)** |

Three findings worth keeping:

- **k-means scored worse than the plain average.** The task suggested either;
  clustering loses because the largest cluster of a tabby is its stripes, not
  its coat. Tried, measured, discarded — not assumed.
- **Invented anchors are worse than measured ones.** Saturated orange for
  ginger is nothing like a ginger cat photographed in a warm room.
- **Except for white and cream**, which keep physical anchors: the set holds
  one white cat and no cream one, and the measured white centre landed
  mid-range where it dragged six other cats to "white".

Weighting lightness was tried at 1×, 2× and 4× and made it worse every time
(52%, 44%, 44%). It is not in the code.

**63% means roughly one cat in three gets the wrong base colour.** That is the
ceiling of this method on this data, and further tuning against 27 photographs
would be fitting noise. It is offered as better than nothing on a path that
only runs when the Worker is down — but if a wrong colour reads worse to a
player than a neutral one, the honest alternative is to hand her
`CatTraits.Default` and say the network was out. That is a product call, not a
code one.

## Confirmed inside a real iOS build

With no Worker wired (there is none — `02-traits-worker` waits on the spend
cap), every accepted photo takes the fallback. Three real photographs through
the built app:

```
cat_01.jpg  -> accepted 73096 bytes -> cat ready (OfflineColourOnly): short black solid, green eyes
blurry_01.jpg -> accepted 40240 bytes -> short brown solid, green eyes
cat_15.jpg  -> accepted 84956 bytes -> short brown solid, green eyes
```

Two of the three are right by eye (`cat_01` is a black cat, `cat_15` a brown
tabby); `blurry_01` is a ginger cat called brown, which is the 37% showing up
exactly where the measurement said it would.

No error screen appears on any of them, which is what the task asks for.

## Left open

VERIFY 1 wants the Worker's domain blocked on a device. There is no Worker and
no device yet, so what was exercised is the same branch by the same route —
the call is absent rather than blocked.
