"""Task 30-levels-solver/05: the shipping script produces the 37-level plan.

These are that task's VERIFY items, made runnable. They used to be checked by
eye against the files in game/Assets/Resources/Levels/, which is not the same
thing: the script shipped twelve level_NN.json files and could not have
produced what the game reads.
"""
import collections
import json
from pathlib import Path

from tools.solver.pacing import PILES_PER_ROOM, TOTAL_LEVELS
from tools.solver.schema import load_level, validate
from tools.solver.ship_levels import ship
from tools.solver.solver import solve

SHIPPED = Path(__file__).resolve().parents[2] / "game/Assets/Resources/Levels"


def _shipped(tmp_path):
    ship(str(tmp_path), seed=7)
    return sorted(tmp_path.glob("*.json"))


def test_produces_exactly_thirty_seven_files(tmp_path):
    assert len(_shipped(tmp_path)) == TOTAL_LEVELS == 37


def test_room_and_pile_pairs_are_unique_and_match_the_pacing_curve(tmp_path):
    per_room = collections.Counter()
    seen = set()
    for path in _shipped(tmp_path):
        level = load_level(str(path))
        room = int(level.room_id.split("_")[1])
        pair = (level.room_id, level.pile_index)
        assert pair not in seen, f"duplicate {pair}"
        seen.add(pair)
        per_room[room] += 1
    assert [per_room[r] for r in range(1, 13)] == list(PILES_PER_ROOM)


def test_every_shipped_level_is_valid_and_solvable(tmp_path):
    for path in _shipped(tmp_path):
        level = load_level(str(path))
        validate(level)
        assert solve(level) is not None, f"{path.name} has no solution"


def test_pile_size_band_follows_the_room(tmp_path):
    for path in _shipped(tmp_path):
        level = load_level(str(path))
        room = int(level.room_id.split("_")[1])
        expected = 36 if room <= 4 else 48 if room <= 8 else 60
        assert len(level.pile) == expected, path.name


def test_locked_kinds_appear_only_from_room_nine(tmp_path):
    for path in _shipped(tmp_path):
        level = load_level(str(path))
        room = int(level.room_id.split("_")[1])
        locked = [i for i in level.pile if i.locked_after_triples]
        if room < 9:
            assert not locked, f"{path.name} locks a kind too early"
        else:
            assert locked, f"{path.name} carries no complication"
            kinds = {i.kind for i in locked}
            assert len(kinds) == 1, "one locked kind per level"
            assert len(locked) % 3 == 0, "locked kinds come in triples"


def test_filenames_match_what_the_player_loads(tmp_path):
    # View/LevelAssets.cs builds "l{seq:00}_room{room:00}_pile{index}".
    produced = {p.name for p in _shipped(tmp_path)}
    on_disk = {p.name for p in SHIPPED.glob("*.json")}
    assert produced == on_disk
