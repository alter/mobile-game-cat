# Independent verification, 2026-08-27

**Verifier:** a fresh context, invoked specifically to check this task. I
wrote none of `Core/PhotoOutcome.cs`, `Shell/PhotoMessages.cs`,
`View/CaptureScreen.cs`, `Shell/CatVision.cs`,
`Plugins/iOS/CatVision.swift`, `Tests/Core/PhotoJudgeTests.cs`, this task's
`NOTES.md`, nor `60-shell-build/08-mid-level-save`,
`70-analytics`, or `80-live-validation` NOTES referenced below. I did **not**
run a Unity build, did not use `adb`, did not install anything on a device
or simulator. Every mutation below was run against a scratch copy outside
the repository — the tracked files were never touched (confirmed with
`git status` before and after). `dotnet test build/core-tests/core-tests.csproj`
was run against the real, unmodified repo for citation.

## 1. Four outcomes, total mapping — code holds, tests are asymmetric

`Core/PhotoOutcome.cs`: exactly four enum values (`NoAnimal`, `Dog`,
`UnclearCat`, `Cat`). `Shell/PhotoMessages.cs`'s `For(PhotoOutcome)` switches
on all four and has a `default: throw new ArgumentOutOfRangeException(...)` —
so a fifth value would fail loudly rather than silently, and each of the
four resolves to a real copy key confirmed present in `Shell/Copy.cs:115-118`
(`photo.no_animal`, `photo.dog`, `photo.unclear`, `photo.accepted`). The
mapping is total by inspection: no outcome without copy, no copy without an
outcome.

