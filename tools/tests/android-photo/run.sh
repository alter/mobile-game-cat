#!/bin/sh
# tools/tests/android-photo — the eight EXIF orientations, checked by exhaustion.
#
#   ./run.sh
#
# Needs a JDK and nothing else: no device, no emulator, no Gradle, no Unity.
# Runs in about a second, so there is no excuse for skipping it.
#
# WHAT IT GUARDS. CatVision turns a photograph the right way up before it looks
# at it and reports boxes in THAT space; CatPhoto.prepare decodes the file as
# written. For EXIF 6 and 8 — a phone held upright, which is most photographs —
# those two spaces have their axes swapped, so a box carried across without
# conversion crops somewhere else entirely. CatPhoto.intoFileSpace is the
# conversion, and this is its proof.
#
# WHY NOT AN INSTRUMENTATION TEST. tools/tests/android-vision needs an emulator
# with Play services, takes minutes, and would exercise this through two ML
# models whose answers move on their own — a run of it on rotated fixtures
# genuinely did come back with a DIFFERENT verdict for one file, which says
# nothing about the arithmetic. The arithmetic is integers, so it is checked as
# integers, and the end-to-end run is recorded in the commit rather than
# pretended to be repeatable.
#
# WHAT IT DOES NOT DO. It checks a COPY of the eight cases, not the ones in
# CatPhoto.java — the file is an Android library and cannot be compiled without
# the SDK. If CatPhoto.point is edited, edit `back` here to match, or this
# proves something about code nobody runs.
set -eu
here=$(cd "$(dirname "$0")" && pwd)
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
javac -d "$out" "$here/RotCheck.java"
java -cp "$out" RotCheck
