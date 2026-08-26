# Why this phase is shaped like this

Requested 2026-08-27. Written in full so the decision to run it — or not — is
made against a real task list rather than a guess.

## The objection, recorded once so it need not be repeated

`GOAL.md` names "building second-wave features before gate 3" as an
anti-pattern, and `cat-shelter-tech.md` puts Android in the second wave
explicitly: "only once install counts run into the thousands". None of the
three gates has passed. So `00-android-decision` is a real gate on this phase,
not paperwork: every other task depends on it, and it is the only one a person
must do first.

That objection does not make the phase wrong to describe. Describing it is
cheap, and it turns "how much is Android" from a feeling into fifteen tasks
with dependencies.

## What Android does not need

The expensive half is already platform-neutral and stays untouched:

- **Rules, levels, solver.** `Core` carries no engine reference, enforced by
  `build/check-core-purity.sh`. The 37 level files are data.
- **The save.** A hand-written line format, precisely so it is not
  `JsonUtility` and not iOS-shaped. `11-save-parity` proves that cheaply by
  moving one file between platforms.
- **The Worker.** It answers HTTP; it has no idea what phone is calling.
- **The art.** Same PNGs. If something reads badly at Android densities, that
  is a `40-art` bug on both platforms.
- **The copy.** One table, one key per string, since `16-localisation-ready`.

## What Android does need: five plugins and a store

Each of the five C# facades already has an `#else` branch waiting — the
Android tasks fill them in behind the same call:

| facade | iOS half | Android task |
|---|---|---|
| `CatPicker` | `CatPicker.swift`, PHPicker + UIImagePicker | `04-picker-plugin` |
| `CatVision` | `CatVision.swift`, VNRecognizeAnimalsRequest | `05-recognition-plugin` |
| `CatPhoto` | `CatPhoto.swift`, CoreGraphics | `06-crop-downscale` |
| `EveningReminder` | Unity Mobile Notifications, iOS side | `09-notifications` |
| `Feedback` | `CatHaptics.swift`, UIFeedbackGenerator | `08-haptics` |

Plus the store account (`12`), internal testing (`13`) and the device matrix
(`14`) — the Android equivalents of what iOS is currently blocked on.

## The one real risk, and why it is task 01

**iOS gets "cat or dog, with a box and a confidence" from a single call.
Android has no documented equivalent.** From
`knowledge/ios/03-vision-animal-recognition.md` section 9: "neither Object
Detection & Tracking nor Image Labeling gives an out-of-the-box, equally
targeted cat/dog identifier pair with a bounding box, the way the
`VNRecognizeAnimalsRequest` + Vision combination does."

If ML Kit cannot name a cat, or names one without a box, the whole photo hook —
the thing the concept rests on — has no stage one on Android. The options then
are a bundled TFLite model (about 15 MB, and `cat-shelter-tech.md` says only
once installs run into the thousands) or sending every photo to the Worker
uncropped, which changes the cost per player.

That is why `01-mlkit-capability-probe` runs before any porting and measures
against the same 41 images the iOS numbers came from: cats 18/20, dogs 5/5,
empty frames 0/5. Two hours of work that can save the phase.

## Order

```
00 decision ─┬─ 01 ML Kit probe ── 05 recognition ─┬─ 06 crop
             │                                     └─ 07 outcome parity
             ├─ 02 build ── 03 emulator ─┬─ 04 picker ── 10 audit ── 13 testing
             │                           ├─ 08 haptics
             │                           ├─ 09 notifications
             │                           ├─ 11 save parity
             │                           └─ 15 analytics parity
             └─ 12 Play Console ─────────────────────── 13 testing ── 14 devices
```

`00` and `12` are the owner's. `12` starts the day `00` says go, because
identity verification takes days that do not shrink with effort — the same
mistake the iOS side is currently paying for.
