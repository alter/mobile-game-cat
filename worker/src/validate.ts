/**
 * Task 50-photo/03: the rule the schema alone cannot carry, ported from
 * `tools/traits/validate.py` because the Worker's own response has to obey
 * it too. `additionalProperties: false` and the enums are schema-level and
 * the model is asked to honour them, but nothing stops a model from
 * returning `["chest","chest","chest","chest"]` — `maxItems` is not a
 * supported `output_config` keyword (see `schema.ts`'s own header) — or from
 * simply not honouring the schema at all. Without this file, `index.ts`
 * parsed the model's JSON and returned it unchecked: the endpoint's own
 * OUTCOME line ("never free text and never an out-of-enum value") was not
 * actually enforced anywhere in this language, only in a Python module the
 * Worker never runs.
 *
 * Reject, never repair (50-photo/03-traits-schema/NOTES.md): a value outside
 * the enum throws rather than being coerced to the nearest allowed one — a
 * silently corrected trait paints the wrong cat and leaves no trace of why.
 */
import { TRAITS_SCHEMA } from "./schema";

type StringProperty = { type: "string"; enum: readonly string[] };
type ArrayProperty = { type: "array"; items: { type: "string"; enum: readonly string[] } };
type SchemaProperty = StringProperty | ArrayProperty;

const PROPERTIES = TRAITS_SCHEMA.properties as unknown as Record<string, SchemaProperty>;
const REQUIRED = TRAITS_SCHEMA.required as unknown as readonly string[];

// One entry per real marking; three is every value in the enum, so a longer
// list can only mean repetition. Read off the schema, like
// MAX_WHITE_MARKINGS in tools/traits/validate.py, rather than hardcoded, so
// the two cannot drift on this number even though they are two files.
const MAX_WHITE_MARKINGS = (PROPERTIES.white_markings as ArrayProperty).items.enum.length;

export class TraitsError extends Error {}

function isArrayProperty(prop: SchemaProperty): prop is ArrayProperty {
	return prop.type === "array";
}

function enumOf(field: string): readonly string[] {
	const prop = PROPERTIES[field];
	return isArrayProperty(prop) ? prop.items.enum : prop.enum;
}

/**
 * Validate a parsed model response against the schema, plus the cap and
 * no-duplicates rule `maxItems` cannot express. Returns the traits object
 * unchanged on success; throws {@link TraitsError} naming exactly what is
 * wrong on failure. Mirrors `tools/traits/validate.py`'s `validate()`
 * field-for-field so the two languages reject the same things.
 */
export function validateTraits(traits: unknown): Record<string, unknown> {
	if (typeof traits !== "object" || traits === null || Array.isArray(traits)) {
		throw new TraitsError(`expected an object, got ${traits === null ? "null" : typeof traits}`);
	}
	const obj = traits as Record<string, unknown>;

	const missing = REQUIRED.filter((field) => !(field in obj));
	if (missing.length > 0) {
		throw new TraitsError(`missing field(s): ${[...missing].sort().join(", ")}`);
	}

	const propertyNames = new Set(Object.keys(PROPERTIES));
	const extra = Object.keys(obj).filter((key) => !propertyNames.has(key));
	if (extra.length > 0) {
		// additionalProperties: false is in the schema, but a model response
		// is not guaranteed to honour it any more than the enums are.
		throw new TraitsError(`unexpected field(s): ${extra.sort().join(", ")}`);
	}

	for (const field of [...REQUIRED].sort()) {
		const value = obj[field];
		const allowed = enumOf(field);
		const property = PROPERTIES[field];

		if (isArrayProperty(property)) {
			if (!Array.isArray(value)) {
				throw new TraitsError(`${field}: expected a list, got ${typeof value}`);
			}
			if (value.length > MAX_WHITE_MARKINGS) {
				throw new TraitsError(
					`${field}: ${value.length} entries, at most ${MAX_WHITE_MARKINGS}`);
			}
			if (new Set(value).size !== value.length) {
				throw new TraitsError(`${field}: repeated entries in ${JSON.stringify(value)}`);
			}
			for (const item of value) {
				if (typeof item !== "string" || !allowed.includes(item)) {
					throw new TraitsError(`${field}: '${item}' is not one of ${allowed.join(", ")}`);
				}
			}
		} else {
			if (typeof value !== "string") {
				throw new TraitsError(`${field}: expected a string, got ${typeof value}`);
			}
			if (!allowed.includes(value)) {
				throw new TraitsError(`${field}: '${value}' is not one of ${allowed.join(", ")}`);
			}
		}
	}

	return obj;
}
