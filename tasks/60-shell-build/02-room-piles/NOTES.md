
# The half without art, 2026-08-27

`Core/RoomPlan` answers the three questions the view needs: how many piles this
room holds, whether this pile finishes it, and how clean the room is once it is
cleared. All three are arithmetic over the shipped levels, so they are rules and
they are tested — `Tests/Core/RoomPlanTests.cs`, checked against the real 37
levels rather than a fixture.

Built from the level files rather than a third hand-written copy of
`1,2,3,3,3,3,3,3,4,4,4,4`. That table already exists twice — `pacing.py` and
`View/LevelAssets.cs` — and a third would be one more to keep in step.

A gap in a room's pile indices is rejected at construction: a missing index
means a corner no level ever clears and a "pile 2 of 3" that is really the last.

## What changed in the view

- The title now reads `Room 5 of 12 · pile 2 of 3` — it used to say
  `pile 2` with no idea how many there were.
- Finishing a corner says how far through the room it is.
- Whether a room is finished comes from `RoomPlan` instead of comparing the
  room numbers of adjacent levels.

## PlayerProgress finally has a caller

It has existed since `20-rules-core` with tests and no caller. It now advances
on every win, so the cat's state follows **completed rooms** rather than levels
played — which is the whole point of separating pacing from difficulty (D2).
Tests walk all 37 levels and assert the cat changes exactly twice, after the
fourth and eighth room.

Resuming a save replays progress up to the resumed level: progress is not saved
(`08` excludes it from scope), and without the replay `PlayerProgress` would be
asked to complete a pile it is not standing on, which it refuses by design.

## What still needs art

Everything visible: the clutter sprites per corner, the dirty and clean room
backgrounds, and the swap between them. `ClearedFractionAfter` is the number
that will drive it. The task stays `todo` because its OUTCOME is about what the
player sees, and none of that can be drawn yet.
