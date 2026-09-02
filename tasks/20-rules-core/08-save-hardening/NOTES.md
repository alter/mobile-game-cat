# 08-save-hardening — notes

1. Shell/SaveFile.cs, Shell/CatSaveFile.cs: `Write` now uses
   `File.Replace` (destination exists) or `File.Move` (first write) onto
   the target instead of `WriteAllText(temp) + Copy + Delete`. Copy
   truncated the target in place; a kill mid-copy left the half-written
   file the docstring already promised protection from. Docstrings were
   already phrased as "moved" and needed no correction, only the code
   catching up to them.

2. Core/GameSave.cs: capacity moved off the "shelf" line onto its own
   `cap N` line, written ahead of "shelf". Reading checks for that line
   first; only when it is absent (old-format saves) does it fall back to
   the old scan for a trailing `capN` token on the shelf line itself, so
   old files on disk keep resuming. No header/version bump — same
   `catshelter-save-v1`.

3. Core/GameSave.Read: `roomsdone` entries now rejected (whole file →
   null) when any value is `< 1` (same rule as `cursorRoom`) or the list
   has a duplicate. Core/PlayerProgress.Restore additionally rejects a
   room `> pilesPerRoom.Count` with the same `ArgumentOutOfRangeException`
   pattern as its existing cursorRoom/cursorPile checks — that is the one
   bound GameSave.Read cannot know on its own. HouseMapView already
   catches `ArgumentOutOfRangeException` from Restore and falls back to a
   fresh progress, so no View change was needed.

Tests added (Tests/Core/GameSaveTests.cs): round trip on real prop_*
kind ids (Resources/Levels/l01_room01_pile0.json), a kind named
"capybara" round-tripping through the new cap-line format, a hand-built
old-format string (embedded `capN` token, no `cap` line) still reading,
`roomsdone -4 99 99 7` → null, honest `roomsdone 1 2` → intact.
Tests/Core/PlayerProgressTests.cs: `Restore` rejects a roomsDone entry
outside the room count.

`dotnet test build/core-tests/core-tests.csproj`:
`Пройден!   : не пройдено     0, пройдено   276, пропущено     0, всего   276`
(270 → 276, six new tests, all green).

Not done: no change to Shell/CatIdentity.cs, Shell/GameBoot.cs, View/*
(another agent's files, per instruction). Version header not bumped
(SCOPE minus).
