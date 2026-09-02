# 13-ios-parity — code half only

No iOS device or Xcode project was available in this session, so the
on-device measurement half of GOAL (does `VNRecognizeAnimalsRequest` depend on
frame size, the seven-size table) was **not attempted**. That is why
`labels.txt` stays `status:in_progress` rather than closing. Everything below
is the code-debt half, which needed no device.

## 1. System-language prose across the native boundary (done)

`Plugins/iOS/CatVision.swift:76` and `CatMarks.swift` (three `notes.append`
sites, `recognise-animals failed`/`foreground mask failed`/`animal body pose
failed`) built their string with `error.localizedDescription` — text in the
device's system language, exactly the fault `CatPicker.swift:20-29` documents
and was fixed for. Added a small `code(_ error: Error) -> String` helper
(duplicated in each file, `private` is file-scope and the files already
duplicate `Detection` for the same reason) that returns `"<NSError domain>/
<code>"`, e.g. `com.apple.Vision/9` — the same shape as Android's
`CatVision.java.reason()`, which already returns `MlKitException/<code>` or a
class name rather than a message.

Checked the C# side does not parse either field by content:
`Shell/CatVision.cs` and `Shell/CatMarks.cs` only deserialise `error`/`notes`
into JSON fields and never branch on their text. One exception found and
confirmed harmless: `Shell/CatCoat.cs:171` compares
`silhouette.answer.error == Threw`, but `Threw` (`"silhouette threw"`) is a
marker **C# itself** writes when its own `catch` fires — never a string that
crosses from Swift — so the Swift-side change does not touch it.

Also found (not asked for, but real): `View/CaptureScreen.cs:427` puts
`answer.error` straight into `_detail.text`, a visible UI Toolkit label — so
the old `vision failed: <localized OS sentence>` was reaching the screen, not
just a log. This fix closes that too.

`grep -rn localizedDescription Assets/Plugins/iOS/` now only matches comments
explaining the ban and `CatPicker.swift`'s existing `NSLog` (device console
only, never crosses the boundary — the sanctioned pattern).

## 2. iOS temp photo directory never purged (done)

`CatPicker.swift` wrote `catpick-<uuid>.jpg` into `NSTemporaryDirectory()` and
never revisited it — a process killed between `deliver(_:)` writing the file
and Unity reading the path left it there until iOS felt like reclaiming the
whole temp dir. Added `purge()`, filtered to this plugin's own
`catpick-*.jpg` names, called at the start of both `CatPicker_openGallery()`
and `CatPicker_openCamera()` — the same "purge on the next pick" idiom
`CatPicker.java`/`CatPickActivity.onCreate` already uses on Android (purge
before `launch()`).

Not verified on device (none available); verified by reading and by
`swiftc -parse` passing (see VERIFY below).

## 3. Android share card lived in cache forever (done, tested on emulator)

