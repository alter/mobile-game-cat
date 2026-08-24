# Headless-сборка и CI для Unity

Дата сбора: 2026-08-24. Версия стека: Unity 6.3 LTS (6000.3.x), C#, .NET Standard 2.1.

## Кратко

- Базовый набор ключей для headless-запуска: `-batchmode -nographics -quit -projectPath <path> -executeMethod <ClassName.MethodName> -logFile <path>` — каждый ключ по отдельности задокументирован в официальном мануале ([Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)).
- `-quit` опасен тем, что «can hide some error messages» (хотя они остаются в логе), и явное предупреждение: «If the Editor is running asynchronous code, then `-quit` can cause the application to hang and become unresponsive» ([там же](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)).
- Правильный способ выйти с нужным кодом ошибки — вызывать `EditorApplication.Exit(code)` из своего `-executeMethod`, а не полагаться на автоматический `-quit`; сообщество отдельно предупреждает, что совмещение `-quit` с ручным `EditorApplication.Exit()` может мешать корректному завершению метода ([Option to remove -quit command, JetBrains/teamcity-unity-plugin#34](https://github.com/JetBrains/teamcity-unity-plugin/issues/34)).
- Рабочий образец кастомного build-скрипта на `BuildPipeline.BuildPlayer`/`BuildPlayerOptions` с проверкой `BuildReport.summary.result` есть в официальном мануале Unity ([Create a custom build script](https://docs.unity3d.com/6000.5/Documentation/Manual/build-script-build.html)).
- Активация лицензии в CI выполняется ключами `-batchmode -serial <key> -username <email> -password <pwd>` (Pro/Plus) либо через файл `-manualLicenseFile <file.alf>` для офлайн-сценариев; для Unity Personal официальный командно-строчный путь ограничен — ручная активация Personal-лицензии через веб-портал `license.unity3d.com` в 2025–2026 годах перестала поддерживаться, что подтверждено открытым issue в репозитории GameCI ([game-ci/documentation#408](https://github.com/game-ci/documentation/issues/408)).
- GameCI (game.ci) — открытый набор Docker-образов (`unityci/editor`) и GitHub Actions для сборки Unity-проектов в CI; официально «currently images are only available with Ubuntu or Windows as the base operating system» — под iOS/macOS сборку нужен нативный macOS-раннер, Docker-образа под macOS нет ([GameCI Docker images for Unity](https://game.ci/docs/docker/docker-images/)).
- На macOS журнал редактора по умолчанию лежит в `~/Library/Logs/Unity/Editor.log`; путь переопределяется ключом `-logFile`.
- Типовые грабли из отчётов разработчиков: зависание batchmode из-за `-quit` при асинхронном коде, ошибка «Multiple Unity instances cannot open the same project» из-за зависших процессов `UnityShaderCompiler`/`JobProcess` и не удалённого `Temp`, регрессия с потерей кода возврата процесса на Unity 6000.2.14f1 в паре с Python `subprocess`, и резкое удлинение первого импорта ассетов на «холодном» CI-раннере без кеша `Library`.
## 1. Сборка без графического режима: ключи и порядок

Точные формулировки из официального мануала ([Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)):

- **`-batchmode`** — «Run Unity in batch mode. In batch mode, Unity runs command line arguments without the need for human interaction.»
- **`-nographics`** — «When you run this in batch mode, Unity doesn't initialize the graphics device. You can then run automated workflows on machines that don't have a GPU.»
- **`-quit`** — «Quit the Unity Editor after other commands have finished executing. This can hide some error messages, but they still appear in the Editor's log file.» Отдельное предупреждение: «If the Editor is running asynchronous code, then `-quit` can cause the application to hang and become unresponsive.»
- **`-projectPath`** — «Open the project at the given path, which can be absolute or relative to the current working directory. If the pathname contains spaces, enclose it in quotes.»
- **`-executeMethod`** — «Execute the static method as soon as Unity opens the project, and after the optional Asset server update is complete.»
- **`-logFile`** — «Specifies a file path location to which Unity writes the Editor log file. To output to the console, specify `-` for the path name.»
- **`-buildTarget`** — «Select an active build target to launch the Editor in. The options available depend on which build targets you have enabled in the Editor.»
- **`-createProject`** — «Create an empty project at the given path.»
- **`-disable-assembly-updater`** — «Specify a space-separated list of assembly names as parameters for Unity to ignore on automatic updates.»

Важное ограничение, прямо связанное с типичной проблемой параллельных запусков (раздел 6): «You can't open a project in batch mode while the Editor has the same project open; only a single instance of Unity can run at a time» ([там же](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)).

**Порядок ключей.** Официальный мануал не описывает единый обязательный порядок аргументов — они распознаются по имени, а не по позиции. На странице про сборку из командной строки для Unity 6.x приведён такой пример (Windows):

```
"C:\Program Files\Unity\Hub\Editor\6000.3.XXf1\Editor\Unity.exe" -executeMethod BuildScripts.BuildWindows64 -buildTarget StandaloneWindows64 -batchmode -quit -projectPath "C:\path\to\Project" -logFile C:\Logs\build.log
```

([Build a player from the command line, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/build-command-line.html)). Обязательными для сборки из командной строки в этом мануале названы `-projectPath <pathname>` и `-quit`; отдельно подчёркнуто ограничение: «you can't build for multiple targets in a single command line invocation. Instead, run the Unity process separately for each target platform», потому что API вроде `BuildProfile.SetActiveBuildProfile`/`EditorUserBuildSettings.SwitchActiveBuildTargetAsync` не работают корректно в batchmode ([там же](https://docs.unity3d.com/6000.4/Documentation/Manual/build-command-line.html)).

**Почему `-quit` опасен и как выходить правильно.** Автоматический `-quit` не даёт контроля над кодом завершения процесса и, как указано выше, может зависнуть при асинхронном коде. Правильный паттерн — не полагаться на `-quit`, а завершать процесс вручную из своего метода: «calling this function will exit right away, without asking to save changes, so it is mostly useful for exiting out of a commandline process with a specific error» — это официальное описание `EditorApplication.Exit` ([Unity Scripting API: EditorApplication.Exit](https://docs.unity3d.com/ScriptReference/EditorApplication.Exit.html)). При этом стороннее сообщество отмечает конфликт двух механизмов: «"-quit" is added automatically as a parameter but that is an issue when executed method waits for editor update to finish execution, so there needs to be an option to remove "-quit" and let the method call EditorApplication.Exit(0) manually» ([Option to remove -quit command, JetBrains/teamcity-unity-plugin#34](https://github.com/JetBrains/teamcity-unity-plugin/issues/34)). Практическая рекомендация, собранная из нескольких обсуждений: оборачивать сборочную логику в `try/catch`, вызывать `EditorApplication.Exit(0)` при успехе и `EditorApplication.Exit(<code>)` при ошибке из собственного `-executeMethod`, и не рассчитывать на автоматическое завершение через `-quit`, если нужен предсказуемый код возврата.

## 2. Пример BuildScript на C#

Официальный пример кастомного build-скрипта из мануала Unity (страница «Create a custom build script»), использующий `BuildPipeline.BuildPlayer`, `BuildPlayerOptions` и проверку `BuildReport.summary.result`:

```csharp
using System.IO;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEngine;

public class CustomBuild
{
    [MenuItem("Build/Build Windows Player With Readme")]
    public static void BuildWindowsPlayer()
    {
        // Define build options
        string path = EditorUtility.SaveFolderPanel("Choose Location of Built Game", "", "");

        var buildOptions = new BuildPlayerOptions()
        {
            // Adjust scene list based on your project
            scenes = new string[] { "Assets/Scenes/Scene1.unity", "Assets/Scenes/Scene2.unity" },
            locationPathName = path + "/MyGame.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.AutoRunPlayer
        };

        // Build the Player
        var buildReport = BuildPipeline.BuildPlayer(buildOptions);

        if (buildReport.summary.result != BuildResult.Succeeded)
        {
            Debug.Log("Build failed!\n\n" + buildReport.SummarizeErrors());
            return;
        }

        // Post-process: Copy README file to the build folder
        File.Copy("Assets/Documentation/README.txt", path + "/README.txt", true);
    }
}
```

([Create a custom build script, 6000.5](https://docs.unity3d.com/6000.5/Documentation/Manual/build-script-build.html)). Мануал уточняет, что скрипты для командной строки размещают в папке `Editor/` проекта (или в отдельной Editor-сборке), а вызывается такой метод через `-executeMethod` ([там же](https://docs.unity3d.com/6000.5/Documentation/Manual/build-script-build.html)).

Официальная документация Scripting API даёт аналогичный по духу пример с явной веткой на `BuildResult.Succeeded`/`BuildResult.Failed`:

```csharp
public class BuildPlayerExample
{
    [MenuItem("Build/Build iOS")]
    public static void MyBuild()
    {
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { "Assets/Scene1.unity", "Assets/Scene2.unity" };
        buildPlayerOptions.locationPathName = "iOSBuild";
        buildPlayerOptions.target = BuildTarget.iOS;
        buildPlayerOptions.options = BuildOptions.None;
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
        }
        if (summary.result == BuildResult.Failed)
        {
            Debug.Log("Build failed");
        }
    }
}
```

([Unity Scripting API: BuildPipeline.BuildPlayer](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html)).

Важная деталь для CI: сам по себе неуспешный `BuildPipeline.BuildPlayer` не приводит к автоматическому ненулевому коду возврата процесса — это подтверждается отдельной статьёй поддержки Unity, посвящённой именно этому вопросу: «Why doesn't a failed BuildPipeline.BuildPlayer return an error code in the command line?» ([Unity Support Help Center](https://support.unity.com/hc/en-us/articles/211195263-Why-doesn-t-a-failed-BuildPipeline-BuildPlayer-return-an-error-code-in-the-command-line)). Поэтому после проверки `summary.result` в CI-скрипте нужно явно вызывать `EditorApplication.Exit(1)` при провале и `EditorApplication.Exit(0)` при успехе (см. раздел 1), а не полагаться на код возврата, который выставит сам Unity.

## 3. Активация лицензии Unity в командной строке и в CI

**Активация серийным ключом (Pro/Plus).** Официальный синтаксис из мануала «Manage your license through the command line»:

macOS:
```
<unity-command-location> -quit -batchmode -serial SB-XXXX-XXXX-XXXX-XXXX-XXXX -username 'name@example.com' -password 'XXXXXXXXXXXXX'
```

Windows:
```
"<editor-installation-location>" -quit -batchmode -serial E3-XXXX-XXXX-XXXX-XXXX-XXXX -username name@example.com -password XXXXXXXXXXXXX
```

([Manage your license through the command line, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)). Активация именованного пользователя без серийного ключа — та же команда, но со значением `-serial` пустым/опущенным. Возврат лицензии:

```
<unity-command-location> -quit -batchmode -returnlicense -username 'name@example.com' -password 'XXXXXXXXXXXXX'
```

([там же](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)). Предварительное условие: «license file folder exists» и есть право записи в эту папку. Явно указано: «The following procedures don't apply to Unity Personal. To activate a license for Unity Personal, log in to the Unity Hub» ([там же](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)).

**Ручная (офлайн) активация через файл.** Для сценариев, где машина CI не имеет прямого доступа к серверу лицензий, используется файловый обмен: `"<editor-installation-location>" -batchmode -manualLicenseFile <yourUlfFile> -logfile`; отмечено, что «this command doesn't return output to the Command Prompt» — то есть проверять успех нужно по логу/файлу, а не по выводу в консоль (по данным поискового обзора мануала Unity, раздел про manual activation; сам ключ не был получен дословно повторным WebFetch — помечается как «требует дополнительной проверки по первоисточнику»).

**Для CI-инструментов вроде Jenkins** мануал отдельно рекомендует добавлять `-nographics`, чтобы избежать проблем при активации без графического окружения ([Manage your license through the command line, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)).

**Ограничения Personal-лицензии в CI (актуальная проблема на 2025–2026).** GameCI документирует раздельные пути для Personal и Professional лицензий: для Personal — получить `.ulf`-файл через Unity Hub (`Preferences > Licenses > Get a free personal license`) и положить его содержимое в секрет `UNITY_LICENSE` вместе с `UNITY_EMAIL`; для Professional/Plus — использовать `UNITY_SERIAL` вместе с `UNITY_EMAIL`/`UNITY_PASSWORD`, и явно: «Do NOT follow the steps for the personal license if you have a professional license» ([Activation, GameCI](https://game.ci/docs/github/activation/)). Однако для сценария ручной активации (ALF → ULF через `license.unity3d.com`) в открытом issue документации GameCI зафиксировано, что этот путь для персональных лицензий сломан: «The website, though, provides activation procedures only for Pro licenses now», результат попытки активации персональной лицензии — «Get an error from the website»; issue остаётся открытым без официального обходного пути от Unity ([alf->ulf license activation no longer possible for personal licenses, game-ci/documentation#408](https://github.com/game-ci/documentation/issues/408)). Известные обходные пути от сообщества — сторонние инструменты вроде `game-ci/unity-license-activate` (поддерживает 2FA через `--authenticator-key`) и `mob-sakai/unity-activate`, использующие переменные окружения `UNITY_USERNAME`/`UNITY_PASSWORD`/`UNITY_SERIAL` для автоматической активации в CI.

## 4. GameCI

GameCI (game.ci) — открытый проект, предоставляющий готовые Docker-образы и GitHub Actions/GitLab CI шаблоны для сборки и тестирования Unity-проектов в CI. «All projects for Unity in GameCI use `game-ci/docker` docker images», публикуемые как `unityci/editor` на Docker Hub; «All editor versions» поддерживаются, «Images for newly released Unity editor versions are added almost immediately» — отдельного явного упоминания Unity 6/6000.x на этой странице не найдено, но формулировка про «все версии» и «почти сразу после релиза» подразумевает и линейку 6000.x ([GameCI Docker images for Unity](https://game.ci/docs/docker/docker-images/)).

**Ограничение по базовой ОС и iOS/macOS.** «Currently images are only available with Ubuntu or Windows as the base operating system» ([там же](https://game.ci/docs/docker/docker-images/)). Из этого прямо следует ограничение по iOS: полноценная компиляция/подпись Xcode-проекта требует инструментов Apple, доступных только на macOS, поэтому macOS-образа Docker для GameCI нет — «We are looking to include MacOS as a base image "in the future", which is mostly dependent on contributions from the community»; вместо контейнера для генерации IL2CPP-сборок под macOS рекомендуется использовать нативный macOS-раннер GitHub Actions ([там же](https://game.ci/docs/docker/docker-images/)). При этом компонент `ios` присутствует в списке компонентов, из которых собираются кастомные образы (`android`, `ios`, `linux-il2cpp`, `mac-mono`, `webgl`, `windows-mono`) — но такой образ на Ubuntu-базе годится только для подготовки/экспорта Xcode-проекта, а не для финальной компиляции бинарника, которая всё равно требует Xcode на macOS ([Customize GameCI Unity Docker images](https://game.ci/docs/docker/customize-docker-images/)).

**Действия (Actions).** GameCI предоставляет отдельные GitHub Actions под разные шаги пайплайна: активацию лицензии (`game-ci/unity-activate`, обёртка над `unity-license-activate`), сборку (`unity-builder`), тесты. Для активации лицензии в GitHub Actions официальная инструкция описывает раздельные шаги для Personal и Professional лицензий (см. раздел 3) ([Activation, GameCI](https://game.ci/docs/github/activation/)).

## 5. Разбор журнала сборки: Editor.log

На macOS путь по умолчанию — `~/Library/Logs/Unity/Editor.log`; на Windows — `%LOCALAPPDATA%\Unity\Editor\Editor.log`; на Linux — `~/.config/unity3d/Editor.log` (данные собраны из практических руководств и подтверждаются документированным поведением ключа `-logFile`, который позволяет переопределить путь по умолчанию — см. раздел 1: «Specifies a file path location to which Unity writes the Editor log file»). Отдельно от Editor.log существует Player.log (для собранного плеера), который по умолчанию лежит там же, в `~/Library/Logs/Unity/Player.log` на macOS.

При запуске в batchmode Unity по умолчанию продолжает писать в тот же Editor.log, если не передан `-logFile` с явным путём — поэтому в CI обязательно указывать `-logFile <путь>`, чтобы получить предсказуемое расположение журнала для последующего разбора.

**Как поймать ошибку компиляции из batchmode.** Официального «единого кода возврата для ошибки компиляции» не задокументировано (см. также раздел 7 первого файла про коды возврата тестов) — рекомендация «the best way to understand the source of a problem is the content of error messages and stack traces» относится и к обычным сборкам. На практике ошибка компиляции C#, возникающая до этапа сборки/тестов, видна в логе как строки с `error CS####` и предшествует любым сообщениям о старте сборки/тестов; при этом сама Unity в таком случае тоже завершается с ненулевым кодом — это подтверждено отдельным issue в трекере: «[Batch Mode] Compilation error on first launch of Android batch build results in Unity closing with non-zero exit code» ([Unity Issue Tracker](https://issuetracker.unity3d.com/issues/batch-mode-compilation-error-on-first-launch-of-android-batch-build-results-in-unity-closing-with-non-zero-exit-code)). Таким образом отличить «ошибку компиляции» от «упавших тестов» или «упавшей сборки» по коду возврата напрямую нельзя — необходимо парсить `-logFile` на предмет `error CS` (компиляция) в противовес записям о результатах тестов/сборки, которые появляются позже в журнале.

## 6. Типовые грабли из отчётов разработчиков

**Зависание batchmode.** Официально задокументированная причина — асинхронный код в паре с `-quit`: «If the Editor is running asynchronous code, then `-quit` can cause the application to hang and become unresponsive» ([Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)). Дополнительно замечено, что `Update()` не вызывается стандартным образом в режиме `-batchmode -executeMethod`, из-за чего код, ожидающий колбэков через `EditorApplication.update`, может не завершиться без ручного цикла ожидания (обобщение из обсуждений на discussions.unity.com).

**«Multiple Unity instances cannot open the same project».** Официальный текст ошибки, с которым сталкиваются разработчики: «It looks like another Unity instance is running with this project open. Multiple Unity instances cannot open the same project» ([Multiple Unity instances cannot open the same project, Unity Discussions](https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project/607546)). Официальный ответ представителя Unity указывает на конкретную причину: «zombie instances of UnityShaderCompiler or JobProcess lingering when this happens» — то есть проблема не в самом ограничении «одна Unity — один проект» (оно тоже официально задокументировано, см. раздел 1), а в зависших дочерних процессах после аварийного завершения редактора ([там же](https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project/607546)). Сообщество предлагает удаление файла `UnityLockfile` (лежит в `Temp/` или `Library/`) и, если это не помогает, полное удаление папки `Temp/`; также рекомендуется на Windows завершать через Task Manager, на macOS — через Activity Monitor процессы `Unity.exe`/`Unity`/`Unity Hub` перед повторным запуском CI-джобы ([там же](https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project/607546); [Resolving "The project is currently open in the Unity Editor", Unity Support](https://support.unity.com/hc/en-us/articles/40828087523092-Resolving-the-The-project-is-currently-open-in-the-Unity-Editor-Please-close-it-in-the-Editor-to-proceed-with-this-operation-Error)). Практический вывод для CI: перед запуском batchmode-джобы стоит принудительно завершать зависшие процессы, относящиеся к проекту, и удалять `Library/Temp` при подозрении на «залипший» лок-файл, а не просто повторно запускать ту же команду.

**Проблема одновременно открытого редактора.** Как отмечено в разделе 1, официально: «You can't open a project in batch mode while the Editor has the same project open; only a single instance of Unity can run at a time» ([Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)) — на self-hosted CI-раннере это означает, что джоба не должна запускаться параллельно с локально открытым редактором на том же чекауте проекта, и что параллельные CI-джобы над одним и тем же клоном репозитория работать не будут — нужен отдельный клон/рабочая копия на джобу.

**Регрессия с потерей кода возврата процесса.** Зафиксировано в свежем (2025–2026) обсуждении: после обновления с Unity 2022.3.62f2 на 6000.2.14f1 Python-процесс, запускающий Unity через `subprocess.Popen()` с `-batchmode -executeMethod`, перестал получать код возврата, хотя C#-скрипт по-прежнему вызывает `EditorApplication.Exit(0)`/`EditorApplication.Exit(1)`; вместо штатного завершения процесс Unity «becomes unresponsive after the build completes, eventually becoming a zombie process» ([Unity batchmode does no longer return exit code, Unity Discussions](https://discussions.unity.com/t/unity-batchmode-does-no-longer-return-exit-code-that-could-be-captured-by-python/1698339)). Предложенные в теме обходные пути: не вызывать `EditorApplication.Exit()` вручную и дать Unity завершиться самостоятельно, либо использовать `Environment.ExitCode` и исключения вместо явного `Exit()` ([там же](https://discussions.unity.com/t/unity-batchmode-does-no-longer-return-exit-code-that-could-be-captured-by-python/1698339)). Это репортится для 6000.2.14f1, а не для 6000.3 — при переходе на 6.3 стоит отдельно перепроверить в своём CI, воспроизводится ли регрессия.

**Долгий первый импорт ассетов.** Поскольку Unity хранит внутреннее представление ассетов в папке `Library/`, любой «холодный» checkout на CI-раннере без сохранённого кеша `Library/` вызывает полный переимпорт всех ассетов при первом запуске batchmode — это структурная причина, а не баг. Зафиксированный на форуме частный случай деградации: «a project that previously imported in approximately one hour in versions 2019, 2023, 6, and 6.5 is now taking nearly four hours in version 6.3» ([Importing assets can be very slow, Unity Discussions](https://discussions.unity.com/t/importing-assets-can-be-very-slow/1716277)) — сообщение от отдельного пользователя, не воспроизведённая официально и не привязанная к причине на стороне Unity 6.3 как таковой; приводится как пример жалобы сообщества, а не подтверждённый факт деградации именно в 6.3. Для диагностики долгих импортов Unity предоставляет встроенный инструмент Import Activity (`Window > Analysis > Import Activity»), который показывает причину каждого переимпорта — например, «no previous revision was found (a first import, or the related artifact in the library was deleted)», смену зависимостей или апгрейд версии Unity (сообщество, [Reducing assets import times in Unity](https://dev.to/attiliohimeki/reducing-assets-import-times-in-unity-2kn2)). Стандартная практическая рекомендация CI-сообщества — кешировать папку `Library/` между запусками джобы, чтобы избежать полного переимпорта на каждом прогоне.

## Источники

- [Unity Manual: Command-line arguments, 6000.3 (landing page)](https://docs.unity3d.com/6000.3/Documentation/Manual/CommandLineArguments.html)
- [Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)
- [Build a player from the command line, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/build-command-line.html)
- [Create a custom build script, 6000.5](https://docs.unity3d.com/6000.5/Documentation/Manual/build-script-build.html)
- [Unity Scripting API: BuildPipeline.BuildPlayer](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html)
- [Unity Scripting API: EditorApplication.Exit](https://docs.unity3d.com/ScriptReference/EditorApplication.Exit.html)
- [Why doesn't a failed BuildPipeline.BuildPlayer return an error code in the command line? — Unity Support Help Center](https://support.unity.com/hc/en-us/articles/211195263-Why-doesn-t-a-failed-BuildPipeline-BuildPlayer-return-an-error-code-in-the-command-line)
- [Option to remove -quit command, JetBrains/teamcity-unity-plugin#34](https://github.com/JetBrains/teamcity-unity-plugin/issues/34)
- [Manage your license through the command line, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)
- [Activation, GameCI (GitHub Actions)](https://game.ci/docs/github/activation/)
- [alf->ulf license activation no longer possible for personal licenses, game-ci/documentation#408](https://github.com/game-ci/documentation/issues/408)
- [GameCI Docker images for Unity](https://game.ci/docs/docker/docker-images/)
- [Customize GameCI Unity Docker images](https://game.ci/docs/docker/customize-docker-images/)
- [Multiple Unity instances cannot open the same project, Unity Discussions](https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project/607546)
- [Resolving "The project is currently open in the Unity Editor" Error — Unity Support Help Center](https://support.unity.com/hc/en-us/articles/40828087523092-Resolving-the-The-project-is-currently-open-in-the-Unity-Editor-Please-close-it-in-the-Editor-to-proceed-with-this-operation-Error)
- [Unity batchmode does no longer return exit code that could be captured by python, Unity Discussions](https://discussions.unity.com/t/unity-batchmode-does-no-longer-return-exit-code-that-could-be-captured-by-python/1698339)
- [Importing assets can be very slow, Unity Discussions](https://discussions.unity.com/t/importing-assets-can-be-very-slow/1716277)
- [Reducing assets import times in Unity, dev.to](https://dev.to/attiliohimeki/reducing-assets-import-times-in-unity-2kn2)
- [Unity Issue Tracker: Compilation error on first launch of Android batch build results in non-zero exit code](https://issuetracker.unity3d.com/issues/batch-mode-compilation-error-on-first-launch-of-android-batch-build-results-in-unity-closing-with-non-zero-exit-code)






