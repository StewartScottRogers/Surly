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
mqtt-publish-and-subscribe, mqtt-grant-a-topic, mqtt-grant-topics-to-a-group, mqtt-keep-a-retained-message, mqtt-subscribe-with-a-wildcard (all with twins), mqtt-change-retained-config-mid-test (and twins), mqtt5-check-a-retained-message-from-an-agent.

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
