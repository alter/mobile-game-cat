# Where the photo actually goes — 2026-09-02

The plugin comments said "nothing leaves the device" and were right about
themselves and wrong by extension. `Plugins/Android/.../CatVision.java` and
`Plugins/iOS/CatMarks.swift` never write or transmit anything — that part is
true and stays true. But the accepted photo does not stop at those files:
`Shell/CatPhoto.cs` crops it and `Core/TraitsRequest.cs` sends that crop to a
Worker that calls a model over the network. A comment that reads as a claim
about the whole pipeline, read by whoever writes the store listing, becomes a
promise to players that the code does not keep. This note is the correction,
read from the code rather than from memory of what the code was meant to do.

## Canon — quote this, don't re-derive it

> On the online path, the game crops the accepted photo **on-device** to a
> 512×512 JPEG under 200 KB (`Shell/CatPhoto.cs`, `Plugins/iOS/CatPhoto.swift`,
> `Plugins/Android/.../CatPhoto.java`), base64-encodes that crop, and POSTs it
> — together with a fixed `media_type` and an opaque `device_id` used only as
> a rate-limit key (`Core/TraitsRequest.cs`) — to a Cloudflare Worker the
> game's own owner operates (`worker/src/index.ts`). The Worker forwards the
> crop and a fixed prompt to Anthropic's Messages API for one inference call
> and returns to the game only validated coat/mark traits; the model's own
> text is discarded. What does **not** leave the device: the original photo
> at full resolution, and any EXIF or GPS metadata — the crop is re-drawn
> into a fresh bitmap and re-encoded on both platforms, which carries no
> metadata forward. What is **not stored** anywhere on this path: the Worker
> holds the crop in memory for the length of one request only — no KV, D1 or
> R2 binding exists in `worker/wrangler.jsonc`, no `console.log` of image
> bytes or request body appears in `worker/src/index.ts`, and no file is
> written. `wrangler.jsonc`'s `observability: enabled` turns on Cloudflare's
> own request-metadata logging (method, status, timing), not body capture,
> and nothing in the Worker's code logs the body; what Anthropic itself
> retains from the API call is outside this codebase and was not checked
> here. **When the Worker is unreachable, nothing is sent at all**:
> `TraitsOrigin.OfflineColourOnly` (`Core/CatTraits.cs`) is computed entirely
> on-device, from the same segmentation mask Vision/ML Kit already produced
> for the vision plugins — no network call happens on that path.

## Supporting detail, so the canon isn't taken on faith

**What crosses the wire, exactly.** `Core/TraitsRequest.cs:63-87` builds
`{"image_base64": ..., "media_type": "image/jpeg", "device_id": ...}` — three
fields, nothing else. No player name, no language, no location is in that
body. `worker/src/index.ts:122,129-152` reads exactly those three fields and
nothing more.

**`device_id` today.** It exists solely as the rate-limit key
(`index.ts:145-152`, keyed by device rather than IP because carriers share
addresses). `View/CaptureScreen.cs:73` currently defaults it to `""`, which
`TraitsRequest.BuildJson` turns into the literal string `"anonymous"` — no
code path yet fills it with a real per-device identifier
(`SystemInfo.deviceUniqueIdentifier` is read elsewhere, in
`Shell/CatIdentity.cs`, for something unrelated to this request).
`50-photo/08-capture-screen` is still `in_progress`; when it wires a real
device id in, this canon does not change, only the value of one field it
already describes.

**EXIF/GPS, checked on both platforms, not assumed.**
- iOS: `CatPhoto.swift` crops the decoded `CGImage`, draws it into a *new*
  `CGContext` (`resize`, line 70-79) and encodes that with
  `CGImageDestinationAddImage`/`Finalize` passing only a compression-quality
  dictionary (line 84-97) — no metadata dictionary is ever attached, so
  nothing from the source's EXIF (GPS included) reaches the output bytes.
- Android: `CatPhoto.java` reads only `ExifInterface.TAG_ORIENTATION` from the
  source (line 175-177, 375-384) to rotate the pixels the way the camera
  wrote them — it is the one EXIF tag ever read — and writes the result with
  `Bitmap.compress(Bitmap.CompressFormat.JPEG, ...)` (line 422), which has no
  metadata-writing capability at all. No GPS or other EXIF tag survives.

**What the Worker's own comment already said, now checked rather than
trusted.** `worker/src/index.ts:9-12`: "The photo lives in memory for the
length of the request and is not stored, logged or forwarded anywhere but to
the model." Read against the file: true. There is no storage binding in
`worker/wrangler.jsonc`, no `console.log`/log call anywhere in
`worker/src/index.ts`, and the only network call the Worker makes is the one
to `api.anthropic.com` (`index.ts:157`). The model's own error text is
deliberately *not* passed back to the game (`index.ts:182-187`) precisely
because "it can carry account details" — one more thing that does not leave
the Worker.

