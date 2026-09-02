# Built, 2026-08-28

The rule already existed (`Core/PlayerProgress.CatStateFor`, `CatState`); this
task is the View half — nothing in the game showed it before now.

`View/DebugGameView.cs`:

- A cat portrait, always on the board (top-right corner, `game__cat` in
  `DebugGame.uss`), built once in `OnEnable` (`BuildCatPortrait`) and repainted
  by `RenderCat()`.
- `RenderCat()` reads `_progress.CatState` (1/2/3) and calls
  `CoatBuilder.LoadBase` + `CoatBuilder.Build` only when the state actually
  changed from the last paint — the coat pass walks every pixel, and `Render()`
  runs on every tile tap, so rebuilding unconditionally would redo that work
  on moves that do not touch the cat at all.
- `Render()` calls `RenderCat()` on every normal draw. `Finish()` calls it a
  second time, right after `_progress.CompletePile(...)`, because `Take()`
  already drew this frame *before* judging the outcome — without the extra
  call the room behind a room-clean card would still show yesterday's pose
  until the player pressed Next and the next level rendered. With it, the
  transition is visible in the room the instant the 4th or 8th room closes,
  which is the task's own bar ("a card that says the kitten changed is not
  the same as seeing her change").

## What traits she has

No photo flow reaches the board yet — `GameBoot.cs`'s default branch (no
debug flag file present) still only ever adds `DebugGameView` with no
`Cat`/`CatTraits` handed to it. Checked at the moment of writing this: a
`Shell/CatSaveFile.cs` and a `50-photo/09` `MeetYourCatScreen` were mid-flight
in the working tree (another agent's concurrent work, uncommitted), reachable
only behind their own debug flag (`meet.txt`) — they do not yet feed the
ordinary play path this task builds on. So "whatever cat was built" is,
today, nobody's cat on the path a player actually reaches, and the portrait
uses the same fixed stand-in the skip path already established:
`CatTraits.Default` (`View/DebugGameView.cs`'s `CatStateTraits`), a plain
grey short-haired tabby.

Deliberately not wired to `CatSaveFile.Read()` even though the API now
exists: reading another task's in-flight, not-yet-integrated file would be a
compile-time bet on an API this task cannot verify (View is not covered by
`dotnet test`), for a save the main flow does not yet write. The natural next
step, once `50-photo/09`/`10` are wired into `GameBoot`'s default branch
rather than a debug flag: swap `CatStateTraits` for
`CatSave.Read(CatSaveFile.Read())?.Traits ?? CatTraits.Default` in
`RenderCat()` — a one-line change, left for whoever lands that wiring, per
this task's own SCOPE ("this task only switches state on top of whatever cat
was built").

## What a screenshot has to show

Play three rooms and close the 4th: the portrait in the corner changes shape
— thinner/duller silhouette to a slightly fuller, cleaner one — the instant
the win card for that room's last pile appears, without touching Next. Same
again at the 8th room's close, for the third pose. No change on any other
room close, and no change tied to a level number (piles-per-room differs by
room — 1,2,3×6,4×4 per D2 — so a level-number-based reader would misfire
where a room-count-based one will not).

## Left open

`CoatBuilder.LoadBase` falls back to the short-haired silhouette with a
logged warning when a length is missing — irrelevant here since
`CatTraits.Default` is always short-haired, but worth naming: this portrait
does not exercise that fallback path.

Raw test output, 2026-08-28:
`dotnet test build/core-tests/core-tests.csproj -v q --nologo` →
`Пройден!   : не пройдено     0, пройдено   189, пропущено     0, всего   189`
(189 passing, unchanged — no new Core rule was needed; `CatStateFor` was
already tested by `PlayerProgressTests.cs`).
`.venv/bin/python -m pytest tools/ -q` → `160 passed` (up from 159 — the two
new `win.*` Copy.cs keys used by the sibling task).
`build/check-core-purity.sh` → `Core is engine-free: OK`.

## The game did not run on iOS at all, and the cat was why — 2026-08-28

`ios-board-blank-before-fix.png` is the board on the iOS simulator before this
was found: a uniform dark grey screen. `ios-board-cat.png` is the same build
after. The cat is top right, tinted, with her green eyes.

**Every screen that built a coat was blank on iOS.** The board and
meet-your-cat drew nothing; the house map — the one screen that calls no
`CoatBuilder` — drew correctly in the same panel on the same run. The game was
unplayable on one of its two platforms and had been since the coat shader
landed, because nobody had run it there.

### Why it took so long to find, which is the part worth remembering

Nothing was wrong by any measurement available. No exception was thrown. The
save file was written normally. `boot-state.txt`, added while hunting this,
reported the tree fully laid out: panel 388×844, `game-root` 388×844 carrying
the cream background colour, `pile` 359×677 with 36 children, and the first
tile 52×52, `Visible`, opacity 1, `display: Flex`, with its texture loaded.
Every number said the screen was fine. The screen was blank.

I believed at the time that Unity's `Debug.Log` reached no console on either
platform, so I never looked at one and inferred everything from screenshots.
**That belief was wrong** — `xcrun simctl launch --console booted <bundle-id>`
prints every line, and `adb logcat -s Unity` does the same on Android. Checked
on 28.08 by running it. The blindness was self-inflicted, and two rounds of
guessing came out of a premise I never tested.

What settled it was a controlled experiment rather than another theory: a
`nocat.txt` flag that skips `CoatBuilder` entirely. With the flag, the whole
board came back. Without it, dark grey. One binary, one variable.

### The cause and the fix

`CoatBuilder.ReadPixels` read its pixels back by blitting the texture into a
temporary `RenderTexture` and binding it — the standard way to read a texture
imported without Read/Write. On the iOS simulator, binding that target during
`OnEnable` leaves the Metal render target somewhere the camera never recovers
from, and nothing draws for the rest of the run.

The three cat silhouettes are now imported readable and **uncompressed**, and
`ReadPixels` reads them straight from memory. Uncompressed matters: the first
attempt set only `isReadable`, `GetPixels32` threw on the compressed format,
and the `catch` quietly fell back to the very blit that caused the problem —
same blank screen, and silently. That is why `CoatBuilder.LastReadWasBlit` is
now reported in `boot-state.txt`: a path this consequential should not be
invisible.

Cost: 12 MB resident for the three silhouettes. The comment that path once
carried argued this was not worth "one pass at load". A screen that does not
draw costs more.

### What is now in place so this cannot repeat quietly

- `Shell/DeviceLog` writes every error and exception to `errors.txt` beside the
  save, including the ones Unity catches inside `Awake`/`OnEnable` and only
  logs. Attached on the first line of `Awake`.
- `GameBoot.SafeBuild` puts a screen that throws on the screen, in words,
  instead of leaving a black one.
- `GameBoot.BootState` writes positive evidence — which branch ran, the
  post-layout sizes, the coat's read path — to `boot-state.txt`.
- `CoatBuilder.TryBuild` returns null instead of throwing, and the caller paints
  the untinted silhouette. A cat that will not tint costs a tint, not a screen.

### What is still untested

Everything above was found on the **simulator**. Whether a real iPhone shares
the blit fault is unknown and probably not — simulator Metal is not device
Metal. The fix is correct on both regardless, and the diagnostics are what will
answer it in one run when a device is available.

## Boot state is written by every branch now

`boot-state.txt` was produced only by the board branch, which meant a flag-file
screen coming up empty — the exact case this file exists for — left nothing
behind. Every branch writes one now: `coat`, `housemap`, `meet` and `board`,
each with the post-layout sizes and the coat's read path.

The board also logs what it does: `[Board] took <id>, shelf=N, triples=N,
available=N` on every tile taken, `[Board] tap <id> refused` when a tap changes
nothing, and a line for each ending — win, lose, house complete. A tap that does
nothing is either a locked tile behaving correctly or a bug, and from outside the
app those look identical.

## The two transitions, actually watched — 2026-09-03

`VERIFY.md` (2026-08-28 pass) was explicit: the two QA checks in `task.txt`
had never been run, and neither shipped screenshot showed a state change —
both were state 1. This closes that gap. No code changed; the installed APK
(`versionName=09-02 21:23 d4c2926 vc262`) already matches `HEAD` for every
file this task touches (`git log d4c2926..HEAD` on `PlayerProgress.cs`,
`DebugGameView.cs`, `CoatBuilder.cs`, `GameSave.cs`, `SaveResume.cs`,
`SaveFile.cs`, `RoomPlan.cs` — empty), so the shipped build was used as-is.
Android only (`emulator-5554`); no iOS simulator was part of this task's
tooling, so the iOS side of the standing "check both platforms" rule is not
covered by this run.

**How the boundary was reached.** Same trick `06-win-screen` and
`70-analytics/02-nine-events` used: read the level JSON, compute a full
winning move order with `tools/solver.solve()`, replay all but the last move
through `tools/solver/rules.RulesState` (the tested mirror of `Board.cs`), and
write a `board.save` that is one tap from winning — this time picking the
*last pile of room 4* (`l09_room04_pile2.json`, level 9) and the *last pile of
room 8* (`l21_room08_pile2.json`, level 21), found from
`game/Assets/Resources/Levels/` (`l01_room01_pile0.json` ...
`l37_room12_pile3.json`; piles per room 1,2,3,3,3,3,3,3,4,4,4,4, so level 9
closes room 4 and level 21 closes room 8 — matches `RoomPlanTests.cs:119-135`
which walks all 37 and asserts exactly 2 transitions). Script:
`tools/solver`-based, kept only in the scratchpad, not committed (throwaway,
same as the earlier tasks' scripts).

One wrinkle found while doing this: `DebugGameView.Resume()`
(`View/DebugGameView.cs:199-221`) does **not** use the save's `cursor`/
`roomsdone` lines at all — those are written by `GameSave.Write` but the only
reader that uses them is `HouseMapView` (`View/HouseMapView.cs:229,256-257`),
for the map screen. `Resume()` instead rebuilds `PlayerProgress` by replaying
`CompletePile(_levels[i].PileIndex)` for every level before the resumed one
(comment at `DebugGameView.cs:214-218`: "Progress is not saved ... so it is
replayed"). So the crafted saves omitted `cursor`/`roomsdone` entirely — only
`level 9 room_04 2` / `level 21 room_08 2` matters — and the house map still
drew the right ticks (rooms 1–3 done, 4 open, then 1–7 done, 8 open) purely
from that level line, confirming the replay path is what actually drives
`PlayerProgress.CatState` in play.

**Room 4 boundary** (`android-room4-*.png`):
- `android-room4-before-board.png` / `-before-card.png`: room 4, pile 3 of 3,
  one tile left (a cloth). Portrait and full cat card both show **state 1** —
  hunched sitting pose, muted/dull coat (`CoatBuilder.Neglect[1] = 1.0`).
- Tapped the remaining cloth. The win card ("The room is clean", before/after
  room art) came up immediately, and **the portrait behind the card had
  already changed** — `android-room4-after-board.png` was taken with the win
  card still on screen, before touching "Next". Portrait is now standing,
  upright, visibly cleaner coat.
- Pressed Next (into room 5, pile 1), tapped the portrait:
  `android-room4-after-card.png` — full cat card, standing state-2 pose, and a
  reward bowl prop is now in the scene (that prop is `05-rewards` scope, not
  this task's — noted, not touched).

**Room 8 boundary** (`android-room8-*.png`): same routine, level 21.
- `android-room8-before-board.png` / `-before-card.png`: room 8, pile 3 of 3,
  one board left. Portrait and card both still **state 2** — same standing
  pose as after room 4, confirming state 2 holds steady across rooms 5–7 and
  only changes again at room 8.
- Tapped the last board. Win card up immediately; `android-room8-after-board.png`
  shows the portrait **already** in a third, distinct pose — lying down,
  glossier coat — again captured with the win card still on screen, before
  "Next".
- `android-room8-after-card.png`: full cat card, state 3, lying/relaxed, with
  both the bowl and a blanket prop now present.

**VERIFY QA 1 (transition fires the instant the 4th/8th room closes, not
before, not on a later level) — confirmed by direct observation.** Both
`*-after-board.png` shots were taken while the win card for that exact pile
was still on screen, before advancing. The portrait was still state 1 (resp.
2) on the pile immediately before the last one, and was already state 2
(resp. 3) the moment the last tile of the room's last pile landed — matching
the `RenderCat()` call inside `Finish()`'s win branch
(`View/DebugGameView.cs:1216`, right after `CompletePile`), not the one at the
top of `Render()` for the next level. Answers task point 5 directly: **the cat
changes at the moment of victory itself, before the player has done anything
else — not merely on the next render.**

**VERIFY QA 2 (pile_count changes don't shift the transition)** — not
re-verified by a device run (would need editing shipped level data, more than
a small change, out of scope for this pass). Already covered on the Core side:
`PlayerProgress.CatStateFor` (`Core/PlayerProgress.cs:150-156`) is a pure
function of `_roomsDone.Count`, with no reference to level numbers, and
`RoomPlanTests.cs:119-135` walks the real 37-level, 12-room shipped plan and
asserts exactly 2 state changes regardless of the 1..4 piles-per-room curve.
No discrepancy found between the promise ("ровно дважды, на 4 и 8, независимо
от числа уровней в комнате") and the implementation.

**No discrepancy found overall.** The three states are visibly, unmistakably
different (pose, coat brightness/texture from `Neglect = {_, 1.0, 0.30, 0}`,
and — incidentally — the reward props from `05-rewards` riding the same two
boundaries), the swap is exactly two, exactly at rooms 4 and 8, and it is
already on screen before the player takes any further action.

Device cleaned: `board.save` removed from
`/sdcard/Android/data/com.sootpaw.game/files/` after the run, no other file
added or left behind.

`dotnet test build/core-tests/core-tests.csproj` was not re-run in this pass
(no code changed); the 2026-08-28 VERIFY run already logged 195 passed / 0
failed on this exact code.
