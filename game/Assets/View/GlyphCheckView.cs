using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Can this build draw the alphabets it claims to speak?
    ///
    /// The game went from two languages to seventeen on 2026-08-29, and the one
    /// thing that decides whether that is worth anything is not the translation
    /// — it is whether the glyphs come out at all. This project ships **no font
    /// of its own**: `PanelSettings.textSettings` is `fileID: 0`.
    ///
    /// The expectation that put this screen here was that Chinese, Japanese,
    /// Korean, Thai, Devanagari and Arabic would therefore come out as empty
    /// boxes, Russian having worked only because Cyrillic happens to sit in
    /// Unity's built-in face. **That expectation was wrong**, and this screen is
    /// what showed it: on Android every one of the seventeen draws, because
    /// Unity 6 borrows what it is missing from the operating system's own fonts.
    /// The screen stays because the borrowing is undocumented behaviour on two
    /// different operating systems, and the day it stops being true is a day
    /// nobody would otherwise notice.
    ///
    /// A missing glyph is not an error. Nothing is logged, nothing throws — the
    /// text is simply absent or drawn as a box, and only a screenshot says so.
    /// Hence a screen rather than a test.
    ///
    /// Reached like the coat harness and the capture screen: drop a `glyphs.txt`
    /// beside the save. A checking tool, not a screen in the game.
    ///
    ///   # Android
    ///   "$ADB" shell "touch /sdcard/Android/data/com.sootpaw.game/files/glyphs.txt"
    ///
    ///   # iOS simulator
    ///   D=$(xcrun simctl get_app_container booted com.sootpaw.game data)
    ///   touch "$D/Documents/glyphs.txt"
    ///
    /// The samples are deliberately NOT read from <see cref="Shell.Copy"/>: this
    /// has to answer the question before the tables exist, and it has to keep
    /// answering it if a table is later removed.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GlyphCheckView : MonoBehaviour
    {
        public static bool Requested =>
            File.Exists(Path.Combine(Application.persistentDataPath, "glyphs.txt"));

        /// <summary>
        /// One line per script the game means to speak. The text is the same
        /// sentence everywhere — "the room is clean" — so a line that comes out
        /// wrong is wrong about the alphabet and not about the words.
        /// </summary>
        private static readonly (string name, string sample)[] Samples =
        {
            ("English",     "The room is clean"),
            ("Русский",     "В комнате чисто. Ёлка, ъ, ы"),
            ("Español",     "La habitación está limpia"),
            ("Português",   "O quarto está limpo"),
            ("Français",    "La pièce est propre"),
            ("Deutsch",     "Das Zimmer ist sauber, groß"),
            ("Italiano",    "La stanza è pulita"),
            ("Türkçe",      "Oda temiz. İıŞşĞğÇçÖöÜü"),
            ("Indonesia",   "Ruangan ini bersih"),
            ("Tiếng Việt",  "Căn phòng đã sạch sẽ"),
            ("简体中文",     "房间干净了"),
            ("繁體中文",     "房間乾淨了"),
            ("日本語",       "部屋がきれいになりました"),
            ("한국어",       "방이 깨끗해졌어요"),
            ("ไทย",         "ห้องสะอาดแล้ว"),
            ("العربية",     "الغرفة نظيفة"),
            ("हिन्दी",        "कमरा साफ़ हो गया"),

            // Arabic is the one script where drawing the glyphs is not the
            // question. It reads right to left, and Unity's UI Toolkit is not
            // promised to reorder it; a build can show every letter perfectly
            // and still show the sentence backwards, which no screenshot of
            // ordinary Arabic settles unless you read Arabic.
            //
            // So: three alifs and a meem. An alif is a bare vertical stroke and
            // a meem is a small loop, and nothing else in the string competes
            // with either. Laid out correctly, the loop lands on the LEFT with
            // the three strokes to its right. Laid out left-to-right — the
            // failure — the strokes come first and the loop is on the RIGHT.
            // One glance, no Arabic needed, no room to argue.
            ("RTL probe",   "اااام"),
        };

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.style.backgroundColor = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 40;

            var title = new Label("glyphs: what this build can actually draw");
            title.style.fontSize = 14;
            title.style.color = (Color)new Color32(0x4A, 0x3B, 0x28, 0xFF);
            title.style.marginBottom = 10;
            root.Add(title);

            foreach (var (name, sample) in Samples)
                root.Add(Row(name, sample, advanced: false));

            // The same complex scripts again, through Unity 6's advanced text
            // generator. Only these four: for Latin and Cyrillic the two engines
            // agree, and thirty-six rows do not fit on a phone. The advanced one
            // is switched on per element rather than for the whole game — it is
            // a different engine with its own metrics, and turning it on
            // everywhere to fix one language would quietly re-lay out every
            // screen in the game.
            var sheet = Resources.Load<StyleSheet>("UI/AdvancedText");
            if (sheet != null) root.styleSheets.Add(sheet);
            Debug.Log($"[Glyphs] advanced stylesheet: {(sheet != null ? "loaded" : "MISSING")}");

            var heading = new Label(sheet != null
                ? "the same, through the advanced generator"
                : "advanced generator: stylesheet MISSING");
            heading.style.fontSize = 12;
            heading.style.marginTop = 14;
            heading.style.marginBottom = 4;
            heading.style.color = (Color)new Color32(0x7C, 0x6A, 0x52, 0xFF);
            root.Add(heading);

            foreach (var (name, sample) in Samples)
                if (name is "العربية" or "RTL probe" or "ไทย" or "हिन्दी")
                    root.Add(Row(name, sample, advanced: true));

            // And once more for Arabic with the paragraph direction set, which
            // is a second switch and not the same one: the advanced generator
            // shapes and reorders, `direction` says which way the line runs.
            // Three rows, so the failure can be told apart from the fix.
            var third = new Label("arabic: advanced + direction rtl");
            third.style.fontSize = 12;
            third.style.marginTop = 10;
            third.style.color = (Color)new Color32(0x7C, 0x6A, 0x52, 0xFF);
            root.Add(third);

            foreach (var (name, sample) in Samples)
                if (name is "العربية" or "RTL probe")
                    root.Add(Row(name, sample, advanced: true, rtl: true));

            // Which face is doing the drawing, in the picture itself. A screen
            // whose whole job is diagnosing fonts should not make the reader
            // guess which font it diagnosed.
            var settings = GetComponent<UIDocument>().panelSettings;
            var font = settings != null && settings.textSettings != null
                ? "textSettings assigned"
                : "no textSettings — Unity's built-in face";
            var note = new Label(font);
            note.style.fontSize = 10;
            note.style.marginTop = 12;
            note.style.color = (Color)new Color32(0x7C, 0x6A, 0x52, 0xFF);
            root.Add(note);

            Debug.Log($"[Glyphs] {Samples.Length} scripts drawn, {font}");
        }

        private static VisualElement Row(string name, string sample, bool advanced,
                                        bool rtl = false)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            // The label is written in its own script too, so a language whose
            // NAME comes out blank is spotted at a glance — the left column
            // fails and the right one does with it.
            var left = new Label(name);
            left.style.width = 92;
            left.style.fontSize = 12;
            left.style.color = (Color)new Color32(0x7C, 0x6A, 0x52, 0xFF);
            row.Add(left);

            var right = new Label(sample);
            right.style.fontSize = 17;
            right.style.color = (Color)new Color32(0x4A, 0x3B, 0x28, 0xFF);
            if (advanced) right.AddToClassList("advanced-text");
            if (rtl) right.AddToClassList("rtl");
            row.Add(right);
            return row;
        }
    }
}
