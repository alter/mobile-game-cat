"""Solver: is a level solvable, and in how many moves at sensible play?

Depth-first search with failure memoization. "Sensible play" = the heuristic
order (kinds closest to a triple first); the returned move count is what that
policy achieves, which is exactly what task 3.4's slack curve is built on.
"""
from __future__ import annotations

import sys
import time
from dataclasses import dataclass

from .rules import CAPACITY
from .schema import LevelDef


@dataclass(frozen=True)
class Solution:
    moves: tuple[int, ...]
    move_count: int


def _counts_of(shelf: list[str | None]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for kind in shelf:
        if kind is not None:
            counts[kind] = counts.get(kind, 0) + 1
    return counts


class _Search:
    def __init__(self, level: LevelDef,
                 deadline: float) -> None:
        self.by_id = level.by_id()
        self.pile = level.pile
        self.total = len(level.pile)
        self.moves_limit = level.moves_limit
        self.deadline = deadline
        self.failed: set[tuple] = set()  # (taken frozenset, sorted shelf kinds)

    @staticmethod
    def _shelf_key(shelf: list[str | None]) -> tuple:
        # Only multiset of kinds matters for the future, not slot order.
        kinds = sorted(k for k in shelf if k is not None)
        return (tuple(kinds), shelf.count(None))

    def run(self) -> Solution | None:
        shelf: list[str | None] = [None] * CAPACITY
        path: list[int] = []
        taken: set[int] = set()
        if self._dfs(taken, shelf, path):
            return Solution(moves=tuple(path), move_count=len(path))
        return None

    def _available(self, taken: set[int]) -> list:
        counts = _counts_of_from(taken)
        avail = [
            item for item in self.pile
            if item.id not in taken
            and all(b in taken for b in item.blocked_by)
        ]
        # prefer items whose kind already has 2 on the shelf, then 1
        shelf_counts = self._shelf_counts
        avail.sort(key=lambda it: -shelf_counts.get(it.kind, 0))
        return avail

    _shelf_counts: dict[str, int]

    def _dfs(self, taken: set[int], shelf: list[str | None],
             path: list[int]) -> bool:
        if time.monotonic() > self.deadline:
            raise TimeoutError("solver deadline exceeded")

        key = (frozenset(taken), self._shelf_key(shelf))
        if key in self.failed:
            return False

        avail = [
            item for item in self.pile
            if item.id not in taken
            and all(b in taken for b in item.blocked_by)
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
            if None not in new_shelf and all(
                    n < 3 for n in _counts_of(new_shelf).values()):
                jammed = True

            c = _counts_of(new_shelf)
            for kind, n in c.items():
                if n >= 3:
                    removed = 0
                    for i in range(CAPACITY):
                        if new_shelf[i] == kind and removed < 3:
                            new_shelf[i] = None
                            removed += 1

            if jammed:
                continue

            path.append(item.id)
            taken.add(item.id)

            if len(taken) == self.total:
                return True  # pile cleared → win

            if len(path) < self.moves_limit and self._dfs(taken, new_shelf, path):
                return True

            taken.discard(item.id)
            path.pop()

        self.failed.add(key)
        return False


def _counts_of_from(taken: set[int]) -> dict[str, int]:
    return {}  # unused helper kept out; see _dfs


def solve(level: LevelDef, state_cap: int = 500_000,
          time_cap_s: float = 10.0) -> Solution | None:
    """Return a solution under sensible play, or None if unsolvable/too hard."""
    search = _Search(level, deadline=time.monotonic() + min(time_cap_s, 5.0))
    search._shelf_counts = {}
    try:
        result = search.run()
    except TimeoutError:
        return None
    if len(search.failed) > state_cap:
        return None
    return result


def check_dead_end_by_outcome(level: LevelDef, order: list[int]):
    """Replay helper for tests."""
    from .rules import replay, Outcome
    outcome, _ = replay(level, order)
    assert outcome is not None
    return outcome
