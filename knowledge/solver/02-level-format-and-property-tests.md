# Формат уровня и property-based тестирование

Дата сбора материала: 2026-08-24.

## Кратко

- Готовой, широко используемой открытой JSON-схемы именно под механику «слои + перекрытие + полка на N мест» найти не удалось; ближайшие открытые примеры — общие форматы тайловых уровней с полями `layer`/`tiles` (например, [вики game-map-editor](https://github.com/ppelikan/game-map-editor/wiki/Level-JSON-file-structure)) и общее описание слоёв/перекрытий из разбора клона 羊了个羊 у [阮一峰](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html) — оба ниже разобраны и на их основе предложена рабочая схема с явным полем `blocked_by`.
- Библиотека для property-based тестирования в Python — [Hypothesis](https://github.com/HypothesisWorks/hypothesis), актуальная версия на момент сбора — **6.165.10** (проверено на странице [PyPI](https://pypi.org/project/hypothesis/)), поддерживает Python 3.10–3.14.
- Базовый приём — декоратор `@given` со стратегиями (`st.integers()`, `st.lists()` и т.д.); для составных структур с зависимостями между полями — декоратор `@st.composite`, как показано в официальном разделе документации [Custom strategies](https://hypothesis.readthedocs.io/en/latest/tutorial/custom-strategies.html).
- Общая академическая рекомендация по процедурной генерации головоломок с обязательной решаемостью — конструировать данные так, чтобы решаемость была гарантирована самим способом генерации (сведено к DAG/обратному построению), а не проверять и отбрасывать невалидные образцы постфактум; это прямо соответствует официальной рекомендации Hypothesis «если вы обнаруживаете, что фильтруете большинство случаев — почти всегда лучше генерировать нужные данные напрямую» ([Custom strategies](https://hypothesis.readthedocs.io/en/latest/tutorial/custom-strategies.html)).
- Для игрового движка на C# и решателя на Python синхронизация правил — открытая инженерная проблема без единственно верного решения; из реально задокументированных подходов: полный независимый порт правил (дорого, риск расхождения), общий набор проверочных случаев / контрактных тестов (дешевле, но не покрывает всё пространство состояний), и «golden master» — фиксация эталонных прогонов одной реализации и сверка с ними другой (техника хорошо задокументирована как приём характеризационного тестирования, например у [Coding is Like Cooking / Ro-che, «Introduction to golden testing»](https://ro-che.info/articles/2017-12-04-golden-tests) и в статье о «слепом golden master» на [DEV Community](https://dev.to/rnowif/the-blind-golden-master-67h)).
- Golden-master подход снимает бремя доказывать корректность каждой реализации по отдельности, но сам по себе не доказывает, что правила реализованы верно в принципе — он только фиксирует, что два прогона совпали (явно отмечено в материалах про golden master, см. выше); отсюда практическая рекомендация комбинировать его с ручным набором «известных верных» контрактных примеров.
- Смежная и хорошо документированная техника из мультиплеерных игр — детерминированный lockstep с посценарийной сверкой контрольных сумм состояния между независимыми реализациями симуляции; она не про C#/Python порт напрямую, но даёт готовый метод обнаружения момента расхождения (сверка чек-суммы состояния на каждом шаге, а не только в конце партии).

## 1. Формат описания уровня в JSON

### 1.1 Что реально нашлось в открытых проектах

Прямого открытого JSON-формата для игр вида «Sheep a Sheep» / «Triple Match 3D» с типичными для такой механики полями (`layer`, `blocked_by`/`coverIds` и т.п.) в виде готового файла схемы найти не удалось — целевой поиск по этим именам полей на GitHub не дал совпадений на момент сбора. Нашлись два косвенных источника:

1. Общий (не специфичный для триплет-матч игр) формат уровня с явным делением на слои — вики проекта [ppelikan/game-map-editor, «Level JSON file structure»](https://github.com/ppelikan/game-map-editor/wiki/Level-JSON-file-structure): уровень описывается полями вида `level-name`, размеры тайла (`tile-sizeX`/`tile-sizeY`), и один или несколько `layers`, каждый со своими `sizeX`/`sizeY` и вложенным двумерным массивом `tiles` (идентификаторы тайлов), плюс отдельный список `events` с именованными триггерами и координатами `level-positions`. Отрицательные значения в массиве `tiles` используются для обозначения специальных состояний/блокираторов. Это формат для тайловой карты общего назначения (не именно матч-3), но именно оттуда естественно берётся идея представлять глубину слоёв как отдельную ось, а не как атрибут тайла.
2. Текстовое описание (не JSON-схема, а прозаический пересказ реализации) устройства уровня 羊了个羊 у [阮一峰](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html): поле делится на несколько наложенных слоёв со случайными позициями и типами карт на каждом слое; кликабельны только карты слоя, не имеющие перекрытий сверху. Это подтверждает саму модель «слой + перекрытие», уже формализованную в разделе 1.1 файла `01-tile-match-solver.md` через `(i, j, k)`-координаты по работе [Hoogeboom, Kosters, van Rijn, Vis](https://arxiv.org/pdf/1604.05487), но без конкретного JSON-представления.

Поскольку готовой отраслевой схемы нет, ниже — самостоятельно составленная схема, обоснованная требованиями решателя (раздел 1 файла `01-tile-match-solver.md`: доступность через `blocked_by`, полка максимум на N мест, кратность 3 для каждого вида).

### 1.2 Предлагаемая схема

Ключевое решение: хранить перекрытие явным списком `blocked_by` на каждом предмете (список id предметов, которые нужно снять раньше), а не выводить его из геометрии на лету — это делает уровень самодостаточным и не привязывает решатель к конкретной системе координат (сетка, произвольные полигоны, 3D-сцена — неважно, откуда взялось перекрытие, решателю нужен только итоговый граф).

```json
{
  "level_id": "lvl-0007",
  "shelf_capacity": 9,
  "match_size": 3,
  "kinds": 12,
  "move_limit": null,
  "items": [
    {
      "id": 0,
      "kind": 4,
      "layer": 0,
      "position": {"x": 120.0, "y": 80.0},
      "blocked_by": []
    },
    {
      "id": 1,
      "kind": 4,
      "layer": 1,
      "position": {"x": 118.0, "y": 82.0},
      "blocked_by": [0]
    },
    {
      "id": 2,
      "kind": 7,
      "layer": 0,
      "position": {"x": 300.0, "y": 40.0},
      "blocked_by": []
    }
  ]
}
```

Пояснение полей:

- `level_id` — идентификатор уровня, для трассировки в логах/тестах.
- `shelf_capacity` — вместимость полки (в задании — 9); вынесено в данные уровня, а не захардкожено в решатель, чтобы одним и тем же кодом проверять варианты сложности.
- `match_size` — сколько одинаковых предметов исчезает за раз (в задании — 3); тоже параметр, не константа, ради переиспользования схемы и решателя для смежных механик (например, тестового режима «match 2» для отладки).
- `kinds` — число видов предметов в уровне; используется валидатором кратности (раздел 7 файла `01-tile-match-solver.md`) и не обязано совпадать с `len(items)`, потому что позволяет проверить полноту набора.
- `move_limit` — необязательный лимит ходов; `null`, если в конкретном варианте правил (как в оригинальной «Sheep a Sheep») лимита ходов нет, и есть только лимит полки.
- `items[].id` — уникальный идентификатор предмета, используется как узел графа зависимостей.
- `items[].kind` — вид предмета (что с чем матчится); значения `kind` не обязаны быть подряд идущими, схема допускает произвольную нумерацию.
- `items[].layer` — глубина слоя, используется только для генерации/отладки/статистики сложности (раздел 8 файла `01-tile-match-solver.md`); решателю для проверки доступности эта величина не нужна, только `blocked_by`.
- `items[].position` — координаты на экране/сцене, нужны только для рендеринга и генератора, решателем не используются.
- `items[].blocked_by` — список `id` предметов, которые перекрывают данный предмет и должны быть убраны раньше; пустой список — предмет доступен изначально.

```python
from dataclasses import dataclass


@dataclass(frozen=True)
class LevelItem:
    item_id: int
    kind: int
    layer: int
    blocked_by: frozenset[int]


@dataclass(frozen=True)
class Level:
    level_id: str
    shelf_capacity: int
    match_size: int
    kinds: int
    move_limit: int | None
    items: tuple[LevelItem, ...]


def load_level(raw: dict) -> Level:
    items = tuple(
        LevelItem(
            item_id=it["id"],
            kind=it["kind"],
            layer=it["layer"],
            blocked_by=frozenset(it["blocked_by"]),
        )
        for it in raw["items"]
    )
    return Level(
        level_id=raw["level_id"],
        shelf_capacity=raw["shelf_capacity"],
        match_size=raw["match_size"],
        kinds=raw["kinds"],
        move_limit=raw.get("move_limit"),
        items=items,
    )
```
## 2. Property-based тестирование порождения уровней

### 2.1 Библиотека Hypothesis: версия и базовые приёмы

[Hypothesis](https://github.com/HypothesisWorks/hypothesis) — основная библиотека property-based тестирования для Python. На странице [PyPI](https://pypi.org/project/hypothesis/) на момент сбора материала (2026-08-24) указана версия **6.165.10**, классификаторы пакета перечисляют поддержку Python 3.10, 3.11, 3.12, 3.13, 3.14 (CPython и PyPy). Официальная документация подтверждает тот же номер версии в заголовке страницы [Quickstart](https://hypothesis.readthedocs.io/en/latest/quickstart.html) («Hypothesis 6.165.10 documentation»).

Базовый пример из официального Quickstart — тест, который должен выполняться для любого значения из описанного пространства входов:

```python
from hypothesis import given, strategies as st


@given(st.integers())
def test_integers(n):
    print(f"called with {n}")
    assert isinstance(n, int)


test_integers()
```

Декоратор `@given` принимает одну или несколько стратегий (`st.integers()`, `st.text()`, `st.lists(...)` и т.д.); по умолчанию Hypothesis генерирует и прогоняет 100 случайных примеров, а при обнаружении падающего примера автоматически «сжимает» (shrink) его до минимального воспроизводящего ошибку случая — это задокументированное поведение библиотеки (см. [Quickstart](https://hypothesis.readthedocs.io/en/latest/quickstart.html) и репозиторий [HypothesisWorks/hypothesis](https://github.com/hypothesisworks/hypothesis)).

### 2.2 Составные стратегии для уровня с зависимостями между полями

Для генерации структуры вроде нашего уровня (список предметов, где `blocked_by` должен ссылаться только на существующие `id`, количество каждого `kind` кратно 3, вместимость полки в разумных пределах) простых стратегий `st.lists`/`st.integers` недостаточно — нужны стратегии с зависимостями между сгенерированными значениями. Официальный раздел документации [Custom strategies](https://hypothesis.readthedocs.io/en/latest/tutorial/custom-strategies.html) для этого даёт декоратор `@st.composite`, показанный на примере генерации упорядоченной пары:

```python
from hypothesis import given, strategies as st


@st.composite
def ordered_pairs(draw):
    n1 = draw(st.integers())
    n2 = draw(st.integers(min_value=n1))
    return (n1, n2)


@given(ordered_pairs())
def test_pairs_are_ordered(pair):
    n1, n2 = pair
    assert n1 <= n2
```

Официальная документация там же отмечает, что этот конкретный пример можно было бы записать короче через `st.tuples(st.integers(), st.integers()).map(sorted)`, но именованная функция с `@composite` даёт больше контроля и читаемость там, где зависимостей несколько — именно наш случай (нужно провязать `id`, `kind`, `blocked_by` и общее число предметов каждого вида одновременно).

Ключевая рекомендация из той же документации, прямо применимая к порождению уровней: если для получения корректных данных приходится фильтровать (`.filter(...)`) большинство сгенерированных Hypothesis значений — почти всегда лучше генерировать нужные данные сразу правильными через `@st.composite`, а не генерировать вслепую и отбрасывать невалидные образцы. Это прямое обоснование того же принципа, что и «генерация уровня обратным ходом» из раздела 6 файла `01-tile-match-solver.md`: конструировать заведомо корректные (там — заведомо решаемые) объекты, а не генерировать-и-фильтровать.

### 2.3 Стратегия генерации уровня, гарантированно проходящего инвариант кратности 3

```python
from hypothesis import given, strategies as st


@st.composite
def level_with_valid_kind_counts(draw, min_kinds=2, max_kinds=15, max_multiplier=6):
    """Generate a level where every kind's total count is a multiple of match_size (3),
    by construction rather than by filtering."""
    match_size = 3
    num_kinds = draw(st.integers(min_value=min_kinds, max_value=max_kinds))

    items = []
    next_id = 0
    for kind in range(num_kinds):
        multiplier = draw(st.integers(min_value=1, max_value=max_multiplier))
        copies = multiplier * match_size          # always a multiple of match_size, by construction
        for _ in range(copies):
            items.append({"id": next_id, "kind": kind, "layer": 0, "blocked_by": []})
            next_id += 1

    return {
        "level_id": "generated",
        "shelf_capacity": 9,
        "match_size": match_size,
        "kinds": num_kinds,
        "move_limit": None,
        "items": items,
    }


@given(level_with_valid_kind_counts())
def test_kind_counts_are_always_multiples_of_three(raw_level):
    from collections import Counter
    counts = Counter(it["kind"] for it in raw_level["items"])
    assert all(n % 3 == 0 for n in counts.values())
```

Здесь свойство `n % 3 == 0` тривиально истинно по построению — это намеренно: сама стратегия `level_with_valid_kind_counts` служит регрессионным тестом на то, что генератор уровней **в принципе не может** выпустить некорректные количества, потому что математически невозможно нарушить условие внутри цикла `for _ in range(copies)`. Полезность такого теста — не в поиске бага в этой конкретной стратегии, а в фиксации контракта: если позже кто-то отредактирует функцию генерации боевого (не тестового) уровня и случайно сломает кратность, аналогичный тест, применённый к боевому генератору, а не к тестовой заглушке, поймает регресс.

### 2.4 Свойство «любой порождённый уровень проходим»

Для этого свойства стратегия должна порождать уровень **через обратное построение** (раздел 6 файла `01-tile-match-solver.md`), а сам тест — проверять, что решатель (DFS с разделов 3–4 того же файла) действительно находит решение. Это одновременно тест на генератор (не сломалась ли гарантия обратного построения) и на решатель (не потерял ли он какое-то валидное решение из-за ошибки в отсечениях):

```python
import random

from hypothesis import given, settings, strategies as st


@st.composite
def reverse_built_level(draw, min_kinds=2, max_kinds=10, max_multiplier=5, seed_max=2**31 - 1):
    num_kinds = draw(st.integers(min_value=min_kinds, max_value=max_kinds))
    multipliers = draw(
        st.lists(st.integers(min_value=1, max_value=max_multiplier), min_size=num_kinds, max_size=num_kinds)
    )
    seed = draw(st.integers(min_value=0, max_value=seed_max))
    rng = random.Random(seed)

    # generate_solvable_pile is the function defined in 01-tile-match-solver.md, section 6
    pile = generate_solvable_pile_multi(num_kinds, multipliers, rng)
    return pile


@settings(max_examples=200, deadline=None)   # solving can be slow; disable the per-example time limit
@given(reverse_built_level())
def test_reverse_built_levels_are_always_solvable(pile):
    state = State(pile=pile, shelf=Shelf.empty())
    solution = solve_dfs(state, seen=set())
    assert solution is not None, "a reverse-built level must always have a solution"
```

Параметр `settings(deadline=None)` — не произвольная деталь, а задокументированная необходимость: Hypothesis по умолчанию ограничивает время выполнения одного примера и считает превышение ошибкой, что при вызове потенциально небыстрого решателя (DFS может быть экспоненциальным в худшем случае даже с отсечениями) даёт ложные падения теста, не связанные с логической корректностью кода; отключение или увеличение `deadline` — стандартная рекомендация для тестов, вызывающих небыстрый код, применяемая на практике в блогах про Hypothesis, например в разборе [«How to Build Property-Based Testing with Hypothesis»](https://oneuptime.com/blog/post/2026-01-30-how-to-build-property-based-testing-with-hypothesis/view).

### 2.5 Свойство «запас ходов в заданных границах»

```python
from hypothesis import given, settings, strategies as st


@settings(max_examples=100, deadline=None)
@given(
    reverse_built_level(),
    st.randoms(),
)
def test_greedy_player_move_count_within_bounds(pile, rng):
    """After generation, a plausible-human greedy player (section 5 of
    01-tile-match-solver.md) must be able to finish within a designer-set move budget,
    and the level must not be trivially short (a lower bound guards against degenerate levels)."""
    state = State(pile=pile, shelf=Shelf.empty())
    moves_made = 0
    max_moves_allowed = 3 * len(pile.items)   # generous upper bound: at most one "wasted" shelf slot per item

    while not state.is_win():
        move = greedy_human_move(state, rng)
        if move is None:
            break
        state = apply_move(state, move)
        moves_made += 1
        if moves_made > max_moves_allowed:
            break

    assert state.is_win(), "greedy human policy failed to clear a reverse-built (solvable) level"
    min_moves_expected = len(pile.items) // 3       # cannot finish faster than the number of triples
    assert min_moves_expected <= moves_made <= max_moves_allowed
```
## 3. Как не разойтись правилами игры между C# и Python

Задача: решатель написан на Python (для порождения и проверки уровней), а сама игра — на C# (типично для Unity/Godot-C#). Оба должны согласованно понимать, что такое «доступный предмет», «ход», «тройка», «поражение». Ниже — честная оценка вариантов, без утверждения, что какой-то один является отраслевым стандартом именно для этого случая (специализированных источников про синхронизацию C#/Python для игровых правил не нашлось; ниже собраны задокументированные общеинженерные техники, которые непосредственно переносятся на эту задачу).

### 3.1 Полный независимый порт правил на оба языка

Правила игры (доступность, снятие троек, условие поражения) реализуются дважды — один раз на C# для игры, один раз на Python для решателя, — как два независимых, но по смыслу идентичных модуля.

- **Цена.** Двойная стоимость разработки и, что важнее, двойная стоимость сопровождения: любое изменение правил (например, добавление нового типа препятствия) нужно вносить в обоих местах и держать в голове оба представления одновременно.
- **Надёжность.** Самая низкая из всех вариантов без дополнительных мер: ничто не мешает двум реализациям незаметно разойтись в редком краевом случае (например, порядок обработки одновременного заполнения полки и появления тройки), и это может не проявиться до тех пор, пока решатель не пометит уровень как проходимый, а игра не даст игроку застрять.
- Единственный практичный способ снизить риск при этом подходе — обязательный набор общих контрактных тестов (раздел 3.2) или golden master (раздел 3.3) поверх обеих реализаций; сам по себе «просто оба порта» — самый ненадёжный вариант из перечисленных.

### 3.2 Общий формат проверочных случаев (контрактные тесты)

Фиксируется язык-независимый набор пар «состояние + ход → новое состояние» (или «уровень → решаемость/число ходов») в нейтральном формате (JSON/YAML), и обе реализации — C# и Python — прогоняются против одного и того же набора в своих тестовых раннерах (xUnit/NUnit на стороне C#, pytest на стороне Python).

- **Цена.** Умеренная: нужно один раз спроектировать нейтральный формат случая и написать по одному адаптеру на каждый язык («загрузить случай → прогнать через мои правила → сравнить с ожидаемым результатом»), дальше пополнение набора случаев дёшево.
- **Надёжность.** Средняя и управляемая: сила метода прямо пропорциональна полноте набора случаев — руками написанные случаи покрывают предвидённые дизайнером ситуации (типичный ход, заполнение полки, тройка при последнем ходе, кратность 3), но по определению не покрывают то, что дизайнер не предусмотрел. Комбинация с property-based тестами (раздел 2) на стороне Python, чьи находки (минимальные «сжатые» контрпримеры) переносятся в контрактный набор, частично закрывает этот пробел — это стандартная практика: Hypothesis сохраняет найденный минимальный контрпример для детерминированного воспроизведения при последующих запусках (см. описание поведения shrink/replay в [Quickstart](https://hypothesis.readthedocs.io/en/latest/quickstart.html) и в обзоре [GitHub HypothesisWorks/hypothesis](https://github.com/hypothesisworks/hypothesis)), и такой контрпример буквально становится новым файлом контрактного случая для C#-реализации.

```python
import json
from pathlib import Path


def export_contract_case(state_before: "State", move: int, state_after: "State", case_path: Path) -> None:
    """Write a language-neutral test case that both the Python and the C# rule
    engines can be checked against independently."""
    case = {
        "state_before": state_before.to_dict(),
        "move": move,
        "state_after_expected": state_after.to_dict(),
    }
    case_path.write_text(json.dumps(case, indent=2, ensure_ascii=False))
```

```csharp
// C# side: load the same JSON file and assert the C# rules produce an identical result.
// (Illustrative signature only — actual (de)serialization depends on the project's JSON library.)
var testCase = ContractCase.LoadFrom("cases/case-0007.json");
var actual = GameRules.ApplyMove(testCase.StateBefore, testCase.Move);
Assert.Equal(testCase.StateAfterExpected, actual);
```

### 3.3 Golden master (эталонные записи прогонов)

Один прогон одной реализации (например, эталонного решателя на Python) на конкретном уровне сохраняется целиком — вся последовательность состояний или хотя бы вход и итоговый результат — как «эталон» (golden file); при последующих изменениях кода (в любой из реализаций) новый прогон сравнивается побайтово/по значению с сохранённым файлом.

- Техника происходит из практики характеризационного тестирования легаси-кода, введённой Майклом Физерсом; общее описание и происхождение термина — [Blexin, «Golden Master Pattern: don't fear the legacy code!»](https://blexin.com/en/blog-en/golden-master-pattern-dont-fear-the-legacy-code/) и статья в Wikipedia про [Characterization test](https://en.wikipedia.org/wiki/Characterization_test).
- Разновидность «слепой golden master» (blind golden master), где новая и старая реализация вызываются в рамках одного теста и их результаты сравниваются напрямую без промежуточного файла, задокументирована в статье [«The Blind Golden Master», DEV Community](https://dev.to/rnowif/the-blind-golden-master-67h); там же приводится пример практики: «GitHub ran both algorithms in production, comparing outputs and raising errors on mismatch until confidence was established, then switched to the new implementation» — то есть параллельный прогон двух реализаций в проде до полного доверия перед переключением, что напрямую переносится на пару «Python-решатель / C#-игра», если у обоих есть общий пайплайн CI.
- **Цена.** Низкая для старта (написать «прогнать и сохранить файл» проще, чем спроектировать формат контрактных случаев), но накопление golden-файлов без присмотра создаёт свою проблему сопровождения: при намеренном изменении правил нужно осознанно перегенерировать эталоны, а автоматическая перегенерация «раз тест упал — обновим эталон» обесценивает тест (см. предупреждение там же, в [Ro-che, «Introduction to golden testing»](https://ro-che.info/articles/2017-12-04-golden-tests): golden master не доказывает корректность результата — он только защищает от непреднамеренного отклонения от уже зафиксированного поведения).
- **Надёжность.** Хорошо обнаруживает **расхождение** между реализациями (в том числе неожиданное, не предусмотренное заранее — в отличие от контрактных тестов из раздела 3.2, которые проверяют только то, что явно записано), но не гарантирует, что зафиксированное поведение вообще было верным изначально: если первый прогон (тот, что стал эталоном) уже содержал ошибку, golden master её просто заморозит и будет требовать её же от второй реализации.
- Дополнительное необходимое условие для применимости — детерминированность: недетерминированные значения (порядок обхода, `random`-состояния, время) должны быть либо зафиксированы общим seed, либо исключены из сравнения, иначе метод неприменим в принципе — это отдельно подчёркивается в материалах про golden master (см. выше, Ro-che).

### 3.4 Смежная техника: детерминированный lockstep с чек-суммами состояния

Из области сетевого мультиплеера — при lockstep-архитектуре все копии симуляции обязаны давать битово идентичный результат по идентичным входам, и для отладки расхождений применяется чек-сумма состояния, которую каждая копия считает и сверяет на каждом шаге, а не только в конце партии — это позволяет локализовать первый момент расхождения, а не только факт его наличия. Этот метод не является прямым решением задачи «синхронизировать C# и Python», но даёт полезный практический приём для встраивания в любой из подходов 3.1–3.3: если результаты не совпали, сверять не только финальное состояние, а чек-сумму на каждом ходу, чтобы быстро найти конкретное правило, в котором разошлись реализации.

### 3.5 Итоговая рекомендация

Ни один из трёх подходов не является безусловно «правильным» — они решают разные части проблемы и обычно комбинируются:

- Контрактные тесты (3.2) — для явно предвидённых, специально спроектированных дизайнером граничных случаев; дёшевы в сопровождении, но слепы к непредвиденному.
- Golden master (3.3) — для быстрого обнаружения любого незапланированного расхождения между уже существующими реализациями; дёшев для старта, но требует дисциплины при обновлении эталонов и не проверяет исходную корректность.
- Property-based тесты на Python-стороне (раздел 2) как поставщик новых контрактных случаев для C# — практический мост между «нашли баг один раз в Python» и «эта же проверка теперь навсегда защищает и C#-реализацию».
- Полный дублирующий порт правил (3.1) неизбежен в том смысле, что правила так или иначе должны существовать в обоих языках — вопрос не «делать порт или нет», а «чем, кроме честного слова, подтверждать, что оба порта согласованы», и на этот вопрос отвечают именно 3.2–3.4, а не сам факт порта.

## Источники

- [Hypothesis — PyPI](https://pypi.org/project/hypothesis/)
- [Hypothesis — Quickstart (readthedocs)](https://hypothesis.readthedocs.io/en/latest/quickstart.html)
- [Hypothesis — Custom strategies (readthedocs)](https://hypothesis.readthedocs.io/en/latest/tutorial/custom-strategies.html)
- [HypothesisWorks/hypothesis (GitHub)](https://github.com/hypothesisworks/hypothesis)
- [«How to Build Property-Based Testing with Hypothesis», 2026](https://oneuptime.com/blog/post/2026-01-30-how-to-build-property-based-testing-with-hypothesis/view)
- [ppelikan/game-map-editor — Level JSON file structure (GitHub wiki)](https://github.com/ppelikan/game-map-editor/wiki/Level-JSON-file-structure)
- [阮一峰, «羊了个羊，如何自己实现»](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html)
- [Hoogeboom, Kosters, van Rijn, Vis, «Acyclic Constraint Logic and Games», arXiv:1604.05487](https://arxiv.org/pdf/1604.05487)
- [Blexin, «Golden Master Pattern: don't fear the legacy code!»](https://blexin.com/en/blog-en/golden-master-pattern-dont-fear-the-legacy-code/)
- [Characterization test — Wikipedia](https://en.wikipedia.org/wiki/Characterization_test)
- [«The Blind Golden Master», DEV Community](https://dev.to/rnowif/the-blind-golden-master-67h)
- [Ro-che, «Introduction to golden testing»](https://ro-che.info/articles/2017-12-04-golden-tests)

