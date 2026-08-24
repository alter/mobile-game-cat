# Cloudflare Workers — узел-посредник для обращения к Anthropic (2026-08-24)

Рабочая база знаний по применению Cloudflare Workers для одного маленького обработчика: приём фотографии кота от игры на iOS, обращение к Anthropic Messages API, возврат короткого JSON. Источник — официальная документация developers.cloudflare.com/workers/ (проверено дословными цитатами), плюс обсуждения разработчиков и открытые issues на GitHub там, где документация не даёт прямого ответа.

## Кратко

- Файл настроек — **`wrangler.jsonc`**: документация прямо называет его форматом «для новых проектов» и уточняет, что часть новых возможностей Wrangler будет доступна только в JSON-настройках. Текущая версия `wrangler` в npm на дату проверки — **4.125.0**.
- Ключевой вывод по времени процессора: официальная документация Cloudflare прямо говорит, что **ожидание сетевого ответа (`fetch()`, чтение KV, запрос к базе) не засчитывается в лимит процессорного времени (CPU time)**. Значит бесплатный уровень с лимитом 10 мс CPU на запрос в принципе не мешает секундному ожиданию ответа модели зрения — считается только время, когда Worker реально занимает процессор (разбор JSON, декодирование base64 и т.п.).
- Ключ Anthropic кладётся командой `wrangler secret put ANTHROPIC_API_KEY` — секрет не хранится в файле настроек и не попадает в репозиторий; в отличие от обычных `vars`, значение секрета скрыто даже в кабинете Cloudflare после создания.
- Поддомен вида `имя.поддомен.workers.dev` выдаётся бесплатно сразу при первом развёртывании и годится для обращений из приложения технически, но Cloudflare прямо не советует его для «business-critical» нагрузки — для нескольких сотен обращений за всё время это не помеха.
- Правила ограничения частоты в кабинете Cloudflare (WAF Rate limiting rules) работают на уровне зоны (собственного домена, подключённого к Cloudflare); программный способ ограничить частоту прямо в коде Worker — привязка (binding) Rate Limiting, настраивается в `wrangler.jsonc` без явного указания зависимости от тарифа.
- D1 и Workers KV на бесплатном тарифе с большим запасом хватает под второй обработчик игровых событий при объёме «сотни обращений»: D1 — 5 ГБ на аккаунт и 50 подзапросов на вызов Worker; KV — 100 000 операций чтения и 1000 операций записи разных ключей в сутки.
- `wrangler tail` даёт журнал в реальном времени бесплатно на любом тарифе; Workers Logs в кабинете на бесплатном тарифе хранит записи 3 дня и принимает до 200 000 событий в сутки.
- Размер тела запроса ограничен в первую очередь планом самого аккаунта Cloudflare (100 МБ на Free), а не Workers — фотография до 200 КБ в base64 (около 270 КБ после кодирования) укладывается с огромным запасом.
- Итог по применимости бесплатного уровня: для нашей задачи — годится. Единственный риск — не сетевое ожидание, а реальная процессорная работа (декодирование base64, сборка/разбор JSON) внутри лимита 10 мс; при сотнях запросов в принципе стоит один раз проверить фактический расход через `wrangler tail`, и при нехватке — перейти на платный тариф Workers ($5/мес, 30 секунд CPU по умолчанию) без изменения кода.

## 1. С нуля до выложенного обработчика

Источники: developers.cloudflare.com/workers/get-started/guide/, developers.cloudflare.com/workers/wrangler/configuration/, developers.cloudflare.com/workers/wrangler/commands/.

Точная последовательность команд:

```sh
# 1. Создание проекта (устанавливает wrangler локально в проект через npm create)
npm create cloudflare@latest -- my-first-worker

# 2. Переход в папку проекта
cd my-first-worker

# 3. Локальная проверка (при первом запуске откроет браузер для входа в аккаунт Cloudflare)
npx wrangler dev

# 4. Развёртывание в Cloudflare
npx wrangler deploy
```

Отдельный явный вход в аккаунт (если нужен заранее, а не при первом `wrangler dev`):

```sh
wrangler login
```

По документации: «Authorize Wrangler with your Cloudflare account using OAuth.» Есть также `wrangler logout` — «Remove Wrangler's authorization for accessing your account. This command will invalidate your current OAuth token and delete the stored credentials.» — и `wrangler whoami` для проверки текущего входа.

