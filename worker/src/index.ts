/**
 * Task 50-photo/02 + 04: the one thing standing between the game and the model.
 *
 * It exists for a single reason: a key shipped inside the app is extractable,
 * so the model key never leaves this Worker. Everything else here is a
 * consequence of that — the payload limits, the rate limit, the fact that the
 * photo is never written anywhere.
 *
 * The photo lives in memory for the length of the request and is not stored,
 * logged or forwarded anywhere but to the model. That is not a nicety: the
 * whole premise is "it is her cat", and a proxy that kept a copy of every
 * player's cat would be a different product with a different privacy story.
 */
import { TRAITS_SCHEMA } from "./schema";
import { TraitsError, validateTraits } from "./validate";

export interface Env {
	/** wrangler secret put ANTHROPIC_API_KEY — never in the repo. */
	ANTHROPIC_API_KEY: string;
	TRAITS_LIMITER: { limit(options: { key: string }): Promise<{ success: boolean }> };
	/** Overridable so a test can point at a stub. */
	ANTHROPIC_URL?: string;
	MODEL?: string;
}

/**
 * The crop is 512×512 under 200 KB (50-photo/07), which is about 270 KB once
 * base64 inflates it by a third. 400 KB leaves room for the envelope and still
 * rejects anything that is not our own crop.
 */
const MAX_BODY_BYTES = 400 * 1024;

const ALLOWED_MEDIA = new Set(["image/jpeg", "image/png"]);

/**
 * Haiku 4.5: the cheapest model that supports structured outputs. The choice
 * between this and Sonnet is meant to be settled by comparing colourings by
 * eye on the reference set (cat-shelter-tech.md section 3), which needs the
 * endpoint live — so this is the starting point, not the verdict.
 */
const DEFAULT_MODEL = "claude-haiku-4-5-20251001";

/**
 * Two instructions, not one, because the schema now holds two different kinds
 * of fact and the right instruction for one is the wrong instruction for the
 * other.
 *
 * The five coat fields are class characteristics: they narrow a pool and never
 * identify anybody. For those, "choose the closest allowed value" is right —
 * some answer is always better than none, and 288 possible cats is the
 * catalogue we meant to build.
 *
 * `spots` is the opposite. It is the only field that can identify one cat, and
 * it can only do that by being usually empty. A model told to pick the closest
 * value will find a mark on every cat, and a mark on every cat identifies
 * nobody — the field would cost a schema, a validator and a renderer and buy
 * exactly the recognition of a sixth class trait. So the instruction for it has
 * to push the other way: silence is the expected answer, and a mark is worth
 * reporting only when it survives being doubted.
 *
 * The asymmetry paragraph is doing the real work. "White paws" is coat; "one
 * white paw" is her. Left and right are already separate values in the enum
 * (CatSpot.cs), and a model that answers `paw_left` without checking which paw
 * it is looking at throws that away while appearing to comply.
 */
const PROMPT =
	"Describe this cat from the photograph. Report only what you can actually see.\n\n" +

	"base_color, pattern, fur_length, eye_color and white_markings describe her " +
	"coat. If one of them is genuinely ambiguous, choose the closest allowed " +
	"value rather than guessing at something unusual.\n\n" +

	"spots is not a coat description, and the opposite rule applies to it. A spot " +
	"is one distinctive mark — the thing that would let her owner pick her out of " +
	"a row of cats that otherwise look exactly like her: a patch over one eye, a " +
	"white sock on one front paw, a smudge on the chin. Most cats have none, and " +
	"an empty list is the ordinary and expected answer. Return an empty list " +
	"unless a mark genuinely stands out. Never add one to fill the field, and " +
	"when you are unsure, leave it out rather than choosing the closest place for " +
	"it: a mark on every cat identifies nobody.\n\n" +

	"Anything symmetrical is coat, not a mark. For the paired places — eye_left " +
	"and eye_right, paw_left and paw_right — report a mark only when it is on one " +
	"side and not the other. Two white front paws are white_markings; one white " +
	"front paw is exactly what spots is for, and which paw it is carries the whole " +
	"meaning, so look before you answer. Left and right are the cat's own, not " +
	"yours: when she faces the camera, her left side is on your right. Likewise a " +
	"white chest bib is white_markings, while a single odd patch on the chest is a " +
	"mark.\n\n" +

	"shade is relative to her own coat, not absolute: light means lighter than the " +
	"fur immediately around it, dark means darker. A white patch on a black cat is " +
	"light; a black patch on a white cat is dark.\n\n" +

	"At most two marks, and a second one only if it is as unmistakable as the " +
	"first. If several things seem worth listing, then none of them is " +
	"distinctive: give the single most conspicuous one, or none at all.";

