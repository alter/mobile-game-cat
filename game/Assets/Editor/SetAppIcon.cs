using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Puts one of the five delivered icons into the build, for every size iOS and
/// Android ask for.
///
/// Until 28.08 the project shipped with **no icon at all** — `m_BuildTargetIcons`
/// was empty, so a build wore Unity's grey placeholder. Five icons had been
/// delivered and sat unused in `Assets/Art/icons`.
///
/// **Which one is not mine to decide, and the art delivery says so** ("выбирается
/// опросом, не автором"). What can be decided by measurement is which ones
/// survive being 60 points wide, which is how an icon is actually seen.
/// Downscaled to 60×60 and measured:
///
///   icon   cat fills   contrast vs background
///   1        58%         3.03:1
///   2        49%         5.31:1   ← best separation
///   3        65%         3.02:1   ← biggest cat
///   4        42%         1.95:1   ← fails: ginger on terracotta, nearly one tone
///   5        50%         2.51:1
///
/// **icon_3 is wired provisionally**: at that size how much of the frame the cat
/// occupies matters more than fine detail, and its paws give the silhouette
/// something other than a circle. icon_4 should not be on the shortlist at all.
///
/// Change it in one place — `Chosen` below — and re-run.
///
///   Unity -batchmode -quit -projectPath game \
///         -executeMethod SetAppIcon.Apply -logFile icon.log
/// </summary>
public static class SetAppIcon
{
    private const string Chosen = "Assets/Art/icons/icon_3.png";

    /// <summary>Adaptive layers, made from <see cref="Chosen"/> by
    /// `tools/icons/make_adaptive.py`. Re-run that after changing the choice
    /// above, or the square icon and the round one show different cats.</summary>
    private const string Foreground = "Assets/Art/icons/icon_3_fg.png";
    private const string Background = "Assets/Art/icons/icon_3_bg.png";

    private static Texture2D Load(string path)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (t != null && AssetImporter.GetAtPath(path) is TextureImporter im && !im.isReadable)
        {
            im.isReadable = true;
            im.npotScale = TextureImporterNPOTScale.None;
            im.alphaIsTransparency = true;
            im.SaveAndReimport();
            t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return t;
    }

    public static void Apply()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Chosen);
        if (texture == null)
        {
            Debug.LogError($"[SetAppIcon] {Chosen} not found");
            return;
        }

        // The source is 1328×1328 and imported as a plain texture. Unity
        // resizes each icon slot itself, but it can only read the file if the
        // importer let it — the same readable trap that cost this project a
        // day on the coat.
        var path = AssetDatabase.GetAssetPath(texture);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        foreach (var platform in new[] { NamedBuildTarget.iOS, NamedBuildTarget.Android })
        {
            // The platform-icon API, not the legacy `SetIcons(IconKind)` one:
            // iOS and Android each ask for several *kinds* (application, notification,
            // adaptive foreground and so on) and many sizes inside each, and
            // the legacy call cannot reach them.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(platform))
            {
                var icons = PlayerSettings.GetPlatformIcons(platform, kind);
                foreach (var icon in icons)
                {
                    // EVERY layer, not just layer 0 — filling only the first put
                    // the cat behind Unity's grey cube, and that is exactly what
                    // the launcher showed on the first attempt: she was there,
                    // with the placeholder still sitting on top of her.
                    //
                    // Two layers means Android's adaptive icon, and it does not
                    // want the same square twice. Every launcher masks it to its
                    // own shape and shows only the middle of the canvas, which
                    // clipped her ears off. So the adaptive form gets purpose-made
                    // layers: the flat background colour behind, and her scaled
                    // into the safe middle in front. One layer means the plain
                    // square icon, which wants the art whole.
                    if (icon.maxLayerCount >= 2 && Adaptive())
                    {
                        icon.SetTexture(Load(Background), 0);
                        icon.SetTexture(Load(Foreground), 1);
                    }
                    else
                    {
                        for (int layer = 0; layer < icon.maxLayerCount; layer++)
                            icon.SetTexture(texture, layer);
                    }

                    bool Adaptive() => Load(Foreground) != null && Load(Background) != null;
                }
                PlayerSettings.SetPlatformIcons(platform, kind, icons);
                var layers = icons.Length > 0 ? icons[0].maxLayerCount : 0;
                Debug.Log($"[SetAppIcon] {platform.TargetName} {kind}: " +
                          $"{icons.Length} slots x {layers} layers");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetAppIcon] done, using {Chosen}");
    }
}
