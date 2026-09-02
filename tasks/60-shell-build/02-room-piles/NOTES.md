
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

## Ready to build, and one assumption in the task is wrong — 2026-08-28

The board today is a cream page with prop tiles on it. **The room is not there at
all**, which makes this the largest piece of the product still missing: the game's
whole promise is a room getting better, and the room is absent from the screen
where the work happens.

Everything needed now exists. `Resources/Art/room_NN_dirty.png` and `_clean.png`,
24 files, 1024×2048, undistorted since today's resize. `RoomPlan` already answers
the questions this needs: `PilesIn`, `IsLastPileOfRoom` and — the useful one —
`ClearedFractionAfter(level)`.

**The task's SCOPE assumes something that is not true.** It says which items sit
in which corner "is decided by 30-levels-solver's level data, this task only
renders it". Checked: a level file carries `number`, `room_id`, `pile_index` and
a pile of items with `id`, `kind` and `blocked_by` — **no corner, no position,
nothing spatial**. Nothing downstream can render a corner it was never told
about.

Two ways out, and the choice should be deliberate:

1. **Add corner data to the levels.** Faithful to the task, and it means
   regenerating 37 level files and teaching the generator a spatial notion it
   does not have. Everything about difficulty would have to be re-measured.
2. **Derive the corners from the pile index.** A room has 1–4 piles; a room has 4
   quadrants. Pile *n* cleans quadrant *n*, and the last pile swaps the whole
   background to clean. No new data, no regeneration, and the OUTCOME — "visibly
   increasing cleanliness, corner by corner, swaps to clean on the last pile" —
   is met exactly as written.

**Going with 2**, and recording it here rather than quietly: it delivers what the
OUTCOME asks with data that exists, and if per-item corners are ever added, the
same rendering can read them instead of the pile index.

Waiting on `DebugGameView.cs`, which another worker is in.

# Открытый дефект — 2026-09-01

Последний угол комнаты 12 не открывается (наблюдение владельца, задача пока
нигде не велась — теперь ведётся здесь). Пока он не разобран, OUTCOME «чистота
растёт угол за углом и на последней куче фон меняется на чистый» наблюдается
на комнатах 1–11 и не наблюдается на 12-й.

Дополнение 2026-09-02: механизм найден и он не в данных уровней — финальная
карточка игры перекрывает показ преображения последней комнаты (разбор в
30-levels-solver/13, исправление ведётся в 60-shell-build/26-room12-reveal).
