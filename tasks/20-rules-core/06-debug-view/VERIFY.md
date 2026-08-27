# VERIFY — 20-rules-core/06-debug-view

Result: **pending** — not passed, not failed. Both VERIFY items are human
observations, neither has been performed, and this verifier is barred from
standing in for them.

Verifier: an independent agent context, 2026-08-26, against `dev` at commit
`27f9904`. It did **not** write `game/Assets/View/DebugGameView.cs`,
`DebugGame.uxml` or `DebugGame.uss`, and changed none of them. It did **not**
play the game, did **not** run a build on a phone, did **not** show ten tiles to
anybody, and did **not** ask anyone to name them. Its only writes were this file
and `labels.txt`.

## Why the label was moved back to `pending`

`labels.txt` in this directory carried `verify:passed` as an uncommitted change
(`git diff` shows `-verify:pending` / `+verify:passed`) with no `VERIFY.md`
beside it. `tasks/README.md`, "The verify:passed rule", forbids that.

More to the point, this task is `ROLE: VIEW, HUMAN` with `VERIFY (HUMAN)`, and
`tasks/README.md`, "The independence rule", says of such tasks: "substitution is
impossible in principle: an agent almost always reports that the result looks
good. Such tasks are not performed and not simulated. They are handed to a
person with a precise statement of what is needed."

So `passed` would be a fabrication and `failed` would be equally false — nobody
has tried and reported a problem. `pending` is the accurate state, and it is
what the committed tree already said.

## What is needed from a person

1. **One level played through on a phone.** Not the editor, not the macOS
   player, not a screenshot: a build on a handset, one level from first tile to
   the win or jam screen. Report the device, and whether anything was unreadable
   or untappable at real size.
2. **Ten kinds named as ten different things, by someone who did not build it.**
   Show the ten tiles side by side at tile size to a person who has not seen the
   code, and write down the ten names they give. Ten distinct names is a pass;
   any two called the same thing is a fail, and the pair should be recorded.

## Supporting facts, which are not a substitute for either item

- The shipped levels use exactly ten kinds, `prop_00` … `prop_09`, across 37
  level files (counted from `game/Assets/Resources/Levels/l*.json`; command in
  "How to reproduce").
- `DebugGameView.HueFor` (`game/Assets/View/DebugGameView.cs:28-38`) spaces hues
  by the golden angle, 137.508°, at fixed saturation 0.38 and value 0.62. For
  those ten kind ids the resulting hues are 0.00, 20.06, 52.52, 105.05, 137.51,
  157.57, 190.03, 242.56, 275.02 and 327.54 degrees; the smallest gap is 20.06°,
  between `prop_00`/`prop_08` and again between `prop_01`/`prop_09`.
  Whether a 20° hue gap at S=0.38 V=0.62 is *told apart at tile size on a
  phone*, by a person, is exactly what item 2 asks and this number does not
  answer.
- `game/Assets/View/DebugGameView.cs` is uncommitted work in progress right now
  (`git status --porcelain game/Assets/View/` shows ` M`), so the artefact a
  human would be asked to judge is still moving. That is a second reason not to
  close this now.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand. These commands
reproduce the supporting facts above; they do not verify the task, which needs a
person and a handset.

```bash
git clone <repo-url> /tmp/verify-06 && cd /tmp/verify-06
git rev-parse --short HEAD          # expect 27f9904 for the numbers above

# the label state this file is about
git log -1 --format=%h -- tasks/20-rules-core/06-debug-view/labels.txt
cat tasks/20-rules-core/06-debug-view/labels.txt

# ten kinds across 37 level files, and the hues the debug view gives them
python3 - <<'EOF'
import json, glob
files = sorted(glob.glob("game/Assets/Resources/Levels/l*.json"))
kinds = sorted({e["kind"] for f in files for e in json.load(open(f))["pile"]})
print("level files:", len(files), "distinct kinds:", len(kinds), kinds)
def hue(k):
    d = "".join(c for c in k if c.isdigit())
    n = int(d) if d else 0
    return (n * 137.508) % 360.0
hs = sorted((hue(k), k) for k in kinds)
for h, k in hs: print(f"{k:9s} hue={h:7.2f}")
gaps = sorted(((hs[(i+1) % len(hs)][0] - hs[i][0]) % 360, hs[i][1], hs[(i+1) % len(hs)][1])
              for i in range(len(hs)))
print("smallest hue gaps:", [(round(g, 2), a, b) for g, a, b in gaps[:3]])
EOF
```

