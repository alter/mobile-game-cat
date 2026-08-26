"""Build a self-contained playable HTML prototype of the puzzle.

Embeds all 37 shipped levels from game/Assets/Resources/Levels — the same files
the player loads (View/LevelAssets.cs). Output: build/playtest/index.html

This build serves gate 3.7 (five outsiders), which runs on the VISIBLE pile:
kinds are always shown and blocked items are dimmed, not blanked. Hiding (3.9)
and locked kinds (3.11) come after this gate, so `locked_after_triples` is
dropped on the way in and nothing is buried. The pile-size curve (36 / 48 / 60)
and the piles-per-room pacing are the shipped ones, untouched.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEVELS = ROOT / "game" / "Assets" / "Resources" / "Levels"

levels = []
for f in sorted(LEVELS.glob("l*.json")):
    data = json.loads(f.read_text())
    levels.append({
        "number": data["number"],
        "roomId": data["room_id"],
        "pileIndex": data.get("pile_index", 0),
        # locked_after_triples deliberately not carried over: see module docstring
        "pile": [{"id": e["id"], "kind": e["kind"],
                  "blockedBy": e.get("blocked_by", [])}
                 for e in data["pile"]],
    })

if not levels:
    raise SystemExit(f"no levels found in {LEVELS}")

html = """<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, user-scalable=no">
<title>Разбор завала — прототип</title>
<style>
  :root { --bg:#f4ead8; --shelf:#e8d9bd; --ink:#4a3b28; }
  * { box-sizing:border-box; -webkit-tap-highlight-color:transparent; }
  body { margin:0; font-family:-apple-system,sans-serif; background:var(--bg); color:var(--ink);
         display:flex; flex-direction:column; align-items:center; min-height:100vh;
         padding-bottom:14px; }
  h1 { font-size:17px; margin:12px 0 4px; font-weight:600; }
  #status { font-size:13px; margin-bottom:6px; opacity:.75; }
  #roombar { display:flex; gap:2px; width:340px; margin-bottom:10px; }
  .rseg { flex:1; min-width:0; height:6px; border-radius:3px; background:rgba(0,0,0,.12); }
  .rseg.done { background:#7f9e7a; }
  .rseg.now { background:var(--ink); }
  .rseg.roomstart { margin-left:6px; }
  #pile { display:flex; flex-wrap:wrap; gap:5px; justify-content:center; align-content:flex-start;
          width:340px; max-height:50vh; overflow-y:auto; margin-bottom:14px; padding:8px;
          background:rgba(0,0,0,.06); border-radius:12px; min-height:110px; }
  .item { width:38px; height:38px; border-radius:8px; border:none; padding:0;
          display:flex; align-items:center; justify-content:center;
          font-size:12px; font-family:inherit; color:#fff; cursor:pointer; }
  /* blocked = still covered: kind stays visible, tile is dimmed (gate 3.7) */
  .item.blocked { opacity:.35; cursor:default; }
  #shelf { display:grid; grid-template-columns:repeat(3,46px); gap:5px;
           background:var(--shelf); padding:8px; border-radius:10px; }
  .slot { width:46px; height:46px; border-radius:8px; background:rgba(255,255,255,.5); }
  #answerlink { margin-top:14px; font-size:13px; color:var(--ink); opacity:.6;
                background:none; border:none; text-decoration:underline; font-family:inherit; }
  #overlay { position:fixed; inset:0; background:rgba(0,0,0,.55); padding:16px;
             display:none; align-items:center; justify-content:center; z-index:2; }
  #card { background:#fff; border-radius:16px; padding:22px; max-width:330px;
          max-height:90vh; overflow-y:auto; text-align:center; font-size:15px; line-height:1.45; }
  #card p { margin:8px 0 0; font-size:14px; }
  button.act { display:block; width:100%; margin-top:10px; padding:11px 16px; border:none;
               border-radius:10px; background:var(--ink); color:#fff; font-size:15px;
               font-family:inherit; cursor:pointer; }
  button.act.quiet { background:none; color:var(--ink); opacity:.6; text-decoration:underline; }
  textarea { width:100%; height:70px; margin-top:10px; font-size:14px; font-family:inherit;
             border-radius:8px; border:1px solid rgba(0,0,0,.2); padding:6px; }
  #answercopy { font-size:12px; text-align:left; white-space:pre-wrap; margin-top:10px;
                background:rgba(0,0,0,.05); border-radius:8px; padding:8px; }
