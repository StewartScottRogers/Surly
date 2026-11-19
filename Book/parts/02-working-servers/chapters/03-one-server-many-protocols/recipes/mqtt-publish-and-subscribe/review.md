<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-publish-and-subscribe
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 12 of 12 cases pass on windows.
## Findings
1. Fixed: a YAML comment swallowed the `#` claim (unquoted `#` after a space); quoted. Prose writes the wildcards as "plus sign" and "hash sign", since a bare `+` or `#` in backticks is AsciiDoc markup.
2. Fixed: the wrong-password case now asserts `got 0004` (CONNACK 4, bad user name or password); it had only asserted exit 8.
3. Truth (5), needs the author: the recipe depends on two surl behaviours its own help documents but MQTT 3.1.1 does not: surl keeps a publish with RETAIN 0 (curl sends fixed header 0x30) as the retained message, where section 3.3.1.3 says a server must not; and surl ends a subscription by sending DISCONNECT (0xE0), a client-to-server packet only (section 3.14). curl prints "Got DISCONNECT" and exits 0. The Discussion says plainly that surl makes every publish retained. Reported to the lead as a program finding.
4. Truth: the subscribe ends because surl disconnects, so `-m`/`--max-time` is not needed and not used; against a broker that keeps the subscription open, curl would wait until stopped. Not claimed, since no case can show it against surl.
5. Cited, not cased: the shared message bus reaching NATS and STOMP, `--directory` keeping retained messages, `mqtts` needing a certificate, the start-up warnings (<<surl-readme>>).
6. Reader: the Solution block shows three `surl` commands; a sentence under it says only the first is the Solution. The subscribe output starts with two raw bytes, so the solution expects `stdout-matches` and the book prints the pattern; the Discussion explains the bytes.
