# Independent verification, 2026-08-27

**Verifier:** a fresh context. I wrote none of `tools/solver/measure.py`,
`tools/solver/rules.py`, `tools/tests/test_measure.py`, nor this task's
`NOTES.md`. No adb, no emulator, no Unity build. All mutation testing below
ran against scratch copies outside the repository
(`/tmp/.../scratchpad/mutation-solver`); the tracked files were never
touched. `09-hidden-kinds`, `04-difficulty-curve` and `D15`/`D3` in
`DECISIONS.md` read for context, not audited here.

## 1. VERIFY item 3 — a convenient escape, or a real argument?

**The argument holds.** VERIFY 3 asks the new table to be lower than
98/87/66 "at every band," but `shelf_only_policy` — the reproduction of the
*retired* measurement's player — never inspects a choice's kind against
anything but the current shelf (`measure.py:57-76`); every candidate it
scores already comes from `RulesState.available()`
(`rules.py:52-59`), which excludes buried and locked items regardless of
whether hiding exists as a rule. There is no code path by which hiding could
change `shelf_only_policy`'s output — it is a structural identity, not an
oversight. NOTES.md's chosen substitute (`partial_policy` vs `oracle_policy`,
same heuristic, only the buried-pile lookahead differs) is the only
apples-to-apples comparison available in this rule model, and it is reported
plainly, including the fact that the literal VERIFY 3 direction (new number
lower than old) does not hold — 99.2/94.2/90.0 is *higher* than 98/87/66,
disclosed, not hidden.

## 2. The headline number, reproduced independently

```
$ .venv/bin/python -m tools.solver.measure
| pile size | ... | shelf-only | reachable-aware, pile hidden | reachable-aware, pile visible | price of hiding |
| 36 | ... | 98.0% | 99.2% | 99.8% | 0.6 pp |
| 48 | ... | 83.8% | 94.2% | 94.2% | 0.0 pp |
| 60 | ... | 71.5% | 90.0% | 89.8% | -0.2 pp |
```

**Exact match, to the decimal, with the 27.08 table in `NOTES.md`** — every
column, every band, including `open_front` (16.7/24.6/28.3). Also ran
`--shipped --json` (37 levels, 200 repeats): band averages and ranges
(36: 96.2%/98.2%, 84.5–100; 48: 87.5%/96.0%, 85.5–100; 60: 63.0%/87.1%,
36.5–99.5), the three hardest levels (`l34_room12_pile0` 62.0%,
`l32_room11_pile2` 66.5%, `l35_room12_pile1` 77.0%), and the 37-level mean
(92.7%) all match `NOTES.md`'s 27.08 shipped table exactly. "0.0 ± 1.2 pp"
is a fair description of both the 26.08 (-0.2/0.0/1.2) and 27.08
(0.6/0.0/-0.2) price-of-hiding rows — confirmed, not just repeated.

## 3. The `is_revealed` claim

```
$ grep -n "is_revealed" tools/solver/measure.py   # no output
$ grep -c "is_revealed" tools/solver/rules.py      # 1 (the definition only)
```

Confirmed by reading, not just grepping: `RulesState.is_revealed` exists and
implements D15 (a locked item is not excluded, only a buried one is) but is
called from nowhere in `measure.py`. Every policy is handed
`[(item.id, item.kind) for item in available]` (`measure.py:175`), and
`available()` already excludes locked items independently of whether they
are revealed (`rules.py:58`). **The conclusion is right, not overstated**:
since availability and revelation are computed by two different, uncoupled
checks, and only availability ever reaches a policy, D15 could not have
moved these numbers regardless of which way it went. "This instrument
cannot answer that question, in either direction" is exactly correct.

## 4. Mutation-testing the policy-signature test

`test_player_policies_receive_nothing_but_what_a_player_sees` checks
`inspect.signature(policy).parameters` for `shelf_only_policy` and
`partial_policy`. Two mutations, on a scratch copy:

**Mutation A — declare an extra parameter** on `partial_policy`
(`..., pile=None`), signature unchanged elsewhere:

```
FAILED test_player_policies_receive_nothing_but_what_a_player_sees
AssertionError: assert ['choices', 'shelf', 'pile'] == ['choices', 'shelf']
```

Caught immediately. The test does what it claims for this shape of change.

**Mutation B — leak whole-pile kind counts through a side channel**,
signature left exactly `(choices, shelf)`: `play()` populates a module-level
dict from `state.level.pile` (buried items included) before calling
`partial_policy`; the policy's own tie-break reads that dict instead of its
local `reachable` count when populated.

```
$ pytest tools/tests/test_measure.py -q
..........
10 passed in 0.56s
```

**Nothing notices.** All ten tests pass, including the signature test — its
declared parameters never changed. Run through `measure --games 100` to
confirm the leak is real, not inert: win rates shift measurably (e.g. 48
items: partial 86.0% vs the true 94.2%, oracle 92.0% vs 94.2% — the leak
changes the tie-break heuristic itself, not simply "better," but plainly
different, proving the policy is now reading pile data it has no parameter
for).

**So: the test guards against a policy's own declared parameter list
growing, and does not guard against a call site (or a closure, a global, a
mutable default) handing more information through the parameters that
already exist.** `NOTES.md`'s line — "a future edit cannot quietly hand
them the buried pile" — is true of the specific bug class the test was
built for (`test_ties_are_not_resolved_by_item_id`'s sibling problem) and
not true in general, as demonstrated. It does not affect the numbers in
this document: code review (§3, VERIFY 2's own stated method) confirms the
current, unmutated `play()`/`partial_policy` contain no such channel today.

## How to reproduce

```bash
.venv/bin/python -m tools.solver.measure
.venv/bin/python -m tools.solver.measure --shipped --json
grep -n "is_revealed" tools/solver/measure.py
sed -n '52,67p' tools/solver/rules.py
# mutation testing: copy tools/{__init__.py,solver/*.py,tests/__init__.py,
# tests/test_measure.py} to a scratch dir outside the repo, edit
# partial_policy's def line to add a third parameter, rerun
# `pytest tools/tests/test_measure.py -q` there — one failure, the signature
# assertion, naming partial_policy.
```

## What was not checked

- `09-hidden-kinds` and `04-difficulty-curve` themselves — read only as
  context, not re-verified.
- The generator/shipped-level provenance for why numbers moved between
  26.08 and 27.08 (`bff0de2`, reshipped levels) — `NOTES.md`'s attribution
  taken on trust; not independently traced through git history.
- Whether `oracle_policy`'s `dig_cost` computation (`_dig_cost`, transitive
  blocker counting) is itself correct — used as given.
- Bounding-box / conformance parity between `rules.py` and `Core/Board.cs`
  — that is `tools/tests/conformance_test.py`'s job, out of scope and
  explicitly another agent's file this pass.

## Overall verdict: **verify:passed**

Every number this task publishes reproduces exactly, independently, on a
second run. VERIFY 1 is met (script output, not hand-typed — confirmed by
running it myself). VERIFY 2 is met of the current code, by the code-review
method the item itself specifies. VERIFY 3's non-satisfaction is argued
correctly, not dodged. The D15 conclusion is exactly right. The one
overstatement found — the signature test's implied protection against a
*future* smuggled leak — is real, demonstrated, and worth a follow-up
(hardening the test, e.g. asserting the whole-pile is never referenced
inside `partial_policy`/`shelf_only_policy`'s closures, or that `choices`'
length never exceeds `len(available)`), but it describes a risk to
*tomorrow's* edits, not a defect in what is published today.
