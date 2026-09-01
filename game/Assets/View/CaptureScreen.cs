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
        /// <summary>The verdict in small type — see where it is built.</summary>
        private Label _detail;
        /// <summary>
        /// The build stamp — see where it is built in <see cref="Build"/> for
        /// why it exists and why it is its own label rather than sharing
        /// <see cref="_detail"/>.
        /// </summary>
        private Label _stamp;
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
            // Padding and a rounded ground it does not use while it is the
            // hint — see Answer() for why they exist and when they turn on.
            _message.style.paddingLeft = 14;
            _message.style.paddingRight = 14;
            _message.style.paddingTop = 10;
            _message.style.paddingBottom = 10;
            _message.style.borderTopLeftRadius = 10;
            _message.style.borderTopRightRadius = 10;
            _message.style.borderBottomLeftRadius = 10;
            _message.style.borderBottomRightRadius = 10;

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

            // The verdict, in the smallest type on the screen, shown only when
            // a photograph was turned away.
            //
            // Diagnostics belong on disk and not in front of players, and this
            // file argues that at length above `OnTrouble`. It is still the
            // right rule, and this is the exception that proves what the rule
            // is for: on 2026-09-01 the owner could not read the disk. Android
            // 11 walls `Android/data/<package>` off from every other app, so
            // `capture-state.txt` is unreachable from a file manager and from
            // Termux alike — he tried, and got "No such file or directory"
            // three times over on a path that certainly exists.
            //
            // A number he can photograph beats a file he cannot open. The line
            // is deliberately terse and unexplained — "Cat 0.58" — which is
            // meaningless to a player and complete to us: it separates "the
            // labeller saw something else" from "it saw her cat and the 0.60
            // threshold turned it away", and those have nothing in common but
            // the sentence above them.
            _detail = new Label(string.Empty);
            _detail.style.fontSize = 11;
            _detail.style.color = new Color(0.25f, 0.21f, 0.17f, 0.45f);
            _detail.style.marginBottom = 14;
            _detail.style.unityTextAlign = TextAnchor.MiddleCenter;
            _detail.style.display = DisplayStyle.None;

            // The build stamp. Task 90-android/?? — the night this got added,
            // the owner reported two photographs failing and the fix for
            // exactly those two had already shipped twenty minutes earlier.
            // Neither of us could tell whether the APK on his phone was built
            // before or after it: he installs by hand, and there was no
            // version anywhere on screen or in the package. We spent the
            // exchange on that instead of on the bug.
            //
            // A separate label from `_detail`, deliberately, even though the
            // two would sit inches apart: `_detail` is a diagnostic that
            // appears and disappears with a rejected photograph, and this is
            // always on screen and never about the photograph. Sharing one
            // Label would mean either the stamp vanishes whenever a verdict
            // is shown, or a verdict has to share its line with a commit
            // hash — both wrong for different reasons.
            //
            // Same smallest-type, low-contrast look as `_detail` on purpose:
            // this is for the person testing the build, not for a player, and
            // it must read as background texture rather than as a message.
            // `Application.version` is `PlayerSettings.bundleVersion`, which
            // BuildScript.StampVersion stamps with the local date-time and the
            // short git commit before every Android and iOS build — so the
            // same string that lands here also lands in the APK manifest,
            // readable with `adb shell dumpsys package <id> | grep
            // versionName` without launching the app.
            //
            // No Copy key: this is a build stamp, not copy. A date and a
            // commit hash mean the same thing in every one of the seventeen
            // languages this screen already speaks.
            _stamp = new Label(Application.version);
            _stamp.style.fontSize = 11;
            _stamp.style.color = new Color(0.25f, 0.21f, 0.17f, 0.45f);
            _stamp.style.unityTextAlign = TextAnchor.MiddleCenter;
            _stamp.style.marginTop = 14;

            _root.Add(title);
            _root.Add(_message);
            _root.Add(_detail);
            // The waiting block goes HERE — above the buttons, not below them.
            // See BuildBusy for the whole argument; the short version is that
            // the player's eye is on the control she just pressed, and the
            // block takes that control's place while the pipeline runs.
            _root.Add(_busy);
            _root.Add(_camera);
            _root.Add(_gallery);
            _root.Add(_skip);
            // Last, so it sits at the bottom of the screen's content — the
            // one place on this screen nothing else claims.
            _root.Add(_stamp);
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
                // Cancelling is not an answer about a photograph — she
                // changed her mind, and the screen goes back to offering.
                if (reason == "cancelled")
                    HintAgain(Shell.Copy.Of("capture.cancelled"));
                else
                    Answer(Shell.Copy.Of("photo.our_fault"), $"picker · {reason}");
            };

            if (fromCamera) CatPicker.CaptureWithCamera(picked, failed);
            else CatPicker.PickFromGallery(picked, failed);
        }

        /// <summary>
        /// The pipeline. Public so a PlayMode test can push a stubbed photo
        /// through it without a picker.
        ///
        /// THIS SCREEN NEVER REFUSES TO MAKE A CAT. As of 2026-09-01 there is
        /// no way out of this method that leaves the player standing on the
        /// capture screen holding a photograph we would not use. Every branch
        /// ends at <see cref="OnCatReady"/>.
        ///
        /// Until that day there were three `yield break`s in here and each one
        /// was a dead end: Vision could not run, the judge said "not a cat", or
        /// the crop failed. The middle one did nearly all the damage. It read
        /// `if (!PhotoJudge.Accepts(outcome))` and stopped, and what the owner
        /// saw for two days was his own cat — filling the frame, in focus —
        /// answered with "кошки здесь не видно" or "похоже на собаку" and an
        /// instruction to go and take a better photograph. Three separate
        /// causes for that were found and fixed inside one day and a fourth was
        /// on its way; <see cref="PhotoJudge"/> carries the full account and
        /// the argument for why deleting the gate is right rather than lazy.
        ///
        /// What recognition is FOR now: locating her, so
        /// <see cref="Shell.CatPhoto.Prepare"/> can aim the crop at the cat
        /// rather than at the room. When it locates nothing worth believing,
        /// the whole frame is the crop — which is exactly what the Android
        /// build did for months before it had a recogniser at all, so this is a
        /// path with mileage on it and not a fallback invented today.
        ///
        /// The copy still comes off the four outcomes and is still shown. It is
        /// a remark made in passing about the photograph, not a refusal — the
        /// dog message in particular is kept precisely because it is a kind and
        /// true thing to say on the way to giving her the cat anyway.
        /// </summary>
        public IEnumerator Handle(byte[] photo)
        {
            SetBusy(true, Shell.Copy.Of("capture.looking"));
            yield return null;      // let the busy state paint before Vision blocks

            // Where the crop will be aimed. A default box means "use the whole
            // image" to both halves of CatPhoto (the Swift one and the Java
            // one say so in as many words), and that is the answer whenever
            // recognition has not given us somewhere better to point. It is
            // never a reason to stop.
            var box = default(AnimalBox);

            var answer = Recognise(photo);
            if (answer.Failed)
            {
                // 50-photo/05 VERIFY: Vision could not run at all — decode
                // failure, not iOS, or the request itself threw. Not a
                // judgement that there is no cat, which is why it keeps
                // `photo.our_fault` instead of borrowing the "no cat in this
                // one" copy.
                //
                // It is one of the two genuine failures and is still said out
                // loud, but it no longer ends the run. A recogniser that could
                // not run has told us nothing whatever about the photograph;
                // there may well be a cat filling it. So: the honest sentence,
                // and then the whole frame.
                Debug.LogWarning($"[CaptureScreen] vision failed: {answer.error}");
                OnTrouble?.Invoke(
                    $"vision could not run: {answer.error}; cropping the whole frame");
                Answer(Shell.Copy.Of("photo.our_fault"), $"vision · {answer.error}");
            }
            else
            {
                var best = answer.FoundAnimal ? answer.Best : default;
                var outcome = PhotoJudge.Judge(
                    answer.FoundAnimal ? best.identifier : null,
                    answer.FoundAnimal ? best.confidence : 0f);

                // The identifier and the confidence, not just the verdict. A
                // photograph read as "no cat" and one read as "cat, 0.58" are
                // the same sentence to the player and completely different
                // facts: the first says the labeller saw something else, the
                // second says it saw her cat below MinimumConfidence (0.60).
                //
                // Both produce a cat now, so this line is no longer the
                // difference between a player who got one and a player who did
                // not. It is the difference between a crop aimed at the cat and
                // a crop of the whole room — and the room is what her coat
                // would then be read from, so it is still the most useful thing
                // this file writes down.
                OnTrouble?.Invoke(
                    $"vision said {(answer.FoundAnimal ? best.identifier : "nothing")} " +
                    $"at {(answer.FoundAnimal ? best.confidence : 0f):F2} -> {outcome}");

                // Everything the device knows, in one line she can photograph.
                //
                // "Cat 0.58" was enough while the question was "did the
                // threshold turn her away". It is not enough now. The owner's
                // phone answers `NoAnimal` and `Dog` on two photographs this
                // same build reads as `Cat 0.97` and `Cat 0.88` on an emulator
                // — same APK, same files, opposite verdicts — so the
                // difference lives inside the recogniser on his hardware and
                // cannot be reproduced here. What CAN be done is to make one
                // screenshot carry the whole answer: what was named, how many
                // things were found at all, and the size of the frame it was
                // found in.
                //
                // Terse and unexplained on purpose. It means nothing to a
                // player and everything to whoever is holding the other end of
                // the conversation, which for this build is one person.
                var verdict = answer.FoundAnimal
                    ? $"{best.identifier} {best.confidence:F2}"
                    : "nothing";
                verdict += $" · {answer.detections?.Length ?? 0}d";
                verdict += $" · {answer.imageWidth}x{answer.imageHeight}";
                // A cat we are sure of gets the plain hint; the other three get
                // a ground under them, because a remark about the photograph
                // should still look like a reply rather than like the screen
                // she started on. Answer() makes that case at length.
                if (PhotoJudge.SawACat(outcome)) HintAgain(PhotoMessages.For(outcome));
                else Answer(PhotoMessages.For(outcome), verdict);

                // The one thing the outcome still decides. An animal we are
                // willing to name has a box worth cropping to — a dog's
                // included, because a dog's own fur is a better thing to read a
                // coat off than the sofa behind her, and we are making a cat
                // from this photograph either way. `NoAnimal` means the label
                // is not to be believed, and a box we do not believe is worse
                // than no box at all.
                if (PhotoJudge.LocatedAnAnimal(outcome)) box = best;
            }

            SetBusy(true, Shell.Copy.Of("capture.colours"));
            yield return null;

            // No box from the labeller? Ask the segmenter where the subject is
            // before falling back to the whole picture.
            //
            // The two are independent, and that is the point. The labeller says
            // WHAT it saw and can be wrong about it; the segmenter says WHERE
            // something is and does not care what it is called. On the owner's
            // phone the first fails on photographs the second handles fine — he
            // gets "кота тут не видно" on a cat filling the frame, a file that
            // measures Cat 0.97 here — and until now a failed label meant a
            // crop of the entire room.
            //
            // What that cost, measured on his `photo_3` on 2026-09-01: the
            // colour taken over the whole frame reads `ginger`, over the
            // subject's own pixels `brown`. He reported a ginger kitten from a
            // brown cat, and this is where the ginger came from — the room, not
            // the animal. The species line he saw was wrong and harmless; the
            // crop underneath it was wrong and on screen.
            //
            // Deliberately AFTER the message is painted and the bar is moving:
            // this costs a mask over the full photograph, a few hundred
            // milliseconds, and it must not delay her being told what happened.
            if (box.width <= 0 || box.height <= 0)
            {
                var found = SubjectBox(photo);
                if (found.width > 0 && found.height > 0)
                {
                    OnTrouble?.Invoke("no label box; cropping to the subject " +
                                      $"mask {found.width}x{found.height}");
                    box = found;
                }
            }

            var prepared = Crop(photo, box);
            if (prepared == null)
            {
                // The other genuine failure, and now the only one that costs
                // her the cat in her photograph: the bytes could not be
                // decoded, or this device could not hold them. Nothing to make
                // a cat FROM — not a judgement we can shrug off, an empty hand.
                //
                // So she gets the same kitten the skip button gives rather than
                // an error screen. CatTraits.Default is a supported, shipped,
                // deliberately shared cat (see Skip), which makes this the
                // mildest ending available: one sentence saying it was our
                // doing, and then a cat.
                //
                // SetBusy(false) is deliberately not called, for the reason
                // spelled out at the end of the path below — whoever handles
                // OnCatReady owns the next frame, and the last frame this
                // screen paints must not be the "everything is fine, press
                // something" one.
                OnTrouble?.Invoke("the crop failed; handing over the default cat");
                Answer(Shell.Copy.Of("photo.our_fault"), "crop");
                // The only surviving meaning of photo:rejected, and a narrower
                // one than it had. It used to count every photograph the judge
                // turned away, which was most of the funnel's losses; it now
                // counts only the ones we could not process at all. Worth
                // keeping under that name — "she gave us a photograph and did
                // not get her own cat" is precisely what section 5 wants to
                // watch — but a reading from before today is not comparable
                // with one from after.
                Analytics.PhotoRejected();
                OnCatReady?.Invoke(CatTraits.Default);
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
            HintAgain(Shell.Copy.Of("capture.skipped"));
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
        /// <summary>
        /// Say something back about the photograph she just chose, so it reads
        /// as an answer rather than as the screen she started on.
        ///
        /// The four outcomes — no cat, a dog, too blurry, our fault — were
        /// already written into `_message`, and that was the whole of it: the
        /// same label, the same size, the same grey, in the same place the hint
        /// had been sitting a second earlier. With the buttons back and the
        /// title unchanged, a rejected photograph and the opening screen are
        /// the same picture.
        ///
        /// The owner described it exactly that way on 2026-08-31 — he chose a
        /// photograph of his cat, saw "Looking…", and was "thrown back to the
        /// screen with the two buttons". He was not thrown anywhere. He was
        /// told something, in a way that did not read as being told anything.
        ///
        /// So an answer gets a ground to sit on and the hint does not. Nothing
        /// alarming: the same warm ink at a wash, no red, no icon. Being turned
        /// away is not an error, and three of the four reasons are about the
        /// photograph rather than about her.
        /// </summary>
        private void Answer(string text, string detail = null)
        {
            _message.text = text;
            _message.style.backgroundColor = new Color(0.25f, 0.21f, 0.17f, 0.07f);
            _detail.text = detail ?? string.Empty;
            _detail.style.display = string.IsNullOrEmpty(detail)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Back to a plain hint with no ground behind it.</summary>
        private void HintAgain(string text)
        {
            _message.text = text;
            _message.style.backgroundColor = Color.clear;
            _detail.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Where the segmenter thinks the subject is, as a box, or an empty box
        /// when it has nothing to say.
        ///
        /// Injected like <see cref="Recognise"/> and <see cref="Crop"/> so a
        /// test can drive this path without a device — the whole point of this
        /// fallback is a phone whose labeller behaves differently from ours,
        /// and a path only exercised on hardware nobody has is a path nobody
        /// checks.
        /// </summary>
        public Func<byte[], AnimalBox> SubjectBox = photo =>
        {
            try
            {
                var cut = CatVision.Silhouette(photo);
                if (!cut.HasMask) return default;

                // The whole rule lives in Core.SubjectBox, where the test suite
                // can reach it. It was eleven lines here and it shipped missing
                // its conversion back into photograph pixels — see the note on
                // that class for what that cost and how it was measured.
                return Core.SubjectBox.Of(cut.mask, cut.maskWidth, cut.maskHeight,
                                          cut.answer.imageWidth, cut.answer.imageHeight);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CaptureScreen] subject box failed: {e.Message}");
                return default;
            }
        };

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
