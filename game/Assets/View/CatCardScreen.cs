using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Task 60-shell-build/15: tap the kitten, and she fills the screen —
    /// lying on her blanket with her bowl beside her, under the game's name,
    /// over one large Share button that opens the phone's own share sheet.
    ///
    /// Built from code like MeetYourCatScreen (50-photo/09) and CaptureScreen
    /// (50-photo/08): UI Toolkit, no UXML, a Build(...) entry point and
    /// callbacks out. The cat texture and the room texture arrive as arguments
    /// because the board owns them and knows which room she is in; a screen
    /// that went and loaded its own would have to duplicate that knowledge and
    /// would drift from it.
    ///
    /// The blanket and the bowl are the exception, and they are loaded here on
    /// purpose. They are not state — they do not depend on the save, the room
    /// or the coat — they are this card's own set dressing, and the board has
    /// no reason to know they exist. Two Resources.Load calls, once, on the
    /// first tap.
    ///
    /// It does not compose the shared picture either. <c>renderCard</c> is
    /// called on tap and hands back PNG bytes; whoever supplies it decides
    /// what is in them. What this screen contributes is the caption, in the
    /// player's language, out of Copy.cs.
    ///
    /// The cat's name is deliberately absent from this screen, and must stay
    /// absent from the rendered card: DECISIONS.md D8, "the name she typed
    /// must not appear on the shared image, or it becomes a public artifact
    /// next to the app's branding". Nothing on this screen reads a Cat, which
    /// is the cheapest way to keep that true.
    ///
    /// NOT RUN as part of this change — no Unity build, no device. Every
    /// number below is arithmetic against PanelSettings' 390x844 reference
    /// resolution, or measured off the art files, and has not been looked at
    /// on a screen.
    /// </summary>
    public sealed class CatCardScreen : MonoBehaviour
    {
        // The board's paper, the same value SafeArea paints the panel root
        // with, so the safe-area strip and this card are one surface rather
        // than a card floating on a slightly different cream.
        private static readonly Color Paper = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);
        private static readonly Color Ink = new Color(0.25f, 0.21f, 0.17f);

        // The card's own breathing room, before anything the notch adds.
        private const float PadX = 16f;
        private const float PadTop = 8f;
        private const float PadBottom = 12f;

        /// <summary>
        /// WHERE EACH PIECE OF ART STANDS ON THE GROUND, as a fraction of its
        /// own square frame's height, measured 2026-08-28 by walking the alpha
        /// channel of the shipped PNGs (`Resources/Art/`) and taking the last
        /// row with alpha &gt; 8:
        ///
        ///   coat_default_1.png  bbox y 33..244 of 256 -> foot 0.95
        ///   coat_default_2.png  bbox y 33..244 of 256 -> foot 0.95
        ///   coat_default_3.png  bbox y 115..244 of 256 -> foot 0.95
        ///   reward_blanket.png  bbox y 54..209 of 256 -> foot 0.82
        ///   reward_bowl.png     bbox y 49..213 of 256 -> foot 0.83
        ///
        /// The three coats agree on 0.95 even though they do not agree on
        /// anything else — state 3 is lying down and fills only the bottom
        /// HALF of her frame (0.45..0.95), states 1 and 2 stand and fill
        /// 0.13..0.95. That is exactly why the shipped screenshot showed a
        /// small cat floating in the middle of a huge empty box: ScaleToFit
        /// fits the FRAME, and half of state 3's frame is nothing. Anchoring
        /// on the foot and oversizing the frame is what puts her on the floor
        /// at a size worth looking at, whichever state she is in.
        ///
        /// If the coat bake ever re-frames the cat, these move with it.
        /// </summary>
        private const float CatFoot = 0.95f;
        private const float BlanketFoot = 0.82f;
        private const float BowlFoot = 0.83f;

        /// <summary>Fired when the player closes the card. Whoever built it
        /// decides whether that means Hide() or Destroy().</summary>
        public Action OnClose;

        /// <summary>
        /// Fired once per tap on Share, before the sheet opens — the moment
        /// the old task's `share_tap` analytics hook belongs at. It is a tap,
        /// not a completed share: neither iOS nor Android reports which target
        /// took the picture, so an event named for a share landing would be a
        /// number nobody can honour. Shape owned by 70-analytics.
        /// </summary>
        public Action OnShareTapped;

        private VisualElement _root;
        private VisualElement _panelTop;
        private VisualElement _stage;
        private VisualElement _kitten;
        private VisualElement _blanket;
        private VisualElement _bowl;
        private Func<byte[]> _renderCard;

        /// <param name="parent">Panel root. The card covers it whole and pads
        /// itself off <see cref="Screen.safeArea"/>; see SafeAreaPad.</param>
        /// <param name="cat">The kitten as she is now — coat built, current
        /// state. Drawn over the room, not composited into it.</param>
        /// <param name="room">The room she is in. May be null while 40-art/07
        /// is outstanding: the stage then shows paper, and the card is a
        /// portrait rather than an empty frame.</param>
        /// <param name="renderCard">Called on each Share tap, returns the PNG
        /// to hand the system. 1080x1080 — see NOTES.md.</param>
        public void Build(VisualElement parent, Texture2D cat, Texture2D room,
                          Func<byte[]> renderCard)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (renderCard == null) throw new ArgumentNullException(nameof(renderCard));
            _renderCard = renderCard;

            _panelTop = parent;
            while (_panelTop.parent != null) _panelTop = _panelTop.parent;

            _root = new VisualElement();
            // Over everything: absolute and painted. An unpainted root shows
            // through as black, and the board is still alive underneath.
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = Paper;
            _root.style.paddingLeft = PadX;
            _root.style.paddingRight = PadX;
            _root.style.paddingTop = PadTop;
            _root.style.paddingBottom = PadBottom;
            _root.style.alignItems = Align.Center;

            _root.Add(Header());
            _root.Add(Stage(cat, room));
            _root.Add(ShareButton());

            parent.Add(_root);

            // Same pattern, and the same reason, as DebugGameView.FillScreen:
            // once now, again whenever the geometry moves, and again on a timer
            // until layout has actually happened.
            SafeAreaPad();
            _root.RegisterCallback<GeometryChangedEvent>(_ => { SafeAreaPad(); });
            _root.schedule.Execute(SafeAreaPad).Every(100).Until(
                () => !float.IsNaN(_panelTop.resolvedStyle.width)
                      && _panelTop.resolvedStyle.width > 0f);
        }

        public void Show() => _root.style.display = DisplayStyle.Flex;
        public void Hide() => _root.style.display = DisplayStyle.None;

        // --- the notch -------------------------------------------------------

        /// <summary>
        /// Hold the card's contents clear of the notch, the Dynamic Island and
        /// the home indicator.
        ///
        /// THE FAULT this fixes: in `ios-cat-card.png` the title "Cat Shelter"
        /// sits under the Dynamic Island. `Shell/SafeArea` does pad the panel
        /// root, but this card is an absolutely positioned child pinned at
        /// 0/0/0/0 and it was reaching the glass anyway. DebugGameView.FillScreen
        /// hit the mirror image of this and wrote down why: SafeArea applies its
        /// padding from Update, retrying until layout is ready, so anything
        /// positioned before that misses it and nothing recomputes it. Its cure
        /// was to compute the inset from `Screen.safeArea` itself rather than
        /// read it back off the parent, and that is the cure here.
        ///
        /// WHAT I DID NOT ASSUME. FillScreen's evidence (a room element pinned
        /// at 0/0/0/0 that needed NEGATIVE insets to escape the padding) says
        /// UI Toolkit lays an absolute child out inside the parent's padding.
        /// This card is pinned identically and the screenshot says it was NOT
        /// inset. Both cannot be true, and I cannot run the thing to settle it.
        /// So nothing here assumes: it MEASURES how far the card's own edge
        /// already sits inside the panel's, and adds only the shortfall. If the
        /// parent's padding does apply, the shortfall is zero and the padding is
        /// not paid twice; if it does not, the card pays it in full. Both cases
        /// land in the same place, which is the point.
        ///
        /// Screen pixels are not panel units — same arithmetic as SafeArea and
        /// FillScreen, the panel's width against the screen's giving the factor.
        /// </summary>
        private void SafeAreaPad()
        {
            if (_root == null || _panelTop == null) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var panelWidth = _panelTop.resolvedStyle.width;
            if (float.IsNaN(panelWidth) || panelWidth <= 0f) return;
            var scale = panelWidth / Screen.width;

            var area = Screen.safeArea;
            // Screen.safeArea is bottom-up, padding is top-down: the top inset
            // is what lies above the rectangle, the bottom inset is its own y.
            var wantTop = (Screen.height - area.yMax) * scale;
            var wantBottom = area.yMin * scale;
            var wantLeft = area.xMin * scale;
            var wantRight = (Screen.width - area.xMax) * scale;

            var mine = _root.worldBound;
            var panel = _panelTop.worldBound;
            if (float.IsNaN(mine.width) || mine.width <= 0f) return;

            _root.style.paddingTop = PadTop + Mathf.Max(0f, wantTop - (mine.yMin - panel.yMin));
            _root.style.paddingBottom = PadBottom + Mathf.Max(0f, wantBottom - (panel.yMax - mine.yMax));
            _root.style.paddingLeft = PadX + Mathf.Max(0f, wantLeft - (mine.xMin - panel.xMin));
            _root.style.paddingRight = PadX + Mathf.Max(0f, wantRight - (panel.xMax - mine.xMax));
        }

        // --- the pieces ----------------------------------------------------

        private VisualElement Header()
        {
            var header = new VisualElement();
            header.style.width = Length.Percent(100);
            // As tall as the button in it: Apple's 44pt hit-region floor, so
            // the row cannot squeeze the target it holds.
            header.style.height = Buttons.MinTarget;
            header.style.flexShrink = 0;
            header.style.justifyContent = Justify.Center;
            header.style.marginBottom = Buttons.Gap;

            // The game's name, on the screen for the same reason it is on the
            // rendered card: this is the thing being shown off, and a picture
            // of somebody's cat with no name on it is a picture of a cat.
            //
            // Padded off both edges by more than the Back button is wide, so
            // the title stays optically centred in the row and can never run
            // under the button — which an absolutely positioned button in a
            // row of unknown width otherwise permits.
            var name = new Label(Shell.Copy.Of("card.game_name"));
            name.style.fontSize = 22;
            name.style.color = Ink;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.paddingLeft = 96;
            name.style.paddingRight = 96;
            name.pickingMode = PickingMode.Ignore;
            header.Add(name);

            // Top LEFT, not top right. It is labelled Back, and back is the
            // top-left corner on iOS; the old placement put it where Done or
            // Close would sit, which is a different promise.
            var close = Buttons.Secondary(Shell.Copy.Of("card.close"),
                                          () => OnClose?.Invoke());
            close.style.position = Position.Absolute;
            close.style.left = 0;
            close.style.top = 0;
            header.Add(close);

            return header;
        }

        private VisualElement Stage(Texture2D cat, Texture2D room)
        {
            // Takes whatever height is left between the header and the button.
            // The card is the picture; the picture should get the screen.
            _stage = new VisualElement();
            _stage.style.width = Length.Percent(100);
            _stage.style.flexGrow = 1;
            _stage.style.overflow = Overflow.Hidden;
            _stage.style.borderTopLeftRadius = 16;
            _stage.style.borderTopRightRadius = 16;
            _stage.style.borderBottomLeftRadius = 16;
            _stage.style.borderBottomRightRadius = 16;

            if (room != null)
            {
                _stage.style.backgroundImage = new StyleBackground(room);
                // ScaleAndCrop, not ScaleToFit: the room is a backdrop and
                // should reach all four edges. Letterbox bars inside a card
                // that is about to be posted read as a mistake.
                _stage.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }
            else
            {
                // 40-art/07 has not landed. Paper rather than black, and the
                // kitten still gets her screen.
                _stage.style.backgroundColor = new Color(0.92f, 0.88f, 0.80f);
            }

            // Order is depth. Blanket behind her, bowl in front, because a bowl
            // a cat could reach is nearer the viewer than the cat is.
            _blanket = Prop(Resources.Load<Texture2D>("Art/reward_blanket"));
            _kitten = Prop(cat);
            _bowl = Prop(Resources.Load<Texture2D>("Art/reward_bowl"));

            if (_blanket != null) _stage.Add(_blanket);
            if (_kitten != null) _stage.Add(_kitten);
            if (_bowl != null) _stage.Add(_bowl);

            _stage.RegisterCallback<GeometryChangedEvent>(_ => LayoutScene());
            return _stage;
        }

        /// <summary>A square, absolutely placed, holding one piece of art.
        /// Square because ScaleToFit inside a square box renders a square
        /// source at exactly the box's side, which is what makes the
        /// fractions in <see cref="LayoutScene"/> arithmetic rather than
        /// guesswork. All five PNGs involved are square.</summary>
        private static VisualElement Prop(Texture2D art)
        {
            if (art == null) return null;
            var e = new VisualElement();
            e.style.position = Position.Absolute;
            e.style.backgroundImage = new StyleBackground(art);
            e.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        /// <summary>
        /// Put the kitten on something, and make her big.
        ///
        /// The owner's complaint was two faults in one picture: she is small,
        /// and she is lying on nothing. Both are fixed from the same arithmetic,
        /// which needs the stage's real size and so cannot happen before layout
        /// — hence GeometryChangedEvent rather than fixed percentages.
        ///
        /// SIZE. Her frame is 1.06 of the stage's WIDTH, not a percentage of
        /// its height. Wider than the stage on purpose: the art never reaches
        /// its own frame edge (the widest coat fills 0.86 of it), so nothing is
        /// clipped, and the cat herself lands at about 0.91 of the card's width.
        /// She owns the card, which is what a card about her should look like.
        /// Against the old 15%/15%/72% box she is roughly one and a half times
        /// wider, and the empty half of her lying-down frame no longer costs
        /// anything, because the frame is anchored by her feet.
        ///
        /// GROUND. One line, a tenth of the stage's height up from its bottom
        /// edge, and every piece stands on it via its own foot fraction. Her
        /// paws land a little way up the blanket rather than level with its
        /// bottom edge, which is what reads as ON it instead of BEHIND it.
        /// </summary>
        private void LayoutScene()
        {
            if (_stage == null) return;
            var w = _stage.resolvedStyle.width;
            var h = _stage.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f) return;

            var ground = h * 0.10f;

            // Every frame is square, so one number sizes it. Clamped against
            // the stage's height as well as its width so a short stage (a small
            // phone in the hand of a player with large text) cannot push her
            // head off the top.
            var blanket = Mathf.Min(w * 0.82f, h * 0.62f);
            var kitten = Mathf.Min(w * 1.06f, h * 0.95f);
            var bowl = Mathf.Min(w * 0.30f, h * 0.24f);

            Place(_blanket, (w - blanket) * 0.5f, ground - blanket * (1f - BlanketFoot), blanket);

            // 0.16 of the blanket's frame up from the ground line: far enough
            // that her paws are on the folded top of it, not beside it.
            Place(_kitten, (w - kitten) * 0.5f,
                  ground + blanket * 0.16f - kitten * (1f - CatFoot), kitten);

            // Beside her and a touch nearer the viewer, at the right-hand edge.
            Place(_bowl, w - bowl * 0.99f,
                  ground - bowl * (1f - BowlFoot) - h * 0.02f, bowl);
        }

        private static void Place(VisualElement e, float left, float bottom, float side)
        {
            if (e == null) return;
            e.style.left = left;
            e.style.bottom = bottom;
            e.style.width = side;
            e.style.height = side;
        }

        private Button ShareButton()
        {
            var share = Buttons.Share(ShareLabel(), TapShare);
            share.style.width = Length.Percent(100);
            share.style.marginTop = Buttons.Gap;

            // Not hidden when Shell.Share.Available is false. That flag is
            // false only in the editor — every phone this ships to has a share
            // sheet — so hiding it would hide the button from the person
            // building the screen and from nobody else. CatPicker's camera
            // button is the opposite case and is rightly hidden: some real
            // devices genuinely have no camera.
            return share;
        }

        /// <summary>
        /// "Share", not "Share her".
        ///
        /// Apple: "Using title-style capitalization, consider starting the
        /// label with a verb to help convey the button's action"
        /// (https://developer.apple.com/design/human-interface-guidelines/buttons).
        /// The verb is the whole label here — the button sits under a
        /// full-screen picture of one cat, so "her" names something the player
        /// is already looking at.
        ///
        /// UGLY, AND ON PURPOSE. `Shell/Copy.cs` is not a file this task may
        /// touch, and its `card.share` still reads "Share her". So this asks
        /// for a key that does not exist yet: Copy.Of returns "[card.share_short]"
        /// for a miss, deliberately loud, and that bracket is the signal to fall
        /// back. The fallback is a hard-coded English word, which is a hole in
        /// the one thing Copy.cs exists to prevent, and it closes the moment
        /// somebody adds the key. NOTES.md carries that as a request.
        /// </summary>
        private static string ShareLabel()
        {
            var label = Shell.Copy.Of("card.share_short");
            return label.StartsWith("[") ? "Share" : label;
        }

        private void TapShare()
        {
            OnShareTapped?.Invoke();

            byte[] png;
            try
            {
                png = _renderCard();
            }
            catch (Exception e)
            {
                // The card is the least important thing on this screen. A
                // render that throws must not take the screen down with it,
                // and e.Message is diagnostic only — an OS or .NET string, not
                // copy, and not English on every device.
                Debug.LogWarning($"[CatCard] render_failed: {e.Message}");
                return;
            }

            // Two lookups, not one sentence assembled here: the caption is a
            // format string in the table ("... in {0}") and the game's name is
            // its only argument. A translator gets both, and neither is built
            // out of fragments in C#.
            Shell.Share.Image(png, Shell.Copy.Of("card.caption",
                                                 Shell.Copy.Of("card.game_name")));
        }
    }
}
