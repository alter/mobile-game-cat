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
        state = new_state(level)
        revealed_trace = []
        shelf_trace = []
        for move in sol.moves:
            state.take(move)
            revealed_trace.append(
                {str(i.id): state.is_revealed(i) for i in level.pile})
            shelf_trace.append(list(state.shelf))
        expected[str(case)] = {
            "outcome": outcome.value,
            "occupied": state.capacity - state.shelf.count(None),
            "triples": state.triples_completed,
            "revealed": revealed_trace,
            "shelf": shelf_trace,
        }
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
        revealed_trace = []
        shelf_trace = []
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
            revealed_trace.append(
                {str(i.id): st.is_revealed(i) for i in level.pile})
            shelf_trace.append(list(st.shelf))
        if st.over and st.outcome == Outcome.SHELF_JAMMED:
            case += 1
            levels.append(_level_dict(level, case))
            scripts.append(script)
            expected[str(case)] = {
                "outcome": Outcome.SHELF_JAMMED.value,
                "occupied": st.capacity - st.shelf.count(None),
                "triples": st.triples_completed,
                "revealed": revealed_trace,
                "shelf": shelf_trace,
            }
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
        # Not just the outcome: the two engines disagreed for months on whether
        # the last item is placed before the win is declared, and matching
        # outcomes hid it. Compare the position they end in.
        assert got["occupied"] == exp["occupied"], (
            f"case {key}: shelf holds {exp['occupied']} in python, "
            f"{got['occupied']} in csharp")
        assert got["triples"] == exp["triples"], (
            f"case {key}: {exp['triples']} triples in python, "
            f"{got['triples']} in csharp")

        # Outcome/occupied/triples all still agree even if the two engines
        # disagree about which item is visible or which slot it sits in —
        # both are D-decisions (D15: locked is seen, not hidden; D16: the
        # shelf neither compacts nor sorts) that never showed up in a scalar
        # summary. Compare every move, item by item and slot by slot.
        exp_revealed, got_revealed = exp["revealed"], got["revealed"]
        assert len(got_revealed) == len(exp_revealed), (
            f"case {key}: python played {len(exp_revealed)} moves, "
            f"csharp played {len(got_revealed)}")
        for move_idx, (exp_snap, got_snap) in enumerate(
                zip(exp_revealed, got_revealed)):
            for item_id, exp_val in exp_snap.items():
                got_val = got_snap.get(item_id)
                assert got_val == exp_val, (
                    f"case {key}, move {move_idx}, item {item_id}: "
                    f"revealed python={exp_val} csharp={got_val}")

        exp_shelf, got_shelf = exp["shelf"], got["shelf"]
        assert len(got_shelf) == len(exp_shelf), (
            f"case {key}: shelf trace length python={len(exp_shelf)} "
            f"csharp={len(got_shelf)}")
        for move_idx, (exp_row, got_row) in enumerate(zip(exp_shelf, got_shelf)):
            assert len(got_row) == len(exp_row), (
                f"case {key}, move {move_idx}: shelf capacity python="
                f"{len(exp_row)} csharp={len(got_row)}")
            for slot_idx, (exp_kind, got_kind) in enumerate(zip(exp_row, got_row)):
                assert got_kind == exp_kind, (
                    f"case {key}, move {move_idx}, slot {slot_idx}: "
                    f"python={exp_kind!r} csharp={got_kind!r}")

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

    # The booster must land AFTER the move that jammed, not on it. Attaching it
    # to the last move grew the shelf before the jam happened, so the C# side
    # never jammed and recovery went unchecked — while C# in fact could not
    # recover at all, because AddSlots left the board over.
    finish = _greedy_finish(st)
    assert finish, "the booster freed no move — nothing to compare"
    case_levels = [_level_dict(level, 1)]
    case_scripts = {str(1): script +
                    [[finish[0], "booster", slots_needed]] +
                    finish[1:]}
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
    assert r["capacity"] == st.capacity, (
        f"shelf capacity diverged: python={st.capacity} csharp={r['capacity']}")
    assert cs_over, f"C# did not finish after booster; result={r}"
    assert cs_win == py_final, (
        f"booster recovery diverged: python_win={py_final} csharp={r['outcome']}")


