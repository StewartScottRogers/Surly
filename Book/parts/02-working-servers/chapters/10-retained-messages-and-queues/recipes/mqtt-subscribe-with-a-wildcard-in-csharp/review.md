<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-subscribe-with-a-wildcard-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): Surl.Api 1.0.0 (Surl commit 9ad60d80c), Curl.Api 0.2.0: 10 of 10 cases pass on windows.

## Findings

1. Carries every console claim (the filters, what each misses, nothing matching, a bare # as a fragment, # not last and a partial + refused with code 8). Every claim shows the misses too, which the console cases show by regex.
2. Readings are seeded with `PublishAsync`, which sets RETAIN, so no read follows a QoS 0 client publish and no wait is needed.
3. The payload has no length on the wire; the program ends it at the next zero byte. Fine for these topics, said so in the prose.
