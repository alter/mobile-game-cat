"""Rules engine mirror of game/Assets/Core/Board.cs — kept in lockstep.

The conformance test (tools/tests/conformance_test.py) is the guard: same
level + same move order must produce the same outcome on both sides.

No move limit: winning means taking every item, so a limit either blocks the
level or can never be reached (reviews/2026-08-24-refactor-difficulty.md).

Partial information (3.9): a buried item's kind is hidden until reachable.
Locked items (3.11): unreachable until `locked_after_triples` triples done.
"""
from __future__ import annotations

import enum
from dataclasses import dataclass, field

from .schema import LevelDef, PileItem


class Outcome(enum.Enum):
    WIN = "win"
    SHELF_JAMMED = "shelf_jammed"


SLOTS_PER_ROW = 3
DEFAULT_CAPACITY = SLOTS_PER_ROW * 3  # nine places


class RulesState:
    """Mutable replay state. `taken` drives availability, `shelf` mirrors Shelf."""

    def __init__(self, level: LevelDef, shelf_capacity: int = DEFAULT_CAPACITY):
        counts: dict[str, int] = {}
        for item in level.pile:
            counts[item.kind] = counts.get(item.kind, 0) + 1
        for kind, n in counts.items():
            if n % 3 != 0:
                raise ValueError(
                    f"kind {kind!r} appears {n} times, not a multiple of three")
        self.level = level
        self.capacity = shelf_capacity
        self.taken: set[int] = set()
        self.shelf: list[str | None] = [None] * shelf_capacity
        self.triples_completed = 0
        self.over = False
        self.outcome: Outcome | None = None

    def is_locked(self, item: PileItem) -> bool:
        return item.locked_after_triples > 0 \
            and self.triples_completed < item.locked_after_triples

    def available(self) -> list[PileItem]:
        by_id = self.level.by_id()
        return [
            item for item in self.level.pile
            if item.id not in self.taken
            and all(b in self.taken for b in item.blocked_by)
            and not self.is_locked(item)
        ]

    def is_revealed(self, item: PileItem) -> bool:
        """Task 3.9: kind visible only once reachable."""
        if item.id in self.taken:
            return False
        # Locked is not hidden - the player must see which kind is withheld.
        # Mirrors Board.IsRevealed; see tasks/DECISIONS.md D15.
        return all(b in self.taken for b in item.blocked_by)

    def _try_match(self) -> bool:
        counts: dict[str, int] = {}
        for kind in self.shelf:
            if kind is not None:
                counts[kind] = counts.get(kind, 0) + 1
        for kind, n in counts.items():
            if n >= 3:
                removed = 0
                for i in range(len(self.shelf)):
                    if self.shelf[i] == kind and removed < 3:
                        self.shelf[i] = None
                        removed += 1
                return True
        return False

    def take(self, item_id: int) -> bool:
        """Mirror of Board.TakeItem — win checked before the jam."""
        if self.over:
            return False
        by_id = self.level.by_id()
        if item_id not in by_id or item_id in self.taken:
            raise ValueError(f"illegal move {item_id}: unknown or already taken")
        item = by_id[item_id]
        if not all(b in self.taken for b in item.blocked_by):
            raise ValueError(f"illegal move {item_id}: blocked")
        if self.is_locked(item):
            raise ValueError(f"illegal move {item_id}: locked")

        self.taken.add(item_id)

        # The last item is placed like any other, and the triple it completes is
        # counted, before the win is declared. Returning early instead — as this
        # did — left the shelf holding two items on every win where C# left it
        # empty, and skipped the final triple in triples_completed. Same
        # outcome, so the conformance test never saw it.
        if None not in self.shelf:
            # shelf full: the win still wins, otherwise it's a jam
            self.over = True
            self.outcome = (Outcome.WIN
                            if len(self.taken) == len(self.level.pile)
                            else Outcome.SHELF_JAMMED)
            return True

        self.shelf[self.shelf.index(None)] = item.kind

        if self._try_match():
            self.triples_completed += 1

        # full-but-matched shelf with a pile remaining: nowhere for the next
        # item to go
        if None not in self.shelf and len(self.taken) != len(self.level.pile):
            self.over = True
            self.outcome = Outcome.SHELF_JAMMED
            return True

        if len(self.taken) == len(self.level.pile):
            self.over = True
            self.outcome = Outcome.WIN
            return True

        # Pile not empty, shelf not full, and yet nothing can be taken: every
        # remaining item is locked and there are not enough triples to open
        # them. One outcome with the full shelf — no move exists either way.
        if not self.over and not self.available():
            self.over = True
            self.outcome = Outcome.SHELF_JAMMED

        return True

    def add_slots(self, extra: int) -> None:
        """Mirror of Board.AddShelfSlots — the "one more shelf" booster.

        Resumes a jammed game, and only a jammed one: a win stays won, and a
        jam the extra room does not solve stays jammed. Used to reset `over`
        unconditionally, which C# never did — the two now agree.
        """
        if extra < 0:
            raise ValueError("extra must be >= 0")
        self.shelf.extend([None] * extra)
        self.capacity += extra
        if self.outcome is not Outcome.SHELF_JAMMED:
            return
        if None not in self.shelf or not self.available():
            return
        self.over = False
        self.outcome = None


def new_state(level: LevelDef, shelf_capacity: int = DEFAULT_CAPACITY) -> RulesState:
    return RulesState(level, shelf_capacity)


def replay(level: LevelDef, order: list[int],
           shelf_capacity: int = DEFAULT_CAPACITY) -> tuple[Outcome | None, int]:
    """Play a move order; returns (outcome, slots_used_at_end).

    Raises ValueError if a move in the order is illegal after the game
    already ended (a solution script must never contain such moves).
    """
    state = new_state(level, shelf_capacity)
    for item_id in order:
        if not state.take(item_id):
            raise ValueError(
                f"illegal move {item_id}"
                + (" (game already over)" if state.over else ""))
        if state.over:
            break
    return state.outcome, state.capacity - state.shelf.count(None)
