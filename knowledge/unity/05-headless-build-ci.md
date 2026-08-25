# Headless builds and CI for Unity

Date collected: 2026-08-24. Stack version: Unity 6.3 LTS (6000.3.x), C#, .NET Standard 2.1.

## In brief

- The basic flag set for a headless run: `-batchmode -nographics -quit -projectPath <path> -executeMethod <ClassName.MethodName> -logFile <path>` — each flag is individually documented in the official manual ([Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)).
- `-quit` is dangerous because it "can hide some error messages" (though they remain in the log), and there's an explicit warning: "If the Editor is running asynchronous code, then `-quit` can cause the application to hang and become unresponsive" ([same source](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)).
- The correct way to exit with a specific error code is to call `EditorApplication.Exit(code)` from your own `-executeMethod`, rather than relying on the automatic `-quit`; the community separately warns that combining `-quit` with a manual `EditorApplication.Exit()` can interfere with the method completing correctly ([Option to remove -quit command, JetBrains/teamcity-unity-plugin#34](https://github.com/JetBrains/teamcity-unity-plugin/issues/34)).
- A working sample custom build script using `BuildPipeline.BuildPlayer`/`BuildPlayerOptions` with a check of `BuildReport.summary.result` is in the official Unity manual ([Create a custom build script](https://docs.unity3d.com/6000.5/Documentation/Manual/build-script-build.html)).
- License activation in CI is done via the flags `-batchmode -serial <key> -username <email> -password <pwd>` (Pro/Plus), or through a file with `-manualLicenseFile <file.alf>` for offline scenarios; for Unity Personal the official command-line path is limited — manual activation of a Personal license via the `license.unity3d.com` web portal stopped being supported in 2025–2026, confirmed by an open issue in the GameCI repository ([game-ci/documentation#408](https://github.com/game-ci/documentation/issues/408)).
- GameCI (game.ci) — an open set of Docker images (`unityci/editor`) and GitHub Actions for building Unity projects in CI; officially "currently images are only available with Ubuntu or Windows as the base operating system" — for an iOS/macOS build, a native macOS runner is needed, there's no Docker image for macOS ([GameCI Docker images for Unity](https://game.ci/docs/docker/docker-images/)).
- On macOS, the editor log by default lives at `~/Library/Logs/Unity/Editor.log`; the path is overridden with the `-logFile` flag.
- Typical pitfalls from developer reports: batchmode hanging due to `-quit` with asynchronous code, the error "Multiple Unity instances cannot open the same project" from stuck `UnityShaderCompiler`/`JobProcess` processes and an undeleted `Temp`, a regression with the loss of the process exit code on Unity 6000.2.14f1 paired with Python's `subprocess`, and a sharp increase in the length of the first asset import on a "cold" CI runner without a `Library` cache.
## 1. Building without a graphical mode: flags and order

Exact wording from the official manual ([Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)):

- **`-batchmode`** — "Run Unity in batch mode. In batch mode, Unity runs command line arguments without the need for human interaction."
- **`-nographics`** — "When you run this in batch mode, Unity doesn't initialize the graphics device. You can then run automated workflows on machines that don't have a GPU."
- **`-quit`** — "Quit the Unity Editor after other commands have finished executing. This can hide some error messages, but they still appear in the Editor's log file." Separate warning: "If the Editor is running asynchronous code, then `-quit` can cause the application to hang and become unresponsive."
- **`-projectPath`** — "Open the project at the given path, which can be absolute or relative to the current working directory. If the pathname contains spaces, enclose it in quotes."
- **`-executeMethod`** — "Execute the static method as soon as Unity opens the project, and after the optional Asset server update is complete."
- **`-logFile`** — "Specifies a file path location to which Unity writes the Editor log file. To output to the console, specify `-` for the path name."
- **`-buildTarget`** — "Select an active build target to launch the Editor in. The options available depend on which build targets you have enabled in the Editor."
- **`-createProject`** — "Create an empty project at the given path."
- **`-disable-assembly-updater`** — "Specify a space-separated list of assembly names as parameters for Unity to ignore on automatic updates."

An important limitation directly related to the typical problem of parallel runs (section 6): "You can't open a project in batch mode while the Editor has the same project open; only a single instance of Unity can run at a time" ([same source](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)).

**Flag order.** The official manual doesn't describe a single mandatory argument order — they're recognized by name, not by position. The page on building from the command line for Unity 6.x gives this example (Windows):

```
"C:\Program Files\Unity\Hub\Editor\6000.3.XXf1\Editor\Unity.exe" -executeMethod BuildScripts.BuildWindows64 -buildTarget StandaloneWindows64 -batchmode -quit -projectPath "C:\path\to\Project" -logFile C:\Logs\build.log
```

([Build a player from the command line, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/build-command-line.html)). This manual names `-projectPath <pathname>` and `-quit` as required for building from the command line; a separate limitation is also emphasized: "you can't build for multiple targets in a single command line invocation. Instead, run the Unity process separately for each target platform," because APIs like `BuildProfile.SetActiveBuildProfile`/`EditorUserBuildSettings.SwitchActiveBuildTargetAsync` don't work correctly in batchmode ([same source](https://docs.unity3d.com/6000.4/Documentation/Manual/build-command-line.html)).

**Why `-quit` is dangerous and how to exit correctly.** The automatic `-quit` gives no control over the process's exit code and, as noted above, can hang with asynchronous code. The correct pattern is not to rely on `-quit`, but to terminate the process manually from your own method: "calling this function will exit right away, without asking to save changes, so it is mostly useful for exiting out of a commandline process with a specific error" — this is the official description of `EditorApplication.Exit` ([Unity Scripting API: EditorApplication.Exit](https://docs.unity3d.com/ScriptReference/EditorApplication.Exit.html)). At the same time, the third-party community notes a conflict between the two mechanisms: "\"-quit\" is added automatically as a parameter but that is an issue when executed method waits for editor update to finish execution, so there needs to be an option to remove \"-quit\" and let the method call EditorApplication.Exit(0) manually" ([Option to remove -quit command, JetBrains/teamcity-unity-plugin#34](https://github.com/JetBrains/teamcity-unity-plugin/issues/34)). A practical recommendation gathered from several discussions: wrap the build logic in a `try/catch`, call `EditorApplication.Exit(0)` on success and `EditorApplication.Exit(<code>)` on error from your own `-executeMethod`, and don't count on automatic termination via `-quit` if a predictable exit code is needed.

## 2. A C# BuildScript example

An official custom build script example from the Unity manual (the "Create a custom build script" page), using `BuildPipeline.BuildPlayer`, `BuildPlayerOptions`, and a check of `BuildReport.summary.result`:

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

([Create a custom build script, 6000.5](https://docs.unity3d.com/6000.5/Documentation/Manual/build-script-build.html)). The manual clarifies that command-line scripts are placed in the project's `Editor/` folder (or in a separate Editor assembly), and such a method is invoked via `-executeMethod` ([same source](https://docs.unity3d.com/6000.5/Documentation/Manual/build-script-build.html)).

The official Scripting API documentation gives a similarly spirited example with an explicit branch on `BuildResult.Succeeded`/`BuildResult.Failed`:

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

An important detail for CI: an unsuccessful `BuildPipeline.BuildPlayer` on its own doesn't produce an automatic non-zero process exit code — this is confirmed by a separate Unity support article devoted specifically to this question: "Why doesn't a failed BuildPipeline.BuildPlayer return an error code in the command line?" ([Unity Support Help Center](https://support.unity.com/hc/en-us/articles/211195263-Why-doesn-t-a-failed-BuildPipeline-BuildPlayer-return-an-error-code-in-the-command-line)). So after checking `summary.result` in a CI script, `EditorApplication.Exit(1)` must be explicitly called on failure and `EditorApplication.Exit(0)` on success (see section 1), rather than relying on the exit code that Unity itself sets.

## 3. Activating a Unity license from the command line and in CI

**Serial-key activation (Pro/Plus).** Official syntax from the "Manage your license through the command line" manual:

macOS:
```
<unity-command-location> -quit -batchmode -serial SB-XXXX-XXXX-XXXX-XXXX-XXXX -username 'name@example.com' -password 'XXXXXXXXXXXXX'
```

Windows:
```
"<editor-installation-location>" -quit -batchmode -serial E3-XXXX-XXXX-XXXX-XXXX-XXXX -username name@example.com -password XXXXXXXXXXXXX
```

([Manage your license through the command line, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)). Named-user activation without a serial key is the same command, but with the `-serial` value empty/omitted. Returning a license:

```
<unity-command-location> -quit -batchmode -returnlicense -username 'name@example.com' -password 'XXXXXXXXXXXXX'
```

([same source](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)). Prerequisite: "license file folder exists" and there's write access to that folder. Explicitly stated: "The following procedures don't apply to Unity Personal. To activate a license for Unity Personal, log in to the Unity Hub" ([same source](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)).

**Manual (offline) activation via a file.** For scenarios where the CI machine has no direct access to the license server, a file-based exchange is used: `"<editor-installation-location>" -batchmode -manualLicenseFile <yourUlfFile> -logfile`; it's noted that "this command doesn't return output to the Command Prompt" — meaning success needs to be checked via the log/file, not console output (per a search review of the Unity manual's manual-activation section; the flag itself wasn't obtained verbatim by a repeat WebFetch — marked as "requires additional verification against the primary source").

**For CI tools like Jenkins**, the manual separately recommends adding `-nographics`, to avoid issues activating without a graphical environment ([Manage your license through the command line, 6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/ManagingYourUnityLicense.html)).

**Personal license limitations in CI (a current issue as of 2025–2026).** GameCI documents separate paths for Personal and Professional licenses: for Personal — obtain a `.ulf` file via Unity Hub (`Preferences > Licenses > Get a free personal license`) and put its content into the `UNITY_LICENSE` secret together with `UNITY_EMAIL`; for Professional/Plus — use `UNITY_SERIAL` together with `UNITY_EMAIL`/`UNITY_PASSWORD`, and explicitly: "Do NOT follow the steps for the personal license if you have a professional license" ([Activation, GameCI](https://game.ci/docs/github/activation/)). However, for the manual activation scenario (ALF → ULF via `license.unity3d.com`), an open GameCI documentation issue records that this path is broken for personal licenses: "The website, though, provides activation procedures only for Pro licenses now," and the result of attempting to activate a personal license is "Get an error from the website"; the issue remains open with no official workaround from Unity ([alf->ulf license activation no longer possible for personal licenses, game-ci/documentation#408](https://github.com/game-ci/documentation/issues/408)). Known community workarounds are third-party tools like `game-ci/unity-license-activate` (supports 2FA via `--authenticator-key`) and `mob-sakai/unity-activate`, which use the `UNITY_USERNAME`/`UNITY_PASSWORD`/`UNITY_SERIAL` environment variables for automatic activation in CI.

## 4. GameCI

GameCI (game.ci) is an open project providing ready-made Docker images and GitHub Actions/GitLab CI templates for building and testing Unity projects in CI. "All projects for Unity in GameCI use `game-ci/docker` docker images," published as `unityci/editor` on Docker Hub; "All editor versions" are supported, "Images for newly released Unity editor versions are added almost immediately" — no separate explicit mention of Unity 6/6000.x was found on this page, but the wording about "all versions" and "almost immediately after release" implies the 6000.x line too ([GameCI Docker images for Unity](https://game.ci/docs/docker/docker-images/)).

**Limitation by base OS and iOS/macOS.** "Currently images are only available with Ubuntu or Windows as the base operating system" ([same source](https://game.ci/docs/docker/docker-images/)). This directly implies a limitation for iOS: full compilation/signing of an Xcode project requires Apple tools available only on macOS, so there's no macOS Docker image for GameCI — "We are looking to include MacOS as a base image \"in the future\", which is mostly dependent on contributions from the community"; instead of a container for generating IL2CPP builds for macOS, using a native macOS GitHub Actions runner is recommended ([same source](https://game.ci/docs/docker/docker-images/)). At the same time, an `ios` component is present in the list of components used to assemble custom images (`android`, `ios`, `linux-il2cpp`, `mac-mono`, `webgl`, `windows-mono`) — but such an image on an Ubuntu base is only suitable for preparing/exporting the Xcode project, not for the final binary compilation, which still requires Xcode on macOS ([Customize GameCI Unity Docker images](https://game.ci/docs/docker/customize-docker-images/)).

**Actions.** GameCI provides separate GitHub Actions for different pipeline steps: license activation (`game-ci/unity-activate`, a wrapper around `unity-license-activate`), building (`unity-builder`), tests. For license activation in GitHub Actions, the official instructions describe separate steps for Personal and Professional licenses (see section 3) ([Activation, GameCI](https://game.ci/docs/github/activation/)).

## 5. Examining the build log: Editor.log

On macOS the default path is `~/Library/Logs/Unity/Editor.log`; on Windows — `%LOCALAPPDATA%\Unity\Editor\Editor.log`; on Linux — `~/.config/unity3d/Editor.log` (data gathered from practical guides and confirmed by the documented behavior of the `-logFile` flag, which lets you override the default path — see section 1: "Specifies a file path location to which Unity writes the Editor log file"). Separate from Editor.log there's Player.log (for the built player), which by default lives in the same place, at `~/Library/Logs/Unity/Player.log` on macOS.

When run in batchmode, Unity by default keeps writing to the same Editor.log if `-logFile` with an explicit path isn't passed — so in CI it's essential to specify `-logFile <path>`, to get a predictable log location for later parsing.

**How to catch a compilation error from batchmode.** No official "single exit code for a compilation error" is documented (see also section 7 of the first file, on test exit codes) — the recommendation "the best way to understand the source of a problem is the content of error messages and stack traces" applies to regular builds too. In practice, a C# compilation error occurring before the build/test stage is visible in the log as lines with `error CS####`, preceding any messages about the start of the build/tests; in this case Unity itself also exits with a non-zero code — confirmed by a separate issue in the tracker: "[Batch Mode] Compilation error on first launch of Android batch build results in Unity closing with non-zero exit code" ([Unity Issue Tracker](https://issuetracker.unity3d.com/issues/batch-mode-compilation-error-on-first-launch-of-android-batch-build-results-in-unity-closing-with-non-zero-exit-code)). So distinguishing a "compilation error" from "failed tests" or a "failed build" can't be done directly by exit code — `-logFile` needs to be parsed for `error CS` (compilation) as opposed to test/build result entries, which appear later in the log.

## 6. Typical pitfalls from developer reports

**Batchmode hanging.** The officially documented cause is asynchronous code paired with `-quit`: "If the Editor is running asynchronous code, then `-quit` can cause the application to hang and become unresponsive" ([Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)). It's additionally been observed that `Update()` isn't called in the standard way in `-batchmode -executeMethod` mode, which means code waiting on callbacks via `EditorApplication.update` may not complete without a manual wait loop (a generalization from discussions on discussions.unity.com).

**"Multiple Unity instances cannot open the same project."** The official error text developers encounter: "It looks like another Unity instance is running with this project open. Multiple Unity instances cannot open the same project" ([Multiple Unity instances cannot open the same project, Unity Discussions](https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project/607546)). An official reply from a Unity representative points to a specific cause: "zombie instances of UnityShaderCompiler or JobProcess lingering when this happens" — meaning the problem isn't the "one Unity — one project" limitation itself (which is also officially documented, see section 1), but stuck child processes after the editor crashes ([same source](https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project/607546)). The community suggests deleting the `UnityLockfile` file (located in `Temp/` or `Library/`), and if that doesn't help, fully deleting the `Temp/` folder; it's also recommended to terminate the `Unity.exe`/`Unity`/`Unity Hub` processes via Task Manager on Windows or Activity Monitor on macOS before re-running the CI job ([same source](https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project/607546); [Resolving "The project is currently open in the Unity Editor", Unity Support](https://support.unity.com/hc/en-us/articles/40828087523092-Resolving-the-The-project-is-currently-open-in-the-Unity-Editor-Please-close-it-in-the-Editor-to-proceed-with-this-operation-Error)). Practical conclusion for CI: before running a batchmode job, it's worth force-terminating any stuck processes belonging to the project, and deleting `Library/Temp` if a "stuck" lock file is suspected, rather than simply re-running the same command.

**The problem of a simultaneously open editor.** As noted in section 1, officially: "You can't open a project in batch mode while the Editor has the same project open; only a single instance of Unity can run at a time" ([Unity Editor command-line arguments reference, 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)) — on a self-hosted CI runner, this means the job must not run in parallel with a locally opened editor on the same project checkout, and that parallel CI jobs over the same clone of the repository won't work — a separate clone/working copy is needed per job.

**A regression with the loss of the process exit code.** Recorded in a recent (2025–2026) discussion: after upgrading from Unity 2022.3.62f2 to 6000.2.14f1, a Python process launching Unity via `subprocess.Popen()` with `-batchmode -executeMethod` stopped receiving the exit code, even though the C# script still calls `EditorApplication.Exit(0)`/`EditorApplication.Exit(1)`; instead of exiting normally, the Unity process "becomes unresponsive after the build completes, eventually becoming a zombie process" ([Unity batchmode does no longer return exit code, Unity Discussions](https://discussions.unity.com/t/unity-batchmode-does-no-longer-return-exit-code-that-could-be-captured-by-python/1698339)). Workarounds proposed in the thread: don't call `EditorApplication.Exit()` manually and let Unity exit on its own, or use `Environment.ExitCode` and exceptions instead of an explicit `Exit()` ([same source](https://discussions.unity.com/t/unity-batchmode-does-no-longer-return-exit-code-that-could-be-captured-by-python/1698339)). This is reported for 6000.2.14f1, not for 6000.3 — when moving to 6.3, it's worth separately re-checking in your own CI whether the regression reproduces.

**Slow first asset import.** Since Unity stores its internal asset representation in the `Library/` folder, any "cold" checkout on a CI runner without a saved `Library/` cache triggers a full re-import of all assets on the first batchmode run — this is a structural cause, not a bug. A specific case of degradation recorded on the forum: "a project that previously imported in approximately one hour in versions 2019, 2023, 6, and 6.5 is now taking nearly four hours in version 6.3" ([Importing assets can be very slow, Unity Discussions](https://discussions.unity.com/t/importing-assets-can-be-very-slow/1716277)) — a report from an individual user, not officially reproduced and not tied to a cause on Unity 6.3's side as such; cited as an example of a community complaint, not a confirmed fact of degradation specifically in 6.3. For diagnosing slow imports, Unity provides the built-in Import Activity tool (`Window > Analysis > Import Activity`), which shows the reason for each re-import — for example, "no previous revision was found (a first import, or the related artifact in the library was deleted)," a dependency change, or a Unity version upgrade (community, [Reducing assets import times in Unity](https://dev.to/attiliohimeki/reducing-assets-import-times-in-unity-2kn2)). The standard practical recommendation from the CI community is to cache the `Library/` folder between job runs, to avoid a full re-import on every run.

## Sources

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
