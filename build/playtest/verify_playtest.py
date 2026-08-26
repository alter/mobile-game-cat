"""Verify the HTML prototype: play all 37 levels to a win, on gate 3.7 rules.

Extracts two things from index.html: the embedded LEVELS array and the marked
`rules` block of the prototype's own JS. The rules block is run in node as-is —
not re-typed here, because a hand-copied mirror can drift from the page it
claims to check (the previous version of this file mirrored a `movesLimit` the
page no longer had, so the check passed on code that did not exist).

Solutions come from the Python solver, and every order is replayed through
tools/solver/rules.py first, so both engines must agree.

Gate 3.7 runs on the visible pile: the embedded levels must carry no locked
kinds and no move limit.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
html = (ROOT / "build" / "playtest" / "index.html").read_text()

m = re.search(r"const LEVELS = (\[.*?\]);", html, re.S)
assert m, "LEVELS not found in html"
levels = json.loads(m.group(1))

rules_js = re.search(r"// --- rules:.*?---\n(.*?)// --- end rules ---", html, re.S)
assert rules_js, "rules block not found in html"
rules_js = rules_js.group(1)

assert len(levels) == 37, f"expected 37 levels, got {len(levels)}"
assert sorted({len(lv["pile"]) for lv in levels}) == [36, 48, 60], "pile-size curve changed"
assert "movesLimit" not in html, "D1: no move limit anywhere"
for lv in levels:
    for e in lv["pile"]:
        assert set(e) == {"id", "kind", "blockedBy"}, \
            f"level {lv['number']}: unexpected item fields {sorted(e)}"

sys.path.insert(0, str(ROOT))
from tools.solver.schema import LevelDef, PileItem  # noqa: E402
from tools.solver.solver import solve  # noqa: E402
from tools.solver.rules import Outcome, new_state, replay  # noqa: E402

node_script = """
__RULES__

const levels = __LEVELS__;
const solutions = __SOLUTIONS__;

const results = [];
for (let li = 0; li < levels.length; li++) {
  const L = levels[li];
  const state = newState();
  const byId = {};
  for (const it of L.pile) byId[it.id] = it;
  for (const id of solutions[li]) {
    if (state.outcome) break;
    if (!takeItem(L, state, byId[id])) { state.outcome = "illegal:" + id; break; }
  }
  results.push({level: L.number, outcome: state.outcome, triples: state.triples});
}
console.log(JSON.stringify(results));
"""

solutions = []
for lv in levels:
    pile = tuple(PileItem(e["id"], e["kind"], tuple(e["blockedBy"]))
                 for e in lv["pile"])
    level = LevelDef(lv["number"], lv["roomId"], lv.get("pileIndex", 0), pile)
    sol = solve(level)
    assert sol is not None, f"level {lv['number']} unsolvable"
    outcome, _ = replay(level, list(sol.moves))
    assert outcome == Outcome.WIN, f"level {lv['number']}: python says {outcome}"
    solutions.append(list(sol.moves))

script = (node_script
          .replace("__RULES__", rules_js)
          .replace("__LEVELS__", json.dumps(levels))
          .replace("__SOLUTIONS__", json.dumps(solutions)))
out = subprocess.run(["node", "-e", script], capture_output=True, text=True)
assert out.returncode == 0, out.stderr

results = json.loads(out.stdout)
wins = [r for r in results if r["outcome"] == "win"]
print(f"{len(wins)}/{len(results)} levels end in WIN inside the prototype engine")
for r in results:
    status = "OK " if r["outcome"] == "win" else "FAIL"
    print(f"  {status} level {r['level']}: {r['outcome']}")
assert len(wins) == 37

# Winning orders alone prove very little: an engine that never jams passes them
# all. The 2026-08-24 review caught exactly this in the C# conformance test.
# So drive every level into a jam as well and check the two engines still agree.
jam_scripts = []
for lv in levels:
    pile = tuple(PileItem(e["id"], e["kind"], tuple(e["blockedBy"]))
                 for e in lv["pile"])
    level = LevelDef(lv["number"], lv["roomId"], lv.get("pileIndex", 0), pile)
    state = new_state(level)
    order = []
    while not state.over:
        available = state.available()
        if not available:
            break
        held = {}
        for kind in state.shelf:
            if kind:
                held[kind] = held.get(kind, 0) + 1
        # spread the shelf as thin as possible: the fastest way to a jam
        available.sort(key=lambda item: (held.get(item.kind, 0), item.id))
        order.append(available[0].id)
        state.take(available[0].id)
    assert state.outcome is Outcome.SHELF_JAMMED, \
        f"level {lv['number']}: python expected a jam, got {state.outcome}"
    jam_scripts.append(order)

script = (node_script
          .replace("__RULES__", rules_js)
          .replace("__LEVELS__", json.dumps(levels))
          .replace("__SOLUTIONS__", json.dumps(jam_scripts)))
out = subprocess.run(["node", "-e", script], capture_output=True, text=True)
assert out.returncode == 0, out.stderr

# the prototype names it "jam", rules.py names it "shelf_jammed"
jams = [r for r in json.loads(out.stdout) if r["outcome"] == "jam"]
print(f"{len(jams)}/37 levels also jam identically in both engines")
assert len(jams) == 37
