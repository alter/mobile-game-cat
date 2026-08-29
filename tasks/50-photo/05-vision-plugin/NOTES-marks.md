# Measuring her marks on the device — 2026-08-29

`Core/CatSpot.cs` argues that the five coat traits are class characteristics —
288 drawable cats, and none of them is anybody's — while a white sock on ONE
paw is what a person recognises her own animal by. Until today those marks
could only come from a language model looking at the photograph and saying so.

This is the other source: measure them. `Plugins/iOS/CatMarks.swift` and
`Shell/CatMarks.cs`, free, offline, and with the photograph never leaving the
phone.

**Nothing here has ever seen a cat.** Neither iOS 17 request runs in the
simulator, and there is no device. Read the whole file with that in front of
you; the last section lists what that leaves unknown.

## The three calls, and what each one gives back

| call | since | gives back |
|---|---|---|
| `VNRecognizeAnimalsRequest` | iOS 13 | `Cat`/`Dog` + confidence + a normalised box. The same call `CatVision.swift` makes, repeated here so one pass serves the whole measurement. |
| `VNGenerateForegroundInstanceMaskRequest` | **iOS 17** | `VNInstanceMaskObservation`: `instanceMask`, a low-resolution buffer whose pixel *values are instance numbers*, and `generateScaledMaskForImage(forInstances:from:)`, a full-resolution soft alpha mask for the instances you name. |
| `VNDetectAnimalBodyPoseRequest` | **iOS 17** | `VNAnimalBodyPoseObservation`: 25 normalised joints, each with its own confidence. Cats and dogs only. |

The last two are what the move to `iOSTargetOSVersionString: 17.0` bought.

Each request is performed on **its own** `VNImageRequestHandler`. Three in one
`perform` would be cheaper, but Vision throws for the whole call when any one
of them fails — and the point of this file is that each rung survives the loss
of the one above.

## What is measured, and in what units

CIE **L\***, 0–100, from sRGB through linear light and the Rec. 709 luminance
weights. Not the green channel and not the mean of three: L\* is perceptually
uniform, so "10 points lighter" is about the same visible step on a black cat
as on a cream one, and the threshold C# tunes is one number rather than one per
coat colour.

For each place: the **median L\* of the cat pixels inside a small disc**, minus
**her own median L\* over the whole mask**. Signed. Positive is lighter than her
coat, negative darker. Both medians come off a 256-bin histogram — 0.39 L\*
per bin, finer than anything this is looking for, and no sort over a quarter of
a million pixels.

Only pixels inside the mask count, and that is the load-bearing part: a paw
against a pale floor would otherwise read as a white sock because half the disc
is floorboard.

The disc scales with the cat, not the image — 4.5% of the shorter side of the
mask's bounding box, clamped to 2–20 px — and a place with fewer than 8 mask
pixels under it is dropped rather than reported.

**No verdict crosses the boundary.** `CatMarks.cs` holds `Threshold = 18.0`
L\* points and `MinLandmarkConfidence = 0.5`. Both are guesses. They are in C#
so they can be tuned against `fixtures/reference-photos` without another native
build — a `light`/`dark` decision made in Swift could only ever be re-tuned by
rebuilding the plugin, and a test enforces that the Swift never writes either
word.

## Where a place comes from

**Only four of the ten places are landmarks.** This is the single most
important thing on this page.

| place | from | |
|---|---|---|
| `muzzle` | `nose` | measured |
| `eye_left` | `leftEye` | measured |
| `eye_right` | `rightEye` | measured |
| `paw_left` | `leftFrontPaw` | measured |
| `paw_right` | `rightFrontPaw` | measured |
| `tail_tip` | the tail joint farthest from `neck` | measured, see below |
| `forehead` | eye midpoint → half way to the ear-bottom midpoint | **derived** |
| `chin` | eye midpoint through `nose` and 0.55 again past it | **derived** |
| `chest` | `neck` → a third of the way to the front-elbow midpoint | **derived** |
| `flank` | midpoint(`neck`, `tailBottom`) → a third towards the front elbows | **derived** |

The 25 joints are: nine head (ear top/middle/bottom ×2, both eyes, nose), **one
trunk — the neck, and nothing else** — six foreleg, six hindleg, three tail.
There is no chin joint, no forehead joint, no chest joint and no flank joint,
and those are exactly four of the game's ten places. They are constructed from
the joints that do exist, every one is flagged `derived` in the JSON, and the
fractions above (0.5, 0.55, 0.33, 0.33) **have never been checked against a
photograph.** `chin` is the weakest: a cat looking up hides her chin entirely
and this still returns a number, from whatever is behind it inside the mask.

A place is dropped outright when any joint it needs is missing or below the
confidence floor. A wrong mark is worse than a missing one, and a chest placed
off a guessed neck lands on the carpet.

### The tail, and why it is measured rather than named

