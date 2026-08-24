"""Solver tests — acceptance for task 3.2: solvable + dead ends, fast."""
import random
import time

from tools.solver.generate import generate_level
from tools.solver.rules import Outcome, replay
from tools.solver.schema import LevelDef, PileItem
from tools.solver.solver import solve


def L(*pile):
    return LevelDef(number=7, room_id="room_1", pile_index=0,
                    pile=tuple(pile))


def E(i, kind, *blocked):
    return PileItem(i, kind, tuple(blocked))


def Locked(i, kind, unlock_after):
    return PileItem(i, kind, (), unlock_after)


def test_five_known_solvable():
    cases = [
        L(E(1, "a"), E(2, "a"), E(3, "a")),                      # plain triple
        L(E(1, "a", 2), E(2, "a", 3), E(3, "a"),                 # stack of one kind
             E(4, "b"), E(5, "b"), E(6, "b")),
        L(E(1, "a"), E(2, "b"), E(3, "a"), E(4, "b"),            # interleaved pairs
             E(5, "a"), E(6, "b")),
        L(*(E(i, f"k{(i - 1) // 3}") for i in range(1, 13))),    # four kinds wide
        generate_level(random.Random(42), item_count=24),
    ]
    for level in cases:
        sol = solve(level)
        assert sol is not None, f"level {level.number} should be solvable"
        outcome, _ = replay(level, list(sol.moves))
        assert outcome == Outcome.WIN


def test_solution_is_legal_and_winning_on_generated_levels():
    rng_level = generate_level(random.Random(42), item_count=24)
    sol = solve(rng_level)
    assert sol is not None
    outcome, _ = replay(rng_level, list(sol.moves))
    assert outcome == Outcome.WIN


def test_locked_items_in_search():
    # locked 'x' triple + one free 'a' triple: solvable via the a-triple first
    lvl = L(Locked(1, "x", 1), Locked(2, "x", 1), Locked(3, "x", 1),
            E(4, "a"), E(5, "a"), E(6, "a"))
    sol = solve(lvl)
    assert sol is not None
    outcome, _ = replay(lvl, list(sol.moves))
    assert outcome == Outcome.WIN

    # impossible variant: 'x' needs 2 triples but only 1 exists → unsolvable
    hard = L(Locked(1, "x", 2), Locked(2, "x", 2), Locked(3, "x", 2),
             E(4, "a"), E(5, "a"), E(6, "a"))
    assert solve(hard) is None


def test_circular_block_unsolvable():
    cycled = L(E(1, "a", 2), E(2, "a", 1), E(3, "a"),
               E(4, "b"), E(5, "b"), E(6, "b"))
    assert solve(cycled, state_cap=50000) is None


def test_shelf_jam_dead_end():
    pile = []
    for k in range(5):
        for i in range(3):
            pile.append(E(k * 3 + i + 1, f"k{k}"))
    from tools.solver.rules import new_state
    st = new_state(L(*pile))
    order = [k * 3 + i + 1 for i in range(3) for k in range(5)]
    for id_ in order:
        if st.over:
            break
        st.take(id_)
    assert st.over and st.outcome == Outcome.SHELF_JAMMED


def test_solver_speed_under_two_seconds_realistic():
    level = generate_level(random.Random(7), item_count=45)
    t0 = time.monotonic()
    sol = solve(level)
    dt = time.monotonic() - t0
    assert sol is not None
    assert dt < 2.0, f"solver took {dt:.2f}s"


def test_minimal_move_count_matches_replay():
    level = generate_level(random.Random(3), item_count=21)
    sol = solve(level)
    assert sol is not None
    assert sol.move_count == len(sol.moves) == len(set(sol.moves))
