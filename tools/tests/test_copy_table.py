"""Tasks 60-shell-build/16 and /12: the copy lives in one table, in English.

Reads the sources because View and Shell are not compiled by build/core-tests,
so no C# test can see them.
"""
import re
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
COPY = ROOT / "game/Assets/Shell/Copy.cs"
UI_DIRS = [ROOT / "game/Assets/View", ROOT / "game/Assets/Shell"]

# Files exempt from the no-literals rule, with the reason.
EXEMPT = {
    "Copy.cs",            # the table itself
    "VisionSelfTest.cs",  # debug harness, never shown to a player
    "SaveFile.cs",        # log lines only
    "CatPicker.cs",       # failure reasons handed to Copy.Of("capture.failed")
}


def keys() -> set[str]:
    return set(re.findall(r'\["([a-z0-9_.]+)"\]\s*=', COPY.read_text()))


def sources():
    for directory in UI_DIRS:
        for path in sorted(directory.rglob("*.cs")):
            if path.name not in EXEMPT:
                yield path, path.read_text()


def strip_noise(text: str) -> str:
    text = re.sub(r"//.*", "", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return text


def test_the_table_is_not_empty_and_has_no_duplicate_keys():
    raw = re.findall(r'\["([a-z0-9_.]+)"\]\s*=', COPY.read_text())
    assert len(raw) > 20
    assert len(raw) == len(set(raw)), "a duplicate key silently wins or loses"


@pytest.mark.parametrize("path", [p for p, _ in sources()], ids=lambda p: p.name)
def test_no_player_visible_english_outside_the_table(path):
    # A sentence: two or more words, at least one space, starting with a capital.
    sentence = re.compile(r'"[A-Z][a-z]+(?: [A-Za-z0-9â€™\'…,.!?%-]+){1,}"')
    found = [m for m in sentence.findall(strip_noise(path.read_text()))
             # names of things, not copy
             if not re.match(r'"[A-Z][a-z]+ [A-Z]', m)]
    assert not found, f"{path.name}: move these into Copy.cs -> {found}"


# Keys are not always written next to Copy.Of: one call picks between two with
# a ternary. So any literal shaped like a key counts as a use.
KEY_LITERAL = re.compile(r'"([a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+)"')


def used_keys() -> set[str]:
    found = set()
    for _, text in sources():
        found |= set(KEY_LITERAL.findall(strip_noise(text)))
    return found


def test_every_key_the_code_asks_for_exists():
    declared = keys()
    for path, text in sources():
        for key in re.findall(r'Copy\.Of\("([a-z0-9_.]+)"', text):
            assert key in declared, f"{path.name} asks for a missing key: {key}"


def test_every_declared_key_is_used():
    # An unused key is copy nobody sees, and it gets translated anyway.
    unused = keys() - used_keys()
    assert not unused, f"unused keys: {sorted(unused)}"


def test_the_copy_is_english():
    # Task 12-copy-english: zero non-English strings anywhere a player can see
    # them. Cyrillic is the one that would actually turn up here.
    cyrillic = re.compile(r"[а-яА-ЯёЁ]")
    for path, text in list(sources()) + [(COPY, COPY.read_text())]:
        for line in text.splitlines():
            if line.strip().startswith("//"):
                continue
            assert not cyrillic.search(line), f"{path.name}: {line.strip()}"


def test_analytics_names_are_not_in_the_table():
    # Event names are protocol, not copy. Translating them breaks the funnel.
    table = COPY.read_text()
    for name in ("app:open", "photo:uploaded", "level_start", "booster:tap"):
        assert name not in table, f"{name} is protocol and must stay out of the copy table"


def test_format_placeholders_are_balanced():
    # A string with {0} reached through the no-argument Of() renders literally.
    text = COPY.read_text()
    for key, value in re.findall(r'\["([a-z0-9_.]+)"\]\s*=\s*"((?:[^"\\]|\\.)*)"', text):
        if "{" not in value:
            continue
        indices = sorted(int(n) for n in re.findall(r"\{(\d+)\}", value))
        assert indices == list(range(len(set(indices)))), \
            f"{key}: placeholders {indices} are not 0..n"
