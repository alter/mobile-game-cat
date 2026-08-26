# Pacing is not difficulty - three knobs, kept apart

Piles per room governs *pacing* - how often a large reward (room completion:
light changes, cat moves, a possession may be handed over) lands. Pile size
and complications govern *difficulty*. Grow all three together and they
multiply: a final room of six piles, sixty items each, under three
complications. Keeping them separate is the whole point of D2.

## The chosen curve

1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 across twelve rooms - 37 levels, an
estimated 44-74 minutes by tap count (a floor: hidden kinds and complications
both slow play further). The first room finishes in a single level on
purpose: she sees the whole loop immediately - cleared, room brightens, kitten
better. For an audience that must not be intimidated at the door, that is the
strongest possible opening.

## Why linear growth to twelve piles was rejected

Growing piles-per-room linearly to 12 yields 78 levels and 94-156 minutes,
which is tempting on paper - more content - and is a trap: the twelfth room
would need twelve levels between large rewards. At 10-20 minute sessions
(the stated audience pattern) that is two or three whole sittings in which
nothing completes, arriving exactly where the player is most likely to stop.
**Content that stops paying out does not retain, it tires.**

## A consequence to carry into 60-shell-build, not this task

With a variable curve, "state changes at level 5" stops meaning anything. Cat
states now change after the **4th and 8th completed room** (not level number),
per the arc in cat-shelter-mvp.md section 4. Anchoring to rooms keeps the arc
intact however this curve is later re-tuned.

Source: cat-shelter-tasks.md lines 515-541; DECISIONS.md D2.

## status:todo → done, 2026-08-26

`tools/solver/pacing.py` already held the curve and six tests already covered
the *tuple*. What VERIFY 1 asks for was missing: it says "for every room in the
**shipped set**", and nothing here looked at the 37 files the game loads.
Reading a constant and asserting things about it proves the constant, not the
content — the same gap 05-ship-37-levels found in itself.

Seven tests added to `tools/tests/test_pacing.py` (6 → 13), the shipped ones
parsing `l<seq>_room<room>_pile<index>.json` straight off
`game/Assets/Resources/Levels/`:

- `test_no_shipped_room_holds_more_than_four_piles` — the cap, per room, on
  disk (VERIFY 1).
- `test_generated_plan_never_exceeds_the_cap` — the same cap on every room of
  `level_map()`, which is what `ship_levels.py` walks to generate (VERIFY 1,
  generated half).
- `test_shipped_piles_per_room_match_the_curve` — per-room counts equal
  `PILES_PER_ROOM` and sum to 37 (VERIFY 2, VERIFY 3).
- `test_shipped_set_is_the_thirty_seven_levels`,
  `test_shipped_pile_indices_are_contiguous_from_zero`,
  `test_shipped_play_order_matches_the_level_map` — 37 files, sequence 1..37,
  pile indices 0..n-1 per room, play order identical to the plan.
- `test_shipped_files_agree_with_their_own_filenames` — `number`, `room_id`,
  `pile_index` inside each JSON match its name. A filename can claim any room;
  the definition inside is what the game reads.

`.venv/bin/python -m pytest tools/tests -q` → 90 passed (was 83).
`dotnet test build/core-tests/core-tests.csproj -v q --nologo` → 60 passed,
untouched by this change.

**The new tests were mutation-checked, not just run green.** On a copy of the
level directory: adding `l38_room12_pile4.json` is caught by the cap test
("room 12 ships 5 piles"), by the curve test and by the count test; rewriting
`l37`'s `room_id` to `room_11` is caught by the contents-vs-filename test. A
test that has never failed has not been shown to test anything.

### What is still not enforced

The curve is written out four times: `pacing.py` (the source of truth),
`game/Assets/View/LevelAssets.cs:21`, `Tests/Core/GameSaveTests.cs:36`,
`Tests/Core/PlayerProgressTests.cs:15`. All four read
`1,2,3,3,3,3,3,3,4,4,4,4` today — checked by grep — but nothing compares them.
`build/core-tests` compiles only `Assets/Core` and `Assets/Tests/Core`, so the
`LevelAssets` copy, the one that actually builds the filenames the player
loads, is under no test at all. If it drifted, these Python tests would still
pass and the game would ask for names that do not exist. Out of scope here;
it belongs to whoever owns level loading (06-json-level-loading).

`verify` stays `pending`: the tests above are mine, and a context does not sign
off its own tests (`tasks/README.md`, the independence rule).
