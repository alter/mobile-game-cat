"""Across multiple partial-policy playthroughs (fixed seeds), how many
per-move forks contain at least one unsafe alternative (0 < safe < avail)?
Density = risky forks / branch-having moves, averaged over N seeds. Tests
whether l32/l34/l35 simply encounter more such forks per game than their
room-mates, given the per-fork danger (safe/avail ratio) looked uniform."""
from __future__ import annotations
import json, random, sys, time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import analyze_solvability as az

from tools.solver.schema import load_level
from tools.solver.rules import new_state, Outcome
from tools.solver.measure import partial_policy

LEVELS_DIR = "game/Assets/Resources/Levels"


def play_path(level, rng):
    st = new_state(level)
    moves = []
    while not st.over:
        avail = st.available()
        if not avail:
            break
        choices = [(i.id, i.kind) for i in avail]
        best = partial_policy(choices, tuple(st.shelf))
        mv = rng.choice(best)
        moves.append(mv)
        st.take(mv)
    return moves, st.outcome


def risky_forks_along(level, moves, cap_s=10.0):
    st = new_state(level)
    branch_steps = 0
    risky = 0
    for mv in moves:
        avail = st.available()
        if len(avail) > 1:
            branch_steps += 1
            safe = 0
            for item in avail:
                trial = new_state(level)
                trial.taken = set(st.taken)
                trial.shelf = list(st.shelf)
                trial.triples_completed = st.triples_completed
                trial.take(item.id)
                if trial.over:
                    ok = trial.outcome == Outcome.WIN
                else:
                    ok = bool(az.solve_from_state(level, trial.taken, trial.shelf,
                                                  trial.triples_completed, time_cap_s=cap_s))
                if ok:
                    safe += 1
            if safe < len(avail):
                risky += 1
        st.take(mv)
    return branch_steps, risky


def main():
    targets = [
        "l30_room11_pile0", "l31_room11_pile1", "l32_room11_pile2", "l33_room11_pile3",
        "l34_room12_pile0", "l35_room12_pile1", "l36_room12_pile2", "l37_room12_pile3",
    ]
    n_seeds = 15
    base_seed = 20260826
    results = []
    for name in targets:
        lv = load_level(f"{LEVELS_DIR}/{name}.json")
        t0 = time.monotonic()
        totals = []
        for s in range(n_seeds):
            rng = random.Random(base_seed + 1000 + s)
            moves, outcome = play_path(lv, rng)
            branch_steps, risky = risky_forks_along(lv, moves)
            totals.append((len(moves), branch_steps, risky, outcome.value))
        dt = time.monotonic() - t0
        avg_risky = sum(t[2] for t in totals) / n_seeds
        avg_branch = sum(t[1] for t in totals) / n_seeds
        row = {"level": name, "n_seeds": n_seeds,
               "avg_path_len": round(sum(t[0] for t in totals) / n_seeds, 1),
               "avg_branch_steps": round(avg_branch, 1),
               "avg_risky_forks": round(avg_risky, 2),
               "risky_density_pct": round(100 * avg_risky / avg_branch, 1) if avg_branch else None,
               "elapsed_s": round(dt, 1)}
        results.append(row)
        print(json.dumps(row))
    out = Path(str(Path(__file__).parent / "risky_density.json"))
    out.write_text(json.dumps(results, indent=2))
    print("wrote", out)


if __name__ == "__main__":
    main()
