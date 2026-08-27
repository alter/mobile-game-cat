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
