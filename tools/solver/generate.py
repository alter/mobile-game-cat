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


def items_for_room(room_number: int) -> int:
    """Difficulty curve by pile size (reviews/2026-08-24-refactor-difficulty.md).

    The band is a property of the ROOM, not of the level's position in the run:
    36 items in rooms 1–4, 48 in rooms 5–8, 60 in rooms 9–12. Every pile in a
    room is the same size, so the 37-level plan (D2) stays three bands wide
    rather than thirty-seven steps long.
    """
    if room_number <= 4:
        return 36
    if room_number <= 8:
        return 48
    return 60


# Historical name from the one-level-per-room era, when the level number and
# the room number were the same thing.
_items_for_level = items_for_room


# Task 3.11: the one complication shipped in the MVP. A whole kind is locked
# until the player completes that many triples; introduced in room 9+ only,
# so a late room feels unlike an early one.
LOCKED_KIND_FROM_ROOM = 9
LOCKED_TRIPLE_THRESHOLD = 2

# The props that exist as art, in game/Assets/Resources/Art. Levels name these
# directly rather than prop_00..prop_09, so a level file says what is in the
# pile and the view loads the sprite by that name — one name instead of two and
# a lookup table between them.
#
# A pile uses ten of the thirty, drawn per level, so rooms differ from each
# other: with one fixed set of ten, every room in the house held the same
# clutter.
PROPS = (
    "prop_ball", "prop_board", "prop_book", "prop_bottle", "prop_box",
    "prop_candle", "prop_casket", "prop_clock", "prop_cloth", "prop_comb",
    "prop_crate", "prop_fork", "prop_frame", "prop_hanger", "prop_jar",
    "prop_keys", "prop_lamp", "prop_mirror", "prop_mitten", "prop_pillow",
    "prop_plate", "prop_rug", "prop_sack", "prop_scarf", "prop_scissors",
    "prop_spool", "prop_suitcase", "prop_tray", "prop_vase", "prop_yarn",
)


def generate_level(rng: random.Random, number: int = 1,
                   item_count: int = 30, kind_count: int | None = None,
                   room_id: str | None = None,
                   with_locked_kind: bool | None = None) -> LevelDef:
    """Build one level.

    Structure: kinds each appear a multiple of 3 times; items are laid out in
    layers where every item may be blocked only by items in strictly earlier
    layers — this makes cycles impossible by construction.

    kind_count is an explicit tuning lever (refactor decision 3); when omitted
    it defaults to the historical behaviour of ~2 triples per kind.

    with_locked_kind (task 3.11): lock one kind behind LOCKED_TRIPLE_THRESHOLD
    completed triples. Default: on for rooms >= LOCKED_KIND_FROM_ROOM, off before.
    """
    if item_count % 3 != 0:
        item_count += 3 - item_count % 3
    if kind_count is None:
        kind_count = max(1, round(item_count / 3 / 2))
    if kind_count > len(PROPS):
        raise ValueError(
            f"{kind_count} kinds asked for, only {len(PROPS)} props are drawn")
    kinds = rng.sample(PROPS, kind_count)

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
        layer = idx % n_layers
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

    # task 3.11: pick one kind and lock its three items behind N triples
    if with_locked_kind is None:
        with_locked_kind = number >= LOCKED_KIND_FROM_ROOM and bool(kinds)
    if with_locked_kind and kinds:
        locked_kind = rng.choice(kinds)
        unlock_at = min(LOCKED_TRIPLE_THRESHOLD, len(kinds) - 1)
        pile = [
            PileItem(i.id, i.kind, i.blocked_by,
                     i.locked_after_triples or (unlock_at if i.kind == locked_kind else 0))
            for i in pile
        ]

    return LevelDef(
        number=number,
        room_id=room_id or f"room_{number:02d}",
        pile_index=0,
        pile=tuple(pile),
    )


def generate_for_curve(rng: random.Random, number: int,
                       kind_count: int | None = None) -> LevelDef | None:
    """Generate at the curve's pile size for this level number. None if the
    solver cannot verify solvability."""
    level = generate_level(rng, number=number,
                           item_count=_items_for_level(number),
                           kind_count=kind_count)
    return level if solve(level) is not None else None


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
        level = generate_level(rng, number=number,
                               item_count=args.items if args.items else None)
        if solve(level) is None:
            rejected += 1
            continue
        save_level(level, str(out_dir / f"pool_{args.seed}_{i:03d}.json"))
        made += 1

    print(json.dumps({"generated": made, "rejected_unsolvable": rejected}))


if __name__ == "__main__":
    main()
