"""Rules engine mirror of game/Assets/Core/Board.cs — kept in lockstep.

The conformance test (tools/tests/conformance_test.py) is the guard: same
level + same move order must produce the same outcome on both sides.
"""
from __future__ import annotations

import enum
from dataclasses import dataclass, field

from .schema import LevelDef, PileItem


class Outcome(enum.Enum):
    WIN = "win"
    OUT_OF_MOVES = "out_of_moves"
    SHELF_JAMMED = "shelf_jammed"


SLOTS_PER_ROW = 3
CAPACITY = SLOTS_PER_ROW * 3  # nine places


@dataclass
class RulesState:
    """Mutable replay state. `taken` drives availability, `shelf` mirrors Shelf."""

    level: LevelDef
    moves_left: int
    taken: set[int] = field(default_factory=set)
    shelf: list[str] = field(default_factory=lambda: [None] * CAPACITY)  # kind per slot
    over: bool = False
    outcome: Outcome | None = None

    def available(self) -> list[PileItem]:
        by_id = self.level.by_id()
        return [
            item for item in self.level.pile
            if item.id not in self.taken
            and all(b in self.taken for b in item.blocked_by)
        ]

    def _place_and_match(self, kind: str) -> None:
        try:
            slot = self.shelf.index(None)
        except ValueError:
            raise AssertionError("unreachable: jam handled before placement")
        self.shelf[slot] = kind

        # A completely full shelf with nothing matched is a jam.
        if None not in self.shelf:
            if not self._try_match():
                self.over = True
                self.outcome = Outcome.SHELF_JAMMED
                return

        self._try_match()

    def _try_match(self) -> bool:
        counts: dict[str, int] = {}
        for kind in self.shelf:
            if kind is not None:
                counts[kind] = counts.get(kind, 0) + 1
        for kind, n in counts.items():
            if n >= 3:
                removed = 0
                for i in range(CAPACITY):
                    if self.shelf[i] == kind and removed < 3:
                        self.shelf[i] = None
                        removed += 1
                return True
        return False

    def take(self, item_id: int) -> bool:
        """Mirror of Board.TakeItem."""
        if self.over:
            return False
        by_id = self.level.by_id()
        if item_id not in by_id or item_id in self.taken:
            raise ValueError(f"illegal move {item_id}: unknown or already taken")
        item = by_id[item_id]
        if not all(b in self.taken for b in item.blocked_by):
            raise ValueError(f"illegal move {item_id}: blocked")

        self.taken.add(item_id)
        self._place_and_match(item.kind)
        if self.over:
            return True

        if len(self.taken) == len(self.level.pile):
            self.over = True
            self.outcome = Outcome.WIN
            return True

        self.moves_left -= 1
        if self.moves_left <= 0:
            self.over = True
            self.outcome = Outcome.OUT_OF_MOVES
        return True


def new_state(level: LevelDef) -> RulesState:
    return RulesState(level=level, moves_left=level.moves_limit)


def replay(level: LevelDef, order: list[int]) -> tuple[Outcome | None, int]:
    """Play a move order; returns (outcome, moves_left_at_end).

    Raises ValueError if a move in the order is illegal after the game
    already ended (a solution script must never contain such moves).
    """
    state = new_state(level)
    for item_id in order:
        if not state.take(item_id):
            raise ValueError(
                f"illegal move {item_id}"
                + (" (game already over)" if state.over else ""))
        if state.over:
            break
    return state.outcome, state.moves_left
