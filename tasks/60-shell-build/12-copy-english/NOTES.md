# Every word the player reads, re-read against the game as it is — 2026-08-28

The task shipped `verify:passed` on 2026-08-27 against a narrow claim: zero
non-English strings anywhere a player can see them. That claim still holds and
was not what this pass was about. Several screens changed underneath the table
on 27–28 August — the house map became the game's first screen, the win card
gained the room's own before/after photographs, the lose card stopped offering
anything, the ending card appeared — and the copy had not been read against any
of them. The owner played the build twice and said both times that he did not
understand what was happening or what he was meant to do.

Method: every key in `Copy.cs` was grepped for its use, and every use read on
the screen it appears on, including the three device screenshots this
repository already holds (`06-win-screen/ios-win-room-before-after.png`,
`07-lose-screen-fake-door/ios-shelf-jammed.png`,
`11-post-level-12/ios-every-room-is-clean.png`) and the map
(`03-house-map/ios-numbers-in-progress.png`). Reading rendered text was worth
more than reading the table: two of the seven findings below are invisible in
the table and obvious in the screenshot.

No key was renamed, added or deleted. Every key is referenced by name from C#,
and the four files that do the referencing — `DebugGameView.cs`,
`HouseMapView.cs`, `CaptureScreen.cs`, `MeetYourCatScreen.cs` — were out of
bounds for this pass. `tools/tests/test_copy_table.py`: 32 passed, before and
after.

---

## What changed

### 1. `win.room_clean.title` — "Room clean!" → "The room is clean"

The exclamation mark congratulates the player for a tap. Section 2 of
`cat-shelter-mvp.md` lists what this audience is to be spared — punishment,
rushing, competition, humiliation — and cheering is the same register from the
other side. The stronger argument is the screenshot: this card now carries the
room's own dirty and clean photographs, side by side, directly under the title.
The pictures make the case. A title over them should name what they are and
stop.

"Room clean" was also telegraphic — an adjective doing a verb's work, the
register of a status light. 17 characters against the old 11, and the ending
card proves the width is there: "Every room is clean" (19) sets on one line in
the same style.

### 2. `win.corner.title` — "Corner cleared!" → "Pile cleared"

The comment above this key already contained the diagnosis and did not act on
it: the board header two lines above the card reads `Room 3 of 12 · pile 2 of
3`, and the card answered with a different noun for the same object. A player
meeting both within a second has to deduce that a corner is a pile.

Which word wins is not arbitrary. "Corner" comes from `cat-shelter-mvp.md`
section 3 ("a room clears in parts: a corner, a wall, a windowsill") and is the
warmer word, but it is wrong on its face for the four-pile rooms and it appears
exactly once in the shipped game. "Pile" is on screen at every moment of play,
is reinforced by `board.items_left`, and is literally what the player is looking
at. The card yields to the board.

Exclamation dropped for the same reason as (1). The body, "The kitten came over
to look.", is untouched — it is the one line of warmth on that card and it earns
its place.

### 3. `lose.body` — "Levels finished: {0}." → the rule, and that nothing is lost

Now: *"Every slot is full and no three the same. The pile goes back the way it
was."*

The old line defended itself in a comment as "the count, which is a fact and not
a reproach". `07-lose-screen-fake-door/ios-shelf-jammed.png` shows the fact it
actually states: **"Levels finished: 0."**, rendered at the exact moment the
player has just failed. A fact can be a reproach when the fact is zero, and this
is the one screen `cat-shelter-mvp.md` section 2 and D4 both go out of their way
to keep free of blame.

Two further faults, either sufficient on its own:

- **"Levels" is engine vocabulary.** The player is never shown that word
  anywhere else. The header counts rooms and piles; the map counts rooms. The
  single number on this card was denominated in a unit the game never taught.
- **It does not say what happened.** "Shelf jammed" is exact and terse. A
  first-time player who has not yet worked out that three of a kind clear a slot
  cannot distinguish a jam from a bug — and the owner's report is that this
  player exists.

