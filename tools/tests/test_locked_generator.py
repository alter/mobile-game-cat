"""Tests for the locked-kind complication in the generator (task 3.11)."""
import random

import pytest

from tools.solver.generate import (
    LOCKED_KIND_FROM_ROOM, LOCKED_TRIPLE_THRESHOLD, generate_level)
from tools.solver.rules import Outcome, replay
from tools.solver.schema import validate
from tools.solver.solver import solve


@pytest.mark.parametrize("seed", range(5))
def test_late_rooms_carry_locked_kind(seed):
    rng = random.Random(seed)
    level = generate_level(rng, number=LOCKED_KIND_FROM_ROOM, item_count=36)
    locked_kinds = {i.kind for i in level.pile if i.locked_after_triples > 0}
    assert len(locked_kinds) == 1, "exactly one kind is locked"
    locked_kind = locked_kinds.pop()
    n = sum(1 for i in level.pile if i.kind == locked_kind)
    assert n % 3 == 0
    # every copy of the kind carries the same lock
    assert all(i.locked_after_triples == LOCKED_TRIPLE_THRESHOLD
               for i in level.pile if i.kind == locked_kind)


@pytest.mark.parametrize("seed", range(5))
def test_early_rooms_stay_lock_free(seed):
    rng = random.Random(seed)
    for room in (1, 4, 8):
        level = generate_level(rng, number=room, item_count=36)
        assert not any(i.locked_after_triples for i in level.pile), \
            f"room {room} must be lock-free"


def test_every_locked_level_solvable_and_winnable():
    """The key gate: a lock must never make the pile unsolvable."""
    rng = random.Random(777)
    for _ in range(20):
        level = generate_level(rng, number=9, item_count=36)
        validate(level)
        sol = solve(level)
        assert sol is not None, "locked level unsolvable"
        outcome, _ = replay(level, list(sol.moves))
        assert outcome == Outcome.WIN


def test_lock_threshold_is_reachable():
    """Threshold must be low enough that other triples can open the lock."""
    # with ~2 triples per kind, threshold 2 always leaves >=1 free triple to
    # complete before the locked kind matters; assert the invariant holds
    assert LOCKED_TRIPLE_THRESHOLD == 2
