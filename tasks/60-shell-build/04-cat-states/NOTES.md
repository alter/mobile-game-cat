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
