"""Level generator: layered-DAG piles, kind counts divisible by 3, all solvable.

The property test (test_generate.py) is the gate: every generated level must
be solver-verified. One unsolvable level rejects the milestone.
"""
from __future__ import annotations

import argparse
import json
import random
from pathlib import Path

from .schema import LevelDef, PileItem, save_level
from .solver import solve


def _slack_for_level(number: int) -> int:
    """Move slack from 8 (level 1) down to 2 (level 12)."""
    table = {1: 8, 2: 7, 3: 7, 4: 6, 5: 6, 6: 5,
             7: 4, 8: 4, 9: 3, 10: 3, 11: 2, 12: 2}
    return table.get(number, 2)


def generate_level(rng: random.Random, number: int = 1,
                   item_count: int = 30, room_id: str | None = None) -> LevelDef:
    """Build one level.

    Structure: kinds each appear a multiple of 3 times; items are laid out in
    layers where every item may be blocked only by items in strictly earlier
    layers — this makes cycles impossible by construction.
    """
    if item_count % 3 != 0:
        item_count += 3 - item_count % 3
    kind_count = max(1, round(item_count / 3 / 2))  # ~2 triples per kind
    kinds = [f"prop_{i:02d}" for i in range(kind_count)]

    # expand kinds into a multiset of exactly item_count entries
    pool: list[str] = []
    while len(pool) < item_count:
        for k in kinds:
            pool.extend([k] * 3)
            if len(pool) >= item_count:
                break
    rng.shuffle(pool)

    # assign to layers; layer sizes grow so later items can block earlier ones
    n_layers = max(2, item_count // 8)
    layers: list[list[str]] = [[] for _ in range(n_layers)]
    for idx, kind in enumerate(pool):
        # distribute round-robin-ish with jitter
        layer = idx % n_layers
        rng.shuffle(layers[layer])
        layers[layer].append(kind)

    pile: list[PileItem] = []
    next_id = 1
    ids_by_layer: list[list[int]] = [[] for _ in range(n_layers)]
    for li, layer in enumerate(layers):
        for kind in layer:
            pile.append(PileItem(id=next_id, kind=kind))
            ids_by_layer[li].append(next_id)
            next_id += 1

    # blockers: each item gets 0-2 blockers from strictly earlier layers
    for li in range(n_layers):
        below = [i for l in ids_by_layer[:li] for i in l]
        if not below:
            continue
        for pid in ids_by_layer[li]:
            n_blockers = rng.choices([0, 1, 2], weights=[0.5, 0.35, 0.15])[0]
            n_blockers = min(n_blockers, len(below))
            blockers = rng.sample(below, n_blockers)
            pile[pid - 1] = PileItem(pid, pile[pid - 1].kind, tuple(blockers))

    return LevelDef(
        number=number,
        room_id=room_id or f"room_{number:02d}",
        moves_limit=999,  # replaced after solving
        pile=tuple(pile),
    )


def generate_with_slack(rng: random.Random, number: int,
                        item_count: int) -> LevelDef | None:
    """Generate and set moves_limit = min_moves + slack. None if unsolvable."""
    level = generate_level(rng, number=number, item_count=item_count)
    sol = solve(level)
    if sol is None:
        return None
    slack = _slack_for_level(number)
    return LevelDef(number=level.number, room_id=level.room_id,
                    moves_limit=sol.move_count + slack, pile=level.pile)


def main() -> None:
    ap = argparse.ArgumentParser(description="Generate solver-verified levels")
    ap.add_argument("--count", type=int, default=100)
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--items", type=int, default=36)
    args = ap.parse_args()

    rng = random.Random(args.seed)
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    made = rejected = 0
    for i in range(args.count):
        number = i % 12 + 1  # spread across the difficulty curve
        level = generate_with_slack(rng, number=number, item_count=args.items)
        if level is None:
            rejected += 1
            continue
        save_level(level, str(out_dir / f"pool_{args.seed}_{i:03d}.json"))
        made += 1

    print(json.dumps({"generated": made, "rejected_unsolvable": rejected}))


if __name__ == "__main__":
    main()
