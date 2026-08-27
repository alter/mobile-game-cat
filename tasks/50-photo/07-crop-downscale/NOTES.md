# Built and measured, 2026-08-26

`Assets/Plugins/iOS/CatPhoto.swift` plus the C# wrapper `Assets/Shell/CatPhoto.cs`.

## The two numbers everything follows from

Image cost is `ceil(w/28)·ceil(h/28)` visual tokens, so 512×512 is 361 tokens;
accuracy falls off below roughly 200 px on a side
(`knowledge/vision-model/01-traits-strict-json.md`). Hence 512 as the output
side, 200 px as the floor a crop may not go under, and 200 KB before base64,
which inflates by about a third.

## Against the VERIFY list, on real data

Run over every image the Vision stage accepted — 27 of the 41, cats plus the
blurry, multi and screen-shot cats it also recognised:

| check | result |
|---|---|
| every output exactly 512×512 | **27/27** |
| every output under 200 KB pre-base64 | **27/27** — 37 KB min, 85 KB median, 138 KB max |
| the under-200 px guard triggers and widens | **12 of 27** |

The guard is not a corner case. Twelve of the twenty-seven boxes Vision
returned were under 200 px on a side — as small as 75 px (`multi_02`) and 90 px
(`multi_01`) — because a cat sitting in a corner of a large photo is a small
box. Upscaling those to 512 would invent detail the model then reads as real,
so the crop widens around its own centre instead and the cat simply sits
smaller in frame. `crop-check.jpg` shows the results.

## Two decisions worth stating

**Orientation is not touched here.** The image arriving has already been
through Vision, which was given the orientation explicitly; re-applying it
would turn the crop on its side. Orientation belongs to `05-vision-plugin` and
stays there.

**Quality is stepped, not fixed.** A busy photo at 0.9 can exceed 200 KB where
a plain one never will, so the encoder walks 0.9 → 0.4 and stops at the first
size that fits. Nothing in this set needed to go below 0.9 — the 138 KB
maximum is a first-try encode — but a photo of a patterned rug would.

## How this was checked without a device

The same Swift source compiles for macOS, so the probe under
`scratchpad/vision/cropprobe.swift` links `CatPhoto.swift` directly and runs it
over the reference set with the real boxes measured in `05-vision-plugin`. That
is the same code path the plugin exports, minus the `@_cdecl` bridge — which
`05` already exercised 41 times.

Not verified on hardware: `14-testflight`.

---

## status:done → in_progress, 2026-08-27

The OUTCOME artefact this task names is not there. What is missing, what does
exist, and why it matters: `tasks/AUDIT-2026-08-27.md`.

---

## The base64 gap closed, 2026-08-27

`tasks/AUDIT-2026-08-27.md` item 2 found the crop and the 200 KB ceiling real
(`Shell/CatPhoto.cs`, `Side = 512`, `MaxBytes = 200*1024`) but no code anywhere
turning the result into base64, while `worker/src/index.ts` requires a field
named `image_base64`.

**What now exists.** `game/Assets/Core/TraitsRequest.cs` — engine-free
(`build/check-core-purity.sh` passes), so it is testable by
`dotnet test build/core-tests/core-tests.csproj` without Unity or a device,
unlike `CatPhoto.cs` which needs the iOS plugin. `TraitsRequest.BuildJson(byte[]
jpegBytes, string deviceId)` returns the exact JSON body `POST /traits`
expects: `image_base64` (`Convert.ToBase64String`), `media_type`
(`"image/jpeg"`, the only type `CatPhoto` ever produces), `device_id`
(defaults to `"anonymous"` the same way `worker/src/index.ts:97-99` does when
none is given). It rejects null/empty input and anything over the 200 KB
pre-encode ceiling with `ArgumentException`, so a bad request never leaves the
device — matched against `worker/test/traits.test.ts`'s own 400/413 cases
rather than invented. No JSON library: Core stays dependency-free the same way
`GameSave.cs` already explains (`System.Text.Json` is IL2CPP-forbidden,
Newtonsoft would put a dependency inside Core), and three fields does not
need one.

`View/CaptureScreen.cs` is the seam that uses it: the moment `Crop` succeeds,
`Handle()` now builds `LastTraitsRequestJson` from the prepared bytes plus a
new `DeviceId` field (empty by default — nothing in the shipping app has a
real device id yet; wiring one up is part of 08's HTTP client, not this
task). That is genuinely "ready to POST to /traits" sitting in production
code, not just in a test. `Shell/CatPhoto.cs` was left untouched — it has no
reason to know about the request envelope.

Tests added in `game/Assets/Tests/Core/TraitsRequestTests.cs` (12 cases): a
round trip (bytes → base64 → decoded bytes, equal), the empty/null/oversized
rejections, the anonymous-device-id default, and — following the idiom of
`CatTraitsTests.TheAllowedValuesMatchTheWorkerSchema` — three tests that read
`worker/src/index.ts` itself and check `TraitsRequest`'s field names, media
type and the 400 KB post-encode constant against it, so the two drifting apart
would fail here rather than only in the Worker's own suite.

`dotnet test build/core-tests/core-tests.csproj -v q --nologo`:

```
Пройден!   : не пройдено     0, пройдено   136, пропущено     0, всего   136, длительность 264 ms. - core-tests.dll (net8.0)
```

(124 before this change, 136 after — the 12 new tests, 0 skipped, confirming
the cross-language check actually reached `worker/src/index.ts` rather than
silently `Assert.Ignore`-ing.)

`build/check-core-purity.sh`: `Core is engine-free: OK`.

**What OUTCOME still does not cover, on purpose.** The HTTP call itself —
actually POSTing `LastTraitsRequestJson` to the Worker — is
`50-photo/08-capture-screen`, still `in_progress`, and per `tasks/DECISIONS.md`
D17 there is no spend cap and no account to call anything with regardless.
`CaptureScreen.AskWorker` is still an unassigned delegate; nothing calls the
network. A real per-device id is likewise 08's concern. This task's OUTCOME —
crop, ceiling, base64 encoding, a body shaped exactly like what `/traits`
expects — is now met by code that exists and is tested.

Label: `status:in_progress` → `review`. `verify:` left untouched — a
different context verifies.

### Review of the above, same day

Two things were changed after the payload landed, both of the same kind — a
check that could stop running without anyone noticing.

**`Assert.Ignore` removed from every cross-language check.** The three new
tests that read `worker/src/index.ts` skipped themselves when the path did not
resolve, and so did the older `CatTraitsTests.TheAllowedValuesMatchTheWorkerSchema`
they were modelled on. That is the same shape as the coverage gate nobody ever
invoked: green on a check that was never performed. They now fail with a
message naming the path, because a missing `worker/` is a finding. The new
tests inherited the weakness honestly — the idiom they copied had it first.

**Control characters are escaped.** `JsonString` handled `"`, `\`, newline,
carriage return and tab, and passed everything else through, so a device id
carrying any other control character produced a body that is not JSON. The
class's stated promise is that a bad request never leaves the device, and that
was the hole in it. Coverage showed the escaping branches untested (11 of 16
lines); there is now a test that feeds a hostile id through and asserts no raw
control character survives anywhere in the body.

Core coverage 93.8% → 94.4% as a side effect. 137 C# tests green.
