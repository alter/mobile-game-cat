# Independent verification, 2026-08-27

**Verifier:** a fresh context. I wrote none of `Plugins/iOS/CatVision.swift`,
`Shell/CatVision.cs`, this task's `NOTES.md`, nor `06-outcome-handling`
(read only, for cross-reference). No adb, no emulator, no Unity build, no
device. Touched only this task's directory.

## 1. VERIFY items against the actual Swift/C#

**1 — run on a physical device: not met, and `NOTES.md` already says so.**
"1. Run against the reference set on a physical device — **not met**, and
the simulator cannot stand in... nothing runs on hardware." That is the
task's own account, not a new finding — but it is the load-bearing item, and
`status:done` sits above an admission that VERIFY 1 failed.

**2 — cats/dogs/empty classified correctly:** true, but not of the shipped
plugin (see §2). **3 — bounding boxes match by eye:** `box-check.jpg`
exists; not independently re-checked here (no way to see the source photos,
gitignored per `01-reference-photo-set`).

## 2. The crux — plugin numbers or probe numbers

**Only suggested, not met.** The one real run of `CatVision.swift`, in the
simulator, returned `{"ok":false,"error":"vision failed: Could not create
inference context"}` for **41 of 41** images — every single one, no
exceptions. The confidence table in `NOTES.md` (18/20 cats, 5/5 dogs, etc.)
is explicitly sourced to a *different* program: "the same
`VNRecognizeAnimalsRequest`... called from **the macOS probe**." No file
matching that description exists anywhere in this repository (`find . -iname
"*probe*"` finds only unrelated Android build logs) — it is not checked in,
not re-runnable, and its equivalence to the iOS plugin (same model weights,
same preprocessing) is asserted, not shown. Worth noting too: "recognises 30
of them" (line 100) does not match the table's own total of 32 found
(18+5+3+4+0+2) — a small, unexplained discrepancy in the one source that
exists for the probe's output.

So the OUTCOME — "a C# call that, given a photo, returns whether an animal
was found... **on device**" — has never once succeeded on the thing it
names. What exists is evidence that Vision *can* produce these numbers on
some machine, not that this plugin does.

## 3. The threshold's real source

`06-outcome-handling/NOTES.md`: "0.60... comes from the 41 photographs in
`05-vision-plugin/NOTES.md`." Those 41 numbers are the macOS probe's, per
§2. **The threshold was tuned on a program that is not the one that will
run**, not disclosed as such in either document until now.

## 4. Error path vs. "no animal," multiple animals, dog

Swift: any `handler.perform` failure returns `ok:false, error:"..."`,
`detections:[]` (`CatVision.swift:73-77`). C#'s
`FoundAnimal => ok && detections != null && detections.Length > 0`
(`CatVision.cs:27`) makes `ok:false` indistinguishable from "ran fine, found
nothing" — both drive `PhotoJudge.Judge(null, 0)` → `NoAnimal` →
"No cat in this one." **`answer.error` is read nowhere in `CaptureScreen.cs`**
(confirmed by grep); only the debug-only `VisionSelfTest.cs` logs it. A
device failure and an empty frame tell the player the identical thing and
leave no trace of which happened.

**Several animals:** Swift returns every detection, sorted by confidence
(`CatVision.swift:97`); C# takes the single highest-confidence one
(`CatVision.cs:34`, since `06`). One detection decides the outcome even when
two are present — consistent with the task's own singular framing ("which
species, a confidence value"), not a defect against SCOPE, but worth naming
since a higher-confidence dog in frame with a cat would read as `Dog`.

**Dog:** `identifier == "Dog"` reaches `PhotoJudge` and maps to
`PhotoOutcome.Dog` correctly — the one path with a real, if indirect,
evidence trail (Vision's own two-species enum is well documented; nothing
device-specific about matching a string).

Zero test coverage for any of this: `grep -rln "CatVision\|VisionAnswer"
game/Assets/Tests` returns nothing.

## How to reproduce

```bash
sed -n '1,20p;85,105p' tasks/50-photo/05-vision-plugin/NOTES.md
grep -n "ok = false\|FoundAnimal" game/Assets/Shell/CatVision.cs
grep -rn "answer\.error" game/Assets/View/CaptureScreen.cs game/Assets/Shell/VisionSelfTest.cs
find . -iname "*probe*"
grep -rln "CatVision\|VisionAnswer" game/Assets/Tests
```

## What was not checked

- The macOS probe's actual code/methodology — does not exist in this repo
  to inspect.
- Bounding-box accuracy — `box-check.jpg` not re-derived from source images
  (gitignored).
- Whether `10-accounts/02`/`14-testflight` have since produced a device run
  — checked only as of this pass.

## Overall verdict: **verify:failed**

Not a nitpick: the plugin this task delivers has a 0-for-41 real-world
record, the numbers backing every other VERIFY item come from an unchecked,
unrepo'd second program, and the threshold `06` built on top inherited that
same substitution silently. `06`'s branching logic was sound where tested;
here the thing under test has not actually been shown to work at all.
`status:` left untouched — a judgment call for whoever owns it, not this
document.

## Re-verification, 2026-08-28 — after the probe was checked in

**Verifier: a fresh context, wrote none of `tools/vision-probe/`, `NOTES.md`'s
2026-08-27 section, `Core/VisionAnswer.cs`, or `CaptureScreen.cs`.** No
device, no Xcode build, no adb/emulator.

