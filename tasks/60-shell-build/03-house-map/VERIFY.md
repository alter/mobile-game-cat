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

---

## Second pass — the layout, `AccessFor`, and the tap path — 2026-08-28

Verifier: an independent context. **Wrote none of** `game/Assets/View/HouseMapView.cs`
(no line of `Placements`, `Place`, `FitToPicture`, `Cell`, `StartPlaying`,
`ShowOpening`, `SwapInBoard`), none of `PlayerProgress.AccessFor` or the
`RoomAccess` enum, none of the six `AccessFor_*` tests, none of this task's
`task.txt`, `NOTES.md` or the section above, and none of
`tasks/40-art/06-house-map/ROOM-PLACEMENT.md`. **Did not** run Unity in any
mode, did not build for either platform, did not touch the iOS simulator or the
Android emulator, did not modify any file under `game/Assets/` or `tools/`
(`git diff --stat` on the three files under review is empty), and did not
perform the task's own `VERIFY (HUMAN)` item. Read the sources directly rather
than trusting `NOTES.md`; ran the two test suites; ran two mutations of
`AccessFor` against a copy of `Core` + `Tests` in a scratch directory outside
the repository; opened all the task's PNGs.

### Verdict by item

| # | Question | Verdict |
|---|---|---|
| 1 | Does `Placements` agree with the prose? | **Pass** |
| 2 | Is `FitToPicture`'s letterbox maths right in both directions? | **Pass** |
| 3 | Is "exactly one room open at every point" true, and does the test prove it? | **Fail — the claim is false at the end of the game; the test asserts something weaker than its own name** |
| 4 | Can the `fired` guard leak, or the room open twice? | **Pass** |
| 5 | What happens if the panel is torn down inside `StartPlaying`'s 120ms? | **Not established** (partly settled — see below) |
| 6 | Do the screenshots show what the notes say? | **Mostly — three claims in `NOTES.md` do not match the artefacts** |

### 1. `Placements` — read from the table, not the prose. Pass.

`HouseMapView.cs:233-247`, twelve rows of `{cx, cy, w, h}`. `cy` is a top-down
percentage, so larger `cy` is lower on screen.

- odd left / even right: rooms 01,03,05,07,09 all `cx = 35.0f`; 02,04,06,08,10
  all `cx = 65.0f`. Holds for all ten.
- bottom to top: `cy` = 85, 85, 74, 74, 63, 63, 52, 52, 41, 41 — strictly
  decreasing by 11 per row as the number climbs. Holds.
- 11 and 12 alone under the roof: `{50.5f, 28.5f, …}` and `{50.5f, 17.0f, …}`
  — the only two rows with `cx = 50.5f`, and the only two above `cy = 41`.
  Holds, and 12 is above 11.

Cross-checked against the measured silhouette in `ROOM-PLACEMENT.md` §1, which
gives a constant 640px-wide body inside an 807px house box (= 79.3%, centred →
local x 10.4%–89.6%, matching the `10.9%–90.1%` in the code's own comment at
`HouseMapView.cs:229`). Every body cell spans `cx ± 8.5` → 26.5–43.5% and
56.5–73.5%, inside `ROOM-PLACEMENT.md`'s "usable cavity ≈ local x 19%–81%".
Room 11 spans y 23.5–33.5% where the silhouette interpolates to ≈540px (66.9%,
centred 16.5–83.5%) and room 12 spans y 12–22% where it is ≈363px (45%, centred
27.5–72.5%); both cells are 42–59% wide, so both fit. Rooms 01/02 bottom out at
`cy + h/2 = 90%`, above the `ly 91–100%` rounded base `ROOM-PLACEMENT.md` §3
says to keep clear.

Confirmed visually on both platforms in the current build:
`ios-numbers-in-order.png` and `android-tap-before.png` both show 1 lit at
bottom-left, 2 bottom-right, climbing to 11 and 12 centred under the roof.

Note the current `Placements` is **not** `ROOM-PLACEMENT.md`'s placement table
(§4) — that table is the superseded spatial layout. Only its §1 geometry
survives. `HouseMapView.cs:197-232` says so at length; recording it here because
the task brief pointed at `ROOM-PLACEMENT.md` as "where the placement numbers
came from" and only the house-geometry half of that is still true.

### 2. `FitToPicture` — Pass, both directions.

`HouseMapView.cs:299-336`. `scale = Mathf.Min(r.width / artWidth, r.height / artHeight)`
is a contain-fit, and the two branches fall out of the `Min`:

- image relatively **taller** than the element → `r.width/artWidth` is smaller →
  `pictureWidth = r.width`, `pictureHeight < r.height`, so
  `pictureLeft = 0` and `pictureTop = (r.height - pictureHeight) * 0.5f > 0`.
  Letterboxed top and bottom. Correct.
