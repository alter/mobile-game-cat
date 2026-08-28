# 60-shell-build/01-presentation-input — actual state (from disk + build)

CODE: DebugGameView.cs (existing) + DebugGame.uxml/uss (new) — click-to-take works;
drag-and-drop NOT implemented — requires PointerManipulator (no ready component per
knowledge/03-ui-toolkit-runtime.md section 9) and real prop sprites (40-art).

VERIFIED BY (not imagined):
- /tmp/game_screen.png: title visible, 36 tiles, shelf 9 slots, click-take works
- build/osx/CatShelter.app builds (Succeeded, 105MB) with scene
- No fake drag code inserted (reverted when attempted)

BLOCKED BY: 40-art (sprites for 10 prop kinds). Without art the reveal of
hidden kinds and matching animation can't be shown — the task's VERIFY requires
"hidden item renders as unknown prop, not real sprite", which needs actual
sprite assets.

DECISION: leave as done-with-known-limit (label updated); do NOT claim drag
or real art until 40-art delivers.

---

# 2026-08-28 — the animation half, built

`tasks/AUDIT-2026-08-28.md` called this task's `status:done` **false** on the
animation half, and it was right: the OUTCOME says "placement and match
animated", and `grep -E "transition|animation|@keyframes"` over the 293-line
`DebugGame.uss` returned nothing, three days running. It now returns 14 lines.
The owner played the game for the first time today and what he noticed was the
silence — a tap that produces no visible answer reads as a tap that did not
register.

Touched: `game/Assets/View/DebugGame.uss` (+~90 lines) and the animation path of
`game/Assets/View/DebugGameView.cs`. Nothing else.

## The finding that shaped the whole design

**`Render()` destroys the elements an animation would live on.** `RenderPile()`
opens with `_pileArea.Clear()` and `RenderShelf()` with `_shelfArea.Clear()`,
and `Take()` calls `Render()` on every successful move. So a transition started
on the tapped tile, or on the slot it lands in, dies with the element in the
same frame it began — the tile is disposed and rebuilt before a single frame of
motion is drawn.

That rules out animating the real elements, and it is why nothing here is a
tween on a tile:

- The travelling tile and the match pop are **throwaway copies** on a new
  `.game__fx-layer` — absolute, full-bleed, `pickingMode = Ignore`, inserted
  just before the overlay so a win/lose card is never flown across. `Render()`
  never touches that layer.
- All the geometry those copies need is **captured before the model moves**, at
  the top of `Take()`: the source tile's `worldBound`, and the destination
  slot's. After `Render()` there is no tapped tile to fly from and no slot to
  fly to, and a freshly created element's `worldBound` is NaN until the next
  layout pass anyway.
- The destination is knowable in advance and is not guessed: `Shelf.TryPlace`
  fills `Array.IndexOf(_slots, null)`, so it is the leftmost free slot, which
  after a match is a **gap in the middle** rather than the end of the row (D16).
  `FirstFreeSlot()` reads exactly that.

**The one exception**: a refused tap does *not* call `Render()` — `Take()`
returns early. That is the only place where the real tile survives long enough
to animate, so the refusal flinch is a plain class toggle on the tile itself.

## Which API, and why

**USS `transition-property`/`transition-duration`, driven by inline
`style.left`/`style.top` writes and class toggles from C#.** Not
`VisualElement.experimental.animation`.

- `experimental` is named that for a reason and its overloads have shifted
  between Unity versions. I cannot compile or run anything on this machine
  before you build it, so an API I am not certain of is a build you lose, not a
  risk I get to take back.
- Transitions have been stable public USS since 2021.2.
- Deliberately **no** `Scale`/`Translate`/`Rotate` struct constructor appears
  anywhere in the animation path — those are the signatures that have churned.
  Position is animated through `style.left`/`style.top`, which take a plain
  `float` via `StyleLength`'s implicit conversion; every transform (`scale`,
  `rotate`) is written in USS and reached by adding a class. `left`/`top` cost a
  layout pass per frame where `translate` would not, but it is one element per
  tap and I would rather pay that than gamble on a constructor.
