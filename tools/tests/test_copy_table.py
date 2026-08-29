"""Tasks 60-shell-build/16 and /12: the copy lives in tables, one per language.

Reads the sources because View and Shell are not compiled by build/core-tests,
so no C# test can see them.

2026-08-28, when Russian was added: the checks that used to read Copy.cs as one
flat table now read it as a set of them, and four failures that a second
language makes possible are new here — a key present in one language and not
another, a key no language but English has, a placeholder count that differs
between languages, and a value left sitting in English. The third is the one
with teeth: a translation that drops a `{0}` prints the literal characters
`{0}` on a player's screen, and a translation that *adds* one to a key called
through the no-argument `Copy.Of` does the same on a lock screen.
"""
import re
from collections import Counter
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
SHELL = ROOT / "game/Assets/Shell"
COPY = SHELL / "Copy.cs"

# 2026-08-29: the tables were split across files by script, because seventeen
# languages of forty-eight strings in one file is four thousand lines nobody
# can review and two translators cannot work in at once. `Copy.cs` keeps
# English, Russian and the machinery; `Copy.Latin.cs` and `Copy.Scripts.cs`
# are partial-class companions that fill in the rest through the
# `AddLatinScript` / `AddOtherScripts` hooks.
#
# Globbed rather than listed, and deliberately so: a companion added and not
# named here would be a whole script's worth of translation that no check in
# this file ever looks at, which is the exact failure the glob makes
# impossible. `Copy.cs` stays first — it holds the reference language, and
# every parse below reads these in order.
#
# The glob cannot match `Copy.cs` itself (the pattern needs two dots) and
# cannot match a `.meta` (it must end `.cs`), so neither is doubled up.
COPY_SOURCES = [COPY] + sorted(SHELL.glob("Copy.*.cs"))
UI_DIRS = [ROOT / "game/Assets/View", ROOT / "game/Assets/Shell"]
SWIFT_DIR = ROOT / "game/Assets/Plugins"

# 50-photo/06 VERIFY item 1: the outcome->key mapping moved to
# Core/PhotoMessages.cs so dotnet test can guard it. Core is not in UI_DIRS
# (it is compiled by build/core-tests, and scanning all of it here would be
# noise), but the four literal keys now live only in this one file — without
# listing it, test_every_declared_key_is_used would call them orphans.
EXTRA_KEY_FILES = [ROOT / "game/Assets/Core/PhotoMessages.cs"]

# Files exempt from the no-literals rule, with the reason.
#
# CatPicker.cs was exempted here until 2026-08-27 for "failure reasons handed
# to Copy.Of('capture.failed')" — true, but the reasons themselves were raw
# English (some arriving from CatPicker.swift, one able to carry a
# system-language OS error string), substituted straight into a tabled
# template. That is exactly the leak this test exists to catch, and the
# exemption hid it twice over: once here, and once by this file never having
# scanned Swift at all (see SWIFT_EXEMPT below). CatPicker.cs now sends only
# fixed lowercase reason codes and needs no exemption — its reason stopped
# holding, so it was removed rather than reworded.
EXEMPT = {
    "VisionSelfTest.cs",  # debug harness, never shown to a player
    "SaveFile.cs",        # log lines only
    # Reached only by dropping a `glyphs.txt` beside the save, like the coat
    # harness — no player ever sees it. Its samples MUST be written into the
    # file: the screen exists to answer "can this build draw Thai" before any
    # Thai table exists, and to keep answering after one is deleted. Reading
    # them from `Copy` would make it agree with itself and prove nothing. It
    # is the one file where an untabled Russian sentence is the point rather
    # than the leak.
    "GlyphCheckView.cs",
}
# The tables themselves — `Copy.cs` and every by-script companion beside it.
# Exempted by the same glob that parses them, not by a hand-kept list: a
# companion left out of this set would have all of its translated sentences
# read as loose English literals by
# `test_no_player_visible_english_outside_the_table`, and the file would have
# to be added in two places to be added at all.
EXEMPT |= {path.name for path in COPY_SOURCES}

# Same idea, for game/Assets/Plugins/**/*.swift. Empty today: no native file
# needs to hand the player a sentence, and none should — a reason a native
# layer wants known should be a code, mapped to copy on the C# side of the
# boundary, per CatPicker.cs's own class doc.
SWIFT_EXEMPT = set()


