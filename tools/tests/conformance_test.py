"""Conformance: same level + same move script → same outcome in Python and C#.

Covers both outcomes (win AND shelf_jam) plus booster recovery, per the
refactor plan step 3: until jams are compared across implementations,
task 3.1 is not closed.
"""
import json
import random
import subprocess
import tempfile
from pathlib import Path

import pytest

from tools.solver.generate import generate_level
from tools.solver.rules import Outcome, new_state, replay
from tools.solver.solver import solve

BRIDGE = Path(__file__).resolve().parents[2] / "build" / "solver-bridge"
N_WIN_CASES = 20
N_JAM_CASES = 10


def _greedy_finish(state):
    """Play greedily from the current state to the end; return the script."""
    script = []
    while not state.over:
        avail = state.available()
        if not avail:
            break
        counts: dict[str, int] = {}
        for k in state.shelf:
            if k:
                counts[k] = counts.get(k, 0) + 1
        avail.sort(key=lambda it: -counts.get(it.kind, 0))
        pick = avail[0]
        script.append(pick.id)
        try:
            state.take(pick.id)
        except ValueError:
            break
    return script


def build_cases(rng):
    """Returns (levels, scripts, expected) with wins, jams and boosters."""
    levels, scripts, expected = [], [], {}

    # --- win cases: solver solution replayed verbatim ---
    case = 0
    made = 0
    while made < N_WIN_CASES:
        level = generate_level(rng, item_count=rng.choice([24, 36]))
        sol = solve(level)
        if sol is None:
            continue
        outcome, _ = replay(level, list(sol.moves))
        case += 1
        levels.append(_level_dict(level, case))
        scripts.append([[m] for m in sol.moves])
        expected[str(case)] = {"outcome": outcome.value}
        made += 1

    # --- jam cases: take a prefix of the solution, then play greedily in a
    # kind-spread order designed to fill the shelf without matching ---
    jam_made = 0
    attempts = 0
    while jam_made < N_JAM_CASES and attempts < 200:
        attempts += 1
        level = generate_level(rng, item_count=36)
        if solve(level) is None:
            continue
        st = new_state(level)
        # greedy anti-match order: always take an item whose kind has the
        # FEWEST copies on the shelf → maximises shelf spread
        script = []
        while not st.over:
            avail = st.available()
            if not avail:
                break
            counts: dict[str, int] = {}
            for k in st.shelf:
                if k:
                    counts[k] = counts.get(k, 0) + 1
            avail.sort(key=lambda it: counts.get(it.kind, 0))
            pick = avail[0]
            script.append([pick.id])
            st.take(pick.id)
        if st.over and st.outcome == Outcome.SHELF_JAMMED:
            case += 1
            levels.append(_level_dict(level, case))
            scripts.append(script)
            expected[str(case)] = {"outcome": Outcome.SHELF_JAMMED.value}
            jam_made += 1

    assert len(expected) == N_WIN_CASES + N_JAM_CASES
    return levels, scripts, expected


def _level_dict(level, number, shelf_capacity=None):
    d = {
        "number": number,
        "room_id": level.room_id,
        "pile": [{"id": i.id, "kind": i.kind,
                  "blocked_by": list(i.blocked_by)} for i in level.pile],
    }
    if shelf_capacity:
        d["shelf_capacity"] = shelf_capacity
    return d


@pytest.fixture(scope="module")
def conformance_results():
    rng = random.Random(31337)
    levels, scripts, expected = build_cases(rng)

    with tempfile.TemporaryDirectory() as tmp:
        lv_path = Path(tmp) / "levels.json"
        sc_path = Path(tmp) / "scripts.json"
        res_path = Path(tmp) / "results.json"
        lv_path.write_text(json.dumps(levels))
        sc_path.write_text(json.dumps(
            {str(i + 1): s for i, s in enumerate(scripts)}))
        subprocess.run(
            ["dotnet", "run", "--project", str(BRIDGE), "--",
             str(lv_path), str(sc_path), str(res_path)],
            check=True, capture_output=True)
        results = {r["number"]: r for r in json.loads(res_path.read_text())}

    return expected, results


def _norm(outcome: str) -> str:
    """'ShelfJammed' -> 'shelf_jammed', 'Win' -> 'win'."""
    import re
    return re.sub(r'(?<!^)(?=[A-Z])', '_', outcome).lower()


def test_csharp_and_python_agree(conformance_results):
    expected, results = conformance_results
    outcomes_seen = set()
    for key, exp in expected.items():
        got = results[int(key)]
        assert got["legal"], f"case {key}: {got['error']}"
        assert got["over"], f"case {key}: C# game did not end"
        got_outcome = _norm(got["outcome"])
        assert got_outcome == exp["outcome"], (
            f"case {key}: python={exp['outcome']} csharp={got['outcome']}")
        outcomes_seen.add(exp["outcome"])

    # acceptance: BOTH outcomes covered by the comparison
    assert Outcome.WIN.value in outcomes_seen
    assert Outcome.SHELF_JAMMED.value in outcomes_seen


def test_booster_recovery_agrees(conformance_results):
    """A jammed board + AddSlots must unblock identically on both sides."""
    rng = random.Random(777)
    level = generate_level(rng, item_count=36)
    assert solve(level) is not None

    # drive Python into a jam
    st = new_state(level)
    script = []
    while not st.over:
        avail = st.available()
        if not avail:
            break
        counts: dict[str, int] = {}
        for k in st.shelf:
            if k:
                counts[k] = counts.get(k, 0) + 1
        avail.sort(key=lambda it: counts.get(it.kind, 0))
        pick = avail[0]
        script.append([pick.id])
        st.take(pick.id)
    assert st.outcome == Outcome.SHELF_JAMMED

    # recover with boosters on both sides
    slots_needed = 3  # one extra triple's worth
    st.add_slots(slots_needed)

    case_levels = [_level_dict(level, 1)]
    case_scripts = {str(1): script[:len(script)-1] +
                    [[script[-1][0], "booster", slots_needed]] +
                    _greedy_finish(st)}
    with tempfile.TemporaryDirectory() as tmp:
        lv_path = Path(tmp) / "l.json"
        sc_path = Path(tmp) / "s.json"
        res_path = Path(tmp) / "r.json"
        lv_path.write_text(json.dumps(case_levels))
        sc_path.write_text(json.dumps(case_scripts))
        subprocess.run(
            ["dotnet", "run", "--project", str(BRIDGE), "--",
             str(lv_path), str(sc_path), str(res_path)],
            check=True, capture_output=True)
        r = json.loads(res_path.read_text())[0]

    # after booster + greedy finish, python says:
    py_final = st.over and st.outcome == Outcome.WIN
    cs_over = bool(r["over"])
    cs_win = r["outcome"].lower() == "win"
    assert cs_over, f"C# did not finish after booster; result={r}"
    assert cs_win == py_final, (
        f"booster recovery diverged: python_win={py_final} csharp={r['outcome']}")
