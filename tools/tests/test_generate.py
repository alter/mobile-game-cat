"""Property tests for the generator — the 3.3 acceptance gate."""
import random

import pytest

from tools.solver.generate import _items_for_level, generate_level
from tools.solver.schema import validate
from tools.solver.solver import solve


def test_pile_size_curve():
    assert [_items_for_level(n) for n in range(1, 13)] == \
        [36] * 4 + [48] * 4 + [60] * 4


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


def test_explicit_kind_count_is_respected():
    rng = random.Random(9)
    level = generate_level(rng, item_count=36, kind_count=6)
    kinds = {i.kind for i in level.pile}
    assert len(kinds) == 6
    for kind in kinds:
        assert sum(1 for i in level.pile if i.kind == kind) % 3 == 0


def test_item_counts_are_multiples_of_three():
    rng = random.Random(11)
    for count in (30, 45, 58):  # 58 → bumped to 60 internally
        level = generate_level(rng, item_count=count)
        assert len(level.pile) % 3 == 0