Apple documents `tailTop` as "the top of the tail" and `tailBottom` as "the
bottom", which settles nothing. Apple's own WWDC23 sample
`DetectingAnimalBodyPosesWithVision` draws the skeleton `neck → tailBottom →`
hind elbows and `tailBottom → tailMiddle → tailTop`, so **`tailBottom` is the
rump and `tailTop` is the tip** — the reverse of the plain reading of the words.

The code does not rely on either reading. Whichever of the three tail joints
is farthest from the neck is the tip. That agrees with the sample, costs
nothing, and stays right if a revision ever renumbers the tail. When the answer
is not `tailTop`, the JSON says so in `notes`.

## The ladder

| rung | when | what comes back |
|---|---|---|
| `pose_and_mask` | both | all ten places attempted — nine from the recipe table, plus `tail_tip`, which is placed by geometry instead |
| `mask_only` | mask, no pose | three coarse bands down the silhouette: `chest`, `flank`, `paws` |
| `pose_only` | pose, no mask | the landmarks, and **nothing measured** |
| `none` | neither | nothing, and `notes` says why |

`mask_only` is much weaker than it looks and says so in its own `notes`: it
assumes a cat upright in frame and is wrong for one lying on her side, and
`paws` is both front paws as one number — the asymmetry the whole feature
exists to catch, thrown away. `paws` is not a `spot_place`; it carries
`grouped: true`, and `MarksAnswer.ToSpots` drops it by that flag rather than by
its name.

`pose_only` measures nothing on purpose. Without a mask there is no telling her
coat from the sofa, so a body median would be a median of the room and every
delta against it would be a number about the furniture.

## Where the API did not give the design what it wanted

Listed rather than approximated quietly, which was the instruction.

1. **No chin, forehead, chest or flank joint exists.** Four of ten places are
   arithmetic between other joints, with fractions nobody has checked. Named
   above; repeated here because it is the largest gap.
2. **Neither iOS 17 request runs in the simulator.** Apple's own sample-code
   page for animal body pose says outright: *"This sample app doesn't run in
   Simulator, so you need to run it on a physical device with iOS 17 or later"*.
   `VNGenerateForegroundInstanceMaskRequest` fails in the simulator with the
   same `Could not create inference context` this task's `NOTES.md` already
   records for `VNRecognizeAnimalsRequest`. **So no photograph has been through
   this code, and every number in it is arithmetic.**
3. **"Left" is not defined.** Nothing in the SDK headers says whether
   `leftFrontPaw` is the cat's own left or the left of the image. The game's
   `paw_left`/`eye_left` presumably mean the viewer's. If Vision's convention
   is anatomical, every left/right mark is mirrored — and the feature still
   half-works, because a mark on one paw and not the other is the point, but
   the mark would be drawn on the wrong side. **One photo of a cat with a
   single white sock settles it.** Until then it is unknown, not assumed.
4. **A pose cannot be tied to a detection.** `VNAnimalBodyPoseObservation`
   descends from `VNRecognizedPointsObservation`, which descends straight from
   `VNObservation` and **not** from `VNDetectedObjectObservation` — so it has
   no bounding box, and it carries no species either. With a cat and a dog in
   one frame, nothing in the API says which pose is which animal. The plugin
   makes the link itself, out of the only thing a pose has: it picks the body
   whose joints mostly fall inside the recognise-animals box. With no box, it
   takes the most confident pose and records in `notes` that it guessed.
5. **Segmentation has no notion of "cat".** The request separates *every*
   salient object and numbers them from 1 — the cat, the arm holding her, the
   cushion. There is no way to ask it for the animal. `regionOfInterest` is
   inherited from `VNImageBasedRequest` and does apply, but it crops what is
   analysed rather than naming a subject, and a box tight to the cat would cut
   off whatever of her falls outside it. The plugin instead reads `instanceMask`
   — whose pixel values are the instance numbers — and keeps the instance with
   the largest share of itself inside the animal box.
6. **`instanceMask`'s pixel format is undocumented.** The header states
   `kCVPixelFormatType_OneComponent32Float` for `generateMaskForInstances:` and
   says nothing about the `instanceMask` property. The reader handles 32-float
   and 8-bit and **refuses anything else** rather than reading the bytes as if
   it knew. If a device turns out to hand back a third format, the symptom is
   the note "could not read the instance mask", not a wrong answer.
7. **Nothing corrects for lighting.** A sunlit flank is a large positive delta
   and is not a marking. Measuring against her own median instead of an
   absolute helps with coat colour and not at all with an illumination
   gradient across one animal. No mitigation is in the code, and this is the
   most likely way the measurement lies on a real photograph.
8. **Liveness is still unavailable, as `NOTES.md` records** — two of three
   photographs-of-a-screen pass as cats. This measures marks on whatever cat is
   in the frame. It cannot tell whose.

## Privacy

