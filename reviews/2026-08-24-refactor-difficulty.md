# Restructuring: difficulty, the loss condition, and the offer on the loss screen

Date: 2026-08-24
Basis: the review `reviews/2026-08-24-m2-m3.md` and measurements on the shipped levels.
Affects: `game/Assets/Core`, `tools/solver`, `game/Assets/Levels`,
`cat-shelter-mvp.md`, `cat-shelter-tasks.md`.

---

## What came to light, and why restructuring is needed

Three facts, each obtained by running the code, not by reasoning.

**The move limit is unreachable.** In all twelve shipped levels the pile has
36 items, and the limit ranges from 44 to 38. A move removes exactly one
item, an item is taken once — meaning more than 36 moves can never be made.
The `OutOfMoves` outcome never occurs, and the move-budget slack curve from
8 to 2 affects nothing.

**There is no difficulty progression.** A run of 400 games per level using
the rules from `tools/solver/rules.py`:

| | random moves | sensible play |
|---|---|---|
| Level 1 | 23.5% wins | 99.5% |
| Level 12 | 11.5% wins | 99.2% |

"Sensible play" means a player who prefers the item that already has two on
the shelf. Level twelve is no harder than level one.

**The offer on the loss screen doesn't address the cause of the loss.** The
only reachable loss is a jammed shelf. The "+5 moves" button doesn't clear a
jammed shelf. The metric "tapped +5 moves > 15%" would measure willingness
to buy something useless — and the denominator is nearly empty, because a
sensible player doesn't lose.

---

## Difficulty map: what actually works

Measurement on generated levels, 200 per cell, sensible-player win rate.
This is the reference table for all decisions below — the numbers were
obtained with the existing generator and existing rules.

| shelf spots | 36 items (6 kinds) | 48 (8 kinds) | 60 (10 kinds) |
|---|---|---|---|
| **9** (current) | **98.0%** | 86.5% | 66.0% |
| 8 | 90.5% | 68.0% | 43.5% |
| 7 (genre standard) | 84.5% | 55.5% | 33.5% |
| 6 | 56.5% | 28.0% | 7.5% |

Pile size turned out to be a stronger lever than the number of spots.

---

## Decisions

### Decision 1. Remove the move limit entirely

Not "fix the numbers," but remove the entity.

The move limit is **incompatible** with the win condition. A win is "the
pile is fully cleared," each move removes one item, so a win requires
exactly as many moves as there are items. A limit smaller than the pile
size makes the level unbeatable; a larger one makes it unreachable. There
is no middle ground: no meaningful value exists for this parameter.

Confirmation from the genre: in the reference examples named in the concept
itself — "Sheep a Sheep," "Triple Match 3D," "Zen Match" — there is no move
counter. The only loss there is also a jam.

What gets removed: `Level.MovesLimit`, `Board.MovesLeft`,
`GameOutcome.OutOfMoves`, the `moves_limit` field in the level description,
the entire move-budget-slack calculation in `tools/solver/generate.py` and
`ship_levels.py`, and the corresponding tests.

Two outcomes remain: win and jam. This honestly reflects the game that's
already written.

### Decision 2. Difficulty is set by pile size

