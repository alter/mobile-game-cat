"""Task 30-levels-solver/10: win rate measured under partial information.

The 98/87/66 table (D2, reviews/2026-08-24-refactor-difficulty.md) was produced
before buried items hid their kind, and no script for it survives in the
repository. This one replaces both: it is the source of the published numbers,
and it re-runs.

Two policies play the same generated levels:

* `partial_policy` — what a real player has. Its signature is the proof: it
  receives the reachable items as `(id, kind)` pairs and the shelf, and nothing
  else. It cannot see a buried item's kind, how deep a copy lies, or how many
  copies remain, because none of that is passed in.
* `oracle_policy` — the old full-information model, kept only as the baseline
  the new table is compared against. It additionally receives, for every kind,
  how much digging the remaining copies would cost.

Neither policy is the solver. `solve()` stays a feasibility oracle: it answers
"a solution exists", which says nothing about how often a person finds one.

Usage:
    python -m tools.solver.measure                  # bands, 400 games each
    python -m tools.solver.measure --shipped        # the 37 levels as shipped
"""
from __future__ import annotations

import argparse
import json
import random
from pathlib import Path

from .generate import generate_level, items_for_room
from .rules import Outcome, new_state
from .schema import LevelDef, load_level
from .solver import solve

BANDS = ((36, 1), (48, 5), (60, 9))  # pile size -> a room in that band
GAMES_PER_BAND = 400
POLICIES = ("shelf_only", "partial", "oracle")


# --------------------------------------------------------------------------
# policies
# --------------------------------------------------------------------------

def _best(choices: list[tuple[int, str]], score) -> list[int]:
    """Every id that scores highest — the caller breaks the tie.

    Ties are not a detail: with a wide-open pile most moves are tied, and
    resolving them by item id instead of at random turns a 79% win rate into
    100%. A player cannot see item ids, so the tie must go to chance.
    """
    best = max(score(c) for c in choices)
    return [c[0] for c in choices if score(c) == best]


def shelf_only_policy(choices: list[tuple[int, str]],
                      shelf: tuple[str | None, ...]) -> list[int]:
    """The policy behind the retired 98/87/66 table: "prefers kinds already
    2-of-3 on the shelf" (04-difficulty-curve/task.txt) and nothing more.

    Reproduced here because a new number is only comparable to an old one when
    the same player produced both. It sees no more than `partial_policy` does —
    it simply thinks less.
    """
    on_shelf: dict[str, int] = {}
    for kind in shelf:
        if kind is not None:
            on_shelf[kind] = on_shelf.get(kind, 0) + 1

    def score(choice: tuple[int, str]) -> tuple[int, int]:
        _, kind = choice
        held = on_shelf.get(kind, 0)
        return (1 if held == 2 else 0, held)

    return _best(choices, score)


def partial_policy(choices: list[tuple[int, str]],
                   shelf: tuple[str | None, ...]) -> list[int]:
    """Pick the equally-best item ids, seeing only what the player sees.

    1. Finish a triple whenever one can be finished.
    2. Otherwise prefer the kind already on the shelf, then the kind with most
       copies among the reachable items — filling the shelf with singletons is
       what jams it.
    3. Ties break on id, so a run is reproducible.
    """
    on_shelf: dict[str, int] = {}
    for kind in shelf:
        if kind is not None:
            on_shelf[kind] = on_shelf.get(kind, 0) + 1

    reachable: dict[str, int] = {}
    for _, kind in choices:
        reachable[kind] = reachable.get(kind, 0) + 1

    def score(choice: tuple[int, str]) -> tuple[int, int, int]:
        _, kind = choice
        held = on_shelf.get(kind, 0)
        completes = 1 if held == 2 else 0
        return (completes, held, reachable[kind])

    return _best(choices, score)


def oracle_policy(choices: list[tuple[int, str]],
                  shelf: tuple[str | None, ...],
                  dig_cost: dict[str, int]) -> list[int]:
    """The pre-hiding model: everything `partial_policy` does, plus the pile.

    Strictly a superset — same first three criteria, then `dig_cost[kind]`
    breaks the ties: how many items still have to be removed before the
    remaining copies of that kind can be reached. That lookahead is exactly
    what hiding takes away, so the gap between the two policies is the price of
    hiding and nothing else. Making the oracle a *different* heuristic rather
    than a better-informed one would compare two guesses, not two states of
    knowledge.
    """
    on_shelf: dict[str, int] = {}
    for kind in shelf:
        if kind is not None:
            on_shelf[kind] = on_shelf.get(kind, 0) + 1

    reachable: dict[str, int] = {}
    for _, kind in choices:
        reachable[kind] = reachable.get(kind, 0) + 1

    def score(choice: tuple[int, str]) -> tuple[int, int, int, int]:
        _, kind = choice
        held = on_shelf.get(kind, 0)
        completes = 1 if held == 2 else 0
        return (completes, held, reachable[kind], -dig_cost.get(kind, 0))

    return _best(choices, score)


# --------------------------------------------------------------------------
# playing
# --------------------------------------------------------------------------

def _dig_cost(state) -> dict[str, int]:
    """Per kind: how many items block its untaken copies (transitively)."""
    by_id = state.level.by_id()
    memo: dict[int, set[int]] = {}

    def blockers(item_id: int) -> set[int]:
        if item_id in memo:
            return memo[item_id]
        memo[item_id] = set()          # guards against a malformed cycle
        found: set[int] = set()
        for ref in by_id[item_id].blocked_by:
            if ref not in state.taken:
                found.add(ref)
                found |= blockers(ref)
        memo[item_id] = found
        return found

    cost: dict[str, int] = {}
    for item in state.level.pile:
        if item.id in state.taken:
            continue
        cost[item.kind] = cost.get(item.kind, 0) + len(blockers(item.id))
    return cost


