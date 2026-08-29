/**
 * Task 50-photo/02 VERIFY 1 and 04: the four status codes, plus the limit.
 *
 * Runs against the handler directly with a stubbed model endpoint, so it needs
 * no account, no key and no network — which matters, because the account is
 * exactly what this task is blocked on.
 */
import { describe, expect, it, vi } from "vitest";
import worker, { type Env } from "../src/index";
import { MAX_SPOTS } from "../src/validate";

const ONE_PIXEL_JPEG_BASE64 =
	"/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0a" +
	"HBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAFAABAAAAAAAA" +
	"AAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==";

function env(overrides: Partial<Env> = {}): Env {
	return {
		ANTHROPIC_API_KEY: "test-key-not-a-real-one",
		TRAITS_LIMITER: { limit: async () => ({ success: true }) },
		ANTHROPIC_URL: "https://stub.invalid/v1/messages",
		...overrides,
	};
}

/**
 * A path next to this file, as a string.
 *
 * The URL object itself would do at runtime, but not to tsc:
 * @cloudflare/workers-types and @types/node each declare their own `URL`, the
 * two are structurally different, and `readFileSync` wants node's. A decoded
 * pathname is a string and belongs to neither.
 */
function pathTo(relative: string): string {
	return decodeURIComponent(new URL(relative, import.meta.url).pathname);
}

function post(body: unknown, headers: Record<string, string> = {}): Request {
	return new Request("https://proxy.example/traits", {
		method: "POST",
		headers: { "content-type": "application/json", ...headers },
		body: typeof body === "string" ? body : JSON.stringify(body),
	});
}

// A fresh Response per call: one shared instance can only be read once, and
// the second test to use it fails with "body already read" — which looks like
// a Worker bug and is not one.
function stubModel(payload: unknown, status = 200) {
	return vi.spyOn(globalThis, "fetch").mockImplementation(async () =>
		new Response(JSON.stringify(payload), { status }));
}

// An unmarked cat: `spots` is empty here on purpose, because empty is what
// the field is supposed to be nearly all of the time. Every marked case below
// spreads over this one.
const GOOD_TRAITS = {
	base_color: "ginger",
	pattern: "tabby",
	fur_length: "short",
	eye_color: "green",
	white_markings: ["chest"],
	spots: [],
};

/** GOOD_TRAITS wearing the marks given, as the model would have returned it. */
function withSpots(spots: unknown) {
	return { ...GOOD_TRAITS, spots };
}

/** Answer with `traits` from the stubbed model and return the Worker's reply. */
async function answering(traits: unknown): Promise<Response> {
	stubModel({ content: [{ type: "text", text: JSON.stringify(traits) }] });
	return worker.fetch(
		post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
		env(),
	);
}

describe("POST /traits", () => {
	it("answers 200 with the model's traits for a well-formed request", async () => {
		stubModel({ content: [{ type: "text", text: JSON.stringify(GOOD_TRAITS) }] });

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, media_type: "image/jpeg", device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(200);
		expect(await response.json()).toEqual(GOOD_TRAITS);
	});

	it("rejects malformed JSON with 400", async () => {
		const response = await worker.fetch(post("{ not json"), env());
		expect(response.status).toBe(400);
	});

	it("rejects an empty image with 400", async () => {
		const response = await worker.fetch(post({ image_base64: "" }), env());
		expect(response.status).toBe(400);
	});

	it("rejects a missing image with 400", async () => {
		const response = await worker.fetch(post({ device_id: "d1" }), env());
		expect(response.status).toBe(400);
	});

	it("rejects an oversized payload with 413, by header, before reading it", async () => {
		const response = await worker.fetch(
			post({ image_base64: "x" }, { "content-length": String(5 * 1024 * 1024) }),
			env(),
		);
		expect(response.status).toBe(413);
	});

	it("rejects an oversized image with 413 even when the header lies", async () => {
		const response = await worker.fetch(
			post({ image_base64: "x".repeat(500 * 1024) }),
			env(),
		);
		expect(response.status).toBe(413);
	});

	it("rejects a media type it will not send on", async () => {
		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, media_type: "image/heic" }),
			env(),
		);
		expect(response.status).toBe(400);
	});

	it("refuses anything but POST /traits", async () => {
		const get = new Request("https://proxy.example/traits");
		expect((await worker.fetch(get, env())).status).toBe(405);

		const elsewhere = new Request("https://proxy.example/", { method: "POST", body: "{}" });
		expect((await worker.fetch(elsewhere, env())).status).toBe(404);
	});
});

