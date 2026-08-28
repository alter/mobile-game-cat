"""Task 3: minimal, concrete edits for l32/l34/l35, measured before/after
with the exact methodology of the original report (measure.py's play(),
300 games/policy, seed 20260826) - applied only in memory, never written to
the shipped JSON."""
from __future__ import annotations
import json, random
from dataclasses import replace
from pathlib import Path

from tools.solver.schema import load_level, LevelDef, PileItem, validate
from tools.solver.rules import Outcome
from tools.solver.measure import play

LEVELS_DIR = "game/Assets/Resources/Levels"
SEED = 20260826
REPEATS = 300
POLICIES = ("shelf_only", "partial", "oracle")


def measure(level: LevelDef, repeats=REPEATS, seed=SEED):
    row = {}
    for policy in POLICIES:
        rng = random.Random(seed)
        wins = sum(1 for _ in range(repeats)
                  if play(level, policy, rng) is Outcome.WIN)
        row[policy] = round(100 * wins / repeats, 1)
    return row


def edit_item(level: LevelDef, item_id: int, **changes) -> LevelDef:
    new_pile = tuple(
        replace(item, **changes) if item.id == item_id else item
        for item in level.pile
    )
    new_level = replace(level, pile=new_pile)
    validate(new_level)  # still a legal level: ids, acyclic, counts % 3
    return new_level


def edit_kind_for_all(level: LevelDef, kind: str, **changes) -> LevelDef:
    new_pile = tuple(
        replace(item, **changes) if item.kind == kind else item
        for item in level.pile
    )
    new_level = replace(level, pile=new_pile)
    validate(new_level)
    return new_level


def drop_one_blocker(level: LevelDef, item_id: int, blocker_to_drop: int) -> LevelDef:
    item = level.by_id()[item_id]
    assert blocker_to_drop in item.blocked_by
    new_bb = tuple(b for b in item.blocked_by if b != blocker_to_drop)
    return edit_item(level, item_id, blocked_by=new_bb)


def main():
    results = []

    # ---- l32: prop_ball is the dominant "unsafe pick" kind (57 of the top-10
    # unsafe-item hits across 25 sampled seeds), because 4 of its 6 copies
    # each depend on a DIFFERENT blocker (items 2, 10x2, 11, 14, 15&13), so
    # they surface in scattered dribbles instead of clumps.
    l32 = load_level(f"{LEVELS_DIR}/l32_room11_pile2.json")
    base32 = measure(l32)
    results.append({"level": "l32", "edit": "baseline", **base32})

    e32_a = drop_one_blocker(l32, 58, 14)  # prop_ball id58: [14] -> []
    results.append({"level": "l32", "edit": "drop blocked_by 14 from item 58 (prop_ball)",
                    **measure(e32_a)})

    e32_b = edit_kind_for_all(l32, "prop_clock", locked_after_triples=1)
    results.append({"level": "l32", "edit": "prop_clock locked_after_triples 2->1",
                    **measure(e32_b)})

    e32_c = drop_one_blocker(l32, 22, 11)  # prop_ball id22: [11,2] -> [2]
    results.append({"level": "l32", "edit": "drop blocked_by 11 from item 22 (prop_ball)",
                    **measure(e32_c)})

    # kind swap: give prop_ball item 59 (already open) trade places with an
    # already-open item of a "safe" kind, and vice versa - test whether
    # relabeling (not just unblocking) changes anything.
    l32_by = l32.by_id()
    # find an open-from-start item of a different kind to swap with item 58
    donor = next(i for i in l32.pile if i.blocked_by == () and i.kind != "prop_ball")
    new_pile = tuple(
        replace(item, kind="prop_ball") if item.id == donor.id else
        replace(item, kind=donor.kind) if item.id == 58 else item
        for item in l32.pile
    )
    e32_d = replace(l32, pile=new_pile)
    validate(e32_d)
    results.append({"level": "l32",
                    "edit": f"swap kind of item 58 (prop_ball) <-> item {donor.id} ({donor.kind})",
                    **measure(e32_d)})

    # does stacking the same idea help further? swap a SECOND prop_ball copy
    # (item 24, blocked_by [10]) with a second already-open donor of yet
    # another kind, on top of e32_d's swap.
    donor2 = next(i for i in l32.pile if i.blocked_by == () and i.kind != "prop_ball"
                 and i.id not in (donor.id, 58, 24))
    new_pile2 = tuple(
        replace(item, kind="prop_ball") if item.id == donor2.id else
        replace(item, kind=donor2.kind) if item.id == 24 else item
        for item in e32_d.pile
    )
    e32_e = replace(e32_d, pile=new_pile2)
    validate(e32_e)
    results.append({"level": "l32",
                    "edit": (f"stack: also swap item 24 (prop_ball) <-> "
                            f"item {donor2.id} ({donor2.kind}), on top of the item-58 swap"),
                    **measure(e32_e)})

    # ---- l34: prop_pillow dominant unsafe kind; item 14 depends on item 9,
    # while 4 of its 6 copies are already open at the start.
    l34 = load_level(f"{LEVELS_DIR}/l34_room12_pile0.json")
    results.append({"level": "l34", "edit": "baseline", **measure(l34)})

    e34_a = drop_one_blocker(l34, 14, 9)  # prop_pillow id14: [9] -> []
    results.append({"level": "l34", "edit": "drop blocked_by 9 from item 14 (prop_pillow)",
                    **measure(e34_a)})

    e34_b = edit_kind_for_all(l34, "prop_crate", locked_after_triples=1)
    results.append({"level": "l34", "edit": "prop_crate locked_after_triples 2->1",
                    **measure(e34_b)})

    # ---- l35: prop_ball dominant unsafe kind; item 60 is the most gated
    # copy, blocked by two items (52, 46).
    l35 = load_level(f"{LEVELS_DIR}/l35_room12_pile1.json")
    results.append({"level": "l35", "edit": "baseline", **measure(l35)})

    e35_a = drop_one_blocker(l35, 60, 52)  # prop_ball id60: [52,46] -> [46]
    results.append({"level": "l35", "edit": "drop blocked_by 52 from item 60 (prop_ball)",
                    **measure(e35_a)})

    e35_b = edit_kind_for_all(l35, "prop_lamp", locked_after_triples=1)
    results.append({"level": "l35", "edit": "prop_lamp locked_after_triples 2->1",
                    **measure(e35_b)})

    for r in results:
        print(json.dumps(r))
    Path(str(Path(__file__).parent / "edits_results.json")).write_text(
        json.dumps(results, indent=2))


if __name__ == "__main__":
    main()
