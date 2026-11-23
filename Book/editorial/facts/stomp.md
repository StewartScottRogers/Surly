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

- Decided by Surl (2026-11-22): a STOMP SEND or SUBSCRIBE to an undeclared queue creates it (ADR-0118, as ADR-0107 for AMQP 1.0), so a producer can meet a later consumer. Seeding with EnqueueAsync still refuses an undeclared queue (BL-1605 open): declare it with --queue first.

- Decided by Surl (2026-11-22, ADR-0124 amendment): seeding an undeclared queue stays refused (UnknownQueue) on Surl.Api, surl-mcp and the control endpoint; STOMP SEND still creates queues (ADR-0118). Declare with --queue before seeding.

## STOMP to a live MQTT 5 subscriber (stomp-join-a-topic-across-two-protocols and twins; Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae, 2026-11-22, windows)
- `surl --allow-anonymous --topic "orders/#;retain" --idle-timeout 1 stomp://.. mqtt://.. mqtt5://..`: a background `curl -s -m 60 mqtt5://h:p/orders/created -o got.txt` receives each STOMP send to `/topic/orders/created` as it happens (payload alone); send with `printf 'x\n' | curl -s --data-binary @- stomp://h:p/topic/...` to keep the line feed. Wait for the subscription with a marker sent over STOMP until it shows in the file (first try usually); a send made a few seconds after starting the watcher, without a marker, was once lost.
- Misses: `/topic/orders.created` (dot) does not reach `orders/created`; `/queue/orders/created` reaches no MQTT topic. Both sends exit 0.
- Curl.Api: GET `mqtt5://` returns its `CurlResponse` once subscribed, and the body stream yields each message as it arrives (read lines on a task; cancel the token to end it); the same `CurlClient` sends STOMP POSTs meanwhile. Surl.Api `Topics = { "orders/#;retain" }` as a collection initializer; `Messages.ReadRetainedAsync` -> `SurlRetainedMessage.Payload`.
- curl-mcp: a `curl` call `-s -m 30 -o got.txt mqtt5://...` started with `.AsTask()` and not awaited stays subscribed while other `curl` calls run (`args ["-s","--data-binary","@-",url]`, `stdin` "ping\n"); read got.txt with `FileShare.ReadWrite` (File.ReadAllText threw on Windows while curl-mcp held it). surl-mcp `read_retained_message` -> `text` "order 1002\n".
