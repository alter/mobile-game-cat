using System;
using System.Linq;
using UnityEditor;
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
