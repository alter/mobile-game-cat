"""Per-level solvability + tightness analysis for the 37 shipped levels.

Run from repo root: .venv/bin/python <this file>

For each level:
  1. Solvability verdict via the existing DFS solver (tools/solver/solver.py),
     with a generous time cap. Distinguishes "proven unsolvable" (search
     exhausted all reachable states) from "timeout" (inconclusive) by calling
     the internal _Search directly and watching for TimeoutError, since
     solve() collapses both into None.
  2. The found solution is replayed through the rules mirror (rules.replay)
     to confirm it actually reaches Outcome.WIN — a solver bug would show up
     here as a "solved" level that doesn't actually replay to a win.
  3. greedy_wins() from ship_levels.py: does the actual sensible-forward-play
     policy (no backtracking, prefers kind already 2-of-3 on shelf, ties by
     id) used at ship time also win? A level can be solve()-solvable (some
     clever order exists) yet not greedy-winnable (a forward player without
     backtracking gets stuck) - worth telling apart.
  4. Peak shelf occupancy along the solver's found win path: exact, from one
     concrete winning line. Lower bound on how close to a 9-slot jam a
     reasonable strategy comes; not necessarily the globally tightest line.
  5. Exact "safe-move fraction": at every state along the solver's found win
     path, how many of the currently-available choices (not just the one
     taken) themselves lead to a state from which solve() still finds a win?
     This is computed exactly (not sampled) by re-running solve() from each
     alternative branch, which is affordable here because solves on these
     levels take low milliseconds (measured below).
"""
from __future__ import annotations

import glob
import json
import time
from pathlib import Path

from tools.solver.schema import load_level, LevelDef
from tools.solver.solver import solve, _Search
from tools.solver.rules import Outcome, new_state, replay, DEFAULT_CAPACITY
from tools.solver.ship_levels import greedy_wins

LEVELS_DIR = "game/Assets/Resources/Levels"
GENEROUS_CAP_S = 30.0
PROOF_CAP_S = 120.0  # only used if the generous cap times out


def solve_with_reason(level: LevelDef, time_cap_s: float):
    """('solved', Solution) | ('proven_unsolvable', None) | ('timeout', None)."""
    search = _Search(level, deadline=time.monotonic() + time_cap_s,
                      shelf_capacity=DEFAULT_CAPACITY)
    t0 = time.monotonic()
    try:
        result = search.run()
    except TimeoutError:
        return "timeout", None, time.monotonic() - t0
    dt = time.monotonic() - t0
    if result is not None:
        return "solved", result, dt
    return "proven_unsolvable", None, dt


def peak_occupancy_and_dead_end(level: LevelDef, moves: tuple[int, ...]):
    """Replay moves; return (peak_occupied, final_outcome, dead_end_step)."""
    state = new_state(level)
    peak = 0
    for step, mv in enumerate(moves):
        state.take(mv)
        occ = state.capacity - state.shelf.count(None)
        peak = max(peak, occ)
        if state.over:
            return peak, state.outcome, (step if state.outcome != Outcome.WIN else None)
    return peak, state.outcome, None


def solve_from_state(level: LevelDef, taken: set[int], shelf: list,
                      triples: int, time_cap_s: float = 5.0) -> bool:
    """Feasibility from a mid-game state, using the solver's own DFS (_dfs)
    directly instead of its run() entry point, which always starts empty."""
    search = _Search(level, deadline=time.monotonic() + time_cap_s,
                      shelf_capacity=len(shelf))
    try:
        return search._dfs(set(taken), list(shelf), [], triples)
    except TimeoutError:
        return None  # inconclusive; caller must handle


def safe_move_fraction(level: LevelDef, moves: tuple[int, ...]):
    """At each state along `moves`, of the available choices, how many keep
    the level winnable (re-solved exactly, not sampled)? Returns a list of
    (avail_count, safe_count) per step where avail_count > 1 - a single-choice
    step carries no branching information."""
    state = new_state(level)
    per_step = []
    inconclusive = 0
    for mv in moves:
        avail = state.available()
        if len(avail) > 1:
            safe = 0
            for item in avail:
                trial = new_state(level, shelf_capacity=state.capacity)
                trial.taken = set(state.taken)
                trial.shelf = list(state.shelf)
                trial.triples_completed = state.triples_completed
                trial.take(item.id)
                if trial.over:
                    ok = trial.outcome == Outcome.WIN
                else:
                    ok = solve_from_state(level, trial.taken, trial.shelf,
                                          trial.triples_completed)
                    if ok is None:
                        inconclusive += 1
                        ok = False
                if ok:
                    safe += 1
            per_step.append((len(avail), safe))
        state.take(mv)
    return per_step, inconclusive


def main():
    files = sorted(f for f in glob.glob(f"{LEVELS_DIR}/*.json")
                    if not f.endswith(".meta"))
    print(f"{len(files)} level files")
    rows = []
    for f in files:
        name = Path(f).stem
        lv = load_level(f)
        reason, sol, dt = solve_with_reason(lv, GENEROUS_CAP_S)
        if reason == "timeout":
            reason2, sol2, dt2 = solve_with_reason(lv, PROOF_CAP_S)
            note = f"retried at {PROOF_CAP_S}s cap -> {reason2} ({dt2:.2f}s)"
            reason, sol, dt = reason2, sol2, dt + dt2
        else:
            note = ""

        row = {"file": name, "items": len(lv.pile), "reason": reason,
               "solve_time_s": round(dt, 4), "note": note}

        if reason == "solved":
            outcome, occ_end = replay(lv, list(sol.moves))
            row["replay_outcome"] = outcome.value
            peak, final_outcome, dead_step = peak_occupancy_and_dead_end(lv, sol.moves)
            row["peak_shelf"] = peak
            row["greedy_wins"] = greedy_wins(lv)

            per_step, inconclusive = safe_move_fraction(lv, sol.moves)
            trap_steps = [(a, s) for a, s in per_step if s < a]
            ratios = [s / a for a, s in per_step]
            row["branch_steps"] = len(per_step)          # steps with >1 choice
            row["trap_steps"] = len(trap_steps)           # >=1 wrong choice existed
            row["worst_trap"] = (min(trap_steps, key=lambda p: p[1] / p[0])
                                 if trap_steps else None)  # (avail, safe)
            row["min_safe_ratio"] = round(min(ratios), 3) if ratios else None
            row["mean_safe_ratio"] = round(sum(ratios) / len(ratios), 3) if ratios else None
            row["safe_ratio_inconclusive"] = inconclusive
        else:
            row["replay_outcome"] = None
            row["peak_shelf"] = None
            row["greedy_wins"] = None
            row["branch_steps"] = None
            row["trap_steps"] = None
            row["worst_trap"] = None
            row["min_safe_ratio"] = None
            row["mean_safe_ratio"] = None
            row["safe_ratio_inconclusive"] = None

        rows.append(row)
        print(json.dumps(row))

    out = Path(__file__).parent / "results.json"
    out.write_text(json.dumps(rows, indent=2))
    print("wrote", out)


if __name__ == "__main__":
    main()
