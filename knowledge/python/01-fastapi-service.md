# FastAPI-узел приёма снимка кота: сервис, версии, развёртывание

Дата сбора сведений: 2026-08-24.

Проверенные номера версий (по PyPI / официальным страницам, дата обращения 2026-08-24):

| Пакет | Версия | Дата выпуска | Источник |
|---|---|---|---|
| Python | 3.14.7 (последняя стабильная) | 2026-08-05 | [python.org/downloads](https://www.python.org/downloads/) |
| FastAPI | 0.141.1 | 2026-07-29 | [pypi.org/project/fastapi](https://pypi.org/project/fastapi/) |
| Pydantic | 2.13.4 | 2026-05-06 | [pypi.org/project/pydantic](https://pypi.org/project/pydantic/) |
| Uvicorn | 0.52.4 | 2026-08-19 | [pypi.org/project/uvicorn](https://pypi.org/project/uvicorn/) |
| pydantic-settings | 2.15.0 | 2026-08-07 | [pypi.org/project/pydantic-settings](https://pypi.org/project/pydantic-settings/) |
| httpx | 0.28.1 | 2024-12-06 | [pypi.org/project/httpx](https://pypi.org/project/httpx/) |
| httpx2 (форк, см. ниже) | 2.12.0 | 2026-08-18 | [pypi.org/project/httpx2](https://pypi.org/project/httpx2/) |

## Кратко

- Последняя стабильная версия Python на дату сбора — 3.14.7 (5 августа 2026), для сервера разумно взять 3.12 или 3.13 (обе ещё в статусе активной поддержки безопасности до октября 2027–2028) либо саму 3.14, если нет ограничений совместимости зависимостей — [python.org/downloads](https://www.python.org/downloads/).
- FastAPI 0.141.1, Pydantic 2.13.4, Uvicorn 0.52.4, pydantic-settings 2.15.0 — все подтверждены по PyPI на 2026-08-24.
- Библиотека httpx давно не получала стабильных релизов (последний — 0.28.1 от 6 декабря 2024, при этом ветка 1.0 остаётся в статусе dev-версий); в 2026 году компания Pydantic Services Inc. взяла на себя сопровождение форка **httpx2** как прямого продолжения того же API — это существенный факт, который нужно учитывать при выборе клиента для обращения к облачной модели. Подробности ниже, в разделе «Асинхронность».
- Pydantic v2 (в отличие от v1) использует `field_validator`/`model_validator` вместо `validator`, ужесточает преобразование типов (например, float в int теперь не проходит, если есть дробная часть) и заменяет `parse_raw` на `model_validate_json`.
- Для перечислимых значений (черты окраса кота) в Pydantic v2 используются `Literal` или `enum.Enum` — оба поддерживаются как типы полей моделей.
- Приём изображения возможно устроить как через `UploadFile` (multipart/form-data), так и через base64-строку в теле JSON; у каждого способа свои плюсы, ограничение размера тела запроса нужно явно выставлять и на уровне Uvicorn/приложения, и на уровне nginx (`client_max_body_size`).
- В FastAPI решение `async def` или `def` зависит от того, блокирующий вызов внутри или нет: блокирующий код внутри `async def` останавливает весь обработчик событий, а не только один запрос.
- Секреты (ключ облачной модели) не должны попадать в репозиторий — для этого используется `pydantic-settings` и переменные окружения, а не литералы в коде.
- Для одной машины типовая схема — Uvicorn с несколькими worker-процессами (или Gunicorn с uvicorn-worker) за nginx как обратным посредником, под управлением systemd.

## Версии на август 2026

Последняя стабильная версия Python — **3.14.7**, выпущена 5 августа 2026. Действующие сопровождаемые ветки на дату проверки: 3.14 (полное сопровождение, ошибки), 3.13 (полное сопровождение), 3.12, 3.11 и 3.10 (только исправления безопасности, для 3.10 сопровождение заканчивается в октябре 2026) — [python.org/downloads](https://www.python.org/downloads/). Для серверного узла-посредника разумны 3.12 или 3.13 — они уже прошли обкатку экосистемой пакетов и не находятся на самом краю выпуска, либо сама 3.14, если совместимость зависимостей (FastAPI, Pydantic, httpx) подтверждена — прямого источника, «какую версию рекомендует сама FastAPI», в ходе сбора не найдено, поэтому это вывод из общих сроков сопровождения, а не цитата.

FastAPI: последняя версия **0.141.1**, выпущена 29 июля 2026, с частым темпом выпуска (несколько версий 0.140.x выходили в течение нескольких дней подряд) — [pypi.org/project/fastapi](https://pypi.org/project/fastapi/).

Pydantic: последняя версия **2.13.4**, выпущена 6 мая 2026; в примечаниях к выпуску упомянуты правки, связанные с сохранением метаданных `RootModel` и работой флагов компоновщика pydantic-core на macOS — [pypi.org/project/pydantic](https://pypi.org/project/pydantic/).

Uvicorn: последняя версия **0.52.4**, выпущена 19 августа 2026 — [pypi.org/project/uvicorn](https://pypi.org/project/uvicorn/).

pydantic-settings: последняя версия **2.15.0**, выпущена 7 августа 2026 — [pypi.org/project/pydantic-settings](https://pypi.org/project/pydantic-settings/).

httpx: последняя стабильная версия **0.28.1** от 6 декабря 2024 — с этой даты вышли только пред-релизы ветки 1.0 (`1.0.dev1`…`1.0.dev5`, последний — 21 августа 2026), стабильного релиза 1.0 на дату сбора нет — [pypi.org/project/httpx#history](https://pypi.org/project/httpx/#history), [github.com/encode/httpx/releases](https://github.com/encode/httpx/releases). Отдельно на PyPI существует пакет **httpx2** версии 2.12.0 (18 августа 2026), который описан как сопровождаемый компанией Pydantic Services Inc. прямой продолжатель того же API, а не переписывание с нуля — [pypi.org/project/httpx2](https://pypi.org/project/httpx2/). Это значимый факт для проекта: если требуется активно сопровождаемый асинхронный HTTP-клиент, стоит явно проверить состояние httpx2 (или дождавшиеся релиза httpx 1.0) перед закладкой в зависимости, а не полагаться на «httpx» по умолчанию без проверки.
## Pydantic v2 против v1

Официальное руководство по переходу указывает несколько отличий, важных для узла-посредника.

Строгие типы и преобразование: в v1 «whenever a field was annotated as `int`, any float value would be accepted», в v2 «type conversion from floats to integers is only allowed if the decimal part is zero» — [pydantic.dev/…/migration](https://pydantic.dev/docs/validation/latest/get-started/migration/).

Валидаторы: декоратор `@validator` признан устаревшим, «`@validator` has been deprecated, and should be replaced with `@field_validator`»; новый декоратор не принимает `each_item`, а в сигнатуру функции валидатора больше нельзя добавлять аргументы `field` или `config`; `TypeError` внутри валидатора больше не превращается автоматически в `ValidationError` — [pydantic.dev/…/migration](https://pydantic.dev/docs/validation/latest/get-started/migration/).

Пример `field_validator` (режим «after», применяется по умолчанию после стандартной валидации поля) — [pydantic.dev/…/validators](https://pydantic.dev/docs/validation/latest/concepts/validators/):

```python
from pydantic import BaseModel, ValidationError, field_validator

class Model(BaseModel):
    number: int

    @field_validator('number', mode='after')
    @classmethod
    def is_even(cls, value: int) -> int:
        if value % 2 == 1:
            raise ValueError(f'{value} is not an even number')
        return value
```

Пример `model_validator` для проверки согласованности нескольких полей сразу — [pydantic.dev/…/validators](https://pydantic.dev/docs/validation/latest/concepts/validators/):

```python
from typing_extensions import Self
from pydantic import BaseModel, model_validator

class UserModel(BaseModel):
    username: str
    password: str
    password_repeat: str

    @model_validator(mode='after')
    def check_passwords_match(self) -> Self:
        if self.password != self.password_repeat:
            raise ValueError('Passwords do not match')
        return self
```

Разбор JSON: `parse_raw` заменён на `model_validate_json` — «In Pydantic V2, `model_validate_json` works like `parse_raw`» — [pydantic.dev/…/migration](https://pydantic.dev/docs/validation/latest/get-started/migration/). Пример использования (обратите внимание, что при `strict=True` строка даты и список всё равно корректно приводятся к `date` и `tuple` именно потому, что это разбор JSON, а не произвольных данных) — [pydantic.dev/…/json](https://pydantic.dev/docs/validation/latest/concepts/json/):

```python
from datetime import date

from pydantic import BaseModel, ConfigDict, ValidationError


class Event(BaseModel):
  model_config = ConfigDict(strict=True)

  when: date
  where: tuple[int, int]


json_data = '{"when": "1987-01-28", "where": [51, -1]}'
print(Event.model_validate_json(json_data))
#> when=datetime.date(1987, 1, 28) where=(51, -1)
```

Для перечислимых значений (черты окраса кота: например, набор фиксированных строк «tabby», «solid», «calico» и т. п.) в Pydantic v2 можно использовать `Literal` или `enum.Enum` как тип поля — оба варианта поддерживаются напрямую — [pydantic.dev/…/standard_library_types](https://pydantic.dev/docs/validation/latest/api/standard_library_types/):

```python
from enum import Enum, IntEnum
from pydantic import BaseModel, ValidationError

class FruitEnum(str, Enum):
    PEAR = 'pear'
    BANANA = 'banana'

class ToolEnum(IntEnum):
    SPANNER = 1
    WRENCH = 2

class CookingModel(BaseModel):
    fruit: FruitEnum = FruitEnum.PEAR
    tool: ToolEnum = ToolEnum.SPANNER

print(CookingModel())
print(CookingModel(tool=2, fruit='banana'))
```

```python
from typing import Literal
from pydantic import BaseModel, ValidationError

class Pie(BaseModel):
    flavor: Literal['apple', 'pumpkin']
    quantity: Literal[1, 2] = 1

Pie(flavor='apple')
Pie(flavor='pumpkin')
```

Для узла-посредника `Literal` предпочтительнее там, где набор строго фиксирован и не должен превращаться в отдельный тип с собственным пространством имён, а `Enum` — там, где значения переиспользуются в нескольких моделях или нужна проверка через `isinstance`.

## Минимальное рабочее приложение FastAPI

Модель запроса задаётся как обычный класс `BaseModel`, а обработчик POST принимает её как параметр — FastAPI сам читает и разбирает тело JSON, проверяет данные и формирует схему OpenAPI — [fastapi.tiangolo.com/tutorial/body](https://fastapi.tiangolo.com/tutorial/body/):

```python
from fastapi import FastAPI
from pydantic import BaseModel

class Item(BaseModel):
    name: str
    description: str | None = None
    price: float
    tax: float | None = None

app = FastAPI()

@app.post("/items/")
async def create_item(item: Item):
    return item
```

Обработка ошибок — через `HTTPException` с явным кодом состояния и телом `detail` (может быть строкой, словарём или списком) — [fastapi.tiangolo.com/tutorial/handling-errors](https://fastapi.tiangolo.com/tutorial/handling-errors/):

```python
from fastapi import FastAPI, HTTPException

app = FastAPI()
items = {"foo": "The Foo Wrestlers"}

@app.get("/items/{item_id}")
async def read_item(item_id: str):
    if item_id not in items:
        raise HTTPException(status_code=404, detail="Item not found")
    return {"item": items[item_id]}
```

Раскрывать исключение нужно через `raise`, а не `return` — это немедленно прерывает обработку запроса и отправляет ошибку клиенту; коды 400–499 обозначают ошибку клиента — [fastapi.tiangolo.com/tutorial/handling-errors](https://fastapi.tiangolo.com/tutorial/handling-errors/).

Свой класс исключения и отдельный обработчик под него — тот же источник:

```python
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

class UnicornException(Exception):
    def __init__(self, name: str):
        self.name = name

app = FastAPI()

@app.exception_handler(UnicornException)
async def unicorn_exception_handler(request: Request, exc: UnicornException):
    return JSONResponse(
        status_code=418,
        content={"message": f"Oops! {exc.name} did something. There goes a rainbow..."},
    )

@app.get("/unicorns/{name}")
async def read_unicorn(name: str):
    if name == "yolo":
        raise UnicornException(name=name)
    return {"unicorn_name": name}
```

Переопределение обработчика ошибок валидации запроса (`RequestValidationError`) с собственным телом ответа — тот же источник:

```python
from fastapi import FastAPI, HTTPException
from fastapi.exceptions import RequestValidationError
from fastapi.responses import PlainTextResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

app = FastAPI()

@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request, exc: RequestValidationError):
    message = "Validation errors:"
    for error in exc.errors():
        message += f"\nField: {error['loc']}, Error: {error['msg']}"
    return PlainTextResponse(message, status_code=400)

@app.get("/items/{item_id}")
async def read_item(item_id: int):
    if item_id == 3:
        raise HTTPException(status_code=418, detail="Nope! I don't like 3.")
    return {"item_id": item_id}
```

Для узла `/traits` из этого напрямую следует шаблон: модель запроса с полем изображения и метаданными устройства, модель ответа с перечислимыми чертами окраса (`Literal`/`Enum`), обработчик `POST`, который при ошибке облачной модели или неверном формате изображения поднимает `HTTPException` с кодом 400/422/502, а не молча возвращает пустой ответ.

## Приём изображения: base64 в JSON против UploadFile

Официальная документация FastAPI описывает два способа приёма файлов — [fastapi.tiangolo.com/tutorial/request-files](https://fastapi.tiangolo.com/tutorial/request-files/):

Через `bytes` — файл целиком читается в память; подходит только для небольших файлов, простой, но требователен к памяти.

Через `UploadFile` — используется «spooled»-файл (хранится в памяти до определённого предела, затем переносится на диск), доступны метаданные (`filename`, `content_type`), асинхронный файлоподобный интерфейс, можно передавать напрямую в библиотеки, ожидающие файлоподобный объект. Так как `UploadFile` устроен через `multipart/form-data`, важное ограничение: «нельзя одновременно объявлять параметры `File`/`Form` и поля JSON `Body` в одном запросе» — [fastapi.tiangolo.com/tutorial/request-files](https://fastapi.tiangolo.com/tutorial/request-files/). Для установки требуется зависимость `python-multipart`.

```python
from typing import Annotated
from fastapi import FastAPI, File, UploadFile

app = FastAPI()

@app.post("/files/")
async def create_file(file: Annotated[bytes, File()]):
    return {"file_size": len(file)}
```

```python
@app.post("/uploadfile/")
async def create_upload_file(file: UploadFile):
    return {"filename": file.filename}
```

Для узла `/traits`, где снимок передаётся вместе с метаданными устройства и, возможно, HMAC-подписью (см. файл 03), практичнее одно тело JSON, где изображение закодировано в base64 отдельным строковым полем модели `BaseModel` — это укладывается в один `Content-Type: application/json` без переключения на `multipart/form-data`, упрощает подпись всего тела целиком и не требует `python-multipart`. Обратная сторона — base64 увеличивает объём передаваемых данных примерно на треть по сравнению с двоичным представлением, и всё изображение целиком должно быть в памяти как часть разобранной модели Pydantic ещё до того, как код разработчика получит управление. `UploadFile` эффективнее по памяти для крупных файлов и удобнее, когда изображение — единственная полезная нагрузка запроса, но плохо сочетается с общей HMAC-подписью тела и require `python-multipart`.

Ограничение размера тела запроса нужно выставлять минимум на двух уровнях, так как ни один из них не подменяет другой:

На уровне nginx как обратного посредника — директива `client_max_body_size`, по умолчанию `1m`; при превышении клиенту возвращается код 413 (Request Entity Too Large); значение `0` полностью отключает проверку — [nginx.org/…/client_max_body_size](https://nginx.org/en/docs/http/ngx_http_core_module.html#client_max_body_size):

```nginx
# Allow request bodies up to 10 megabytes
client_max_body_size 10m;

# Disable size checking
client_max_body_size 0;
```

Поскольку снимок обрезан до 512×512 и передаётся как несжатое или слабо сжатое base64-изображение, разумный потолок тела запроса — порядка нескольких мегабайт (например, 5–10 Мбайт с запасом), но точное число не установлено ни в одном официальном источнике и должно быть определено экспериментально исходя из формата и качества сжатия конкретного снимка — «надёжного источника с готовым числом для этого случая не найдено».

На уровне самого приложения FastAPI/Starlette в найденных источниках отдельного простого параметра ограничения размера тела на уровне Uvicorn/FastAPI при прямом обращении к WebFetch подтвердить не удалось — официальная страница `uvicorn.org/deployment` была недоступна из среды сбора (см. раздел «Развёртывание»); на практике для узла-посредника это означает, что необходимо не полагаться только на nginx, а дополнительно проверять длину/размер декодированного base64 внутри валидатора Pydantic (например, через `field_validator`, отклоняющий значение до передачи в облачную модель), поскольку прямого источника про встроенный лимит тела в самом Starlette/FastAPI в рамках этого сбора не подтверждено.

## Асинхронность: async def, def и обращение к облаку

Официальное руководство FastAPI формулирует правило выбора так — [fastapi.tiangolo.com/async](https://fastapi.tiangolo.com/async/):

Используйте `async def`, если сторонняя библиотека требует вызова через `await`:

```python
@app.get('/')
async def read_results():
    results = await some_library()
    return results
```

Используйте обычный `def`, если сторонняя библиотека, с которой идёт обмен данными (база данных, API, файловая система), не поддерживает `await` («this is currently the case for most database libraries»):

```python
@app.get('/')
def results():
    results = some_library()
    return results
```

«Если ваше приложение (каким-то образом) не должно ни с чем взаимодействовать и ждать ответа, используйте `async def`, даже если внутри не нужен `await`»; а «если вы просто не знаете — используйте обычный `def`» — [fastapi.tiangolo.com/async](https://fastapi.tiangolo.com/async/).

Ключевая опасность — блокирующий вызов внутри `async def`: «в этих случаях лучше использовать `async def`, если только функции обработки пути не выполняют блокирующий ввод-вывод» — то есть блокирующий код внутри `async def` без `await` останавливает весь цикл обработки событий (и, соответственно, все параллельные запросы этого worker-процесса), а не только текущий запрос — [fastapi.tiangolo.com/async](https://fastapi.tiangolo.com/async/). Для узла `/traits` это значит: если обращение к облачной модели идёт синхронным клиентом (`requests` или синхронный `httpx.Client`) внутри `async def`, весь процесс встанет на время ожидания ответа облака.

Правильный способ ходить в облако из `async def` — асинхронный клиент `httpx.AsyncClient`. Управление таймаутами — по умолчанию httpx поднимает `TimeoutException` после 5 секунд бездействия сети; таймаут можно тонко разбить на составляющие (`connect`, `read`, `write`, `pool`) — [python-httpx.org/advanced/timeouts](https://www.python-httpx.org/advanced/timeouts/):

```python
httpx.get('http://example.com/api/v1/example', timeout=10.0)
```

```python
timeout = httpx.Timeout(10.0, connect=60.0)
client = httpx.Client(timeout=timeout)
response = client.get('http://example.com/')
```

Повторные попытки на уровне транспорта — `HTTPTransport(retries=N)` повторяет запрос при `httpx.ConnectError` или `httpx.ConnectTimeout` («allowing smoother operation under flaky networks»), но не при ошибках чтения/записи и не при кодах состояния вида 503 — для этого документация прямо отсылает к общим библиотекам вроде `tenacity` — [python-httpx.org/advanced/transports](https://www.python-httpx.org/advanced/transports/):

```python
import httpx
transport = httpx.HTTPTransport(retries=1)
client = httpx.Client(transport=transport)
```

В официальной странице `python-httpx.org/api` параметр `retries` в перечне параметров `Client` не упомянут — управление повторами делается именно через `transport`, а не напрямую через клиент — [python-httpx.org/api](https://www.python-httpx.org/api/). Учитывая, что стабильная библиотека `httpx` не обновлялась с декабря 2024 года, а версия 1.0 остаётся в статусе dev-релизов, при выборе зависимости для обращения к облаку стоит явно решить и зафиксировать: остаться на `httpx` 0.28.1, перейти на пред-релиз 1.0 или на поддерживаемый компанией Pydantic форк `httpx2` — сведения об этом см. в разделе «Версии» выше.

## Настройки и секреты

`pydantic-settings` (последняя версия 2.15.0 от 7 августа 2026, отдельный пакет от основного `pydantic` — [pypi.org/project/pydantic-settings](https://pypi.org/project/pydantic-settings/)) читает значения полей из переменных окружения при создании модели, если они не переданы явно как именованные аргументы: «If you create a model that inherits from `BaseSettings`, the model initialiser will attempt to determine the values of any fields not passed as keyword arguments by reading from the environment» — [pydantic.dev/…/pydantic_settings](https://pydantic.dev/docs/validation/latest/concepts/pydantic_settings/):

```python
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

class Settings(BaseSettings):
    auth_key: str = Field(validation_alias='my_auth_key')
    redis_dsn: str = 'redis://user:pass@localhost:6379/1'

    model_config = SettingsConfigDict(env_prefix='my_prefix_')

settings = Settings()
```

Приоритет источников (по убыванию): аргументы инициализации переопределяют переменные окружения, те переопределяют значения из `.env`-файла, а те — значения из файлов секретов; сложные типы (списки, словари, вложенные модели) разбираются из переменных окружения как JSON, если не задан свой разбор через валидатор — [pydantic.dev/…/pydantic_settings](https://pydantic.dev/docs/validation/latest/concepts/pydantic_settings/).

Для узла-посредника практический вывод: ключ облачной модели (`CLOUD_API_KEY` или подобное имя) и общий секрет для HMAC-подписи (см. файл 03) должны объявляться как поля `BaseSettings` и читаться из переменных окружения процесса (например, из юнита systemd через `EnvironmentFile=`), а не как строковые литералы в исходном коде — тогда ключ физически не попадает в репозиторий и не может быть случайно закоммичен вместе с кодом. Файл `.env` с реальными значениями должен быть добавлен в `.gitignore`, а в репозитории может лежать только `.env.example` с именами переменных без значений — это общая практика, отдельного официального источника именно на этот совет в ходе сбора не открывалось, поэтому отмечается как практика, а не как цитата.

## Развёртывание на одной машине

Официальная документация FastAPI описывает запуск нескольких worker-процессов через параметр `--workers` у команды `fastapi` или напрямую у `uvicorn`: запускается несколько worker-процессов (например, 4), родительский процесс выступает диспетчером, у каждого worker — свой PID, это даёт параллельное выполнение на нескольких ядрах и обслуживание большего числа запросов одновременно — [fastapi.tiangolo.com/deployment/server-workers](https://fastapi.tiangolo.com/deployment/server-workers/):

```bash
fastapi run --workers 4 main.py
```

```bash
uvicorn main:app --host 0.0.0.0 --port 8080 --workers 4
```

Там же отмечено, что использование worker-процессов закрывает только аспект «репликации», а вопросы HTTPS, автозапуска, перезапусков и управления памятью остаются на стороне развёртывания; для комплексных случаев FastAPI рекомендует контейнеры (Docker/Kubernetes) — [fastapi.tiangolo.com/deployment/server-workers](https://fastapi.tiangolo.com/deployment/server-workers/). Для одной машины с небольшой нагрузкой (обработка снимков кота — не высокочастотная операция) контейнеризация не обязательна, но сам принцип «несколько worker-процессов + перезапуск через systemd» применим и без контейнера.

Отдельный пакет **uvicorn-worker** (последняя версия 0.4.0, 20 сентября 2025) — это класс worker для Gunicorn, объединяющий производительность Uvicorn с управлением процессами Gunicorn (перезапуск без простоя, штатное завершение и т. п.): «The Uvicorn Worker is a package designed for the mature and comprehensive server and process manager, Gunicorn» — [pypi.org/project/uvicorn-worker](https://pypi.org/project/uvicorn-worker/). Он используется как `gunicorn app:app -k uvicorn_worker.UvicornWorker` — сам официальный пример команды взят из описания пакета на PyPI и общей идеи класса worker, а не процитирован дословно построчно из отдельного руководства.

Официальную страницу `uvicorn.org/deployment` (равно как и `www.uvicorn.org` и `uvicorn.dev`) в ходе этого сбора открыть через WebFetch не удалось — домен не резолвился либо возвращал ошибку 403/404 из среды сбора. Поэтому подробности про конкретный шаблон unit-файла systemd, аргументы `--proxy-headers`/`--forwarded-allow-ips` и настройку Supervisor **не подтверждены прямым обращением к первоисточнику** и здесь не приводятся как факт — «надёжного источника (успешно открытого через WebFetch) с этими подробностями не найдено». Общий принцип, не зависящий от точной страницы документации: unit-файл systemd описывает команду запуска (`ExecStart=`, например тот же `gunicorn`/`uvicorn` с нужными аргументами), переменные окружения через `EnvironmentFile=`, политику перезапуска (`Restart=on-failure`) и пользователя, под которым выполняется процесс, а nginx перед ним слушает порт 80/443 и проксирует запросы на локальный сокет или порт, обычно с передачей заголовков `X-Forwarded-For`/`X-Forwarded-Proto`; для маленькой нагрузки узла-посредника это означает, что двух-четырёх worker-процессов Uvicorn достаточно, а более сложная схема с балансировщиком не требуется.

## Журналирование и наблюдаемость

Стандартная встроенная библиотека `logging` — минимальный вариант для узла с небольшой нагрузкой: настроить один обработчик на stdout (который затем собирает systemd/journald) и логировать как минимум входящие запросы к `/traits`, коды ответа облачной модели и исключения. Для того, что именно стоит записывать в контексте предотвращения злоупотреблений (частота обращений, идентификатор устройства, попытки подмены подписи), см. файл `03-ratelimit-and-signing.md`, раздел про журналирование, где это подкреплено ссылкой на OWASP Logging Cheat Sheet. Отдельного официального руководства именно от FastAPI/Uvicorn по журналированию за пределами базовой конфигурации access-log Uvicorn в ходе этого сбора не открывалось, поэтому детальные рекомендации по структурированному журналированию (например, через `structlog`) здесь не приводятся как подтверждённые — «надёжных источников не найдено».

## Источники

- [python.org/downloads](https://www.python.org/downloads/) — версия Python и статус сопровождения веток
- [pypi.org/project/fastapi](https://pypi.org/project/fastapi/) — версия FastAPI
- [pypi.org/project/pydantic](https://pypi.org/project/pydantic/) — версия Pydantic
- [pypi.org/project/uvicorn](https://pypi.org/project/uvicorn/) — версия Uvicorn
- [pypi.org/project/pydantic-settings](https://pypi.org/project/pydantic-settings/) — версия pydantic-settings
- [pypi.org/project/httpx](https://pypi.org/project/httpx/) и [pypi.org/project/httpx/#history](https://pypi.org/project/httpx/#history) — версия и история релизов httpx
- [github.com/encode/httpx/releases](https://github.com/encode/httpx/releases) — история релизов httpx
- [pypi.org/project/httpx2](https://pypi.org/project/httpx2/) — форк httpx2 под управлением Pydantic Services Inc.
- [pypi.org/project/uvicorn-worker](https://pypi.org/project/uvicorn-worker/) — пакет uvicorn-worker для Gunicorn
- [pydantic.dev/docs/validation/latest/get-started/migration](https://pydantic.dev/docs/validation/latest/get-started/migration/) — различия Pydantic v1 и v2
- [pydantic.dev/docs/validation/latest/concepts/validators](https://pydantic.dev/docs/validation/latest/concepts/validators/) — field_validator и model_validator
- [pydantic.dev/docs/validation/latest/concepts/json](https://pydantic.dev/docs/validation/latest/concepts/json/) — model_validate_json
- [pydantic.dev/docs/validation/latest/api/standard_library_types](https://pydantic.dev/docs/validation/latest/api/standard_library_types/) — Enum и Literal как типы полей
- [fastapi.tiangolo.com/tutorial/body](https://fastapi.tiangolo.com/tutorial/body/) — модель запроса и обработчик POST
- [fastapi.tiangolo.com/tutorial/handling-errors](https://fastapi.tiangolo.com/tutorial/handling-errors/) — HTTPException и обработчики ошибок
- [fastapi.tiangolo.com/tutorial/request-files](https://fastapi.tiangolo.com/tutorial/request-files/) — UploadFile и bytes
- [fastapi.tiangolo.com/async](https://fastapi.tiangolo.com/async/) — правило выбора async def/def
- [fastapi.tiangolo.com/deployment/server-workers](https://fastapi.tiangolo.com/deployment/server-workers/) — запуск нескольких worker-процессов
- [nginx.org/en/docs/http/ngx_http_core_module.html#client_max_body_size](https://nginx.org/en/docs/http/ngx_http_core_module.html#client_max_body_size) — ограничение размера тела запроса в nginx
- [python-httpx.org/advanced/timeouts](https://www.python-httpx.org/advanced/timeouts/) — управление таймаутами httpx
- [python-httpx.org/advanced/transports](https://www.python-httpx.org/advanced/transports/) — повторные попытки через HTTPTransport
- [python-httpx.org/api](https://www.python-httpx.org/api/) — параметры Client (проверка отсутствия retries на уровне Client)







