"""Ship 12 levels: pick from a pool matching the slack curve, re-verify, write."""
from __future__ import annotations

import argparse
import json
import random
from pathlib import Path

from .schema import load_level, save_level
from .solver import solve
from .generate import _slack_for_level


def ship(pool_dir: str, out_dir: str, seed: int = 7) -> None:
    pool = sorted(Path(pool_dir).glob("pool_*.json"))
    if len(pool) < 12:
        raise SystemExit(f"pool has {len(pool)} levels, need >= 12")

    rng = random.Random(seed)
    chosen: dict[int, Path] = {}
    # one level per number 1..12; prefer size diversity within same slack
    by_slack: dict[int, list[Path]] = {}
    for p in pool:
        lvl = load_level(str(p))
        sol = solve(lvl)
        if sol is None:
            continue
        slack = _slack_for_level(min(len(chosen) + 1, 12))
        # bucket by current min-move count so we can pick varied sizes
        by_slack.setdefault(sol.move_count // 5, []).append(p)

    used: set[Path] = set()
    for number in range(1, 13):
        target_slack = _slack_for_level(number)
        candidates = []
        for p in pool:
            if p in used or p in chosen.values():
                continue
            lvl = load_level(str(p))
            sol = solve(lvl)
            if sol is None:
                continue
            candidates.append((abs((lvl.moves_limit - sol.move_count) - target_slack), p))
        if not candidates:
            raise SystemExit("not enough verified levels to ship")
        candidates.sort(key=lambda t: (t[0], rng.random()))
        chosen[number] = candidates[0][1]
        used.add(candidates[0][1])

    out_path = Path(out_dir)
    out_path.mkdir(parents=True, exist_ok=True)
    report = []
    for number in range(1, 13):
        src = chosen[number]
        lvl = load_level(str(src))
        sol = solve(lvl)
        assert sol is not None
        shipped = type(lvl)(number=number, room_id=f"room_{number:02d}",
                            moves_limit=lvl.moves_limit, pile=lvl.pile)
        dest = out_path / f"level_{number:02d}.json"
        save_level(shipped, str(dest))
        report.append({"level": number, "items": len(shipped.pile),
                       "min_moves": sol.move_count,
                       "moves_limit": shipped.moves_limit,
                       "slack": shipped.moves_limit - sol.move_count})

    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--pool", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=7)
    args = ap.parse_args()
    ship(args.pool, args.out, args.seed)
