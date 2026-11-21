<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# amqp facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows; MCP, Surl.Api and in-flight sections against Surl 9ad60d80c, Curl 7d167fa10a, 2026-11-20.

## Listen URLs and readiness
`amqp://` (AMQP 0-9-1), `amqps://` (TLS, needs `--cert`/`--key`), `amqp1://` and `amqp1s://` (AMQP 1.0); TCP, `ready: port`. One virtual host, `/`.

## Server options that matter
- `--user alice:s3cret`: an account.
- Mechanisms (`surl --aihelp amqp`): `amqp://` offers CRAM-MD5 by default; PLAIN and AMQPLAIN only over TLS or with `--allow-plaintext-auth` (loosens, warns); `--auth` changes the set.
- `--queue <name>[;durable]` (repeatable): a queue that exists from start (Surl.Api `Queues`). Seeding needs it.
- `--allow-anonymous` (loosens, warns): offers PLAIN, AMQPLAIN and ANONYMOUS on every transport, credentials unchecked (new in 763754785).

## Secure by default
- Refused login: `connection.close` 403, curl exit 67, stderr `reply code 403, ACCESS_REFUSED`.
- `amqp://` with an account and no loosening: curl exit 67, `AMQP server offers no mechanism this client can use (it offers "CRAM-MD5")`.
- A virtual host other than `/`: 530, curl exit 8, `reply code 530, NOT_ALLOWED - unknown virtual host`.

## Client: curl
- URL `amqp[s]://host:port/%2F/<queue>`; a path without a virtual host: exit 3, `the URL path must be /<virtual host>/<queue>`.
- Declares the queue (passive, then creates it). `-d x` publishes through the default exchange; no `-d` is `basic.get`, one message to stdout with no newline added; an empty queue writes nothing, exit 0.
- Logs in with PLAIN; does not use CRAM-MD5 or ANONYMOUS. Without `-u` it uses RabbitMQ's default account (403 on a server with accounts). Under `--allow-anonymous`, no `-u` or any `-u` gets in.
- `--next` resets options: repeat `--cacert` and `-u`. Untrusted certificate: exit 60.

- A get (no `-d`) is `basic.get` with no-ack: the bus records `delivered` then `acknowledged` at once, so a curl worker never
  holds a message in flight. With `--user` on `amqp://`, curl needs `--allow-plaintext-auth` (else exit 67 as above).

## In flight (2026-11-20)
A consumer that holds deliveries unacknowledged: STOMP on the same surl via `curl -m 3 -T worker.stomp telnet://h:<stomp port>/`,
a fixture of frames each ended by NUL (`CONNECT` with login/passcode, `SUBSCRIBE` `destination:/queue/jobs`
`ack:client-individual`, `ACK id:1`, `ACK id:2` pipelined in the same file work; ack ids are 1, 2, ... per connection).
A subscriber gets EVERY ready message (no prefetch limit; `prefetch-count` ignored). `UNSUBSCRIBE` or the connection
closing (curl's `-m` -> exit 28) puts unacknowledged ones back as ready, in order. STOMP passcode needs `--allow-plaintext-auth`.

## MCP
- `publish_message(server_id, queue, payload)` -> `{"published":true}` (queue must exist; events `enqueued` + `seeded`).
- `browse_queue(server_id, queue, after_position, limit)` -> `{messages:[{position, text, headers}], ready_count,
  in_flight_count, has_more}`; takes nothing; in-flight messages counted, not listed. Unknown queue: tool error `No queue nope.`
- `get_events(store: "bus", kinds: ["store_changed"])`: `change` enqueued / seeded / delivered / acknowledged, `name`
  `$queue/jobs`, `byte_count`; no event names a requeue. `store` values: content, mail, bus, accounts, keyspace, ...
- curl-mcp `curl` tool runs `telnet://` with `-T` from its root; a call that ends in exit 28 still answers (not awaited first).

## Surl.Api / Curl.Api
- `server.Messages.EnqueueAsync(queue, bytes)`; unknown queue -> `SurlSeedRefusedException` `Store` Bus, `Refusal` UnknownQueue,
  message `Bus: A message for queue job was refused: UnknownQueue.` `BrowseQueueAsync(queue, -1, 100, ct)` -> `SurlQueuePage`
  (`Messages` of `SurlQueueMessage {Position, Payload, Headers}`, `ReadyCount`, `InFlightCount`, `HasMore`). `ResetAsync`
  empties queues and keeps them.
- Curl.Api: `new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}%2F/jobs")` + `CurlAuthenticationOptions` takes one message
  (Body = payload); needs `AllowPlaintextAuthentication`, else `CurlException` 67 with the CRAM-MD5 text.

## Gotchas
- One bus per surl: AMQP 0-9-1 queue `orders` is AMQP 1.0 address `orders`, both ways. curl publishes only to the default exchange, so it cannot reach MQTT or Kafka topics (that takes `amq.topic`).

## Recipes
amqp-send-and-receive, amqp-seed-a-queue-and-browse-what-is-left, amqp-check-what-a-queue-still-holds
