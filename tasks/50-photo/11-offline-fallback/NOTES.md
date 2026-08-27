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

## Fixed, 2026-08-27 — the uncaught-exception path an independent VERIFY.md found

`CaptureScreen.Handle` wrapped the Worker call in `try/catch` but not the
three lines below it: `CatTraits.FromColourOnly(colour)` throws
`ArgumentException` for any base colour outside the six-name palette, and
nothing caught it. Confirmed by mutation, outside the repo: it throws
uncaught, which stops the coroutine before `OnCatReady` fires — worse than
the error screen this task exists to avoid, since the player sees nothing
at all rather than something.

**Guarded.** `CaptureScreen.cs` now wraps the colour-only branch too and
falls back to `CatTraits.Default` on any `ArgumentException`, logging the
rejected colour. Same shape as the Worker's own catch three lines above it.

**Caused by**: `CatColour.swift`'s `palette` and `CatTraits.Allowed["base_color"]`
are two copies of one set of six names with nothing keeping them in sync —
the same shape as the Worker-schema drift `CatTraitsTests` already guards.
Added `Tests/Core/CatColourPaletteParityTests.cs`: reads the Swift file,
extracts the palette names by regex, and asserts they equal
`CatTraits.Allowed["base_color"]` as sets — fails (not `Assert.Ignore`) if
the file can't be found, matching the rule the project settled on today.

Proved it can fail: added a seventh colour to a Swift copy outside the repo.
The test failed with:

> CatColour.swift's palette and CatTraits.Allowed["base_color"] have
> drifted: a name only one side knows about is exactly what makes
> CatTraits.FromColourOnly throw.

`dotnet test build/core-tests/core-tests.csproj -v q --nologo`: 161 passed, 0
failed. `.venv/bin/python -m pytest tools/ -q`: 156 passed, 0 failed (counts
moved from other agents' concurrent work this session; no failures either
side). `build/check-core-purity.sh`: engine-free, OK.

`verify:` is left `failed` — the finding that earned it is fixed, but the
fix itself has not been independently checked, per the same rule that
applies to everyone else's fixes in this tree.
