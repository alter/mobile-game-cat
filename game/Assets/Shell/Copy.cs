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
                ["board.title"] = "Room {0} of {1} · pile {2} of {3}",
                ["board.items_left"] = "Items left: {0}",

                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "Room clean!",
                ["win.room_clean.body"] = "The kitten likes it better already.",
                ["win.corner.title"] = "Corner cleared!",
                ["win.corner.body"] = "{0}% of this room is done.",
                ["win.next"] = "Next",

                // --- losing a pile -------------------------------------------
                // No blame and no urgency: losing here is a setback of two
                // minutes, and the copy should not make it feel larger.
                ["lose.title"] = "Shelf jammed",
                ["lose.body"] = "Levels finished: {0}.\n\nWould you keep playing if this were the real game?",
                ["lose.replay"] = "Replay",
                ["lose.booster"] = "One more shelf",
                ["lose.booster.soon"] = "Coming soon.",

                // --- the end of the house ------------------------------------
                // An honest stop, not a teaser: no waitlist, no purchase, no
                // "coming soon, wishlist now". The MVP's own rule is to build
                // no second-wave feature before gate 3, and a call to action
                // here would be exactly that.
                ["house.complete.title"] = "Every room is clean",
                ["house.complete.body"] =
                    "All twelve of them, and one kitten who no longer has anywhere " +
                    "to hide her finds.\n\nThat is as far as this house goes for now.",

                // --- the photo screen ----------------------------------------
                ["capture.title"] = "Show us your cat",
                ["capture.hint"] = "A photo where she fills most of the frame works best.",
                ["capture.camera"] = "Take a photo",
                ["capture.gallery"] = "Choose one I have",
                ["capture.skip"] = "Not now — give me a kitten",
                ["capture.skipped"] = "A kitten is waiting for you either way.",
                ["capture.opening"] = "Opening…",
                ["capture.looking"] = "Looking…",
                ["capture.colours"] = "Copying her colours…",
                ["capture.cancelled"] = "No rush. Pick one whenever you like.",
                ["capture.failed"] = "That did not work: {0}",

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
                ["notification.body"] = "It is waiting to show you, whenever you have a minute.",
            };
    }
}
