# Где бесплатно разместить узел-посредник для мобильной игры (проверка по официальным страницам, 2026-08-24)

Дата проверки: 2026-08-24. Все цифры ниже взяты с официальных страниц цен/документации через прямой запрос к сайту (WebFetch/curl), с цитатами. Там, где официальную страницу не удалось получить или число не указано прямо, написано «данных не найдено» — вместо предположения.

Контекст задачи: один обработчик HTTP POST, принимает снимок кота в base64 (до 512×512, до 200 КБ), вызывает Anthropic Claude со зрением (ключ должен жить на узле, не на устройстве), возвращает ~100 байт JSON, ничего не хранит. Нагрузка — несколько сотен обращений за весь период проверки, пик — десятки в сутки. Возможен второй такой же обработчик для приёма игровых событий с записью в простое хранилище. Бюджет — ноль, GCP исключён.

## Кратко

1. **Cloudflare Workers** — самый подходящий вариант: 100 000 запросов в сутки бесплатно, карта не нужна, воркер не «засыпает» (это не контейнер, а изолят V8, поднимается за миллисекунды), исходящие HTTPS-запросы к api.anthropic.com разрешены штатно через `fetch()`.
2. Но **Python Workers у Cloudflare — открытая бета** (`python_workers` — флаг совместимости), готовность к промышленной эксплуатации официально не заявлена. Для надёжности сам обработчик лучше писать на JavaScript/TypeScript, даже если остальной проект на Python.
3. У Cloudflare есть бесплатное хранилище: **KV** (100 000 чтений/сутки, 1 000 записей/сутки, 1 ГБ) и **D1** (5 млн строк чтения/сутки, 100 000 строк записи/сутки, 5 ГБ) — годится под второй обработчик для игровых событий.
4. **Fly.io лишился свободного уровня** — карта обязательна для любой организации, минимальная работающая машина стоит ~$2/мес.
5. **Render** бесплатен без карты, но веб-сервис засыпает через 15 минут простоя и «просыпается» около минуты — для игрока, ждущего результат на экране съёмки, это плохо при первом обращении после паузы. Бесплатная база Postgres на Render истекает через 30 дней.
6. **Railway** — это не постоянно бесплатный план, а 30-дневный пробный период с $5 кредита (карта не нужна); дальше — платно.
7. **Hugging Face Spaces**: бесплатное оборудование (CPU Basic) есть, но с 2026 года создание Docker/Gradio Space для личного аккаунта требует платного плана PRO ($9/мес) — бесплатно только статические сайты и до 2 Gradio-приложений на ZeroGPU (заточен под GPU-инференс, а не проксирование к внешнему API).
8. **Oracle Cloud Always Free** даёт по-настоящему бессрочно 2 ARM OCPU + 12 ГБ ОЗУ + 200 ГБ диска, но требует карту для верификации личности при регистрации, и аккаунты, простаивающие 30+ дней, официально могут быть признаны заброшенными и приостановлены.
9. **PythonAnywhere**: бесплатный (Beginner) аккаунт не «спит», в его белом списке разрешённых внешних адресов уже есть `api.anthropic.com` — то есть обращение к Anthropic возможно. Лимит — 100 секунд CPU-времени в сутки; для наших объёмов (десятки лёгких запросов в сутки) этого может хватить, но фоновые/always-on задачи в бесплатном плане недоступны.
10. Для сравнения: самый дешёвый «обычный» VPS — Netcup VPS 500 G12 за **€5,91/мес** (4 ГБ ОЗУ, 128 ГБ NVMe); у Hetzner дешёвая линейка «Cost-Optimized» сейчас недоступна для заказа, актуальный минимум — CPX12 от **€11,49/мес** (1 vCPU, по данным из открытого прайс-JSON hetzner.com).

## Сводная таблица

