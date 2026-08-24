"""Rules mirror tests — kept in lockstep with game/Tests/Core/BoardTests.cs."""
import pytest

from tools.solver.rules import DEFAULT_CAPACITY, Outcome, new_state, replay
from tools.solver.schema import LevelDef, LevelValidationError, PileItem, validate


def L(*pile):
    """Build a level, padding each kind group to a multiple of three."""
    from tools.solver.schema import LevelDef, PileItem as P
    out = list(pile)
    next_id = len(out) + 1
    for kind in {e.kind for e in out}:
        n = sum(1 for e in out if e.kind == kind)
        deficit = (3 - n % 3) % 3
        for _ in range(deficit):
            out.append(P(next_id, kind))
            next_id += 1
    return LevelDef(number=7, room_id="room_1", pile=tuple(out))


def E(i, kind, *blocked):
    return PileItem(i, kind, tuple(blocked))


def _taken_ids(state):
    return state.taken


# helper no longer used; kept out


# ---- GetAvailable --------------------------------------------------------

def test_empty_pile_no_available():
    assert new_state(L()).available() == []


def test_single_layer_all_available():
    st = new_state(L(E(1, "a"), E(2, "b"), E(3, "b")))
    ids = [i.id for i in st.available()]
    assert 1 in ids and 2 in ids


def test_three_layers_only_top_of_each_stack():
    st = new_state(L(E(1, "a", 2), E(2, "a", 3), E(3, "a"),
                     E(4, "b"), E(5, "b", 4)))
    ids = [i.id for i in st.available()]
    assert 3 in ids and 4 in ids
    assert 1 not in ids and 2 not in ids


def test_circular_block_nothing_in_cycle_available():
    st = new_state(L(E(1, "a", 2), E(2, "b", 1), E(3, "c")))
    ids = [i.id for i in st.available()]
    assert 3 in ids
    assert 1 not in ids and 2 not in ids


def test_after_taking_top_item_below_becomes_available():
    st = new_state(L(E(1, "a", 2), E(2, "b"), E(3, "c")))
    assert st.take(2)
    ids = [i.id for i in st.available()]
    assert 1 in ids and 3 in ids


def test_illegal_moves_raise():
    st = new_state(L(E(1, "a", 2), E(2, "b")))
    with pytest.raises(ValueError, match="blocked"):
        st.take(1)
    st.take(2)
    with pytest.raises(ValueError, match="already taken"):
        st.take(2)
    with pytest.raises(ValueError, match="unknown"):
        st.take(99)


# note: duplicate-id rejection lives in schema validation
def test_duplicate_item_ids_rejected_at_validation():
    from tools.solver.schema import LevelValidationError, validate
    with pytest.raises(LevelValidationError):
        validate(L(E(1, "a"), E(1, "a"), E(1, "b")))


# ---- Triple validation ----------------------------------------------------

def test_kind_not_in_triples_rejected():
    with pytest.raises(ValueError, match="multiple of three"):
        new_state(LevelDef(1, "r", (E(1, "a"), E(2, "a"))))


# ---- Shelf ----------------------------------------------------------------

def test_place_fills_leftmost_free_slot():
    st = new_state(L(E(1, "a"), E(2, "b")))
    st.take(1)
    st.take(2)
    assert st.shelf[0] == "a" and st.shelf[1] == "b"


def test_match_across_row_boundary():
    # 2 copies of 'm' sit on the shelf; a third copy completes the triple.
    # A fourth item keeps the board open so the cleared slots are observable
    # (if the triple were the last take, the win would fire before placement).
    st = new_state(L(E(1, "m"), E(2, "m"), E(3, "m"), E(4, "n")))
    st.take(1)
    st.take(2)
    assert st.shelf.count("m") == 2  # no match before the third copy
    st.take(3)  # third 'm' completes the triple and clears all three
    assert st.shelf.count("m") == 0
    n_on_shelf = sum(1 for e in st.level.pile
                     if e.kind == "n" and e.id in st.taken)
    assert st.shelf.count(None) == DEFAULT_CAPACITY - n_on_shelf


def test_two_of_a_kind_do_not_match():
    st = new_state(L(E(1, "a"), E(2, "a"), E(3, "b")))
    st.take(1)
    st.take(2)
    assert st.shelf.count("a") == 2


# ---- Outcomes -------------------------------------------------------------

def test_win_checked_before_jam():
    # 4 kinds x 3 on a shelf that would fill first: emptied pile still wins
    pile = []
    for k in range(4):
        for i in range(3):
            pile.append(E(k * 3 + i + 1, f"k{k}"))
    outcome, _ = replay(L(*pile),
                        [k * 3 + i + 1 for k in range(4) for i in range(3)],
                        shelf_capacity=12)
    assert outcome == Outcome.WIN


def test_shelf_jammed_unmatched_kinds_fill_shelf():
    pile = []
    for k in range(5):
        for i in range(3):
            pile.append(E(k * 3 + i + 1, f"k{k}"))
    order = [1, 4, 7, 10, 13, 2, 5, 8, 11, 14]
    outcome, _ = replay(L(*pile), order)
    assert outcome == Outcome.SHELF_JAMMED


def test_add_slots_booster_recovers_from_full_shelf():
    st = new_state(L(E(1, "a"), E(2, "b"), E(3, "c")))
    for i in (1, 2, 3):
        st.take(i)
    assert not st.over  # 3 of 9 slots used

    # fill the shelf to jam: 6 more distinct kinds needed; build such a level
    pile = []
    for k in range(5):
        for i in range(3):
            pile.append(E(k * 3 + i + 1, f"k{k}"))
    st2 = new_state(L(*pile))
    for id in [1, 4, 7, 10, 13]:
        st2.take(id)
    assert not st2.over
    assert st2.shelf.count(None) == 4
    # simulate booster before the jam happens
    st2.add_slots(3)
    assert len(st2.shelf) == 12


def test_replay_stops_at_game_end():
    outcome, _ = replay(L(E(1, "a"), E(2, "a"), E(3, "a")), [1, 2, 3, 1])
    assert outcome == Outcome.WIN


def test_illegal_move_in_replay_raises():
    with pytest.raises(ValueError, match="illegal move"):
        replay(L(E(1, "a")), [99])
    with pytest.raises(ValueError, match="blocked"):
        replay(L(E(1, "a", 2), E(2, "b")), [1])
