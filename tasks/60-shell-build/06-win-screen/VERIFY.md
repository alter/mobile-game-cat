# VERIFY — 60-shell-build/06-win-screen

Verifier: an independent agent context, 2026-08-28. It wrote none of
`View/DebugGameView.cs`, none of `View/DebugGame.uss`, none of `Core/RoomPlan.cs`,
none of the room art or its `.meta` files, and none of `NOTES.md`. It did not run
Unity, did not open a simulator or an emulator, did not take the two screenshots
in this directory, and did not touch any file outside this `VERIFY.md`. It read
files, ran the two test suites and the purity check, and measured the shipped
screenshots with Pillow.

## Verdict

`verify: pending` — not `passed`, and not because a check failed.

This task's own acceptance is `VERIFY (HUMAN)`: "shown the win screen for half a
second, a person who did not build it names which side is 'before' and which is
'after' correctly and immediately." Per `tasks/README.md` ("The independence
rule"), a HUMAN task is not performed and not simulated by an agent. That check
is still owed by a person and nothing below substitutes for it.

Everything an agent *can* settle about the mechanism is settled, and one real
defect was found that the code comment explicitly denies. See "Defect found".

## What passed

**1. The art path exists and the fallback is conditional on both files.**
`game/Assets/View/DebugGameView.cs:654-683`:

```csharp
var roomNo = RoomPlan.RoomNumber(closedLevel.RoomId)
                     .ToString("00", CultureInfo.InvariantCulture);
var dirty = SpriteNamed($"Art/room_{roomNo}_dirty");
var clean = SpriteNamed($"Art/room_{roomNo}_clean");
if (dirty != null && clean != null)
```

The prop collage at `:685-713` is reached only when either texture is null, and
each branch logs which one ran (`:660`, `:688`). `SpriteNamed` (`:721-722`) is a
plain `Resources.Load<Texture2D>` returning null without a warning — correct for
a state that is ordinary rather than a defect.

**2. The room number is derived correctly for every room 1..12.**
`game/Assets/Core/RoomPlan.cs:85-90` keeps the digits of the id and parses them:

```csharp
var digits = new string(roomId.Where(char.IsDigit).ToArray());
return digits.Length > 0 && int.TryParse(digits, out var number) ? number : 0;
```

The ids actually in the level files are `room_01` … `room_12` and nothing else —
`grep -ho '"room_id"[^,]*' game/Assets/Resources/Levels/*.json | sort -u` returns
exactly twelve lines, `"room_id": "room_01"` through `"room_id": "room_12"`. Each
yields digits `01`…`12` → 1…12, and `.ToString("00")` renders `01`…`12`. No id
carries a second number that would corrupt the parse. (The *file* names do —
`l01_room01_pile0.json` — but `RoomId` comes from the JSON field, not the
filename, so that trap is not sprung.)

**3. All 24 room files are present under the names the code builds.**
`ls game/Assets/Resources/Art/room_*.png | wc -l` → `24`, running
`room_01_clean.png`, `room_01_dirty.png` … `room_12_clean.png`,
`room_12_dirty.png`. Each has a `.meta`. So on shipped data the collage fallback
is unreachable, and the `[Board] before/after: room NN has no art, using props`
line cannot fire for any of the twelve rooms.

**4. The gating is what SCOPE asks for.** `DebugGameView.cs:485-486` calls
`ShowRoomTransformation` only under `if (lastPileOfRoom)`, and `ShowCard`
(`:521`) calls `HideRoomTransformation()` unconditionally first, so the lose card
and the corner-clear card cannot inherit a stale block.
`RoomPlan.IsLastPileOfRoom` (`Core/RoomPlan.cs:54-55`) is
`level.PileIndex == PilesIn(level.RoomId) - 1`, and
`Tests/Core/RoomPlanTests.cs:35-36` asserts exactly one last pile per room across
the shipped files.

**5. The screenshots show what `NOTES.md` claims.** Both
`ios-win-room-before-after.png` (1170×2532) and
`android-win-room-before-after.png` (1080×2340) show the "Room clean!" card with
two portrait photographs of the same hallway — left grey, cobwebbed, cluttered,
captioned "Before"; right warm, lit, tidy, captioned "After" — over the body text
"The kitten likes it better already." and a "Next" button. This is the drawn
room pair, not a scatter of prop icons. The two platforms are visually identical.
Header reads "Room 1 of 12 · pile 1 of 1", consistent with the room-01 art the
`[Board] before/after: room 01 art` trace in `NOTES.md` claims.

**6. Test suites, run from this checkout on 2026-08-28.**

