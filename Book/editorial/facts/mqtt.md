<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# mqtt facts
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19, windows.

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
mqtt-publish-and-subscribe, mqtt-grant-a-topic.

## Retained state (mqtt-check-retained-state-after-a-run)
- surl-mcp `read_retained_message(server_id, topic)` answers `{topic, qos, text}`; a topic with nothing kept is `{"retained": false}`, not a tool error. No call lists topics.
- A publish outside every `--topic ...;retain` filter is accepted (curl exit 0) and not kept. The last publish on a topic replaces the retained one.
- curl-mcp `curl(args: ["-s","-d","on", "mqtt://127.0.0.1:port/devices/lamp/state"])` publishes (answer `exitCode 0`, empty output); without `--allow-anonymous` exitCode 8.
- `start_server` answer `log_lines` carries the `--allow-anonymous` warning.
