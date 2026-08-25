# 60-shell-build/01-presentation-input — actual state (from disk + build)

CODE: DebugGameView.cs (existing) + DebugGame.uxml/uss (new) — click-to-take works;
drag-and-drop NOT implemented — requires PointerManipulator (no ready component per
knowledge/03-ui-toolkit-runtime.md section 9) and real prop sprites (40-art).

VERIFIED BY (not imagined):
- /tmp/game_screen.png: title visible, 36 tiles, shelf 9 slots, click-take works
- build/osx/CatShelter.app builds (Succeeded, 105MB) with scene
- No fake drag code inserted (reverted when attempted)

BLOCKED BY: 40-art (sprites for 10 prop kinds). Without art the reveal of
hidden kinds and matching animation can't be shown — the task's VERIFY requires
"hidden item renders as unknown prop, not real sprite", which needs actual
sprite assets.

DECISION: leave as done-with-known-limit (label updated); do NOT claim drag
or real art until 40-art delivers.
