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

## The room itself, at last — 2026-08-28

`ios-win-room-before-after.png` is this screen's first picture in the project's
life, and the first time it shows what the task always asked for: **the room
before and the room after**, from the drawn pair.

It had been showing a collage of the room's prop sprites — scattered for
"before", lined up for "after". That was honest when it was written: 40-art/07
had delivered nothing, and a collage of real props beat a placeholder image. It
stopped being honest on 28.08 when 24 room files arrived, and it survived
because nobody went back to the code that had worked around their absence.

The owner played the game and named it in one sentence: "мы рисовали комнаты
грязные и чистые, почему просто не показать комнату до и после?" The pair of
pictures *is* the pitch — `cat-shelter-mvp.md` calls it the game's eight-second
reel — and the screen built to show it was showing something else.

**What changed.** `ShowRoomTransformation` loads `Art/room_NN_dirty` and
`Art/room_NN_clean` and paints them into the two frames. The frames were 116×116
squares built for a scatter of props; a room is 1856×3328, so scale-to-fit would
have shrunk it to a letterboxed sliver. They become 104×186 portrait for a room
and drop their painted background and border — the picture is the panel now, and
a frame around it only competes.

The prop collage stays as the fallback for a room with no pair drawn, and the
log says which of the two ran: `[Board] before/after: room 01 art` or
`… has no art, using props`.

### How it was reached, which is worth writing down

There is no debug route to this screen and playing to it by hand is 36 taps of
matching. Instead the level file was read (`l01_room01_pile0.json`), a full
winning order computed against its `blocked_by` graph, and a save written that
replays all but the last take — leaving one tile on the board and two of its
kind on the shelf. One tap then wins the room. The trace confirms it:

```
[Board] took 36, shelf=0, triples=12, available=0
[Board] win: level 1, lastPileOfRoom=True
[Board] before/after: room 01 art
```

That save-crafting trick works for any level and is the cheap way to reach any
end-of-level screen: the lose card, the house-complete card, the reward drop.

## Both platforms — 2026-08-28

`android-win-room-before-after.png` beside the iOS one: identical, the drawn
room pair on both. Reached the same way — the crafted one-move-from-won save,
then `adb shell input tap` on the last tile.

```
[Board] took 36, shelf=0, triples=12, available=0
[Board] win: level 1, lastPileOfRoom=True
[Board] before/after: room 01 art
```

## The rooms were being squashed 10%, and nobody looked — 2026-08-28

An independent verifier measured the panes on both screenshots and found them at
aspect **0.5000** where the art is **0.5577**: about a ten per cent horizontal
squash. Confirmed by eye once pointed at — the round mirror in room 01 was an
oval.

Cause: the NPOT trap this project already met with the map background, left half
fixed. Room art is 1856×3328 — neither dimension a power of two — and with
`nPOTScale: 1` Unity rescales each axis independently, so the runtime texture
came out 1024×2048 and the aspect with it. The notes on `03-house-map` had said
outright that whoever wired the rooms would have to decide this properly. The
rooms got wired and the decision did not get made.

**Fixed at the source, not in code.** All 24 files are now **1024×2048** with the
room scaled to fit and centred on transparent padding — the content keeps its own
0.5577 exactly (measured: content occupies rows 106–1941, 1024×1836). Power of
two, so nothing is rescaled and compression still applies; the earlier attempt
that turned NPOT scaling off took the APK from 45 MB to 133 MB, and this avoids
that entirely while being smaller than the originals.

The delivered files are kept at `game/Assets/Art/delivery-originals/rooms/`.

**For `02-room-piles`:** a room drawn full-screen from a 1024-wide texture will be
softer than one drawn from 1856. If that shows, the answer is a larger
power-of-two canvas (2048×4096), not a return to non-power-of-two.

## Room 12's pair was the one nobody would ever see

`Finish()` returned on house-complete before showing the transformation, so the
last room — the one a player works hardest for — ended with words alone. Found by
reading, not by playing: nobody had ever got that far. It now shows its
before/after on the ending card.