The replacement states the rule at the one moment the player is guaranteed to
want it, then says the pile comes back whole. That second sentence is the
no-guilt half of D4 ("losing is not punishment here; replaying a two-minute
level is not") said to the player instead of only to the decision log. This
audience's fear is lost progress; the card should answer it rather than count.

The `{0}` is gone. `DebugGameView.Finish` still passes `_levelIndex`, which is
harmless — `string.Format` ignores an argument the format string does not
use — but the argument is dead and should be dropped whenever that file is next
open. Flagged rather than done: `DebugGameView.cs` was out of bounds.

### 4. `house.complete.body` — five wrapped lines → four, same ending

Was: *"All twelve of them, and one kitten who no longer has anywhere to hide her
finds.\n\nThat is as far as this house goes for now."*
Now: *"All twelve, and a kitten with nowhere left to hide her finds.\n\nThat is
as far as the house goes for now."*

`11-post-level-12/ios-every-room-is-clean.png` shows the old text as five
wrapped lines on a card that, in that screenshot, held nothing else. It holds
more now: room 12's before/after pair was added to this branch after that
screenshot was taken (`DebugGameView.Finish` calls `ShowRoomTransformation`
before returning). The same paragraph now sits under two photographs on a phone.
Nothing is said that was not said before; the image of a kitten with nowhere
left to hide her finds is intact.

### 5. `levels.unavailable.body` — two false hedges removed

Was: *"The rooms could not be loaded this time. Please reinstall or try again
later."*
Now: *"The rooms could not be loaded. Please reinstall the game."*

Shipped level data is either in the app or it is not, so there is no "this
time", and waiting changes nothing about it. Telling someone with a broken
install to try again later sends them away to fail again. The card should carry
the one instruction that can work. (This card is gated by
`test_ship_levels.py` and `HeadlessRunTests` and should never fire; it was read
and corrected anyway, because a floor that lies is not a floor.)

### 6. `capture.hint` — the reason, then the advice

Was: *"A photo where she fills most of the frame works best."*
Now: *"The kitten in the game gets her colours. Fill the frame with her if you
can."*

On the screen the entire concept rests on — `cat-shelter-mvp.md` section 5, "the
main feature and the main source of cheap installs" — the one wrapping label was
spending itself on camera technique for someone who had not been told what the
picture is for. `capture.title` cannot carry the reason instead: `CaptureScreen`
builds it at `fontSize 26` and never sets `whiteSpace = Normal`, so that label
cannot wrap and anything longer than about "Show us your cat" runs past the
padding. That is a string which reads fine in this table and breaks on the
device, and it is now recorded in a comment beside the key so the next person
does not discover it in a screenshot.

Framing advice kept in the second half: it is what holds Vision's rejection rate
down, and dropping it to make room would have traded one defect for another.

### 7. `notification.body` — "It is waiting" → "She is waiting"

The kitten is "she" in every other string in the table — "the kitten likes it
better already", "her finds", "a photo where she fills more of the frame",
"Copying her colours…", "Got her." The single place the game speaks to a player
who is not holding the phone called her an it.

---

## What is missing, and not filled in

### The whole house map is outside the table

**This is the largest finding in the pass and none of it could be fixed here.**
The map became the game's first screen on 2026-08-28. Every word on it is a
hard-coded literal in `HouseMapView.cs`, which was out of bounds:

| Line | Where |
|---|---|
| `"house map: 12 rooms"` | `HouseMapView.cs:78` |
| `"tap the lit number to play it · ticked rooms are done · dim rooms are still locked"` | `HouseMapView.cs:180-182` |
| `"no levels loaded — nothing to map"` | `HouseMapView.cs:87` |
| `"the board's layout is missing — DebugGame.uxml is not assigned to the UIDocument"` | `HouseMapView.cs:787-788` |
| `$"could not open the room: {e.Message}"` | `HouseMapView.cs:812` |
| `$"could not open the map: {e.Message}"` | `HouseMapView.cs:975` |

They pass `test_no_player_visible_english_outside_the_table` only because that
test's `SENTENCE` regex requires a capitalised first word, and every one of
these is lowercase. That is a hole in the check, not a property of the strings.
Worth closing separately; naming it here so the next reader knows the green test
is not evidence about this file.

Read on the device (`03-house-map/ios-numbers-in-progress.png`), the two
player-facing ones are also the weakest copy in the game:

- **`"house map: 12 rooms"`** names the screen rather than the place. It is
  lowercase, in the register of a debug label, and it is the first sentence in
  the product. It tells a player nothing they cannot see: there is a house, and
  the rooms are numbered up to 12.
- **The legend** is a three-clause key to a colour code, set in the smallest
  type on the screen, wrapping to two lines. It explains the *notation*. It does
  not say there is a kitten in this house, that the rooms are dirty, or why
  anyone would clean them. **Nothing on the first screen does.**

Proposal, not shipped, because it needs `HouseMapView.cs` and a product call on
what this screen is for:

- title → something that names the place and the stake in one short line, e.g.
  *"A kitten lives here"* over the house, with the room count dropped (the map
  shows it);
- legend → shortened to the one instruction that is actionable, *"Tap the lit
  room"*, with "done" and "locked" left to the tick and the dimming, which
  already say it by shape (`Cell`'s own doc argues exactly this for the plaques
  and then writes a legend that repeats it in words);
- all six literals moved into `Copy.cs` under `map.*` keys in the same change.

Not shipped as keys here on purpose: `test_every_declared_key_is_used` fails on
a key nothing references, and nothing can reference them until `HouseMapView.cs`
is editable. **The key and its call site have to land in one commit.** Same
constraint, same reason, as the next item.

### `map.returning` — a gap someone else already named

`HouseMapView.ShowOpening` is called with `null` on the way back from the board,
and its own doc-comment says why: *"`Shell/Copy.cs` holds every string a player
reads and I was not allowed to edit it in this pass... the return veil is the
bar alone."* So the trip out of the board is a moving bar with no word, while
the trip in says "Opening the room…".

I could not close it from this side either, for the mirror-image reason: the key
would be unused until `HouseMapView.cs` passes it. Suggested value when someone
holds both files: **"Back to the house"** — "the house", not "the map", because
that is the place; `map.opening` already says "the room" rather than "loading"
for the same reason.

### Nothing anywhere teaches the rule before it is needed

The owner's complaint — he did not know what he was meant to do — is partly a
copy problem and this pass could only reach the tail of it. After change (3),
the matching rule is written down in exactly one place in the game: the card
shown when you have already lost to not knowing it.

There is no first-run line, no hint on level 1, nothing that says three of a
kind clear a slot or that a full shelf with no match ends the pile. `board.title`
and `board.items_left` are both counters; neither is an instruction.

**This is a product decision and it is not mine to make.** It implies either a
first-level hint line, a one-time card before room 1, or a scripted opening
pile — three different products with three different costs. What I can say from
this pass is that the cheapest shape that would work is a single line under
`board.items_left`, shown only while `_levelIndex == 0`, reading something like
*"Three of a kind clear a slot."* That is one key, one `if`, and no new screen.
I have not written the key, because a key nothing uses fails the test suite and
because inventing the screen is the thing the brief forbids.

### The name she typed is never used

`MeetYourCatScreen` asks *"What's her name?"*, `Cat` stores it,
`CatSaveFile` persists it — and no string in the table ever says it. The kitten
is "the kitten" and "your kitten" everywhere, including in the notification,
where `cat-shelter-mvp.md` section 4's own worked example is
*"Murzik found something behind the couch"* — that is, the **name**. Using the
name is most of the point of asking for it.

Not shipped, and this one is a trap worth spelling out: `EveningReminder.cs:52`
calls `Copy.Of("notification.title")` with **no arguments**. Adding a `{0}` to
that value renders the literal characters `{0}` in a push notification on a
player's lock screen. The key and the call site must change together, in
`EveningReminder.cs`, which was out of bounds.

### `board.title` reads "pile 1 of 1"

Room 1 has a single pile by design (`cat-shelter-mvp.md` section 3: "The first
room closes in one level"), so the very first header a player ever sees is
`Room 1 of 12 · pile 1 of 1` — a counter of one, which is noise at the moment
attention is scarcest. Suppressing the second clause when `PilesIn == 1` is a
change in `DebugGameView.Render`, not in this table, and it would need a second
key. Left alone; recorded because it is a first-run problem and the first run is
what this pass was called in for.

---

## What was checked and left exactly as it was

Not everything that could be reworded should be. These were read against their
screens and are correct:

- `board.title`, `board.items_left` — accurate, and "pile" is now the settled
  word for the unit (change 2).
- `win.room_clean.body`, `win.corner.body` — warmth without praise; they
  describe the kitten, not the player.
- `win.before` / `win.after`, `win.next`, `lose.title`, `lose.replay`,
  `map.opening`, `house.complete.title` — accurate on the screens they appear
  on, short enough for a card.
- The five `photo.*` outcomes — each says what happened and then offers a way
  forward, in that order, and none of them blames the player. `photo.dog`
  ("Lovely, but this shelter is for cats.") is the best line in the file.
- `meet.*`, the remaining `capture.*` — checked and left. `capture.skip`
  ("Not now — give me a kitten") is in the player's voice where most buttons are
  not, but so is `meet.confirm` ("That's her"), and both read well; churning
  them would add risk without removing a lie.
- `notification.channel`, `notification.channel_description` — Android system
  Settings text, still accurate for a game that sends one kind of message.

Note that the whole `capture.*` and `meet.*` block is reachable today only by
dropping `capture.txt` or `meet.txt` beside the save (`GameBoot.OnEnable`); no
player reaches those screens until `50-photo/10-skip-default-cat` lands. The
copy was fixed anyway, since it will be the first thing a player reads when it
does.
