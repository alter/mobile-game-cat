# Модель со зрением: черты окраса строгим JSON

Дата сбора: 2026-08-24
Отношение к проекту: ступень 2 разбора снимка (`cat-shelter-tech.md`, раздел 3),
узел-посредник `POST /traits` (`/tools/traits`).

---

## Кратко

1. Стоимость разбора одного снимка 512×512 посчитана по действующим ценам:
   **около 0,10 цента на Claude Haiku 4.5** и **около 0,20 цента на Claude Sonnet 5**.
   Оценка «0,1–0,3 цента» из `cat-shelter-tech.md` подтверждается. Расчёт ниже,
   с формулой из документации.
2. Просить модель «отвечать только JSON» словами — устаревший приём. Есть
   **structured outputs**: параметр `output_config.format` с JSON Schema, ответ
   гарантированно соответствует схеме. Beta-заголовок больше не нужен.
3. Перечислимые значения задаются ключевым словом `enum` прямо в схеме — это
   именно то, что нужно для `base_color`, `pattern`, `fur_length`, `eye_color`.
   Значения вне перечня стать ответом не могут.
4. `additionalProperties: false` для каждого объекта — **обязательное**
   требование схемы, не пожелание.
5. Стоимость изображения считается по числу лоскутов 28×28 пикселей:
   `⌈width / 28⌉ × ⌈height / 28⌉`. Для 512×512 это 361 токен.
6. Изображение следует ставить **перед** текстом в содержимом сообщения —
   прямая рекомендация документации.
7. Снимки не хранятся на стороне Anthropic: «Image uploads are ephemeral and not
   stored beyond the duration of the API request.» Это подкрепляет обещание «снимок
   не сохраняется нигде».
8. Модель не обрабатывает непристойные изображения, нарушающие правила
   допустимого применения. Это дополнительный, но **не основной** заслон —
   основной остаётся Apple Vision на устройстве.
9. Модель не называет людей на снимках и отказывается это делать. Для нашей
   задачи это безразлично, но важно знать при разборе снимка с человеком в кадре.
10. Для перечислимой выборки из шести значений разумный выбор — **Claude Haiku 4.5**:
    он поддерживает structured outputs и стоит вчетверо дешевле Sonnet 5.

---

## 1. Стоимость: расчёт, а не догадка

### Формула

Документация задаёт её дословно:

> Claude views images in patches instead of pixels. Each patch is a 28×28-pixel
> block of the image, referred to as a visual token. An image, therefore, costs
> `⌈width / 28⌉ × ⌈height / 28⌉` visual tokens.

