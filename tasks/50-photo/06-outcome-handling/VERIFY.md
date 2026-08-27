# Re-verification, 2026-08-27 — ruling on a different agent's fixes to the 2026-08-27 failed VERIFY.md above

**Verifier:** fresh context. Wrote none of the fixes below (`Core/PhotoMessages.cs`,
`Shell/PhotoMessages.cs`, `Tests/Core/PhotoMessageKeyTests.cs`,
`Shell/CatVision.cs`'s `Best`/`Failed`, `View/CaptureScreen.cs`'s `Failed`
branch, this `NOTES.md` addendum) or the original failing `VERIFY.md`. No
build/adb/emulator. Ran `dotnet test` on the real repo (169/169) and
`build/check-core-purity.sh` (OK). One mutation probe outside the repo.

## Per-finding ruling

| # | Original finding | Status |
|---|---|---|
| 1 | `PhotoMessages` totality untested | **closed** |
| 2 | `Best` rested on a Swift comment, untested | **partly closed** — fix is real, coverage gap is not |
| 3 | Device failure indistinguishable from empty frame | **closed** |
| 4 | No outcome ever came from real Vision | **closed, transparently** |

**1 — closed.** Mapping moved to `Core/PhotoMessageKey.For`, guarded both
ways: `EveryOutcomeMapsToANonEmptyKey` and — the direction that was actually
missing — `EveryKeyExistsInTheCopyTable`, which reads real `Copy.cs` and
fails (not skips) if the file can't be found. `Shell/PhotoMessages.For` is
now one line, `Copy.Of(PhotoMessageKey.For(outcome))` — a lookup, not a
second copy. 4/4 new tests pass, run myself.

**2 — partly closed.** `VisionAnswer.Best` now does
`detections.OrderByDescending(d => d.confidence).First()` — no longer reads
Swift's ordering at all. Confirmed by mutation outside the repo: fed an
unsorted `[Dog 0.55, Cat 0.91]`, old `detections[0]` returns **Dog** (wrong),
new sort returns **Cat** (right). The defect is genuinely gone. But
`grep -rln "VisionAnswer\|AnimalBox" game/Assets/Tests` is still empty — no
committed test exercises this in either language. The original finding named
two things, correctness and coverage; only correctness is fixed. Structural,
not negligence — `VisionAnswer` lives in `Shell`, which `dotnet test` cannot
compile — but the gap named in the finding is still there.

**3 — closed.** `VisionAnswer.Failed => !ok`, checked first in
`CaptureScreen.Handle`, returns `Copy.Of("photo.our_fault")` before
`PhotoJudge.Judge` is ever called — structurally cannot conflate with
`NoAnimal`. Three distinct Copy keys confirmed present and different:
`photo.our_fault`, `photo.no_animal`, and the Dog/UnclearCat/Cat set.

**4 — closed, and enough.** `NOTES.md`'s new section names all four facts
(simulator can't load Vision, the confidence table is from a separate macOS
probe, the one real-build run used a hand-typed stub, no device has run it)
and states plainly: "`60-shell-build/14-testflight` owns closing this." A
reader of this file alone now sees the gap and where it's tracked.

## How to reproduce

```bash
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~PhotoMessageKeyTests"  # 4/4
grep -rln "VisionAnswer\|AnimalBox" game/Assets/Tests   # still empty
mkdir -p /tmp/best-mutation && cd /tmp/best-mutation
cat > probe.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>
<TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>
EOF
cat > Program.cs <<'EOF'
using System.Linq;
struct AnimalBox { public string identifier; public float confidence; }
var unsorted = new[] {
    new AnimalBox { identifier = "Dog", confidence = 0.55f },
    new AnimalBox { identifier = "Cat", confidence = 0.91f } };
System.Console.WriteLine("old: " + unsorted[0].identifier);
System.Console.WriteLine("new: " + unsorted.OrderByDescending(d => d.confidence).First().identifier);
EOF
dotnet run --project probe.csproj   # old: Dog, new: Cat
```

## What was not checked

Same boundaries as the original `VERIFY.md`: no device run exists to check
(item 4's own subject); the macOS probe's methodology was not re-inspected;
`PhotoMessages`'s wording is `12-copy-english`'s scope, not checked here.

## Verdict

`verify:failed`, kept — not softened. Items 1, 3, and 4 are genuinely
closed. Item 2's underlying bug is fixed and independently mutation-confirmed,
but its coverage half is not: no test in either language guards
`VisionAnswer.Best` today, so a future regression there would go unnoticed
again. `status:` stays `done`; fix the one gap named above (a C# test is
possible if `VisionAnswer`/`AnimalBox` move somewhere `dotnet test` can
reach, or an `XCTest` on the Swift side) and this passes cleanly.
