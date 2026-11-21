<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-subscribe-with-a-wildcard-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl-mcp and curl-mcp from surl 1.0.0 (Surl commit 9ad60d80c) and the Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.

## Findings

1. Carries every console claim, with the misses printed as well. A subscription refused in the SUBACK is not a tool error: the curl tool answers exit code 8 in its JSON, and the program returns it.
2. Readings are seeded with `publish_message` (retain true), so no read follows a QoS 0 client publish and no wait is needed.
3. The payload has no length on the wire; the program ends it at the next zero byte. Said so in the prose.
