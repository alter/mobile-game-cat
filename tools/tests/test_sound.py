"""Task 60-shell-build/10, sound pass: the three cues in `Resources/Audio`.

Nobody in this pipeline can hear these files, so every claim made about them
has to be a measured one. That is what this suite is: the properties that would
otherwise be checked by listening — length, headroom, no click at the edges, the
three cues in the intended loudness order, the match reading as three events and
not one — written as arithmetic.

The strongest test here is `test_committed_wavs_match_the_generator`. It runs
`tools/sound/make_sounds.py` into a temporary directory and compares bytes with
what is in git. It is what makes the script the source of the sound rather than
a plausible-looking file that happens to sit next to it.
"""
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np
import pytest

from tools.sound import make_sounds as sounds

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / "game/Assets/Resources/Audio"
FEEDBACK_CS = ROOT / "game/Assets/Shell/Feedback.cs"

NAMES = ("place", "match", "refused")

# Per-cue ceilings, tighter than the generator's global 400 ms. A placement is
# heard on every single move: at 120 ms two fast taps already overlap, and the
# second one is the more important of the two.
CEILINGS = {"place": 0.120, "match": 0.400, "refused": 0.200}

# Mobile: today's APK is about 50 MB, and three UI cues have no business being a
# measurable part of it. 56 KB is what the current set costs; the budget is that
# with room to add a fourth cue, and it fails loudly if a future one arrives as
# a five-second stereo pad.
TOTAL_BYTES_BUDGET = 96 * 1024


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

def read_wav(path: Path):
    """Returns (channels, sample_width, framerate, samples as float [-1, 1])."""
    with wave.open(str(path), "rb") as w:
        raw = w.readframes(w.getnframes())
        params = (w.getnchannels(), w.getsampwidth(), w.getframerate())
    x = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32767.0
    return (*params, x)


def samples(name: str) -> np.ndarray:
    return read_wav(AUDIO / f"{name}.wav")[3]


def rms(x: np.ndarray) -> float:
    return float(np.sqrt(np.mean(x ** 2)))


def envelope(x: np.ndarray, window_ms: float = 15.0) -> np.ndarray:
    """Loudness over time, smoothed to about the ear's own integration window.

    15 ms and not less: at 5 ms the envelope of a mallet note still shows the
    partials wobbling against each other, which is not something anyone hears as
    a separate event.
    """
    n = max(1, int(window_ms / 1000 * sounds.SAMPLE_RATE))
    return np.convolve(np.abs(x), np.ones(n) / n, mode="same")


def onsets(x: np.ndarray, floor: float = 0.25, hop_ms: float = 5.0,
           spacing_ms: float = 45.0) -> int:
    """How many separate attacks the ear would hear.

    Rising edges of the envelope, with a refractory gap so that one attack is
    not counted twice. One for a knock, three for three notes.

    The envelope is read on a 5 ms grid before differencing, not sample by
    sample: the difference of a moving average taken one sample at a time is
    two rectified samples subtracted from each other, which follows the carrier
    rather than the loudness, and finds an "onset" every few milliseconds.
    """
    env = envelope(x)[::max(1, int(hop_ms / 1000 * sounds.SAMPLE_RATE))]
    rise = np.diff(env)
    gap = max(1, int(spacing_ms / hop_ms))
    found: list[int] = []
    for i in np.flatnonzero(rise > floor * rise.max()):
        if found and i - found[-1] < gap:
            continue
        found.append(int(i))
    return len(found)


def energy_above(x: np.ndarray, hz: float) -> float:
    """Fraction of the signal's energy above `hz`."""
    power = np.abs(np.fft.rfft(x)) ** 2
    freqs = np.fft.rfftfreq(x.size, 1.0 / sounds.SAMPLE_RATE)
    total = power.sum()
    return float(power[freqs >= hz].sum() / total) if total else 0.0


# ---------------------------------------------------------------------------
# the files exist and are what they say they are
# ---------------------------------------------------------------------------

@pytest.mark.parametrize("name", NAMES)
def test_the_wav_exists(name):
    assert (AUDIO / f"{name}.wav").is_file()


@pytest.mark.parametrize("name", NAMES)
def test_mono_48k_16bit(name):
    channels, width, rate, _ = read_wav(AUDIO / f"{name}.wav")
    # 48 kHz matches AudioManager.asset's m_OutputSamplingRate, so nothing is
    # resampled at play time; 16-bit and mono because a UI cue needs neither
    # more resolution nor a position in space.
    assert (channels, width, rate) == (1, 2, 48_000)


@pytest.mark.parametrize("name", NAMES)
def test_under_its_ceiling(name):
    seconds = samples(name).size / sounds.SAMPLE_RATE
    assert seconds <= CEILINGS[name], f"{name} is {seconds * 1000:.0f} ms"
    assert seconds <= sounds.CEILING_SECONDS


@pytest.mark.parametrize("name", NAMES)
def test_nothing_clips(name):
    x = samples(name)
    peak = float(np.max(np.abs(x)))
    assert peak < 1.0
    # Not merely "does not clip": these are meant to leave real headroom, so
    # that the game is quiet by default and a later mix has somewhere to go.
    assert peak <= 0.5, f"{name} peaks at {peak:.3f} — louder than intended"
    assert peak >= 0.10, f"{name} peaks at {peak:.3f} — effectively silent"


@pytest.mark.parametrize("name", NAMES)
def test_no_click_at_either_edge(name):
    """A waveform that starts or stops away from zero ends in a step, and a step
    is a click — the one artefact that would make these sound cheap."""
    x = samples(name)
    assert abs(x[0]) <= 0.002
    assert abs(x[-1]) <= 0.002


