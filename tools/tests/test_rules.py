"""Rules mirror tests — acceptance cases copied from game/Tests/Core/BoardTests.cs."""
import pytest

from tools.solver.rules import CAPACITY, Outcome, new_state, replay
from tools.solver.schema import LevelDef, PileItem


def L(moves, *pile):
    return LevelDef(number=7, room_id="room_1", moves_limit=moves, pile=tuple(pile))


def E(i, kind, *blocked):
    return PileItem(i, kind, tuple(blocked))


# ---- GetAvailable --------------------------------------------------------

def test_empty_pile_no_available():
    assert new_state(L(10)).available() == []


def test_single_layer_all_available():
    st = new_state(L(10, E(1, "a"), E(2, "b")))
    assert sorted(i.id for i in st.available()) == [1, 2]


def test_three_layers_only_top_of_each_stack():
    st = new_state(L(10, E(1, "a", 2), E(2, "a", 3), E(3, "a"), E(4, "b")))
    assert sorted(i.id for i in st.available()) == [3, 4]


def test_circular_block_nothing_in_cycle_available():
    st = new_state(L(10, E(1, "a", 2), E(2, "b", 1), E(3, "c")))
    assert sorted(i.id for i in st.available()) == [3]


def test_after_taking_top_item_below_becomes_available():
    st = new_state(L(10, E(1, "a", 2), E(2, "b"), E(3, "c")))
    assert st.take(2)
    assert sorted(i.id for i in st.available()) == [1, 3]


def test_cannot_take_blocked_or_twice_or_unknown():
    st = new_state(L(10, E(1, "a", 2), E(2, "b")))
    with pytest.raises(ValueError, match="blocked"):
        st.take(1)
    assert st.take(2)
    with pytest.raises(ValueError, match="already taken"):
        st.take(2)
    with pytest.raises(ValueError, match="unknown"):
        st.take(99)


def test_duplicate_item_ids_rejected_at_validation():
    from tools.solver.schema import LevelValidationError, validate
    with pytest.raises(LevelValidationError):
        validate(L(10, E(1, "a"), E(1, "b")))


# ---- Shelf ---------------------------------------------------------------

def test_place_fills_leftmost_free_slot():
    st = new_state(L(10, E(1, "a"), E(2, "b")))
    st.take(1)
    st.take(2)
    assert st.shelf[0] == "a" and st.shelf[1] == "b"
    assert st.shelf.count(None) == CAPACITY - 2


def test_match_across_row_boundary():
    # two fill row 0, third lands in row 1 — match must still fire
    assert CAPACITY == 9
    st = new_state(L(50, *(E(i, "m") for i in range(1, 4))))
    for i in range(1, 4):
        st.take(i)
    assert st.shelf.count(None) == CAPACITY


def test_two_of_a_kind_do_not_match():
    st = new_state(L(10, E(1, "a"), E(2, "a")))
    st.take(1)
    st.take(2)
    assert st.shelf.count("a") == 2


# ---- Outcomes ------------------------------------------------------------

def test_win():
    outcome, _ = replay(L(5, E(1, "a"), E(2, "a"), E(3, "a")), [1, 2, 3])
    assert outcome == Outcome.WIN


def test_out_of_moves():
    outcome, left = replay(L(1, E(1, "a"), E(2, "b")), [1])
    assert outcome == Outcome.OUT_OF_MOVES and left == 0


def test_shelf_jammed_full_without_match():
    entries = [E(i, f"k{i}") for i in range(1, 10)]
    outcome, _ = replay(L(50, *entries), list(range(1, 10)))
    assert outcome == Outcome.SHELF_JAMMED


def test_winning_take_consumes_no_move():
    st = new_state(L(10, E(1, "a"), E(2, "a"), E(3, "a")))
    st.take(1)
    assert st.moves_left == 9
    st.take(2)
    assert st.moves_left == 8
    st.take(3)
    assert st.over and st.outcome == Outcome.WIN
    assert st.moves_left == 8


def test_illegal_move_in_replay_raises():
    with pytest.raises(ValueError, match="illegal move"):
        replay(L(10, E(1, "a")), [99])
    with pytest.raises(ValueError, match="blocked"):
        replay(L(10, E(1, "a", 2), E(2, "b")), [1])


def test_replay_stops_at_game_end():
    # trailing moves after the win are not executed and do not raise
    outcome, _ = replay(L(10, E(1, "a"), E(2, "a"), E(3, "a")), [1, 2, 3, 1, 1])
    assert outcome == Outcome.WIN
