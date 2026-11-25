<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# mqtt facts
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19, windows; seeding and mqtt5 retained sections probed on Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20.

## Listen URLs and readiness
`mqtt` 1883, `mqtts` 8883 (needs a certificate); TCP, `ready: port`. MQTT 3.1.1 broker; in one surl
it is the message bus that NATS, STOMP, AMQP and Kafka share.

## Server options that matter
- `--allow-anonymous` (loosens, warns): a CONNECT with no user name is accepted.
- `--user alice:s3cret` with `--allow-plaintext-auth` (loosens, warns): password over `mqtt://` checked.
- `--topic "<filter>[;retain][;qos=n]"`: `retain` keeps every publish on a matching topic, whatever its
  RETAIN flag. Unknown flag exits 2 (`surl: option --topic: unknown flag retained`). In a `--topic`
  filter `#` may stand at any level (kept on purpose, Surl ADR-0177), matching zero or more levels:
  `shop/#/orders` matches `shop/orders` and `shop/eu/north/orders`, not `shop/eu/returns`. `#` or `+`
  must fill a whole level: `shop/a#` exits 2, `surl: (2) option --topic: topic shop/a# is not a valid topic filter`.
- `--topic-access "shop/#=ann:pubsub,bob:sub"`: once given, every publish and subscribe needs a grant.
  A member naming no account exits 2.
- `--directory`: keeps retained messages on disk; otherwise in memory.

## Secure by default
- No user name, no `--allow-anonymous`: CONNACK 5; curl exit 8, stderr `Expected 0000 but got 0005`.
- Password over `mqtt://` without `--allow-plaintext-auth`: CONNACK 5, unchecked.
- Wrong password (with the option): CONNACK 4, `got 0004`, exit 8.
- Subscribe with no grant: refused in SUBACK; exit 8 `Weird server reply`. Publish with no grant:
  dropped silently, exit 0 (QoS 0 gets no answer).

## Client: curl
`curl -d <msg> mqtt://h:p/<topic>` publishes (RETAIN 0 always); without `-d` subscribes. `--next`
chains them. No topic: exit 3 before connecting. `+` and `#` in the URL are `%2B`, `%23`.
Subscribe output per message: 2-byte big-endian topic length, topic, payload (`\x00\x11home/kitchen/temp71`);
use `stdout-matches` with `\A...\Z`.

## MCP
surl-mcp: `publish_message(server_id, topic, queue, payload, payload_base64, retain)`,
`read_retained_message(server_id, topic)`, `read_log(server_id, topic, from_offset, limit)`.

## Gotchas
- surl answers a subscribe with the matching retained messages, then sends DISCONNECT (server to
  client, outside MQTT 3.1.1; kept on purpose, Surl ADR-0014, ADR-0176) so curl exits 0, not 56. The
  subscribe ends on its own: no `-m` needed. Cite <<surl-readme>>; do not call it a bug.
- Without `--topic ...;retain`, a curl publish is not kept: a later subscribe gets nothing.
- `curl -v` on MQTT writes raw packet bytes to stderr; `build/probe.py` and verify cannot decode them
  on Windows (UnicodeDecodeError), so DISCONNECT is not cased.
- In YAML, quote a claim containing ` #`, or the rest is a comment.

## Recipes
mqtt-publish-and-subscribe, mqtt-grant-a-topic, mqtt-grant-topics-to-a-group, mqtt-keep-a-retained-message, mqtt-subscribe-with-a-wildcard (all with twins), mqtt-change-retained-config-mid-test (and twins), mqtt5-check-a-retained-message-from-an-agent, mqtt-watch-a-topic-during-a-test, mqtt-test-a-sensor-to-alert-flow (and twins).

## Retained state (mqtt-check-retained-state-after-a-run)
- surl-mcp `read_retained_message(server_id, topic)` answers `{topic, qos, text}`; a topic with nothing kept is `{"retained": false}`, not a tool error. No call lists topics.
- A publish outside every `--topic ...;retain` filter is accepted (curl exit 0) and not kept. The last publish on a topic replaces the retained one.
- curl-mcp `curl(args: ["-s","-d","on", "mqtt://127.0.0.1:port/devices/lamp/state"])` publishes (answer `exitCode 0`, empty output); without `--allow-anonymous` exitCode 8.
- `start_server` answer `log_lines` carries the `--allow-anonymous` warning.

