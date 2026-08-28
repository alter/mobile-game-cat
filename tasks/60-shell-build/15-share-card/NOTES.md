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

---

# 2026-08-28 — the card looked like a Windows 95 dialog

`ios-cat-card.png` in this folder is what shipped. The owner's four complaints,
and what each one turned into. Touched: `game/Assets/View/CatCardScreen.cs` and
a new `game/Assets/View/Buttons.cs`. Nothing else — `RenderShareCard` (the PNG
that leaves the phone) is untouched, and so are `DebugGameView`, `DebugGame.uss`
and `Copy.cs`.

**NOT RUN.** No Unity, no simulator, no screenshot — those are the owner's.
What *was* run is a compile: both files built clean against 6000.3.22f1's own
`UnityEngine.UIElementsModule.dll` with Unity's Roslyn (`DotNetSdkRoslyn/csc.dll`,
`netstandard` 2.1 as corlib), no errors, and only the two
`unityBackgroundScaleMode` deprecation warnings the card already had. That
proves the API spellings exist. It proves nothing about how any of it looks.

## 1. "серые квадраты угловатые" — the buttons

`new Button()` in a runtime panel inherits Unity's default theme: grey fill,
hairline border, square corners. Both buttons in the screenshot are that.
`View/Buttons.cs` now makes them, because the ending card is about to want the
same thing.

**Apple's numbers, quoted rather than remembered** — I read the pages, and the
44 figure I re-fetched myself rather than trusting the research pass:

