
# Built, 2026-08-26 — the tap is real. Sound pass, 2026-08-28 — so is the sound

`Assets/Plugins/iOS/CatHaptics.swift` + `Assets/Shell/Feedback.cs`, called from
`DebugGameView.Take` on every successful placement.

## Haptics (unchanged, 2026-08-26)

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

---

# The sound pass, 2026-08-28

Until today `Feedback` synthesised two blips at runtime and called them a
placeholder, and `find game/Assets -iname "*.wav"` returned nothing. A game
about tidying with no sound is missing half of what a tap feels like.

## Three files, generated, not downloaded

`tools/sound/make_sounds.py` writes
`Assets/Resources/Audio/{place,match,refused}.wav`. It is committed with them,
and `tools/tests/test_sound.py` regenerates the set into a temporary directory
and compares **bytes** with what is in git — so the script is the source of the
audio, not a plausible-looking file sitting next to it.

Synthesised rather than sampled for one reason that is not aesthetic. A free
sample drags a licence question behind it: attribution terms, a
"non-commercial" clause noticed after shipping, a chain of re-uploads whose
provenance nobody can reconstruct. This project already has one unresolved
licence question and did not need a second. Arithmetic on numbers written in
our own repository is unambiguously ours.

| cue | length | bytes | peak | RMS | what it is |
|---|---|---|---|---|---|
| `place` | 85 ms | 8 204 | 0.32 | 0.052 | a small wooden object set down on a shelf |
| `match` | 360 ms | 34 604 | 0.36 | 0.094 | three things clicking into place — G4-C5-E5, mallets, 62 ms apart |
| `refused` | 140 ms | 13 484 | 0.18 | 0.027 | a shelf that will not take it — one flat, muted note |

56 292 bytes of audio, 57 838 with the four `.meta` files. Against a 50 MB APK
that is 0.11%. 48 kHz / 16-bit / mono: 48 kHz because `AudioManager.asset` has
`m_OutputSamplingRate: 48000`, so nothing is resampled on the frame the player
taps; mono because a UI cue has no position in a room.

The `.meta` files are committed too, PCM and decompressed-on-load. Without them
every machine that opens the project invents its own guid for the same file and
the next commit carries the churn; Vorbis on an 8 KB clip would trade nothing
for a decode on the tapped frame. (They are written at `serializedVersion: 6`,
copied from a package sample; Unity 6 may rewrite that number once on first
import. The guid — the part that matters — is ours and stays.)

## How they are built, and the one thing measurement changed

Each cue is a struck object: a short contact transient (banded noise, the
instant two surfaces meet) plus decaying partials, each damped harder than the
one below it. `place` uses **inharmonic** partials — 212 Hz with ratios 2.30 /
4.06 / 6.49 / 9.72 — because exact 2x, 3x reads as a musical note, i.e. as a
synthesizer, and because a knock's bright part dying in 10 ms while its body
rings for 60 is most of what makes wood sound like wood. `refused` is harmonic,
lower, with the transient removed and a 5 ms rise in its place: a hand pressing
something that does not move. `match` is the same mallet world an octave and a
half up and in tune, rising and resolving so that it lands rather than stops.

The first version of `place` and `refused` used the textbook amplitudes for a
wooden block, and measuring them was worth more than any amount of reasoning
about them: **86% of `place`'s energy sat below 300 Hz, and 99% of
`refused`'s.** True to the physics, and inaudible on a phone speaker, which
radiates almost nothing down there. Both were revoiced by lifting the upper
modes until at least a third of the energy is above 300 Hz, and `test_sound.py`
holds that line for anything added later. Nobody would have caught this by
reading the code; it would have shipped as "the sound is very quiet on my
phone".

Loudness is baked into the files, quietest to loudest — refused, place, match —
so `Feedback` has one volume constant and not three, and a fourth cue gets its
level in the generator instead of a guessed number at a call site. All three
keep about 9 dB of headroom: this game is played in bed at night, so nothing
here is allowed to be the loudest thing in a dark room.

## Scope: two things this pass added past `task.txt`

`task.txt` excluded both, and the sound pass asked for both:

1. **A refused cue.** The original reasoning — "negative moments do not get
   feedback in a game about care" — was half right. A refusal must not scold,
   which is why this one is the quietest of the three, and flat rather than
   harsh. But a refused tap with *no* answer is indistinguishable from a tap
   the app never received, and that is worse than either.