## Update, 2026-08-27: the colour argument above is obsolete

The art arrived, and the tiles are no longer coloured rectangles. Two of the
supporting facts above no longer describe the build:

- Kind ids are the prop names now — `prop_vase`, `prop_book` and so on, thirty
  of them, ten drawn per level — not `prop_00` … `prop_09`. The levels were
  regenerated (`tools/solver/generate.py`, `PROPS`), so a level file names the
  sprite the view loads and there is no table in between.
- `HueFor` is no longer what a player sees. `DebugGameView.MakeTile` paints the
  tile with `Resources.Load<Texture2D>($"Art/{kind}")`; the hue function is kept
  only as the fallback when a kind has no file, so a missing sprite is visible
  rather than invisible. The golden-angle hue arithmetic above therefore
  answers nothing about the current build, including the colour-vision concern
  under "What was not checked" — thirty drawn objects are told apart by shape.

Both human items still stand, and item 2 is now the easier question: ten
drawings of household objects, not ten shades of the same colour.

## What was not checked

- Item 1 in full: nothing was played, on any device or in any editor. No build
  was produced by this verifier.
- Item 2 in full: no tile was rendered and no person was asked to name anything.
  The hue arithmetic above was computed from the source, not sampled from
  pixels, and does not account for the tile size, the phone's display, ambient
  light, or colour-vision deficiency — under which a golden-angle hue ramp at
  fixed saturation is the least robust choice.
- Whether `DebugGameView` renders what `HueFor` returns. Only the colour
  function was read; `game/Assets/View/DebugGameView.cs:194` assigns it to
  `tile.style.backgroundColor`, but the layout, the blanks for hidden items, the
  shelf and the pile were not inspected, and the file is mid-edit.
- Whether the reachable-item and hidden-item rules are honoured on screen. The
  Core side is verified in `02-pile-occlusion/VERIFY.md`; the view side is not.
- `build/playtest/index.html`, the HTML prototype named in this task's CONTEXT,
  was not opened or compared against the Unity view.

---

## Update, 2026-08-27 (later): an emulator run, still pending

Verifier: a third independent context, spawned to re-examine this task after
`tasks/AUDIT-2026-08-27.md` items 9–11 classed it HUMAN-BLOCKED alongside
`60-shell-build/09-notification` and `/10-click-haptics`, and after this
context had a working Android emulator (`emulator-5554`) and an installed
build from `90-android/09-notifications`. Wrote none of
`game/Assets/View/DebugGameView.cs`, `DebugGame.uxml`/`.uss`, `Board.cs`, or
`tools/solver/rules.py`, and did not write the two VERIFY.md sections above.
Did operate the installed app on the emulator directly by hand — `adb shell
input tap`, `adb shell input keyevent`, `adb exec-out screencap` — starting
from `adb shell pm clear com.DefaultCompany.game` (a clean install state, no
save file). Did **not** ask any human to play the game or to name a tile, and
is not claiming to have done so.

### Per-item verdict