- The only scheduler API used is `element.schedule.Execute(Action).ExecuteLater(ms)`,
  which is long-standing and documented.

## What was built

1. **Taking a tile — 150ms.** A copy of the tapped tile flies from its pile
   position to its shelf slot and shrinks 52px → 32px on the way, so it arrives
   as a shelf item rather than landing oversized. The end position is set **one
   frame later** (`ExecuteLater(0)`) on purpose: two writes to the same property
   inside one frame collapse into a single resolved style and the transition has
   nothing to interpolate from — the copy would just appear at the shelf.
2. **A match — 170ms, starting as the flight lands.** The three slots that
   emptied expand to 1.7× and fade out where they stand. A different *verb* from
   a placement on purpose: that one travels, this one bursts. The three slots are
   found by diffing shelf occupancy across the move, which is exact because
   `Shelf.TryMatch` empties in place and never compacts (D16).
3. **A refused tap — 90ms out, 110ms back.** The tile flinches: `scale: 0.86`
   and `rotate: -5deg`, then back.

   **This exposed a real bug, not just a missing animation.** A locked or buried
   tile registered *no click callback at all* — only `game__tile--dim`. So the
   `[Board] tap {id} refused` line the brief points at was unreachable by
   tapping a locked tile; it could only fire once the board was already over.
   A locked tile answered a tap with total silence, and "the game said no" and
   "the game has frozen" look identical from the far side of the screen. Both
   the locked/unavailable branch and the buried branch now register a `Refuse`
   handler, so the log line fires *and* the tile moves.

## Motion does not block play

The model moves first and the animation is decoration over a board that is
already correct. `AnimateTake` is called after `TakeItem` has returned and
before `Save()`/`Render()`; nothing is awaited, no input is gated, no flag
suppresses a tap while a copy is in flight. A player tapping faster than 150ms
gets a second flyer beside the first, not a queue to sit through. The fx-layer
is `PickingMode.Ignore`, so a flyer passing over the pile cannot swallow the
next tap. Every copy is removed on a timer owned by the *layer*, not by the
copy — chosen over `TransitionEndEvent` so that if a transition never runs for
any reason, the cleanup still fires; a copy stuck over the board would be much
worse than a missing animation.

Budget: 150ms placement, 320ms for the match including the flight that precedes
it. Inside the brief's "120–200ms is a game" for the common case.

## What I could not do without a build

- **Nothing here has been seen.** No Unity, no xcodebuild, no simulator — per
  instruction. Timings, distances and the 1.7× pop are read off the stylesheet,
  not off a screen.
