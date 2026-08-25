using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildScript
{
    // Headless build entry: -executeMethod BuildScript.BuildOSXPlayer
    // (iOS requires signing; macOS player verifies the whole pipeline.)
    public static void BuildOSXPlayer()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        if (scenes.Length == 0)
            scenes = new[] { "Assets/Scenes/Empty.unity" };

        var report = BuildPipeline.BuildPlayer(
            scenes,
            "build/osx/CatShelter.app",
            BuildTarget.StandaloneOSX,
            BuildOptions.None);

        var result = report.summary.result;
        Console.WriteLine($"[BuildScript] result={result} " +
                          $"size={report.summary.totalSize} errors={report.summary.totalErrors}");
        if (result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Build failed: " + result);
    }
}