- `dotnet test build/core-tests/core-tests.csproj -v q --nologo` →
  `Пройден!   : не пройдено     0, пройдено   195, пропущено     0, всего   195,
  длительность 307 ms.` — **195 passed, 0 failed**.
- `.venv/bin/python -m pytest tools/tests -q` → `170 passed in 13.43s`.
  `.venv/bin/python -m pytest tools/ -q` → `170 passed in 14.18s` (same set).
- `build/check-core-purity.sh` → `Core is engine-free: OK`.

`NOTES.md` in this directory quotes 189 and 160 for these two suites. Those
numbers were true when written and are now stale; both suites have grown, and
nothing fails. The discrepancy is staleness, not a regression.

## Defect found — the room art is squashed at import, by about 10%

The code comment at `DebugGameView.cs:662-667` reasons from the source size:

```
// The frames are square (116×116) because they were built to
// hold a scatter of props. A room is 1856×3328 — portrait, and
// scale-to-fit would shrink it into a letterboxed sliver.
```

`sips` confirms the *source* is 1856×3328 (aspect 0.5577) for
`room_01_dirty.png`, `room_01_clean.png` and `room_12_clean.png`. But that is not
what reaches the screen. `game/Assets/Resources/Art/room_01_dirty.png.meta` has:

```
  maxTextureSize: 2048
  nPOTScale: 1
```

`nPOTScale: 1` is *ToNearest*: 1856 → 2048, 3328 → 4096; `maxTextureSize: 2048`
then halves that to **1024×2048**, aspect exactly **0.5000**. The room is
therefore squashed horizontally by 1024/1142 ≈ 0.90 relative to a plain
max-size downscale, an ~10% narrowing of everything in the picture.

This is measured, not inferred. Pillow bounding boxes of the two panes in the
shipped screenshots:

- iOS: before `x[266..544] y[875..1432]` = 279×558, aspect **0.5000**;
  after `x[626..904] y[875..1432]` = 279×558, aspect **0.5000**.
- Android: 258×516 for both panes, aspect **0.5000**.

Neither 0.5577 (source) nor 0.5591 (the 104×186 frame). The arithmetic closes
exactly: `Paint` sets `ScaleMode.ScaleToFit` (`DebugGameView.cs:371`), so a
1024×2048 texture in a 104×186 frame draws at
`min(104/1024, 186/2048) = 0.09082` → **93×186 logical px**, letterboxed ~5.5 px
on each side. At the iOS screenshot's 3.0 device scale that is 279×558 — the
measured number.

**Consequences.** Nothing is cropped: `ScaleToFit` never crops, and the whole
room is visible in both panes. The distortion is uniform and identical in the
"before" and the "after" pane, so the side-by-side comparison the task exists for
is not damaged, which is why it survived a look at the screenshot. But the room
is not drawn at its true proportions, and the comment in the code asserts a
runtime size that is wrong. If the intent is undistorted room art, the fix is
`nPOTScale: 0` (None) on the 24 room `.meta` files. Not applied here — this
context verifies and does not fix.

## Second defect — inline frame styles leak into the fallback

`ShowRoomTransformation` mutates the two frames in the art branch
(`DebugGameView.cs:668-676`): width 104, height 186, `backgroundColor` cleared,
all four border widths 0, `paddingTop`/`paddingLeft` 0. These are **inline**
styles and are never restored. The fallback branch (`:685-713`) assumes the USS
defaults — `.game__ba-collage` 116×116 with `#B0A79B` / `#F4EAD8` backgrounds, a
2px `#C9A97C` border and 4px padding on the "after" pane
(`View/DebugGame.uss:238-270`) — and inline styles beat USS. So a room with art
closing before a room without art would leave the collage drawn borderless and
transparent at 104×186, with the `messy` scatter's hard-coded `left`/`top` ranges
of 0..82 (`:700-701`) now sized for a frame that is no longer 116 wide.

Unreachable on shipped data, because all 24 files exist (point 3). Recorded
because it becomes reachable the moment a thirteenth room is added without art,
and because the fallback is the branch nobody will look at again.

## Observation — room 12 never shows its pair

