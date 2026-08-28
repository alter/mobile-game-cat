# Notes - share the transformation, not a portrait

Source: cat-shelter-tasks.md, "6.14: share the transformation, not a
portrait" (lines 755-765).

The card shows before and after, not the cat standing still. The MVP already
knows why: 'the "before-and-after" spectacle films into an eight-second
clip' (the was/became spectacle sells itself in eight seconds of footage). A
scruffy cat
in a dirty room is not a thing anyone posts; the pair is.

Two moments are worth offering the share at, and no others: right after a
room is cleaned, and at the cat's state transitions after rooms 4 and 8,
where the change in the animal itself is large enough to read at thumbnail
size. Offering it on level 1 gives her nothing to show yet.

---

# OPEN QUESTION for the owner: the portrait won, and I do not think it should have

**2026-08-28.** The owner asked for something different from everything above,
and what is built is what he asked for: tapping the kitten opens a full-screen
card showing **her in the room she is in right now**, with a big Share button.
One cat, one room, no pair.

The old reasoning is kept above rather than deleted, because it is not
obviously wrong and nobody has argued against it - it was overridden, not
answered. Set out plainly so the choice can be made on purpose:

| | before/after pair (task.txt, D8) | kitten in her room (built) |
|---|---|---|
| what it shows | a change | a state |
| why it gets posted | the change is the story; the viewer sees work done | the cat is the story; the viewer sees a nice picture |
| where the claim comes from | `cat-shelter-mvp.md` s.3, `DECISIONS.md` D8 - reasoning, explicitly "not data" | the owner, 2026-08-28 |
| art it needs | `share_frame.png` + a room in two states (`40-art/07`, todo) | one room background + the cat (`40-art/03`, landed) |
| when it can be offered | only after a room closes | any time she taps the cat |

**My objection, for the record.** D8's own text says the pair is the point
because the artifact has to be worth a stranger's attention, and a cat
standing in a room is a picture of a cat - the thing the MVP names under
"later or never" in section 14. I have no data either way, and neither does
D8; both sides here are reasoning.

**What is actually cheap.** The two are not exclusive and the difference is
small. `CatCardScreen.Build` takes the room as one `Texture2D`; a pair means a
second texture and a two-up stage inside `Stage(...)`. The Share button, the
caption, `Shell/Share.cs` and both native plug-ins are identical either way -
they take a PNG and a string and do not care what is drawn on it. The board
composes the image, not this screen (`renderCard`), so the pair could be what
gets *shared* while the portrait is what gets *shown*, if the owner wants
both.

**If the portrait stands**, three things in the repo now disagree with the
shipped behaviour and should be corrected rather than left to rot:
`task.txt` (SCOPE still says before/after), `DECISIONS.md` D8, and
`art-brief.md` s.10 - the last of which is a commissioned art asset
(`share_frame.png`, 1080x1080, "room before on the left, after on the right")
drawn for a layout that would no longer exist.

---

## What was built

| file | what it is |
|---|---|
| `game/Assets/View/CatCardScreen.cs` | the full-screen card. Code-built UI Toolkit, no UXML, like `MeetYourCatScreen`. |
| `game/Assets/Shell/Share.cs` | one entry point, `Share.Image(byte[] png, string text)`. |
| `game/Assets/Plugins/iOS/CatShare.swift` | `UIActivityViewController`. |
| `game/Assets/Plugins/Android/CatShare.androidlib/` | `ACTION_SEND` + `FileProvider`: `build.gradle`, `src/main/AndroidManifest.xml`, `src/main/res/xml/catshare_paths.xml`, two `.java` files. |
| `game/Assets/Plugins/Android/gradleTemplate.properties` | Unity's own default template plus one line, `android.useAndroidX=true`. |

`CatCardScreen` loads nothing. Cat texture, room texture and a
`Func<byte[]> renderCard` all arrive as arguments; the board owns them.
Composing the PNG is not this screen's job and not `Share`'s.

