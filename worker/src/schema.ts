/**
 * The response contract, generated from tools/traits/schema.json.
 *
 * Do not edit by hand: that file is the single definition shared with the
 * game and the Python checks, and a second copy would drift. Regenerate with
 *   python worker/sync-schema.py
 */
export const TRAITS_SCHEMA = {
  "type": "object",
  "properties": {
    "base_color": {
      "type": "string",
      "enum": [
        "ginger",
        "grey",
        "black",
        "white",
        "cream",
        "brown"
      ]
    },
    "pattern": {
      "type": "string",
      "enum": [
        "solid",
        "tabby",
        "bicolor",
        "calico",
        "tuxedo",
        "pointed"
      ]
    },
    "fur_length": {
      "type": "string",
      "enum": [
        "short",
        "long"
      ]
    },
    "eye_color": {
      "type": "string",
      "enum": [
        "green",
        "amber",
        "blue"
      ]
    },
    "white_markings": {
      "type": "array",
      "items": {
        "type": "string",
        "enum": [
          "chest",
          "paws",
          "face"
        ]
      }
    }
  },
  "required": [
    "base_color",
    "pattern",
    "fur_length",
    "eye_color",
    "white_markings"
  ],
  "additionalProperties": false
} as const;
