"""Ship 12 levels: pick from a pool matching the pile-size curve, verify, write."""
from __future__ import annotations

import argparse
import json
import random
from pathlib import Path

from .schema import load_level, save_level
from .solver import solve
from .generate import _items_for_level


def ship(pool_dir: str, out_dir: str, seed: int = 7) -> None:
    pool = sorted(Path(pool_dir).glob("pool_*.json"))
    if len(pool) < 12:
        raise SystemExit(f"pool has {len(pool)} levels, need >= 12")

    rng = random.Random(seed)
    used: set[Path] = set()
    chosen: dict[int, Path] = {}

    for number in range(1, 13):
        target_items = _items_for_level(number)
        candidates = []
        for p in pool:
            if p in used:
                continue
            lvl = load_level(str(p))
            if len(lvl.pile) != target_items:
                continue
            if solve(lvl) is None:
                continue
            candidates.append(p)
        if not candidates:
            raise SystemExit(
                f"no verified {target_items}-item level for level {number}")
        pick = rng.choice(candidates[:10])
        chosen[number] = pick
        used.add(pick)

    out_path = Path(out_dir)
    out_path.mkdir(parents=True, exist_ok=True)
    report = []
    for number in range(1, 13):
        src = chosen[number]
        lvl = load_level(str(src))
        sol = solve(lvl)
        assert sol is not None
        shipped = type(lvl)(number=number, room_id=f"room_{number:02d}",
                            pile=lvl.pile)
        dest = out_path / f"level_{number:02d}.json"
        save_level(shipped, str(dest))
        report.append({"level": number, "items": len(shipped.pile),
                       "min_moves": sol.move_count})

    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--pool", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=7)
    args = ap.parse_args()
    ship(args.pool, args.out, args.seed)