# Copy.cs holds one
#   public static readonly IReadOnlyDictionary<string, string> <Language> = ...
# per language. Sliced on the declaration line rather than by matching braces:
# every table opens one, a table runs to the next declaration or to the end of
# the file, and nothing else in the file has that exact modifier list — the
# `Tables` map is private, `For` and `Current` are not readonly.
TABLE_DECL = re.compile(
    r"public\s+static\s+readonly\s+IReadOnlyDictionary<string,\s*string>\s+(\w+)\s*=")

# One entry. The value may be a single literal or several joined with "+"
# across lines ("lose.body", "capture.hint", "house.complete.body").
ENTRY = re.compile(
    r'\["([a-z0-9_.]+)"\]\s*=\s*((?:"(?:[^"\\]|\\.)*"\s*(?:\+\s*)?)+),')
LITERAL_PIECE = re.compile(r'"((?:[^"\\]|\\.)*)"')

# The reference language. Every other table is checked against it rather than
# against the union of all of them: a union has no author and cannot say which
# side of a mismatch is the mistake.
REFERENCE = "English"


def parsed_tables() -> list[tuple[Path, str, str]]:
    """(file, language, body) for every table in every copy source.

    A table runs from its own declaration line to the next one **in the same
    file**, or to that file's end — the slicing that already worked inside
    `Copy.cs`, applied per file rather than to one blob, so a companion's last
    table stops at its own closing brace instead of swallowing the next file.
    """
    found = []
    for path in COPY_SOURCES:
        text = strip_noise(path.read_text())
        marks = [(m.group(1), m.start()) for m in TABLE_DECL.finditer(text)]
        assert marks, \
            f"no language table found in {path.name} — these checks are not running"
        found += [(path, name,
                   text[start:(marks[i + 1][1] if i + 1 < len(marks) else len(text))])
                  for i, (name, start) in enumerate(marks)]
    return found


def table_bodies() -> list[tuple[str, str]]:
    return [(name, body) for _, name, body in parsed_tables()]


def tables() -> dict[str, dict[str, str]]:
    out = {}
    for path, name, body in parsed_tables():
        assert name not in out, (
            f"two tables are called {name} — the second, in {path.name}, silently "
            f"replaces the first in every check here and in Copy.BuildTables")
        out[name] = {key: "".join(LITERAL_PIECE.findall(value))
                     for key, value in ENTRY.findall(body)}
    assert REFERENCE in out, f"Copy.cs has no {REFERENCE} table to check against"
    return out


def copy_text() -> str:
    """Every copy source as one string, comments stripped. For the checks that
    are about the files rather than about a single table."""
    return "\n".join(strip_noise(path.read_text()) for path in COPY_SOURCES)


def keys() -> set[str]:
    """The reference language's keys. Every other language having exactly
    these is its own test below, so the rest of this file needs only one set."""
    return set(tables()[REFERENCE])


def sources():
    for directory in UI_DIRS:
        for path in sorted(directory.rglob("*.cs")):
            if path.name not in EXEMPT:
                yield path, path.read_text()
    for path in EXTRA_KEY_FILES:
        yield path, path.read_text()


def swift_sources():
    for path in sorted(SWIFT_DIR.rglob("*.swift")):
        if path.name not in SWIFT_EXEMPT:
            yield path, path.read_text()


