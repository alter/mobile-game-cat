# Сборочный конвейер Unity → Xcode → App Store для iOS

Дата сбора материала: 2026-08-24.
Версия стека проекта: Unity 6.3 LTS (6000.3.x), сборка под iOS, IL2CPP, распространение через TestFlight и App Store.

## Кратко

- Unity не собирает готовое приложение под iOS напрямую — она генерирует Xcode-проект (`Unity-iPhone.xcodeproj`), после чего сборку, архивирование, подпись и загрузку в App Store Connect должен выполнить Xcode или `xcodebuild`.
- IL2CPP переводит управляемый код C# в C++, который затем компилирует уже Xcode: «Unity generates C++ source files based on your C# scripts and places them in the generated Xcode project. Xcode then invokes the IL2CPP program which compiles the C++ source files into libraries.»
- Сгенерированный Xcode-проект содержит минимум три цели: `Unity-iPhone` (тонкий лаунчер и Info.plist), `UnityFramework` (рантайм, плагины, `PrivacyInfo.xcprivacy`) и статические библиотеки IL2CPP (`libGameAssembly.a`, `il2cpp.a`).
- Архивирование и экспорт в командной строке — `xcodebuild -archive`, затем `xcodebuild -exportArchive` с файлом `ExportOptions.plist`, где задаются `method`, `teamID`, `signingStyle`, `provisioningProfiles`.
- Для нотаризации `xcrun altool` не принимается сервисом нотаризации Apple с 1 ноября 2023 года — вместо него обязателен `notarytool`. Для загрузки самого `.ipa` в App Store Connect `altool` формально не «убит», но его ключ `--upload-app` помечен как deprecated в пользу `--upload-package`; на практике в CI чаще используют `xcodebuild -exportArchive` с встроенной выгрузкой либо Transporter.
- Начиная с Xcode 13, `xcodebuild` поддерживает аутентификацию через ключ App Store Connect API (`-authenticationKeyPath`, `-authenticationKeyID`, `-authenticationKeyIssuerID`) вместо интерактивного логина Apple ID — это стандарт для headless CI.
- Из C# можно модифицировать сгенерированный Xcode-проект через `[PostProcessBuild]` и класс `PBXProject` (`UnityEditor.iOS.Xcode`) — добавлять фреймворки, файлы, менять `Info.plist` (например, `NSCameraUsageDescription`).
- Реальный эффект на размер сборки дают: Strip Engine Code, IL2CPP managed stripping level (Low/Medium/High), сжатие текстур (ASTC/ETC2, Crunch), контроль содержимого папки `Resources`. Пустой проект Unity без оптимизаций — около 20 МБ в App Store, с оптимизациями — менее 12 МБ.
- Частые ошибки при переходе Unity → Xcode: `Undefined symbol` при добавлении сторонних native SDK (Firebase, Google Sign-In, Apple.GameKit), `Multiple commands produce ... Info.plist` при смене версии Xcode, сбои `PhaseScriptExecution`/code signing при апгрейде Xcode или CI-окружении.

## Как Unity 6.3 собирает под iOS

Официальная страница Unity Manual «How Unity builds iOS applications» для ветки 6000.3 (открыта через WebFetch) описывает процесс так:

> "Unity collects project resources, code libraries, and plug-ins from your Unity project and uses them to create a valid Xcode project."

Дальше про IL2CPP:

> "Unity generates C++ source files based on your C# scripts and places them in the generated Xcode project. Xcode then invokes the IL2CPP program which compiles the C++ source files into libraries."

И финальный шаг локальной сборки/запуска:

> "Xcode builds the project into a standalone application and deploys and launches it on a connected device or the Xcode simulator."