**But `PhotoMessages` has zero test coverage.** `grep -rn "PhotoMessages"
game/Assets/Tests` and `grep -rln "PhotoMessages" game/Assets/Tests` both
return nothing. Nothing in the 152-test suite calls `PhotoMessages.For`,
confirms the four keys exist, or confirms the `default` branch throws. The
totality claim in this task's own OUTCOME line ("no fifth silent case") is
enforced by the code but unverified by any automated check — a regression
here (a typo'd `case`, a missing key) would only surface by eye.

## 2. The boundary — matches the reference doc, tested at the exact edge; multi-detection selection does not

`PhotoJudge.MinimumConfidence = 0.60f`, compared as `confidence >=
MinimumConfidence` (`Core/PhotoOutcome.cs:44,67`). `cat-shelter-tech.md`
section 3, line 261: `if cat, confidence < 0.6 → "Photo's unclear..."` —
same boundary, `<` there is the mirror of `>=` here. Match.

**At exactly the threshold:** `PhotoJudgeTests.BoundariesAreWhereTheyAreDeclared`
has `[TestCase("Cat", 0.60f, PhotoOutcome.Cat)]` next to `[TestCase("Cat",
0.59f, PhotoOutcome.UnclearCat)]` — the edge itself is pinned, not just the
interior. Confirmed by running it: 15/15 `PhotoJudgeTests` pass (below).

**Multiple detections — chosen by the plugin, unverified by any test.**
`Judge` takes one identifier/confidence; the reduction from "several
detections" to "one" happens in `Shell/CatVision.cs:27`:
`public AnimalBox Best => detections[0]; // plugin sorts by confidence`. That
comment is the entire guarantee from the C# side. Read
`Plugins/iOS/CatVision.swift:97`: `detections.sort { $0.confidence >
$1.confidence }` — confirms the claim is true in the shipped Swift source
today. But `grep -rln "VisionAnswer\|AnimalBox" game/Assets/Tests` returns
nothing — no C# test constructs a multi-detection `VisionAnswer` and checks
`.Best`, and there is no Swift `XCTest` for the sort either. The correctness
of "best" rests entirely on one line of Swift, read and confirmed by hand
today, guarded by no test in either language.

## 3. Reachability — all four exist in code and tests; **none has ever come from a real on-device Vision call**

All four are reachable from `PhotoJudge.Judge` (item 1) and from
`CaptureScreen.Handle` given a `VisionAnswer` (empty/absent identifier →
`NoAnimal`, `"Dog"` → `Dog`, `"Cat"` below 0.60 → `UnclearCat`, `"Cat"` at or
above → `Cat`). That much is not in question.

What *produced* each of the four, tracing every occurrence back:

- **The 41-image confidence table** inlined in `PhotoJudgeTests.cs` (and
  restated in `PhotoOutcome.cs`'s own doc comment) is not from the shipped
  app. `50-photo/05-vision-plugin/NOTES.md`: the real plugin, run inside a
  real iOS build **in the simulator**, returned
  `{"ok":false,"error":"vision failed: Could not create inference context"}`
  for **41 of 41** images — Vision does not load under the simulator at all.
  The confidence numbers actually used come from "**the macOS probe**" — a
  separate command-line tool calling the same `VNRecognizeAnimalsRequest`
  API directly on macOS, not through `Plugins/iOS/CatVision.swift`, not
  through the C# bridge, not on iOS. Real Vision output, different code
  path than what ships.
- **The one time all four branches were driven through the real app**
  (`50-photo/08-capture-screen/NOTES.md`, "All four messages, driven through
  a real iOS build") used a **hand-typed stub** — `capture.txt`'s second
  line, `fake Cat 0.80` and similar — because, per that same document,
  "Vision cannot run in the simulator... so only the 'no cat' branch would
  ever be reachable" for real.
- **On physical hardware:** never run. `05-vision-plugin/NOTES.md`: "There
  is no developer team yet (`10-accounts/02`), so nothing runs on hardware,"
  left open for `14-testflight`.

So: **every one of the four outcomes, as produced through
`CaptureScreen`+`CatVision.swift`, has only ever come from a stub.** The only
genuine Vision-API evidence in the project is the macOS probe's output, a
different binary entirely. This task's own VERIFY 1 ("run all 40
reference-set images through the pipeline") is satisfied only if "the
pipeline" is read as "`PhotoJudge` fed by macOS-probe numbers" — a
substitution this task's `NOTES.md` never states plainly, though `05` and
`08`'s do.

## 4. Mutation testing — the boundary is genuinely guarded

Copied `game/Assets/Core` and `game/Assets/Tests/Core` to a scratch project
outside the repository (`private/tmp/.../scratchpad/mutation-06`, a
standalone `.csproj`, not `build/core-tests`). Baseline: 137/152 pass; the 15
failures are pre-existing path-resolution issues unrelated to this task
(`TheAllowedValuesMatchTheWorkerSchema` and several `worker/`/level-JSON
cross-file tests that walk up to the real repo root, which does not exist
above the scratch copy) — none are `PhotoJudgeTests`.

**Mutation 1** — `confidence >= MinimumConfidence` → `confidence >
MinimumConfidence`: two new failures, both real —
`BoundariesAreWhereTheyAreDeclared("Cat",0.6f,Cat)` and
`EighteenOfTwentyCatsAreAccepted_TheOtherTwoAreNotSeenAtAll` (three cats sit
at exactly 0.60; flipping the operator drops them to `UnclearCat`).

**Mutation 2** — `MinimumConfidence = 0.60f` → `0.65f`: five new failures —
the two above plus `BlurryAndMultiCatImagesAreHandled_NeverUnclassified`,
`PhotographsOfAScreenAreAcceptedAsCats_AndTheThresholdCannotHelp`, and
`TheThresholdSitsAtTheBottomOfTheObservedRange`.

The threshold cannot move, by either the comparison or the constant, without
multiple tests failing immediately. This is the opposite of the finding the
task asked me to check for.

## 5. `PhotoJudge.Accepts` vs. `photo:uploaded` — not the same condition, already on record

`View/CaptureScreen.cs:170-192`: a rejected outcome (`!PhotoJudge.Accepts`)
fires `Analytics.PhotoRejected()` and returns. An accepted outcome proceeds
to `Crop(...)`; **if the crop fails** (`prepared == null`), it fires
`Analytics.PhotoRejected()` again and returns — `Analytics.PhotoUploaded()`
is only reached after a successful crop, one branch later. So
`PhotoJudge.Accepts(outcome)` is **necessary but not sufficient** for
`photo:uploaded`. This exact gap is already named in
`80-live-validation/00-thresholds/NOTES.md` ("Metric two does not mean what
its name says"): *"`Analytics.PhotoUploaded()` fires... immediately after the
crop succeeds... the event means 'the photo passed the on-device cat check
and was cropped.'"* Confirmed independently here, not a new finding, but the
coordinator's question has a definite answer: **they diverge.**

## How to reproduce

```bash
# item 1 — no PhotoMessages coverage
grep -rn "PhotoMessages" game/Assets/Tests   # empty

# item 2 — multi-detection selection untested
grep -rln "VisionAnswer\|AnimalBox" game/Assets/Tests   # empty
sed -n '97p' game/Assets/Plugins/iOS/CatVision.swift
# detections.sort { $0.confidence > $1.confidence }

# item 3 — reachability, sourced
grep -n "simulator\|stub\|real Vision" tasks/50-photo/08-capture-screen/NOTES.md
grep -n "macOS probe\|Could not create inference context\|no developer team" \
  tasks/50-photo/05-vision-plugin/NOTES.md

# item 3 (repo, clean state) — PhotoJudgeTests pass count
dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~PhotoJudgeTests"
# Пройден!: не пройдено 0, пройдено 15, всего 15

# item 4 — mutation, on a scratch copy, never touching the repo
SCRATCH=/tmp/mutation-06   # any scratch dir outside the repo
mkdir -p "$SCRATCH" && cp -R game/Assets/Core "$SCRATCH/Core" \
  && cp -R game/Assets/Tests/Core "$SCRATCH/TestsCore" \
  && find "$SCRATCH" -name "*.meta" -delete
cat > "$SCRATCH/mutation-test.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings><LangVersion>9</LangVersion>
    <RollForward>Major</RollForward><IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup><Compile Include="Core/**/*.cs" /><Compile Include="TestsCore/**/*.cs" /></ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
EOF
cd "$SCRATCH" && dotnet test mutation-test.csproj --nologo   # baseline: 137/152
sed -i '' 's/confidence >= MinimumConfidence/confidence > MinimumConfidence/' Core/PhotoOutcome.cs
dotnet test mutation-test.csproj --nologo   # now 135/152 — 2 new failures

# item 5
sed -n '170,192p' game/Assets/View/CaptureScreen.cs
grep -n "Metric two does not mean" tasks/80-live-validation/00-thresholds/NOTES.md
```

## What was not checked

- Whether Vision ever runs on real hardware — cannot be checked without a
  device, which does not exist in this project (`10-accounts/02`,
  `14-testflight`). This is the root cause of item 3's finding, not
  something this verification could close.
- Whether `PhotoMessages`'s copy is correct/well-worded — out of scope
  (`12-copy-english` owns wording).
- The macOS probe's own code and methodology — cited from
  `05-vision-plugin/NOTES.md`, not re-run or re-inspected here.
- `Shell/VisionSelfTest.cs` and the `visiontest` debug folder path — read
  only as far as confirming it exists; not exercised.
- Whether a Swift `XCTest` target exists or could be added for
  `CatVision.swift`'s sort — not investigated; noted only that none exists
  today.
- `dotnet test build/core-tests/core-tests.csproj -v q --nologo` on the real
  repo: 152/152 pass, unaffected by anything in this verification (no repo
  file was changed) — confirms the baseline this document's citations rest
  on, not a new claim.

## Overall verdict: **verify:failed**

Item 4 (mutation testing) came back clean — the boundary is genuinely
guarded and this is not the failure. The failure is items 1–3: `PhotoMessages`'s
totality claim, half of this task's own OUTCOME line, has no test at all;
the multi-detection "best" selection has no test in either language and
rests on one comment plus one line of Swift I had to read by hand to
confirm; and — the substantial one — none of the four outcomes this task
claims to handle has ever been produced by a real Vision call through the
code that ships. The `NOTES.md` presents 41 measured images as if they came
from running the pipeline, without stating that the numbers are from a
separate macOS tool and that the one real-build run of all four branches
used a hand-typed stub. Each fact is individually documented elsewhere
(`05`, `08`), but `06`'s own account does not connect them, and a reader of
`06/NOTES.md` alone would not know "the pipeline" in its VERIFY section
never ran with real Vision even once.

None of this means the branching logic is wrong — by every check here it is
right, and well-tested where it is tested. It means the claim as written
outruns what was actually demonstrated, in the same shape `tasks/README.md`
names as the reason this rule exists.