@pytest.mark.parametrize("name", NAMES)
def test_no_dc_offset(name):
    """A DC offset wastes headroom and thumps the speaker on every play."""
    assert abs(float(np.mean(samples(name)))) < 0.002


# ---------------------------------------------------------------------------
# the sounds are the ones that were designed
# ---------------------------------------------------------------------------

def test_loudness_order_is_refused_place_match():
    """The reward is the loudest thing the player hears and the refusal the
    quietest. This is the whole of the mix, and it lives in the files."""
    quiet, mid, loud = (rms(samples(n)) for n in ("refused", "place", "match"))
    assert quiet < mid < loud
    # And not by so much that a match startles someone playing in bed.
    assert loud / mid < 3.0


def test_match_is_three_events_and_the_others_are_one():
    """`match` is three things clicking into place, so it has to *sound* like
    three; `place` and `refused` are single objects and must not."""
    assert onsets(samples("match")) == 3
    assert onsets(samples("place")) == 1
    assert onsets(samples("refused")) == 1


def test_match_sits_higher_than_place():
    """Brighter and warmer than a placement, which is what makes the reward
    unmistakable without making it louder."""
    def median_hz(x):
        power = np.abs(np.fft.rfft(x)) ** 2
        freqs = np.fft.rfftfreq(x.size, 1.0 / sounds.SAMPLE_RATE)
        return float(freqs[np.searchsorted(np.cumsum(power) / power.sum(), 0.5)])

    assert median_hz(samples("match")) > median_hz(samples("refused"))
    assert median_hz(samples("refused")) < median_hz(samples("place"))


@pytest.mark.parametrize("name", NAMES)
def test_survives_a_phone_speaker(name):
    """At least a third of the energy has to be above 300 Hz.

    A phone speaker radiates almost nothing below that, so a cue voiced purely
    on its low modes is honest physics and an inaudible sound. The first pass at
    `place` and `refused` failed this at 0.14 and 0.01, which is exactly the
    kind of thing that is invisible until someone plays the build on a phone.
    """
    assert energy_above(samples(name), 300.0) >= 0.33


@pytest.mark.parametrize("name", NAMES)
def test_no_hiss_up_top(name):
    """Nothing above 8 kHz worth speaking of. Leftover noise-burst top end is
    what turns a wooden knock into a hiss, and a hiss at 2 a.m. is the failure
    mode that matters for this game."""
    assert energy_above(samples(name), 8000.0) < 0.01


# ---------------------------------------------------------------------------
# the generator is the source of the files
# ---------------------------------------------------------------------------

def test_committed_wavs_match_the_generator(tmp_path):
    subprocess.run(
        [sys.executable, str(ROOT / "tools/sound/make_sounds.py"),
         "--out-dir", str(tmp_path), "--quiet"],
        check=True, cwd=ROOT, capture_output=True,
    )
    for name in NAMES:
        assert (tmp_path / f"{name}.wav").read_bytes() == \
               (AUDIO / f"{name}.wav").read_bytes(), \
               f"{name}.wav in git is not what make_sounds.py produces"


def test_generator_refuses_to_write_something_too_long(monkeypatch, tmp_path):
    """The ceiling is enforced where the files are written, not only here."""
    monkeypatch.setitem(sounds.CUES, "place",
                        lambda rng: np.zeros(sounds.SAMPLE_RATE))  # one second
    with pytest.raises(ValueError, match="ceiling"):
        sounds.generate(tmp_path, quiet=True)


def test_total_size_is_mobile_sized():
    total = sum((AUDIO / f"{n}.wav").stat().st_size for n in NAMES)
    assert total <= TOTAL_BYTES_BUDGET, f"{total} B of audio"


@pytest.mark.parametrize("name", NAMES)
def test_meta_pins_a_guid(name):
    """Without a committed .meta, every machine that opens the project invents
    its own guid for the same file and the next commit carries the churn."""
    meta = (AUDIO / f"{name}.wav.meta").read_text()
    assert "AudioImporter:" in meta
    assert "compressionFormat: 0" in meta   # PCM: no decode on the tapped frame
    guid = [l for l in meta.splitlines() if l.startswith("guid: ")][0][6:]
    assert len(guid) == 32 and all(c in "0123456789abcdef" for c in guid)


def test_the_folder_has_a_meta_too():
    assert (AUDIO.parent / "Audio.meta").is_file()


# ---------------------------------------------------------------------------
# the shell plays them
# ---------------------------------------------------------------------------

def test_feedback_loads_exactly_these_three():
    """A renamed file is a silent cue: `Resources.Load` returns null and the
    game goes on quietly. No C# test can catch it — those assemblies are not
    compiled outside Unity — so it is caught here."""
    source = FEEDBACK_CS.read_text()
    for name in NAMES:
        assert f'Load("{name}")' in source, f"Feedback.cs never loads {name}"
    assert 'ClipFolder = "Audio/"' in source


def test_feedback_has_a_persistent_mute():
    """Sound the player cannot turn off is sound the player turns off by
    deleting the game."""
    source = FEEDBACK_CS.read_text()
    assert "public static bool Muted" in source
    assert "PlayerPrefs.SetInt(MutedKey" in source
    assert "PlayerPrefs.GetInt(MutedKey" in source


def test_every_cue_has_a_method():
    source = FEEDBACK_CS.read_text()
    for method in ("public static void Place()", "public static void Match()",
                   "public static void Refused()"):
        assert method in source
