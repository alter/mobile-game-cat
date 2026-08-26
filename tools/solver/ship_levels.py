"""Ship the 37-level plan: generate, verify with the solver, write to disk.

One file per (room, pile) pair of `pacing.PILES_PER_ROOM`, named
`l<seq>_room<room>_pile<index>.json` — the names `View/LevelAssets.cs` builds
when it loads levels in the player.

This replaces a version that shipped twelve `level_NN.json` files, one per
room, and so could not produce what the game actually reads. The 37 levels in
`game/Assets/Resources/Levels/` predate it and were not reproducible from the
repository at all.
"""
from __future__ import annotations

import argparse
import json
import random
from pathlib import Path

from .generate import generate_level, items_for_room
from .pacing import level_map
from .schema import LevelDef, save_level
from .solver import solve

MAX_ATTEMPTS = 50


def ship(out_dir: str, seed: int = 7, attempts: int = MAX_ATTEMPTS) -> list[dict]:
    """Write the full plan into out_dir; returns one report row per level."""
    rng = random.Random(seed)
    out_path = Path(out_dir)
    out_path.mkdir(parents=True, exist_ok=True)

    report: list[dict] = []
    for seq, (room, pile_index) in enumerate(level_map(), start=1):
        items = items_for_room(room)
        level = solution = None
        for _ in range(attempts):
            # `number=room` on purpose: pile size and the locked kind are both
            # keyed to the room (generate.LOCKED_KIND_FROM_ROOM), not to the
            # level's place in the run.
            candidate = generate_level(rng, number=room, item_count=items,
                                       room_id=f"room_{room:02d}")
            solution = solve(candidate)
            if solution is not None:
                level = candidate
                break
        if level is None or solution is None:
            raise SystemExit(
                f"no solvable {items}-item level for room {room} "
                f"pile {pile_index} after {attempts} attempts")

        shipped = LevelDef(number=seq, room_id=f"room_{room:02d}",
                           pile_index=pile_index, pile=level.pile)
        name = f"l{seq:02d}_room{room:02d}_pile{pile_index}.json"
        save_level(shipped, str(out_path / name))
        report.append({
            "level": seq,
            "room": room,
            "pile_index": pile_index,
            "items": len(shipped.pile),
            "locked_items": sum(1 for i in shipped.pile if i.locked_after_triples),
            "min_moves": solution.move_count,
            "file": name,
        })
    return report


def main() -> None:
    ap = argparse.ArgumentParser(description="Ship the 37-level plan")
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--attempts", type=int, default=MAX_ATTEMPTS)
    args = ap.parse_args()
    print(json.dumps(ship(args.out, args.seed, args.attempts), indent=2))


if __name__ == "__main__":
    main()