| Служба | Бесплатный предел | Карта нужна | Засыпает | Годится нам |
|---|---|---|---|---|
| Cloudflare Workers | 100 000 запросов/сутки, 10 мс CPU/запрос | Нет | Нет (edge-изолят) | Да — основной кандидат (писать на JS/TS) |
| Cloudflare KV / D1 | KV: 100k чтений + 1k записей/сутки, 1 ГБ; D1: 5 млн чтений + 100k записей/сутки, 5 ГБ | Нет | — | Да, для второго обработчика (события) |
| Deno Deploy | 1 млн запросов/мес, 15 ч CPU/мес, 20 ГБ трафика/мес, 1 ГиБ KV | Данных не найдено | Данных не найдено (вероятно нет, это edge-функции) | Возможен как запасной вариант (только JS/TS) |
| Fly.io | Свободного уровня нет | Да, обязательна | Не сервис "спит", а машину нужно явно останавливать | Нет — не бесплатно с первого часа |
| Render | 750 инстанс-часов/мес | Нет (без карты — просто отключение при перерасходе) | Да, через 15 мин простоя, пробуждение ~1 мин | Частично — плохо для «холодного» первого запроса игрока |
| Railway | $5 кредита на 30 дней (пробный период) | Нет | Данных не найдено | Нет — не постоянно бесплатно |
| Koyeb | По документации есть free instance 512 МБ/0.1 vCPU/2 ГБ SSD | Данных не найдено | Данных не найдено | Под вопросом — противоречивые данные на сайте |
| Vercel (Hobby) | 1 млн вызовов функций/мес, 1 млн edge-запросов/мес | Данных не найдено | Функции serverless, не контейнер — «сна» как у Render нет | Нет — план прямо ограничен некоммерческим личным использованием |
| Hugging Face Spaces | CPU Basic бесплатно, но Docker/Gradio Space требует PRO $9/мес для личного аккаунта | Данных не найдено | Да, «засыпает» при простое (точное время не указано) | Нет для простого FastAPI-обработчика без платного плана |
| Oracle Cloud Always Free | 2 OCPU + 12 ГБ (ARM Ampere), 200 ГБ диска — бессрочно | Да, для верификации личности | Нет, но аккаунт может быть признан заброшенным при простое 30+ дней | Да, но тяжеловесно для такой простой задачи (свой VPS, свой веб-сервер) |
| PythonAnywhere | 100 сек CPU/сутки, доступ к внешним сайтам только по белому списку (api.anthropic.com в списке) | Данных не найдено | Нет (это не контейнер, веб-приложение всегда «есть», но лимит CPU-секунд в сутки) | Да, как вариант на чистом Python |
| Hetzner Cloud (для сравнения, не бесплатно) | — | — | — | От €11,49/мес (CPX12, 1 vCPU) |
| Netcup (для сравнения, не бесплатно) | — | — | — | От €5,91/мес (VPS 500 G12, 4 ГБ ОЗУ, 128 ГБ NVMe) |

## 1. Cloudflare Workers

Источник: `developers.cloudflare.com/workers/platform/pricing/`, `.../workers/platform/limits/`, `.../workers/languages/python/`, `.../workers/runtime-apis/fetch/`, `.../kv/platform/limits/`, `.../d1/platform/limits/`, `.../d1/platform/pricing/`, `cloudflare.com/plans/`.

- **Запросы**: «100,000 per day» на бесплатном плане.
- **CPU-время**: «10 milliseconds of CPU time per invocation».
- **Превышение лимита**: «If you exceed any one of these limits, further operations of that type will fail with an error» — запрос просто отклоняется с ошибкой, автосписаний на бесплатном плане это не влечёт.
- **Карта**: маркетинговая страница cloudflare.com/plans прямо говорит: «Start building for free — no credit card required».
- **Python Workers**: страница документации прямо пишет «Python Workers are in beta», требуется флаг совместимости `python_workers`. Явного подтверждения промышленной готовности в документации нет — это открытая бета, а не GA. Заявлена поддержка FastAPI, Pydantic и доступ к KV/D1/R2/Workers AI через биндинги, но для критичного по надёжности прод-обработчика безопаснее взять JavaScript/TypeScript Worker (там ограничений по зрелости нет).
- **Размер тела запроса**: до 100 МБ (зависит от плана аккаунта, не от плана Workers) — для 200 КБ base64-снимка кота с огромным запасом.
- **Размер самого воркера**: 3 МБ после gzip, 64 МБ до сжатия.
- **Подзапросы**: 50 исходящих `fetch()`-вызовов на один запрос — для одного вызова Anthropic более чем достаточно.
- **Память**: 128 МБ на изолят.
- **Исходящие HTTPS-запросы к стороннему API**: подтверждено — `fetch()` в Workers Runtime API прямо предназначен для «asynchronously fetching resources via HTTP requests inside of a Worker», ограничений именно на домен назначения в документации нет.
- **KV** (бесплатный уровень): 100 000 чтений/сутки, 1 000 записей/сутки на разные ключи (на один и тот же ключ — не чаще раза в секунду), хранилище 1 ГБ на аккаунт и на namespace, размер значения до 25 МиБ.
- **D1** (бесплатный уровень): 5 млн прочитанных строк в сутки, 100 000 записанных строк в сутки, до 10 баз на аккаунт, до 500 МБ на одну базу, 5 ГБ хранилища всего, 50 запросов на один вызов Worker, история Time Travel 7 дней. Лимиты сбрасываются в полночь по UTC.

