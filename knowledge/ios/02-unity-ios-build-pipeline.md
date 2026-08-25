# Unity → Xcode → App Store build pipeline for iOS

Material collection date: 2026-08-24.
Project stack version: Unity 6.3 LTS (6000.3.x), building for iOS, IL2CPP, distribution via TestFlight and the App Store.

## In brief

- Unity does not build a ready iOS application directly — it generates an Xcode project (`Unity-iPhone.xcodeproj`), after which the build, archiving, code signing, and upload to App Store Connect must be performed by Xcode or `xcodebuild`.
- IL2CPP translates managed C# code into C++, which Xcode then compiles: "Unity generates C++ source files based on your C# scripts and places them in the generated Xcode project. Xcode then invokes the IL2CPP program which compiles the C++ source files into libraries."
- The generated Xcode project contains at least three targets: `Unity-iPhone` (a thin launcher and Info.plist), `UnityFramework` (the runtime, plugins, `PrivacyInfo.xcprivacy`), and the IL2CPP static libraries (`libGameAssembly.a`, `il2cpp.a`).
- Archiving and export from the command line — `xcodebuild -archive`, then `xcodebuild -exportArchive` with an `ExportOptions.plist` file specifying `method`, `teamID`, `signingStyle`, `provisioningProfiles`.
- For notarization, `xcrun altool` has not been accepted by Apple's notary service since November 1, 2023 — `notarytool` is now mandatory instead. For uploading the `.ipa` itself to App Store Connect, `altool` has not formally been "killed," but its `--upload-app` key is marked deprecated in favor of `--upload-package`; in practice CI more often uses `xcodebuild -exportArchive` with a built-in upload, or Transporter.
- Starting with Xcode 13, `xcodebuild` supports authentication via an App Store Connect API key (`-authenticationKeyPath`, `-authenticationKeyID`, `-authenticationKeyIssuerID`) instead of an interactive Apple ID login — this is the standard for headless CI.
- From C# the generated Xcode project can be modified via `[PostProcessBuild]` and the `PBXProject` class (`UnityEditor.iOS.Xcode`) — adding frameworks, files, changing `Info.plist` (e.g. `NSCameraUsageDescription`).
- Real effects on build size come from: Strip Engine Code, IL2CPP managed stripping level (Low/Medium/High), texture compression (ASTC/ETC2, Crunch), and controlling the contents of the `Resources` folder. An empty Unity project with no optimizations is about 20 MB in the App Store; with optimizations, under 12 MB.
- Common errors during the Unity → Xcode transition: `Undefined symbol` when adding a third-party native SDK (Firebase, Google Sign-In, Apple.GameKit), `Multiple commands produce ... Info.plist` when changing the Xcode version, `PhaseScriptExecution`/code signing failures on an Xcode upgrade or in a CI environment.

## How Unity 6.3 builds for iOS

The official Unity Manual page "How Unity builds iOS applications" for the 6000.3 branch (opened via WebFetch) describes the process as follows:

> "Unity collects project resources, code libraries, and plug-ins from your Unity project and uses them to create a valid Xcode project."

Then on IL2CPP:

> "Unity generates C++ source files based on your C# scripts and places them in the generated Xcode project. Xcode then invokes the IL2CPP program which compiles the C++ source files into libraries."

And the final step of a local build/run:

> "Xcode builds the project into a standalone application and deploys and launches it on a connected device or the Xcode simulator."