**Файл настроек.** После `npm create cloudflare@latest` генератор (C3) создаёт `wrangler.jsonc`: «C3 will have generated the following: `wrangler.jsonc`: Your Wrangler configuration file.» Документация по конфигурации прямо рекомендует именно этот формат: «Cloudflare recommends using `wrangler.jsonc` for new projects, and some newer Wrangler features will only be available to projects using a JSON config file.» Старый `wrangler.toml` при этом продолжает поддерживаться (начиная с Wrangler 3.91.0 оба формата работают параллельно), но для нового проекта нет причины брать TOML.

Минимальная конфигурация в `wrangler.jsonc`:

```jsonc
{
	"name": "cat-traits-proxy",
	"main": "src/index.js",
	"compatibility_date": "2026-08-24"
}
```

Текущая версия `wrangler` в npm на момент проверки: `4.125.0` (тег `latest`).
## 2. Устройство обработчика

Источники: developers.cloudflare.com/workers/runtime-apis/handlers/fetch/, developers.cloudflare.com/workers/runtime-apis/request/, developers.cloudflare.com/workers/runtime-apis/response/.

Современный вид модуля Worker — экспорт объекта по умолчанию с методом `fetch`, принимающим три аргумента: запрос, привязки окружения (`env`) и контекст выполнения (`ctx`):

```js
export default {
	async fetch(request, env, ctx) {
		// request — стандартный объект Request Web-платформы
		// env — доступ к секретам, переменным и привязкам (KV, D1, Rate Limiting и т.д.)
		// ctx — например, ctx.waitUntil(promise) для фоновой работы после ответа
		return new Response("ok");
	},
};
```

Разбор задачи под наш обработчик `/traits` укладывается в такую последовательность внутри `fetch`:

1. Проверить метод: `if (request.method !== "POST") return new Response(null, { status: 405 });`
2. Проверить путь: `new URL(request.url).pathname === "/traits"`.
3. Прочитать тело как JSON: `const body = await request.json();` — при ошибке разбора (не-JSON тело) `request.json()` выбрасывает исключение, которое нужно поймать и вернуть `400`.
4. Проверить размер входа до передачи дальше (мы задаём собственный предел — 200 КБ по условию задачи, что заметно меньше системных лимитов, см. раздел 5) — так как `Content-Length` от клиента доверять нельзя, безопаснее проверять длину уже прочитанной строки base64.
5. Обратиться к Anthropic через `fetch()` (раздел 4).
6. Вернуть Worker'ом compact JSON нужной формы и правильный код состояния (`200` — успех, `400` — некорректный вход, `413` — превышен предельный размер, `502` — ошибка при обращении к Anthropic, `429` — сработало ограничение частоты).

Ответ формируется через стандартный конструктор `Response`:

```js
return new Response(JSON.stringify({ ok: true, traits }), {
	status: 200,
	headers: { "content-type": "application/json" },
});
```

Полный рабочий образец под задачу — приём base64-снимка и обращение к Anthropic — приведён отдельным разделом ниже («Полный образец обработчика `/traits`»).
## 3. Секреты

Источник: developers.cloudflare.com/workers/configuration/secrets/.

Команда для добавления секрета (создаёт новую версию Worker и разворачивает её немедленно):

```sh
npx wrangler secret put ANTHROPIC_API_KEY
```

Wrangler запросит значение интерактивно (значение не остаётся в истории оболочки). Доступ к секрету в коде — через параметр `env`:

```js
export default {
	async fetch(request, env, ctx) {
		const apiKey = env.ANTHROPIC_API_KEY;
		// ...
	},
};
```

**Почему нельзя писать ключ в `vars` файла настроек.** Документация прямо предупреждает: «Do not use `vars` to store sensitive information in your Worker's Wrangler configuration file. Use secrets instead.» Обычные `vars` хранятся в открытом виде прямо в `wrangler.jsonc` — то есть при коммите этого файла ключ уйдёт в git. Разница между секретом и переменной зафиксирована документацией дословно: «The difference is secret values are not visible within Wrangler or Cloudflare dashboard after you define them» — то есть секрет не только не в файле настроек, но и не отображается обратно даже в кабинете после того, как его один раз ввели.

Для локальной разработки (`wrangler dev`) секреты кладутся в файл `.dev.vars` (или `.env`) рядом с файлом настроек — но этот файл обязательно исключается из версионирования: «The `.dev.vars` and `.env` files should not be committed to git. Add `.dev.vars*` and `.env*` to your project's `.gitignore` file.»

