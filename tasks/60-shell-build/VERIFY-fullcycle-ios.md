# VERIFY — full cycle on iOS, end to end

Date: 2026-08-29. Device: iPhone 16e simulator, iOS 26.3 (`4F02086F-122A-4BE2-9848-C41A53A2466D`),
panel 390×844 pt, screenshots 1170×2532 px, scale 3.
Build: Unity `6000.3.22f1` → `BuildScript.BuildIOSSimulatorProject` (exit 0), then
`xcodebuild ... -sdk iphonesimulator -arch arm64` → `** BUILD SUCCEEDED **`,
`IPHONEOS_DEPLOYMENT_TARGET = 17.0`. Installed from
`game/build/ios-sim/CatShelter/DerivedData/Build/Products/Release-iphonesimulator/Sootpaw.app`
after `simctl uninstall`, so step 1 started with an empty container.

Screenshots: `shots-fullcycle-ios/`. Every claim below comes from a command that was run;
console lines are quoted verbatim from `xcrun simctl launch --console-pty booted com.sootpaw.game`.

> **Шаг 1 устарел — примечание от 2026-09-02.** Ниже написано, что первый
> запуск ведёт прямо на карту дома, «без вступления, котёнка и знакомства».
> С тех пор экран съёмки стал воротами первого запуска: пустой контейнер
> теперь даёт «Show us your cat», и на карту игра попадает только после
> снимка, отказа от него или уже сохранённого кота. Проверено на Android —
> `VERIFY-fullcycle-android.md`, шаг 1. Остальные шаги этого документа
> перепроверить было нечем: устройства и симулятора iOS в сессии 02.09 не
> было, поэтому их следует читать как описание той сборки, а не сегодняшней.

## The steps

| # | Step | Result | Shot |
|---|---|---|---|
| 1 | First launch, no save | Straight to the house map. No intro, no kitten, no naming. `[GameBoot] branch=map` | `01` |
| 2 | House map | `[HouseMap] built 12 rooms, cursor=1/0, done=[], open=1` — room 1 lit, 2–12 dim | `02` |
| 3 | Choosing a room | `[HouseMap] tap room 1 via up/plaque` → `[Board] room 01, pile 0: 0 of 4 corners clean` | `03` |
| 4 | Playing, a match | `took 1 … took 8, shelf=1, triples=1` — shelf filled to 3, triple cleared | `04a–04c` |
| 5 | Finishing a room | `[Board] win: level 1, lastPileOfRoom=True` → "The room is clean" with before/after and **Next** | `05c` |
| 6 | Progress survives restart | Resumed. See below | `06`, `06b` |
| 7 | Kitten changes with progress | `state=1` / `state=2` / `state=3` at 1 / 4 / 8 rooms done | `07a`, `07c`, `07d` |
| 8 | Rewards | Bowl present at 4 rooms; bowl **and** blanket at 8 | `07c`, `07d` |
| 9 | Capture screen | Three buttons, all reachable | `09a–09e` |
| 10 | Choosing a photograph | Picker opens, selection **fails** — see D1 | `10a`, `10b` |
| 11 | Meet-your-cat | Name field and "That's her". Name persists, screen does not advance — see D2 | `11a–11e` |
| 12 | End of the game | `[Board] house complete` → "Every room is clean" | `12a–12c` |
| 13 | Sharing | System sheet on both cards, with captions | `13a`, `13b` |
| 14 | Review heart | **Not drawn**, and correctly so | `14` |

### Step 6 in detail — it does resume

Killed the app mid-level (4 tiles taken, one crate on the shelf) and relaunched.
No `[DebugGameView] starting fresh` anywhere in the console. Re-entering room 1 gave back
the identical board — 32 tiles, crate still in shelf slot 2. Reading `board.save`:

```
catshelter-save-v1
level 1 room_01 0
shelf _ prop_crate _ _ _ _ _ _ _ cap9
triples 1
taken 1 6 5 8
```

Two notes. The app relaunches to the **house map**, not into the board the player left;
the board state is restored on re-entry, not on launch. And the game's own `board.save`
writes **no `cursor` and no `roomsdone` line** — the map derives both from the `room_NN`
token, exactly as `tools/save-forge/README.md` warns. Proof: after room 1 the save read
`level 2 room_02 0` with no `roomsdone`, and the map still reported
`done=[1], open=2, cursor=2/0`.

## Defects

**D1 — a chosen photograph never becomes a cat, and the message sends the player in a circle.**
Shot `10b`. Selecting a real JPEG from the library returns
"Something went wrong on our side. Try that one again?". The console names the cause:

```
[CatPicker] read 27271 bytes from the picker
[CaptureScreen] vision failed: vision failed: Could not create inference context
```

The picker did its job — 27 271 bytes arrived. Vision's inference context cannot be created
on the simulator, so the root cause is very likely environmental and **this needs a real
device to rule on**. The defect that is *not* environmental is the recovery: "Try that one
again?" invites a retry that will fail identically every time on this device, and nothing
steers the player to the third button that works. `CatVision.swift:76` is where the message
originates.

**D2 — "That's her" saves the name and goes nowhere.**
Shots `11b`–`11e`. The button fires (`[CaptureScreen] cat named: Ьгклф`) and `cat.save` is
written, but the screen does not change. A pixel diff of the screen before and after the tap
returns `None` — not one pixel altered. Tapping again appends a second `cat named:` line to
`capture-state.txt`. Neither `capture.txt` nor `meet.txt` is deleted on confirm, so the boot
branch stays pinned and there is no way forward. Reproduced on both flag files, so it is not
specific to one. Confined to the debug flag paths today, since neither screen is on a player's
route yet.

