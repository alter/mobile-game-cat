# App Privacy declaration answers, drafted 2026-08-27

Not the TestFlight upload itself — that work hasn't started. This is the
answer key for the "photo collection" line in SCOPE, written down now
because it was derived while correcting a factual error elsewhere
(`cat-shelter-tech.md` §3, full sourcing in
`tasks/00-validate-demand/01-market-scan/legal-risk.md` §3) and should live
where the App Privacy form actually gets filled in, not only in a report.

The cropped cat photo is shared with a third-party processor (Anthropic) for
trait extraction and retained there for up to 30 days by default, not used
for training by default, not "not collected." Source: see the URLs and
retrieval dates in `legal-risk.md` §3 and in `cat-shelter-tech.md` §3.

## The three answers

- **Data collected: yes.** Photos — the cropped cat image is transmitted to
  Anthropic for trait extraction. This is "collection" under Apple's App
  Privacy rules regardless of how long the vendor retains it or that the app
  itself never keeps a copy.
- **Data linked to the user: no, today** — but conditionally, not
  permanently. `game/Assets/View/CaptureScreen.cs`'s `DeviceId` field is
  currently `""` (unset); its own comment says "No source for a real one
  exists in the shipping app yet," and `TraitsRequest` falls back to the
  Worker's own "anonymous" default. With nothing identifying the device or
  player attached to the request, there is nothing to call "linked" yet.
  **This is a decision nobody has made, not a settled fact:** the Worker's
  rate limit is keyed by device id (`worker/src/index.ts`,
  `env.TRAITS_LIMITER.limit({ key: deviceId })`), so the moment a real
  device id is wired up to make that limiter meaningful, the photo request
  carries an identifier and this answer flips to "yes, linked via device
  ID." Re-check this line specifically when `08-capture-screen`'s HTTP
  client is finished and `DeviceId` stops being empty.
- **Data used for tracking: no.** The call to Anthropic is a one-shot
  functional processing request (trait extraction), not shared for
  advertising, cross-app measurement, or profiling — consistent with D9 (no
  advertising ID, no tracking prompt on either platform). Nothing about the
  retention correction changes this.

## What this does not cover

- Whether the finished App Privacy form is actually filled in — this task
  (`14-testflight`) hasn't started (`labels.txt` still says `status:todo`
  as of this writing; not changed by this note).
- The equivalent Play Data Safety form on Android — same three answers
  apply by the same reasoning, but that's `90-android/10-permission-audit`'s
  deliverable, not this one's; its `task.txt` SCOPE now points back to
  `legal-risk.md` §3 for the same sourcing.
