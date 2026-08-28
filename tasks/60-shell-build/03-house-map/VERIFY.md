Verifier: independent context, wrote none of `game/Assets/View/HouseMapView.cs`,
none of the `PlayerProgress.cs`/`PlayerProgressTests.cs` additions
(`PilesClearedIn`, `CellStateFor`, `Restore`, `RoomCellState`, and the twelve
tests under "60-shell-build/03: house map derivation"), and wrote none of the
task's own `task.txt`/`NOTES.md`. Read `HouseMapView.cs`, `PlayerProgress.cs`
and `PlayerProgressTests.cs` directly rather than trusting NOTES.md's
description of them; cross-checked NOTES.md's citations against
`art-brief.md` section 9, `art-prompts.md`'s "House map" prompt block, and
`tasks/40-art/06-house-map/task.txt`. Ran `dotnet test`, `pytest tools/`,
`check-core-purity.sh`, and a mutation test against a scratch clone outside
the repo. Did **not** run a Unity build, PlayMode test, or perform the
task's own VERIFY (HUMAN) item — see below.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Is cell state truly derived, with nothing cached? | **Pass** | `PlayerProgress.CellStateFor` (PlayerProgress.cs:59-66) computes from `PilesClearedIn(room)` vs `PilesPerRoom[room-1]` on every call; `PilesClearedIn` itself reads only `CurrentRoom`/`CurrentPile`/`_roomsDone` — the same fields `CompletePile` mutates — never a stored per-room state. `grep -rn "RoomCellState" game/Assets` shows the enum used only as a method parameter/return type and in switch statements (`HouseMapView.cs:153,158-160,219,226-248`); no field of that type exists anywhere. `HouseMapView.OnEnable` calls `progress.CellStateFor(room)` fresh inside the render loop (line 102) and never stores the result beyond passing it to `Cell(...)`. |
| 2 | Does a mutation-tested `Restore` disagree with replay, and does a test catch it? | **Pass** | Cloned the repo (`git clone --local`) into scratch, overwrote the two changed files with the current working-tree copies (the clone alone would have missed uncommitted work), confirmed baseline: 17/17 `PlayerProgressTests` green. Mutated `Restore` (PlayerProgress.cs:85) from `progress.CurrentPile = cursorPile;` to `cursorPile > 0 ? cursorPile - 1 : cursorPile` — a one-pile drift. Re-ran: 16/17, with `Restore_MatchesReplayedState` failing exactly on the drifted field (`Assert.That(restored.CurrentPile, ...) Expected: 1 But was: 0`). The other 11 new tests stayed green, as expected — only the replay-equivalence test is positioned to catch this class of bug. |
| 3 | Do flat square / hard-split tile / circle differ at a glance, not by shade, and does NOTES tell the artist the right thing? | **Pass, with a nuance worth recording** | `PaintPlaceholder` (HouseMapView.cs:219-266): dirty is a flat single-tone panel (radius 2), partial is the same panel plus a hard-edged light half at exactly 50% width (no gradient — a real second child element, not a blend), clean is a fully light, fully rounded circle (radius 36) with a mark. Three distinct silhouettes/fill-patterns, not three tints of one shape — passes the letter of "not by shade." Nuance: art-brief.md section 9's actual text is "Not by shade, but visibly: dark, halfway, light" — the *real* art (art-prompts.md "House map", `tasks/40-art/06-house-map/task.txt` SCOPE: "dirty (dark), partial (half-lit...), clean (warm, light) — lightness, not colour") is explicitly a lightness-only scheme, which reads as shade by the plain meaning of the word. NOTES.md catches this itself ("art-prompts.md already commits the real art to lightness-only... not something this task is overriding") and correctly does *not* ask the artist to reproduce the shape trick. What it does tell the artist to preserve — a crisp, ungraded boundary on `partial`, and that all three states of every room stay orderable dark-to-light with colour removed while the full set of twelve still reads "mostly unfinished"/"mostly done" at a glance — matches QA checks 2 and 3 in `tasks/40-art/06-house-map/task.txt` verbatim. The guidance is right; the placeholder is deliberately stricter than the approved real-art plan, and NOTES says so rather than leaving it implicit. |
| 4 | Do the 12 new tests reach the states their names claim, including the single-pile room? | **Pass** | All twelve (`PlayerProgressTests.cs:83-195`) construct the state they name rather than asserting a formula in isolation: `CellStateFor_PartlyCleared_IsPartial` finishes rooms 1-8 then clears 2 of room 9's 4 piles before asserting `Partial`; `CellStateFor_SinglePileRoom_SkipsPartial` uses room 1 specifically — `Curve[0] == 1` — asserting `Dirty` before `CompletePile(0)` and `Clean` immediately after, with no intermediate state possible or asserted, which is the one edge case that could hide an off-by-one in `CellStateFor`'s `cleared >= total` check. `Restore_MatchesReplayedState` compares a replayed and a restored `PlayerProgress` across `CurrentRoom`, `CurrentPile`, `RoomsDone`, and `CellStateFor` for all 12 rooms, not just the headline fields — this is also what let the item-2 mutation get caught. Confirmed by running the suite; see reproduction. |