The shelf stays at nine spots. This is part of the visual identity ("three
shelves of three"), and there's no reason to change it: pile size moves
difficulty more strongly.

A twelve-level curve, per the table above:

| Levels | Items | Kinds | Expected win rate |
|---|---|---|---|
| 1–4 | 36 | 6 | about 98% |
| 5–8 | 48 | 8 | about 87% |
| 9–12 | 60 | 10 | about 66% |

The start is deliberately easy. The audience is women 30–55 playing during
breaks; the concept explicitly requires avoiding punishment and humiliation
on loss. The first four levels should teach and delight, not filter people
out. Losing appears around level nine, once the kitten has already moved
into its second state and attachment has formed.

The "30–60 items" range is already recorded in the concept — no new
entities are introduced.

### Decision 3. Generator: decouple the number of kinds from pile size

Currently in `tools/solver/generate.py`:

```python
kind_count = max(1, round(item_count / 3 / 2))  # ~2 triples per kind
```

The number of kinds rigidly follows pile size. There's nothing to tune —
this is exactly why all twelve levels are the same.

Make `kind_count` an explicit optional argument with the previous behavior
kept as the default:

```python
def generate_level(rng, number=1, item_count=30, kind_count=None, room_id=None):
    if kind_count is None:
        kind_count = max(1, round(item_count / 3 / 2))
```

Ten lines of work, and the levers become two instead of one.

### Decision 4. The offer on the loss screen — "+1 shelf spot"

The button should relieve the specific trouble that occurred. Only a jam
occurs.

Among the genre solutions — remove three items from the shelf, add a spot,
undo a move — we take **add a spot**: it's a direct antidote to a jam,
self-explanatory without needing an explanation, and in the core it's a
single integer (shelf capacity stops being a constant).

There's no payment in the MVP; the button stays a stub with a tap counter.
The label and meaning change, not the mechanism.

Consequences for measurement: the event `moves_button_tap` gets renamed. I
propose `booster_tap` — neutral to whatever the offer actually is, and it
will survive a change of offer. The fourth metric stops measuring
willingness to buy something useless.

### Decision 5. While we're at it, close out what the review found

Since the core is being opened up anyway:

- **Multiple-of-three.** Add a check to `Level`'s constructor: the count of
  each item kind is a multiple of three. Otherwise a win occurs on an
  emptied pile with items stuck on the shelf.
- **Order of win and jam.** In `Board.TakeItem` the jam check comes before
  the win check. Swap them: the pile being cleared is a win, even if the
  shelf happens to fill up at the same time.
- **`SlotsPerRow` and `RowCount`.** The match is searched across all nine
  spots; the rows don't affect anything. Either remove them or mark with a
  comment that this is display layout only. With decision 4 the capacity
  becomes mutable anyway — do it at the same time.
- **The comment in `Shelf.TryMatch`** promises the method clears every
  completed triple it finds, but the code returns control after the first.
  The behavior is correct; the text needs fixing.

---

## Order of work

Core → generator → levels → display → documents. Each step ends with green
tests.

**Step 1. Core.**
Remove the move limit and `OutOfMoves`; swap the order of the win and jam
checks; add the multiple-of-three check; make shelf capacity mutable.
Acceptance: C# tests green, coverage no lower than 90%, `check-core-purity.sh`
passes.

**Step 2. The rules mirror in Python.**
The same changes in `tools/solver/rules.py`. Acceptance: `pytest tools/tests`
green **with not a single manually set environment variable** — first fix
`<RollForward>Major</RollForward>` in
`build/solver-bridge/solver-bridge.csproj` (blocking item 1 from the review).

**Step 3. Conformance.**
Add cases with a jam to `conformance_test.py`, not just wins: cut the
solution off partway and finish greedily to the end. Acceptance: among the
cases there are both `win` and `shelf_jammed`, and both sides match. Until
this exists, task 3.1 isn't considered closed.

**Step 4. Generator.**
Explicit `kind_count`. Acceptance: a property test — for every generated
level the solver finds a solution; kinds are multiples of three.

**Step 5. Twelve levels regenerated.**
Per the curve from decision 2. Acceptance: every level is checked by the
solver; measuring with sensible play gives a win rate within 90–100% on
levels 1–4, 80–92% on 5–8, 55–75% on 9–12. The numbers are a guide, not
dogma: what matters is that the curve decreases.

**Step 6. Hygiene from the review.**
`__pycache__` out of the repository and into `.gitignore`, the fate of
`.hermes/` and `pool/`, remove `NoWarn` or explain it.

**Step 7. Documents.**
`cat-shelter-mvp.md`: section 3 — remove the move limit, record the curve by
pile size; section 6 — replace "+5 moves" with "+1 spot"; section 12 —
rename the event. `cat-shelter-tasks.md`: task 3.4 gets rewritten from
move-budget slack to the pile-size curve; 6.6 — the new button label; M7 —
the event name; 8.0 — clarify the definition of the fourth metric.

The documents are edited **last**, once the code has already confirmed the
numbers. The other way around would repeat the original mistake: writing
down a number first, then discovering it's unreachable.

---

## What we're deliberately not changing

**The shelf stays at nine spots.** The genre standard is seven, and with
seven the game would be closer to the reference examples. But nine is part
of the visual identity, and pile size gives the same range of difficulty.
Changing both at once would mean losing track of what affected what.

**The solver and its algorithm are untouched.** It works, it's verified, it
answers fast.

**The "port plus cross-check" approach between C# and Python stays.** It's
weaker than shared code — both sides can get it wrong the same way, as in
the case with the order of checks — but rewriting it as shared code costs
more than it's worth on a three-week prototype. The price for this is
step 3: conformance must cover all outcomes, not just wins.

---

## What this costs

The core and the mirror — a few hours, the changes are small and targeted.
The twelve levels are regenerated by running the generator. Step 3 is the
most expensive, because it requires figuring out a way to generate losing
games.

In return: a dead parameter disappears, a real difficulty curve appears,
verified by measurement, and the fourth metric starts measuring what it was
meant to measure.

---

## One check that none of this replaces

Win rate isn't the same thing as being fun. The table says that on level
nine the player will lose roughly every third attempt; it doesn't say
whether they'll want to try again.

That's what task 3.7 finds out — five outside people playing the debug
build. A prototype for it is already built (`build/playtest/`). It's worth
seeing the restructuring through to the end precisely so that 3.7 sits
people down with a game that has a real curve, not twelve identical levels.
