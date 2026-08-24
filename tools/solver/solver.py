"""Solver: is a level solvable, and in how many moves?

BFS over (frozenset taken, shelf tuple) states with heuristic move ordering.
Mirrors rules.py semantics exactly.
"""
from __future__ import annotations

import time
from dataclasses import dataclass

from .rules import CAPACITY, Outcome
from .schema import LevelDef


@dataclass(frozen=True)
class Solution:
    moves: tuple[int, ...]
    move_count: int


def _kind_counts(level: LevelDef) -> dict[str, int]:
    counts: dict[str, int] = {}
    for item in level.pile:
        counts[item.kind] = counts.get(item.kind, 0) + 1
    return counts


def _shelf_key(shelf: list[str | None]) -> tuple:
    # Order of identical kinds on the shelf doesn't matter for the outcome;
    # sorting collapses equivalent shelf arrangements.
    return tuple(sorted(k for k in shelf if k is not None)) + (
        (None,) * shelf.count(None))


def solve(level: LevelDef, state_cap: int = 200_000,
          time_cap_s: float = 2.0) -> Solution | None:
    """Return the shortest solution or None if unsolvable / caps exceeded."""
    by_id = level.by_id()
    start_shelf = [None] * CAPACITY
    start = (frozenset(), _shelf_key(start_shelf))
    # queue entries: (taken frozenset, shelf list, path tuple)
    from collections import deque
    queue: deque[tuple[frozenset, list, tuple[int, ...]]] = deque()
    queue.append((frozenset(), start_shelf, ()))
    visited: set = {start}
    deadline = time.monotonic() + time_cap_s
    total = len(level.pile)

    while queue:
        if len(visited) > state_cap or time.monotonic() > deadline:
            return None

        taken, shelf, path = queue.popleft()

        # available items with preference: kinds closest to completing first
        avail = [
            item for item in level.pile
            if item.id not in taken
            and all(b in taken for b in item.blocked_by)
        ]
        counts: dict[str, int] = {}
        for kind in shelf:
            if kind is not None:
                counts[kind] = counts.get(kind, 0) + 1
        avail.sort(key=lambda it: -counts.get(it.kind, 0))

        for item in avail:
            new_taken = taken | {item.id}
            new_shelf = shelf.copy()
            try:
                slot = new_shelf.index(None)
            except ValueError:
                break  # shelf full and nothing matched earlier — dead branch
            new_shelf[slot] = item.kind

            jammed = False
            # full shelf without a match?
            if None not in new_shelf:
                has_match = any(
                    n >= 3 for n in
                    _counts_of(new_shelf).values())
                if not has_match:
                    jammed = True

            # match triples (mirror rules._try_match)
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

            new_path = path + (item.id,)
            if len(new_path) == total:
                # pile cleared → win (winning take consumes no move)
                return Solution(moves=new_path, move_count=len(new_path))
            if len(new_path) >= level.moves_limit:
                continue  # would run out of moves before clearing

            key = (new_taken, _shelf_key(new_shelf))
            if key in visited:
                continue
            visited.add(key)
            queue.append((new_taken, new_shelf, new_path))

    return None


def _counts_of(shelf: list[str | None]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for kind in shelf:
        if kind is not None:
            counts[kind] = counts.get(kind, 0) + 1
    return counts


def check_dead_end_by_outcome(level: LevelDef, order: list[int]) -> Outcome:
    """Replay helper for tests."""
    from .rules import replay
    outcome, _ = replay(level, order)
    assert outcome is not None
    return outcome
