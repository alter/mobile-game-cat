# Vision model: coat traits as strict JSON

Date collected: 2026-08-24
Relation to the project: stage 2 of photo analysis (`cat-shelter-tech.md`, section 3),
intermediary node `POST /traits` (`/tools/traits`).

---

## In brief

1. The cost of analyzing one 512×512 snapshot has been calculated at current prices:
   **about 0.10 cents on Claude Haiku 4.5** and **about 0.20 cents on Claude Sonnet 5**.
   The "0.1–0.3 cents" estimate from `cat-shelter-tech.md` is confirmed. The calculation is below,
   with the formula from the documentation.
2. Asking the model in words to "respond with JSON only" is an outdated technique. There is
   **structured outputs**: the `output_config.format` parameter with a JSON Schema, and the response
   is guaranteed to match the schema. A beta header is no longer needed.
3. Enumerable values are set with the `enum` keyword directly in the schema — this is
   exactly what's needed for `base_color`, `pattern`, `fur_length`, `eye_color`.
   Values outside the list cannot become the answer.
4. `additionalProperties: false` for every object is a **mandatory**
   schema requirement, not a suggestion.
5. Image cost is calculated by the number of 28×28-pixel patches:
   `⌈width / 28⌉ × ⌈height / 28⌉`. For 512×512 that is 361 tokens.
6. The image should be placed **before** the text in the message content —
   a direct recommendation of the documentation.
7. Snapshots are not stored on Anthropic's side: "Image uploads are ephemeral and not
   stored beyond the duration of the API request." This backs up the promise that "the snapshot
   isn't stored anywhere."
8. The model does not process indecent images that violate the acceptable-use
   policy. This is an additional, but **not the primary**, safeguard —
   the primary one remains on-device Apple Vision.
9. The model does not name people in photographs and refuses to do so. For our
   task this doesn't matter, but it's worth knowing when analyzing a snapshot with a person in frame.
10. For an enumerated pick among six values, the reasonable choice is **Claude Haiku 4.5**:
    it supports structured outputs and costs a quarter of Sonnet 5.

---

## 1. Cost: a calculation, not a guess

### The formula

The documentation states it verbatim:

> Claude views images in patches instead of pixels. Each patch is a 28×28-pixel
> block of the image, referred to as a visual token. An image, therefore, costs
> `⌈width / 28⌉ × ⌈height / 28⌉` visual tokens.

