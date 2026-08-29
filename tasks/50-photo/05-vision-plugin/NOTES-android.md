# Is there a cat in this photograph, on Android — 2026-08-29

`NOTES.md` measures what Vision does with `fixtures/reference-photos` on iOS.
Off iOS, `Shell/CatVision.cs` answered `"vision is iOS-only"` and nothing else
existed. This is the Android half: what was built, what it was measured doing
over the same 41 images, and what it costs.

Everything below came out of a command that is checked in and can be re-run.
`tools/tests/android-vision/run.sh` is to this file what `tools/vision-probe`
is to `NOTES.md` — with one difference in our favour: it compiles **the shipped
plug-in's own source files**, not a reconstruction of them
(`app/build.gradle` points `sourceSets.main.java` straight at
`game/Assets/Plugins/Android/CatVision.androidlib/src/main/java`).

## The choice

Android has no `VNRecognizeAnimalsRequest`. There is no on-device animal
recogniser in the platform, and no ML Kit API answers both halves of the
question. So two APIs, doing one job each:

| | artefact | what it answers | where the model lives |
|---|---|---|---|
| species | `com.google.mlkit:image-labeling:17.0.9` | `Cat` / `Dog` + confidence | **in our APK** |
| silhouette | `com.google.android.gms:play-services-mlkit-subject-segmentation:16.0.0-beta1` | a foreground mask + a box per subject | **in Google Play services** |

Segment first, then label each subject's crop. A cat filling a fifth of a
photograph competes with the sofa and the rug for a whole-frame labeller's
attention; on a crop she does not.

### What the others cost

Checked by downloading the artefacts and unzipping them, not from prose:

- **ML Kit Object Detection** (`object-detection:17.0.2`) gives boxes, and its
  base classifier's five coarse categories are Home good, Fashion good, Food,
  Place, Plant. **No animal.** It would answer "something is there", never
  "a cat", and it carries its own 1 706 957-byte classifier for the privilege.
- **ML Kit Selfie Segmentation** (`segmentation-selfie:16.0.0-beta6`) is
  people-only — its whole model is
  `selfiesegmentation_mlkit-256x256-2021_01_19-v1215.f16.tflite`, 249 024
  bytes. On a cat it segments nothing.
- **A bundled TFLite ImageNet classifier** would name cat breeds, but brings a
  TFLite runtime we would then own, and still gives no box and no mask.
- **MediaPipe Interactive Segmenter** needs the user to tap the subject. The
  capture screen does not ask a player to trace her cat.
- **There is no bundled alternative to subject segmentation at all.**
  `com.google.mlkit:segmentation-subject` does not exist — checked against
  `dl.google.com/dl/android/maven2/com/google/mlkit/group-index.xml`; subject
  segmentation is published only as a `play-services-*` artefact. The mask is
  Play-services-shaped or it does not exist.

### "Then why the labeller at all — can't the mask answer it?"

**No, and not by a little.** `Subject`, the whole of what segmentation returns
per subject, is `getStartX/getStartY/getWidth/getHeight/getBitmap/
getConfidenceMask` (checked with `javap` on the AAR). There is no label, no
class, no score. It knows a prominent thing is there; it has no opinion about
what.

**Measured, not argued.** On the rung-1 run below the segmenter produced a mask
for **41 of 41** images — including all five dogs (coverage 0.09–0.46) and all
five empty frames (0.33–0.70, the highest coverage in the whole set being an
`empty_01` with no animal in it at all). A rule of "there is a subject and it
fills a reasonable share of the frame" would accept every dog and every empty
room in the reference set. The requirement that decides this feature — *no dog
is ever called a cat, no empty frame ever produces a detection* — is a species
question, and the mask has no species in it. The labeller is not a
convenience.

## What it does with the 41 reference photographs

`./run.sh`, Android 15 / API 35, arm64-v8a, Google Play services 24.23.35. 41
of 41 images, one JSON line each in `out.jsonl`.

**It was measured twice, on two emulators, and the difference between them is
the most useful thing in this file.** The first AVD is `google_apis`: Play
services present and reporting itself available, but no working Play Store, so
the subject-segmentation module can never arrive. Every image there came back
on rung 2, whole-frame labelling, no mask. The second AVD is
`google_apis_playstore` with `PlayStore.enabled=yes`, where the module
downloads and all 41 run on rung 1. Two rungs, same 41 photographs, same
build — which is exactly the pair of columns a player's phone will fall into.

