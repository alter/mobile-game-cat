
# Built, 2026-08-26 — the tap is real, the sound is a placeholder

`Assets/Plugins/iOS/CatHaptics.swift` + `Assets/Shell/Feedback.cs`, called from
`DebugGameView.Take` on every successful placement.

## Haptics

`UIFeedbackGenerator`, not Core Haptics: the two cues here are stock patterns
the system already tunes per device, and Core Haptics would mean authoring
waveforms and handling engine restarts for nothing gained.

- placement → `UIImpactFeedbackGenerator(.light)`
- match → `UINotificationFeedbackGenerator(.success)`, which is the "distinct
  cue on a triple" the scope asks for

Generators are created once and kept `prepare()`d. A generator built at the
moment of the tap arrives late enough to feel unrelated to the tap that caused
it — which is the whole difference between feedback and noise.

Feedback fires **before** the redraw, so the answer belongs to the finger
rather than to a frame of layout.

## Sound is synthesised, on purpose, for now

The game has no audio assets and none are planned before the art pass, so
`Feedback` generates two short percussive blips at runtime: a low one at 220 Hz
for a placement, a brighter two-note one for a match. They are the *shape* of
the feedback — quiet, short, distinct from each other — not its final voice.
Replacing them with recordings changes nothing at the call sites.

Nothing else was given a sound: a refused tap and the lose screen stay silent,
per SCOPE. Negative moments do not get feedback in a game about care.

## What cannot be checked here

VERIFY is a HUMAN item and needs hardware twice over: a simulator has no Taptic
Engine, so the haptic calls are no-ops there, and "feels present and pleasant,
not annoying" is a judgement no agent should make about its own work
(`ROLES.md`). Both wait for `14-testflight`.

What *is* confirmed: the project builds with the plugin, and the calls sit on
the one code path that completes a move.