</style>
</head>
<body>
<h1 id="title"></h1>
<div id="status"></div>
<div id="roombar"></div>
<div id="pile"></div>
<div id="shelf"></div>
<button id="answerlink" onclick="askQuestion()">Закончить и ответить на вопрос</button>

<div id="overlay"><div id="card"></div></div>

<script>
const LEVELS = __LEVELS__;

// --- rules: verify_playtest.py extracts this exact block and runs it in node ---
// Mirror of Core/Board.cs and tools/solver/rules.py. No move limit (D1). No
// locked kinds in this build, so rules.py's "nothing available" jam cannot
// fire: blocked_by is acyclic, so some item is always free.
const SHELF_SLOTS = 9;

function newState() {
  return { taken: new Set(), shelf: Array(SHELF_SLOTS).fill(null), triples: 0, outcome: null };
}
function isFree(state, item) {
  return !state.taken.has(item.id) && item.blockedBy.every(b => state.taken.has(b));
}
function freeIds(level, state) {
  const s = new Set();
  for (const it of level.pile) if (isFree(state, it)) s.add(it.id);
  return s;
}
function tryMatch(state) {
  const counts = {};
  state.shelf.forEach(k => { if (k) counts[k] = (counts[k] || 0) + 1; });
  for (const [k, n] of Object.entries(counts)) {
    if (n >= 3) {
      let removed = 0;
      for (let i = 0; i < state.shelf.length && removed < 3; i++)
        if (state.shelf[i] === k) { state.shelf[i] = null; removed++; }
      return true;
    }
  }
  return false;
}
function takeItem(level, state, item) {
  if (state.outcome || !isFree(state, item)) return false;
  state.taken.add(item.id);
  // win before jam: an emptied pile wins even if that item would fill the shelf
  if (state.taken.size === level.pile.length) { state.outcome = "win"; return true; }
  state.shelf[state.shelf.indexOf(null)] = item.kind;
  if (tryMatch(state)) state.triples++;
  else if (!state.shelf.includes(null)) state.outcome = "jam";
  return true;
}
// --- end rules ---

// Hue by golden angle so kinds stay distinct well past 20 kinds.
function kindStyle(kind) {
  let n = parseInt(kind.replace("prop_", ""), 10);
  if (isNaN(n)) { let h = 0; for (let i = 0; i < kind.length; i++) h = (h * 31 + kind.charCodeAt(i)) >>> 0; n = h; }
  const hue = (n * 137.508) % 360;
  return { bg: `hsl(${hue.toFixed(1)}, 38%, 62%)`, label: String(n) };
}

const ROOM_ORDER = [...new Set(LEVELS.map(l => l.roomId))];

let levelIdx = 0, state = newState();
let jams = 0, boosterTaps = 0, cleared = 0;

function startLevel(i) {
  levelIdx = i;
  state = newState();
  const L = LEVELS[i];
  document.getElementById("title").textContent =
    `Комната ${ROOM_ORDER.indexOf(L.roomId) + 1} из ${ROOM_ORDER.length} · завал ${L.pileIndex + 1}`;
  renderRoomBar();
  render();
}

function renderRoomBar() {
  const bar = document.getElementById("roombar");
  bar.innerHTML = "";
  LEVELS.forEach((l, i) => {
    const seg = document.createElement("div");
    seg.className = "rseg";
    if (i < levelIdx) seg.classList.add("done");
    if (i === levelIdx) seg.classList.add("now");
    if (l.pileIndex === 0 && i > 0) seg.classList.add("roomstart");
    bar.appendChild(seg);
  });
}

function onTake(level, item) {
  if (!takeItem(level, state, item)) return;
  if (state.outcome === "win") return finishWin();
  if (state.outcome === "jam") { jams++; return showJam(); }
  render();
}

function finishWin() {
  cleared++;
  const L = LEVELS[levelIdx];
  const next = LEVELS[levelIdx + 1];
  if (!next) return finalScreenAll();
  if (next.roomId !== L.roomId) {
    return showCard(`<b>Комната чистая!</b><p>Котёнку лучше.</p>` +
      `<button class="act" onclick="nextLevel()">Дальше</button>`);
  }
  showCard(`<b>Угол убран!</b><p>В комнате ещё остался завал.</p>` +
    `<button class="act" onclick="nextLevel()">Продолжить</button>`);
}

// D4: the booster is a fake door — the tap is counted, nothing is granted,
// the завал stays lost and is replayed from the start.
function showJam() {
  showCard(`<b>Полка переполнена.</b>` +
    `<p>Все девять мест заняты, а тройка не собирается.</p>` +
    `<button class="act" onclick="tapBooster()">Поставить ещё одну полку</button>` +
    `<button class="act" onclick="replayLevel()">Разобрать завал заново</button>` +
    `<button class="act quiet" onclick="askQuestion()">Закончить и ответить на вопрос</button>`);
}

