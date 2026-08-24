"""Tests for the pacing curve (task 3.12) and the 37-level map."""
import pytest

from tools.solver.pacing import (
    PILES_PER_ROOM, TOTAL_LEVELS, level_map, piles_for_room)


def test_curve_matches_spec():
    assert PILES_PER_ROOM == (1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4)
    assert TOTAL_LEVELS == 37


def test_no_room_needs_more_than_four_piles():
    assert max(PILES_PER_ROOM) <= 4


def test_first_room_single_level():
    # she sees the whole loop in level one
    assert piles_for_room(1) == 1


def test_monotone_nondecreasing():
    assert all(a <= b for a, b in zip(PILES_PER_ROOM, PILES_PER_ROOM[1:]))


def test_level_map_covers_every_pile():
    plan = level_map()
    assert len(plan) == 37
    assert len(set(plan)) == 37  # no duplicates
    for room in range(1, 13):
        indices = [p for r, p in plan if r == room]
        assert sorted(indices) == list(range(piles_for_room(room)))


def test_out_of_range_room_raises():
    with pytest.raises(ValueError):
        piles_for_room(0)
    with pytest.raises(ValueError):
        piles_for_room(13)
