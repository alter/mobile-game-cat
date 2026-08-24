# Unity Test Framework: тестирование ядра правил на C#

Дата сбора: 2026-08-24. Версия стека: Unity 6.3 LTS (6000.3.x), C#, .NET Standard 2.1.

## Кратко

- Test Framework (UTF) в Unity 6.3 — пакет `com.unity.test-framework`; на странице мануала 6000.3 явных цифр версии пакета не приводится, страница описывает его как «Test framework for running Edit mode and Play mode tests in Unity» ([Unity Manual: Test Framework, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)). В реальном проекте на Unity 6.x (Unity-Technologies/com.unity.multiplayer.samples.coop) в `manifest.json` зафиксирована версия `1.5.1` ([manifest.json](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Packages/manifest.json)); точную версию, которую ставит именно 6000.3 «из коробки», нужно проверять в конкретном проекте — надёжного единого источника с привязкой «6000.3 → версия X.Y.Z» не найдено.
- EditMode-тесты выполняются только в редакторе и не поддерживают корутины; PlayMode-тесты умеют работать как корутины через атрибут `[UnityTest]` ([Edit Mode vs Play Mode tests, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).
- Тестовую сборку опознаёт не имя папки, а набор ссылок: assembly, ссылающаяся на `nunit.framework.dll` (и, для EditMode, дополнительно на `UnityEngine.TestRunner`/`UnityEditor.TestRunner`), становится «Test Assembly» ([там же](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).
- Чистое ядро без `UnityEngine` выделяется через `.asmdef` без ссылки на `UnityEngine`/`UnityEditor`; для реального переиспользования кода вне Unity разработчики используют либо линковку исходников через `<Compile Include>` в отдельном `.csproj`, либо построение отдельной netstandard-библиотеки с постройкой `.dll`.
- NUnit внутри UTF — не ванильный NuGet-пакет, а `com.unity.ext.nunit`, «Based on NUnit version 3.5» ([Custom NUnit manual, 2.0](https://docs.unity3d.com/Packages/com.unity.ext.nunit@2.0/manual/index.html)); для `[Test]` доступны `[TestCase]`, `[Values]`, `[TestCaseSource]`, а для `[UnityTest]` (корутинных тестов) поддерживается только `[ValueSource]` — `[TestCase]` официально не поддержан ([Parameterized tests, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)).
- Property-based тестирование в чистом NUnit-проекте реально работает через `FsCheck.NUnit` (F#-ориентированная библиотека с атрибутом `[Property]`) или через C#-библиотеку CsCheck; официальных отчётов о запуске именно внутри Unity Test Framework не найдено — но обе библиотеки представляют собой обычные .NET-сборки без специфичных для Unity зависимостей, и старые версии CsCheck (до 3.x) собраны под `netstandard2.0`, что совместимо с профилем Unity .NET Standard 2.1.
- Пакет Code Coverage (`com.unity.testtools.codecoverage`) запускается из batchmode ключами `-enableCodeCoverage`, `-coverageResultsPath`, `-coverageHistoryPath`, `-coverageOptions`, `-debugCodeOptimization`; встроенного порога («fail if coverage < N%») в самом пакете нет — это подтверждено отсутствием такого ключа в официальной документации batchmode ([Using Code Coverage in batchmode, 1.2](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)).
- Тесты из командной строки запускаются ключами `-runTests -testPlatform -testResults` поверх `-batchmode -projectPath`; при падении хотя бы одного теста Unity возвращает код завершения 2, при полном прохождении — 0 ([обсуждение на GitHub, Dinomite-Studios/unity-azure-pipelines-tasks#167](https://github.com/Dinomite-Studios/unity-azure-pipelines-tasks/issues/167)); отдельно от «упавших тестов» стоит «упавшая сборка/компиляция», которую нужно смотреть в логе, а не по коду возврата, так как единого соглашения по кодам возврата у самой Unity нет ([Running tests from the command line, 2.0.1-exp.2](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html)).

## 1. Unity Test Framework: устройство, EditMode против PlayMode

Test Framework — пакет `com.unity.test-framework`, предназначенный для запуска Edit mode и Play mode тестов в Unity: «Test framework for running Edit mode and Play mode tests in Unity» ([Unity Manual: Test Framework, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)). Страница мануала для версии Editor 6000.3 не приводит номер версии пакета явно — она лишь ссылается на подробный манул пакета. Согласно поиску по официальному changelog пакета на GitHub (зеркало `needle-mirror/com.unity.test-framework`), самая свежая запись на момент сбора — `[1.4.6] - 2025-02-03`, и упоминаний «6000.3»/«6.3» в changelog нет ([CHANGELOG.md](https://github.com/needle-mirror/com.unity.test-framework/blob/master/CHANGELOG.md)). Отдельно существует более новая экспериментальная линейка `2.0.1-exp.2`, чья документация используется ниже для описания командной строки ([Test Framework command line arguments, 2.0](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html)). Итог: точная версия test-framework, идущая «по умолчанию» именно в 6000.3, не подтверждена ни одним найденным источником — при настройке проекта её нужно посмотреть в `Packages/manifest.json` конкретной установки.

**EditMode тесты.** «Edit mode tests (also known as Editor tests) only run in the Unity Editor and have access to Editor code and runtime application code.» Ограничение: «You can't run coroutines in Edit mode tests.» ([Edit Mode vs Play Mode tests, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).

Требование к assembly: «Edit mode tests must have an assembly definition that references nunit.framework.dll and have the Editor as their only target platform» ([там же](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).

**PlayMode тесты.** «Play mode tests allow you to test your runtime application code, and the tests run as coroutines if marked with the [UnityTest] attribute.» Требование к assembly: «Tests must have their own assembly definition with a reference to nunit.framework.dll. Test scripts must be in a folder alongside the .asmdef file.» Для PlayMode `includePlatforms` в asmdef должен быть пустым массивом (`[]`) ([там же](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).

Важное общее ограничение для обоих режимов: «Your test assembly can't reference the predefined Assembly-Csharp.dll assembly. You must move code you want to test into a custom assembly» ([там же](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)). Рекомендация по выбору атрибута: использовать `[Test]`, «unless you need to yield instructions for the Editor in Edit mode tests» или «skip a frame or wait for a certain amount of time in Play mode tests» ([там же](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).

Формально Unity определяет тестовую сборку так: «Unity automatically identifies any assembly as a test assembly if it has an assembly reference to nunit.framework.dll and assembly definition references to UnityEngine.TestRunner and UnityEditor.TestRunner» — при этом ссылка на `UnityEditor.TestRunner` актуальна только для EditMode ([Create a test assembly, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/workflow-create-test-assembly.html)).

Практический способ создать тестовую сборку: через окно Test Runner (`Window > General > Test Runner`, пункт «Create a new Test Assembly Folder in the active path») либо через меню `Assets > Create > Testing > Test Assembly Folder`. В результате создаётся подпапка `Tests` с файлом `.asmdef`, который содержит три ссылки: `nunit.framework.dll`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`. По умолчанию Platforms ограничены только Editor; выбор других платформ включает прогон PlayMode-тестов на билде плеера ([Create a test assembly, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/workflow-create-test-assembly.html)).
## 2. Assembly Definition (.asmdef): выделение чистой C#-сборки и тестовой сборки

Полная схема JSON-файла `.asmdef` включает поля `name`, `references`, `includePlatforms`, `excludePlatforms`, `allowUnsafeCode`, `overrideReferences`, `precompiledReferences`, `autoReferenced`, `defineConstraints`, `versionDefines`, `noEngineReferences` ([Assembly Definition File Format reference](https://docs.unity3d.com/Manual/assembly-definition-file-format.html)).

Чтобы ядро правил не зависело от `UnityEngine`, у его `.asmdef` просто не должно быть ссылок на `UnityEngine`/`UnityEditor` в `references` — Unity не требует их наличия принудительно; зависимость появляется только если сам код использует `UnityEngine`-типы. Поле `noEngineReferences` управляет отдельной настройкой «No Engine References», которая явно запрещает сборке ссылаться на модули движка.

**Auto Referenced.** «Specify whether this assembly is automatically referenced by Unity's predefined assemblies. When disabled, Unity does not automatically reference the assembly during compilation» ([Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)). В `.asmdef` это поле `autoReferenced`, по умолчанию `true` ([Assembly Definition File Format reference](https://docs.unity3d.com/Manual/assembly-definition-file-format.html)). Отключение полезно для тестовых/служебных сборок, которые не должны неявно попадать в зависимости `Assembly-CSharp.dll` — по практическому наблюдению это «often unneeded and may even increase build times if you don't have code outside your own assemblies» (наблюдение из практического руководства по asmdef, не из официального мануала).

**Define Constraints.** «Define constraints specify the scripting symbols that must be defined in your project for Unity to compile or reference an assembly. All the listed symbols must be defined for the assembly to compile» ([Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)). Развёрнутое объяснение логики: «Unity only compiles and references a Project assembly if all the Define Constraints are satisfied, and constraints work like the #if preprocessor directive in C#, but on the assembly level instead of the script level»; символ можно инвертировать префиксом `!` (constraint выполняется, если символ НЕ определён) ([Unity Manual: Assembly Definition properties / define constraints](https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html)).

`UNITY_INCLUDE_TESTS` — предопределённый символ, который Unity автоматически определяет при компиляции тестовых сборок; типичный пример из официального формата файла:

```json
{
    "name": "BeeAssembly",
    "references": [
        "Unity.CollabProxy.Editor",
        "AssemblyB",
        "UnityEngine.UI",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Android", "LinuxStandalone64", "WebGL"],
    "excludePlatforms": [],
    "overrideReferences": true,
    "precompiledReferences": ["Newtonsoft.Json.dll", "nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_2019", "UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

([Assembly Definition File Format reference](https://docs.unity3d.com/Manual/assembly-definition-file-format.html)). На практике для EditMode/PlayMode тестовых сборок используется схема вида: EditMode-сборка — `"includePlatforms": ["Editor"]`, PlayMode-сборка — `"includePlatforms": []` (пусто означает «все платформы»), у обеих — ссылка на `nunit.framework.dll` (см. раздел 1).

**Ссылка на NUnit.** Тестовая сборка опознаётся по ссылке на `nunit.framework.dll` в `precompiledReferences`/`references` — «This combination of references is what identifies an assembly as a test assembly» (в сочетании с `UnityEngine.TestRunner`/`UnityEditor.TestRunner`) ([Create a test assembly, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/workflow-create-test-assembly.html)).

**Assembly Definition References** — список ссылок на другие `.asmdef`: «A list of assemblies to reference from the current assembly. Click the + button to add a new reference» ([Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)). Чтобы тестовая сборка «увидела» классы ядра, ей нужна явная Assembly Definition Reference на asmdef ядра — «you need to add an Assembly Definition Reference for your unit testing class to "see" the class under test, since the tested class belongs to a separate Game Assembly Definition (asmdef) file» (сообщество, GameDev Dustin, [Unit testing classes from other assemblies with Unity's Test Framework](https://gamedevdustin.medium.com/unit-testing-classes-from-other-assemblies-with-unitys-test-framework-in-unity-2022-820b3e3486fb)).

**Use GUIDs** — настройка, определяющая, как Unity сериализует ссылку на другой asmdef: «When you enable this property, Unity saves the reference as the asset's GUID, instead of the Assembly Definition name» ([Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)).

## 3. Компиляция ядра вне Unity: реальные приёмы разработчиков

Базовое наблюдение сообщества: «most C# code will be source-compatible between a Unity project and a standalone .NET C# project — if you copy-and-paste the source code from one to the other, it will almost certainly compile and run as expected», при условии что используемые API входят в .NET Standard; «Not all of the .NET Standard is available to Unity», а IL2CPP-платформы «only supports a subset of the .NET Standard» ([Sharing C# Code with Unity, randomPoison](https://randompoison.github.io/posts/sharing-with-unity/)).

**Приём 1 — линковка исходников в отдельный .csproj (без копирования).** Рабочий пример из практики: рядом с Unity-проектом заводится папка (например `HeadlessTests`) с отдельным `.csproj`, который через `<Compile Include>` подключает исходники ядра из `Assets` напрямую, без копирования:

```xml
<ItemGroup>
  <!-- Include ALL Runtime scripts -->
  <Compile Include="..\Assets\!_Project\Scripts\Runtime\**\*.cs" LinkBase="Runtime" />

  <!-- Include all Tests -->
  <Compile Include="..\Assets\!_Project\Scripts\Tests\**\*.cs" LinkBase="Tests" />
</ItemGroup>
```

Автор поясняет мотивацию: «We don't copy the files; we link them. This ensures that when we modify a file in the test project, we are actually modifying the Unity asset.» Если тестируемый код всё же трогает `UnityEngine`-типы (например через `Physics.Raycast`), в `.csproj` добавляют прямую ссылку на управляемые сборки движка по `HintPath`:

```xml
<Reference Include="UnityEngine.CoreModule">
  <HintPath>D:\ProgramFiles\UnityHub\Editor\6000.3.2f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll</HintPath>
</Reference>
```

Запуск — обычный `dotnet test`. Явное предупреждение: «Since the Unity Native Backend isn't running, calls to things like `Physics.Raycast` or `Time.deltaTime` might not work as expected. This setup is strictly for logic testing.» Итоговая рекомендация: «Keep your core logic independent of Unity so it can run in a normal .NET environment» ([Run Unity Tests 10x Faster with .NET](https://gamedev.center/run-unity-tests-faster-dotnet/)).

**Приём 2 — отдельная netstandard-библиотека вне Unity с копированием .dll.** Схема: пишется независимая библиотека под netstandard, собирается вне Unity, и результат публикуется прямо в `Assets/Plugins` пост-сборочным шагом, например `dotnet publish -c Release -o ../UnityProject/Assets/Plugins`. Для предсказуемого набора зависимостей в `.csproj` включают `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` ([Sharing C# Code with Unity, randomPoison](https://randompoison.github.io/posts/sharing-with-unity/)). Риски этого пути: конфликт версий зависимостей — «if you were two have two different projects both depending on `SomePackage`, each one would pull a copy of `SomePackage.dll` into your Unity project and your project will fail to build», а также «полировка» проекта Unity-специфичными файлами — «polluting your shared code project with Unity-specific details» из-за `.meta`-файлов, которые Unity генерирует для каждого ассета и которые «clutter the files list in your editor» ([там же](https://randompoison.github.io/posts/sharing-with-unity/)).

**Приём 3 — managed plug-in.** Официальный путь Unity: «Managed plug-ins are .NET assemblies you create and compile outside of Unity, into a dynamically linked library (DLL) with tools such as Visual Studio» — этот способ применим, «if the DLL does not contain Unity API code» ([Unity Manual: Managed plug-ins, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-ins-managed.html)).

**Приём 4 — инструмент CsprojToAsmdef.** Стороннее сообщество-решение генерирует `.asmdef` из `.csproj`, позволяя «use project references from outside or inside of your Unity project», «add NuGet packages», «support Roslyn analyzers» и собирать код Unity через .NET CLI. Настройка: установить `dotnet tool install -g CsprojToAsmdef.Cli`, создать Class Library на `.NET Standard 2.0` внутри `Assets`, подключить к solution выше корня Unity-проекта, сгенерировать `.asmdef`. Из документированных ограничений: «VSTU is not supported yet», отладка требует ручного подключения Unity-дебаггера, «ReSharper (and possibly Rider) works partially», для CI «Requires a Unity installation», свойство `VersionDefines` при генерации `.asmdef` не поддерживается ([KuraiAndras/CsprojToAsmdef, README](https://github.com/KuraiAndras/CsprojToAsmdef)).

Общий вывод по всем приёмам: чтобы ядро гарантированно компилировалось и вне Unity, и внутри — держать его код в отдельном `.asmdef` без ссылок на `UnityEngine`/`UnityEditor`, ограничиваться API уровня .NET Standard 2.1, и собирать/тестировать его отдельным `dotnet test`/`dotnet build` поверх `.csproj`, который либо линкует те же `.cs`-файлы (`<Compile Include>`), либо ссылается на тот же asmdef как на project reference.

## 4. NUnit внутри Unity: версия, атрибуты, ограничения

Unity не использует ванильный NuGet-пакет NUnit — вместо этого поставляется собственный пакет `com.unity.ext.nunit`, описанный как «A custom version of NUnit used by Unity Test Framework», «Based on NUnit version 3.5 and works with all platforms, il2cpp and Mono AOT» ([Custom NUnit manual, 2.0.5](https://docs.unity3d.com/Packages/com.unity.ext.nunit@2.0/manual/index.html)). То есть база — NUnit 3.5, адаптированная под AOT/il2cpp-ограничения Unity; какая именно версия `com.unity.ext.nunit` идёт с 6000.3 — не проверено (страница пакета не указывает привязку к версии Editor).

**Поддерживаемые атрибуты для `[Test]`** — полный набор стандартного NUnit: `[Test]`, `[TestCase]` (параметризация фиксированным набором значений), `[Values]`, `[TestCaseSource]`, `[ValueSource]` ([15. Test cases, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/course/test-cases.html); [Parameterized tests, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)).

**Ограничение для `[UnityTest]`** (корутинных, PlayMode/EditMode-со-yield тестов): «Regular NUnit tests support both the `[TestCase]` and `[ValueSource]` attributes for parameterized tests. Unity tests only support `ValueSource`» ([Parameterized tests, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)). Пример из документации:

```csharp
static int[] values = new int[] { 1, 5, 6 };

[UnityTest]
public IEnumerator MyTestWithMultipleValues([ValueSource("values")] int value)
{
    yield return null;
}
```

([там же](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)). Дополнительно доступен `ParameterizedIgnoreAttribute`, позволяющий «selectively ignore tests based on the parameters supplied to the test method» ([там же](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)).

`Assert.That` работает как обычный NUnit-констрейнт-синтаксис (`Assert.That(actual, Is.EqualTo(expected))` и т. п.) — прямых официальных ограничений на использование `Assert.That` в EditMode/PlayMode тестах в найденных источниках не описано, кроме общего требования не использовать корутины в EditMode-тестах (раздел 1) и известного ограничения на асинхронные тесты: ожидание корутины внутри `async`-теста в режиме EditMode может привести к зависанию теста, поскольку «coroutine scheduler» редактора не обрабатывает yield во время выполнения EditMode-тестов (сообщество, [nowsprinting/UnityTestExamples, Asynchronous Testing](https://deepwiki.com/nowsprinting/UnityTestExamples/4.3-async-setup-and-teardown)) — это утверждение из стороннего описания, не из официального мануала, и его стоит считать «не проверено по первоисточнику Unity».

## 5. Property-based тестирование в C#: применимость внутри Unity

**FsCheck.NUnit.** FsCheck — .NET-библиотека для property-based тестирования, «whose generator combinators can be used in any testing framework», и интегрируется с NUnit через отдельный пакет `FsCheck.NUnit`: вместо `[Test]`/`[Fact]` используется атрибут `[Property]`, при этом «tests written this way look like native NUnit tests, except they can take arguments» ([FsCheck.NUnit reference](https://fscheck.github.io/FsCheck/reference/fscheck-nunit.html)). Библиотека изначально ориентирована на F#, что в C# ощущается «страннее» ([Property based testing - Updating FsCheck to version 3.x, Bart Wullems blog](https://bartwullems.blogspot.com/2025/06/property-based-testing-updating-fscheck.html)).

**CsCheck.** C#-ориентированная альтернатива FsCheck: «CsCheck offers no specific integration but can be used with any testing framework (XUnit, NUnit, MSTest, …)» — то есть используется прямо внутри обычного `[Test]`-метода NUnit через вызовы вида `Check.Sample(...)`, без отдельного NuGet-плагина под NUnit (обзор, [Property based testing in C#–CsCheck, Bart Wullems blog](https://bartwullems.blogspot.com/2024/02/property-based-testing-in-ccscheck.html)). По собственному описанию автора библиотеки, «no reflection was used in the making of this product», и библиотека «close to being AOT compatible» ([AnthonyLloyd/CsCheck, README](https://github.com/AnthonyLloyd/CsCheck)) — это существенно для Unity, где `System.Reflection.Emit` ненадёжен под IL2CPP/AOT (сообщество единогласно отмечает, что Reflection.Emit «works fine under Mono in the Editor» и «generally... fails when built with IL2CPP or AOT compilation», по совокупности нескольких обсуждений на forum.unity.com/discussions.unity.com).

**Совместимость по целевому фреймворку.** Актуальная версия CsCheck на NuGet (4.8.0) собирается под `net8.0` и не годится для использования внутри Unity как есть ([NuGet: CsCheck](https://www.nuget.org/packages/CsCheck)); файл проекта в репозитории подтверждает `<TargetFramework>net8.0</TargetFramework>` без мультитаргетинга ([CsCheck.csproj](https://github.com/AnthonyLloyd/CsCheck/blob/master/CsCheck/CsCheck.csproj)). При этом более старые версии (например, 2.10.0, июль 2022) собирались под `.NET Standard 2.0` — «This package targets .NET Standard 2.0. The package is compatible with this framework or higher» ([NuGet: CsCheck 2.10.0](https://www.nuget.org/packages/CsCheck/2.10.0)), что совместимо с профилем Unity .NET Standard 2.1. Практический вывод: для использования в Unity нужно закрепить в проекте старую (netstandard2.0-совместимую) версию CsCheck как `.dll`, а не последнюю версию с NuGet.

**Работающий способ внутри UTF.** Прямых отчётов разработчиков о реальном запуске FsCheck.NUnit или CsCheck именно внутри Unity Test Framework (то есть через Test Runner, а не только через внешний `dotnet test`) в открытых источниках не найдено — «надёжных источников не найдено». Теоретически обе библиотеки — обычные .NET-сборки без прямых зависимостей от Unity API, поэтому запуск возможен при соблюдении условий: (а) библиотека или её старая версия собрана под netstandard2.0/2.1-совместимый таргет, (б) она не использует запрещённый под IL2CPP `System.Reflection.Emit` — для CsCheck это по заявлению автора не проблема, для FsCheck (based on F# runtime + рефлексия для генерации значений) риски совместимости с AOT/IL2CPP выше и отдельно не подтверждены. Для EditMode-тестов, которые всегда выполняются в редакторе на Mono, риск ниже, чем для PlayMode-тестов на билде под IL2CPP-платформу.

## 6. Покрытие кода: пакет Code Coverage

Пакет `com.unity.testtools.codecoverage` работает поверх Test Runner: он «gather[s] and present[s] test coverage information», а после прогона тестов может сгенерировать HTML-отчёт с указанием покрытых строк ([Code Coverage package discussion, Unity Forum](https://forum.unity.com/threads/code-coverage-package-discussion.777542/); [About Code Coverage, 1.2](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/index.html)).

**Ключи командной строки для batchmode** ([Using Code Coverage in batchmode, 1.2.6](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)):

- `-enableCodeCoverage` — включает сбор покрытия.
- `-coverageResultsPath` (опционально) — путь для сохранения результатов и отчёта; по умолчанию путь проекта.
- `-coverageHistoryPath` (опционально) — путь для истории отчёта; по умолчанию путь проекта.
- `-coverageOptions` (опционально) — список опций через `;` в кавычках. Доступные значения: `generateHtmlReport`, `generateHtmlReportHistory`, `generateAdditionalReports` (SonarQube/Cobertura/LCOV), `generateBadgeReport` (SVG/PNG-бейджи), `generateAdditionalMetrics` (цикломатическая сложность, Crap Score), `generateTestReferences`, `dontClear` (накопление между прогонами), `assemblyFilters` (включение/исключение сборок через `+`/`-`), `pathFilters`, `sourcePaths`, `verbosity`.
- `-debugCodeOptimization` — переводит компиляцию скриптов в Debug-режим, что необходимо для точного покрытия («Code Optimization mode defines whether Unity Editor compiles scripts in Debug or Release mode, and Debug mode enables C# debugging which is required in order to obtain accurate code coverage»).

Пример полной команды из мануала:

```
Unity.exe -projectPath <path-to-project> -batchmode -testPlatform editmode -runTests -testResults <path-to-results-xml> -debugCodeOptimization -enableCodeCoverage -coverageResultsPath <path-to-coverage-results> -coverageHistoryPath <path-to-coverage-history> -coverageOptions "generateAdditionalMetrics;generateHtmlReport;generateHtmlReportHistory;generateBadgeReport"
```

([Using Code Coverage in batchmode, 1.2.6](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)).

Для совмещения EditMode- и PlayMode-покрытия в одном отчёте рекомендуемая схема — три отдельных запуска Unity: первый прогоняет EditMode-тесты с `-coverageOptions "generateAdditionalMetrics;assemblyFilters:+my.assembly.*;dontClear"`, второй аналогично прогоняет PlayMode-тесты, третий (без прогона тестов) формирует объединённый отчёт с `-coverageOptions "generateHtmlReport;generateBadgeReport;assemblyFilters:+my.assembly.*" -quit` ([там же](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)). При работе с несколькими проектами, использующими общий код, `-coverageResultsPath` для каждого проекта должен указывать на отдельное место внутри общей корневой папки, чтобы объединённый отчёт строился корректно ([там же](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)).

**Порог покрытия.** Встроенного ключа вида «минимально допустимый процент покрытия» / `minimumCoverage` в документации пакета не найдено — в мануале batchmode такой опции нет. На практике команды реализуют проверку порога отдельным шагом CI, разбирая сгенерированный отчёт/бейдж — это подтверждается обсуждением в GitHub-issue стороннего экшена: «the badge report metric information could be used by teams to enforce a minimum code coverage standard on their repositories», то есть принудительное соблюдение порога строится поверх пакета, а не встроено в него ([game-ci/unity-test-runner issue #181](https://github.com/game-ci/unity-test-runner/issues/181)).

Известное ограничение batchmode: сообщалось, что «Code Coverage isn't calculated for embedded package in batchmode» — то есть при запуске из командной строки покрытие для embedded-пакетов проекта может не собираться, хотя локально через Test Runner в редакторе всё работает корректно ([Code Coverage isn't calculated for embedded package in batchmode, Unity Discussions](https://discussions.unity.com/t/code-coverage-isnt-calculated-for-embedded-package-in-batchmode/915358)).

## 7. Запуск тестов из командной строки

Ключевые аргументы ([Test Framework command line arguments, 2.0.1-exp.2](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html)):

- `-runTests` — «Runs tests in the Project. This argument is required to run any tests.»
- `-testPlatform` — платформа для тестов: EditMode, PlayMode, либо любое значение `BuildTarget` для прогона на билде под конкретную платформу; если не указан, по умолчанию EditMode.
- `-testResults` — «The path where Unity should save the result file. By default, Unity saves it in the Project's root folder.» Результаты — в формате NUnit XML.
- `-testFilter` — «A semicolon-separated list of test names to run, or a regular expression pattern to match tests by their full name», поддерживает отрицание через `!`.
- `-testCategory` — список категорий через `;`, также с поддержкой `!`.
- `-assemblyNames` — список тестовых сборок через `;`.
- `-testNames` — список конкретных полных имён тестов.
- `-forgetProjectPath` — «Don't save your current Project into the Unity launcher/hub history.»
- `-runSynchronously` — «If included, the test run will run tests synchronously, guaranteeing that all tests run in one editor update call» (только для EditMode).
- `-requiresPlayMode`, `-assemblyType` — дополнительные фильтры (по требованию PlayMode, по типу сборки: EditorOnly/EditorAndPlatforms).
- `-playerHeartbeatTimeout` — «The time, in seconds, the editor should wait for heartbeats after starting a test run on a player. This defaults to 10 minutes.»
- `-buildPlayerPath`, `-androidAppBundle`, `-orderedTestListFile`, `-testSettingsFile` — вспомогательные опции для сборки плеера под тесты, формата APK/AAB, порядка тестов и файла `TestSettings.json`.

Пример полной команды (форма из практики game-ci и официальных примеров):

```
Unity -batchmode -projectPath <path> -runTests -testPlatform EditMode -testResults <path-to-results.xml> -logFile <path-to-log> -quit
```

(составлено по документированным ключам `-batchmode`, `-projectPath`, `-runTests`, `-testPlatform`, `-testResults`, `-logFile`, `-quit` — каждый ключ подтверждён отдельно в источниках этого документа; сама строка целиком как «канонический пример» в одном месте официального мануала не встречена, обозначена как составленная из документированных частей).

**Коды возврата.** По официальной документации: «test results follow the XML format as defined by NUnit», но «there is currently no common definition for exit codes reported by individual Unity components under test — the best way to understand the source of a problem is the content of error messages and stack traces» ([Running tests from the command line, 1.1.33 / общая формулировка сохраняется в более новых версиях](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-command-line.html)). На практике, по независимому наблюдению разработчиков: при `-runTests` с провалившимися тестами процесс завершается с кодом 2, при полном прохождении — с кодом 0, и «no error or warning occurs in the log file when this happens»; в GitHub issue поясняется: «if the exit code is 2 it means that the Unity process has been executed correctly but that some tests have failed» ([Handle Test Runner Exit code 2, Dinomite-Studios/unity-azure-pipelines-tasks#167](https://github.com/Dinomite-Studios/unity-azure-pipelines-tasks/issues/167)).

**Как отличить упавшие тесты от упавшей сборки/компиляции.** Единого документированного кода именно для «ошибки компиляции» не найдено; практический подход — читать `-logFile` (Editor.log) на предмет ошибок компиляции C#, которые возникают до того, как Test Runner вообще успевает запустить тесты — в этом случае XML-файл результатов (`-testResults`) может не быть создан вовсе или быть пустым, тогда как код возврата 2 при обычном падении тестов сопровождается валидным XML с перечнем провалившихся тестов. Отдельно зафиксирован баг: «[Batch Mode] Compilation error on first launch of Android batch build results in Unity closing with non-zero exit code» — то есть ошибка компиляции тоже даёт ненулевой код завершения, и различать её от «просто упавших тестов» надёжно можно только по наличию/содержимому файла `-testResults` и по логу ([Unity Issue Tracker](https://issuetracker.unity3d.com/issues/batch-mode-compilation-error-on-first-launch-of-android-batch-build-results-in-unity-closing-with-non-zero-exit-code)).

## Источники

- [Unity Manual: Test Framework, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)
- [Unity Manual: Edit mode and Play mode tests, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)
- [Test Framework command line arguments, 2.0.1-exp.2](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html)
- [Running tests from the command line, 1.1.33](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-command-line.html)
- [com.unity.test-framework CHANGELOG.md (needle-mirror)](https://github.com/needle-mirror/com.unity.test-framework/blob/master/CHANGELOG.md)
- [Unity-Technologies/com.unity.multiplayer.samples.coop, Packages/manifest.json](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Packages/manifest.json)
- [Custom NUnit manual, 2.0.5](https://docs.unity3d.com/Packages/com.unity.ext.nunit@2.0/manual/index.html)
- [Unity Manual: Parameterized tests, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)
- [Unity Manual: 15. Test cases, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/course/test-cases.html)
- [Unity Manual: Create a test assembly, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/workflow-create-test-assembly.html)
- [Unity Manual: Assembly Definition File Format reference](https://docs.unity3d.com/Manual/assembly-definition-file-format.html)
- [Unity Manual: Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)
- [Unity Manual: Managed plug-ins, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-ins-managed.html)
- [Run Unity Tests 10x Faster with .NET (Skip the Unity Test Runner)](https://gamedev.center/run-unity-tests-faster-dotnet/)
- [Sharing C# Code with Unity, randomPoison](https://randompoison.github.io/posts/sharing-with-unity/)
- [KuraiAndras/CsprojToAsmdef, README](https://github.com/KuraiAndras/CsprojToAsmdef)
- [Unit testing classes from other assemblies with Unity's Test Framework in Unity 2022, GameDev Dustin](https://gamedevdustin.medium.com/unit-testing-classes-from-other-assemblies-with-unitys-test-framework-in-unity-2022-820b3e3486fb)
- [nowsprinting/UnityTestExamples — Asynchronous Testing (DeepWiki)](https://deepwiki.com/nowsprinting/UnityTestExamples/4.3-async-setup-and-teardown)
- [FsCheck.NUnit reference](https://fscheck.github.io/FsCheck/reference/fscheck-nunit.html)
- [Property based testing - Updating FsCheck to version 3.x, Bart Wullems blog](https://bartwullems.blogspot.com/2025/06/property-based-testing-updating-fscheck.html)
- [Property based testing in C#–CsCheck, Bart Wullems blog](https://bartwullems.blogspot.com/2024/02/property-based-testing-in-ccscheck.html)
- [AnthonyLloyd/CsCheck, README](https://github.com/AnthonyLloyd/CsCheck)
- [AnthonyLloyd/CsCheck, CsCheck.csproj](https://github.com/AnthonyLloyd/CsCheck/blob/master/CsCheck/CsCheck.csproj)
- [NuGet Gallery: CsCheck (latest, 4.8.0)](https://www.nuget.org/packages/CsCheck)
- [NuGet Gallery: CsCheck 2.10.0](https://www.nuget.org/packages/CsCheck/2.10.0)
- [Using Code Coverage in batchmode, 1.2.6](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)
- [About Code Coverage, 1.2](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/index.html)
- [Code Coverage Package - Discussion, Unity Forum](https://forum.unity.com/threads/code-coverage-package-discussion.777542/)
- [Code Coverage isn't calculated for embedded package in batchmode, Unity Discussions](https://discussions.unity.com/t/code-coverage-isnt-calculated-for-embedded-package-in-batchmode/915358)
- [game-ci/unity-test-runner issue #181](https://github.com/game-ci/unity-test-runner/issues/181)
- [Handle Test Runner Exit code 2, Dinomite-Studios/unity-azure-pipelines-tasks#167](https://github.com/Dinomite-Studios/unity-azure-pipelines-tasks/issues/167)
- [Unity Issue Tracker: Compilation error on first launch of Android batch build results in non-zero exit code](https://issuetracker.unity3d.com/issues/batch-mode-compilation-error-on-first-launch-of-android-batch-build-results-in-unity-closing-with-non-zero-exit-code)







