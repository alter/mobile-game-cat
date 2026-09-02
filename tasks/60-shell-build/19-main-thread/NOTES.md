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

---

# The coat build, 2026-09-02

The one part of SCOPE the entry above left standing: `CoatBuilder.TryBuildFor`.
Recognition had already come off the main thread (efd3354); this is the coat.

## Cut into frames, not moved to a thread

`Texture2D` exists only on the main thread — `ReadPixels` at one end and the
upload at the other — so a worker was never the answer. The arithmetic between
them could have moved, but both ends would then have to marshal back and the
seams would have landed in the same places. Frames buy the same thing for a
fraction of the risk.

`CoatBuilder.Steps` (new) is the pass order with a `yield return null` between
stages. **`Build` is now that enumerator drained in one frame**, so there is one
sequence and only the crank differs; the first attempt kept two copies of a
nine-stage pipeline, which is a cat that renders differently depending on which
screen asked for it.

Two passes needed the seam *inside* them, not around them:

* **`Outline`** — a square window per pixel, the dearest stage. Split into
  `OutlineRows`, done 32 rows at a time. Each band reads the untouched source
  and writes only its own rows, so the band size cannot change the picture.
* **`CoatMasks.Build`** — a run of independent searches over the same body.
  Became the longest thing left once Outline was banded (190 ms at 512), so it
  got the same treatment: `CoatMasks.BuildOverFrames`, with `Build` drained
  from it.

New public entry points: `CoatBuilder.TryBuildForOverFrames` and
`TryBuildOverFrames`. The synchronous `TryBuildFor` / `TryBuild` stay for the
editor bakers (`BakeDefaultCoats`, `BakeTraitSet`), the coat harness and
anything without a MonoBehaviour to run a coroutine on.

**Proof the picture did not change:** `build/coat-harness` renders 26 coats
through `CoatBuilder.Build`. Run from a worktree at HEAD and from this change,
the two output directories are **byte-identical, 26 of 26** (`diff -rq`), and
re-checked after each of the two internal splits. `EYE CHECK PASSED worst
white_solid 122%` both times.

## Callers

| where | was | is |
|---|---|---|
| `DebugGameView.RenderCat` :750 | `TryBuildFor(…, 256)` inline | paints the silhouette, then `StartCoroutine(BuildCatCoat)` :786 |
| `DebugGameView.ShowCatCard` :551 | `TryBuildFor(…, 512)` inline | `StartCoroutine(OpenCatCard)` :562, coat built before the card |
| `MeetYourCatScreen.Build` :175 | `TryBuildFor(…, 256)` inline | silhouette now, `StartCoroutine` :198 for the coat |

`MeetYourCatScreen.Build` is not a coroutine and the class is a MonoBehaviour,
so it starts one. **The number decided it, not a preference:** 256 measured
74 ms of held main thread, four frames at 60 Hz, on the screen where she is
watching her cat appear. Had it come in under a frame it would have been left
alone. The synchronous path is kept there behind `isActiveAndEnabled` for a
caller that news the screen up on a dead object.

The cat card is the one place the screen waits rather than the phone: the coat
is built before `CatCardScreen.Build` because that class composes the kitten
into its stage at build time (`Prop(cat)` returns null for a null texture, and
there is then no element to fill in later). The board underneath goes on drawing
throughout. `_openingCard` keeps a second tap from starting a second build.

## Measurement — before and after

emulator-5554, `board.txt`, rolled cat `cream/bicolor/short/green` (not the
default — the baked coats cost nothing and would measure nothing). Coat cache
`coat_v4_*.png` deleted before every run, so every number is a real build.
Both APKs built by `BuildScript.BuildAndroidPlayer`; the "before" APK is HEAD
plus the `[Perf] coat 512` stopwatch and nothing else.

**Before** — synchronous, verbatim from `adb logcat -s Unity`:

```
[Perf] board ready 134ms
[Perf] coat 256 74ms
[Perf] coat 512 243ms
```

0 frames drawn during either, by construction: nothing yielded.

**After** — verbatim:

```
[Perf] board ready 59ms
[Perf] coat 256 899ms, 24 frame(s) drawn while it ran, longest stage masks 11ms
[Perf] coat 512 1086ms, 32 frame(s) drawn while it ran, longest stage masks 46ms
```

and the same at cat state 2 (`level 10 room_05 0`, four rooms replayed):

```
[Perf] board ready 68ms
[Perf] coat 256 881ms, 24 frame(s) drawn while it ran, longest stage masks 12ms
[Perf] coat 512 1074ms, 32 frame(s) drawn while it ran, longest stage masks 45ms
```

**Read the third number, not the first.** Wall-clock went *up* — the build now
waits for the screen between stages, and a frame on this emulator is ~35–45 ms,
so 24 frames is most of that 899 ms. What the task is about is the longest
single stretch the main thread was held, and that is what `longest stage` counts:

| | held, before | held, after |
|---|---|---|
| coat at 256 | 74 ms | **11 ms** |
| coat at 512 | 243 ms | **46 ms** |
| `board ready` | 134 ms | **59 ms** |

`board ready` is the independent check: the board's opening no longer contains
the coat at all, and it halved without anything else on that path changing.