| number | what Apple says | where |
|---|---|---|
| **44 pt** hit region | *"As a general rule, a button needs a hit region of at least 44x44 pt — in visionOS, 60x60 pt — to ensure that people can select it easily, whether they use a fingertip, a pointer, their eyes, or a remote."* | [buttons](https://developer.apple.com/design/human-interface-guidelines/buttons) |
| **12 pt** between controls | *"In general, it works well to add about 12 points of padding around elements that include a bezel."* | [accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility) |
| two levels, not three | *"Keep the number of prominent buttons to one or two per view."* and *"Use style — not size — to visually distinguish the preferred choice"* | buttons |
| the label | *"Using title-style capitalization, consider starting the label with a verb"* | buttons |

`Buttons.Primary` is filled tan at 52 high; `Buttons.Secondary` is cream with an
ink hairline at 44 — they differ in **fill, not size**, which is Apple's rule.
Both have `minHeight`/`minWidth` 44 whatever a caller does to them.

**What Apple does NOT say, so none of this is dressed up as theirs:**

- **No corner radius for iOS.** The Buttons page gives no radius at any size;
  its only numeric size table is visionOS *heights* (28/32/44/52/64) and even
  that omits radii. `Buttons.Radius` = 12 comes from **this project** —
  `DebugGame.uss` rounds `.game__hud` at 12 and `.game__card-button` at 10.
- **No iOS button height table.** 52 for the primary is a choice above the 44
  floor, not a citation.
- **No verified type size.** Apple's Dynamic Type table for the default (Large)
  size sits behind a JavaScript tab that neither fetcher could render — only the
  xSmall column came back (Body 14, Headline 14). So **17 is not cited to
  Apple**; it is the widely repeated Large-default body size, and it sits
  between this project's own 15px card body and 22px card title.

**Colour is this project's, not iOS blue.** From `DebugGame.uss`: tan `#C9A97C`
fill, ink `#332A1E` label, cream `#F6EEDC` for the quiet button. Contrast by the
WCAG relative-luminance formula, computed rather than eyeballed: ink on tan
**6.34:1**, ink on cream **12.20:1** — both past AA (4.5:1). Ink is used rather
than the deep brown `#4A3B28` that `.game__card-button` carries, which is only
**4.85:1** on the same tan.

**One thing that had to be rebuilt by hand.** Inline styles outrank USS in UI
Toolkit — that is what lets the fill override the theme, and it also kills the
theme's `:hover`/`:active`, which are USS and cannot win against an inline
background. So `Buttons.Press` re-adds the press state as `opacity` on
PointerDown, restored on PointerUp **and** Leave **and** Cancel: a finger that
slides off never sends an Up to that element, and the button would be left dim.
Opacity rather than a scale, because it is one float and needs no struct whose
C# spelling could vary.

## 2. "зачем вообще писать her" — the label and the glyph

The label is **Share**. The button sits under a full-screen picture of one cat;
"her" names the thing the player is already looking at, and Apple's rule is a
verb and a few words.

**How the glyph is drawn.** No SF Symbol can ship inside the app's textures, and
a PNG would be one more asset to keep in step with the tint. So
`Buttons.ShareGlyph` builds the square-with-an-arrow out of **five painted
`VisualElement`s** in a 20-unit box, stroke 2:

- **the tray** — one element with `borderLeftWidth`, `borderRightWidth` and
  `borderBottomWidth` set and `borderTopWidth` **0**, so it draws as an
  open-topped U;
- **the lid, in two pieces** — two short bars on the tray's top line with a gap
  between them. Two bars rather than a top border, because a border cannot have
  a hole in it, and the hole is where the arrow goes through;
- **the shaft** — a 2-unit bar down the centre, running from near the top of the
  box down *into* the tray;
- **the head** — a square carrying **only its top and left borders** (an "L" on
  its back), given `rotate: 45deg`. Rotated about its own centre, its former
  top-left corner lands directly above that centre and the two arms fall away
  down-left and down-right. That is a chevron; a chevron on a shaft is an arrow.
  UI Toolkit's y axis points down, so a positive angle turns clockwise, which is
  the direction that puts the corner at the top. The element's top is placed at
  `apex + h*0.7071 − h/2`, which is the arithmetic that lands the point exactly
  on the top of the shaft.

`new StyleRotate(new Rotate(new Angle(45f, AngleUnit.Degree)))` is the C#
spelling; the project had only ever set `rotate` from USS, so it was worth
compiling before claiming it. It compiles.

**One ugly thing, flagged rather than hidden.** `Shell/Copy.cs` is not a file
this task may touch, and its `card.share` still reads **"Share her"**. So
`CatCardScreen.ShareLabel()` asks for `card.share_short`, and falls back to a
hard-coded English `"Share"` when that key is missing — `Copy.Of` returns
`[card.share_short]` for a miss, and the bracket is the signal. That is a hole
in the one thing `Copy.cs` exists to prevent.

> **Request for whoever owns `Copy.cs`:** add `["card.share_short"] = "Share"`,
> or change `card.share` itself to `"Share"` and point this at it. Either closes
> the hole and the fallback goes away.

## 3. "Cat Shelter" под челкой — the notch

The card is an absolutely positioned child pinned at 0/0/0/0 in the panel root,
and it was reaching the glass. `CatCardScreen.SafeAreaPad` now computes the
inset from `Screen.safeArea` itself — the same cure, and the same pixels-to-
panel-units factor (`panelWidth / Screen.width`), as `DebugGameView.FillScreen`,
which wrote down the reason: SafeArea applies its padding from `Update`,
retrying until layout is ready, so anything positioned earlier misses it and
nothing recomputes it. Applied once at Build, again on `GeometryChangedEvent`,
and again on a 100 ms schedule until the panel has a resolved width — all three,
because FillScreen learned the hard way that the callback alone can never fire.

**What I refused to assume.** FillScreen's own evidence — a room element pinned
at 0/0/0/0 that needed *negative* insets to escape the padding — says UI Toolkit
lays absolute children out **inside** the parent's padding. This card is pinned
identically and the screenshot says it was **not** inset. Both cannot be true,
and I cannot run the thing to settle which. So `SafeAreaPad` does not assume: it
**measures** how far the card's own edge already sits inside the panel's
(`worldBound` against the top ancestor's) and adds only the shortfall,
`Mathf.Max(0, want − have)`. If the parent's padding does apply, the shortfall is
zero and the inset is not paid twice; if it does not, the card pays it in full.
Both cases land in the same place. Padding does not change the element's own
border-box, so this cannot oscillate against its own geometry callback.

The Back button also moved from top-**right** to top-**left**. It is labelled
Back, and back is the top-left corner on iOS; the old corner is where Done or
Close lives, which is a different promise. The title is centred with a 96-unit
padding on both sides so it can never run under the button — an absolutely
positioned button of unknown width otherwise permits exactly that.

## 4. "The kitten lies on nothing" — the stage

**Why she was small, and it was not the percentages.** The alpha channels,
walked on 2026-08-28 (last row with alpha > 8, of a 256-square frame):

| file | alpha bbox, y | foot | fills |
|---|---|---|---|
| `coat_default_1.png` | 33..244 | **0.95** | 0.68 w × 0.82 h |
| `coat_default_2.png` | 33..244 | **0.95** | 0.62 w × 0.82 h |
| `coat_default_3.png` | 115..244 | **0.95** | 0.86 w × **0.50** h |
| `reward_blanket.png` | 54..209 | **0.82** | 0.86 w × 0.61 h |
| `reward_bowl.png` | 49..213 | **0.83** | 0.87 w × 0.64 h |

State 3 — the lying-down cat in the screenshot — fills only the **bottom half**
of her own frame. `ScaleToFit` fits the *frame*, so the old 15%/15%/72% box
spent half its height on nothing, and that is the whole of "she is small and
floating". The three coats disagree about everything except **0.95**, which is
where all three put their feet. So the layout anchors on the foot and oversizes
the frame, and it works for whichever state she is in.

`LayoutScene` runs from the stage's own `GeometryChangedEvent`, because it needs
the stage's real size:

- a **ground line** one tenth of the stage's height up from its bottom edge;
- every prop is a **square** element (`ScaleToFit` in a square box renders a
  square source at exactly the box's side — all five PNGs are square), so one
  number sizes each, and each sits on the ground by its own foot fraction;
- **blanket** frame `0.82 × stage width`, behind her;
- **kitten** frame `1.06 × stage width` — *wider than the stage on purpose*. The
  art never reaches its own frame edge (the widest coat is 0.86 of it), so
  nothing is clipped, and the cat herself lands at about **0.91 of the card's
  width**. Against the old box she is roughly one and a half times wider. Her
  paws are set 0.16 of the blanket's frame above the ground line, which is what
  reads as **on** it rather than behind it;
- **bowl** frame `0.30 × stage width`, at the right edge and a touch lower, so
  it reads as nearer the viewer than she is. Added last, so it draws in front.

Each side is also clamped against the stage's *height* (`Mathf.Min`), so a short
stage cannot push her head off the top.

`CatCardScreen` now loads two assets, which its own header used to say it never
did. That claim is updated in the file. The reason for the exception: the
blanket and the bowl are not state — they do not depend on the save, the room or
the coat — they are this card's set dressing, and the board has no reason to
know they exist. Two `Resources.Load` calls, once, on the first tap.

## Still unproven

- Everything above, on a screen. No build was run.
- **Android.** The standing rule is both platforms; only iOS has ever been shot.
- Whether the tan/cream buttons read as buttons *over a room photograph* — the
  screenshot has no room in it (40-art/07 outstanding), so the stage is paper
  and the buttons are on paper.
- The glyph at 20 units beside 17px text. The five rectangles are arithmetic;
  whether it reads as "share" at thumbnail size is an eye question.
- Item 8 of the list above is now stale: the 72%/15% numbers it names are gone.