Итог: лучший бесплатный вариант для лёгкого прокси-обработчика — при условии, что код пишется на JS/TS, а не на Python (тот пока в бете).

## 2. Deno Deploy

Источник: `deno.com/deploy/pricing`.

- Бесплатный план: «1M» запросов в месяц, «15h» CPU-времени в месяц, «20GB» исходящего трафика в месяц, «1GiB» хранилища KV.
- Требование карты на регистрацию — данных не найдено на странице цен.
- Поведение при простое (холодный старт/«сон») — на проверенных страницах (`deploy/pricing`, `deploy/manual/regions`) явного описания не найдено; это серверлесс-платформа на edge, что обычно означает отсутствие «спящего контейнера» в духе Render, но официального подтверждения задержки первого запроса не найдено.
- Работает только с JavaScript/TypeScript — под наш Python-проект годится лишь как отдельно написанный тонкий прокси не на Python.

## 3. Fly.io

Источник: `fly.io/docs/about/pricing/`.

- Бесплатного уровня в 2026 году нет: «All organizations (except for Linked Organizations) require a credit card on file».
- Минимальная работающая машина — shared-cpu-1x с 256 МБ ОЗУ: «$0.0028/час» (около $2,02/мес в Ashburn; по другим регионам от $1,94 до $3,14).
- Остановленная (не работающая) машина продолжает тарифицироваться только за хранилище: «$0.15/GB per month of provisioned capacity» для volumes.
- Реально бесплатные позиции на странице цен — только первые 10 SSL-сертификатов на один хост и первые 10 ГБ снимков томов в месяц; вычисления и трафик бесплатными не бывают.
- Итог: не подходит при нулевом бюджете — деньги нужны с первого часа, и обязательна карта.

## 4. Render

Источник: `render.com/docs/free`.

- Веб-сервис засыпает при простое: «Render spins down a Free web service that goes 15 minutes without receiving any inbound traffic».
- Задержка «пробуждения»: «This process takes about one minute. Render displays a loading page to connecting browsers while a service is spinning up» — то есть игрок, сделавший фото после паузы больше 15 минут, будет ждать до минуты, и в это время увидит страницу загрузки Render, а не JSON от нашего API. Это критично для сценария «игрок ждёт ответа на экране съёмки».
- Бесплатные часы: «Render grants 750 Free instance hours to each workspace per calendar month».
- Карта: явного требования карты для регистрации на странице нет. При перерасходе трафика/минут сборки без привязанной карты — «Render instead suspends all of your Free services for the remainder of the month» (просто отключение, автосписаний без карты быть не может).
- Файловая система эфемерна — «ephemeral filesystem», локальные изменения теряются при перезапуске/повторном деплое.
- Бесплатная база Postgres: «Free Render Postgres databases expire 30 days after creation» — то есть для долговременного хранения игровых событий бесплатный Postgres на Render не подходит без апгрейда.

## 5. Railway

Источник: `railway.com/pricing`, `docs.railway.com/reference/pricing`.

- Это не бессрочный бесплатный план, а пробный период: «Free Trial — $5 in credits for 30 days to try Railway».
- Карта: «No credit card required» для пробного периода.
- Что после триала — на проверенных страницах явно не описано; по общей структуре тарифов дальше идёт платный план Hobby. Для задачи с ограниченным периодом проверки может хватить, но это не «бесплатно навсегда».
- Поведение «сна» — данных не найдено.

