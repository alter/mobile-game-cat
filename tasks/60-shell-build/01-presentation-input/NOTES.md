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
