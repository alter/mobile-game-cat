# FastAPI node for receiving a cat photo: service, versions, deployment

Date information was gathered: 2026-08-24.

Verified version numbers (per PyPI / official pages, accessed 2026-08-24):

| Package | Version | Release date | Source |
|---|---|---|---|
| Python | 3.14.7 (latest stable) | 2026-08-05 | [python.org/downloads](https://www.python.org/downloads/) |
| FastAPI | 0.141.1 | 2026-07-29 | [pypi.org/project/fastapi](https://pypi.org/project/fastapi/) |
| Pydantic | 2.13.4 | 2026-05-06 | [pypi.org/project/pydantic](https://pypi.org/project/pydantic/) |
| Uvicorn | 0.52.4 | 2026-08-19 | [pypi.org/project/uvicorn](https://pypi.org/project/uvicorn/) |
| pydantic-settings | 2.15.0 | 2026-08-07 | [pypi.org/project/pydantic-settings](https://pypi.org/project/pydantic-settings/) |
| httpx | 0.28.1 | 2024-12-06 | [pypi.org/project/httpx](https://pypi.org/project/httpx/) |
| httpx2 (fork, see below) | 2.12.0 | 2026-08-18 | [pypi.org/project/httpx2](https://pypi.org/project/httpx2/) |

## Summary

- The latest stable version of Python as of the collection date is 3.14.7 (August 5, 2026); for a server it is reasonable to take 3.12 or 3.13 (both still in active security-support status until October 2027-2028), or 3.14 itself if there are no dependency compatibility constraints — [python.org/downloads](https://www.python.org/downloads/).
- FastAPI 0.141.1, Pydantic 2.13.4, Uvicorn 0.52.4, pydantic-settings 2.15.0 — all confirmed against PyPI on 2026-08-24.
- The httpx library has not had a stable release in a long time (the last one — 0.28.1 from December 6, 2024, with the 1.0 branch still in dev-release status); in 2026 the company Pydantic Services Inc. took over maintenance of the **httpx2** fork as a direct continuation of the same API — this is a significant fact to account for when choosing a client for talking to the cloud model. Details below, in the "Asynchrony" section.
- Pydantic v2 (unlike v1) uses `field_validator`/`model_validator` instead of `validator`, tightens type coercion (for example, float to int no longer passes if there is a fractional part), and replaces `parse_raw` with `model_validate_json`.
- For enumerable values (a cat's coat traits) Pydantic v2 uses `Literal` or `enum.Enum` — both are supported as model field types.
- Accepting an image can be arranged either through `UploadFile` (multipart/form-data) or through a base64 string in the JSON body; each approach has its own trade-offs, and the request body size limit needs to be set explicitly both at the Uvicorn/application level and at the nginx level (`client_max_body_size`).
- In FastAPI the choice of `async def` or `def` depends on whether the call inside is blocking: blocking code inside `async def` halts the entire event handler, not just one request.
- Secrets (the cloud model key) must not end up in the repository — `pydantic-settings` and environment variables are used for this instead of literals in the code.
- For a single machine, the typical scheme is Uvicorn with several worker processes (or Gunicorn with uvicorn-worker) behind nginx as the proxy worker, managed by systemd.

## Versions as of August 2026

The latest stable version of Python is **3.14.7**, released August 5, 2026. Maintained branches as of the verification date: 3.14 (full support, bug fixes), 3.13 (full support), 3.12, 3.11 and 3.10 (security fixes only, support for 3.10 ends in October 2026) — [python.org/downloads](https://www.python.org/downloads/). For a server proxy worker, 3.12 or 3.13 are reasonable — they have already been broken in by the package ecosystem and are not on the very edge of a release, or 3.14 itself, if dependency compatibility (FastAPI, Pydantic, httpx) is confirmed — no direct source stating "which version FastAPI itself recommends" was found during collection, so this is a conclusion drawn from general support timelines, not a quotation.

FastAPI: latest version **0.141.1**, released July 29, 2026, with a frequent release cadence (several 0.140.x versions came out on consecutive days) — [pypi.org/project/fastapi](https://pypi.org/project/fastapi/).

Pydantic: latest version **2.13.4**, released May 6, 2026; the release notes mention fixes related to preserving `RootModel` metadata and pydantic-core linker flag behavior on macOS — [pypi.org/project/pydantic](https://pypi.org/project/pydantic/).

Uvicorn: latest version **0.52.4**, released August 19, 2026 — [pypi.org/project/uvicorn](https://pypi.org/project/uvicorn/).

pydantic-settings: latest version **2.15.0**, released August 7, 2026 — [pypi.org/project/pydantic-settings](https://pypi.org/project/pydantic-settings/).

httpx: latest stable version **0.28.1** from December 6, 2024 — since that date only pre-releases of the 1.0 branch have come out (`1.0.dev1`…`1.0.dev5`, the latest — August 21, 2026); there is no stable 1.0 release as of the collection date — [pypi.org/project/httpx#history](https://pypi.org/project/httpx/#history), [github.com/encode/httpx/releases](https://github.com/encode/httpx/releases). Separately, on PyPI there is a package **httpx2** version 2.12.0 (August 18, 2026), described as a direct continuation of the same API maintained by Pydantic Services Inc., not a rewrite from scratch — [pypi.org/project/httpx2](https://pypi.org/project/httpx2/). This is a significant fact for the project: if an actively maintained async HTTP client is required, it is worth explicitly checking the state of httpx2 (or waiting for the httpx 1.0 release) before locking it into dependencies, rather than relying on "httpx" by default without checking.
## Pydantic v2 vs v1

The official migration guide points out several differences that matter for the proxy worker.

Strict types and coercion: in v1 "whenever a field was annotated as `int`, any float value would be accepted", in v2 "type conversion from floats to integers is only allowed if the decimal part is zero" — [pydantic.dev/…/migration](https://pydantic.dev/docs/validation/latest/get-started/migration/).

Validators: the `@validator` decorator is deprecated, "`@validator` has been deprecated, and should be replaced with `@field_validator`"; the new decorator does not accept `each_item`, and the validator function's signature can no longer take `field` or `config` arguments; a `TypeError` inside a validator is no longer automatically turned into a `ValidationError` — [pydantic.dev/…/migration](https://pydantic.dev/docs/validation/latest/get-started/migration/).

Example of `field_validator` ("after" mode, applied by default after standard field validation) — [pydantic.dev/…/validators](https://pydantic.dev/docs/validation/latest/concepts/validators/):

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

Example of `model_validator` for checking the consistency of several fields at once — [pydantic.dev/…/validators](https://pydantic.dev/docs/validation/latest/concepts/validators/):

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

JSON parsing: `parse_raw` is replaced by `model_validate_json` — "In Pydantic V2, `model_validate_json` works like `parse_raw`" — [pydantic.dev/…/migration](https://pydantic.dev/docs/validation/latest/get-started/migration/). Usage example (note that with `strict=True` the date string and the list are still correctly coerced to `date` and `tuple`, precisely because this is JSON parsing rather than parsing of arbitrary data) — [pydantic.dev/…/json](https://pydantic.dev/docs/validation/latest/concepts/json/):

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

For enumerable values (a cat's coat traits: for example, a fixed set of strings like "tabby", "solid", "calico", etc.) Pydantic v2 can use `Literal` or `enum.Enum` as the field type — both variants are supported directly — [pydantic.dev/…/standard_library_types](https://pydantic.dev/docs/validation/latest/api/standard_library_types/):

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

For the proxy worker, `Literal` is preferable where the set of values is strictly fixed and should not become a separate type with its own namespace, while `Enum` is preferable where the values are reused across several models or an `isinstance` check is needed.

## Minimal working FastAPI application

The request model is defined as an ordinary `BaseModel` class, and the POST handler accepts it as a parameter — FastAPI itself reads and parses the JSON body, validates the data, and builds the OpenAPI schema — [fastapi.tiangolo.com/tutorial/body](https://fastapi.tiangolo.com/tutorial/body/):

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

Error handling — via `HTTPException` with an explicit status code and a `detail` body (which can be a string, a dict, or a list) — [fastapi.tiangolo.com/tutorial/handling-errors](https://fastapi.tiangolo.com/tutorial/handling-errors/):

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

An exception must be raised via `raise`, not `return` — this immediately halts request processing and sends the error to the client; codes 400-499 denote a client error — [fastapi.tiangolo.com/tutorial/handling-errors](https://fastapi.tiangolo.com/tutorial/handling-errors/).

A custom exception class and a dedicated handler for it — same source:

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

Overriding the request validation error handler (`RequestValidationError`) with a custom response body — same source:

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

For the `/traits` node, this directly implies a pattern: a request model with an image field and device metadata, a response model with enumerable coat traits (`Literal`/`Enum`), a `POST` handler that raises an `HTTPException` with code 400/422/502 on a cloud model error or an invalid image format, rather than silently returning an empty response.

## Receiving an image: base64 in JSON vs UploadFile

The official FastAPI documentation describes two ways to accept files — [fastapi.tiangolo.com/tutorial/request-files](https://fastapi.tiangolo.com/tutorial/request-files/):

Via `bytes` — the whole file is read into memory; suitable only for small files, simple, but memory-hungry.

Via `UploadFile` — uses a "spooled" file (kept in memory up to a certain limit, then moved to disk), metadata is available (`filename`, `content_type`), an async file-like interface, and it can be passed directly to libraries that expect a file-like object. Because `UploadFile` works through `multipart/form-data`, there is an important restriction: "you cannot declare `File`/`Form` parameters and JSON `Body` fields in the same request at the same time" — [fastapi.tiangolo.com/tutorial/request-files](https://fastapi.tiangolo.com/tutorial/request-files/). Installation requires the `python-multipart` dependency.

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

For the `/traits` node, where the photo is sent together with device metadata and, possibly, an HMAC signature (request signing, see file 03), a single JSON body is more practical, with the image encoded in base64 as a separate string field of a `BaseModel` — this fits into a single `Content-Type: application/json` without switching to `multipart/form-data`, simplifies signing the whole body at once, and does not require `python-multipart`. The downside — base64 increases the volume of transmitted data by roughly a third compared to the binary representation, and the entire image must be in memory as part of the parsed Pydantic model even before the developer's code gets control. `UploadFile` is more memory-efficient for large files and more convenient when the image is the only payload of the request, but combines poorly with request signing of the whole body and requires `python-multipart`.

The request body size limit needs to be set at a minimum of two levels, since neither one substitutes for the other:

At the nginx level, as the proxy worker — the `client_max_body_size` directive, `1m` by default; on exceeding it, the client gets a code 413 (Request Entity Too Large); a value of `0` disables the check entirely — [nginx.org/…/client_max_body_size](https://nginx.org/en/docs/http/ngx_http_core_module.html#client_max_body_size):

```nginx
# Allow request bodies up to 10 megabytes
client_max_body_size 10m;

# Disable size checking
client_max_body_size 0;
```

Since the photo is cropped to 512×512 and sent as an uncompressed or lightly compressed base64 image, a reasonable request body cap is on the order of several megabytes (for example, 5-10 MB with some margin), but no exact number is set in any official source, and it should be determined experimentally based on the specific photo's format and compression quality — "no reliable source with a ready-made number for this case was found".

At the application level itself, in FastAPI/Starlette, the sources found could not confirm a separate simple body size limit parameter at the Uvicorn/FastAPI level via direct WebFetch access — the official `uvicorn.org/deployment` page was unavailable from the collection environment (see the "Deployment" section); in practice, for the proxy worker this means one must not rely only on nginx, but should additionally check the length/size of the decoded base64 inside a Pydantic validator (for example, via a `field_validator` that rejects the value before it is passed to the cloud model), since no direct source about a built-in body limit in Starlette/FastAPI itself was confirmed during this collection.

## Asynchrony: async def, def, and talking to the cloud

The official FastAPI guide states the choice rule as follows — [fastapi.tiangolo.com/async](https://fastapi.tiangolo.com/async/):

Use `async def` if a third-party library requires calling it via `await`:

```python
@app.get('/')
async def read_results():
    results = await some_library()
    return results
```

Use plain `def` if the third-party library you're exchanging data with (a database, an API, the file system) does not support `await` ("this is currently the case for most database libraries"):

```python
@app.get('/')
def results():
    results = some_library()
    return results
```

"If your application (somehow) doesn't have to communicate with anything else and wait for it to respond, use `async def`, even if you don't need to use `await` inside"; and "if you just don't know, use normal `def`" — [fastapi.tiangolo.com/async](https://fastapi.tiangolo.com/async/).

The key danger is a blocking call inside `async def`: "in these cases, it's better to use `async def` unless the path operation functions use blocking I/O" — that is, blocking code inside `async def` without `await` halts the entire event handler (and thus all concurrent requests of that worker process), not just the current request — [fastapi.tiangolo.com/async](https://fastapi.tiangolo.com/async/). For the `/traits` node this means: if the call to the cloud model is made with a synchronous client (`requests` or a synchronous `httpx.Client`) inside `async def`, the whole process will stall for the duration of the wait for the cloud's response.

The correct way to reach the cloud from `async def` is the asynchronous client `httpx.AsyncClient`. Timeout management — by default httpx raises `TimeoutException` after 5 seconds of network inactivity; the timeout can be finely split into components (`connect`, `read`, `write`, `pool`) — [python-httpx.org/advanced/timeouts](https://www.python-httpx.org/advanced/timeouts/):

```python
httpx.get('http://example.com/api/v1/example', timeout=10.0)
```

```python
timeout = httpx.Timeout(10.0, connect=60.0)
client = httpx.Client(timeout=timeout)
response = client.get('http://example.com/')
```

Retries at the transport level — `HTTPTransport(retries=N)` retries the request on `httpx.ConnectError` or `httpx.ConnectTimeout` ("allowing smoother operation under flaky networks"), but not on read/write errors and not on status codes such as 503 — for that the documentation directly points to general-purpose libraries such as `tenacity` — [python-httpx.org/advanced/transports](https://www.python-httpx.org/advanced/transports/):

```python
import httpx
transport = httpx.HTTPTransport(retries=1)
client = httpx.Client(transport=transport)
```

On the official `python-httpx.org/api` page, the `retries` parameter is not listed among the `Client` parameters — retry control is done specifically via `transport`, not directly through the client — [python-httpx.org/api](https://www.python-httpx.org/api/). Given that the stable `httpx` library has not been updated since December 2024, and version 1.0 remains in dev-release status, when choosing a dependency for reaching the cloud it is worth explicitly deciding and fixing: staying on `httpx` 0.28.1, moving to the 1.0 pre-release, or moving to the `httpx2` fork maintained by Pydantic — see the "Versions" section above for details.

## Settings and secrets

`pydantic-settings` (latest version 2.15.0 from August 7, 2026, a package separate from the main `pydantic` — [pypi.org/project/pydantic-settings](https://pypi.org/project/pydantic-settings/)) reads field values from environment variables when the model is created, if they are not passed explicitly as keyword arguments: "If you create a model that inherits from `BaseSettings`, the model initialiser will attempt to determine the values of any fields not passed as keyword arguments by reading from the environment" — [pydantic.dev/…/pydantic_settings](https://pydantic.dev/docs/validation/latest/concepts/pydantic_settings/):

```python
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

class Settings(BaseSettings):
    auth_key: str = Field(validation_alias='my_auth_key')
    redis_dsn: str = 'redis://user:pass@localhost:6379/1'

    model_config = SettingsConfigDict(env_prefix='my_prefix_')

settings = Settings()
```

Source priority (descending): initialization arguments override environment variables, those override values from a `.env` file, and those override values from secret files; complex types (lists, dicts, nested models) are parsed from environment variables as JSON unless a custom parse is set up via a validator — [pydantic.dev/…/pydantic_settings](https://pydantic.dev/docs/validation/latest/concepts/pydantic_settings/).

For the proxy worker, the practical conclusion is: the cloud model key (`CLOUD_API_KEY` or a similar name) and the shared secret for HMAC request signing (see file 03) must be declared as `BaseSettings` fields and read from the process's environment variables (for example, from a systemd unit via `EnvironmentFile=`), rather than as string literals in the source code — that way the key physically never ends up in the repository and cannot accidentally be committed together with the code. The `.env` file with real values must be added to `.gitignore`, and the repository can only contain `.env.example` with variable names and no values — this is common practice; no separate official source specifically for this piece of advice was found during collection, so it is noted as a practice, not as a quotation.

## Deployment on a single machine

The official FastAPI documentation describes running several worker processes via the `--workers` parameter of the `fastapi` command or directly via `uvicorn`: several worker processes are started (for example, 4), the parent process acts as a manager, each worker has its own PID, this gives parallel execution across several cores and serves a larger number of requests concurrently — [fastapi.tiangolo.com/deployment/server-workers](https://fastapi.tiangolo.com/deployment/server-workers/):

```bash
fastapi run --workers 4 main.py
```

```bash
uvicorn main:app --host 0.0.0.0 --port 8080 --workers 4
```

The same source notes that using worker processes covers only the "replication" aspect, while HTTPS, autostart, restarts, and memory management remain the responsibility of the deployment; for complex cases FastAPI recommends containers (Docker/Kubernetes) — [fastapi.tiangolo.com/deployment/server-workers](https://fastapi.tiangolo.com/deployment/server-workers/). For a single machine with light load (processing cat photos is not a high-frequency operation), containerization is not required, but the principle itself of "several worker processes + restart via systemd" applies even without a container.

A separate package, **uvicorn-worker** (latest version 0.4.0, September 20, 2025), is a worker class for Gunicorn that combines Uvicorn's performance with Gunicorn's process management (zero-downtime restarts, graceful shutdown, etc.): "The Uvicorn Worker is a package designed for the mature and comprehensive server and process manager, Gunicorn" — [pypi.org/project/uvicorn-worker](https://pypi.org/project/uvicorn-worker/). It is used as `gunicorn app:app -k uvicorn_worker.UvicornWorker` — this official example command is taken from the package's description on PyPI and the general idea of the worker class, rather than quoted verbatim line-by-line from a separate guide.

The official `uvicorn.org/deployment` page (as well as `www.uvicorn.org` and `uvicorn.dev`) could not be opened via WebFetch during this collection — the domain either failed to resolve or returned a 403/404 error from the collection environment. Therefore, details about the specific systemd unit-file template, the `--proxy-headers`/`--forwarded-allow-ips` arguments, and Supervisor configuration **are not confirmed by direct access to the primary source** and are not presented here as fact — "no reliable source (successfully opened via WebFetch) with these details was found". The general principle, independent of the exact documentation page: a systemd unit file describes the startup command (`ExecStart=`, for example the same `gunicorn`/`uvicorn` with the needed arguments), environment variables via `EnvironmentFile=`, a restart policy (`Restart=on-failure`), and the user under which the process runs, while nginx in front of it listens on port 80/443 and proxies requests to a local socket or port, usually passing through the `X-Forwarded-For`/`X-Forwarded-Proto` headers; for the proxy worker's light load this means that two to four Uvicorn worker processes are enough, and a more complex scheme with a load balancer is not required.

## Logging and observability

The standard built-in `logging` library is the minimal option for a lightly loaded node: set up one handler to stdout (which systemd/journald then collects) and log at least incoming requests to `/traits`, the cloud model's response codes, and exceptions. For what specifically should be recorded in the context of abuse prevention (rate limiting frequency, device identifier, signature forgery attempts), see the file `03-ratelimit-and-signing.md`, the logging section, which is backed by a reference to the OWASP Logging Cheat Sheet. No separate official guide specifically from FastAPI/Uvicorn on logging, beyond Uvicorn's basic access-log configuration, was found during this collection, so detailed recommendations on structured logging (for example, via `structlog`) are not presented here as confirmed — "no reliable sources found".

## Sources

- [python.org/downloads](https://www.python.org/downloads/) — Python version and branch support status
- [pypi.org/project/fastapi](https://pypi.org/project/fastapi/) — FastAPI version
- [pypi.org/project/pydantic](https://pypi.org/project/pydantic/) — Pydantic version
- [pypi.org/project/uvicorn](https://pypi.org/project/uvicorn/) — Uvicorn version
- [pypi.org/project/pydantic-settings](https://pypi.org/project/pydantic-settings/) — pydantic-settings version
- [pypi.org/project/httpx](https://pypi.org/project/httpx/) and [pypi.org/project/httpx/#history](https://pypi.org/project/httpx/#history) — httpx version and release history
- [github.com/encode/httpx/releases](https://github.com/encode/httpx/releases) — httpx release history
- [pypi.org/project/httpx2](https://pypi.org/project/httpx2/) — the httpx2 fork maintained by Pydantic Services Inc.
- [pypi.org/project/uvicorn-worker](https://pypi.org/project/uvicorn-worker/) — the uvicorn-worker package for Gunicorn
- [pydantic.dev/docs/validation/latest/get-started/migration](https://pydantic.dev/docs/validation/latest/get-started/migration/) — differences between Pydantic v1 and v2
- [pydantic.dev/docs/validation/latest/concepts/validators](https://pydantic.dev/docs/validation/latest/concepts/validators/) — field_validator and model_validator
- [pydantic.dev/docs/validation/latest/concepts/json](https://pydantic.dev/docs/validation/latest/concepts/json/) — model_validate_json
- [pydantic.dev/docs/validation/latest/api/standard_library_types](https://pydantic.dev/docs/validation/latest/api/standard_library_types/) — Enum and Literal as field types
- [fastapi.tiangolo.com/tutorial/body](https://fastapi.tiangolo.com/tutorial/body/) — request model and POST handler
- [fastapi.tiangolo.com/tutorial/handling-errors](https://fastapi.tiangolo.com/tutorial/handling-errors/) — HTTPException and error handlers
- [fastapi.tiangolo.com/tutorial/request-files](https://fastapi.tiangolo.com/tutorial/request-files/) — UploadFile and bytes
- [fastapi.tiangolo.com/async](https://fastapi.tiangolo.com/async/) — the async def/def choice rule
- [fastapi.tiangolo.com/deployment/server-workers](https://fastapi.tiangolo.com/deployment/server-workers/) — running several worker processes
- [nginx.org/en/docs/http/ngx_http_core_module.html#client_max_body_size](https://nginx.org/en/docs/http/ngx_http_core_module.html#client_max_body_size) — request body size limit in nginx
- [python-httpx.org/advanced/timeouts](https://www.python-httpx.org/advanced/timeouts/) — httpx timeout management
- [python-httpx.org/advanced/transports](https://www.python-httpx.org/advanced/transports/) — retries via HTTPTransport
- [python-httpx.org/api](https://www.python-httpx.org/api/) — Client parameters (confirming the absence of retries at the Client level)
