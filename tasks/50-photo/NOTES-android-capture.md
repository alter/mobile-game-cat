# Android capture: picking, preparing, colour

Task 50-photo on Android. The three parts of the photo pipeline that are not
machine learning, brought to the platform where none of them existed:
`CatPicker`, `CatPhoto`, `CatColour`. `CatVision` and `CatMarks` are somebody
else's, and the last section of these notes says what that costs today.

New: `game/Assets/Plugins/Android/CatPicker.androidlib/`, shaped after
`CatShare.androidlib` next door — own `build.gradle`, own manifest, own
`FileProvider`, `src/main/java` layout.
Changed: `Shell/CatPicker.cs`, `Shell/CatPhoto.cs`, `Shell/CatColour.cs`.
`View/CaptureScreen.cs` needed no change at all, which was the point.

Everything below with a number in it came out of a command run against a
running emulator: `sdk_gphone64_arm64`, `arm64-v8a`, Android 15, API 35
(`getprop ro.build.version.sdk` → `35`).

---

## 1. Picking: `MediaStore.ACTION_PICK_IMAGES`, and nothing else

The app's floor moved to 33 mid-task, which is the same API level the system
photo picker arrived at. That turned a decision into a non-decision.

**What was measured.** `cmd package resolve-activity -a
android.provider.action.PICK_IMAGES -t image/*` answers
`com.android.providers.media.photopicker.PhotoPickerActivity` in
`com.google.android.providers.media.module`. On the device it logs
`CatPicker: gallery: system photo picker (ACTION_PICK_IMAGES), sdk=35`.

**Why the others lose.**

