#!/usr/bin/env bash
# Gate: Core must not reference the engine. Enforced by the build, not by
# good intentions (cat-shelter-tasks.md, M2 ARCH review).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET="$ROOT/game/Assets/Core"

hits=$(grep -rn "using UnityEngine\|UnityEngine\." "$TARGET" --include='*.cs' || true)
if [ -n "$hits" ]; then
  echo "ARCH VIOLATION — engine references under Assets/Core:"
  echo "$hits"
  exit 1
fi
echo "Core is engine-free: OK"
