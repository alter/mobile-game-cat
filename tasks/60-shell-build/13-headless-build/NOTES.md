ЗАДАЧА 13-headless-build: ЗАКРЫТО с оговоркой
- macOS build: Succeeded (109MB), скриншот /tmp/game_screen.png
- iOS Xcode project: Unity-iPhone.xcodeproj (233KB) с build/ios/
- Исходная ошибка 'build only device' исправлена выбором симулятора/устройства в Xcode

Дополнение 26.08.2026: запуск в симуляторе проверен целиком
- Проект под устройство в симуляторе не собирается: SDKROOT=iphoneos,
  библиотеки помечены platform 2. Нужен отдельный проект под SDK симулятора.
- Добавлена точка входа BuildScript.BuildIOSSimulatorProject -> build/ios-sim/,
  переключает SDK и архитектуру на ARM64, после сборки возвращает настройки.
- xcodebuild ... -sdk iphonesimulator -arch arm64 -> BUILD SUCCEEDED,
  установлено и запущено в симуляторе iPhone 17, снимок экрана снят.
- Порядок команд записан в cat-shelter-tech.md, раздел 5.
- Найдено на снимке, в работу вёрстки: заголовок уходит под вырез камеры,
  полка внизу обрезана по краям экрана — безопасная область не учтена.
- Запуск на настоящем устройстве по-прежнему не проверен: нет команды
  разработчика и подписи, это задача 14-testflight.

status:todo → done, 26.08.2026 — метка отставала от repository: сборка
работает и проверена дважды. verify остаётся pending: проверяющий здесь —
тот же, кто правил сборочный сценарий, а по правилу независимости
(tasks/README.md) подписывать свою работу нельзя. Открытым остаётся запуск
на устройстве.

---

## status:done → in_progress, 2026-08-27

The OUTCOME artefact this task names is not there. What is missing, what does
exist, and why it matters: `tasks/AUDIT-2026-08-27.md`.

---

## What now exists, 2026-08-27

`build/headless-build.sh` exists, is executable, and does what the OUTCOME
line asks for as far as it is possible to ask for right now:

- `set -euo pipefail`, resolves the repo root from `${BASH_SOURCE[0]}`, runs
  from any cwd, and names the failing stage on stderr via an `ERR` trap
  (`== STAGE FAILED: <name> (exit N) ==`).
- Stage order: core-purity (`build/check-core-purity.sh`) → C# tests
  (`dotnet test build/core-tests/core-tests.csproj` with the coverage
  runsettings) → Python tests (`pytest tools/tests -q`, via `.venv/bin/python3`
  if present, since the Homebrew `python3` on this machine has a broken
  `pyexpat` and cannot even import `xml.etree.ElementTree`) → the coverage
  gate (`build/coverage-summary.py --min 90`, task 20-rules-core/05-coverage)
  → Unity Android build → Unity iOS Xcode project build → signing/.ipa.
- `dotnet test` is wrapped in a retry: on MSB3021/MSB3027/"being used by
  another process" it waits 30s and retries once, per the shared-tree rule for
  this audit.
- Unity is located via `$UNITY_PATH` if set, else the version-sorted newest
  `/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity`, with a
  readable error if none is found. After each Unity batch build the script
  asserts the expected output exists on disk
  (`game/build/android/CatShelter.apk`,
  `game/build/ios/CatShelter/Unity-iPhone.xcodeproj`) rather than trusting
  Unity's own exit code, because that code is known to return 0 on some
  failures.
- `--tests-only` runs everything up through the coverage gate and exits 0
  without touching Unity or signing at all. `--no-sign` runs the Unity builds
  but skips the signing stage. `-h`/`--help` prints usage.
- The signing stage does not fake a `.ipa`. It checks `APPLE_TEAM_ID`; if
  unset (it always will be right now — no Apple Developer Program account, no
  team ID, `tasks/DECISIONS.md` decision D17) it prints that explanation by
  name, names `60-shell-build/14-testflight` as the task that closes it, and
  exits 1. Even with `APPLE_TEAM_ID` set the archive/export path itself is
  not implemented — it has never been exercised, since there has never been a
  team ID to exercise it with — so the stage also exits 1 in that case,
  pointing at the same follow-up task.

**Verified this run** (raw output kept by the calling context, not repeated
here): `--tests-only` → exit 0, all four stages pass, coverage 93.8%.
Unsigned full run (default, no flags) → core-purity OK, 136 C# tests pass,
144 Python tests pass, coverage gate passes at 93.8% (≥ 90%), Unity produces
both `game/build/android/CatShelter.apk` (27M) and
`game/build/ios/CatShelter/Unity-iPhone.xcodeproj`, then the script stops at
the signing stage with exit 1 and the `APPLE_TEAM_ID` / `14-testflight`
message.

**What the script still cannot do, and why:**

- Produce a signed `.ipa`. Blocked on the Apple Developer Program account and
  team ID (D17) — not a scripting problem, an account problem.
- Even given a team ID, actually archive/export one — that code path is not
  written yet, because there has been nothing to test it against. Belongs to
  `60-shell-build/14-testflight`.
- Prove anything about a physical device. Everything above is simulator/CI
  grade; device signing, install and launch are explicitly out of scope here
  per `cat-shelter-tech.md` §5 ("Running on a real device").

`status:in_progress` is left as `in_progress` here even though the script now
exists and runs correctly end to end, because the OUTCOME's own words — "a
signed .ipa" — are still not produced, by design, until D17 is resolved. The
part of the task that was actually missing (the script itself, wired to every
stage, truthful about signing) now exists.

### Review of the above, same day

**The clean-checkout promise had a hole.** The script preferred
`.venv/bin/python3` and fell back to the system `python3`, and a clean checkout
has no `.venv` — so on the very case the OUTCOME names, the Python stage would
have failed with a missing-pytest error and the coverage stage with an XML
parse error, neither of which says what is actually wrong. Homebrew's python3
on this machine has a `pyexpat` that cannot read the cobertura report at all,
so the venv is not optional even where pytest happens to be installed.

Now the script checks its interpreter can do both jobs before using it, and
fails with the two commands that fix it. `requirements.txt` was created for the
same reason: the script pointed at a file that did not exist. `PYTHON=` is
honoured as an override.

Verified by hand afterwards, not taken on report: `--tests-only` exits 0;
`coverage-summary.py --min 99.9` exits 1 and prints the FAIL line, `--min 90`
exits 0; the Android APK is on disk at 27.8 MB and the iOS Xcode project
exists; the signing stage refuses with the D17 message.
