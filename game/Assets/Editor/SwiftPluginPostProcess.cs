#if UNITY_IOS
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// Unity copies Assets/Plugins/iOS/*.swift into the generated project but does
/// not configure Xcode to compile Swift. Without this the build fails on the
/// first .swift file with "Swift is not supported".
///
/// ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES stays NO on both targets: YES either
/// fails App Store validation with "disallowed file 'Frameworks'" or breaks the
/// build with "UnityFramework.h file not found"
/// (knowledge/ios/04-unity-swift-native-plugin.md).
/// </summary>
public class SwiftPluginPostProcess : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;

        var projectPath = PBXProject.GetPBXProjectPath(report.summary.outputPath);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);

        foreach (var guid in new[] { project.GetUnityFrameworkTargetGuid(),
                                     project.GetUnityMainTargetGuid() })
        {
            project.SetBuildProperty(guid, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(guid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            project.SetBuildProperty(guid, "CLANG_ENABLE_MODULES", "YES");
        }

        File.WriteAllText(projectPath, project.WriteToString());
    }
}
#endif
