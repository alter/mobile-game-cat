# Ориентиры retention/CPI и атрибуция на iOS после ATT

Дата сбора: 2026-08-24

Контекст: в проекте порог «возврат на первый день > 35%» — нужно понять, насколько это реалистично для казуальной головоломки, и как вообще проверять стоимость установки по ролику до выхода игры при бюджете на ~500 тестовых установок.

## Кратко

- По отчёту Adjust «The gaming app insights report: 2025 edition» (данные 2024 года), средний D1 retention по всем жанрам мобильных игр — **27%** (снижение с 28% в 2023 году); у казуальных жанров-лидеров (hybrid casual, hyper casual) — 27–28%, но у них retention обваливается к дню 30 до ~2% [Adjust, «The gaming app insights report: 2025 edition», 2025].
- По отчёту GameAnalytics «2025 Mobile Gaming Benchmarks» (данные за 2024 год, 11 600 игр, 1,48 млрд MAU), медианный D1 retention у жанра «puzzle» — **19,66–20,74%**, D7 — 4,27–4,79%, D28 — 1,09–1,26% [GameAnalytics, «2025 Mobile Gaming Benchmarks», 2025].
- Порог «> 35%» — это заметно выше и среднего по индустрии (27%), и медианного показателя головоломок по двум независимым первичным отчётам (≈20–21%). Это не скромная, а амбициозная цель: по формулировке самого Adjust, такие цифры (40–50%) показывают только «топовые игры на рынке» — то есть речь о верхнем эшелоне жанра, а не о типичном результате MVP [Adjust, «The gaming app insights report: 2025 edition», 2025].
- По CPI: медианный CPI по всем игровым жанрам в 2024 году — **$0,36** (снижение с $0,38 в 2023), CPI в Северной Америке вырос до **$1,20**, в США — до **$1,22** [Adjust, «The gaming app insights report: 2025 edition», 2025]. По отдельному отчёту Liftoff и Singular «2025 Casual Gaming Apps Report» (данные февраль 2024 — февраль 2025), CPI казуальных игр на iOS — **$1,41**, на Android — **$0,14** [Liftoff, «2025 Casual Gaming Apps Report», 2025].
- Прямого свежего числа «CPI головоломок в США» и «CPI головоломок в дешёвых странах» по отдельности в открытых, лично проверенных отчётах не нашлось — только числа по казуальному жанру в целом (Liftoff) и по всем играм в разбивке по регионам (Adjust). Встречающиеся в блогах-агрегаторах конкретные цифры вида «puzzle iOS $3, Android $2» или «puzzle CPI на iOS в 5 раз выше, чем на Android» не подтверждаются ни одним из первично проверенных отчётов и, судя по формулировкам, спутаны с данными по казино — такие цифры в этот файл сознательно не включены.
- Для проверки ролика до выхода игры на iOS до полноценного релиза реалистичны Apple Search Ads (Basic, без строгого минимального бюджета, оплата по модели CPI, месячный потолок до $10 000 на приложение) и Custom Product Pages/Product Page Optimization в App Store Connect для сравнения версий страницы — но оба инструмента требуют уже опубликованного в App Store приложения, то есть не работают как «фейковая страница» до появления реального листинга [Apple Developer — Custom Product Pages](https://developer.apple.com/app-store/custom-product-pages/); [Apple Developer — Product Page Optimization](https://developer.apple.com/app-store/product-page-optimization/).
- Честный ответ по атрибуции: на выборке в 500 тестовых установок ни SKAdNetwork, ни AdAttributionKit практически не нужны — оба фреймворка спроектированы для агрегированной атрибуции больших рекламных кампаний с порогами анонимности, а на 500 установках постбэков будет мало и толку от них немного; для собственного event-пайплайна достаточно связывать событие `app_open` с источником через простой параметр диплинка/UTM на уровне собственного сервера, без привлечения SKAdNetwork/AdAttributionKit вообще.
- Custom Product Pages и Product Page Optimization в App Store Connect годятся для сравнения роликов/скриншотов между собой **после** того, как приложение опубликовано (пусть даже в ограниченном регионе), но не раньше — это инструмент оптимизации живой страницы, а не A/B-тест до появления самого приложения в App Store.

## 1. Отраслевые ориентиры retention и CPI для казуальных игр/головоломок (2025–2026)

### Retention day 1

Отчёт **Adjust, «The gaming app insights report: 2025 edition»** (методология: смесь топ-5000 приложений и полного датасета, который отслеживает Adjust, 45 стран в детальной разбивке плюс около 250 стран по стандарту ISO 3166-1, период данных — январь 2023 – март 2025, все суммы в USD) даёт по всем игровым жанрам глобально:

> «Day 1 retention rates for gaming apps globally decreased from 28% to 27% in 2024. Board and card games held steady at 22%, casino climbed from 16% to 19%, and strategy (17%) and trivia (16%) declined. Hybrid and hyper casual games maintained their lead at 28% and 27%—but despite strong early engagement from these genres, by day 30, both dropped to just 2% (vs. the overall games average of 5%).»

— [Adjust, «The gaming app insights report: 2025 edition», 2025, ебук/PDF, зеркало документа: investgame.net](https://investgame.net/wp-content/uploads/2025/05/gamingreport2025_ebook_en.pdf)

Более свежий отчёт этого же семейства, **«The gaming app insights report: 2026 edition»** (данные 2025 года), по вторичному изложению его содержимого подтверждает ту же величину — «D1 Retention across all genres was 27% in 2025» — с оговоркой, что топовые игры на рынке регулярно превышают 40–50% D1 retention, а сравнение со средним по рынку «somewhat misleading», то есть само Adjust предупреждает не путать средний показатель с ориентиром для успешного продукта [пересказ отчёта Adjust 2026 в GameDev Reports, 2026]. Именно текст отчёта 2025 edition (данные 2024 года) был открыт и прочитан напрямую как PDF — сам файл 2026 edition открыть через WebFetch не удалось (сайт adjust.com стабильно отдавал ограничение по количеству запросов на все попытки в ходе этого исследования), поэтому цифра «27% в 2025 году» помечается как взятая из вторичного пересказа, а не из лично прочитанного оригинала.

Жанр-специфичный, лично проверенный ориентир по головоломкам — отчёт **GameAnalytics, «2025 Mobile Gaming Benchmarks»** (датасет: 11 600 игровых приложений, 9 регионов, iOS и Android, 16 жанров, суммарный MAU выборки превышает 1,48 млрд, данные за календарный 2024 год, retention — классическое/calendar-day, не rolling):

> «Puzzle games maintained median D1 retention between 19.66% and 20.74% throughout 2024» (данные извлечены напрямую со страницы отчёта) — там же D7 составил 4,27–4,79%, D28 — 1,09–1,26%, что заметно выше общего медианного D28 по рынку (75% проектов не превышают 3%).

— [GameAnalytics, «2025 Mobile Gaming Benchmarks», 2025](https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks)

**Важное расхождение, которое стоит явно проговорить.** В большом количестве вторичных блогов и агрегаторов (Segwise, Business of Apps со ссылкой на «Mistplay», различные SEO-статьи про «2026 benchmarks») кочует цифра «puzzle D1 retention ≈ 31,85%», иногда приписанная GameAnalytics. При прямом открытии страницы отчёта GameAnalytics эта цифра **не подтвердилась** — реальное значение в первоисточнике почти вдвое меньше (19,66–20,74%). Судя по формулировкам вторичных источников, цифра 31,85% происходит из отдельного источника (упоминается атрибуция «Mistplay Mobile Game Retention Benchmarks»), а не из отчёта GameAnalytics, но саму страницу Mistplay в рамках этого исследования открыть и проверить не удалось (URL не был обнаружен и не был лично прочитан) — поэтому цифра 31,85% в этот файл не включается как непроверенная, а в качестве ориентира для головоломок используется только лично прочитанный отчёт GameAnalytics (19,66–20,74%). Разница практически двукратная, и для оценки реалистичности внутреннего порога проекта это существенно.

**Ответ на прямой вопрос проекта.** Порог «D1 retention > 35%» нужно сравнивать с двумя независимо и лично проверенными первичными числами: общий средний по всем играм — 27% (Adjust, данные 2024), медиана по головоломкам — 19,66–20,74% (GameAnalytics, данные 2024). 35% — это между «средним для лучших жанров-казуалок в моменте установки» (hybrid/hyper casual — 27–28% по Adjust) и «топовыми играми рынка» (40–50% по прямой цитате Adjust). То есть порог в 35% реалистичен не как типичный результат MVP, а как результат уровня успешного, отполированного продукта — надёжных данных о том, что такой уровень типичен именно для головоломок на старте, не найдено; скорее наоборот, оба лично проверенных отчёта показывают, что медиана жанра ниже этого порога, часто заметно ниже.

### CPI (cost per install)

Тот же отчёт Adjust даёт медианный CPI по всем игровым жанрам:

> «In 2024, the median cost per install (CPI) for gaming apps decreased from $0.38 to $0.36. Casino apps climbed from $1.17 to $1.5 [...]. Hyper casual games climbed from $0.33 to $0.4, while hybrid casual nearly doubled, up from $0.54 to $0.95.» — региональная разбивка там же: «North America ($1.03 to $1.2) and the U.S. ($1.04 to $1.22) saw notable increases in CPI».

— [Adjust, «The gaming app insights report: 2025 edition», 2025, PDF-зеркало investgame.net](https://investgame.net/wp-content/uploads/2025/05/gamingreport2025_ebook_en.pdf)

Отдельно по CPM (цена за 1000 показов) тот же отчёт называет по головоломкам конкретное число: «Idle RPG and puzzle games also saw increases, reaching $6.06 and $3.75, respectively» — то есть CPM головоломок в 2024 году составил **$3,75** (глобальный медианный показатель, не разбит по iOS/Android отдельно в тексте отчёта).

Специализированный отчёт **Liftoff и Singular, «2025 Casual Gaming Apps Report»** (данные с февраля 2024 по февраль 2025, 1,4 трлн показов рекламы, 63 млрд кликов, 2,5 млрд установок, $11,9 млрд затрат на рекламу; головоломки в этом отчёте относятся к укрупнённой категории «casual») даёт разбивку по платформам:

> «The cost per install (CPI) of casual gaming apps via iOS amounted to 1.41 U.S. dollars, compared to an overall average of 14 cents for Android» (период: 01.02.2024–28.02.2025).

— [Liftoff, «2025 Casual Gaming Apps Report», 2025](https://liftoff.ai/2025-casual-gaming-apps-report/); дублируется в [Statista, со ссылкой на этот же отчёт, 2025](https://www.statista.com/statistics/1241651/global-cpi-gaming-apps-genre-platform/)

Отдельного, лично проверенного числа именно для «CPI головоломок в США» и «CPI головоломок в дешёвых странах» (раздельно от общей категории casual и от общего показателя по всем играм) в открытых отчётах Adjust, AppsFlyer, Sensor Tower, Liftoff, GameAnalytics, AppMagic обнаружить не удалось. Попытка открыть релевантные страницы Business of Apps (`businessofapps.com/data/mobile-game-retention-rates/`, `businessofapps.com/marketplace/mobile-game-marketing/research/mobile-game-marketing-costs/`) через WebFetch была предпринята несколько раз — сайт стабильно возвращал HTTP 403 и не выдал ни одной страницы для прямого чтения, поэтому цифры с этого сайта в файл не включены, хотя оно часто цитируется другими агрегаторами. Отчёт **AppsFlyer, «State of Gaming for Marketers 2026»** (данные 2025 года; в отчёте использованы данные 9,6 тыс. игровых приложений, 24,8 млрд установок, из них 14,1 млрд платных) был открыт через WebFetch, но его целевая страница-лендинг не выдаёт постатейные цифры без регистрации/скачивания полного отчёта — удалось подтвердить только общие агрегаты (глобальные затраты на UA в играх в 2025 году — $25 млрд, по пересказу отчёта), но не разбивку CPI по жанрам или странам [AppsFlyer, «State of Gaming for Marketers 2026», landing-страница, 2026]. Итог: **надёжных данных о раздельном CPI головоломок «США против дешёвых стран» не найдено** — можно опираться только на общий региональный CPI по всем играм (Adjust: США $1,22, Северная Америка $1,20 в 2024 году) и на общий CPI казуального жанра по платформам (Liftoff: iOS $1,41, Android $0,14 за период февраль 2024 – февраль 2025).

## 2. Как измерять CPI при проверке роликов до выхода игры

### Apple Search Ads

Кампании Apple Search Ads Basic работают по модели оплаты за установку (CPI): рекламодатель задаёт месячный бюджет и либо принимает предлагаемую Apple максимальную цену за установку, либо выставляет свою — Apple со своей стороны не публикует на официальной странице конкретную минимальную сумму месячного бюджета для Basic-кампаний (страница описывает только сам механизм назначения максимального CPI и подсказку от Apple на основе конкурентной среды приложения), при этом типичный потолок для Basic-кампаний на одно приложение — до $10 000 в месяц по данным сторонних агентских разборов сервиса [ApptWeak, «The ultimate guide to Apple Ads in 2026», 2026]. Официальная страница Apple подтверждает саму модель (CPI, свой или рекомендованный максимум) и содержит промо-предложение «Try Apple Ads for free with a 100 USD credit» для новых рекламодателей [Apple Ads — Basic, 2026]. Ограничение метода: Apple Search Ads показывает объявление только в поиске App Store, то есть измеряет не «нравится ли ролик людям в ленте», а конверсию из поискового намерения — это не полноценная проверка креатива для холодной аудитории, скорее проверка конверсии страницы приложения.

### Custom Product Pages и Product Page Optimization

Оба инструмента App Store Connect требуют, чтобы приложение уже было опубликовано в App Store (пусть и в ограниченном регионе/со статусом «доступно», а не «черновик») — это не способ протестировать ролик до появления реального листинга приложения.

**Custom Product Pages (CPP)** — дополнительные версии карточки приложения (до 70 штук на приложение) со своими скриншотами, промо-текстом, видео-превью и уникальной ссылкой:

> «Developers can publish up to 70 additional versions of their product page on the App Store for iPhone and iPad» [...] «Developers see a 2.5 percentage point increase on average when referring people to a custom product page. This is a 156% increase compared to the 1.6% average conversion rate on default product pages.»

— [Apple Developer — Custom Product Pages, 2026](https://developer.apple.com/app-store/custom-product-pages/)

CPP напрямую связываются с вариациями объявлений Apple Search Ads — именно эта связка даёт измеримое сравнение конверсии разных роликов/скриншотов при одинаковом трафике из поиска.

**Product Page Optimization (PPO)** — встроенный A/B-тест самой карточки приложения на органическом трафике (без привязки к конкретному рекламному каналу): до трёх альтернативных версий («treatments») с иконкой, скриншотами и превью показываются случайно выбранной доле посетителей страницы, а App Analytics считает показы, конверсию, процент улучшения и уровень доверия к результату:

> «Compare different app icons, screenshots, and app previews on your App Store product page to find out which resonate with people most» [...] «If you allocate 40% of your traffic to your test and have two treatments, each treatment receives 20% of your total traffic and your original product page receives the remaining 60%.»

— [Apple Developer — Product Page Optimization, 2026](https://developer.apple.com/app-store/product-page-optimization/)

Тест PPO рассчитан на приложение с уже существующим органическим или рекламным трафиком (тесты идут до 90 дней или до ручной остановки, а оценка длительности отталкивается от имеющихся исторических показателей конверсии) — то есть это инструмент для «после того как страница уже живёт», а не для доисследования ролика при полном отсутствии листинга.

### Meta и TikTok

Оба канала позволяют направлять рекламу на предзаказ/pre-order страницу приложения (Apple поддерживает предзаказы через App Store) или напрямую на страницу приложения, если тестовая сборка уже прошла ревью и опубликована (например, в ограниченном регионе soft launch). Точных официальных цифр по минимальному дневному/недельному бюджету кампании в Meta Ads Manager и TikTok Ads Manager для целей этого документа лично проверить через открытые справочные страницы не удалось: несколько попыток открыть официальные страницы справки Meta Business Help Center и TikTok for Business через WebFetch вернули либо страницы без содержательного текста (только заголовок), либо страницу-404 у TikTok — поэтому конкретную сумму минимального бюджета в USD/день в этот документ включать нельзя (риск устаревшего или неверного числа). Что можно сказать без риска ошибиться: оба сервиса позволяют запускать кампании с оптимизацией на установки приложения при небольших ежедневных бюджетах (кампании такого рода принято тестировать по несколько дней подряд, чтобы у алгоритма показа набралось достаточно данных для стабилизации ставки, — это общая механика оптимизации, а не конкретное число), и оба принимают ссылку на предзаказ/страницу приложения как посадочную страницу для кампании на установки. Перед тем как закладывать бюджет в план MVP, минимальные бюджеты нужно свежими глазами проверить непосредственно в интерфейсе Ads Manager на момент запуска — точных официальных данных на дату сбора этого файла не найдено.

### «Заглушка страницы в магазине» как отдельный приём

Заявленный в задаче приём «заглушка страницы в магазине» (dummy/fake door store page) технически не поддерживается напрямую ни Apple, ни Google как официальный инструмент — App Store не позволяет опубликовать листинг без реального билда, проходящего ревью. То, что на практике называют этим термином в индустрии — это либо (а) страница предзаказа в App Store с минимальным работоспособным билдом, прошедшим ревью, либо (б) отдельная веб-страница-имитация карточки приложения, на которую ведёт реклама, с измерением клика по кнопке «Установить» как прокси-конверсии вместо реальной установки. Общей достоверной статистики по точности такого прокси-метода (насколько CTR по веб-заглушке предсказывает будущий CPI в реальном App Store) в лично проверенных источниках этого исследования не найдено — это описывается как распространённая практика в среде разработчиков, но не как измеренный и опубликованный крупным источником метод с точными числами.

## 3. Атрибуция на iOS после ATT: SKAdNetwork и AdAttributionKit

**SKAdNetwork (SKAN)** — введённый Apple ещё до ATT механизм приватной агрегированной атрибуции: рекламная сеть получает не данные о конкретном пользователе, а агрегированный постбэк с ограниченным «значением конверсии» (conversion value) по кампании, с задержками и порогами анонимности (crowd anonymity), не раскрывающими точные данные о маленьких по объёму кампаниях. **AdAttributionKit (AAK)** — представленный на WWDC 2024 преемник SKAN, который сохраняет полную обратную совместимость с ним и добавляет несколько возможностей, отсутствовавших в SKAN: измерение повторного вовлечения (re-engagement) через Universal Links, поддержку альтернативных сторонних магазинов приложений (что стало актуальным из-за антимонопольного регулирования Евросоюза, DMA), «Developer Mode» с сильно сокращённой задержкой постбэков для целей тестирования (вместо обычных 24–48 часов — порядка 5–10 минут), обязательную криптографическую подпись показов (JSON Web Signature) и учёт показа рекламы только при просмотре дольше двух секунд [Tenjin — «AdAttributionKit vs. SKAdNetwork: What's the Difference?», 2026]. Оба фреймворка сосуществуют: «Apple has not announced any deprecation timeline for SKAdNetwork» — то есть переходить на AAK прямо сейчас не обязательно, если нет конкретной причины (например, работа с альтернативными магазинами приложений в ЕС или потребность в измерении re-engagement) [Tenjin — «AdAttributionKit vs SKAdNetwork: What's the Difference?», 2026]; [Singular — «AdAttributionKit: the new SKAdNetwork?», 2026].

**Честный ответ на вопрос "нужно ли это при 500 проверочных установках": нет, не нужно.** Оба фреймворка спроектированы для атрибуции покупной рекламы через сторонние рекламные сети в масштабе, где работают пороги анонимности и агрегация по кампаниям — на выборке в 500 установок постбэков будет физически мало, а сами механизмы (задержки постбэков, урезанные значения конверсии, агрегация) созданы для защиты приватности в больших объёмах трафика, а не для точного измерения на маленькой тестовой когорте. Прямая цитата по этому поводу: «For a pre-launch test of this scale, attribution frameworks carry minimal practical importance. At 500 installs, you'll likely see limited postback data due to Apple's aggregation and privacy protections designed for larger campaigns» [Singular — «AdAttributionKit: the new SKAdNetwork?», 2026]; независимо к тому же выводу приходит и другой источник: «At this scale, neither framework meaningfully impacts results. Focus on basic conversion tracking instead» [Tenjin — «AdAttributionKit vs SKAdNetwork: What's the Difference?», 2026]. Для проекта, который и так строит собственный сбор событий (файл `01-own-event-collection.md`), это означает: связывать `app_open` с источником трафика достаточно через собственный параметр в диплинке/deferred deep link на уровне своего же сервера, не подключая ни SKAdNetwork, ни AdAttributionKit — они не нужны ни технически (объём мал), ни организационно (усложняют пайплайн ради возможностей, которые на этом масштабе не раскрываются).

## 4. Custom Product Page и Product Page Optimization для сравнения роликов

Оба механизма (описаны подробнее в разделе 2) в принципе годятся для сравнения роликов между собой — но с оговорками по применимости к задаче «проверить ролик до выхода игры»:

- **Product Page Optimization** сравнивает несколько версий карточки (в том числе видео-превью) между собой на едином входящем трафике страницы, случайно раскидывая посетителей по вариантам, и даёт статистику по конверсии и уровню доверия — это корректный инструмент А/Б-сравнения роликов между собой, но только когда трафик на страницу уже идёт (органический или платный) и приложение уже опубликовано;
- **Custom Product Pages**, связанные с вариациями объявлений Apple Search Ads, позволяют направить разный рекламный трафик на разные версии страницы с разными видео и сравнить конверсию по каждой версии — это тоже сравнение роликов между собой, но опять же требует опубликованного приложения и настроенных кампаний Apple Search Ads;
- ни один из двух инструментов не сравнивает ролики друг с другом *до* публикации приложения в App Store — для этого этапа применимы только внешние площадки (Meta, TikTok, YouTube) с посадкой на предзаказ/veb-заглушку, как описано в разделе 2, либо стадия soft launch в одном небольшом регионе, после которой уже доступны и CPP, и PPO.

## Источники

- [Adjust — «The gaming app insights report: 2025 edition» (PDF, зеркало investgame.net)](https://investgame.net/wp-content/uploads/2025/05/gamingreport2025_ebook_en.pdf)
- [GameDev Reports — пересказ «Adjust: Gaming App Insights Report 2026»](https://gamedevreports.substack.com/p/adjust-gaming-app-insights-report)
- [GameAnalytics — «2025 Mobile Gaming Benchmarks»](https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks)
- [Segwise — «Mobile Game Retention Benchmarks 2026»](https://segwise.ai/blog/mobile-gaming-app-user-retention-strategies)
- [Liftoff — «2025 Casual Gaming Apps Report»](https://liftoff.ai/2025-casual-gaming-apps-report/)
- [Liftoff — «Must-Know Highlights From the 2025 Casual Gaming Apps Report»](https://liftoff.ai/blog/highlights-2025-casual-gaming-apps-report/)
- [Statista — «Global CPI gaming apps by genre and platform»](https://www.statista.com/statistics/1241651/global-cpi-gaming-apps-genre-platform/)
- [AppsFlyer — «State of Gaming for Marketers 2026» (страница отчёта)](https://www.appsflyer.com/resources/reports/gaming-app-marketing/)
- [Apple Developer — Custom Product Pages](https://developer.apple.com/app-store/custom-product-pages/)
- [Apple Developer — Product Page Optimization](https://developer.apple.com/app-store/product-page-optimization/)
- [ApptWeak — «The ultimate guide to Apple Ads in 2026»](https://www.apptweak.com/en/aso-blog/guide-to-apple-search-ads)
- [Tenjin — «AdAttributionKit vs. SKAdNetwork: What's the Difference?»](https://tenjin.com/blog/adattributionkit-vs-skadnetwork-whats-the-difference/)
- [Singular — «AdAttributionKit: the new SKAdNetwork?»](https://www.singular.net/blog/adattributionkit-the-new-skadnetwork/)

### Страницы, которые не удалось открыть (упомянуты для прозрачности, цифры с них не использованы)

- businessofapps.com/data/mobile-game-retention-rates/ — HTTP 403 при каждой попытке WebFetch
- businessofapps.com/marketplace/mobile-game-marketing/research/mobile-game-marketing-costs/ — HTTP 403
- adjust.com/blog/gaming-app-insights-2026/, adjust.com/resources/ebooks/ и adjust.com/blog/adattributionkit/ — стабильный HTTP 429 (ограничение частоты запросов) на все попытки
- facebook.com/business/help/... и ads.tiktok.com/help/... — открылись без содержательного текста (только заголовок страницы) или как страница 404