`Finish()` returns early on the house-complete branch (`DebugGameView.cs:458-466`)
before reaching the `if (lastPileOfRoom)` call at `:485`. The last pile of room 12
is also the end of the house, so it gets the house-complete card and no
before/after. Deliberate per the comment there ("it gets the ending screen rather
than the ordinary win card followed by one"), and not contradicted by SCOPE, but
it means the twelfth room's transformation is the one the player never sees.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand:

```sh
git clone git@github.com:alter/mobile-game-cat.git
cd mobile-game-cat
git checkout dev

# 1. All 24 room files present, named as ShowRoomTransformation builds the name
ls game/Assets/Resources/Art/room_*.png | wc -l          # -> 24
ls game/Assets/Resources/Art/room_{01,12}_{dirty,clean}.png

# 2. Every room_id in the shipped levels, to check RoomNumber's parse
grep -ho '"room_id"[^,]*' game/Assets/Resources/Levels/*.json | sort -u
# -> exactly "room_01" .. "room_12", no id carrying a second number

# 3. The import settings that cause the squash
grep -n 'maxTextureSize:\|nPOTScale' game/Assets/Resources/Art/room_01_dirty.png.meta
sips -g pixelWidth -g pixelHeight game/Assets/Resources/Art/room_01_dirty.png
# -> source 1856x3328; nPOTScale 1 + maxTextureSize 2048 -> runtime 1024x2048

# 4. The measured aspect of what was actually drawn, from the shipped screenshots
python3 - <<'PY'
from PIL import Image
import numpy as np
for path, bands in [
    ("tasks/60-shell-build/06-win-screen/ios-win-room-before-after.png",
     [(266,544),(626,904)]),
    ("tasks/60-shell-build/06-win-screen/android-win-room-before-after.png",
     [(245,502),(577,834)])]:
    a = np.asarray(Image.open(path).convert("RGB")).astype(int)
    mask = np.abs(a - np.array([245,237,222])).sum(axis=2) > 40
    H = a.shape[0]
    for c0, c1 in bands:
        rows = mask[:, c0:c1+1].sum(axis=1) > (c1-c0+1)*0.6
        s = int(0.40*H); r0 = r1 = s
        while r0 > 0 and rows[r0-1]: r0 -= 1
        while r1 < H-1 and rows[r1+1]: r1 += 1
        w, h = c1-c0+1, r1-r0+1
        print(path.split('/')[-1], f"{w}x{h}", round(w/h, 4))
PY
# -> 0.5 on every pane; source aspect is 0.5577, frame aspect 0.5591

# 5. Suites
dotnet test build/core-tests/core-tests.csproj -v q --nologo   # 195 passed, 0 failed
python3 -m venv .venv && .venv/bin/pip install -r tools/requirements.txt
.venv/bin/python -m pytest tools/tests -q                       # 170 passed
./build/check-core-purity.sh                                    # Core is engine-free: OK
```

Step 4 needs Pillow and NumPy; they are in `.venv` after the step-5 install.

The remaining check is the task's own, and it needs a person: show the win card
for half a second to someone who did not build it and ask which side is "before".

## What was not checked

- **The HUMAN acceptance criterion.** Not performed and not simulated. This is
  the whole of `VERIFY` in `task.txt` and it is still open.
- **Unity was not run.** No compile, no PlayMode, no `-runTests` (forbidden —
  `AGENT-BRIEF.md:196`). That `DebugGameView.cs` compiles is assumed from the
  screenshots existing, not from a build performed here. The View layer is not
  covered by `dotnet test` at all: the 195 Core tests exercise
  `RoomPlan.RoomNumber` and `IsLastPileOfRoom`, and nothing exercises
  `ShowRoomTransformation`.
- **The runtime texture size was derived, not observed.** 1024×2048 is what
  `nPOTScale: 1` plus `maxTextureSize: 2048` predicts and what the screenshot
  measurement matches to the pixel; it was not read out of a Unity memory profile
  or an importer log. An alternative cause producing exactly 0.5000 on both
  platforms was not found, but was not excluded either.
- **Only rooms 01 and 12 had their pixel dimensions and import settings read**
  (`room_01_dirty`, `room_01_clean`, `room_12_clean` for size; `room_01_dirty`
  for `.meta`). The other 21 files were checked for presence only, not for
  matching size or import settings.
- **Only room 01 was ever seen on screen.** Both screenshots are room 1 of 12.
  Rooms 02–12 are verified as files on disk and as a name the code would build,
  never as a picture that appeared.
- **The collage fallback was not exercised**, on either platform. It is
  unreachable on shipped data, so the style leak described above is reasoned from
  the source, not observed.
- **Memory.** Twelve rooms × two frames of 1024×2048 is 8 MiB per texture if all
  were resident and uncompressed; the room `.meta` has `textureCompression: 1`
  (compressed) and `isReadable: 0`, so the real figure is much lower, but no
  measurement of win-screen memory or load time was taken.
- **No test was added or run for the "shown once per room close" rule** beyond
  reading the `if (lastPileOfRoom)` guard. A multi-pile room was not played to
  confirm mid-room piles show nothing.
