# Android: go — 2026-08-27

**Decided by the owner, before gate 3, knowingly.**

## What this decision is NOT based on

The task asks for gate-3 numbers by value. **There are none.** Gate 3 has not
run: no build has reached a store, no money has been spent on installs, and
none of the four metrics has a measurement. The same is true of gates 1 and 2.

That is written here rather than papered over, because the VERIFY list for this
task cannot be satisfied and should not be marked as if it were. Anyone reading
this later should know the decision was made on judgement, not on evidence.

## What it is based on

The owner's reasoning, recorded as given: the iOS side is blocked on things
only he can unblock — an Apple developer account, a spend cap, art, and five
outsiders — and Android work is available in the meantime.

That is a real argument about *idle capacity*, not about *value*. It is worth
separating: doing Android now costs nothing that iOS was going to use, and it
does not make the game more likely to succeed either.

## The cost, summed from this phase's own tasks

| task | who | estimate |
|---|---|---|
| 01 ML Kit probe | agent | half a day, and it can end the phase |
| 02 build pipeline | agent | done in an hour, see below |
| 03 emulator run | agent | half a day, mostly downloads |
| 04 picker plugin | agent | a day |
| 05 recognition plugin | agent | a day, or much more if 01 goes badly |
| 06 crop and downscale | agent | half a day |
| 07 outcome parity | agent | half a day |
| 08 haptics | agent | half a day |
| 09 notifications | agent | half a day |
| 10 permission audit | agent | half a day |
| 11 save parity | agent | half a day |
| 12 Play Console | **owner** | one-off fee, days of identity verification |
| 13 internal testing | owner + agent | half a day after 12 clears |
| 14 device matrix | **owner** | needs real phones |
| 15 analytics parity | agent | half a day, blocked on the GameAnalytics account |

Agent work: roughly six to seven days if ML Kit cooperates. Owner work: the fee,
the verification wait, and access to real Android phones.

## What would have to be true for this to be wrong

- **Gate 1 or gate 2 comes back "no".** Then Android is work on a game that is
  not being built, and this phase is the most expensive way to have learned it.
- **ML Kit cannot name a cat** (`01`). Then stage one has no Android
  implementation, and the alternatives cost either 15 MB of bundled model or a
  different price per player.
- **The owner's blocked items unblock.** If the Apple account, the art or the
  five outsiders arrive, iOS work resumes and is worth more per hour than
  Android work, because iOS is where the gates are being run.

## Status of the phase

`go`, with 01 as the real technical gate: if ML Kit cannot do stage one, stop
and bring the finding back rather than reaching for a bundled model.