## 6. Koyeb

Источник: `koyeb.com/pricing`, `koyeb.com/docs`.

- Публичная страница цен (`/pricing`) показывает только платные планы Pro ($29/мес), Scale ($299/мес), Enterprise, и упоминает лишь «Free 5h» для Postgres (0.25 vCPU, 1 ГБ).
- При этом страница документации (`/docs`, раздел про деплой приложений) содержит фразу: «Start with a `free` Instance: 512MB of RAM, 0.1 vCPU, and 2GB of SSD» — то есть где-то в продукте бесплатный постоянный инстанс для сервисов, видимо, есть.
- Это противоречие не удалось разрешить в рамках проверки: отдельные страницы про лимиты и условия free-инстанса (`/docs/reference/free-instances`, `/docs/reference/plans`, `/docs/pricing-details`) возвращают 404.
- Требование карты и поведение «сна» (scale-to-zero) для free-инстанса — данных не найдено на доступных официальных страницах.
- Итог: Koyeb нельзя ни уверенно рекомендовать, ни уверенно исключить — нужна отдельная проверка через реальную регистрацию, если рассматривать всерьёз.

## 7. Vercel

Источник: `vercel.com/pricing`, `vercel.com/docs/plans/hobby`.

- План Hobby (бесплатный): 1 000 000 вызовов функций/месяц, 4 CPU-часа активного вычисления/месяц, 360 ГБ-часов памяти/месяц, до 1 000 000 edge-запросов/месяц, 10 ГБ трафика/месяц, максимальная длительность функции — 300 секунд.
- Требование карты — данных не найдено на проверенных страницах.
- Ключевое ограничение: «the Hobby plan restricts users to non-commercial, personal use only» (согласно fair use guidelines). Мобильная игра, даже на этапе проверки, обычно не подпадает под «личное некоммерческое использование» — это делает Vercel Hobby юридически рискованным выбором для этой задачи, а не только технически ограниченным.
- При превышении лимитов Hobby: «in most cases, if you exceed your usage limits on the Hobby plan, you will have to wait until 30 days have passed before you can use the feature again» — то есть просто пауза функции, а не счёт.

## 8. Hugging Face Spaces

Источник: `huggingface.co/docs/hub/spaces-overview`, `huggingface.co/pricing`.

- Оборудование CPU Basic (2 vCPU, 16 ГБ ОЗУ, 50 ГБ не постоянного диска) формально бесплатно, но с важной оговоркой прямо в документации: «Static Spaces are free for everyone. Gradio and Docker Spaces run on compute and require a paid plan to create: PRO for personal accounts, Team or Enterprise for organizations. Free personal accounts in good standing can still host up to 2 Gradio Spaces running on ZeroGPU».
- То есть **обычный Docker-контейнер с FastAPI на бесплатном личном аккаунте создать нельзя** — для этого нужен план PRO за «$9 /month». Бесплатно доступны только статические Spaces и (в ограниченном количестве) Gradio-приложения на ZeroGPU — а ZeroGPU заточен под инференс на GPU по очереди, а не под простой прокси к внешнему HTTP API.
- «Lifecycle management»: «On free hardware, your Space will "go to sleep" and stop executing after a period of time if unused» — засыпание подтверждено, но точное время простоя до сна в документации не указано.
- Требование карты — данных не найдено.
- Итог: для задачи не подходит без платы $9/мес, если делать это через Docker/FastAPI Space, как и планировалось.

## 9. Oracle Cloud Always Free

Источник: `docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm`, `oracle.com/cloud/free/`.

