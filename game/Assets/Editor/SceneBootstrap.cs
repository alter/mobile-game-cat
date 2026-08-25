using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class SceneBootstrap
{
    // Menu + batch entry: -executeMethod SceneBootstrap.EnsureScene
    public static void EnsureScene()
    {
        // 1) PanelSettings asset — create once via CreateInstance+CreateAsset,
        // then ALWAYS reload through the asset database so we hold a persistent
        // reference (an instance created this session does not serialize into
        // the scene: assigning it leaves m_PanelSettings at fileID 0).
        const string panelPath = "Assets/Shell/PanelSettings.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(panelPath) == null)
        {
            var fresh = ScriptableObject.CreateInstance<PanelSettings>();
            fresh.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            fresh.referenceResolution = new Vector2Int(390, 844); // iPhone class
            fresh.match = 1f; // match width
            AssetDatabase.CreateAsset(fresh, panelPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        AssetDatabase.ImportAsset(panelPath);   // import first...
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath); // ...then load
        if (panel == null)
            throw new System.InvalidOperationException(
                $"PanelSettings failed to load from {panelPath}");
        Debug.Log($"[SceneBootstrap] panel loaded: {panel.name}");

        // 1b) Ensure the UXML is assigned as source asset in the scene below.
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/View/DebugGame.uxml");
        if (uxml == null)
            throw new System.InvalidOperationException(
                "Assets/View/DebugGame.uxml missing");

        // 2) Scene with one GameObject: UIDocument + GameBoot
        var scene = EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);

        var go = new GameObject("Game");
        var uid = go.AddComponent<UIDocument>();
        uid.visualTreeAsset = uxml;
        go.AddComponent<CatShelter.Shell.GameBoot>();

        // Assign last; re-load the asset because NewScene can invalidate
        // references held across the scene switch.
        panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
        if (panel == null)
            throw new System.InvalidOperationException("panel reload failed");
        uid.panelSettings = panel;
        Debug.Log($"[SceneBootstrap] post-assign: uid.panelSettings={(uid.panelSettings != null ? uid.panelSettings.name : "NULL")}");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Empty.unity");

        // Re-open check: reload the saved scene from disk and assert the link.
        var reloaded = EditorSceneManager.OpenScene("Assets/Scenes/Empty.unity");
        var doc = UnityEngine.Object.FindObjectOfType<UIDocument>();
        if (doc == null || doc.panelSettings == null)
            throw new System.InvalidOperationException(
                "saved scene lost PanelSettings link");
        Debug.Log("[SceneBootstrap] verified: panel link survives save/load");
        Debug.Log("[SceneBootstrap] scene written with UIDocument+GameBoot");
    }
}
