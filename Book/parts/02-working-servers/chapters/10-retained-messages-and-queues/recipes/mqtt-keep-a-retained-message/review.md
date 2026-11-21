<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-keep-a-retained-message

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.

## Findings

1. Every Discussion claim has a case. The "QoS 0 gets no answer" sentence cites the MQTT 3.1.1 specification, not a case.
2. Publish then subscribe in one `--next` chain is the book's idiom; on macOS a read straight after a QoS 0 publish can in principle be stale (facts/mqtt.md). The twins wait for the store event; the console recipe says so in prose.
