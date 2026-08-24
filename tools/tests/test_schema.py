"""Tests for the level format schema."""
import pytest

from tools.solver.schema import (
    LevelDef, LevelValidationError, PileItem,
    level_from_dict, level_to_dict, validate)


def make_level(pile=None, number=1, room_id="room_01"):
    return LevelDef(number=number, room_id=room_id, pile=tuple(pile or []))


def test_valid_roundtrip():
    level = make_level([
        PileItem(1, "vase", (2,)),
        PileItem(2, "book"),
        PileItem(3, "vase"),
        PileItem(4, "vase"),
        PileItem(5, "book"),
        PileItem(6, "book"),
    ])
    validate(level)  # no raise
    restored = level_from_dict(level_to_dict(level))
    assert restored == level


def test_duplicate_ids_raise():
    with pytest.raises(LevelValidationError, match="duplicate"):
        validate(make_level([PileItem(1, "a"), PileItem(1, "b")]))


def test_dangling_blocked_by_raises():
    with pytest.raises(LevelValidationError, match="does not exist"):
        validate(make_level([PileItem(1, "a", (9,))]))


def test_self_reference_raises():
    with pytest.raises(LevelValidationError):
        validate(make_level([PileItem(1, "a", (1,))]))


def test_cycle_raises():
    with pytest.raises(LevelValidationError, match="cycle"):
        validate(make_level([PileItem(1, "a", (2,)), PileItem(2, "b", (1,))]))


def test_kind_count_not_multiple_of_three_raises():
    with pytest.raises(LevelValidationError, match="multiple of 3"):
        validate(make_level([PileItem(1, "a"), PileItem(2, "a")]))


def test_malformed_dict_raises():
    with pytest.raises(LevelValidationError, match="malformed"):
        level_from_dict({"number": 1})


def test_empty_room_raises():
    with pytest.raises(LevelValidationError):
        validate(make_level(room_id=""))