## QoS 0 publish vs reading the store (surl-build-an-environment-from-a-spec, Surl 9ad60d80c, 2026-11-20)
- curl-mcp `curl -s -u app:apppass -d ready mqtt://H:P/env/status` (QoS 0, unacknowledged) returns before the broker keeps the message: `read_retained_message` straight after answered `{"retained":false}` 6 times in 6. Call `wait_for_event(kinds:["store_changed"], store:"bus", name:"env/status")` first; the event is `{store:"bus", change:"written", name:"env/status", byte_count:5}`. With `--user` the publish needs `--allow-plaintext-auth`.

## Seeding and changing retained messages (probed 2026-11-20, Surl 9ad60d80c / Curl 7d167fa10a)
- A seed is not a client's publish: `Surl.Api` `Messages.PublishAsync(topic, bytes, true)` and surl-mcp `publish_message(retain: true)` set RETAIN themselves, so the message is kept
  on a topic outside every `--topic ...;retain` filter and with no `--topic` at all; a curl publish (RETAIN 0) there is accepted (exit 0) and not kept. Reading back: `Messages.ReadRetainedAsync`
  (`null` if none; `.Payload`, `.QualityOfService` 0) and `read_retained_message`. An empty payload, seeded or from `curl -d ""`, clears the topic (`retained: false`).
  surl-mcp `read_retained_message` with a non-text payload answers `base64` in place of `text` (`payload_base64: "AP8B"`).
