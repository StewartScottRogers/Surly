<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-subscribe-with-a-wildcard

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.

## Findings

1. Cases check which messages arrive and the byte count, not their order (observed sorted by topic on Windows; not relied on).
2. A plain `#` in the URL is a fragment and is not sent (case); the wildcard cases use `%23`. A literal `+` works.
3. The seed publishes go through `seed:`, shown in the Solution. A read straight after a QoS 0 publish can be stale on macOS in principle; the seed finishes before the first case starts.
