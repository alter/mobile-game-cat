# Four branches, and the threshold picked from data — 2026-08-26

`Core/PhotoJudge` decides; `Shell/PhotoMessages` words it. The split is
deliberate: which branch a photo falls into is a rule and is tested, while the
wording is copy that `12-copy-english` will rewrite and
`16-localisation-ready` will move into a table.

| outcome | when | what the player is told |
|---|---|---|
| `NoAnimal` | nothing detected, or an identifier Vision should never return | "No cat in this one. Try a photo where she fills more of the frame." |
| `Dog` | identifier `Dog` | "That looks like a dog. Lovely, but this shelter is for cats." |
| `UnclearCat` | `Cat` below the threshold | "A cat, but too blurry to copy her colours. One more, holding still?" |
| `Cat` | `Cat` at or above it | "Got her." → crop, then the model call |

Nothing can miss all four: the identifier is either empty, `Dog`, `Cat`, or
something Vision does not produce — and that last case is routed to `NoAnimal`
rather than guessed, because a species we do not recognise must never be
accepted as a cat.

## The threshold is 0.60, and it is measured

Apple publishes no recommended value, so this one comes from the 41 photographs
in `05-vision-plugin/NOTES.md`. Confidence on genuine cats runs **0.60 to
0.81**, and the four lowest — 0.60, 0.60, 0.60, 0.61 — are ordinary photographs
of ordinary cats. Anything above 0.60 starts rejecting cats that plainly are
cats, so the floor sits exactly at the bottom of the observed range. A test
asserts that equality, so re-measuring on new data forces the constant to move
with it.

**What the threshold cannot do**, and does not pretend to: separate a live cat
from a photograph of one on a screen. Those measured 0.62 and 0.64 — inside the
range, above four genuine cats. It is in the tests as a fact about the product,
not as a defect.

## Against the VERIFY list

Run over the measured results for all 41 images (inlined into the test, since
the photographs themselves are third-party and gitignored):

- **5 of 5 dogs** → `Dog`. **5 of 5 empty frames** → `NoAnimal`. Both exact.
- **Blurry and multi-cat: all handled**, none unclassified — 4 blurry and all
  3 multi accepted as `Cat`, one blurry falls to `NoAnimal`.
- **Cats: 18 accepted, not the 20 VERIFY 2 asks for.** The other two were never
  detected by Vision at all — `cat_10` is the smallest image in the set
  (259×270) and `cat_20` has two kittens filling the frame — so they arrive
  here as "nothing found" and the judge cannot accept what was not seen. This
  is a limit of stage one, not of the branching, and it is pinned by a test
  rather than rounded up.

99 C# tests (84 → 99).

---

## The third gap, assembled from what `05` and `08` each recorded separately — 2026-08-27

`VERIFY.md`'s failure connects three facts that were each already written
down elsewhere in this project, but never joined up here, where "the
pipeline" is named:

- **The real plugin fails 41/41 in the simulator.** `05-vision-plugin/NOTES.md`:
  run inside a real iOS build in the simulator, every one of the 41
  reference images came back `{"ok":false,"error":"vision failed: Could not
  create inference context"}`. Vision does not load under the simulator at
  all.
- **The 41-image confidence table above did not come from `CatVision.swift`.**
  Same document: those numbers are from "the macOS probe" — a separate
  command-line tool calling `VNRecognizeAnimalsRequest` directly on macOS,
  not through the shipped plugin, not through the C# bridge, not on iOS.
- **The only run of all four branches through the real app used a stub.**
  `08-capture-screen/NOTES.md`, "All four messages, driven through a real
  iOS build": `capture.txt`'s second line, `fake Cat 0.80` and similar —
  because Vision cannot run in the simulator, so nothing else was reachable.
- **No device has ever run this.** `05-vision-plugin/NOTES.md`: "There is no
  developer team yet (`10-accounts/02`), so nothing runs on hardware,"
  left open for `14-testflight`.

So every outcome in the table above is real code, tested against real
numbers — but the numbers are from a different binary than the one that
ships, and the four branches have only ever fired for real off a hand-typed
file. **`60-shell-build/14-testflight` owns closing this** — it is the task
already named in both `05` and `08` as where a real device run happens, and
this task adds nothing new to that queue, only makes explicit that it is
also what `06`'s own reachability claim is waiting on.

---

## The last closeable quarter of the failing verdict, fixed after the verdict — 2026-08-28

`VERIFY.md` failed this task on four grounds; three were closed same-day
(`PhotoMessages` moved to `Core.PhotoMessageKey` and tested, `Best` made to
sort defensively, the reachability gap written up above). The fourth stood
until now: `VisionAnswer` and `AnimalBox` had zero test coverage in either
language, because they lived in `Shell/CatVision.cs`, which `dotnet test`
does not compile.

That reason no longer holds. Both types were plain data — `[Serializable]`
is `System.SerializableAttribute`, not Unity's, and neither struct touched
`UnityEngine` anywhere. Moved to `Core/VisionAnswer.cs`; `Shell/CatVision.cs`
keeps only what genuinely cannot move — the `DllImport`, the pointer
marshalling, `Application.platform`. `build/check-core-purity.sh` still
passes. `Core/VisionAnswer.cs`, `game/Assets/Tests/Core/VisionAnswerTests.cs`
(8 new tests: `Best` on an unsorted list, on one detection, on an empty list
— throws, pinned — and the three `Failed`/`FoundAnimal` states kept
mutually exclusive) and three call sites (`GameBoot.cs`, `VisionSelfTest.cs`,
`CatPhoto.cs`) updated to reference the moved types. `dotnet test`: 169 → 177.

This fix came **after** the verdict above, not as part of it — this section
records that ordering, it does not change the verdict. `verify:failed`
stays; a different context rules on whether the file is now closed.