def test_locked_items_agree(conformance_results):
    """A move on a locked item must be refused identically on both sides,
    and — D15 — its visibility while locked must match too. The whole point
    of D15 was that a locked item is *seen*, not hidden; a bridge that only
    compared outcome/occupied/triples could not have told the two engines
    apart even if one still hid it. See tasks/DECISIONS.md D15.
    """
    rng = random.Random(4242)
    from tools.solver.schema import LevelDef, PileItem
    # locked 'x' triple + one full 'a' triple: taking the a-triple unlocks x
    pile = (
        PileItem(1, "x", (), 1),          # locked until 1 triple
        PileItem(2, "x"), PileItem(3, "x"),
        PileItem(4, "a"), PileItem(5, "a"), PileItem(6, "a"),
    )
    level = LevelDef(1, "room_01", 0, pile)
    levels = [{
        "number": 1, "room_id": level.room_id,
        "pile": [{"id": i.id, "kind": i.kind,
                  "blocked_by": list(i.blocked_by),
                  "locked_after_triples": i.locked_after_triples}
                 for i in level.pile]}]

    # python side
    st = new_state(level)
    with pytest.raises(ValueError, match="locked"):
        st.take(1)
    assert st.is_revealed(level.by_id()[1]) is True, (
        "D15: a locked item must be visible even while it cannot be taken")

    # Full script, matching the C# side exactly (below) so the two per-move
    # traces line up move for move.
    script_order = [4, 5, 6, 1, 2, 3]
    revealed_trace = []
    shelf_trace = []
    for item_id in script_order:
        st.take(item_id)
        revealed_trace.append(
            {str(i.id): st.is_revealed(i) for i in level.pile})
        shelf_trace.append(list(st.shelf))

    # csharp side — same script
    script = {"1": [[n] for n in script_order]}
    with tempfile.TemporaryDirectory() as tmp:
        lv_path = Path(tmp) / "l.json"
        sc_path = Path(tmp) / "s.json"
        res_path = Path(tmp) / "r.json"
        lv_path.write_text(json.dumps(levels))
        sc_path.write_text(json.dumps(script))
        subprocess.run(
            ["dotnet", "run", "--project", str(BRIDGE), "--",
             str(lv_path), str(sc_path), str(res_path)],
            check=True, capture_output=True)
        r = json.loads(res_path.read_text())[0]

    got_revealed = r["revealed"]
    assert len(got_revealed) == len(revealed_trace), (
        f"move count mismatch: python={len(revealed_trace)} "
        f"csharp={len(got_revealed)}")
    for move_idx, (item_id, exp_snap, got_snap) in enumerate(
            zip(script_order, revealed_trace, got_revealed)):
        for iid, exp_val in exp_snap.items():
            got_val = got_snap.get(iid)
            assert got_val == exp_val, (
                f"move {move_idx} (took item {item_id}): item {iid} "
                f"revealed python={exp_val} csharp={got_val}")

    got_shelf = r["shelf"]
    assert len(got_shelf) == len(shelf_trace), (
        f"shelf trace length python={len(shelf_trace)} "
        f"csharp={len(got_shelf)}")
    for move_idx, (item_id, exp_row, got_row) in enumerate(
            zip(script_order, shelf_trace, got_shelf)):
        for slot_idx, (exp_kind, got_kind) in enumerate(zip(exp_row, got_row)):
            assert got_kind == exp_kind, (
                f"move {move_idx} (took item {item_id}), slot {slot_idx}: "
                f"python={exp_kind!r} csharp={got_kind!r}")

    assert r["legal"], r["error"]
    assert r["outcome"].lower() == "win"


def test_locked_level_solution_agrees():
    """Solver opens locks via free triples; C# replays to the same win."""
    from tools.solver.schema import LevelDef, PileItem
    lvl = LevelDef(1, "room_01", 0, (
        PileItem(1, "x", (), 1), PileItem(2, "x", (), 1), PileItem(3, "x", (), 1),
        PileItem(4, "a"), PileItem(5, "a"), PileItem(6, "a"),
    ))
    sol = solve(lvl)
    assert sol is not None
    outcome, _ = replay(lvl, list(sol.moves))
    assert outcome == Outcome.WIN

    levels = [{"number": 1, "room_id": lvl.room_id,
               "pile_index": 0,
               "pile": [{"id": i.id, "kind": i.kind,
                         "blocked_by": [],
                         "locked_after_triples": i.locked_after_triples}
                        for i in lvl.pile]}]
    with tempfile.TemporaryDirectory() as tmp:
        lv_path = Path(tmp) / "l.json"
        sc_path = Path(tmp) / "s.json"
        res_path = Path(tmp) / "r.json"
        lv_path.write_text(json.dumps(levels))
        sc_path.write_text(json.dumps({"1": [[m] for m in sol.moves]}))
        subprocess.run(
            ["dotnet", "run", "--project", str(BRIDGE), "--",
             str(lv_path), str(sc_path), str(res_path)],
            check=True, capture_output=True)
        r = json.loads(res_path.read_text())[0]

    assert r["legal"] and r["over"] and r["outcome"].lower() == "win"
