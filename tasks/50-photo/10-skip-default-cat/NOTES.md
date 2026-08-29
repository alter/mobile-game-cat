# Built, 2026-08-26

A third control on the capture screen — "Not now — give me a kitten" — next to
the two photo buttons, not hidden behind them. The share of players who skip is
one of the numbers this project watches (cat-shelter-mvp.md section 5), and a
skip control that has to be hunted for measures the hunt, not the preference.

`CatTraits.Default` is a **plain grey short-haired tabby with green eyes**:
the most ordinary cat there is. Deliberately not a rare or striking one — a
player who skips should feel she got the same game, not a consolation prize.

Fixed, never random. Two players who skipped must be able to talk about the
same kitten, and a player who skips twice must not meet two different cats.
A test asserts that every field comes out identical across calls.

The path touches no camera, no gallery, no network and no permission: it is a
constant in `Core`, reached by one call. That is what makes VERIFY 2 —
"airplane mode and camera permission denied" — true by construction rather
than by luck.

## Left open

VERIFY names PlayMode tests that tap the control and walk on to
`09-meet-your-cat`, which does not exist yet — there is no screen to walk to.
What is verified today: the traits are constant and complete
(`Tests/Core/CatTraitsTests.cs`), and the button is on screen. Tapping it is
for `14-testflight`.

---

## status:done → in_progress, 2026-08-27

The OUTCOME artefact this task names is not there. What is missing, what does
exist, and why it matters: `tasks/AUDIT-2026-08-27.md`.

---

## The naming half, built at the Core level — 2026-08-27

