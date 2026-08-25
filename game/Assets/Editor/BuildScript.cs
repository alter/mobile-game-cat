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
}
