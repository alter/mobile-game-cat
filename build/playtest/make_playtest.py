"""Build a self-contained playable HTML prototype of the puzzle.

Embeds all 37 shipped levels (rooms of 1-4 piles) with hidden kinds (3.9)
and room progress (6.2). Output: build/playtest/index.html
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEVELS = ROOT / "game" / "Assets" / "Levels"

levels = []
for f in sorted(LEVELS.glob("l*.json")):
    data = json.loads(f.read_text())
    levels.append({
        "number": data["number"],
        "roomId": data["room_id"],
        "pileIndex": data.get("pile_index", 0),
        "pile": [{"id": e["id"], "kind": e["kind"],
                  "blockedBy": e.get("blocked_by", []),
                  "lockedAfter": e.get("locked_after_triples", 0)}
                 for e in data["pile"]],
    })

html = """<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, user-scalable=no">
<title>Разбор завала — прототип</title>
<style>
  :root { --bg:#f4ead8; --tile:#c9a97c; --shelf:#e8d9bd; --ink:#4a3b28; }
  * { box-sizing:border-box; -webkit-tap-highlight-color:transparent; }
  body { margin:0; font-family:-apple-system,sans-serif; background:var(--bg); color:var(--ink);
         display:flex; flex-direction:column; align-items:center; min-height:100vh; }
  h1 { font-size:18px; margin:12px 0 4px; }
  #status { font-size:14px; margin-bottom:6px; }
  #roombar { display:flex; gap:4px; margin-bottom:8px; }
  .rseg { width:22px; height:8px; border-radius:4px; background:rgba(0,0,0,.12); }
  .rseg.done { background:#7f9e7a; }
  #pile { display:flex; flex-wrap:wrap; gap:6px; justify-content:center;
          max-width:360px; margin-bottom:14px; padding:10px;
          background:rgba(0,0,0,.06); border-radius:12px; min-height:120px; }
  .item { width:52px; height:52px; border-radius:10px; border:none;
          display:flex; align-items:center; justify-content:center;
          font-size:11px; color:#fff; cursor:pointer; }
  .item.blocked { opacity:.35; }
  .item.hidden { background:#b9a88d !important; color:transparent; cursor:default; }
  #shelf { display:grid; grid-template-columns:repeat(9,34px); gap:4px;
           background:var(--shelf); padding:8px; border-radius:10px; }
  .slot { width:34px; height:38px; border-radius:7px; background:rgba(255,255,255,.5); }
  #overlay { position:fixed; inset:0; background:rgba(0,0,0,.55);
             display:none; align-items:center; justify-content:center; z-index:2; }
  #card { background:#fff; border-radius:16px; padding:24px; max-width:320px;
          text-align:center; font-size:15px; line-height:1.45; }
  button { margin-top:12px; padding:10px 20px; border:none; border-radius:10px;
           background:var(--ink); color:#fff; font-size:15px; }
  textarea { width:100%; height:60px; margin-top:10px; font-size:14px; }
</style>
</head>
<body>
<h1 id="title"></h1>
<div id="status"></div>
<div id="roombar"></div>
<div id="pile"></div>
<div id="shelf"></div>

<div id="overlay"><div id="card"></div></div>

<script>
const LEVELS = __LEVEL__;

// Hue by golden angle so kinds stay distinct well past 20 kinds.
function kindStyle(kind) {
  let n = parseInt(kind.replace("prop_",""), 10);
  if (isNaN(n)) { let h=0; for (let i=0;i<kind.length;i++) h=(h*31+kind.charCodeAt(i))>>>0; n=h; }
  const hue = (n * 137.508) % 360;
  return { bg:`hsl(${hue.toFixed(1)}, 38%, 62%)`, label:String(n) };
}

// rooms in order of first appearance
const ROOM_ORDER = [...new Set(LEVELS.map(l => l.roomId))];
// piles per room for the progress bar
const ROOM_SEGMENTS = {};
for (const r of ROOM_ORDER) ROOM_SEGMENTS[r] = LEVELS.filter(l => l.roomId === r).length;

let levelIdx = 0, taken, shelf, over, triplesDone;

function byId(level){ const m={}; for(const it of level.pile) m[it.id]=it; return m; }

function isLocked(item){
  return item.lockedAfter > 0 && triplesDone < item.lockedAfter;
}
function isRevealed(level, item){
  if (taken.has(item.id)) return false;
  return item.blockedBy.every(b => taken.has(b)) && !isLocked(item);
}
// locked-but-revealed: player sees a seal icon on an otherwise visible tile
function isSealed(item){ return isLocked(item); }

function startLevel(i){
  levelIdx = i;
  taken = new Set(); shelf = Array(9).fill(null);
  triplesDone = 0; over = false;
  const L = LEVELS[i];
  document.getElementById("title").textContent =
    `Комната ${ROOM_ORDER.indexOf(L.roomId)+1} из 12 · завал ${L.pileIndex+1}`;
  renderRoomBar();
  render();
}

function renderRoomBar(){
  const bar = document.getElementById("roombar");
  bar.innerHTML = "";
  const currentRoom = LEVELS[levelIdx].roomId;
  for (const r of ROOM_ORDER){
    for (let p = 0; p < ROOM_SEGMENTS[r]; p++){
      const seg = document.createElement("div");
      seg.className = "rseg";
      // this pile done? whole earlier rooms count as cleared
      const idxInPlan = LEVELS.findIndex(l => l.roomId === r && l.pileIndex === p);
      if (idxInPlan < levelIdx || (idxInPlan === levelIdx && false)) seg.classList.add("done");
      bar.appendChild(seg);
    }
  }
}

function available(level){
  const m = byId(level);
  return level.pile.filter(it => !taken.has(it.id) &&
    it.blockedBy.every(b => taken.has(b)));
}

function tryMatch(){
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

function take(level, item){
  if (over) return;
  taken.add(item.id);
  const slot = shelf.indexOf(null);
  shelf[slot] = item.kind;
  const matched = tryMatch();
  if (matched) triplesDone++;
  if (!matched && !shelf.includes(null)) return finish("jam");
  if (taken.size === level.pile.length) return finish("win");
  render();
}

function finish(how){
  over = true;
  if (how === "win"){
    const L = LEVELS[levelIdx];
    const lastPileOfRoom = !LEVELS[levelIdx+1] || LEVELS[levelIdx+1].roomId !== L.roomId;
    if (lastPileOfRoom){
      showCard(`<b>Комната чистая!</b><br>Котёнку лучше.<br><br>` +
        `<button onclick="nextLevel()">Дальше</button>`);
      return;
    }
    showCard(`<b>Угол убран!</b><br>В комнате ещё остался завал.<br><br>` +
      `<button onclick="nextLevel()">Продолжить</button>`);
  } else {
    showCard(
      "<b>Полка переполнена.</b>" +
      `<br>Прошёл уровней: ${levelIdx} из ${LEVELS.length}<br><br>` +
      `<b>Сыграл бы ты дальше, если бы это была настоящая игра?</b>` +
      `<textarea id="answer" placeholder="Пара слов почему"></textarea>` +
      `<button onclick="sendAnswer(false)">Отправить</button>`);
  }
}

function nextLevel(){ hideCard(); startLevel(levelIdx+1); }

function finalScreenAll(){
  showCard(
    "<b>Ты прошёл все комнаты дома!</b>" +
    `<br><br><b>Сыграл бы ты дальше, если бы это была настоящая игра?</b>` +
    `<textarea id="answer" placeholder="Пара слов почему"></textarea>` +
    `<button onclick="sendAnswer(true)">Отправить</button>`);
}

function sendAnswer(won){
  const text = document.getElementById("answer")?.value || "(пусто)";
  location.href = "mailto:?subject=Играл бы дальше&body=" +
    encodeURIComponent(`Ответ игрока:\\nДошёл до уровня: ${levelIdx+1}\\nСыграл бы дальше: ${text}`);
}

function showCard(html){
  document.getElementById("card").innerHTML = html;
  document.getElementById("overlay").style.display = "flex";
}
function hideCard(){ document.getElementById("overlay").style.display = "none"; }

function render(){
  const L = LEVELS[levelIdx];
  const availIds = new Set(available(L).map(i=>i.id));
  const pileEl = document.getElementById("pile");
  pileEl.innerHTML = "";
  for (const it of L.pile){
    if (taken.has(it.id)) continue;
    const b = document.createElement("button");
    const s = kindStyle(it.kind);
    if (isRevealed(L, it)){
      b.className = "item" + (availIds.has(it.id) ? "" : " blocked");
      b.style.background = s.bg;
      b.textContent = isSealed(it) ? "🔒" : s.label;
      if (availIds.has(it.id) && !isSealed(it)) b.onclick = () => take(L, it);
    } else {
      // task 3.9: buried tile shows nothing
      b.className = "item blocked hidden";
    }
    pileEl.appendChild(b);
  }
  const shelfEl = document.getElementById("shelf");
  shelfEl.innerHTML = "";
  shelf.forEach(k => {
    const d = document.createElement("div");
    d.className = "slot";
    if (k){ const s = kindStyle(k); d.style.background = s.bg; }
    shelfEl.appendChild(d);
  });
  document.getElementById("status").textContent =
    `Осталось предметов: ${L.pile.length - taken.size}`;
}

startLevel(0);
</script>
</body>
</html>
"""

out_dir = ROOT / "build" / "playtest"
out_dir.mkdir(parents=True, exist_ok=True)
out = out_dir / "index.html"
out.write_text(html.replace("__LEVEL__", json.dumps(levels, ensure_ascii=False)))
print(f"wrote {out} ({out.stat().st_size // 1024} KB), {len(levels)} levels")
