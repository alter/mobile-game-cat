"""Task 30-levels-solver/10: the measurement itself has to be trustworthy.

Two of these tests exist because the first version of this script was wrong in
a way that produced a confident, believable, false table.
"""
import inspect
import random

import pytest

from tools.solver.measure import (_best, measure_bands, oracle_policy,
                                  partial_policy, play, shelf_only_policy)
from tools.solver.generate import generate_level
from tools.solver.rules import Outcome
from tools.solver.solver import solve


def _level(seed=1, items=36, room=1):
    rng = random.Random(seed)
    while True:
        level = generate_level(rng, number=room, item_count=items,
                               room_id=f"room_{room:02d}")
        if solve(level) is not None:
            return level


def test_player_policies_receive_nothing_but_what_a_player_sees():
    # VERIFY 2 of the task, enforced by the signature rather than by review:
    # neither policy can consult the level, the state or a buried item.
    for policy in (shelf_only_policy, partial_policy):
        params = list(inspect.signature(policy).parameters)
        assert params == ["choices", "shelf"], policy.__name__
    # the oracle is the one allowed to know more, and says so
    assert list(inspect.signature(oracle_policy).parameters) == \
        ["choices", "shelf", "dig_cost"]


def test_ties_are_not_resolved_by_item_id():
    # The bug this guards: scoring ended in `-item_id`, so every tie went to
    # the lowest id. Item ids follow the generator's layer order, which made
    # the policy quietly excellent — 100% wins instead of 79%. A player cannot
    # see ids, so all equally-good moves must come back.
    choices = [(5, "a"), (9, "a"), (2, "b")]
    empty = (None, None, None)
    assert sorted(shelf_only_policy(choices, empty)) == [2, 5, 9]
    assert sorted(partial_policy(choices, empty)) == [5, 9]


def test_a_finishable_triple_is_always_finished():
    choices = [(1, "a"), (2, "b")]
    shelf = ("a", "a", None)
    assert shelf_only_policy(choices, shelf) == [1]
    assert partial_policy(choices, shelf) == [1]
    assert oracle_policy(choices, shelf, {"a": 99, "b": 0}) == [1]


def test_best_returns_every_equal_option():
    scored = _best([(1, "a"), (2, "b"), (3, "c")], lambda c: 0)
    assert scored == [1, 2, 3]


def test_the_oracle_only_breaks_ties_partial_cannot():
    # Same first three criteria: with equal dig costs the two agree.
    choices = [(1, "a"), (2, "b")]
    empty = (None, None, None)
    flat = {"a": 0, "b": 0}
    assert oracle_policy(choices, empty, flat) == partial_policy(choices, empty)
    # and with unequal ones it prefers the kind that is cheaper to dig out
    assert oracle_policy(choices, empty, {"a": 10, "b": 0}) == [2]


@pytest.mark.parametrize("policy", ["shelf_only", "partial", "oracle"])
def test_a_run_is_reproducible(policy):
    level = _level()
    first = play(level, policy, random.Random(4), 0.1)
    again = play(level, policy, random.Random(4), 0.1)
    assert first is again


def test_every_game_ends_in_an_outcome():
    level = _level(items=60, room=9)
    for seed in range(20):
        assert play(level, "partial", random.Random(seed), 0.2) in (
            Outcome.WIN, Outcome.SHELF_JAMMED)


def test_bands_come_out_in_difficulty_order():
    rows = measure_bands(games=40, seed=20260826)
    rates = [r["partial"] for r in rows]
    assert rates == sorted(rates, reverse=True), rows
    assert [r["items"] for r in rows] == [36, 48, 60]
