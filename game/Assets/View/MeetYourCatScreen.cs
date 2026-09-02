using System;
using CatShelter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Task 50-photo/09: the moment the whole project exists for — the cat
    /// built from the player's own traits, and the name she gives it. Where
    /// CaptureScreen (50-photo/08) stops at handing over a CatTraits, this
    /// is what turns them into her cat.
    ///
    /// Built from code like CaptureScreen: UI Toolkit, no UXML, a Build(...)
    /// entry point and callbacks out.
    ///
    /// The name typed here must never appear on a shared image
    /// (DECISIONS.md D8, "content caution": "the name she typed must not
    /// appear on the shared image, or it becomes a public artifact next to
    /// the app's branding"). This screen is the one place that name is
    /// first captured, so whatever eventually builds the before/after share
    /// card (D8) has to leave it out on purpose — the rule starts mattering
    /// here, not there.
    /// </summary>
    public sealed class MeetYourCatScreen : MonoBehaviour
    {
        /// <summary>
        /// Nobody reaches this screen having already cleared a room — it is
        /// the first look at the cat — so the coat is always drawn at
        /// PlayerProgress.CatStateFor(0): freshly rescued, never tidied up.
        /// </summary>
        private const int State = 1;

        /// <summary>
        /// Reachable on its own for checking, the same convention
        /// CaptureScreen (`capture.txt`) and CoatGridView (`coat.txt`) use:
        /// drop a `meet.txt` beside the save.
        /// </summary>
        public static bool Requested =>
            System.IO.File.Exists(
                System.IO.Path.Combine(Application.persistentDataPath, "meet.txt"));

        /// <summary>
        /// Fired once the player confirms a name. The Cat already carries
        /// the trimmed/defaulted name (Cat's own constructor rule) over the
        /// traits this screen was built from; persisting it is the caller's
        /// job — the same division CaptureScreen.OnCatReady uses.
        /// </summary>
        public Action<Cat> OnNamed;

        private VisualElement _root;
        private TextField _nameField;
        private CatTraits _traits;

        /// <summary>
        /// What is in the field, exactly as it is drawn. For a cat the player
        /// has never renamed that is the TRANSLATED default — not
        /// <see cref="Cat.DefaultName"/>, which is what gets stored. See
        /// <see cref="Shown"/> for why the two differ; <see cref="Confirm"/>
        /// is what converts back.
        /// </summary>
        public string CurrentName => _nameField?.value;

        /// <summary>
        /// The stored name as the player should see it.
        ///
        /// 2026-08-29. `Cat.DefaultName` is the literal "Kitty" and stays
        /// that way in every language: it is part of the save format,
        /// `Core` is engine-free by a rule this project checks, so it cannot
        /// read `Shell.Copy`, and a save must mean the same cat after the
        /// phone's language changes. The defect a seventeen-language sweep
        /// found was not that the constant is English — it is that the
        /// constant was reaching the screen. So the swap happens here, at the
        /// one place the name is drawn, and only for a name that is still the
        /// untouched default: anything the player typed herself is hers and is
        /// shown back to her verbatim, whatever language it is in.
        /// </summary>
        private static string Shown(string stored) =>
            stored == Cat.DefaultName ? Shell.Copy.Of("cat.default_name") : stored;

        /// <summary>
        /// The inverse, applied on the way out so the two cannot drift.
        ///
        /// Without it, a player who opened this screen and confirmed without
        /// editing would have the translated name written into her save as a
        /// literal, and her cat would stop being the default cat — she would
        /// carry a Japanese name onto an English phone, and
        /// <see cref="Cat.Skipped"/> would no longer describe the same animal.
        /// Trimmed before comparing, because <see cref="Cat"/>'s own
        /// constructor trims and a trailing space would otherwise store a
        /// name one character different from the default.
        ///
        /// A player who deliberately types the translated name gets
        /// `Cat.DefaultName` stored instead of her own identical string. The
        /// outcome she can see is the same string on the same screen, so this
        /// costs her nothing.
        /// </summary>
        private static string Stored(string shown) =>
            shown?.Trim() == Shell.Copy.Of("cat.default_name") ? Cat.DefaultName : shown;

        /// <summary>
        /// Is the language the game is speaking written right to left?
        ///
        /// Asked the same way GameBoot asks it, and for the same reason it has
        /// to be asked twice: `Copy.For` answers English for any language with
        /// no table, so "are we in Arabic" is only a real question once "does
        /// Arabic exist" has been answered yes. Compared without it, every
        /// English player becomes an Arabic one the day the table is deleted.
        /// </summary>
        private static bool RightToLeft
        {
            get
            {
                var arabic = Shell.Copy.For(SystemLanguage.Arabic);
                return !ReferenceEquals(arabic, Shell.Copy.English)
                       && ReferenceEquals(Shell.Copy.Current, arabic);
            }
        }

        public void Build(VisualElement parent, CatTraits traits, string initialName = null)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            _traits = traits;

            _root = new VisualElement();
            // Absolute and painted: the screen sits over whatever the panel
            // already holds, and an unpainted root shows through as black.
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0.96f, 0.93f, 0.87f);
            _root.style.paddingLeft = 24;
            _root.style.paddingRight = 24;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;

            var ink = new Color(0.25f, 0.21f, 0.17f);

            var title = new Label(Shell.Copy.Of("meet.title"));
            title.style.fontSize = 26;
            title.style.marginBottom = 16;
            title.style.color = ink;

            var portrait = new VisualElement();
            portrait.style.width = 220;
            portrait.style.height = 220;
            portrait.style.marginBottom = 20;
            portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

            // TryBuildFor, at 256, like every other caller — not TryBuild on the
            // shipped 1024x1024 silhouette, which is what this screen did until
            // 2026-09-01.
            //
            // The portrait above is 220 points wide, so 1024 was four times more
            // than could ever be shown, and the cost is superlinear: `Outline`
            // dilates by 1.6 % of the width and scans a square window per pixel,
            // so halving the source cuts the work by roughly sixteen. The
            // project's own measurement of this exact call is recorded on
            // `CoatBuilder.Downscale`: 21.8 seconds for one coat at 1024.
            //
            // It ran on the main thread, on the screen where she meets her cat.
            // That is the likeliest reason the owner reported being thrown back
            // to the main screen when he picked a second photograph: a main
            // thread standing still that long is one Android is entitled to give
            // up on, and the picker activity dying reaches us as "she changed
            // her mind" (`CatPickActivity.onDestroy`). Likeliest, not proven —
            // it has not been reproduced here, and it is being fixed because
            // building four times the pixels that can be shown is wrong on its
            // own terms.
            //
            // TryBuildFor also remembers: in memory by traits, and on disk
            // across launches. Nothing on this screen ever needed the big one.
            //
            // Over frames since 2026-09-02 (60-shell-build/19), and the number
            // decided it rather than a feeling: one 256 coat measured 74 ms of
            // held main thread on emulator-5554, which is four frames at 60 Hz
            // on the one screen where she is watching her cat appear. Below a
            // frame it would have been left alone; it is not below a frame.
            //
            // So the silhouette goes up now and her colour arrives a few frames
            // later. This is also the untinted fallback: a coat that will not
            // build must not cost her the name field with it, so the silhouette
            // is painted first and simply stays if nothing replaces it.
            var art = CoatBuilder.LoadBase(traits, State);
            if (art != null)
            {
                portrait.style.backgroundImage = new StyleBackground(art);
            }

            // Coroutines need a live component. Nothing in the game builds this
            // screen on a disabled object, but a test that news one up would,
            // and it should get a cat rather than an exception — the
            // synchronous path is kept for exactly that.
            if (isActiveAndEnabled)
            {
                StartCoroutine(CoatBuilder.TryBuildForOverFrames(traits, State, 256, built =>
                {
                    if (built != null)
                        portrait.style.backgroundImage = new StyleBackground(built);
                }));
            }
            else
            {
                var built = CoatBuilder.TryBuildFor(traits, State, 256);
                if (built != null)
                    portrait.style.backgroundImage = new StyleBackground(built);
            }

            // TextField has no built-in placeholder on the UI Toolkit version
            // this project's other code already relies on (pickingMode /
            // RegisterCallback, both used as-is in DebugGameView.cs), so the
            // hint is a Label stacked on top, hidden the moment there is a
            // real value.
            var nameWrap = new VisualElement();
            nameWrap.style.width = 220;
            nameWrap.style.marginBottom = 20;

            // Shown, not stored: an unrenamed cat arrives here as the literal
            // "Kitty" (GameBoot passes `saved?.Name` on the way back in) and
            // must not be shown that way to a player reading Japanese.
            _nameField = new TextField { value = Shown(initialName) ?? "" };
            _nameField.style.width = Length.Percent(100);
            // The typed name starts from the side the language reads from. In
            // Arabic a name pinned to the left is a name in the wrong corner of
            // its own field, which is what the sweep photographed on 2026-08-29.
            // Set on the inner input as well as the field: the field is a
            // container, and the alignment that matters belongs to the element
            // actually drawing the text.
            if (RightToLeft)
            {
                _nameField.style.unityTextAlign = TextAnchor.MiddleRight;
                var input = _nameField.Q(TextField.textInputUssName);
                if (input != null) input.style.unityTextAlign = TextAnchor.MiddleRight;
            }

            var placeholder = new Label(Shell.Copy.Of("meet.name_placeholder"));
            placeholder.style.position = Position.Absolute;
            // Pinned to the side the language reads from, not always the left.
            // In Arabic the field's own text starts at the right edge, so a hint
            // at the left is a hint in the wrong place — caught by the
            // seventeen-language sweep on 2026-08-29, which also measured it two
            // points too tall for its box, hence the room below.
            if (RightToLeft) placeholder.style.right = 6;
            else placeholder.style.left = 6;
            placeholder.style.top = 2;
            placeholder.style.color = new Color(ink.r, ink.g, ink.b, 0.45f);
            placeholder.pickingMode = PickingMode.Ignore;
            placeholder.style.display = string.IsNullOrEmpty(_nameField.value)
                ? DisplayStyle.Flex : DisplayStyle.None;
            _nameField.RegisterValueChangedCallback(evt =>
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue)
                    ? DisplayStyle.Flex : DisplayStyle.None);

            nameWrap.Add(_nameField);
            nameWrap.Add(placeholder);

            // Through Buttons, not `new Button`: a bare one wears UI Toolkit's
            // default theme — a grey rectangle — which is what this screen and
            // the capture screen showed in all seventeen languages until the
            // sweep on 2026-08-29 photographed it. This is the moment she meets
            // her cat; the one control on it should not look like a debug stub.
            var confirm = Buttons.Primary(Shell.Copy.Of("meet.confirm"), Confirm);
            confirm.style.minWidth = 160;

            _root.Add(title);
            _root.Add(portrait);
            _root.Add(nameWrap);
            _root.Add(confirm);
            parent.Add(_root);
        }

        public void Show() => _root.style.display = DisplayStyle.Flex;
        public void Hide() => _root.style.display = DisplayStyle.None;

        private void Confirm()
        {
            // Cat's constructor is what turns a blank field into
            // Cat.DefaultName — the same rule the skip path (50-photo/10)
            // relies on, so a player who reaches this field and leaves it
            // empty still ends up with a named cat.
            //
            // Stored() is the other half of that: a field still holding the
            // translated default goes back to Cat.DefaultName, so the save
            // carries the canonical value in all seventeen languages and the
            // constant remains the single thing every other file compares
            // against.
            var cat = new Cat(Stored(_nameField.value), _traits);
            OnNamed?.Invoke(cat);
        }
    }
}