The plug-in returns raw confidence with a 0.05 floor and no verdict — the
threshold belongs in C#, as on iOS. So each rung is reported as returned and
again after `PhotoJudge`'s existing 0.6.

### Rung 1 — segmentation available (what most phones will do)

41 of 41 `rung: "subject+label"`, **41 of 41 masks**. 182–557 ms per
photograph, mean 214.

| category | n | at 0.6 | what it said | mean | range | masks |
|---|---|---|---|---|---|---|
| cat | 20 | **20** | Cat ×20 | 0.98 | 0.91–0.99 | 20 |
| dog | 5 | 5 | Dog ×5 | 0.96 | 0.89–0.99 | 5 |
| multi | 3 | 3 | Cat ×3 | 0.97 | 0.92–0.99 | 3 |
| blurry | 5 | **5** | Cat ×5 | 0.99 | 0.98–0.99 | 5 |
| empty | 5 | **0** | — (Dog ×3 and Cat ×1 below 0.28) | — | — | 5 |
| ofphoto | 3 | **2** | **Cat ×2** at 0.97, 0.87 | 0.92 | 0.87–0.97 | 3 |

### Rung 2 — no segmentation module (whole-frame labelling only)

No mask on any image; 44–204 ms, mean 67.

| category | n | at 0.6 | what it said | mean | range |
|---|---|---|---|---|---|
| cat | 20 | **20** | Cat ×20 | 0.99 | 0.97–0.99 |
| dog | 5 | 5 | Dog ×5 | 0.98 | 0.93–0.99 |
| multi | 3 | 3 | Cat ×3 | 0.95 | 0.92–0.99 |
| blurry | 5 | **5** | Cat ×5 | 0.99 | 0.96–0.99 |
| empty | 5 | **0** | — (Dog ×4 below 0.31) | — | — |
| ofphoto | 3 | **0** | — (0.46, 0.43, 0.17) | — | — |

Against the iOS bar in `NOTES.md` — 18/20 cats, 4/5 blurry, 5/5 dogs, 0/5
empty — **both rungs are equal or better on every category except one.** The
two cats Vision missed, `cat_10` at 259×270 px and `cat_20` with two kittens,
are found on both rungs at 0.99; so is `blurry_04`, the second-blurriest image
in the set. No dog was ever called a cat, on either rung, at any threshold.

### The photograph-of-a-screen hole is here too, and the mask is what opens it

`NOTES.md` names this as the finding that matters: two of three
photographs-of-a-screen came back **Cat** on iOS, at 0.64 and 0.62, inside the
range of real cats, so no threshold could separate them.

**Android reproduces it exactly — two of three, at 0.97 and 0.87.** Real cats
on this rung run 0.91–0.99; both fakes are inside that range, and `cat_16` at
0.91 sits *below* both of them. There is no threshold that rejects the screen
shots and keeps the cats. The Android answer to this question is the iOS
answer.

What is new is *why*, and it is worth knowing before anyone tries to fix it. On
rung 2 those same three images score 0.46, 0.43 and 0.17 — all rejected, with a
clean empty gap between 0.46 and 0.92. **Cropping to the subject is what breaks
it.** Whole-frame, the labeller sees a room with a monitor in it and is
unimpressed; handed the segmenter's cut-out, it sees a cat filling the frame
and is certain. `overlay-ofphoto_02.jpg.png` from
`maskOverlaysForEyeChecking` shows the cut-out in question: the cat on the
screen, neatly extracted, with a sliver of monitor bezel attached.

So the better species answer and the fake-detecting one are not the same
answer, and this pipeline cannot have both from one call. If rejecting screen
shots is ever judged to matter, the cheap experiment is already implied by
these two tables: **label the whole frame as well as the crop, and disbelieve a
crop the whole frame does not support.** That is one extra labeller call, and
it is not implemented — the tables above are the reason to consider it, not
evidence that it works.

**Do not read any of this as "the game can tell whose cat it is".** Neither
platform has a notion of liveness. The premise is "it is *her* cat" and nothing
here verifies that, at any threshold, on either operating system.

## Offline, and the first run

Airplane mode on the rung-2 emulator, same 41 images, immediately after the
online run:

