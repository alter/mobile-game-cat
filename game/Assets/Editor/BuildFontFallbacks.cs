using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

/// <summary>
/// Gives the game its own fonts for the scripts it cannot otherwise draw.
///
/// The game shipped without any: `PanelSettings.textSettings` was `fileID: 0`,
/// so every glyph came from Unity's built-in face or, for anything that face
/// lacks, from whatever the operating system happened to have. On Android that
/// worked for all seventeen languages — the glyph harness (`glyphs.txt`) proved
/// it on a device on 2026-08-29, and the expectation that it would fail was
/// wrong.
///
/// On the iOS simulator the same build, the same day:
///
///   Thai                  ▢▢▢▢▢▢▢▢▢▢▢▢▢   nothing at all
///   Chinese, simplified   房▢干▢了            间 and 净 missing
///
/// Thai failed with the Thai font sitting right there in the runtime image
/// (`Thonburi.ttc`), so this is not "the simulator has fewer fonts" — Unity
/// simply did not reach it. And the Han glyphs are being served by a Japanese
/// face, which is why traditional Chinese is whole and simplified is not: the
/// simplified-only forms are not in it.
///
/// Undocumented, different on two platforms, and free to change in any update.
/// So the game carries its own, and this is what builds them.
///
/// Not the whole fonts — four CJK faces are 22 MB against a 50 MB build. Only
/// the glyphs the tables actually use, cut by `tools/fonts/subset.py` straight
/// from `Copy*.cs`: 862 characters, 870 KB for all seven.
///
/// This builds the font assets and nothing else. Hooking them up is
/// <see cref="CatShelter.Shell.FontFallbacks"/>'s job and happens at runtime,
/// for a reason recorded there: the panel's text settings are created lazily
/// when a panel first exists, and in batch mode — where this script runs —
/// there is no panel and the property answers null.
///
/// They go in as **fallbacks**, not as the game's font. The face every screen
/// was designed and screenshotted against stays exactly where it is; these are
/// consulted only for a character it does not have. A German player's build
/// renders identically to yesterday's.
///
///   Unity -batchmode -quit -projectPath game \
///         -executeMethod BuildFontFallbacks.Apply -logFile fonts.log
///
/// Re-run after adding a language or editing a table: a glyph nobody subset for
/// is a glyph nobody can draw.
/// </summary>
public static class BuildFontFallbacks
{
    private const string FontDir = "Assets/Resources/Fonts";

    /// <summary>
    /// Sampling size for the atlas. 90 is Unity's own default for a dynamic
    /// font asset; the largest text in the game is the win card's title at 34
    /// points, so this has room and still rasterises on demand rather than up
    /// front.
    /// </summary>
    private const int SamplingPointSize = 90;

    private const int AtlasPadding = 9;
    private const int AtlasSize = 1024;

    public static void Apply()
    {
        var sources = AssetDatabase.FindAssets("t:Font", new[] { FontDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .ToList();

        if (sources.Count == 0)
        {
            Debug.LogError($"[Fonts] no fonts in {FontDir} — run tools/fonts/subset.py first");
            return;
        }

        var fallbacks = new List<FontAsset>();
        foreach (var path in sources)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null) continue;

            var assetPath = Path.ChangeExtension(path, null) + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath);
            if (existing != null)
            {
                fallbacks.Add(existing);
                Debug.Log($"[Fonts] kept {Path.GetFileName(assetPath)}");
                continue;
            }

            // Dynamic, not static: the atlas is filled with the glyphs a run
            // actually asks for. A static atlas would bake all 862 up front,
            // and a player reading one language would carry six others' glyphs
            // as pixels.
            var asset = FontAsset.CreateFontAsset(
                font, SamplingPointSize, AtlasPadding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

            if (asset == null)
            {
                Debug.LogError($"[Fonts] could not build a font asset from {path}");
                continue;
            }

            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, assetPath);
            // The atlas texture and material are sub-assets; without this they
            // are lost on the next reimport and the font draws nothing.
            if (asset.atlasTextures != null)
                foreach (var texture in asset.atlasTextures)
                    if (texture != null) AssetDatabase.AddObjectToAsset(texture, asset);
            if (asset.material != null)
                AssetDatabase.AddObjectToAsset(asset.material, asset);

            fallbacks.Add(asset);
            Debug.Log($"[Fonts] built {Path.GetFileName(assetPath)}");
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[Fonts] done, {fallbacks.Count} fallbacks: " +
                  string.Join(", ", fallbacks.Select(f => f.name)));
    }
}
