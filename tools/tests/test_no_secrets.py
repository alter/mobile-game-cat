"""No credential may enter the repository, by any path.

Added 2026-08-27 after a verifier established that the GameAnalytics SDK's
`SettingsGA` getter creates `Assets/Resources/GameAnalytics/Settings.asset`
the first time anything touches it inside the Editor, that the asset holds
`gameKey`/`secretKey` as serialized fields, and that the SDK ships an
Inspector tab inviting a person to type keys straight into it. A .gitignore
rule was added for it — but a rule only protects files nobody has added yet,
and `git add -f`, or a rule written after the file was committed, protects
nothing at all. So this checks the tracked tree itself.

**Scope, chosen deliberately.** Data files are scanned for a credential-shaped
value under a credential-shaped name; source files are scanned only for a
*literal* that looks like a key. Source assigns from variables constantly —
`gameKey = lines[0]` is correct code and must not fail a build — and a test
that cries wolf on ordinary source is a test somebody deletes. The risk this
is written against is a key sitting in a settings asset or a config file, and
that is what it catches.
"""
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

SECRET_NAMES = ("gameKey", "secretKey", "game_key", "secret_key",
                "ANTHROPIC_API_KEY", "apiKey", "api_key")
NAME_RE = "|".join(SECRET_NAMES)

# Data: key: value, "key": "value", - key: value, <string>value</string> after
# a <key>name</key>. Values are taken loosely and filtered below.
DATA_ASSIGN = re.compile(
    r'["\']?(' + NAME_RE + r')["\']?\s*[:=]\s*["\']?([^"\'\s,}\]<]+)', re.IGNORECASE)

# Source: only a quoted literal counts, never an expression.
SOURCE_LITERAL = re.compile(
    r'["\']?(' + NAME_RE + r')["\']?\s*[:=]\s*["\']([^"\']+)["\']', re.IGNORECASE)

DATA_SUFFIXES = {".asset", ".json", ".jsonc", ".txt", ".plist", ".yaml", ".yml",
                 ".xml", ".env", ".cfg", ".ini", ".properties"}
SOURCE_SUFFIXES = {".cs", ".ts", ".js", ".py", ".swift", ".sh", ".java"}

HARMLESS = {"", "null", "none", "nil", "0", "false", "true", "[]", "{}", "-",
            "todo", "changeme", "string", "string;", "unknown"}

# A real key from either vendor is long and mixed. Anything shorter than this
# is a placeholder, a type name or a fragment, and saying so here is more
# honest than a list of exceptions that grows every time the test annoys
# somebody.
MIN_KEY_LENGTH = 20


def _looks_like_a_key(value: str) -> bool:
    v = value.strip().strip("\"'")
    if v.lower() in HARMLESS or len(v) < MIN_KEY_LENGTH:
        return False
    if v.startswith(("$", "{", "<", "%", "//", "#")) or v.endswith(">"):
        return False          # a reference or a placeholder, not a value
    # Deliberate stubs say so in their own text.
    if "not-a-real" in v.lower() or "example" in v.lower() or "test-key" in v.lower():
        return False
    # Keys are dense in letters and digits; prose and paths are not.
    alnum = sum(c.isalnum() for c in v)
    return alnum / len(v) > 0.8 and any(c.isdigit() for c in v)


def _tracked() -> list[Path]:
    out = subprocess.run(["git", "ls-files"], cwd=ROOT,
                         capture_output=True, text=True, check=True)
    return [ROOT / line for line in out.stdout.splitlines() if line]


def test_no_credential_value_is_tracked():
    offenders = []
    for path in _tracked():
        if path.name in {"test_no_secrets.py", ".gitignore"}:
            continue          # these name the fields; neither carries a value
        suffix = path.suffix.lower()
        pattern = (DATA_ASSIGN if suffix in DATA_SUFFIXES
                   else SOURCE_LITERAL if suffix in SOURCE_SUFFIXES else None)
        if pattern is None:
            continue
        try:
            text = path.read_text(errors="ignore")
        except OSError:
            continue
        for name, value in pattern.findall(text):
            if _looks_like_a_key(value):
                offenders.append(f"{path.relative_to(ROOT)}: {name} = {value!r}")
    assert not offenders, ("a credential value appears in a tracked file:\n  "
                           + "\n  ".join(offenders))


def test_the_guard_would_notice_a_real_key(tmp_path):
    """The check above passes today; prove it can fail."""
    assert _looks_like_a_key("5f2c1ab9d34e47f0a1b8c7d6e5f40312")
    assert _looks_like_a_key("ga_live_9K2mQ7xR4tZ1pL8vN3wS6yB0")
    assert not _looks_like_a_key("lines[0]")
    assert not _looks_like_a_key("string;")
    assert not _looks_like_a_key("test-key-not-a-real-one")
    assert not _looks_like_a_key("")


def test_the_settings_asset_is_ignored():
    """The SDK creates this on the first Editor open; it must already be ignored."""
    r = subprocess.run(["git", "check-ignore", "-q",
                        "game/Assets/Resources/GameAnalytics/Settings.asset"], cwd=ROOT)
    assert r.returncode == 0, (
        "game/Assets/Resources/GameAnalytics/Settings.asset is not ignored; the "
        "SDK creates it on first Editor open and it holds gameKey and secretKey "
        "as serialized fields")


def test_the_keys_file_is_ignored():
    r = subprocess.run(["git", "check-ignore", "-q", "analytics-keys.txt"], cwd=ROOT)
    assert r.returncode == 0, "analytics-keys.txt is not ignored"
