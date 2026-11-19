<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: stomp-send-to-a-queue
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 13 of 13 cases pass on windows (six runs, five of them side by side with the two other recipes).
## Findings
1. Fitness (3): `--idle-timeout 1` is in the Solution so the subscription ends by itself with exit 0 once the queue is empty; the Curl port's STOMP client has no `?count=N` and reads until the broker closes. The `held-open` case shows the alternative a reader needs against another broker: `-m 2`, exit 28, the message still written. Both are deterministic: the queue is delivered at once and the idle timer is surl's own.
2. Fixed: removed "waits for the broker's receipt" and "made by the first send or subscribe", which no case showed; the queue is now said to be made by the first send that names it.
3. Truth: a send before any subscriber is kept (solution), a queue message is taken once (`taken-once`), order is kept (`in-order`), a topic keeps nothing (`topic-keeps-nothing`), and the queue is the same one AMQP 1.0 reads and writes (`to-amqp1`, `from-amqp1`). Round-robin among several live subscribers is not claimed: it needs concurrent subscribers.
4. Cross-protocol choice: AMQP 1.0 (`amqp1`), not AMQP 0-9-1. Under `--allow-anonymous` surl's AMQP 0-9-1 server offers `CRAM-MD5 ANONYMOUS` and the Curl port's `amqp://` client without `--user` exits 67 ("offers no PLAIN mechanism"); that belongs to the AMQP recipe's writer and is reported to the lead.
5. Cited, not cased: the start-up warnings, `stomps` needing a certificate (<<surl-readme>>).
6. Reader: STOMP and AMQP spelled out at first use in the Discussion and the Solution note; the Problem uses STOMP as the reader would search for it, as the MQTT recipe does.
