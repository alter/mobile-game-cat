using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace CatShelter.Shell
{
    /// <summary>
    /// Hands the game's own face a list of places to look for a character it
    /// does not have.
    ///
    /// The game shipped with no font of its own. On Android that cost nothing:
    /// the glyph harness (`glyphs.txt`) showed on a device on 2026-08-29 that
    /// all seventeen languages draw, because Unity 6 quietly borrows what it is
    /// missing from the operating system's fonts.
    ///
    /// On the iOS simulator, same build, same day:
    ///
    ///   Thai                  ▢▢▢▢▢▢▢▢▢▢▢▢▢   nothing at all
    ///   Chinese, simplified   房▢干▢了            间 and 净 missing
    ///
    /// Thai failed with a Thai font sitting in the runtime image, so it is not
    /// that the simulator has fewer fonts — Unity did not reach it. The Han
    /// glyphs come from a Japanese face, which is why traditional Chinese is
    /// whole and simplified is full of holes.
    ///
    /// Undocumented, different on two platforms, free to change in an update.
    /// So the game carries its own: seven Noto faces cut down to the 862
    /// characters the tables actually use (`tools/fonts/subset.py`), 870 KB
    /// instead of 22 MB.
    ///
    /// **Added to the panel's fallback list, never as the game's face.** The
    /// default face is not touched, so every screen renders exactly as it did
    /// before; these are consulted only for a character it cannot draw. A
    /// German player's build is identical to yesterday's.
    ///
    /// Two dead ends are recorded here so nobody walks them again. Assigning a
    /// PanelTextSettings asset to PanelSettings needs the engine's own default
    /// face copied into it, and that default sits behind an internal property
    /// filled in lazily when a panel first exists — in batch mode, where the
    /// editor scripts run, it answers null, and naming a face by hand instead
    /// would redraw the whole game in a font nobody chose. Reading the root's
    /// `resolvedStyle.unityFontDefinition` answers null for both the asset and
    /// the legacy Font, measured on a device: nothing in this game sets a font
    /// on an element, so there is nothing there to read.
    ///
    /// What works is asking the engine for those same default settings at
    /// runtime, when a panel does exist, and appending. Verified on the iOS
    /// simulator: Thai went from thirteen empty boxes to text.
    /// </summary>
    public static class FontFallbacks
    {
        /// <summary>Beside the tables, so a language and its glyphs are added
        /// in one place. Order is the order they are consulted in.</summary>
        private static readonly string[] Faces =
        {
            "Fonts/NotoSansThai-Regular SDF",
            "Fonts/NotoSansSC-Regular SDF",
            "Fonts/NotoSansTC-Regular SDF",
            "Fonts/NotoSansJP-Regular SDF",
            "Fonts/NotoSansKR-Regular SDF",
            "Fonts/NotoSansArabic-Regular SDF",
            "Fonts/NotoSansDevanagari-Regular SDF",
        };

        private static bool _done;

        /// <summary>
        /// Call once, with the panel root. The work is scheduled rather than
        /// done here: the settings this needs are created when the panel first
        /// draws, and GameBoot runs before that.
        /// </summary>
        public static void Attach(VisualElement root)
        {
            if (_done || root == null) return;
            _done = true;
            root.schedule.Execute(() => Wire(root)).ExecuteLater(0);
        }

        private static void Wire(VisualElement root)
        {
            // Not the root's resolved style. Nothing in this game sets a font
            // on an element, so that answers null for both the asset and the
            // legacy Font — measured on a device, which is how this attempt
            // replaced the previous one. The face comes from the panel's own
            // text settings, and those are the engine's internal default.
            //
            // Read here rather than baked in at build time because the property
            // is filled in lazily, when a panel first needs it: in batch mode,
            // where the editor scripts run, it is null. At runtime a panel
            // exists by definition.
            var settings = typeof(PanelTextSettings).GetProperty(
                    "defaultPanelTextSettings",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as PanelTextSettings;

            if (settings == null)
            {
                // Not fatal on Android, where the OS borrowing works. Logged
                // rather than thrown: a game that draws Thai as boxes is worse
                // than one that draws it, and both are better than one that
                // does not start.
                Debug.LogWarning("[Fonts] could not reach the panel's text settings — " +
                                 "fallbacks not attached, non-Latin scripts are " +
                                 "at the mercy of the OS");
                return;
            }

            Debug.Log($"[Fonts] panel face: {settings.defaultFontAsset?.name ?? "null"}");

            var table = settings.fallbackFontAssets ??= new List<FontAsset>();
            var added = 0;
            foreach (var name in Faces)
            {
                var face = Resources.Load<FontAsset>(name);
                if (face == null)
                {
                    Debug.LogWarning($"[Fonts] missing {name} — run " +
                                     "BuildFontFallbacks.Apply after subsetting");
                    continue;
                }
                if (table.Contains(face)) continue;
                table.Add(face);
                added++;
            }

            Debug.Log($"[Fonts] {settings.defaultFontAsset?.name} + {added} fallbacks " +
                      $"({table.Count} in the table)");
        }
    }
}
