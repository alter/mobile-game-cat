using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class BuildScript
{
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

    private static void ConfigureAndroid()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
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
