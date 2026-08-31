using System;
using System.Linq;
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
        /// Anything that stopped a photograph becoming a cat, with the reason
        /// code the picker gave — for `capture-state.txt`, never for the player.
        ///
        /// Added 2026-08-31, and the reason it had to be is the point. The
        /// owner reported that the camera and the gallery both do nothing on
        /// his phone. Both paths were then driven on an emulator — plain, and
        /// again with "don't keep activities" on to kill the game's window
        /// while the picker was in front — and both worked every time. So the
        /// difference is his device, and the only thing that can describe it is
        /// the device itself.
        ///
        /// It could not. `capture-state.txt` recorded three things — a photo
        /// accepted, a cat ready, a cat named — every one of them a SUCCESS.
        /// A run that failed wrote nothing at all, so the file was empty
        /// precisely when it was needed, and looked the same as a run that
        /// never happened. `errors.txt` did not cover it either: a picker that
        /// declines is not an error and must not be logged as one.
        ///
        /// The reason code stays out of the copy. `photo.our_fault` is what the
        /// player reads, and that is deliberate — a code is a diagnostic, and
        /// one of them once arrived from native Swift as a raw system-language
        /// sentence (60-shell-build/16 VERIFY). This hook is the other half of
        /// that decision: the honest message on screen, the exact code on disk.
        /// </summary>
        public Action<string> OnTrouble;

        /// <summary>
        /// Ask the Worker for the coat. Injected, and null until
        /// 02-traits-worker exists — which is exactly the "unreachable" case
        /// 11-offline-fallback is about, so the fallback path is the one that
        /// runs today.
        /// </summary>
        public Func<byte[], CatTraits> AskWorker;

        /// <summary>
        /// Identifies this device to the Worker's rate limit (worker/src/index.ts
        /// keys on device_id, not IP). No source for a real one exists in the
        /// shipping app yet — wiring one up belongs to 08's HTTP call, not this
        /// screen. Left empty, TraitsRequest falls back to the same
        /// "anonymous" the Worker itself would choose.
        /// </summary>
        public string DeviceId = "";

        /// <summary>
        /// Task 50-photo/07: the exact JSON body POST /traits expects for the
        /// most recently accepted photo — built the moment the crop succeeds,
        /// so it exists before 08 has an HTTP client to send it with. Null
        /// until a photo is accepted.
        /// </summary>
        public string LastTraitsRequestJson { get; private set; }

        /// <summary>Injected so a test can drive the pipeline without a device.</summary>
        public Func<byte[], VisionAnswer> Recognise = bytes => CatVision.Recognise(bytes);
        public Func<byte[], AnimalBox, byte[]> Crop = (bytes, box) => CatPhoto.Prepare(bytes, box);

        private VisualElement _root;
        private Label _message;
        private Button _camera;
        private Button _gallery;
        private Button _skip;

        /// <summary>
        /// The waiting block: the stage's words with a bar sliding underneath
        /// them. See <see cref="BuildBusy"/> for what it replaced and why.
        /// </summary>
        private VisualElement _busy;
        private Label _busyWords;
        private VisualElement _busyBar;
        private IVisualElementScheduledItem _busyTick;

        /// <summary>
        /// Whether this device has a camera at all, remembered at Build time.
        /// <see cref="SetBusy"/> hides the three buttons while the pipeline
        /// runs and puts them back afterwards; without this, putting them back
        /// would conjure a camera button onto an iPad that has none.
        /// </summary>
        private bool _hasCamera;

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
            var title = new Label(Shell.Copy.Of("capture.title"));
            title.style.fontSize = 26;
            title.style.marginBottom = 8;
            title.style.color = ink;

            _message = new Label(Shell.Copy.Of("capture.hint"));
            _message.style.whiteSpace = WhiteSpace.Normal;
            _message.style.marginBottom = 20;
            _message.style.color = ink;
            _message.style.unityTextAlign = TextAnchor.MiddleCenter;

            // Through Buttons, not `new Button`. A bare Button wears UI
            // Toolkit's default theme — a grey rectangle with a grey hairline —
            // which is what this screen showed in all seventeen languages until
            // 2026-08-29, when the sweep photographed it. Buttons.cs exists so
            // that a control in this game looks like this game; the two screens
            // built after it were simply never moved over.
            //
            // Taking the photograph is the prominent one; the gallery is the
            // quiet alternative beside it.
            _camera = Buttons.Primary(Shell.Copy.Of("capture.camera"),
                                      () => Pick(fromCamera: true));
            _gallery = Buttons.Secondary(Shell.Copy.Of("capture.gallery"),
                                         () => Pick(fromCamera: false));
            _camera.style.marginBottom = Buttons.Gap;
            _gallery.style.marginBottom = Buttons.Gap;

            // A camera the device does not have is not a button to grey out,
            // it is a button not to show: an iPad without one, or a simulator.
            _hasCamera = CatPicker.CameraAvailable;
            _camera.style.display = _hasCamera
                ? DisplayStyle.Flex : DisplayStyle.None;

            // Skipping is a supported path, not a dead end: the share of
            // players who skip is one of the numbers this project watches
            // (cat-shelter-mvp.md section 5), so the control has to be plainly
            // there rather than hidden away.
            // Quiet, like the gallery: skipping is supported, not steered
            // towards. It was the only one of the three that already read as a
            // choice rather than a default control, and only by accident.
            _skip = Buttons.Secondary(Shell.Copy.Of("capture.skip"), Skip);

            BuildBusy(ink);

            _root.Add(title);
            _root.Add(_message);
            // The waiting block goes HERE — above the buttons, not below them.
            // See BuildBusy for the whole argument; the short version is that
            // the player's eye is on the control she just pressed, and the
            // block takes that control's place while the pipeline runs.
            _root.Add(_busy);
            _root.Add(_camera);
            _root.Add(_gallery);
            _root.Add(_skip);
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
            var which = fromCamera ? "camera" : "gallery";
            // Written BEFORE the picker is asked for, not after it answers,
            // because the failure this is chasing may be that it never answers
            // at all. A file that says "asked the camera" and then stops is the
            // difference between "the button did nothing" and "the button did
            // something and the something never came back" — and those two have
            // no fix in common.
            OnTrouble?.Invoke($"asked the {which}");
            SetBusy(true, Shell.Copy.Of("capture.opening"));
            Action<byte[]> picked = bytes =>
            {
                OnTrouble?.Invoke($"the {which} returned {bytes?.Length ?? 0} bytes");
                StartCoroutine(Handle(bytes));
            };
            Action<string> failed = reason =>
            {
                OnTrouble?.Invoke($"the {which} declined: {reason}");
                SetBusy(false);
                // "cancelled" is not a failure and must not read as one: she
                // changed her mind, which is allowed. Every other reason
                // code (CatPicker.cs: "unsupported"/"read_failed"/
                // "save_failed"/"no_window"/"unavailable") reduces to one
                // honest message rather than being shown verbatim — a code
                // is diagnostic, not copy, and one of them used to arrive
                // from native Swift as a raw, sometimes system-language,
                // sentence (60-shell-build/16 VERIFY).
                _message.text = reason == "cancelled"
                    ? Shell.Copy.Of("capture.cancelled")
                    : Shell.Copy.Of("photo.our_fault");
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
            SetBusy(true, Shell.Copy.Of("capture.looking"));
            yield return null;      // let the busy state paint before Vision blocks

            var answer = Recognise(photo);
            if (answer.Failed)
            {
                // 50-photo/05 VERIFY: Vision could not run at all - decode
                // failure, not iOS, or the request itself threw. Not a
                // judgement that there is no cat, so it does not get the "no
                // cat in this one" copy; same honest message as a crop
                // failure below, since neither is the player's doing.
                Debug.LogWarning($"[CaptureScreen] vision failed: {answer.error}");
                _message.text = Shell.Copy.Of("photo.our_fault");
                Analytics.PhotoRejected();
                SetBusy(false);
                yield break;
            }

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

            SetBusy(true, Shell.Copy.Of("capture.colours"));
            yield return null;

            var prepared = Crop(photo, best);
            if (prepared == null)
            {
                // Vision said cat and the crop still failed: not the player's
                // doing, and not something she can fix by trying harder.
                _message.text = Shell.Copy.Of("photo.our_fault");
                Analytics.PhotoRejected();
                SetBusy(false);
                yield break;
            }

            Analytics.PhotoUploaded();
            OnAccepted?.Invoke(prepared);

            try
            {
                LastTraitsRequestJson = TraitsRequest.BuildJson(prepared, DeviceId);
            }
            catch (ArgumentException e)
            {
                // Should not happen: Crop already enforces the same ceiling
                // TraitsRequest checks (Shell.CatPhoto.MaxBytes ==
                // TraitsRequest.MaxPreEncodeBytes). Logged rather than thrown,
                // because a malformed request body must not break the
                // fallback pipeline below.
                Debug.LogWarning($"[CaptureScreen] could not build traits request: {e.Message}");
                LastTraitsRequestJson = null;
            }

            CatTraits traits = null;
            if (AskWorker != null)
            {
                SetBusy(true, Shell.Copy.Of("capture.colours"));
                yield return null;
                try { traits = AskWorker(prepared); }
                catch (Exception e) { Debug.LogWarning($"[CaptureScreen] worker failed: {e.Message}"); }
            }

            if (traits == null)
            {
                // Task 6.11, and 60-coat/01: no Worker, or it did not answer.
                // Read what the phone can read off the photograph and default
                // the rest — never an error screen, because the photo itself
                // was fine.
                //
                // Two readers, and the order is the point. CatCoat measures
                // the cat's OWN pixels, from the subject mask, and gives back
                // a colour, a pattern and a fur length, each of which may be
                // null where the measurement would not commit. CatColour is
                // the older estimate — the mean of the middle of the frame,
                // background and all — and it is still here because a mask is
                // not guaranteed: on Android it comes from an optional Play
                // services module that may not have downloaded yet, and a
                // player on that phone must still get her cat.
                //
                // A null pattern or fur length is not a gap to fill in here.
                // FromColourOnly keeps solid and short for exactly those, and
                // that is the shipped behaviour: a cat wrongly called a tabby
                // is worse than the plain cat nobody was ever surprised by.
                var coat = Shell.CatCoat.Read(prepared);
                var colour = coat.BaseColor ?? Shell.CatColour.Estimate(prepared);
                try
                {
                    traits = colour != null
                        ? CatTraits.FromColourOnly(colour, coat.Pattern, coat.FurLength)
                        : CatTraits.Default;
                }
                catch (ArgumentException e)
                {
                    // FromColourOnly throws if `colour` is not one of
                    // CatTraits.Allowed["base_color"] — Swift's palette
                    // (Plugins/iOS/CatColour.swift) and that list are two
                    // copies of one set of names with nothing keeping them
                    // in sync (CatColourPaletteParityTests guards this now).
                    // If they ever drift, this is the one call standing
                    // between that drift and a hung screen: the coroutine
                    // must not die here, so she still gets a cat.
                    Debug.LogWarning($"[CaptureScreen] colour '{colour}' rejected: {e.Message}");
                    traits = CatTraits.Default;
                }
            }

            // Her distinctive marks, measured on the device from the same
            // photograph — the one trait that says WHICH cat rather than what
            // kind of cat. Everything above is a class characteristic: colour,
            // pattern, fur, eyes, and where white sits. Six colours by six
            // patterns by the rest come to 288 distinguishable cats; a patch on
            // one paw is worth more than all of them together.
            //
            // Measured rather than described. The model can name a mark and
            // will when asked, but it is guessing at a place from a photograph;
            // the device has the segmentation mask and the animal's skeleton
            // and can measure how far this cat's muzzle is from what a muzzle
            // usually is. When the measurement produces nothing — an old phone,
            // a pose Vision could not read — whatever the model said stands.
            //
            // Never fatal. A cat without marks is the cat we shipped yesterday;
            // a screen that hangs because Vision threw is not.
            try
            {
                var measured = Shell.CatMarks.Measure(prepared);
                var spots = measured.ToSpots();
                if (spots.Count > 0)
                {
                    traits = traits.WithSpots(spots);
                    Debug.Log($"[CaptureScreen] measured {spots.Count} mark(s): " +
                              string.Join("; ", spots.Select(s => s.ToString())));
                }
                else
                {
                    Debug.Log($"[CaptureScreen] no distinctive marks measured " +
                              $"(rung {measured.rung})");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CaptureScreen] marks not measured: {e.Message}");
            }

            // The waiting block STAYS UP, and `SetBusy(false)` is deliberately
            // not called here.
            //
            // What used to happen: this line cleared the busy state, and
            // GameBoot's OnCatReady then hid the screen and built
            // MeetYourCatScreen in the same call stack. That screen's Build
            // runs CoatBuilder.TryBuild synchronously, and on a first run
            // nothing is cached, so the main thread is held for the whole coat
            // build with no frame drawn in between. The last thing painted
            // before the freeze was therefore a capture screen with its three
            // buttons back, no words and no bar — the "everything is fine,
            // press something" frame, immediately followed by a screen that
            // answers nothing. That is the worst possible frame to freeze on.
            //
            // Leaving it up means the last painted frame says "Copying the
            // colours…" over a moving bar, which is the truth: copying the
            // colours is exactly what the coat build is doing. This screen is
            // finished either way — whoever handles OnCatReady owns the frame
            // from here — and GameBoot gives the bar a moment to be seen
            // moving before it starts the build (see GameBoot.ShowCapture).
            OnCatReady?.Invoke(traits);
        }

        private void Skip()
        {
            // No camera, no network, no permission: the same cat every time,
            // so two players who skipped can talk about the same kitten.
            _message.text = Shell.Copy.Of("capture.skipped");
            // Wordless — the message above already says what happened, and the
            // bar says the app heard the tap. Skipping is not free either: the
            // default cat's coat is built by the same synchronous CoatBuilder
            // call the photographed one goes through, and on a first run it is
            // just as uncached. This also takes the three buttons away, so the
            // hand-over below cannot be started twice.
            SetBusy(true, "");
            OnCatReady?.Invoke(CatTraits.Default);
        }

        /// <summary>
        /// The waiting block: the stage's words, and under them a short bar
        /// sliding back and forth inside a track.
        ///
        /// WHAT WAS HERE BEFORE, AND WHY IT READ AS A DEAD APP. Until
        /// 2026-08-31 this was one line: `_busy = new Label(...)`, added to
        /// the root LAST — after the title, the message and all three buttons —
        /// with `marginTop = 12`. Waiting therefore looked like this: the three
        /// buttons went grey, and a small line of text appeared BELOW them, at
        /// the bottom of the stack, well away from the control the player had
        /// just pressed. Nothing on the screen moved. Not one pixel changed
        /// between the frame after the tap and the frame the cat arrived in,
        /// except that a static string was swapped twice.
        ///
        /// A still frame with greyed-out controls is precisely what a hung
        /// application looks like — it is what a hung application looks like
        /// because it IS what a hung application looks like — so the owner read
        /// it correctly: "я как игрок не вижу прогресса и для меня выглядит как
        /// будто либо ничего не происходит, либо все зависло". The three-stage
        /// copy (`capture.opening` → `capture.looking` → `capture.colours`) was
        /// honest and is kept word for word; it simply cannot carry the load on
        /// its own, because the difference between text that changed a moment
        /// ago and text that will never change again is invisible.
        ///
        /// WHY THIS SHAPE. It is GameBoot.Waiting's bar, deliberately — the
        /// same 168x5 track, the same third-width bar, the same 28ms step,
        /// the same ping-pong. That indicator was built for the identical
        /// complaint about the house map ("кликаю - ничего не происходит"),
        /// and it is the shape that has actually been watched moving on a
        /// device. A second, differently-drawn spinner would be a second thing
        /// to get right for no gain.
        ///
        /// INDETERMINATE, AND THAT IS THE HONEST ANSWER. None of the three
        /// stages can report progress: Vision either answers or does not, the
        /// crop is one call, and the coat build is one more. A bar that crawled
        /// to 90% and stopped would be a lie told slowly. A bar with no scale
        /// on it claims only "this is still running", which is exactly what is
        /// true and exactly what the player is asking.
        ///
        /// CALM. 28ms a step over a 40-step cycle is one sweep every 1.1s, and
        /// the bar is a thin rule rather than a churning wheel. This screen is
        /// the emotional beat of the game — we are looking at your cat — and
        /// the motion has to say "attending to it", not "hurry up".
        /// </summary>
        private void BuildBusy(Color ink)
        {
            _busy = new VisualElement();
            _busy.style.display = DisplayStyle.None;
            _busy.style.alignItems = Align.Center;
            // Room above and below, because the three buttons vanish while this
            // is up and it stands alone under the message.
            _busy.style.marginTop = 4;
            _busy.style.marginBottom = 16;
            // Nothing in here is ever a target; the block exists to be looked
            // at, and a tap that lands on it must not be swallowed silently.
            _busy.pickingMode = PickingMode.Ignore;

            _busyWords = new Label(Shell.Copy.Of("capture.looking"));
            _busyWords.style.color = ink;
            _busyWords.style.whiteSpace = WhiteSpace.Normal;
            _busyWords.style.unityTextAlign = TextAnchor.MiddleCenter;
            _busyWords.style.marginBottom = 12;
            _busy.Add(_busyWords);

            var track = new VisualElement();
            track.style.width = 168;
            track.style.height = 5;
            track.style.flexShrink = 0;
            track.style.backgroundColor = new Color(0.84f, 0.78f, 0.66f);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius =
                track.style.borderBottomLeftRadius =
                    track.style.borderBottomRightRadius = 3;

            // Offset with `left` on a relatively-positioned child, not with an
            // absolute one — GameBoot.Waiting's note, and the reason is that
            // this is the arrangement that was seen moving on a real device.
            _busyBar = new VisualElement();
            _busyBar.style.height = Length.Percent(100);
            _busyBar.style.width = Length.Percent(34);
            _busyBar.style.backgroundColor = new Color(0.20f, 0.16f, 0.12f);
            _busyBar.style.borderTopLeftRadius = _busyBar.style.borderTopRightRadius =
                _busyBar.style.borderBottomLeftRadius =
                    _busyBar.style.borderBottomRightRadius = 3;
            track.Add(_busyBar);
            _busy.Add(track);

            // schedule.Execute(...).Every(ms) rather than a coroutine: the
            // pipeline itself is a coroutine and the whole point is that this
            // belongs to the panel, not to the work. Paused while hidden, so a
            // screen nobody is looking at is not repainting a bar every 28ms
            // for the rest of the session — this component outlives its own
            // screen and is only destroyed on the way to the house map.
            var step = 0;
            _busyTick = _busy.schedule.Execute(() =>
            {
                step = (step + 1) % 40;
                var t = step < 20 ? step : 40 - step;   // 0..20..0
                _busyBar.style.left = Length.Percent(t * 3.3f);
            }).Every(28);
            _busyTick.Pause();
        }

        /// <summary>
        /// Enter or leave the waiting state.
        /// </summary>
        /// <param name="text">
        /// The stage's words. <c>null</c> keeps whatever is already there —
        /// the old contract, unchanged. The empty string means WORDLESS: the
        /// bar alone, no label. That is for <see cref="Skip"/>, where there is
        /// nothing true and short to say and no case for a new copy key in
        /// seventeen tables; GameBoot.Waiting makes the same choice for the
        /// same reason and says so at length.
        /// </param>
        private void SetBusy(bool busy, string text = null)
        {
            _busy.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            if (text != null)
            {
                _busyWords.text = text;
                _busyWords.style.display = text.Length == 0
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }

            // Hidden, not merely disabled. Three greyed rectangles under a
            // still line of text was the old picture, and greyed-out controls
            // are the universal look of an application that has stopped
            // answering. Taking them away instead says something has happened
            // and leaves the waiting block standing where the pressed control
            // was — which is where the eye already is.
            var buttons = busy ? DisplayStyle.None : DisplayStyle.Flex;
            _gallery.style.display = buttons;
            _skip.style.display = buttons;
            // The camera comes back only if there was ever a camera.
            _camera.style.display = _hasCamera ? buttons : DisplayStyle.None;

            // SetEnabled as well, and not as belt-and-braces: an element with
            // `display: none` still exists, and this keeps the pipeline's own
            // guarantee that a second photograph cannot be started under it.
            _camera.SetEnabled(!busy);
            _gallery.SetEnabled(!busy);
            _skip.SetEnabled(!busy);

            if (busy) _busyTick.Resume();
            else _busyTick.Pause();
        }
    }
}
