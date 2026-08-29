/**
 * Task 50-photo/03: the rule the schema alone cannot carry, ported from
 * `tools/traits/validate.py` because the Worker's own response has to obey
 * it too. `additionalProperties: false` and the enums are schema-level and
 * the model is asked to honour them, but nothing stops a model from
 * returning `["chest","chest","chest","chest"]` — `maxItems` is not a
 * supported `output_config` keyword (see MAX_SPOTS below) — or from simply
 * not honouring the schema at all. Without this file, `index.ts` parsed the
 * model's JSON and returned it unchecked: the endpoint's own OUTCOME line
 * ("never free text and never an out-of-enum value") was not actually
 * enforced anywhere in this language, only in a Python module the Worker
 * never runs.
 *
 * Reject, never repair (50-photo/03-traits-schema/NOTES.md): a value outside
 * the enum throws rather than being coerced to the nearest allowed one — a
 * silently corrected trait paints the wrong cat and leaves no trace of why.
 * That rule matters most for `spots`, the one field that is supposed to be
 * empty most of the time: a repaired mark is a mark she does not have, and
 * the whole point of the field is that the player recognises it.
 */
import { TRAITS_SCHEMA } from "./schema";

type StringProperty = { type: "string"; enum: readonly string[] };
type StringArrayProperty = { type: "array"; items: StringProperty };
/**
 * `spots`: a list of objects rather than a list of strings, because a mark is
 * two facts at once — where it is and whether it is lighter or darker than the
 * coat around it — and neither is worth anything alone.
 */
type ObjectArrayProperty = {
	type: "array";
	items: {
		type: "object";
		properties: Record<string, StringProperty>;
		required: readonly string[];
		additionalProperties: false;
	};
};
type SchemaProperty = StringProperty | StringArrayProperty | ObjectArrayProperty;

const PROPERTIES = TRAITS_SCHEMA.properties as unknown as Record<string, SchemaProperty>;
const REQUIRED = TRAITS_SCHEMA.required as unknown as readonly string[];

// One entry per real marking; three is every value in the enum, so a longer
// list can only mean repetition. Read off the schema, like
// MAX_WHITE_MARKINGS in tools/traits/validate.py, rather than hardcoded, so
// the two cannot drift on this number even though they are two files.
const MAX_WHITE_MARKINGS = (PROPERTIES.white_markings as StringArrayProperty).items.enum.length;

/**
 * At most two marks — and unlike MAX_WHITE_MARKINGS this one cannot be read
 * off the schema, in two senses.
 *
 * It is not the size of the enum: there are ten places a mark can be, and a
 * cat wearing ten of them is a cat nobody looked at. Two is a judgement about
 * what identifies her, not a count of the vocabulary.
 *
 * And it cannot live in schema.json either. `maxItems` is not a supported
 * structured-outputs keyword, and an unsupported keyword is not ignored — the
 * request comes back 400 (knowledge/vision-model/01-traits-strict-json.md, and
 * the Structured outputs page it cites). So schema.json carries no maxItems and
 * the cap is enforced here, on the way out, exactly as it is for
 * white_markings.
 *
 * Exported so a test can hold it against `CatTraits.MaxSpots` in
 * game/Assets/Core/CatTraits.cs: two languages, one number, and nothing but
 * that test standing between them.
 */
export const MAX_SPOTS = 2;

/** The member of a spot that must be unique across the list; see below. */
const SPOT_PLACE = "place";

export class TraitsError extends Error {}

function isArrayProperty(prop: SchemaProperty): prop is StringArrayProperty | ObjectArrayProperty {
	return prop.type === "array";
}

function isObjectArrayProperty(prop: SchemaProperty): prop is ObjectArrayProperty {
	return prop.type === "array" && prop.items.type === "object";
}

function enumOf(field: string): readonly string[] {
	const prop = PROPERTIES[field];
	if (isObjectArrayProperty(prop)) {
		// Nothing to compare a whole object against; validateSpots reads the
		// per-member enums itself.
		throw new TraitsError(`${field}: has no enum of its own`);
	}
	return isArrayProperty(prop) ? prop.items.enum : prop.enum;
}

