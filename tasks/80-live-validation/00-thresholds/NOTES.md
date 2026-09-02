Source: cat-shelter-tasks.md lines 992-1051; primary data in
knowledge/analytics/02-benchmarks-and-attribution.md.

## Why this task exists

As originally written, "day-1 retention > 35%" is not a floor separating a
viable game from a dead one - it is roughly double the genre median. Two
independently checked primary sources:

- GameAnalytics, "2025 Mobile Gaming Benchmarks" (11,600 games, 2024 data):
  puzzle median day-1 retention 19.66-20.74%.
- Adjust, "The gaming app insights report: 2025 edition" (2024 data):
  all-games average day-1 retention 27%.

35% sits between "the best casual sub-genres at launch" (hybrid/hyper
casual, 27-28% per Adjust) and "the top of the market" (40-50% per Adjust's
own framing). A game returning 25% - comfortably above the genre median -
would be shut down by the 35% rule as originally written. The frequently
repeated "puzzle day-1 is ~32%" figure did not survive a check against the
GameAnalytics primary source and is not used here.

## The three stances (pick one, write it down)

1. Keep 35%. Only interested in a breakout, not a merely viable game. Honest
   given "three prototypes in three months, three weeks each."
2. Lower to ~25%. Comfortably above the genre median, still evidence the
   hook works.
3. Bands instead of one line: below 20% stop, 20-30% iterate once, above
   30% push. Costs nothing, avoids a binary verdict on a noisy sample.

## Sample size caution

A hundred installs puts a day-1 retention reading within several percentage
points either way. Treat 24% and 27% as the same number - this is itself an
argument for the banded stance.

## Metric four: two numbers, not one

"Tapped 'one more shelf', >15%" was written against all players, but the
button only appears on the lose screen, and the difficulty curve (pile sizes
36/48/60, measured win rates ~98% / ~87% / ~66%) determines how many players
ever see it. A low combined figure is ambiguous between "the levels were too
easy" and "nobody would pay" - the two numbers (share who ever lost; share
of those who tapped) resolve that ambiguity. Set both here, in advance.

## Publishers as a source for the fourth threshold

SayGames, Homa, Kwalee, CrazyLabs, Rollic (the five in
04-publisher-submission) are worth asking directly what they consider a
passing monetisation signal, rather than guessing at 15% alone.

---

## Metric four lost its instrument, 2026-08-27

The section above ("Metric four: two numbers, not one") assumes the "one more
shelf" button exists. It does not. **D4 was revised on 27.08.2026** and the
button and its two strings were removed from the lose screen, after the owner
hit the jam in play and asked why the game offers something and then refuses
it. `Analytics.BoosterTap` and `Board.AddShelfSlots` both remain; only the
offer is gone.

The reasoning is worth repeating because it settles what to do next: the tap
was **free**, and a tap on a costless offer to not lose is not evidence about
willingness to pay. Metric four asks whether anyone would pay. So the number
that button produced was never going to decide anything.

That leaves gate 3 with three real metrics and one hole. Pick one of these,
in writing, before `01-spend`:

**(a) Drop metric four from this run.** Say plainly that this $400 answers
"do they arrive, and do they come back" and not "would they pay". Costs
nothing, and the honesty matters: a missing number must not later be read as
a failed one. What it forfeits is the monetisation signal a publisher will
ask for.

**(b) Put the offer back with a real price.** One StoreKit product, three
slots, once per level, priced. This is the only version that produces evidence
about paying. It needs the App Store Connect record, a configured in-app
purchase and review — days, not hours — and it pulls monetisation work forward
that GOAL.md defers until after gate 3.

**(c) Ask the five publishers instead of measuring.** `04-publisher-submission`
already contacts SayGames, Homa, Kwalee, CrazyLabs and Rollic. What they call
a passing monetisation signal for a prototype is free to ask and worth more
than a number from a hundred installs. This is not a substitute for (a) or
(b) — it is how the threshold gets chosen if (b) is taken.

**The denominator problem is now measured, and it is the reason to lean to
(a).** Re-measured 27.08 on the shipped levels
(`30-levels-solver/10-remeasure-curve-partial-info/NOTES.md`): a realistic
player wins **92.7%** of level attempts, so roughly one attempt in fourteen
ends in a jam. At a hundred installs, the number who ever reach a lose screen
is small, and the number who then buy anything is smaller still. Option (b)
would spend days building a purchase to measure it on a handful of people.