- **41 of 41 identical.** Every detection matched the online run byte for byte.
- Mean 93 ms per photograph (46–650 ms); online, 67 ms (44–204 ms).
- Every image reported `segmentation unavailable: MlKitException/14`.

That is the whole point of the bundled labeller: **the rung that decides
whether a player is told "that is not a cat" needs no network, no Google
account and no Play services.** It is in the APK.

Error code 14 is `MlKitException.UNAVAILABLE`. The message behind it, which the
diagnostic captured in full, is Google's own:

```
com.google.mlkit.common.MlKitException: Waiting for the subject segmentation
optional module to be downloaded. Please wait.
    at com.google.mlkit.vision.segmentation.subject.internal.zzj.load(...)
```

So, precisely:

| situation | species | mask | what the player gets |
|---|---|---|---|
| Play services + module present | yes | yes | everything |
| Play services, module still downloading | yes | no | her cat, no marks measured |
| **no Play services at all** | **yes** | no | her cat, no marks measured |
| first run, offline, module never fetched | yes | no | her cat, no marks measured |
| not a decodable image | no (`ok:false`) | no | `VisionAnswer.Failed` — "could not look", not "not a cat" |

Google documents the middle rows in one sentence — *"Requests you make before
the download has completed produce no results"*
(developers.google.com/ml-kit/vision/subject-segmentation/android) — and the
measurement above is what "no results" turns out to be in practice: error 14,
returned immediately, not a hang and not a crash.

`CatVision.prepare()` asks Play services for the module up front, and the
plug-in's manifest carries the install-time hint
(`com.google.mlkit.vision.DEPENDENCIES` = `subject_segment`, the value that
same page publishes) so the fetch usually happens while the app installs. It
does survive into the shipped app — `aapt2 dump xmltree` on
`game/build/android/CatShelter.apk` finds it at line 171 of the merged
manifest. Neither is load-bearing.

## What it adds to the APK — measured

`game/build/android/CatShelter.apk`, ARM64 only, before and after this plug-in:

```
53 720 643 bytes   before  (51.2 MiB)
61 398 726 bytes   after   (58.6 MiB)
+7 678 083 bytes           (+7.32 MiB, +14.3%)
```

Compressed, inside that APK, the ML Kit payload is:

| entry | in the APK | uncompressed |
|---|---|---|
| `lib/arm64-v8a/libmlkitcommonpipeline.so` | 4 453 514 | 10 989 136 |
| `assets/mlkit_label_default_model/mobile_ica_8bit_with_metadata_tflite` | 1 989 552 | 3 042 638 |
| `play-services-mlkit-subject-segmentation.properties` | 75 | 130 |

The remaining ≈1.23 MB is dex and archive overhead. **Subject segmentation
contributes 75 bytes** — its AAR has no `assets/` entry and no native library
at all; the model is in Play services.

### The trade, and the recommendation

Both halves of the pipeline could be unbundled. Three release APKs of the same
source, built by `./run.sh size`, differing only in the labeller:

| arm | APK |
|---|---|
| control — neither artefact, no plug-in | 1 956 |
| **unbundled** labeller + unbundled segmenter | 7 616 817 |
| **bundled** labeller + unbundled segmenter — what ships | 21 922 109 |

The unbundled arm's APK contains **no `.so` and no `.tflite`**. Applied to
Unity's APK, swapping `com.google.mlkit:image-labeling:17.0.9` for
`com.google.android.gms:play-services-mlkit-image-labeling:16.0.8` removes both
entries above: **6 443 066 bytes, 6.14 MiB**, taking the build to ≈54.96 MB —
about +1.2 MB over the 52 MB baseline instead of +7.3 MB.

**The recommendation is to keep the bundled labeller, and the reason is not
"simpler".** It is that unbundled models are not reliably there, and this was
measured rather than feared:

- On a `google_apis` emulator, Play services reports itself **available**
  (`isGooglePlayServicesAvailable` → 0, SUCCESS) and the download **never
  completes**, on any timescale — `ZappPhoneskyConn: Unable to bind to
  Phonesky`. "Play services is present" and "the model will arrive" are
  different claims.
- On a proper `google_apis_playstore` emulator it does arrive, in about a
  minute. **But not instantly, and not permanently.** Polling every 40 seconds,
  one attempt in six fell back to "waiting for the module to be downloaded"
  after four consecutive successes.