**1. Ran the probe myself — reproduces at the precision claimed, on a third
occasion.** `swiftc -O vision-probe.swift` (clean compile), run against all
41 `fixtures/reference-photos` on macOS 26.0.1 — the same OS build the
2026-08-27 re-run used. Categorical results and rounded means matched both
prior runs exactly: 18/20 cats (same misses `cat_10`/`cat_20`), 5/5 dogs,
0/5 empty, 4/5 blurry (`blurry_04` miss), 3/3 multi, 2/3 ofphoto
(`ofphoto_02` miss); means 0.70/0.74/0.73/0.78/0.63. The three
threshold-setting values reproduced to the same three decimals `NOTES.md`
reports: `cat_03` 0.5981, `cat_12` 0.6019, `cat_14` 0.5970 — each within
0.005 of the original table's 0.60/0.60/0.60. All nine values `NOTES.md`
flags as drifted from the *original* 2026-08-26 table matched *my* run
closely (e.g. `cat_17` 0.8098 here vs. 0.81 in the 08-27 re-run vs. 0.72
originally) — meaning the drift is between the original run's environment
and the current one, not noise between successive runs today. The
reproduction claim holds exactly as stated.

**2. Does not close the failure — it closes a different, narrower gap.**
The original verdict's crux: the OUTCOME names a C# call that works "on
device," and the plugin's own record was 0/41 everywhere it had run, with
every quoted number sourced to an unchecked program. The probe fixes
exactly the "unchecked" half — the numbers are now independently
reproducible by anyone with a Mac, confirmed here by a third party. It does
not and cannot fix the other half: `tools/vision-probe/vision-probe.swift`
itself says so ("This is NOT the shipped plugin. No `@_cdecl` bridge, no
C#/IL2CPP marshalling, and macOS Vision is not guaranteed to run the same
model an iPhone's neural engine does"), and `NOTES.md` repeats it
unprompted ("The plugin's own record is unchanged by any of this: still 0
of 41, everywhere it has ever been run"). Reproducible-on-a-Mac and
true-of-the-plugin are different claims; only the first has been repaired.
VERIFY 1 in `task.txt` — "run against all 40 reference-set images **on a
physical device**" — is still not met, and nothing added since the failed
verdict claims otherwise.

**3. The error-path split is real and produces different messages.**
`Core/VisionAnswer.Failed` now exists (`!ok`), tested in
`Tests/Core/VisionAnswerTests.cs` (`dotnet test --filter VisionAnswerTests`
→ 8 passed) including a test that a `Failed`/found-nothing/found-something
answer are mutually exclusive. `CaptureScreen.Handle()` checks
`answer.Failed` **before** calling `PhotoJudge.Judge`: on failure it shows
`Copy.Of("photo.our_fault")` ("Something went wrong on our side. Try that
one again?") and logs the real error via `Debug.LogWarning`; an
empty-frame, Vision-ran-fine result reaches `PhotoJudge.Judge(null, 0)` →
`PhotoOutcome.NoAnimal` → `Copy.Of("photo.no_animal")` ("No cat in this
one. Try a photo where she fills more of the frame."). Two different keys,
two different sentences, confirmed by reading both call sites — the
indistinguishability the failed verdict named is fixed.

**4. What is left, precisely, and who owns it.** One thing: a run of this
plugin on a physical iPhone, VERIFY 1. `NOTES.md` attributes this to
`60-shell-build/14-testflight`, which is broader than necessary — reading
`14-testflight/task.txt`, that task is about **distribution** (upload to
App Store Connect, an internal testing group, the App Privacy
declaration), gated on `13-headless-build`'s signed `.ipa`, which is gated
on `APPLE_TEAM_ID` (D17) and ultimately on `10-accounts/02`'s paid
developer account. None of that is strictly required just to *run the
generated Xcode project on one physical iPhone via Xcode* — Apple allows
free-tier local-device development signing without a paid Program
membership. So the precise remaining gap is smaller than "wait for
TestFlight": anyone with a Mac, Xcode, and a personal iPhone can open
`game/build/ios/CatShelter` (or a fresh build from it) and run it directly,
today, without `10-accounts/02` or `14-testflight` completing first — and
only that closes VERIFY 1. `10-accounts/02`/`13`/`14` remain the path to
*distributing* a signed build to other testers, a separate and larger
question this task does not need answered.

**Verdict: `verify:failed` stands, for a narrower reason than the original
document gave.** The unrepo'd-program problem is fixed and independently
reproduced twice more (once here, once in the 08-27 note). The
indistinguishable-error-message problem is fixed and tested. What remains
is exactly and only VERIFY 1: no run of `CatVision.swift` on hardware
exists anywhere, still. `status:` untouched, same reasoning as the original
document — a call for whoever owns the device run, not this one.

### How to reproduce

```sh
cd tools/vision-probe && swiftc -O vision-probe.swift -o /tmp/vision-probe
/tmp/vision-probe ../../fixtures/reference-photos > /tmp/out.jsonl
wc -l /tmp/out.jsonl   # -> 41
# compare per-category found/miss/mean-confidence against NOTES.md's tables
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~VisionAnswerTests"   # -> 8 passed
grep -n "answer.Failed" game/Assets/View/CaptureScreen.cs
grep -n "photo.our_fault\|photo.no_animal" game/Assets/Shell/Copy.cs
```

### What was not checked (this pass)

- No device run was attempted or is possible under these constraints —
  VERIFY 1 stays unverifiable from here by construction.
- Did not re-verify bounding-box accuracy (`box-check.jpg`) or the
  several-animals/dog paths — unchanged since the original failed verdict,
  not part of this pass's four questions.
- Did not confirm whether a personal, unpaid Apple ID can actually sign and
  run *this specific* generated Xcode project end to end (IL2CPP framework
  embedding, minimum iOS 15 target, etc.) — the general Apple policy is
  correct, but this project's specific build was not tried against it.