**D3 — finishing the game erases the game.**
Shots `15a`, `15b`. After `[Board] house complete`, `board.save` is **deleted** from Documents.
Relaunching gives `[HouseMap] built 12 rooms, cursor=1/0, done=[], open=1` — twelve ticks gone,
eleven rooms re-locked. `cat.save` survives with her name and traits, but her state is derived
from rooms done, so the cat card reopens at `[Board] cat card opened, state=1`: the huddled
dusty kitten, no bowl, no blanket. Completing the house is indistinguishable from never having
played it, and both rewards are lost.

**D4 — the last corner of the last room is never cleaned.**
Shots `12c`, `12d`. After the final tile (`took 57 … available=0`, `house complete`), the
bottom-right quadrant of room 12 still shows the dirty art behind the card. Measured on
`12d`: a hard colour step at exactly x=585 px (half the panel width) —
`(142,127,108)` left of it, `(126,114,96)` right. The card overhead says "Every room is clean".
The quadrant reveal itself is correct behaviour (`3 of 4 corners clean` before the last tile);
what is missing is the fourth reveal at the moment of winning.

**D5 — the ending card is a dead end.**
Shot `12d`. The back arrow is drawn at top-left but inert: tapping it (26, 73 pt) produced no
`[HouseMap]` line and no pixel change. The card has no close button. The only exits are the
share sheet or killing the app.

**D6 — `errors.txt` never records the failure the player was shown.**
`Documents/errors.txt` does not exist after D1 occurred. `game/Assets/Shell/DeviceLog.cs:96`
records only `LogType.Error`, `Exception` and `Assert`; the vision failure is a plain
`Debug.Log`. The file documented as holding "every error and exception" therefore stays empty
through the single biggest failure on that screen — a tester without a console attached would
have no record of it.

**D7 — coat rendering.** Two separate faults, both on the cat card.
State 3 (`07d`) has a razor-straight vertical seam through the cat's back at x≈623 px, dark
brown on one side and pale orange on the other, cutting the tabby stripes off mid-stroke.
State 1 (`07a`, `15b`) has an aliased outline with stray hairs escaping the mask along the
lower-left edge, and the legs dissolve into a flat blob.

**D8 — the game exposes nothing to accessibility.**
`idb ui describe-all` returns exactly one element, the application frame, on every screen
tested. No button, label or tile is visible to VoiceOver.

## Smaller observations, not defects

- The house-map hint wraps mid-phrase: "… dim rooms / are still locked" (`01`, `12a`).
- Room 12's plaque overlaps the roof once it becomes the active room (`12a`); at rest it sits
  inside the gable (`01`).
- The shelf does not compact — a cleared triple leaves a hole and later tiles keep their slot
  (`04c`, and `shelf _ prop_crate _ …` in the save).
- "Next" on the room-clean card goes straight into the next room's board, not back to the map.
- The cat card title is "Sootpaw", not the cat's name. **Checked, and intended** —
  `CatCardScreen.cs:263` reads the copy key `card.game_name`. Her name did not appear on any
  screen reached in this run.
- The share sheet's preview thumbnail is a text glyph, though `CatShare.swift:93` puts the
  `UIImage` first in `activityItems` and the guard above it returns early if the image fails
  to load. "Assign to Contact" and "Print" being offered says the image is in the payload.
  Cosmetic at most; not pursued further.
- The name field on the meet screen is an unstyled `TextField` — square corners, hard grey
  border, out of keeping with every other control (`11a`).

## Step 14 — the answer

**The heart is not there, and that is the correct behaviour.** Runtime:
`[Board] ending card: no store page yet, heart hidden`. Source agrees —
`game/Assets/Shell/Review.cs`: `AppStoreId = ""`, and on iOS
`Available => !string.IsNullOrEmpty(AppStoreId)`, so `false`. The file's own comment records
why this matters: on 2026-08-29 the Android side had the heart silently switched **on** by the
`com.sootpaw.game` rename, because that guard tested a correlate rather than the thing itself.

## What I could not reach, and why

- **The camera.** The simulator answers "Take a photo" with iOS's own picker showing a flat
  grey viewfinder (`09b`); the shutter is inert — pressing it changed nothing (`09c` is
  pixel-wise the same screen). No camera permission dialog appeared. A real device is needed.
- **The photo→cat pipeline (D1).** Blocked by Vision on the simulator. Whether the pipeline
  works at all is unanswered by this run; only a device settles it.
- **Typed text.** `idb ui text "Murka"` arrived as "Ьгклф" — the simulator's hardware keyboard
  is on a Russian layout and idb sends key codes, so the physical keys transliterated. An
  input artefact, not a game fault. It does incidentally show Cyrillic renders with no tofu.
- **Playing all 37 levels by hand.** Steps 5, 7, 8 and 12 used forged saves from
  `tools/save-forge/`. `almost.save` reached the room-clean card; two saves I wrote
  (`level 10 room_05 0`, `level 22 room_09 0`) set the cat states; `house.save` reached the
  ending. The saves put the game in a plausible position, they do not prove a human can
  play there.
- **Android.** Untouched, by instruction.

Nothing under `game/Assets/` was edited and nothing was committed.
