<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# kafka facts
Checked against: surl 1.0.0 (Surl 9ad60d80c), curl (Curl 7d167fa10a), 2026-11-20, windows (SASL re-probed; the rest carried from Surl 763754785 / Curl b9ca725077). Batch-09 sections (Curl.Api publish, AMQP 1.0, durable queues, STOMP worker, keys and the log) against Surl 9ad60d80c, Curl 7d167fa10a, 2026-11-21, windows.

## Listen URLs and readiness
`kafka://`, `kafkas://` (TLS, needs `--cert`/`--key`); TCP, `ready: port`. Every topic has one partition, 0.

## Server options that matter
- `--allow-anonymous` (loosens, warns): any client in, unchecked.
- `--user alice:s3cret`: an account. Mechanisms (`surl --aihelp kafka`, Surl's SCRAM ADR, new in 763754785): `kafka://` enables SCRAM-SHA-256 and SCRAM-SHA-512; `kafkas://` PLAIN as well; PLAIN over `kafka://` needs `--allow-plaintext-auth` (loosens, warns); `--auth` replaces the offer.
- With an `mqtt5://` listener in the same surl, Kafka `shop.orders` is MQTT `shop/orders`. Kafka to MQTT needs `--topic "shop/#;retain"` and `--mqtt5-disconnect-after-retained`, so curl's later subscriber gets the record and ends.

## Secure by default
- No login, no `--allow-anonymous`: connection closed, curl exit 56, `Connection disconnected`.
- `-u` over `kafka://` without `--allow-plaintext-auth`: curl logs in with SCRAM (Curl 7d167fa10a, BL-5600), produce and consume work. Wrong password: exit 67, `Kafka SASL SCRAM-SHA-512 authentication failed: Authentication failed` (512 when both are offered; `--auth SCRAM-SHA-256` gives 256).
- `--auth PLAIN` over `kafka://` without `--allow-plaintext-auth`: surl warns `--auth: accepted methods are plain` and offers nothing; curl exit 67 `SASL is not enabled on the Kafka listener, it offers no SASL mechanism`.
- With `--allow-plaintext-auth` (PLAIN offered too) curl picks PLAIN: wrong password exit 67, `Kafka SASL PLAIN authentication failed`.

## Client: curl
- `kafka://host:port/<topic>[/<partition>][?offset=N]`. `-d v` produces one record; no `-d` consumes from offset 0 (or N) to the end, each value and a newline, then exits 0 (no time limit needed).
- Broker errors exit 8 with `error N (NAME)`: 1 OFFSET_OUT_OF_RANGE (past the end; an offset equal to the count is empty, exit 0), 3 UNKNOWN_TOPIC_OR_PARTITION (partition 1), 17 INVALID_TOPIC_EXCEPTION (`a%20b`). No topic: exit 3, `No Kafka topic found`.
- SASL PLAIN, SCRAM-SHA-256, SCRAM-SHA-512; prefers PLAIN when offered, else SCRAM-SHA-512. `--cacert` for kafkas; untrusted: exit 60.

## Offsets and replay (kafka-replay-a-topic-from-an-offset)
- Offsets are 0-based record counts; `?offset=N` after the topic (and after `/0`). N equal to the count: empty, exit 0; N past it, also on an empty topic: exit 8 `error 1 (OFFSET_OUT_OF_RANGE) for topic orders partition 0`. `?offset=abc` or `-1`: exit 3 `Kafka URL query is offset=<number>`. A `-d` produce ignores the query. The log is in memory: after a surl restart it is empty.

## MCP
surl-mcp `start_server(listen_urls ["kafka://127.0.0.1:0"], arguments ["--allow-anonymous"])`; `publish_message(server_id, topic, payload)` appends a record; `read_log(server_id, topic, from_offset, limit)` answers `{records:[{offset,timestamp,text}], next_offset, has_more}`, and an offset past the end is a tool error `Offset 9 is outside the log, which holds 0 to 5.`. curl-mcp `curl(args ["-sS","-d","d",url+"orders"])` produces; an error exit is a tool error (`isError` true) with `exitCode` 8 or 3.

## C#
- `SurlServerOptions { ListenUrls = { "kafka://127.0.0.1:0" }, AllowAnonymous = true }`; `server.Messages.PublishAsync(topic, bytes)` appends; `ReadLogAsync(topic, fromOffset: n, cancellationToken: ...)` returns `SurlLogPage` (`Records` with `Offset`, `Payload`; `NextOffset`), `SurlReadRefusedException` past the end.
- Curl.Api 0.2.0: `GET kafka://.../orders?offset=3` consumes (lines of values). A `POST`/`PUT` body does NOT produce (status 0, no error, no record): produce with `server.Messages.PublishAsync`. Errors: `CurlException` 8 `WeirdServerReply` (broker error 1), 3 `UrlMalformat` (bad offset).

## Gotchas
- Records stay after a consume: a log, not a queue.

## Recipes
kafka-produce-and-consume, kafka-replay-a-topic-from-an-offset (and twins)

- Libcurl answers `UnsupportedProtocol` for this scheme by design: Libcurl mirrors upstream libcurl's protocol set, and extensions never change Libcurl or `curl -V`. Use Curl.Api or the console (Curl session, 2026-11-20).

## Logging in over TLS (kafka-log-in-over-tls and twins; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-21, windows)
- curl cannot be made to insist on PLAIN (`--login-options AUTH=PLAIN`, `--sasl-ir` change nothing): it takes PLAIN where offered (kafkas://, or kafka:// with `--allow-plaintext-auth`), else SCRAM-SHA-512. The mechanism is named in the refusal: `Kafka SASL PLAIN authentication failed`, `Kafka SASL SCRAM-SHA-512 authentication failed` (kafka:// default), `SCRAM-SHA-256` with `--auth SCRAM-SHA-256`; Curl.Api messages end `: Authentication failed`. `--auth PLAIN` over kafka://: exit 67 `SASL is not enabled on the Kafka listener, it offers no SASL mechanism`.
- kafkas:// no login: exit 56 (Curl.Api `RecvError` `Connection disconnected`); untrusted certificate: 60 (message is the TLS library's: print only the code).
- Surl.Api: `AuthenticationMethods = ["SCRAM-SHA-256"]` (`IList<string>`, `--auth`), `AllowPlaintextAuthentication`, `CertificateFile`/`KeyFile`; Curl.Api `Authentication` on a GET consumes after login. surl-mcp: `--user`, `--cert`, `--auth X` in `arguments`; curl-mcp `curl -sS -u alice:s3cret -d first kafkas://.../orders`; `read_log` then answers the record. RFC 4616 is not in the bibliography.

## Keys and the log (batch-09, `kafka-read-a-topic-log-after-a-test`, `kafka-check-a-consumer-saw-every-record`)
- No producer here sets a record key: `curl -d` has no key option (`?key=` is refused, exit 3 `Kafka URL query is offset=<number>`),
  and `PublishAsync` / `publish_message` take none. Every `SurlLogRecord.Key` has no value. A recipe that claims keys says so.
- `ReadLogAsync(topic, fromOffset, limit 1..1000, ct)`: a `SurlLogPage` (`Records` with `Offset`, `Timestamp`, `Key`, `Payload`;
  `NextOffset`; `HasMore`). Past the end: `SurlReadRefusedException` `Offset 9 is outside the log, which holds 0 to 4.` A topic nobody
  wrote to: `SurlNotFoundException` `No log for topic refunds.` Reads take nothing; Curl.Api `GET ...orders?offset=2` afterwards
  still sees the records.
- MCP `read_log(server_id, topic, from_offset, limit)`: `{records:[{offset, timestamp, text}], next_offset, has_more}`; a refused read
  is a tool error with the same text. A Kafka produce (curl `-d` or `publish_message`) is acknowledged before it returns, so the log
  holds the record at once and no wait is needed.

