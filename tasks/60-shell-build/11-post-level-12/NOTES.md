
# Built, 2026-08-27 — the text half

The house already ended, but badly: winning the last pile showed the ordinary
"Room clean!" card with a **Next** button, and only pressing it revealed the
ending. VERIFY 1 asks for the ending screen instead of the regular win screen,
not after it. `Finish` now checks `RoomPlan.Next(level) == null` and shows the
ending directly.

**No button on it at all.** The previous version offered "Play again", which is
the replay-from-scratch the scope excludes. A card at the end of the house has
nowhere to go, and a button would have to invent a destination — so `ShowCard`
learned to hide its primary button rather than be given a fake one.

**No call to action of any kind** (VERIFY 2): no waitlist, no email, no
purchase, no "wishlist the full version". The MVP's own rule is to build no
second-wave feature before gate 3, and a teaser here would be one.

The copy:

> **Every room is clean**
> All twelve of them, and one kitten who no longer has anywhere to hide her
> finds.
>
> That is as far as this house goes for now.

An honest stop. It says the content ended, not that the player did something
wrong and not that something is coming.

The save is cleared at that point, so relaunching does not drop the player back
onto a finished board.

## What is still missing, and why the task stays open

SCOPE asks the screen to show **the fully-clean house map and the cat in its
final form**. Both are art: the map is 37 PNGs (`40-art/06`) and the cat is six
silhouettes plus 54 masks (`40-art/03`, `/04`). What exists today is the text
and the moment it appears at.

The route there is covered by test rather than by eye: `RoomPlanTests` asserts
`Next` returns null on the last level and that the house does not wrap.

## The ending, seen — 2026-08-28

`ios-every-room-is-clean.png`. "Every room is clean / All twelve of them, and one
kitten who no longer has anywhere to hide her finds. / That is as far as this
house goes for now." No button: the screen does not offer to start over, which is
out of scope on purpose.

The cat is in state 3 in the header — lying down — which is the arc landing where
it should at the last room.

Reached with `tools/save-forge/house.save`: level 37, room 12 of 12, pile 4 of 4,
eleven rooms already done, one tile left. One tap.

```
[Board] took 57, shelf=0, triples=20, available=0
[Board] house complete
```

# The dead end gets two doors — 2026-08-28

The owner played to the end and asked **"человек доиграл — и что?"**. He met a
card with no button on it, a cleared save, and no way to show anybody. Fair.

## The rule this reverses, and why that is not a reversal

The section above says "no button on it at all" and cites the scope line about
call-to-action. That line was being applied to the wrong thing. What it forbids
is a **teaser** — a waitlist, a purchase, a wishlist for content that does not
exist. Offering to show the finished house to somebody, or to say you liked the
game, sells nothing and promises nothing. VERIFY 2 stands: no purchase prompt,
no second-wave feature, nothing dated.

## What the card is now

Title, the kitten, the same two paragraphs, then a row: **Show someone** and an
unfilled heart.

- **The kitten.** `Resources/Art/cat_4_short_base.png`, loaded by name, 148px
  above the body. A fourth **pose**, not a fourth state: `CatStateFor` still
  returns 1..3, nothing here asks it anything, and the coat shader is untouched.
  She stays greyscale — the shader is keyed to the three states and this pose is
  not one of them, which is a real (small) inconsistency with the header kitten
  two lines up and is worth an eye on the first screenshot.
- **Show someone.** Calls `Shell.Share.Image`, the same sheet the kitten's card
  uses. This view composes nothing: it takes a `public Func<byte[]>
  RenderEndingCard`, exactly as `CatCardScreen` takes its `renderCard`.
  **Not wired yet** — whoever owns the picture must set it, and until they do
  the button is not drawn and the log says
  `[Board] ending card: no RenderEndingCard, share button hidden`.

## Like: which API, and why

Researched before writing, both stores' own documentation, and both rule out
their in-app rating prompt for a button the player pressed.