def strip_noise(text: str) -> str:
    text = re.sub(r"//.*", "", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return text


def test_every_table_is_not_empty_and_has_no_duplicate_keys():
    for name, body in table_bodies():
        raw = [key for key, _ in ENTRY.findall(body)]
        assert len(raw) > 20, f"{name}: {len(raw)} entries parsed — is it being read at all?"
        assert len(raw) == len(set(raw)), f"{name}: a duplicate key silently wins or loses"


# A sentence: two or more words, at least one space, starting with a capital.
SENTENCE = re.compile(r'"[A-Z][a-z]+(?: [A-Za-z0-9â€™\'…,.!?%-]+){1,}"')

# 60-shell-build/16 VERIFY, 2026-08-27: a sentence split across "+"-joined
# literals reads as one player-visible string but not one literal, so
# SENTENCE above — which only ever looks inside a single "..." — cannot see
# it once no individual fragment happens to open with a capital letter
# ("Could" + " not read the picked image..." was the proof). This closes
# that one shape: two or more PLAIN literals chained with "+".
#
# Deliberately narrow. `(?<!\$)` excludes C# interpolated strings (`$"..."`)
# on purpose — nearly every diagnostic Debug.Log/NSLog line in this codebase
# is built that way (VisionSelfTest.cs, EveningReminder.cs, CoatBuilder.cs),
# and running the same check on those would flag routine log-message
# assembly as if it were copy, which is exactly the kind of false alarm that
# gets a check turned off rather than heeded. It also means the *joined*
# text has to pass the same bar as a single literal always did — a
# concatenated file path, JSON fragment or format string will not, in
# general, read as a capitalised multi-word sentence, so this is the same
# threshold as before, applied to one more shape of literal, not a looser
# threshold applied to everything.
#
# Known, named limit, not chased further here: string interpolation
# (`$"...{x}..."` in C#, `"...\(x)..."` in Swift) can build a sentence the
# same way and is invisible to both checks. Closing that — or concatenation
# built through a variable instead of inline — is not a regex problem
# anymore; it needs either a linter with real syntax understanding or an
# architectural rule that all player-visible text is constructed by exactly
# one call (`Copy.Of`), so there is only one place to check in the first
# place. Naming that is the honest stopping point for a source-text scan.
PLAIN_LITERAL = r'(?<!\$)"(?:[^"\\]|\\.)*"'
CONCAT_CHAIN = re.compile(rf'{PLAIN_LITERAL}(?:\s*\+\s*{PLAIN_LITERAL})+')


def _concatenated_sentences(text: str) -> list[str]:
    found = []
    for chain in CONCAT_CHAIN.finditer(text):
        pieces = re.findall(r'"((?:[^"\\]|\\.)*)"', chain.group(0))
        joined = '"' + "".join(pieces) + '"'
        if SENTENCE.fullmatch(joined) and not re.match(r'"[A-Z][a-z]+ [A-Z]', joined):
            found.append(chain.group(0))
    return found


def _sentence_literals(text):
    stripped = strip_noise(text)
    found = [m for m in SENTENCE.findall(stripped)
            # names of things, not copy
            if not re.match(r'"[A-Z][a-z]+ [A-Z]', m)]
    found += _concatenated_sentences(stripped)
    return found


@pytest.mark.parametrize("path", [p for p, _ in sources()], ids=lambda p: p.name)
def test_no_player_visible_english_outside_the_table(path):
    found = _sentence_literals(path.read_text())
    assert not found, f"{path.name}: move these into Copy.cs -> {found}"


@pytest.mark.parametrize("path", [p for p, _ in swift_sources()], ids=lambda p: p.name)
def test_no_player_visible_english_in_swift(path):
    # Task 60-shell-build/16 VERIFY: CatPicker.swift used to send prose
    # ("could not save the picked image: ...") across UnitySendMessage,
    # invisible to this file because it only ever scanned *.cs. A native
    # layer should hand back a reason code, not a sentence — the C# side
    # (Shell/CatPicker.cs) maps codes to Copy.cs keys, and never displays a
    # native string verbatim.
    found = _sentence_literals(path.read_text())
    assert not found, (
        f"{path.name}: this looks like prose crossing the native boundary "
        f"-> {found}. Send a reason code instead and map it to a Copy.cs "
        f"key on the C# side."
    )


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


CYRILLIC = re.compile(r"[а-яА-ЯёЁ]")


def test_no_cyrillic_outside_the_copy_table():
    # Task 12-copy-english, narrowed on 2026-08-28 rather than dropped. Until
    # Russian existed this also covered Copy.cs, and it cannot any more; what
    # it was actually protecting is unchanged, and is the stronger half — that
    # no translated word reaches a player from anywhere except a table. A
    # Cyrillic string in a View file is a string no other language can ever
    # override. Swift is included: it reaches the player exactly as View/Shell
    # can, over UnitySendMessage (60-shell-build/16 VERIFY).
    for path, text in (list(sources()) + list(swift_sources())):
        for line in text.splitlines():
            if line.strip().startswith("//"):
                continue
            assert not CYRILLIC.search(line), f"{path.name}: {line.strip()}"


def test_the_english_table_is_english():
    # The other half of the check above, now that Copy.cs is allowed Cyrillic:
    # it is allowed it in the Russian table and nowhere else. A Russian value
    # pasted into the English table is invisible to every other check here —
    # the key exists, the placeholders match, it is used — and shows a
    # Cyrillic sentence to an English player.
    for key, value in sorted(tables()[REFERENCE].items()):
        assert not CYRILLIC.search(value), f"{REFERENCE}[{key}] is not English: {value}"


# Everything a Latin-script table may not contain. Not a whitelist of allowed
# characters: Vietnamese alone needs most of Latin Extended Additional, Turkish
# needs the dotless i, and enumerating what is *permitted* would be a list that
# grows with every language and fails on the first correct string nobody
# anticipated. The scripts below are the ones this project's other copy file is
# for, so finding one here means a value landed in the wrong file.
NON_LATIN = re.compile(
    "[Ͱ-Ͽ"      # Greek
    "Ѐ-ӿ"       # Cyrillic
    "֐-׿"       # Hebrew
    "؀-ۿ"       # Arabic
    "ऀ-ॿ"       # Devanagari
    "฀-๿"       # Thai
    "぀-ヿ"       # kana
    "一-鿿"       # CJK
    "가-힯]")     # Hangul


def test_each_copy_file_holds_the_script_it_is_named_for():
    # `Copy.Latin.cs` exists so that eight Latin-script languages can be
    # reviewed and edited without touching the file the non-Latin ones live in.
    # A value pasted into the wrong one still passes every other check here —
    # the key exists, the placeholders match, it is not English — and the split
    # stops meaning anything the first time it happens silently.
    #
    # Only the Latin file is checked, and in one direction. The companion holds
    # several scripts at once by design, so "what belongs in Copy.Scripts.cs"
    # is not a property a regex can state; "no Cyrillic, Greek, CJK, Arabic,
    # Hebrew, Devanagari, Thai or Hangul in the Latin file" is.
    latin = [(path, name, body) for path, name, body in parsed_tables()
             if path.name == "Copy.Latin.cs"]
    if not latin:
        pytest.skip("no Copy.Latin.cs in this tree")
    for path, name, body in latin:
        for key, value in ENTRY.findall(body):
            text = "".join(LITERAL_PIECE.findall(value))
            stray = NON_LATIN.findall(text)
            assert not stray, (
                f"{name}[{key}] in {path.name} is not Latin script ({stray}) — "
                f"it belongs in the companion file, not this one")


def test_every_language_has_exactly_the_reference_keys():
    # Both directions, and they fail differently. A key missing from a
    # language renders as "[win.next]" on that player's button — Copy.Of's
    # deliberate loud fallback, which is right for a typo caught in a
    # screenshot and wrong for a language shipped with a hole in it. A key a
    # language has and English does not is copy no call site can reach, which
    # is a translator's work thrown away silently.
    all_tables = tables()
    assert len(all_tables) >= 2, \
        "only one table in Copy.cs — this file's cross-language checks prove nothing"
    reference = set(all_tables[REFERENCE])
    for name, table in sorted(all_tables.items()):
        missing = sorted(reference - set(table))
        extra = sorted(set(table) - reference)
        assert not missing, f"{name} is missing keys {REFERENCE} has: {missing}"
        assert not extra, f"{name} has keys {REFERENCE} does not: {extra}"


PLACEHOLDER = re.compile(r"\{(\d+)\}")


def test_placeholders_are_identical_in_every_language():
    # Counted per index, not totalled: "{0} in {0}" and "{0} in {1}" have the
    # same number of placeholders and are not the same format string. A
    # translation that drops one prints nothing where a name should be; one
    # that keeps a {1} the English no longer passes throws FormatException at
    # the moment the card is shown.
    all_tables = tables()
    reference = all_tables[REFERENCE]
    for name, table in sorted(all_tables.items()):
        if name == REFERENCE:
            continue
        for key, value in sorted(table.items()):
            if key not in reference:
                continue        # test_every_language_has_exactly_the_reference_keys owns that
            want = Counter(PLACEHOLDER.findall(reference[key]))
            got = Counter(PLACEHOLDER.findall(value))
            assert got == want, (
                f"{name}[{key}]: placeholders {sorted(got.elements())} do not match "
                f"{REFERENCE}'s {sorted(want.elements())} — the difference reaches "
                f"the screen as literal braces or as a FormatException")


# The app's own name. The one string that is meant to be identical everywhere:
# a caption naming something no store search finds sends nobody anywhere.
SAME_IN_EVERY_LANGUAGE = {"card.game_name"}


def test_no_value_was_left_untranslated():
    # A key added to English and copied into the other tables to get this file
    # green is the failure mode a parity check invites. Caught cheaply: an
    # identical string in two languages is either the app's name or an
    # oversight, and the first is a list of one.
    all_tables = tables()
    reference = all_tables[REFERENCE]
    for name, table in sorted(all_tables.items()):
        if name == REFERENCE:
            continue
        same = sorted(key for key, value in table.items()
                      if key not in SAME_IN_EVERY_LANGUAGE
                      and reference.get(key) == value)
        assert not same, f"{name} still holds the {REFERENCE} string for: {same}"


def test_every_table_is_selectable_by_a_device_language():
    # A table nothing maps to is a language that never reaches a player, and
    # every other check in this file would pass while it sat there.
    #
    # Read across every copy source, not just the file the table is written
    # in: a companion's tables are selected from that companion's own
    # `AddLatinScript` / `AddOtherScripts` body, and `Copy.cs` never names
    # them. The `tables[SystemLanguage.Spanish] = Spanish;` a hook is written
    # with matches the same pattern as `Copy.cs`'s own dictionary initialiser,
    # so one regex still covers both shapes.
    text = copy_text()
    for name, _ in table_bodies():
        assert re.search(rf"\[SystemLanguage\.\w+\]\s*=\s*{name}\b", text), \
            f"{name} is a table no device language selects — add it to Copy.Tables"


def test_every_table_is_selected_by_its_own_language():
    # The sharper half of the check above, and the one a copy-paste of eight
    # hook lines actually gets wrong: `tables[SystemLanguage.Spanish] =
    # Portuguese;` passes every other check in this file — both tables exist,
    # both are reachable, both have the right keys — and ships Portuguese to
    # Spain while Portuguese-speaking Brazil gets English, because nothing maps
    # to it any more.
    #
    # It holds because every table here is named for its own SystemLanguage
    # value. If a language ever needs a table whose name differs from its
    # enum value (a regional variant, say), this is the check to widen.
    text = copy_text()
    for name, _ in table_bodies():
        assert re.search(rf"\[SystemLanguage\.{name}\]\s*=\s*{name}\b", text), \
            (f"{name} is not selected by SystemLanguage.{name} — some other "
             f"language's table is, or none is")


def test_analytics_names_are_not_in_the_table():
    # Event names are protocol, not copy. Translating them breaks the funnel.
    table = "\n".join(path.read_text() for path in COPY_SOURCES)
    for name in ("app:open", "photo:uploaded", "level_start", "booster:tap"):
        assert name not in table, f"{name} is protocol and must stay out of the copy table"


def test_format_placeholders_are_balanced():
    # string.Format indexes into the argument array, so a value whose
    # placeholders are not 0..n throws on the n it was never passed. Checked in
    # every language: the English one being 0..n says nothing about a
    # translation that renumbered them to put the name last.
    for name, table in sorted(tables().items()):
        for key, value in sorted(table.items()):
            if "{" not in value:
                continue
            indices = sorted(int(n) for n in PLACEHOLDER.findall(value))
            assert indices == list(range(len(set(indices)))), \
                f"{name}[{key}]: placeholders {indices} are not 0..n"


# `Copy.Of("key")` with nothing after the key — the un-formatted overload.
NO_ARG_CALL = re.compile(r'Copy\.Of\("([a-z0-9_.]+)"\s*\)')


def test_keys_read_without_arguments_have_no_placeholders():
    # The trap this task was warned about, closed for every language at once.
    # EveningReminder.cs reads "notification.title" through the overload that
    # takes no arguments and does no formatting, so a "{0}" added to that value
    # — in English or in a translation, by someone reasonably wanting the
    # kitten's typed name in it — is delivered to a lock screen as the four
    # characters `{0}`. The key and its call site have to change together, and
    # this fails until they do.
    no_args = set()
    for _, text in sources():
        no_args |= set(NO_ARG_CALL.findall(strip_noise(text)))
    assert no_args, "no un-formatted Copy.Of call found — this check is not running"
    for name, table in sorted(tables().items()):
        for key in sorted(no_args & set(table)):
            assert "{" not in table[key], (
                f"{name}[{key}] has a placeholder, but every call site reads it "
                f"through Copy.Of(key) with no arguments — it would print the "
                f"braces verbatim. Change the call site in the same commit.")
