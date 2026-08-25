# The solver for games of the "pile of layers + a 9-slot shelf" type

Date material collected: 2026-08-24.

Mechanic: a pile of 30–60 items stacked in layers (only the top, unoccluded items are available); the player moves an available item onto a 9-slot shelf; three identical items on the shelf vanish; loss — the shelf is full with no match, or moves have run out; win — the pile is fully cleared. This is the mechanic of the games "Sheep a Sheep" (羊了个羊), "Triple Match 3D," "Tile Busters," "Zen Match."

## In brief

- No strict theorem on NP-completeness for exactly this mechanic (a layered pile + a bounded shelf) was found in open sources. The closest, strictly proven problem by construction — Mahjong Solitaire (also layers, also occlusion, but removal happens in pairs, with no bounded buffer) — has been proven NP-complete by several independent works: [de Bondt, 2012](https://arxiv.org/abs/1203.6559) and [van Rijn / Hoogeboom / Kosters / Vis, 2012–2014](https://arxiv.org/pdf/1604.05487).
- A game with a bounded buffer (a shelf/reserve, as in our mechanic) is structurally closer to FreeCell solitaire, for which NP-completeness is proven for **any fixed number of reserve cells** ([Helmert, 2003](https://www.sciencedirect.com/science/article/abs/pii/S0004370202003648)) — this is a direct formal analog of the "9-slot shelf."
- The related SameGame/Clickomania problem (clearing groups of identical tiles) is also NP-complete, but under different constraints (2 columns and 5 colors, or 5 columns and 3 colors) — see [Biedl, Demaine et al., 2002](https://arxiv.org/abs/cs/0107031) and the strengthening in [Demaine et al., 2015](https://erikdemaine.org/papers/Clickomania_MOVES2015/paper.pdf).
- With 30–60 items and a 9-slot shelf, exhaustive search (BFS/DFS with no pruning) does not converge in reasonable time: branching at each move is bounded by the number of visible items (usually 3–8), but the game's depth is dozens of moves, and the state includes both the pile's contents and the shelf's contents.
- A practically usable approach is depth-first search with backtracking, with memoization of already-seen states, a canonical state form for the cache key, and heuristic move ordering — this is how open Mahjong Solitaire solvers are built, including the practical algorithm from de Bondt's paper.
- Generating levels that are guaranteed solvable — by reverse construction: take the fully cleared pile (an empty field), and "hand out" items back in the reverse order of removal. The technique is confirmed in articles on generating solitaire layouts ([Dan Q, FlipFlop Solitaire](https://danq.me/2026/04/18/flipflop-solitaires-deck-generation-secret/)) and implemented in open Mahjong Solitaire generators, e.g. [dAmihl/MyJong](https://github.com/dAmihl/MyJong) ("always-solvable" mode) and indirectly in [cchaiyatad/mahjong-solitaire-solver](https://github.com/cchaiyatad/mahjong-solitaire-solver).
- The count of each item kind must be a multiple of three — otherwise one or two "leftover" items can never be cleared, and the level is guaranteed unsolvable under any play.
- Heuristics for "plausible play" (not an optimal solver, but a believable human) are documented only in fragments: writeups of Zen Match and Tile Busters describe the effect of hidden layers ("blind" moves), and the open solver [NB-Dragon/SheepSolver](https://github.com/NB-Dragon/SheepSolver) implements several move-selection strategies (by index, by layer, by "normal" progress, random) — this is the closest thing to a formalization of a "greedy player" in open code.
- There is almost no open-source code for real solvers and generators aimed specifically at the "layers + an N-slot buffer" combination (Sheep a Sheep, Triple Match 3D); there is one backtracking solver in Python ([NB-Dragon/SheepSolver](https://github.com/NB-Dragon/SheepSolver)) and one RL agent ([opendilab/DI-sheep](https://github.com/opendilab/DI-sheep)). Most of the open code out there is for Mahjong Solitaire (paired removal, no buffer).

## 1. Formal problem statement

### 1.1 Representing the pile: layers and occlusion

In the formal definition of Mahjong Solitaire given by [Hoogeboom, Kosters, van Rijn and Vis](https://arxiv.org/pdf/1604.05487) (section 3.1 of their paper), a configuration is a set of positions `(i, j, k)`, where `i, j` are coordinates in the plane and `k` is the height (layer). Two integrity conditions are imposed:

1. if `(i, j, k)` and `(i, j', k)` are in the configuration and `j < j'`, then all intermediate `(i, j'', k)` are also in the configuration (no holes within a row);
2. if `(i, j, k)` is in the configuration and `k > 0`, then `(i, j, k-1)` is also in the configuration (a tile cannot hang in the air — it must be supported from below).

A position `(i, j, k)` is called **hidden** if `(i, j, k+1)` exists — that is, another tile sits on top of it; otherwise the position is **visible**. A position is "available" if it is not hidden and at least one of the row-adjacent positions `(i, j-1, k)` or `(i, j+1, k)` is either absent from the configuration or also free — meaning the tile can be "pulled out sideways." This formalization carries over directly to our mechanic: the "pile of layers" is the same three-dimensional (or arbitrary-graph) representation, where availability is determined by nothing sitting on top, rather than by lateral "pulling out."

For our game it's more convenient not to tie things to the `(i, j, k)` grid, but to keep an explicit **directed dependency graph** `blocked_by`: each item has a set of item identifiers that sit on top of it and must be removed first. This is a generalization of the same occlusion principle used by the open 羊了个羊 clone described in [阮一峰 (Ruan Yifeng)](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html)'s writeup: "the field is divided into several overlapping layers with random positions and card types on each layer; only cards on a layer with no occlusion on top are clickable" (paraphrased from the article).

```python
from dataclasses import dataclass, field

@dataclass
class Item:
    item_id: int
    kind: int                    # item kind (for triple matching)
    blocked_by: set[int] = field(default_factory=set)   # ids of items on top

@dataclass
class Pile:
    items: dict[int, Item]       # item_id -> Item, including already-removed ones (unless deleted)

    def is_available(self, item_id: int) -> bool:
        item = self.items[item_id]
        return len(item.blocked_by) == 0

    def available_items(self) -> list[int]:
        return [iid for iid, it in self.items.items() if self.is_available(iid)]
```

### 1.2 Game state, a move, the goal

State `S = (Pile, Shelf, moves_left)`, where:

- `Pile` — the remaining pile (the set of not-yet-removed items and the `blocked_by` graph);
- `Shelf` — the shelf's contents: a multiset of item kinds, `|Shelf| <= 9` (or another limit);
- `moves_left` — the remaining move count, if it's bounded by the level's rules (not every clone has a move limit; in "Sheep a Sheep" there is formally no move limit, only a shelf limit).

A move: pick an available (`is_available`) item from `Pile`, move it onto `Shelf`. After moving it, if `Shelf` has accumulated three items of the same kind, they vanish (this is not the player's choice, but an automatic rule). The game is lost if `|Shelf| == 9` and no triple formed after the last move. The game is won if `Pile` is empty.

```python
def apply_move(state: "State", item_id: int) -> "State":
    assert item_id in state.pile.available_items()
    new_pile = state.pile.remove(item_id)          # remove from the pile, clear blocks on whatever was underneath
    kind = state.pile.items[item_id].kind
    new_shelf = state.shelf.add(kind)               # place on the shelf
    new_shelf = new_shelf.resolve_triples()          # automatically clear triples
    return State(pile=new_pile, shelf=new_shelf)

def is_win(state: "State") -> bool:
    return state.pile.is_empty()

def is_loss(state: "State") -> bool:
    return len(state.shelf) >= 9 and not state.pile.available_items()
```

## 2. Computational complexity

### 2.1 What is proven about Mahjong Solitaire (the closest strictly studied mechanic)

No strict work specifically on "Sheep a Sheep" / "Triple Match 3D" / games with an N-slot shelf was found in open sources (journals, arXiv, conferences) — the closest by construction (layers, occlusion, only top items available) is Mahjong Solitaire, for which there are several independent results:

- [Condon, Feigenbaum, Lund, Shor, 1997] — per the account in [Hoogeboom et al.](https://arxiv.org/pdf/1604.05487), proved that the variant with **incomplete information** (the player does not know in advance what lies under the top tiles) is PSPACE-complete.
- [Eppstein, 2012] — per the same account, formulated a proof that the variant with **complete information** (all tiles visible to the player from the start) is NP-complete.
- [de Bondt, "Solving Mahjong Solitaire boards with peeking," 2012, arXiv:1203.6559](https://arxiv.org/abs/1203.6559) — independently proved NP-completeness of the complete-information ("peeking") version by reduction from 3-SAT, with NP-completeness holding even if only isolated stacks of height 2 of the forms `/aab/` and `/abb/` are allowed. The same paper shows that layouts consisting only of isolated stacks of height 1 and 2 are always solvable "with peeking" and this is solvable in polynomial time (the problem is in class P), and gives an optimal algorithm for such layouts without peeking.
- [Hoogeboom, Kosters, van Rijn, Vis, "Acyclic Constraint Logic and Games," ICGA Journal, 2014, arXiv:1604.05487](https://arxiv.org/pdf/1604.05487) — give a formal definition of a Mahjong Solitaire configuration (see section 1.1 above) and independently confirm NP-completeness by reduction from Acyclic Bounded Nondeterministic Constraint Logic (Bounded NCL) — a constructive reduction via AND/OR/FANOUT/CHOICE "gadgets." The same paper uses the same technique to prove NP-completeness of generalized Klondike solitaire.
- The master's thesis [van Rijn, "Playing Games: The complexity of Klondike, Mahjong, Nonograms and Animal Chess," LIACS, Universiteit Leiden, 2012](https://theses.liacs.nl/398) — states directly in the abstract: "We present a proof that both Klondike and Mahjong are NP-complete, by reduction from Bounded Nondeterministic Constraint Logic" (this excerpt was confirmed by opening the thesis PDF).
- [de Bondt, "Solving Shisen-Sho boards," arXiv:2010.09014](https://arxiv.org/abs/2010.09014) — a related game (clearing pairs of identical tiles connected by a path of at most three segments), also proven NP-complete; incidentally shows that under realistic assumptions, checking "can a given pair be played" is computable in logarithmic time.

### 2.2 A related mechanic with a bounded buffer — FreeCell

Our mechanic contains a significant element absent from classic Mahjong Solitaire: a **bounded external buffer** (a 9-slot shelf) into which items are set aside in reserve. Formally this is closer to FreeCell solitaire, which has a fixed number of free cells for temporary card storage. [Helmert, "Complexity results for standard benchmark domains in planning," Artificial Intelligence 143(2), 2003](https://www.sciencedirect.com/science/article/abs/pii/S0004370202003648) proved that determining winnability of FreeCell is NP-complete **for any fixed positive number of free cells** — meaning a bounded buffer by itself does not make the problem polynomial. The paper's direct text could not be opened via WebFetch (the site blocked access); the statement is given per independent works that cite it and use it as a proof template (e.g., [«Spider Solitaire is NP-Complete», arXiv:1110.1052](https://arxiv.org/abs/1110.1052), which directly references section 4.3 of Helmert's work as a model for the proof construction). This is a structural analog: the 9-slot buffer in our game plays the same role as the reserve cells in FreeCell.

### 2.3 A related mechanic without a buffer — SameGame/Clickomania

Another problem close in spirit (clearing groups of identical tiles) is SameGame (aka Clickomania): the player removes a connected group of two or more identical tiles, and the ones above fall down. [Biedl, Demaine, Demaine, Fleischer, Jacobsen, Munro, "The Complexity of Clickomania," 2002 (arXiv:cs/0107031)](https://arxiv.org/abs/cs/0107031) proved that determining solvability (can all tiles be cleared) is NP-complete for two columns and five colors or for five columns and three colors, whereas a single column with two colors is solvable in linear time. A later strengthening — [«Clickomania is Hard, Even with Two Colors and Columns», MOVES 2015](https://erikdemaine.org/papers/Clickomania_MOVES2015/paper.pdf) — shows NP-completeness already for two colors and two columns (in the scoring variant). This mechanic is structurally further from ours (no layers and no buffer, removal is by adjacency on a plane rather than by kind with a "three pieces" limit), so it should be regarded only as one more example: the "clear identical tiles" family generally has high computational complexity, and none of the variants turned out to be polynomial without strong restrictions.

### 2.4 An honest conclusion

For the task's exact mechanic — a layered pile, only top items available, a bounded 9-slot buffer, removal in triples — no rigorous proof of NP-completeness (or membership in P) was found in open scientific sources. What is actually proven:

- Mahjong Solitaire (layers + occlusion, removal in pairs, no buffer) — NP-complete ([de Bondt, 2012](https://arxiv.org/abs/1203.6559); [Hoogeboom et al., 2014](https://arxiv.org/pdf/1604.05487); [van Rijn, 2012](https://theses.liacs.nl/398) thesis).
- FreeCell (bounded buffer, no layers) — NP-complete for any fixed buffer size ([Helmert, 2003](https://www.sciencedirect.com/science/article/abs/pii/S0004370202003648)).
- SameGame/Clickomania (clearing connected groups of identical tiles, no layers and no buffer) — NP-complete under certain constraints on the number of columns and colors ([Biedl et al., 2002](https://arxiv.org/abs/cs/0107031); [Demaine et al., 2015](https://erikdemaine.org/papers/Clickomania_MOVES2015/paper.pdf)).

Since our mechanic combines both "heavy" ingredients (layers with hiding, as in Mahjong, and a bounded buffer, as in FreeCell), it is reasonable to assume (but this is **not proven** in the sources found!) that NP-completeness holds for it too. The claim in the GitHub repository [NB-Dragon/SheepSolver](https://github.com/NB-Dragon/SheepSolver) that solving "羊了个羊" is an "NP problem" is an informal remark by an enthusiast author in the README, not the result of peer-reviewed work; this wording cannot be substituted for a rigorous proof in the task text, and we do not do so here.

## 3. Search algorithms

The practical side of the problem (not proving complexity, but actually building a solver) with 30–60 items and a 9-slot shelf looks like this: the state space is huge, but branching at each move is small (a choice among "available" items, usually 1 to 8–10 at once), and the game's depth equals the number of items in the pile. This is a classic setup for pruned search, not "brute force."

- **Breadth-first search (BFS).** Guarantees the shortest solution by move count, but requires storing the entire state frontier — with branching of even 3–4 and a depth of 40–60 moves, the frontier grows orders of magnitude faster than memory allows. Practically unusable without aggressive duplicate pruning.
- **Depth-first search with backtracking (DFS/backtracking).** The main tool for this kind of problem: at each step try one of the available moves, recursively solve the rest, and on failure roll back and try the next one. This is exactly how de Bondt's practical algorithm for Mahjong Solitaire is built (described as "simple and fast," with an "efficient pruning criterion and a heuristic for finding and prioritizing critical groups," per the abstract of [arXiv:1203.6559](https://arxiv.org/abs/1203.6559)), and the open solver [NB-Dragon/SheepSolver](https://github.com/NB-Dragon/SheepSolver) for "Sheep a Sheep" is built the same way — its README explicitly calls the algorithm "backtracking search."
- **A\* with a heuristic.** Applicable if the task is not "solvable/not solvable" but "the minimal number of moves": then an admissible (non-overestimating) heuristic is needed, e.g. "the number of triples not yet collected" or "the number of kinds that currently have fewer than 3 available copies left in the pile." For pure solvability checking, A* is overkill — DFS with pruning is usually enough; A* is justified if you also need the answer to "in how many moves under perfect play."
- **Iterative deepening (iterative deepening / IDDFS, and for A* — IDA\*).** Useful if a reasonable depth/move-count limit isn't known in advance and it's important not to spend memory on a full BFS frontier: repeat DFS with a growing depth threshold (or a growing cost threshold for IDA*), until a solution is found or its absence is proven within the budget.

For pure "is this level solvable" checking, DFS with backtracking and hard pruning (section 4) is the most practical. For estimating "how many moves under plausible play" — not a full search, but a single run of a greedy heuristic player (section 5), possibly averaged over several runs with different admissible variations in move order.

```python
def solve_dfs(state: "State", seen: set, limit: int | None = None) -> list[int] | None:
    """Depth-first search with backtracking. Returns a winning move sequence or None."""
    key = state.canonical_key()
    if key in seen:
        return None                      # already proven a dead end from this state
    if state.is_win():
        return []
    if state.is_loss():
        seen.add(key)
        return None
    if limit is not None and limit <= 0:
        return None

    for item_id in ordered_candidate_moves(state):   # move ordering, see section 4
        next_state = apply_move(state, item_id)
        rest = solve_dfs(next_state, seen, None if limit is None else limit - 1)
        if rest is not None:
            return [item_id] + rest

    seen.add(key)                        # memoize: this state is a proven dead end
    return None
```

## 4. Pruning without which the search doesn't converge

- **Memoization of already-seen states.** Without remembering "there's no solution from this state," DFS will keep recomputing the same dead ends reachable via different move orders (the interchangeability of independent moves is a classic source of duplicate-count blowup). What needs storing is not the move sequence itself but the **state** (the pile's contents + the shelf's contents) in a set of already-proven losses.
- **A canonical state form for the cache key.** The shelf is a multiset of kinds, not a sequence: the order of items on the shelf doesn't matter for further play (except possibly for the UI), so the key must normalize the shelf (e.g., a sorted tuple of kinds) and, separately, the remaining part of the pile (a frozenset of not-yet-removed item ids is enough, since the `blocked_by` graph is uniquely computable from the set of remaining ids and the pile's original structure).
- **Pruning of provably losing states.** Besides the literal "shelf full with no matches," earlier necessary conditions for the impossibility of winning are useful, e.g.: if some item kind has a remaining count in the pile that isn't a multiple of three after excluding already-removed ones, the level is unsolvable in principle (see section 7); this can be checked as a level invariant once at load time, rather than at every search node.
- **Move ordering.** DFS finds a solution faster if moves that are likely useful are tried first: for example, taking an item first if two items of the same kind are already on the shelf (then the move immediately frees a shelf slot), and only afterward the other available items. This ordering sharply reduces the average branching factor in practice, though it formally does not change the search's correctness.

```python
def ordered_candidate_moves(state: "State") -> list[int]:
    """Heuristic move ordering: prefer moves that complete a triple right away,
    then moves that make progress on a pair already on the shelf,
    then everything else, biased toward items with fewer remaining copies."""
    available = state.pile.available_items()

    def priority(item_id: int) -> tuple:
        kind = state.pile.items[item_id].kind
        count_on_shelf = state.shelf.count(kind)
        remaining_of_kind = state.pile.remaining_count(kind)
        completes_triple = 1 if count_on_shelf == 2 else 0
        return (-completes_triple, -count_on_shelf, remaining_of_kind)

    return sorted(available, key=priority)
```

## 5. Heuristics of "plausible play" (a greedy player)

There is little open, formal literature on exactly how to model a "believable human" specifically for games of the "pile + shelf" type; what follows is what was actually found in write-ups and open code, without speculation.

- **[NB-Dragon/SheepSolver](https://github.com/NB-Dragon/SheepSolver)** implements not one but several variants of the card-selection strategy for removal ("抽牌策略," card-drawing strategy): by ascending/descending index, by layer (bottom-first/top-first), a "normal" mode (depends on the game's progress), and a random mode. Different strategies give different solving efficiency — meaning the author explicitly notes that move order is critical for the outcome, not just whether a solution exists at all.
- The Zen Match write-up by [Deconstructor of Fun](https://www.deconstructoroffun.com/blog/2023/10/15/zen-match-how-a-first-mover-falls-behind) points directly at a source of human sub-optimality: the hidden layer forces the player to make "blind" moves ("since Tile Match is a pretty deterministic genre, anything that the player can't see increases the difficulty" — the genre is deterministic, so it's precisely what the player can't see that raises the difficulty). The Tile Busters write-up by [Gamigion](https://www.gamigion.com/game-analysis-spyke-games-tile-busters/) confirms the same effect: "hidden tiles encourage players to make blind guesses, introducing a chance factor." Formally this means a "plausible player" model must either not see the pile's contents below the top layer (i.e., build a solution under incomplete information — closer to the Condon et al. PSPACE setup, section 2.1) or explicitly model guessing via random choice among covered items.
- The general "greediness" principle that follows from the automatic triple-clearing mechanic (not from a separate publication, but directly from the game's rules as formalized in section 1.2): it is not advantageous for the player to take an item of a kind that has no third copy already on the shelf and none expected among the near-term available layers, if free shelf slots are scarce — because this pushes toward a loss with no benefit. From this comes a natural (and the one most often found in troubleshooting write-ups about clone implementations, like the [Ruan Yifeng](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html) write-up) greedy heuristic: `priority(item) = -(2, if it completes a triple; 1, if the shelf already has at least one copy; 0 otherwise)`, prioritizing kinds that will have exactly a multiple-of-three count remaining after the move.

```python
def greedy_human_move(state: "State", rng) -> int | None:
    """A plausible-human policy, not an optimal solver.
    Prefers: (1) complete a triple now, (2) add to an already-started pair,
    (3) otherwise avoid moves that would fill the shelf without progress,
    falling back to a blind (random) choice among visually identical covered items
    to emulate imperfect information about what is still buried."""
    available = state.pile.available_items()
    if not available:
        return None

    def score(item_id: int) -> tuple:
        kind = state.pile.items[item_id].kind
        on_shelf = state.shelf.count(kind)
        free_slots = 9 - len(state.shelf)
        risky = 1 if (on_shelf == 0 and free_slots <= 2) else 0
        return (-(on_shelf == 2), -(on_shelf == 1), risky)

    best_score = min(score(i) for i in available)
    best_moves = [i for i in available if score(i) == best_score]
    return rng.choice(best_moves)          # tie-break emulates human blind pick
```

Estimating "how many moves a human would take to finish" is technically not done by running the solver, but by running such a greedy player (possibly repeatedly with different `rng`, to get a distribution of move counts rather than a single number) — as opposed to the "optimal solver" from sections 3–4, which looks for any solution at all, or the shortest one.

## 6. Generating guaranteed-solvable levels by reverse construction

This is the key technique: instead of "generate a random pile → check with a solver → discard if unsolvable" (generate-and-check, expensive for a problem assumed to be NP-complete), we build the pile **in the reverse order of removal**. Take the fully cleared pile (an empty field) and "hand out" items backward, step by step performing the operations inverse to the player's moves; then the handing-out sequence itself is already a ready-made solution, and solvability is guaranteed by construction, not by checking.

- The article [Dan Q, «FlipFlop Solitaire's Deck-Generation Secret», 2026](https://danq.me/2026/04/18/flipflop-solitaires-deck-generation-secret/) formulates the technique in general form for solitaire games: "start with a "solved" deck… and perform a randomly-selected series of valid reverse-moves" — start from a deck laid out per the winning rules and apply a random sequence of reverse moves; the author directly calls this turning the problem "check that a layout is winnable" (NP-hard in general) into the problem "build a winnable layout" (polynomial, because we ourselves choose the steps).
- For Mahjong Solitaire the same technique is implemented in open code: the repository [dAmihl/MyJong](https://github.com/dAmihl/MyJong) (Godot) explicitly offers a choice between "a fully random layout" and "an always-solvable layout" ("always-solvable tile placement" — per the project description); the repository [cchaiyatad/mahjong-solitaire-solver](https://github.com/cchaiyatad/mahjong-solitaire-solver) is stated to generate a solvable board and solve it in the same pass, based on algorithms from T. Stam's paper "Solving Mahjong Solitaire Positions" (available at `iivq.net/scriptie/scriptie-bsc.pdf`; the PDF's content could not be opened with the available tools — the file is served as binary data with no extractable text, so the details of its algorithm are not retold here, only the fact of its use and the citing paper).
- The Wikipedia article on [Mahjong solitaire](https://en.wikipedia.org/wiki/Mahjong_solitaire) separately explains why a solvability guarantee is needed at all: "An analysis of ten million games with the default layout, 'the turtle', found that about 3 percent of the turtles cannot be solved even when looking below tiles is allowed" — meaning that with a purely random deal of the classic layout, about 3% of games are not solvable even with full knowledge of the pile's contents; hence the practice that "many implementations do not let a win become impossible even with move undo" (same source).
- A general academic survey of techniques (not about Mahjong specifically, but about procedural puzzle generation in general) highlights reverse search ("find the initial state by generating the solution in advance") as one of three typical puzzle-generation strategies alongside "generate-and-check" and direct constructive generation under constraints; source — a survey of procedural puzzle generation, see the cited work on Wave Function Collapse algorithms and related methods for generating puzzle levels (access confirmed via web search, not by direct reading of the article, so the wording is given in general form without an exact quote).

Applied to our mechanic (layers + a 9-slot shelf), a reverse move looks like this: at each step of the "reverse handout" we virtually return one removed item back into the pile — either onto an empty (visible) position, or on top of an already-placed item (creating occlusion), or we restore a triple on the shelf, splitting it back into three items and moving them from the shelf into the pile. The order of these reverse operations is chosen randomly (with probability biases toward the desired occlusion density and layer depth — see section 8), and the sequence of forward moves, being the reverse of the handout order, is itself a ready-made proof of solvability.

```python
import random


def generate_solvable_pile(num_kinds: int, copies_per_kind: int, rng: random.Random) -> "Pile":
    """Reverse construction: start from an empty pile and 'undo' removals.
    copies_per_kind must be a multiple of 3 (see section 7)."""
    assert copies_per_kind % 3 == 0

    pool = []
    for kind in range(num_kinds):
        pool += [kind] * copies_per_kind
    rng.shuffle(pool)                       # order in which items will be "un-removed"

    pile = Pile.empty()
    open_positions: list[int] = []          # positions with no item on top yet

    for kind in pool:
        pos = choose_reverse_position(pile, open_positions, rng)  # see section 8 for density knobs
        pile = pile.place_item(pos, kind)
        open_positions = pile.recompute_open_positions()

    # The reverse order of `pool` (grouped by shelf-triples) is a certificate
    # that this pile is solvable: playing items in that order always wins.
    return pile
```

## 7. Multiple of three

Each item kind vanishes from the shelf only in triples. If the final count of some kind in the pile is not a multiple of three, then after clearing all full triples, one or two "leftover" copies of that kind will remain, which can never form a triple — meaning either they physically cannot be removed from the pile (if the pile must be fully cleared), or they will guaranteedly occupy a shelf slot until the end of the game, pushing it toward overflow. Either way this makes the level unsolvable **under any play strategy**, i.e., it is a necessary (though not the only — the occlusion graph must also be consistent) condition for solvability, which is cheapest to check once at level generation rather than to search for with a solver. This conclusion needs no external source — it follows directly from the formal rule "three identical items vanish" (section 1.2) and is pure combinatorics (a number not a multiple of 3 cannot be represented as a sum of threes with no remainder).

```python
def validate_multiple_of_three(pile: "Pile") -> None:
    from collections import Counter
    counts = Counter(item.kind for item in pile.items.values())
    bad = {kind: n for kind, n in counts.items() if n % 3 != 0}
    if bad:
        raise ValueError(f"Level is unsolvable by construction: counts not divisible by 3: {bad}")
```

With procedural level generation by reverse construction (section 6), this condition is satisfied automatically if the generator places items into the pile in batches of three copies of one kind (as in the `generate_solvable_pile` example above — `copies_per_kind` is explicitly checked for a multiple of 3 before generation). The danger arises mainly not at the generation stage, but at the stage of **manually editing** a level (a designer moved/removed one item by hand) — this is where the property test "after any modification, the count of each item kind is a multiple of 3" is needed, covered in the file `02-level-format-and-property-tests.md`.

## 8. Difficulty tuning

There are no direct scientific papers on difficulty parameters specifically for the "pile + shelf" mechanic; the parameters collected below come from write-ups of specific commercial games in this genre.

- **Number of item kinds and layer depth.** The browser version of Zen Match's Chrome Web Store listing directly describes its difficulty progression: "difficulty increases with more tile types and stacked layers, with a layer mechanic where, from level 3 onward, tiles stack on top of each other" (per the extension page [Zen Match — Tile Puzzle, Chrome Web Store](https://chromewebstore.google.com/detail/zen-match-tile-puzzle/ngbebofjmipghhicjloijdnjdnkkhlpd), paraphrased via search).
- **Occlusion density and hidden layers.** The [Deconstructor of Fun write-up on Zen Match](https://www.deconstructoroffun.com/blog/2023/10/15/zen-match-how-a-first-mover-falls-behind) introduces the notion of "Layer 3" — tiles fully hidden from the player — and explicitly ties their count to difficulty: the more such tiles, the more "blind" moves the player is forced to make; the article's author directly criticizes Zen Match for "populates Layer 3 much more than other Tile Match games," and recommends "depopulate Layer 3" as a way to lower artificial difficulty that doesn't correspond to player skill.
- **Obstructions (obstacles) as a separate difficulty lever**, independent of pure layer occlusion: the [Gamigion write-up on Tile Busters](https://www.gamigion.com/game-analysis-spyke-games-tile-busters/) lists obstacle types ("grass, sticky gel, chains, curtains, frogs, and bombs," as well as "crates, ice blocks, and chained tiles"), which "not only conceal tiles but also limit the available space" — that is, they control difficulty separately from both the number of kinds and layer depth.
- **Move budget / helper tools as a compensating lever.** Zen Match's official support page describes the shelf as a capacity with a fill limit ("matching 3-of-a-kind in your tile holder… but overloading the tile holder ends the round," per [support.zenmatchgame.com](https://support.zenmatchgame.com/hc/en-us/articles/9516848294034-How-do-I-play-Zen-Match)); the Chrome Web Store browser version explicitly states both the shelf's numeric capacity and the number of helper tools per level: "7 slots and it's game over," "undo… 3 uses per level," "shuffle… 1 use per level."
- **Two-phase level structure** as a technique against overly early frustrating failure: the Zen Match write-up notes that "every level comes in two waves. The first wave is always very easy and can be solved in under a minute" — that is, difficulty within a single level rises over the course of play rather than staying constant.
- **A metric for tuning, not just pass/fail.** A general level-design article for this game family ([Room8Studio, «Smart & Casual: How to Build Match 3 Games Level Design»](https://room8studio.com/news/smart-casual-the-state-of-tile-puzzle-games-level-design-part-1/)) advises "switching from the pass rate to defining a number of attempts per level" and tracking "average and median attempts" — that is, validating difficulty not by a binary "is it solvable" but by the distribution of attempt/move counts of an emulated (or real) player, which corresponds directly to the heuristic run from section 5.
- An academic paper on automated validation of generated match-3 levels, [«Improving Conditional Level Generation using Automated Validation in Match-3 Games» (Avalon), arXiv:2409.06349](https://arxiv.org/html/2409.06349v2), gives a quantitative example of this practice: a deterministic bot plays each generated level 30 times, gets a budget of 39 moves (whereas the designer-set limit is 20 moves, the difference leaving room for further tuning), and **the median move count of the bot over 30 runs** is used as a numeric difficulty measure when training a generative model; the authors note that a "random" bot fails all training levels within 20 moves, while their heuristic bot's results are comparable to a human tester.

## 9. Open code: solvers and generators

| Repository | Language | What it does | Assessment |
|---|---|---|---|
| [NB-Dragon/SheepSolver](https://github.com/NB-Dragon/SheepSolver) | Python | A "羊了个羊" (Sheep a Sheep) solver via backtracking search; 6 move-selection strategies (by index, by layer, "normal," random); the README honestly states that without using in-game hint items, solving efficiency depends heavily on luck. | The only open solver found specifically for the target "layers + shelf" mechanic, in Python. Simple, no stated time-performance guarantees; useful as a reference for the `ordered_candidate_moves`/heuristics functions of sections 4–5, but not as a ready-made library for integration. |
| [opendilab/DI-sheep](https://github.com/opendilab/DI-sheep) | Python | Trains a deep reinforcement-learning agent (RL, PPO) to play "羊了个羊"; includes a Flask service and a gym-compatible environment `sheep_env.py`. | Shows that not only exact search but also trainable policies are applied to this mechanic — useful as a source of inspiration for stochastic "difficulty for a human" estimation, but not as an exact solver: the repository page does not serve `sheep_env.py`'s source code through the available viewing tools, so state-representation details could not be confirmed. |
| [zc2638/ylgy](https://github.com/zc2638/ylgy) | not verified while reading (the repository page was not opened via WebFetch, only the header from search) | Automation of a full "羊了个羊" clear ("通关程序，支持自动通关"), the project is marked as finished (期间/archived). | Not checked directly, given only per the search result's header — not to be used as a confirmed source for the algorithm, only as a lead for further study. |
| [de Bondt, arXiv:1203.6559](https://arxiv.org/abs/1203.6559) (not a repository, but a paper describing an algorithm) | — | A practical algorithm for solving Mahjong Solitaire "with peeking": DFS with pruning and a heuristic for prioritizing "critical groups." | Not open code in the sense of a repository, but the only source found describing a specific effective algorithm for a related mechanic at the level of a paper, rather than a README. |
| [cchaiyatad/mahjong-solitaire-solver](https://github.com/cchaiyatad/mahjong-solitaire-solver) | Go (not Python — important: the initial search mistakenly took it for a Python project) | A solvable Mahjong Solitaire board generator + solver using algorithms from T. Stam's paper; a REST API with Random/MaxBlock heuristics and Random/MultipleFirst strategies; the README states directly that generation "sometimes it fails" — meaning the solvability guarantee is not absolute but probabilistic/with retries. | Useful as an example of a "generator + solver behind one HTTP API" architecture and as a bridge to the primary source (T. Stam), but written in Go, not suitable for directly borrowing Python code. |
| [dAmihl/MyJong](https://github.com/dAmihl/MyJong) | Godot (GDScript, per indirect evidence; the project's language was not confirmed by directly viewing the source) | A Mahjong Solitaire board game with an explicit toggle between a fully random and an "always-solvable" (guaranteed solvable) layout. | Useful mainly as one more precedent of a conscious split between "random generation" / "reverse-construction generation" at the level of a user setting — meaning this fork appears in real products, not invented for this task. |
| [ffalt/mah](https://github.com/ffalt/mah) | Angular/TypeScript | An open web implementation of Mahjong Solitaire with a seed-based board generator (for reproducibility and "share a layout"), 3 generation difficulty levels. | The README does not disclose the solvability-guarantee algorithm (the publicly visible text has no details), so the presence of reverse construction in it cannot be assessed directly — given as an example of a product with configurable generation difficulty, not as a verified algorithm source. |

## Sources

- [de Bondt, «Solving Mahjong Solitaire boards with peeking», arXiv:1203.6559](https://arxiv.org/abs/1203.6559)
- [de Bondt, «Solving Shisen-Sho boards», arXiv:2010.09014](https://arxiv.org/abs/2010.09014)
- [Hoogeboom, Kosters, van Rijn, Vis, «Acyclic Constraint Logic and Games», ICGA Journal, arXiv:1604.05487](https://arxiv.org/pdf/1604.05487)
- [van Rijn, «Playing Games: The complexity of Klondike, Mahjong, Nonograms and Animal Chess», LIACS Master's thesis, 2012](https://theses.liacs.nl/398) (PDF: https://theses.liacs.nl/pdf/2012-01JanvanRijn_2.pdf)
- [Helmert, «Complexity results for standard benchmark domains in planning», Artificial Intelligence 143(2), 2003](https://www.sciencedirect.com/science/article/abs/pii/S0004370202003648)
- [«Spider Solitaire is NP-Complete», arXiv:1110.1052](https://arxiv.org/abs/1110.1052)
- [Biedl, Demaine, Demaine, Fleischer, Jacobsen, Munro, «The Complexity of Clickomania», arXiv:cs/0107031](https://arxiv.org/abs/cs/0107031)
- [«Clickomania is Hard, Even with Two Colors and Columns», MOVES 2015](https://erikdemaine.org/papers/Clickomania_MOVES2015/paper.pdf)
- [NB-Dragon/SheepSolver (GitHub)](https://github.com/NB-Dragon/SheepSolver)
- [opendilab/DI-sheep (GitHub)](https://github.com/opendilab/DI-sheep)
- [zc2638/ylgy (GitHub)](https://github.com/zc2638/ylgy)
- [cchaiyatad/mahjong-solitaire-solver (GitHub)](https://github.com/cchaiyatad/mahjong-solitaire-solver)
- [dAmihl/MyJong (GitHub)](https://github.com/dAmihl/MyJong)
- [ffalt/mah (GitHub)](https://github.com/ffalt/mah)
- [Dan Q, «FlipFlop Solitaire's Deck-Generation Secret», 2026](https://danq.me/2026/04/18/flipflop-solitaires-deck-generation-secret/)
- [Mahjong solitaire — Wikipedia](https://en.wikipedia.org/wiki/Mahjong_solitaire)
- [阮一峰, «羊了个羊，如何自己实现»](https://www.ruanyifeng.com/blog/2022/10/sheep-n-sheep.html)
- [Deconstructor of Fun, «The Zen Match Case: How a First Mover Fell Behind», 2023](https://www.deconstructoroffun.com/blog/2023/10/15/zen-match-how-a-first-mover-falls-behind)
- [Gamigion, «Game Analysis: Spyke Games' Tile Busters»](https://www.gamigion.com/game-analysis-spyke-games-tile-busters/)
- [Zen Match — Tile Puzzle, Chrome Web Store](https://chromewebstore.google.com/detail/zen-match-tile-puzzle/ngbebofjmipghhicjloijdnjdnkkhlpd)
- [Zen Match support: «How do I play Zen Match?»](https://support.zenmatchgame.com/hc/en-us/articles/9516848294034-How-do-I-play-Zen-Match)
- [Room8Studio, «Smart & Casual: How to Build Match 3 Games Level Design»](https://room8studio.com/news/smart-casual-the-state-of-tile-puzzle-games-level-design-part-1/)
- [«Improving Conditional Level Generation using Automated Validation in Match-3 Games», arXiv:2409.06349](https://arxiv.org/html/2409.06349v2)
- T. Stam, «Solving Mahjong Solitaire Positions» — mentioned via the README of [cchaiyatad/mahjong-solitaire-solver](https://github.com/cchaiyatad/mahjong-solitaire-solver); the direct URL `http://iivq.net/scriptie/scriptie-bsc.pdf` could not have its text extracted with the available tools.