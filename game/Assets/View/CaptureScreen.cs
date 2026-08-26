using System;
using System.Collections;
using CatShelter.Core;
using CatShelter.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Task 50-photo/08: the screen the whole concept rests on — photograph
    /// your cat, or pick one you already have.
    ///
    /// It owns the pipeline and nothing else: picker → Vision (05) → outcome
    /// (06) → crop (07) → the Worker call. Rendering the resulting cat is
    /// 09-meet-your-cat and skipping is 10-skip-default-cat; this screen stops
    /// at handing over traits.
    ///
    /// Built from code like the rest of the shell, so there is no scene asset
    /// to edit and no UXML to keep in step.
    /// </summary>
    public sealed class CaptureScreen : MonoBehaviour
    {
        /// <summary>What happens when a photo is finally accepted.</summary>
        public Action<byte[]> OnAccepted;

        /// <summary>Where a finished cat goes, however it was arrived at.</summary>
        public Action<CatTraits> OnCatReady;

        /// <summary>
        /// Ask the Worker for the coat. Injected, and null until
        /// 02-traits-worker exists — which is exactly the "unreachable" case
        /// 11-offline-fallback is about, so the fallback path is the one that
        /// runs today.
        /// </summary>
        public Func<byte[], CatTraits> AskWorker;

        /// <summary>Injected so a test can drive the pipeline without a device.</summary>
        public Func<byte[], VisionAnswer> Recognise = bytes => CatVision.Recognise(bytes);
        public Func<byte[], AnimalBox, byte[]> Crop = (bytes, box) => CatPhoto.Prepare(bytes, box);

        private VisualElement _root;
        private Label _message;
        private Button _camera;
        private Button _gallery;
        private Button _skip;
        private VisualElement _busy;

        public string CurrentMessage => _message?.text;
        public bool IsBusy => _busy != null && _busy.style.display == DisplayStyle.Flex;

        public void Build(VisualElement parent)
        {
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
            var title = new Label("Show us your cat");
            title.style.fontSize = 26;
            title.style.marginBottom = 8;
            title.style.color = ink;

            _message = new Label("A photo where she fills most of the frame works best.");
            _message.style.whiteSpace = WhiteSpace.Normal;
            _message.style.marginBottom = 20;
            _message.style.color = ink;
            _message.style.unityTextAlign = TextAnchor.MiddleCenter;

            _camera = new Button(() => Pick(fromCamera: true)) { text = "Take a photo" };
            _gallery = new Button(() => Pick(fromCamera: false)) { text = "Choose one I have" };

            // A camera the device does not have is not a button to grey out,
            // it is a button not to show: an iPad without one, or a simulator.
            _camera.style.display = CatPicker.CameraAvailable
                ? DisplayStyle.Flex : DisplayStyle.None;

            // Skipping is a supported path, not a dead end: the share of
            // players who skip is one of the numbers this project watches
            // (cat-shelter-mvp.md section 5), so the control has to be plainly
            // there rather than hidden away.
            _skip = new Button(Skip) { text = "Not now — give me a kitten" };

            _busy = new Label("Looking…");
            _busy.style.color = ink;
            _busy.style.display = DisplayStyle.None;
            _busy.style.marginTop = 12;

            _root.Add(title);
            _root.Add(_message);
            _root.Add(_camera);
            _root.Add(_gallery);
            _root.Add(_skip);
            _root.Add(_busy);
            parent.Add(_root);

            // photo:screen_shown counts players who REACHED this screen — the
            // first of the four go/no-go metrics, whose threshold is >90%.
            // Firing it after a photo is handled would count players who got
            // as far as picking one, which is metric two, and would make the
            // first metric look like the second.
            Analytics.PhotoScreenShown();
        }

        public void Show() => _root.style.display = DisplayStyle.Flex;
        public void Hide() => _root.style.display = DisplayStyle.None;

        private void Pick(bool fromCamera)
        {
            SetBusy(true, "Opening…");
            Action<byte[]> picked = bytes => StartCoroutine(Handle(bytes));
            Action<string> failed = reason =>
            {
                SetBusy(false);
                // "cancelled" is not a failure and must not read as one: she
                // changed her mind, which is allowed.
                _message.text = reason == "cancelled"
                    ? "No rush. Pick one whenever you like."
                    : $"That did not work: {reason}";
            };

            if (fromCamera) CatPicker.CaptureWithCamera(picked, failed);
            else CatPicker.PickFromGallery(picked, failed);
        }

        /// <summary>
        /// The pipeline. Public so a PlayMode test can push a stubbed photo
        /// through it without a picker.
        /// </summary>
        public IEnumerator Handle(byte[] photo)
        {
            SetBusy(true, "Looking…");
            yield return null;      // let the busy state paint before Vision blocks

            var answer = Recognise(photo);
            var best = answer.FoundAnimal ? answer.Best : default;
            var outcome = PhotoJudge.Judge(
                answer.FoundAnimal ? best.identifier : null,
                answer.FoundAnimal ? best.confidence : 0f);

            _message.text = PhotoMessages.For(outcome);

            if (!PhotoJudge.Accepts(outcome))
            {
                Analytics.PhotoRejected();
                SetBusy(false);
                yield break;
            }

            SetBusy(true, "Copying her colours…");
            yield return null;

            var prepared = Crop(photo, best);
            if (prepared == null)
            {
                // Vision said cat and the crop still failed: not the player's
                // doing, and not something she can fix by trying harder.
                _message.text = "Something went wrong on our side. Try that one again?";
                Analytics.PhotoRejected();
                SetBusy(false);
                yield break;
            }

            Analytics.PhotoUploaded();
            OnAccepted?.Invoke(prepared);

            CatTraits traits = null;
            if (AskWorker != null)
            {
                SetBusy(true, "Asking about her colours…");
                yield return null;
                try { traits = AskWorker(prepared); }
                catch (Exception e) { Debug.LogWarning($"[CaptureScreen] worker failed: {e.Message}"); }
            }

            if (traits == null)
            {
                // Task 6.11: no Worker, or it did not answer. Read the one
                // trait a phone can read and default the rest — never an error
                // screen, because the photo itself was fine.
                var colour = Shell.CatColour.Estimate(prepared);
                traits = colour != null
                    ? CatTraits.FromColourOnly(colour)
                    : CatTraits.Default;
            }

            OnCatReady?.Invoke(traits);
            SetBusy(false);
        }

        private void Skip()
        {
            // No camera, no network, no permission: the same cat every time,
            // so two players who skipped can talk about the same kitten.
            _message.text = "A kitten is waiting for you either way.";
            OnCatReady?.Invoke(CatTraits.Default);
        }

        private void SetBusy(bool busy, string text = null)
        {
            _busy.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            if (text != null) ((Label)_busy).text = text;
            _camera.SetEnabled(!busy);
            _gallery.SetEnabled(!busy);
            _skip.SetEnabled(!busy);
        }
    }
}
