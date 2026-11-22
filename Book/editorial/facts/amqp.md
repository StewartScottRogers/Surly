<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# amqp facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows; MCP, Surl.Api and in-flight sections against Surl 9ad60d80c, Curl 7d167fa10a, 2026-11-20. Batch-09 sections (Curl.Api publish, AMQP 1.0, durable queues, STOMP worker, keys and the log) against Surl 9ad60d80c, Curl 7d167fa10a, 2026-11-21, windows.

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
  (the response `Body`); the server needs `AllowPlaintextAuthentication` (or `AllowAnonymous`), else `CurlException` 67
  `LoginDenied` with the CRAM-MD5 text. `CurlAuthenticationOptions` has no plaintext flag.

## Gotchas
- One bus per surl: AMQP 0-9-1 queue `orders` is AMQP 1.0 address `orders`, both ways. curl publishes only to the default exchange, so it cannot reach MQTT or Kafka topics (that takes `amq.topic`).

## Curl.Api cannot publish (batch-09, Curl 7d167fa10a, probed on amqp:// and amqp1://)
A `CurlRequest` with `Body`, POST or PUT, is NOT a publish: `amqp` and `amqp1` are not in the upload schemes (Curl ADR-1316),
so the handler sees no post data and does a receive (`basic.get`; an AMQP 1.0 one waits). Status 0, no error, nothing queued.
Recipes put messages on the queue with `server.Messages.EnqueueAsync` (queue must exist: `options.Queues.Add`), and say so;
surl-mcp's `publish_message(queue:)` or curl-mcp's `curl -d` publish for real.

## AMQP 1.0 (batch-09)
- `amqp1://host:port/<address>`: no virtual host; the address is the queue name, one bus with 0-9-1. `curl -d` sends, no `-d`
  receives. An empty address does NOT answer: the receive waits for a message (curl `-m N` -> exit 28, curl-mcp a tool error
  with `curl: (28) Operation timed out after ... milliseconds with 0 bytes received`; Curl.Api needs a `CancellationToken`,
  `OperationCanceledException`). An empty 0-9-1 queue answers at once, empty, exit 0.
- Login over `amqp1://` with an account and no loosening: no `-u` -> exit 67 `AMQP 1.0 server does not offer the SASL ANONYMOUS
  mechanism`; with `-u` -> 67 `AMQP 1.0 server does not offer the SASL PLAIN mechanism`. With `--allow-plaintext-auth` a wrong
  password is 67 `AMQP 1.0 SASL authentication failed with outcome code 1`; the 0-9-1 one is `reply code 403, ACCESS_REFUSED -
  login refused`. Curl.Api `CurlException` code 67 `LoginDenied` with the same texts.
- One bus: a message taken over either version is gone for the other; mixed senders keep the order sent.

## Durable queues (batch-09, `amqp-keep-a-queue-across-restarts`)
- `--queue "jobs;durable"` with `--directory data` (folder must exist; `data/readme.txt` as in the mail recipe): messages are kept
  under `data/.surl/bus/queues` (`index`, one file per ready message) beside `data/.surl/lock`. After a stop and a start of the same
  command the ready messages are back, oldest first; a message taken before is not. `--queue scratch` (not durable) is empty after
  the restart. `;durable` with no `--directory` is accepted and keeps nothing (empty after a restart). A queue curl itself declared
  (not named by `--queue`) also kept its message with `--directory` (probed once, not claimed).
- Surl.Api: `options.Queues.Add("jobs;durable")`, `options.DataDirectory = folder`; `DisposeAsync` then a new `StartAsync` is the
  restart (new port); `data/.surl/lock` stays on disk after a stop but is released. Stop the server before deleting the folder, or
  the delete fails on `lock`. `RecordExchanges = true` is needed for `FindEvents`.
- A restarted server's event record is new: a job read from disk shows `delivered` and `acknowledged`, no `enqueued`.

## Work queue with a STOMP worker (batch-09, `amqp-process-a-work-queue-end-to-end`)
- STOMP worker file (NUL after each frame): `CONNECT` (`accept-version:1.2`, `host:/`, no login under `--allow-anonymous`),
  `SUBSCRIBE id:0 destination:/queue/jobs ack:client-individual`, `ACK id:1` ... , `DISCONNECT receipt:bye`; `curl -m 10 -T file
  telnet://h:<stomp port>/` writes `CONNECTED`, a `MESSAGE` frame per job (`message-id`, `ack` 1, 2, 3 per connection), then
  `RECEIPT`, exit 0. Jobs not acknowledged before the `DISCONNECT` are ready again (a later `curl` get takes them; the event record
  then shows `delivered` once more). Without `--allow-anonymous` or an account the worker gets `ERROR` `message:Authentication failed`
  (exit 0) and the app's `curl` AMQP get is 67 CRAM-MD5.
