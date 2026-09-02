"""Task 30-levels-solver/05: the shipping script produces the 37-level plan.

These are that task's VERIFY items, made runnable. They used to be checked by
eye against the files in game/Assets/Resources/Levels/, which is not the same
thing: the script shipped twelve level_NN.json files and could not have
produced what the game reads.
"""
import collections
import hashlib
import json
from pathlib import Path

from tools.solver.pacing import PILES_PER_ROOM, TOTAL_LEVELS
from tools.solver.schema import load_level, validate
from tools.solver.ship_levels import ship
from tools.solver.solver import solve

SHIPPED = Path(__file__).resolve().parents[2] / "game/Assets/Resources/Levels"

# tasks/30-levels-solver/13-shipped-levels-drift: three levels were hand-edited
# by commit 8fa3651 ("Three levels were harder than their neighbours for one
# measurable reason each", 2026-08-28) to lower their measured loss rate — a
# kind swap on l32, a dropped blocked_by edge on l34, a locked_after_triples
# threshold change on l35. Those edits are deliberate design decisions with a
# measured rationale in the commit message, kept nowhere else in the
# repository (originals under tasks/30-levels-solver/level-originals/), and
# are not expressible as ship_levels.py/generate.py parameters — the script
# only knows random search under a seed, not "swap item 1's kind". So seed 7
# regeneration reproduces the PRE-edit content for exactly these three files
# and will keep disagreeing with disk forever; that disagreement is expected,
# not drift, and is pinned by hash below instead of by regeneration equality.
HAND_EDITED_SHA256 = {
    "l32_room11_pile2.json":
        "42bc4bae5a8d8ef38a305374743f80c01d6add194595a27db95377fd14048e1d",
    "l34_room12_pile0.json":
        "9a4f7e022b4414636329aad29cff1eb3ce4b80ec7a7004c711708983949399f2",
    "l35_room12_pile1.json":
        "3beaffcff1da8b38b0c4c636b0a0f569b344138886c66ddadaec2642edb0ad38",
}


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


def test_regeneration_matches_the_shipped_files_byte_for_byte(tmp_path):
    # Audited 2026-08-28 (tasks/30-levels-solver/05-ship-37-levels/VERIFY.md,
    # item 1): the diff was empty by hand that day but nothing enforced it
    # going forward, and by 2026-09-01 it had drifted on exactly the three
    # hand-edited files below. This is the enforcement that was missing.
    for path in _shipped(tmp_path):
        shipped_path = SHIPPED / path.name
        if path.name in HAND_EDITED_SHA256:
            digest = hashlib.sha256(shipped_path.read_bytes()).hexdigest()
            assert digest == HAND_EDITED_SHA256[path.name], (
                f"{path.name} is one of the deliberately hand-edited levels "
                "(tasks/30-levels-solver/13-shipped-levels-drift) and its "
                "content changed again without updating HAND_EDITED_SHA256 "
                "here to match")
            continue
        assert path.read_bytes() == shipped_path.read_bytes(), (
            f"{path.name} no longer matches seed 7 regeneration and is not "
            "in HAND_EDITED_SHA256 — either the generator drifted from the "
            "shipped files, or this is a new deliberate hand edit that needs "
            "recording above with its rationale and a hash")
