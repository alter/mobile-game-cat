using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns on UI Toolkit's advanced text generator for the project.
///
/// Why, measured rather than assumed. On 2026-08-29 the game went from two
/// languages to seventeen, and the glyph harness (`glyphs.txt`, see
/// View/GlyphCheckView) showed on an Android device that **every script draws**
/// — Chinese, Japanese, Korean, Thai, Devanagari and Arabic all appear, because
/// Unity 6 quietly borrows the missing glyphs from the operating system's own
/// fonts. The prediction that the game would show empty boxes was wrong.
///
/// Arabic is the exception, and not for want of glyphs. The harness draws a
/// deliberate probe — four alifs and a meem, "اااام" — chosen because an alif is
/// a bare vertical stroke and a meem is a small loop, so nobody needs to read
/// Arabic to judge the result. Laid out correctly the loop sits on the LEFT.
/// On the device it sat on the RIGHT: the letters are all there and the sentence
/// is backwards.
///
/// The standard text generator does no bidirectional reordering. The advanced
/// one does — Unity 6 rebased its text back end on HarfBuzz and ICU — but it is
/// opt-in, and this project setting is the switch. See
/// https://docs.unity3d.com/6000.4/Documentation/Manual/ui-systems/enable-and-use-atg.html
///
/// **Switching this on does not change how the game draws today.** The setting
/// makes the generator available; which elements use it is decided per element
/// by the `-unity-text-generator` USS property, and only Arabic asks for it
/// (Resources/UI/AdvancedText.uss). Sixteen languages keep the engine they were
/// tested on. That is deliberate: the advanced generator has its own metrics,
/// and turning it on for everything to fix one language would silently re-lay
/// out every screen in the game.
///
/// Run it:
///   Unity -batchmode -quit -projectPath game \
///         -executeMethod EnableAdvancedText.Apply -logFile atg.log
///
/// Written through reflection because the settings class is internal to the
/// editor. It reads back what it set, so a version that moves or renames the
/// property fails loudly here instead of silently shipping backwards Arabic.
/// </summary>
public static class EnableAdvancedText
{
    private const string TypeName = "UnityEditor.UIElements.UIToolkitProjectSettings";
    private const string PropertyName = "enableAdvancedText";

    public static void Apply()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(TypeName, throwOnError: false))
            .FirstOrDefault(t => t != null);

        if (type == null)
        {
            Debug.LogError($"[AdvancedText] {TypeName} not found — this editor " +
                           "version keeps the setting somewhere else, and Arabic " +
                           "will render backwards until someone finds where");
            return;
        }

        var property = type.GetProperty(PropertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (property == null || !property.CanWrite)
        {
            Debug.LogError($"[AdvancedText] {TypeName}.{PropertyName} is not a " +
                           "settable static property in this editor version");
            return;
        }

        var before = property.GetValue(null);
        property.SetValue(null, true);
        var after = property.GetValue(null);

        // Read back, not just write: a setting that did not take is exactly the
        // kind of failure that shows up months later as a bug report in a
        // language nobody on the project reads.
        if (!(after is bool ok && ok))
        {
            Debug.LogError($"[AdvancedText] set failed — still {after}");
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AdvancedText] enableAdvancedText {before} -> {after}");
    }
}
