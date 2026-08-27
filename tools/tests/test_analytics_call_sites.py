"""Task 70-analytics/02: all nine events have a call site, and only those nine.

A missing call site is silent — the event simply never arrives, and the metric
built on it reads as a real zero. This walks the shell and view sources and
fails when one goes missing, which no C# test can do because those assemblies
are not compiled outside Unity.
"""
import re
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
CORE = ROOT / "game/Assets/Core/Analytics.cs"
CALLER_DIRS = [ROOT / "game/Assets/View", ROOT / "game/Assets/Shell"]

# name in Analytics.cs -> the helper a caller invokes
NINE = {
    "app:open": "AppOpen",
    "photo:screen_shown": "PhotoScreenShown",
    "photo:uploaded": "PhotoUploaded",
    "photo:rejected": "PhotoRejected",
    "booster:tap": "BoosterTap",
    "notification:allowed": "NotificationAllowed",
    "level_start": "LevelStart",
    "level_win": "LevelWin",
    "level_fail": "LevelFail",
}


def sources() -> list[tuple[Path, str]]:
    return [(p, p.read_text()) for d in CALLER_DIRS for p in d.rglob("*.cs")]


def calls_of(helper: str) -> list[str]:
    pattern = re.compile(rf"Analytics\.{helper}\s*\(")
    return [f"{path.name}" for path, text in sources() if pattern.search(text)]


# Declared, deliberately not called yet. The entry carries the reason, so a
# dormant event cannot be confused with a call site someone deleted by accident
# — which is the whole point of the test above it.
DORMANT = {
    "booster:tap":
        "the lose-screen offer was removed on 2026-08-27 (D4 revised): a tap on "
        "a free button measures 'do you want to not lose', not willingness to "
        "pay. The event comes back with the button, once there is a price.",
}


@pytest.mark.parametrize("event,helper", sorted(NINE.items()))
def test_every_event_has_a_call_site(event, helper):
    if event in DORMANT:
        assert not calls_of(helper), (
            f"{event} is listed as dormant but something calls "
            f"Analytics.{helper} — remove it from DORMANT")
        return
    assert calls_of(helper), f"{event}: nothing calls Analytics.{helper}"


def test_dormant_events_are_still_declared():
    # A dormant event that quietly leaves Analytics.cs is a hole in the funnel
    # nobody notices until the metric is wanted.
    declared = set(re.findall(r'public const string \w+ = "([^"]+)"', CORE.read_text()))
    for event in DORMANT:
        assert event in declared, f"{event} is dormant but no longer declared"


def test_the_surface_is_exactly_nine():
    # DECISIONS.md: no event names beyond these nine. A tenth is not a bigger
    # funnel, it is a funnel nobody agreed to read.
    declared = set(re.findall(r'public const string \w+ = "([^"]+)"', CORE.read_text()))
    assert declared == set(NINE)


def test_the_screen_event_fires_where_the_screen_is_built_not_after_a_photo():
    # Metric one counts players who REACHED the capture screen; firing this
    # after a photo has been handled would count players who picked one, which
    # is metric two. The two thresholds are 90% and 40%.
    capture = (ROOT / "game/Assets/View/CaptureScreen.cs").read_text()
    build = capture.index("public void Build(")
    handle = capture.index("public IEnumerator Handle(")
    shown = capture.index("Analytics.PhotoScreenShown()")
    assert build < shown < handle, "PhotoScreenShown moved out of Build()"


def test_progression_events_carry_a_level_number():
    # level_start/win/fail go to the dashboard's Progression section keyed by
    # level; a bare call would compile and land in the wrong place.
    for helper in ("LevelStart", "LevelWin", "LevelFail"):
        for _, text in sources():
            for call in re.findall(rf"Analytics\.{helper}\s*\(([^)]*)\)", text):
                assert call.strip(), f"Analytics.{helper}() called with no level"


def test_open_and_rejection_are_not_the_same_site():
    # app:open is the denominator for everything else, so it belongs at launch
    # and nowhere near the photo flow.
    assert "GameBoot.cs" in calls_of("AppOpen")
    assert "CaptureScreen.cs" not in calls_of("AppOpen")