- `--directory state` needs `state/` to exist (else exit 37 `Could not open directory state`: make it with a fixture); retained messages then survive a restart (console `restart:` case needs `schema: 2`;
  agent: `stop_server` then a second `start_server`; C#: a second `SurlServer` on the same `DataDirectory`).
- Curl.Api as the device: `CurlRequest(HttpMethod.Get, "mqtt://host:port/topic")` answers a body of the topic's two-byte length, the topic and the message (empty if none), and returns once the retained messages are sent;
  `HttpMethod.Post` with `Body = CurlContent.FromBytes(...)` publishes (RETAIN 0). A broker without `AllowAnonymous`: `CurlException` code 8.
- curl-mcp on mqtt://: `curl -s mqtt://.../topic` answer `standardOutput.content` starts with the length bytes (`U+0000 U+0013` for a 19-character topic), exit 0.
  Without `--allow-anonymous`: exitCode 8, stderr `curl: (8) Expected 0000 but got 0005`.
- mqtt5: curl publish refused with no login is exit 67 `MQTT 5 CONNACK reason code 0x87 NotAuthorized`. A curl-mcp subscribe on `mqtt5://` needs `--mqtt5-disconnect-after-retained` on the broker, else it hangs.
  `--topic "devices/#;retain;qos=1"` is a cap (ADR-0102): a QoS 0 publish is still read back with `qos` 0. No tool or option in these builds publishes a retained message at QoS 1 or 2.

## Groups, wildcards and waiting for a publish (mqtt-grant-topics-to-a-group, mqtt-subscribe-with-a-wildcard, mqtt-keep-a-retained-message and twins; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20, windows)
- Groups: `--group name=member,...` (auth category of `surl --help`, not mqtt); a grant names one as `@name:right`, `--topic-access "plant/#=@sensors:pub,@dashboards:sub"`. Rights are `pub`, `sub`, `pubsub`; another word exits 2 `option --topic-access: is badly used here`.
  A bare group name in a grant is looked up as an account: exit 2 `surl: (2) option --topic-access: member sensors names no account or group`; same for `@robots`; `--group sensors=sensor1,ghost` exits 2 `option --group: member ghost names no account or group`.
  An account in neither group, or a dashboard (`sub`) that publishes: publish dropped, curl exit 0; a sensor (`pub`) or outsider that subscribes: exit 8 `Weird server reply`. Wrong password CONNACK 4 (`got 0004`); without `--allow-plaintext-auth` CONNACK 5.
  Surl.Api: `SurlServerOptions.Groups.Add("sensors=a,b")`, `TopicAccess.Add("plant/#=@sensors:pub")`, `Accounts`, `AllowPlaintextAuthentication`; a bad grant makes `SurlServer.StartAsync` throw `SurlServerStartException` with the same message. surl-mcp `start_server` with a bad grant: tool error, `exit_code` 2, `exit_code_name` `FailedInit`, text `surl: (2) option ...`.
- Subscribe filters (`--topic` is separate): `+` literal in the curl URL works; `#` must be `%23`, a plain `#` is a URL fragment and not sent (subscribes to nothing, exit 0). `sensors/#` also matches `sensors`; `+` one level; `sensors/%23/temp` and `sensors/k+/temp` are refused in the SUBACK, exit 8 `Weird server reply` (Curl.Api: `CurlException` code 8). No match: empty, exit 0. Several retained messages arrive back to back (length, topic, payload, no payload length); seen sorted by topic on Windows, so cases check which messages and the byte count, not order. Payload ends at the next 0x00 if topics are under 256 bytes.
- Waiting after a QoS 0 publish: Curl.Api POST or curl-mcp `curl -d` returns before the broker has applied it. C#: `RecordExchanges = true`, then `server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = last }, TimeSpan.FromSeconds(10))` and keep `ended.Sequence`; agent: `wait_for_event(server_id, kinds:["exchange_ended"], after_sequence, timeout_ms)`, answer carries `sequence` (inside `event` or flat). Works for dropped publishes too (nothing is stored, so no store_changed). A seed (`PublishAsync`, `publish_message`) needs no wait.
- Surl.Api `ReadRetainedAsync` and surl-mcp `read_retained_message` read the store without a client, so without grants: the way to show a dropped publish left the value alone.
- Console `seed:` (schema 2) runs curl publishes into the running server before the first case and prints them in the Solution.

## Seeded readings and the store, in process and from an agent (mqtt-publish-a-retained-reading-in-process, mqtt-read-a-retained-message-from-an-agent; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-21, windows)
- Surl.Api `Messages.PublishAsync(topic, bytes, true, ct)` on a broker with no `Topics` filter: kept at once (a Curl.Api GET straight after is sent it), `ReadRetainedAsync` gives `Payload` and `QualityOfService` 0. 100,000 random bytes and `00 FF 10 80` arrive unchanged; a later publish replaces; an empty payload clears (`ReadRetainedAsync` null, subscriber 0 bytes); a topic never published: 0 bytes, the GET ends by itself.
- Curl.Api POST on a broker with no filter: accepted, not kept. Take `last` (the highest `ExchangeEnded` `Sequence`) BEFORE the POST, then `WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = last }, 10 s)`; reading the sequence after the POST made the wait time out (the exchange had already ended).
- `AllowAnonymous = false`: the seed is kept and `ReadRetainedAsync` reads it, but the subscriber throws `CurlException` code 8 `WeirdServerReply`.
- Three seeded topics `home/kitchen/temp` 21.5, `home/hall/temp` 19.0, `home/cellar/temp` 12.5 and the filter `home/%23`: 65 bytes (2+16+4, 2+14+4, 2+16+4), all three topics present.
- surl-mcp `publish_message(server_id, topic, payload | payload_base64, retain)`: `retain: false` keeps nothing (`read_retained_message` `{"retained": false}`, subscriber empty, exit 0); `retain: true` is kept at once, `qos` 0, `text` or (non-UTF-8) `base64` (`AP8QgA==` for 00 FF 10 80). curl-mcp subscribe `curl -s -S mqtt://H:P/devices/lamp/state`: `standardOutput.content` (UTF-8 text) or base64 (`isBase64` true) holds length(2), topic, payload; exit 8 with `curl: (8) Expected 0000 but got 0005` without `--allow-anonymous`. `wait_for_event` after every curl call (subscribes too) with `after_sequence` advancing; the answer has `event.sequence` or flat `sequence`. 100,000-byte payload (base64 134 KB in the call) round-trips through `read_retained_message` and curl-mcp.

## MQTT 3.1.1 and MQTT 5 on one bus (mqtt-move-a-device-to-mqtt5 and twins; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-21, windows)
- `surl ... mqtt://127.0.0.1:{{port}}/ mqtt5://127.0.0.1:{{port2}}/` shares one bus (two TCP listeners, two ports). A curl publish over either is read back over the other; the mqtt:// subscriber is written length(2), topic, payload, mqtt5:// the payload alone; one retained message per topic; an empty publish from the other side clears it; outside `--topic ...;retain` neither keeps it. mqtt5:// subscribe needs `--mqtt5-disconnect-after-retained` to end.
- Refusals by version (`--user lamp:s3cret`): mqtt:// wrong password CONNACK 4, no login or unchecked clear password CONNACK 5 (exit 8 `Expected 0000 but got 0004|0005`); mqtt5:// wrong password `MQTT 5 CONNACK reason code 0x86 BadUserNameOrPassword`, no login or clear password without `--allow-plaintext-auth` `0x87 NotAuthorized`, exit 67. Curl.Api: 8 `WeirdServerReply` vs 67 `LoginDenied`.
- Curl.Api 0.2.0 does NOT publish over `mqtt5://`: `POST`/`PUT` with a body sends a SUBSCRIBE (packet 0x82) and nothing is kept, status 0, no error (reported). GET (subscribe) works over mqtt5 and, with `--mqtt5-disconnect-after-retained` in `AdditionalArguments`, returns the payload alone. Use `Messages.PublishAsync(..., true)` for the MQTT 5 side in C#.
- Surl.Api: `ListenUrls = { "mqtt://127.0.0.1:0", "mqtt5://127.0.0.1:0" }`, `BoundUrls[0|1]`; wait with `ExchangeEnded` after every client call, subscribes and refused ones too (take the highest sequence from the previous wait); `Topics`, `Accounts`, `AllowPlaintextAuthentication`. surl-mcp `start_server` with both URLs: `bound_urls[0|1]`; curl-mcp `curl -s -S -d 68 mqtt5://.../topic` publishes (exit 0).

## Live watching and a sensor-to-alert scenario (mqtt-watch-a-topic-during-a-test, mqtt-test-a-sensor-to-alert-flow and twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- A live watcher must be MQTT 5: `curl -s -m 60 mqtt5://h:p/topic -o f &` (no `--mqtt5-disconnect-after-retained` on the broker) stays subscribed and writes each payload alone as it arrives; `-o` grows the file as messages come (created at the first one). An `mqtt://` subscriber ends at once after the retained messages (exit 0, nothing if none). Left alone, the MQTT 5 watcher ends at `-m`, exit 28. `kill` from Git Bash stops it (143).
- Publish with `printf 'temp=21.5\n' | curl -s --data-binary @- mqtt://...` to keep a line feed per message; a marker published until it shows in the file (bounded) proves the watcher is subscribed.
- Shell service reading a retained message: `curl -s mqtt://h:p/$topic | tail -c +$(( ${#topic} + 3 ))` gives the payload.
- Mail under `--allow-anonymous`: the one mailbox's owner is the empty name (`store_changed` name `/INBOX/;UID=1`). Surl.Api `ListMessagesAsync("", "INBOX")` works; surl-mcp `list_messages`/`read_message` refuse `owner: ""` (schema minLength 1, McpProtocolException `Invalid params: arguments.owner: expected at least 1 characters`): read it over POP3 with the curl tool. See mail.md.

## MQTT 5 reason codes, grants and WebSocket (mqtt5-refuse-a-publish-by-reason-code, mqtt5-subscribe-over-websocket and twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- With `--topic-access`, mqtt5: a subscribe with no grant (outside every filter, a pub-only account, an account with no grant) is `curl: (8) MQTT 5 SUBACK reason code 0x87 NotAuthorized` (Curl.Api `CurlException` 8, same message). A wrong password is `CONNACK reason code 0x86 BadUserNameOrPassword`, no login or an unchecked clear password `0x87 NotAuthorized`, exit 67 (`CurlException` 67). A PUBLISH with no right is dropped, curl exit 0: curl publishes at QoS 0, so there is no PUBACK reason code to show; a read afterwards (or `read_retained_message`, which needs no grant) shows the old value.
- Curl.Api POST over `mqtt5://` still sends a SUBSCRIBE (nothing kept); publish in C# through a second `mqtt://` listener on the same bus (accounts and grants apply to it too). curl and the curl tool publish over `mqtt5://` and `mqtt5+ws://`.
- `mqtt5+ws://127.0.0.1:P/` listens on path `/mqtt`, subprotocol `mqtt`; curl `mqtt5+ws://h:p/topic` subscribes/publishes (curl adds `/mqtt`; the URL path is the topic), payload alone, same retained rules as mqtt5 (`--topic "dash/#;retain"`, `--mqtt5-disconnect-after-retained` ends the subscribe). Retained messages are shared with the TCP listeners. `curl ws://h:p/mqtt` (no subprotocol) exits 22 `Refused WebSocket upgrade: 400`; `ws://h:p/other` 404. No `mqtt5+wss` in `surl --version`. Over `mqtt5+ws` a password needs `--allow-plaintext-auth` like mqtt5:// (else 0x87). Surl.Api `ListenUrls = { "mqtt://127.0.0.1:0", "mqtt5+ws://127.0.0.1:0" }`, `BoundUrls[1]`; Curl.Api GET `mqtt5+ws://` returns the payload (needs `--mqtt5-disconnect-after-retained` in `AdditionalArguments`); `ws://` upgrade refusal `CurlException` 22.
- Do not run a hanging client in a probe: `curl -H "Sec-WebSocket-Protocol: mqtt" ws://.../mqtt` stays open.

## Checking a published reading and keeping readings across restarts (mqtt-check-an-app-published-a-reading, mqtt-keep-readings-across-restarts and twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- Check script (console): `curl -s mqtt://h:p/$topic | tail -c +$(( ${#topic} + 3 ))` polled every 0.5 s, bounded, until it equals the expected reading (a `sh` fixture; `sleep 0.5` is fine in Git Bash). A topic never published, outside the `--topic` filter, or on a server with no `--topic`: empty (`nothing kept`); a typo'd topic keeps the reading under the typo.
- `--directory state` (`state/` must exist): retained messages are ONE file `state/.surl/bus/retained` (header `SURL-BUS-RETAINED-1`, written when the first message is kept; with no `;retain` filter only `.surl/bus/logs/*` appear) beside `state/.surl/lock`. The lock file STAYS after a stop (Surl.Api `DisposeAsync`, `stop_server`); a stale lock is no obstacle to the next start. Outright stop (console `restart:`), then the same command: every kept topic comes back, a replaced reading comes back as the newer one, a publish outside the filter is gone, a `--directory`-less server starts over (nothing).
- Seeds survive too: `Messages.PublishAsync(topic, bytes, true)` and `publish_message(retain: true)` are written to disk and read back after a restart.
- Folder in use: console exit 124 `surl: (124) Directory state is in use by another surl process`; Surl.Api `SurlServerStartException.ExitCode` is an ENUM (`DataDirectoryInUse`, `CouldNotReadFile`: cast with `(int)` for 124 and 37), `Lines[0]` the message; surl-mcp `start_server` tool error `exit_code` 124 / 37, `lines[0]`. Missing folder: `surl: (37) Could not open directory nosuch` (Surl.Api line 0 `(37) Could not open directory <full path>`, no `surl:` prefix).
- C#: delete the temp `DataDirectory` only after the server is disposed (a held lock file throws on Windows); a restarted `SurlServer` has its own port and event sequence (restart `lastSequence` at 0). Console `restart:` verified with `ready: port` servers and a seed that publishes then polls.

## Why a subscriber missed a message (mqtt-find-why-a-subscriber-missed-a-message, Surl v0.1.5 d64faa4bb / Curl 2145a49eae, 2026-11-22, windows)
- `get_events(kinds:["store_changed"], store:"bus", name: topic)` tells publish outcomes apart: a granted publish under a `;retain` filter gives `written` then `published`; a granted publish outside every retain filter gives `published` only; a publish with no grant gives NO event (curl exit 0). These events carry no exchange_id; find the publisher by the PUBLISH packets in `get_exchanges` and the exchange's login_judged.
- MQTT chunks split packets and mix `text` and `base64` encodings: join each direction's bytes (decode base64, UTF-8 for text) and cut packets by the fixed header (type = first byte >> 4, remaining length varint). SUBSCRIBE (8): packet id, then filter (2-byte length); SUBACK (9): packet id then return code (0x00 granted, 0x80 refused, curl exit 8 `Weird server reply`); CONNACK (2) body[1] the return code (5 for a password over mqtt:// without `--allow-plaintext-auth`).

## Shared subscriptions and queues on an MQTT broker (mqtt5-share-a-subscription and twins, surl-browse-a-queue-from-an-agent; Surl v0.1.5 d64faa4bb / Curl 2145a49eae, 2026-11-24, windows)
- `curl mqtt5://h:p/%24share/workers/jobs` joins the shared group `workers`; each publish to `jobs` goes to one member (round robin seen, 3 and 3 of 6; claim only "each at least one, all once between them"); a plain `jobs` subscriber gets all. A live subscriber needs no `--mqtt5-disconnect-after-retained`; readiness: publish `ping` until every member's `-o` file is non-empty (curl creates it at the first message). A shared member is NOT sent retained messages (`--topic "jobs;retain"`): late member `-m 3` exit 28, empty; plain late subscriber gets the kept one. curl refuses `%24share/workers` (no topic filter), `%24share//jobs` and `%24share/wo%2Bkers/jobs` with exit 3 before CONNECT (`Invalid MQTT 5 topic: "$share/workers" has no ShareName followed by '/'.`; `ShareName "wo+kers" holds a wildcard.`).
- Curl.Api: `GET mqtt5://.../%24share/workers/jobs` with no disconnect option is a live subscription; `SendAsync` returns only when the FIRST payload arrives, then `Body` streams payloads until the token is cancelled. `server.Messages.PublishAsync("jobs", bytes, false)` reaches live subscribers (shared too). Same refusals as `CurlException` 3 `UrlMalformat` / 67 `LoginDenied`.
- surl-mcp: a subscriber's readiness shows in `get_exchanges`: the SUBACK is a `sent` chunk of its own whose first byte is 0x90 (CONNACK 0x20 is an earlier chunk). curl-mcp runs concurrent `curl` calls (start the subscribers' `CallToolAsync(...).AsTask()` and go on).
- Queues on an MQTT-only broker: `--queue jobs` declares it; `publish_message(queue)` puts, `browse_queue` lists (`base64` in place of `text` for non-UTF-8); `publish_message(topic: "jobs")` is the topic, not the queue; topic and queue both or neither: tool error `give exactly one of topic and queue`. An MQTT publish to `%24queue/jobs` is a TOPIC `$queue/jobs` (event `published`), not the queue. `--queue jobs --queue jobs` exits 2 `option --queue: queue jobs is given twice`; `--queue "bad;nope"` exits 2 `option --queue: unknown flag nope`. `reset_server` empties a declared queue and keeps it. In C#, `JsonSerializer.Serialize(JsonElement)` crashed a file-based MCP program (reflection off): print `StructuredContent.Value.GetRawText()`.