([Vision](https://platform.claude.com/docs/en/build-with-claude/vision))

For our 512×512 snapshot: `⌈512 / 28⌉ = ⌈18.29⌉ = 19`, so `19 × 19 = 361`
visual tokens. There is no reduction here: the long-side limit is
1568 px for the standard tier and 2576 px for the elevated tier, and 512 is below both.

### Prices as of 2026-08-24

| Model | Input, $/1M | Output, $/1M |
|---|---|---|
| Claude Haiku 4.5 | 1 | 5 |
| Claude Sonnet 5 | 2 | 10 |
| Claude Opus 5 | 5 | 25 |

([Pricing](https://platform.claude.com/docs/en/about-claude/pricing))

Worth knowing separately: the promotional price of 2/10 announced when Sonnet 5 launched
**has become permanent**; there will be no increase to 3/15 on September 1, 2026 —
this is stated plainly on the pricing page.

### Cost of a single analysis

Assumptions: a 512×512 snapshot = 361 tokens, a prompt with the schema ≈ 250 input tokens,
a response ≈ 80 tokens. Total 611 input tokens.

| Model | Input | Output | Total | In cents |
|---|---|---|---|---|
| Haiku 4.5 | 611 × $1/1M = $0.00061 | 80 × $5/1M = $0.00040 | $0.00101 | **0.10** |
| Sonnet 5 | 611 × $2/1M = $0.00122 | 80 × $10/1M = $0.00080 | $0.00202 | **0.20** |
| Opus 5 | 611 × $5/1M = $0.00306 | 80 × $25/1M = $0.00200 | $0.00506 | **0.51** |

At 500 verification installs and a 40% download rate, that's 200 analyses:
**20 cents on Haiku, 40 cents on Sonnet** for the whole MVP. A line item
negligible enough to ignore: it is a thousand times smaller than the $400 for the retention
check.

Conclusion for the project: the "is the cloud call expensive" debate is closed. The model
should be chosen for coat-analysis quality, not for price.

### What the calculation doesn't cover

Prompt caching won't help here: the smallest cacheable segment is around 1024
tokens, and our prompt is shorter. The right conclusion is **don't try**
to bolt caching onto this call.

---

## 2. Structured outputs instead of "respond with JSON only"

### Why not a prompt

The instruction "return strict JSON" gives an answer that **usually** parses. Item 5.2
in `cat-shelter-tasks.md` requires 100% parse success on a reference set of 40 snapshots.
A prompt doesn't reliably clear that bar — a schema does.

### Request shape

Verbatim from the documentation:

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

### The beta header is no longer needed

> The `output_format` parameter has moved to `output_config.format`, and beta
> headers are no longer required. The API continues to accept the old beta header
> (`structured-outputs-2025-11-13`) and the `output_format` request field for a
> transition period, but the Python SDK (v1.0 and later) does not accept
> `output_format={...}` on `client.beta.messages.create()` or `count_tokens()`
> and raises a `TypeError`; use `output_config` instead.

This matters: if an agent writes `output_format=...` from memory, on Python SDK 1.x
it will get a `TypeError`. The correct one is `output_config`.

### Which models support it

> Supported models: `claude-fable-5`, `claude-mythos-5`, `claude-mythos-preview`,
> `claude-opus-5`, `claude-opus-4-8`, `claude-opus-4-7`, `claude-opus-4-6`,
> `claude-sonnet-5`, `claude-sonnet-4-6`, `claude-sonnet-4-5-20250929`,
> `claude-opus-4-5-20251101`, `claude-haiku-4-5-20251001`

Haiku 4.5 is on the list — meaning the cheapest path is open to us.

### What the schema can and can't do

Supported (verbatim):

> * All basic types: object, array, string, integer, number, boolean, null
> * `enum` (strings, numbers, bools, or nulls only - no complex types)
> * `const`
> * `anyOf` and `allOf` (with limitations - `allOf` with `$ref` not supported)
> * `$ref`, `$def`, and `definitions` (external `$ref` not supported)
> * `default` property for all supported types
> * `required` and `additionalProperties` (must be set to `false` for objects)
> * String formats: `date-time`, `time`, `date`, `duration`, `email`, `hostname`, `uri`, `ipv4`, `ipv6`, `uuid`
> * Array `minItems` (only values 0 and 1 supported)

Not supported (verbatim):

> * Recursive schemas
> * Complex types within enums
> * External `$ref` (for example, `'$ref': 'http://...'`)
> * Numerical constraints (such as `minimum`, `maximum`, `multipleOf`)
> * String constraints (`minLength`, `maxLength`)

**What in there affects us.** An upper bound on array length can't be set:
`maxItems` is not in the supported list. So the `white_markings` field is
restricted by the schema only to the list of allowed values, not by list length. Cutting off
an overly long list will have to be done by our own code on the intermediary-node side.

---

## 3. Schema for our trait set

Below is a schema matching directly to section 3 of `cat-shelter-tech.md`. Every field is
an enum, `additionalProperties: false` on the object — as the
documentation requires.

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

The `is_cat` field is **deliberately not included** in the schema: checking "is this
a cat in the photo" is stage 1, on-device via Apple Vision, not the cloud model's job.
If the cloud does need to be able to refuse, the field is added as a separate
boolean, but then the case "Vision said cat, model said not cat" has to be
handled — an unnecessary branch for no reason.

---

## 4. A working call in Python

Matches `Python 3.12 + FastAPI` from section 4 of `cat-shelter-tech.md`.

### Via Pydantic — the preferred path

`client.messages.parse()` validates the response against the schema and returns a ready-made object:

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

The content order — image, then text — matches the documentation's direct
recommendation:

> Claude works best when images come before text. Images placed after text or
> interpolated with text still perform well, but if your use case allows it,
> prefer an image-then-text structure.

Note a naming mismatch that's easy to miss: for
`client.messages.parse()` the parameter is called `output_format` and takes a
Pydantic class, while for `client.messages.create()` it's `output_config` with a raw
schema. These are different calls, not different spellings of the same one.

### Via a raw schema

When Pydantic isn't needed:

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

The documentation separately notes: `output_config.format` guarantees that
the first content block is text with valid JSON.

---

## 5. Image constraints

All verbatim from the Vision page.

| What | Value |
|---|---|
| Supported formats | JPEG, PNG, GIF, WebP (`image/jpeg`, `image/png`, `image/gif`, `image/webp`) |
| Largest size of a single snapshot | 10 MB in base64 when calling the Claude API directly |
| Largest dimensions in pixels | 8000×8000 px |
| Long-side limit, standard tier | 1568 px, up to 1568 visual tokens |
| Long-side limit, elevated tier (Claude 4.7 and newer) | 2576 px, up to 4784 visual tokens |
| Largest number of images per request | 100 with a 200k-token window, 600 for the rest |
| Largest request size | 32 MB for regular requests |

Our 512×512 fits comfortably within any of these limits. The
"payload up to 200 KB" constraint from task 5.6 is dictated not by the Claude API but by our
own intermediary node, and it's stricter than what the cloud requires.

A significant warning about compression:

> Compressing images before sending them, using a lossy format such as JPEG or
> WebP (lossy mode), can reduce latency by reducing the size of requests.
> However, this can introduce artifacts that are detrimental to model
> performance, especially when multiple compression passes are applied.

For us this means: a snapshot from the camera is already JPEG-compressed once, and our downscaling
to 512 with re-compression is a second pass. The JPEG quality on re-save should be kept
at no lower than 85, and **checked against the reference set** to make sure it doesn't
degrade the coat analysis. This is exactly the check that task 5.2 requires.

---

## 6. Model limitations affecting our task

From the Vision page's Limitations section:

- **Accuracy on small images.** "Claude might hallucinate or make mistakes when
  interpreting low-quality, rotated, or very small images under 200 pixels."
  Our 512 px is safe, but a Vision bounding-box crop could give a smaller
  piece if the cat in frame is small. It's worth setting a lower bound: if the box after cropping
  is smaller than 200 px on a side — don't downscale, take a wider crop of the frame instead.
- **Rotated images.** Named alongside small ones as a source of errors.
  Orientation must be corrected on the Swift side **before** sending, not
  left to the EXIF metadata.
- **Metadata is not read.** "Claude does not parse or receive any metadata from
  images passed to it." The cloud won't see orientation from EXIF — one more reason
  to correct rotation on-device.
- **Indecent content.** "Claude does not process inappropriate or explicit
  images that violate the Acceptable Use Policy." Useful as a second line of defense, but
  it can't be relied on: the refusal arrives as an error or a model refusal, not a
  predictable field in the schema.
- **People in the photo.** The model refuses to name people. If the owner is
  in frame along with the cat, the task "describe the cat's coat" is unaffected, but
  the case is worth including in the reference set.

---

## 7. Handling refusals and errors

The response must be checked for `stop_reason` before reading the content. The value `refusal`
means the safety classifiers triggered; then the reason is in
`stop_details`.

```python
if response.stop_reason == "refusal" and response.stop_details:
    # fall back to a default cat, don't analyze the snapshot
    log.warning("refusal: %s", response.stop_details.category)
    return DEFAULT_TRAITS
```

Errors from the call should be handled in a chain from specific to general, not one broad
catch-all:

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

The SDK itself retries calls on connection errors, 408, 409, 429, and 5xx with
increasing backoff, two retries by default. There's no need to write your own retry loop
on top of this — just set `max_retries`.

**Connection to the fallback path.** Section 3 of `cat-shelter-tech.md` provides for operation
without a network, via k-means over colors. All the listed cases — refusal, rate
limiting, connection error — lead into the same branch: return `DEFAULT_TRAITS` or
the result of local analysis. The player must not see an error; they must see
a cat.

---

## 8. Choosing the model: the argument

| Argument | Haiku 4.5 | Sonnet 5 |
|---|---|---|
| Analysis cost | 0.10 cents | 0.20 cents |
| Structured outputs | supported | supported |
| Task | pick from 6 values based on an image | same |

A 10-cent difference across 200 analyses means nothing. What matters is only the quality of the
coat analysis, and it **cannot be known from the documentation** — only by measuring
against the 40-snapshot reference set. The right order of operations: build the set, run
both models, compare by eye, take the cheap one if there's no visible difference.

Note that task 5.2 in `cat-shelter-tasks.md` checks only that the response parses,
not coat-color accuracy: "A ginger cat classified as cream is not a defect."
Under that acceptance criterion, both models will pass equally, and the choice should be made
by eye, not by test.

---

## Sources

- [Vision — platform.claude.com](https://platform.claude.com/docs/en/build-with-claude/vision)
- [Structured outputs — platform.claude.com](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- [Pricing — platform.claude.com](https://platform.claude.com/docs/en/about-claude/pricing)
- [Coordinates and bounding boxes](https://platform.claude.com/docs/en/build-with-claude/vision-coordinates)
- [Messages API — create](https://platform.claude.com/docs/en/api/messages/create)