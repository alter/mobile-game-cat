Verifier: independent QA context. Wrote none of `game/Assets/View/DebugGameView.cs`,
`game/Assets/Shell/Copy.cs`, `game/Assets/Core/Analytics.cs`, `game/Assets/Core/Board.cs`,
`tools/tests/test_analytics_call_sites.py`, `tasks/DECISIONS.md` D4, this
task's own `task.txt`/`NOTES.md`, or `tasks/80-live-validation/00-thresholds/NOTES.md`.
Read every file directly and ran every command itself. Did **not** run a
Unity build, PlayMode test, adb, or an emulator — another agent is running
the Unity build for this pass; out of scope here regardless. Its only
writes are to this file and `labels.txt`.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Does `task.txt` still describe the game that exists? | **No — stale, and would mislead a reader building it cold** | `task.txt` SCOPE still reads: "Button reads 'one more shelf'... Tapping shows a 'coming soon' stub. The level stays lost." VERIFY item 1 still reads "tapping the button shows the stub". OUTCOME still reads "a lose screen that offers a credible rescue, records intent to pay". None of this was touched after D4's revision (`tasks/DECISIONS.md`, "Revised 2026-08-27: the door is closed until there is a price behind it" — "The button and its two strings are gone from the lose screen"). Confirmed against the live build: `game/Assets/View/DebugGameView.cs:376-383` shows the lose card is built with `secondaryText: null` (the button branch), and a `grep -rniE "coming soon\|one more shelf" game/Assets/Shell game/Assets/View` finds the phrase only inside comments *explaining that it was removed* — never as UI text. A reader coming to this `task.txt` cold today, with no access to `DECISIONS.md`'s later revision, would build back exactly the button and stub D4 removed. |
| 2 | Does what D4 says must survive actually survive, and is dormancy enforced rather than assumed? | **Pass, with one real gap found in the enforcing test** | `AnalyticsEventNames.BoosterTap = "booster:tap"` (`Analytics.cs:13`) and `Analytics.BoosterTap()` (`Analytics.cs:96`) both still exist. `Board.AddShelfSlots` still exists (`Board.cs:179-188`). `tools/tests/test_analytics_call_sites.py`'s `DORMANT` dict (lines 43-48) lists `booster:tap` with the exact reason D4 gives, and `test_every_event_has_a_call_site` inverts its own assertion for any dormant event (asserts **no** call site, not a required one) while `test_dormant_events_are_still_declared` asserts the constant is still declared — so a dormant event vanishing from `Analytics.cs` entirely would also be caught. Live check: `grep -rn "Analytics\.BoosterTap\|AddShelfSlots" game/Assets/Shell game/Assets/View` finds exactly one hit, a comment (`DebugGameView.cs:375`) explaining why both stay unused — zero real call sites, matching D4 and this task's own `−` line ("`Shelf.AddSlots` must NEVER be called from this screen or anywhere in the MVP"). `.venv/bin/python -m pytest tools/tests/test_analytics_call_sites.py -q` → `14 passed`.<br><br>**Mutation-tested outside the repo, both directions:** (a) deleted the real `Core.Analytics.AppOpen()` call from a copy of `GameBoot.cs` → `test_every_event_has_a_call_site[app:open-AppOpen]` and `test_open_and_rejection_are_not_the_same_site` both failed correctly. (b) added a real, uncommented `Analytics.BoosterTap();` call to a copy of `DebugGameView.cs` without touching `DORMANT` → `test_every_event_has_a_call_site[booster:tap-BoosterTap]` failed with exactly the message D4 promises ("booster:tap is listed as dormant but something calls Analytics.BoosterTap — remove it from DORMANT"). Both directions work: a missing required call site fails, and the dormant event returning without updating the test also fails.<br><br>**Real gap found, not currently triggered:** `calls_of()` in this test searches raw file text, not comment-stripped text (unlike `test_copy_table.py`'s `strip_noise`). Wrapping the real `Core.Analytics.AppOpen();` call site in a `// TODO: call ...` comment (deleting the live call, keeping only the mention) still made `test_every_event_has_a_call_site[app:open-AppOpen]` **pass** — a call site that exists only in a comment is indistinguishable to this test from a real one. This did not affect today's verdict (the one comment near `BoosterTap` in the live tree doesn't happen to write the pattern `BoosterTap(` with an immediate parenthesis), but it is a real latent hole in the guarantee D4 describes, and it sits in the file the task's own SCOPE relies on for enforcement. |
| 3 | Does this task point at the metric-4 consequence, or would a reader believe it is still instrumented? | **Would mislead — no pointer exists** | `tasks/80-live-validation/00-thresholds/NOTES.md` ("Metric four lost its instrument, 2026-08-27") lays out the consequence in full: metric 4 has no instrument in the MVP, and three options (a/b/c) with costs are recorded for `01-spend` to choose from before proceeding. `07-lose-screen-fake-door/task.txt` and `NOTES.md` contain **no reference to this file, to metric 4's threshold discussion, or to the fact that D4 was revised at all** — `grep -rn "80-live-validation\|metric 4\|metric four" tasks/60-shell-build/07-lose-screen-fake-door/` returns nothing. `NOTES.md`'s own last section ("Actual verification (from build, not from imagination)") still frames the remaining work as "HUMAN gate 3.7 / metric 4 NOT executed — requires 5 outsiders," i.e. still written as if the instrument exists and only needs running, not as if it was removed. A reader who reads only this task's files, not `DECISIONS.md` or `80-live-validation`, would finish believing metric 4 is a working, if unrun, instrument. It is not. |
| 4 | Lose screen: are the named strings actually gone, and is nothing left dangling? | **Strings gone and correct; one stale doc comment found, no broken references** | `Copy.cs`'s `lose.*` keys today are exactly `lose.title` ("Shelf jammed"), `lose.body` ("Levels finished: {0}."), `lose.replay` ("Replay") — a count, stated as a fact, matching D4's "not a reproach" requirement. No `Copy.cs` key for a booster offer or a "coming soon" stub exists; the two retired strings are named only in an explanatory comment (`Copy.cs:85-93`) marking when and why they were removed. `DebugGameView.cs:369-383` calls `ShowCard` for the lose case with `secondaryText: null`, so no second button renders at all — confirmed structurally, not just by string absence. No dangling `Copy.Of(...)` call references a removed key (`test_every_key_the_code_asks_for_exists` in `tools/tests/test_copy_table.py` passes, `27 passed` overall). **One stale reference found**, not a broken one: `DebugGameView.cs:19-20`'s class-level doc comment still reads *"the lose screen offers 'one more shelf' but NEVER calls Shelf.AddSlots — the booster is a fake door in the MVP (D4)"* — present tense, describing the pre-revision behavior as current, six lines above a different, correct, dated comment at line 370 in the same file explaining the button was removed. Not a compile-breaking dangle, but the same class of drift as item 1, inside the code this time rather than the task tree. |

**Overall verdict: `verify:failed`.** The code is faithful to D4's revision — the button, its strings, and its `AddSlots`/`AddShelfSlots` prohibition are all exactly as D4 describes, dormancy is enforced by a real, mutation-tested guard (with one latent gap noted above). What fails is the task's own documentation: `task.txt`'s SCOPE, VERIFY, and OUTCOME describe a screen that was deliberately deleted eleven days before this check, and neither `task.txt` nor `NOTES.md` mentions that the decision underneath the task was reversed or that metric 4 now has no instrument — both of which a reader relying on this task alone would need to know and would not learn. `status:` moved `done → in_progress`: the artefact `task.txt` describes ("a lose screen that... records intent to pay") does not exist, per D4's own text ("metric four now has no instrument at all in the MVP"), and the fix (rewrite `task.txt`/`NOTES.md` to match the revision and point at `80-live-validation/00-thresholds`) is in-repo, not blocked on anything external.

## How to reproduce

From the current tree, no exported variables:

```sh
sed -n '1,40p' tasks/60-shell-build/07-lose-screen-fake-door/task.txt
# -> SCOPE/VERIFY still describe the button and the "coming soon" stub
grep -n "Revised 2026-08-27" -A 30 tasks/DECISIONS.md | head -35
# -> "The button and its two strings are gone from the lose screen"
sed -n '355,384p' game/Assets/View/DebugGameView.cs
# -> lose card built with secondaryText: null; no booster button
grep -n "lose\." game/Assets/Shell/Copy.cs
# -> only title/body/replay; no booster/coming-soon key
grep -rniE "coming soon|one more shelf" game/Assets/Shell game/Assets/View
# -> only inside comments, never as UI text
.venv/bin/python -m pytest tools/tests/test_analytics_call_sites.py -q
# -> 14 passed
.venv/bin/python -m pytest tools/tests/test_copy_table.py -q
# -> 27 passed
grep -rn "80-live-validation\|metric 4\|metric four" tasks/60-shell-build/07-lose-screen-fake-door/
# -> no output: no pointer from this task to the metric-4 consequence
```

Mutation test (analytics call-site guard), run outside the repository:

```sh
SP=$(mktemp -d)
mkdir -p "$SP/tools/tests" "$SP/game/Assets/Core" "$SP/game/Assets/View" "$SP/game/Assets/Shell"
cp tools/tests/test_analytics_call_sites.py "$SP/tools/tests/"
cp game/Assets/Core/Analytics.cs "$SP/game/Assets/Core/"
cp -R game/Assets/View/. "$SP/game/Assets/View/"
cp -R game/Assets/Shell/. "$SP/game/Assets/Shell/"

# (a) delete a real, required call site
python3 -c "p='$SP/game/Assets/Shell/GameBoot.cs'; t=open(p).read(); open(p,'w').write(t.replace('Core.Analytics.AppOpen();\n',''))"
.venv/bin/python -m pytest "$SP/tools/tests/test_analytics_call_sites.py" -q
# -> 2 failed (app:open has no call site)

# reset, then (b): revive the dormant event without touching DORMANT
cp game/Assets/Shell/GameBoot.cs "$SP/game/Assets/Shell/GameBoot.cs"
python3 -c "p='$SP/game/Assets/View/DebugGameView.cs'; t=open(p).read(); open(p,'w').write(t.replace('Analytics.LevelFail(_level.Number);','Analytics.LevelFail(_level.Number);\n                Analytics.BoosterTap();'))"
.venv/bin/python -m pytest "$SP/tools/tests/test_analytics_call_sites.py" -q
# -> 1 failed: "booster:tap is listed as dormant but something calls Analytics.BoosterTap"

# (c) the comment gap: wrap a real call in a comment instead of deleting it
SP2=$(mktemp -d); mkdir -p "$SP2/tools/tests" "$SP2/game/Assets/Core" "$SP2/game/Assets/View" "$SP2/game/Assets/Shell"
cp tools/tests/test_analytics_call_sites.py "$SP2/tools/tests/"; cp game/Assets/Core/Analytics.cs "$SP2/game/Assets/Core/"
cp -R game/Assets/View/. "$SP2/game/Assets/View/"; cp -R game/Assets/Shell/. "$SP2/game/Assets/Shell/"
python3 -c "p='$SP2/game/Assets/Shell/GameBoot.cs'; t=open(p).read(); open(p,'w').write(t.replace('Core.Analytics.AppOpen();','// TODO: call Core.Analytics.AppOpen();'))"
.venv/bin/python -m pytest "$SP2/tools/tests/test_analytics_call_sites.py::test_every_event_has_a_call_site" -k app -q
# -> 1 passed — a commented-out call site is indistinguishable from a real one to this test
```

`git status --short` in the real repository confirmed untouched throughout
(`game/Assets/Shell/GameBoot.cs`, `game/Assets/View/DebugGameView.cs`
unmodified) — all mutation was performed on copies outside the repository.

## What was not checked

- No Unity build, PlayMode test, adb, or emulator — another agent is
  running the Unity build for this same pass; VERIFY item 1 in `task.txt`
  ("PlayMode: reaching ShelfJammed shows the screen...") was evaluated by
  reading the source, not by running the scene.
- The `labels.txt` `verify:` line carries free text after a dash
  ("`verify:pending — HUMAN gate 3.7 (metric 4) required...`"), which does
  not match the plain `key:value` format `tasks/README.md` describes for
  labels. Not fixed here — flagged, since a strict parser reading labels
  would choke on it; only the `verify:`/`status:` values were changed by
  this pass, not the formatting of other lines.
- Whether `DORMANT`'s comment-blindness (item 2's gap) affects any other
  event beyond `booster:tap` was not swept — only the one instance relevant
  to this task was tested.
- Whether `tasks/80-live-validation/00-thresholds`'s three options (a/b/c)
  have themselves been decided — this task only checks whether `07`
  *points at* that discussion, not whether it has been resolved.
- The content of `tasks/DECISIONS.md` D4 itself was read and trusted as the
  record of what was decided; it was not independently re-derived (e.g. no
  attempt to re-run the booster-vs-win-rate simulation).

## Re-verification, 2026-08-27 — the failure above is fixed

**Verifier: independent QA context, a fresh pass, not a continuation of the
one that wrote the `verify:failed` verdict above.** Wrote none of
`task.txt`, `NOTES.md`, `tools/tests/test_analytics_call_sites.py`, or the
code files named below. Did not run a Unity build, PlayMode test, adb, or
an emulator. Read `task.txt`/`NOTES.md` as rewritten, re-ran every command,
and mutation-tested the specific comment-stripping fix the earlier pass
found missing, on a copy outside the repository. Only writes: this file and
`labels.txt`.

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Read `task.txt` cold — would you build the screen that exists? | **Yes** | GOAL states the current shape plainly ("a count of levels finished, and a way to try again — nothing offered that will not be granted") before narrating the history; SCOPE's `+`/`−` lines and VERIFY 1 match: a card with "Levels finished: {0}." and a Replay button, no second button, no stub. Confirmed against code unchanged since the failed pass: `DebugGameView.cs` still calls `ShowCard(..., secondaryText: null, ...)` for the lose case. A cold reader builds exactly this. |
| 2 | Is keeping the 2026-08-25 text verbatim the right call? | **Right call — ordering and markup make it hard to mistake for current instruction** | The current, authoritative GOAL/SCOPE/OUTCOME/VERIFY/ROLE/DEPENDS come first, in the project's normal task-file order; the historical text sits after a `---` rule, under "As originally written, 2026-08-25 — kept for the record, not current," and is blockquoted (`>`) throughout — a reader who stops at DEPENDS (where every other task file ends) never reaches it, and one who does gets three separate signals it is not current. Matches `DECISIONS.md`'s own convention of keeping superseded numbers rather than editing them away, per this task's own `NOTES.md`. Residual risk: a keyword grep landing inside the blockquote without reading the header could mislead — true of any document, not specific to this one. |
| 3 | Do the three code facts hold, and does the comment-stripping fix work? | **All three hold; the fix is real, confirmed by mutation both before and after** | `Analytics.cs:13,96` — `BoosterTap` constant and method both present. `Board.cs:186` — `AddShelfSlots` present. `grep -rn "Analytics\.BoosterTap\|AddShelfSlots" game/Assets/Shell game/Assets/View` → one hit, a comment. `pytest tools/tests/test_analytics_call_sites.py -q` → `14 passed`. Read the current file: `strip_comments()` now exists and `calls_of()` runs text through it before searching — exactly the fix the earlier pass's gap called for. Mutation-tested on a scratch copy (not the repo): wrapped the real `Core.Analytics.AppOpen();` call in `GameBoot.cs` as `// TODO: call Core.Analytics.AppOpen();` — `calls_of("AppOpen")` now returns `[]`, and `test_every_event_has_a_call_site[app:open-AppOpen]` plus `test_open_and_rejection_are_not_the_same_site` both fail. This is the exact opposite of what the earlier pass found (that mutation used to pass silently). `git status --short` confirms the real repo untouched. |
| 4 | Does a reader finish knowing metric 4 is uninstrumented, not just unmeasured? | **Yes, and the pointer resolves to real content** | `task.txt` CONTEXT names `tasks/80-live-validation/00-thresholds/NOTES.md` ("Metric four lost its instrument, 2026-08-27") and OUTCOME states "Metric 4 has no instrument in this build" — "instrument," matching D4's own word, not "unmeasured." Confirmed the target section exists verbatim: `tasks/80-live-validation/00-thresholds/NOTES.md:54`, "## Metric four lost its instrument, 2026-08-27," with options (a)/(b)/(c) present below it. |

**Overall: `verify:passed`.** `status:` moved `in_progress → done` — the
artefact `task.txt` now describes (a truthful lose screen, dormancy
enforced, the metric-4 gap tracked elsewhere) matches what is on disk, and
the guard gap the earlier pass found and did not block progress on is
independently confirmed fixed here.

### How to reproduce (this pass)

```sh
.venv/bin/python -m pytest tools/tests/test_analytics_call_sites.py -q   # -> 14 passed
grep -n "strip_comments" tools/tests/test_analytics_call_sites.py         # -> present, used by calls_of()
grep -n "Metric four lost its instrument" tasks/80-live-validation/00-thresholds/NOTES.md

# Mutation, outside the repo:
SP=$(mktemp -d)
mkdir -p "$SP/tools/tests" "$SP/game/Assets/Core" "$SP/game/Assets/View" "$SP/game/Assets/Shell"
cp tools/tests/test_analytics_call_sites.py "$SP/tools/tests/"
cp game/Assets/Core/Analytics.cs "$SP/game/Assets/Core/"
cp -R game/Assets/View/. "$SP/game/Assets/View/"
cp -R game/Assets/Shell/. "$SP/game/Assets/Shell/"
python3 -c "p='$SP/game/Assets/Shell/GameBoot.cs'; t=open(p).read(); open(p,'w').write(t.replace('Core.Analytics.AppOpen();','// TODO: call Core.Analytics.AppOpen();'))"
.venv/bin/python -m pytest "$SP/tools/tests/test_analytics_call_sites.py" -q
# -> 2 failed (app:open reads as having no call site — the comment no longer counts)
git status --short   # repo untouched
```

### What was not checked (this pass)

- No Unity build, PlayMode test, adb, or emulator — VERIFY 1 was confirmed
  by reading `DebugGameView.cs`, as the earlier pass also did.
- Did not sweep whether `strip_comments()`'s regex has its own edge cases
  (e.g. a `//` inside a string literal) — none of the files it runs against
  today contain one in a position that would matter.
- Did not re-check the `labels.txt` free-text formatting issue the earlier
  pass flagged; out of scope for this pass's four questions.
