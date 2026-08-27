# Re-verification, 2026-08-28 — ruling on the last closed quarter

**Verifier:** fresh context. Wrote none of `Core/VisionAnswer.cs`,
`Shell/CatVision.cs`, `Tests/Core/VisionAnswerTests.cs`, the NOTES.md
addendum, the prior `VERIFY.md` (either round), or any earlier fix in this
file's history. No build/adb/emulator. Ran `dotnet test` on the real repo
(177/177, including 8/8 filtered to `VisionAnswerTests` and 8/8 to
`PhotoMessageKeyTests`) and `bash build/check-core-purity.sh` (OK) myself.

## Per-item verdict

| # | Item | Result |
|---|---|---|
| 1 | is the move honest — nothing left in Shell that could have moved, purity gate still passes | **pass** |
| 2 | do the 8 new tests reach the states their names claim; is `Best` throwing the right contract | **pass, with a design note kept visible** |
| 3 | are the three states (failed / found-nothing / found-something) genuinely distinct through `CaptureScreen` to copy | **pass** |
| 4 | rule on the whole task | **pass** |

### 1. The move

`Core/VisionAnswer.cs` holds `AnimalBox` and `VisionAnswer` — plain structs,
`[Serializable]` (`System.SerializableAttribute`, confirmed by reading the
`using` list, no `UnityEngine`). `grep -rn "struct VisionAnswer\|struct
AnimalBox" game/Assets` finds exactly one definition of each, in `Core` —
no shadow copy left in `Shell/CatVision.cs`, which now holds only the
`DllImport`, `Marshal`/`JsonUtility` glue, and `Application.platform`. Every
call site (`Shell/GameBoot.cs`, `Shell/VisionSelfTest.cs`,
`View/CaptureScreen.cs`) references the moved types via `using
CatShelter.Core`, confirmed by grep — none redeclares anything. `bash
build/check-core-purity.sh` → `Core is engine-free: OK`, run myself, not
taken on NOTES.md's word. `dotnet test`: 177 passed, 0 failed, 0 skipped
(up from 169, matching the claimed 8 new).

### 2. The eight tests, and the throw contract

Ran `--filter FullyQualifiedName~VisionAnswerTests`: 8/8 pass. Read each
assertion against its name — `BestPicksTheHighestConfidenceFromAnUnsortedList`
puts Dog(0.91) after two Cats and asserts Dog wins (real regression
coverage for the sort fix, not a rename of an old passing case);
`BestOnAnEmptyListThrows` asserts `InvalidOperationException`, which is
exactly what `.OrderByDescending().First()` throws on an empty sequence —
verified by reading `VisionAnswer.Best`'s implementation directly, not
inferred; `TheThreeStatesAreMutuallyExclusive` exercises all three
reachable `ok`/`detections` combinations and checks both flags on each. No
test asserts a weaker condition than its name implies.

**On the throw itself:** `grep -rn "\.Best\b" game/Assets` finds exactly two
real call sites, `CaptureScreen.cs:183` and `VisionSelfTest.cs:59`, and both
read `answer.FoundAnimal ? answer.Best : default` — the ternary
short-circuits, so `.Best` is never evaluated when it would throw. This
differs from the `CatTraits.FromColourOnly` defect closed in
`50-photo/11-offline-fallback` today: that throw sat on a live, reachable
path with no guard at its one call site. Here every real call site already
guards the precondition, so the throw is presently unreachable, and it
exists to fail loudly if a future call site forgets to — the same shape as
`Enumerable.First()`'s own contract. Worth naming for the record: it is
still an uncaught-exception-in-a-coroutine risk *if* that discipline ever
lapses at a new call site, since nothing in the type system enforces the
guard — but it is not a live defect today, and enforcing it belongs to
whichever task adds the next call site, not to 06.

### 3. Three states, every layer

`VisionAnswer`: `Failed` / `FoundAnimal==false` / `FoundAnimal==true` —
pinned mutually exclusive by test. `CaptureScreen.Handle` (read directly,
`View/CaptureScreen.cs:168-193`): `answer.Failed` returns immediately with
`Copy.Of("photo.our_fault")`, before `PhotoJudge.Judge` is ever called —
structurally cannot merge with "found nothing". `PhotoJudge.Judge(null,
0f)` (the found-nothing case, since `identifier` is passed as `null` when
`!FoundAnimal`) returns `NoAnimal` → `"photo.no_animal"`. Found-something
splits further into `Dog`/`UnclearCat`/`Cat`. `grep -n "photo\." Shell/Copy.cs`
confirms five distinct strings: `no_animal`, `dog`, `unclear`, `accepted`,
`our_fault` — the player reads a different sentence for "we couldn't look"
than for "we looked and saw nothing," at every layer.

### 4. Whole task

All four grounds the original `VERIFY.md` failed on are now closed:
mapping totality (`Core.PhotoMessageKey`, tested both directions, 8/8
`PhotoMessageKeyTests` pass here including the fails-not-skips missing-file
guard), `Best` no longer trusting Swift's ordering (fixed *and* now covered,
closing the one gap the 2026-08-27 re-verification left open), the failure
path split (unchanged, still correct), and the device gap named with its
owner (`NOTES.md`'s "third gap" section still names
`60-shell-build/14-testflight` as the owner, untouched by this fix). **Item
2's coverage half is the one that decides this**, since it was the sole
surviving finding from the prior round — it is now closed, verified by
running the tests myself rather than trusting the count in NOTES.md.

## How to reproduce

```bash
bash build/check-core-purity.sh                                   # Core is engine-free: OK
dotnet test build/core-tests/core-tests.csproj -v q --nologo      # 177 passed, 0 failed, 0 skipped
dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~VisionAnswerTests"                 # 8/8
dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~PhotoMessageKeyTests"               # 8/8

grep -rn "struct VisionAnswer\|struct AnimalBox" game/Assets       # one definition each, in Core
grep -rn "\.Best\b" game/Assets --include="*.cs"                   # two call sites, both guarded by FoundAnimal
grep -n "photo\." game/Assets/Shell/Copy.cs                        # five distinct player-facing keys
```

## What was not checked

- No device/simulator/Unity build run — the device gap named in `NOTES.md`
  ("The third gap") is unchanged by this fix and stays open, owned by
  `60-shell-build/14-testflight`, not re-litigated here.
- Whether a future call site could call `.Best` without the `FoundAnimal`
  guard — flagged as a design note in item 2, not something a repo scan can
  rule out for code that doesn't exist yet.
- `PhotoMessages`' English wording — `12-copy-english`'s scope, not this
  task's or this verification's.
- The Swift side (`Plugins/iOS/CatVision.swift`) itself — only its C#
  consumer was in scope for this pass, matching the finding under review.

## Verdict

`verify: passed`. The move is honest (no logic left duplicated in `Shell`,
purity gate holds), the new tests reach the states their names claim and
run clean (177/177 total, 8/8 and 8/8 on the filtered suites), the three
failure/empty/found states stay distinct through to the copy the player
reads, and the one surviving finding from the prior round — `VisionAnswer`
and `AnimalBox` had zero test coverage because `dotnet test` could not
compile `Shell` — is closed on its merits, independently confirmed rather
than taken on NOTES.md's count.

`status:` stays `done` (already so; nothing to move).
