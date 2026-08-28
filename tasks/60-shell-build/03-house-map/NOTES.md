# Notes - why this task exists at all

Source: cat-shelter-tasks.md, "6.2.1 was missing, and it serves a motivator
the MVP names explicitly" (lines 740-753).

There was no screen showing the whole house. 6.2 binds a room to a level and
6.10 (11-post-level-12) shows an end card, but nothing in between shows how
much is left. The audience analysis lists, as motivator #3, "completeness — an unfinished
set is nagging" (an unfinished set nags). A house with four clean
rooms and eight dirty ones makes that feeling visible, for the cost of one
screen. Leaving it out would discard a retention driver the design already
identified.

It also fixes something subtler: progress in this game is meant to be *seen*,
not counted - a room brightens, the cat improves, objects appear - but all of
that is visible only one room at a time without this screen. The map turns
twelve separate improvements into one accumulating thing, which is what makes
an unfinished set nag.

## Placeholder art, and what the real art must preserve

40-art/06-house-map has not delivered art yet (still `status:todo`), so
`View/HouseMapView.cs` falls back to a painted placeholder for the background
and all 36 cells, the same way `CoatBuilder.LoadBase` falls back when a coat
texture is missing - one warning, not a hole in the screen.

The placeholder tells the three states apart by silhouette, not shade:
- dirty - a plain square, flat dark fill, no icon.
- partial - a split tile: two halves across a hard vertical edge at 50%, no
  gradient between them.
- clean - a full circle (a different outline from the other two, not a
  lighter square) with a small mark.

art-prompts.md already commits the real art to lightness-only, hue-fixed
tiles ("dark, halfway, light"), which is a narrower rule than the
placeholder's shape trick and is not something this task is overriding. What
the placeholder's choices point at, and what the real art has to keep, is
two things art-prompts.md already says but that are easy to lose while
drawing thirty-six small files one at a time:
1. "partial" needs a **crisp boundary**, not a blend - at 256x256 (and
   smaller once laid into the map), a soft gradient between lit and unlit
   reads as one smudged tile, not "half done". The placeholder's hard edge
   at 50% is standing in for that requirement, not inventing a new one.
