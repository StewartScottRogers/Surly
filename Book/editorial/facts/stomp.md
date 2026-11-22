<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# stomp facts
Checked against: surl 1.0.0 (Surl 5da5b3eaf), curl (Curl c84035f797), 2026-11-22, windows. Sections from other batches are in amqp.md (STOMP worker, in-flight) and `stomp-send-to-a-queue`.

## Listen URLs and readiness
`stomp://` (default 61613) and `stomps://` (TLS from the first byte, needs a certificate); TCP, `ready: port`. STOMP 1.2. One message bus with MQTT, AMQP, NATS and Kafka: `/topic/a/b` is the MQTT topic `a/b`; `/queue/a` is the queue `a` (AMQP address `a`).

## Server options that matter
- `--allow-anonymous` (loosens, warns), `--user`, `--allow-plaintext-auth` (a STOMP password over `stomp://` is refused unchecked without it: ERROR `Authentication failed`, curl 67).
- `--idle-timeout <s>` (default 120): ends an idle STOMP subscription, so a `curl stomp://.../queue/x` or `/topic/x` subscribe exits 0 after that long when nothing arrives; with the default it needs `-m` (exit 28).
- `--topic "devices/#;retain"`: keeps every message sent to a matching topic, STOMP included (a STOMP SEND has no retain flag).

## Client: curl
`curl -d body stomp://h:p/topic/x` SENDs; no `-d` subscribes and writes each MESSAGE body with nothing between. A destination that starts with neither `/queue/` nor `/topic/` is an ERROR (exit 8); no destination is exit 3. Curl.Api: `HttpMethod.Post` with `Body = CurlContent.FromBytes(...)` sends (unlike amqp://, it publishes); `HttpMethod.Get` subscribes. `--next` chains a send and an MQTT read in one `run:`.

## STOMP to MQTT (stomp-send-and-read-over-mqtt and twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `/topic/devices/lamp/state` sent over STOMP, with `--topic "devices/#;retain"` on a server that also listens on `mqtt://`: an MQTT subscribe to `devices/lamp/state` is sent it (22 bytes: 2 length, 18 topic, `on`). A second send replaces it; an empty body (`-d ""`) clears it; `devices/hall/%23` returns every topic under it (48 bytes for `temp`=21.5 and `humidity`=55).
- Outside every filter, or with no `--topic`: the send is accepted (exit 0) and nothing is kept. `/queue/devices/jobs` is a queue: no MQTT topic carries it.
- A STOMP subscriber to the topic is NOT sent the retained message (an MQTT one is): ends by the idle timeout, exit 0, empty. A retained message published over MQTT is not replayed to a STOMP subscriber either.
- Two servers cannot share `--directory`; here none is needed.
- A STOMP queue subscribe that hung past 120 s once in a probe with `--idle-timeout 1` straight after a `curl -d ""` send to a topic on the same server; not reproduced. Give every STOMP subscribe `-m`.
- Waiting: console cases are sequential (`--next` sends then reads: stable in 3 runs). Surl.Api: `RecordExchanges = true`, then count `ExchangeEnded` against `ExchangeStarted` (bounded loop) after each call; agent: `wait_for_event(kinds:["exchange_ended"], after_sequence)`; `read_retained_message(server_id, topic)` -> `{topic, qos, text}` reads the kept message without a client.
- Surl.Api: `ListenUrls = { "stomp://127.0.0.1:0", "mqtt://127.0.0.1:0" }`, `BoundUrls[0|1]`, `Topics.Add("devices/#;retain")`, `AdditionalArguments = { "--idle-timeout", "1" }`, `AllowAnonymous`. curl-mcp `curl`: the MQTT read's `standardOutput` holds the NUL-led bytes (`isBase64` may be true).

## Recipes
stomp-send-to-a-queue, stomp-send-and-read-over-mqtt (and C# and agent twins).

- Decided by Surl (2026-11-22): retained messages are MQTT's alone to read (ADR-0100 d.3.3, ADR-0118): STOMP, AMQP, NATS and Redis subscribers never get one. A STOMP subscribe with no time limit waits for ever on an empty queue, or on a message held by another live ack:client subscription; bound every subscribe.

## Logging in over TLS (stomps-log-in-over-tls and twins, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `stomps://` with `--cert --key --user alice:s3cret`; `curl --cacert server.pem -u alice:s3cret -d hello stomps://h:p/queue/jobs --next -s -m 3 --cacert server.pem -u alice:s3cret stomps://h:p/queue/jobs` writes `hello`, exit 28. Wrong password, unknown user, no login, right password over plain `stomp://`: exit 67 `STOMP server sent ERROR: Authentication failed` (same for all). With `--allow-plaintext-auth` plain sends (exit 0). No `--cacert`: 60. No `--ssl-no-revoke` needed. Client-first protocol: the BL-5750 TLS-before-greeting fault is not expected on Linux; not run there.
- Surl.Api: `Queues.Add("jobs")`, `RecordExchanges = true`; Curl.Api POST `queue/jobs`; wait `ExchangeEnded` then `server.Messages.BrowseQueueAsync("jobs", -1, 100, ct)` (`ReadyCount`, `Messages[].Payload`). Refusal `CurlException` 67, message ends `Authentication failed`. surl-mcp: `--queue jobs` in `arguments`, `wait_for_event(kinds:["exchange_ended"], timeout_ms)`, `browse_queue(server_id, queue)` -> `ready_count`, `messages[].text`.
