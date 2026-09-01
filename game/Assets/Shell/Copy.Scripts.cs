using System.Collections.Generic;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// The seven languages that are not written in Latin script, 2026-08-29:
    /// Chinese (Simplified and Traditional), Japanese, Korean, Thai, Arabic,
    /// Hindi. Companion to <c>Copy.cs</c>, which owns English, Russian and the
    /// lookup; <c>Copy.Latin.cs</c> owns the rest.
    ///
    /// Every value here was written against the screen it lands on, not
    /// against the English sentence. The width notes at each key cite the
    /// element that draws it, because the two limits that actually bite are
    /// not the same for these scripts as for Latin:
    ///
    ///  - **A CJK character is one em wide.** `capture.title` is drawn at
    ///    fontSize 26 with no `whiteSpace = Normal` (CaptureScreen.cs:86-89),
    ///    inside a 390-unit panel less 48 of padding — 342 units, so THIRTEEN
    ///    Chinese, Japanese or Korean characters, not the twenty-four a Latin
    ///    line gets there. Same arithmetic for `meet.title`
    ///    (MeetYourCatScreen.cs:78-81), the card titles at 22
    ///    (.game__card-title, also unwrapped) and the 116-unit before/after
    ///    panes at 12 (.game__ba-label).
    ///  - **Thai has no spaces between words, and unlike CJK it cannot be
    ///    broken between any two characters.** UI Toolkit's standard text
    ///    generator wraps Chinese, Japanese and Korean per character with no
    ///    spaces needed, so those tables wrap by themselves; Thai needs
    ///    dictionary lookup, which needs the Advanced Text Generator and an
    ///    ICU data asset, and `Shell/PanelSettings.asset` has
    ///    `m_ICUDataAsset: {fileID: 0}` — there is none. A Thai sentence
    ///    written the way Thai is normally written is therefore ONE
    ///    unbreakable token, and the 240-unit `.game__card-body` cannot wrap
    ///    it. Every Thai value below is split with spaces at clause
    ///    boundaries, which is where written Thai puts its spaces anyway.
    ///  - **CJK wrapping works but is unpolished, and no code here fixes
    ///    that.** The rules that stop 。、」 from being pushed to the start of
    ///    a line — kinsoku — live in a PanelTextSettings asset, and
    ///    `PanelSettings.asset` has `textSettings: {fileID: 0}`, so the
    ///    leading/following character lists are empty. The symptom is an
    ///    orphaned full stop at the left edge of a wrapped card body. It is
    ///    ugly, not broken, and it is a PanelSettings fix rather than a copy
    ///    one.
    ///
    /// **Arabic will very probably not render correctly, and no code here
    /// tries to fix that.** The same `PanelSettings.asset` line means the
    /// Advanced Text Generator is not configured, so UI Toolkit neither
    /// reorders bidirectional runs nor joins Arabic letters into their
    /// contextual forms. The expected symptom is isolated, disconnected
    /// letters running left to right. The table is written correctly anyway,
    /// in logical order, so that the day a shaper is switched on it is right
    /// without being retranslated. See NOTES-scripts.md — this is a
    /// documented, unfixed risk, not an oversight.
    /// </summary>
    public static partial class Copy
    {
        static partial void AddOtherScripts(
            Dictionary<SystemLanguage, IReadOnlyDictionary<string, string>> tables)
        {
            tables[SystemLanguage.ChineseSimplified] = ChineseSimplified;
            tables[SystemLanguage.ChineseTraditional] = ChineseTraditional;
            // Unity still reports the bare `Chinese` on some devices — an
            // older Android build, or a locale string it cannot resolve any
            // further. Left unmapped it would fall through `For` to English,
            // which is the one fallback a Chinese player least needs. It goes
            // to Simplified: that is the larger population by an order of
            // magnitude, and a Traditional reader can read Simplified far
            // more easily than either can read English.
            tables[SystemLanguage.Chinese] = ChineseSimplified;
            tables[SystemLanguage.Japanese] = Japanese;
            tables[SystemLanguage.Korean] = Korean;
            tables[SystemLanguage.Thai] = Thai;
            tables[SystemLanguage.Arabic] = Arabic;
            tables[SystemLanguage.Hindi] = Hindi;
        }

        /// <summary>
        /// Simplified Chinese, mainland wording.
        ///
        /// Register: plain declarative, no exclamation mark anywhere in the
        /// table. The mobile-game register this deliberately avoids is
        /// "恭喜！""太棒了！""完成啦！" — a voice that applauds a tap. Section 2
        /// of cat-shelter-mvp.md rules out congratulation and rushing, so the
        /// cards state what is true and stop. 您 is not used either: the
        /// polite pronoun would make the game sound like a bank, and almost
        /// every string here addresses nobody at all, which is quieter still.
        /// The four strings that must address the player use 你, the ordinary
        /// second person a friend uses.
        ///
        /// **No 她 anywhere, and this is the one rule in the table that is
        /// about the cat rather than about the player.** Until 2026-08-30 ten
        /// strings here called the player's cat 她 — the written third person
        /// that is unambiguously female on the page. It was carried over from
        /// the English "she", which was itself a deliberate choice, and it was
        /// wrong for the same reason there: the animal in the photograph is the
        /// player's own, and about half of pet cats are male. A player whose
        /// tom is on the screen is told he is a she, on every screen, starting
        /// with the title of the one where they meet.
        ///
        /// Spoken Chinese would not have had the problem — tā is one sound —
        /// but the writing has to pick a character, and 她 picks female. The
        /// three ways out were 它 (the inanimate "it", which is what a form
        /// would say about somebody's cat), 牠 (the animal third person, and
        /// not current in Simplified writing at all), and dropping the pronoun.
        /// **Dropping it is the one that costs nothing**, because Chinese drops
        /// subject and possessive pronouns freely and a line without one reads
        /// as MORE spoken rather than less. Where a noun was needed to keep the
        /// sentence clear it is 猫咪 for the player's real cat — the ordinary
        /// affectionate word, no sex in it — and 小猫 for the kitten in the
        /// game, which is the word the rest of the table already uses.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> ChineseSimplified =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                // Card titles are .game__card-title: fontSize 22, no wrap, and
                // the card is 260 wide before it starts growing. At one em per
                // character that is nine characters inside the card's own
                // padding. Every title in this table is six or fewer.
                ["win.room_clean.title"] = "房间干净了",
                ["win.room_clean.body"] = "小猫已经更喜欢这里了。",
                ["win.corner.title"] = "这堆收好了",
                ["win.corner.body"] = "小猫走过来看了看。",
                ["win.next"] = "继续",

                // --- the kitten's card, and sharing her --------------------
                // Never translated. It is the name the game is listed under,
                // and a caption naming something no store search finds sends
                // nobody anywhere.
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "返回",
                ["card.share_short"] = "分享",
                // {0} is the game's name, and it stays at the end: Chinese puts
                // the place before the thing, so "在 Sootpaw 里养的小猫" would
                // bury the name mid-sentence where a reader skimming a shared
                // post will not see it.
                ["card.caption"] = "看看我在 {0} 里养的小猫",
                ["map.opening"] = "正在打开房间…",
                // .game__ba-label is fontSize 12 under a 116-unit pane — nine
                // characters of room, and these are two. 前/后 alone would be
                // ambiguous on a photograph; 之前/之后 is what a Chinese
                // before-and-after pair is captioned with.
                ["win.before"] = "之前",
                ["win.after"] = "之后",

                // --- losing a pile -------------------------------------------
                // No blame and no urgency. 卡住 is "stuck", not "failed".
                ["lose.title"] = "架子满了",
                // The rule first, then that nothing is lost — the order the
                // English pass settled on. 26 characters in .game__card-body's
                // 240 units at fontSize 15, which is 16 per line: two lines,
                // against the English three, under two photographs.
                //
                // No placeholder, exactly as in English: DebugGameView.Finish
                // still passes _levelIndex and string.Format still ignores it.
                ["lose.body"] =
                    "格子都占满了，也没有三个一样的。" +
                    "这堆会恢复原样。",
                ["lose.replay"] = "再来一次",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "整座房子都干净了",
                // Two sentences, as in Russian and for the same reason: the
                // relative clause Chinese would need to keep the English's one
                // comma is a line longer than the card can spare under two
                // photographs.
                ["house.complete.body"] =
                    // "藏她捡到的东西" until 2026-08-30. The possessive is what
                    // Chinese drops most freely of all, and 小猫 is the subject
                    // of the same clause — nothing is ambiguous without it.
                    "十二个房间，全都收拾好了。小猫再也没有地方藏捡到的东西。" +
                    "\n\n这座房子暂时就到这里。",
                // A different verb from card.share_short, as in English: 分享
                // is already spent on the kitten's card.
                ["house.complete.share"] = "给别人看看",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "{0} 里每个房间都干净了。",

                // --- the house map -------------------------------------------
                ["map.title"] = "你的房子",
                // Three clauses on the same interpunct as the English, at
                // fontSize 10 wrapping inside 92% of the width — the smallest
                // type on the screen, so this is the one string where being
                // three characters shorter is worth more than being graceful.
                ["map.legend"] =
                    "点亮着的号码可以进   ·   打勾的房间已经收好   " +
                    "·   暗的还没开",
                ["map.no_levels"] = "关卡没有加载，房子是空的",
                // {0} is the system's own reason and arrives in the system
                // language, not this table's. Kept anyway, for the reason the
                // English gives.
                ["map.room_failed"] = "打不开这个房间：{0}",
                ["map.map_failed"] = "打不开地图：{0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "少了些东西",
                // One instruction, and it is the one that can work — no "try
                // again later", for the reason the English gives.
                ["levels.unavailable.body"] =
                    "房间没能加载。请重新安装游戏。",

                // --- the photo screen ----------------------------------------
                // THE TIGHTEST STRING IN THE TABLE, and tighter here than in
                // any Latin language. CaptureScreen.cs:86-89 draws it at
                // fontSize 26 and never sets whiteSpace = Normal; 342 units of
                // panel at one em per character is thirteen. This is seven.
                ["capture.title"] = "让我们看看你的猫",
                // The reason first, the framing advice second — the order the
                // English pass settled on, and the advice is what keeps
                // Vision's rejection rate down. 毛色 is the word a Chinese cat
                // owner uses for a cat's colouring.
                //
                // "会用她的毛色…让她占满整个画面" until 2026-08-30. This is the
                // string where the pronoun could not simply be dropped: the
                // sentence has two cats in it, the one in the game and the one
                // in the photograph, so something has to distinguish them. 你家
                // 猫咪 — "your cat at home" — does it in the warmest register
                // Chinese has for the animal, and the second clause then takes
                // the bare 猫咪 because the reference is already established.
                ["capture.hint"] =
                    "游戏里的小猫会用你家猫咪的毛色。" +
                    "可以的话，让猫咪占满整个画面。",
                ["capture.camera"] = "拍一张",
                ["capture.gallery"] = "从相册里选",
                // In the player's voice, like the English: a person saying
                // what she wants, not asking a favour.
                ["capture.skip"] = "先不拍 — 我想要小猫",
                ["capture.skipped"] = "小猫都会等着你的。",
                ["capture.opening"] = "正在打开…",
                ["capture.looking"] = "正在看…",
                // "正在描她的毛色…" until 2026-08-30. A progress line has the
                // screen to itself and needs no subject at all; without one it
                // reads shorter and more spoken, which is what this row of
                // three ellipsis strings is for.
                ["capture.colours"] = "正在描毛色…",
                ["capture.cancelled"] = "不着急，什么时候想选都行。",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped
                // (MeetYourCatScreen.cs:78-81), and this one has room to spare.
                // "她来了" / "她叫什么名字？" / "就是她" until 2026-08-30 —
                // the three strings the whole defect was really about, because
                // they are the first three the player reads about her own cat.
                //
                // The title keeps the shape and swaps the pronoun for the noun:
                // 小猫来了 is the same four beats and the same news. The
                // placeholder drops the subject, which is what a Chinese
                // speaker asking this question out loud does anyway — 叫什么名字
                // is the question, 她 was never carrying any of it. The button
                // becomes 这只, the classifier standing in for the animal:
                // "this one it is", which is what Japanese この子にします and
                // Korean 이 아이로 할래요 do with their own words for a small
                // creature nobody has to sex.
                ["meet.title"] = "小猫来了",
                ["meet.name_placeholder"] = "叫什么名字？",
                ["meet.confirm"] = "就是这只",
                // 小咪 — the stock Chinese cat name, and the closest thing any
                // of these seventeen languages has to an exact "Kitty": 咪 is
                // the sound a person makes to call a cat, 小 makes it a name
                // rather than a call. Two characters, ordinary to the point of
                // being a joke about how ordinary it is, which is the register.
                //
                // **Not the bare 咪咪, which is the more famous form and was
                // rejected on purpose.** It is the same word doubled and is
                // what the dictionaries actually gloss as the Chinese term of
                // endearment for a cat — but it is also current slang for
                // breasts. Everywhere else in this table that would not matter,
                // because a sentence supplies the context; here it does not,
                // because a name sits alone in a field with nothing around it
                // to disambiguate. 小咪 is read by the same people as the same
                // kind of name and carries none of it.
                ["cat.default_name"] = "小咪",

                // --- the four outcomes ---------------------------------------
                // **All four rewritten 2026-09-01, and the change is not a
                // wording change.** Until then this screen could REFUSE: each
                // of the four ended the run and sent the player back to the
                // buttons with nothing, so each one instructed a retry, because
                // a retry was the only move left. Nothing refuses now — every
                // photograph makes a kitten, and these lines are read WHILE it
                // is being made, over a bar that says 正在描毛色…. 换一张试试 in
                // that moment contradicted the screen underneath it.
                //
                // So each line now says what we saw, then what we did, and
                // offers a better photograph as a choice — 会更准, not 试试.
                // Chinese carries "we did it anyway" with 还是, which is the
                // ordinary concessive and needs no subject.
                ["photo.no_animal"] = "这张里没看到猫，小猫还是照它做的。换一张会更准些。",
                // "不过这里是猫的收容所" until 2026-09-01 — that clause WAS the
                // refusal, and there is none. The compliment stays: it was
                // there so that being turned away was not a rebuke, and it
                // costs nothing now that nobody is turned away. 毛色 is the
                // table's word for a coat's colouring throughout, and the dog's
                // own is what the kitten takes.
                ["photo.dog"] = "这看着像狗。很可爱，小猫就用这身毛色。",
                ["photo.unclear"] = "有猫，但太糊了，毛色是猜的。清楚一点的会更准。",
                // "The kitten's with us now." Not 完成, which is the register of
                // a progress bar finishing.
                //
                // "她到我们这儿了。" until 2026-08-30. This is the moment the
                // photograph becomes the game's kitten, so 小猫 is not a
                // substitution here — it is the more accurate subject of the
                // two, and it is the word the next screen opens with.
                ["photo.accepted"] = "小猫到我们这儿了。",
                // "那张再试一次？" until 2026-08-29: it named the same photo,
                // which is the one thing that fails identically every time on
                // every path that shows this line. See the English table.
                //
                // The tail was `capture.skipped` — 小猫都会等着你的 — until
                // 2026-09-01, and it pointed at the skip button because being
                // sent back to the buttons was what happened next. It is not
                // what happens next any more: the kitten is made from the photo
                // even on this path, so the middle clause now says so, and the
                // offer of another photograph stands where the reassurance did.
                ["photo.our_fault"] = "是我们这边出了问题。小猫还是照你的照片做了，换一张也许会更好。",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added: EveningReminder.cs:52
                // reads this through Copy.Of(key) with no arguments, so a "{0}"
                // here reaches a lock screen as four literal characters.
                ["notification.title"] = "小猫在沙发后面找到了东西",
                // No subject: Chinese drops it freely, the title above has
                // already named 小猫, and the line reads as one breath rather
                // than as a report about somebody.
                //
                // The comment above said exactly this before 2026-08-30 while
                // the string underneath it began with 她 — it described the
                // sentence somebody meant to write rather than the one that
                // shipped. Dropping the pronoun makes the note true.
                ["notification.body"] = "想拿给你看，等你有空的时候。",

                // Android only, and shown in system Settings rather than in
                // the game.
                ["notification.channel"] = "傍晚的提醒",
                ["notification.channel_description"] =
                    "傍晚一条安静的消息，只在你那天没玩的时候。",
            };

        /// <summary>
        /// Traditional Chinese.
        ///
        /// **Be exact about what this is.** It is the Simplified table above
        /// converted character by character, plus ten deliberate lexical
        /// substitutions where Taiwan and the mainland use different WORDS and
        /// not merely different glyphs — listed at the keys that carry them:
        /// 相簿 for 相册, 載入 for 加载, 訊息 for 消息, 這裡 for 这儿,
        /// 模糊 for 糊, and 佔/著/裡 where Taiwan's standard character differs
        /// from the one a naive converter picks.
        ///
        /// **Ten, not eleven, since 2026-09-01.** 試試看 for the mainland's
        /// 試試 was the eleventh and it lived in exactly one string,
        /// `photo.no_animal`, which no longer asks the player to try anything —
        /// the photo screen stopped refusing, so none of the four outcome lines
        /// instructs a retry. The substitution was not withdrawn; the sentence
        /// that needed it was.
        ///
        /// It has NOT been read by a Taiwanese or Hong Kong reader, and Hong
        /// Kong wording is not addressed at all — Cantonese-influenced usage
        /// differs from Taiwan's again, and Unity reports both through this
        /// one SystemLanguage value. Treat this table as "readable and not
        /// wrong" rather than as localised. NOTES-scripts.md says the same
        /// thing at more length, and it is the first table to hand to a real
        /// reviewer if the game ever sells in Taipei.
        ///
        /// **No 她 here either, and the removal tracks the Simplified table
        /// exactly.** The argument is written out in full at the Simplified
        /// class note: ten strings called the player's own cat by the female
        /// third person, about half of pet cats are male, and Chinese lets a
        /// writer drop the pronoun instead of choosing one. Traditional
        /// writing has one option Simplified does not — 牠, the third person
        /// reserved for animals, which is current in Taiwan and would have read
        /// as neither male nor female. It was NOT taken, for two reasons: it
        /// would have split these two tables at ten keys where they have never
        /// differed except by glyph and by the eleven listed words, and 牠 is
        /// still a pronoun where the Simplified fix is a sentence that no
        /// longer needs one. 這隻 rather than 這只 at `meet.confirm` is the
        /// ordinary Traditional writing of the classifier and not a twelfth
        /// lexical substitution.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> ChineseTraditional =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "房間乾淨了",
                ["win.room_clean.body"] = "小貓已經更喜歡這裡了。",
                ["win.corner.title"] = "這堆收好了",
                ["win.corner.body"] = "小貓走過來看了看。",
                ["win.next"] = "繼續",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "返回",
                ["card.share_short"] = "分享",
                // {0} is the game's name, at the end for the same reason as in
                // the Simplified table.
                ["card.caption"] = "看看我在 {0} 裡養的小貓",
                ["map.opening"] = "正在打開房間…",
                ["win.before"] = "之前",
                ["win.after"] = "之後",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "架子滿了",
                // 佔滿, not 占满: Taiwan's standard character for "occupy" in
                // this sense is 佔, which a straight Simplified-to-Traditional
                // conversion of 占 does not always produce.
                //
                // No placeholder, exactly as in English.
                ["lose.body"] =
                    "格子都佔滿了，也沒有三個一樣的。" +
                    "這堆會恢復原樣。",
                ["lose.replay"] = "再來一次",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "整座房子都乾淨了",
                ["house.complete.body"] =
                    // "藏她撿到的東西" until 2026-08-30 — see the Simplified
                    // table. The possessive is the one Chinese drops most
                    // freely, and 小貓 is the subject of the same clause.
                    "十二個房間，全都收拾好了。小貓再也沒有地方藏撿到的東西。" +
                    "\n\n這座房子暫時就到這裡。",
                ["house.complete.share"] = "給別人看看",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "{0} 裡每個房間都乾淨了。",

                // --- the house map -------------------------------------------
                ["map.title"] = "你的房子",
                ["map.legend"] =
                    "點亮著的號碼可以進   ·   打勾的房間已經收好   " +
                    "·   暗的還沒開",
                // 載入, not 加載: this is the Taiwan word for loading data, and
                // it is one of the eleven real lexical changes rather than a
                // glyph swap. Same substitution in levels.unavailable.body.
                ["map.no_levels"] = "關卡沒有載入，房子是空的",
                ["map.room_failed"] = "打不開這個房間：{0}",
                ["map.map_failed"] = "打不開地圖：{0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "少了些東西",
                ["levels.unavailable.body"] =
                    "房間沒能載入。請重新安裝遊戲。",

                // --- the photo screen ----------------------------------------
                // Eight characters at fontSize 26, unwrapped, in 342 units —
                // the same thirteen-character ceiling as the Simplified table.
                ["capture.title"] = "讓我們看看你的貓",
                // "會用她的毛色…讓她佔滿整個畫面" until 2026-08-30. Two cats in
                // one sentence, so this is the one string where the pronoun
                // could not simply go: 你家貓咪 names the one in the
                // photograph, and the second clause takes the bare 貓咪.
                ["capture.hint"] =
                    "遊戲裡的小貓會用你家貓咪的毛色。" +
                    "可以的話，讓貓咪佔滿整個畫面。",
                ["capture.camera"] = "拍一張",
                // 相簿, not 相冊: the Taiwan word for a phone's photo library.
                ["capture.gallery"] = "從相簿裡選",
                ["capture.skip"] = "先不拍 — 我想要小貓",
                ["capture.skipped"] = "小貓都會等著你的。",
                ["capture.opening"] = "正在打開…",
                ["capture.looking"] = "正在看…",
                // "正在描她的毛色…" until 2026-08-30: a progress line has the
                // screen to itself and needs no subject.
                ["capture.colours"] = "正在描毛色…",
                // 不著急, with 著: Taiwan's standard writing of the word the
                // mainland sets as 着.
                ["capture.cancelled"] = "不著急，什麼時候想選都行。",

                // --- meeting the cat ------------------------------------------
                // "她來了" / "她叫什麼名字？" / "就是她" until 2026-08-30 — the
                // three the defect was really about, and the reasoning is set
                // out at the Simplified table. Noun for the title, no subject
                // at all for the question, classifier for the button.
                ["meet.title"] = "小貓來了",
                ["meet.name_placeholder"] = "叫什麼名字？",
                ["meet.confirm"] = "就是這隻",
                // Identical to the Simplified table, and this is the one value
                // in the whole file where that is the right answer rather than
                // an unconverted leftover: neither 小 nor 咪 was ever
                // simplified, so the Taiwan spelling of this name IS these two
                // characters, and 小咪 is as ordinary in Taipei as it is in
                // Shanghai. Changing it to differ would be inventing a
                // difference the language does not have.
                //
                // The class note above counts eleven deliberate lexical
                // substitutions between these two tables; this key is not a
                // twelfth and is not a missed one.
                ["cat.default_name"] = "小咪",

                // --- the four outcomes ---------------------------------------
                // All four rewritten 2026-09-01 — the screen no longer refuses,
                // so none of these instructs a retry. The reasoning is written
                // out at the Simplified table and this is the same copy
                // converted, as everywhere else here.
                //
                // **試試看 is gone with the string that carried it.** The class
                // note above used to count ELEVEN lexical substitutions between
                // these two tables; 試試看 for the mainland's 試試 was one of
                // them and lived only in this key, which no longer offers a try.
                // Ten now, and the class note says ten. Nothing else moved.
                ["photo.no_animal"] = "這張裡沒看到貓，小貓還是照它做的。換一張會更準些。",
                // "不過這裡是貓的收容所" until 2026-09-01 — that clause was the
                // refusal. The compliment stays; the dog's own 毛色 is what the
                // kitten takes.
                ["photo.dog"] = "這看著像狗。很可愛，小貓就用這身毛色。",
                // 太模糊了, not the mainland colloquial 太糊了.
                ["photo.unclear"] = "有貓，但太模糊了，毛色是猜的。清楚一點的會更準。",
                // 這裡, not 這兒: the 兒 suffix is northern-mainland speech and
                // reads as an accent in Taipei. "她到我們這裡了。" until
                // 2026-08-30 — this is the moment the photograph becomes the
                // game's kitten, so 小貓 is the truer subject as well as the
                // sexless one.
                ["photo.accepted"] = "小貓到我們這裡了。",
                // "那張再試一次？" until 2026-08-29 — see the English table. The
                // `capture.skipped` tail went on 2026-09-01: nothing sends the
                // player back to the buttons now, so the middle clause says
                // that the kitten was made from her photo regardless.
                ["photo.our_fault"] = "是我們這邊出了問題。小貓還是照你的照片做了，換一張也許會更好。",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added — see the Simplified
                // table and EveningReminder.cs:52.
                ["notification.title"] = "小貓在沙發後面找到了東西",
                // No subject: the title above has already named 小貓.
                // "她想拿給你看" until 2026-08-30.
                ["notification.body"] = "想拿給你看，等你有空的時候。",

                // Android only, and shown in system Settings rather than in
                // the game. 訊息, not 消息: the Taiwan word for a message a
                // system sends.
                ["notification.channel"] = "傍晚的提醒",
                ["notification.channel_description"] =
                    "傍晚一則安靜的訊息，只在你那天沒玩的時候。",
            };

        /// <summary>
        /// Japanese.
        ///
        /// **Politeness level: ですます, plain and level, with no exclamation
        /// mark anywhere in the table.** This is a decision and not a default,
        /// and it is made against two wrong registers that a mobile game
        /// reaches for by reflex:
        ///
        ///  - the loud one — 「クリア！」「おめでとうございます！」「ゲット！」,
        ///    the katakana-and-exclamation voice of a free-to-play title. It
        ///    congratulates the player for a tap, which section 2 of
        ///    cat-shelter-mvp.md rules out along with rushing and competition;
        ///  - the cute one — 「〜だよ」「〜しちゃおう」, plain form with
        ///    sentence-final particles, which reads as written for a
        ///    teenager. The audience is women 30-55.
        ///
        /// Not 尊敬語 or 謙譲語 as a house style either: honorifics throughout
        /// would make the kitten's house sound like a department store. The
        /// one 謙譲語 form in the table is `photo.accepted` 「お預かりしました」,
        /// which is deliberate — it is the moment the game takes custody of
        /// somebody's real cat, and that is exactly what humble form is for.
        ///
        /// Cards state facts about the room intransitively —
        /// 「部屋がきれいになりました」, not 「部屋をきれいにしました」— so the
        /// game reports what is true rather than crediting the player, which
        /// is the same choice the English pass made when it deleted
        /// "Room clean!".
        ///
        /// **Nothing in this table names the cat's sex, and after 2026-08-30
        /// that is the rule rather than a side effect.** The note here used to
        /// read: Japanese drops the subject freely, so the English's deliberate
        /// "she" for the kitten (12-copy-english change 7) is neither carried
        /// nor contradicted. That was true and it was recorded as a shrug — the
        /// English had made a choice and Japanese had simply been unable to
        /// copy it.
        ///
        /// The choice turned out to be the defect. The cat on the screen is
        /// built from a photograph of the player's own, and about half of pet
        /// cats are male, so "she" told a great many players their tom was a
        /// queen on every screen they opened. The Chinese, Traditional Chinese
        /// and Thai tables had all carried it and all had to be unpicked; this
        /// one had nothing to unpick, and 「この子です」/「この子にします」 became
        /// the model the other tables were rewritten against. What was an
        /// accident of Japanese grammar is now a requirement: **no line in this
        /// table may assign the kitten a sex**, and その子 — "that little one" —
        /// is the word for her when a word is needed at all.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Japanese =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                // .game__card-title is fontSize 22 and unwrapped; the overlay
                // gives the card 366 units less its 48 of padding, so fourteen
                // full-width characters. This is twelve, and it is the longest
                // title in the table.
                ["win.room_clean.title"] = "部屋がきれいになりました",
                ["win.room_clean.body"] = "子猫も気に入ったようです。",
                ["win.corner.title"] = "山が片づきました",
                ["win.corner.body"] = "子猫が見に来ました。",
                ["win.next"] = "次へ",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "戻る",
                // 共有, not シェア: 共有 is what iOS and Android already print
                // on this control in Japanese, and a player should not have to
                // learn a second word for a button she uses every day.
                ["card.share_short"] = "共有",
                // {0} is the game's name, and Japanese wants it first: the
                // place comes before the thing, and a name buried mid-sentence
                // is a name a reader skimming a feed does not see.
                ["card.caption"] = "{0} にいるうちの子猫です",
                ["map.opening"] = "部屋を開いています…",
                // .game__ba-label is fontSize 12 under a 116-unit pane — nine
                // full-width characters of room, and these are four. Bare
                // 前／後 would read as "front/back" over a photograph, which is
                // the wrong sense of both characters.
                ["win.before"] = "片づけ前",
                ["win.after"] = "片づけ後",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "棚がいっぱいです",
                // The rule first, then that nothing is lost. 41 characters in
                // .game__card-body's 240 units at fontSize 15 — sixteen per
                // line, so three lines, the same as the English.
                //
                // No placeholder, exactly as in English: DebugGameView.Finish
                // still passes _levelIndex and string.Format still ignores it.
                ["lose.body"] =
                    "置き場所がすべて埋まり、同じものが三つそろいません。" +
                    "山は元どおりに戻ります。",
                ["lose.replay"] = "もう一度",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "どの部屋もきれいです",
                ["house.complete.body"] =
                    "十二部屋、ぜんぶです。子猫にはもう、見つけたものを隠す場所がありません。" +
                    "\n\nこの家はここまでです。",
                // A different verb from card.share_short, as in English.
                ["house.complete.share"] = "だれかに見せる",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "{0} は、どの部屋もきれいになりました。",

                // --- the house map -------------------------------------------
                ["map.title"] = "あなたの家",
                // fontSize 10, wrapping inside 92% of the width — the smallest
                // type on the screen. チェック is katakana and stays: it is the
                // word Japanese UI uses for a tick, and inventing a native one
                // would be clearer to nobody.
                ["map.legend"] =
                    "明るい番号を押すと遊べます   ·   チェックのついた部屋は片づきました   " +
                    "·   暗い部屋はまだ開きません",
                ["map.no_levels"] = "部屋が読み込めず、地図に出せません",
                // {0} is the system's own reason and arrives in the system
                // language, not this table's.
                ["map.room_failed"] = "部屋を開けませんでした：{0}",
                ["map.map_failed"] = "地図を開けませんでした：{0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "何かが足りません",
                // One instruction, and it is the one that can work.
                ["levels.unavailable.body"] =
                    "部屋を読み込めませんでした。アプリを入れ直してください。",

                // --- the photo screen ----------------------------------------
                // THE TIGHTEST STRING IN THE TABLE. CaptureScreen.cs:86-89
                // draws it at fontSize 26 with no whiteSpace = Normal, in 342
                // units — thirteen full-width characters. This is twelve.
                // 「あなたの猫を見せてください」is thirteen and was dropped for
                // sitting exactly on the ceiling with nothing to spare.
                ["capture.title"] = "猫の写真を見せてください",
                // The reason first, the framing advice second — the order the
                // English pass settled on, and the advice is what keeps
                // Vision's rejection rate down. 毛色 is the ordinary Japanese
                // word for an animal's colouring.
                ["capture.hint"] =
                    "ゲームの子猫が、その子の毛色をもらいます。" +
                    "できれば画面いっぱいに写してください。",
                ["capture.camera"] = "写真を撮る",
                ["capture.gallery"] = "持っている写真から選ぶ",
                // In the player's voice, like the English: 〜たい is a person
                // saying what she wants, not a request for permission.
                ["capture.skip"] = "今はいいので、子猫に会いたい",
                ["capture.skipped"] = "どちらでも、子猫は待っています。",
                ["capture.opening"] = "開いています…",
                ["capture.looking"] = "見ています…",
                ["capture.colours"] = "毛色をうつしています…",
                ["capture.cancelled"] = "急ぎません。いつでも選んでください。",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped
                // (MeetYourCatScreen.cs:78-81), and this one has room to spare.
                ["meet.title"] = "この子です",
                ["meet.name_placeholder"] = "お名前は？",
                // タマ — the name that IS "a cat's name" in Japanese. Not the
                // most-registered name of this year (that has been ムギ for
                // seven years running, and ラテ and ルナ behind it); the one
                // everybody answers with when asked what a cat is called, the
                // way an English speaker answers "Fluffy" or "Kitty" without
                // owning a cat named either.
                //
                // That gap between "most common" and "default" is the whole
                // choice. ムギ is a name a particular person chose in 2019;
                // タマ is the one the language keeps for a cat with no name
                // yet — which is exactly what this key is for. It is plain and
                // a little old-fashioned, which suits a quiet game read by
                // women 30-55 better than a trend does.
                //
                // Katakana, not たま: Japanese pet names are conventionally
                // written in katakana, and the two characters are the ones
                // every ranking and every dictionary entry prints.
                ["cat.default_name"] = "タマ",
                // The player's own line, so 〜します rather than a bare noun:
                // she is choosing, and the button says so.
                ["meet.confirm"] = "この子にします",

                // --- the four outcomes ---------------------------------------
                // **All four rewritten 2026-09-01.** They were written when
                // this screen could still REFUSE — each ended the run and sent
                // the player back to the buttons, so each instructed a retry.
                // Nothing refuses now: the kitten is being built while the line
                // is read, under a bar reading 毛色をうつしています…, so
                // 「もう一枚」 as an instruction contradicted the screen.
                //
                // Each now says what we saw, then what we did, then offers a
                // better photograph as a possibility. Japanese carries the
                // "anyway" with 〜が + the plain report 「作りました」, and the
                // offer with 〜なら, which is a condition and not a request:
                // 「別の写真なら…なります」 states what would follow, and leaves
                // the choosing to the player. An imperative or 〜てください
                // would have put the instruction straight back in.
                ["photo.no_animal"] = "猫は写っていないようですが、この写真から作りました。別の写真なら毛色がもっと近くなります。",
                // 「ここは猫のための家です」 until 2026-09-01 — that clause WAS
                // the refusal and there is none. The compliment stays, and the
                // dog's own 毛色 is now what the kitten takes, which is the
                // whole of the second sentence.
                ["photo.dog"] = "犬のようです。かわいいですね。子猫はその毛色をもらいます。",
                ["photo.unclear"] = "猫はいますが、ぼやけていて毛色はおおよそです。はっきりした写真ならもっと確かです。",
                // 謙譲語 on purpose, and the only place in the table: this is
                // the moment the game takes custody of somebody's real cat.
                ["photo.accepted"] = "お預かりしました。",
                // "同じ写真をもう一度お願いできますか。" until 2026-08-29 — it
                // asked for the SAME photograph, which is precisely the one
                // thing that fails the same way every time. See the English
                // table.
                //
                // The `capture.skipped` tail 「どちらでも、子猫は待っています」
                // went on 2026-09-01. It pointed at the skip button because
                // being sent back to the buttons was what came next; nothing
                // does now, so the middle clause reports that the kitten was
                // made from her photograph on this path too.
                ["photo.our_fault"] =
                    "こちらで問題が起きました。それでも写真から子猫を作りました。" +
                    "別の写真ならうまくいくかもしれません。",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added: EveningReminder.cs:52
                // reads this through Copy.Of(key) with no arguments, so a "{0}"
                // here reaches a lock screen as four literal characters.
                ["notification.title"] = "子猫がソファの後ろで何か見つけました",
                ["notification.body"] = "見せたがっています。お手すきのときにどうぞ。",

                // Android only, and shown in system Settings rather than in
                // the game.
                ["notification.channel"] = "夜のお知らせ",
                ["notification.channel_description"] =
                    "遊ばなかった日の夜に、静かなお知らせを一通だけ。",
            };

        /// <summary>
        /// Korean.
        ///
        /// **Speech level: 해요체 throughout, with no exclamation mark
        /// anywhere in the table.** Chosen against three alternatives, all of
        /// which are what a Korean mobile game would actually ship:
        ///
        ///  - 합니다체 — 「청소했습니다」. Correct, and it is the register of an
        ///    announcement, a bank statement or a news reader. Over a
        ///    photograph of a kitten it is cold;
        ///  - 반말 — 「다 치웠어!」. What casual games use, and it addresses a
        ///    woman of forty as though she were a schoolfriend;
        ///  - the aegyo register — 「축하해요!」, 「냥이」, cat-speak endings.
        ///    This is the exact equivalent of the pink-with-glitter that
        ///    section 2 of cat-shelter-mvp.md rules out.
        ///
        /// 해요체 is the one that is polite without being formal, which is the
        /// voice a quiet game for women 30-55 wants. 당신 appears nowhere: it
        /// is textbook Korean rather than spoken Korean, and most strings here
        /// address nobody at all, which is quieter again.
        ///
        /// The kitten is 「아기 고양이」 and never 「냥이」 or 「야옹이」.
        ///
        /// **Nothing in this table names the cat's sex, and after 2026-08-30
        /// that is the rule rather than a side effect.** The note here used to
        /// read: Korean has no third-person pronoun a native speaker uses in
        /// running text, so — as in Japanese — the English's deliberate "she"
        /// is simply not carried, rather than replaced with something wrong.
        /// True, and recorded as a limitation.
        ///
        /// It was not a limitation. The kitten is drawn from a photograph of
        /// the player's own cat and about half of pet cats are male, so the
        /// English "she" was telling half its players the wrong thing about an
        /// animal they know personally. 「이 아이예요」/「이 아이로 할래요」 — this
        /// one, this little one — is what the Chinese and Thai tables were
        /// rewritten to match. So: **no line in this table may assign the
        /// kitten a sex**, and 그 아이 is the word for her when a word is
        /// needed, which is once, in `capture.hint`.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Korean =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                // Hangul syllables are full-width, so .game__card-title's
                // fontSize 22 and 366-unit unwrapped line give about fourteen
                // of them. The longest title here is nine.
                ["win.room_clean.title"] = "방이 깨끗해졌어요",
                ["win.room_clean.body"] = "아기 고양이가 여기를 더 좋아하네요.",
                // Intransitive, like the Japanese and for the same reason: the
                // card reports what is true rather than crediting a tap.
                ["win.corner.title"] = "더미가 정리됐어요",
                ["win.corner.body"] = "아기 고양이가 보러 왔어요.",
                ["win.next"] = "다음",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "뒤로",
                ["card.share_short"] = "공유",
                // {0} is the game's name, first: Korean marks the place with
                // 에서 before naming the thing, and a name buried mid-sentence
                // is a name a reader skimming a feed does not see.
                ["card.caption"] = "{0}에서 키우는 제 고양이예요",
                ["map.opening"] = "방을 여는 중이에요…",
                // .game__ba-label is fontSize 12 under a 116-unit pane — about
                // nine syllables, and these are four. Bare 전／후 would read as
                // a timestamp; 정리 names what the pair is a before and after
                // OF.
                ["win.before"] = "정리 전",
                ["win.after"] = "정리 후",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "선반이 가득 찼어요",
                // The rule first, then that nothing is lost.
                //
                // No placeholder, exactly as in English: DebugGameView.Finish
                // still passes _levelIndex and string.Format still ignores it.
                ["lose.body"] =
                    "자리가 다 찼는데 같은 것이 세 개가 안 돼요. " +
                    "더미는 원래대로 돌아가요.",
                ["lose.replay"] = "다시 하기",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "집 안이 다 깨끗해요",
                ["house.complete.body"] =
                    "열두 방, 전부요. 아기 고양이는 이제 주워 온 것을 숨길 데가 없어요." +
                    "\n\n집은 여기까지예요.",
                // A different verb from card.share_short, as in English.
                ["house.complete.share"] = "누군가에게 보여주기",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "{0}의 모든 방이 깨끗해졌어요.",

                // --- the house map -------------------------------------------
                // 우리 집, not 당신의 집. Korean says "our house" for the house
                // one lives in, and 당신의 is the possessive of a textbook
                // exercise. The English "Your house" is doing warmth, not
                // ownership, and 우리 집 is where that warmth lives in Korean.
                ["map.title"] = "우리 집",
                // fontSize 10, wrapping inside 92% of the width — the smallest
                // type on the screen.
                ["map.legend"] =
                    "밝은 번호를 누르면 들어가요   ·   체크된 방은 다 치웠어요   " +
                    "·   어두운 방은 아직 안 열려요",
                ["map.no_levels"] = "방을 불러오지 못해서 지도에 놓을 게 없어요",
                // {0} is the system's own reason and arrives in the system
                // language, not this table's.
                ["map.room_failed"] = "방을 열지 못했어요: {0}",
                ["map.map_failed"] = "지도를 열지 못했어요: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "뭔가 빠졌어요",
                // One instruction, and it is the one that can work.
                ["levels.unavailable.body"] =
                    "방을 불러오지 못했어요. 게임을 다시 설치해 주세요.",

                // --- the photo screen ----------------------------------------
                // THE TIGHTEST STRING IN THE TABLE. CaptureScreen.cs:86-89
                // draws it at fontSize 26 with no whiteSpace = Normal, in 342
                // units — about thirteen syllables. This is nine and a space.
                ["capture.title"] = "고양이를 보여주세요",
                // The reason first, the framing advice second — the order the
                // English pass settled on, and the advice is what keeps
                // Vision's rejection rate down. 털색 is the word a Korean cat
                // owner uses for a cat's colouring.
                ["capture.hint"] =
                    "게임 속 아기 고양이가 그 아이의 털색을 따라가요. " +
                    "될 수 있으면 화면에 가득 담아 주세요.",
                ["capture.camera"] = "사진 찍기",
                ["capture.gallery"] = "있는 사진에서 고르기",
                // In the player's voice, like the English: -ㄹ래요 is a person
                // saying what she wants.
                ["capture.skip"] = "지금은 말고 — 고양이부터 볼래요",
                ["capture.skipped"] = "그래도 아기 고양이는 기다리고 있어요.",
                ["capture.opening"] = "여는 중…",
                ["capture.looking"] = "보는 중…",
                ["capture.colours"] = "털색을 옮기는 중…",
                ["capture.cancelled"] = "급하지 않아요. 편할 때 고르세요.",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped
                // (MeetYourCatScreen.cs:78-81), and this one has room to spare.
                ["meet.title"] = "이 아이예요",
                ["meet.name_placeholder"] = "이름이 뭐예요?",
                // 나비 — the traditional Korean name for a cat, to the point
                // that 「나비야」 is how an older Korean calls one she has never
                // met. Two syllables, plain, and the exact counterpart of
                // English "Kitty": a word that became the name every unnamed
                // cat gets.
                //
                // It happens to mean "butterfly", and the etymology is
                // genuinely unsettled — one story is the cat's face, another
                // an old word for "quick". Neither matters here, because no
                // Korean hears the insect when it is a cat being called; what
                // matters is that it asserts no colour and no sex, and this
                // kitten's coat comes from the player's own photograph.
                //
                // Not 「냥이」 or 「야옹이」 — the class note above already rules
                // those out of the running text as the aegyo register that
                // section 2 of cat-shelter-mvp.md puts next to pink glitter,
                // and a name is no place to smuggle them back in.
                ["cat.default_name"] = "나비",
                // The player's own line, so -ㄹ래요 again: she is choosing, and
                // the button says so.
                ["meet.confirm"] = "이 아이로 할래요",

                // --- the four outcomes ---------------------------------------
                // **All four rewritten 2026-09-01.** Written when the screen
                // could still REFUSE — each ended the run, so each asked for
                // another photo, 「해볼까요?」 and 「한 장 더요?」. Nothing refuses
                // now: the kitten is being built as the line is read, under a
                // bar saying 털색을 옮기는 중…, and asking for another picture
                // there contradicted the screen.
                //
                // Each now says what we saw, then what we did — 그래도, the
                // ordinary Korean concessive — then what a better photograph
                // WOULD give, as a statement rather than a question. The
                // 〜ㄹ까요? ending was doing the asking, so it is gone from all
                // four; 해요체 is unchanged everywhere else.
                ["photo.no_animal"] = "이 사진에는 고양이가 없네요. 그래도 이대로 만들었어요. 다른 사진이면 털색이 더 잘 나와요.",
                // 「여기는 고양이 보호소예요」 until 2026-09-01 — that clause WAS
                // the refusal. The compliment stays, and the dog's own 털색 is
                // what the kitten takes.
                ["photo.dog"] = "강아지 같아요. 예쁘네요. 아기 고양이가 그 털색을 가져가요.",
                ["photo.unclear"] = "고양이는 있는데 흐려서 털색은 짐작이에요. 또렷한 사진이면 더 정확해요.",
                // "Got her." — she is with us now. Not 완료, which is the
                // register of a progress bar finishing.
                ["photo.accepted"] = "이제 저희가 데리고 있어요.",
                // "그 사진으로 한 번 더 해볼까요?" until 2026-08-29 — "그 사진",
                // that same photo, is the one retry that cannot work. See the
                // English table.
                //
                // The `capture.skipped` tail — 그래도 아기 고양이는 기다리고
                // 있어요 — went on 2026-09-01. It pointed at the skip button
                // below, and nothing sends her there now, so the middle clause
                // reports that the kitten was made from her photo even here.
                ["photo.our_fault"] =
                    "저희 쪽에서 문제가 생겼어요. 그래도 보내주신 사진으로 만들었어요. " +
                    "다른 사진이면 더 잘 나올 수도 있어요.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added: EveningReminder.cs:52
                // reads this through Copy.Of(key) with no arguments, so a "{0}"
                // here reaches a lock screen as four literal characters.
                ["notification.title"] = "아기 고양이가 소파 뒤에서 뭔가 찾았어요",
                ["notification.body"] = "보여주고 싶어 해요. 시간 날 때요.",

                // Android only, and shown in system Settings rather than in
                // the game.
                ["notification.channel"] = "저녁 알림",
                ["notification.channel_description"] =
                    "놀지 않은 날 저녁에, 조용한 알림 하나만요.",
            };

        /// <summary>
        /// Thai.
        ///
        /// **The spaces in these values are load-bearing, and they are not
        /// where an English eye expects them.** Thai is written without spaces
        /// between words; a space in Thai separates clauses, the way a comma
        /// does in English. UI Toolkit breaks a line on whitespace, and it can
        /// only do better than that with an ICU dictionary —
        /// `Shell/PanelSettings.asset` has `m_ICUDataAsset: {fileID: 0}`, so
        /// there is none. A Thai sentence written normally would therefore be
        /// ONE unbreakable token: in the 240-unit `.game__card-body` it would
        /// not wrap, it would run out of the card.
        ///
        /// So every value long enough to need a second line is broken at a
        /// clause boundary with a real space — which is where written Thai
        /// puts its spaces anyway, so this costs the reader nothing. **Do not
        /// "tidy up" these spaces.** Removing one turns a wrapping paragraph
        /// into a line that overflows the card.
        ///
        /// Register: plain polite, and **no ครับ/ค่ะ anywhere.** Those are the
        /// normal source of politeness in spoken Thai, and both of them are
        /// gendered by SPEAKER — the game would have to decide whether it is a
        /// man or a woman talking, on every line, which is a thing this game
        /// has no reason to be. Softness comes from word choice instead
        /// (ไม่ต้องรีบ, ได้เลย) and from addressing the player as คุณ, the
        /// neutral polite second person. กรุณา appears once, on the one string
        /// that is genuinely an instruction.
        ///
        /// **น้อง for the cat, and never เธอ.** Eight strings called the
        /// player's cat เธอ until 2026-08-30, and the irony is that this table
        /// had already refused to let the game decide the SPEAKER's sex — that
        /// is the whole ครับ/ค่ะ paragraph above — and then went and decided
        /// the CAT's. เธอ is not neutral. Wiktionary marks the third-person
        /// sense "commonly feminine"; the pronoun guides that teach it teach it
        /// as the third person for a woman, against เขา, which they call as
        /// neutral as a Thai pronoun gets. And the animal in the photograph is
        /// the player's own, of which about half are toms.
        ///
        /// The three replacements considered, and why น้อง won:
        ///
        ///  - **มัน**, the pronoun Thai grammar actually reserves for animals.
        ///    Not rude by the dictionary, and used constantly even in fond
        ///    writing about pets — but there is a live argument among Thai
        ///    speakers about exactly this, and one side hears มัน about a
        ///    loved animal as coldness. A game about somebody's own cat does
        ///    not get to take that bet;
        ///  - **เค้า/เขา**, warm and genderless, and the one the pet brands
        ///    use. Rejected for a collision: in the speech of younger women
        ///    เค้า is also an affectionate "I", so a three-word string with no
        ///    sentence around it can read as the cat talking about itself;
        ///  - **น้อง**, the kinship term for a younger sibling — no sex in it
        ///    (Wiktionary glosses both brother and sister, and records the use
        ///    "as an affectionate title for an animal"), and it is what Thai
        ///    cat owners and the Thai pet brands actually write: น้องแมว,
        ///    น้องเหมียว, "ทำความรู้จักกับน้องเหมียวของคุณ".
        ///
        /// **น้อง is the same move as Japanese この子 and Korean 이 아이** — a
        /// word for a small person in the family, borrowed for the animal,
        /// carrying warmth and no sex at all. That is the register this defect
        /// was fixed against in every table.
        ///
        /// Two exceptions, both at their keys: `photo.accepted`, where รับน้อง
        /// is a fixed phrase meaning freshers' hazing and the pronoun had to go
        /// entirely, and `notification.body`, which takes the full น้องเหมียว
        /// because a lock screen arrives with no picture beside it.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Thai =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                // Thai glyphs run about half an em, and the vowels and tone
                // marks that stack above and below the line add no width at
                // all — so .game__card-title's unwrapped 366 units are not the
                // constraint here that they are for CJK. Wrapping is.
                ["win.room_clean.title"] = "ห้องสะอาดแล้ว",
                ["win.room_clean.body"] = "ลูกแมว ชอบที่นี่ มากขึ้นแล้ว",
                ["win.corner.title"] = "เก็บกองนี้แล้ว",
                ["win.corner.body"] = "ลูกแมว เดินมาดู",
                ["win.next"] = "ต่อไป",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "กลับ",
                ["card.share_short"] = "แบ่งปัน",
                // {0} is the game's name, at the end: Thai puts the place last
                // with ใน, and the space before it is one the language would
                // want anyway.
                ["card.caption"] = "ดูลูกแมว ของฉัน ใน {0}",
                ["map.opening"] = "กำลังเปิดห้อง…",
                // .game__ba-label, fontSize 12 under a 116-unit pane. Short
                // enough not to need a break.
                ["win.before"] = "ก่อน",
                ["win.after"] = "หลัง",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "ชั้นวางเต็ม",
                // Four clause-spaces, so this wraps to three lines inside
                // .game__card-body's 240 units instead of running off the
                // card. The rule first, then that nothing is lost.
                //
                // No placeholder, exactly as in English: DebugGameView.Finish
                // still passes _levelIndex and string.Format still ignores it.
                ["lose.body"] =
                    "ช่องเต็มหมดแล้ว และไม่มีสามชิ้นที่เหมือนกัน " +
                    "กองนี้ จะกลับไปเป็นเหมือนเดิม",
                ["lose.replay"] = "เล่นใหม่",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "ทุกห้องสะอาดแล้ว",
                ["house.complete.body"] =
                    "ครบทั้งสิบสองห้อง และลูกแมว ไม่เหลือที่ซ่อนของ ที่เก็บมาได้อีกแล้ว" +
                    "\n\nบ้านหลังนี้ จบเพียงเท่านี้ก่อน",
                // A different verb from card.share_short, as in English.
                ["house.complete.share"] = "ให้ใครสักคนดู",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "ทุกห้องใน {0} สะอาดแล้ว",

                // --- the house map -------------------------------------------
                ["map.title"] = "บ้านของคุณ",
                // fontSize 10, wrapping inside 92% of the width — the smallest
                // type on the screen, and the string with the most clauses, so
                // the extra clause-spaces inside each third matter here as
                // much as the interpuncts between them.
                ["map.legend"] =
                    "แตะเลขที่สว่าง เพื่อเล่น   ·   ห้องที่มีเครื่องหมายถูก เก็บแล้ว   " +
                    "·   ห้องที่มืด ยังไม่เปิด",
                ["map.no_levels"] = "โหลดด่านไม่ได้ ไม่มีอะไรจะใส่ในแผนที่",
                // {0} is the system's own reason and arrives in the system
                // language, not this table's.
                ["map.room_failed"] = "เปิดห้องไม่ได้: {0}",
                ["map.map_failed"] = "เปิดแผนที่ไม่ได้: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "มีบางอย่างหายไป",
                // The one กรุณา in the table: this really is an instruction.
                // One instruction, and it is the one that can work.
                ["levels.unavailable.body"] =
                    "โหลดห้องไม่ได้ กรุณาติดตั้งเกมใหม่",

                // --- the photo screen ----------------------------------------
                // fontSize 26, unwrapped, 342 units (CaptureScreen.cs:86-89).
                // Thai's half-width glyphs mean this one is not tight — but it
                // also cannot break, so it is kept to one clause regardless.
                ["capture.title"] = "ขอดูแมวของคุณ",
                // The reason first, the framing advice second — the order the
                // English pass settled on, and the advice is what keeps
                // Vision's rejection rate down. Three clause-spaces, so it
                // wraps. สีขน is the Thai for an animal's coat colour.
                ["capture.hint"] =
                    // "สีขนของเธอ … ให้เธออยู่เต็มเฟรม" until 2026-08-30. The
                    // swap pays a second dividend here: the sentence has two
                    // cats in it, and ลูกแมวในเกม against น้อง now tells them
                    // apart — the kitten in the game, and the little one in
                    // front of the camera.
                    "ลูกแมวในเกม จะได้สีขนของน้อง " +
                    "ถ้าทำได้ ให้น้องอยู่เต็มเฟรม",
                ["capture.camera"] = "ถ่ายรูป",
                ["capture.gallery"] = "เลือกจากรูปที่มี",
                // In the player's voice, like the English.
                ["capture.skip"] = "ยังก่อน — ขอลูกแมวเลย",
                ["capture.skipped"] = "ลูกแมว รออยู่เสมอ",
                ["capture.opening"] = "กำลังเปิด…",
                ["capture.looking"] = "กำลังดู…",
                ["capture.colours"] = "กำลังลอกสีขน…",
                ["capture.cancelled"] = "ไม่ต้องรีบ เลือกเมื่อไรก็ได้",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped
                // (MeetYourCatScreen.cs:78-81).
                // "นี่คือเธอ" / "เธอชื่ออะไร" / "ใช่เธอเลย" until 2026-08-30 —
                // the three strings that told half the players their tom was a
                // queen, on the one screen whose whole job is to say "this is
                // YOUR cat". Ten and twelve and ten characters, all well inside
                // the unwrapped 342 units, and no internal space in any of
                // them, which matters: a space is a line break in this build.
                //
                // `meet.name_placeholder` is the confident one — น้องชื่ออะไร
                // is the formula Thai pet owners already write. The other two
                // are the frame of the old string with น้อง dropped into it,
                // which is sound Thai but is not quoted from anywhere; see the
                // report. `ใช่เลย` with no pronoun at all was the safe
                // alternative for the button and was not taken, because it
                // stops naming the cat on the button that accepts her.
                ["meet.title"] = "นี่คือน้อง",
                ["meet.name_placeholder"] = "น้องชื่ออะไร",
                ["meet.confirm"] = "ใช่น้องเลย",
                // **Thai has no stock cat name, and this is one of the two
                // tables where that had to be said rather than papered over.**
                // Japanese has タマ and Korean has 나비 — a single name the
                // language keeps for a cat nobody has named yet. Thai does not.
                // Thai names a cat for what it looks like or what it is the
                // colour of: ส้ม (orange), กะทิ (coconut milk), ปุยฝ้าย
                // (cotton), เต้าหู้ (tofu), ถ่าน (charcoal). Every one of those
                // lists is a list of coats.
                //
                // Which rules the whole method out here, and not on taste: this
                // kitten's coat is copied from the player's own cat
                // (`capture.hint` — "ลูกแมวในเกม จะได้สีขนของน้อง"), so a
                // default name that names a colour is a name that is wrong for
                // most players the second the shader runs.
                //
                // So: เหมียว, the sound a Thai cat makes and the word a Thai
                // uses for one — "แมวเหมียว" is simply what a cat is called.
                // It is the generic-word-used-as-a-name that "Kitty" is, it
                // asserts no coat, and a Thai reader takes it as a name here
                // because the field asked for one. It is a construction rather
                // than a convention, and that is the honest description of it.
                //
                // One word, no clause-space: the spaces elsewhere in this table
                // are line-break points for a wrapping label, and a name that
                // wraps in the middle would be worse than one that does not.
                ["cat.default_name"] = "เหมียว",

                // --- the four outcomes ---------------------------------------
                // **All four rewritten 2026-09-01, and still clause-spaced so
                // the wrapping label can break them — that constraint has not
                // moved and the spaces below are line-break opportunities, not
                // decoration.** What moved is the message: these were written
                // when the screen could REFUSE, so each one told the player to
                // ลอง — to try another picture — because a retry was the only
                // way on. Nothing refuses now, and the bar under the line reads
                // กำลังลอกสีขน… while it is read.
                //
                // So each says what we saw, then what we did (แต่เรา…แล้ว —
                // "but we already did"), then what a better photo WOULD give,
                // with จะ and a comparative rather than with ลอง. No กรุณา:
                // the one in this table is still the single genuine
                // instruction, at `levels.unavailable.body`.
                //
                // No ครับ/ค่ะ, as everywhere in this table, and no เธอ — see
                // the class note. น้อง is not needed in any of the four now:
                // no_animal says there is no cat to point at, dog names the
                // สุนัข, unclear talks about the สีขน rather than about the
                // animal wearing it, and our_fault is about us.
                ["photo.no_animal"] = "รูปนี้ไม่มีแมว แต่เราทำลูกแมวจากรูปนี้แล้ว รูปอื่น จะได้สีขนที่ตรงกว่า",
                // "แต่ที่นี่เป็นบ้านพักของแมว" until 2026-09-01 — that clause WAS
                // the refusal and there is none. น่ารักนะ stays; the dog's own
                // สีขน is what the kitten takes.
                ["photo.dog"] = "ดูเหมือนสุนัข น่ารักนะ ลูกแมวจะได้สีขนนี้ไป",
                ["photo.unclear"] = "มีแมวอยู่ แต่เบลอ สีขนจึงเป็นการเดา รูปที่ชัดกว่า จะแม่นกว่า",
                // **The one string in the table where น้อง could not be used,
                // and the reason is a collocation rather than a register.**
                // "รับเธอไว้แล้ว" until 2026-08-30; the obvious repair,
                // "รับน้องไว้แล้ว", walks straight into รับน้อง — the fixed
                // Thai phrase for the hazing of first-year students, with its
                // own Wikipedia article. A Thai reader would stumble over it on
                // the warmest line in the game.
                //
                // So the pronoun goes entirely, which Thai allows freely and
                // which the language does more readily than English does:
                // "รับไว้แล้ว", taken in. It is a shade drier than the string
                // it replaces, and that is the price of the collocation.
                ["photo.accepted"] = "รับไว้แล้ว",
                // "ลองรูปนั้นอีกครั้งไหม" until 2026-08-29 — "รูปนั้น", that same
                // picture, is the retry that cannot work. See the English
                // table. The `capture.skipped` tail — ลูกแมว รออยู่เสมอ — went
                // on 2026-09-01: it pointed at the skip button because being
                // sent back to the buttons was what came next, and nothing does
                // now, so the middle clause says the kitten was made from her
                // photo on this path too.
                //
                // Clause-spaced by hand, like the rest of this table: Thai
                // writes no spaces between words and this build ships no ICU
                // dictionary to break lines with (NOTES-scripts.md), so the
                // spaces here ARE the line-break opportunities. The longest
                // unbreakable run below is แต่เราทำลูกแมวจากรูปของคุณแล้ว —
                // twenty-five spacing characters, the vowels and tone marks
                // adding no width, so about 206 units against the 240 the card
                // body allows.
                ["photo.our_fault"] = "มีบางอย่างผิดพลาดทางเรา แต่เราทำลูกแมวจากรูปของคุณแล้ว รูปอื่นอาจได้ผลดีกว่า",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added: EveningReminder.cs:52
                // reads this through Copy.Of(key) with no arguments, so a "{0}"
                // here reaches a lock screen as four literal characters.
                //
                // The clause-space matters on a lock screen too: a notification
                // title is truncated, not wrapped, but the body is wrapped by
                // the OS, and iOS and Android break Thai on whitespace for the
                // same reason UI Toolkit does.
                ["notification.title"] = "ลูกแมวเจอบางอย่าง หลังโซฟา",
                // "เธอรออยู่ …" until 2026-08-30, and the full น้องเหมียว here
                // rather than the bare น้อง the rest of the table uses: a
                // notification arrives on a lock screen with no picture of a
                // cat beside it and no screen of context above it, and
                // "น้องรออยู่" with nothing around it reads as a PERSON
                // waiting. เหมียว settles it in one word.
                ["notification.body"] = "น้องเหมียวรออยู่ จะให้ดู เมื่อคุณมีเวลาสักครู่",

                // Android only, and shown in system Settings rather than in
                // the game.
                ["notification.channel"] = "เตือนตอนเย็น",
                ["notification.channel_description"] =
                    "ข้อความเงียบ ๆ หนึ่งข้อความ ตอนเย็น ในวันที่คุณไม่ได้เล่น",
            };

        /// <summary>
        /// Arabic — Modern Standard, no dialect.
        ///
        /// **THIS TABLE WILL PROBABLY NOT RENDER CORRECTLY, AND NOTHING HERE
        /// TRIES TO MAKE IT.** Arabic is right-to-left and its letters change
        /// shape depending on what they join to. Both of those are the text
        /// engine's job, and this project has not given it the means:
        /// `Shell/PanelSettings.asset` carries `m_ICUDataAsset: {fileID: 0}`
        /// and `textSettings: {fileID: 0}`, so UI Toolkit's Advanced Text
        /// Generator — the one that does bidirectional reordering and Arabic
        /// shaping — is not configured. The expected symptom is a line of
        /// isolated, unjoined letters running left to right: readable to
        /// nobody, and not something a different word choice can fix.
        ///
        /// The table is written anyway, and written CORRECTLY — logical order,
        /// no manual reversal, no pre-shaped presentation forms, no
        /// zero-width joiners. Faking right-to-left by reversing the strings
        /// here would produce something that looks better in one screenshot
        /// and is broken beyond repair the day a shaper is switched on. If the
        /// device test says the letters are disconnected, the fix is a text
        /// engine and a font, in `PanelSettings.asset` — not this file.
        /// NOTES-scripts.md carries this as a named, unresolved risk.
        ///
        /// Register: neutral MSA, no exclamation mark, and **no grammatical
        /// gender forced on the player.** Arabic imperatives are gendered
        /// (اختر / اختاري), and the game does not know who is holding the
        /// phone. So buttons use the verbal noun — التقاط صورة, اختيار صورة —
        /// which is what Arabic iOS and Android already print on their own
        /// buttons; the player's own lines use the first person (أريد), which
        /// is genderless; and the remaining instructions use impersonal
        /// constructions (يُرجى, يُفضَّل). Possessive suffixes such as بيتك and
        /// بانتظارك are written identically for both genders once the
        /// diacritics are off, which they are.
        ///
        /// **The cat is a separate problem from the player, and Arabic is the
        /// one table in this file that cannot fully solve it.** Everything
        /// above is about not gendering the PLAYER. On 2026-08-30 the same
        /// question was asked about the CAT, because the English had been
        /// calling the player's own photographed animal "she" and about half of
        /// pet cats are toms. Six strings here were rewritten; the honest
        /// summary of what could and could not be done is this:
        ///
        ///  - **What worked.** Every string about the PHOTOGRAPHED cat now
        ///    avoids agreeing with it: `ألوانها` became `الألوان`, `وهي ساكنة`
        ///    became `في لحظة سكون`, `تملأ القطّة` was deleted outright, and
        ///    `ما اسمها؟` — the feminine possessive that was the defect itself
        ///    — became `ما الاسم؟`. Naming no possessor is the one move Arabic
        ///    reliably allows.
        ///  - **What could not be done.** There is no Arabic sentence about a
        ///    single cat that assigns it no sex. The Japanese and Korean class
        ///    notes state the rule as "no line in this table may assign the
        ///    kitten a sex"; **Arabic cannot obey that rule and no attempt is
        ///    made to fake it.** So the GAME's kitten stays الهرّة, feminine, a
        ///    character the game keeps — which is what the English source still
        ///    does deliberately at `notification.body`. `win.*`, `card.caption`,
        ///    `house.complete.body`, `capture.skip`, `capture.skipped` and both
        ///    notification strings are about that kitten and were left alone on
        ///    purpose, not missed.
        ///
        /// **And قطّة is NOT the species-generic that Hindi बिल्ली is** — do
        /// not import that argument here. Counts taken from the raw HTML of two
        /// Nestlé Purina Arabia pages put قطتك at 32 and 71 against قطك at 7 and
        /// 2, so قطّتك is indeed the unmarked way to say "your cat" to a reader
        /// whose cat's sex is unknown, and `capture.title` keeps it for that
        /// reason. But the same page switches to قطك with the agreement flipped
        /// masculine, which a true generic would not do. قطّة is the DEFAULT,
        /// not the neuter. That is why the fix here was to stop things agreeing
        /// with the noun rather than to lean on the noun.
        ///
        /// `meet.title` and `meet.confirm` are medium confidence on REGISTER —
        /// their grammar is not in doubt, the question is whether they sound
        /// warm — and `ما الاسم؟` is flagged at its own key.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Arabic =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "الغرفة نظيفة",
                ["win.room_clean.body"] = "أصبحت الهرّة تحبّ المكان أكثر.",
                ["win.corner.title"] = "تمّ ترتيب الكومة",
                ["win.corner.body"] = "جاءت الهرّة لتنظر.",
                ["win.next"] = "التالي",

                // --- the kitten's card, and sharing her --------------------
                // Latin letters in an Arabic sentence are the one case where
                // the missing bidi algorithm bites even a correct string: the
                // name is a left-to-right run inside a right-to-left line, and
                // resolving that is exactly what the engine is not doing. The
                // caption is composed by Share.Image and handed to the OS
                // share sheet, though, which does have a bidi implementation —
                // so this string is likelier to survive than the ones drawn
                // inside the game.
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "رجوع",
                ["card.share_short"] = "مشاركة",
                // {0} is the game's name, and it goes last, which in a
                // right-to-left line means leftmost on the screen: Arabic
                // marks the place with في after naming the thing.
                ["card.caption"] = "انظروا إلى هرّتي في {0}",
                ["map.opening"] = "جارٍ فتح الغرفة…",
                // .game__ba-label, fontSize 12 under a 116-unit pane.
                ["win.before"] = "قبل",
                ["win.after"] = "بعد",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "الرفّ ممتلئ",
                // The rule first, then that nothing is lost.
                //
                // No placeholder, exactly as in English: DebugGameView.Finish
                // still passes _levelIndex and string.Format still ignores it.
                ["lose.body"] =
                    "كلّ الخانات ممتلئة، ولا توجد ثلاث قطع متشابهة. " +
                    "تعود الكومة كما كانت.",
                ["lose.replay"] = "من جديد",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "كلّ الغرف نظيفة",
                ["house.complete.body"] =
                    "الاثنتا عشرة كلّها، وهرّة لم يعد لديها مكان تخبّئ فيه ما تجده." +
                    "\n\nإلى هنا يتوقّف البيت في الوقت الحالي.",
                // A verbal noun, not an imperative: عرض rather than أرِ/أري,
                // for the reason in the class note above. Also a different
                // word from card.share_short, as in English.
                ["house.complete.share"] = "عرض البيت على أحد",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "كلّ غرفة في {0} نظيفة.",

                // --- the house map -------------------------------------------
                // بيتك: the possessive suffix is -ka for a man and -ki for a
                // woman, and undiacritized they are the same four letters.
                ["map.title"] = "بيتك",
                // fontSize 10, wrapping inside 92% of the width. Three clauses
                // on the same interpunct as the English — and the interpunct
                // is itself a neutral character the bidi algorithm would have
                // to place, which is one more thing to look at on the device.
                ["map.legend"] =
                    "الرقم المضيء يمكن لعبه   ·   الغرف المؤشَّرة تمّ ترتيبها   " +
                    "·   الغرف الداكنة ما زالت مقفلة",
                ["map.no_levels"] = "تعذّر تحميل المراحل، لا شيء لعرضه على الخريطة",
                // {0} is the system's own reason and arrives in the system
                // language, not this table's — which here means it is very
                // likely to be a Latin-script run inside an Arabic line.
                ["map.room_failed"] = "تعذّر فتح الغرفة: {0}",
                ["map.map_failed"] = "تعذّر فتح الخريطة: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "شيء ما ناقص",
                // يُرجى is impersonal — it asks without addressing a man or a
                // woman. One instruction, and it is the one that can work.
                ["levels.unavailable.body"] =
                    "تعذّر تحميل الغرف. يُرجى إعادة تثبيت اللعبة.",

                // --- the photo screen ----------------------------------------
                // fontSize 26, unwrapped, 342 units (CaptureScreen.cs:86-89).
                // Not an imperative: أرِنا / أرينا would pick a gender for the
                // player on the first screen she sees. "We would like to see"
                // asks for the same thing and asks it of nobody in particular.
                ["capture.title"] = "نودّ رؤية قطّتك",
                // The reason first, the framing advice second — the order the
                // English pass settled on, and the advice is what keeps
                // Vision's rejection rate down. يُفضَّل keeps the advice
                // impersonal where اجعليها would not.
                //
                // "ألوان قطّتك … أن تملأ القطّة الصورة" until 2026-08-30. Both
                // halves named the photographed cat and both then had to agree
                // with it. هذه الألوان — "these colours", the ones in the
                // picture the player is looking at — needs no owner at all, and
                // the second clause simply drops its subject. الهرّة stays: that
                // is the kitten in the game, which this table keeps feminine on
                // purpose (see the class note).
                ["capture.hint"] =
                    "ستأخذ الهرّة في اللعبة هذه الألوان. " +
                    "ويُفضَّل أن تملأ الصورة كلّها.",
                // Verbal nouns, as on Arabic iOS and Android.
                ["capture.camera"] = "التقاط صورة",
                ["capture.gallery"] = "اختيار صورة موجودة",
                // In the player's voice, like the English — and أريد is the
                // same word whoever says it.
                ["capture.skip"] = "ليس الآن — أريد هرّة",
                ["capture.skipped"] = "الهرّة بانتظارك على أيّ حال.",
                ["capture.opening"] = "جارٍ الفتح…",
                ["capture.looking"] = "جارٍ النظر…",
                // "جارٍ نقل ألوانها…" until 2026-08-30: the possessive suffix
                // was the only gendered thing in it, and a progress line does
                // not need to say whose colours these are.
                ["capture.colours"] = "جارٍ نقل الألوان…",
                ["capture.cancelled"] = "لا عجلة. الاختيار متاح في أيّ وقت.",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped
                // (MeetYourCatScreen.cs:78-81).
                // "ها هي ذي" / "ما اسمها؟" / "هذه هي" until 2026-08-30 — three
                // strings, and every one of them female: the presentative ها هي
                // ذي, the possessive in اسمها, the demonstrative هذه. They are
                // the first three things an Arabic player reads about the
                // animal she has just photographed.
                //
                // Arabic has no "this little one" to reach for the way Japanese
                // has この子, so none of the three is a translation of the old
                // string — each sidesteps the sentence that forced the gender:
                //
                //  - أوّل لقاء, "a first meeting". A verbless noun phrase, which
                //    is the one construction in Arabic with nothing in it to
                //    agree. It names the occasion rather than the cat;
                //  - هكذا تمامًا, "exactly so". هكذا is invariable, and the
                //    button now confirms the likeness instead of the animal —
                //    which is what the player is actually being asked;
                //  - ما الاسم؟ — see below.
                ["meet.title"] = "أوّل لقاء",
                // **The highest-risk string in this table, and it is being
                // shipped anyway.** ما الاسم؟ is grammatical and it is what
                // every sibling table now does — Japanese お名前は？, Korean
                // 이름이 뭐예요?, Chinese 叫什么名字？, Russian «Как зовут?» — but
                // it is not ATTESTED as shipped Arabic UI copy, and the phrase
                // that is attested is the bare label الاسم, which is precisely
                // the vet-clinic register this whole pass exists to avoid.
                // Keeping ما اسمها؟ was not an option: that feminine possessive
                // is the defect itself. **Wants a native reader before launch.**
                ["meet.name_placeholder"] = "ما الاسم؟",
                ["meet.confirm"] = "هكذا تمامًا",
                // مشمش — apricot, and the name that turns up first on every
                // list of what Arabs actually call their cats, from Cairo to
                // the Gulf. Ordinary, affectionate, no exclamation in it, and
                // it takes no grammatical gender from the player, which is the
                // constraint this whole table is written around.
                //
                // It is a food name and not a coat name, and that distinction
                // is doing work: Turkish "Pamuk" and Indonesian "Oyen" were
                // both rejected in the Latin file for naming a colour this
                // kitten's coat may not be. Arabic cat names are food names as
                // a class — سمسم, لوزة, سكر, عنبر — so مشمش is heard the way
                // "Peaches" is in English, as a fond name, not as a claim about
                // the animal's fur.
                //
                // **It leans male, and that was checked rather than assumed on
                // 2026-08-30.** The Arabic name lists that were consulted file
                // مشمش under أسماء قطط ذكور and give مشمشة as the female form.
                // Note which way that cuts: it is the mirror of the defect this
                // pass removed, not another instance of it, and it is much the
                // mildest thing in the file — a name, which real cats have
                // whatever their sex, overwritten by the first thing the player
                // types, and food names cross sexes freely in Arabic anyway.
                // Confidence medium: this rests on name-list articles, not on a
                // corpus count. Left as it stands.
                //
                // Not the colloquial بسّة or بسبس, which are the literal
                // register match — بسّة is "pussycat" and بس بس is the call. The
                // table is Modern Standard by its class note, they are dialect,
                // and مشمش is the one that crosses every dialect unchanged.
                //
                // **Right-to-left, and this is the one string on the screen
                // where the unconfigured bidi engine is worse than usual.** The
                // class note above records the whole risk: `PanelSettings.asset`
                // has no ICU data and no text settings, so nothing reorders or
                // shapes Arabic, and the expected symptom is disconnected
                // letters running the wrong way. A name in an editable
                // `TextField` adds a caret and a selection to that, and neither
                // has been seen on a device. Two things were done about it here
                // rather than nothing: the value is four letters, the shortest
                // it can be while still being a real name, and it is pure
                // Arabic with no Latin run inside it — so it is a single
                // right-to-left run with no direction change for the missing
                // algorithm to get wrong, unlike `card.caption`, which embeds
                // "Sootpaw". The value is written in logical order with no
                // manual reversal and no presentation forms, exactly as the
                // rest of this table is: the day a shaper is switched on it is
                // right, and until then the fix is `PanelSettings.asset` and
                // not this file.
                ["cat.default_name"] = "مشمش",

                // --- the four outcomes ---------------------------------------
                // **All four rewritten 2026-09-01, and the two constraints this
                // table already had both survive it.** All four are still
                // STATED and not commanded — no imperative, so no gender is
                // forced on the player — and none of them makes anything agree
                // with the photographed cat. What changed is that the screen
                // stopped refusing: each line now says what we saw, then what
                // we did (على أيّ حال — "in any case"), then what a better
                // photograph would give, as a statement.
                //
                // الهرّة appears in three of the four and is the GAME's kitten,
                // which this table keeps feminine on purpose — see the class
                // note. It is not the animal in the photograph and nothing here
                // agrees with that one.
                //
                // "صورة تملؤها القطّة أكثر ستنفع." until 2026-08-30 — the verb
                // agreed with the cat and carried a feminine object suffix
                // besides. صورة أقرب, "a closer photo", says the same thing to
                // the player and agrees only with صورة; it is kept.
                ["photo.no_animal"] = "لا قطّة في هذه الصورة، وقد صنعنا الهرّة منها على أيّ حال. صورة أقرب ستنفع أكثر.",
                // "لكنّ هذا الملجأ للقطط" until 2026-09-01 — that clause WAS the
                // refusal, and there is none. The compliment stays, and the
                // dog's own colours are what the kitten takes. الألوان with no
                // possessor, as everywhere on this screen.
                ["photo.dog"] = "يبدو أنّه كلب. جميل، وستأخذ الهرّة هذه الألوان.",
                // "نقل ألوانها … وهي ساكنة؟" until 2026-08-30: a feminine
                // possessive and then a feminine circumstantial clause. الألوان
                // owns nothing. في لحظة سكون went with the question mark on
                // 2026-09-01 — it was asking for another photograph, and the
                // line no longer asks for anything.
                ["photo.unclear"] = "هناك قطّة، لكنّ الصورة ضبابية والألوان تخمين. صورة أوضح تجعل الألوان أدقّ.",
                // "Got you." — spoken TO the cat, following the English.
                //
                // "أصبحت عندنا." until 2026-08-30, which was "she has become
                // ours". Addressing the animal is what rescues this one:
                // أمسكنا is first person plural and has no gender at all, and
                // the ـك suffix is written with the same four letters for a
                // male and a female cat once the diacritics are off — which
                // they are, here as everywhere in this table.
                ["photo.accepted"] = "أمسكنا بك.",
                // "تلك الصورة مرّة أخرى؟" until 2026-08-29 — "تلك الصورة", that
                // same picture, is the one retry that cannot work. See the
                // English table. The `capture.skipped` tail —
                // والهرّة بانتظارك على أيّ حال — went on 2026-09-01: it pointed
                // at the skip button, and nothing sends her there now, so the
                // middle clause reports that the kitten was made from her photo
                // even on this path. صنعنا is first person plural, which has no
                // gender, and صورتك is the same four letters for either player.
                ["photo.our_fault"] =
                    "حدث خطأ من جهتنا. صنعنا الهرّة من صورتك على أيّ حال، " +
                    "وقد تنفع صورة أخرى أكثر.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added: EveningReminder.cs:52
                // reads this through Copy.Of(key) with no arguments, so a "{0}"
                // here reaches a lock screen as four literal characters.
                //
                // These two are the strings most likely to look RIGHT on a
                // device, and that is not a contradiction of the warning above:
                // a notification is drawn by iOS and Android, both of which
                // shape and reorder Arabic properly. If the lock screen reads
                // correctly and the game's own cards do not, that is the
                // clearest possible evidence that the fault is UI Toolkit's
                // text engine and not this table.
                ["notification.title"] = "وجدت هرّتك شيئًا خلف الأريكة",
                ["notification.body"] = "تنتظر لتريك، متى ما توفّرت لديك دقيقة.",

                // Android only, and shown in system Settings rather than in
                // the game. The description avoids تلعبين — the feminine "you
                // play" — with an impersonal clause instead.
                ["notification.channel"] = "تذكير المساء",
                ["notification.channel_description"] =
                    "رسالة واحدة هادئة في المساء، في الأيّام التي تمرّ دون لعب.",
            };

        /// <summary>
        /// Hindi.
        ///
        /// **आप, not तुम, and not तू.** Hindi's second person is a three-step
        /// ladder and every string that addresses the player has to pick a
        /// rung. तू is intimate to the point of rudeness between strangers;
        /// तुम is what a mobile game uses and what an older relative uses to a
        /// child, and using it to a woman of forty-five is a small rudeness
        /// repeated on every screen. आप is the ordinary respectful form
        /// between adults — the same call the Russian table made with вы, and
        /// for the same audience. Verbs follow it: दिखाइए, चुनिए, लीजिए, not
        /// दिखाओ, चुनो, लो.
        ///
        /// **नन्ही बिल्ली, not बिल्ली का बच्चा.** The dictionary phrase for a
        /// kitten is बिल्ली का बच्चा — "a cat's child" — which is three words,
        /// grammatically masculine, and reads like a caption in a school
        /// textbook. नन्ही बिल्ली is "little cat", and it is the ordinary way
        /// to say it.
        ///
        /// **This table was CHECKED on 2026-08-30 for the gendering defect and
        /// deliberately left unchanged. Do not re-open it from scratch.** The
        /// note here used to justify itself like this: नन्ही बिल्ली "is
        /// feminine, and so carries the English's deliberate 'she'
        /// (12-copy-english change 7) without any extra work." **That sentence
        /// was wrong, and it was wrong in a way worth recording**, because the
        /// English "she" turned out to be the defect — the cat is built from a
        /// photograph of the player's own and about half of pet cats are male —
        /// and every other table in this file had to be unpicked because of it.
        ///
        /// Hindi had nothing to unpick, and not for the reason the old note
        /// gave. It does not "carry" the she. Hindi's third-person pronouns are
        /// sexless — वह and इसका say nothing about the animal, which is why
        /// `meet.title`, `meet.name_placeholder`, `meet.confirm` and
        /// `photo.accepted` needed no work at all. What is left is verb and
        /// adjective agreement, and agreement in Hindi follows the NOUN, not
        /// the animal: बिल्ली is the unmarked species word — Platts glosses it
        /// "female cat, cat (in general)" — and Indian journalism writes नर
        /// बिल्लियां … चलाती हैं, feminine agreement on cats the sentence has
        /// just called male. So दिख रही, बैठी and आ गई are a bill the noun
        /// presents; they are not the game asserting a sex.
        ///
        /// **Confidence: medium, and stated as medium on purpose.** The
        /// counter-evidence is real: बिल्ली is absent from the नित्य स्त्रीलिंग
        /// lists (मक्खी, कोयल, तितली — the nouns with no masculine partner)
        /// precisely because बिल्ला/बिलाव exists, so the pairing has not fully
        /// bleached out the way it has in English "dog". The reading is
        /// therefore "mostly concord", not "purely concord". It was still left
        /// alone, on the rule this whole pass ran on: **a stilted string is
        /// worse than a grammatically gendered one**, the alternatives all
        /// force a नर/मादा prefix that would pick a sex outright, and neither
        /// the writer nor the reviewer of this table reads Hindi.
        ///
        /// The one place Hindi does CHOOSE rather than agree is
        /// `cat.default_name`, and that is flagged at the key.
        ///
        /// No exclamation mark anywhere, for the same reason as the other six.
        ///
        /// **Nukta warning for the font build.** Words like साफ़, फ़ोटो, ज़्यादा
        /// and नक़्शा are written here with the base consonant followed by the
        /// combining nukta U+093C rather than with the precomposed codepoints
        /// (U+095E and friends). glyphs.txt therefore lists U+093C on its own
        /// line, and a font that omits it will show these words with a missing
        /// mark or a broken cluster rather than with an empty box — a failure
        /// that is easy to miss in a screenshot.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Hindi =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                // Devanagari runs a little over half an em, so .game__card-title
                // at fontSize 22, unwrapped, in 366 units less 48 of card
                // padding takes about twenty-six characters. The longest title
                // here is fifteen.
                ["win.room_clean.title"] = "कमरा साफ़ हो गया",
                ["win.room_clean.body"] = "नन्ही बिल्ली को यहाँ अब ज़्यादा अच्छा लग रहा है।",
                // "हट गया", not "समेट दिया": the second credits the player for
                // the tap, which is the thing the English pass deleted when it
                // dropped "Corner cleared!".
                ["win.corner.title"] = "ढेर हट गया",
                ["win.corner.body"] = "नन्ही बिल्ली देखने आ गई।",
                ["win.next"] = "आगे",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "वापस",
                ["card.share_short"] = "साझा करें",
                // {0} is the game's name, early: Hindi puts the place before
                // the thing with में, and a name buried at the end of a Hindi
                // sentence sits after the verb, where nobody skimming a feed
                // will look.
                ["card.caption"] = "देखिए, {0} में मेरी नन्ही बिल्ली",
                ["map.opening"] = "कमरा खुल रहा है…",
                // .game__ba-label is fontSize 12 under a 116-unit pane, which
                // is about seventeen Devanagari characters. These are four and
                // three.
                ["win.before"] = "पहले",
                ["win.after"] = "बाद",

                // --- losing a pile -------------------------------------------
                // ताक, not the loanword शेल्फ़: the game already has one
                // English word in it (its own name) and does not need a second.
                ["lose.title"] = "ताक भर गया",
                // The rule first, then that nothing is lost.
                //
                // No placeholder, exactly as in English: DebugGameView.Finish
                // still passes _levelIndex and string.Format still ignores it.
                ["lose.body"] =
                    "सारी जगहें भर गई हैं और तीन एक जैसे नहीं हैं। " +
                    "ढेर जैसा था वैसा ही लौट आएगा।",
                ["lose.replay"] = "फिर से",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "हर कमरा साफ़ है",
                ["house.complete.body"] =
                    "पूरे बारह, और एक नन्ही बिल्ली जिसके पास अब अपनी चीज़ें छिपाने की जगह नहीं बची।" +
                    "\n\nघर फ़िलहाल यहीं तक जाता है।",
                // A different verb from card.share_short, as in English.
                ["house.complete.share"] = "किसी को दिखाइए",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "{0} में हर कमरा साफ़ है।",

                // --- the house map -------------------------------------------
                ["map.title"] = "आपका घर",
                // fontSize 10, wrapping inside 92% of the width — the smallest
                // type on the screen. टैप stays as a loanword: it is what
                // every Hindi phone interface prints, and a native coinage
                // would be clearer to nobody.
                ["map.legend"] =
                    "जो नंबर जल रहा है उस पर टैप करें   ·   निशान लगे कमरे हो चुके   " +
                    "·   धुँधले कमरे अभी बंद हैं",
                ["map.no_levels"] = "स्तर लोड नहीं हुए — नक़्शे में डालने को कुछ नहीं",
                // {0} is the system's own reason and arrives in the system
                // language, not this table's.
                ["map.room_failed"] = "कमरा नहीं खुल सका: {0}",
                ["map.map_failed"] = "नक़्शा नहीं खुल सका: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "कुछ कमी है",
                // One instruction, and it is the one that can work.
                ["levels.unavailable.body"] =
                    "कमरे लोड नहीं हो सके। कृपया गेम फिर से इंस्टॉल करें।",

                // --- the photo screen ----------------------------------------
                // fontSize 26, unwrapped, 342 units (CaptureScreen.cs:86-89) —
                // about twenty-four Devanagari characters. This is eighteen.
                // दिखाइए and not दिखाओ: the आप form, on the first screen the
                // player sees.
                ["capture.title"] = "अपनी बिल्ली दिखाइए",
                // The reason first, the framing advice second — the order the
                // English pass settled on, and the advice is what keeps
                // Vision's rejection rate down. रंग is the plain word for a
                // coat's colour; there is no need for a specialist term.
                ["capture.hint"] =
                    "गेम की नन्ही बिल्ली को इसका रंग मिलेगा। " +
                    "हो सके तो इसे पूरे फ़्रेम में रखिए।",
                ["capture.camera"] = "फ़ोटो लीजिए",
                ["capture.gallery"] = "अपनी कोई फ़ोटो चुनिए",
                // In the player's voice, like the English: a person saying
                // what she wants, not asking a favour.
                ["capture.skip"] = "अभी नहीं — मुझे बिल्ली चाहिए",
                ["capture.skipped"] = "नन्ही बिल्ली वैसे भी आपका इंतज़ार कर रही है।",
                ["capture.opening"] = "खुल रहा है…",
                ["capture.looking"] = "देख रहे हैं…",
                ["capture.colours"] = "इसका रंग उतारा जा रहा है…",
                ["capture.cancelled"] = "कोई जल्दी नहीं। जब मन हो तब चुन लीजिए।",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped
                // (MeetYourCatScreen.cs:78-81), and this one has room to spare.
                ["meet.title"] = "यह रही वह",
                ["meet.name_placeholder"] = "इसका नाम क्या है?",
                ["meet.confirm"] = "यही है",
                // **Hindi has no conventional default cat name, and this is
                // the least confident value in all seventeen tables.** Said
                // plainly because the brief asked for it to be. Japanese keeps
                // タマ and Korean keeps 나비 for a cat nobody has named; Hindi
                // keeps nothing. Cats are far less often kept as named
                // housepets in the Hindi belt than dogs are, urban owners who
                // do keep them very largely give English names, and the Hindi
                // Wikipedia article on बिल्ली reaches for the English "pussy
                // cat" as the familiar name for one. There was no convention to
                // find and none is claimed.
                //
                // What is here instead: मिनी. An ordinary, short Indian pet
                // name — the register of a name a real person writes rather
                // than a dictionary word — which asserts no coat colour, the
                // objection that removed the strongest candidates in Turkish,
                // Indonesian, Vietnamese and Thai.
                //
                // **It reads as a girl's name, and after the 2026-08-30 pass
                // that is a knowing trade rather than an unnoticed one.** It
                // used to be defended here as agreeing "with the नन्ही बिल्ली
                // this table chose to carry the English 'she'", and both halves
                // of that have since fallen: the English no longer says "she"
                // of the photographed cat, and बिल्ली's feminine agreement is
                // concord rather than a claim (see the class note). So this key
                // is now the ONE place in the Hindi table where the game picks
                // a sex instead of inheriting one from grammar. मिनी is filed
                // as a girl's name by the Indian baby-name sites, and a Hindi
                // reader met a girl called मिनी twice in primary school — in
                // Tagore's Kabuliwala and in the Class-2 reader नन्ही सुनहरी,
                // where she rescues a kitten.
                //
                // It was kept anyway, on evidence rather than inertia: Hindi
                // pet-name morphology is -ू for toms and -ी for queens, the
                // Indian name guides split their desi cat lists into male and
                // female with **no unisex section at all**, and every candidate
                // checked lands on one list or the other. There is no warm,
                // ordinary, sexless Hindi cat name to move to; the choice was
                // between a female name and a male one.
                //
                // Two things make it much milder than the pronouns that were
                // removed elsewhere: it is a NAME, which real cats have
                // regardless of sex, and it is overwritten by the first thing
                // the player types. The alternative that makes no claim at all
                // is the empty string — `meet.name_placeholder` already stands
                // in the empty field and does the work — and that was not taken
                // here because it needs a check of every path that prints a
                // name (`MeetYourCatScreen`, the card, the share caption) and
                // this pass was a copy pass. **It is the change to make if the
                // owner wants the Hindi table to assert nothing.**
                //
                // Not बिल्ली, the common noun, for the reason set out at the
                // German entry: in a field asking इसका नाम क्या है? a bare
                // "cat" reads as an unanswered question. Not चीकू, which is
                // genuinely common on Indian pets and is a sapodilla — a food
                // name, so the Arabic argument would allow it, but it goes to
                // dogs at least as often and reads as a child's pet.
                //
                // **This is the value to hand to a native reader first** if the
                // game ever gets one, ahead of the Traditional Chinese table
                // the class note already names.
                //
                // No nukta and no conjunct: four plain Devanagari codepoints,
                // so unlike साफ़ and नक़्शा this one cannot break on a font that
                // omits U+093C.
                ["cat.default_name"] = "मिनी",

                // --- the four outcomes ---------------------------------------
                // **All four rewritten 2026-09-01.** They were written when the
                // screen could REFUSE: each ended the run, so each instructed a
                // retry — लीजिए, the आप imperative, on a screen that had just
                // taken the photograph away. Nothing refuses now, and the bar
                // under the line reads इसका रंग उतारा जा रहा है… as it is read.
                //
                // Each now says what we saw, then what we did (फिर भी, the
                // ordinary concessive), then what a better photograph WOULD
                // have given — the counterfactual आता/होता, which offers
                // without instructing. **No imperative in any of the four**,
                // which is a change: the आप-form verbs the class note requires
                // are still everywhere else in this table, but there is nothing
                // left here for the player to be told to do.
                //
                // Agreement is unchanged and is still concord with बिल्ली, not
                // an assertion about the animal — see the class note, which
                // records that reading as medium confidence.
                ["photo.no_animal"] = "इसमें बिल्ली नहीं दिख रही, फिर भी हमने इसी से बनाया। किसी और फ़ोटो से रंग और सटीक आता।",
                // "पर यह आश्रय बिल्लियों के लिए है" until 2026-09-01 — that
                // clause WAS the refusal, and there is none. प्यारा है stays,
                // and the dog's own रंग is what the kitten takes.
                ["photo.dog"] = "यह कुत्ता लग रहा है। प्यारा है — नन्ही बिल्ली को यही रंग मिलेगा।",
                ["photo.unclear"] = "बिल्ली तो है, पर धुँधली — रंग अंदाज़े से लिया है। साफ़ फ़ोटो से और पक्का होता।",
                // "Got her." — she is with us now.
                ["photo.accepted"] = "वह अब हमारे पास है।",
                // "वही फ़ोटो एक बार और?" until 2026-08-29 — "वही फ़ोटो", that very
                // photo, is the one retry that cannot work. See the English
                // table. The `capture.skipped` tail — नन्ही बिल्ली वैसे भी आपका
                // इंतज़ार कर रही है — went on 2026-09-01: it pointed at the skip
                // button below, and nothing sends her there now, so the middle
                // clause reports that the kitten was made from her photo here
                // too.
                ["photo.our_fault"] =
                    "हमारी तरफ़ कुछ गड़बड़ हो गई। फिर भी आपकी फ़ोटो से नन्ही बिल्ली बना दी। " +
                    "कोई दूसरी फ़ोटो शायद बेहतर आए।",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added: EveningReminder.cs:52
                // reads this through Copy.Of(key) with no arguments, so a "{0}"
                // here reaches a lock screen as four literal characters.
                ["notification.title"] = "आपकी नन्ही बिल्ली को सोफ़े के पीछे कुछ मिला",
                ["notification.body"] = "दिखाने के लिए इंतज़ार कर रही है, जब आपके पास एक मिनट हो।",

                // Android only, and shown in system Settings rather than in
                // the game.
                ["notification.channel"] = "शाम की सूचना",
                ["notification.channel_description"] =
                    "शाम को एक शांत संदेश, उन दिनों जब आपने खेला न हो।",
            };
    }
}