| # | Item | Verdict | Basis |
|---|---|---|---|
| 1 | A human plays one level through on a phone | **Still pending — not passable by this context, on any hardware** | See ruling below. What changed: I played level 1 through by hand on the emulator (tapped tiles, filled the shelf, completed a triple — `board.save` showed `triples 1` — and reached a "Shelf jammed / Levels finished: 0 / Replay" outcome card), proving the tap-to-take-to-outcome pipeline works end to end. Screenshot: `android-emulator-shelf-jammed-outcome.png`. This removes "does the plumbing even work" as an open question, but it is not the item: the item names a human, not a mechanism. |
| 2 | Ten kinds side by side at tile size, named as ten different things by someone who did not build it | **Still pending — not passable by this context, on any hardware** | Not attempted, for the same reason as item 1: naming them myself would be exactly the substitution `tasks/README.md`'s independence rule forbids for `role:HUMAN` tasks. What is newly confirmed: the 2026-08-27 update note above (art has replaced coloured rectangles, 30 named props) is accurate — I saw the actual sprite set in play (cutting board, plate, crate, box, book, lamp, jar, ball, scarf, suitcase, comb, hanger, tray, chest, bottle, fork, candle, at minimum) and they are shape- and colour-distinct at the tile size rendered on a 1080×2340 emulator screen. That is my own visual impression, offered as context, not as the required third-party naming test. |

### Two extra conventions, checked with my own eyes as asked

Not part of task.txt's two VERIFY items, but the coordinator asked for them
specifically:

- **D3 — a buried item renders blank, not its kind.** Confirmed. The very
  first screen of a fresh level 1 (`android-emulator-buried-items-blank.png`)
  shows a pile where every non-reachable tile is drawn as the same brown
  hooded/shrouded silhouette (`prop_unknown`) — not the crate, plate, book,
  etc. underneath — while reachable tiles show their real sprite. Matches
  `DebugGameView.MakeTile` lines 223–230 read together with the screenshot.