describe("the limit", () => {
	it("returns 429 past the burst, keyed by device rather than IP", async () => {
		const seen: string[] = [];
		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "the-device" }),
			env({
				TRAITS_LIMITER: {
					limit: async ({ key }) => {
						seen.push(key);
						return { success: false };
					},
				},
			}),
		);

		expect(response.status).toBe(429);
		expect(seen).toEqual(["the-device"]);
	});

	it("throttles clients that send no device id instead of exempting them", async () => {
		stubModel({ content: [{ type: "text", text: JSON.stringify(GOOD_TRAITS) }] });
		const seen: string[] = [];
		await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64 }),
			env({
				TRAITS_LIMITER: {
					limit: async ({ key }) => {
						seen.push(key);
						return { success: true };
					},
				},
			}),
		);
		expect(seen).toEqual(["anonymous"]);
	});
});

describe("what reaches the model, and what comes back", () => {
	it("sends the key in the header and the schema in output_config", async () => {
		const spy = stubModel({ content: [{ type: "text", text: JSON.stringify(GOOD_TRAITS) }] });

		await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		const [, init] = spy.mock.calls[0] as [string, RequestInit];
		expect((init.headers as Record<string, string>)["x-api-key"])
			.toBe("test-key-not-a-real-one");
		const sent = JSON.parse(init.body as string);
		// output_config, not output_format: the old name is a TypeError now.
		expect(sent.output_config.format.type).toBe("json_schema");
		expect(sent.output_config.format.schema.additionalProperties).toBe(false);
	});

	it("asks for the marks the way that makes them worth having", async () => {
		// The prompt is the only thing standing between a field that identifies
		// one cat and a sixth field that identifies nobody. Nothing else in the
		// system can tell the difference: a mark on every cat is schema-valid,
		// passes every check in validate.ts, and renders perfectly. So the two
		// instructions that prevent it are pinned here rather than left to
		// whoever edits the string next.
		const spy = stubModel({ content: [{ type: "text", text: JSON.stringify(GOOD_TRAITS) }] });

		await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		const [, init] = spy.mock.calls[0] as [string, RequestInit];
		const content = JSON.parse(init.body as string).messages[0].content;
		// Image before text: the documented ordering
		// (knowledge/vision-model/01-traits-strict-json.md).
		expect(content[0].type).toBe("image");
		const prompt = content[1].text as string;

		// Saying nothing must be the expected answer, not a failure to comply.
		expect(prompt).toContain("empty list");
		// The "closest allowed value" licence is right for the coat and wrong
		// for the marks; it must be scoped to the coat fields, and the marks
		// must be told the opposite.
		expect(prompt).toContain("closest allowed value");
		expect(prompt).toMatch(/opposite rule/i);
		// Asymmetry is the whole point: one white paw is worth more than two.
		expect(prompt).toContain("paw_left");
		expect(prompt).toContain("paw_right");
		expect(prompt).toMatch(/one side and not the other/i);
	});

	it("sends a schema the structured-outputs API will actually accept", async () => {
		const spy = stubModel({ content: [{ type: "text", text: JSON.stringify(GOOD_TRAITS) }] });

		await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		const [, init] = spy.mock.calls[0] as [string, RequestInit];
		const schema = JSON.parse(init.body as string).output_config.format.schema;

		// The marks have to be in the schema the model is handed, or the field
		// can only ever come back empty and every test below is theatre.
		expect(schema.properties.spots).toBeDefined();
		expect(schema.required).toContain("spots");
		expect(schema.properties.spots.items.additionalProperties).toBe(false);

		// And `maxItems` must not be: it is not a supported structured-outputs
		// keyword, and an unsupported keyword is not ignored — the whole request
		// comes back 400. The cap lives in validate.ts instead. Checked over the
		// serialised schema rather than one property, because the next nested
		// array somebody adds will be capped in schema.json by reflex.
		expect(JSON.stringify(schema)).not.toContain("maxItems");
	});

	it("turns a model failure into 502 without passing its text through", async () => {
		stubModel({ error: { message: "account 12345 has insufficient credit" } }, 400);

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(502);
		expect(JSON.stringify(await response.json())).not.toContain("12345");
	});

	it("turns an unparseable model answer into 502 rather than passing it on", async () => {
		stubModel({ content: [{ type: "text", text: "here you go: a ginger cat" }] });

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(502);
	});
});

