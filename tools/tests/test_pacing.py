"""Tests for the pacing curve (task 3.12) and the 37-level map."""
import collections
import json
import re
from pathlib import Path

import pytest

from tools.solver.pacing import (
    PILES_PER_ROOM, TOTAL_LEVELS, level_map, piles_for_room)

MAX_PILES_PER_ROOM = 4

SHIPPED = Path(__file__).resolve().parents[2] / "game/Assets/Resources/Levels"

# View/LevelAssets.cs builds "l{seq:00}_room{room:00}_pile{index}".
_LEVEL_NAME = re.compile(r"^l(\d{2})_room(\d{2})_pile(\d+)\.json$")


def _shipped_levels() -> list[tuple[int, int, int]]:
    """(sequence, room, pile_index) read off the names the game loads, in play order."""
    parsed = []
    for path in SHIPPED.glob("*.json"):
        match = _LEVEL_NAME.match(path.name)
        assert match is not None, f"unparseable level filename {path.name}"
        parsed.append(tuple(int(group) for group in match.groups()))
    return sorted(parsed)


def _piles_per_shipped_room() -> collections.Counter:
    return collections.Counter(room for _, room, _ in _shipped_levels())


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


def test_generated_plan_never_exceeds_the_cap():
    # The cap is a property of every room the plan generates, not only of the
    # tuple's maximum: a level map is what 05-ship-37-levels actually walks.
    per_room = collections.Counter(room for room, _ in level_map())
    for room in range(1, len(PILES_PER_ROOM) + 1):
        assert piles_for_room(room) <= MAX_PILES_PER_ROOM
        assert per_room[room] == piles_for_room(room)
        assert per_room[room] <= MAX_PILES_PER_ROOM


def test_shipped_set_is_the_thirty_seven_levels():
    shipped = _shipped_levels()
    assert len(shipped) == TOTAL_LEVELS == 37
    assert [seq for seq, _, _ in shipped] == list(range(1, 38))


def test_no_shipped_room_holds_more_than_four_piles():
    per_room = _piles_per_shipped_room()
    for room, count in sorted(per_room.items()):
        assert count <= MAX_PILES_PER_ROOM, f"room {room} ships {count} piles"


def test_shipped_piles_per_room_match_the_curve():
    per_room = _piles_per_shipped_room()
    assert sorted(per_room) == list(range(1, 13))
    assert [per_room[room] for room in range(1, 13)] == list(PILES_PER_ROOM)
    assert sum(per_room.values()) == 37


def test_shipped_pile_indices_are_contiguous_from_zero():
    per_room = collections.defaultdict(list)
    for _, room, pile in _shipped_levels():
        per_room[room].append(pile)
    for room, indices in per_room.items():
        assert sorted(indices) == list(range(piles_for_room(room))), room


def test_shipped_play_order_matches_the_level_map():
    assert [(room, pile) for _, room, pile in _shipped_levels()] == level_map()


def test_shipped_files_agree_with_their_own_filenames():
    # A filename can claim any room; the definition inside is what the game reads.
    for seq, room, pile in _shipped_levels():
        name = f"l{seq:02d}_room{room:02d}_pile{pile}.json"
        level = json.loads((SHIPPED / name).read_text())
        assert level["number"] == seq, name
        assert level["room_id"] == f"room_{room:02d}", name
        assert level["pile_index"] == pile, name