The two internal cuts were each worth their build, and the log says so. With
seams between passes only:

```
[Perf] coat 256 415ms, 8 frame(s) drawn while it ran
[Perf] coat 512 420ms, 8 frame(s) drawn while it ran
```

with `Outline` banded (the stage clock now stopped across the yield — left
running it had charged CoatMasks with the board's own first render, 247 ms for
a pass that takes 12):

```
[Perf] coat 256 666ms, 15 frame(s) drawn while it ran, longest stage masks 47ms
[Perf] coat 512 916ms, 23 frame(s) drawn while it ran, longest stage masks 190ms
```

## Texture ownership (the audit's second finding)

**(a) There is no leak, and there never was.** The comment at
`HouseMapView.cs:967` warned that the board's cat texture goes unreleased on
every return to the map because `DebugGameView` has no `OnDestroy`. Checked by
grep rather than by reading the comment: the board creates exactly **one**
texture of its own — the 1080×1080 share card in `RenderShareCard` — and
destroys it in the same method, with no early return between the two. Everything
else it paints is a `Resources` asset or belongs to `CoatBuilder`'s static
caches, which are meant to outlive it and are what make returning to a room
free. There is nothing for an `OnDestroy` to release. The comment has been
corrected in place rather than deleted, so the claim does not come back.

**(b) The real fault was the opposite one, and it was in the board.**
`DebugGameView.RenderCat` called `Destroy(_catTexture)` on every state change —
on a texture `CoatBuilder._builtCache` still holds. Two ways that is wrong:

* for the **default cat** `TryBuildFor` hands back the *baked Resources asset*
  (`Art/coat_default_N`, `CoatBuilder.cs:698`), and destroying a Resources asset
  takes the art out of the game for the rest of the run;
* for anyone else the cache entry is shared with every other screen asking for
  the same cat at the same size, so the board tidying up on a state change
  emptied the meet screen's and the card's copy too.

The line is gone. `_builtCache`'s doc now states the rule in one sentence — the
dictionary owns what is in it, no caller destroys what `TryBuildFor` returns —
so the next person does not have to re-derive it. Nothing in `BuildCatCoat`
allocates a texture, so nothing in it frees one.

## Not done

* **The 512 coat still holds 46 ms in one stage.** Three frames at 60 Hz, down
  from fifteen. It is one of `CoatMasks`'s searches; the `[Perf]` label says
  only "masks" and not which. By arithmetic the candidate is
  `Blur(Patches(…), radius 8)` for `pattern_bicolor` — a 17×17 window over
  512×512, some 76 M reads, larger than anything else in that file — but that is
  inference, not measurement. Whoever wants it under a frame should label the
  yields in `CoatMasks.BuildOverFrames` first and measure, not start cutting.
* **The mid-session state change was not reproduced on the emulator.** Cat state
  moves only after the 4th and 8th completed *room*, which is four played rooms,
  so `RenderCat` cannot be made to fire twice with different states in one
  session by hand. State 2 was reached instead by resuming at level 10 and
  measured above, and the code path is the same `BuildCatCoat` either way; the
  guard against a second coroutine (`_catTextureState` claimed before the work,
  re-checked before painting) is argued from the code, not from a run.
* **iOS was not run.** The change is engine-neutral C# with no platform branch.

## Verify

* `dotnet test build/core-tests/core-tests.csproj` — **279 passed, 0 failed**,
  both before and after. Nothing in `Core` was touched.
* `[BuildScript] result=Succeeded path=build/android/CatShelter.apk errors=0`
  (`/tmp/build-coat.log`).
* `build/coat-harness` — 26 of 26 coats byte-identical to HEAD.
* Screens photographed on emulator-5554: board portrait, cat card at state 1,
  cat card at state 2 (with the bowl). All draw the tinted coat, not the
  silhouette.
* `board.save` crafted for the state-2 run was removed from the device
  afterwards; `board.txt` was already there and was left alone.

## Независимая сверка координатором — 2026-09-02

Не со слов исполнителя:

1. **Побайтная тождественность шуб** перепроверена своим способом:
   `build/coat-harness` прогнан на текущем дереве, затем `git stash`, прогон
   на HEAD, `git stash pop`, `diff -rq` двух выдач — **47 файлов, ноль
   расхождений**. Резка по кадрам не сдвинула ни одного пикселя.
2. **Числа сняты своим прогоном** на свежей сборке (эмулятор, кэш очищен
   `pm clear`): `board ready 67ms`, `coat 256 925ms, 24 frame(s), longest
   stage masks 11ms`, `coat 512 1082ms, 32 frame(s), longest stage masks
   45ms`. Совпадает с докладом исполнителя.
3. **Снимки экрана**, которых в каталоге задачи не было, сняты и положены
   здесь: `emulator-board-coat.png` (портрет на доске — шуба, не силуэт) и
   `emulator-cat-card-512.png` (карточка кота, 512).

Что это меняет в оценке: время «от нажатия до готовой шубы» ВЫРОСЛО (74 мс
одним куском стали 925 мс с уступкой кадров), и это не ухудшение — экран всё
это время живой. Ухудшением было бы обратное: 74 мс замершего кадра игрок
видит как рывок, 925 мс живого ожидания — как работу.
