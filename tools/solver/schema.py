"""Shared level format: dataclasses, validation, load/save.

Contract with C# Core is documented in level_format.md.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field


@dataclass(frozen=True)
class PileItem:
    id: int
    kind: str
    blocked_by: tuple[int, ...] = field(default=())
    locked_after_triples: int = 0


@dataclass(frozen=True)
class LevelDef:
    number: int
    room_id: str
    pile_index: int
    pile: tuple[PileItem, ...]

    def by_id(self) -> dict[int, PileItem]:
        return {item.id: item for item in self.pile}


class LevelValidationError(ValueError):
    pass


def validate(level: LevelDef) -> None:
    if level.number < 1:
        raise LevelValidationError("number must be >= 1")
    if not level.room_id:
        raise LevelValidationError("room_id must be non-empty")
    if level.pile_index < 0:
        raise LevelValidationError("pile_index must be >= 0")

    ids = [item.id for item in level.pile]
    if len(ids) != len(set(ids)):
        raise LevelValidationError("duplicate item ids")
    known = set(ids)

    for item in level.pile:
        if not item.kind:
            raise LevelValidationError(f"item {item.id}: empty kind")
        for ref in item.blocked_by:
            if ref == item.id:
                raise LevelValidationError(f"item {item.id} blocks itself")
            if ref not in known:
                raise LevelValidationError(
                    f"item {item.id}: blocked_by {ref} does not exist")

    _check_acyclic(level)

    counts: dict[str, int] = {}
    for item in level.pile:
        counts[item.kind] = counts.get(item.kind, 0) + 1
    for kind, n in counts.items():
        if n % 3 != 0:
            raise LevelValidationError(
                f"kind {kind!r} appears {n} times, not a multiple of 3")


def _check_acyclic(level: LevelDef) -> None:
    # blocked_by edges point downward (blocker is above); a cycle would make
    # some item permanently unavailable.
    by_id = level.by_id()
    state: dict[int, int] = {}  # 0 visiting, 1 done

    def visit(item_id: int) -> None:
        mark = state.get(item_id)
        if mark == 0:
            raise LevelValidationError("cycle in blocked_by")
        if mark == 1:
            return
        state[item_id] = 0
        for ref in by_id[item_id].blocked_by:
            visit(ref)
        state[item_id] = 1

    for item in level.pile:
        visit(item.id)


def level_to_dict(level: LevelDef) -> dict:
    return {
        "number": level.number,
        "room_id": level.room_id,
        "pile_index": level.pile_index,
        "pile": [
            {"id": i.id, "kind": i.kind,
             "blocked_by": list(i.blocked_by),
             **({"locked_after_triples": i.locked_after_triples}
                if i.locked_after_triples else {})}
            for i in level.pile
        ],
    }


def level_from_dict(data: dict) -> LevelDef:
    try:
        return LevelDef(
            number=int(data["number"]),
            room_id=str(data["room_id"]),
            pile_index=int(data.get("pile_index", 0)),
            pile=tuple(
                PileItem(id=int(e["id"]), kind=str(e["kind"]),
                         blocked_by=tuple(int(x) for x in e.get("blocked_by", [])),
                         locked_after_triples=int(e.get("locked_after_triples", 0)))
                for e in data["pile"]
            ),
        )
    except (KeyError, TypeError, ValueError) as exc:
        raise LevelValidationError(f"malformed level: {exc}") from exc


def save_level(level: LevelDef, path: str) -> None:
    validate(level)
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(level_to_dict(level), fh, indent=2)
        fh.write("\n")


def load_level(path: str) -> LevelDef:
    with open(path, encoding="utf-8") as fh:
        level = level_from_dict(json.load(fh))
    validate(level)
    return level
