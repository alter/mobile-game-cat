"""Task 60-shell-build/10, sound pass: the three cues the shell plays.

Writes `game/Assets/Resources/Audio/{place,match,refused}.wav` and, beside each,
the `.meta` Unity would otherwise invent per machine.

Why synthesised and not sampled
-------------------------------
A free sample carries a licence question behind it — attribution terms, a
"non-commercial" clause noticed after shipping, a chain of re-uploads with no
provenance. This project already has one unresolved licence question and does
not need a second. Everything below is arithmetic on numbers written here, so
the audio is unambiguously ours and the .wav files in git are reproducible from
this file (`test_sound.py` regenerates them and compares bytes).

What the three sounds are, physically
-------------------------------------
The game is a quiet one about tidying, played in ten-minute gaps and in bed at
night (`cat-shelter-mvp.md` sections 2 and 4). So none of these is a "game
sound"; each one is an object doing something on a shelf.

*place* — a small wooden object set down. A struck wooden block is a contact
transient (the instant of the two surfaces meeting: broad, dull, 8 ms) plus a
handful of **inharmonic** modes that decay faster the higher they are. The
inharmonicity is what makes wood wood: partials at exact 2x, 3x would read as a
musical note, i.e. as a synthesizer. Damping rising with frequency is the other
half — the bright part of a knock is gone in 10 ms while the body rings for 60.

*match* — three things clicking into place. Three soft mallet notes, G4-C5-E5,
62 ms apart, each with the marimba's strong fundamental and quiet 4th partial.
Rising and resolving, so it lands rather than stops; the third note is the only
one allowed a longer tail. It is the same mallet world as *place*, an octave and
a half up and in tune — a reward should sound like this game, not like a
different game interrupting it.

*refused* — a locked tile, a shelf that will not take it. Pitched below *place*,
with the contact transient removed and a soft 5 ms rise in its place, and
everything above 1.8 kHz damped away: a hand pressing something that does not
move. Harmonic partials where *place* is inharmonic, so it reads as a flat note
rather than a thud. It is the quietest of the three by design — a refusal is
information, not a reprimand, and the audience this game is for is driven off by
punishment (mvp section 2).

Loudness
--------
Peaks are baked in at three different levels so the C# side needs no per-cue
mixing: refused < place < match, in RMS as well as in peak. All three sit well
under full scale — a phone at night, not a demo booth.

A first pass voiced *place* and *refused* on their low modes alone, which is
what the physics of a wooden block actually says, and put 86% and 99% of their
energy below 300 Hz. A phone speaker radiates almost nothing down there, so both
were revoiced upward until a third of the energy survives it. `test_sound.py`
holds that line. Being right about the physics and inaudible on the device the
game ships on is still being wrong.

Rate and depth
--------------
48 kHz, 16-bit, mono. 48 kHz because `game/ProjectSettings/AudioManager.asset`
has `m_OutputSamplingRate: 48000`; a 44.1 kHz clip would be resampled on every
play for nothing. Mono because these are UI cues with no position in space.

Run it
------
    .venv/bin/python tools/sound/make_sounds.py
    .venv/bin/python tools/sound/make_sounds.py --out-dir /tmp/x --quiet
"""
from __future__ import annotations

import argparse
import hashlib
import wave
from pathlib import Path

import numpy as np

SAMPLE_RATE = 48_000
SAMPLE_WIDTH = 2  # bytes, i.e. 16-bit signed PCM
CHANNELS = 1

# Nothing here may be longer than this. A match cue over 400 ms is a game that
# feels slow: the next tap arrives before the reward for the last one is over.
CEILING_SECONDS = 0.40

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUT = ROOT / "game/Assets/Resources/Audio"
NAMES = ("place", "match", "refused")

# One seed, so the noise transients are the same bytes on every machine.
SEED = 60_10


# ---------------------------------------------------------------------------
# building blocks
# ---------------------------------------------------------------------------

def _frames(seconds: float) -> int:
    return int(round(seconds * SAMPLE_RATE))


def _time(seconds: float) -> np.ndarray:
    return np.arange(_frames(seconds), dtype=np.float64) / SAMPLE_RATE


def modes(seconds: float, partials, phase: float = 0.0) -> np.ndarray:
    """Sum of exponentially decaying sines: (hz, amplitude, decay tau).

    This is the whole of a struck rigid object. Which frequencies are present
    says what the object is made of; how fast each one dies says how big and how
    damped it is.
    """
    t = _time(seconds)
    out = np.zeros_like(t)
    for hz, amp, tau in partials:
        out += amp * np.exp(-t / tau) * np.sin(2 * np.pi * hz * t + phase)
    return out


def lowpass(x: np.ndarray, cutoff_hz: float, poles: int = 1) -> np.ndarray:
    """One-pole RC lowpass, applied `poles` times. -6 dB/octave per pass."""
    a = 1.0 - np.exp(-2.0 * np.pi * cutoff_hz / SAMPLE_RATE)
    y = x
    for _ in range(poles):
        out = np.empty_like(y)
        acc = 0.0
        for i, v in enumerate(y):
            acc += a * (v - acc)
            out[i] = acc
        y = out
    return y