([Vision](https://platform.claude.com/docs/en/build-with-claude/vision))

Для нашего снимка 512×512: `⌈512 / 28⌉ = ⌈18,29⌉ = 19`, значит `19 × 19 = 361`
визуальный токен. Уменьшения не происходит: предел по длинной стороне —
1568 px для обычного разряда и 2576 px для повышенного, 512 меньше обоих.

### Цены на 2026-08-24

| Модель | Вход, $/1M | Выход, $/1M |
|---|---|---|
| Claude Haiku 4.5 | 1 | 5 |
| Claude Sonnet 5 | 2 | 10 |
| Claude Opus 5 | 5 | 25 |

([Pricing](https://platform.claude.com/docs/en/about-claude/pricing))

Отдельно стоит знать: объявленная при выпуске Sonnet 5 льготная цена 2/10
**стала постоянной**, повышения до 3/15 первого сентября 2026 не будет —
это сказано на странице цен прямо.

### Стоимость одного разбора

Допущения: снимок 512×512 = 361 токен, наказ со схемой ≈ 250 токенов входа,
ответ ≈ 80 токенов. Итого 611 токенов входа.

| Модель | Вход | Выход | Всего | В центах |
|---|---|---|---|---|
| Haiku 4.5 | 611 × $1/1M = $0,00061 | 80 × $5/1M = $0,00040 | $0,00101 | **0,10** |
| Sonnet 5 | 611 × $2/1M = $0,00122 | 80 × $10/1M = $0,00080 | $0,00202 | **0,20** |
| Opus 5 | 611 × $5/1M = $0,00306 | 80 × $25/1M = $0,00200 | $0,00506 | **0,51** |

При 500 проверочных установках и доле загрузивших 40% это 200 разборов:
**20 центов на Haiku, 40 центов на Sonnet** за весь MVP. Статья расходов,
которой можно пренебречь: она в тысячу раз меньше 400 долларов на проверку
удержания.

Вывод для проекта: спор «дорого ли обходится облако» закрыт. Выбирать модель
надо по качеству разбора окраса, а не по цене.

### Чего расчёт не покрывает

Prompt caching здесь не поможет: наименьший кэшируемый отрезок — около 1024
токенов, а наш наказ короче. Правильный вывод — **не пытаться** прикручивать
кэш к этому вызову.

---

## 2. Structured outputs вместо «отвечай только JSON»

### Почему не наказом

Наказ «верни строго JSON» даёт ответ, который **обычно** разбирается. Пункт 5.2
в `cat-shelter-tasks.md` требует 100% разбора на эталонном наборе из 40 снимков.
Наказом эта планка не берётся надёжно — берётся схемой.

### Форма запроса

Дословно из документации:

```json
"output_config": {
  "format": {
    "type": "json_schema",
    "schema": {
      "type": "object",
      "properties": {
        "name": {"type": "string"},
        "email": {"type": "string"}
      },
      "required": ["name", "email"],
      "additionalProperties": false
    }
  }
}
```

([Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs))

### Beta-заголовок больше не нужен

> The `output_format` parameter has moved to `output_config.format`, and beta
> headers are no longer required. The API continues to accept the old beta header
> (`structured-outputs-2025-11-13`) and the `output_format` request field for a
> transition period, but the Python SDK (v1.0 and later) does not accept
> `output_format={...}` on `client.beta.messages.create()` or `count_tokens()`
> and raises a `TypeError`; use `output_config` instead.

Это важно: если агент напишет `output_format=...` по памяти, на Python SDK 1.x
он получит `TypeError`. Правильно — `output_config`.

### Какие модели поддерживают

> Supported models: `claude-fable-5`, `claude-mythos-5`, `claude-mythos-preview`,
> `claude-opus-5`, `claude-opus-4-8`, `claude-opus-4-7`, `claude-opus-4-6`,
> `claude-sonnet-5`, `claude-sonnet-4-6`, `claude-sonnet-4-5-20250929`,
> `claude-opus-4-5-20251101`, `claude-haiku-4-5-20251001`

Haiku 4.5 в списке — значит самый дешёвый путь нам открыт.

### Что схема умеет и чего не умеет

Поддерживается (дословно):

> * All basic types: object, array, string, integer, number, boolean, null
> * `enum` (strings, numbers, bools, or nulls only - no complex types)
> * `const`
> * `anyOf` and `allOf` (with limitations - `allOf` with `$ref` not supported)
> * `$ref`, `$def`, and `definitions` (external `$ref` not supported)
> * `default` property for all supported types
> * `required` and `additionalProperties` (must be set to `false` for objects)
> * String formats: `date-time`, `time`, `date`, `duration`, `email`, `hostname`, `uri`, `ipv4`, `ipv6`, `uuid`
> * Array `minItems` (only values 0 and 1 supported)

Не поддерживается (дословно):

> * Recursive schemas
> * Complex types within enums
> * External `$ref` (for example, `'$ref': 'http://...'`)
> * Numerical constraints (such as `minimum`, `maximum`, `multipleOf`)
> * String constraints (`minLength`, `maxLength`)

**Что из этого задевает нас.** Ограничение длины массива сверху задать нельзя:
`maxItems` в списке поддерживаемых нет. Значит поле `white_markings` схемой
ограничивается только перечнем допустимых значений, а не длиной списка. Отсекать
слишком длинный список придётся уже своим кодом на стороне узла-посредника.

---

## 3. Схема под наш набор черт

Ниже — схема, прямо соответствующая разделу 3 `cat-shelter-tech.md`. Каждое поле
перечислимое, `additionalProperties: false` на объекте — как требует
документация.

```json
{
  "type": "object",
  "properties": {
    "base_color": {
      "type": "string",
      "enum": ["ginger", "grey", "black", "white", "cream", "brown"]
    },
    "pattern": {
      "type": "string",
      "enum": ["solid", "tabby", "bicolor", "calico", "tuxedo", "pointed"]
    },
    "fur_length": {
      "type": "string",
      "enum": ["short", "long"]
    },
    "eye_color": {
      "type": "string",
      "enum": ["green", "amber", "blue"]
    },
    "white_markings": {
      "type": "array",
      "items": {
        "type": "string",
        "enum": ["chest", "paws", "face"]
      }
    }
  },
  "required": ["base_color", "pattern", "fur_length", "eye_color", "white_markings"],
  "additionalProperties": false
}
```

Поле `is_cat` в схему **не включено намеренно**: проверка «на снимке кот» —
это ступень 1 на устройстве через Apple Vision, а не работа облачной модели.
Если облако всё же должно уметь отказать, поле добавляется отдельным
логическим значением, но тогда придётся обрабатывать случай «Vision сказал кот,
модель сказала не кот» — лишняя развилка на ровном месте.

---

## 4. Рабочий вызов на Python

Соответствует `Python 3.12 + FastAPI` из раздела 4 `cat-shelter-tech.md`.

### Через Pydantic — предпочтительный путь

`client.messages.parse()` проверяет ответ по схеме и возвращает готовый объект:

```python
from enum import Enum
from typing import List

import anthropic
from pydantic import BaseModel


class BaseColor(str, Enum):
    ginger = "ginger"
    grey = "grey"
    black = "black"
    white = "white"
    cream = "cream"
    brown = "brown"


class Pattern(str, Enum):
    solid = "solid"
    tabby = "tabby"
    bicolor = "bicolor"
    calico = "calico"
    tuxedo = "tuxedo"
    pointed = "pointed"


class FurLength(str, Enum):
    short = "short"
    long = "long"


class EyeColor(str, Enum):
    green = "green"
    amber = "amber"
    blue = "blue"


class Marking(str, Enum):
    chest = "chest"
    paws = "paws"
    face = "face"


class CatTraits(BaseModel):
    base_color: BaseColor
    pattern: Pattern
    fur_length: FurLength
    eye_color: EyeColor
    white_markings: List[Marking]


client = anthropic.Anthropic()

SYSTEM = (
    "You describe the coat of a cat in a photograph. "
    "Report only what is visible. When a trait is ambiguous, choose the closest "
    "allowed value rather than guessing an unusual one."
)


def read_traits(image_b64: str, media_type: str = "image/jpeg") -> CatTraits:
    response = client.messages.parse(
        model="claude-haiku-4-5",
        max_tokens=1024,
        system=SYSTEM,
        messages=[{
            "role": "user",
            "content": [
                {
                    "type": "image",
                    "source": {
                        "type": "base64",
                        "media_type": media_type,
                        "data": image_b64,
                    },
                },
                {"type": "text", "text": "Describe this cat's coat."},
            ],
        }],
        output_format=CatTraits,
    )
    return response.parsed_output
```

Порядок содержимого — изображение, затем текст — соответствует прямой
рекомендации документации:

> Claude works best when images come before text. Images placed after text or
> interpolated with text still perform well, but if your use case allows it,
> prefer an image-then-text structure.

Обратите внимание на несовпадение имён, которое легко упустить: у
`client.messages.parse()` параметр называется `output_format` и принимает класс
Pydantic, а у `client.messages.create()` — `output_config` с сырой схемой. Это
разные вызовы, не разные написания одного.

### Через сырую схему

Когда Pydantic не нужен:

```python
response = client.messages.create(
    model="claude-haiku-4-5",
    max_tokens=1024,
    system=SYSTEM,
    messages=[{
        "role": "user",
        "content": [
            {"type": "image", "source": {"type": "base64",
                                         "media_type": "image/jpeg",
                                         "data": image_b64}},
            {"type": "text", "text": "Describe this cat's coat."},
        ],
    }],
    output_config={"format": {"type": "json_schema", "schema": TRAITS_SCHEMA}},
)

import json
text = next(b.text for b in response.content if b.type == "text")
traits = json.loads(text)
```

Документация отдельно оговаривает: `output_config.format` гарантирует, что
первый блок содержимого — текст с правильным JSON.

---

## 5. Ограничения по изображениям

Всё дословно со страницы Vision.

| Что | Значение |
|---|---|
| Поддерживаемые форматы | JPEG, PNG, GIF, WebP (`image/jpeg`, `image/png`, `image/gif`, `image/webp`) |
| Наибольший размер одного снимка | 10 MB в base64 при обращении напрямую к Claude API |
| Наибольшие размеры в пикселях | 8000×8000 px |
| Предел по длинной стороне, обычный разряд | 1568 px, до 1568 визуальных токенов |
| Предел по длинной стороне, повышенный разряд (Claude 4.7 и новее) | 2576 px, до 4784 визуальных токенов |
| Наибольшее число снимков в запросе | 100 при окне в 200k токенов, 600 у остальных |
| Наибольший размер запроса | 32 MB для обычных обращений |

Наши 512×512 укладываются в любой из пределов с запасом. Ограничение
«полезная нагрузка до 200 KB» из задачи 5.6 диктуется не Claude API, а нашим
собственным узлом-посредником, и оно строже, чем требует облако.

Существенное предупреждение о сжатии:

> Compressing images before sending them, using a lossy format such as JPEG or
> WebP (lossy mode), can reduce latency by reducing the size of requests.
> However, this can introduce artifacts that are detrimental to model
> performance, especially when multiple compression passes are applied.

Для нас это значит: снимок с камеры уже сжат в JPEG один раз, и наше уменьшение
до 512 с повторным сжатием — второй проход. Качество JPEG при повторном
сохранении стоит держать не ниже 85 и **проверить на эталонном наборе**, не
портится ли разбор окраса. Это ровно та проверка, которую предписывает
задача 5.2.

---

## 6. Ограничения модели, задевающие нашу задачу

Со страницы Vision, раздел Limitations:

- **Точность на мелких снимках.** «Claude might hallucinate or make mistakes when
  interpreting low-quality, rotated, or very small images under 200 pixels.»
  Наши 512 px безопасны, но обрезка по рамке Vision может дать меньший кусок,
  если кот в кадре мелкий. Стоит задать нижнюю границу: если рамка после обрезки
  меньше 200 px по стороне — не уменьшать, а брать более широкий кусок кадра.
- **Повёрнутые снимки.** Названы наравне с мелкими как источник ошибок.
  Ориентация должна быть выправлена на стороне Swift **до** отправки, а не
  оставлена в метаданных EXIF.
- **Метаданные не читаются.** «Claude does not parse or receive any metadata from
  images passed to it.» Ориентацию из EXIF облако не увидит — ещё один довод
  выправлять поворот на устройстве.
- **Непристойное содержимое.** «Claude does not process inappropriate or explicit
  images that violate the Acceptable Use Policy.» Полезно как второй рубеж, но
  опираться на него нельзя: отказ придёт в виде ошибки или отказа модели, а не
  предсказуемого признака в схеме.
- **Люди на снимке.** Модель отказывается называть людей. Если в кадр попал
  хозяин вместе с котом, задача «опиши окрас кота» этим не задевается, но
  случай стоит включить в эталонный набор.

---

## 7. Обработка отказа и ошибок

Ответ надо проверять на `stop_reason` до чтения содержимого. Значение `refusal`
означает, что сработали защитные разборщики; тогда в `stop_details` лежит
причина.

```python
if response.stop_reason == "refusal" and response.stop_details:
    # заменяем на кота по умолчанию, снимок не разбираем
    log.warning("refusal: %s", response.stop_details.category)
    return DEFAULT_TRAITS
```

Разбор ошибок обращения — цепочкой от частного к общему, а не одним широким
перехватом:

```python
import anthropic

try:
    response = client.messages.parse(...)
except anthropic.BadRequestError as e:
    ...
except anthropic.RateLimitError as e:
    retry_after = int(e.response.headers.get("retry-after", "60"))
    ...
except anthropic.APIStatusError as e:
    if e.status_code >= 500:
        ...
except anthropic.APIConnectionError:
    ...
```

SDK сам повторяет обращения при ошибках соединения, 408, 409, 429 и 5xx с
нарастающей задержкой, по умолчанию два повтора. Свой цикл повторов писать
поверх этого не нужно — только задать `max_retries`.

**Связь с запасным путём.** Раздел 3 `cat-shelter-tech.md` предусматривает работу
без сети через k-средних по цветам. Все перечисленные случаи — отказ, превышение
частоты, ошибка соединения — ведут в одну ветку: отдать `DEFAULT_TRAITS` либо
результат местного разбора. Игрок не должен видеть ошибку; он должен увидеть
кота.

---

## 8. Выбор модели: довод

| Довод | Haiku 4.5 | Sonnet 5 |
|---|---|---|
| Цена разбора | 0,10 цента | 0,20 цента |
| Structured outputs | поддерживает | поддерживает |
| Задача | выбор из 6 значений по картинке | то же |

Разница в 10 центов на 200 разборов не значит ничего. Значит только качество
разбора окраса, а его **нельзя узнать из документации** — только замером на
эталонном наборе из 40 снимков. Правильный порядок: собрать набор, прогнать
обе модели, сравнить руками, взять дешёвую, если разницы не видно.

Заметьте, что задача 5.2 в `cat-shelter-tasks.md` проверяет только разбираемость
ответа, а не точность окраса: «A ginger cat classified as cream is not a defect».
При таком условии приёмки обе модели пройдут одинаково, и выбор надо делать
глазами, а не тестом.

---

## Источники

- [Vision — platform.claude.com](https://platform.claude.com/docs/en/build-with-claude/vision)
- [Structured outputs — platform.claude.com](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- [Pricing — platform.claude.com](https://platform.claude.com/docs/en/about-claude/pricing)
- [Coordinates and bounding boxes](https://platform.claude.com/docs/en/build-with-claude/vision-coordinates)
- [Messages API — create](https://platform.claude.com/docs/en/api/messages/create)
</content>
</invoke>
