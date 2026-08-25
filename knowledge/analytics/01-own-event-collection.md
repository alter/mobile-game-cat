# Own event collection: Unity 6.3 LTS (iOS) → HTTP → Python

Date collected: 2026-08-24

Context: nine events (`app_open`, `photo_screen_shown`, `photo_uploaded`, `photo_rejected`, `level_start`, `level_win`, `level_fail`, `moves_button_tap`, `notification_allowed`), the receiver is a custom Python node, with no third-party SDKs (neither Firebase nor GameAnalytics).

## In brief

- For networking from Unity on iOS, `UnityWebRequest` is used rather than `System.Net.Http.HttpClient` — it is integrated with Unity's loop (coroutines, `async`/`await` via `SendWebRequest()`), requires no manual thread management, and has none of the known IL2CPP/AOT issues on iOS that occasionally surface with `HttpClient` combined with certain versions of `System.Net.Http` on the iOS backend.
- Events should not be sent one at a time: every HTTP request on iOS over a mobile network is a noticeable delay and battery drain. Practitioners batch events (10–50 at a time) and send them in bulk on a timer or once enough have accumulated.
- `OnApplicationPause`/`OnApplicationFocus` are not a guarantee that the last events will be sent: iOS is not obligated to run any code after the app is backgrounded, and background time is limited (roughly 30 seconds under normal conditions, as confirmed by Apple's background tasks documentation). The only reliable way not to lose events is to keep a queue on disk and resend it on the next launch.
- `SystemInfo.deviceUniqueIdentifier` on iOS in Unity is a wrapper around `UIDevice.identifierForVendor` (confirmed by Unity's documentation). The value is shared across all apps from the same developer (vendor) on a device and is reset if the user deletes all of that developer's apps. IDFA is not suitable for stitching sessions together without ATT (App Tracking Transparency) consent — and across 500 test installs, far from every user will grant consent.
- The minimum event schema must include: event name, timestamp in UTC, device identifier, session number, event sequence number (monotonic sequence), and build version. The sequence number is the only cheap way for the receiver to tell that some events were lost or duplicated.
- For 500 installs and nine event types, the data volume is so small that PostgreSQL is excessive complexity at the outset; SQLite or JSON Lines files handle it fully, and migrating to PostgreSQL later, if it grows, is a day's work.
- Idempotency on ingest is implemented as a uniqueness constraint on the pair (device identifier, event sequence number) — so re-sending the same batch does not create duplicates.
- All timestamps must be stored in UTC; the mistake is almost always the same one — using the device's local time without preserving the time zone offset, which causes the "day" recalculation for retention to drift at day boundaries.

## 1. Sending events from Unity: UnityWebRequest vs. HttpClient

Unity 6.3 LTS offers both ways to talk to the network: the built-in `UnityWebRequest` (namespace `UnityEngine.Networking`) and the standard .NET `System.Net.Http.HttpClient`. Unity's documentation describes `UnityWebRequest` as an asynchronous API integrated with coroutines via `SendWebRequest()` — the request does not block the main thread, and its state is checked via `isDone` or awaited via `yield return` [Unity Manual — UnityWebRequest](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UnityWebRequest.html).

The practical reason to choose `UnityWebRequest` for sending analytics on iOS with an IL2CPP build is that it is the engine's own code, tested by Unity across all target platforms alongside each release, whereas `HttpClient` depends on the `System.Net.Http` implementation of whichever .NET backend is in use (Mono/IL2CPP) and has historically been a source of hard-to-track issues specifically on iOS builds (hangs on connection loss, behavior of `HttpClientHandler` under AOT compilation). For a simple JSON payload to your own server, the difference in capabilities does not matter — `UnityWebRequest` handles POST JSON and headers just fine.

```csharp
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

IEnumerator SendBatch(string json, string endpoint)
{
    var bodyRaw = Encoding.UTF8.GetBytes(json);
    using (var request = new UnityWebRequest(endpoint, "POST"))
    {
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            EventQueue.MarkBatchSent();
        }
        else
        {
            // Leave the batch in the on-disk queue — retry on the next tick/launch
            Debug.Log($"Analytics send failed: {request.error}");
        }
    }
}
```

Asynchrony matters in another sense too: sending should not happen in the frame where the game event occurs (e.g., `level_win`) — the call should simply place the event in the queue synchronously and instantly, while network I/O runs on a separate scheduled coroutine. This is standard advice from practitioners who write their own analytics wrappers on top of Unity: put the event into the buffer immediately and never tie game logic to the outcome of a network call.

### What to do with no network: an on-device queue

The only durable model is "write to disk first, send later" (a write-ahead queue). The event is added to local storage (a file or `PlayerPrefs` for small volumes, SQLite via `Mono.Data.Sqlite`/a third-party plugin for larger volumes) before any network attempt. A background process periodically tries to send what has accumulated and removes from the queue only what the server has confirmed (HTTP 200 with a body listing the accepted sequence numbers).

```csharp
// Pseudocode for the queue lifecycle
void OnEvent(string name, Dictionary<string, object> props)
{
    var evt = EventFactory.Build(name, props, sequenceNumber: LocalStore.NextSeq());
    LocalStore.Append(evt);           // synchronous write to disk, does not wait on the network
}

IEnumerator FlushLoop()
{
    while (true)
    {
        yield return new WaitForSeconds(15f);
        var batch = LocalStore.PeekBatch(maxSize: 50);
        if (batch.Count > 0)
            yield return SendBatch(JsonUtility.ToJson(new EventBatch(batch)), Endpoint);
    }
}
```

Retries should use exponential backoff (e.g., 5s → 30s → 2min → 10min, with a ceiling) and must not duplicate batches — the server must be idempotent (section 6 below), because on a bad network the client cannot always reliably tell whether a batch was processed before the connection dropped.

The queue must survive an app restart — that is, it cannot live only in memory. In practice, for 9 event types and low traffic, a file under `Application.persistentDataPath` works fine (JSON Lines, appended line by line, read line by line) — it is simpler than an encoding-safe SQLite database on the client and reliable enough as long as writes are atomic (appending to the end of the file, never rewriting the whole thing).

## 2. Batching events and unreliable delivery on app close

Sending events one at a time is bad practice for two reasons: first, every TCP/TLS handshake on a mobile network costs noticeable time (hundreds of milliseconds to seconds on a poor connection) and battery charge; second, with nine infrequent events per session, individual requests will almost always be tiny and will create disproportionate load on the server relative to the payload's usefulness. Standard practice is to accumulate events in a buffer and send them in a batch, triggered by one of two conditions: enough events have accumulated (typically 10 to 50–100 events for large SDKs) or a timer has elapsed (10–30 seconds up to a few minutes). With nine rare game events, a reasonable target is to send every 15–30 seconds, or immediately if more than ~20 events have accumulated, so the buffer does not grow unbounded during a long session with no network.

### OnApplicationPause/OnApplicationFocus and what happens on close on iOS

Unity's documentation for `OnApplicationPause` describes the callback firing when the application loses/regains focus, but does not document behavior when the operating system forcibly terminates the process — this is directly visible in the text of the manual, where behavior is spelled out for focus-loss and backgrounding scenarios, but not for a process kill [Unity Manual — MonoBehaviour.OnApplicationPause](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnApplicationPause.html). This is not an accidental gap: iOS simply has no guaranteed "the app is about to be killed" callback — moving to the background (`OnApplicationPause(true)`) gives the app a limited amount of time to wrap things up, and the process's further fate (frozen in memory or fully closed) is decided by the system without notifying the app.

Apple's official background tasks documentation describes the following mechanism: on entering the background, the system grants the app a short standard amount of time to finish critical operations (the source states roughly 30 seconds), after which the process is suspended; to get more time to finish sending network data, you must explicitly request it via `beginBackgroundTask`, and even then the system does not guarantee the operation will complete — once the allotted time expires, the expiration handler is called and remaining work must be cut off cleanly [Apple Developer Documentation — Background Tasks](https://developer.apple.com/documentation/backgroundtasks). From a suspended state, iOS can fully terminate the process at any moment without calling any app code — meaning you cannot count on `OnApplicationPause`/`OnApplicationFocus` managing to "top off" the network send before closing, not just "sometimes it doesn't work out."

The practical conclusion reached by everyone who has written their own telemetry for iOS: the event must be on disk (written synchronously at the moment it occurs) well before the question of sending it even arises. `OnApplicationPause(true)` is used only as a trigger to attempt an out-of-cycle flush of the queue (better than nothing, and it usually succeeds during a soft backgrounding via a swipe-to-home), not as the sole delivery mechanism. If the flush does not finish in time, the on-disk queue resends itself on the next app launch, and no events are lost — only delayed.

```csharp
void OnApplicationPause(bool pauseStatus)
{
    if (pauseStatus)
    {
        // Best-effort attempt to finish the queue, not the only line of defense
        StartCoroutine(FlushOnce());
    }
}
```

## 3. A stable device identifier on iOS

Unity's documentation for `SystemInfo.deviceUniqueIdentifier` states directly that on iOS this value comes from `UIDevice.identifierForVendor` [Unity Manual — SystemInfo.deviceUniqueIdentifier](https://docs.unity3d.com/ScriptReference/SystemInfo-deviceUniqueIdentifier.html). This means every constraint on `identifierForVendor` from Apple's documentation carries over unchanged to `SystemInfo.deviceUniqueIdentifier` [Apple Developer Documentation — identifierForVendor](https://developer.apple.com/documentation/uikit/uidevice/identifierforvendor):

- the value is shared across all apps from the same developer (vendor) on a single device — it is not tied to one specific app;
- the value stays stable across launches and app updates, as long as at least one app from that developer remains installed on the device;
- the value **resets** if the user deletes all of that developer's apps from the device and then installs any of them again — the new `identifierForVendor` will differ from the old one;
- obtaining this identifier does not require an ATT (App Tracking Transparency) prompt and is not considered cross-site/cross-app tracking, since it is not shared across different developers.

IDFA (`identifierForAdvertisers`) is not suitable for stitching together a single player's sessions, for two reasons. First, with the introduction of ATT in iOS 14.5, access to IDFA requires the user's explicit consent via a system pop-up dialog ("Allow the app to track your activity"), and without consent the IDFA value is a string of all zeros; across a sample of 500 test installs, you cannot count on everyone granting consent. Second, the very purpose of IDFA is cross-app ad attribution, not identifying a player within a single game, and using it for something else (internal analytics without ATT permission) directly violates Apple's IDFA usage policy.

So for stitching together a single player's sessions within your own telemetry, the right choice is `SystemInfo.deviceUniqueIdentifier` (i.e., `identifierForVendor`) — it does not require ATT, is not considered an advertising identifier, and is stable enough for purposes like "how many sessions has this device had" and "did it come back the next day." The one caveat to keep in mind when calculating retention: if a test user deletes the game and reinstalls it, `identifierForVendor` may change (if no other app from the same developer remains on the device), and such a user will appear as new in the data — this is a standard, well-known limitation of every analytics system on iOS, not just custom ones.

For extra durability, it can make sense to generate your own UUID on first launch and store it in the `Keychain` (it survives app reinstallation, but not a device wipe/reset) — this is common practice, but it adds complexity, and for a 500-install MVP it is probably overkill; `SystemInfo.deviceUniqueIdentifier` as the primary device identifier is a reasonable compromise at this scale.

## 4. Event schema

The minimum set of fields, without which calculating even simple measures (share reaching a screen, day 1 retention) becomes unreliable:

| Field | Purpose |
|---|---|
| `event_name` | one of the nine fixed event names |
| `event_time_utc` | the event's timestamp in UTC, set on the device at the moment it occurs |
| `device_id` | `SystemInfo.deviceUniqueIdentifier` (see section 3) |
| `session_id` | game session identifier (generated on `app_open` after a cold start) |
| `seq` | monotonically increasing event sequence number **on the device** (not reset between sessions) |
| `build_version` | the game build's version (e.g., `CFBundleShortVersionString` plus a build number) |

The sequence number (`seq`) is needed for one simple reason: the receiver gets events in batches, batches can be lost entirely (connection dropped before confirmation), can arrive out of order (a retry of an old batch after a newer one has already been delivered), or can be duplicated (the client did not get confirmation and resent what the server had already accepted). Without an end-to-end sequence number, the server cannot tell "this device has not yet sent event 42" apart from "this device sent events 1–41, and 42 was lost" — it only sees what actually arrived. A monotonic `seq` paired with `device_id` makes it possible to:

- detect gaps: if a device has `seq = 40` and `seq = 43` but is missing `41` and `42`, this is visible with a direct query;
- cheaply discard duplicates (section 6);
- reconstruct event order even if network packets arrived out of sequence.

```json
{
  "event_name": "level_win",
  "event_time_utc": "2026-08-24T14:03:11.482Z",
  "device_id": "3F2A9C11-4B7E-4D2A-9C11-8A2F1B3C4D5E",
  "session_id": "b7e1a9c0-...-2f",
  "seq": 128,
  "build_version": "1.4.2 (37)",
  "props": { "level_id": 12, "moves_used": 18 }
}
```

The batch sent to the server is simply an array of such objects plus a small batch header (not required, but useful for diagnostics):

```json
{
  "device_id": "3F2A9C11-4B7E-4D2A-9C11-8A2F1B3C4D5E",
  "batch_sent_at_utc": "2026-08-24T14:03:26.000Z",
  "events": [ { "...": "..." }, { "...": "..." } ]
}
```

## 5. The receiving side in Python

The batch-ingest handler is deliberately simple as an HTTP endpoint: accept JSON, check basic validity, insert rows while ignoring duplicates (section 6), and respond with the list of accepted `seq` values so the client knows it can remove them from the local queue.

```python
from flask import Flask, request, jsonify
import sqlite3
import datetime

app = Flask(__name__)
DB_PATH = "events.db"

def get_db():
    conn = sqlite3.connect(DB_PATH)
    conn.execute("PRAGMA journal_mode=WAL")  # reduces locking under concurrent inserts
    return conn

@app.post("/v1/events")
def ingest():
    payload = request.get_json(force=True)
    device_id = payload["device_id"]
    events = payload["events"]

    conn = get_db()
    accepted_seq = []
    with conn:
        for e in events:
            cur = conn.execute(
                """
                INSERT INTO events (device_id, seq, event_name, event_time_utc,
                                     session_id, build_version, props, received_at_utc)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(device_id, seq) DO NOTHING
                """,
                (
                    device_id, e["seq"], e["event_name"], e["event_time_utc"],
                    e["session_id"], e["build_version"], str(e.get("props", {})),
                    datetime.datetime.utcnow().isoformat() + "Z",
                ),
            )
            accepted_seq.append(e["seq"])  # even an "already existed" one can be dropped from the client queue

    return jsonify({"accepted_seq": accepted_seq}), 200
```

```sql
CREATE TABLE IF NOT EXISTS events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    device_id TEXT NOT NULL,
    seq INTEGER NOT NULL,
    event_name TEXT NOT NULL,
    event_time_utc TEXT NOT NULL,
    session_id TEXT NOT NULL,
    build_version TEXT,
    props TEXT,
    received_at_utc TEXT NOT NULL,
    UNIQUE(device_id, seq)
);
```

### SQLite vs. JSON Lines vs. PostgreSQL for 500 installs

An honest comparison for the stated scale (500 installs, nine event types, no external readers besides your own SQL queries for four measures):

| | SQLite | JSON Lines files | PostgreSQL |
|---|---|---|---|
| Data volume at 500 installs | trivial (tens–hundreds of thousands of rows, single-digit–tens of MB) — not a problem for any option | | |
| Deployment simplicity | a file on disk, nothing to install | nothing to install, the simplest option | needs a separate process/service, access setup, backups |
| SQL queries for the four measures (retention, shares, etc.) | yes, out of the box | no — needs loading into pandas/DuckDB or writing by hand | yes, out of the box |
| Concurrent writes on batch ingest | limited (one writer at a time even in WAL mode), but at 500 installs this is not a bottleneck | a problem when multiple processes append to one file | scales fine |
| Idempotency via `UNIQUE`/`ON CONFLICT` | yes | must be implemented by hand (deduplication on read) | yes |
| Growth path when scaling | migration to PostgreSQL — export tables, minor SQL edits (the dialect is close) | migration is harder — the schema needs to be designed from scratch | already the endpoint |
| What to pick at 500 installs | **Yes** — the best balance of simplicity and being able to write SQL for analytics right away | Works as a **raw log for debugging/audit** alongside the database, but not as the primary store | Excessive at the outset: an extra service for a volume that one SQLite file handles for free |

Conclusion: for 500 installs it makes sense to use SQLite as the primary store (gives you SQL immediately, no separate service required) and, if you want extra safety, to also write raw JSON lines in parallel to an append-only log file (cheap protection against database corruption — you can rebuild the table from the log). PostgreSQL is worth introducing once there is more than one reader of the data at a time (e.g., a dashboard accessed by several people), once concurrent writes from multiple ingest processes are needed, or once the volume grows by orders of magnitude — none of which is characteristic of validating an MVP with 500 installs.

## 6. Idempotency: discarding duplicates

Re-sending the same batch is a normal occurrence on a bad network (the client did not get confirmation and, to be safe, resends the same thing). The right place to guard against duplicates is a unique key on the pair (`device_id`, `seq`), declared in the database itself, rather than a check at the application code level (code can be forgotten in one of the code paths; a database constraint cannot be bypassed).

```sql
-- SQLite / PostgreSQL: same idea, syntax differs in details
UNIQUE(device_id, seq)
```

On insert, `INSERT ... ON CONFLICT (device_id, seq) DO NOTHING` is used (SQLite and PostgreSQL support this syntax identically) — re-inserting the same event is simply ignored, rather than turning into an error that needs to be caught and handled in the handler's code. The server's response to the client contains `seq` as "accepted" regardless, even if the row already existed — from the client's point of view the outcome is the same either way: the event can be removed from the local queue.

## 7. Calculating the four measures from the event stream

All four measures are essentially a ratio of the number of devices that reached event B to the number of devices that reached event A, within a suitable time window.

### Share reaching the photo screen

The ratio of unique devices with a `photo_screen_shown` event to unique devices with an `app_open` event (usually — within the first session/first day, so as not to confuse "reached it on the first visit" with "reached it a month later").

```sql
WITH first_open AS (
    SELECT device_id, MIN(event_time_utc) AS first_open_at
    FROM events
    WHERE event_name = 'app_open'
    GROUP BY device_id
),
reached_screen AS (
    SELECT DISTINCT device_id
    FROM events
    WHERE event_name = 'photo_screen_shown'
)
SELECT
    COUNT(DISTINCT fo.device_id) AS installs,
    COUNT(DISTINCT rs.device_id) AS reached_photo_screen,
    1.0 * COUNT(DISTINCT rs.device_id) / COUNT(DISTINCT fo.device_id) AS share_reached
FROM first_open fo
LEFT JOIN reached_screen rs ON rs.device_id = fo.device_id;
```

### Share who uploaded a photo

Similar, but relative to those who reached the photo screen (funnel), or relative to all installs (share of the total) — the project needs to explicitly fix which denominator the measure is computed against (this is often mixed up). Below is the "share of all installs" variant, as stated in the task:

```sql
WITH first_open AS (
    SELECT device_id FROM events WHERE event_name = 'app_open' GROUP BY device_id
),
uploaded AS (
    SELECT DISTINCT device_id FROM events WHERE event_name = 'photo_uploaded'
)
SELECT
    1.0 * COUNT(DISTINCT u.device_id) / COUNT(DISTINCT fo.device_id) AS share_uploaded
FROM first_open fo
LEFT JOIN uploaded u ON u.device_id = fo.device_id;
```

### Day 1 retention

The definition of D1 retention accepted across the mobile industry is the "classic" (classic/calendar-day) retention: the share of users who installed the app on calendar day D who opened the app again on calendar day D+1 (exactly the next calendar day, not "at any point within 24–48 hours after install" — that is a separate, "rolling," definition, which yields different, typically higher, numbers). This classic definition is the one used, for example, by the GameAnalytics report: "Retention measures the percentage of players who return to your game after their initial play session, typically tracked on key milestones like Day 1 (D1), Day 7 (D7), and Day 28 (D28)," and the report itself states explicitly that it uses classic (calendar-day) rather than cumulative (rolling) retention [GameAnalytics — 2025 Mobile Gaming Benchmarks](https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks). Practical recommendation: the day is counted in UTC (or in a single chosen time zone for the whole project — it matters not to mix a device's local time with UTC, see section 8), and "returned" means the presence of **any** event (not necessarily `app_open`, but that is most often the one taken as the visit marker) on calendar day D+1 relative to the day of that device's first `app_open`.

```sql
WITH cohort AS (
    SELECT
        device_id,
        DATE(MIN(event_time_utc)) AS install_date
    FROM events
    WHERE event_name = 'app_open'
    GROUP BY device_id
),
day1_return AS (
    SELECT DISTINCT e.device_id
    FROM events e
    JOIN cohort c ON c.device_id = e.device_id
    WHERE e.event_name = 'app_open'
      AND DATE(e.event_time_utc) = DATE(c.install_date, '+1 day')
)
SELECT
    COUNT(DISTINCT c.device_id) AS installs,
    COUNT(DISTINCT d.device_id) AS returned_day1,
    1.0 * COUNT(DISTINCT d.device_id) / COUNT(DISTINCT c.device_id) AS d1_retention
FROM cohort c
LEFT JOIN day1_return d ON d.device_id = c.device_id;
```

(The `DATE(..., '+1 day')` syntax is SQLite's dialect; the PostgreSQL equivalent is `(install_date + INTERVAL '1 day')::date`.)

### Share who tapped "+5 moves"

Computed as the share of unique devices with a `moves_button_tap` event out of the number of devices that reached the corresponding game state (usually — out of everyone who had at least one `level_fail`, since the button is presumably offered at exactly that moment):

```sql
WITH failed AS (
    SELECT DISTINCT device_id FROM events WHERE event_name = 'level_fail'
),
tapped AS (
    SELECT DISTINCT device_id FROM events WHERE event_name = 'moves_button_tap'
)
SELECT
    1.0 * COUNT(DISTINCT t.device_id) / COUNT(DISTINCT f.device_id) AS share_tapped_plus5
FROM failed f
LEFT JOIN tapped t ON t.device_id = f.device_id;
```

## 8. Time zones and timestamps

There is one rule: store `event_time_utc` in UTC, and only in UTC, regardless of the physical time zone the device is in. A common mistake is taking `DateTime.Now` on the device (local time) instead of `DateTime.UtcNow` and writing it into a field that reports then treat as UTC; at day boundaries (local midnight) this shifts the event into the "wrong" calendar day when calculating retention and changes the user's cohort. A second common mistake is mixing up the server's time zone (wherever the Python process physically runs) with the time zone in which the "calendar day" is computed for retention: even with a single server, if users are spread across the world, the "calendar day" must still be fixed in UTC, not in the server's time zone or in each user's individual time zone — otherwise different user cohorts get different actual day windows and comparison across regions becomes invalid. If it becomes necessary in the future to show dates to a user or developer in local time, that should be done only at the display stage (in the report's interface), not in the stored data.

## Sources

- [Unity Manual — UnityWebRequest (6000.0)](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UnityWebRequest.html)
- [Unity Manual — MonoBehaviour.OnApplicationPause](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnApplicationPause.html)
- [Unity Manual — Player Settings (iOS)](https://docs.unity3d.com/Manual/class-PlayerSettingsiOS.html)
- [Unity Manual — SystemInfo.deviceUniqueIdentifier](https://docs.unity3d.com/ScriptReference/SystemInfo-deviceUniqueIdentifier.html)
- [Apple Developer Documentation — identifierForVendor](https://developer.apple.com/documentation/uikit/uidevice/identifierforvendor)
- [Apple Developer Documentation — Background Tasks](https://developer.apple.com/documentation/backgroundtasks)
- [GameAnalytics — 2025 Mobile Gaming Benchmarks (report page)](https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks)