Deliberately fire-and-forget: no `UnitySendMessage` listener, no completion
callback. `CatPicker` needs one because the game is blocked until a photo comes
back; nothing waits for a share, and neither platform reports which target the
player chose. The `share_tap` hook therefore fires at the tap
(`CatCardScreen.OnShareTapped`), not on a native answer - an event named for a
share *landing* would be a number nobody can honour.

## The size of the image, and why

**1080 x 1080.**

1. `art-brief.md` line 213 and line 541 already fix `share_frame.png` at
   1080x1080 for this exact card, and note the size is deliberately not a
   power of two because it never enters an atlas. Choosing a different number
   would orphan the only art asset specified for the feature.
2. Square is the only aspect ratio that survives every target the owner named
   without being cropped by one of them. Instagram's feed is natively square;
   a 9:16 story frame gets letterboxed in a feed and a 16:9 frame gets its
   sides cut. Telegram, WhatsApp, VK and Facebook all pass a square through.
3. 1080 is the short edge of a mainstream phone screenshot. Going past it
   costs bytes for detail nobody sees: 1080x1080 encodes to roughly 1-2 MB as
   PNG, which both share paths carry without complaint.

## What must go in the Android manifest

Already written, in
`game/Assets/Plugins/Android/CatShare.androidlib/src/main/AndroidManifest.xml`,
which Gradle merges into the app manifest (Unity Manual, *Android App
Manifest*: plug-in manifests are one of the merged sources). Reproduced here
because it is the part a build engineer must be able to check without opening
the plug-in:

```xml
<application>
    <provider
        android:name="com.catshelter.share.CatShareFileProvider"
        android:authorities="${applicationId}.catshare"
        android:exported="false"
        android:grantUriPermissions="true">
        <meta-data
            android:name="android.support.FILE_PROVIDER_PATHS"
            android:resource="@xml/catshare_paths" />
    </provider>
</application>
```

and `res/xml/catshare_paths.xml`:

```xml
<paths>
    <cache-path name="card" path="share/" />
</paths>
```

**No permission is added, and none is needed.** The file lives in the app's own
internal cache; the receiving app gets read access to one URI for the length of
one intent, via `FLAG_GRANT_READ_URI_PERMISSION`. That keeps
`60-shell-build/17-permission-audit`'s standard by construction.

**Three things that will break the build if they are dropped:**

- `android.useAndroidX=true`. `FileProvider` is `androidx.core.content`, and
  Unity does not ship androidx - I unzipped
  `6000.3.22f1/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar`
  and it contains **zero** `androidx/` entries. Unity's stock
  `gradleTemplate.properties` has no `useAndroidX` line either, so the file
  under `Assets/Plugins/Android/` had to be created. It is Unity's default
  template verbatim plus that one line; the `**JVM_HEAP_SIZE**`,
  `**STREAMING_ASSETS**` and `**ADDITIONAL_PROPERTIES**` tokens must stay.
- The dependency itself, `implementation 'androidx.core:core:1.13.1'` in the
  plug-in's `build.gradle`. `google()` and `mavenCentral()` are already in
  Unity's `settingsTemplate.gradle`, so nothing else changes.
- `${applicationId}.catshare` in the manifest and
  `getPackageName() + ".catshare"` in `CatShare.java` must stay in step. If
  they diverge, `getUriForFile` throws `IllegalArgumentException` - loud, not
  silent.

## Where the API claims come from

- `init(activityItems: [Any], applicationActivities: [UIActivity]?)` - Apple,
  *UIActivityViewController*, "Initializing the activity view controller".
- The iPad rule, quoted verbatim from the same page: *"When presenting the
  view controller, you must do so using the appropriate means for the current
  device. On iPad, you must present the view controller in a popover. On
  iPhone and iPod touch, you must present it modally."* UIKit sets the popover
  style itself but does **not** anchor it, and an unanchored popover raises
  *"UIPopoverPresentationController should have a non-nil sourceView or
  barButtonItem set before the presentation occurs"* at presentation time - an
  uncaught exception, i.e. a crash on every iPad and on no iPhone. Anchor
  properties (`sourceView`, `sourceRect`, `permittedArrowDirections`) from
  Apple, *UIPopoverPresentationController*.
