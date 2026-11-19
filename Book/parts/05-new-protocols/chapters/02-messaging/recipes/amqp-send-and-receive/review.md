<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-send-and-receive
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 17 of 17 cases pass on windows.
## Findings
1. Fitness (3), needs the author: the Solution runs over `amqps://` with an account and a certificate, heavier than the other brokers' `--allow-anonymous` Solutions. It has to: `curl`'s AMQP 0-9-1 client logs in only with PLAIN (guest/guest without `-u`), and `surl` offers PLAIN only inside TLS or under `--allow-plaintext-auth`; `--allow-anonymous` alone offers ANONYMOUS, which `curl` does not use (case `anonymous-is-not-enough`, exit 67). The secure path needs no loosening at all, so it is the Solution. The lighter alternative would be `--allow-anonymous --allow-plaintext-auth` (two loosenings); not cased, not claimed.
2. Truth (7): refusals and loosening are cases: no `-u` (403, exit 67), wrong password over TLS (403), untrusted certificate (60), `amqp://` with no PLAIN on offer (67), `--allow-plaintext-auth` (accepted; wrong password 403).
3. Cross-protocol: `bus` listens on `amqps://` and `amqp1s://`; a message crosses AMQP 0-9-1 queue `orders` and AMQP 1.0 address `orders` both ways. `curl` publishes only through the default exchange, so it cannot reach MQTT or Kafka topics; probed (an `amqp` send to `shop.orders` was not seen by a Kafka consumer of `shop.orders`) and stated with a citation, not cased.
4. Truth (5): queue semantics are cased: empty queue exit 0 and no output, a receive takes the message off, oldest first; virtual host other than `/` is 530, exit 8; a path without a virtual host is exit 3.
5. Cited, not cased: `curl` declares the queue passively first and creates a missing one, and logs in as RabbitMQ's default account without `-u` (<<curl-protocols>>, from the Curl port's AMQP conformance notes).
6. Fixed while writing: the claims' `--next` transfers repeat `--cacert` and `-u`; the Discussion says why.
