<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# amqp facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`amqp://` (AMQP 0-9-1), `amqps://` (TLS, needs `--cert`/`--key`), `amqp1://` and `amqp1s://` (AMQP 1.0); TCP, `ready: port`. One virtual host, `/`.

## Server options that matter
- `--user alice:s3cret`: an account.
- Mechanisms (`surl --aihelp amqp`): `amqp://` offers CRAM-MD5 by default; PLAIN and AMQPLAIN only over TLS or with `--allow-plaintext-auth` (loosens, warns); `--auth` changes the set.
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

## MCP
Not probed.

## Gotchas
- One bus per surl: AMQP 0-9-1 queue `orders` is AMQP 1.0 address `orders`, both ways. curl publishes only to the default exchange, so it cannot reach MQTT or Kafka topics (that takes `amq.topic`).

## Recipes
amqp-send-and-receive
