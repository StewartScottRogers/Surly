<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# kafka facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`kafka://`, `kafkas://` (TLS, needs `--cert`/`--key`); TCP, `ready: port`. Every topic has one partition, 0.

## Server options that matter
- `--allow-anonymous` (loosens, warns): any client in, unchecked.
- `--user alice:s3cret`: an account. Mechanisms (`surl --aihelp kafka`, Surl's SCRAM ADR, new in 763754785): `kafka://` enables SCRAM-SHA-256 and SCRAM-SHA-512; `kafkas://` PLAIN as well; PLAIN over `kafka://` needs `--allow-plaintext-auth` (loosens, warns); `--auth` replaces the offer.
- With an `mqtt5://` listener in the same surl, Kafka `shop.orders` is MQTT `shop/orders`. Kafka to MQTT needs `--topic "shop/#;retain"` and `--mqtt5-disconnect-after-retained`, so curl's later subscriber gets the record and ends.

## Secure by default
- No login, no `--allow-anonymous`: connection closed, curl exit 56, `Connection disconnected`.
- `-u` over `kafka://` without `--allow-plaintext-auth`: curl exit 67, `Kafka broker does not enable SASL PLAIN, it enables: SCRAM-SHA-256, SCRAM-SHA-512`.
- Wrong password over PLAIN: exit 67, `SASL PLAIN authentication failed`.

## Client: curl
- `kafka://host:port/<topic>[/<partition>][?offset=N]`. `-d v` produces one record; no `-d` consumes from offset 0 (or N) to the end, each value and a newline, then exits 0 (no time limit needed).
- Broker errors exit 8 with `error N (NAME)`: 1 OFFSET_OUT_OF_RANGE (past the end; an offset equal to the count is empty, exit 0), 3 UNKNOWN_TOPIC_OR_PARTITION (partition 1), 17 INVALID_TOPIC_EXCEPTION (`a%20b`). No topic: exit 3, `No Kafka topic found`.
- SASL PLAIN only. `--cacert` for kafkas; untrusted: exit 60.

## MCP
Not probed.

## Gotchas
- Records stay after a consume: a log, not a queue.

## Recipes
kafka-produce-and-consume
