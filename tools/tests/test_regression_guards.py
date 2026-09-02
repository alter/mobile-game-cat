"""Task 60-shell-build/27: four fixes from 2026-09-02, pinned so they cannot
regress silently. Per tasks/AUDIT-2026-08-28.md: "утверждение, называющее
число, счёт или файл, требует теста, а не предложения." Each guard below
names a commit that fixed a real, observed bug; the test is what stops the
same bug coming back unnoticed.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHELL = ROOT / "game/Assets/Shell"
VIEW = ROOT / "game/Assets/View"
GAME_BOOT = SHELL / "GameBoot.cs"
CAT_IDENTITY = SHELL / "CatIdentity.cs"
CAT_SAVE_FILE = SHELL / "CatSaveFile.cs"
CAPTURE_SCREEN = VIEW / "CaptureScreen.cs"
CAT_COAT = SHELL / "CatCoat.cs"
COAT_BUILDER = VIEW / "CoatBuilder.cs"


def strip_comments(text: str) -> str:
    # Same rule as test_analytics_call_sites.strip_comments: a call site
    # named only in a comment (GameBoot.cs and CaptureScreen.cs both explain
    # these fixes in prose right next to the code) must not count as a call.
    text = re.sub(r"//.*", "", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return text


def all_cs_sources() -> list[tuple[Path, str]]:
    dirs = [SHELL, VIEW, ROOT / "game/Assets/Core", ROOT / "game/Assets/Editor"]
    return [(p, strip_comments(p.read_text()))
            for d in dirs if d.exists() for p in d.rglob("*.cs")]


# ---------------------------------------------------------------------------
# Guard 1 (commit 22a073f): cat.save is written from exactly one place — the
# player confirming her cat's name in GameBoot's meet-your-cat flow.
# CatIdentity.Traits used to write it on a bare property READ, which quietly
# closed the first-run gate (HasACat() below) before the player ever saw the
# capture screen. The fix removed that write; nothing may bring it back.
# ---------------------------------------------------------------------------

WRITE_CALL = re.compile(r"CatSaveFile\.Write\s*\(")


def test_cat_save_file_write_has_exactly_one_call_site():
    sites = [(p.name, len(WRITE_CALL.findall(text)))
             for p, text in all_cs_sources() if WRITE_CALL.search(text)]
    total = sum(n for _, n in sites)
    assert total == 1, (
        f"CatSaveFile.Write should be called from exactly one place "
        f"(GameBoot's OnNamed handler); found {total} call(s): {sites}")
    assert sites == [("GameBoot.cs", 1)], (
        f"CatSaveFile.Write moved out of GameBoot.cs: {sites}")


def test_cat_identity_never_writes_the_save():
    # The specific regression from 22a073f: CatIdentity.Traits wrote on read.
    text = strip_comments(CAT_IDENTITY.read_text())
    assert not WRITE_CALL.search(text), (
        "CatIdentity.cs calls CatSaveFile.Write — this silently closes the "
        "first-run gate the moment anything reads CatIdentity.Traits, "
        "exactly the bug commit 22a073f fixed")


def test_the_one_write_site_is_inside_on_named():
    # Not just "somewhere in GameBoot.cs" — inside the handler that fires
    # when the player confirms a name, not e.g. in Awake or OnEnable.
    text = strip_comments(GAME_BOOT.read_text())
    on_named = text.index("screen.OnNamed = cat =>")
    write = text.index("CatSaveFile.Write(")
    # The handler is a short lambda; the next member after it is GoToTheHouse.
    go_to_house_call = text.index("GoToTheHouse(root);", write)
    assert on_named < write < go_to_house_call, (
        "CatSaveFile.Write moved out of the OnNamed handler")


# ---------------------------------------------------------------------------
# Guard 2 (commit efd3354): CatVision.Recognise / CatVision.Silhouette /
# CatCoat.Read never block the main thread — they run through Shell/OffMain.cs
# or through CatCoat.ReadOverFrames's frame-sliced coroutine path.
#
# A strong "no unwrapped call anywhere" check is not buildable from regex
# alone (a call three lines inside a method that eventually reaches
# OffMain.Run looks identical, in text, to one that does not). So this is
# the deliberately weak-but-honest version the task asked for: a pinned
# inventory of every direct call site, each checked against the OffMain.Run
# (or ReadOverFrames) call that is supposed to wrap it. It DOES catch: a new
# raw call appearing anywhere else in View/Shell (the count below changes),
# or one of the three known sites losing its wrapping OffMain.Run line. It
# does NOT catch: someone inlining the body of Recognise/SubjectBox/Look
# directly into Handle()/ReadOverFrames without going through OffMain.Run —
# the text would still contain "OffMain.Run" nearby and pass.
# ---------------------------------------------------------------------------

DIRECT_CALL = re.compile(r"CatVision\.(Recognise|Silhouette)\s*\(|CatCoat\.Read\s*\(")

# (file, exact substring the call site must still appear in) — each is a lazy
# Func field or a private helper, never invoked except through OffMain.Run.
LAZY_WRAPPED_SITES = [
    (CAPTURE_SCREEN,
     "public Func<byte[], VisionAnswer> Recognise = bytes => CatVision.Recognise(bytes);"),
    (CAPTURE_SCREEN, "var cut = CatVision.Silhouette(photo);"),
    (CAT_COAT, "return CatVision.Silhouette(croppedJpeg, orientation);"),
]

# VisionSelfTest.cs is the one deliberate exception: a dormant, device-only
# harness (50-photo/05) that runs Recognise synchronously over a `visiontest`
# folder nobody ships — it exists to be pushed onto a device by a tester, not
# reached by a player, so blocking the main thread there was never the bug
# efd3354 fixed. Pinned by name so a NEW blocking call cannot hide behind it.
SYNC_TEST_ONLY_SITES = [
    (SHELL / "VisionSelfTest.cs", "answer = CatVision.Recognise(File.ReadAllBytes(path));"),
]

KNOWN_DIRECT_SITES = LAZY_WRAPPED_SITES + SYNC_TEST_ONLY_SITES

# Each lazy site above must be reached through one of these OffMain.Run
# lines, or the coroutine path never actually gets it off the main thread.
WRAPPING_OFFMAIN_LINES = [
    (CAPTURE_SCREEN, 'OffMain.Run(() => Recognise(photo), "recognise")'),
    (CAPTURE_SCREEN, 'OffMain.Run(() => SubjectBox(photo), "subject box")'),
    (CAT_COAT, 'OffMain.Run(() => Look(croppedJpeg, orientation), "coat mask")'),
]


def test_no_new_direct_vision_call_sites_appeared():
    sites = [(p.name, m.group(0))
             for p, text in [(f, strip_comments(f.read_text()))
                              for f in list(VIEW.rglob("*.cs")) + list(SHELL.rglob("*.cs"))]
             for m in DIRECT_CALL.finditer(text)]
    assert len(sites) == len(KNOWN_DIRECT_SITES), (
        f"expected exactly {len(KNOWN_DIRECT_SITES)} direct CatVision/"
        f"CatCoat.Read call sites (all lazy, wrapped by OffMain.Run "
        f"elsewhere); found {len(sites)}: {sites}")


def test_known_direct_sites_are_still_the_lazy_wrapped_ones():
    for path, needle in KNOWN_DIRECT_SITES:
        text = strip_comments(path.read_text())
        assert needle in text, (
            f"{path.name} no longer contains the expected lazy call site "
            f"{needle!r} — a direct call may have moved into a method body")


def test_lazy_sites_are_actually_wrapped_by_offmain():
    for path, needle in WRAPPING_OFFMAIN_LINES:
        text = strip_comments(path.read_text())
        assert needle in text, (
            f"{path.name} no longer wraps its recognition call with "
            f"{needle!r} — it may now run on the main thread")


def test_catcoat_read_blocking_form_is_never_called_in_production():
    # Read() (as opposed to ReadOverFrames()) runs all three steps blocking
    # on the calling thread. It exists for the editor/tools, not the game;
    # zero real call sites in View/Shell is the whole guarantee.
    calls = [p.name for p, text in
             [(f, strip_comments(f.read_text()))
              for f in list(VIEW.rglob("*.cs")) + list(SHELL.rglob("*.cs"))]
             if re.search(r"CatCoat\.Read\s*\(", text)]
    assert calls == [], f"CatCoat.Read (blocking) called from: {calls}"


def test_offmain_used_at_least_four_times_in_capture_screen():
    # Baseline measured today: recognise, subject box, crop, marks — four
    # native calls taken off the main thread. A drop below this is a call
    # that moved back onto it without anyone deleting OffMain itself.
    text = strip_comments(CAPTURE_SCREEN.read_text())
    count = len(re.findall(r"OffMain\.Run\(", text))
    assert count >= 4, f"OffMain.Run used only {count} times in CaptureScreen.cs"


# ---------------------------------------------------------------------------
# Guard 3 (commit ddc64d7): nobody calls CoatBuilder.TryBuild directly on a
# full 1024 silhouette — that measured 21.8s on the iOS simulator (see the
# comment above TryBuild's Downscale doc in CoatBuilder.cs). Production code
# goes through TryBuildFor/TryBuildForOverFrames, which downscale first; the
# two Editor bake tools call TryBuild directly but always through
# Downscale(art, size) with size <= 512.
# ---------------------------------------------------------------------------

TRY_BUILD_DIRECT = re.compile(r"CoatBuilder\.TryBuild\s*\(")
BAKE_TOOLS = {"BakeDefaultCoats.cs", "BakeTraitSet.cs"}


def test_try_build_called_directly_only_from_editor_bake_tools():
    # The regex requires "(" right after "TryBuild", so it naturally does
    # not match "TryBuildFor(" or "TryBuildForOverFrames(" — those are a
    # different identifier, not TryBuild with trailing text.
    sites = [(p.name, m.start()) for p, text in all_cs_sources()
             for m in TRY_BUILD_DIRECT.finditer(text)]
    files = sorted({name for name, _ in sites})
    assert files == sorted(BAKE_TOOLS), (
        f"CoatBuilder.TryBuild called directly (not via TryBuildFor/"
        f"TryBuildForOverFrames) from: {files} — production code must use "
        f"the sized wrappers, not the raw 1024 build")


def test_bake_tools_downscale_before_calling_try_build():
    for name in BAKE_TOOLS:
        path = ROOT / "game/Assets/Editor" / name
        text = strip_comments(path.read_text())
        for call in re.findall(r"CoatBuilder\.TryBuild\(([^;]*?)\);", text, re.S):
            assert "Downscale(" in call, (
                f"{name} calls CoatBuilder.TryBuild without Downscale(...): "
                f"{call.strip()!r}")


def test_bake_tool_sizes_are_at_most_512():
    # The constants that decide what size actually reaches TryBuild.
    default_text = (ROOT / "game/Assets/Editor/BakeDefaultCoats.cs").read_text()
    default_size = re.search(r"private const int Size = (\d+);", default_text)
    card_size = re.search(r"private const int CardSize = (\d+);", default_text)
    trait_size = re.search(r"private const int Size = (\d+);",
                            (ROOT / "game/Assets/Editor/BakeTraitSet.cs").read_text())
    assert default_size and card_size and trait_size
    assert int(default_size.group(1)) <= 512
    assert int(card_size.group(1)) <= 512
    assert int(trait_size.group(1)) <= 512


# ---------------------------------------------------------------------------
# Guard 4 (commit 22a073f: "Оба AddComponent получили проверку повторного
# добавления, как остальные шесть"): every AddComponent<T>() in GameBoot.cs
# is guarded by a GetComponent<T>() == null (or == null / != null branch)
# check, so OnEnable running twice — it can, UI Toolkit re-enables panels —
# never stacks a second screen component on the GameObject.
# ---------------------------------------------------------------------------

ADD_COMPONENT = re.compile(r"gameObject\.AddComponent<([\w.]+)>\(\)")
GET_COMPONENT = re.compile(r"GetComponent<([\w.]+)>\(\)")
LOOKBACK_LINES = 6


def test_every_add_component_in_game_boot_is_guarded():
    lines = GAME_BOOT.read_text().splitlines()
    unguarded = []
    for i, line in enumerate(lines):
        for m in ADD_COMPONENT.finditer(line):
            short_name = m.group(1).rsplit(".", 1)[-1]
            window = "\n".join(lines[max(0, i - LOOKBACK_LINES):i + 1])
            guard = re.search(rf"GetComponent<(?:[\w.]+\.)?{re.escape(short_name)}>\(\)",
                               window)
            if not guard:
                unguarded.append((i + 1, short_name))
    assert unguarded == [], (
        f"AddComponent<T>() with no preceding GetComponent<T>() guard within "
        f"{LOOKBACK_LINES} lines (line, type): {unguarded}")


def test_game_boot_has_exactly_eight_add_component_call_sites():
    # A count pin so a guard that quietly stops being written (e.g. a ninth
    # AddComponent added and reviewed only for its own new guard, while the
    # window-based check above happens to still pass by accident) is still
    # visible as a number that moved. Measured today; see grep -n
    # "AddComponent<" game/Assets/Shell/GameBoot.cs.
    count = len(ADD_COMPONENT.findall(GAME_BOOT.read_text()))
    assert count == 8, f"expected 8 AddComponent<T>() call sites, found {count}"
