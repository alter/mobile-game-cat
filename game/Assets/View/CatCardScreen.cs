using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Task 60-shell-build/15: tap the kitten, and she fills the screen —
    /// standing in the room she is in right now, under the game's name, over
    /// one large Share button that opens the phone's own share sheet.
    ///
    /// Built from code like MeetYourCatScreen (50-photo/09) and CaptureScreen
    /// (50-photo/08): UI Toolkit, no UXML, a Build(...) entry point and
    /// callbacks out. Nothing here loads an asset. The cat texture and the
    /// room texture arrive as arguments because the board owns them and knows
    /// which room she is in; a screen that went and loaded its own would have
    /// to duplicate that knowledge and would drift from it.
    ///
    /// It does not compose the shared picture either. <c>renderCard</c> is
    /// called on tap and hands back PNG bytes; whoever supplies it decides
    /// what is in them. What this screen contributes is the caption, in the
    /// player's language, out of Copy.cs — see NOTES.md for the four keys
    /// that must exist in that table before this compiles clean.
    ///
    /// The cat's name is deliberately absent from this screen, and must stay
    /// absent from the rendered card: DECISIONS.md D8, "the name she typed
    /// must not appear on the shared image, or it becomes a public artifact
    /// next to the app's branding". Nothing on this screen reads a Cat, which
    /// is the cheapest way to keep that true.
    ///
    /// NOT RUN as part of this change — no Unity build, no device. The
    /// numbers below are chosen against PanelSettings' 390x844 reference
    /// resolution and have never been looked at on a screen.
    /// </summary>
    public sealed class CatCardScreen : MonoBehaviour
    {
        // The board's paper, the same value SafeArea paints the panel root
        // with, so the safe-area strip and this card are one surface rather
        // than a card floating on a slightly different cream.
        private static readonly Color Paper = new Color32(0xF4, 0xEA, 0xD8, 0xFF);
        private static readonly Color Ink = new Color(0.25f, 0.21f, 0.17f);

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
        private Func<byte[]> _renderCard;

        /// <param name="parent">Panel root, already safe-area padded by
        /// Shell.SafeArea; an absolutely positioned child sits inside that
        /// padding, so this card clears the notch without asking.</param>
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

            _root = new VisualElement();
            // Over everything: absolute and painted. An unpainted root shows
            // through as black, and the board is still alive underneath.
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = Paper;
            _root.style.paddingLeft = 16;
            _root.style.paddingRight = 16;
            _root.style.paddingTop = 12;
            _root.style.paddingBottom = 20;
            _root.style.alignItems = Align.Center;

            _root.Add(Header());
            _root.Add(Stage(cat, room));
            _root.Add(ShareButton());

            parent.Add(_root);
        }

        public void Show() => _root.style.display = DisplayStyle.Flex;
        public void Hide() => _root.style.display = DisplayStyle.None;

        // --- the pieces ----------------------------------------------------

        private VisualElement Header()
        {
            var header = new VisualElement();
            header.style.width = Length.Percent(100);
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.Center;
            header.style.marginBottom = 12;

            // The game's name, on the screen for the same reason it is on the
            // rendered card: this is the thing being shown off, and a picture
            // of somebody's cat with no name on it is a picture of a cat.
            var name = new Label(Shell.Copy.Of("card.game_name"));
            name.style.fontSize = 22;
            name.style.color = Ink;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.flexGrow = 1;
            header.Add(name);

            var close = new Button(() => OnClose?.Invoke())
            {
                text = Shell.Copy.Of("card.close")
            };
            close.style.position = Position.Absolute;
            close.style.right = 0;
            close.style.top = 0;
            header.Add(close);

            return header;
        }

        private VisualElement Stage(Texture2D cat, Texture2D room)
        {
            // Takes whatever height is left between the header and the button.
            // The card is the picture; the picture should get the screen.
            var stage = new VisualElement();
            stage.style.width = Length.Percent(100);
            stage.style.flexGrow = 1;
            stage.style.overflow = Overflow.Hidden;
            stage.style.borderTopLeftRadius = 12;
            stage.style.borderTopRightRadius = 12;
            stage.style.borderBottomLeftRadius = 12;
            stage.style.borderBottomRightRadius = 12;

            if (room != null)
            {
                stage.style.backgroundImage = new StyleBackground(room);
                // ScaleAndCrop, not ScaleToFit: the room is a backdrop and
                // should reach all four edges. Letterbox bars inside a card
                // that is about to be posted read as a mistake.
                stage.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }
            else
            {
                // 40-art/07 has not landed. Paper rather than black, and the
                // kitten still gets her screen.
                stage.style.backgroundColor = new Color(0.92f, 0.88f, 0.80f);
            }

            if (cat != null)
            {
                var kitten = new VisualElement();
                // Standing on the floor of the room, not floating in the
                // middle of it: anchored to the bottom, sized as a share of
                // the stage so it holds on any phone.
                kitten.style.position = Position.Absolute;
                kitten.style.left = Length.Percent(15);
                kitten.style.right = Length.Percent(15);
                kitten.style.bottom = Length.Percent(6);
                kitten.style.height = Length.Percent(72);
                kitten.style.backgroundImage = new StyleBackground(cat);
                kitten.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                kitten.pickingMode = PickingMode.Ignore;
                stage.Add(kitten);
            }

            return stage;
        }

        private Button ShareButton()
        {
            var share = new Button(TapShare) { text = Shell.Copy.Of("card.share") };
            share.style.width = Length.Percent(100);
            share.style.height = 52;
            share.style.marginTop = 16;
            share.style.fontSize = 18;

            // Not hidden when Shell.Share.Available is false. That flag is
            // false only in the editor — every phone this ships to has a share
            // sheet — so hiding it would hide the button from the person
            // building the screen and from nobody else. CatPicker's camera
            // button is the opposite case and is rightly hidden: some real
            // devices genuinely have no camera.
            return share;
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
