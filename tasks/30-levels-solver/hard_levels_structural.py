"""Structural comparison of l30-l37 (room 11 and room 12), task 1.

Run from repo root: PYTHONPATH=$(pwd) .venv/bin/python <this file>
"""
from __future__ import annotations
import glob, json, statistics
from pathlib import Path
from tools.solver.schema import load_level
from tools.solver.rules import new_state
from tools.solver.solver import solve

LEVELS_DIR = "game/Assets/Resources/Levels"

def graph_depth_width(level):
    by_id = level.by_id()
    depth_memo = {}
    def depth(iid):
        if iid in depth_memo:
            return depth_memo[iid]
        item = by_id[iid]
        if not item.blocked_by:
            d = 0
        else:
            d = 1 + max(depth(b) for b in item.blocked_by)
        depth_memo[iid] = d
        return d
    depths = [depth(i.id) for i in level.pile]
    max_depth = max(depths)
    from collections import Counter
    width = Counter(depths)
    max_width = max(width.values())
    return max_depth, max_width, dict(sorted(width.items()))

def first_available_kinds(level):
    st = new_state(level)
    return sorted(set(i.kind for i in st.available()))

def kind_stats(level):
    from collections import Counter
    c = Counter(i.kind for i in level.pile)
    counts = list(c.values())
    return {
        "n_kinds": len(c),
        "min_count": min(counts),
        "max_count": max(counts),
        "stdev_count": round(statistics.pstdev(counts), 2),
        "counts": dict(sorted(c.items(), key=lambda kv: -kv[1])),
    }

def opening_kind_availability(level):
    """At the very first move (empty shelf), how many copies of each kind are
    already reachable? Kinds with exactly 1 reachable copy force a shelf slot
    that cannot be completed soon - the level commits you to a wait."""
    from collections import Counter
    st = new_state(level)
    c = Counter(i.kind for i in st.available())
    n_singleton_kinds = sum(1 for v in c.values() if v == 1)
    return n_singleton_kinds, dict(sorted(c.items(), key=lambda kv: kv[1]))


def first_forced_commitment(level, moves):
    """Walk a concrete move order (e.g. the solver's found path). At each
    state with >1 choice, a choice is 'lone' if no other currently-available
    item shares its kind (so taking it cannot itself progress toward a
    triple). Returns the 1-based move index of the first state where EVERY
    available choice is lone (whichever you pick, you commit blind) or None
    if that never happens along this path."""
    st = new_state(level)
    for step, mv in enumerate(moves, start=1):
        avail = st.available()
        if len(avail) > 1:
            from collections import Counter
            kc = Counter(i.kind for i in avail)
            if all(kc[i.kind] == 1 for i in avail):
                return step
        st.take(mv)
    return None


def locked_stats(level):
    locked = [i for i in level.pile if i.locked_after_triples > 0]
    thresholds = sorted(set(i.locked_after_triples for i in locked))
    kinds = sorted(set(i.kind for i in locked))
    return {"n_locked_items": len(locked), "thresholds": thresholds, "locked_kinds": kinds}

def main():
    files = sorted(f for f in glob.glob(f"{LEVELS_DIR}/*.json") if not f.endswith(".meta"))
    rows = []
    for f in files:
        name = Path(f).stem
        lv = load_level(f)
        depth, width, width_hist = graph_depth_width(lv)
        ks = kind_stats(lv)
        ls = locked_stats(lv)
        opening_kinds = first_available_kinds(lv)
        st0 = new_state(lv)
        opening_items = st0.available()
        n_singleton, opening_hist = opening_kind_availability(lv)
        sol = solve(lv)
        ffc = first_forced_commitment(lv, sol.moves) if sol else None
        row = {
            "level": name,
            "room": lv.room_id,
            "items": len(lv.pile),
            "n_kinds": ks["n_kinds"],
            "min_kind_count": ks["min_count"],
            "max_kind_count": ks["max_count"],
            "stdev_kind_count": ks["stdev_count"],
            "graph_max_depth": depth,
            "graph_max_width": width,
            "n_opening_items": len(opening_items),
            "n_opening_kinds": len(opening_kinds),
            "n_locked_items": ls["n_locked_items"],
            "locked_thresholds": ls["thresholds"],
            "n_singleton_kinds_at_open": n_singleton,
            "first_forced_commitment_move": ffc,
        }
        rows.append(row)
    print(json.dumps(rows, indent=2))

if __name__ == "__main__":
    main()