- Curl.Api can run the same worker: `new CurlRequest(HttpMethod.Put, "telnet://host:port/") { Body = frames }`; the response body
  is what the server answered. curl-mcp: `curl -T worker.stomp` with the file in its root (standard output may be base64 when
  it holds NUL bytes: check `standardOutput.isBase64`).
- Events: `get_events(store: "bus", kinds: ["store_changed"], max_events)` -> `{events:[{sequence, change, name, byte_count}]}`;
  `wait_for_event(after_sequence:)` answers `{event:{...}}`, and the next event after a send may be an earlier receive's
  `delivered`/`acknowledged`: loop until `change` is `enqueued`. Surl.Api: `server.FindEvents(new SurlEventQuery { Store = StoreKind.Bus })`
  `.OfType<StoreChangedEvent>()`, `Change` `Enqueued`/`Delivered`/`Acknowledged`.

## Recipes
amqps-log-in-over-tls, amqp-send-and-receive, amqp-seed-a-queue-and-browse-what-is-left, amqp-check-what-a-queue-still-holds, amqp-bridge-to-amqp-1-0, amqp-process-a-work-queue-end-to-end, amqp-keep-a-queue-across-restarts (each with C# and agent twins)

## AMQPS and PLAIN (amqps-log-in-over-tls and twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `surl --cert server.pem --key server.key --user alice:s3cret amqps://h:P/ amqp://h:Q/`: curl `--cacert server.pem -u alice:s3cret` over `amqps://h:P/%2F/orders` publishes (`-d`) and reads (exit 0; `--ssl-no-revoke` not needed). Same login over `amqp://` is exit 67 `AMQP server offers no mechanism this client can use (it offers "CRAM-MD5")`; with `--allow-plaintext-auth` on its own server it works, and a wrong password there is 403 as below. (curl has no `--allow-plaintext-auth`; exit 2.)
- Over TLS a wrong password, an unknown user and no `-u` are all 67 `AMQP server sent connection.close: reply code 403, ACCESS_REFUSED - login refused`. No `--cacert`: exit 60 (schannel wording). `surl --user a:b amqps://...` with no cert: exit 58 `needs a certificate`.
- Surl.Api: `ListenUrls = { "amqps://127.0.0.1:0", "amqp://127.0.0.1:0" }`, `CertificateFile`, `KeyFile`, `Accounts`, `AllowPlaintextAuthentication`, `Queues.Add`; Curl.Api GET with `CurlAuthenticationOptions` reads one message (`EnqueueAsync` puts it there; Curl.Api cannot publish); `CurlClientOptions { Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" } } }` trusts the cert; no CA: `CurlException` 60 `PeerFailedVerification`; refusals 67 `LoginDenied`; no certificate: `SurlServerStartException.ExitCode` `CertificateProblem` (58).
- surl-mcp `start_server` with an amqps listener and no `--cert`: tool error, structured `exit_code` 58 `CertificateProblem`. curl-mcp `curl` with a non-zero exit (67, 60) is a tool error (`IsError`); `standardError.content` holds `curl: (67) ...`. After `curl -d` wait with `wait_for_event(kinds:["store_changed"], store:"bus", name:"$queue/orders")` before reading.
- Linux and macOS not probed (CI checks).

## Browsing what an app queued (amqp-check-what-a-queue-holds-in-process, amqp-browse-a-queue-from-an-agent; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- Curl.Api CAN queue over STOMP: `new CurlRequest(HttpMethod.Post, $"{stompUrl}queue/jobs") { Body = CurlContent.FromBytes(bytes) }` is one SEND (status 0, empty body) and lands in the AMQP queue `jobs` (one bus). Needs `AllowAnonymous` (else `CurlException` 67 `STOMP server sent ERROR: Authentication failed`). A SEND to a queue the server was NOT started with (`/queue/job`) is accepted and CREATES that queue (browse of `job` finds the messages; `jobs` stays empty), unlike `EnqueueAsync` to an unknown queue, which is refused.
- `BrowseQueueAsync(queue, afterPosition, limit, ct)`: `Position` starts at 0 and counts the messages still ready (after a worker takes the first, the others are positions 0, 1), `HasMore` true with a limit smaller than the queue, `Headers` empty for STOMP and curl-published messages. Unknown queue: `SurlNotFoundException` `No queue nope.`; surl-mcp `browse_queue` the tool error `No queue nope.` An empty declared queue is `ready_count 0`, `messages []`, not an error.
- Agent: `curl -s -S -d "resize cat.png" amqp://h:p/%2F/jobs` with `--queue jobs --allow-anonymous` publishes (no `-u`); without `--allow-anonymous` exit 67 `AMQP server offers no mechanism this client can use (it offers "CRAM-MD5")`. After each publish `wait_for_event(kinds:["store_changed"], store:"bus", name:"$queue/jobs", after_sequence)` returns the `enqueued` event.