- Вычисления: «All tenancies get the first 1,500 OCPU hours and 9,000 GB hours per month for free for VM instances using the VM.Standard.A1.Flex shape... For Always Free tenancies, this is equivalent to 2 OCPUs and 12 GB of memory» — можно поднять один инстанс 2 OCPU/12 ГБ либо два по 1 OCPU/6 ГБ, и это бессрочно, а не пробный период.
- Хранилище: «All tenancies receive a total of 200 GB of Block Volume storage, and five volume backups included in the Always Free resources».
- Карта обязательна, и это явно объясняется в FAQ: «Why do I need to provide credit or debit card information when I sign up for Oracle Cloud Free Tier? ... we need to ensure that you are who you say you are. We use your contact information and credit/debit card information for account setup and identity verification. Oracle may periodically check the validity of your card, resulting in a temporary "authorization" hold... [it does] not result in actual charges to your account».
- Риск для простаивающего аккаунта — тоже прямо описан: «Accounts left idle for 30 days or more may be deemed abandoned and become eligible for suspension or termination».
- Надёжность по отзывам пользователей проверить в рамках этой сессии не удалось (лимит поисковых запросов исчерпан) — оценивать можно только по официально задокументированной политике выше, без ссылки на форумы.
- Итог: единственный вариант из списка с бессрочно бесплатным полноценным сервером (2 vCPU, 12 ГБ ОЗУ) — но это уже настоящий VPS, на котором самому придётся поднимать веб-сервер, TLS, systemd/докер и следить за простоями аккаунта. Для одного HTTP-обработчика на несколько сотен запросов — избыточно тяжеловесно, но пригодится, если проект перерастёт «игрушечный» объём.

## 10. PythonAnywhere

Источник: `pythonanywhere.com/pricing/`, `pythonanywhere.com/whitelist/`.

- Бесплатный (Beginner) план: «100 seconds» CPU-времени в сутки, 512 МБ диска, 1 веб-приложение на `<имя>.pythonanywhere.com`, до 2 консолей, без SSH, без MySQL, без запланированных задач (scheduled tasks) и без «always-on tasks» (в таблице тарифов стоит крест).
- Исходящие сетевые запросы на бесплатном плане ограничены белым списком доменов: «Specific sites via HTTP(S) only». Проверка списка (`pythonanywhere.com/whitelist/`) показала, что **`api.anthropic.com` в этом списке присутствует** — то есть вызывать Anthropic с бесплатного аккаунта можно.
- Требование карты для регистрации — данных не найдено на проверенных страницах.
- Веб-приложение на бесплатном плане не «спит» так, как Render (это не поднимаемый по требованию контейнер, а постоянно смонтированное WSGI-приложение за прокси PythonAnywhere), но лимит в 100 секунд CPU-времени в сутки — это именно процессорное время, а не время ожидания сети, поэтому ожидание ответа от Anthropic по HTTP, скорее всего, не расходует эту квоту так быстро, как вычисления. Точных данных о том, засчитывается ли сетевое ожидание в CPU-секунды на PythonAnywhere, официально найти не удалось.
- Итог: единственный из проверенных сервисов, который одновременно (а) написан явно под Python/Flask/Django, (б) не имеет проблемы с «пробуждением», (в) официально разрешает обращение к `api.anthropic.com` с бесплатного аккаунта. Главный риск — уложиться в 100 секунд CPU-времени в сутки при десятках запросов пиковой нагрузки.

## Самый дешёвый обычный VPS (точка отсчёта)

Источник: `hetzner.com/cloud/` (данные добыты из открытого JSON-файла тарифов `www.hetzner.com/_resources/app/data/bench/cloud_data.json`, который использует сама страница), `netcup.com/en/server/vps`.

- **Hetzner Cloud**: строка «Cost-Optimized» (исторически самая дешёвая линейка) на странице явно помечена как «currently unavailable» — сейчас недоступна для заказа. Актуальный минимум среди линейки «Shared - Regular Performance» — тариф **CPX12** (1 виртуальное ядро AMD) по цене **€11,49/мес** в европейских дата-центрах (Нюрнберг/Фалькенштайн/Хельсинки), согласно официальному прайсовому JSON, который сама страница hetzner.com подгружает для отрисовки таблицы цен. Данных по объёму ОЗУ/диска для этого тарифа с официальной страницы получить не удалось (таблица с характеристиками отрисовывается через JavaScript, в статическом JSON есть только цена и число ядер).
- **Netcup**: самый дешёвый VPS — **VPS 500 G12** за **€5,91/мес** (с НДС 19%), 4 ГБ ОЗУ DDR5 (ECC), 128 ГБ NVMe.
- Итог: «не выкручиваться» стоит от **€5,91/мес** (Netcup) — это и есть цена вопроса, если решить не тратить время на подгонку под чей-то бесплатный лимит.

