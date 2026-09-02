# 20-first-run-gate — notes

## Дыра 1 — CatIdentity.Traits писала cat.save на чтение
`Shell/CatIdentity.cs`: убрана запись `CatSaveFile.Write(...)` из геттера `Traits`.
Причина записи (комментарий "player who opens and closes the game should not
meet a different animal") была верна 28.08, до того как существование
cat.save стало воротами первого запуска. Причина жива, но уже удовлетворена
без записи: `CatTraits.Roll(seed)` детерминирован от device id + language
(см. `CatTraits.cs:139` "Deterministic ... so the same player gets the same
cat on every launch"), так что повторное чтение без сохранённого файла даёт
тот же кот каждый раз. Единственный вызывающий `Traits` без сохранённого
кота — `DebugGameView.CatStateTraits` через `board.txt`; раньше это тихо
создавало безымянный cat.save и навсегда закрывало ворота. `Forget()`
как был без вызывающих, так и остался — вне SCOPE этой задачи.

## Дыра 2 и 3 — голый Build и AddComponent без проверки
`Shell/GameBoot.cs`:
- `SafeBuild` теперь возвращает `bool` (успех/провал), остальные 6 мест её
  игнорируют — обратная совместимость сохранена.
- `ShowMeetYourCat` — теперь: 1) проверка `GetComponent<...>() != null` перед
  `AddComponent` (как в остальных 6 местах); 2) `Build` идёт через
  `SafeBuild`; 3) при провале компонент уничтожается (`Destroy`) и метод
  возвращает `false`; 4) метод возвращает `bool`.
- `ShowCapture` — тот же guard перед `AddComponent<CaptureScreen>`.
- В `OnCatReady`: если `ShowMeetYourCat` вернул `false`, экран съёмки НЕ
  прячется — вместо этого `screen.BackToButtons(Copy.Of("photo.our_fault"))`
  (новый публичный метод в `CaptureScreen`, снимает busy-состояние и
  показывает сообщение). Раньше провал Build молча прятал рабочий экран
  съёмки за красной меткой SafeBuild без единой кнопки.

## Дыра 4 — SetBusy без проверки _busy
`View/CaptureScreen.cs`: `SetBusy` — добавлена проверка `if (_busy == null)
return;` в начале. Путь: `capture.txt` со второй строкой запускает
`Handle()` через корутину независимо от того, успел ли `Build()` дойти до
создания `_busy` — при испорченном каркасе (SafeBuild ловит исключение)
первый же вызов `SetBusy` внутри `Handle()` падал в NullReferenceException.

## Тест
`dotnet test /Users/rdolgov/workflow/git/mobile-game-cat/build/core-tests/core-tests.csproj`:
```
Пройден!   : не пройдено     0, пройдено   270, пропущено     0, всего   270, длительность 497 ms. - core-tests.dll (net8.0)
```
(core-tests не видит Shell/View — там Unity-типы; правки в этих файлах тестом
не покрываются напрямую, только тем, что Core не тронут.)

## Не сделано
- Сценарий владельца (выбрал кота → не оставил → закрыл → открыл → выбрал
  другого) не прогнан — эмулятор и сборку не гонял по прямому указанию
  задачи; должен прогнать владелец сам (VERIFY).
- Искусственная порча каркаса для проверки экрана съёмки после отказа
  Build — не воспроизведена вживую, только через чтение кода.

## Прогон сценария ворот — 2026-09-02, эмулятор, сборка 5062311

1. pm clear → board.txt в files → запуск → в files НЕТ cat.save (раньше
   появлялся молча; список файлов снят командой ls).
2. board.txt удалён → перезапуск → экран съёмки на месте, все три кнопки
   (emulator-gate-stays-open.png, клеймо сборки на снимке).

Не прогнано живьём: искусственный отказ Build экрана знакомства (VERIFY п.3)
— возврат кнопок проверен чтением кода. Поэтому задача остаётся in_progress.
