# Rate limiting and request signing for the proxy/worker

Date of information gathering: 2026-08-24.

Verified version numbers (via PyPI/GitHub, accessed 2026-08-24):

| Package | Version | Release date | Source |
|---|---|---|---|
| slowapi | 0.1.10 | 2026-06-13 | [pypi.org/project/slowapi](https://pypi.org/project/slowapi/) |
| fastapi-limiter | v0.2.0 (release year not confirmed by direct access) | «06 Feb» | [github.com/long2ice/fastapi-limiter/releases](https://github.com/long2ice/fastapi-limiter/releases) |

## Summary

- **slowapi** is actively maintained as of the collection date: the latest release is 0.1.10 from June 13, 2026, described as "a rate limiting library for Starlette and FastAPI adapted from flask-limiter" — [pypi.org/project/slowapi](https://pypi.org/project/slowapi/).
- **fastapi-limiter** (long2ice) on GitHub shows its latest release tagged v0.2.0; the exact release year could not be confirmed through public pages (the page gives a date without a year) — this should be treated as a possible signal of low maintenance activity and checked independently before choosing it for the project.
- For "10 calls per day per device", a sliding window or a token bucket on Redis with a key based on the device identifier — not the IP address — fits; tying to IP doesn't work for a mobile client (network changes, carrier NAT).
- HMAC-SHA256 — the standard `hmac` Python library: `hmac.new(key, msg, digestmod)` to compute it, and `hmac.compare_digest(a, b)` for comparison — a plain `==` comparison is vulnerable to timing attacks.
- What needs signing is not just the request body, but the body + timestamp + device identifier together — this simultaneously protects against replay if the timestamp is checked for staleness, and the nonce protects against replay within the allowed time window.
- In C# the same scheme is implemented via `System.Security.Cryptography.HMACSHA256` — the principle is identical, only the call API differs.
- A shared secret baked into the client app can be extracted from the build given sufficient effort — this is not protection against a determined attacker, but a barrier against mass automated abuse and casual copying.
- Apple provides a separate, much stronger mechanism — App Attest (part of DeviceCheck) — to confirm that a request comes from a genuine instance of the app on a genuine Apple device, without a shared secret in the binary.
- The log should record the facts of the call (device identifier, timestamp, signature and limit check result), but not the secret itself, not the signature as a reusable disclosing value, and not the full payload if it may contain sensitive data.

## Rate limiting in FastAPI

**slowapi** — a wrapper around the `limits` library, an adaptation of `flask-limiter` for Starlette/FastAPI; supports redis, memcached and in-memory backends (memory as a fallback), limit decorators on individual handlers and shared limits on a group of routes, and works with both sync and async handlers — "a rate limiting library for Starlette and FastAPI adapted from flask-limiter"; latest release 0.1.10 from June 13, 2026 — [pypi.org/project/slowapi](https://pypi.org/project/slowapi/). The official setup example — [slowapi.readthedocs.io](https://slowapi.readthedocs.io/en/latest/):

```python
from fastapi import FastAPI
from slowapi import Limiter, _rate_limit_exceeded_handler
from slowapi.util import get_remote_address
from slowapi.errors import RateLimitExceeded

limiter = Limiter(key_func=get_remote_address)
app = FastAPI()
app.state.limiter = limiter
app.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)

@app.get("/home")
@limiter.limit("5/minute")
async def homepage(request: Request):
    return PlainTextResponse("test")
```

An important restriction from the documentation: the `request` parameter must be explicitly passed to the handler — "the request argument must be explicitly passed to your endpoint, or slowapi won't be able to hook into it", otherwise slowapi cannot hook into the request; WebSocket handlers are not yet supported — [pypi.org/project/slowapi](https://pypi.org/project/slowapi/) (package description). The `key_func` parameter in the example is `get_remote_address`, meaning the default limit is tied to the IP address; for a "per device" limit, `get_remote_address` needs to be replaced with a custom function that reads the device identifier from a header or the signed request body, for example:

```python
def device_id_key(request: Request) -> str:
    return request.headers.get("X-Device-Id", "unknown")

limiter = Limiter(key_func=device_id_key)
```

This is not a verbatim example from the slowapi documentation (a ready-made example with a non-IP key could not be found on the public documentation pages — "no verbatim example found in the primary source"), but code composed from the documented `key_func` parameter, consistent with its purpose.

**fastapi-limiter** (long2ice) — "A request rate limiter for fastapi... powered by pyrate-limiter"; provides a `RateLimiter` dependency, and also `RateLimiterMiddleware` for a limit on all routes at once without adding a dependency to each one — [github.com/long2ice/fastapi-limiter](https://github.com/long2ice/fastapi-limiter). The default identifier is "ip + path", but the documentation explicitly says it can be overridden, for example to `userid`: "Identifier of route limit, default is `ip + path`, you can override it such as `userid` and so on" — same source. The latest release on the releases page is tagged v0.2.0 with the change description "use lifespan" — [github.com/long2ice/fastapi-limiter/releases](https://github.com/long2ice/fastapi-limiter/releases); the date is shown without a year ("06 Feb"), so the exact release year was not confirmed within this collection — this should be re-checked before using the package in the project rather than assumed to be unambiguously fresh.

Neither library was directly compared to the other for maintenance activity in the sources reviewed; based on what could be opened, slowapi shows a more recent and clearly dated release history (the year 2026 is explicitly visible), while this is not confirmed directly for fastapi-limiter — for this reason slowapi is preferable for a new project, unless more recent data on fastapi-limiter appears.

## Algorithms: sliding window and token bucket

Based on the Redis rate-limiting glossary material — [redis.io/glossary/rate-limiting](https://redis.io/glossary/rate-limiting/):

The sliding window tracks the number of requests over a recent time span through a window that continuously moves: "this algorithm tracks the number of requests received in the recent past using a sliding window that moves over time"; it is more flexible than a fixed window and adapts better to traffic bursts, but is less effective against a sustained prolonged attack — same source.

The token bucket maintains a "bucket" that is refilled with tokens at a fixed rate; each request spends one token, and once tokens run out — requests are denied: "this maintains a token bucket that is refilled at a fixed rate. Each request consumes a token, and additional requests are denied once the bucket is empty"; it handles bursts well (tokens can accumulate and be spent at once), but is likewise not designed for sustained prolonged load — same source.

The same source also describes a practical way to implement a simple limiter on Redis via `INCR`+`EXPIRE` inside `MULTI`/`EXEC` (an atomic transaction): the key is built as "client identifier + current minute number", on the first call within the minute `INCR` returns 1, the key expires after the window duration — [redis.io/glossary/rate-limiting](https://redis.io/glossary/rate-limiting/):

```
MULTI
  INCR [user-api-key]:[current minute number]
  EXPIRE [user-api-key]:[current minute number] 59
EXEC
```

For the requirement "10 calls per day per device" this is essentially a special case of a sliding or fixed window with a very large period (a day) and a low limit. A token bucket is excessive here: its strength is smoothing short bursts under high frequency (tens/hundreds of requests per second), while at a limit of "10 per day" the requests themselves are so few that the difference between algorithms is not noticeable in practice, and the simpler, more predictable option is a fixed or sliding window over a day with a Redis key of the form `device:{device_id}:{date}` (analogous to the scheme shown above, but with a day-long window instead of a minute) and a counter via `INCR`+`EXPIRE`. An exact comparison specifically for the "10 requests per day" case is not addressed separately in any source found — this conclusion is drawn from the general described properties of the algorithms, not quoted directly.

## Signing a request with a shared secret: HMAC-SHA256

The standard Python `hmac` library: `hmac.new(key, msg=None, digestmod)` returns a new HMAC object; the `key` parameter is bytes or `bytearray`; starting with version 3.8 the `digestmod` parameter is mandatory — [docs.python.org/3/library/hmac.html](https://docs.python.org/3/library/hmac.html).

To compare the computed value with the one sent by the client, the documentation explicitly recommends not the `==` operator but `hmac.compare_digest`, which is resistant to timing attacks (it doesn't stop the comparison at the first mismatching byte): "When comparing the output of digest() or hexdigest() to an externally supplied digest during a verification routine, it is recommended to use the compare_digest() function instead of the == operator to reduce the vulnerability to timing attacks" — [docs.python.org/3/library/hmac.html](https://docs.python.org/3/library/hmac.html).

What to sign: the request body alone is not enough — without a timestamp the signature remains valid forever (an intercepted request can be resent), without a device identifier the limit and signature cannot be tied to a specific source. A practical Python example (composed from the documented `hmac.new`/`hmac.compare_digest` API, not quoted as a single ready-made example from one source — no such end-to-end example with body+timestamp+device identifier could be found on public pages):

```python
import hmac
import hashlib
import time

SHARED_SECRET = b"..."  # only from an environment variable, see file 01

def sign_request(body: bytes, device_id: str, timestamp: str, nonce: str) -> str:
    message = body + b"|" + device_id.encode() + b"|" + timestamp.encode() + b"|" + nonce.encode()
    digest = hmac.new(SHARED_SECRET, message, hashlib.sha256)
    return digest.hexdigest()

def verify_request(body: bytes, device_id: str, timestamp: str, nonce: str, signature: str) -> bool:
    expected = sign_request(body, device_id, timestamp, nonce)
    return hmac.compare_digest(expected, signature)
```

Replay protection is made up of two independent checks on the server:

Timestamp — the server rejects the request if the timestamp is too old or too far off from the server's current time (for example, more than a few minutes in either direction); this limits the window during which an intercepted request can be replayed at all.

Nonce (a one-time value) — the server remembers already-seen pairs (device identifier, nonce) within the allowed timestamp window and rejects a repeat of the same pair; this closes off the very possibility of replay within the allowed time window, which the timestamp alone does not eliminate. No separate official source specifically addressing this "timestamp + nonce" pair for this exact task was opened during this collection — the scheme described is a general anti-replay practice, not a quote from one document.

On the C# side, the same scheme is computed via `System.Security.Cryptography.HMACSHA256`: the constructor takes the key as a byte array, the `ComputeHash` method computes the HMAC of a byte array or a stream — [learn.microsoft.com/…/HMACSHA256](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256?view=net-10.0). The official Microsoft example shows signing and verifying an entire file via `FileStream`:

```csharp
using (HMACSHA256 hmac = new HMACSHA256(key))
{
    using (FileStream inStream = new FileStream(sourceFile, FileMode.Open))
    {
        using (FileStream outStream = new FileStream(destFile, FileMode.Create))
        {
            byte[] hashValue = hmac.ComputeHash(inStream);
            inStream.Position = 0;
            outStream.Write(hashValue, 0, hashValue.Length);
        }
    }
}
```

The same source explains the general principle: "An HMAC can be used to determine whether a message sent over an insecure channel has been tampered with, provided that the sender and receiver share a secret key. The sender computes the hash value for the original data and sends both the original data and hash value as a single message. The receiver recalculates the hash value on the received message and checks that the computed HMAC matches the transmitted HMAC" — [learn.microsoft.com/…/HMACSHA256](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256?view=net-10.0). For a string (rather than a file) — on the game client side, where the request body + timestamp + device identifier are signed, not a file — the construction is analogous: the string needs to be converted to bytes (`Encoding.UTF8.GetBytes`, the same way as on the Python server side — `str.encode()`), passed to `ComputeHash`, and the resulting bytes represented in the same format (for example, a lowercase hex string) as on the server, otherwise the comparison won't match because of different representation, not because of a different key or byte order.

The key condition for compatibility between Python and C#: the concatenation order of the message fields (body, delimiter, device identifier, timestamp, nonce), the text encoding (UTF-8 on both sides), and the signature output format (a hex string, usually lowercase) must be defined once as a protocol and implemented identically on both sides — the HMAC library itself does not guarantee or check this.

## An honest warning: a shared secret in the app can be extracted from the build

A shared secret baked into the code or resources of a mobile app (including obfuscated) is, in principle, extractable: anyone who obtains the installer can, statically or dynamically (with a debugger, by intercepting memory at runtime), retrieve the key bytes, after which they can form their own correctly signed requests without limit. This is not a made-up claim with a specific hacking method attached — no direct source with a step-by-step guide was deliberately sought or is cited within this collection — but the fact itself that a secret is extractable from a client binary is a well-known property of the "secret on the device" model, not a feature of any particular library.

What an HMAC signature with a shared secret actually gives: a barrier against accidental or mass automated abuse (bots that haven't specifically reverse-engineered this app), integrity protection for the request in transit (the body can't be swapped in flight without knowing the secret), and binding of a specific request to a specific device and moment in time in combination with the checks from the previous section. What it does not give: protection from a determined attacker who has decompiled this specific app and extracted the secret — against such an attacker, HMAC with a shared secret is equivalent to no protection at all, since they can sign requests indistinguishably from a genuine client.

Apple provides a separate, fundamentally stronger mechanism specifically for this case — **App Attest**, part of the DeviceCheck platform. The official page — [developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity](https://developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity); during this collection WebFetch was only able to retrieve the page title ("Establishing your app's integrity | Apple Developer Documentation") — the page itself is built on JavaScript and does not deliver text content to a plain HTML fetcher, so the details below are not quoted verbatim from this page but taken from open searches of related material (including the official Apple pages "Preparing to use the app attest service" and WWDC discussions), and should be considered less strictly confirmed than the other facts in these three files — "the primary source could not be opened verbatim, see the links below for the facts."

Based on gathered (but not verbatim-quoted) information: the private key is created inside the device's Secure Enclave and never leaves it — it cannot be read, exported, or copied; the `DCAppAttestService` service creates a key pair on the device, the app sends an attestation request to Apple's servers, which return an attestation object including a certificate chain proving the key was created on genuine Apple hardware. Verification of this object must be performed on the developer's server, not in the app itself — "attestation should always be validated by your server, and not the app." There is no ready-made Apple API for this verification itself — the developer must implement CBOR format parsing and X.509 certificate chain verification independently. Links to the official Apple pages on the topic (titles confirmed via WebFetch, full content not confirmed):

- [developer.apple.com/documentation/devicecheck](https://developer.apple.com/documentation/devicecheck) — general DeviceCheck page
- [developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity](https://developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity) — establishing app authenticity
- [developer.apple.com/documentation/devicecheck/validating-apps-that-connect-to-your-server](https://developer.apple.com/documentation/devicecheck/validating-apps-that-connect-to-your-server) — server-side verification
- [developer.apple.com/documentation/devicecheck/dcappattestservice](https://developer.apple.com/documentation/devicecheck/dcappattestservice) — the DCAppAttestService class itself

For the proxy/worker, the practical conclusion: HMAC with a shared secret is an acceptable first line of defense (fast, simple, requires no changes on the Apple/Google side), but if protection is needed against a determined attacker with a decompiled app, not just against mass automated traffic, App Attest (for iOS) is worth studying separately and in detail directly from the official Apple pages listed, since within this collection their content is confirmed only at the title level, not the text.

## What to log to notice abuse

Based on the OWASP Logging Cheat Sheet material — what is always worth recording: authentication successes and failures, authorization denials, input validation failures ("input validation failures e.g. protocol violations, unacceptable encodings, invalid parameter names and values"), suspicious attempts to bypass business logic restrictions or exceed allowed action limits, as well as application starts and stops and configuration changes — [cheatsheetseries.owasp.org/…/Logging_Cheat_Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html).

The same source — what must never go into the log directly: authentication passwords, session identifier values (if needed — hashed only), access tokens, encryption keys and other core secrets, payment data, sensitive personal data — "never log data unless it is legally sanctioned"; it also separately stresses the need to sanitize event data before writing it, to prevent log injection via carriage-return/line-feed characters and other delimiters — same source.

Applied to the `/traits` node and abuse protection, this means logging at minimum: the device identifier (not the shared secret itself and not the signature itself as a reusable value), the request timestamp, the HMAC signature check result (passed/failed, without computation details), the rate limit check result (within limit/exceeded, with the current counter), the response code of the cloud model or the fact of an error calling it, and — when a request is rejected — the rejection reason (expired timestamp, nonce reuse, limit exceeded, invalid signature) as separate event categories, not as a single generic "error." The image body itself in base64 does not need to be logged — this is not a security requirement quoted directly from the source, but a direct consequence of the general principle "don't log sensitive/bulky user data," applied to the specific case of a user device's snapshot.

## Sources

- [pypi.org/project/slowapi](https://pypi.org/project/slowapi/) — slowapi version and description
- [slowapi.readthedocs.io/en/latest](https://slowapi.readthedocs.io/en/latest/) — Limiter setup example
- [github.com/long2ice/fastapi-limiter](https://github.com/long2ice/fastapi-limiter) — fastapi-limiter description and identifier parameter
- [github.com/long2ice/fastapi-limiter/releases](https://github.com/long2ice/fastapi-limiter/releases) — fastapi-limiter release history
- [redis.io/glossary/rate-limiting](https://redis.io/glossary/rate-limiting/) — sliding window and token bucket algorithms, INCR/EXPIRE example
- [docs.python.org/3/library/hmac.html](https://docs.python.org/3/library/hmac.html) — hmac.new and hmac.compare_digest
- [learn.microsoft.com/…/HMACSHA256](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256?view=net-10.0) — HMACSHA256 in C#
- [cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html) — what to log and what not to log
- [developer.apple.com/documentation/devicecheck](https://developer.apple.com/documentation/devicecheck) — DeviceCheck (only page title retrieved)
- [developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity](https://developer.apple.com/documentation/devicecheck/establishing-your-app-s-integrity) — App Attest, establishing app authenticity (only page title retrieved)
- [developer.apple.com/documentation/devicecheck/validating-apps-that-connect-to-your-server](https://developer.apple.com/documentation/devicecheck/validating-apps-that-connect-to-your-server) — server-side verification (only page title retrieved)
- [developer.apple.com/documentation/devicecheck/dcappattestservice](https://developer.apple.com/documentation/devicecheck/dcappattestservice) — DCAppAttestService (only page title retrieved)