// 50-photo/03: the schema and validate.py's rules are asked of the model,
// not guaranteed by it — a model can return syntactically valid JSON that
// still violates the enum, additionalProperties, or the cap maxItems cannot
// express. Before src/validate.ts existed, index.ts parsed the model's JSON
// and returned it as-is: every case below would have reached the game as a
// 200, contradicting this task's own OUTCOME line ("never an out-of-enum
// value"). Kept as its own describe block because it is a distinct claim
// from "the request sent to the model looks right" above.
describe("what the endpoint refuses even when the model's JSON parses", () => {
	it("rejects an out-of-enum value with 502, not the value itself", async () => {
		stubModel({
			content: [{ type: "text", text: JSON.stringify({ ...GOOD_TRAITS, base_color: "orange" }) }],
		});

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(502);
		expect(JSON.stringify(await response.json())).not.toContain("orange");
	});

	it("rejects a field the schema does not declare", async () => {
		stubModel({
			content: [{ type: "text", text: JSON.stringify({ ...GOOD_TRAITS, breed: "maine coon" }) }],
		});

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(502);
	});

	it("rejects white_markings past the cap — maxItems is not a supported keyword, so this is the check", async () => {
		stubModel({
			content: [{
				type: "text",
				text: JSON.stringify({ ...GOOD_TRAITS, white_markings: ["chest", "paws", "face", "chest"] }),
			}],
		});

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(502);
	});

	it("rejects repeated white_markings even under the cap", async () => {
		stubModel({
			content: [{ type: "text", text: JSON.stringify({ ...GOOD_TRAITS, white_markings: ["chest", "chest"] }) }],
		});

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(502);
	});

	it("accepts every white_marking in the enum at once — the cap is inclusive, not one short", async () => {
		stubModel({
			content: [{ type: "text", text: JSON.stringify({ ...GOOD_TRAITS, white_markings: ["chest", "paws", "face"] }) }],
		});

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(200);
	});

	it("accepts an empty white_markings — a cat with no white on it is not an error", async () => {
		stubModel({
			content: [{ type: "text", text: JSON.stringify({ ...GOOD_TRAITS, white_markings: [] }) }],
		});

		const response = await worker.fetch(
			post({ image_base64: ONE_PIXEL_JPEG_BASE64, device_id: "d1" }),
			env(),
		);

		expect(response.status).toBe(200);
	});
});