Итог: ключ Anthropic никогда не должен появляться ни в `wrangler.jsonc`, ни в самом коде Worker — только через `wrangler secret put` в проде и `.dev.vars` (в `.gitignore`) локально. При таком порядке ключ на устройство игрока не попадает вообще — он существует только на стороне Cloudflare.
## 4. Обращение к стороннему API — ключевой вопрос про CPU time

Источник: developers.cloudflare.com/workers/platform/limits/, developers.cloudflare.com/workers/runtime-apis/fetch/.

Вызов делается обычным `fetch()` из тела обработчика:

```js
const anthropicResponse = await fetch("https://api.anthropic.com/v1/messages", {
	method: "POST",
	headers: {
		"content-type": "application/json",
		"x-api-key": env.ANTHROPIC_API_KEY,
		"anthropic-version": "2023-06-01",
	},
	body: JSON.stringify(payload),
});
```

**Ожидание сетевого ответа НЕ входит в лимит процессорного времени.** Документация Cloudflare даёт это прямым текстом: время ожидания ответа сети (в том числе вызовы `fetch()`, чтение из KV, запросы к базе данных) не засчитывается в CPU time — «Waiting on network requests (such as `fetch()` calls, KV reads, or database queries) does not count toward CPU time.» Это разделение двух разных величин:

- **CPU time (процессорное время)** — считается только активная работа процессора самим Worker'ом: разбор JSON, сериализация, декодирование base64, любые вычисления в JS. Именно на эту величину действует лимит 10 мс на бесплатном тарифе.
- **Wall time (время „по часам“)** — полное время от начала до конца обработки запроса, включая ожидание сети, ввод-вывод и прочие асинхронные операции: «Wall time (also called wall-clock time) is the total elapsed time from the start to end of an invocation, including time spent waiting on network requests, I/O, and other asynchronous operations.» Для HTTP-обработчиков документация отдельно уточняет отсутствие жёсткого предела по wall time: «There is no hard limit on duration for HTTP-triggered Workers. As long as the client remains connected, the Worker can continue processing…»

Из этого прямо следует ответ на вопрос задачи: **секундное (и дольше) ожидание ответа модели зрения от Anthropic не расходует бюджет 10 мс процессорного времени бесплатного тарифа Workers Free.** Единственное, что расходует этот бюджет, — сама работа Worker'а до и после ожидания: чтение и разбор входящего JSON с base64-строкой (до ~270 КБ после кодирования), сборка тела запроса к Anthropic, разбор ответа и сборка ~100-байтного ответа игре. Это существенно меньше, чем, например, хеширование паролей (в реальных обсуждениях разработчиков именно вычислительно тяжёлые операции вроде scrypt/bcrypt регулярно упираются в лимит 10 мс на Free — см. раздел «Подводные камни»), но декодирование base64 объёмом в сотни килобайт — тоже не бесплатная операция, и после первого развёртывания стоит один раз посмотреть фактический расход CPU через `wrangler tail` (поле `cpuTime` в структурированном выводе).

**Ограничения на исходящие обращения:**

- Одновременных открытых исходящих подключений — **6** (`developers.cloudflare.com/workers/platform/limits/`) — для нашей задачи это не помеха: на один входящий запрос требуется одно исходящее обращение к `api.anthropic.com`.
- Подзапросов (subrequests) на один вызов Worker: **50 на Workers Free**, **до 10 000 на Workers Paid** (при отдельной настройке — до 10 000 000). Один `fetch()` к Anthropic — это один подзапрос, так что запас на Free-тарифе более чем достаточен.
- Время запуска скрипта (startup time) — до **1 секунды** и на Free, и на Paid тарифе; это отдельный от CPU time лимит на инициализацию модуля (импорты, код верхнего уровня) — не на обработку запроса.

Таким образом, ответ на вопрос задачи прямой: **ожидание ответа сети НЕ входит в CPU time**, и это подтверждено официальной документацией дословно. Бесплатный уровень для описанного узла годится.
## 5. Пределы

Источник: developers.cloudflare.com/workers/platform/limits/.

