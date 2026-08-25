"""Solver: is a level solvable, and in how many moves at sensible play?

Depth-first search with failure memoization. "Sensible play" = the heuristic
order (kinds closest to a triple first); the returned move count is what that
policy achieves.

Locked items (task 3.11): an item with locked_after_triples > 0 is not takeable
until that many triples have been completed, so the search state must track the
triple count alongside taken/shelf.
"""
from __future__ import annotations

import time
from dataclasses import dataclass

from .rules import DEFAULT_CAPACITY
from .schema import LevelDef


@dataclass(frozen=True)
class Solution:
    moves: tuple[int, ...]
    move_count: int


def _counts_of(shelf) -> dict[str, int]:
    counts: dict[str, int] = {}
    for kind in shelf:
        if kind is not None:
            counts[kind] = counts.get(kind, 0) + 1
    return counts


class _Search:
    def __init__(self, level: LevelDef, deadline: float,
                 shelf_capacity: int) -> None:
        self.pile = level.pile
        self.total = len(level.pile)
        self.deadline = deadline
        self.capacity = shelf_capacity
        self.failed: set[tuple] = set()

    @staticmethod
    def _shelf_key(shelf) -> tuple:
        # Only multiset of kinds matters for the future, not slot order.
        kinds = sorted(k for k in shelf if k is not None)
        return (tuple(kinds), shelf.count(None))

    def run(self) -> Solution | None:
        shelf: list[str | None] = [None] * self.capacity
        path: list[int] = []
        taken: set[int] = set()
        triples = 0
        if self._dfs(taken, shelf, path, triples):
            return Solution(moves=tuple(path), move_count=len(path))
        return None

    def _dfs(self, taken: set[int], shelf, path: list[int],
             triples: int) -> bool:
        if time.monotonic() > self.deadline:
            raise TimeoutError("solver deadline exceeded")

        key = (frozenset(taken), self._shelf_key(shelf), min(triples, 30))
        if key in self.failed:
            return False

        avail = [
            item for item in self.pile
            if item.id not in taken
            and all(b in taken for b in item.blocked_by)
            and not (item.locked_after_triples > 0
                     and triples < item.locked_after_triples)
        ]
        sc = _counts_of(shelf)
        avail.sort(key=lambda it: -sc.get(it.kind, 0))

        for item in avail:
            new_shelf = shelf.copy()
            try:
                slot = new_shelf.index(None)
            except ValueError:
                break  # shelf somehow full without match — dead branch
            new_shelf[slot] = item.kind

            jammed = False
            new_triples = triples
            matched = self._match(new_shelf)
            if matched:
                new_triples += 1
            elif None not in new_shelf:
                jammed = True

            if jammed:
                continue

            path.append(item.id)
            taken.add(item.id)

            if len(taken) == self.total:
                return True  # pile cleared → win (checked before any jam)

            if self._dfs(taken, new_shelf, path, new_triples):
                return True

            taken.discard(item.id)
            path.pop()

        self.failed.add(key)
        return False

    @staticmethod
    def _match(shelf) -> bool:
        counts: dict[str, int] = {}
        for kind in shelf:
            if kind is not None:
                counts[kind] = counts.get(kind, 0) + 1
        for kind, n in counts.items():
            if n >= 3:
                removed = 0
                for i in range(len(shelf)):
                    if shelf[i] == kind and removed < 3:
                        shelf[i] = None
                        removed += 1
                return True
        return False


def solve(level: LevelDef, state_cap: int = 500_000,
          time_cap_s: float = 5.0,
          shelf_capacity: int = DEFAULT_CAPACITY) -> Solution | None:
    """Return a solution under sensible play, or None if unsolvable/too hard."""
    search = _Search(level, deadline=time.monotonic() + time_cap_s,
                     shelf_capacity=shelf_capacity)
    try:
        return search.run()
    except TimeoutError:
        return None