`CatShare.java` wrote every card to the same `cache/share/kitten-card.png`
and never deleted it — `Shell/Share.cs`'s own comment says the fixed name is
deliberate ("a UUID per tap would leave one megabyte-and-a-bit behind per tap
forever… the worst case is a single stale file"), but nothing ever collected
even that one file. iOS deletes the instant the bytes are copied into a
`UIImage`, before the sheet is even shown (`CatShare.swift:85`); Android
cannot do that synchronously because the `FileProvider` URI has to keep
resolving for as long as the chosen app is reading it, and there is no OS
callback for "finished reading" — researched
(developer.android.com/training/sharing/send) rather than guessed.

Implemented the accepted compromise from that page:
`Intent.createChooser(send, null, pendingIntent.getIntentSender())` with
`EXTRA_CHOSEN_COMPONENT_INTENT_SENDER` — a `BroadcastReceiver` registered with
`Context.RECEIVER_NOT_EXPORTED` (mandatory at minSdk 33) fires once the player
picks a target, unregisters itself, and schedules the delete 3 s later
(`CLEANUP_DELAY_MS`) to give a slow target time to finish reading. Belt: the
previous card is also deleted at the **start** of every `send()`, so a share
the player backs out of (no target ever chosen, no broadcast ever fires) is
cleaned up the next time she shares — the same purge-before idiom as item 2
and as `CatPicker.purge()`.

**Tested end to end on `emulator-5554` (API 35)**, because it was reachable —
built the APK, installed it, drove the UI by hand (screenshots + `adb shell
input tap`, no root/`run-as` available on this Play-services image so the
cache directory itself isn't `ls`-able without root):

1. Reached the real `CatCardScreen` (its `Share` button calls
   `Shell.Share.Image` directly — confirmed by reading
   `View/CatCardScreen.cs:443-467`; note the `DebugGameView` harness's own
   `OnShareTapped` is wired only to a log line for its analytics hook and is
   NOT the path that fires the native share — `TapShare()` calls
   `Shell.Share.Image` unconditionally regardless of that delegate).
2. Tapped Share → the real Android Sharesheet opened with the ClipData
   thumbnail and caption ("Look at the kitten I have in Sootpaw").
3. Picked "Drive" as the target (started its "sign in" activity — enough to
   fire the chosen-component broadcast; no Google account needed for the
   test).
4. `adb logcat`, ~4 s later:
   `CatShare: deleted the sent card`

That log line is new, permanent, `Log.i`-level (added specifically because
`run-as`/cache `ls` were both closed off without root on this image) —
matches the existing "worth logging on a device build" precedent
(`CatPicker.java`'s own `Log.i("picked …")`), never reaches the player.

## 4. minSdkVersion 25 vs project floor 33 (done, build-verified)

`CatShare.androidlib/build.gradle:34` said `minSdkVersion 25` with a comment
claiming it matched `ProjectSettings AndroidMinSdkVersion: 25` — that project
setting is `33` (`ProjectSettings.asset:183`, `BuildScript.cs:166`
`AndroidApiLevel33`), and the other two `.androidlib`s
(`CatVision`, `CatPicker`) already say `33`. Fixed the number and the stale
comment.

## VERIFY, run this session

- `bash build/headless-build.sh --tests-only`: exit 0. 279/279 C# tests,
  256/256 python tests, coverage gate ≥90% held (96.7%), Android photo
  rotation check clean, `swiftc -parse` clean on all 7
  `Plugins/iOS/*.swift` files including the two touched here.
- `Unity … -executeMethod BuildScript.BuildAndroidPlayer`: two runs (one
  before, one after adding the diagnostic `Log.i`), both
  `[BuildScript] result=Succeeded … errors=0`. Confirmed the generated Gradle
  project actually picked up `minSdkVersion 33`
  (`Library/Bee/.../CatShare.androidlib/build.gradle:41`) and the edited
  `CatShare.java`, and that the APK installs and runs on an API 35 emulator
  (minSdk 33 did not break anything).
- Share flow driven on `emulator-5554` end to end, see item 3 above:
  `CatShare: deleted the sent card` observed in logcat after choosing a
  target.
- Item 2 (iOS purge) and item 1's Swift-side correctness are **not** verified
  on a device — no iOS hardware or Xcode project available. Verified by
  reading, by `swiftc -parse`, and by the C#-side content-parsing check
  above.

## Left for a device (why status stays in_progress)

- The GOAL's measurement half: whether `VNRecognizeAnimalsRequest` accuracy
  depends on frame size the way ML Kit's labeller does (`Decode.java`'s
  documented table, same seven sizes). Needs an iOS device or the macOS Vision
  probe (`tools/marks-probe` compiles `CatMarks.swift` for macOS — the same
  trick could presumably run `CatVision.swift`'s recognise call, but that
  probe and its harness do not exist yet and building one was out of scope for
  "code debt only").
- A real iOS device/Xcode run of everything touched here (items 1 and 2):
  `swiftc -parse` only proves syntax, not that the JSON, the purge, or the
  `NSError` domain/code shape behave as expected at runtime.