// The marks are the only field in the schema that can identify one cat rather
// than narrow a pool, and they buy that by being empty most of the time. Every
// rejection below is a way the field could quietly stop meaning anything:
// three marks, the same mark twice, half a mark, a mark somewhere no cat has.
// A model that returns any of them has not looked at the photograph, and a 502
// the game falls back from is better than a stranger's cat drawn confidently.
describe("the marks, which are the only thing here that identifies anybody", () => {
	it("accepts a cat with no marks — that is the ordinary answer, not a failure", async () => {
		const response = await answering(withSpots([]));
		expect(response.status).toBe(200);
		expect((await response.json() as { spots: unknown[] }).spots).toEqual([]);
	});

	it("passes one mark through untouched", async () => {
		const spots = [{ place: "paw_left", shade: "light" }];
		const response = await answering(withSpots(spots));

		expect(response.status).toBe(200);
		// Untouched, not merely accepted: this is the one field the player is
		// expected to recognise, so a Worker that helpfully normalised it would
		// be handing her a different cat.
		expect((await response.json() as { spots: unknown[] }).spots).toEqual(spots);
	});

	it("accepts two marks in different places, which is the cap", async () => {
		const spots = [
			{ place: "eye_right", shade: "dark" },
			{ place: "tail_tip", shade: "light" },
		];
		const response = await answering(withSpots(spots));

		expect(response.status).toBe(200);
		expect((await response.json() as { spots: unknown[] }).spots).toEqual(spots);
	});

	it("accepts every place in the enum, one at a time", async () => {
		// A place that validates in the game but not here, or the reverse, is
		// the exact drift this endpoint exists to prevent.
		for (const place of [
			"muzzle", "forehead", "eye_left", "eye_right", "chin",
			"chest", "paw_left", "paw_right", "flank", "tail_tip",
		]) {
			const response = await answering(withSpots([{ place, shade: "dark" }]));
			expect(response.status, place).toBe(200);
		}
	});

	it("rejects more marks than the cap — maxItems cannot say so in the schema", async () => {
		const response = await answering(withSpots([
			{ place: "muzzle", shade: "light" },
			{ place: "chin", shade: "dark" },
			{ place: "flank", shade: "light" },
		]));
		expect(response.status).toBe(502);
	});

	it("rejects two marks in the same place even when the shades differ", async () => {
		// One mark described twice, and drawn twice — one patch over the other.
		// Shade is deliberately not part of what makes them different marks.
		const response = await answering(withSpots([
			{ place: "chin", shade: "light" },
			{ place: "chin", shade: "dark" },
		]));
		expect(response.status).toBe(502);
	});

	it("rejects a place no cat has, without echoing it back", async () => {
		const response = await answering(withSpots([{ place: "whiskers", shade: "dark" }]));

		expect(response.status).toBe(502);
		// Same rule as the coat fields: the offending value stays server-side.
		expect(JSON.stringify(await response.json())).not.toContain("whiskers");
	});

	it("rejects a shade outside the two allowed", async () => {
		// "ginger" is a real value — in a different enum. Reject, never repair.
		const response = await answering(withSpots([{ place: "chest", shade: "ginger" }]));
		expect(response.status).toBe(502);
	});

	it("rejects half a mark: a place with no shade", async () => {
		const response = await answering(withSpots([{ place: "forehead" }]));
		expect(response.status).toBe(502);
	});

	it("rejects half a mark: a shade with nowhere to be", async () => {
		const response = await answering(withSpots([{ shade: "dark" }]));
		expect(response.status).toBe(502);
	});

	it("rejects a member the schema does not declare", async () => {
		// additionalProperties: false is on the nested object too, and a "size"
		// the model volunteered is a field nothing draws.
		const response = await answering(withSpots([
			{ place: "flank", shade: "dark", size: "large" },
		]));
		expect(response.status).toBe(502);
	});

	it.each([
		["a bare string", ["dark spot on the chin"]],
		["null", [null]],
		["a nested list", [["chin", "dark"]]],
	])("rejects a mark that is not an object: %s", async (_name, spots) => {
		const response = await answering(withSpots(spots));
		expect(response.status).toBe(502);
	});

	it.each([
		["a string", "one white paw"],
		["an object", { place: "chin", shade: "dark" }],
		["null", null],
	])("rejects spots that is not a list: %s", async (_name, spots) => {
		const response = await answering(withSpots(spots));
		expect(response.status).toBe(502);
	});

	it("rejects a response that omits spots altogether", async () => {
		const { spots, ...withoutSpots } = GOOD_TRAITS;
		void spots;
		const response = await answering(withoutSpots);
		expect(response.status).toBe(502);
	});
});

// The cap is a judgement — two, not ten — and it is written down in two
// languages that never see each other. Nothing else would notice them drifting:
// the game would throw on a third mark the Worker had already blessed, on the
// player's device, after the photograph she was asked for.
describe("the cap on marks, which lives in two languages", () => {
	it("matches CatTraits.MaxSpots in the game", async () => {
		const { readFileSync } = await import("node:fs");
		const source = readFileSync(
			pathTo("../../game/Assets/Core/CatTraits.cs"), "utf8");

		const declared = source.match(/public const int MaxSpots = (\d+);/);
		expect(declared, "CatTraits.MaxSpots is not declared where this test looks")
			.not.toBeNull();
		expect(Number(declared![1])).toBe(MAX_SPOTS);
	});
});

// The tests above stub the limiter, so they prove the handler honours whatever
// verdict it is given and passes the right key. They cannot prove the binding
// is configured to the numbers the task asked for — that lives in
// wrangler.jsonc and nothing checked it. A limit of 600 an hour would pass
// every test on this page.
describe("the limiter's configuration, which no stub can check", () => {
	it("matches what 50-photo/04-rate-limit asked for", async () => {
		const { readFileSync } = await import("node:fs");
		const source = readFileSync(pathTo("../wrangler.jsonc"), "utf8");
		// jsonc: strip // comments before parsing. Block comments are not used
		// in this file and are deliberately not handled, so one appearing would
		// break this loudly rather than be silently mis-parsed.
		const config = JSON.parse(source.replace(/^\s*\/\/.*$/gm, ""));

		const limiter = config.ratelimits?.find(
			(r: { name: string }) => r.name === "TRAITS_LIMITER");
		expect(limiter, "TRAITS_LIMITER is not declared in wrangler.jsonc").toBeDefined();

		// Six in sixty seconds: a player photographs her cat once, so six is
		// already generous, and the binding accepts no period but 10 or 60.
		expect(limiter.simple.limit).toBe(6);
		expect(limiter.simple.period).toBe(60);
	});
});
