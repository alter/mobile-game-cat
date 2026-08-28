# save-forge — доводит игру до нужного экрана за один ход

Экраны концовок нельзя было снять: до каждого надо доиграть, а это десятки
совпадений подряд, и автоматически их не наберёшь — «закопанные» плитки нарочно
рисуются одинаково, по картинке их не различить.

Решение: **не играть, а собрать сохранение за один ход до нужного исхода.**
Формат сохранения (`Core/GameSave`) хранит `taken` — порядок взятых плиток, — и
`SaveResume` его переигрывает. Значит достаточно посчитать любой правильный
порядок по файлу уровня и записать всё, кроме последнего хода.

Три готовых сохранения здесь, все проверены на симуляторе iOS 28.08.2026:

| файл | что даёт | последний ход | итог |
|---|---|---|---|
| `almost.save` | уровень 1, на доске 1 плитка, 2 на полке | взять книгу | **Room clean!** с парой «до/после» |
| `jam.save` | уровень 1, полка 8 из 9, троек нет | взять любой вид, которого на полке меньше двух | **Shelf jammed** |
| `house.save` | уровень 37, комната 12 из 12, куча 4 из 4 | взять варежку | **Every room is clean** — концовка игры |

## Как получить такое сохранение для любого уровня

Читаем файл уровня, считаем порядок с оглядкой на `blocked_by` (плитку нельзя
взять, пока не убраны закрывающие) и на `locked_after_triples`, отбрасываем
последний ход, переигрываем остаток, чтобы получить полку и число троек.

```python
import json
from collections import Counter
d = json.load(open('game/Assets/Resources/Levels/l01_room01_pile0.json'))
pile = {t['id']: t for t in d['pile']}
blocked = {t['id']: set(t['blocked_by']) for t in d['pile']}
CAP, taken, shelf, triples = 9, [], [], 0
rem = set(pile)
while rem:
    c = Counter(shelf)
    # берём то, что достраивает тройку; иначе новый вид, если на полке есть место
    cand = sorted((i for i in rem if blocked[i] <= set(taken)),
                  key=lambda i: (-c[pile[i]['kind']], i))
    p = cand[0]
    taken.append(p); rem.discard(p); shelf.append(pile[p]['kind'])
    cc = Counter(shelf)
    for k, n in list(cc.items()):
        if n >= 3:
            for _ in range(3): shelf.remove(k)
            triples += 1
```

Для затора порядок обратный: **избегать** троек, брать только те виды, которых
на полке меньше двух, пока не наберётся восемь.

Файл сохранения:

```
catshelter-save-v1
level <N> <room_id> <pile_index>
shelf <вид|_> ... cap9
triples <N>
taken <id id id ...>
cursor <комната> <куча>
roomsdone <номера пройденных комнат>
```

## Куда класть

```bash
# iOS
D=$(xcrun simctl get_app_container booted com.DefaultCompany.game data)
cp tools/save-forge/jam.save "$D/Documents/board.save"

# Android — путь внешний, run-as на релизной сборке не работает
ADB=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$ADB" push tools/save-forge/jam.save \
  /sdcard/Android/data/com.DefaultCompany.game/files/board.save
```

Затем перезапустить приложение и нажать последнюю плитку: на iOS —
`idb ui tap X Y` в точках (пиксели снимка делить на 3, см. `AGENT-BRIEF.md`),
на Android — `adb shell input tap X Y` прямо в пикселях снимка.

Журнал подтверждает исход, гадать по картинке не нужно:

```
[Board] took 33, shelf=9, triples=0, available=22
[Board] lose
```

## Чего это не заменяет

Сохранение ставит игру в **правдоподобное** положение, а не проходит её. Оно не
проверяет, что уровень вообще проходим живым человеком — этим занимается
решатель в `tools/solver`. И оно не заменяет живого игрока: расчётный порядок
не похож на то, как играют люди.
