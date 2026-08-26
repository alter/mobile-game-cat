# What Vision actually does with our reference set

Measured 2026-08-26 over all 41 images of `fixtures/reference-photos`, through
`VNRecognizeAnimalsRequest`. The knowledge file for this task says Apple
publishes no recommended confidence threshold and that no reliable source could
be found on whether Vision confuses a photo-of-a-photo with a live animal.
Both gaps are now filled with our own numbers.

| category | n | animal found | what Vision said | mean confidence |
|---|---|---|---|---|
| cat | 20 | 18 | Cat ×18 | 0.70 |
| dog | 5 | 5 | Dog ×5 | 0.74 |
| multi | 3 | 3 | Cat ×3 | 0.78 |
| blurry | 5 | 4 | Cat ×4 | 0.73 |
| empty | 5 | 0 | — | — |
| ofphoto | 3 | 2 | Cat ×2 | 0.63 |

Nothing was misclassified across species: no dog was ever called a cat, and no
empty frame produced a detection. The failures are all "found nothing", never
"found the wrong thing".

## The finding that matters: a photo of a screen passes as a cat

Two of the three photographs-of-a-photograph came back **Cat**, at 0.64 and
0.62. Vision has no notion of liveness, and nothing in this pipeline can tell a
player's own cat from a cat someone photographed off a monitor.

**And the threshold cannot separate them.** Confidence on real cats runs
0.60–0.81; the two screen shots sit at 0.62 and 0.64 — inside that range, with
four genuine cats scoring *below* them:

```
cat_14 0.60   cat_03 0.60   cat_12 0.60   cat_16 0.61
ofphoto_03 0.62   cat_02 0.62   ofphoto_01 0.64
```

A threshold at 0.65 would reject both screen shots and eight real cats with
them. So the 0.6 in `06-outcome-handling` ("ours to tune against the reference
set") cannot be tuned to solve this; it can only trade real cats for fake ones.

That is worth knowing before the concept leans on it. The premise is "it is
*her* cat" — and the game cannot verify that, at any threshold. Whether that
matters is a product question: a player who uploads a cat off the internet is
cheating only herself, and the metric that decides the project ("uploaded a
photo") counts her either way.

The third screen shot — the one taken in **portrait mode** — was not recognised
at all. The phone blurs the background before Vision ever sees the image, and
that was enough. Worth remembering when the capture screen tells a player why
her photo was rejected: the answer may be her camera's setting, not her cat.

## The three misses

| file | why, most likely |
|---|---|
| `blurry_04` | sharpness 34 — the second-blurriest in the set |
| `cat_10` | 259×270 px, the smallest image in the set |
| `cat_20` | 423×418, two kittens filling the frame |

Small and blurred is the pattern; a phone photo will be neither, so the
practical miss rate should be lower than 2 in 20. That is an expectation, not a
measurement — a real camera roll would settle it (`own_*.jpg`, see
`01-reference-photo-set`).

## The plugin

- `Assets/Plugins/iOS/CatVision.swift` — `@_cdecl` entry points, JPEG bytes in,
  JSON out. Orientation is a required argument, not an option: Vision keeps
  none of its own and mis-detects silently when it is wrong, which the
  knowledge file names as the classic cause of "recognition doesn't work". 0
  means "read the file's own EXIF".
- Bounding boxes are returned in **pixels with the origin top-left**. Vision
  reports normalised coordinates from the bottom-left; converting once here
  keeps the flip out of every caller, and out of `07-crop-downscale`.
- `Assets/Shell/CatVision.cs` — the C# side. Off iOS it answers "not available"
  instead of throwing, so the capture screen can be built in the editor.
- `Assets/Editor/SwiftPluginPostProcess.cs` — Unity copies `.swift` into the
  Xcode project but does not configure Xcode to compile it. Sets
  `SWIFT_VERSION` on both targets and keeps
  `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` at NO, which is the setting that
  otherwise fails App Store validation.
- `Assets/Shell/VisionSelfTest.cs` — runs the set inside a real build. Dormant
  unless a `visiontest` folder is pushed into the app container, so no
  third-party fixture ever ships inside the app.

## Against the VERIFY list

**1. Run against the reference set on a physical device — not met, and the
simulator cannot stand in.** There is no developer team yet
(`10-accounts/02`), so nothing runs on hardware.

The plugin *was* run inside a real iOS build, and this is what came back for
all 41 images:

```
{"file":"cat_01.jpg","ok":false,"error":"vision failed: Could not create inference context",...}
```

41 of 41, in the simulator. The same `VNRecognizeAnimalsRequest` on the same
machine, called from the macOS probe, recognises 30 of them. So the model that
backs animal recognition does not load under the simulator, and no amount of
work on this side changes that — the numbers in the table above are the only
ones obtainable without a device.

What the failed run does prove, and it is not nothing: **the bridge works.**
The call crossed C# → `DllImport("__Internal")` → `@_cdecl` → Swift, ran, built
a JSON answer, marshalled it back and freed it, 41 times, at 3.5–58 ms per
call, with no crash and no leak. Everything except Vision itself is exercised.

This item stays open until a build runs on hardware (`14-testflight`).

**2. Cats flagged as cat, dogs as dog, empty frames as no animal — holds**, with
the miss rate above: 18/20, 5/5, 5/5.

**3. Bounding boxes match the animal by eye on at least five images —** see
`box-check.jpg` beside this file: the reported rectangles drawn back onto the
photos.
