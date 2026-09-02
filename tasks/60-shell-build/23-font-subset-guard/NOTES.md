# 23-font-subset-guard — заметки

## Что сделано

- `tools/tests/test_font_coverage.py`: для семи нелатинских таблиц
  (ChineseSimplified, ChineseTraditional, Japanese, Korean, Thai, Arabic,
  Hindi) читает cmap соответствующего подрезанного шрифта в
  `game/Assets/Resources/Fonts/` (через fontTools) и проверяет, что каждый
  знак своей письменности (диапазон Юникода, те же границы, что и
  `NON_LATIN` в `test_copy_table.py`) в этом cmap есть. Список «своих»
  шрифтов не хардкожен в отрыве от кода: `test_the_fallback_faces_are_exactly_the_known_scripts`
  сверяет `OWN_FONT_TABLES` с `FontFallbacks.Faces` — расхождение валит тест
  явно, а не молча.
- Разбор `Copy*.cs` не продублирован — таблицы читаются импортом
  `tables()` из `tools/tests/test_copy_table.py`.
- Латиница/кириллица/греческий/иврит внутри нелатинских строк (например,
  `card.game_name` = "Sootpaw" одинаков во всех языках) намеренно НЕ
  проверяется по cmap подрезанного шрифта — эти знаки рисует шрифт панели
  по умолчанию до того, как дойдёт очередь до списка запасных, так что их
  отсутствие в тайском/арабском/деванагари файле не квадрат. Первая версия
  теста проверяла весь текст строки целиком и упала на этом ложно (тайская
  и хинди таблицы содержат буквы из "Sootpaw" и пунктуацию, которых в
  скачанном на 29.08 наборе не было) — сужено до диапазона письменности.
- `requirements.txt`: fontTools в нём не было (только в `.venv`, куда его
  когда-то поставили руками для `tools/fonts/subset.py`) — добавлен
  `fonttools>=4.0` с комментарием, для чего он нужен.
- `build/headless-build.sh`: правка не потребовалась — единственная ступень
  Python-тестов уже запускает `pytest tools/tests -q` целиком (строка 127),
  новый файл подхватывается автоматически.

## Мутационная проверка

Знак `龘` (U+9F98, редкий иероглиф) добавлен временной строкой
`["font.coverage.mutation_test"] = "龘"` в таблицу `Japanese`
(`game/Assets/Shell/Copy.Scripts.cs`, рядом с `win.next`). Заранее
проверено чтением cmap, что этого знака в `NotoSansJP-Regular.otf` нет.

Красный прогон (`.venv/bin/python3 -m pytest tools/tests/test_font_coverage.py -q`),
дословно:

```
=========================== short test summary info ============================
FAILED tools/tests/test_font_coverage.py::test_every_character_is_in_its_subset_fonts_cmap[Japanese]
1 failed, 7 passed in 0.07s
```

Сообщение об ошибке (тоже дословно):

```
E       AssertionError: Japanese: 1 character(s) missing from NotoSansJP-Regular's cmap — these render as empty boxes on iOS (commit 9478eb4 is the last time this happened by hand): 龘. Re-run tools/fonts/subset.py against the master Noto sources.
E       assert not {'龘'}
```

Строка убрана (`git diff --stat game/Assets/Shell/Copy.Scripts.cs` после
отката пуст — правка не оставила следов). Зелёный прогон того же файла,
дословно:

```
........                                                                 [100%]
8 passed in 0.06s
```

## Не сделано / ограничения

- Латинские/кириллические таблицы (English, Russian, восемь языков
  `Copy.Latin.cs`) не проверяются по cmap вообще — для них в репозитории
  нет ни одного шрифтового файла (шрифт по умолчанию встроен в Unity и не
  подрезается), проверять нечего. Это решение записано в докстринге файла,
  не придумано на ходу.
