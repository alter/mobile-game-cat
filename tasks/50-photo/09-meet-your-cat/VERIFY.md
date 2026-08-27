Verifier: an agent context with no prior involvement in this task — did not
write `MeetYourCatScreen.cs`, `CatSaveFile.cs`, `Cat.cs`, `CatSave.cs`, the
`GameBoot.cs`/`Copy.cs` changes, or `CatSaveTests.cs` (that suite belongs to
`50-photo/10`). No Unity build, `adb`, or `simctl` was used or is available
here — the coordinator holds that toolchain. Checked by reading the code,
running the existing `dotnet test` suite, and an independent fuzz harness
built outside the repo.

## 1. OUTCOME — how much is true from the code alone

True, read directly:
- `MeetYourCatScreen.Build` renders a portrait from `CoatBuilder.LoadBase` +
  `CoatBuilder.Build` over the traits passed in, plus a `TextField` for the
  name (`game/Assets/View/MeetYourCatScreen.cs:83-121`).
- `Confirm()` builds `new Cat(_nameField.value, _traits)` and fires
  `OnNamed` (`MeetYourCatScreen.cs:135-143`); `Cat`'s constructor turns a
  blank/whitespace name into `Cat.DefaultName` (`Cat.cs:38-44`), so leaving
  the field empty still yields a named cat.
- `GameBoot.ShowMeetYourCat` wires `OnNamed` to
  `CatSaveFile.Write(CatSave.Write(cat))` (`GameBoot.cs:236-240`) — the name
  is written to disk the moment it is confirmed, not just held in memory.

**What only a run can settle** (both are the task's own `VERIFY (HUMAN)`
items, not gaps I introduced):
1. Whether the rendered cat actually reads as a "rough match" to a real
   photographed cat — requires the real capture screen, camera and Vision
   plugin, none of which run outside a device/simulator.
2. Whether the typed name is still shown after the screen is left and
   reopened, in the running app — requires launching the app and driving the
   UI.

## 2. Persistence path — CatSaveFile vs SaveFile, line by line

`diff` between the two files, with `CatSaveFile`→`SaveFile` and
`cat.save`→`board.save` normalised, shows **zero code differences** — every
line that differs is a doc comment. `Read()`, `Write()`, and `Clear()` are
byte-identical in structure: same `Application.persistentDataPath` directory,
same `File.Exists` guard, same broad `catch (Exception e)` → `Debug.LogWarning`
→ null/no-op, same temp-file write in `Write()`.

**One thing is not a difference between the two files, but is present in
both, and it bears directly on VERIFY item 2.** `SaveFile.cs`'s own doc
comment (lines 14-16) claims "the file is replaced atomically: a
fully-written temporary file is moved over the old one." `CatSaveFile.cs`
inherits the same claim ("same atomic write through a temp file",
`CatSaveFile.cs:10`). Neither implementation does a move. Both do:

```csharp
File.WriteAllText(TempPath, text);
File.Copy(TempPath, Path, overwrite: true);
File.Delete(TempPath);
```

`File.Copy` is a byte-stream copy into the real save file, not `File.Move` /
a rename. A kill during `WriteAllText` is harmless (the real file is
untouched, only the orphaned temp file is bad). A kill **during the
`File.Copy` call** overwrites `cat.save` (or `board.save`) in place and can
leave it truncated — the one scenario "atomic write" is supposed to rule
out, and exactly the window VERIFY item 2 (name survives a reopen) would
need to land in to fail. This is not something `50-photo/09` introduced —
`CatSaveFile.cs` mirrors `SaveFile.cs` faithfully, including this gap — but
it is a real, reproducible-in-principle risk to the persistence guarantee
both files' own comments claim, and I could not rule it out from a static
read; it would need a kill-during-write test on device, which is outside
this environment's toolchain.

## 3. Corrupt/absent cat file

`Core.CatSave.Read` returns `null` on anything malformed
(`CatSave.cs:52-102`, explicit catches for `FormatException`,
`IndexOutOfRangeException`, `ArgumentException`). The only call site is
`GameBoot.cs:216-218`:

```csharp
var saved = CatSave.Read(CatSaveFile.Read());
var traits = saved?.Traits ?? CatTraits.Default;
ShowMeetYourCat(uid.rootVisualElement, traits, saved?.Name);
```

This does **not** call `Cat.Skipped` by name, but its effect is the same:
`Cat.Skipped` is defined as `new Cat(DefaultName, CatTraits.Default)`
(`Cat.cs:52`), and here `traits` already falls back to `CatTraits.Default`
while `saved?.Name` is `null`, which `MeetYourCatScreen.Build`'s
`initialName ?? ""` turns into an empty field that `Confirm()` turns into
`Cat.DefaultName` on save (`Cat.cs:41-42`). Same end state, reached via the
traits/name pair rather than a `Cat` instance — worth noting as an
implementation detail, not a defect.