function tapBooster() {
  boosterTaps++;
  showCard(`<b>Ещё одна полка — скоро.</b>` +
    `<p>В этой сборке её пока нет. Завал придётся разобрать заново.</p>` +
    `<button class="act" onclick="replayLevel()">Разобрать заново</button>` +
    `<button class="act quiet" onclick="askQuestion()">Закончить и ответить на вопрос</button>`);
}

function replayLevel() { hideCard(); startLevel(levelIdx); }
function nextLevel() { hideCard(); startLevel(levelIdx + 1); }

function finalScreenAll() {
  showCard(`<b>Ты разобрал все завалы в доме!</b>` + questionForm());
}

function askQuestion() {
  showCard(`<b>Последний вопрос.</b>` + questionForm() +
    `<button class="act quiet" onclick="hideCard()">Ещё поиграю</button>`);
}

function questionForm() {
  return `<p><b>Сыграл бы ты дальше, если бы это была настоящая игра?</b></p>` +
    `<textarea id="answer" placeholder="Да или нет и пара слов почему"></textarea>` +
    `<button class="act" onclick="sendAnswer()">Отправить письмом</button>` +
    `<button class="act quiet" onclick="showAnswerText()">Показать текст, чтобы скопировать</button>` +
    `<div id="answercopy" style="display:none"></div>`;
}

function answerText() {
  const text = document.getElementById("answer")?.value || "(пусто)";
  return `Сыграл бы дальше: ${text}\\n` +
    `Разобрано завалов: ${cleared} из ${LEVELS.length}\\n` +
    `Дошёл до завала №: ${levelIdx + 1}\\n` +
    `Полка переполнялась: ${jams}\\n` +
    `Жал «ещё одну полку»: ${boosterTaps}`;
}

function sendAnswer() {
  location.href = "mailto:?subject=Играл бы дальше&body=" + encodeURIComponent(answerText());
}

function showAnswerText() {
  const el = document.getElementById("answercopy");
  el.style.display = "block";
  el.textContent = answerText();
}

function showCard(html) {
  document.getElementById("card").innerHTML = html;
  document.getElementById("overlay").style.display = "flex";
}
function hideCard() { document.getElementById("overlay").style.display = "none"; }

function render() {
  const L = LEVELS[levelIdx];
  const free = freeIds(L, state);
  const pileEl = document.getElementById("pile");
  pileEl.innerHTML = "";
  for (const it of L.pile) {
    if (state.taken.has(it.id)) continue;
    const b = document.createElement("button");
    const s = kindStyle(it.kind);
    b.className = "item" + (free.has(it.id) ? "" : " blocked");
    b.style.background = s.bg;
    b.textContent = s.label;
    if (free.has(it.id)) b.onclick = () => onTake(L, it);
    pileEl.appendChild(b);
  }
  const shelfEl = document.getElementById("shelf");
  shelfEl.innerHTML = "";
  state.shelf.forEach(k => {
    const d = document.createElement("div");
    d.className = "slot";
    if (k) { const s = kindStyle(k); d.style.background = s.bg; }
    shelfEl.appendChild(d);
  });
  document.getElementById("status").textContent =
    `Осталось предметов: ${L.pile.length - state.taken.size} · собрано троек: ${state.triples}`;
}

function begin() { hideCard(); startLevel(0); }

startLevel(0);
showCard(`<b>Разбор завала</b>` +
  `<p>Нажимай на предмет — он ложится на полку. Три одинаковых на полке — исчезают.</p>` +
  `<p>Мест на полке девять. Бледные предметы придавлены сверху: сначала убери то, что лежит на них.</p>` +
  `<p>Если полка забита, а тройки нет — завал не разобран.</p>` +
  `<button class="act" onclick="begin()">Начать</button>`);
</script>
</body>
</html>
"""

out_dir = ROOT / "build" / "playtest"
out_dir.mkdir(parents=True, exist_ok=True)
out = out_dir / "index.html"
out.write_text(html.replace("__LEVELS__", json.dumps(levels, ensure_ascii=False)))
sizes = sorted({len(lv["pile"]) for lv in levels})
print(f"wrote {out} ({out.stat().st_size // 1024} KB), {len(levels)} levels, "
      f"pile sizes {sizes}, from {LEVELS}")