## Можно ли обойтись вообще без своего узла

Источник: `developers.cloudflare.com/ai-gateway/`, `developers.cloudflare.com/ai-gateway/configuration/authentication/`.

Единственный проверенный по официальной документации способ не писать собственный обработчик — это **Cloudflare AI Gateway**.

- Что это: «Observe and control your AI applications with analytics, caching, rate limiting, and model fallback through AI Gateway» — прокси-слой перед провайдерами ИИ.
- Поддержка Anthropic подтверждена: документация перечисляет поддерживаемых провайдеров, включая «OpenAI, Anthropic, Google, and more».
- Механизм скрытия ключа называется **BYOK (Bring Your Own Keys)**: реальный ключ Anthropic сохраняется на стороне Cloudflare («configured with stored provider keys through Bring Your Own Keys (BYOK)»), а устройство обращается к Gateway уже с отдельным токеном Cloudflare, а не с ключом Anthropic. Это действительно означает, что не нужно писать и разворачивать собственный код-обработчик — только настроить Gateway через панель/API.
- Важная оговорка из той же документации по безопасности: «Any token with AI Gateway Run can send requests through every gateway in the account, including any configured with stored provider keys through BYOK, consuming those credentials» — то есть токен, который придётся так или иначе зашить в приложение (или получать динамически), при утечке позволяет расходовать привязанный ключ Anthropic через любой Gateway в аккаунте. Риск того же класса, что и утечка ключа с самодельного узла — просто на один уровень косвенности дальше. Для безопасного использования всё равно обычно нужен свой минимальный код, который аутентифицирует именно ваше мобильное устройство/сессию перед тем как отдать ей Gateway-токен, а не встраивает статический токен в APK/IPA.
- Стоимость и лимиты AI Gateway: документация отвечает лишь «Available on all plans» — то есть доступен и на бесплатном плане Cloudflare, но точные количественные лимиты (запросов в сутки и т.п.) на проверенных страницах не приведены — данных не найдено.
- Другие «посредники поставщиков» (сторонние агрегаторы вроде OpenRouter и т.п.) в рамках этой проверки не удалось изучить по официальным страницам — лимит поисковых запросов в сессии был исчерпан до того, как до них дошла очередь. Утверждать что-либо про их бесплатные лимиты или безопасность было бы домыслом, поэтому здесь эта тема сознательно не раскрыта.

Вывод по разделу: полностью без какого-либо серверного посредника (пусть даже готового, чужого) обойтись нельзя — Anthropic не выдаёт клиентских ограниченных/одноразовых ключей для мобильных приложений, поэтому по факту либо пишете свой тонкий Worker, либо настраиваете чужой (Cloudflare AI Gateway с BYOK) и всё равно добавляете свою аутентификацию поверх него. С учётом того, что писать свой Worker на Cloudflare — это буквально десяток строк кода и тот же самый бесплатный лимит, отдельно настраивать AI Gateway ради «экономии кода» смысла немного: разница не в том, нужен ли посредник, а в том, кто содержит его логику маршрутизации.

## Что выбрать при нулевом бюджете

**Прямая рекомендация: Cloudflare Workers, обработчик на JavaScript/TypeScript (не Python — тот в бете), плюс Cloudflare D1 или KV для второго обработчика с игровыми событиями.**

Почему именно так:
- 100 000 запросов в сутки бесплатно с огромным запасом покрывают заявленную нагрузку (несколько сотен обращений за весь период проверки, пики — десятки в сутки).
- Карта не нужна ни для регистрации, ни для работы в пределах лимита.
- Нет проблемы «пробуждения» — в отличие от Render, Koyeb (предположительно) и Hugging Face Spaces, воркер Cloudflare не контейнер, который засыпает: он поднимается на edge за миллисечунды, что критично для сценария «игрок ждёт ответа на экране съёмки».
- `fetch()` к `api.anthropic.com` работает без ограничений сверх общего лимита в 50 подзапросов на вызов — для одного обращения к Anthropic этого более чем достаточно.
- Для второго обработчика (игровые события) не нужно поднимать отдельную базу — хватает D1 (5 млн строк чтения и 100 000 строк записи в сутки бесплатно) или, если хранить совсем просто, KV.

