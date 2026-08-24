"""Conformance: same level + same solution → same outcome in Python and C#.

Generates random solvable levels, solves them in Python, replays the exact
same move order through the C# Board via build/solver-bridge, and asserts
both sides agree on the outcome and moves left.
"""
import json
import random
import subprocess
import tempfile
from pathlib import Path

import pytest

from tools.solver.generate import generate_level
from tools.solver.rules import Outcome, replay
from tools.solver.solver import solve

BRIDGE = Path(__file__).resolve().parents[2] / "build" / "solver-bridge"
N_CASES = 30


@pytest.fixture(scope="module")
def conformance_results():
    rng = random.Random(31337)
    levels = []
    solutions = {}
    expected = {}

    case = 0
    while case < N_CASES:
        level = generate_level(rng, item_count=rng.choice([24, 36]))
        sol = solve(level)
        if sol is None:
            continue
        outcome, moves_left = replay(level, list(sol.moves))
        key = str(case + 1)
        level_dict = {
            "number": case + 1,
            "room_id": level.room_id,
            "moves_limit": level.moves_limit,
            "pile": [{"id": i.id, "kind": i.kind,
                      "blocked_by": list(i.blocked_by)} for i in level.pile],
        }
        # bridge keys by number; renumber so each case is unique
        level_dict["number"] = case + 1
        levels.append(level_dict)
        solutions[key] = list(sol.moves)[:len(sol.moves)]  # full script
        # replay stops at game end; C# does the same via IsOver break
        expected[key] = {"outcome": outcome.value, "moves_left": moves_left}
        case += 1

    with tempfile.TemporaryDirectory() as tmp:
        lv_path = Path(tmp) / "levels.json"
        sol_path = Path(tmp) / "solutions.json"
        res_path = Path(tmp) / "results.json"
        lv_path.write_text(json.dumps(levels))
        sol_path.write_text(json.dumps(solutions))
        subprocess.run(
            ["dotnet", "run", "--project", str(BRIDGE), "--",
             str(lv_path), str(sol_path), str(res_path)],
            check=True, capture_output=True)
        results = {r["number"]: r for r in json.loads(res_path.read_text())}

    return expected, results


def test_csharp_and_python_agree(conformance_results):
    expected, results = conformance_results
    assert len(results) == N_CASES
    for key, exp in expected.items():
        got = results[int(key)]
        assert got["legal"], f"case {key}: {got['error']}"
        assert got["over"], f"case {key}: C# game did not end"
        assert got["outcome"].lower() == exp["outcome"], (
            f"case {key}: python={exp['outcome']} csharp={got['outcome']}")
        if exp["outcome"] != Outcome.WIN.value:
            assert got["moves_left"] == exp["moves_left"], (
                f"case {key}: moves_left diverged")