The premise of the feature is that it is *her* cat, so: the photo is not
written, not logged, not sent. There is no `NSLog` in the Swift on purpose —
error strings name a Vision failure, never a pixel, a path or a size — and the
bytes exist in memory for the length of one call.
`test_marks_plugin.py::test_the_photograph_is_never_written_down_or_logged`
fails the day somebody adds a debug dump while chasing a measurement, which is
exactly when one would be added and exactly when nobody would notice.

## What was compiled and run

```
$ xcrun swiftc -parse -sdk <iPhoneSimulator26.2.sdk> \
      -target arm64-apple-ios17.0-simulator game/Assets/Plugins/iOS/CatMarks.swift
exit=0

$ xcrun swiftc -typecheck -sdk <iPhoneSimulator26.2.sdk> \
      -target arm64-apple-ios17.0-simulator game/Assets/Plugins/iOS/CatMarks.swift
exit=0                                    (0 warnings, 0 errors)

$ xcrun swiftc -c -O -sdk <iPhoneOS26.2.sdk> \
      -target arm64-apple-ios17.0 game/Assets/Plugins/iOS/CatMarks.swift -o CatMarks.o
exit=0
$ nm -gU CatMarks.o | grep CatMarks
0000000000011430 T _$s4main13CatMarks_freeyySpys4Int8VGSgF
000000000000b60c T _$s4main16CatMarks_measureySpys4Int8VGSgSPys5UInt8VGSg_s5Int32VALSdtF
000000000001141c T _CatMarks_free
000000000000b5f8 T _CatMarks_measure
```

`-parse` is what the brief asked for and it only checks syntax; `-typecheck`
is what actually proves the Vision calls exist with those signatures, and
`-c` against the **device** SDK proves the two `@_cdecl` symbols
`DllImport("__Internal")` will look for are exported. All three are recorded
because the first alone would have proved very little.

C#, against a UnityEngine stub (`RuntimePlatform`, `Application.platform`,
`JsonUtility.FromJson`) at `LangVersion 9`, the same recipe
`60-shell-build/16-localisation-ready` used, compiled **both** ways — the
editor branch and the `UNITY_IOS` branch that holds the `DllImport`s:

```
$ dotnet build marks.csproj                                -> 0 warnings, 0 errors
$ dotnet build marks.csproj -p:DefineConstants="UNITY_IOS"  -> 0 warnings, 0 errors
```

`tools/tests/test_marks_plugin.py` — 10 checks over the three sides that agree
only by somebody having typed the same string three times: the place names
against `tools/traits/schema.json` and `Core/CatTraits.cs`, the JSON field
names on both halves of the bridge (JsonUtility matches by name and says
nothing when it cannot — a renamed field silently reads as zero), the rung
names, the privacy grep, and that the threshold is in C#.

**Proved by mutation, on copies outside the repo, not asserted.** Nine
mutations, each reverted before the next:

```
baseline (copy, unmutated)                             10 passed
1. Swift emits "chest_left"                       FAILED every_place_is_a_real_spot_place
2. tail_tip is never recorded                     FAILED plugin_reaches_every_place (+1)
3. Swift lightness -> brightness                  FAILED both_sides_name_the_same_fields[Mark]
4. C# drops bodyPixels                            FAILED both_sides_name_the_same_fields[Answer]
5. NSLog added to the Swift                       FAILED photograph_is_never_written_down
6. Swift decides the shade itself                 FAILED threshold_lives_in_csharp (+1)
7. a rung renamed in Swift only                   FAILED rung_names_match_on_both_sides
8. CodingKeys added to the Swift                  FAILED swift_side_declares_no_CodingKeys
9. CatTraits and the schema drift apart           FAILED schema_and_CatTraits_still_agree
```

Mutation 8 is the one worth keeping: the field-parity check is only valid
while `JSONEncoder` uses the property names, so the thing that would quietly
invalidate it has its own test.

```
$ .venv/bin/python -m pytest tools/tests/test_marks_plugin.py -q  -> 10 passed
$ .venv/bin/python -m pytest tools/ -q                            -> 245 passed
```

**Not run: Unity, Xcode, the simulator, a device.** Out of bounds for this
pass, and item 2 above means the simulator would prove nothing anyway. Nothing
calls `CatMarks.Measure` yet — wiring it into the capture path is a separate
change, and it should not be wired in before item 3 is settled on hardware.

Two files have no `.meta`: `CatMarks.swift` and `CatMarks.cs`. Unity writes
those itself on the next editor open, and inventing a GUID by hand is a worse
risk than leaving them.

## The first thing to do with a phone in hand

`NOTES.md` above already argues that a free personal Apple ID installs on your
own device for seven days, which is enough. The order that gets the most out of
one evening:

1. Does `VNGenerateForegroundInstanceMaskRequest` return a mask at all — does
   `rung` come back `pose_and_mask`, or does the ladder fall to `none`?
2. **Whose left is `leftFrontPaw`.** One cat, one white sock, one photo.
3. What `delta` actually reads on a cat with a known marking, and on the same
   cat in flat light and in sun. That is the number `Threshold = 18` is
   waiting for, and until it exists the constant is decoration.