- image relatively **wider** → `r.height/artHeight` is smaller →
  `pictureHeight = r.height`, `pictureWidth < r.width`, `pictureTop = 0`,
  `pictureLeft > 0`. Pillarboxed left and right. Correct.

`artWidth`/`artHeight` are `int` but divided into a `float`, so no integer
division; the `artWidth > 0 && artHeight > 0` guard at line 307 covers the
no-art path, where the box becomes the whole element as the doc comment says.

Corroborated numerically. `map_background.png` is 809×1385 (`python3` struct
read of the IHDR chunk), aspect **0.58412**. The device log quoted in
`NOTES.md:368` reads `[HouseMap] picture 366.45x627.37 at 0,7.69 in element
366.45x642.74` — aspect **0.58410**, `pictureLeft = 0`, `pictureTop > 0`.
That is the width-limited branch, computing the image's own aspect ratio to
five significant figures. The pillarbox branch is exercised by neither
screenshot (both devices are tall phones) and is verified by derivation only.

One boundary: the code uses `r.width`/`r.height` from `contentRect` but never
`r.x`/`r.y`, while `houseBox`'s absolute `left`/`top` are offsets from the
parent's padding box. On the art path the background element has neither
padding nor border (`HouseMapView.cs:82-93`), so the two coincide. On the
no-art fallback a 2px border is set (lines 100-107) — a possible 2px offset
there, and that path draws no house to place cells against anyway.

### 3. `AccessFor` — the claim is false at the end of the game. Fail.

The test's name is `AccessFor_ExactlyOneRoomIsOpen_AtEveryPointInTheGame`
(`PlayerProgressTests.cs:238`). What it asserts is weaker
(`PlayerProgressTests.cs:246-248`):

```csharp
var finished = p.RoomsDone.Count == Curve.Length;
Assert.That(open, Is.EqualTo(finished ? 0 : 1),
            $"after {step} piles, {open} rooms were open");
```

It permits — in fact requires — **zero** open rooms once the house is done.
And zero is what the code produces: `CompletePile` (`PlayerProgress.cs:125-134`)
adds room 12 to `_roomsDone` and then `return`s without advancing the cursor, so
the final state is `CurrentRoom = 12` with `IsRoomDone(12) == true`, and
`AccessFor` (`PlayerProgress.cs:87`) takes the `Done` branch before it can reach
the `Open` one. No room is Open.

Established by running, not by reading. Mutating line 87 from
`if (IsRoomDone(room) || room < CurrentRoom)` to `if (room < CurrentRoom)` —
which would leave room 12 Open at the end — produced exactly one failure, and
its message is the proof of the unmutated behaviour:

```
Не пройден AccessFor_ExactlyOneRoomIsOpen_AtEveryPointInTheGame
     after 37 piles, 1 rooms were open
Assert.That(open, Is.EqualTo(finished ? 0 : 1))
  Expected: 0
  But was:  1
```

So the property is "exactly one room open at every point **except the last**".
Three places state it without that exception:

- `NOTES.md:231-232` — "one of which walks the whole 37-pile game asserting
  that **exactly one room is open at every point**";
- `HouseMapView.cs:357-358` — "Exactly one room is ever open, which
  `PlayerProgress.AccessFor` guarantees and its tests pin";
- `HouseMapView.cs:538-539` — "exactly one room is ever open and it is always
  the save's cursor".

**The test is nevertheless a real guard, not decoration.** A second mutation
opening two rooms (`room == CurrentRoom || room == CurrentRoom + 1`) failed 4
of 23 `PlayerProgressTests`; baseline is 23/23. Both mutations screamed.

What it costs the screen: at the terminal state `openRoom` stays `0`
(`HouseMapView.cs:138-144`), every cell gets `onTap == null` and
`PickingMode.Ignore` (lines 502-506), and the legend still reads "tap the lit
number to play it" (lines 157-159) with nothing lit. Nothing crashes; the
finished house just invites a tap it will not accept. This is a documentation
defect plus a small end-of-game gap, not a broken feature.

### 4. The `fired` guard — Pass, on all three questions.

- **Across cells:** `fired` is a local of `Cell(...)` (`HouseMapView.cs:484`),
  captured by the `Fire` local function. Each of the twelve `Cell` calls in the
  loop at line 139 creates its own. Nothing static, nothing on the instance.
- **Across rebuilds:** `OnEnable` rebuilds every cell, so a rebuild produces
  fresh closures. There is no path that re-enables the component anyway.
- **Twice within one cell:** only the Open room registers handlers at all
  (`onTap` is `null` for the other eleven, line 146). Its four registrations —
  `ClickEvent` and `PointerUpEvent` on both `plaque` and `wrapper`, lines
  494-500 — all route through the one `Fire`, which sets `fired = true` before
  calling `onTap()`. A tap that bubbles plaque→wrapper and produces both event
  kinds still fires once. `ShowOpening`'s veil is a second, independent guard:
  it covers the panel and is `PickingMode.Position` (line 605), so taps during
  the 120ms window reach it and not the map.