2. **A mute switch.** `Feedback.Muted`, persisted in `PlayerPrefs` under
   `catshelter.audio.muted`. Sound a player cannot turn off is sound the player
   turns off by deleting the game.

**`Feedback.Refused()` has no call site yet.** Its one place is the refusal
branch of `DebugGameView.Take` — the branch that already logs `tap refused` and
calls `Flinch(source)` — and that file belonged to another worker this pass.
One line, beside the `Flinch`. Until it is added the clip ships and nothing
plays it.

`Muted` has no UI either: there is no settings screen in the MVP. It is a
property such a screen can flip in one line, and it survives a restart.

## Silence, and what C# can and cannot do about it

- **iOS: it already works, and no code here is what makes it work.**
  `ProjectSettings.asset` has `muteOtherAudioSources: 0`, so Unity runs the
  audio session in the **Ambient** category — which is silenced by the hardware
  ring/silent switch and mixes under the player's own music rather than
  stopping it. Both are what this game wants. This was read out of the settings
  file, not observed on a device. Note the fragility: flipping that one
  checkbox to "Mute Other Audio Sources" moves the session to SoloAmbient and
  stops the player's music, and nothing in `Feedback.cs` could put it back.
  `ProjectSettings` was another worker's this pass and was not touched.
- **Android: there is no silent switch over the media stream, by design.**
  Ringer mode governs the ring and notification streams; game audio is media,
  and the volume keys are its control — which works with no code at all.
  What *is* possible from C# alone, and was deliberately not done: an
  `AndroidJavaObject` call to `AudioManager.getRingerMode()`, muting ourselves
  when the ringer is silent. Left out because it would surprise — no Android
  game behaves that way, so a player who silenced her ringer and expects the
  game to keep playing would read it as a bug — and because it needs
  re-checking on every focus change and could not be tested from here. It is
  one method if the owner disagrees.
- **Ours:** `Feedback.Muted` silences sound and leaves haptics alone. A phone
  on silent still vibrates, and so does this.

## Tests

`tools/tests/test_sound.py`, 37 checks. The files exist; 48 kHz / 16-bit /
mono; each under its own ceiling (place 120 ms, refused 200 ms, match 400 ms —
a match cue over 400 ms is a game that feels slow); no clipping and real
headroom; no click at either edge and no DC offset; the loudness order
refused < place < match; `match` reads as three attacks where the other two
read as one; at least a third of every cue's energy above 300 Hz; nothing above
8 kHz that could hiss; the committed bytes equal what the generator produces;
the total stays inside a 96 KB budget; and `Feedback.cs` really loads all three
and really has a persistent mute — which no C# test can check, because those
assemblies are not compiled outside Unity.

Suite: 219 passed (176 before this pass, 37 added here, 6 added by another
worker in `test_copy_table.py` during the same window).

## What cannot be checked here

**Nothing in this pipeline can hear these files.** Every claim above is a
measured one — length, spectrum, envelope, headroom — and measurement does not
cover:

- Whether `place` reads as *wood* rather than as a synthesizer blip. The
  inharmonic partials and the contact transient are the standard way to get
  there; whether they got there is a listening judgement.
- Whether the match phrase is charming on the tenth hearing and still not
  grating on the hundredth. It fires on every triple.
- Whether `refused` reads as "that isn't allowed" rather than as a glitch. The
  riskiest of the three: a flat note with no bite has to carry its meaning by
  being unlike `place`, and it is unlike it only in pitch, loudness and attack.
- Whether the balance holds *through a phone speaker* rather than in the
  numbers. The measurement says the energy sits in a band a phone can
  reproduce, not that the result is pleasant.
- Whether the set is too quiet at half media volume in an ordinary room. It was
  deliberately biased quiet.
- Whether sound and haptic land together. Both fire on the same line before the
  redraw, but a speaker has latency a Taptic Engine does not.

VERIFY is a HUMAN item and now needs hardware three times over: a simulator has
no Taptic Engine, an iOS silent switch cannot be exercised without a device, and
"feels present and pleasant, not annoying" is a judgement no agent should make
about its own work (`ROLES.md`). All of it waits for `14-testflight`, and on
both platforms — one verified platform once hid a completely broken second one.
