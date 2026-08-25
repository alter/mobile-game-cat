# Unity Test Framework: testing the rules engine in C#

Date collected: 2026-08-24. Stack version: Unity 6.3 LTS (6000.3.x), C#, .NET Standard 2.1.

## In brief

- Test Framework (UTF) in Unity 6.3 is the `com.unity.test-framework` package; the 6000.3 manual page gives no explicit package version numbers, describing it as "Test framework for running Edit mode and Play mode tests in Unity" ([Unity Manual: Test Framework, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)). In a real Unity 6.x project (Unity-Technologies/com.unity.multiplayer.samples.coop), the `manifest.json` records version `1.5.1` ([manifest.json](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Packages/manifest.json)); the exact version that 6000.3 installs "out of the box" needs to be checked in the specific project — no reliable single source with a "6000.3 → version X.Y.Z" mapping was found.
- EditMode tests run only in the editor and don't support coroutines; PlayMode tests can run as coroutines via the `[UnityTest]` attribute ([Edit Mode vs Play Mode tests, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).
- A test assembly is recognized not by folder name but by its set of references: an assembly referencing `nunit.framework.dll` (and, for EditMode, additionally `UnityEngine.TestRunner`/`UnityEditor.TestRunner`) becomes a "Test Assembly" ([same source](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).
- A pure core without `UnityEngine` is isolated via an `.asmdef` with no reference to `UnityEngine`/`UnityEditor`; for real code reuse outside Unity, developers use either linking sources via `<Compile Include>` in a separate `.csproj`, or building a separate netstandard library with a `.dll` build.
- NUnit inside UTF isn't the vanilla NuGet package but `com.unity.ext.nunit`, "Based on NUnit version 3.5" ([Custom NUnit manual, 2.0](https://docs.unity3d.com/Packages/com.unity.ext.nunit@2.0/manual/index.html)); `[TestCase]`, `[Values]`, `[TestCaseSource]` are available for `[Test]`, while for `[UnityTest]` (coroutine tests) only `[ValueSource]` is supported — `[TestCase]` is officially unsupported ([Parameterized tests, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)).
- Property-based testing in a plain NUnit project actually works via `FsCheck.NUnit` (an F#-oriented library with the `[Property]` attribute) or via the C# library CsCheck; no official reports of actually running them inside Unity Test Framework were found — but both libraries are regular .NET assemblies with no Unity-specific dependencies, and older versions of CsCheck (before 3.x) are built for `netstandard2.0`, which is compatible with Unity's .NET Standard 2.1 profile.
- The Code Coverage package (`com.unity.testtools.codecoverage`) is launched from batchmode with the flags `-enableCodeCoverage`, `-coverageResultsPath`, `-coverageHistoryPath`, `-coverageOptions`, `-debugCodeOptimization`; the package itself has no built-in threshold ("fail if coverage < N%") — confirmed by the absence of such a flag in the official batchmode documentation ([Using Code Coverage in batchmode, 1.2](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)).
- Tests from the command line are run with the flags `-runTests -testPlatform -testResults` on top of `-batchmode -projectPath`; if at least one test fails, Unity returns exit code 2, on a full pass — 0 ([GitHub discussion, Dinomite-Studios/unity-azure-pipelines-tasks#167](https://github.com/Dinomite-Studios/unity-azure-pipelines-tasks/issues/167)); separate from "failed tests" is a "failed build/compilation," which needs to be checked in the log rather than by exit code, since Unity itself has no unified convention for exit codes ([Running tests from the command line, 2.0.1-exp.2](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html)).

## 1. Unity Test Framework: structure, EditMode versus PlayMode

Test Framework is the `com.unity.test-framework` package, intended for running Edit mode and Play mode tests in Unity: "Test framework for running Edit mode and Play mode tests in Unity" ([Unity Manual: Test Framework, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)). The manual page for Editor version 6000.3 doesn't give the package version number explicitly — it only links to the package's detailed manual. Per a search of the package's official changelog on GitHub (the `needle-mirror/com.unity.test-framework` mirror), the most recent entry at collection time is `[1.4.6] - 2025-02-03`, and there's no mention of "6000.3"/"6.3" in the changelog ([CHANGELOG.md](https://github.com/needle-mirror/com.unity.test-framework/blob/master/CHANGELOG.md)). Separately, a newer experimental line `2.0.1-exp.2` exists, whose documentation is used below to describe the command line ([Test Framework command line arguments, 2.0](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html)). Bottom line: the exact test-framework version that ships "by default" specifically with 6000.3 isn't confirmed by any source found — when setting up a project, it needs to be checked in the `Packages/manifest.json` of the specific installation.

**EditMode tests.** "Edit mode tests (also known as Editor tests) only run in the Unity Editor and have access to Editor code and runtime application code." Limitation: "You can't run coroutines in Edit mode tests." ([Edit Mode vs Play Mode tests, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).

Assembly requirement: "Edit mode tests must have an assembly definition that references nunit.framework.dll and have the Editor as their only target platform" ([same source](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).

**PlayMode tests.** "Play mode tests allow you to test your runtime application code, and the tests run as coroutines if marked with the [UnityTest] attribute." Assembly requirement: "Tests must have their own assembly definition with a reference to nunit.framework.dll. Test scripts must be in a folder alongside the .asmdef file." For PlayMode, `includePlatforms` in the asmdef must be an empty array (`[]`) ([same source](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).

An important shared limitation for both modes: "Your test assembly can't reference the predefined Assembly-Csharp.dll assembly. You must move code you want to test into a custom assembly" ([same source](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)). Recommendation for choosing an attribute: use `[Test]`, "unless you need to yield instructions for the Editor in Edit mode tests" or "skip a frame or wait for a certain amount of time in Play mode tests" ([same source](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)).

Formally Unity defines a test assembly as follows: "Unity automatically identifies any assembly as a test assembly if it has an assembly reference to nunit.framework.dll and assembly definition references to UnityEngine.TestRunner and UnityEditor.TestRunner" — the reference to `UnityEditor.TestRunner` is relevant only for EditMode ([Create a test assembly, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/workflow-create-test-assembly.html)).

A practical way to create a test assembly: via the Test Runner window (`Window > General > Test Runner`, "Create a new Test Assembly Folder in the active path") or via the `Assets > Create > Testing > Test Assembly Folder` menu. This creates a `Tests` subfolder with an `.asmdef` file containing three references: `nunit.framework.dll`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`. By default, Platforms is restricted to Editor only; choosing other platforms enables running PlayMode tests on a player build ([Create a test assembly, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/workflow-create-test-assembly.html)).
## 2. Assembly Definition (.asmdef): isolating a pure C# assembly and a test assembly

The full JSON schema of an `.asmdef` file includes the fields `name`, `references`, `includePlatforms`, `excludePlatforms`, `allowUnsafeCode`, `overrideReferences`, `precompiledReferences`, `autoReferenced`, `defineConstraints`, `versionDefines`, `noEngineReferences` ([Assembly Definition File Format reference](https://docs.unity3d.com/Manual/assembly-definition-file-format.html)).

For the rules engine's core to not depend on `UnityEngine`, its `.asmdef` simply must have no references to `UnityEngine`/`UnityEditor` in `references` — Unity doesn't force their presence; the dependency only appears if the code itself uses `UnityEngine` types. The `noEngineReferences` field controls a separate "No Engine References" setting, which explicitly forbids the assembly from referencing engine modules.

**Auto Referenced.** "Specify whether this assembly is automatically referenced by Unity's predefined assemblies. When disabled, Unity does not automatically reference the assembly during compilation" ([Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)). In `.asmdef` this is the `autoReferenced` field, `true` by default ([Assembly Definition File Format reference](https://docs.unity3d.com/Manual/assembly-definition-file-format.html)). Disabling it is useful for test/utility assemblies that shouldn't implicitly end up in `Assembly-CSharp.dll`'s dependencies — per a practical observation, this is "often unneeded and may even increase build times if you don't have code outside your own assemblies" (an observation from a practical asmdef guide, not from the official manual).

**Define Constraints.** "Define constraints specify the scripting symbols that must be defined in your project for Unity to compile or reference an assembly. All the listed symbols must be defined for the assembly to compile" ([Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)). Extended explanation of the logic: "Unity only compiles and references a Project assembly if all the Define Constraints are satisfied, and constraints work like the #if preprocessor directive in C#, but on the assembly level instead of the script level"; a symbol can be inverted with the `!` prefix (the constraint is satisfied if the symbol is NOT defined) ([Unity Manual: Assembly Definition properties / define constraints](https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html)).

`UNITY_INCLUDE_TESTS` — a predefined symbol that Unity automatically defines when compiling test assemblies; a typical example from the official file format:

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

([Assembly Definition File Format reference](https://docs.unity3d.com/Manual/assembly-definition-file-format.html)). In practice, for EditMode/PlayMode test assemblies a scheme like the following is used: EditMode assembly — `"includePlatforms": ["Editor"]`, PlayMode assembly — `"includePlatforms": []` (empty means "all platforms"), both with a reference to `nunit.framework.dll` (see section 1).

**Reference to NUnit.** A test assembly is recognized by its reference to `nunit.framework.dll` in `precompiledReferences`/`references` — "This combination of references is what identifies an assembly as a test assembly" (together with `UnityEngine.TestRunner`/`UnityEditor.TestRunner`) ([Create a test assembly, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/workflow-create-test-assembly.html)).

**Assembly Definition References** — a list of references to other `.asmdef`s: "A list of assemblies to reference from the current assembly. Click the + button to add a new reference" ([Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)). For a test assembly to "see" the core's classes, it needs an explicit Assembly Definition Reference to the core's asmdef — "you need to add an Assembly Definition Reference for your unit testing class to \"see\" the class under test, since the tested class belongs to a separate Game Assembly Definition (asmdef) file" (community, GameDev Dustin, [Unit testing classes from other assemblies with Unity's Test Framework](https://gamedevdustin.medium.com/unit-testing-classes-from-other-assemblies-with-unitys-test-framework-in-unity-2022-820b3e3486fb)).

**Use GUIDs** — a setting determining how Unity serializes a reference to another asmdef: "When you enable this property, Unity saves the reference as the asset's GUID, instead of the Assembly Definition name" ([Assembly Definition properties, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)).

## 3. Compiling the core outside Unity: real developer techniques

A basic community observation: "most C# code will be source-compatible between a Unity project and a standalone .NET C# project — if you copy-and-paste the source code from one to the other, it will almost certainly compile and run as expected," provided that the APIs used are part of .NET Standard; "Not all of the .NET Standard is available to Unity," and IL2CPP platforms "only supports a subset of the .NET Standard" ([Sharing C# Code with Unity, randomPoison](https://randompoison.github.io/posts/sharing-with-unity/)).

**Technique 1 — linking sources into a separate .csproj (without copying).** A working example from practice: alongside the Unity project, a folder is set up (e.g., `HeadlessTests`) with a separate `.csproj` that, via `<Compile Include>`, pulls in the core's sources from `Assets` directly, without copying:

```xml
<ItemGroup>
  <!-- Include ALL Runtime scripts -->
  <Compile Include="..\Assets\!_Project\Scripts\Runtime\**\*.cs" LinkBase="Runtime" />

  <!-- Include all Tests -->
  <Compile Include="..\Assets\!_Project\Scripts\Tests\**\*.cs" LinkBase="Tests" />
</ItemGroup>
```

The author explains the motivation: "We don't copy the files; we link them. This ensures that when we modify a file in the test project, we are actually modifying the Unity asset." If the tested code does touch `UnityEngine` types (e.g., via `Physics.Raycast`), a direct reference to the engine's managed assemblies is added to the `.csproj` via `HintPath`:

```xml
<Reference Include="UnityEngine.CoreModule">
  <HintPath>D:\ProgramFiles\UnityHub\Editor\6000.3.2f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll</HintPath>
</Reference>
```

Run with plain `dotnet test`. An explicit warning: "Since the Unity Native Backend isn't running, calls to things like `Physics.Raycast` or `Time.deltaTime` might not work as expected. This setup is strictly for logic testing." Final recommendation: "Keep your core logic independent of Unity so it can run in a normal .NET environment" ([Run Unity Tests 10x Faster with .NET](https://gamedev.center/run-unity-tests-faster-dotnet/)).

**Technique 2 — a separate netstandard library outside Unity, copying the .dll.** Scheme: an independent netstandard library is written, built outside Unity, and the result is published directly into `Assets/Plugins` via a post-build step, for example `dotnet publish -c Release -o ../UnityProject/Assets/Plugins`. For a predictable set of dependencies, `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` is added to the `.csproj` ([Sharing C# Code with Unity, randomPoison](https://randompoison.github.io/posts/sharing-with-unity/)). Risks of this approach: dependency version conflicts — "if you were two have two different projects both depending on `SomePackage`, each one would pull a copy of `SomePackage.dll` into your Unity project and your project will fail to build," as well as "polluting" the Unity project with Unity-specific files — "polluting your shared code project with Unity-specific details" because of the `.meta` files that Unity generates for every asset, which "clutter the files list in your editor" ([same source](https://randompoison.github.io/posts/sharing-with-unity/)).

**Technique 3 — a managed plug-in.** Unity's official path: "Managed plug-ins are .NET assemblies you create and compile outside of Unity, into a dynamically linked library (DLL) with tools such as Visual Studio" — this method applies "if the DLL does not contain Unity API code" ([Unity Manual: Managed plug-ins, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-ins-managed.html)).

**Technique 4 — the CsprojToAsmdef tool.** A third-party community solution generates `.asmdef` from `.csproj`, allowing you to "use project references from outside or inside of your Unity project," "add NuGet packages," "support Roslyn analyzers," and build Unity code via the .NET CLI. Setup: install `dotnet tool install -g CsprojToAsmdef.Cli`, create a Class Library on `.NET Standard 2.0` inside `Assets`, attach it to a solution above the Unity project root, generate the `.asmdef`. Among the documented limitations: "VSTU is not supported yet," debugging requires manually attaching the Unity debugger, "ReSharper (and possibly Rider) works partially," CI "Requires a Unity installation," and the `VersionDefines` property isn't supported when generating the `.asmdef` ([KuraiAndras/CsprojToAsmdef, README](https://github.com/KuraiAndras/CsprojToAsmdef)).

Overall conclusion across all these techniques: to guarantee that the core compiles both outside and inside Unity, keep its code in a separate `.asmdef` with no references to `UnityEngine`/`UnityEditor`, restrict yourself to .NET Standard 2.1-level APIs, and build/test it with a separate `dotnet test`/`dotnet build` on top of a `.csproj` that either links the same `.cs` files (`<Compile Include>`) or references the same asmdef as a project reference.

## 3-bis. Temporary run via `dotnet test` without Unity installed

Added 2026-08-24 following the first M2 task, done on a machine without Unity.

**Situation.** While Unity isn't installed, the Unity Test Framework is unavailable, and
M2 acceptance requires tests on every change. The workaround is a regular
`dotnet test` project referencing the same `Core`. This justification is accepted: without it
there's nothing to build the core with and nothing to check it with.

**The trap this section exists because of.** The NuGet package `NUnit` and
NUnit inside Unity are **different versions**: Unity ships
`com.unity.ext.nunit`, "Based on NUnit version 3.5," while NuGet brings NUnit 4.x.
Taking the fresh NUnit and deciding "the syntax is the same" won't work: in NUnit 4.0
the classic assertions were moved into a separate library and renamed.

Verbatim from [Breaking Changes](https://docs.nunit.org/articles/nunit/release-notes/breaking-changes.html):

> The [Classic Asserts] have been moved to a separate library and their namespace
> and their class name were renamed to: `NUnit.Framework.Legacy.ClassicAssert`.

> The standalone assert classes have also been moved to the `NUnit.Framework.Legacy`
> namespace. These classes are: Collection Assert, String Assert, Directory Assert,
> File Assert.

> `Assert.That` overloads with *format* specification and `params` have been removed
> in favor of an overload using `FormattableString`.

**The rule to migrate tests into UTF without rewriting them.**

| Write | Don't write |
|---|---|
| `Assert.That(actual, Is.EqualTo(expected))` — the constraint model, present in both 3.5 and 4.x | `ClassicAssert.AreEqual(...)` — the `ClassicAssert` class doesn't exist at all in 3.5 |
| `[Test]`, `[TestCase]`, `[TestFixture]`, `[SetUp]`, `[ValueSource]` | `NUnit.Framework.Legacy.*` — this namespace doesn't exist in 3.5 |
| block-scoped `namespace X { }` | `namespace X;` — file-scoped, that's C# 10, and Unity's ceiling is C# 9 |

The constraint model (`Assert.That` with `Is.`/`Does.`/`Has.`) is the only style
portable in both directions. It should be the only style allowed in the core's tests
until Unity is available.

**Check before migrating to UTF.** Run against the tests:

```bash
grep -rn "ClassicAssert\|NUnit.Framework.Legacy" game/Tests/   # must be empty
grep -rn "^namespace .*;$" game/Tests/                          # must be empty
```

The first catches assertions that don't exist in 3.5. The second — file-scoped
namespaces, which won't compile under C# 9.

**What remains unverified.** Features added to NUnit between 3.5 and
4.x are absent in 3.5, and a full list of them isn't provided here. Among others, this includes
the later `Assert.Multiple` wrappers and asynchronous assertions. As long as
the tests stick to the constraint model and the basic attributes from the table above, the risk is
close to zero; a step outside that needs to be checked against the 3.5 documentation separately.

---

## 4. NUnit inside Unity: version, attributes, limitations

Unity doesn't use the vanilla NuGet NUnit package — instead it ships its own package `com.unity.ext.nunit`, described as "A custom version of NUnit used by Unity Test Framework," "Based on NUnit version 3.5 and works with all platforms, il2cpp and Mono AOT" ([Custom NUnit manual, 2.0.5](https://docs.unity3d.com/Packages/com.unity.ext.nunit@2.0/manual/index.html)). So the base is NUnit 3.5, adapted for Unity's AOT/il2cpp constraints; exactly which version of `com.unity.ext.nunit` ships with 6000.3 wasn't verified (the package page doesn't indicate a binding to an Editor version).

**Supported attributes for `[Test]`** — the full standard NUnit set: `[Test]`, `[TestCase]` (parameterization with a fixed set of values), `[Values]`, `[TestCaseSource]`, `[ValueSource]` ([15. Test cases, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/course/test-cases.html); [Parameterized tests, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)).

**Limitation for `[UnityTest]`** (coroutine, PlayMode/EditMode-with-yield tests): "Regular NUnit tests support both the `[TestCase]` and `[ValueSource]` attributes for parameterized tests. Unity tests only support `ValueSource`" ([Parameterized tests, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)). Example from the documentation:

```csharp
static int[] values = new int[] { 1, 5, 6 };

[UnityTest]
public IEnumerator MyTestWithMultipleValues([ValueSource("values")] int value)
{
    yield return null;
}
```

([same source](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)). Additionally, `ParameterizedIgnoreAttribute` is available, allowing you to "selectively ignore tests based on the parameters supplied to the test method" ([same source](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/reference-tests-parameterized.html)).

`Assert.That` works as regular NUnit constraint syntax (`Assert.That(actual, Is.EqualTo(expected))`, etc.) — no direct official limitations on using `Assert.That` in EditMode/PlayMode tests were described in the sources found, apart from the general requirement not to use coroutines in EditMode tests (section 1) and a known limitation on asynchronous tests: awaiting a coroutine inside an `async` test in EditMode can cause the test to hang, because the editor's "coroutine scheduler" doesn't process yields while EditMode tests are running (community, [nowsprinting/UnityTestExamples, Asynchronous Testing](https://deepwiki.com/nowsprinting/UnityTestExamples/4.3-async-setup-and-teardown)) — this statement is from a third-party description, not from the official manual, and should be considered "not verified against a primary Unity source."

## 5. Property-based testing in C#: applicability inside Unity

**FsCheck.NUnit.** FsCheck is a .NET library for property-based testing, "whose generator combinators can be used in any testing framework," and integrates with NUnit via a separate `FsCheck.NUnit` package: instead of `[Test]`/`[Fact]`, the `[Property]` attribute is used, and "tests written this way look like native NUnit tests, except they can take arguments" ([FsCheck.NUnit reference](https://fscheck.github.io/FsCheck/reference/fscheck-nunit.html)). The library is originally F#-oriented, which feels "stranger" in C# ([Property based testing - Updating FsCheck to version 3.x, Bart Wullems blog](https://bartwullems.blogspot.com/2025/06/property-based-testing-updating-fscheck.html)).

**CsCheck.** A C#-oriented alternative to FsCheck: "CsCheck offers no specific integration but can be used with any testing framework (XUnit, NUnit, MSTest, …)" — meaning it's used directly inside a regular NUnit `[Test]` method via calls like `Check.Sample(...)`, without a separate NuGet plugin for NUnit (overview, [Property based testing in C#–CsCheck, Bart Wullems blog](https://bartwullems.blogspot.com/2024/02/property-based-testing-in-ccscheck.html)). Per the library author's own description, "no reflection was used in the making of this product," and the library is "close to being AOT compatible" ([AnthonyLloyd/CsCheck, README](https://github.com/AnthonyLloyd/CsCheck)) — significant for Unity, where `System.Reflection.Emit` is unreliable under IL2CPP/AOT (the community unanimously notes that Reflection.Emit "works fine under Mono in the Editor" and "generally... fails when built with IL2CPP or AOT compilation," per a combination of several discussions on forum.unity.com/discussions.unity.com).

**Compatibility by target framework.** The current version of CsCheck on NuGet (4.8.0) is built for `net8.0` and isn't suitable for use inside Unity as-is ([NuGet: CsCheck](https://www.nuget.org/packages/CsCheck)); the project file in the repository confirms `<TargetFramework>net8.0</TargetFramework>` with no multi-targeting ([CsCheck.csproj](https://github.com/AnthonyLloyd/CsCheck/blob/master/CsCheck/CsCheck.csproj)). At the same time, older versions (e.g., 2.10.0, July 2022) were built for `.NET Standard 2.0` — "This package targets .NET Standard 2.0. The package is compatible with this framework or higher" ([NuGet: CsCheck 2.10.0](https://www.nuget.org/packages/CsCheck/2.10.0)), which is compatible with Unity's .NET Standard 2.1 profile. Practical conclusion: for use in Unity, an older (netstandard2.0-compatible) version of CsCheck needs to be pinned in the project as a `.dll`, rather than the latest NuGet version.

**A working method inside UTF.** No direct developer reports of actually running FsCheck.NUnit or CsCheck specifically inside the Unity Test Framework (i.e., via Test Runner, not just via an external `dotnet test`) were found in open sources — "no reliable source found." Theoretically, both libraries are regular .NET assemblies with no direct dependencies on the Unity API, so running them should be possible under these conditions: (a) the library or an older version of it is built for a netstandard2.0/2.1-compatible target, (b) it doesn't use `System.Reflection.Emit`, which is forbidden under IL2CPP — for CsCheck this isn't a problem per the author's claim, while for FsCheck (based on the F# runtime + reflection for value generation) the AOT/IL2CPP compatibility risks are higher and not separately confirmed. For EditMode tests, which always run in the editor on Mono, the risk is lower than for PlayMode tests on a build targeting an IL2CPP platform.

## 6. Code coverage: the Code Coverage package

The `com.unity.testtools.codecoverage` package works on top of the Test Runner: it "gather[s] and present[s] test coverage information," and after running tests it can generate an HTML report showing covered lines ([Code Coverage package discussion, Unity Forum](https://forum.unity.com/threads/code-coverage-package-discussion.777542/); [About Code Coverage, 1.2](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/index.html)).

**Command-line flags for batchmode** ([Using Code Coverage in batchmode, 1.2.6](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)):

- `-enableCodeCoverage` — enables coverage collection.
- `-coverageResultsPath` (optional) — path for saving results and the report; defaults to the project path.
- `-coverageHistoryPath` (optional) — path for the report history; defaults to the project path.
- `-coverageOptions` (optional) — a list of options separated by `;` in quotes. Available values: `generateHtmlReport`, `generateHtmlReportHistory`, `generateAdditionalReports` (SonarQube/Cobertura/LCOV), `generateBadgeReport` (SVG/PNG badges), `generateAdditionalMetrics` (cyclomatic complexity, Crap Score), `generateTestReferences`, `dontClear` (accumulate across runs), `assemblyFilters` (include/exclude assemblies via `+`/`-`), `pathFilters`, `sourcePaths`, `verbosity`.
- `-debugCodeOptimization` — switches script compilation to Debug mode, which is needed for accurate coverage ("Code Optimization mode defines whether Unity Editor compiles scripts in Debug or Release mode, and Debug mode enables C# debugging which is required in order to obtain accurate code coverage").

Full command example from the manual:

```
Unity.exe -projectPath <path-to-project> -batchmode -testPlatform editmode -runTests -testResults <path-to-results-xml> -debugCodeOptimization -enableCodeCoverage -coverageResultsPath <path-to-coverage-results> -coverageHistoryPath <path-to-coverage-history> -coverageOptions "generateAdditionalMetrics;generateHtmlReport;generateHtmlReportHistory;generateBadgeReport"
```

([Using Code Coverage in batchmode, 1.2.6](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)).

To combine EditMode and PlayMode coverage into one report, the recommended scheme is three separate Unity runs: the first runs EditMode tests with `-coverageOptions "generateAdditionalMetrics;assemblyFilters:+my.assembly.*;dontClear"`, the second similarly runs PlayMode tests, and the third (without running tests) builds the combined report with `-coverageOptions "generateHtmlReport;generateBadgeReport;assemblyFilters:+my.assembly.*" -quit` ([same source](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)). When working with multiple projects sharing code, `-coverageResultsPath` for each project must point to a separate location inside the shared root folder, so the combined report is built correctly ([same source](https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.2/manual/CoverageBatchmode.html)).

**Coverage threshold.** No built-in flag like a "minimum acceptable coverage percentage" / `minimumCoverage` was found in the package's documentation — the batchmode manual has no such option. In practice, teams implement threshold checking as a separate CI step, parsing the generated report/badge — this is confirmed by discussion in a third-party action's GitHub issue: "the badge report metric information could be used by teams to enforce a minimum code coverage standard on their repositories," meaning enforcing a threshold is built on top of the package rather than being built into it ([game-ci/unity-test-runner issue #181](https://github.com/game-ci/unity-test-runner/issues/181)).

Known batchmode limitation: it has been reported that "Code Coverage isn't calculated for embedded package in batchmode" — meaning that when run from the command line, coverage for the project's embedded packages might not be collected, even though it works correctly locally via Test Runner in the editor ([Code Coverage isn't calculated for embedded package in batchmode, Unity Discussions](https://discussions.unity.com/t/code-coverage-isnt-calculated-for-embedded-package-in-batchmode/915358)).

## 7. Running tests from the command line

Key arguments ([Test Framework command line arguments, 2.0.1-exp.2](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html)):

- `-runTests` — "Runs tests in the Project. This argument is required to run any tests."
- `-testPlatform` — the platform for tests: EditMode, PlayMode, or any `BuildTarget` value to run on a build for a specific platform; if not specified, defaults to EditMode.
- `-testResults` — "The path where Unity should save the result file. By default, Unity saves it in the Project's root folder." Results are in NUnit XML format.
- `-testFilter` — "A semicolon-separated list of test names to run, or a regular expression pattern to match tests by their full name," supports negation via `!`.
- `-testCategory` — a list of categories separated by `;`, also supporting `!`.
- `-assemblyNames` — a list of test assemblies separated by `;`.
- `-testNames` — a list of specific fully qualified test names.
- `-forgetProjectPath` — "Don't save your current Project into the Unity launcher/hub history."
- `-runSynchronously` — "If included, the test run will run tests synchronously, guaranteeing that all tests run in one editor update call" (EditMode only).
- `-requiresPlayMode`, `-assemblyType` — additional filters (by PlayMode requirement, by assembly type: EditorOnly/EditorAndPlatforms).
- `-playerHeartbeatTimeout` — "The time, in seconds, the editor should wait for heartbeats after starting a test run on a player. This defaults to 10 minutes."
- `-buildPlayerPath`, `-androidAppBundle`, `-orderedTestListFile`, `-testSettingsFile` — auxiliary options for building the player for tests, the APK/AAB format, test ordering, and the `TestSettings.json` file.

Full command example (a form drawn from game-ci practice and official examples):

```
Unity -batchmode -projectPath <path> -runTests -testPlatform EditMode -testResults <path-to-results.xml> -logFile <path-to-log> -quit
```

(assembled from the documented flags `-batchmode`, `-projectPath`, `-runTests`, `-testPlatform`, `-testResults`, `-logFile`, `-quit` — each flag is confirmed separately in this document's sources; the string as a whole as a single "canonical example" wasn't found in one place in the official manual, and is noted as assembled from documented parts).

**Exit codes.** Per the official documentation: "test results follow the XML format as defined by NUnit," but "there is currently no common definition for exit codes reported by individual Unity components under test — the best way to understand the source of a problem is the content of error messages and stack traces" ([Running tests from the command line, 1.1.33 / the general wording persists in newer versions](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-command-line.html)). In practice, per an independent developer observation: with `-runTests` and failing tests, the process exits with code 2, on a full pass — with code 0, and "no error or warning occurs in the log file when this happens"; a GitHub issue clarifies: "if the exit code is 2 it means that the Unity process has been executed correctly but that some tests have failed" ([Handle Test Runner Exit code 2, Dinomite-Studios/unity-azure-pipelines-tasks#167](https://github.com/Dinomite-Studios/unity-azure-pipelines-tasks/issues/167)).

**How to distinguish failed tests from a failed build/compilation.** No single documented code specifically for a "compilation error" was found; the practical approach is to read the `-logFile` (Editor.log) for C# compilation errors, which occur before the Test Runner even manages to start the tests — in this case the results XML file (`-testResults`) may not be created at all, or may be empty, whereas exit code 2 from a normal test failure is accompanied by a valid XML listing the failed tests. A separate bug is recorded: "[Batch Mode] Compilation error on first launch of Android batch build results in Unity closing with non-zero exit code" — meaning a compilation error also produces a non-zero exit code, and distinguishing it reliably from "just failed tests" can only be done via the presence/content of the `-testResults` file and the log ([Unity Issue Tracker](https://issuetracker.unity3d.com/issues/batch-mode-compilation-error-on-first-launch-of-android-batch-build-results-in-unity-closing-with-non-zero-exit-code)).

## Sources

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
