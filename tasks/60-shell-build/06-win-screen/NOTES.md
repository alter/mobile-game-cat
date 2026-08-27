# Built, 2026-08-28

`View/DebugGameView.cs`: a before/after block inserted into the existing win
card (`BuildBeforeAfter`, `ShowRoomTransformation`, `HideRoomTransformation`),
shown only on `GameOutcome.Win` for a room's last pile (`IsLastPileOfRoom`),
never for a corner clear — that keeps its own feedback per SCOPE. `ShowCard`
hides the block by default for every card; `Finish()` turns it back on only
in the `lastPileOfRoom` branch, right after the ordinary win card is shown.

## What stands in for room art — read this before trusting a screenshot

**No room art exists.** `40-art/07-rooms` is `status:todo`; there is no
drawn dirty/clean frame pair for any of the twelve rooms. What this task
built instead, and why it is not the same thing:

`ShowRoomTransformation` gathers the distinct prop kinds the room that just
closed actually held (every pile of that room, via `_levels.Where(l =>
l.RoomId == closedLevel.RoomId)`, not just the last pile — a four-pile room
shows what the whole room was made of), capped at 9 and seeded by room
number so the same room reads the same way on a replay. Each kind's real
sprite (one of the shipped 30 props, the same texture already drawn on the
board) is placed twice:

- **Before pane** — the sprites scattered, overlapping, at random angles, on
  a muddier background (`#B0A79B`, the same tone `.game__tile--hidden`
  already uses for "not seen yet").
- **After pane** — the same sprites, upright, in a tidy wrapped row, on a
  lighter bordered background (`#F4EAD8`, the shelf's own palette).

This is real data (which props this room held) drawn with real shipped art
(the props), not a stock placeholder image. It is **not** the drawn
dirty/clean room pair `art-brief.md` section 8 and the task's own SCOPE ask
for — there is no room background, no cat in the shot, no "eight-second clip"
material. It is the honest thing buildable today from what exists: clutter
becoming order, using the actual objects the player just cleared. Captions
"Before"/"After" (`Copy.cs` keys `win.before`/`win.after`) are added on top
so the HUMAN VERIFY ("names which side is before/after ... immediately") does
not depend on the visual metaphor landing on its own.

When `40-art/07-rooms` delivers the real pairs, this block's population logic
(`ShowRoomTransformation`) is what to replace — the show/hide wiring in
`Finish()`/`ShowCard()` and the "once per room close" gating stay as they are.

## What a screenshot has to show

Clear the last pile of any room (not a mid-room pile — those still show the
plain "Corner cleared!" card with no before/after). The win card shows two
panes side by side between the title and the body text: a cluttered,
overlapping scatter of that room's actual prop icons on the left labelled
"Before", the same icons upright in a neat row on the right labelled "After".
Clearing a non-final pile shows neither pane.

Raw test output, 2026-08-28:
`dotnet test build/core-tests/core-tests.csproj -v q --nologo` →
`Пройден!   : не пройдено     0, пройдено   189, пропущено     0, всего   189`
(189 passing, unchanged — this task is pure View; no Core rule was added).
`.venv/bin/python -m pytest tools/ -q` → `160 passed` (up from 159 — two new
`win.before`/`win.after` Copy.cs keys, both declared and used).
`build/check-core-purity.sh` → `Core is engine-free: OK`.
