"""Task 2: is the difficulty on l30-l37 the good kind or the punishing kind?

Two measurements, both exact (not sampled) feasibility via the existing
solver, reusing tasks/30-levels-solver/analyze_solvability.solve_from_state:

A) Opening-move fraction: from the empty-shelf start, of all legal first
   moves, how many still admit a win (solve()-feasibility from the resulting
   state)? This is exhaustive over the true first branch, independent of any
   policy.

B) Trap depth along a REPRESENTATIVE path: analyze_solvability.py's own
   "zero unsafe branches" result was measured only along the solver's own
   found path, which the earlier report flagged as biased toward
   conservative choices (its heuristic prefers completing near-triples). This
   repeats that check along the `partial` policy's path instead - the policy
   meant to model what a real player, with real information, actually does
   - with a fixed seed so the path is a concrete, reproducible trajectory.
   Reports the first move index (if any) where a legal alternative choice
   leads to a state solve() can no longer win from.
"""
from __future__ import annotations
import glob, json, random, sys, time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import analyze_solvability as az  # solve_from_state, GENEROUS_CAP_S

from tools.solver.schema import load_level
from tools.solver.rules import new_state, Outcome, DEFAULT_CAPACITY
from tools.solver.measure import partial_policy
from tools.solver.solver import solve

LEVELS_DIR = "game/Assets/Resources/Levels"


def opening_move_fraction(level):
    st = new_state(level)
    avail = st.available()
    total = len(avail)
    safe = 0
    unsafe_ids = []
    for item in avail:
        trial = new_state(level)
        trial.take(item.id)
        if trial.over:
            ok = trial.outcome == Outcome.WIN
        else:
            ok = az.solve_from_state(level, trial.taken, trial.shelf,
                                     trial.triples_completed, time_cap_s=10.0)
            if ok is None:
                ok = None  # inconclusive
        if ok is True:
            safe += 1
        elif ok is False:
            unsafe_ids.append((item.id, item.kind))
    return total, safe, unsafe_ids


def partial_policy_path(level, seed):
    """One concrete playthrough under partial_policy; returns the move list
    and whether it won."""
    rng = random.Random(seed)
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


def trap_depth_along_path(level, moves, cap_s=10.0):
    """At each state along `moves` with >1 choice, check every alternative
    for solve()-feasibility. Returns (first_trap_move_index_or_None,
    per_step list of (avail, safe), inconclusive_count)."""
    st = new_state(level)
    per_step = []
    first_trap = None
    inconclusive = 0
    for step, mv in enumerate(moves, start=1):
        avail = st.available()
        if len(avail) > 1:
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
                    ok = az.solve_from_state(level, trial.taken, trial.shelf,
                                             trial.triples_completed, time_cap_s=cap_s)
                    if ok is None:
                        inconclusive += 1
                        ok = False
                if ok:
                    safe += 1
            per_step.append((step, len(avail), safe))
            if safe < len(avail) and first_trap is None:
                first_trap = step
        st.take(mv)
    return first_trap, per_step, inconclusive


def main():
    targets = [
        "l30_room11_pile0", "l31_room11_pile1", "l32_room11_pile2", "l33_room11_pile3",
        "l34_room12_pile0", "l35_room12_pile1", "l36_room12_pile2", "l37_room12_pile3",
    ]
    seed = 20260826  # same default seed as tools.solver.measure
    results = []
    for name in targets:
        path = f"{LEVELS_DIR}/{name}.json"
        lv = load_level(path)
        t0 = time.monotonic()
        total, safe, unsafe = opening_move_fraction(lv)
        moves, outcome = partial_policy_path(lv, seed)
        first_trap, per_step, inconclusive = trap_depth_along_path(lv, moves)
        dt = time.monotonic() - t0
        risky_steps = [(s, a, sf) for s, a, sf in per_step if sf < a]
        row = {
            "level": name,
            "opening_moves_total": total,
            "opening_moves_safe": safe,
            "opening_unsafe": unsafe,
            "partial_path_outcome": outcome.value,
            "partial_path_len": len(moves),
            "first_trap_move_on_partial_path": first_trap,
            "branch_steps_on_partial_path": len(per_step),
            "risky_steps_on_partial_path": len(risky_steps),
            "risky_step_positions": [s for s, a, sf in risky_steps],
            "inconclusive": inconclusive,
            "elapsed_s": round(dt, 2),
        }
        results.append(row)
        print(json.dumps(row))
    out = Path(str(Path(__file__).parent / "trap_results.json"))
    out.write_text(json.dumps(results, indent=2))
    print("wrote", out)


if __name__ == "__main__":
    main()
