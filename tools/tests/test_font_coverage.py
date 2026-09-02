"""Task 60-shell-build/23: a subset font can drop a glyph silently.

2026-09-01 (commit 9478eb4): four rewritten sentences added 25 characters to
the Japanese/Korean/Chinese tables that the shipped, hand-subsetted Noto
faces did not have yet. Nothing in the build failed — a translator's new
word simply became an empty box on a player's screen, on iOS only (Android
quietly borrows the missing glyph from the OS; that is exactly the
undocumented behaviour `FontFallbacks.cs` exists to stop relying on). It was
found by reading the four `.otf` cmaps by hand. This file automates that
reading.

Scope, decided by `FontFallbacks.cs` rather than guessed here: seven scripts
travel with their own subset font (its `Faces` list, cross-checked below
against `game/Assets/Resources/Fonts`) and are the only ones this file can
check — a character is either in that font's cmap or it is a box. Every
other language (English, Russian, and the eight Latin-script tables in
`Copy.Latin.cs`) draws through Unity's own default panel face plus the OS,
never touched by `tools/fonts/subset.py`, so there is no shipped font file
here to hold it against — nothing to assert.
"""
import functools
import re

import pytest
from fontTools.ttLib import TTFont

from tools.tests.test_copy_table import ROOT, SHELL, tables

FONTS_DIR = ROOT / "game/Assets/Resources/Fonts"
FALLBACKS_CS = SHELL / "FontFallbacks.cs"

# Matches an entry of FontFallbacks.Faces, e.g. "Fonts/NotoSansJP-Regular SDF".
FACE_ENTRY = re.compile(r'"Fonts/(NotoSans\w+-Regular) SDF"')

# The seven non-Latin language tables (Copy.Scripts.cs) and the Noto face
# each is subset from. This is the one piece of knowledge this file adds on
# top of FontFallbacks.cs: which *language table* corresponds to which
# script face, since the .cs file only lists faces, not table names. Guarded
# by test_the_fallback_faces_are_exactly_the_known_scripts below, so a face
# added or removed in FontFallbacks.cs without a matching update here fails
# loudly instead of leaving a table unchecked.
OWN_FONT_TABLES = {
    "ChineseSimplified": "NotoSansSC-Regular",
    "ChineseTraditional": "NotoSansTC-Regular",
    "Japanese": "NotoSansJP-Regular",
    "Korean": "NotoSansKR-Regular",
    "Thai": "NotoSansThai-Regular",
    "Arabic": "NotoSansArabic-Regular",
    "Hindi": "NotoSansDevanagari-Regular",
}

# Only characters inside a table's own script are checked against its font —
# not every character the table happens to contain. A Thai or Hindi line
# still carries the odd Latin letter (card.game_name, "Sootpaw", is the same
# string in every language) or plain ASCII punctuation, and those are drawn
# by the engine's own default face before the fallback list is ever
# consulted — Panel text resolution tries the primary face per character
# first, same as FontFallbacks.cs's own docstring describes. Checking those
# against the script-specific subset font produced false failures here
# (basic Latin letters flagged as "missing" from NotoSansThai) that have
# nothing to do with the actual defect this file exists to catch.
#
# Boundaries copied from test_copy_table.py's NON_LATIN, which already
# separates these blocks for the Latin/non-Latin file split; kept as the
# same numbers here rather than re-derived, so the two files cannot silently
# disagree about where a script starts.
SCRIPT_RANGES = {
    "ChineseSimplified": [(0x4E00, 0x9FFF)],            # CJK Unified Ideographs
    "ChineseTraditional": [(0x4E00, 0x9FFF)],
    "Japanese": [(0x3040, 0x30FF), (0x4E00, 0x9FFF)],   # kana + CJK
    "Korean": [(0xAC00, 0xD7AF)],                       # Hangul syllables
    "Thai": [(0x0E00, 0x0E7F)],
    "Arabic": [(0x0600, 0x06FF)],
    "Hindi": [(0x0900, 0x097F)],                        # Devanagari
}
assert set(SCRIPT_RANGES) == set(OWN_FONT_TABLES)


def in_script(ch: str, language: str) -> bool:
    code = ord(ch)
    return any(lo <= code <= hi for lo, hi in SCRIPT_RANGES[language])


def test_the_fallback_faces_are_exactly_the_known_scripts():
    # If a face is ever added to or removed from FontFallbacks.Faces without
    # this file's map being updated to match, the language it covers would
    # either go unchecked (silently, the exact failure this file exists to
    # close) or this test names the drift instead.
    text = FALLBACKS_CS.read_text()
    faces = set(FACE_ENTRY.findall(text))
    assert faces, f"no Faces entries parsed from {FALLBACKS_CS.name} — parsing broke"
    known = set(OWN_FONT_TABLES.values())
    assert faces == known, (
        f"FontFallbacks.cs lists {sorted(faces)} but this file's "
        f"OWN_FONT_TABLES map is {sorted(known)} — update OWN_FONT_TABLES to "
        f"match, so the character check below covers the right font for the "
        f"right language")


def font_path(face: str):
    # Every font file sits beside a Unity ".*.meta" sidecar, which also
    # matches "face.*" — exclude it explicitly rather than assuming the glob
    # only ever finds one real extension.
    matches = sorted(p for p in FONTS_DIR.glob(face + ".*") if p.suffix != ".meta")
    assert matches, (
        f"no {face}.* in {FONTS_DIR} — run tools/fonts/subset.py before this "
        f"test can check anything")
    assert len(matches) == 1, f"more than one file matches {face}.*: {matches}"
    return matches[0]


@functools.lru_cache(maxsize=None)
def cmap_for(face: str) -> frozenset:
    font = TTFont(str(font_path(face)))
    return frozenset(font.getBestCmap())


@pytest.mark.parametrize("language", sorted(OWN_FONT_TABLES), ids=str)
def test_every_character_is_in_its_subset_fonts_cmap(language):
    table = tables().get(language)
    assert table, f"no {language} table found in Copy* sources — parsing broke"
    cmap = cmap_for(OWN_FONT_TABLES[language])
    missing = set()
    for value in table.values():
        missing |= {ch for ch in value
                    if in_script(ch, language) and ord(ch) not in cmap}
    assert not missing, (
        f"{language}: {len(missing)} character(s) missing from "
        f"{OWN_FONT_TABLES[language]}'s cmap — these render as empty boxes "
        f"on iOS (commit 9478eb4 is the last time this happened by hand): "
        f"{''.join(sorted(missing))}. Re-run tools/fonts/subset.py against "
        f"the master Noto sources.")
