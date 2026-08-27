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

        public string CurrentName => _nameField?.value;

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

            var art = CoatBuilder.LoadBase(traits, State);
            if (art != null)
            {
                // Untinted silhouette rather than no screen at all: this is the
                // moment she meets her cat, and a coat that will not build must
                // not cost her the name field with it.
                var built = CoatBuilder.TryBuild(art, traits, State);
                portrait.style.backgroundImage = new StyleBackground(built != null ? built : art);
            }

            // TextField has no built-in placeholder on the UI Toolkit version
            // this project's other code already relies on (pickingMode /
            // RegisterCallback, both used as-is in DebugGameView.cs), so the
            // hint is a Label stacked on top, hidden the moment there is a
            // real value.
            var nameWrap = new VisualElement();
            nameWrap.style.width = 220;
            nameWrap.style.marginBottom = 20;

            _nameField = new TextField { value = initialName ?? "" };
            _nameField.style.width = Length.Percent(100);

            var placeholder = new Label(Shell.Copy.Of("meet.name_placeholder"));
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 6;
            placeholder.style.top = 4;
            placeholder.style.color = new Color(ink.r, ink.g, ink.b, 0.45f);
            placeholder.pickingMode = PickingMode.Ignore;
            placeholder.style.display = string.IsNullOrEmpty(_nameField.value)
                ? DisplayStyle.Flex : DisplayStyle.None;
            _nameField.RegisterValueChangedCallback(evt =>
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue)
                    ? DisplayStyle.Flex : DisplayStyle.None);

            nameWrap.Add(_nameField);
            nameWrap.Add(placeholder);

            var confirm = new Button(Confirm) { text = Shell.Copy.Of("meet.confirm") };

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
            var cat = new Cat(_nameField.value, _traits);
            OnNamed?.Invoke(cat);
        }
    }
}