def bandpass(x: np.ndarray, low_hz: float, high_hz: float) -> np.ndarray:
    """Crude two-pole band: everything under `low_hz` subtracted back off."""
    return lowpass(x, high_hz, poles=2) - lowpass(x, low_hz, poles=1)


def contact(seconds: float, tau: float, low_hz: float, high_hz: float,
            rng: np.random.Generator) -> np.ndarray:
    """The instant two surfaces meet: a very short burst of banded noise.

    Without it a knock is a tone with a fast attack — recognisably a synthesizer.
    With it, the ear hears an impact and then a body ringing.

    Banded, not merely low-passed, for two reasons. The top is cut because the
    leftover hiss of white noise at 2 a.m. is the one thing these sounds must
    never be. The bottom is cut because a phone speaker cannot reproduce it
    anyway, so that energy only eats headroom the audible part needs.
    """
    n = _frames(seconds)
    burst = rng.standard_normal(n) * np.exp(-np.arange(n) / (tau * SAMPLE_RATE))
    return bandpass(burst, low_hz, high_hz)


def attack(x: np.ndarray, seconds: float) -> np.ndarray:
    """Raised-cosine rise over the first `seconds` — softens a hard onset."""
    n = min(_frames(seconds), x.size)
    if n <= 1:
        return x
    ramp = 0.5 - 0.5 * np.cos(np.pi * np.arange(n) / n)
    out = x.copy()
    out[:n] *= ramp
    return out


def tail(x: np.ndarray, seconds: float) -> np.ndarray:
    """Ramp the last `seconds` to exactly zero.

    A file that stops while the waveform is still off zero ends with a step, and
    a step is a click — the one artefact that would make these sounds cheap.
    """
    n = min(_frames(seconds), x.size)
    if n <= 1:
        return x
    ramp = 0.5 + 0.5 * np.cos(np.pi * np.arange(n) / n)
    out = x.copy()
    out[-n:] *= ramp
    out[-1] = 0.0
    return out


def peak_at(x: np.ndarray, peak: float) -> np.ndarray:
    """Scale so the loudest sample sits exactly at `peak` of full scale."""
    loudest = np.max(np.abs(x))
    if loudest == 0.0:
        return x
    return x * (peak / loudest)


def pad_to(x: np.ndarray, seconds: float) -> np.ndarray:
    n = _frames(seconds)
    out = np.zeros(n, dtype=np.float64)
    out[:min(n, x.size)] = x[:n]
    return out


# ---------------------------------------------------------------------------
# the three cues
# ---------------------------------------------------------------------------

def place(rng: np.random.Generator) -> np.ndarray:
    """A small wooden object set down on a shelf. 85 ms, low, soft."""
    seconds = 0.085
    # 212 Hz body; ratios 2.30 / 4.06 / 6.49 / 9.72 are deliberately not
    # 2 / 3 / 4. Each partial is damped harder than the one below it, which is
    # the other half of what makes wood sound like wood: the bright part of a
    # knock is gone in 10 ms while the body is still going at 60.
    #
    # The upper modes are loud relative to the body on purpose. Measured
    # against a first pass that used the textbook amplitudes, 86% of the energy
    # landed below 300 Hz — true to the physics of a wooden block and inaudible
    # on the speaker this game is actually played through.
    body = modes(seconds, [
        (212.0, 0.85, 0.016),
        (487.0, 0.95, 0.016),
        (861.0, 0.65, 0.010),
        (1375.0, 0.30, 0.0060),
        (2060.0, 0.14, 0.0035),
    ])
    knock = pad_to(contact(0.008, 0.0025, 320.0, 3200.0, rng), seconds)
    x = body + 0.9 * knock
    x = attack(x, 0.0008)          # 0.8 ms — an impact, but not a digital edge
    x = tail(x, 0.010)
    return peak_at(x, 0.32)


def match(rng: np.random.Generator) -> np.ndarray:
    """Three things clicking into place: G4-C5-E5, mallets, 360 ms."""
    seconds = 0.36
    out = np.zeros(_frames(seconds), dtype=np.float64)
    # Rising and resolving. Amplitude grows a little across the three so the
    # phrase lands on the last note instead of trailing off.
    notes = [(392.00, 0.00, 0.85, 0.095),
             (523.25, 0.062, 0.92, 0.095),
             (659.25, 0.124, 1.00, 0.105)]
    for hz, onset, amp, tau in notes:
        span = seconds - onset
        # Marimba-ish: strong fundamental, quiet 4th partial, a trace of wood
        # high up. Nothing bright enough to fizz on a phone speaker.
        note = modes(span, [
            (hz * 1.0, 1.00, tau),
            (hz * 2.0, 0.22, tau * 0.55),
            (hz * 4.0, 0.15, tau * 0.30),
            (hz * 9.2, 0.05, tau * 0.13),
        ])
        note += 0.10 * pad_to(contact(0.004, 0.0010, 400.0, 3800.0, rng), span)
        note = attack(note, 0.0015)
        start = _frames(onset)
        out[start:start + note.size] += amp * note
    out = tail(out, 0.030)
    return peak_at(out, 0.36)