## How to reproduce

From a clean checkout, no exported variables:

```sh
cd game/build/ios/CatShelter    # repo root
dotnet test build/core-tests/core-tests.csproj -v q --nologo
# -> Пройден!   : не пройдено 0, пройдено 189, всего 189
.venv/bin/python -m pytest tools/ -q
# -> 160 passed
bash build/check-core-purity.sh
# -> Core is engine-free: OK
grep -rn "RoomCellState" game/Assets
# -> only parameter/return-type/switch uses, no stored field
```

Mutation test (outside the repository):

```sh
SP=$(mktemp -d)
git clone --local . "$SP/mutclone"
cp game/Assets/Core/PlayerProgress.cs "$SP/mutclone/game/Assets/Core/PlayerProgress.cs"
cp game/Assets/Tests/Core/PlayerProgressTests.cs "$SP/mutclone/game/Assets/Tests/Core/PlayerProgressTests.cs"
cd "$SP/mutclone"
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~PlayerProgressTests"
# -> baseline: 17/17 pass
# edit Restore(): progress.CurrentPile = cursorPile; -> cursorPile > 0 ? cursorPile - 1 : cursorPile;
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~PlayerProgressTests"
# -> 16/17 pass; Restore_MatchesReplayedState fails: Expected 1, was 0
cd - && rm -rf "$SP"
```

## What was not checked

- **The task's own VERIFY (HUMAN) item** — "a person who did not build it is
  shown the map after two rooms are touched and answers 'how much is left'
  correctly within a couple of seconds" — was not run and cannot be
  substituted by any agent context (README's independence rule: HUMAN-role
  tasks "are not performed and not simulated"). This item is what the task's
  OUTCOME actually rests on and remains open regardless of the verdicts
  above.
- No Unity build, no PlayMode test, no editor session — `HouseMapView.cs` was
  read for correctness, not executed. Whether the placeholder cells actually
  render as described (UI Toolkit layout, `StyleBackground`, absolute
  positioning of the caption/check labels) was not confirmed visually.
- `LevelAssets.LoadAll()`, `RoomPlan`, `GameSave.Read`, and `Shell/SaveFile`
  were read only far enough to confirm `HouseMapView`'s calls into them are
  shaped correctly (`PilesPerRoomInOrder()`, `GameSave.Read(text)` →
  `PlayerProgress.Restore(...)`); their own internal correctness was not
  re-verified here — it is covered by their own existing tests, unchanged by
  this task.
- `labels.txt` still reads `status:todo`; that field was left untouched here
  on purpose — status is the implementing role's call, not the verifier's,
  per the status:done rule.
- The 40-art/06-house-map real-art files do not exist yet (task is
  `status:todo`), so the placeholder-vs-real-art comparison in item 3 is
  necessarily a comparison against written specs, not rendered art.

---

## This pass predates a rewrite of half the file — 2026-08-28

The verification above read a `HouseMapView.cs` whose layout half no longer
exists. On 28.08 the flex-wrap grid was replaced by measured per-room placement
(`Placements`, `Place`, `FitToPicture`), and the background asset was cropped
and given transparency.

**What the pass still covers:** the state derivation — `PlayerProgress`'s
`PilesClearedIn`, `CellStateFor`, `Restore`, `RoomCellState` and their twelve
tests. None of that was touched, and `dotnet test` still reports 189/189.

**What it no longer covers:** everything about where a cell lands and what the
screen looks like. That is now the substance of the task and it has never been
verified by an independent context — only by the author, with screenshots from
both platforms (`ios-house-map-placed.png`, `android-house-map-placed.png`).

`verify:` is left at `passed` for the derivation rather than reset, because
resetting would discard a real independent check of code that did not change.
Anyone re-verifying should treat the layout as unverified and start there.
