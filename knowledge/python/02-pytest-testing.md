# Тестирование узла-посредника: pytest и FastAPI

Дата сбора сведений: 2026-08-24.

Проверенные номера версий (по PyPI, дата обращения 2026-08-24):

| Пакет | Версия | Дата выпуска | Источник |
|---|---|---|---|
| pytest | 9.1.1 | 2026-06-19 | [pypi.org/project/pytest](https://pypi.org/project/pytest/) |
| pytest-cov | 7.1.0 | 2026-03-21 | [pypi.org/project/pytest-cov](https://pypi.org/project/pytest-cov/) |
| respx | 0.23.1 | 2026-04-08 | [pypi.org/project/respx](https://pypi.org/project/respx/) |
| pytest-httpx | 0.36.2 | 2026-04-09 (по данным Socket.dev, PyPI напрямую открыть не удалось) | см. примечание в разделе ниже |

## Кратко

- Последняя версия pytest на дату сбора — 9.1.1 (19 июня 2026) — [pypi.org/project/pytest](https://pypi.org/project/pytest/).
- Официальная документация FastAPI рекомендует `TestClient` (обёртка над `httpx`, встроенная в Starlette) для обычных синхронных тестов и `httpx.AsyncClient` с `ASGITransport` — для тестов, написанных как `async def` (например, если внутри теста нужно вызывать и `await`-ить другой асинхронный код).
- `app.dependency_overrides` — штатный механизм FastAPI для подмены зависимостей в тестах, в том числе для подмены обращения к облачной модели заглушкой.
- Для перехвата вызовов `httpx` без реального обращения к сети есть отдельные библиотеки `respx` (0.23.1, требует httpx ≥0.25) и `pytest-httpx`; обе моложе, чем `httpx` 1.0 — обе явно ориентированы на `httpx`, а не на его форк `httpx2` (см. файл 01), поэтому при переходе на `httpx2` совместимость этих библиотек нужно проверять отдельно — «не проверено» в рамках этого сбора.
- 40 эталонных снимков естественно ложатся на `pytest.mark.parametrize`: список файлов (или их путей) передаётся как параметры, тест выполняется один раз на каждый снимок.
- Прогон по эталонному набору не должен обращаться к реальной облачной модели на каждый запуск — для этого её обращение подменяется фикстурой/заглушкой (либо заранее записанными ответами), а не реальным сетевым вызовом.
- Проверка «все ответы разбираются» — это по сути обычный тест на успешный `model_validate_json`/`model_validate` и последующую проверку принадлежности значений допустимому перечню (`Literal`/`Enum`).
- `pytest-cov` (7.1.0) добавляет отчёт покрытия и опцию `--cov-fail-under MIN` для проверки порога прямо в CI.

## Устройство pytest: фикстуры, параметризация, маркеры, conftest.py

Фикстура — функция, декорированная `@pytest.fixture`, которая предоставляет данные для настройки теста; тестовая функция «запрашивает» фикстуру, указывая её имя как параметр — [docs.pytest.org/…/fixtures](https://docs.pytest.org/en/stable/how-to/fixtures.html):

```python
import pytest

@pytest.fixture
def fruit_bowl():
    return [Fruit("apple"), Fruit("banana")]

def test_fruit_salad(fruit_bowl):
    fruit_salad = FruitSalad(*fruit_bowl)
    assert all(fruit.cubed for fruit in fruit_salad.fruit)
```

Параметризация — декоратор `@pytest.mark.parametrize` задаёт набор наборов аргументов, и тест выполняется отдельно для каждого набора — [docs.pytest.org/…/parametrize](https://docs.pytest.org/en/stable/how-to/parametrize.html):

```python
# content of test_expectation.py
import pytest


@pytest.mark.parametrize("test_input,expected", [("3+5", 8), ("2+4", 6), ("6*9", 42)])
def test_eval(test_input, expected):
    assert eval(test_input) == expected
```

Маркеры регистрируются в конфигурационном файле (в `pyproject.toml` в формате TOML или в `pytest.ini`/`setup.cfg` в формате INI), «всё, что стоит после `:` в имени метки — необязательное описание»; регистрация меток избавляет от предупреждений и рекомендуется для сторонних плагинов; «метки можно применять только к тестам, на фикстуры они не действуют» — [docs.pytest.org/…/mark](https://docs.pytest.org/en/stable/how-to/mark.html):

```toml
[pytest]
markers = [
    "slow: marks tests as slow (deselect with '-m \"not slow\"')",
    "serial",
]
```

Маркеры можно регистрировать и программно, через хук в `conftest.py`:

```python
def pytest_configure(config):
    config.addinivalue_line(
        "markers", "env(name): mark test to run only on named environment"
    )
```

Для узла-посредника типовое устройство `conftest.py` — общие фикстуры: тестовый экземпляр FastAPI-приложения, тестовый клиент, путь к каталогу с эталонными снимками, подмена настроек (`Settings`) с фиктивным ключом облачной модели, чтобы реальный ключ не требовался для запуска тестов.

## Тестирование FastAPI: TestClient против httpx.AsyncClient с ASGITransport

Официальная документация FastAPI по тестированию описывает `TestClient` как основной способ: «Testing FastAPI applications is easy and enjoyable thanks to Starlette's TestClient, which is based on HTTPX (designed after Requests)»; тестовые функции пишутся как обычный `def` (не `async def`), вызовы к клиенту делаются без `await` — [fastapi.tiangolo.com/tutorial/testing](https://fastapi.tiangolo.com/tutorial/testing/):

```python
from fastapi import FastAPI
from fastapi.testclient import TestClient

app = FastAPI()

@app.get("/")
async def read_main():
    return {"msg": "Hello World"}

client = TestClient(app)

def test_read_main():
    response = client.get("/")
    assert response.status_code == 200
    assert response.json() == {"msg": "Hello World"}
```

Расширенный пример с проверкой заголовка и телом запроса — тот же источник:

```python
from typing import Annotated
from fastapi import FastAPI, Header, HTTPException
from pydantic import BaseModel
from fastapi.testclient import TestClient

app = FastAPI()

class Item(BaseModel):
    id: str
    title: str
    description: str | None = None

fake_secret_token = "coneofsilence"
fake_db = {
    "foo": {"id": "foo", "title": "Foo", "description": "There goes my hero"},
    "bar": {"id": "bar", "title": "Bar", "description": "The bartenders"},
}

@app.get("/items/{item_id}", response_model=Item)
async def read_main(item_id: str, x_token: Annotated[str, Header()]):
    if x_token != fake_secret_token:
        raise HTTPException(status_code=400, detail="Invalid X-Token header")
    if item_id not in fake_db:
        raise HTTPException(status_code=404, detail="Item not found")
    return fake_db[item_id]

client = TestClient(app)

def test_read_item():
    response = client.get("/items/foo", headers={"X-Token": "coneofsilence"})
    assert response.status_code == 200
    assert response.json() == {
        "id": "foo",
        "title": "Foo",
        "description": "There goes my hero",
    }
```

Для тестов, написанных как `async def` (например, чтобы внутри теста вызывать другой асинхронный код), `TestClient` не подходит — «while TestClient uses magic to call async FastAPI applications from synchronous test functions, this doesn't work inside async functions»; вместо него используется `httpx.AsyncClient` с `ASGITransport`, а тест помечается `@pytest.mark.anyio` — [fastapi.tiangolo.com/advanced/async-tests](https://fastapi.tiangolo.com/advanced/async-tests/):

```python
import pytest
from httpx import ASGITransport, AsyncClient
from .main import app

@pytest.mark.anyio
async def test_root():
    async with AsyncClient(
        transport=ASGITransport(app=app), base_url="http://test"
    ) as ac:
        response = await ac.get("/")
    assert response.status_code == 200
    assert response.json() == {"message": "Tomato"}
```

Важная оговорка того же источника: если приложение использует события жизненного цикла (`lifespan`), `AsyncClient` их автоматически не запускает — для этого нужен `LifespanManager` из пакета `asgi-lifespan` — [fastapi.tiangolo.com/advanced/async-tests](https://fastapi.tiangolo.com/advanced/async-tests/). Для узла-посредника это означает: если инициализация клиента к облачной модели или подключения к Redis (для ограничения частоты обращений, см. файл 03) происходит в `lifespan`, асинхронные тесты должны явно поднимать `LifespanManager`, иначе эти ресурсы в тесте просто не будут созданы.

## Подмена внешних вызовов: dependency_overrides, respx, pytest-httpx

`app.dependency_overrides` — простой словарь на объекте приложения FastAPI: ключ — исходная зависимость (функция), значение — функция-подмена; FastAPI вызывает подмену вместо оригинала. Это официально рекомендуемый способ «avoid calling expensive external services (like authentication providers) in tests» — [fastapi.tiangolo.com/advanced/testing-dependencies](https://fastapi.tiangolo.com/advanced/testing-dependencies/):

```python
from typing import Annotated
from fastapi import Depends, FastAPI
from fastapi.testclient import TestClient

app = FastAPI()

async def common_parameters(q: str | None = None, skip: int = 0, limit: int = 100):
    return {"q": q, "skip": skip, "limit": limit}

@app.get("/items/")
async def read_items(commons: Annotated[dict, Depends(common_parameters)]):
    return {"message": "Hello Items!", "params": commons}

client = TestClient(app)

async def override_dependency(q: str | None = None):
    return {"q": q, "skip": 5, "limit": 10}

app.dependency_overrides[common_parameters] = override_dependency

def test_override_in_items():
    response = client.get("/items/")
    assert response.status_code == 200
    assert response.json() == {
        "message": "Hello Items!",
        "params": {"q": None, "skip": 5, "limit": 10},
    }
```

Сброс подмен после теста: `app.dependency_overrides = {}` — тот же источник. Для узла-посредника это означает: обращение к облачной модели стоит вынести в отдельную зависимость (`Depends(get_cloud_client)` или аналогичную функцию, возвращающую клиента или сам вызов), чтобы в тестах подменить её на заглушку, возвращающую заранее заданный набор черт окраса, без единого реального HTTP-обращения наружу.

Если внешний вызов сделан напрямую через `httpx` внутри кода (а не как отдельная FastAPI-зависимость), перехватывать сетевые вызовы можно на уровне самой библиотеки `httpx` двумя специализированными библиотеками:

**respx** (последняя версия 0.23.1, 8 апреля 2026, требует httpx ≥0.25) — «A utility for mocking out the Python HTTPX and HTTP Core libraries» — [pypi.org/project/respx](https://pypi.org/project/respx/). Пример через декоратор и через фикстуру pytest — [lundberg.github.io/respx](https://lundberg.github.io/respx/):

```python
import httpx
import respx

from httpx import Response

@respx.mock
def test_example():
    my_route = respx.get("https://foo.bar/").mock(return_value=Response(204))
    response = httpx.get("https://foo.bar/")
    assert my_route.called
    assert response.status_code == 204
```

```python
import httpx
import pytest

def test_default(respx_mock):
    respx_mock.get("https://foo.bar/").mock(return_value=httpx.Response(204))
    response = httpx.get("https://foo.bar/")
    assert response.status_code == 204
```

**pytest-httpx** — по данным поисковой выдачи (напрямую страницу PyPI открыть через WebFetch в ходе сбора не удалось — сервер возвращал ошибку загрузки страницы), последняя версия на момент сбора — 0.36.2, выпущена 9 апреля 2026, по данным стороннего каталога Socket.dev — этот номер версии помечается как «не проверено напрямую по PyPI», в отличие от остальных версий в этом файле. Библиотека предоставляет фикстуру для перехвата запросов `httpx` без явного мока каждого вызова вручную — конкретный код использования в рамках этого сбора дословно не процитирован, так как первоисточник открыть не удалось; перед применением стоит свериться с актуальным README на PyPI напрямую.

Обе библиотеки ориентированы на `httpx`, а не на упомянутый в файле 01 форк `httpx2` — совместимость с `httpx2` в рамках этого сбора не проверялась.

## Прогон по набору из 40 эталонных снимков

Официальный механизм для этого — `pytest.mark.parametrize`, применённый к списку путей файлов (см. пример выше в разделе про устройство pytest). Практическая схема, вытекающая из документированных возможностей pytest, но не процитированная дословно как единый готовый пример (составлена по задаче, а не взята из одного источника):

- Эталонные снимки хранить внутри репозитория теста, например `tests/fixtures/cat_photos/*.jpg`, рядом — файл или структуру с ожидаемыми/эталонными чертами окраса для каждого снимка (например, JSON-файл с сопоставлением «имя файла → ожидаемые черты» либо просто ожидание «ответ успешно разбирается и укладывается в перечень», если эталонных «правильных» черт по каждому снимку нет).
- Список файлов собирать динамически (например, через `pathlib.Path.glob`) и передавать в `pytest.mark.parametrize("image_path", ...)`, либо использовать `pytest_generate_tests` в `conftest.py` для параметризации на основе содержимого каталога — эта функция официально описана в документации pytest по генерации тестов, но отдельно в рамках этого сбора не открывалась постранично, поэтому здесь не цитируется дословно.
- Чтобы не гонять реальное обращение к облаку на каждый из 40 снимков при каждом запуске тестов, сам вызов к облачной модели подменяется на уровне зависимости FastAPI (`dependency_overrides`) или на уровне HTTP-библиотеки (`respx`/`pytest-httpx`, см. выше) — обращение к сети не выполняется, тестируется код разбора и валидации ответа, а не сама модель. Отдельный маркер (например, зарегистрированный `@pytest.mark.cloud`, см. раздел про маркеры) может выделять те немногие тесты, которые всё же обращаются к реальному облаку — такие тесты по умолчанию исключаются из обычного прогона (`pytest -m "not cloud"`) и запускаются отдельно, вручную или по расписанию.

## Проверка того, что ответ модели разбирается

Из устройства Pydantic v2 (см. файл 01) прямо следует форма такой проверки: если модель ответа облачной модели описана как `BaseModel` с полями типа `Literal[...]` или `Enum`, то сам факт успешного вызова `Model.model_validate_json(raw_response)` (или `Model.model_validate(parsed_dict)`, если тело уже разобрано из JSON заранее) уже доказывает, что: 1) JSON синтаксически корректен, 2) все обязательные поля присутствуют, 3) значения перечислимых полей принадлежат допустимому множеству — Pydantic поднимет `ValidationError`, если значение не входит в `Literal`/`Enum`, это прямое следствие описанного в файле 01 поведения `field_validator`/`Literal`.

Практический тест на все 40 эталонных ответов (или ответов заглушки, эмулирующей облако) — сама идея параметризованной проверки следует из документированного `pytest.mark.parametrize` (см. выше), составлена для задачи, а не процитирована как готовый пример:

```python
import pytest
from pydantic import ValidationError

@pytest.mark.parametrize("raw_response", ALL_SAMPLE_RESPONSES)
def test_all_responses_parse(raw_response):
    model = TraitsResponse.model_validate_json(raw_response)
    assert model.color_pattern in ColorPattern
```

Отдельно стоит проверять и обратный случай — что заведомо некорректный ответ (лишнее поле недопустимого значения, отсутствующее обязательное поле) действительно поднимает `ValidationError`, а не проходит валидацию молча; это стандартная практика тестирования на отрицательных примерах, отдельного специфичного источника именно под эту задачу в ходе сбора не требовалось, так как это прямое следствие поведения Pydantic, описанного и процитированного в файле 01.

## Покрытие: pytest-cov

Последняя версия `pytest-cov` — 7.1.0, выпущена 21 марта 2026; в примечаниях к выпуску упомянуто исправление подсчёта суммарного покрытия и работы с `ResourceWarning` от `sqlite3` — [pypi.org/project/pytest-cov](https://pypi.org/project/pytest-cov/).

Опция `--cov-fail-under MIN` описана в документации так: «Fail if the total coverage is less than MIN» — [pytest-cov.readthedocs.io/…/config](https://pytest-cov.readthedocs.io/en/latest/config.html). Порог также можно задать в конфигурационном файле (`.coveragerc`, либо секция в `setup.cfg`/`pyproject.toml`) — та же страница; конкретного числового порога сама документация не предписывает, выбор конкретного значения (например, 80% или 90%) — решение проекта, а не требование инструмента, поэтому здесь не указывается как «рекомендуемое число» — «конкретной рекомендованной цифры в официальном источнике не найдено».

Практическая команда для запуска с проверкой покрытия и порогом (составлена по документированным опциям, а не процитирована целиком как единый пример из одного источника):

```bash
pytest --cov=app --cov-report=term-missing --cov-fail-under=80
```

## Источники

- [pypi.org/project/pytest](https://pypi.org/project/pytest/) — версия pytest
- [docs.pytest.org/en/stable/how-to/fixtures.html](https://docs.pytest.org/en/stable/how-to/fixtures.html) — фикстуры
- [docs.pytest.org/en/stable/how-to/parametrize.html](https://docs.pytest.org/en/stable/how-to/parametrize.html) — параметризация
- [docs.pytest.org/en/stable/how-to/mark.html](https://docs.pytest.org/en/stable/how-to/mark.html) — маркеры, регистрация в конфигурации и через conftest.py
- [fastapi.tiangolo.com/tutorial/testing](https://fastapi.tiangolo.com/tutorial/testing/) — TestClient
- [fastapi.tiangolo.com/advanced/async-tests](https://fastapi.tiangolo.com/advanced/async-tests/) — httpx.AsyncClient с ASGITransport, pytest.mark.anyio, LifespanManager
- [fastapi.tiangolo.com/advanced/testing-dependencies](https://fastapi.tiangolo.com/advanced/testing-dependencies/) — app.dependency_overrides
- [pypi.org/project/respx](https://pypi.org/project/respx/) — версия respx
- [lundberg.github.io/respx](https://lundberg.github.io/respx/) — примеры использования respx
- [pypi.org/project/pytest-cov](https://pypi.org/project/pytest-cov/) — версия pytest-cov
- [pytest-cov.readthedocs.io/en/latest/config.html](https://pytest-cov.readthedocs.io/en/latest/config.html) — опция --cov-fail-under