**Adjacent, out of this task's scope but worth flagging.** `tasks/DECISIONS.md`
D8 ("The share card exists, at P2") says "the photo never left the device,
only traits did" in the same over-broad shape this task exists to fix. This
task's SCOPE names only `Plugins` and `Shell`, so `DECISIONS.md` was left
untouched — but whoever next edits D8 should read it against this canon, not
against the sentence currently there.

## Comments corrected

- `Plugins/Android/.../CatVision.java` — the section header "Three rungs, and
  the photo never leaves" and the paragraph under it claimed the property for
  the whole photo. Reworded to "this layer never sends or stores anything"
  and the paragraph now says explicitly that what happens to the photo after
  this file is a different layer, pointing at `Shell/CatPhoto.cs`,
  `Core/TraitsRequest.cs`, `worker/src/index.ts` and this NOTES.md.
- `Plugins/iOS/CatMarks.swift` — "Nothing leaves the device and nothing is
  written down" reworded to "Nothing leaves this file and nothing is written
  down here", with the same pointer added.
- `grep -rn "never leaves\|not leave\|nothing leaves\|does not leave\|stays on
  the device" game/Assets` (Plugins + Shell): the only other hits are
  `TraitsRequest.cs:106` and its tests ("a bad request never leaves the
  device") — accurate as written, about client-side validation rejecting a
  malformed request before any network call, not a claim about what a *valid*
  request does. Left alone.

## Draft: store-page text

**English, short.**
> Cat Shelter looks at a cropped, resized copy of your cat's photo to read
> her coat colours and any one-of-a-kind mark — it never sees the original,
> full-size photo, and the copy isn't kept afterwards. No photo leaves your
> phone at all if you're offline: the game measures her colours on-device
> instead.

**Russian, short.**
> «Приют для кошек» смотрит на уменьшенную обрезанную копию снимка вашей
> кошки, чтобы распознать окрас и особую примету, — исходную фотографию в
> полном размере игра не видит, а копию после этого не хранит. Без сети
> снимок вообще никуда не уходит: окрас определяется прямо на телефоне.

## Draft: PrivacyInfo / privacy-policy data-type list

Starting point for `PrivacyInfo.xcprivacy` and the policy page — not a final
legal text, and not a replacement for `60-shell-build/17-permission-audit`'s
own table, which this draft should be reconciled against before shipping.

| Data type | Collected? | Purpose | Linked to identity | Used for tracking | Retention |
|---|---|---|---|---|---|
| Photos (a cropped, resized copy) | Yes, only when the online trait path runs | App functionality — describing coat/marks | No | No | Not stored; in memory for one request (`worker/src/index.ts`) |
| Device ID (opaque string) | Yes | App functionality — rate-limiting `/traits` only | No | No | Held by Cloudflare's rate-limiter binding, not by app code |
| Original photo, EXIF/GPS | No — never leaves the device | — | — | — | — |

**No ad-network SDK.** `grep -rli "admob\|facebook\|firebase\|advertising"` over
`game/Assets/**/*.xml` and `*.plist` returns nothing; the three Android plugin
manifests (`CatPicker`, `CatShare`, `CatVision`) declare no ad SDK.

**GameAnalytics is present and is a separate story, not this task's to
settle.** `Shell/GameAnalyticsSink.cs` integrates GameAnalytics for
design/progression events (`70-analytics/01`). Per `tasks/DECISIONS.md` D9,
ATT is deliberately never requested and
`GameAnalytics.EnableAdvertisingIdTracking(false)` is called before
`Initialize()` — no advertising identifier is used. GameAnalytics's own
package declares `NSPrivacyTracking = true` and a tracking domain (D9), which
`60-shell-build/17-permission-audit`'s NOTES.md already flagged for a
re-audit after `70-analytics/01` landed. That re-audit has not happened yet
as of this note and should not be duplicated here — it belongs in that task,
not folded into the photo-path canon above, which is about the `/traits`
request only.

## VERIFY

1. `grep -rn "never leaves\|does not leave\|nothing leaves\|stays on the
   device" game/Assets/Plugins game/Assets/Shell` — every remaining hit is
   scoped to what its own file does, none reads as a claim about the whole
   photo path.
2. `./.venv/bin/python -m pytest tools/tests -q` — green (see run below).
3. `bash build/headless-build.sh --tests-only` — green (see run below).