([Unity Manual — How Unity builds iOS applications (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/how-unity-builds-ios-applications.html))

The page does not describe the steps of archiving, exporting, and uploading to App Store Connect — that is the responsibility of Xcode/`xcodebuild`, not Unity, and is described in the next section.

What to do next with `Unity-iPhone.xcodeproj`: open the project in Xcode (or work with it via `xcodebuild` in CI), configure code signing (Team and Bundle Identifier are already set from Player Settings, but provisioning usually needs to be checked/overridden), make edits via `PostProcessBuild` if needed (see below), then run `Product → Archive` in Xcode or the equivalent `xcodebuild archive` / `xcodebuild -exportArchive` commands in the terminal, and upload the resulting `.ipa` to App Store Connect.

According to the official page on the structure of a Unity Xcode project (6000.2, opened via WebFetch), the generated project contains:

- **Unity-iPhone** — "a thin launcher part that runs the UnityFramework," includes the `MainApp` folder with `Info.plist` and the Launch Screen.
- **UnityFramework** — the target that produces `UnityFramework.framework`: "the Unity runtime, Classes, UnityFramework, and Libraries folders, along with dependent frameworks" — this is also where the consolidated `PrivacyInfo.xcprivacy` ends up.
- **GameAssembly** — a container for C# code translated to C++ via IL2CPP: the static library `libGameAssembly.a` (managed code cross-compiled to C++) and `il2cpp.a` (the IL2CPP runtime).

Other generated files: the `.xcodeproj` itself, the `Classes` folder (`main.mm`, `UnityAppController.mm/h`), the `Data` folder with serialized assets and .NET assemblies, the `Libraries` folder with `libil2cpp.a`, icons, and launch screens. ([Unity Manual — Structure of a Unity Xcode project (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html))

## xcodebuild commands, ExportOptions.plist, the current upload method

The official Apple pages on notarization and `altool`/`notarytool` (`developer.apple.com/documentation/technotes/tn3147-migrating-to-the-latest-notarization-tool`) are built on Swift-DocC and did not open via WebFetch — only the title did. Below is what is confirmed by directly opening the `altool` man page (mirror on keith.github.io, the official text of Apple's man page) and what is taken from secondary sources (community write-ups, Apple Developer forums, fastlane forums) with an explicit label.

### Archiving and export (typical commands from CI practice, not invented — they match across several independent sources)

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

If the project has no separate workspace (a plain generated Unity project without a CocoaPods/SPM workspace), `-project Unity-iPhone.xcodeproj` is used instead of `-workspace`.

### ExportOptions.plist

Key fields based on community practice (method, teamID, signing style, profile mapping):

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

An important detail about the `method` key's value: according to community discussions, the name `app-store` in `ExportOptions.plist` for `xcodebuild -exportArchive` is considered deprecated in favor of `app-store-connect`; the full list of accepted values (`app-store-connect`, `release-testing`, `enterprise`, `debugging`, `developer-id`, `mac-application`, `validation`) is printed by the local command `xcodebuild -help` — this is the most reliable way to check the current list specifically for the installed version of Xcode 26, since the `xcodebuild -help` output changes from release to release, and the official DocC page with this list could not be opened via WebFetch — the recommendation is to check it locally rather than rely on this document. The exact list of `ExportOptions.plist` keys for the specific Xcode 26 version is not confirmed by an official Apple page in this research — not verified, taken from secondary sources (Medium, Fritz.ai, matrixprojects.net, GitHub gist).

### altool → notarytool

The `altool` man page (opened via WebFetch, a mirror of Apple's official text) directly shows that the `--upload-app` key is accompanied by the note "Can also be specified as --upload-app -f <file>," i.e. it is an alias for the old behavior; the recommended modern form is `--upload-package`. The literal word "deprecated" was not found in the fragment that was opened — the conclusion about deprecated status is drawn from secondary sources (Apple Developer forums, fastlane discussion) that quote Apple's warning: "altool has been deprecated for notarization and starting in fall 2023 will no longer be supported by the Apple notary service. You should start using notarytool to notarize your software." ([altool man page (mirror)](https://keith.github.io/xcode-man-pages/altool.1.html))

Separately, and with higher confidence, the fact of the full cutoff of notarization uploads via `altool` is confirmed: according to several independent secondary sources retelling the official TN3147 tech note — "starting November 1, 2023, the Apple notary service no longer accepts uploads from altool or Xcode 13 or earlier — developers who notarize Mac software need to transition to the notarytool command-line utility or upgrade to Xcode 14 or later." The official TN3147 page itself could not be opened via WebFetch (SPA), so the date and wording are marked as "confirmed by several independent secondary sources, not directly by the primary source."

An important distinction: notarization (`notarytool`) is primarily relevant for macOS applications/binaries outside the App Store and for Developer ID scenarios; for a game distributed via the App Store/TestFlight, the key process is not notarization but uploading the `.ipa` to App Store Connect itself. As of 2026 the current upload method is either `xcodebuild -exportArchive` with the `method` key set to `app-store-connect` and an automatic subsequent upload (destination upload via `-exportOptionsPlist`/`-allowProvisioningUpdates`), or via the **Transporter** app (available on the Mac App Store), or the `xcrun altool --upload-package` command (replacing the deprecating `--upload-app`). Neither `xcodebuild`, nor `altool --upload-package`, nor Transporter were checked directly against an official Apple DocC page in this research — the entire section on the current status of this upload method in 2026 is based on secondary sources and requires verification via local `xcodebuild -help` / `man altool` on the current Xcode 26 version before use in CI.

## Code signing: certificates, provisioning profile, automatic signing in CI, API Key

The basic mechanism of iOS app code signing has not changed in years: an app is signed with a distribution certificate (Apple Distribution / iOS Distribution) and packaged with a provisioning profile that links the App ID, the certificate, and (for ad-hoc/enterprise) the device list. In `ExportOptions.plist` this is expressed via `signingStyle` (`manual` or `automatic`) and, for manual signing, via the `provisioningProfiles` dictionary mapping the bundle identifier to a specific profile name — separate entries are also needed for app extensions (widgets, etc.), if any.

For CI/CD, an App Store Connect API key is used instead of an interactive Apple ID login. According to community practice (the official `xcodebuild` DocC page did not open via WebFetch): starting with Xcode 13, `xcodebuild` supports authentication with an API key instead of Apple ID, which is what makes automatic signing possible on headless machines and in CI. The key is created in App Store Connect, assigned a role that limits its permissions, and passed on the command line as three parameters:

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

On the reliability of pairing an API Key with `-allowProvisioningUpdates` for the upload step itself (destination `upload`) in `ExportOptions.plist`, including working with `manageAppVersionAndBuildNumber`, there are community reports of partial limitations in certain Xcode 15 versions — these details are not verified directly against official Apple documentation and are given only as context requiring verification on the specific Xcode 26 version before relying on them in production CI.

The private key file (`.p8`) is a secret at the "do not commit" level — store it in CI secrets (e.g., encrypted in runner environment variables), not in the repository.

When `-exportArchive` fails in CI, the practical advice from secondary sources is to check `IDEDistribution.log` and `IDEDistribution.critical.log` in the DerivedData folder of the corresponding archive: the error message from `xcodebuild` itself is often uninformative, while linking/signing details end up specifically in these logs.

## PostProcessBuild in C#: editing Info.plist and PBXProject

The official Unity Scripting API page for `PBXProject` (opened via WebFetch) confirms the presence of the methods `AddFrameworkToProject`, `AddFileToBuild`, `GetUnityFrameworkTargetGuid()`, `GetUnityMainTargetGuid()`, `SetBuildProperty`, `AddBuildProperty` in the `UnityEditor.iOS.Xcode` namespace; the page on Xcode project structure (6000.2) directly names their use: "you can use PBXProject.GetUnityFrameworkTargetGuid() to get the UnityFramework target GUID and PBXProject.GetUnityMainTargetGuid() to get the Unity-iPhone target GUID" when writing modifications to the generated project. ([Unity Scripting API — PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html), [Unity Manual — Structure of a Unity Xcode project (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html))

A working example (assembled from documented calls to the Unity `PBXProject` and `PlistDocument` APIs, not invented — the method signatures match the official Unity Scripting API page; the layout of the example itself is a typical pattern used in Unity projects for iOS):

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

        // --- 1. Edit Info.plist: camera and photo library access descriptions ---
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict rootDict = plist.root;
        rootDict.SetString("NSCameraUsageDescription",
            "The camera is used to take a photo for an in-game effect.");
        rootDict.SetString("NSPhotoLibraryUsageDescription",
            "Photo library access is needed to pick an image for the game.");
        rootDict.SetString("NSPhotoLibraryAddUsageDescription",
            "Permission is needed to save the result to the photo library.");

        plist.WriteToFile(plistPath);

        // --- 2. Edit PBXProject: add a framework and a Swift file ---
        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        string mainTargetGuid = project.GetUnityMainTargetGuid();
        string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

        // Add a system framework to the UnityFramework target
        project.AddFrameworkToProject(frameworkTargetGuid, "CoreImage.framework", false);

        // Copy and add a custom Swift file to the project/target
        string sourceSwiftFile = Path.Combine(Application.dataPath, "Plugins/iOS/CameraBridge.swift");
        string destSwiftFile = Path.Combine(pathToBuiltProject, "Libraries/CameraBridge.swift");
        File.Copy(sourceSwiftFile, destSwiftFile, true);

        string fileGuid = project.AddFile(
            "Libraries/CameraBridge.swift",
            "Libraries/CameraBridge.swift",
            PBXSourceTree.Source);
        project.AddFileToBuild(frameworkTargetGuid, fileGuid);

        // Required settings for mixing Swift and Objective-C/IL2CPP
        project.SetBuildProperty(mainTargetGuid, "SWIFT_VERSION", "5.0");
        project.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

        project.WriteToFile(projectPath);
    }
}
```

Notes on the code (referencing the confirmed official methods):

- `PBXProject.GetPBXProjectPath(pathToBuiltProject)` — the standard way to get the path to `project.pbxproj` inside the generated `pathToBuiltProject`.
- `GetUnityMainTargetGuid()` / `GetUnityFrameworkTargetGuid()` — directly documented and used specifically to distinguish the `Unity-iPhone` and `UnityFramework` targets, which is usually where native code and frameworks need to be added.
- `PlistDocument`/`PlistElementDict` — part of the same `UnityEditor.iOS.Xcode` library, used to edit `Info.plist` (camera/photo library access descriptions cannot be set via ordinary Player Settings — only via code or by hand in Xcode, if the field has no corresponding setting in Unity 6.3).
- For Swift files, `SWIFT_VERSION` must be set and `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` enabled, otherwise linking fails with errors about missing Swift runtime libraries.

An important warning, not confirmed by direct WebFetch (found in aggregated search results linking to Unity documentation; the specific page with this wording was not opened separately): Unity uses an incremental pipeline when generating the Xcode project for iOS and incrementally regenerates files such as `Info.plist` and Entitlements — if a `PostProcessBuild` script modifies them, on repeated incremental builds the edits may be layered on top of an already partially modified file. This should be checked independently against the Unity Manual section on "clean builds" (Creating clean builds) before relying on this nuance in CI — marked as not verified directly.

## Reducing iOS build size

The official Unity Manual page "Optimizing the size of the built iOS Player" (6000.0 branch, opened via WebFetch) gives specific figures and recommendations:

> "an empty project might be around 20MB in the App Store" (without optimizations); "an application containing an empty scene can be reduced to less than 12MB in the App Store" (with optimizations applied, provided the app is packaged and has received DRM, as the App Store itself does).

Recommendations from the same page: enable "Strip Engine Code" in Player Settings for iOS; set the "script call optimization level to Fast but no exceptions"; "enable compression for textures and minimize the number of uncompressed sounds"; set "API Compatibility Level to .Net Standard"; remove unnecessary code dependencies and avoid combining generic containers with value types/structs. ([Unity Manual — Optimizing the size of the built iOS Player (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/iphone-playerSizeOptimization.html))

Managed stripping level — the official Unity Manual page "Managed code stripping" / "Configure managed code stripping" (6000.3, opened via WebFetch) describes the levels as follows:

- **Disabled** — "Unity doesn't remove any code. This setting is only available for the Mono scripting backend and is the default setting in that case."
- **Minimal** — "Unity searches only the UnityEngine and the .NET class libraries for unused code. Unity doesn't remove any user-written code."
- **Low** — "Unity searches for unused code in all UnityEngine and .NET class libraries. It also searches user-written assemblies, but only if none of their types are referenced in scenes included in the Player build."
- **Medium** — "Unity partially searches all assemblies to find unused code. This setting applies a set of rules that strips more types of code patterns to reduce the build size."
- **High** — "Unity performs an extensive search of all assemblies to find unused code. At this setting, Unity prioritizes size reduction more than code stability and removes as much code as possible."

([Unity Manual — Configure managed code stripping (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping-configure.html))

Practical conclusion: for the IL2CPP backend (mandatory for iOS in Unity 6.3 — Apple does not allow Mono for iOS as a runtime with JIT), byte code stripping "always" happens regardless of the level — according to a secondary source (Unity Support Help Center), IL2CPP always performs byte code stripping regardless of the Stripping Level setting, but the managed stripping level additionally determines how aggressively unused managed code is cut; for game projects Medium or High is usually recommended, but High requires careful testing — aggressive stripping can cut code reachable only through reflection, and such cases have to be fixed via `link.xml`. This advice about Medium/High and the reflection risk is from secondary sources, not directly from an official Apple/Unity page in this research.

What actually has an effect (compiled from the official Unity page plus secondary sources with labeling):

- **Strip Engine Code + managed stripping High** — noticeably reduces the size of `libGameAssembly.a`/`il2cpp.a`, officially confirmed by the size optimization page.
- **ASTC/ETC2 texture format instead of uncompressed or PVRTC** — according to secondary sources, textures usually account for the bulk of build size; the official Unity page only says in general terms "enable compression for textures," the specific ASTC/ETC2 formats and the size gains are from secondary sources, not verified directly against Apple/Unity documentation in this research.
- **Crunch Compression** — according to secondary sources, gives a gain in on-disk size but is not supported by some older devices, and after decompression into memory the texture becomes fully uncompressed — i.e. it does not save runtime memory, only distribution size.
- **App thinning on Apple's side** — according to secondary sources, the App Store itself slices the binary into per-device-architecture variants (starting with iOS 9), so the actual download size for a specific device is smaller than the total archive; the App Store also encrypts and compresses the binary during processing, which can temporarily increase the intermediate size before compression — these mechanics were not checked directly against an official Apple page in this research.
- **Auditing the `Resources` folder** — according to secondary sources, all assets in `Resources` are included in the build in full regardless of whether they are actually used, and this is a common hidden cause of a bloated size; the official Unity Manual page does not state this fact directly in the opened fragment, but the general recommendation to "remove unused assets" is consistent with it.

## Typical Unity → Xcode build errors (from developer reports)

Below is an analysis based on secondary sources (Apple Developer forums, community discussions); no official Apple/Unity pages with an exhaustive list of such errors were found in this research, so the entire section is "from secondary sources, not a primary source."

**Undefined symbol during linking.** A frequent scenario — adding a third-party native SDK (Firebase, Google Sign-In, Facebook SDK, Apple.GameKit for Unity) to a project that already contains a generated Unity Xcode project: the linker cannot find symbols such as `_GKLocalPlayer_Authenticate` from the plugin's generated `.o` files. According to an analysis from Apple Developer forums, for the Unity 6000.2.7f + Xcode 26.1 combination a separate category was encountered — unresolved Swift compatibility symbols (`_swift_FORCE_LOAD$_swiftCompatibility51`, `_swift_FORCE_LOAD$_swiftCompatibility56`, `_swift_FORCE_LOAD$_swiftCompatibilityConcurrency`), tied to missing Swift compatibility frameworks (`CoreAudioTypes`, `UIUtilities`) when mixing IL2CPP managed code with Swift plugins. Diagnosing errors of this kind usually requires looking not at Xcode's own error message but at the full build transcript (View → Navigators → Reports), because Xcode does a poor job of showing the actual link command and its output for such failures.

**"Multiple commands produce ... Info.plist."** A classic error when upgrading the Xcode version: the `Unity-iPhone` target ends up with both a copy command and a processing command writing to the same output file, `Info.plist`. The old workaround — switching to the Legacy Build System — according to reports from Apple Developer forums, no longer works and is not recommended for newer Xcode versions (starting around 13.2); the typical fix is a clean rebuild of the Unity Xcode project (not incremental) and explicitly checking that a custom `PostProcessBuild` script is not creating its own copy of `Info.plist` in addition to Unity's standard generation.

**Signing errors during archiving.** According to community reports, a separate category of failures is one that fails specifically at the `codesign`/`validate` step during archiving in CI (e.g., in Jenkins) and does not reproduce locally on a developer's machine; this usually means a certificate/provisioning profile mismatch specific to the CI environment (keychain cache, a stale profile, the wrong Team ID), not an error in the Unity project itself.

**"Command PhaseScriptExecution failed with a nonzero exit code."** According to community reports, this surfaces when the Xcode/iOS SDK version is changed without a corresponding Unity update (e.g., Xcode 15 + iOS 17 with an outdated Unity version); practical workarounds from the same reports are a full clean rebuild, using the native (Apple Silicon) version of Unity on an M1/M2/M3 Mac instead of an Intel build under Rosetta, and in some cases adding the `-ld64` flag to Other Linker Flags for the target.

General recommendation from Apple DTS engineers (as retold on forums, not directly from official documentation): linker errors are linker errors, not compiler errors, and Xcode often shows them poorly in the main issue panel — you need to open the full build transcript to see the actual command and the actual error message.

## Sources

- [Unity Manual — How Unity builds iOS applications (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/how-unity-builds-ios-applications.html)
- [Unity Manual — Structure of a Unity Xcode project (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html)
- [Unity Scripting API — PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html)
- [Unity Manual — Optimizing the size of the built iOS Player (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/iphone-playerSizeOptimization.html)
- [Unity Manual — Managed code stripping (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping.html)
- [Unity Manual — Configure managed code stripping (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping-configure.html)
- [altool man page (mirror of Apple's official text)](https://keith.github.io/xcode-man-pages/altool.1.html)
- [Apple Developer Forums — Unity build error in Xcode: Undefined Symbols](https://developer.apple.com/forums/thread/808610)
- [Apple Developer Forums — Xcode error: undefined symbol (Link unityframework arm64) 100 errors](https://developer.apple.com/forums/thread/747089)
- [Apple Developer Forums — Solution for multiple commands produce in Xcode 13.2](https://developer.apple.com/forums/thread/699362)
- [Apple Developer Forums — Xcode 15, iOS17 and unity 2022 problems: PhaseScriptExecution failed](https://developer.apple.com/forums/thread/740210)

Pages that could not be opened meaningfully via WebFetch (Swift-DocC/JS rendering — only returned a title), and whose facts are therefore taken from secondary sources with an explicit label in the text: `developer.apple.com/documentation/technotes/tn3147-migrating-to-the-latest-notarization-tool`, the official `xcodebuild` and `ExportOptions.plist` pages in the Xcode documentation section.

Secondary sources used for context (not a primary source, labeled separately in the text): GitHub fastlane discussion #21347 (altool deprecation), Bitrise/Capgo/Xojo blog posts on the privacy manifest, Unity Support Help Center (IL2CPP build size optimizations), Reddit/StackOverflow/forum discussions aggregated via web search.