**Mutation-tested, outside the repo** (`Shell/CatSaveFile.cs` depends on
`UnityEngine` and cannot be compiled by `dotnet test`; `Core/CatSave.cs`
can). Copied `Cat.cs`, `CatSave.cs`, `CatTraits.cs` into a standalone
console project under the scratchpad and fuzzed:
- 20,000 in-memory trials: random byte buffers decoded as UTF-8, truncations
  of a real save at every length, and real saves with 1-9 random byte flips.
- 500 trials writing genuine random bytes to a real file on disk and reading
  it back through `File.Exists` + `File.ReadAllText`, then `CatSave.Read`.

Result: **0 exceptions across 20,500 trials** (19,690 of the 20,000
in-memory trials correctly returned `null`; the rest were mutations that
happened to still parse). Also ran the existing `CatSaveTests.cs` (a
different task's suite, not written by this verifier): **189/189 tests
pass**, `build/core-tests`, `dotnet test`, 317ms.

## 4. The seam — routing into the screen

Both paths are real, not stubs:
- Photo pipeline: `CaptureScreen.OnCatReady` is invoked at both the success
  path (`CaptureScreen.cs:265`) and the skip path (`CaptureScreen.cs:274`);
  `GameBoot.cs:152-157` wires it to `screen.Hide(); ShowMeetYourCat(...)`.
- Standalone flag: `MeetYourCatScreen.Requested` checks for `meet.txt`
  beside the save (`MeetYourCatScreen.cs:39-41`); `GameBoot.cs:212-221`
  checks it in `OnEnable`, reads any existing `cat.save` and calls the same
  `ShowMeetYourCat`.

Both converge on the one `ShowMeetYourCat`/`OnNamed` implementation
(`GameBoot.cs:231-241`) — there is no second, divergent code path for either
entry point. `meet.txt` is the only way to reach this screen without running
the actual photo/Vision pipeline, which this environment cannot run.

## 5. D8 — the name must never reach a shared image

`DECISIONS.md` D8 (`tasks/DECISIONS.md:292-313`): "The name she typed must
not appear on the shared image, or it becomes a public artifact next to the
app's branding." `MeetYourCatScreen.cs:17-23`'s doc comment quotes this rule
verbatim and adds: "whatever eventually builds the before/after share card
(D8) has to leave it out on purpose — the rule starts mattering here, not
there." That is actionable for whoever builds the share card later: it names
the source of truth (D8), states the constraint in the same terms, and
identifies this screen as where the name first exists — enough to know what
to omit and why, without prescribing the share card's own implementation
(out of that task's scope).

## How to reproduce

From a clean checkout:

```
cd build/core-tests && dotnet test
```

189/189 pass, including 8 in `CatSaveTests.cs` covering the corrupt-input
cases directly. The fuzz harness used for section 3 is not part of the repo
(built in the scratchpad per this session's instructions); its result is
reported above rather than left as a rerunnable artefact.

## What was not checked

- VERIFY item 1 (rendered cat resembles a real photographed cat) — not
  checked, cannot be: requires the real capture screen, camera, and Vision
  plugin on a device or simulator, none reachable here.
- VERIFY item 2 (name survives leaving and reopening the screen), as an
  actual running-app behaviour — not checked; only the static/fuzz-level
  persistence mechanism was checked (section 2/3 above). The kill-during-
  `File.Copy` window in section 2 was not exercised on a real device; it is
  a static-analysis finding, not a reproduced failure.
- The coat art itself (silhouette/pattern/markings) — explicitly out of
  this task's scope (`60-shell-build/18-coat-shader`, `40-art/03`,
  `40-art/04`); `CoatBuilder.LoadBase`/`Build` were only checked for the
  null-safety of `MeetYourCatScreen`'s own call, not for correctness of the
  rendered coat.
- iOS-specific file-system behaviour (whether `File.Copy` on APFS under
  IL2CPP is any more or less interruptible than on this dev machine) — not
  checked, no device available.
- Whether two `MeetYourCatScreen`/`GameBoot` components could be
  double-added under some re-entry sequence not exercised by `meet.txt` or
  the capture flow as written — not checked beyond reading the existing
  `GetComponent<...>() == null` guards.

## Verdict

`verify: pending` — not `passed`, not `failed`. Every part of this task
checkable without a device or the coordinator's screenshots came back clean:
routing is real, corruption handling never throws (20,500 fuzz trials, 189
existing tests), the D8 note is present and actionable, and the persistence
code is a faithful, bug-for-bug-identical mirror of the already-shipped
`SaveFile.cs` — including one shared, pre-existing gap between the "atomic
write" the comments claim and the copy-then-delete the code actually does,
flagged in section 2 for whoever next touches either file. Items 1 and 2 of
the task's own `VERIFY (HUMAN)` are exactly what the coordinator's
screenshots are for, and per `tasks/README.md`'s independence rule those
are not something an agent can perform or simulate on their behalf.