- Google's own wording for that window is *"Requests you make before the
  download has completed produce no results."*

That window is not an edge case: it is **every fresh install**. A player who
installs the game and opens it straight away is inside it. With the bundled
labeller she is told "yes, that is a cat" and plays; her cat simply has no
marks measured. With the unbundled one she is told her photograph is not a
cat — and "uploaded a photo" is the single metric this project is judged on.

So the 6.14 MB does not buy the mask, and it does not buy a rare device. It
buys the first thirty seconds of every player's first session. The mask can be
absent and the game still works; the species answer cannot.

**If 6 MB is judged more valuable than that, the swap is one line** in
`CatVision.androidlib/build.gradle` and not one line in `CatVision.java` —
`ImageLabeling.getClient(ImageLabelerOptions)` is the same call for both
artefacts, which is why the three arms above are comparable at all. The cost,
stated plainly: on a device with no Play services, or on a first run before the
download lands, the game would reject every photograph a player offered it.

## Where it sits, and the contract

Nothing above `Shell/CatVision.cs` changed. `CatVision.Recognise` has the same
signature and returns the same `Core.VisionAnswer`; the Android JSON uses the
same field names as the Swift, and `JsonUtility` ignores the extra mask fields.
`Available` now includes `RuntimePlatform.Android`.

The mask needed somewhere to go, so `CatVision.Silhouette` is new, returning
`CatSilhouette` — the same answer plus `mask`, `maskWidth`, `maskHeight`,
`maskCoverage`, `maskSource` and `rung`. It is declared in `Shell` rather than
`Core` deliberately: `VisionAnswer` is shared with iOS and should not grow
fields one platform never fills. If a silhouette ever reaches gameplay logic,
that type moves to `Core` beside it.

**One `byte[]` carries both** — magic `CVS1`, a big-endian int32 JSON length,
the JSON, then the mask, one byte per pixel over the whole image. Two calls
would mean the plug-in holding a player's mask in a static field between them,
and this pipeline keeps no such state.

Three rungs, each surviving the loss of the one above: `subject+label`,
`label`, `none`. `rung` crosses to C# on every answer, and is worth putting in
analytics — it is the only way we will ever learn how many players' devices
actually have the segmentation module.

**Nothing is logged and nothing is written.** There is not one `Log` call in
`com.catshelter.vision`. The photograph exists as bytes in memory for the
length of one call; no error string names a pixel, a path or a size; and where
ML Kit's own error message is English prose that could reach a Russian player,
only its numeric code crosses (`MlKitException/14`). No permission is declared.

## Against a device — what is still unknown

**1. The mask exists, and it is good.** Forty-one of forty-one, and looked at
by eye rather than trusted from a coverage number:
`maskOverlaysForEyeChecking` paints everything outside her flat magenta and
leaves the PNG beside `out.jsonl`.

- `cat_09` — a tight silhouette down to individual white paws and the fringe of
  fur along her back. This is the quality the marks measurement needs.
- `multi_03` — two cats on a sofa, **one** cut out. That is deliberate
  (`Mask.from` carries the best-labelled *cat* subject, not the union of
  everything the segmenter found) and it is what a mark measurement requires:
  one animal, or the median lightness is two cats averaged.
- `ofphoto_02` — the cat on the monitor, cleanly extracted, with a sliver of
  bezel attached. The picture that explains the screen-shot result above.

**Getting there took a second emulator, and that is itself the finding.** The
first AVD is `google_apis`: `PlayStore.enabled = no`, `com.android.vending`
installed but with no launcher activity, so Phonesky cannot be bound —

```
ZappPhoneskyConn: Unable to bind to Phonesky
ZappDownloader: No successful Zapp module downloads for requested modules
    [MlkitSubjectSegmentation.optional:242335100400:permitMetered]
```

— and the module can never arrive, on any timescale. On a
`google_apis_playstore` AVD with `PlayStore.enabled=yes`, the same code, the
same call, downloads `MlkitSubjectSegmentation.optional_242335100400_2.apk`
within about a minute of the first request and every one of the 41 images moves
to rung 1.

