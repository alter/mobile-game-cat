"""For l32/l34/l35: at risky forks (0<safe<avail) along partial-policy
play, which item ids / kinds most often sit on the UNSAFE side? Used to pick
a data-driven, minimal edit target rather than guessing."""
from __future__ import annotations
import json, random, sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import analyze_solvability as az

from tools.solver.schema import load_level
from tools.solver.rules import new_state, Outcome
from tools.solver.measure import partial_policy

LEVELS_DIR = "game/Assets/Resources/Levels"


def inspect(level_name, n_seeds=25, base_seed=3000, cap_s=10.0):
    lv = load_level(f"{LEVELS_DIR}/{level_name}.json")
    unsafe_item_ctr = Counter()
    unsafe_kind_ctr = Counter()
    fork_step_hist = Counter()
    n_forks = 0
    for s in range(n_seeds):
        rng = random.Random(base_seed + s)
        st = new_state(lv)
        step = 0
        while not st.over:
            avail = st.available()
            if not avail:
                break
            step += 1
            if len(avail) > 1:
                unsafe = []
                for item in avail:
                    trial = new_state(lv)
                    trial.taken, trial.shelf = set(st.taken), list(st.shelf)
                    trial.triples_completed = st.triples_completed
                    trial.take(item.id)
                    ok = (trial.outcome == Outcome.WIN) if trial.over else bool(
                        az.solve_from_state(lv, trial.taken, trial.shelf,
                                            trial.triples_completed, time_cap_s=cap_s))
                    if not ok:
                        unsafe.append(item)
                if unsafe and len(unsafe) < len(avail):
                    n_forks += 1
                    fork_step_hist[step] += 1
                    for item in unsafe:
                        unsafe_item_ctr[item.id] += 1
                        unsafe_kind_ctr[item.kind] += 1
            choices = [(i.id, i.kind) for i in avail]
            best = partial_policy(choices, tuple(st.shelf))
            mv = rng.choice(best)
            st.take(mv)
    return {
        "level": level_name, "n_seeds": n_seeds, "n_forks": n_forks,
        "top_unsafe_items": unsafe_item_ctr.most_common(10),
        "top_unsafe_kinds": unsafe_kind_ctr.most_common(10),
        "fork_step_range": (min(fork_step_hist) if fork_step_hist else None,
                            max(fork_step_hist) if fork_step_hist else None),
    }


def main():
    out = {}
    for name in ["l32_room11_pile2", "l34_room12_pile0", "l35_room12_pile1"]:
        r = inspect(name)
        out[name] = r
        print(json.dumps(r, indent=2))
    Path(str(Path(__file__).parent / "fork_inspect.json")).write_text(json.dumps(out, indent=2))


if __name__ == "__main__":
    main()
