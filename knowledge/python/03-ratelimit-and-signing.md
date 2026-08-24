# Ограничение частоты обращений и подпись запросов для узла-посредника

Дата сбора сведений: 2026-08-24.

Проверенные номера версий (по PyPI/GitHub, дата обращения 2026-08-24):

| Пакет | Версия | Дата выпуска | Источник |
|---|---|---|---|
| slowapi | 0.1.10 | 2026-06-13 | [pypi.org/project/slowapi](https://pypi.org/project/slowapi/) |
| fastapi-limiter | v0.2.0 (год выпуска не подтверждён прямым обращением) | «06 Feb» | [github.com/long2ice/fastapi-limiter/releases](https://github.com/long2ice/fastapi-limiter/releases) |

## Кратко

- **slowapi** активно поддерживается на дату сбора: последний выпуск 0.1.10 от 13 июня 2026, описан как «a rate limiting library for Starlette and FastAPI adapted from flask-limiter» — [pypi.org/project/slowapi](https://pypi.org/project/slowapi/).
- **fastapi-limiter** (long2ice) на GitHub показывает последний релиз с тегом v0.2.0; точный год выпуска через открытые страницы подтвердить не удалось (страница отдаёт дату без года) — это стоит трактовать как сигнал возможной низкой активности сопровождения и проверить самостоятельно перед выбором в проект.
- Для «10 обращений в сутки на устройство» подходит скользящее окно (sliding window) или ведро с жетонами (token bucket) на Redis с ключом на основе идентификатора устройства, а не IP-адреса — привязка к IP не годится для мобильного клиента (смена сети, NAT у оператора).
- HMAC-SHA256 — стандартная библиотека `hmac` в Python: `hmac.new(key, msg, digestmod)` для вычисления и обязательно `hmac.compare_digest(a, b)` для сравнения — обычное сравнение `==` уязвимо к атакам по времени выполнения.
- Подписывать нужно не только тело запроса, а тело + метку времени + идентификатор устройства вместе — это одновременно защищает от повторной отправки (replay), если метка времени проверяется на устаревание, а nonce — от повтора в пределах допустимого окна времени.
- В C# та же схема реализуется через `System.Security.Cryptography.HMACSHA256` — принцип идентичен, отличаются только API вызова.
- Общий секрет, зашитый в клиентское приложение, извлекается из сборки при должных усилиях — это не защита от целеустремлённого злоумышленника, а барьер против массового автоматического злоупотребления и случайного копирования.
- Apple предоставляет отдельный, гораздо более сильный механизм — App Attest (часть DeviceCheck) — для подтверждения того, что запрос идёт от подлинного экземпляра приложения на подлинном устройстве Apple, без общего секрета в бинарном файле.
- В журнал стоит писать факты обращения (идентификатор устройства, метку времени, результат проверки подписи и лимита), но не сам секрет, не саму подпись как раскрывающее значение и не полезную нагрузку целиком, если она может содержать чувствительные данные.

## Ограничение частоты обращений в FastAPI

**slowapi** — обёртка над библиотекой `limits`, адаптация `flask-limiter` под Starlette/FastAPI; поддерживает бэкенды redis, memcached и память (память — как запасной вариант), декораторы лимита на отдельные обработчики и общие лимиты на группу маршрутов, работает и с синхронными, и с асинхронными обработчиками — «a rate limiting library for Starlette and FastAPI adapted from flask-limiter»; последний выпуск 0.1.10 от 13 июня 2026 — [pypi.org/project/slowapi](https://pypi.org/project/slowapi/). Официальный пример настройки — [slowapi.readthedocs.io](https://slowapi.readthedocs.io/en/latest/):

```python
from fastapi import FastAPI
from slowapi import Limiter, _rate_limit_exceeded_handler
from slowapi.util import get_remote_address
from slowapi.errors import RateLimitExceeded

limiter = Limiter(key_func=get_remote_address)
app = FastAPI()
app.state.limiter = limiter
app.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)

@app.get("/home")
@limiter.limit("5/minute")
async def homepage(request: Request):
    return PlainTextResponse("test")
```

Важное ограничение из документации: в обработчик обязательно нужно явно передавать параметр `request` — «the request argument must be explicitly passed to your endpoint, or slowapi won't be able to hook into it», иначе slowapi не сможет подключиться к запросу; WebSocket-обработчики пока не поддерживаются — [pypi.org/project/slowapi](https://pypi.org/project/slowapi/) (описание пакета). Параметр `key_func` в примере — `get_remote_address`, то есть лимит по умолчанию завязан на IP-адрес; для лимита «на устройство» вместо `get_remote_address` нужна собственная функция, читающая идентификатор устройства из заголовка или подписанного тела запроса, например:

```python
def device_id_key(request: Request) -> str:
    return request.headers.get("X-Device-Id", "unknown")

limiter = Limiter(key_func=device_id_key)
```

Это не дословный пример из документации slowapi (в открытых страницах документации готового примера с ключом не по IP найти не удалось — «дословного примера в первоисточнике не найдено»), а составленный по документированному параметру `key_func` код, соответствующий его назначению.

**fastapi-limiter** (long2ice) — «A request rate limiter for fastapi... powered by pyrate-limiter»; предоставляет зависимость `RateLimiter`, а также `RateLimiterMiddleware` для лимита сразу на все маршруты без добавления зависимости к каждому — [github.com/long2ice/fastapi-limiter](https://github.com/long2ice/fastapi-limiter). По умолчанию идентификатор — «ip + path», но документация явно говорит, что его можно переопределить, например на `userid`: «Identifier of route limit, default is `ip + path`, you can override it such as `userid` and so on» — тот же источник. Последний релиз на странице релизов помечен тегом v0.2.0 с описанием изменения «use lifespan» — [github.com/long2ice/fastapi-limiter/releases](https://github.com/long2ice/fastapi-limiter/releases); дата показана без года («06 Feb»), поэтому точный год выпуска в рамках этого сбора не подтверждён — это стоит перепроверить перед использованием пакета в проекте, а не считать его однозначно свежим.

Обе библиотеки в рассмотренных источниках не сравнивались друг с другом по активности сопровождения впрямую; на основе того, что удалось открыть, slowapi показывает более свежую и понятную по дате историю релизов (2026 год явно виден), а у fastapi-limiter это не подтверждено напрямую — по этой причине для нового проекта предпочтительнее slowapi, если не появится более свежих данных о fastapi-limiter.

## Алгоритмы: скользящее окно и ведро с жетонами

По материалам глоссария Redis о рате-лимитировании — [redis.io/glossary/rate-limiting](https://redis.io/glossary/rate-limiting/):

Скользящее окно (sliding window) отслеживает количество запросов за недавний промежуток времени через окно, которое непрерывно сдвигается: «this algorithm tracks the number of requests received in the recent past using a sliding window that moves over time»; оно гибче фиксированного окна и лучше подстраивается под всплески трафика, но менее эффективно против устойчивой продолжительной атаки — тот же источник.

Ведро с жетонами (token bucket) поддерживает «ведро», которое пополняется жетонами с фиксированной скоростью; каждый запрос тратит один жетон, когда жетоны заканчиваются — запросы отклоняются: «this maintains a token bucket that is refilled at a fixed rate. Each request consumes a token, and additional requests are denied once the bucket is empty»; хорошо справляется с всплесками (можно накопить и разом потратить жетоны), но тоже не рассчитан на устойчивую продолжительную нагрузку — тот же источник.

Там же описан практический способ реализации простого ограничителя на Redis через `INCR`+`EXPIRE` внутри `MULTI`/`EXEC` (атомарная транзакция): ключ строится как «идентификатор клиента + номер минуты», при первом обращении в минуте `INCR` возвращает 1, ключ истекает через время окна — [redis.io/glossary/rate-limiting](https://redis.io/glossary/rate-limiting/):

```
MULTI
  INCR [user-api-key]:[current minute number]
  EXPIRE [user-api-key]:[current minute number] 59
EXEC
```

Для требования «10 обращений в сутки на устройство» это по сути частный случай скользящего или фиксированного окна с очень большим периодом (сутки) и низким лимитом. Токен-бакет здесь избыточен: его сильная сторона — сглаживание кратковременных всплесков при высокой частоте (десятки/сотни запросов в секунду), а при лимите «10 в сутки» самих запросов в принципе так мало, что разница между алгоритмами на практике не ощущается, а более простой и предсказуемый вариант — фиксированное или скользящее окно на сутки с ключом Redis вида `device:{device_id}:{date}` (аналог показанной выше схемы, но с окном в сутки вместо минуты) и счётчиком через `INCR`+`EXPIRE`. Точное сравнение именно для случая «10 запросов в сутки» отдельно ни в одном найденном источнике не рассматривается — этот вывод сделан из общих описанных свойств алгоритмов, а не процитирован напрямую.

## Подпись запроса общим секретом: HMAC-SHA256

Стандартная библиотека Python `hmac`: `hmac.new(key, msg=None, digestmod)` возвращает новый объект HMAC; параметр `key` — байты или `bytearray`; начиная с версии 3.8 параметр `digestmod` обязателен — [docs.python.org/3/library/hmac.html](https://docs.python.org/3/library/hmac.html).

Для сравнения вычисленного значения с присланным клиентом документация прямо рекомендует не оператор `==`, а `hmac.compare_digest`, устойчивый к атакам по времени выполнения (не прерывает сравнение по первому несовпадающему байту): «When comparing the output of digest() or hexdigest() to an externally supplied digest during a verification routine, it is recommended to use the compare_digest() function instead of the == operator to reduce the vulnerability to timing attacks» — [docs.python.org/3/library/hmac.html](https://docs.python.org/3/library/hmac.html).

Что подписывать: тело запроса само по себе недостаточно — без метки времени подпись остаётся действительной вечно (можно повторно отправить перехваченный запрос), без идентификатора устройства нельзя привязать лимит и подпись к конкретному источнику. Практический пример на Python (составлен на основе задокументированного API `hmac.new`/`hmac.compare_digest`, а не процитирован как единый готовый пример из одного источника — конкретно такого сквозного примера с телом+меткой времени+идентификатором устройства в открытых страницах не найдено):

```python
import hmac
import hashlib
import time

SHARED_SECRET = b"..."  # только из переменной окружения, см. файл 01

def sign_request(body: bytes, device_id: str, timestamp: str, nonce: str) -> str:
    message = body + b"|" + device_id.encode() + b"|" + timestamp.encode() + b"|" + nonce.encode()
    digest = hmac.new(SHARED_SECRET, message, hashlib.sha256)
    return digest.hexdigest()

def verify_request(body: bytes, device_id: str, timestamp: str, nonce: str, signature: str) -> bool:
    expected = sign_request(body, device_id, timestamp, nonce)
    return hmac.compare_digest(expected, signature)
```

Защита от повторной отправки (replay) складывается из двух независимых проверок на сервере:

Метка времени — сервер отклоняет запрос, если метка времени слишком старая или слишком сильно расходится с текущим временем сервера (например, больше нескольких минут в любую сторону); это ограничивает окно, в течение которого перехваченный запрос вообще можно повторно отправить.

Nonce (одноразовое значение) — сервер запоминает уже виденные пары (идентификатор устройства, nonce) в пределах допустимого окна времени метки и отклоняет повтор той же пары; это закрывает саму возможность повторной отправки в пределах допустимого окна времени, которую одна лишь метка времени не устраняет. Отдельного официального источника именно с этой парой «метка времени + nonce» под конкретно эту задачу в ходе сбора не открывалось — описанная схема является общей практикой защиты от replay-атак, а не цитатой из одного документа.

На стороне C# та же схема считается через `System.Security.Cryptography.HMACSHA256`: конструктор принимает ключ как массив байт, метод `ComputeHash` вычисляет HMAC от массива байт или потока — [learn.microsoft.com/…/HMACSHA256](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256?view=net-10.0). Официальный пример Microsoft показывает подпись и проверку файла целиком через `FileStream`:

```csharp
using (HMACSHA256 hmac = new HMACSHA256(key))
{
    using (FileStream inStream = new FileStream(sourceFile, FileMode.Open))
    {
        using (FileStream outStream = new FileStream(destFile, FileMode.Create))
        {
            byte[] hashValue = hmac.ComputeHash(inStream);
            inStream.Position = 0;
            outStream.Write(hashValue, 0, hashValue.Length);
        }
    }
}
```

Там же поясняется общий принцип: «An HMAC can be used to determine whether a message sent over an insecure channel has been tampered with, provided that the sender and receiver share a secret key. The sender computes the hash value for the original data and sends both the original data and hash value as a single message. The receiver recalculates the hash value on the received message and checks that the computed HMAC matches the transmitted HMAC» — [learn.microsoft.com/…/HMACSHA256](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256?view=net-10.0). Для строки (а не файла) — на стороне игрового клиента, где подписывается тело запроса + метка времени + идентификатор устройства, а не файл — конструкция аналогична: строку нужно перевести в байты (`Encoding.UTF8.GetBytes`, тем же способом, что и на стороне Python-сервера — `str.encode()`), передать в `ComputeHash`, а полученные байты — представить в том же формате (например, шестнадцатеричная строка в нижнем регистре), что и на сервере, иначе сравнение не сойдётся из-за разного представления, а не из-за разного ключа или порядка байт.

Ключевое условие совместимости между Python и C#: порядок конкатенации полей сообщения (тело, разделитель, идентификатор устройства, метка времени, nonce), кодировка текста (UTF-8 с обеих сторон) и формат вывода подписи (hex-строка, обычно в нижнем регистре) должны быть определены один раз как протокол и одинаково реализованы на обеих сторонах — сама библиотека HMAC этого не гарантирует и не проверяет.

## Честное предупреждение: общий секрет в приложении извлекается из сборки

Общий секрет, зашитый в код или ресурсы мобильного приложения (в том числе обфусцированный), в принципе извлекаем: любой, кто получит установочный файл, может статически или динамически (отладчиком, перехватом памяти во время выполнения) достать байты ключа, после чего сможет формировать собственные корректно подписанные запросы неограниченно. Это не выдуманное утверждение с конкретной методикой взлома — прямого источника с пошаговой инструкцией в рамках этого сбора намеренно не искалось и не приводится, — но сам факт извлекаемости секрета из клиентского бинарного файла является общеизвестным свойством модели «секрет на устройстве», а не особенностью какой-то конкретной библиотеки.

Что HMAC-подпись общим секретом реально даёт: барьер против случайного или массового автоматического злоупотребления (боты, которые не разбирали конкретно это приложение), защиту целостности запроса в пути (тело не подменить на лету, не зная секрета) и привязку конкретного запроса к конкретному устройству и моменту времени в сочетании с проверками из предыдущего раздела. Чего она не даёт: защиты от целеустремлённого злоумышленника, который декомпилировал именно это приложение и извлёк секрет — против такого злоумышленника HMAC на общем секрете эквивалентен отсутствию защиты вообще, так как он может подписывать запросы неотличимо от настоящего клиента.

Apple предоставляет отдельный, принципиально более сильный механизм именно для этого случая — **App Attest**, часть платформы DeviceCheck. Официальная страница — [developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity](https://developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity); в ходе этого сбора WebFetch смог получить только заголовок страницы («Establishing your app's integrity | Apple Developer Documentation») — сама страница построена на JavaScript и не отдаёт текстовое содержимое обычному получателю HTML, поэтому подробности ниже не процитированы дословно из этой страницы, а взяты из открытого поиска по связанным материалам (в том числе официальным страницам Apple «Preparing to use the app attest service» и обсуждениям на WWDC), и должны считаться менее строго подтверждёнными, чем остальные факты в этих трёх файлах — «дословно первоисточник открыть не удалось, за фактами — см. ссылки ниже».

По собранным (но не процитированным дословно) сведениям: приватный ключ создаётся внутри Secure Enclave устройства и никогда не покидает его — его нельзя прочитать, экспортировать или скопировать; служба `DCAppAttestService` создаёт на устройстве пару ключей, приложение передаёт запрос на подтверждение (attestation) в серверы Apple, которые возвращают объект аттестации, включающий цепочку сертификатов, доказывающую, что ключ создан на подлинном оборудовании Apple. Проверка этого объекта должна выполняться на сервере разработчика, а не в самом приложении — «attestation should always be validated by your server, and not the app». Готового API от Apple для самой этой проверки нет — разработчик должен реализовать разбор формата CBOR и проверку цепочки сертификатов X.509 самостоятельно. Ссылки на официальные страницы Apple по теме (заголовки подтверждены через WebFetch, полное содержимое — нет):

- [developer.apple.com/documentation/devicecheck](https://developer.apple.com/documentation/devicecheck) — общая страница DeviceCheck
- [developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity](https://developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity) — подтверждение подлинности приложения
- [developer.apple.com/documentation/devicecheck/validating-apps-that-connect-to-your-server](https://developer.apple.com/documentation/devicecheck/validating-apps-that-connect-to-your-server) — проверка на стороне сервера
- [developer.apple.com/documentation/devicecheck/dcappattestservice](https://developer.apple.com/documentation/devicecheck/dcappattestservice) — сам класс DCAppAttestService

Для узла-посредника практический вывод: HMAC на общем секрете — приемлемый первый рубеж (быстро, просто, не требует изменений на стороне Apple/Google), но если требуется защита от целеустремлённого злоумышленника с декомпилированным приложением, а не только от массового автоматического трафика, стоит отдельно и подробно изучить App Attest (для iOS) непосредственно по указанным официальным страницам Apple, так как в рамках этого сбора их содержимое подтверждено только на уровне заголовков, а не текста.

## Что записывать в журнал, чтобы заметить злоупотребление

По материалам OWASP Logging Cheat Sheet — что стоит фиксировать всегда: успехи и неудачи проверки подлинности («authentication successes and failures»), отказы авторизации, ошибки проверки входных данных («input validation failures e.g. protocol violations, unacceptable encodings, invalid parameter names and values»), подозрительные попытки обойти ограничения бизнес-логики или превысить допустимые пределы действий, а также запуски и остановки приложения и изменения конфигурации — [cheatsheetseries.owasp.org/…/Logging_Cheat_Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html).

Там же — что никогда не должно попадать в журнал напрямую: пароли аутентификации, значения идентификаторов сессии (при необходимости — только хешированные), токены доступа, ключи шифрования и прочие основные секреты, платёжные данные, чувствительные персональные данные — «never log data unless it is legally sanctioned»; отдельно подчёркнута необходимость очищать данные события перед записью, чтобы исключить внедрение в журнал через символы возврата каретки/перевода строки и другие разделители — тот же источник.

Применительно к узлу `/traits` и защите от злоупотреблений это означает записывать в журнал как минимум: идентификатор устройства (не сам общий секрет и не саму подпись как значение, годное для повторного использования), метку времени запроса, результат проверки HMAC-подписи (прошла/не прошла, без деталей вычисления), результат проверки ограничения частоты (в пределах лимита/превышен, с текущим счётчиком), код ответа облачной модели или факт ошибки при обращении к ней, и — при отклонении запроса — причину отклонения (просроченная метка времени, повтор nonce, превышение лимита, неверная подпись) как отдельные категории событий, а не как единственное общее «ошибка». Само тело изображения в base64 в журнал писать не нужно — это не требование безопасности из процитированного источника впрямую, а прямое следствие общего принципа «не логировать чувствительные/объёмные пользовательские данные», применённого к конкретному случаю снимка устройства пользователя.

## Источники

- [pypi.org/project/slowapi](https://pypi.org/project/slowapi/) — версия и описание slowapi
- [slowapi.readthedocs.io/en/latest](https://slowapi.readthedocs.io/en/latest/) — пример настройки Limiter
- [github.com/long2ice/fastapi-limiter](https://github.com/long2ice/fastapi-limiter) — описание и параметр идентификатора fastapi-limiter
- [github.com/long2ice/fastapi-limiter/releases](https://github.com/long2ice/fastapi-limiter/releases) — история релизов fastapi-limiter
- [redis.io/glossary/rate-limiting](https://redis.io/glossary/rate-limiting/) — алгоритмы sliding window и token bucket, пример на INCR/EXPIRE
- [docs.python.org/3/library/hmac.html](https://docs.python.org/3/library/hmac.html) — hmac.new и hmac.compare_digest
- [learn.microsoft.com/…/HMACSHA256](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256?view=net-10.0) — HMACSHA256 в C#
- [cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html) — что логировать и что не логировать
- [developer.apple.com/documentation/devicecheck](https://developer.apple.com/documentation/devicecheck) — DeviceCheck (открыт только заголовок страницы)
- [developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity](https://developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity) — App Attest, подтверждение подлинности приложения (открыт только заголовок страницы)
- [developer.apple.com/documentation/devicecheck/validating-apps-that-connect-to-your-server](https://developer.apple.com/documentation/devicecheck/validating-apps-that-connect-to-your-server) — серверная проверка (открыт только заголовок страницы)
- [developer.apple.com/documentation/devicecheck/dcappattestservice](https://developer.apple.com/documentation/devicecheck/dcappattestservice) — DCAppAttestService (открыт только заголовок страницы)