`AUDIT-2026-08-27.md` item 8 found the real gap: `CatTraits.Default` gives a
deterministic coat, but nothing in `game/Assets` had ever heard of a cat's
*name*. That still stood after re-reading `09-meet-your-cat/task.txt` (name
entry belongs to that screen) and `CaptureScreen.cs` ("this screen stops at
handing over traits" — its own doc comment). Neither owns a name field
anywhere, because the piece of the game that is supposed to ask for one does
not exist: `50-photo/09-meet-your-cat` is `status:todo`, and there is no
`MeetYourCat`-shaped file anywhere under `game/Assets` — `GameBoot.cs` says so
itself ("[09 and 10] decide what happens after a photo is accepted" — neither
is wired in yet).

That changes what "smallest honest thing" means here. Building the name-entry
screen would be building 09, not 10, and 09 is not mine — the boundary the
task brief drew. What *is* mine, and was genuinely missing: the Core-level
primitive a name needs before any screen can use it.

**Added**, following the `GameSave`/`PlayerProgress` idiom directly (plain
ASCII-safe text, zero dependencies, engine-free, `dotnet test`-able):

- `Core/Cat.cs` — the `Cat` entity from `cat-shelter-mvp.md` section 8
  (`Cat name, traits{}, state(1..3), owned_items[]`), narrowed to the two
  fields this task needs: `Name` and `Traits`. `state` is already
  `PlayerProgress.CatState`, `owned_items` belongs to whichever task first
  needs it — adding either here would be guessing at a shape nothing uses
  yet. `Cat.DefaultName = "Kitty"`; the constructor turns a null, empty or
  whitespace-only name into it, so "named" holds both for a player who skips
  outright and one who reaches a name field and leaves it blank.
  `Cat.Skipped` is `new Cat(DefaultName, CatTraits.Default)` — fixed, like
  `CatTraits.Default` already was, so two players who skip can talk about the
  same *named* kitten, not just the same coat.
- `Core/CatSave.cs` — `Write`/`Read`, mirroring `GameSave.cs`'s format and its
  "never throw, malformed input becomes null" contract. Deliberately its own
  file rather than folded into `GameSave`: a cat is named before any `Board`
  exists (the skip path reaches a named cat before the first level starts),
  so tying the two together would make their lifetimes agree by accident.
  Where the resulting string lives on disk (a `Shell/CatSaveFile.cs` beside
  `Shell/SaveFile.cs`, or folded into it) is a decision for whoever wires 09
  up — building that now, with no caller, would be guessing at 09's shape too.
- `Tests/Core/CatTests.cs`, `Tests/Core/CatSaveTests.cs` — 15 new tests:
  blank/null name → default, typed name kept and trimmed, `Cat.Skipped` named
  and deterministic across calls, round-trip through `CatSave` (including a
  non-ASCII name and a name containing a newline), and the same
  never-crash-on-garbage battery `GameSaveTests.cs` runs for the board.

**What this does not do.** No screen shows a name, no field takes player
input, and no file on a real phone holds one yet — `CaptureScreen.Skip()` is
unchanged, still only calling `OnCatReady?.Invoke(CatTraits.Default)`. Wiring
`Cat.Skipped` (or a typed name) into an actual screen is `09-meet-your-cat`'s
job once it exists. VERIFY 1 in this task's own `task.txt` — "tap skip, reach
meet-your-cat with the fixed default traits, name it, proceed to the first
level" — cannot pass today for a reason this task cannot fix by itself: there
is no meet-your-cat screen to reach. So OUTCOME is genuinely closer to true
(the default cat is now nameable, deterministically, with no UI or network
dependency) but not yet true end-to-end, and `status:` stays `in_progress`
rather than moving to `review`.

Raw test output, 2026-08-27:
`dotnet test build/core-tests/core-tests.csproj -v q --nologo` →
`Пройден!   : не пройдено     0, пройдено   152, пропущено     0, всего   152`
(152 passing, up from 137; all new; no regressions).
`build/check-core-purity.sh` → `Core is engine-free: OK`.
`.venv/bin/python -m pytest tools/tests/test_copy_table.py -q` →
`20 passed in 0.02s`.

### One consequence to remember, noted in review

`Cat.DefaultName` is `"Kitty"`, a constant in `Core`, and it is written into
the save file. That is the right place for the *stored* value — a name has to
survive a restart, and `Core` is where things that persist live — but it means
the default name is the one player-visible string that does not come from
`Copy.cs`, and `tools/tests/test_copy_table.py` does not see it because it only
scans `View/` and `Shell/`.

If the game is ever translated (`60-shell-build/16-localisation-ready`), the
name a player *sees* before typing anything should come from the copy table,
while the name already written into a save stays as it was — renaming a stored
default would change the cat of every player who never typed one. Two values,
not one. Worth deciding then rather than discovering it during a translation.

---

## The screen finally has a way in — 2026-08-29

Everything above was true and none of it was reachable. A full playthrough on
both platforms on 2026-08-29 found the same thing the audit above kept
circling: the capture screen, the animal recognition, the colour estimate, the
crop and the marks all worked, all were measured, and **no player had ever seen
any of them**. `GameBoot` opened that screen only when a `capture.txt` debug
file existed beside the save. The game promises "it is her cat" and never asked
anybody for a cat.

Three dead ends were found in one pass, and they are one path, not three
features. What was built today closes all three.

### 1. The photograph is the first screen, once

`GameBoot.OnEnable` gained one branch, placed **after every debug flag and
before the house map**:

```
if (!HasACat()) { ShowCapture(root); return; }
```

`HasACat()` is `CatSave.Read(CatSaveFile.Read()) != null` and nothing else. The
gate is **the saved cat itself**, not a "have we asked" flag — two facts in two
places disagree eventually, and the day they do the game either re-asks a
player who has a cat or drops one who has none into the house. A corrupt save
reads as null and is asked again, which is right: she is better off meeting a
cat than finding a stranger in her house with no explanation.

The save is written the instant she confirms a name, so quitting before that
means being asked again — she never answered.

`capture.txt` still works and now means something sharper than it did: "offer
the photograph again, whatever is already saved", which is exactly the one
thing a first-run gate takes away from us.

Both callers go through one `ShowCapture(root)`, so the checking route
exercises the player's route instead of a parallel copy of it. The flag adds
only the stubbed Vision answer and the photo to push through the pipeline.

### 2. Skip, and the rest of the way

The skip control was already on the screen (2026-08-26, at the top of this
file). What it lacked was a screen to be on. It hands `CatTraits.Default` to
the same `ShowMeetYourCat` a photographed cat reaches — from `OnCatReady` the
two are indistinguishable except by `traits.Origin`, which is the point: she
gets a real cat, not a consolation prize.

**VERIFY 1 of `task.txt` now passes end to end** ("tap skip, reach
meet-your-cat with the fixed default traits, name it, proceed"), which it could
not on 2026-08-27 because there was no screen to walk to. VERIFY 2 (airplane
mode, camera denied) is still true by construction — the path is one constant
in `Core`, reached by one call, touching no camera, network or permission.

### 3. "That's her" led nowhere; now it leads home

Confirming saved the name and did nothing else — measured by a pixel diff
across the tap: the screen before and after were the same file. `OnNamed` now
writes the save **and then** swaps in `HouseMapView`.

Saved before the swap, not after: the map's own build reads the disk, and
anything thrown on the way would otherwise cost her the name she just typed.

The swap is scheduled 120ms out rather than run inside the click, and puts a
veil up first. Both rules are `HouseMapView.StartPlaying`'s, learned there:
clearing the panel mid-dispatch destroys the element the dispatcher is still
walking, and an indicator created and destroyed before a repaint is worse than
none. The veil is wordless, the same choice `ShowOpening(root, null)` already
makes on the way back from the board — there is no honest one-word label for
this that is not either "Loading" (engine vocabulary the game uses nowhere) or
a new key in seventeen tables for something on screen for a second.

### 4. The ending card had no exit

Not this task's screen, but the same dead end wearing a different hat, so it is
recorded here with the other two.

After the twelfth room the card appeared and there was nothing to do: no
primary button by design, no close, and the back arrow in the corner did not
answer a tap. The game's last impression was being trapped.

**The arrow was inert on purpose, and the purpose has expired.**
`HouseMapView.AddReturnToMap` inserts that plaque *before* the overlay in
`game-root` so a card's scrim dims it and swallows its taps — wanted while a
card is up, and its doc names the danger exactly: `Finish` used to CLEAR the
save when the house was finished, so leaving to the map from this card would
have put a player who cleared twelve rooms back at room 1. Since 2026-08-29
that branch does the opposite — it writes `GameSave.Write(_board, _progress)`,
so the map redraws a house with twelve ticks on it.

So `ShowEndingCard` now moves that same plaque to the end of its parent. Later
siblings paint later and are picked first, so the button the player has been
pressing all game becomes the button that works here. **Moved, not rebuilt**:
no second exit to keep in step with the first, no navigation logic copied out
of `HouseMapView`, and — the constraint that mattered — **nothing about the
card itself changes.** It says what it said, offers what it offered, and still
does not propose starting over.

Only this card. The lose card still clears the save and its plaque stays where
it is.

### 5. One failure message that told the player to do the impossible

`photo.our_fault` ended "Try that one again?" — the successor of the deleted
`capture.failed`, and the one instruction on the screen guaranteed not to work.
All three paths that show it fail identically on the same photo every time: the
recogniser could not run (`CaptureScreen.Handle`, `answer.Failed`), the crop
failed after a cat was found, or the picker failed with any code but
`"cancelled"` — including `"unavailable"`, which means there is no picker on
this device to open. A player who had done nothing wrong was sent into a loop,
and the loop looked like her fault because she was the one repeating it.

Rewritten in all seventeen tables. The first sentence is unchanged — it was
already true and already put the fault where it belongs. The second is replaced
by two things that can actually happen:

> Something went wrong on our side. Another photo may work — and a kitten is
> waiting either way.

A *different* photo is a real move: these failures are about this file and this
moment, not about her cat. And the tail is each language's own
`capture.skipped`, word for word, because that is the button standing directly
underneath — the message points at a control the player can see.

No placeholder, and none may be added: the call site reads this through
`Copy.Of(key)` with no arguments, and formatting a reason code into a sentence
here is precisely what `capture.failed` did and why it is gone.

## Seen running — Android, 2026-08-29

Emulator `emulator-5554`, 1080×2340, Android 15, release APK from
`BuildScript.BuildAndroidPlayer`, installed over a wiped `pm clear`. Package is
**`com.sootpaw.game`**, not the `com.DefaultCompany.game` that
`tools/save-forge/README.md` still names — worth fixing there.

| shot | what it shows | log line behind it |
|---|---|---|
| `android-first-launch.png` | a fresh install opens on the photograph, three controls, skip plainly among them | `[GameBoot] branch=first-run` |
| `android-skip-meet.png` | skip → meet-your-cat, the default grey tabby | `[CaptureScreen] cat ready (Skipped): short grey tabby, green eyes` |
| `android-skip-house.png` | "Это она" → the house map, room 1 lit | `cat named: Kitty` → `[GameBoot] branch=map (after the photograph)` → `[HouseMap] built 12 rooms, ... open=1` |
| `android-skip-board.png` | the room, playable — two taps moved two props to the shelf | `[Board] took 1, shelf=1...` / `[Board] took 3, shelf=2...` |
| `android-ending-card.png` | the ending card with the plaque lit above its scrim | `[Board] ending card: the way back to the house is above it` |
| `android-ending-back.png` | tapping it lands on her finished house, twelve ticks | `[HouseMap] back to the map via up` → `done=[1..12], open=0` |
| `android-photo-failed-ru.png`, `android-photo-failed-en.png` | the rewritten failure line, three wrapped lines, skip button under it | `[CaptureScreen] vision failed: not a decodable image` |

"Asked once" was checked by relaunching after the skip: `[GameBoot] branch=map`,
no photograph offered. `cat.save` on the device reads
`name Kitty` / `traits grey tabby short green  Skipped`.

The ending was reached with `tools/save-forge/house.save` — one tile short of
the last pile of room 12 — which is what that tool is for. `errors.txt` on the
device holds nothing but the vision failure that was provoked on purpose.

Tests after the change: `dotnet test build/core-tests/core-tests.csproj` →
227 passed; `.venv/bin/python -m pytest tools/tests -q` → 245 passed.

## Left open

- **iOS was not checked.** The brief for this pass forbade running the
  simulator. Every claim above is Android only, which breaks this project's own
  "check both platforms" rule — one verified platform has hidden a completely
  broken second one here before. The iOS pass is outstanding.
- **The veil between "That's her" and the map was never photographed.** The map
  built 172ms after the confirm on this device, so a screenshot one second later
  already showed the map. The code is the same shape `HouseMapView` uses and
  costs nothing; it protects a slower device, and it has not been seen doing so.
- **`board.txt` still ends in a dead end.** A board reached by that flag has no
  plaque to lift, so the ending card there has no exit —
  `ShowTheWayOff` logs a warning saying exactly that. This is consistent with
  what `GameBoot` already states about the flag ("it gets no way back to the map
  on purpose"), but somebody finishing the house through `board.txt` will still
  be stuck, and should read the log rather than file it again.
- **A finished house has no lit room and the legend still says "tap the lit
  number".** Coming back from the ending card is the one moment a player sees
  twelve ticks and nothing to press. Changing `map.legend` for that state is a
  copy decision on somebody else's screen and was left alone.
- **Skip is still not counted.** `CaptureScreen.Skip()` fires no analytics event,
  so the share of skippers — one of the numbers `cat-shelter-mvp.md` section 5
  names — is not being measured even though the path is now real and reachable.
  That is `70-analytics`, and it matters more from today than it did yesterday.
