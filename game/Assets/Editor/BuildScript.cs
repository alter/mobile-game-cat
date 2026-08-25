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
}