- `FileProvider.getUriForFile(Context, String, File)` - Android, *FileProvider*
  reference; added in androidx.core 1.1.0.
- The provider declaration attributes, the `android.support.FILE_PROVIDER_PATHS`
  meta-data name, and `<cache-path>` meaning `getCacheDir()` - same page,
  "Defining a FileProvider" / "Specifying Available Files".
- Extending `FileProvider` rather than declaring it directly, quoted: *"It is
  possible to use FileProvider directly instead of extending it. However, this
  is not reliable and will causes crashes on some devices."* Same page. Hence
  `CatShareFileProvider`, which does nothing but exist.
- `ACTION_SEND` / `createChooser` / `EXTRA_STREAM` /
  `FLAG_GRANT_READ_URI_PERMISSION` - Android, *Send data to other apps*, which
  is also where the rule against building our own share list comes from.
- `com.unity3d.player.UnityPlayer.currentActivity` - not taken on trust:
  `javap` on this project's own `classes.jar` for 6000.3.22f1 reports
  `public static android.app.Activity currentActivity;`.
- The `.androidlib` layout - copied from the one Android library plug-in this
  project already ships and that already builds under this Unity version,
  `com.unity.mobile.notifications`' `mobilenotifications.androidlib`: same
  `com.android.library` plugin id, same `namespace` in `build.gradle` instead
  of a package attribute, same `api files('../libs/unity-classes.jar')`, same
  `src/main/java` layout. Unity's own manual page for importing an Android
  Library plug-in 404s on `docs.unity3d.com` for 6000.3, so the working
  in-repo example is the evidence, not the documentation.

## BLOCKER: four copy keys, in a file I was not allowed to touch

`CatCardScreen` asks `Copy.Of` for four keys that are not in
`game/Assets/Shell/Copy.cs`. That file was outside my scope, so
`tools/tests/test_copy_table.py::test_every_key_the_code_asks_for_exists`
**fails right now** - I ran it: 35 passed, 1 failed, and that is the one.
Every other check in that file passes on all five new sources, Swift included.

Add these and it goes green:

```csharp
// --- the shareable card ---------------------------------------------
["card.game_name"] = "<the game's name>",
["card.share"] = "Share",
["card.close"] = "Close",
["card.caption"] = "Look at the kitten I have in {0}",
```

`card.game_name` is left blank on purpose: **the game has no name yet.**
`ProjectSettings.asset` says `productName: game` and
`companyName: DefaultCompany`, so `Application.productName` would put the
literal word "game" on the card and in the caption. Someone has to decide the
name before this ships - it goes on the shared image, which is the most public
thing the project produces.

Why a key and not a constant: `card.caption` is a format string with the name
as `{0}`, so a translator gets the whole sentence and the word order is theirs.
Building it in C# as `"Look at the kitten I have in " + name` is exactly the
concatenated-sentence shape `test_copy_table.py` was extended to catch on
2026-08-27.

## What I could not verify without a build or a device

Everything below is reasoned or read from documentation, never executed. No
Unity build, no simulator, no emulator, no device was run - by instruction.

1. **Nothing here has been compiled.** Not the Swift, not the Java, not the
   two C# files. `CatShare.swift` follows `CatPicker.swift`'s conventions
   exactly (`@_cdecl`, `#if os(iOS)`, no bridging header), and those do work in
   this project, but "follows a working file" is not "compiles".
2. **The Android half has never been built at all**, and cannot be yet: the
   project has no Android build pipeline (`90-android/02-build-pipeline` is
   outstanding) and, before that, `90-android/00-android-decision` is a real
   gate on the whole phase. `CatShare.androidlib` is written against Unity
   6000.3.22f1's gradle templates as read off this machine; whether AGP 9.0.0
   accepts `androidx.core:core:1.13.1` in this configuration is unproven.