| candidate | why not |
|---|---|
| `ACTION_PICK` on MediaStore | Reads the whole media library, and needs `READ_MEDIA_IMAGES` (`READ_EXTERNAL_STORAGE` below 33) to do anything with the result. One broad permission prompt to fetch one photograph. |
| `ACTION_GET_CONTENT` | Costs no permission either, but it is a document browser: every provider on the device, cloud drives included, and a document URI that need not be an image. Right as a fallback, wrong as a default — and at a floor of 33 there is no fallback left to be right about. |
| `ACTION_PICK_IMAGES` | Photographs only, out of process, and "the caller gets read access to user picked items even without storage permissions" (developer.android.com/reference/android/provider/MediaStore#ACTION_PICK_IMAGES). |

**A citation correction worth recording.** The sentence "apps that use the
photo picker don't need to declare any permissions" is *not* on
developer.android.com/training/data-storage/shared/photopicker. It was in an
early draft of this plug-in's manifest as a quotation and has been replaced
with the `MediaStore` reference sentence above, which is real. Two other
attributions were checked at the same time: the `ACTION_IMAGE_CAPTURE`
sentence about `CAMERA` is genuine and is quoted with its own typo ("if you
app"); and the claim that Android 13 reroutes an image-typed
`ACTION_GET_CONTENT` to the photo picker is **not documented anywhere on
developer.android.com and is not tied to an API level** — it is a MediaProvider
mainline rollout behind a `device_config` flag, so a device can have the
platform version and not the behaviour. Nothing here relies on it.

**Deleted because the floor moved to 33**, and worth naming because absent code
is the cheapest kind:

- `photoPickerAvailable()` — the `SDK_INT >= 33 || SdkExtensions.getExtensionVersion(R) >= 2`
  test that AndroidX's `PickVisualMedia.isPhotoPickerAvailable` performs. At a
  floor of 33 the first half is always true. Keeping it would have cost a
  branch that can never be false on any supported device.
- The `Api30` holder class that existed only so ART never had to resolve
  `android.os.ext.SdkExtensions` on a device below API 30. No such device now.
- The whole `ACTION_GET_CONTENT` path, its `<queries>` entry, and its
  `CATEGORY_OPENABLE`/`setType` setup. This is the expensive one to have kept:
  it is a second result shape (a document URI from an arbitrary provider,
  possibly not an image, possibly cloud-backed and slow) that no device we
  support would ever have taken, so no emulator run would ever have covered it.
- The Google Play services backport branch, which was never written. It answers
  a different action entirely (`androidx.activity.result.contract.action.PICK_IMAGES`)
  and needs a `ModuleDependencies` `<service>` in the manifest before the module
  is installed at all. Below 33 it was the only permission-free picker for
  Android 7.1–10; at a floor of 33 it is dead weight.

`resolveActivity` survived, but as error handling rather than as a branch: an
Android image with the API level and no picker activity is declined cleanly
(`OnPickUnavailable`), not fallen back from. It needs the `<queries>` block to
see anything at all — package visibility, not a permission.

**Nothing here needs a floor above 33.** The highest thing in the plug-in is
`BitmapRegionDecoder.newInstance(byte[], int, int)` at 31, then
`ACTION_PICK_IMAGES` at 33. `android.media.ExifInterface(InputStream)` is 24.

## 2. Permissions: none

The permission audit answer, from `aapt2 dump permissions` on the built APK:

```
uses-permission: name='android.permission.INTERNET'
uses-permission: name='android.permission.ACCESS_NETWORK_STATE'
uses-permission: name='android.permission.POST_NOTIFICATIONS'
uses-permission: name='com.sootpaw.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION'
```

All four predate this work. **No storage permission, no media permission, no
camera permission** — not declared, therefore never prompted for. Three reasons,
in order of how easy each is to lose:

1. The photo picker grants the URI it returns. Nothing here ever reads a
   MediaStore row, so `READ_MEDIA_IMAGES` has nothing to be needed for.
2. `ACTION_IMAGE_CAPTURE` delegates to the camera app, which holds the camera
   permission itself. Declaring `android.permission.CAMERA` here would make
   it *required*: "Note: if you app targets M and above and declares as using
   the CAMERA permission which is not granted, then attempting to use this
   action will result in a SecurityException."
   (developer.android.com/reference/android/provider/MediaStore#ACTION_IMAGE_CAPTURE)
   Declaring it would add a prompt and buy nothing.
3. Everything written goes to `getCacheDir()/catpick`, which is internal
   storage and costs no permission at any API level.

`<queries>` is in the manifest and is not a permission — it is Android 11
package visibility, and without it `resolveActivity` returns null and the
camera button disappears on every device.

## 3. Where the photograph lives, and for how long

`getCacheDir()/catpick`, and nowhere else. Never MediaStore, never external
storage, never `Application.temporaryCachePath` (which moves depending on the
project's write-permission setting and could land outside what the
`FileProvider` may serve).

The file exists because both halves need one: `UnitySendMessage` carries a
string, so `OnPicked` carries a path exactly as it does on iOS; and
`EXTRA_OUTPUT` demands a `content://` URI the camera app can write to, which
means a `FileProvider`, which means a real file. Three things delete it:

- `CatPicker.cs` `OnPicked` reads and `File.Delete`s it — the same line that
  already ran on iOS, unchanged.
- `CatPicker.purge()` empties the directory at the *start* of every pick, for
  the case where the process died between the camera writing and Unity reading.
- Every failure path purges before answering.

Measured: after a completed camera capture, `adb root` then
`ls -la /data/data/com.sootpaw.game/cache/catpick` shows an empty directory.

## 4. Preparing: a port of `CatPhoto.swift`, not a reinterpretation

`SIDE = 512`, `MIN_CROP_SIDE = 200`, `MAX_BYTES = 200 * 1024`, quality ladder
`{90, 80, 70, 60, 50, 40}` with the last rung returned whether it fits or not.
`expand()` and `square()` are the Swift functions transliterated.

Two deliberate divergences:

1. **Region decode instead of whole-image decode.** `CGImage.cropping()` is
   nearly free because the pixels are lazily backed; `BitmapFactory` has no
   such thing, and a 4032×3024 photograph decoded whole is 48 MB of
   ARGB_8888. `BitmapRegionDecoder` with `inSampleSize` decodes only the
   square that survives, at the largest power-of-two subsampling that still
   leaves ≥512 px a side. There is a whole-image fallback for formats
   `BitmapRegionDecoder` cannot open.
2. **EXIF orientation is applied.** On iOS it deliberately is not — the bytes
   have already been through Vision, which oriented them. On Android nothing
   has, and a phone held upright writes a landscape JPEG with a "rotate me"
   tag. The rotation is applied to the finished 512×512 square, which is exact
   for the only case Android has: with no box the crop is the centred square,
   and the centred square is invariant under quarter turns.

**Measured on the emulator.** Every row is one `I CatPhoto:` line from
`adb logcat`. The 400×400 crop is `GameBoot`'s Vision stub box, since
`CatVision` has no Android answer yet.

| source | source bytes | crop | out | note |
|---|---|---|---|---|
| cat_01.jpg 500×375 | 27 005 | 375×375 | 49 008 | box 400×400 clamped to the short side |
| big_cat.jpg 4032×3024 | 1 047 286 | 400×400 | 2 294 | 12 MP in, 2.3 KB out |
| rotated_cat.jpg 4032×3024, EXIF 6 | 712 046 | 400×400 | 7 438 | logged `EXIF orientation applied: rotate 90` |
| noise.jpg 2048×2048 random noise | 10 312 134 | 400×400 | 193 556 | worst case reachable; 11 244 bytes under the cap |
| camera capture 1392×1856 | 73 079 | 400×400 | 2 297 | `ACTION_IMAGE_CAPTURE` via `EXTRA_OUTPUT` |
| a 1080×2340 PNG | 406 222 | 400×400 | 14 493 | picked by accident; PNG in, JPEG out, no complaint |

The cap held every time and `TraitsRequest.BuildJson` never raised. The
quality ladder never had to step: the worst case above encoded to 193 556 bytes
at quality 90, against a 204 800 cap. See §7 — the lower rungs are untested on
device.

## 5. Colour: the palette was NOT copied a third time

`CatColour.swift`'s six names and `CatTraits.Allowed["base_color"]` are already
two copies of one set, and `CatColourPaletteParityTests` exists because a name
only one side knows about makes `CatTraits.FromColourOnly` throw. A Java copy
would have made it three, and nothing could have checked the third: a test that
greps a `.java` file is still only grepping.

**So there is no `CatColour.java`.** The estimate is made in
`Shell/CatColour.cs`, in managed code, on the prepared 512×512 JPEG:
`ImageConversion.LoadImage`, `GetPixels32`, mean over the centre half in each
dimension — Swift's `insetBy(dx: width * 0.25, dy: height * 0.25)` — then
nearest anchor by plain squared distance. `GetPixels32` returns stored bytes
with no colour conversion and the project renders in Gamma
(`m_ActiveColorSpace: 0`), so these are sRGB values, which is what
`CIAreaAverage` with a null working colour space produces. iOS is untouched and
still calls Swift.

**How drift is caught now, and it is stronger than the test.** `Nearest()`
checks the winning name against `CatTraits.Allowed["base_color"]` before
returning it and returns `null` if it is not there. Null already meant "could
not read a colour" and already led to `CatTraits.Default`. So a drifted name
cannot reach `FromColourOnly` at all — it is caught in the same assembly, at
runtime, by comparison rather than by regex.

**What is still duplicated, plainly: the six anchor RGB triples.** Swift's and
the C# ones must stay identical or two phones disagree about the same cat.
Nothing checks them and nothing here can — they are numbers, not names, and a
wrong one is a worse guess rather than an exception. The parity test still
covers Swift↔`CatTraits` and is untouched.

`CatColourPaletteParityTests` was not extended to cover the C# palette because
`Assets/Tests/**` was outside this task's permitted files. The change it wants
is small and belongs to whoever picks it up: parse the `Palette` array in
`Shell/CatColour.cs` the same way the Swift array is parsed, and assert all
three name sets are equal — which would also catch an anchor row deleted by
accident.

**Measured**, from `[CatColour]` log lines, each over 65 536 px (256×256, the
centre half of 512×512):

| photograph | centre mean RGB | answer |
|---|---|---|
| cat_01.jpg, a black cat | 0.128, 0.124, 0.130 | `black` |
| big_cat.jpg, stub box on a dark corner | 0.031, 0.031, 0.031 | `black` |
| rotated_cat.jpg, stub box on red bedding | 0.465, 0.208, 0.199 | `black` |
| noise.jpg, uniform random | 0.499, 0.499, 0.498 | `grey` |
| emulator camera, a white wall | 1.000, 0.973, 1.000 | `white` |

Only the first is a fair test of the estimator — the rest are the stub's
top-left 400×400 box, not a cat. It is right on that one.

## 6. Driving it from the outside

**Put a photograph in the emulator's gallery.** `adb push` alone is not enough:
the file exists but MediaProvider has not indexed it, so the photo picker never
shows it. The scan call that works on Android 15 is

```
adb push cat_01.jpg /sdcard/Pictures/catshelter/
adb shell "content call --uri content://media --method scan_file \
    --arg /sdcard/Pictures/catshelter/cat_01.jpg"
```

Confirm with
`adb shell "content query --uri content://media/external/images/media --projection _display_name:width:height:_size"`.
The older `am broadcast -a android.intent.action.MEDIA_SCANNER_SCAN_FILE` was
not used: that broadcast has been unavailable to apps since API 29.

**Open the capture screen.** `capture.txt` beside the save, as `GameBoot`
documents. For this work the useful form is a blank first line and a stub
second line, which leaves the screen interactive while standing in for the
Vision answer `CatVision` cannot give on Android:

```
printf '\nfake Cat 0.95\n' > capture.txt
adb push capture.txt /storage/emulated/0/Android/data/com.sootpaw.game/files/
adb shell am start -n com.sootpaw.game/com.unity3d.player.UnityPlayerGameActivity
```

**Screenshots** in this task's evidence: the capture screen with the camera
button showing (so `hasCamera()` answered true through JNI), the system photo
picker open over a translucent Unity, the cancelled state reading
"Спешить некуда" rather than an error, and the camera app reached with no
permission prompt.

## 7. Not tested, and why

- **The quality ladder stepping below 90.** Unreachable through the harness:
  `GameBoot`'s stub box is fixed at 400×400, so the crop is always upscaled
  to 512 and never encodes at native 512 detail. The worst case got to
  193 556 bytes against the 204 800 cap. Off device, the same noise image
  resized to a native 512×512 encodes to 220 806 bytes at quality 90 and
  115 194 at 80 (ImageMagick), so the ladder has real work to do on a device
  where the box is real. **The step-down has not run on hardware.**
- **A device with no camera.** This AVD has one: `pm list features` lists
  `android.hardware.camera.any`, and `ACTION_IMAGE_CAPTURE` resolves to
  `com.android.camera2/com.android.camera.CaptureActivity`. So `hasCamera()`
  was only ever observed returning **true**, and the hide-the-button path was
  not exercised. Both halves of it — `hasSystemFeature(FEATURE_CAMERA_ANY)`
  and `resolveActivity` — are the documented checks, but the false branch is
  unverified.
- **A file that is not an image.** The guard is a header-only
  `inJustDecodeBounds` decode that turns `outWidth <= 0` into `read_failed`.
  It could not be driven through the UI: the photo picker only offers indexed
  images, and a text file with a `.jpg` name is not indexed, so it never
  appears to be picked.
- **An oversized file.** The 32 MB copy cap was not hit; the largest test file
  was 10 312 134 bytes.
- **A real device, any real device.** Emulator only.
- **A permission refusal.** There is no permission to refuse. That is the
  design, but it also means the refusal path has nothing to test.

## 8. Cost

Compiled output of this plug-in: **23 033 bytes** of `.class` files across five
classes, a 13 451-byte release `classes.jar`, less again once dexed. No new
dependency: `androidx.core:core:1.13.1` was already in the build for
`CatShare.androidlib`, and `android.media.ExifInterface` is the platform class,
chosen over `androidx.exifinterface` deliberately — Google recommends the
AndroidX one and Lint has a check saying so, but what it buys is fixes for old
platform versions and formats the old class could not read, and this app's
floor is 33 and reads one tag. If a device is ever seen getting orientation
wrong, that is the first decision to revisit.

The APK is 67 731 906 bytes as of this build (minSdk 33, targetSdk 36). It was
53 720 643 before. **Almost none of that growth is this plug-in** — ML Kit
arrived in the same window through another worker's `CatVision.androidlib`
(`com.google.mlkit:image-labeling` and
`play-services-mlkit-subject-segmentation`).

## 9. Why every measurement above used a stubbed Vision answer

`CaptureScreen.Handle` calls `Recognise` first and returns at stage one if it
fails, before the crop. In the APK these measurements were taken from (built
17:31), `Shell/CatVision.cs` still had no Android branch: with the
`capture.txt` stub removed, a picked photograph reached
`[CaptureScreen] vision failed: vision is iOS-only` and the player was shown
"our fault". That is why `GameBoot`'s `fake Cat 0.95` line is in every run
here, and why every crop in §4 has the stub's fixed 400×400 box rather than a
box drawn round an actual cat.

`Shell/CatVision.cs` grew its Android branch at 17:32, one minute after that
build — another worker's task, landing alongside this one. **Nothing in these
notes was measured against it.** The three pieces here sit behind it unchanged
and `CaptureScreen` still needs no edit, but the first run of picker → real
Vision → crop → colour on Android has not happened yet, and the crop numbers
in §4 should be taken again once it has: a real box is a different size from
400×400, and it is the size that decides whether the quality ladder ever has
to step.