One behavioural note, not a defect: `PointerUpEvent` is not `ClickEvent`, so a
press that begins on a locked room and is released over the open room will
start the game. `NOTES.md:378-380` records that `up/plaque` is the handler that
actually fires on both platforms, so this is the live path.

### 5. The 120ms defer — partly settled, and the rest not established.

What the code does, which I can settle:
`StartPlaying` null-checks `root` at entry (`HouseMapView.cs:552-553`) and then
schedules `SwapInBoard(uid, root)` (line 587). `SwapInBoard` re-checks nothing:
it does not test `this.enabled`, does not test that the map still owns `root`,
and dereferences `uid` at line 651 (`uid.visualTreeAsset`) and `gameObject` at
line 663. If the component were destroyed inside the window those become
`MissingReferenceException`, and the `try`/`catch` at 646-674 turns that into a
`Debug.LogError`, a line in `tap.txt`, and an on-screen message — so a
destroyed component degrades rather than crashes. If the component were merely
*disabled*, nothing in `SwapInBoard` would stop it clearing the panel and
adding `DebugGameView` anyway.

What I cannot settle without Unity: **whether a `VisualElement.schedule` item
survives its element leaving the panel, or the panel being torn down.** That
single fact decides both whether the deferred swap runs at all after a
teardown, and whether `ShowOpening`'s repeating `.Every(28)` item
(`HouseMapView.cs:632-637`), which is never explicitly stopped, keeps ticking
on a detached `bar` after `root.Clear()`. `grep -rln "ExecuteLater" knowledge/
tasks/` returns nothing, so this project has no recorded primary source for it,
and the brief forbids reconstructing a call's semantics from memory. In
practice `GameBoot.cs:349-359` `return`s straight after adding the component,
so no other screen competes for `root` during the window — the hazard is
theoretical on today's code, but the guards that would make it safe are absent
rather than present.

### 6. The screenshots — mostly, with three claims that do not match.

Opened, on both platforms, at the sizes reported by `sips`:

- **Current numbering, confirmed on both.** `ios-numbers-in-order.png`
  (1206×2622) and `android-tap-before.png` (1080×2340) both show 1 lit
  bottom-left through 10 top-right and 11/12 centred under the roof, with the
  current legend text "tap the lit number to play it".
- **The veil, confirmed on both.** `android-opening-the-room.png` and
  `ios-opening-the-room.png` (1170×2532 — a different simulator device from the
  other iOS shots, which are 1206×2622) both show "Opening the room…", a
  part-filled bar, and the dimmed current layout beneath.
- **The tap reaching the board, confirmed on both.**
  `android-tap-opens-room-1.png` and `ios-tap-opens-room-1.png` both read
  "Room 1 of 12 · pile 1 of 1 / Items left: 36", as `NOTES.md:337-341` says.

Three claims that do not:

1. **`NOTES.md:221` cites `ios-numbers-in-order.png`. There is no such file**
   in the task directory.
2. **`NOTES.md:292` cites `android-tap-opens-room-1.png`. There is no such
   file** — the artefact is named `android-tap-opens-room-1.png`.
3. **`NOTES.md:187` and `NOTES.md:315-317` both give the crop as 807×1381.**
   `game/Assets/Resources/Art/map_background.png` is **809×1385** (IHDR read).
   The delivery original is 928×1664 as claimed. 807×1381 is what
   `ROOM-PLACEMENT.md`'s `x 6–93%, y 9–92%` box computes to, so the number was
   taken from the intended crop rather than re-derived from the file that
   shipped — the "a number nobody counted" shape from `AGENT-BRIEF.md`.

