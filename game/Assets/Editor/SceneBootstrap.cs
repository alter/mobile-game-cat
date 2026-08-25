using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class SceneBootstrap
{
    // Menu + batch entry: -executeMethod SceneBootstrap.EnsureScene
    public static void EnsureScene()
    {
        // 1) PanelSettings asset, once
        const string panelPath = "Assets/Shell/PanelSettings.asset";
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
        if (panel == null)
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(390, 844); // iPhone class
            panel.match = 1f; // match width
            AssetDatabase.CreateAsset(panel, panelPath);
        }

        // 2) Scene with one GameObject: UIDocument + GameBoot
        var scene = EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);

        var go = new GameObject("Game");
        var uid = go.AddComponent<UIDocument>();
        uid.panelSettings = panel;
        go.AddComponent<CatShelter.Shell.GameBoot>();

        UnityEditor.EditorApplication.delayCall += () => { };
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Empty.unity");
        Debug.Log("[SceneBootstrap] scene written with UIDocument+GameBoot");
    }
}