| Показатель | Workers Free | Workers Paid |
|---|---|---|
| Размер тела запроса (зависит от плана Cloudflare-аккаунта, не от плана Workers) | 100 МБ | 100 МБ (Pro), 200 МБ (Business), 500 МБ (Enterprise по умолчанию) |
| Размер скрипта Worker, сжатый | 3 МБ | 10 МБ |
| Размер скрипта Worker, несжатый | 64 МБ | 64 МБ |
| CPU time на HTTP-запрос | 10 мс | 30 секунд по умолчанию, до 5 минут по настройке |
| Wall time (длительность) на HTTP-запрос | нет жёсткого предела, пока клиент подключён | нет жёсткого предела, пока клиент подключён |
| Время запуска скрипта (startup time) | 1 секунда | 1 секунда |
| Одновременных открытых исходящих подключений | 6 | 6 |
| Подзапросов (subrequests) на вызов | 50 | 10 000 (до 10 000 000 по настройке) |
| Число Worker-скриптов на аккаунт | 100 | 500 |
| Память на изолят | 128 МБ | 128 МБ |
| Дневной лимит запросов к Worker | 100 000 в сутки | без ограничения |

Для нашей задачи (снимок до 200 КБ в base64, ответ около 100 байт, сотни обращений за всё время) ни один из этих пределов не является узким местом — даже дневной лимит в 100 000 запросов на Free с большим запасом перекрывает ожидаемую нагрузку.

## 6. Ограничение частоты

Источники: developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/, developers.cloudflare.com/waf/rate-limiting-rules/.

**Способ 1 — Rate Limiting binding (программно, внутри кода Worker).** Настраивается в `wrangler.jsonc`:

```jsonc
{
	"name": "cat-traits-proxy",
	"main": "src/index.js",
	"compatibility_date": "2026-08-24",
	"ratelimits": [
		{
			"name": "MY_RATE_LIMITER",
			"namespace_id": "1001",
			"simple": {
				"limit": 100,
				"period": 60
			}
		}
	]
}
```

Использование в обработчике:

```js
export default {
	async fetch(request, env) {
		const { pathname } = new URL(request.url);
		const { success } = await env.MY_RATE_LIMITER.limit({ key: pathname });
		if (!success) {
			return new Response("429 Rate limit exceeded", { status: 429 });
		}
		return new Response("Success!");
	},
};
```

Требование по версии инструмента: «You must use version 4.36.0 or later of the Wrangler CLI.» Параметр `period` документация ограничивает жёстко: «Must be either 10 or 60» (только 10 или 60 секунд — не произвольный период). Документация не содержит явного указания на ограничение доступности этой привязки по тарифу — упоминаний слова «Free» или «Paid» применительно именно к самой возможности использования Rate Limiting binding на странице нет, поэтому по тарифной доступности здесь — данных не найдено, ограничение только по версии Wrangler.

Чтобы получить требуемое «не больше N обращений с устройства в сутки», ключом (`key` в `limit({ key })`) стоит взять устойчивый идентификатор устройства (например, значение, которое присылает игра в теле или заголовке запроса), а не IP — IP-адреса мобильных операторов часто общие на многих пользователей. Поскольку `period` жёстко ограничен 10 или 60 секундами, а не сутками, суточный лимит на одном этом механизме не собрать напрямую — постоянного окна в 86 400 секунд Rate Limiting binding не поддерживает; для суточного лимита нужен либо счётчик поверх KV/D1 с собственной логикой окна, либо смириться с ограничением на минуту (100 обращений в минуту с одного идентификатора практически исключает злоупотребление при ожидаемой нагрузке в сотни обращений за всё время).

**Способ 2 — правила в кабинете Cloudflare (WAF Rate limiting rules).** Документация описывает их как настройку «for a zone» — то есть правило создаётся для зоны, зарегистрированной в Cloudflare (собственного домена). На бесплатном тарифе Cloudflare (не Workers, а тариф самого аккаунта/зоны) доступно только 1 правило с узкими условиями: поле для подсчёта — только по IP-адресу, период подсчёта фиксирован — 10 секунд, период блокировки — до 10 секунд, условие выражения — только по пути (Path) или «Verified Bot». Подсчёт по заголовкам, произвольным выражениям и более длинные периоды требуют платных тарифов Cloudflare (Business и выше). Поскольку эти правила настраиваются на уровне зоны, для обращений на голый `*.workers.dev` (без собственного домена, подключённого к Cloudflare как зона) их применимость документацией прямо не подтверждена — данных не найдено; для нашей задачи с малым объёмом обращений практичнее использовать Rate Limiting binding прямо в коде.
## 7. Хранилище для второго обработчика

Источники: developers.cloudflare.com/d1/, developers.cloudflare.com/d1/platform/limits/, developers.cloudflare.com/d1/best-practices/import-export-data/, developers.cloudflare.com/kv/, developers.cloudflare.com/kv/platform/limits/, developers.cloudflare.com/kv/reference/kv-commands/.