And one gap rather than an error: `ios-numbers-in-progress.png` and
`android-numbers-in-progress.png` show the **superseded** spatial layout (9 at
the apex, 12 beneath it, and the older legend "the lit number is the room to
play"). `NOTES.md:222-224` describes them accurately for the build they came
from — rooms 1–4 ticked, room 5 lit with a part-filled bar, on both platforms.
But they are the only screenshots with mixed done/open/locked states, so **the
current numbering has no partial-progress screenshot on either platform.**
Every current-build shot is of a fresh save.

### Test numbers, as run

```
dotnet test build/core-tests/core-tests.csproj -v q --nologo
  -> Пройден!   : не пройдено 0, пройдено 195, пропущено 0, всего 195, 298 ms
.venv/bin/python -m pytest tools/tests -q
  -> 161 passed in 5.88s
```

195, not the 189 recorded in the pass above — the six `AccessFor_*` tests are
the difference (`PlayerProgressTests.cs:204-272`), and the filtered fixture run
reports 23.

## How to reproduce

From a clean checkout at the repository root, nothing exported by hand:

```sh
dotnet test build/core-tests/core-tests.csproj -v q --nologo
# -> 195 passed
.venv/bin/python -m pytest tools/tests -q
# -> 161 passed

# item 6, claim 3 — the shipped crop is 809x1385, not 807x1381:
python3 -c "import struct;d=open('game/Assets/Resources/Art/map_background.png','rb').read(24);print(struct.unpack('>II',d[16:24]))"

# item 6, claims 1 and 2 — the two cited screenshots do not exist:
ls tasks/60-shell-build/03-house-map/ios-numbers-in-order.png \
   tasks/60-shell-build/03-house-map/android-tap-opens-room-1.png
```

Item 3, on a copy outside the repository (never on the tree):

```sh
SP=$(mktemp -d); mkdir -p "$SP/game/Assets"
cp -R build "$SP/build"
cp -R game/Assets/Core game/Assets/Tests "$SP/game/Assets/"
rm -rf "$SP/build/core-tests/bin" "$SP/build/core-tests/obj"
cd "$SP"
dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~PlayerProgressTests"     # baseline: 23/23

# mutation A — two rooms open mid-game. In PlayerProgress.AccessFor, replace
#   return room == CurrentRoom ? RoomAccess.Open : RoomAccess.Locked;
# with
#   return (room == CurrentRoom || room == CurrentRoom + 1) ? RoomAccess.Open : RoomAccess.Locked;
# -> 19/23; 4 failures. The guard bites.

# mutation B — the end-of-game room stays Open. Replace
#   if (IsRoomDone(room) || room < CurrentRoom) return RoomAccess.Done;
# with
#   if (room < CurrentRoom) return RoomAccess.Done;
# -> 22/23; AccessFor_ExactlyOneRoomIsOpen_AtEveryPointInTheGame fails with
#    "after 37 piles, 1 rooms were open / Expected: 0 But was: 1",
#    which is the proof that the unmutated code leaves ZERO rooms open at the end.
cd - && rm -rf "$SP"
```

## What was not checked

- **The task's own `VERIFY (HUMAN)` item** — still not performed and still not
  performable by an agent (`tasks/README.md`, the independence rule). Nothing
  in this pass touches it. It is the only acceptance criterion the task states,
  and the task's `OUTCOME` rests on it.
- **No Unity, no build, no simulator, no emulator.** Everything about the
  running screen here comes from PNGs and from the log lines quoted in
  `NOTES.md` — neither of which I produced. I could not re-derive the log
  trace, so `[HouseMap] picture 366.45x627.37 …` is trusted as quoted, not
  re-measured; if that line were wrong, item 2's empirical half falls and only
  the derivation stands.
- **`VisualElement.schedule` lifetime** — see item 5. Not established, and
  therefore neither is the fate of the deferred swap or of `ShowOpening`'s
  repeating item after a teardown. Settling it needs a PlayMode experiment or a
  primary source added to `knowledge/`.
- **The pillarbox branch of `FitToPicture`** (image relatively wider than the
  element) is verified by derivation only. Both available screenshots are tall
  phones and exercise the letterbox branch.
- **`PlayerProgress.Restore` does not validate `roomsDone` against the cursor.**
  `Restore(Curve, cursorRoom: 2, cursorPile: 0, roomsDone: new[]{5})` would
  produce a Done room ahead of the cursor with Locked rooms between. No test
  covers it and I did not establish whether `GameSave` can ever emit such a
  file; `CompletePile` cannot. Flagged, not claimed.
- **The `Place` fallback for a thirteenth room** (`HouseMapView.cs:266-269`)
  was read and the arithmetic checked by hand (room 13 → `cx = 8`, room 14 →
  `20`), never executed; the shipped plan has twelve rooms, which the board
  screenshots confirm ("Room 1 of 12").
- **Cosmetic, not chased:** the doc comment on `Placements` opens `<summary>`
  twice (`HouseMapView.cs:197-198`) and closes once. No compiler warning
  appeared in `dotnet build`.
- **`labels.txt` left untouched.** It reads `status:in_progress` /
  `verify:passed`. `status:in_progress` is truthful — the HUMAN acceptance is
  open. On `verify:`, I did not change it, and my recommendation is that it
  stay `passed` **only as this file scopes it**: derivation (first pass) plus
  layout maths, the tap guard, and the placement table (this pass). It should
  not be read as the task's `VERIFY (HUMAN)` item having been met, and the
  three `NOTES.md` claims in item 6 plus the three over-strong "exactly one
  room" statements in item 3 should be corrected by whoever owns the file —
  none of them is a code fault, and all six are the failure shape
  `AGENT-BRIEF.md` says to look for first.
