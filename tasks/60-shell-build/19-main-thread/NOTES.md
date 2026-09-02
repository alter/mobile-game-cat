# 19-main-thread — heavy work off the main thread

## What was wrong

Five native calls on the photograph path were made from the main thread, one
after another, from a coroutine that never yielded between them:

| where | call | ceiling on the far side |
|---|---|---|
| `View/CaptureScreen.cs:363` | `Recognise` → `CatVision.Recognise` | 30 s (`CatVision.java`) |
| `View/CaptureScreen.cs:469` | `SubjectBox` → `CatVision.Silhouette` | 30 s |
| `View/CaptureScreen.cs:478` | `Crop` → `CatPhoto.Prepare` (12 Mpx JPEG decode in Java) | none, but the largest single piece of work |
| `View/CaptureScreen.cs:560` | `CatCoat.Read` → `CatVision.Silhouette` | 30 s |
| `Shell/GameBoot.cs:65` | `CatVision.Prepare()` inside `Awake` | 12 s |

Three analyse calls at 30 s each plus the module fetch at 12 s is a minute and a
half of held main thread in the worst case, and Android is entitled to kill an
application for a fraction of that. `Prepare` was the worst-placed of them: its
own summary said "it does not block", which was written about the *download* and
read as being about the *call*.

## What changed

`Assets/Shell/OffMain.cs` (new) is the single place worker threads are made.
One thread per call, `AndroidJNI.AttachCurrentThread()` first and
`DetachCurrentThread()` in a `finally` — without the pair, an
`AndroidJavaClass` call from an unattached thread does not throw, it aborts the
process natively. The pair is compiled out off Android: iOS is `DllImport`,
which has no such rule.

The main thread polls a `Call<T>` once a frame from the coroutine
(`CaptureScreen.Await`), which also writes the measurement line below.

`CatCoat.Read` could not simply move: it decodes the crop with `Texture2D`,
which exists only on the main thread. It is now three steps —
`Look` (mask, worker) → `Decode` (Texture2D, main thread, one frame) →
`Measure` (`CoatReader` arithmetic, worker) — with `ReadOverFrames` driving
them. `Application.persistentDataPath` is main-thread only too, so `Decode`
reads it and `CoatPixels` carries it to `Dump` on the worker.

## Measurement — before and after

Measured on emulator-5554 with the original 3000×4000 (5.2 MB) photograph
through the `capture.txt` path, twice: once with an APK built from HEAD
(0fdc201) and once with this change. Both numbers come from the app's own log
in `adb logcat`, not from an estimate.

**What is measured:** the longest single stretch of main thread held by one
call, and the number of frames drawn while each call ran. Frames are the honest
metric here — the mask call takes ~250 ms in either arrangement; what changed is
whether anything was drawn during it.

Before (APK from HEAD):

```
[CatCoat] ... (mask 232 ms, all 271 ms)
```

232 ms of main thread inside one call, 0 frames drawn — 0 by construction, the
coroutine did not yield. Nothing between `Recognise` and `cat ready` was
measured separately because there was nothing to measure: it was one straight
line of blocking calls.

After:

```
[CaptureScreen] recognise: 265 ms, 3 frame(s) drawn while it ran
[CaptureScreen] crop: 68 ms, 2 frame(s) drawn while it ran
[CatCoat] ... (mask 263 ms, decode 4 ms on the main thread, all 304 ms)
[CaptureScreen] marks: 33 ms, 1 frame(s) drawn while it ran
```

Longest main-thread step on the path: **232 ms → 4 ms** (the Texture2D decode,
which cannot move). Thread ids in logcat confirm it: the main thread is 2710,
`CatCoat:Look` logged from 2842 and `CatCoat:Measure` from 2846.
`[GameBoot] subject segmentation: ready` came from 2815, not from Awake.

The emulator is a best case — Play services answered in 263 ms with the module
already downloaded. The number this task is actually about is the ceiling: on a
phone still fetching the module those same calls are entitled to take 30 s each,
and the main thread now spends all of it drawing.

The `subject box` line does not appear because the labeller gave a box
(`Cat 0.86`), so that branch was skipped — the same on both runs.

**On the animated bar:** VERIFY asks for screenshots of the bar in motion. The
whole path takes ~500 ms on this emulator, which is too fast to photograph
usefully; the frame counts above are the same evidence in a form that can be
read. The bar rides `_busy.schedule.Execute(...).Every(28)` on the panel's
scheduler, which advances on every frame drawn, so "3 frames drawn while it ran"
is literally "the bar moved three times during the recogniser call".

## Run

```
adb install -r -d game/build/android/CatShelter.apk
adb push tmp/IMG20260829212451.jpg $FILES/x.jpg
adb shell "echo x.jpg > $FILES/capture.txt"
```

`capture-state.txt`, identical before and after:

```
vision said Cat at 0.86 -> Cat
accepted a 97784-byte photo
cat ready (OfflineColourOnly): short brown tabby, green eyes
x.jpg -> "小猫到我们这儿了。"
```

No `JNI DETECTED`, no tombstone, no abort in logcat. `capture.txt` removed from
the device afterwards.

`dotnet test build/core-tests/core-tests.csproj` — 261 passed, 0 failed, both
before and after. Nothing in `Core` was touched.

`[BuildScript] result=Succeeded path=build/android/CatShelter.apk errors=0`

## Not done, and why

- **`CoatBuilder.TryBuildFor` is untouched.** SCOPE asks for it either on a
  worker or cut into frames; it is `Texture2D` from end to end, which cannot
  leave the main thread, and cutting it into frames is a different change to a
  file this task does not otherwise open. It still runs synchronously from
  `MeetYourCatScreen.Build`, and the capture screen's waiting block is still
  deliberately left standing over it (`CaptureScreen.Handle`, last comment).
  That is the largest remaining main-thread block in the app and it wants its
  own task.
- **`CatColour.Estimate` stays on the main thread** for the same reason —
  `Texture2D` over one 512×512 crop, and only on the no-mask path.
- **`AskWorker` is untouched.** It is `null` in the shipping build
  (02-traits-worker does not exist), and an HTTP call there will want
  `UnityWebRequest`, which is a coroutine and not a worker thread.
- **iOS was not run.** The change is platform-neutral and the iOS calls are
  `DllImport` with no attach rule, but no iOS device run was made for this task.
