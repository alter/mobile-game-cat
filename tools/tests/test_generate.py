"""Property tests for the generator — the 3.3/3.4 acceptance gate."""
import random

import pytest

from tools.solver.generate import _slack_for_level, generate_level, generate_with_slack
from tools.solver.schema import validate
from tools.solver.solver import solve


def test_slack_curve_endpoints_and_bounds():
    assert _slack_for_level(1) == 8
    assert _slack_for_level(12) == 2
    for n in range(1, 13):
        assert 2 <= _slack_for_level(n) <= 8
    # monotone non-increasing
    values = [_slack_for_level(n) for n in range(1, 13)]
    assert all(a >= b for a, b in zip(values, values[1:]))


@pytest.mark.parametrize("seed", range(5))
def test_every_generated_level_is_valid(seed):
    rng = random.Random(seed)
    for _ in range(10):
        level = generate_level(rng, item_count=36)
        validate(level)  # raises on any violation


def test_batch_of_100_all_parse_all_solvable():
    """The key property test: one unsolvable level rejects the milestone."""
    rng = random.Random(2026)
    levels = [generate_level(rng, item_count=36) for _ in range(100)]
    for level in levels:
        validate(level)
        sol = solve(level)
        assert sol is not None, (
            f"unsolvable level generated (seed 2026): {level.room_id}")


def test_generated_with_slack_sets_moves_limit():
    rng = random.Random(9)
    level = generate_with_slack(rng, number=1, item_count=36)
    assert level is not None
    sol = solve(level)
    assert sol is not None
    assert level.moves_limit == sol.move_count + 8
    level12 = generate_with_slack(rng, number=12, item_count=36)
    if level12 is not None:
        sol12 = solve(level12)
        assert level12.moves_limit == sol12.move_count + 2


def test_item_counts_are_multiples_of_three():
    rng = random.Random(11)
    for count in (30, 45, 58):  # 58 → bumped to 60 internally
        level = generate_level(rng, item_count=count)
        assert len(level.pile) % 3 == 0
