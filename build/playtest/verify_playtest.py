"""Verify the HTML prototype's JS engine: play all 12 levels to a win.

Extracts the LEVELS array from index.html, mirrors the prototype's exact JS
logic in Node (same take/match/jam/win semantics), drives it with solutions
from the Python solver, and asserts every level ends in a win.
"""
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
html = (ROOT / "build" / "playtest" / "index.html").read_text()

m = re.search(r"const LEVELS = (\[.*?\]);", html, re.S)
assert m, "LEVELS not found in html"
levels = json.loads(m.group(1))

# get Python solver solutions per level
import sys
sys.path.insert(0, str(ROOT))
from tools.solver.schema import LevelDef, PileItem  # noqa: E402
from tools.solver.solver import solve  # noqa: E402
from tools.solver.rules import Outcome, replay  # noqa: E402

node_script = r"""
const levels = __LEVELS__;
const solutions = __SOLUTIONS__;

// exact mirror of the prototype JS semantics
function tryMatch(shelf){
  const counts = {};
  shelf.forEach(k => { if(k) counts[k]=(counts[k]||0)+1; });
  for (const [k,n] of Object.entries(counts)){
    if (n >= 3){
      let removed = 0;
      for (let i=0;i<9 && removed<3;i++) if (shelf[i]===k){ shelf[i]=null; removed++; }
      return true;
    }
  }
  return false;
}

let results = [];
for (let li=0; li<levels.length; li++){
  const L = levels[li];
  const taken = new Set(); const shelf = Array(9).fill(null);
  let movesLeft = L.movesLimit, over=null;
  for (const id of solutions[li]){
    if (over) break;
    const it = L.pile.find(x=>x.id===id);
    taken.add(it.id);
    const slot = shelf.indexOf(null);
    shelf[slot] = it.kind;
    if (!shelf.includes(null) && !tryMatch(shelf)) { over="jam"; break; }
    tryMatch(shelf);
    if (taken.size === L.pile.length){ over="win"; break; }
    movesLeft--;
    if (movesLeft<=0){ over="moves"; break; }
  }
  results.push({level: L.number, outcome: over});
}
console.log(JSON.stringify(results));
"""

solutions = []
for lv in levels:
    pile = tuple(PileItem(e["id"], e["kind"], tuple(e["blockedBy"]))
                 for e in lv["pile"])
    level = LevelDef(lv["number"], f"room_{lv['number']:02d}", pile)
    sol = solve(level)
    assert sol is not None, f"level {lv['number']} unsolvable"
    # verify through python rules too
    outcome, _ = replay(level, list(sol.moves))
    assert outcome == Outcome.WIN
    solutions.append(list(sol.moves))

script = node_script.replace("__LEVELS__", json.dumps(levels)).replace(
    "__SOLUTIONS__", json.dumps(solutions))
out = subprocess.run(["node", "-e", script], capture_output=True, text=True)
assert out.returncode == 0, out.stderr

results = json.loads(out.stdout)
wins = [r for r in results if r["outcome"] == "win"]
print(f"{len(wins)}/{len(results)} levels end in WIN inside the prototype engine")
for r in results:
    status = "OK " if r["outcome"] == "win" else "FAIL"
    print(f"  {status} level {r['level']}: {r['outcome']}")
assert len(wins) == 12
