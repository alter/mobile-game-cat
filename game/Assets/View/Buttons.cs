using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// One place to make a button that does not look like a Windows 95 dialog.
    ///
    /// WHY THIS FILE EXISTS. `new Button()` in a runtime UI Toolkit panel picks
    /// up Unity's default runtime theme: a grey fill, a hairline border, square
    /// corners, and a height driven by the font. On a cream paper board that is
    /// a foreign object — see `tasks/60-shell-build/15-share-card/ios-cat-card.png`,
    /// where both buttons on the kitten's card are grey rectangles. Every screen
    /// that needs a button needs the same six overrides, so they live here once.
    ///
    /// THE NUMBERS, AND WHERE THEY COME FROM. Apple's Human Interface
    /// Guidelines, read 2026-08-28, quoted rather than remembered:
    ///
    ///  - <see cref="MinTarget"/> = 44. "As a general rule, a button needs a hit
    ///    region of at least 44x44 pt — in visionOS, 60x60 pt — to ensure that
    ///    people can select it easily, whether they use a fingertip, a pointer,
    ///    their eyes, or a remote."
    ///    https://developer.apple.com/design/human-interface-guidelines/buttons
    ///
    ///  - <see cref="Gap"/> = 12. "In general, it works well to add about 12
    ///    points of padding around elements that include a bezel."
    ///    https://developer.apple.com/design/human-interface-guidelines/accessibility
    ///
    ///  - Two levels, not three, and one prominent button per card. "Keep the
    ///    number of prominent buttons to one or two per view. Presenting too
    ///    many prominent buttons increases cognitive load" — and "Use style —
    ///    not size — to visually distinguish the preferred choice among
    ///    multiple options." (buttons, as above.) Hence <c>Primary</c>
    ///    (filled) and <c>Secondary</c> (outlined), which differ in fill
    ///    and not in height.
    ///
    ///  - Labels: "Using title-style capitalization, consider starting the
    ///    label with a verb to help convey the button's action." (buttons.)
    ///    That is why the share button says Share and not "Share her".
    ///
    /// WHAT APPLE DOES **NOT** SAY, so nothing below is dressed up as theirs:
    ///
    ///  - **Corner radius.** The current Buttons page gives no corner-radius
    ///    number for iOS at any size; its only numeric size table is visionOS
    ///    heights (28/32/44/52/64 pt) and even that omits radii. So
    ///    <see cref="Radius"/> = 12 is taken from THIS project — `DebugGame.uss`
    ///    already rounds `.game__hud` at 12 and `.game__card-button` at 10 —
    ///    not from Apple.
    ///  - **Type size.** Apple's Dynamic Type table for the default (Large)
    ///    size sits behind a JavaScript tab that would not render for either
    ///    fetcher used; only the xSmall column came back (Body 14, Headline 14).
    ///    <see cref="LabelSize"/> = 17 is therefore NOT cited to Apple. It is
    ///    the widely repeated Large-default body size and it sits sensibly
    ///    between this project's own 15px card body and 22px card title.
    ///  - **Button height.** No iOS height table exists on the page.
    ///    <see cref="PrimaryHeight"/> = 52 is a choice above the 44 floor,
    ///    because the primary is the one thing a card is asking for.
    ///
    /// COLOUR is this project's, from `DebugGame.uss` — not iOS blue. Contrast
    /// by the WCAG relative-luminance formula, computed rather than eyeballed:
    /// ink #332A1E on tan #C9A97C is 6.34:1, ink on cream #F6EEDC is 12.20:1.
    /// Both clear WCAG AA (4.5:1) for the 17px label; the tan pair also clears
    /// AAA for large text (4.5:1) but not AAA for body (7:1). Ink #332A1E is
    /// used rather than the deep brown #4A3B28 the old `.game__card-button`
    /// carried, which is only 4.85:1 on the same tan — a hair over the line.
    ///
    /// NOT RUN. No Unity build and no device in this change; every number below
    /// is arithmetic against the 390x844 reference panel and has never been
    /// looked at on a screen.
    /// </summary>
    public static class Buttons
    {
        // --- the palette, from DebugGame.uss -------------------------------
        public static readonly Color Ink = (Color)new Color32(0x33, 0x2A, 0x1E, 0xFF);
        public static readonly Color Tan = (Color)new Color32(0xC9, 0xA9, 0x7C, 0xFF);
        public static readonly Color Cream = (Color)new Color32(0xF6, 0xEE, 0xDC, 0xFF);
        public static readonly Color Paper = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);

        /// <summary>Apple's floor for a hit region, in points.</summary>
        public const float MinTarget = 44f;

        /// <summary>Apple's suggested padding around a bezelled control.</summary>
        public const float Gap = 12f;

        /// <summary>This project's radius, not Apple's — see the class note.</summary>
        public const float Radius = 12f;

        public const float PrimaryHeight = 52f;
        public const float LabelSize = 17f;

        /// <summary>
        /// The prominent one. Filled tan, ink label, full width wherever it is
        /// put. One per card.
        /// </summary>
        public static Button Primary(string label, Action onClick) =>
            Primary(label, onClick, glyph: null);

        private static Button Primary(string label, Action onClick, VisualElement glyph)
        {
            var b = Bare(onClick, Tan, Ink, borderWidth: 0f);
            b.style.height = PrimaryHeight;
            if (glyph != null) b.Add(glyph);
            var text = Text(label, Ink);
            if (glyph != null) text.style.marginLeft = 8;
            b.Add(text);
            return b;
        }

        /// <summary>
        /// The quiet one. Cream fill with an ink hairline — a real edge, not
        /// the theme's grey one — for Back, Cancel, and anything a player is
        /// not being steered towards.
        /// </summary>
        public static Button Secondary(string label, Action onClick)
        {
            var b = Bare(onClick, Cream, Ink, borderWidth: 2f);
            b.style.height = MinTarget;
            b.style.paddingLeft = b.style.paddingRight = 16;
            b.Add(Text(label, Ink));
            return b;
        }

        /// <summary>
        /// A <c>Primary</c> with the share glyph in front of the label —
        /// the square with the arrow rising out of it. Drawn, not imported: an
        /// SF Symbol cannot ship inside the app's own texture set, and a PNG of
        /// it would be one more asset to keep in step with the tint.
        /// </summary>
        public static Button Share(string label, Action onClick) =>
            Primary(label, onClick, ShareGlyph(Ink, 20f));

        // --- the share glyph ------------------------------------------------

        /// <summary>
        /// The share mark, built out of five painted rectangles, one of them
        /// turned 45 degrees. No texture, no font, no SF Symbol.
        ///
        ///  - the box: an element with left, right and bottom borders only, so
        ///    it draws as an open-topped tray;
        ///  - the box's lid: two short bars at the tray's top line with a gap
        ///    between them, which is the break the arrow passes through. Two
        ///    bars rather than a top border, because a border cannot have a
        ///    hole in it;
        ///  - the shaft: a 2-unit bar down the centre;
        ///  - the head: a square carrying ONLY its top and left borders — an
        ///    "L" lying on its back — turned 45 degrees about its own centre,
        ///    which lands the corner at the top and the two arms pointing down
        ///    and out. That is a chevron, and a chevron on a shaft is an arrow.
        ///
        /// The one API here worth checking was <c>IStyle.rotate</c> taking
        /// <c>new StyleRotate(new Rotate(new Angle(deg, AngleUnit.Degree)))</c>
        /// from C#, since this project had only ever set `rotate` from USS
        /// (`DebugGame.uss`, `rotate: -5deg` on a refused tile). It resolves:
        /// this file and CatCardScreen.cs compile clean against 6000.3.22f1's
        /// own `UnityEngine.UIElementsModule.dll` under Unity's Roslyn, with no
        /// errors and only the two `unityBackgroundScaleMode` deprecation
        /// warnings the card already carried. That is a compile and not a run —
        /// it says the spellings exist, and nothing about how any of it looks.
        ///
        /// Rotation sign: UI Toolkit's y axis points down, so a positive angle
        /// turns clockwise, which is what puts the corner at the top.
        /// </summary>
        /// <param name="tint">Stroke colour. Pass the label's colour.</param>
        /// <param name="side">Box side in panel units. 20 reads at 17px text.</param>
        public static VisualElement ShareGlyph(Color tint, float side)
        {
            const float Stroke = 2f;

            var g = new VisualElement();
            g.style.width = side;
            g.style.height = side;
            g.style.flexShrink = 0;
            // The button owns the tap; nothing in here should ever be a target.
            g.pickingMode = PickingMode.Ignore;

            // Where the box's top line sits, measured from the glyph's top.
            var lid = side * 0.40f;

            // the tray: left, right and bottom edges
            var tray = new VisualElement();
            tray.style.position = Position.Absolute;
            tray.style.left = 0;
            tray.style.right = 0;
            tray.style.top = lid;
            tray.style.bottom = 0;
            tray.style.borderLeftWidth = Stroke;
            tray.style.borderRightWidth = Stroke;
            tray.style.borderBottomWidth = Stroke;
            tray.style.borderTopWidth = 0;
            tray.style.borderLeftColor = tint;
            tray.style.borderRightColor = tint;
            tray.style.borderBottomColor = tint;
            tray.style.borderBottomLeftRadius = 3;
            tray.style.borderBottomRightRadius = 3;
            g.Add(tray);

            // the lid, in two pieces, with the arrow's width missing from the
            // middle. 0.30 each side leaves a gap of 0.40*side for the shaft
            // and for air either side of it.
            g.Add(Bar(0f, lid, side * 0.30f, Stroke, tint));
            g.Add(Bar(side * 0.70f, lid, side * 0.30f, Stroke, tint));

            // the shaft: from just under the glyph's top down into the tray
            var apex = side * 0.06f;
            var shaft = new VisualElement();
            shaft.style.position = Position.Absolute;
            shaft.style.left = (side - Stroke) * 0.5f;
            shaft.style.top = apex;
            shaft.style.width = Stroke;
            shaft.style.height = side * 0.46f;
            shaft.style.backgroundColor = tint;
            g.Add(shaft);

            // the head. A square of side h turned 45 degrees about its centre
            // puts its former top-left corner directly above that centre, at
            // h/sqrt(2). So to land the point at `apex`, the centre goes
            // apex + h*0.7071 down, and the element's top is half a side above
            // its centre.
            var h = side * 0.42f;
            var head = new VisualElement();
            head.style.position = Position.Absolute;
            head.style.width = h;
            head.style.height = h;
            head.style.left = (side - h) * 0.5f;
            head.style.top = apex + h * 0.7071f - h * 0.5f;
            head.style.borderTopWidth = Stroke;
            head.style.borderLeftWidth = Stroke;
            head.style.borderRightWidth = 0;
            head.style.borderBottomWidth = 0;
            head.style.borderTopColor = tint;
            head.style.borderLeftColor = tint;
            head.style.rotate = new StyleRotate(new Rotate(new Angle(45f, AngleUnit.Degree)));
            g.Add(head);

            return g;
        }

        private static VisualElement Bar(float left, float top, float width,
                                         float height, Color tint)
        {
            var bar = new VisualElement();
            bar.style.position = Position.Absolute;
            bar.style.left = left;
            bar.style.top = top;
            bar.style.width = width;
            bar.style.height = height;
            bar.style.backgroundColor = tint;
            return bar;
        }

        // --- the shared body -------------------------------------------------

        /// <summary>
        /// A Button with every trace of the default runtime theme overridden.
        ///
        /// Inline styles outrank USS in UI Toolkit, which is what makes this
        /// work — and also what breaks the theme's own `:hover` and `:active`
        /// rules, because those are USS and cannot win against an inline
        /// background. So the press state is re-added by hand below. Without
        /// that the button would be visually dead under a finger.
        /// </summary>
        private static Button Bare(Action onClick, Color fill, Color edge,
                                    float borderWidth)
        {
            var b = onClick == null ? new Button() : new Button(onClick);

            // A Button is a TextElement and draws `text` itself; leaving it
            // empty and adding a Label child is what allows an icon beside the
            // words at all.
            b.text = string.Empty;

            var s = b.style;
            s.backgroundColor = fill;
            s.borderTopWidth = s.borderBottomWidth = borderWidth;
            s.borderLeftWidth = s.borderRightWidth = borderWidth;
            s.borderTopColor = s.borderBottomColor = edge;
            s.borderLeftColor = s.borderRightColor = edge;
            s.borderTopLeftRadius = s.borderTopRightRadius = Radius;
            s.borderBottomLeftRadius = s.borderBottomRightRadius = Radius;

            // The theme gives every button a 3-unit margin all round. Screens
            // here place their own spacing; a hidden 3 makes every layout
            // arithmetic wrong by 6.
            s.marginTop = s.marginBottom = s.marginLeft = s.marginRight = 0;
            s.paddingTop = s.paddingBottom = 0;
            s.paddingLeft = s.paddingRight = Gap;

            // Apple's floor on both axes, whatever the caller does with height.
            s.minHeight = MinTarget;
            s.minWidth = MinTarget;

            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;
            s.justifyContent = Justify.Center;
            s.flexShrink = 0;

            Press(b);
            return b;
        }

        /// <summary>
        /// Press feedback, by hand, because the theme's cannot reach past the
        /// inline fill. Opacity rather than a scale or a second colour: it is
        /// one float, it needs no new struct whose C# spelling could differ
        /// between Unity versions, and it reads on any fill.
        ///
        /// PointerUp is not enough on its own — a finger that slides off the
        /// button never sends one to this element — so Leave and Cancel restore
        /// it too, or the button would be left dimmed.
        /// </summary>
        private static void Press(Button b)
        {
            b.RegisterCallback<PointerDownEvent>(_ => b.style.opacity = 0.72f);
            b.RegisterCallback<PointerUpEvent>(_ => b.style.opacity = 1f);
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.opacity = 1f);
            b.RegisterCallback<PointerCancelEvent>(_ => b.style.opacity = 1f);
        }

        private static Label Text(string label, Color colour)
        {
            var t = new Label(label);
            t.style.fontSize = LabelSize;
            t.style.color = colour;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.unityTextAlign = TextAnchor.MiddleCenter;
            t.pickingMode = PickingMode.Ignore;
            return t;
        }
    }
}