3. **Whether Unity picks up `src/main/AndroidManifest.xml` and `src/main/res/`**
   inside an `.androidlib` that supplies its own `build.gradle`. The in-repo
   example proves `src/main/java` works and that a plug-in-supplied
   `build.gradle` is honoured; it ships no manifest and no resources, so that
   half is standard AGP convention rather than something I watched work here.
   If it fails, the fallback is Unity's flattened layout (`AndroidManifest.xml`
   and `res/` at the `.androidlib` root, which is what `libTemplate.gradle`
   expects when a plug-in has no `build.gradle` of its own).
4. **The iPad path, which is the one that crashes.** The anchor is set behind
   `if let popover = ...`, which is nil on iPhone - so an iPhone build proves
   nothing about it. This needs an actual iPad or iPad simulator.
5. **`UIApplication.shared.windows`** is deprecated since iOS 15. It is used
   because `CatPicker.swift` uses it and works; a deprecation warning is
   expected, and if the project ever moves to `connectedScenes`, both files
   should move together.
6. **Sharing a `UIImage` rather than a file URL.** A URL would give AirDrop and
   Files a real `.png` with a name; a `UIImage` is what every target reads as a
   picture without inferring from an extension. I chose the `UIImage`. Which
   behaves better across the owner's list of apps is a device question; if a
   URL turns out to be better, it is two lines in `CatShare.swift`.
7. **What each target does with the caption.** Instagram's feed composer is
   widely reported to drop the text item and keep only the picture. Nothing in
   either plug-in can change that. If the caption turns out to matter, the only
   fix is to draw the words into the PNG - the board's job, not this task's.
8. **The layout numbers** in `CatCardScreen` (52pt button, 72% stage height,
   15% side inset on the kitten) are chosen against PanelSettings' 390x844
   reference and have never been looked at on a screen, let alone a tablet.
9. **`Application.temporaryCachePath` on iOS** is assumed readable by
   `UIActivityViewController`. It resolves under `Library/Caches`, inside the
   app container, so it should be; unproven. The Android side sidesteps the
   equivalent question by letting Java choose the directory, because Unity's
   `temporaryCachePath` moves between internal and external storage with the
   project's write-permission setting - and an external path is outside
   anything `catshare_paths.xml` allows.
10. **Both platforms, per the standing rule.** Only iOS has ever been run in
    this project. This task adds an Android plug-in that no screenshot covers.

## The picture that leaves the phone — 2026-08-28

The owner: "кот на странице где мы шарим лежит просто на пустой странице, а
должен лежать в комнате, мы же для этого в комнатах оставляли 30-35% пустого
места на переднем плане снизу."

He is right, and the art was built for it. `Art/share_room_NN.png` is that
foreground: a 1080×1080 square cut from the bottom of each **clean** room —
2048×2048 at +0+1835 of the 2048×4096 file, then scaled — which lands exactly on
the empty floor the rooms reserve. Twelve files, imported **readable**.

Readable, and composed on the CPU, deliberately. The alternative is a
`RenderTexture` blit, and that is the path that blanked the iOS simulator for a
whole session on 28.08. Twelve small readable squares is the cheaper price, and
the composition is a couple of nested loops over 1.1M pixels — well under a
frame.

**The clean room, whatever state the player is in.** This picture leaves the
phone and nobody posts the mess. It is also the only choice that makes the
picture the same shape for every player at every moment, which matters for
something that is supposed to be recognisable.

The kitten is drawn at 72% of the card and sits low, on the floor rather than
floating in the middle.

**Not done here:** the game's name and the caption are drawn by nothing yet — the
card is room plus cat. The name is blocked on the game not having one
(`productName: game`), which is the owner's to give and appears in every repost.