def play(level: LevelDef, policy: str, rng: random.Random,
         mistake_rate: float = 0.0) -> Outcome:
    """Play one game to the end; returns the outcome."""
    state = new_state(level)
    while not state.over:
        available = state.available()
        if not available:
            break                      # rules.take ends the game itself
        choices = [(item.id, item.kind) for item in available]
        if rng.random() < mistake_rate:
            best = [c[0] for c in choices]          # a careless tap
        elif policy == "oracle":
            best = oracle_policy(choices, tuple(state.shelf), _dig_cost(state))
        elif policy == "shelf_only":
            best = shelf_only_policy(choices, tuple(state.shelf))
        else:
            best = partial_policy(choices, tuple(state.shelf))
        state.take(rng.choice(best))
    return state.outcome


def open_front(level: LevelDef, rng: random.Random) -> float:
    """Mean number of items reachable at once over one random play.

    The measure explains the tables below: choices are made among reachable
    items, whose kinds are always visible, so hiding can only matter for what
    is planned beyond them. When a quarter of the pile is open at every moment,
    there is little left to plan.
    """
    state = new_state(level)
    counts = []
    while not state.over:
        available = state.available()
        if not available:
            break
        counts.append(len(available))
        state.take(rng.choice(available).id)
    return sum(counts) / len(counts) if counts else 0.0


def _band_levels(rng: random.Random, items: int, room: int,
                 count: int) -> list[LevelDef]:
    levels = []
    while len(levels) < count:
        level = generate_level(rng, number=room, item_count=items,
                               room_id=f"room_{room:02d}")
        if solve(level) is not None:   # only levels a player could win
            levels.append(level)
    return levels


def measure_bands(games: int = GAMES_PER_BAND, seed: int = 20260826,
                  mistake_rate: float = 0.0) -> list[dict]:
    rows = []
    for items, room in BANDS:
        rng = random.Random(seed + items)
        levels = _band_levels(rng, items, room, games)
        front_rng = random.Random(seed)
        row = {"items": items, "room_band": f"{room}–{room + 3}", "games": games,
               "open_front": round(
                   sum(open_front(l, front_rng) for l in levels) / len(levels), 1)}
        for policy in POLICIES:
            play_rng = random.Random(seed + items + 1)
            wins = sum(
                1 for level in levels
                if play(level, policy, play_rng, mistake_rate) is Outcome.WIN)
            row[policy] = round(100 * wins / games, 1)
        rows.append(row)
    return rows


def measure_shipped(levels_dir: str, repeats: int = 1, seed: int = 20260826,
                    mistake_rate: float = 0.0) -> list[dict]:
    rows = []
    for path in sorted(Path(levels_dir).glob("*.json")):
        level = load_level(str(path))
        row = {"file": path.stem, "items": len(level.pile)}
        for policy in POLICIES:
            rng = random.Random(seed)
            wins = sum(1 for _ in range(repeats)
                       if play(level, policy, rng, mistake_rate) is Outcome.WIN)
            row[policy] = round(100 * wins / repeats, 1)
        rows.append(row)
    return rows


def _table(rows: list[dict], mistake_rate: float) -> str:
    out = ["| pile size | rooms | open at once | shelf-only | reachable-aware, "
           "pile hidden | reachable-aware, pile visible | price of hiding |",
           "|---|---|---|---|---|---|---|"]
    for r in rows:
        out.append(f"| {r['items']} | {r['room_band']} | {r['open_front']} | "
                   f"{r['shelf_only']}% | {r['partial']}% | {r['oracle']}% | "
                   f"{round(r['oracle'] - r['partial'], 1)} pp |")
    out.append("")
    out.append(f"{rows[0]['games']} games per band, mistake rate "
               f"{round(100 * mistake_rate)}%, "
               f"`python -m tools.solver.measure`.")
    return "\n".join(out)


def main() -> None:
    ap = argparse.ArgumentParser(description="Win rate under partial information")
    ap.add_argument("--games", type=int, default=GAMES_PER_BAND)
    ap.add_argument("--seed", type=int, default=20260826)
    ap.add_argument("--mistakes", type=float, default=0.0,
                    help="probability of a random pick instead of the best one")
    ap.add_argument("--shipped", metavar="DIR", nargs="?",
                    const="game/Assets/Resources/Levels",
                    help="measure the shipped levels instead of generated bands")
    ap.add_argument("--repeats", type=int, default=200,
                    help="games per shipped level")
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args()

    if args.shipped:
        # Repeats matter even with no mistakes: ties are broken at random, so a
        # single game is one sample, not the level's win rate.
        rows = measure_shipped(args.shipped, args.repeats, args.seed, args.mistakes)
        print(json.dumps(rows, indent=2) if args.json else
              "\n".join(f"{r['file']:<24} {r['items']:>3} items  "
                        f"shelf-only {r['shelf_only']:>5}%  "
                        f"partial {r['partial']:>5}%  oracle {r['oracle']:>5}%"
                        for r in rows))
        return

    rows = measure_bands(args.games, args.seed, args.mistakes)
    print(json.dumps(rows, indent=2) if args.json else _table(rows, args.mistakes))


if __name__ == "__main__":
    main()
