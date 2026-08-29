using System;
using System.Collections.Generic;
using UnityEngine;

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
    public static partial class Copy
    {
        private static IReadOnlyDictionary<string, string> _current;
        private static IReadOnlyDictionary<SystemLanguage,
            IReadOnlyDictionary<string, string>> _tables;

        /// <summary>
        /// Every language the game speaks, by the value Unity reports for the
        /// device. Adding a third is one entry here and one table below;
        /// nothing else in the game, and no call site, changes.
        ///
        /// A lazy property and not a static field, for the reason recorded at
        /// <see cref="Current"/>: a field initialised from `English` and
        /// `Russian` would read whichever of them is declared after it as
        /// null. Evaluated on first use, both tables are certainly built.
        /// </summary>
        private static IReadOnlyDictionary<SystemLanguage,
            IReadOnlyDictionary<string, string>> Tables => _tables ??= BuildTables();

        /// <summary>
        /// The two languages written here, plus whatever the companion files
        /// add.
        ///
        /// Split into files by script on 2026-08-29, when the game went from two
        /// languages to seventeen. Seventeen tables of forty-odd strings in one
        /// file is four thousand lines nobody can review, and — the reason that
        /// actually forced it — two people cannot translate into one file at
        /// once without treading on each other.
        ///
        /// <c>Copy.Latin.cs</c> and <c>Copy.Scripts.cs</c> each fill in their own
        /// half through the hooks below. A partial method with no implementation
        /// compiles to nothing, so removing either file leaves a game that still
        /// builds and simply speaks fewer languages.
        /// </summary>
        private static Dictionary<SystemLanguage,
            IReadOnlyDictionary<string, string>> BuildTables()
        {
            var tables = new Dictionary<SystemLanguage,
                IReadOnlyDictionary<string, string>>
            {
                [SystemLanguage.English] = English,
                [SystemLanguage.Russian] = Russian,
            };
            AddLatinScript(tables);
            AddOtherScripts(tables);
            return tables;
        }

        static partial void AddLatinScript(
            Dictionary<SystemLanguage, IReadOnlyDictionary<string, string>> tables);

        static partial void AddOtherScripts(
            Dictionary<SystemLanguage, IReadOnlyDictionary<string, string>> tables);

        /// <summary>
        /// The table for a language, English for every language without one.
        /// English is the fallback rather than the nearest match because there
        /// is no nearest match to compute: two tables, and a player whose
        /// phone is in Polish is better served by a language she may read than
        /// by one chosen for her by a similarity rule this game cannot check.
        /// </summary>
        public static IReadOnlyDictionary<string, string> For(SystemLanguage language) =>
            Tables.TryGetValue(language, out var table) ? table : English;

        /// <summary>
        /// The language the game is speaking. Chosen from the device on first
        /// use; settable, so a test — or a future in-game language switch —
        /// can override it without touching a call site.
        ///
        /// A property with a lazy default rather than `= English`: static
        /// fields initialise in declaration order, so a field assigned from
        /// `English` above its declaration gets null — which is exactly what
        /// happened, and it took the whole game down on launch.
        ///
        /// `Application.systemLanguage` is a main-thread call, which every
        /// caller of `Of` already is (UI build, and EveningReminder's
        /// coroutine). If that ever stops being true, set `Current` from
        /// `GameBoot` at startup rather than making this thread-safe.
        /// </summary>
        public static IReadOnlyDictionary<string, string> Current
        {
            get => _current ??= For(Application.systemLanguage);
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
                // The game's name, chosen 2026-08-28. One invented word: it
                // finds itself in a store search, which "Cat Shelter" never
                // would — that phrase sits in the title or description of a
                // dozen shelter games — and it is true to this one, a kitten
                // found in a dusty house.
                ["card.game_name"] = "Sootpaw",
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

                // --- the house map -------------------------------------------
                // These four lived as literals inside HouseMapView until
                // 2026-08-28. The map is the FIRST screen the game shows, so
                // a Russian player met the game in English before reaching a
                // single translated string — the one place where an untranslated
                // literal costs the most.
                //
                // The title lost its "12 rooms". The house on the screen has
                // twelve rooms drawn on it, numbered; counting them for the
                // player was a caption explaining a picture that does not need
                // one, and it would have gone stale the day room 13 arrived.
                ["map.title"] = "Your house",
                ["map.legend"] =
                    "tap the lit number to play it   ·   ticked rooms are done   " +
                    "·   dim rooms are still locked",
                ["map.no_levels"] = "no levels loaded — nothing to map",
                // {0} is the system's own reason, and it arrives in the system
                // language rather than this table's. Kept anyway: a player who
                // reports "it says it could not open the room" has told us
                // more than one who reports a blank screen.
                ["map.room_failed"] = "could not open the room: {0}",
                ["map.map_failed"] = "could not open the map: {0}",

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
                // 2026-08-29. The kitten's name before the player types one.
                //
                // The stored value is `Core/Cat.cs`'s `DefaultName`, and it
                // stays the literal "Kitty" in every language: it is part of
                // the save format, `Core` is engine-free and cannot read this
                // table, and a save written on a Japanese phone must still
                // mean the same cat when the phone's language changes. What is
                // TRANSLATED is only what the player is SHOWN —
                // `MeetYourCatScreen` swaps a name equal to `Cat.DefaultName`
                // for this key on the way into the field, and swaps it back on
                // the way out, so the two never drift.
                //
                // English keeps "Kitty" on purpose, and that is the one place
                // this key is allowed to equal `Cat.DefaultName`: it is the
                // reference table, and the constant was named after it.
                //
                // Width is not a constraint here and the arithmetic is worth
                // recording so nobody re-derives it: the field is 220 units
                // (`MeetYourCatScreen.Build`, `nameWrap.style.width = 220`,
                // the TextField at 100% of it) at the default theme's 12px,
                // which is roughly forty Latin characters. The longest value
                // across all seventeen tables is seven. A name is short by
                // nature and none of these tables had to trade meaning for
                // room — unlike `capture.title`, which did.
                ["cat.default_name"] = "Kitty",

                // --- the four outcomes ---------------------------------------
                ["photo.no_animal"] = "No cat in this one. Try a photo where she fills more of the frame.",
                ["photo.dog"] = "That looks like a dog. Lovely, but this shelter is for cats.",
                ["photo.unclear"] = "A cat, but too blurry to copy her colours. One more, holding still?",
                ["photo.accepted"] = "Got her.",
                // THE ONE MESSAGE ON THIS SCREEN THAT IS NOT ABOUT THE PHOTO.
                // Three paths reach it and none of them is a judgement about
                // the picture: the recogniser could not run at all
                // (CaptureScreen.Handle, `answer.Failed`), the crop failed
                // after a cat was found, or the picker itself failed with any
                // code but "cancelled" — including "unavailable", which means
                // there is no picker on this device to open.
                //
                // The second sentence read "Try that one again?" until
                // 2026-08-29, and it was the one instruction on the screen
                // guaranteed not to work. Every path above fails the same way
                // on the same photo, every time: a picture the decoder cannot
                // read is not readable on the second tap, and a picker that
                // will not open does not open twice. It sent a player who had
                // done nothing wrong into a loop, and the loop looked like her
                // fault because she was the one repeating it.
                //
                // What replaces it is two things that can actually happen. A
                // DIFFERENT photo is a real move — the failures above are about
                // this file and this moment, not about her cat — and the skip
                // control is standing right underneath, which is why the line
                // ends by pointing at it in the same words `capture.skipped`
                // uses. The first sentence is unchanged: it was already true
                // and already put the fault where it belongs.
                //
                // No placeholder, and none may be added: `CaptureScreen` reads
                // this through `Copy.Of(key)` with no arguments. A reason code
                // formatted into a sentence here is exactly what "capture.failed"
                // did, and why it is gone (see above).
                ["photo.our_fault"] =
                    "Something went wrong on our side. Another photo may work — " +
                    "and a kitten is waiting either way.",

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

        /// <summary>
        /// Russian, 2026-08-28. The owner and the first players read Russian,
        /// and until today a Russian player posted "Look at the kitten I have
        /// in Sootpaw" to a Russian-speaking feed.
        ///
        /// Written against the screens, not against the English: every value
        /// below was read on the card, button or label it lands on, and the
        /// narrow ones are called out where a longer word would wrap a line.
        /// `12-copy-english/NOTES.md` records why each English string says what
        /// it says; none of those decisions is reversed here, and where one of
        /// them is a length limit it is a tighter limit in Russian, which runs
        /// about a fifth longer for the same sentence.
        ///
        /// Three decisions that hold across the whole table, made once here
        /// rather than argued at each key — the reasoning is in
        /// `16-localisation-ready/NOTES.md`:
        ///
        ///  - **"вы", not "ты".** The audience is women 30-55
        ///    (cat-shelter-mvp.md section 2). Most strings address nobody at
        ///    all, which is the quieter option and is taken wherever it reads
        ///    naturally.
        ///  - **"котёнок", and no pronoun for her.** The English calls the
        ///    kitten "she" everywhere on purpose (12-copy-english change 7).
        ///    Russian has no ungendered way to keep that: "котёнок" is
        ///    grammatically masculine and "кошечка" is the diminutive a
        ///    Russian ear hears as baby-talk, which section 2 rules out along
        ///    with pink and glitter. Russian drops subject pronouns freely, so
        ///    the two places the English uses "she" for the game's kitten drop
        ///    it instead of choosing a gender.
        ///  - **"она" IS used for the player's own cat** — `capture.*` and
        ///    `photo.*` — where it is a real cat with a real sex and "кошка"
        ///    is the word a Russian cat owner uses.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Russian =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                // "В комнате чисто", not "Комната убрана": the card sits over
                // the room's own before/after photographs and should name what
                // they show, quietly. "Убрана" is the register of a report
                // filed about the player's work; "чисто" is what a person says
                // looking at the second picture.
                ["win.room_clean.title"] = "В комнате чисто",
                ["win.room_clean.body"] = "Котёнку здесь уже больше нравится.",
                // "Куча", the same noun for the same object every time. The
                // English picked "pile" over "corner" because the board said
                // "pile"; the board has since lost its words entirely
                // (DebugGameView.RenderHeader, 60-shell-build/01), so this is
                // now the only place the unit is named at all — one more
                // reason not to have two words for it.
                ["win.corner.title"] = "Куча разобрана",
                ["win.corner.body"] = "Котёнок подошёл посмотреть.",
                ["win.next"] = "Дальше",

                // --- the kitten's card, and sharing her --------------------
                // NOT translated, and this is the one key that must stay
                // identical in every language: it is the name the game is
                // listed under. An app's name is not copy, and a caption that
                // says a name no store search will find is a caption that
                // sends nobody anywhere.
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Назад",
                ["card.share_short"] = "Поделиться",
                // {0} is the game's name.
                ["card.caption"] = "Смотрите, какой у меня котёнок в {0}",
                ["map.opening"] = "Открываем комнату…",
                // "Было" / "Стало", which is how a Russian before-and-after is
                // captioned — the pair is idiomatic and each word is shorter
                // than the English it replaces, under a 116px pane.
                ["win.before"] = "Было",
                ["win.after"] = "Стало",

                // --- losing a pile -------------------------------------------
                // "Полки забиты": the shelves, plural, because there are three
                // of them and the body immediately says every slot is taken.
                ["lose.title"] = "Полки забиты",
                // The rule, then that nothing is lost — the order and the job
                // of the English (12-copy-english change 3). 73 characters
                // against the English 76, so it wraps to the same three lines
                // in `.game__card-body`'s 240px.
                //
                // No placeholder, exactly as in English: `DebugGameView.Finish`
                // still passes `_levelIndex` and string.Format still ignores
                // it. Putting a {0} back here would print the argument, and
                // putting one in a key called with no arguments prints "{0}" —
                // see notification.title.
                ["lose.body"] =
                    "Все ячейки заняты, и нет трёх одинаковых. " +
                    "Куча вернётся такой, какой была.",
                ["lose.replay"] = "Заново",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "Во всём доме чисто",
                // Two sentences where the English has one clause and a comma:
                // the relative clause Russian would need ("котёнок, которому
                // больше некуда прятать находки") is a line longer than the
                // card can spare under two photographs, and a full stop costs
                // nothing here. Same image, same ending.
                ["house.complete.body"] =
                    "Все двенадцать. И котёнку больше некуда прятать находки." +
                    "\n\nНа этом дом пока заканчивается.",
                // A different verb from card.share_short, as in English:
                // "Поделиться" is already spent on the kitten's card, and this
                // is a second, different moment.
                ["house.complete.share"] = "Показать кому-нибудь",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "В {0} чисто во всех комнатах.",

                // --- the house map -------------------------------------------
                ["map.title"] = "Ваш дом",
                // Three clauses joined by the same interpunct as the English.
                // "Отмеченные" rather than "с галочкой": the mark on a cleared
                // room is a tick, and naming the glyph tells the player less
                // than naming what it means.
                ["map.legend"] =
                    "нажмите на светлый номер   ·   отмеченные комнаты убраны   " +
                    "·   тёмные пока закрыты",
                ["map.no_levels"] = "уровни не загрузились — дом нечем заполнить",
                ["map.room_failed"] = "не удалось открыть комнату: {0}",
                ["map.map_failed"] = "не удалось открыть карту: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Чего-то не хватает",
                // One instruction, and it is the one that can work — no "try
                // again later", for the reason the English gives.
                ["levels.unavailable.body"] =
                    "Комнаты не загрузились. Пожалуйста, переустановите игру.",

                // --- the photo screen ----------------------------------------
                // THE TIGHTEST STRING IN THE TABLE. CaptureScreen builds this
                // at fontSize 26 and never sets whiteSpace = Normal, so it
                // cannot wrap: it has the panel's 390 units less 48 of padding,
                // and every character past about 24 runs off the screen. This
                // is 19. "Покажите нам свою кошку" is 23 and was dropped for
                // that reason alone — "нам" adds nothing a Russian sentence
                // needs.
                ["capture.title"] = "Покажите свою кошку",
                // The reason first, the framing advice second — the order the
                // English pass settled on, and the advice is what keeps
                // Vision's rejection rate down. "Окрас" is the word a Russian
                // cat owner uses for a cat's colouring; "масть" is for horses.
                ["capture.hint"] =
                    "Котёнок в игре получит её окрас. " +
                    "Пусть она займёт весь кадр, если получится.",
                ["capture.camera"] = "Сфотографировать",
                ["capture.gallery"] = "Выбрать из своих фото",
                // In the player's voice, like the English. "Просто дайте
                // котёнка" would be the literal reading and puts the player in
                // the position of asking a favour; "хочу котёнка" is a person
                // saying what she wants.
                ["capture.skip"] = "Не сейчас — хочу котёнка",
                ["capture.skipped"] = "Котёнок всё равно вас ждёт.",
                ["capture.opening"] = "Открываем…",
                ["capture.looking"] = "Смотрим…",
                ["capture.colours"] = "Переносим её окрас…",
                ["capture.cancelled"] = "Спешить некуда. Выберите, когда захотите.",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped (MeetYourCatScreen), and
                // this one has room to spare.
                ["meet.title"] = "Вот она",
                ["meet.name_placeholder"] = "Как её зовут?",
                ["meet.confirm"] = "Это она",
                // "Мурка" — от «мурлыкать», и это ровно тот регистр, в котором
                // написано английское "Kitty": не шутка, не бренд и не
                // сюсюканье, а самая обыкновенная кошачья кличка, которую
                // живой человек действительно впишет в поле.
                //
                // Это единственная строка во всей таблице, где котёнок
                // получает род, и он женский — против решения, записанного в
                // шапке. Противоречия нет: там речь о МЕСТОИМЕНИИ для игрового
                // котёнка, а «котёнок» мужского рода, поэтому местоимение
                // опускается. Кличка местоимением не управляет — «Мурка» стоит
                // в поле сама по себе, ни одна другая строка на неё не
                // согласуется, — и женская кличка возвращает ту самую «she»,
                // которую 12-copy-english выбрал нарочно, ничего не ломая.
                //
                // Не «Мурзик» (он же в примере из cat-shelter-mvp.md, раздел 4)
                // и не «Барсик»: обе клички мужские. Не «Муся» — она столь же
                // распространена, но это уменьшительное от «Мария», и рядом с
                // именем игрока читается как человеческое имя, а не кошачье.
                ["cat.default_name"] = "Мурка",

                // --- the four outcomes ---------------------------------------
                // Each says what happened and then offers a way forward, in
                // that order, and none of them blames the player.
                ["photo.no_animal"] = "Кошки здесь не видно. Попробуйте фото, где она крупнее.",
                // "Славная" carries the English "Lovely" — a compliment to the
                // dog, so that a refusal is not a rebuke.
                ["photo.dog"] = "Похоже на собаку. Славная, но у нас приют для кошек.",
                ["photo.unclear"] = "Кошка есть, но снимок размыт — окрас не разобрать. Ещё одно, пока она сидит смирно?",
                // "Got her." — "она у нас", we have her now. Not "Готово",
                // which is the register of a progress bar finishing.
                ["photo.accepted"] = "Она у нас.",
                // "Попробуете ещё раз?" until 2026-08-29 — the same false
                // instruction the English carried, and false for the same
                // reason: every path that shows this line fails identically on
                // the same photo. "Другое фото" is the move that can work;
                // "котёнок всё равно ждёт" is `capture.skipped` word for word,
                // because it names the button standing right below.
                ["photo.our_fault"] =
                    "Что-то пошло не так на нашей стороне. Может помочь другое фото — " +
                    "и котёнок всё равно вас ждёт.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added: EveningReminder.cs:52
                // reads this through `Copy.Of(key)` with no arguments, so a
                // "{0}" here reaches a lock screen as the four literal
                // characters. The same trap is why the kitten's typed name is
                // still not in this line — see NOTES.md.
                //
                // "Ваш котёнок" was dropped: Russian notifications do not need
                // the possessive, and without it the title fits a lock screen
                // at 31 characters against the English 43.
                ["notification.title"] = "Котёнок нашёл что-то за диваном",
                // No subject. The English says "She is waiting" on purpose;
                // Russian would have to say "он", because "котёнок" is
                // masculine, and dropping the pronoun — which Russian does
                // freely — keeps the line from assigning her a sex the English
                // spent a change getting right.
                ["notification.body"] = "Ждёт, чтобы показать. Когда у вас будет минутка.",

                // Android only, and shown in system Settings rather than in
                // the game.
                ["notification.channel"] = "Вечернее напоминание",
                ["notification.channel_description"] =
                    "Одно тихое сообщение вечером — в те дни, когда вы не играли.",
            };
    }
}