## Snapshots of the bus and durable queues in process (Surl v0.1.8 9f8005dff / Curl 2145a49eae, 2026-11-25, windows)
- surl-mcp `snapshot_server` / `restore_server` cover retained messages AND queues: after a test (curl publish, AMQP take, new topic) a restore puts back the seeded retained text, drops topics added since (`retained: false`) and returns taken jobs in order; the same snapshot id restores again and again. `reset_server` goes to the start state (seed gone, queue empty, declared queue kept). Unknown id: tool error `server s1 has no snapshot p9; it was never taken of this server, or was dropped as one past its newest 4 snapshots`. Events: `store_changed` `change` `restored` / `reset`, name `#`.
- Surl.Api durable queue on an MQTT-only broker: `Queues = { "jobs;durable", "scratch" }`, `DataDirectory`; `Messages.EnqueueAsync` x3, dispose, new `StartAsync` on the folder: `BrowseQueueAsync("jobs", -1, 100)` gives all three (Position 0..2); `scratch` and `;durable` with no `DataDirectory` are empty (ReadyCount 0). Files: `.surl/bus/queues/index` and `.surl/bus/queues/messages/0000000000000000` ... one per ready message. Undeclared queue: `SurlSeedRefusedException` `Bus: A message for queue job was refused: UnknownQueue.`. Second server on the folder: `SurlServerStartException` 124 `(124) Directory <full path> is in use by another surl process`.
