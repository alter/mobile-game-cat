"""Task 50-photo/05: the on-device marks plugin agrees with the rest of the game.

Nothing here runs Vision. Neither iOS 17 request in `CatMarks.swift`
(`VNGenerateForegroundInstanceMaskRequest`, `VNDetectAnimalBodyPoseRequest`)
runs in the simulator, and there is no device — see NOTES-marks.md. So no test
can be written that measures a photograph.

What CAN be checked, and is worth checking precisely because a device run is
not available, is every place where the three sides of this feature agree only
by somebody having typed the same string three times:

  Plugins/iOS/CatMarks.swift   emits  {"place": "...", "delta": ...}
  Shell/CatMarks.cs            decodes it with UnityEngine.JsonUtility
  Core/CatTraits.cs            validates the place and throws if it is unknown

Each of those failures is silent. A misspelt place name compiles on both sides
and throws inside a player's game. A field named `lightness` in Swift and
`brightness` in C# compiles on both sides, and JsonUtility quietly leaves it 0
— it reports no error for a field it cannot match, which is the single most
expensive thing about that class.
"""
import json
import re
from pathlib import Path

import pytest

REPO = Path(__file__).resolve().parents[2]
SWIFT = REPO / "game/Assets/Plugins/iOS/CatMarks.swift"
CSHARP = REPO / "game/Assets/Shell/CatMarks.cs"
TRAITS = REPO / "game/Assets/Core/CatTraits.cs"
SCHEMA = REPO / "tools/traits/schema.json"


def without_comments(text):
    """Swift source with `//` and `/* */` removed.

    Every check below is about what the code does. This file argues its own
    design at length in prose, and a comment explaining why `"light"` is NOT
    decided in Swift would otherwise fail the test that asserts exactly that —
    the checker reading the argument as the crime.
    """
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