function json(body: unknown, status: number): Response {
	return new Response(JSON.stringify(body), {
		status,
		headers: { "content-type": "application/json" },
	});
}

export default {
	async fetch(request: Request, env: Env): Promise<Response> {
		const url = new URL(request.url);

		if (url.pathname !== "/traits") return json({ error: "not found" }, 404);
		if (request.method !== "POST") {
			return json({ error: "use POST" }, 405, );
		}

		const declared = Number(request.headers.get("content-length") ?? 0);
		if (declared > MAX_BODY_BYTES) {
			// Refuse on the header before reading the body: a client sending
			// 50 MB should not get to spend our CPU on it.
			return json({ error: "payload too large" }, 413);
		}

		let payload: { image_base64?: unknown; media_type?: unknown; device_id?: unknown };
		try {
			payload = await request.json();
		} catch {
			return json({ error: "body is not JSON" }, 400);
		}

		const image = payload.image_base64;
		if (typeof image !== "string" || image.length === 0) {
			return json({ error: "image_base64 is required" }, 400);
		}
		if (image.length > MAX_BODY_BYTES) {
			// A chunked request has no content-length to check up front.
			return json({ error: "payload too large" }, 413);
		}

		const mediaType = typeof payload.media_type === "string"
			? payload.media_type
			: "image/jpeg";
		if (!ALLOWED_MEDIA.has(mediaType)) {
			return json({ error: `media_type must be one of ${[...ALLOWED_MEDIA].join(", ")}` }, 400);
		}

		// Keyed by the device the game reports rather than by IP — mobile
		// carriers share addresses, so an IP limit punishes bystanders. A
		// missing id is treated as one shared bucket, which throttles clients
		// that omit it rather than exempting them.
		const deviceId = typeof payload.device_id === "string" && payload.device_id.length > 0
			? payload.device_id
			: "anonymous";
		const { success } = await env.TRAITS_LIMITER.limit({ key: deviceId });
		if (!success) {
			return json({ error: "too many requests" }, 429);
		}

		const response = await fetch(env.ANTHROPIC_URL ?? "https://api.anthropic.com/v1/messages", {
			method: "POST",
			headers: {
				"content-type": "application/json",
				"x-api-key": env.ANTHROPIC_API_KEY,
				"anthropic-version": "2023-06-01",
			},
			body: JSON.stringify({
				model: env.MODEL ?? DEFAULT_MODEL,
				max_tokens: 512,
				// output_config, not output_format: the parameter moved, and the
				// old name now raises a TypeError in the SDKs.
				output_config: { format: { type: "json_schema", schema: TRAITS_SCHEMA } },
				messages: [
					{
						role: "user",
						content: [
							{ type: "image", source: { type: "base64", media_type: mediaType, data: image } },
							{ type: "text", text: PROMPT },
						],
					},
				],
			}),
		});

		if (!response.ok) {
			// The model's own error text is not passed through: it can carry
			// account details, and the game has nothing to do with it. The
			// status is enough for the game to fall back to the on-device
			// colour estimate (50-photo/11).
			return json({ error: "model call failed", status: response.status }, 502);
		}

		const answer = await response.json() as {
			content?: Array<{ type: string; text?: string }>;
		};
		const text = answer.content?.find((part) => part.type === "text")?.text;
		if (!text) {
			return json({ error: "model returned no text" }, 502);
		}

		let parsed: unknown;
		try {
			// Parsed here so a malformed answer becomes a 502 rather than
			// reaching the game as something it has to guess at.
			parsed = JSON.parse(text);
		} catch {
			return json({ error: "model returned unparseable JSON" }, 502);
		}

		try {
			// 50-photo/03: schema.json's enums and additionalProperties, plus
			// the maxItems-shaped cap validate.ts enforces in their place —
			// checked here, not just asked of the model, so an out-of-enum or
			// over-capped response becomes a 502 rather than reaching the game
			// as something it has to trust.
			return json(validateTraits(parsed), 200);
		} catch (error) {
			if (error instanceof TraitsError) {
				// The offending value stays server-side, not in the response —
				// same rule as the model's own error text above: the game gets
				// a status to fall back on, not detail.
				return json({ error: "model returned traits outside the schema" }, 502);
			}
			throw error;
		}
	},
};
