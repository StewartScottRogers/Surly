<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-publish-and-subscribe
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 18 of 18 cases pass on windows.
## Findings
1. Fitness (3): `--mqtt5-disconnect-after-retained` is in the Solution because a `curl` subscribe ends only when the broker disconnects it; the Discussion says so, says what the option costs (the listener is no broker for anyone else), and the `broker-stays-open` case shows the alternative against a real broker: `-m 2`, exit 28, the retained message already written. The case has `timeout: 30` and a 2-second limit, so it is not a race.
2. Fitness (3): `--topic "home/#;retain"` is needed because `curl` publishes with RETAIN 0 (`outside-the-filter` shows nothing kept). Said in the Discussion.
3. Truth (7): each refusal and its loosening are cases on their own servers: no login (0x87, exit 67), clear password refused unchecked (0x87), `--allow-plaintext-auth` (accepted; wrong password 0x86), and the secure way over `mqtt5s://` with `--cacert` (accepted; exit 60 without it).
4. Cross-protocol: one `surl` (`bus`) listens on `mqtt5://` and `mqtt://`; a message crosses both ways, and the case pins the two clients' different output (MQTT 3.1.1 writes the topic-length header and topic; MQTT 5 the payload alone). Fixed while writing: the cross-protocol listener was first in the Solution's server; moved to its own server so the Solution shows only what it needs.
5. Truth (5): two matching retained topics come out concatenated with no separator; the order is not claimed (`stdout-matches '\A(6871|7168)\Z'`).
6. Cited, not cased: a broker never ends a subscription itself, and the option's effect on other clients (<<surl-readme>>); what `curl` writes and when it stops (<<curl-protocols>>); version 5.0 features in one clause (<<mqtt5>>).
7. Machine: the solution's server needs `openssl` only for the `tls` server; the Solution text says so.
