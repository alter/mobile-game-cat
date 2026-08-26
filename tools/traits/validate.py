"""Task 50-photo/03: the traits contract, and the one rule the schema cannot carry.

`schema.json` is the exact object handed to the model as
`output_config.format.schema`. It is data, not code, so the Worker, the game and
these checks all read the same file rather than three copies that drift.

Two things live here instead of in the schema:

* **The cap on `white_markings`.** `maxItems` is not a supported schema keyword
  (knowledge/vision-model/01-traits-strict-json.md), so a model could return
  "chest" four times and still satisfy the schema. The cap and the
  no-duplicates rule are enforced in code, on the way out of the Worker.
* **Rejection, not repair.** A response outside the enums is an error, not
  something to coerce into the nearest value: a silently corrected trait would
  paint the wrong cat and nobody would ever see why.
"""
from __future__ import annotations

import json
from pathlib import Path

SCHEMA_PATH = Path(__file__).with_name("schema.json")
SCHEMA = json.loads(SCHEMA_PATH.read_text())

# One entry per real marking; three is every value in the enum, so a longer
# list can only mean repetition.
MAX_WHITE_MARKINGS = len(
    SCHEMA["properties"]["white_markings"]["items"]["enum"])


class TraitsError(ValueError):
    """A response that cannot be trusted to describe a cat."""


def enum_of(field: str) -> list[str]:
    prop = SCHEMA["properties"][field]
    return list(prop["items"]["enum"] if prop["type"] == "array" else prop["enum"])


def validate(traits: dict) -> dict:
    """Return the traits unchanged, or raise TraitsError saying what is wrong."""
    if not isinstance(traits, dict):
        raise TraitsError(f"expected an object, got {type(traits).__name__}")

    required = set(SCHEMA["required"])
    missing = required - traits.keys()
    if missing:
        raise TraitsError(f"missing field(s): {', '.join(sorted(missing))}")

    extra = traits.keys() - set(SCHEMA["properties"])
    if extra:
        # additionalProperties:false is in the schema, but a Worker that
        # assembles a response by hand can still add one.
        raise TraitsError(f"unexpected field(s): {', '.join(sorted(extra))}")

    for field in sorted(required):
        value = traits[field]
        allowed = enum_of(field)
        if SCHEMA["properties"][field]["type"] == "array":
            if not isinstance(value, list):
                raise TraitsError(f"{field}: expected a list, got {type(value).__name__}")
            if len(value) > MAX_WHITE_MARKINGS:
                raise TraitsError(
                    f"{field}: {len(value)} entries, at most {MAX_WHITE_MARKINGS}")
            if len(set(value)) != len(value):
                raise TraitsError(f"{field}: repeated entries in {value}")
            for item in value:
                if item not in allowed:
                    raise TraitsError(f"{field}: '{item}' is not one of {allowed}")
        else:
            if not isinstance(value, str):
                raise TraitsError(f"{field}: expected a string, got {type(value).__name__}")
            if value not in allowed:
                raise TraitsError(f"{field}: '{value}' is not one of {allowed}")

    return traits


def parse(text: str) -> dict:
    """Validate a raw response body. Anything unparseable is a TraitsError."""
    try:
        return validate(json.loads(text))
    except json.JSONDecodeError as error:
        raise TraitsError(f"not JSON: {error.msg}") from error


def output_config() -> dict:
    """The request fragment to send to the model.

    `output_config.format`, not `output_format`: the parameter moved, and the
    Python SDK 1.x raises TypeError on the old name
    (knowledge/vision-model/01-traits-strict-json.md).
    """
    return {"format": {"type": "json_schema", "schema": SCHEMA}}
