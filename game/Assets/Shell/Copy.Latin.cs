using System.Collections.Generic;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// The eight Latin-script languages, 2026-08-29: Spanish, Portuguese,
    /// French, German, Italian, Turkish, Indonesian, Vietnamese.
    ///
    /// Written against `Copy.cs`, not against a glossary. Every value here was
    /// read on the card, button or label it lands on, and the narrow ones are
    /// called out at the key where a longer word would wrap or clip. The
    /// English table's own comments record why each string says what it says;
    /// none of those decisions is reversed here.
    ///
    /// **Variants.** Unity has one <c>SystemLanguage.Spanish</c> and one
    /// <c>SystemLanguage.Portuguese</c> — no Iberia/LatAm and no Portugal/Brazil
    /// split — so each table has to serve both halves from one text. Both are
    /// written for the larger mobile audience: **Latin American Spanish**
    /// (es-419) and **Brazilian Portuguese** (pt-BR). Where the two halves of a
    /// language disagree, the wording chosen is the one that is at worst
    /// slightly foreign in the smaller market rather than wrong in the larger
    /// one; the individual cases are commented at their keys.
    ///
    /// **Three decisions that hold across all eight tables**, so they are not
    /// argued again at each key — the full reasoning is in
    /// `tasks/60-shell-build/16-localisation-ready/NOTES-latin.md`:
    ///
    ///  - **Form of address is chosen per language and stated in that table's
    ///    comment.** English hides the choice; seven of these eight force it.
    ///    The rule behind every one of them is the same — the audience is women
    ///    30-55 (`cat-shelter-mvp.md` section 2) and the game does not shout,
    ///    congratulate or rush — but the register that *sounds* like that is a
    ///    different one in each language, and copying Russian's "вы" into all
    ///    eight would be wrong in at least five.
    ///  - **The GAME'S kitten stays "she" wherever the language can carry it.**
    ///    This is `12-copy-english` change 7, which Russian had to give up
    ///    because "котёнок" is masculine. Six of these eight can keep it —
    ///    Spanish, Portuguese, French, German and Italian all have a feminine
    ///    word for a young cat that is ordinary and not baby-talk, so they use
    ///    it. Turkish, Indonesian and Vietnamese have no grammatical gender at
    ///    all and simply say "the kitten", which reads as neither.
    ///  - **The PLAYER'S OWN cat has no sex, from 2026-08-30.** Until then the
    ///    rule above made no distinction, and `capture.*`, `meet.*` and
    ///    `photo.*` called the photographed animal female in all five gendered
    ///    tables. Roughly half of pet cats are male, so on the screens whose
    ///    whole job is to say "this is YOUR cat" the copy told half the owners
    ///    it was not. Fixed the way Japanese and Korean had it from the start
    ///    — 「この子です」, 이 아이예요, "this little one" — and the way `Copy.cs`
    ///    fixed English and Russian: the generic noun ("un gato", "un chat",
    ///    "eine Katze") where the sentence needs a noun, a restructure that
    ///    names no animal where it does not. Not one of these sentences was
    ///    ever about the animal's sex, so none of them lost anything.
    ///  - **`card.game_name` is "Sootpaw" in every table**, exactly as in
    ///    Russian: it is the name the game is listed under, an app name is not
    ///    copy, and a caption naming something no store search finds sends
    ///    nobody anywhere. It is the one key
    ///    `test_no_value_was_left_untranslated` allows to be identical.
    ///
    /// The other proper nouns follow the Russian table's lead too. There are
    /// none: the game names no character (the kitten is named by the player)
    /// and no place, so "Sootpaw" is the whole list.
    /// </summary>
    public static partial class Copy
    {
        static partial void AddLatinScript(
            Dictionary<SystemLanguage, IReadOnlyDictionary<string, string>> tables)
        {
            tables[SystemLanguage.Spanish] = Spanish;
            tables[SystemLanguage.Portuguese] = Portuguese;
            tables[SystemLanguage.French] = French;
            tables[SystemLanguage.German] = German;
            tables[SystemLanguage.Italian] = Italian;
            tables[SystemLanguage.Turkish] = Turkish;
            tables[SystemLanguage.Indonesian] = Indonesian;
            tables[SystemLanguage.Vietnamese] = Vietnamese;
        }

        /// <summary>
        /// Spanish, written as **Latin American Spanish**: Mexico, Colombia and
        /// Argentina are together several times the mobile audience of Spain,
        /// and Unity gives us one table for both.
        ///
        /// **Address: "tú".** Not the Russian table's formality, and the
        /// difference is in the languages, not in the decision. Russian "ты"
        /// from an app to a woman of 45 claims a familiarity she did not grant;
        /// Spanish "tú" does not — it is what Apple's and Google's own Spanish
        /// interfaces use, and "usted" in a game about a kitten reads like a
        /// bank. Two further reasons it is not close: "Su casa" for `map.title`
        /// is ambiguous with *his/her* house, and "Tu casa" is not; and most of
        /// this table addresses nobody at all, which is the quieter option and
        /// is taken wherever it reads naturally, so the choice actually shows
        /// in seven strings.
        ///
        /// **Not "vos".** Argentina says "mirá" and "tenés", and a table written
        /// in vos is wrong everywhere else; tú forms are read without friction
        /// in Buenos Aires, so tú is the form that is at worst slightly foreign
        /// in one market rather than wrong in the rest.
        ///
        /// **"la gatita".** The diminutive of "gata" is the ordinary Spanish
        /// word for a kitten and carries the feminine the English spent a
        /// change getting right — no baby-talk problem, so the pronoun Russian
        /// had to drop is kept here. **For the player's own cat it is "un
        /// gato"**, the generic, from 2026-08-30: see the class note. Spanish
        /// makes this cheap, because most of these sentences drop the subject
        /// anyway ("¿Cómo se llama?", "donde salga más grande") and never had
        /// to say a sex in the first place.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Spanish =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                // Names what the two photographs above it show, quietly, like
                // the English and the Russian. 25 characters at `.game__card-title`'s
                // 22px bold, which does not wrap (DebugGame.uss:412-417) — the
                // card grows to fit and the overlay is 390 units wide less the
                // card's 48 of padding, so this is inside 342 with room.
                ["win.room_clean.title"] = "La habitación está limpia",
                ["win.room_clean.body"] = "A la gatita ya le gusta más.",
                // "Montón", one noun for the object every time — the board no
                // longer names it anywhere (DebugGameView.RenderHeader), so
                // this card is the only place the player learns the word.
                ["win.corner.title"] = "Montón despejado",
                ["win.corner.body"] = "La gatita se acercó a mirar.",
                // "Seguir", not "Siguiente": one syllable shorter and it is
                // what a person says, where "Siguiente" is what a form says.
                ["win.next"] = "Seguir",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Volver",
                ["card.share_short"] = "Compartir",
                // {0} is the game's name. Addressed in the plural ("miren"),
                // which is a feed and not a person — and which sidesteps the
                // tú/vos split entirely, since Latin America has no "vosotros"
                // and "ustedes" is the one plural everybody uses.
                ["card.caption"] = "Miren la gatita que tengo en {0}",
                ["map.opening"] = "Abriendo la habitación…",
                // Under a 116px pane at 12px bold (`.game__ba-label`).
                ["win.before"] = "Antes",
                ["win.after"] = "Después",

                // --- losing a pile -------------------------------------------
                // The shelves, plural — there are three, and the body says so.
                ["lose.title"] = "Estantes llenos",
                // The rule, then that nothing is lost, in that order — the job
                // the English does (12-copy-english change 3). 84 characters
                // against the English 76, inside `.game__card-body`'s 240px at
                // 15px, so it wraps to one line more than the English on the
                // narrowest phone. Accepted rather than cut further: the rule
                // is the only place the game ever states how matching works.
                ["lose.body"] =
                    "Todas las casillas están ocupadas y no hay tres iguales. " +
                    "El montón queda como estaba.",
                ["lose.replay"] = "Otra vez",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "Toda la casa está limpia",
                ["house.complete.body"] =
                    "Las doce, y una gatita que ya no tiene dónde esconder " +
                    "lo que encuentra." +
                    "\n\nHasta aquí llega la casa, por ahora.",
                // A different verb from card.share_short, as in English and
                // Russian. "Mostrar a alguien" is 17 characters at
                // Buttons.LabelSize 17, beside the 44px heart inside 342
                // available (DebugGameView.BuildEndingExtras) — shorter than
                // the Russian that fitted on the same arithmetic. The literal
                // "Mostrárselo a alguien" is 21 and buys nothing.
                ["house.complete.share"] = "Mostrar a alguien",
                // {0} is the game's name, same as card.caption.
                ["house.complete.caption"] = "En {0} ya está todo limpio.",

                // --- the house map -------------------------------------------
                ["map.title"] = "Tu casa",
                ["map.legend"] =
                    "toca el número claro para jugar   ·   " +
                    "las habitaciones marcadas están listas   ·   " +
                    "las oscuras siguen cerradas",
                ["map.no_levels"] = "no se cargaron las habitaciones — no hay nada que mostrar",
                // {0} is the system's own reason and arrives in the system
                // language, not this table's. Kept for the same reason English
                // keeps it: a reported sentence beats a reported blank screen.
                ["map.room_failed"] = "no se pudo abrir la habitación: {0}",
                ["map.map_failed"] = "no se pudo abrir el mapa: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Falta algo",
                // One instruction, and it is the one that can work — no "try
                // again later", for the reason the English gives.
                ["levels.unavailable.body"] =
                    "No se pudieron cargar las habitaciones. " +
                    "Vuelve a instalar el juego.",

                // --- the photo screen ----------------------------------------
                // CANNOT WRAP. CaptureScreen.cs:86-89 builds it at fontSize 26
                // and never sets whiteSpace = Normal; the panel is 390 units
                // (PanelSettings.asset, m_Match 1, so 390 is the narrow end)
                // less 48 of padding. 18 characters, inside the ~24 the Russian
                // pass measured as the ceiling.
                //
                // "Muéstranos tu gata" until 2026-08-30 — a female cat, asked
                // of every player. "Gato" is the generic and costs one
                // character less.
                ["capture.title"] = "Muéstranos tu gato",
                // The reason first, the framing advice second — the order the
                // English pass settled on, and the advice is what keeps
                // Vision's rejection rate down. "Pelaje" is the word a Spanish-
                // speaking cat owner uses for a cat's coat.
                ["capture.hint"] =
                    "La gatita del juego tendrá su mismo pelaje. " +
                    "Que ocupe todo el cuadro, si se puede.",
                ["capture.camera"] = "Tomar una foto",
                ["capture.gallery"] = "Elegir una que tengo",
                // In the player's voice, like the English and the Russian: a
                // person saying what she wants, not asking a favour.
                ["capture.skip"] = "Ahora no — quiero una gatita",
                ["capture.skipped"] = "Una gatita te espera igual.",
                ["capture.opening"] = "Abriendo…",
                ["capture.looking"] = "Mirando…",
                ["capture.colours"] = "Copiando su pelaje…",
                ["capture.cancelled"] = "No hay prisa. Elige una cuando quieras.",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped (MeetYourCatScreen.cs:78),
                // and this one has room to spare.
                // "Aquí está" and "¿Cómo se llama?" already named no sex and
                // are untouched. "Es ella" did, and became "Así es" on
                // 2026-08-30 — the same move as Russian's "Так и есть": what
                // the player confirms is a likeness, and agreeing with a
                // likeness is what the phrase is for.
                ["meet.title"] = "Aquí está",
                ["meet.name_placeholder"] = "¿Cómo se llama?",
                ["meet.confirm"] = "Así es",
                // "Michi" is what a Latin American calls a cat when she is
                // being fond of it, and it is also what she writes on the
                // name tag — the same double life "Kitty" has in English, and
                // the reason it is the right answer rather than a literal
                // "Gatita". Kept on 2026-08-30 while the female names in the
                // other tables went, because it takes no sex at all — "el
                // michi" and "la michi" are both said — and this key is shown
                // as the player's own cat's name (MeetYourCatScreen.cs:79).
                // The line that stood here claimed it "carries the feminine
                // this table already chose with la gatita"; it does not.
                //
                // The es-419 / Spain split shows here and is decided the way
                // the class note says: Spain's own generic is "misi" or
                // "minino", but "michi" travelled out of Mexico through the
                // internet and is read without friction in Madrid, while
                // "minino" in Guadalajara would read as a book word. At worst
                // slightly foreign in the smaller market, not wrong in the
                // larger one.
                ["cat.default_name"] = "Michi",

                // --- the four outcomes ---------------------------------------
                // Each says what happened and then offers a way forward, in
                // that order, and none of them blames the player.
                // "gata" → "gato" throughout on 2026-08-30: the generic, which
                // is what these three sentences always meant. "Con ella quieta"
                // had no neutral form to fall back on, so it is "sin que se
                // mueva" — no animal named, and one character shorter.
                //
                // REWRITTEN 2026-09-01, and the reason is in `Copy.cs` above
                // `photo.no_animal`: nothing ends the run any more. All four of
                // these instructed a retry ("Prueba con una foto…", "¿Otra, sin
                // que se mueva?") because a retry used to be the only way
                // forward; now the cat is being built while the line is read and
                // the bar underneath says "Copiando su pelaje…". Each one says
                // what we saw, then what we did, and offers the better
                // photograph as a choice. "Igual" for "anyway" throughout — the
                // word `capture.skipped` already uses on this screen.
                ["photo.no_animal"] = "Aquí no hay ningún gato — igual usamos la foto. Con otra saldría mejor.",
                // "Precioso" agrees with "perro" — a compliment to the dog. It
                // was written so that a refusal would not be a rebuke; there is
                // no refusal now, and it stays because being kind about
                // somebody's dog costs nothing. The second half is
                // `capture.hint` word for word ("tendrá su mismo pelaje"),
                // which is the promise this screen already made.
                ["photo.dog"] = "Parece un perro. Precioso — la gatita tendrá su mismo pelaje.",
                // "A ojo" is what a Spanish speaker says for a measurement made
                // by eye, which is exactly what the colour is on a blurred
                // photograph.
                ["photo.unclear"] = "Hay un gato, pero está borroso — copiamos el pelaje a ojo. Con una foto más nítida saldría más fiel.",
                // Not "Listo", which is the register of a progress bar
                // finishing. This one needed no change — "está" carries no
                // sex — and the English it was written against has since
                // become "Got you."
                ["photo.accepted"] = "Ya está con nosotros.",
                // "¿Lo intentas otra vez?" until 2026-08-29: the one
                // instruction on the screen that could only fail again. What
                // replaced it — "Otra foto puede funcionar, y una gatita te
                // espera igual", `capture.skipped` word for word — was true
                // while a failure here still sent the player back to the
                // buttons empty-handed. Since 2026-09-01 it does not: the
                // kitten is made from her photo even when our side fails, so
                // the line says that instead of promising a kitten she is
                // already watching being built.
                ["photo.our_fault"] =
                    "Algo falló de nuestro lado. Igual hicimos la gatita con " +
                    "tu foto — con otra podría salir mejor.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER, and none may be added: EveningReminder.cs:52
                // reads this through `Copy.Of(key)` with no arguments, so a
                // "{0}" here reaches a lock screen as four literal characters.
                ["notification.title"] = "Tu gatita encontró algo detrás del sofá",
                ["notification.body"] = "Te espera para mostrártelo, cuando tengas un minuto.",

                // Android only, and shown in system Settings rather than in
                // the game.
                ["notification.channel"] = "Recordatorio de la tarde",
                ["notification.channel_description"] =
                    "Un mensaje tranquilo por la tarde, los días que no jugaste.",
            };

        /// <summary>
        /// Portuguese, written as **Brazilian Portuguese**: Brazil is by a wide
        /// margin the larger mobile audience, and Unity gives us one table for
        /// Brazil and Portugal both.
        ///
        /// **Address: "você".** In Brazil this is not the informal half of a
        /// pair — it is the neutral register, the one a stranger uses to a
        /// stranger, and it carries none of the familiarity Russian "ты" would.
        /// The genuinely formal Brazilian forms ("a senhora") would read as
        /// addressing an elderly customer, which is the opposite of the tone.
        /// European "tu" is regional in Brazil and is not used here.
        ///
        /// **"cômodo", not "quarto" or "sala".** The house has twelve rooms of
        /// different kinds; "quarto" is a bedroom and "sala" a living room, and
        /// either would be wrong eleven times out of twelve. "Cômodo" is the
        /// generic, and it is the word that makes `map.legend` and
        /// `house.complete.title` agree with the card.
        ///
        /// **"a gatinha"** — feminine and ordinary, so the English "she" is
        /// kept rather than dropped, for the game's own kitten. **The player's
        /// cat is "um gato"** from 2026-08-30, and "dela" is gone from
        /// `capture.*` and `photo.*`: see the class note.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Portuguese =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "O cômodo está limpo",
                ["win.room_clean.body"] = "A gatinha já gostou mais daqui.",
                ["win.corner.title"] = "Pilha arrumada",
                ["win.corner.body"] = "A gatinha veio ver.",
                ["win.next"] = "Seguir",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Voltar",
                ["card.share_short"] = "Compartilhar",
                // {0} is the game's name. "em {0}" and not "no {0}": the name
                // is invented and takes no article, and a contraction would
                // assign it a gender Portuguese has no way to know.
                ["card.caption"] = "Olhem a gatinha que eu tenho em {0}",
                ["map.opening"] = "Abrindo o cômodo…",
                ["win.before"] = "Antes",
                ["win.after"] = "Depois",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "Prateleiras cheias",
                // 79 characters against the English 76 — the same three lines
                // in `.game__card-body`'s 240px. "Espaços" rather than
                // "compartimentos", which is correct and four characters
                // longer for nothing.
                ["lose.body"] =
                    "Todos os espaços estão ocupados e não há três iguais. " +
                    "A pilha volta como estava.",
                ["lose.replay"] = "De novo",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "A casa toda está limpa",
                // Two sentences where the English has one clause and a comma,
                // as in Russian and for the same reason: the relative clause
                // costs a line this card cannot spare under two photographs.
                ["house.complete.body"] =
                    "Todos os doze. E a gatinha não tem mais onde esconder " +
                    "o que acha." +
                    "\n\nA casa vai até aqui, por enquanto.",
                // 19 characters at Buttons.LabelSize 17, beside the 44px heart
                // inside 342 available — one shorter than the Russian that fits
                // on the same arithmetic.
                ["house.complete.share"] = "Mostrar para alguém",
                ["house.complete.caption"] = "Em {0} está tudo limpo.",

                // --- the house map -------------------------------------------
                ["map.title"] = "Sua casa",
                ["map.legend"] =
                    "toque no número claro para jogar   ·   " +
                    "os cômodos marcados estão prontos   ·   " +
                    "os escuros ainda estão fechados",
                ["map.no_levels"] = "os cômodos não carregaram — não há o que mostrar",
                ["map.room_failed"] = "não foi possível abrir o cômodo: {0}",
                ["map.map_failed"] = "não foi possível abrir o mapa: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Falta alguma coisa",
                ["levels.unavailable.body"] =
                    "Os cômodos não carregaram. Por favor, instale o jogo de novo.",

                // --- the photo screen ----------------------------------------
                // CANNOT WRAP — see the Spanish note. 15 characters.
                // "Mostre para nós a sua gata" is 26 and would run off the
                // screen; "para nós" adds nothing the sentence needs, the same
                // cut the Russian pass made with "нам".
                //
                // "Mostre sua gata" until 2026-08-30 — a female cat, asked of
                // every player. "Gato" is the generic and the same 15.
                ["capture.title"] = "Mostre seu gato",
                // "a cor dela" and "Deixe ela ocupar" until 2026-08-30. Both
                // said *her* about the player's cat; neither sentence was
                // about her sex. "Essa cor" points at the photo, and the
                // subjunctive drops the subject the way the Spanish line
                // beside it already did.
                ["capture.hint"] =
                    "A gatinha do jogo vai ficar com essa cor. " +
                    "Que preencha o quadro todo, se der.",
                ["capture.camera"] = "Tirar uma foto",
                ["capture.gallery"] = "Escolher uma que eu tenho",
                ["capture.skip"] = "Agora não — quero uma gatinha",
                ["capture.skipped"] = "Uma gatinha espera por você de todo jeito.",
                ["capture.opening"] = "Abrindo…",
                ["capture.looking"] = "Olhando…",
                ["capture.colours"] = "Copiando a cor…",
                ["capture.cancelled"] = "Sem pressa. Escolha quando quiser.",

                // --- meeting the cat ------------------------------------------
                // "Aqui está ela", "Qual é o nome dela?" and "É ela" until
                // 2026-08-30 — three sexes asserted on the one screen that
                // exists to say "this is YOUR cat". Portuguese drops the
                // subject freely, so the first two lose only the pronoun that
                // was carrying nothing: both speakers are looking at the same
                // animal. "É isso mesmo" confirms the likeness, which is what
                // the button was ever for.
                ["meet.title"] = "Aqui está",
                ["meet.name_placeholder"] = "Como se chama?",
                ["meet.confirm"] = "É isso mesmo",
                // "Mimi" until 2026-08-30, and its own note gave the reason it
                // had to go: "the top ten Brazilian names for a FEMALE cat".
                // MeetYourCatScreen.cs:79 shows this as the player's cat's
                // name, so a male cat was called Mimi.
                //
                // "Gatinho" is the same answer English and Russian reached —
                // "Kitty" and «Котёнок» are not names either, they are the
                // generic word standing in the field until the owner types
                // hers. Masculine in form, like «котёнок», and that is the
                // Portuguese generic rather than a claim about the animal.
                //
                // Not "Bichano", which is the closer register but reads as
                // European; not "Frajola", which is Sylvester from Looney
                // Tunes — a borrowed brand and a black-and-white cat both,
                // when this kitten's coat comes from the player's photograph.
                ["cat.default_name"] = "Gatinho",

                // --- the four outcomes ---------------------------------------
                // "gata" → "gato" throughout on 2026-08-30, the generic these
                // three sentences always meant, and the pronouns for the
                // player's cat dropped with it.
                //
                // REWRITTEN 2026-09-01 — see `Copy.cs` above `photo.no_animal`.
                // Nothing ends the run now, so "Tente uma foto…" and "Mais uma,
                // sem que se mexa?" told the player to retry while the bar under
                // them read "Copiando a cor…". What we saw, then what we did,
                // then the better photograph as a choice.
                ["photo.no_animal"] = "Não tem gato nesta — usamos assim mesmo. Com outra ficaria melhor.",
                // "Lindo" agrees with "cachorro": the compliment is to the dog.
                // It existed so that a refusal would not be a rebuke; there is
                // no refusal now and it stays anyway, because the dog's own
                // colour is what the kitten gets. The second half repeats
                // `capture.hint` ("vai ficar com essa cor").
                ["photo.dog"] = "Parece um cachorro. Lindo — a gatinha vai ficar com essa cor.",
                ["photo.unclear"] = "Tem um gato, mas está borrado — a cor é um palpite. Com uma foto nítida ficaria mais fiel.",
                ["photo.accepted"] = "Já está com a gente.",
                // "Tenta de novo?" until 2026-08-29 — see the English table for
                // why a retry was the one move guaranteed to fail. Its
                // replacement ("Outra foto pode dar certo, e uma gatinha espera
                // por você de todo jeito", `capture.skipped` word for word) was
                // right while a failure here left the player with nothing. From
                // 2026-09-01 the kitten is made from her photo even on this
                // path, so the line reports that rather than promising a kitten
                // she can already see being made.
                ["photo.our_fault"] =
                    "Algo deu errado do nosso lado. Fizemos a gatinha com a sua " +
                    "foto mesmo assim — com outra pode sair melhor.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER — see the Spanish note.
                ["notification.title"] = "Sua gatinha achou algo atrás do sofá",
                ["notification.body"] = "Ela espera para mostrar, quando você tiver um minuto.",

                ["notification.channel"] = "Lembrete da noite",
                ["notification.channel_description"] =
                    "Uma mensagem tranquila à noite, nos dias em que você não jogou.",
            };

        /// <summary>
        /// French.
        ///
        /// **Address: "vous".** This is the one of the eight where the Russian
        /// table's reasoning transfers unchanged. French "tu" from an app to an
        /// adult stranger is a familiarity she did not grant, and French keeps
        /// that distinction alive in a way Spanish and Portuguese no longer do;
        /// "vous" is both the correct register and the quieter one. As in
        /// Russian, most strings address nobody at all — French lets a button
        /// be an infinitive ("Prendre une photo") and a status a present
        /// participle ("Ouverture…") — so the choice shows in six strings.
        ///
        /// **The game's own kitten is "le chaton" in French, and only in
        /// French, and not for the reason the other tables have.**
        ///
        /// She is female everywhere else by design (see `Copy.cs`, the note
        /// above `meet.title`). This table used to make her "la petite chatte"
        /// where she is the subject of a sentence, reasoning that it is an
        /// ordinary phrase and not baby-talk. The grammar was right and the
        /// word was not: **"chatte" is also the common vulgar French term for
        /// the female genitals**, the exact register of English "pussy", and no
        /// amount of surrounding context suppresses that reading.
        ///
        /// It was in six strings on 2026-08-30, and two of them are the worst
        /// possible places for it. `notification.title` — "La petite chatte a
        /// trouvé quelque chose derrière le canapé" — is a push notification,
        /// which arrives on a lock screen where the player is not the only
        /// person who may read it. `card.caption` — "Regardez la petite chatte
        /// que j'ai dans {0}" — is the text a player posts in public, and reads
        /// as a crude joke to any French speaker.
        ///
        /// This paragraph said, until it was checked on a device, that
        /// `card.caption` "is currently unused by any code, which is luck, not
        /// design". That was wrong, and wrong in the direction that matters:
        /// `CatCardScreen.cs:466` passes it to `Share.Image` as the caption of
        /// the picture, and an Android sharesheet on 2026-08-30 duly offered
        /// "Look at the kitten I have in Sootpaw" beside the rendered card. The
        /// French line was not a latent risk waiting for someone to wire it up.
        /// It was live, on the one screen whose whole purpose is to leave the
        /// phone.
        ///
        /// The claim came from a grep for the key that piped through
        /// `grep -v Copy` to drop the table itself — and the call site reads
        /// `Shell.Copy.Of("card.caption", …)`, so the filter deleted the single
        /// line being looked for. Worth remembering: a negative grep result is
        /// evidence about the grep, not about the program.
        ///
        /// So the kitten is "le chaton" here. The cost is that French alone
        /// loses her sex, which is a real loss and a small one. There is no way
        /// to keep it: every feminine French word for a cat is "chatte".
        ///
        /// "La chatonne" is not the escape. It exists, and it is rare enough to
        /// read as a curiosity.
        ///
        /// **The player's own cat is "votre chat"** from 2026-08-30. This line
        /// used to read "votre chatte, the word a French cat owner uses" — true
        /// of an owner whose cat is female, and about half are not. "Chat" is
        /// the generic, and French generic masculine for an animal of unknown
        /// sex is neutral in a way English "he" is not, so it is used without
        /// apology where a pronoun cannot be dropped.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> French =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "La pièce est propre",
                ["win.room_clean.body"] = "Le chaton s'y sent déjà mieux.",
                // "Tas", one noun for the object every time — the board names
                // it nowhere else (DebugGameView.RenderHeader).
                ["win.corner.title"] = "Tas rangé",
                ["win.corner.body"] = "Le chaton est venu voir.",
                ["win.next"] = "Suivant",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Retour",
                ["card.share_short"] = "Partager",
                // {0} is the game's name.
                ["card.caption"] = "Regardez le chaton que j'ai dans {0}",
                ["map.opening"] = "Ouverture de la pièce…",
                ["win.before"] = "Avant",
                ["win.after"] = "Après",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "Étagères pleines",
                // 82 characters against the English 76 — one line more than
                // the English at worst in `.game__card-body`'s 240px at 15px.
                // "Emplacements" was the first word tried for a slot and is
                // twelve characters; "places" is what the shelf actually has.
                ["lose.body"] =
                    "Toutes les places sont prises et pas trois pareils. " +
                    "Le tas revient comme il était.",
                ["lose.replay"] = "Rejouer",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "Toute la maison est propre",
                ["house.complete.body"] =
                    "Les douze, et un chaton qui n'a plus où cacher " +
                    "ses trouvailles." +
                    "\n\nLa maison s'arrête là, pour l'instant.",
                // A different verb from card.share_short, as everywhere else.
                // 19 characters at Buttons.LabelSize 17, beside the 44px heart
                // inside 342 available.
                ["house.complete.share"] = "Montrer à quelqu'un",
                ["house.complete.caption"] = "Dans {0}, tout est propre.",

                // --- the house map -------------------------------------------
                ["map.title"] = "Votre maison",
                ["map.legend"] =
                    "touchez le numéro clair pour jouer   ·   " +
                    "les pièces cochées sont faites   ·   " +
                    "les pièces sombres sont encore fermées",
                ["map.no_levels"] = "les pièces n'ont pas chargé — rien à afficher",
                ["map.room_failed"] = "impossible d'ouvrir la pièce : {0}",
                ["map.map_failed"] = "impossible d'ouvrir la carte : {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Il manque quelque chose",
                ["levels.unavailable.body"] =
                    "Les pièces n'ont pas pu être chargées. " +
                    "Réinstallez le jeu, s'il vous plaît.",

                // --- the photo screen ----------------------------------------
                // CANNOT WRAP — CaptureScreen.cs:86-89, fontSize 26, 390 units
                // less 48 of padding. 21 characters. "Montrez-nous votre
                // chatte" is 26 and was dropped for that alone: "-nous" adds
                // nothing the French sentence needs, the same cut Russian made
                // with "нам" and Portuguese with "para nós".
                //
                // "Montrez votre chatte" until 2026-08-30 — a female cat, asked
                // of every player. "Chat" is the generic and two shorter.
                ["capture.title"] = "Montrez votre chat",
                // The reason first, the framing advice second. "Robe" is the
                // word a French cat owner uses for a cat's coat and colouring;
                // "pelage" is the fur itself and says less here.
                //
                // "sa robe" and "Qu'elle remplisse le cadre" until 2026-08-30.
                // The second said *she* about the player's cat; "cette robe"
                // points at the photograph instead, and the advice goes to the
                // player, who is the one holding the phone.
                ["capture.hint"] =
                    "Le chaton du jeu prendra cette robe. " +
                    "Remplissez le cadre, si possible.",
                ["capture.camera"] = "Prendre une photo",
                ["capture.gallery"] = "En choisir une à moi",
                // In the player's voice, like the English.
                ["capture.skip"] = "Pas maintenant — je veux un chaton",
                ["capture.skipped"] = "Un chaton vous attend de toute façon.",
                ["capture.opening"] = "Ouverture…",
                ["capture.looking"] = "On regarde…",
                ["capture.colours"] = "On copie sa robe…",
                ["capture.cancelled"] = "Rien ne presse. Choisissez quand vous voulez.",

                // --- meeting the cat ------------------------------------------
                // "La voici", "Comment s'appelle-t-elle ?" and "C'est elle"
                // until 2026-08-30 — three sexes asserted on the one screen
                // that exists to say "this is YOUR cat". French cannot drop a
                // subject the way Russian can, so the title names the animal
                // instead of pointing at it, and the placeholder asks for the
                // name rather than about the bearer: "son" in "son nom" agrees
                // with "nom", not with the cat, so it is free of the problem.
                // "C'est bien ça" confirms a likeness, which is the button's
                // whole job.
                ["meet.title"] = "Voici votre chat",
                ["meet.name_placeholder"] = "Quel est son nom ?",
                ["meet.confirm"] = "C'est bien ça",
                // "Minette" until 2026-08-30, and its own note gave the reason
                // it had to go: "the affectionate French word for a FEMALE
                // cat". MeetYourCatScreen.cs:79 shows this as the player's
                // cat's name, so a male cat was called Minette.
                //
                // "Minou" is the same word in the masculine, and the old note
                // rejected it for exactly that — which is now the reason to
                // take it. It is what a French speaker says to call any cat
                // ("viens, minou"), which is the generic half of "Kitty", and
                // it is also written on name tags, which is the other half.
                //
                // Still not "Chatte", which is the animal and, alone in a name
                // field, reads as the vulgar sense rather than as a name.
                ["cat.default_name"] = "Minou",

                // --- the four outcomes ---------------------------------------
                // "chatte" → "chat" on 2026-08-30, the generic. The advice in
                // the second half goes to the player instead of describing the
                // animal, which drops the last pronoun in this line.
                //
                // REWRITTEN 2026-09-01 — see `Copy.cs` above `photo.no_animal`:
                // nothing ends the run, so "Essayez une photo prise de plus
                // près" was an instruction to retry printed over a progress bar
                // reading "On copie sa robe…". "On" throughout, as in
                // `capture.looking` and `capture.colours` — this table's voice
                // for the game is "on", not "nous".
                ["photo.no_animal"] = "Pas de chat sur cette photo — on l'a utilisée quand même. Une autre rendrait mieux.",
                // "Beau" agrees with "chien": the compliment is to the dog. It
                // was there so that a refusal would not be a rebuke, and it
                // stays now that there is no refusal — the dog's own robe is
                // what the chaton takes, which is `capture.hint`'s promise
                // ("prendra cette robe") kept word for word.
                ["photo.dog"] = "On dirait un chien. Beau — le chaton prendra sa robe.",
                // "pendant qu'il ne bouge pas" until 2026-08-30, and the note
                // here defended it: "il" after "un chat" is the neutral French
                // for an animal of unknown sex, and no restructure kept the
                // advice without it.
                //
                // Half right. The generic masculine is genuinely neutral in
                // French grammar, but this table had just finished removing
                // every "elle" for the same animal, and a reader does not parse
                // "il" as an unmarked default — it is the one word left on the
                // screen that says which sex the cat is.
                //
                // And a restructure did keep the advice: "Une autre, sans
                // bouger ?" named nobody either, exactly like the English "One
                // more, holding still?" it was written against.
                //
                // That whole line is gone on 2026-09-01, because it was still a
                // request for another photograph, and the robe is now copied
                // from this one while the player reads it. What survives is the
                // pronoun rule: "on devine la robe" names no animal at all, so
                // the "il" this note fought over never comes back.
                ["photo.unclear"] = "Un chat, mais flou — on devine la robe. Une photo plus nette serait plus juste.",
                // "Elle est chez nous." until 2026-08-30. "Bienvenue" speaks
                // TO the cat rather than about her, which is what a person does
                // at that moment anyway — the same move English made with
                // "Got you."
                ["photo.accepted"] = "Bienvenue chez nous.",
                // "On réessaie ?" until 2026-08-29 — see the English table. Its
                // replacement ("Une autre photo peut marcher, et un chaton vous
                // attend de toute façon", the tail being `capture.skipped`) held
                // while this path still ended the run. From 2026-09-01 it does
                // not end anything: the chaton is built from her photo even
                // here, so the line says so instead of promising one she is
                // already watching appear. "Chatte" stays out of it, as
                // everywhere in this table — see the class note.
                ["photo.our_fault"] =
                    "Quelque chose a échoué de notre côté. On a quand même fait " +
                    "le chaton d'après votre photo — une autre marcherait " +
                    "peut-être mieux.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER — see the Spanish note.
                // "Votre" dropped, as in Russian and for the same reason: the
                // possessive costs six characters on a lock screen and French
                // does not need it.
                //
                // This note used to argue for "La petite chatte" over "Le
                // chaton" here, on the grounds that the body below takes her as
                // its subject and French cannot drop a subject pronoun the way
                // Russian can, so "il attend" would say *he* and lose the sex
                // that 12-copy-english spent a pass getting right.
                //
                // The argument was sound and it lost anyway, on 2026-08-30, to
                // something it had not weighed: "chatte" is the vulgar French
                // word for the female genitals — and this string is a PUSH
                // NOTIFICATION, so it lands on a lock screen in front of
                // whoever happens to be looking. See the class note above.
                // "Il attend" it is, and the kitten is masculine in French.
                ["notification.title"] = "Le chaton a trouvé quelque chose derrière le canapé",
                ["notification.body"] = "Il attend de vous le montrer, quand vous aurez une minute.",

                ["notification.channel"] = "Rappel du soir",
                ["notification.channel_description"] =
                    "Un message discret le soir, les jours où vous n'avez pas joué.",
            };

        /// <summary>
        /// German. **The longest table here, and the one width decided most
        /// often** — German runs roughly a third longer than English for the
        /// same sentence, and three call sites have no room for that.
        ///
        /// **Address: "du".** The opposite of the Russian decision, on purpose.
        /// German "Sie" is not the polite default a Russian "вы" is; it is the
        /// register of a bank, an insurer and a government form, and every
        /// consumer app this audience already has on her phone — Apple's own
        /// German, Spotify, Netflix, IKEA — says "du". "Sie" in a quiet game
        /// about a kitten would not read as respect, it would read as an
        /// institution. It is also the shorter form nearly every time, which on
        /// this table is not a small thing. **This is the decision in the pass
        /// most open to being overruled on taste**; it shows in nine strings
        /// and is a find-replace.
        ///
        /// **"die kleine Katze", not "das Kätzchen"**, wherever she is the
        /// subject of a sentence. "Kätzchen" is the ordinary word and is
        /// grammatically neuter, so "es wartet" says *it* — exactly the trap
        /// Russian hit from the other side. "Die kleine Katze" is plain German,
        /// keeps the feminine, and lets `notification.body` say "sie". Where
        /// the sentence does not need her sex, "Kätzchen" stays.
        ///
        /// **The player's own cat gets no "sie"** from 2026-08-30. The species
        /// noun "Katze" is untouched — a German cat owner says "meine Katze"
        /// about a Kater, and `capture.title` and the `photo.*` lines where a
        /// "Katze" stands right in front of the pronoun are plain grammatical
        /// agreement, not a claim. What went is the bare "sie" with no noun
        /// behind it, in `meet.*` and `photo.accepted`, which a reader can only
        /// take as *she*.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> German =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "Das Zimmer ist sauber",
                ["win.room_clean.body"] = "Der kleinen Katze gefällt es hier schon besser.",
                // "Haufen", one noun for the object every time.
                ["win.corner.title"] = "Haufen weggeräumt",
                ["win.corner.body"] = "Die kleine Katze kam schauen.",
                ["win.next"] = "Weiter",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Zurück",
                ["card.share_short"] = "Teilen",
                // {0} is the game's name. Plural imperative: a feed is people,
                // not a person, and it keeps the caption clear of the du/Sie
                // choice the rest of the table makes.
                ["card.caption"] = "Schaut, welche kleine Katze ich in {0} habe",
                ["map.opening"] = "Zimmer wird geöffnet…",
                // Under a 116px pane at 12px bold. The idiomatic German pair,
                // and both words are shorter than the English they replace.
                ["win.before"] = "Vorher",
                ["win.after"] = "Nachher",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "Regale voll",
                // SHORTENED. The faithful sentence is "Alle Plätze sind belegt
                // und es sind keine drei gleichen dabei. Der Haufen liegt
                // wieder so, wie er vorher war." — 104 characters, which is
                // five wrapped lines in `.game__card-body`'s 240px at 15px
                // under two photographs. What is here is 82 against the
                // English 76: "es sind … dabei" and the relative clause both
                // go, and neither carried meaning. 22 characters cut.
                ["lose.body"] =
                    "Alle Plätze sind belegt und keine drei gleichen. " +
                    "Der Haufen liegt wieder wie vorher.",
                ["lose.replay"] = "Nochmal",

                // --- the end of the house ------------------------------------
                // 25 characters at `.game__card-title`'s 22px bold, which does
                // not wrap; the card grows to fit inside the overlay's 390
                // units less 48 of card padding, so this is the longest title
                // in the eight tables and still inside 342.
                ["house.complete.title"] = "Das ganze Haus ist sauber",
                ["house.complete.body"] =
                    "Alle zwölf. Und eine kleine Katze, die ihre Funde " +
                    "nirgends mehr verstecken muss." +
                    "\n\nWeiter geht das Haus vorerst nicht.",
                // A different verb from card.share_short, as everywhere else.
                // 15 characters at Buttons.LabelSize 17, beside the 44px heart
                // inside 342 available — the shortest of the eight, which
                // German needed: "Jemandem zeigen" drops the dative object the
                // literal "Zeig es jemandem" would carry.
                ["house.complete.share"] = "Jemandem zeigen",
                ["house.complete.caption"] = "In {0} ist jetzt jedes Zimmer sauber.",

                // --- the house map -------------------------------------------
                ["map.title"] = "Dein Haus",
                // SHORTENED in the third clause. "dunkle Zimmer sind noch
                // verschlossen" is the literal and repeats "Zimmer" a second
                // time in one line; "dunkle sind noch zu" is what a person
                // says and is 20 characters shorter.
                ["map.legend"] =
                    "tippe auf die helle Zahl zum Spielen   ·   " +
                    "abgehakte Zimmer sind fertig   ·   " +
                    "dunkle sind noch zu",
                ["map.no_levels"] = "keine Zimmer geladen — nichts zu zeigen",
                ["map.room_failed"] = "Zimmer ließ sich nicht öffnen: {0}",
                ["map.map_failed"] = "Karte ließ sich nicht öffnen: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Da fehlt etwas",
                ["levels.unavailable.body"] =
                    "Die Zimmer konnten nicht geladen werden. " +
                    "Bitte installiere das Spiel neu.",

                // --- the photo screen ----------------------------------------
                // CANNOT WRAP. CaptureScreen.cs:86-89, fontSize 26, panel 390
                // units less 48 of padding — about 24 characters. 20 here, and
                // it is why the address is "du": "Zeigen Sie uns Ihre Katze" is
                // 25 and clips on the narrowest phone. The width did not decide
                // the register, but it agrees with it.
                ["capture.title"] = "Zeig uns deine Katze",
                // The reason first, the framing advice second. "Fell" is the
                // word a German cat owner uses; "Fellzeichnung" is what a
                // breed standard says.
                //
                // "ihr Fell" and "füllt sie das ganze Bild" until 2026-08-30 —
                // both about the player's cat, and the second one a bare "sie"
                // two words after a different cat. "Dieses Fell" points at the
                // photograph, and the framing advice goes to the player.
                ["capture.hint"] =
                    "Die kleine Katze im Spiel bekommt dieses Fell. " +
                    "Am besten ganz nah heran.",
                ["capture.camera"] = "Foto aufnehmen",
                ["capture.gallery"] = "Eines von meinen wählen",
                // In the player's voice, like the English.
                ["capture.skip"] = "Jetzt nicht — ich will ein Kätzchen",
                ["capture.skipped"] = "Ein Kätzchen wartet so oder so auf dich.",
                ["capture.opening"] = "Wird geöffnet…",
                ["capture.looking"] = "Wir schauen…",
                ["capture.colours"] = "Wir übernehmen das Fell…",
                ["capture.cancelled"] = "Keine Eile. Wähle eines, wann du magst.",

                // --- meeting the cat ------------------------------------------
                // Also fontSize 26 and also unwrapped (MeetYourCatScreen.cs:78).
                // "Da ist sie", "Wie heißt sie?" and "Das ist sie" until
                // 2026-08-30 — three bare "sie" with no noun anywhere near
                // them, on the one screen that exists to say "this is YOUR
                // cat". The first two now put the species noun where the
                // pronoun was, which is agreement rather than a claim; the
                // button confirms a likeness, which is all it ever did.
                // "Da ist deine Katze" is 18 and "Wie heißt deine Katze?" 22,
                // both inside their boxes.
                ["meet.title"] = "Da ist deine Katze",
                ["meet.name_placeholder"] = "Wie heißt deine Katze?",
                ["meet.confirm"] = "Genau so",
                // "Minka" until 2026-08-30, and its own note gave the reason it
                // had to go: "the classic German cat name, plain, FEMININE".
                // MeetYourCatScreen.cs:79 shows this as the player's cat's
                // name, so a Kater was called Minka.
                //
                // "Mieze" was rejected here for being the generic — "Kitty" in
                // the "Miezekatze" sense rather than a name — and that is now
                // the reason to take it. English keeps "Kitty" and Russian
                // moved to «Котёнок» on exactly this argument: what stands in
                // the field before the owner types is not supposed to be a
                // name, it is the word you use for a cat whose name you do not
                // know yet.
                //
                // Not "Kätzchen", which is the grammatically safest answer —
                // neuter, so it asserts nothing at all — and reads like a
                // caption rather than like something anyone says to a cat.
                ["cat.default_name"] = "Mieze",

                // --- the four outcomes ---------------------------------------
                // REWRITTEN 2026-09-01 — see `Copy.cs` above `photo.no_animal`.
                // "Versuch ein Foto…" and "Noch eines, während sie still hält?"
                // were retry instructions, and a retry was the only way forward
                // until the photo screen stopped refusing. It does not refuse
                // now, and the bar under these lines reads "Wir übernehmen das
                // Fell…" while they are being read.
                ["photo.no_animal"] = "Hier ist keine Katze — wir haben das Foto trotzdem genommen. Ein anderes würde besser passen.",
                // "Schön" as a compliment to the dog. It was written so that a
                // refusal would not be a rebuke; there is no refusal left, and
                // it stays because the dog's own Fell is what the kitten gets.
                // "Die kleine Katze" and not "das Kätzchen" — she is the
                // subject here, which is the rule stated in the class note.
                ["photo.dog"] = "Das sieht nach einem Hund aus. Schön — die kleine Katze bekommt dieses Fell.",
                ["photo.unclear"] = "Eine Katze, aber unscharf — das Fell haben wir geraten. Mit einem schärferen Foto wäre es genauer.",
                // "Sie ist bei uns." until 2026-08-30 — a bare "sie" with no
                // noun to agree with. "Willkommen" is said TO the cat, which
                // is what a person does at that moment anyway, and is the same
                // move English made with "Got you."
                ["photo.accepted"] = "Willkommen bei uns.",
                // "Versuchst du es noch einmal?" until 2026-08-29 — see the
                // English table. What replaced it ("Ein anderes Foto klappt
                // vielleicht, und ein Kätzchen wartet so oder so auf dich", the
                // tail being `capture.skipped`) was true while this path still
                // sent her back to the buttons. From 2026-09-01 it does not:
                // the kitten is made from her photo even when our side fails,
                // so the line reports that rather than promising a Kätzchen she
                // can already see. "Die kleine Katze" again — subject of the
                // sentence, so not the neuter "Kätzchen".
                ["photo.our_fault"] =
                    "Bei uns ist etwas schiefgegangen. Wir haben die kleine Katze " +
                    "trotzdem aus deinem Foto gemacht — ein anderes klappt " +
                    "vielleicht besser.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER — see the Spanish note. "Deine" dropped for
                // length, as in Russian and French: 51 characters against the
                // English 43, and it is the longest lock-screen title of the
                // eight. "Kätzchen" would be six characters shorter and would
                // force "es wartet" in the body below, which says *it*.
                ["notification.title"] = "Die kleine Katze hat etwas hinter dem Sofa gefunden",
                ["notification.body"] = "Sie wartet darauf, es dir zu zeigen, wenn du eine Minute hast.",

                ["notification.channel"] = "Abenderinnerung",
                ["notification.channel_description"] =
                    "Eine ruhige Nachricht am Abend, an Tagen ohne Spiel.",
            };

        /// <summary>
        /// Italian.
        ///
        /// **Address: "tu".** "Lei" is alive in Italian, but it belongs to a
        /// counter, a doctor's office and a letter from the bank; every app on
        /// this player's phone, Apple's Italian included, says "tu", and "Lei"
        /// in a game about a kitten would read as a firm writing to a client.
        /// The same reasoning as Spanish and German, and the same reason it is
        /// not the Russian decision: Italian "tu" does not claim the
        /// familiarity Russian "ты" does.
        ///
        /// **"la gattina"** — the ordinary Italian word for a young female cat,
        /// so the English "she" is kept, for the game's own kitten. **The
        /// player's cat is "un gatto"** from 2026-08-30, the generic: see the
        /// class note. Italian pays almost nothing for it, because it drops
        /// subjects — "Come si chiama?", "in cui si vede più grande" and "Ora è
        /// con noi" were already free of the problem and are untouched.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Italian =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "La stanza è pulita",
                ["win.room_clean.body"] = "Alla gattina piace già di più.",
                // "Mucchio", one noun for the object every time.
                ["win.corner.title"] = "Mucchio sistemato",
                ["win.corner.body"] = "La gattina è venuta a guardare.",
                ["win.next"] = "Avanti",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Indietro",
                ["card.share_short"] = "Condividi",
                // {0} is the game's name. Plural: a feed is people.
                ["card.caption"] = "Guardate che gattina ho in {0}",
                ["map.opening"] = "Apriamo la stanza…",
                ["win.before"] = "Prima",
                ["win.after"] = "Dopo",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "Scaffali pieni",
                // 78 characters against the English 76 — the same wrapping in
                // `.game__card-body`'s 240px. "Com'era" rather than "come era
                // prima", which is three words for the same thing.
                ["lose.body"] =
                    "Tutti i posti sono occupati e non ci sono tre uguali. " +
                    "Il mucchio torna com'era.",
                ["lose.replay"] = "Di nuovo",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "Tutta la casa è pulita",
                ["house.complete.body"] =
                    "Tutte e dodici. E una gattina che non ha più dove " +
                    "nascondere quello che trova." +
                    "\n\nLa casa per ora finisce qui.",
                // A different verb from card.share_short. 19 characters at
                // Buttons.LabelSize 17, beside the 44px heart inside 342
                // available. "Farlo vedere a qualcuno" is 23 and is the more
                // idiomatic phrase; it was dropped for the width, and
                // "Mostrare" loses nothing but warmth.
                ["house.complete.share"] = "Mostrare a qualcuno",
                ["house.complete.caption"] = "In {0} è tutto pulito.",

                // --- the house map -------------------------------------------
                ["map.title"] = "Casa tua",
                ["map.legend"] =
                    "tocca il numero chiaro per giocare   ·   " +
                    "le stanze con la spunta sono fatte   ·   " +
                    "quelle scure sono ancora chiuse",
                ["map.no_levels"] = "stanze non caricate — non c'è nulla da mostrare",
                ["map.room_failed"] = "non è stato possibile aprire la stanza: {0}",
                ["map.map_failed"] = "non è stato possibile aprire la mappa: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Manca qualcosa",
                ["levels.unavailable.body"] =
                    "Non è stato possibile caricare le stanze. " +
                    "Reinstalla il gioco, per favore.",

                // --- the photo screen ----------------------------------------
                // CANNOT WRAP — see the Spanish note. 21 characters, inside the
                // ~24 the Russian pass measured as the ceiling.
                //
                // "Mostraci la tua gatta" until 2026-08-30 — a female cat,
                // asked of every player. "Gatto" is the generic, same 21.
                ["capture.title"] = "Mostraci il tuo gatto",
                // "Mantello" is the word an Italian cat owner uses for an
                // animal's coat and its colouring; "pelo" is the fur itself
                // and says less here.
                ["capture.hint"] =
                    "La gattina del gioco prenderà il suo mantello. " +
                    "Meglio se riempie tutta l'inquadratura.",
                ["capture.camera"] = "Scattare una foto",
                ["capture.gallery"] = "Sceglierne una mia",
                // In the player's voice, like the English.
                ["capture.skip"] = "Non ora — voglio una gattina",
                ["capture.skipped"] = "Una gattina ti aspetta comunque.",
                ["capture.opening"] = "Apriamo…",
                ["capture.looking"] = "Guardiamo…",
                ["capture.colours"] = "Copiamo il suo mantello…",
                ["capture.cancelled"] = "Nessuna fretta. Scegli quando vuoi.",

                // --- meeting the cat ------------------------------------------
                // "Eccola qui" and "È lei" until 2026-08-30 — the "-la" and the
                // "lei" were the player's cat, called female. The title names
                // her instead of pointing, and the button confirms a likeness,
                // which is all it ever did. "Come si chiama?" needed nothing:
                // Italian had already dropped the subject.
                ["meet.title"] = "Ecco il tuo gatto",
                ["meet.name_placeholder"] = "Come si chiama?",
                ["meet.confirm"] = "Proprio così",
                // "Micia" until 2026-08-30, and its own note gave the reason it
                // had to go: "the ordinary affectionate Italian for a FEMALE
                // cat". MeetYourCatScreen.cs:79 shows this as the player's
                // cat's name, so a male cat was called Micia.
                //
                // "Micio" is the same word in the masculine and keeps
                // everything that made "Micia" right — a caress rather than
                // the dictionary word, which is "gatto", and a thing Italians
                // both say and write on a tag, which is the double life
                // "Kitty" has. Capitalised, which is the whole difference
                // between the noun and the name.
                ["cat.default_name"] = "Micio",

                // --- the four outcomes ---------------------------------------
                // "gatta" → "gatto" throughout on 2026-08-30, the generic these
                // three sentences always meant. Nothing else moved: Italian had
                // already dropped every subject in them.
                //
                // REWRITTEN 2026-09-01 — see `Copy.cs` above `photo.no_animal`.
                // "Prova una foto…" and "Un'altra, mentre sta fermo?" ordered a
                // retry, which was the only way forward until this screen
                // stopped refusing; it reads now over a bar saying "Copiamo il
                // suo mantello…". "Comunque" for "anyway", the word
                // `capture.skipped` already uses.
                ["photo.no_animal"] = "Qui non c'è nessun gatto — la foto l'abbiamo usata comunque. Con un'altra verrebbe meglio.",
                // "Bello" agrees with "cane": the compliment is to the dog, and
                // it was there so that a refusal would not be a rebuke. No
                // refusal is left and it stays anyway — the second half is
                // `capture.hint`'s own promise ("prenderà il suo mantello").
                ["photo.dog"] = "Sembra un cane. Bello — la gattina prenderà il suo mantello.",
                ["photo.unclear"] = "C'è un gatto, ma è sfocato — il mantello lo indoviniamo. Con una foto più nitida verrebbe più fedele.",
                ["photo.accepted"] = "Ora è con noi.",
                // "Vuoi riprovare?" until 2026-08-29 — see the English table.
                // Its replacement ("Un'altra foto può funzionare, e una gattina
                // ti aspetta comunque", the tail being `capture.skipped`) was
                // true while this path still ended the run empty-handed. From
                // 2026-09-01 the gattina is made from her photo even here, so
                // the line says that instead of promising one she is already
                // watching appear.
                ["photo.our_fault"] =
                    "Qualcosa è andato storto da parte nostra. La gattina l'abbiamo " +
                    "fatta lo stesso dalla tua foto — con un'altra potrebbe venire meglio.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER — see the Spanish note.
                ["notification.title"] = "La gattina ha trovato qualcosa dietro il divano",
                ["notification.body"] = "Aspetta di mostrartelo, quando hai un minuto.",

                ["notification.channel"] = "Promemoria della sera",
                ["notification.channel_description"] =
                    "Un messaggio tranquillo la sera, nei giorni in cui non hai giocato.",
            };

        /// <summary>
        /// Turkish.
        ///
        /// **Address: "siz", in sentences — and the bare imperative on
        /// buttons.** This is the one language here where the two are not the
        /// same decision. Turkish addresses a stranger with "siz", and this
        /// audience is strangers; "sen" from an app to a woman of 45 is the
        /// familiarity the Russian pass refused, and Turkish holds that line
        /// more firmly than Spanish or Italian do. But Turkish interface
        /// convention writes a *button* as a bare imperative stem — "Paylaş",
        /// "Geri", "Devam" — and that form is read as a label rather than as
        /// "sen"; writing "Paylaşın" on a 200px button would be both longer and
        /// stranger. So: full sentences take siz endings ("gösterin",
        /// "bekliyor… bir dakikanız"), labels take the bare stem.
        ///
        /// **No gender to keep.** Turkish has none — "yavru kedi" is a kitten
        /// and "o" is he, she and it at once — so the English "she" survives
        /// without a decision being made, and none is made. The same is true of
        /// Indonesian and Vietnamese below.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Turkish =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "Oda artık temiz",
                ["win.room_clean.body"] = "Yavru kedi burayı şimdiden daha çok sevdi.",
                // "Yığın", one noun for the object every time.
                ["win.corner.title"] = "Yığın toplandı",
                ["win.corner.body"] = "Yavru kedi bakmaya geldi.",
                ["win.next"] = "Devam",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Geri",
                ["card.share_short"] = "Paylaş",
                // {0} is the game's name, and it takes the Turkish suffix
                // through the noun after it ("{0} oyunundaki") rather than
                // directly: an invented Latin name cannot take a Turkish
                // case ending without vowel harmony guessing at its last
                // syllable, and "Sootpaw'daki" would be a guess on a name the
                // store search depends on.
                ["card.caption"] = "{0} oyunundaki yavru kedime bakın",
                ["map.opening"] = "Oda açılıyor…",
                ["win.before"] = "Önce",
                ["win.after"] = "Sonra",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "Raflar doldu",
                // 69 characters against the English 76 — Turkish says this in
                // less, and it wraps to one line fewer in `.game__card-body`'s
                // 240px. "Göz" is the word for a shelf's compartment.
                ["lose.body"] =
                    "Bütün gözler dolu ve aynı olan üç tane yok. " +
                    "Yığın eskisi gibi kalıyor.",
                ["lose.replay"] = "Yeniden",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "Evin her odası temiz",
                ["house.complete.body"] =
                    "On iki odanın hepsi. Ve bulduklarını artık saklayacak " +
                    "yeri kalmayan bir yavru kedi." +
                    "\n\nEv şimdilik burada bitiyor.",
                // A button, so the bare imperative — 13 characters at
                // Buttons.LabelSize 17, beside the 44px heart inside 342
                // available. A different verb from card.share_short, as
                // everywhere else.
                ["house.complete.share"] = "Birine göster",
                ["house.complete.caption"] = "{0} oyununda her oda temiz.",

                // --- the house map -------------------------------------------
                ["map.title"] = "Eviniz",
                // A sentence and not a label, so siz: "dokunun".
                ["map.legend"] =
                    "oynamak için aydınlık numaraya dokunun   ·   " +
                    "işaretli odalar bitti   ·   " +
                    "koyu odalar hâlâ kapalı",
                ["map.no_levels"] = "odalar yüklenmedi — gösterilecek bir şey yok",
                ["map.room_failed"] = "oda açılamadı: {0}",
                ["map.map_failed"] = "harita açılamadı: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Bir şey eksik",
                ["levels.unavailable.body"] =
                    "Odalar yüklenemedi. Lütfen oyunu yeniden kurun.",

                // --- the photo screen ----------------------------------------
                // CANNOT WRAP — see the Spanish note. 17 characters. A
                // sentence, so siz: "gösterin". "Bize kedinizi gösterin" is 23
                // and just fits, but "bize" adds nothing the Turkish needs —
                // the same cut Russian made with "нам".
                ["capture.title"] = "Kedinizi gösterin",
                // "Renk" — colour — rather than a coat-pattern term: Turkish
                // has "post" for a pelt, which is what a rug is made of.
                ["capture.hint"] =
                    "Oyundaki yavru kedi onun rengini alacak. " +
                    "Mümkünse kareyi o doldursun.",
                ["capture.camera"] = "Fotoğraf çek",
                ["capture.gallery"] = "Kendi fotoğrafımı seç",
                // In the player's voice, like the English — and the one place
                // a first-person verb makes the register plain.
                ["capture.skip"] = "Şimdi değil — bir yavru kedi istiyorum",
                ["capture.skipped"] = "Bir yavru kedi yine de sizi bekliyor.",
                ["capture.opening"] = "Açılıyor…",
                ["capture.looking"] = "Bakıyoruz…",
                ["capture.colours"] = "Rengini alıyoruz…",
                ["capture.cancelled"] = "Acele yok. Ne zaman isterseniz seçin.",

                // --- meeting the cat ------------------------------------------
                ["meet.title"] = "İşte o",
                ["meet.name_placeholder"] = "Adı ne?",
                ["meet.confirm"] = "Evet, o",
                // "Mırmır" — from the purr, the way "Мурка" is in Russian, and
                // one of the handful of names Turkish lists as its own rather
                // than as an import. Ordinary, warm, and no gender to get
                // wrong, which is this table's situation throughout.
                //
                // **Not "Pamuk", and the reason is the coat shader, not
                // taste.** Pamuk is cotton, and it is the most common Turkish
                // cat name precisely because it is given to WHITE cats. This
                // kitten's coat is copied from the player's own photograph
                // (`capture.hint`: "Oyundaki yavru kedi onun rengini alacak"),
                // so a default name that asserts a colour is a name that is
                // wrong for most players the moment the shader runs. Same
                // objection to "Tekir", which is a tabby, and to "Sarman",
                // which is a ginger. "Mırmır" describes a sound every cat
                // makes and no coat at all.
                //
                // Six characters and two dotted-less ı, which the font build
                // already had to cover for "yığın" and "açılıyor".
                ["cat.default_name"] = "Mırmır",

                // --- the four outcomes ---------------------------------------
                // REWRITTEN 2026-09-01 — see `Copy.cs` above `photo.no_animal`.
                // "…bir fotoğraf deneyin" and "O dururken bir tane daha?" told
                // the player to try again, which was the only way forward until
                // this screen stopped refusing; they now sit above a bar reading
                // "Rengini alıyoruz…". Siz endings kept, as everywhere in this
                // table's sentences.
                ["photo.no_animal"] = "Burada kedi görünmüyor — fotoğrafı yine de kullandık. Başka bir fotoğraf daha iyi olurdu.",
                // "Çok tatlı" as a compliment to the dog. It was written so that
                // a refusal would not be a rebuke; nobody is refused now and it
                // stays, because the dog's own colour is what the kitten takes —
                // `capture.hint`'s promise ("onun rengini alacak") word for word.
                ["photo.dog"] = "Bu bir köpeğe benziyor. Çok tatlı — yavru kedi onun rengini alacak.",
                ["photo.unclear"] = "Kedi var ama fotoğraf bulanık — rengini tahmin ettik. Daha net bir fotoğrafla daha doğru olurdu.",
                ["photo.accepted"] = "O artık bizde.",
                // "Bir daha denemek ister misiniz?" until 2026-08-29 — see the
                // English table. Its replacement ("Başka bir fotoğraf işe
                // yarayabilir, bir yavru kedi yine de sizi bekliyor", the tail
                // being `capture.skipped`) was true while this path still ended
                // the run. From 2026-09-01 the kitten is made from her photo
                // even here, so the line reports that rather than promising one
                // she is already watching being made.
                ["photo.our_fault"] =
                    "Bizim tarafımızda bir şey ters gitti. Yavru kediyi yine de " +
                    "sizin fotoğrafınızdan yaptık — başka bir fotoğrafla daha iyi çıkabilir.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER — see the Spanish note.
                ["notification.title"] = "Yavru kediniz kanepenin arkasında bir şey buldu",
                ["notification.body"] = "Size göstermek için bekliyor, bir dakikanız olduğunda.",

                ["notification.channel"] = "Akşam hatırlatması",
                ["notification.channel_description"] =
                    "Akşamları, oynamadığınız günlerde tek bir sakin mesaj.",
            };

        /// <summary>
        /// Indonesian.
        ///
        /// **Address: "Anda".** Indonesian's other second person, "kamu", is
        /// what a friend or a younger person is called; from an app to a woman
        /// of 45 it is the same claim Russian "ты" makes. "Anda" is the
        /// register every Indonesian interface she already uses is written in,
        /// including Google's and Apple's own. It is not distant the way German
        /// "Sie" is — Indonesian has no colder register above it that "Anda"
        /// could be mistaken for.
        ///
        /// Indonesian also drops pronouns freely, and most of this table takes
        /// that option: the choice actually shows in five strings.
        ///
        /// **No gender to keep** — "anak kucing" is a kitten and "dia" is he
        /// and she at once, so the English's "she" survives without a decision.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Indonesian =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "Ruangan sudah bersih",
                // "Betah" — settled, at home somewhere — is the word this
                // sentence wants and English has to spend four on.
                ["win.room_clean.body"] = "Anak kucing makin betah di sini.",
                // "Tumpukan", one noun for the object every time.
                ["win.corner.title"] = "Tumpukan beres",
                ["win.corner.body"] = "Anak kucing datang melihat.",
                ["win.next"] = "Lanjut",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Kembali",
                ["card.share_short"] = "Bagikan",
                // {0} is the game's name.
                ["card.caption"] = "Lihat anak kucing saya di {0}",
                ["map.opening"] = "Membuka ruangan…",
                ["win.before"] = "Sebelum",
                ["win.after"] = "Sesudah",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "Rak penuh",
                // 81 characters against the English 76.
                ["lose.body"] =
                    "Semua tempat terisi dan tidak ada tiga yang sama. " +
                    "Tumpukan kembali seperti semula.",
                ["lose.replay"] = "Ulangi",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "Seluruh rumah bersih",
                // SHORTENED. "Dan seekor anak kucing yang tidak lagi punya
                // tempat untuk menyembunyikan barang-barang temuannya" is the
                // faithful clause and is 103 characters on its own — two
                // wrapped lines more than this card can spare under two
                // photographs. "Temuannya" is one word for the four the
                // literal needs, and 24 characters go with it.
                ["house.complete.body"] =
                    "Kedua belas ruangan. Dan seekor anak kucing yang tak " +
                    "punya tempat lagi untuk menyembunyikan temuannya." +
                    "\n\nSampai di sini dulu rumahnya.",
                // A different verb from card.share_short, as everywhere else.
                // 22 characters at Buttons.LabelSize 17 — about 303 units with
                // the share glyph, the padding and the 44px heart, inside the
                // 342 available (DebugGameView.BuildEndingExtras). The longest
                // of the eight, and it fits on arithmetic only.
                ["house.complete.share"] = "Tunjukkan ke seseorang",
                ["house.complete.caption"] = "Semua ruangan di {0} sudah bersih.",

                // --- the house map -------------------------------------------
                ["map.title"] = "Rumah Anda",
                ["map.legend"] =
                    "ketuk nomor yang terang untuk bermain   ·   " +
                    "ruangan bertanda sudah selesai   ·   " +
                    "yang gelap masih terkunci",
                ["map.no_levels"] = "ruangan tidak termuat — tidak ada yang bisa ditampilkan",
                ["map.room_failed"] = "ruangan tidak bisa dibuka: {0}",
                ["map.map_failed"] = "peta tidak bisa dibuka: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Ada yang hilang",
                ["levels.unavailable.body"] =
                    "Ruangan tidak bisa dimuat. Silakan pasang ulang permainan ini.",

                // --- the photo screen ----------------------------------------
                // CANNOT WRAP — see the Spanish note. 21 characters.
                ["capture.title"] = "Tunjukkan kucing Anda",
                // SHORTENED. "Anak kucing di dalam permainan akan mengikuti
                // warna bulunya" is the full phrase; "bulunya" (its fur) goes,
                // because "warnanya" already says whose colour it is and the
                // hint has to leave room for the framing advice that keeps
                // Vision's rejection rate down. 8 characters.
                ["capture.hint"] =
                    "Anak kucing di permainan akan mengikuti warnanya. " +
                    "Usahakan dia memenuhi bingkai.",
                ["capture.camera"] = "Ambil foto",
                ["capture.gallery"] = "Pilih dari foto saya",
                // In the player's voice, like the English.
                ["capture.skip"] = "Nanti saja — saya mau anak kucing",
                ["capture.skipped"] = "Seekor anak kucing tetap menunggu Anda.",
                ["capture.opening"] = "Membuka…",
                ["capture.looking"] = "Melihat…",
                ["capture.colours"] = "Menyalin warnanya…",
                ["capture.cancelled"] = "Tidak buru-buru. Pilih kapan saja.",

                // --- meeting the cat ------------------------------------------
                ["meet.title"] = "Ini dia",
                ["meet.name_placeholder"] = "Siapa namanya?",
                ["meet.confirm"] = "Ya, dia",
                // "Mimi" — at the head of every Indonesian list of ordinary
                // cat names, short, affectionate, and no gender to get wrong.
                // It stood in the Portuguese table too until 2026-08-30, where
                // it was female and had to go; here it is neither, so it
                // stays. (Comment updated only — no Indonesian value changed.)
                //
                // Not "Pus", which is the closer literal match — it is what an
                // Indonesian actually says to call a cat, so it is "Kitty" in
                // the generic half of its meaning. It is also used as a name,
                // and it was still rejected: three letters that spell an
                // English word for wound discharge, in a Latin-script field
                // that an English-reading reviewer, store screenshot or press
                // shot will pass through. The Indonesian player would never
                // see that; everyone auditing the build would.
                //
                // Not "Oyen", the ginger cat of the last few years' Indonesian
                // internet — it names a colour, and this coat comes from the
                // player's photograph. Same objection as Turkish "Pamuk".
                ["cat.default_name"] = "Mimi",

                // --- the four outcomes ---------------------------------------
                // REWRITTEN 2026-09-01 — see `Copy.cs` above `photo.no_animal`.
                // "Coba foto yang…" and "Sekali lagi, saat dia diam?" were
                // instructions to retry, which was the only way forward until
                // this screen stopped refusing; they are read now over a bar
                // saying "Menyalin warnanya…".
                ["photo.no_animal"] = "Tidak ada kucing di sini — fotonya tetap kami pakai. Foto lain akan lebih pas.",
                // "Lucu" as a compliment to the dog. It was there so that a
                // refusal would not be a rebuke; nothing is refused now and it
                // stays, because the dog's own colour is what the kitten takes —
                // `capture.hint`'s promise ("akan mengikuti warnanya") word for
                // word.
                ["photo.dog"] = "Ini sepertinya anjing. Lucu — anak kucing akan mengikuti warnanya.",
                ["photo.unclear"] = "Ada kucing, tapi fotonya buram — warnanya masih kira-kira. Foto yang lebih jelas akan lebih tepat.",
                ["photo.accepted"] = "Dia sudah bersama kami.",
                // "Coba sekali lagi?" until 2026-08-29 — see the English table.
                // Its replacement ("Foto lain mungkin berhasil, dan seekor anak
                // kucing tetap menunggu Anda", the tail being `capture.skipped`)
                // was true while this path still ended the run with nothing.
                // From 2026-09-01 the kitten is made from her photo even here,
                // so the line says that instead of promising one she can already
                // see being made.
                ["photo.our_fault"] =
                    "Ada yang salah di pihak kami. Anak kucingnya tetap kami buat " +
                    "dari foto Anda — dengan foto lain hasilnya mungkin lebih baik.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER — see the Spanish note.
                ["notification.title"] = "Anak kucing Anda menemukan sesuatu di balik sofa",
                ["notification.body"] = "Dia menunggu untuk menunjukkannya, kapan pun Anda sempat.",

                ["notification.channel"] = "Pengingat sore",
                ["notification.channel_description"] =
                    "Satu pesan tenang di sore hari, pada hari-hari Anda tidak bermain.",
            };

        /// <summary>
        /// Vietnamese. **The hardest address decision of the eight, because
        /// Vietnamese has no neutral "you" to fall back on.**
        ///
        /// Every Vietnamese second-person word encodes a relationship: "chị"
        /// is an older sister, "cô" an aunt or a teacher, "em" a younger
        /// person, "bà" an old woman, "quý khách" a valued customer. Choosing
        /// one means the game asserts how old the player is, what sex she is,
        /// and how she stands to whoever is speaking — from a screen that knows
        /// none of the three.
        ///
        /// **Chosen: "bạn".** Literally "friend", and the one word Vietnamese
        /// interfaces have settled on precisely because it is the least
        /// committal: it carries no age, no sex and no hierarchy, and every
        /// Vietnamese app this player already uses says it. It is not perfect —
        /// a Vietnamese ear hears it as *slightly* neutral-to-flat, the way a
        /// form does — and the warmer choice for this audience would be "chị",
        /// which is what a shop assistant would say to a woman of 45 and would
        /// suit the tone better. **"Chị" was rejected because it is wrong when
        /// it is wrong**: it addresses a man as a woman, and a woman of 25 as
        /// older than she is, and there is no way for the game to find out. A
        /// flat-but-correct pronoun beats a warm-but-mistaken one on a screen
        /// this quiet.
        ///
        /// Vietnamese drops pronouns even more freely than Russian, and this
        /// table takes that option nearly everywhere: "bạn" appears in four
        /// strings out of forty-eight. Where the game speaks of itself it says
        /// "mình", the soft first person, rather than the plural "chúng tôi",
        /// which is a company writing to a customer.
        ///
        /// **No gender to keep** — "mèo con" is a kitten and Vietnamese marks
        /// no sex on it, so the English's "she" survives without a decision.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Vietnamese =
            new Dictionary<string, string>
            {
                // --- finishing a pile ----------------------------------------
                ["win.room_clean.title"] = "Căn phòng đã sạch",
                ["win.room_clean.body"] = "Mèo con thấy thích hơn rồi.",
                // "Đống", one noun for the object every time.
                ["win.corner.title"] = "Dọn xong một đống",
                ["win.corner.body"] = "Mèo con lại gần xem.",
                ["win.next"] = "Tiếp",

                // --- the kitten's card, and sharing her --------------------
                ["card.game_name"] = "Sootpaw",
                ["card.close"] = "Quay lại",
                ["card.share_short"] = "Chia sẻ",
                // {0} is the game's name. No second person at all: the caption
                // is written to a feed, and "mình" keeps it in the player's own
                // voice without addressing anybody.
                ["card.caption"] = "Đây là mèo con của mình trong {0}",
                ["map.opening"] = "Đang mở phòng…",
                // Under a 116px pane at 12px bold — the shortest pair of the
                // eight tables.
                ["win.before"] = "Trước",
                ["win.after"] = "Sau",

                // --- losing a pile -------------------------------------------
                ["lose.title"] = "Kệ đã đầy",
                // 67 characters against the English 76. Vietnamese says this
                // shorter, which was not expected — the diacritics make it look
                // long and cost no width at all.
                ["lose.body"] =
                    "Mọi ô đều đầy và không có ba món giống nhau. " +
                    "Đống đồ trở lại như cũ.",
                ["lose.replay"] = "Chơi lại",

                // --- the end of the house ------------------------------------
                ["house.complete.title"] = "Cả nhà đã sạch",
                ["house.complete.body"] =
                    "Cả mười hai phòng. Và một chú mèo con không còn chỗ nào " +
                    "để giấu những thứ nhặt được." +
                    "\n\nNgôi nhà tạm dừng ở đây.",
                // A different verb from card.share_short, as everywhere else.
                // 13 characters at Buttons.LabelSize 17, beside the 44px heart
                // inside 342 available — and "ai đó" (someone) needs no
                // pronoun, so this button is one of the four that never had to
                // choose a form of address at all.
                ["house.complete.share"] = "Cho ai đó xem",
                ["house.complete.caption"] = "Mọi căn phòng trong {0} đều đã sạch.",

                // --- the house map -------------------------------------------
                // One of the four strings that says "bạn": the house being the
                // player's own is the whole point of the screen, and dropping
                // the possessive would make it anybody's house.
                ["map.title"] = "Nhà của bạn",
                ["map.legend"] =
                    "chạm vào số sáng để chơi   ·   " +
                    "phòng có dấu là đã xong   ·   " +
                    "phòng tối vẫn còn khoá",
                ["map.no_levels"] = "không tải được phòng nào — không có gì để hiện",
                ["map.room_failed"] = "không mở được phòng: {0}",
                ["map.map_failed"] = "không mở được bản đồ: {0}",

                // --- levels missing or broken ---------------------------------
                ["levels.unavailable.title"] = "Thiếu mất gì đó",
                ["levels.unavailable.body"] =
                    "Không tải được các phòng. Vui lòng cài lại trò chơi.",

                // --- the photo screen ----------------------------------------
                // CANNOT WRAP — see the Spanish note. 19 characters. "Cho
                // chúng mình xem mèo của bạn" is the literal "show US your
                // cat" and is 30, which clips; "cho xem" carries the same
                // request with nobody named, the same cut Russian made with
                // "нам".
                ["capture.title"] = "Cho xem mèo của bạn",
                // "Màu lông" — the colour of the fur — is what a Vietnamese cat
                // owner says. No pronoun for the player's cat: Vietnamese would
                // need "con mèo ấy" and the sentence before it has just named
                // her.
                ["capture.hint"] =
                    "Mèo con trong trò chơi sẽ mang màu lông ấy. " +
                    "Nếu được, hãy để mèo chiếm trọn khung hình.",
                ["capture.camera"] = "Chụp một tấm",
                ["capture.gallery"] = "Chọn ảnh có sẵn",
                // In the player's voice, like the English.
                ["capture.skip"] = "Để sau — mình muốn một chú mèo con",
                ["capture.skipped"] = "Vẫn có một chú mèo con đang đợi bạn.",
                ["capture.opening"] = "Đang mở…",
                ["capture.looking"] = "Đang xem…",
                ["capture.colours"] = "Đang chép màu lông…",
                ["capture.cancelled"] = "Không vội đâu. Khi nào muốn thì chọn.",

                // --- meeting the cat ------------------------------------------
                ["meet.title"] = "Đây rồi",
                ["meet.name_placeholder"] = "Tên mèo là gì?",
                ["meet.confirm"] = "Đúng là mèo ấy",
                // "Miu" — the one-syllable name Vietnamese guides put first
                // when they say a cat's name should be one or two syllables,
                // and the sound a Vietnamese uses to call one. Ordinary, warm,
                // no gender and no colour asserted.
                //
                // Not "Mun", the other very common one: mun is ebony, it goes
                // to black cats, and this coat is copied from the player's
                // photograph. Not "Mèo", which is simply the word "cat" — the
                // German entry explains why a bare common noun in a field
                // asking for a name reads as an unanswered question.
                //
                // Three plain ASCII letters, alone in this table in needing no
                // diacritic at all, so it is also the one Vietnamese value
                // that cannot fail on a font missing stacked tone marks.
                ["cat.default_name"] = "Miu",

                // --- the four outcomes ---------------------------------------
                // REWRITTEN 2026-09-01 — see `Copy.cs` above `photo.no_animal`.
                // "Thử tấm ảnh có mèo lớn hơn nhé" and "Thêm một tấm lúc mèo
                // ngồi yên nhé?" asked for another photograph, which was the
                // only way forward until this screen stopped refusing; they are
                // read now over a bar saying "Đang chép màu lông…". No subject
                // pronoun for the game, as everywhere in this table — "vẫn dùng
                // tấm này" needs none, and "chúng tôi" would be a company
                // writing to a customer.
                ["photo.no_animal"] = "Không thấy con mèo nào — vẫn dùng tấm này. Tấm khác sẽ hợp hơn.",
                // "Dễ thương lắm" as a compliment to the dog. It was written so
                // that a refusal would not be a rebuke; nobody is refused now
                // and it stays, because the dog's own màu lông is what the
                // kitten takes — `capture.hint`'s promise ("sẽ mang màu lông
                // ấy") word for word.
                ["photo.dog"] = "Trông giống một chú chó. Dễ thương lắm — mèo con sẽ mang màu lông ấy.",
                ["photo.unclear"] = "Có mèo, nhưng ảnh mờ — màu lông đành đoán thôi. Ảnh rõ hơn sẽ đúng màu hơn.",
                ["photo.accepted"] = "Mèo về với nhà rồi.",
                // "Thử lại tấm ấy nhé?" until 2026-08-29, and it named the one
                // move that could only fail again — "tấm ấy", that same photo.
                // Its replacement ("Một tấm khác có thể được, và vẫn có một chú
                // mèo con đang đợi bạn", the tail being `capture.skipped`) was
                // true while this path still ended the run. From 2026-09-01 the
                // kitten is made from her photo even here, so the line says that
                // rather than promising one she is already watching appear.
                ["photo.our_fault"] =
                    "Có gì đó hỏng ở phía bên này. Mèo con vẫn làm theo ảnh của bạn " +
                    "— tấm khác có thể ra đẹp hơn.",

                // --- the evening reminder ------------------------------------
                // NO PLACEHOLDER — see the Spanish note.
                ["notification.title"] = "Mèo con vừa tìm thấy thứ gì đó sau ghế sofa",
                ["notification.body"] = "Mèo đang đợi để khoe, khi nào bạn rảnh một phút.",

                ["notification.channel"] = "Nhắc buổi tối",
                ["notification.channel_description"] =
                    "Một tin nhắn nhẹ nhàng buổi tối, vào những ngày bạn không chơi.",
            };
    }
}
