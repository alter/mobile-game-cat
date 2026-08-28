"""Applies the three measured fixes from HARD-LEVELS-2026-08-28.md to the
shipped level JSON. Originals are kept under
tasks/30-levels-solver/level-originals/ (copied there before this script
ever runs, by hand, so this script only ever overwrites the live file).

l34: drop item 14's `blocked_by` edge on item 9 (prop_pillow no longer waits
     on it). Measured 63.7% -> 84.3% partial win rate, no downside on any
     policy (hard_levels_edits.py).
l32: swap the `kind` of item 58 (prop_ball) and item 1 (prop_lamp), so
     prop_ball has 2 copies open from move 1 instead of 1. Measured
     66.0% -> 70.0% partial, 39.7% -> 64.0% oracle, no downside found for
     the single swap (stacking a second swap regressed, so only this one
     ships).
l35: lower prop_lamp's locked_after_triples from 2 to 1 (the locked kind
     unlocks one triple earlier). Measured 78.7% -> 83.3% partial, no
     downside on any policy - the largest of the two l35 edits tried.

Run once from repo root: PYTHONPATH=$(pwd) .venv/bin/python <this file>
"""
from __future__ import annotations
from dataclasses import replace

from tools.solver.schema import load_level, save_level, validate

LEVELS_DIR = "game/Assets/Resources/Levels"


def main():
    # ---- l34: drop blocked_by edge ----
    path34 = f"{LEVELS_DIR}/l34_room12_pile0.json"
    lv34 = load_level(path34)
    new_pile = tuple(
        replace(item, blocked_by=tuple(b for b in item.blocked_by if b != 9))
        if item.id == 14 else item
        for item in lv34.pile
    )
    lv34_new = replace(lv34, pile=new_pile)
    validate(lv34_new)
    assert lv34_new.by_id()[14].blocked_by == ()
    save_level(lv34_new, path34)
    print("l34: item 14 blocked_by [9] -> [] written to", path34)

    # ---- l32: swap kind labels of item 58 and item 1 ----
    path32 = f"{LEVELS_DIR}/l32_room11_pile2.json"
    lv32 = load_level(path32)
    by32 = lv32.by_id()
    k58, k1 = by32[58].kind, by32[1].kind
    assert k58 == "prop_ball" and k1 == "prop_lamp", (k58, k1)
    new_pile = tuple(
        replace(item, kind=k1) if item.id == 58 else
        replace(item, kind=k58) if item.id == 1 else item
        for item in lv32.pile
    )
    lv32_new = replace(lv32, pile=new_pile)
    validate(lv32_new)
    assert lv32_new.by_id()[58].kind == "prop_lamp"
    assert lv32_new.by_id()[1].kind == "prop_ball"
    save_level(lv32_new, path32)
    print("l32: item 58 <-> item 1 kind swap written to", path32)

    # ---- l35: prop_lamp locked_after_triples 2 -> 1 ----
    path35 = f"{LEVELS_DIR}/l35_room12_pile1.json"
    lv35 = load_level(path35)
    new_pile = tuple(
        replace(item, locked_after_triples=1) if item.kind == "prop_lamp" else item
        for item in lv35.pile
    )
    lv35_new = replace(lv35, pile=new_pile)
    validate(lv35_new)
    n_changed = sum(1 for i in lv35_new.pile if i.kind == "prop_lamp"
                    and i.locked_after_triples == 1)
    assert n_changed == 6, n_changed
    save_level(lv35_new, path35)
    print(f"l35: prop_lamp locked_after_triples 2 -> 1 on {n_changed} items, "
          f"written to", path35)


if __name__ == "__main__":
    main()
