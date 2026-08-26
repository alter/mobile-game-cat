#!/usr/bin/env python3
"""Regenerate worker/src/schema.ts from tools/traits/schema.json.

The schema is data and lives in one place. TypeScript cannot import a JSON file
into a Worker bundle without extra build steps, so it is copied — and copied by
a script, so nobody edits the copy and wonders later why the Worker accepts a
value the game refuses.
"""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
schema = json.loads((root / "tools/traits/schema.json").read_text())
out = root / "worker/src/schema.ts"
out.write_text(
    "/**\n * The response contract, generated from tools/traits/schema.json.\n"
    " *\n * Do not edit by hand: that file is the single definition shared with the\n"
    " * game and the Python checks, and a second copy would drift. Regenerate with\n"
    " *   python worker/sync-schema.py\n */\n"
    "export const TRAITS_SCHEMA = " + json.dumps(schema, indent=2) + " as const;\n")
print(f"wrote {out.relative_to(root)}")