([Unity Manual — How Unity builds iOS applications (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/how-unity-builds-ios-applications.html))

Страница не описывает шаги архивирования, экспорта и загрузки в App Store Connect — это зона ответственности Xcode/`xcodebuild`, а не Unity, и описана в следующем разделе.

Что делать дальше с `Unity-iPhone.xcodeproj`: открыть проект в Xcode (или работать с ним через `xcodebuild` в CI), настроить подпись (Team, Bundle Identifier уже проставлены из Player Settings, но провижининг обычно нужно проверить/переопределить), при необходимости внести правки через `PostProcessBuild` (см. ниже), затем выполнить `Product → Archive` в Xcode либо эквивалентные команды `xcodebuild archive` / `xcodebuild -exportArchive` в терминале, и загрузить получившийся `.ipa` в App Store Connect.

По официальной странице структуры Xcode-проекта Unity (6000.2, открыта через WebFetch) сгенерированный проект содержит:

- **Unity-iPhone** — «a thin launcher part that runs the UnityFramework», включает папку `MainApp` с `Info.plist` и Launch Screen.
- **UnityFramework** — цель, производящая `UnityFramework.framework`: «the Unity runtime, Classes, UnityFramework, and Libraries folders, along with dependent frameworks» — сюда же попадает консолидированный `PrivacyInfo.xcprivacy`.
- **GameAssembly** — контейнер для C#-кода, транслированного в C++ через IL2CPP: статическая библиотека `libGameAssembly.a` (управляемый код, кросс-компилированный в C++) и `il2cpp.a` (рантайм IL2CPP).

Прочие сгенерированные файлы: сам `.xcodeproj`, папка `Classes` (`main.mm`, `UnityAppController.mm/h`), папка `Data` с сериализованными ассетами и .NET-сборками, папка `Libraries` с `libil2cpp.a`, иконки и launch screens. ([Unity Manual — Structure of a Unity Xcode project (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html))

## Команды xcodebuild, ExportOptions.plist, актуальный способ загрузки

Официальные страницы Apple по нотаризации и `altool`/`notarytool` (`developer.apple.com/documentation/technotes/tn3147-migrating-to-the-latest-notarization-tool`) построены на Swift-DocC и через WebFetch не открылись — только заголовок. Ниже — то, что подтверждено прямым открытием мана `altool` (зеркало на keith.github.io, официальный текст man-страницы Apple) и то, что взято из вторичных источников (community-разборы, форумы Apple Developer, форумы fastlane) с явной пометкой.

### Архивирование и экспорт (типовые команды из практики CI, не выдуманы — совпадают в нескольких независимых источниках)

```
xcodebuild -workspace Unity-iPhone.xcworkspace \
  -scheme Unity-iPhone \
  -configuration Release \
  -archivePath build/App.xcarchive \
  archive

xcodebuild -exportArchive \
  -archivePath build/App.xcarchive \
  -exportPath build/export \
  -exportOptionsPlist ExportOptions.plist
```

Если у проекта нет отдельного workspace (обычный сгенерированный Unity-проект без CocoaPods/SPM-workspace), используется `-project Unity-iPhone.xcodeproj` вместо `-workspace`.

### ExportOptions.plist

Ключевые поля по практике сообщества (метод, teamID, стиль подписи, соответствие профилей):

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key>
  <string>app-store-connect</string>
  <key>teamID</key>
  <string>YOUR_TEAM_ID</string>
  <key>signingStyle</key>
  <string>manual</string>
  <key>provisioningProfiles</key>
  <dict>
    <key>com.yourcompany.yourgame</key>
    <string>Your Provisioning Profile Name</string>
  </dict>
</dict>
</plist>
```

Важная деталь про значение ключа `method`: по сведениям из community-обсуждений, имя `app-store` в `ExportOptions.plist` для `xcodebuild -exportArchive` считается устаревшим в пользу `app-store-connect`; полный список принимаемых значений (`app-store-connect`, `release-testing`, `enterprise`, `debugging`, `developer-id`, `mac-application`, `validation`) выводится локальной командой `xcodebuild -help` — это самый надёжный способ проверить актуальный список именно для установленной версии Xcode 26, так как страница `xcodebuild -help` меняется от релиза к релизу и официальную DocC-страницу с этим списком через WebFetch открыть не удалось — рекомендация проверить командой на месте, а не полагаться на этот документ. Точный список ключей `ExportOptions.plist` для конкретной версии Xcode 26 официальной страницей Apple в этом исследовании не подтверждён — не проверено, взято из вторичных источников (Medium, Fritz.ai, matrixprojects.net, GitHub gist).

### altool → notarytool

Man-страница `altool` (открыта через WebFetch, зеркало официального текста Apple) прямо показывает: ключ `--upload-app` сопровождается пометкой «Can also be specified as --upload-app -f <file>», то есть является алиасом старого поведения; рекомендуемая современная форма — `--upload-package`. Дословного текста «deprecated» в самом открытом фрагменте не обнаружено — вывод о статусе deprecated сделан по вторичным источникам (форумы Apple Developer, обсуждение fastlane), которые цитируют предупреждение Apple: «altool has been deprecated for notarization and starting in fall 2023 will no longer be supported by the Apple notary service. You should start using notarytool to notarize your software.» ([altool man page (зеркало)](https://keith.github.io/xcode-man-pages/altool.1.html))

Отдельно и с более высокой уверенностью подтверждён факт полного прекращения приёма нотаризации через `altool`: по нескольким независимым вторичным источникам, пересказывающим официальный технот TN3147, — «starting November 1, 2023, the Apple notary service no longer accepts uploads from altool or Xcode 13 or earlier — developers who notarize Mac software need to transition to the notarytool command-line utility or upgrade to Xcode 14 or later.» Саму официальную страницу TN3147 открыть через WebFetch не удалось (SPA), поэтому дата и формулировка помечены как «подтверждено по нескольким независимым вторичным источникам, не по первоисточнику напрямую».

Важное разграничение: нотаризация (`notarytool`) актуальна прежде всего для macOS-приложений/бинарников вне App Store и для сценариев Developer ID; для игры, которая распространяется через App Store/TestFlight, ключевой процесс — не нотаризация, а именно загрузка `.ipa` в App Store Connect. Актуальный на 2026 год способ загрузки — либо `xcodebuild -exportArchive` с ключом `method` `app-store-connect` и последующей автоматической выгрузкой (destination upload через `-exportOptionsPlist`/`-allowProvisioningUpdates`), либо через приложение **Transporter** (доступно в Mac App Store), либо командой `xcrun altool --upload-package` (замена устаревающего `--upload-app`). Ни `xcodebuild`, ни `altool --upload-package`, ни Transporter в этом исследовании не проверялись по официальной DocC-странице Apple напрямую — весь раздел про актуальность именно этого способа загрузки в 2026 году основан на вторичных источниках и требует проверки локальным `xcodebuild -help` / `man altool` на актуальной версии Xcode 26 перед использованием в CI.

## Подпись: сертификаты, provisioning profile, автоматическая подпись в CI, API Key

Базовый механизм подписи iOS-приложений не менялся годами: приложение подписывается сертификатом распространения (Apple Distribution / iOS Distribution) и упаковывается с provisioning profile, который связывает App ID, сертификат и (для ad-hoc/enterprise) список устройств. В `ExportOptions.plist` это выражается через `signingStyle` (`manual` или `automatic`) и, при ручной подписи, через словарь `provisioningProfiles`, сопоставляющий bundle identifier конкретному имени профиля — в том числе отдельные записи нужны для расширений приложения (виджеты и т. п.), если они есть.

Для CI/CD вместо интерактивного входа Apple ID используется ключ App Store Connect API. По сведениям из практики сообщества (официальная DocC-страница `xcodebuild` через WebFetch не открылась): начиная с Xcode 13 `xcodebuild` поддерживает аутентификацию ключом API вместо Apple ID, что и даёт возможность делать автоматическую подпись на headless-машинах и в CI. Ключ создаётся в App Store Connect, ему назначается роль, ограничивающая права; в командной строке передаётся тремя параметрами:

```
xcodebuild -exportArchive \
  -archivePath build/App.xcarchive \
  -exportPath build/export \
  -exportOptionsPlist ExportOptions.plist \
  -authenticationKeyPath /path/to/AuthKey_XXXXXXXXXX.p8 \
  -authenticationKeyID XXXXXXXXXX \
  -authenticationKeyIssuerID your-issuer-id \
  -allowProvisioningUpdates
```

Про надёжность связки API Key + `-allowProvisioningUpdates` для самого шага выгрузки (destination `upload`) в `ExportOptions.plist`, включая работу с `manageAppVersionAndBuildNumber`, встречаются сообщения сообщества о частичных ограничениях в отдельных версиях Xcode 15 — эти детали не проверены по официальной документации Apple напрямую и приводятся только как контекст, требующий проверки на конкретной версии Xcode 26 перед тем, как полагаться на них в продовом CI.

Приватный файл ключа (`.p8`) — секрет уровня «нельзя коммитить», хранить в секретах CI (например, зашифрованным в переменных окружения раннера), а не в репозитории.

При падении `-exportArchive` в CI практический совет из вторичных источников — смотреть `IDEDistribution.log` и `IDEDistribution.critical.log` в папке DerivedData соответствующего архива: сообщение об ошибке от самого `xcodebuild` часто неинформативно, а подробности линковки/подписи попадают именно в эти логи.

## PostProcessBuild на C#: правка Info.plist и PBXProject

Официальная страница Unity Scripting API `PBXProject` (открыта через WebFetch) подтверждает наличие методов `AddFrameworkToProject`, `AddFileToBuild`, `GetUnityFrameworkTargetGuid()`, `GetUnityMainTargetGuid()`, `SetBuildProperty`, `AddBuildProperty` в пространстве имён `UnityEditor.iOS.Xcode`; страница структуры Xcode-проекта (6000.2) прямо называет их применение: «you can use PBXProject.GetUnityFrameworkTargetGuid() to get the UnityFramework target GUID and PBXProject.GetUnityMainTargetGuid() to get the Unity-iPhone target GUID» при написании модификаций сгенерированного проекта. ([Unity Scripting API — PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html), [Unity Manual — Structure of a Unity Xcode project (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html))

Рабочий пример (собран из документированных вызовов Unity API `PBXProject` и `PlistDocument`, а не выдуман — сигнатуры методов соответствуют официальной странице Unity Scripting API; сама компоновка примера — типовой паттерн, применяемый в проектах Unity для iOS):

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public class IOSPostProcess
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        // --- 1. Правка Info.plist: описания доступа к камере и фотоплёнке ---
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict rootDict = plist.root;
        rootDict.SetString("NSCameraUsageDescription",
            "Камера используется, чтобы сделать снимок для игрового эффекта.");
        rootDict.SetString("NSPhotoLibraryUsageDescription",
            "Доступ к фотоплёнке нужен, чтобы выбрать изображение для игры.");
        rootDict.SetString("NSPhotoLibraryAddUsageDescription",
            "Разрешение нужно, чтобы сохранить результат в фотоплёнку.");

        plist.WriteToFile(plistPath);

        // --- 2. Правка PBXProject: добавление фреймворка и файла Swift ---
        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        string mainTargetGuid = project.GetUnityMainTargetGuid();
        string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

        // Добавить системный фреймворк в цель UnityFramework
        project.AddFrameworkToProject(frameworkTargetGuid, "CoreImage.framework", false);

        // Скопировать и добавить свой файл Swift в проект/цель
        string sourceSwiftFile = Path.Combine(Application.dataPath, "Plugins/iOS/CameraBridge.swift");
        string destSwiftFile = Path.Combine(pathToBuiltProject, "Libraries/CameraBridge.swift");
        File.Copy(sourceSwiftFile, destSwiftFile, true);

        string fileGuid = project.AddFile(
            "Libraries/CameraBridge.swift",
            "Libraries/CameraBridge.swift",
            PBXSourceTree.Source);
        project.AddFileToBuild(frameworkTargetGuid, fileGuid);

        // Обязательные настройки для смешивания Swift и Objective-C/IL2CPP
        project.SetBuildProperty(mainTargetGuid, "SWIFT_VERSION", "5.0");
        project.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

        project.WriteToFile(projectPath);
    }
}
```

Пояснения по коду (со ссылкой на подтверждённые официальные методы):

- `PBXProject.GetPBXProjectPath(pathToBuiltProject)` — стандартный способ получить путь к `project.pbxproj` внутри сгенерированного `pathToBuiltProject`.
- `GetUnityMainTargetGuid()` / `GetUnityFrameworkTargetGuid()` — прямо документированы и используются именно для различения целей `Unity-iPhone` и `UnityFramework`, куда обычно и нужно добавлять нативный код и фреймворки.
- `PlistDocument`/`PlistElementDict` — часть той же библиотеки `UnityEditor.iOS.Xcode`, используется для правки `Info.plist` (описания доступа к камере/фотоплёнке нельзя задать через обычные Player Settings — только через код или руками в Xcode, если у поля нет соответствующей настройки в Unity 6.3).
- Для файлов Swift обязательно выставить `SWIFT_VERSION` и включить `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES`, иначе линковка упадёт с ошибками про отсутствующие рантайм-библиотеки Swift.

Важное предупреждение, не подтверждённое прямым WebFetch (встретилось в агрегированной выдаче поиска со ссылкой на документацию Unity, саму страницу с этой формулировкой отдельно не открывал): Unity использует инкрементальный конвейер при генерации Xcode-проекта для iOS и инкрементально пересоздаёт такие файлы, как `Info.plist` и Entitlements — если `PostProcessBuild`-скрипт их модифицирует, при повторных инкрементальных сборках правки могут накладываться на уже частично изменённый файл. Это стоит проверить самостоятельно по разделу Unity Manual про «чистые сборки» (Creating clean builds) перед тем, как полагаться на этот нюанс в CI — помечаю как не проверено напрямую.

## Уменьшение размера сборки iOS

Официальная страница Unity Manual «Optimizing the size of the built iOS Player» (ветка 6000.0, открыта через WebFetch) даёт конкретные цифры и рекомендации:

> "an empty project might be around 20MB in the App Store" (без оптимизаций); "an application containing an empty scene can be reduced to less than 12MB in the App Store" (с применёнными оптимизациями, при условии, что приложение упаковано и получило DRM, как это делает сам App Store).

Рекомендации той же страницы: включить «Strip Engine Code» в Player Settings для iOS; выставить «script call optimization level to Fast but no exceptions»; «enable compression for textures and minimize the number of uncompressed sounds»; выставить «API Compatibility Level to .Net Standard»; убрать лишние зависимости кода и избегать сочетания generic-контейнеров со value-типами/структурами. ([Unity Manual — Optimizing the size of the built iOS Player (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/iphone-playerSizeOptimization.html))

Managed stripping level — официальная страница Unity Manual «Managed code stripping» / «Configure managed code stripping» (6000.3, открыта через WebFetch) описывает уровни так:

- **Disabled** — «Unity doesn't remove any code. This setting is only available for the Mono scripting backend and is the default setting in that case.»
- **Minimal** — «Unity searches only the UnityEngine and the .NET class libraries for unused code. Unity doesn't remove any user-written code.»
- **Low** — «Unity searches for unused code in all UnityEngine and .NET class libraries. It also searches user-written assemblies, but only if none of their types are referenced in scenes included in the Player build.»
- **Medium** — «Unity partially searches all assemblies to find unused code. This setting applies a set of rules that strips more types of code patterns to reduce the build size.»
- **High** — «Unity performs an extensive search of all assemblies to find unused code. At this setting, Unity prioritizes size reduction more than code stability and removes as much code as possible.»

([Unity Manual — Configure managed code stripping (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping-configure.html))

Практический вывод: для IL2CPP-бэкенда (обязателен для iOS в Unity 6.3 — Mono для iOS Apple не разрешает как рантайм с JIT) байткод-стриппинг «always» происходит независимо от уровня — по данным вторичного источника (Unity Support Help Center) IL2CPP всегда делает byte code stripping вне зависимости от настройки Stripping Level, но managed stripping level дополнительно решает, насколько агрессивно вырезается неиспользуемый managed-код; для игровых проектов обычно рекомендуют Medium или High, но High требует внимательного тестирования — агрессивный стриппинг может вырезать код, до которого достаёт только рефлексия, и такие случаи приходится чинить через `link.xml`. Этот совет про Medium/High и риск с рефлексией — по вторичным источникам, не по официальной странице Apple/Unity напрямую в данном исследовании.

Что реально даёт эффект (сведено из официальной страницы Unity + вторичных источников с пометкой):

- **Strip Engine Code + managed stripping High** — заметно уменьшает размер `libGameAssembly.a`/`il2cpp.a`, официально подтверждено страницей оптимизации размера.
- **Формат текстур ASTC/ETC2 вместо несжатых или PVRTC** — согласно вторичным источникам, текстуры обычно занимают основную долю размера сборки; официальная страница Unity лишь в общем виде говорит «enable compression for textures», конкретные форматы ASTC/ETC2 и цифры выигрыша — по вторичным источникам, не проверено напрямую по документации Apple/Unity в этом исследовании.
- **Crunch Compression** — по вторичным источникам даёт выигрыш в размере на диске, но не поддерживается частью старых устройств, и после распаковки в память текстура становится полностью несжатой — то есть не экономит оперативную память во время выполнения, только размер дистрибутива.
- **App thinning на стороне Apple** — по вторичным источникам, App Store сам нарезает бинарник на срезы под архитектуру устройства (начиная с iOS 9), из-за чего фактический размер загрузки для конкретного устройства меньше суммарного архива; App Store также шифрует и сжимает бинарник при обработке, что может временно увеличивать промежуточный размер перед сжатием — эти механики не проверялись напрямую по официальной странице Apple в этом исследовании.
- **Aудит папки `Resources`** — по вторичным источникам, все ассеты в `Resources` включаются в сборку полностью независимо от того, используются ли они по факту, и это частая скрытая причина раздутого размера; официальная страница Unity Manual об этом факте прямо в открытом фрагменте не говорит, но общая рекомендация «удалять неиспользуемые ассеты» согласуется с этим.

## Типовые ошибки сборки Unity → Xcode (по отчётам разработчиков)

Ниже — разбор по вторичным источникам (форумы Apple Developer, обсуждения сообщества); официальных страниц Apple/Unity с исчерпывающим списком таких ошибок в этом исследовании не найдено, поэтому весь раздел — «по вторичным источникам, не первоисточник».

**Undefined symbol при линковке.** Частый сценарий — добавление стороннего нативного SDK (Firebase, Google Sign-In, Facebook SDK, Apple.GameKit для Unity) в проект, уже содержащий сгенерированный Unity Xcode-проект: линкер не находит символы вроде `_GKLocalPlayer_Authenticate` из сгенерированных `.o`-файлов плагина. По разбору с форумов Apple Developer, для связки Unity 6000.2.7f + Xcode 26.1 встречалась отдельная категория — неразрешённые символы совместимости Swift (`_swift_FORCE_LOAD$_swiftCompatibility51`, `_swift_FORCE_LOAD$_swiftCompatibility56`, `_swift_FORCE_LOAD$_swiftCompatibilityConcurrency`), связанная с отсутствующими фреймворками совместимости Swift (`CoreAudioTypes`, `UIUtilities`) при смешивании управляемого кода IL2CPP и Swift-плагинов. Разбор такого рода ошибок обычно требует смотреть не сообщение самого Xcode, а полный транскрипт сборки (View → Navigators → Reports), потому что Xcode плохо показывает настоящую команду линковки и её вывод при таких сбоях.

**«Multiple commands produce ... Info.plist».** Классическая ошибка при апгрейде версии Xcode: у цели `Unity-iPhone` одновременно оказываются команда копирования и команда обработки, пишущие в один и тот же выходной файл `Info.plist`. Старый обходной путь — переключение на Legacy Build System — по отчётам с форумов Apple Developer, для более новых версий Xcode (начиная примерно с 13.2) это больше не работает и не рекомендуется; типичное решение — чистая пересборка Xcode-проекта Unity (не инкрементальная) и явная проверка, что кастомный `PostProcessBuild`-скрипт не создаёт свою копию `Info.plist` вдобавок к стандартной генерации Unity.

**Ошибки подписи при архивировании.** По отчётам сообщества, отдельная категория сбоев — падение именно на этапе `codesign`/`validate` при архивировании в CI (например, в Jenkins), которое не воспроизводится локально на машине разработчика; это обычно означает рассинхронизацию сертификата/provisioning profile именно в CI-окружении (кэш keychain, устаревший профиль, не тот Team ID), а не ошибку самого Unity-проекта.

**«Command PhaseScriptExecution failed with a nonzero exit code».** По отчётам сообщества, всплывает при смене версии Xcode/iOS SDK без соответствующего обновления Unity (например, Xcode 15 + iOS 17 при устаревшей версии Unity); практические обходные пути из тех же отчётов — полная чистая пересборка, использование нативной (Apple Silicon) версии Unity на M1/M2/M3-Mac вместо Intel-сборки под Rosetta, и в отдельных случаях — добавление флага `-ld64` в Other Linker Flags цели.

Общая рекомендация DTS-инженеров Apple (по пересказу с форумов, не по официальной документации напрямую): ошибки линковки — это ошибки линкера, а не компилятора, и Xcode часто плохо их показывает в основной панели проблем — нужно открывать полный build transcript, чтобы увидеть настоящую команду и настоящее сообщение об ошибке.

## Источники

- [Unity Manual — How Unity builds iOS applications (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/how-unity-builds-ios-applications.html)
- [Unity Manual — Structure of a Unity Xcode project (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html)
- [Unity Scripting API — PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html)
- [Unity Manual — Optimizing the size of the built iOS Player (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/iphone-playerSizeOptimization.html)
- [Unity Manual — Managed code stripping (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping.html)
- [Unity Manual — Configure managed code stripping (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping-configure.html)
- [altool man page (зеркало официального текста Apple)](https://keith.github.io/xcode-man-pages/altool.1.html)
- [Apple Developer Forums — Unity build error in Xcode: Undefined Symbols](https://developer.apple.com/forums/thread/808610)
- [Apple Developer Forums — Xcode error: undefined symbol (Link unityframework arm64) 100 errors](https://developer.apple.com/forums/thread/747089)
- [Apple Developer Forums — Solution for multiple commands produce in Xcode 13.2](https://developer.apple.com/forums/thread/699362)
- [Apple Developer Forums — Xcode 15, iOS17 and unity 2022 problems: PhaseScriptExecution failed](https://developer.apple.com/forums/thread/740210)

Страницы, которые не удалось открыть содержательно через WebFetch (Swift-DocC/JS-рендеринг — отдавали только заголовок), и поэтому факты по ним взяты из вторичных источников с явной пометкой в тексте: `developer.apple.com/documentation/technotes/tn3147-migrating-to-the-latest-notarization-tool`, официальные страницы `xcodebuild` и `ExportOptions.plist` в разделе документации Xcode.

Вторичные источники, использованные для контекста (не первоисточник, помечены в тексте отдельно): GitHub fastlane discussion #21347 (altool deprecation), Bitrise/Capgo/Xojo блоги про privacy manifest, Unity Support Help Center (IL2CPP build size optimizations), обсуждения на Reddit/StackOverflow/форумах, агрегированные через веб-поиск.
