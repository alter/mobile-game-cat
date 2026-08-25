# Level format and property-based testing

Date material collected: 2026-08-24.

## In brief

- No ready-made, widely used open JSON schema specifically for the "layers + occlusion + N-slot shelf" mechanic could be found; the closest open examples are general tiled-level formats with `layer`/`tiles` fields (e.g. the [game-map-editor wiki](https://github.com/ppelikan/game-map-editor/wiki/Level-JSON-file-structure)) and the general layer/occlusion description from the 羊了个羊 clone write-up by [阮一峰](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html) — both are examined below, and a working schema with an explicit `blocked_by` field is proposed based on them.
- The property-based testing library for Python is [Hypothesis](https://github.com/HypothesisWorks/hypothesis), current version at the time of collection — **6.165.10** (verified on the [PyPI](https://pypi.org/project/hypothesis/) page), supporting Python 3.10–3.14.
- The basic technique is the `@given` decorator with strategies (`st.integers()`, `st.lists()`, etc.); for composite structures with dependencies between fields — the `@st.composite` decorator, as shown in the official [Custom strategies](https://hypothesis.readthedocs.io/en/latest/tutorial/custom-strategies.html) documentation section.
- The general academic recommendation for procedural puzzle generation with mandatory solvability is to construct the data so that solvability is guaranteed by the generation method itself (reduced to a DAG/reverse construction), rather than checking and discarding invalid samples after the fact; this directly matches Hypothesis's official recommendation: "if you find yourself filtering out most cases — it's almost always better to generate the data you want directly" ([Custom strategies](https://hypothesis.readthedocs.io/en/latest/tutorial/custom-strategies.html)).
- For a game engine in C# and a solver in Python, keeping the rules in sync is an open engineering problem with no single correct solution; from the actually documented approaches: a full independent port of the rules (expensive, risk of divergence), a shared set of test cases / contract tests (cheaper, but doesn't cover the whole state space), and "golden master" — recording the reference runs of one implementation and checking the other against them (a well-documented characterization-testing technique, e.g. [Coding is Like Cooking / Ro-che, «Introduction to golden testing»](https://ro-che.info/articles/2017-12-04-golden-tests) and the article on the "blind golden master" at [DEV Community](https://dev.to/rnowif/the-blind-golden-master-67h)).
- The golden-master approach lifts the burden of proving each implementation correct on its own, but it does not by itself prove the rules were implemented correctly in principle — it only records that two runs matched (explicitly noted in the golden-master material above); hence the practical recommendation to combine it with a manual set of "known-correct" contract examples.
- A related, well-documented technique from multiplayer games — deterministic lockstep with per-step checksum verification of state between independent simulation implementations; it isn't a direct solution to "sync C# and Python," but it offers a ready method for detecting the moment of divergence (checking the state checksum at every step, not just at the end of a match).

## 1. JSON level description format

### 1.1 What was actually found in open projects

No direct open JSON format for games of the "Sheep a Sheep" / "Triple Match 3D" type, with fields typical of that mechanic (`layer`, `blocked_by`/`coverIds`, etc.), was found as a ready-made schema file — a targeted search for these field names on GitHub returned no matches at the time of collection. Two indirect sources were found:

1. A general (not specific to triple-match games) level format with an explicit layer split — the [ppelikan/game-map-editor, «Level JSON file structure»](https://github.com/ppelikan/game-map-editor/wiki/Level-JSON-file-structure) project wiki: a level is described with fields like `level-name`, tile dimensions (`tile-sizeX`/`tile-sizeY`), and one or more `layers`, each with its own `sizeX`/`sizeY` and a nested two-dimensional `tiles` array (tile identifiers), plus a separate `events` list with named triggers and `level-positions` coordinates. Negative values in the `tiles` array are used to denote special states/blockers. This is a general-purpose tile-map format (not specifically match-3), but it's precisely there that the idea of representing layer depth as a separate axis, rather than as a tile attribute, naturally comes from.
2. A textual description (not a JSON schema, but a prose retelling of an implementation) of how a 羊了个羊 level is built, by [阮一峰](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html): the field is divided into several overlapping layers with random positions and card types on each layer; only cards on a layer with no occlusion on top are clickable. This confirms the "layer + occlusion" model itself, already formalized in section 1.1 of the file `01-tile-match-solver.md` via `(i, j, k)` coordinates per [Hoogeboom, Kosters, van Rijn, Vis](https://arxiv.org/pdf/1604.05487), but without a concrete JSON representation.

Since there is no ready industry schema, below is an independently drafted schema, justified by the solver's requirements (section 1 of the file `01-tile-match-solver.md`: availability via `blocked_by`, a shelf capped at N slots, a multiple of 3 for each kind).

### 1.2 Proposed schema

Key decision: store occlusion as an explicit `blocked_by` list on each item (a list of ids of items that must be removed earlier), rather than deriving it from geometry on the fly — this makes the level self-contained and does not tie the solver to a specific coordinate system (a grid, arbitrary polygons, a 3D scene — it doesn't matter where the occlusion came from, the solver only needs the resulting graph).

```json
{
  "level_id": "lvl-0007",
  "shelf_capacity": 9,
  "match_size": 3,
  "kinds": 12,
  "move_limit": null,
  "items": [
    {
      "id": 0,
      "kind": 4,
      "layer": 0,
      "position": {"x": 120.0, "y": 80.0},
      "blocked_by": []
    },
    {
      "id": 1,
      "kind": 4,
      "layer": 1,
      "position": {"x": 118.0, "y": 82.0},
      "blocked_by": [0]
    },
    {
      "id": 2,
      "kind": 7,
      "layer": 0,
      "position": {"x": 300.0, "y": 40.0},
      "blocked_by": []
    }
  ]
}
```

Field explanations:

- `level_id` — the level's identifier, for tracing in logs/tests.
- `shelf_capacity` — the shelf's capacity (9 in the task); moved into the level's data rather than hardcoded into the solver, so the same code can check difficulty variants.
- `match_size` — how many identical items vanish at once (3 in the task); also a parameter, not a constant, for reusing the schema and solver for related mechanics (e.g., a "match 2" test mode for debugging).
- `kinds` — the number of item kinds in the level; used by the multiple-of-3 validator (section 7 of the file `01-tile-match-solver.md`) and need not equal `len(items)`, since it lets you check the completeness of the set.
- `move_limit` — an optional move limit; `null` if the specific rules variant (as in the original "Sheep a Sheep") has no move limit, only a shelf limit.
- `items[].id` — the item's unique identifier, used as a node of the dependency graph.
- `items[].kind` — the item's kind (what matches with what); `kind` values need not be sequential, the schema allows arbitrary numbering.
- `items[].layer` — layer depth, used only for generation/debugging/difficulty statistics (section 8 of the file `01-tile-match-solver.md`); the solver doesn't need this value to check availability, only `blocked_by`.
- `items[].position` — screen/scene coordinates, needed only for rendering and the generator, not used by the solver.
- `items[].blocked_by` — a list of `id`s of items that occlude this item and must be removed earlier; an empty list means the item is available from the start.

```python
from dataclasses import dataclass


@dataclass(frozen=True)
class LevelItem:
    item_id: int
    kind: int
    layer: int
    blocked_by: frozenset[int]


@dataclass(frozen=True)
class Level:
    level_id: str
    shelf_capacity: int
    match_size: int
    kinds: int
    move_limit: int | None
    items: tuple[LevelItem, ...]


def load_level(raw: dict) -> Level:
    items = tuple(
        LevelItem(
            item_id=it["id"],
            kind=it["kind"],
            layer=it["layer"],
            blocked_by=frozenset(it["blocked_by"]),
        )
        for it in raw["items"]
    )
    return Level(
        level_id=raw["level_id"],
        shelf_capacity=raw["shelf_capacity"],
        match_size=raw["match_size"],
        kinds=raw["kinds"],
        move_limit=raw.get("move_limit"),
        items=items,
    )
```
## 2. Property-based testing of level generation

### 2.1 The Hypothesis library: version and basic techniques

[Hypothesis](https://github.com/HypothesisWorks/hypothesis) is the main property-based testing library for Python. The [PyPI](https://pypi.org/project/hypothesis/) page, at the time of material collection (2026-08-24), lists version **6.165.10**, and the package classifiers list support for Python 3.10, 3.11, 3.12, 3.13, 3.14 (CPython and PyPy). The official documentation confirms the same version number in the [Quickstart](https://hypothesis.readthedocs.io/en/latest/quickstart.html) page title ("Hypothesis 6.165.10 documentation").

A basic example from the official Quickstart — a test that must hold for any value from the described input space:

```python
from hypothesis import given, strategies as st


@given(st.integers())
def test_integers(n):
    print(f"called with {n}")
    assert isinstance(n, int)


test_integers()
```

The `@given` decorator accepts one or more strategies (`st.integers()`, `st.text()`, `st.lists(...)`, etc.); by default Hypothesis generates and runs 100 random examples, and upon finding a failing example it automatically "shrinks" it to the minimal case that reproduces the error — this is documented library behavior (see [Quickstart](https://hypothesis.readthedocs.io/en/latest/quickstart.html) and the [HypothesisWorks/hypothesis](https://github.com/hypothesisworks/hypothesis) repository).

### 2.2 Composite strategies for a level with dependencies between fields

For generating a structure like our level (a list of items, where `blocked_by` must only reference existing `id`s, the count of each `kind` is a multiple of 3, and the shelf capacity is within reasonable bounds), plain `st.lists`/`st.integers` strategies are not enough — strategies with dependencies between generated values are needed. The official [Custom strategies](https://hypothesis.readthedocs.io/en/latest/tutorial/custom-strategies.html) documentation section gives the `@st.composite` decorator for this, shown with an example that generates an ordered pair:

```python
from hypothesis import given, strategies as st


@st.composite
def ordered_pairs(draw):
    n1 = draw(st.integers())
    n2 = draw(st.integers(min_value=n1))
    return (n1, n2)


@given(ordered_pairs())
def test_pairs_are_ordered(pair):
    n1, n2 = pair
    assert n1 <= n2
```

The official documentation notes in the same place that this specific example could be written more briefly via `st.tuples(st.integers(), st.integers()).map(sorted)`, but a named function with `@composite` gives more control and readability where there are several dependencies — exactly our case (we need to tie together `id`, `kind`, `blocked_by`, and the total count of each item kind at once).

A key recommendation from the same documentation, directly applicable to level generation: if getting correct data requires filtering out (`.filter(...)`) most of the values Hypothesis generates — it's almost always better to generate the correct data directly via `@st.composite`, rather than generating blindly and discarding invalid samples. This is a direct justification of the same principle as "generating a level by reverse construction" from section 6 of the file `01-tile-match-solver.md`: constructing objects that are guaranteed correct (there — guaranteed solvable) rather than generate-and-filter.

### 2.3 A strategy for generating a level that is guaranteed to satisfy the multiple-of-3 invariant

```python
from hypothesis import given, strategies as st


@st.composite
def level_with_valid_kind_counts(draw, min_kinds=2, max_kinds=15, max_multiplier=6):
    """Generate a level where every kind's total count is a multiple of match_size (3),
    by construction rather than by filtering."""
    match_size = 3
    num_kinds = draw(st.integers(min_value=min_kinds, max_value=max_kinds))

    items = []
    next_id = 0
    for kind in range(num_kinds):
        multiplier = draw(st.integers(min_value=1, max_value=max_multiplier))
        copies = multiplier * match_size          # always a multiple of match_size, by construction
        for _ in range(copies):
            items.append({"id": next_id, "kind": kind, "layer": 0, "blocked_by": []})
            next_id += 1

    return {
        "level_id": "generated",
        "shelf_capacity": 9,
        "match_size": match_size,
        "kinds": num_kinds,
        "move_limit": None,
        "items": items,
    }


@given(level_with_valid_kind_counts())
def test_kind_counts_are_always_multiples_of_three(raw_level):
    from collections import Counter
    counts = Counter(it["kind"] for it in raw_level["items"])
    assert all(n % 3 == 0 for n in counts.values())
```

Here the property `n % 3 == 0` is trivially true by construction — this is intentional: the `level_with_valid_kind_counts` strategy itself serves as a regression test that the level generator **cannot in principle** produce incorrect counts, because it is mathematically impossible to violate the condition inside the `for _ in range(copies)` loop. The usefulness of such a test lies not in finding a bug in this specific strategy, but in pinning down a contract: if someone later edits the function that generates production (not test-only) levels and accidentally breaks the multiple-of-3 property, an analogous test applied to the production generator, not to the test stub, will catch the regression.

### 2.4 The property "any generated level is solvable"

For this property the strategy must generate a level **via reverse construction** (section 6 of the file `01-tile-match-solver.md`), and the test itself must check that the solver (the DFS from sections 3–4 of the same file) actually finds a solution. This is simultaneously a test of the generator (has the reverse-construction guarantee broken) and of the solver (has it lost some valid solution due to a bug in the pruning):

```python
import random

from hypothesis import given, settings, strategies as st


@st.composite
def reverse_built_level(draw, min_kinds=2, max_kinds=10, max_multiplier=5, seed_max=2**31 - 1):
    num_kinds = draw(st.integers(min_value=min_kinds, max_value=max_kinds))
    multipliers = draw(
        st.lists(st.integers(min_value=1, max_value=max_multiplier), min_size=num_kinds, max_size=num_kinds)
    )
    seed = draw(st.integers(min_value=0, max_value=seed_max))
    rng = random.Random(seed)

    # generate_solvable_pile is the function defined in 01-tile-match-solver.md, section 6
    pile = generate_solvable_pile_multi(num_kinds, multipliers, rng)
    return pile


@settings(max_examples=200, deadline=None)   # solving can be slow; disable the per-example time limit
@given(reverse_built_level())
def test_reverse_built_levels_are_always_solvable(pile):
    state = State(pile=pile, shelf=Shelf.empty())
    solution = solve_dfs(state, seen=set())
    assert solution is not None, "a reverse-built level must always have a solution"
```

The `settings(deadline=None)` parameter is not an arbitrary detail, but a documented necessity: by default Hypothesis limits how long a single example may run and treats exceeding it as an error, which, when calling a potentially slow solver (DFS can be exponential in the worst case even with pruning), produces false test failures unrelated to the code's logical correctness; disabling or increasing `deadline` is a standard recommendation for tests calling slow code, applied in practice in blog posts about Hypothesis, e.g. in the write-up [«How to Build Property-Based Testing with Hypothesis»](https://oneuptime.com/blog/post/2026-01-30-how-to-build-property-based-testing-with-hypothesis/view).

### 2.5 The property "move budget within given bounds"

```python
from hypothesis import given, settings, strategies as st


@settings(max_examples=100, deadline=None)
@given(
    reverse_built_level(),
    st.randoms(),
)
def test_greedy_player_move_count_within_bounds(pile, rng):
    """After generation, a plausible-human greedy player (section 5 of
    01-tile-match-solver.md) must be able to finish within a designer-set move budget,
    and the level must not be trivially short (a lower bound guards against degenerate levels)."""
    state = State(pile=pile, shelf=Shelf.empty())
    moves_made = 0
    max_moves_allowed = 3 * len(pile.items)   # generous upper bound: at most one "wasted" shelf slot per item

    while not state.is_win():
        move = greedy_human_move(state, rng)
        if move is None:
            break
        state = apply_move(state, move)
        moves_made += 1
        if moves_made > max_moves_allowed:
            break

    assert state.is_win(), "greedy human policy failed to clear a reverse-built (solvable) level"
    min_moves_expected = len(pile.items) // 3       # cannot finish faster than the number of triples
    assert min_moves_expected <= moves_made <= max_moves_allowed
```
## 3. How not to let the game rules diverge between C# and Python

The problem: the solver is written in Python (for generating and checking levels), while the game itself is in C# (typical for Unity/Godot-C#). Both must agree consistently on what an "available item," a "move," a "triple," and a "loss" mean. Below is an honest assessment of the options, without claiming that any one of them is the industry standard specifically for this case (no specialized sources on syncing C#/Python for game rules were found; below are documented general-engineering techniques that transfer directly to this problem).

### 3.1 A full independent port of the rules to both languages

The game rules (availability, clearing triples, the loss condition) are implemented twice — once in C# for the game, once in Python for the solver — as two independent but semantically identical modules.

- **Cost.** Double the development cost and, more importantly, double the maintenance cost: any rule change (e.g., adding a new obstacle type) must be made in both places and kept in mind for both representations at once.
- **Reliability.** The lowest of all the options without extra measures: nothing stops the two implementations from silently diverging in a rare edge case (e.g., the order of processing a simultaneous shelf fill and a triple forming), and this may not surface until the solver marks a level as solvable while the game leaves the player stuck.
- The only practical way to reduce risk with this approach is a mandatory shared set of contract tests (section 3.2) or a golden master (section 3.3) on top of both implementations; "just having both ports" by itself is the least reliable option of those listed.

### 3.2 A shared format of test cases (contract tests)

A language-independent set of pairs "state + move → new state" (or "level → solvability/move count") is fixed in a neutral format (JSON/YAML), and both implementations — C# and Python — are run against the same set in their own test runners (xUnit/NUnit on the C# side, pytest on the Python side).

- **Cost.** Moderate: you need to design the neutral case format once and write one adapter per language ("load the case → run it through my rules → compare with the expected result"), after which adding more cases is cheap.
- **Reliability.** Medium and manageable: the method's strength is directly proportional to how complete the case set is — hand-written cases cover situations the designer anticipated (a typical move, filling the shelf, a triple on the last move, a multiple of 3), but by definition don't cover what the designer didn't foresee. Combining this with property-based tests (section 2) on the Python side, whose findings (minimal "shrunk" counterexamples) get carried over into the contract set, partly closes this gap — this is standard practice: Hypothesis saves the minimal counterexample found for deterministic reproduction on subsequent runs (see the description of shrink/replay behavior in [Quickstart](https://hypothesis.readthedocs.io/en/latest/quickstart.html) and the [GitHub HypothesisWorks/hypothesis](https://github.com/hypothesisworks/hypothesis) overview), and such a counterexample literally becomes a new contract-case file for the C# implementation.

```python
import json
from pathlib import Path


def export_contract_case(state_before: "State", move: int, state_after: "State", case_path: Path) -> None:
    """Write a language-neutral test case that both the Python and the C# rule
    engines can be checked against independently."""
    case = {
        "state_before": state_before.to_dict(),
        "move": move,
        "state_after_expected": state_after.to_dict(),
    }
    case_path.write_text(json.dumps(case, indent=2, ensure_ascii=False))
```

```csharp
// C# side: load the same JSON file and assert the C# rules produce an identical result.
// (Illustrative signature only — actual (de)serialization depends on the project's JSON library.)
var testCase = ContractCase.LoadFrom("cases/case-0007.json");
var actual = GameRules.ApplyMove(testCase.StateBefore, testCase.Move);
Assert.Equal(testCase.StateAfterExpected, actual);
```

### 3.3 Golden master (reference recordings of runs)

One run of one implementation (e.g., the reference solver in Python) on a specific level is saved in full — the whole sequence of states, or at least the input and the final result — as a "reference" (golden file); on subsequent code changes (in either implementation), a new run is compared byte-for-byte/value-for-value against the saved file.

- The technique comes from characterization-testing practice for legacy code, introduced by Michael Feathers; a general description and the term's origin — [Blexin, «Golden Master Pattern: don't fear the legacy code!»](https://blexin.com/en/blog-en/golden-master-pattern-dont-fear-the-legacy-code/) and the Wikipedia article on [Characterization test](https://en.wikipedia.org/wiki/Characterization_test).
- The "blind golden master" variant, where the new and old implementations are called within the same test and their results compared directly with no intermediate file, is documented in the article [«The Blind Golden Master», DEV Community](https://dev.to/rnowif/the-blind-golden-master-67h); it also gives a practice example: "GitHub ran both algorithms in production, comparing outputs and raising errors on mismatch until confidence was established, then switched to the new implementation" — that is, running two implementations in parallel in production until full confidence, before switching over, which transfers directly to the "Python solver / C# game" pair if both share a common CI pipeline.
- **Cost.** Low to start (writing "run it and save a file" is simpler than designing a contract-case format), but accumulating golden files without oversight creates its own maintenance problem: when rules are intentionally changed, the references need to be regenerated deliberately, and automatically regenerating them "the test failed — let's just update the reference" defeats the purpose of the test (see the warning in the same place, [Ro-che, «Introduction to golden testing»](https://ro-che.info/articles/2017-12-04-golden-tests): a golden master doesn't prove the result is correct — it only guards against unintentional drift from behavior already recorded).
- **Reliability.** Good at detecting **divergence** between implementations (including unexpected divergence not foreseen in advance — unlike the contract tests of section 3.2, which check only what was explicitly written down), but it does not guarantee that the recorded behavior was correct to begin with: if the first run (the one that became the reference) already contained a bug, the golden master will simply freeze it and demand the same bug from the second implementation.
- An additional necessary condition for applicability is determinism: non-deterministic values (traversal order, `random` state, time) must either be pinned by a shared seed or excluded from the comparison, otherwise the method is inapplicable in principle — this is separately emphasized in the golden-master material (see above, Ro-che).

### 3.4 A related technique: deterministic lockstep with state checksums

From the field of multiplayer networking — in a lockstep architecture, all copies of the simulation must produce a bit-identical result given identical inputs, and to debug divergences a state checksum is used, computed and compared by each copy at every step, not only at the end of a match — this lets you localize the first moment of divergence, not just the fact that it occurred. This method is not a direct solution to "sync C# and Python," but it gives a useful practical technique to embed in any of approaches 3.1–3.3: if results don't match, compare not just the final state but the checksum at every move, to quickly find the specific rule where the implementations diverged.

### 3.5 Final recommendation

None of the three approaches is unconditionally "correct" — they solve different parts of the problem and are usually combined:

- Contract tests (3.2) — for explicitly foreseen, deliberately designed edge cases; cheap to maintain, but blind to the unforeseen.
- Golden master (3.3) — for quickly detecting any unplanned divergence between already-existing implementations; cheap to start, but requires discipline when updating references and doesn't verify original correctness.
- Property-based tests on the Python side (section 2) as a supplier of new contract cases for C# — a practical bridge between "we found a bug once in Python" and "this same check now permanently protects the C# implementation too."
- A full duplicate port of the rules (3.1) is unavoidable in the sense that the rules must exist in both languages one way or another — the question isn't "port or not," but "what, besides an honest promise, confirms that both ports agree," and it's exactly 3.2–3.4 that answer that question, not the mere fact of porting.

## Sources

- [Hypothesis — PyPI](https://pypi.org/project/hypothesis/)
- [Hypothesis — Quickstart (readthedocs)](https://hypothesis.readthedocs.io/en/latest/quickstart.html)
- [Hypothesis — Custom strategies (readthedocs)](https://hypothesis.readthedocs.io/en/latest/tutorial/custom-strategies.html)
- [HypothesisWorks/hypothesis (GitHub)](https://github.com/hypothesisworks/hypothesis)
- [«How to Build Property-Based Testing with Hypothesis», 2026](https://oneuptime.com/blog/post/2026-01-30-how-to-build-property-based-testing-with-hypothesis/view)
- [ppelikan/game-map-editor — Level JSON file structure (GitHub wiki)](https://github.com/ppelikan/game-map-editor/wiki/Level-JSON-file-structure)
- [阮一峰, «羊了个羊，如何自己实现»](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html)
- [Hoogeboom, Kosters, van Rijn, Vis, «Acyclic Constraint Logic and Games», arXiv:1604.05487](https://arxiv.org/pdf/1604.05487)
- [Blexin, «Golden Master Pattern: don't fear the legacy code!»](https://blexin.com/en/blog-en/golden-master-pattern-dont-fear-the-legacy-code/)
- [Characterization test — Wikipedia](https://en.wikipedia.org/wiki/Characterization_test)
- [«The Blind Golden Master», DEV Community](https://dev.to/rnowif/the-blind-golden-master-67h)
- [Ro-che, «Introduction to golden testing»](https://ro-che.info/articles/2017-12-04-golden-tests)