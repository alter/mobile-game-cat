using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class BuildScript
{
    // Task 90-android/??, night of 2026-08-31 into 09-01. The owner reported
    // two photographs failing on his phone. The fix for exactly those two had
    // been pushed twenty minutes earlier, and neither of us could tell whether
    // the APK in his hand was built before or after it — he installs by hand,
    // there is no version on screen or in the package, and we spent the
    // exchange on that instead of on the bug.
    //
    // `bundleVersion` rather than a separate settings file or a build number
    // counter: Application.version reads it at runtime with no plumbing, and
    // it lands in the APK manifest, so `adb shell dumpsys package
    // com.sootpaw.game | grep versionName` answers the question without even
    // launching the app. Local date-time plus the short commit hash, because
    // a build number alone tells nobody which commit it was and a bare hash
    // tells nobody when it was built relative to a report timestamped in a
    // chat log.
    //
    // MUST NEVER FAIL A BUILD. A stamp that is wrong, or absent, still lets
    // the game ship; a build that throws while stamping ships nothing. Every
    // failure path below falls back to something honest rather than an
    // exception: `git` missing, `git` erroring, or the process call itself
    // throwing (no shell, no PATH, whatever) all land on the same "nogit"
    // marker with a logged warning, not a stopped build.
    private static void StampVersion()
    {
        string hash;
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            hash = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            if (proc.ExitCode != 0 || string.IsNullOrEmpty(hash))
            {
                Console.WriteLine("[BuildScript] WARNING git rev-parse --short HEAD " +
                                  $"exit={proc.ExitCode}, stamping 'nogit' instead");
                hash = "nogit";
            }
        }
        catch (Exception e)
        {
            // No git binary, no PATH, the process could not start at all —
            // whatever it is, the build goes on without knowing the commit.
            Console.WriteLine("[BuildScript] WARNING could not run git " +
                              $"rev-parse: {e.Message}, stamping 'nogit' instead");
            hash = "nogit";
        }

        try
        {
            PlayerSettings.bundleVersion = $"{DateTime.Now:MM-dd HH:mm} {hash}";
        }
        catch (Exception e)
        {
            // Belt-and-braces: even setting the field must not stop a build.
            Console.WriteLine($"[BuildScript] WARNING could not set bundleVersion: {e.Message}");
        }
    }

    // Headless build entry: -executeMethod BuildScript.BuildOSXPlayer
    public static void BuildOSXPlayer()
    {
        var report = BuildPipeline.BuildPlayer(
            SceneList(),
            "build/osx/CatShelter.app",
            BuildTarget.StandaloneOSX,
            BuildOptions.None);

        var result = report.summary.result;
        Console.WriteLine($"[BuildScript] result={result} " +
                          $"size={report.summary.totalSize} errors={report.summary.totalErrors}");
        if (result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Build failed: " + result);
    }

    /// <summary>The single boot scene; the UI is assembled from code at runtime.</summary>
    private static string[] SceneList() => new[] { "Assets/Scenes/Empty.unity" };

    // Android, task 90-android/02. Two entry points because they answer two
    // different questions: an .apk goes on a phone in front of you, an .aab
    // goes to Play and cannot be installed directly.
    //
    // ARM64 only, IL2CPP: Play requires 64-bit, and adding ARMv7 doubles build
    // time for devices this audience does not have.
    //
    // Minimum API 25 because Unity 6.3 refuses anything lower — "Minimum
    // supported Android API level is 25 (Android 7.1 Nougat)". Asking for 24
    // does not fail the build, it logs an error and silently uses 25, which is
    // the kind of thing that looks like a decision in the source and is not.

    public static void BuildAndroidPlayer()
    {
        ConfigureAndroid();
        BuildAndroid("build/android/CatShelter.apk", aab: false);
    }

    public static void BuildAndroidBundle()
    {
        ConfigureAndroid();
        BuildAndroid("build/android/CatShelter.aab", aab: true);
    }

    /// <summary>
    /// Make <paramref name="target"/> the ACTIVE build target before building
    /// for it, rather than only passing it to BuildPlayer.
    ///
    /// This is not housekeeping. Editor code that hooks the build is guarded by
    /// the platform define — com.unity.mobile.notifications opens its
    /// AndroidNotificationPostProcessor with `#if UNITY_ANDROID` — and in the
    /// editor those defines follow the ACTIVE target, which is settled before
    /// any of this runs. Handing BuildTarget.Android to BuildPlayer while the
    /// active target is still iOS produces an APK whose Android editor
    /// callbacks never existed. On 2026-08-27 that shipped a build with the
    /// notification Java classes present in classes.dex and neither the
    /// POST_NOTIFICATIONS permission nor the UnityNotificationManager receiver
    /// in the manifest — so nothing could ever be delivered — and the build
    /// reported success. It was found by dumping the APK's manifest, not by
    /// reading the log, because there was nothing in the log to read.
    /// </summary>
    private static void UseTarget(BuildTargetGroup group, BuildTarget target)
    {
        if (EditorUserBuildSettings.activeBuildTarget == target)
            return;
        Console.WriteLine("[BuildScript] switching active build target " +
                          $"{EditorUserBuildSettings.activeBuildTarget} -> {target}");
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            throw new Exception($"could not switch active build target to {target}");
    }

    private static void ConfigureAndroid()
    {
        StampVersion();
        UseTarget(BuildTargetGroup.Android, BuildTarget.Android);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        // 33 (Android 13) since 2026-08-29, up from 25 (Android 7.1, 2016).
        //
        // Set here AND in ProjectSettings.asset, and the pair is a trap: this
        // line runs before every build and overwrites the asset, so editing the
        // asset alone changes nothing and the APK keeps shipping the old floor.
        // That is exactly what happened when the floor was first raised — the
        // committed value said 33 and every build still declared 25.
        //
        // Why 33 rather than higher or lower. It is the highest level that buys
        // anything: the system photo picker is native there, so no Play-services
        // backport path exists to write or test, and the media permissions split
        // there, so the game can ask for NO storage permission at all
        // (60-shell-build/17-permission-audit). 34 and 35 buy nothing we use.
        // The cost is real and worth stating: by April 2026 figures a floor of
        // 33 reaches about 69% of devices where 30 reaches 87% — the steepest
        // single step on the whole scale.
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
        // Play rejects uploads built against an SDK more than a year behind;
        // "highest installed" keeps that from being a surprise at upload time.
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
    }

    private static void BuildAndroid(string path, bool aab)
    {
        EditorUserBuildSettings.buildAppBundle = aab;

        var report = BuildPipeline.BuildPlayer(
            SceneList(), path, BuildTarget.Android, BuildOptions.None);

        var summary = report.summary;
        Console.WriteLine($"[BuildScript] result={summary.result} " +
                          $"path={path} size={summary.totalSize} errors={summary.totalErrors}");
        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Android build failed: " + summary.result);
    }

    // Xcode project for iOS: -executeMethod BuildScript.BuildIOSXcodeProject
    // Output opens in Xcode: pick your device, set the team, press Play.
    public static void BuildIOSXcodeProject()
    {
        StampVersion();
        UseTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        var report = BuildPipeline.BuildPlayer(
            SceneList(),
            "build/ios/CatShelter",
            BuildTarget.iOS,
            BuildOptions.None);

        var result = report.summary.result;
        Console.WriteLine($"[BuildScript] result={result} errors={report.summary.totalErrors}");
        if (result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("iOS build failed: " + result);
    }

    // Xcode project against the simulator SDK: -executeMethod BuildScript.BuildIOSSimulatorProject
    // The device project cannot run in the simulator: its libraries are device-only,
    // so the simulator gets its own output folder and the SDK setting is restored afterwards.
    public static void BuildIOSSimulatorProject()
    {
        // The architecture enum is not part of the public API surface in every editor
        // version, so it is reached by name and left alone when absent.
        var archProp = typeof(PlayerSettings.iOS).GetProperty(
            "simulatorSdkArchitecture",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        UseTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        var sdk = PlayerSettings.iOS.sdkVersion;
        var arch = archProp?.GetValue(null);
        try
        {
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            if (archProp != null)
            {
                var names = Enum.GetNames(archProp.PropertyType);
                Console.WriteLine($"[BuildScript] simulator arch options: {string.Join(",", names)}");
                var arm = names.FirstOrDefault(n => n.ToLowerInvariant() == "arm64")
                          ?? names.FirstOrDefault(n => n.ToLowerInvariant().Contains("arm64"));
                if (arm != null)
                {
                    archProp.SetValue(null, Enum.Parse(archProp.PropertyType, arm));
                    Console.WriteLine($"[BuildScript] simulator arch {arch} -> {archProp.GetValue(null)}");
                }
            }

            var report = BuildPipeline.BuildPlayer(
                SceneList(),
                "build/ios-sim/CatShelter",
                BuildTarget.iOS,
                BuildOptions.None);

            var result = report.summary.result;
            Console.WriteLine($"[BuildScript] result={result} errors={report.summary.totalErrors}");
            if (result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("iOS simulator build failed: " + result);
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = sdk;
            if (archProp != null) archProp.SetValue(null, arch);
            AssetDatabase.SaveAssets();
        }
    }
}
