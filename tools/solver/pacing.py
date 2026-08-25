"""Pacing curve (task 3.12): piles per room, and the 37-level room map.

Rooms hold 1, 2, 3... piles; pile size governs difficulty, piles-per-room
governs pacing. The two levers stay separate.
"""
from __future__ import annotations

# Room number -> number of piles in that room (task 3.12).
PILES_PER_ROOM: tuple[int, ...] = (1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4)

TOTAL_LEVELS = sum(PILES_PER_ROOM)  # 37


def piles_for_room(room_number: int) -> int:
    """1-based room number -> pile count."""
    if not 1 <= room_number <= len(PILES_PER_ROOM):
        raise ValueError(f"room {room_number} out of range")
    return PILES_PER_ROOM[room_number - 1]


def level_map() -> list[tuple[int, int]]:
    """The full 37-level plan: [(room_number, pile_index), ...] in play order."""
    plan: list[tuple[int, int]] = []
    for room, count in enumerate(PILES_PER_ROOM, start=1):
        for pile_index in range(count):
            plan.append((room, pile_index))
    return plan