function describe(value: unknown): string {
	if (value === null) return "null";
	if (Array.isArray(value)) return "array";
	return typeof value;
}

/**
 * The marks. Every rule here mirrors `CatTraits`/`CatSpot` in the game so a
 * response the Worker accepts is a response the game can construct — the C#
 * constructor throws on the same four things, and a trait that passes here
 * only to throw on device would be a crash the player sees instead of a 502
 * she never notices.
 */
function validateSpots(field: string, value: unknown, property: ObjectArrayProperty): void {
	if (!Array.isArray(value)) {
		throw new TraitsError(`${field}: expected a list, got ${describe(value)}`);
	}
	if (value.length > MAX_SPOTS) {
		throw new TraitsError(`${field}: ${value.length} entries, at most ${MAX_SPOTS}`);
	}

	const members = property.items.properties;
	const memberNames = new Set(Object.keys(members));
	const requiredMembers = [...property.items.required].sort();

	const seenPlaces = new Set<string>();
	for (const item of value) {
		if (typeof item !== "object" || item === null || Array.isArray(item)) {
			throw new TraitsError(`${field}: expected an object, got ${describe(item)}`);
		}
		const spot = item as Record<string, unknown>;

		// Both members or neither. "a dark something" and "a something on the
		// chin" are each half a mark, and half a mark cannot be drawn.
		const missing = requiredMembers.filter((member) => !(member in spot));
		if (missing.length > 0) {
			throw new TraitsError(`${field}: missing ${missing.join(", ")}`);
		}
		const extra = Object.keys(spot).filter((key) => !memberNames.has(key));
		if (extra.length > 0) {
			// additionalProperties: false is on the nested object too, and is
			// no more guaranteed there than at the top level. A "size" or
			// "shape" the model volunteered is a field nothing draws.
			throw new TraitsError(`${field}: unexpected member(s): ${extra.sort().join(", ")}`);
		}

		for (const member of requiredMembers) {
			const memberValue = spot[member];
			const allowed = members[member].enum;
			if (typeof memberValue !== "string") {
				throw new TraitsError(
					`${field}.${member}: expected a string, got ${describe(memberValue)}`);
			}
			if (!allowed.includes(memberValue)) {
				throw new TraitsError(
					`${field}.${member}: '${memberValue}' is not one of ${allowed.join(", ")}`);
			}
		}

		// Two marks in the same place is one mark described twice — and drawn
		// twice, one patch over the other, which is either invisible or a
		// darker patch nobody asked for. Same rule as CatTraits' "two spots in
		// the same place". Shade is deliberately not part of the key: "light
		// chin" and "dark chin" are not two marks, they are a model that could
		// not decide.
		const place = spot[SPOT_PLACE] as string;
		if (seenPlaces.has(place)) {
			throw new TraitsError(`${field}: two marks on the ${place}`);
		}
		seenPlaces.add(place);
	}
}

/**
 * Validate a parsed model response against the schema, plus the caps and
 * no-duplicates rules `maxItems` cannot express. Returns the traits object
 * unchanged on success; throws {@link TraitsError} naming exactly what is
 * wrong on failure. Mirrors `tools/traits/validate.py`'s `validate()`
 * field-for-field so the two languages reject the same things.
 */
export function validateTraits(traits: unknown): Record<string, unknown> {
	if (typeof traits !== "object" || traits === null || Array.isArray(traits)) {
		throw new TraitsError(`expected an object, got ${describe(traits)}`);
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
		const property = PROPERTIES[field];

		if (isObjectArrayProperty(property)) {
			validateSpots(field, value, property);
			continue;
		}

		const allowed = enumOf(field);

		if (isArrayProperty(property)) {
			if (!Array.isArray(value)) {
				throw new TraitsError(`${field}: expected a list, got ${describe(value)}`);
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
				throw new TraitsError(`${field}: expected a string, got ${describe(value)}`);
			}
			if (!allowed.includes(value)) {
				throw new TraitsError(`${field}: '${value}' is not one of ${allowed.join(", ")}`);
			}
		}
	}

	return obj;
}
