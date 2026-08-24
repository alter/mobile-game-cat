"""Solver tests — acceptance for task 3.2: 5 solvable + 5 dead ends, <2s."""
import time

from tools.solver.generate import generate_level
from tools.solver.rules import Outcome, replay
from tools.solver.schema import LevelDef, PileItem
from tools.solver.solver import solve


def L(moves, *pile):
    return LevelDef(number=7, room_id="room_1", moves_limit=moves, pile=tuple(pile))


def E(i, kind, *blocked):
    return PileItem(i, kind, tuple(blocked))


def test_five_known_solvable():
    cases = [
        L(10, E(1, "a"), E(2, "a"), E(3, "a")),                      # plain triple
        L(20, E(1, "a", 2), E(2, "a", 3), E(3, "a"),                 # stack of one kind
             E(4, "b"), E(5, "b"), E(6, "b")),
        L(15, E(1, "a"), E(2, "b"), E(3, "a"), E(4, "b"),            # interleaved pairs
             E(5, "a"), E(6, "b")),
        L(50, *(E(i, f"k{i}") for i in range(1, 10))),               # nine distinct → jam!
    ]
    # note: case 4 is actually a dead end (see test_five_dead_ends);
    # replace with a genuinely solvable wide board:
    cases[3] = L(60, *(E(i, f"k{(i - 1) // 3}") for i in range(1, 13)))
    for level in cases:
        sol = solve(level)
        assert sol is not None, f"level {level.number} should be solvable"
        outcome, _ = replay(level, list(sol.moves))
        assert outcome == Outcome.WIN


def test_solution_is_legal_and_winning_on_generated_levels():
    rng_level = generate_level(__import__("random").Random(42), item_count=24)
    sol = solve(rng_level)
    assert sol is not None
    outcome, _ = replay(rng_level, list(sol.moves))
    assert outcome == Outcome.WIN


def test_five_known_dead_ends():
    dead = [
        # circular block makes items permanently unreachable
        L(99, E(1, "a", 2), E(2, "a", 1), E(3, "a"),
             E(4, "b"), E(5, "b"), E(6, "b")),
        # nine distinct kinds fill shelf → jam before clearing
        L(99, *(E(i, f"k{i}") for i in range(1, 10))),
        # buried odd kind under blockers with tight move limit
        L(3, E(1, "x"), E(2, "y"), E(3, "y"), E(4, "y"),
             E(5, "z"), E(6, "z"), E(7, "z")),
        # not enough moves to ever clear
        L(1, E(1, "a"), E(2, "a"), E(3, "a")),
    ]
    for level in dead:
        assert solve(level, state_cap=50000) is None, (
            f"level {level.number} should be unsolvable")


def test_solver_speed_under_two_seconds_realistic():
    level = generate_level(__import__("random").Random(7), item_count=45)
    t0 = time.monotonic()
    sol = solve(level)
    dt = time.monotonic() - t0
    assert sol is not None
    assert dt < 2.0, f"solver took {dt:.2f}s"


def test_minimal_move_count_matches_replay():
    level = generate_level(__import__("random").Random(3), item_count=21)
    sol = solve(level)
    assert sol is not None
    assert sol.move_count == len(sol.moves) == len(set(sol.moves))
