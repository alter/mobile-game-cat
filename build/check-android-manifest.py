#!/usr/bin/env python3
"""Task 90-android/02-build-pipeline: a build that succeeds is not the same
claim as a build that carries what it was supposed to. On 2026-08-27 the
Android build reported `result=Succeeded errors=0` while missing the active
build target switch, and the APK it produced carried the notification Java
classes with neither the permission nor the receiver those classes need in
AndroidManifest.xml to ever run. No existing check would have caught that —
"the build succeeded" and "the file exists" are both blind to it by
construction.

This dumps the built APK's manifest with aapt2 and asserts it carries every
manifest contribution a currently-integrated Android package is responsible
for. What packages are "currently integrated" is read from
game/Packages/manifest.json — the honest source, so this stops needing an
edit the day a package is only added there. What each package is expected to
inject cannot be derived from manifest.json (it only names versions, not
Android manifest entries), so that half is a table below, PACKAGE_CONTRIBUTIONS.

A package present in manifest.json that this script does not recognise is a
hard failure, not a silent skip: an unrecognised entry is exactly the
situation this check exists to catch. Two categories are excluded by
construction, not silence, because they cannot inject Android manifest
content at all: `com.unity.modules.*` are engine feature stubs compiled
directly into UnityEngine itself, and EDITOR_ONLY_PACKAGES never ship inside
a player build to begin with.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_MANIFEST_JSON = ROOT / "game/Packages/manifest.json"
DEFAULT_AAPT2 = (
    "/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer"
    "/SDK/build-tools/36.0.0/aapt2"
)

# Package id -> the manifest strings its Android integration is responsible
# for (a permission name, a fully-qualified receiver/service class name, ...).
# An empty list means "known, confirmed to inject nothing into the manifest" -
# still a declared decision, not an absence.
PACKAGE_CONTRIBUTIONS: dict[str, list[str]] = {
    "com.gameanalytics.sdk": [
        "android.permission.INTERNET",
        "android.permission.ACCESS_NETWORK_STATE",
    ],
    "com.unity.mobile.notifications": [
        "android.permission.POST_NOTIFICATIONS",
        "com.unity.androidnotifications.UnityNotificationManager",
    ],
    "com.unity.2d.sprite": [],
    "com.unity.2d.tilemap": [],
    "com.unity.ai.navigation": [],
    "com.unity.nuget.newtonsoft-json": [],
    "com.unity.timeline": [],
    "com.unity.ugui": [],
    "com.unity.xr.legacyinputhelpers": [],
}

# Never compiled into a player build in the first place, so they cannot
# contribute anything to the manifest a device ever sees.
EDITOR_ONLY_PACKAGES = {
    "com.unity.collab-proxy",
    "com.unity.ide.rider",
    "com.unity.ide.visualstudio",
    "com.unity.multiplayer.center",
    "com.unity.test-framework",
}


def is_engine_module(package_id: str) -> bool:
    # Feature toggles bundled with the editor itself (androidjni, physics,
    # ui, ...), not separate Android libraries — never manifest content.
    return package_id.startswith("com.unity.modules.")


def integrated_packages(manifest_json: Path) -> list[str]:
    data = json.loads(manifest_json.read_text())
    return sorted(data.get("dependencies", {}).keys())


def expected_strings(manifest_json: Path) -> list[tuple[str, str]]:
    """[(package_id, expected_manifest_string), ...] — fails loudly, not
    silently, on any package this table does not recognise."""
    pairs: list[tuple[str, str]] = []
    unknown: list[str] = []
    for package_id in integrated_packages(manifest_json):
        if is_engine_module(package_id) or package_id in EDITOR_ONLY_PACKAGES:
            continue
        if package_id not in PACKAGE_CONTRIBUTIONS:
            unknown.append(package_id)
            continue
        for entry in PACKAGE_CONTRIBUTIONS[package_id]:
            pairs.append((package_id, entry))
    if unknown:
        sys.exit(
            "check-android-manifest: game/Packages/manifest.json names "
            f"{len(unknown)} package(s) this check does not recognise: "
            f"{', '.join(unknown)}. Add each to PACKAGE_CONTRIBUTIONS in "
            "build/check-android-manifest.py (an empty list if it injects "
            "nothing) or EDITOR_ONLY_PACKAGES if it never ships in a player "
            "build. An unrecognised package is refused on purpose — see the "
            "module docstring.")
    return pairs


def dump_manifest_text(aapt2: str, apk: Path) -> str:
    badging = subprocess.run(
        [aapt2, "dump", "badging", str(apk)],
        capture_output=True, text=True)
    if badging.returncode != 0:
        sys.exit(f"check-android-manifest: aapt2 dump badging failed on "
                  f"{apk}: {badging.stderr.strip()}")
    xmltree = subprocess.run(
        [aapt2, "dump", "xmltree", str(apk), "--file", "AndroidManifest.xml"],
        capture_output=True, text=True)
    if xmltree.returncode != 0:
        sys.exit(f"check-android-manifest: aapt2 dump xmltree failed on "
                  f"{apk}: {xmltree.stderr.strip()}")
    return badging.stdout + "\n" + xmltree.stdout


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--apk", required=True, type=Path,
                     help="built .apk to inspect")
    ap.add_argument("--aapt2", default=DEFAULT_AAPT2,
                     help="path to the aapt2 binary")
    ap.add_argument("--manifest-json", default=DEFAULT_MANIFEST_JSON, type=Path,
                     help="game/Packages/manifest.json")
    args = ap.parse_args()

    if not args.apk.is_file():
        sys.exit(f"check-android-manifest: no such file: {args.apk}")

    pairs = expected_strings(args.manifest_json)
    text = dump_manifest_text(args.aapt2, args.apk)

    missing = [(pkg, entry) for pkg, entry in pairs if entry not in text]
    if missing:
        print(f"check-android-manifest: {args.apk} is missing "
              f"{len(missing)} of {len(pairs)} expected manifest "
              "contribution(s):", file=sys.stderr)
        for pkg, entry in missing:
            print(f"  {pkg}: {entry!r} not found in the built manifest",
                  file=sys.stderr)
        print("This is exactly the 2026-08-27 defect's shape: a package is "
              "integrated (game/Packages/manifest.json) but its manifest "
              "entries never reached the built APK — most likely because "
              "the active Unity build target was not Android when the "
              "build ran (see BuildScript.UseTarget).", file=sys.stderr)
        return 1

    print(f"check-android-manifest: {args.apk} carries all "
          f"{len(pairs)} expected manifest contribution(s) from "
          f"{len({p for p, _ in pairs})} package(s). OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
