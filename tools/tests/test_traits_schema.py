"""Task 50-photo/03: the traits contract holds, and rejects rather than repairs."""
import json

import pytest

from tools.traits.validate import (MAX_WHITE_MARKINGS, SCHEMA, TraitsError,
                                   enum_of, output_config, parse, validate)

GOOD = {
    "base_color": "ginger",
    "pattern": "tabby",
    "fur_length": "short",
    "eye_color": "green",
    "white_markings": ["chest", "paws"],
}


def test_a_well_formed_response_passes_unchanged():
    assert validate(dict(GOOD)) == GOOD


def test_every_field_is_required():
    for field in SCHEMA["required"]:
        broken = {k: v for k, v in GOOD.items() if k != field}
        with pytest.raises(TraitsError, match=field):
            validate(broken)


def test_the_schema_matches_the_five_fields_in_the_tech_doc():
    # cat-shelter-tech.md section 3 names exactly these, and the sprite is
    # assembled from them: silhouette(fur_length) + fill(base_color) +
    # pattern mask + white markings + eyes.
    assert set(SCHEMA["properties"]) == {
        "base_color", "pattern", "fur_length", "eye_color", "white_markings"}
    assert enum_of("base_color") == ["ginger", "grey", "black", "white", "cream", "brown"]
    assert enum_of("pattern") == ["solid", "tabby", "bicolor", "calico", "tuxedo", "pointed"]
    assert enum_of("fur_length") == ["short", "long"]
    assert enum_of("eye_color") == ["green", "amber", "blue"]
    assert enum_of("white_markings") == ["chest", "paws", "face"]


@pytest.mark.parametrize("field,bad", [
    ("base_color", "orange"),      # a plausible synonym is still out of enum
    ("pattern", "striped"),
    ("fur_length", "medium"),
    ("eye_color", "yellow"),
])
def test_a_value_outside_the_enum_is_an_error_not_a_correction(field, bad):
    broken = dict(GOOD, **{field: bad})
    with pytest.raises(TraitsError, match=bad):
        validate(broken)


def test_additional_properties_are_refused_on_both_sides():
    assert SCHEMA["additionalProperties"] is False
    with pytest.raises(TraitsError, match="breed"):
        validate(dict(GOOD, breed="maine coon"))


def test_white_markings_cap_is_enforced_in_code():
    # maxItems is not a supported schema keyword, so the schema alone would
    # accept this and the Worker has to catch it.
    assert "maxItems" not in SCHEMA["properties"]["white_markings"]
    too_many = dict(GOOD, white_markings=["chest", "paws", "face", "chest"])
    with pytest.raises(TraitsError):
        validate(too_many)


def test_repeated_markings_are_refused():
    with pytest.raises(TraitsError, match="[Rr]epeated"):
        validate(dict(GOOD, white_markings=["chest", "chest"]))


def test_empty_markings_are_fine():
    # A cat with no white on it is a cat, not an error.
    assert validate(dict(GOOD, white_markings=[]))["white_markings"] == []


def test_full_set_of_markings_fits_the_cap():
    assert len(enum_of("white_markings")) == MAX_WHITE_MARKINGS
    assert validate(dict(GOOD, white_markings=["chest", "paws", "face"]))


@pytest.mark.parametrize("body", [
    "",
    "not json",
    "{",
    '"a string, not an object"',
    "[]",
    '{"base_color": 1, "pattern": "tabby", "fur_length": "short",'
    ' "eye_color": "green", "white_markings": []}',
])
def test_unparseable_bodies_raise_traits_error_and_nothing_else(body):
    with pytest.raises(TraitsError):
        parse(body)


def test_request_fragment_uses_output_config_not_output_format():
    # The parameter moved; the Python SDK 1.x raises TypeError on the old name.
    fragment = output_config()
    assert set(fragment) == {"format"}
    assert fragment["format"]["type"] == "json_schema"
    assert fragment["format"]["schema"] is SCHEMA
    assert json.dumps(fragment)   # has to survive serialisation
