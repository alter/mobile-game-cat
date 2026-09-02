
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

# Полный прогон четырёхкучной комнаты, 2026-09-03

Пройдена комната 9 (уровни 22–25, room_09, четыре кучи) целиком — единственная
из уже сыгранных 02.09 не годилась для проверки правила углов: там кучи в
комнате 1 была ровно одна. Комната 12 — особый случай (финальная карточка,
разобран в 26-room12-reveal), поэтому для «обычного» правила углов взята
девятая.

Приём — тот же, что в `04-cat-states/NOTES.md`: сценарий на `tools/solver`
(`RulesState`/`_Search`), а для трёх «свежих» уровней вообще не понадобилось
решать — код `RenderRoom` смотрит только на `_level.PileIndex`, так что
пустая позиция (`taken` пусто, `shelf` все `_`, `triples 0`, нужная строка
`level N room_09 P`) уже показывает нужное число чистых углов. Файл `cap 9`
перед `shelf` — обязателен (текущий формат, иначе битый шелф на 08). Для
последней кучи (уровень 25) решён полный порядок ходов, реплеен все 59
ходов кроме последнего — тот же трюк «за один ход до победы».
Android, `emulator-5554`; iOS в этот прогон не входил (как и в
`04-cat-states`, инструментов для симулятора в этой сессии не было — пробел,
не заглушенный молчанием).

Пять снимков в этой папке, `android-room09-*`:
- `pile1of4-board-0corners.png` — уровень 22, кисть пипсов ●○○○, комната
  целиком грязная — ни один угол не подсвечен.
- `pile2of4-board-1corner.png` — уровень 23, ●●○○, левый верхний угол уже
  чистый и светлый (тёплое окно), остальные три — нет.
- `pile3of4-board-2corners.png` — уровень 24, ●●●○, весь верхний ряд (оба
  верхних угла) чистый, низ ещё серый.
- `pile4of4-board-3corners.png` — уровень 25, ●●●●, три угла чистые, один
  (правый нижний, где закрытое чехлом кресло) — всё ещё грязный.
- `pile4of4-wincard-transformation.png` — тот же уровень 25, снят СРАЗУ после
  снимка выше: доигран последний тап (свеча, id 58), карточка «The room is
  clean» с парой Before/After — After показывает комнату целиком чистой,
  включая тот самый правый нижний угол с креслом, которое на снимке доски за
  секунду до этого было ещё серым.
- `room10-pile1of4-board-after-next.png` — по «Next» игра ушла в комнату 10
  (уровень 26), фон снова целиком грязный — подтверждает, что цикл
  повторяется.

**Правило углов подтверждено визуально и оно растущее, угол за углом**:
0 → 1 → 2 → 3 чистых угла по кучам 1–4, и на последней куче полностью чистая
комната показывается — но не на доске, а на отдельной карточке
преображения (см. вопрос 3 ниже).

## Вопрос 3 — сколько углов реально рисует RenderRoom во время игры

Число: **максимум 3 из 4**, никогда 4.

`View/DebugGameView.cs:531` — `int cleaned = Mathf.Clamp(_level.PileIndex, 0, 4);`
`PileIndex` — нулевой отсчёт (0..piles-1), так что на последней куче
четырёхкучной комнаты `PileIndex == 3`, и `cleaned == 3`. Клэмп до 4 в коде
не бьётся ни разу за всю игру: пока уровень не завершён, `PileIndex` комнаты
с P кучами никогда не превышает P-1, то есть `cleaned` никогда не доходит до
P на самой доске. Снимок `pile4of4-board-3corners.png` — прямое
подтверждение: три угла чистые, четвёртый (кресло под чехлом) — нет, при
пипсах ●●●● (это последняя куча).

Четвёртый угол и полностью чистая комната появляются только в отдельном
элементе — карточке преображения (`ShowRoomTransformation`, строки 1693–1798),
которая рисует `room_NN_dirty`/`room_NN_clean` целиком, а не через окна-
четверти `_roomClean[0..3]`. Комнатный слой `_room` под карточкой в этот
момент так и остаётся с тремя чистыми углами — в `wincard-transformation.png`
это видно по краям карточки, где выглядывает фон доски.

**Увидит ли игрок четвёртый угол, если выйдет на карту, не досмотрев
карточку — нет, такого пути нет.** Кнопка «на карту» (`to-map`, вставляется
`HouseMapView.AddReturnToMap` перед `overlay` в `game-root`) при поднятой
карточке лежит ПОД затемняющим слоем оверлея и не ловит тапы — это прямо
описано в комментарии у `ShowTheWayOff` (`DebugGameView.cs:1394-1397`,
«dimmed by it and cannot be tapped through it, while a card is up»). В
`Update()` (строки 1813–1820) нет обработки аппаратной кнопки «назад». Уйти с
экрана можно только кнопкой «Next» на самой карточке — она гарантированно
показывает готовую пару Before/After. Единственный обходной путь — убить
процесс, но `Finish()` (`DebugGameView.cs:1199-1200`) пишет сохранение на
СЛЕДУЮЩИЙ уровень ДО показа карточки, так что убийство процесса не «покажет
позже» непросмотренный четвёртый угол — оно перепрыгивает игрока сразу в
следующую комнату, минуя и доску с тремя углами, и карточку с четырьмя.

**Расхождение с обещанием, если оно есть.** Формально OUTCOME («чистота
растёт угол за углом и на последней куче фон меняется на чистый») выполнено
— угол за углом рост виден (0→1→2→3), и на последней куче показывается
полностью чистый фон. Но дословно «фон меняется на чистый» происходит не на
игровом фоне комнаты (`_room`/`_roomClean`), а на отдельной панели карточки:
сама комната позади карточки со своими окнами-четвертями чистый фон никогда
не получает — после «Next» её уже не существует (следующий уровень строит
новую комнату). Технически это не баг (26-room12-reveal и текущий прогон это
подтверждают на двух комнатах), но READMENOTES стоит сказать явно: «фон
меняется на чистый» верно только про карточку, не про сам слой `_room`.
Правка (если её захотят) — не тривиальная замена двух строк: пришлось бы
либо красить четвёртое окно `_roomClean[3]` сразу по клику победного тапа до
показа карточки (рискует гонкой с `Render()`, который перерисовывается на
каждый тап), либо снимать клэмп и полагаться на `IsLastPileOfRoom`. Не тронуто
по инструкции задачи (не чинить, если правка больше двух строк).

`dotnet test build/core-tests/core-tests.csproj` — 279 пройдено, 0 неудач
(перезапущен в этом прогоне, код не менялся).

Устройство очищено: `board.save`, который использовался для всех прыжков по
уровням 22–25 (`level N room_09 P`, пустая позиция, плюс один файл «за один
ход до победы» для уровня 25), удалён с `emulator-5554` после съёмки;
остальные файлы устройства не трогались.