**Apple** — [Requesting App Store reviews](https://developer.apple.com/documentation/storekit/requesting-app-store-reviews):

> the system displays the review prompt to a user a maximum of three times
> within a 365-day period

> Avoid requesting a review as the result of a user action.

and, under *Manually request a review*, the API for exactly our case:

> To enable a person to initiate a review as a result of an action in the UI,
> the sample code uses a deep link to the App Store page for the app with the
> query parameter `action=write-review` appended to the URL

**Google** — [Google Play In-App Reviews API](https://developer.android.com/guide/playcore/in-app-review):

> you should not have a call-to-action option (such as a button) to trigger the
> API, as a user might have already hit their quota and the flow won't be shown,
> presenting a broken experience to the user. For this use case, redirect the
> user to the Play Store instead.

So: **a URL on both platforms**, not `SKStoreReviewController.requestReview` and
not `ReviewManager.launchReviewFlow`. Those are for a moment the *app* chose;
this is a moment the *player* chose.

- iOS: `https://apps.apple.com/app/id{AppStoreId}?action=write-review` — Apple's
  own sample URL with the id substituted.
- Android: `https://play.google.com/store/apps/details?id={Application.identifier}`
  — the store-listing link from
  [Linking to Google Play](https://developer.android.com/distribute/marketing-tools/linking-to-google-play).
  Play documents no write-review query parameter of its own; the listing page is
  what the in-app-review page tells you to redirect to, and the rating control
  is on it.

Both go through `Application.OpenURL`. **No native plugin was written** — no
`CatReview.swift`, no Java next to CatShare's — because the whole feature is one
documented URL per store and neither platform offers anything a plugin could add
except the caveat below.

### The App Store id does not exist — one constant, `Shell/Review.cs`

```csharp
public const string AppStoreId = "";
```

Empty on purpose. The App Store assigns it when the app record is created in App
Store Connect, it cannot be derived from the bundle id, and inventing one means a
heart that opens a stranger's app. Paste the digits from the product URL and the
button appears — nothing else changes.

**The same hole exists on Android and is easy to miss.** `ProjectSettings` still
has `applicationIdentifier: {}`, so an Android build ships as
`com.DefaultCompany.game` and that Play listing does not exist either.
`Review.Available` therefore checks both: a non-empty `AppStoreId` on iOS, and a
package name that is not Unity's default on Android.

**While `Available` is false the heart is not drawn at all** — same rule that
removed the "one more shelf" fake door (D4). A heart that opens a 404 is worse
than no heart. **Which means: on a build made today, the ending card has neither
button.** That is honest, and it is also the thing most likely to be read as
"the feature does not work". It does; the game has no store identity yet.

## The heart is drawn, not typed

Not the character ♡ (U+2661). Nothing in `DebugGame.uss` sets a font, so text
renders in Unity's default face, and a glyph that face may not carry would ship
as a blank box on the last screen of the game. It is a `Painter2D` path
(`generateVisualContent`), stroked and not filled, in `Buttons.Ink` (#332A1E).
No font to be missing from, and the same stroke on both platforms — the same
reasoning, and the same answer, as `Buttons.ShareGlyph` sitting next to it.

The hit region is a 44x44 element; the heart drawn inside it is 26x24.

**It carries no label.** That was the ask ("an unfilled heart"), and it is the
one design decision here I would want a second opinion on — a bare heart at the
end of a game reads as "favourite" at least as easily as "rate this". A word
next to it costs one copy key.

## Buttons.cs

It landed mid-task, so nothing here is written against a guess. **Show someone**
is `Buttons.Share(label, onClick)` — the filled tan button with that file's drawn
share mark in front of the label. One override: `marginRight = Buttons.Gap`,
because `Buttons` zeroes every margin on purpose and spacing is the caller's.

The heart is deliberately **not** `Buttons.Primary` or `Buttons.Secondary` —
both are filled or bezelled, and a filled button around an *unfilled* heart
argues with itself. It borrows the number that matters, `Buttons.MinTarget`
(Apple's 44pt floor), so its hit region matches its neighbour's even though
nothing is drawn at its edges; and `Buttons.Ink` (#332A1E), so the two marks on
the row are the same colour. `Buttons.Press` is private and the heart is not a
`Button`, so its four press callbacks are repeated by hand — same opacity, same
Leave/Cancel handling, and that duplication is the one thing worth folding back
into `Buttons` if a third caller ever wants it.

## What was given up

**Room 12's before/after is no longer shown.** The ending branch used to call
`ShowRoomTransformation`; it does not any more. Title + two picture frames +
four lines of body + a button row does not fit a phone card, the frames are
sized in the stylesheet this task may not touch, and the kitten holding a heart
is the picture drawn for this screen. Rooms 1–11 still show their pair on the
room's last pile; room 12's is now the one nobody sees, which is the exact fault
the section above was written to fix. **Reversible in one line** — put
`ShowRoomTransformation(_level);` back into `ShowEndingCard` — and worth doing if
the card turns out to have the room after all.

## Not verified — needs a device, and I did not build

Nothing below was run. No Unity, no simulator.

1. **Both buttons are invisible today** (no store id, no `RenderEndingCard`), so
   the first build shows the card with the kitten and nothing else. To see the
   heart, set `AppStoreId` to any digits **on a throwaway build** and check it
   opens the App Store rather than Safari.
2. **The heart's shape at 26x24.** The bezier path was written by hand and never
   rasterised. The lobes and the dimple are the parts likely to want a nudge.
3. **Card height on a small phone** (iPhone SE / a short Android). Title +
   148px kitten + body + a 44px row. If it overflows, the kitten shrinks first.
4. **Android may show a chooser.** Google's in-app example builds the listing
   link as an `ACTION_VIEW` intent with `setPackage("com.android.vending")` so
   the Play app opens directly. `Application.OpenURL` cannot set a package, so a
   device with more than one handler for `play.google.com` may show a chooser or
   a browser. If that happens on a real phone, a small Java class next to
   `CatShare.androidlib` fixes it — that is the only reason to write one.
5. **iOS `apps.apple.com` as a universal link.** Expected to open the App Store
   app directly; unconfirmed on a device.
6. **`cat_4_short_base.png` imports with `isReadable: 0`**, where cat_1..3 are
   `isReadable: 1` (they are read pixel-by-pixel by the coat shader; this pose
   is not). Painting it as a background image does not need readable pixels, so
   nothing here cares — but whoever writes `RenderEndingCard` should know, in
   case the composer wants `GetPixels` on it.

Both platforms still need a screenshot, per the standing rule.

`tools/tests/` is green: 175 passed, including `test_copy_table.py` — the two new
keys (`house.complete.share`, `house.complete.caption`) are declared and used,
and nothing declared went unused.
