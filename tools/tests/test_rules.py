"""Rules mirror tests — kept in lockstep with game/Tests/Core/BoardTests.cs."""
import pytest

from tools.solver.rules import DEFAULT_CAPACITY, Outcome, new_state, replay
from tools.solver.schema import LevelDef, LevelValidationError, PileItem, validate


def L(*pile):
    """Build a level, padding each kind group to a multiple of three."""
    out = list(pile)
    next_id = 100
    for kind in {e.kind for e in out}:
        n = sum(1 for e in out if e.kind == kind)
        deficit = (3 - n % 3) % 3
        for _ in range(deficit):
            out.append(PileItem(next_id, kind))
            next_id += 1
    return LevelDef(number=7, room_id="room_1", pile_index=0, pile=tuple(out))


def E(i, kind, *blocked):
    return PileItem(i, kind, tuple(blocked))


def Locked(i, kind, unlock_after):
    return PileItem(i, kind, (), unlock_after)


# ---- GetAvailable --------------------------------------------------------

def test_empty_pile_no_available():
    assert new_state(L()).available() == []


def test_single_layer_all_available():
    st = new_state(L(E(1, "a"), E(2, "b")))
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


def test_duplicate_item_ids_rejected_at_validation():
    from tools.solver.schema import validate
    with pytest.raises(LevelValidationError):
        validate(L(E(1, "a"), E(1, "a"), E(1, "b")))


# ---- Triple validation ----------------------------------------------------

def test_kind_not_in_triples_rejected():
    with pytest.raises(ValueError, match="multiple of three"):
        new_state(LevelDef(1, "r", 0, (E(1, "a"), E(2, "a"))))


# ---- Shelf ----------------------------------------------------------------

def test_place_fills_leftmost_free_slot():
    st = new_state(L(E(1, "a"), E(2, "b")))
    st.take(1)
    st.take(2)
    assert st.shelf[0] == "a" and st.shelf[1] == "b"


def test_match_across_row_boundary():
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
    st = new_state(L(E(1, "a"), E(2, "a")))
    st.take(1)
    st.take(2)
    assert st.shelf.count("a") == 2


# ---- Outcomes -------------------------------------------------------------

def test_win_checked_before_jam():
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
    order = [k * 3 + i + 1 for i in range(3) for k in range(5)]
    outcome, _ = replay(L(*pile), order)
    assert outcome == Outcome.SHELF_JAMMED


def test_add_slots_booster_grows_capacity():
    pile = []
    for k in range(5):
        for i in range(3):
            pile.append(E(k * 3 + i + 1, f"k{k}"))
    st = new_state(L(*pile))
    for id in [k * 3 + 1 for k in range(5)]:
        st.take(id)
    st.add_slots(3)
    assert len(st.shelf) == 12


def test_replay_stops_at_game_end():
    outcome, _ = replay(L(E(1, "a"), E(2, "a"), E(3, "a")), [1, 2, 3, 1])
    assert outcome == Outcome.WIN


def test_illegal_move_in_replay_raises():
    with pytest.raises(ValueError, match="illegal move"):
        replay(L(E(1, "a")), [99])
    with pytest.raises(ValueError, match="blocked"):
        replay(L(E(1, "a", 2), E(2, "b")), [1])


# ---- 3.9 hidden kinds -------------------------------------------------------

def test_buried_kind_hidden_until_reachable():
    st = new_state(L(E(1, "a", 2), E(2, "b")))
    by_id = st.level.by_id()
    assert st.is_revealed(by_id[1]) is False
    assert st.is_revealed(by_id[2]) is True
    st.take(2)
    assert st.is_revealed(by_id[1]) is True


def test_available_items_always_revealed():
    st = new_state(L(E(1, "a"), E(2, "b")))
    for item in st.available():
        assert st.is_revealed(item)


# ---- 3.11 locked items ------------------------------------------------------

def test_locked_item_not_available_until_threshold():
    st = new_state(L(Locked(1, "x", 1),
                     E(10, "a"), E(11, "a"), E(12, "a")))
    assert all(i.id != 1 for i in st.available())

    for id in (10, 11, 12):
        st.take(id)  # first triple completes

    assert st.triples_completed == 1
    assert any(i.id == 1 for i in st.available())


def test_locked_item_cannot_be_taken_even_if_asked():
    st = new_state(L(Locked(1, "x", 1),
                     E(10, "a"), E(11, "a"), E(12, "a")))
    with pytest.raises(ValueError, match="locked"):
        st.take(1)


def test_locked_item_is_not_revealed():
    st = new_state(L(Locked(1, "x", 5),
                     E(10, "a"), E(11, "a"), E(12, "a")))
    by_id = st.level.by_id()
    assert st.is_revealed(by_id[1]) is False
