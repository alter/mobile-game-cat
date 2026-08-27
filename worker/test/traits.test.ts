/**
 * Task 50-photo/02 VERIFY 1 and 04: the four status codes, plus the limit.
 *
 * Runs against the handler directly with a stubbed model endpoint, so it needs
 * no account, no key and no network — which matters, because the account is
 * exactly what this task is blocked on.
 */
import { describe, expect, it, vi } from "vitest";
import worker, { type Env } from "../src/index";

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

const GOOD_TRAITS = {
	base_color: "ginger",
	pattern: "tabby",
	fur_length: "short",
	eye_color: "green",
	white_markings: ["chest"],
};

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

// The tests above stub the limiter, so they prove the handler honours whatever
// verdict it is given and passes the right key. They cannot prove the binding
// is configured to the numbers the task asked for — that lives in
// wrangler.jsonc and nothing checked it. A limit of 600 an hour would pass
// every test on this page.
describe("the limiter's configuration, which no stub can check", () => {
	it("matches what 50-photo/04-rate-limit asked for", async () => {
		const { readFileSync } = await import("node:fs");
		const source = readFileSync(new URL("../wrangler.jsonc", import.meta.url), "utf8");
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