### Cloudflare D1 (SQLite)

Создание базы и привязка:

```sh
npx wrangler d1 create [NAME]
```

Параметры: `[NAME]` (обязательный) — имя базы; `--location` — подсказка о географическом расположении (`weur`, `eeur`, `apac`, `oc`, `wnam`, `enam`); `--binding` — имя привязки в Worker.

Выполнение SQL:

```sh
npx wrangler d1 execute [DATABASE] --command "INSERT INTO events (name) VALUES ('spawn')"
npx wrangler d1 execute [DATABASE] --file ./schema.sql --remote
```

Обязателен либо `--command`, либо `--file`; `--local` выполняет запрос против локальной копии, `--remote` — против настоящей удалённой базы D1.

Запись из кода Worker (после привязки `d1_databases` в `wrangler.jsonc`):

```js
export default {
	async fetch(request, env) {
		await env.DB.prepare("INSERT INTO events (name, ts) VALUES (?, ?)")
			.bind("spawn", Date.now())
			.run();
		return new Response("ok");
	},
};
```

Пределы D1: Free — 10 баз данных, до 500 МБ на одну базу, 5 ГБ хранилища на аккаунт, 50 подзапросов на вызов Worker; Paid — 50 000 баз, до 10 ГБ на базу, 1 ТБ на аккаунт, 1000 подзапросов на вызов. Общие для обоих тарифов пределы: строка/BLOB — до 2 000 000 байт (2 МБ), один SQL-запрос — до 100 000 байт (100 КБ). Точных суточных лимитов на число запросов чтения/записи в документации по лимитам не приведено — данных не найдено.

Выгрузка данных — команда `d1 export`:

```sh
npx wrangler d1 export [NAME] --remote --output backup.sql
```

Поддерживаются флаги `--table` (конкретные таблицы), `--no-data` (только схема), `--no-schema` (только данные).

### Workers KV

Создание пространства имён и привязка в `wrangler.jsonc`:

```sh
npx wrangler kv namespace create [NAMESPACE]
```

```jsonc
"kv_namespaces": [
	{ "binding": "KV", "id": "<YOUR_BINDING_ID>" }
]
```

Запись и чтение из кода:

```js
await env.KV.put("KEY", "VALUE");
const value = await env.KV.get("KEY");
await env.KV.delete("KEY");
```

Пределы KV: Free — 100 000 операций чтения в сутки, 1000 операций записи разных ключей в сутки (запись одного и того же ключа — не чаще 1 раза в секунду на обоих тарифах), 1 ГБ хранилища на аккаунт и на пространство имён; Paid — операции чтения/записи без ограничения, хранилище без ограничения. Общие пределы: ключ — до 512 байт, значение — до 25 МиБ, метаданные ключа — до 1024 байт, до 1000 пространств имён на аккаунт, число ключей в пространстве имён — без ограничения.

Выгрузка данных из KV встроенной командой полного экспорта не предусмотрена — есть только точечные операции и пакетные:

```sh
npx wrangler kv key list --namespace-id=<ID>
npx wrangler kv bulk get [FILENAME]
npx wrangler kv bulk put [FILENAME]
```