def refused(_rng: np.random.Generator) -> np.ndarray:
    """A shelf that will not take it: a short flat note, 140 ms, dull, quiet."""
    seconds = 0.14
    # G3 with two harmonic partials: a note, not a thud, and not a chord —
    # there is nothing to be dissonant about, the move simply did not happen.
    # Harmonic where `place` is inharmonic, which is most of why the two do not
    # sound like the same event.
    x = modes(seconds, [
        (196.00, 0.80, 0.020),
        (392.00, 0.85, 0.018),
        (588.00, 0.35, 0.010),
    ])
    x = lowpass(x, 1800.0, poles=2)  # dull, but not so dull a phone loses it
    x = attack(x, 0.005)             # no contact transient: nothing struck
    x = tail(x, 0.015)
    return peak_at(x, 0.18)


CUES = {"place": place, "match": match, "refused": refused}


# ---------------------------------------------------------------------------
# writing
# ---------------------------------------------------------------------------

def to_pcm16(x: np.ndarray) -> np.ndarray:
    """Float [-1, 1] to signed 16-bit, clamped one LSB inside full scale."""
    return np.clip(np.rint(x * 32767.0), -32767, 32767).astype("<i2")


def write_wav(path: Path, x: np.ndarray) -> int:
    with wave.open(str(path), "wb") as w:
        w.setnchannels(CHANNELS)
        w.setsampwidth(SAMPLE_WIDTH)
        w.setframerate(SAMPLE_RATE)
        w.writeframes(to_pcm16(x).tobytes())
    return path.stat().st_size


def meta_text(asset: str) -> str:
    """A Unity .meta with a guid derived from the asset name.

    Unity writes one of these on first import with a random guid, so without it
    every machine that opens the project produces a different guid for the same
    file and the next commit carries the churn. PCM, decompressed on load,
    preloaded: these are 8-35 KB each, and Vorbis would trade nothing for a
    decode on the frame the player taps.
    """
    guid = hashlib.md5(f"catshelter.audio.{asset}".encode()).hexdigest()
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "AudioImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 6\n"
        "  defaultSettings:\n"
        "    loadType: 0\n"
        "    sampleRateSetting: 0\n"
        f"    sampleRateOverride: {SAMPLE_RATE}\n"
        "    compressionFormat: 0\n"
        "    quality: 1\n"
        "    conversionMode: 0\n"
        "  platformSettingOverrides: {}\n"
        "  forceToMono: 0\n"
        "  normalize: 0\n"
        "  preloadAudioData: 1\n"
        "  loadInBackground: 0\n"
        "  ambisonic: 0\n"
        "  3D: 0\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def folder_meta_text(folder: str) -> str:
    guid = hashlib.md5(f"catshelter.audio.folder.{folder}".encode()).hexdigest()
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def describe(x: np.ndarray) -> str:
    """Peak, RMS and spectral centroid — the three numbers worth reading here.

    Nobody in this pipeline can hear the files, so these stand in: peak for
    headroom, RMS for how loud it will actually feel, centroid for how bright.
    """
    q = to_pcm16(x).astype(np.float64) / 32767.0
    spectrum = np.abs(np.fft.rfft(q))
    freqs = np.fft.rfftfreq(q.size, 1.0 / SAMPLE_RATE)
    weight = spectrum.sum()
    centroid = float((freqs * spectrum).sum() / weight) if weight else 0.0
    return (f"{q.size / SAMPLE_RATE * 1000:6.1f} ms  "
            f"peak {np.max(np.abs(q)):.3f}  "
            f"rms {np.sqrt(np.mean(q ** 2)):.4f}  "
            f"centroid {centroid:6.0f} Hz")


def generate(out_dir: Path, quiet: bool = False) -> dict[str, int]:
    out_dir.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(SEED)
    sizes: dict[str, int] = {}
    for name in NAMES:
        x = CUES[name](rng)
        seconds = x.size / SAMPLE_RATE
        if seconds > CEILING_SECONDS:
            raise ValueError(f"{name}: {seconds:.3f}s over the "
                             f"{CEILING_SECONDS:.2f}s ceiling")
        wav = out_dir / f"{name}.wav"
        sizes[name] = write_wav(wav, x)
        meta = out_dir / f"{name}.wav.meta"
        if not meta.exists():
            meta.write_text(meta_text(name))
        if not quiet:
            print(f"{name:>8}.wav  {describe(x)}  {sizes[name]:6d} B")
    folder = out_dir.parent / f"{out_dir.name}.meta"
    if not folder.exists():
        folder.write_text(folder_meta_text(out_dir.name))
    if not quiet:
        print(f"{'total':>8}      {sum(sizes.values()):6d} B in {out_dir}")
    return sizes


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out-dir", type=Path, default=DEFAULT_OUT,
                    help=f"where the .wav files go (default: {DEFAULT_OUT})")
    ap.add_argument("--quiet", action="store_true", help="no report on stdout")
    args = ap.parse_args()
    generate(args.out_dir, quiet=args.quiet)


if __name__ == "__main__":
    main()
