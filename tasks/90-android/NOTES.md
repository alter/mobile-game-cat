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

---

## The gating rationale changed on 2026-08-27 — read this before the section above

`DECISIONS.md` D17: the owner decided that the game targets **both Apple and
Android**, and that the accounts — the provider spend cap and the Apple
Developer Program — are deferred "much later".

**The objection recorded above still stands, but its reason is now different.**
It used to be "Android is the second wave, per cat-shelter-tech.md — only once
install counts run into the thousands". That premise is gone: Android is a
target, not a port. What holds the phase is no longer *iOS first*, it is
**gates first** — none of the three has passed, and `30-levels-solver/07` is
the only one that can even be run while there are no accounts.

**Two consequences worth stating plainly, because they cut opposite ways.**

*Against starting:* the anti-pattern in GOAL.md is unchanged. Building either
platform further before five people have played costs the same whichever store
it is aimed at.

*For remembering it exists:* **Android is the cheaper platform to test on.** No
$99, no review queue, no team ID, and the emulator takes an APK today —
`60-shell-build/08-mid-level-save` was proved on it on 27.08 after sitting
unproven for want of an iPhone. When the accounts question reopens, that
asymmetry is worth weighing rather than defaulting to iOS because the code
happened to be written there first.

**And one gap D17 opened that nobody had written down.**
`Shell/EveningReminder.cs` is entirely inside `#if UNITY_IOS && !UNITY_EDITOR`.
On Android the evening reminder does not exist at all — not stubbed, not
degraded, absent. That is `09-notifications` in this phase, and it is the only
mechanism in the MVP built to cause a return. While Android was a second wave
that was fine; with Android a target, metric 3 would be measured on one
platform that nudges players and one that does not, which makes the number a
comparison of platforms rather than of the game.

`00-android-decision` is also superseded in substance: it was written to be
decided on gate-3 numbers, its own text records that there are none, and the
owner has since decided on other grounds. It is not reopened here — but its
`done` rests on D17 now, not on the reasoning inside it.
