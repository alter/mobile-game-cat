# Testing the proxy worker: pytest and FastAPI

Date of information gathering: 2026-08-24.

Verified version numbers (per PyPI, accessed 2026-08-24):

| Package | Version | Release date | Source |
|---|---|---|---|
| pytest | 9.1.1 | 2026-06-19 | [pypi.org/project/pytest](https://pypi.org/project/pytest/) |
| pytest-cov | 7.1.0 | 2026-03-21 | [pypi.org/project/pytest-cov](https://pypi.org/project/pytest-cov/) |
| respx | 0.23.1 | 2026-04-08 | [pypi.org/project/respx](https://pypi.org/project/respx/) |
| pytest-httpx | 0.36.2 | 2026-04-09 (per Socket.dev data, PyPI itself could not be opened directly) | see the note in the section below |

## Summary

- The latest version of pytest as of the collection date is 9.1.1 (June 19, 2026) — [pypi.org/project/pytest](https://pypi.org/project/pytest/).
- The official FastAPI documentation recommends `TestClient` (a wrapper over `httpx`, built into Starlette) for ordinary synchronous tests, and `httpx.AsyncClient` with `ASGITransport` — for tests written as `async def` (for example, if other asynchronous code needs to be called and awaited inside the test).
- `app.dependency_overrides` is FastAPI's standard mechanism for substituting dependencies in tests, including substituting a call to the cloud model with a stub.
- To intercept `httpx` calls without an actual network request, there are separate libraries `respx` (0.23.1, requires httpx ≥0.25) and `pytest-httpx`; both are younger than `httpx` 1.0 — both are explicitly aimed at `httpx`, not at its fork `httpx2` (see file 01), so when moving to `httpx2` the compatibility of these libraries needs to be checked separately — "not verified" within the scope of this collection.
- 40 reference snapshots naturally map onto `pytest.mark.parametrize`: a list of files (or their paths) is passed as parameters, and the test runs once per snapshot.
- A run over the reference set must not hit the real cloud model on every run — for that, the call to it is replaced by a fixture/stub (or by pre-recorded responses), rather than a real network call.
- The check "all responses parse" is essentially an ordinary test for a successful `model_validate_json`/`model_validate` call, followed by a check that the values belong to the allowed enumeration (`Literal`/`Enum`).
- `pytest-cov` (7.1.0) adds a coverage report and the `--cov-fail-under MIN` option to check a threshold right in CI.

## How pytest is structured: fixtures, parametrization, markers, conftest.py

A fixture is a function decorated with `@pytest.fixture` that provides data for setting up a test; the test function "requests" the fixture by naming it as a parameter — [docs.pytest.org/…/fixtures](https://docs.pytest.org/en/stable/how-to/fixtures.html):

```python
import pytest

@pytest.fixture
def fruit_bowl():
    return [Fruit("apple"), Fruit("banana")]

def test_fruit_salad(fruit_bowl):
    fruit_salad = FruitSalad(*fruit_bowl)
    assert all(fruit.cubed for fruit in fruit_salad.fruit)
```

Parametrization — the `@pytest.mark.parametrize` decorator specifies a set of argument sets, and the test is run separately for each set — [docs.pytest.org/…/parametrize](https://docs.pytest.org/en/stable/how-to/parametrize.html):

```python
# content of test_expectation.py
import pytest


@pytest.mark.parametrize("test_input,expected", [("3+5", 8), ("2+4", 6), ("6*9", 42)])
def test_eval(test_input, expected):
    assert eval(test_input) == expected
```

Markers are registered in a configuration file (in `pyproject.toml` in TOML format, or in `pytest.ini`/`setup.cfg` in INI format); "everything after the `:` in a mark name is an optional description"; registering marks removes warnings and is recommended for third-party plugins; "marks can only be applied to tests, they do not work on fixtures" — [docs.pytest.org/…/mark](https://docs.pytest.org/en/stable/how-to/mark.html):

```toml
[pytest]
markers = [
    "slow: marks tests as slow (deselect with '-m \"not slow\"')",
    "serial",
]
```

Markers can also be registered programmatically, via a hook in `conftest.py`:

```python
def pytest_configure(config):
    config.addinivalue_line(
        "markers", "env(name): mark test to run only on named environment"
    )
```

For the proxy worker, a typical `conftest.py` layout is: shared fixtures — a test instance of the FastAPI application, a test client, the path to the directory with reference snapshots, an override of the settings (`Settings`) with a fake cloud model key, so a real key is not required to run the tests.

## Testing FastAPI: TestClient versus httpx.AsyncClient with ASGITransport

The official FastAPI testing documentation describes `TestClient` as the primary method: "Testing FastAPI applications is easy and enjoyable thanks to Starlette's TestClient, which is based on HTTPX (designed after Requests)"; test functions are written as an ordinary `def` (not `async def`), and calls to the client are made without `await` — [fastapi.tiangolo.com/tutorial/testing](https://fastapi.tiangolo.com/tutorial/testing/):

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

An extended example checking a header and a request body — same source:

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

For tests written as `async def` (for example, to call other asynchronous code inside the test), `TestClient` does not fit — "while TestClient uses magic to call async FastAPI applications from synchronous test functions, this doesn't work inside async functions"; instead, `httpx.AsyncClient` with `ASGITransport` is used, and the test is marked with `@pytest.mark.anyio` — [fastapi.tiangolo.com/advanced/async-tests](https://fastapi.tiangolo.com/advanced/async-tests/):

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

An important caveat from the same source: if the application uses lifespan events (`lifespan`), `AsyncClient` does not run them automatically — for that, `LifespanManager` from the `asgi-lifespan` package is needed — [fastapi.tiangolo.com/advanced/async-tests](https://fastapi.tiangolo.com/advanced/async-tests/). For the proxy worker this means: if the initialization of the cloud model client or the Redis connection (for rate limiting, see file 03) happens in `lifespan`, asynchronous tests must explicitly bring up `LifespanManager`, otherwise those resources simply will not be created in the test.

## Substituting external calls: dependency_overrides, respx, pytest-httpx

`app.dependency_overrides` is a simple dictionary on the FastAPI application object: the key is the original dependency (a function), the value is the replacement function; FastAPI calls the replacement instead of the original. This is the officially recommended way to "avoid calling expensive external services (like authentication providers) in tests" — [fastapi.tiangolo.com/advanced/testing-dependencies](https://fastapi.tiangolo.com/advanced/testing-dependencies/):

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

Resetting the overrides after a test: `app.dependency_overrides = {}` — same source. For the proxy worker this means: the call to the cloud model is worth pulling out into a separate dependency (`Depends(get_cloud_client)` or a similar function returning a client or the call itself), so that in tests it can be replaced with a stub returning a pre-set list of coat traits, without a single real outbound HTTP call.

If the external call is made directly through `httpx` inside the code (rather than as a separate FastAPI dependency), network calls can be intercepted at the level of the `httpx` library itself, using two specialized libraries:

**respx** (latest version 0.23.1, April 8, 2026, requires httpx ≥0.25) — "A utility for mocking out the Python HTTPX and HTTP Core libraries" — [pypi.org/project/respx](https://pypi.org/project/respx/). An example via a decorator and via a pytest fixture — [lundberg.github.io/respx](https://lundberg.github.io/respx/):

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

**pytest-httpx** — per search results (the PyPI page itself could not be opened directly via WebFetch during this collection — the server kept returning a page-load error), the latest version as of the collection date is 0.36.2, released April 9, 2026, per data from the third-party catalog Socket.dev — this version number is flagged as "not verified directly against PyPI," unlike the other versions in this file. The library provides a fixture for intercepting `httpx` requests without explicitly mocking each call by hand — the specific usage code is not quoted verbatim within this collection, since the primary source could not be opened; before use, the current README should be checked directly on PyPI.

Both libraries are aimed at `httpx`, not at the `httpx2` fork mentioned in file 01 — compatibility with `httpx2` was not checked within this collection.

## Running the set of 40 reference snapshots

The official mechanism for this is `pytest.mark.parametrize`, applied to a list of file paths (see the example above in the section on how pytest is structured). A practical scheme following from pytest's documented capabilities, but not quoted verbatim as a single ready-made example (assembled for the task, not taken from one source):

- Store the reference snapshots inside the test repository, e.g. `tests/fixtures/cat_photos/*.jpg`, alongside a file or structure with the expected/reference coat traits for each snapshot (for example, a JSON file mapping "file name → expected traits," or simply the expectation "the response parses successfully and fits within the enumeration," if there are no reference "correct" traits for each snapshot).
- Collect the list of files dynamically (for example, via `pathlib.Path.glob`) and pass it to `pytest.mark.parametrize("image_path", ...)`, or use `pytest_generate_tests` in `conftest.py` for parametrization based on the directory contents — this function is officially described in the pytest documentation on generating tests, but was not opened page by page separately within this collection, so it is not quoted verbatim here.
- To avoid making a real call to the cloud on each of the 40 snapshots on every test run, the call to the cloud model itself is replaced at the level of a FastAPI dependency (`dependency_overrides`) or at the level of the HTTP library (`respx`/`pytest-httpx`, see above) — no network call is made, and what is tested is the response-parsing and validation code, not the model itself. A separate marker (for example, a registered `@pytest.mark.cloud`, see the section on markers) can single out the few tests that do reach the real cloud — such tests are excluded from the ordinary run by default (`pytest -m "not cloud"`) and run separately, either manually or on a schedule.

## Checking that the model's response parses

From how Pydantic v2 is structured (see file 01) the form of this check follows directly: if the cloud model's response model is described as a `BaseModel` with fields of type `Literal[...]` or `Enum`, then the mere fact of a successful call to `Model.model_validate_json(raw_response)` (or `Model.model_validate(parsed_dict)`, if the body was already parsed from JSON beforehand) already proves that: 1) the JSON is syntactically correct, 2) all required fields are present, 3) the values of the enumerated fields belong to the allowed set — Pydantic will raise `ValidationError` if a value is not among the `Literal`/`Enum` options, which is a direct consequence of the `field_validator`/`Literal` behavior described in file 01.

A practical test over all 40 reference responses (or stub responses emulating the cloud) — the idea itself of a parametrized check follows from the documented `pytest.mark.parametrize` (see above), assembled for the task, not quoted as a ready-made example:

```python
import pytest
from pydantic import ValidationError

@pytest.mark.parametrize("raw_response", ALL_SAMPLE_RESPONSES)
def test_all_responses_parse(raw_response):
    model = TraitsResponse.model_validate_json(raw_response)
    assert model.color_pattern in ColorPattern
```

It is also worth separately checking the reverse case — that a deliberately invalid response (an extra field with a disallowed value, a missing required field) does indeed raise `ValidationError`, rather than silently passing validation; this is standard practice for testing negative examples, and no separate source specific to this task was needed during collection, since it is a direct consequence of Pydantic's behavior described and quoted in file 01.

## Coverage: pytest-cov

The latest version of `pytest-cov` is 7.1.0, released March 21, 2026; the release notes mention a fix to the total coverage count and to handling of `ResourceWarning` from `sqlite3` — [pypi.org/project/pytest-cov](https://pypi.org/project/pytest-cov/).

The `--cov-fail-under MIN` option is described in the documentation as: "Fail if the total coverage is less than MIN" — [pytest-cov.readthedocs.io/…/config](https://pytest-cov.readthedocs.io/en/latest/config.html). The threshold can also be set in a configuration file (`.coveragerc`, or a section in `setup.cfg`/`pyproject.toml`) — same page; the documentation itself does not prescribe any specific numeric threshold, and choosing a particular value (for example, 80% or 90%) is a project decision, not a tool requirement, so it is not given here as a "recommended figure" — "no specific recommended figure was found in the official source."

A practical command for running with coverage checking and a threshold (assembled from documented options, not quoted in full as a single example from one source):

```bash
pytest --cov=app --cov-report=term-missing --cov-fail-under=80
```

## Sources

- [pypi.org/project/pytest](https://pypi.org/project/pytest/) — pytest version
- [docs.pytest.org/en/stable/how-to/fixtures.html](https://docs.pytest.org/en/stable/how-to/fixtures.html) — fixtures
- [docs.pytest.org/en/stable/how-to/parametrize.html](https://docs.pytest.org/en/stable/how-to/parametrize.html) — parametrization
- [docs.pytest.org/en/stable/how-to/mark.html](https://docs.pytest.org/en/stable/how-to/mark.html) — markers, registration in configuration and via conftest.py
- [fastapi.tiangolo.com/tutorial/testing](https://fastapi.tiangolo.com/tutorial/testing/) — TestClient
- [fastapi.tiangolo.com/advanced/async-tests](https://fastapi.tiangolo.com/advanced/async-tests/) — httpx.AsyncClient with ASGITransport, pytest.mark.anyio, LifespanManager
- [fastapi.tiangolo.com/advanced/testing-dependencies](https://fastapi.tiangolo.com/advanced/testing-dependencies/) — app.dependency_overrides
- [pypi.org/project/respx](https://pypi.org/project/respx/) — respx version
- [lundberg.github.io/respx](https://lundberg.github.io/respx/) — respx usage examples
- [pypi.org/project/pytest-cov](https://pypi.org/project/pytest-cov/) — pytest-cov version
- [pytest-cov.readthedocs.io/en/latest/config.html](https://pytest-cov.readthedocs.io/en/latest/config.html) — the --cov-fail-under option





