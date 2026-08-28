"""Why does `oracle` sometimes score lower than `partial` on l28/l30/l32?

Two tests:
A) Multi-seed robustness: is oracle<partial on these levels a stable effect,
   or does it flip sign across seeds (consistent with the two policies'
   random-tie-break streams simply desyncing once their tie-set sizes
   differ, per move.choice consuming a variable number of random bits
   depending on len(choices))?
B) Direct fork comparison: at states where the two policies' tie sets
   actually differ (oracle's dig_cost broke a tie partial left standing),
   does oracle's specific pick resolve to a safe (solve()-feasible) state
   more or less often than partial's full tied set?
"""
from __future__ import annotations
import json, random, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import analyze_solvability as az

from tools.solver.schema import load_level
from tools.solver.rules import new_state, Outcome
from tools.solver.measure import partial_policy, oracle_policy, _dig_cost, play

LEVELS_DIR = "game/Assets/Resources/Levels"


def multiseed_compare(levels, n_seeds=20, repeats=100, base_seed=1000):
    rows = []
    for name in levels:
        lv = load_level(f"{LEVELS_DIR}/{name}.json")
        diffs = []
        for s in range(n_seeds):
            seed = base_seed + s
            p_wins = sum(1 for _ in range(repeats)
                        if play(lv, "partial", random.Random(seed)) is Outcome.WIN)
            o_wins = sum(1 for _ in range(repeats)
                        if play(lv, "oracle", random.Random(seed)) is Outcome.WIN)
            diffs.append(o_wins - p_wins)  # positive = oracle better
        rows.append({
            "level": name, "n_seeds": n_seeds, "repeats_per_seed": repeats,
            "mean_oracle_minus_partial_wins": round(sum(diffs) / len(diffs), 2),
            "min": min(diffs), "max": max(diffs),
            "n_seeds_oracle_worse": sum(1 for d in diffs if d < 0),
            "n_seeds_oracle_better": sum(1 for d in diffs if d > 0),
            "n_seeds_tied": sum(1 for d in diffs if d == 0),
        })
        print(json.dumps(rows[-1]))
    return rows


def fork_divergence(level_name, n_seeds=30, base_seed=2000):
    """Play with partial_policy; at every state where oracle_policy's choice
    set is a STRICT subset of partial_policy's (a real tie-break divergence),
    check whether oracle's narrower pick is safe and whether it excludes any
    safe options partial would have included."""
    lv = load_level(f"{LEVELS_DIR}/{level_name}.json")
    n_divergent = 0
    oracle_excludes_a_safe_choice = 0
    oracle_pick_unsafe = 0
    examples = []
    for s in range(n_seeds):
        rng = random.Random(base_seed + s)
        st = new_state(lv)
        while not st.over:
            avail = st.available()
            if not avail:
                break
            choices = [(i.id, i.kind) for i in avail]
            p_best = set(partial_policy(choices, tuple(st.shelf)))
            o_best = set(oracle_policy(choices, tuple(st.shelf), _dig_cost(st)))
            if o_best != p_best and len(p_best) > 1:
                n_divergent += 1
                # safety of every id in p_best vs o_best
                safe_ids = set()
                for iid in p_best:
                    trial = new_state(lv)
                    trial.taken, trial.shelf = set(st.taken), list(st.shelf)
                    trial.triples_completed = st.triples_completed
                    trial.take(iid)
                    ok = (trial.outcome == Outcome.WIN) if trial.over else bool(
                        az.solve_from_state(lv, trial.taken, trial.shelf,
                                            trial.triples_completed, time_cap_s=10.0))
                    if ok:
                        safe_ids.add(iid)
                if safe_ids - o_best:  # a safe id existed that oracle excluded
                    oracle_excludes_a_safe_choice += 1
                if not (o_best & safe_ids):  # oracle's entire narrowed set is unsafe
                    oracle_pick_unsafe += 1
                    if len(examples) < 5:
                        examples.append({
                            "seed": base_seed + s, "partial_choices": list(p_best),
                            "oracle_choices": list(o_best), "safe_choices": list(safe_ids),
                        })
            rng_choices = list(partial_policy(choices, tuple(st.shelf)))
            mv = rng.choice(rng_choices)
            st.take(mv)
    return {
        "level": level_name, "n_seeds": n_seeds,
        "n_divergent_forks_seen": n_divergent,
        "oracle_narrowed_out_a_safe_choice": oracle_excludes_a_safe_choice,
        "oracle_narrowed_set_entirely_unsafe": oracle_pick_unsafe,
        "examples": examples,
    }


def main():
    print("--- A: multi-seed robustness ---")
    a_rows = multiseed_compare(["l28_room10_pile2", "l30_room11_pile0",
                                "l32_room11_pile2", "l33_room11_pile3"])
    print("--- B: fork divergence, l32 ---")
    b32 = fork_divergence("l32_room11_pile2")
    print(json.dumps(b32, indent=2))
    print("--- B: fork divergence, l30 ---")
    b30 = fork_divergence("l30_room11_pile0")
    print(json.dumps(b30, indent=2))
    out = Path(str(Path(__file__).parent / "oracle_anomaly.json"))
    out.write_text(json.dumps({"A": a_rows, "B_l32": b32, "B_l30": b30}, indent=2))
    print("wrote", out)


if __name__ == "__main__":
    main()
