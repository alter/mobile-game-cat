"""Refines trap_depth_stats: for each sampled losing game, find the exact
move where the ACTUAL choice taken (not just some alternative) first turns
a still-winnable state into an unwinnable one. That is the real "wrong tap".
Also reports whether, at that same state, a safe alternative existed
(avoidable mistake) or not (already doomed no matter what)."""
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


def is_feasible(level, state):
    if state.over:
        return state.outcome == Outcome.WIN
    ok = az.solve_from_state(level, state.taken, state.shelf,
                             state.triples_completed, time_cap_s=10.0)
    return bool(ok)


def first_real_mistake(level, moves):
    """Walk the actual path. At each move, before taking it, the state must
    be feasible (state is derived from only-safe choices so far - invariant
    checked). Return the first step where taking the ACTUAL move `mv` lands
    in an infeasible state, plus whether an alternative at that fork was
    safe."""
    st = new_state(level)
    for step, mv in enumerate(moves, start=1):
        avail = st.available()
        trial = new_state(level)
        trial.taken = set(st.taken)
        trial.shelf = list(st.shelf)
        trial.triples_completed = st.triples_completed
        trial.take(mv)
        taken_ok = is_feasible(level, trial)
        if not taken_ok:
            # was there a safe alternative at this fork?
            had_alt = False
            for item in avail:
                if item.id == mv:
                    continue
                alt = new_state(level)
                alt.taken = set(st.taken)
                alt.shelf = list(st.shelf)
                alt.triples_completed = st.triples_completed
                alt.take(item.id)
                if is_feasible(level, alt):
                    had_alt = True
                    break
            return step, len(avail), had_alt
        st.take(mv)
    return None, None, None


def main():
    targets = [
        "l30_room11_pile0", "l31_room11_pile1", "l32_room11_pile2", "l33_room11_pile3",
        "l34_room12_pile0", "l35_room12_pile1", "l36_room12_pile2", "l37_room12_pile3",
    ]
    max_trials = 400
    max_losses_to_inspect = 8
    base_seed = 20260826
    results = []
    for name in targets:
        lv = load_level(f"{LEVELS_DIR}/{name}.json")
        t0 = time.monotonic()
        n_win = n_loss = 0
        mistakes = []
        for trial in range(max_trials):
            rng = random.Random(base_seed + trial)
            moves, outcome = play_path(lv, rng)
            if outcome == Outcome.WIN:
                n_win += 1
                continue
            n_loss += 1
            if len(mistakes) < max_losses_to_inspect:
                step, avail, had_alt = first_real_mistake(lv, moves)
                mistakes.append({
                    "trial": trial, "path_len": len(moves),
                    "mistake_move": step, "avail_at_mistake": avail,
                    "avoidable": had_alt,
                })
        dt = time.monotonic() - t0
        row = {"level": name, "trials": max_trials, "wins": n_win, "losses": n_loss,
               "win_rate_pct": round(100 * n_win / max_trials, 1),
               "mistakes": mistakes, "elapsed_s": round(dt, 1)}
        results.append(row)
        print(json.dumps(row))
    out = Path(str(Path(__file__).parent / "first_mistake.json"))
    out.write_text(json.dumps(results, indent=2))
    print("wrote", out)


if __name__ == "__main__":
    main()