**Correction to the section above.** It quotes win rates "~98% / ~87% / ~66%".
Those are the retired figures for a player who only watches the shelf. The
current measurement, same script, same seed, is 98.0% / 83.8% / 71.5% for that
player and **99.2% / 94.2% / 90.0%** for a realistic one. The realistic row is
the one that governs how often a lose screen is ever seen.

---

## Metric two does not mean what its name says, 2026-08-27

GOAL.md defines metric 2 as **"uploaded a photo > 40%"** and calls it the one
that matters most: *"the hook missed, and the hook is the whole concept"*. The
threshold is written against a phrase, and the phrase has two readings that
this task must choose between before any money moves.

**What the code counts today.** `Analytics.PhotoUploaded()` fires in
`View/CaptureScreen.cs:191`, immediately after the crop succeeds and before
anything leaves the device. Nothing is uploaded at that moment — and nothing
ever is, because `CaptureScreen.AskWorker` is a delegate the shipping code
never assigns and the Worker is not deployed (D17). So the event means **"the
photo passed the on-device cat check and was cropped"**.

**The two readings, and why the difference is not cosmetic.**

- *Accepted locally.* Measures whether a player is willing to point a camera at
  her cat and hand the photo over. That is the hook.
- *Traits came back.* Measures the same willingness **times** the reliability
  of a network call to a service that does not exist yet. A player who did
  everything right but hit a 502 would count as a miss.

Read as the second, a bad number is ambiguous between "nobody wanted to" and
"our proxy fell over", which is exactly the ambiguity metric 4's two-number
rule was invented to avoid. Read as the first, the number is clean but says
nothing about whether she ever saw *her* cat — and seeing her own cat is the
promise, not handing over a photo.

**So: record both, or record one and say which.** This is the same discipline
as metric 4's denominator. Two events cost nothing — acceptance is already
instrumented, and a second event on a successful traits response is one line
whenever the Worker exists. Choosing one and not writing down which is the only
outcome that fails.

**Unblocked note for whoever writes it:** `70-analytics/02-nine-events/NOTES.md`
already flags this under "Left open" and asks for a deliberate decision. This
is where the decision belongs, because it is a threshold question, not a
call-site question. The declared event surface is exactly nine (D9), so a
second event is a change to that surface and needs saying out loud.

---

# Чем из четырёх метрик можно мерить СЕГОДНЯ — 2026-09-02, наблюдением

Пороги выбирает владелец, и это не мой вопрос. Но выбирать их вслепую не
надо: вот что в игре сейчас есть, чем это подтверждено и чего нет вовсе.

| метрика | измеритель | состояние |
|---|---|---|
| дошли до экрана съёмки, >90 % | `photo:screen_shown` | **работает**, наблюдалось на устройстве 02.09 (`70-analytics/02`, таблица прогона) |
| загрузили снимок, >40 % | `photo:uploaded` | **работает**, наблюдалось; срабатывает только после принятого снимка, закреплено проверкой мест вызова |
| вернулись на первый день | GameAnalytics / App Store Connect | **кода не требует**, но требует ключей — пункт 7 в OWNER-TODO |
| нажали «ещё полка», >15 % | `booster:tap` | **измерителя НЕТ** |

Про четвёртую подробно, потому что это единственная дыра.

Кнопка «ещё полка» убрана 27.08 пересмотром решения D4, и убрана правильно:
она предлагала бесплатное и потому меряла «хотите ли вы не проиграть» (хотят
все), а не «заплатите ли вы». Событие `Analytics.BoosterTap` и приём
`Board.AddShelfSlots` в коде оставлены намеренно — вернуть кнопку, когда у
неё будет цена, это одна строка. Но сегодня `booster:tap` не вызывается
ниоткуда, и это подтверждено дважды: поиском по всему дереву 02.09 и
проверкой мест вызова в `tools/tests/test_analytics_call_sites.py`, где его
мёртвость закреплена намеренно.

**Что это значит для порога.** Четвёртую метрику нельзя ни превысить, ни
провалить — её нечем снять. Пока это так, «четыре метрики» проекта на деле
три, и решение по метрике 4 (три выхода описаны выше в этом же файле) надо
принять раньше, чем начнутся траты `01-spend`, а не после.