2. The three states of every one of the twelve rooms must stay orderable
   dark-to-light with colour removed (40-art/06's own QA check 2) *and* the
   full set of twelve must still read as "mostly unfinished" or "mostly
   done" in one glance once laid out together (QA check 3) - the thing this
   whole task exists to make visible.

## The screen was unreachable until it was wired — 2026-08-28

Built, verified, and impossible to open. `HouseMapView.Requested` reads a
`housemap.txt` flag exactly as `CoatGridView.Requested` does, and its author
concluded no wiring was needed because the pattern was already "present in
GameBoot". It is present there in the sense that **GameBoot is what asks**:
`CoatGridView` is reached because `OnEnable` tests its `Requested` property and
adds the component. Nothing tested this one, so the property was a door with no
corridor leading to it.

Found by running the build rather than by reading it: the flag was on the
device, the screen never appeared, and the board came up instead. Two builds
were spent before the cause was looked for in the right place.

**The coordination error was mine.** The author was told not to touch
`GameBoot.cs` because another agent held it, and was invited to report any line
it needed instead — which it did, saying none was needed. A file boundary drawn
to prevent a collision also prevented the one line that made the work usable,
and neither side noticed because the screen looked complete from every angle
except running it.

One branch added to `GameBoot.OnEnable`, beside the other three.

## Run with the real art, and what three builds fixed — 2026-08-28

`android-house-map-real-art.png` is the screen on the emulator at 1080×2340
with the delivered art. Twelve rooms inside the house, numbered, all in the
dirty state because the save is fresh, legend wrapping, nothing off-screen.
`android-before-layout-fix.png` is the first run, kept for the comparison.

Three defects, none of which any amount of reading would have found:

**The screen could not be opened at all.** Recorded above — no branch in
`GameBoot` asked for it.

**The layout was written in points.** 480×420 for the house and 440 for the
grid were chosen against placeholder cells. With the real 928×1664 background
the house drew over the grid and the outer columns ran off both edges. Now
everything is a percentage of the panel, so it fits whatever screen it lands
on rather than the one it was written on.

**Percentage heights collapsed the cells to thumbnails.** A child's percentage
height resolves against its parent, and the grid had no height of its own, so
twelve rooms rendered a few pixels tall. Giving the grid an explicit height
fixed it. This is the failure that looks most like it worked: the screen drew,
the cells were there, and they were the wrong size by two orders of magnitude.

**And one thing that was measured rather than guessed:** the delivered
`map_background.png` is opaque with a white surround — 255,255,254 at three
corners. The dark page it was drawn on framed the house in a white rectangle,
so the page now matches the image instead of fighting it.

## What still looks unfinished, said plainly

- **Two columns, not three.** The grid is narrower than three cells plus their
  margins, so twelve rooms stack as 2×6. In a house this tall that reads fine,
  and it was not worth a fourth build to change — but it is an accident rather
  than a decision, and whoever gives this screen a real navigation entry should
  decide it on purpose.
- **Rooms 1 and 2 sit slightly over the roofline.** Cosmetic.
- **The white surround is still visible** as a panel behind the house. Matching
  the page to it hid the contrast, not the rectangle. The real fix is art with
  transparency or a background sized to the house itself.

None of the three blocks the screen from doing its job, which is showing twelve
rooms in three states derived from one source.

## Both platforms, same screen — 2026-08-28

`ios-house-map-real-art.png` is the same screen on the iOS simulator (iPhone 17,
1206×2622) beside `android-house-map-real-art.png` on Android (1080×2340). The
layout is identical in structure, the notch is respected, and the proportional
sizing that fixed Android needed nothing further for a different aspect ratio —
which is the point of having done it in percentages.

Route to it on iOS: `xcrun simctl get_app_container booted com.DefaultCompany.game data`
and drop `housemap.txt` into its `Documents/`, which is where
`Application.persistentDataPath` lands on iOS. Same flag, different folder.

## The placement is wrong, and the screenshot shows it

The owner pointed this out and both screenshots confirm it: **room 9 is plainly
an attic** — sloped ceiling, dormer window — and it sits in the fifth row of a
numbered grid, in the middle of the house. Room 2 is as plainly a kitchen and
sits under the roof.

A house map whose rooms are not where they belong in the house is a numbered
list drawn on a picture of a house. The grid was the placeholder's logic
surviving into the real art, and nobody looked at what the twelve rooms
actually *are* until the pictures existed.

`tasks/40-art/06-house-map/ROOM-PLACEMENT.md` is being written now: what each
of the twelve rooms is, which floor it belongs on, and coordinates relative to
the house's own bounding box — which is **x 6–93%, y 9–92%** of
`map_background.png`, measured, not the whole file. Percentages against the
file would place every room about 7% too far up and left.

## The rooms are now where they are in the house — 2026-08-28

`ios-house-map-placed.png` and `android-house-map-placed.png` are the same
screen on both platforms after the fix. Room 09, the attic, is under the roof.
Room 12, the reading nook — the second sloped-ceiling room, which nobody had
noticed was one — is under the roof beside it. The kitchen and the entry hall
are on the ground floor. Compare with `*-house-map-real-art.png` beside them,
where the attic sits in the middle of the house because the cells were laid out
by number.

Three things were wrong and all three are fixed:

**1. The grid.** Twelve cells in a flex-wrap grid is a numbered list drawn on a
picture of a house. `HouseMapView.Placements` now carries a measured position
per room; `tasks/40-art/06-house-map/ROOM-PLACEMENT.md` says what each of the
twelve rooms is and how the coordinates were derived.

**2. Percentages of the element are not percentages of the picture.** The
background paints with ScaleToFit, which letterboxes: the drawn house occupies
only part of the element, and how much depends on the screen's aspect ratio.
Placing cells as a percentage of the element therefore drifts by however much
letterboxing there is — which is most of what put cells over the roofline on a
1080×2340 phone. `HouseMapView.FitToPicture` sizes a house box to the letterboxed
image rect on every geometry change, and the cells are percentages of that.

**3. The white card.** The delivered background sat on an opaque white
rectangle, which showed as a white card behind the map on a cream screen.
`map_background.png` is now trimmed to the painted house and its corners made
transparent; the uncropped original is kept at
`game/Assets/Art/delivery-originals/`.

### Two columns, not three, and that is the answer

Earlier notes list "two columns instead of three" as a defect. It is not. A
row-by-row scan of the background shows the walls standing at local x
10.9%–90.1% and the interior recess narrower still; three columns of room art
inside that would be about 90px wide on a 1080 phone. Two columns in the body
and one under the roof is what the house has room for.

### The trap the crop set, and how it cost two builds

Cropping the background to 807×1381 made it non-power-of-two, and Unity's
importer rescales such textures to the *nearest* power of two per axis —
807→1024 and 1381→1024. The house came out square. `nPOTScale: 0` on that one
file fixes it.

Setting the same flag on the 24 room backgrounds (1856×3328, equally NPOT) took
the APK from 45 MB to **133 MB**: an NPOT texture loses its compressed format,
and 24 room images at 1856×3328 uncompressed is 240 MB of textures against 58 MB
compressed. Those 24 files are back on `nPOTScale: 1` because no screen loads
them yet. **Whoever wires `60-shell-build/02-room-piles` has to decide this
properly** — either accept the slight aspect change from 1856×3328 → 2048×4096,
or re-export the rooms at a power-of-two size, or pay the size. Do not simply
copy the flag across.

## The thumbnails are gone; the map says where you may go — 2026-08-28

`ios-thumbnails-before.png` is what the owner saw running: twelve rooms drawn
as their own photographs, all desaturated because a dirty room is drawn
desaturated, coming out as twelve near-identical grey-green smudges with an
unreadable white number on each. His verdict was that you could not tell what
any of them were, and he was right — at that size the picture carries nothing a
player can use, and it hides the thing they need.

**A map's first question is where you may go.** That question had no answer on
this screen at all: nothing said which room was next, which were finished, or
which were shut. Now:

| state | drawn as |
|---|---|
| the room to play | cream plaque, heavy ink ring, the largest number on the map, and a bar under it when the room is part-cleared |
| done | sage circle with a tick above the number |
| locked | sunk into the wood, dim, and **smaller** — size does the work, not only colour |

`ios-numbers-fresh-game.png` is a new game: room 1 lit, everything else shut.
`ios-numbers-in-progress.png` and `android-numbers-in-progress.png` are rooms
1–4 done with room 5 a third cleared, on both platforms.

Three states told apart by shape and size rather than by tint — the same rule
art-brief.md section 9 sets for the room cells, which three shades of one
colour do not satisfy.

`PlayerProgress.AccessFor` is the new rule and it lives in Core with six tests,
one of which walks the whole 37-pile game asserting that **exactly one room is
open at every point** — a map offering two playable rooms, or none mid-game,
misleads worse than one that says nothing.

The room art is untouched on disk and still named as art-brief.md section 9
requires. Where a room's picture belongs is `60-shell-build/02-room-piles`, at a
size where it can be seen. `PaintPlaceholder` went with the thumbnails: it had
no caller left, and dead code that draws things is the kind that gets revived
by accident.

### Still not done, and it is the obvious next thing

**The map is not a hub.** The plaques are not tappable, so a player cannot
choose a room from here — the board simply starts wherever the save says. The
owner ran the game and was dropped into room 3 with no explanation and nothing
chosen, which was partly a leftover test save on the device, and partly this:
nothing connects the map to the board in either direction. Wiring the tap needs
`DebugGameView` to accept a starting room, which it does not today.

## The numbers climb, and the lit one starts the game — 2026-08-28

Two things the owner hit within a minute of looking at the previous version.

### "9 на самом верху, 12 под ней" — the order made no sense

It made sense from the inside, which is the trap. Each room sat where its
*picture* belonged in a house: the attic art under the roof, the kitchen on the
ground floor, all measured off the background (`ROOM-PLACEMENT.md`). That was
right while the cells were photographs of rooms. The moment they became numbers,
the number was the only thing on screen, and a scattered sequence reads as a
mistake however principled the scatter is.

Now: odd left, even right, bottom to top, 11 and 12 alone under the roof.
`ios-numbers-in-order.png`.

**What was given up, so nobody restores it by accident.** Room 9's art is an
attic and it now sits mid-house. Room 12's is a reading nook and still lands
under the roof by luck of the numbering. Having both properties needs the two
sloped-ceiling pictures to *be* rooms 11 and 12 — a reassignment of which
picture belongs to which room number, which is a data change and not a layout
one. Worth doing; not worth doing quietly.

### A lit button that does nothing

The owner tapped the glowing room and the game did not start. A plaque that
lights up is a promise, and this one made it while nothing was wired behind it.

`HouseMapView.StartPlaying` now runs on a tap of the open room, and only that
room — the other eleven are `PickingMode.Ignore`, because a locked room that
reacts is a different lie.

It needed no room parameter. Exactly one room is ever open and it is always the
save's cursor, which is where the board starts anyway — `AccessFor` makes that
true and a test walks the whole game asserting it. So "tap the open room" and
"start the board" turned out to be the same instruction.

The one real piece of plumbing: the map calls `root.Clear()` when it takes over,
which destroys the UXML skeleton the board needs. `StartPlaying` clones
`visualTreeAsset` back before adding `DebugGameView`, and says so on screen if
that asset is missing rather than leaving an empty panel.

**Verified by an actual tap, on Android.** `android-tap-before.png` is the map
with room 1 lit; `android-tap-starts-room-1.png` is what `adb shell input tap`
produced: "Room 1 of 12 · pile 1 of 1". The same tap could not be automated on
the iOS simulator — posting a synthetic click needs macOS accessibility
permission the terminal does not hold — so **iOS is verified for the layout by
screenshot and for the tap only by sharing the code path with Android.** A human
tap on the simulator is the outstanding check.

## The tap fired, logged itself, threw nothing — and the map stayed put

The first version of the tap looked like it worked and did not. The owner
reported it plainly: "1 кнопка светится - кликаю и ничего не происходит".

Two separate causes, and the first one hid the second for an hour.

**The wrong one I chased.** `houseBox` was `PickingMode.Ignore`, which looked
like the obvious culprit for a child that will not answer a tap. It was not —
Ignore excludes the element itself, not its children — but it was plausible
enough to spend two builds on.

**The one that mattered.** `StartPlaying` rebuilt the panel from inside the
pointer callback: `root.Clear()` destroys the very element UI Toolkit is still
walking the propagation path through. The handler ran, wrote its log line,
threw nothing that any log recorded, and the map stayed exactly where it was.
The swap now runs one frame later via `root.schedule.Execute`, and its body is
wrapped so a failure lands in `tap.txt` and on the screen instead of vanishing —
UI Toolkit swallows what an event callback throws.

### What actually established this, and what did not

Two of my own measurements were wrong before the real one landed, and both are
worth recording because they are easy to repeat:

- **A screenshot comparison proves nothing if the first screenshot is of a
  screen that had not finished drawing.** "The click changed 98.8% of the
  pixels" was the board appearing 20 seconds after launch, not the tap doing
  anything. The simulator needs ~30 seconds before a screenshot means anything.
- **A synthetic click needs the Simulator window to exist**, and it disappears
  on its own — `xcrun simctl terminate`/`launch` can leave the app running with
  no window at all. Three "the tap does nothing" results were clicks posted at
  coordinates where no window was. `tap_ios.sh` in the session scratchpad
  re-locates the window and re-derives the mapping on every call for this
  reason, and still reports when the window is gone rather than clicking blind.

**Evidence.** `android-tap-opens-room-1.png` — final build, `adb shell input
tap` on the lit room, "Room 1 of 12 · pile 1 of 1". `ios-tap-opens-room-1.png` —
same result on the iOS simulator, from the build that introduced the deferred
swap. The final build's only difference from that one is the removal of
temporary probes, and its tap has **not** been re-confirmed on iOS: the
Simulator window could not be brought back from the shell afterwards. Android
carries the final build; iOS carries the fix.

## The log reaches the simulator. It always did. — 2026-08-28

Everything above was diagnosed by comparing screenshots, because the code and
this project's own brief both said Unity's `Debug.Log` reaches no console on a
device or simulator. **That is false**, and it was never tested:

```bash
xcrun simctl launch --console booted com.DefaultCompany.game   # every Debug.Log line
"$ADB" logcat -c && "$ADB" logcat -s Unity                      # the same on Android
```

A day went into inferring from pixels what one line of log already said. The
claim has been corrected in `AGENT-BRIEF.md`, `Shell/DeviceLog`,
`Shell/EveningReminder`, `View/CoatBuilder` and `60-shell-build/04`'s notes,
because a false premise repeated in five places is how it survives.

The file-based records keep their place — a capture script or a tester's phone
has no console attached — but they are the second channel now, not the only one.

### What the trace says, end to end

Real run, real tap, `adb logcat -s Unity`:

```
[HouseMap] built 12 rooms, cursor=1/0, done=[], open=1
[HouseMap] picture 366.45x627.37 at 0,7.69 in element 366.45x642.74
[HouseMap] tap room 1 via up/plaque
[HouseMap] swap: clearing 3 children
[HouseMap] swap: cloned skeleton, game-root=True, pile=True
[Board] enabled, skeleton found
[HouseMap] swap: board added
```

**`via up/plaque`** is the line worth keeping. The tap arrives as a
`PointerUpEvent`, not a `ClickEvent` — so the first version of this screen,
which registered only `ClickEvent`, could not have worked on either platform.
The second handler was added as a belt-and-braces guess and turned out to be
the one that fires. Do not "simplify" it away.

The 1.5 seconds between "skeleton found" and "board added" is the board loading
levels and prop sprites, not the swap.

### Cost

Seven `Debug.Log` calls on this path, none in a loop or a per-frame method.
Cheap enough to keep in a shipping build, and the first thing anyone
investigating this screen should read.

## The attic is under the roof again, without breaking the numbering

Two commits ago the numbers were made to climb — odd left, even right, bottom to
top — and the price was that room 9's picture is an attic sitting mid-house.
That price has now been refunded: `room_09_*` and `room_11_*` swapped pixels,
`map_room_09_*` and `map_room_11_*` with them. The `.meta` files stayed where
they were, so every guid, import setting and reference is untouched — only the
image behind each name changed.

Rooms 11 and 12 are the two slots under the roof, and they now hold the two
rooms drawn with a sloped ceiling: the attic and the reading nook. The numbers
still climb. Both properties, no compromise.

Nothing in the level files or the code refers to a room's *theme*, only its
number, so this is a change of picture and not of content. The uncropped
delivery originals are unaffected; `git show` on the previous commit recovers
either file if the swap is ever judged wrong.

## A loading indicator nobody could see — 2026-08-28

The owner played it and named the defect exactly: "кликаю - ничего не
происходит, похоже, там загрузка идет... юзер не понимает что происходит,
кликает и раздражается, что все зависло." Building the board takes about a
second and a half of level and sprite loading, and the screen just sat there.

`ShowOpening` puts a veil over the map the instant the room is tapped — the
words "Opening the room…" and a bar that **slides back and forth rather than
filling**, because the work behind it has no measurable progress and a number
that jumps 0→100 lies more than no number at all. It also swallows further taps,
which is the other half of what the owner described.

**And the first version of it did nothing at all.** The swap was scheduled for
the next scheduler tick, which arrives before the panel repaints, so the veil
was created and destroyed without ever reaching the screen. Measured, not
guessed: six screenshots taken in the second after a tap all showed the map,
unchanged, while the log showed `swap: clearing 4 children` — the fourth child
being the veil that nobody saw.

The swap now waits 120ms, about seven frames. The veil then stays visible
through the whole build for free: the board build blocks the main thread, no
repaint happens during it, and the last painted frame is the veil.

**Confirmed on Android** — `android-opening-the-room.png`: "Opening the room…"
and the bar over the dimmed map, caught by screenshotting immediately after
`adb shell input tap`. The log puts the delay at 133ms between the tap and the
swap, which is the 120ms doing its job.

**And on iOS** — `ios-opening-the-room.png`. That took finding the right tool
instead of persisting with the wrong one. Three rounds went into driving the
host's mouse, which cannot work reliably: the Simulator window moves and closes
on its own, so every coordinate computed from it is stale before it is used;
between two runs it jumped from (629,71) to (482,82), and `simctl terminate` can
leave the app running with no window at all.

Meta's `idb` is the documented answer and takes coordinates in the **device's own
point space** — no window, no bezel, no mapping. `idb ui tap 142 595` landed
first try. Setup and the one conversion that matters (screenshot pixels ÷ 3 =
points) are in AGENT-BRIEF.md. `simctl terminate`/`launch` can leave the app running
with no window at all, and between two runs the window jumped from (629,71) to
(482,82). Any coordinate computed before that move points at nothing. Read the
window's position immediately before every click, or click nothing.