**One transient is worth recording.** Polling the same call every 40 seconds,
one attempt out of six went back to "waiting for the module to be downloaded"
after four consecutive successes. The module is not a thing that becomes true
once. That is the whole reason `segment` does not latch its failure: an earlier
draft of this plug-in cached "unavailable" on the first miss, which would have
turned that one transient into a mask-less rest-of-process.

**The geometry underneath it is not left to hope either.** `Subject` has a public
constructor, so `MaskGeometryTest` builds one by hand and runs `Mask.from`
without any model — the arithmetic that takes a subject-sized confidence buffer
and places it into whole-image coordinates is the kind that is wrong the first
time, and it is now pinned: a 30×40 subject at (10, 20) of a 100×100 image sets
exactly 1 200 mask bytes and none outside its edges; a 512×384 subject in a
2048×1536 photograph still lands in the right quarter after the downscale to
512 and covers a sixteenth of the frame; a subject with no confidence buffer
returns no mask instead of throwing. The same test pins the `Subject`
constructor's undocumented integer order — `(width, height, startX, startY)`,
not the other way round, which is the sort of thing that would otherwise put a
cat's silhouette in the wrong corner and be blamed on the model.

```
$ adb shell am instrument -w -e class com.catshelter.vision.MaskGeometryTest ...
com.catshelter.vision.MaskGeometryTest:.....
OK (5 tests)
```

The contrast with iOS is worth stating plainly, because it changes where this
project's risk sits. `NOTES.md` records the iOS plug-in as **0 of 41,
everywhere it has ever been run** — the simulator cannot load the animal model,
and its numbers come from a macOS probe that is not the plug-in. The Android
plug-in's own record is **41 of 41, twice, on both rungs, including every
mask**, from the shipped source files compiled by a second build. The half of
this feature that has been observed to work is the Android half.

**2. What is proven.** The bridge, the decode, the orientation handling, the
species answer, the packing, the degrading, and the threading:

```
$ ./run.sh
com.catshelter.vision.MaskGeometryTest:.....
com.catshelter.visionprobe.ProbeTest:.......
OK (12 tests)
41 out.jsonl
```

- `probeReferenceSet` — 41 images, 41 answers.
- `garbageBytesDegrade`, `emptyBytesDegrade` — five junk bytes and zero bytes
  return `ok:false` cleanly, no throw, no mask.
- `orientationSwapsReportedSize` — EXIF 6 swaps the reported width and height,
  so a phone held sideways reports upright pixels.
- `shouldNotDeadlockOnMainThread` — the call is made from the main looper and
  returns. ML Kit's `Tasks.await` throws on the main thread, Unity calls from
  it, and the executor hop in `analyse` is the only thing standing between
  those two facts and a frozen capture screen. Without this test that would be
  discovered by a player.

**3. Not tested, and an emulator cannot test it:**

- **A real camera-roll photograph.** Every fixture is a downloaded dataset
  image, 220–1280 px on the long side. A phone shot is 4032×3024, which is the
  only thing that exercises `Decode`'s `inSampleSize` path — nothing here has
  ever subsampled anything, and the box-rescaling by `Decode.Result.scale` that
  goes with it is therefore untried on real numbers.
- **Speed on a phone.** 214 ms per photograph, mean, is a software-rendered
  emulator on a laptop. It could be much better on a phone's NPU or much worse
  on a cheap one; there is no way to know from here. It is one call per
  photograph taken by hand, so the bar is low, but 557 ms was the slowest here
  and that is already noticeable.
- **A device with no Play services at all.** The third row of the table above
  is reasoned from the bundled model being an asset in our own APK, not
  measured — no such emulator image was available.
- **The first-run download itself.** How many megabytes the segmentation model
  is, how long it takes on a phone network, and whether it costs a player
  mobile data. Google publishes none of the three, and an emulator on a
  laptop's ethernet cannot stand in for any of them. What *is* known is the
  request flag Play services used: `permitMetered`.
- **Thermal and battery behaviour**, and whether the ML Kit detectors held in
  static fields survive an Android process death and restore the way the rest
  of the app does.

**4. The C#-to-Java crossing has never been executed.** `Shell/CatVision.cs`'s
Android branch is written against `AndroidJavaClass`, and the harness calls the
Java directly. Unity's marshalling of a `byte[]` argument and a `byte[]` return
across `CallStatic` is the one link in the chain that only a Unity build on a
device will prove — the same gap `NOTES.md` leaves open for iOS.
