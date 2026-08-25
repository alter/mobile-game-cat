using System.Linq;
using UnityEditor;
using UnityEngine;

static class SceneSetup
{
    [InitializeOnLoadMethod]
    static void EnsureSceneInBuild()
    {
        EditorApplication.delayCall += () =>
        {
            var scene = "Assets/Scenes/Empty.unity";
            var current = EditorBuildSettings.scenes;
            if (!current.Any(s => s.path == scene))
            {
                var list = current.ToList();
                list.Insert(0, new EditorBuildSettingsScene(scene, true));
                EditorBuildSettings.scenes = list.ToArray();
                Debug.Log($"[SceneSetup] added {scene} to build");
            }
        };
    }
}
