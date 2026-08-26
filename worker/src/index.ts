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

const PROMPT =
	"Describe this cat's coat. Report only what is visible in the photograph. " +
	"If a field is genuinely ambiguous, choose the closest allowed value rather " +
	"than guessing at something unusual.";

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

		try {
			// Parsed here so a malformed answer becomes a 502 rather than
			// reaching the game as something it has to guess at.
			return json(JSON.parse(text), 200);
		} catch {
			return json({ error: "model returned unparseable JSON" }, 502);
		}
	},
};