`kv bulk get` читает список ключей из файла и возвращает пары ключ-значение; сплошного «экспортировать всё одним вызовом» в официальных командах нет — это подтверждается и открытым issue на GitHub (`cloudflare/wrangler`, #1957: «Currently, I have 300k+ keys in a project and I see no way to get this data out of Workers again. Wrangler has a bulk set, but not a bulk read» — по состоянию на дату issue). Для полной выгрузки на практике нужно сначала получить список ключей через `kv key list`, затем прогнать его через `kv bulk get`.

Для второго обработчика (приём игровых событий, некопящих чувствительных данных) любой из двух вариантов подходит: KV — если нужна простая пара ключ-значение и не важна мгновенная согласованность (у KV — распределённая, не строгая, консистентность), D1 — если нужны выборки, агрегаты и SQL по накопленным событиям.
## 8. Журналы и наблюдение

Источники: developers.cloudflare.com/workers/observability/logs/workers-logs/, developers.cloudflare.com/workers/wrangler/commands/ (раздел Workers commands).

**`wrangler tail`** — журнал в реальном времени прямо в терминале:

```sh
npx wrangler tail [WORKER]
```

Основные опции: `--format` (`json` или `pretty`), `--status` (`ok` | `error` | `canceled`), `--header`, `--method`, `--sampling-rate`, `--search`, `--ip`, `--version-id`. Это средство разработчика — доступно вне зависимости от тарифа, показывает поток запросов по мере их обработки (в том числе `console.log` и ошибки), но ничего не сохраняет — сессия обрывается при закрытии терминала.

**Workers Logs** в кабинете Cloudflare — постоянное хранилище журналов с фильтрами и анализом. Доступность на обоих тарифах: Free и Paid. Пределы Free-тарифа: **200 000 логических событий в сутки**, хранение записей — **3 дня**. Paid-тариф: включено 20 миллионов событий в месяц, хранение — 7 дней, сверх лимита — доплата $0.60 за миллион событий. При превышении общего предела в 5 миллиардов записей на аккаунт в сутки система переходит на 1%-ную выборочную запись до конца суток.

Для нашей нагрузки (сотни обращений за всё время) оба инструмента с большим запасом достаточны: `wrangler tail` — для отладки в моменте (в том числе чтобы один раз замерить фактический расход CPU time, см. раздел 4), Workers Logs — чтобы посмотреть историю ошибок за последние 3 дня без дополнительной настройки.

## 9. Своё имя или выданное

Источник: developers.cloudflare.com/workers/configuration/routing/workers-dev/.

Поддомен вида `<имя_воркера>.<поддомен_аккаунта>.workers.dev` выдаётся **бесплатно** сразу при первом развёртывании (`wrangler deploy`), без какой-либо дополнительной настройки и без необходимости иметь собственный домен. Технически он полностью пригоден для обращений из мобильного приложения — это обычный HTTPS-адрес.

При этом документация прямо предупреждает про уместность такого адреса: «It's recommended to run production Workers on a Workers route or custom domain, rather than on your workers.dev subdomain» — и описывает сам workers.dev как предназначенный «for personal or hobby projects that aren't business-critical». Технические ограничения на само имя: до 63 символов, только буквы, цифры и дефис, не может начинаться или заканчиваться дефисом.

Для описанного узла (несколько сотен обращений за всё время, некритичный к простою вспомогательный сервис) начинать с бесплатного `workers.dev`-адреса — разумно и достаточно; переход на собственный домен имеет смысл только если проект вырастет до нагрузки, которую сам разработчик сочтёт «business-critical», либо если понадобится более тонкая настройка на уровне зоны (в частности, из раздела 6 — правила ограничения частоты в кабинете, которые привязаны именно к зоне/собственному домену).
## 10. Подводные камни из обсуждений разработчиков и GitHub issues

- **«Exceeded CPU time limit» на Free-тарифе почти всегда возникает от вычислений, а не от ожидания сети.** Реальный пример — библиотека `better-auth`: «Cloudflare's CPU time isn't sufficient to hash and verify passwords, so email/password login doesn't work well in a worker environment» (issue `better-auth/better-auth#969`), и более поздний случай: «Sign-up fails intermittently with Worker exceeded CPU time limit. The pure JS scrypt from @noble/hashes is right on the edge of Workers' CPU [limit]» (issue `better-auth/better-auth#8860`). Для нашей задачи прямого хеширования нет, но вывод общий: перед тем как полагаться на бесплатный тариф, стоит один раз замерить фактический расход CPU на декодирование base64 и сборку/разбор JSON через `wrangler tail`, а не полагаться только на то, что «сеть не считается».
- **Отдельный от request CPU time лимит — время запуска скрипта (startup time).** На StackOverflow и в issue `cloudflare/workers-sdk#2152` («BUG: Startup script exceeded CPU time limit», код ошибки `10021`) описана путаница: код верхнего уровня модуля (импорты тяжёлых библиотек, инициализация на этапе загрузки) тоже ограничен по CPU и это не то же самое, что лимит на обработку самого запроса. Для нашего маленького обработчика без тяжёлых зависимостей это не должно быть проблемой, но при добавлении крупных npm-пакетов (например, SDK) стоит держать в уме.
- **Локальные счётчики Rate Limiting binding в `wrangler dev` ненадёжны.** Issue `cloudflare/workers-sdk#14962`: «In `wrangler dev`, hit your Worker, pause for ~15 seconds to look at something, hit it again, and your limit has silently reset» — локальные счётчики Rate Limiting binding в режиме разработки могут самопроизвольно сбрасываться. Проверять реальную работу ограничения частоты нужно уже на развёрнутом Worker'е, а не только локально.
- **`wrangler dev --remote` может незаметно унаследовать чужие правила ограничения частоты.** Issue `cloudflare/workers-sdk#9880` описывает, что при удалённой разработке (`--remote`) Worker может неявно попасть под действие правил ограничения частоты, настроенных для зоны первым разработчиком — стоит проверять источник неожиданных `429` при отладке в команде.
- **Секреты для `wrangler dev` нужно класть отдельно, файл настроек их не содержит.** Официальная страница подтверждает это явно (раздел 3), но в сообществе регулярно возникает путаница из-за старых статей, где секреты предлагали класть прямо в `wrangler.toml` (пример — обсуждение на Cloudflare Community «Confusing advice about secrets and Wrangler»); актуальный и единственно верный путь — `wrangler secret put` для продакшена и `.dev.vars` (обязательно в `.gitignore`) для местной разработки.
- **Полной команды экспорта для Workers KV нет.** Как отмечено в разделе 7, issue `cloudflare/wrangler#1957` и последующее обсуждение на Cloudflare Community («Quick(ish) Reliable bulk export of all KV data from a namespace») фиксируют, что штатного способа выгрузить всё пространство имён одним вызовом не существует уже несколько лет — если для второго обработчика (события) в будущем понадобится массовая выгрузка, для этого изначально разумнее выбрать D1 (у него есть `d1 export` целиком в SQL) вместо KV.
- **Правила ограничения частоты в кабинете зависят от зоны.** Как отмечено в разделе 6, документация описывает создание правила именно «for a zone» — для голого `*.workers.dev` без подключённого собственного домена применимость этих правил официально не подтверждена; для минимального узла безопаснее полагаться на встроенную привязку Rate Limiting в коде, а не на кабинетные правила.
## Полный образец обработчика `/traits`

Файл настроек `wrangler.jsonc` (ключ Anthropic сюда не попадает — он кладётся отдельно командой `wrangler secret put ANTHROPIC_API_KEY`, см. раздел 3; привязка ограничения частоты — необязательная часть, см. раздел 6):

```jsonc
{
	"name": "cat-traits-proxy",
	"main": "src/index.js",
	"compatibility_date": "2026-08-24",
	"ratelimits": [
		{
			"name": "TRAITS_RATE_LIMITER",
			"namespace_id": "1001",
			"simple": { "limit": 30, "period": 60 }
		}
	]
}
```

Код обработчика (`src/index.js`). Ограничения из условия задачи: снимок до 512×512, до 200 КБ до кодирования в base64 (примерно до 273 000 символов после кодирования — коэффициент base64 составляет 4/3), модель со зрением, ключ только на стороне Worker, ответ игре — компактный JSON:

```js
const MAX_BASE64_LENGTH = 280_000; // с запасом над 200 КБ * 4/3
const ANTHROPIC_MODEL = "claude-haiku-4-5"; // недорогая модель с поддержкой зрения
const ANTHROPIC_VERSION = "2023-06-01";

export default {
	async fetch(request, env, ctx) {
		const url = new URL(request.url);

		if (url.pathname !== "/traits") {
			return new Response(null, { status: 404 });
		}
		if (request.method !== "POST") {
			return new Response(null, { status: 405 });
		}

		// необязательное ограничение частоты — ключ на идентификатор устройства
		// из заголовка, который проставляет сама игра
		if (env.TRAITS_RATE_LIMITER) {
			const deviceId = request.headers.get("x-device-id") ?? "unknown";
			const { success } = await env.TRAITS_RATE_LIMITER.limit({ key: deviceId });
			if (!success) {
				return new Response(JSON.stringify({ error: "rate_limited" }), {
					status: 429,
					headers: { "content-type": "application/json" },
				});
			}
		}

		let body;
		try {
			body = await request.json();
		} catch {
			return new Response(JSON.stringify({ error: "invalid_json" }), {
				status: 400,
				headers: { "content-type": "application/json" },
			});
		}

		const imageBase64 = body?.image_base64;
		if (typeof imageBase64 !== "string" || imageBase64.length === 0) {
			return new Response(JSON.stringify({ error: "missing_image_base64" }), {
				status: 400,
				headers: { "content-type": "application/json" },
			});
		}
		if (imageBase64.length > MAX_BASE64_LENGTH) {
			return new Response(JSON.stringify({ error: "image_too_large" }), {
				status: 413,
				headers: { "content-type": "application/json" },
			});
		}

		const mediaType = body?.media_type ?? "image/jpeg";

		const anthropicPayload = {
			model: ANTHROPIC_MODEL,
			max_tokens: 300,
			system:
				"Ты определяешь черты окраса кота по фотографии. Ответь ТОЛЬКО json без пояснений, " +
				"строго по схеме: {\"color\":string,\"pattern\":string,\"eyeColor\":string}.",
			messages: [
				{
					role: "user",
					content: [
						{
							type: "image",
							source: { type: "base64", media_type: mediaType, data: imageBase64 },
						},
						{ type: "text", text: "Определи черты окраса кота на фото." },
					],
				},
			],
		};

		let anthropicResponse;
		try {
			anthropicResponse = await fetch("https://api.anthropic.com/v1/messages", {
				method: "POST",
				headers: {
					"content-type": "application/json",
					"x-api-key": env.ANTHROPIC_API_KEY,
					"anthropic-version": ANTHROPIC_VERSION,
				},
				body: JSON.stringify(anthropicPayload),
			});
		} catch (err) {
			return new Response(JSON.stringify({ error: "upstream_unreachable" }), {
				status: 502,
				headers: { "content-type": "application/json" },
			});
		}

		if (!anthropicResponse.ok) {
			return new Response(JSON.stringify({ error: "upstream_error" }), {
				status: 502,
				headers: { "content-type": "application/json" },
			});
		}

		const anthropicJson = await anthropicResponse.json();
		const rawText = anthropicJson?.content?.[0]?.text ?? "";

		let traits;
		try {
			traits = JSON.parse(rawText);
		} catch {
			return new Response(JSON.stringify({ error: "bad_model_output" }), {
				status: 502,
				headers: { "content-type": "application/json" },
			});
		}

		// ничего не сохраняем — сразу отдаём компактный ответ игре (около 100 байт)
		return new Response(JSON.stringify(traits), {
			status: 200,
			headers: { "content-type": "application/json" },
		});
	},
};
```

Развёртывание: `npx wrangler secret put ANTHROPIC_API_KEY`, затем `npx wrangler deploy`. Проверка локально: `npx wrangler dev` и `curl -X POST http://localhost:8787/traits -H "content-type: application/json" -d '{"image_base64":"..."}'`.
## Источники

- https://developers.cloudflare.com/workers/get-started/guide/
- https://developers.cloudflare.com/workers/wrangler/configuration/
- https://developers.cloudflare.com/workers/wrangler/commands/
- https://developers.cloudflare.com/workers/wrangler/commands/workers/
- https://developers.cloudflare.com/workers/wrangler/commands/d1/
- https://developers.cloudflare.com/workers/wrangler/commands/kv/
- https://developers.cloudflare.com/workers/wrangler/commands/general/
- https://developers.cloudflare.com/workers/configuration/secrets/
- https://developers.cloudflare.com/workers/runtime-apis/handlers/fetch/
- https://developers.cloudflare.com/workers/runtime-apis/fetch/
- https://developers.cloudflare.com/workers/platform/limits/
- https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/
- https://developers.cloudflare.com/waf/rate-limiting-rules/
- https://developers.cloudflare.com/d1/
- https://developers.cloudflare.com/d1/platform/limits/
- https://developers.cloudflare.com/d1/best-practices/import-export-data/
- https://developers.cloudflare.com/d1/wrangler-commands/
- https://developers.cloudflare.com/kv/
- https://developers.cloudflare.com/kv/platform/limits/
- https://developers.cloudflare.com/kv/reference/kv-commands/
- https://developers.cloudflare.com/workers/observability/logs/workers-logs/
- https://developers.cloudflare.com/workers/configuration/routing/workers-dev/
- npm registry: `npm view wrangler version` (проверено 2026-08-24, `4.125.0`)
- GitHub issue `better-auth/better-auth#969` — https://github.com/better-auth/better-auth/issues/969
- GitHub issue `better-auth/better-auth#8860` — https://github.com/better-auth/better-auth/issues/8860
- GitHub issue `cloudflare/workers-sdk#2152` — https://github.com/cloudflare/workers-sdk/issues/2152
- GitHub issue `cloudflare/workers-sdk#14962` — https://github.com/cloudflare/workers-sdk/issues/14962
- GitHub issue `cloudflare/workers-sdk#9880` — https://github.com/cloudflare/workers-sdk/issues/9880
- GitHub issue `cloudflare/wrangler#1957` — https://github.com/cloudflare/wrangler/issues/1957
- Anthropic Messages API — структура запроса/заголовков подтверждена внутренним справочником `claude-api` (`curl/examples.md`, идентификаторы текущих моделей Anthropic, дата кеша 2026-06-24).