Единственная реальная плата за это решение — писать обработчик не на Python, а на JS/TS, раз Python Workers остаётся открытой бетой без заявленной готовности к продакшену.

**Если писать обязательно на Python** — второй по пригодности вариант это **PythonAnywhere**: бесплатный Beginner-план не «спит», как Render, и в белом списке разрешённых внешних адресов уже официально числится `api.anthropic.com`. Ограничение — 100 секунд CPU-времени в сутки; при лёгкой нагрузке (десятки коротких запросов в сутки, основное время которых уходит на ожидание ответа Anthropic по сети, а не на процессор) шансы уложиться высоки, но точных данных о том, засчитывается ли сетевое ожидание против этой квоты, найти не удалось — стоит проверить эмпирически перед тем, как полагаться на этот вариант.

**Если проект перерастёт объём «нескольких сотен обращений» и потребуется полноценный контролируемый сервер** — следующая ступень это **Oracle Cloud Always Free** (2 vCPU/12 ГБ ОЗУ бессрочно, но требует карту для верификации и своего администрирования), а если и это не подойдёт — обычный VPS от **€5,91/мес** (Netcup VPS 500 G12) как чистая точка отсчёта «сколько стоит не выкручиваться».

**Не подходят вовсе для этой задачи**: Fly.io (нет бесплатного уровня, карта обязательна с первого часа), Railway (не бессрочный план, только 30-дневный триал), Vercel Hobby (прямой запрет на коммерческое использование), Hugging Face Spaces (Docker/FastAPI Space недоступен бесплатно с 2026 года без плана PRO за $9/мес).

## Источники

- Cloudflare Workers pricing — https://developers.cloudflare.com/workers/platform/pricing/
- Cloudflare Workers limits — https://developers.cloudflare.com/workers/platform/limits/
- Cloudflare Python Workers — https://developers.cloudflare.com/workers/languages/python/
- Cloudflare Workers fetch API — https://developers.cloudflare.com/workers/runtime-apis/fetch/
- Cloudflare Workers KV limits — https://developers.cloudflare.com/kv/platform/limits/
- Cloudflare D1 limits — https://developers.cloudflare.com/d1/platform/limits/
- Cloudflare D1 pricing — https://developers.cloudflare.com/d1/platform/pricing/
- Cloudflare plans (карта не нужна) — https://www.cloudflare.com/plans/
- Cloudflare AI Gateway — https://developers.cloudflare.com/ai-gateway/
- Cloudflare AI Gateway authentication (BYOK) — https://developers.cloudflare.com/ai-gateway/configuration/authentication/
- Deno Deploy pricing — https://deno.com/deploy/pricing
- Fly.io pricing — https://fly.io/docs/about/pricing/
- Render free plan docs — https://render.com/docs/free
- Railway pricing — https://railway.com/pricing
- Railway pricing reference — https://docs.railway.com/reference/pricing
- Koyeb pricing — https://www.koyeb.com/pricing
- Koyeb docs — https://www.koyeb.com/docs
- Vercel pricing — https://vercel.com/pricing
- Vercel Hobby plan docs — https://vercel.com/docs/plans/hobby
- Hugging Face Spaces overview — https://huggingface.co/docs/hub/spaces-overview
- Hugging Face pricing — https://huggingface.co/pricing
- Oracle Cloud Always Free resources — https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm
- Oracle Cloud Free Tier (FAQ о карте, простое аккаунта) — https://www.oracle.com/cloud/free/
- PythonAnywhere pricing — https://www.pythonanywhere.com/pricing/
- PythonAnywhere whitelist — https://www.pythonanywhere.com/whitelist/
- Hetzner Cloud — https://www.hetzner.com/cloud/ (цены сверены по открытому JSON https://www.hetzner.com/_resources/app/data/bench/cloud_data.json, который подгружает сама эта страница)
- Netcup VPS — https://www.netcup.com/en/server/vps

