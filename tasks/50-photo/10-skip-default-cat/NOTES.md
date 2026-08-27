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
