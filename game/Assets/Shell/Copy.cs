using System;
using System.Collections.Generic;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 60-shell-build/16: every string a player reads, in one place with a
    /// key. Translating then means adding a table, not hunting through C# for
    /// quoted English.
    ///
    /// Not `com.unity.localization`: it pulls in addressables and an editor
    /// workflow for what is currently about thirty strings, and the MVP rule is
    /// to add a dependency only when hand-writing is worse. If the game ships
    /// and needs plurals, gendered forms or right-to-left, revisit — this table
    /// is a lookup, not a localisation system, and it does not pretend to be.
    ///
    /// Analytics event names are deliberately NOT here: they are protocol, not
    /// copy, and translating them would break the funnel.
    /// </summary>
    public static class Copy
    {
        private static IReadOnlyDictionary<string, string> _current;

        /// <summary>
        /// Swap this to change language. One table per language, added as a
        /// file; nothing else in the game changes.
        ///
        /// A property with a lazy default rather than `= English`: static
        /// fields initialise in declaration order, so a field assigned from
        /// `English` above its declaration gets null — which is exactly what
        /// happened, and it took the whole game down on launch.
        /// </summary>
        public static IReadOnlyDictionary<string, string> Current
        {
            get => _current ??= English;
            set => _current = value;
        }

        public static string Of(string key)
        {
            if (Current.TryGetValue(key, out var text)) return text;
            // Loud rather than blank: a missing key should be obvious in the
            // first screenshot, not discovered by a player looking at an empty
            // button.
            return $"[{key}]";
        }

        /// <summary>Formatted lookup, for the handful of strings with numbers.</summary>
        public static string Of(string key, params object[] args) =>
            string.Format(Of(key), args);

        public static readonly IReadOnlyDictionary<string, string> English =
            new Dictionary<string, string>
            {
                // --- the board -----------------------------------------------

                // --- finishing a pile ----------------------------------------
                // "Room clean!" until 2026-08-28. The exclamation mark was
                // congratulating the player for a tap, which section 2 of
                // cat-shelter-mvp.md rules out along with rushing and
                // competition; and the card now carries the room's own
                // before/after photographs, which SHOW it clean far better
                // than a shout does. A title over two pictures should name
                // what they are, quietly, and then get out of the way.
                ["win.room_clean.title"] = "The room is clean",
                ["win.room_clean.body"] = "The kitten likes it better already.",
                // "Corner cleared!" until 2026-08-28, and the comment below
                // had already diagnosed the fault without fixing it: the
                // header two lines above this card says "pile 2 of 3", and
                // this card answered with a different noun for the same
                // thing. A player meeting both in one second has to work out
                // that a corner is a pile. One word, and it is the one the
                // board uses, because the board says it on every screen and
                // this card appears once.
                ["win.corner.title"] = "Pile cleared",
                // No count here on purpose: the header above the pile already
                // reads "Room 3 of 12 - pile 2 of 3". The card used to say
                // "67% of this room is done", which is the same number said
                // worse - a fraction with no denominator on screen, and a
                // second word ("corner") for the thing the header calls a
                // pile. What the card is for is the small beat of having
                // finished one, so it says that instead of counting again.
                ["win.corner.body"] = "The kitten came over to look.",
                ["win.next"] = "Next",

                // --- the kitten's card, and sharing her --------------------
                //
                // `card.game_name` is a placeholder and must not ship as one:
                // ProjectSettings still says `productName: game`, so the game
                // has no name, and a caption reading "Look at the kitten I have
                // in Cat Shelter" is a name I invented rather than one anybody
                // chose. Raised with the owner; it belongs in ProjectSettings
                // and here at the same time.
                ["card.game_name"] = "Cat Shelter",
                ["card.close"] = "Back",
                // Beside the share glyph. Just the verb: the owner asked why it
                // said "Share her" at all, and he is right — the icon and the
                // picture in it already say who.
                ["card.share_short"] = "Share",
                // {0} is the game's name. Kept short: it rides on a picture and
                // most people rewrite the caption anyway.
                ["card.caption"] = "Look at the kitten I have in {0}",
                // Shown over the map for the second or so it takes to build
                // the board. Names the room being entered rather than saying
                // "Loading": the player tapped a room, and the answer should
                // be about the room.
                ["map.opening"] = "Opening the room…",
                // 60-shell-build/06: captions for the room's before/after,
                // shown on a room's last pile only. Room art itself does not
                // exist yet (40-art/07 is todo) — see that task's NOTES.md
                // for what stands in.
                ["win.before"] = "Before",
                ["win.after"] = "After",

                // --- losing a pile -------------------------------------------
                // No blame and no urgency: losing here is a setback of two
                // minutes, and the copy should not make it feel larger.
                ["lose.title"] = "Shelf jammed",
                // The question "Would you keep playing if this were the real
                // game?" was here until 2026-08-27, and "Levels finished: {0}."
                // replaced it until 2026-08-28. That line defended itself as
                // "a fact and not a reproach". Three things were wrong with
                // it, and the screenshot in 07-lose-screen-fake-door/
                // ios-shelf-jammed.png shows all three at once:
                //
                //  - it reads "Levels finished: 0." on a first loss, which is
                //    a scoreboard of nothing shown at the exact moment the
                //    player failed. A fact can still be a reproach when the
                //    fact is zero (mvp section 2: no humiliation on loss);
                //  - "Levels" is engine vocabulary. The player is never shown
                //    the word anywhere else — the header counts rooms and
                //    piles, the map counts rooms — so the one number on this
                //    card is denominated in a unit the game never taught;
                //  - it says nothing about what just happened. "Shelf jammed"
                //    is exact but terse, and a first-time player who does not
                //    yet know that three of a kind clear a slot cannot tell a
                //    jam from a bug.
                //
                // What replaces it states the rule at the one moment the
                // player is guaranteed to care about it, and then says
                // nothing is lost — which is the no-guilt half of D4 ("losing
                // is not punishment here") said to the player rather than
                // only in the decision log. It is still the only place in the
                // game where the matching rule is written down; see this
                // task's NOTES.md for why that is a gap and not a fix.
                //
                // No placeholder any more. The call site still passes
                // `_levelIndex` (DebugGameView.cs, Finish) and that is
                // harmless — string.Format ignores an argument the format
                // string does not use — but the argument is now dead and
                // should go whenever that file is next open.
                ["lose.body"] =
                    "Every slot is full and no three the same. " +
                    "The pile goes back the way it was.",
                ["lose.replay"] = "Replay",
                // "One more shelf" / "Coming soon." lived here until
                // 2026-08-27. The offer is gone until there is a price behind
                // it (D4); the wording is kept in that decision, not here,
                // because an unused key is copy nobody sees that gets
                // translated anyway.

                // --- the end of the house ------------------------------------
                // An honest stop, not a teaser: no waitlist, no purchase, no
                // "coming soon, wishlist now". The MVP's own rule is to build
                // no second-wave feature before gate 3, and a call to action
                // here would be exactly that.
                ["house.complete.title"] = "Every room is clean",
                // Shortened on 2026-08-28, not rewritten. The old text
                // ("All twelve of them, and one kitten who no longer has
                // anywhere to hide her finds.") rendered as five wrapped
                // lines — 11-post-level-12/ios-every-room-is-clean.png,
                // taken when this card held nothing but words. It has since
                // gained room 12's before/after pair above it
                // (DebugGameView.Finish calls ShowRoomTransformation on this
                // branch too), so the same paragraph now sits under two
                // photographs on a phone card. Same image, same ending, two
                // fewer lines.
                ["house.complete.body"] =
                    "All twelve, and a kitten with nowhere left to hide her finds." +
                    "\n\nThat is as far as the house goes for now.",
                // 2026-08-28. The card above was, until today, the whole
                // ending: no button on it at all, by an old reading of this
                // task's "no call to action". The owner played to the end and
                // asked "человек доиграл — и что?" — and he is right that the
                // rule was being applied to the wrong thing. What that scope
                // line forbids is a *teaser*: a waitlist, a purchase, a
                // wishlist for content that does not exist. Offering to show
                // the finished house to somebody, or to say you liked it, sells
                // nothing and promises nothing. Two buttons, and neither of
                // them asks the player for money or for patience.
                //
                // "Show someone", not "Share": the complaint was that there was
                // no way to show anyone, and the game already says "Share her"
                // on the kitten's card. A second, different moment deserves a
                // different verb rather than the same one twice.
                ["house.complete.share"] = "Show someone",
                // {0} is the game's name, same as card.caption. Kept to one
                // short sentence: it rides on a picture, and most people
                // rewrite the caption anyway.
                ["house.complete.caption"] = "Every room in {0} is clean.",

                // --- levels missing or broken ---------------------------------
                // Shipped level data is gated before release (test_ship_levels.py,
                // HeadlessRunTests): this should never fire. It exists as the
                // floor under that gate — an honest stop instead of the blank
                // screen a malformed or missing file used to leave behind
                // (Core/LevelLoadPolicy, task 30-levels-solver/06).
                ["levels.unavailable.title"] = "Something is missing",
                // "...could not be loaded this time. Please reinstall or try
                // again later." until 2026-08-28. Both hedges were false:
                // shipped level data is either in the app or not, so there is
                // no "this time", and waiting changes nothing. Offering
                // "try again later" to someone whose install is broken sends
                // them away to fail again. One instruction, and it is the one
                // that can actually work.
                ["levels.unavailable.body"] =
                    "The rooms could not be loaded. Please reinstall the game.",

                // --- the photo screen ----------------------------------------
                // Kept short deliberately: CaptureScreen builds this at
                // fontSize 26 and does NOT set whiteSpace = Normal, so this
                // label cannot wrap. A title that reads well in this table
                // and runs past the padding on a phone is exactly the failure
                // this pass was looking for — anything much longer than this
                // has to go in "capture.hint" below, which does wrap.
                ["capture.title"] = "Show us your cat",
                // Until 2026-08-28 this line spent itself entirely on framing
                // advice and never said why a photo is being asked for. On
                // the screen the whole concept rests on (mvp section 5: "the
                // main feature and the main source of cheap installs"), the
                // one wrapping label was explaining how to hold a camera to
                // someone who had not been told what the picture is for. The
                // reason comes first now; the advice, which is what keeps
                // Vision's rejection rate down, survives in the second half.
                ["capture.hint"] =
                    "The kitten in the game gets her colours. " +
                    "Fill the frame with her if you can.",
                ["capture.camera"] = "Take a photo",
                ["capture.gallery"] = "Choose one I have",
                ["capture.skip"] = "Not now — give me a kitten",
                ["capture.skipped"] = "A kitten is waiting for you either way.",
                ["capture.opening"] = "Opening…",
                ["capture.looking"] = "Looking…",
                ["capture.colours"] = "Copying her colours…",
                ["capture.cancelled"] = "No rush. Pick one whenever you like.",
                // "capture.failed" = "That did not work: {0}" lived here
                // until 2026-08-27. It formatted a raw reason string from
                // CatPicker.cs/CatPicker.swift straight into a sentence -
                // untranslatable by construction, and on one path capable of
                // embedding a system-language OS error (60-shell-build/16
                // VERIFY). Picker failures now show "photo.our_fault"
                // below, the same honest message the crop-failure path
                // already used; an unused key is copy nobody sees that gets
                // translated anyway.

                // --- meeting the cat ------------------------------------------
                ["meet.title"] = "Here she is",
                ["meet.name_placeholder"] = "What's her name?",
                ["meet.confirm"] = "That's her",

                // --- the four outcomes ---------------------------------------
                ["photo.no_animal"] = "No cat in this one. Try a photo where she fills more of the frame.",
                ["photo.dog"] = "That looks like a dog. Lovely, but this shelter is for cats.",
                ["photo.unclear"] = "A cat, but too blurry to copy her colours. One more, holding still?",
                ["photo.accepted"] = "Got her.",
                ["photo.our_fault"] = "Something went wrong on our side. Try that one again?",

                // --- the evening reminder ------------------------------------
                // The kitten never gets sick: a discovery, not a chore and not
                // a reproach (cat-shelter-mvp.md section 4).
                ["notification.title"] = "Your kitten found something behind the couch",
                // "It is waiting..." until 2026-08-28. The kitten is "she"
                // in every other string in this table — "the kitten likes it
                // better", "her finds", "a photo where she fills the frame" —
                // and the one place the game speaks to a player who is not
                // holding the phone called her an it. The player has also, by
                // this point, typed a name for her (MeetYourCatScreen). See
                // NOTES.md for why the name still does not appear here.
                ["notification.body"] = "She is waiting to show you, whenever you have a minute.",

                // Android only, and shown in system Settings rather than in the
                // game — which is exactly why it belongs here and not as a
                // literal in EveningReminder. Named for what it is. One
                // channel: the game sends one kind of message, and a list of
                // switches for a single message is a list of one.
                ["notification.channel"] = "Evening reminder",
                ["notification.channel_description"] =
                    "One quiet message in the evening, on days you have not played.",
            };
    }
}
