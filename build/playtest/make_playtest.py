"""Build a self-contained playable HTML prototype of the puzzle.

Embeds the 12 shipped levels and a JS mirror of the rules engine so the
3.7 playtest can run in any phone browser without installing anything.
Output: build/playtest/index.html
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEVELS = ROOT / "game" / "Assets" / "Levels"

levels = []
for n in range(1, 13):
    data = json.loads((LEVELS / f"level_{n:02d}.json").read_text())
    levels.append({
        "number": data["number"],
        "movesLimit": data["moves_limit"],
        "pile": [{"id": e["id"], "kind": e["kind"],
                  "blockedBy": e.get("blocked_by", [])} for e in data["pile"]],
    })

# palette per kind index — muted warm tones, no art, plain tiles
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
  #status { font-size:14px; margin-bottom:8px; }
  #pile { display:flex; flex-wrap:wrap; gap:6px; justify-content:center;
          max-width:360px; margin-bottom:14px; padding:10px;
          background:rgba(0,0,0,.06); border-radius:12px; min-height:120px; }
  .item { width:52px; height:52px; border-radius:10px; border:none;
          display:flex; align-items:center; justify-content:center;
          font-size:11px; color:#fff; cursor:pointer; }
  .item.blocked { opacity:.35; }
  #shelf { display:grid; grid-template-columns:repeat(9,34px); gap:4px;
           background:var(--shelf); padding:8px; border-radius:10px; }
  .slot { width:34px; height:38px; border-radius:7px; background:rgba(255,255,255,.5); }
  .slot.filled { background-size:cover; }
  #overlay { position:fixed; inset:0; background:rgba(0,0,0,.55);
             display:none; align-items:center; justify-content:center; }
  #card { background:#fff; border-radius:16px; padding:24px; max-width:320px;
          text-align:center; font-size:15px; line-height:1.45; }
  button { margin-top:12px; padding:10px 20px; border:none; border-radius:10px;
           background:var(--ink); color:#fff; font-size:15px; }
  textarea { width:100%; height:60px; margin-top:10px; font-size:14px; }
</style>
</head>
<body>
<h1 id="title">Уровень 1 из 12</h1>
<div id="status"></div>
<div id="pile"></div>
<div id="shelf"></div>

<div id="overlay"><div id="card"></div></div>

<script>
const LEVELS = __LEVELS__;

// kind -> color + label (plain rectangles, per debug-view spirit)
const KIND_COLORS = ["#c0765a","#7f9e7a","#6b8cae","#b48ead","#d0a35c",
                     "#8a7f8d","#a26d5f","#6da39c","#9c8a5a"];
function kindStyle(kind) {
  let idx = 0; for (let i = 0; i < kind.length; i++) idx = (idx*31 + kind.charCodeAt(i)) >>> 0;
  const c = KIND_COLORS[idx % KIND_COLORS.length];
  const n = kind.replace("prop_","");
  return { bg:c, label:n };
}

let levelIdx = 0, taken, shelf, over, movesLeft;

function byId(level){ const m={}; for(const it of level.pile) m[it.id]=it; return m; }

function startLevel(i){
  levelIdx = i; const L = LEVELS[i];
  taken = new Set(); shelf = Array(9).fill(null);
  movesLeft = L.movesLimit; over = false;
  document.getElementById("title").textContent = `Уровень ${i+1} из 12`;
  render();
}

function available(level){
  const m = byId(level);
  return level.pile.filter(it => !taken.has(it.id) &&
    it.blockedBy.every(b => taken.has(b)));
}

function take(level, item){
  if (over) return;
  taken.add(item.id);
  const slot = shelf.indexOf(null);
  shelf[slot] = item.kind;
  // full shelf with no match = jam
  if (!shelf.includes(null) && !tryMatch()) return finish("jam");
  tryMatch();
  if (taken.size === level.pile.length) return finish("win");
  movesLeft--;
  if (movesLeft <= 0) return finish("moves");
  render();
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

function finish(how){
  over = true;
  if (how === "win" && levelIdx < LEVELS.length-1){
    showCard(`<b>Комната чистая!</b><br>Котёнок довольствуется.<br><br>` +
      `<button onclick="nextLevel()">Дальше</button>`);
  } else if (how === "win"){
    finalScreen(true);
  } else {
    finalScreen(false, how);
  }
}

function nextLevel(){ hideCard(); startLevel(levelIdx+1); }

function finalScreen(won, how){
  showCard(
    (won ? "<b>Ты прошёл все 12 комнат!</b>"
         : (how==="jam" ? "<b>Полка переполнена.</b>" : "<b>Ходы кончились.</b>")) +
    `<br><br><b>Сыграл бы ты дальше, если бы это была настоящая игра?</b>` +
    `<textarea id="answer" placeholder="Пара слов почему"></textarea>` +
    `<button onclick="sendAnswer(${won})">Отправить</button>`);
}

function sendAnswer(won){
  const text = document.getElementById("answer").value || "(пусто)";
  const body = `Ответ игрока:\\nПрошёл: ${won ? "да" : "нет"}\\n` +
    `Сыграл бы дальше: ${text}`;
  location.href = "mailto:?subject=Играл бы дальше&body=" + encodeURIComponent(body);
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
    b.className = "item" + (availIds.has(it.id) ? "" : " blocked");
    b.style.background = s.bg;
    b.textContent = s.label;
    if (availIds.has(it.id))
      b.onclick = () => take(L, it);
    pileEl.appendChild(b);
  }
  const shelfEl = document.getElementById("shelf");
  shelfEl.innerHTML = "";
  shelf.forEach(k => {
    const d = document.createElement("div");
    d.className = "slot" + (k ? " filled" : "");
    if (k){ const s = kindStyle(k); d.style.background = s.bg; }
    shelfEl.appendChild(d);
  });
  document.getElementById("status").textContent =
    `Осталось предметов: ${L.pile.length - taken.size} · Ходы: ${movesLeft}`;
}

startLevel(0);
</script>
</body>
</html>
"""

out_dir = ROOT / "build" / "playtest"
out_dir.mkdir(parents=True, exist_ok=True)
out = out_dir / "index.html"
out.write_text(html.replace("__LEVELS__", json.dumps(levels, ensure_ascii=False)))
print(f"wrote {out} ({out.stat().st_size // 1024} KB)")