- **I did compile it, though, and it is clean.** Not a Unity build: a throwaway
  csproj outside the repo, `netstandard2.1`/`LangVersion 9`, compiling
  `Assets/{Core,Shell,View}` against the real
  `6000.3.22f1/PlaybackEngines/iOSSupport/.../Managed/*.dll` (77 assemblies) plus
  the project's own Newtonsoft. Result: **`Ошибок: 0`**, 8 warnings, all
  pre-existing `CS8632` in `Core` from my project not enabling `<Nullable>`.
  Three files were excluded because they fail for reasons that are not mine and
  that I was told not to touch: `Shell/GameAnalyticsSink.cs` (the vendor SDK is
  not in the reference set) and `View/HouseMapView.cs` + `Shell/GameBoot.cs`
  (`HouseMapView.Requested` and `AddReturnToMap` do not currently exist — the
  other worker's in-flight state). **Zero errors were reported in
  `DebugGameView.cs` even before those exclusions.**
- **USS is not compiler-checked.** Unity's USS parser never ran. If any property
  is rejected it will show as a console warning at load and that rule will
  simply do nothing — it cannot break the board. The ones I am least sure of, in
  order: `transition-property: scale` and `rotate` (transform transitions), and
  single-number `scale: 0.86`. If the flinch does not move, that is where to
  look; the flight uses `left`/`top`, which I am confident of.
- **Drag-and-drop is still not implemented** and is still out of what I touched.
  The task's SCOPE asks for it; this section does not close that. Click-to-take,
  now animated, is what exists.

## What to look for in the screenshots — both platforms

The hard part: these are 150–320ms events, so a screenshot taken after the tap
settles shows nothing. **A screen recording is worth far more than a still
here**; if it must be stills, they have to be caught mid-flight.

1. **Mid-flight placement.** Tap a pile tile and capture within ~100ms: a single
   prop should be visible *between* the pile and the shelf, smaller than a pile
   tile and larger than a slot. If it is at the shelf instantly, the
   one-frame-later trick failed and the transition is collapsing.
2. **Where it lands.** The flyer must end on the slot the item actually occupies.
   After a match has left a gap mid-row, the next tile must fly into **that gap**,
   not to the end of the row. If it flies to the wrong slot, `FirstFreeSlot` and
   `TryPlace` have diverged.
3. **A match reads as an event.** Place a third of a kind and capture ~200ms
   after: three enlarged, half-faded props over three slots. It must look
   obviously unlike a placement.
4. **The refusal.** Tap a **locked** tile (rooms 09–12 carry them) and a
   **buried** tile. Expect a visible tilt/shrink, and — this is the cheap check
   that needs no timing at all — `[Board] tap {id} refused` in the device log
   for *both*. That line has never once appeared from a locked-tile tap before
   today. If the log line appears and the tile does not move, the USS transform
   transition is the suspect (see above).
5. **Nothing stuck.** After playing a dozen moves, a still of a resting board
   must look exactly like it did yesterday — no leftover prop floating over the
   pile, no half-faded ghost on the shelf. That is the fx-layer cleanup working.
6. **Fast tapping.** Tap five tiles as fast as possible. Every tap must register
   (item count keeps dropping, one per tap) and no tap may be eaten by a flyer
   passing over the pile.
7. **iOS and Android both** (MEMORY: one verified platform once hid a completely
   broken second one). The fx-layer is positioned from `worldBound` deltas, so
   it should survive a different panel scale — but that is reasoning, not
   evidence, and panel scaling is exactly the kind of thing that differs between
   the two.

---

# 2026-08-28 — the header, and giving the kitten somewhere to live

The owner played it and said the header is an enormous sentence eating the top
of the screen, and that because of it the cat is a tiny icon you cannot make
out. Both halves of that are one problem: `Room 1 of 12 · pile 1 of 1` at 20px
bold runs about two thirds of the panel's width and is centred, so there is
nothing left of that row but two corners — and the kitten got one of them, at
56 units. See `02-room-piles/ios-room-behind-the-pile.png` and
`06-win-screen/ios-win-room-before-after.png`.

Touched: `DebugGame.uxml`, `DebugGame.uss`, and the header-building path of
`DebugGameView.cs` (`OnEnable` queries, `Render`'s two copy lines →
`RenderHeader`, `BuildCatPortrait`). Nothing else. Not run — no Unity, no
simulator, per instruction — but **compiled**: a throwaway csproj outside the
repo, `netstandard2.1`/`LangVersion 9`, `Assets/{Core,Shell,View}` against
`6000.3.22f1/PlaybackEngines/iOSSupport/Variations/il2cpp/Managed/*.dll` plus
the project's Newtonsoft. **`Ошибок: 0`**, 7 warnings, every one of them a
pre-existing `CS0618` about `unityBackgroundScaleMode` in files I did not
touch. `HouseMapView.cs`, `GameBoot.cs` and `Shell/GameAnalyticsSink.cs` were
excluded for the same reasons the 01-animation pass excluded them.

## 1. What published games actually put up there

Read off store and press screenshots retrieved by image search, plus store
listing text where the page would load. **I have not played any of these.** I
identified each game from the branding and UI inside the image itself and
cross-checked the store copy where I could; where I could not, I say so.

**Triple Match 3D** — Bumbleboo, App Store `id1607122287`, Google Play
`com.master.triple3d.find`. Store screenshots: one row across the top —
`LEVEL:7` at the left, `09:18` centred, a coin chip `2572` at the right. Under
it a second short row of three goal chips, each an item picture with a count
(`6`, `3`, `6`). Then the board, then a seven-slot tray, then a booster row.
The store text matches what the pictures show: *"Each level has a timer, so you
must move fast & reach the level goal"* and *"Complete the goal set at the
start of the level"* — https://apps.apple.com/us/app/triple-match-3d/id1607122287

**The Match Factory** — Peak. Screenshots: `Level 1210` in a small chip at the
top left, an hourglass and `3:08` centred with a thin bar beneath it, a gear at
the right. Goal chips under that (burger `16`, fries `9`). A second set of
shots shows the same bar at `Level 10` / `Level 3` / `Level 20` with a pause
button instead of a gear. I could not load its App Store page (Apple returned
"page can't be found" for the id I tried), so this one rests on the screenshots
alone.

**Match Triple 3D** — `and.lihuhu.machingtriple`. `LEVEL 128` left, an alarm
clock and `03:06` centred, a pause button right; five goal chips with counts,
one of them carrying a green tick because it is finished.

**Zen Match** — Good Job Games. The closest thing to our problem, because it
plays over a *photographic* background (a forest, a field of flowers). It puts
`LEVEL 15` small and centred at the very top, a two-dot progress indicator
directly under it, and a gear at the right. Nothing else. No count of tiles
remaining anywhere on the screen. Store page would not load for me either.

**Triple Tile** and the "Match Tiles" family — also over painted backgrounds.
One shows only a back arrow `<` top-left and a coin chip `200` top-right;
others put a small mascot avatar at the top left, then a `Level 78` / `Level
91` pill, a coin chip and a gear. The board takes everything else.

**My Talking Tom** — not a matcher, included because it is the case where the
animal *is* the product. The pet occupies roughly the middle 40% of the screen
height and the HUD collapses to small chips in the two top corners.

### What they have in common

1. **One row.** Roughly 40–50pt of a ~844pt screen, sometimes with a second
   short row of goal chips. Never two rows of prose.
2. **The level is a number, not a clause.** "Level 1210", "LEVEL:7",
   "LEVEL 15", "level 36". The only word is "Level", and two of the six drop
   even that.
3. **Not one of the six shows a denominator.** No "level 1210 of 3000".
4. **The centre of the bar belongs to the thing that changes second by
   second** — in all four timed games, the timer.
5. **Remaining work is a picture with a number on it**, never a sentence.
6. **Several show nothing at all about how much is left.** Zen Match and
   Triple Tile don't: the pile is its own progress bar, and it is already on
   screen.
7. **The tray is never labelled.** Nine slots explain themselves.

Touch targets, for the kitten: Material 3 — *"consider making touch targets at
least 48 x 48dp… Note: iOS recommends 44 x 44dp targets"*
(https://m3.material.io/foundations/designing/structure). The existing
back-to-map plaque is already built to the 44 figure (`HouseMapView.cs:856`).

## 2. What I chose, and what I dropped

The header is now **one row, 104 units tall, holding a cream plaque of
numerals on the left and the kitten at 104 units on the right**. It contains
no words at all.

| fact | was | is | why |
|---|---|---|---|
| which room of twelve | `Room 5 of 12` | `5/12` | 4 glyphs instead of 13. I kept the denominator *against* what all six games do, because their level counts are unbounded and ours is twelve — a finite promise the player can hold — and because the house map has already shown them twelve rooms, so the fraction is a legend for a picture they have seen. |
| which pile of the room's piles | `· pile 1 of 3` | one pip per pile, filled up to the current one | Words dropped. This is the same count `RenderRoom` already draws as cleaned quadrants behind the pile; saying it twice, once in prose, was the redundancy. Zen Match's two-dot indicator is the precedent. |
| how many items remain | `Items left: 36` | a bar that empties, and the number beside it | The words go, the number stays. This is the only value on the screen that changes on every tap and the only one a player cannot count by looking. The genre's answer (icon + count) needs a goal icon we do not have, so the bar plays that role. |

**Bar and number move the same way on purpose.** The fill is `left / total`, so
the bar shrinks as the number falls. A bar that filled while the number beside
it dropped would be one fact stated twice in opposite directions.

**Dropped entirely: every translatable string in the header.** `RenderHeader`
never calls `Shell.Copy`. That leaves `board.title` and `board.items_left`
unused in `Shell/Copy.cs`, and it makes two comments in that file stale — the
ones under `win.corner.title` and `win.corner.body` that argue from "the header
two lines above this card says *pile 2 of 3*". It no longer says that. I did
not edit `Copy.cs`; it is not mine this pass.

### The kitten

56 → **104 units**, on a soft cream disc, at the right-hand end of the header
row. In area that is 3.4×. Honestly: her art has about 15% transparent margin
on each side of a 1024² square (`Art/cat_1_short_base.png`), and `Paint` uses
`ScaleToFit`, so what actually renders goes from roughly 44 units of cat to
roughly 79. **1.8× on the diagonal, not 2×** — I would rather write the real
number than the flattering one.

**A real bug fell out of this.** `Paint()` sets
`element.style.backgroundColor = Color.clear` on whatever it paints. The old
`.game__cat` declared `background-color: #E8D9BD` and a 28px radius — a cream
disc that **has never once rendered**, because her art wipes it the moment it
loads. That is why she reads as a cut-out floating on the photograph. She is
now two elements: a `.game__cat-seat` parent that carries the disc and that
`Paint` never touches, and `.game__cat` inside it holding the art.

### The hole for 15-share-card

**`_catSeat` is the tap target.** It is 104×104 — more than double the 48dp
Material asks for — it is the largest non-prop object on the board, and the
disc under her is what makes it look pressable before anything is wired to it.
The other worker's `CatCardScreen` already exposes `Build(parent, cat, room,
…)` and `Show()`; the handler is one line on `_catSeat`
(`RegisterCallback<ClickEvent>`), in `BuildCatPortrait`. **I did not register
it.** Nothing on the seat consumes a tap today.

Note for that worker: the seat is a child of `#header`, which is a normal flow
child of `game-root`, so it sits *behind* `#overlay` — the same trick the back
button relies on. While a win or lose card is up, she is dimmed by the scrim
and cannot be tapped. That is almost certainly what you want.

## 3. The height arithmetic, because this screen has no slack

`Shell/PanelSettings.asset`: reference resolution 390×844, `m_ScaleMode: 2`,
`m_ScreenMatchMode: 0`, `m_Match: 1` — matched to **height**, so the panel is
844 units tall on every phone and only the width moves.

Pile sizes, counted from `Assets/Resources/Levels/*.json`: **36** items for
rooms 01–04, **48** for rooms 05–08, **60** for rooms 09–12. Six columns at the
tile pitch, so 60 items is **ten rows** — and that is the case everything below
has to survive.

Worst realistic device, a Dynamic Island phone (~59 + 34 units of safe area,
which `SafeArea` pads out of the panel): **751 units** of usable height.

|  | before | after |
|---|---|---|
| root padding top + bottom | 16 + 16 | 0 + 4 |
| header | ~71 (two labels + margins) | 104 |
| shelf (padding + slot + margin) | 6+32+6 + 12 = 56 | 5+32+5 + 8 = 50 |
| **left for the pile** | **592** | **593** |
| tile row pitch | 52 + 4 + 4 = 60 | 52 + 3 + 3 = 58 |
| **ten rows need** | **600** | **580** |

So the header grew by 33 units and the pile still came out **13 units better
off**, paid for by the doubled safe-area padding, four units of shelf air, and
one unit off each vertical tile margin. Only the *vertical* tile margins moved:
left/right stay at 4, so the column pitch is still 60 against the 360-unit
`max-width` — six columns, no reflow — and the tile's own 52 is untouched, so
`TileHalf` (26) and `.game__fly` (52) in the animation path still describe it.

**This also says rooms 09–12 were already overflowing.** 600 needed against 592
available: the bottom tile row would be sitting under, or on, the shelf. That
is derived from the numbers above, not seen — no screenshot in the repo shows a
60-tile room. **It is the first thing to check.**

## 4. Contrast, since the header now sits over a photograph

The plaque is **opaque** cream `#F6EEDC` with ink `#332A1E` text and a 2px ink
border — the same pair `AddReturnToMap` and the map's room plaques already use,
so the top-left of the board reads as one family. By the WCAG relative-luminance
formula that pair is **12.2:1**. Opaque was the point: a translucent scrim would
have made legibility depend on which room photograph is behind it, and there are
twelve of them. The kitten's disc is the one translucent thing
(`rgba(246,238,220,0.82)`), and nothing has to be read through it.

## 5. APIs — what I was and was not sure of

Everything new is either already used elsewhere in this file or is plain flexbox.

- `Length.Percent(float)` into `style.width` — exactly what `BuildRoom` does at
  `window.style.width = Length.Percent(50)`. The bar fill is the only inline
  style `RenderHeader` writes; the track's size lives in USS.
- USS: `flex-direction`, `align-items`, `flex-grow`, `flex-shrink`, `width`,
  `height`, `margin-*`, `padding-*`, and the four-way `border-*-width/color`
  and `border-*-radius` longhands. Every one of these is already in this
  stylesheet. I deliberately did **not** use the `border-width` / `border-color`
  / `border-radius` shorthands, for the same reason the rest of the file does
  not.
- `overflow: hidden` on `.game__bar` — the USS spelling of
  `style.overflow = Overflow.Hidden`, which `BuildRoom` already sets in C#.
- `rgba()` — already in this file twice.
- **No** `Scale`/`Translate`/`Rotate` constructors, no `experimental.*`, no
  `background-position`/`background-size`, no `ProgressBar` control. The bar is
  a `VisualElement` inside a `VisualElement`; that cannot fail to compile and
  cannot fail to lay out.

**USS is still not compiler-checked** — Unity's parser has not run. If a rule is
rejected it becomes a console warning at load and does nothing. Ranked by how
unsure I am: `overflow: hidden` on the bar (if it fails, the fill's rounded
corners poke past the track — cosmetic), then `flex-shrink: 0` on the header
(if it fails, a tall pile could squash the header and the cat with it — that
one would be visible immediately).

## 6. What to look for in the screenshot — both platforms

MEMORY: one verified platform has already hidden a completely broken second
one. iOS **and** Android.

1. **Room 01, the board.** One row across the top: back plaque, then a cream
   plaque reading `1/12` with a single filled pip and a full dark bar with `36`
   beside it, then a large kitten at the right. **No sentence anywhere.** If
   you still see "Room 1 of 12", the UXML did not reload.
2. **The kitten is unmistakably bigger** — she should be about twice as tall as
   the back button next to her, sitting on a pale disc. If there is no disc,
   the seat/portrait split did not take.
3. **Room 09 or later, first thing after a full board loads.** This is the
   check that matters most. Ten rows of tiles, and the bottom row must be fully
   visible and clear of the shelf. If it is clipped or overlapping, the height
   budget in section 3 is wrong and the tile pitch has to come down further.
4. **Room 05 (three piles) and room 09 (four piles).** Count the pips: three
   and four respectively, filled up to the pile you are on. On pile 2 of 3 the
   pips must read filled-filled-hollow.
5. **The bar moves.** Take five tiles and shoot again: the number falls by one
   each time and the dark fill visibly retreats. Both must move the same way.
   A number falling while the bar grows is the sign I got the fraction
   backwards.
6. **Legibility over a dark room.** Rooms differ; find the darkest photograph
   in the twelve and confirm the plaque still reads. It is opaque, so it should
   be immune, but "should be" is reasoning and this is the check.
7. **The back button still works** and is not covered. The 52-unit
   `.game__header-gap` is reserved for it by hand — if the plaque overlaps it,
   that number is wrong. It is the only place in this layout that depends on a
   constant owned by another file (`HouseMapView.cs:856`, 44 wide at left 4).
8. **A win card still covers the header**, kitten included, and she is dimmed
   by the scrim like the back button is.