@pytest.fixture(scope="module")
def swift():
    return without_comments(SWIFT.read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def csharp():
    return CSHARP.read_text(encoding="utf-8")


def schema_places():
    spots = json.loads(SCHEMA.read_text(encoding="utf-8"))["properties"]["spots"]
    return set(spots["items"]["properties"]["place"]["enum"])


def swift_places(text):
    """Every string the plugin can put in a mark's `place` field.

    Both shapes it uses: `Recipe(place: "chest", ...)` for the pose rung and
    `record("paws", ...)` for the mask-only one.
    """
    return (set(re.findall(r'Recipe\(place:\s*"([a-z_]+)"', text))
            | set(re.findall(r'\brecord\(\s*"([a-z_]+)"', text)))


# --- the place names ---------------------------------------------------------

def test_every_place_the_plugin_emits_is_a_real_spot_place(swift):
    """A place C# cannot construct a CatSpot from crashes on a player's phone.

    `CatSpot`'s constructor calls `CatTraits.CheckValue`, which throws on
    anything outside the table. The one deliberate exception is `paws`, which
    is not a place at all: it is both front paws as one number on the mask-only
    rung, and `MarksAnswer.ToSpots` drops it by its `grouped` flag rather than
    by its name.
    """
    allowed = schema_places() | {"paws"}
    unknown = swift_places(swift) - allowed
    assert not unknown, (
        f"{SWIFT.name} emits places the game cannot draw: {sorted(unknown)}. "
        f"CatSpot would throw on each of these.")


def test_the_plugin_reaches_every_place_the_game_can_draw(swift):
    """Ten places exist; a place with no recipe is one the game can never see.

    This is the check that would have caught `tail_tip` being left out — it is
    the one place not in the `recipes` table, because which of the three tail
    joints is the tip is decided by geometry rather than by name.
    """
    missing = schema_places() - swift_places(swift)
    assert not missing, (
        f"{SWIFT.name} has no way to measure {sorted(missing)}, so those marks "
        f"can only ever come from the language model.")


def test_the_schema_and_CatTraits_still_agree_about_places():
    """The two lists this test rests on are themselves two copies of one list."""
    table = re.search(r'\["spot_place"\]\s*=\s*new\[\]\s*\{(.*?)\}',
                      TRAITS.read_text(encoding="utf-8"), re.S)
    assert table, "could not find spot_place in CatTraits.cs"
    assert set(re.findall(r'"([a-z_]+)"', table.group(1))) == schema_places()


# --- the JSON boundary -------------------------------------------------------

def swift_encoded_fields(text, name):
    """Swift stored properties of an Encodable struct, in declaration order.

    Handles both `let a: T` and the several-on-one-line `let a: T, b: T` this
    file uses for coordinate pairs — the shape that made the first draft of
    this parser drop `y` and `imageHeight` in silence, which is the same class
    of mistake the test itself is for.

    JSONEncoder uses the property name verbatim unless CodingKeys says
    otherwise, and this file declares none — asserted below.
    """
    body = re.search(r'struct\s+%s\s*:\s*Encodable\s*\{(.*?)\n\}' % re.escape(name),
                     text, re.S)
    assert body, f"no Encodable struct {name} in {SWIFT.name}"
    fields = []
    for line in re.findall(r'^\s*let\s+(.+)$', body.group(1), re.M):
        for part in line.split(","):
            identifier = re.match(r'\s*([A-Za-z_]\w*)', part)
            if identifier and ":" in part:
                fields.append(identifier.group(1))
    return fields


def csharp_serialised_fields(text, name):
    """Public instance fields of a [Serializable] C# struct.

    Expression-bodied properties are excluded on the `=>`: JsonUtility
    serialises fields only, and every property in this file is a computed one
    that must NOT appear in the JSON.
    """
    body = re.search(r'struct\s+%s\s*\{(.*?)\n    \}' % re.escape(name), text, re.S)
    assert body, f"no struct {name} in {CSHARP.name}"
    fields = []
    for match in re.finditer(r'^\s*public\s+[\w\[\]<>]+\s+([^;{}=\n]+);\s*$',
                             body.group(1), re.M):
        fields += [part.strip() for part in match.group(1).split(",")]
    return fields


@pytest.mark.parametrize("swift_name,csharp_name", [
    ("Mark", "MeasuredMark"),
    ("Landmark", "PoseLandmark"),
    ("Answer", "MarksAnswer"),
])
def test_both_sides_of_the_bridge_name_the_same_fields(swift, csharp,
                                                       swift_name, csharp_name):
    """JsonUtility matches by name and says nothing when it cannot.

    A field Swift writes and C# does not declare is dropped in silence; a field
    C# declares and Swift never writes stays at its zero value, in silence.
    Either one is a measurement that reads as "no mark" on a device nobody has,
    which is not a bug anyone would find quickly.
    """
    assert set(swift_encoded_fields(swift, swift_name)) == \
        set(csharp_serialised_fields(csharp, csharp_name)), (
            f"{swift_name} (Swift) and {csharp_name} (C#) have drifted apart")


def test_the_swift_side_declares_no_CodingKeys(swift):
    """The test above is only valid while JSONEncoder uses the property names."""
    assert "CodingKeys" not in swift


def test_the_rung_names_match_on_both_sides(swift, csharp):
    """C#'s `Rung` constants are what callers branch on; Swift decides them."""
    emitted = set(re.findall(r'rung\s*=\s*"(\w+)"', swift))
    declared = set(re.findall(r'public const string \w+ = "(\w+)"', csharp))
    assert emitted == declared, (
        f"Swift can return {sorted(emitted)}; C# names {sorted(declared)}")


# --- the promise the feature is built on -------------------------------------

def test_the_photograph_is_never_written_down_or_logged(swift):
    """"It is her cat" is the whole premise, so the photo must not leave.

    Not a proof — a native file can always be made to write one — but it fails
    the day somebody adds a debug dump while chasing a measurement, which is
    exactly when it would be added and exactly when nobody would notice.
    """
    forbidden = {
        "NSLog": "logs to the device console",
        "os_log": "logs to the device console",
        "print(": "logs to stdout",
        "FileManager": "touches the file system",
        "URLSession": "could send it off the device",
        "UIPasteboard": "could copy it out of the app",
        "write(to": "writes a file",
        "UserDefaults": "persists across launches",
    }
    found = [f"{k} ({why})" for k, why in forbidden.items() if k in swift]
    assert not found, f"{SWIFT.name} does something with the photo it must not: {found}"


def test_the_threshold_lives_in_csharp_and_not_in_swift(swift, csharp):
    """The native side reports a number; the verdict is tunable without a build.

    A `light`/`dark` decision made in Swift could only ever be re-tuned by
    rebuilding the native plugin, and the reference set cannot be run on a
    simulator at all — so the threshold has to sit where a test can reach it.
    """
    assert "public const double Threshold" in csharp
    assert '"light"' not in swift and '"dark"' not in swift, (
        f"{SWIFT.name} decides a shade; that decision belongs to C#")