- **D15 — a locked item is seen, with a corner badge, not hidden.**
  Confirmed, and reached without playing 21 levels by hand: `Update()`
  (`DebugGameView.cs:428-429`) wires `KeyCode.N` to
  `StartLevel(_levelIndex + 1)` for exactly this kind of manual testing, and
  `adb shell input keyevent 42` (Android's `KEYCODE_N`) reaches Unity's
  `Input.GetKeyDown(KeyCode.N)` through the emulator's virtual keyboard — I
  used it to skip from level 1 straight to room 9 (`board.save` reading
  `level 22 room_09 0`), i.e. exactly where DECISIONS.md D15 says locked
  kinds first appear. Room 9, pile 2 (`level 23 room_09 1`) shows a lamp
  (`prop_lamp` or similar) with a small tan rope-coil badge in its lower-right
  corner — the prop fully identifiable underneath, the badge marking it as
  withheld — screenshot `android-emulator-locked-item-visible-room9.png`,
  top row, fourth tile, and again third row, third tile. I did not confirm
  the tap-ignoring half of D15 on this specific tile (the `N`-key scan had
  already advanced past that pile by the time I went to test it, and there is
  no "previous level" hotkey to go back); that half rests on reading
  `DebugGameView.cs:261-264` (`if (available && !locked) ... else
  tile.AddToClassList("game__tile--dim")`), not on an on-device tap test.

### The emulator ruling, argued

`60-shell-build/08-mid-level-save/VERIFY.md` ruled, for an "on device" item,
that an AVD emulator does not satisfy a hardware requirement — it is "a
virtualised OS instance, not a device" — and marked that item **partial**
rather than pass, over and above a second, iOS-specific gap.

That reasoning is about *hardware capability*: whether a background-kill /
process-lifecycle claim, or (per the audit's own framing for the sibling
tasks 9–10 in this same audit) a Taptic Engine or a real notification
scheduler, exists on an emulator. None of that is what this task's two items
need. Item 1 does not name a capability an emulator lacks — I just showed the
tap → take → shelf → triple → jam-card loop runs correctly on `emulator-5554`
end to end. Item 2 does not either; a screen is a screen, and the emulator
renders the same sprites at the same relative size a phone would (modulo the
specific device's screen size and viewing conditions, which is a real but
different gap — see "what was not checked" below).

So the emulator-vs-phone question, read literally against *these* two items,
resolves in the emulator's favour more than it did for `08-mid-level-save`:
nothing here depends on silicon a virtual device lacks. But that does not
unblock the task, because the actual bar these two items set is not
"hardware," it is "a human." `task.txt` says `VERIFY (HUMAN)`, `labels.txt`
says `role:VIEW, HUMAN`, and `tasks/README.md`'s independence rule states
this in terms that leave no room for a substitution argument: "For
role:HUMAN tasks substitution is impossible in principle: an agent almost
always reports that the result looks good. Such tasks are not performed and
not simulated." I am an agent. Handing me a physical phone instead of an
emulator would not change that fact — the missing ingredient in "a human
plays one level" and "named... by someone who did not build it" is the human,
not the hardware. The audit's one-line grouping of this task with
`09-notification`/`10-click-haptics` under "needs a physical phone" is
therefore imprecise for this task specifically (those two genuinely need
hardware an emulator lacks — no Taptic Engine, no real notification tray
guarantees; this one does not), but the practical conclusion the audit
reached — leave it, an agent cannot close it — is correct for a different
and stronger reason than the one it gave.

### Verdict

`verify:` stays **pending**. Not `passed` — no human played it or named
anything. Not `failed` — nothing tried has produced a problem; both items
were exercised mechanically without defect. `status:` is left at `done`
unchanged: this run adds a second, independent confirmation (after the
`60-shell-build/08` verifier's kind of check, applied here) that the artefact
named in OUTCOME — "something a person can play through on a phone" —
concretely exists and functions, end to end, including both conventions
argued over in DECISIONS.md. What remains is exactly what the first VERIFY.md
section already said it was: a person, with a phone, and someone who did not
build this to look at ten tiles.

## How to reproduce (this update)

From a clean checkout, with `emulator-5554` running and
`com.DefaultCompany.game` already installed (see `90-android/09-notifications`
for the build/install path):

```sh
ADB=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb

# clean state
$ADB shell pm clear com.DefaultCompany.game
$ADB shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity

# play level 1 by hand (tile grid is 6 columns; real-pixel centres used here
# for a 1080x2340 screen)
$ADB shell input tap 123 345      # take a tile
$ADB shell cat /sdcard/Android/data/com.DefaultCompany.game/files/board.save
# repeat taps across the grid; watch `taken`/`triples` grow, then a
# "Shelf jammed" or win card appears — screenshot with:
$ADB exec-out screencap -p > outcome.png

# skip forward by level index using the desktop-testing hotkey — works on the
# emulator's virtual keyboard
$ADB shell input keyevent 42      # KEYCODE_N -> StartLevel(_levelIndex + 1)
$ADB shell cat /sdcard/Android/data/com.DefaultCompany.game/files/board.save
# repeat until the second line reads "level N room_09 ..." (took 20 presses
# from level 1 in this run); screenshot each pile and look for a small
# rope-coil badge in a tile's lower-right corner

# cleanup
$ADB shell pm clear com.DefaultCompany.game
$ADB shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
```

## What was not checked (this update)

- No human played anything and no human named any tile — the two things the
  task actually asks for remain undone, by construction (independence rule).
- Real hardware: the emulator's screen size, pixel density, ambient lighting
  and touch-target feel may not match the eventual test phone; only the
  emulator's own 1080x2340 rendering was inspected.
- The tap-ignoring half of D15 (a locked tile does not register a take) was
  read from source, not exercised on device — see the D15 paragraph above for
  why the specific pile was no longer reachable when I went to test it.
- Colour-vision deficiency and screen glare, the same gaps the first VERIFY.md
  section already flagged for the (now-obsolete) hue-based fallback, were not
  re-examined; the fallback path (`HueFor`, used only when a sprite file is
  missing) was not exercised at all in this run because every kind
  encountered had art.
- Rooms 10 through 12 were passed through via the `N` hotkey but their piles
  were not inspected tile-by-tile for further locked-item instances beyond
  the one confirmed in room 9; one confirmed sighting was judged sufficient
  for D15.
- `build/playtest/index.html` was again not opened.
